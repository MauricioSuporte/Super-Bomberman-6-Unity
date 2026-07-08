using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Ability passiva de consciência de ControlBombs adversárias.
///
/// PROBLEMA RESOLVIDO:
///   O modelo nativo trata ControlBombs como fuse fixo de 0.65s, e a IA usa esse
///   tempo para "passar correndo" pela zona de blast. Mas uma ControlBomb adversária
///   explode QUANDO O DONO QUISER — qualquer presença na zona dela é risco máximo.
///
/// SOLUÇÃO:
///   ControlBombs que não são da própria IA são tratadas como perigo IMINENTE
///   (fuse efetivo 0): estar na zona dispara esquiva com peso altíssimo (vence o
///   sorteio ponderado de candidates) e o BFS de esquiva nunca atravessa zonas de
///   control adversária. As ControlBombs da PRÓPRIA IA são ignoradas aqui — elas
///   só explodem quando a IA aperta ActionB (BattleModeComControlBombAbility).
///
/// Sempre ativa para IAs COM.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerIdentity))]
[RequireComponent(typeof(MovementController))]
[RequireComponent(typeof(BombController))]
public sealed class BattleModeComControlBombAwarenessAbility : MonoBehaviour, IBattleModeComAbility
{
    // === Filtro de diagnóstico ===
    public const int DiagnosticPlayerIdFilter = 0; // 0 = todos
    public static readonly bool EnableControlAwarenessDiagnostics = false;
    private const float SurgicalLogIntervalSeconds = 0.35f;

    private const float DodgeStuckSeconds = 0.25f;

    private static readonly Vector2Int[] CardinalTiles =
    {
        Vector2Int.up,
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.right
    };

    // === Referências ===
    private PlayerIdentity identity;
    private MovementController movement;
    private BombController bombController;
    private AbilitySystem abilitySystem;
    private GameManager gameManager;
    private Tilemap groundTilemap;
    private Tilemap destructibleTilemap;
    private Tilemap indestructibleTilemap;
    private ContactFilter2D obstacleFilter;
    private Collider2D[] ownColliders;
    private readonly Collider2D[] obstacleHits = new Collider2D[12];
    private float tileSize = 1f;

    // === Detecção de travamento na esquiva ===
    private Vector2Int dodgeLastTile;
    private float dodgeStuckSince = -10f;
    private Vector2Int dodgeLastAttemptedStep;
    private readonly List<Vector2Int> dodgeBlockedSteps = new List<Vector2Int>(4);

    // === BFS reutilizável ===
    private struct SearchNode
    {
        public Vector2Int Parent;
        public int Depth;
    }

    private readonly Dictionary<Vector2Int, SearchNode> searchVisited =
        new Dictionary<Vector2Int, SearchNode>(96);
    private readonly Queue<Vector2Int> searchOpen = new Queue<Vector2Int>(96);

    // === Diagnóstico ===
    private string lastDecisionTrace = "not evaluated";
    private float lastSurgicalLogTime = -10f;
    private string lastSurgicalLogKey = string.Empty;

    // === IBattleModeComAbility ===
    public string DiagnosticName => "ControlBombAwareness";
    public string LastDecisionTrace => lastDecisionTrace;

    public bool IsAvailable
    {
        get
        {
            CacheReferences();
            return identity != null && movement != null && !movement.isDead;
        }
    }

    private bool CanPassBombs =>
        abilitySystem != null && abilitySystem.IsEnabled(BombPassAbility.AbilityId);

    private bool CanPassDestructibles =>
        abilitySystem != null && abilitySystem.IsEnabled(DestructiblePassAbility.AbilityId);

    private void Awake() => CacheReferences();
    private void OnEnable() => CacheReferences();

    private void CacheReferences()
    {
        if (identity == null) TryGetComponent(out identity);
        if (movement == null) TryGetComponent(out movement);
        if (bombController == null) TryGetComponent(out bombController);
        if (abilitySystem == null) TryGetComponent(out abilitySystem);

        ownColliders = GetComponentsInChildren<Collider2D>(true);

        if (movement != null)
        {
            tileSize = Mathf.Max(0.01f, movement.tileSize);
            obstacleFilter = new ContactFilter2D { useLayerMask = true, useTriggers = true };
            obstacleFilter.SetLayerMask(movement.obstacleMask);
        }

        gameManager = GameManager.Instance != null ? GameManager.Instance : FindAnyObjectByType<GameManager>();
        if (gameManager != null)
        {
            groundTilemap = gameManager.groundTilemap;
            destructibleTilemap = gameManager.destructibleTilemap;
            indestructibleTilemap = gameManager.indestructibleTilemap;
        }
    }

    // =====================================================================
    // Emergency / Candidate — mesma esquiva, pesos diferentes
    // =====================================================================
    public bool TryBuildEmergencyDecision(
        BattleModeComDifficultySettings settings,
        BattleModeComController controller,
        Vector2Int myTile,
        float currentDangerSeconds,
        out BattleModeComAbilityDecision decision)
    {
        decision = default;
        lastDecisionTrace = "emergency start";

        if (!IsAvailable)
        {
            lastDecisionTrace = "emergency unavailable";
            return false;
        }

        if (!IsInEnemyControlZone(myTile, out Vector2Int threatTile))
        {
            lastDecisionTrace = "emergency not in enemy control zone";
            return false;
        }

        return TryBuildDodgeDecision(settings, myTile, threatTile, 295 + DifficultyWeight(settings),
            "emergency", out decision);
    }

    public bool TryBuildCandidateDecision(
        BattleModeComDifficultySettings settings,
        BattleModeComController controller,
        Vector2Int myTile,
        out BattleModeComAbilityDecision decision)
    {
        decision = default;
        lastDecisionTrace = "candidate start";

        if (!IsAvailable)
        {
            lastDecisionTrace = "candidate unavailable";
            return false;
        }

        if (!IsInEnemyControlZone(myTile, out Vector2Int threatTile))
        {
            lastDecisionTrace = "candidate not in enemy control zone";
            ClearDodgeStuckState();
            dodgeLastTile = myTile;
            return false;
        }

        // ControlBomb adversária pode explodir A QUALQUER MOMENTO: estar na zona é
        // sempre urgente. Peso altíssimo para vencer o sorteio ponderado.
        return TryBuildDodgeDecision(settings, myTile, threatTile, 4000, "candidate", out decision);
    }

    private bool TryBuildDodgeDecision(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        Vector2Int threatTile,
        int weight,
        string phase,
        out BattleModeComAbilityDecision decision)
    {
        decision = default;

        UpdateDodgeStuckDetection(myTile);

        if (!TryFindControlSafeTile(settings, myTile, dodgeBlockedSteps,
                out Vector2 firstMove, out Vector2Int target, out int depth))
        {
            lastDecisionTrace = $"{phase} in enemy control zone but no safe route";
            LogSurgical("DODGE_NO_ROUTE",
                $"my:{myTile} threat:{threatTile} scan:{BuildNeighborScanSummary(myTile)}",
                force: true);
            return false;
        }

        dodgeLastAttemptedStep = Vector2Int.RoundToInt(firstMove);

        decision = new BattleModeComAbilityDecision
        {
            Action = BattleModeComActionType.Reposition,
            Weight = weight,
            TargetTile = target,
            HasTarget = true,
            FirstMove = firstMove,
            Reason = "control-dodge enemy control bomb zone",
            InputDescription = FirstMoveDescription(firstMove)
        };

        lastDecisionTrace = $"{phase} control dodge threat:{threatTile} target:{target} depth:{depth} w:{weight}";
        LogSurgical("DODGE",
            $"phase:{phase} my:{myTile} threat:{threatTile} target:{target} " +
            $"move:{FirstMoveDescription(firstMove)} depth:{depth} weight:{weight}");
        return true;
    }

    // =====================================================================
    // Zona de ControlBomb adversária
    // =====================================================================
    private bool IsInEnemyControlZone(Vector2Int tile, out Vector2Int threatTile)
    {
        threatTile = tile;

        foreach (Bomb bomb in Bomb.ActiveBombs)
        {
            if (!IsEnemyControlBomb(bomb))
                continue;

            Vector2Int bombTile = WorldToTile(bomb.GetLogicalPosition());
            int radius = bomb.Owner != null
                ? Mathf.Max(1, bomb.Owner.GetPredictedBlastRadius(bomb))
                : 2;

            if (IsTileInBlastLine(bombTile, tile, radius))
            {
                threatTile = bombTile;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// ControlBomb que NÃO pertence à própria IA — pode explodir a qualquer momento.
    /// (Inclui as de teammates: friendly fire existe e o aliado pode detonar.)
    /// </summary>
    private bool IsEnemyControlBomb(Bomb bomb)
    {
        return bomb != null &&
               !bomb.HasExploded &&
               !bomb.IsBeingHeldByPowerGlove &&
               bomb.IsControlBomb &&
               bomb.Owner != bombController;
    }

    // =====================================================================
    // BFS para tile fora de qualquer zona de control adversária
    // =====================================================================
    private bool TryFindControlSafeTile(
        BattleModeComDifficultySettings settings,
        Vector2Int start,
        List<Vector2Int> blockedFirstSteps,
        out Vector2 firstMove,
        out Vector2Int target,
        out int resultDepth)
    {
        firstMove = Vector2.zero;
        target = start;
        resultDepth = 0;

        searchVisited.Clear();
        searchOpen.Clear();
        searchVisited[start] = new SearchNode { Parent = start, Depth = 0 };

        if (blockedFirstSteps != null)
        {
            for (int i = 0; i < blockedFirstSteps.Count; i++)
            {
                if (blockedFirstSteps[i] != Vector2Int.zero)
                    searchVisited[start + blockedFirstSteps[i]] =
                        new SearchNode { Parent = start, Depth = 0 };
            }
        }

        searchOpen.Enqueue(start);
        int maxDepth = Mathf.Max(4, settings.searchDepth + 2);

        while (searchOpen.Count > 0)
        {
            Vector2Int tile = searchOpen.Dequeue();
            SearchNode node = searchVisited[tile];
            float eta = EstimateTraversalSeconds(node.Depth);

            if (node.Depth > 0 &&
                !IsInEnemyControlZone(tile, out _) &&
                float.IsInfinity(GetNonControlDangerSeconds(tile)) &&
                !IsNonControlDangerousAt(tile, eta + settings.safeTileMinimumSeconds, settings))
            {
                target = tile;
                resultDepth = node.Depth;
                firstMove = TileDirectionToVector(ReconstructFirstStep(start, tile));
                return firstMove != Vector2.zero;
            }

            if (node.Depth >= maxDepth)
                continue;

            for (int i = 0; i < CardinalTiles.Length; i++)
            {
                Vector2Int next = tile + CardinalTiles[i];
                if (searchVisited.ContainsKey(next))
                    continue;

                if (!IsWalkableTile(next, start))
                    continue;

                // Zonas de control adversária são NO-GO absoluto (exceto sair da
                // própria zona em que já estamos — o BFS naturalmente caminha para
                // fora porque o destino exige zona limpa).
                // Demais perigos: checagem de timing normal.
                if (node.Depth > 0 && IsInEnemyControlZone(next, out _))
                    continue;

                if (IsNonControlDangerousAt(next, EstimateTraversalSeconds(node.Depth + 1), settings))
                    continue;

                searchVisited[next] = new SearchNode { Parent = tile, Depth = node.Depth + 1 };
                searchOpen.Enqueue(next);
            }
        }

        return false;
    }

    // =====================================================================
    // Perigo de bombas não-control (timing normal)
    // =====================================================================
    private float GetNonControlDangerSeconds(Vector2Int tile)
    {
        float danger = float.PositiveInfinity;
        foreach (Bomb bomb in Bomb.ActiveBombs)
        {
            if (bomb == null || bomb.HasExploded || bomb.IsBeingHeldByPowerGlove)
                continue;

            if (IsEnemyControlBomb(bomb))
                continue; // tratado como zona NO-GO, não por timing

            Vector2Int bombTile = WorldToTile(bomb.GetLogicalPosition());
            int radius = bomb.Owner != null
                ? Mathf.Max(1, bomb.Owner.GetPredictedBlastRadius(bomb))
                : 2;
            if (!IsTileInBlastLine(bombTile, tile, radius))
                continue;

            float seconds = bomb.IsControlBomb ? 0.65f : bomb.RemainingFuseSeconds;
            danger = Mathf.Min(danger, seconds);
        }

        return danger;
    }

    private bool IsNonControlDangerousAt(
        Vector2Int tile,
        float arrivalSeconds,
        BattleModeComDifficultySettings settings)
    {
        float dangerSeconds = GetNonControlDangerSeconds(tile);
        if (float.IsInfinity(dangerSeconds))
            return false;

        return dangerSeconds <= arrivalSeconds + settings.dangerReactionSeconds;
    }

    private bool IsTileInBlastLine(Vector2Int origin, Vector2Int tile, int radius)
    {
        if (tile == origin)
            return true;

        Vector2Int delta = tile - origin;
        bool sameColumn = delta.x == 0 && delta.y != 0;
        bool sameRow = delta.y == 0 && delta.x != 0;
        if (!sameColumn && !sameRow)
            return false;

        int distance = Mathf.Abs(delta.x) + Mathf.Abs(delta.y);
        if (distance > radius)
            return false;

        Vector2Int dir = new(Mathf.Clamp(delta.x, -1, 1), Mathf.Clamp(delta.y, -1, 1));
        for (int step = 1; step < distance; step++)
        {
            Vector2Int check = origin + dir * step;
            if (HasIndestructibleTile(check) || HasDestructibleTile(check) || FindBombAt(check) != null)
                return false;
        }

        return true;
    }

    private Bomb FindBombAt(Vector2Int tile)
    {
        foreach (Bomb bomb in Bomb.ActiveBombs)
        {
            if (bomb == null || bomb.HasExploded || bomb.IsBeingHeldByPowerGlove)
                continue;

            if (WorldToTile(bomb.GetLogicalPosition()) == tile)
                return bomb;
        }

        return null;
    }

    // =====================================================================
    // Walkability (respeita BombPass/DestructiblePass se presentes)
    // =====================================================================
    private bool IsWalkableTile(Vector2Int tile, Vector2Int startTile)
    {
        if (!HasGroundTile(tile))
            return false;

        if (HasIndestructibleTile(tile))
            return false;

        if (HasDestructibleTile(tile) && !CanPassDestructibles)
            return false;

        if (FindBombAt(tile) != null && tile != startTile && !CanPassBombs)
            return false;

        if (movement != null && movement.obstacleMask.value != 0)
        {
            Vector2 center = TileToWorld(tile);
            Vector2 size = Vector2.one * (tileSize * 0.55f);
            int hitCount = Physics2D.OverlapBox(center, size, 0f, obstacleFilter, obstacleHits);
            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hit = obstacleHits[i];
                if (hit == null || IsOwnCollider(hit))
                    continue;

                if (hit.GetComponentInParent<ItemPickup>() != null ||
                    hit.GetComponentInParent<MountWorldPickup>() != null)
                    continue;

                if (hit.GetComponentInParent<PlayerIdentity>() != null)
                    continue;

                if (hit.GetComponentInParent<Bomb>() != null &&
                    (tile == startTile || CanPassBombs))
                    continue;

                if (CanPassDestructibles && IsDestructibleCollider(hit))
                    continue;

                return false;
            }
        }

        return true;
    }

    private static bool IsDestructibleCollider(Collider2D collider)
    {
        Transform current = collider != null ? collider.transform : null;
        int guard = 0;
        while (current != null && guard++ < 6)
        {
            if (current.CompareTag("Destructibles"))
                return true;

            current = current.parent;
        }

        return false;
    }

    // =====================================================================
    // Detecção de travamento
    // =====================================================================
    private void UpdateDodgeStuckDetection(Vector2Int myTile)
    {
        if (myTile == dodgeLastTile)
        {
            if (dodgeStuckSince < 0f)
            {
                dodgeStuckSince = Time.time;
            }
            else if (Time.time - dodgeStuckSince > DodgeStuckSeconds &&
                     dodgeLastAttemptedStep != Vector2Int.zero &&
                     !dodgeBlockedSteps.Contains(dodgeLastAttemptedStep))
            {
                dodgeBlockedSteps.Add(dodgeLastAttemptedStep);
                dodgeStuckSince = -1f;
                LogSurgical("DODGE_STUCK",
                    $"my:{myTile} blocking:{dodgeLastAttemptedStep} total:{dodgeBlockedSteps.Count}",
                    force: true);
            }
        }
        else
        {
            dodgeLastTile = myTile;
            ClearDodgeStuckState();
        }
    }

    private void ClearDodgeStuckState()
    {
        dodgeStuckSince = -1f;
        dodgeBlockedSteps.Clear();
        dodgeLastAttemptedStep = Vector2Int.zero;
    }

    // =====================================================================
    // Utilitários / diagnóstico
    // =====================================================================
    private static int DifficultyWeight(BattleModeComDifficultySettings settings) =>
        settings.difficulty switch
        {
            BattleModeComputerLevel.Easy => -20,
            BattleModeComputerLevel.Hard => 30,
            _ => 0
        };

    private string BuildNeighborScanSummary(Vector2Int myTile)
    {
        var sb = new System.Text.StringBuilder(96);
        string[] labels = { "U", "D", "L", "R" };
        for (int i = 0; i < CardinalTiles.Length; i++)
        {
            Vector2Int next = myTile + CardinalTiles[i];
            sb.Append(labels[i]).Append('[');

            if (!IsWalkableTile(next, myTile))
                sb.Append("blocked");
            else if (IsInEnemyControlZone(next, out _))
                sb.Append("control-zone");
            else
            {
                float danger = GetNonControlDangerSeconds(next);
                sb.Append(float.IsInfinity(danger) ? "walk" : $"danger {danger:F2}");
            }

            sb.Append("] ");
        }

        return sb.ToString();
    }

    private Vector2Int ReconstructFirstStep(Vector2Int start, Vector2Int goal)
    {
        Vector2Int current = goal;
        int guard = 0;
        while (searchVisited.TryGetValue(current, out SearchNode node) &&
               node.Parent != start &&
               current != start &&
               guard++ < 128)
        {
            current = node.Parent;
        }

        return current - start;
    }

    private bool HasGroundTile(Vector2Int tile)
    {
        if (groundTilemap == null)
            return true;

        return groundTilemap.HasTile(groundTilemap.WorldToCell(TileToWorld(tile)));
    }

    private bool HasDestructibleTile(Vector2Int tile) =>
        destructibleTilemap != null &&
        destructibleTilemap.HasTile(destructibleTilemap.WorldToCell(TileToWorld(tile)));

    private bool HasIndestructibleTile(Vector2Int tile) =>
        indestructibleTilemap != null &&
        indestructibleTilemap.HasTile(indestructibleTilemap.WorldToCell(TileToWorld(tile)));

    private bool IsOwnCollider(Collider2D colliderToCheck)
    {
        if (ownColliders == null)
            return false;

        for (int i = 0; i < ownColliders.Length; i++)
        {
            if (ownColliders[i] == colliderToCheck)
                return true;
        }

        return false;
    }

    private Vector2Int WorldToTile(Vector2 world)
    {
        float size = Mathf.Max(0.01f, tileSize);
        return new Vector2Int(
            Mathf.RoundToInt(world.x / size),
            Mathf.RoundToInt(world.y / size));
    }

    private Vector2 TileToWorld(Vector2Int tile)
    {
        float size = Mathf.Max(0.01f, tileSize);
        return new Vector2(tile.x * size, tile.y * size);
    }

    private float EstimateTraversalSeconds(int depth)
    {
        if (movement == null)
            return depth * 0.25f;

        float tilesPerSecond = Mathf.Max(1f, movement.speed);
        return depth / tilesPerSecond;
    }

    private static Vector2 TileDirectionToVector(Vector2Int dir) => new Vector2(dir.x, dir.y);

    private static string FirstMoveDescription(Vector2 move)
    {
        if (move == Vector2.zero) return "none";
        if (move.x > 0.5f) return "MoveRight";
        if (move.x < -0.5f) return "MoveLeft";
        if (move.y > 0.5f) return "MoveUp";
        if (move.y < -0.5f) return "MoveDown";
        return "none";
    }

    private void LogSurgical(string key, string message, bool force = false)
    {
        if (!EnableControlAwarenessDiagnostics) return;

        int id = identity != null ? Mathf.Clamp(identity.playerId, 1, 6) : 0;
        if (DiagnosticPlayerIdFilter != 0 && id != DiagnosticPlayerIdFilter) return;

        if (!force &&
            key == lastSurgicalLogKey &&
            Time.time - lastSurgicalLogTime < SurgicalLogIntervalSeconds)
            return;

        lastSurgicalLogKey = key;
        lastSurgicalLogTime = Time.time;
        Vector2Int tile = movement != null ? WorldToTile(movement.transform.position) : Vector2Int.zero;
        Debug.LogWarning($"[BattleCOM{DiagnosticName}][P{id}] tile:{tile} {key} {message}", this);
    }
}

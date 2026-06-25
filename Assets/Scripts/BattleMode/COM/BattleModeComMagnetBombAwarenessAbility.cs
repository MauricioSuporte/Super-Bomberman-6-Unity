using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Ability passiva de consciência de MagnetBombs adversárias.
///
/// MECÂNICA DA MAGNETBOMB (MagnetBomb.cs):
///   - Escaneia a cada ~0.08s até 12 tiles nas 4 direções.
///   - Se um jogador de OUTRO time aparece na mesma linha/coluna com linha livre,
///     a bomba é puxada até parar 1 tile antes do alvo (re-atração após ~1s).
///   - O fuse continua correndo durante a perseguição — a bomba explode onde parar.
///
/// PROBLEMA RESOLVIDO:
///   O modelo nativo calcula perigo pela posição ATUAL da bomba. A IA fica parada
///   alinhada com uma magnet bomb achando-se segura, a bomba desliza até ela e
///   explode a 1 tile de distância.
///
/// SOLUÇÃO:
///   Estar alinhado (linha/coluna livre, dentro do alcance de scan) com uma magnet
///   bomb inimiga é tratado como ameaça: o corredor de perseguição inteiro é zona
///   de risco com o fuse restante da bomba como prazo. A IA quebra o alinhamento
///   movendo-se perpendicular para um tile não-alinhado e seguro, com urgência
///   escalada pelo fuse e pela distância.
///
/// Sempre ativa para IAs COM.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerIdentity))]
[RequireComponent(typeof(MovementController))]
[RequireComponent(typeof(BombController))]
public sealed class BattleModeComMagnetBombAwarenessAbility : MonoBehaviour, IBattleModeComAbility
{
    // === Filtro de diagnóstico ===
    public const int DiagnosticPlayerIdFilter = 0; // 0 = todos
    public static readonly bool EnableMagnetAwarenessDiagnostics = false;
    private const float SurgicalLogIntervalSeconds = 0.35f;

    // === Constantes de comportamento ===
    // Alcance de scan da MagnetBomb (espelha scanMaxDistanceTiles do MagnetBomb.cs).
    private const int MagnetScanRangeTiles = 12;
    // Fuse abaixo disso ou bomba mais perto que isso = urgência máxima.
    private const float UrgentFuseSeconds = 2.5f;
    private const int UrgentDistanceTiles = 5;
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
    private int explosionMask;

    // === Ameaças ativas (recalculadas por avaliação) ===
    private struct MagnetThreat
    {
        public Bomb Bomb;
        public Vector2Int BombTile;
        public int Radius;
        public float Fuse;
        public int AlignedDistance; // distância na linha/coluna até a IA (-1 = não alinhado)
    }

    private readonly List<MagnetThreat> activeThreats = new List<MagnetThreat>(4);

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
    public string DiagnosticName => "MagnetBombAwareness";
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

        explosionMask = LayerMask.GetMask("Explosion");

        gameManager = GameManager.Instance != null ? GameManager.Instance : FindAnyObjectByType<GameManager>();
        if (gameManager != null)
        {
            groundTilemap = gameManager.groundTilemap;
            destructibleTilemap = gameManager.destructibleTilemap;
            indestructibleTilemap = gameManager.indestructibleTilemap;
        }
    }

    // =====================================================================
    // Emergency / Candidate
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

        if (!RefreshThreats(myTile) || !TryGetWorstAlignedThreat(out MagnetThreat worst))
        {
            lastDecisionTrace = "emergency no aligned magnet threat";
            return false;
        }

        return TryBuildDodgeDecision(settings, myTile, worst,
            295 + DifficultyWeight(settings), "emergency", out decision);
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

        if (!RefreshThreats(myTile) || !TryGetWorstAlignedThreat(out MagnetThreat worst))
        {
            lastDecisionTrace = "candidate no aligned magnet threat";
            ClearDodgeStuckState();
            dodgeLastTile = myTile;
            return false;
        }

        // Seleção de candidates é sorteio ponderado: urgência alta = peso que
        // não perde sorteio. A bomba se move rápido em direção à IA, então
        // alinhamento próximo ou fuse curto é urgência máxima.
        bool urgent = worst.Fuse <= UrgentFuseSeconds ||
                      worst.AlignedDistance <= UrgentDistanceTiles;
        int weight = urgent ? 4000 : 400 + DifficultyWeight(settings);

        return TryBuildDodgeDecision(settings, myTile, worst, weight, "candidate", out decision);
    }

    private bool TryBuildDodgeDecision(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        MagnetThreat worst,
        int weight,
        string phase,
        out BattleModeComAbilityDecision decision)
    {
        decision = default;

        UpdateDodgeStuckDetection(myTile);

        if (!TryFindUnalignedSafeTile(settings, myTile, dodgeBlockedSteps,
                out Vector2 firstMove, out Vector2Int target, out int depth))
        {
            lastDecisionTrace = $"{phase} aligned with magnet bomb but no unaligned route";
            LogSurgical("DODGE_NO_ROUTE",
                $"my:{myTile} bomb:{worst.BombTile} dist:{worst.AlignedDistance} fuse:{worst.Fuse:F2} " +
                $"scan:{BuildNeighborScanSummary(myTile)}",
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
            Reason = "magnet-dodge break alignment",
            InputDescription = FirstMoveDescription(firstMove)
        };

        lastDecisionTrace =
            $"{phase} magnet dodge bomb:{worst.BombTile} dist:{worst.AlignedDistance} " +
            $"fuse:{worst.Fuse:F2} target:{target} depth:{depth} w:{weight}";
        LogSurgical("DODGE",
            $"phase:{phase} my:{myTile} bomb:{worst.BombTile} dist:{worst.AlignedDistance} " +
            $"fuse:{worst.Fuse:F2} target:{target} move:{FirstMoveDescription(firstMove)} depth:{depth} weight:{weight}");
        return true;
    }

    // =====================================================================
    // Ameaças
    // =====================================================================

    /// <summary>
    /// Recalcula o snapshot de magnet bombs inimigas e o alinhamento com a IA.
    /// </summary>
    private bool RefreshThreats(Vector2Int myTile)
    {
        activeThreats.Clear();

        foreach (Bomb bomb in Bomb.ActiveBombs)
        {
            if (!IsEnemyMagnetBomb(bomb))
                continue;

            Vector2Int bombTile = WorldToTile(bomb.GetLogicalPosition());
            int radius = bomb.Owner != null
                ? Mathf.Max(1, bomb.Owner.GetPredictedBlastRadius(bomb))
                : 2;

            activeThreats.Add(new MagnetThreat
            {
                Bomb = bomb,
                BombTile = bombTile,
                Radius = radius,
                Fuse = bomb.RemainingFuseSeconds,
                AlignedDistance = GetAlignedDistance(bombTile, myTile)
            });
        }

        return activeThreats.Count > 0;
    }

    private bool IsEnemyMagnetBomb(Bomb bomb)
    {
        if (bomb == null || bomb.HasExploded || bomb.IsBeingHeldByPowerGlove)
            return false;

        if (!bomb.TryGetComponent<MagnetBomb>(out var magnet) || magnet == null)
            return false;

        // Bombas próprias não perseguem o dono.
        if (bomb.Owner == bombController)
            return false;

        // Em times: magnet de aliado não persegue a IA.
        if (bomb.Owner != null && IsAlly(bomb.Owner.PlayerId))
            return false;

        return true;
    }

    private bool IsAlly(int otherPlayerId)
    {
        if (BattleModeRules.Instance == null || !BattleModeRules.Instance.UsesTeams || identity == null)
            return false;

        return BattleModeRules.Instance.GetTeamForPlayer(identity.playerId) ==
               BattleModeRules.Instance.GetTeamForPlayer(otherPlayerId);
    }

    private bool TryGetWorstAlignedThreat(out MagnetThreat worst)
    {
        worst = default;
        float bestScore = float.PositiveInfinity;
        bool found = false;

        for (int i = 0; i < activeThreats.Count; i++)
        {
            MagnetThreat threat = activeThreats[i];
            if (threat.AlignedDistance < 0)
                continue;

            // Mais urgente = fuse curto e perto.
            float score = threat.Fuse + threat.AlignedDistance * 0.15f;
            if (score < bestScore)
            {
                bestScore = score;
                worst = threat;
                found = true;
            }
        }

        return found;
    }

    /// <summary>
    /// Distância de alinhamento (linha/coluna com linha de perseguição livre,
    /// dentro do alcance de scan da magnet bomb), ou -1 se não alinhado.
    /// Espelha o TryScanDirection do MagnetBomb: a perseguição é bloqueada por
    /// paredes, blocos destrutíveis e outras bombas entre a bomba e o alvo.
    /// </summary>
    private int GetAlignedDistance(Vector2Int bombTile, Vector2Int targetTile)
    {
        Vector2Int delta = targetTile - bombTile;
        bool sameColumn = delta.x == 0 && delta.y != 0;
        bool sameRow = delta.y == 0 && delta.x != 0;
        if (!sameColumn && !sameRow)
            return -1;

        int distance = Mathf.Abs(delta.x) + Mathf.Abs(delta.y);
        if (distance > MagnetScanRangeTiles)
            return -1;

        Vector2Int dir = new(Mathf.Clamp(delta.x, -1, 1), Mathf.Clamp(delta.y, -1, 1));
        for (int step = 1; step < distance; step++)
        {
            Vector2Int check = bombTile + dir * step;
            if (HasIndestructibleTile(check) || HasDestructibleTile(check))
                return -1;

            Bomb blocking = FindBombAt(check);
            if (blocking != null)
                return -1;
        }

        return distance;
    }

    /// <summary>
    /// True se o tile está alinhado (perseguível) com alguma magnet bomb inimiga.
    /// Usado pelo BFS: o destino da esquiva precisa quebrar TODOS os alinhamentos.
    /// </summary>
    private bool IsTileAlignedWithAnyMagnet(Vector2Int tile)
    {
        for (int i = 0; i < activeThreats.Count; i++)
        {
            if (GetAlignedDistance(activeThreats[i].BombTile, tile) >= 0)
                return true;
        }

        return false;
    }

    /// <summary>
    /// True se o tile está na zona de blast da posição ATUAL de alguma magnet
    /// bomb inimiga (a bomba pode explodir antes/no fim da perseguição).
    /// </summary>
    private bool IsTileInMagnetBlast(Vector2Int tile)
    {
        for (int i = 0; i < activeThreats.Count; i++)
        {
            MagnetThreat threat = activeThreats[i];
            if (IsTileInBlastLine(threat.BombTile, tile, threat.Radius))
                return true;
        }

        return false;
    }

    // =====================================================================
    // BFS para tile não-alinhado e seguro
    // =====================================================================
    private bool TryFindUnalignedSafeTile(
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

        // Passada 1 (estrita): destino não-alinhado E fora do blast atual das magnets.
        // Passada 2 (fallback): apenas não-alinhado.
        for (int pass = 0; pass < 2; pass++)
        {
            bool strict = pass == 0;

            if (pass == 1)
            {
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
            }

            while (searchOpen.Count > 0)
            {
                Vector2Int tile = searchOpen.Dequeue();
                SearchNode node = searchVisited[tile];
                float eta = EstimateTraversalSeconds(node.Depth);

                bool unaligned = !IsTileAlignedWithAnyMagnet(tile);
                bool clearOfBlast = !strict || !IsTileInMagnetBlast(tile);
                if (node.Depth > 0 &&
                    unaligned &&
                    clearOfBlast &&
                    float.IsInfinity(GetDangerSeconds(tile)) &&
                    !IsDangerousAt(tile, eta + settings.safeTileMinimumSeconds, settings))
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

                    if (IsDangerousAt(next, EstimateTraversalSeconds(node.Depth + 1), settings))
                        continue;

                    searchVisited[next] = new SearchNode { Parent = tile, Depth = node.Depth + 1 };
                    searchOpen.Enqueue(next);
                }
            }
        }

        return false;
    }

    // =====================================================================
    // Perigo / blast line
    // =====================================================================
    private float GetDangerSeconds(Vector2Int tile)
    {
        if (explosionMask != 0)
        {
            Collider2D explosion = Physics2D.OverlapCircle(TileToWorld(tile), tileSize * 0.25f, explosionMask);
            if (explosion != null)
                return 0f;
        }

        float danger = float.PositiveInfinity;
        foreach (Bomb bomb in Bomb.ActiveBombs)
        {
            if (bomb == null || bomb.HasExploded || bomb.IsBeingHeldByPowerGlove)
                continue;

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

    private bool IsDangerousAt(
        Vector2Int tile,
        float arrivalSeconds,
        BattleModeComDifficultySettings settings)
    {
        float dangerSeconds = GetDangerSeconds(tile);
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

                if (hit.GetComponentInParent<ItemPickup>() != null)
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
            else if (IsTileAlignedWithAnyMagnet(next))
                sb.Append("aligned");
            else
            {
                float danger = GetDangerSeconds(next);
                sb.Append(float.IsInfinity(danger) ? "walk" : $"danger {danger:F2}");
            }

            sb.Append("] ");
        }

        sb.Append($"magnets:{activeThreats.Count}");
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
        if (!EnableMagnetAwarenessDiagnostics) return;

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

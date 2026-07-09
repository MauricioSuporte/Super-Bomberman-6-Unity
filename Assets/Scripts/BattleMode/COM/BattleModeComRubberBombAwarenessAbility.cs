using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Ability passiva de consciência de RubberBombs em movimento.
///
/// PROBLEMA RESOLVIDO:
///   Uma RubberBomb chutada ricocheteia: ao colidir com algo ela INVERTE a direção
///   e volta (Bomb.KickRoutineFixed). O controller calcula perigo apenas pela posição
///   ATUAL da bomba, então a IA é pega desprevenida quando a bomba retorna na direção
///   dela — inclusive bombas que a própria IA chutou.
///
/// SOLUÇÃO:
///   Enquanto uma RubberBomb estiver sendo chutada, o corredor inteiro de viagem
///   (a linha/coluna entre os obstáculos que delimitam o vai-e-vem) é tratado como
///   zona de risco. Se a IA estiver nesse corredor, esta ability gera uma decisão de
///   reposicionamento PERPENDICULAR para fora da lane — em emergency (prioridade
///   sobre a fuga nativa) e também proativamente como candidate.
///
/// Sempre ativa para IAs COM (como a HazardAwareness) — a ameaça independe dos
/// power-ups da própria IA.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerIdentity))]
[RequireComponent(typeof(MovementController))]
[RequireComponent(typeof(BombController))]
public sealed class BattleModeComRubberBombAwarenessAbility : MonoBehaviour, IBattleModeComAbility
{
    // === Filtro de diagnóstico ===
    public const int DiagnosticPlayerIdFilter = 0; // 0 = todos
    public static readonly bool EnableRubberAwarenessDiagnostics = false;
    private const float SurgicalLogIntervalSeconds = 0.35f;

    // === Constantes de comportamento ===
    // Comprimento máximo do corredor rastreado a partir da bomba (cada direção).
    private const int MaxLaneTilesPerDirection = 14;
    // Distância (em tiles, ao longo da lane) em que a ameaça é tratada como urgente.
    private const int UrgentLaneDistanceTiles = 6;
    // Tempo parado no mesmo tile para considerar travamento na esquiva.
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

    // === Lanes ativas (recalculadas por avaliação) ===
    private struct RubberThreat
    {
        public Bomb Bomb;
        public Vector2Int BombTile;
        public Vector2Int Axis;          // (1,0) = horizontal, (0,1) = vertical
        public Vector2Int LaneMin;       // extremo negativo do corredor
        public Vector2Int LaneMax;       // extremo positivo do corredor
        public int Radius;               // raio de blast previsto
        public int DistanceToMe;         // distância na lane até a IA (-1 = fora)
    }

    private readonly List<RubberThreat> activeThreats = new List<RubberThreat>(4);
    private readonly HashSet<Vector2Int> laneTiles = new HashSet<Vector2Int>();

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
    public string DiagnosticName => "RubberBombAwareness";
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
    // TryBuildEmergencyDecision — esquiva urgente da lane
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

        if (!RefreshThreats(myTile) || !IsOnAnyLane(myTile, out RubberThreat worst))
        {
            lastDecisionTrace = "emergency no rubber lane threat";
            return false;
        }

        // Em emergency só interfere se a ameaça é urgente (bomba perto na lane) —
        // caso contrário deixa a fuga nativa cuidar do perigo que disparou o inDanger.
        if (worst.DistanceToMe > UrgentLaneDistanceTiles)
        {
            lastDecisionTrace =
                $"emergency rubber lane far dist:{worst.DistanceToMe} (native escape handles)";
            return false;
        }

        return TryBuildDodgeDecision(settings, myTile, worst, 290, "emergency", out decision);
    }

    // =====================================================================
    // TryBuildCandidateDecision — esquiva proativa
    // =====================================================================
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

        if (!RefreshThreats(myTile) || !IsOnAnyLane(myTile, out RubberThreat worst))
        {
            lastDecisionTrace = "candidate no rubber lane threat";
            ClearDodgeStuckState();
            dodgeLastTile = myTile;
            return false;
        }

        // Proativo: qualquer presença na lane de uma rubber em movimento justifica sair.
        // A seleção de candidates é SORTEIO PONDERADO — peso modesto perde para
        // farm/patrol e a IA fica parada na lane. Bomba chutada viaja ~10 tiles/s,
        // então presença na lane é sempre urgente: peso alto para vencer o sorteio.
        int weight = worst.DistanceToMe <= UrgentLaneDistanceTiles
            ? 4000
            : 400 + DifficultyWeight(settings);
        return TryBuildDodgeDecision(settings, myTile, worst, weight, "candidate", out decision);
    }

    // =====================================================================
    // Esquiva
    // =====================================================================
    private bool TryBuildDodgeDecision(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        RubberThreat worst,
        int weight,
        string phase,
        out BattleModeComAbilityDecision decision)
    {
        decision = default;

        UpdateDodgeStuckDetection(myTile);

        if (!TryFindOffLaneTile(settings, myTile, dodgeBlockedSteps,
                out Vector2 firstMove, out Vector2Int target, out int depth))
        {
            lastDecisionTrace = $"{phase} on rubber lane but no off-lane route";
            LogSurgical("DODGE_NO_ROUTE",
                $"my:{myTile} bomb:{worst.BombTile} dist:{worst.DistanceToMe} " +
                $"lane:{worst.LaneMin}->{worst.LaneMax} scan:{BuildNeighborScanSummary(myTile)}",
                force: true);
            return false;
        }

        dodgeLastAttemptedStep = Vector2Int.RoundToInt(firstMove);

        decision = new BattleModeComAbilityDecision
        {
            Action = BattleModeComActionType.Reposition,
            Weight = weight + DifficultyWeight(settings),
            TargetTile = target,
            HasTarget = true,
            FirstMove = firstMove,
            Reason = "rubber-dodge moving rubber bomb lane",
            InputDescription = FirstMoveDescription(firstMove)
        };

        lastDecisionTrace =
            $"{phase} rubber dodge bomb:{worst.BombTile} dist:{worst.DistanceToMe} target:{target} depth:{depth}";
        LogSurgical("DODGE",
            $"phase:{phase} my:{myTile} bomb:{worst.BombTile} dir:{worst.Axis} dist:{worst.DistanceToMe} " +
            $"target:{target} move:{FirstMoveDescription(firstMove)} depth:{depth} weight:{weight}");
        return true;
    }

    // =====================================================================
    // Ameaças / lanes
    // =====================================================================

    /// <summary>
    /// Recalcula as lanes de todas as RubberBombs em movimento. Retorna false se
    /// não houver nenhuma.
    /// </summary>
    private bool RefreshThreats(Vector2Int myTile)
    {
        activeThreats.Clear();
        laneTiles.Clear();

        foreach (Bomb bomb in Bomb.ActiveBombs)
        {
            if (bomb == null || bomb.HasExploded || !bomb.IsRubberBomb || !bomb.IsBeingKicked)
                continue;

            Vector2 kickDir = bomb.CurrentKickDirection;
            if (kickDir == Vector2.zero)
                continue;

            Vector2Int bombTile = WorldToTile(bomb.GetLogicalPosition());
            Vector2Int axis = Mathf.Abs(kickDir.x) >= Mathf.Abs(kickDir.y)
                ? Vector2Int.right
                : Vector2Int.up;

            // Corredor do vai-e-vem: caminha nas duas direções até um bloqueio.
            Vector2Int laneMin = bombTile;
            Vector2Int laneMax = bombTile;
            for (int step = 1; step <= MaxLaneTilesPerDirection; step++)
            {
                Vector2Int tile = bombTile + axis * step;
                if (BlocksLane(tile, bomb)) break;
                laneMax = tile;
            }

            for (int step = 1; step <= MaxLaneTilesPerDirection; step++)
            {
                Vector2Int tile = bombTile - axis * step;
                if (BlocksLane(tile, bomb)) break;
                laneMin = tile;
            }

            int radius = bomb.Owner != null
                ? Mathf.Max(1, bomb.Owner.GetPredictedBlastRadius(bomb))
                : 2;

            var threat = new RubberThreat
            {
                Bomb = bomb,
                BombTile = bombTile,
                Axis = axis,
                LaneMin = laneMin,
                LaneMax = laneMax,
                Radius = radius,
                DistanceToMe = LaneDistance(bombTile, axis, laneMin, laneMax, myTile)
            };
            activeThreats.Add(threat);

            // Registra os tiles do corredor para o BFS evitar.
            Vector2Int cursor = laneMin;
            int guard = 0;
            while (guard++ <= 2 * MaxLaneTilesPerDirection + 1)
            {
                laneTiles.Add(cursor);
                if (cursor == laneMax) break;
                cursor += axis;
            }
        }

        return activeThreats.Count > 0;
    }

    /// <summary>
    /// True se o tile bloqueia o vai-e-vem da rubber bomb (paredes, blocos, bombas).
    /// </summary>
    private bool BlocksLane(Vector2Int tile, Bomb movingBomb)
    {
        if (!HasGroundTile(tile))
            return true;

        if (HasIndestructibleTile(tile) || HasDestructibleTile(tile))
            return true;

        Bomb other = FindBombAt(tile);
        return other != null && other != movingBomb;
    }

    private bool IsOnAnyLane(Vector2Int myTile, out RubberThreat worst)
    {
        worst = default;
        int bestDistance = int.MaxValue;
        bool found = false;

        for (int i = 0; i < activeThreats.Count; i++)
        {
            RubberThreat threat = activeThreats[i];
            if (threat.DistanceToMe < 0)
                continue;

            if (threat.DistanceToMe < bestDistance)
            {
                bestDistance = threat.DistanceToMe;
                worst = threat;
                found = true;
            }
        }

        return found;
    }

    /// <summary>
    /// Distância ao longo da lane do tile da bomba até myTile, ou -1 se myTile
    /// não está no corredor.
    /// </summary>
    private static int LaneDistance(
        Vector2Int bombTile,
        Vector2Int axis,
        Vector2Int laneMin,
        Vector2Int laneMax,
        Vector2Int myTile)
    {
        if (axis == Vector2Int.right)
        {
            if (myTile.y != bombTile.y) return -1;
            if (myTile.x < laneMin.x || myTile.x > laneMax.x) return -1;
            return Mathf.Abs(myTile.x - bombTile.x);
        }

        if (myTile.x != bombTile.x) return -1;
        if (myTile.y < laneMin.y || myTile.y > laneMax.y) return -1;
        return Mathf.Abs(myTile.y - bombTile.y);
    }

    private bool IsTileOnAnyLane(Vector2Int tile) => laneTiles.Contains(tile);

    /// <summary>
    /// True se o tile fica na linha de blast de algum ponto possível de parada
    /// da rubber bomb (os dois extremos do corredor) — esses são os lugares onde
    /// ela vai explodir com mais probabilidade.
    /// </summary>
    private bool IsTileInLaneEndBlast(Vector2Int tile)
    {
        for (int i = 0; i < activeThreats.Count; i++)
        {
            RubberThreat threat = activeThreats[i];
            if (IsTileInBlastLine(threat.LaneMin, tile, threat.Radius) ||
                IsTileInBlastLine(threat.LaneMax, tile, threat.Radius))
                return true;
        }

        return false;
    }

    // =====================================================================
    // BFS para fora da lane
    // =====================================================================
    private bool TryFindOffLaneTile(
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
        int maxDepth = Mathf.Max(4, settings.searchDepth);

        // Primeira passada: alvo ideal — fora da lane E fora do blast dos extremos.
        // Segunda passada (fallback): apenas fora da lane.
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

                bool offLane = !IsTileOnAnyLane(tile);
                bool clearOfEndBlast = !strict || !IsTileInLaneEndBlast(tile);
                if (node.Depth > 0 &&
                    offLane &&
                    clearOfEndBlast &&
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
    // Walkability / perigo (respeita BombPass/DestructiblePass se presentes)
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
            {
                sb.Append("blocked");
            }
            else if (IsTileOnAnyLane(next))
            {
                sb.Append("lane");
            }
            else
            {
                float danger = GetDangerSeconds(next);
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

    private static string FormatDanger(float seconds)
    {
        if (float.IsInfinity(seconds)) return "safe";
        if (seconds <= 0f) return "now";
        return $"{seconds:F2}";
    }

    private void LogSurgical(string key, string message, bool force = false)
    {
        if (!EnableRubberAwarenessDiagnostics) return;

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

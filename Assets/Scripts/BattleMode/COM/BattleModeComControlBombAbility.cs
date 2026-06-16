using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Ability de IA para usar as próprias ControlBombs.
///
/// REGRA FUNDAMENTAL:
///   ActionB detona a ControlBomb MAIS ANTIGA da IA em campo
///   (BombController.TryExplodeOldestControlledBomb). Toda decisão desta ability
///   avalia exclusivamente essa bomba (via PeekOldestControlledBomb) — nunca as
///   mais novas.
///
/// COMPORTAMENTOS:
///   1. DETONAÇÃO OFENSIVA — inimigo na zona da bomba mais antiga, sem risco para
///      si/aliados → TapActionB.
///   2. RECUO PRÉ-DETONAÇÃO — inimigo na zona MAS a IA também está → sai da zona
///      primeiro; a detonação acontece num Think seguinte.
///   3. LIBERAÇÃO DE SLOT — sem bombas restantes, bomba mais antiga sem alvo e sem
///      risco para si/aliados → detona para reciclar o slot.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerIdentity))]
[RequireComponent(typeof(MovementController))]
[RequireComponent(typeof(BombController))]
public sealed class BattleModeComControlBombAbility : MonoBehaviour, IBattleModeComAbility
{
    // === Filtro de diagnóstico ===
    public const int DiagnosticPlayerIdFilter = 0; // 0 = todos
    public static readonly bool EnableControlBombDiagnostics = false;
    private const float SurgicalLogIntervalSeconds = 0.35f;

    // === Constantes de comportamento ===
    // Cooldown entre detonações decididas por esta ability.
    private const float DetonateCooldownSeconds = 0.6f;
    // Cooldown maior para a detonação de liberação de slot (não é urgente).
    private const float FreeSlotCooldownSeconds = 2.5f;
    // Bomba mais antiga sem alvo há mais que isso → detona para reciclar e
    // destravar a IA (sem isso a IA fica eternamente esperando a bomba "explodir").
    private const float StaleDetonateSeconds = 3.0f;
    private const float RetreatStuckSeconds = 0.25f;

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
    private readonly List<PlayerIdentity> activePlayers = new List<PlayerIdentity>(6);
    private float tileSize = 1f;

    // === Estado ===
    private float nextDetonateTime = -10f;
    private float nextFreeSlotTime = -10f;
    private Bomb trackedOldestBomb;
    private float trackedOldestSince = -10f;

    // === Detecção de travamento no recuo ===
    private Vector2Int retreatLastTile;
    private float retreatStuckSince = -10f;
    private Vector2Int retreatLastAttemptedStep;
    private readonly List<Vector2Int> retreatBlockedSteps = new List<Vector2Int>(4);

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
    public string DiagnosticName => "ControlBomb";
    public string LastDecisionTrace => lastDecisionTrace;

    public bool IsAvailable
    {
        get
        {
            CacheReferences();
            if (identity == null || movement == null || movement.isDead)
                return false;

            return abilitySystem != null &&
                   abilitySystem.IsEnabled(ControlBombAbility.AbilityId);
        }
    }

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
    // Emergency — não interfere na fuga; detonação só sem perigo ativo.
    // =====================================================================
    public bool TryBuildEmergencyDecision(
        BattleModeComDifficultySettings settings,
        BattleModeComController controller,
        Vector2Int myTile,
        float currentDangerSeconds,
        out BattleModeComAbilityDecision decision)
    {
        decision = default;
        lastDecisionTrace = "emergency: control bomb usage is candidate-only";
        return false;
    }

    // =====================================================================
    // Candidate — detonação ofensiva / recuo / liberação de slot
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

        Bomb oldest = bombController != null ? bombController.PeekOldestControlledBomb() : null;
        if (oldest == null || oldest.HasExploded || oldest.IsBeingHeldByPowerGlove)
        {
            lastDecisionTrace = "candidate no own control bomb on field";
            ClearRetreatStuckState();
            retreatLastTile = myTile;
            trackedOldestBomb = null;
            return false;
        }

        // Rastreia há quanto tempo esta bomba é a "mais antiga" (idade aproximada).
        if (trackedOldestBomb != oldest)
        {
            trackedOldestBomb = oldest;
            trackedOldestSince = Time.time;
        }

        Vector2Int bombTile = WorldToTile(oldest.GetLogicalPosition());
        int radius = Mathf.Max(1, bombController.GetPredictedBlastRadius(oldest));

        bool selfInZone = IsTileInBlastLine(bombTile, myTile, radius);
        bool enemyInZone = AnyEnemyInZone(bombTile, radius, out int enemyId);
        bool allyInZone = AnyAllyInZone(bombTile, radius);

        // 1. Detonação ofensiva: inimigo na zona, ninguém do nosso lado.
        if (enemyInZone && !selfInZone && !allyInZone)
        {
            if (Time.time < nextDetonateTime)
            {
                lastDecisionTrace = $"candidate detonate cooldown {(nextDetonateTime - Time.time):F2}s";
                return false;
            }

            nextDetonateTime = Time.time + DetonateCooldownSeconds;

            decision = new BattleModeComAbilityDecision
            {
                Action = BattleModeComActionType.CombatPlant,
                Weight = 4000, // acerto garantido não pode perder o sorteio
                TargetTile = bombTile,
                HasTarget = true,
                FirstMove = Vector2.zero,
                Reason = $"control-detonate hits P{enemyId}",
                InputDescription = "ActionB",
                TapActionB = true
            };

            lastDecisionTrace = $"candidate DETONATE oldest:{bombTile} hits P{enemyId}";
            LogSurgical("DETONATE",
                $"my:{myTile} bomb:{bombTile} r:{radius} enemy:P{enemyId}", force: true);
            return true;
        }

        // 2. Recuo pré-detonação: inimigo na zona mas a IA também — sai primeiro.
        if (enemyInZone && selfInZone)
        {
            UpdateRetreatStuckDetection(myTile);

            if (!TryFindTileOutsideZone(settings, myTile, bombTile, radius, retreatBlockedSteps,
                    out Vector2 firstMove, out Vector2Int target, out int depth))
            {
                lastDecisionTrace = "candidate enemy in zone but no retreat route";
                LogSurgical("RETREAT_NO_ROUTE", $"my:{myTile} bomb:{bombTile} r:{radius}", force: true);
                return false;
            }

            retreatLastAttemptedStep = Vector2Int.RoundToInt(firstMove);

            decision = new BattleModeComAbilityDecision
            {
                Action = BattleModeComActionType.Reposition,
                Weight = 600 + DifficultyWeight(settings),
                TargetTile = target,
                HasTarget = true,
                FirstMove = firstMove,
                Reason = "control-retreat before detonate",
                InputDescription = FirstMoveDescription(firstMove)
            };

            lastDecisionTrace = $"candidate retreat target:{target} depth:{depth} (enemy P{enemyId} in zone)";
            LogSurgical("RETREAT",
                $"my:{myTile} bomb:{bombTile} target:{target} move:{FirstMoveDescription(firstMove)} enemy:P{enemyId}");
            return true;
        }

        // 3. Detonação de farm: se o blast da bomba vai destruir blocos destrutíveis,
        // não há motivo para esperar — detona assim que o nosso lado estiver seguro.
        if (!enemyInZone && !selfInZone && !allyInZone &&
            CountDestructiblesHitByBlast(bombTile, radius) > 0)
        {
            if (Time.time < nextDetonateTime)
            {
                lastDecisionTrace = $"candidate farm cooldown {(nextDetonateTime - Time.time):F2}s";
                return false;
            }

            nextDetonateTime = Time.time + DetonateCooldownSeconds;

            decision = new BattleModeComAbilityDecision
            {
                Action = BattleModeComActionType.CombatPlant,
                Weight = 350 + DifficultyWeight(settings),
                TargetTile = bombTile,
                HasTarget = true,
                FirstMove = Vector2.zero,
                Reason = "control-detonate farm",
                InputDescription = "ActionB",
                TapActionB = true
            };

            lastDecisionTrace = $"candidate FARM detonate oldest:{bombTile}";
            LogSurgical("FARM_DETONATE", $"my:{myTile} bomb:{bombTile} r:{radius}", force: true);
            return true;
        }

        // 4. Detonação por tempo: bomba antiga sem alvo há StaleDetonateSeconds e
        // segura para o nosso lado → detona para reciclar o slot e destravar a IA
        // (a lógica nativa fica esperando a "explosão" de uma bomba que nunca
        // explode sozinha).
        float oldestAge = Time.time - trackedOldestSince;
        if (!enemyInZone && !selfInZone && !allyInZone && oldestAge >= StaleDetonateSeconds)
        {
            if (Time.time < nextDetonateTime)
            {
                lastDecisionTrace = $"candidate stale cooldown {(nextDetonateTime - Time.time):F2}s";
                return false;
            }

            nextDetonateTime = Time.time + DetonateCooldownSeconds;

            decision = new BattleModeComAbilityDecision
            {
                Action = BattleModeComActionType.CombatPlant,
                Weight = 350 + DifficultyWeight(settings),
                TargetTile = bombTile,
                HasTarget = true,
                FirstMove = Vector2.zero,
                Reason = "control-detonate stale bomb",
                InputDescription = "ActionB",
                TapActionB = true
            };

            lastDecisionTrace = $"candidate STALE detonate oldest:{bombTile} age:{oldestAge:F1}s";
            LogSurgical("STALE_DETONATE", $"my:{myTile} bomb:{bombTile} age:{oldestAge:F1}s", force: true);
            return true;
        }

        // 5. Liberação de slot: sem bombas restantes, bomba antiga inútil e segura.
        if (bombController.BombsRemaining <= 0 && !selfInZone && !allyInZone)
        {
            if (Time.time < nextFreeSlotTime)
            {
                lastDecisionTrace = $"candidate free-slot cooldown {(nextFreeSlotTime - Time.time):F2}s";
                return false;
            }

            nextFreeSlotTime = Time.time + FreeSlotCooldownSeconds;
            nextDetonateTime = Time.time + DetonateCooldownSeconds;

            decision = new BattleModeComAbilityDecision
            {
                Action = BattleModeComActionType.CombatPlant,
                Weight = 200 + DifficultyWeight(settings),
                TargetTile = bombTile,
                HasTarget = true,
                FirstMove = Vector2.zero,
                Reason = "control-detonate free slot",
                InputDescription = "ActionB",
                TapActionB = true
            };

            lastDecisionTrace = $"candidate FREE_SLOT detonate oldest:{bombTile}";
            LogSurgical("FREE_SLOT", $"my:{myTile} bomb:{bombTile} r:{radius}");
            return true;
        }

        lastDecisionTrace =
            $"candidate hold oldest:{bombTile} self:{selfInZone} enemy:{enemyInZone} ally:{allyInZone} " +
            $"bombsLeft:{bombController.BombsRemaining} age:{oldestAge:F1}s";
        LogSurgical("HOLD",
            $"my:{myTile} bomb:{bombTile} r:{radius} self:{selfInZone} enemy:{enemyInZone} " +
            $"ally:{allyInZone} bombsLeft:{bombController.BombsRemaining} age:{oldestAge:F1}s");
        return false;
    }

    // =====================================================================
    // Jogadores nas zonas
    // =====================================================================
    private bool AnyEnemyInZone(Vector2Int bombTile, int radius, out int enemyId)
    {
        enemyId = 0;
        activePlayers.Clear();
        PlayerIdentity.GetActivePlayers(activePlayers);

        for (int i = 0; i < activePlayers.Count; i++)
        {
            PlayerIdentity player = activePlayers[i];
            if (player == null || player == identity)
                continue;

            if (!player.TryGetComponent<MovementController>(out var enemyMovement) ||
                enemyMovement == null || enemyMovement.isDead)
                continue;

            if (IsAlly(player.playerId))
                continue;

            if (IsTileInBlastLine(bombTile, WorldToTile(player.transform.position), radius))
            {
                enemyId = player.playerId;
                return true;
            }
        }

        return false;
    }

    private bool AnyAllyInZone(Vector2Int bombTile, int radius)
    {
        activePlayers.Clear();
        PlayerIdentity.GetActivePlayers(activePlayers);

        for (int i = 0; i < activePlayers.Count; i++)
        {
            PlayerIdentity player = activePlayers[i];
            if (player == null || player == identity)
                continue;

            if (!IsAlly(player.playerId))
                continue;

            if (!player.TryGetComponent<MovementController>(out var allyMovement) ||
                allyMovement == null || allyMovement.isDead)
                continue;

            if (IsTileInBlastLine(bombTile, WorldToTile(player.transform.position), radius))
                return true;
        }

        return false;
    }

    private bool IsAlly(int otherPlayerId)
    {
        if (BattleModeRules.Instance == null || !BattleModeRules.Instance.UsesTeams || identity == null)
            return false;

        return BattleModeRules.Instance.GetTeamForPlayer(identity.playerId) ==
               BattleModeRules.Instance.GetTeamForPlayer(otherPlayerId);
    }

    // =====================================================================
    // BFS para fora da zona da própria bomba
    // =====================================================================
    private bool TryFindTileOutsideZone(
        BattleModeComDifficultySettings settings,
        Vector2Int start,
        Vector2Int bombTile,
        int radius,
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

        while (searchOpen.Count > 0)
        {
            Vector2Int tile = searchOpen.Dequeue();
            SearchNode node = searchVisited[tile];
            float eta = EstimateTraversalSeconds(node.Depth);

            if (node.Depth > 0 &&
                !IsTileInBlastLine(bombTile, tile, radius) &&
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

        return false;
    }

    // =====================================================================
    // Perigo / blast line
    // =====================================================================
    private float GetDangerSeconds(Vector2Int tile)
    {
        float danger = float.PositiveInfinity;
        foreach (Bomb bomb in Bomb.ActiveBombs)
        {
            if (bomb == null || bomb.HasExploded || bomb.IsBeingHeldByPowerGlove)
                continue;

            // As próprias control bombs não explodem sozinhas.
            if (bomb.IsControlBomb && bomb.Owner == bombController)
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

    /// <summary>
    /// Conta blocos destrutíveis que o blast da bomba destruiria (primeiro blocker
    /// de cada direção, se for destrutível). > 0 significa que a bomba tem valor
    /// de farm e deve ser detonada assim que estiver segura.
    /// </summary>
    private int CountDestructiblesHitByBlast(Vector2Int bombTile, int radius)
    {
        int count = 0;
        for (int d = 0; d < CardinalTiles.Length; d++)
        {
            for (int step = 1; step <= radius; step++)
            {
                Vector2Int tile = bombTile + CardinalTiles[d] * step;

                if (HasIndestructibleTile(tile) || FindBombAt(tile) != null)
                    break;

                if (HasDestructibleTile(tile))
                {
                    count++;
                    break; // explosão normal para no primeiro destrutível
                }
            }
        }

        return count;
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
    // Walkability
    // =====================================================================
    private bool IsWalkableTile(Vector2Int tile, Vector2Int startTile)
    {
        if (!HasGroundTile(tile))
            return false;

        if (HasIndestructibleTile(tile) || HasDestructibleTile(tile))
            return false;

        if (FindBombAt(tile) != null && tile != startTile)
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

                if (tile == startTile && hit.GetComponentInParent<Bomb>() != null)
                    continue;

                return false;
            }
        }

        return true;
    }

    // =====================================================================
    // Detecção de travamento no recuo
    // =====================================================================
    private void UpdateRetreatStuckDetection(Vector2Int myTile)
    {
        if (myTile == retreatLastTile)
        {
            if (retreatStuckSince < 0f)
            {
                retreatStuckSince = Time.time;
            }
            else if (Time.time - retreatStuckSince > RetreatStuckSeconds &&
                     retreatLastAttemptedStep != Vector2Int.zero &&
                     !retreatBlockedSteps.Contains(retreatLastAttemptedStep))
            {
                retreatBlockedSteps.Add(retreatLastAttemptedStep);
                retreatStuckSince = -1f;
                LogSurgical("RETREAT_STUCK",
                    $"my:{myTile} blocking:{retreatLastAttemptedStep} total:{retreatBlockedSteps.Count}",
                    force: true);
            }
        }
        else
        {
            retreatLastTile = myTile;
            ClearRetreatStuckState();
        }
    }

    private void ClearRetreatStuckState()
    {
        retreatStuckSince = -1f;
        retreatBlockedSteps.Clear();
        retreatLastAttemptedStep = Vector2Int.zero;
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
        if (!EnableControlBombDiagnostics) return;

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

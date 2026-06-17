using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Ability de IA para DestructiblePass: torna a IA ciente de que pode ATRAVESSAR
/// blocos destrutíveis.
///
/// COMPORTAMENTOS:
///   1. EMERGENCY — fuga através de destrutíveis. O BFS nativo do controller trata
///      blocos destrutíveis como paredes; com DestructiblePass eles são atravessáveis.
///      Quando a fuga normal seria impossível (encurralamento), esta ability fornece
///      uma rota de fuga que passa por dentro dos blocos.
///   2. CANDIDATE / DESENCURRALAMENTO — sem perigo imediato mas preso num beco,
///      atravessa destrutíveis para área aberta.
///   3. CANDIDATE / FARM — atravessa destrutíveis para alcançar um ponto de plantio
///      cercado por mais blocos (farm mais eficiente).
///   4. CANDIDATE / PERSEGUIÇÃO — atravessa destrutíveis para invadir o território
///      de um adversário e plantar bomba na linha de blast dele.
///
/// SINERGIA:
///   Se a IA também possuir BombPassAbility, o BFS desta ability considera bombas
///   atravessáveis (com margem de fuse), combinando as duas habilidades.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerIdentity))]
[RequireComponent(typeof(MovementController))]
[RequireComponent(typeof(BombController))]
public sealed class BattleModeComDestructiblePassAbility : MonoBehaviour, IBattleModeComAbility
{
    // === Filtro de diagnóstico ===
    public const int DiagnosticPlayerIdFilter = 0; // 0 = todos
    public static readonly bool EnableDestructiblePassDiagnostics = false;
    private static bool EnableSurgicalDiagnostics => EnableDestructiblePassDiagnostics;
    private const float SurgicalLogIntervalSeconds = 0.25f;

    // === Constantes de comportamento ===
    private const float OffensiveCooldownSeconds = 3.0f;
    private const float BombTileExtraMarginSeconds = 0.40f;
    private const float ApproachTimeoutSeconds = 7.0f;
    private const float PostPlantEscapeSeconds = 2.2f;
    // Distância máxima (Manhattan) do inimigo para iniciar perseguição.
    private const int MaxChaseEnemyDistance = 12;
    // Mínimo de destrutíveis adjacentes para um plant tile de farm valer a pena.
    private const int MinFarmAdjacentDestructibles = 2;
    private const float EscapeStuckSeconds = 0.25f;

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

    // === Máquina de estados ===
    private enum SequenceState
    {
        None,
        ApproachingPlantTile,
        EscapingAfterPlant
    }

    private SequenceState sequenceState;
    private float sequenceStateStartedTime = -10f;
    private Vector2Int plantTargetTile;
    private string plantPlanLabel = "none"; // "farm" ou "chase" (diagnóstico)
    private float postPlantEscapeUntil = -10f;
    private float nextOffensiveTime = -10f;

    // === Detecção de travamento na fuga ===
    private Vector2Int escapeLastTile;
    private float escapeStuckSince = -10f;
    private Vector2Int escapeLastAttemptedStep;
    private readonly List<Vector2Int> escapeBlockedSteps = new List<Vector2Int>(4);

    // === Cache de chance ofensiva ===
    private float offensiveTriggerChanceCacheTime = -10f;
    private bool offensiveTriggerChanceCacheResult;

    // === BFS reutilizável ===
    private struct SearchNode
    {
        public Vector2Int Parent;
        public int Depth;
        public bool UsedDestructibleTile;
    }

    private readonly Dictionary<Vector2Int, SearchNode> searchVisited =
        new Dictionary<Vector2Int, SearchNode>(96);
    private readonly Queue<Vector2Int> searchOpen = new Queue<Vector2Int>(96);
    private readonly List<Vector2Int> plannedBlastTiles = new List<Vector2Int>(32);

    // === Contadores de rejeição do último BFS (diagnóstico) ===
    private int bfsRejectedWalkability;
    private int bfsRejectedDanger;
    private int bfsDestructibleTilesEntered;

    // === Diagnóstico ===
    private string lastDecisionTrace = "not evaluated";
    private float lastSurgicalLogTime = -10f;
    private string lastSurgicalLogKey = string.Empty;

    // === IBattleModeComAbility ===
    public string DiagnosticName => "DestructiblePass";
    public string LastDecisionTrace => lastDecisionTrace;

    public bool IsAvailable
    {
        get
        {
            CacheReferences();
            if (identity == null || movement == null || movement.isDead)
                return false;

            return abilitySystem != null &&
                   abilitySystem.IsEnabled(DestructiblePassAbility.AbilityId);
        }
    }

    private bool CanAlsoPassBombs =>
        abilitySystem != null && abilitySystem.IsEnabled(BombPassAbility.AbilityId);

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
    // TryBuildEmergencyDecision — fuga através de destrutíveis
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
            lastDecisionTrace = "emergency unavailable " + BuildAvailabilityTrace();
            LogSurgical("EMERGENCY_UNAVAILABLE", BuildAvailabilityTrace());
            if (sequenceState != SequenceState.None)
                ResetSequence("ability disabled mid-sequence");
            return false;
        }

        if (sequenceState == SequenceState.EscapingAfterPlant)
        {
            if (TryBuildPostPlantEscapeDecision(settings, myTile, out decision))
            {
                lastDecisionTrace = "emergency continue post-plant escape -> " + lastDecisionTrace;
                return true;
            }

            return false;
        }

        if (sequenceState == SequenceState.ApproachingPlantTile)
            ResetSequence("danger during approach");

        UpdateEscapeStuckDetection(myTile);

        LogSurgical("EMERGENCY_EVAL",
            $"danger:{FormatDanger(currentDangerSeconds)} scan:{BuildNeighborScanSummary(myTile)} " +
            $"blockedSteps:{escapeBlockedSteps.Count}");

        if (!TryFindSafeTileThroughDestructibles(
                settings, myTile, escapeBlockedSteps, null,
                out Vector2 firstMove, out Vector2Int target,
                out bool usedDestructible, out int depth))
        {
            lastDecisionTrace = "emergency no route through destructibles";
            LogSurgical("EMERGENCY_NO_ROUTE",
                $"danger:{FormatDanger(currentDangerSeconds)} scan:{BuildNeighborScanSummary(myTile)}",
                force: true);
            return false;
        }

        // Se a rota não atravessa nenhum destrutível, a fuga nativa resolve igual.
        if (!usedDestructible)
        {
            lastDecisionTrace = $"emergency route does not need destructible pass (target:{target})";
            LogSurgical("EMERGENCY_DEFER_NATIVE",
                $"target:{target} depth:{depth} danger:{FormatDanger(currentDangerSeconds)}");
            return false;
        }

        escapeLastAttemptedStep = Vector2Int.RoundToInt(firstMove);

        decision = new BattleModeComAbilityDecision
        {
            Action = BattleModeComActionType.Reposition,
            Weight = 300 + DifficultyWeight(settings),
            TargetTile = target,
            HasTarget = true,
            FirstMove = firstMove,
            Reason = "destructible-pass escape",
            InputDescription = FirstMoveDescription(firstMove)
        };

        lastDecisionTrace = $"emergency destructible-pass escape target:{target} depth:{depth}";
        LogSurgical("EMERGENCY_DESTPASS_ESCAPE",
            $"my:{myTile} target:{target} move:{FirstMoveDescription(firstMove)} depth:{depth} " +
            $"danger:{FormatDanger(currentDangerSeconds)} (NOVA ROTA ATRAVES DE DESTRUTIVEL)",
            force: true);
        return true;
    }

    // =====================================================================
    // TryBuildCandidateDecision — desencurralamento + farm + perseguição
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
            if (sequenceState != SequenceState.None)
                ResetSequence("ability disabled mid-sequence");
            return false;
        }

        if (sequenceState == SequenceState.EscapingAfterPlant)
            return TryBuildPostPlantEscapeDecision(settings, myTile, out decision);

        if (sequenceState == SequenceState.ApproachingPlantTile)
            return TryContinueApproach(settings, myTile, out decision);

        if (TryBuildUncornerDecision(settings, myTile, out decision))
            return true;

        return TryStartOffensivePlan(settings, myTile, out decision);
    }

    // =====================================================================
    // Desencurralamento
    // =====================================================================
    private bool TryBuildUncornerDecision(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        out BattleModeComAbilityDecision decision)
    {
        decision = default;

        // Conta saídas normais (sem atravessar destrutíveis).
        int openWithoutDestructibles = 0;
        bool hasAdjacentDestructible = false;
        for (int i = 0; i < CardinalTiles.Length; i++)
        {
            Vector2Int next = myTile + CardinalTiles[i];
            if (HasDestructibleTile(next))
            {
                if (IsWalkableTileThroughDestructibles(next, myTile))
                    hasAdjacentDestructible = true;
                continue;
            }

            if (IsWalkableTileThroughDestructibles(next, myTile))
                openWithoutDestructibles++;
        }

        if (!hasAdjacentDestructible || openWithoutDestructibles > 1)
        {
            lastDecisionTrace =
                $"candidate not cornered open:{openWithoutDestructibles} adjDest:{hasAdjacentDestructible}";
            return false;
        }

        UpdateEscapeStuckDetection(myTile);

        if (!TryFindSafeTileThroughDestructibles(
                settings, myTile, escapeBlockedSteps, null,
                out Vector2 firstMove, out Vector2Int target,
                out bool usedDestructible, out int depth))
        {
            lastDecisionTrace = "candidate cornered but no route through destructibles";
            return false;
        }

        if (!usedDestructible)
        {
            lastDecisionTrace = "candidate cornered but normal route exists";
            return false;
        }

        escapeLastAttemptedStep = Vector2Int.RoundToInt(firstMove);

        decision = new BattleModeComAbilityDecision
        {
            Action = BattleModeComActionType.Reposition,
            Weight = settings.patrolWeight + 70 + DifficultyWeight(settings),
            TargetTile = target,
            HasTarget = true,
            FirstMove = firstMove,
            Reason = "destructible-pass uncorner",
            InputDescription = FirstMoveDescription(firstMove)
        };

        lastDecisionTrace = $"candidate uncorner target:{target} depth:{depth}";
        LogSurgical("UNCORNER",
            $"my:{myTile} target:{target} move:{FirstMoveDescription(firstMove)} open:{openWithoutDestructibles}");
        return true;
    }

    // =====================================================================
    // Plano ofensivo: perseguição (prioridade) ou farm
    // =====================================================================
    private bool TryStartOffensivePlan(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        out BattleModeComAbilityDecision decision)
    {
        decision = default;

        if (bombController == null || bombController.BombsRemaining <= 0)
        {
            lastDecisionTrace = "candidate no bombs remaining";
            return false;
        }

        if (Time.time < nextOffensiveTime)
        {
            lastDecisionTrace = $"candidate cooldown {(nextOffensiveTime - Time.time):F2}s";
            return false;
        }

        if (!RollOffensiveChance(settings))
            return false;

        // 1. Perseguição: plant tile na linha de blast de um inimigo, atravessando blocos.
        bool found = false;
        string label = "none";
        Vector2Int plantTile = myTile;
        Vector2 firstMove = Vector2.zero;
        bool usedDestructible = false;
        int depth = 0;

        if (TryFindChasePlantTile(settings, myTile,
                out plantTile, out firstMove, out usedDestructible, out depth, out Vector2Int enemyTile))
        {
            found = true;
            label = "chase";
        }
        // 2. Farm: plant tile cercado de destrutíveis, alcançável atravessando blocos.
        else if (TryFindFarmPlantTile(settings, myTile,
                out plantTile, out firstMove, out usedDestructible, out depth, out int adjacentCount))
        {
            found = true;
            label = "farm";
        }

        if (!found)
        {
            lastDecisionTrace = "candidate no offensive plant tile (chase/farm)";
            return false;
        }

        // Só interessa se a rota realmente atravessa destrutíveis — caso contrário
        // o fluxo nativo (CombatPlant / FarmDestructible) já cobre o plano.
        if (!usedDestructible)
        {
            lastDecisionTrace = $"candidate {label} route does not need destructible pass";
            return false;
        }

        plantTargetTile = plantTile;
        plantPlanLabel = label;
        SetSequenceState(SequenceState.ApproachingPlantTile);
        nextOffensiveTime = Time.time + OffensiveCooldownSeconds;

        if (myTile == plantTile)
            return TryPlantNow(settings, myTile, out decision);

        decision = new BattleModeComAbilityDecision
        {
            Action = BattleModeComActionType.CombatPlant,
            Weight = settings.combatPlantWeight + 90 + DifficultyWeight(settings),
            TargetTile = plantTile,
            HasTarget = true,
            FirstMove = firstMove,
            Reason = $"destructible-pass {label} approach",
            InputDescription = FirstMoveDescription(firstMove)
        };

        lastDecisionTrace = $"candidate {label} approach plant:{plantTile} depth:{depth}";
        LogSurgical("PLAN",
            $"my:{myTile} plan:{label} plant:{plantTile} depth:{depth} move:{FirstMoveDescription(firstMove)}");
        return true;
    }

    private bool TryContinueApproach(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        out BattleModeComAbilityDecision decision)
    {
        decision = default;

        if (Time.time - sequenceStateStartedTime > ApproachTimeoutSeconds)
        {
            ResetSequence("approach timeout");
            lastDecisionTrace = "approach timeout";
            return false;
        }

        if (bombController == null || bombController.BombsRemaining <= 0)
        {
            ResetSequence("no bombs remaining during approach");
            lastDecisionTrace = "approach no bombs";
            return false;
        }

        if (myTile == plantTargetTile)
            return TryPlantNow(settings, myTile, out decision);

        if (!TryFindPathThroughDestructibles(settings, myTile, plantTargetTile,
                out Vector2 firstMove, out int depth))
        {
            ResetSequence("approach path lost");
            lastDecisionTrace = "approach path lost";
            return false;
        }

        decision = new BattleModeComAbilityDecision
        {
            Action = BattleModeComActionType.CombatPlant,
            Weight = 240 + DifficultyWeight(settings),
            TargetTile = plantTargetTile,
            HasTarget = true,
            FirstMove = firstMove,
            Reason = $"destructible-pass {plantPlanLabel} approach",
            InputDescription = FirstMoveDescription(firstMove)
        };

        lastDecisionTrace = $"approach continue plant:{plantTargetTile} depth:{depth}";
        LogSurgical("APPROACH", $"my:{myTile} plan:{plantPlanLabel} plant:{plantTargetTile} depth:{depth}");
        return true;
    }

    private bool TryPlantNow(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        out BattleModeComAbilityDecision decision)
    {
        decision = default;

        BuildPlannedBlastTiles(myTile);
        if (!TryFindSafeTileThroughDestructibles(
                settings, myTile, null, plannedBlastTiles,
                out Vector2 escapeMove, out Vector2Int escapeTarget,
                out _, out _))
        {
            ResetSequence("no escape after planned plant");
            lastDecisionTrace = "plant aborted: no escape route";
            LogSurgical("PLANT_ABORT", $"my:{myTile} no escape", force: true);
            return false;
        }

        SetSequenceState(SequenceState.EscapingAfterPlant);
        postPlantEscapeUntil = Time.time + PostPlantEscapeSeconds;
        ClearEscapeStuckState();

        decision = new BattleModeComAbilityDecision
        {
            Action = BattleModeComActionType.CombatPlant,
            Weight = 260 + DifficultyWeight(settings),
            TargetTile = escapeTarget,
            HasTarget = true,
            FirstMove = escapeMove,
            Reason = "destructible-pass plant",
            InputDescription = AppendInput(FirstMoveDescription(escapeMove), "ActionA"),
            TapBomb = true
        };

        lastDecisionTrace = $"plant now ({plantPlanLabel}) escape:{escapeTarget}";
        LogSurgical("PLANT", $"my:{myTile} plan:{plantPlanLabel} escape:{escapeTarget}", force: true);
        return true;
    }

    // =====================================================================
    // Fuga pós-plant
    // =====================================================================
    private bool TryBuildPostPlantEscapeDecision(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        out BattleModeComAbilityDecision decision)
    {
        decision = default;

        float dangerHere = GetDangerSeconds(myTile);
        if (float.IsInfinity(dangerHere))
        {
            LogSurgical("ESCAPE_DONE", $"my:{myTile}");
            ResetSequence("escape complete");
            lastDecisionTrace = "post-plant escape complete";
            return false;
        }

        if (Time.time > postPlantEscapeUntil)
        {
            LogSurgical("ESCAPE_EXPIRED",
                $"my:{myTile} dangerHere:{FormatDanger(dangerHere)}", force: true);
            ResetSequence("escape expired");
            lastDecisionTrace = "post-plant escape expired";
            return false;
        }

        UpdateEscapeStuckDetection(myTile);

        if (!TryFindSafeTileThroughDestructibles(
                settings, myTile, escapeBlockedSteps, null,
                out Vector2 firstMove, out Vector2Int target, out _, out int depth))
        {
            LogSurgical("ESCAPE_FAILED",
                $"my:{myTile} dangerHere:{FormatDanger(dangerHere)}", force: true);
            ResetSequence("escape failed");
            lastDecisionTrace = "post-plant escape failed";
            return false;
        }

        escapeLastAttemptedStep = Vector2Int.RoundToInt(firstMove);

        decision = new BattleModeComAbilityDecision
        {
            Action = BattleModeComActionType.Reposition,
            Weight = 280 + DifficultyWeight(settings),
            TargetTile = target,
            HasTarget = true,
            FirstMove = firstMove,
            Reason = "destructible-pass escape after plant",
            InputDescription = FirstMoveDescription(firstMove)
        };

        lastDecisionTrace = $"post-plant escape target:{target} depth:{depth}";
        LogSurgical("ESCAPE",
            $"my:{myTile} target:{target} move:{FirstMoveDescription(firstMove)} dangerHere:{FormatDanger(dangerHere)}");
        return true;
    }

    // =====================================================================
    // BFS através de destrutíveis
    // =====================================================================
    private bool TryFindSafeTileThroughDestructibles(
        BattleModeComDifficultySettings settings,
        Vector2Int start,
        List<Vector2Int> blockedFirstSteps,
        List<Vector2Int> extraBlastTiles,
        out Vector2 firstMove,
        out Vector2Int target,
        out bool usedDestructible,
        out int resultDepth)
    {
        firstMove = Vector2.zero;
        target = start;
        usedDestructible = false;
        resultDepth = 0;

        ResetBfsCounters();
        searchVisited.Clear();
        searchOpen.Clear();
        searchVisited[start] = new SearchNode { Parent = start, Depth = 0, UsedDestructibleTile = false };

        if (blockedFirstSteps != null)
        {
            for (int i = 0; i < blockedFirstSteps.Count; i++)
            {
                if (blockedFirstSteps[i] != Vector2Int.zero)
                    searchVisited[start + blockedFirstSteps[i]] =
                        new SearchNode { Parent = start, Depth = 0, UsedDestructibleTile = false };
            }
        }

        searchOpen.Enqueue(start);
        int maxDepth = Mathf.Max(4, settings.searchDepth + 4);

        while (searchOpen.Count > 0)
        {
            Vector2Int tile = searchOpen.Dequeue();
            SearchNode node = searchVisited[tile];
            float eta = EstimateTraversalSeconds(node.Depth);

            // Tile seguro precisa ser livre (não dentro de um destrutível nem de bomba):
            // parar DENTRO de um bloco/bomba não é um destino de fuga válido.
            bool tileIsFree = !HasDestructibleTile(tile) && FindBombAt(tile) == null;
            if (node.Depth > 0 &&
                tileIsFree &&
                float.IsInfinity(GetEffectiveDangerSeconds(tile, extraBlastTiles)) &&
                !IsDangerousAt(tile, eta + settings.safeTileMinimumSeconds, settings, extraBlastTiles))
            {
                target = tile;
                usedDestructible = node.UsedDestructibleTile;
                resultDepth = node.Depth;
                firstMove = TileDirectionToVector(ReconstructFirstStep(start, tile));
                return firstMove != Vector2.zero;
            }

            if (node.Depth >= maxDepth)
                continue;

            ExpandThroughDestructibles(tile, node, start, settings, extraBlastTiles);
        }

        return false;
    }

    private bool TryFindPathThroughDestructibles(
        BattleModeComDifficultySettings settings,
        Vector2Int start,
        Vector2Int goal,
        out Vector2 firstMove,
        out int resultDepth)
    {
        firstMove = Vector2.zero;
        resultDepth = 0;

        ResetBfsCounters();
        searchVisited.Clear();
        searchOpen.Clear();
        searchVisited[start] = new SearchNode { Parent = start, Depth = 0, UsedDestructibleTile = false };
        searchOpen.Enqueue(start);

        int maxDepth = Mathf.Max(4, settings.searchDepth + 6);

        while (searchOpen.Count > 0)
        {
            Vector2Int tile = searchOpen.Dequeue();
            SearchNode node = searchVisited[tile];

            if (tile == goal)
            {
                resultDepth = node.Depth;
                firstMove = TileDirectionToVector(ReconstructFirstStep(start, tile));
                return firstMove != Vector2.zero;
            }

            if (node.Depth >= maxDepth)
                continue;

            ExpandThroughDestructibles(tile, node, start, settings, null);
        }

        return false;
    }

    private void ExpandThroughDestructibles(
        Vector2Int tile,
        SearchNode node,
        Vector2Int start,
        BattleModeComDifficultySettings settings,
        List<Vector2Int> extraBlastTiles)
    {
        for (int i = 0; i < CardinalTiles.Length; i++)
        {
            Vector2Int next = tile + CardinalTiles[i];
            if (searchVisited.ContainsKey(next))
                continue;

            if (!IsWalkableTileThroughDestructibles(next, start))
            {
                bfsRejectedWalkability++;
                continue;
            }

            float eta = EstimateTraversalSeconds(node.Depth + 1);
            bool nextHasBomb = FindBombAt(next) != null;
            bool nextHasDestructible = HasDestructibleTile(next);

            // Tiles com bomba exigem margem extra de fuse (apenas com BombPass).
            float requiredMargin = nextHasBomb ? BombTileExtraMarginSeconds : 0f;
            if (IsDangerousAt(next, eta + requiredMargin, settings, extraBlastTiles))
            {
                bfsRejectedDanger++;
                continue;
            }

            if (nextHasDestructible)
                bfsDestructibleTilesEntered++;

            searchVisited[next] = new SearchNode
            {
                Parent = tile,
                Depth = node.Depth + 1,
                UsedDestructibleTile = node.UsedDestructibleTile || nextHasDestructible
            };
            searchOpen.Enqueue(next);
        }
    }

    // =====================================================================
    // Alvos ofensivos
    // =====================================================================

    /// <summary>
    /// Perseguição: BFS através de destrutíveis até um tile livre na linha de blast
    /// do inimigo mais próximo (com fuga pós-plant viável).
    /// </summary>
    private bool TryFindChasePlantTile(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        out Vector2Int plantTile,
        out Vector2 firstMove,
        out bool usedDestructible,
        out int resultDepth,
        out Vector2Int enemyTile)
    {
        plantTile = myTile;
        firstMove = Vector2.zero;
        usedDestructible = false;
        resultDepth = 0;
        enemyTile = myTile;

        if (!TryFindClosestEnemyTile(myTile, out enemyTile))
            return false;

        if (Manhattan(myTile, enemyTile) > MaxChaseEnemyDistance)
            return false;

        int radius = bombController != null ? Mathf.Max(1, bombController.GetPlannedExplosionRadius()) : 2;

        ResetBfsCounters();
        searchVisited.Clear();
        searchOpen.Clear();
        searchVisited[myTile] = new SearchNode { Parent = myTile, Depth = 0, UsedDestructibleTile = false };
        searchOpen.Enqueue(myTile);

        int maxDepth = Mathf.Max(4, settings.searchDepth + 4);

        while (searchOpen.Count > 0)
        {
            Vector2Int tile = searchOpen.Dequeue();
            SearchNode node = searchVisited[tile];

            bool tileIsFree = !HasDestructibleTile(tile) && FindBombAt(tile) == null;
            if (tileIsFree &&
                tile != enemyTile &&
                IsTileInBlastLine(tile, enemyTile, radius) &&
                HasEscapeAfterPlantAt(settings, tile))
            {
                plantTile = tile;
                usedDestructible = node.UsedDestructibleTile;
                resultDepth = node.Depth;
                firstMove = node.Depth == 0
                    ? Vector2.zero
                    : TileDirectionToVector(ReconstructFirstStep(myTile, tile));
                return node.Depth == 0 || firstMove != Vector2.zero;
            }

            if (node.Depth >= maxDepth)
                continue;

            ExpandThroughDestructibles(tile, node, myTile, settings, null);
        }

        return false;
    }

    /// <summary>
    /// Farm: BFS através de destrutíveis até um tile livre com pelo menos
    /// MinFarmAdjacentDestructibles blocos adjacentes (mais blocos = melhor).
    /// Retorna o melhor candidato dentro do alcance (mais blocos; empate = mais perto).
    /// </summary>
    private bool TryFindFarmPlantTile(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        out Vector2Int plantTile,
        out Vector2 firstMove,
        out bool usedDestructible,
        out int resultDepth,
        out int adjacentCount)
    {
        plantTile = myTile;
        firstMove = Vector2.zero;
        usedDestructible = false;
        resultDepth = 0;
        adjacentCount = 0;

        ResetBfsCounters();
        searchVisited.Clear();
        searchOpen.Clear();
        searchVisited[myTile] = new SearchNode { Parent = myTile, Depth = 0, UsedDestructibleTile = false };
        searchOpen.Enqueue(myTile);

        int maxDepth = Mathf.Max(4, settings.searchDepth + 2);
        bool found = false;
        int bestScore = int.MinValue;

        while (searchOpen.Count > 0)
        {
            Vector2Int tile = searchOpen.Dequeue();
            SearchNode node = searchVisited[tile];

            bool tileIsFree = !HasDestructibleTile(tile) && FindBombAt(tile) == null;
            if (node.Depth > 0 && tileIsFree)
            {
                int adjacent = CountAdjacentDestructibles(tile);
                if (adjacent >= MinFarmAdjacentDestructibles &&
                    HasEscapeAfterPlantAt(settings, tile))
                {
                    // Score: prioriza mais blocos adjacentes, depois menor distância.
                    int score = adjacent * 100 - node.Depth;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        plantTile = tile;
                        usedDestructible = node.UsedDestructibleTile;
                        resultDepth = node.Depth;
                        adjacentCount = adjacent;
                        found = true;
                    }
                }
            }

            if (node.Depth >= maxDepth)
                continue;

            ExpandThroughDestructibles(tile, node, myTile, settings, null);
        }

        if (!found)
            return false;

        firstMove = TileDirectionToVector(ReconstructFirstStep(myTile, plantTile));
        return firstMove != Vector2.zero;
    }

    private int CountAdjacentDestructibles(Vector2Int tile)
    {
        int count = 0;
        for (int i = 0; i < CardinalTiles.Length; i++)
        {
            if (HasDestructibleTile(tile + CardinalTiles[i]))
                count++;
        }

        return count;
    }

    private bool HasEscapeAfterPlantAt(BattleModeComDifficultySettings settings, Vector2Int tile)
    {
        BuildPlannedBlastTiles(tile);

        for (int i = 0; i < CardinalTiles.Length; i++)
        {
            Vector2Int next = tile + CardinalTiles[i];
            if (!IsWalkableTileThroughDestructibles(next, tile))
                continue;

            // Dentro de destrutível não é refúgio: o blast da própria bomba o destrói
            // e o tile pode estar na linha de explosão.
            if (HasDestructibleTile(next))
                continue;

            if (plannedBlastTiles.Contains(next))
                continue;

            if (IsDangerousAt(next, EstimateTraversalSeconds(1), settings, null))
                continue;

            return true;
        }

        return false;
    }

    private void BuildPlannedBlastTiles(Vector2Int bombTile)
    {
        plannedBlastTiles.Clear();
        plannedBlastTiles.Add(bombTile);

        int radius = bombController != null ? Mathf.Max(1, bombController.GetPlannedExplosionRadius()) : 2;
        for (int d = 0; d < CardinalTiles.Length; d++)
        {
            for (int step = 1; step <= radius; step++)
            {
                Vector2Int tile = bombTile + CardinalTiles[d] * step;
                bool blocks = HasIndestructibleTile(tile) || HasDestructibleTile(tile) || FindBombAt(tile) != null;
                plannedBlastTiles.Add(tile);
                if (blocks)
                    break; // o destrutível é atingido, mas a explosão para nele
            }
        }
    }

    private bool TryFindClosestEnemyTile(Vector2Int myTile, out Vector2Int enemyTile)
    {
        enemyTile = myTile;
        int bestDistance = int.MaxValue;

        var activePlayers = new List<PlayerIdentity>(6);
        PlayerIdentity.GetActivePlayers(activePlayers);

        for (int i = 0; i < activePlayers.Count; i++)
        {
            PlayerIdentity player = activePlayers[i];
            if (player == null || player == identity)
                continue;

            if (!player.TryGetComponent<MovementController>(out var enemyMovement) ||
                enemyMovement == null || enemyMovement.isDead)
                continue;

            // Mesmo critério de IsTeammate do BattleModeComController.
            if (BattleModeRules.Instance != null &&
                identity != null &&
                BattleModeRules.Instance.GetTeamForPlayer(identity.playerId) ==
                BattleModeRules.Instance.GetTeamForPlayer(player.playerId))
                continue;

            Vector2Int tile = WorldToTile(player.transform.position);
            int distance = Manhattan(myTile, tile);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                enemyTile = tile;
            }
        }

        return bestDistance != int.MaxValue;
    }

    // =====================================================================
    // Walkability / perigo
    // =====================================================================

    /// <summary>
    /// Igual à walkability padrão da IA, mas destrutíveis NÃO bloqueiam
    /// (DestructiblePass). Bombas só são atravessáveis se a IA também tiver BombPass.
    /// </summary>
    private bool IsWalkableTileThroughDestructibles(Vector2Int tile, Vector2Int startTile)
    {
        if (!HasGroundTile(tile))
            return false;

        if (HasIndestructibleTile(tile))
            return false;

        // DestructiblePass: destrutíveis são atravessáveis (não retorna false aqui).

        if (FindBombAt(tile) != null && tile != startTile && !CanAlsoPassBombs)
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
                    (tile == startTile || CanAlsoPassBombs))
                    continue;

                // DestructiblePass: ignora colliders dos destrutíveis.
                if (IsDestructibleCollider(hit))
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
        BattleModeComDifficultySettings settings,
        List<Vector2Int> extraBlastTiles)
    {
        float dangerSeconds = GetEffectiveDangerSeconds(tile, extraBlastTiles);
        if (float.IsInfinity(dangerSeconds))
            return false;

        return dangerSeconds <= arrivalSeconds + settings.dangerReactionSeconds;
    }

    private float GetEffectiveDangerSeconds(Vector2Int tile, List<Vector2Int> extraBlastTiles)
    {
        float danger = GetDangerSeconds(tile);

        if (extraBlastTiles != null && extraBlastTiles.Contains(tile))
        {
            float fuse = bombController != null ? Mathf.Max(0.5f, bombController.bombFuseTime) : 2f;
            danger = Mathf.Min(danger, fuse);
        }

        return danger;
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
    private void UpdateEscapeStuckDetection(Vector2Int myTile)
    {
        if (myTile == escapeLastTile)
        {
            if (escapeStuckSince < 0f)
            {
                escapeStuckSince = Time.time;
            }
            else if (Time.time - escapeStuckSince > EscapeStuckSeconds &&
                     escapeLastAttemptedStep != Vector2Int.zero &&
                     !escapeBlockedSteps.Contains(escapeLastAttemptedStep))
            {
                escapeBlockedSteps.Add(escapeLastAttemptedStep);
                escapeStuckSince = -1f;
                LogSurgical("ESCAPE_STUCK",
                    $"my:{myTile} blocking:{escapeLastAttemptedStep} total:{escapeBlockedSteps.Count}",
                    force: true);
            }
        }
        else
        {
            escapeLastTile = myTile;
            ClearEscapeStuckState();
        }
    }

    private void ClearEscapeStuckState()
    {
        escapeStuckSince = -1f;
        escapeBlockedSteps.Clear();
        escapeLastAttemptedStep = Vector2Int.zero;
    }

    // =====================================================================
    // Estado / chance
    // =====================================================================
    private void SetSequenceState(SequenceState state)
    {
        sequenceStateStartedTime = Time.time;
        sequenceState = state;
    }

    private void ResetSequence(string reason)
    {
        if (sequenceState != SequenceState.None)
            LogSurgical("SEQUENCE_RESET", reason, force: true);

        SetSequenceState(SequenceState.None);
        plantTargetTile = Vector2Int.zero;
        plantPlanLabel = "none";
        postPlantEscapeUntil = -10f;
        ClearEscapeStuckState();
        offensiveTriggerChanceCacheTime = -10f;
        offensiveTriggerChanceCacheResult = false;
    }

    private bool RollOffensiveChance(BattleModeComDifficultySettings settings)
    {
        if (Time.time - offensiveTriggerChanceCacheTime < 0.001f)
            return offensiveTriggerChanceCacheResult;

        float chance = DifficultyChance(settings, 0.10f, 0.25f, 0.50f);
        bool result = Random.value <= chance;
        offensiveTriggerChanceCacheTime = Time.time;
        offensiveTriggerChanceCacheResult = result;

        if (!result)
            lastDecisionTrace = $"chance fail chance:{chance:F2}";

        return result;
    }

    private static float DifficultyChance(
        BattleModeComDifficultySettings settings,
        float easy, float normal, float hard) =>
        settings.difficulty switch
        {
            BattleModeComputerLevel.Easy => easy,
            BattleModeComputerLevel.Hard => hard,
            _ => normal
        };

    private static int DifficultyWeight(BattleModeComDifficultySettings settings) =>
        settings.difficulty switch
        {
            BattleModeComputerLevel.Easy => -20,
            BattleModeComputerLevel.Hard => 30,
            _ => 0
        };

    // =====================================================================
    // Utilitários / diagnóstico
    // =====================================================================
    private void ResetBfsCounters()
    {
        bfsRejectedWalkability = 0;
        bfsRejectedDanger = 0;
        bfsDestructibleTilesEntered = 0;
    }

    private string BuildAvailabilityTrace()
    {
        if (identity == null) return "identity:null";
        if (movement == null) return "movement:null";
        if (movement.isDead) return "isDead";
        if (abilitySystem == null) return "abilitySystem:null";
        if (!abilitySystem.IsEnabled(DestructiblePassAbility.AbilityId)) return "DestructiblePassAbility:disabled";
        return "available?";
    }

    /// <summary>
    /// Resume os 4 vizinhos: atravessável, destrutível, bomba ou perigo.
    /// Ex.: "U[dest] D[wall] L[walk] R[danger 0.30]"
    /// </summary>
    private string BuildNeighborScanSummary(Vector2Int myTile)
    {
        var sb = new System.Text.StringBuilder(96);
        string[] labels = { "U", "D", "L", "R" };
        for (int i = 0; i < CardinalTiles.Length; i++)
        {
            Vector2Int next = myTile + CardinalTiles[i];
            sb.Append(labels[i]).Append('[');

            if (HasIndestructibleTile(next) || !HasGroundTile(next))
            {
                sb.Append("wall");
            }
            else if (HasDestructibleTile(next))
            {
                sb.Append(IsWalkableTileThroughDestructibles(next, myTile) ? "dest" : "dest-blocked");
            }
            else
            {
                Bomb bomb = FindBombAt(next);
                if (bomb != null)
                {
                    sb.Append("bomb f:");
                    sb.Append(bomb.RemainingFuseSeconds.ToString("F2"));
                }
                else if (!IsWalkableTileThroughDestructibles(next, myTile))
                {
                    sb.Append("blocked");
                }
                else
                {
                    float danger = GetDangerSeconds(next);
                    sb.Append(float.IsInfinity(danger) ? "walk" : $"danger {danger:F2}");
                }
            }

            sb.Append("] ");
        }

        sb.Append($"bfs(rejWalk:{bfsRejectedWalkability} rejDanger:{bfsRejectedDanger} " +
                  $"destEntered:{bfsDestructibleTilesEntered})");
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

    private static int Manhattan(Vector2Int a, Vector2Int b) =>
        Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

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

    private static string AppendInput(string existing, string input) =>
        string.IsNullOrEmpty(existing) || existing == "none" ? input : existing + "+" + input;

    private static string FormatDanger(float seconds)
    {
        if (float.IsInfinity(seconds)) return "safe";
        if (seconds <= 0f) return "now";
        return $"{seconds:F2}";
    }

    private void LogSurgical(string key, string message, bool force = false)
    {
        if (!EnableSurgicalDiagnostics) return;

        int id = identity != null ? Mathf.Clamp(identity.playerId, 1, 6) : 0;
        if (DiagnosticPlayerIdFilter != 0 && id != DiagnosticPlayerIdFilter) return;

        if (!force &&
            key == lastSurgicalLogKey &&
            Time.time - lastSurgicalLogTime < SurgicalLogIntervalSeconds)
            return;

        lastSurgicalLogKey = key;
        lastSurgicalLogTime = Time.time;
        Vector2Int tile = movement != null ? WorldToTile(movement.transform.position) : Vector2Int.zero;
        Debug.LogWarning($"[BattleCOM{DiagnosticName}][P{id}] tile:{tile} state:{sequenceState} {key} {message}", this);
    }
}

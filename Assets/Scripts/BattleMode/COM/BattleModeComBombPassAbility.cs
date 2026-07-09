using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Ability de IA para BombPass: torna a IA ciente de que pode ATRAVESSAR bombas.
///
/// COMPORTAMENTOS:
///   1. EMERGENCY — fuga através de bombas. O BFS nativo do controller trata bombas
///      como obstáculos; com BombPass elas são atravessáveis. Quando a fuga normal
///      seria impossível ou pior (encurralamento entre bombas), esta ability fornece
///      uma rota de fuga que passa por cima de tiles com bomba (respeitando o fuse).
///   2. CANDIDATE / DESENCURRALAMENTO — quando a IA está num beco fechado por bombas
///      (sem perigo imediato), reposiciona-se atravessando a bomba para área aberta.
///   3. CANDIDATE / OFENSIVA — usa rotas através de bombas para alcançar inimigos e
///      plantar bomba; a fuga pós-plant também pode atravessar bombas.
///
/// EXCLUSÕES:
///   BombPass é mutuamente exclusivo com BombKick (BattleModeComKickBombAbility nunca
///   coexiste — o PlayerPersistentStats já zera CanKickBombs ao ativar CanPassBombs).
///   BattleModeComYellowLouieKickAbility PODE coexistir (chute via Yellow Louie não
///   depende de BombKickAbility) e não é afetada por esta ability.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerIdentity))]
[RequireComponent(typeof(MovementController))]
[RequireComponent(typeof(BombController))]
public sealed class BattleModeComBombPassAbility : MonoBehaviour, IBattleModeComAbility
{
    // === Filtro de diagnóstico ===
    public const int DiagnosticPlayerIdFilter = 0; // 0 = todos
    // Diagnóstico da fuga via BombPass — ATIVO para investigar por que a IA
    // não cria rotas de fuga atravessando bombas.
    public static readonly bool EnableBombPassEscapeDiagnostics = false;
    private static bool EnableSurgicalDiagnostics => EnableBombPassEscapeDiagnostics;
    private const float SurgicalLogIntervalSeconds = 0.35f;

    // === Constantes de comportamento ===
    // Cooldown entre planos ofensivos iniciados por esta ability.
    private const float OffensiveCooldownSeconds = 2.5f;
    // Margem extra de segurança exigida ao ATRAVESSAR um tile com bomba
    // (estar em cima da bomba quando ela explode é morte certa).
    private const float BombTileExtraMarginSeconds = 0.40f;
    // Tempo máximo da fase de aproximação ofensiva.
    private const float ApproachTimeoutSeconds = 6.0f;
    // Tempo máximo da fuga pós-plant.
    private const float PostPlantEscapeSeconds = 2.2f;
    // Distância máxima (Manhattan) do inimigo para iniciar plano ofensivo.
    private const int MaxOffensiveEnemyDistance = 10;
    // Tempo parado no mesmo tile para considerar travamento na fuga.
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
    private float postPlantEscapeUntil = -10f;
    private float nextOffensiveTime = -10f;

    // === Detecção de travamento na fuga ===
    private Vector2Int escapeLastTile;
    private float escapeStuckSince = -10f;
    private Vector2Int escapeLastAttemptedStep;
    private readonly List<Vector2Int> escapeBlockedSteps = new List<Vector2Int>(4);

    // === Cache de chance ofensiva (evita re-roll no mesmo ciclo de Think) ===
    private float offensiveTriggerChanceCacheTime = -10f;
    private bool offensiveTriggerChanceCacheResult;

    // === BFS reutilizável ===
    private struct SearchNode
    {
        public Vector2Int Parent;
        public int Depth;
        public bool UsedBombTile;
    }

    private readonly Dictionary<Vector2Int, SearchNode> searchVisited =
        new Dictionary<Vector2Int, SearchNode>(96);
    private readonly Queue<Vector2Int> searchOpen = new Queue<Vector2Int>(96);
    private readonly List<Vector2Int> plannedBlastTiles = new List<Vector2Int>(32);

    // === Contadores de rejeição do último BFS (diagnóstico) ===
    private int bfsRejectedWalkability;
    private int bfsRejectedDanger;
    private int bfsRejectedBombDanger;
    private int bfsBombTilesEntered;

    // === Diagnóstico ===
    private string lastDecisionTrace = "not evaluated";
    private float lastSurgicalLogTime = -10f;
    private string lastSurgicalLogKey = string.Empty;

    // === IBattleModeComAbility ===
    public string DiagnosticName => "BombPass";
    public string LastDecisionTrace => lastDecisionTrace;

    public bool IsAvailable
    {
        get
        {
            CacheReferences();
            if (identity == null || movement == null || movement.isDead)
                return false;

            return abilitySystem != null &&
                   abilitySystem.IsEnabled(BombPassAbility.AbilityId);
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
    // TryBuildEmergencyDecision — fuga através de bombas
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

        // Fuga pós-plant em andamento tem prioridade — ela já considera bombas
        // atravessáveis e o perigo da própria bomba plantada.
        if (sequenceState == SequenceState.EscapingAfterPlant)
        {
            if (TryBuildPostPlantEscapeDecision(settings, myTile, out decision))
            {
                lastDecisionTrace = "emergency continue post-plant escape -> " + lastDecisionTrace;
                return true;
            }

            return false;
        }

        // Aproximação ofensiva sob perigo: aborta — fugir vem primeiro.
        if (sequenceState == SequenceState.ApproachingPlantTile)
            ResetSequence("danger during approach");

        // Fuga de emergência atravessando bombas.
        UpdateEscapeStuckDetection(myTile);

        // Log da avaliação: o que a IA enxerga ao redor neste momento de perigo.
        LogSurgical("EMERGENCY_EVAL",
            $"danger:{FormatDanger(currentDangerSeconds)} scan:{BuildNeighborScanSummary(settings, myTile)} " +
            $"blockedSteps:{escapeBlockedSteps.Count}");

        if (!TryFindSafeTileThroughBombs(
                settings, myTile, escapeBlockedSteps, null,
                out Vector2 firstMove, out Vector2Int target,
                out bool usedBombTile, out int depth))
        {
            lastDecisionTrace = "emergency no route through bombs";
            LogSurgical("EMERGENCY_NO_ROUTE",
                $"danger:{FormatDanger(currentDangerSeconds)} scan:{BuildNeighborScanSummary(settings, myTile)}",
                force: true);
            return false;
        }

        // Se a rota não usa nenhum tile de bomba, a fuga nativa do controller
        // encontraria o mesmo caminho — não interferimos.
        if (!usedBombTile)
        {
            lastDecisionTrace = $"emergency route does not need bomb pass (target:{target})";
            LogSurgical("EMERGENCY_DEFER_NATIVE",
                $"target:{target} depth:{depth} danger:{FormatDanger(currentDangerSeconds)} " +
                $"(rota sem bomba existe; fuga nativa assume)");
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
            Reason = "bomb-pass escape",
            InputDescription = FirstMoveDescription(firstMove)
        };

        lastDecisionTrace = $"emergency bomb-pass escape target:{target} depth:{depth}";
        LogSurgical("EMERGENCY_BOMBPASS_ESCAPE",
            $"my:{myTile} target:{target} move:{FirstMoveDescription(firstMove)} depth:{depth} " +
            $"danger:{FormatDanger(currentDangerSeconds)} (NOVA ROTA ATRAVES DE BOMBA)",
            force: true);
        return true;
    }

    // =====================================================================
    // TryBuildCandidateDecision — desencurralamento + ofensiva
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

        // 1. Sequência em andamento tem prioridade.
        if (sequenceState == SequenceState.EscapingAfterPlant)
            return TryBuildPostPlantEscapeDecision(settings, myTile, out decision);

        if (sequenceState == SequenceState.ApproachingPlantTile)
            return TryContinueApproach(settings, myTile, out decision);

        // 2. Desencurralamento: cercado por bombas mas ainda sem perigo imediato.
        if (TryBuildUncornerDecision(settings, myTile, out decision))
            return true;

        // 3. Plano ofensivo.
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

        // Conta vizinhos livres SEM atravessar bombas (visão do controller).
        int openWithoutBombs = 0;
        bool hasAdjacentBomb = false;
        for (int i = 0; i < CardinalTiles.Length; i++)
        {
            Vector2Int next = myTile + CardinalTiles[i];
            if (FindBombAt(next) != null)
            {
                hasAdjacentBomb = true;
                continue;
            }

            if (IsWalkableTileThroughBombs(next, myTile))
                openWithoutBombs++;
        }

        // Só é encurralamento se há bomba adjacente e no máximo 1 saída normal.
        if (!hasAdjacentBomb || openWithoutBombs > 1)
        {
            lastDecisionTrace =
                $"candidate not cornered open:{openWithoutBombs} adjBomb:{hasAdjacentBomb}";
            return false;
        }

        UpdateEscapeStuckDetection(myTile);

        if (!TryFindSafeTileThroughBombs(
                settings, myTile, escapeBlockedSteps, null,
                out Vector2 firstMove, out Vector2Int target,
                out bool usedBombTile, out int depth))
        {
            lastDecisionTrace = "candidate cornered but no route through bombs";
            return false;
        }

        if (!usedBombTile)
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
            Reason = "bomb-pass uncorner",
            InputDescription = FirstMoveDescription(firstMove)
        };

        lastDecisionTrace = $"candidate uncorner target:{target} depth:{depth}";
        LogSurgical("UNCORNER",
            $"my:{myTile} target:{target} move:{FirstMoveDescription(firstMove)} open:{openWithoutBombs}");
        return true;
    }

    // =====================================================================
    // Plano ofensivo
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

        if (!TryFindOffensivePlantTile(settings, myTile,
                out Vector2Int plantTile, out Vector2 firstMove,
                out bool usedBombTile, out int depth, out Vector2Int enemyTile))
        {
            lastDecisionTrace = "candidate no offensive plant tile";
            return false;
        }

        // Só interessa se a rota usa bomb-pass — caso contrário o CombatPlant
        // nativo do controller já cobre o mesmo plano.
        if (!usedBombTile)
        {
            lastDecisionTrace = "candidate offensive route does not need bomb pass";
            return false;
        }

        plantTargetTile = plantTile;
        SetSequenceState(SequenceState.ApproachingPlantTile);
        nextOffensiveTime = Time.time + OffensiveCooldownSeconds;

        // Já no tile de plantio? Planta imediatamente.
        if (myTile == plantTile)
            return TryPlantNow(settings, myTile, out decision);

        decision = new BattleModeComAbilityDecision
        {
            Action = BattleModeComActionType.CombatPlant,
            Weight = settings.combatPlantWeight + 90 + DifficultyWeight(settings),
            TargetTile = plantTile,
            HasTarget = true,
            FirstMove = firstMove,
            Reason = "bomb-pass offensive approach",
            InputDescription = FirstMoveDescription(firstMove)
        };

        lastDecisionTrace =
            $"candidate offensive approach plant:{plantTile} enemy:{enemyTile} depth:{depth}";
        LogSurgical("PLAN",
            $"my:{myTile} plant:{plantTile} enemy:{enemyTile} depth:{depth} move:{FirstMoveDescription(firstMove)}");
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

        // Re-pathing a cada ciclo (bombas se movem / explodem).
        if (!TryFindPathThroughBombs(settings, myTile, plantTargetTile,
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
            Reason = "bomb-pass offensive approach",
            InputDescription = FirstMoveDescription(firstMove)
        };

        lastDecisionTrace = $"approach continue plant:{plantTargetTile} depth:{depth}";
        LogSurgical("APPROACH", $"my:{myTile} plant:{plantTargetTile} depth:{depth}");
        return true;
    }

    private bool TryPlantNow(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        out BattleModeComAbilityDecision decision)
    {
        decision = default;

        // Antes de plantar, garante que existe fuga considerando a bomba planejada.
        BuildPlannedBlastTiles(myTile);
        if (!TryFindSafeTileThroughBombs(
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
            Reason = "bomb-pass plant",
            InputDescription = AppendInput(FirstMoveDescription(escapeMove), "ActionA"),
            TapBomb = true
        };

        lastDecisionTrace = $"plant now escape:{escapeTarget}";
        LogSurgical("PLANT", $"my:{myTile} escape:{escapeTarget}", force: true);
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

        if (!TryFindSafeTileThroughBombs(
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
            Reason = "bomb-pass escape after plant",
            InputDescription = FirstMoveDescription(firstMove)
        };

        lastDecisionTrace = $"post-plant escape target:{target} depth:{depth}";
        LogSurgical("ESCAPE",
            $"my:{myTile} target:{target} move:{FirstMoveDescription(firstMove)} dangerHere:{FormatDanger(dangerHere)}");
        return true;
    }

    // =====================================================================
    // BFS através de bombas
    // =====================================================================

    /// <summary>
    /// BFS que encontra o tile seguro mais próximo permitindo atravessar bombas.
    /// Atravessar um tile com bomba exige margem extra de fuse
    /// (BombTileExtraMarginSeconds) além da checagem normal de perigo.
    /// </summary>
    private bool TryFindSafeTileThroughBombs(
        BattleModeComDifficultySettings settings,
        Vector2Int start,
        List<Vector2Int> blockedFirstSteps,
        List<Vector2Int> extraBlastTiles,
        out Vector2 firstMove,
        out Vector2Int target,
        out bool usedBombTile,
        out int resultDepth)
    {
        firstMove = Vector2.zero;
        target = start;
        usedBombTile = false;
        resultDepth = 0;

        ResetBfsCounters();
        searchVisited.Clear();
        searchOpen.Clear();
        searchVisited[start] = new SearchNode { Parent = start, Depth = 0, UsedBombTile = false };

        if (blockedFirstSteps != null)
        {
            for (int i = 0; i < blockedFirstSteps.Count; i++)
            {
                if (blockedFirstSteps[i] != Vector2Int.zero)
                    searchVisited[start + blockedFirstSteps[i]] =
                        new SearchNode { Parent = start, Depth = 0, UsedBombTile = false };
            }
        }

        searchOpen.Enqueue(start);
        int maxDepth = Mathf.Max(4, settings.searchDepth + 4);

        while (searchOpen.Count > 0)
        {
            Vector2Int tile = searchOpen.Dequeue();
            SearchNode node = searchVisited[tile];
            float eta = EstimateTraversalSeconds(node.Depth);

            bool tileHasBomb = FindBombAt(tile) != null;
            if (node.Depth > 0 &&
                !tileHasBomb &&
                float.IsInfinity(GetEffectiveDangerSeconds(tile, extraBlastTiles)) &&
                !IsDangerousAt(tile, eta + settings.safeTileMinimumSeconds, settings, extraBlastTiles))
            {
                target = tile;
                usedBombTile = node.UsedBombTile;
                resultDepth = node.Depth;
                firstMove = TileDirectionToVector(ReconstructFirstStep(start, tile));
                return firstMove != Vector2.zero;
            }

            if (node.Depth >= maxDepth)
                continue;

            ExpandThroughBombs(tile, node, start, settings, extraBlastTiles);
        }

        return false;
    }

    /// <summary>
    /// BFS de caminho até um destino específico, atravessando bombas.
    /// </summary>
    private bool TryFindPathThroughBombs(
        BattleModeComDifficultySettings settings,
        Vector2Int start,
        Vector2Int goal,
        out Vector2 firstMove,
        out int resultDepth)
    {
        firstMove = Vector2.zero;
        resultDepth = 0;

        searchVisited.Clear();
        searchOpen.Clear();
        searchVisited[start] = new SearchNode { Parent = start, Depth = 0, UsedBombTile = false };
        searchOpen.Enqueue(start);

        int maxDepth = Mathf.Max(4, settings.searchDepth + 4);

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

            ExpandThroughBombs(tile, node, start, settings, null);
        }

        return false;
    }

    private void ExpandThroughBombs(
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

            if (!IsWalkableTileThroughBombs(next, start))
            {
                bfsRejectedWalkability++;
                continue;
            }

            float eta = EstimateTraversalSeconds(node.Depth + 1);
            bool nextHasBomb = FindBombAt(next) != null;

            // Tiles com bomba exigem margem extra: estar sobre a bomba quando
            // o fuse acaba é fatal, então só atravessamos com folga.
            float requiredMargin = nextHasBomb ? BombTileExtraMarginSeconds : 0f;
            if (IsDangerousAt(next, eta + requiredMargin, settings, extraBlastTiles))
            {
                if (nextHasBomb) bfsRejectedBombDanger++;
                else bfsRejectedDanger++;
                continue;
            }

            if (nextHasBomb)
                bfsBombTilesEntered++;

            searchVisited[next] = new SearchNode
            {
                Parent = tile,
                Depth = node.Depth + 1,
                UsedBombTile = node.UsedBombTile || nextHasBomb
            };
            searchOpen.Enqueue(next);
        }
    }

    /// <summary>
    /// Procura o melhor tile de plantio próximo a um inimigo, alcançável
    /// atravessando bombas. Plant tile = tile na linha de blast do inimigo
    /// (dentro do raio da própria bomba) que tenha rota de fuga.
    /// </summary>
    private bool TryFindOffensivePlantTile(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        out Vector2Int plantTile,
        out Vector2 firstMove,
        out bool usedBombTile,
        out int resultDepth,
        out Vector2Int enemyTile)
    {
        plantTile = myTile;
        firstMove = Vector2.zero;
        usedBombTile = false;
        resultDepth = 0;
        enemyTile = myTile;

        if (!TryFindClosestEnemyTile(myTile, out enemyTile))
            return false;

        if (Manhattan(myTile, enemyTile) > MaxOffensiveEnemyDistance)
            return false;

        int radius = bombController != null ? Mathf.Max(1, bombController.GetPlannedExplosionRadius()) : 2;

        // BFS através de bombas; primeiro tile alcançado que está na linha de
        // blast do inimigo (e com fuga viável) vira o plant tile.
        searchVisited.Clear();
        searchOpen.Clear();
        searchVisited[myTile] = new SearchNode { Parent = myTile, Depth = 0, UsedBombTile = false };
        searchOpen.Enqueue(myTile);

        int maxDepth = Mathf.Max(4, settings.searchDepth + 2);

        while (searchOpen.Count > 0)
        {
            Vector2Int tile = searchOpen.Dequeue();
            SearchNode node = searchVisited[tile];

            bool tileHasBomb = FindBombAt(tile) != null;
            if (!tileHasBomb &&
                tile != enemyTile &&
                IsTileInBlastLine(tile, enemyTile, radius) &&
                HasEscapeAfterPlantAt(settings, tile))
            {
                plantTile = tile;
                usedBombTile = node.UsedBombTile;
                resultDepth = node.Depth;
                firstMove = node.Depth == 0
                    ? Vector2.zero
                    : TileDirectionToVector(ReconstructFirstStep(myTile, tile));
                return node.Depth == 0 || firstMove != Vector2.zero;
            }

            if (node.Depth >= maxDepth)
                continue;

            ExpandThroughBombs(tile, node, myTile, settings, null);
        }

        return false;
    }

    private bool HasEscapeAfterPlantAt(BattleModeComDifficultySettings settings, Vector2Int tile)
    {
        BuildPlannedBlastTiles(tile);

        // Checagem leve: existe vizinho atravessável fora do blast planejado e seguro?
        for (int i = 0; i < CardinalTiles.Length; i++)
        {
            Vector2Int next = tile + CardinalTiles[i];
            if (!IsWalkableTileThroughBombs(next, tile))
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
                if (HasIndestructibleTile(tile) || HasDestructibleTile(tile) || FindBombAt(tile) != null)
                    break;

                plannedBlastTiles.Add(tile);
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
    /// Igual à walkability padrão da IA, mas bombas NÃO bloqueiam (BombPass).
    /// </summary>
    private bool IsWalkableTileThroughBombs(Vector2Int tile, Vector2Int startTile)
    {
        if (!HasGroundTile(tile))
            return false;

        if (HasIndestructibleTile(tile) || HasDestructibleTile(tile))
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

                // BombPass: bombas são atravessáveis em qualquer tile.
                if (hit.GetComponentInParent<Bomb>() != null)
                    continue;

                return false;
            }
        }

        return true;
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
    // Utilitários
    // =====================================================================
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

    private void ResetBfsCounters()
    {
        bfsRejectedWalkability = 0;
        bfsRejectedDanger = 0;
        bfsRejectedBombDanger = 0;
        bfsBombTilesEntered = 0;
    }

    /// <summary>
    /// Explica por que IsAvailable está false (para o log EMERGENCY_UNAVAILABLE).
    /// </summary>
    private string BuildAvailabilityTrace()
    {
        if (identity == null) return "identity:null";
        if (movement == null) return "movement:null";
        if (movement.isDead) return "isDead";
        if (abilitySystem == null) return "abilitySystem:null";
        if (!abilitySystem.IsEnabled(BombPassAbility.AbilityId)) return "BombPassAbility:disabled";
        return "available?";
    }

    /// <summary>
    /// Resume os 4 vizinhos cardinais: atravessável, tem bomba, fuse/perigo.
    /// Ex.: "U[walk] D[bomb f:1.20] L[wall] R[danger 0.30]"
    /// </summary>
    private string BuildNeighborScanSummary(BattleModeComDifficultySettings settings, Vector2Int myTile)
    {
        var sb = new System.Text.StringBuilder(96);
        string[] labels = { "U", "D", "L", "R" };
        for (int i = 0; i < CardinalTiles.Length; i++)
        {
            Vector2Int next = myTile + CardinalTiles[i];
            sb.Append(labels[i]).Append('[');

            if (!IsWalkableTileThroughBombs(next, myTile))
            {
                sb.Append("wall");
            }
            else
            {
                Bomb bomb = FindBombAt(next);
                if (bomb != null)
                {
                    sb.Append("bomb f:");
                    sb.Append(bomb.RemainingFuseSeconds.ToString("F2"));
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
                  $"rejBombDanger:{bfsRejectedBombDanger} bombEntered:{bfsBombTilesEntered})");
        return sb.ToString();
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

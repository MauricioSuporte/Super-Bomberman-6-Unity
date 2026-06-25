using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Ability de IA para usar a linha de bombas do Purple Louie
/// (PurpleLouieBombLineAbility).
///
/// MECÂNICA (PurpleLouieBombLineAbility.cs):
///   ActionC planta TODAS as bombas da reserva em linha reta à frente (direção do
///   facing), parando em indestrutível/inimigo/tile inválido. Se o jogador possui
///   ControlBomb, a linha inteira é de control bombs e ActionB detona TODAS
///   (TryExplodeAllControlledBombs).
///
/// COMPORTAMENTO DA IA (puramente ofensivo):
///   1. SETUP — se o adversário está muito perto ou fora do eixo, procura um tile
///      seguro que crie espaço e deixe a explosão lateral da linha alcançá-lo.
///   2. CAST — vira no eixo dominante do adversário e pressiona ActionC. O alvo
///      não precisa estar exatamente na mesma linha/coluna.
///   3. RETREAT — após o cast a IA está colada na primeira bomba da linha; recua
///      para fora da zona de blast combinada da linha.
///   4. DETONATE — linha de control bombs: quando a IA (e aliados) estão fora da
///      zona, pressiona ActionB para detonar tudo (idealmente com o adversário
///      ainda na zona). Linha normal: o fuse resolve sozinho e a fuga nativa cuida.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerIdentity))]
[RequireComponent(typeof(MovementController))]
[RequireComponent(typeof(BombController))]
public sealed class BattleModeComPurpleLouieBombLineAbility : MonoBehaviour, IBattleModeComAbility
{
    // === Filtro de diagnóstico ===
    public const int DiagnosticPlayerIdFilter = 0; // 0 = todos
    public static readonly bool EnablePurpleLineDiagnostics = false;
    private const float SurgicalLogIntervalSeconds = 0.35f;

    // === Constantes de comportamento ===
    // Mínimo de bombas na reserva para a linha valer a pena.
    private const int MinBombsForLine = 2;
    // Tempo máximo da fase de recuo antes de abortar a sequência.
    private const float RetreatTimeoutSeconds = 4.0f;
    private const float SetupTimeoutSeconds = 3.0f;
    private const float RetreatStuckSeconds = 0.25f;
    private const int CloseEnemyDistance = 3;
    // Profundidade máxima do tile de cast no SETUP. Planos muito longos quase
    // nunca sobrevivem ao trajeto numa arena cheia de bombas alheias, então
    // limitamos a busca para favorecer casts próximos e alcançáveis.
    private const int MaxSetupDepth = 4;
    // Após abortar um SETUP (perigo/rota perdida), espera curta antes de tentar
    // de novo — evita spam de re-roll sem silenciar a habilidade por segundos.
    private const float SetupAbortRetrySeconds = 0.75f;

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
    private readonly List<PlayerIdentity> occupancyPlayers = new List<PlayerIdentity>(6);
    private float tileSize = 1f;
    private int explosionMask;

    // === Máquina de estados ===
    private enum SequenceState
    {
        None,
        ApproachingCastTile,
        Retreating
    }

    private SequenceState sequenceState;
    private float sequenceStartedTime = -10f;
    private Vector2Int setupCastTile;
    private Vector2Int setupCastDirection;
    private int setupEnemyId;
    private int setupLineLength;
    private bool lineIsControl;
    private readonly List<Vector2Int> plannedBlastTiles = new List<Vector2Int>(48);
    private readonly List<Vector2Int> plannedBombTiles = new List<Vector2Int>(12);
    private float nextCastTime = -10f;
    // O DoCast do PurpleLouie trava o input por ~0.25s; durante essa janela não
    // emitimos decisões de recuo (evita stuck detection falso e BFS antes de as
    // bombas existirem nos tiles).
    private float castLockUntil = -10f;
    private const float CastLockSeconds = 0.35f;

    // === Cache de chance ===
    private float chanceCacheTime = -10f;
    private bool chanceCacheResult;

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

    private struct CastPlan
    {
        public Vector2Int CastTile;
        public Vector2Int Direction;
        public Vector2Int FirstStep;
        public int EnemyId;
        public int EnemyDistance;
        public int LineLength;
        public int Depth;
        public int Score;
    }

    private readonly Dictionary<Vector2Int, SearchNode> searchVisited =
        new Dictionary<Vector2Int, SearchNode>(96);
    private readonly Queue<Vector2Int> searchOpen = new Queue<Vector2Int>(96);
    private readonly List<CastPlan> castPlans = new List<CastPlan>(32);

    // === Diagnóstico ===
    private string lastDecisionTrace = "not evaluated";
    private float lastSurgicalLogTime = -10f;
    private string lastSurgicalLogKey = string.Empty;

    // === IBattleModeComAbility ===
    public string DiagnosticName => "PurpleLouieLine";
    public string LastDecisionTrace => lastDecisionTrace;

    public bool IsAvailable
    {
        get
        {
            CacheReferences();
            if (identity == null || movement == null || movement.isDead)
                return false;

            return abilitySystem != null &&
                   abilitySystem.IsEnabled(PurpleLouieBombLineAbility.AbilityId);
        }
    }

    private bool HasControlBombs =>
        abilitySystem != null && abilitySystem.IsEnabled(ControlBombAbility.AbilityId);

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
    // Emergency — continua o recuo da sequência em andamento
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
            if (sequenceState != SequenceState.None)
                ResetSequence("ability disabled mid-sequence");
            return false;
        }

        if (sequenceState == SequenceState.Retreating)
        {
            if (TryBuildRetreatDecision(settings, myTile, out decision))
            {
                lastDecisionTrace = "emergency continue retreat -> " + lastDecisionTrace;
                return true;
            }

            return false;
        }

        if (sequenceState == SequenceState.ApproachingCastTile &&
            !float.IsInfinity(currentDangerSeconds))
        {
            ResetSequence("danger during setup");
            lastDecisionTrace = "emergency aborted threatened setup";
            return false;
        }

        lastDecisionTrace = "emergency no active sequence";
        return false;
    }

    // =====================================================================
    // Candidate — cast / recuo / detonação
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
            LogSurgical("CANDIDATE_REJECT_UNAVAILABLE",
                $"identity:{(identity != null)} movement:{(movement != null)} " +
                $"dead:{(movement != null && movement.isDead)} " +
                $"abilitySystem:{(abilitySystem != null)} " +
                $"enabled:{(abilitySystem != null && abilitySystem.IsEnabled(PurpleLouieBombLineAbility.AbilityId))}");
            if (sequenceState != SequenceState.None)
                ResetSequence("ability disabled mid-sequence");
            return false;
        }

        if (sequenceState == SequenceState.Retreating)
            return TryBuildRetreatDecision(settings, myTile, out decision);

        if (sequenceState == SequenceState.ApproachingCastTile)
            return TryContinueCastSetup(settings, myTile, out decision);

        return TryStartLineCast(settings, myTile, out decision);
    }

    // =====================================================================
    // Cast da linha
    // =====================================================================
    private bool TryStartLineCast(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        out BattleModeComAbilityDecision decision)
    {
        decision = default;

        if (bombController == null || bombController.BombsRemaining < MinBombsForLine)
        {
            int remaining = bombController != null ? bombController.BombsRemaining : 0;
            lastDecisionTrace = $"candidate few bombs ({remaining})";
            LogSurgical("CANDIDATE_REJECT_FEW_BOMBS",
                $"my:{myTile} bombs:{remaining} min:{MinBombsForLine}");
            return false;
        }

        // Nunca re-casta com bombas próprias ainda vivas em campo — era a causa
        // do spam de ActionC colado na linha recém-plantada.
        if (AnyOwnActiveBomb())
        {
            lastDecisionTrace = "candidate own bombs still active";
            LogSurgical("CANDIDATE_REJECT_OWN_BOMBS_ACTIVE", $"my:{myTile}");
            return false;
        }

        if (Time.time < nextCastTime)
        {
            float wait = nextCastTime - Time.time;
            lastDecisionTrace = $"candidate cooldown {wait:F2}s";
            LogSurgical("CANDIDATE_REJECT_COOLDOWN", $"my:{myTile} wait:{wait:F2}s");
            return false;
        }

        // Não inicia a jogada com o próprio tile ameaçado.
        if (!float.IsInfinity(GetDangerSeconds(myTile)))
        {
            lastDecisionTrace = "candidate own tile threatened";
            LogSurgical("CANDIDATE_REJECT_TILE_THREATENED",
                $"my:{myTile} danger:{FormatDanger(GetDangerSeconds(myTile))}");
            return false;
        }

        if (!TryFindCastPlan(settings, myTile, out CastPlan plan))
        {
            lastDecisionTrace = "candidate no useful line/setup plan";
            LogSurgical("CANDIDATE_REJECT_NO_PLAN",
                $"my:{myTile} bombs:{bombController.BombsRemaining} " +
                $"nearestEnemy:{GetNearestEnemyDistance(myTile)} searchDepth:{settings.searchDepth}");
            return false;
        }

        // Em curta distância, afastar e usar a habilidade é uma decisão
        // intencional, não um sorteio. Fora desse caso, reserva cheia usa sempre
        // e reservas parciais ainda respeitam a personalidade da dificuldade.
        bool fullReserve = bombController.BombsRemaining >= bombController.bombAmout;
        bool closeSetup =
            plan.Depth > 0 &&
            GetNearestEnemyDistance(myTile) <= CloseEnemyDistance;
        if (!fullReserve && !closeSetup && !RollCastChance(settings))
        {
            lastDecisionTrace = "candidate chance fail";
            LogSurgical("CANDIDATE_REJECT_CHANCE",
                $"my:{myTile} fullReserve:{fullReserve} closeSetup:{closeSetup} " +
                $"bombs:{bombController.BombsRemaining}/{bombController.bombAmout} " +
                $"diff:{settings.difficulty}");
            return false;
        }

        LogSurgical("CANDIDATE_PLAN_OK",
            $"my:{myTile} cast:{plan.CastTile} dir:{FirstMoveDescription(plan.Direction)} " +
            $"enemy:P{plan.EnemyId} depth:{plan.Depth} line:{plan.LineLength} " +
            $"fullReserve:{fullReserve} closeSetup:{closeSetup}",
            force: true);

        setupCastTile = plan.CastTile;
        setupCastDirection = plan.Direction;
        setupEnemyId = plan.EnemyId;
        setupLineLength = plan.LineLength;

        if (myTile == setupCastTile)
            return TryCastNow(settings, myTile, out decision);

        // NÃO consome o cooldown aqui: ele só deve contar após um CAST real
        // (TryCastNow). Antes, todo SETUP abortado silenciava a habilidade pelo
        // cooldown cheio mesmo sem nunca plantar a linha — principal causa de a
        // IA "quase não usar" o Purple Louie.
        SetSequenceState(SequenceState.ApproachingCastTile);

        decision = new BattleModeComAbilityDecision
        {
            Action = BattleModeComActionType.Reposition,
            Weight = closeSetup ? 1800 : settings.combatPlantWeight + 220 + DifficultyWeight(settings),
            TargetTile = setupCastTile,
            HasTarget = true,
            FirstMove = TileDirectionToVector(plan.FirstStep),
            Reason = $"purple-line setup for P{setupEnemyId}",
            InputDescription = FirstMoveDescription(plan.FirstStep)
        };

        lastDecisionTrace =
            $"candidate SETUP tile:{setupCastTile} dir:{setupCastDirection} enemy:P{setupEnemyId} depth:{plan.Depth}";
        LogSurgical("SETUP",
            $"my:{myTile} cast:{setupCastTile} dir:{FirstMoveDescription(setupCastDirection)} " +
            $"enemy:P{setupEnemyId} enemyDist:{plan.EnemyDistance} depth:{plan.Depth} line:{setupLineLength}",
            force: true);
        return true;
    }

    private bool TryContinueCastSetup(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        out BattleModeComAbilityDecision decision)
    {
        decision = default;

        if (Time.time - sequenceStartedTime > SetupTimeoutSeconds)
        {
            ResetSequence("setup timeout");
            lastDecisionTrace = "setup timeout";
            return false;
        }

        if (bombController == null || bombController.BombsRemaining < MinBombsForLine ||
            AnyOwnActiveBomb())
        {
            ResetSequence("setup lost bomb availability");
            lastDecisionTrace = "setup bombs unavailable";
            return false;
        }

        if (!float.IsInfinity(GetDangerSeconds(myTile)))
        {
            ResetSequence("setup tile became threatened");
            lastDecisionTrace = "setup threatened";
            return false;
        }

        if (myTile == setupCastTile)
            return TryCastNow(settings, myTile, out decision);

        if (!TryFindPathToTile(settings, myTile, setupCastTile,
                out Vector2 firstMove, out int depth))
        {
            if (!TryFindCastPlan(settings, myTile, out CastPlan replacement))
            {
                ResetSequence("setup path lost");
                lastDecisionTrace = "setup path lost";
                return false;
            }

            setupCastTile = replacement.CastTile;
            setupCastDirection = replacement.Direction;
            setupEnemyId = replacement.EnemyId;
            setupLineLength = replacement.LineLength;
            firstMove = TileDirectionToVector(replacement.FirstStep);
            depth = replacement.Depth;

            if (myTile == setupCastTile)
                return TryCastNow(settings, myTile, out decision);
        }

        decision = new BattleModeComAbilityDecision
        {
            Action = BattleModeComActionType.Reposition,
            Weight = 1800,
            TargetTile = setupCastTile,
            HasTarget = true,
            FirstMove = firstMove,
            Reason = $"purple-line setup for P{setupEnemyId}",
            InputDescription = FirstMoveDescription(Vector2Int.RoundToInt(firstMove))
        };

        lastDecisionTrace = $"setup continue cast:{setupCastTile} enemy:P{setupEnemyId} depth:{depth}";
        LogSurgical("SETUP_CONTINUE",
            $"my:{myTile} cast:{setupCastTile} enemy:P{setupEnemyId} depth:{depth} " +
            $"move:{decision.InputDescription}");
        return true;
    }

    private bool TryCastNow(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        out BattleModeComAbilityDecision decision)
    {
        decision = default;

        if (!TryGetEnemyTile(setupEnemyId, out Vector2Int enemyTile) ||
            !TryChooseCastDirection(myTile, enemyTile,
                out setupCastDirection, out setupLineLength, out _))
        {
            if (!TryFindCastPlan(settings, myTile, out CastPlan replacement) ||
                replacement.CastTile != myTile)
            {
                ResetSequence("target left cast zone");
                lastDecisionTrace = "cast target left zone";
                return false;
            }

            setupCastDirection = replacement.Direction;
            setupEnemyId = replacement.EnemyId;
            setupLineLength = replacement.LineLength;
            TryGetEnemyTile(setupEnemyId, out enemyTile);
        }

        BuildPlannedLineBlast(myTile, setupCastDirection, setupLineLength);
        if (!plannedBlastTiles.Contains(enemyTile))
        {
            ResetSequence("cast no longer threatens target");
            lastDecisionTrace = "cast target outside planned blast";
            return false;
        }

        if (!TryFindRetreatTile(settings, myTile, null,
                out _, out Vector2Int plannedRetreat, out int plannedRetreatDepth))
        {
            ResetSequence("no retreat after line");
            lastDecisionTrace = "cast no retreat after line";
            LogSurgical("CAST_ABORT_NO_RETREAT",
                $"my:{myTile} dir:{FirstMoveDescription(setupCastDirection)} len:{setupLineLength}",
                force: true);
            return false;
        }

        if (plannedRetreatDepth > settings.searchDepth)
        {
            ResetSequence("retreat too deep");
            lastDecisionTrace = "cast retreat too deep";
            LogSurgical("CAST_ABORT_RETREAT_DEEP",
                $"my:{myTile} retreat:{plannedRetreat} depth:{plannedRetreatDepth}",
                force: true);
            return false;
        }

        lineIsControl = HasControlBombs;
        SetSequenceState(SequenceState.Retreating);
        nextCastTime = Time.time + DifficultyCooldown(settings);
        castLockUntil = Time.time + CastLockSeconds;
        ClearRetreatStuckState();

        decision = new BattleModeComAbilityDecision
        {
            Action = BattleModeComActionType.CombatPlant,
            Weight = 4000,
            TargetTile = myTile + setupCastDirection * setupLineLength,
            HasTarget = true,
            FirstMove = TileDirectionToVector(setupCastDirection),
            Reason = $"purple-line cast at P{setupEnemyId}",
            InputDescription = AppendInput(FirstMoveDescription(setupCastDirection), "ActionC"),
            TapActionC = true
        };

        lastDecisionTrace =
            $"CAST dir:{setupCastDirection} len:{setupLineLength} enemy:P{setupEnemyId} control:{lineIsControl}";
        LogSurgical("CAST",
            $"my:{myTile} dir:{FirstMoveDescription(setupCastDirection)} len:{setupLineLength} " +
            $"enemy:P{setupEnemyId} enemyTile:{enemyTile} control:{lineIsControl} " +
            $"bombs:{bombController.BombsRemaining} plannedRetreat:{plannedRetreat} " +
            $"retreatDepth:{plannedRetreatDepth}",
            force: true);
        return true;
    }

    private bool TryFindCastPlan(
        BattleModeComDifficultySettings settings,
        Vector2Int start,
        out CastPlan bestPlan)
    {
        bestPlan = default;
        castPlans.Clear();

        int startNearestDistance = GetNearestEnemyDistance(start);
        // Limita a profundidade do tile de cast: planos distantes (depth 5-7)
        // quase nunca sobrevivem ao trajeto numa arena cheia de bombas e só
        // queimavam tentativas. Casts curtos (depth <= MaxSetupDepth) são os
        // que de fato chegam ao CAST nos logs.
        int maxDepth = Mathf.Clamp(settings.searchDepth, MinBombsForLine, MaxSetupDepth);

        searchVisited.Clear();
        searchOpen.Clear();
        searchVisited[start] = new SearchNode { Parent = start, Depth = 0 };
        searchOpen.Enqueue(start);

        while (searchOpen.Count > 0)
        {
            Vector2Int tile = searchOpen.Dequeue();
            SearchNode node = searchVisited[tile];

            GatherCastPlansAtTile(
                start,
                tile,
                node.Depth,
                startNearestDistance,
                castPlans);

            if (node.Depth >= maxDepth)
                continue;

            for (int i = 0; i < CardinalTiles.Length; i++)
            {
                Vector2Int next = tile + CardinalTiles[i];
                if (searchVisited.ContainsKey(next) ||
                    !IsWalkableTile(next, start) ||
                    IsDangerousAt(next, EstimateWalkSeconds(node.Depth + 1), settings))
                    continue;

                searchVisited[next] = new SearchNode { Parent = tile, Depth = node.Depth + 1 };
                searchOpen.Enqueue(next);
            }
        }

        castPlans.Sort((a, b) => a.Score.CompareTo(b.Score));
        for (int i = 0; i < castPlans.Count; i++)
        {
            CastPlan plan = castPlans[i];
            BuildPlannedLineBlast(plan.CastTile, plan.Direction, plan.LineLength);
            if (!TryFindRetreatTile(settings, plan.CastTile, null,
                    out _, out _, out int retreatDepth) ||
                retreatDepth > settings.searchDepth)
                continue;

            bestPlan = plan;
            return true;
        }

        plannedBlastTiles.Clear();
        plannedBombTiles.Clear();
        return false;
    }

    private void GatherCastPlansAtTile(
        Vector2Int start,
        Vector2Int castTile,
        int depth,
        int startNearestDistance,
        List<CastPlan> output)
    {
        if (depth > 0 && IsLivingPlayerAt(castTile))
            return;

        activePlayers.Clear();
        PlayerIdentity.GetActivePlayers(activePlayers);

        for (int i = 0; i < activePlayers.Count; i++)
        {
            PlayerIdentity player = activePlayers[i];
            if (!IsLivingEnemy(player))
                continue;

            Vector2Int enemyTile = WorldToTile(player.transform.position);
            int enemyDistance = Manhattan(castTile, enemyTile);

            // Se o alvo está colado, primeiro abre espaço. Isso evita gastar a
            // linha no tile atual e favorece o comportamento pedido de recuar,
            // virar para o adversário e então usar ActionC.
            if (startNearestDistance <= CloseEnemyDistance && depth == 0)
                continue;

            if (startNearestDistance <= CloseEnemyDistance &&
                enemyDistance <= startNearestDistance)
                continue;

            if (!TryChooseCastDirection(
                    castTile,
                    enemyTile,
                    out Vector2Int dir,
                    out int lineLength,
                    out int perpendicularOffset))
                continue;

            Vector2Int firstStep = depth > 0
                ? ReconstructFirstStep(start, castTile)
                : Vector2Int.zero;

            int preferredDistance = Mathf.Max(3, lineLength);
            int score =
                depth * 12 +
                perpendicularOffset * 8 +
                Mathf.Abs(enemyDistance - preferredDistance) * 2 -
                lineLength * 5;

            output.Add(new CastPlan
            {
                CastTile = castTile,
                Direction = dir,
                FirstStep = firstStep,
                EnemyId = player.playerId,
                EnemyDistance = enemyDistance,
                LineLength = lineLength,
                Depth = depth,
                Score = score
            });
        }
    }

    private bool TryChooseCastDirection(
        Vector2Int castTile,
        Vector2Int enemyTile,
        out Vector2Int bestDir,
        out int bestLineLength,
        out int bestPerpendicularOffset)
    {
        bestDir = Vector2Int.zero;
        bestLineLength = 0;
        bestPerpendicularOffset = int.MaxValue;

        Vector2Int delta = enemyTile - castTile;
        Vector2Int horizontal =
            delta.x > 0 ? Vector2Int.right :
            delta.x < 0 ? Vector2Int.left :
            Vector2Int.zero;
        Vector2Int vertical =
            delta.y > 0 ? Vector2Int.up :
            delta.y < 0 ? Vector2Int.down :
            Vector2Int.zero;

        EvaluateCastDirection(castTile, enemyTile, horizontal,
            ref bestDir, ref bestLineLength, ref bestPerpendicularOffset);
        EvaluateCastDirection(castTile, enemyTile, vertical,
            ref bestDir, ref bestLineLength, ref bestPerpendicularOffset);
        return bestDir != Vector2Int.zero;
    }

    private void EvaluateCastDirection(
        Vector2Int castTile,
        Vector2Int enemyTile,
        Vector2Int dir,
        ref Vector2Int bestDir,
        ref int bestLineLength,
        ref int bestPerpendicularOffset)
    {
        if (dir == Vector2Int.zero)
            return;

        int lineLength = CountPlaceableLine(castTile, dir);
        if (lineLength < MinBombsForLine)
            return;

        int radius = Mathf.Max(1, bombController.GetPlannedExplosionRadius());
        if (!DoesLineBlastThreaten(castTile, dir, lineLength, enemyTile, radius))
            return;

        int perpendicularOffset = dir.x != 0
            ? Mathf.Abs(enemyTile.y - castTile.y)
            : Mathf.Abs(enemyTile.x - castTile.x);

        if (bestDir != Vector2Int.zero &&
            (perpendicularOffset > bestPerpendicularOffset ||
             (perpendicularOffset == bestPerpendicularOffset && lineLength <= bestLineLength)))
            return;

        bestDir = dir;
        bestLineLength = lineLength;
        bestPerpendicularOffset = perpendicularOffset;
    }

    private int CountPlaceableLine(Vector2Int castTile, Vector2Int dir)
    {
        int maxBombs = bombController != null ? bombController.BombsRemaining : 0;
        int count = 0;
        for (int step = 1; step <= maxBombs; step++)
        {
            Vector2Int tile = castTile + dir * step;
            if (!CanPlaceBombAt(tile) || IsLivingPlayerAt(tile))
                break;

            count++;
        }

        return count;
    }

    private bool DoesLineBlastThreaten(
        Vector2Int castTile,
        Vector2Int dir,
        int lineLength,
        Vector2Int enemyTile,
        int radius)
    {
        for (int i = 1; i <= lineLength; i++)
        {
            if (IsTileInBlastLine(castTile + dir * i, enemyTile, radius))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Tile aceita plantio de bomba da linha (chão, sem blocos, sem bomba).
    /// </summary>
    private bool CanPlaceBombAt(Vector2Int tile)
    {
        if (!HasGroundTile(tile))
            return false;

        if (HasIndestructibleTile(tile) || HasDestructibleTile(tile))
            return false;

        if (FindBombAt(tile) != null)
            return false;

        return true;
    }

    /// <summary>
    /// Zona de blast combinada da linha planejada (cada bomba + raio em cruz).
    /// </summary>
    private void BuildPlannedLineBlast(Vector2Int myTile, Vector2Int dir, int lineLength)
    {
        plannedBlastTiles.Clear();
        plannedBombTiles.Clear();
        int radius = Mathf.Max(1, bombController != null ? bombController.GetPlannedExplosionRadius() : 2);

        for (int i = 1; i <= lineLength; i++)
        {
            Vector2Int bombTile = myTile + dir * i;
            plannedBombTiles.Add(bombTile);
            if (!plannedBlastTiles.Contains(bombTile))
                plannedBlastTiles.Add(bombTile);

            for (int d = 0; d < CardinalTiles.Length; d++)
            {
                for (int step = 1; step <= radius; step++)
                {
                    Vector2Int tile = bombTile + CardinalTiles[d] * step;
                    bool blocks = HasIndestructibleTile(tile) || HasDestructibleTile(tile);
                    if (!plannedBlastTiles.Contains(tile))
                        plannedBlastTiles.Add(tile);

                    if (blocks)
                        break;
                }
            }
        }
    }

    private bool HasRetreatTile(BattleModeComDifficultySettings settings, Vector2Int myTile)
    {
        return TryFindRetreatTile(settings, myTile, null, out _, out _, out _);
    }

    // =====================================================================
    // Recuo + detonação
    // =====================================================================
    private bool TryBuildRetreatDecision(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        out BattleModeComAbilityDecision decision)
    {
        decision = default;

        if (Time.time - sequenceStartedTime > RetreatTimeoutSeconds)
        {
            ResetSequence("retreat timeout");
            lastDecisionTrace = "retreat timeout";
            return false;
        }

        // Janela de lock do cast: o input está travado pelo DoCast e as bombas
        // podem ainda não existir nos tiles — não emite recuo nem stuck detection.
        if (Time.time < castLockUntil)
        {
            lastDecisionTrace = "retreat waiting cast lock";
            ClearRetreatStuckState();
            retreatLastTile = myTile;
            return false;
        }

        bool inPlannedZone = plannedBlastTiles.Contains(myTile);

        if (!inPlannedZone)
        {
            // Fora da zona. Linha de control: detona tudo com ActionB.
            if (lineIsControl)
            {
                bool enemyInZone = AnyEnemyInPlannedZone(out int enemyId);
                bool allyInZone = AnyAllyInPlannedZone();

                if (allyInZone)
                {
                    lastDecisionTrace = "retreat done, ally in zone — holding detonation";
                    return false;
                }

                decision = new BattleModeComAbilityDecision
                {
                    Action = BattleModeComActionType.CombatPlant,
                    Weight = 4000,
                    TargetTile = myTile,
                    HasTarget = true,
                    FirstMove = Vector2.zero,
                    Reason = enemyInZone
                        ? $"purple-line detonate hits P{enemyId}"
                        : "purple-line detonate",
                    InputDescription = "ActionB",
                    TapActionB = true
                };

                lastDecisionTrace = $"DETONATE enemyInZone:{enemyInZone}";
                LogSurgical("DETONATE",
                    $"my:{myTile} enemyInZone:{enemyInZone} zoneTiles:{plannedBlastTiles.Count}",
                    force: true);
                ResetSequence("detonated");
                return true;
            }

            // Linha normal: fuse resolve sozinho; sequência completa.
            LogSurgical("RETREAT_DONE", $"my:{myTile}");
            ResetSequence("retreat complete");
            lastDecisionTrace = "retreat complete (normal line)";
            return false;
        }

        // Ainda dentro da zona planejada: recua.
        UpdateRetreatStuckDetection(myTile);

        if (!TryFindRetreatTile(settings, myTile, retreatBlockedSteps,
                out Vector2 firstMove, out Vector2Int target, out int depth))
        {
            LogSurgical("RETREAT_FAILED", $"my:{myTile}", force: true);
            ResetSequence("retreat failed");
            lastDecisionTrace = "retreat failed";
            return false;
        }

        retreatLastAttemptedStep = Vector2Int.RoundToInt(firstMove);

        decision = new BattleModeComAbilityDecision
        {
            Action = BattleModeComActionType.Reposition,
            Weight = 300 + DifficultyWeight(settings),
            TargetTile = target,
            HasTarget = true,
            FirstMove = firstMove,
            Reason = "purple-line retreat",
            InputDescription = FirstMoveDescription(Vector2Int.RoundToInt(firstMove))
        };

        lastDecisionTrace = $"retreat target:{target} depth:{depth}";
        LogSurgical("RETREAT",
            $"my:{myTile} target:{target} move:{decision.InputDescription}");
        return true;
    }

    private bool TryFindRetreatTile(
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

        while (searchOpen.Count > 0)
        {
            Vector2Int tile = searchOpen.Dequeue();
            SearchNode node = searchVisited[tile];
            float eta = EstimateWalkSeconds(node.Depth);

            if (node.Depth > 0 &&
                !plannedBlastTiles.Contains(tile) &&
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

                // Tiles onde a linha planta bomba são intransponíveis mesmo que o
                // BFS rode antes de as bombas se registrarem fisicamente (era a
                // causa do recuo apontando PARA DENTRO da linha recém-plantada).
                if (plannedBombTiles.Contains(next))
                    continue;

                if (!IsWalkableTile(next, start))
                    continue;

                if (IsDangerousAt(next, EstimateWalkSeconds(node.Depth + 1), settings))
                    continue;

                searchVisited[next] = new SearchNode { Parent = tile, Depth = node.Depth + 1 };
                searchOpen.Enqueue(next);
            }
        }

        return false;
    }

    private bool TryFindPathToTile(
        BattleModeComDifficultySettings settings,
        Vector2Int start,
        Vector2Int goal,
        out Vector2 firstMove,
        out int resultDepth)
    {
        firstMove = Vector2.zero;
        resultDepth = 0;

        if (start == goal)
            return true;

        searchVisited.Clear();
        searchOpen.Clear();
        searchVisited[start] = new SearchNode { Parent = start, Depth = 0 };
        searchOpen.Enqueue(start);
        int maxDepth = Mathf.Max(4, settings.searchDepth + 3);

        while (searchOpen.Count > 0)
        {
            Vector2Int tile = searchOpen.Dequeue();
            SearchNode node = searchVisited[tile];
            if (node.Depth >= maxDepth)
                continue;

            for (int i = 0; i < CardinalTiles.Length; i++)
            {
                Vector2Int next = tile + CardinalTiles[i];
                if (searchVisited.ContainsKey(next) ||
                    !IsWalkableTile(next, start) ||
                    IsDangerousAt(next, EstimateWalkSeconds(node.Depth + 1), settings))
                    continue;

                searchVisited[next] = new SearchNode { Parent = tile, Depth = node.Depth + 1 };
                if (next == goal)
                {
                    resultDepth = node.Depth + 1;
                    firstMove = TileDirectionToVector(ReconstructFirstStep(start, next));
                    return firstMove != Vector2.zero;
                }

                searchOpen.Enqueue(next);
            }
        }

        return false;
    }

    private bool IsLivingEnemy(PlayerIdentity player)
    {
        if (player == null || player == identity || IsAlly(player.playerId))
            return false;

        return player.TryGetComponent<MovementController>(out var enemyMovement) &&
               enemyMovement != null &&
               !enemyMovement.isDead;
    }

    private bool TryGetEnemyTile(int enemyId, out Vector2Int enemyTile)
    {
        enemyTile = Vector2Int.zero;
        activePlayers.Clear();
        PlayerIdentity.GetActivePlayers(activePlayers);

        for (int i = 0; i < activePlayers.Count; i++)
        {
            PlayerIdentity player = activePlayers[i];
            if (player == null || player.playerId != enemyId || !IsLivingEnemy(player))
                continue;

            enemyTile = WorldToTile(player.transform.position);
            return true;
        }

        return false;
    }

    private int GetNearestEnemyDistance(Vector2Int tile)
    {
        int best = int.MaxValue;
        activePlayers.Clear();
        PlayerIdentity.GetActivePlayers(activePlayers);

        for (int i = 0; i < activePlayers.Count; i++)
        {
            PlayerIdentity player = activePlayers[i];
            if (!IsLivingEnemy(player))
                continue;

            best = Mathf.Min(best, Manhattan(tile, WorldToTile(player.transform.position)));
        }

        return best;
    }

    private bool IsLivingPlayerAt(Vector2Int tile)
    {
        occupancyPlayers.Clear();
        PlayerIdentity.GetActivePlayers(occupancyPlayers);

        for (int i = 0; i < occupancyPlayers.Count; i++)
        {
            PlayerIdentity player = occupancyPlayers[i];
            if (player == null ||
                !player.TryGetComponent<MovementController>(out var playerMovement) ||
                playerMovement == null ||
                playerMovement.isDead)
                continue;

            if (WorldToTile(player.transform.position) == tile)
                return true;
        }

        return false;
    }

    private bool AnyOwnActiveBomb()
    {
        foreach (Bomb bomb in Bomb.ActiveBombs)
        {
            if (bomb == null || bomb.HasExploded)
                continue;

            if (bomb.Owner == bombController)
                return true;
        }

        return false;
    }

    private bool AnyEnemyInPlannedZone(out int enemyId)
    {
        enemyId = 0;
        activePlayers.Clear();
        PlayerIdentity.GetActivePlayers(activePlayers);

        for (int i = 0; i < activePlayers.Count; i++)
        {
            PlayerIdentity player = activePlayers[i];
            if (player == null || player == identity)
                continue;

            if (IsAlly(player.playerId))
                continue;

            if (!player.TryGetComponent<MovementController>(out var enemyMovement) ||
                enemyMovement == null || enemyMovement.isDead)
                continue;

            if (plannedBlastTiles.Contains(WorldToTile(player.transform.position)))
            {
                enemyId = player.playerId;
                return true;
            }
        }

        return false;
    }

    private bool AnyAllyInPlannedZone()
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

            if (plannedBlastTiles.Contains(WorldToTile(player.transform.position)))
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
    // Estado / chance
    // =====================================================================
    private void SetSequenceState(SequenceState state)
    {
        sequenceStartedTime = Time.time;
        sequenceState = state;
    }

    private void ResetSequence(string reason)
    {
        if (sequenceState != SequenceState.None)
        {
            LogSurgical("SEQUENCE_RESET", reason, force: true);
            // Abortar um SETUP/recuo não deve gastar o cooldown cheio (esse é
            // exclusivo do CAST). Aplica só uma espera curta para evitar re-roll
            // no mesmo frame, mantendo a habilidade responsiva.
            nextCastTime = Mathf.Max(nextCastTime, Time.time + SetupAbortRetrySeconds);
        }

        sequenceState = SequenceState.None;
        sequenceStartedTime = Time.time;
        setupCastTile = Vector2Int.zero;
        setupCastDirection = Vector2Int.zero;
        setupEnemyId = 0;
        setupLineLength = 0;
        lineIsControl = false;
        plannedBlastTiles.Clear();
        plannedBombTiles.Clear();
        ClearRetreatStuckState();
        chanceCacheTime = -10f;
        chanceCacheResult = false;
    }

    private bool RollCastChance(BattleModeComDifficultySettings settings)
    {
        if (Time.time - chanceCacheTime < 0.001f)
            return chanceCacheResult;

        // A linha é a habilidade principal do Purple Louie. Mantém alguma
        // variação por dificuldade, mas deixa de ser um evento raro.
        float chance = settings.difficulty switch
        {
            BattleModeComputerLevel.Easy => 0.25f,
            BattleModeComputerLevel.Hard => 0.80f,
            _ => 0.55f
        };

        if (bombController != null && bombController.BombsRemaining <= 3)
            chance *= 0.75f;

        bool result = Random.value <= chance;
        chanceCacheTime = Time.time;
        chanceCacheResult = result;
        return result;
    }

    private static float DifficultyCooldown(BattleModeComDifficultySettings settings) =>
        settings.difficulty switch
        {
            BattleModeComputerLevel.Easy => 7.0f,
            BattleModeComputerLevel.Hard => 3.0f,
            _ => 5.0f
        };

    private static int DifficultyWeight(BattleModeComDifficultySettings settings) =>
        settings.difficulty switch
        {
            BattleModeComputerLevel.Easy => -20,
            BattleModeComputerLevel.Hard => 30,
            _ => 0
        };

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
    // Perigo / walkability
    // =====================================================================
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

            // As próprias control bombs só explodem com ActionB.
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
    // Tilemaps / utilitários
    // =====================================================================
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

    private float EstimateWalkSeconds(int depth)
    {
        if (movement == null)
            return depth * 0.25f;

        float tilesPerSecond = Mathf.Max(1f, movement.speed);
        return depth / tilesPerSecond;
    }

    private static Vector2 TileDirectionToVector(Vector2Int dir) => new Vector2(dir.x, dir.y);

    private static int Manhattan(Vector2Int a, Vector2Int b) =>
        Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

    private static string FirstMoveDescription(Vector2Int dir)
    {
        if (dir == Vector2Int.right) return "MoveRight";
        if (dir == Vector2Int.left) return "MoveLeft";
        if (dir == Vector2Int.up) return "MoveUp";
        if (dir == Vector2Int.down) return "MoveDown";
        return "none";
    }

    private static string FormatDanger(float seconds)
    {
        if (float.IsInfinity(seconds)) return "safe";
        if (seconds <= 0f) return "now";
        return $"{seconds:F2}";
    }

    private static string AppendInput(string existing, string input) =>
        string.IsNullOrEmpty(existing) || existing == "none" ? input : existing + "+" + input;

    private void LogSurgical(string key, string message, bool force = false)
    {
        if (!EnablePurpleLineDiagnostics) return;

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

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Ability de IA para usar o dash de empurrão do Black Louie
/// (BlackLouieDashPushAbility).
///
/// MECÂNICA (BlackLouieDashPushAbility.cs):
///   ActionC dispara um dash de até dashTiles (3) na direção do facing/movimento.
///   O PRIMEIRO bloqueador encontrado no caminho que seja adversário (Enemy ou
///   outro Player em battle mode) é EMPURRADO enemyPushTiles (2) tiles na mesma
///   direção e recebe stun (~0.5s). Bombas no caminho são chutadas; paredes /
///   destrutíveis interrompem o dash sem empurrão. O input fica travado durante
///   o dash (não dá para abortar no meio).
///
/// COMPORTAMENTO DA IA (ofensivo, candidate-only — nunca interfere na fuga):
///   1. ALINHAMENTO — procura um adversário exatamente na mesma linha/coluna,
///      a 1..dashTiles tiles, com o caminho livre (a IA precisa ser o primeiro a
///      bater nele, igual ao alvo do Red Louie stun, mas com alcance maior).
///   2. EMPURRÃO PARA O PERIGO — simula para onde o adversário seria empurrado
///      (até 2 tiles, parando em bloqueio) e só executa se esse tile de descanso
///      estiver dentro de uma zona de explosão iminente (GetDangerSeconds finito
///      e dentro da janela útil do stun).
///   3. SEGURANÇA PRÓPRIA — calcula a distância do dash e exige que o percurso e
///      o tile onde a IA para sejam seguros (igual ao Green Louie dash), para não
///      morrer junto com o alvo empurrado.
///
///   Frequência por dificuldade (chance e cooldown):
///     Easy   ~15% por oportunidade, cooldown 3.0s
///     Normal ~40% por oportunidade, cooldown 2.0s
///     Hard   ~75% por oportunidade, cooldown 1.2s
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerIdentity))]
[RequireComponent(typeof(MovementController))]
[RequireComponent(typeof(BombController))]
public sealed class BattleModeComBlackLouieDashPushAbility : MonoBehaviour, IBattleModeComAbility
{
    // === Filtro de diagnóstico ===
    public const int DiagnosticPlayerIdFilter = 0; // 0 = todos
    public static readonly bool EnableBlackLouiePushDiagnostics = false;
    private const float SurgicalLogIntervalSeconds = 0.35f;

    // === Constantes de comportamento (espelham BlackLouieDashPushAbility) ===
    // Alcance do dash da IA (dashTiles).
    private const int DashTiles = 3;
    // Quantos tiles o adversário é empurrado (enemyPushTiles).
    private const int EnemyPushTiles = 2;
    // Duração do stun aplicado ao alvo (enemyStunSeconds).
    private const float EnemyStunSeconds = 0.5f;
    // Velocidade do empurrão do alvo em tiles/seg (enemyPushTilesPerSecond).
    private const float EnemyPushTilesPerSecond = 14f;
    // Velocidade do dash em tiles/seg = dashTiles / dashMoveSeconds (3 / 0.5).
    private const float DashTilesPerSecond = 6f;
    // O empurrão só vale a pena se o tile de descanso do alvo explodir dentro
    // desta janela — fora dela o adversário se recupera do stun e foge a tempo.
    private const float MaxUsefulPushDangerSeconds = 1.25f;

    private static readonly Vector2Int[] CardinalTiles =
    {
        Vector2Int.up,
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.right
    };

    private struct SearchNode
    {
        public Vector2Int Parent;
        public int Depth;
    }

    // === Referências ===
    private PlayerIdentity identity;
    private MovementController movement;
    private BombController bombController;
    private AbilitySystem abilitySystem;
    private GameManager gameManager;
    private Tilemap groundTilemap;
    private Tilemap destructibleTilemap;
    private Tilemap indestructibleTilemap;
    private readonly List<PlayerIdentity> activePlayers = new List<PlayerIdentity>(6);
    private readonly Dictionary<Vector2Int, SearchNode> setupVisited = new();
    private readonly Queue<Vector2Int> setupOpen = new();
    private float tileSize = 1f;
    private int explosionMask;

    // === Estado ===
    private float nextAttemptTime = -10f;
    private Vector2Int committedSetupTile;
    private Vector2Int committedPushDirection;
    private int committedEnemyId;
    private float committedSetupUntil = -10f;

    // === Cache de chance (evita re-roll no mesmo ciclo de Think) ===
    private float chanceCacheTime = -10f;
    private bool chanceCacheResult;

    // === Diagnóstico ===
    private string lastDecisionTrace = "not evaluated";
    private float lastSurgicalLogTime = -10f;
    private string lastSurgicalLogKey = string.Empty;

    // === IBattleModeComAbility ===
    public string DiagnosticName => "BlackLouiePush";
    public string LastDecisionTrace => lastDecisionTrace;

    public bool IsAvailable
    {
        get
        {
            CacheReferences();
            if (identity == null || movement == null || movement.isDead)
                return false;

            return abilitySystem != null &&
                   abilitySystem.IsEnabled(BlackLouieDashPushAbility.AbilityId);
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

        if (movement != null)
            tileSize = Mathf.Max(0.01f, movement.tileSize);

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
    // Emergency — nunca interfere na fuga (push é candidate-only).
    // =====================================================================
    public bool TryBuildEmergencyDecision(
        BattleModeComDifficultySettings settings,
        BattleModeComController controller,
        Vector2Int myTile,
        float currentDangerSeconds,
        out BattleModeComAbilityDecision decision)
    {
        decision = default;
        lastDecisionTrace = "emergency: push is candidate-only";
        return false;
    }

    // =====================================================================
    // Candidate — empurrão ofensivo em direção ao perigo
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
                $"abilitySystem:{(abilitySystem != null)} " +
                $"enabled:{(abilitySystem != null && abilitySystem.IsEnabled(BlackLouieDashPushAbility.AbilityId))}");
            return false;
        }

        if (Time.time < nextAttemptTime)
        {
            float wait = nextAttemptTime - Time.time;
            lastDecisionTrace = $"candidate cooldown {wait:F2}s";
            LogSurgical("CANDIDATE_REJECT_COOLDOWN", $"my:{myTile} wait:{wait:F2}s");
            return false;
        }

        // Não tenta empurrar se o próprio tile está ameaçado — fugir vem primeiro.
        if (!float.IsInfinity(GetDangerSeconds(myTile)))
        {
            lastDecisionTrace = "candidate own tile threatened";
            LogSurgical("CANDIDATE_REJECT_TILE_THREATENED",
                $"my:{myTile} danger:{FormatDanger(GetDangerSeconds(myTile))}");
            return false;
        }

        if (!TryFindBestPush(settings, myTile,
                out Vector2Int dir, out int enemyId, out int dashLength,
                out Vector2Int enemyTile, out Vector2Int restTile, out float dangerAtRest,
                out string pushSearchTrace))
        {
            if (TryFindPushSetup(settings, myTile,
                    out Vector2Int setupTile, out Vector2Int firstStep,
                    out Vector2Int setupDirection, out int setupEnemyId,
                    out Vector2Int setupEnemyTile, out Vector2Int setupRestTile,
                    out float setupDanger, out int setupDepth, out string setupTrace))
            {
                decision = new BattleModeComAbilityDecision
                {
                    Action = BattleModeComActionType.Reposition,
                    Weight = 520 + DifficultyWeight(settings),
                    TargetTile = setupTile,
                    HasTarget = true,
                    FirstMove = new Vector2(firstStep.x, firstStep.y),
                    Reason = $"blacklouie setup push P{setupEnemyId} into danger",
                    InputDescription = FirstMoveDescription(firstStep)
                };

                lastDecisionTrace =
                    $"candidate SETUP tile:{setupTile} dir:{setupDirection} enemy:P{setupEnemyId} " +
                    $"rest:{setupRestTile} danger:{FormatDanger(setupDanger)} depth:{setupDepth}";
                committedSetupTile = setupTile;
                committedPushDirection = setupDirection;
                committedEnemyId = setupEnemyId;
                committedSetupUntil = Time.time + 2.5f;
                LogSurgical("CANDIDATE_SETUP_PUSH",
                    $"my:{myTile} setup:{setupTile} move:{FirstMoveDescription(firstStep)} " +
                    $"pushDir:{FirstMoveDescription(setupDirection)} enemy:P{setupEnemyId}@{setupEnemyTile} " +
                    $"rest:{setupRestTile} danger:{FormatDanger(setupDanger)} depth:{setupDepth} trace:{setupTrace}",
                    force: true);
                return true;
            }

            lastDecisionTrace = $"candidate no danger push: {pushSearchTrace}";
            LogSurgical("CANDIDATE_REJECT_NO_PUSH",
                $"my:{myTile} myWorld:{FormatWorld(movement.transform.position)} " +
                $"nearestEnemy:{FormatEnemyDistance(GetNearestEnemyDistance(myTile))} " +
                $"enemies:[{DescribeAdversaries(myTile)}] dirs:[{pushSearchTrace}] setup:[{setupTrace}]");
            return false;
        }

        // Frequência por dificuldade — só depois de já termos uma jogada válida.
        bool completesCommittedSetup =
            Time.time <= committedSetupUntil &&
            myTile == committedSetupTile &&
            dir == committedPushDirection &&
            enemyId == committedEnemyId;
        if (!completesCommittedSetup && !RollPushChance(settings))
        {
            lastDecisionTrace = "candidate chance fail";
            LogSurgical("CANDIDATE_REJECT_CHANCE",
                $"my:{myTile} chance:{DifficultyChance(settings):F2} diff:{settings.difficulty}");
            return false;
        }

        nextAttemptTime = Time.time + DifficultyCooldown(settings);
        committedSetupUntil = -10f;

        Vector2Int stopTile = myTile + dir * (dashLength - 1);

        decision = new BattleModeComAbilityDecision
        {
            Action = BattleModeComActionType.CombatPlant,
            Weight = 650 + DifficultyWeight(settings),
            TargetTile = restTile,
            HasTarget = true,
            // Vira para o adversário no mesmo frame do dash — o BlackLouie usa
            // movement.Direction/FacingDirection para definir a direção.
            FirstMove = new Vector2(dir.x, dir.y),
            Reason = $"blacklouie-push P{enemyId} into danger",
            InputDescription = AppendInput(FirstMoveDescription(dir), "ActionC"),
            TapActionC = true
        };

        lastDecisionTrace =
            $"candidate PUSH dir:{dir} enemy:P{enemyId} rest:{restTile} dangerRest:{FormatDanger(dangerAtRest)}";
        LogSurgical("CANDIDATE_READY_PUSH",
            $"my:{myTile} dir:{FirstMoveDescription(dir)} enemy:P{enemyId} enemyTile:{enemyTile} " +
            $"rest:{restTile} stop:{stopTile} dashLen:{dashLength} dangerRest:{FormatDanger(dangerAtRest)} " +
            $"committed:{completesCommittedSetup} chance:{DifficultyChance(settings):F2} " +
            $"cd:{DifficultyCooldown(settings):F1}s",
            force: true);
        return true;
    }

    // =====================================================================
    // Busca da melhor jogada de empurrão
    // =====================================================================

    /// <summary>
    /// Avalia as 4 direções. Em cada uma, anda tile a tile até o primeiro
    /// bloqueador; se for um adversário alinhado dentro do alcance do dash, e o
    /// percurso/parada da IA forem seguros, e o adversário puder ser empurrado
    /// para uma zona de perigo iminente, registra como candidata. Escolhe a que
    /// joga o alvo no perigo que explode mais cedo (mais letal durante o stun).
    /// </summary>
    private bool TryFindBestPush(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        out Vector2Int bestDir,
        out int bestEnemyId,
        out int bestDashLength,
        out Vector2Int bestEnemyTile,
        out Vector2Int bestRestTile,
        out float bestDangerAtRest,
        out string evaluationTrace)
    {
        bestDir = Vector2Int.zero;
        bestEnemyId = 0;
        bestDashLength = 0;
        bestEnemyTile = myTile;
        bestRestTile = myTile;
        bestDangerAtRest = float.PositiveInfinity;
        evaluationTrace = string.Empty;

        for (int d = 0; d < CardinalTiles.Length; d++)
        {
            Vector2Int dir = CardinalTiles[d];

            if (!TryEvaluateDirection(settings, myTile, dir, 0f,
                    out int enemyId, out int dashLength,
                    out Vector2Int enemyTile, out Vector2Int restTile, out float dangerAtRest,
                    out string directionTrace))
            {
                AppendDirectionTrace(ref evaluationTrace, dir, directionTrace);
                continue;
            }

            AppendDirectionTrace(ref evaluationTrace, dir, directionTrace);

            // Prefere o perigo que explode mais cedo (alvo ainda stunado/perto).
            if (dangerAtRest < bestDangerAtRest)
            {
                bestDir = dir;
                bestEnemyId = enemyId;
                bestDashLength = dashLength;
                bestEnemyTile = enemyTile;
                bestRestTile = restTile;
                bestDangerAtRest = dangerAtRest;
            }
        }

        return bestDir != Vector2Int.zero;
    }

    private bool TryEvaluateDirection(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        Vector2Int dir,
        float actionDelaySeconds,
        out int enemyId,
        out int dashLength,
        out Vector2Int enemyTile,
        out Vector2Int restTile,
        out float dangerAtRest,
        out string evaluationTrace)
    {
        enemyId = 0;
        dashLength = 0;
        enemyTile = myTile;
        restTile = myTile;
        dangerAtRest = float.PositiveInfinity;
        evaluationTrace = "not evaluated";

        // 1. Anda na direção até achar o primeiro bloqueador (espelha o dash):
        //    se for adversário, é o alvo; parede/destrutível/bomba sem passe
        //    interrompem o dash antes — então não há empurrão nessa direção.
        for (int step = 1; step <= DashTiles; step++)
        {
            Vector2Int tile = myTile + dir * step;

            if (TryGetLivingAdversaryAt(tile, out int foundId))
            {
                enemyId = foundId;
                enemyTile = tile;
                dashLength = step;
                break;
            }

            // Bloqueador não-adversário antes do alvo: o dash para aqui (sem push).
            if (DashBlockedAt(tile))
            {
                evaluationTrace =
                    $"blocked-before-target step:{step} tile:{tile} blocker:{DescribeDashBlocker(tile)}";
                return false;
            }

            // Tile vazio atravessado pela IA: precisa ser seguro na ETA do dash.
            float passEta = step / DashTilesPerSecond;
            if (IsDangerousAt(tile, actionDelaySeconds + passEta, settings))
            {
                evaluationTrace =
                    $"unsafe-corridor step:{step} tile:{tile} eta:{actionDelaySeconds + passEta:F2} " +
                    $"danger:{FormatDanger(GetDangerSeconds(tile))} reaction:{settings.dangerReactionSeconds:F2}";
                return false;
            }
        }

        if (enemyId == 0)
        {
            evaluationTrace = $"no-aligned-target range:1-{DashTiles}";
            return false; // nenhum adversário alinhado dentro do alcance
        }

        // 2. Segurança própria: a IA para no tile imediatamente antes do alvo.
        Vector2Int stopTile = myTile + dir * (dashLength - 1);
        float stopEta = Mathf.Max(0, dashLength - 1) / DashTilesPerSecond;
        float stopDanger = GetDangerSeconds(stopTile);
        if (!float.IsInfinity(stopDanger) ||
            IsDangerousAt(
                stopTile,
                actionDelaySeconds + stopEta + settings.safeTileMinimumSeconds,
                settings))
        {
            evaluationTrace =
                $"unsafe-stop enemy:P{enemyId}@{enemyTile} stop:{stopTile} eta:{stopEta:F2} " +
                $"danger:{FormatDanger(stopDanger)} minSafe:{settings.safeTileMinimumSeconds:F2}";
            return false;
        }

        // 3. Simula o empurrão do alvo: até EnemyPushTiles, parando em bloqueio.
        restTile = SimulatePush(enemyTile, dir, out int pushedTiles, out string pushBlocker);
        if (pushedTiles <= 0)
        {
            evaluationTrace =
                $"push-blocked enemy:P{enemyId}@{enemyTile} rest:{restTile} pushBlocker:{pushBlocker}";
            return false;
        }

        // 4. Só vale a pena se o tile de descanso explodir dentro da janela útil
        //    do stun (caso contrário o alvo se recupera e foge a tempo).
        dangerAtRest = GetDangerSeconds(restTile);
        if (float.IsInfinity(dangerAtRest))
        {
            evaluationTrace =
                $"rest-safe enemy:P{enemyId}@{enemyTile} pushed:{pushedTiles}/{EnemyPushTiles} " +
                $"rest:{restTile} pushBlocker:{pushBlocker}";
            return false;
        }

        float pushTravelSeconds = Manhattan(restTile, enemyTile) / EnemyPushTilesPerSecond;
        float usefulWindow = EnemyStunSeconds + pushTravelSeconds + MaxUsefulPushDangerSeconds;
        float impactEta = actionDelaySeconds + dashLength / DashTilesPerSecond;
        if (dangerAtRest + 0.12f < impactEta)
        {
            evaluationTrace =
                $"rest-danger-too-early enemy:P{enemyId}@{enemyTile} pushed:{pushedTiles}/{EnemyPushTiles} " +
                $"rest:{restTile} danger:{FormatDanger(dangerAtRest)} impactEta:{impactEta:F2} " +
                $"pushBlocker:{pushBlocker}";
            return false;
        }

        if (dangerAtRest > impactEta + usefulWindow)
        {
            evaluationTrace =
                $"rest-danger-too-late enemy:P{enemyId}@{enemyTile} pushed:{pushedTiles}/{EnemyPushTiles} " +
                $"rest:{restTile} danger:{FormatDanger(dangerAtRest)} max:{impactEta + usefulWindow:F2} " +
                $"pushBlocker:{pushBlocker}";
            return false;
        }

        evaluationTrace =
            $"valid enemy:P{enemyId}@{enemyTile} dash:{dashLength} pushed:{pushedTiles}/{EnemyPushTiles} " +
            $"rest:{restTile} danger:{FormatDanger(dangerAtRest)} pushBlocker:{pushBlocker}";
        return true;
    }

    // Procura um ponto de preparação atrás de um adversário para que o próximo
    // Think possa executar o dash e empurrá-lo na direção de uma explosão.
    private bool TryFindPushSetup(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        out Vector2Int bestSetupTile,
        out Vector2Int bestFirstStep,
        out Vector2Int bestPushDirection,
        out int bestEnemyId,
        out Vector2Int bestEnemyTile,
        out Vector2Int bestRestTile,
        out float bestDangerAtRest,
        out int bestDepth,
        out string evaluationTrace)
    {
        bestSetupTile = myTile;
        bestFirstStep = Vector2Int.zero;
        bestPushDirection = Vector2Int.zero;
        bestEnemyId = 0;
        bestEnemyTile = myTile;
        bestRestTile = myTile;
        bestDangerAtRest = float.PositiveInfinity;
        bestDepth = int.MaxValue;
        evaluationTrace = "no reachable setup";

        setupVisited.Clear();
        setupOpen.Clear();
        setupVisited[myTile] = new SearchNode { Parent = myTile, Depth = 0 };
        setupOpen.Enqueue(myTile);

        int maxDepth = Mathf.Clamp(settings.searchDepth, 2, 6);
        float bestScore = float.NegativeInfinity;
        int inspectedStances = 0;
        int timingRejected = 0;

        while (setupOpen.Count > 0)
        {
            Vector2Int tile = setupOpen.Dequeue();
            SearchNode node = setupVisited[tile];
            float walkEta = EstimateWalkSeconds(node.Depth);

            if (node.Depth > 0)
            {
                for (int d = 0; d < CardinalTiles.Length; d++)
                {
                    Vector2Int pushDirection = CardinalTiles[d];
                    if (!TryEvaluateDirection(settings, tile, pushDirection, walkEta,
                            out int enemyId, out int dashLength,
                            out Vector2Int enemyTile, out Vector2Int restTile,
                            out float dangerAtRest, out _))
                        continue;

                    inspectedStances++;
                    float impactEta = walkEta + dashLength / DashTilesPerSecond;
                    float timingTolerance = 0.12f;
                    if (dangerAtRest + timingTolerance < impactEta)
                    {
                        timingRejected++;
                        continue;
                    }

                    Vector2Int firstStep = ReconstructFirstStep(myTile, tile);
                    if (firstStep == Vector2Int.zero)
                        continue;

                    float score =
                        500f -
                        node.Depth * 30f -
                        Mathf.Abs(dangerAtRest - impactEta) * 20f;
                    if (score <= bestScore)
                        continue;

                    bestScore = score;
                    bestSetupTile = tile;
                    bestFirstStep = firstStep;
                    bestPushDirection = pushDirection;
                    bestEnemyId = enemyId;
                    bestEnemyTile = enemyTile;
                    bestRestTile = restTile;
                    bestDangerAtRest = dangerAtRest;
                    bestDepth = node.Depth;
                }
            }

            if (node.Depth >= maxDepth)
                continue;

            for (int d = 0; d < CardinalTiles.Length; d++)
            {
                Vector2Int next = tile + CardinalTiles[d];
                if (setupVisited.ContainsKey(next))
                    continue;

                int nextDepth = node.Depth + 1;
                float arrivalEta = EstimateWalkSeconds(nextDepth);
                if (!IsWalkableSetupTile(next, myTile) ||
                    IsDangerousAt(next, arrivalEta + settings.safeTileMinimumSeconds, settings))
                    continue;

                setupVisited[next] = new SearchNode { Parent = tile, Depth = nextDepth };
                setupOpen.Enqueue(next);
            }
        }

        if (bestEnemyId == 0)
        {
            evaluationTrace =
                $"none depth:{maxDepth} visited:{setupVisited.Count} " +
                $"validStances:{inspectedStances} timingRejected:{timingRejected}";
            return false;
        }

        evaluationTrace =
            $"found setup:{bestSetupTile} depth:{bestDepth} enemy:P{bestEnemyId}@{bestEnemyTile} " +
            $"pushDir:{DirectionLabel(bestPushDirection)} rest:{bestRestTile} " +
            $"danger:{FormatDanger(bestDangerAtRest)} visited:{setupVisited.Count}";
        return true;
    }

    private Vector2Int ReconstructFirstStep(Vector2Int start, Vector2Int target)
    {
        Vector2Int current = target;
        while (setupVisited.TryGetValue(current, out SearchNode node) &&
               node.Parent != start &&
               node.Parent != current)
        {
            current = node.Parent;
        }

        return current - start;
    }

    private bool IsWalkableSetupTile(Vector2Int tile, Vector2Int startTile)
    {
        if (!HasGroundTile(tile))
            return false;

        if (HasIndestructibleTile(tile))
            return false;

        if (HasDestructibleTile(tile) && !CanPassDestructibles)
            return false;

        if (FindBombAt(tile) != null && tile != startTile && !CanPassBombs)
            return false;

        if (tile != startTile && IsLivingPlayerAt(tile))
            return false;

        return true;
    }

    private float EstimateWalkSeconds(int depth)
    {
        float tilesPerSecond = movement != null ? Mathf.Max(1f, movement.speed) : 4f;
        return depth / tilesPerSecond;
    }

    /// <summary>
    /// Espelha o PushTargetRoutine/IsBlockedForTarget: empurra o alvo até
    /// EnemyPushTiles na direção, parando antes de um tile bloqueado.
    /// </summary>
    private Vector2Int SimulatePush(
        Vector2Int enemyTile,
        Vector2Int dir,
        out int pushedTiles,
        out string blocker)
    {
        Vector2Int rest = enemyTile;
        pushedTiles = 0;
        blocker = "none";
        for (int i = 0; i < EnemyPushTiles; i++)
        {
            Vector2Int next = rest + dir;
            if (PushBlockedAt(next))
            {
                blocker = $"{DescribePushBlocker(next)}@{next}";
                break;
            }

            rest = next;
            pushedTiles++;
        }

        return rest;
    }

    /// <summary>
    /// Tile onde o alvo NÃO pode ser empurrado (parede/indestrutível/destrutível/
    /// bomba/outro player vivo). Destrutíveis sempre bloqueiam o alvo (o passe é
    /// do dasher, não de quem é empurrado).
    /// </summary>
    private bool PushBlockedAt(Vector2Int tile)
    {
        if (!HasGroundTile(tile))
            return true;

        if (HasIndestructibleTile(tile) || HasDestructibleTile(tile))
            return true;

        if (FindBombAt(tile) != null)
            return true;

        if (IsLivingPlayerAt(tile))
            return true;

        return false;
    }

    // =====================================================================
    // Bloqueio do dash da própria IA (espelha o IsBlocked do dash)
    // =====================================================================
    private bool DashBlockedAt(Vector2Int tile)
    {
        if (!HasGroundTile(tile))
            return true;

        if (HasIndestructibleTile(tile))
            return true;

        if (HasDestructibleTile(tile) && !CanPassDestructibles)
            return true;

        if (FindBombAt(tile) != null && !CanPassBombs)
            return true;

        return false;
    }

    private string DescribeDashBlocker(Vector2Int tile)
    {
        if (!HasGroundTile(tile))
            return "no-ground";

        if (HasIndestructibleTile(tile))
            return "indestructible";

        if (HasDestructibleTile(tile) && !CanPassDestructibles)
            return "destructible";

        if (FindBombAt(tile) != null && !CanPassBombs)
            return "bomb";

        return "unknown";
    }

    private string DescribePushBlocker(Vector2Int tile)
    {
        if (!HasGroundTile(tile))
            return "no-ground";

        if (HasIndestructibleTile(tile))
            return "indestructible";

        if (HasDestructibleTile(tile))
            return "destructible";

        if (FindBombAt(tile) != null)
            return "bomb";

        if (IsLivingPlayerAt(tile))
            return "player";

        return "unknown";
    }

    // =====================================================================
    // Adversários
    // =====================================================================
    private bool TryGetLivingAdversaryAt(Vector2Int tile, out int playerId)
    {
        playerId = 0;
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

            if (WorldToTile(player.transform.position) == tile)
            {
                playerId = player.playerId;
                return true;
            }
        }

        return false;
    }

    private bool IsLivingPlayerAt(Vector2Int tile)
    {
        activePlayers.Clear();
        PlayerIdentity.GetActivePlayers(activePlayers);

        for (int i = 0; i < activePlayers.Count; i++)
        {
            PlayerIdentity player = activePlayers[i];
            if (player == null || player == identity)
                continue;

            if (!player.TryGetComponent<MovementController>(out var playerMovement) ||
                playerMovement == null || playerMovement.isDead)
                continue;

            if (WorldToTile(player.transform.position) == tile)
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
            if (player == null || player == identity || IsAlly(player.playerId))
                continue;

            if (!player.TryGetComponent<MovementController>(out var enemyMovement) ||
                enemyMovement == null || enemyMovement.isDead)
                continue;

            best = Mathf.Min(best, Manhattan(tile, WorldToTile(player.transform.position)));
        }

        return best;
    }

    private string DescribeAdversaries(Vector2Int myTile)
    {
        string description = string.Empty;
        Vector2 myWorld = movement != null ? movement.transform.position : TileToWorld(myTile);
        activePlayers.Clear();
        PlayerIdentity.GetActivePlayers(activePlayers);

        for (int i = 0; i < activePlayers.Count; i++)
        {
            PlayerIdentity player = activePlayers[i];
            if (player == null || player == identity || IsAlly(player.playerId))
                continue;

            if (!player.TryGetComponent<MovementController>(out var enemyMovement) ||
                enemyMovement == null || enemyMovement.isDead)
                continue;

            Vector2Int enemyTile = WorldToTile(player.transform.position);
            Vector2 enemyWorld = player.transform.position;
            Vector2Int delta = enemyTile - myTile;
            Vector2 worldOffset = enemyWorld - myWorld;
            bool aligned = delta.x == 0 || delta.y == 0;
            int distance = Manhattan(myTile, enemyTile);

            if (!string.IsNullOrEmpty(description))
                description += " ";

            description +=
                $"P{player.playerId}@{enemyTile} world:{FormatWorld(enemyWorld)} " +
                $"offset:{FormatWorld(worldOffset)} delta:{delta} dist:{distance} " +
                $"aligned:{aligned} inDash:{(aligned && distance >= 1 && distance <= DashTiles)}";
        }

        return string.IsNullOrEmpty(description) ? "none" : description;
    }

    private bool IsAlly(int otherPlayerId)
    {
        if (BattleModeRules.Instance == null || !BattleModeRules.Instance.UsesTeams || identity == null)
            return false;

        return BattleModeRules.Instance.GetTeamForPlayer(identity.playerId) ==
               BattleModeRules.Instance.GetTeamForPlayer(otherPlayerId);
    }

    // =====================================================================
    // Chance / cooldown por dificuldade
    // =====================================================================
    private bool RollPushChance(BattleModeComDifficultySettings settings)
    {
        if (Time.time - chanceCacheTime < 0.001f)
            return chanceCacheResult;

        bool result = Random.value <= DifficultyChance(settings);
        chanceCacheTime = Time.time;
        chanceCacheResult = result;
        return result;
    }

    private static float DifficultyChance(BattleModeComDifficultySettings settings) =>
        settings.difficulty switch
        {
            BattleModeComputerLevel.Easy => 0.15f,
            BattleModeComputerLevel.Hard => 0.75f,
            _ => 0.40f
        };

    private static float DifficultyCooldown(BattleModeComDifficultySettings settings) =>
        settings.difficulty switch
        {
            BattleModeComputerLevel.Easy => 3.0f,
            BattleModeComputerLevel.Hard => 1.2f,
            _ => 2.0f
        };

    private static int DifficultyWeight(BattleModeComDifficultySettings settings) =>
        settings.difficulty switch
        {
            BattleModeComputerLevel.Easy => -20,
            BattleModeComputerLevel.Hard => 30,
            _ => 0
        };

    // =====================================================================
    // Perigo (modelo padrão, igual Green/Red Louie)
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

    private static int Manhattan(Vector2Int a, Vector2Int b) =>
        Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

    private static string FormatEnemyDistance(int distance) =>
        distance == int.MaxValue ? "none" : distance.ToString();

    private static string FormatWorld(Vector2 world) =>
        $"({world.x:F2},{world.y:F2})";

    private static void AppendDirectionTrace(
        ref string trace,
        Vector2Int direction,
        string directionTrace)
    {
        if (!string.IsNullOrEmpty(trace))
            trace += " | ";

        trace += $"{DirectionLabel(direction)}:{directionTrace}";
    }

    private static string DirectionLabel(Vector2Int dir)
    {
        if (dir == Vector2Int.right) return "R";
        if (dir == Vector2Int.left) return "L";
        if (dir == Vector2Int.up) return "U";
        if (dir == Vector2Int.down) return "D";
        return "?";
    }

    private static string FirstMoveDescription(Vector2Int dir)
    {
        if (dir == Vector2Int.right) return "MoveRight";
        if (dir == Vector2Int.left) return "MoveLeft";
        if (dir == Vector2Int.up) return "MoveUp";
        if (dir == Vector2Int.down) return "MoveDown";
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
        if (!EnableBlackLouiePushDiagnostics) return;

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

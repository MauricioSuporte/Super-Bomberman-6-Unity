using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerIdentity))]
[RequireComponent(typeof(MovementController))]
[RequireComponent(typeof(BombController))]
public class BattleModeComKickBombAbility : MonoBehaviour, IBattleModeComAbility
{
    // Filtro de diagnóstico: só emite logs [BattleCOMKick] para esta IA (playerId).
    // Use 0 para logar TODAS as IAs. Ajuste conforme qual COM você quer inspecionar.
    public const int DiagnosticPlayerIdFilter = 5;
    public static readonly bool EnableKickBombLoadDiagnostics = false;

    private const float OffensiveSequenceCooldownSeconds = 1.1f;
    private const float ActionRStopCooldownSeconds = 0.7f;
    private const int MaxOffensiveKickDistance = 8;
    private const int MaxKickSequenceDistanceFromBomb = 3;
    private const int MaxSequentialKickBombs = 2;
    private const float RepeatPlantOriginWaitSeconds = 0.6f;
    private const float RepeatReturnBlockedWaitSeconds = 1.0f;
    private const float ForceActionRStopAfterKickSeconds = 1.35f;
    private const float PostKickEscapeSeconds = 1.8f;
    private const float MinimumOwnTriggerKickFuseSeconds = 0.7f;
    private const float DirectKickRetrySeconds = 0.9f;
    private const float DirectKickRetryMinFuseSeconds = 0.65f;
    private const float DirectKickRetryDangerBufferSeconds = 0.05f;
    private const float ReturnToKickStuckTimeoutSeconds = 0.2f;
    private static readonly bool EnableKickBombSurgicalDiagnostics = false;
    private static readonly bool EnableKickBombRiskDiagnostics = false;
    private const float SurgicalLogIntervalSeconds = 0.35f;

    private static readonly Vector2Int[] CardinalTiles =
    {
        Vector2Int.up,
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.right
    };

    private enum SequenceState
    {
        None,
        RetreatAfterPlant,
        ReturnToKick,
        ReturnToRepeatPlant,
        EscapeAfterKick
    }

    private struct EscapeSearchNode
    {
        public Vector2Int Parent;
        public int Depth;
    }

    private PlayerIdentity identity;
    private MovementController movement;
    private BombController bombController;
    private BombKickAbility kickAbility;
    private GameManager gameManager;
    private Tilemap groundTilemap;
    private Tilemap destructibleTilemap;
    private Tilemap indestructibleTilemap;
    private ContactFilter2D obstacleFilter;
    private Collider2D[] ownColliders;
    private readonly Collider2D[] obstacleHits = new Collider2D[12];
    private int explosionMask;
    private float tileSize = 1f;
    private float nextOffensiveSequenceTime;
    private float lastActionRStopTime = -10f;
    private SequenceState sequenceState;
    private Vector2Int sequenceBombTile;
    private Vector2Int sequenceRetreatTile;
    private Vector2Int sequenceKickDirection;
    private int sequenceKicksCompleted;
    private int sequenceTargetKickCount = 1;
    private float sequenceStateStartedTime = -10f;
    private float forceActionRStopUntil = -10f;
    private float sequenceDirectKickRetryUntil = -10f;
    private float postKickEscapeUntil = -10f;
    private Vector2Int postKickEscapeLastTile;
    private float postKickEscapeStuckSince = -10f;
    private Vector2Int postKickEscapeLastAttemptedStep;
    private readonly List<Vector2Int> postKickEscapeBlockedSteps = new List<Vector2Int>(4);
    private Bomb sequenceTrackedBomb;
    private bool sequencePlantDirectionPatched;
    private bool sequenceAllowActionRStop;
    protected string lastDecisionTrace = "not evaluated";
    private float lastSurgicalLogTime = -10f;
    private string lastSurgicalLogKey = string.Empty;

    // Cache do roll de chance ofensivo para evitar que múltiplas chamadas
    // dentro do mesmo ciclo de Think() re-rolem independentemente e aumentem
    // a probabilidade efetiva além do pretendido (ex.: 50% × 50% = 75%).
    private float offensiveTriggerChanceCacheTime = -10f;
    private bool offensiveTriggerChanceCacheResult;
    private readonly Queue<Vector2Int> pathCheckOpen = new();
    private readonly HashSet<Vector2Int> pathCheckVisited = new();
    private readonly Queue<Vector2Int> escapeOpen = new();
    private readonly Dictionary<Vector2Int, EscapeSearchNode> escapeVisited = new();

    public virtual string DiagnosticName => "KickBomb";
    public string LastDecisionTrace => lastDecisionTrace;

    public virtual bool IsAvailable
    {
        get
        {
            CacheReferences();
            return IsKickAbilityEnabled && !ShouldDeferToMountedYellowLouieKick();
        }
    }

    protected MovementController Movement => movement;
    protected BombController BombController => bombController;
    protected float TileSize => tileSize;

    protected virtual bool IsKickAbilityEnabled => kickAbility != null && kickAbility.IsEnabled;
    protected virtual bool CanUseActionRStop => true;

    private void Awake()
    {
        CacheReferences();
    }

    private void OnEnable()
    {
        CacheReferences();
    }

    protected void CacheReferences()
    {
        if (identity == null)
            TryGetComponent(out identity);

        if (movement == null)
            TryGetComponent(out movement);

        if (bombController == null)
            TryGetComponent(out bombController);

        if (kickAbility == null)
            TryGetComponent(out kickAbility);

        ownColliders = GetComponentsInChildren<Collider2D>(true);

        if (movement != null)
        {
            tileSize = Mathf.Max(0.01f, movement.tileSize);
            obstacleFilter = new ContactFilter2D
            {
                useLayerMask = true,
                useTriggers = true
            };
            obstacleFilter.SetLayerMask(movement.obstacleMask);
        }

        gameManager = GameManager.Instance != null ? GameManager.Instance : FindAnyObjectByType<GameManager>();
        if (gameManager != null)
        {
            groundTilemap = gameManager.groundTilemap;
            destructibleTilemap = gameManager.destructibleTilemap;
            indestructibleTilemap = gameManager.indestructibleTilemap;
        }

        explosionMask = LayerMask.GetMask("Explosion");

        CacheExtraReferences();
    }

    protected virtual void CacheExtraReferences()
    {
    }

    public virtual bool TryBuildEmergencyDecision(
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
            lastDecisionTrace = BuildAvailabilityTrace("emergency unavailable");
            return false;
        }

        // Jogada ofensiva já em andamento: mesmo sob perigo da própria bomba recém-plantada,
        // priorize CONCLUIR a sequência em vez de fugir. Ao recuar 1 tile a IA fica dentro do
        // raio da própria bomba (inDanger=true) e o controller normalmente entraria em fuga,
        // abandonando o chute. Aqui reproduzimos o gesto gravado do humano: recuar 1 tile (a
        // bomba solidifica) e voltar para chutá-la no adversário.
        if (sequenceState != SequenceState.None &&
            TryContinueOffensiveSequence(settings, myTile, out decision))
        {
            lastDecisionTrace = "emergency continue offensive sequence -> " + lastDecisionTrace;
            return true;
        }

        if (CanUseActionRStop &&
            Time.time < forceActionRStopUntil &&
            TryBuildActionRStopDecision(settings, myTile, out decision, forceWhenGood: true))
        {
            return true;
        }

        if (TryBuildCustomEmergencyDecision(settings, myTile, currentDangerSeconds, out decision))
            return true;

        if (TryBuildOwnTriggerBombKickDecision(settings, myTile, out decision, forceChance: true))
            return true;

        string rejectedDirections = string.Empty;

        for (int i = 0; i < CardinalTiles.Length; i++)
        {
            Vector2Int dir = CardinalTiles[i];
            Bomb bomb = FindBombAt(myTile + dir);
            if (!CanKick(bomb))
            {
                Bomb nearBomb = FindBombAt(myTile + dir * 2);
                if (CanKick(nearBomb) &&
                    IsWalkableTile(myTile + dir, myTile) &&
                    IsKickLaneOpen(myTile + dir * 2, dir, 1))
                {
                    Vector2Int standTile = myTile + dir;
                    Vector2Int bombTile = myTile + dir * 2;
                    Vector2Int approachBombDestination = myTile + dir * 3;
                    decision = new BattleModeComAbilityDecision
                    {
                        Action = BattleModeComActionType.KickBomb,
                        Weight = 200 + DifficultyWeight(settings),
                        TargetTile = bombTile,
                        HasTarget = true,
                        FirstMove = TileDirectionToVector(dir),
                        Reason = $"approach kick bomb at {bombTile} toward {approachBombDestination}",
                        InputDescription = FirstMoveDescription(TileDirectionToVector(dir)),
                        UsesEscapeAbilityChance = true
                    };
                    lastDecisionTrace = $"emergency approach selected dir {dir} stand {standTile} bomb {bombTile} dest {approachBombDestination}";
                    return true;
                }

                AppendTracePart(ref rejectedDirections, $"{dir}:no adjacent kickable bomb");
                continue;
            }

            Vector2Int bombDestination = myTile + dir * 2;
            if (!IsKickLaneOpen(myTile + dir, dir, 1))
            {
                AppendTracePart(ref rejectedDirections, $"{dir}:lane blocked");
                continue;
            }

            Vector2Int escapeTile = myTile - dir;
            if (!IsSafeWalkable(escapeTile, myTile, settings, 1))
                escapeTile = FindBestEscapeAfterKick(myTile, dir, settings);

            bool escapesThroughVacatedBombTile = false;
            if (escapeTile == myTile && IsSafeAfterKickingBomb(myTile + dir, bomb, settings, 1))
            {
                escapeTile = myTile + dir;
                escapesThroughVacatedBombTile = true;
            }

            if (escapeTile == myTile)
            {
                AppendTracePart(ref rejectedDirections, $"{dir}:no escape after kick");
                continue;
            }

            if (TryBuildKickActivationInputDecision(
                    settings,
                    myTile,
                    bomb,
                    myTile + dir,
                    dir,
                    240 + DifficultyWeight(settings),
                    $"activate emergency kick bomb toward {bombDestination}",
                    out decision))
            {
                lastDecisionTrace =
                    $"emergency input selected dir {dir} bomb {myTile + dir} dest {bombDestination} escape {escapeTile}" +
                    (escapesThroughVacatedBombTile ? " via vacated bomb tile" : string.Empty);
                decision.UsesEscapeAbilityChance = true;
                return true;
            }

            decision = new BattleModeComAbilityDecision
            {
                Action = BattleModeComActionType.KickBomb,
                Weight = 220 + DifficultyWeight(settings),
                TargetTile = bombDestination,
                HasTarget = true,
                FirstMove = TileDirectionToVector(dir),
                Reason = $"kick escape bomb toward {bombDestination}",
                InputDescription = FirstMoveDescription(TileDirectionToVector(dir)),
                UsesEscapeAbilityChance = true
            };
            lastDecisionTrace =
                $"emergency selected dir {dir} bomb {myTile + dir} dest {bombDestination} escape {escapeTile}" +
                (escapesThroughVacatedBombTile ? " via vacated bomb tile" : string.Empty);
            return true;
        }

        lastDecisionTrace = string.IsNullOrEmpty(rejectedDirections)
            ? $"emergency no adjacent kick options nearby:{DescribeNearbyBombs(myTile)}"
            : $"emergency rejected {rejectedDirections} nearby:{DescribeNearbyBombs(myTile)}";
        return false;
    }

    public virtual bool TryBuildCandidateDecision(
        BattleModeComDifficultySettings settings,
        BattleModeComController controller,
        Vector2Int myTile,
        out BattleModeComAbilityDecision decision)
    {
        decision = default;
        lastDecisionTrace = "candidate start";

        if (!IsAvailable)
        {
            lastDecisionTrace = BuildAvailabilityTrace("candidate unavailable");
            return false;
        }

        if (sequenceState != SequenceState.None &&
            TryContinueOffensiveSequence(settings, myTile, out decision))
            return true;

        if (CanUseActionRStop &&
            TryBuildActionRStopDecision(
                settings,
                myTile,
                out decision,
                forceWhenGood: Time.time < forceActionRStopUntil))
        {
            return true;
        }

        if (TryBuildOwnTriggerBombKickDecision(settings, myTile, out decision))
            return true;

        if (Time.time < nextOffensiveSequenceTime)
        {
            lastDecisionTrace = $"candidate cooldown {(nextOffensiveSequenceTime - Time.time):F2}s";
            return false;
        }

        // Chute ofensivo é uma jogada ocasional, não automática. Mesmo quando há alvo
        // alinhado e lane aberta, a IA só executa conforme a chance por dificuldade.
        float chance = DifficultyChance(settings, 0.10f, 0.25f, 0.50f);
        if (Random.value > chance)
        {
            lastDecisionTrace = $"candidate chance failed chance {chance:F2}";
            return false;
        }

        if (bombController == null || bombController.BombsRemaining <= 0)
        {
            lastDecisionTrace = $"candidate no bombs remaining bc:{(bombController != null)}";
            return false;
        }

        if (!TryFindNearestEnemyAligned(myTile, out PlayerIdentity target, out Vector2Int targetTile, out Vector2Int kickDir))
        {
            lastDecisionTrace = "candidate no aligned target with open lane";
            return false;
        }

        if (!CanPlantBombForKick(myTile, kickDir, settings, out Vector2Int retreatTile))
        {
            lastDecisionTrace = $"candidate cannot plant/retreat dir {kickDir} target P{target.playerId}@{targetTile}";
            return false;
        }

        // Verifica se o caminho da bomba após o chute está livre de explosões ativas
        // ou de bombas com fuse iminente cujo raio alcance o lane. Sem essa verificação
        // a IA pode chutar uma bomba para dentro de uma explosão em andamento, criando
        // uma reação em cadeia que a mata.
        int laneCheckDist = Manhattan(myTile, targetTile);
        if (!IsKickLaneSafeFromImminentExplosions(myTile + kickDir, kickDir, laneCheckDist))
        {
            lastDecisionTrace = $"candidate kick lane unsafe imminent explosion dir {kickDir} target P{target.playerId}@{targetTile}";
            LogKickSurgical(
                "CANDIDATE_LANE_UNSAFE",
                $"dir:{kickDir} target:P{target.playerId}@{targetTile} laneCheckDist:{laneCheckDist}",
                true);
            return false;
        }

        SetSequenceState(SequenceState.RetreatAfterPlant);
        ConfigureNewKickSequencePlan(settings, firstBombAlreadyPlaced: false);
        sequenceBombTile = myTile;
        sequenceRetreatTile = retreatTile;
        sequenceKickDirection = kickDir;
        nextOffensiveSequenceTime = Time.time + OffensiveSequenceCooldownSeconds;

        Vector2 retreatMove = TileDirectionToVector(retreatTile - myTile);
        decision = new BattleModeComAbilityDecision
        {
            Action = BattleModeComActionType.KickBomb,
            Weight = Mathf.Max(1, settings.combatPlantWeight + 90 + DifficultyWeight(settings)),
            TargetTile = targetTile,
            HasTarget = true,
            FirstMove = retreatMove,
            Reason = $"plant kick bomb toward P{target.playerId}",
            InputDescription = AppendInput("ActionA", FirstMoveDescription(retreatMove)),
            TapBomb = true
        };
        lastDecisionTrace = $"candidate selected plant target P{target.playerId}@{targetTile} dir {kickDir} retreat {retreatTile}";
        LogKickSurgical(
            "PLAN",
            $"bomb:{myTile} stand:{retreatTile} target:P{target.playerId}@{targetTile} dir:{kickDir} move:{FirstMoveDescription(retreatMove)} targetKicks:{sequenceTargetKickCount} actionR:{sequenceAllowActionRStop} nearby:{DescribeNearbyBombs(myTile)}",
            true);
        return true;
    }

    private bool TryContinueOffensiveSequence(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        out BattleModeComAbilityDecision decision)
    {
        decision = default;

        if (sequenceState == SequenceState.None)
        {
            lastDecisionTrace = "sequence none";
            return false;
        }

        if (sequenceState == SequenceState.EscapeAfterKick)
            return TryContinuePostKickEscape(settings, myTile, out decision);

        if (sequenceState == SequenceState.ReturnToRepeatPlant)
            return TryContinueRepeatPlantSequence(settings, myTile, out decision);

        Bomb bomb = FindBombAt(sequenceBombTile);
        if ((bomb == null || bomb.HasExploded || bomb.Owner != bombController) &&
            IsKickActivationComplete(sequenceTrackedBomb, sequenceBombTile))
        {
            bomb = sequenceTrackedBomb;
        }

        if (bomb == null || bomb.HasExploded || bomb.Owner != bombController)
        {
            lastDecisionTrace = $"sequence cancelled bomb missing/exploded/owner tile {sequenceBombTile}";
            LogKickSurgical(
                "CANCEL_MISSING",
                $"bomb:{sequenceBombTile} my:{myTile} nearby:{DescribeNearbyBombs(myTile)}",
                true);
            ResetOffensiveSequence();
            return false;
        }

        EnsureSequenceBombPlantDirection(bomb);

        if (sequenceState == SequenceState.RetreatAfterPlant)
        {
            if (myTile != sequenceRetreatTile)
            {
                Vector2Int dir = StepToward(myTile, sequenceRetreatTile);
                if (dir == Vector2Int.zero || !IsWalkableTile(myTile + dir, myTile))
                {
                    lastDecisionTrace = $"sequence retreat blocked from {myTile} to {sequenceRetreatTile}";
                    LogKickSurgical(
                        "RETREAT_BLOCKED",
                        $"from:{myTile} stand:{sequenceRetreatTile} dir:{dir} bomb:{DescribeBomb(bomb)} dangerHere:{FormatDanger(GetDangerSeconds(myTile, bomb))} dangerStand:{FormatDanger(GetDangerSeconds(sequenceRetreatTile, bomb))} nearby:{DescribeNearbyBombs(myTile)}",
                        true);
                    ResetOffensiveSequence();
                    return false;
                }

                decision = new BattleModeComAbilityDecision
                {
                    Action = BattleModeComActionType.KickBomb,
                    Weight = 170 + DifficultyWeight(settings),
                    TargetTile = sequenceRetreatTile,
                    HasTarget = true,
                    FirstMove = TileDirectionToVector(dir),
                    Reason = "retreat before kick",
                    InputDescription = FirstMoveDescription(TileDirectionToVector(dir))
                };
                lastDecisionTrace = $"sequence retreat toward {sequenceRetreatTile} move {dir}";
                LogKickSurgical(
                    "RETREAT",
                    $"from:{myTile} stand:{sequenceRetreatTile} move:{dir} bomb:{DescribeBomb(bomb)} dangerStand:{FormatDanger(GetDangerSeconds(sequenceRetreatTile, bomb))}");
                return true;
            }

            SetSequenceState(SequenceState.ReturnToKick);
        }

        if (sequenceState == SequenceState.ReturnToKick)
        {
            Vector2Int kickStandTile = sequenceBombTile - sequenceKickDirection;

            if (Manhattan(myTile, sequenceBombTile) > MaxKickSequenceDistanceFromBomb)
            {
                lastDecisionTrace =
                    $"sequence cancelled too far from bomb tile {sequenceBombTile} stand {kickStandTile}";
                LogKickSurgical(
                    "CANCEL_TOO_FAR",
                    $"my:{myTile} bomb:{DescribeBomb(bomb)} stand:{kickStandTile} distance:{Manhattan(myTile, sequenceBombTile)} nearby:{DescribeNearbyBombs(myTile)}",
                    true);
                ResetOffensiveSequence();
                return false;
            }

            if (Time.time - sequenceStateStartedTime > ReturnToKickStuckTimeoutSeconds &&
                !IsKickActivationComplete(bomb, sequenceBombTile))
            {
                lastDecisionTrace =
                    $"sequence cancelled return/kick timeout bomb {sequenceBombTile} stand {kickStandTile}";
                LogKickSurgical(
                    "RETURN_TO_KICK_TIMEOUT",
                    $"my:{myTile} bomb:{DescribeBomb(bomb)} stand:{kickStandTile} elapsed:{Time.time - sequenceStateStartedTime:F2}s nearby:{DescribeNearbyBombs(myTile)}",
                    true);
                ResetOffensiveSequence();
                return false;
            }

            if (IsKickActivationComplete(bomb, sequenceBombTile))
            {
                bool repeatArmed = TryArmRepeatKickAfterSuccessfulKick(myTile, bomb, kickStandTile);
                bool postKickEscapeArmed = false;
                Vector2 postKickEscapeMove = Vector2.zero;
                Vector2Int postKickEscapeTarget = kickStandTile;
                string postKickEscapeRoute = string.Empty;
                lastDecisionTrace = $"sequence completed bomb already moving from {sequenceBombTile}";
                LogKickSurgical(
                    "KICK_CONFIRMED",
                    $"my:{myTile} bomb:{DescribeBomb(bomb)} stand:{kickStandTile} repeat:{repeatArmed} kicks:{sequenceKicksCompleted}/{sequenceTargetKickCount} actionR:{sequenceAllowActionRStop}",
                    true);

                if (!repeatArmed)
                {
                    postKickEscapeArmed = ArmPostKickEscapeIfNeeded(
                        settings,
                        myTile,
                        out postKickEscapeMove,
                        out postKickEscapeTarget,
                        out postKickEscapeRoute);

                    if (!postKickEscapeArmed)
                        ResetOffensiveSequence();

                    nextOffensiveSequenceTime = Time.time + OffensiveSequenceCooldownSeconds;
                }

                decision = new BattleModeComAbilityDecision
                {
                    Action = BattleModeComActionType.KickBomb,
                    Weight = 220 + DifficultyWeight(settings),
                    TargetTile = postKickEscapeArmed ? postKickEscapeTarget : repeatArmed ? sequenceBombTile : kickStandTile,
                    HasTarget = true,
                    FirstMove = postKickEscapeMove,
                    Reason = postKickEscapeArmed
                        ? "kick confirmed and escape"
                        : repeatArmed ? "prepare repeat kick bomb" : "kick confirmed",
                    InputDescription = postKickEscapeArmed
                        ? FirstMoveDescription(postKickEscapeMove)
                        : "none"
                };
                if (postKickEscapeArmed)
                {
                    LogKickSurgical(
                        "POST_KICK_ESCAPE_ARMED",
                        $"my:{myTile} target:{postKickEscapeTarget} move:{FirstMoveDescription(postKickEscapeMove)} route:{postKickEscapeRoute} dangerHere:{FormatDanger(GetDangerSeconds(myTile))} nearby:{DescribeNearbyBombs(myTile)}",
                        true);
                }

                return true;
            }

            if (myTile == sequenceBombTile)
            {
                Vector2Int recoverDir = -sequenceKickDirection;
                if (!IsWalkableTile(myTile + recoverDir, myTile))
                {
                    lastDecisionTrace = $"sequence recover blocked from bomb tile {sequenceBombTile} move {recoverDir}";
                    LogKickSurgical(
                        "RECOVER_BLOCKED",
                        $"my:{myTile} stand:{kickStandTile} move:{recoverDir} bomb:{DescribeBomb(bomb)} dangerHere:{FormatDanger(GetDangerSeconds(myTile, bomb))} dangerStand:{FormatDanger(GetDangerSeconds(kickStandTile, bomb))} nearby:{DescribeNearbyBombs(myTile)}",
                        true);
                    ResetOffensiveSequence();
                    return false;
                }

                decision = new BattleModeComAbilityDecision
                {
                    Action = BattleModeComActionType.KickBomb,
                    Weight = 210 + DifficultyWeight(settings),
                    TargetTile = kickStandTile,
                    HasTarget = true,
                    FirstMove = TileDirectionToVector(recoverDir),
                    Reason = "recover kick stance",
                    InputDescription = FirstMoveDescription(TileDirectionToVector(recoverDir))
                };
                lastDecisionTrace = $"sequence recover from bomb tile {sequenceBombTile} toward stand {kickStandTile} move {recoverDir}";
                LogKickSurgical(
                    "ON_BOMB_TILE",
                    $"my:{myTile} stand:{kickStandTile} move:{recoverDir} bomb:{DescribeBomb(bomb)} dangerHere:{FormatDanger(GetDangerSeconds(myTile, bomb))} dangerStand:{FormatDanger(GetDangerSeconds(kickStandTile, bomb))} nearby:{DescribeNearbyBombs(myTile)}");
                return true;
            }

            Vector2Int dir = myTile == kickStandTile
                ? sequenceKickDirection
                : StepToward(myTile, kickStandTile);

            if (dir == Vector2Int.zero)
            {
                lastDecisionTrace = $"sequence return no dir from {myTile} to {kickStandTile}";
                LogKickSurgical(
                    "RETURN_NO_DIR",
                    $"my:{myTile} stand:{kickStandTile} bomb:{DescribeBomb(bomb)} nearby:{DescribeNearbyBombs(myTile)}",
                    true);
                ResetOffensiveSequence();
                return false;
            }

            if (myTile != kickStandTile && myTile + dir == sequenceBombTile)
            {
                lastDecisionTrace = $"sequence return blocked would step onto bomb {sequenceBombTile} from {myTile}";
                LogKickSurgical(
                    "RETURN_BLOCKED_BOMB_TILE",
                    $"my:{myTile} stand:{kickStandTile} move:{dir} bomb:{DescribeBomb(bomb)} dangerHere:{FormatDanger(GetDangerSeconds(myTile, bomb))} nearby:{DescribeNearbyBombs(myTile)}",
                    true);
                ResetOffensiveSequence();
                return false;
            }

            if (myTile != kickStandTile && !IsWalkableTile(myTile + dir, myTile))
            {
                lastDecisionTrace = $"sequence return blocked from {myTile} to {myTile + dir}";
                LogKickSurgical(
                    "RETURN_BLOCKED",
                    $"my:{myTile} next:{myTile + dir} stand:{kickStandTile} move:{dir} bomb:{DescribeBomb(bomb)} dangerHere:{FormatDanger(GetDangerSeconds(myTile, bomb))} nearby:{DescribeNearbyBombs(myTile)}",
                    true);
                ResetOffensiveSequence();
                return false;
            }

            if (myTile == kickStandTile)
            {
                if (TryBuildKickActivationInputDecision(
                        settings,
                        myTile,
                        bomb,
                        sequenceBombTile,
                        sequenceKickDirection,
                        285 + DifficultyWeight(settings),
                        "activate kick bomb",
                        out decision))
                {
                    lastDecisionTrace = $"sequence input kick stand {kickStandTile} dir {sequenceKickDirection}";
                    LogKickSurgical(
                        "KICK_INPUT",
                        $"my:{myTile} stand:{kickStandTile} dir:{sequenceKickDirection} bomb:{DescribeBomb(bomb)}",
                        true);
                    return true;
                }

                if (TryKickSequenceBombDirect(bomb, sequenceKickDirection, out string kickFailReason))
                {
                    bool repeatArmed = TryArmRepeatKickAfterSuccessfulKick(myTile, bomb, kickStandTile);
                    bool postKickEscapeArmed = false;
                    Vector2 postKickEscapeMove = Vector2.zero;
                    Vector2Int postKickEscapeTarget = sequenceBombTile;
                    string postKickEscapeRoute = string.Empty;
                    if (!repeatArmed)
                    {
                        postKickEscapeArmed = ArmPostKickEscapeIfNeeded(
                            settings,
                            myTile,
                            out postKickEscapeMove,
                            out postKickEscapeTarget,
                            out postKickEscapeRoute);

                        if (!postKickEscapeArmed)
                            ResetOffensiveSequence();

                        nextOffensiveSequenceTime = Time.time + OffensiveSequenceCooldownSeconds;
                    }

                    decision = new BattleModeComAbilityDecision
                    {
                        Action = BattleModeComActionType.KickBomb,
                        Weight = 260 + DifficultyWeight(settings),
                        TargetTile = postKickEscapeArmed ? postKickEscapeTarget : sequenceBombTile,
                        HasTarget = true,
                        FirstMove = postKickEscapeMove,
                        Reason = postKickEscapeArmed ? "direct early kick bomb and escape" : "direct early kick bomb",
                        InputDescription = postKickEscapeArmed
                            ? AppendInput("DirectKick", FirstMoveDescription(postKickEscapeMove))
                            : "DirectKick"
                    };

                    lastDecisionTrace = $"sequence direct kick stand {kickStandTile} dir {sequenceKickDirection}";
                    LogKickSurgical(
                        "KICK_DIRECT",
                        $"my:{myTile} stand:{kickStandTile} dir:{sequenceKickDirection} bomb:{DescribeBomb(bomb)} repeat:{repeatArmed} kicks:{sequenceKicksCompleted}/{sequenceTargetKickCount} actionR:{sequenceAllowActionRStop} escape:{postKickEscapeArmed} escapeMove:{FirstMoveDescription(postKickEscapeMove)} escapeTarget:{postKickEscapeTarget} route:{postKickEscapeRoute}",
                        true);
                    return true;
                }

                lastDecisionTrace = $"sequence direct kick failed stand {kickStandTile} reason {kickFailReason}";

                EnsureDirectKickRetryWindow();
                if (CanRetryDirectKick(bomb))
                {
                    float dangerHere = GetDangerSeconds(myTile);
                    float minRetrySafetySeconds = Mathf.Max(
                        settings.dangerReactionSeconds,
                        EstimateFirstMoveTraversalSeconds(sequenceKickDirection)) +
                        DirectKickRetryDangerBufferSeconds;
                    if (!float.IsInfinity(dangerHere) && dangerHere <= minRetrySafetySeconds)
                    {
                        lastDecisionTrace =
                            $"sequence direct kick retry cancelled unsafe stand {kickStandTile} " +
                            $"danger {FormatDanger(dangerHere)} minSafe {minRetrySafetySeconds:F2} " +
                            $"reason {kickFailReason}";
                        LogKickSurgical(
                            "KICK_DIRECT_RETRY_UNSAFE",
                            $"my:{myTile} stand:{kickStandTile} dir:{sequenceKickDirection} reason:{kickFailReason} dangerHere:{FormatDanger(dangerHere)} minSafe:{minRetrySafetySeconds:F2} bomb:{DescribeBomb(bomb)} nearby:{DescribeNearbyBombs(myTile)}",
                            true);
                        ResetOffensiveSequence();
                        return false;
                    }

                    decision = new BattleModeComAbilityDecision
                    {
                        Action = BattleModeComActionType.KickBomb,
                        Weight = 255 + DifficultyWeight(settings),
                        TargetTile = sequenceBombTile,
                        HasTarget = true,
                        FirstMove = Vector2.zero,
                        Reason = "retry direct kick bomb",
                        InputDescription = "DirectKickRetry"
                    };

                    lastDecisionTrace =
                        $"sequence direct kick retry stand {kickStandTile} reason {kickFailReason} " +
                        $"retryLeft {Mathf.Max(0f, sequenceDirectKickRetryUntil - Time.time):F2}";
                    LogKickSurgical(
                        "KICK_DIRECT_RETRY",
                        $"my:{myTile} stand:{kickStandTile} dir:{sequenceKickDirection} reason:{kickFailReason} bomb:{DescribeBomb(bomb)} retryLeft:{Mathf.Max(0f, sequenceDirectKickRetryUntil - Time.time):F2} nearby:{DescribeNearbyBombs(myTile)}");
                    return true;
                }

                LogKickSurgical(
                    "KICK_DIRECT_FAILED",
                    $"my:{myTile} stand:{kickStandTile} dir:{sequenceKickDirection} reason:{kickFailReason} bomb:{DescribeBomb(bomb)} retryLeft:{Mathf.Max(0f, sequenceDirectKickRetryUntil - Time.time):F2} nearby:{DescribeNearbyBombs(myTile)}",
                    true);
                ResetOffensiveSequence();
                return false;
            }

            decision = new BattleModeComAbilityDecision
            {
                Action = BattleModeComActionType.KickBomb,
                Weight = 190 + DifficultyWeight(settings),
                TargetTile = sequenceBombTile,
                HasTarget = true,
                FirstMove = TileDirectionToVector(dir),
                Reason = "return and kick bomb",
                InputDescription = FirstMoveDescription(TileDirectionToVector(dir))
            };

            lastDecisionTrace = $"sequence return/kick stand {kickStandTile} move {dir}";
            LogKickSurgical(
                "RETURN",
                $"my:{myTile} stand:{kickStandTile} move:{dir} bomb:{DescribeBomb(bomb)} dangerHere:{FormatDanger(GetDangerSeconds(myTile, bomb))}");
            return true;
        }

        lastDecisionTrace = $"sequence unknown state {sequenceState}";
        return false;
    }

    private bool TryContinueRepeatPlantSequence(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        out BattleModeComAbilityDecision decision)
    {
        decision = default;

        if (sequenceKicksCompleted >= MaxSequentialKickBombs)
        {
            lastDecisionTrace = $"repeat plant cancelled max kicks {sequenceKicksCompleted}";
            ResetOffensiveSequence();
            return false;
        }

        if (bombController == null || bombController.BombsRemaining <= 0)
        {
            lastDecisionTrace = $"repeat plant cancelled no bombs remaining bc:{(bombController != null)}";
            LogKickSurgical(
                "CHAIN_NO_BOMBS",
                $"my:{myTile} origin:{sequenceBombTile} kicks:{sequenceKicksCompleted}/{sequenceTargetKickCount}",
                true);
            ResetOffensiveSequence();
            return false;
        }

        Vector2Int repeatStandTile = sequenceBombTile - sequenceKickDirection;
        if (!IsWalkableTile(repeatStandTile, sequenceBombTile))
        {
            lastDecisionTrace = $"repeat plant cancelled stand blocked {repeatStandTile}";
            LogKickSurgical(
                "CHAIN_STAND_BLOCKED",
                $"my:{myTile} origin:{sequenceBombTile} stand:{repeatStandTile} nearby:{DescribeNearbyBombs(myTile)}",
                true);
            ResetOffensiveSequence();
            return false;
        }

        Bomb originBomb = FindBombAt(sequenceBombTile);
        if (originBomb != null)
        {
            if (Time.time - sequenceStateStartedTime > RepeatPlantOriginWaitSeconds)
            {
                lastDecisionTrace = $"repeat plant cancelled origin occupied {sequenceBombTile}";
                LogKickSurgical(
                    "CHAIN_ORIGIN_OCCUPIED",
                    $"my:{myTile} originBomb:{DescribeBomb(originBomb)} nearby:{DescribeNearbyBombs(myTile)}",
                    true);
                ResetOffensiveSequence();
                return false;
            }

            decision = new BattleModeComAbilityDecision
            {
                Action = BattleModeComActionType.KickBomb,
                Weight = 190 + DifficultyWeight(settings),
                TargetTile = sequenceBombTile,
                HasTarget = true,
                FirstMove = Vector2.zero,
                Reason = "wait repeat kick origin clear",
                InputDescription = "none"
            };
            lastDecisionTrace = $"repeat plant waiting origin {sequenceBombTile} bomb {DescribeBomb(originBomb)}";
            LogKickSurgical(
                "CHAIN_WAIT_ORIGIN",
                $"my:{myTile} origin:{sequenceBombTile} bomb:{DescribeBomb(originBomb)}");
            return true;
        }

        if (myTile != sequenceBombTile)
        {
            Vector2Int dir = StepToward(myTile, sequenceBombTile);
            if (dir == Vector2Int.zero || !IsWalkableTile(myTile + dir, myTile))
            {
                if (Time.time - sequenceStateStartedTime <= RepeatReturnBlockedWaitSeconds)
                {
                    decision = new BattleModeComAbilityDecision
                    {
                        Action = BattleModeComActionType.KickBomb,
                        Weight = 205 + DifficultyWeight(settings),
                        TargetTile = sequenceBombTile,
                        HasTarget = true,
                        FirstMove = Vector2.zero,
                        Reason = "wait repeat kick origin unblock",
                        InputDescription = "none"
                    };
                    lastDecisionTrace = $"repeat plant waiting return unblock from {myTile} to {sequenceBombTile}";
                    LogKickSurgical(
                        "CHAIN_WAIT_RETURN",
                        $"my:{myTile} origin:{sequenceBombTile} move:{dir} nearby:{DescribeNearbyBombs(myTile)}");
                    return true;
                }

                lastDecisionTrace = $"repeat plant return blocked from {myTile} to {sequenceBombTile}";
                LogKickSurgical(
                    "CHAIN_RETURN_BLOCKED",
                    $"my:{myTile} origin:{sequenceBombTile} move:{dir} nearby:{DescribeNearbyBombs(myTile)}",
                    true);
                ResetOffensiveSequence();
                return false;
            }

            decision = new BattleModeComAbilityDecision
            {
                Action = BattleModeComActionType.KickBomb,
                Weight = 210 + DifficultyWeight(settings),
                TargetTile = sequenceBombTile,
                HasTarget = true,
                FirstMove = TileDirectionToVector(dir),
                Reason = "return to repeat kick origin",
                InputDescription = FirstMoveDescription(TileDirectionToVector(dir))
            };
            lastDecisionTrace = $"repeat plant return origin {sequenceBombTile} move {dir}";
            LogKickSurgical(
                "CHAIN_RETURN_ORIGIN",
                $"my:{myTile} origin:{sequenceBombTile} move:{dir}");
            return true;
        }

        if (!IsRepeatKickLaneReady(sequenceBombTile, sequenceKickDirection, out string repeatLaneReason))
        {
            lastDecisionTrace = $"repeat plant cancelled lane blocked {repeatLaneReason}";
            LogKickSurgical(
                "CHAIN_PLANT_LANE_BLOCKED",
                $"my:{myTile} origin:{sequenceBombTile} dir:{sequenceKickDirection} reason:{repeatLaneReason} nearby:{DescribeNearbyBombs(myTile)}",
                true);
            ResetOffensiveSequence();
            return false;
        }

        if (!TryPlaceSequenceBombAtOrigin(out Bomb placedBomb, out string placeFailReason))
        {
            lastDecisionTrace = $"repeat plant failed origin {sequenceBombTile} reason {placeFailReason}";
            LogKickSurgical(
                "CHAIN_PLANT_FAILED",
                $"my:{myTile} origin:{sequenceBombTile} reason:{placeFailReason} nearby:{DescribeNearbyBombs(myTile)}",
                true);
            ResetOffensiveSequence();
            return false;
        }

        SetSequenceState(SequenceState.RetreatAfterPlant);
        sequenceRetreatTile = repeatStandTile;

        Vector2 retreatMove = TileDirectionToVector(sequenceRetreatTile - sequenceBombTile);
        decision = new BattleModeComAbilityDecision
        {
            Action = BattleModeComActionType.KickBomb,
            Weight = 250 + DifficultyWeight(settings),
            TargetTile = sequenceRetreatTile,
            HasTarget = true,
            FirstMove = retreatMove,
            Reason = "plant repeat kick bomb",
            InputDescription = AppendInput("DirectPlant", FirstMoveDescription(retreatMove))
        };

        lastDecisionTrace = $"repeat plant selected origin {sequenceBombTile} stand {sequenceRetreatTile} kicks {sequenceKicksCompleted}";
        LogKickSurgical(
            "CHAIN_PLANT",
            $"my:{myTile} bomb:{DescribeBomb(placedBomb)} stand:{sequenceRetreatTile} kicks:{sequenceKicksCompleted}/{sequenceTargetKickCount} actionR:{sequenceAllowActionRStop}",
            true);
        return true;
    }

    private bool TryArmRepeatKickAfterSuccessfulKick(
        Vector2Int myTile,
        Bomb kickedBomb,
        Vector2Int standTile)
    {
        sequenceKicksCompleted = Mathf.Min(sequenceKicksCompleted + 1, MaxSequentialKickBombs);
        sequenceTrackedBomb = null;
        sequencePlantDirectionPatched = false;

        if (sequenceKicksCompleted >= Mathf.Clamp(sequenceTargetKickCount, 1, MaxSequentialKickBombs))
        {
            ArmActionRStopWindowIfPlanned();
            return false;
        }

        if (bombController == null || bombController.BombsRemaining <= 0)
        {
            ArmActionRStopWindowIfPlanned();
            return false;
        }

        if (!IsCardinalStep(sequenceKickDirection))
        {
            ArmActionRStopWindowIfPlanned();
            return false;
        }

        if (!IsWalkableTile(standTile, sequenceBombTile))
        {
            ArmActionRStopWindowIfPlanned();
            return false;
        }

        if (!IsRepeatKickLaneReady(sequenceBombTile, sequenceKickDirection, out string laneReason))
        {
            LogKickSurgical(
                "CHAIN_NOT_ARMED_LANE_BLOCKED",
                $"my:{myTile} origin:{sequenceBombTile} dir:{sequenceKickDirection} reason:{laneReason} kicked:{DescribeBomb(kickedBomb)}",
                true);
            ArmActionRStopWindowIfPlanned();
            return false;
        }

        SetSequenceState(SequenceState.ReturnToRepeatPlant);
        LogKickSurgical(
            "CHAIN_ARMED",
            $"my:{myTile} origin:{sequenceBombTile} stand:{standTile} kicked:{DescribeBomb(kickedBomb)} kicks:{sequenceKicksCompleted}/{sequenceTargetKickCount} actionR:{sequenceAllowActionRStop}",
            true);
        return true;
    }

    private bool IsRepeatKickLaneReady(Vector2Int originTile, Vector2Int kickDir, out string reason)
    {
        reason = "ready";

        if (!IsCardinalStep(kickDir))
        {
            reason = $"invalid dir {kickDir}";
            return false;
        }

        Vector2Int firstTile = originTile + kickDir;
        Bomb blockingBomb = FindBombAt(firstTile);
        if (blockingBomb != null && blockingBomb.IsBeingKicked)
        {
            reason = $"moving bomb still in first lane tile {DescribeBomb(blockingBomb)}";
            return false;
        }

        if (blockingBomb != null)
        {
            reason = $"bomb still in first lane tile {DescribeBomb(blockingBomb)}";
            return false;
        }

        return IsKickLaneOpen(originTile, kickDir, 1);
    }

    private void ConfigureNewKickSequencePlan(BattleModeComDifficultySettings settings, bool firstBombAlreadyPlaced)
    {
        sequenceKicksCompleted = 0;
        sequenceTrackedBomb = null;
        sequencePlantDirectionPatched = false;
        forceActionRStopUntil = -10f;
        sequenceDirectKickRetryUntil = -10f;
        postKickEscapeUntil = -10f;

        int bombsNeededForSecondKick = firstBombAlreadyPlaced ? 1 : 2;
        bool hasBombForSecondKick = bombController != null && bombController.BombsRemaining >= bombsNeededForSecondKick;
        float repeatChance = DifficultyChance(settings, 0.20f, 0.45f, 0.65f);
        sequenceTargetKickCount = hasBombForSecondKick && Random.value < repeatChance
            ? MaxSequentialKickBombs
            : 1;

        float actionRChance = DifficultyChance(settings, 0.20f, 0.42f, 0.58f);
        sequenceAllowActionRStop = CanUseActionRStop && Random.value < actionRChance;
    }

    private void ArmActionRStopWindowIfPlanned()
    {
        forceActionRStopUntil = sequenceAllowActionRStop
            ? Time.time + ForceActionRStopAfterKickSeconds
            : -10f;
    }

    private bool TryPlaceSequenceBombAtOrigin(out Bomb placedBomb, out string failReason)
    {
        placedBomb = null;
        failReason = string.Empty;

        if (bombController == null)
        {
            failReason = "bomb controller null";
            return false;
        }

        if (bombController.BombsRemaining <= 0)
        {
            failReason = "no bombs remaining";
            return false;
        }

        if (FindBombAt(sequenceBombTile) != null)
        {
            failReason = "origin occupied";
            return false;
        }

        if (!bombController.TryPlaceBombAtIgnoringInputLock(TileToWorld(sequenceBombTile), out GameObject placedGo, consumeBomb: true, playSfx: true))
        {
            failReason = "TryPlaceBombAtIgnoringInputLock false";
            return false;
        }

        if (placedGo == null || !placedGo.TryGetComponent(out placedBomb) || placedBomb == null)
        {
            failReason = "placed bomb missing component";
            return false;
        }

        sequenceTrackedBomb = placedBomb;
        sequencePlantDirectionPatched = false;
        EnsureSequenceBombPlantDirection(placedBomb);
        return true;
    }

    private void EnsureSequenceBombPlantDirection(Bomb bomb)
    {
        if (bomb == null)
            return;

        if (sequenceTrackedBomb != bomb)
        {
            sequenceTrackedBomb = bomb;
            sequencePlantDirectionPatched = false;
        }

        if (sequencePlantDirectionPatched)
            return;

        Vector2 retreatDirection = TileDirectionToVector(sequenceRetreatTile - sequenceBombTile);
        if (retreatDirection == Vector2.zero)
            retreatDirection = -TileDirectionToVector(sequenceKickDirection);

        NotifySequenceBombPlanted(bomb, retreatDirection);
        sequencePlantDirectionPatched = true;

        LogKickSurgical(
            "PLANT_DIR_PATCH",
            $"bomb:{DescribeBomb(bomb)} retreatDir:{FirstMoveDescription(retreatDirection)}");
    }

    private bool TryBuildOwnTriggerBombKickDecision(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        out BattleModeComAbilityDecision decision,
        bool forceChance = false)
    {
        decision = default;

        // Chute ofensivo de bomba própria é jogada ocasional. O roll é CACHEADO por
        // ~0.2s para que múltiplas chamadas dentro do mesmo ciclo de Think() (emergency +
        // ExecuteEscape) compartilhem o mesmo resultado em vez de re-rolarem e elevarem
        // a probabilidade efetiva além da intenção (ex.: dois rolls de 50% → 75%).
        float ownTriggerChance = DifficultyChance(settings, 0.10f, 0.25f, 0.50f);
        float cacheWindowSeconds = 0.20f;
        if (forceChance)
        {
            offensiveTriggerChanceCacheTime = Time.time;
            offensiveTriggerChanceCacheResult = true;
        }
        else if (Time.time - offensiveTriggerChanceCacheTime > cacheWindowSeconds)
        {
            offensiveTriggerChanceCacheTime = Time.time;
            offensiveTriggerChanceCacheResult = Random.value <= ownTriggerChance;
            if (!offensiveTriggerChanceCacheResult)
            {
                lastDecisionTrace =
                    $"own trigger kick chance failed chance {ownTriggerChance:F2} (new roll)";
                LogKickSurgical(
                    "OWN_TRIGGER_CHANCE_FAIL",
                    $"chance:{ownTriggerChance:F2} cached for {cacheWindowSeconds:F2}s");
            }
        }
        else if (!offensiveTriggerChanceCacheResult)
        {
            lastDecisionTrace =
                $"own trigger kick chance failed chance {ownTriggerChance:F2} (cached)";
        }

        if (!offensiveTriggerChanceCacheResult)
            return false;

        Bomb bestBomb = null;
        PlayerIdentity bestTarget = null;
        Vector2Int bestBombTile = myTile;
        Vector2Int bestTargetTile = myTile;
        Vector2Int bestKickDir = Vector2Int.zero;
        Vector2Int bestStandTile = myTile;
        Vector2Int bestMoveDir = Vector2Int.zero;
        bool bestDefensiveEscapeOnly = false;
        int bestScore = int.MaxValue;
        int ownEarlyBombsNear = 0;
        string rejectedRescue = string.Empty;

        foreach (Bomb bomb in Bomb.ActiveBombs)
        {
            if (!IsOwnEarlyKickBomb(bomb))
                continue;

            Vector2Int bombTile = WorldToTile(bomb.GetLogicalPosition());
            int distanceFromMe = Manhattan(myTile, bombTile);
            if (distanceFromMe > MaxKickSequenceDistanceFromBomb)
                continue;

            ownEarlyBombsNear++;

            if (bomb.RemainingFuseSeconds <= MinimumOwnTriggerKickFuseSeconds)
            {
                AppendTracePart(
                    ref rejectedRescue,
                    $"bomb:{bombTile} fuse low:{FormatDanger(bomb.RemainingFuseSeconds)}");
                continue;
            }

            if (!TryFindNearestEnemyAligned(bombTile, out PlayerIdentity target, out Vector2Int targetTile, out Vector2Int kickDir))
            {
                string defensiveReject = string.Empty;
                Vector2Int defensiveStandTile = myTile;
                Vector2Int defensiveMoveDir = Vector2Int.zero;
                Vector2Int defensiveTargetTile = bombTile;
                bool hasDefensivePlan = forceChance &&
                    TryFindDefensiveOwnKickPlan(settings, myTile, bombTile, out kickDir, out defensiveStandTile, out defensiveMoveDir, out defensiveTargetTile, out defensiveReject);

                if (!hasDefensivePlan)
                {
                    AppendTracePart(
                        ref rejectedRescue,
                        $"bomb:{bombTile} no aligned target{(string.IsNullOrEmpty(defensiveReject) ? string.Empty : " defensive:" + defensiveReject)}");
                    continue;
                }

                target = null;
                targetTile = defensiveTargetTile;

                int defensiveScore = distanceFromMe + Manhattan(myTile, defensiveStandTile);
                if (defensiveScore >= bestScore)
                    continue;

                bestScore = defensiveScore;
                bestBomb = bomb;
                bestTarget = null;
                bestBombTile = bombTile;
                bestTargetTile = targetTile;
                bestKickDir = kickDir;
                bestStandTile = defensiveStandTile;
                bestMoveDir = defensiveMoveDir;
                bestDefensiveEscapeOnly = true;
                continue;
            }

            Vector2Int standTile = bombTile - kickDir;
            if (!IsWalkableTile(standTile, bombTile))
            {
                AppendTracePart(ref rejectedRescue, $"bomb:{bombTile} stand blocked:{standTile}");
                continue;
            }

            // Verifica se há um caminho completo de BFS de myTile até standTile.
            // TryStepTowardKickStand valida apenas passos imediatos; sem esta verificação
            // a IA pode iniciar a sequência e ficar presa no meio do caminho (entre bombas
            // próprias), sem conseguir alcançar o stand e sem conseguir fugir.
            int bfsMaxDepth = MaxKickSequenceDistanceFromBomb + 2;
            if (!CanReachStandTileAvoidingBomb(myTile, standTile, bombTile, bfsMaxDepth))
            {
                AppendTracePart(ref rejectedRescue, $"bomb:{bombTile} no full path to stand:{standTile}");
                continue;
            }

            Vector2Int moveDir;
            if (myTile == standTile)
            {
                moveDir = kickDir;
            }
            else if (myTile == bombTile)
            {
                moveDir = standTile - bombTile;
                if (!IsCardinalStep(moveDir) || !IsWalkableTile(standTile, bombTile))
                {
                    AppendTracePart(ref rejectedRescue, $"bomb:{bombTile} recover blocked stand:{standTile}");
                    continue;
                }
            }
            else if (!TryStepTowardKickStand(myTile, standTile, bombTile, out moveDir))
            {
                AppendTracePart(ref rejectedRescue, $"bomb:{bombTile} route blocked stand:{standTile}");
                continue;
            }

            // Rejeita se o caminho do chute passa por explosão ativa ou bomba iminente
            int candidateLaneDist = Manhattan(bombTile, targetTile);
            if (!IsKickLaneSafeFromImminentExplosions(bombTile + kickDir, kickDir, candidateLaneDist))
            {
                AppendTracePart(ref rejectedRescue, $"bomb:{bombTile} kick lane unsafe imminent explosion");
                continue;
            }

            int score = distanceFromMe + Manhattan(myTile, standTile);
            if (score >= bestScore)
                continue;

            bestScore = score;
            bestBomb = bomb;
            bestTarget = target;
            bestBombTile = bombTile;
            bestTargetTile = targetTile;
            bestKickDir = kickDir;
            bestStandTile = standTile;
            bestMoveDir = moveDir;
            bestDefensiveEscapeOnly = false;
        }

        if (bestBomb == null || (!bestDefensiveEscapeOnly && bestTarget == null) || bestMoveDir == Vector2Int.zero)
        {
            lastDecisionTrace = $"own trigger kick rescue none nearby:{DescribeNearbyBombs(myTile)}";
            if (ownEarlyBombsNear > 0)
            {
                LogKickSurgical(
                    "RESCUE_FAILED",
                    $"my:{myTile} ownEarly:{ownEarlyBombsNear} rejected:{(string.IsNullOrEmpty(rejectedRescue) ? "none" : rejectedRescue)} nearby:{DescribeNearbyBombs(myTile)}");
            }
            return false;
        }

        // Verifica se o caminho do chute está livre de explosões ativas/iminentes.
        // Sem isso, a IA pode chutar uma bomba já existente para dentro de uma
        // explosão em andamento (de outra bomba com fuse quase zero), causando
        // reação em cadeia e morte própria.
        int ownLaneCheckDist = Manhattan(bestBombTile, bestTargetTile);
        string rescueTargetLabel = bestDefensiveEscapeOnly
            ? $"escape@{bestTargetTile}"
            : $"P{bestTarget.playerId}@{bestTargetTile}";
        if (!IsKickLaneSafeFromImminentExplosions(bestBombTile + bestKickDir, bestKickDir, ownLaneCheckDist))
        {
            lastDecisionTrace =
                $"own trigger kick lane unsafe imminent explosion bomb:{bestBombTile} dir:{bestKickDir} target:{rescueTargetLabel}";
            LogKickSurgical(
                "RESCUE_LANE_UNSAFE",
                $"my:{myTile} bomb:{bestBombTile} dir:{bestKickDir} target:{rescueTargetLabel} laneCheckDist:{ownLaneCheckDist}",
                true);
            return false;
        }

        SetSequenceState(SequenceState.ReturnToKick);
        ConfigureNewKickSequencePlan(settings, firstBombAlreadyPlaced: true);
        sequenceBombTile = bestBombTile;
        sequenceRetreatTile = bestStandTile;
        sequenceKickDirection = bestKickDir;
        nextOffensiveSequenceTime = Time.time + OffensiveSequenceCooldownSeconds;
        EnsureSequenceBombPlantDirection(bestBomb);

        Vector2 firstMove = TileDirectionToVector(bestMoveDir);
        bool kickNow = myTile == bestStandTile;
        if (kickNow)
        {
            if (TryBuildKickActivationInputDecision(
                    settings,
                    myTile,
                    bestBomb,
                    bestBombTile,
                    bestKickDir,
                    300 + DifficultyWeight(settings),
                    $"activate own early bomb kick toward {rescueTargetLabel}",
                    out decision))
            {
                lastDecisionTrace =
                    $"own trigger input kick bomb {bestBombTile} stand {bestStandTile} target {rescueTargetLabel} " +
                    $"dir {bestKickDir}";
                decision.UsesEscapeAbilityChance = bestDefensiveEscapeOnly;
                LogKickSurgical(
                    "RESCUE_INPUT_KICK",
                    $"my:{myTile} bomb:{DescribeBomb(bestBomb)} stand:{bestStandTile} target:{rescueTargetLabel} dir:{bestKickDir}",
                    true);
                return true;
            }

            if (TryKickSequenceBombDirect(bestBomb, bestKickDir, out string kickFailReason))
            {
                bool repeatArmed = TryArmRepeatKickAfterSuccessfulKick(myTile, bestBomb, bestStandTile);
                bool postKickEscapeArmed = false;
                Vector2 postKickEscapeMove = Vector2.zero;
                Vector2Int postKickEscapeTarget = bestBombTile;
                string postKickEscapeRoute = string.Empty;
                if (!repeatArmed)
                {
                    postKickEscapeArmed = ArmPostKickEscapeIfNeeded(
                        settings,
                        myTile,
                        out postKickEscapeMove,
                        out postKickEscapeTarget,
                        out postKickEscapeRoute);

                    if (!postKickEscapeArmed)
                        ResetOffensiveSequence();

                    nextOffensiveSequenceTime = Time.time + OffensiveSequenceCooldownSeconds;
                }

                decision = new BattleModeComAbilityDecision
                {
                    Action = BattleModeComActionType.KickBomb,
                    Weight = 280 + DifficultyWeight(settings),
                    TargetTile = postKickEscapeArmed ? postKickEscapeTarget : bestBombTile,
                    HasTarget = true,
                    FirstMove = postKickEscapeMove,
                    Reason = postKickEscapeArmed
                        ? $"direct kick own early bomb toward {rescueTargetLabel} and escape"
                        : $"direct kick own early bomb toward {rescueTargetLabel}",
                    InputDescription = postKickEscapeArmed
                        ? AppendInput("DirectKick", FirstMoveDescription(postKickEscapeMove))
                        : "DirectKick",
                    UsesEscapeAbilityChance = bestDefensiveEscapeOnly
                };

                lastDecisionTrace =
                    $"own trigger direct kick bomb {bestBombTile} stand {bestStandTile} target {rescueTargetLabel} " +
                    $"dir {bestKickDir}";
                LogKickSurgical(
                    "RESCUE_DIRECT_KICK",
                    $"my:{myTile} bomb:{DescribeBomb(bestBomb)} stand:{bestStandTile} target:{rescueTargetLabel} dir:{bestKickDir} repeat:{repeatArmed} kicks:{sequenceKicksCompleted}/{sequenceTargetKickCount} actionR:{sequenceAllowActionRStop} escape:{postKickEscapeArmed} escapeMove:{FirstMoveDescription(postKickEscapeMove)} escapeTarget:{postKickEscapeTarget} route:{postKickEscapeRoute}",
                    true);
                return true;
            }

            lastDecisionTrace = $"own trigger direct kick failed bomb {bestBombTile} stand {bestStandTile} reason {kickFailReason}";
            LogKickSurgical(
                "RESCUE_DIRECT_FAILED",
                $"my:{myTile} bomb:{DescribeBomb(bestBomb)} stand:{bestStandTile} target:{rescueTargetLabel} dir:{bestKickDir} reason:{kickFailReason}",
                true);
            return false;
        }

        decision = new BattleModeComAbilityDecision
        {
            Action = BattleModeComActionType.KickBomb,
            Weight = 240 + DifficultyWeight(settings),
            TargetTile = kickNow ? bestBombTile : bestStandTile,
            HasTarget = true,
            FirstMove = firstMove,
            Reason = kickNow
                ? $"kick own early bomb toward {rescueTargetLabel}"
                : $"recover own early bomb kick toward {rescueTargetLabel}",
            InputDescription = FirstMoveDescription(firstMove),
            UsesEscapeAbilityChance = bestDefensiveEscapeOnly
        };

        lastDecisionTrace =
            $"own trigger kick rescue bomb {bestBombTile} stand {bestStandTile} target {rescueTargetLabel} " +
            $"dir {bestKickDir} move {bestMoveDir} solid:{bestBomb.IsSolid}/early:{bestBomb.CanBeKickedEarly}";
        LogKickSurgical(
            "RESCUE_PLAN",
            $"my:{myTile} bomb:{DescribeBomb(bestBomb)} stand:{bestStandTile} target:{rescueTargetLabel} dir:{bestKickDir} move:{bestMoveDir} kickNow:{kickNow} nearby:{DescribeNearbyBombs(myTile)}",
            true);
        return true;
    }

    protected virtual void NotifySequenceBombPlanted(Bomb bomb, Vector2 retreatDirection)
    {
        movement?.NotifyBombPlanted(bomb, retreatDirection);
        kickAbility?.NotifyBombPlanted(bomb, retreatDirection);
    }

    protected virtual bool TryBuildCustomEmergencyDecision(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        float currentDangerSeconds,
        out BattleModeComAbilityDecision decision)
    {
        decision = default;
        return false;
    }

    private bool TryFindDefensiveOwnKickPlan(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        Vector2Int bombTile,
        out Vector2Int kickDir,
        out Vector2Int standTile,
        out Vector2Int moveDir,
        out Vector2Int targetTile,
        out string rejectReason)
    {
        kickDir = Vector2Int.zero;
        standTile = myTile;
        moveDir = Vector2Int.zero;
        targetTile = bombTile;
        rejectReason = string.Empty;

        int bestScore = int.MaxValue;
        int bfsMaxDepth = MaxKickSequenceDistanceFromBomb + 2;

        for (int i = 0; i < CardinalTiles.Length; i++)
        {
            Vector2Int dir = CardinalTiles[i];
            Vector2Int candidateStand = bombTile - dir;
            Vector2Int candidateTarget = bombTile + dir * 2;

            if (!IsKickLaneOpen(bombTile, dir, 1))
            {
                AppendTracePart(ref rejectReason, $"{dir}:lane blocked");
                continue;
            }

            if (!IsWalkableTile(candidateStand, bombTile))
            {
                AppendTracePart(ref rejectReason, $"{dir}:stand blocked:{candidateStand}");
                continue;
            }

            if (!CanReachStandTileAvoidingBomb(myTile, candidateStand, bombTile, bfsMaxDepth))
            {
                AppendTracePart(ref rejectReason, $"{dir}:no path stand:{candidateStand}");
                continue;
            }

            Vector2Int candidateMove;
            if (myTile == candidateStand)
            {
                candidateMove = dir;
            }
            else if (myTile == bombTile)
            {
                candidateMove = candidateStand - bombTile;
                if (!IsCardinalStep(candidateMove) || !IsWalkableTile(candidateStand, bombTile))
                {
                    AppendTracePart(ref rejectReason, $"{dir}:recover blocked:{candidateStand}");
                    continue;
                }
            }
            else if (!TryStepTowardKickStand(myTile, candidateStand, bombTile, out candidateMove))
            {
                AppendTracePart(ref rejectReason, $"{dir}:route blocked:{candidateStand}");
                continue;
            }

            if (!IsKickLaneSafeFromImminentExplosions(bombTile + dir, dir, 1))
            {
                AppendTracePart(ref rejectReason, $"{dir}:lane unsafe");
                continue;
            }

            if (!HasSafeEscapeAfterDefensiveKick(
                    bombTile,
                    dir,
                    candidateStand,
                    settings,
                    out Vector2Int escapeTile,
                    out string escapeReason))
            {
                AppendTracePart(ref rejectReason, $"{dir}:no post-kick escape stand:{candidateStand} {escapeReason}");
                continue;
            }

            int score = Manhattan(myTile, candidateStand);
            if (myTile == candidateStand)
                score -= 4;

            score -= CountOpenNeighbors(escapeTile);

            if (score >= bestScore)
                continue;

            bestScore = score;
            kickDir = dir;
            standTile = candidateStand;
            moveDir = candidateMove;
            targetTile = candidateTarget;
        }

        return kickDir != Vector2Int.zero && moveDir != Vector2Int.zero;
    }

    private bool HasSafeEscapeAfterDefensiveKick(
        Vector2Int bombTile,
        Vector2Int kickDir,
        Vector2Int standTile,
        BattleModeComDifficultySettings settings,
        out Vector2Int escapeTile,
        out string reason)
    {
        escapeTile = standTile;
        reason = "none";

        Bomb bomb = FindBombAt(bombTile);
        int radius = bomb != null && bomb.Owner != null
            ? Mathf.Max(1, bomb.Owner.explosionRadius)
            : 2;
        Vector2Int firstBombTileAfterKick = bombTile + kickDir;
        string rejected = string.Empty;

        for (int i = 0; i < CardinalTiles.Length; i++)
        {
            Vector2Int dir = CardinalTiles[i];
            Vector2Int next = standTile + dir;

            if (next == bombTile)
            {
                AppendTracePart(ref rejected, $"{dir}:through bomb origin");
                continue;
            }

            if (!IsWalkableTile(next, standTile))
            {
                AppendTracePart(ref rejected, $"{dir}:blocked");
                continue;
            }

            float eta = EstimateFirstMoveTraversalSeconds(dir);
            if (IsDangerousAt(next, eta, settings, bomb))
            {
                AppendTracePart(ref rejected, $"{dir}:existing danger");
                continue;
            }

            if (IsTileInPredictedKickedBombBlastLine(firstBombTileAfterKick, next, radius, bombTile))
            {
                AppendTracePart(ref rejected, $"{dir}:kicked bomb blast");
                continue;
            }

            escapeTile = next;
            reason = $"escape:{next}";
            return true;
        }

        reason = string.IsNullOrEmpty(rejected) ? "no adjacent option" : rejected;
        return false;
    }

    private bool IsTileInPredictedKickedBombBlastLine(
        Vector2Int origin,
        Vector2Int tile,
        int radius,
        Vector2Int vacatedBombTile)
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
            if (HasIndestructibleTile(check) || HasDestructibleTile(check))
                return false;

            if (check != vacatedBombTile && FindBombAt(check) != null)
                return false;
        }

        return true;
    }

    private bool TryBuildActionRStopDecision(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        out BattleModeComAbilityDecision decision,
        bool forceWhenGood = false)
    {
        decision = default;

        if (!CanUseActionRStop)
        {
            lastDecisionTrace = "actionR unavailable for kick ability";
            return false;
        }

        if (Time.time - lastActionRStopTime < ActionRStopCooldownSeconds)
        {
            lastDecisionTrace = $"actionR cooldown {ActionRStopCooldownSeconds - (Time.time - lastActionRStopTime):F2}s";
            return false;
        }

        float chance = DifficultyChance(settings, 0.04f, 0.16f, 0.35f);
        if (!forceWhenGood && Random.value > chance)
        {
            lastDecisionTrace = $"actionR chance failed chance {chance:F2}";
            return false;
        }

        int movingOwnBombs = 0;

        foreach (Bomb bomb in Bomb.ActiveBombs)
        {
            if (bomb == null || bomb.HasExploded || !bomb.IsBeingKicked || bomb.Owner != bombController)
                continue;

            movingOwnBombs++;
            Vector2Int bombTile = WorldToTile(bomb.GetLogicalPosition());
            if (!TryFindNearestEnemyAligned(bombTile, out PlayerIdentity target, out Vector2Int targetTile, out _))
                continue;

            int distance = Manhattan(bombTile, targetTile);
            if (distance < 1 || distance > 3)
                continue;

            Vector2 escapeMove = Vector2.zero;
            Vector2Int escapeTarget = targetTile;
            string escapeRoute = "none";
            bool hasEscapeMove = !float.IsInfinity(GetDangerSeconds(myTile)) &&
                                 TryFindPostKickEscapeMove(
                                     settings,
                                     myTile,
                                     out escapeMove,
                                     out escapeTarget,
                                     out escapeRoute);

            lastActionRStopTime = Time.time;
            decision = new BattleModeComAbilityDecision
            {
                Action = BattleModeComActionType.KickBomb,
                Weight = (forceWhenGood ? 260 : 160) + DifficultyWeight(settings),
                TargetTile = hasEscapeMove ? escapeTarget : targetTile,
                HasTarget = true,
                FirstMove = hasEscapeMove ? escapeMove : Vector2.zero,
                Reason = hasEscapeMove
                    ? $"stop kicked bomb near P{target.playerId} and escape"
                    : $"stop kicked bomb near P{target.playerId}",
                InputDescription = hasEscapeMove
                    ? AppendInput("ActionR", FirstMoveDescription(escapeMove))
                    : "ActionR",
                TapActionR = true
            };
            lastDecisionTrace = $"actionR selected bomb {bombTile} target P{target.playerId}@{targetTile} distance {distance} escape {hasEscapeMove}";
            LogKickSurgical(
                "ACTION_R_STOP",
                $"bomb:{bombTile} target:P{target.playerId}@{targetTile} distance:{distance} force:{forceWhenGood} escape:{hasEscapeMove} escapeMove:{FirstMoveDescription(escapeMove)} escapeTarget:{escapeTarget} route:{escapeRoute}",
                true);
            return true;
        }

        lastDecisionTrace = $"actionR no stop target movingOwnBombs {movingOwnBombs}";
        return false;
    }

    private bool CanPlantBombForKick(
        Vector2Int myTile,
        Vector2Int kickDir,
        BattleModeComDifficultySettings settings,
        out Vector2Int retreatTile)
    {
        retreatTile = myTile - kickDir;
        if (!IsWalkableTile(retreatTile, myTile))
            return false;

        return IsKickLaneOpen(myTile + kickDir, kickDir, 2);
    }

    private Vector2Int FindBestEscapeAfterKick(
        Vector2Int myTile,
        Vector2Int kickDir,
        BattleModeComDifficultySettings settings)
    {
        Vector2Int best = myTile;
        int bestOpen = -1;

        for (int i = 0; i < CardinalTiles.Length; i++)
        {
            Vector2Int dir = CardinalTiles[i];
            if (dir == kickDir)
                continue;

            Vector2Int tile = myTile + dir;
            if (!IsSafeWalkable(tile, myTile, settings, 1))
                continue;

            int open = CountOpenNeighbors(tile);
            if (open > bestOpen)
            {
                bestOpen = open;
                best = tile;
            }
        }

        return best;
    }

    private bool ArmPostKickEscapeIfNeeded(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        out Vector2 firstMove,
        out Vector2Int target,
        out string route)
    {
        firstMove = Vector2.zero;
        target = myTile;
        route = "none";

        if (float.IsInfinity(GetDangerSeconds(myTile)))
            return false;

        SetSequenceState(SequenceState.EscapeAfterKick);
        postKickEscapeUntil = Time.time + PostKickEscapeSeconds;
        postKickEscapeLastTile = myTile;
        postKickEscapeStuckSince = -1f;
        postKickEscapeLastAttemptedStep = Vector2Int.zero;
        postKickEscapeBlockedSteps.Clear();

        if (TryFindPostKickEscapeMove(settings, myTile, out firstMove, out target, out route))
            return true;

        route = "escape pending";
        return true;
    }

    private bool TryContinuePostKickEscape(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        out BattleModeComAbilityDecision decision)
    {
        decision = default;

        float dangerHere = GetDangerSeconds(myTile);
        if (float.IsInfinity(dangerHere))
        {
            lastDecisionTrace = "post kick escape complete";
            LogKickSurgical(
                "POST_KICK_ESCAPE_DONE",
                $"my:{myTile} nearby:{DescribeNearbyBombs(myTile)}");
            ResetOffensiveSequence();
            return false;
        }

        if (Time.time > postKickEscapeUntil)
        {
            lastDecisionTrace = $"post kick escape expired danger {FormatDanger(dangerHere)}";
            LogKickSurgical(
                "POST_KICK_ESCAPE_EXPIRED",
                $"my:{myTile} dangerHere:{FormatDanger(dangerHere)} nearby:{DescribeNearbyBombs(myTile)}",
                true);
            ResetOffensiveSequence();
            return false;
        }

        // Detecta travamento: se a IA não mudou de tile por mais de 0.25s durante
        // a fuga, bloqueia a direção que estava sendo tentada e força o BFS a
        // encontrar uma rota alternativa. Repete para cada nova direção que travar.
        if (myTile == postKickEscapeLastTile)
        {
            if (postKickEscapeStuckSince < 0f)
                postKickEscapeStuckSince = Time.time;
            else if (Time.time - postKickEscapeStuckSince > 0.25f
                     && postKickEscapeLastAttemptedStep != Vector2Int.zero
                     && !postKickEscapeBlockedSteps.Contains(postKickEscapeLastAttemptedStep))
            {
                postKickEscapeBlockedSteps.Add(postKickEscapeLastAttemptedStep);
                postKickEscapeStuckSince = -1f; // reinicia timer para próxima direção
                LogKickSurgical(
                    "POST_KICK_ESCAPE_STUCK",
                    $"my:{myTile} blocking dir:{postKickEscapeLastAttemptedStep} totalBlocked:{postKickEscapeBlockedSteps.Count}",
                    true);
            }
        }
        else
        {
            postKickEscapeLastTile = myTile;
            postKickEscapeStuckSince = -1f;
            postKickEscapeBlockedSteps.Clear();
            postKickEscapeLastAttemptedStep = Vector2Int.zero;
        }

        if (!TryFindPostKickEscapeMove(settings, myTile, postKickEscapeBlockedSteps, out Vector2 firstMove, out Vector2Int target, out string route))
        {
            lastDecisionTrace = $"post kick escape no route danger {FormatDanger(dangerHere)}";
            // Reseta a sequência para que o sistema de fuga principal do controller
            // possa agir sem que a habilidade o intercepte em cada chamada de Think().
            // Sem esse reset, TryContinueOffensiveSequence continua sendo chamada via
            // TryBuildEmergencyCandidate (inclusive dentro de ExecuteEscape), bloqueando
            // o fallback normal do controller e deixando a IA travada sob perigo.
            ResetOffensiveSequence();
            LogKickSurgical(
                "POST_KICK_ESCAPE_FAILED",
                $"my:{myTile} dangerHere:{FormatDanger(dangerHere)} sequence reset nearby:{DescribeNearbyBombs(myTile)}",
                true);
            return false;
        }

        decision = new BattleModeComAbilityDecision
        {
            Action = BattleModeComActionType.Reposition,
            Weight = 280 + DifficultyWeight(settings),
            TargetTile = target,
            HasTarget = true,
            FirstMove = firstMove,
            Reason = "escape after kick bomb sequence",
            InputDescription = FirstMoveDescription(firstMove)
        };

        // Registra o primeiro passo tentado para que o stuck detection saiba
        // qual direção bloquear se a IA não conseguir se mover.
        postKickEscapeLastAttemptedStep = Vector2Int.RoundToInt(firstMove);

        lastDecisionTrace = $"post kick escape target {target} route {route}";
        LogKickSurgical(
            "POST_KICK_ESCAPE",
            $"my:{myTile} target:{target} move:{FirstMoveDescription(firstMove)} route:{route} dangerHere:{FormatDanger(dangerHere)} nearby:{DescribeNearbyBombs(myTile)}");
        return true;
    }

    private static readonly List<Vector2Int> EmptyBlockedSteps = new List<Vector2Int>(0);

    private bool TryFindPostKickEscapeMove(
        BattleModeComDifficultySettings settings,
        Vector2Int start,
        out Vector2 firstMove,
        out Vector2Int target,
        out string route)
        => TryFindPostKickEscapeMove(settings, start, EmptyBlockedSteps, out firstMove, out target, out route);

    private bool TryFindPostKickEscapeMove(
        BattleModeComDifficultySettings settings,
        Vector2Int start,
        List<Vector2Int> blockedFirstSteps,
        out Vector2 firstMove,
        out Vector2Int target,
        out string route)
    {
        firstMove = Vector2.zero;
        target = start;
        route = "none";

        escapeVisited.Clear();
        escapeOpen.Clear();

        escapeVisited[start] = new EscapeSearchNode { Parent = start, Depth = 0 };

        // Pré-marca todos os tiles de direções bloqueadas como visitados para que
        // o BFS os ignore e encontre rotas alternativas.
        for (int i = 0; i < blockedFirstSteps.Count; i++)
        {
            if (blockedFirstSteps[i] != Vector2Int.zero)
                escapeVisited[start + blockedFirstSteps[i]] = new EscapeSearchNode { Parent = start, Depth = 0 };
        }

        escapeOpen.Enqueue(start);

        int maxDepth = Mathf.Max(3, settings.searchDepth + 4);
        while (escapeOpen.Count > 0)
        {
            Vector2Int tile = escapeOpen.Dequeue();
            EscapeSearchNode node = escapeVisited[tile];
            float eta = EstimateEscapeTraversalSeconds(start, tile, node.Depth);
            float dangerSeconds = GetDangerSeconds(tile);

            if (node.Depth > 0 &&
                float.IsInfinity(dangerSeconds) &&
                !IsDangerousAt(tile, eta + settings.safeTileMinimumSeconds, settings))
            {
                target = tile;
                Vector2Int firstStep = ReconstructEscapeFirstStep(start, tile);
                float firstMoveEta = EstimateFirstMoveTraversalSeconds(firstStep);
                float currentDangerSeconds = GetDangerSeconds(start);
                if (!float.IsInfinity(currentDangerSeconds) &&
                    currentDangerSeconds <= firstMoveEta + DirectKickRetryDangerBufferSeconds)
                {
                    continue;
                }

                firstMove = TileDirectionToVector(firstStep);
                route = $"escape depth {node.Depth} eta {eta:F2}s";
                return firstMove != Vector2.zero;
            }

            if (node.Depth >= maxDepth)
                continue;

            for (int i = 0; i < CardinalTiles.Length; i++)
            {
                Vector2Int next = tile + CardinalTiles[i];
                if (escapeVisited.ContainsKey(next))
                    continue;

                if (!IsWalkableTile(next, start))
                    continue;

                float nextEta = EstimateEscapeTraversalSeconds(start, tile, next, node.Depth + 1);
                if (IsDangerousAt(next, nextEta, settings))
                    continue;

                escapeVisited[next] = new EscapeSearchNode { Parent = tile, Depth = node.Depth + 1 };
                escapeOpen.Enqueue(next);
            }
        }

        route = "no safe post-kick route";
        return false;
    }

    private bool TryFindPostKickFallbackMove(
        BattleModeComDifficultySettings settings,
        Vector2Int start,
        out Vector2 firstMove,
        out Vector2Int target,
        out string route)
    {
        firstMove = Vector2.zero;
        target = start;
        route = "no fallback";
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < CardinalTiles.Length; i++)
        {
            Vector2Int dir = CardinalTiles[i];
            Vector2Int next = start + dir;
            if (!IsWalkableTile(next, start))
                continue;

            float arrivalSeconds = EstimateFirstMoveTraversalSeconds(dir);
            float dangerMargin = GetSurvivalMarginSeconds(next, arrivalSeconds, settings);
            float score = dangerMargin + CountOpenNeighbors(next) * 0.25f;
            if (score <= bestScore)
                continue;

            bestScore = score;
            firstMove = TileDirectionToVector(dir);
            target = next;
            route = $"fallback score {score:F2}";
        }

        return firstMove != Vector2.zero;
    }

    private Vector2Int ReconstructEscapeFirstStep(Vector2Int start, Vector2Int target)
    {
        Vector2Int current = target;
        while (escapeVisited.TryGetValue(current, out EscapeSearchNode node) && node.Parent != start)
        {
            if (node.Parent == current)
                break;

            current = node.Parent;
        }

        return current - start;
    }

    private bool TryFindNearestEnemyAligned(
        Vector2Int origin,
        out PlayerIdentity target,
        out Vector2Int targetTile,
        out Vector2Int direction)
    {
        target = null;
        targetTile = origin;
        direction = Vector2Int.zero;
        int bestDistance = int.MaxValue;

        PlayerIdentity[] players = FindObjectsByType<PlayerIdentity>(FindObjectsInactive.Exclude);
        for (int i = 0; i < players.Length; i++)
        {
            PlayerIdentity player = players[i];
            if (player == null || player == identity)
                continue;

            if (player.TryGetComponent<CharacterHealth>(out var health) && health.life <= 0)
                continue;

            if (identity != null &&
                BattleModeRules.Instance != null &&
                BattleModeRules.Instance.GetTeamForPlayer(player.playerId) ==
                BattleModeRules.Instance.GetTeamForPlayer(identity.playerId))
                continue;

            Vector2Int tile = WorldToTile(player.transform.position);
            Vector2Int delta = tile - origin;
            bool aligned = delta.x == 0 ^ delta.y == 0;
            if (!aligned)
                continue;

            int distance = Manhattan(origin, tile);
            if (distance <= 1 || distance > MaxOffensiveKickDistance || distance >= bestDistance)
                continue;

            Vector2Int dir = new(Mathf.Clamp(delta.x, -1, 1), Mathf.Clamp(delta.y, -1, 1));
            if (!IsKickLaneOpen(origin, dir, distance - 1))
                continue;

            bestDistance = distance;
            target = player;
            targetTile = tile;
            direction = dir;
        }

        return target != null;
    }

    /// <summary>
    /// Verifica via BFS se existe um caminho completo de <paramref name="from"/>
    /// até <paramref name="to"/> sem passar pelo tile da bomba <paramref name="bombTile"/>.
    /// Evita o problema de verificar apenas o primeiro passo da rota e depois
    /// ficar preso no meio do caminho ao tentar alcançar o stand tile.
    /// </summary>
    private bool CanReachStandTileAvoidingBomb(
        Vector2Int from,
        Vector2Int to,
        Vector2Int bombTile,
        int maxDepth)
    {
        if (from == to)
            return true;

        pathCheckOpen.Clear();
        pathCheckVisited.Clear();

        pathCheckVisited.Add(from);
        pathCheckOpen.Enqueue(from);
        int depth = 0;

        while (pathCheckOpen.Count > 0)
        {
            int count = pathCheckOpen.Count;
            if (depth >= maxDepth)
                break;

            depth++;
            for (int i = 0; i < count; i++)
            {
                Vector2Int tile = pathCheckOpen.Dequeue();
                for (int d = 0; d < CardinalTiles.Length; d++)
                {
                    Vector2Int next = tile + CardinalTiles[d];
                    if (next == to)
                        return true;

                    if (pathCheckVisited.Contains(next))
                        continue;

                    if (next == bombTile)
                        continue;

                    if (!IsWalkableTile(next, from))
                        continue;

                    pathCheckVisited.Add(next);
                    pathCheckOpen.Enqueue(next);
                }
            }
        }

        return false;
    }

    // Margem de fuse a partir da qual uma bomba é considerada "iminente" para
    // efeito de segurança do lane de chute. Bombas com fuse abaixo deste valor
    // e cujo raio cobre qualquer tile do caminho tornam o chute perigoso.
    private const float KickLaneImminentFuseThreshold = 1.5f;

    /// <summary>
    /// Verifica se o caminho que a bomba percorrerá após o chute (a partir de
    /// <paramref name="kickOrigin"/> na direção <paramref name="kickDir"/>) está
    /// livre de explosões já ativas e de bombas com fuse iminente cujo raio
    /// alcança qualquer tile desse caminho.
    ///
    /// Além da verificação direta no tile do lane, também verifica tiles PERPENDICULARES
    /// ao kick — isto cobre o caso em que o BattleModeComController (ExecutionOrder -100)
    /// roda antes dos scripts de bomba (ordem 0) e a explosão ainda não criou seus
    /// colliders no tile do lane, mas já existe no tile de origem ou em tiles adjacentes
    /// na linha perpendicular.
    /// </summary>
    private bool IsKickLaneSafeFromImminentExplosions(
        Vector2Int kickOrigin,
        Vector2Int kickDir,
        int checkDistance)
    {
        // Direções perpendiculares ao chute (±90°)
        var perpDirs = new Vector2Int[]
        {
            new(-kickDir.y, kickDir.x),
            new(kickDir.y, -kickDir.x)
        };

        // step 0 is the first tile the bomb enters after the kick. Skipping it
        // lets the COM kick into an imminent blast and trigger an instant chain.
        for (int step = 0; step <= Mathf.Max(1, checkDistance); step++)
        {
            Vector2Int tile = kickOrigin + kickDir * step;

            if (!HasGroundTile(tile) || HasIndestructibleTile(tile))
                break;

            // 1) Explosão já ativa diretamente no tile do lane
            if (LaneTileHasActiveExplosion(tile))
                return false;

            // 2) Bomba alheia com fuse iminente cujo raio cobre este tile
            foreach (Bomb bomb in Bomb.ActiveBombs)
            {
                if (bomb == null || bomb.HasExploded || bomb.IsBeingHeldByPowerGlove)
                    continue;

                float fuse = bomb.IsControlBomb ? 0.65f : bomb.RemainingFuseSeconds;
                if (fuse > KickLaneImminentFuseThreshold)
                    continue;

                Vector2Int bombTile = WorldToTile(bomb.GetLogicalPosition());
                int bombRadius = bomb.Owner != null ? Mathf.Max(1, bomb.Owner.explosionRadius) : 2;
                if (IsTileInBlastLine(bombTile, tile, bombRadius))
                    return false;
            }

            // 3) Explosões ativas em tiles PERPENDICULARES que podem se propagar até
            //    o tile do lane. Trata o caso de race condition de execução: a bomba
            //    explodiu antes de COM rodar (HasExploded=true, removida de ActiveBombs)
            //    mas seus colliders de explosão ainda não chegaram ao tile do lane —
            //    entretanto, o tile de origem ou tiles intermediários já os têm.
            for (int pd = 0; pd < perpDirs.Length; pd++)
            {
                Vector2Int perpDir = perpDirs[pd];
                for (int pk = 1; pk <= MaxOffensiveKickDistance; pk++)
                {
                    Vector2Int perpTile = tile + perpDir * pk;

                    if (!HasGroundTile(perpTile))
                        break;

                    if (HasIndestructibleTile(perpTile))
                        break;

                    if (!LaneTileHasActiveExplosion(perpTile))
                        continue;

                    // Há explosão em perpTile. Verifica se não há parede entre
                    // perpTile e tile (explosão poderia chegar ao tile do lane).
                    bool blocked = false;
                    for (int bi = 1; bi < pk; bi++)
                    {
                        Vector2Int between = tile + perpDir * bi;
                        if (HasIndestructibleTile(between) || HasDestructibleTile(between))
                        {
                            blocked = true;
                            break;
                        }
                    }

                    if (!blocked)
                        return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Verifica se há um collider de explosão ativo no tile especificado,
    /// usando um raio um pouco maior para robustez contra imprecisões de posição.
    /// </summary>
    private bool LaneTileHasActiveExplosion(Vector2Int tile)
    {
        if (explosionMask == 0)
            return false;

        Collider2D exp = Physics2D.OverlapCircle(TileToWorld(tile), tileSize * 0.45f, explosionMask);
        return exp != null;
    }

    private bool IsKickLaneOpen(Vector2Int start, Vector2Int dir, int minOpenTiles)
    {
        int open = 0;
        for (int step = 1; step <= Mathf.Max(1, minOpenTiles); step++)
        {
            Vector2Int tile = start + dir * step;
            if (!HasGroundTile(tile) || HasIndestructibleTile(tile) || HasDestructibleTile(tile))
                return false;

            Bomb bomb = FindBombAt(tile);
            if (bomb != null && step > 1)
                return false;

            open++;
        }

        return open >= minOpenTiles;
    }

    private bool IsSafeWalkable(
        Vector2Int tile,
        Vector2Int startTile,
        BattleModeComDifficultySettings settings,
        int depth)
    {
        if (!IsWalkableTile(tile, startTile))
            return false;

        float eta = EstimateTraversalSeconds(depth);
        return !IsDangerousAt(tile, eta, settings);
    }

    private bool IsSafeAfterKickingBomb(
        Vector2Int vacatedBombTile,
        Bomb kickedBomb,
        BattleModeComDifficultySettings settings,
        int depth)
    {
        if (!HasGroundTile(vacatedBombTile))
            return false;

        if (HasIndestructibleTile(vacatedBombTile) || HasDestructibleTile(vacatedBombTile))
            return false;

        float eta = EstimateTraversalSeconds(depth);
        return !IsDangerousAt(vacatedBombTile, eta, settings, kickedBomb);
    }

    protected bool IsWalkableTile(Vector2Int tile, Vector2Int startTile)
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

    private bool IsDangerousAt(
        Vector2Int tile,
        float arrivalSeconds,
        BattleModeComDifficultySettings settings,
        Bomb ignoredBomb = null)
    {
        float dangerSeconds = GetDangerSeconds(tile, ignoredBomb);
        if (float.IsInfinity(dangerSeconds))
            return false;

        return dangerSeconds <= arrivalSeconds + settings.dangerReactionSeconds;
    }

    private float GetSurvivalMarginSeconds(
        Vector2Int tile,
        float arrivalSeconds,
        BattleModeComDifficultySettings settings)
    {
        float dangerSeconds = GetDangerSeconds(tile);
        if (float.IsInfinity(dangerSeconds))
            return 999f;

        return dangerSeconds - arrivalSeconds - settings.dangerReactionSeconds;
    }

    protected float GetDangerSeconds(Vector2Int tile, Bomb ignoredBomb = null)
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

            if (bomb == ignoredBomb)
                continue;

            Vector2Int bombTile = WorldToTile(bomb.GetLogicalPosition());
            int radius = bomb.Owner != null ? Mathf.Max(1, bomb.Owner.explosionRadius) : 2;
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

    protected Bomb FindBombAt(Vector2Int tile)
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

    private bool IsOwnEarlyKickBomb(Bomb bomb)
    {
        return bomb != null &&
               !bomb.HasExploded &&
               !bomb.IsBeingKicked &&
               !bomb.IsSolid &&
               bomb.CanBeKickedEarly &&
               bomb.Owner == bombController;
    }

    private bool TryStepTowardKickStand(
        Vector2Int from,
        Vector2Int standTile,
        Vector2Int bombTile,
        out Vector2Int direction)
    {
        direction = Vector2Int.zero;

        Vector2Int primary = StepToward(from, standTile);
        if (CanStepTowardKickStand(from, primary, bombTile))
        {
            direction = primary;
            return true;
        }

        Vector2Int delta = standTile - from;
        Vector2Int horizontal = delta.x != 0 ? new Vector2Int(Mathf.Clamp(delta.x, -1, 1), 0) : Vector2Int.zero;
        Vector2Int vertical = delta.y != 0 ? new Vector2Int(0, Mathf.Clamp(delta.y, -1, 1)) : Vector2Int.zero;

        if (CanStepTowardKickStand(from, horizontal, bombTile))
        {
            direction = horizontal;
            return true;
        }

        if (CanStepTowardKickStand(from, vertical, bombTile))
        {
            direction = vertical;
            return true;
        }

        return false;
    }

    private bool CanStepTowardKickStand(Vector2Int from, Vector2Int direction, Vector2Int bombTile)
    {
        if (!IsCardinalStep(direction))
            return false;

        Vector2Int next = from + direction;
        return next != bombTile && IsWalkableTile(next, from);
    }

    private bool CanKick(Bomb bomb)
    {
        // A IA chuta encostando na bomba (TryHandleBlockedHit só dispara quando o
        // movimento é BLOQUEADO por uma bomba SÓLIDA). Bombas não-sólidas são
        // atravessáveis: a IA andaria POR CIMA dela sem chutar e ficaria presa,
        // oscilando em cima da bomba até explodir. Por isso só consideramos bombas
        // realmente kickáveis (sólidas), nunca o early-kick (que exige uma troca de
        // direção que a IA não executa).
        return bomb != null &&
               !bomb.HasExploded &&
               !bomb.IsBeingKicked &&
               bomb.CanBeKicked;
    }

    protected virtual bool IsKickActivationComplete(Bomb bomb, Vector2Int originalBombTile)
    {
        return bomb != null && bomb.IsBeingKicked;
    }

    protected virtual bool TryBuildKickActivationInputDecision(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        Bomb bomb,
        Vector2Int bombTile,
        Vector2Int kickDirection,
        int weight,
        string reason,
        out BattleModeComAbilityDecision decision)
    {
        decision = default;
        return false;
    }

    private bool TryKickSequenceBombDirect(Bomb bomb, Vector2Int kickDirection, out string failReason)
    {
        failReason = string.Empty;

        if (kickAbility == null || !kickAbility.IsEnabled)
        {
            failReason = "kick ability unavailable";
            return false;
        }

        if (bomb == null)
        {
            failReason = "bomb null";
            return false;
        }

        if (bomb.HasExploded)
        {
            failReason = "bomb exploded";
            return false;
        }

        if (bomb.IsBeingKicked)
        {
            failReason = "already moving";
            return false;
        }

        if (!bomb.CanBeKicked && !bomb.CanBeKickedEarly)
        {
            failReason = $"not kickable solid:{bomb.IsSolid} can:{bomb.CanBeKicked} early:{bomb.CanBeKickedEarly}";
            return false;
        }

        if (!IsCardinalStep(kickDirection))
        {
            failReason = $"invalid dir {kickDirection}";
            return false;
        }

        if (!bomb.TryGetComponent(out Collider2D bombCollider) || bombCollider == null)
        {
            failReason = "bomb collider missing";
            return false;
        }

        Vector2 direction = TileDirectionToVector(kickDirection);
        LayerMask obstacleMask = movement != null ? movement.obstacleMask : 0;
        bool kicked = kickAbility.TryHandleBlockedHit(bombCollider, direction, tileSize, obstacleMask);
        if (!kicked)
        {
            Tilemap bombDestructibleTilemap = bombController != null
                ? bombController.destructibleTiles
                : destructibleTilemap;

            LayerMask blockMoveMask = LayerMask.GetMask("Player", "Stage", "Bomb", "Enemy", "Louie");
            bool fallbackKicked = bomb.StartKick(
                direction,
                tileSize,
                obstacleMask,
                bombDestructibleTilemap,
                blockMoveMask,
                0.60f,
                0.90f,
                false);

            if (!fallbackKicked)
            {
                failReason = "TryHandleBlockedHit false StartKick false";
                return false;
            }

            kickAbility.RegisterExternallyKickedBomb(bomb, playSfx: true);
            LogKickSurgical(
                "KICK_DIRECT_FALLBACK",
                $"dir:{kickDirection} bomb:{DescribeBomb(bomb)}",
                true);
        }

        return true;
    }

    private void EnsureDirectKickRetryWindow()
    {
        if (sequenceDirectKickRetryUntil < -9f)
            sequenceDirectKickRetryUntil = Time.time + DirectKickRetrySeconds;
    }

    private bool CanRetryDirectKick(Bomb bomb)
    {
        return bomb != null &&
               !bomb.HasExploded &&
               !bomb.IsBeingKicked &&
               (bomb.CanBeKicked || bomb.CanBeKickedEarly) &&
               bomb.RemainingFuseSeconds > DirectKickRetryMinFuseSeconds &&
               Time.time <= sequenceDirectKickRetryUntil;
    }

    protected bool HasGroundTile(Vector2Int tile)
    {
        if (groundTilemap == null)
            return true;

        return groundTilemap.HasTile(WorldToCell(groundTilemap, tile));
    }

    protected bool HasDestructibleTile(Vector2Int tile)
    {
        return destructibleTilemap != null && destructibleTilemap.HasTile(WorldToCell(destructibleTilemap, tile));
    }

    protected bool HasIndestructibleTile(Vector2Int tile)
    {
        return indestructibleTilemap != null && indestructibleTilemap.HasTile(WorldToCell(indestructibleTilemap, tile));
    }

    private int CountOpenNeighbors(Vector2Int tile)
    {
        int count = 0;
        for (int i = 0; i < CardinalTiles.Length; i++)
        {
            if (IsWalkableTile(tile + CardinalTiles[i], tile))
                count++;
        }

        return count;
    }

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

    private Vector3Int WorldToCell(Tilemap tilemap, Vector2Int tile)
    {
        return tilemap.WorldToCell(TileToWorld(tile));
    }

    protected Vector2Int WorldToTile(Vector2 world)
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

    private float EstimateEscapeTraversalSeconds(Vector2Int start, Vector2Int target, int depth)
    {
        if (depth <= 0 || target == start)
            return 0f;

        Vector2Int firstStep = ReconstructEscapeFirstStep(start, target);
        return EstimateTraversalSeconds(depth) + EstimateTurnAxisCenteringSeconds(firstStep);
    }

    private float EstimateEscapeTraversalSeconds(Vector2Int start, Vector2Int parent, Vector2Int next, int depth)
    {
        if (depth <= 0 || next == start)
            return 0f;

        Vector2Int firstStep = parent == start
            ? next - start
            : ReconstructEscapeFirstStep(start, parent);

        return EstimateTraversalSeconds(depth) + EstimateTurnAxisCenteringSeconds(firstStep);
    }

    private float EstimateFirstMoveTraversalSeconds(Vector2Int firstStep)
    {
        return EstimateTraversalSeconds(1) + EstimateTurnAxisCenteringSeconds(firstStep);
    }

    private float EstimateTurnAxisCenteringSeconds(Vector2Int requestedDirection)
    {
        if (movement == null || requestedDirection == Vector2Int.zero)
            return 0f;

        Vector2Int currentTile = WorldToTile(transform.position);
        Vector2 delta = TileToWorld(currentTile) - (Vector2)transform.position;
        float offAxisDistance = requestedDirection.x != 0
            ? Mathf.Abs(delta.y)
            : requestedDirection.y != 0
                ? Mathf.Abs(delta.x)
                : 0f;

        float tolerance = Mathf.Max(0.01f, tileSize * 0.045f);
        if (offAxisDistance <= tolerance)
            return 0f;

        return (offAxisDistance - tolerance) / Mathf.Max(1f, movement.speed);
    }

    private static Vector2Int StepToward(Vector2Int from, Vector2Int to)
    {
        Vector2Int delta = to - from;
        if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y) && delta.x != 0)
            return new Vector2Int(Mathf.Clamp(delta.x, -1, 1), 0);

        if (delta.y != 0)
            return new Vector2Int(0, Mathf.Clamp(delta.y, -1, 1));

        return Vector2Int.zero;
    }

    private static bool IsCardinalStep(Vector2Int direction)
    {
        return Mathf.Abs(direction.x) + Mathf.Abs(direction.y) == 1;
    }

    private static int Manhattan(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    protected static int DifficultyWeight(BattleModeComDifficultySettings settings)
    {
        return settings.difficulty switch
        {
            BattleModeComputerLevel.Easy => 0,
            BattleModeComputerLevel.Hard => 80,
            _ => 35
        };
    }

    private static float DifficultyChance(
        BattleModeComDifficultySettings settings,
        float easy,
        float normal,
        float hard)
    {
        return settings.difficulty switch
        {
            BattleModeComputerLevel.Easy => easy,
            BattleModeComputerLevel.Hard => hard,
            _ => normal
        };
    }

    protected static Vector2 TileDirectionToVector(Vector2Int direction)
    {
        if (direction == Vector2Int.up)
            return Vector2.up;

        if (direction == Vector2Int.down)
            return Vector2.down;

        if (direction == Vector2Int.left)
            return Vector2.left;

        if (direction == Vector2Int.right)
            return Vector2.right;

        return Vector2.zero;
    }

    protected static string FirstMoveDescription(Vector2 move)
    {
        if (move == Vector2.up)
            return "MoveUp";

        if (move == Vector2.down)
            return "MoveDown";

        if (move == Vector2.left)
            return "MoveLeft";

        if (move == Vector2.right)
            return "MoveRight";

        return "none";
    }

    protected static string AppendInput(string current, string next)
    {
        if (string.IsNullOrWhiteSpace(next) || next == "none")
            return string.IsNullOrWhiteSpace(current) ? "none" : current;

        if (string.IsNullOrWhiteSpace(current) || current == "none")
            return next;

        if (current.Contains(next, System.StringComparison.Ordinal))
            return current;

        return current + "+" + next;
    }

    private bool ShouldDeferToMountedYellowLouieKick()
    {
        if (this is BattleModeComYellowLouieKickAbility)
            return false;

        return TryGetComponent<BattleModeComYellowLouieKickAbility>(out var yellowCom) &&
               yellowCom != null &&
               yellowCom.IsMountedYellowLouieKickAvailable;
    }

    protected virtual string BuildAbilityAvailabilityTrace()
    {
        return $"kick:{(kickAbility != null)} kickEnabled:{(kickAbility != null && kickAbility.IsEnabled)}";
    }

    private string BuildAvailabilityTrace(string prefix)
    {
        return $"{prefix} {BuildAbilityAvailabilityTrace()} " +
               $"movement:{(movement != null)} bombController:{(bombController != null)}";
    }

    private void SetSequenceState(SequenceState state)
    {
        if (sequenceState != state)
        {
            sequenceStateStartedTime = Time.time;
            sequenceDirectKickRetryUntil = state == SequenceState.ReturnToKick
                ? Time.time + DirectKickRetrySeconds
                : -10f;
        }

        sequenceState = state;
    }

    private void ResetOffensiveSequence()
    {
        SetSequenceState(SequenceState.None);
        sequenceKicksCompleted = 0;
        sequenceTargetKickCount = 1;
        sequenceTrackedBomb = null;
        sequencePlantDirectionPatched = false;
        sequenceAllowActionRStop = false;
        sequenceDirectKickRetryUntil = -10f;
        postKickEscapeUntil = -10f;
        postKickEscapeLastTile = default;
        postKickEscapeStuckSince = -10f;
        postKickEscapeLastAttemptedStep = Vector2Int.zero;
        postKickEscapeBlockedSteps.Clear();
        // Invalida o cache de chance para que a próxima oportunidade ofensiva
        // receba um roll limpo, sem herdar o resultado da sequência encerrada.
        offensiveTriggerChanceCacheTime = -10f;
        offensiveTriggerChanceCacheResult = false;
        OnOffensiveSequenceReset();
    }

    protected virtual void OnOffensiveSequenceReset()
    {
    }

    private static void AppendTracePart(ref string trace, string part)
    {
        if (string.IsNullOrEmpty(trace))
            trace = part;
        else
            trace += "; " + part;
    }

    private static string FormatDanger(float seconds)
    {
        if (float.IsInfinity(seconds))
            return "safe";

        if (seconds <= 0f)
            return "now";

        return $"{seconds:F2}";
    }

    private string DescribeNearbyBombs(Vector2Int myTile)
    {
        string result = string.Empty;
        foreach (Bomb bomb in Bomb.ActiveBombs)
        {
            if (bomb == null || bomb.HasExploded)
                continue;

            Vector2Int tile = WorldToTile(bomb.GetLogicalPosition());
            int distance = Manhattan(myTile, tile);
            if (distance > 2)
                continue;

            AppendTracePart(
                ref result,
                $"{tile}/d{distance}/solid:{bomb.IsSolid}/can:{bomb.CanBeKicked}/early:{bomb.CanBeKickedEarly}/moving:{bomb.IsBeingKicked}");
        }

        return string.IsNullOrEmpty(result) ? "none" : result;
    }

    private string DescribeBomb(Bomb bomb)
    {
        if (bomb == null)
            return "null";

        Vector2Int tile = WorldToTile(bomb.GetLogicalPosition());
        return
            $"{tile}/solid:{bomb.IsSolid}/can:{bomb.CanBeKicked}/early:{bomb.CanBeKickedEarly}/moving:{bomb.IsBeingKicked}/fuse:{FormatDanger(bomb.RemainingFuseSeconds)}";
    }

    private void LogKickSurgical(string key, string message, bool force = false)
    {
        bool useRiskTag = EnableKickBombRiskDiagnostics;
        if (!useRiskTag && !EnableKickBombSurgicalDiagnostics)
            return;

        int id = identity != null ? Mathf.Clamp(identity.playerId, 1, 6) : 0;
        if (DiagnosticPlayerIdFilter != 0 && id != DiagnosticPlayerIdFilter)
            return;

        if (!force &&
            key == lastSurgicalLogKey &&
            Time.time - lastSurgicalLogTime < SurgicalLogIntervalSeconds)
        {
            return;
        }

        lastSurgicalLogKey = key;
        lastSurgicalLogTime = Time.time;
        Vector2Int tile = movement != null ? WorldToTile(movement.transform.position) : Vector2Int.zero;
        string tag = useRiskTag ? "BattleCOMKickRisk" : "BattleCOMKickSurgical";
        Debug.Log(
            $"[{tag}][P{id}] frame:{Time.frameCount} t:{Time.time:F2} " +
            $"tile:{tile} state:{sequenceState} key:{key} {message}",
            this);
    }
}

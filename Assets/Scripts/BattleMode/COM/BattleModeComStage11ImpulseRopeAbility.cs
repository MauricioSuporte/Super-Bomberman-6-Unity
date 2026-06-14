using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerIdentity))]
[RequireComponent(typeof(MovementController))]
public sealed class BattleModeComStage11ImpulseRopeAbility :
    MonoBehaviour,
    IBattleModeComStageAbility,
    IBattleModeComKickTrajectoryProvider
{
    private const float ImpulseUseChance = 0.45f;
    private const float ImpulseCommitSeconds = 8f;
    private const float ImpulseRetryMinSeconds = 2.5f;
    private const float ImpulseRetryMaxSeconds = 4.5f;
    private const float ImpulseTotalSeconds = 1.4f;
    private const float DiagnosticThrottleSeconds = 0.4f;
    private const int ImpulseTiles = 8;

    private static readonly Vector2Int[] CardinalTiles =
    {
        Vector2Int.up,
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.right
    };

    public float OwnChainPlanChance => 0.75f;
    public float PowerGloveChanceMultiplier => 2.4f;
    public float PowerGloveCooldownSeconds => 0.8f;
    public int PowerGloveWeightBonus => 90;
    public float PunchChanceMultiplier => 2.4f;
    public float PunchCooldownSeconds => 0.65f;
    public int PunchWeightBonus => 90;

    public float OffensiveKickChanceMultiplier => 2.5f;
    public int OffensiveKickWeightBonus => 100;
    public float OffensiveKickCooldownSeconds => 0.4f;
    public int MaxSequentialKickBombs => 3;
    public float RepeatKickChance => 0.9f;

    private PlayerIdentity identity;
    private MovementController movement;
    private BattleMode11ImpulseRopeController ropeController;

    private bool impulseCommitted;
    private Vector2Int committedApproachTile;
    private Vector2Int committedLandingTile;
    private Vector2Int committedImpactDirection;
    private float impulseCommitStartedTime = -10f;
    private float impulseHoldStartedTime = -10f;
    private float nextImpulseAttemptTime = -10f;
    private float lastDiagnosticTime = -10f;
    private string lastDiagnosticKey = string.Empty;
    private string lastDecisionTrace = "not evaluated";

    public bool IsAvailable
    {
        get
        {
            CacheReferences();
            return identity != null &&
                   movement != null &&
                   !movement.isDead &&
                   ropeController != null &&
                   ropeController.isActiveAndEnabled;
        }
    }

    public string DiagnosticName => "Stage11ImpulseRope";
    public string LastDecisionTrace => lastDecisionTrace;

    private void Awake()
    {
        CacheReferences();
        ScheduleNextImpulse();
    }

    private void OnEnable()
    {
        CacheReferences();
        ScheduleNextImpulse();
    }

    private void OnDisable() => ResetImpulseCommit();

    private void CacheReferences()
    {
        if (identity == null)
            TryGetComponent(out identity);

        if (movement == null)
            TryGetComponent(out movement);

        if (ropeController == null)
        {
            ropeController =
                FindAnyObjectByType<BattleMode11ImpulseRopeController>();
        }
    }

    public void ApplyAggressionSettings(
        BattleModeComDifficultySettings settings)
    {
        if (settings == null)
            return;

        settings.decisionInterval =
            Mathf.Max(0.08f, settings.decisionInterval * 0.75f);
        settings.hesitationChance *= 0.4f;
        settings.stoppedWeight =
            Mathf.Max(1, Mathf.RoundToInt(settings.stoppedWeight * 0.4f));
        settings.patrolWeight =
            Mathf.Max(1, Mathf.RoundToInt(settings.patrolWeight * 0.65f));
        settings.farmDestructibleWeight =
            Mathf.Max(
                1,
                Mathf.RoundToInt(
                    settings.farmDestructibleWeight * 0.75f));
        settings.combatPlantWeight =
            Mathf.Max(
                settings.combatPlantWeight + 15,
                Mathf.RoundToInt(settings.combatPlantWeight * 1.55f));
    }

    public bool TryGetRedirectedKickDirection(
        Vector2Int tile,
        Vector2Int incomingDirection,
        out Vector2Int redirectedDirection)
    {
        redirectedDirection = incomingDirection;
        if (!IsRopeImpact(tile, incomingDirection))
            return false;

        redirectedDirection = -incomingDirection;
        return true;
    }

    public bool TryBuildEmergencyDecision(
        BattleModeComDifficultySettings settings,
        BattleModeComController controller,
        Vector2Int myTile,
        float currentDangerSeconds,
        out BattleModeComAbilityDecision decision)
    {
        decision = default;
        if (!IsAvailable ||
            controller == null ||
            float.IsInfinity(currentDangerSeconds) ||
            currentDangerSeconds <= ImpulseTotalSeconds + 0.15f)
        {
            lastDecisionTrace =
                $"emergency impulse unavailable danger:{FormatDanger(currentDangerSeconds)}";
            return false;
        }

        if (!TryFindImmediateImpulse(
                controller,
                settings,
                myTile,
                out Vector2Int impactDirection,
                out Vector2Int landingTile,
                out string trace))
        {
            lastDecisionTrace =
                $"emergency no safe immediate impulse {trace}";
            return false;
        }

        CommitImpulse(myTile, impactDirection, landingTile);
        decision = BuildHoldDecision(
            myTile,
            impactDirection,
            landingTile,
            weight: 1200,
            reason: "stage 11 rope impulse emergency");
        lastDecisionTrace =
            $"emergency impulse {myTile}->{landingTile} " +
            $"impact:{impactDirection} danger:{currentDangerSeconds:F2}s";
        LogDiagnostic(
            "IMPULSE_EMERGENCY",
            $"approach:{myTile} landing:{landingTile} " +
            $"impact:{DirectionLabel(impactDirection)} " +
            $"danger:{currentDangerSeconds:F2}s {trace}",
            force: true);
        return true;
    }

    public bool TryBuildCandidateDecision(
        BattleModeComDifficultySettings settings,
        BattleModeComController controller,
        Vector2Int myTile,
        out BattleModeComAbilityDecision decision)
    {
        decision = default;
        if (!IsAvailable || controller == null)
        {
            lastDecisionTrace = "candidate impulse unavailable";
            return false;
        }

        if (impulseCommitted &&
            impulseHoldStartedTime > 0f &&
            Time.time - impulseHoldStartedTime > ImpulseTotalSeconds)
        {
            ResetImpulseCommit();
            ScheduleNextImpulse();
        }

        if (impulseCommitted &&
            Time.time - impulseCommitStartedTime <= ImpulseCommitSeconds)
        {
            return TryContinueImpulseCommit(
                settings,
                controller,
                myTile,
                out decision);
        }

        ResetImpulseCommit();
        if (Time.time < nextImpulseAttemptTime)
        {
            lastDecisionTrace =
                $"candidate impulse cooldown " +
                $"{nextImpulseAttemptTime - Time.time:F2}s";
            return false;
        }

        ScheduleNextImpulse();
        if (Random.value > ImpulseUseChance)
        {
            lastDecisionTrace =
                $"candidate impulse chance failed:{ImpulseUseChance:F2}";
            return false;
        }

        if (!TryFindBestImpulsePlan(
                settings,
                controller,
                myTile,
                out Vector2Int approachTile,
                out Vector2Int impactDirection,
                out Vector2Int landingTile,
                out Vector2 firstMove,
                out int distance,
                out string trace))
        {
            lastDecisionTrace = $"candidate no safe rope plan {trace}";
            LogDiagnostic("IMPULSE_NO_PLAN", trace);
            return false;
        }

        CommitImpulse(approachTile, impactDirection, landingTile);
        if (myTile == approachTile)
        {
            decision = BuildHoldDecision(
                approachTile,
                impactDirection,
                landingTile,
                weight: 520,
                reason: "stage 11 rope impulse");
        }
        else
        {
            decision = new BattleModeComAbilityDecision
            {
                Action = BattleModeComActionType.Reposition,
                Weight = 210,
                TargetTile = approachTile,
                HasTarget = true,
                FirstMove = firstMove,
                Reason = "stage 11 rope impulse approach",
                InputDescription = FirstMoveDescription(firstMove)
            };
        }

        lastDecisionTrace =
            $"candidate impulse approach:{approachTile} " +
            $"landing:{landingTile} distance:{distance}";
        LogDiagnostic(
            "IMPULSE_SELECT",
            $"my:{myTile} approach:{approachTile} " +
            $"landing:{landingTile} " +
            $"impact:{DirectionLabel(impactDirection)} " +
            $"distance:{distance} move:{FirstMoveDescription(firstMove)} " +
            trace,
            force: true);
        return true;
    }

    public bool CanUseCommittedImpulseInput(
        BattleModeComDifficultySettings settings,
        BattleModeComController controller,
        Vector2Int currentTile,
        Vector2Int requestedDirection,
        out string trace)
    {
        trace = "no committed impulse";
        if (!impulseCommitted ||
            controller == null ||
            currentTile != committedApproachTile ||
            requestedDirection != committedImpactDirection)
        {
            return false;
        }

        if (!TryResolveLanding(
                controller,
                settings,
                committedApproachTile,
                committedImpactDirection,
                out Vector2Int landingTile,
                out trace) ||
            landingTile != committedLandingTile)
        {
            ResetImpulseCommit();
            return false;
        }

        impulseHoldStartedTime =
            impulseHoldStartedTime < 0f
                ? Time.time
                : impulseHoldStartedTime;
        trace =
            $"approach:{committedApproachTile} " +
            $"landing:{committedLandingTile} " +
            $"hold:{Time.time - impulseHoldStartedTime:F2}s";
        LogDiagnostic("IMPULSE_HOLD", trace);
        return true;
    }

    private bool TryContinueImpulseCommit(
        BattleModeComDifficultySettings settings,
        BattleModeComController controller,
        Vector2Int myTile,
        out BattleModeComAbilityDecision decision)
    {
        decision = default;
        if (myTile == committedApproachTile)
        {
            if (!TryResolveLanding(
                    controller,
                    settings,
                    committedApproachTile,
                    committedImpactDirection,
                    out Vector2Int landingTile,
                    out string trace) ||
                landingTile != committedLandingTile)
            {
                lastDecisionTrace =
                    $"candidate committed impulse invalid {trace}";
                ResetImpulseCommit();
                return false;
            }

            decision = BuildHoldDecision(
                committedApproachTile,
                committedImpactDirection,
                committedLandingTile,
                weight: 520,
                reason: "stage 11 rope impulse");
            lastDecisionTrace =
                $"candidate holding rope impact:{committedImpactDirection} " +
                $"landing:{committedLandingTile}";
            return true;
        }

        if (!controller.TryFindAbilityRouteToDangerousGoal(
                settings,
                myTile,
                committedApproachTile,
                6,
                out Vector2 firstMove,
                out int distance,
                out float arrivalSeconds,
                out string route) ||
            controller.IsAbilityTileDangerousAt(
                committedApproachTile,
                arrivalSeconds,
                settings))
        {
            lastDecisionTrace =
                $"candidate committed approach blocked {route}";
            ResetImpulseCommit();
            return false;
        }

        decision = new BattleModeComAbilityDecision
        {
            Action = BattleModeComActionType.Reposition,
            Weight = 240,
            TargetTile = committedApproachTile,
            HasTarget = true,
            FirstMove = firstMove,
            Reason = "stage 11 rope impulse approach",
            InputDescription = FirstMoveDescription(firstMove)
        };
        lastDecisionTrace =
            $"candidate continue impulse approach:{committedApproachTile} " +
            $"distance:{distance}";
        return true;
    }

    private bool TryFindBestImpulsePlan(
        BattleModeComDifficultySettings settings,
        BattleModeComController controller,
        Vector2Int myTile,
        out Vector2Int bestApproach,
        out Vector2Int bestImpact,
        out Vector2Int bestLanding,
        out Vector2 bestFirstMove,
        out int bestDistance,
        out string trace)
    {
        bestApproach = myTile;
        bestImpact = Vector2Int.zero;
        bestLanding = myTile;
        bestFirstMove = Vector2.zero;
        bestDistance = int.MaxValue;
        trace = "no candidate";

        for (int i = 0; i < CardinalTiles.Length; i++)
        {
            Vector2Int impact = CardinalTiles[i];
            GetApproachRange(
                impact,
                out Vector2Int rangeStart,
                out Vector2Int rangeEnd);
            Vector2Int step = new(
                Mathf.Clamp(rangeEnd.x - rangeStart.x, -1, 1),
                Mathf.Clamp(rangeEnd.y - rangeStart.y, -1, 1));
            Vector2Int approach = rangeStart;
            while (true)
            {
                int directDistance =
                    Mathf.Abs(approach.x - myTile.x) +
                    Mathf.Abs(approach.y - myTile.y);
                if (directDistance <= settings.searchDepth + 6 &&
                    TryResolveLanding(
                        controller,
                        settings,
                        approach,
                        impact,
                        out Vector2Int landing,
                        out string landingTrace) &&
                    controller.TryFindAbilityRouteToDangerousGoal(
                        settings,
                        myTile,
                        approach,
                        6,
                        out Vector2 firstMove,
                        out int distance,
                        out float arrivalSeconds,
                        out string route) &&
                    !controller.IsAbilityTileDangerousAt(
                        approach,
                        arrivalSeconds,
                        settings) &&
                    distance < bestDistance)
                {
                    bestApproach = approach;
                    bestImpact = impact;
                    bestLanding = landing;
                    bestFirstMove = firstMove;
                    bestDistance = distance;
                    trace =
                        $"route:{route} arrival:{arrivalSeconds:F2}s " +
                        landingTrace;
                }

                if (approach == rangeEnd)
                    break;

                approach += step;
            }
        }

        return bestImpact != Vector2Int.zero;
    }

    private bool TryFindImmediateImpulse(
        BattleModeComController controller,
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        out Vector2Int impactDirection,
        out Vector2Int landingTile,
        out string trace)
    {
        for (int i = 0; i < CardinalTiles.Length; i++)
        {
            Vector2Int impact = CardinalTiles[i];
            if (!IsApproachTile(myTile, impact))
                continue;

            if (TryResolveLanding(
                    controller,
                    settings,
                    myTile,
                    impact,
                    out landingTile,
                    out trace))
            {
                impactDirection = impact;
                return true;
            }
        }

        impactDirection = Vector2Int.zero;
        landingTile = myTile;
        trace = "not adjacent to a safe rope";
        return false;
    }

    private static bool TryResolveLanding(
        BattleModeComController controller,
        BattleModeComDifficultySettings settings,
        Vector2Int approachTile,
        Vector2Int impactDirection,
        out Vector2Int landingTile,
        out string trace)
    {
        landingTile = approachTile;
        Vector2Int launchDirection = -impactDirection;
        int traveled = 0;
        for (int i = 0; i < ImpulseTiles; i++)
        {
            Vector2Int next = landingTile + launchDirection;
            if (!controller.IsAbilityTileWalkable(next, approachTile))
                break;

            landingTile = next;
            traveled++;
        }

        if (traveled < 2)
        {
            trace = $"landing blocked traveled:{traveled}";
            return false;
        }

        bool dangerous =
            controller.IsAbilityTileDangerousAt(
                landingTile,
                ImpulseTotalSeconds,
                settings);
        trace =
            $"landing:{landingTile} traveled:{traveled} " +
            $"danger:{FormatDanger(controller.GetAbilityDangerSeconds(landingTile))}";
        return !dangerous;
    }

    private static BattleModeComAbilityDecision BuildHoldDecision(
        Vector2Int approachTile,
        Vector2Int impactDirection,
        Vector2Int landingTile,
        int weight,
        string reason)
    {
        Vector2 move = new(impactDirection.x, impactDirection.y);
        return new BattleModeComAbilityDecision
        {
            Action = BattleModeComActionType.Reposition,
            Weight = weight,
            TargetTile = approachTile,
            HasTarget = true,
            FirstMove = move,
            Reason = reason,
            InputDescription = FirstMoveDescription(move)
        };
    }

    private void CommitImpulse(
        Vector2Int approachTile,
        Vector2Int impactDirection,
        Vector2Int landingTile)
    {
        impulseCommitted = true;
        committedApproachTile = approachTile;
        committedImpactDirection = impactDirection;
        committedLandingTile = landingTile;
        impulseCommitStartedTime = Time.time;
        impulseHoldStartedTime = -10f;
    }

    private void ResetImpulseCommit()
    {
        impulseCommitted = false;
        committedImpactDirection = Vector2Int.zero;
        impulseCommitStartedTime = -10f;
        impulseHoldStartedTime = -10f;
    }

    private void ScheduleNextImpulse()
    {
        nextImpulseAttemptTime =
            Time.time +
            Random.Range(
                ImpulseRetryMinSeconds,
                ImpulseRetryMaxSeconds);
    }

    private static bool IsApproachTile(
        Vector2Int tile,
        Vector2Int impactDirection)
    {
        if (impactDirection == Vector2Int.up)
            return tile.y == 4 && tile.x >= -7 && tile.x <= 5;

        if (impactDirection == Vector2Int.right)
            return tile.x == 5 && tile.y >= -6 && tile.y <= 4;

        if (impactDirection == Vector2Int.down)
            return tile.y == -6 && tile.x >= -7 && tile.x <= 5;

        return impactDirection == Vector2Int.left &&
               tile.x == -7 &&
               tile.y >= -6 &&
               tile.y <= 4;
    }

    private static bool IsRopeImpact(
        Vector2Int tile,
        Vector2Int incomingDirection)
    {
        if (incomingDirection == Vector2Int.up)
            return tile.y == 5 && tile.x >= -7 && tile.x <= 5;

        if (incomingDirection == Vector2Int.right)
            return tile.x == 6 && tile.y >= -6 && tile.y <= 4;

        if (incomingDirection == Vector2Int.down)
            return tile.y == -7 && tile.x >= -7 && tile.x <= 5;

        return incomingDirection == Vector2Int.left &&
               tile.x == -8 &&
               tile.y >= -6 &&
               tile.y <= 4;
    }

    private static void GetApproachRange(
        Vector2Int impactDirection,
        out Vector2Int start,
        out Vector2Int end)
    {
        if (impactDirection == Vector2Int.up)
        {
            start = new Vector2Int(-7, 4);
            end = new Vector2Int(5, 4);
            return;
        }

        if (impactDirection == Vector2Int.right)
        {
            start = new Vector2Int(5, -6);
            end = new Vector2Int(5, 4);
            return;
        }

        if (impactDirection == Vector2Int.down)
        {
            start = new Vector2Int(-7, -6);
            end = new Vector2Int(5, -6);
            return;
        }

        start = new Vector2Int(-7, -6);
        end = new Vector2Int(-7, 4);
    }

    private void LogDiagnostic(
        string key,
        string message,
        bool force = false)
    {
        if (!force &&
            key == lastDiagnosticKey &&
            Time.time - lastDiagnosticTime <
            DiagnosticThrottleSeconds)
        {
            return;
        }

        lastDiagnosticKey = key;
        lastDiagnosticTime = Time.time;
        int playerId = identity != null ? identity.playerId : 0;
        Debug.LogWarning(
            $"[BattleCOMStage11Rope][P{playerId}] " +
            $"frame:{Time.frameCount} t:{Time.time:F2} " +
            $"key:{key} {message}",
            this);
    }

    private static string DirectionLabel(Vector2Int direction)
    {
        if (direction == Vector2Int.up)
            return "Up";
        if (direction == Vector2Int.down)
            return "Down";
        if (direction == Vector2Int.left)
            return "Left";
        return direction == Vector2Int.right ? "Right" : "None";
    }

    private static string FirstMoveDescription(Vector2 move)
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

    private static string FormatDanger(float seconds)
    {
        if (float.IsInfinity(seconds))
            return "safe";

        return seconds <= 0f ? "now" : $"{seconds:F2}s";
    }
}

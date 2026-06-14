using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerIdentity))]
[RequireComponent(typeof(MovementController))]
[RequireComponent(typeof(BombController))]
[RequireComponent(typeof(BattleModeComController))]
public sealed class BattleModeComStage9MinecartAbility :
    MonoBehaviour,
    IBattleModeComStageAbility,
    IBattleModeComDangerProvider
{
    public static readonly bool EnableMinecartDiagnostics = true;
    public const int DiagnosticPlayerIdFilter = 0;

    private const float PunishRideChance = 0.45f;
    private const float PunishEarlyToleranceSeconds = 0.9f;
    private const float PunishLateToleranceSeconds = 0.35f;
    private const float PunishMinimumExitLeadSeconds = 0.35f;
    private const float InitialCartUseDelayMinSeconds = 3f;
    private const float InitialCartUseDelayMaxSeconds = 6f;
    private const float CartUseRetryMinSeconds = 4f;
    private const float CartUseRetryMaxSeconds = 7f;
    private const float CartUseCooldownMinSeconds = 10f;
    private const float CartUseCooldownMaxSeconds = 16f;
    private const float CartCommitBlockedRetrySeconds = 0.65f;
    private const float CartCommitTimeoutSeconds = 12f;
    private const float DiagnosticThrottleSeconds = 0.5f;

    private readonly List<Vector2Int> railTiles = new();
    private readonly HashSet<Vector2Int> railTileSet = new();

    private PlayerIdentity identity;
    private MovementController movement;
    private BombController bombController;
    private BattleModeComController comController;
    private BattleMode9MinecartController minecartController;

    private MovementController observedRider;
    private bool punishThisRide;
    private bool punishCommit;
    private bool cartUseCommit;
    private float cartUseCommitStartedTime = -10f;
    private float cartUseBlockedUntilTime = -10f;
    private float nextCartUseTime = -10f;
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
                   bombController != null &&
                   comController != null &&
                   minecartController != null &&
                   minecartController.isActiveAndEnabled;
        }
    }

    public string DiagnosticName => "Stage9Minecart";
    public string LastDecisionTrace => lastDecisionTrace;
    public bool HasCartUseCommit => cartUseCommit;

    private void Awake()
    {
        CacheReferences();
        ScheduleInitialCartUse();
    }

    private void OnEnable()
    {
        CacheReferences();
        ScheduleInitialCartUse();
    }

    private void CacheReferences()
    {
        if (identity == null)
            TryGetComponent(out identity);

        if (movement == null)
            TryGetComponent(out movement);

        if (bombController == null)
            TryGetComponent(out bombController);

        if (comController == null)
            TryGetComponent(out comController);

        if (minecartController == null)
        {
            minecartController =
                FindAnyObjectByType<BattleMode9MinecartController>();
        }

        if (minecartController != null && railTiles.Count == 0)
            RefreshRailTiles();
    }

    private void RefreshRailTiles()
    {
        railTiles.Clear();
        railTileSet.Clear();
        minecartController.CopyRailTiles(railTiles);
        for (int i = 0; i < railTiles.Count; i++)
            railTileSet.Add(railTiles[i]);
    }

    private void ScheduleInitialCartUse()
    {
        if (nextCartUseTime > -9f)
            return;

        nextCartUseTime =
            Time.time +
            Random.Range(
                InitialCartUseDelayMinSeconds,
                InitialCartUseDelayMaxSeconds);
    }

    public bool TryGetDangerSeconds(
        Vector2Int tile,
        out float dangerSeconds)
    {
        dangerSeconds = float.PositiveInfinity;
        if (!IsAvailable)
            return false;

        UpdateRideObservation();
        if (!minecartController.RideActive ||
            minecartController.CurrentRider == movement ||
            !railTileSet.Contains(tile) ||
            !minecartController.TryGetSecondsUntilCartReachesTile(
                tile,
                out float arrivalSeconds))
        {
            return false;
        }

        dangerSeconds = Mathf.Max(0.05f, arrivalSeconds);
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
        UpdateRideObservation();

        if (IsAvailable &&
            punishCommit &&
            myTile == minecartController.ExitTile &&
            !minecartController.SuddenDeathStarted &&
            minecartController.RideActive &&
            minecartController.CurrentRider != movement &&
            TryBuildPunishDecision(
                settings,
                controller,
                myTile,
                out decision) &&
            decision.TapBomb)
        {
            lastDecisionTrace =
                $"emergency completed punish plant at {myTile}";
            LogDiagnostic(
                "PUNISH_EMERGENCY_PLANT",
                $"exit:{myTile} " +
                $"exitIn:{FormatSeconds(minecartController.EstimateSecondsUntilExit())}",
                force: true);
            return true;
        }

        if (!IsAvailable ||
            float.IsInfinity(currentDangerSeconds) ||
            !railTileSet.Contains(myTile))
        {
            lastDecisionTrace = "emergency outside active minecart rail";
            return false;
        }

        lastDecisionTrace =
            $"emergency rail dangerIn:{currentDangerSeconds:F2}s " +
            $"cart:{minecartController.GetCurrentCartTile()} " +
            $"exitIn:{FormatSeconds(minecartController.EstimateSecondsUntilExit())}";
        LogDiagnostic(
            "RAIL_DANGER",
            $"tile:{myTile} dangerIn:{currentDangerSeconds:F2}s " +
            $"cart:{minecartController.GetCurrentCartTile()} " +
            $"exitIn:{FormatSeconds(minecartController.EstimateSecondsUntilExit())}");
        return false;
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
            lastDecisionTrace = "candidate unavailable";
            return false;
        }

        UpdateRideObservation();

        if (minecartController.SuddenDeathStarted)
        {
            bool hadCommit = punishCommit || cartUseCommit;
            punishCommit = false;
            cartUseCommit = false;
            cartUseCommitStartedTime = -10f;
            lastDecisionTrace =
                "candidate minecart blocked by sudden death";
            LogDiagnostic(
                "SUDDEN_DEATH_BLOCK",
                $"my:{myTile} ride:{minecartController.RideActive} " +
                $"cartDestroyed:{minecartController.CartDestroyedBySuddenDeath} " +
                $"cancelledCommit:{hadCommit}",
                force: hadCommit);
            return false;
        }

        if (minecartController.CurrentRider == movement)
        {
            punishCommit = false;
            cartUseCommit = false;
            cartUseCommitStartedTime = -10f;
            nextCartUseTime =
                Time.time +
                Random.Range(
                    CartUseCooldownMinSeconds,
                    CartUseCooldownMaxSeconds);
            lastDecisionTrace =
                $"candidate riding cart exitIn:" +
                $"{FormatSeconds(minecartController.EstimateSecondsUntilExit())}";
            LogDiagnostic(
                "SELF_RIDING",
                $"cart:{minecartController.GetCurrentCartTile()} " +
                $"exitIn:{FormatSeconds(minecartController.EstimateSecondsUntilExit())}");
            return false;
        }

        if (minecartController.RideActive)
        {
            cartUseCommit = false;
            if (TryBuildPunishDecision(
                    settings,
                    controller,
                    myTile,
                    out decision))
            {
                return true;
            }

            return false;
        }

        punishCommit = false;
        return TryBuildCartUseDecision(
            settings,
            controller,
            myTile,
            out decision);
    }

    private void UpdateRideObservation()
    {
        MovementController rider =
            minecartController != null
                ? minecartController.CurrentRider
                : null;
        if (rider == observedRider)
            return;

        observedRider = rider;
        punishCommit = false;
        if (rider == null)
        {
            punishThisRide = false;
            LogDiagnostic("RIDE_END", "cart returned to station", force: true);
            return;
        }

        bool enemyRider = rider != movement;
        punishThisRide =
            enemyRider &&
            Random.value < PunishRideChance;
        int riderId = rider.TryGetComponent(
            out PlayerIdentity riderIdentity)
            ? riderIdentity.playerId
            : 0;
        LogDiagnostic(
            "RIDE_START",
            $"rider:P{riderId} enemy:{enemyRider} " +
            $"punishRoll:{punishThisRide} chance:{PunishRideChance:F2} " +
            $"exitIn:{FormatSeconds(minecartController.EstimateSecondsUntilExit())}",
            force: true);
        if (minecartController.TryGetSecondsUntilCartReachesTile(
                WorldToTile(movement.transform.position),
                out float dangerSeconds))
        {
            LogDiagnostic(
                "RIDE_ALERT",
                $"rider:P{riderId} myDangerIn:{dangerSeconds:F2}s " +
                $"myTile:{WorldToTile(movement.transform.position)} " +
                $"cart:{minecartController.GetCurrentCartTile()}",
                force: true);
        }
    }

    private bool TryBuildPunishDecision(
        BattleModeComDifficultySettings settings,
        BattleModeComController controller,
        Vector2Int myTile,
        out BattleModeComAbilityDecision decision)
    {
        decision = default;
        if (!punishThisRide)
        {
            lastDecisionTrace = "candidate enemy ride observed punish roll false";
            return false;
        }

        if (bombController.BombsRemaining <= 0)
        {
            punishCommit = false;
            lastDecisionTrace = "candidate punish rejected no bombs";
            LogDiagnostic("PUNISH_NO_BOMBS", $"my:{myTile}");
            return false;
        }

        Vector2Int exitTile = minecartController.ExitTile;
        float exitSeconds = minecartController.EstimateSecondsUntilExit();
        if (float.IsInfinity(exitSeconds) ||
            exitSeconds <= PunishMinimumExitLeadSeconds)
        {
            punishCommit = false;
            punishThisRide = false;
            lastDecisionTrace =
                $"candidate punish too late exitIn:{FormatSeconds(exitSeconds)}";
            LogDiagnostic(
                "PUNISH_TOO_LATE",
                $"my:{myTile} exit:{exitTile} " +
                $"exitIn:{FormatSeconds(exitSeconds)}",
                force: true);
            return false;
        }

        if (!controller.TryFindAbilityRouteToDangerousGoal(
                settings,
                myTile,
                exitTile,
                8,
                out Vector2 firstMove,
                out int distance,
                out float arrivalSeconds,
                out string route))
        {
            punishCommit = false;
            lastDecisionTrace =
                $"candidate punish no route exit:{exitTile} {route}";
            LogDiagnostic(
                "PUNISH_NO_ROUTE",
                $"my:{myTile} exit:{exitTile} route:{route}");
            return false;
        }

        float bombFuse = controller.GetAbilityBombFuseSeconds();
        float detonationBeforeExit =
            exitSeconds - arrivalSeconds;
        bool timingAligned =
            detonationBeforeExit >=
                bombFuse - PunishLateToleranceSeconds &&
            detonationBeforeExit <=
                bombFuse + PunishEarlyToleranceSeconds;

        if (!timingAligned)
        {
            if (detonationBeforeExit <
                bombFuse - PunishLateToleranceSeconds)
            {
                punishCommit = false;
                punishThisRide = false;
            }

            lastDecisionTrace =
                $"candidate punish timing wait exitIn:{exitSeconds:F2}s " +
                $"arrival:{arrivalSeconds:F2}s fuse:{bombFuse:F2}s " +
                $"detonationDelta:{detonationBeforeExit:F2}s";
            LogDiagnostic(
                "PUNISH_TIMING",
                $"my:{myTile} exit:{exitTile} distance:{distance} " +
                $"exitIn:{exitSeconds:F2}s arrival:{arrivalSeconds:F2}s " +
                $"fuse:{bombFuse:F2}s delta:{detonationBeforeExit:F2}s " +
                $"commit:{punishCommit}");
            return false;
        }

        if (myTile == exitTile)
        {
            if (!controller.TryPlanAbilityBombWithEscape(
                    exitTile,
                    settings,
                    out Vector2 escapeMove,
                    out Vector2Int escapeTile))
            {
                punishCommit = false;
                lastDecisionTrace =
                    $"candidate punish at exit but no bomb escape " +
                    $"exitIn:{exitSeconds:F2}s";
                LogDiagnostic(
                    "PUNISH_NO_ESCAPE",
                    $"exit:{exitTile} exitIn:{exitSeconds:F2}s " +
                    $"fuse:{bombFuse:F2}s",
                    force: true);
                return false;
            }

            punishCommit = false;
            punishThisRide = false;
            decision = new BattleModeComAbilityDecision
            {
                Action = BattleModeComActionType.CombatPlant,
                Weight = settings.combatPlantWeight + 520,
                TargetTile = exitTile,
                HasTarget = true,
                FirstMove = escapeMove,
                Reason = "stage 9 minecart punish exit plant",
                InputDescription =
                    $"ActionA+{FirstMoveDescription(escapeMove)}",
                TapBomb = true
            };
            lastDecisionTrace =
                $"candidate punish plant exit:{exitTile} " +
                $"escape:{escapeTile} exitIn:{exitSeconds:F2}s " +
                $"fuse:{bombFuse:F2}s";
            LogDiagnostic(
                "PUNISH_PLANT",
                $"exit:{exitTile} escape:{escapeTile} " +
                $"move:{FirstMoveDescription(escapeMove)} " +
                $"exitIn:{exitSeconds:F2}s fuse:{bombFuse:F2}s",
                force: true);
            return true;
        }

        bool continuingPunish = punishCommit;
        punishCommit = true;
        decision = new BattleModeComAbilityDecision
        {
            Action = BattleModeComActionType.Reposition,
            Weight = settings.combatPlantWeight + 240,
            TargetTile = exitTile,
            HasTarget = true,
            FirstMove = firstMove,
            Reason = "stage 9 minecart punish exit approach",
            InputDescription = FirstMoveDescription(firstMove)
        };
        lastDecisionTrace =
            $"candidate punish approach exit:{exitTile} " +
            $"distance:{distance} arrival:{arrivalSeconds:F2}s " +
            $"exitIn:{exitSeconds:F2}s route:{route}";
        LogDiagnostic(
            continuingPunish ? "PUNISH_CONTINUE" : "PUNISH_SELECT",
            $"my:{myTile} exit:{exitTile} distance:{distance} " +
            $"move:{FirstMoveDescription(firstMove)} " +
            $"arrival:{arrivalSeconds:F2}s exitIn:{exitSeconds:F2}s " +
            $"fuse:{bombFuse:F2}s",
            force: !continuingPunish);
        return true;
    }

    private bool TryBuildCartUseDecision(
        BattleModeComDifficultySettings settings,
        BattleModeComController controller,
        Vector2Int myTile,
        out BattleModeComAbilityDecision decision)
    {
        decision = default;
        if (!minecartController.CartAvailable)
        {
            cartUseCommit = false;
            cartUseCommitStartedTime = -10f;
            lastDecisionTrace = "candidate cart unavailable";
            return false;
        }

        if (cartUseCommit &&
            Time.time - cartUseCommitStartedTime >
            CartCommitTimeoutSeconds)
        {
            LogDiagnostic(
                "CART_COMMIT_TIMEOUT",
                $"my:{myTile} station:{minecartController.StationTile} " +
                $"age:{Time.time - cartUseCommitStartedTime:F2}s",
                force: true);
            cartUseCommit = false;
            cartUseCommitStartedTime = -10f;
            nextCartUseTime =
                Time.time +
                Random.Range(
                    CartUseRetryMinSeconds,
                    CartUseRetryMaxSeconds);
            return false;
        }

        if (cartUseCommit &&
            Time.time < cartUseBlockedUntilTime)
        {
            lastDecisionTrace =
                $"candidate cart committed waiting route retry:" +
                $"{cartUseBlockedUntilTime - Time.time:F2}s";
            return false;
        }

        if (!cartUseCommit && Time.time < nextCartUseTime)
        {
            lastDecisionTrace =
                $"candidate cart use cooldown:" +
                $"{nextCartUseTime - Time.time:F2}s";
            return false;
        }

        Vector2Int stationTile = minecartController.StationTile;
        if (!controller.TryFindAbilityRouteToDangerousGoal(
                settings,
                myTile,
                stationTile,
                10,
                out Vector2 firstMove,
                out int distance,
                out float arrivalSeconds,
                out string route))
        {
            if (cartUseCommit)
            {
                cartUseBlockedUntilTime =
                    Time.time +
                    CartCommitBlockedRetrySeconds;
                lastDecisionTrace =
                    $"candidate cart committed route temporarily blocked " +
                    $"retry:{CartCommitBlockedRetrySeconds:F2}s {route}";
                LogDiagnostic(
                    "CART_COMMIT_BLOCKED",
                    $"my:{myTile} station:{stationTile} " +
                    $"age:{Time.time - cartUseCommitStartedTime:F2}s " +
                    $"retry:{CartCommitBlockedRetrySeconds:F2}s route:{route}");
                return false;
            }

            nextCartUseTime =
                Time.time +
                Random.Range(
                    CartUseRetryMinSeconds,
                    CartUseRetryMaxSeconds);
            lastDecisionTrace =
                $"candidate cart no route station:{stationTile} {route}";
            LogDiagnostic(
                "CART_NO_ROUTE",
                $"my:{myTile} station:{stationTile} route:{route}");
            return false;
        }

        if (!cartUseCommit &&
            Random.value >= 0.45f)
        {
            nextCartUseTime =
                Time.time +
                Random.Range(
                    CartUseRetryMinSeconds,
                    CartUseRetryMaxSeconds);
            lastDecisionTrace =
                $"candidate cart optional roll false distance:{distance}";
            LogDiagnostic(
                "CART_ROLL_SKIP",
                $"my:{myTile} station:{stationTile} distance:{distance} " +
                $"retryIn:{nextCartUseTime - Time.time:F2}s");
            return false;
        }

        bool continuingCartUse = cartUseCommit;
        cartUseCommit = true;
        if (!continuingCartUse)
            cartUseCommitStartedTime = Time.time;
        cartUseBlockedUntilTime = -10f;
        decision = new BattleModeComAbilityDecision
        {
            Action = BattleModeComActionType.Reposition,
            Weight = settings.combatPlantWeight + 650,
            TargetTile = stationTile,
            HasTarget = true,
            FirstMove = firstMove,
            Reason = "stage 9 minecart use cart",
            InputDescription = FirstMoveDescription(firstMove)
        };
        lastDecisionTrace =
            $"candidate cart selected station:{stationTile} " +
            $"distance:{distance} arrival:{arrivalSeconds:F2}s route:{route}";
        LogDiagnostic(
            myTile == stationTile
                ? "CART_WAIT_ENTRY"
                : continuingCartUse
                    ? "CART_CONTINUE"
                    : "CART_SELECT",
            $"my:{myTile} station:{stationTile} distance:{distance} " +
            $"move:{FirstMoveDescription(firstMove)} " +
            $"arrival:{arrivalSeconds:F2}s",
            force: !continuingCartUse || myTile == stationTile);
        return true;
    }

    public bool CanTraverseCommittedRailTile(
        Vector2Int currentTile,
        Vector2Int nextTile,
        float arrivalSeconds,
        out string trace)
    {
        trace = "no minecart rail commitment";
        if (!punishCommit ||
            !IsAvailable ||
            minecartController.SuddenDeathStarted ||
            !minecartController.RideActive ||
            minecartController.CurrentRider == movement ||
            nextTile != minecartController.ExitTile)
        {
            return false;
        }

        float exitSeconds =
            minecartController.EstimateSecondsUntilExit();
        Vector2Int cartTile =
            minecartController.GetCurrentCartTile();
        bool activeBombThreat =
            HasActiveBombThreat(nextTile);
        bool safeWindow =
            cartTile != nextTile &&
            !activeBombThreat &&
            exitSeconds >
                Mathf.Max(0f, arrivalSeconds) +
                PunishMinimumExitLeadSeconds;
        trace =
            $"commit:{punishCommit} current:{currentTile} next:{nextTile} " +
            $"cart:{cartTile} arrival:{arrivalSeconds:F2}s " +
            $"exitIn:{FormatSeconds(exitSeconds)} " +
            $"bombThreat:{activeBombThreat} safeWindow:{safeWindow}";
        return safeWindow;
    }

    public bool IsPunishExitTile(Vector2Int tile)
        => minecartController != null &&
           tile == minecartController.ExitTile;

    public void LogCommittedRailEntry(
        Vector2Int currentTile,
        Vector2Int nextTile,
        string trace)
    {
        LogDiagnostic(
            "PUNISH_RAIL_ENTRY",
            $"from:{currentTile} to:{nextTile} {trace}");
    }

    public void LogCommittedRailBlocked(
        Vector2Int currentTile,
        Vector2Int nextTile,
        string trace)
    {
        LogDiagnostic(
            "PUNISH_RAIL_BLOCKED",
            $"from:{currentTile} to:{nextTile} {trace}");
    }

    private bool HasActiveBombThreat(Vector2Int tile)
    {
        foreach (Bomb bomb in Bomb.ActiveBombs)
        {
            if (bomb == null ||
                bomb.HasExploded ||
                bomb.IsBeingHeldByPowerGlove)
            {
                continue;
            }

            if (comController.DoesBombBlastReachTile(bomb, tile))
                return true;
        }

        return false;
    }

    private void LogDiagnostic(
        string key,
        string message,
        bool force = false)
    {
        if (!EnableMinecartDiagnostics)
            return;

        int id = identity != null ? identity.playerId : 0;
        if (DiagnosticPlayerIdFilter != 0 &&
            id != DiagnosticPlayerIdFilter)
        {
            return;
        }

        if (!force &&
            key == lastDiagnosticKey &&
            Time.time - lastDiagnosticTime <
            DiagnosticThrottleSeconds)
        {
            return;
        }

        lastDiagnosticKey = key;
        lastDiagnosticTime = Time.time;
        Vector2Int tile = movement != null
            ? WorldToTile(movement.transform.position)
            : Vector2Int.zero;
        Debug.LogWarning(
            $"[BattleCOMStage9Minecart][P{id}] " +
            $"frame:{Time.frameCount} t:{Time.time:F2} " +
            $"tile:{tile} key:{key} {message}",
            this);
    }

    private Vector2Int WorldToTile(Vector3 world)
    {
        float size = movement != null
            ? Mathf.Max(0.01f, movement.tileSize)
            : 1f;
        return new Vector2Int(
            Mathf.RoundToInt(world.x / size),
            Mathf.RoundToInt(world.y / size));
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

    private static string FormatSeconds(float seconds)
    {
        return float.IsInfinity(seconds)
            ? "none"
            : $"{Mathf.Max(0f, seconds):F2}s";
    }
}

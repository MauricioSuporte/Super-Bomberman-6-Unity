using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerIdentity))]
[RequireComponent(typeof(MovementController))]
[RequireComponent(typeof(BombController))]
[RequireComponent(typeof(BattleModeComController))]
public sealed class BattleModeComStage7PortalEscapeAbility :
    MonoBehaviour,
    IBattleModeComStageAbility,
    IBattleModeComDangerProvider,
    IBattleModeComPlannedBombDangerProvider
{
    public static readonly bool EnablePortalDiagnostics = false;
    public const int DiagnosticPlayerIdFilter = 6;

    private const float OptionalPortalInitialDelayMinSeconds = 1.5f;
    private const float OptionalPortalInitialDelayMaxSeconds = 3f;
    private const float OptionalPortalRetryMinSeconds = 2f;
    private const float OptionalPortalRetryMaxSeconds = 3.5f;
    private const float OptionalPortalUseCooldownMinSeconds = 5f;
    private const float OptionalPortalUseCooldownMaxSeconds = 7f;
    private const float DiagnosticThrottleSeconds = 0.5f;

    private readonly List<Vector2Int> portalCells = new();
    private readonly List<Vector2Int> predictedBlastTiles = new();

    private PlayerIdentity identity;
    private MovementController movement;
    private BattleModeComController comController;
    private BombController bombController;
    private BattleMode7PortalController portalController;
    private Vector2Int committedPortalTile;
    private Vector2Int committedDestinationTile;
    private bool hasCommittedPortal;
    private bool committedForEmergency;
    private float nextOptionalPortalTime = -10f;
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
                   comController != null &&
                   portalController != null &&
                   portalController.isActiveAndEnabled;
        }
    }

    public string DiagnosticName => "Stage7PortalEscape";
    public string LastDecisionTrace => lastDecisionTrace;

    private void Awake()
    {
        CacheReferences();
        ScheduleInitialOptionalPortal();
    }

    private void OnEnable() => CacheReferences();

    private void ScheduleInitialOptionalPortal()
    {
        if (nextOptionalPortalTime > -9f)
            return;

        nextOptionalPortalTime =
            Time.time +
            Random.Range(
                OptionalPortalInitialDelayMinSeconds,
                OptionalPortalInitialDelayMaxSeconds);
    }

    private void CacheReferences()
    {
        if (identity == null)
            TryGetComponent(out identity);

        if (movement == null)
            TryGetComponent(out movement);

        if (comController == null)
            TryGetComponent(out comController);

        if (bombController == null)
            TryGetComponent(out bombController);

        if (portalController == null)
        {
            portalController =
                FindAnyObjectByType<BattleMode7PortalController>();
        }

        if (portalController != null && portalCells.Count == 0)
            portalController.CopyPortalCells(portalCells);
    }

    public bool TryBuildEmergencyDecision(
        BattleModeComDifficultySettings settings,
        BattleModeComController controller,
        Vector2Int myTile,
        float currentDangerSeconds,
        out BattleModeComAbilityDecision decision)
    {
        decision = default;
        hasCommittedPortal = false;
        committedForEmergency = false;

        if (!IsAvailable ||
            controller == null ||
            float.IsInfinity(currentDangerSeconds))
        {
            lastDecisionTrace = "emergency no active danger";
            return false;
        }

        if (controller.TryFindAbilityEscape(
                settings,
                myTile,
                out _,
                out _,
                out string normalRoute))
        {
            lastDecisionTrace =
                $"emergency normal route available:{normalRoute}";
            return false;
        }

        bool found = false;
        Vector2 bestFirstMove = Vector2.zero;
        Vector2Int bestPortalTile = myTile;
        Vector2Int bestDestinationTile = myTile;
        int bestDistance = int.MaxValue;
        float bestArrivalSeconds = float.PositiveInfinity;
        string bestRoute = "none";

        for (int i = 0; i < portalCells.Count; i++)
        {
            Vector2Int portalTile = portalCells[i];
            if (!portalController.TryGetClockwiseDestination(
                    portalTile,
                    out Vector2Int destinationTile))
            {
                continue;
            }

            if (!controller.TryFindAbilityPortalEscapeRoute(
                    settings,
                    myTile,
                    portalTile,
                    destinationTile,
                    portalController.TeleportDurationSeconds,
                    out Vector2 firstMove,
                    out int distance,
                    out float arrivalSeconds,
                    out string route))
            {
                continue;
            }

            if (found &&
                (distance > bestDistance ||
                 (distance == bestDistance &&
                  arrivalSeconds >= bestArrivalSeconds)))
            {
                continue;
            }

            found = true;
            bestFirstMove = firstMove;
            bestPortalTile = portalTile;
            bestDestinationTile = destinationTile;
            bestDistance = distance;
            bestArrivalSeconds = arrivalSeconds;
            bestRoute = route;
        }

        if (!found)
        {
            lastDecisionTrace =
                $"emergency danger:{currentDangerSeconds:F2}s " +
                "no safe portal route";
            return false;
        }

        hasCommittedPortal = true;
        committedForEmergency = true;
        committedPortalTile = bestPortalTile;
        committedDestinationTile = bestDestinationTile;
        float exitSeconds =
            bestArrivalSeconds +
            portalController.TeleportDurationSeconds;
        lastDecisionTrace =
            $"emergency portal:{bestPortalTile} " +
            $"destination:{bestDestinationTile} " +
            $"arrival:{bestArrivalSeconds:F2}s " +
            $"exit:{exitSeconds:F2}s route:{bestRoute}";

        decision = new BattleModeComAbilityDecision
        {
            Action = BattleModeComActionType.Reposition,
            Weight = settings.patrolWeight + 560,
            TargetTile = bestPortalTile,
            HasTarget = true,
            FirstMove = bestFirstMove,
            Reason =
                $"stage 7 portal escape to {bestDestinationTile}",
            InputDescription = FirstMoveDescription(bestFirstMove)
        };
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
            hasCommittedPortal = false;
            committedForEmergency = false;
            lastDecisionTrace = "candidate unavailable";
            return false;
        }

        if (hasCommittedPortal && committedForEmergency)
        {
            lastDecisionTrace =
                $"candidate emergency commitment portal:{committedPortalTile}";
            return false;
        }

        if (hasCommittedPortal &&
            myTile == committedDestinationTile)
        {
            LogDiagnostic(
                "ARRIVED",
                $"destination:{committedDestinationTile}",
                force: true);
            hasCommittedPortal = false;
            committedForEmergency = false;
            nextOptionalPortalTime =
                Time.time +
                Random.Range(
                    OptionalPortalUseCooldownMinSeconds,
                    OptionalPortalUseCooldownMaxSeconds);
        }

        if (hasCommittedPortal)
        {
            if (TryBuildPortalDecision(
                    settings,
                    controller,
                    myTile,
                    committedPortalTile,
                    committedDestinationTile,
                    settings.patrolWeight + 190,
                    "continue",
                    out decision))
            {
                LogDiagnostic(
                    "CONTINUE",
                    $"my:{myTile} portal:{committedPortalTile} " +
                    $"destination:{committedDestinationTile} " +
                    $"move:{FirstMoveDescription(decision.FirstMove)}");
                return true;
            }

            LogDiagnostic(
                "CANCEL",
                $"my:{myTile} portal:{committedPortalTile} " +
                $"destination:{committedDestinationTile} " +
                $"trace:{lastDecisionTrace}",
                force: true);
            hasCommittedPortal = false;
            committedForEmergency = false;
            nextOptionalPortalTime =
                Time.time +
                Random.Range(
                    OptionalPortalRetryMinSeconds,
                    OptionalPortalRetryMaxSeconds);
        }

        if (Time.time < nextOptionalPortalTime)
        {
            lastDecisionTrace =
                $"candidate optional cooldown:" +
                $"{nextOptionalPortalTime - Time.time:F2}s";
            return false;
        }

        bool found = false;
        BattleModeComAbilityDecision bestDecision = default;
        Vector2Int bestPortal = myTile;
        Vector2Int bestDestination = myTile;
        int bestDistance = int.MaxValue;

        for (int i = 0; i < portalCells.Count; i++)
        {
            Vector2Int portalTile = portalCells[i];
            if (!portalController.TryGetClockwiseDestination(
                    portalTile,
                    out Vector2Int destinationTile) ||
                !TryBuildPortalDecision(
                    settings,
                    controller,
                    myTile,
                    portalTile,
                    destinationTile,
                    settings.patrolWeight + 55,
                    "optional",
                    out BattleModeComAbilityDecision candidate,
                    out int distance))
            {
                continue;
            }

            if (found && distance >= bestDistance)
                continue;

            found = true;
            bestDecision = candidate;
            bestPortal = portalTile;
            bestDestination = destinationTile;
            bestDistance = distance;
        }

        if (!found)
        {
            nextOptionalPortalTime =
                Time.time +
                Random.Range(
                    OptionalPortalRetryMinSeconds,
                    OptionalPortalRetryMaxSeconds);
            lastDecisionTrace =
                "candidate optional no safe portal";
            LogDiagnostic(
                "NO_SAFE_PORTAL",
                $"my:{myTile} retryIn:" +
                $"{nextOptionalPortalTime - Time.time:F2}s " +
                $"trace:{lastDecisionTrace}");
            return false;
        }

        hasCommittedPortal = true;
        committedForEmergency = false;
        committedPortalTile = bestPortal;
        committedDestinationTile = bestDestination;
        nextOptionalPortalTime =
            Time.time +
            Random.Range(
                OptionalPortalUseCooldownMinSeconds,
                OptionalPortalUseCooldownMaxSeconds);
        decision = bestDecision;
        lastDecisionTrace =
            $"candidate optional selected portal:{bestPortal} " +
            $"destination:{bestDestination} distance:{bestDistance}";
        LogDiagnostic(
            "SELECT",
            $"my:{myTile} portal:{bestPortal} " +
            $"destination:{bestDestination} distance:{bestDistance} " +
            $"move:{FirstMoveDescription(decision.FirstMove)} " +
            $"weight:{decision.Weight}",
            force: true);
        return true;
    }

    private bool TryBuildPortalDecision(
        BattleModeComDifficultySettings settings,
        BattleModeComController controller,
        Vector2Int myTile,
        Vector2Int portalTile,
        Vector2Int destinationTile,
        int weight,
        string purpose,
        out BattleModeComAbilityDecision decision)
    {
        return TryBuildPortalDecision(
            settings,
            controller,
            myTile,
            portalTile,
            destinationTile,
            weight,
            purpose,
            out decision,
            out _);
    }

    private bool TryBuildPortalDecision(
        BattleModeComDifficultySettings settings,
        BattleModeComController controller,
        Vector2Int myTile,
        Vector2Int portalTile,
        Vector2Int destinationTile,
        int weight,
        string purpose,
        out BattleModeComAbilityDecision decision,
        out int distance)
    {
        decision = default;
        distance = int.MaxValue;
        if (!controller.TryFindAbilityPortalEscapeRoute(
                settings,
                myTile,
                portalTile,
                destinationTile,
                portalController.TeleportDurationSeconds,
                out Vector2 firstMove,
                out distance,
                out float arrivalSeconds,
                out string route))
        {
            lastDecisionTrace =
                $"candidate {purpose} rejected portal:{portalTile} " +
                $"destination:{destinationTile} route:{route}";
            return false;
        }

        if (firstMove == Vector2.zero &&
            myTile == portalTile &&
            !portalController.IsMovementAtPortal(
                movement,
                portalTile) &&
            portalController.TryGetPortalWorldCenter(
                portalTile,
                out Vector2 portalCenter))
        {
            Vector2 delta =
                portalCenter -
                (Vector2)movement.transform.position;
            firstMove = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y)
                ? new Vector2(Mathf.Sign(delta.x), 0f)
                : new Vector2(0f, Mathf.Sign(delta.y));
            route +=
                $" centerPortal delta:{delta} " +
                $"move:{FirstMoveDescription(firstMove)}";
            LogDiagnostic(
                "CENTER_ENTRY",
                $"my:{myTile} portal:{portalTile} " +
                $"delta:{delta} " +
                $"move:{FirstMoveDescription(firstMove)}");
        }

        float exitSeconds =
            arrivalSeconds +
            portalController.TeleportDurationSeconds;
        decision = new BattleModeComAbilityDecision
        {
            Action = BattleModeComActionType.Reposition,
            Weight = Mathf.Max(1, weight),
            TargetTile = portalTile,
            HasTarget = true,
            FirstMove = firstMove,
            Reason =
                $"stage 7 portal {purpose} to {destinationTile}",
            InputDescription = FirstMoveDescription(firstMove)
        };
        lastDecisionTrace =
            $"candidate {purpose} portal:{portalTile} " +
            $"destination:{destinationTile} arrival:{arrivalSeconds:F2}s " +
            $"exit:{exitSeconds:F2}s route:{route}";
        return true;
    }

    public bool IsPortalTile(Vector2Int tile)
    {
        CacheReferences();
        return portalController != null &&
               portalController.IsPortalCell(tile);
    }

    public bool IsCommittedPortalTarget(Vector2Int tile)
        => hasCommittedPortal &&
           tile == committedPortalTile;

    public bool TryGetDangerSeconds(
        Vector2Int tile,
        out float dangerSeconds)
    {
        dangerSeconds = float.PositiveInfinity;
        if (!IsAvailable)
            return false;

        bool found = false;
        foreach (Bomb bomb in Bomb.ActiveBombs)
        {
            if (bomb == null || bomb.HasExploded || bomb.IsBeingHeldByPowerGlove)
                continue;

            Vector2Int sourceTile = WorldToTile(bomb.GetLogicalPosition());
            if (!portalController.TryGetBombPortalTrajectory(
                    sourceTile,
                    out _,
                    out Vector2Int launchDirection,
                    out Vector2Int landingTile))
            {
                continue;
            }

            for (int step = 0; step <= 3; step++)
            {
                Vector2Int travelTile = landingTile - launchDirection * step;
                if (tile != travelTile)
                    continue;

                dangerSeconds = Mathf.Min(
                    dangerSeconds,
                    portalController.TeleportDurationSeconds +
                    (3 - step) * 0.08f);
                found = true;
            }

            predictedBlastTiles.Clear();
            comController.AppendAbilityBlastTiles(
                landingTile,
                GetBombRadius(bomb),
                predictedBlastTiles);
            if (predictedBlastTiles.Contains(tile))
            {
                float fuse = bomb.IsControlBomb ? 0.65f : bomb.RemainingFuseSeconds;
                dangerSeconds = Mathf.Min(dangerSeconds, fuse + 0.25f);
                found = true;
            }
        }

        return found;
    }

    public bool TryAppendPlannedBombDangerTiles(
        Vector2Int plantTile,
        List<Vector2Int> plannedDangerTiles)
    {
        if (plannedDangerTiles == null ||
            !IsAvailable ||
            !portalController.TryGetBombPortalTrajectory(
                plantTile,
                out _,
                out Vector2Int launchDirection,
                out Vector2Int landingTile))
        {
            return false;
        }

        for (int step = 0; step <= 3; step++)
            AddUnique(plannedDangerTiles, landingTile - launchDirection * step);

        predictedBlastTiles.Clear();
        comController.AppendAbilityBlastTiles(
            landingTile,
            bombController.GetPlannedExplosionRadius(),
            predictedBlastTiles);
        for (int i = 0; i < predictedBlastTiles.Count; i++)
            AddUnique(plannedDangerTiles, predictedBlastTiles[i]);

        return true;
    }

    private int GetBombRadius(Bomb bomb)
    {
        if (bomb != null && bomb.Owner != null)
            return Mathf.Max(1, bomb.Owner.GetPredictedBlastRadius(bomb));

        return bombController != null
            ? Mathf.Max(1, bombController.GetPredictedBlastRadius(bomb))
            : 2;
    }

    private static void AddUnique(List<Vector2Int> tiles, Vector2Int tile)
    {
        if (!tiles.Contains(tile))
            tiles.Add(tile);
    }

    public bool IsPortalExitSafe(
        Vector2Int portalTile,
        float portalArrivalSeconds,
        BattleModeComDifficultySettings settings,
        out string trace)
    {
        trace = "portal unavailable";
        if (!IsAvailable ||
            !portalController.TryGetClockwiseDestination(
                portalTile,
                out Vector2Int destinationTile))
        {
            return false;
        }

        float exitSeconds =
            Mathf.Max(0f, portalArrivalSeconds) +
            portalController.TeleportDurationSeconds;
        float destinationDanger =
            comController.GetAbilityDangerSeconds(destinationTile);
        bool safe = !comController.IsAbilityTileDangerousAt(
            destinationTile,
            exitSeconds,
            settings) &&
            (!hasCommittedPortal ||
             destinationTile == committedDestinationTile);
        trace =
            $"portal:{portalTile} destination:{destinationTile} " +
            $"exitIn:{exitSeconds:F2}s " +
            $"danger:{FormatDanger(destinationDanger)} " +
            $"committedDestinationMatch:" +
            $"{(!hasCommittedPortal || destinationTile == committedDestinationTile)}";
        return safe;
    }

    public void LogPortalEntryBlocked(
        Vector2Int portalTile,
        string trace)
    {
        lastDecisionTrace =
            $"portal entry blocked:{portalTile} {trace}";
        LogDiagnostic(
            "ENTRY_BLOCKED",
            $"portal:{portalTile} {trace}",
            force: true);
    }

    public void LogTeleportStarted(
        Vector2Int portalTile,
        Vector2Int destinationTile)
    {
        LogDiagnostic(
            "TELEPORT_START",
            $"portal:{portalTile} destination:{destinationTile} " +
            $"committed:{hasCommittedPortal} " +
            $"expectedPortal:{committedPortalTile} " +
            $"expectedDestination:{committedDestinationTile}",
            force: true);
    }

    public void LogTeleportCompleted(
        Vector2Int portalTile,
        Vector2Int destinationTile)
    {
        LogDiagnostic(
            "TELEPORT_END",
            $"portal:{portalTile} destination:{destinationTile} " +
            $"committed:{hasCommittedPortal}",
            force: true);
    }

    private void LogDiagnostic(
        string key,
        string message,
        bool force = false)
    {
        if (!EnablePortalDiagnostics)
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
            $"[BattleCOMStage7Portal][P{id}] " +
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

    private static string FormatDanger(float seconds)
    {
        if (float.IsInfinity(seconds))
            return "safe";

        if (seconds <= 0f)
            return "now";

        return $"{seconds:F2}s";
    }
}

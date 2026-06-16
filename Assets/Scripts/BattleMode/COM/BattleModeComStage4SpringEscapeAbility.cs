using System.Collections.Generic;
using System.Text;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerIdentity))]
[RequireComponent(typeof(MovementController))]
[RequireComponent(typeof(BattleModeComController))]
public sealed class BattleModeComStage4SpringEscapeAbility :
    MonoBehaviour,
    IBattleModeComStageAbility
{
    public static readonly bool EnableSpringDiagnostics = false;
    public const int DiagnosticPlayerIdFilter = 0;

    private readonly List<Vector2> springWorldPositions = new();
    private readonly List<Vector2> landingWorldPositions = new();
    private readonly Dictionary<string, float> diagnosticTimes = new();
    private readonly StringBuilder diagnosticBuilder = new(512);

    private PlayerIdentity identity;
    private MovementController movement;
    private BattleModeComController comController;
    private BattleMode4SpringTileController springController;
    private float tileSize = 1f;
    private Vector2Int committedSpringTile;
    private bool hasCommittedSpring;
    private float committedArrivalSeconds;
    private float committedLandingSeconds;
    private float committedDecisionTime = -10f;
    private bool lastLaunchWasCommitted;
    private Vector2Int lastLaunchSelectedLandingTile;
    private float lastLaunchTime = -10f;
    private float lastLaunchDecisionLandingSeconds = -1f;
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
                   springController != null &&
                   springController.isActiveAndEnabled;
        }
    }

    public string DiagnosticName => "Stage4SpringEscape";
    public string LastDecisionTrace => lastDecisionTrace;

    private void Awake() => CacheReferences();
    private void OnEnable() => CacheReferences();

    private void CacheReferences()
    {
        if (identity == null)
            TryGetComponent(out identity);

        if (movement == null)
            TryGetComponent(out movement);

        if (comController == null)
            TryGetComponent(out comController);

        if (springController == null)
            springController =
                FindAnyObjectByType<BattleMode4SpringTileController>();

        if (movement != null)
            tileSize = Mathf.Max(0.01f, movement.tileSize);
    }

    public bool TryBuildEmergencyDecision(
        BattleModeComDifficultySettings settings,
        BattleModeComController controller,
        Vector2Int myTile,
        float currentDangerSeconds,
        out BattleModeComAbilityDecision decision)
    {
        decision = default;
        hasCommittedSpring = false;

        if (!IsAvailable || controller == null || float.IsInfinity(currentDangerSeconds))
        {
            lastDecisionTrace = "emergency no active bomb danger";
            return false;
        }

        if (controller.TryFindAbilityEscape(
                settings,
                myTile,
                out _,
                out _,
                out string normalRoute))
        {
            lastDecisionTrace = $"emergency normal route available:{normalRoute}";
            return false;
        }

        springController.CopySpringWorldPositions(springWorldPositions);

        bool found = false;
        Vector2 bestFirstMove = Vector2.zero;
        Vector2Int bestSpringTile = default;
        int bestDistance = int.MaxValue;
        float bestArrivalSeconds = float.PositiveInfinity;
        int bestLandingCount = 0;
        string bestRoute = "none";

        for (int i = 0; i < springWorldPositions.Count; i++)
        {
            Vector2 springWorld = springWorldPositions[i];
            Vector2Int springTile = WorldToTile(springWorld);
            if (!controller.TryFindAbilitySpringEscapeRoute(
                    settings,
                    myTile,
                    springTile,
                    out Vector2 firstMove,
                    out int distance,
                    out float arrivalSeconds,
                    out string route))
            {
                LogDiagnostic(
                    $"REJECT_ROUTE spring:{springTile} from:{myTile} " +
                    $"danger:{FormatDanger(currentDangerSeconds)}",
                    throttle: true);
                continue;
            }

            if (!HasAnySafeLanding(
                    controller,
                    springWorld,
                    arrivalSeconds,
                    out int landingCount,
                    out string landingTrace))
            {
                LogDiagnostic(
                    $"REJECT_LANDINGS spring:{springTile} from:{myTile} " +
                    $"danger:{FormatDanger(currentDangerSeconds)} arrival:{arrivalSeconds:F2}s " +
                    $"jump:{springController.JumpDurationSeconds:F2}s {landingTrace}",
                    throttle: true);
                continue;
            }

            LogDiagnostic(
                $"ACCEPT_CANDIDATE spring:{springTile} from:{myTile} " +
                $"danger:{FormatDanger(currentDangerSeconds)} arrival:{arrivalSeconds:F2}s " +
                $"jump:{springController.JumpDurationSeconds:F2}s route:{route} {landingTrace}",
                throttle: true);

            if (found &&
                (distance > bestDistance ||
                 (distance == bestDistance && arrivalSeconds >= bestArrivalSeconds)))
            {
                continue;
            }

            found = true;
            bestFirstMove = firstMove;
            bestSpringTile = springTile;
            bestDistance = distance;
            bestArrivalSeconds = arrivalSeconds;
            bestLandingCount = landingCount;
            bestRoute = route;
        }

        if (!found)
        {
            lastDecisionTrace =
                $"emergency danger:{currentDangerSeconds:F2} no safe spring route";
            return false;
        }

        hasCommittedSpring = true;
        committedSpringTile = bestSpringTile;
        committedArrivalSeconds = bestArrivalSeconds;
        committedLandingSeconds =
            bestArrivalSeconds + springController.JumpDurationSeconds;
        committedDecisionTime = Time.time;
        lastDecisionTrace =
            $"emergency danger:{currentDangerSeconds:F2} spring:{bestSpringTile} " +
            $"arrival:{bestArrivalSeconds:F2}s landings:{bestLandingCount} route:{bestRoute}";
        LogDiagnostic(
            $"SELECT spring:{bestSpringTile} from:{myTile} " +
            $"danger:{FormatDanger(currentDangerSeconds)} arrival:{bestArrivalSeconds:F2}s " +
            $"landingEta:{committedLandingSeconds:F2}s landings:{bestLandingCount} " +
            $"move:{FirstMoveDescription(bestFirstMove)} route:{bestRoute}",
            throttle: true);

        decision = new BattleModeComAbilityDecision
        {
            Action = BattleModeComActionType.Reposition,
            Weight = settings.patrolWeight + 550,
            TargetTile = bestSpringTile,
            HasTarget = true,
            FirstMove = bestFirstMove,
            Reason = "escape stage 4 spring jump",
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
        lastDecisionTrace = "candidate spring reserved for emergency escape";
        return false;
    }

    public bool IsCommittedSpringTarget(Vector2Int tile)
        => hasCommittedSpring &&
           tile == committedSpringTile &&
           springController != null &&
           springController.IsSpringWorldPosition(TileToWorld(tile));

    public bool IsSpringTile(Vector2Int tile)
    {
        CacheReferences();
        return springController != null &&
               springController.IsSpringWorldPosition(TileToWorld(tile));
    }

    public bool HasSafeImmediateLanding(Vector2Int springTile, out string trace)
    {
        trace = "spring unavailable";
        if (!IsAvailable ||
            !springController.CopyLandingWorldPositions(
                TileToWorld(springTile),
                landingWorldPositions))
        {
            return false;
        }

        int safeCount = 0;
        diagnosticBuilder.Clear();
        diagnosticBuilder.Append("policy:no-known-danger landings:[");

        for (int i = 0; i < landingWorldPositions.Count; i++)
        {
            Vector2 landingWorld = landingWorldPositions[i];
            Vector2Int landingTile = WorldToTile(landingWorld);
            float danger = comController.GetAbilityDangerSeconds(landingTile);
            bool safe = IsImmediateLandingSafe(landingWorld);
            if (safe)
                safeCount++;

            if (i > 0)
                diagnosticBuilder.Append(',');

            diagnosticBuilder.Append(landingTile);
            diagnosticBuilder.Append('=');
            diagnosticBuilder.Append(FormatDanger(danger));
            diagnosticBuilder.Append(safe ? ":safe" : ":unsafe");
        }

        diagnosticBuilder.Append("] safeCount:");
        diagnosticBuilder.Append(safeCount);
        trace = diagnosticBuilder.ToString();
        return safeCount > 0;
    }

    public bool IsImmediateLandingSafe(Vector2 landingWorld)
    {
        CacheReferences();
        if (comController == null || springController == null)
            return false;

        float danger =
            comController.GetAbilityDangerSeconds(WorldToTile(landingWorld));
        return float.IsInfinity(danger);
    }

    public void LogSpringEntryBlocked(Vector2Int springTile, string trace)
    {
        LogDiagnostic(
            $"BLOCK_ENTRY spring:{springTile} {trace}",
            throttle: true);
    }

    public void LogSpringLaunchBlocked(Vector2 springWorld)
    {
        Vector2Int springTile = WorldToTile(springWorld);
        HasSafeImmediateLanding(springTile, out string trace);
        LogDiagnostic(
            $"BLOCK_LAUNCH spring:{springTile} {trace}",
            throttle: true);
    }

    public void LogSpringLaunch(Vector2 springWorld, Vector2 selectedLandingWorld)
    {
        if (!ShouldLog())
            return;

        Vector2Int springTile = WorldToTile(springWorld);
        Vector2Int selectedLandingTile = WorldToTile(selectedLandingWorld);
        float springDanger = comController != null
            ? comController.GetAbilityDangerSeconds(springTile)
            : float.PositiveInfinity;
        float selectedDanger = comController != null
            ? comController.GetAbilityDangerSeconds(selectedLandingTile)
            : float.PositiveInfinity;
        bool matchesCommitment =
            hasCommittedSpring && committedSpringTile == springTile;
        float actualLandingSeconds = springController != null
            ? springController.JumpDurationSeconds
            : 0f;
        lastLaunchWasCommitted = matchesCommitment;
        lastLaunchSelectedLandingTile = selectedLandingTile;
        lastLaunchTime = Time.time;
        lastLaunchDecisionLandingSeconds =
            matchesCommitment ? committedLandingSeconds : -1f;

        string candidates = BuildLandingDiagnostic(
            springWorld,
            actualLandingSeconds,
            selectedLandingTile);

        LogDiagnostic(
            $"LAUNCH spring:{springTile} selectedLanding:{selectedLandingTile} " +
            $"springDangerNow:{FormatDanger(springDanger)} " +
            $"selectedDangerNow:{FormatDanger(selectedDanger)} " +
            $"actualLandingIn:{actualLandingSeconds:F2}s " +
            $"committed:{matchesCommitment} " +
            $"planAge:{(matchesCommitment ? Time.time - committedDecisionTime : -1f):F2}s " +
            $"decisionArrivalEstimate:{(matchesCommitment ? committedArrivalSeconds : -1f):F2}s " +
            $"decisionLandingEstimate:{(matchesCommitment ? committedLandingSeconds : -1f):F2}s " +
            $"{candidates}",
            force: true);
    }

    public void LogSpringLanding(Vector2 springWorld, Vector2 landingWorld)
    {
        if (!ShouldLog())
            return;

        Vector2Int springTile = WorldToTile(springWorld);
        Vector2Int landingTile = WorldToTile(landingWorld);
        float dangerNow = comController != null
            ? comController.GetAbilityDangerSeconds(landingTile)
            : float.PositiveInfinity;

        LogDiagnostic(
            $"LAND spring:{springTile} landing:{landingTile} " +
            $"dangerNow:{FormatDanger(dangerNow)} " +
            $"threatPresent:{!float.IsInfinity(dangerNow)} " +
            $"flightTime:{Time.time - lastLaunchTime:F2}s " +
            $"selectedMatch:{landingTile == lastLaunchSelectedLandingTile} " +
            $"committed:{lastLaunchWasCommitted} " +
            $"decisionLandingEstimate:{lastLaunchDecisionLandingSeconds:F2}s",
            force: true);

        hasCommittedSpring = false;
    }

    private bool HasAnySafeLanding(
        BattleModeComController controller,
        Vector2 springWorld,
        float springArrivalSeconds,
        out int landingCount,
        out string landingTrace)
    {
        landingCount = 0;
        landingTrace = "landings:none";
        if (!springController.CopyLandingWorldPositions(
                springWorld,
                landingWorldPositions))
        {
            return false;
        }

        float landingSeconds =
            springArrivalSeconds + springController.JumpDurationSeconds;
        int safeCount = 0;
        diagnosticBuilder.Clear();
        diagnosticBuilder.Append("landingEta:");
        diagnosticBuilder.Append(landingSeconds.ToString("F2"));
        diagnosticBuilder.Append(" landings:[");

        for (int i = 0; i < landingWorldPositions.Count; i++)
        {
            Vector2Int landingTile = WorldToTile(landingWorldPositions[i]);
            float dangerSeconds = controller.GetAbilityDangerSeconds(landingTile);
            bool safe = float.IsInfinity(dangerSeconds);

            if (i > 0)
                diagnosticBuilder.Append(',');

            diagnosticBuilder.Append(landingTile);
            diagnosticBuilder.Append('=');
            diagnosticBuilder.Append(FormatDanger(dangerSeconds));
            diagnosticBuilder.Append(safe ? ":safe" : ":unsafe");

            if (safe)
                safeCount++;
        }

        diagnosticBuilder.Append("] safeCount:");
        diagnosticBuilder.Append(safeCount);
        landingCount = safeCount;
        landingTrace = diagnosticBuilder.ToString();
        return safeCount > 0;
    }

    private string BuildLandingDiagnostic(
        Vector2 springWorld,
        float landingSeconds,
        Vector2Int selectedLandingTile)
    {
        if (springController == null ||
            !springController.CopyLandingWorldPositions(
                springWorld,
                landingWorldPositions))
        {
            return "launchLandings:none";
        }

        diagnosticBuilder.Clear();
        diagnosticBuilder.Append("launchLandings:[");
        for (int i = 0; i < landingWorldPositions.Count; i++)
        {
            Vector2Int tile = WorldToTile(landingWorldPositions[i]);
            float danger = comController != null
                ? comController.GetAbilityDangerSeconds(tile)
                : float.PositiveInfinity;

            if (i > 0)
                diagnosticBuilder.Append(',');

            if (tile == selectedLandingTile)
                diagnosticBuilder.Append('*');

            diagnosticBuilder.Append(tile);
            diagnosticBuilder.Append('=');
            diagnosticBuilder.Append(FormatDanger(danger));
            diagnosticBuilder.Append(" vsLanding:");
            diagnosticBuilder.Append(landingSeconds.ToString("F2"));
        }

        diagnosticBuilder.Append(']');
        return diagnosticBuilder.ToString();
    }

    private bool ShouldLog()
    {
        if (!EnableSpringDiagnostics)
            return false;

        CacheReferences();
        return identity != null &&
               (DiagnosticPlayerIdFilter == 0 ||
                identity.playerId == DiagnosticPlayerIdFilter);
    }

    private void LogDiagnostic(string message, bool throttle = false, bool force = false)
    {
        if (!ShouldLog())
            return;

        if (throttle && !force)
        {
            int dangerIndex = message.IndexOf(" danger:", System.StringComparison.Ordinal);
            int landingsIndex =
                message.IndexOf(" landings:[", System.StringComparison.Ordinal);
            string key = dangerIndex >= 0
                ? message.Substring(0, dangerIndex)
                : landingsIndex >= 0
                    ? message.Substring(0, landingsIndex)
                    : message;
            if (diagnosticTimes.TryGetValue(key, out float lastTime) &&
                Time.time - lastTime < 1f)
            {
                return;
            }

            diagnosticTimes[key] = Time.time;
        }

        Debug.LogWarning(
            $"[BattleCOMSpring][P{identity.playerId}] frame:{Time.frameCount} " +
            $"t:{Time.time:F2} {message}",
            this);
    }

    private Vector2Int WorldToTile(Vector2 world)
    {
        float size = Mathf.Max(0.01f, tileSize);
        return new Vector2Int(
            Mathf.RoundToInt(world.x / size),
            Mathf.RoundToInt(world.y / size));
    }

    private Vector2 TileToWorld(Vector2Int tile)
        => new(tile.x * tileSize, tile.y * tileSize);

    private static string FirstMoveDescription(Vector2 move)
    {
        if (move.x > 0.5f)
            return "MoveRight";

        if (move.x < -0.5f)
            return "MoveLeft";

        if (move.y > 0.5f)
            return "MoveUp";

        if (move.y < -0.5f)
            return "MoveDown";

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

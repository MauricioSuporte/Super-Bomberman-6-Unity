using UnityEngine;

/// <summary>
/// Uses the Mole mount drill as a preventive Battle Mode escape.
/// The player remains vulnerable and input-locked during phase 1, so the COM
/// only starts drilling when the predicted explosion happens after that phase.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerIdentity))]
[RequireComponent(typeof(MovementController))]
public sealed class BattleModeComMoleDrillEscapeAbility : MonoBehaviour, IBattleModeComAbility
{
    public const int DiagnosticPlayerIdFilter = 0;
    public static readonly bool EnableMoleDrillDiagnostics = false;

    private const float StartupSafetyMarginSeconds = 0.12f;
    private const float SurgicalLogIntervalSeconds = 0.35f;

    private PlayerIdentity identity;
    private MovementController movement;
    private AbilitySystem abilitySystem;
    private MoleMountDrillAbility drillAbility;

    private string lastDecisionTrace = "not evaluated";
    private float lastSurgicalLogTime = -10f;
    private string lastSurgicalLogKey = string.Empty;

    public string DiagnosticName => "MoleDrillEscape";
    public string LastDecisionTrace => lastDecisionTrace;

    public bool IsAvailable
    {
        get
        {
            CacheReferences();
            return identity != null &&
                   movement != null &&
                   !movement.isDead &&
                   abilitySystem != null &&
                   abilitySystem.IsEnabled(MoleMountDrillAbility.AbilityId) &&
                   drillAbility != null &&
                   drillAbility.CanStartDrill;
        }
    }

    private void Awake() => CacheReferences();
    private void OnEnable() => CacheReferences();

    private void CacheReferences()
    {
        if (identity == null)
            TryGetComponent(out identity);

        if (movement == null)
            TryGetComponent(out movement);

        if (abilitySystem == null)
            TryGetComponent(out abilitySystem);

        if (drillAbility == null)
            TryGetComponent(out drillAbility);
    }

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
            lastDecisionTrace = BuildUnavailableTrace();
            return false;
        }

        if (float.IsInfinity(currentDangerSeconds))
        {
            lastDecisionTrace = "emergency no danger";
            return false;
        }

        int safeExits = controller != null
            ? controller.CountSafeEscapeFirstSteps(settings, myTile)
            : 0;
        if (safeExits >= 1)
        {
            lastDecisionTrace = $"emergency walking exits available:{safeExits}";
            LogSurgical(
                "REJECT_SAFE_EXITS",
                $"my:{myTile} danger:{FormatDanger(currentDangerSeconds)} exits:{safeExits}");
            return false;
        }

        float startupSeconds = drillAbility.StartupInvulnerabilityDelaySeconds;
        float requiredSurvivalSeconds =
            startupSeconds +
            StartupSafetyMarginSeconds +
            Mathf.Max(0f, settings.dangerReactionSeconds);

        if (currentDangerSeconds < requiredSurvivalSeconds)
        {
            lastDecisionTrace =
                $"emergency too late danger:{FormatDanger(currentDangerSeconds)} " +
                $"startupRequired:{requiredSurvivalSeconds:F2}";
            LogSurgical(
                "REJECT_TOO_LATE",
                $"my:{myTile} danger:{FormatDanger(currentDangerSeconds)} " +
                $"startup:{startupSeconds:F2} required:{requiredSurvivalSeconds:F2}");
            return false;
        }

        decision = new BattleModeComAbilityDecision
        {
            Action = BattleModeComActionType.Stopped,
            Weight = 380,
            TargetTile = myTile,
            HasTarget = true,
            FirstMove = Vector2.zero,
            Reason = "mole-drill preventive escape",
            InputDescription = "ActionC",
            TapActionC = true,
            UsesEscapeAbilityChance = true
        };

        lastDecisionTrace =
            $"emergency DRILL danger:{FormatDanger(currentDangerSeconds)} " +
            $"startup:{startupSeconds:F2}";
        LogSurgical(
            "DRILL_ESCAPE_READY",
            $"my:{myTile} danger:{FormatDanger(currentDangerSeconds)} " +
            $"startup:{startupSeconds:F2} required:{requiredSurvivalSeconds:F2} " +
            $"exits:{safeExits} chance:{settings.escapeAbilityChance:F2}");
        return true;
    }

    public bool TryBuildCandidateDecision(
        BattleModeComDifficultySettings settings,
        BattleModeComController controller,
        Vector2Int myTile,
        out BattleModeComAbilityDecision decision)
    {
        decision = default;
        lastDecisionTrace = "candidate disabled: drill is escape-only";
        return false;
    }

    private string BuildUnavailableTrace()
    {
        return
            $"emergency unavailable identity:{(identity != null)} movement:{(movement != null)} " +
            $"abilitySystem:{(abilitySystem != null)} " +
            $"enabled:{(abilitySystem != null && abilitySystem.IsEnabled(MoleMountDrillAbility.AbilityId))} " +
            $"drill:{(drillAbility != null)} running:{(drillAbility != null && drillAbility.Running)} " +
            $"canStart:{(drillAbility != null && drillAbility.CanStartDrill)}";
    }

    private static string FormatDanger(float seconds)
    {
        if (float.IsInfinity(seconds))
            return "safe";

        if (seconds <= 0f)
            return "now";

        return $"{seconds:F2}";
    }

    private void LogSurgical(string key, string message, bool force = false)
    {
        if (!EnableMoleDrillDiagnostics)
            return;

        int id = identity != null ? Mathf.Clamp(identity.playerId, 1, 6) : 0;
        if (DiagnosticPlayerIdFilter != 0 && id != DiagnosticPlayerIdFilter)
            return;

        if (!force &&
            key == lastSurgicalLogKey &&
            Time.time - lastSurgicalLogTime < SurgicalLogIntervalSeconds)
            return;

        lastSurgicalLogKey = key;
        lastSurgicalLogTime = Time.time;
        Debug.LogWarning($"[BattleCOM{DiagnosticName}][P{id}] {key} {message}", this);
    }
}

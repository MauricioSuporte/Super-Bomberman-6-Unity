using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerIdentity))]
[RequireComponent(typeof(MovementController))]
public sealed class BattleModeComStage10PowerZoneAggressionAbility :
    MonoBehaviour,
    IBattleModeComStageAbility,
    IBattleModeComKickTrajectoryProvider
{
    public float OwnChainPlanChance => 0.75f;
    public float PowerGloveChanceMultiplier => 2.4f;
    public float PowerGloveCooldownSeconds => 0.8f;
    public int PowerGloveWeightBonus => 90;
    public float PunchChanceMultiplier => 2.4f;
    public float PunchCooldownSeconds => 0.65f;
    public int PunchWeightBonus => 90;

    public float OffensiveKickChanceMultiplier => 2.5f;
    public int OffensiveKickWeightBonus => 90;
    public float OffensiveKickCooldownSeconds => 0.4f;
    public int MaxSequentialKickBombs => 3;
    public float RepeatKickChance => 0.9f;

    private PlayerIdentity identity;
    private MovementController movement;
    private string lastDecisionTrace = "not evaluated";

    public bool IsAvailable
    {
        get
        {
            CacheReferences();
            return identity != null &&
                   movement != null &&
                   !movement.isDead;
        }
    }

    public string DiagnosticName => "Stage10PowerZoneAggression";
    public string LastDecisionTrace => lastDecisionTrace;

    private void Awake() => CacheReferences();
    private void OnEnable() => CacheReferences();

    private void CacheReferences()
    {
        if (identity == null)
            TryGetComponent(out identity);

        if (movement == null)
            TryGetComponent(out movement);
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
        return false;
    }

    public bool TryBuildEmergencyDecision(
        BattleModeComDifficultySettings settings,
        BattleModeComController controller,
        Vector2Int myTile,
        float currentDangerSeconds,
        out BattleModeComAbilityDecision decision)
    {
        decision = default;
        lastDecisionTrace =
            $"emergency passive danger:{FormatDanger(currentDangerSeconds)}";
        return false;
    }

    public bool TryBuildCandidateDecision(
        BattleModeComDifficultySettings settings,
        BattleModeComController controller,
        Vector2Int myTile,
        out BattleModeComAbilityDecision decision)
    {
        decision = default;
        lastDecisionTrace =
            $"candidate passive chainChance:{OwnChainPlanChance:F2} " +
            $"gloveMultiplier:{PowerGloveChanceMultiplier:F2} " +
            $"kickMultiplier:{OffensiveKickChanceMultiplier:F2} " +
            $"punchMultiplier:{PunchChanceMultiplier:F2}";
        return false;
    }

    private static string FormatDanger(float seconds)
    {
        if (float.IsInfinity(seconds))
            return "safe";

        return seconds <= 0f ? "now" : $"{seconds:F2}s";
    }
}

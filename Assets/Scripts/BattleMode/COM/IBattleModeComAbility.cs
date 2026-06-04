public interface IBattleModeComAbility
{
    bool IsAvailable { get; }
    string DiagnosticName { get; }
    string LastDecisionTrace { get; }

    bool TryBuildEmergencyDecision(
        BattleModeComDifficultySettings settings,
        BattleModeComController controller,
        UnityEngine.Vector2Int myTile,
        float currentDangerSeconds,
        out BattleModeComAbilityDecision decision);

    bool TryBuildCandidateDecision(
        BattleModeComDifficultySettings settings,
        BattleModeComController controller,
        UnityEngine.Vector2Int myTile,
        out BattleModeComAbilityDecision decision);
}

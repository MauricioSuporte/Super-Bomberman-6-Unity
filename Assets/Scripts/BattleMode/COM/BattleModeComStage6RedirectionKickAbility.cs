using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerIdentity))]
[RequireComponent(typeof(MovementController))]
public sealed class BattleModeComStage6RedirectionKickAbility :
    MonoBehaviour,
    IBattleModeComStageAbility,
    IBattleModeComKickTrajectoryProvider
{
    private readonly List<BattleMode6RedirectionController.ArrowCell> arrows = new();

    private PlayerIdentity identity;
    private MovementController movement;
    private BattleMode6RedirectionController redirectionController;
    private string lastDecisionTrace = "not evaluated";

    public float OffensiveKickChanceMultiplier => 2.2f;
    public int OffensiveKickWeightBonus => 65;
    public float OffensiveKickCooldownSeconds => 0.45f;
    public int MaxSequentialKickBombs => 3;
    public float RepeatKickChance => 0.9f;

    public bool IsAvailable
    {
        get
        {
            CacheReferences();
            return identity != null &&
                   movement != null &&
                   !movement.isDead &&
                   redirectionController != null &&
                   redirectionController.isActiveAndEnabled;
        }
    }

    public string DiagnosticName => "Stage6RedirectionKick";
    public string LastDecisionTrace => lastDecisionTrace;

    private void Awake() => CacheReferences();
    private void OnEnable() => CacheReferences();

    private void CacheReferences()
    {
        if (identity == null)
            TryGetComponent(out identity);

        if (movement == null)
            TryGetComponent(out movement);

        if (redirectionController == null)
        {
            redirectionController =
                FindAnyObjectByType<BattleMode6RedirectionController>();
        }

        if (redirectionController != null && arrows.Count == 0)
            redirectionController.CopyArrowCells(arrows);
    }

    public bool TryGetRedirectedKickDirection(
        Vector2Int tile,
        Vector2Int incomingDirection,
        out Vector2Int redirectedDirection)
    {
        redirectedDirection = incomingDirection;
        if (!IsAvailable ||
            !redirectionController.TryGetRedirection(
                tile,
                out Vector2Int arrowDirection))
        {
            return false;
        }

        redirectedDirection = arrowDirection;
        return redirectedDirection != incomingDirection;
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
            $"emergency passive arrows:{arrows.Count} danger:{FormatDanger(currentDangerSeconds)}";
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
            $"candidate passive arrows:{arrows.Count} kickChanceMultiplier:{OffensiveKickChanceMultiplier:F2} " +
            $"maxSequence:{MaxSequentialKickBombs}";
        return false;
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

using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerIdentity))]
[RequireComponent(typeof(MovementController))]
public sealed class BattleModeComStage2FallingBombAbility :
    MonoBehaviour,
    IBattleModeComStageAbility,
    IBattleModeComDangerProvider
{
    private PlayerIdentity identity;
    private MovementController movement;
    private BattleMode2FallingBombController fallingBombController;
    private float tileSize = 1f;
    private string lastDecisionTrace = "not evaluated";

    public bool IsAvailable
    {
        get
        {
            CacheReferences();
            return identity != null &&
                   movement != null &&
                   !movement.isDead &&
                   fallingBombController != null &&
                   fallingBombController.isActiveAndEnabled;
        }
    }

    public string DiagnosticName => "Stage2FallingBomb";
    public string LastDecisionTrace => lastDecisionTrace;

    private void Awake() => CacheReferences();
    private void OnEnable() => CacheReferences();

    private void CacheReferences()
    {
        if (identity == null)
            TryGetComponent(out identity);

        if (movement == null)
            TryGetComponent(out movement);

        if (fallingBombController == null)
            fallingBombController = FindAnyObjectByType<BattleMode2FallingBombController>();

        if (movement != null)
            tileSize = Mathf.Max(0.01f, movement.tileSize);
    }

    public bool TryGetDangerSeconds(Vector2Int tile, out float dangerSeconds)
    {
        dangerSeconds = float.PositiveInfinity;

        if (!IsAvailable ||
            !fallingBombController.TryGetActiveTargetWarning(
                out Vector2 targetWorld,
                out int explosionRadius,
                out float warningSeconds))
        {
            return false;
        }

        Vector2Int targetTile = WorldToTile(targetWorld);
        if (!IsTileInBlastZone(targetTile, tile, explosionRadius))
            return false;

        dangerSeconds = Mathf.Max(0.01f, warningSeconds);
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
        lastDecisionTrace = "emergency start";

        if (!TryGetDangerSeconds(myTile, out float warningSeconds))
        {
            lastDecisionTrace = "emergency outside falling bomb blast";
            return false;
        }

        if (controller == null ||
            !controller.TryFindAbilityEscape(
                settings,
                myTile,
                out Vector2 firstMove,
                out Vector2Int targetTile,
                out string route))
        {
            lastDecisionTrace =
                $"emergency warning:{warningSeconds:F2} no safe escape route";
            return false;
        }

        lastDecisionTrace =
            $"emergency warning:{warningSeconds:F2} target:{targetTile} route:{route}";
        decision = new BattleModeComAbilityDecision
        {
            Action = BattleModeComActionType.Reposition,
            Weight = settings.patrolWeight + 500,
            TargetTile = targetTile,
            HasTarget = true,
            FirstMove = firstMove,
            Reason = "escape stage 2 falling bomb blast",
            InputDescription = FirstMoveDescription(firstMove)
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
        lastDecisionTrace = TryGetDangerSeconds(myTile, out float warningSeconds)
            ? $"candidate warning:{warningSeconds:F2} handled as emergency"
            : "candidate no falling bomb threat";
        return false;
    }

    private Vector2Int WorldToTile(Vector2 world)
    {
        float size = Mathf.Max(0.01f, tileSize);
        return new Vector2Int(
            Mathf.RoundToInt(world.x / size),
            Mathf.RoundToInt(world.y / size));
    }

    private static bool IsTileInBlastZone(
        Vector2Int bombTile,
        Vector2Int tile,
        int radius)
    {
        if (bombTile == tile)
            return true;

        Vector2Int delta = tile - bombTile;
        int distance = Mathf.Abs(delta.x) + Mathf.Abs(delta.y);
        bool sameAxis = delta.x == 0 || delta.y == 0;
        return sameAxis && distance <= Mathf.Max(1, radius);
    }

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
}

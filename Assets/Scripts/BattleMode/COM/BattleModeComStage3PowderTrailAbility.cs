using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerIdentity))]
[RequireComponent(typeof(MovementController))]
[RequireComponent(typeof(BattleModeComController))]
public sealed class BattleModeComStage3PowderTrailAbility :
    MonoBehaviour,
    IBattleModeComStageAbility,
    IBattleModeComDangerProvider,
    IBattleModeComPlannedBombDangerProvider
{
    private readonly List<Vector2> trailWorldPositions = new();
    private readonly HashSet<Vector2Int> trailTiles = new();
    private readonly HashSet<Vector2Int> ignoredBombTiles = new();
    private readonly List<Bomb> activeBombs = new();
    private readonly List<float> predictedDetonationSeconds = new();

    private PlayerIdentity identity;
    private MovementController movement;
    private BattleModeComController comController;
    private BattleMode3PowderTrailController powderTrailController;
    private float tileSize = 1f;
    private int predictionFrame = -1;
    private float predictedTrailDangerSeconds = float.PositiveInfinity;
    private Vector2Int predictedTriggerBombTile;
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
                   powderTrailController != null &&
                   powderTrailController.isActiveAndEnabled &&
                   trailTiles.Count > 0;
        }
    }

    public string DiagnosticName => "Stage3PowderTrail";
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

        if (powderTrailController == null)
        {
            powderTrailController =
                FindAnyObjectByType<BattleMode3PowderTrailController>();
        }

        if (movement != null)
            tileSize = Mathf.Max(0.01f, movement.tileSize);

        if (powderTrailController != null && trailTiles.Count == 0)
            RefreshTrailTiles();
    }

    private void RefreshTrailTiles()
    {
        trailTiles.Clear();
        trailWorldPositions.Clear();
        powderTrailController.CopyTrailWorldPositions(trailWorldPositions);

        for (int i = 0; i < trailWorldPositions.Count; i++)
            trailTiles.Add(WorldToTile(trailWorldPositions[i]));

        predictionFrame = -1;
    }

    public bool TryGetDangerSeconds(Vector2Int tile, out float dangerSeconds)
    {
        dangerSeconds = float.PositiveInfinity;

        if (!IsAvailable || !trailTiles.Contains(tile))
            return false;

        RefreshPrediction();
        if (float.IsInfinity(predictedTrailDangerSeconds))
            return false;

        dangerSeconds = predictedTrailDangerSeconds;
        return true;
    }

    public bool TryAppendPlannedBombDangerTiles(
        Vector2Int plantTile,
        List<Vector2Int> plannedDangerTiles)
    {
        if (!IsAvailable || plannedDangerTiles == null)
            return false;

        bool triggersTrail = false;
        foreach (Vector2Int trailTile in trailTiles)
        {
            if (!plannedDangerTiles.Contains(trailTile))
                continue;

            triggersTrail = true;
            break;
        }

        if (!triggersTrail)
            return false;

        foreach (Vector2Int trailTile in trailTiles)
        {
            if (!plannedDangerTiles.Contains(trailTile))
                plannedDangerTiles.Add(trailTile);
        }

        return true;
    }

    private void RefreshPrediction()
    {
        if (predictionFrame == Time.frameCount)
            return;

        predictionFrame = Time.frameCount;
        predictedTrailDangerSeconds = float.PositiveInfinity;
        predictedTriggerBombTile = default;

        if (powderTrailController.IgnitionRunning)
        {
            predictedTrailDangerSeconds = 0f;
            return;
        }

        BuildBombPrediction();

        for (int i = 0; i < activeBombs.Count; i++)
        {
            Bomb bomb = activeBombs[i];
            BuildIgnoredBombTiles(predictedDetonationSeconds[i]);

            if (!DoesBombReachPowderTrail(bomb, ignoredBombTiles))
                continue;

            float dangerSeconds = predictedDetonationSeconds[i];

            if (dangerSeconds >= predictedTrailDangerSeconds)
                continue;

            predictedTrailDangerSeconds = Mathf.Max(0f, dangerSeconds);
            predictedTriggerBombTile = WorldToTile(bomb.GetLogicalPosition());
        }
    }

    private void BuildBombPrediction()
    {
        activeBombs.Clear();
        predictedDetonationSeconds.Clear();

        foreach (Bomb bomb in Bomb.ActiveBombs)
        {
            if (bomb == null ||
                bomb.HasExploded ||
                bomb.IsBeingHeldByPowerGlove)
            {
                continue;
            }

            activeBombs.Add(bomb);
            predictedDetonationSeconds.Add(
                bomb.IsControlBomb ? 0.65f : bomb.RemainingFuseSeconds);
        }

        // Relax chain timings so a bomb triggered by another uses the earlier
        // detonation time instead of its original fuse.
        for (int pass = 0; pass < activeBombs.Count - 1; pass++)
        {
            bool changed = false;

            for (int sourceIndex = 0; sourceIndex < activeBombs.Count; sourceIndex++)
            {
                Bomb sourceBomb = activeBombs[sourceIndex];
                float sourceSeconds = predictedDetonationSeconds[sourceIndex];
                BuildIgnoredBombTiles(sourceSeconds);

                for (int targetIndex = 0; targetIndex < activeBombs.Count; targetIndex++)
                {
                    if (sourceIndex == targetIndex)
                        continue;

                    Bomb targetBomb = activeBombs[targetIndex];
                    float chainedSeconds =
                        sourceSeconds +
                        Mathf.Max(0f, targetBomb.chainStepDelay);

                    if (chainedSeconds >= predictedDetonationSeconds[targetIndex])
                        continue;

                    Vector2Int targetTile =
                        WorldToTile(targetBomb.GetLogicalPosition());

                    if (!comController.DoesBombBlastReachTile(
                            sourceBomb,
                            targetTile,
                            ignoredBombTiles))
                    {
                        continue;
                    }

                    predictedDetonationSeconds[targetIndex] = chainedSeconds;
                    changed = true;
                }
            }

            if (!changed)
                break;
        }
    }

    private void BuildIgnoredBombTiles(float detonationSeconds)
    {
        ignoredBombTiles.Clear();

        for (int i = 0; i < activeBombs.Count; i++)
        {
            if (predictedDetonationSeconds[i] > detonationSeconds)
                continue;

            ignoredBombTiles.Add(
                WorldToTile(activeBombs[i].GetLogicalPosition()));
        }
    }

    private bool DoesBombReachPowderTrail(
        Bomb bomb,
        ICollection<Vector2Int> ignoredTiles)
    {
        foreach (Vector2Int trailTile in trailTiles)
        {
            if (comController.DoesBombBlastReachTile(
                    bomb,
                    trailTile,
                    ignoredTiles))
            {
                return true;
            }
        }

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
        lastDecisionTrace = "emergency start";

        if (!TryGetDangerSeconds(myTile, out float trailDangerSeconds))
        {
            lastDecisionTrace = "emergency outside powder trail danger";
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
                $"emergency trailDanger:{trailDangerSeconds:F2} no safe escape route";
            return false;
        }

        string trigger = powderTrailController.IgnitionRunning
            ? $"active:{powderTrailController.IgnitionSecondsRemaining:F2}"
            : $"bomb:{predictedTriggerBombTile}";

        lastDecisionTrace =
            $"emergency trailDanger:{trailDangerSeconds:F2} trigger:{trigger} " +
            $"target:{targetTile} route:{route}";
        decision = new BattleModeComAbilityDecision
        {
            Action = BattleModeComActionType.Reposition,
            Weight = settings.patrolWeight + 500,
            TargetTile = targetTile,
            HasTarget = true,
            FirstMove = firstMove,
            Reason = "escape stage 3 powder trail explosion",
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
        lastDecisionTrace = TryGetDangerSeconds(myTile, out float trailDangerSeconds)
            ? $"candidate trailDanger:{trailDangerSeconds:F2} handled as emergency"
            : "candidate no powder trail threat";
        return false;
    }

    private Vector2Int WorldToTile(Vector2 world)
    {
        float size = Mathf.Max(0.01f, tileSize);
        return new Vector2Int(
            Mathf.RoundToInt(world.x / size),
            Mathf.RoundToInt(world.y / size));
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

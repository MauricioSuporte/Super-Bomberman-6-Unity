using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerIdentity))]
[RequireComponent(typeof(MovementController))]
[RequireComponent(typeof(BombController))]
[RequireComponent(typeof(BattleModeComController))]
public sealed class BattleModeComStage5ConveyorAwarenessAbility :
    MonoBehaviour,
    IBattleModeComStageAbility,
    IBattleModeComDangerProvider,
    IBattleModeComPlannedBombDangerProvider
{
    private readonly List<Bomb> predictedBombs = new();
    private readonly List<float> predictedDetonationSeconds = new();
    private readonly List<Vector2Int> predictedBombTiles = new();
    private readonly List<Vector2> conveyorWorldPositions = new();
    private readonly List<Vector2Int> predictedPlannedBlastTiles = new();

    private PlayerIdentity identity;
    private MovementController movement;
    private BombController bombController;
    private BattleModeComController comController;
    private BattleMode5ConveyorController conveyorController;
    private float tileSize = 1f;
    private int predictionFrame = -1;
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
                   conveyorController != null &&
                   conveyorController.isActiveAndEnabled;
        }
    }

    public string DiagnosticName => "Stage5ConveyorAwareness";
    public string LastDecisionTrace => lastDecisionTrace;

    private void Awake() => CacheReferences();
    private void OnEnable() => CacheReferences();

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

        if (conveyorController == null)
        {
            conveyorController =
                FindAnyObjectByType<BattleMode5ConveyorController>();
        }

        if (movement != null)
            tileSize = Mathf.Max(0.01f, movement.tileSize);
    }

    public bool TryGetDangerSeconds(
        Vector2Int tile,
        out float dangerSeconds)
    {
        dangerSeconds = float.PositiveInfinity;
        if (!IsAvailable)
            return false;

        RefreshBombPredictions();

        for (int i = 0; i < predictedBombs.Count; i++)
        {
            float detonationSeconds = predictedDetonationSeconds[i];
            Vector2 predictedPlayerWorld = TileToWorld(tile);
            if (conveyorController.TryPredictConveyorWorldPosition(
                predictedPlayerWorld,
                detonationSeconds,
                out Vector2 carriedPlayerWorld))
            {
                predictedPlayerWorld = carriedPlayerWorld;
            }

            Vector2Int predictedPlayerTile =
                WorldToTile(predictedPlayerWorld);
            if (!comController.DoesPredictedBombBlastReachTile(
                    predictedBombs[i],
                    predictedBombTiles[i],
                    predictedPlayerTile))
            {
                continue;
            }

            dangerSeconds = Mathf.Min(
                dangerSeconds,
                Mathf.Max(0f, detonationSeconds));
        }

        return !float.IsInfinity(dangerSeconds);
    }

    public bool TryAppendPlannedBombDangerTiles(
        Vector2Int plantTile,
        List<Vector2Int> plannedDangerTiles)
    {
        if (!IsAvailable || plannedDangerTiles == null)
            return false;

        float fuseSeconds = Mathf.Max(0.01f, bombController.bombFuseTime);
        Vector2 plantWorld = TileToWorld(plantTile);
        if (!conveyorController.TryPredictConveyorWorldPosition(
                plantWorld,
                fuseSeconds,
                out Vector2 predictedWorld))
        {
            return false;
        }

        Vector2Int predictedTile = WorldToTile(predictedWorld);
        if (predictedTile == plantTile)
            return false;

        predictedPlannedBlastTiles.Clear();
        comController.AppendAbilityBlastTiles(
            predictedTile,
            bombController.GetPlannedExplosionRadius(),
            predictedPlannedBlastTiles);

        for (int i = 0; i < predictedPlannedBlastTiles.Count; i++)
        {
            if (!plannedDangerTiles.Contains(predictedPlannedBlastTiles[i]))
                plannedDangerTiles.Add(predictedPlannedBlastTiles[i]);
        }

        conveyorWorldPositions.Clear();
        conveyorController.CopyConveyorWorldPositions(conveyorWorldPositions);
        for (int i = 0; i < conveyorWorldPositions.Count; i++)
        {
            Vector2 startWorld = conveyorWorldPositions[i];
            Vector2 carriedWorld = startWorld;
            conveyorController.TryPredictConveyorWorldPosition(
                startWorld,
                fuseSeconds,
                out carriedWorld);

            if (!predictedPlannedBlastTiles.Contains(WorldToTile(carriedWorld)))
                continue;

            Vector2Int carriedFromTile = WorldToTile(startWorld);
            if (!plannedDangerTiles.Contains(carriedFromTile))
                plannedDangerTiles.Add(carriedFromTile);
        }

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

        if (!TryGetDangerSeconds(myTile, out float conveyorDangerSeconds))
        {
            lastDecisionTrace = "emergency no conveyor-carried blast threat";
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
                $"emergency danger:{conveyorDangerSeconds:F2} no safe route";
            return false;
        }

        string motion = FormatConveyorMotion(myTile);
        lastDecisionTrace =
            $"emergency danger:{conveyorDangerSeconds:F2} {motion} " +
            $"target:{targetTile} route:{route}";
        decision = new BattleModeComAbilityDecision
        {
            Action = BattleModeComActionType.Reposition,
            Weight = settings.patrolWeight + 525,
            TargetTile = targetTile,
            HasTarget = true,
            FirstMove = firstMove,
            Reason = "escape stage 5 conveyor-carried blast",
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
        lastDecisionTrace = TryGetDangerSeconds(myTile, out float dangerSeconds)
            ? $"candidate danger:{dangerSeconds:F2} handled as emergency"
            : $"candidate aware {FormatConveyorMotion(myTile)}";
        return false;
    }

    private void RefreshBombPredictions()
    {
        if (predictionFrame == Time.frameCount)
            return;

        predictionFrame = Time.frameCount;
        predictedBombs.Clear();
        predictedDetonationSeconds.Clear();
        predictedBombTiles.Clear();

        foreach (Bomb bomb in Bomb.ActiveBombs)
        {
            if (bomb == null ||
                bomb.HasExploded ||
                bomb.IsBeingHeldByPowerGlove)
            {
                continue;
            }

            float detonationSeconds =
                bomb.IsControlBomb ? 0.65f : bomb.RemainingFuseSeconds;

            predictedBombs.Add(bomb);
            predictedDetonationSeconds.Add(detonationSeconds);
            predictedBombTiles.Add(
                PredictBombTile(bomb, detonationSeconds));
        }

        for (int pass = 0; pass < predictedBombs.Count - 1; pass++)
        {
            bool changed = false;

            for (int sourceIndex = 0; sourceIndex < predictedBombs.Count; sourceIndex++)
            {
                Bomb sourceBomb = predictedBombs[sourceIndex];
                float sourceSeconds = predictedDetonationSeconds[sourceIndex];
                Vector2Int sourceTile =
                    PredictBombTile(sourceBomb, sourceSeconds);

                for (int targetIndex = 0; targetIndex < predictedBombs.Count; targetIndex++)
                {
                    if (sourceIndex == targetIndex)
                        continue;

                    Bomb targetBomb = predictedBombs[targetIndex];
                    float chainedSeconds =
                        sourceSeconds + Mathf.Max(0f, targetBomb.chainStepDelay);
                    if (chainedSeconds >= predictedDetonationSeconds[targetIndex])
                        continue;

                    Vector2Int targetTile =
                        PredictBombTile(targetBomb, sourceSeconds);
                    if (!comController.DoesPredictedBombBlastReachTile(
                            sourceBomb,
                            sourceTile,
                            targetTile))
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

        for (int i = 0; i < predictedBombs.Count; i++)
        {
            predictedBombTiles[i] = PredictBombTile(
                predictedBombs[i],
                predictedDetonationSeconds[i]);
        }
    }

    private Vector2Int PredictBombTile(Bomb bomb, float seconds)
    {
        Vector2 currentWorld = bomb.GetLogicalPosition();
        if (bomb.IsBeingKicked ||
            bomb.IsBeingPunched ||
            bomb.IsBeingMagnetPulled)
        {
            return WorldToTile(currentWorld);
        }

        Vector2 predictedWorld = currentWorld;
        if (conveyorController.TryPredictConveyorWorldPosition(
                currentWorld,
                seconds,
                out Vector2 carriedWorld))
        {
            predictedWorld = carriedWorld;
        }

        return WorldToTile(predictedWorld);
    }

    private string FormatConveyorMotion(Vector2Int tile)
    {
        if (!conveyorController.TryGetConveyorMotion(
                TileToWorld(tile),
                out Vector2 direction,
                out float speed))
        {
            return "off-conveyor";
        }

        return
            $"conveyor:{FirstMoveDescription(direction)} " +
            $"speed:{speed:F2} state:" +
            $"{(conveyorController.IsFast ? "fast" : "slow")}/" +
            $"{(conveyorController.IsClockwise ? "clockwise" : "counterclockwise")}";
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
}

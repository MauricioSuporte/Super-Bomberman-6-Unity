using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerIdentity))]
[RequireComponent(typeof(MovementController))]
[RequireComponent(typeof(BombController))]
[RequireComponent(typeof(BattleModeComController))]
public sealed class BattleModeComStage8PowerGroundAwarenessAbility :
    MonoBehaviour,
    IBattleModeComStageAbility,
    IBattleModeComDangerProvider,
    IBattleModeComPlannedBombDangerProvider
{
    private readonly List<Vector2Int> boostedBlastTiles = new();

    private PlayerIdentity identity;
    private MovementController movement;
    private BombController bombController;
    private BattleModeComController comController;
    private PowerGroundTileHandler powerGroundHandler;
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
                   bombController != null &&
                   comController != null &&
                   powerGroundHandler != null &&
                   powerGroundHandler.isActiveAndEnabled;
        }
    }

    public string DiagnosticName => "Stage8PowerGround";
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

        if (powerGroundHandler == null)
        {
            powerGroundHandler =
                FindAnyObjectByType<PowerGroundTileHandler>();
        }

        if (movement != null)
            tileSize = Mathf.Max(0.01f, movement.tileSize);
    }

    public int GetEffectivePlannedRadius(
        Vector2Int plantTile,
        int baseRadius)
    {
        int radius = Mathf.Max(1, baseRadius);
        if (!IsAvailable ||
            !powerGroundHandler.TryGetBoostedRadiusAtWorldPosition(
                TileToWorld(plantTile),
                out int boostedRadius))
        {
            return radius;
        }

        return Mathf.Max(radius, boostedRadius);
    }

    public bool TryGetDangerSeconds(
        Vector2Int tile,
        out float dangerSeconds)
    {
        dangerSeconds = float.PositiveInfinity;
        if (!IsAvailable)
            return false;

        foreach (Bomb bomb in Bomb.ActiveBombs)
        {
            if (bomb == null ||
                bomb.HasExploded ||
                bomb.IsBeingHeldByPowerGlove)
            {
                continue;
            }

            Vector2 bombWorld = bomb.GetLogicalPosition();
            if (!powerGroundHandler.TryGetBoostedRadiusAtWorldPosition(
                    bombWorld,
                    out int boostedRadius))
            {
                continue;
            }

            int normalRadius = bomb.Owner != null
                ? Mathf.Max(
                    1,
                    bomb.Owner.GetPredictedBlastRadius(bomb))
                : Mathf.Max(
                    1,
                    bomb.ExplosionRadiusOverride > 0
                        ? bomb.ExplosionRadiusOverride
                        : 2);
            int effectiveRadius =
                Mathf.Max(normalRadius, boostedRadius);
            Vector2Int bombTile = WorldToTile(bombWorld);
            if (!comController.DoesBombBlastReachTileWithRadius(
                    bomb,
                    bombTile,
                    tile,
                    effectiveRadius))
            {
                continue;
            }

            float seconds = bomb.IsControlBomb
                ? 0.65f
                : bomb.RemainingFuseSeconds;
            dangerSeconds = Mathf.Min(
                dangerSeconds,
                Mathf.Max(0f, seconds));
        }

        return !float.IsInfinity(dangerSeconds);
    }

    public bool TryAppendPlannedBombDangerTiles(
        Vector2Int plantTile,
        List<Vector2Int> plannedDangerTiles)
    {
        if (!IsAvailable ||
            plannedDangerTiles == null ||
            !powerGroundHandler.TryGetBoostedRadiusAtWorldPosition(
                TileToWorld(plantTile),
                out int boostedRadius))
        {
            return false;
        }

        int normalRadius =
            Mathf.Max(1, bombController.GetPlannedExplosionRadius());
        int effectiveRadius =
            Mathf.Max(normalRadius, boostedRadius);
        if (effectiveRadius <= normalRadius)
            return false;

        boostedBlastTiles.Clear();
        comController.AppendAbilityBlastTiles(
            plantTile,
            effectiveRadius,
            boostedBlastTiles);

        bool expanded = false;
        for (int i = 0; i < boostedBlastTiles.Count; i++)
        {
            Vector2Int blastTile = boostedBlastTiles[i];
            if (plannedDangerTiles.Contains(blastTile))
                continue;

            plannedDangerTiles.Add(blastTile);
            expanded = true;
        }

        return expanded;
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
            TryGetDangerSeconds(myTile, out float dangerSeconds)
                ? $"emergency boosted blast danger:{dangerSeconds:F2}s"
                : "emergency no boosted blast danger";
        return false;
    }

    public bool TryBuildCandidateDecision(
        BattleModeComDifficultySettings settings,
        BattleModeComController controller,
        Vector2Int myTile,
        out BattleModeComAbilityDecision decision)
    {
        decision = default;
        int boostedRadius = 0;
        bool onPowerGround =
            powerGroundHandler != null &&
            powerGroundHandler.TryGetBoostedRadiusAtWorldPosition(
                TileToWorld(myTile),
                out boostedRadius);
        lastDecisionTrace = onPowerGround
            ? $"candidate power ground radius:{boostedRadius}"
            : "candidate aware of power ground";
        return false;
    }

    private Vector2Int WorldToTile(Vector2 world)
    {
        float size = Mathf.Max(0.01f, tileSize);
        return new Vector2Int(
            Mathf.RoundToInt(world.x / size),
            Mathf.RoundToInt(world.y / size));
    }

    private Vector2 TileToWorld(Vector2Int tile)
    {
        float size = Mathf.Max(0.01f, tileSize);
        return new Vector2(tile.x * size, tile.y * size);
    }
}

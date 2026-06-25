using UnityEngine;
using UnityEngine.Tilemaps;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerIdentity))]
[RequireComponent(typeof(MovementController))]
[RequireComponent(typeof(BattleModeComController))]
public sealed class BattleModeComStage15PassThroughBlastAbility :
    MonoBehaviour,
    IBattleModeComStageAbility
{
    private PlayerIdentity identity;
    private MovementController movement;
    private BattleModeComController comController;
    private PassThroughIndestructibleTileHandler passThroughHandler;
    private Tilemap indestructibleTilemap;
    private int passThroughTileCount;
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
                   passThroughHandler != null &&
                   passThroughHandler.isActiveAndEnabled &&
                   passThroughTileCount > 0;
        }
    }

    public string DiagnosticName => "Stage15PassThroughBlast";
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

        if (passThroughHandler == null)
        {
            passThroughHandler =
                FindAnyObjectByType<PassThroughIndestructibleTileHandler>();
        }

        if (indestructibleTilemap == null && GameManager.Instance != null)
            indestructibleTilemap = GameManager.Instance.indestructibleTilemap;

        if (passThroughTileCount == 0 &&
            comController != null &&
            indestructibleTilemap != null)
        {
            RefreshPassThroughTileCount();
        }
    }

    private void RefreshPassThroughTileCount()
    {
        passThroughTileCount = 0;
        BoundsInt bounds = indestructibleTilemap.cellBounds;
        foreach (Vector3Int cell in bounds.allPositionsWithin)
        {
            Vector3 world = indestructibleTilemap.GetCellCenterWorld(cell);
            if (comController.IsExplosionPassThroughTile(
                    WorldToTile(world)))
            {
                passThroughTileCount++;
            }
        }
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
            $"emergency central blast model aware of " +
            $"{passThroughTileCount} pass-through tiles";
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
            $"candidate routes and chains use " +
            $"{passThroughTileCount} pass-through tiles";
        return false;
    }

    private Vector2Int WorldToTile(Vector3 world)
    {
        float tileSize = movement != null
            ? Mathf.Max(0.01f, movement.tileSize)
            : 1f;
        return new Vector2Int(
            Mathf.RoundToInt(world.x / tileSize),
            Mathf.RoundToInt(world.y / tileSize));
    }
}

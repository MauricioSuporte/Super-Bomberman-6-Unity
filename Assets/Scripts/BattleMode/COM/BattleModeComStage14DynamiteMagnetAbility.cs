using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerIdentity))]
[RequireComponent(typeof(MovementController))]
[RequireComponent(typeof(BombController))]
[RequireComponent(typeof(BattleModeComController))]
public sealed class BattleModeComStage14DynamiteMagnetAbility :
    MonoBehaviour,
    IBattleModeComStageAbility,
    IBattleModeComDangerProvider,
    IBattleModeComPlannedBombDangerProvider
{
    private static readonly Vector2Int[] CardinalDirections =
    {
        Vector2Int.up,
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.right
    };

    private readonly List<Vector2Int> dynamiteTiles = new();
    private readonly List<float> dynamiteDetonationSeconds = new();
    private readonly List<Vector3Int> scheduledCells = new();
    private readonly List<float> scheduledSeconds = new();
    private readonly List<Vector2Int> expandedBlastTiles = new();

    private PlayerIdentity identity;
    private MovementController movement;
    private BombController bombController;
    private BattleModeComController comController;
    private DynamiteTileHandler dynamiteHandler;
    private MagnetIndestructibleTileHandler magnetHandler;
    private DestructibleTileResolver destructibleResolver;
    private Tilemap destructibleTilemap;
    private Tilemap indestructibleTilemap;
    private int predictionFrame = -1;
    private string lastDecisionTrace = "not evaluated";
    private string lastDiagnosticKey;
    private float lastDiagnosticTime = float.NegativeInfinity;

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
                   dynamiteHandler != null &&
                   magnetHandler != null;
        }
    }

    public string DiagnosticName => "Stage14DynamiteMagnet";
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

        if (dynamiteHandler == null)
            dynamiteHandler = FindAnyObjectByType<DynamiteTileHandler>();

        if (magnetHandler == null)
        {
            magnetHandler =
                FindAnyObjectByType<MagnetIndestructibleTileHandler>();
        }

        GameManager gameManager = GameManager.Instance;
        if (gameManager != null)
        {
            destructibleTilemap = gameManager.destructibleTilemap;
            indestructibleTilemap = gameManager.indestructibleTilemap;
        }

        if (destructibleResolver == null)
        {
            destructibleResolver =
                FindAnyObjectByType<DestructibleTileResolver>();
        }
    }

    public bool TryGetDangerSeconds(
        Vector2Int tile,
        out float dangerSeconds)
    {
        dangerSeconds = float.PositiveInfinity;
        if (!IsAvailable)
            return false;

        RefreshPrediction();
        for (int i = 0; i < dynamiteTiles.Count; i++)
        {
            float seconds = dynamiteDetonationSeconds[i];
            if (float.IsInfinity(seconds) ||
                !DoesDynamiteBlastReachTile(dynamiteTiles[i], tile))
            {
                continue;
            }

            dangerSeconds = Mathf.Min(dangerSeconds, seconds);
        }

        foreach (Bomb bomb in Bomb.ActiveBombs)
        {
            if (!TryGetActiveBombPrediction(
                    bomb,
                    out Vector2Int sourceTile,
                    out Vector2Int predictedTile))
            {
                continue;
            }

            if (predictedTile == sourceTile)
            {
                continue;
            }

            bool reachesTile = false;
            Vector2Int travelDirection = CardinalStep(sourceTile, predictedTile);
            int travelSteps = Manhattan(sourceTile, predictedTile);
            for (int step = 1; step <= travelSteps; step++)
            {
                Vector2Int possibleBombTile =
                    sourceTile + travelDirection * step;
                if (!comController.DoesPredictedBombBlastReachTile(
                        bomb,
                        possibleBombTile,
                        tile))
                {
                    continue;
                }

                reachesTile = true;
                break;
            }

            if (!reachesTile)
                continue;

            float seconds = bomb.IsControlBomb
                ? 0.65f
                : bomb.RemainingFuseSeconds;
            dangerSeconds = Mathf.Min(dangerSeconds, Mathf.Max(0f, seconds));
        }

        return !float.IsInfinity(dangerSeconds);
    }

    public bool TryAppendPlannedBombDangerTiles(
        Vector2Int plantTile,
        List<Vector2Int> plannedDangerTiles)
    {
        if (!IsAvailable || plannedDangerTiles == null)
            return false;

        bool expanded = false;
        Vector2Int predictedTile = plantTile;
        if (magnetHandler.TryPredictBombPull(plantTile, out var pull))
        {
            predictedTile = pull.DestinationTile;
            Vector2Int travelDirection = CardinalStep(plantTile, predictedTile);
            int travelSteps = Manhattan(plantTile, predictedTile);
            for (int step = 1; step <= travelSteps; step++)
            {
                expandedBlastTiles.Clear();
                comController.AppendAbilityBlastTiles(
                    plantTile + travelDirection * step,
                    Mathf.Max(1, bombController.GetPlannedExplosionRadius()),
                    expandedBlastTiles);
                expanded |= AppendUnique(
                    expandedBlastTiles,
                    plannedDangerTiles);
            }
        }

        RefreshDynamiteTiles();
        bool foundTriggeredDynamite;
        do
        {
            foundTriggeredDynamite = false;
            for (int i = 0; i < dynamiteTiles.Count; i++)
            {
                Vector2Int dynamiteTile = dynamiteTiles[i];
                if (!plannedDangerTiles.Contains(dynamiteTile))
                    continue;

                expandedBlastTiles.Clear();
                AppendDynamiteBlastTiles(dynamiteTile, expandedBlastTiles);
                if (!AppendUnique(expandedBlastTiles, plannedDangerTiles))
                    continue;

                expanded = true;
                foundTriggeredDynamite = true;
            }
        }
        while (foundTriggeredDynamite);

        if (expanded)
        {
            lastDecisionTrace =
                $"planned bomb:{plantTile} projected:{predictedTile} " +
                $"dangerTiles:{plannedDangerTiles.Count}";
        }

        predictionFrame = -1;
        return expanded;
    }

    private void RefreshPrediction()
    {
        if (predictionFrame == Time.frameCount)
            return;

        predictionFrame = Time.frameCount;
        RefreshDynamiteTiles();
        dynamiteDetonationSeconds.Clear();
        for (int i = 0; i < dynamiteTiles.Count; i++)
            dynamiteDetonationSeconds.Add(float.PositiveInfinity);

        scheduledCells.Clear();
        scheduledSeconds.Clear();
        dynamiteHandler.CopyScheduledDetonations(
            scheduledCells,
            scheduledSeconds);
        for (int i = 0; i < scheduledCells.Count; i++)
        {
            Vector2Int tile = ToTile(scheduledCells[i]);
            int index = EnsureDynamiteTile(tile);
            dynamiteDetonationSeconds[index] =
                Mathf.Min(dynamiteDetonationSeconds[index], scheduledSeconds[i]);
        }

        foreach (Bomb bomb in Bomb.ActiveBombs)
        {
            if (!TryGetActiveBombPrediction(
                    bomb,
                    out Vector2Int sourceTile,
                    out Vector2Int predictedTile))
            {
                continue;
            }

            float bombSeconds = bomb.IsControlBomb
                ? 0.65f
                : bomb.RemainingFuseSeconds;
            for (int i = 0; i < dynamiteTiles.Count; i++)
            {
                if (!CanBombReachTileAlongMagnetPath(
                        bomb,
                        sourceTile,
                        predictedTile,
                        dynamiteTiles[i]))
                {
                    continue;
                }

                dynamiteDetonationSeconds[i] = Mathf.Min(
                    dynamiteDetonationSeconds[i],
                    Mathf.Max(0f, bombSeconds) +
                    dynamiteHandler.TriggerDelaySeconds);

                if (predictedTile != sourceTile)
                {
                    LogDiagnostic(
                        "MAGNET_TRIGGER",
                        $"bomb:{sourceTile}->{predictedTile} dynamite:{dynamiteTiles[i]} " +
                        $"danger:{dynamiteDetonationSeconds[i]:F2}s");
                }
            }
        }

        for (int pass = 0; pass < dynamiteTiles.Count - 1; pass++)
        {
            bool changed = false;
            for (int source = 0; source < dynamiteTiles.Count; source++)
            {
                float sourceSeconds = dynamiteDetonationSeconds[source];
                if (float.IsInfinity(sourceSeconds))
                    continue;

                for (int target = 0; target < dynamiteTiles.Count; target++)
                {
                    if (source == target ||
                        !DoesDynamiteBlastReachTile(
                            dynamiteTiles[source],
                            dynamiteTiles[target]))
                    {
                        continue;
                    }

                    float chainedSeconds =
                        sourceSeconds + dynamiteHandler.TriggerDelaySeconds;
                    if (chainedSeconds >= dynamiteDetonationSeconds[target])
                        continue;

                    dynamiteDetonationSeconds[target] = chainedSeconds;
                    changed = true;
                }
            }

            if (!changed)
                break;
        }
    }

    private void RefreshDynamiteTiles()
    {
        dynamiteTiles.Clear();
        if (destructibleTilemap == null ||
            destructibleResolver == null ||
            dynamiteHandler == null)
        {
            return;
        }

        BoundsInt bounds = destructibleTilemap.cellBounds;
        foreach (Vector3Int cell in bounds.allPositionsWithin)
        {
            TileBase tile = destructibleTilemap.GetTile(cell);
            if (!destructibleResolver.TryGetHandler(tile, out var handler) ||
                (Object)handler != dynamiteHandler)
            {
                continue;
            }

            dynamiteTiles.Add(ToTile(cell));
        }
    }

    private int EnsureDynamiteTile(Vector2Int tile)
    {
        int index = dynamiteTiles.IndexOf(tile);
        if (index >= 0)
            return index;

        dynamiteTiles.Add(tile);
        dynamiteDetonationSeconds.Add(float.PositiveInfinity);
        return dynamiteTiles.Count - 1;
    }

    private bool TryGetActiveBombPrediction(
        Bomb bomb,
        out Vector2Int sourceTile,
        out Vector2Int predictedTile)
    {
        sourceTile = default;
        predictedTile = default;
        if (bomb == null ||
            bomb.HasExploded ||
            bomb.IsBeingHeldByPowerGlove)
        {
            return false;
        }

        sourceTile = WorldToTile(bomb.GetLogicalPosition());
        predictedTile = sourceTile;
        if ((bomb.CanBeMagnetPulled || bomb.IsBeingMagnetPulled) &&
            magnetHandler.TryPredictBombPull(sourceTile, out var pull))
        {
            predictedTile = pull.DestinationTile;
        }

        return true;
    }

    private bool CanBombReachTileAlongMagnetPath(
        Bomb bomb,
        Vector2Int sourceTile,
        Vector2Int predictedTile,
        Vector2Int targetTile)
    {
        Vector2Int travelDirection = CardinalStep(sourceTile, predictedTile);
        int travelSteps = Manhattan(sourceTile, predictedTile);
        for (int step = 0; step <= travelSteps; step++)
        {
            Vector2Int possibleBombTile =
                sourceTile + travelDirection * step;
            if (comController.DoesPredictedBombBlastReachTile(
                    bomb,
                    possibleBombTile,
                    targetTile))
            {
                return true;
            }
        }

        return false;
    }

    private bool DoesDynamiteBlastReachTile(
        Vector2Int dynamiteTile,
        Vector2Int targetTile)
    {
        int radius = dynamiteHandler.ExplosionRadius;
        if (IsPierceCrossReaching(dynamiteTile, targetTile, radius))
            return true;

        for (int i = 0; i < CardinalDirections.Length; i++)
        {
            Vector2Int secondaryOrigin =
                dynamiteTile + CardinalDirections[i] * radius;
            if (IsPierceCrossReaching(
                    secondaryOrigin,
                    targetTile,
                    radius))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsPierceCrossReaching(
        Vector2Int origin,
        Vector2Int target,
        int radius)
    {
        if (origin == target)
            return true;

        Vector2Int delta = target - origin;
        if ((delta.x == 0) == (delta.y == 0))
            return false;

        int distance = Mathf.Abs(delta.x) + Mathf.Abs(delta.y);
        if (distance > radius)
            return false;

        Vector2Int direction = new(
            Mathf.Clamp(delta.x, -1, 1),
            Mathf.Clamp(delta.y, -1, 1));
        for (int step = 1; step < distance; step++)
        {
            Vector2Int check = origin + direction * step;
            if (HasBlockingIndestructible(check))
                return false;
        }

        return true;
    }

    private bool HasBlockingIndestructible(Vector2Int tile)
    {
        if (magnetHandler.IsMagnetCell(tile))
            return false;

        return indestructibleTilemap != null &&
               indestructibleTilemap.HasTile(
                   new Vector3Int(tile.x, tile.y, 0));
    }

    private void AppendDynamiteBlastTiles(
        Vector2Int origin,
        List<Vector2Int> destination)
    {
        int radius = dynamiteHandler.ExplosionRadius;
        AppendPierceCross(origin, radius, destination);
        AppendPierceCross(origin + Vector2Int.up * radius, radius, destination);
        AppendPierceCross(origin + Vector2Int.down * radius, radius, destination);
        AppendPierceCross(origin + Vector2Int.left * radius, radius, destination);
        AppendPierceCross(origin + Vector2Int.right * radius, radius, destination);
    }

    private void AppendPierceCross(
        Vector2Int origin,
        int radius,
        List<Vector2Int> destination)
    {
        AddUnique(origin, destination);
        for (int i = 0; i < CardinalDirections.Length; i++)
        {
            for (int step = 1; step <= radius; step++)
            {
                Vector2Int tile = origin + CardinalDirections[i] * step;
                if (HasBlockingIndestructible(tile))
                    break;

                AddUnique(tile, destination);
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
            TryGetDangerSeconds(myTile, out float dangerSeconds)
                ? $"emergency stage14 danger:{dangerSeconds:F2}s"
                : "emergency no stage14 danger";
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
            TryGetDangerSeconds(myTile, out float dangerSeconds)
                ? $"candidate stage14 danger:{dangerSeconds:F2}s"
                : "candidate monitoring dynamites and magnets";
        return false;
    }

    private void LogDiagnostic(string key, string detail)
    {
        if (key == lastDiagnosticKey &&
            Time.time - lastDiagnosticTime < 0.5f)
        {
            return;
        }

        lastDiagnosticKey = key;
        lastDiagnosticTime = Time.time;
        Debug.LogWarning(
            $"[BattleCOMStage14][P{identity.playerId}] {key} {detail}",
            this);
    }

    private Vector2Int WorldToTile(Vector2 world)
    {
        float tileSize = movement != null
            ? Mathf.Max(0.01f, movement.tileSize)
            : 1f;
        return new Vector2Int(
            Mathf.RoundToInt(world.x / tileSize),
            Mathf.RoundToInt(world.y / tileSize));
    }

    private static Vector2Int ToTile(Vector3Int cell)
        => new(cell.x, cell.y);

    private static Vector2Int CardinalStep(
        Vector2Int from,
        Vector2Int to)
    {
        Vector2Int delta = to - from;
        return new Vector2Int(
            Mathf.Clamp(delta.x, -1, 1),
            Mathf.Clamp(delta.y, -1, 1));
    }

    private static int Manhattan(Vector2Int a, Vector2Int b)
        => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

    private static bool AppendUnique(
        List<Vector2Int> source,
        List<Vector2Int> destination)
    {
        bool added = false;
        for (int i = 0; i < source.Count; i++)
        {
            if (destination.Contains(source[i]))
                continue;

            destination.Add(source[i]);
            added = true;
        }

        return added;
    }

    private static void AddUnique(
        Vector2Int tile,
        List<Vector2Int> destination)
    {
        if (!destination.Contains(tile))
            destination.Add(tile);
    }
}

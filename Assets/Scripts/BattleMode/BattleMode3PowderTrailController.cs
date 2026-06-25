using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(GroundTileResolver))]
public sealed class BattleMode3PowderTrailController : MonoBehaviour, IGroundTileHandler, IGroundTileExplosionHitHandler
{
    [System.Serializable]
    private struct HorizontalTrailSegment
    {
        public Vector3Int startCell;
        public Vector3Int endCell;
        public bool flipExplosionY;
    }

    [System.Serializable]
    private struct VerticalTrailSegment
    {
        public Vector3Int startCell;
        public Vector3Int endCell;
        public bool flipExplosionX;
    }

    [System.Serializable]
    private struct CornerTrailCell
    {
        public Vector3Int cell;
        public int rotateRightTurns;
    }

    private enum TrailSpriteSet
    {
        Horizontal,
        Vertical,
        Corner
    }

    [Header("Tilemaps")]
    [SerializeField] private Tilemap groundTilemap;
    [SerializeField] private GroundTileResolver groundTileResolver;

    [Header("Powder Trail")]
    [SerializeField]
    private HorizontalTrailSegment[] horizontalSegments =
    {
        new() { startCell = new Vector3Int(-4, 2, 0), endCell = new Vector3Int(2, 2, 0), flipExplosionY = false },
        new() { startCell = new Vector3Int(-4, -4, 0), endCell = new Vector3Int(2, -4, 0), flipExplosionY = true },
    };

    [SerializeField]
    private VerticalTrailSegment[] verticalSegments =
    {
        new() { startCell = new Vector3Int(-5, 1, 0), endCell = new Vector3Int(-5, -3, 0), flipExplosionX = false },
        new() { startCell = new Vector3Int(3, 1, 0), endCell = new Vector3Int(3, -3, 0), flipExplosionX = true },
    };

    [SerializeField]
    private CornerTrailCell[] cornerCells =
    {
        new() { cell = new Vector3Int(-5, 2, 0), rotateRightTurns = 0 },
        new() { cell = new Vector3Int(3, 2, 0), rotateRightTurns = 1 },
        new() { cell = new Vector3Int(3, -4, 0), rotateRightTurns = 2 },
        new() { cell = new Vector3Int(-5, -4, 0), rotateRightTurns = 3 },
    };

    [SerializeField] private TileBase[] powderGroundTiles;

    [Header("Explosion Animation")]
    [SerializeField, Min(0.01f)] private float explosionDurationSeconds = 0.5f;
    [SerializeField] private Sprite[] horizontalExplosionSprites = new Sprite[10];
    [SerializeField] private Sprite[] verticalExplosionSprites = new Sprite[10];
    [SerializeField] private Sprite[] cornerExplosionSprites = new Sprite[10];

    [Header("Explosion Hitbox")]
    [SerializeField] private BombExplosion explosionPrefab;

    private readonly List<Vector3Int> trailCells = new();
    private readonly Dictionary<Vector3Int, TrailSpriteSet> spriteSetByCell = new();
    private readonly Dictionary<Vector3Int, Matrix4x4> explosionTransformByCell = new();
    private readonly HashSet<TileBase> registeredTiles = new();
    private readonly List<BombExplosion> activeHitboxes = new();
    private readonly Dictionary<Vector3Int, TileBase> originalTiles = new();
    private readonly Dictionary<Vector3Int, Matrix4x4> originalTransforms = new();
    private readonly Dictionary<Vector3Int, TileBase> stableTrailTiles = new();
    private readonly Dictionary<Vector3Int, Matrix4x4> stableTrailTransforms = new();

    private Tile[] horizontalExplosionTiles;
    private Tile[] horizontalExplosionTilesFlippedY;
    private Tile[] verticalExplosionTiles;
    private Tile[] verticalExplosionTilesFlippedX;
    private Tile[] cornerExplosionTiles;
    private Coroutine igniteRoutine;
    private bool ignitionRunning;
    private float ignitionEndsAt;

    public bool IgnitionRunning => ignitionRunning;

    public float IgnitionSecondsRemaining =>
        ignitionRunning
            ? Mathf.Max(0f, ignitionEndsAt - Time.time)
            : 0f;

    public void CopyTrailWorldPositions(List<Vector2> destination)
    {
        if (destination == null)
            return;

        destination.Clear();
        ResolveReferences();

        if (trailCells.Count == 0)
            BuildTrailCells();

        if (groundTilemap == null)
            return;

        for (int i = 0; i < trailCells.Count; i++)
        {
            Vector3 world = groundTilemap.GetCellCenterWorld(trailCells[i]);
            destination.Add(new Vector2(world.x, world.y));
        }
    }

    private void Awake()
    {
        ResolveReferences();
        BuildTrailCells();
        BuildExplosionTiles();
        RegisterResolverTiles();
    }

    private void OnEnable()
    {
        ResolveReferences();
        RegisterResolverTiles();
    }

    private void OnDisable()
    {
        StopIgnition(restoreTiles: true);
        UnregisterResolverTiles();
    }

    public bool TryModifyExplosion(
        BombController source,
        Vector2 worldPos,
        TileBase groundTile,
        ref int radius,
        ref bool pierce)
        => false;

    public void OnExplosionHit(
        BombController source,
        Vector2 worldPos,
        Vector3Int cell,
        TileBase groundTile)
    {
        if (!IsTrailCell(cell))
            return;

        Ignite(source, worldPos);
    }

    private void Ignite(BombController source, Vector2 origin)
    {
        ResolveReferences();
        BuildTrailCells();
        BuildExplosionTiles();
        RegisterResolverTiles();

        if (groundTilemap == null || trailCells.Count == 0)
            return;

        bool wasRunning = ignitionRunning;
        StopIgnition(restoreTiles: true);
        RestoreStableTrailStateIfNeeded(wasRunning);
        igniteRoutine = StartCoroutine(IgniteRoutine(source, origin));
    }

    private IEnumerator IgniteRoutine(BombController source, Vector2 origin)
    {
        ignitionRunning = true;
        CacheOriginalTiles(captureStableState: stableTrailTiles.Count == 0);
        SpawnHitboxes(source, origin);

        float duration = Mathf.Max(0.01f, explosionDurationSeconds);
        ignitionEndsAt = Time.time + duration;
        int frameCount = GetMaxExplosionFrameCount();

        if (frameCount <= 0)
        {
            yield return new WaitForSeconds(duration);
        }
        else
        {
            float elapsed = 0f;
            int lastFrame = -1;

            while (elapsed < duration)
            {
                int frame = Mathf.Clamp(Mathf.FloorToInt((elapsed / duration) * frameCount), 0, frameCount - 1);
                if (frame != lastFrame)
                {
                    ApplyExplosionTileFrame(frame);
                    lastFrame = frame;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            ApplyExplosionTileFrame(frameCount - 1);
        }

        RestoreOriginalTiles();
        activeHitboxes.Clear();
        originalTiles.Clear();
        originalTransforms.Clear();
        igniteRoutine = null;
        ignitionRunning = false;
        ignitionEndsAt = 0f;
    }

    private void StopIgnition(bool restoreTiles)
    {
        if (igniteRoutine != null)
        {
            StopCoroutine(igniteRoutine);
            igniteRoutine = null;
        }

        for (int i = 0; i < activeHitboxes.Count; i++)
        {
            if (activeHitboxes[i] != null)
                activeHitboxes[i].DestroyAfter(0f);
        }

        activeHitboxes.Clear();

        if (restoreTiles)
            RestoreOriginalTiles();

        originalTiles.Clear();
        originalTransforms.Clear();
        ignitionRunning = false;
        ignitionEndsAt = 0f;
    }

    private void CacheOriginalTiles(bool captureStableState)
    {
        originalTiles.Clear();
        originalTransforms.Clear();

        if (groundTilemap == null)
            return;

        for (int i = 0; i < trailCells.Count; i++)
        {
            Vector3Int cell = trailCells[i];
            originalTiles[cell] = groundTilemap.GetTile(cell);
            originalTransforms[cell] = groundTilemap.GetTransformMatrix(cell);

            if (captureStableState)
            {
                stableTrailTiles[cell] = originalTiles[cell];
                stableTrailTransforms[cell] = originalTransforms[cell];
            }
        }
    }

    private void RestoreStableTrailStateIfNeeded(bool wasRunning)
    {
        if (!wasRunning)
            return;

        if (groundTilemap == null || stableTrailTiles.Count == 0)
            return;

        foreach (var entry in stableTrailTiles)
        {
            groundTilemap.SetTile(entry.Key, entry.Value);

            if (stableTrailTransforms.TryGetValue(entry.Key, out Matrix4x4 matrix))
                groundTilemap.SetTransformMatrix(entry.Key, matrix);
        }

        groundTilemap.RefreshAllTiles();
    }

    private void RestoreOriginalTiles()
    {
        if (groundTilemap == null || originalTiles.Count == 0)
            return;

        foreach (var entry in originalTiles)
        {
            groundTilemap.SetTile(entry.Key, entry.Value);

            if (originalTransforms.TryGetValue(entry.Key, out Matrix4x4 matrix))
                groundTilemap.SetTransformMatrix(entry.Key, matrix);
        }

        groundTilemap.RefreshAllTiles();
    }

    private void ApplyExplosionTileFrame(int frame)
    {
        if (groundTilemap == null)
            return;

        for (int i = 0; i < trailCells.Count; i++)
        {
            Vector3Int cell = trailCells[i];
            Tile tile = GetTileForCell(cell, frame);

            if (tile != null)
            {
                groundTilemap.SetTile(cell, tile);
                Matrix4x4 matrix = explosionTransformByCell.TryGetValue(cell, out Matrix4x4 configured)
                    ? configured
                    : Matrix4x4.identity;

                groundTilemap.SetTransformMatrix(cell, matrix);
            }
        }

        groundTilemap.RefreshAllTiles();
    }

    private void SpawnHitboxes(BombController source, Vector2 origin)
    {
        ResolveExplosionPrefab();

        if (groundTilemap == null || explosionPrefab == null)
            return;

        float duration = Mathf.Max(0.01f, explosionDurationSeconds);

        for (int i = 0; i < trailCells.Count; i++)
        {
            Vector3 pos = groundTilemap.GetCellCenterWorld(trailCells[i]);
            pos.z = 0f;

            BombExplosion hitbox = BombExplosion.Spawn(explosionPrefab, pos, Quaternion.identity);
            if (hitbox == null)
                continue;

            int ownerPlayerId = source != null ? source.PlayerId : 0;
            hitbox.SetSource(source, ownerPlayerId, false);
            hitbox.PlayDamageOnly(duration, origin);
            activeHitboxes.Add(hitbox);
        }
    }

    private void BuildTrailCells()
    {
        trailCells.Clear();
        spriteSetByCell.Clear();
        explosionTransformByCell.Clear();

        for (int i = 0; horizontalSegments != null && i < horizontalSegments.Length; i++)
        {
            HorizontalTrailSegment segment = horizontalSegments[i];
            int y = segment.startCell.y;
            int minX = Mathf.Min(segment.startCell.x, segment.endCell.x);
            int maxX = Mathf.Max(segment.startCell.x, segment.endCell.x);

            for (int x = minX; x <= maxX; x++)
            {
                Vector3Int cell = new(x, y, segment.startCell.z);
                trailCells.Add(cell);
                spriteSetByCell[cell] = TrailSpriteSet.Horizontal;
                explosionTransformByCell[cell] = segment.flipExplosionY ? GetFlipYMatrix() : Matrix4x4.identity;
            }
        }

        for (int i = 0; verticalSegments != null && i < verticalSegments.Length; i++)
        {
            VerticalTrailSegment segment = verticalSegments[i];
            int x = segment.startCell.x;
            int minY = Mathf.Min(segment.startCell.y, segment.endCell.y);
            int maxY = Mathf.Max(segment.startCell.y, segment.endCell.y);

            for (int y = minY; y <= maxY; y++)
            {
                Vector3Int cell = new(x, y, segment.startCell.z);
                trailCells.Add(cell);
                spriteSetByCell[cell] = TrailSpriteSet.Vertical;
                explosionTransformByCell[cell] = segment.flipExplosionX ? GetFlipXMatrix() : Matrix4x4.identity;
            }
        }

        for (int i = 0; cornerCells != null && i < cornerCells.Length; i++)
        {
            CornerTrailCell corner = cornerCells[i];
            trailCells.Add(corner.cell);
            spriteSetByCell[corner.cell] = TrailSpriteSet.Corner;
            explosionTransformByCell[corner.cell] = GetRotateRightMatrix(corner.rotateRightTurns);
        }
    }

    private void BuildExplosionTiles()
    {
        if (horizontalExplosionSprites == null || horizontalExplosionSprites.Length == 0)
        {
            horizontalExplosionTiles = null;
            horizontalExplosionTilesFlippedY = null;
        }
        else if (horizontalExplosionTiles == null
            || horizontalExplosionTiles.Length != horizontalExplosionSprites.Length
            || horizontalExplosionTilesFlippedY == null
            || horizontalExplosionTilesFlippedY.Length != horizontalExplosionSprites.Length)
        {
            horizontalExplosionTiles = BuildTileArray(horizontalExplosionSprites, "BattleMode3PowderTrailHorizontalExplosion");
            horizontalExplosionTilesFlippedY = BuildTileArray(horizontalExplosionSprites, "BattleMode3PowderTrailHorizontalExplosionLower");
        }

        if (verticalExplosionSprites == null || verticalExplosionSprites.Length == 0)
        {
            verticalExplosionTiles = null;
            verticalExplosionTilesFlippedX = null;
        }
        else if (verticalExplosionTiles == null
            || verticalExplosionTiles.Length != verticalExplosionSprites.Length
            || verticalExplosionTilesFlippedX == null
            || verticalExplosionTilesFlippedX.Length != verticalExplosionSprites.Length)
        {
            verticalExplosionTiles = BuildTileArray(verticalExplosionSprites, "BattleMode3PowderTrailVerticalExplosion");
            verticalExplosionTilesFlippedX = BuildTileArray(verticalExplosionSprites, "BattleMode3PowderTrailVerticalExplosionRight");
        }

        if (cornerExplosionSprites == null || cornerExplosionSprites.Length == 0)
        {
            cornerExplosionTiles = null;
        }
        else if (cornerExplosionTiles == null || cornerExplosionTiles.Length != cornerExplosionSprites.Length)
        {
            cornerExplosionTiles = BuildTileArray(cornerExplosionSprites, "BattleMode3PowderTrailCornerExplosion");
        }
    }

    private static Tile[] BuildTileArray(Sprite[] sprites, string namePrefix)
    {
        Tile[] tiles = new Tile[sprites.Length];

        for (int i = 0; i < sprites.Length; i++)
        {
            Sprite sprite = sprites[i];
            if (sprite == null)
                continue;

            Tile tile = ScriptableObject.CreateInstance<Tile>();
            tile.name = $"{namePrefix}_{i + 1:00}";
            tile.sprite = sprite;
            tile.colliderType = Tile.ColliderType.None;
            tile.flags = TileFlags.None;
            tiles[i] = tile;
        }

        return tiles;
    }

    private int GetMaxExplosionFrameCount()
    {
        int count = 0;

        if (horizontalExplosionTiles != null)
            count = Mathf.Max(count, horizontalExplosionTiles.Length);

        if (verticalExplosionTiles != null)
            count = Mathf.Max(count, verticalExplosionTiles.Length);

        if (cornerExplosionTiles != null)
            count = Mathf.Max(count, cornerExplosionTiles.Length);

        return count;
    }

    private static Matrix4x4 GetFlipYMatrix()
    {
        return Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(1f, -1f, 1f));
    }

    private static Matrix4x4 GetFlipXMatrix()
    {
        return Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(-1f, 1f, 1f));
    }

    private static Matrix4x4 GetRotateRightMatrix(int turns)
    {
        int normalizedTurns = ((turns % 4) + 4) % 4;
        float angle = normalizedTurns * -90f;
        return Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, 0f, angle), Vector3.one);
    }

    private Tile GetTileForCell(Vector3Int cell, int frame)
    {
        TrailSpriteSet spriteSet = spriteSetByCell.TryGetValue(cell, out TrailSpriteSet configured)
            ? configured
            : TrailSpriteSet.Horizontal;

        bool flipped = explosionTransformByCell.TryGetValue(cell, out Matrix4x4 matrix) && matrix != Matrix4x4.identity;

        return spriteSet switch
        {
            TrailSpriteSet.Vertical => flipped
                ? GetTileAt(verticalExplosionTilesFlippedX, frame)
                : GetTileAt(verticalExplosionTiles, frame),
            TrailSpriteSet.Corner => GetTileAt(cornerExplosionTiles, frame),
            _ => flipped
                ? GetTileAt(horizontalExplosionTilesFlippedY, frame)
                : GetTileAt(horizontalExplosionTiles, frame),
        };
    }

    private static Tile GetTileAt(Tile[] tiles, int frame)
    {
        if (tiles == null || tiles.Length == 0)
            return null;

        return tiles[Mathf.Clamp(frame, 0, tiles.Length - 1)];
    }

    private void RegisterResolverTiles()
    {
        ResolveReferences();

        if (groundTileResolver == null)
            return;

        RegisterTiles(powderGroundTiles);
        RegisterTiles(horizontalExplosionTiles);
        RegisterTiles(horizontalExplosionTilesFlippedY);
        RegisterTiles(verticalExplosionTiles);
        RegisterTiles(verticalExplosionTilesFlippedX);
        RegisterTiles(cornerExplosionTiles);
    }

    private void RegisterTiles(IEnumerable<TileBase> tiles)
    {
        if (tiles == null)
            return;

        foreach (TileBase tile in tiles)
        {
            if (tile == null || registeredTiles.Contains(tile))
                continue;

            groundTileResolver.RegisterRuntimeHandler(tile, this);
            registeredTiles.Add(tile);
        }
    }

    private void UnregisterResolverTiles()
    {
        if (groundTileResolver == null)
            return;

        foreach (TileBase tile in registeredTiles)
            groundTileResolver.UnregisterRuntimeHandler(tile, this);

        registeredTiles.Clear();
    }

    private bool IsTrailCell(Vector3Int cell)
    {
        for (int i = 0; i < trailCells.Count; i++)
        {
            if (trailCells[i] == cell)
                return true;
        }

        return false;
    }

    private bool IsRegisteredPowderOrExplosionTile(TileBase tile)
    {
        if (tile == null)
            return false;

        if (registeredTiles.Contains(tile))
            return true;

        for (int i = 0; powderGroundTiles != null && i < powderGroundTiles.Length; i++)
        {
            if (powderGroundTiles[i] == tile)
                return true;
        }

        for (int i = 0; horizontalExplosionTiles != null && i < horizontalExplosionTiles.Length; i++)
        {
            if (horizontalExplosionTiles[i] == tile)
                return true;
        }

        for (int i = 0; horizontalExplosionTilesFlippedY != null && i < horizontalExplosionTilesFlippedY.Length; i++)
        {
            if (horizontalExplosionTilesFlippedY[i] == tile)
                return true;
        }

        for (int i = 0; verticalExplosionTiles != null && i < verticalExplosionTiles.Length; i++)
        {
            if (verticalExplosionTiles[i] == tile)
                return true;
        }

        for (int i = 0; verticalExplosionTilesFlippedX != null && i < verticalExplosionTilesFlippedX.Length; i++)
        {
            if (verticalExplosionTilesFlippedX[i] == tile)
                return true;
        }

        for (int i = 0; cornerExplosionTiles != null && i < cornerExplosionTiles.Length; i++)
        {
            if (cornerExplosionTiles[i] == tile)
                return true;
        }

        return false;
    }

    private void ResolveReferences()
    {
        if (groundTileResolver == null)
            groundTileResolver = GetComponent<GroundTileResolver>();

        if (groundTilemap == null)
        {
            var gm = FindAnyObjectByType<GameManager>();
            if (gm != null)
                groundTilemap = gm.groundTilemap;
        }

        if (groundTilemap == null)
        {
            var tilemaps = FindObjectsByType<Tilemap>();
            for (int i = 0; i < tilemaps.Length; i++)
            {
                Tilemap tm = tilemaps[i];
                if (tm != null && tm.name.ToLowerInvariant().Contains("ground"))
                {
                    groundTilemap = tm;
                    break;
                }
            }
        }
    }

    private void ResolveExplosionPrefab()
    {
        if (explosionPrefab != null)
            return;

        explosionPrefab = Resources.Load<BombExplosion>("Explosions/BombExplosion");
    }
}

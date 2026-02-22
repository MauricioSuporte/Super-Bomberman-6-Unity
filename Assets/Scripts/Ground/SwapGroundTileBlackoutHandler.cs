using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class SwapGroundTileBlackoutHandler : MonoBehaviour, IGroundTileExplosionHitHandler, IGroundTileHandler
{
    [Header("Swap Tiles (Ground Tilemap)")]
    [SerializeField] private TileBase tileA;
    [SerializeField] private TileBase tileB;

    [Header("Behavior")]
    [SerializeField] private bool onlyIfMatchesAorB = true;

    [Header("Tilemap Override")]
    [SerializeField] private Tilemap groundTilemapOverride;

    [Header("Initial Sync")]
    [SerializeField] private bool syncBlackoutOnStart = true;

    private void Start()
    {
        if (!syncBlackoutOnStart)
            return;

        var tm = ResolveTilemapEarly();
        if (tm == null || tileA == null || tileB == null)
            return;

        tm.CompressBounds();
        var b = tm.cellBounds;

        bool anyA = false;
        bool anyB = false;

        for (int y = b.yMin; y < b.yMax; y++)
        {
            for (int x = b.xMin; x < b.xMax; x++)
            {
                var c = new Vector3Int(x, y, 0);
                var t = tm.GetTile(c);
                if (t == tileA) anyA = true;
                else if (t == tileB) anyB = true;

                if (anyA && anyB)
                    break;
            }

            if (anyA && anyB)
                break;
        }

        if (anyA)
            StageBlackout.Instance?.SetBlackoutActive(true);
        else if (anyB)
            StageBlackout.Instance?.SetBlackoutActive(false);
    }

    private Tilemap ResolveTilemapEarly()
    {
        if (groundTilemapOverride != null)
            return groundTilemapOverride;

        var tms = FindObjectsByType<Tilemap>(FindObjectsSortMode.None);
        for (int i = 0; i < tms.Length; i++)
        {
            var tm = tms[i];
            if (tm == null) continue;

            string n = tm.name.ToLowerInvariant();
            if (n.Contains("ground"))
            {
                groundTilemapOverride = tm;
                return tm;
            }
        }

        return null;
    }

    public void OnExplosionHit(
        BombController source,
        Vector2 worldPos,
        Vector3Int cell,
        TileBase groundTile)
    {
        if (source == null)
            return;

        if (tileA == null || tileB == null)
            return;

        Tilemap tm =
            groundTilemapOverride != null ? groundTilemapOverride :
            (source.groundTiles != null ? source.groundTiles : source.stageBoundsTiles);

        if (tm == null)
            return;

        TileBase next;

        if (groundTile == tileA)
            next = tileB;
        else if (groundTile == tileB)
            next = tileA;
        else
        {
            if (onlyIfMatchesAorB)
                return;

            return;
        }

        tm.SetTile(cell, next);
        tm.RefreshTile(cell);

        bool blackoutOn = next == tileA;
        StageBlackout.Instance?.SetBlackoutActive(blackoutOn);
    }

    public bool TryModifyExplosion(
        BombController source,
        Vector2 worldPos,
        TileBase groundTile,
        ref int radius,
        ref bool pierce)
    {
        return false;
    }
}
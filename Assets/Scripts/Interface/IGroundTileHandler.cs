using UnityEngine;
using UnityEngine.Tilemaps;

public interface IGroundTileHandler
{
    bool TryModifyExplosion(
        BombController source,
        Vector2 worldPos,
        TileBase groundTile,
        ref int radius,
        ref bool pierce);
}

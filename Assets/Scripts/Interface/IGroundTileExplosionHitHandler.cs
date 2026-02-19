using UnityEngine;
using UnityEngine.Tilemaps;

public interface IGroundTileExplosionHitHandler
{
    void OnExplosionHit(
        BombController source,
        Vector2 worldPos,
        Vector3Int cell,
        TileBase groundTile);
}

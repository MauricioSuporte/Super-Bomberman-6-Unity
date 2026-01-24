using UnityEngine;
using UnityEngine.Tilemaps;

public interface IIndestructibleTileHandler
{
    bool HandleExplosionHit(BombController source, Vector2 hitWorldPos, Vector3Int cell, TileBase tile);
}

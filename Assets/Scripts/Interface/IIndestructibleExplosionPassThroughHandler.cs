using UnityEngine;
using UnityEngine.Tilemaps;

public interface IIndestructibleExplosionPassThroughHandler : IIndestructibleTileHandler
{
    bool AllowsExplosionPassThrough(BombController source, Vector2 hitWorldPos, Vector3Int cell, TileBase tile);
}

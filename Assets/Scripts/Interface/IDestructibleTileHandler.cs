using UnityEngine;

public interface IDestructibleTileHandler
{
    bool HandleHit(BombController source, Vector2 worldPos, Vector3Int cell);
}

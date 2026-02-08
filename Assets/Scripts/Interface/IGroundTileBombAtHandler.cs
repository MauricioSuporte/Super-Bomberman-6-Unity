using UnityEngine;
using UnityEngine.Tilemaps;

public interface IGroundTileBombAtHandler
{
    void OnBombAt(
        BombController source,
        Vector2 worldPos,
        Vector3Int cell,
        TileBase groundTile,
        GameObject bombGo);
}

using UnityEngine;
using UnityEngine.Tilemaps;

public interface IGroundTileBombPlacedHandler
{
    void OnBombPlaced(
        BombController source,
        Vector2 worldPos,
        Vector3Int cell,
        TileBase groundTile,
        GameObject bombGo);
}

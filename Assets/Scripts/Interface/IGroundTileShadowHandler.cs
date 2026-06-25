using UnityEngine;
using UnityEngine.Tilemaps;

public interface IGroundTileShadowHandler
{
    bool TryGetShadowTile(
        Vector3Int cell,
        TileBase currentGroundTile,
        TileBase defaultShadowTile,
        out TileBase shadowTile);

    bool TryGetRestoredTile(
        Vector3Int cell,
        TileBase currentGroundTile,
        TileBase defaultGroundTile,
        out TileBase restoredTile);
}

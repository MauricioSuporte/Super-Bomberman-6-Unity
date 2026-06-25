using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class PassThroughIndestructibleTileHandler : MonoBehaviour, IIndestructibleExplosionPassThroughHandler
{
    [SerializeField] private TileBase[] passThroughTiles;

    public bool HandleExplosionHit(BombController source, Vector2 hitWorldPos, Vector3Int cell, TileBase tile)
    {
        return AllowsExplosionPassThrough(source, hitWorldPos, cell, tile);
    }

    public bool AllowsExplosionPassThrough(BombController source, Vector2 hitWorldPos, Vector3Int cell, TileBase tile)
    {
        if (tile == null || passThroughTiles == null)
            return false;

        for (int i = 0; i < passThroughTiles.Length; i++)
        {
            if (passThroughTiles[i] == tile)
                return true;
        }

        return false;
    }
}

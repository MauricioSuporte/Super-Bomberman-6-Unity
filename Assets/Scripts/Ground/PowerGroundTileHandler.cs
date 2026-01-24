using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class PowerGroundTileHandler : MonoBehaviour, IGroundTileHandler
{
    [SerializeField] private int boostedRadius = 10;

    public bool TryModifyExplosion(
        BombController source,
        Vector2 worldPos,
        TileBase groundTile,
        ref int radius,
        ref bool pierce)
    {
        if (radius < boostedRadius)
            radius = boostedRadius;

        return true;
    }
}

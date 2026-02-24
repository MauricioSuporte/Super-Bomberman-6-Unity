using UnityEngine;
using UnityEngine.Tilemaps;

public interface IMagnetPullable
{
    bool IsBeingMagnetPulled { get; }
    bool CanBeMagnetPulled { get; }

    bool StartMagnetPull(
        Vector2 directionToMagnet,
        float tileSize,
        int steps,
        LayerMask obstacleMask,
        Tilemap destructibleTilemap,
        float speedMultiplier
    );
}
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// A junction-turning enemy that leaves a destructible block behind whenever
/// its navigation chooses a new direction.
/// </summary>
public sealed class SandpileMovementController : JunctionTurningEnemyMovementController
{
    [Header("Turn Drop")]
    [SerializeField] private Tilemap destructiblesTilemap;
    [SerializeField] private Tilemap indestructiblesTilemap;
    [SerializeField] private TileBase destructibleTile;

    private bool navigationStarted;

    protected override void Start()
    {
        ResolveTilemaps();
        base.Start();
        navigationStarted = true;
    }

    protected override void DecideNextTile()
    {
        Vector2 previousDirection = direction;
        base.DecideNextTile();

        if (navigationStarted && direction != previousDirection)
            DropDestructibleTileAt(rb.position);
    }

    private void DropDestructibleTileAt(Vector2 worldPosition)
    {
        if (destructiblesTilemap == null || destructibleTile == null)
            return;

        Vector3Int destructibleCell = destructiblesTilemap.WorldToCell(worldPosition);

        if (indestructiblesTilemap != null)
        {
            Vector3Int indestructibleCell = indestructiblesTilemap.WorldToCell(worldPosition);
            if (indestructiblesTilemap.GetTile(indestructibleCell) != null)
                return;
        }

        if (destructiblesTilemap.GetTile(destructibleCell) != null)
            return;

        destructiblesTilemap.SetTile(destructibleCell, destructibleTile);
    }

    private void ResolveTilemaps()
    {
        if (destructiblesTilemap != null && indestructiblesTilemap != null)
            return;

        foreach (Tilemap tilemap in FindObjectsByType<Tilemap>())
        {
            if (tilemap.name == "Destructibles")
                destructiblesTilemap = tilemap;
            else if (tilemap.name == "Indestructibles")
                indestructiblesTilemap = tilemap;
        }
    }
}

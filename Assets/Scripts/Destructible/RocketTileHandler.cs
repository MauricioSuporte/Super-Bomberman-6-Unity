using System.Collections.Generic;
using StageAssets;
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class RocketTileHandler : MonoBehaviour, IDestructibleTileHandler
{
    [SerializeField] private DestructibleRocketController flightPrefab;

    public bool HandleHit(BombController source, Vector2 worldPos, Vector3Int cell)
    {
        if (source == null || flightPrefab == null || source.destructibleTiles == null)
            return false;
        if (!source.destructibleTiles.HasTile(cell))
            return true;

        Tilemap ground = source.groundTiles;
        Collider2D room = World3RoomProgressionController.FindRoomBoundsContaining(worldPos);
        if (ground == null || room == null)
            return false;

        var targets = new List<Vector3>();
        Tilemap walls = GameManager.Instance != null ? GameManager.Instance.indestructibleTilemap : null;
        foreach (Vector3Int targetCell in ground.cellBounds.allPositionsWithin)
        {
            if (!ground.HasTile(targetCell))
                continue;
            Vector3 center = ground.GetCellCenterWorld(targetCell);
            if (room.OverlapPoint(center) && (walls == null || !walls.HasTile(walls.WorldToCell(center))))
                targets.Add(center);
        }
        if (targets.Count == 0)
            return false;

        Vector3 origin = source.destructibleTiles.GetCellCenterWorld(cell);
        float tileHeight = Vector3.Distance(ground.GetCellCenterWorld(Vector3Int.zero),
            ground.GetCellCenterWorld(Vector3Int.up));
        source.ClearDestructibleForEffect(worldPos, spawnDestructiblePrefab: false);
        DestructibleRocketController rocket = Instantiate(flightPrefab, origin, Quaternion.identity, transform);
        rocket.Launch(targets[Random.Range(0, targets.Count)], tileHeight, room);
        return true;
    }
}

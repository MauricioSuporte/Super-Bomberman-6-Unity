using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// Encounter-local grid queries shared by the boss and its grounded attacks.
public sealed class FreezerVenusArena
{
    private static readonly Vector3Int[] Directions =
        { Vector3Int.down, Vector3Int.left, Vector3Int.right, Vector3Int.up };
    private readonly GameManager manager;
    public Tilemap Ground => manager != null ? manager.groundTilemap : null;

    public FreezerVenusArena(GameManager manager) => this.manager = manager;

    public Vector3Int Cell(Vector3 position) => Ground != null ? Ground.WorldToCell(position) :
        new Vector3Int(Mathf.RoundToInt(position.x), Mathf.RoundToInt(position.y), 0);

    public Vector3 Center(Vector3Int cell) => Ground != null ? Ground.GetCellCenterWorld(cell) : (Vector3)cell;

    public bool IsFloor(Vector3Int cell) => Ground != null && Ground.HasTile(cell);

    public bool IsStaticWalkable(Vector3Int cell)
    {
        if (!IsFloor(cell)) return false;
        Vector3 world = Center(cell);
        if (manager.indestructibleTilemap != null &&
            manager.indestructibleTilemap.HasTile(manager.indestructibleTilemap.WorldToCell(world))) return false;
        return manager.destructibleTilemap == null ||
            !manager.destructibleTilemap.HasTile(manager.destructibleTilemap.WorldToCell(world));
    }

    public bool IsWalkable(Vector3Int cell)
    {
        if (!IsStaticWalkable(cell)) return false;
        foreach (var bomb in Bomb.ActiveBombs)
            if (bomb != null && !bomb.HasExploded && !bomb.IsBeingHeldByPowerGlove && Cell(bomb.transform.position) == cell)
                return false;
        return true;
    }

    public Vector3Int NearestFloor(Vector3 position)
    {
        Vector3Int best = Cell(position);
        if (IsStaticWalkable(best)) return best;
        float distance = float.PositiveInfinity;
        if (Ground == null) return best;
        foreach (var cell in Ground.cellBounds.allPositionsWithin)
        {
            if (!IsStaticWalkable(cell)) continue;
            float candidate = (Center(cell) - position).sqrMagnitude;
            if (candidate >= distance) continue;
            best = cell;
            distance = candidate;
        }
        return best;
    }

    public bool TryNextStep(Vector3Int start, Vector3Int destination, out Vector3Int next)
    {
        next = start;
        if (Ground == null || start == destination) return false;
        var queue = new Queue<Vector3Int>();
        var previous = new Dictionary<Vector3Int, Vector3Int> { [start] = start };
        queue.Enqueue(start);
        Vector3Int closest = start;
        int closestDistance = Manhattan(start, destination);
        while (queue.Count > 0)
        {
            Vector3Int current = queue.Dequeue();
            int distance = Manhattan(current, destination);
            if (distance < closestDistance) { closest = current; closestDistance = distance; }
            if (current == destination) { closest = current; break; }
            foreach (var direction in Directions)
            {
                Vector3Int candidate = current + direction;
                if (previous.ContainsKey(candidate) || !IsWalkable(candidate)) continue;
                previous.Add(candidate, current);
                queue.Enqueue(candidate);
            }
        }
        if (closest == start) return false;
        next = closest;
        while (previous[next] != start) next = previous[next];
        return true;
    }

    public List<Vector3Int> DollSpawnCells(Vector3 origin)
    {
        var result = new List<Vector3Int>();
        if (Ground == null) return result;
        BoundsInt bounds = Ground.cellBounds;
        int spawnY = Mathf.Clamp(Cell(origin).y, bounds.yMin, bounds.yMax - 1);
        for (int x = bounds.xMin; x < bounds.xMax; x++)
        {
            bool corridor = true;
            for (int y = bounds.yMin; y < bounds.yMax; y++)
                if (!IsStaticWalkable(new Vector3Int(x, y, 0))) { corridor = false; break; }
            // The terminal wall below ground.yMin is intentionally outside this test.
            if (corridor) result.Add(new Vector3Int(x, spawnY, 0));
        }
        result.Sort((a, b) =>
        {
            int distance = Mathf.Abs(Center(a).x - origin.x).CompareTo(Mathf.Abs(Center(b).x - origin.x));
            return distance != 0 ? distance : a.x.CompareTo(b.x);
        });
        if (result.Count > 4) result.RemoveRange(4, result.Count - 4);
        return result;
    }

    private static int Manhattan(Vector3Int a, Vector3Int b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

    public static Vector3 Snap(Vector3 position)
        => new(Mathf.Round(position.x * 16f) / 16f, Mathf.Round(position.y * 16f) / 16f, position.z);
}

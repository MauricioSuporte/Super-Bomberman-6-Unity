#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class FreezerVenusEditModeTests
{
    private GameObject root;
    private Tile tile;
    private Tilemap ground;
    private Tilemap walls;
    private FreezerVenusArena arena;

    [SetUp]
    public void SetUp()
    {
        // Keep gameplay bootstraps inactive while exercising the real grid queries.
        root = new GameObject("FreezerVenus arena test");
        root.AddComponent<Grid>();
        var managerObject = new GameObject("Inactive manager");
        managerObject.transform.SetParent(root.transform, false);
        managerObject.SetActive(false);
        var manager = managerObject.AddComponent<GameManager>();
        var groundObject = new GameObject("Ground");
        groundObject.transform.SetParent(root.transform, false);
        ground = groundObject.AddComponent<Tilemap>();
        var wallObject = new GameObject("Indestructibles");
        wallObject.transform.SetParent(root.transform, false);
        walls = wallObject.AddComponent<Tilemap>();
        manager.groundTilemap = ground;
        manager.indestructibleTilemap = walls;
        tile = ScriptableObject.CreateInstance<Tile>();
        arena = new FreezerVenusArena(manager);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(root);
        Object.DestroyImmediate(tile);
    }

    [Test]
    public void Tornado_RoutesAroundPillarsUsingAdjacentFloorCells()
    {
        FillGround(-2, 2, -2, 2);
        walls.SetTile(Vector3Int.zero, tile);
        Vector3Int current = new(-1, 0, 0);
        Vector3Int destination = new(1, 0, 0);
        int steps = 0;
        while (current != destination && steps < 12)
        {
            Assert.IsTrue(arena.TryNextStep(current, destination, out var next));
            Assert.AreEqual(1, Mathf.Abs(next.x - current.x) + Mathf.Abs(next.y - current.y));
            Assert.IsTrue(arena.IsFloor(next));
            Assert.IsFalse(walls.HasTile(next));
            current = next;
            steps++;
        }
        Assert.AreEqual(destination, current);
        Assert.AreEqual(4, steps);
    }

    [Test]
    public void Tornado_DoesNotLeaveGroundToBypassASealedCorridor()
    {
        FillGround(-1, 1, 0, 0);
        walls.SetTile(Vector3Int.zero, tile);
        Assert.IsFalse(arena.TryNextStep(new Vector3Int(-1, 0, 0), new Vector3Int(1, 0, 0), out _));
    }

    [Test]
    public void Dolls_SelectFourNearestFullyOpenColumnsAndAllowTheTerminalWall()
    {
        FillGround(-7, 5, -6, 4);
        for (int x = -6; x <= 4; x += 2)
            walls.SetTile(new Vector3Int(x, -3, 0), tile);
        for (int x = -7; x <= 5; x++)
            walls.SetTile(new Vector3Int(x, -7, 0), tile);
        List<Vector3Int> cells = arena.DollSpawnCells(arena.Center(new Vector3Int(-1, 2, 0)));
        Assert.AreEqual(4, cells.Count);
        CollectionAssert.AreEquivalent(new[] { -1, -3, 1, -5 }, cells.ConvertAll(c => c.x));
        foreach (var cell in cells)
        {
            Assert.AreEqual(2, cell.y);
            for (int y = -6; y <= 4; y++)
                Assert.IsTrue(arena.IsStaticWalkable(new Vector3Int(cell.x, y, 0)));
        }
    }

    [Test]
    public void Dolls_RejectAColumnWithAnObstacleAboveTheSpawnPoint()
    {
        FillGround(-7, 5, -6, 4);
        walls.SetTile(new Vector3Int(-1, 4, 0), tile);
        var cells = arena.DollSpawnCells(arena.Center(new Vector3Int(-1, -4, 0)));
        Assert.AreEqual(4, cells.Count);
        Assert.IsFalse(cells.Exists(c => c.x == -1));
    }

    private void FillGround(int left, int right, int bottom, int top)
    {
        for (int y = bottom; y <= top; y++)
            for (int x = left; x <= right; x++)
                ground.SetTile(new Vector3Int(x, y, 0), tile);
    }
}
#endif

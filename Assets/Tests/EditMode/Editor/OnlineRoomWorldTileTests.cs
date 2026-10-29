using System;
using System.Collections.Generic;
using System.Reflection;
using Assets.Scripts.Netcode;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

public sealed partial class OnlineRoomEditModeTests
{
    [Test]
    public void RuntimeConveyorTilesKeepTheirCatalogIdentityAndApprovedCollision()
    {
        using var fixture = new ConveyorWorldFixture();
        var variant = fixture.map.GetTile(Vector3Int.zero);
        Assert.IsTrue(fixture.conveyor.TryResolveNetworkTileOrigin(fixture.map, variant, out var source));
        Assert.AreSame(fixture.source, source);
        Assert.IsFalse(fixture.conveyor.TryResolveNetworkTileOrigin(null, variant, out _));
        var read = typeof(BombermanNetworkManager).GetMethod("ReadCoopTileFrame", ConveyorWorldFixture.Flags);
        var frame = (CoopTileFrame)read.Invoke(fixture.world.manager, new object[] { fixture.map, Vector3Int.zero });
        Assert.AreEqual(7, frame.tile, "A generated conveyor variant must not become an empty cell on observers.");
        Assert.AreEqual((byte)fixture.map.GetColliderType(Vector3Int.zero), frame.collisionType);
        var ids = (Dictionary<TileBase, int>)typeof(BombermanNetworkManager).GetField("coopTileIds", ConveyorWorldFixture.Flags).GetValue(fixture.world.manager);
        Assert.AreEqual(7, ids[variant]);
    }

    [Test]
    public void CatalogTileRefreshPreservesTheConveyorsApprovedPhaseWithoutOfflineMutation()
    {
        using var fixture = new ConveyorWorldFixture();
        fixture.conveyor.ApplyNetworkPhase(new ConveyorPhase { scene = fixture.conveyor.gameObject.scene.name,
            path = fixture.conveyor.NetworkPath, revision = 1, clockwise = false, fast = true });
        var approved = fixture.map.GetTile(Vector3Int.zero);
        Assert.AreNotSame(fixture.source, approved);
        var color = new Color(.2f, .4f, .6f, .8f);
        var matrix = Matrix4x4.TRS(new Vector3(.25f, .5f), Quaternion.Euler(0, 0, 90), Vector3.one * .5f);
        typeof(BombermanNetworkManager).GetMethod("ApplyCoopTileCells", ConveyorWorldFixture.Flags).Invoke(fixture.world.manager,
            new object[] { fixture.map, new[] { new CoopTileFrame { cell = Vector3Int.zero, tile = 7, color = color, matrix = matrix } } });
        Assert.AreSame(approved, fixture.map.GetTile(Vector3Int.zero));
        Assert.AreEqual(color, fixture.map.GetColor(Vector3Int.zero));
        Assert.AreEqual(matrix, fixture.map.GetTransformMatrix(Vector3Int.zero));
        fixture.map.SetTile(Vector3Int.zero, fixture.source);
        var previous = NetSync.Mode;
        try
        {
            NetSync.Mode = NetSync.NetMode.Offline;
            fixture.conveyor.RefreshNetworkTileVisuals(fixture.map);
            Assert.AreSame(fixture.source, fixture.map.GetTile(Vector3Int.zero));
        }
        finally { NetSync.Mode = previous; }
    }

    sealed class ConveyorWorldFixture : IDisposable
    {
        public const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        public readonly WorldBombFixture world = new(false);
        readonly GameObject root, grid;
        readonly Sprite sprite;
        readonly Texture2D texture;
        readonly CoopSceneCatalog catalog;
        public readonly Tilemap map;
        public readonly BattleMode5ConveyorController conveyor;
        public readonly AnimatedTile source;
        public ConveyorWorldFixture()
        {
            root = new GameObject("ApprovedWorldConveyor"); root.SetActive(false);
            grid = new GameObject("ApprovedConveyorGrid", typeof(Grid));
            var child = new GameObject("Ground", typeof(Tilemap)); child.transform.SetParent(grid.transform, false);
            map = child.GetComponent<Tilemap>(); conveyor = root.AddComponent<BattleMode5ConveyorController>();
            typeof(BattleMode5ConveyorController).GetField("groundTilemap", Flags).SetValue(conveyor, map);
            ((HashSet<Vector3Int>)typeof(BattleMode5ConveyorController).GetField("conveyorCells", Flags).GetValue(conveyor)).Add(Vector3Int.zero);
            texture = new Texture2D(2, 2); sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * .5f);
            source = ScriptableObject.CreateInstance<AnimatedTile>();
            source.m_AnimatedSprites = new[] { sprite }; source.m_MinSpeed = source.m_MaxSpeed = 1;
            source.m_TileColliderType = Tile.ColliderType.Grid;
            map.SetTile(Vector3Int.zero, source);
            conveyor.RefreshNetworkTileVisuals(map);
            typeof(BombermanNetworkManager).GetField("coopConveyorSources", Flags).SetValue(world.manager, new[] { conveyor });
            ((Dictionary<TileBase, int>)typeof(BombermanNetworkManager).GetField("coopTileIds", Flags).GetValue(world.manager))[source] = 7;
            catalog = ScriptableObject.CreateInstance<CoopSceneCatalog>(); catalog.tiles = new TileBase[8]; catalog.tiles[7] = source;
            typeof(BombermanNetworkManager).GetField("coopCatalog", Flags).SetValue(world.manager, catalog);
        }
        public void Dispose()
        {
            var variants = new List<AnimatedTile>(((Dictionary<AnimatedTile, AnimatedTile>)typeof(BattleMode5ConveyorController)
                .GetField("conveyorRuntimeTileOrigins", Flags).GetValue(conveyor)).Keys);
            Object.DestroyImmediate(grid); Object.DestroyImmediate(root);
            foreach (var tile in variants) if (tile != null) Object.DestroyImmediate(tile);
            Object.DestroyImmediate(source); Object.DestroyImmediate(sprite); Object.DestroyImmediate(texture); world.Dispose(); Object.DestroyImmediate(catalog);
        }
    }
}

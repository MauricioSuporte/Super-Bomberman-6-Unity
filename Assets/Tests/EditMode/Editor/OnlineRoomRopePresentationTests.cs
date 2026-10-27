using System.Collections.Generic;
using System.Reflection;
using Assets.Scripts.Netcode;
using Mirror;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed partial class OnlineRoomEditModeTests
{
    sealed class RopeViewFixture : System.IDisposable
    {
        public readonly GameObject actor = new("ApprovedRope"), grid = new("RopeGrid", typeof(Grid));
        public readonly Tile[] tiles = new Tile[4];
        public readonly BattleMode11ImpulseRopeController controller;
        public readonly Tilemap map;
        public readonly Vector3Int cell = new(2, 3);
        public readonly BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        readonly NetSync.NetMode oldMode = NetSync.Mode;
        readonly FieldInfo clientState = typeof(NetworkClient).GetField("connectState", BindingFlags.Static | BindingFlags.NonPublic);
        readonly FieldInfo pause = typeof(GamePauseController).GetField("<IsPaused>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        readonly object oldClient, oldPause;
        public RopeViewFixture()
        {
            oldClient = clientState.GetValue(null); oldPause = pause.GetValue(null);
            actor.SetActive(false);
            var child = new GameObject("Rope", typeof(Tilemap), typeof(TilemapRenderer), typeof(TilemapCollider2D));
            child.transform.SetParent(grid.transform, false); map = child.GetComponent<Tilemap>();
            for (int i = 0; i < tiles.Length; i++) tiles[i] = ScriptableObject.CreateInstance<Tile>();
            tiles[0].colliderType = Tile.ColliderType.Grid;
            controller = actor.AddComponent<BattleMode11ImpulseRopeController>();
            Set("indestructibleTilemap", map);
            Set("ropeTileAnimations", new[] { new BattleMode11ImpulseRopeController.RopeTileAnimation {
                tile = tiles[0], impulseAnimationTiles = new TileBase[] { tiles[1], tiles[0] },
                preparingAnimationTiles = new TileBase[] { tiles[2] } } });
            map.SetTile(cell, tiles[0]);
            NetSync.Mode = NetSync.NetMode.Client;
            clientState.SetValue(null, System.Enum.Parse(clientState.FieldType, "Connected")); pause.SetValue(null, false);
        }
        public void Set(string field, object value) => typeof(BattleMode11ImpulseRopeController).GetField(field, flags).SetValue(controller, value);
        public object Get(string field) => typeof(BattleMode11ImpulseRopeController).GetField(field, flags).GetValue(controller);
        public object Call(string method) => typeof(BattleMode11ImpulseRopeController).GetMethod(method, flags).Invoke(controller, null);
        public void Pause(bool value) => pause.SetValue(null, value);
        public RopeTileView Message(uint revision = 1, int frame = 1) => new() { scene = actor.scene.name,
            path = controller.NetworkRopePath, revision = revision, time = NetworkTime.time - .1,
            cells = new[] { cell }, entries = new[] { 0 }, frames = new[] { frame } };
        public void Dispose()
        {
            Call("OnDisable");
            var overlay = Get("ropeViewMap") as Tilemap;
            if (overlay != null) Object.DestroyImmediate(overlay.gameObject);
            var generated = (Dictionary<Tile, Tile>)Get("ropeViewTiles");
            foreach (var tile in generated.Values) Object.DestroyImmediate(tile);
            generated.Clear();
            Object.DestroyImmediate(actor); Object.DestroyImmediate(grid);
            foreach (var tile in tiles) Object.DestroyImmediate(tile);
            NetSync.Mode = oldMode; clientState.SetValue(null, oldClient); pause.SetValue(null, oldPause);
        }
    }

    [Test]
    public void RopeApprovedFramesUseVisualOnlyTilesAndRetainNativeCollisionAndFlags()
    {
        using var f = new RopeViewFixture();
        var random = Random.state;
        f.map.SetTileFlags(f.cell, TileFlags.None);
        var tint = new Color(.3f, .4f, .5f, .7f); f.map.SetColor(f.cell, tint);
        f.map.SetTileFlags(f.cell, TileFlags.LockColor);
        var message = f.Message();
        using (var writer = NetworkWriterPool.Get())
        { writer.Write(message); message = new NetworkReader(writer.ToArray()).Read<RopeTileView>(); }
        f.controller.ReceiveNetworkRope(message); f.Call("PresentNetworkRope");
        Assert.AreSame(f.tiles[0], f.map.GetTile(f.cell));
        Assert.AreEqual(Tile.ColliderType.Grid, f.map.GetColliderType(f.cell));
        Assert.AreEqual(0, f.map.GetColor(f.cell).a);
        var overlay = (Tilemap)f.Get("ropeViewMap");
        var plain = (Tile)overlay.GetTile(f.cell);
        Assert.IsNotNull(plain); Assert.AreNotSame(f.tiles[1], plain);
        Assert.IsNull(plain.gameObject); Assert.AreEqual(Tile.ColliderType.None, plain.colliderType);
        Assert.IsNull(overlay.GetComponent<Collider2D>()); Assert.AreEqual(tint, overlay.GetColor(f.cell));
        f.controller.ReceiveNetworkRope(f.Message(2, 0)); f.Call("PresentNetworkRope");
        Assert.IsNull(overlay.GetTile(f.cell)); Assert.AreEqual(tint, f.map.GetColor(f.cell));
        Assert.AreEqual(TileFlags.LockColor, f.map.GetTileFlags(f.cell)); Assert.AreEqual(random, Random.state);
    }

    [Test]
    public void RopeViewDefersFutureFramesFreezesOnPauseAndRejectsOldOrMalformedMessages()
    {
        using var f = new RopeViewFixture();
        var history = (List<RopeTileView>)f.Get("ropeViewHistory");
        var future = f.Message(); future.time = NetworkTime.time + 100;
        f.controller.ReceiveNetworkRope(future); f.Call("PresentNetworkRope"); Assert.IsNull(f.Get("ropeViewMap"));
        future.time = NetworkTime.time - .1; history[0] = future;
        f.Pause(true); f.Call("PresentNetworkRope"); Assert.IsNull(f.Get("ropeViewMap"));
        f.Pause(false); f.Call("PresentNetworkRope"); Assert.IsNotNull(f.Get("ropeViewMap"));
        f.controller.ReceiveNetworkRope(f.Message(1, 0)); Assert.AreEqual(1, history.Count);
        var malformed = f.Message(2); malformed.frames = new[] { -1 };
        f.controller.ReceiveNetworkRope(malformed); Assert.AreEqual(1, history.Count);
        malformed = f.Message(2); malformed.entries = new[] { 99 };
        f.controller.ReceiveNetworkRope(malformed); Assert.AreEqual(1, history.Count);
        malformed = f.Message(2); malformed.path = "Other";
        f.controller.ReceiveNetworkRope(malformed); Assert.AreEqual(1, history.Count);
        malformed = f.Message(2); malformed.scene = "Other";
        f.controller.ReceiveNetworkRope(malformed); Assert.AreEqual(1, history.Count);
        malformed = f.Message(2); malformed.time = double.NaN;
        f.controller.ReceiveNetworkRope(malformed); Assert.AreEqual(1, history.Count);
        malformed = f.Message(2); malformed.entries = System.Array.Empty<int>();
        f.controller.ReceiveNetworkRope(malformed); Assert.AreEqual(1, history.Count);
        malformed = f.Message(2); malformed.cells = new[] { f.cell, f.cell };
        malformed.frames = new[] { 1, 1 }; malformed.entries = new[] { 0, 0 };
        f.controller.ReceiveNetworkRope(malformed); Assert.AreEqual(1, history.Count);
        for (uint i = 2; i < 80; i++) f.controller.ReceiveNetworkRope(f.Message(i));
        Assert.AreEqual(64, history.Count);
    }

    [Test]
    public void RopeVisualReleasesOnTerrainReplacementClearAndDisableWithoutResurrectingTiles()
    {
        using var f = new RopeViewFixture();
        f.controller.ReceiveNetworkRope(f.Message()); f.Call("PresentNetworkRope");
        var overlay = (Tilemap)f.Get("ropeViewMap");
        f.map.SetTile(f.cell, f.tiles[3]); f.Call("PresentNetworkRope");
        Assert.AreSame(f.tiles[3], f.map.GetTile(f.cell)); Assert.IsNull(overlay.GetTile(f.cell));
        f.map.SetTile(f.cell, f.tiles[0]);
        f.controller.ReceiveNetworkRope(f.Message(2)); f.Call("PresentNetworkRope");
        var clear = f.Message(3); clear.cells = System.Array.Empty<Vector3Int>();
        clear.entries = clear.frames = System.Array.Empty<int>();
        f.controller.ReceiveNetworkRope(clear); f.Call("PresentNetworkRope");
        Assert.IsNull(overlay.GetTile(f.cell)); Assert.AreEqual(1, f.map.GetColor(f.cell).a);
        f.controller.ReceiveNetworkRope(f.Message(4)); f.Call("PresentNetworkRope");
        f.Call("OnDisable"); Assert.IsNull(overlay.GetTile(f.cell)); Assert.AreEqual(1, f.map.GetColor(f.cell).a);
    }

    [Test]
    public void RopeClientCannotTriggerNativeDeformationOrConsumeGameplayRandom()
    {
        using var f = new RopeViewFixture();
        var random = Random.state;
        var method = typeof(BattleMode11ImpulseRopeController).GetMethod("StartRopeDeformation", f.flags);
        var kind = method.GetParameters()[3].ParameterType;
        method.Invoke(f.controller, new object[] { f.map, new Vector3Int(2, 5), Vector2.up,
            System.Enum.ToObject(kind, 0), .25f });
        Assert.AreEqual(0, ((System.Collections.IDictionary)f.Get("runningSwaps")).Count);
        Assert.AreSame(f.tiles[0], f.map.GetTile(f.cell)); Assert.AreEqual(random, Random.state);
    }
}

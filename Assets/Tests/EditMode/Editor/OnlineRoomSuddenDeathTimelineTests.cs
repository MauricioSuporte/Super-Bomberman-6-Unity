using System;
using System.Collections.Generic;
using System.Reflection;
using Assets.Scripts.Netcode;
using Mirror;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

public sealed partial class OnlineRoomEditModeTests
{
    sealed class ClosingWorld : IDisposable
    {
        readonly WorldBombFixture world = new(false);
        public readonly GameObject root, actor;
        public readonly Tilemap solid, destructible;
        public readonly BattleSuddenDeathController controller;
        public readonly Tile stone, wood;
        readonly Texture2D texture;
        readonly Sprite sprite;
        public const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        public ClosingWorld()
        {
            root=new GameObject("ClosingGrid",typeof(Grid));
            var s=new GameObject("Solid",typeof(Tilemap),typeof(TilemapRenderer));s.transform.SetParent(root.transform,false);solid=s.GetComponent<Tilemap>();
            var d=new GameObject("Destructible",typeof(Tilemap));d.transform.SetParent(root.transform,false);destructible=d.GetComponent<Tilemap>();
            actor=new GameObject("ApprovedClosing");actor.SetActive(false);actor.transform.SetParent(root.transform,false);
            controller=actor.AddComponent<BattleSuddenDeathController>();
            texture=new Texture2D(2,2);sprite=Sprite.Create(texture,new Rect(0,0,2,2),Vector2.one*.5f,16);
            stone=ScriptableObject.CreateInstance<Tile>();stone.sprite=sprite;
            wood=ScriptableObject.CreateInstance<Tile>();wood.sprite=sprite;
            Set("indestructibleTilemap",solid);Set("destructibleTilemap",destructible);Set("currentStageIndestructibleTile",stone);Set("enableShadowVisual",false);
        }
        public void Set(string name,object value)=>typeof(BattleSuddenDeathController).GetField(name,Flags).SetValue(controller,value);
        public object Get(string name)=>typeof(BattleSuddenDeathController).GetField(name,Flags).GetValue(controller);
        public void Present()=>typeof(BattleSuddenDeathController).GetMethod("PresentNetworkSuddenDeath",Flags).Invoke(controller,null);
        public SuddenDeathState State(uint serial=1)=>new() {scene=actor.scene.name,revision=serial,time=NetworkTime.time-.01,
            active=true,elapsed=10,remaining=50,musicWait=1,path=new[]{Vector3Int.zero},committed=Array.Empty<Vector3Int>(),
            falls=new[]{new SuddenDeathFall {cell=Vector3Int.zero,start=Vector3.up*5,end=Vector3.zero,began=9,duration=.5f}}};
        public void Dispose()
        {
            typeof(BattleSuddenDeathController).GetMethod("ClearNetworkSuddenDeath",Flags).Invoke(controller,null);
            Object.DestroyImmediate(root);Object.DestroyImmediate(stone);Object.DestroyImmediate(wood);Object.DestroyImmediate(sprite);Object.DestroyImmediate(texture);world.Dispose();
        }
    }

    [Test]
    public void ClosingTimelineNeverCommitsFromAVisualDeadlineAndAppliesOnlyApprovedTerrain()
    {
        using var f=new ClosingWorld();f.destructible.transform.position=Vector3.right;
        var woodCell=f.destructible.WorldToCell(f.solid.GetCellCenterWorld(Vector3Int.zero));f.destructible.SetTile(woodCell,f.wood);
        var random=UnityEngine.Random.state;var state=f.State();
        using(var writer=NetworkWriterPool.Get()){writer.Write(state);state=new NetworkReader(writer.ToArray()).Read<SuddenDeathState>();}
        f.controller.ReceiveNetworkSuddenDeath(state);f.Present();
        Assert.IsTrue(f.controller.SuddenDeathStarted);Assert.IsNull(f.solid.GetTile(Vector3Int.zero));Assert.AreSame(f.wood,f.destructible.GetTile(woodCell));
        var views=(Dictionary<Vector3Int,(GameObject block,GameObject shadow)>)f.Get("networkViews");
        Assert.AreEqual(Vector3.zero,views[Vector3Int.zero].block.transform.position,"Late presentation may finish visually but must wait for terrain approval.");
        Assert.IsNull(views[Vector3Int.zero].block.GetComponent<Collider2D>());
        var commit=state;commit.revision=2;commit.falls=Array.Empty<SuddenDeathFall>();commit.committed=new[]{Vector3Int.zero};
        f.controller.ReceiveNetworkSuddenDeath(commit);f.Present();
        Assert.AreSame(f.stone,f.solid.GetTile(Vector3Int.zero));Assert.IsNull(f.destructible.GetTile(woodCell));
        Assert.IsTrue(f.controller.IsActiveSuddenDeathCell(Vector3Int.zero));
        Assert.AreEqual(0,((Dictionary<Vector3Int,Coroutine>)f.Get("damageCoroutines")).Count);
        f.controller.ReceiveNetworkSuddenDeath(state);f.Present();Assert.AreEqual(0,views.Count,"Old flights cannot resurrect after committed landing.");
        Assert.AreEqual(random,UnityEngine.Random.state);
    }

    [Test]
    public void ClosingTimelineValidatesBeforeSerialAndRecoversLateObserverWithoutEarlierEvents()
    {
        using var f=new ClosingWorld();var state=f.State();state.revision=7;
        var bad=state;bad.committed=new[]{Vector3Int.right};f.controller.ReceiveNetworkSuddenDeath(bad);
        bad=state;bad.falls=new[]{new SuddenDeathFall {cell=Vector3Int.zero,start=new Vector3(float.NaN,0),duration=1}};f.controller.ReceiveNetworkSuddenDeath(bad);
        bad=state;bad.path=new[]{Vector3Int.zero,Vector3Int.zero};f.controller.ReceiveNetworkSuddenDeath(bad);
        state.committed=new[]{Vector3Int.zero};state.falls=Array.Empty<SuddenDeathFall>();
        f.controller.ReceiveNetworkSuddenDeath(state);state.committed[0]=Vector3Int.right;f.Present();
        Assert.AreSame(f.stone,f.solid.GetTile(Vector3Int.zero));Assert.IsNull(f.solid.GetTile(Vector3Int.right));
        Assert.AreEqual(7u,f.Get("receivedNetworkRevision"));
        var cancel=f.State(8);cancel.active=false;f.controller.ReceiveNetworkSuddenDeath(cancel);
        Assert.IsFalse(f.controller.SuddenDeathStarted);Assert.AreEqual(0,((List<SuddenDeathState>)f.Get("networkHistory")).Count);
        f.controller.ReceiveNetworkSuddenDeath(f.State(7));f.Present();Assert.IsFalse(f.controller.SuddenDeathStarted);
    }

    [Test]
    public void ClosingTimelineFreezesOnPauseBoundsHistoryAndCannotRunNativeDropOnClient()
    {
        using var f=new ClosingWorld();var pause=typeof(GamePauseController).GetField("<IsPaused>k__BackingField",BindingFlags.Static|BindingFlags.NonPublic);var previous=pause.GetValue(null);
        try
        {
            var state=f.State();state.elapsed=10;state.falls[0].duration=2;state.paused=true;
            f.controller.ReceiveNetworkSuddenDeath(state);f.Present();
            var views=(Dictionary<Vector3Int,(GameObject block,GameObject shadow)>)f.Get("networkViews");Vector3 held=views[Vector3Int.zero].block.transform.position;
            Assert.AreEqual(2.5f,held.y,.0001f,"Approved falls preserve the native linear curve.");
            pause.SetValue(null,true);
            var commit=f.State(2);commit.committed=new[]{Vector3Int.zero};commit.falls=Array.Empty<SuddenDeathFall>();f.controller.ReceiveNetworkSuddenDeath(commit);f.Present();
            Assert.AreEqual(held,views[Vector3Int.zero].block.transform.position);Assert.IsNull(f.solid.GetTile(Vector3Int.zero));
            pause.SetValue(null,false);f.Present();Assert.AreSame(f.stone,f.solid.GetTile(Vector3Int.zero));
            for(uint i=3;i<50;i++){state.revision=i;f.controller.ReceiveNetworkSuddenDeath(state);}
            Assert.AreEqual(32,((List<SuddenDeathState>)f.Get("networkHistory")).Count);
            var routine=(System.Collections.IEnumerator)typeof(BattleSuddenDeathController).GetMethod("DropTileRoutine",ClosingWorld.Flags).Invoke(f.controller,new object[]{Vector3Int.right});
            Assert.IsFalse(routine.MoveNext());Assert.IsNull(f.solid.GetTile(Vector3Int.right));
        }
        finally {pause.SetValue(null,previous);}
    }
    [UnityEngine.TestTools.UnityTest]
    public System.Collections.IEnumerator ClosingNativeDisableCancelsPendingLandingAndCanBeReusedOffline()
    {
        yield return new UnityEngine.TestTools.EnterPlayMode();
        yield return UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("TitleScreen");
        var mode=NetSync.Mode;NetSync.Mode=NetSync.NetMode.Offline;
        var root=new GameObject("NativeClosingCancellation",typeof(Grid));
        var tile=ScriptableObject.CreateInstance<Tile>();var texture=new Texture2D(2,2);
        var sprite=Sprite.Create(texture,new Rect(0,0,2,2),Vector2.one*.5f,16);tile.sprite=sprite;
        try
        {
            var mapObject=new GameObject("Solid",typeof(Tilemap),typeof(TilemapRenderer));mapObject.transform.SetParent(root.transform,false);
            var map=mapObject.GetComponent<Tilemap>();
            var actor=new GameObject("NativeClosing");actor.SetActive(false);actor.transform.SetParent(root.transform,false);
            var controller=actor.AddComponent<BattleSuddenDeathController>();
            foreach(var pair in new Dictionary<string,object>{{"indestructibleTilemap",map},{"currentStageIndestructibleTile",tile},{"fallingDuration",.3f},{"enableShadowVisual",false}})
                typeof(BattleSuddenDeathController).GetField(pair.Key,ClosingWorld.Flags).SetValue(controller,pair.Value);
            actor.SetActive(true);
            var drop=typeof(BattleSuddenDeathController).GetMethod("DropTileRoutine",ClosingWorld.Flags);
            controller.StartCoroutine((System.Collections.IEnumerator)drop.Invoke(controller,new object[]{Vector3Int.zero}));
            yield return null;
            controller.enabled=false;
            yield return new WaitForSecondsRealtime(.4f);
            Assert.IsNull(map.GetTile(Vector3Int.zero),"A disabled mechanism cannot land a pending block later.");
            var pending=(Dictionary<Vector3Int,float>)typeof(BattleSuddenDeathController).GetField("activeFallingCellApplyTimes",ClosingWorld.Flags).GetValue(controller);
            Assert.AreEqual(0,pending.Count);
            controller.enabled=true;
            controller.StartCoroutine((System.Collections.IEnumerator)drop.Invoke(controller,new object[]{Vector3Int.zero}));
            yield return new WaitForSecondsRealtime(.4f);
            Assert.AreSame(tile,map.GetTile(Vector3Int.zero),"The normal offline landing still commits after reuse.");
            controller.enabled=false;
        }
        finally {Object.Destroy(root);Object.Destroy(tile);Object.Destroy(sprite);Object.Destroy(texture);NetSync.Mode=mode;}
        yield return new UnityEngine.TestTools.ExitPlayMode();
    }

}

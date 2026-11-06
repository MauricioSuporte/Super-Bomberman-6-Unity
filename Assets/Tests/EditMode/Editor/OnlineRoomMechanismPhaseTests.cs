using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Assets.Scripts.Netcode;
using Mirror;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

public sealed partial class OnlineRoomEditModeTests
{
    [Test]
    public void ApprovedTileFramesFreezeClientAnimationWithoutDrawingRandomOrInstallingPrefab()
    {
        using var world = new WorldBombFixture(false);
        var root = new GameObject("MechanismPhaseGrid", typeof(Grid));
        var child = new GameObject("Ground", typeof(Tilemap)); child.transform.SetParent(root.transform, false);
        var map = child.GetComponent<Tilemap>();
        var texture = new Texture2D(4, 2);
        var a = Sprite.Create(texture, new Rect(0,0,2,2), Vector2.one*.5f,16);
        var b = Sprite.Create(texture, new Rect(2,0,2,2), Vector2.one*.5f,16);
        var animation = ScriptableObject.CreateInstance<AnimatedTile>();
        animation.m_AnimatedSprites = new[] { a, b }; animation.m_MinSpeed=.7f; animation.m_MaxSpeed=2.5f;
        animation.m_TileColliderType = Tile.ColliderType.None;
        var prefabTile = ScriptableObject.CreateInstance<Tile>();
        var prefab = new GameObject("ForbiddenTileGameplay"); prefab.SetActive(false); prefabTile.gameObject=prefab; prefabTile.sprite=a;
        var catalog = ScriptableObject.CreateInstance<CoopSceneCatalog>(); catalog.tiles=new TileBase[] { animation, prefabTile };
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var random = Random.state;
        try
        {
            typeof(BombermanNetworkManager).GetField("coopCatalog",flags).SetValue(world.manager,catalog);
            ((Dictionary<TileBase,int>)typeof(BombermanNetworkManager).GetField("coopTileIds",flags).GetValue(world.manager))[animation]=0;
            var data = new TileAnimationData();
            Assert.IsTrue(animation.GetTileAnimationData(Vector3Int.zero,null,ref data));
            Assert.AreEqual(random,Random.state,"Reading a client tile cannot consume gameplay RNG.");
            var frame = new CoopTileFrame { cell=Vector3Int.zero,tile=0,color=Color.cyan,matrix=Matrix4x4.Translate(Vector3.right),animated=true,animationFrame=1 };
            using(var writer=NetworkWriterPool.Get()) { writer.Write(frame); frame=new NetworkReader(writer.ToArray()).Read<CoopTileFrame>(); }
            var apply=typeof(BombermanNetworkManager).GetMethod("ApplyCoopTileCells",flags);
            apply.Invoke(world.manager,new object[] {map,new[] {frame}});
            Assert.AreEqual(2,map.GetAnimationFrameCount(Vector3Int.zero));
            Assert.AreEqual(1,map.GetAnimationFrame(Vector3Int.zero));
            Assert.IsTrue((map.GetTileAnimationFlags(Vector3Int.zero)&TileAnimationFlags.PauseAnimation)!=0);
            Assert.AreEqual(Color.cyan,map.GetColor(Vector3Int.zero));
            Assert.AreEqual(frame.matrix,map.GetTransformMatrix(Vector3Int.zero));
            var changed=frame;changed.animationFrame=0;Assert.IsFalse(frame.Matches(changed));
            ((Dictionary<string,Tilemap>)typeof(BombermanNetworkManager).GetField("coopMaps",flags).GetValue(world.manager))["C09Map"]=map;
            ((Dictionary<string,List<Vector3Int>>)typeof(BombermanNetworkManager).GetField("coopAnimatedCells",flags).GetValue(world.manager))["C09Map"]=new List<Vector3Int> {Vector3Int.zero};
            var cache=new Dictionary<Vector3Int,CoopTileFrame> {{Vector3Int.zero,frame}};
            ((Dictionary<string,Dictionary<Vector3Int,CoopTileFrame>>)typeof(BombermanNetworkManager).GetField("coopLastTiles",flags).GetValue(world.manager))["C09Map"]=cache;
            map.SetColor(Vector3Int.zero,Color.blue);map.SetAnimationFrame(Vector3Int.zero,0);
            typeof(BombermanNetworkManager).GetMethod("CaptureCoopTileAnimations",flags).Invoke(world.manager,new object[] {new List<NetworkConnectionToClient>()});
            Assert.AreEqual(0,cache[Vector3Int.zero].animationFrame);
            Assert.AreEqual(Color.cyan,cache[Vector3Int.zero].color,"A compact animation update cannot acknowledge an unsent structural/color change.");

            apply.Invoke(world.manager,new object[] {map,new[] {new CoopTileFrame {cell=Vector3Int.right,tile=1,color=Color.white,matrix=Matrix4x4.identity}}});
            var safe=map.GetTile<Tile>(Vector3Int.right);
            Assert.AreNotSame(prefabTile,safe);Assert.IsNull(safe.gameObject);
            Assert.AreEqual(Tile.ColliderType.None,safe.colliderType);Assert.IsNull(map.GetInstantiatedObject(Vector3Int.right));
            Assert.AreSame(prefab,prefabTile.gameObject,"Never mutate the source asset to sanitize client presentation.");
            Assert.AreEqual(random,Random.state);
        }
        finally
        {
            map.ClearAllTiles();typeof(BombermanNetworkManager).GetMethod("ClearCoopTileAnimation",flags).Invoke(world.manager,null);
            Object.DestroyImmediate(root);Object.DestroyImmediate(animation);Object.DestroyImmediate(prefabTile);Object.DestroyImmediate(prefab);
            Object.DestroyImmediate(a);Object.DestroyImmediate(b);Object.DestroyImmediate(texture);Object.DestroyImmediate(catalog);Random.state=random;
        }
    }

    [Test]
    public void HeadlessTileClockAdvancesAndLoopsWithoutDrawingRandom()
    {
        var tile=ScriptableObject.CreateInstance<AnimatedTile>();
        var random=Random.state;
        try
        {
            tile.m_AnimatedSprites=new Sprite[5];tile.m_MinSpeed=10;tile.m_MaxSpeed=30;
            var method=typeof(BombermanNetworkManager).GetMethod("ApprovedHeadlessTileFrame",BindingFlags.Static|BindingFlags.NonPublic);
            Assert.AreEqual(0,method.Invoke(null,new object[] {tile,0f}));
            Assert.AreEqual(2,method.Invoke(null,new object[] {tile,.25f}));
            Assert.AreEqual(0,method.Invoke(null,new object[] {tile,.5f}));
            tile.UseSpecifiedTime=true;tile.SpecifiedTime=1;
            Assert.AreEqual(2,method.Invoke(null,new object[] {tile,.5f}));
            Assert.AreEqual(random,Random.state);
        }
        finally {Object.DestroyImmediate(tile);Random.state=random;}
    }

    [Test]
    public void RocketRenderersFollowGameplayPauseWithoutRequiringABody()
    {
        using var world = new WorldBombFixture(false);
        var root = new GameObject("RocketPauseClassification"); root.SetActive(false);
        try
        {
            root.AddComponent<StageAssets.DestructibleRocketController>();
            var visual = new GameObject("RocketVisual", typeof(SpriteRenderer)); visual.transform.SetParent(root.transform,false);
            var classify=typeof(BombermanNetworkManager).GetMethod("CoopPauseWithGameplay",BindingFlags.Instance|BindingFlags.NonPublic);
            Assert.IsTrue((bool)classify.Invoke(world.manager,new object[] {visual.GetComponent<SpriteRenderer>()}));
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void ClientCameraUsesAuthoredBoundsWithoutReactivatingRoomCollider()
    {
        var mode=NetSync.Mode;var root=new GameObject("ClientRoomCameraBounds");
        try
        {
            NetSync.Mode=NetSync.NetMode.Client;
            var box=root.AddComponent<BoxCollider2D>();box.size=new Vector2(14,12);box.offset=new Vector2(2,-1);
            root.transform.position=new Vector3(60,4);root.transform.rotation=Quaternion.Euler(0,0,20);box.enabled=false;
            var bounds=(Bounds)typeof(CameraFollowClamp2D).GetMethod("ReadFollowBounds",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[] {box});
            Assert.That(bounds.size.x,Is.GreaterThan(14));Assert.That(bounds.size.y,Is.GreaterThan(12));
            Assert.That(Vector3.Distance(bounds.center,root.transform.TransformPoint(box.offset)),Is.LessThan(.001f));
            Assert.IsFalse(box.enabled);
        }
        finally {Object.DestroyImmediate(root);NetSync.Mode=mode;}
    }

    [UnityTest]
    public IEnumerator BoilerDisableRestoresFrozenBombAndStopsPendingPhysicsMovement()
    {
        yield return new EnterPlayMode();yield return SceneManager.LoadSceneAsync("TitleScreen");
        var mode=NetSync.Mode;NetSync.Mode=NetSync.NetMode.Offline;
        var original=SceneManager.GetActiveScene();var scene=SceneManager.CreateScene("Stage_C09");SceneManager.SetActiveScene(scene);
        GameObject player=null,bombObject=null,root=null;
        try
        {
            player=Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab"));
            player.transform.position=new Vector3(30,30);player.GetComponent<MovementController>().enabled=false;
            var controller=player.GetComponent<BombController>();
            bombObject=Object.Instantiate(controller.bombPrefab,new Vector3(32,30),Quaternion.identity);
            var bomb=bombObject.GetComponent<Bomb>();bomb.Initialize(controller);bomb.SetFuseSeconds(20);
            root=new GameObject("NativeBoilerCancellation");var boiler=root.AddComponent<BoilerTriggerGroundTileHandler>();
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
            typeof(BoilerTriggerGroundTileHandler).GetField("pullDelaySeconds",flags).SetValue(boiler,0f);
            typeof(BoilerTriggerGroundTileHandler).GetField("pullMoveSeconds",flags).SetValue(boiler,.4f);
            boiler.OnBombAt(controller,new Vector2(32,30),Vector3Int.zero,null,bombObject);
            yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();
            var animation=bombObject.GetComponent<AnimatedSpriteRenderer>();Assert.IsNotNull(animation);Assert.IsTrue(animation.CapturePlayback().frozen);
            boiler.enabled=false;
            Assert.IsFalse(animation.CapturePlayback().frozen);
            var body=bombObject.GetComponent<Rigidbody2D>();Vector2 held=body.position;
            yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();
            Assert.That(Vector2.Distance(body.position,held),Is.LessThan(.01f));Assert.IsFalse(bomb.HasExploded);
            ExerciseMechanismExplosionScope(controller);
            ExerciseMechanismTileMessageRevisions();
        }
        finally
        {
            if(root!=null)Object.Destroy(root);if(bombObject!=null)Object.Destroy(bombObject);if(player!=null)Object.Destroy(player);
            SceneManager.SetActiveScene(original);SceneManager.UnloadSceneAsync(scene);NetSync.Mode=mode;
        }
        yield return new ExitPlayMode();
    }
    static void ExerciseMechanismExplosionScope(BombController controller)
    {
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var fxField=typeof(BombController).GetField("_netFxCollector",flags);
        var cellsField=typeof(BombController).GetField("_netDestroyedCells",flags);
        var outerFx=new List<ExplosionFx>();var outerCells=new List<Vector3Int>();
        fxField.SetValue(controller,outerFx);cellsField.SetValue(controller,outerCells);
        var mode=NetSync.Mode;
        try
        {
            controller.SpawnExplosionCrossForEffect(new Vector2(40,40),0,true);
            Assert.AreSame(outerFx,fxField.GetValue(controller));Assert.AreSame(outerCells,cellsField.GetValue(controller));
            int before=Object.FindObjectsByType<BombExplosion>().Length;
            Assert.That(before,Is.GreaterThan(0));
            NetSync.Mode=NetSync.NetMode.Client;
            controller.SpawnExplosionCrossForEffect(new Vector2(45,45),0,true);
            Assert.AreEqual(before,Object.FindObjectsByType<BombExplosion>().Length,"Pure clients cannot start mechanism explosion gameplay.");
        }
        finally {NetSync.Mode=mode;fxField.SetValue(controller,null);cellsField.SetValue(controller,null);}
    }

    static void ExerciseMechanismTileMessageRevisions()
    {
        using var world = new WorldBombFixture(false);
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var previousMode=BombermanNetworkManager.RoomMode;
        var root=new GameObject("C09PhaseProtocol",typeof(Grid));
        var child=new GameObject("Ground",typeof(Tilemap));child.transform.SetParent(root.transform,false);
        var map=child.GetComponent<Tilemap>();
        var texture=new Texture2D(4,2);
        var a=Sprite.Create(texture,new Rect(0,0,2,2),Vector2.one*.5f,16);
        var b=Sprite.Create(texture,new Rect(2,0,2,2),Vector2.one*.5f,16);
        var source=ScriptableObject.CreateInstance<AnimatedTile>();source.m_AnimatedSprites=new[] {a,b};source.m_TileColliderType=Tile.ColliderType.None;
        var replacement=ScriptableObject.CreateInstance<Tile>();replacement.sprite=a;
        var catalog=ScriptableObject.CreateInstance<CoopSceneCatalog>();catalog.tiles=new TileBase[] {source};
        try
        {
            typeof(BombermanNetworkManager).GetProperty("RoomMode").SetValue(null,OnlineRoomMode.NormalGame);
            string scene=SceneManager.GetActiveScene().name;
            Assert.IsTrue(BombermanNetworkManager.IsCoopStage);
            typeof(BombermanNetworkManager).GetField("coopScene",flags).SetValue(world.manager,scene);
            typeof(BombermanNetworkManager).GetField("coopCatalog",flags).SetValue(world.manager,catalog);
            var cameraA=new GameObject("C09CameraA",typeof(Camera));var cameraB=new GameObject("C09CameraB",typeof(Camera));
            try
            {
                cameraA.GetComponent<Camera>().enabled=true;cameraB.GetComponent<Camera>().enabled=false;
                var cameras=(Dictionary<string,Camera>)typeof(BombermanNetworkManager).GetField("coopClientCameras",flags).GetValue(world.manager);
                cameras["A"]=cameraA.GetComponent<Camera>();cameras["B"]=cameraB.GetComponent<Camera>();
                var select=typeof(BombermanNetworkManager).GetMethod("ApplyCoopCameraSelection",flags);
                select.Invoke(world.manager,new object[] {"B"});Assert.IsFalse(cameraA.GetComponent<Camera>().enabled);Assert.IsTrue(cameraB.GetComponent<Camera>().enabled);
                select.Invoke(world.manager,new object[] {"unknown"});Assert.IsTrue(cameraB.GetComponent<Camera>().enabled);
                var status=new CoopStageStatus {scene=scene,camera="B"};
                using(var writer=NetworkWriterPool.Get()) {writer.Write(status);status=new NetworkReader(writer.ToArray()).Read<CoopStageStatus>();}
                Assert.AreEqual("B",status.camera);
            }
            finally {Object.DestroyImmediate(cameraA);Object.DestroyImmediate(cameraB);}
            var maps=(Dictionary<string,Tilemap>)typeof(BombermanNetworkManager).GetField("coopMaps",flags).GetValue(world.manager);maps["C09Map"]=map;
            var ids=(Dictionary<TileBase,int>)typeof(BombermanNetworkManager).GetField("coopTileIds",flags).GetValue(world.manager);ids[source]=0;
            map.SetTile(Vector3Int.zero,source);
            var apply=typeof(BombermanNetworkManager).GetMethod("ApplyCoopTileAnimation",flags);
            var phase=new CoopTileAnimation {scene=scene,path="C09Map",revision=2,cells=new[] {new CoopTileAnimationFrame {cell=Vector3Int.zero,tile=0,frame=1}}};
            using(var writer=NetworkWriterPool.Get()) {writer.Write(phase);phase=new NetworkReader(writer.ToArray()).Read<CoopTileAnimation>();}
            apply.Invoke(world.manager,new object[] {phase});Assert.AreEqual(1,map.GetAnimationFrame(Vector3Int.zero));
            var stale=phase;stale.revision=1;stale.cells=new[] {new CoopTileAnimationFrame {cell=Vector3Int.zero,tile=0,frame=0}};
            apply.Invoke(world.manager,new object[] {stale});Assert.AreEqual(1,map.GetAnimationFrame(Vector3Int.zero));
            stale.revision=3;stale.scene="wrong-scene";apply.Invoke(world.manager,new object[] {stale});Assert.AreEqual(1,map.GetAnimationFrame(Vector3Int.zero));
            stale.scene=scene;stale.path="wrong-path";apply.Invoke(world.manager,new object[] {stale});Assert.AreEqual(1,map.GetAnimationFrame(Vector3Int.zero));
            stale.path="C09Map";stale.cells=new[] {new CoopTileAnimationFrame {cell=Vector3Int.zero,tile=0,frame=99}};
            apply.Invoke(world.manager,new object[] {stale});Assert.AreEqual(1,map.GetAnimationFrame(Vector3Int.zero));
            stale.cells=new[] {new CoopTileAnimationFrame {cell=Vector3Int.zero,tile=0,frame=0}};
            apply.Invoke(world.manager,new object[] {stale});Assert.AreEqual(0,map.GetAnimationFrame(Vector3Int.zero),"Malformed frames cannot consume a valid revision.");
            map.SetAnimationFrame(Vector3Int.zero,1);
            // Headless servers own a presentation clock instead of renderer state.
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)
                source.m_AnimationStartTime=1f/source.m_MinSpeed;
            var snapshot=(CoopTileFrame)typeof(BombermanNetworkManager).GetMethod("ReadCoopTileFrame",flags).Invoke(world.manager,new object[] {map,Vector3Int.zero});
            Assert.IsTrue(snapshot.animated);Assert.AreEqual(1,snapshot.animationFrame,"A late observer snapshot carries the current phase.");
            map.SetTile(Vector3Int.zero,replacement);phase.revision=4;
            apply.Invoke(world.manager,new object[] {phase});Assert.AreSame(replacement,map.GetTile(Vector3Int.zero),"A buffered animation cannot recreate removed terrain.");
        }
        finally
        {
            typeof(BombermanNetworkManager).GetProperty("RoomMode").SetValue(null,previousMode);
            map.ClearAllTiles();Object.DestroyImmediate(root);Object.DestroyImmediate(source);Object.DestroyImmediate(replacement);
            Object.DestroyImmediate(a);Object.DestroyImmediate(b);Object.DestroyImmediate(texture);Object.DestroyImmediate(catalog);
        }
    }

}

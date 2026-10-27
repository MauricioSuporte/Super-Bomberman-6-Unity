#if UNITY_EDITOR
using System.Collections.Generic;
using Assets.Scripts.Netcode;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Mirror;

public sealed partial class OnlineRoomEditModeTests
{
    [Test]
    public void BattleShuffleRoundUsesOnlyActiveSpawnsAndRetainsItsPermutation()
    {
        bool hadSettings = SaveSystem.HasOnlineBattleSettings;
        string saved = SaveSystem.CaptureOnlineBattleSettings();
        var previousScene = SceneManager.GetActiveScene();
        var scene = SceneManager.CreateScene("BattleMode_ShuffleTest");
        var random = Random.state; var mode = NetSync.Mode;
        GameObject actor = null;
        try
        {
            SceneManager.SetActiveScene(scene); NetSync.Mode = NetSync.NetMode.Offline;
            SaveSystem.BeginOnlineBattleSettings();
            Assert.AreEqual(0, new SaveData().battleModeShufflePlayers);
            for (int i = 1; i <= 6; i++)
                SaveSystem.SetBattleModePlayerControlMode(i, i == 1 || i == 3 || i == 6 ? BattleModePlayerControlMode.Man : BattleModePlayerControlMode.Off);
            SaveSystem.SetBattleModeShufflePlayers(1);
            var snapshot = SaveSystem.CaptureOnlineBattleSettings();
            SaveSystem.SetBattleModeShufflePlayers(0);
            SaveSystem.ApplyOnlineBattleSettings(snapshot);
            Assert.AreEqual(1, SaveSystem.GetBattleModeShufflePlayers());
            actor = new GameObject("ShuffleSpawner");
            var spawner = actor.AddComponent<PlayersSpawner>();
            var resolve = typeof(PlayersSpawner).GetMethod("ResolveBattleSpawnIndex", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var actual = new List<int>();
            foreach (int i in new[] { 0, 2, 5 }) actual.Add((int)resolve.Invoke(spawner, new object[] { i }));
            CollectionAssert.AreEquivalent(new[] { 0, 2, 5 }, actual);
            foreach (int i in new[] { 1, 3, 4 }) Assert.AreEqual(i, resolve.Invoke(spawner, new object[] { i }));
            for (int i = 0; i < 3; i++) Assert.AreEqual(actual[i], resolve.Invoke(spawner, new object[] { new[] { 0, 2, 5 }[i] }));
        }
        finally
        {
            if (actor != null) Object.DestroyImmediate(actor);
            SceneManager.SetActiveScene(previousScene); UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
            SaveSystem.ApplyOnlineBattleSettings(saved); if (!hadSettings) SaveSystem.EndOnlineBattleSettings();
            Random.state = random; NetSync.Mode = mode;
        }
    }

    [Test]
    public void SuddenDeathClientCopiesApprovedPathWithoutDrawingGameplayRandom()
    {
        var oldMode = NetSync.Mode; var oldRandom = Random.state;
        var actor = new GameObject("ApprovedSuddenDeathPath"); actor.SetActive(false);
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        try
        {
            NetSync.Mode = NetSync.NetMode.Client;
            var controller = actor.AddComponent<BattleSuddenDeathController>();
            var path = new[] { new Vector3Int(2, 3), new Vector3Int(1, 3), new Vector3Int(1, 2) };
            Random.InitState(418); float expectedNext = Random.value; Random.InitState(418);
            var initialize = typeof(BattleSuddenDeathController).GetMethod("InitializeClosingPath", flags);
            initialize.Invoke(controller, new object[] { path });
            Assert.AreEqual(expectedNext, Random.value, "An approved path must not first draw another drop pattern/corner/direction on the client.");
            var actual = (List<Vector3Int>)typeof(BattleSuddenDeathController).GetField("suddenDeathPath", flags).GetValue(controller);
            CollectionAssert.AreEqual(path, actual); path[0] = Vector3Int.zero; Assert.AreEqual(new Vector3Int(2, 3), actual[0]);
            initialize.Invoke(controller, new object[] { new[] { Vector3Int.left } }); CollectionAssert.AreEqual(new[] { Vector3Int.left }, actual);
        }
        finally { Object.DestroyImmediate(actor); NetSync.Mode = oldMode; Random.state = oldRandom; }
    }

    [Test]
    public void SuddenDeathClientUsesApprovedMusicWaitWithoutDrawingAnotherVoice()
    {
        var oldMode = NetSync.Mode; var oldRandom = Random.state; var oldMusic = GameMusicController.Instance;
        var actor = new GameObject("ApprovedSuddenDeathAudio"); actor.SetActive(false);
        var musicObject = new GameObject("SuddenDeathAudioHost"); musicObject.SetActive(false);
        var voice = AudioClip.Create("HurryUpTestVoice", 8000, 1, 8000, false);
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        System.Collections.IEnumerator routine = null;
        try
        {
            NetSync.Mode = NetSync.NetMode.Client; GameMusicController.Instance = musicObject.AddComponent<GameMusicController>();
            var controller = actor.AddComponent<BattleSuddenDeathController>();
            typeof(BattleSuddenDeathController).GetField("hurryUpVoices", flags).SetValue(controller, new[] { voice, voice });
            Random.InitState(623); float expectedNext = Random.value; Random.InitState(623);
            routine = (System.Collections.IEnumerator)typeof(BattleSuddenDeathController).GetMethod("PlayHurryUpAndResumeMusic", flags)
                .Invoke(controller, new object[] { 50f, 2.75f });
            Assert.IsTrue(routine.MoveNext()); Assert.IsInstanceOf<WaitForSeconds>(routine.Current);
            Assert.AreEqual(2.75f, (float)typeof(WaitForSeconds).GetField("m_Seconds", flags).GetValue(routine.Current));
            Assert.AreEqual(expectedNext, Random.value, "Only the authority selects a hurry-up voice; clients use its sound event and approved music wait.");
        }
        finally
        {
            (routine as System.IDisposable)?.Dispose(); Object.DestroyImmediate(actor); Object.DestroyImmediate(musicObject); Object.DestroyImmediate(voice);
            GameMusicController.Instance = oldMusic; NetSync.Mode = oldMode; Random.state = oldRandom;
        }
    }

    [Test]
    public void SuddenDeathClientRejectsIncompleteBeginAndDropBeforeApprovedPath()
    {
        var oldMode = NetSync.Mode;
        var actor = new GameObject("SuddenDeathInvalidBegin"); actor.SetActive(false);
        try
        {
            NetSync.Mode = NetSync.NetMode.Client; var controller = actor.AddComponent<BattleSuddenDeathController>();
            controller.ApplyNetworkDrop(false, Vector3Int.zero, 0, null, 0);
            controller.ApplyNetworkDrop(true, Vector3Int.zero, 50, null, 1);
            controller.ApplyNetworkDrop(true, Vector3Int.zero, 50, new Vector3Int[1025], 1);
            controller.ApplyNetworkDrop(true, Vector3Int.zero, float.NaN, new[] { Vector3Int.zero }, 1);
            controller.ApplyNetworkDrop(true, Vector3Int.zero, 50, new[] { Vector3Int.zero }, float.NaN);
            controller.ApplyNetworkDrop(true, Vector3Int.zero, 50, new[] { Vector3Int.zero }, -1);
            Assert.IsFalse(controller.SuddenDeathStarted, "Invalid payloads must return before UI, coroutines, RNG or gameplay cleanup.");
        }
        finally { Object.DestroyImmediate(actor); NetSync.Mode = oldMode; }
    }

    [Test]
    public void PowderClientCannotIgniteOrSpawnNativeDamageHitboxes()
    {
        var oldMode = NetSync.Mode;
        var actor = new GameObject("PowderAuthorityGate"); actor.SetActive(false);
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        System.Collections.IEnumerator routine = null;
        try
        {
            NetSync.Mode = NetSync.NetMode.Client;
            var controller = actor.AddComponent<BattleMode3PowderTrailController>();
            typeof(BattleMode3PowderTrailController).GetMethod("BuildTrailCells", flags).Invoke(controller, null);
            controller.OnExplosionHit(null, Vector2.zero, new Vector3Int(-4, 2), null);
            typeof(BattleMode3PowderTrailController).GetMethod("Ignite", flags).Invoke(controller, new object[] { null, Vector2.zero });
            routine = (System.Collections.IEnumerator)typeof(BattleMode3PowderTrailController).GetMethod("IgniteRoutine", flags).Invoke(controller, new object[] { null, Vector2.zero });
            Assert.IsFalse(routine.MoveNext()); Assert.IsFalse(controller.IgnitionRunning);
            typeof(BattleMode3PowderTrailController).GetMethod("SpawnHitboxes", flags).Invoke(controller, new object[] { null, Vector2.zero });
            var hitboxes = (List<BombExplosion>)typeof(BattleMode3PowderTrailController).GetField("activeHitboxes", flags).GetValue(controller);
            Assert.IsEmpty(hitboxes); Assert.IsNull(typeof(BattleMode3PowderTrailController).GetField("explosionPrefab", flags).GetValue(controller), "The client gate must return before resolving a gameplay explosion.");
        }
        finally { (routine as System.IDisposable)?.Dispose(); Object.DestroyImmediate(actor); NetSync.Mode = oldMode; }
    }

    [Test]
    public void PowderApprovedFramesRespectTimelinePauseAndEndWithoutDamageOrLocalIgnition()
    {
        var oldMode = NetSync.Mode;
        var clientState = typeof(NetworkClient).GetField("connectState", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        var oldClientState = clientState.GetValue(null);
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var paused = typeof(GamePauseController).GetField("<IsPaused>k__BackingField", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        bool wasPaused = (bool)paused.GetValue(null);
        var actor = new GameObject("ApprovedPowder"); actor.SetActive(false);
        var grid = new GameObject("PowderGrid", typeof(Grid));
        var mapObject = new GameObject("Ground", typeof(UnityEngine.Tilemaps.Tilemap)); mapObject.transform.SetParent(grid.transform, false);
        var map = mapObject.GetComponent<UnityEngine.Tilemaps.Tilemap>();
        var floor = ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();
        var texture = new Texture2D(1, 1);
        var sprites = new[] { Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.zero), Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.zero) };
        BattleMode3PowderTrailController controller = null;
        try
        {
            NetSync.Mode = NetSync.NetMode.Client; clientState.SetValue(null, System.Enum.Parse(clientState.FieldType, "Connected")); paused.SetValue(null, false);
            controller = actor.AddComponent<BattleMode3PowderTrailController>();
            typeof(BattleMode3PowderTrailController).GetField("groundTilemap", flags).SetValue(controller, map);
            typeof(BattleMode3PowderTrailController).GetField("horizontalExplosionSprites", flags).SetValue(controller, sprites);
            var cell = new Vector3Int(-4, 2); var matrix = Matrix4x4.Translate(new Vector3(0.1f, 0.2f));
            map.SetTile(cell, floor); map.SetTileFlags(cell, UnityEngine.Tilemaps.TileFlags.None); map.SetTransformMatrix(cell, matrix);
            var state = new PowderTrailFrame { scene = actor.scene.name, path = controller.NetworkPowderPath,
                revision = 1, time = NetworkTime.time - 0.1, frame = 0 };
            using (var writer = NetworkWriterPool.Get()) { writer.Write(state); state = new NetworkReader(writer.ToArray()).Read<PowderTrailFrame>(); }
            var present = typeof(BattleMode3PowderTrailController).GetMethod("PresentNetworkPowder", flags);
            controller.ReceiveNetworkPowder(state); present.Invoke(controller, null);
            Assert.AreSame(sprites[0], map.GetSprite(cell)); Assert.IsFalse(controller.IgnitionRunning);
            Assert.IsEmpty((List<BombExplosion>)typeof(BattleMode3PowderTrailController).GetField("activeHitboxes", flags).GetValue(controller));
            var future = state; future.revision = 2; future.time = NetworkTime.time + 100; future.frame = 1;
            controller.ReceiveNetworkPowder(future); present.Invoke(controller, null); Assert.AreSame(sprites[0], map.GetSprite(cell));
            var history = (List<PowderTrailFrame>)typeof(BattleMode3PowderTrailController).GetField("networkPowderHistory", flags).GetValue(controller);
            future.time = NetworkTime.time - 0.05; history[1] = future;
            paused.SetValue(null, true); present.Invoke(controller, null); Assert.AreSame(sprites[0], map.GetSprite(cell));
            paused.SetValue(null, false); present.Invoke(controller, null); Assert.AreSame(sprites[1], map.GetSprite(cell));
            state.revision = 3; state.frame = -1; controller.ReceiveNetworkPowder(state); present.Invoke(controller, null);
            Assert.AreSame(floor, map.GetTile(cell)); Assert.AreEqual(matrix, map.GetTransformMatrix(cell));
            controller.ReceiveNetworkPowder(future); present.Invoke(controller, null); Assert.AreSame(floor, map.GetTile(cell));
            state.revision = 4; state.frame = 99; controller.ReceiveNetworkPowder(state); Assert.AreEqual(1, history.Count);
            state.frame = 0; state.path = "WrongPath"; controller.ReceiveNetworkPowder(state); Assert.AreEqual(1, history.Count);
            state.path = controller.NetworkPowderPath; state.time = double.NaN; controller.ReceiveNetworkPowder(state); Assert.AreEqual(1, history.Count);
            state.time = NetworkTime.time - 0.01; controller.ReceiveNetworkPowder(state); present.Invoke(controller, null); Assert.AreSame(sprites[0], map.GetSprite(cell));
            typeof(BattleMode3PowderTrailController).GetMethod("OnDisable", flags).Invoke(controller, null);
            Assert.AreSame(floor, map.GetTile(cell)); Assert.AreEqual(matrix, map.GetTransformMatrix(cell)); Assert.IsEmpty(history);
        }
        finally
        {
            if (controller != null)
                foreach (string name in new[] { "horizontalExplosionTiles", "horizontalExplosionTilesFlippedY", "verticalExplosionTiles", "verticalExplosionTilesFlippedX", "cornerExplosionTiles" })
                    if (typeof(BattleMode3PowderTrailController).GetField(name, flags).GetValue(controller) is UnityEngine.Tilemaps.Tile[] tiles)
                        foreach (var tile in tiles) if (tile != null) Object.DestroyImmediate(tile);
            Object.DestroyImmediate(actor); Object.DestroyImmediate(grid); Object.DestroyImmediate(floor);
            foreach (var sprite in sprites) Object.DestroyImmediate(sprite); Object.DestroyImmediate(texture);
            NetSync.Mode = oldMode; clientState.SetValue(null, oldClientState); paused.SetValue(null, wasPaused);
        }
    }

    [Test]
    public void RoofAuthoritySnapshotRetainsClearedCellsForLateObserversAndClosing()
    {
        var oldMode = NetSync.Mode;
        var actor = new GameObject("RoofSnapshot"); actor.SetActive(false);
        var grid = new GameObject("RoofSnapshotGrid", typeof(Grid));
        var mapObject = new GameObject("Roof", typeof(UnityEngine.Tilemaps.Tilemap)); mapObject.transform.SetParent(grid.transform, false);
        var map = mapObject.GetComponent<UnityEngine.Tilemaps.Tilemap>();
        var closed = ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        try
        {
            NetSync.Mode = NetSync.NetMode.Offline;
            var controller = actor.AddComponent<BattleMode8RoofController>();
            typeof(BattleMode8RoofController).GetField("roofTilemap", flags).SetValue(controller, map);
            typeof(BattleMode8RoofController).GetField("roofTiles", flags).SetValue(controller,
                new[] { new BattleMode8RoofController.RoofTileSwap { closedTile = closed } });
            var cell = new Vector3Int(1, 2); map.SetTile(cell, closed);
            typeof(BattleMode8RoofController).GetField("closed", flags).SetValue(controller, false);
            typeof(BattleMode8RoofController).GetMethod("ApplyCurrentState", flags).Invoke(controller, null); Assert.IsNull(map.GetTile(cell));
            var capture = typeof(BattleMode8RoofController).GetMethod("CaptureNetworkRoofTiles", flags);
            Assert.IsTrue((bool)capture.Invoke(controller, null));
            var cells = (List<Vector3Int>)typeof(BattleMode8RoofController).GetField("networkRoofCellBuffer", flags).GetValue(controller);
            var indices = (List<int>)typeof(BattleMode8RoofController).GetField("networkRoofTileBuffer", flags).GetValue(controller);
            CollectionAssert.AreEqual(new[] { cell }, cells); CollectionAssert.AreEqual(new[] { -1 }, indices);
            typeof(BattleMode8RoofController).GetField("closed", flags).SetValue(controller, true);
            typeof(BattleMode8RoofController).GetMethod("ApplyCurrentState", flags).Invoke(controller, null);
            Assert.AreSame(closed, map.GetTile(cell)); Assert.IsTrue((bool)capture.Invoke(controller, null));
            CollectionAssert.AreEqual(new[] { 0 }, indices);
        }
        finally
        {
            Object.DestroyImmediate(actor); Object.DestroyImmediate(grid); Object.DestroyImmediate(closed); NetSync.Mode = oldMode;
        }
    }

    [TestCase(NetSync.NetMode.Offline)]
    [TestCase(NetSync.NetMode.Host)]
    [TestCase(NetSync.NetMode.Client)]
    public void RoofNativeStateChangesRequireSimulationAuthority(NetSync.NetMode mode)
    {
        var oldMode = NetSync.Mode;
        var actor = new GameObject("RoofAuthority"); actor.SetActive(false);
        var grid = new GameObject("RoofAuthorityGrid", typeof(Grid));
        var mapObject = new GameObject("Roof", typeof(UnityEngine.Tilemaps.Tilemap)); mapObject.transform.SetParent(grid.transform, false);
        var map = mapObject.GetComponent<UnityEngine.Tilemaps.Tilemap>();
        var closed = ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();
        var opened = ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        try
        {
            NetSync.Mode = mode;
            var controller = actor.AddComponent<BattleMode8RoofController>();
            typeof(BattleMode8RoofController).GetField("roofTilemap", flags).SetValue(controller, map);
            typeof(BattleMode8RoofController).GetField("roofTiles", flags).SetValue(controller,
                new[] { new BattleMode8RoofController.RoofTileSwap { closedTile = closed, openTile = opened } });
            map.SetTile(Vector3Int.zero, opened);
            typeof(BattleMode8RoofController).GetMethod("Awake", flags).Invoke(controller, null);
            typeof(BattleMode8RoofController).GetMethod("OnEnable", flags).Invoke(controller, null);
            typeof(BattleMode8RoofController).GetMethod("ApplyCurrentState", flags).Invoke(controller, null);
            Assert.AreSame(mode == NetSync.NetMode.Client ? opened : closed, map.GetTile(Vector3Int.zero),
                "Client startup/context-menu paths must not reset an approved roof phase; offline/host retain the native path.");
        }
        finally
        {
            Object.DestroyImmediate(actor); Object.DestroyImmediate(grid); Object.DestroyImmediate(closed); Object.DestroyImmediate(opened); NetSync.Mode = oldMode;
        }
    }

    [Test]
    public void RoofClientPresentsApprovedNullAndTransitionTilesOnTimelineWithBoundedHistory()
    {
        var oldMode = NetSync.Mode;
        var clientState = typeof(NetworkClient).GetField("connectState", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        var oldClientState = clientState.GetValue(null);
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var paused = typeof(GamePauseController).GetField("<IsPaused>k__BackingField", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        bool wasPaused = (bool)paused.GetValue(null);
        var actor = new GameObject("ApprovedRoof"); actor.SetActive(false);
        var grid = new GameObject("ApprovedRoofGrid", typeof(Grid));
        var mapObject = new GameObject("Roof", typeof(UnityEngine.Tilemaps.Tilemap)); mapObject.transform.SetParent(grid.transform, false);
        var map = mapObject.GetComponent<UnityEngine.Tilemaps.Tilemap>();
        var tiles = new UnityEngine.Tilemaps.Tile[3]; for (int i = 0; i < tiles.Length; i++) tiles[i] = ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();
        try
        {
            NetSync.Mode = NetSync.NetMode.Client; clientState.SetValue(null, System.Enum.Parse(clientState.FieldType, "Connected")); paused.SetValue(null, false);
            var controller = actor.AddComponent<BattleMode8RoofController>();
            typeof(BattleMode8RoofController).GetField("roofTilemap", flags).SetValue(controller, map);
            typeof(BattleMode8RoofController).GetField("roofTiles", flags).SetValue(controller, new[] {
                new BattleMode8RoofController.RoofTileSwap { closedTile = tiles[0], firstTransitionTile = tiles[1], secondTransitionTile = tiles[2] } });
            var cell = new Vector3Int(2, 3); map.SetTile(cell, tiles[0]);
            var state = new RoofTileState { scene = actor.scene.name, path = controller.NetworkRoofPath, revision = 1,
                time = NetworkTime.time - 0.1, cells = new[] { cell }, tiles = new[] { 1 } };
            using (var writer = NetworkWriterPool.Get()) { writer.Write(state); state = new NetworkReader(writer.ToArray()).Read<RoofTileState>(); }
            var present = typeof(BattleMode8RoofController).GetMethod("PresentNetworkRoof", flags);
            controller.ReceiveNetworkRoof(state); present.Invoke(controller, null); Assert.AreSame(tiles[1], map.GetTile(cell));
            var future = state; future.revision = 2; future.time = NetworkTime.time + 100; future.tiles = new[] { -1 };
            controller.ReceiveNetworkRoof(future); present.Invoke(controller, null); Assert.AreSame(tiles[1], map.GetTile(cell));
            var history = (List<RoofTileState>)typeof(BattleMode8RoofController).GetField("networkRoofHistory", flags).GetValue(controller);
            future.time = NetworkTime.time - 0.05; history[1] = future;
            paused.SetValue(null, true); present.Invoke(controller, null); Assert.AreSame(tiles[1], map.GetTile(cell));
            paused.SetValue(null, false); present.Invoke(controller, null); Assert.IsNull(map.GetTile(cell), "An approved open roof can clear tiles even for a late observer.");
            controller.ReceiveNetworkRoof(state); present.Invoke(controller, null); Assert.IsNull(map.GetTile(cell), "A stale phase must not rewind the roof.");
            state.revision = 3; state.tiles = new[] { 99 }; controller.ReceiveNetworkRoof(state); Assert.AreEqual(1, history.Count);
            state.tiles = new[] { 3 }; controller.ReceiveNetworkRoof(state); Assert.AreEqual(1, history.Count, "Reject null catalog entries; clearing is explicit -1.");
            state.tiles = new[] { 0 }; state.path = "WrongPath"; controller.ReceiveNetworkRoof(state); Assert.AreEqual(1, history.Count);
            state.path = controller.NetworkRoofPath; state.scene = "WrongScene"; controller.ReceiveNetworkRoof(state); Assert.AreEqual(1, history.Count);
            state.scene = actor.scene.name; state.time = double.NaN; controller.ReceiveNetworkRoof(state); Assert.AreEqual(1, history.Count);
            state.time = NetworkTime.time + 100;
            state.cells = new Vector3Int[513]; state.tiles = new int[513]; controller.ReceiveNetworkRoof(state); Assert.AreEqual(1, history.Count);
            state.cells = new[] { cell }; state.tiles = new[] { 0 };
            for (uint i = 3; i < 50; i++) { state.revision = i; controller.ReceiveNetworkRoof(state); }
            Assert.AreEqual(32, history.Count);
        }
        finally
        {
            Object.DestroyImmediate(actor); Object.DestroyImmediate(grid); foreach (var tile in tiles) Object.DestroyImmediate(tile);
            NetSync.Mode = oldMode; clientState.SetValue(null, oldClientState); paused.SetValue(null, wasPaused);
        }
    }

    [Test]
    public void ApprovedExplosionWaitKeepsPrefabDataAfterOwnerIsDestroyedAndStopsOnDisconnect()
    {
        var oldMode = NetSync.Mode;
        var clientState = typeof(NetworkClient).GetField("connectState", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        var oldClientState = clientState.GetValue(null);
        var actor = new GameObject("ExplosionDataOwner"); actor.SetActive(false); actor.AddComponent<BoxCollider2D>();
        var definition = new GameObject("ApprovedExplosionDefinition"); definition.SetActive(false);
        System.Collections.IEnumerator wait = null;
        try
        {
            NetSync.Mode = NetSync.NetMode.Client; clientState.SetValue(null, System.Enum.Parse(clientState.FieldType, "Connected"));
            var owner = actor.AddComponent<BombController>(); var prefab = definition.AddComponent<BombExplosion>(); owner.explosionPrefab = prefab;
            var approvedPrefab = owner.GetNetworkExplosionVisualPrefab(); Assert.AreSame(prefab, approvedPrefab);
            var parts = new[] { new ExplosionFx { pos = new Vector2(7000, 7000), part = 0, dir = Vector2.up } };
            wait = (System.Collections.IEnumerator)typeof(NetworkBombFx).GetMethod("PlayOnTimeline", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                .Invoke(null, new object[] { approvedPrefab, Vector2.zero, parts, false, 0.3f, NetworkTime.time + 100 });
            Assert.IsTrue(wait.MoveNext()); Object.DestroyImmediate(actor); actor = null;
            Assert.IsTrue(wait.MoveNext(), "An already-approved timeline wait must survive its owner's destruction without dereferencing gameplay components.");
            clientState.SetValue(null, System.Enum.Parse(clientState.FieldType, "Disconnected")); Assert.IsFalse(wait.MoveNext());
        }
        finally
        {
            (wait as System.IDisposable)?.Dispose(); if (actor != null) Object.DestroyImmediate(actor); Object.DestroyImmediate(definition);
            NetSync.Mode = oldMode; clientState.SetValue(null, oldClientState);
        }
    }

    [Test]
    public void ApprovedExplosionTimelineUsesActiveManagerEvenWhenOwnerIsInactive()
    {
        var oldMode = NetSync.Mode; var oldNetworkedScene = NetSync.IsNetworkedScene;
        var clientState = typeof(NetworkClient).GetField("connectState", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        var oldClientState = clientState.GetValue(null);
        var singleton = typeof(NetworkManager).GetProperty("singleton"); var oldManager = singleton.GetValue(null);
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var managerObject = new GameObject("ApprovedVisualManager"); managerObject.SetActive(false);
        var actor = new GameObject("InactiveExplosionOwner"); actor.SetActive(false); actor.AddComponent<BoxCollider2D>();
        const uint id = uint.MaxValue - 180;
        try
        {
            NetSync.Mode = NetSync.NetMode.Client; clientState.SetValue(null, System.Enum.Parse(clientState.FieldType, "Connected"));
            var manager = managerObject.AddComponent<BombermanNetworkManager>(); manager.enabled = false; singleton.SetValue(null, manager);
            var identity = actor.AddComponent<NetworkIdentity>(); var fx = actor.AddComponent<NetworkBombFx>();
            typeof(NetworkIdentity).GetMethod("InitializeNetworkBehaviours", flags).Invoke(identity, null);
            typeof(NetworkIdentity).GetProperty("netId").SetValue(identity, id); NetworkClient.spawned.Add(id, identity);
            managerObject.SetActive(true);
            var host = NetworkBombMotionLifetime.ApprovedVisualHost(fx);
            Assert.IsInstanceOf<NetworkBombMotionLifetime>(host); Assert.IsTrue(host.isActiveAndEnabled); Assert.IsFalse(actor.activeSelf);
            managerObject.SetActive(false); Assert.IsNull(NetworkBombMotionLifetime.ApprovedVisualHost(fx), "Never start a coroutine on an inactive fallback.");
            foreach (var behaviour in actor.GetComponents<MonoBehaviour>()) behaviour.enabled = false;
            fx.enabled = true; actor.SetActive(true);
            Assert.AreSame(fx, NetworkBombMotionLifetime.ApprovedVisualHost(fx), "An active owner remains a fallback when no active manager host is available.");
            managerObject.SetActive(true); Assert.AreSame(host, NetworkBombMotionLifetime.ApprovedVisualHost(fx), "Use the independent timeline host even before the owner deactivates.");
            NetSync.Mode = NetSync.NetMode.Host; Assert.IsNull(NetworkBombMotionLifetime.ApprovedVisualHost(fx), "The server/host must not duplicate its native explosion.");
        }
        finally
        {
            NetworkClient.spawned.Remove(id); Object.DestroyImmediate(actor);
            singleton.SetValue(null, oldManager); Object.DestroyImmediate(managerObject);
            NetSync.IsNetworkedScene = oldNetworkedScene; NetSync.Mode = oldMode; clientState.SetValue(null, oldClientState);
        }
    }

    [TestCase(false, NetworkBombFx.BombMotionKind.Punch)]
    [TestCase(true, NetworkBombFx.BombMotionKind.Punch)]
    [TestCase(false, NetworkBombFx.BombMotionKind.Revenge)]
    [TestCase(true, NetworkBombFx.BombMotionKind.Revenge)]
    public void InactiveOwnerKeepsApprovedBombMotionAndCancelsOnlyUnconfirmedActions(bool stopClient, NetworkBombFx.BombMotionKind kind)
    {
        var oldMode = NetSync.Mode; var oldNetworkedScene = NetSync.IsNetworkedScene;
        var clientState = typeof(NetworkClient).GetField("connectState", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        var oldClientState = clientState.GetValue(null);
        var timeline = typeof(NetworkClient).GetField("localTimeline", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        var oldTimeline = timeline.GetValue(null);
        var singleton = typeof(NetworkManager).GetProperty("singleton"); var oldManager = singleton.GetValue(null);
        var paused = typeof(GamePauseController).GetField("<IsPaused>k__BackingField", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        bool wasPaused = (bool)paused.GetValue(null);
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var managerObject = new GameObject("RetainedBombManager"); managerObject.SetActive(false);
        var actor = new GameObject("InactiveBombOwner"); actor.SetActive(false); actor.AddComponent<BoxCollider2D>();
        var replicas = new List<GameObject>();
        const uint actorId = uint.MaxValue - 170, approvedId = actorId - 1, predictedId = actorId - 2;
        NetworkBombMotionLifetime lifetime = null;
        try
        {
            NetSync.Mode = NetSync.NetMode.Client; clientState.SetValue(null, System.Enum.Parse(clientState.FieldType, "Connected")); paused.SetValue(null, false);
            var manager = managerObject.AddComponent<BombermanNetworkManager>(); singleton.SetValue(null, manager);
            var identity = actor.AddComponent<NetworkIdentity>(); var fx = actor.AddComponent<NetworkBombFx>();
            typeof(NetworkIdentity).GetMethod("InitializeNetworkBehaviours", flags).Invoke(identity, null);
            typeof(NetworkIdentity).GetProperty("netId").SetValue(identity, actorId); typeof(NetworkIdentity).GetProperty("isLocalPlayer").SetValue(identity, true);
            NetworkClient.spawned.Add(actorId, identity);
            Bomb Replica(uint id, Vector2 position)
            {
                var go = new GameObject("RetainedBomb" + id); go.SetActive(false); go.AddComponent<CircleCollider2D>(); replicas.Add(go);
                var bomb = go.AddComponent<Bomb>(); var bombIdentity = go.AddComponent<NetworkIdentity>();
                typeof(NetworkIdentity).GetProperty("netId").SetValue(bombIdentity, id); NetworkClient.spawned.Add(id, bombIdentity);
                go.SetActive(true); go.transform.position = position; go.GetComponent<Collider2D>().enabled = true;
                return bomb;
            }
            Vector2 origin = new(3000, 3000);
            var approved = Replica(approvedId, origin); var predicted = Replica(predictedId, origin + Vector2.up * 2);
            var motion = new NetworkBombFx.KickMotion { bombId = approvedId, revision = 1, inputTick = 20,
                kind = kind, start = origin, end = origin + Vector2.right, duration = 1,
                moving = true, first = true, serverTime = NetworkBombFx.KickServerNow };
            using (var writer = NetworkWriterPool.Get())
            { writer.Write(motion); motion = new NetworkReader(writer.ToArray()).Read<NetworkBombFx.KickMotion>(); }
            if (kind == NetworkBombFx.BombMotionKind.Revenge)
                Assert.IsFalse(fx.BeginAbilityPrediction(approved, motion, Vector2.right, .25f), "Eligibility to return to the arena is never speculative.");
            fx.ReceiveKickMotion(motion); Assert.IsFalse(approved.GetComponent<Collider2D>().enabled);
            Assert.IsFalse(fx.AbilityPredictionLocksMovement);
            var pending = motion; pending.kind = NetworkBombFx.BombMotionKind.Punch; pending.bombId = predictedId; pending.inputTick = 21; pending.revision = 0;
            pending.start = predicted.transform.position; pending.end = pending.start + Vector2.right;
            Assert.IsTrue(fx.BeginAbilityPrediction(predicted, pending, Vector2.right, 0.25f));
            Assert.IsTrue(fx.HasUnconfirmedClientMotion(predicted));
            typeof(NetworkBombFx).GetMethod("OnDisable", flags).Invoke(fx, null);
            lifetime = managerObject.GetComponent<NetworkBombMotionLifetime>(); Assert.IsNotNull(lifetime);
            Assert.IsFalse(fx.HasUnconfirmedClientMotion(predicted)); Assert.IsTrue(predicted.GetComponent<Collider2D>().enabled);
            Assert.IsFalse(fx.AbilityPredictionLocksMovement); Assert.IsFalse(approved.GetComponent<Collider2D>().enabled);
            float before = approved.transform.position.x;
            lifetime.Present(0.1f, 0.1f); Assert.Greater(approved.transform.position.x, before + 0.05f);
            Assert.IsFalse(approved.IsBeingPunched); Assert.IsFalse(approved.IsFusePaused); Assert.IsFalse(actor.activeSelf);
            Vector3 held = approved.transform.position; paused.SetValue(null, true); lifetime.Present(0.2f, 0.2f); Assert.AreEqual(held, approved.transform.position);
            paused.SetValue(null, false);
            motion.revision = 2; motion.moving = false; motion.first = false; motion.start = motion.end;
            motion.serverTime += 0.1; timeline.SetValue(null, motion.serverTime + 1);
            fx.ReceiveKickMotion(motion);
            Assert.IsFalse(approved.GetComponent<Collider2D>().enabled, "An approved stop waits behind the retained in-flight arc.");
            lifetime.Present(1f, 1f); lifetime.Present(0.1f, 0.1f);
            Assert.IsTrue(approved.GetComponent<Collider2D>().enabled); Assert.IsFalse(approved.ClientMotionNonBlocking);
            var playback = (System.Collections.IDictionary)typeof(NetworkBombFx).GetField("kickPlayback", flags).GetValue(fx); Assert.AreEqual(0, playback.Count);
            motion.revision = 3; motion.moving = true; motion.first = true; motion.start = origin; motion.serverTime += 0.1;
            fx.ReceiveKickMotion(motion); Assert.IsFalse(approved.GetComponent<Collider2D>().enabled);
            if (stopClient) fx.OnStopClient(); else NetworkClient.spawned.Remove(actorId);
            lifetime.Present(0.1f, 0.1f);
            Assert.IsTrue(approved.GetComponent<Collider2D>().enabled, "Disconnect/despawn must restore an in-flight bomb's captured collider.");
            Assert.AreEqual(0, playback.Count);
            Assert.AreEqual(0, ((HashSet<NetworkBombFx>)typeof(NetworkBombMotionLifetime).GetField("retained", flags).GetValue(lifetime)).Count);
        }
        finally
        {
            if (lifetime != null) lifetime.Clear();
            NetworkClient.spawned.Remove(actorId); NetworkClient.spawned.Remove(approvedId); NetworkClient.spawned.Remove(predictedId);
            Object.DestroyImmediate(actor); foreach (var go in replicas) Object.DestroyImmediate(go);
            singleton.SetValue(null, oldManager); Object.DestroyImmediate(managerObject);
            NetSync.IsNetworkedScene = oldNetworkedScene; NetSync.Mode = oldMode; clientState.SetValue(null, oldClientState); paused.SetValue(null, wasPaused); timeline.SetValue(null, oldTimeline);
        }
    }

    [Test]
    public void MagnetClientReadsInitialTilesAndPresentsOnlyFreshApprovedDirections()
    {
        var oldMode = NetSync.Mode;
        var clientState = typeof(NetworkClient).GetField("connectState", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        var oldClientState = clientState.GetValue(null);
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var paused = typeof(GamePauseController).GetField("<IsPaused>k__BackingField", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        bool wasPaused = (bool)paused.GetValue(null);
        var actor = new GameObject("ApprovedMagnetField"); actor.SetActive(false);
        var grid = new GameObject("MagnetGrid", typeof(Grid));
        var mapObject = new GameObject("Indestructible", typeof(Tilemap)); mapObject.transform.SetParent(grid.transform, false);
        var map = mapObject.GetComponent<Tilemap>();
        var tiles = new Tile[5]; for (int i = 0; i < tiles.Length; i++) tiles[i] = ScriptableObject.CreateInstance<Tile>();
        try
        {
            NetSync.Mode = NetSync.NetMode.Client; clientState.SetValue(null, System.Enum.Parse(clientState.FieldType, "Connected")); paused.SetValue(null, false);
            var handler = actor.AddComponent<MagnetIndestructibleTileHandler>();
            typeof(MagnetIndestructibleTileHandler).GetField("indestructibleTilemap", flags).SetValue(handler, map);
            string[] names = { "magnetLeft", "magnetUp", "magnetRight", "magnetDown" };
            for (int i = 0; i < names.Length; i++) typeof(MagnetIndestructibleTileHandler).GetField(names[i], flags).SetValue(handler, tiles[i]);
            var cell = new Vector3Int(3, 4); map.SetTile(cell, tiles[2]);
            typeof(MagnetIndestructibleTileHandler).GetMethod("Awake", flags).Invoke(handler, null);
            Assert.AreEqual(tiles[2], map.GetTile(cell), "A client startup must not reset a magnet to the left.");
            var scan = (System.Collections.IEnumerator)typeof(MagnetIndestructibleTileHandler).GetMethod("ScanRoutine", flags).Invoke(handler, null);
            Assert.IsFalse(scan.MoveNext()); (scan as System.IDisposable)?.Dispose();
            var phase = new MagnetFieldPhase { scene = actor.scene.name, path = handler.NetworkFieldPath, revision = 1,
                time = NetworkTime.time - 0.1, cells = new[] { cell }, directions = new byte[] { 1 } };
            using (var writer = NetworkWriterPool.Get()) { writer.Write(phase); phase = new NetworkReader(writer.ToArray()).Read<MagnetFieldPhase>(); }
            handler.ReceiveNetworkField(phase);
            var present = typeof(MagnetIndestructibleTileHandler).GetMethod("PresentNetworkField", flags); present.Invoke(handler, null);
            Assert.AreEqual(tiles[1], map.GetTile(cell));
            Assert.IsFalse(handler.HandleExplosionHit(null, Vector2.zero, cell, tiles[1])); Assert.AreEqual(tiles[1], map.GetTile(cell));
            var future = phase; future.revision = 2; future.time = NetworkTime.time + 100; future.directions = new byte[] { 3 };
            handler.ReceiveNetworkField(future); present.Invoke(handler, null); Assert.AreEqual(tiles[1], map.GetTile(cell));
            var history = (List<MagnetFieldPhase>)typeof(MagnetIndestructibleTileHandler).GetField("networkFieldHistory", flags).GetValue(handler);
            future.time = NetworkTime.time - 0.05; history[1] = future;
            paused.SetValue(null, true); present.Invoke(handler, null); Assert.AreEqual(tiles[1], map.GetTile(cell));
            paused.SetValue(null, false); present.Invoke(handler, null); Assert.AreEqual(tiles[3], map.GetTile(cell));
            handler.ReceiveNetworkField(phase); present.Invoke(handler, null); Assert.AreEqual(tiles[3], map.GetTile(cell), "An old direction cannot rewind a confirmed field.");
            phase.revision = 3; phase.directions = new byte[] { 9 }; handler.ReceiveNetworkField(phase); Assert.AreEqual(1, history.Count);
            phase.directions = new byte[] { 0 }; phase.scene = "WrongScene"; handler.ReceiveNetworkField(phase); Assert.AreEqual(1, history.Count);
            map.SetTile(cell, tiles[4]); present.Invoke(handler, null);
            Assert.AreEqual(tiles[4], map.GetTile(cell), "Buffered magnet phases must not restore a tile replaced by sudden death.");
            Assert.IsFalse(handler.IsMagnetCell(new Vector2Int(cell.x, cell.y)));
        }
        finally
        {
            Object.DestroyImmediate(actor); Object.DestroyImmediate(grid);
            foreach (var tile in tiles) Object.DestroyImmediate(tile);
            NetSync.Mode = oldMode; clientState.SetValue(null, oldClientState); paused.SetValue(null, wasPaused);
        }
    }

    [Test]
    public void BombClientEntryPointsCannotStartOrRedirectNativeMotion()
    {
        var oldMode = NetSync.Mode; var actor = new GameObject("ClientBombMotionGate"); actor.SetActive(false);
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        try
        {
            NetSync.Mode = NetSync.NetMode.Client;
            actor.AddComponent<BoxCollider2D>(); var bomb = actor.AddComponent<Bomb>();
            typeof(Bomb).GetField("isKicked", flags).SetValue(bomb, true);
            typeof(Bomb).GetField("kickDirection", flags).SetValue(bomb, Vector2.right);
            Assert.IsFalse(bomb.TryRedirectKick(Vector2.up));
            Assert.AreEqual(Vector2.right, typeof(Bomb).GetField("kickDirection", flags).GetValue(bomb));
            typeof(Bomb).GetField("isKicked", flags).SetValue(bomb, false);
            Assert.IsFalse(bomb.StartKick(Vector2.up, 1, 0, null));
            Assert.IsFalse(bomb.StartMagnetPull(Vector2.up, 1, 3, 0, null, 1));
            Assert.AreEqual(Vector2.right, typeof(Bomb).GetField("kickDirection", flags).GetValue(bomb));
            Assert.IsFalse(bomb.IsBeingMagnetPulled); Assert.IsFalse(bomb.IsBeingKicked);
        }
        finally { Object.DestroyImmediate(actor); NetSync.Mode = oldMode; }
    }

    [Test]
    public void CoopWorldAndAnimationClockFollowApprovedRiderTransformOffset()
    {
        var actor = new GameObject("CoopRiderClock"); actor.SetActive(false);
        var localPlayer = typeof(NetworkClient).GetProperty("localPlayer"); var previous = localPlayer.GetValue(null);
        try
        {
            actor.AddComponent<NetworkIdentity>();
            var sync = actor.AddComponent<NetworkTransformReliable>(); sync.syncInterval = (float)NetworkServer.sendInterval * 3; sync.timelineOffset = true;
            Assert.AreEqual(3u, sync.sendIntervalMultiplier);
            localPlayer.SetValue(null, actor.GetComponent<NetworkIdentity>());
            var clock = typeof(BombermanNetworkManager).GetProperty("CoopVisualClock", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            double expected = NetworkClient.sendInterval * 5;
            Assert.AreEqual(expected, NetworkTime.time - (double)clock.GetValue(null), 0.01);
            sync.timelineOffset = false;
            Assert.AreEqual(NetworkClient.sendInterval * 2, NetworkTime.time - (double)clock.GetValue(null), 0.01);
            localPlayer.SetValue(null, null);
            Assert.AreEqual(0, NetworkTime.time - (double)clock.GetValue(null), 0.01);
        }
        finally { localPlayer.SetValue(null, previous); Object.DestroyImmediate(actor); }
    }

    [Test]
    public void PlatformOwnsEachRiderPresentationUntilApprovedUnmountOrDisable()
    {
        var oldMode = NetSync.Mode;
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var platformObject = new GameObject("ApprovedPlatform"); platformObject.SetActive(false);
        var actors = new List<GameObject>();
        try
        {
            NetSync.Mode = NetSync.NetMode.Host;
            var platform = platformObject.AddComponent<FloatingPlatform>();
            platform.enabled = false; platformObject.SetActive(true);
            typeof(FloatingPlatform).GetMethod("Awake", flags).Invoke(platform, null);
            MovementController CreateRider(uint id)
            {
                var actor = new GameObject("PlatformRider" + id); actor.SetActive(false); actor.tag = "Player"; actors.Add(actor);
                actor.AddComponent<BoxCollider2D>(); actor.AddComponent<NetworkIdentity>(); actor.AddComponent<MovementController>(); actor.AddComponent<NetworkPlayerInput>();
                var identity = actor.GetComponent<NetworkIdentity>(); typeof(NetworkIdentity).GetMethod("InitializeNetworkBehaviours", flags).Invoke(identity, null);
                foreach (var behaviour in actor.GetComponents<MonoBehaviour>()) behaviour.enabled = false;
                actor.SetActive(true);
                typeof(NetworkIdentity).GetProperty("isServer").SetValue(identity, true); typeof(NetworkIdentity).GetProperty("netId").SetValue(identity, id);
                var movement = actor.GetComponent<MovementController>();
                typeof(MovementController).GetProperty(nameof(MovementController.Rigidbody)).SetValue(movement, actor.GetComponent<Rigidbody2D>());
                return movement;
            }
            var first = CreateRider(85); var second = CreateRider(86);
            Assert.IsTrue(platform.TryMount(first)); Assert.IsTrue(platform.TryMount(second));
            var token = typeof(NetworkPlayerInput).GetField("authorityMotionToken", flags);
            Assert.AreNotEqual(0u, token.GetValue(first.GetComponent<NetworkPlayerInput>())); Assert.AreNotEqual(0u, token.GetValue(second.GetComponent<NetworkPlayerInput>()));
            Assert.AreEqual("platform_transport", first.NetworkPredictionBlockReason, "Even at a stop the native platform owns the centered rider pose.");
            Vector2 previousPosition = first.Rigidbody.position;
            platformObject.transform.position += Vector3.down; Physics2D.SyncTransforms();
            typeof(FloatingPlatform).GetMethod("SnapAllRidersToPlatformCenter", flags).Invoke(platform, null);
            Assert.AreNotEqual(previousPosition, first.Rigidbody.position, "Fixture must move the actual platform bounds.");
            Assert.AreEqual(first.Rigidbody.position, second.Rigidbody.position);
            Assert.AreEqual(first.Rigidbody.position.x, first.transform.position.x, 0.0001f); Assert.AreEqual(first.Rigidbody.position.y, first.transform.position.y, 0.0001f);
            Assert.IsTrue(platform.TryUnmount(first)); Assert.IsFalse(FloatingPlatform.IsRidingPlatform(first));
            Assert.AreEqual(0u, token.GetValue(first.GetComponent<NetworkPlayerInput>())); Assert.AreNotEqual(0u, token.GetValue(second.GetComponent<NetworkPlayerInput>()));
            typeof(FloatingPlatform).GetMethod("OnDisable", flags).Invoke(platform, null);
            Assert.IsFalse(FloatingPlatform.IsRidingPlatform(second)); Assert.AreEqual(0u, token.GetValue(second.GetComponent<NetworkPlayerInput>()));
            Assert.IsFalse(second.InputLocked);
            NetSync.Mode = NetSync.NetMode.Client;
            Assert.IsFalse(platform.TryMount(first)); Assert.IsFalse(platform.TryUnmount(second));
        }
        finally
        {
            Object.DestroyImmediate(platformObject);
            foreach (var actor in actors)
            {
                if (actor.TryGetComponent<NetworkIdentity>(out var identity)) typeof(NetworkIdentity).GetProperty("isServer").SetValue(identity, false);
                Object.DestroyImmediate(actor);
            }
            NetSync.Mode = oldMode;
        }
    }

    [Test]
    public void WaterBarrierReconciliationReplaysIndependentApprovedCopy()
    {
        var actor = new GameObject("WaterBarrierReplay"); actor.SetActive(false);
        var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
        var oldMode = NetSync.Mode;
        try
        {
            NetSync.Mode = NetSync.NetMode.Offline;
            actor.AddComponent<BoxCollider2D>(); var movement = actor.AddComponent<MovementController>();
            typeof(MovementController).GetProperty(nameof(MovementController.Rigidbody)).SetValue(movement, actor.GetComponent<Rigidbody2D>());
            var origin = new Vector2(30000, 30000);
            typeof(WaterCycleCurrent).GetMethod("RegisterObstacleBarrier", flags).Invoke(null, new object[] { movement, origin, Vector2.right, 1f });
            var state = movement.CaptureNetworkMovementState(); Assert.IsTrue(state.waterBarrier.active);
            using (var writer = NetworkWriterPool.Get())
            { writer.Write(state); state = new NetworkReader(writer.ToArray()).Read<MovementController.NetworkMovementState>(); }
            NetSync.Mode = NetSync.NetMode.Client; movement.SetPredictLocally(true); movement.RestoreNetworkMovementState(state);
            Assert.IsTrue(WaterCycleCurrent.BlocksPlayerForwardMovement(movement, origin + Vector2.right * 0.125f));
            Assert.IsFalse(WaterCycleCurrent.BlocksPlayerForwardMovement(movement, origin + Vector2.up));
            Assert.IsFalse(WaterCycleCurrent.BlocksPlayerForwardMovement(movement, origin + Vector2.left * 0.5f));
            Assert.IsFalse(movement.CaptureNetworkMovementState().waterBarrier.active);
            Assert.IsTrue(WaterCycleCurrent.CaptureNetworkBarrier(movement).active, "Predicted retreat must not mutate the native server barrier.");
            movement.RestoreNetworkMovementState(state);
            Assert.IsTrue(WaterCycleCurrent.BlocksPlayerForwardMovement(movement, origin + Vector2.right * 0.125f), "Reconciliation restores the barrier before replay.");
        }
        finally
        {
            movementCleanup(); NetSync.Mode = oldMode; Object.DestroyImmediate(actor);
        }
        void movementCleanup()
        {
            var barriers = (System.Collections.IDictionary)typeof(WaterCycleCurrent).GetField("obstacleBarriers", flags).GetValue(null);
            if (actor != null && actor.TryGetComponent<MovementController>(out var movement)) barriers.Remove(movement);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void WaterPushApprovesContinuousMotionFreezesPauseAndRestoresPreviousLock(bool previouslyLocked)
    {
        var oldMode = NetSync.Mode;
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var paused = typeof(GamePauseController).GetField("<IsPaused>k__BackingField", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        bool wasPaused = (bool)paused.GetValue(null);
        var actor = new GameObject("WaterPushOwner"); actor.SetActive(false);
        var currentObject = new GameObject("WaterPushCurrent"); currentObject.SetActive(false);
        NetworkIdentity identity = null; System.Collections.IEnumerator push = null;
        try
        {
            NetSync.Mode = NetSync.NetMode.Host;
            actor.AddComponent<BoxCollider2D>(); actor.AddComponent<NetworkIdentity>(); actor.AddComponent<MovementController>(); var input = actor.AddComponent<NetworkPlayerInput>();
            identity = actor.GetComponent<NetworkIdentity>(); typeof(NetworkIdentity).GetMethod("InitializeNetworkBehaviours", flags).Invoke(identity, null);
            var movement = actor.GetComponent<MovementController>(); var body = actor.GetComponent<Rigidbody2D>();
            foreach (var behaviour in actor.GetComponents<MonoBehaviour>()) behaviour.enabled = false;
            actor.SetActive(true); typeof(MovementController).GetProperty(nameof(MovementController.Rigidbody)).SetValue(movement, body);
            typeof(NetworkIdentity).GetProperty("isServer").SetValue(identity, true); typeof(NetworkIdentity).GetProperty("netId").SetValue(identity, 84u);
            movement.SetNetworkTickDriven(true); movement.SetInputLocked(previouslyLocked, false); movement.SetExternalMovementAllowsHazardDamage(true);
            Vector2 origin = new(30000, 30000); body.position = origin; actor.transform.position = origin;
            var current = currentObject.AddComponent<WaterCycleCurrent>();
            paused.SetValue(null, true);
            push = (System.Collections.IEnumerator)typeof(WaterCycleCurrent).GetMethod("PushPlayerRoutine", flags).Invoke(current, new object[] { movement, true });
            Assert.IsTrue(push.MoveNext()); Assert.AreEqual(origin, body.position); Assert.IsTrue(movement.InputLocked); Assert.IsTrue(movement.ExternalMovementOverride);
            var token = typeof(NetworkPlayerInput).GetField("authorityMotionToken", flags); Assert.AreNotEqual(0u, token.GetValue(input));
            paused.SetValue(null, false); Assert.IsTrue(push.MoveNext()); Assert.Greater(body.position.x, origin.x);
            Assert.AreEqual(body.position.x, actor.transform.position.x, 0.0001f, "Both snapshot sources must agree on the approved step.");
            typeof(WaterCycleCurrent).GetMethod("OnDisable", flags).Invoke(current, null);
            Assert.AreEqual(previouslyLocked, movement.InputLocked); Assert.IsFalse(movement.ExternalMovementOverride);
            Assert.IsTrue(movement.ExternalMovementAllowsHazardDamage); Assert.AreEqual(0u, token.GetValue(input));
        }
        finally
        {
            (push as System.IDisposable)?.Dispose();
            if (identity != null) typeof(NetworkIdentity).GetProperty("isServer").SetValue(identity, false);
            Object.DestroyImmediate(actor); Object.DestroyImmediate(currentObject); NetSync.Mode = oldMode; paused.SetValue(null, wasPaused);
        }
    }

    [Test]
    public void WaterClientCannotStartNativePushOrApplyHazardProtection()
    {
        var oldMode = NetSync.Mode; var actor = new GameObject("WaterClientGate"); actor.SetActive(false);
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        try
        {
            NetSync.Mode = NetSync.NetMode.Client;
            var current = actor.AddComponent<WaterCycleCurrent>();
            var push = (System.Collections.IEnumerator)typeof(WaterCycleCurrent).GetMethod("PushPlayerRoutine", flags).Invoke(current, new object[] { null, true });
            Assert.IsFalse(push.MoveNext()); (push as System.IDisposable)?.Dispose();
            Assert.AreEqual(0, ((System.Collections.IDictionary)typeof(WaterCycleCurrent).GetField("activePushes", flags).GetValue(current)).Count);
        }
        finally { Object.DestroyImmediate(actor); NetSync.Mode = oldMode; }
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public void InterruptedPortalPhaseRestoresVisualsAndMaskWithoutTeleportingActor(bool fixedMask, bool disableController)
    {
        var oldMode = NetSync.Mode; NetSync.Mode = NetSync.NetMode.Offline;
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var actor = new GameObject("InterruptedPortalActor"); actor.SetActive(false);
        var controllerObject = new GameObject("InterruptedPortal"); controllerObject.SetActive(false);
        var texture = new Texture2D(8, 8); var sprite = Sprite.Create(texture, new Rect(0, 0, 8, 8), Vector2.one * 0.5f);
        System.Collections.IEnumerator phase = null;
        try
        {
            actor.AddComponent<BoxCollider2D>(); var movement = actor.AddComponent<MovementController>();
            typeof(MovementController).GetProperty(nameof(MovementController.Rigidbody)).SetValue(movement, actor.GetComponent<Rigidbody2D>());
            var head = new GameObject("PortalHead"); head.transform.SetParent(actor.transform, false);
            head.transform.localScale = new Vector3(0.9f, 1.1f, 1); head.transform.localPosition = Vector3.up * 0.2f;
            var renderer = head.AddComponent<SpriteRenderer>(); renderer.sprite = sprite;
            var portal = controllerObject.AddComponent<BattleMode7PortalController>();
            foreach (var behaviour in actor.GetComponents<MonoBehaviour>()) behaviour.enabled = false;
            actor.SetActive(true);
            typeof(BattleMode7PortalController).GetField("useFixedPortalMaskVisual", flags).SetValue(portal, fixedMask);
            var originalScale = head.transform.localScale; var originalPosition = head.transform.localPosition;
            phase = (System.Collections.IEnumerator)typeof(BattleMode7PortalController).GetMethod("AnimatePortalSinkRise", flags)
                .Invoke(portal, new object[] { movement, Vector2.one, false, 1f, null, 0f });
            Assert.IsTrue(phase.MoveNext());
            if (fixedMask) Assert.AreEqual(SpriteMaskInteraction.VisibleInsideMask, renderer.maskInteraction);
            var interruptedPosition = new Vector2(30000, 30000); movement.Rigidbody.position = interruptedPosition;
            Assert.AreEqual(interruptedPosition, movement.Rigidbody.position, "Fixture needs an active native body before testing cleanup.");
            if (disableController) typeof(BattleMode7PortalController).GetMethod("OnDisable", flags).Invoke(portal, null);
            else { (phase as System.IDisposable).Dispose(); phase = null; }
            Assert.AreEqual(originalScale, head.transform.localScale); Assert.AreEqual(originalPosition, head.transform.localPosition);
            Assert.AreEqual(SpriteMaskInteraction.None, renderer.maskInteraction);
            Assert.AreEqual(interruptedPosition, movement.Rigidbody.position, "Cleanup must not send an interrupted actor back to the portal.");
            foreach (var mask in Object.FindObjectsByType<SpriteMask>()) Assert.AreNotEqual("Stage34PortalFixedMask", mask.name);
        }
        finally
        {
            (phase as System.IDisposable)?.Dispose(); Object.DestroyImmediate(actor); Object.DestroyImmediate(controllerObject);
            Object.DestroyImmediate(sprite); Object.DestroyImmediate(texture); NetSync.Mode = oldMode;
        }
    }

    [Test]
    public void VisualPoseRoundTripsHierarchyAndBaselineNeverRewindsGameplayRoot()
    {
        var actor = new GameObject("VisualPoseActor"); actor.SetActive(false);
        var copy = new GameObject("VisualPoseCopy"); copy.SetActive(false);
        try
        {
            actor.transform.SetPositionAndRotation(new Vector3(3, 4), Quaternion.Euler(0, 0, 15));
            actor.transform.localScale = Vector3.one * 2;
            copy.transform.SetPositionAndRotation(actor.transform.position, actor.transform.rotation);
            copy.transform.localScale = actor.transform.localScale;
            var parent = new GameObject("MountOffset"); parent.transform.SetParent(actor.transform, false);
            parent.transform.localPosition = Vector3.up; parent.transform.localScale = Vector3.one * 0.8f;
            var visual = new GameObject("Head"); visual.transform.SetParent(parent.transform, false);
            visual.transform.localPosition = Vector3.down * 0.3f; visual.transform.localScale = new Vector3(1, 0.4f, 1);
            visual.transform.localRotation = Quaternion.Euler(0, 0, 35);
            var source = visual.AddComponent<SpriteRenderer>(); source.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            var target = new GameObject("RenderedHead"); target.transform.SetParent(copy.transform, false);
            var sprite = target.AddComponent<SpriteRenderer>(); var animated = target.AddComponent<AnimatedSpriteRenderer>();
            var baseline = NetworkVisualPose.CaptureBaseline(new[] { animated }, copy.transform);
            var pose = NetworkVisualPose.Capture(actor.transform, source);
            using (var writer = NetworkWriterPool.Get())
            { writer.Write(pose); pose = new NetworkReader(writer.ToArray()).Read<NetworkVisualPose>(); }
            pose.Apply(copy.transform, sprite);
            Assert.Less(Vector3.Distance(source.transform.position, target.transform.position), 0.0001f);
            Assert.Less(Vector3.Distance(source.transform.lossyScale, target.transform.lossyScale), 0.0001f);
            Assert.Less(Quaternion.Angle(source.transform.rotation, target.transform.rotation), 0.001f);
            Assert.AreEqual(source.maskInteraction, sprite.maskInteraction);
            NetworkVisualPose.RestoreBaseline(baseline);
            Assert.AreEqual(Vector3.zero, target.transform.localPosition); Assert.AreEqual(Vector3.one, target.transform.localScale);
            Assert.AreEqual(SpriteMaskInteraction.None, sprite.maskInteraction);
            var rootSprite = copy.AddComponent<SpriteRenderer>(); var rootAnimation = copy.AddComponent<AnimatedSpriteRenderer>();
            baseline = NetworkVisualPose.CaptureBaseline(new[] { rootAnimation }, copy.transform);
            copy.transform.position += Vector3.right * 7; var confirmedPosition = copy.transform.position;
            pose.Apply(copy.transform, rootSprite); NetworkVisualPose.RestoreBaseline(baseline);
            Assert.AreEqual(confirmedPosition, copy.transform.position, "Presentation cannot rewind a confirmed gameplay root.");
            Assert.AreEqual(Vector3.one * 2, copy.transform.localScale);
            var next = pose; next.position += Vector3.up; next.scale = Vector3.one; next.mask = (byte)SpriteMaskInteraction.None;
            var middle = NetworkVisualPose.Interpolate(pose, next, 0.5f);
            Assert.AreEqual(pose.mask, middle.mask); Assert.AreEqual(pose.position + Vector3.up * 0.5f, middle.position);
            Assert.IsFalse(NetworkVisualPose.Interpolate(default, next, 1).valid);
        }
        finally { Object.DestroyImmediate(actor); Object.DestroyImmediate(copy); }
    }

    [Test]
    public void PortalMasksUseApprovedTimelineAndEmptySnapshotRemovesOnlyViews()
    {
        var oldMode = NetSync.Mode;
        var clientState = typeof(NetworkClient).GetField("connectState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var oldClientState = clientState.GetValue(null);
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var actor = new GameObject("PortalMaskTestManager"); actor.SetActive(false);
        BombermanNetworkManager manager = null;
        try
        {
            NetSync.Mode = NetSync.NetMode.Client; clientState.SetValue(null, System.Enum.Parse(clientState.FieldType, "Connected"));
            manager = actor.AddComponent<BombermanNetworkManager>();
            var message = new PortalMaskSnapshot { scene = SceneManager.GetActiveScene().name, revision = 1, time = NetworkTime.time - 0.1,
                masks = new[] { new PortalMaskView { id = 10, position = new Vector3(7, 8), scale = new Vector3(2, 3, 1),
                    rotation = Quaternion.identity, cutoff = 0.01f, custom = true, backOrder = 3, frontOrder = 15 } } };
            using (var writer = NetworkWriterPool.Get())
            { writer.Write(message); message = new NetworkReader(writer.ToArray()).Read<PortalMaskSnapshot>(); }
            manager.ApplyPortalMasks(message);
            var present = typeof(BombermanNetworkManager).GetMethod("PresentPortalMaskViews", flags); present.Invoke(manager, null);
            var views = (Dictionary<int, SpriteMask>)typeof(BombermanNetworkManager).GetField("portalMaskViews", flags).GetValue(manager);
            Assert.AreEqual(1, views.Count); var mask = views[10];
            Assert.AreEqual(message.masks[0].position, mask.transform.position); Assert.AreEqual(message.masks[0].scale, mask.transform.localScale);
            Assert.IsTrue(mask.isCustomRangeActive); Assert.AreEqual(15, mask.frontSortingOrder);
            Assert.IsNull(mask.GetComponent<Collider2D>()); Assert.IsNull(mask.GetComponent<Rigidbody2D>()); Assert.IsNull(mask.GetComponent<NetworkIdentity>());
            Assert.AreEqual(0, mask.GetComponents<MonoBehaviour>().Length);
            var empty = message; empty.revision = 2; empty.time = NetworkTime.time + 100; empty.masks = new PortalMaskView[0];
            manager.ApplyPortalMasks(empty); present.Invoke(manager, null); Assert.AreEqual(1, views.Count, "Do not remove the current mask before its approved timeline.");
            var history = (List<PortalMaskSnapshot>)typeof(BombermanNetworkManager).GetField("portalMaskHistory", flags).GetValue(manager);
            empty.time = NetworkTime.time - 0.05; history[1] = empty;
            present.Invoke(manager, null); Assert.AreEqual(0, views.Count);
            manager.ApplyPortalMasks(message); Assert.AreEqual(1, history.Count, "An old snapshot cannot resurrect a mask.");
            message.revision = 3; message.scene = "DifferentScene"; manager.ApplyPortalMasks(message); Assert.AreEqual(1, history.Count);
        }
        finally
        {
            if (manager != null) typeof(BombermanNetworkManager).GetMethod("ClearPortalMaskViews", flags).Invoke(manager, null);
            Object.DestroyImmediate(actor); NetSync.Mode = oldMode; clientState.SetValue(null, oldClientState);
        }
    }

    [Test]
    public void PortalClientCannotTeleportOrDrawGameplayRandomnessButCanPresentApprovedStars()
    {
        var oldMode = NetSync.Mode; var oldRandom = Random.state;
        var clientState = typeof(NetworkClient).GetField("connectState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var oldClientState = clientState.GetValue(null);
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var actor = new GameObject("PortalAuthorityTest"); actor.SetActive(false);
        var texture = new Texture2D(8, 8); var sprite = Sprite.Create(texture, new Rect(0, 0, 8, 8), Vector2.one * 0.5f);
        try
        {
            NetSync.Mode = NetSync.NetMode.Client; clientState.SetValue(null, System.Enum.Parse(clientState.FieldType, "Connected"));
            var portal = actor.AddComponent<BattleMode7PortalController>();
            typeof(BattleMode7PortalController).GetField("teleportStarSprites", flags).SetValue(portal, new[] { sprite });
            var random = Random.state;
            var teleport = (System.Collections.IEnumerator)typeof(BattleMode7PortalController).GetMethod("TeleportRoutine", flags).Invoke(portal, new object[] { null, 0, 1 });
            Assert.IsFalse(teleport.MoveNext()); (teleport as System.IDisposable)?.Dispose();
            typeof(BattleMode7PortalController).GetMethod("SpawnTeleportStar", flags).Invoke(portal, new object[] { Vector2.zero });
            Assert.AreEqual(0, actor.transform.childCount); Assert.AreEqual(random, Random.state);
            var view = new PortalStarView { scene = actor.scene.name, path = portal.NetworkPortalPath, revision = 1,
                position = Vector3.up * 5, scale = Vector3.one * 0.3f, drift = Vector2.right, sprite = 0, sorting = 90 };
            var star = portal.CreateNetworkPortalStar(view, 0.1f); Assert.IsNotNull(star);
            Assert.AreEqual(view.position + Vector3.right * 0.1f, star.transform.position);
            Assert.AreEqual(0, star.GetComponents<MonoBehaviour>().Length); Assert.IsNull(star.GetComponent<Collider2D>());
            Assert.IsNull(star.GetComponent<Rigidbody2D>()); Assert.IsNull(star.GetComponent<NetworkIdentity>()); Assert.AreEqual(random, Random.state);
            typeof(BattleMode7PortalController).GetField("clearedPortalStarRevision", flags).SetValue(portal, 2u);
            var queued = (System.Collections.IEnumerator)typeof(BattleMode7PortalController).GetMethod("PresentNetworkPortalStar", flags).Invoke(portal, new object[] { view });
            Assert.IsFalse(queued.MoveNext()); (queued as System.IDisposable)?.Dispose();
        }
        finally
        {
            Object.DestroyImmediate(actor); Object.DestroyImmediate(sprite); Object.DestroyImmediate(texture);
            NetSync.Mode = oldMode; clientState.SetValue(null, oldClientState); Random.state = oldRandom;
        }
    }

    [Test]
    public void PortalStarViewViewIsRenderOnlyPreservesRngAndClearCancelsQueuedVisuals()
    {
        var oldMode = NetSync.Mode; var oldRandom = Random.state;
        var clientState = typeof(NetworkClient).GetField("connectState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var oldClientState = clientState.GetValue(null);
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene(); scene.name = "BattleMode_12";
        var actor = new GameObject("ApprovedCartStars"); SceneManager.MoveGameObjectToScene(actor, scene);
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var texture = new Texture2D(8, 8); var sprite = Sprite.Create(texture, new Rect(0, 0, 8, 8), Vector2.one * 0.5f);
        GameObject star = null;
        try
        {
            NetSync.Mode = NetSync.NetMode.Client; clientState.SetValue(null, System.Enum.Parse(clientState.FieldType, "Connected"));
            var controller = actor.AddComponent<BattleMode9MinecartController>();
            typeof(BattleMode9MinecartController).GetField("teleportStarSprites", flags).SetValue(controller, new[] { sprite });
            var view = new PortalStarView { scene = "BattleMode_12", path = controller.NetworkCartPath, revision = 1, time = NetworkTime.time - 0.1,
                position = new Vector3(34000, 34000), scale = Vector3.one * 0.25f, drift = Vector2.up * 0.5f,
                angle = 30, spin = 100, life = 0.32f, frameSeconds = 0.1f, sprite = 0, sorting = 90 };
            using (var writer = NetworkWriterPool.Get())
            { writer.Write(view); view = new NetworkReader(writer.ToArray()).Read<PortalStarView>(); }
            var random = Random.state; star = controller.CreateNetworkStarView(view, 0.1f);
            Assert.IsNotNull(star); Assert.AreEqual(view.position + (Vector3)view.drift * 0.1f, star.transform.position);
            Assert.AreEqual(0, star.GetComponents<MonoBehaviour>().Length); Assert.IsNull(star.GetComponent<Collider2D>()); Assert.IsNull(star.GetComponent<Rigidbody2D>()); Assert.IsNull(star.GetComponent<NetworkIdentity>());
            Assert.AreEqual(sprite, star.GetComponent<SpriteRenderer>().sprite); Assert.AreEqual(90, star.GetComponent<SpriteRenderer>().sortingOrder);
            Assert.AreEqual(random, Random.state); Assert.IsNull(controller.CurrentRider);
            Object.DestroyImmediate(star); star = null;
            var clear = view; clear.revision = 2; clear.clear = true; controller.ReceiveNetworkStar(clear);
            var queued = (System.Collections.IEnumerator)typeof(BattleMode9MinecartController).GetMethod("PresentNetworkStar", flags).Invoke(controller, new object[] { view });
            Assert.IsFalse(queued.MoveNext(), "An older scheduled particle must not reappear after the approved clear.");
            (queued as System.IDisposable)?.Dispose(); Assert.AreEqual(random, Random.state);
            view.sprite = 99; Assert.IsNull(controller.CreateNetworkStarView(view, 0));
        }
        finally
        {
            if (star != null) Object.DestroyImmediate(star);
            NetSync.Mode = oldMode; clientState.SetValue(null, oldClientState); Random.state = oldRandom;
            Object.DestroyImmediate(actor); Object.DestroyImmediate(sprite); Object.DestroyImmediate(texture);
            UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
        }
    }


    [TestCase(0.25f, 0.5f, 0f)]
    [TestCase(0.5f, 1f, 0f)]
    [TestCase(0.75f, 1f, 0.5f)]
    [TestCase(1f, 1f, 1f)]
    public void ApprovedCartSamplesPreserveRailCorners(float progress, float x, float y)
    {
        var from = new MinecartView { time = 10, position = Vector3.zero, direction = Vector2.right };
        var to = new MinecartView { time = 10.1, position = new Vector3(1, 1), direction = Vector2.up };
        var sampled = BattleMode9MinecartController.SampleNetworkCartPosition(from, to, 10 + 0.1 * progress, 1);
        Assert.AreEqual(x, sampled.x, 0.0001f); Assert.AreEqual(y, sampled.y, 0.0001f);
        to.position = new Vector3(10, 0); to.direction = Vector2.right;
        Assert.AreEqual(progress < 1 ? Vector3.zero : to.position, BattleMode9MinecartController.SampleNetworkCartPosition(from, to, 10 + 0.1 * progress, 1));
    }

    [Test]
    public void CartClientPresentsApprovedPoseWithoutBoardingCollisionOrInvulnerability()
    {
        var oldMode = NetSync.Mode;
        var clientState = typeof(NetworkClient).GetField("connectState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var oldClientState = clientState.GetValue(null);
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene(); scene.name = "BattleMode_9";
        var actor = new GameObject("ApprovedCart"); SceneManager.MoveGameObjectToScene(actor, scene);
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        try
        {
            NetSync.Mode = NetSync.NetMode.Client; clientState.SetValue(null, System.Enum.Parse(clientState.FieldType, "Connected"));
            var controller = actor.AddComponent<BattleMode9MinecartController>();
            var collider = actor.AddComponent<BoxCollider2D>(); collider.enabled = false;
            AnimatedSpriteRenderer Branch(string name)
            {
                var go = new GameObject(name); go.transform.SetParent(actor.transform, false);
                go.AddComponent<SpriteRenderer>();
                var animation = go.AddComponent<AnimatedSpriteRenderer>(); animation.animationSprite = new Sprite[3];
                typeof(BattleMode9MinecartController).GetField(name, flags).SetValue(controller, animation); return animation;
            }
            var down = Branch("down"); Branch("up"); Branch("left"); var right = Branch("right");
            var from = new MinecartView { scene = "BattleMode_9", path = controller.NetworkCartPath, revision = 1, time = 10, rider = 77,
                position = new Vector3(33000, 33000), scale = Vector3.one, direction = Vector2.right, renderer = 0,
                frame = new NetworkWorldMount.MountFrame { visible = true, idle = false, frame = 1, localPosition = Vector3.up * 0.5f, color = Color.white } };
            using (var writer = NetworkWriterPool.Get())
            { writer.Write(from); from = new NetworkReader(writer.ToArray()).Read<MinecartView>(); }
            var to = from; to.revision = 2; to.time = 10.1; to.position += Vector3.right; to.renderer = 3; to.frame.frame = 2;
            controller.ReceiveNetworkCart(from); controller.ReceiveNetworkCart(to); controller.ReceiveNetworkCart(from);
            var present = typeof(BattleMode9MinecartController).GetMethod("PresentNetworkCart", flags); present.Invoke(controller, new object[] { 10.05d });
            Assert.AreEqual(33000.5f, actor.transform.position.x, 0.001f); Assert.IsTrue(down.enabled); Assert.IsFalse(right.enabled); Assert.AreEqual(1, down.CurrentFrame);
            Assert.AreEqual(actor.transform.position + Vector3.up * 0.5f, down.transform.position);
            present.Invoke(controller, new object[] { 10.1d }); Assert.IsTrue(right.enabled); Assert.AreEqual(2, right.CurrentFrame);
            var sink = to; sink.revision = 3; sink.time = 10.2; sink.scale = new Vector3(1, 0.5f, 1); sink.frame.localPosition = Vector3.zero;
            controller.ReceiveNetworkCart(sink); present.Invoke(controller, new object[] { 10.15d });
            Assert.AreEqual(0.75f, right.transform.localScale.y, 0.001f); Assert.AreEqual(actor.transform.position.y + 0.25f, right.transform.position.y, 0.001f);

            var stop = sink; stop.revision = 4; stop.time = 10.3; stop.rider = 0; stop.renderer = 255;
            controller.ReceiveNetworkCart(stop); present.Invoke(controller, new object[] { 10.25d }); Assert.IsTrue(right.enabled, "The buffered final pose survives release of the authoritative occupant.");
            present.Invoke(controller, new object[] { 10.3d }); Assert.IsFalse(right.enabled); Assert.IsFalse(down.enabled);
            to.revision = 5; to.scene = "BattleMode_12"; controller.ReceiveNetworkCart(to); present.Invoke(controller, new object[] { 10.3d }); Assert.IsFalse(right.enabled);
            Assert.IsFalse(controller.RideActive); Assert.IsNull(controller.CurrentRider); Assert.IsFalse(collider.enabled); Assert.IsNull(actor.GetComponent<NetworkIdentity>());
            controller.OnSuddenDeathIndestructiblePlaced(Vector3Int.zero); Assert.IsFalse(controller.CartDestroyedBySuddenDeath, "A client visual cannot apply sudden-death gameplay.");
        }
        finally { NetSync.Mode = oldMode; clientState.SetValue(null, oldClientState); Object.DestroyImmediate(actor); UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
    }


    [TestCase(2f, 4f, true)]
    [TestCase(20f, 4f, false)]
    [TestCase(20f, 20f, true)]
    public void MountVisualHistoryInterpolatesApprovedOffsetsAndSeparatesMountGenerations(float offset, float limit, bool interpolates)
    {
        var oldMode = NetSync.Mode; var actor = new GameObject("MountObserverHistory"); actor.SetActive(false);
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var clientState = typeof(NetworkClient).GetField("connectState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var oldClientState = clientState.GetValue(null);
        try
        {
            NetSync.Mode = NetSync.NetMode.Client; clientState.SetValue(null, System.Enum.Parse(clientState.FieldType, "Connected"));
            actor.AddComponent<BoxCollider2D>(); var animation = actor.AddComponent<NetworkMountAnimation>();
            typeof(NetworkIdentity).GetMethod("InitializeNetworkBehaviours", flags).Invoke(actor.GetComponent<NetworkIdentity>(), null);
            typeof(NetworkMountAnimation).GetMethod("Awake", flags).Invoke(animation, null);
            var revision = typeof(NetworkMountAnimation).GetField("frameRevision", flags); revision.SetValue(animation, 1u);
            typeof(NetworkMountAnimation).GetField("frameType", flags).SetValue(animation, MountedType.Pink);
            var sample = new NetworkWorldMount.MountFrame { visible = true, idle = false, color = Color.white, frame = 1,
                pose = new NetworkVisualPose { valid = true, offsetLimit = limit, position = Vector3.down * 0.2f, scale = new Vector3(1, 0.2f, 1), rotation = Quaternion.identity, mask = (byte)SpriteMaskInteraction.VisibleInsideMask } };
            animation.frames.Add(sample);
            var receive = typeof(NetworkMountAnimation).GetMethod("OnMountVisualTime", flags); receive.Invoke(animation, new object[] { 0d, 10d });
            sample.localPosition = Vector3.up * offset; sample.frame = 3; sample.flipX = true; sample.color = Color.red;
            sample.pose = new NetworkVisualPose { valid = true, offsetLimit = limit, position = Vector3.up * (offset - 2), scale = Vector3.one, rotation = Quaternion.identity };
            animation.frames[0] = sample; receive.Invoke(animation, new object[] { 10d, 10.2d });
            Assert.IsTrue(animation.TrySampleMountFrame(10.1, MountedType.Pink, 0, out var displayed));
            Assert.AreEqual(interpolates ? offset * .5f : 0, displayed.localPosition.y, 0.001f); Assert.AreEqual(1, displayed.frame); Assert.IsFalse(displayed.flipX); Assert.AreEqual(Color.white, displayed.color);
            Assert.AreEqual(interpolates ? .6f : .2f, displayed.pose.scale.y, 0.001f); Assert.AreEqual(interpolates ? (offset - 2.2f) * .5f : -.2f, displayed.pose.position.y, 0.001f);
            Assert.AreEqual((byte)SpriteMaskInteraction.VisibleInsideMask, displayed.pose.mask);
            Assert.IsFalse(animation.TrySampleMountFrame(10.1, MountedType.Yellow, 0, out _));
            revision.SetValue(animation, 2u); Assert.IsFalse(animation.TrySampleMountFrame(10.1, MountedType.Pink, 0, out _), "A new mount of the same type must not inherit old poses.");
            sample.localPosition = Vector3.up * 5; animation.frames[0] = sample; receive.Invoke(animation, new object[] { 10.2d, 11d });
            Assert.IsTrue(animation.TrySampleMountFrame(11, MountedType.Pink, 0, out displayed)); Assert.AreEqual(5, displayed.localPosition.y);
            for (int i = 1; i <= 70; i++) receive.Invoke(animation, new object[] { 0d, 11d + i * 0.1 });
            var history = (System.Collections.IList)typeof(NetworkMountAnimation).GetField("visualHistory", flags).GetValue(animation); Assert.AreEqual(64, history.Count);
            Assert.IsNull(actor.GetComponent<PlayerMountCompanion>().GetMountedLouieObject(), "Buffered visuals cannot grant a mount.");
        }
        finally { NetSync.Mode = oldMode; clientState.SetValue(null, oldClientState); Object.DestroyImmediate(actor); }
    }

    [TestCase(2f, 4f, true)]
    [TestCase(20f, 4f, false)]
    [TestCase(20f, 20f, true)]
    public void PlayerObserverInterpolatesApprovedOffsetWithoutAdvancingDiscretePoseEarly(float offset, float limit, bool interpolates)
    {
        var actor = new GameObject("PlayerObserverOffset"); actor.SetActive(false);
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var clientState = typeof(NetworkClient).GetField("connectState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var oldClientState = clientState.GetValue(null);
        try
        {
            clientState.SetValue(null, System.Enum.Parse(clientState.FieldType, "Connected"));
            actor.AddComponent<BoxCollider2D>(); var animation = actor.AddComponent<NetworkPlayerAnimation>();
            var child = new GameObject("Head"); child.transform.SetParent(actor.transform); child.AddComponent<SpriteRenderer>(); var renderer = child.AddComponent<AnimatedSpriteRenderer>(); renderer.animationSprite = new Sprite[4];
            typeof(NetworkIdentity).GetMethod("InitializeNetworkBehaviours", flags).Invoke(actor.GetComponent<NetworkIdentity>(), null);
            typeof(NetworkPlayerAnimation).GetMethod("Awake", flags).Invoke(animation, null);
            typeof(NetworkPlayerAnimation).GetField("activeIndex", flags).SetValue(animation, (byte)0);
            typeof(NetworkPlayerAnimation).GetField("activeIdle", flags).SetValue(animation, false);
            typeof(NetworkPlayerAnimation).GetField("activeFrame", flags).SetValue(animation, (byte)1);
            typeof(NetworkPlayerAnimation).GetField("activeSortingOrder", flags).SetValue(animation, 7);
            var receive = typeof(NetworkPlayerAnimation).GetMethod("OnVisualTime", flags); double now = NetworkTime.time;
            typeof(NetworkPlayerAnimation).GetField("activePose", flags).SetValue(animation,
                new NetworkVisualPose { valid = true, offsetLimit = limit, position = Vector3.down * 0.2f, scale = new Vector3(1, 0.2f, 1), rotation = Quaternion.identity, mask = (byte)SpriteMaskInteraction.VisibleInsideMask });
            receive.Invoke(animation, new object[] { 0d, now - 0.1 });
            typeof(NetworkPlayerAnimation).GetField("activePose", flags).SetValue(animation,
                new NetworkVisualPose { valid = true, offsetLimit = limit, position = Vector3.up * (offset - 2), scale = Vector3.one, rotation = Quaternion.identity });
            typeof(NetworkPlayerAnimation).GetField("activeOffset", flags).SetValue(animation, Vector3.up * offset);
            typeof(NetworkPlayerAnimation).GetField("activeFrame", flags).SetValue(animation, (byte)3);
            typeof(NetworkPlayerAnimation).GetField("activeSortingOrder", flags).SetValue(animation, 19);
            receive.Invoke(animation, new object[] { now - 0.1, now + 0.1 });
            typeof(NetworkPlayerAnimation).GetMethod("ClientApply", flags).Invoke(animation, null);
            Assert.AreEqual(interpolates ? offset * .5f : 0, renderer.RuntimeBaseLocalOffset.y, 0.001f); Assert.AreEqual(1, renderer.CurrentFrame);
            Assert.AreEqual(7, renderer.GetComponent<SpriteRenderer>().sortingOrder);
            Assert.AreEqual(interpolates ? .6f : .2f, child.transform.localScale.y, 0.001f); Assert.AreEqual(interpolates ? (offset - 2.2f) * .5f : -.2f, child.transform.localPosition.y, 0.001f);
            Assert.AreEqual(SpriteMaskInteraction.VisibleInsideMask, child.GetComponent<SpriteRenderer>().maskInteraction);
            renderer.enabled = true; child.GetComponent<SpriteRenderer>().enabled = false;
            typeof(NetworkPlayerAnimation).GetMethod("SampleOwned", flags).Invoke(animation, null);
            Assert.AreEqual((byte)255, typeof(NetworkPlayerAnimation).GetField("activeIndex", flags).GetValue(animation), "A renderer hidden between portals must not remain visible on peers.");
        }
        finally { clientState.SetValue(null, oldClientState); Object.DestroyImmediate(actor); }
    }


    [TestCase(typeof(BattleMode9MinecartController), "RideRoutine")]
    [TestCase(typeof(SpringLauncher), "SpringRoutine")]
    [TestCase(typeof(CannonLauncher), "FireRoutine")]
    [TestCase(typeof(CannonLauncher), "LaunchPlayerArc")]
    [TestCase(typeof(BattleMode4SpringTileController), "SpringJumpRoutine")]
    [TestCase(typeof(BattleMode11ImpulseRopeController), "PlayerPreparingImpulseRoutine")]
    public void ExternalLaunchRoutinesCannotStartGameplayOnPureClient(System.Type type, string method)
    {
        var oldMode = NetSync.Mode; var actor = new GameObject("ClientLaunchGuardActor"); actor.SetActive(false);
        var mechanism = new GameObject("ClientLaunchGuardMechanism"); mechanism.SetActive(false);
        try
        {
            NetSync.Mode = NetSync.NetMode.Client; actor.AddComponent<BoxCollider2D>(); var movement = actor.AddComponent<MovementController>();
            mechanism.AddComponent<BoxCollider2D>();
            var controller = mechanism.AddComponent(type);
            object[] args = method == "SpringJumpRoutine" ? new object[] { movement, Vector3Int.zero, Vector3Int.one } :
                method == "PlayerPreparingImpulseRoutine" ? new object[] { movement, Vector3Int.zero, Vector2.right } :
                method == "LaunchPlayerArc" ? new object[] { movement, Vector2.right } : new object[] { movement };
            var routine = (System.Collections.IEnumerator)type.GetMethod(method, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(controller, args);
            Assert.IsFalse(routine.MoveNext()); Assert.IsFalse(movement.InputLocked); Assert.IsFalse(movement.explosionInvulnerable);
            Assert.IsFalse(movement.ExternalMovementOverride); Assert.AreEqual(Vector3.zero, actor.transform.position);
            (routine as System.IDisposable)?.Dispose();
        }
        finally { NetSync.Mode = oldMode; Object.DestroyImmediate(mechanism); Object.DestroyImmediate(actor); }
    }

    [Test]
    public void FallingBombClientDisplaysApprovedWarningRejectsStaleEventsAndNeverChoosesOrSpawns()
    {
        var oldMode = NetSync.Mode;
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        scene.name = "BattleMode_2";
        var clientState = typeof(NetworkClient).GetField("connectState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var oldClientState = clientState.GetValue(null); var oldRandom = Random.state;
        var texture = new Texture2D(8, 8); var sprite = Sprite.Create(texture, new Rect(0, 0, 8, 8), Vector2.one * 0.5f);
        GameObject actor = null;
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        try
        {
            NetSync.Mode = NetSync.NetMode.Client; clientState.SetValue(null, System.Enum.Parse(clientState.FieldType, "Connected"));
            actor = new GameObject("ApprovedFallingBombTarget");
            SceneManager.MoveGameObjectToScene(actor, scene);
            var controller = actor.AddComponent<BattleMode2FallingBombController>();
            typeof(BattleMode2FallingBombController).GetField("targetSprite", flags).SetValue(controller, sprite);
            var before = Random.state;
            Assert.AreEqual(Vector3Int.zero, typeof(BattleMode2FallingBombController).GetMethod("PickTargetCell", flags).Invoke(controller, null));
            Assert.AreEqual(before, Random.state, "A client warning must not consume gameplay RNG.");
            Assert.IsNull(typeof(BattleMode2FallingBombController).GetMethod("LaunchBomb", flags).Invoke(controller, new object[] { Vector2.one }));
            var message = new FallingBombTarget { scene = "BattleMode_2", revision = 1, time = NetworkBombFx.KickServerNow,
                position = new Vector2(3, 4), remaining = 2, fuse = 3, radius = 4, active = true, paused = true };
            using (var writer = NetworkWriterPool.Get())
            { writer.Write(message); message = new NetworkReader(writer.ToArray()).Read<FallingBombTarget>(); }
            controller.ReceiveNetworkTarget(message);
            Assert.IsTrue(controller.TryGetActiveTargetWarning(out var position, out var radius, out var seconds));
            Assert.AreEqual(message.position, position); Assert.AreEqual(4, radius); Assert.AreEqual(5, seconds, 0.001f);
            var renderer = (SpriteRenderer)typeof(BattleMode2FallingBombController).GetField("targetRenderer", flags).GetValue(controller);
            Assert.IsNotNull(renderer); Assert.IsTrue(renderer.enabled); Assert.AreEqual((Vector3)message.position, renderer.transform.position);
            typeof(BattleMode2FallingBombController).GetMethod("Update", flags).Invoke(controller, null);
            controller.TryGetActiveTargetWarning(out _, out _, out seconds); Assert.AreEqual(5, seconds, 0.001f, "An approved pause freezes the display countdown.");
            var stop = message; stop.revision = 2; stop.time += 0.1; stop.active = false;
            controller.ReceiveNetworkTarget(stop); controller.ReceiveNetworkTarget(message);
            Assert.IsFalse(controller.TryGetActiveTargetWarning(out _, out _, out _)); Assert.IsFalse(renderer.enabled);
            message.revision = 3; message.time += 1; message.scene = "BattleMode_1"; controller.ReceiveNetworkTarget(message);
            Assert.IsFalse(renderer.enabled, "A previous or different arena cannot resurrect the warning.");
            Assert.AreEqual(before, Random.state); Assert.IsNull(actor.GetComponent<NetworkIdentity>(), "A warning is presentation, not a client gameplay spawn.");
        }
        finally
        {
            NetSync.Mode = oldMode; clientState.SetValue(null, oldClientState); Random.state = oldRandom;
            if (actor != null)
            {
                var controller = actor.GetComponent<BattleMode2FallingBombController>();
                if (controller != null) typeof(BattleMode2FallingBombController).GetField("targetRenderer", flags).SetValue(controller, null);
                Object.DestroyImmediate(actor);
            }
            Object.DestroyImmediate(sprite); Object.DestroyImmediate(texture);
            UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
        }
    }


    [TestCase(0.1, 1f, 0.5, 0.5f)]
    [TestCase(0.4, 1f, 0.5, 0f)]
    [TestCase(0.1, 10f, 0.5, 0f)]
    [TestCase(0.1, 10f, 1d, 10f)]
    public void AuthorityMotionSamplesOnlyApprovedSegmentsAndCutsDiscontinuities(double interval, float distance, double progress, float expectedX)
    {
        var from = new TransformSnapshot(10, 10, Vector3.zero, Quaternion.identity, Vector3.one);
        var to = new TransformSnapshot(10 + interval, 10 + interval, Vector3.right * distance, Quaternion.identity, Vector3.one);
        Assert.AreEqual(expectedX, NetworkPlayerInput.SampleApprovedAuthoritySegment(from, to, progress, 1.5f).x, 0.0001f);
    }

    [Test]
    public void AuthorityMotionScopesAreOfflineNoOpsAndOldDisposalCannotEndNewMotion()
    {
        var oldMode = NetSync.Mode; var actor = new GameObject("AuthorityMotionScopes"); actor.SetActive(false);
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        NetworkIdentity identity = null;
        try
        {
            actor.AddComponent<BoxCollider2D>(); actor.AddComponent<NetworkBombFx>();
            var input = actor.AddComponent<NetworkPlayerInput>(); identity = actor.GetComponent<NetworkIdentity>();
            typeof(NetworkIdentity).GetMethod("InitializeNetworkBehaviours", flags).Invoke(identity, null);
            typeof(NetworkIdentity).GetProperty("isServer").SetValue(identity, true); typeof(NetworkIdentity).GetProperty("netId").SetValue(identity, 129u);
            var movement = actor.GetComponent<MovementController>(); var token = typeof(NetworkPlayerInput).GetField("authorityMotionToken", flags);
            NetSync.Mode = NetSync.NetMode.Offline;
            using (NetworkPlayerInput.PresentAuthorityMotion(movement)) Assert.AreEqual(0u, token.GetValue(input));
            NetSync.Mode = NetSync.NetMode.Host;
            var first = NetworkPlayerInput.PresentAuthorityMotion(movement, 20); uint firstToken = (uint)token.GetValue(input); Assert.AreNotEqual(0u, firstToken);
            Assert.AreEqual(20, input.AuthorityVisualOffsetLimit);
            var second = NetworkPlayerInput.PresentAuthorityMotion(movement, 10); uint secondToken = (uint)token.GetValue(input); Assert.AreNotEqual(firstToken, secondToken);
            first.Dispose(); Assert.AreEqual(secondToken, token.GetValue(input)); Assert.AreEqual(10, input.AuthorityVisualOffsetLimit);
            second.Dispose(); Assert.AreEqual(0u, token.GetValue(input)); Assert.AreEqual(4, input.AuthorityVisualOffsetLimit); Assert.IsFalse(movement.InputLocked);
            using (NetworkPlayerInput.PresentAuthorityMotion(movement, float.NaN)) Assert.AreEqual(4, input.AuthorityVisualOffsetLimit);
            using (NetworkPlayerInput.PresentAuthorityMotion(movement, 300)) Assert.AreEqual(128, input.AuthorityVisualOffsetLimit);
            Assert.IsFalse(movement.explosionInvulnerable);
        }
        finally { if (identity != null) typeof(NetworkIdentity).GetProperty("isServer").SetValue(identity, false); NetSync.Mode = oldMode; Object.DestroyImmediate(actor); }
    }

    [Test]
    public void OwnerAuthorityMotionInterpolatesWithoutPacketSnapsAndYieldsToPredictionOrBounce()
    {
        var oldMode = NetSync.Mode; var actor = new GameObject("AuthorityMotionOwner"); actor.SetActive(false);
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var clientState = typeof(NetworkClient).GetField("connectState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var oldClientState = clientState.GetValue(null);
        var paused = typeof(GamePauseController).GetField("<IsPaused>k__BackingField", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        bool wasPaused = (bool)paused.GetValue(null);
        try
        {
            NetSync.Mode = NetSync.NetMode.Client; clientState.SetValue(null, System.Enum.Parse(clientState.FieldType, "Connected")); paused.SetValue(null, false);
            actor.AddComponent<BoxCollider2D>(); var fx = actor.AddComponent<NetworkBombFx>(); var input = actor.AddComponent<NetworkPlayerInput>();
            var sync = actor.AddComponent<NetworkTransformReliable>(); sync.enabled = false; sync.syncDirection = SyncDirection.ServerToClient;
            var identity = actor.GetComponent<NetworkIdentity>(); typeof(NetworkIdentity).GetMethod("InitializeNetworkBehaviours", flags).Invoke(identity, null);
            typeof(NetworkIdentity).GetProperty("isLocalPlayer").SetValue(identity, true);
            typeof(NetworkPlayerInput).GetMethod("Awake", flags).Invoke(input, null);
            var movement = actor.GetComponent<MovementController>(); var body = actor.GetComponent<Rigidbody2D>();
            typeof(MovementController).GetProperty(nameof(MovementController.Rigidbody)).SetValue(movement, body);
            var head = new GameObject("LocalHead"); head.transform.SetParent(actor.transform, false); head.AddComponent<SpriteRenderer>();
            var localAnimation = head.AddComponent<AnimatedSpriteRenderer>(); localAnimation.animationSprite = new Sprite[4];
            var animation = actor.AddComponent<NetworkPlayerAnimation>(); typeof(NetworkPlayerAnimation).GetMethod("Awake", flags).Invoke(animation, null);
            typeof(NetworkIdentity).GetMethod("InitializeNetworkBehaviours", flags).Invoke(identity, null);
            Vector3 origin = new(32000, 32000); actor.transform.position = origin; body.position = origin;
            double now = NetworkTime.time;
            sync.clientSnapshots.Add(now - 0.1, new TransformSnapshot(now - 0.1, now - 0.1, origin, Quaternion.identity, Vector3.one));
            sync.clientSnapshots.Add(now + 0.1, new TransformSnapshot(now + 0.1, now + 0.1, origin + Vector3.right, Quaternion.identity, Vector3.one));
            var state = movement.CaptureNetworkMovementState(); state.serverTime = now + 0.1; state.position = origin + Vector3.right;
            state.canPredict = false; state.continuousPresentation = true;
            using (var writer = NetworkWriterPool.Get())
            { writer.Write(state); Assert.IsTrue(new NetworkReader(writer.ToArray()).Read<MovementController.NetworkMovementState>().continuousPresentation); }
            input.ReceiveMovementState(1, state, default, default);
            Assert.IsTrue(input.OwnerUsesAuthorityPresentation); Assert.AreEqual(origin, actor.transform.position, "A reconciliation packet must not snap the displayed flight.");
            var present = typeof(NetworkPlayerInput).GetMethod("UpdateOwnerAuthorityPresentation", flags); present.Invoke(input, null);
            Assert.AreEqual(origin.x + 0.5f, actor.transform.position.x, 0.01f); Assert.AreEqual((Vector2)actor.transform.position, body.position);
            Assert.IsFalse(movement.PredictLocally); Assert.IsFalse(sync.enabled, "Interpolation consumes the existing buffer without resetting its delta baseline.");
            typeof(NetworkPlayerAnimation).GetMethod("OnVisualTime", flags).Invoke(animation, new object[] { 0d, now });
            var frames = (System.Collections.IList)typeof(NetworkPlayerAnimation).GetField("visualFrames", flags).GetValue(animation); Assert.AreEqual(1, frames.Count);
            var displayed = actor.transform.position; paused.SetValue(null, true); input.ReceiveMovementState(2, state, default, default); present.Invoke(input, null);
            Assert.AreEqual(displayed, actor.transform.position); paused.SetValue(null, false);
            state.canPredict = true; state.continuousPresentation = false; input.ReceiveMovementState(3, state, default, default);
            Assert.IsFalse(input.OwnerUsesAuthorityPresentation); Assert.AreEqual(0, frames.Count); Assert.AreEqual(state.position, body.position);
            typeof(NetworkIdentity).GetProperty("isClient").SetValue(identity, true);
            localAnimation.CurrentFrame = 2;
            typeof(NetworkPlayerAnimation).GetField("activeIndex", flags).SetValue(animation, (byte)0);
            typeof(NetworkPlayerAnimation).GetField("activeFrame", flags).SetValue(animation, (byte)1);
            typeof(NetworkPlayerAnimation).GetField("activeSortingOrder", flags).SetValue(animation, 7);
            typeof(NetworkPlayerAnimation).GetMethod("LateUpdate", flags).Invoke(animation, null);
            Assert.AreEqual(7, head.GetComponent<SpriteRenderer>().sortingOrder); Assert.AreEqual(2, localAnimation.CurrentFrame, "Confirmed sorting must not replace a predicted walking frame.");
            typeof(NetworkPlayerAnimation).GetField("activeSortingOrder", flags).SetValue(animation, 2);
            typeof(NetworkPlayerAnimation).GetMethod("LateUpdate", flags).Invoke(animation, null); Assert.AreEqual(2, head.GetComponent<SpriteRenderer>().sortingOrder, "Leaving a cart must not leave its head ordering behind.");
            fx.ReceivePlayerBounce(new NetworkBombFx.PlayerBounceMotion { revision = 1, first = true, moving = true, start = origin, end = origin + Vector3.up,
                duration = 0.2f, curveDuration = 0.2f, height = 0.5f });
            state.canPredict = false; state.continuousPresentation = true; input.ReceiveMovementState(4, state, default, default);
            Assert.IsFalse(input.OwnerUsesAuthorityPresentation, "A specialized approved presenter keeps ownership of its arc.");
            Assert.AreEqual(origin, actor.transform.position); Assert.IsFalse(movement.explosionInvulnerable);
        }
        finally { paused.SetValue(null, wasPaused); NetSync.Mode = oldMode; clientState.SetValue(null, oldClientState); Object.DestroyImmediate(actor); }
    }


    [Test]
    public void EggQueuePresentationUsesApprovedTimelineWithoutMovingOrCollecting()
    {
        var oldMode = NetSync.Mode;
        var actor = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/EggFollower.prefab"));
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var clientState = typeof(NetworkClient).GetField("connectState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var oldClientState = clientState.GetValue(null);
        try
        {
            NetSync.Mode = NetSync.NetMode.Client; clientState.SetValue(null, System.Enum.Parse(clientState.FieldType, "Connected"));
            var network = actor.GetComponent<NetworkEggFollower>();
            typeof(NetworkIdentity).GetMethod("InitializeNetworkBehaviours", flags).Invoke(actor.GetComponent<NetworkIdentity>(), null);
            typeof(NetworkEggFollower).GetMethod("Awake", flags).Invoke(network, null);
            network.OnStartClient(); actor.transform.position = new Vector3(31000, 31000);
            var directional = actor.GetComponentInChildren<EggFollowerDirectionalVisual>();
            var sample = new NetworkEggFollower.QueuePresentation { valid = true, time = 10, facing = Vector2.right, visible = true, idle = false, sortingOrder = 7 };
            using (var writer = NetworkWriterPool.Get())
            {
                writer.Write(sample); var reader = new NetworkReader(writer.ToArray());
                var copy = reader.Read<NetworkEggFollower.QueuePresentation>();
                Assert.AreEqual(sample.facing, copy.facing); Assert.AreEqual(sample.time, copy.time);
                Assert.AreEqual(sample.visible, copy.visible); Assert.AreEqual(sample.sortingOrder, copy.sortingOrder);
            }
            network.ReceiveQueuePresentation(sample);
            var positionSync = actor.GetComponent<NetworkTransformReliable>();
            double adjustment = NetworkClient.sendInterval * (positionSync.sendIntervalMultiplier - 1 +
                (positionSync.timelineOffset ? positionSync.sendIntervalMultiplier : 0));
            var pending = (List<NetworkEggFollower.QueuePresentation>)typeof(NetworkEggFollower).GetField("pendingPresentation", flags).GetValue(network);
            Assert.AreEqual(10 + adjustment, pending[0].time, 0.000001, "Pose must use the NetworkTransform timeline offset.");
            sample.time = 11; sample.facing = Vector2.up; sample.idle = true; sample.visible = false; sample.sortingOrder = 9;
            network.ReceiveQueuePresentation(sample);
            var present = typeof(NetworkEggFollower).GetMethod("ApplyQueuePresentation", flags);
            present.Invoke(network, new object[] { 10.5 + adjustment });
            Assert.AreEqual(Vector2.right, directional.facing); Assert.IsFalse(directional.IsIdle);
            foreach (var sprite in actor.GetComponentsInChildren<SpriteRenderer>(true))
            { Assert.IsFalse(sprite.forceRenderingOff); Assert.AreEqual(7, sprite.sortingOrder); }
            present.Invoke(network, new object[] { 11d + adjustment });
            Assert.AreEqual(Vector2.up, directional.facing); Assert.IsTrue(directional.IsIdle);
            foreach (var sprite in actor.GetComponentsInChildren<SpriteRenderer>(true))
            { Assert.IsTrue(sprite.forceRenderingOff); Assert.AreEqual(9, sprite.sortingOrder); }
            sample.time = 10.9; sample.visible = true; network.ReceiveQueuePresentation(sample);
            present.Invoke(network, new object[] { 12d + adjustment });
            foreach (var sprite in actor.GetComponentsInChildren<SpriteRenderer>(true)) Assert.IsTrue(sprite.forceRenderingOff, "Stale visibility must not resurrect an egg.");
            sample.time = 13; sample.visible = true; sample.idle = false; network.ReceiveQueuePresentation(sample);
            present.Invoke(network, new object[] { 13d + adjustment }); Assert.IsFalse(directional.IsIdle);
            Assert.AreEqual(new Vector3(31000, 31000), actor.transform.position);
            foreach (var collider in actor.GetComponentsInChildren<Collider2D>(true)) Assert.IsFalse(collider.enabled);
            Assert.IsNull(actor.GetComponent<MountEggQueue>(), "Presentation must not create queue ownership.");
            typeof(NetworkEggFollower).GetField("destroyKind", flags).SetValue(network, (byte)1);
            sample.time = 14; sample.visible = false; network.ReceiveQueuePresentation(sample);
            present.Invoke(network, new object[] { 14d + adjustment });
            foreach (var sprite in actor.GetComponentsInChildren<SpriteRenderer>(true)) Assert.IsFalse(sprite.forceRenderingOff, "The approved destroy visual owns renderers after destruction starts.");
        }
        finally { NetSync.Mode = oldMode; clientState.SetValue(null, oldClientState); Object.DestroyImmediate(actor); }
    }

    [Test]
    public void EggQueueHiddenIdentityRemainsSpawnableAndRestoresExactHitboxState()
    {
        var oldMode = NetSync.Mode;
        var actor = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/EggFollower.prefab"));
        var owner = new GameObject("EggVisibilityQueue"); owner.SetActive(false);
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        NetworkIdentity identity = null;
        try
        {
            identity = actor.GetComponent<NetworkIdentity>(); var network = actor.GetComponent<NetworkEggFollower>();
            typeof(NetworkIdentity).GetMethod("InitializeNetworkBehaviours", flags).Invoke(identity, null);
            typeof(NetworkIdentity).GetProperty("netId").SetValue(identity, 126u); typeof(NetworkIdentity).GetProperty("isServer").SetValue(identity, true);
            var original = actor.GetComponent<Collider2D>(); original.enabled = true;
            var disabledObject = new GameObject("AlreadyDisabledHitbox"); disabledObject.transform.SetParent(actor.transform);
            var disabled = disabledObject.AddComponent<BoxCollider2D>(); disabled.enabled = false;
            var queue = owner.AddComponent<MountEggQueue>();
            var entryType = typeof(MountEggQueue).GetNestedType("EggEntry", System.Reflection.BindingFlags.NonPublic);
            var entry = System.Activator.CreateInstance(entryType);
            entryType.GetField("rootTr").SetValue(entry, actor.transform);
            entryType.GetField("directional").SetValue(entry, actor.GetComponentInChildren<EggFollowerDirectionalVisual>());
            var eggs = (System.Collections.IList)typeof(MountEggQueue).GetField("_eggs", flags).GetValue(queue); eggs.Add(entry);
            NetSync.Mode = NetSync.NetMode.Host; queue.ForceVisible(false);
            Assert.IsTrue(actor.activeSelf, "Mirror must still spawn the hidden identity for a late observer.");
            Assert.IsFalse(original.enabled); Assert.IsFalse(disabled.enabled);
            foreach (var sprite in actor.GetComponentsInChildren<SpriteRenderer>(true)) Assert.IsTrue(sprite.forceRenderingOff);
            var state = (NetworkEggFollower.QueuePresentation)typeof(NetworkEggFollower).GetField("queuePresentation", flags).GetValue(network);
            Assert.IsTrue(state.valid); Assert.IsFalse(state.visible);
            queue.ForceVisible(true); Assert.IsTrue(original.enabled); Assert.IsFalse(disabled.enabled);
            state = (NetworkEggFollower.QueuePresentation)typeof(NetworkEggFollower).GetField("queuePresentation", flags).GetValue(network); Assert.IsTrue(state.visible);
            NetSync.Mode = NetSync.NetMode.Offline; queue.ForceVisible(false); Assert.IsFalse(actor.activeSelf, "Offline keeps its native visibility lifecycle.");
            queue.ForceVisible(true); Assert.IsTrue(actor.activeSelf);
            eggs.Clear();
        }
        finally
        {
            if (identity != null) typeof(NetworkIdentity).GetProperty("isServer").SetValue(identity, false);
            NetSync.Mode = oldMode; Object.DestroyImmediate(owner); Object.DestroyImmediate(actor);
        }
    }


    [Test]
    public void RejectedAbilityReplaysOriginalWalkingIntentInsteadOfStoredPoseZeros()
    {
        var oldMode = NetSync.Mode; var actor = new GameObject("RejectedPoseMovementReplay"); actor.SetActive(false);
        var mount = new GameObject("RejectedPoseMount"); mount.transform.SetParent(actor.transform, false);
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var clientState = typeof(NetworkClient).GetField("connectState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var oldClientState = clientState.GetValue(null); NetworkBombFx fx = null;
        try
        {
            NetSync.Mode = NetSync.NetMode.Client; clientState.SetValue(null, System.Enum.Parse(clientState.FieldType, "Connected"));
            actor.tag = "Player"; actor.AddComponent<BoxCollider2D>(); var identity = actor.AddComponent<NetworkIdentity>(); fx = actor.AddComponent<NetworkBombFx>();
            var input = actor.AddComponent<NetworkPlayerInput>(); typeof(NetworkIdentity).GetMethod("InitializeNetworkBehaviours", flags).Invoke(identity, null);
            typeof(NetworkIdentity).GetProperty("isLocalPlayer").SetValue(identity, true);
            typeof(NetworkPlayerInput).GetMethod("Awake", flags).Invoke(input, null);
            var movement = actor.GetComponent<MovementController>(); var body = actor.GetComponent<Rigidbody2D>(); body.bodyType = RigidbodyType2D.Kinematic;
            typeof(MovementController).GetProperty(nameof(MovementController.Rigidbody)).SetValue(movement, body);
            var companion = actor.GetComponent<PlayerMountCompanion>() ?? actor.AddComponent<PlayerMountCompanion>();
            typeof(PlayerMountCompanion).GetField("currentLouie", flags).SetValue(companion, mount);
            typeof(PlayerMountCompanion).GetField("mountedType", flags).SetValue(companion, MountedType.Red);
            actor.SetActive(true); movement.SetNetworkTickDriven(true); Vector2 origin = new(31000, 31000);
            var state = new MovementController.NetworkMovementState { position = origin, mount = MountedType.Red, facing = Vector2.right,
                worldSpeed = 4, animationSpeed = PlayerPersistentStats.BaseSpeedNormal, canPredict = true, serverTime = NetworkTime.localTime };
            movement.RestoreNetworkMovementState(state); movement.SetPredictLocally(true);
            Assert.IsTrue(fx.TryPredictRedPunch(8, 10));
            var record = typeof(NetworkPlayerInput).GetMethod("RecordLocalFrame", flags);
            for (uint tick = 10; tick <= 13; tick++)
            {
                var command = new NetworkInputFrame { tick = tick, held = 8, pressed = tick == 10 ? 64 : 0 };
                var motor = (NetworkInputFrame)record.Invoke(input, new object[] { command }); Assert.AreEqual(0, motor.held & 15);
            }
            var history = (NetworkInputHistory)typeof(NetworkPlayerInput).GetField("pending", flags).GetValue(input);
            Assert.AreEqual(8, history[1].held, "A temporary presentation lock cannot erase the command sent to authority.");
            input.ReceiveMovementState(9, state, default, default); Assert.AreEqual(origin, body.position, "An unprocessed gesture still masks walking locally.");
            input.ReceiveMovementState(10, state, default, default);
            Assert.IsFalse(fx.RedPunchPoseActive); Assert.Greater(body.position.x, origin.x + 0.15f, "After rejection, pending walking inputs must be replayed immediately.");
            Assert.AreEqual(3, history.Count); Assert.AreEqual(8, history[0].held);
        }
        finally
        {
            if (fx != null) typeof(NetworkBombFx).GetMethod("ClearRedPunch", flags).Invoke(fx, new object[] { "test_cleanup" });
            NetSync.Mode = oldMode; clientState.SetValue(null, oldClientState); Object.DestroyImmediate(actor);
        }
    }

    [Test]
    public void RidingOwnerPredictsDismountRejectsAndHandsLandingToQueuedBounce()
    {
        var oldMode = NetSync.Mode; var actor = new GameObject("RidingOwner"); actor.SetActive(false);
        var mount = new GameObject("RidingOwnerMount"); mount.transform.SetParent(actor.transform, false);
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var clientState = typeof(NetworkClient).GetField("connectState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var oldClientState = clientState.GetValue(null); NetworkBombFx fx = null;
        var paused = typeof(GamePauseController).GetProperty(nameof(GamePauseController.IsPaused)); bool wasPaused = GamePauseController.IsPaused;
        AnimatedSpriteRenderer Animation(string name) { var go = new GameObject(name); go.transform.SetParent(actor.transform, false); return go.AddComponent<AnimatedSpriteRenderer>(); }
        try
        {
            NetSync.Mode = NetSync.NetMode.Client; clientState.SetValue(null, System.Enum.Parse(clientState.FieldType, "Connected"));
            actor.tag = "Player"; var collider = actor.AddComponent<BoxCollider2D>(); var identity = actor.AddComponent<NetworkIdentity>(); fx = actor.AddComponent<NetworkBombFx>();
            typeof(NetworkIdentity).GetMethod("InitializeNetworkBehaviours", flags).Invoke(identity, null);
            typeof(NetworkIdentity).GetProperty("isLocalPlayer").SetValue(identity, true);
            var movement = actor.GetComponent<MovementController>(); var body = actor.GetComponent<Rigidbody2D>(); body.bodyType = RigidbodyType2D.Kinematic;
            typeof(MovementController).GetProperty(nameof(MovementController.Rigidbody)).SetValue(movement, body);
            var companion = actor.AddComponent<PlayerMountCompanion>(); typeof(PlayerMountCompanion).GetField("currentLouie", flags).SetValue(companion, mount);
            typeof(PlayerMountCompanion).GetField("mountedType", flags).SetValue(companion, MountedType.Red);
            var rider = actor.AddComponent<PlayerRidingController>(); rider.mountAscendDown = Animation("RidingOwnerAscend"); rider.mountDescendDown = Animation("RidingOwnerDescend");
            actor.SetActive(true); movement.SetNetworkTickDriven(true); Vector2 origin = new(28000, 28000);
            var state = new MovementController.NetworkMovementState { position = origin, mount = MountedType.Red, facing = Vector2.down,
                worldSpeed = 4, animationSpeed = PlayerPersistentStats.BaseSpeedNormal, canPredict = true, serverTime = NetworkTime.localTime };
            movement.RestoreNetworkMovementState(state); movement.SetPredictLocally(true);
            var arc = rider.CaptureNetworkArcState(PlayerRidingController.RidingArcType.Dismount, Vector2.down, origin, origin, 0.5f);
            var actions = new NetworkBombFx.MountActionState { dismountEligible = true, dismountArc = arc };
            fx.ReconcileRiding(0, state, actions); Assert.IsTrue(fx.TryPredictDismount(0, 10));
            Assert.IsTrue(fx.RidingArcActive); Assert.IsFalse(rider.IsPlaying); Assert.IsFalse(movement.InputLocked); Assert.IsFalse(movement.explosionInvulnerable);
            Assert.AreEqual(MountedType.Red, companion.GetMountedLouieType(), "A speculative dismount cannot transfer mount ownership.");
            fx.ReconcileRiding(9, state, actions); Assert.IsTrue(fx.RidingArcActive);
            fx.ReconcileRiding(10, state, actions); Assert.IsFalse(fx.RidingArcActive); Assert.AreEqual(origin, body.position);
            Assert.IsTrue(fx.TryPredictDismount(0, 11));
            var update = typeof(NetworkBombFx).GetMethod("UpdateRidingPresentation", flags); var elapsed = typeof(NetworkBombFx).GetField("ridingElapsed", flags);
            update.Invoke(fx, new object[] { 0.1f, 0.1f }); float before = (float)elapsed.GetValue(fx);
            paused.SetValue(null, true); update.Invoke(fx, new object[] { 1f, 1f }); Assert.AreEqual(before, elapsed.GetValue(fx)); paused.SetValue(null, false);
            var motion = new NetworkBombFx.RidingMotion { revision = 1, inputTick = 11, arc = arc, active = true, serverTime = NetworkTime.localTime, sampleTime = NetworkTime.localTime };
            fx.ReceiveRidingArc(motion); Assert.AreEqual(before, elapsed.GetValue(fx));
            update.Invoke(fx, new object[] { 0.2f, 0.2f });
            motion.revision = 2; motion.active = false; motion.completed = true; motion.endTime = motion.serverTime + arc.duration;
            fx.ReceiveRidingArc(motion);
            var bounce = new NetworkBombFx.PlayerBounceMotion { revision = 1, start = origin, end = origin + Vector2.right,
                duration = 0.2f, curveDuration = 0.2f, height = 0.5f, first = true, moving = true };
            fx.ReceivePlayerBounce(bounce); Assert.IsFalse(fx.PlayerBounceActive, "Landing recovery cannot cancel an unfinished approved riding arc.");
            bounce.revision = 2; bounce.first = false; bounce.moving = false; fx.ReceivePlayerBounce(bounce);
            update.Invoke(fx, new object[] { 0.25f, 0.25f }); Assert.IsFalse(fx.RidingArcActive); Assert.IsTrue(fx.PlayerBounceActive);
            typeof(NetworkBombFx).GetMethod("UpdatePlayerBouncePresentation", flags).Invoke(fx, new object[] { 0.25f, 0.25f });
            Assert.IsFalse(fx.PlayerBounceActive); Assert.IsTrue(collider.enabled); Assert.AreEqual(origin + Vector2.right, body.position);
            movement.RestoreNetworkMovementState(state); movement.SetPredictLocally(true); fx.ReconcileRiding(12, state, actions);
            Assert.IsTrue(fx.TryPredictDismount(0, 12)); motion.revision = 3; motion.inputTick = 12; motion.active = true; motion.completed = false;
            motion.serverTime = motion.sampleTime = NetworkTime.localTime; fx.ReceiveRidingArc(motion);
            update.Invoke(fx, new object[] { 0.55f, 0.55f });
            motion.revision = 4; motion.active = false; motion.completed = true; motion.endTime = NetworkTime.localTime + 0.1;
            fx.ReceiveRidingArc(motion); fx.ReconcileRiding(12, state, actions); Assert.IsTrue(fx.RidingArcActive, "The final position bridges to a post-arc snapshot.");
            state.serverTime = motion.endTime + 0.01; fx.ReconcileRiding(12, state, actions); Assert.IsFalse(fx.RidingArcActive);
            Assert.IsTrue(fx.TryPredictDismount(0, 13)); movement.isDead = true; update.Invoke(fx, new object[] { 0.02f, 0.02f });
            Assert.IsFalse(fx.RidingArcActive); Assert.IsFalse(rider.IsPlaying); Assert.IsFalse(movement.explosionInvulnerable);
        }
        finally
        {
            if (fx != null) typeof(NetworkBombFx).GetMethod("ClearRidingPresentation", flags).Invoke(fx, new object[] { "test_cleanup" });
            paused.SetValue(null, wasPaused); NetSync.Mode = oldMode; clientState.SetValue(null, oldClientState); Object.DestroyImmediate(actor);
        }
    }

    [Test]
    public void RidingObserverStartsFromRetainedPausedPhaseAndRejectsStaleEvents()
    {
        var oldMode = NetSync.Mode; var actor = new GameObject("RidingObserver"); actor.SetActive(false);
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var clientState = typeof(NetworkClient).GetField("connectState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var oldClientState = clientState.GetValue(null); NetworkBombFx fx = null;
        var paused = typeof(GamePauseController).GetProperty(nameof(GamePauseController.IsPaused)); bool wasPaused = GamePauseController.IsPaused;
        try
        {
            NetSync.Mode = NetSync.NetMode.Client; clientState.SetValue(null, System.Enum.Parse(clientState.FieldType, "Connected"));
            actor.AddComponent<BoxCollider2D>(); var identity = actor.AddComponent<NetworkIdentity>(); fx = actor.AddComponent<NetworkBombFx>();
            typeof(NetworkIdentity).GetMethod("InitializeNetworkBehaviours", flags).Invoke(identity, null);
            var movement = actor.GetComponent<MovementController>(); var body = actor.GetComponent<Rigidbody2D>(); body.bodyType = RigidbodyType2D.Kinematic;
            typeof(MovementController).GetProperty(nameof(MovementController.Rigidbody)).SetValue(movement, body);
            var rider = actor.AddComponent<PlayerRidingController>(); actor.SetActive(true); Vector3 origin = new(29000, 29000); actor.transform.position = origin;
            var arc = rider.CaptureNetworkArcState(PlayerRidingController.RidingArcType.Mount, Vector2.down, origin, origin + Vector3.right * 2, 1);
            var retained = new NetworkBombFx.RidingMotion { revision = 1, arc = arc, active = true, paused = true, elapsed = 0.25f,
                serverTime = NetworkTime.time - 10, sampleTime = NetworkTime.time - 5 };
            typeof(NetworkBombFx).GetField("serverRidingMotion", flags).SetValue(fx, retained); paused.SetValue(null, true); fx.OnStartClient();
            Assert.IsTrue(fx.RidingArcActive); Assert.That(body.position.x, Is.EqualTo(origin.x + 0.5f).Within(0.01f));
            var update = typeof(NetworkBombFx).GetMethod("UpdateRidingPresentation", flags);
            update.Invoke(fx, new object[] { 0.5f, 0.5f }); Assert.That(body.position.x, Is.EqualTo(origin.x + 0.5f).Within(0.01f));
            paused.SetValue(null, false); update.Invoke(fx, new object[] { 0.5f, 0.5f });
            Assert.That(body.position.x, Is.EqualTo(origin.x + 0.5f).Within(0.01f), "Resume waits for a fresh approved unpaused sample.");
            retained.paused = false; retained.sampleTime = NetworkTime.time; typeof(NetworkBombFx).GetField("serverRidingMotion", flags).SetValue(fx, retained);
            update.Invoke(fx, new object[] { 0.25f, 0.25f }); Assert.That(body.position.x, Is.EqualTo(origin.x + 1).Within(0.01f));
            var stop = retained; stop.revision = 2; stop.active = false; stop.completed = true; stop.endTime = NetworkTime.time - 0.01;
            fx.ReceiveRidingArc(stop); update.Invoke(fx, new object[] { 0.6f, 0.6f }); Assert.IsFalse(fx.RidingArcActive);
            Assert.That(body.position.x, Is.EqualTo(origin.x + 2).Within(0.01f));
            fx.ReceiveRidingArc(retained); Assert.IsFalse(fx.RidingArcActive); Assert.IsFalse(movement.IsMounted); Assert.IsFalse(rider.IsPlaying);
            using var writer = NetworkWriterPool.Get(); writer.Write(retained);
            using var reader = NetworkReaderPool.Get(writer.ToArraySegment()); var copy = reader.Read<NetworkBombFx.RidingMotion>();
            Assert.AreEqual(retained.elapsed, copy.elapsed); Assert.AreEqual(arc.peak, copy.arc.peak); Assert.AreEqual(arc.target, copy.arc.target);
            Assert.AreEqual(retained.paused, copy.paused); Assert.AreEqual(retained.sampleTime, copy.sampleTime);
        }
        finally
        {
            if (fx != null) typeof(NetworkBombFx).GetMethod("ClearRidingPresentation", flags).Invoke(fx, new object[] { "test_cleanup" });
            paused.SetValue(null, wasPaused); NetSync.Mode = oldMode; clientState.SetValue(null, oldClientState); Object.DestroyImmediate(actor);
        }
    }

    [Test]
    public void RidingServerBodyIsImmediatelyCoherentWithoutChangingOfflineVelocity()
    {
        var oldMode = NetSync.Mode; var actor = new GameObject("RidingServerPosition"); actor.SetActive(false);
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        try
        {
            actor.AddComponent<BoxCollider2D>(); var identity = actor.AddComponent<NetworkIdentity>(); var fx = actor.AddComponent<NetworkBombFx>();
            typeof(NetworkIdentity).GetMethod("InitializeNetworkBehaviours", flags).Invoke(identity, null);
            typeof(NetworkIdentity).GetProperty("netId").SetValue(identity, 124u); typeof(NetworkIdentity).GetProperty("isServer").SetValue(identity, true);
            var movement = actor.GetComponent<MovementController>(); var body = actor.GetComponent<Rigidbody2D>(); body.bodyType = RigidbodyType2D.Kinematic;
            typeof(MovementController).GetProperty(nameof(MovementController.Rigidbody)).SetValue(movement, body);
            var rider = actor.AddComponent<PlayerRidingController>(); typeof(PlayerRidingController).GetField("movement", flags).SetValue(rider, movement);
            // An inactive GameObject has no native physics body: velocity writes are ignored.
            foreach (var behaviour in actor.GetComponents<MonoBehaviour>()) behaviour.enabled = false;
            actor.SetActive(true);
            body.linearVelocity = Vector2.right * 2; body.angularVelocity = 2;
            Assert.AreEqual(Vector2.right * 2, body.linearVelocity, "Fixture needs a live physics body.");
            Vector2 target = new(30000, 30000); actor.transform.position = target;
            var sync = typeof(PlayerRidingController).GetMethod("SyncNetworkRidingBody", flags);
            NetSync.Mode = NetSync.NetMode.Offline; sync.Invoke(rider, null); Assert.AreEqual(Vector2.right * 2, body.linearVelocity);
            NetSync.Mode = NetSync.NetMode.Host; sync.Invoke(rider, null);
            Assert.AreEqual(target, body.position); Assert.AreEqual(target, movement.CaptureNetworkMovementState().position);
            Assert.AreEqual(Vector2.zero, body.linearVelocity); Assert.AreEqual(0, body.angularVelocity);
        }
        finally { NetSync.Mode = oldMode; Object.DestroyImmediate(actor); }
    }

    [Test]
    public void RidingArcPresentationPreservesPixelCurveWithoutStartingGameplay()
    {
        var oldMode = NetSync.Mode; var actor = new GameObject("RidingVisualOnly"); actor.SetActive(false);
        AnimatedSpriteRenderer Animation(string name) { var go = new GameObject(name); go.transform.SetParent(actor.transform, false); return go.AddComponent<AnimatedSpriteRenderer>(); }
        try
        {
            var collider = actor.AddComponent<BoxCollider2D>(); var movement = actor.AddComponent<MovementController>();
            var rider = actor.AddComponent<PlayerRidingController>(); rider.ridingSeconds = 0.8f;
            var up = Animation("RidingAscend"); var down = Animation("RidingDescend"); var idle = Animation("RidingIdle");
            up.animationSprite = down.animationSprite = new Sprite[3]; up.animationTime = down.animationTime = 0.1f;
            rider.mountAscendDown = up; rider.mountDescendDown = down; movement.spriteRendererDown = idle;
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(PlayerRidingController).GetField("mountEndHeightPixels", flags).SetValue(rider, 8);
            typeof(PlayerRidingController).GetField("dismountStartHeightPixels", flags).SetValue(rider, 8);
            Vector3 origin = new(27000, 27000); actor.transform.position = origin;
            var arc = rider.CaptureNetworkArcState(PlayerRidingController.RidingArcType.Mount, Vector2.down, origin, origin + new Vector3(2, 0, 9));
            Assert.AreEqual(0.8f, arc.duration); Assert.AreEqual(origin.z, arc.target.z);
            Assert.AreEqual(1.625f, PlayerRidingController.SampleNetworkArcHeight(arc, 0.25f));
            Assert.AreEqual(2.25f, PlayerRidingController.SampleNetworkArcHeight(arc, 0.5f));
            Assert.AreEqual(0.5f, PlayerRidingController.SampleNetworkArcHeight(arc, 1));
            NetSync.Mode = NetSync.NetMode.Client; rider.PresentNetworkArc(arc, 0.2f);
            Assert.IsTrue(up.enabled); Assert.IsFalse(down.enabled); Assert.IsFalse(idle.enabled);
            Assert.AreEqual(2, up.CurrentFrame); Assert.AreEqual(1.625f, up.RuntimeBaseLocalOffset.y);
            rider.PresentNetworkArc(arc, 0.2f); Assert.AreEqual(2, up.CurrentFrame);
            rider.PresentNetworkArc(arc, 0.6f); Assert.IsFalse(up.enabled); Assert.IsTrue(down.enabled);
            Assert.AreEqual(1.875f, down.RuntimeBaseLocalOffset.y); Assert.AreEqual(origin, actor.transform.position);
            Assert.IsFalse(rider.IsPlaying); Assert.IsFalse(movement.InputLocked); Assert.IsFalse(movement.explosionInvulnerable); Assert.IsTrue(collider.enabled);
            rider.StopNetworkArcVisual(); Assert.IsFalse(up.enabled); Assert.IsFalse(down.enabled);
            Assert.AreEqual(Vector3.zero, up.RuntimeBaseLocalOffset); Assert.AreEqual(Vector3.zero, down.RuntimeBaseLocalOffset);
            var exit = rider.CaptureNetworkArcState(PlayerRidingController.RidingArcType.Dismount, Vector2.down, origin, origin, 0.5f);
            Assert.AreEqual(0.5f, exit.duration); Assert.AreEqual(0.5f, PlayerRidingController.SampleNetworkArcHeight(exit, 0));
            Assert.AreEqual(0, PlayerRidingController.SampleNetworkArcHeight(exit, 1));
            NetSync.Mode = NetSync.NetMode.Host; rider.PresentNetworkArc(arc, 0.2f); Assert.IsFalse(up.enabled);
        }
        finally { NetSync.Mode = oldMode; Object.DestroyImmediate(actor); }
    }

    [Test]
    public void ControlledBombDetonationCannotRunGameplayOnPureClient()
    {
        var oldMode = NetSync.Mode; var actor = new GameObject("ControlDetonationClient"); actor.SetActive(false);
        var bombObject = new GameObject("ControlDetonationTarget"); bombObject.SetActive(false);
        try
        {
            actor.AddComponent<BoxCollider2D>(); var controller = actor.AddComponent<BombController>();
            bombObject.AddComponent<BoxCollider2D>(); var target = bombObject.AddComponent<Bomb>(); target.IsControlBomb = true;
            var planted = (List<GameObject>)typeof(BombController).GetField("plantedBombs", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(controller);
            planted.Add(bombObject); NetSync.Mode = NetSync.NetMode.Client;
            Assert.IsFalse(controller.TryExplodeOldestControlledBomb()); Assert.IsFalse(controller.TryExplodeAllControlledBombs());
            Assert.AreEqual(1, planted.Count); Assert.AreSame(bombObject, planted[0]); Assert.IsFalse(target.HasExploded);
            planted.Clear();
        }
        finally { NetSync.Mode = oldMode; Object.DestroyImmediate(actor); Object.DestroyImmediate(bombObject); }
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    public void LineInputPreviewUsesApprovedGeometryAndNeverEditsWorldOrInventory(int obstruction)
    {
        var oldMode = NetSync.Mode; var actor = new GameObject("LineGeometryOwner"); actor.SetActive(false);
        var grid = new GameObject("LineGeometryGrid", typeof(Grid)); grid.transform.position = new Vector3(26000 - 0.5f, 26000 - 0.5f);
        var blocker = new GameObject("LineGeometryBlocker");
        var tile = ScriptableObject.CreateInstance<Tile>(); tile.colliderType = Tile.ColliderType.None;
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var clientState = typeof(NetworkClient).GetField("connectState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var oldClientState = clientState.GetValue(null); const uint prefabId = 19900001;
        bool hadPrefab = NetworkClient.prefabs.TryGetValue(prefabId, out var oldPrefab); NetworkBombFx fx = null;
        Tilemap Map(string name) { var go = new GameObject(name, typeof(Tilemap)); go.transform.SetParent(grid.transform, false); return go.GetComponent<Tilemap>(); }
        try
        {
            NetSync.Mode = NetSync.NetMode.Client; clientState.SetValue(null, System.Enum.Parse(clientState.FieldType, "Connected"));
            actor.tag = "Player"; actor.AddComponent<BoxCollider2D>(); var identity = actor.AddComponent<NetworkIdentity>(); fx = actor.AddComponent<NetworkBombFx>();
            typeof(NetworkIdentity).GetMethod("InitializeNetworkBehaviours", flags).Invoke(identity, null);
            typeof(NetworkIdentity).GetProperty("isLocalPlayer").SetValue(identity, true);
            var movement = actor.GetComponent<MovementController>(); var body = actor.GetComponent<Rigidbody2D>(); body.bodyType = RigidbodyType2D.Kinematic;
            typeof(MovementController).GetProperty(nameof(MovementController.Rigidbody)).SetValue(movement, body);
            var controller = actor.GetComponent<BombController>(); var ground = Map("Ground"); var walls = Map("Indestructibles");
            var water = Map("Water"); var holes = Map("Hole"); var destructible = Map("Destructibles");
            controller.groundTiles = ground; controller.stageBoundsTiles = walls; controller.destructibleTiles = destructible;
            typeof(BombController).GetField("waterTiles", flags).SetValue(controller, water); typeof(BombController).GetField("holeTiles", flags).SetValue(controller, holes);
            for (int x = 0; x < 8; x++) ground.SetTile(new Vector3Int(x, 0), tile);
            if (obstruction == 0) water.SetTile(new Vector3Int(3, 0), tile);
            else if (obstruction == 1) holes.SetTile(new Vector3Int(3, 0), tile);
            else if (obstruction == 2) walls.SetTile(new Vector3Int(3, 0), tile);
            else { blocker.layer = LayerMask.NameToLayer(obstruction == 3 ? "Enemy" : "Explosion"); blocker.transform.position = new Vector3(26003, 26000); blocker.AddComponent<BoxCollider2D>(); }
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Bombs/Bomb.prefab"); NetworkClient.prefabs[prefabId] = prefab;
            actor.SetActive(true); movement.SetNetworkTickDriven(true); Vector2 origin = new(26000, 26000);
            movement.RestoreNetworkMovementState(new MovementController.NetworkMovementState { position = origin, facing = Vector2.right,
                worldSpeed = 4, animationSpeed = PlayerPersistentStats.BaseSpeedNormal, canPredict = true }); movement.SetPredictLocally(true); Physics2D.SyncTransforms();
            var state = new NetworkBombFx.PlacementState { available = 5, allowed = true, lineAllowed = true, prefabAssetId = prefabId, continuationPrefabAssetId = prefabId };
            fx.ReconcilePlacement(0, state); int bombs = Bomb.ActiveBombs.Count, stock = controller.BombsRemaining;
            Assert.IsTrue(fx.BeginPlacementPrediction(1, origin, prefab));
            Assert.IsTrue(fx.TryPredictPlacement(2), "A second A edge can preview a line from its pending first bomb.");
            Assert.AreEqual(3, fx.PlacementReservationCount, "Seed plus two valid cells; the obstruction ends the preview.");
            Assert.IsTrue(fx.HasPredictedPlacementAt(origin + Vector2.right, 1, 2));
            Assert.IsTrue(fx.HasPredictedPlacementAt(origin + Vector2.right * 2, 1, 2));
            Assert.IsFalse(fx.HasPredictedPlacementAt(origin + Vector2.right * 3, 1, 2));
            Assert.AreEqual(bombs, Bomb.ActiveBombs.Count); Assert.AreEqual(stock, controller.BombsRemaining);
            Assert.IsTrue(ground.HasTile(new Vector3Int(3, 0))); Assert.AreEqual(origin, body.position);
        }
        finally
        {
            if (fx != null) typeof(NetworkBombFx).GetMethod("ClearPlacementPredictions", flags).Invoke(fx, null);
            if (hadPrefab) NetworkClient.prefabs[prefabId] = oldPrefab; else NetworkClient.prefabs.Remove(prefabId);
            NetSync.Mode = oldMode; clientState.SetValue(null, oldClientState); Object.DestroyImmediate(actor);
            Object.DestroyImmediate(grid); Object.DestroyImmediate(blocker); Object.DestroyImmediate(tile);
        }
    }

    [Test]
    public void LinePlacementGroupsReserveTogetherAndConfirmEachCellIndependently()
    {
        var oldMode = NetSync.Mode; var actor = new GameObject("LinePlacementGroups"); actor.SetActive(false);
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var clientState = typeof(NetworkClient).GetField("connectState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var oldClientState = clientState.GetValue(null); NetworkBombFx fx = null;
        try
        {
            NetSync.Mode = NetSync.NetMode.Client; clientState.SetValue(null, System.Enum.Parse(clientState.FieldType, "Connected"));
            actor.AddComponent<BoxCollider2D>(); var identity = actor.AddComponent<NetworkIdentity>(); fx = actor.AddComponent<NetworkBombFx>();
            typeof(NetworkIdentity).GetMethod("InitializeNetworkBehaviours", flags).Invoke(identity, null);
            typeof(NetworkIdentity).GetProperty("isLocalPlayer").SetValue(identity, true);
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Bombs/Bomb.prefab");
            var state = new NetworkBombFx.PlacementState { available = 3, allowed = true, prefabAssetId = 1,
                continuationPrefabAssetId = 2, lineAllowed = true, purpleAllowed = true, purpleDuration = 0.25f };
            fx.ReconcilePlacement(0, state); Vector2 origin = new(24000, 24000); int bombs = Bomb.ActiveBombs.Count;
            Assert.IsTrue(fx.BeginPlacementPrediction(1, origin, prefab, 1, false));
            Assert.IsTrue(fx.BeginPlacementPrediction(1, origin + Vector2.right, prefab, 1, false));
            Assert.IsTrue(fx.BeginPlacementPrediction(2, origin + Vector2.right * 2, prefab, 2, false));
            Assert.AreEqual(3, fx.PlacementReservationCount);
            Assert.IsFalse(fx.BeginPlacementPrediction(3, origin + Vector2.right * 3, prefab), "All action groups share the approved inventory.");
            var proxies = (System.Collections.IDictionary)typeof(NetworkBombFx).GetField("placements", flags).GetValue(fx);
            var second = proxies[(1u, (byte)1, origin + Vector2.right)];
            fx.ReceivePlacementConfirmed(1, 777, origin + Vector2.right, 2);
            Assert.IsFalse((bool)second.GetType().GetField("confirmed").GetValue(second), "Another action cannot take the same tick's proxy.");
            fx.ReceivePlacementConfirmed(1, 777, origin + Vector2.right, 1);
            Assert.IsTrue((bool)second.GetType().GetField("confirmed").GetValue(second));
            Assert.IsFalse((bool)proxies[(1u, (byte)1, origin)].GetType().GetField("confirmed").GetValue(proxies[(1u, (byte)1, origin)]));
            Assert.AreEqual(3, fx.PlacementReservationCount, "Confirmed cells reserve old snapshot inventory until acknowledgement.");
            fx.ReconcilePlacement(1, state); Assert.AreEqual(1, fx.PlacementReservationCount);
            Assert.IsTrue(fx.HasPredictedPlacementAt(origin + Vector2.right * 2, 1, 2));
            fx.ReconcilePlacement(2, state); Assert.AreEqual(0, fx.PlacementReservationCount);
            Assert.AreEqual(bombs, Bomb.ActiveBombs.Count);
            using var writer = NetworkWriterPool.Get(); writer.Write(state);
            using var reader = NetworkReaderPool.Get(writer.ToArraySegment()); var copy = reader.Read<NetworkBombFx.PlacementState>();
            Assert.AreEqual(state.continuationPrefabAssetId, copy.continuationPrefabAssetId); Assert.IsTrue(copy.purpleAllowed);
            Assert.IsTrue(copy.lineAllowed); Assert.AreEqual(state.purpleDuration, copy.purpleDuration);
        }
        finally
        {
            if (fx != null) typeof(NetworkBombFx).GetMethod("ClearPlacementPredictions", flags).Invoke(fx, null);
            NetSync.Mode = oldMode; clientState.SetValue(null, oldClientState); Object.DestroyImmediate(actor);
        }
    }

    [Test]
    public void PurpleLinePoseWithNoStockConfirmsWithoutRestartAndRestoresOnRejection()
    {
        var oldMode = NetSync.Mode; var actor = new GameObject("PurpleLineOwner"); actor.SetActive(false);
        var mount = new GameObject("PurpleLineMount"); mount.transform.SetParent(actor.transform, false);
        var phase = new GameObject("PurpleLineMagic"); phase.transform.SetParent(mount.transform, false);
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var clientState = typeof(NetworkClient).GetField("connectState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var oldClientState = clientState.GetValue(null); NetworkBombFx fx = null;
        var paused = typeof(GamePauseController).GetProperty(nameof(GamePauseController.IsPaused)); bool wasPaused = GamePauseController.IsPaused;
        try
        {
            NetSync.Mode = NetSync.NetMode.Client; clientState.SetValue(null, System.Enum.Parse(clientState.FieldType, "Connected"));
            actor.tag = "Player"; actor.AddComponent<BoxCollider2D>(); var identity = actor.AddComponent<NetworkIdentity>(); fx = actor.AddComponent<NetworkBombFx>();
            typeof(NetworkIdentity).GetMethod("InitializeNetworkBehaviours", flags).Invoke(identity, null);
            typeof(NetworkIdentity).GetProperty("isLocalPlayer").SetValue(identity, true);
            var movement = actor.GetComponent<MovementController>(); var body = actor.GetComponent<Rigidbody2D>(); body.bodyType = RigidbodyType2D.Kinematic;
            typeof(MovementController).GetProperty(nameof(MovementController.Rigidbody)).SetValue(movement, body);
            var companion = actor.AddComponent<PlayerMountCompanion>();
            typeof(PlayerMountCompanion).GetField("currentLouie", flags).SetValue(companion, mount);
            typeof(PlayerMountCompanion).GetField("mountedType", flags).SetValue(companion, MountedType.Purple);
            var animator = mount.AddComponent<PurpleLouieBombLineAnimator>(); animator.magicDown = phase.AddComponent<AnimatedSpriteRenderer>();
            actor.SetActive(true); movement.SetNetworkTickDriven(true);
            var state = new MovementController.NetworkMovementState { position = new Vector2(25000, 25000), mount = MountedType.Purple,
                facing = Vector2.down, worldSpeed = 4, animationSpeed = PlayerPersistentStats.BaseSpeedNormal, canPredict = true };
            movement.RestoreNetworkMovementState(state); movement.SetPredictLocally(true);
            fx.ReconcilePlacement(0, new NetworkBombFx.PlacementState { purpleAllowed = true, purpleDuration = 0.25f });
            int bombs = Bomb.ActiveBombs.Count;
            Assert.IsTrue(fx.TryPredictLinePlacement(10, 0, true)); Assert.IsTrue(fx.PurpleLinePoseActive);
            Assert.IsTrue(fx.AbilityPredictionLocksMovement); Assert.AreEqual(0, fx.PlacementReservationCount);
            Assert.IsTrue(animator.magicDown.enabled); Assert.IsFalse(movement.InputLocked);
            animator.ForceStop(); animator.magicDown.enabled = false; fx.ReapplyMountAbilityPose(); Assert.IsTrue(animator.magicDown.enabled);
            var update = typeof(NetworkBombFx).GetMethod("UpdatePurpleLineCast", flags);
            var elapsed = typeof(NetworkBombFx).GetField("purpleLineElapsed", flags);
            update.Invoke(fx, new object[] { 0.1f, 0.1f }); float before = (float)elapsed.GetValue(fx);
            paused.SetValue(null, true); update.Invoke(fx, new object[] { 1f, 1f }); Assert.AreEqual(before, elapsed.GetValue(fx)); paused.SetValue(null, false);
            fx.ReceivePurpleLineCast(10, Vector2.down, 0.25f, NetworkTime.time); Assert.AreEqual(before, elapsed.GetValue(fx));
            state.canPredict = false; fx.ReconcilePurpleLineCast(10, state); Assert.IsTrue(fx.PurpleLinePoseActive);
            update.Invoke(fx, new object[] { 0.2f, 0.2f }); Assert.IsFalse(fx.PurpleLinePoseActive);
            fx.ReceivePurpleLineCast(10, Vector2.down, 0.25f, NetworkTime.time); Assert.IsFalse(fx.PurpleLinePoseActive);
            state.canPredict = true; movement.RestoreNetworkMovementState(state); movement.SetPredictLocally(true);
            Assert.IsTrue(fx.TryPredictLinePlacement(11, 0, true)); fx.ReconcilePurpleLineCast(11, state); Assert.IsFalse(fx.PurpleLinePoseActive);
            Assert.IsTrue(fx.TryPredictLinePlacement(12, 0, true)); update.Invoke(fx, new object[] { 2f, 2f }); Assert.IsFalse(fx.PurpleLinePoseActive);
            Assert.IsTrue(fx.TryPredictLinePlacement(13, 0, true)); movement.isDead = true;
            update.Invoke(fx, new object[] { 0.01f, 0.01f }); Assert.IsFalse(fx.PurpleLinePoseActive);
            Assert.AreEqual(bombs, Bomb.ActiveBombs.Count); Assert.AreEqual(state.position, body.position);
        }
        finally
        {
            if (fx != null) typeof(NetworkBombFx).GetMethod("ClearPurpleLineCast", flags).Invoke(fx, new object[] { "test_cleanup" });
            paused.SetValue(null, wasPaused); NetSync.Mode = oldMode; clientState.SetValue(null, oldClientState); Object.DestroyImmediate(actor);
        }
    }

    [Test]
    public void LinePlacementContinuationPreviewRespectsFirstOnlyVariantsWithoutSpawning()
    {
        var actor = new GameObject("LineVariantPreview"); actor.SetActive(false);
        var normal = new GameObject("NormalPreviewDefinition"); var special = new GameObject("SpecialPreviewDefinition");
        var oldRandom = Random.state;
        try
        {
            actor.AddComponent<BoxCollider2D>();
            var controller = actor.AddComponent<BombController>(); var abilities = actor.GetComponent<AbilitySystem>() ?? actor.AddComponent<AbilitySystem>();
            controller.bombPrefab = normal; controller.searchBombPrefab = special; controller.powerBombPrefab = special;
            abilities.Enable(SearchBombAbility.AbilityId);
            int count = Bomb.ActiveBombs.Count, stock = controller.BombsRemaining;
            Random.InitState(202); float expected = Random.value; Random.InitState(202);
            Assert.AreSame(special, controller.GetNetworkPlacementPrefab());
            Assert.AreSame(normal, controller.GetNetworkPlacementContinuationPrefab());
            typeof(BombController).GetField("specialBombsOnEveryPlacement", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(controller, true);
            Assert.AreSame(special, controller.GetNetworkPlacementContinuationPrefab());
            abilities.Enable(PowerBombAbility.AbilityId);
            Assert.AreSame(special, controller.GetNetworkPlacementPrefab());
            Assert.AreSame(normal, controller.GetNetworkPlacementContinuationPrefab(), "Power Bomb remains first-only even when Search/Magnet every-placement is configured.");
            Assert.AreEqual(expected, Random.value); Assert.AreEqual(count, Bomb.ActiveBombs.Count); Assert.AreEqual(stock, controller.BombsRemaining);
        }
        finally { Random.state = oldRandom; Object.DestroyImmediate(actor); Object.DestroyImmediate(normal); Object.DestroyImmediate(special); }
    }

    [Test]
    public void MoleStartupPredictsOnlyPoseConfirmsWithoutRestartAndRestoresOnEveryExit()
    {
        var oldMode = NetSync.Mode; var actor = new GameObject("MoleStartupOwner"); actor.SetActive(false);
        var mount = new GameObject("MoleStartupMount"); mount.transform.SetParent(actor.transform, false);
        var phase = new GameObject("MoleStartupPhase"); phase.transform.SetParent(mount.transform, false);
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var clientState = typeof(NetworkClient).GetField("connectState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var oldClientState = clientState.GetValue(null); var oldRandom = Random.state; NetworkBombFx fx = null;
        var paused = typeof(GamePauseController).GetProperty(nameof(GamePauseController.IsPaused)); bool wasPaused = GamePauseController.IsPaused;
        try
        {
            NetSync.Mode = NetSync.NetMode.Client; clientState.SetValue(null, System.Enum.Parse(clientState.FieldType, "Connected"));
            actor.tag = "Player"; actor.AddComponent<BoxCollider2D>();
            var identity = actor.AddComponent<NetworkIdentity>(); fx = actor.AddComponent<NetworkBombFx>();
            typeof(NetworkIdentity).GetMethod("InitializeNetworkBehaviours", flags).Invoke(identity, null);
            typeof(NetworkIdentity).GetProperty("isLocalPlayer").SetValue(identity, true);
            var movement = actor.GetComponent<MovementController>(); var body = actor.GetComponent<Rigidbody2D>(); body.bodyType = RigidbodyType2D.Kinematic;
            typeof(MovementController).GetProperty(nameof(MovementController.Rigidbody)).SetValue(movement, body);
            var companion = actor.AddComponent<PlayerMountCompanion>();
            typeof(PlayerMountCompanion).GetField("currentLouie", flags).SetValue(companion, mount);
            typeof(PlayerMountCompanion).GetField("mountedType", flags).SetValue(companion, MountedType.Mole);
            var animator = mount.AddComponent<MoleMountDrillAnimator>(); animator.phase1 = phase.AddComponent<AnimatedSpriteRenderer>();
            actor.SetActive(true); movement.SetNetworkTickDriven(true); Vector2 origin = new(23000, 23000);
            var state = new MovementController.NetworkMovementState { position = origin, mount = MountedType.Mole,
                facing = Vector2.down, worldSpeed = 4, animationSpeed = PlayerPersistentStats.BaseSpeedNormal, canPredict = true };
            movement.RestoreNetworkMovementState(state); movement.SetPredictLocally(true);
            fx.PredictBombAbilityInput(0, 0, 10); var ability = actor.GetComponent<MoleMountDrillAbility>(); Assert.IsNotNull(ability);
            typeof(MoleMountDrillAbility).GetMethod("Awake", flags).Invoke(ability, null);
            Random.InitState(135); float expectedRandom = Random.value; Random.InitState(135);
            Assert.IsTrue(fx.TryPredictMoleStartup(0, 10)); Assert.AreEqual(expectedRandom, Random.value);
            Assert.IsTrue(fx.MoleStartupActive); Assert.IsTrue(fx.HasLocalMountAbilityPose); Assert.IsTrue(fx.AbilityPredictionLocksMovement);
            Assert.IsFalse(ability.Running); Assert.IsFalse(ability.IsProtectingFromStun); Assert.IsFalse(movement.explosionInvulnerable);
            Assert.AreEqual(origin, body.position); Assert.IsTrue(animator.phase1.enabled);
            Assert.AreSame(animator, typeof(MoleMountDrillAbility).GetField("networkStartupAnimator", flags).GetValue(ability));
            Assert.IsTrue((bool)typeof(MoleMountDrillAnimator).GetField("playing", flags).GetValue(animator));
            animator.phase1.enabled = false; fx.ReapplyMountAbilityPose(); Assert.IsTrue(animator.phase1.enabled, "Mount rebind cannot disable the active owner startup pose.");
            fx.ReconcileMoleStartup(9, state); Assert.IsTrue(fx.MoleStartupActive);
            fx.ReconcileMoleStartup(10, state); Assert.IsFalse(fx.MoleStartupActive); Assert.IsFalse(movement.InputLocked);
            movement.RestoreNetworkMovementState(state); movement.SetPredictLocally(true); Assert.IsTrue(fx.TryPredictMoleStartup(0, 11));
            var update = typeof(NetworkBombFx).GetMethod("UpdateMoleStartup", flags);
            update.Invoke(fx, new object[] { 0.2f, 0.2f });
            var elapsed = typeof(NetworkBombFx).GetField("moleStartupElapsed", flags); float before = (float)elapsed.GetValue(fx);
            paused.SetValue(null, true); update.Invoke(fx, new object[] { 1f, 1f });
            Assert.AreEqual(before, (float)elapsed.GetValue(fx)); Assert.IsTrue(fx.MoleStartupActive); paused.SetValue(null, false);
            var motion = new NetworkBombFx.MoleStartupMotion { revision = 1, inputTick = 11, direction = Vector2.down,
                duration = 1, active = true, serverTime = NetworkTime.time };
            fx.ReceiveMoleStartup(motion); Assert.AreEqual(before, (float)elapsed.GetValue(fx));
            state.canPredict = false; fx.ReconcileMoleStartup(11, state); Assert.IsTrue(fx.MoleStartupActive);
            motion.revision = 2; motion.active = false; fx.ReceiveMoleStartup(motion);
            Assert.IsFalse(fx.MoleStartupActive); Assert.IsFalse(animator.phase1.enabled); Assert.IsFalse(fx.AbilityPredictionLocksMovement);
            motion.revision = 1; motion.active = true; fx.ReceiveMoleStartup(motion); Assert.IsFalse(fx.MoleStartupActive, "Stale startup cannot resurrect after authority hands presentation to the next phase.");
            state.canPredict = true; movement.RestoreNetworkMovementState(state); movement.SetPredictLocally(true);
            Assert.IsTrue(fx.TryPredictMoleStartup(0, 12)); update.Invoke(fx, new object[] { 2f, 2f }); Assert.IsFalse(fx.MoleStartupActive);
            movement.RestoreNetworkMovementState(state); movement.SetPredictLocally(true); Assert.IsTrue(fx.TryPredictMoleStartup(0, 13));
            movement.isDead = true; update.Invoke(fx, new object[] { 0.02f, 0.02f }); Assert.IsFalse(fx.MoleStartupActive);
            Assert.AreEqual(origin, body.position); Assert.IsFalse(ability.Running); Assert.IsFalse(movement.explosionInvulnerable);
            using var writer = NetworkWriterPool.Get(); writer.Write(motion);
            using var reader = NetworkReaderPool.Get(writer.ToArraySegment()); var copy = reader.Read<NetworkBombFx.MoleStartupMotion>();
            Assert.AreEqual(motion.inputTick, copy.inputTick); Assert.AreEqual(motion.duration, copy.duration); Assert.IsTrue(copy.active);
            NetSync.Mode = NetSync.NetMode.Host; Assert.IsFalse(fx.TryPredictMoleStartup(0, 14));
        }
        finally
        {
            if (fx != null) typeof(NetworkBombFx).GetMethod("ClearMoleStartup", flags).Invoke(fx, new object[] { "test_cleanup" });
            paused.SetValue(null, wasPaused); Random.state = oldRandom; NetSync.Mode = oldMode; clientState.SetValue(null, oldClientState); Object.DestroyImmediate(actor);
        }
    }

    [Test]
    public void CoopTileQueriesUseSafeTilesAndPreserveReplayPassAndApprovedPose()
    {
        var oldMode = NetSync.Mode;
        var grid = new GameObject("SourceCollisionGrid", typeof(Grid)); grid.transform.position = new Vector3(20000, 20000);
        var sourceObject = new GameObject("Destructibles", typeof(Tilemap), typeof(TilemapCollider2D)); sourceObject.transform.SetParent(grid.transform, false);
        var sourcePrefab = new GameObject("TileStartupObject"); sourcePrefab.SetActive(false);
        var sourceTile = ScriptableObject.CreateInstance<Tile>(); sourceTile.colliderType = Tile.ColliderType.Grid; sourceTile.gameObject = sourcePrefab;
        var actor = new GameObject("CoopCollisionReplay"); actor.SetActive(false); CoopTileCollisionView view = null;
        try
        {
            NetSync.Mode = NetSync.NetMode.Client;
            sourceObject.layer = LayerMask.NameToLayer("Stage"); sourceObject.tag = "Destructibles";
            var source = sourceObject.GetComponent<Tilemap>(); source.SetTile(Vector3Int.zero, sourceTile);
            var sourceCollider = sourceObject.GetComponent<TilemapCollider2D>(); sourceCollider.enabled = false;
            Assert.IsTrue(CoopTileCollisionView.TryCreate(source, null, out view));
            view.SetCell(Vector3Int.zero, Tile.ColliderType.Grid, null, Matrix4x4.identity); view.SyncPose(true, Vector2.zero); view.Refresh(); Physics2D.SyncTransforms();
            Vector2 center = source.GetCellCenterWorld(Vector3Int.zero);
            Assert.AreSame(view.Collider, Physics2D.OverlapPoint(center, LayerMask.GetMask("Stage")));
            Assert.IsFalse(sourceCollider.enabled); Assert.IsNull(view.Map.GetComponent<TilemapRenderer>());
            Assert.IsEmpty(view.Map.GetComponentsInParent<MonoBehaviour>()); Assert.IsNull(view.Map.GetInstantiatedObject(Vector3Int.zero));
            var copied = (Tile)view.Map.GetTile(Vector3Int.zero); Assert.AreNotSame(sourceTile, copied); Assert.IsNull(copied.gameObject);
            view.SetCell(Vector3Int.right * 10, Tile.ColliderType.Sprite, null, Matrix4x4.identity);
            Assert.IsFalse(view.Ready, "An unresolved approved collision sprite cannot silently become traversable ground.");
            view.SetCell(Vector3Int.right * 10, Tile.ColliderType.None, null, Matrix4x4.identity); Assert.IsTrue(view.Ready);
            actor.tag = "Player"; actor.AddComponent<BoxCollider2D>(); var body = actor.AddComponent<Rigidbody2D>(); body.bodyType = RigidbodyType2D.Kinematic;
            var movement = actor.AddComponent<MovementController>();
            typeof(MovementController).GetProperty(nameof(MovementController.Rigidbody)).SetValue(movement, body);
            movement.obstacleMask = LayerMask.GetMask("Stage"); movement.RefreshObstacleContactFilter();
            actor.SetActive(true); movement.SetNetworkTickDriven(true); movement.SetPredictLocally(true);
            var state = new MovementController.NetworkMovementState { position = center - Vector2.right, facing = Vector2.right,
                worldSpeed = 4, animationSpeed = PlayerPersistentStats.BaseSpeedNormal, canPredict = true };
            movement.RestoreNetworkMovementState(state);
            for (int i = 0; i < 40; i++) movement.SimulateNetworkMovementTick(8, replay: true);
            Assert.Less(body.position.x, center.x, "A hidden original collider cannot make predicted walking ignore the approved wall.");
            state.movementCapabilities = 2; movement.RestoreNetworkMovementState(state);
            for (int i = 0; i < 40; i++) movement.SimulateNetworkMovementTick(8, replay: true);
            Assert.Greater(body.position.x, center.x + 0.5f, "Query geometry keeps the source tag used by acknowledged destructible pass.");
            grid.transform.position += Vector3.right * 3; view.SyncPose(true, Vector2.zero); Physics2D.SyncTransforms();
            Assert.IsNull(Physics2D.OverlapPoint(center, LayerMask.GetMask("Stage")));
            Assert.AreSame(view.Collider, Physics2D.OverlapPoint(center + Vector2.right * 3, LayerMask.GetMask("Stage")));
            view.SetCell(Vector3Int.zero, Tile.ColliderType.None, null, Matrix4x4.identity); view.Refresh(); Physics2D.SyncTransforms();
            Assert.IsNull(Physics2D.OverlapPoint(center + Vector2.right * 3, LayerMask.GetMask("Stage")));
        }
        finally
        {
            view?.Dispose(); NetSync.Mode = oldMode; Object.DestroyImmediate(actor); Object.DestroyImmediate(grid);
            Object.DestroyImmediate(sourceTile); Object.DestroyImmediate(sourcePrefab);
        }
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void CoopPrimitiveQueriesFollowApprovedPoseAndDisableWithoutSceneScripts(int kind)
    {
        var sourceObject = new GameObject("DoorQuerySource"); CoopColliderView view = null;
        try
        {
            sourceObject.layer = LayerMask.NameToLayer("Stage"); sourceObject.tag = "Destructibles";
            sourceObject.transform.position = new Vector3(21000 + kind * 5, 21000);
            Collider2D source = kind == 1 ? sourceObject.AddComponent<BoxCollider2D>() : kind == 2 ? sourceObject.AddComponent<CircleCollider2D>() : sourceObject.AddComponent<CapsuleCollider2D>();
            var body = sourceObject.AddComponent<Rigidbody2D>(); body.bodyType = RigidbodyType2D.Kinematic;
            var frame = CoopColliderFrame.Capture(source); Assert.IsTrue(frame.enabled); Assert.AreEqual(kind, frame.kind);
            source.enabled = false; view = new CoopColliderView(frame, null); Physics2D.SyncTransforms();
            Assert.AreSame(view.Collider, Physics2D.OverlapPoint(frame.position, LayerMask.GetMask("Stage")));
            Assert.IsEmpty(view.Collider.GetComponents<MonoBehaviour>()); Assert.IsNull(view.Collider.GetComponent<Rigidbody2D>());
            Vector3 old = frame.position; sourceObject.transform.position += Vector3.right * 3; source.enabled = true;
            frame = CoopColliderFrame.Capture(source); source.enabled = false; view.Apply(frame); Physics2D.SyncTransforms();
            Assert.IsNull(Physics2D.OverlapPoint(old, LayerMask.GetMask("Stage")));
            Assert.AreSame(view.Collider, Physics2D.OverlapPoint(frame.position, LayerMask.GetMask("Stage")));
            body.simulated = false; source.enabled = true; frame = CoopColliderFrame.Capture(source); Assert.IsFalse(frame.enabled);
            view.Apply(frame); Assert.IsFalse(view.Collider.enabled);
        }
        finally { view?.Dispose(); Object.DestroyImmediate(sourceObject); }
    }

    [Test]
    public void CoopGeometryWaitsForCompleteSnapshotAndFallsBackForUnknownShapes()
    {
        var managerObject = new GameObject("GeometryStateRegression"); managerObject.SetActive(false);
        var viewRoot = new GameObject("GeometryStateViews");
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        try
        {
            var manager = managerObject.AddComponent<BombermanNetworkManager>(); var type = typeof(BombermanNetworkManager);
            type.GetField("coopViewRoot", flags).SetValue(manager, viewRoot); type.GetField("coopCollisionMapsSupported", flags).SetValue(manager, true);
            var apply = type.GetMethod("ApplyCoopCollisionData", flags); var ready = type.GetProperty("CoopGeometryStateReady", flags);
            var frame = new CoopColliderFrame { id = 1, kind = 1, layer = LayerMask.NameToLayer("Stage"), tag = "Untagged", enabled = true,
                position = new Vector3(22000, 22000), scale = Vector3.one, rotation = Quaternion.identity, size = Vector2.one };
            apply.Invoke(manager, new object[] { new CoopCollisions { reset = true, frames = new[] { frame } } }); Assert.IsFalse((bool)ready.GetValue(manager));
            apply.Invoke(manager, new object[] { new CoopCollisions { complete = true } }); Assert.IsTrue((bool)ready.GetValue(manager));
            var unsupported = frame; unsupported.id = 2; unsupported.kind = 0;
            apply.Invoke(manager, new object[] { new CoopCollisions { complete = true, frames = new[] { unsupported } } }); Assert.IsFalse((bool)ready.GetValue(manager));
            apply.Invoke(manager, new object[] { new CoopCollisions { complete = true, removed = new[] { 1, 2 } } }); Assert.IsTrue((bool)ready.GetValue(manager));
            var views = (System.Collections.IDictionary)type.GetField("coopCollisionViews", flags).GetValue(manager); Assert.AreEqual(0, views.Count);
            apply.Invoke(manager, new object[] { new CoopCollisions { reset = true } }); Assert.IsFalse((bool)ready.GetValue(manager));
        }
        finally { Object.DestroyImmediate(managerObject); if (viewRoot != null) Object.DestroyImmediate(viewRoot); }
    }

    [Test]
    public void CoopTileGeometryAndPrimitiveFramesRoundTripWithCompletionAndShapeChanges()
    {
        var frame = new CoopTileFrame { cell = new Vector3Int(1, 2), tile = 7, collisionType = (byte)Tile.ColliderType.Grid,
            collisionSprite = -1, matrix = Matrix4x4.identity };
        var changed = frame; changed.collisionType = (byte)Tile.ColliderType.None; Assert.IsFalse(frame.Matches(changed));
        var tiles = new CoopTiles { scene = "Stage_1-1", path = "Grid/Destructibles", reset = true, complete = true,
            collisionEnabled = true, collisionOffset = Vector2.right * 0.1f, cells = new[] { frame } };
        using var writer = NetworkWriterPool.Get(); writer.Write(tiles);
        using var reader = NetworkReaderPool.Get(writer.ToArraySegment()); var copy = reader.Read<CoopTiles>();
        Assert.IsTrue(copy.complete); Assert.IsTrue(copy.collisionEnabled); Assert.AreEqual(tiles.collisionOffset, copy.collisionOffset);
        Assert.IsTrue(frame.Matches(copy.cells[0]));
        var shape = new CoopColliderFrame { id = 12, kind = 3, enabled = true, scale = Vector3.one, rotation = Quaternion.identity, size = Vector2.one, tag = "Untagged" };
        using var shapeWriter = NetworkWriterPool.Get(); shapeWriter.Write(new CoopCollisions { reset = true, complete = true, frames = new[] { shape } });
        using var shapeReader = NetworkReaderPool.Get(shapeWriter.ToArraySegment()); var shapeCopy = shapeReader.Read<CoopCollisions>();
        Assert.IsTrue(shapeCopy.complete); Assert.IsTrue(shape.Matches(shapeCopy.frames[0]));
    }

    [Test]
    public void BlackDashPredictionIsBoundedConfirmsHoldAndReleasesThroughSnapshot()
    {
        var oldMode = NetSync.Mode; var actor = new GameObject("BlackPredictionOwner"); actor.SetActive(false);
        var mount = new GameObject("BlackPredictionMount"); mount.transform.SetParent(actor.transform, false);
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var clientState = typeof(NetworkClient).GetField("connectState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var oldClientState = clientState.GetValue(null); NetworkBombFx fx = null;
        try
        {
            NetSync.Mode = NetSync.NetMode.Client; clientState.SetValue(null, System.Enum.Parse(clientState.FieldType, "Connected"));
            actor.tag = "Player"; actor.AddComponent<BoxCollider2D>();
            var identity = actor.AddComponent<NetworkIdentity>(); fx = actor.AddComponent<NetworkBombFx>();
            typeof(NetworkIdentity).GetMethod("InitializeNetworkBehaviours", flags).Invoke(identity, null);
            typeof(NetworkIdentity).GetProperty("isLocalPlayer").SetValue(identity, true);
            var movement = actor.GetComponent<MovementController>(); var body = actor.GetComponent<Rigidbody2D>(); body.bodyType = RigidbodyType2D.Kinematic;
            typeof(MovementController).GetProperty(nameof(MovementController.Rigidbody)).SetValue(movement, body);
            var companion = actor.AddComponent<PlayerMountCompanion>();
            typeof(PlayerMountCompanion).GetField("currentLouie", flags).SetValue(companion, mount);
            typeof(PlayerMountCompanion).GetField("mountedType", flags).SetValue(companion, MountedType.Black);
            actor.SetActive(true); movement.SetNetworkTickDriven(true); Vector2 origin = new(17000, 17000);
            var state = new MovementController.NetworkMovementState { position = origin, mount = MountedType.Black,
                facing = Vector2.right, worldSpeed = 4, animationSpeed = PlayerPersistentStats.BaseSpeedNormal, canPredict = true,
                serverTime = NetworkTime.predictedTime };
            movement.RestoreNetworkMovementState(state); movement.SetPredictLocally(true);
            var actions = new NetworkBombFx.MountActionState { blackEligible = true };
            fx.ReconcileBlackDash(0, state, actions); Assert.IsTrue(fx.TryPredictBlackDash(8, 10));
            Assert.AreEqual("ability_motion", MotionDiagnosticProbe.ClassifyPredictionBlock(movement, null, fx));
            var update = typeof(NetworkBombFx).GetMethod("UpdateBlackDash", flags);
            for (int i = 0; i < 10; i++) update.Invoke(fx, new object[] { 0.02f, 0.02f });
            Assert.Greater(body.position.x, origin.x); Assert.LessOrEqual(body.position.x, origin.x + 0.92f);
            Assert.IsFalse(actor.GetComponent<BlackLouieDashPushAbility>().DashActive); Assert.IsFalse(actor.GetComponent<CharacterHealth>().IsInvulnerable);
            movement.RestoreNetworkMovementState(state); fx.ReconcileBlackDash(10, state, actions); Assert.IsFalse(fx.BlackDashActive);
            movement.SetPredictLocally(true); Assert.IsTrue(fx.TryPredictBlackDash(8, 11));
            update.Invoke(fx, new object[] { 0.02f, 0.02f }); Vector2 before = body.position;
            var motion = new NetworkBombFx.BlackDashMotion { revision = 1, inputTick = 11, start = origin, position = origin,
                direction = Vector2.right, distance = 3, duration = 0.5f, cooldown = 0.6f, serverTime = NetworkBombFx.KickServerNow, active = true, first = true };
            fx.ReceiveBlackDash(motion); Assert.AreEqual(before, body.position, "First acceptance preserves the owner's phase.");
            motion.revision = 2; motion.holding = true; motion.first = false; motion.position = origin + Vector2.right * 0.4f;
            fx.ReceiveBlackDash(motion); update.Invoke(fx, new object[] { 0.1f, 0.1f }); Assert.AreEqual(motion.position, body.position);
            Assert.IsTrue(fx.BlackDashOwnsPose); motion.revision = 3; motion.active = false; fx.ReceiveBlackDash(motion);
            Assert.IsTrue(fx.BlackDashActive, "Keep the final position until a post-dash snapshot hands control back to the motor.");
            Assert.IsFalse(fx.BlackDashOwnsPose);
            state.position = motion.position; movement.RestoreNetworkMovementState(state); fx.ReconcileBlackDash(11, state, actions);
            Assert.IsFalse(fx.BlackDashActive); Assert.AreEqual(motion.position, body.position); Assert.IsFalse(movement.InputLocked);
            state.serverTime = NetworkTime.predictedTime - 0.2; actions.blackCooldown = 0.3f;
            fx.ReconcileBlackDash(12, state, actions);
            float delay = (float)typeof(NetworkBombFx).GetField("blackNextAllowed", flags).GetValue(fx) - Time.time;
            Assert.That(delay, Is.InRange(0.08f, 0.12f), "Cooldown readiness is projected to the next input's estimated server validation time.");
            var shortDash = motion; shortDash.inputTick = 20; shortDash.active = true; shortDash.holding = false;
            shortDash.position = shortDash.start = origin; shortDash.distance = 0.25f; shortDash.duration = 0.1f; shortDash.serverTime = NetworkBombFx.KickServerNow;
            Assert.IsTrue(fx.BeginBlackDashPrediction(shortDash, true));
            for (int i = 0; i < 20; i++) update.Invoke(fx, new object[] { 0.02f, 0.02f });
            Assert.That(body.position.x, Is.EqualTo(origin.x + 0.25f).Within(0.01f), "A confirmed finite dash cannot drift beyond its validated range.");
            NetSync.Mode = NetSync.NetMode.Host; Assert.IsFalse(fx.TryPredictBlackDash(8, 13));
            using var writer = NetworkWriterPool.Get(); writer.Write(motion);
            using var reader = NetworkReaderPool.Get(writer.ToArraySegment()); var wire = reader.Read<NetworkBombFx.BlackDashMotion>();
            Assert.AreEqual(motion.inputTick, wire.inputTick); Assert.AreEqual(motion.position, wire.position); Assert.IsTrue(wire.holding);
        }
        finally
        {
            if (fx != null) typeof(NetworkBombFx).GetMethod("ClearBlackDash", flags).Invoke(fx, new object[] { "test_cleanup" });
            NetSync.Mode = oldMode; clientState.SetValue(null, oldClientState); Object.DestroyImmediate(actor);
        }
    }

    [Test]
    public void BlackDashPreviewUsesConfirmedPassAndNeverPushesOrStunsTargets()
    {
        var actor = new GameObject("BlackPreviewActor"); actor.SetActive(false); var wall = new GameObject("BlackPreviewWall");
        var enemy = new GameObject("BlackPreviewEnemy"); enemy.SetActive(false);
        try
        {
            actor.tag = "Player"; actor.AddComponent<BoxCollider2D>(); var ability = actor.AddComponent<BlackLouieDashPushAbility>();
            actor.GetComponent<MovementController>().obstacleMask = LayerMask.GetMask("Stage");
            Vector2 origin = new(18000, 18000); actor.transform.position = origin;
            wall.layer = LayerMask.NameToLayer("Stage"); wall.tag = "Destructibles"; wall.transform.position = origin + Vector2.right * 1.5f;
            wall.AddComponent<BoxCollider2D>(); Physics2D.SyncTransforms();
            Assert.IsTrue(ability.TryPreviewNetworkDashStep(origin, Vector2.right, 5, false, out var blocked));
            Assert.Less(blocked.x, origin.x + 1.1f);
            Assert.IsTrue(ability.TryPreviewNetworkDashStep(origin, Vector2.right, 5, true, out var passed));
            Assert.Greater(passed.x, origin.x + 1.5f); Assert.LessOrEqual(passed.x, origin.x + 2.01f);
            enemy.layer = LayerMask.NameToLayer("Enemy"); enemy.transform.position = origin + Vector2.right * 1.5f;
            enemy.AddComponent<BoxCollider2D>(); var body = enemy.AddComponent<Rigidbody2D>(); body.bodyType = RigidbodyType2D.Kinematic;
            var receiver = enemy.AddComponent<StunReceiver>(); enemy.SetActive(true); Physics2D.SyncTransforms();
            ability.TryPreviewNetworkDashStep(origin, Vector2.right, 5, true, out blocked);
            Assert.Less(blocked.x, origin.x + 1.1f); Assert.IsFalse(receiver.IsStunned);
            Assert.AreEqual(origin + Vector2.right * 1.5f, body.position); Assert.IsFalse(ability.DashActive);
        }
        finally { Object.DestroyImmediate(actor); Object.DestroyImmediate(wall); Object.DestroyImmediate(enemy); }
    }

    [Test]
    public void NetworkBlackDashPositionIsImmediatelyCoherentWithMovementSnapshot()
    {
        var oldMode = NetSync.Mode; var actor = new GameObject("BlackAuthoritativePosition"); actor.SetActive(false);
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        try
        {
            NetSync.Mode = NetSync.NetMode.Host; actor.AddComponent<BoxCollider2D>();
            var identity = actor.AddComponent<NetworkIdentity>(); typeof(NetworkIdentity).GetProperty("netId").SetValue(identity, 123u);
            var ability = actor.AddComponent<BlackLouieDashPushAbility>(); var movement = actor.GetComponent<MovementController>();
            var body = actor.GetComponent<Rigidbody2D>(); body.bodyType = RigidbodyType2D.Kinematic;
            typeof(MovementController).GetProperty(nameof(MovementController.Rigidbody)).SetValue(movement, body);
            typeof(BlackLouieDashPushAbility).GetField("rb", flags).SetValue(ability, body);
            body.linearVelocity = Vector2.right * 2; Vector2 target = new(19000, 19000);
            typeof(BlackLouieDashPushAbility).GetMethod("MoveDashBody", flags).Invoke(ability, new object[] { target });
            Assert.AreEqual(target, body.position); Assert.AreEqual(target, (Vector2)actor.transform.position);
            Assert.AreEqual(target, movement.CaptureNetworkMovementState().position); Assert.AreEqual(Vector2.zero, body.linearVelocity);
        }
        finally { NetSync.Mode = oldMode; Object.DestroyImmediate(actor); }
    }

    [Test]
    public void TankPredictionUsesVisualProxyBoundsTravelConfirmsPhaseAndRejects()
    {
        var oldMode = NetSync.Mode; var actor = new GameObject("TankPredictionOwner"); actor.SetActive(false);
        var mount = new GameObject("TankPredictionMount"); mount.transform.SetParent(actor.transform, false);
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var clientState = typeof(NetworkClient).GetField("connectState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var oldClientState = clientState.GetValue(null); NetworkBombFx fx = null;
        try
        {
            NetSync.Mode = NetSync.NetMode.Client; clientState.SetValue(null, System.Enum.Parse(clientState.FieldType, "Connected"));
            actor.tag = "Player"; actor.AddComponent<BoxCollider2D>();
            var identity = actor.AddComponent<NetworkIdentity>(); fx = actor.AddComponent<NetworkBombFx>();
            typeof(NetworkIdentity).GetMethod("InitializeNetworkBehaviours", flags).Invoke(identity, null);
            typeof(NetworkIdentity).GetProperty("isLocalPlayer").SetValue(identity, true);
            var movement = actor.GetComponent<MovementController>(); var body = actor.GetComponent<Rigidbody2D>(); body.bodyType = RigidbodyType2D.Kinematic;
            typeof(MovementController).GetProperty(nameof(MovementController.Rigidbody)).SetValue(movement, body);
            var companion = actor.AddComponent<PlayerMountCompanion>();
            typeof(PlayerMountCompanion).GetField("currentLouie", flags).SetValue(companion, mount);
            typeof(PlayerMountCompanion).GetField("mountedType", flags).SetValue(companion, MountedType.Tank);
            actor.SetActive(true); movement.SetNetworkTickDriven(true);
            var state = new MovementController.NetworkMovementState { position = new Vector2(15000, 15000), mount = MountedType.Tank,
                facing = Vector2.right, worldSpeed = 4, animationSpeed = PlayerPersistentStats.BaseSpeedNormal, canPredict = true };
            movement.RestoreNetworkMovementState(state); movement.SetPredictLocally(true);
            Assert.IsFalse(fx.TryPredictTankShot(8, 10), "A confirmed ready state is required after mounting.");
            var ready = new NetworkBombFx.MountActionState { tankReady = true, tankEligible = true };
            fx.ReconcileTankShot(0, ready, state); Assert.IsTrue(fx.TryPredictTankShot(8, 10));
            var proxy = (SpriteRenderer)typeof(NetworkBombFx).GetField("tankProxy", flags).GetValue(fx);
            Assert.IsNotNull(proxy); Assert.AreEqual(2, proxy.GetComponents<Component>().Length);
            Assert.IsNull(proxy.GetComponent<TankShot>()); Assert.IsNull(proxy.GetComponent<Collider2D>());
            Assert.IsNull(proxy.GetComponent<NetworkIdentity>()); Assert.IsNull(proxy.GetComponent<Rigidbody2D>());
            Vector2 start = proxy.transform.position; Assert.IsTrue(fx.AbilityPredictionLocksMovement); Assert.IsFalse(movement.InputLocked);
            Assert.AreEqual("ability_pose", MotionDiagnosticProbe.ClassifyPredictionBlock(movement, null, fx), "Intentional owner pose waits are not free movement latency.");
            Assert.IsFalse(actor.GetComponent<TankMountShootAbility>().Running);
            var update = typeof(NetworkBombFx).GetMethod("UpdateTankPrediction", flags);
            for (int i = 0; i < 10; i++) update.Invoke(fx, new object[] { 0.02f });
            Assert.Greater(proxy.transform.position.x, start.x); Assert.LessOrEqual(proxy.transform.position.x, start.x + 1.21f);
            fx.ReconcileTankShot(10, ready, state); Assert.IsNull(typeof(NetworkBombFx).GetField("tankProxy", flags).GetValue(fx));
            Assert.IsFalse(fx.TankShotPoseActive); Assert.IsTrue(fx.TryPredictTankShot(8, 11));
            update.Invoke(fx, new object[] { 0.1f }); float before = (float)typeof(NetworkBombFx).GetField("tankAge", flags).GetValue(fx);
            fx.ReceiveTankShot(11, 999, Vector2.right, 0.25f, 10, NetworkBombFx.KickServerNow);
            Assert.AreEqual(before, typeof(NetworkBombFx).GetField("tankAge", flags).GetValue(fx));
            fx.ReconcileTankShot(11, new NetworkBombFx.MountActionState { tankCooldown = 10 }, state);
            Assert.IsFalse(fx.TryPredictTankShot(8, 12)); update.Invoke(fx, new object[] { 1.1f });
            Assert.IsNull(typeof(NetworkBombFx).GetField("tankProxy", flags).GetValue(fx), "A missing spawn cannot leave a confirmed proxy forever.");
            var cooling = new NetworkBombFx.MountActionState { tankEligible = true, tankCooldown = 0.3f };
            state.serverTime = NetworkTime.predictedTime - 0.2; fx.ReconcileTankShot(12, cooling, state);
            float delay = (float)typeof(NetworkBombFx).GetField("tankNextAllowed", flags).GetValue(fx) - Time.time;
            Assert.That(delay, Is.InRange(0.08f, 0.12f), "Snapshot age must not be added again to the cooldown.");
            Assert.IsFalse(fx.TryPredictTankShot(8, 12));
            state.serverTime = NetworkTime.predictedTime - 0.4; fx.ReconcileTankShot(12, cooling, state);
            Assert.IsTrue(fx.TryPredictTankShot(8, 12), "An eligible Tank can preview when the confirmed cooldown expires, without waiting for another ready packet.");
            fx.ReconcileTankShot(12, default, state);
            Assert.IsFalse(fx.TryPredictTankShot(8, 13), "Zero cooldown alone cannot grant eligibility lost through disable, cast or mount transition.");
            var tank = actor.GetComponent<TankMountShootAbility>();
            typeof(TankMountShootAbility).GetField("cooldownUntil", flags).SetValue(tank, Time.time + 0.3f);
            var captured = fx.CaptureMountActionState(); Assert.IsTrue(captured.tankEligible); Assert.IsFalse(captured.tankReady);
            tank.Disable(); Assert.IsFalse(fx.CaptureMountActionState().tankEligible);
            actor.GetComponent<CharacterHealth>().life = 0;
            fx.ReceiveTankShot(12, 999, Vector2.right, 0.25f, 10, NetworkBombFx.KickServerNow);
            Assert.IsFalse(fx.TankShotPoseActive, "A late receipt cannot resurrect the dead owner's firing pose.");
            NetSync.Mode = NetSync.NetMode.Host; Assert.IsFalse(fx.TryPredictTankShot(8, 13));
            using var writer = NetworkWriterPool.Get(); writer.Write(ready);
            using var reader = NetworkReaderPool.Get(writer.ToArraySegment()); var restored = reader.Read<NetworkBombFx.MountActionState>();
            Assert.IsTrue(restored.tankReady); Assert.IsTrue(restored.tankEligible);
        }
        finally
        {
            if (fx != null) typeof(NetworkBombFx).GetMethod("ClearTankPrediction", flags).Invoke(fx, new object[] { "test_cleanup" });
            NetSync.Mode = oldMode; clientState.SetValue(null, oldClientState); Object.DestroyImmediate(actor);
        }
    }

    [Test]
    public void TankTravelPreviewStopsAtKnownGeometryWithoutImpactOrWorldSimulation()
    {
        var owner = new GameObject("TankPreviewOwner"); var wall = new GameObject("TankPreviewWall");
        try
        {
            Vector2 start = new(16000, 16000); owner.transform.position = start;
            owner.layer = wall.layer = LayerMask.NameToLayer("Stage"); owner.AddComponent<BoxCollider2D>();
            wall.transform.position = start + Vector2.right; wall.AddComponent<BoxCollider2D>(); Physics2D.SyncTransforms();
            int shots = Object.FindObjectsByType<TankShot>().Length;
            var result = TankShot.PreviewNetworkTravel(start, start + Vector2.right * 3, Vector2.one * 0.25f,
                LayerMask.GetMask("Stage"), owner, new RaycastHit2D[16]);
            Assert.Greater(result.x, start.x); Assert.Less(result.x, start.x + 0.5f);
            Assert.AreEqual(start.y, result.y); Assert.AreEqual(shots, Object.FindObjectsByType<TankShot>().Length);
            Assert.IsFalse(wall.GetComponent<BoxCollider2D>() == null);
        }
        finally { Object.DestroyImmediate(owner); Object.DestroyImmediate(wall); }
    }

    [Test]
    public void TankConfirmedPresentationMovesOnlyChildAndFreezesAcrossRootUpdatesDuringPause()
    {
        var oldMode = NetSync.Mode; var actor = new GameObject("TankVisualOwner"); actor.SetActive(false);
        var projectile = new GameObject("TankVisualProjectile"); projectile.SetActive(false);
        var child = new GameObject("TankVisualSprite", typeof(SpriteRenderer)); child.transform.SetParent(projectile.transform, false);
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var clientState = typeof(NetworkClient).GetField("connectState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var oldClientState = clientState.GetValue(null);
        var paused = typeof(GamePauseController).GetProperty(nameof(GamePauseController.IsPaused)); bool wasPaused = GamePauseController.IsPaused;
        try
        {
            NetSync.Mode = NetSync.NetMode.Client; clientState.SetValue(null, System.Enum.Parse(clientState.FieldType, "Connected"));
            actor.AddComponent<BoxCollider2D>();
            var identity = actor.AddComponent<NetworkIdentity>(); var fx = actor.AddComponent<NetworkBombFx>();
            typeof(NetworkIdentity).GetMethod("InitializeNetworkBehaviours", flags).Invoke(identity, null);
            typeof(NetworkIdentity).GetProperty("isLocalPlayer").SetValue(identity, true);
            projectile.AddComponent<BoxCollider2D>(); var projectileIdentity = projectile.AddComponent<NetworkIdentity>();
            var shot = projectile.AddComponent<NetworkTankShot>();
            typeof(NetworkIdentity).GetMethod("InitializeNetworkBehaviours", flags).Invoke(projectileIdentity, null);
            typeof(NetworkTankShot).GetField("ownerFx", flags).SetValue(shot, fx);
            typeof(NetworkTankShot).GetField("direction", flags).SetValue(shot, Vector2.right);
            typeof(NetworkTankShot).GetField("launchSpeed", flags).SetValue(shot, 8f);
            typeof(NetworkTankShot).GetField("launchTime", flags).SetValue(shot, NetworkBombFx.KickServerNow);
            Assert.IsNotNull(shot.netIdentity, "Projectile fixture identity must be bound before presentation.");
            Assert.IsNotNull(fx.netIdentity, "Player fixture identity must be bound before presentation.");
            Assert.IsFalse(shot.isServer); Assert.AreEqual(NetSync.NetMode.Client, NetSync.Mode);
            Assert.IsTrue(NetworkClient.active); Assert.IsTrue(fx.isLocalPlayer);
            Assert.AreSame(fx, typeof(NetworkTankShot).GetField("ownerFx", flags).GetValue(shot));
            Assert.AreSame(child.GetComponent<SpriteRenderer>(), shot.GetComponentInChildren<SpriteRenderer>(true));
            Assert.IsTrue(shot.AdoptOwnerPresentation(Vector2.right * 0.8f));
            Assert.AreEqual(Vector3.zero, projectile.transform.position); Assert.That(child.transform.position.x, Is.EqualTo(0.8f).Within(0.001f));
            Vector3 displayed = child.transform.position; paused.SetValue(null, true); projectile.transform.position = Vector3.right * 10;
            Assert.AreEqual("paused", MotionDiagnosticProbe.ClassifyPredictionBlock(actor.GetComponent<MovementController>(), null, fx));
            typeof(NetworkTankShot).GetMethod("LateUpdate", flags).Invoke(shot, null);
            Assert.Less(Vector3.Distance(displayed, child.transform.position), 0.00001f); Assert.AreEqual(Vector3.right * 10, projectile.transform.position);
            paused.SetValue(null, false); typeof(NetworkTankShot).GetField("launchTime", flags).SetValue(shot, NetworkBombFx.KickServerNow - 2);
            typeof(NetworkTankShot).GetMethod("LateUpdate", flags).Invoke(shot, null);
            Assert.IsTrue(shot.OwnerPresentationActive, "Resuming a long pause waits for a fresh approved sample instead of treating the pause as packet loss.");
            Assert.Less(Vector3.Distance(displayed, child.transform.position), 0.00001f);
            shot.ReleaseOwnerPresentation(); Assert.AreEqual(Vector3.zero, child.transform.localPosition); Assert.IsFalse(shot.OwnerPresentationActive);
        }
        finally
        {
            paused.SetValue(null, wasPaused); NetSync.Mode = oldMode; clientState.SetValue(null, oldClientState);
            Object.DestroyImmediate(projectile); Object.DestroyImmediate(actor);
        }
    }

    [Test]
    public void ConveyorMotorReplaysTurnLockAndHistoricalPhaseWithoutDoublePush()
    {
        var oldMode = NetSync.Mode;
        var root = new GameObject("PredictionConveyor"); root.SetActive(false);
        var grid = new GameObject("PredictionConveyorGrid", typeof(Grid)); grid.transform.position = new Vector3(12000, 12000);
        var groundObject = new GameObject("PredictionConveyorGround", typeof(UnityEngine.Tilemaps.Tilemap)); groundObject.transform.SetParent(grid.transform, false);
        var wallObject = new GameObject("PredictionConveyorWall", typeof(UnityEngine.Tilemaps.Tilemap)); wallObject.transform.SetParent(grid.transform, false);
        var player = new GameObject("ConveyorOwner"); player.SetActive(false);
        var tile = ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        try
        {
            NetSync.Mode = NetSync.NetMode.Host;
            var conveyor = root.AddComponent<BattleMode5ConveyorController>();
            var ground = groundObject.GetComponent<UnityEngine.Tilemaps.Tilemap>(); var walls = wallObject.GetComponent<UnityEngine.Tilemaps.Tilemap>();
            typeof(BattleMode5ConveyorController).GetField("groundTilemap", flags).SetValue(conveyor, ground);
            typeof(BattleMode5ConveyorController).GetField("indestructibleTilemap", flags).SetValue(conveyor, walls);
            var segmentField = typeof(BattleMode5ConveyorController).GetField("directedSegments", flags);
            var segmentType = segmentField.FieldType.GetElementType(); var segments = System.Array.CreateInstance(segmentType, 3);
            void Segment(int index, Vector3Int start, Vector3Int end, string direction, string reverse)
            {
                var segment = System.Activator.CreateInstance(segmentType);
                segmentType.GetField("startCell").SetValue(segment, start); segmentType.GetField("endCell").SetValue(segment, end);
                var field = segmentType.GetField("direction"); field.SetValue(segment, System.Enum.Parse(field.FieldType, direction));
                segmentType.GetField("reverseDirection").SetValue(segment, System.Enum.Parse(field.FieldType, reverse)); segments.SetValue(segment, index);
            }
            Segment(0, new Vector3Int(0, 0), new Vector3Int(1, 0), "Right", "Left");
            Segment(1, new Vector3Int(2, 0), new Vector3Int(2, 0), "Down", "Left");
            Segment(2, new Vector3Int(2, -1), new Vector3Int(2, -3), "Down", "Up");
            segmentField.SetValue(conveyor, segments);
            root.SetActive(true); typeof(BattleMode5ConveyorController).GetMethod("Awake", flags).Invoke(conveyor, null);
            player.tag = "Player"; player.AddComponent<BoxCollider2D>();
            var body = player.AddComponent<Rigidbody2D>(); body.bodyType = RigidbodyType2D.Kinematic;
            var movement = player.AddComponent<MovementController>();
            typeof(MovementController).GetProperty(nameof(MovementController.Rigidbody)).SetValue(movement, body);
            player.SetActive(true); movement.SetNetworkTickDriven(true);
            Vector2 origin = ground.GetCellCenterWorld(new Vector3Int(1, 0));
            var state = new MovementController.NetworkMovementState { position = origin, facing = Vector2.right,
                worldSpeed = 4, baseWorldSpeed = 4, speedMultiplier = 1, animationSpeed = PlayerPersistentStats.BaseSpeedNormal, canPredict = true,
                conveyor = conveyor.CaptureNetworkMotor(movement, default) };
            NetSync.Mode = NetSync.NetMode.Client; movement.RestoreNetworkMovementState(state); movement.SetPredictLocally(true);
            Assert.IsTrue(movement.CanPredictNetworkMovement);
            for (int i = 0; i < 22; i++) movement.SimulateNetworkMovementTick(0);
            var ack = movement.CaptureNetworkMovementState(); Assert.IsTrue(ack.conveyor.hasTarget);
            Assert.AreEqual(new Vector3Int(2, 0), ack.conveyor.target);
            Assert.AreEqual(Vector2.right, ack.conveyor.incoming);
            for (int i = 0; i < 38; i++) movement.SimulateNetworkMovementTick(0);
            var expected = movement.CaptureNetworkMovementState();
            Assert.That(expected.position.x, Is.EqualTo(origin.x + 1).Within(0.01f)); Assert.Less(expected.position.y, origin.y);
            conveyor.ApplyNetworkPhase(new ConveyorPhase { scene = root.scene.name, path = conveyor.NetworkPath, revision = 1, clockwise = false, fast = true });
            movement.RestoreNetworkMovementState(ack);
            for (int i = 0; i < 38; i++) movement.SimulateNetworkMovementTick(0, replay: true);
            Assert.AreEqual(JsonUtility.ToJson(expected), JsonUtility.ToJson(movement.CaptureNetworkMovementState()), "A newer world phase cannot rewrite the historical turn during replay.");
            Vector2 before = body.position;
            NetSync.Mode = NetSync.NetMode.Host; typeof(BattleMode5ConveyorController).GetMethod("FixedUpdate", flags).Invoke(conveyor, null);
            Assert.AreEqual(before, body.position, "The arena loop must not apply a second conveyor push to a tick-driven actor.");
            NetSync.Mode = NetSync.NetMode.Client; walls.SetTile(new Vector3Int(2, 0), tile); movement.RestoreNetworkMovementState(state);
            for (int i = 0; i < 10; i++) movement.SimulateNetworkMovementTick(0);
            Assert.AreEqual(origin, body.position, "Known blocked exits stop at the source center.");
            state.conveyor = default; movement.RestoreNetworkMovementState(state);
            Assert.AreEqual("conveyor_context_pending", movement.NetworkPredictionBlockReason);
            using var writer = NetworkWriterPool.Get(); writer.Write(ack);
            using var reader = NetworkReaderPool.Get(writer.ToArraySegment()); var received = reader.Read<MovementController.NetworkMovementState>();
            Assert.AreEqual(ack.conveyor.path, received.conveyor.path); Assert.AreEqual(ack.conveyor.target, received.conveyor.target);
        }
        finally { NetSync.Mode = oldMode; Object.DestroyImmediate(player); Object.DestroyImmediate(root); Object.DestroyImmediate(grid); Object.DestroyImmediate(tile); }
    }

    [Test]
    public void ReplayExpiresConfirmedSpeedBySimulationTicks()
    {
        var oldMode = NetSync.Mode; var player = new GameObject("SpeedExpiryReplay"); player.SetActive(false);
        try
        {
            NetSync.Mode = NetSync.NetMode.Client; player.tag = "Player"; player.AddComponent<BoxCollider2D>();
            var body = player.AddComponent<Rigidbody2D>(); body.bodyType = RigidbodyType2D.Kinematic;
            var movement = player.AddComponent<MovementController>();
            typeof(MovementController).GetProperty(nameof(MovementController.Rigidbody)).SetValue(movement, body);
            typeof(MovementController).GetField("useIntegerPixelSteps", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(movement, false);
            player.SetActive(true); movement.SetNetworkTickDriven(true); movement.SetPredictLocally(true);
            var state = new MovementController.NetworkMovementState { position = new Vector2(13000, 13000), facing = Vector2.right,
                worldSpeed = 2, baseWorldSpeed = 4, speedMultiplier = 0.5f, speedModifierRemaining = Time.fixedDeltaTime * 2,
                animationSpeed = PlayerPersistentStats.BaseSpeedNormal, canPredict = true };
            movement.RestoreNetworkMovementState(state);
            for (int i = 0; i < 3; i++) movement.SimulateNetworkMovementTick(8);
            var expected = movement.CaptureNetworkMovementState();
            Assert.That(expected.position.x - state.position.x, Is.EqualTo(8 * Time.fixedDeltaTime).Within(0.005f));
            Assert.AreEqual(0, expected.speedModifierRemaining); Assert.AreEqual(4, expected.worldSpeed);
            movement.RestoreNetworkMovementState(state);
            for (int i = 0; i < 3; i++) movement.SimulateNetworkMovementTick(8, replay: true);
            Assert.AreEqual(JsonUtility.ToJson(expected), JsonUtility.ToJson(movement.CaptureNetworkMovementState()));
        }
        finally { NetSync.Mode = oldMode; Object.DestroyImmediate(player); }
    }

    [Test]
    public void BattleCountdownWaitsForBothSceneReadyAndPresentedScreens()
    {
        var first = new NetworkConnectionToClient(101) { isAuthenticated = true, isReady = true };
        var second = new NetworkConnectionToClient(102) { isAuthenticated = true, isReady = true };
        var peers = new[] { first, second };
        var presented = new HashSet<int> { 101 };
        var check = typeof(BombermanNetworkManager).GetMethod("AreBattleScreensReady",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.IsFalse((bool)check.Invoke(null, new object[] { peers, presented }), "The slower rendered screen must hold the round.");
        presented.Add(102);
        Assert.IsTrue((bool)check.Invoke(null, new object[] { peers, presented }));
        second.isReady = false;
        Assert.IsFalse((bool)check.Invoke(null, new object[] { peers, presented }), "An acknowledgement cannot bypass Mirror scene readiness.");
        Assert.IsFalse((bool)check.Invoke(null, new object[] { new NetworkConnectionToClient[0], presented }));
    }

    [Test]
    public void BattleReadinessAndCountdownResetWhenReloadingSameArena()
    {
        var go = new GameObject("BattleStartResetRegression");
        go.SetActive(false);
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        try
        {
            var manager = go.AddComponent<BombermanNetworkManager>();
            var type = typeof(BombermanNetworkManager);
            var ready = (HashSet<int>)type.GetField("battleScreenReadyPeers", flags).GetValue(manager);
            ready.Add(101);
            type.GetField("battleReleaseTime", flags).SetValue(manager, 123.0);
            type.GetMethod("ResetBattleStart", flags).Invoke(manager, new object[] { "BattleMode_1" });
            Assert.IsEmpty(ready);
            Assert.AreEqual(0.0, type.GetField("battleReleaseTime", flags).GetValue(manager));
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void TankShotPrefabHasAuthoritativeTransformAndPresentation()
    {
        var prefab = Resources.Load<GameObject>("Tank/Shot");
        Assert.IsNotNull(prefab.GetComponent<NetworkIdentity>());
        Assert.IsNotNull(prefab.GetComponent<NetworkTankShot>());
        var transform = prefab.GetComponent<NetworkTransformReliable>();
        Assert.IsNotNull(transform);
        Assert.AreEqual(SyncDirection.ServerToClient, transform.syncDirection);
    }

    [Test]
    public void BattleAudioMessageKeepsExplosionExclusivitySeparateFromVoices()
    {
        using var writer = NetworkWriterPool.Get();
        writer.Write(new CoopSound { scene = "BattleMode_1", clip = 12, exclusiveExplosion = true, volume = 0.7f, pitch = 1.1f });
        using var reader = NetworkReaderPool.Get(writer.ToArraySegment());
        var result = reader.Read<CoopSound>();
        Assert.IsTrue(result.exclusiveExplosion);
        Assert.IsFalse(result.voice);
        Assert.AreEqual(12, result.clip);
        Assert.AreEqual(0.7f, result.volume);
        Assert.AreEqual(1.1f, result.pitch);
    }

    [Test]
    public void BattleFeedbackPreservesAuthoritativePathAndExpulsionParameters()
    {
        var path = new[] { new Vector3Int(-6, 4, 0), new Vector3Int(-5, 4, 0) };
        using var writer = NetworkWriterPool.Get();
        writer.Write(new BattleFeedback { scene = "BattleMode_1", kind = 6, netId = 42,
            direction = Vector2.left, value = 1.25f, duration = 2.75f, steps = 3, cell = path[0], path = path });
        using var reader = NetworkReaderPool.Get(writer.ToArraySegment());
        var result = reader.Read<BattleFeedback>();
        Assert.AreEqual("BattleMode_1", result.scene);
        Assert.AreEqual(42u, result.netId);
        Assert.AreEqual(6, result.kind);
        Assert.AreEqual(Vector2.left, result.direction);
        Assert.AreEqual(1.25f, result.value);
        Assert.AreEqual(2.75f, result.duration);
        Assert.AreEqual(3, result.steps);
        Assert.AreEqual(path[0], result.cell);
        CollectionAssert.AreEqual(path, result.path);
    }

    [Test]
    public void BattleSoundCatalogResolvesSerializedClipsWithoutCoopScene()
    {
        var catalog = Resources.Load<CoopSceneCatalog>("OnlineBattleAudio");
        Assert.IsNotNull(catalog);
        var go = new GameObject("BattleAudioRegression");
        go.SetActive(false);
        try
        {
            var manager = go.AddComponent<BombermanNetworkManager>();
            var resolve = typeof(BombermanNetworkManager).GetMethod("CoopSoundId",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Sounds", "Assets/Resources/Sounds" }))
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                int id = (int)resolve.Invoke(manager, new object[] { clip });
                Assert.GreaterOrEqual(id, 0, path);
                Assert.AreSame(clip, catalog.sounds[id], path);
            }
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void EditorWaitsForCompatibleProtocolAndRecoversAfterDeployment()
    {
        var go = new GameObject("VersionBlockedEditorBrowser");
        go.SetActive(false);
        try
        {
            var browser = go.AddComponent<OnlineRoomBrowser>();
            Assert.IsFalse(browser.CanUseOnline);
            var apply = typeof(OnlineRoomBrowser).GetMethod("ApplyDirectoryHealth",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            apply.Invoke(browser, new object[] { new OnlineDirectoryHealth { ok = true,
                protocol = OnlineVersionAuthenticator.ProtocolVersion - 1, buildIdentity = "older-server" } });
            Assert.IsTrue(browser.VersionBlocked, "Editor must still reject incompatible wire protocols.");
            Assert.IsFalse(browser.CanUseOnline);
            browser.CreateRoom();
            Assert.IsFalse(browser.Creating);
            apply.Invoke(browser, new object[] { new OnlineDirectoryHealth { ok = true,
                protocol = OnlineVersionAuthenticator.ProtocolVersion, buildIdentity = "deployed-build" } });
            Assert.IsFalse(browser.VersionBlocked, "Editor can recover when deployment finishes.");
            Assert.IsTrue(browser.CanUseOnline);
            Assert.AreEqual("deployed-build", OnlineVersionAuthenticator.ConnectionBuildIdentity);
            Assert.AreNotEqual("deployed-build", OnlineVersionAuthenticator.BuildIdentity,
                "Diagnostics must retain the actual local checkout identity.");
        }
        finally { OnlineVersionAuthenticator.SetEditorServerBuildIdentity(null); Object.DestroyImmediate(go); }
    }

    [Test]
    public void MenuDeltaSendsChangesAndHeartbeatWithoutRepeatingPayloads()
    {
        var cache = new OnlineRoomPresentationCache();
        OnlineRoomMessage Snapshot() => new OnlineRoomMessage { menu = "cursor1", characters = "skins", settings = "rules", progress = "save", flowView = "", players = new[] { 1, 2 } };
        var message = Snapshot();
        Assert.IsTrue(cache.Prepare(ref message, 0));
        Assert.AreEqual("rules", message.settings);
        message = Snapshot();
        Assert.IsFalse(cache.Prepare(ref message, .01f));
        message = Snapshot(); message.menu = "cursor2";
        Assert.IsTrue(cache.Prepare(ref message, .02f));
        Assert.AreEqual("cursor2", message.menu);
        Assert.IsNull(message.settings);
        Assert.IsNull(message.characters);
        message = Snapshot(); message.menu = "cursor2";
        Assert.IsTrue(cache.Prepare(ref message, 1.1f));
        Assert.IsNull(message.menu);
        Assert.IsNull(message.progress);
        message = Snapshot(); message.menu = ""; message.progress = "newSave";
        Assert.IsTrue(cache.Prepare(ref message, 1.2f));
        Assert.AreEqual("", message.menu, "Empty explicitly clears a previous view");
        Assert.AreEqual("rules", message.settings, "Progress replaces the settings copy, so restore settings too");
        var joiningPeer = new OnlineRoomPresentationCache();
        message = Snapshot();
        Assert.IsTrue(joiningPeer.Prepare(ref message, 1.3f));
        Assert.AreEqual("save", message.progress);
        Assert.AreEqual("skins", message.characters);
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    public void RemoteFastAnimationPreservesPlaybackPhase(bool pingPong, bool customDurations)
    {
        var local = new GameObject("LocalAnimation", typeof(SpriteRenderer));
        var remote = new GameObject("RemoteAnimation", typeof(SpriteRenderer));
        var parent = new GameObject("RemoteParent");
        remote.transform.SetParent(parent.transform, false);
        try
        {
            var source = local.AddComponent<AnimatedSpriteRenderer>();
            var target = remote.AddComponent<AnimatedSpriteRenderer>();
            foreach (var animation in new[] { source, target })
            {
                animation.animationSprite = new Sprite[4];
                animation.idle = false; animation.loop = true; animation.animationTime = .01f;
                animation.pingPong = pingPong;
                if (customDurations) animation.frameDurations = new[] { .01f, .02f, .005f, .015f };
                animation.SetManualAnimationUpdate(true);
            }
            source.AdvanceAnimation(.057f, .057f);
            var snapshot = source.CapturePlayback();
            source.AdvanceAnimation(.033f, .033f);
            target.PresentPlayback(snapshot, .033f, false);
            Assert.AreEqual(source.CurrentFrame, target.CurrentFrame);
            Assert.AreEqual(source.DebugFrameTimer, target.DebugFrameTimer, .00001f);
            var position = new Vector3(4, 3, 0);
            target.transform.position = position;
            target.frameOffsets = new[] { Vector2.up, Vector2.down };
            target.PresentPlayback(snapshot, .033f, false);
            Assert.AreEqual(position, target.transform.position, "Offsets already included in server positions must not be applied twice");
            source.SetFrozen(true);
            target.PresentPlayback(source.CapturePlayback(), .2f, false);
            Assert.AreEqual(source.CurrentFrame, target.CurrentFrame);
        }
        finally { Object.DestroyImmediate(local); Object.DestroyImmediate(remote); Object.DestroyImmediate(parent); }
    }

    [TestCase("", false)]
    [TestCase("   ", false)]
    [TestCase("<b>NAME</b>", false)]
    [TestCase("ABCDEFGHIJKLMNOPQ", false)]
    [TestCase("MauricioSuporte", true)]
    [TestCase("Jogador_1", true)]
    public void OnlineNamesMustBeNonEmptyAndValid(string name, bool expected)
    {
        Assert.AreEqual(expected, OnlineVersionAuthenticator.ValidPlayerName(name));
    }

    [Test]
    public void OnlineNamesAreUniqueIgnoringCaseAndNormalizedSpaces()
    {
        var go = new GameObject("Names");
        try
        {
            var auth = go.AddComponent<OnlineVersionAuthenticator>();
            var field = typeof(OnlineVersionAuthenticator).GetField("admittedNames", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var names = (Dictionary<int, string>)field.GetValue(auth);
            names[1] = "Jogador";
            Assert.IsFalse(auth.NameAvailable(" jogador "));
            Assert.IsTrue(auth.NameAvailable("Outro"));
            auth.ReleaseName(1);
            Assert.IsTrue(auth.NameAvailable("JOGADOR"));
        }
        finally { Object.DestroyImmediate(go); }
    }

    [TestCase("CrazyCup", 2f)]
    [TestCase("Fooler", 2f)]
    [TestCase("Springer", 1f)]
    public void StageOneTwoEnemiesKeepTheirConfiguredSequenceDuration(string enemy, float expected)
    {
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/" + enemy + ".prefab");
        var source = prefab.GetComponentsInChildren<AnimatedSpriteRenderer>(true)[0];
        Assert.IsTrue(source.useSequenceDuration);
        Assert.AreEqual(expected, source.sequenceDuration);
        var go = new GameObject("RemoteSequence", typeof(SpriteRenderer));
        try
        {
            var target = go.AddComponent<AnimatedSpriteRenderer>();
            target.animationSprite = source.animationSprite;
            target.useSequenceDuration = true; target.sequenceDuration = source.sequenceDuration;
            target.idle = false; target.loop = source.loop;
            target.frameDurations = source.frameDurations;
            target.AdvanceAnimation(expected / source.animationSprite.Length * 1.5f, expected / source.animationSprite.Length * 1.5f);
            Assert.AreEqual(1, target.CurrentFrame);
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void GeneratedOnlineCharacterIdsSurviveSelectionAndReplication()
    {
        SaveSystem.BeginOnlineBattleSettings();
        try
        {
            var characters = BomberSkinResourceCatalog.GetAvailableCharacters();
            BomberCharacter generated = System.Array.Find(characters, value => (int)value > byte.MaxValue);
            Assert.Greater((int)generated, byte.MaxValue, "The generated catalog should include a full-width character id");
            var settings = SaveSystem.BattleConfigurationData;
            settings.player1SelectedCharacter = (int)generated;
            settings.player1SelectedSkin = (int)BomberSkinResourceCatalog.NormalizeGeneratedSkin(generated, BomberSkin.Palette1);
            string snapshot = SaveSystem.CaptureOnlineBattleSettings();
            PlayerPersistentStats.ApplyOnlineSelections(settings);
            Assert.AreEqual(generated, PlayerPersistentStats.Get(1).Character);
            Assert.AreEqual(generated, PlayerPersistentStats.GetRuntime(1).Character);
            SaveSystem.ApplyOnlineBattleSettings(snapshot);
            Assert.AreEqual(generated, PlayerPersistentStats.Get(1).Character);
            var wire = typeof(NetworkPlayerState).GetField("character", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.AreEqual(typeof(int), wire.FieldType, "Network state must preserve the complete generated character id");
        }
        finally
        {
            SaveSystem.EndOnlineBattleSettings();
            PlayerPersistentStats.ApplyOnlineSelections(SaveSystem.Data);
        }
    }

    [UnityTest]
    public System.Collections.IEnumerator LeavingRoomCanReloadTheOnlineBrowser()
    {
        yield return new EnterPlayMode();
        yield return SceneManager.LoadSceneAsync(NetworkLobby.SceneName);
        yield return null;
        BombermanNetworkManager.LeaveRoomToBrowser();
        yield return null;
        float deadline = Time.realtimeSinceStartup + 5f;
        while (Object.FindAnyObjectByType<BombermanNetworkManager>() == null && Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.AreEqual(NetworkLobby.SceneName, SceneManager.GetActiveScene().name);
        Assert.IsNotNull(Object.FindAnyObjectByType<OnlineRoomBrowser>());
        Assert.IsFalse(NetworkClient.active);
        Assert.IsFalse(NetworkServer.active);
        BombermanNetworkManager.LeaveRoom();
        yield return null;
        yield return new ExitPlayMode();
    }

    [Test]
    public void PrivateRoomAuthenticationRequiresTheCorrectPassword()
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        string hash = System.BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes("salt123456"))).Replace("-", "").ToLowerInvariant();
        Assert.IsTrue(OnlineVersionAuthenticator.PasswordMatches("salt", hash, "123456"));
        Assert.IsFalse(OnlineVersionAuthenticator.PasswordMatches("salt", hash, "654321"));
        Assert.IsFalse(OnlineVersionAuthenticator.PasswordMatches("other", hash, "123456"));
        Assert.IsFalse(OnlineVersionAuthenticator.PasswordMatches("salt", hash, ""));
        Assert.IsTrue(OnlineVersionAuthenticator.PasswordMatches("", "", ""));
    }

    [TestCase("commit-a", "commit-a", true)]
    [TestCase("commit-a", "commit-b", false)]
    [TestCase("commit-a", "", false)]
    [TestCase("", "", false)]
    public void SameVersionRequiresMatchingBuildIdentity(string localBuild, string remoteBuild, bool expected)
    {
        Assert.AreEqual(expected, OnlineVersionAuthenticator.Compatible("0.5.0", "0.5.0",
            OnlineVersionAuthenticator.ProtocolVersion, localBuild, remoteBuild));
    }

    [Test]
    public void RoomProgressKeepsSelectionsFromTheOnlineConfiguration()
    {
        SaveSystem.BeginOnlineCoopSettings();
        SaveSystem.BeginOnlineBattleSettings();
        try
        {
            SaveSystem.BattleConfigurationData.player1SelectedCharacter = (int)BomberCharacter.Bomberman;
            SaveSystem.BattleConfigurationData.player2SelectedCharacter = (int)BomberCharacter.Bomberman;
            SaveSystem.BattleConfigurationData.player1SelectedSkin = (int)BomberSkin.Palette2;
            SaveSystem.BattleConfigurationData.player2SelectedSkin = (int)BomberSkin.Palette3;
            var snapshot = JsonUtility.FromJson<SaveData>(SaveSystem.CaptureRoomProgress());
            Assert.AreEqual((int)BomberSkin.Palette2, snapshot.player1SelectedSkin);
            Assert.AreEqual((int)BomberSkin.Palette3, snapshot.player2SelectedSkin);
            PlayerPersistentStats.ApplyOnlineSelections(snapshot);
            Assert.AreEqual(BomberSkin.Palette2, PlayerPersistentStats.GetRuntime(1).Skin);
            Assert.AreEqual(BomberSkin.Palette3, PlayerPersistentStats.GetRuntime(2).Skin);
        }
        finally
        {
            SaveSystem.EndOnlineBattleSettings();
            SaveSystem.EndOnlineCoopSettings();
            PlayerPersistentStats.ApplyOnlineSelections(SaveSystem.Data);
        }
    }

    [Test]
    public void LineBombIdentityBelongsToPickupRoot()
    {
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Items/LineBomb.prefab");
        Assert.NotNull(prefab.GetComponent<ItemPickup>());
        Assert.NotNull(prefab.GetComponent<NetworkIdentity>());
        Assert.AreEqual(1, prefab.GetComponentsInChildren<NetworkIdentity>(true).Length);
    }

    [Test]
    public void WorldMountProxyKeepsSharedMountPrefabsFreeOfNestedIdentities()
    {
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/NetworkWorldMount.prefab");
        Assert.NotNull(prefab.GetComponent<NetworkIdentity>());
        var component = prefab.GetComponent<NetworkWorldMount>();
        Assert.AreNotEqual(0u, prefab.GetComponent<NetworkIdentity>().assetId);
        Assert.NotNull(component);
        var serialized = new UnityEditor.SerializedObject(component);
        var catalog = serialized.FindProperty("mountCatalog").objectReferenceValue as PlayerMountCompanion;
        Assert.NotNull(catalog);
        foreach (MountedType type in System.Enum.GetValues(typeof(MountedType)))
        {
            if (type == MountedType.None) continue;
            var mount = catalog.GetMountPrefabForType(type);
            Assert.NotNull(mount);
            Assert.AreEqual(0, mount.GetComponentsInChildren<NetworkIdentity>(true).Length);
            Assert.Greater(mount.GetComponentsInChildren<AnimatedSpriteRenderer>(true).Length, 0);
        }
        string lobby = System.IO.File.ReadAllText("Assets/Scenes/OnlineLobby.unity", System.Text.Encoding.UTF8);
        StringAssert.Contains(UnityEditor.AssetDatabase.AssetPathToGUID("Assets/Prefabs/NetworkWorldMount.prefab"), lobby);
        StringAssert.Contains("fileID: 6871244334814668704, guid: 1859c76e0a194baa929ef21612edfdca", lobby);
    }

    [Test]
    public void WorldMountFrameRoundTripsPoseAndVisibility()
    {
        var frame = new NetworkWorldMount.MountFrame { visible = true, idle = false, frame = 7,
            flipX = true, color = new Color(0.5f, 1f, 0.2f, 0.4f), localPosition = new Vector3(1, 2, 3) };
        using var writer = NetworkWriterPool.Get();
        writer.Write(frame);
        using var reader = NetworkReaderPool.Get(writer.ToArraySegment());
        Assert.IsTrue(frame.Equals(reader.Read<NetworkWorldMount.MountFrame>()));
        var hidden = frame; hidden.visible = false;
        Assert.IsFalse(frame.Equals(hidden));
    }

    [Test]
    public void RedPunchPredictsOnlyGestureConfirmsPhaseRejectsAndRespectsCooldown()
    {
        var oldMode = NetSync.Mode; var actor = new GameObject("RedPredictionOwner"); actor.SetActive(false);
        var mount = new GameObject("RedPredictionMount"); mount.transform.SetParent(actor.transform, false);
        var frames = new GameObject("Punch"); frames.transform.SetParent(mount.transform, false);
        frames.AddComponent<SpriteRenderer>(); var animation = frames.AddComponent<AnimatedSpriteRenderer>(); animation.animationSprite = new Sprite[4];
        var animator = mount.AddComponent<RedLouiePunchAnimator>(); animator.punchRight = animation;
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var clientState = typeof(NetworkClient).GetField("connectState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var oldClientState = clientState.GetValue(null); NetworkBombFx fx = null;
        var paused = typeof(GamePauseController).GetProperty(nameof(GamePauseController.IsPaused)); bool wasPaused = GamePauseController.IsPaused;
        try
        {
            NetSync.Mode = NetSync.NetMode.Client; clientState.SetValue(null, System.Enum.Parse(clientState.FieldType, "Connected"));
            actor.tag = "Player"; actor.AddComponent<BoxCollider2D>();
            var identity = actor.AddComponent<NetworkIdentity>(); fx = actor.AddComponent<NetworkBombFx>();
            typeof(NetworkIdentity).GetMethod("InitializeNetworkBehaviours", flags).Invoke(identity, null);
            typeof(NetworkIdentity).GetProperty("isLocalPlayer").SetValue(identity, true);
            var movement = actor.GetComponent<MovementController>(); var body = actor.GetComponent<Rigidbody2D>(); body.bodyType = RigidbodyType2D.Kinematic;
            typeof(MovementController).GetProperty(nameof(MovementController.Rigidbody)).SetValue(movement, body);
            var companion = actor.AddComponent<PlayerMountCompanion>();
            typeof(PlayerMountCompanion).GetField("currentLouie", flags).SetValue(companion, mount);
            typeof(PlayerMountCompanion).GetField("mountedType", flags).SetValue(companion, MountedType.Red);
            actor.SetActive(true); movement.SetNetworkTickDriven(true);
            var state = new MovementController.NetworkMovementState { position = new Vector2(11000, 11000), mount = MountedType.Red,
                facing = Vector2.right, worldSpeed = 4, animationSpeed = PlayerPersistentStats.BaseSpeedNormal, canPredict = true };
            movement.RestoreNetworkMovementState(state); movement.SetPredictLocally(true);
            Assert.IsTrue(fx.TryPredictRedPunch(8, 10)); Assert.IsTrue(fx.AbilityPredictionLocksMovement);
            Assert.IsFalse(movement.InputLocked, "The presentation does not own the server's gameplay lock.");
            Assert.IsNull(typeof(RedLouiePunchStunAbility).GetField("routine", flags).GetValue(actor.GetComponent<RedLouiePunchStunAbility>()));
            var update = typeof(NetworkBombFx).GetMethod("UpdateRedPunch", flags);
            update.Invoke(fx, new object[] { 0.1f }); int frame = animation.CurrentFrame; Assert.Greater(frame, 0);
            paused.SetValue(null, true); update.Invoke(fx, new object[] { 1f }); Assert.AreEqual(frame, animation.CurrentFrame);
            paused.SetValue(null, false);
            fx.ReceiveRedPunch(10, Vector2.right, 0.25f, 2f, false, NetworkBombFx.KickServerNow);
            Assert.AreEqual(frame, animation.CurrentFrame, "Confirmation cannot restart the predicted gesture.");
            fx.ReceiveRedPunch(9, Vector2.left, 1, 0, false, NetworkBombFx.KickServerNow);
            update.Invoke(fx, new object[] { 0.16f }); Assert.IsFalse(fx.RedPunchPoseActive); Assert.IsFalse(fx.AbilityPredictionLocksMovement);
            Assert.IsFalse(fx.TryPredictRedPunch(8, 11), "Success cooldown is confirmed by the server.");
            typeof(NetworkBombFx).GetField("redPunchNextAllowed", flags).SetValue(fx, 0f);
            Assert.IsTrue(fx.TryPredictRedPunch(8, 11)); fx.ReconcileRedPunch(11, state);
            Assert.IsFalse(fx.RedPunchPoseActive); Assert.IsFalse(fx.AbilityPredictionLocksMovement);
            Assert.IsTrue(fx.TryPredictRedPunch(8, 12)); actor.GetComponent<CharacterHealth>().life = 0;
            update.Invoke(fx, new object[] { 0.01f }); Assert.IsFalse(fx.RedPunchPoseActive);
            NetSync.Mode = NetSync.NetMode.Host; Assert.IsFalse(fx.TryPredictRedPunch(8, 13));
        }
        finally
        {
            if (fx != null) typeof(NetworkBombFx).GetMethod("ClearRedPunch", flags).Invoke(fx, new object[] { "test_cleanup" });
            paused.SetValue(null, wasPaused); NetSync.Mode = oldMode; clientState.SetValue(null, oldClientState); Object.DestroyImmediate(actor);
        }
    }

    [Test]
    public void PinkJumpPredictionSeparatesGroundArcAndShadowWithoutGrantingInvulnerability()
    {
        var oldMode = NetSync.Mode; var actor = new GameObject("PinkPredictionOwner"); actor.SetActive(false);
        var mount = new GameObject("PinkPredictionMount"); mount.transform.SetParent(actor.transform, false);
        var shadowObject = new GameObject("PinkPredictionShadow"); shadowObject.transform.SetParent(mount.transform, false);
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var clientState = typeof(NetworkClient).GetField("connectState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var oldClientState = clientState.GetValue(null); NetworkBombFx fx = null;
        try
        {
            NetSync.Mode = NetSync.NetMode.Client; clientState.SetValue(null, System.Enum.Parse(clientState.FieldType, "Connected"));
            actor.tag = "Player"; var collider = actor.AddComponent<BoxCollider2D>();
            var identity = actor.AddComponent<NetworkIdentity>(); fx = actor.AddComponent<NetworkBombFx>();
            typeof(NetworkIdentity).GetMethod("InitializeNetworkBehaviours", flags).Invoke(identity, null);
            typeof(NetworkIdentity).GetProperty("isLocalPlayer").SetValue(identity, true);
            var movement = actor.GetComponent<MovementController>(); var body = actor.GetComponent<Rigidbody2D>(); body.bodyType = RigidbodyType2D.Kinematic;
            typeof(MovementController).GetProperty(nameof(MovementController.Rigidbody)).SetValue(movement, body);
            var companion = actor.AddComponent<PlayerMountCompanion>();
            typeof(PlayerMountCompanion).GetField("currentLouie", flags).SetValue(companion, mount);
            typeof(PlayerMountCompanion).GetField("mountedType", flags).SetValue(companion, MountedType.Pink);
            var shadow = shadowObject.AddComponent<PinkLouieShadowController>(); shadow.followTarget = mount.transform;
            actor.SetActive(true); movement.SetNetworkTickDriven(true);
            Vector2 origin = new(10000, 10000);
            var state = new MovementController.NetworkMovementState { position = origin, mount = MountedType.Pink,
                facing = Vector2.right, worldSpeed = 4, animationSpeed = PlayerPersistentStats.BaseSpeedNormal, canPredict = true };
            movement.RestoreNetworkMovementState(state); movement.SetPredictLocally(true);
            var jump = new NetworkBombFx.PinkJumpMotion { inputTick = 10, start = origin, end = origin + Vector2.right * 2,
                facing = Vector2.right, duration = 0.8f, height = 1, cooldown = 0.1f, active = true };
            Assert.IsTrue(fx.BeginPinkJumpPrediction(jump));
            var advance = typeof(NetworkBombFx).GetMethod("UpdatePinkJump", flags);
            advance.Invoke(fx, new object[] { 0.4f, 0.4f });
            Assert.Less(Vector2.Distance(origin + Vector2.right, body.position), 0.01f, "Physics stays on the ground trajectory.");
            Assert.That(mount.transform.localPosition.y, Is.EqualTo(1).Within(0.001f));
            Assert.That(shadow.transform.position.y, Is.EqualTo(origin.y + shadow.localY).Within(0.001f), "Shadow stays on ground after the parent arc offset.");
            Assert.IsFalse(collider.enabled); Assert.IsFalse(actor.GetComponent<CharacterHealth>().IsInvulnerable);
            Assert.IsFalse(actor.GetComponent<PinkLouieJumpAbility>().JumpActive);
            jump.revision = 1; jump.serverTime = NetworkBombFx.KickServerNow;
            Vector2 before = body.position; fx.ReceivePinkJump(jump);
            Assert.AreEqual(before, body.position, "Acceptance preserves already displayed jump progress.");
            var stop = jump; stop.revision = 2; stop.active = false; stop.completed = true;
            fx.ReceivePinkJump(stop); Assert.IsTrue(fx.PinkJumpActive, "An early stop waits for the approved arc to finish.");
            advance.Invoke(fx, new object[] { 0.4f, 0.4f });
            Assert.IsFalse(fx.PinkJumpActive); Assert.IsTrue(collider.enabled); Assert.AreEqual(jump.end, body.position);
            Assert.That(mount.transform.localPosition.y, Is.EqualTo(0).Within(0.001f));
            Assert.That(shadow.transform.position.y, Is.EqualTo(origin.y + shadow.localY).Within(0.001f));
            movement.RestoreNetworkMovementState(state); movement.SetPredictLocally(true); jump.inputTick = 11;
            Assert.IsTrue(fx.BeginPinkJumpPrediction(jump));
            movement.RestoreNetworkMovementState(state); fx.ReconcilePinkJump(11, state);
            Assert.IsFalse(fx.PinkJumpActive); Assert.IsTrue(collider.enabled); Assert.AreEqual(origin, body.position);
            Assert.IsFalse(actor.GetComponent<PinkLouieJumpAbility>().TryStartExternalJump(Vector2.right), "Clients cannot execute the authoritative jump coroutine.");
            typeof(NetworkIdentity).GetProperty("isLocalPlayer").SetValue(identity, false);
            jump.inputTick = 12; jump.revision = 3; jump.serverTime = NetworkTime.time - 0.4;
            fx.ReceivePinkJump(jump); advance.Invoke(fx, new object[] { 0.01f, 0.01f });
            Assert.IsTrue(fx.PinkJumpVisualActive);
            Assert.AreEqual(origin, body.position, "An observer renders the approved arc without taking over NetworkTransform's ground position.");
            Assert.That(mount.transform.localPosition.y, Is.EqualTo(1).Within(0.02f));
            stop = jump; stop.revision = 4; stop.active = false; stop.completed = false;
            fx.ReceivePinkJump(stop); Assert.IsFalse(fx.PinkJumpActive); Assert.IsTrue(collider.enabled);
            NetSync.Mode = NetSync.NetMode.Host; Assert.IsFalse(fx.BeginPinkJumpPrediction(jump));
            using var writer = NetworkWriterPool.Get(); writer.Write(jump);
            using var reader = NetworkReaderPool.Get(writer.ToArraySegment()); var received = reader.Read<NetworkBombFx.PinkJumpMotion>();
            Assert.AreEqual(jump.inputTick, received.inputTick); Assert.AreEqual(jump.serverTime, received.serverTime);
            Assert.AreEqual(jump.start, received.start); Assert.AreEqual(jump.end, received.end); Assert.AreEqual(jump.height, received.height);
        }
        finally
        {
            if (fx != null) typeof(NetworkBombFx).GetMethod("ClearPinkJump", flags).Invoke(fx, new object[] { "test_cleanup" });
            NetSync.Mode = oldMode; clientState.SetValue(null, oldClientState); Object.DestroyImmediate(actor);
        }
    }

    [Test]
    public void GreenDashPredictionIsBoundedConfirmsAndReleasesToAuthority()
    {
        var oldMode = NetSync.Mode; var actor = new GameObject("GreenDashPredictionOwner"); actor.SetActive(false);
        var mount = new GameObject("GreenDashPredictionMount"); mount.transform.SetParent(actor.transform);
        var wall = new GameObject("GreenDashPredictionWall");
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var clientState = typeof(NetworkClient).GetField("connectState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var oldClientState = clientState.GetValue(null); NetworkBombFx fx = null;
        try
        {
            NetSync.Mode = NetSync.NetMode.Client; clientState.SetValue(null, System.Enum.Parse(clientState.FieldType, "Connected"));
            actor.tag = "Player"; var collider = actor.AddComponent<BoxCollider2D>();
            var identity = actor.AddComponent<NetworkIdentity>(); fx = actor.AddComponent<NetworkBombFx>();
            typeof(NetworkIdentity).GetMethod("InitializeNetworkBehaviours", flags).Invoke(identity, null);
            typeof(NetworkIdentity).GetProperty("isLocalPlayer").SetValue(identity, true);
            var movement = actor.GetComponent<MovementController>(); var body = actor.GetComponent<Rigidbody2D>(); body.bodyType = RigidbodyType2D.Kinematic;
            typeof(MovementController).GetProperty(nameof(MovementController.Rigidbody)).SetValue(movement, body);
            var companion = actor.AddComponent<PlayerMountCompanion>();
            typeof(PlayerMountCompanion).GetField("currentLouie", flags).SetValue(companion, mount);
            typeof(PlayerMountCompanion).GetField("mountedType", flags).SetValue(companion, MountedType.Green);
            actor.SetActive(true); movement.SetNetworkTickDriven(true);
            Vector2 origin = new(9000, 9000);
            var state = new MovementController.NetworkMovementState { position = origin, mount = MountedType.Green,
                facing = Vector2.right, worldSpeed = 4, animationSpeed = PlayerPersistentStats.BaseSpeedNormal, canPredict = true };
            movement.RestoreNetworkMovementState(state); movement.SetPredictLocally(true);
            Assert.IsTrue(fx.BeginGreenDashPrediction(10, Vector2.right, 10, origin));
            Assert.IsTrue(fx.ClientPlayerMotionActive);
            var step = typeof(NetworkBombFx).GetMethod("UpdateGreenDash", flags);
            for (int i = 0; i < 10; i++) step.Invoke(fx, new object[] { 0.02f, 0.02f });
            Assert.Greater(body.position.x, origin.x);
            Assert.LessOrEqual(body.position.x, origin.x + 1.51f, "Unconfirmed dash continuation is bounded to 150 ms.");
            Assert.IsFalse(actor.GetComponent<GreenLouieDashAbility>().DashActive, "Client presentation must not run the gameplay dash coroutine.");
            movement.RestoreNetworkMovementState(state); fx.ReconcileGreenDash(10, state);
            Assert.IsFalse(fx.GreenDashActive); Assert.AreEqual(origin, body.position);
            movement.SetPredictLocally(true);
            Assert.IsTrue(fx.BeginGreenDashPrediction(11, Vector2.right, 10, origin));
            step.Invoke(fx, new object[] { 0.02f, 0.02f }); Vector2 beforeReply = body.position;
            fx.ReceiveGreenDash(11, true, Vector2.right, 10, origin, NetworkBombFx.KickServerNow);
            Assert.AreEqual(beforeReply, body.position, "First acceptance must not pull the owner back to the initial server position.");
            fx.ReceiveGreenDash(10, false, Vector2.right, 10, origin, NetworkBombFx.KickServerNow);
            Assert.IsTrue(fx.GreenDashActive, "A previous dash cannot stop a newer action.");
            fx.ReceiveGreenDash(11, false, Vector2.right, 10, origin + Vector2.right, NetworkBombFx.KickServerNow);
            Assert.IsFalse(fx.GreenDashActive); Assert.AreEqual(origin + Vector2.right, body.position); Assert.IsTrue(collider.enabled);
            Assert.IsFalse(movement.InputLocked, "Presentation must not own the authoritative input lock.");
            wall.layer = LayerMask.NameToLayer("Stage"); wall.transform.position = origin + Vector2.right * 1.5f;
            wall.AddComponent<BoxCollider2D>(); Physics2D.SyncTransforms();
            var ability = actor.GetComponent<GreenLouieDashAbility>();
            Assert.IsTrue(ability.TryPreviewNetworkDashStep(origin, Vector2.right, 5, out var end));
            Assert.Less(end.x, origin.x + 1, "The read-only dash must stop before known geometry.");
            NetSync.Mode = NetSync.NetMode.Host;
            Assert.IsFalse(fx.TryPredictGreenDash(8, 12));
            Assert.IsFalse(fx.BeginGreenDashPrediction(12, Vector2.right, 10, origin));
        }
        finally
        {
            if (fx != null) typeof(NetworkBombFx).GetMethod("ClearGreenDash", flags).Invoke(fx, new object[] { "test_cleanup" });
            NetSync.Mode = oldMode; clientState.SetValue(null, oldClientState); Object.DestroyImmediate(actor); Object.DestroyImmediate(wall);
        }
    }

    [Test]
    public void PlacementProxyIsVisualOnlyAndReservesCapacityUntilAcknowledged()
    {
        var mode = NetSync.Mode; var actor = new GameObject("PlacementPredictionOwner"); actor.SetActive(false);
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var clientState = typeof(NetworkClient).GetField("connectState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var previousClientState = clientState.GetValue(null);
        NetworkBombFx fx = null;
        try
        {
            NetSync.Mode = NetSync.NetMode.Client; clientState.SetValue(null, System.Enum.Parse(clientState.FieldType, "Connected"));
            actor.AddComponent<BoxCollider2D>(); var identity = actor.AddComponent<NetworkIdentity>(); fx = actor.AddComponent<NetworkBombFx>();
            typeof(NetworkIdentity).GetMethod("InitializeNetworkBehaviours", flags).Invoke(identity, null);
            typeof(NetworkIdentity).GetProperty("isLocalPlayer").SetValue(identity, true);
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Bombs/Bomb.prefab");
            var state = new NetworkBombFx.PlacementState { available = 1, allowed = true, prefabAssetId = 123 };
            fx.ReconcilePlacement(0, state);
            Vector2 origin = new(7000, 7000); int bombsBefore = Bomb.ActiveBombs.Count;
            Assert.IsTrue(fx.BeginPlacementPrediction(1, origin, prefab));
            var proxies = (System.Collections.IDictionary)typeof(NetworkBombFx).GetField("placements", flags).GetValue(fx);
            var proxy = (GameObject)proxies[(1u, (byte)0, origin)].GetType().GetField("visual").GetValue(proxies[(1u, (byte)0, origin)]);
            Assert.IsNotNull(proxy.GetComponent<SpriteRenderer>());
            Assert.IsNull(proxy.GetComponent<Bomb>()); Assert.IsNull(proxy.GetComponent<Collider2D>());
            Assert.IsNull(proxy.GetComponent<Rigidbody2D>()); Assert.IsNull(proxy.GetComponent<NetworkIdentity>());
            Assert.AreEqual(bombsBefore, Bomb.ActiveBombs.Count, "A visual proxy cannot join the gameplay bomb registry.");
            Assert.IsFalse(fx.BeginPlacementPrediction(1, origin, prefab));
            Assert.IsFalse(fx.BeginPlacementPrediction(2, origin + Vector2.right, prefab), "Reserve predicted stock until the server processes it.");
            Assert.IsFalse(fx.BlocksPredictedPlacement(origin, origin + Vector2.right, 1, 1), "The planting player can leave its bomb.");
            Assert.IsTrue(fx.BlocksPredictedPlacement(origin + Vector2.right, origin, 1, 1), "Returning to a pending planted bomb must still query the predicted footprint.");
            Assert.IsFalse(fx.BlocksPredictedPlacement(origin + Vector2.right, origin, 1, 0), "A placement cannot affect an earlier replay tick.");
            state.available = 2; fx.ReconcilePlacement(0, state);
            Assert.IsFalse(fx.BeginPlacementPrediction(2, origin, prefab));
            Assert.IsTrue(fx.BeginPlacementPrediction(2, origin + Vector2.right, prefab));
            fx.ReconcilePlacement(1, state); Assert.AreEqual(1, fx.PlacementReservationCount);
            Assert.IsFalse(fx.BlocksPredictedPlacement(origin + Vector2.right, origin, 1, 2), "Rejection releases the footprint.");
            fx.ReceivePlacementConfirmed(2, 777, origin + Vector2.right);
            Assert.AreEqual(1, fx.PlacementReservationCount, "Confirmation hides the proxy but stock waits for the authoritative snapshot.");
            Assert.IsFalse(fx.BlocksPredictedPlacement(origin + Vector2.right * 2, origin + Vector2.right, 1, 2));
            fx.ReconcilePlacement(2, state); Assert.AreEqual(0, fx.PlacementReservationCount);
            state.allowed = false; fx.ReconcilePlacement(2, state); Assert.IsFalse(fx.BeginPlacementPrediction(3, origin, prefab));
            state.allowed = true; fx.ReconcilePlacement(2, state); Assert.IsTrue(fx.BeginPlacementPrediction(3, origin, prefab));
            typeof(NetworkBombFx).GetMethod("UpdatePlacementPresentation", flags).Invoke(fx, new object[] { 1.1f });
            Assert.AreEqual(0, fx.PlacementReservationCount); Assert.IsFalse(fx.BeginPlacementPrediction(4, origin, prefab));
            NetSync.Mode = NetSync.NetMode.Host; Assert.IsFalse(fx.BeginPlacementPrediction(4, origin, prefab));
            using var writer = NetworkWriterPool.Get(); writer.Write(state);
            using var reader = NetworkReaderPool.Get(writer.ToArraySegment()); var result = reader.Read<NetworkBombFx.PlacementState>();
            Assert.AreEqual(state.available, result.available); Assert.AreEqual(state.prefabAssetId, result.prefabAssetId); Assert.IsTrue(result.allowed);
        }
        finally
        {
            if (fx != null) typeof(NetworkBombFx).GetMethod("ClearPlacementPredictions", flags).Invoke(fx, null);
            NetSync.Mode = mode; clientState.SetValue(null, previousClientState); Object.DestroyImmediate(actor);
        }
    }

    [TestCase(MountedType.Mole)]
    [TestCase(MountedType.Tank)]
    [TestCase(MountedType.Blue)]
    [TestCase(MountedType.Black)]
    [TestCase(MountedType.Purple)]
    [TestCase(MountedType.Green)]
    [TestCase(MountedType.Yellow)]
    [TestCase(MountedType.Pink)]
    [TestCase(MountedType.Red)]
    public void WorldMountClientCloneOnlyPresentsServerOutput(MountedType type)
    {
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/NetworkWorldMount.prefab");
        var instance = Object.Instantiate(prefab);
        try
        {
            var proxy = instance.GetComponent<NetworkWorldMount>();
            // Edit Mode does not run NetworkIdentity.Awake; initialize the same
            // behaviour binding Mirror establishes before OnStartClient at runtime.
            typeof(NetworkIdentity).GetMethod("InitializeNetworkBehaviours", System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance).Invoke(instance.GetComponent<NetworkIdentity>(), null);
            typeof(NetworkWorldMount).GetField("mountType", System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance).SetValue(proxy, type);
            proxy.OnStartClient();
            Assert.AreEqual(1, instance.transform.childCount);
            foreach (var body in instance.GetComponentsInChildren<Rigidbody2D>(true)) Assert.IsFalse(body.simulated);
            foreach (var collider in instance.GetComponentsInChildren<Collider2D>(true)) Assert.IsFalse(collider.enabled);
            foreach (var behaviour in instance.transform.GetChild(0).GetComponentsInChildren<MonoBehaviour>(true))
                Assert.IsFalse(behaviour.enabled, behaviour.GetType().Name);
            Assert.AreEqual(1, instance.GetComponentsInChildren<NetworkIdentity>(true).Length);
        }
        finally { Object.DestroyImmediate(instance); }
    }

    [TestCase(-0.1f, 0f)]
    [TestCase(0.025f, 0.25f)]
    [TestCase(0.1f, 1f)]
    [TestCase(5f, 1f)]
    public void KickPresentationNeverLeavesTheServerApprovedSegment(float elapsed, float expectedProgress)
    {
        var start = new Vector2(3, 4);
        var end = new Vector2(4, 4);
        Assert.AreEqual(start + Vector2.right * expectedProgress,
            NetworkBombFx.SampleKickSegment(start, end, 0.1f, elapsed));
        Assert.AreEqual(end, NetworkBombFx.SampleKickSegment(start, end, 0, elapsed), "Stop messages snap to the server position.");
    }

    [TestCase(0.125f, true, 1.25f)]
    [TestCase(0.2f, true, 2f)]
    [TestCase(10f, true, 2f)]
    [TestCase(0.125f, false, 1f)]
    public void KickContinuationBridgesJitterButStopsAtOneExtraTile(float elapsed, bool clear, float progress)
    {
        var start = new Vector2(3, 4);
        var end = start + Vector2.right;
        Assert.AreEqual(progress, NetworkBombFx.SampleKickContinuation(start, end, 0.1f, elapsed, clear).x - start.x, 0.001f);
        Assert.AreEqual(end, NetworkBombFx.SampleKickContinuation(start, end, 0, elapsed, clear));
        Assert.AreEqual(1.15f, NetworkBombFx.SampleKickContinuation(Vector2.zero, Vector2.right, 1, 100, true).x, 0.001f,
            "Long segments still predict no more than 150 ms.");
    }

    [TestCase("Stage", false)]
    [TestCase("Water", true)]
    [TestCase("Bomb", true)]
    [TestCase("Player", false)]
    public void KickContinuationStopsAtReplicatedObstacles(string layer, bool trigger)
    {
        var root = new GameObject("KickContinuationBomb");
        root.SetActive(false);
        root.AddComponent<CircleCollider2D>();
        var obstacle = new GameObject("KickContinuationObstacle");
        try
        {
            var bomb = root.AddComponent<Bomb>();
            Vector2 target = new Vector2(1000, 1000);
            obstacle.layer = LayerMask.NameToLayer(layer);
            obstacle.transform.position = target;
            var collider = obstacle.AddComponent<BoxCollider2D>();
            collider.isTrigger = trigger;
            Physics2D.SyncTransforms();
            Assert.IsFalse(bomb.CanPredictKickContinuation(target, 1));
            collider.enabled = false;
            Physics2D.SyncTransforms();
            Assert.IsTrue(bomb.CanPredictKickContinuation(target, 1));
            Assert.IsFalse(bomb.IsBeingKicked, "Presentation queries must not start gameplay simulation.");
        }
        finally { Object.DestroyImmediate(obstacle); Object.DestroyImmediate(root); }
    }

    [Test]
    public void NetworkInputBudgetRecoversDeliveryGapsWithoutSpeedingUpForPacketFloods()
    {
        var budget = new NetworkInputStepBudget();
        Assert.IsFalse(budget.TryConsume());
        budget.Advance(); budget.Advance(); budget.Advance();
        for (int tick = 0; tick < 3; tick++) Assert.IsTrue(budget.TryConsume());
        Assert.IsFalse(budget.TryConsume(), "Only elapsed server steps may be recovered.");
        int simulated = 0;
        for (int serverTick = 0; serverTick < 100; serverTick++)
        {
            budget.Advance();
            for (int packet = 0; packet < 100; packet++)
                if (budget.TryConsume()) simulated++;
        }
        Assert.AreEqual(100, simulated, "Packet floods cannot select simulation time or sustained movement speed.");
        for (int i = 0; i < 100; i++) budget.Advance();
        int recovered = 0;
        while (budget.TryConsume()) recovered++;
        Assert.AreEqual(8, recovered, "Outage recovery must remain bounded.");
        budget.Advance(); budget.Reset();
        Assert.IsFalse(budget.TryConsume(), "Pause clears unused simulation credit.");
    }

    [Test]
    public void NetworkHeldButtonCannotCreatePowerGlovePressWithoutAnExplicitEdge()
    {
        var input = PlayerInputManager.Instance;
        var owned = input == null ? new GameObject("NetworkActionInputRegression") : null;
        if (owned != null) input = owned.AddComponent<PlayerInputManager>();
        const int player = 6;
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        try
        {
            input.ClearSyntheticPlayer(player);
            var taps = (int[,])typeof(PlayerInputManager).GetField("syntheticTapFrame", flags).GetValue(input);
            for (int i = 0; i < taps.GetLength(1); i++) taps[player, i] = -1;
            input.SetSyntheticHeld(player, PlayerAction.ActionA, true, explicitEdges: true);
            Assert.IsTrue(input.Get(player, PlayerAction.ActionA));
            Assert.IsFalse(input.GetDown(player, PlayerAction.ActionA));
            input.SetSyntheticHeld(player, PlayerAction.ActionA, false, explicitEdges: true);
            // EditMode does not initialize the unrelated directional dictionaries
            // read by LateUpdate. Reproduce its action-history update directly.
            var previousHeld = (bool[,])typeof(PlayerInputManager).GetField("syntheticPreviousHeld", flags).GetValue(input);
            for (int i = 0; i < previousHeld.GetLength(1); i++) previousHeld[player, i] = false;
            input.SetSyntheticHeld(player, PlayerAction.ActionA, true, explicitEdges: true);
            Assert.IsFalse(input.GetDown(player, PlayerAction.ActionA), "Resuming held A after a message gap must not pick up the planted bomb.");
            input.TapSynthetic(player, PlayerAction.ActionA);
            Assert.IsTrue(input.GetDown(player, PlayerAction.ActionA), "A real second press must still activate the glove.");
        }
        finally { input.ClearSyntheticPlayer(player); if (owned != null) Object.DestroyImmediate(owned); }
    }

    [Test]
    public void InitialKickPredictionStartsImmediatelyAndRollsBackOrConfirms()
    {
        var mode = NetSync.Mode;
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
        var clientState = typeof(NetworkClient).GetField("connectState", flags);
        var oldState = clientState.GetValue(null);
        var player = new GameObject("InitialKickPredictionOwner"); player.SetActive(false);
        player.AddComponent<BoxCollider2D>();
        var bombObject = new GameObject("InitialKickPredictionBomb"); bombObject.SetActive(false);
        bombObject.AddComponent<CircleCollider2D>();
        var blocker = new GameObject("InitialKickPredictionWall");
        const uint bombId = uint.MaxValue - 50;
        try
        {
            var owner = player.AddComponent<NetworkIdentity>();
            var input = player.AddComponent<NetworkPlayerInput>();
            var fx = player.AddComponent<NetworkBombFx>();
            typeof(NetworkIdentity).GetMethod("InitializeNetworkBehaviours",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(owner, null);
            typeof(NetworkIdentity).GetProperty("isLocalPlayer").SetValue(owner, true);
            typeof(NetworkPlayerInput).GetField("movementTick", System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance).SetValue(input, 25u);
            clientState.SetValue(null, System.Enum.Parse(clientState.FieldType, "Connected"));
            var movingBomb = bombObject.AddComponent<Bomb>();
            var identity = bombObject.AddComponent<NetworkIdentity>();
            typeof(NetworkIdentity).GetProperty("netId").SetValue(identity, bombId);
            NetworkClient.spawned.Add(bombId, identity);
            bombObject.SetActive(true);
            Vector2 origin = new Vector2(2000, 2000);
            bombObject.transform.position = origin;
            NetSync.Mode = NetSync.NetMode.Offline;
            Assert.IsFalse(fx.TryPredictKick(movingBomb, Vector2.right, 1), "Offline prefab instances must not use network prediction.");
            NetSync.Mode = NetSync.NetMode.Host;
            Assert.IsFalse(fx.TryPredictKick(movingBomb, Vector2.right, 1), "Host uses its authoritative kick.");
            NetSync.Mode = NetSync.NetMode.Client;
            blocker.layer = LayerMask.NameToLayer("Stage"); blocker.transform.position = origin + Vector2.right;
            var wall = blocker.AddComponent<BoxCollider2D>(); Physics2D.SyncTransforms();
            Assert.IsFalse(fx.TryPredictKick(movingBomb, Vector2.right, 1), "Blocked exits wait for authority.");
            wall.enabled = false; Physics2D.SyncTransforms();
            Assert.IsTrue(fx.TryPredictKick(movingBomb, Vector2.right, 1));
            Assert.Greater(bombObject.transform.position.x, origin.x, "The bomb moves inside the contact call, before any server response.");
            Assert.IsFalse(movingBomb.IsBeingKicked, "Prediction must not start the gameplay coroutine.");
            Assert.IsFalse(fx.IsKickMovingForTick(movingBomb, 24));
            Assert.IsTrue(fx.IsKickMovingForTick(movingBomb, 25));
            Assert.AreSame(movingBomb, fx.LastKickForTick(25));
            var motor = player.GetComponent<MovementController>();
            motor.SetPredictLocally(true);
            motor.RestorePredictedKickReference(fx.LastKickForTick(25));
            var reference = typeof(MovementController).GetField("_lastAdjKickedBomb", System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);
            Assert.AreSame(movingBomb, reference.GetValue(motor), "The client must recognize the same recent kick as the server motor.");
            Assert.IsTrue((bool)typeof(MovementController).GetMethod("IsKickMovingForMotor", System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance).Invoke(motor, new object[] { movingBomb }));
            Assert.IsFalse(fx.TryPredictKick(movingBomb, Vector2.right, 1), "Repeated contacts must not restart a prediction.");
            fx.MarkInitialMotorHold(movingBomb);
            Assert.IsTrue(fx.ReplayInitialKick(25, Vector2.right));
            fx.PrepareClientMovementTick(25, true, 8);
            Assert.AreEqual(origin, (Vector2)bombObject.transform.position, "Replay sees the original contact position.");
            fx.RestoreClientKickPositions();
            fx.ReconcilePredictedKicks(24);
            Assert.Greater(bombObject.transform.position.x, origin.x);
            fx.ReconcilePredictedKicks(25);
            Assert.AreEqual(origin, (Vector2)bombObject.transform.position, "Processed input without a kick rolls back presentation.");
            Assert.IsFalse(fx.TryPredictKick(movingBomb, Vector2.right, 1), "A rejected contact cannot oscillate every tick.");
            fx.PrepareClientMovementTick(26, false, 1);
            Assert.IsTrue(fx.TryPredictKick(movingBomb, Vector2.up, 1));
            fx.ReceiveKickMotion(new NetworkBombFx.KickMotion { bombId = bombId, inputTick = 25,
                start = origin, end = origin + Vector2.up, duration = 0.12f,
                serverTime = NetworkBombFx.KickServerNow, moving = true, first = true });
            fx.ReconcilePredictedKicks(26);
            Assert.Greater(bombObject.transform.position.y, origin.y, "A confirmed prediction must survive later acknowledgements.");
            Assert.IsFalse(movingBomb.IsBeingKicked);
            fx.ReceiveKickMotion(new NetworkBombFx.KickMotion { bombId = bombId, inputTick = 30,
                start = origin + Vector2.up, end = origin + Vector2.up, moving = false,
                serverTime = NetworkBombFx.KickServerNow + 0.01 });
            Assert.IsTrue(fx.IsKickMovingForTick(movingBomb, 29), "Replay before a stop still sees the moving bomb.");
            Assert.IsFalse(fx.IsKickMovingForTick(movingBomb, 30));
            Assert.IsNull(fx.LastKickForTick(30), "Stopped bombs regain ordinary collision behavior.");
        }
        finally
        {
            NetworkClient.spawned.Remove(bombId); clientState.SetValue(null, oldState); NetSync.Mode = mode;
            Object.DestroyImmediate(blocker); Object.DestroyImmediate(bombObject); Object.DestroyImmediate(player);
        }
    }

    [Test]
    public void InitialKickConfirmationPreservesBoundedOwnerTimelineLead()
    {
        var motion = new NetworkBombFx.KickMotion { start = Vector2.zero, end = Vector2.right, duration = 0.1f, moving = true };
        Assert.AreEqual(0.03f, NetworkBombFx.InitialKickPresentationLead(Vector2.right * 0.7f, motion, 0.04f), 0.001f);
        Assert.AreEqual(0f, NetworkBombFx.InitialKickPresentationLead(Vector2.right * 0.2f, motion, 0.04f));
        Assert.AreEqual(0.15f, NetworkBombFx.InitialKickPresentationLead(Vector2.right * 5, motion, 0));
        motion.moving = false;
        Assert.AreEqual(0f, NetworkBombFx.InitialKickPresentationLead(Vector2.right, motion, 0), "Stops must not inherit prediction lead.");
    }

    [Test]
    public void KickPredictionHistoryRetainsReplayPositionsAcrossRolloverAndBoundsMemory()
    {
        var history = new NetworkBombFx.KickInputHistory(uint.MaxValue, Vector2.one);
        history.Remember(uint.MaxValue, Vector2.one);
        history.Remember(0, Vector2.right * 2);
        Assert.AreEqual(Vector2.one, history.At(uint.MaxValue - 1, Vector2.zero));
        Assert.AreEqual(Vector2.right * 2, history.At(0, Vector2.zero));
        Assert.IsFalse(NetworkBombFx.PredictionAcknowledged(uint.MaxValue - 1, uint.MaxValue));
        Assert.IsTrue(NetworkBombFx.PredictionAcknowledged(0, uint.MaxValue));
        for (uint i = 1; i < 200; i++) history.Remember(i, Vector2.right * i);
        Assert.AreEqual(NetworkInputHistory.Capacity, history.Count);
        Assert.AreEqual(Vector2.right * 199, history.At(199, Vector2.zero));
    }

    [TestCase(NetSync.NetMode.Offline, false)]
    [TestCase(NetSync.NetMode.Host, false)]
    [TestCase(NetSync.NetMode.Client, true)]
    public void KickLogicalPositionUsesTheReplicaOnlyOnPureClients(NetSync.NetMode mode, bool replica)
    {
        var previous = NetSync.Mode;
        var root = new GameObject("KickLogicalPositionRegression");
        root.SetActive(false);
        root.AddComponent<CircleCollider2D>();
        try
        {
            var bomb = root.AddComponent<Bomb>();
            var field = typeof(Bomb).GetField("lastPos", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var authoritative = new Vector2(1, 2);
            var received = new Vector2(5, 7);
            field.SetValue(bomb, authoritative);
            root.transform.position = received;
            NetSync.Mode = mode;
            Assert.AreEqual(replica ? received : authoritative, bomb.GetLogicalPosition());
            Assert.AreEqual(authoritative, field.GetValue(bomb), "Reading a replica must not alter simulation state.");
        }
        finally { NetSync.Mode = previous; Object.DestroyImmediate(root); }
    }

    [TestCase(NetworkBombFx.BombMotionKind.Kick, false)]
    [TestCase(NetworkBombFx.BombMotionKind.Punch, true)]
    [TestCase(NetworkBombFx.BombMotionKind.Throw, true)]
    [TestCase(NetworkBombFx.BombMotionKind.Lift, true)]
    [TestCase(NetworkBombFx.BombMotionKind.Carry, true)]
    [TestCase(NetworkBombFx.BombMotionKind.Yellow, false)]
    [TestCase(NetworkBombFx.BombMotionKind.Search, false)]
    [TestCase(NetworkBombFx.BombMotionKind.Magnet, false)]
    public void OnlyAirborneAndCarriedBombPresentationSuppressesCollision(NetworkBombFx.BombMotionKind kind, bool expected)
        => Assert.AreEqual(expected, NetworkBombFx.NonBlockingMotion(kind));

    [Test]
    public void BombArcsCarryAndRubberBounceUseBoundedOfflineGeometry()
    {
        Assert.AreEqual(Vector2.zero, NetworkBombFx.SampleArc(Vector2.zero, Vector2.right * 3, 0.3f, 0, 1));
        Assert.Less(Vector2.Distance(new Vector2(1.5f, 1), NetworkBombFx.SampleArc(Vector2.zero, Vector2.right * 3, 0.3f, 0.15f, 1)), 0.001f);
        Assert.Less(Vector2.Distance(Vector2.right * 3, NetworkBombFx.SampleArc(Vector2.zero, Vector2.right * 3, 0.3f, 5, 1)), 0.001f);
        var lift = new NetworkBombFx.KickMotion { kind = NetworkBombFx.BombMotionKind.Lift, carryHeight = 1, duration = 0.1f };
        Assert.AreEqual(new Vector2(4, 2.5f), NetworkBombFx.SampleCarry(lift, new Vector2(4, 2), 0.05f));
        lift.kind = NetworkBombFx.BombMotionKind.Carry;
        Assert.AreEqual(new Vector2(7, 4), NetworkBombFx.SampleCarry(lift, new Vector2(7, 3), 0));
        Assert.AreEqual(0.75f, NetworkBombFx.SampleBoundedBounce(Vector2.zero, Vector2.right, 0.1f, 0.125f).x, 0.001f);
        Assert.AreEqual(0, NetworkBombFx.SampleBoundedBounce(Vector2.zero, Vector2.right, 0.1f, 100).x, 0.001f);
    }

    [Test]
    public void BombAbilityPresentationConfirmsRollsBackCarriesAndRejectsStaleOwners()
    {
        var mode = NetSync.Mode;
        var clientState = typeof(NetworkClient).GetField("connectState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var oldState = clientState.GetValue(null);
        var actor = new GameObject("BombMotionOwner"); actor.SetActive(false); actor.AddComponent<BoxCollider2D>();
        var replica = new GameObject("BombMotionReplica"); replica.SetActive(false); replica.AddComponent<CircleCollider2D>();
        var observer = new GameObject("BombMotionObserver"); observer.SetActive(false); observer.AddComponent<BoxCollider2D>();
        var grid = new GameObject("BombMotionGrid", typeof(Grid));
        var mapObject = new GameObject("BombMotionMap", typeof(UnityEngine.Tilemaps.Tilemap), typeof(UnityEngine.Tilemaps.TilemapRenderer));
        mapObject.transform.SetParent(grid.transform);
        var tile = ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();
        const uint id = uint.MaxValue - 70;
        try
        {
            clientState.SetValue(null, System.Enum.Parse(clientState.FieldType, "Connected")); NetSync.Mode = NetSync.NetMode.Client;
            var ownerId = actor.AddComponent<NetworkIdentity>();
            var fx = actor.AddComponent<NetworkBombFx>();
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            typeof(NetworkIdentity).GetMethod("InitializeNetworkBehaviours", flags).Invoke(ownerId, null);
            typeof(NetworkIdentity).GetProperty("isLocalPlayer").SetValue(ownerId, true);
            var otherId = observer.AddComponent<NetworkIdentity>();
            var otherFx = observer.AddComponent<NetworkBombFx>();
            typeof(NetworkIdentity).GetMethod("InitializeNetworkBehaviours", flags).Invoke(otherId, null);
            var bomb = replica.AddComponent<Bomb>(); var bombId = replica.AddComponent<NetworkIdentity>();
            typeof(NetworkIdentity).GetProperty("netId").SetValue(bombId, id); NetworkClient.spawned.Add(id, bombId);
            replica.SetActive(true);
            var collider = replica.GetComponent<Collider2D>(); collider.enabled = true;
            Vector2 origin = new Vector2(2000, 2000); actor.transform.position = origin; replica.transform.position = origin;
            var punch = new NetworkBombFx.KickMotion { kind = NetworkBombFx.BombMotionKind.Punch, bombId = id, inputTick = 10,
                start = origin, end = origin + Vector2.right * 3, duration = 0.3f, arcHeight = 1, moving = true, first = true };
            Assert.IsTrue(fx.BeginAbilityPrediction(bomb, punch, Vector2.right, 0.1f));
            Assert.Greater(replica.transform.position.x, origin.x, "Punch presentation starts before an RPC.");
            Assert.IsFalse(collider.enabled); Assert.IsTrue(bomb.ClientMotionNonBlocking);
            Assert.IsFalse(bomb.IsBeingPunched); Assert.IsFalse(bomb.IsFusePaused, "Client presentation must not pause the gameplay fuse.");
            var immediatePunch = (Vector2)replica.transform.position;
            otherFx.ReceiveKickMotion(new NetworkBombFx.KickMotion { bombId = id, revision = 1,
                kind = NetworkBombFx.BombMotionKind.Search, moving = false, start = origin, end = origin, serverTime = NetworkBombFx.KickServerNow });
            Assert.AreEqual(immediatePunch, (Vector2)replica.transform.position, "Another presenter's in-flight stop must not cancel our pending command.");
            Assert.IsTrue(fx.HasUnconfirmedClientMotion(bomb));
            fx.ReconcilePredictedKicks(10);
            Assert.IsTrue(collider.enabled); Assert.IsFalse(bomb.ClientMotionNonBlocking); Assert.AreEqual(origin, (Vector2)replica.transform.position);
            Assert.IsFalse(fx.AbilityPredictionLocksMovement);
            fx.PrepareClientMovementTick(11, false, 1);
            var lift = new NetworkBombFx.KickMotion { kind = NetworkBombFx.BombMotionKind.Lift, bombId = id, inputTick = 11,
                start = origin, end = origin + Vector2.up, duration = 0.1f, carryHeight = 1, moving = true, first = true };
            Assert.IsTrue(fx.BeginAbilityPrediction(bomb, lift, Vector2.up, 0.1f));
            lift.revision = 1; lift.serverTime = NetworkBombFx.KickServerNow; fx.ReceiveKickMotion(lift); fx.ReconcilePredictedKicks(11);
            Assert.IsTrue(fx.CarryingBombLocally);
            var carry = new NetworkBombFx.KickMotion { kind = NetworkBombFx.BombMotionKind.Carry, bombId = id, inputTick = 12,
                revision = 2, carryHeight = 1, moving = true, first = false, serverTime = NetworkBombFx.KickServerNow };
            fx.ReceiveKickMotion(carry);
            var motor = actor.GetComponent<MovementController>(); motor.SetExternalVisualSuppressed(true);
            Assert.IsTrue(motor.CanPredictNetworkMovement, "Normal glove carry must retain movement prediction.");
            actor.transform.position = origin + Vector2.right;
            typeof(NetworkBombFx).GetMethod("LateUpdate", flags).Invoke(fx, null);
            Assert.AreEqual(origin + Vector2.one, (Vector2)replica.transform.position, "Carry follows the client's current body, not its buffered server transform.");
            var stop = new NetworkBombFx.KickMotion { kind = NetworkBombFx.BombMotionKind.Carry, bombId = id, inputTick = 15, revision = 3,
                end = origin + Vector2.right, moving = false, serverTime = NetworkBombFx.KickServerNow + 0.01 };
            fx.ReceiveKickMotion(stop); Assert.IsTrue(collider.enabled); Assert.IsFalse(bomb.ClientMotionNonBlocking);
            var automatic = new NetworkBombFx.KickMotion { kind = NetworkBombFx.BombMotionKind.Search, bombId = id, inputTick = 16,
                revision = 4, start = origin + Vector2.right, end = origin + Vector2.right * 2, duration = 0.3f, moving = true, first = true,
                serverTime = NetworkBombFx.KickServerNow + 0.02 };
            otherFx.ReceiveKickMotion(automatic);
            Vector2 actual = replica.transform.position;
            fx.ReceiveKickMotion(stop);
            Assert.AreEqual(actual, (Vector2)replica.transform.position, "An old presenter/revision cannot overwrite a transferred bomb.");
            Assert.IsFalse(otherFx.BeginAbilityPrediction(bomb, punch, Vector2.right, 0.1f), "Observers cannot predict player commands.");
            var map = mapObject.GetComponent<UnityEngine.Tilemaps.Tilemap>();
            map.SetTile(Vector3Int.zero, tile);
            otherFx.ReceiveTileStart(1, Vector3Int.zero, map); Assert.IsFalse(map.HasTile(Vector3Int.zero));
            var reservedOrigin = Physics2D.OverlapPoint(map.GetCellCenterWorld(Vector3Int.zero), LayerMask.GetMask("Stage"));
            Assert.IsNotNull(reservedOrigin, "The approved moving block must still block the local motor.");
            otherFx.ReceiveTileMove(1, Vector3Int.zero, Vector3Int.right, 0.1f);
            var reservedDestination = Physics2D.OverlapPoint(map.GetCellCenterWorld(Vector3Int.right), LayerMask.GetMask("Stage"));
            Assert.IsNotNull(reservedDestination, "Match the server's destination-cell reservation.");
            otherFx.ReceiveTileEnd(1, Vector3Int.right, false); Assert.AreSame(tile, map.GetTile(Vector3Int.right));
            Assert.IsTrue(reservedOrigin == null && reservedDestination == null, "Settling must release temporary reservations.");
            otherFx.ReceiveTileStart(2, Vector3Int.right, map);
            otherFx.ReceiveTileEnd(2, Vector3Int.right, true); Assert.IsFalse(map.HasTile(Vector3Int.right), "Destroyed moving blocks must not be restored.");
            // Short approved arcs queue in order, including the final stop. An
            // early packet must not skip the current apex or introduce a jump.
            var arc = new NetworkBombFx.KickMotion { bombId = id, revision = 5, kind = NetworkBombFx.BombMotionKind.Punch,
                start = origin, end = origin + Vector2.right, duration = 0.1f, arcHeight = 0.5f,
                moving = true, first = true, serverTime = automatic.serverTime + 0.01 };
            otherFx.ReceiveKickMotion(arc); Vector2 firstArcPosition = replica.transform.position;
            arc.revision = 6; arc.first = false; arc.start = arc.end; arc.end += Vector2.up;
            otherFx.ReceiveKickMotion(arc); Assert.AreEqual(firstArcPosition, (Vector2)replica.transform.position);
            arc.revision = 7; arc.moving = false; arc.start = arc.end; arc.duration = 0;
            otherFx.ReceiveKickMotion(arc); Assert.AreEqual(firstArcPosition, (Vector2)replica.transform.position);
            var playbackMap = (System.Collections.IDictionary)typeof(NetworkBombFx).GetField("kickPlayback", flags).GetValue(otherFx);
            object playback = playbackMap[bomb];
            playback.GetType().GetField("elapsed").SetValue(playback, 0.1f);
            typeof(NetworkBombFx).GetMethod("PresentFrame", flags).Invoke(otherFx, new object[] { 0f, 0f });
            Assert.Less(Vector2.Distance(origin + Vector2.right, replica.transform.position), 0.2f);
            playback = playbackMap[bomb]; playback.GetType().GetField("elapsed").SetValue(playback, 0.1f);
            typeof(NetworkBombFx).GetMethod("PresentFrame", flags).Invoke(otherFx, new object[] { 0f, 0f });
            Assert.AreEqual(arc.end, (Vector2)replica.transform.position);
            Assert.IsTrue(collider.enabled);

            map.SetTile(new Vector3Int(1980, 1980), tile); map.SetTile(new Vector3Int(2020, 2020), tile);
            bomb.SetStageBoundsTilemap(map);
            var rubber = new NetworkBombFx.KickMotion { bombId = id, revision = 8, kind = NetworkBombFx.BombMotionKind.Throw,
                start = origin, end = origin + Vector2.right, duration = 0.1f, arcHeight = 0.5f, rubber = true,
                bounceSeed = 0, bounceIndex = -1, bounceDuration = 0.1f, bounceHeight = 0.5f,
                first = true, moving = true, serverTime = automatic.serverTime + 0.02 };
            otherFx.ReceiveKickMotion(rubber);
            playback = playbackMap[bomb]; playback.GetType().GetField("elapsed").SetValue(playback, 0.15f);
            typeof(NetworkBombFx).GetMethod("PresentFrame", flags).Invoke(otherFx, new object[] { 0f, 0f });
            Vector2 anticipated = replica.transform.position;
            Assert.Greater(anticipated.y, rubber.end.y, "A mandatory deterministic bounce starts before its next RPC.");
            rubber.revision = 9; rubber.first = false; rubber.start = rubber.end; rubber.end += Vector2.up; rubber.bounceIndex = 0;
            otherFx.ReceiveKickMotion(rubber);
            Assert.Less(Vector2.Distance(anticipated, replica.transform.position), 0.07f, "Confirmation must preserve the predicted arc phase.");
            playback = playbackMap[bomb]; Assert.That((float)playback.GetType().GetField("elapsed").GetValue(playback), Is.EqualTo(0.05f).Within(0.001f));
            rubber.revision = 10; rubber.moving = false; otherFx.ReceiveKickMotion(rubber);
            playback.GetType().GetField("elapsed").SetValue(playback, 0.1f);
            typeof(NetworkBombFx).GetMethod("PresentFrame", flags).Invoke(otherFx, new object[] { 0f, 0f });
            Assert.IsTrue(collider.enabled);

            var playerCollider = actor.GetComponent<Collider2D>(); playerCollider.enabled = true;
            var hop = new NetworkBombFx.PlayerBounceMotion { revision = 1, first = true, moving = true,
                start = origin, end = origin + Vector2.right, duration = 0.1f, height = 0.35f };
            fx.ReceivePlayerBounce(hop); Assert.IsTrue(fx.PlayerBounceActive); Assert.IsFalse(playerCollider.enabled);
            hop.revision = 2; hop.first = false; hop.start = hop.end; hop.end += Vector2.up;
            fx.ReceivePlayerBounce(hop); Assert.AreEqual(origin, (Vector2)actor.transform.position);
            hop.revision = 3; hop.moving = false; hop.duration = 0;
            fx.ReceivePlayerBounce(hop);
            typeof(NetworkBombFx).GetField("bounceElapsed", flags).SetValue(fx, 0.1f);
            typeof(NetworkBombFx).GetMethod("UpdatePlayerBouncePresentation", flags).Invoke(fx, new object[] { 0f, 0f });
            Assert.IsTrue(fx.PlayerBounceActive);
            Assert.Less(Vector2.Distance(origin + Vector2.right, actor.transform.position), 0.2f);
            typeof(NetworkBombFx).GetField("bounceElapsed", flags).SetValue(fx, 0.1f);
            typeof(NetworkBombFx).GetMethod("UpdatePlayerBouncePresentation", flags).Invoke(fx, new object[] { 0f, 0f });
            Assert.IsFalse(fx.PlayerBounceActive); Assert.IsTrue(playerCollider.enabled);
            Assert.AreEqual(hop.end, (Vector2)actor.transform.position);
            hop.revision = 2; hop.moving = true; fx.ReceivePlayerBounce(hop);
            Assert.IsFalse(fx.PlayerBounceActive, "Stale bounce messages must not restart a completed sequence.");
            NetSync.Mode = NetSync.NetMode.Host; hop.revision = 4; fx.ReceivePlayerBounce(hop);
            Assert.IsFalse(fx.PlayerBounceActive, "Host/offline gameplay must never use replica presentation.");
            NetSync.Mode = NetSync.NetMode.Client;
        }
        finally
        {
            NetworkClient.spawned.Remove(id); clientState.SetValue(null, oldState); NetSync.Mode = mode;
            Object.DestroyImmediate(replica); Object.DestroyImmediate(actor); Object.DestroyImmediate(observer);
            Object.DestroyImmediate(grid); Object.DestroyImmediate(tile);
        }
    }

    [Test]
    public void RubberBounceSequenceIsStableAndDoesNotConsumeGameplayRandom()
    {
        var saved = Random.state;
        try
        {
            uint seed = RubberBombBounceSequence.Seed(new Vector2(-3, 7), 1, Vector2.right, 3);
            var expected = new Vector2[64];
            for (int i = 0; i < expected.Length; i++) expected[i] = RubberBombBounceSequence.Direction(seed, i);
            Random.InitState(12345); var before = Random.state;
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i], RubberBombBounceSequence.Direction(seed, i));
                Assert.AreEqual(1, Mathf.Abs(expected[i].x) + Mathf.Abs(expected[i].y));
            }
            Assert.AreEqual(before, Random.state, "Bounce prediction must not advance Unity's gameplay RNG.");
            Assert.AreEqual(seed, RubberBombBounceSequence.Seed(new Vector2(-6, 14), 2, Vector2.right, 3));
            Assert.AreNotEqual(seed, RubberBombBounceSequence.Seed(new Vector2(-3, 7), 1, Vector2.up, 3));
            Assert.That(RubberBombBounceSequence.MinimumBounces(seed), Is.InRange(3, 5));
            Assert.AreEqual(Vector2.up, RubberBombBounceSequence.Direction(0, 0));
        }
        finally { Random.state = saved; }
    }

    [Test]
    public void PlayerBounceUsesOfflineParabolaAndClampedEndpoints()
    {
        Vector2 from = new(20, 30), to = from + Vector2.right;
        Assert.AreEqual(from, NetworkBombFx.SamplePlayerBounce(from, to, 0.1f, -1, 0.35f));
        Assert.AreEqual(to, NetworkBombFx.SamplePlayerBounce(from, to, 0.1f, 10, 0.35f));
        Assert.Less(Vector2.Distance(from + new Vector2(0.5f, 0.35f),
            NetworkBombFx.SamplePlayerBounce(from, to, 0.1f, 0.05f, 0.35f)), 0.0001f);
        var motion = new NetworkBombFx.PlayerBounceMotion { revision = uint.MaxValue, start = from, end = to,
            duration = 0.1f, curveDuration = 1f / 12, height = 0.35f, moving = true, first = true };
        using var writer = NetworkWriterPool.Get(); writer.Write(motion);
        using var reader = NetworkReaderPool.Get(writer.ToArraySegment()); var received = reader.Read<NetworkBombFx.PlayerBounceMotion>();
        Assert.AreEqual(motion.revision, received.revision); Assert.AreEqual(motion.start, received.start); Assert.AreEqual(motion.end, received.end);
        Assert.AreEqual(motion.duration, received.duration); Assert.AreEqual(motion.curveDuration, received.curveDuration);
        Assert.AreEqual(motion.height, received.height); Assert.IsTrue(received.first && received.moving);
    }

    [Test]
    public void KickMotionWireRoundTripPreservesSegmentAndAuthoritativeTick()
    {
        var motion = new NetworkBombFx.KickMotion { bombId = 123, inputTick = 49,
            start = Vector2.one, end = Vector2.one + Vector2.left, duration = 0.1f,
            serverTime = 456.75, moving = true, first = true, kind = NetworkBombFx.BombMotionKind.Throw,
            revision = 77, arcHeight = 0.9f, carryHeight = 1, rubber = true,
            bounceSeed = 1234, bounceIndex = 2, bounceDuration = 0.12f, bounceHeight = 0.9f, curveDuration = 0.11f };
        using var writer = NetworkWriterPool.Get();
        writer.Write(motion);
        using var reader = NetworkReaderPool.Get(writer.ToArraySegment());
        var result = reader.Read<NetworkBombFx.KickMotion>();
        Assert.AreEqual(motion.kind, result.kind);
        Assert.AreEqual(motion.revision, result.revision);
        Assert.AreEqual(motion.arcHeight, result.arcHeight);
        Assert.AreEqual(motion.carryHeight, result.carryHeight);
        Assert.IsTrue(result.rubber);
        Assert.AreEqual(motion.curveDuration, result.curveDuration);
        Assert.AreEqual(motion.bounceSeed, result.bounceSeed); Assert.AreEqual(motion.bounceIndex, result.bounceIndex);
        Assert.AreEqual(motion.bounceDuration, result.bounceDuration); Assert.AreEqual(motion.bounceHeight, result.bounceHeight);
        Assert.AreEqual(motion.bombId, result.bombId);
        Assert.AreEqual(motion.inputTick, result.inputTick);
        Assert.AreEqual(motion.start, result.start);
        Assert.AreEqual(motion.end, result.end);
        Assert.AreEqual(motion.duration, result.duration);
        Assert.AreEqual(motion.serverTime, result.serverTime);
        Assert.IsTrue(result.moving);
        Assert.IsTrue(result.first);
    }

    [Test]
    public void PredictionHistoryAcknowledgesInputsAcrossSequenceRollover()
    {
        var history = new NetworkInputHistory();
        history.Add(new NetworkInputFrame { tick = uint.MaxValue - 1 });
        history.Add(new NetworkInputFrame { tick = uint.MaxValue });
        history.Add(new NetworkInputFrame { tick = 0 });
        history.Add(new NetworkInputFrame { tick = 1 });
        history.DiscardThrough(uint.MaxValue);
        Assert.AreEqual(2, history.Count);
        Assert.AreEqual(0u, history[0].tick);
        history.DiscardThrough(0);
        Assert.AreEqual(1u, history[0].tick);
        Assert.IsFalse(NetworkInputHistory.IsNewer(0, 1));
        Assert.IsFalse(NetworkInputHistory.IsNewer(1, 1));
        Assert.IsTrue(NetworkInputHistory.IsNewer(0, uint.MaxValue));
    }

    [Test]
    public void PredictionHistoryBoundsAnOutageAndRetainsOnlyUnacknowledgedTicks()
    {
        var history = new NetworkInputHistory();
        for (uint tick = 1; tick <= NetworkInputHistory.Capacity + 20; tick++)
            history.Add(new NetworkInputFrame { tick = tick, held = 8 });
        Assert.AreEqual(NetworkInputHistory.Capacity, history.Count);
        Assert.AreEqual(21u, history[0].tick);
        history.DiscardThrough(40);
        Assert.AreEqual(41u, history[0].tick);
        history.DiscardThrough(NetworkInputHistory.Capacity + 20);
        Assert.AreEqual(0, history.Count);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void PredictionRollbackReplaysTheMotorIncludingSubpixelProgressAndTurns(bool predict)
    {
        var previous = NetSync.Mode;
        var player = new GameObject("PredictionMotorRegression");
        player.SetActive(false);
        var obstacle = new GameObject("PredictionObstacleRegression");
        try
        {
            NetSync.Mode = predict ? NetSync.NetMode.Client : NetSync.NetMode.Host;
            player.tag = "Player";
            player.AddComponent<BoxCollider2D>();
            var body = player.AddComponent<Rigidbody2D>();
            var movement = player.AddComponent<MovementController>();
            body.bodyType = RigidbodyType2D.Kinematic;
            // EditMode does not run Awake. Bind only the motor's required body;
            // avoid booting save/session singletons or changing the open scene.
            typeof(MovementController).GetProperty(nameof(MovementController.Rigidbody)).SetValue(movement, body);
            movement.SetNetworkTickDriven(true);
            movement.SetPredictLocally(predict);
            player.SetActive(true);
            var initial = new MovementController.NetworkMovementState
            {
                position = new Vector2(1000, 1000), facing = Vector2.down,
                worldSpeed = 4.1f, animationSpeed = PlayerPersistentStats.BaseSpeedNormal, canPredict = true
            };
            movement.RestoreNetworkMovementState(initial);
            var inputs = new[] { 8, 8, 8, 9, 9, 1, 1, 1, 0 };
            for (int i = 0; i < 3; i++) movement.SimulateNetworkMovementTick(inputs[i]);
            var acknowledged = movement.CaptureNetworkMovementState();
            // Capture uses authoritative speed, so preserve the known test speed.
            acknowledged.worldSpeed = initial.worldSpeed;
            for (int i = 3; i < inputs.Length; i++) movement.SimulateNetworkMovementTick(inputs[i]);
            var expected = movement.CaptureNetworkMovementState();
            Assert.Greater(expected.position.x, initial.position.x);
            movement.RestoreNetworkMovementState(acknowledged);
            for (int i = 3; i < inputs.Length; i++) movement.SimulateNetworkMovementTick(inputs[i]);
            Assert.AreEqual(JsonUtility.ToJson(expected), JsonUtility.ToJson(movement.CaptureNetworkMovementState()));
            obstacle.layer = LayerMask.NameToLayer("Stage");
            obstacle.transform.position = initial.position + Vector2.right * 2;
            obstacle.AddComponent<BoxCollider2D>();
            Physics2D.SyncTransforms();
            movement.obstacleMask = LayerMask.GetMask("Stage");
            movement.RefreshObstacleContactFilter();
            movement.RestoreNetworkMovementState(initial);
            for (int i = 0; i < 40; i++) movement.SimulateNetworkMovementTick(8);
            Assert.LessOrEqual(body.position.x, initial.position.x + 1, "Replay must query walls rather than blindly adding deltas.");
            Assert.Greater(body.position.x, initial.position.x);
            movement.SetExternalMovementOverride(true);
            Assert.IsFalse(movement.CanPredictNetworkMovement, "Launchers and other external motion require server presentation.");
            movement.SetExternalMovementOverride(false);
            movement.isDead = true;
            Assert.IsFalse(movement.CanPredictNetworkMovement);
        }
        finally { NetSync.Mode = previous; Object.DestroyImmediate(player); Object.DestroyImmediate(obstacle); }
    }

    [TestCase(MountedType.Blue)]
    [TestCase(MountedType.Black)]
    [TestCase(MountedType.Purple)]
    [TestCase(MountedType.Green)]
    [TestCase(MountedType.Yellow)]
    [TestCase(MountedType.Pink)]
    [TestCase(MountedType.Red)]
    [TestCase(MountedType.Mole)]
    [TestCase(MountedType.Tank)]
    public void ConfirmedMountedMovementReplaysAndWaitsForMatchingVisual(MountedType type)
    {
        var oldMode = NetSync.Mode; var player = new GameObject("MountedPredictionRegression"); player.SetActive(false);
        var visual = new GameObject("MountedPredictionVisual"); visual.transform.SetParent(player.transform);
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        try
        {
            NetSync.Mode = NetSync.NetMode.Client; player.tag = "Player"; player.AddComponent<BoxCollider2D>();
            var body = player.AddComponent<Rigidbody2D>(); body.bodyType = RigidbodyType2D.Kinematic;
            var movement = player.AddComponent<MovementController>();
            typeof(MovementController).GetProperty(nameof(MovementController.Rigidbody)).SetValue(movement, body);
            var companion = player.AddComponent<PlayerMountCompanion>();
            typeof(PlayerMountCompanion).GetField("currentLouie", flags).SetValue(companion, visual);
            typeof(PlayerMountCompanion).GetField("mountedType", flags).SetValue(companion, type);
            movement.SetNetworkTickDriven(true); player.SetActive(true);
            var state = new MovementController.NetworkMovementState { position = new Vector2(3000, 3000),
                facing = Vector2.down, worldSpeed = 4.1f, animationSpeed = PlayerPersistentStats.BaseSpeedNormal,
                mount = type, movementCapabilities = 3, canPredict = true };
            movement.RestoreNetworkMovementState(state); movement.SetPredictLocally(true);
            Assert.IsTrue(movement.IsMounted); Assert.IsTrue(movement.CanPredictNetworkMovement);
            for (int i = 0; i < 4; i++) movement.SimulateNetworkMovementTick(8);
            var ack = movement.CaptureNetworkMovementState(); ack.worldSpeed = state.worldSpeed;
            for (int i = 0; i < 4; i++) movement.SimulateNetworkMovementTick(1);
            Vector2 predicted = body.position;
            movement.RestoreNetworkMovementState(ack);
            for (int i = 0; i < 4; i++) movement.SimulateNetworkMovementTick(1, replay: true);
            Assert.AreEqual(predicted, body.position);
            Assert.Greater(predicted.x, state.position.x); Assert.Greater(predicted.y, state.position.y);
            typeof(PlayerMountCompanion).GetField("mountedType", flags).SetValue(companion, MountedType.None);
            Assert.AreEqual("mount_context_pending", movement.NetworkPredictionBlockReason);
            typeof(PlayerMountCompanion).GetField("mountedType", flags).SetValue(companion, type);
            movement.SetInputLocked(true, false); Assert.IsFalse(movement.CanPredictNetworkMovement);
            movement.SetInputLocked(false, false); movement.SetExternalMovementOverride(true);
            Assert.IsFalse(movement.CanPredictNetworkMovement, "Mounted launchers must retain authoritative presentation.");
            movement.SetExternalMovementOverride(false);
            state.mount = MountedType.None; movement.RestoreNetworkMovementState(state);
            Assert.IsFalse(movement.IsMounted);
            using var writer = NetworkWriterPool.Get(); writer.Write(ack);
            using var reader = NetworkReaderPool.Get(writer.ToArraySegment()); var wire = reader.Read<MovementController.NetworkMovementState>();
            Assert.AreEqual(type, wire.mount); Assert.AreEqual(3, wire.movementCapabilities);
        }
        finally { NetSync.Mode = oldMode; Object.DestroyImmediate(player); }
    }

    [Test]
    public void ReplayUsesAcknowledgedPassCapabilitiesInsteadOfNewerAbilityFlags()
    {
        var oldMode = NetSync.Mode; var player = new GameObject("MovementCapabilityRegression"); player.SetActive(false);
        var wall = new GameObject("MovementCapabilityWall");
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        try
        {
            NetSync.Mode = NetSync.NetMode.Client; player.tag = "Player"; player.AddComponent<BoxCollider2D>();
            var body = player.AddComponent<Rigidbody2D>(); body.bodyType = RigidbodyType2D.Kinematic;
            var movement = player.AddComponent<MovementController>();
            typeof(MovementController).GetProperty(nameof(MovementController.Rigidbody)).SetValue(movement, body);
            var abilities = player.GetComponent<AbilitySystem>();
            typeof(MovementController).GetField("abilitySystem", flags).SetValue(movement, abilities);
            var pass = player.AddComponent<DestructiblePassAbility>(); abilities.RebuildCache();
            player.SetActive(true); movement.SetNetworkTickDriven(true); movement.SetPredictLocally(true);
            wall.layer = LayerMask.NameToLayer("Stage"); wall.tag = "Destructibles";
            wall.transform.position = new Vector2(4002, 4000); wall.AddComponent<BoxCollider2D>(); Physics2D.SyncTransforms();
            movement.obstacleMask = LayerMask.GetMask("Stage"); movement.RefreshObstacleContactFilter();
            var state = new MovementController.NetworkMovementState { position = new Vector2(4000, 4000), facing = Vector2.right,
                worldSpeed = 4.1f, animationSpeed = PlayerPersistentStats.BaseSpeedNormal, movementCapabilities = 2, canPredict = true };
            movement.RestoreNetworkMovementState(state);
            for (int i = 0; i < 40; i++) movement.SimulateNetworkMovementTick(8, replay: true);
            Assert.Greater(body.position.x, 4002, "The acknowledged pass state remains valid even before the visual ability flag arrives.");
            typeof(DestructiblePassAbility).GetField("enabledAbility", flags).SetValue(pass, true);
            state.movementCapabilities = 0; movement.RestoreNetworkMovementState(state);
            for (int i = 0; i < 40; i++) movement.SimulateNetworkMovementTick(8, replay: true);
            Assert.LessOrEqual(body.position.x, 4001, "A newer pass ability cannot rewrite older movement history.");
        }
        finally { NetSync.Mode = oldMode; Object.DestroyImmediate(player); Object.DestroyImmediate(wall); }
    }

    [TestCase(NetSync.NetMode.Offline, true)]
    [TestCase(NetSync.NetMode.Host, true)]
    [TestCase(NetSync.NetMode.Client, false)]
    public void ServerMovesHumanPhysicsWithoutOwnerPrediction(NetSync.NetMode mode, bool expected)
    {
        var previous = NetSync.Mode;
        var player = new GameObject("ServerAuthorityRegression");
        player.SetActive(false);
        try
        {
            NetSync.Mode = mode;
            player.AddComponent<BoxCollider2D>();
            var movement = player.AddComponent<MovementController>();
            movement.SetPredictLocally(false);
            var property = typeof(MovementController).GetProperty("ShouldMovePhysicsLocally",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.AreEqual(expected, property.GetValue(movement));
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
            Assert.AreEqual(SyncDirection.ServerToClient, prefab.GetComponent<NetworkTransformReliable>().syncDirection);
        }
        finally
        {
            NetSync.Mode = previous;
            Object.DestroyImmediate(player);
        }
    }

    [TestCase("0.5.0", OnlineVersionAuthenticator.ProtocolVersion, true)]
    [TestCase("0.5.0", 33, false)]
    [TestCase("0.5.0", 34, false)]
    [TestCase("0.5.0", 35, false)]
    [TestCase("0.5.0", 36, false)]
    [TestCase("0.5.0", 32, false)]
    [TestCase("0.5.0", 31, false)]
    [TestCase("0.5.0", 30, false)]
    [TestCase("0.5.0", 29, false)]
    [TestCase("0.5.0", 28, false)]
    [TestCase("0.5.0", 27, false)]
    [TestCase("0.5.0", 26, false)]
    [TestCase("0.5.0", 25, false)]
    [TestCase("0.5.0", 24, false)]
    [TestCase("0.5.0", 23, false)]
    [TestCase("0.5.0", 22, false)]
    [TestCase("0.5.0", 21, false)]
    [TestCase("0.5.0", 20, false)]
    [TestCase("0.5.0", 19, false)]
    [TestCase("0.5.0", 18, false)]
    [TestCase("0.5.0", 17, false)]
    [TestCase("0.5.0", 14, false)]
    [TestCase("0.5.0", 13, false)]
    [TestCase("0.5.0", 12, false)]
    [TestCase("0.5.0", 11, false)]
    [TestCase("0.5.0", 10, false)]
    [TestCase("0.5.0", 9, false)]
    [TestCase("0.5.0", 8, false)]
    [TestCase("0.5.0", 7, false)]
    [TestCase("0.5.0", 6, false)]
    [TestCase("0.5.0", 5, false)]
    [TestCase("0.5.0", 4, false)]
    [TestCase("0.5.0", 3, false)]
    [TestCase("0.5.1", 1, false)]
    [TestCase("0.5.0", 1, false)]
    [TestCase("0.5.0", 2, false)]
    [TestCase("", 1, false)]
    public void OnlineCompatibilityRequiresSameBuildAndProtocol(string remoteVersion, int protocol, bool expected)
    {
        Assert.AreEqual(expected, OnlineVersionAuthenticator.Compatible("0.5.0", remoteVersion, protocol));
    }

    [Test]
    public void CoopCampaignUsesTemporarySaveAndRestoresOfflineData()
    {
        var offline = SaveSystem.Data;
        string original = JsonUtility.ToJson(offline);
        try
        {
            SaveSystem.BeginOnlineCoopSettings();
            Assert.AreNotSame(offline, SaveSystem.Data);
            SaveSystem.SetActiveSlot(0);
            SaveSystem.ResetSlot(0, Assets.Scripts.SaveSystem.NormalGameDifficulty.Hard);
            SaveSystem.ActiveSlot.clearedStages.Add("Stage_1-1");
            SaveSystem.Reload();
            Assert.Contains("Stage_1-1", SaveSystem.ActiveSlot.clearedStages);
            Assert.AreEqual(original, JsonUtility.ToJson(offline));
        }
        finally { SaveSystem.EndOnlineCoopSettings(); }
        Assert.AreSame(offline, SaveSystem.Data);
        Assert.AreEqual(original, JsonUtility.ToJson(SaveSystem.Data));
    }

    [Test]
    public void OwnerProgressRetainsCampaignAndNeverImportsHardwarePreferences()
    {
        var offline = SaveSystem.Data;
        string original = JsonUtility.ToJson(offline);
        string controls = JsonUtility.ToJson(offline.controls[0]);
        try
        {
            SaveSystem.BeginOnlineCoopSettings();
            var incoming = JsonUtility.FromJson<SaveData>(original);
            incoming.activeSlotIndex = 1;
            incoming.slots[1].started = true;
            incoming.slots[1].difficulty = (int)Assets.Scripts.SaveSystem.NormalGameDifficulty.Hard;
            incoming.slots[1].unlockedStages.Add("Stage_2-5");
            incoming.controls.Clear(); incoming.language = 123;
            incoming.bossRushUnlocked = true;
            Assert.IsTrue(SaveSystem.ApplyRoomProgress(JsonUtility.ToJson(incoming), false));
            Assert.AreEqual(1, SaveSystem.Data.activeSlotIndex);
            Assert.Contains("Stage_2-5", SaveSystem.ActiveSlot.unlockedStages);
            Assert.AreEqual(Assets.Scripts.SaveSystem.NormalGameDifficulty.Hard, SaveSystem.GetActiveNormalGameDifficulty());
            Assert.AreEqual(controls, JsonUtility.ToJson(SaveSystem.Data.controls[0]));
            Assert.AreEqual(offline.language, SaveSystem.Data.language);
            Assert.IsTrue(SaveSystem.Data.bossRushUnlocked);
            Assert.AreSame(SaveSystem.Data, SaveSystem.BattleConfigurationData);
            Assert.AreEqual(original, JsonUtility.ToJson(offline));
            var capturedOriginal = JsonUtility.FromJson<SaveData>(SaveSystem.CaptureRoomOwnerSave());
            Assert.AreEqual(offline.activeSlotIndex, capturedOriginal.activeSlotIndex);
            Assert.IsTrue(capturedOriginal.controls == null || capturedOriginal.controls.Count == 0);
        }
        finally { SaveSystem.EndOnlineBattleSettings(); SaveSystem.EndOnlineCoopSettings(); }
        Assert.AreSame(offline, SaveSystem.Data);
    }

    [TestCase("")]
    [TestCase("{broken")]
    [TestCase("{\"slots\":[]}")]
    public void InvalidOwnerSaveCannotReplaceLocalProgress(string json)
    {
        var before = SaveSystem.Data;
        Assert.IsFalse(SaveSystem.ApplyRoomProgress(json, false));
        Assert.AreSame(before, SaveSystem.Data);
    }

    [TestCase("SaveFileMenu", true)]
    [TestCase("WorldMap", true)]
    [TestCase("BossRush", true)]
    [TestCase("Stage_1-1", false)]
    public void NativeConfigurationScenesAreRecognized(string scene, bool expected)
    {
        Assert.AreEqual(expected, BombermanNetworkManager.IsOwnerMenuScene(scene));
    }

    [Test]
    public void CoopVisualBatchesRoundTripWithoutLosingPoseOrTileRemoval()
    {
        var frame = new CoopSpriteFrame { id = 12, sprite = 3, visible = true,
            position = new Vector3(1, 2, 0), scale = Vector3.one, rotation = Quaternion.identity, color = Color.red };
        using var writer = NetworkWriterPool.Get();
        writer.Write(new CoopSprites { scene = "Stage_1-1", frames = new[] { frame }, removed = new[] { 20 } });
        using var reader = NetworkReaderPool.Get(writer.ToArraySegment());
        var result = reader.Read<CoopSprites>();
        Assert.AreEqual("Stage_1-1", result.scene);
        Assert.AreEqual(frame.position, result.frames[0].position);
        Assert.AreEqual(frame.color, result.frames[0].color);
        Assert.IsTrue(result.frames[0].visible);
        CollectionAssert.AreEqual(new[] { 20 }, result.removed);
    }

    [Test]
    public void CoopTileDeltasPreserveRemovalRecreationAndTransformChanges()
    {
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var managerObject = new GameObject("CoopDeltaRegression");
        managerObject.SetActive(false);
        var grid = new GameObject("DeltaGrid", typeof(Grid));
        var mapObject = new GameObject("DeltaMap", typeof(UnityEngine.Tilemaps.Tilemap));
        mapObject.transform.SetParent(grid.transform);
        var tile = ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();
        try
        {
            var manager = managerObject.AddComponent<BombermanNetworkManager>();
            var map = mapObject.GetComponent<UnityEngine.Tilemaps.Tilemap>();
            var type = typeof(BombermanNetworkManager);
            var ids = (Dictionary<UnityEngine.Tilemaps.TileBase, int>)type.GetField("coopTileIds", flags).GetValue(manager);
            ids[tile] = 7;
            var capture = type.GetMethod("CaptureCoopTiles", flags);
            var changes = (List<CoopTileFrame>)type.GetField("coopChangedTiles", flags).GetValue(manager);
            var peers = new List<NetworkConnectionToClient>();
            var arguments = new object[] { "DeltaMap", map, peers };
            map.SetTile(Vector3Int.zero, tile);
            capture.Invoke(manager, arguments);
            Assert.AreEqual(7, changes[0].tile);
            map.SetTile(Vector3Int.zero, null);
            capture.Invoke(manager, arguments);
            Assert.AreEqual(1, changes.Count);
            Assert.AreEqual(-1, changes[0].tile);
            map.SetTile(Vector3Int.zero, tile);
            map.SetTileFlags(Vector3Int.zero, UnityEngine.Tilemaps.TileFlags.None);
            map.SetColor(Vector3Int.zero, Color.blue);
            var matrix = Matrix4x4.Translate(Vector3.up);
            map.SetTransformMatrix(Vector3Int.zero, matrix);
            capture.Invoke(manager, arguments);
            Assert.AreEqual(7, changes[0].tile);
            Assert.AreEqual(Color.blue, changes[0].color);
            Assert.AreEqual(matrix, changes[0].matrix);
            capture.Invoke(manager, arguments);
            Assert.IsEmpty(changes);
            map.SetTile(Vector3Int.zero, null);
            capture.Invoke(manager, arguments);
            Assert.AreEqual(-1, changes[0].tile);
            capture.Invoke(manager, arguments);
            Assert.IsEmpty(changes);
        }
        finally
        {
            Object.DestroyImmediate(grid); Object.DestroyImmediate(tile); Object.DestroyImmediate(managerObject);
        }
    }

    [Test]
    public void DedicatedFramePercentilesKeepTailAndOverflowStalls()
    {
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var managerObject = new GameObject("FramePercentileRegression");
        managerObject.SetActive(false);
        try
        {
            var manager = managerObject.AddComponent<BombermanNetworkManager>();
            var type = typeof(BombermanNetworkManager);
            var histogram = (int[])type.GetField("dedicatedFrameHistogram", flags).GetValue(manager);
            histogram[40] = 95; histogram[160] = 4; histogram[histogram.Length - 1] = 1;
            type.GetField("dedicatedFrames", flags).SetValue(manager, 100);
            type.GetField("dedicatedMaxFrame", flags).SetValue(manager, 2.5f);
            var percentile = type.GetMethod("DedicatedFramePercentile", flags);
            Assert.AreEqual(10f, percentile.Invoke(manager, new object[] { 0.50f }));
            Assert.AreEqual(10f, percentile.Invoke(manager, new object[] { 0.95f }));
            Assert.AreEqual(40f, percentile.Invoke(manager, new object[] { 0.99f }));
            Assert.AreEqual(2500f, percentile.Invoke(manager, new object[] { 1f }));
        }
        finally { Object.DestroyImmediate(managerObject); }
    }

    [Test]
    public void RoomAdminIsRetainedAndNeverTransferredOnDeparture()
    {
        Assert.AreEqual(2, BombermanNetworkManager.FindRoomAdmin(new[] { 4, 2 }));
        Assert.AreEqual(4, BombermanNetworkManager.FindRoomAdmin(new[] { 1, 4 }, 4));
        Assert.AreEqual(0, BombermanNetworkManager.FindRoomAdmin(new[] { 5, 2 }, 4));
        Assert.AreEqual(0, BombermanNetworkManager.FindRoomAdmin(System.Array.Empty<int>(), 4));
    }

    [TestCase(OnlineRoomMode.BattleMode, 6)]
    [TestCase(OnlineRoomMode.NormalGame, 4)]
    [TestCase(OnlineRoomMode.BossRush, 4)]
    public void RoomModesUseFixedCapacityAndRejectAnExtraSlot(OnlineRoomMode mode, int capacity)
    {
        Assert.AreEqual(capacity, OnlineRoomRules.Capacity(mode));
        Assert.AreEqual(mode, OnlineRoomRules.ReadMode(new[] { "-room-mode", mode.ToString() }));
        var slots = new List<int>();
        for (int id = 1; id <= capacity; id++) slots.Add(id);
        Assert.AreEqual(0, BombermanNetworkManager.FindAvailableRoomSlot(slots, capacity));
        slots.Remove(2);
        Assert.AreEqual(2, BombermanNetworkManager.FindAvailableRoomSlot(slots, capacity));
        Assert.Throws<System.ArgumentException>(() => OnlineRoomRules.ReadMode(new[] { "-room-mode", "invalid" }));
    }

    [Test]
    public void DedicatedOptionsValidatePortsAndKeepProcessesIndependent()
    {
        Assert.AreEqual(7777, BombermanNetworkManager.ReadDedicatedNumber(new string[0], "-port", 7777, 1, 65535));
        Assert.AreEqual(7778, BombermanNetworkManager.ReadDedicatedNumber(new[] { "-port", "7778" }, "-port", 7777, 1, 65535));
        foreach (var value in new[] { "0", "65536", "invalid" })
            Assert.Throws<System.ArgumentException>(() => BombermanNetworkManager.ReadDedicatedNumber(
                new[] { "-port", value }, "-port", 7777, 1, 65535));
        Assert.Throws<System.ArgumentException>(() => BombermanNetworkManager.ReadDedicatedNumber(
            new[] { "-port" }, "-port", 7777, 1, 65535));
    }

    [TearDown]
    public void EndRoomSettings()
    {
        SaveSystem.EndOnlineBattleSettings();
        PlayerPersistentStats.ApplyOnlineSelections(SaveSystem.BattleConfigurationData);
    }

    [TestCase(NetSync.NetMode.Offline)]
    [TestCase(NetSync.NetMode.Host)]
    [TestCase(NetSync.NetMode.Client)]
    public void UnspawnedNetworkPlayerDoesNotOverwriteLocalSelectionOrPickups(NetSync.NetMode mode)
    {
        PlayerPersistentStats.EnsureSessionBooted();
        var baseState = PlayerPersistentStats.Get(1);
        var runtimeState = PlayerPersistentStats.GetRuntime(1);
        var copyState = typeof(PlayerPersistentStats).GetMethod("CopyState",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        var savedBase = new PlayerPersistentStats.PlayerState();
        var savedRuntime = new PlayerPersistentStats.PlayerState();
        copyState.Invoke(null, new object[] { baseState, savedBase });
        copyState.Invoke(null, new object[] { runtimeState, savedRuntime });
        var previousMode = NetSync.Mode;
        var player = new GameObject("LocalPlayerPersistenceRegression");
        player.SetActive(false);
        try
        {
            NetSync.Mode = mode;
            baseState.Character = runtimeState.Character = BomberCharacter.LadyBomber;
            baseState.Skin = runtimeState.Skin = BomberSkin.Palette7;
            baseState.BombAmount = runtimeState.BombAmount = 3;
            baseState.ExplosionRadius = runtimeState.ExplosionRadius = 4;
            baseState.SpeedInternal = runtimeState.SpeedInternal = PlayerPersistentStats.BaseSpeedNormal;
            var networkState = player.AddComponent<NetworkPlayerState>();
            foreach (string method in new[] { "LateUpdate", "ClientApply" })
            {
                typeof(NetworkPlayerState).GetMethod(method,
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(networkState, null);
                Assert.AreEqual(BomberCharacter.LadyBomber, runtimeState.Character);
                Assert.AreEqual(BomberSkin.Palette7, runtimeState.Skin);
                Assert.AreEqual(3, runtimeState.BombAmount);
                Assert.AreEqual(4, runtimeState.ExplosionRadius);
                Assert.AreEqual(PlayerPersistentStats.BaseSpeedNormal, runtimeState.SpeedInternal);
            }
        }
        finally
        {
            Object.DestroyImmediate(player);
            copyState.Invoke(null, new object[] { savedBase, baseState });
            copyState.Invoke(null, new object[] { savedRuntime, runtimeState });
            NetSync.Mode = previousMode;
        }
    }

    [Test]
    public void FullRoomRejectsSeventhSlot()
    {
        Assert.AreEqual(0, BombermanNetworkManager.FindAvailableRoomSlot(new HashSet<int> { 1, 2, 3, 4, 5, 6 }));
    }

    [Test]
    public void FreedSlotIsReusedWithoutRenumberingRemainingPlayers()
    {
        var occupied = new HashSet<int> { 1, 3, 6 };
        Assert.AreEqual(2, BombermanNetworkManager.FindAvailableRoomSlot(occupied));
        CollectionAssert.AreEquivalent(new[] { 1, 3, 6 }, occupied);
    }

    [Test]
    public void RoomConfigurationDoesNotOverwriteOfflineSettings()
    {
        int offlineWins = SaveSystem.GetBattleModeBattlesToWin();
        SaveSystem.BeginOnlineBattleSettings();
        SaveSystem.SetBattleModeBattlesToWin(offlineWins == 3 ? 5 : 3);
        Assert.AreNotEqual(offlineWins, SaveSystem.GetBattleModeBattlesToWin());
        SaveSystem.EndOnlineBattleSettings();
        Assert.AreEqual(offlineWins, SaveSystem.GetBattleModeBattlesToWin());
    }

    [Test]
    public void RoomSnapshotContainsMatchSettingsButNoLocalSaveOrControls()
    {
        SaveSystem.BeginOnlineBattleSettings();
        SaveSystem.SetBattleModeStageIndex(3);
        string snapshot = SaveSystem.CaptureOnlineBattleSettings();
        StringAssert.Contains("battleModeStageIndex", snapshot);
        StringAssert.DoesNotContain("unlockedSkins", snapshot);
        StringAssert.DoesNotContain("controls", snapshot);
        StringAssert.DoesNotContain("slots", snapshot);
        SaveSystem.EndOnlineBattleSettings();
        SaveSystem.ApplyOnlineBattleSettings(snapshot);
        Assert.AreEqual(3, SaveSystem.GetBattleModeStageIndex());
    }

    [UnityTest]
    public System.Collections.IEnumerator ClientDisplaysEveryHostConfigurationPanel()
    {
        yield return new EnterPlayMode();
        yield return SceneManager.LoadSceneAsync("BattleModeMenu");
        yield return null;
        var menu = Object.FindAnyObjectByType<BattleModeMenu>(FindObjectsInactive.Include);
        Assert.IsNotNull(menu);
        menu.StopAllCoroutines();
        menu.gameObject.SetActive(true);
        var view = JsonUtility.FromJson<BattleModeMenu.OnlineView>(menu.CaptureOnlineView());
        view.items = GameManager.GetDefaultBattleModeHiddenItemAmounts();
        view.louies = GameManager.GetDefaultBattleModeLouieAmounts();
        view.handicap = SaveSystem.GetBattleModeHandicapForStage(0);
        view.teams = SaveSystem.GetBattleModePlayerTeams();
        view.selectedPlayer = 2;
        view.ruleIndex = 1;
        var panels = new Dictionary<string, string> {
            { "MatchMode", "leftPanel" }, { "PlayerSelect", "leftPanel" },
            { "TeamSelect", "teamSelectRoot" }, { "RuleConfig", "ruleConfigRoot" },
            { "StageSelect", "stageSelectRoot" }, { "SpecificSettings", "specificSettingsRoot" },
            { "MusicSelect", "musicSelectRoot" }, { "ItemSelect", "itemSelectRoot" },
            { "LouieSelect", "louieSelectRoot" }, { "HandicapSelect", "handicapSelectRoot" }
        };
        // Received snapshots are immutable by reference; each changed message is a new object.
        foreach (var page in panels)
        {
            view.step = page.Key;
            menu.ApplyOnlineMenuView(JsonUtility.FromJson<BattleModeMenu.OnlineView>(JsonUtility.ToJson(view)));
            var panel = (Component)typeof(BattleModeMenu).GetField(page.Value,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(menu);
            Assert.IsTrue(panel.gameObject.activeSelf, page.Key);
            var applied = JsonUtility.FromJson<BattleModeMenu.OnlineView>(menu.CaptureOnlineView());
            Assert.AreEqual(page.Key, applied.step);
            Assert.AreEqual(2, applied.selectedPlayer);
            Assert.AreEqual(1, applied.ruleIndex);
        }
        view.step = "SkinSelect";
        view.characters = true;
        menu.ApplyOnlineMenuView(JsonUtility.FromJson<BattleModeMenu.OnlineView>(JsonUtility.ToJson(view)));
        foreach (var fieldName in panels.Values)
        {
            var panel = (Component)typeof(BattleModeMenu).GetField(fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(menu);
            Assert.IsFalse(panel.gameObject.activeSelf, fieldName + " remained visible during Character Select");
        }
        view.step = "SpecificSettings";
        view.startConfirmed = true;
        view.fadeVisible = true;
        view.fadeAlpha = 0.6f;
        view.startCursorPosition = new Vector2(20f, 30f);
        menu.ApplyOnlineMenuView(JsonUtility.FromJson<BattleModeMenu.OnlineView>(JsonUtility.ToJson(view)));
        var transition = JsonUtility.FromJson<BattleModeMenu.OnlineView>(menu.CaptureOnlineView());
        Assert.IsTrue(transition.startConfirmed);
        Assert.IsTrue(transition.fadeVisible);
        Assert.AreEqual(0.6f, transition.fadeAlpha, 0.001f);
        Assert.AreEqual(view.startCursorPosition, transition.startCursorPosition);
        yield return SceneManager.LoadSceneAsync("TitleScreen");
        yield return new ExitPlayMode();
    }

    [Test]
    public void RoomSnapshotAppliesEachPlayersSkinToRuntime()
    {
        SaveSystem.BeginOnlineBattleSettings();
        var settings = SaveSystem.BattleConfigurationData;
        settings.player1SelectedCharacter = (int)BomberCharacter.Bomberman;
        settings.player2SelectedCharacter = (int)BomberCharacter.Bomberman;
        settings.player1SelectedSkin = (int)BomberSkin.Palette2;
        settings.player2SelectedSkin = (int)BomberSkin.Palette1;
        SaveSystem.ApplyOnlineBattleSettings(SaveSystem.CaptureOnlineBattleSettings());
        Assert.AreEqual(BomberSkin.Palette2, PlayerPersistentStats.GetRuntime(1).Skin);
        Assert.AreEqual(BomberSkin.Palette1, PlayerPersistentStats.GetRuntime(2).Skin);
    }

    [UnityTest]
    public System.Collections.IEnumerator ClientPauseFollowsHostAndRejectsLocalToggle()
    {
        yield return new EnterPlayMode();
        yield return SceneManager.LoadSceneAsync("TitleScreen");
        yield return null;
        var controller = GamePauseController.Instance;
        Assert.IsNotNull(controller);
        NetSync.Mode = NetSync.NetMode.Client;
        try
        {
            var state = controller.CaptureOnlinePause();
            state.paused = true;
            state.selection = 2;
            controller.ApplyOnlinePause(state);
            Assert.AreEqual(0f, Time.timeScale);
            Assert.AreEqual(2, controller.CaptureOnlinePause().selection);
            typeof(GamePauseController).GetMethod("TogglePause",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(controller, null);
            Assert.IsTrue(GamePauseController.IsPaused);
            state.paused = false;
            controller.ApplyOnlinePause(state);
            Assert.IsFalse(GamePauseController.IsPaused);
            Assert.AreEqual(1f, Time.timeScale);
        }
        finally
        {
            NetSync.Reset();
            GamePauseController.ForceUnpause();
        }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public System.Collections.IEnumerator LobbyUsesDedicatedDirectoryWithoutStartingLocalServer()
    {
        yield return new EnterPlayMode();
        yield return SceneManager.LoadSceneAsync(NetworkLobby.SceneName);
        yield return null;
        var manager = Object.FindAnyObjectByType<BombermanNetworkManager>();
        Assert.IsNotNull(manager.GetComponent<OnlineRoomBrowser>());
        Assert.IsNotNull(GameObject.Find("OnlineRoomCanvas/SafeFrame/RoomPanel/RoomBrowser"));
        Assert.IsNull(GameObject.Find("OnlineRoomCanvas/SafeFrame/RoomPanel/HostAddress"));
        Assert.IsFalse(NetworkServer.active);
        BombermanNetworkManager.LeaveRoom();
        yield return null;
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public System.Collections.IEnumerator LobbyCanHostAndReturnToTitle()
    {
        yield return new EnterPlayMode();
        yield return SceneManager.LoadSceneAsync(NetworkLobby.SceneName);
        yield return null;
        var manager = Object.FindAnyObjectByType<BombermanNetworkManager>();
        Assert.IsNotNull(manager);
        Assert.IsNotNull(manager.GetComponent<OnlineLobbyMenu>());
        Assert.AreEqual(1, Object.FindObjectsByType<AudioListener>().Length);
        Assert.AreEqual(1, Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>().Length);
        if (manager.transport is kcp2k.KcpTransport transport) transport.Port = 0;
        manager.StartHost();
        float timeout = Time.realtimeSinceStartup + 3f;
        while (BombermanNetworkManager.LocalRoomPlayerId == 0 && Time.realtimeSinceStartup < timeout) yield return null;
        Assert.IsTrue(NetworkServer.active);
        Assert.AreEqual(1, BombermanNetworkManager.LocalRoomPlayerId,
            $"playing={Application.isPlaying}, client={NetworkClient.isConnected}, ready={NetworkClient.ready}, connections={NetworkServer.connections.Count}, room={string.Join(",", BombermanNetworkManager.RoomPlayers)}");
        CollectionAssert.AreEqual(new[] { 1 }, BombermanNetworkManager.RoomPlayers);
        Assert.IsTrue(SaveSystem.HasOnlineBattleSettings);
        // Mirror releases lobby input before the asynchronous scene load starts.
        // Updating the still-active lobby must not recreate that EventSystem.
        manager.GetComponent<OnlineLobbyMenu>().ReleaseRoomEventSystem();
        yield return null;
        yield return null;
        Assert.AreEqual(0, Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>().Length);
        manager.ServerChangeScene("BattleModeMenu");
        timeout = Time.realtimeSinceStartup + 8f;
        while (NetworkServer.isLoadingScene && Time.realtimeSinceStartup < timeout) yield return null;
        yield return null;
        Assert.AreEqual("BattleModeMenu", SceneManager.GetActiveScene().name);
        Assert.AreEqual(1, Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>().Length);
        Assert.AreEqual(1, Object.FindObjectsByType<AudioListener>().Length);
        BombermanNetworkManager.ServerReturnToLobby();
        timeout = Time.realtimeSinceStartup + 8f;
        while (NetworkServer.isLoadingScene && Time.realtimeSinceStartup < timeout) yield return null;
        yield return null;
        Assert.AreEqual(NetworkLobby.SceneName, SceneManager.GetActiveScene().name);
        Assert.AreEqual(1, Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>().Length);
        Assert.AreEqual(1, Object.FindObjectsByType<AudioListener>().Length);
        BombermanNetworkManager.LeaveRoom();
        timeout = Time.realtimeSinceStartup + 10f;
        while (SceneManager.GetActiveScene().name != "TitleScreen" && Time.realtimeSinceStartup < timeout)
            yield return null;
        Assert.AreEqual("TitleScreen", SceneManager.GetActiveScene().name);
        Assert.IsFalse(SaveSystem.HasOnlineBattleSettings);
        Assert.IsFalse(NetworkServer.active);
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public System.Collections.IEnumerator EarlyDeathIsEvaluatedWithoutAnotherDeathAfterRoundReload()
    {
        yield return new EnterPlayMode();
        yield return SceneManager.LoadSceneAsync(NetworkLobby.SceneName);
        yield return null;
        var manager = Object.FindAnyObjectByType<BombermanNetworkManager>();
        if (manager.transport is kcp2k.KcpTransport transport) transport.Port = 0;
        manager.StartHost();
        float deadline = Time.realtimeSinceStartup + 3f;
        while (BombermanNetworkManager.LocalRoomPlayerId == 0 && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.AreEqual(1, BombermanNetworkManager.LocalRoomPlayerId);
        SaveSystem.SetBattleModeMatchMode(BattleModeRules.MatchMode.SingleMatch);
        SaveSystem.SetBattleModeBattlesToWin(5);
        SaveSystem.SetBattleModePlayerControlMode(2, BattleModePlayerControlMode.Com);
        BombermanNetworkManager.ServerStartMatch("BattleMode_1");
        GameManager previousManager = null;
        for (int round = 1; round <= 2; round++)
        {
            deadline = Time.realtimeSinceStartup + 8f;
            while ((GameManager.Instance == null || GameManager.Instance == previousManager ||
                    SceneManager.GetActiveScene().name != "BattleMode_1" || !BombermanNetworkManager.OnlineRoundPlayersReady || BombermanNetworkManager.BattleStartPending) &&
                   Time.realtimeSinceStartup < deadline) yield return null;
            var game = GameManager.Instance;
            Assert.IsNotNull(game);
            Assert.AreNotSame(previousManager, game);
            Assert.IsTrue(BombermanNetworkManager.OnlineRoundPlayersReady);
            yield return null;
            // A única morte ocorre antes de a janela de proteção terminar.
            typeof(GameManager).GetField("onlineBattleWinEvalReadyTime",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(game, Time.realtimeSinceStartup + 1.2f);
            MovementController loser = null;
            foreach (var player in Object.FindObjectsByType<MovementController>())
                if (player.CompareTag("Player") && player.PlayerId == 2) loser = player;
            Assert.IsNotNull(loser);
            loser.isDead = true;
            game.NotifyPlayerDeathStarted(loser);
            yield return new WaitForSecondsRealtime(0.7f);
            Assert.IsFalse(game.IsBattleRoundResolutionTriggered, "The spawn grace must still protect the round.");
            deadline = Time.realtimeSinceStartup + 3f;
            while (!game.IsBattleRoundResolutionTriggered && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(game.IsBattleRoundResolutionTriggered, "No second death should be needed to resolve the round.");
            Assert.AreEqual(round, GameSession.Instance.GetBattleMatchWins(1));
            previousManager = game;
            if (round == 1) BombermanNetworkManager.ServerStartNextRound();
        }
        BombermanNetworkManager.LeaveRoom();
        yield return null;
        yield return new ExitPlayMode();
    }
}
#endif

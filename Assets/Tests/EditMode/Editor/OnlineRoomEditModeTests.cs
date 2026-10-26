#if UNITY_EDITOR
using System.Collections.Generic;
using Assets.Scripts.Netcode;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Mirror;

public sealed class OnlineRoomEditModeTests
{
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
            var ready = new NetworkBombFx.MountActionState { tankReady = true };
            fx.ReconcileTankShot(0, ready); Assert.IsTrue(fx.TryPredictTankShot(8, 10));
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
            fx.ReconcileTankShot(10, ready); Assert.IsNull(typeof(NetworkBombFx).GetField("tankProxy", flags).GetValue(fx));
            Assert.IsFalse(fx.TankShotPoseActive); Assert.IsTrue(fx.TryPredictTankShot(8, 11));
            update.Invoke(fx, new object[] { 0.1f }); float before = (float)typeof(NetworkBombFx).GetField("tankAge", flags).GetValue(fx);
            fx.ReceiveTankShot(11, 999, Vector2.right, 0.25f, 10, NetworkBombFx.KickServerNow);
            Assert.AreEqual(before, typeof(NetworkBombFx).GetField("tankAge", flags).GetValue(fx));
            fx.ReconcileTankShot(11, new NetworkBombFx.MountActionState { tankCooldown = 10 });
            Assert.IsFalse(fx.TryPredictTankShot(8, 12)); update.Invoke(fx, new object[] { 1.1f });
            Assert.IsNull(typeof(NetworkBombFx).GetField("tankProxy", flags).GetValue(fx), "A missing spawn cannot leave a confirmed proxy forever.");
            actor.GetComponent<CharacterHealth>().life = 0;
            fx.ReceiveTankShot(12, 999, Vector2.right, 0.25f, 10, NetworkBombFx.KickServerNow);
            Assert.IsFalse(fx.TankShotPoseActive, "A late receipt cannot resurrect the dead owner's firing pose.");
            NetSync.Mode = NetSync.NetMode.Host; Assert.IsFalse(fx.TryPredictTankShot(8, 13));
            using var writer = NetworkWriterPool.Get(); writer.Write(ready);
            using var reader = NetworkReaderPool.Get(writer.ToArraySegment()); Assert.IsTrue(reader.Read<NetworkBombFx.MountActionState>().tankReady);
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
            direction = Vector2.left, value = 1.25f, steps = 3, cell = path[0], path = path });
        using var reader = NetworkReaderPool.Get(writer.ToArraySegment());
        var result = reader.Read<BattleFeedback>();
        Assert.AreEqual("BattleMode_1", result.scene);
        Assert.AreEqual(42u, result.netId);
        Assert.AreEqual(6, result.kind);
        Assert.AreEqual(Vector2.left, result.direction);
        Assert.AreEqual(1.25f, result.value);
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
            var proxy = (GameObject)proxies[1u].GetType().GetField("visual").GetValue(proxies[1u]);
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

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
    [TestCase("ABCDEFGHIJKLM", false)]
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

    [TestCase("0.5.0", 12, true)]
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
        foreach (var page in panels)
        {
            view.step = page.Key;
            menu.ApplyOnlineMenuView(view);
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
        menu.ApplyOnlineMenuView(view);
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
        menu.ApplyOnlineMenuView(view);
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
                    SceneManager.GetActiveScene().name != "BattleMode_1" || !BombermanNetworkManager.OnlineRoundPlayersReady) &&
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

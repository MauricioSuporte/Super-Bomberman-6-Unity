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
    public System.Collections.IEnumerator LobbyAutomaticallyBrowsesAndAdvertisesLanRooms()
    {
        yield return new EnterPlayMode();
        yield return SceneManager.LoadSceneAsync(NetworkLobby.SceneName);
        yield return null;
        var manager = Object.FindAnyObjectByType<BombermanNetworkManager>();
        var browser = manager.GetComponent<OnlineRoomBrowser>();
        var discovery = manager.GetComponent<OnlineRoomDiscovery>();
        Assert.IsNotNull(browser);
        Assert.IsNotNull(discovery);
        Assert.IsNotNull(GameObject.Find("OnlineRoomCanvas/SafeFrame/RoomPanel/RoomBrowser"));
        Assert.IsFalse(browser.PublishPublic);
        if (manager.transport is kcp2k.KcpTransport transport) transport.Port = 0;
        manager.StartHost();
        yield return null;
        using (var socket = new System.Net.Sockets.UdpClient(0))
        {
            byte[] packet;
            using (var writer = NetworkWriterPool.Get())
            {
                writer.WriteLong(discovery.secretHandshake);
                writer.Write(new OnlineDiscoveryRequest());
                packet = writer.ToArray();
            }
            socket.Send(packet, packet.Length, new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 47776));
            var responseTask = socket.ReceiveAsync();
            float deadline = Time.realtimeSinceStartup + 3f;
            while (!responseTask.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(responseTask.IsCompleted, "LAN host must respond to discovery without manually entering its gameplay IP.");
            using (var reader = NetworkReaderPool.Get(responseTask.Result.Buffer))
            {
                Assert.AreEqual(discovery.secretHandshake, reader.ReadLong());
                var room = reader.Read<OnlineDiscoveryResponse>();
                Assert.IsTrue(room.available);
                Assert.AreEqual(1, room.players);
                Assert.AreEqual("kcp", room.uri.Scheme);
            }
        }
        Assert.IsTrue(OnlineRoomDiscovery.AcceptingPlayers);
        BombermanNetworkManager.ServerStartMatch("BattleMode_1");
        Assert.IsFalse(OnlineRoomDiscovery.AcceptingPlayers);
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

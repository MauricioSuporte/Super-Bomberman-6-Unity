#if UNITY_EDITOR
using Assets.Scripts.Netcode;
using NUnit.Framework;
using UnityEngine;
using Mirror;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class OnlineChatEditModeTests
{
    string savedChatName;
    bool restoreChatName;
    Keyboard testKeyboard;
    InputSettings.UpdateMode savedInputUpdateMode;
    bool restoreInputUpdateMode;

    static void SetChatKeys(OnlineChatOverlay chat, Keyboard keyboard, params Key[] keys)
    {
        // Register edge tracking before injecting the first state of a new device.
        _ = keyboard.tKey.wasPressedThisFrame;
        _ = keyboard.enterKey.wasPressedThisFrame;
        _ = keyboard.numpadEnterKey.wasPressedThisFrame;
        _ = keyboard.escapeKey.wasPressedThisFrame;
        _ = keyboard.zKey.wasPressedThisFrame;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        InputSystem.Update();
        typeof(OnlineChatOverlay).GetMethod("Update", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(chat, null);
    }

    [UnityTearDown]
    public System.Collections.IEnumerator RestoreChatSession()
    {
        if (testKeyboard != null && testKeyboard.added) InputSystem.RemoveDevice(testKeyboard);
        testKeyboard = null;
        if (restoreInputUpdateMode) InputSystem.settings.updateMode = savedInputUpdateMode;
        restoreInputUpdateMode = false;
        if (restoreChatName) OnlineVersionAuthenticator.PlayerName = savedChatName;
        restoreChatName = false;
        if (Application.isPlaying)
        {
            if (NetworkManager.singleton != null && NetworkServer.active) NetworkManager.singleton.StopHost();
            yield return new ExitPlayMode();
        }
    }

    [UnityTest]
    public System.Collections.IEnumerator RoomChatUsesAuthenticatedNameAndOwnsKeyboardAcrossSceneChanges()
    {
        yield return new EnterPlayMode();
        savedChatName = OnlineVersionAuthenticator.PlayerName;
        restoreChatName = true;
        OnlineVersionAuthenticator.PlayerName = "CHAT_TEST";
        yield return SceneManager.LoadSceneAsync(NetworkLobby.SceneName);
        yield return null;
        var manager = Object.FindAnyObjectByType<BombermanNetworkManager>();
        Assert.IsNotNull(manager);
        if (manager.transport is kcp2k.KcpTransport transport) transport.Port = 0;
        manager.StartHost();
        float deadline = Time.realtimeSinceStartup + 5;
        while (BombermanNetworkManager.LocalRoomPlayerId == 0 && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.Greater(BombermanNetworkManager.LocalRoomPlayerId, 0);
        var chat = manager.GetComponent<OnlineChatOverlay>();
        Assert.IsNotNull(chat);
        var labels = chat.GetComponentsInChildren<TextMeshProUGUI>(true);
        var history = System.Array.Find(labels, label => label.name == "History");
        Assert.IsNotNull(history);
        NetworkClient.Send(new OnlineChatSendMessage { text = "olá <b>teste</b>" });
        deadline = Time.realtimeSinceStartup + 3;
        while (!(history.text ?? "").Contains("olá") && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.AreEqual("CHAT_TEST: olá <b>teste</b>", history.text);
        Assert.IsFalse(history.richText);
        NetworkClient.Send(new OnlineChatSendMessage { text = "flood" });
        yield return null; yield return null;
        Assert.IsFalse(history.text.Contains("flood"));

        savedInputUpdateMode = InputSystem.settings.updateMode;
        restoreInputUpdateMode = true;
        InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
        var keyboard = testKeyboard = InputSystem.AddDevice<Keyboard>();
        SetChatKeys(chat, keyboard, Key.T);
        yield return null; yield return null;
        Assert.IsTrue(OnlineChatOverlay.BlocksLocalInput, $"T must capture local gameplay/menu input. focused={Application.isFocused}, batch={Application.isBatchMode}, key={keyboard.tKey.isPressed}, enabled={keyboard.enabled}, modal={manager.GetComponent<OnlineLobbyMenu>().ChatModalOpen}");
        var input = PlayerInputManager.Instance;
        Assert.IsNotNull(input);
        input.SetSyntheticHeld(6, PlayerAction.MoveRight, true);
        Assert.IsTrue(input.Get(6, PlayerAction.MoveRight), "Host COM input must continue during local chat.");
        input.ClearSyntheticPlayer(6);

        SetChatKeys(chat, keyboard);
        yield return null;
        InputSystem.QueueTextEvent(keyboard, 'o'); InputSystem.QueueTextEvent(keyboard, 'i'); InputSystem.Update();
        yield return null; yield return null;
        var entry = System.Array.Find(labels, label => label.name == "Entry");
        Assert.IsTrue(entry.text.Contains("oi"));
        if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
        {
            System.IO.Directory.CreateDirectory("build/Validation");
            ScreenCapture.CaptureScreenshot("build/Validation/online-chat-lobby.png");
            yield return null; yield return null;
        }
        var safeFrame = PixelPerfectViewport.ResolveFinalPixelRect(Camera.main);
        var corners = new Vector3[4];
        history.transform.parent.GetComponent<RectTransform>().GetWorldCorners(corners);
        Assert.GreaterOrEqual(corners[0].x, safeFrame.xMin);
        Assert.GreaterOrEqual(corners[0].y, safeFrame.yMin);
        Assert.LessOrEqual(corners[2].x, safeFrame.xMax);
        Assert.LessOrEqual(corners[2].y, safeFrame.yMax);
        yield return new WaitForSecondsRealtime(OnlineChatRules.SendInterval);
        SetChatKeys(chat, keyboard, Key.Enter);
        yield return null; yield return null;
        Assert.IsFalse(OnlineChatOverlay.BlocksLocalInput, "Enter must send and close chat.");
        deadline = Time.realtimeSinceStartup + 3;
        while (!history.text.Contains("CHAT_TEST: oi") && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.IsTrue(history.text.Contains("CHAT_TEST: oi"));
        SetChatKeys(chat, keyboard);
        yield return null;
        SetChatKeys(chat, keyboard, Key.T);
        yield return null; yield return null;
        Assert.IsTrue(OnlineChatOverlay.BlocksLocalInput);
        SetChatKeys(chat, keyboard, Key.Escape);
        yield return null; yield return null;
        Assert.IsFalse(OnlineChatOverlay.BlocksLocalInput);
        Assert.IsFalse(GamePauseController.IsPaused, "Esc used to close chat must not pause.");
        SetChatKeys(chat, keyboard, Key.Z);
        yield return null; yield return null;
        Assert.IsTrue(OnlineChatOverlay.ReservesHistoryKey);
        Assert.IsTrue(history.gameObject.activeInHierarchy);
        SetChatKeys(chat, keyboard);
        yield return null;
        manager.ServerChangeScene("BattleModeMenu");
        deadline = Time.realtimeSinceStartup + 10;
        while (SceneManager.GetActiveScene().name != "BattleModeMenu" && Time.realtimeSinceStartup < deadline) yield return null;
        yield return null;
        Assert.AreEqual("BattleModeMenu", SceneManager.GetActiveScene().name);
        Assert.AreSame(chat, manager.GetComponent<OnlineChatOverlay>());
        Assert.AreEqual(1, Object.FindObjectsByType<OnlineChatOverlay>().Length);
        SetChatKeys(chat, keyboard, Key.Z);
        yield return null; yield return null;
        Assert.IsTrue(history.text.Contains("olá"), "Room history must survive scene transitions.");
        InputSystem.RemoveDevice(keyboard);
        manager.StopHost();
        OnlineVersionAuthenticator.PlayerName = savedChatName;
        restoreChatName = false;
        yield return null;
        Assert.IsFalse(OnlineChatOverlay.BlocksLocalInput);
        yield return new ExitPlayMode();
    }

    [TestCase(null, "")]
    [TestCase("  olá 日本語  ", "olá 日本語")]
    [TestCase("a\r\nb\t c\u202Ed", "a  b  cd")]
    [TestCase("<b>texto</b>", "<b>texto</b>")]
    public void ChatNormalizesOnePlainUnicodeLine(string input, string expected)
        => Assert.AreEqual(expected, OnlineChatRules.Normalize(input));

    [Test]
    public void ChatLimitDoesNotSplitUnicodePairsAndRejectsOversizedPayloads()
    {
        Assert.AreEqual("xyz", OnlineChatRules.Normalize("x" + (char)0xD800 + "y" + (char)0xDC00 + "z"));
        string prefix = new('a', OnlineChatRules.MaxLength - 1);
        Assert.AreEqual(prefix, OnlineChatRules.Normalize(prefix + "\U0001F600"));
        Assert.AreEqual(OnlineChatRules.MaxLength, OnlineChatRules.Normalize(new string('a', 200)).Length);
        Assert.IsEmpty(OnlineChatRules.Normalize(new string('a', 513)));
    }

    [Test]
    public void ChatGateReleasesOnDisableAndDoesNotBlockOfflineInputs()
    {
        var actor = new GameObject("ChatInputGate");
        try
        {
            var chat = actor.AddComponent<OnlineChatOverlay>();
            typeof(OnlineChatOverlay).GetMethod("OnEnable", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(chat, null);
            var typing = typeof(OnlineChatOverlay).GetField("typing", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsFalse(OnlineChatOverlay.BlocksLocalInput);
            typing.SetValue(chat, true);
            Assert.IsTrue(OnlineChatOverlay.BlocksLocalInput);
            chat.enabled = false;
            typeof(OnlineChatOverlay).GetMethod("OnDisable", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(chat, null);
            Assert.IsFalse(OnlineChatOverlay.BlocksLocalInput);
            Assert.IsFalse(OnlineChatOverlay.ReservesHistoryKey);
        }
        finally { Object.DestroyImmediate(actor); }
    }
}
#endif

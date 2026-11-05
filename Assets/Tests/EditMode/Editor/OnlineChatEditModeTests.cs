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

    [OneTimeSetUp]
    public void SynchronizePresenceAudioCatalogs() => CoopSceneCatalogBuilder.Generate();

    static int OwnJoinCount(OnlineChatOverlay chat)
    {
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var lines = (System.Collections.IEnumerable)typeof(OnlineChatOverlay).GetField("roomLines", flags).GetValue(chat);
        int count = 0;
        foreach (var line in lines)
            if ((byte)line.GetType().GetField("presence").GetValue(line) == 1 && (bool)line.GetType().GetField("you").GetValue(line)) count++;
        return count;
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
        NetworkClient.Send(new OnlineChatSendMessage { text = "olá <b>teste</b>", nonce = System.Guid.NewGuid().ToString("N") });
        deadline = Time.realtimeSinceStartup + 3;
        while (!(history.text ?? "").Contains("olá") && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.IsTrue(history.text.Contains(OnlineChatRules.FormatLine("CHAT_TEST", "olá <b>teste</b>", 1)));
        Assert.IsTrue(history.text.Contains(GameTextDatabase.Online.ChatPresence("", true, true)));
        Assert.AreEqual(1, OwnJoinCount(chat));
        Assert.IsNotNull(Resources.Load<AudioClip>("Sounds/OnlineChat/player_join"));
        Assert.IsNotNull(Resources.Load<AudioClip>("Sounds/OnlineChat/player_leave"));
        Assert.IsTrue(history.richText);
        Assert.AreEqual(FontStyles.Bold, history.fontStyle);
        Assert.IsNull(history.transform.parent.GetComponent<UnityEngine.UI.Image>());
        for (int i = 0; i < 8; i++)
            NetworkClient.Send(new OnlineChatSendMessage { text = "flood" + i, nonce = System.Guid.NewGuid().ToString("N") });
        yield return null; yield return null;
        Assert.IsFalse(history.text.Contains("flood7"));

        savedInputUpdateMode = InputSystem.settings.updateMode;
        restoreInputUpdateMode = true;
        InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
        var keyboard = testKeyboard = InputSystem.AddDevice<Keyboard>();
        SetChatKeys(chat, keyboard);
        yield return null;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.T));
        InputSystem.QueueTextEvent(keyboard, 't');
        InputSystem.QueueTextEvent(keyboard, 'o');
        InputSystem.QueueTextEvent(keyboard, 'i');
        InputSystem.Update();
        Assert.IsTrue(System.Array.Find(labels, label => label.name == "Entry").text.Contains("oi"), "Text must appear in the opening input update.");
        yield return null; yield return null;
        Assert.IsTrue(OnlineChatOverlay.BlocksLocalInput, $"T must capture local gameplay/menu input. focused={Application.isFocused}, batch={Application.isBatchMode}, key={keyboard.tKey.isPressed}, enabled={keyboard.enabled}, modal={manager.GetComponent<OnlineLobbyMenu>().ChatModalOpen}");
        var input = PlayerInputManager.Instance;
        Assert.IsNotNull(input);
        input.SetSyntheticHeld(6, PlayerAction.MoveRight, true);
        Assert.IsTrue(input.Get(6, PlayerAction.MoveRight), "Host COM input must continue during local chat.");
        input.ClearSyntheticPlayer(6);

        SetChatKeys(chat, keyboard);
        yield return null;
        yield return null; yield return null;
        var entry = System.Array.Find(labels, label => label.name == "Entry");
        Assert.IsTrue(entry.text.Contains("oi"));
        Assert.AreEqual(FontStyles.Bold, entry.fontStyle);
        Assert.IsNull(System.Array.Find(labels, label => label.name == "Hint"));
        var bands = history.transform.parent.Find("TextBackgrounds").GetComponentsInChildren<UnityEngine.UI.Image>();
        Assert.Greater(bands.Length, 0);
        foreach (var band in bands)
        {
            Assert.AreEqual(0, band.color.r); Assert.AreEqual(0, band.color.g); Assert.AreEqual(0, band.color.b);
            Assert.Greater(band.color.a, 0); Assert.Less(band.color.a, 1);
            Assert.LessOrEqual(band.rectTransform.rect.width, 202);
        }
        Assert.IsFalse(input.Get(1, PlayerAction.Start));
        Assert.IsFalse(input.GetDown(1, PlayerAction.ActionA));
        var safeFrame = PixelPerfectViewport.ResolveFinalPixelRect(Camera.main);
        var corners = new Vector3[4];
        history.transform.parent.GetComponent<RectTransform>().GetWorldCorners(corners);
        Assert.GreaterOrEqual(corners[0].x, safeFrame.xMin);
        Assert.GreaterOrEqual(corners[0].y, safeFrame.yMin);
        Assert.LessOrEqual(corners[2].x, safeFrame.xMax);
        Assert.LessOrEqual(corners[2].y, safeFrame.yMax);
        yield return new WaitForSecondsRealtime(OnlineChatRules.SendInterval);
        SetChatKeys(chat, keyboard, Key.Enter);
        Assert.IsTrue(OnlineChatOverlay.BlocksLocalInput, "Keep Enter blocked in the closing input update.");
        yield return null; yield return null;
        SetChatKeys(chat, keyboard);
        yield return null; yield return null;
        Assert.IsFalse(OnlineChatOverlay.BlocksLocalInput, $"Enter must send and close chat: typing={typeof(OnlineChatOverlay).GetField("typing", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(chat)}, release={typeof(OnlineChatOverlay).GetField("releaseKeys", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(chat)}, frame={Time.frameCount}");
        deadline = Time.realtimeSinceStartup + 3;
        while (!history.text.Contains(OnlineChatRules.FormatLine("CHAT_TEST", "oi", 1)) && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.IsTrue(history.text.Contains(OnlineChatRules.FormatLine("CHAT_TEST", "oi", 1)));
        SetChatKeys(chat, keyboard);
        yield return null;
        SetChatKeys(chat, keyboard, Key.T);
        yield return null; yield return null;
        Assert.IsTrue(OnlineChatOverlay.BlocksLocalInput);
        SetChatKeys(chat, keyboard, Key.Escape);
        yield return null; yield return null;
        SetChatKeys(chat, keyboard);
        yield return null; yield return null;
        Assert.IsFalse(OnlineChatOverlay.BlocksLocalInput);
        Assert.IsFalse(GamePauseController.IsPaused, "Esc used to close chat must not pause.");
        SetChatKeys(chat, keyboard, Key.Z);
        yield return null; yield return null;
        Assert.IsFalse(OnlineChatOverlay.BlocksLocalInput, "Z must remain a gameplay key.");
        SetChatKeys(chat, keyboard);
        yield return null;
        manager.ServerChangeScene("BattleModeMenu");
        deadline = Time.realtimeSinceStartup + 10;
        while (SceneManager.GetActiveScene().name != "BattleModeMenu" && Time.realtimeSinceStartup < deadline) yield return null;
        yield return null;
        Assert.AreEqual("BattleModeMenu", SceneManager.GetActiveScene().name);
        Assert.AreSame(chat, manager.GetComponent<OnlineChatOverlay>());
        Assert.AreEqual(1, Object.FindObjectsByType<OnlineChatOverlay>().Length);
        Assert.AreEqual(1, OwnJoinCount(chat), "Changing scenes must not announce the same player again.");
        SetChatKeys(chat, keyboard, Key.T);
        yield return null; yield return null;
        Assert.IsTrue(history.text.Contains("olá"), "Room history must survive scene transitions.");
        if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
        {
            chat.ReceivePresence("VISITANTE", 2, true, false);
            chat.ReceivePresence("VISITANTE", 2, false, false);
            var names = new[] { "BRANCO", "PRETO", "VERMELHO", "AZUL", "VERDE", "AMARELO" };
            for (int id = 1; id <= 6; id++) chat.ReceiveRoomLine("P" + id, names[id - 1], System.Guid.NewGuid().ToString("N"), id);
            yield return null;
            CaptureChatPreview(chat);
        }
        InputSystem.RemoveDevice(keyboard);
        manager.StopHost();
        yield return null; yield return null;
        var departure = Object.FindObjectsByType<OnlineChatOverlay>();
        Assert.IsTrue(System.Array.Exists(departure, overlay =>
        {
            var label = System.Array.Find(overlay.GetComponentsInChildren<TextMeshProUGUI>(true), text => text.name == "History");
            return label != null && label.text.Contains(GameTextDatabase.Online.ChatPresence("", false, true));
        }), "Own departure must remain visible after disconnecting.");
        OnlineVersionAuthenticator.PlayerName = savedChatName;
        restoreChatName = false;
        yield return null; yield return null;
        Assert.IsFalse(OnlineChatOverlay.BlocksLocalInput);
        yield return new ExitPlayMode();
    }

    static void CaptureChatPreview(OnlineChatOverlay chat)
    {
        var source = System.Array.Find(chat.GetComponentsInChildren<Canvas>(true), canvas => canvas.name == "OnlineChatCanvas");
        var clone = Object.Instantiate(source.gameObject);
        var cameraObject = new GameObject("ChatPreviewCamera", typeof(Camera));
        var target = new RenderTexture(1024, 896, 24);
        var previous = RenderTexture.active;
        Texture2D pixels = null;
        try
        {
            foreach (var fitter in clone.GetComponentsInChildren<UICameraViewportFitter>(true)) fitter.enabled = false;
            foreach (var transform in clone.GetComponentsInChildren<Transform>(true)) transform.gameObject.layer = 5;
            var canvas = clone.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var root = clone.GetComponent<RectTransform>();
            root.SetParent(null); root.position = Vector3.zero; root.rotation = Quaternion.identity; root.localScale = Vector3.one;
            root.sizeDelta = new Vector2(256, 224);
            var safe = (RectTransform)root.Find("SafeFrame");
            safe.anchorMin = Vector2.zero; safe.anchorMax = Vector2.one;
            safe.offsetMin = safe.offsetMax = Vector2.zero;
            var panel = (RectTransform)safe.Find("Chat");
            panel.localScale = Vector3.one; panel.anchoredPosition = new Vector2(4, 4); panel.gameObject.SetActive(true);
            var camera = cameraObject.GetComponent<Camera>();
            camera.enabled = false; camera.orthographic = true; camera.orthographicSize = 112;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.cullingMask = 1 << 5; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(64, 68, 76, 255);
            canvas.worldCamera = camera;
            target.Create(); camera.targetTexture = target;
            Canvas.ForceUpdateCanvases();
            var request = new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = target };
            UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, request);
            RenderTexture.active = target;
            pixels = new Texture2D(1024, 896, TextureFormat.RGBA32, false);
            pixels.ReadPixels(new Rect(0, 0, 1024, 896), 0, 0); pixels.Apply();
            string output = System.Environment.GetEnvironmentVariable("SB6_CHAT_PREVIEW") ?? System.IO.Path.GetFullPath("build/Validation/chat-polish-preview.png");
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(output));
            System.IO.File.WriteAllBytes(output, pixels.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            target.Release(); Object.DestroyImmediate(target);
            if (pixels != null) Object.DestroyImmediate(pixels);
            Object.DestroyImmediate(cameraObject); Object.DestroyImmediate(clone);
        }
    }

    [Test]
    public void BurstBudgetRefillsAndFormattingCannotExecuteUserTags()
    {
        var colors = new System.Collections.Generic.HashSet<string>();
        for (int id = 1; id <= 6; id++) Assert.IsTrue(colors.Add(OnlineChatRules.NameColor(id)), "Each room slot needs a distinct name color.");
        Assert.AreEqual(OnlineChatRules.NameColor(3), OnlineChatRules.NameColor(3, "DIFFERENT NAME"));
        CollectionAssert.AreEqual(new[] { "FFFFFF", "000000", "FF0000", "0000FF", "00FF00", "FFFF00" },
            new[] { OnlineChatRules.NameColor(1), OnlineChatRules.NameColor(2), OnlineChatRules.NameColor(3),
                OnlineChatRules.NameColor(4), OnlineChatRules.NameColor(5), OnlineChatRules.NameColor(6) });
        var budget = new OnlineChatSendBudget();
        for (int i = 0; i < 4; i++) Assert.IsTrue(budget.TryConsume(10));
        Assert.IsFalse(budget.TryConsume(10));
        Assert.IsTrue(budget.TryConsume(10.25f));
        var actor = new GameObject("ChatFormatting", typeof(RectTransform), typeof(Canvas));
        try
        {
            var text = actor.AddComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.richText = true;
            text.text = OnlineChatRules.FormatLine("ALICE", "<size=200>oi</size></noparse><b>x</b>");
            text.ForceMeshUpdate(true);
            Assert.AreEqual("ALICE: <size=200>oi</size></noparse><b>x</b>", text.GetParsedText());
        }
        finally { Object.DestroyImmediate(actor); }
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

        }
        finally { Object.DestroyImmediate(actor); }
    }
}
#endif

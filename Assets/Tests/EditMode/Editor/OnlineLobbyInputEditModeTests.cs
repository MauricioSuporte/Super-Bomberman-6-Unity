#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Reflection;
using Assets.Scripts.Netcode;
using Mirror;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class OnlineLobbyInputEditModeTests
{
    [Test]
    public void CreationModePickerSkipsLockedBossRushInBothDirections()
    {
        var method = typeof(OnlineLobbyMenu).GetMethod("NextCreationMode", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.AreEqual(OnlineRoomMode.NormalGame, method.Invoke(null, new object[] { OnlineRoomMode.BattleMode, -1, false }));
        Assert.AreEqual(OnlineRoomMode.BattleMode, method.Invoke(null, new object[] { OnlineRoomMode.NormalGame, 1, false }));
        Assert.AreEqual(OnlineRoomMode.BossRush, method.Invoke(null, new object[] { OnlineRoomMode.NormalGame, 1, true }));
    }

    const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    Keyboard keyboard;
    string savedName;
    InputSettings.UpdateMode savedMode;
    bool started;

    static object Field(object owner, string name) => owner.GetType().GetField(name, Private).GetValue(owner);
    static void Call(object owner, string name, params object[] args) => owner.GetType().GetMethod(name, Private).Invoke(owner, args);

    void PrepareLobby()
    {
        savedName = OnlineVersionAuthenticator.PlayerName;
        savedMode = InputSystem.settings.updateMode;
        started = true;
        if (PlayerInputManager.Instance == null) new GameObject("LobbyTestInput").AddComponent<PlayerInputManager>();
        OnlineVersionAuthenticator.PlayerName = "Mauricio";
        var browser = Object.FindAnyObjectByType<OnlineRoomBrowser>();
        browser.StopAllCoroutines();
        Call(browser, "ApplyDirectoryHealth", new OnlineDirectoryHealth
        { ok = true, protocol = OnlineVersionAuthenticator.ProtocolVersion, buildIdentity = OnlineVersionAuthenticator.ConnectionBuildIdentity, capacity = 3 });
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
        if (started)
        {
            OnlineVersionAuthenticator.PlayerName = savedName;
            InputSystem.settings.updateMode = savedMode;
        }
        if (Application.isPlaying) yield return new ExitPlayMode();
        started = false;
    }

    void Keys(params Key[] keys)
    {
        // Register edge tracking before manual input updates, as in the chat fixtures.
        _ = keyboard.enterKey.wasPressedThisFrame;
        _ = keyboard.numpadEnterKey.wasPressedThisFrame;
        _ = keyboard.escapeKey.wasPressedThisFrame;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        InputSystem.Update();
    }

    [UnityTest]
    public IEnumerator TextEntryIgnoresGameplayBindingsAndUsesEnterEscape()
    {
        yield return new EnterPlayMode();
        yield return SceneManager.LoadSceneAsync(NetworkLobby.SceneName);
        yield return null;
        yield return null;
        PrepareLobby();
        var menu = Object.FindAnyObjectByType<OnlineLobbyMenu>();
        keyboard = InputSystem.AddDevice<Keyboard>();
        InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
        var profile = PlayerInputManager.Instance.GetPlayer(1);
        var bindings = profile.CloneBindings();
        var changed = profile.CloneBindings();
        changed[PlayerAction.ActionB] = new Binding { kind = BindKind.Key, key = KeyCode.M };
        changed[PlayerAction.Start] = new Binding { kind = BindKind.Key, key = KeyCode.N };
        profile.ApplyBindings(changed);
        try
        {
            Call(menu, "BeginNameEdit", "A");
            Keys();
            InputSystem.QueueTextEvent(keyboard, 'm');
            Keys(Key.M);
            Call(menu, "UpdateName");
            Assert.IsTrue(((GameObject)Field(menu, "nameRoot")).activeSelf, "Typing the back binding must keep the editor open.");
            Assert.AreEqual("Am", Field(menu, "editedName"));
            Keys();
            InputSystem.QueueTextEvent(keyboard, 'n');
            Keys(Key.N);
            Call(menu, "UpdateName");
            Assert.IsTrue(((GameObject)Field(menu, "nameRoot")).activeSelf);
            Keys(); Keys(Key.Enter);
            Assert.IsTrue(keyboard.enterKey.isPressed, "Injected Enter must be visible to Input System.");
            Call(menu, "UpdateName");
            Assert.IsFalse(((GameObject)Field(menu, "nameRoot")).activeSelf);
            Assert.AreEqual("Amn", OnlineVersionAuthenticator.PlayerName);
            Keys(); Call(menu, "BeginNameEdit", "OTHER");
            Keys(Key.Escape); Call(menu, "UpdateName");
            Assert.IsFalse(((GameObject)Field(menu, "nameRoot")).activeSelf);
            Assert.AreEqual("Amn", OnlineVersionAuthenticator.PlayerName);

            Keys(); Call(menu, "OpenPassword", null, false);
            Call(menu, "UpdatePassword"); // Subscribe physical text input.
            foreach (char c in "123456") InputSystem.QueueTextEvent(keyboard, c);
            Keys(Key.M); Call(menu, "UpdatePassword");
            Assert.IsTrue((bool)Field(menu, "passwordOpen"));
            Keys(); Keys(Key.Enter); Call(menu, "UpdatePassword");
            Assert.AreEqual("123456", Field(menu, "creationPassword"));
            Assert.IsFalse((bool)Field(menu, "passwordOpen"));
        }
        finally { profile.ApplyBindings(bindings); }
    }

    [UnityTest]
    public IEnumerator ThreeRoomBrowserShowsMatchesAndBlocksEntryAtCapacity()
    {
        yield return new EnterPlayMode();
        yield return SceneManager.LoadSceneAsync(NetworkLobby.SceneName);
        yield return null;
        yield return null;
        PrepareLobby();
        var browser = Object.FindAnyObjectByType<OnlineRoomBrowser>();
        var menu = Object.FindAnyObjectByType<OnlineLobbyMenu>();
        Assert.AreEqual("Mauricio's Room", menu.GetType().GetProperty("CurrentRoomName", Private).GetValue(menu));
        browser.Rooms.Clear();
        browser.Rooms.Add(new OnlineListedRoom { id = "a", creator = "Mauricio", name = "Mauricio's Room", mode = "BattleMode", players = 2, maxPlayers = 6, available = true, uri = "kcp://127.0.0.1:7777" });
        browser.Rooms.Add(new OnlineListedRoom { id = "b", creator = "Mene", name = "Mene's Room", mode = "NormalGame", players = 4, maxPlayers = 4, available = false, inMatch = true, uri = "kcp://127.0.0.1:7778" });
        browser.Rooms.Add(new OnlineListedRoom { id = "c", creator = "Suporte", name = "Suporte's Room", mode = "BossRush", players = 3, maxPlayers = 4, requiresPassword = true, available = true, uri = "kcp://127.0.0.1:7779" });
        Call(menu, "Update");
        Assert.IsTrue(browser.AtCapacity);
        var buttons = (System.Collections.Generic.List<UnityEngine.UI.Button>)Field(menu, "roomButtons");
        Assert.AreEqual(3, buttons.Count);
        var first = buttons[0].GetComponent<RectTransform>();
        var last = buttons[2].GetComponent<RectTransform>();
        float topPadding = -first.anchoredPosition.y - first.rect.height / 2;
        float bottomPadding = 92 + last.anchoredPosition.y - last.rect.height / 2;
        Assert.AreEqual(4, topPadding);
        Assert.AreEqual(topPadding, bottomPadding);
        Assert.IsFalse(buttons[1].interactable);
        Assert.IsTrue(buttons[1].gameObject.activeInHierarchy);
        Assert.IsFalse(((UnityEngine.UI.Button)Field(menu, "browseCreateButton")).interactable);
        Assert.AreEqual(GameTextDatabase.Online.MatchInProgress, buttons[1].transform.Find("RoomState").GetComponent<TextMeshProUGUI>().text);
        browser.JoinRoom(browser.Rooms[1]);
        Assert.IsFalse(NetworkClient.active, "Match in progress must never start a client connection.");
        browser.CreateRoom();
        Assert.IsFalse(browser.Creating, "A full directory must not allocate a fourth room.");
        yield return null;
        if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null) Capture(menu);
    }

    static void Capture(OnlineLobbyMenu menu)
    {
        // Render the actual runtime uGUI with representative local data, without publishing test rooms.
        var canvas = ((GameObject)Field(menu, "canvasRoot")).GetComponent<Canvas>();
        var actor = new GameObject("LobbyPreviewCamera", typeof(Camera));
        var camera = actor.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
        camera.transform.position = new Vector3(0, 0, -100);
        var render = new RenderTexture(1280, 1120, 24);
        camera.targetTexture = render;
        var safe = (RectTransform)Field(menu, "safeRect");
        var fitter = safe.GetComponent<UICameraViewportFitter>();
        fitter.enabled = false;
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 10;
        safe.anchorMin = Vector2.zero; safe.anchorMax = Vector2.one; safe.offsetMin = safe.offsetMax = Vector2.zero;
        ((RectTransform)Field(menu, "panelRect")).localScale = Vector3.one * 5;
        Canvas.ForceUpdateCanvases();
        camera.Render();
        var previous = RenderTexture.active;
        RenderTexture.active = render;
        var image = new Texture2D(1280, 1120, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1280, 1120), 0, 0); image.Apply();
        Directory.CreateDirectory("Captures/OnlineLobby");
        File.WriteAllBytes(Path.GetFullPath("Captures/OnlineLobby/three-rooms-discord.png"), image.EncodeToPNG());
        RenderTexture.active = previous;
        camera.targetTexture = null;
        Object.Destroy(image); Object.Destroy(render); Object.Destroy(actor);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.worldCamera = null; fitter.enabled = true;
    }
}
#endif

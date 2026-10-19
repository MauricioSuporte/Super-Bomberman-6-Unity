using System.Text;
using System.Collections.Generic;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Assets.Scripts.Netcode
{
    public class OnlineLobbyMenu : MonoBehaviour
    {
        GameObject canvasRoot;
        [SerializeField] Sprite[] backgrounds;
        [SerializeField] TMP_FontAsset lobbyFont;
        [SerializeField] AudioClip moveCursorSfx;
        Image background;
        RectTransform panelRect;
        RectTransform safeRect;
        RectTransform roomContent;
        ScrollRect roomScroll;
        TextMeshProUGUI details;
        GameObject roomEventSystem;
        Button hostButton, configureButton, leaveButton;
        readonly Button[] modeButtons = new Button[3];
        OnlineRoomMode selectedMode = OnlineRoomMode.BattleMode;
        OnlineRoomBrowser browser;
        GameObject browserRoot;
        TextMeshProUGUI browserStatus;
        Button refreshButton;
        readonly List<Button> roomButtons = new();
        readonly List<OnlineListedRoom> visibleRooms = new();
        bool roomSceneTransition;
        Button cursorButton;
        readonly List<Button> navigationButtons = new();
        float nextNavigation;
        TextMeshProUGUI timeoutLabel;
        GameObject timeoutRoot;


        void OnEnable() => SceneManager.sceneLoaded += OnRoomSceneLoaded;

        void OnDisable()
        {
            SceneManager.sceneLoaded -= OnRoomSceneLoaded;
            ReleaseRoomEventSystem();
        }

        void OnRoomSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == NetworkLobby.SceneName)
            {
                roomSceneTransition = false;
                if (GameMusicController.Instance != null)
                    GameMusicController.Instance.PlayDefaultMusic(false);
            }
        }

        void Start()
        {
            var manager = NetworkManager.singleton;
            if (manager == null || !manager.enabled) return;
            browser = GetComponent<OnlineRoomBrowser>();
            if (manager is BombermanNetworkManager dedicated && dedicated.IsDedicatedServer)
            {
                if (!NetworkServer.active) manager.StartServer();
                return;
            }
            CreateRoomCanvas();
            if (GameMusicController.Instance != null)
                GameMusicController.Instance.PlayDefaultMusic(false);
        }

        void Update()
        {
            if (canvasRoot == null) return;
            bool inRoom = NetworkServer.active || NetworkClient.isConnected;
            float remaining = BombermanNetworkManager.RoomTimeoutSeconds;
            bool inLobby = SceneManager.GetActiveScene().name == NetworkLobby.SceneName;
            timeoutRoot.SetActive(inRoom && (inLobby || remaining <= 60));
            timeoutLabel.text = string.Format(inLobby ? GameTextDatabase.Online.LobbyTimeout : GameTextDatabase.Online.IdleTimeout,
                System.TimeSpan.FromSeconds(Mathf.CeilToInt(remaining)).ToString(@"mm\:ss"));

            bool lobby = !roomSceneTransition && SceneManager.GetActiveScene().name == NetworkLobby.SceneName;
            if (lobby) EnsureRoomEventSystem();
            canvasRoot.SetActive(lobby);
            if (!canvasRoot.activeSelf) return;
            var text = GameTextDatabase.Online;
            if (backgrounds != null && backgrounds.Length > 0)
                background.sprite = backgrounds[(int)(Time.unscaledTime / 2f) % backgrounds.Length];
            bool connected = NetworkServer.active || NetworkClient.isConnected;
            bool idle = !NetworkServer.active && !NetworkClient.active;
            hostButton.gameObject.SetActive(idle && lobby);
            hostButton.interactable = browser != null && browser.DirectoryConfigured && !browser.Creating;
            for (int i = 0; i < modeButtons.Length; i++)
            {
                modeButtons[i].gameObject.SetActive(idle);
                modeButtons[i].interactable = browser != null && !browser.Creating;
                var mode = (OnlineRoomMode)i;

                modeButtons[i].GetComponentInChildren<TextMeshProUGUI>().text = text.ModeName(mode) + " (" + OnlineRoomRules.Capacity(mode) + ")" + (selectedMode == mode ? " *" : "");
            }
            configureButton.gameObject.SetActive(lobby && BombermanNetworkManager.CanConfigure);
            configureButton.interactable = BombermanNetworkManager.RoomPlayers.Length >= 1;
            hostButton.GetComponentInChildren<TextMeshProUGUI>().text = text.Host;
            configureButton.GetComponentInChildren<TextMeshProUGUI>().text = text.StartGame;
            leaveButton.GetComponentInChildren<TextMeshProUGUI>().text = idle ? text.Back : text.Leave;
            Position(leaveButton.GetComponent<RectTransform>(), idle ? -53 : 0, -78, idle ? 98 : 196, 16);
            Position(leaveButton.GetComponentInChildren<TextMeshProUGUI>().rectTransform, 0, 0, idle ? 94 : 188, 14);
            var lines = new StringBuilder();
            if (connected)
            {
                if (BombermanNetworkManager.OwnerModeLocked) lines.AppendLine(GameTextDatabase.Title.BossRushLocked);
                if (BombermanNetworkManager.RoomCompleted)
                    lines.AppendLine(text.RunCompleted + (BombermanNetworkManager.CompletedRunTime > 0 ?
                        " " + System.TimeSpan.FromSeconds(BombermanNetworkManager.CompletedRunTime).ToString(@"mm\:ss") : ""));
                lines.AppendLine(string.Format(text.You, BombermanNetworkManager.LocalRoomPlayerId));
                if (BombermanNetworkManager.RoomAdminPlayerId > 0)
                    lines.AppendLine(string.Format(text.RoomAdmin, BombermanNetworkManager.RoomAdminPlayerId));
                for (int id = 1; id <= BombermanNetworkManager.RoomCapacity; id++)
                {
                    string mode = BombermanNetworkManager.IsConnectedSlot(id) ? GameTextDatabase.BattleModeMenu.Man : text.Empty;
                    lines.AppendLine($"P{id}: {mode}");
                }
                lines.AppendLine(text.Waiting);

            }
            else if (NetworkClient.active || (browser != null && browser.Creating)) lines.AppendLine(text.Connecting);
            else if (OnlineVersionAuthenticator.VersionRejected) lines.AppendLine(text.VersionMismatch);
            details.text = lines.ToString();
            details.gameObject.SetActive(!idle || OnlineVersionAuthenticator.VersionRejected);
            UpdateBrowser(idle && lobby);
            UpdateNavigation();
        }

        void CreateRoomCanvas()
        {
            canvasRoot = new GameObject("OnlineRoomCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasRoot.transform.SetParent(transform, false);
            var canvas = canvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvas.pixelPerfect = true;
            var scaler = canvasRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            var safe = new GameObject("SafeFrame", typeof(RectTransform), typeof(UICameraViewportFitter));
            safe.transform.SetParent(canvasRoot.transform, false);
            safeRect = safe.GetComponent<RectTransform>();
            var panel = new GameObject("RoomPanel", typeof(RectTransform));
            panel.transform.SetParent(safe.transform, false);
            panelRect = panel.GetComponent<RectTransform>();
            Position(panelRect, 0, 0, 256, 224);
            var art = new GameObject("Background", typeof(RectTransform), typeof(Image));
            art.transform.SetParent(panel.transform, false);
            Position(art.GetComponent<RectTransform>(), 0, 0, 256, 224);
            background = art.GetComponent<Image>();
            background.raycastTarget = false;
            if (backgrounds != null && backgrounds.Length > 0) background.sprite = backgrounds[0];
            timeoutRoot = new GameObject("RoomTimeout", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            timeoutRoot.transform.SetParent(transform, false);
            timeoutRoot.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            timeoutRoot.GetComponent<Canvas>().sortingOrder = 200;
            var timeoutSafe = new GameObject("SafeFrame", typeof(RectTransform), typeof(UICameraViewportFitter));
            timeoutSafe.transform.SetParent(timeoutRoot.transform, false);
            timeoutLabel = Label(timeoutSafe.transform, "Timeout", 0, 0, 246, 20, 7);
            timeoutLabel.rectTransform.anchorMin = timeoutLabel.rectTransform.anchorMax = new Vector2(0.5f, 1);
            timeoutLabel.rectTransform.anchoredPosition = new Vector2(0, -16);
            timeoutLabel.textWrappingMode = TextWrappingModes.Normal;
            timeoutRoot.SetActive(false);
            details = Label(panel.transform, "Players", 0, 14, 212, 128, 7);
            details.alignment = TextAlignmentOptions.TopLeft;
            details.textWrappingMode = TextWrappingModes.Normal;
            details.overflowMode = TextOverflowModes.Truncate;
            hostButton = MakeButton(panel.transform, "Create", 0, -60, () => browser.CreateRoom(selectedMode));
            configureButton = MakeButton(panel.transform, "Configure", 0, -60, BombermanNetworkManager.ServerBeginConfiguration);
            leaveButton = MakeButton(panel.transform, "Leave", 0, -78, BombermanNetworkManager.LeaveRoom);
            for (int i = 0; i < modeButtons.Length; i++)
            {
                var mode = (OnlineRoomMode)i;
                modeButtons[i] = MakeButton(panel.transform, "Mode" + mode, 0, -3 - i * 18, () => selectedMode = mode);
            }
            CreateBrowser(panel.transform);
            EnsureRoomEventSystem();
        }

        void LateUpdate()
        {
            if (panelRect == null || safeRect == null) return;
            float scale = Mathf.Max(1f, Mathf.Floor(Mathf.Min(safeRect.rect.width / 256f, safeRect.rect.height / 224f)));
            panelRect.localScale = new Vector3(scale, scale, 1);
            float warningScale = Mathf.Max(1f, Mathf.Floor(Mathf.Min(timeoutLabel.transform.parent.GetComponent<RectTransform>().rect.width / 256f, timeoutLabel.transform.parent.GetComponent<RectTransform>().rect.height / 224f)));
            timeoutLabel.rectTransform.localScale = new Vector3(warningScale, warningScale, 1);
            timeoutLabel.rectTransform.anchoredPosition = new Vector2(0, -16 * warningScale);
            // Keep keyboard/controller selection inside the scrolling room list.
            var selected = cursorButton != null ? cursorButton.gameObject : null;
            if (selected == null || roomContent == null || selected.transform.parent != roomContent) return;
            float top = -selected.GetComponent<RectTransform>().anchoredPosition.y - 10;
            float offset = roomContent.anchoredPosition.y;
            float height = roomScroll.viewport.rect.height;
            if (top < offset) offset = top;
            else if (top + 20 > offset + height) offset = top + 20 - height;
            roomContent.anchoredPosition = new Vector2(0, Mathf.Clamp(offset, 0, Mathf.Max(0, roomContent.rect.height - height)));
        }

        void CreateBrowser(Transform parent)
        {
            browserRoot = new GameObject("RoomBrowser", typeof(RectTransform));
            browserRoot.transform.SetParent(parent, false);
            Position(browserRoot.GetComponent<RectTransform>(), 0, 0, 256, 224);
            browserStatus = Label(browserRoot.transform, "BrowserStatus", 0, 65, 212, 30, 6);
            browserStatus.textWrappingMode = TextWrappingModes.Normal;
            var viewport = new GameObject("RoomList", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            viewport.transform.SetParent(browserRoot.transform, false);
            Position(viewport.GetComponent<RectTransform>(), 0, 28, 212, 40);
            viewport.GetComponent<Image>().color = new Color(0, 0, 0, 0.15f);
            roomScroll = viewport.GetComponent<ScrollRect>();
            roomScroll.viewport = viewport.GetComponent<RectTransform>();
            roomScroll.horizontal = false;
            roomScroll.movementType = ScrollRect.MovementType.Clamped;
            roomScroll.scrollSensitivity = 20;
            roomScroll.inertia = false;
            var content = new GameObject("Rooms", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            roomContent = content.GetComponent<RectTransform>();
            roomContent.anchorMin = roomContent.anchorMax = new Vector2(0.5f, 1);
            roomContent.pivot = new Vector2(0.5f, 1);
            roomContent.sizeDelta = new Vector2(212, 40);
            roomScroll.content = roomContent;
            refreshButton = MakeButton(browserRoot.transform, "Refresh", 53, -78, () => browser.RefreshRequested = true);
            Position(refreshButton.GetComponent<RectTransform>(), 53, -78, 98, 16);
            refreshButton.GetComponentInChildren<TextMeshProUGUI>().fontSize = 5;
            Position(refreshButton.GetComponentInChildren<TextMeshProUGUI>().rectTransform, 0, 0, 94, 14);
        }

        void JoinListedRoom(int index)
        {
            var room = visibleRooms[index];
            if (room == null || NetworkClient.active || NetworkServer.active || !browser.Rooms.Contains(room)) return;
            browser.JoinRoom(room);
        }

        void UpdateBrowser(bool visible)
        {
            browserRoot.SetActive(visible);
            if (!visible || browser == null) return;
            var text = GameTextDatabase.Online;
            browserStatus.text = text.AvailableRooms + "\n" +
                (!browser.DirectoryConfigured ? text.PublicNotConfigured : browser.BuildRejected ? text.VersionMismatch : browser.DirectoryFailed ? text.PublicUnavailable :
                    browser.Creating ? text.Connecting : browser.Rooms.Count == 0 ? text.NoRooms : text.SelectRoom);
            while (roomButtons.Count < browser.Rooms.Count)
            {
                int index = roomButtons.Count;
                var button = MakeButton(roomContent, "Room" + index, 0, 0, () => JoinListedRoom(index));
                var rect = button.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1);
                rect.anchoredPosition = new Vector2(0, -10 - index * 22);
                rect.sizeDelta = new Vector2(208, 20);
                var label = button.GetComponentInChildren<TextMeshProUGUI>();
                label.richText = false;
                label.overflowMode = TextOverflowModes.Ellipsis;
                label.fontSize = 5;
                label.textWrappingMode = TextWrappingModes.Normal;
                roomButtons.Add(button);
                visibleRooms.Add(null);
            }
            roomContent.sizeDelta = new Vector2(212, Mathf.Max(40, browser.Rooms.Count * 22));
            for (int i = 0; i < roomButtons.Count; i++)
            {
                int index = i;
                visibleRooms[i] = index < browser.Rooms.Count ? browser.Rooms[index] : null;
                roomButtons[i].gameObject.SetActive(visibleRooms[i] != null);
                if (visibleRooms[i] == null) continue;
                var room = visibleRooms[i];
                roomButtons[i].GetComponentInChildren<TextMeshProUGUI>().text =
                    $"{text.ModeName(room.Mode)} / {room.name} / {room.players}/{room.maxPlayers} / {text.Join}";
            }
            refreshButton.GetComponentInChildren<TextMeshProUGUI>().text = text.RefreshRooms;
        }

        void UpdateNavigation()
        {
            navigationButtons.Clear();
            foreach (var button in modeButtons) AddNavigationButton(button);
            foreach (var button in roomButtons) AddNavigationButton(button);
            AddNavigationButton(hostButton);
            AddNavigationButton(configureButton);
            AddNavigationButton(leaveButton);
            AddNavigationButton(refreshButton);
            if (navigationButtons.Count == 0) return;
            int index = navigationButtons.IndexOf(cursorButton);
            if (index < 0) index = 0;
            var input = PlayerInputManager.Instance;
            if (input != null)
            {
                bool previous = input.Get(1, PlayerAction.MoveUp) || input.Get(1, PlayerAction.MoveLeft);
                bool next = input.Get(1, PlayerAction.MoveDown) || input.Get(1, PlayerAction.MoveRight);
                if (!previous && !next) nextNavigation = 0;
                else if (Time.unscaledTime >= nextNavigation)
                {
                    index = (index + (previous ? -1 : 1) + navigationButtons.Count) % navigationButtons.Count;
                    nextNavigation = Time.unscaledTime + 0.18f;
                    GameMusicController.Instance?.PlaySfx(moveCursorSfx, 0.5f);
                }
                cursorButton = navigationButtons[index];
                if (input.GetDown(1, PlayerAction.ActionA) || input.GetDown(1, PlayerAction.Start))
                    cursorButton.onClick.Invoke();
                else if (input.GetDown(1, PlayerAction.ActionB)) leaveButton.onClick.Invoke();
            }
            cursorButton = navigationButtons[index];
            foreach (var button in navigationButtons)
                button.GetComponent<Image>().color = button == cursorButton
                    ? new Color32(93, 65, 149, 255) : new Color32(30, 22, 61, 255);
        }

        void AddNavigationButton(Button button)
        {
            if (button != null && button.gameObject.activeInHierarchy && button.interactable)
                navigationButtons.Add(button);
        }

        void EnsureRoomEventSystem()
        {
            if (roomSceneTransition || SceneManager.GetActiveScene().name != NetworkLobby.SceneName) return;
            if (EventSystem.current != null) return;
            var existing = FindAnyObjectByType<EventSystem>();
            if (existing != null && existing.isActiveAndEnabled) return;
            roomEventSystem = new GameObject("OnlineRoomEventSystem", typeof(EventSystem));
            // Keep input owned by the lobby scene rather than its persistent NetworkManager.
            SceneManager.MoveGameObjectToScene(roomEventSystem, SceneManager.GetActiveScene());

            EventSystem.current.SetSelectedGameObject(hostButton.gameObject);
        }

        public void ReleaseRoomEventSystem()
        {
            roomSceneTransition = true;
            if (canvasRoot != null) canvasRoot.SetActive(false);
            if (roomEventSystem == null) return;
            roomEventSystem.SetActive(false);
            Destroy(roomEventSystem);
            roomEventSystem = null;
        }

        static void Position(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
        }

        TextMeshProUGUI Label(Transform parent, string name, float x, float y, float width, float height, int size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            Position(go.GetComponent<RectTransform>(), x, y, width, height);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.font = lobbyFont != null ? lobbyFont : TMP_Settings.defaultFontAsset;
            text.color = new Color32(32, 255, 72, 255);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = Color.black;
            shadow.effectDistance = new Vector2(1, -1);
            text.fontSize = size;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            LocalizedTmpFontFallback.Apply(text);
            return text;
        }

        Button MakeButton(Transform parent, string name, float x, float y, UnityEngine.Events.UnityAction action)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            Position(go.GetComponent<RectTransform>(), x, y, 196, 16);
            go.GetComponent<Image>().color = new Color32(30, 22, 61, 255);
            var border = go.AddComponent<Outline>();
            border.effectColor = new Color32(202, 161, 54, 255);
            border.effectDistance = new Vector2(1, -1);
            Label(go.transform, "Label", 0, 0, 188, 14, 6);
            var button = go.GetComponent<Button>();
            var colors = button.colors;
            button.transition = Selectable.Transition.None;
            colors.highlightedColor = new Color32(93, 65, 149, 255);
            colors.selectedColor = new Color32(93, 65, 149, 255);
            colors.pressedColor = new Color32(255, 216, 86, 255);
            colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 1);
            colors.fadeDuration = 0;
            button.colors = colors;
            button.onClick.AddListener(action);
            return button;
        }
    }
}

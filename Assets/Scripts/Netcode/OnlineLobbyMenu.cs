using System.Text;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Assets.Scripts.Netcode
{
    public class OnlineLobbyMenu : MonoBehaviour
    {
        GameObject canvasRoot;
        TextMeshProUGUI heading;
        TextMeshProUGUI details;
        GameObject roomEventSystem;
        Button hostButton, configureButton, leaveButton;
        readonly Button[] modeButtons = new Button[3];
        OnlineRoomMode selectedMode = OnlineRoomMode.BattleMode;
        OnlineRoomBrowser browser;
        GameObject browserRoot;
        TextMeshProUGUI browserStatus, pageLabel;
        Button refreshButton, previousButton, nextButton;
        readonly Button[] roomButtons = new Button[4];
        readonly OnlineListedRoom[] visibleRooms = new OnlineListedRoom[4];
        int page;
        bool roomSceneTransition;

        void OnEnable() => SceneManager.sceneLoaded += OnRoomSceneLoaded;

        void OnDisable()
        {
            SceneManager.sceneLoaded -= OnRoomSceneLoaded;
            ReleaseRoomEventSystem();
        }

        void OnRoomSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == NetworkLobby.SceneName) roomSceneTransition = false;
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
        }

        void Update()
        {
            if (canvasRoot == null) return;
            bool lobby = !roomSceneTransition && SceneManager.GetActiveScene().name == NetworkLobby.SceneName;
            if (lobby) EnsureRoomEventSystem();
            canvasRoot.SetActive(lobby);
            if (!canvasRoot.activeSelf) return;
            var text = GameTextDatabase.Online;
            heading.text = text.Room + " — " + text.ModeName(
                NetworkClient.isConnected || NetworkServer.active ? BombermanNetworkManager.RoomMode : selectedMode);
            bool connected = NetworkServer.active || NetworkClient.isConnected;
            bool idle = !NetworkServer.active && !NetworkClient.active;
            hostButton.gameObject.SetActive(idle && lobby);
            hostButton.interactable = browser != null && browser.DirectoryConfigured && !browser.Creating;
            for (int i = 0; i < modeButtons.Length; i++)
            {
                modeButtons[i].gameObject.SetActive(idle);
                modeButtons[i].interactable = browser != null && !browser.Creating;
                var mode = (OnlineRoomMode)i;
                modeButtons[i].GetComponent<Image>().color = selectedMode == mode ? new Color(0.25f, 0.5f, 0.65f) : new Color(0.15f, 0.25f, 0.42f);
                modeButtons[i].GetComponentInChildren<TextMeshProUGUI>().text = text.ModeName(mode) + " (" + OnlineRoomRules.Capacity(mode) + ")";
            }
            configureButton.gameObject.SetActive(lobby && BombermanNetworkManager.CanConfigure);
            configureButton.interactable = BombermanNetworkManager.RoomPlayers.Length >= 2 &&
                BombermanNetworkManager.RoomMode == OnlineRoomMode.BattleMode;
            if (EventSystem.current != null && (EventSystem.current.currentSelectedGameObject == null ||
                !EventSystem.current.currentSelectedGameObject.activeInHierarchy))
            {
                var selection = configureButton.gameObject.activeSelf && configureButton.interactable ? configureButton :
                    hostButton.gameObject.activeSelf ? hostButton : leaveButton;
                EventSystem.current.SetSelectedGameObject(selection.gameObject);
            }
            hostButton.GetComponentInChildren<TextMeshProUGUI>().text = text.Host;
            configureButton.GetComponentInChildren<TextMeshProUGUI>().text = text.Configure;
            leaveButton.GetComponentInChildren<TextMeshProUGUI>().text = text.Leave;
            var lines = new StringBuilder();
            if (connected)
            {
                lines.AppendLine(string.Format(text.You, BombermanNetworkManager.LocalRoomPlayerId));
                if (BombermanNetworkManager.RoomAdminPlayerId > 0)
                    lines.AppendLine(string.Format(text.RoomAdmin, BombermanNetworkManager.RoomAdminPlayerId));
                for (int id = 1; id <= BombermanNetworkManager.RoomCapacity; id++)
                {
                    string mode = BombermanNetworkManager.IsConnectedSlot(id) ? GameTextDatabase.BattleModeMenu.Man : text.Empty;
                    lines.AppendLine($"P{id}: {mode}");
                }
                lines.AppendLine(BombermanNetworkManager.RoomMode != OnlineRoomMode.BattleMode ? text.CoopPending :
                    BombermanNetworkManager.RoomPlayers.Length < 2 ? text.NeedPlayers : text.Waiting);

            }
            else if (NetworkClient.active || (browser != null && browser.Creating)) lines.AppendLine(text.Connecting);
            else if (OnlineVersionAuthenticator.VersionRejected) lines.AppendLine(text.VersionMismatch);
            details.text = lines.ToString();
            details.gameObject.SetActive(!idle || OnlineVersionAuthenticator.VersionRejected);
            UpdateBrowser(idle && lobby);
        }

        void CreateRoomCanvas()
        {
            canvasRoot = new GameObject("OnlineRoomCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasRoot.transform.SetParent(transform, false);
            var canvas = canvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 1120);
            scaler.matchWidthOrHeight = 1;
            var safe = new GameObject("SafeFrame", typeof(RectTransform), typeof(UICameraViewportFitter));
            safe.transform.SetParent(canvasRoot.transform, false);
            var panel = new GameObject("RoomPanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(safe.transform, false);
            Position(panel.GetComponent<RectTransform>(), 0, 0, 1160, 1040);
            panel.GetComponent<Image>().color = new Color(0.035f, 0.045f, 0.09f, 0.98f);
            heading = Label(panel.transform, "Heading", 0, 435, 1080, 100, 32);
            details = Label(panel.transform, "Players", 0, 115, 1040, 510, 28);
            details.alignment = TextAlignmentOptions.TopLeft;
            details.textWrappingMode = TextWrappingModes.Normal;
            details.overflowMode = TextOverflowModes.Truncate;
            hostButton = MakeButton(panel.transform, "Create", 0, -280, () => browser.CreateRoom(selectedMode));
            configureButton = MakeButton(panel.transform, "Configure", 0, -280, BombermanNetworkManager.ServerBeginConfiguration);
            leaveButton = MakeButton(panel.transform, "Leave", 0, -390, BombermanNetworkManager.LeaveRoom);
            for (int i = 0; i < modeButtons.Length; i++)
            {
                var mode = (OnlineRoomMode)i;
                modeButtons[i] = MakeButton(panel.transform, "Mode" + mode, -350 + i * 350, -195, () => selectedMode = mode);
                Position(modeButtons[i].GetComponent<RectTransform>(), -350 + i * 350, -195, 335, 60);
                var label = modeButtons[i].GetComponentInChildren<TextMeshProUGUI>();
                Position(label.rectTransform, 0, 0, 325, 55);
                label.fontSize = 20;
            }
            CreateBrowser(panel.transform);
            EnsureRoomEventSystem();
        }

        void CreateBrowser(Transform parent)
        {
            browserRoot = new GameObject("RoomBrowser", typeof(RectTransform));
            browserRoot.transform.SetParent(parent, false);
            Position(browserRoot.GetComponent<RectTransform>(), 0, 0, 1080, 1040);
            browserStatus = Label(browserRoot.transform, "BrowserStatus", 0, 355, 1040, 75, 23);
            for (int i = 0; i < roomButtons.Length; i++)
            {
                int index = i;
                roomButtons[i] = MakeButton(browserRoot.transform, "Room" + i, 0, 265 - i * 85, () => JoinListedRoom(index));
                Position(roomButtons[i].GetComponent<RectTransform>(), 0, 265 - i * 85, 1000, 70);
                var label = roomButtons[i].GetComponentInChildren<TextMeshProUGUI>();
                Position(label.rectTransform, 0, 0, 960, 65);
                label.fontSize = 22;
                label.richText = false;
                label.overflowMode = TextOverflowModes.Ellipsis;
            }
            previousButton = MakeButton(browserRoot.transform, "Previous", -405, -80, () => page = Mathf.Max(0, page - 1));
            nextButton = MakeButton(browserRoot.transform, "Next", 405, -80, () => page++);
            Position(previousButton.GetComponent<RectTransform>(), -405, -80, 160, 55);
            Position(nextButton.GetComponent<RectTransform>(), 405, -80, 160, 55);
            previousButton.GetComponentInChildren<TextMeshProUGUI>().text = "<";
            nextButton.GetComponentInChildren<TextMeshProUGUI>().text = ">";
            pageLabel = Label(browserRoot.transform, "Page", 0, -80, 400, 55, 22);
            refreshButton = MakeButton(browserRoot.transform, "Refresh", -250, -450, () =>
            {
                browser.RefreshRequested = true;
            });
            Position(refreshButton.GetComponent<RectTransform>(), -250, -450, 460, 55);
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
            int pages = Mathf.Max(1, Mathf.CeilToInt(browser.Rooms.Count / 4f));
            page = Mathf.Clamp(page, 0, pages - 1);
            browserStatus.text = text.AvailableRooms + "\n" +
                (!browser.DirectoryConfigured ? text.PublicNotConfigured : browser.DirectoryFailed ? text.PublicUnavailable :
                    browser.Creating ? text.Connecting : browser.Rooms.Count == 0 ? text.NoRooms : text.SelectRoom);
            for (int i = 0; i < roomButtons.Length; i++)
            {
                int index = page * 4 + i;
                visibleRooms[i] = index < browser.Rooms.Count ? browser.Rooms[index] : null;
                roomButtons[i].gameObject.SetActive(visibleRooms[i] != null);
                if (visibleRooms[i] == null) continue;
                var room = visibleRooms[i];
                roomButtons[i].GetComponentInChildren<TextMeshProUGUI>().text =
                    $"{text.ModeName(room.Mode)} · {room.name} · {room.players}/{room.maxPlayers} · {text.Join}";
            }
            previousButton.interactable = page > 0;
            nextButton.interactable = page + 1 < pages;
            pageLabel.text = $"{page + 1} / {pages}";
            refreshButton.GetComponentInChildren<TextMeshProUGUI>().text = text.RefreshRooms;
        }

        void EnsureRoomEventSystem()
        {
            if (roomSceneTransition || SceneManager.GetActiveScene().name != NetworkLobby.SceneName) return;
            if (EventSystem.current != null) return;
            var existing = FindAnyObjectByType<EventSystem>();
            if (existing != null && existing.isActiveAndEnabled) return;
            roomEventSystem = new GameObject("OnlineRoomEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            // Keep input owned by the lobby scene rather than its persistent NetworkManager.
            SceneManager.MoveGameObjectToScene(roomEventSystem, SceneManager.GetActiveScene());
            roomEventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
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

        static TextMeshProUGUI Label(Transform parent, string name, float x, float y, float width, float height, int size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            Position(go.GetComponent<RectTransform>(), x, y, width, height);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = size;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            LocalizedTmpFontFallback.Apply(text);
            return text;
        }

        static Button MakeButton(Transform parent, string name, float x, float y, UnityEngine.Events.UnityAction action)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            Position(go.GetComponent<RectTransform>(), x, y, 460, 80);
            go.GetComponent<Image>().color = new Color(0.15f, 0.25f, 0.42f);
            Label(go.transform, "Label", 0, 0, 440, 70, 26);
            var button = go.GetComponent<Button>();
            button.onClick.AddListener(action);
            return button;
        }
    }
}

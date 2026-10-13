using System;
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
        [SerializeField] string battleSceneName = "BattleMode_1";
        [SerializeField] string address = "localhost";
        GameObject canvasRoot;
        TextMeshProUGUI heading;
        TextMeshProUGUI details;
        TMP_InputField addressInput;
        GameObject roomEventSystem;
        Button hostButton, joinButton, configureButton, leaveButton;
        OnlineRoomBrowser browser;
        GameObject browserRoot;
        TextMeshProUGUI browserStatus, pageLabel;
        TextMeshProUGUI directJoinLabel;
        Button refreshButton, publicButton, previousButton, nextButton;
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
            if (OnlineRelayProbe.Active)
            {
                if (OnlineRelayProbe.Role == "host") manager.StartHost();
                else manager.StartClient();
                CreateRoomCanvas();
                return;
            }
            if (HasArg("-server")) { manager.StartServer(); return; }
            if (HasArg("-autohost"))
            {
                manager.StartHost();
                Invoke(nameof(AutoStartMatch), 1.5f);
            }
            CreateRoomCanvas();
        }

        void AutoStartMatch() => BombermanNetworkManager.ServerStartMatch(battleSceneName);
        static bool HasArg(string value) => Array.Exists(Environment.GetCommandLineArgs(),
            arg => string.Equals(arg, value, StringComparison.OrdinalIgnoreCase));

        void Update()
        {
            if (canvasRoot == null) return;
            bool lobby = !roomSceneTransition && SceneManager.GetActiveScene().name == NetworkLobby.SceneName;
            if (lobby) EnsureRoomEventSystem();
            canvasRoot.SetActive(lobby);
            if (!canvasRoot.activeSelf) return;
            var text = GameTextDatabase.Online;
            heading.text = text.Room + " — " + text.BattleMode;
            bool connected = NetworkServer.active || NetworkClient.isConnected;
            bool idle = !NetworkServer.active && !NetworkClient.active;
            idle &= !OnlineRelayProbe.Active;
            hostButton.gameObject.SetActive(idle && lobby);
            joinButton.gameObject.SetActive(idle && lobby);
            addressInput.gameObject.SetActive(idle && lobby);
            configureButton.gameObject.SetActive(lobby && BombermanNetworkManager.CanConfigure);
            configureButton.interactable = BombermanNetworkManager.RoomPlayers.Length >= 2;
            if (EventSystem.current != null && (EventSystem.current.currentSelectedGameObject == null ||
                !EventSystem.current.currentSelectedGameObject.activeInHierarchy))
            {
                var selection = configureButton.gameObject.activeSelf && configureButton.interactable ? configureButton :
                    hostButton.gameObject.activeSelf ? hostButton : leaveButton;
                EventSystem.current.SetSelectedGameObject(selection.gameObject);
            }
            hostButton.GetComponentInChildren<TextMeshProUGUI>().text = text.Host;
            joinButton.GetComponentInChildren<TextMeshProUGUI>().text = text.Join;
            configureButton.GetComponentInChildren<TextMeshProUGUI>().text = text.Configure;
            leaveButton.GetComponentInChildren<TextMeshProUGUI>().text = text.Leave;
            var lines = new StringBuilder();
            if (connected)
            {
                lines.AppendLine(string.Format(text.You, BombermanNetworkManager.LocalRoomPlayerId));
                for (int id = 1; id <= GameSession.MaxPlayerId; id++)
                {
                    string mode = BombermanNetworkManager.IsConnectedSlot(id) ? GameTextDatabase.BattleModeMenu.Man : text.Empty;
                    lines.AppendLine($"P{id}: {mode}");
                }
                lines.AppendLine(BombermanNetworkManager.RoomPlayers.Length < 2 ? text.NeedPlayers : text.Waiting);
                if (browser.PublishPublic && browser.DirectoryFailed) lines.AppendLine(text.PublicUnavailable);
            }
            else if (NetworkClient.active) lines.AppendLine(text.Connecting);
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
            var inputRoot = new GameObject("HostAddress", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
            inputRoot.transform.SetParent(panel.transform, false);
            Position(inputRoot.GetComponent<RectTransform>(), 0, -185, 940, 65);
            inputRoot.GetComponent<Image>().color = new Color(0.15f, 0.17f, 0.24f);
            var inputText = Label(inputRoot.transform, "Value", 0, 0, 880, 60, 28);
            inputText.alignment = TextAlignmentOptions.Left;
            addressInput = inputRoot.GetComponent<TMP_InputField>();
            addressInput.textViewport = inputRoot.GetComponent<RectTransform>();
            addressInput.textComponent = inputText;
            addressInput.characterLimit = 255;
            addressInput.text = address;
            hostButton = MakeButton(panel.transform, "Host", -250, -280, () => NetworkManager.singleton.StartHost());
            joinButton = MakeButton(panel.transform, "Join", 250, -280, () =>
            {
                address = addressInput.text.Trim();
                if (string.IsNullOrEmpty(address)) return;
                NetworkManager.singleton.networkAddress = address;
                NetworkManager.singleton.StartClient();
            });
            configureButton = MakeButton(panel.transform, "Configure", 0, -280, BombermanNetworkManager.ServerBeginConfiguration);
            leaveButton = MakeButton(panel.transform, "Leave", 0, -390, BombermanNetworkManager.LeaveRoom);
            CreateBrowser(panel.transform);
            EnsureRoomEventSystem();
        }

        void CreateBrowser(Transform parent)
        {
            browserRoot = new GameObject("RoomBrowser", typeof(RectTransform));
            browserRoot.transform.SetParent(parent, false);
            Position(browserRoot.GetComponent<RectTransform>(), 0, 0, 1080, 1040);
            browserStatus = Label(browserRoot.transform, "BrowserStatus", 0, 355, 1040, 75, 23);
            directJoinLabel = Label(browserRoot.transform, "DirectJoin", 0, -135, 1040, 40, 22);
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
                GetComponent<OnlineRoomDiscovery>().BroadcastDiscoveryRequest();
            });
            publicButton = MakeButton(browserRoot.transform, "Public", 250, -450, () => browser.PublishPublic = !browser.PublishPublic);
            Position(refreshButton.GetComponent<RectTransform>(), -250, -450, 460, 55);
            Position(publicButton.GetComponent<RectTransform>(), 250, -450, 460, 55);
        }

        void JoinListedRoom(int index)
        {
            var room = visibleRooms[index];
            if (room == null || NetworkClient.active || NetworkServer.active || !browser.Rooms.Contains(room)) return;
            if (Uri.TryCreate(room.uri, UriKind.Absolute, out var uri)) NetworkManager.singleton.StartClient(uri);
        }

        void UpdateBrowser(bool visible)
        {
            browserRoot.SetActive(visible);
            if (!visible || browser == null) return;
            var text = GameTextDatabase.Online;
            directJoinLabel.text = text.DirectJoin;
            int pages = Mathf.Max(1, Mathf.CeilToInt(browser.Rooms.Count / 4f));
            page = Mathf.Clamp(page, 0, pages - 1);
            browserStatus.text = text.AvailableRooms + "\n" +
                (browser.LanFailed ? text.LanUnavailable : browser.Rooms.Count == 0 ? text.NoRooms : text.SelectRoom) + " · " +
                (!browser.DirectoryConfigured ? text.PublicNotConfigured : browser.DirectoryFailed ? text.PublicUnavailable : text.PublicRooms);
            for (int i = 0; i < roomButtons.Length; i++)
            {
                int index = page * 4 + i;
                visibleRooms[i] = index < browser.Rooms.Count ? browser.Rooms[index] : null;
                roomButtons[i].gameObject.SetActive(visibleRooms[i] != null);
                if (visibleRooms[i] == null) continue;
                var room = visibleRooms[i];
                roomButtons[i].GetComponentInChildren<TextMeshProUGUI>().text =
                    $"{(room.local ? text.LocalRooms : text.PublicRooms)} · {room.name} · {room.players}/6 · {text.Join}";
            }
            previousButton.interactable = page > 0;
            nextButton.interactable = page + 1 < pages;
            pageLabel.text = $"{page + 1} / {pages}";
            refreshButton.GetComponentInChildren<TextMeshProUGUI>().text = text.RefreshRooms;
            publicButton.interactable = browser.DirectoryConfigured;
            publicButton.GetComponentInChildren<TextMeshProUGUI>().text = browser.PublishPublic ? text.CreatePublic : text.CreateLocal;
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

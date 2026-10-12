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

        void Start()
        {
            var manager = NetworkManager.singleton;
            if (manager == null) return;
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
            bool lobby = SceneManager.GetActiveScene().name == NetworkLobby.SceneName;
            if (lobby) EnsureRoomEventSystem();
            canvasRoot.SetActive(lobby);
            if (!canvasRoot.activeSelf) return;
            var text = GameTextDatabase.Online;
            heading.text = text.Room + " — " + text.BattleMode;
            bool connected = NetworkServer.active || NetworkClient.isConnected;
            bool idle = !NetworkServer.active && !NetworkClient.active;
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
            }
            else lines.AppendLine(NetworkClient.active ? text.Connecting : text.Address);
            details.text = lines.ToString();
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
            EnsureRoomEventSystem();
        }

        void EnsureRoomEventSystem()
        {
            if (EventSystem.current != null) return;
            roomEventSystem = new GameObject("OnlineRoomEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            roomEventSystem.transform.SetParent(transform, false);
            roomEventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            EventSystem.current.SetSelectedGameObject(hostButton.gameObject);
        }

        public void ReleaseRoomEventSystem()
        {
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

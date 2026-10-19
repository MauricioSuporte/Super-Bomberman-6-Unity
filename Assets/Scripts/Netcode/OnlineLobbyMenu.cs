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
        [SerializeField] AudioClip confirmSfx, returnSfx;
        Image background;
        RectTransform panelRect;
        RectTransform safeRect;
        RectTransform roomContent;
        ScrollRect roomScroll;
        TextMeshProUGUI details;
        GameObject roomEventSystem;
        Button hostButton, configureButton, leaveButton;
        Button modeButton, privacyButton, passwordButton;
        TextMeshProUGUI modeLeft, modeRight, privacyLeft, privacyRight;
        GameObject nameRoot;
        TextMeshProUGUI nameText, nameTitle, nameHints;
        string editedName = "";
        int nameCursor, nameTextFrame = -1;
        bool invalidName;
        const string NameCharacters = " ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_-";
        GameObject passwordRoot;
        TextMeshProUGUI passwordText, passwordTitle, passwordHints;
        readonly int[] passwordDigits = new int[6];
        int passwordDigit;
        bool privateRoom, passwordOpen, createAfterPassword;
        string creationPassword = "";
        OnlineListedRoom passwordRoom;
        OnlineRoomMode selectedMode = OnlineRoomMode.BattleMode;
        OnlineRoomBrowser browser;
        GameObject browserRoot;
        TextMeshProUGUI browserStatus;
        Button nameButton, updateButton;
        bool nameRequired;
        const int NameLength = OnlineVersionAuthenticator.MaxPlayerNameLength;
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
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current != null)
                UnityEngine.InputSystem.Keyboard.current.onTextInput -= TypeName;
#endif
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
            bool incompatible = OnlineVersionAuthenticator.VersionRejected || (browser != null && browser.VersionBlocked);
            if (incompatible)
            {
                if (nameRoot.activeSelf) CloseName();
                passwordOpen = false;
                passwordRoot.SetActive(false);
            }
            nameButton.gameObject.SetActive(idle && !incompatible);
            updateButton.gameObject.SetActive(idle && incompatible);
            updateButton.GetComponentInChildren<TextMeshProUGUI>(true).text = text.RunUpdater;
            roomScroll.gameObject.SetActive(!incompatible);
            nameButton.GetComponentInChildren<TextMeshProUGUI>(true).text = text.EditName;
            hostButton.gameObject.SetActive(idle && lobby && !incompatible);
            hostButton.interactable = browser != null && browser.DirectoryConfigured && browser.CanUseOnline && !browser.Creating;
            passwordButton.gameObject.SetActive(idle && privateRoom && !incompatible);
            passwordButton.GetComponentInChildren<TextMeshProUGUI>(true).text = text.Password + ": ******";
            modeButton.gameObject.SetActive(idle && !incompatible);
            privacyButton.gameObject.SetActive(idle && !incompatible);
            modeButton.interactable = privacyButton.interactable = browser != null && !browser.Creating;
            modeButton.GetComponentInChildren<TextMeshProUGUI>(true).text = text.ModeName(selectedMode);
            privacyButton.GetComponentInChildren<TextMeshProUGUI>(true).text = privateRoom ? text.PrivateRoom : text.PublicRooms;
            bool arrowsVisible = idle && !incompatible && Mathf.Repeat(Time.unscaledTime, 0.8f) < 0.5f;
            modeLeft.gameObject.SetActive(arrowsVisible); modeRight.gameObject.SetActive(arrowsVisible);
            privacyLeft.gameObject.SetActive(arrowsVisible); privacyRight.gameObject.SetActive(arrowsVisible);
            configureButton.gameObject.SetActive(lobby && BombermanNetworkManager.CanConfigure);
            configureButton.interactable = BombermanNetworkManager.RoomPlayers.Length >= 1;
            hostButton.GetComponentInChildren<TextMeshProUGUI>(true).text = text.Host;
            configureButton.GetComponentInChildren<TextMeshProUGUI>(true).text = text.StartGame;
            leaveButton.GetComponentInChildren<TextMeshProUGUI>(true).text = idle ? text.Back : text.Leave;
            Position(leaveButton.GetComponent<RectTransform>(), idle ? -53 : 0, -78, idle ? 98 : 196, 16);
            Position(leaveButton.GetComponentInChildren<TextMeshProUGUI>(true).rectTransform, 0, 0, idle ? 94 : 188, 14);
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
                    string mode = BombermanNetworkManager.IsConnectedSlot(id) ? BombermanNetworkManager.RoomPlayerName(id) : text.Empty;
                    lines.AppendLine($"P{id}: {mode}");
                }
                lines.AppendLine(text.Waiting);

            }
            else if (NetworkClient.active || (browser != null && browser.Creating)) lines.AppendLine(text.Connecting);
            else if (OnlineVersionAuthenticator.VersionRejected) lines.AppendLine(text.VersionMismatch);
            details.text = lines.ToString();
            details.gameObject.SetActive(!idle);
            UpdateBrowser(idle && lobby);
            if (nameRoot.activeSelf) UpdateName(); else if (passwordOpen) UpdatePassword(); else UpdateNavigation();
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
            details = Label(panel.transform, "Players", 0, 34, 212, 96, 7);
            details.alignment = TextAlignmentOptions.TopLeft;
            details.textWrappingMode = TextWrappingModes.Normal;
            details.overflowMode = TextOverflowModes.Truncate;
            hostButton = MakeButton(panel.transform, "Create", 0, -56, CreateSelectedRoom);
            configureButton = MakeButton(panel.transform, "Configure", 0, -50, BombermanNetworkManager.ServerBeginConfiguration);
            leaveButton = MakeButton(panel.transform, "Leave", 0, -78, LeaveLobby);
            modeButton = MakeButton(panel.transform, "RoomType", 0, 2, () => ChangeMode(1));
            privacyButton = MakeButton(panel.transform, "Visibility", 0, -20, TogglePrivacy);
            passwordButton = MakeButton(panel.transform, "EditPassword", 0, -38, () => OpenPassword(null, false));
            cursorButton = modeButton;
            modeLeft = Label(modeButton.transform, "PreviousType", -108, 0, 10, 16, 8); modeLeft.text = "<";
            modeRight = Label(modeButton.transform, "NextType", 108, 0, 10, 16, 8); modeRight.text = ">";
            privacyLeft = Label(privacyButton.transform, "PreviousVisibility", -108, 0, 10, 16, 8); privacyLeft.text = "<";
            privacyRight = Label(privacyButton.transform, "NextVisibility", 108, 0, 10, 16, 8); privacyRight.text = ">";
            passwordRoot = new GameObject("Password", typeof(RectTransform), typeof(Image), typeof(Outline));
            passwordRoot.transform.SetParent(panel.transform, false);
            Position(passwordRoot.GetComponent<RectTransform>(), 0, 0, 244, 160);
            passwordRoot.GetComponent<Image>().color = new Color32(30, 22, 61, 255);
            passwordRoot.GetComponent<Outline>().effectColor = new Color32(173, 133, 231, 255);
            passwordRoot.GetComponent<Outline>().effectDistance = new Vector2(1, -1);
            passwordTitle = Label(passwordRoot.transform, "PasswordTitle", 0, 56, 232, 18, 8);
            var entry = new GameObject("PasswordEntry", typeof(RectTransform), typeof(Image), typeof(Outline));
            entry.transform.SetParent(passwordRoot.transform, false);
            Position(entry.GetComponent<RectTransform>(), 0, 22, 200, 26);
            entry.GetComponent<Image>().color = new Color32(12, 9, 29, 255);
            entry.GetComponent<Outline>().effectColor = new Color32(173, 133, 231, 255);
            entry.GetComponent<Outline>().effectDistance = Vector2.one;
            passwordText = Label(entry.transform, "PasswordText", 0, 0, 194, 22, 8);
            passwordHints = Label(passwordRoot.transform, "PasswordHints", 0, -40, 232, 64, 6);
            passwordHints.alignment = TextAlignmentOptions.Bottom;
            passwordRoot.SetActive(false);
            nameRoot = new GameObject("PlayerName", typeof(RectTransform), typeof(Image), typeof(Outline));
            nameRoot.transform.SetParent(panel.transform, false);
            Position(nameRoot.GetComponent<RectTransform>(), 0, 0, 244, 160);
            nameRoot.GetComponent<Image>().color = new Color32(30, 22, 61, 255);
            nameRoot.GetComponent<Outline>().effectColor = new Color32(173, 133, 231, 255);
            nameRoot.GetComponent<Outline>().effectDistance = new Vector2(1, -1);
            nameTitle = Label(nameRoot.transform, "Title", 0, 55, 232, 20, 7);
            nameText = Label(nameRoot.transform, "Name", 0, 20, 232, 25, 8);
            nameHints = Label(nameRoot.transform, "Hints", 0, -38, 232, 66, 6);
            nameHints.alignment = TextAlignmentOptions.Bottom;
            nameRoot.SetActive(false);
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
            timeoutLabel.rectTransform.anchorMin = timeoutLabel.rectTransform.anchorMax = new Vector2(0.5f, 0);
            bool lobby = SceneManager.GetActiveScene().name == NetworkLobby.SceneName;
            timeoutLabel.rectTransform.anchoredPosition = new Vector2(0, (lobby ? 84 : 22) * warningScale);
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
            browserStatus = Label(browserRoot.transform, "BrowserStatus", 0, 65, 232, 30, 6);
            browserStatus.textWrappingMode = TextWrappingModes.Normal;
            var viewport = new GameObject("RoomList", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            viewport.transform.SetParent(browserRoot.transform, false);
            Position(viewport.GetComponent<RectTransform>(), 0, 32, 212, 40);
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
            nameButton = MakeButton(browserRoot.transform, "EditName", 53, -78, OpenName);
            Position(nameButton.GetComponent<RectTransform>(), 53, -78, 98, 16);
            nameButton.GetComponentInChildren<TextMeshProUGUI>(true).fontSize = 5;
            Position(nameButton.GetComponentInChildren<TextMeshProUGUI>(true).rectTransform, 0, 0, 94, 14);
            updateButton = MakeButton(browserRoot.transform, "UpdateGame", 0, 28, RunUpdater);
            updateButton.gameObject.SetActive(false);
        }

        void JoinListedRoom(int index)
        {
            if (browser == null || !browser.CanUseOnline || index < 0 || index >= visibleRooms.Count) return;
            var room = visibleRooms[index];
            if (room == null || NetworkClient.active || NetworkServer.active || !browser.Rooms.Contains(room)) return;
            if (!HasPlayerName()) return;
            if (room.requiresPassword) OpenPassword(room, false);
            else browser.JoinRoom(room);
        }

        void UpdateBrowser(bool visible)
        {
            browserRoot.SetActive(visible);
            if (!visible || browser == null) return;
            var text = GameTextDatabase.Online;
            string savedName = OnlineVersionAuthenticator.PlayerName;
            if (browser.VersionBlocked)
            {
                browserStatus.text = text.VersionMismatch;
                foreach (var button in roomButtons) button.gameObject.SetActive(false);
                return;
            }
            browserStatus.text = text.CurrentName + ": " + (string.IsNullOrEmpty(savedName) ? text.Empty : savedName) + "\n" +
                (!browser.DirectoryConfigured ? text.PublicNotConfigured : nameRequired || OnlineVersionAuthenticator.NameRejected ? text.ChangeNameRequired : OnlineVersionAuthenticator.PasswordRejected ? text.WrongPassword : browser.BuildRejected || OnlineVersionAuthenticator.VersionRejected ? text.VersionMismatch : browser.DirectoryFailed ? text.PublicUnavailable :
                    browser.Creating ? text.Connecting : browser.Rooms.Count == 0 ? text.NoRooms : text.SelectRoom);
            while (roomButtons.Count < browser.Rooms.Count)
            {
                int index = roomButtons.Count;
                var button = MakeButton(roomContent, "Room" + index, 0, 0, () => JoinListedRoom(index));
                var rect = button.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1);
                rect.anchoredPosition = new Vector2(0, -10 - index * 22);
                rect.sizeDelta = new Vector2(208, 20);
                var label = button.GetComponentInChildren<TextMeshProUGUI>(true);
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
                roomButtons[i].GetComponentInChildren<TextMeshProUGUI>(true).text =
                    $"{text.ModeName(room.Mode)} / {room.name} / {(room.requiresPassword ? text.PrivateRoom : text.PublicRooms)} / {text.Join}";
            }

        }

        void UpdateNavigation()
        {
            navigationButtons.Clear();
            AddNavigationButton(modeButton);
            AddNavigationButton(privacyButton);
            AddNavigationButton(passwordButton);
            foreach (var button in roomButtons) AddNavigationButton(button);
            AddNavigationButton(hostButton);
            AddNavigationButton(configureButton);
            AddNavigationButton(leaveButton);
            AddNavigationButton(nameButton);
            AddNavigationButton(updateButton);
            if (navigationButtons.Count == 0) return;
            int index = navigationButtons.IndexOf(cursorButton);
            if (index < 0) index = 0;
            var input = PlayerInputManager.Instance;
            if (input != null)
            {
                bool previous = input.Get(1, PlayerAction.MoveUp);
                bool next = input.Get(1, PlayerAction.MoveDown);
                if (!previous && !next) nextNavigation = 0;
                else if (Time.unscaledTime >= nextNavigation)
                {
                    index = (index + (previous ? -1 : 1) + navigationButtons.Count) % navigationButtons.Count;
                    nextNavigation = Time.unscaledTime + 0.18f;
                    GameMusicController.Instance?.PlaySfx(moveCursorSfx, 0.5f);
                }
                cursorButton = navigationButtons[index];
                if (input.GetDown(1, PlayerAction.MoveLeft) || input.GetDown(1, PlayerAction.MoveRight))
                {
                    if (cursorButton == modeButton) ChangeMode(input.GetDown(1, PlayerAction.MoveLeft) ? -1 : 1);
                    else if (cursorButton == privacyButton) TogglePrivacy();
                }
                if (input.GetDown(1, PlayerAction.ActionA) || input.GetDown(1, PlayerAction.Start))
                {
                    if (cursorButton != leaveButton) PlayLobbySound(confirmSfx);
                    cursorButton.onClick.Invoke();
                }
                else if (input.GetDown(1, PlayerAction.ActionB)) LeaveLobby();
            }
            foreach (var button in navigationButtons)
                button.GetComponent<Image>().color = button == cursorButton
                    ? new Color32(93, 65, 149, 255) : new Color32(30, 22, 61, 255);
        }

        void PlayLobbySound(AudioClip clip) => GameMusicController.Instance?.PlaySfx(clip, 0.5f);

        void LeaveLobby()
        {
            PlayLobbySound(returnSfx);
            if (NetworkClient.active || NetworkServer.active) BombermanNetworkManager.LeaveRoomToBrowser();
            else BombermanNetworkManager.LeaveRoom();
        }

        void ChangeMode(int delta)
        {
            selectedMode = (OnlineRoomMode)(((int)selectedMode + delta + 3) % 3);
            PlayLobbySound(moveCursorSfx);
        }

        void TogglePrivacy()
        {
            privateRoom = !privateRoom;
            PlayLobbySound(moveCursorSfx);
            if (privateRoom && string.IsNullOrEmpty(creationPassword)) OpenPassword(null, false);
        }

        void CreateSelectedRoom()
        {
            if (browser == null || !browser.CanUseOnline) return;
            if (!HasPlayerName()) return;
            if (privateRoom && string.IsNullOrEmpty(creationPassword)) { OpenPassword(null, true); return; }
            browser.CreateRoom(selectedMode, privateRoom ? creationPassword : "");
        }

        bool HasPlayerName()
        {
            nameRequired = !OnlineVersionAuthenticator.ValidPlayerName(OnlineVersionAuthenticator.PlayerName);
            if (nameRequired) cursorButton = nameButton;
            return !nameRequired;
        }

        void RunUpdater()
        {
#if UNITY_EDITOR
            Debug.Log("[Online] Run GameUpdater.exe from the built Windows client.");
#else
            string path = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "GameUpdater.exe"));
            if (!System.IO.File.Exists(path)) { Debug.LogWarning("[Online] GameUpdater.exe missing: " + path); return; }
            try
            {
                using var process = System.Diagnostics.Process.GetCurrentProcess();
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path, "--language " + (int)SaveSystem.GetLanguage() + " --wait-for-game " + process.Id)
                    { UseShellExecute = true, WorkingDirectory = System.IO.Path.GetDirectoryName(path) });
                Application.Quit();
            }
            catch (System.ComponentModel.Win32Exception error) { Debug.LogError("[Online] Could not start updater: " + error.Message); }
#endif
        }

        void OpenName()
        {
            invalidName = false;
            editedName = OnlineVersionAuthenticator.PlayerName;
            nameCursor = Mathf.Min(editedName.Length, NameLength - 1);
            nameRoot.SetActive(true); nameRoot.transform.SetAsLastSibling();
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current != null)
            {
                UnityEngine.InputSystem.Keyboard.current.onTextInput -= TypeName;
                UnityEngine.InputSystem.Keyboard.current.onTextInput += TypeName;
            }
#endif
        }

        void TypeName(char c)
        {
            if (nameRoot == null || !nameRoot.activeSelf) return;
            nameTextFrame = Time.frameCount; invalidName = false;
            if (c == '\b' && editedName.Length > 0) editedName = editedName[..^1];
            else if (editedName.Length < NameLength && (char.IsLetterOrDigit(c) || c is ' ' or '_' or '-')) editedName += c;
            nameCursor = Mathf.Min(editedName.Length, NameLength - 1);
        }

        void CloseName()
        {
            nameRoot.SetActive(false);
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current != null)
                UnityEngine.InputSystem.Keyboard.current.onTextInput -= TypeName;
#endif
        }

        void UpdateName()
        {
            var input = PlayerInputManager.Instance;
            if (input == null) return;
            bool buttons = nameTextFrame != Time.frameCount;
            if (buttons && input.GetDown(1, PlayerAction.MoveLeft)) nameCursor = (nameCursor + NameLength - 1) % NameLength;
            if (buttons && input.GetDown(1, PlayerAction.MoveRight)) nameCursor = (nameCursor + 1) % NameLength;
            int delta = !buttons ? 0 : input.GetDown(1, PlayerAction.MoveUp) ? 1 : input.GetDown(1, PlayerAction.MoveDown) ? -1 : 0;
            if (delta != 0)
            {
                var characters = editedName.PadRight(NameLength).ToCharArray();
                int current = Mathf.Max(0, NameCharacters.IndexOf(char.ToUpperInvariant(characters[nameCursor])));
                characters[nameCursor] = NameCharacters[(current + delta + NameCharacters.Length) % NameCharacters.Length];
                editedName = new string(characters).TrimEnd(); invalidName = false;
                PlayLobbySound(moveCursorSfx);
            }
            nameTitle.text = invalidName ? GameTextDatabase.Online.NameUnavailable : GameTextDatabase.Online.EnterName;
            nameTitle.fontSize = 6; nameTitle.textWrappingMode = TextWrappingModes.Normal;
            var display = new StringBuilder();
            string padded = editedName.PadRight(NameLength);
            for (int i = 0; i < NameLength; i++)
                display.Append(i == nameCursor ? "<color=#C080FF><u>" + (padded[i] == ' ' ? '_' : padded[i]) + "</u></color>" : padded[i].ToString());
            nameText.text = display.ToString();
            nameHints.text = GameTextDatabase.Online.NameHint;
            bool enter = false;
#if ENABLE_INPUT_SYSTEM
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            enter = keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame);
#endif
            bool cancel = input.GetDown(1, PlayerAction.ActionB);
#if ENABLE_INPUT_SYSTEM
            cancel |= keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
#endif
            if (cancel) { CloseName(); PlayLobbySound(returnSfx); return; }
            if (!enter && !(buttons && (input.GetDown(1, PlayerAction.ActionA) || input.GetDown(1, PlayerAction.Start)))) return;
            if (!OnlineVersionAuthenticator.ValidPlayerName(editedName)) { invalidName = true; return; }
            OnlineVersionAuthenticator.PlayerName = editedName;
            CloseName(); PlayLobbySound(confirmSfx);
            nameRequired = false; OnlineVersionAuthenticator.ClearNameRejection();
        }

        void OpenPassword(OnlineListedRoom room, bool create)
        {
            passwordRoom = room; createAfterPassword = create; passwordDigit = 0;
            System.Array.Clear(passwordDigits, 0, passwordDigits.Length);
            passwordOpen = true;
            passwordRoot.SetActive(true); passwordRoot.transform.SetAsLastSibling();
        }

        void UpdatePassword()
        {
            var input = PlayerInputManager.Instance;
            if (input == null) return;
            if (input.GetDown(1, PlayerAction.MoveLeft)) { passwordDigit = (passwordDigit + 5) % 6; PlayLobbySound(moveCursorSfx); }
            if (input.GetDown(1, PlayerAction.MoveRight)) { passwordDigit = (passwordDigit + 1) % 6; PlayLobbySound(moveCursorSfx); }
            if (input.GetDown(1, PlayerAction.MoveUp)) { passwordDigits[passwordDigit] = (passwordDigits[passwordDigit] + 1) % 10; PlayLobbySound(moveCursorSfx); }
            if (input.GetDown(1, PlayerAction.MoveDown)) { passwordDigits[passwordDigit] = (passwordDigits[passwordDigit] + 9) % 10; PlayLobbySound(moveCursorSfx); }
            var digits = new StringBuilder();
            for (int i = 0; i < 6; i++)
                digits.Append(i == passwordDigit ? "<color=#C080FF>[" + passwordDigits[i] + "]</color>" : " * ");
            passwordTitle.text = GameTextDatabase.Online.Password;
            passwordText.text = digits.ToString();
            passwordHints.text = GameTextDatabase.Online.PasswordHint;
            if (input.GetDown(1, PlayerAction.ActionB))
            {
                PlayLobbySound(returnSfx);
                if (passwordRoom == null && string.IsNullOrEmpty(creationPassword)) privateRoom = false;
                passwordOpen = false; passwordRoot.SetActive(false); return;
            }
            if (!input.GetDown(1, PlayerAction.ActionA) && !input.GetDown(1, PlayerAction.Start)) return;
            PlayLobbySound(confirmSfx);
            string password = string.Concat(passwordDigits);
            passwordOpen = false; passwordRoot.SetActive(false);
            if (passwordRoom != null) browser.JoinRoom(passwordRoom, password);
            else { creationPassword = password; if (createAfterPassword) CreateSelectedRoom(); }
            System.Array.Clear(passwordDigits, 0, passwordDigits.Length);
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

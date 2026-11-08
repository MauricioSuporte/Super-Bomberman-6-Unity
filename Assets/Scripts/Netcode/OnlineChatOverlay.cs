using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Assets.Scripts.Netcode
{
    // Owned by the existing persistent network manager, including before room connection.
    [DefaultExecutionOrder(-20000)]
    public sealed class OnlineChatOverlay : MonoBehaviour
    {
        [Serializable] public sealed class DirectoryLine { public int id, colorIndex; public string sender, text, nonce; }
        [Serializable] sealed class DirectoryReply { public string token = ""; public DirectoryLine[] messages = Array.Empty<DirectoryLine>(); public DirectoryLine message = null; public int colorIndex = 0; }
        [Serializable] sealed class DirectorySend
        {
            public int protocol = OnlineVersionAuthenticator.ProtocolVersion;
            public string buildIdentity, name, token, text, nonce;
        }
        sealed class Line
        {
            public string sender, text, nonce;
            public float received;
            public bool pending, failed, room;
            public int colorIndex;
            public byte presence;
            public bool you;
        }
        readonly List<Line> roomLines = new(), publicLines = new();
        readonly Queue<Line> outgoing = new();
        RectTransform safe, panel, backgroundRoot;
        GameObject canvasRoot;
        readonly List<Image> textBackgrounds = new();
        AudioSource localSfx;
        AudioClip joinSfx, leaveSfx;
        bool departing, viewHandedOff, leftNotified;
        int ownPlayerId;
        float expiresAt, lastJoinSound = -100, lastLeaveSound = -100;
        CanvasGroup group;
        UICameraViewportFitter viewport;
        TextMeshProUGUI history, contrastNames, entry, status;
        TMP_FontAsset font;
        OnlineRoomBrowser browser;
        OnlineLobbyMenu lobby;
        Keyboard keyboard;
        TouchScreenKeyboard touchKeyboard;
        public static bool CanOpenTouch => active != null && active.Available && !active.ModalOpen && !active.departing;
        public static void OpenTouchChat() { if (CanOpenTouch && !active.typing) active.Open(); }
        string draft = "", token = "", publicName = "", publicUrl = "";
        bool typing, roomContext, contextInitialized, sending, directoryReady, releaseKeys;
        int consumedFrame = -1, lastId, scroll, previousKeys, publicColorIndex;
        float nextSend, failureUntil;
        static OnlineChatOverlay active;
        public static bool BlocksLocalInput => active != null && (active.typing || active.releaseKeys ||
            active.consumedFrame == Time.frameCount || (Keyboard.current != null && Keyboard.current.tKey.isPressed &&
            (active.previousKeys & 1) == 0 && active.Available && !active.ModalOpen));
        bool ModalOpen => SceneManager.GetActiveScene().name == NetworkLobby.SceneName && lobby != null && lobby.ChatModalOpen;
        bool InRoom => NetworkClient.isConnected && NetworkClient.connection != null && NetworkClient.connection.isAuthenticated;
        bool Browsing => !NetworkClient.active && !NetworkServer.active && SceneManager.GetActiveScene().name == NetworkLobby.SceneName;
        bool Available => departing || InRoom || (Browsing && browser != null && browser.CanUseOnline && browser.DirectoryConfigured &&
            OnlineVersionAuthenticator.ValidPlayerName(OnlineVersionAuthenticator.PlayerName));
        List<Line> Lines => roomContext ? roomLines : publicLines;

        public void SetFont(TMP_FontAsset value) => font = value;

        void Start()
        {
            if (departing) return;
            active = this;
            browser = GetComponent<OnlineRoomBrowser>();
            lobby = GetComponent<OnlineLobbyMenu>();
            CreateView();
            foreach (var previous in FindObjectsByType<OnlineChatOverlay>())
                if (previous != this && previous.departing)
                {
                    foreach (var line in previous.roomLines) if (line.presence != 0) Append(publicLines, line);
                    previous.gameObject.SetActive(false);
                    Destroy(previous.gameObject);
                }
            StartCoroutine(PollDirectory());
        }

        void Update()
        {
            if (departing)
            {
                if (Time.unscaledTime >= expiresAt) Destroy(gameObject);
                return;
            }
            RefreshInput();
            if (typing && touchKeyboard != null)
            {
                draft = touchKeyboard.text;
                RefreshEntry();
                if (touchKeyboard.status == TouchScreenKeyboard.Status.Done) Submit();
                else if (touchKeyboard.status is TouchScreenKeyboard.Status.Canceled or TouchScreenKeyboard.Status.LostFocus) Close();
            }
            PumpOutgoing();
        }

        void AfterInputUpdate()
        {
            if (!departing && Application.isPlaying && isActiveAndEnabled && InputState.currentUpdateType != InputUpdateType.Editor) RefreshInput();
        }

        void RefreshInput()
        {
            if (keyboard != Keyboard.current)
            {
                if (keyboard != null) keyboard.onTextInput -= Type;
                keyboard = Keyboard.current;
                previousKeys = 0;
                if (keyboard != null) keyboard.onTextInput += Type;
            }
            if (!AnyKeyboardKeyHeld()) releaseKeys = false;
            // Track transitions locally: manual updates and newly connected keyboards
            // do not always expose the same wasPressedThisFrame update counter.
            int keys = keyboard == null ? 0 :
                (keyboard.tKey.isPressed ? 1 : 0) |
                (keyboard.enterKey.isPressed || keyboard.numpadEnterKey.isPressed ? 2 : 0) |
                (keyboard.escapeKey.isPressed ? 4 : 0) |
                (keyboard.pageUpKey.isPressed ? 8 : 0) |
                (keyboard.pageDownKey.isPressed ? 16 : 0);
            int pressed = keys & ~previousKeys;
            previousKeys = keys;
            bool currentRoom = InRoom;
            if (!contextInitialized || currentRoom != roomContext)
            {
                Close();
                // Forget the previous room on disconnect. Public history is a separate channel.
                roomLines.Clear();
                outgoing.Clear();
                foreach (var line in publicLines) if (line.pending) { line.pending = false; line.failed = true; }
                roomContext = currentRoom;
                contextInitialized = true;
                scroll = 0;
            }
            if (!Available || ModalOpen || (!Application.isFocused && !Application.isBatchMode)) Close();
            else if (keyboard != null)
            {
                if (!typing && (pressed & 1) != 0)
                    Open();
                else if (typing)
                {
                    if ((pressed & 4) != 0) Close();
                    else if ((pressed & 2) != 0) Submit();
                }
                if (typing)
                {
                    if ((pressed & 8) != 0) scroll = Mathf.Min(Lines.Count - 1, scroll + 3);
                    if ((pressed & 16) != 0) scroll = Mathf.Max(0, scroll - 3);
                }
            }
        }

        void Open()
        {
            typing = true;
            draft = "";
            if (Application.isMobilePlatform && TouchScreenKeyboard.isSupported)
                touchKeyboard = TouchScreenKeyboard.Open("", TouchScreenKeyboardType.Default, false, false, false, false, "", OnlineChatRules.MaxLength);
            scroll = 0;
            RefreshEntry();
        }

        void Type(char c)
        {
            // The opening T arrives before MonoBehaviour.Update. Consume only that
            // character, preserving all subsequent text events in the same frame.
            if (touchKeyboard != null) return;
            if (!typing)
            {
                if ((c == 't' || c == 'T') && Available && !ModalOpen && !releaseKeys) Open();
                return;
            }
            if (c == '\b')
            {
                if (draft.Length > 0)
                {
                    int count = draft.Length > 1 && char.IsLowSurrogate(draft[^1]) && char.IsHighSurrogate(draft[^2]) ? 2 : 1;
                    draft = draft[..^count];
                }
            }
            else if (!char.IsControl(c) && draft.Length < OnlineChatRules.MaxLength) draft += c;
            RefreshEntry();
        }

        bool AnyKeyboardKeyHeld()
        {
            if (keyboard == null || !keyboard.added) return false;
            foreach (var key in keyboard.allKeys) if (key.isPressed) return true;
            return false;
        }

        void Close()
        {
            if (typing) { consumedFrame = Time.frameCount; releaseKeys = AnyKeyboardKeyHeld(); }
            typing = false;
            if (touchKeyboard != null) touchKeyboard.active = false;
            touchKeyboard = null;
            draft = "";
        }

        void Submit()
        {
            string text = OnlineChatRules.Normalize(draft);
            if (text.Length == 0) { Close(); return; }
            if (outgoing.Count >= OnlineChatRules.HistoryLimit) return;
            var line = new Line { sender = OnlineVersionAuthenticator.PlayerName, text = text,
                colorIndex = roomContext ? BombermanNetworkManager.LocalRoomPlayerId : publicColorIndex,
                nonce = Guid.NewGuid().ToString("N"), received = Time.unscaledTime, pending = true, room = roomContext };
            Append(Lines, line);
            outgoing.Enqueue(line);
            Close();
            PumpOutgoing();
        }

        void PumpOutgoing()
        {
            foreach (var line in Lines)
                if (line.pending && Time.unscaledTime - line.received > 10)
                    Fail(line);
            if (sending || outgoing.Count == 0 || !Available || Time.unscaledTime < nextSend) return;
            if (!roomContext && (!directoryReady || token.Length == 0)) return;
            if (roomContext && BombermanNetworkManager.LocalRoomPlayerId <= 0) return;
            var pending = outgoing.Dequeue();
            if (!pending.pending || pending.room != roomContext) { Fail(pending); return; }
            pending.colorIndex = roomContext ? BombermanNetworkManager.LocalRoomPlayerId : publicColorIndex;
            nextSend = Time.unscaledTime + OnlineChatRules.SendInterval + 0.05f;
            if (roomContext) NetworkClient.Send(new OnlineChatSendMessage { text = pending.text, nonce = pending.nonce });
            else StartCoroutine(SendDirectory(pending));
        }

        void Fail(Line line)
        {
            line.pending = false;
            line.failed = true;
            failureUntil = Time.unscaledTime + 4;
        }

        public void ReceivePresence(string sender, int playerId, bool joined, bool you)
        {
            if (!OnlineVersionAuthenticator.ValidPlayerName(sender) || playerId < 1 || playerId > 6) return;
            if (you && joined)
            {
                ownPlayerId = playerId;
                leftNotified = false;
                if (viewHandedOff) { viewHandedOff = false; CreateView(); active = this; }
            }
            if (you && !joined && leftNotified) return;
            if (!roomContext) { roomLines.Clear(); roomContext = contextInitialized = true; Close(); }
            if (you && !joined) leftNotified = true;
            Append(roomLines, new Line { sender = sender, colorIndex = playerId, presence = joined ? (byte)1 : (byte)2,
                you = you, received = Time.unscaledTime });
            PlayPresenceSound(joined);
        }

        void PlayPresenceSound(bool joined)
        {
            if (Time.unscaledTime - (joined ? lastJoinSound : lastLeaveSound) < 0.08f) return;
            if (joined) lastJoinSound = Time.unscaledTime; else lastLeaveSound = Time.unscaledTime;
            if (joinSfx == null) joinSfx = Resources.Load<AudioClip>("Sounds/OnlineChat/player_join");
            if (leaveSfx == null) leaveSfx = Resources.Load<AudioClip>("Sounds/OnlineChat/player_leave");
            var clip = joined ? joinSfx : leaveSfx;
            if (GameMusicController.Instance != null) GameMusicController.Instance.PlayLocalSfxIgnoringListenerPause(clip);
            else if (localSfx != null && clip != null) localSfx.PlayOneShot(clip, GameAudioSettings.ApplySfxVolume(1));
        }

        // Retain the existing chat view briefly while its network manager changes scene.
        // This is an expiring presentation object, not another network/session owner.
        public void PrepareLocalDeparture()
        {
            if (departing || viewHandedOff || panel == null || (!roomContext && ownPlayerId <= 0)) return;
            int id = ownPlayerId > 0 ? ownPlayerId : BombermanNetworkManager.LocalRoomPlayerId;
            if (id <= 0) return;
            if (!leftNotified)
            {
                string name = BombermanNetworkManager.RoomPlayerName(id);
                if (!OnlineVersionAuthenticator.ValidPlayerName(name)) name = OnlineVersionAuthenticator.PlayerName;
                ReceivePresence(name, id, false, true);
            }
            Close();
            var root = new GameObject("OnlineChatDeparture");
            DontDestroyOnLoad(root);
            var view = root.AddComponent<OnlineChatOverlay>();
            view.departing = true; view.expiresAt = Time.unscaledTime + 5.4f;
            view.roomContext = view.contextInitialized = true;
            view.canvasRoot = canvasRoot; view.safe = safe; view.panel = panel; view.backgroundRoot = backgroundRoot;
            view.viewport = viewport; view.group = group; view.history = history; view.contrastNames = contrastNames;
            view.entry = entry; view.status = status; view.localSfx = localSfx;
            view.textBackgrounds.AddRange(textBackgrounds);
            view.roomLines.AddRange(roomLines);
            canvasRoot.transform.SetParent(root.transform, false);
            viewHandedOff = true; panel = null; canvasRoot = null;
            if (active == view || active == this) active = null;
        }

        string Format(Line line, bool contrastOnly = false)
        {
            if (line.presence == 0) return OnlineChatRules.FormatLine(line.sender, line.text, line.colorIndex, contrastOnly);
            string color = contrastOnly ? "FFFFFF00" : line.presence == 1 ? "69FF73" : "FFBB78";
            string nameColor = OnlineChatRules.NameColor(line.colorIndex, line.sender);
            if (contrastOnly) nameColor += line.colorIndex is 2 or 4 ? "FF" : "00";
            string name = "<color=#" + nameColor + ">" + OnlineChatRules.EscapeMarkup(line.sender) + "</color>";
            return "<color=#" + color + ">" + GameTextDatabase.Online.ChatPresence(name, line.presence == 1, line.you) + "</color>";
        }

        public void ReceiveRoomLine(string sender, string text, string nonce, int playerId)
        {
            if (!InRoom) return;
            if (!roomContext) { roomLines.Clear(); roomContext = contextInitialized = true; Close(); }
            Add(roomLines, sender, text, nonce, playerId, false);
        }

        static void Append(List<Line> lines, Line line)
        {
            if (lines.Count == OnlineChatRules.HistoryLimit) lines.RemoveAt(0);
            lines.Add(line);
        }

        static void Add(List<Line> lines, string sender, string text, string nonce, int colorIndex, bool historical)
        {
            text = OnlineChatRules.Normalize(text);
            if (text.Length == 0 || !OnlineVersionAuthenticator.ValidPlayerName(sender)) return;
            if (!string.IsNullOrEmpty(nonce))
                foreach (var line in lines)
                    if (line.nonce == nonce && line.sender == sender)
                    { line.pending = line.failed = false; line.colorIndex = colorIndex; return; }
            Append(lines, new Line { sender = sender, text = text, nonce = nonce, colorIndex = colorIndex,
                received = historical ? -100 : Time.unscaledTime });
        }

        IEnumerator PollDirectory()
        {
            while (true)
            {
                if (Browsing && Available)
                {
                    string name = OnlineVersionAuthenticator.PlayerName;
                    string url = browser.DirectoryUrl;
                    if (name != publicName || url != publicUrl)
                    {
                        publicName = name; publicUrl = url; token = ""; lastId = 0;
                        directoryReady = false; publicLines.RemoveAll(line => line.presence == 0);
                    }
                    if (token.Length == 0)
                    {
                        using var join = JsonPost(url + "/chat/session", new DirectorySend
                        { name = name, buildIdentity = OnlineVersionAuthenticator.ConnectionBuildIdentity });
                        yield return join.SendWebRequest();
                        if (Browsing && name == OnlineVersionAuthenticator.PlayerName && join.result == UnityWebRequest.Result.Success)
                        {
                            var reply = Parse(join);
                            token = reply?.token ?? "";
                            publicColorIndex = reply?.colorIndex ?? 0;
                            foreach (var line in publicLines) if (line.pending) line.colorIndex = publicColorIndex;
                            Apply(reply, true);
                            directoryReady = token.Length > 0;
                        }
                    }
                    else
                    {
                        using var poll = UnityWebRequest.Get(url + "/chat?after=" + lastId);
                        poll.SetRequestHeader("X-Chat-Token", token); poll.timeout = 5;
                        yield return poll.SendWebRequest();
                        directoryReady = poll.result == UnityWebRequest.Result.Success;
                        if (poll.responseCode == 401) { token = ""; lastId = 0; publicLines.Clear(); }
                        if (directoryReady && Browsing) Apply(Parse(poll), false);
                    }
                }
                else directoryReady = false;
                yield return new WaitForSecondsRealtime(0.2f);
            }
        }

        IEnumerator SendDirectory(Line line)
        {
            sending = true;
            string sendingToken = token;
            using var request = JsonPost(publicUrl + "/chat", new DirectorySend { token = token, text = line.text, nonce = line.nonce,
                buildIdentity = OnlineVersionAuthenticator.ConnectionBuildIdentity });
            yield return request.SendWebRequest();
            sending = false;
            if (sendingToken != token || !Browsing) { Fail(line); yield break; }
            if (request.result == UnityWebRequest.Result.Success)
            {
                var reply = Parse(request);
                if (reply?.message != null) Add(publicLines, reply.message.sender, reply.message.text, reply.message.nonce, reply.message.colorIndex, false);
                else Fail(line);
            }
            else if (request.responseCode == 429 || request.responseCode == 401)
            {
                outgoing.Enqueue(line);
                nextSend = Time.unscaledTime + 0.4f;
                if (request.responseCode == 401) { token = ""; directoryReady = false; }
            }
            else Fail(line);
        }

        static UnityWebRequest JsonPost(string url, DirectorySend value)
        {
            var request = new UnityWebRequest(url, "POST")
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(value))),
                downloadHandler = new DownloadHandlerBuffer(), timeout = 5
            };
            request.SetRequestHeader("Content-Type", "application/json");
            return request;
        }

        static DirectoryReply Parse(UnityWebRequest request)
        {
            try { return JsonUtility.FromJson<DirectoryReply>(request.downloadHandler.text); }
            catch (ArgumentException) { return null; }
        }

        void Apply(DirectoryReply reply, bool historical)
        {
            if (reply?.messages == null) return;
            foreach (var line in reply.messages)
                if (line != null && line.id > lastId)
                { lastId = line.id; Add(publicLines, line.sender, line.text, line.nonce, line.colorIndex, historical); }
        }

        void CreateView()
        {
            var canvasObject = new GameObject("OnlineChatCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasRoot = canvasObject;
            canvasObject.transform.SetParent(transform, false);
            localSfx = canvasObject.AddComponent<AudioSource>();
            localSfx.playOnAwake = false; localSfx.spatialBlend = 0; localSfx.ignoreListenerPause = true;
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 300; canvas.pixelPerfect = true;
            canvasObject.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            safe = Rect("SafeFrame", canvasObject.transform);
            viewport = safe.gameObject.AddComponent<UICameraViewportFitter>();
            panel = Rect("Chat", safe);
            panel.anchorMin = panel.anchorMax = panel.pivot = Vector2.zero;
            panel.sizeDelta = new Vector2(200, 88);
            backgroundRoot = Rect("TextBackgrounds", panel);
            backgroundRoot.anchorMin = backgroundRoot.anchorMax = backgroundRoot.pivot = Vector2.zero;
            backgroundRoot.anchoredPosition = Vector2.zero; backgroundRoot.sizeDelta = panel.sizeDelta;
            group = panel.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = group.interactable = false;
            history = Label("History", panel, 0, 23, 200, 65, 6);
            history.richText = true;
            history.verticalAlignment = VerticalAlignmentOptions.Bottom;
            contrastNames = Label("NameContrast", panel, 0, 23, 200, 65, 6);
            contrastNames.richText = true;
            contrastNames.verticalAlignment = VerticalAlignmentOptions.Bottom;
            contrastNames.outlineColor = Color.white;
            entry = Label("Entry", panel, 0, 0, 200, 14, 6);
            entry.color = new Color32(175, 255, 175, 255);
            status = Label("Status", panel, 0, 14, 200, 8, 4);
            status.color = new Color32(255, 150, 150, 255);
        }

        static RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); return rect;
        }

        TextMeshProUGUI Label(string name, Transform parent, float x, float y, float width, float height, int size)
        {
            var rect = Rect(name, parent);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(x, y); rect.sizeDelta = new Vector2(width, height);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font != null ? font : TMP_Settings.defaultFontAsset;
            text.text = "";
            text.fontStyle = FontStyles.Bold;
            text.outlineWidth = 0.2f; text.outlineColor = Color.black;
            text.fontSize = size; text.color = Color.white; text.richText = false; text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal; text.overflowMode = TextOverflowModes.Truncate;
            LocalizedTmpFontFallback.Apply(text, true);
            return text;
        }

        void LateUpdate()
        {
            if (panel == null) return;
            viewport.ForceApplyNow();
            Vector3 scale = PixelPerfectViewport.ResolveUiScale(safe.rect.size, global::SaveSystem.GetVideoSettings().displayMode);
            panel.localScale = scale;
            panel.anchoredPosition = new Vector2(4 * scale.x, 4 * scale.y);
            bool expanded = typing;
            float newest = Lines.Count > 0 ? Lines[^1].received : -100;
            bool visible = Available && !ModalOpen && (expanded || Time.unscaledTime - newest < 5.4f);
            panel.gameObject.SetActive(visible);
            if (!visible) return;
            group.alpha = expanded ? 1 : Mathf.Clamp01((5.4f - (Time.unscaledTime - newest)) / 0.4f);
            RefreshEntry();
            status.gameObject.SetActive(Time.unscaledTime < failureUntil);
            status.text = GameTextDatabase.Online.ChatUnavailable;
            scroll = Mathf.Clamp(scroll, 0, Mathf.Max(0, Lines.Count - 1));
            string content = "", contrast = "";
            for (int i = Lines.Count - 1 - (expanded ? scroll : 0); i >= 0; i--)
            {
                var line = Lines[i];
                if (!expanded && Time.unscaledTime - line.received >= 5.4f) break;
                string candidate = Format(line) + (line.failed ? " <color=#FF7777>!</color>" : "") + (content.Length > 0 ? "\n" + content : "");
                if (history.GetPreferredValues(candidate, 200, float.PositiveInfinity).y > 65 && content.Length > 0) break;
                content = candidate;
                contrast = Format(line, true) +
                    (line.failed ? " <color=#FFFFFF00>!</color>" : "") + (contrast.Length > 0 ? "\n" + contrast : "");
            }
            history.text = content;
            contrastNames.text = contrast;
            LocalizedTmpFontFallback.Apply(history);
            LocalizedTmpFontFallback.Apply(entry);
            UpdateTextBackgrounds();
        }

        void UpdateTextBackgrounds()
        {
            int used = 0;
            AddTextBackgrounds(history, ref used);
            if (entry.gameObject.activeSelf) AddTextBackgrounds(entry, ref used);
            if (status.gameObject.activeSelf) AddTextBackgrounds(status, ref used);
            for (int i = used; i < textBackgrounds.Count; i++) textBackgrounds[i].gameObject.SetActive(false);
        }

        void AddTextBackgrounds(TMP_Text label, ref int used)
        {
            label.ForceMeshUpdate();
            for (int i = 0; i < label.textInfo.lineCount; i++)
            {
                var line = label.textInfo.lineInfo[i];
                if (line.visibleCharacterCount == 0) continue;
                if (used == textBackgrounds.Count)
                {
                    var rect = Rect("TextBand", backgroundRoot);
                    rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
                    var image = rect.gameObject.AddComponent<Image>();
                    image.color = new Color(0, 0, 0, 0.55f); image.raycastTarget = false;
                    textBackgrounds.Add(image);
                }
                var background = textBackgrounds[used++];
                background.gameObject.SetActive(true);
                var position = label.rectTransform.anchoredPosition;
                background.rectTransform.anchoredPosition = new Vector2(
                    Mathf.Floor(position.x + line.lineExtents.min.x - 1), Mathf.Floor(position.y + line.descender - 1));
                background.rectTransform.sizeDelta = new Vector2(
                    Mathf.Ceil(Mathf.Min(200, line.lineExtents.max.x - line.lineExtents.min.x) + 2),
                    Mathf.Ceil(line.ascender - line.descender + 2));
            }
        }

        void RefreshEntry()
        {
            if (entry == null) return;
            entry.gameObject.SetActive(typing);
            int start = Mathf.Max(0, draft.Length - 52);
            if (start > 0 && char.IsLowSurrogate(draft[start])) start++;
            entry.text = typing ? "> " + draft[start..] + (Mathf.Repeat(Time.unscaledTime, 1) < 0.5f ? "_" : "") : "";
        }

        void OnDisable()
        {
            InputSystem.onAfterUpdate -= AfterInputUpdate;
            Close();
            releaseKeys = false;
            if (keyboard != null) keyboard.onTextInput -= Type;
            keyboard = null;
            if (active == this) active = null;
        }

        void OnEnable()
        {
            active = this;
            InputSystem.onAfterUpdate += AfterInputUpdate;
        }
    }
}

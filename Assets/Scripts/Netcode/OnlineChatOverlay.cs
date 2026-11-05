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
        }
        readonly List<Line> roomLines = new(), publicLines = new();
        readonly Queue<Line> outgoing = new();
        RectTransform safe, panel;
        CanvasGroup group;
        UICameraViewportFitter viewport;
        TextMeshProUGUI history, contrastNames, entry, hint, status;
        TMP_FontAsset font;
        OnlineRoomBrowser browser;
        OnlineLobbyMenu lobby;
        Keyboard keyboard;
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
        bool Available => InRoom || (Browsing && browser != null && browser.CanUseOnline && browser.DirectoryConfigured &&
            OnlineVersionAuthenticator.ValidPlayerName(OnlineVersionAuthenticator.PlayerName));
        List<Line> Lines => roomContext ? roomLines : publicLines;

        public void SetFont(TMP_FontAsset value) => font = value;

        void Start()
        {
            active = this;
            browser = GetComponent<OnlineRoomBrowser>();
            lobby = GetComponent<OnlineLobbyMenu>();
            CreateView();
            StartCoroutine(PollDirectory());
        }

        void Update()
        {
            RefreshInput();
            PumpOutgoing();
        }

        void AfterInputUpdate()
        {
            if (Application.isPlaying && isActiveAndEnabled && InputState.currentUpdateType != InputUpdateType.Editor) RefreshInput();
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
            scroll = 0;
            RefreshEntry();
        }

        void Type(char c)
        {
            // The opening T arrives before MonoBehaviour.Update. Consume only that
            // character, preserving all subsequent text events in the same frame.
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
            if (keyboard == null) return false;
            foreach (var key in keyboard.allKeys) if (key.isPressed) return true;
            return false;
        }

        void Close()
        {
            if (typing) { consumedFrame = Time.frameCount; releaseKeys = AnyKeyboardKeyHeld(); }
            typing = false;
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
                        directoryReady = false; publicLines.Clear();
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
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 300; canvas.pixelPerfect = true;
            canvasObject.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            safe = Rect("SafeFrame", canvasObject.transform);
            viewport = safe.gameObject.AddComponent<UICameraViewportFitter>();
            panel = Rect("Chat", safe);
            panel.anchorMin = panel.anchorMax = panel.pivot = Vector2.zero;
            panel.sizeDelta = new Vector2(200, 88);
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
            hint = Label("Hint", safe, 4, 2, 248, 8, 5);
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
            float scale = Mathf.Max(1, Mathf.Floor(Mathf.Min(safe.rect.width / 256f, safe.rect.height / 224f)));
            panel.localScale = hint.rectTransform.localScale = new Vector3(scale, scale, 1);
            panel.anchoredPosition = new Vector2(4 * scale, 4 * scale);
            hint.rectTransform.anchoredPosition = new Vector2(4 * scale, 2 * scale);
            bool expanded = typing;
            float newest = Lines.Count > 0 ? Lines[^1].received : -100;
            bool visible = Available && !ModalOpen && (expanded || Time.unscaledTime - newest < 5.4f);
            panel.gameObject.SetActive(visible);
            hint.gameObject.SetActive(Available && !typing && !visible && !ModalOpen);
            hint.text = GameTextDatabase.Online.ChatHint;
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
                string candidate = OnlineChatRules.FormatLine(line.sender, line.text, line.colorIndex) + (line.failed ? " <color=#FF7777>!</color>" : "") + (content.Length > 0 ? "\n" + content : "");
                if (history.GetPreferredValues(candidate, 200, float.PositiveInfinity).y > 65 && content.Length > 0) break;
                content = candidate;
                contrast = OnlineChatRules.FormatLine(line.sender, line.text, line.colorIndex, true) +
                    (line.failed ? " <color=#FFFFFF00>!</color>" : "") + (contrast.Length > 0 ? "\n" + contrast : "");
            }
            history.text = content;
            contrastNames.text = contrast;
            LocalizedTmpFontFallback.Apply(history);
            LocalizedTmpFontFallback.Apply(entry); LocalizedTmpFontFallback.Apply(hint);
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

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Assets.Scripts.Netcode
{
    // Owned by the existing persistent network manager, including before room connection.
    [DefaultExecutionOrder(-20000)]
    public sealed class OnlineChatOverlay : MonoBehaviour
    {
        [Serializable] public sealed class DirectoryLine { public int id; public string sender, text; }
        [Serializable] sealed class DirectoryReply { public string token = ""; public DirectoryLine[] messages = Array.Empty<DirectoryLine>(); }
        [Serializable] sealed class DirectorySend
        {
            public int protocol = OnlineVersionAuthenticator.ProtocolVersion;
            public string buildIdentity, name, token, text;
        }
        sealed class Line { public string sender, text; public float received; }
        readonly List<Line> roomLines = new(), publicLines = new();
        RectTransform safe, panel;
        CanvasGroup group;
        UICameraViewportFitter viewport;
        TextMeshProUGUI heading, history, entry, hint;
        TMP_FontAsset font;
        OnlineRoomBrowser browser;
        OnlineLobbyMenu lobby;
        Keyboard keyboard;
        string draft = "", token = "", publicName = "", publicUrl = "";
        bool typing, roomContext, contextInitialized, sending, directoryReady;
        int consumedFrame = -1, openedFrame = -1, lastId, scroll, previousKeys;
        float nextSend, noticeUntil;
        static OnlineChatOverlay active;
        public static bool BlocksLocalInput => active != null && (active.typing || active.consumedFrame == Time.frameCount);
        public static bool ReservesHistoryKey => active != null && active.Available && !active.ModalOpen;
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
            if (keyboard != Keyboard.current)
            {
                if (keyboard != null) keyboard.onTextInput -= Type;
                keyboard = Keyboard.current;
                previousKeys = 0;
                if (keyboard != null) keyboard.onTextInput += Type;
            }
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
                roomContext = currentRoom;
                contextInitialized = true;
                scroll = 0;
            }
            if (!Available || ModalOpen || (!Application.isFocused && !Application.isBatchMode)) Close();
            else if (keyboard != null)
            {
                if (!typing && (pressed & 1) != 0)
                { typing = true; openedFrame = Time.frameCount; draft = ""; scroll = 0; }
                else if (typing)
                {
                    if ((pressed & 4) != 0) Close();
                    else if ((pressed & 2) != 0) Submit();
                }
                if (typing || keyboard.zKey.isPressed)
                {
                    if ((pressed & 8) != 0) scroll = Mathf.Min(Lines.Count - 1, scroll + 3);
                    if ((pressed & 16) != 0) scroll = Mathf.Max(0, scroll - 3);
                }
            }
        }

        void Type(char c)
        {
            if (!typing || openedFrame == Time.frameCount) return;
            if (c == '\b')
            {
                if (draft.Length > 0)
                {
                    int count = draft.Length > 1 && char.IsLowSurrogate(draft[^1]) && char.IsHighSurrogate(draft[^2]) ? 2 : 1;
                    draft = draft[..^count];
                }
            }
            else if (!char.IsControl(c) && draft.Length < OnlineChatRules.MaxLength) draft += c;
        }

        void Close()
        {
            if (typing) consumedFrame = Time.frameCount;
            typing = false;
            draft = "";
        }

        void Submit()
        {
            string text = OnlineChatRules.Normalize(draft);
            if (text.Length == 0) { Close(); return; }
            if (sending || Time.unscaledTime < nextSend || (!roomContext && !directoryReady))
            { noticeUntil = Time.unscaledTime + 3; return; }
            nextSend = Time.unscaledTime + OnlineChatRules.SendInterval;
            if (roomContext)
            {
                NetworkClient.Send(new OnlineChatSendMessage { text = text });
                Close();
            }
            else StartCoroutine(SendDirectory(text));
        }

        public void ReceiveRoomLine(string sender, string text)
        {
            if (!InRoom) return;
            // Network callbacks can precede our first Update after authentication.
            if (!roomContext) { roomLines.Clear(); roomContext = contextInitialized = true; Close(); }
            Add(roomLines, sender, text, false);
        }

        static void Add(List<Line> lines, string sender, string text, bool historical)
        {
            text = OnlineChatRules.Normalize(text);
            if (text.Length == 0 || !OnlineVersionAuthenticator.ValidPlayerName(sender)) return;
            if (lines.Count == OnlineChatRules.HistoryLimit) lines.RemoveAt(0);
            lines.Add(new Line { sender = sender, text = text, received = historical ? -100 : Time.unscaledTime });
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
                yield return new WaitForSecondsRealtime(1);
            }
        }

        IEnumerator SendDirectory(string text)
        {
            sending = true;
            string sendingToken = token;
            using var request = JsonPost(publicUrl + "/chat", new DirectorySend { token = token, text = text,
                buildIdentity = OnlineVersionAuthenticator.ConnectionBuildIdentity });
            yield return request.SendWebRequest();
            sending = false;
            if (sendingToken != token || !Browsing) yield break;
            if (request.result == UnityWebRequest.Result.Success) Close();
            else noticeUntil = Time.unscaledTime + 3;
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
                { lastId = line.id; Add(publicLines, line.sender, line.text, historical); }
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
            panel.gameObject.AddComponent<Image>().color = new Color(0.02f, 0.03f, 0.09f, 0.82f);
            heading = Label("Channel", panel, 4, 76, 192, 8, 5);
            heading.color = new Color(1, 0.88f, 0.38f);
            history = Label("History", panel, 4, 19, 192, 55, 5);
            history.verticalAlignment = VerticalAlignmentOptions.Bottom;
            entry = Label("Entry", panel, 4, 4, 192, 12, 5);
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
            panel.anchoredPosition = new Vector2(4 * scale, 13 * scale);
            hint.rectTransform.anchoredPosition = new Vector2(4 * scale, 2 * scale);
            bool held = keyboard != null && keyboard.zKey.isPressed && !ModalOpen;
            bool expanded = typing || held;
            float newest = Lines.Count > 0 ? Lines[^1].received : -100;
            bool visible = Available && !ModalOpen && (expanded || Time.unscaledTime - newest < 5.4f);
            panel.gameObject.SetActive(visible);
            hint.gameObject.SetActive(Available && Browsing && !ModalOpen);
            hint.text = GameTextDatabase.Online.ChatHint;
            if (!visible) return;
            group.alpha = expanded ? 1 : Mathf.Clamp01((5.4f - (Time.unscaledTime - newest)) / 0.4f);
            heading.text = roomContext ? GameTextDatabase.Online.ChatRoom : GameTextDatabase.Online.ChatPublic;
            entry.text = Time.unscaledTime < noticeUntil ? GameTextDatabase.Online.ChatUnavailable :
                typing ? "> " + (draft.Length > 60 ? draft[^60..] : draft) + (Mathf.Repeat(Time.unscaledTime, 1) < 0.5f ? "_" : "") : GameTextDatabase.Online.ChatHint;
            entry.verticalAlignment = VerticalAlignmentOptions.Bottom;
            scroll = Mathf.Clamp(scroll, 0, Mathf.Max(0, Lines.Count - 1));
            string content = "";
            for (int i = Lines.Count - 1 - (expanded ? scroll : 0); i >= 0; i--)
            {
                var line = Lines[i];
                if (!expanded && Time.unscaledTime - line.received >= 5.4f) break;
                string candidate = line.sender + ": " + line.text + (content.Length > 0 ? "\n" + content : "");
                if (history.GetPreferredValues(candidate, 192, float.PositiveInfinity).y > 55 && content.Length > 0) break;
                content = candidate;
            }
            history.text = content;
            LocalizedTmpFontFallback.Apply(heading); LocalizedTmpFontFallback.Apply(history);
            LocalizedTmpFontFallback.Apply(entry); LocalizedTmpFontFallback.Apply(hint);
        }

        void OnDisable()
        {
            Close();
            if (keyboard != null) keyboard.onTextInput -= Type;
            keyboard = null;
            if (active == this) active = null;
        }

        void OnEnable() => active = this;
    }
}

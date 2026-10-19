using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Mirror;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

namespace Assets.Scripts.Netcode
{
    [Serializable] public sealed class OnlineDirectorySettings { public string directoryUrl = ""; }
    [Serializable] public sealed class OnlineListedRoom
    {
        public string id, name, uri, mode;
        public int maxPlayers;
        public int players;
        public bool available, requiresPassword;
        public OnlineRoomMode Mode => Enum.TryParse(mode, out OnlineRoomMode parsed) && OnlineRoomRules.IsValid(parsed)
            ? parsed : OnlineRoomMode.BattleMode;
    }
    [Serializable] public sealed class OnlineRoomListing { public OnlineListedRoom[] rooms; }

    [Serializable] public sealed class OnlineRoomAllocation
    {
        public int protocol;
        public string mode, buildIdentity, password;
        public bool isPrivate;
    }

    // Clients discover and allocate rooms here; gameplay lives in dedicated processes.
    public sealed class OnlineRoomBrowser : MonoBehaviour
    {
        public readonly List<OnlineListedRoom> Rooms = new();
        string directoryUrl;
        public bool DirectoryConfigured => !string.IsNullOrEmpty(directoryUrl);
        public bool DirectoryFailed { get; private set; }
        public bool BuildRejected { get; private set; }
        public bool RefreshRequested { get; set; }
        public bool Creating { get; private set; }

        void Start()
        {
            if (NetworkManager.singleton is BombermanNetworkManager manager && manager.IsDedicatedServer)
            { enabled = false; return; }
            var config = Resources.Load<TextAsset>("OnlineRoomDirectory");
            var settings = config != null ? JsonUtility.FromJson<OnlineDirectorySettings>(config.text) : new();
            directoryUrl = OnlineRoomRules.DirectoryUrl(settings.directoryUrl);
            if (!Uri.TryCreate(directoryUrl, UriKind.Absolute, out var endpoint) ||
                (endpoint.Scheme != "https" && !(endpoint.Scheme == "http" &&
                    (endpoint.IsLoopback || endpoint.Host == "100.64.0.46" || endpoint.Host == "192.48.205.46")))) directoryUrl = "";
            var args = Environment.GetCommandLineArgs();
            int join = Array.IndexOf(args, "-online-join");
            if (join >= 0 && join + 1 < args.Length && Uri.TryCreate(args[join + 1], UriKind.Absolute, out var server) && server.Scheme == "kcp")
            {
                NetworkManager.singleton.StartClient(server);
                return;
            }
            StartCoroutine(RefreshLoop());
        }

        bool Browsing => SceneManager.GetActiveScene().name == NetworkLobby.SceneName &&
            !NetworkClient.active && !NetworkServer.active;

        IEnumerator RefreshLoop()
        {
            float next = 0;
            while (true)
            {
                if (DirectoryConfigured && Browsing && !Creating && (RefreshRequested || Time.unscaledTime >= next))
                {
                    RefreshRequested = false;
                    next = Time.unscaledTime + 5;
                    using var request = UnityWebRequest.Get(directoryUrl + "/rooms");
                    request.timeout = 5;
                    yield return request.SendWebRequest();
                    DirectoryFailed = request.result != UnityWebRequest.Result.Success;
                    Rooms.Clear();
                    if (!DirectoryFailed)
                    {
                        OnlineRoomListing listing = null;
                        try { listing = JsonUtility.FromJson<OnlineRoomListing>(request.downloadHandler.text); }
                        catch (ArgumentException) { DirectoryFailed = true; }
                        if (listing?.rooms != null)
                            foreach (var room in listing.rooms)
                                if (Valid(room) && room.available && room.players < room.maxPlayers) Rooms.Add(room);
                    }
                }
                yield return new WaitForSecondsRealtime(0.5f);
            }
        }

        static bool Valid(OnlineListedRoom room) => room != null && !string.IsNullOrEmpty(room.id) &&
            room.players >= 0 && room.players <= room.maxPlayers &&
            Enum.TryParse(room.mode, out OnlineRoomMode mode) && OnlineRoomRules.IsValid(mode) &&
            room.maxPlayers == OnlineRoomRules.Capacity(mode) && Uri.TryCreate(room.uri, UriKind.Absolute, out var uri) &&
            uri.Scheme == "kcp" && uri.Port > 0;

        public void CreateRoom(OnlineRoomMode mode = OnlineRoomMode.BattleMode, string password = "")
        {
            if (!DirectoryConfigured || !Browsing || Creating || !OnlineRoomRules.IsValid(mode) ||
                !OnlineVersionAuthenticator.ValidPlayerName(OnlineVersionAuthenticator.PlayerName)) return;
            StartCoroutine(Allocate(mode, password));
        }

        IEnumerator Allocate(OnlineRoomMode mode, string password)
        {
            Creating = true;
            DirectoryFailed = false;
            BuildRejected = false;
            using var request = new UnityWebRequest(directoryUrl + "/rooms", "POST");
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(
                JsonUtility.ToJson(new OnlineRoomAllocation { protocol = OnlineVersionAuthenticator.ProtocolVersion,
                    mode = mode.ToString(), buildIdentity = OnlineVersionAuthenticator.BuildIdentity, password = password, isPrivate = !string.IsNullOrEmpty(password) })));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 10;
            yield return request.SendWebRequest();
            BuildRejected = request.responseCode == 409;
            OnlineListedRoom allocated = null;
            if (request.result == UnityWebRequest.Result.Success)
            {
                try { allocated = JsonUtility.FromJson<OnlineListedRoom>(request.downloadHandler.text); }
                catch (ArgumentException) { }
            }
            if (!Valid(allocated) || allocated.Mode != mode)
            { DirectoryFailed = true; Creating = false; yield break; }
            float deadline = Time.realtimeSinceStartup + 45;
            while (Browsing && Time.realtimeSinceStartup < deadline)
            {
                using var poll = UnityWebRequest.Get(directoryUrl + "/rooms/" + Uri.EscapeDataString(allocated.id));
                poll.timeout = 5;
                yield return poll.SendWebRequest();
                if (poll.result != UnityWebRequest.Result.Success) break;
                OnlineListedRoom room = null;
                try { room = JsonUtility.FromJson<OnlineListedRoom>(poll.downloadHandler.text); }
                catch (ArgumentException) { }
                if (Valid(room) && room.Mode == mode && room.available)
                {
                    Creating = false;
                    JoinRoom(room, password);
                    yield break;
                }
                yield return new WaitForSecondsRealtime(0.5f);
            }
            DirectoryFailed = true;
            Creating = false;
        }

        public void JoinRoom(OnlineListedRoom room, string password = "")
        {
            if (!Browsing || Creating || !Valid(room) || !OnlineVersionAuthenticator.ValidPlayerName(OnlineVersionAuthenticator.PlayerName)) return;
            OnlineVersionAuthenticator.RoomPassword = password;
            NetworkManager.singleton.StartClient(new Uri(room.uri));
        }
    }
}

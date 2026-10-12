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
    [Serializable]
    public sealed class OnlineDirectorySettings
    {
        public string directoryUrl = "";
    }

    [Serializable]
    public sealed class OnlineListedRoom
    {
        public string id, name, uri;
        public int players;
        [NonSerialized] public bool local;
        [NonSerialized] public float lastSeen;
    }

    [Serializable] public sealed class OnlineRoomListing { public OnlineListedRoom[] rooms; }
    [Serializable] public sealed class OnlineRoomRegistration
    {
        public string id, token, name, protocol = "sb6-room-v1", scheme;
        public int port, players;
        public bool available;
    }

    // Lives beside the existing persistent NetworkManager; does not own room authority.
    [RequireComponent(typeof(OnlineRoomDiscovery))]
    public sealed class OnlineRoomBrowser : MonoBehaviour
    {
        readonly Dictionary<string, OnlineListedRoom> localRooms = new();
        readonly List<OnlineListedRoom> publicRooms = new();
        public readonly List<OnlineListedRoom> Rooms = new();
        OnlineRoomDiscovery discovery;
        string directoryUrl, registrationId = "", registrationToken = "";
        bool wasServer, wasBrowsing, published, lanStarted;
        float nextLanAttempt;
        public bool PublishPublic { get; set; }
        public bool DirectoryConfigured => !string.IsNullOrEmpty(directoryUrl);
        public bool DirectoryFailed { get; private set; }
        public bool LanFailed { get; private set; }
        public bool RefreshRequested { get; set; }

        void Start()
        {
            discovery = GetComponent<OnlineRoomDiscovery>();
            discovery.RoomFound += OnRoomFound;
            var config = Resources.Load<TextAsset>("OnlineRoomDirectory");
            var settings = config != null ? JsonUtility.FromJson<OnlineDirectorySettings>(config.text) : new();
            registrationId = Guid.NewGuid().ToString("N");
            registrationToken = Guid.NewGuid().ToString("N");
            directoryUrl = settings.directoryUrl?.Trim().TrimEnd('/');
            if (!Uri.TryCreate(directoryUrl, UriKind.Absolute, out var endpoint) ||
                (endpoint.Scheme != "https" && !(endpoint.Scheme == "http" && endpoint.IsLoopback)))
                directoryUrl = "";
            StartCoroutine(DirectoryLoop());
        }

        void Update()
        {
            bool server = NetworkServer.active;
            bool browsing = SceneManager.GetActiveScene().name == NetworkLobby.SceneName &&
                !server && !NetworkClient.active;
            if (server != wasServer || browsing != wasBrowsing)
            {
                discovery.StopDiscovery();
                lanStarted = false;
                nextLanAttempt = 0;
                if (browsing) { publicRooms.Clear(); localRooms.Clear(); RefreshRequested = true; }
                wasServer = server;
                wasBrowsing = browsing;
            }
            if ((server || browsing) && !lanStarted && Time.unscaledTime >= nextLanAttempt &&
                Mirror.Discovery.NetworkDiscovery.SupportedOnThisPlatform)
            {
                try
                {
                    if (server) discovery.AdvertiseServer(); else discovery.StartDiscovery();
                    lanStarted = true;
                    LanFailed = false;
                }
                catch (System.Net.Sockets.SocketException) { LanFailed = true; nextLanAttempt = Time.unscaledTime + 10; }
            }
            Rooms.Clear();
            foreach (var room in localRooms.Values)
                if (Time.unscaledTime - room.lastSeen < 10) Rooms.Add(room);
            Rooms.AddRange(publicRooms);
            Rooms.Sort((a, b) => a.local != b.local ? (a.local ? -1 : 1) : string.CompareOrdinal(a.id, b.id));
        }

        void OnRoomFound(OnlineDiscoveryResponse response)
        {
            string key = response.serverId.ToString();
            if (!response.available) { localRooms.Remove(key); return; }
            localRooms[key] = new OnlineListedRoom
            {
                id = key, name = response.uri.Host, uri = response.uri.AbsoluteUri,
                players = response.players, local = true, lastSeen = Time.unscaledTime
            };
        }

        IEnumerator DirectoryLoop()
        {
            float nextRequest = 0;
            while (true)
            {
                if (!DirectoryConfigured) { yield return new WaitForSecondsRealtime(1); continue; }
                bool advertise = NetworkServer.active && PublishPublic && OnlineRoomDiscovery.AcceptingPlayers;
                if (published && !advertise)
                {
                    using var remove = UnityWebRequest.Delete(directoryUrl + "/rooms/" + registrationId);
                    remove.SetRequestHeader("Authorization", "Bearer " + registrationToken);
                    remove.timeout = 5;
                    yield return remove.SendWebRequest();
                    published = false; // Server TTL also cleans up abrupt exits or failed deletion.
                }
                if (Time.unscaledTime >= nextRequest || RefreshRequested)
                {
                    RefreshRequested = false;
                    nextRequest = Time.unscaledTime + 5;
                    if (advertise)
                    {
                        var uri = Transport.active.ServerUri();
                        var registration = new OnlineRoomRegistration
                        {
                            id = registrationId, token = registrationToken,
                            name = "Battle Mode " + registrationId.Substring(0, 6),
                            port = uri.Port, scheme = uri.Scheme,
                            players = NetworkServer.connections.Count, available = true
                        };
                        using var request = new UnityWebRequest(directoryUrl + "/rooms", "POST");
                        request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(registration)));
                        request.downloadHandler = new DownloadHandlerBuffer();
                        request.SetRequestHeader("Content-Type", "application/json");
                        request.timeout = 5;
                        yield return request.SendWebRequest();
                        DirectoryFailed = request.result != UnityWebRequest.Result.Success;
                        if (!DirectoryFailed) published = true;
                    }
                    else if (wasBrowsing)
                    {
                        using var request = UnityWebRequest.Get(directoryUrl + "/rooms?protocol=sb6-room-v1");
                        request.timeout = 5;
                        yield return request.SendWebRequest();
                        DirectoryFailed = request.result != UnityWebRequest.Result.Success;
                        publicRooms.Clear();
                        if (!DirectoryFailed)
                        {
                            OnlineRoomListing listing = null;
                            try { listing = JsonUtility.FromJson<OnlineRoomListing>(request.downloadHandler.text); }
                            catch (ArgumentException) { DirectoryFailed = true; }
                            if (listing?.rooms != null)
                                foreach (var room in listing.rooms)
                                    if (room != null && room.players >= 0 && room.players < GameSession.MaxPlayerId &&
                                        Uri.TryCreate(room.uri, UriKind.Absolute, out var uri) &&
                                        uri.Scheme == Transport.active.ServerUri().Scheme && uri.Port > 0 &&
                                        !string.IsNullOrEmpty(room.id)) publicRooms.Add(room);
                        }
                    }
                }
                yield return new WaitForSecondsRealtime(0.5f);
            }
        }

        void OnDestroy()
        {
            if (discovery != null) { discovery.RoomFound -= OnRoomFound; discovery.StopDiscovery(); }
            // LeaveRoom destroys the manager immediately, so its coroutine cannot finish a DELETE.
            // Let the native request finish independently; TTL covers application shutdown/failure.
            if (published && DirectoryConfigured)
            {
                var remove = UnityWebRequest.Delete(directoryUrl + "/rooms/" + registrationId);
                remove.SetRequestHeader("Authorization", "Bearer " + registrationToken);
                remove.timeout = 5;
                remove.SendWebRequest().completed += _ => remove.Dispose();
            }
        }
    }
}

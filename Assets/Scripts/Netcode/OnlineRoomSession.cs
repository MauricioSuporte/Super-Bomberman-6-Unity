using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Assets.Scripts.Netcode
{
    public enum OnlineRoomMode { BattleMode, NormalGame, BossRush }

    public struct OnlineRoomMessage : NetworkMessage
    {
        public int playerId;
        public int maxPlayers;
        public int adminPlayerId;
        public bool dedicated, completed;
        public float completedTime;
        public float timeoutSeconds;
        public int[] players;
        public string[] names;
        public OnlineRoomMode mode;
        public string settings;
        public string menu;
        public string characters;
        public string progress, flowView;
        public int bossDifficulty;
        public bool ownerModeLocked;
    }

    // Reliable messages carry null for unchanged presentation fields; empty clears them.
    public sealed class OnlineRoomPresentationCache
    {
        bool initialized;
        OnlineRoomMessage previous;
        float nextHeartbeat;

        static bool SameValues<T>(T[] a, T[] b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (!EqualityComparer<T>.Default.Equals(a[i], b[i])) return false;
            return true;
        }

        static bool SameState(OnlineRoomMessage a, OnlineRoomMessage b) =>
            a.playerId == b.playerId && a.adminPlayerId == b.adminPlayerId &&
            a.dedicated == b.dedicated && a.completed == b.completed && a.completedTime == b.completedTime &&
            a.maxPlayers == b.maxPlayers && a.mode == b.mode && a.bossDifficulty == b.bossDifficulty && a.ownerModeLocked == b.ownerModeLocked &&
            a.settings == b.settings && a.menu == b.menu && a.characters == b.characters &&
            a.progress == b.progress && a.flowView == b.flowView && SameValues(a.players, b.players) && SameValues(a.names, b.names);

        public bool Prepare(ref OnlineRoomMessage message, float now)
        {
            bool initial = !initialized;
            if (!initial && SameState(message, previous) && now < nextHeartbeat) return false;
            initialized = true;
            nextHeartbeat = now + 1f;
            var snapshot = message;
            if (!initial)
            {
                if (message.settings == previous.settings && message.progress == previous.progress && message.bossDifficulty == previous.bossDifficulty) message.settings = null;
                if (message.menu == previous.menu) message.menu = null;
                if (message.characters == previous.characters) message.characters = null;
                if (message.progress == previous.progress && message.bossDifficulty == previous.bossDifficulty) message.progress = null;
                if (message.flowView == previous.flowView) message.flowView = null;
            }
            previous = snapshot;
            return true;
        }
    }

    public struct OnlineSelectionInputMessage : NetworkMessage
    {
        public int held;
        public int pressed;
        public string scene;
    }

    public struct OnlineConfigureRoomMessage : NetworkMessage { public string ownerSave; public int bossDifficulty; }
    public struct OnlineLeaveRoomMessage : NetworkMessage { }

    public struct OnlinePauseMessage : NetworkMessage
    {
        public GamePauseController.OnlinePauseState state;
    }

    public struct OnlineRoundPresentationFinishedMessage : NetworkMessage
    {
        public string scene;
    }

    public partial class BombermanNetworkManager
    {
        public static int LocalRoomPlayerId { get; private set; }
        public static int[] RoomPlayers { get; private set; } = Array.Empty<int>();
        public static OnlineRoomMode RoomMode { get; private set; } = OnlineRoomMode.BattleMode;
        public static bool AcceptingRoomPlayers => NetworkServer.active && !NetworkServer.isLoadingScene &&
            SceneManager.GetActiveScene().name == NetworkLobby.SceneName && NetworkServer.connections.Count < RoomCapacity;
        static int roomPlayerLimit;
        public static int RoomCapacity => roomPlayerLimit >= 2 ? Mathf.Min(roomPlayerLimit, OnlineRoomRules.Capacity(RoomMode)) : OnlineRoomRules.Capacity(RoomMode);
        public static string[] RoomPlayerNames { get; private set; } = Array.Empty<string>();
        public static string RoomPlayerName(int id)
        {
            int index = Array.IndexOf(RoomPlayers, id);
            return index >= 0 && index < RoomPlayerNames.Length ? RoomPlayerNames[index] : "";
        }
        public static bool RoomCompleted { get; private set; }
        public static float CompletedRunTime { get; private set; }
        public static int RoomAdminPlayerId { get; private set; }
        public static bool DedicatedRoom { get; private set; }
        public bool IsDedicatedServer => dedicatedServer;
        bool dedicatedInputApplied;
        public static string MenuView { get; private set; } = "";
        public static string CharacterView { get; private set; } = "";
        public const string ConfigurationScene = "BattleModeMenu";
        readonly Dictionary<int, int> selectionHeld = new();
        readonly Dictionary<int, int> selectionPressed = new();
        readonly Dictionary<int, float> selectionLastReceived = new();
        readonly Dictionary<int, OnlineRoomPresentationCache> roomPresentationByConnection = new();
        float nextRoomBroadcast, nextCoopSessionBroadcast;
        float nextSelectionSend;
        int lastSelectionHeld;
        bool leavingRoom;
        float roomDeadline;
        bool roomStarted;
        float receivedTimeoutAt, receivedTimeoutSeconds;
        public static float RoomTimeoutSeconds => singleton is BombermanNetworkManager manager
            ? Mathf.Max(0, NetworkServer.active ? manager.roomDeadline - Time.unscaledTime :
                manager.receivedTimeoutSeconds - (Time.unscaledTime - manager.receivedTimeoutAt)) : 0;

        public static void RegisterRoomActivity()
        {
            if (NetworkServer.active && singleton is BombermanNetworkManager manager && manager.roomStarted)
                manager.roomDeadline = Time.unscaledTime + 300f;
        }

        void TickRoomTimeout()
        {
            if (!NetworkServer.active || leavingRoom) return;
            if (NetworkClient.active && !dedicatedServer && PlayerInputManager.Instance != null)
                for (int i = 0; i < 11; i++)
                    if (PlayerInputManager.Instance.Get(1, (PlayerAction)i) || PlayerInputManager.Instance.GetDown(1, (PlayerAction)i))
                    { RegisterRoomActivity(); break; }
            if (Time.unscaledTime >= roomDeadline) CloseRoomForEveryone();
        }

        public static System.Collections.IEnumerator WaitForOnlineSceneReady()
        {
            if (!NetworkServer.active || !NetSync.IsOnline) yield break;
            while (NetworkServer.active)
            {
                bool ready = !NetworkServer.isLoadingScene;
                foreach (var peer in NetworkServer.connections.Values)
                    if (peer != null && peer.isAuthenticated && !peer.isReady) ready = false;
                if (ready) yield break;
                yield return null;
            }
        }

        public static bool InConfiguration => NetSync.IsOnline && SceneManager.GetActiveScene().name == ConfigurationScene;
        public static bool IsConnectedSlot(int playerId) => Array.IndexOf(RoomPlayers, playerId) >= 0;
        public static bool IsRoomAdmin => LocalRoomPlayerId > 0 && LocalRoomPlayerId == RoomAdminPlayerId;
        public static bool CanConfigure => NetSync.IsOnline &&
            (IsRoomAdmin || (NetworkServer.active && singleton is BombermanNetworkManager manager && manager.IsDedicatedServer));

        void InitializeRoomServer()
        {
            roomPresentationByConnection.Clear();
            roomStarted = false;
            roomDeadline = Time.unscaledTime + 300f;
            if (GameSession.Instance == null) new GameObject("GameSession").AddComponent<GameSession>();
            NetworkServer.RegisterHandler<OnlineRoundPresentationFinishedMessage>((connection, message) =>
            {
                if (message.scene == SceneManager.GetActiveScene().name)
                    finishedRoundPeers.Add(connection.connectionId);
            });
            global::SaveSystem.BeginOnlineBattleSettings();
            var modes = new BattleModePlayerControlMode[GameSession.MaxPlayerId];
            Array.Fill(modes, BattleModePlayerControlMode.Off);
            global::SaveSystem.SetBattleModePlayerControlModes(modes);
            OwnerModeLocked = false;
            RoomMode = OnlineRoomRules.ReadMode(System.Environment.GetCommandLineArgs());
            roomPlayerLimit = ReadDedicatedNumber(System.Environment.GetCommandLineArgs(), "-room-max-players", OnlineRoomRules.Capacity(RoomMode), 2, OnlineRoomRules.Capacity(RoomMode));
            RoomCompleted = false; CompletedRunTime = 0;
            RoomAdminPlayerId = 0;
            DedicatedRoom = dedicatedServer;
            NetworkServer.RegisterHandler<OnlineLeaveRoomMessage>((connection, _) =>
            {
                if (connection.isAuthenticated && playerIdByConnection.ContainsKey(connection.connectionId)) CloseRoomForEveryone();
            });
            NetworkServer.RegisterHandler<OnlineConfigureRoomMessage>((connection, message) =>
            {
                if (dedicatedServer && InLobbyScene() && connection.isAuthenticated &&
                    playerIdByConnection.TryGetValue(connection.connectionId, out int id) && id == RoomAdminPlayerId)
                {
                    if (!global::SaveSystem.ApplyRoomProgress(message.ownerSave, false)) return;
                    OwnerModeLocked = RoomMode == OnlineRoomMode.BossRush && !global::SaveSystem.Data.bossRushUnlocked;
                    if (OwnerModeLocked) { BroadcastRoom(); return; }
                    BossRushProgress.SetSelectedDifficulty((BossRushDifficulty)Mathf.Clamp(message.bossDifficulty, 0, 3));
                    ownerFlowConfigured = true;
                    ServerBeginConfiguration();
                }
            });
            NetworkServer.RegisterHandler<OnlineSelectionInputMessage>(ReceiveSelectionInput);
        }

        void InitializeRoomClient()
        {
            if (GameSession.Instance == null) new GameObject("GameSession").AddComponent<GameSession>();
            global::SaveSystem.BeginOnlineBattleSettings();
            global::SaveSystem.BeginOnlineCoopSettings();
            InitializeCoopSessionClient();
            NetworkClient.RegisterHandler<OnlineRoomMessage>(ReceiveRoom);
            NetworkClient.RegisterHandler<OnlinePauseMessage>(message => GamePauseController.Instance?.ApplyOnlinePause(message.state));
        }

        static void ReceiveRoom(OnlineRoomMessage message)
        {
            if (singleton is BombermanNetworkManager manager)
            {
                manager.receivedTimeoutSeconds = message.timeoutSeconds;
                manager.receivedTimeoutAt = Time.unscaledTime;
            }
            LocalRoomPlayerId = message.playerId;
            bool presenceChanged = RoomPlayers.Length != (message.players?.Length ?? 0) ||
                RoomAdminPlayerId != message.adminPlayerId || RoomMode != message.mode;
            RoomPlayers = message.players ?? Array.Empty<int>();
            RoomPlayerNames = message.names ?? Array.Empty<string>();
            RoomCompleted = message.completed; CompletedRunTime = message.completedTime;
            RoomMode = message.mode;
            roomPlayerLimit = message.maxPlayers;
            if (!string.IsNullOrEmpty(message.progress)) global::SaveSystem.ApplyRoomProgress(message.progress, message.playerId == message.adminPlayerId);
            if (!string.IsNullOrEmpty(message.progress) && RoomMode == OnlineRoomMode.BossRush)
                BossRushProgress.ApplyOnlineSelection((BossRushDifficulty)message.bossDifficulty, message.playerId == message.adminPlayerId);
            OwnerModeLocked = message.ownerModeLocked;
            if (message.flowView != null) FlowView = message.flowView;
            RoomAdminPlayerId = message.adminPlayerId;
            DedicatedRoom = message.dedicated;
            if (presenceChanged) DiscordRichPresenceController.RefreshOnlinePresence();
            if (message.menu != null) MenuView = message.menu;
            if (message.characters != null) CharacterView = message.characters;
            global::SaveSystem.ApplyOnlineBattleSettings(message.settings);
            if (GameSession.Instance != null)
            {
                var active = new List<int>(RoomPlayers);
                if (InConfiguration || SceneManager.GetActiveScene().name == NetworkLobby.SceneName)
                    GameSession.Instance.EndBattleMatch();
                if (RoomMode == OnlineRoomMode.BattleMode && !InConfiguration && SceneManager.GetActiveScene().name != NetworkLobby.SceneName)
                {
                    var modes = global::SaveSystem.GetBattleModePlayerControlModes();
                    for (int i = 0; i < modes.Length; i++)
                        if (modes[i] == BattleModePlayerControlMode.Com) active.Add(i + 1);
                }
                GameSession.Instance.SetActivePlayerIds(active);
            }
        }

        void ReceiveSelectionInput(NetworkConnectionToClient connection, OnlineSelectionInputMessage message)
        {
            if (message.scene != SceneManager.GetActiveScene().name ||
                !playerIdByConnection.TryGetValue(connection.connectionId, out int id))
                return;
            if ((message.held | message.pressed) != 0) RegisterRoomActivity();
            bool adminInput = dedicatedServer && id == RoomAdminPlayerId && !InLobbyScene();
            if (!adminInput)
            {
                if (!InCharacterConfiguration) return;
                var menu = FindAnyObjectByType<BattleModeMenu>(FindObjectsInactive.Include);
                if (InConfiguration && (menu == null || !menu.OnlineCharacterSelectionActive)) return;
                var characters = FindAnyObjectByType<BomberSkinSelectMenu>(FindObjectsInactive.Include);
                if (characters == null || characters.ActiveOnlineSelectionPlayerId != id) return;
            }
            if (InAnyMenu) nextRoomBroadcast = 0;
            const int valid = (1 << 11) - 1;
            selectionHeld[id] = message.held & valid;
            selectionPressed[id] = (selectionPressed.TryGetValue(id, out int previous) ? previous : 0) | (message.pressed & valid);
            selectionLastReceived[id] = Time.unscaledTime;
        }

        public static bool SelectionHeld(PlayerInputManager input, int id, PlayerAction action)
        {
            var manager = singleton as BombermanNetworkManager;
            if (!InCharacterConfiguration || manager == null || id == LocalRoomPlayerId)
                return input != null && input.Get(InCharacterConfiguration ? 1 : id, action);
            return manager.selectionLastReceived.TryGetValue(id, out float received) &&
                Time.unscaledTime - received < 1f && manager.selectionHeld.TryGetValue(id, out int mask) && (mask & (1 << (int)action)) != 0;
        }

        public static bool SelectionDown(PlayerInputManager input, int id, PlayerAction action)
        {
            var manager = singleton as BombermanNetworkManager;
            if (!InCharacterConfiguration || manager == null || id == LocalRoomPlayerId)
                return input != null && input.GetDown(InCharacterConfiguration ? 1 : id, action);
            if (!manager.selectionPressed.TryGetValue(id, out int mask)) return false;
            int bit = 1 << (int)action;
            manager.selectionPressed[id] = mask & ~bit;
            return (mask & bit) != 0;
        }

        public override void LateUpdate()
        {
            base.LateUpdate();
            TickCoopWorld();
            TickBombAnimations();
            PresentOnlineSprites();
            TickOnlinePauseBroadcast();
            if (NetworkServer.active && Time.unscaledTime >= nextRoomBroadcast)
            {
                nextRoomBroadcast = Time.unscaledTime + (InAnyMenu ? 1f / 60f : IsCoopRoom ? 0.5f : 0.1f);
                // Poll at presentation speed; unchanged payloads are omitted per peer.
                BroadcastRoom();
            }
        }

        public override void Update()
        {
            base.Update();
            TickDedicatedProcess();
            TickRoomTimeout();
#if UNITY_EDITOR || DEBUG
            TickLocalLoadTest();
#endif
            TickDedicatedAdminInput();
            if (NetworkServer.active && RoomMode == OnlineRoomMode.BossRush && Time.unscaledTime >= nextCoopSessionBroadcast)
            {
                nextCoopSessionBroadcast = Time.unscaledTime + 0.1f;
                BroadcastCoopSession();
            }
            if ((InCharacterConfiguration || (DedicatedRoom && IsRoomAdmin && !InLobbyScene())) &&
                !NetworkServer.active && NetworkClient.isConnected && NetworkClient.connection.isAuthenticated)
            {
                var input = PlayerInputManager.Instance;
                int held = 0, pressed = 0;
                for (int i = 0; i < 11; i++)
                {
                    if (input != null && input.Get(1, (PlayerAction)i)) held |= 1 << i;
                    if (input != null && input.GetDown(1, (PlayerAction)i)) pressed |= 1 << i;
                }
#if ENABLE_INPUT_SYSTEM
                if (DedicatedRoom && IsRoomAdmin && !InConfiguration &&
                    UnityEngine.InputSystem.Keyboard.current != null &&
                    UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
                    pressed |= 1 << (int)PlayerAction.Start;
#endif
                if (pressed != 0 || held != lastSelectionHeld || Time.unscaledTime >= nextSelectionSend)
                {
                    nextSelectionSend = Time.unscaledTime + 1f / 60f;
                    lastSelectionHeld = held;
                    NetworkClient.Send(new OnlineSelectionInputMessage
                        { held = held, pressed = pressed, scene = SceneManager.GetActiveScene().name });
                }
            }
        }

        public static int FindAvailableRoomSlot(ICollection<int> occupied, int capacity = GameSession.MaxPlayerId)
        {
            for (int id = GameSession.MinPlayerId; id <= Mathf.Clamp(capacity, GameSession.MinPlayerId, GameSession.MaxPlayerId); id++)
                if (!occupied.Contains(id)) return id;
            return 0;
        }

        public static int FindRoomAdmin(ICollection<int> occupied, int current = 0)
        {
            if (current > 0) return occupied.Contains(current) ? current : 0;
            int selected = 0;
            foreach (int id in occupied)
                if (id >= GameSession.MinPlayerId && id <= GameSession.MaxPlayerId &&
                    (selected == 0 || id < selected)) selected = id;
            return selected;
        }

        void ClearDedicatedAdminInput()
        {
            if (dedicatedInputApplied) PlayerInputManager.Instance?.ClearSyntheticPlayer(1);
            dedicatedInputApplied = false;
        }

        void TickDedicatedAdminInput()
        {
            if (!dedicatedServer || !NetworkServer.active) return;
            var menu = InConfiguration ? FindAnyObjectByType<BattleModeMenu>(FindObjectsInactive.Include) : null;
            bool configuring = (menu != null && !menu.OnlineCharacterSelectionActive) || InOwnerMenu;
            bool playing = IsCoopStage || SceneManager.GetActiveScene().name.StartsWith("BattleMode_", StringComparison.Ordinal);
            if ((!configuring && !playing) || !selectionLastReceived.TryGetValue(RoomAdminPlayerId, out float received) ||
                Time.unscaledTime - received > 0.5f)
            { ClearDedicatedAdminInput(); return; }
            var input = PlayerInputManager.Instance;
            if (input == null) return;
            int held = selectionHeld.TryGetValue(RoomAdminPlayerId, out int heldMask) ? heldMask : 0;
            int pressed = selectionPressed.TryGetValue(RoomAdminPlayerId, out int pressedMask) ? pressedMask : 0;
            selectionPressed[RoomAdminPlayerId] = 0;
            for (int i = 0; i < 11; i++)
            {
                var action = (PlayerAction)i;
                // During gameplay only Start can open pause; gameplay input keeps its owner path.
                bool allowed = configuring || GamePauseController.IsPaused || action == PlayerAction.Start ||
                    (IsCoopStage && SceneManager.GetActiveScene().name == global::StageAssets.World3HallPortalSelectionController.SceneName);
                if (allowed) input.SetSyntheticHeld(1, action, (held & (1 << i)) != 0);
                if (allowed && (pressed & (1 << i)) != 0) input.TapSynthetic(1, action);
            }
            dedicatedInputApplied = true;
        }

        public static void ServerDisconnectRoomAdmin()
        {
            if (singleton is not BombermanNetworkManager manager || !manager.dedicatedServer || !NetworkServer.active) return;
            foreach (var peer in NetworkServer.connections.Values)
                if (peer != null && manager.playerIdByConnection.TryGetValue(peer.connectionId, out int id) && id == RoomAdminPlayerId)
                { peer.Disconnect(); return; }
        }

        void BroadcastRoom()
        {
            RoomAdminPlayerId = FindRoomAdmin(playerIdByConnection.Values, RoomAdminPlayerId);
            RoomPlayers = new List<int>(playerIdByConnection.Values).ToArray();
            Array.Sort(RoomPlayers);
            RoomPlayerNames = new string[RoomPlayers.Length];
            if (authenticator is OnlineVersionAuthenticator names)
                foreach (var connection in playerIdByConnection)
                {
                    int index = Array.IndexOf(RoomPlayers, connection.Value);
                    if (index >= 0) RoomPlayerNames[index] = names.NameForConnection(connection.Key);
                }
            var menu = InConfiguration ? FindAnyObjectByType<BattleModeMenu>(FindObjectsInactive.Include) : null;
            MenuView = menu != null ? menu.CaptureOnlineView() : "";
            var characters = InCharacterConfiguration ? FindAnyObjectByType<BomberSkinSelectMenu>(FindObjectsInactive.Include) : null;
            CharacterView = characters != null ? characters.CaptureOnlineView() : "";
            CaptureChangedRoomProgress();
            string progress = ownerFlowConfigured ? lastRoomProgress ?? "" : "";
            string flow = CaptureOwnerMenuView();
            string settings = global::SaveSystem.CaptureOnlineBattleSettings();
            foreach (var connection in NetworkServer.connections.Values)
            {
                if (connection == null || !connection.isAuthenticated || !connection.isReady ||
                    !playerIdByConnection.TryGetValue(connection.connectionId, out int id)) continue;
                if (connection == NetworkServer.localConnection) { LocalRoomPlayerId = id; continue; }
                if (!roomPresentationByConnection.TryGetValue(connection.connectionId, out var cache))
                    roomPresentationByConnection[connection.connectionId] = cache = new OnlineRoomPresentationCache();
                var message = new OnlineRoomMessage { playerId = id, adminPlayerId = RoomAdminPlayerId,
                    dedicated = dedicatedServer, completed = RoomCompleted, completedTime = CompletedRunTime,
                    players = RoomPlayers, names = RoomPlayerNames, mode = RoomMode, maxPlayers = RoomCapacity, timeoutSeconds = RoomTimeoutSeconds,
                    settings = settings, menu = MenuView, characters = CharacterView, progress = progress, flowView = flow, bossDifficulty = (int)BossRushProgress.GetSelectedDifficulty(), ownerModeLocked = OwnerModeLocked };
                if (cache.Prepare(ref message, Time.unscaledTime)) connection.Send(message);
            }
        }

        public static void ServerBeginConfiguration()
        {
            if (!NetworkServer.active)
            {
                if (DedicatedRoom && IsRoomAdmin && NetworkClient.isConnected && InLobbyScene())
                    NetworkClient.Send(new OnlineConfigureRoomMessage { ownerSave = global::SaveSystem.CaptureRoomOwnerSave(), bossDifficulty = (int)BossRushProgress.GetSelectedDifficulty() });
                return;
            }
            if (RoomMode != OnlineRoomMode.BattleMode)
            {
                if (singleton is BombermanNetworkManager coop && CanConfigure) coop.ServerBeginOwnerCoopFlow();
                return;
            }
            if (!CanConfigure || RoomPlayers.Length < 1 || singleton is not BombermanNetworkManager manager) return;
            var modes = global::SaveSystem.GetBattleModePlayerControlModes();
            for (int i = 0; i < modes.Length; i++)
                modes[i] = IsConnectedSlot(i + 1) ? BattleModePlayerControlMode.Man :
                    modes[i] == BattleModePlayerControlMode.Com ? BattleModePlayerControlMode.Com : BattleModePlayerControlMode.Off;
            global::SaveSystem.SetBattleModePlayerControlModes(modes);
            manager.BroadcastRoom();
            singleton.ServerChangeScene(ConfigurationScene);
        }

        public static void ServerLaunchConfiguredBattle(string scene)
        {
            if (!CanConfigure || singleton is not BombermanNetworkManager manager || RoomPlayers.Length < 1) return;
            PlayerPersistentStats.ApplyOnlineSelections(global::SaveSystem.BattleConfigurationData);
            manager.BroadcastRoom();
            singleton.ServerChangeScene(scene);
        }

        readonly HashSet<int> finishedRoundPeers = new();
        readonly HashSet<int> roundSpawnedPlayerIds = new();
        bool roundPlayersReady;
        public static bool OnlineRoundPlayersReady => !NetSync.IsOnline ||
            (singleton is BombermanNetworkManager manager && manager.roundPlayersReady);

        void RegisterRoundPlayerSpawn(int id)
        {
            roundSpawnedPlayerIds.Add(id);
            var modes = global::SaveSystem.GetBattleModePlayerControlModes();
            int count = 0;
            for (int i = 0; i < modes.Length; i++)
            {
                if (modes[i] == BattleModePlayerControlMode.Off) continue;
                count++;
                if (!roundSpawnedPlayerIds.Contains(i + 1)) return;
            }
            roundPlayersReady = count >= 2;
        }

        public static void BeginOnlineRoundPresentation()
        {
            if (singleton is BombermanNetworkManager manager && NetSync.IsServer)
                manager.finishedRoundPeers.Clear();
        }

        public static System.Collections.IEnumerator CompleteOnlineRoundPresentation(bool matchComplete)
        {
            if (!NetSync.IsServer)
            {
                if (NetworkClient.isConnected)
                    NetworkClient.Send(new OnlineRoundPresentationFinishedMessage { scene = SceneManager.GetActiveScene().name });
                yield break;
            }
            if (singleton is not BombermanNetworkManager manager) yield break;
            float deadline = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < deadline)
            {
                bool ready = true;
                foreach (var peer in NetworkServer.connections.Values)
                    if (peer != null && peer != NetworkServer.localConnection && !manager.finishedRoundPeers.Contains(peer.connectionId))
                        ready = false;
                if (ready) break;
                yield return null;
            }
            if (matchComplete)
            {
                GameSession.Instance?.EndBattleMatch();
                BattleModeMenu.OpenDirectlyAtStageSelect = true;
                manager.BroadcastRoom();
                manager.ServerChangeScene(ConfigurationScene);
            }
            else ServerStartNextRound();
        }

        public static void LeaveRoom() => LeaveRoomToScene("TitleScreen");
        public static void LeaveRoomToBrowser() => LeaveRoomToScene(NetworkLobby.SceneName);

        static void LeaveRoomToScene(string destination)
        {
            if (singleton is not BombermanNetworkManager manager || manager.leavingRoom) return;
            if (NetworkClient.isConnected && NetworkClient.connection.isAuthenticated) NetworkClient.Send(new OnlineLeaveRoomMessage());
            manager.leavingRoom = true;
            if (NetworkServer.active && NetworkClient.active) manager.StopHost();
            else if (NetworkServer.active) manager.StopServer();
            else manager.StopClient();
            ClearRoom();
            if (destination != NetworkLobby.SceneName || SceneManager.GetActiveScene().name != NetworkLobby.SceneName)
                GameMusicController.Instance?.StopMusic();
            Destroy(manager.gameObject);
            OnlineVersionAuthenticator.RoomPassword = "";
            SceneManager.LoadScene(destination);
        }

        static void ClearRoom()
        {
            LocalRoomPlayerId = 0;
            OwnerModeLocked = false;
            BossRushProgress.ClearOnlineSelection();
            RoomCompleted = false; CompletedRunTime = 0;
            RoomAdminPlayerId = 0;
            DedicatedRoom = false;
            RoomPlayers = Array.Empty<int>(); RoomPlayerNames = Array.Empty<string>();
            roomPlayerLimit = 0;
            MenuView = CharacterView = FlowView = "";
            BossRushSession.CancelRun();
            global::SaveSystem.EndOnlineBattleSettings();
            global::SaveSystem.EndOnlineCoopSettings();
            PlayerPersistentStats.ResetSessionForReturnToTitle();
            for (int id = 1; id <= GameSession.MaxPlayerId; id++) PlayerInputManager.Instance?.ClearSyntheticPlayer(id);
            GameSession.Instance?.EndBattleMatch();
            GamePauseController.ClearPauseFlag();
            Time.timeScale = 1f;
            NetSync.IsNetworkedScene = false;
        }

        public override void OnServerConnect(NetworkConnectionToClient connection)
        {
            if (leavingRoom || !InLobbyScene() || playerIdByConnection.Count >= RoomCapacity)
            {
                Debug.Log($"[Room] rejected connection={connection.connectionId} lobby={InLobbyScene()} players={playerIdByConnection.Count}");
                connection.Disconnect(); return;
            }
            base.OnServerConnect(connection);
        }

        public override void OnClientDisconnect()
        {
            base.OnClientDisconnect();
            if (!leavingRoom && !NetworkServer.active) LeaveRoomToBrowser();
        }

        public override void OnServerSceneChanged(string sceneName)
        {
            base.OnServerSceneChanged(sceneName);
            if (!sceneName.StartsWith("BattleMode_", StringComparison.Ordinal)) return;
            var modes = global::SaveSystem.GetBattleModePlayerControlModes();
            for (int i = 0; i < modes.Length; i++)
            {
                if (modes[i] != BattleModePlayerControlMode.Com) continue;
                int id = i + 1;
                var player = Instantiate(playerPrefab, ResolveSpawnPosition(id), Quaternion.identity);
                if (player.TryGetComponent<NetworkPlayerSetup>(out var setup)) setup.ServerAssignPlayerId(id);
                PrepareOnlinePlayer(player, id, true);
                NetworkServer.Spawn(player);
                RegisterRoundPlayerSpawn(id);
            }
            SyncActivePlayersToSession();
        }

        public override void OnServerChangeScene(string newSceneName)
        {
            roomPresentationByConnection.Clear();
            Debug.Log($"[Room] scene={newSceneName} players={playerIdByConnection.Count} admin={RoomAdminPlayerId}");
            if (!roomStarted || newSceneName == NetworkLobby.SceneName)
                roomDeadline = Time.unscaledTime + 300f;
            roomStarted = newSceneName != NetworkLobby.SceneName;
            ClearDedicatedAdminInput();
            selectionHeld.Clear();
            selectionPressed.Clear();
            selectionLastReceived.Clear();
            roundPlayersReady = false;
            roundSpawnedPlayerIds.Clear();
            GetComponent<OnlineLobbyMenu>()?.ReleaseRoomEventSystem();
            if (newSceneName.StartsWith("Stage_", StringComparison.Ordinal))
                PlayerPersistentStats.ApplyOnlineSelections(global::SaveSystem.BattleConfigurationData);
            base.OnServerChangeScene(newSceneName);
        }

        public override void OnClientChangeScene(string newSceneName, SceneOperation operation, bool customHandling)
        {
            MenuView = CharacterView = FlowView = "";
            GetComponent<OnlineLobbyMenu>()?.ReleaseRoomEventSystem();
            base.OnClientChangeScene(newSceneName, operation, customHandling);
        }

        void PrepareOnlinePlayer(GameObject player, int id, bool computer)
        {
            var spawner = FindAnyObjectByType<PlayersSpawner>();
            if (spawner != null) spawner.ConfigureBattleModeComController(player, id);
            PlayerPersistentStats.LoadInto(id, player.GetComponent<MovementController>(), player.GetComponent<BombController>());
            foreach (var skin in player.GetComponentsInChildren<PlayerBomberSkinController>(true)) skin.ApplyFromIdentity();
            if (player.TryGetComponent<PlayerMountCompanion>(out var companion)) companion.RestoreMountedFromPersistent();
            if (player.TryGetComponent<MovementController>(out var movement)) movement.SetPredictLocally(false);
            if (player.TryGetComponent<NetworkPlayerAnimation>(out var animation)) animation.syncDirection = SyncDirection.ServerToClient;
            if (player.TryGetComponent<NetworkTransformReliable>(out var transformSync)) transformSync.syncDirection = SyncDirection.ServerToClient;
        }
    }
}

namespace Assets.Scripts.Netcode
{
    public partial class BombermanNetworkManager
    {
        public static string FlowView { get; private set; } = "";
        bool ownerFlowConfigured;
        string lastRoomProgress;
        int lastRoomDifficulty = -1;
        public static bool OwnerModeLocked { get; private set; }
        public static bool InOwnerMenu => NetSync.IsOnline && IsOwnerMenuScene(SceneManager.GetActiveScene().name);
        public static bool InAnyMenu => InConfiguration || InOwnerMenu || (NetSync.IsOnline && SceneManager.GetActiveScene().name == "SkinSelect");
        public static bool InCharacterConfiguration => InConfiguration || (NetSync.IsOnline && SceneManager.GetActiveScene().name == "SkinSelect");
        public static bool IsMenuObserver => NetSync.IsOnline && !NetworkServer.active;
        public static bool IsOwnerMenuScene(string scene) => scene is "SaveFileMenu" or "WorldMap" or "BossRush" or "END_SCREEN";

        void ServerBeginOwnerCoopFlow()
        {
            if (!NetworkServer.active || !InLobbyScene() || RoomPlayers.Length < 1 || !ownerFlowConfigured) return;
            RoomCompleted = false; CompletedRunTime = 0;
            PlayerPersistentStats.EnsureSessionBooted();
            PlayerPersistentStats.ApplyOnlineSelections(global::SaveSystem.BattleConfigurationData);
            PlayerPersistentStats.ResetGameplayPersistenceToBaseValues();
            GameSession.Instance?.EndBattleMatch();
            GameSession.Instance?.SetActivePlayerIds(RoomPlayers);
            BossRushSession.CancelRun();
            if (RoomMode == OnlineRoomMode.BossRush) SkinSelectFlowRouter.SetReturnToBossRush("BossRush");
            else SkinSelectFlowRouter.SetReturnToSaveFileMenu();
            BroadcastRoom();
            ServerChangeScene("SkinSelect");
        }

        string CaptureChangedRoomProgress()
        {
            if (!ownerFlowConfigured) return "";
            string snapshot = global::SaveSystem.CaptureRoomProgress();
            int difficulty = (int)BossRushProgress.GetSelectedDifficulty();
            if (snapshot == lastRoomProgress && difficulty == lastRoomDifficulty) return "";
            lastRoomProgress = snapshot;
            lastRoomDifficulty = difficulty;
            return snapshot;
        }

        string CaptureOwnerMenuView()
        {
            if (!InOwnerMenu) return "";
            if (FindAnyObjectByType<SaveFileMenu>() is { } saves) return saves.CaptureOnlineView();
            if (FindAnyObjectByType<BossRushMenu>() is { } boss) return boss.CaptureOnlineView();
            if (FindAnyObjectByType<WorldMapController>() is { } map) return map.CaptureOnlineView();
            return "";
        }

        public static bool RouteOnlineMenuScene(string target)
        {
            if (!NetSync.IsOnline) return false;
            if (!NetworkServer.active) return true;
            if (target == "TitleScreen") { CloseRoomForEveryone(); return true; }
            if (singleton is BombermanNetworkManager manager)
            {
                if (target == "SkinSelect")
                {
                    if (RoomMode == OnlineRoomMode.BossRush) SkinSelectFlowRouter.SetReturnToBossRush("BossRush");
                    else SkinSelectFlowRouter.SetReturnToSaveFileMenu();
                }
                manager.BroadcastRoom();
                manager.BroadcastCoopSession();
                manager.ServerChangeScene(target);
            }
            return true;
        }

        public static void CloseRoomForEveryone()
        {
            if (!NetworkServer.active || singleton is not BombermanNetworkManager manager || manager.leavingRoom) return;
            if (!manager.dedicatedServer) { LeaveRoom(); return; }
            manager.BroadcastRoom();
            manager.leavingRoom = true;
            manager.StartCoroutine(manager.StopDedicatedRoomAfterDisconnect());
        }
    }
}

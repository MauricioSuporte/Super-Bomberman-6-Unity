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
        public int[] players;
        public OnlineRoomMode mode;
        public string settings;
        public string menu;
        public string characters;
    }

    public struct OnlineSelectionInputMessage : NetworkMessage
    {
        public int held;
        public int pressed;
    }

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
        public static string MenuView { get; private set; } = "";
        public static string CharacterView { get; private set; } = "";
        public const string ConfigurationScene = "BattleModeMenu";
        readonly Dictionary<int, int> selectionHeld = new();
        readonly Dictionary<int, int> selectionPressed = new();
        readonly Dictionary<int, float> selectionLastReceived = new();
        float nextRoomBroadcast;
        float nextSelectionSend;
        bool leavingRoom;

        public static bool InConfiguration => NetSync.IsOnline && SceneManager.GetActiveScene().name == ConfigurationScene;
        public static bool IsConnectedSlot(int playerId) => Array.IndexOf(RoomPlayers, playerId) >= 0;
        public static bool CanConfigure => NetSync.IsServer && LocalRoomPlayerId == 1;

        void InitializeRoomServer()
        {
            NetworkServer.RegisterHandler<OnlineRoundPresentationFinishedMessage>((connection, message) =>
            {
                if (message.scene == SceneManager.GetActiveScene().name)
                    finishedRoundPeers.Add(connection.connectionId);
            });
            global::SaveSystem.BeginOnlineBattleSettings();
            var modes = new BattleModePlayerControlMode[GameSession.MaxPlayerId];
            Array.Fill(modes, BattleModePlayerControlMode.Off);
            global::SaveSystem.SetBattleModePlayerControlModes(modes);
            RoomMode = OnlineRoomMode.BattleMode;
            NetworkServer.RegisterHandler<OnlineSelectionInputMessage>(ReceiveSelectionInput);
        }

        void InitializeRoomClient()
        {
            global::SaveSystem.BeginOnlineBattleSettings();
            NetworkClient.RegisterHandler<OnlineRoomMessage>(ReceiveRoom);
            NetworkClient.RegisterHandler<OnlinePauseMessage>(message => GamePauseController.Instance?.ApplyOnlinePause(message.state));
        }

        static void ReceiveRoom(OnlineRoomMessage message)
        {
            LocalRoomPlayerId = message.playerId;
            RoomPlayers = message.players ?? Array.Empty<int>();
            RoomMode = message.mode;
            MenuView = message.menu ?? "";
            CharacterView = message.characters ?? "";
            global::SaveSystem.ApplyOnlineBattleSettings(message.settings);
            if (GameSession.Instance != null)
            {
                var active = new List<int>(RoomPlayers);
                if (InConfiguration || SceneManager.GetActiveScene().name == NetworkLobby.SceneName)
                    GameSession.Instance.EndBattleMatch();
                if (!InConfiguration && SceneManager.GetActiveScene().name != NetworkLobby.SceneName)
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
            if (!InConfiguration || !playerIdByConnection.TryGetValue(connection.connectionId, out int id))
                return;
            var menu = FindAnyObjectByType<BattleModeMenu>(FindObjectsInactive.Include);
            if (menu == null || !menu.OnlineCharacterSelectionActive)
                return;
            var characters = FindAnyObjectByType<BomberSkinSelectMenu>(FindObjectsInactive.Include);
            if (characters == null || characters.ActiveOnlineSelectionPlayerId != id) return;
            const int valid = (1 << 11) - 1;
            selectionHeld[id] = message.held & valid;
            selectionPressed[id] = (selectionPressed.TryGetValue(id, out int previous) ? previous : 0) | (message.pressed & valid);
            selectionLastReceived[id] = Time.unscaledTime;
        }

        public static bool SelectionHeld(PlayerInputManager input, int id, PlayerAction action)
        {
            var manager = singleton as BombermanNetworkManager;
            if (!InConfiguration || manager == null || id == LocalRoomPlayerId)
                return input != null && input.Get(InConfiguration ? 1 : id, action);
            return manager.selectionLastReceived.TryGetValue(id, out float received) &&
                Time.unscaledTime - received < 1f && manager.selectionHeld.TryGetValue(id, out int mask) && (mask & (1 << (int)action)) != 0;
        }

        public static bool SelectionDown(PlayerInputManager input, int id, PlayerAction action)
        {
            var manager = singleton as BombermanNetworkManager;
            if (!InConfiguration || manager == null || id == LocalRoomPlayerId)
                return input != null && input.GetDown(InConfiguration ? 1 : id, action);
            if (!manager.selectionPressed.TryGetValue(id, out int mask)) return false;
            int bit = 1 << (int)action;
            manager.selectionPressed[id] = mask & ~bit;
            return (mask & bit) != 0;
        }

        public override void Update()
        {
            base.Update();
            if (NetworkServer.active && Time.unscaledTime >= nextRoomBroadcast)
            {
                nextRoomBroadcast = Time.unscaledTime + (InConfiguration ? 1f / 30f : 0.1f);
                BroadcastRoom();
            }
            if (InConfiguration && !NetworkServer.active && NetworkClient.isConnected)
            {
                var input = PlayerInputManager.Instance;
                int held = 0, pressed = 0;
                for (int i = 0; i < 11; i++)
                {
                    if (input != null && input.Get(1, (PlayerAction)i)) held |= 1 << i;
                    if (input != null && input.GetDown(1, (PlayerAction)i)) pressed |= 1 << i;
                }
                if (pressed != 0 || Time.unscaledTime >= nextSelectionSend)
                {
                    nextSelectionSend = Time.unscaledTime + 0.05f;
                    NetworkClient.Send(new OnlineSelectionInputMessage { held = held, pressed = pressed });
                }
            }
        }

        public static int FindAvailableRoomSlot(ICollection<int> occupied)
        {
            for (int id = GameSession.MinPlayerId; id <= GameSession.MaxPlayerId; id++)
                if (!occupied.Contains(id)) return id;
            return 0;
        }

        void BroadcastRoom()
        {
            if (GamePauseController.Instance != null)
            {
                var pause = new OnlinePauseMessage { state = GamePauseController.Instance.CaptureOnlinePause() };
                foreach (var peer in NetworkServer.connections.Values)
                    if (peer != null && peer != NetworkServer.localConnection) peer.Send(pause);
            }
            RoomPlayers = new List<int>(playerIdByConnection.Values).ToArray();
            Array.Sort(RoomPlayers);
            var menu = InConfiguration ? FindAnyObjectByType<BattleModeMenu>(FindObjectsInactive.Include) : null;
            MenuView = menu != null ? menu.CaptureOnlineView() : "";
            var characters = InConfiguration ? FindAnyObjectByType<BomberSkinSelectMenu>(FindObjectsInactive.Include) : null;
            CharacterView = characters != null ? characters.CaptureOnlineView() : "";
            string settings = global::SaveSystem.CaptureOnlineBattleSettings();
            foreach (var connection in NetworkServer.connections.Values)
            {
                if (connection == null || !playerIdByConnection.TryGetValue(connection.connectionId, out int id)) continue;
                if (connection == NetworkServer.localConnection) { LocalRoomPlayerId = id; continue; }
                connection.Send(new OnlineRoomMessage { playerId = id, players = RoomPlayers, mode = RoomMode,
                    settings = settings, menu = MenuView, characters = CharacterView });
            }
        }

        public static void ServerBeginConfiguration()
        {
            if (!CanConfigure || RoomPlayers.Length < 2 || singleton is not BombermanNetworkManager manager) return;
            var modes = global::SaveSystem.GetBattleModePlayerControlModes();
            for (int i = 0; i < modes.Length; i++)
                modes[i] = IsConnectedSlot(i + 1) ? BattleModePlayerControlMode.Man : BattleModePlayerControlMode.Off;
            global::SaveSystem.SetBattleModePlayerControlModes(modes);
            manager.BroadcastRoom();
            singleton.ServerChangeScene(ConfigurationScene);
        }

        public static void ServerLaunchConfiguredBattle(string scene)
        {
            if (!CanConfigure || singleton is not BombermanNetworkManager manager || RoomPlayers.Length < 2) return;
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

        public static void LeaveRoom()
        {
            if (singleton is not BombermanNetworkManager manager || manager.leavingRoom) return;
            manager.leavingRoom = true;
            if (NetworkServer.active && NetworkClient.active) manager.StopHost();
            else if (NetworkServer.active) manager.StopServer();
            else manager.StopClient();
            ClearRoom();
            Destroy(manager.gameObject);
            SceneManager.LoadScene("TitleScreen");
        }

        static void ClearRoom()
        {
            LocalRoomPlayerId = 0;
            RoomPlayers = Array.Empty<int>();
            MenuView = CharacterView = "";
            global::SaveSystem.EndOnlineBattleSettings();
            PlayerPersistentStats.ResetSessionForReturnToTitle();
            for (int id = 1; id <= GameSession.MaxPlayerId; id++) PlayerInputManager.Instance?.ClearSyntheticPlayer(id);
            GameSession.Instance?.EndBattleMatch();
            GamePauseController.ClearPauseFlag();
            Time.timeScale = 1f;
            NetSync.IsNetworkedScene = false;
        }

        public override void OnServerConnect(NetworkConnectionToClient connection)
        {
            if (!InLobbyScene() || playerIdByConnection.Count >= GameSession.MaxPlayerId)
            { connection.Disconnect(); return; }
            base.OnServerConnect(connection);
        }

        public override void OnClientDisconnect()
        {
            base.OnClientDisconnect();
            if (!leavingRoom && !NetworkServer.active) LeaveRoom();
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
            roundPlayersReady = false;
            roundSpawnedPlayerIds.Clear();
            GetComponent<OnlineLobbyMenu>()?.ReleaseRoomEventSystem();
            base.OnServerChangeScene(newSceneName);
        }

        public override void OnClientChangeScene(string newSceneName, SceneOperation operation, bool customHandling)
        {
            MenuView = CharacterView = "";
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
            if (!computer) return;
            if (player.TryGetComponent<MovementController>(out var movement)) movement.SetPredictLocally(true);
            if (player.TryGetComponent<NetworkPlayerAnimation>(out var animation)) animation.syncDirection = SyncDirection.ServerToClient;
            if (player.TryGetComponent<NetworkTransformReliable>(out var transformSync)) transformSync.syncDirection = SyncDirection.ServerToClient;
        }
    }
}

#if UNITY_EDITOR || DEBUG
using System;
using System.Collections.Generic;
using Mirror;
using kcp2k;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Assets.Scripts.Netcode
{
    // Development-only command-line harness. No remote message can enable it.
    public partial class BombermanNetworkManager
    {
        bool loadTestConfigured, loadTestEnabled, loadTestStarted, loadTestSpawnsChecked;
        int loadTestPlayers;
        float loadTestStart, loadTestDuration, loadTestNextHealth;

        void TickLocalLoadTest()
        {
            if (!dedicatedServer || !NetworkServer.active) return;
            if (!loadTestConfigured)
            {
                loadTestConfigured = true;
                loadTestEnabled = Debug.isDebugBuild && HasArg("-load-test-server");
                if (!loadTestEnabled) return;
                var args = Environment.GetCommandLineArgs();
                loadTestPlayers = ReadDedicatedNumber(args, "-load-test-players", RoomCapacity, 2, RoomCapacity);
                loadTestDuration = ReadDedicatedNumber(args, "-load-test-duration", 180, 30, 1800);
                loadTestStart = Time.realtimeSinceStartup;
                Debug.Log("[LoadTest] Synthetic durable actors; network controls at 30 Hz. Not normal game rules.");
            }
            if (!loadTestEnabled) return;
            if (Time.realtimeSinceStartup - loadTestStart > loadTestDuration)
            {
                Debug.Log("[LoadTest] completed");
                leavingRoom = true; // Mark shutdown before StopServer disconnect callbacks.
                StopServer(); Application.Quit(); return;
            }
            if (!loadTestStarted && InLobbyScene() && RoomPlayers.Length == loadTestPlayers)
            {
                loadTestStarted = true;
                if (IsCoopRoom)
                {
                    var args = Environment.GetCommandLineArgs();
                    int index = Array.IndexOf(args, "-load-test-scene");
                    string scene = index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
                    if (scene != null && (!scene.StartsWith("Stage_", StringComparison.Ordinal) ||
                        SceneUtility.GetBuildIndexByScenePath("Assets/Scenes/" + scene + ".unity") < 0))
                        throw new ArgumentException("Load-test scene must be an enabled stage.");
                    ServerStartCoop(scene);
                }
                else
                {
                    PlayerPersistentStats.ResetToDefaultsAll();
                    var modes = global::SaveSystem.GetBattleModePlayerControlModes();
                    bool fillCom = HasArg("-load-test-com");
                    if (fillCom) global::SaveSystem.SetBattleModeComputerLevel(BattleModeComputerLevel.Hard);
                    for (int i = 0; i < modes.Length; i++) modes[i] = IsConnectedSlot(i + 1) ? BattleModePlayerControlMode.Man :
                        fillCom ? BattleModePlayerControlMode.Com : BattleModePlayerControlMode.Off;
                    global::SaveSystem.SetBattleModePlayerControlModes(modes);
                    GameSession.Instance?.SetActivePlayerIds(RoomPlayers);
                    ServerLaunchConfiguredBattle("BattleMode_15");
                }
            }
            // Keep combat running rather than measuring an empty arena after bot deaths.
            if (loadTestStarted && !InLobbyScene() && Time.unscaledTime >= loadTestNextHealth)
            {
                loadTestNextHealth = Time.unscaledTime + 1;
                foreach (var health in FindObjectsByType<CharacterHealth>())
                    if (!health.IsDead) health.life = 200;
                if (!loadTestSpawnsChecked && HasArg("-load-test-spawns") && !IsCoopRoom && Time.timeSinceLevelLoad > 3)
                {
                    loadTestSpawnsChecked = true;
                    StartCoroutine(CheckLoadTestSpawns());
                }
            }
        }

        System.Collections.IEnumerator CheckLoadTestSpawns()
        {
            var manager = FindAnyObjectByType<GameManager>();
            var spawner = FindAnyObjectByType<PlayersSpawner>();
            var prefabs = new[] { Resources.Load<GameObject>("Items/LineBomb"),
                spawner != null ? spawner.GetPlayerMountPrefabForType(MountedType.Mole) : null,
                spawner != null ? spawner.GetPlayerMountPrefabForType(MountedType.Tank) : null };
            var spawned = new List<GameObject>();
            for (int i = 0; i < prefabs.Length; i++)
            {
                if (manager == null || prefabs[i] == null) throw new InvalidOperationException("Spawn regression fixture unavailable");
                Vector3 position = new Vector3(30 + i * 2, 30, 0);
                var instance = Instantiate(prefabs[i], position, Quaternion.identity);
                manager.PrepareSpawnedHiddenObject(instance, prefabs[i], position);
                NetSpawn.Server(instance);
                spawned.Add(instance);
                Debug.Log("[LoadTest] spawn regression revealed=" + prefabs[i].name);
            }
            if (HasArg("-load-test-full-input"))
            {
                yield return new WaitForSecondsRealtime(7);
                var mountedPlayers = new List<PlayerMountCompanion>();
                var equippedMounts = new List<GameObject>();
                foreach (var connection in NetworkServer.connections.Values)
                {
                    if (connection.identity == null) continue;
                    var companion = connection.identity.GetComponent<PlayerMountCompanion>();
                    if (companion == null) throw new InvalidOperationException("Mount input fixture missing player companion");
                    var type = mountedPlayers.Count == 0 ? MountedType.Mole : MountedType.Tank;
                    companion.RestoreMounted(type);
                    companion.ApplyRidingTransitionInvulnerability(15f, false, companion.GetMountedLouieObject());
                    mountedPlayers.Add(companion);
                    equippedMounts.Add(companion.GetMountedLouieObject());
                    Debug.Log("[LoadTest] full-input equipped=" + type);
                }
                float deadline = Time.unscaledTime + 8;
                while (Time.unscaledTime < deadline)
                {
                    bool allDismounted = true;
                    foreach (var companion in mountedPlayers)
                        allDismounted &= companion.GetMountedLouieType() == MountedType.None;
                    // Keep COM from recollecting the fixture before it is audited.
                    foreach (var mount in equippedMounts)
                        if (mount != null && mount.TryGetComponent<MountWorldPickup>(out _) &&
                            mount.TryGetComponent<Collider2D>(out var collider)) collider.enabled = false;
                    if (allDismounted) break;
                    yield return null;
                }
                yield return null; // newly added pickup components register in Start.
                foreach (var companion in mountedPlayers)
                    if (companion.GetMountedLouieType() != MountedType.None)
                    {
                        var movement = companion.GetComponent<MovementController>();
                        var manual = companion.GetComponent<PlayerManualDismount>();
                        var rider = companion.GetComponent<PlayerRidingController>();
                        throw new InvalidOperationException($"Network Select did not dismount player={movement.PlayerId} " +
                            $"manualEnabled={manual != null && manual.enabled} locked={movement.InputLocked} " +
                            $"mounted={movement.IsMounted} riding={rider != null && rider.IsPlaying}");
                    }
                foreach (var mount in equippedMounts)
                {
                    var pickup = mount != null ? mount.GetComponent<MountWorldPickup>() : null;
                    if (pickup == null || pickup.NetworkPresentation == null || pickup.NetworkPresentation.netId == 0)
                        throw new InvalidOperationException("Dismounted world pickup was not replicated");
                    spawned.Add(mount);
                    Debug.Log("[LoadTest] full-input world pickup replicated type=" + pickup.Type);
                }
                Debug.Log("[LoadTest] full-input dismount confirmed players=" + mountedPlayers.Count);
            }
            yield return new WaitForSecondsRealtime(10);
            foreach (var instance in spawned) NetSpawn.Despawn(instance);
            yield return null;
            Debug.Log("[LoadTest] spawn regression removed LineBomb/Mole/Tank");
        }
    }

    // Test-only synthetic hardware input on an otherwise complete Mirror client.
    // No custom server command: Select travels through NetworkPlayerInput.
    public sealed class LocalNetworkInputSmoke : MonoBehaviour
    {
        MountedType observedMount;
        float mountedAt, releaseTime;
        bool sent, completed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!Debug.isDebugBuild || Array.IndexOf(Environment.GetCommandLineArgs(), "-load-test-real-input") < 0) return;
            Application.runInBackground = true;
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            var root = new GameObject("LocalNetworkInputSmoke");
            DontDestroyOnLoad(root);
            root.AddComponent<LocalNetworkInputSmoke>();
        }

        void Update()
        {
            if (completed || !NetworkClient.isConnected || NetworkClient.localPlayer == null) return;
            var companion = NetworkClient.localPlayer.GetComponent<PlayerMountCompanion>();
            var input = PlayerInputManager.Instance;
            if (companion == null || input == null) return;
            var current = companion.GetMountedLouieType();
            if (observedMount == MountedType.None)
            {
                if (current != MountedType.Mole && current != MountedType.Tank) return;
                observedMount = current; mountedAt = Time.unscaledTime;
            }
            if (!sent && Time.unscaledTime - mountedAt >= 2f)
            {
                sent = true; releaseTime = Time.unscaledTime + 0.5f;
                input.SetSyntheticHeld(1, PlayerAction.Select, true);
                Debug.Log("[LoadTest] full-input Select sent type=" + observedMount + " held=" + input.Get(1, PlayerAction.Select));
            }
            if (sent && Time.unscaledTime >= releaseTime)
            {
                input.SetSyntheticHeld(1, PlayerAction.Select, false);
                if (current == MountedType.None)
                {
                    completed = true;
                    Debug.Log("[LoadTest] full-input dismount acknowledged type=" + observedMount);
                }
            }
        }
    }

    // Many real KCP peers in one process; receives/acknowledges every packet but
    // does not load, instantiate or render the replicated client world.
    public sealed class LocalServerLoadTest : MonoBehaviour
    {
        readonly List<Peer> peers = new();
        float started, duration, nextReport;
        bool configureWhenReady, configurationSent;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!Debug.isDebugBuild || Array.IndexOf(Environment.GetCommandLineArgs(), "-load-test-client") < 0) return;
            SceneManager.LoadScene(NetworkLobby.SceneName);
            var root = new GameObject("LocalServerLoadTest");
            DontDestroyOnLoad(root);
            root.AddComponent<LocalServerLoadTest>();
        }

        void Start()
        {
            var args = Environment.GetCommandLineArgs();
            configureWhenReady = Array.IndexOf(args, "-load-test-configure") >= 0;
            int first = BombermanNetworkManager.ReadDedicatedNumber(args, "-load-test-port", 18777, 1024, 65000);
            int rooms = BombermanNetworkManager.ReadDedicatedNumber(args, "-load-test-rooms", 1, 1, 32);
            int players = BombermanNetworkManager.ReadDedicatedNumber(args, "-load-test-players", 4, 2, 6);
            duration = BombermanNetworkManager.ReadDedicatedNumber(args, "-load-test-duration", 180, 30, 1800);
            var prefab = NetworkManager.singleton.playerPrefab;
            byte inputIndex = (byte)Array.FindIndex(prefab.GetComponents<NetworkBehaviour>(), b => b is NetworkPlayerInput);
            for (int room = 0; room < rooms; room++)
                for (int player = 0; player < players; player++) peers.Add(new Peer((ushort)(first + room), player, inputIndex));
            started = Time.realtimeSinceStartup;
            Application.runInBackground = true; Application.targetFrameRate = 60; QualitySettings.vSyncCount = 0;
        }

        void Update()
        {
            foreach (var peer in peers) peer.Tick();
            if (configureWhenReady && !configurationSent && peers.TrueForAll(peer => peer.RoomReady))
            {
                configurationSent = true;
                foreach (var peer in peers) peer.ConfigureRoom();
            }
            if (Time.realtimeSinceStartup >= nextReport)
            {
                nextReport = Time.realtimeSinceStartup + 5;
                int connected = 0, spawned = 0; long received = 0;
                foreach (var peer in peers) { if (peer.client.connected) connected++; if (peer.netId != 0) spawned++; received += peer.received; }
                Debug.Log($"[LoadClients] connected={connected}/{peers.Count} spawned={spawned} receivedBytes={received}");
            }
            if (Time.realtimeSinceStartup - started >= duration) Application.Quit();
        }

        void OnDestroy() { foreach (var peer in peers) peer.client.Disconnect(); }

        sealed class Peer
        {
            public readonly KcpClient client;
            public uint netId;
            public long received;
            readonly Unbatcher unbatcher = new();
            readonly Batcher batcher = new(1200);
            readonly int player;
            readonly byte inputIndex;
            float nextInput, nextBomb;
            bool authenticated, controlsPaused;
            bool roomOwner;
            int roomPlayers;
            // Lobby slots precede gameplay objects. Requiring a spawned netId
            // here deadlocks a normal release room before its owner can start.
            public bool RoomReady => client.connected && authenticated && roomPlayers >= 2;

            public void ConfigureRoom()
            {
                if (roomOwner) Send(new OnlineConfigureRoomMessage());
            }
            // Cecil full signature used by Mirror's weaver (verified via the server delegate).
            static readonly ushort ControlsHash = (ushort)("System.Void Assets.Scripts.Netcode.NetworkPlayerInput::CmdControls(System.Int32,System.Int32)".GetStableHashCode() & 0xffff);

            public Peer(ushort port, int player, byte inputIndex)
            {
                this.player = player; this.inputIndex = inputIndex;
                client = new KcpClient(() => Send(new OnlineVersionAuthenticator.Request { gameVersion = Application.version, protocol = OnlineVersionAuthenticator.ProtocolVersion }),
                    Receive, () => Debug.Log("[LoadClients] disconnected port=" + port),
                    (error, message) => Debug.LogWarning("[LoadClients] " + error + ": " + message),
                    new KcpConfig(DualMode: false, SendWindowSize: 4096, ReceiveWindowSize: 4096));
                client.Connect("127.0.0.1", port);
            }

            void Send<T>(T message) where T : struct, NetworkMessage
            {
                using var writer = NetworkWriterPool.Get();
                NetworkMessages.Pack(message, writer);
                batcher.AddMessage(writer.ToArraySegment(), Time.realtimeSinceStartupAsDouble);
            }

            void Receive(ArraySegment<byte> data, KcpChannel channel)
            {
                received += data.Count;
                if (!unbatcher.AddBatch(data)) throw new InvalidOperationException("Invalid server batch");
                while (unbatcher.GetNextMessage(out var message, out _))
                {
                    using var reader = NetworkReaderPool.Get(message);
                    if (!NetworkMessages.UnpackId(reader, out ushort id)) continue;
                    if (id == NetworkMessages.GetId<OnlineVersionAuthenticator.Response>())
                    {
                        authenticated = reader.Read<OnlineVersionAuthenticator.Response>().compatible;
                        if (authenticated) { Send(new ReadyMessage()); Send(new AddPlayerMessage()); }
                    }
                    else if (id == NetworkMessages.GetId<OnlineRoomMessage>())
                    {
                        var room = reader.Read<OnlineRoomMessage>();
                        roomOwner = room.playerId > 0 && room.playerId == room.adminPlayerId;
                        roomPlayers = room.players?.Length ?? 0;
                    }
                    else if (id == NetworkMessages.GetId<NotReadyMessage>())
                    {
                        // Match a real Mirror client's input gate while the
                        // server prepares a scene change; the next spawn restores it.
                        netId = 0;
                    }
                    else if (id == NetworkMessages.GetId<RoundOverMessage>())
                    {
                        reader.Read<RoundOverMessage>();
                        controlsPaused = true;
                    }
                    else if (id == NetworkMessages.GetId<SceneMessage>())
                    {
                        reader.Read<SceneMessage>(); netId = 0; controlsPaused = false;
                        Send(new ReadyMessage()); Send(new AddPlayerMessage());
                    }
                    else if (id == NetworkMessages.GetId<SpawnMessage>())
                    {
                        var spawn = reader.Read<SpawnMessage>();
                        if (spawn.isLocalPlayer) netId = spawn.netId;
                    }
                    else if (id == NetworkMessages.GetId<NetworkPingMessage>())
                        Send(new NetworkPongMessage(reader.Read<NetworkPingMessage>().localTime, 0, 0));
                    else if (id == NetworkMessages.GetId<ObjectDestroyMessage>())
                    {
                        if (reader.Read<ObjectDestroyMessage>().netId == netId) netId = 0;
                    }
                }
            }

            public void Tick()
            {
                client.TickIncoming();
                float now = Time.realtimeSinceStartup;
                if (authenticated && !controlsPaused && netId != 0 && now >= nextInput)
                {
                    nextInput = now + 1f / 30f;
                    int direction = ((int)(now * 0.7f) + player) % 4;
                    int pressed = now >= nextBomb ? 16 : 0;
                    if (pressed != 0) nextBomb = now + 1.2f;
                    using var payload = NetworkWriterPool.Get();
                    payload.WriteInt(1 << direction); payload.WriteInt(pressed);
                    Send(new CommandMessage { netId = netId, componentIndex = inputIndex, functionHash = ControlsHash, payload = payload.ToArraySegment() });
                    Send(new TimeSnapshotMessage());
                }
                using (var batch = NetworkWriterPool.Get())
                    while (batcher.GetBatch(batch)) { client.Send(batch.ToArraySegment(), KcpChannel.Reliable); batch.Reset(); }
                client.TickOutgoing();
            }
        }
    }
}
#endif

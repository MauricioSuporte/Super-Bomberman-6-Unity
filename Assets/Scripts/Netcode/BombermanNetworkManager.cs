using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace Assets.Scripts.Netcode
{
    /// <summary>
    /// F5a — anúncio de fim de round (host → clientes). kind: 0=vitória,
    /// 1=empate, 2=empate por tempo. winnerId válido só quando kind==0.
    /// </summary>
    public struct RoundOverMessage : NetworkMessage
    {
        public int winnerId;
        public byte kind;
        public int[] activePlayerIds; // F5a-3: players na partida (linhas do placar)
        public int[] wins;            // F5a-3: vitórias por player (paralelo ao acima)
    }

    /// <summary>F5a — relógio de round replicado (host → clientes).</summary>
    public struct RoundTimerMessage : NetworkMessage
    {
        public float remaining;
        public bool hasLimit;
    }

    /// <summary>
    /// NetworkManager da POC de Battle Mode online (host-autoritativo).
    ///
    /// Responsabilidades:
    ///   - Manter <see cref="NetSync.Mode"/> coerente com o estado da rede.
    ///   - Atribuir um playerId (1..6) determinístico por conexão. O host
    ///     é sempre o player 1.
    ///   - Spawnar o Player na posição de spawn resolvida pelo PlayersSpawner
    ///     da cena (reaproveita o layout já existente do Battle Mode).
    ///   - Refletir os players ativos no GameSession para HUD/regras.
    ///
    /// Wiring no Editor: ver Docs/multiplayer/mirror-battlemode-poc.md.
    /// </summary>
    public class BombermanNetworkManager : NetworkManager
    {
        // Conexão -> playerId atribuído (lado servidor).
        readonly Dictionary<int, int> playerIdByConnection = new();

        // ---- Ciclo de vida / modo ----------------------------------------

        public override void Awake()
        {
            base.Awake();
            // A cena tem um NetworkManager → é destinada a rede. Impede o gameplay
            // (ex.: relógio de round) de rodar antes de clicar Host/Client.
            NetSync.IsNetworkedScene = true;
        }

        public override void OnDestroy()
        {
            // Só o singleton real limpa a flag. Ao recarregar a cena para o próximo
            // round (F5b), o Mirror cria um clone do NetworkManager (objeto de cena)
            // e o destrói via singleton; esse clone NÃO pode limpar a flag.
            if (NetworkManager.singleton == null || NetworkManager.singleton == this)
                NetSync.IsNetworkedScene = false;

            base.OnDestroy(); // cleanup do Mirror (singleton/estáticos)
        }

        // F5c — host inicia a partida a partir do lobby (troca coordenada de cena).
        public static void ServerStartMatch(string battleSceneName)
        {
            if (!NetworkServer.active || singleton == null)
                return;
            if (string.IsNullOrWhiteSpace(battleSceneName))
                return;

            singleton.ServerChangeScene(battleSceneName);
        }

        // F5c — fim de partida: volta todos ao lobby (a sessão continua ativa).
        public static void ServerReturnToLobby()
        {
            if (!NetworkServer.active || singleton == null)
                return;

            singleton.ServerChangeScene(NetworkLobby.SceneName);
        }

        // F5b — inicia o próximo round: recarrega a cena atual de forma coordenada
        // (o Mirror leva os clientes junto e, com autoCreatePlayer, re-spawna os
        // players das conexões existentes — AllocatePlayerId devolve o mesmo id
        // porque playerIdByConnection sobrevive ao reload, NM é dontDestroyOnLoad).
        public static void ServerStartNextRound()
        {
            if (!NetworkServer.active || singleton == null)
                return;

            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            singleton.ServerChangeScene(scene);
        }

        public override void OnStartHost()
        {
            base.OnStartHost();
            NetSync.Mode = NetSync.NetMode.Host;
        }

        public override void OnStopHost()
        {
            base.OnStopHost();
            NetSync.Reset();
        }

        // OnStartClient roda tanto no host quanto no client puro; só forçamos
        // Client quando NÃO somos o servidor (host já foi setado acima).
        public override void OnStartClient()
        {
            base.OnStartClient();
            if (!NetworkServer.active)
            {
                NetSync.Mode = NetSync.NetMode.Client;
                DestroyPreExistingLocalPlayers();
                // F5a — cliente puro escuta os anúncios do host.
                NetworkClient.RegisterHandler<RoundOverMessage>(OnRoundOverMessage);
                NetworkClient.RegisterHandler<RoundTimerMessage>(OnRoundTimerMessage);
            }
        }

        static void OnRoundOverMessage(RoundOverMessage msg)
        {
            // F5a-3 — aplica players ativos + placar replicados antes do scoreboard
            // (o presenter lê do GameSession; o cliente não tinha esses dados).
            if (GameSession.Instance != null && msg.activePlayerIds != null)
            {
                GameSession.Instance.SetActivePlayerIds(new List<int>(msg.activePlayerIds));
                if (msg.wins != null)
                {
                    int n = Mathf.Min(msg.activePlayerIds.Length, msg.wins.Length);
                    for (int i = 0; i < n; i++)
                        GameSession.Instance.SetBattleMatchWins(msg.activePlayerIds[i], msg.wins[i]);
                }
            }

            if (GameManager.Instance != null)
                GameManager.Instance.PlayOnlineRoundOver(msg.winnerId, msg.kind);
        }

        static void OnRoundTimerMessage(RoundTimerMessage msg)
        {
            if (GameManager.Instance != null)
                GameManager.Instance.ApplyNetworkTimer(msg.remaining, msg.hasLimit);
        }

        // F5a — host replica o relógio de round aos clientes remotos.
        public static void ServerBroadcastTimer(float remaining, bool hasLimit)
        {
            if (!NetworkServer.active)
                return;

            var msg = new RoundTimerMessage { remaining = remaining, hasLimit = hasLimit };
            foreach (var conn in NetworkServer.connections.Values)
            {
                if (conn == null || conn == NetworkServer.localConnection)
                    continue;
                conn.Send(msg);
            }
        }

        // F5a — host anuncia o fim de round APENAS aos clientes remotos (o host
        // já toca a apresentação localmente). Custom message evita depender de um
        // objeto de cena com NetworkIdentity.
        public static void ServerBroadcastRoundOver(int winnerId, byte kind)
        {
            if (!NetworkServer.active)
                return;

            // F5a-3 — snapshot do placar do host (já pós-vitória) para o cliente.
            var idList = new List<int>();
            if (GameSession.Instance != null)
                GameSession.Instance.GetActivePlayerIds(idList);

            int[] activeIds = idList.ToArray();
            int[] wins = new int[activeIds.Length];
            if (GameSession.Instance != null)
                for (int i = 0; i < activeIds.Length; i++)
                    wins[i] = GameSession.Instance.GetBattleMatchWins(activeIds[i]);

            var msg = new RoundOverMessage
            {
                winnerId = winnerId,
                kind = kind,
                activePlayerIds = activeIds,
                wins = wins,
            };

            foreach (var conn in NetworkServer.connections.Values)
            {
                if (conn == null || conn == NetworkServer.localConnection)
                    continue;
                conn.Send(msg);
            }
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (!NetworkServer.active)
                NetSync.Reset();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            playerIdByConnection.Clear();
            DestroyPreExistingLocalPlayers();
        }

        // Remove players spawnados localmente (offline, via PlayersSpawner na
        // intro da fase) antes de entrar online, para não coexistirem com os
        // players replicados pela rede. Players já networked (com
        // NetworkIdentity) são preservados.
        static void DestroyPreExistingLocalPlayers()
        {
            var ids = FindObjectsByType<PlayerIdentity>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < ids.Length; i++)
            {
                if (ids[i] == null)
                    continue;
                if (ids[i].GetComponent<NetworkIdentity>() != null)
                    continue;
                Destroy(ids[i].gameObject);
            }
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            playerIdByConnection.Clear();
        }

        // ---- Spawn de players --------------------------------------------

        public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            int playerId = AllocatePlayerId(conn);

            // F5c — no lobby não spawnamos player (só registramos a conexão/id). O
            // player nasce quando a partida começa: o ServerChangeScene para a cena
            // de batalha deixa localPlayer == null e o Mirror re-chama OnServerAddPlayer
            // (autoCreatePlayer) já na cena certa.
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == NetworkLobby.SceneName)
            {
                SyncActivePlayersToSession();
                return;
            }

            Vector3 spawnPos = ResolveSpawnPosition(playerId);
            GameObject player = Instantiate(playerPrefab, spawnPos, Quaternion.identity);

            if (player.TryGetComponent<NetworkPlayerSetup>(out var setup))
                setup.ServerAssignPlayerId(playerId);
            else
                Debug.LogError("[Net] playerPrefab sem NetworkPlayerSetup.");

            NetworkServer.AddPlayerForConnection(conn, player);

            SyncActivePlayersToSession();
        }

        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            if (playerIdByConnection.TryGetValue(conn.connectionId, out _))
                playerIdByConnection.Remove(conn.connectionId);

            base.OnServerDisconnect(conn); // destrói os objetos do player
            SyncActivePlayersToSession();
        }

        int AllocatePlayerId(NetworkConnectionToClient conn)
        {
            if (playerIdByConnection.TryGetValue(conn.connectionId, out int existing))
                return existing;

            var used = new HashSet<int>(playerIdByConnection.Values);
            int assigned = GameSession.MinPlayerId;
            for (int id = GameSession.MinPlayerId; id <= GameSession.MaxPlayerId; id++)
            {
                if (!used.Contains(id))
                {
                    assigned = id;
                    break;
                }
            }

            playerIdByConnection[conn.connectionId] = assigned;
            return assigned;
        }

        Vector3 ResolveSpawnPosition(int playerId)
        {
            var spawner = FindAnyObjectByType<PlayersSpawner>();
            if (spawner != null)
                return spawner.GetResolvedSpawnPosition(playerId);

            // Fallback simples se a cena não tiver PlayersSpawner.
            return new Vector3(playerId * 2f - 6f, 0f, 0f);
        }

        void SyncActivePlayersToSession()
        {
            if (GameSession.Instance == null)
                return;

            var ids = new List<int>(playerIdByConnection.Values);
            if (ids.Count == 0)
                ids.Add(GameSession.MinPlayerId);

            GameSession.Instance.SetActivePlayerIds(ids);
        }
    }
}

using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace Assets.Scripts.Netcode
{
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

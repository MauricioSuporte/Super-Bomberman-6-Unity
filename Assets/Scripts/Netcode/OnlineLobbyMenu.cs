using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Assets.Scripts.Netcode
{
    /// <summary>
    /// F5c — menu online mínimo (bootstrap). Vive no GameObject do NetworkManager
    /// (dontDestroyOnLoad), então sobrevive às trocas de cena e centraliza o fluxo:
    ///
    ///   - Ocioso (sem sessão): Host / Join (IP).
    ///   - Servidor ativo NO LOBBY: Start match (host) + Stop.
    ///   - Em partida: só status.
    ///   - Cliente: status.
    ///
    /// O host fica no lobby depois de "Host" (permite o cliente conectar antes),
    /// e "Start match" faz o ServerChangeScene para a cena de batalha. O fim de
    /// partida volta ao lobby (GameManager → BombermanNetworkManager.ServerReturnToLobby).
    ///
    /// UI em OnGUI de propósito (mínimo funcional, zero wiring de Canvas); pode
    /// virar uma tela uGUI depois.
    /// </summary>
    public class OnlineLobbyMenu : MonoBehaviour
    {
        [SerializeField] string battleSceneName = "BattleMode_1";
        [SerializeField] string address = "localhost";

        // F5d — teste de internet: permite subir um host sem GUI (servidor headless)
        // via linha de comando: "-autohost" (hospeda e já entra na partida). Útil
        // para rodar o host numa máquina remota e conectar clientes de fora.
        void Start()
        {
            var nm = NetworkManager.singleton;
            if (nm == null || NetworkServer.active || NetworkClient.active)
                return;

            // Servidor DEDICADO (sem jogador local): fica no lobby e o
            // BombermanNetworkManager auto-inicia a partida quando há jogadores.
            if (HasArg("-server"))
            {
                Debug.Log("[Net] -server: iniciando servidor dedicado (aguardando jogadores)");
                nm.StartServer();
                return;
            }

            // Host de teste headless: hospeda e já entra na partida.
            if (HasArg("-autohost"))
            {
                Debug.Log("[Net] -autohost: iniciando host + partida (" + battleSceneName + ")");
                nm.StartHost();
                Invoke(nameof(AutoStartMatch), 1.5f);
            }
        }

        void AutoStartMatch()
        {
            BombermanNetworkManager.ServerStartMatch(battleSceneName);
        }

        static bool HasArg(string arg)
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
                if (string.Equals(args[i], arg, System.StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        void OnGUI()
        {
            NetworkManager nm = NetworkManager.singleton;
            if (nm == null)
                return;

            GUILayout.BeginArea(new Rect(16, 16, 300, 200), GUI.skin.box);

            bool serverActive = NetworkServer.active;
            bool clientActive = NetworkClient.active;

            if (!serverActive && !clientActive)
                DrawIdle(nm);
            else if (serverActive)
                DrawServer(nm);
            else
                DrawClient(nm);

            GUILayout.EndArea();
        }

        void DrawIdle(NetworkManager nm)
        {
            GUILayout.Label("Battle Mode — Online");

            if (GUILayout.Button("Host"))
                nm.StartHost();

            GUILayout.BeginHorizontal();
            GUILayout.Label("IP", GUILayout.Width(24));
            address = GUILayout.TextField(address);
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Join"))
            {
                nm.networkAddress = address;
                nm.StartClient();
            }
        }

        void DrawServer(NetworkManager nm)
        {
            bool inLobby = SceneManager.GetActiveScene().name == NetworkLobby.SceneName;

            if (inLobby)
            {
                GUILayout.Label("Lobby — host ativo");
                GUILayout.Label("Jogadores conectados: " + NetworkServer.connections.Count);

                if (GUILayout.Button("Start match (" + battleSceneName + ")"))
                    BombermanNetworkManager.ServerStartMatch(battleSceneName);
            }
            else
            {
                GUILayout.Label("Em partida (host)");
            }

            if (GUILayout.Button("Stop"))
            {
                if (NetworkClient.active)
                    nm.StopHost();
                else
                    nm.StopServer();
            }
        }

        void DrawClient(NetworkManager nm)
        {
            GUILayout.Label(NetworkClient.isConnected ? "Conectado ao host" : "Conectando...");
            if (GUILayout.Button("Stop"))
                nm.StopClient();
        }
    }
}

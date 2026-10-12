namespace Assets.Scripts.Netcode
{
    /// <summary>
    /// Ponte de estado de rede SEM dependência do Mirror.
    ///
    /// Os controllers de gameplay (MovementController, BombController,
    /// PlayersSpawner, etc.) consultam apenas esta classe. Assim o
    /// Assembly-CSharp continua compilando mesmo que o Mirror seja
    /// removido, e a lógica de simulação não precisa conhecer o netcode.
    ///
    /// Quem escreve <see cref="Mode"/> é o BombermanNetworkManager
    /// (esse sim depende do Mirror), nos callbacks de start/stop.
    ///
    /// Modelo host-autoritativo da POC:
    ///   - Offline: single-player/local normal -> simula localmente.
    ///   - Host:    este processo é servidor + cliente local -> simula.
    ///   - Client:  cliente puro -> NÃO simula; só renderiza o estado
    ///              replicado (NetworkTransform + spawns do servidor).
    /// </summary>
    public static class NetSync
    {
        public enum NetMode
        {
            Offline = 0,
            Host = 1,
            Client = 2
        }

        public static NetMode Mode { get; set; } = NetMode.Offline;

        /// <summary>True em Offline e Host. Falso em Client puro.</summary>
        public static bool ShouldSimulateLocally => Mode != NetMode.Client;

        /// <summary>Há uma sessão online ativa (host ou client).</summary>
        public static bool IsOnline => Mode != NetMode.Offline;

        /// <summary>Este processo tem autoridade de servidor (host).</summary>
        public static bool IsServer => Mode == NetMode.Host;

        /// <summary>
        /// A cena atual tem um NetworkManager (é destinada a rede), mesmo que
        /// ainda não se tenha clicado Host/Client. Usado para não rodar gameplay
        /// (ex.: o relógio de round) antes da partida online começar. O
        /// BombermanNetworkManager seta isto conforme existe na cena.
        /// </summary>
        public static bool IsNetworkedScene { get; set; }

        public static void Reset() => Mode = NetMode.Offline;
    }
}

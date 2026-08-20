using Mirror;
using UnityEngine;

namespace Assets.Scripts.Netcode
{
    /// <summary>
    /// F5d — inicialização headless robusta para o build de Dedicated Server.
    ///
    /// O build de Dedicated Server REMOVE o módulo IMGUI, então o OnGUI (e o
    /// auto-start no Start) do OnlineLobbyMenu não rodam de forma confiável.
    /// Aqui o "-server" é tratado em RuntimeInitializeOnLoadMethod(AfterSceneLoad):
    /// roda uma vez depois que a primeira cena carregou, com NetworkManager.singleton
    /// já garantido — sem depender de IMGUI nem da ordem de Start dos componentes.
    /// Só age se o argumento estiver presente, então é inócuo no jogo normal.
    /// </summary>
    public static class HeadlessBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var args = System.Environment.GetCommandLineArgs();
            if (!HasArg(args, "-server"))
                return;

            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var nm = NetworkManager.singleton;
            var found = Object.FindObjectsByType<NetworkManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var mgr = nm != null ? nm : (found.Length > 0 ? found[0] : null);

            Debug.Log("[Net] HeadlessBootstrap(-server): scene=" + scene.name
                + " singleton=" + (nm != null)
                + " foundNM=" + found.Length
                + (found.Length > 0 ? " (" + found[0].GetType().Name + ", active=" + found[0].gameObject.activeInHierarchy + ")" : "")
                + " serverActive=" + NetworkServer.active);

            if (mgr != null && !NetworkServer.active && !NetworkClient.active)
                mgr.StartServer();
        }

        static bool HasArg(string[] a, string x)
        {
            for (int i = 0; i < a.Length; i++)
                if (string.Equals(a[i], x, System.StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }
    }
}

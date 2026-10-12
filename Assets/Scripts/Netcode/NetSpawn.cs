using Mirror;
using UnityEngine;

namespace Assets.Scripts.Netcode
{
    /// <summary>
    /// Helper para replicar objetos instanciados pelo servidor (bombas,
    /// explosões, blocos destrutíveis revelados, itens...).
    ///
    /// Uso nos controllers existentes:
    ///
    ///     GameObject bomb = Instantiate(prefab, pos, Quaternion.identity);
    ///     NetSpawn.Server(bomb);   // <- no-op offline; replica quando host
    ///
    /// Regras:
    ///   - Offline: no-op (o Instantiate local basta).
    ///   - Host:    NetworkServer.Spawn -> replica para os clientes.
    ///   - Client:  NUNCA deve instanciar objetos de simulação; se chamar
    ///              aqui, o objeto local é destruído para evitar "fantasmas"
    ///              não-replicados (o objeto verdadeiro vem do servidor).
    ///
    /// O prefab PRECISA ter NetworkIdentity e estar registrado como
    /// spawnable prefab no BombermanNetworkManager (ver guia de wiring).
    /// </summary>
    public static class NetSpawn
    {
        public static void Server(GameObject instance)
        {
            if (instance == null)
                return;

            if (!NetSync.IsOnline)
                return; // offline: instância local já resolve

            if (!NetSync.IsServer)
            {
                // Client puro nunca deveria instanciar simulação.
                Object.Destroy(instance);
                return;
            }

            if (instance.GetComponent<NetworkIdentity>() == null)
            {
                Debug.LogError(
                    $"[NetSpawn] '{instance.name}' não tem NetworkIdentity; " +
                    "não será replicado. Adicione NetworkIdentity ao prefab e " +
                    "registre-o no BombermanNetworkManager.");
                return;
            }

            NetworkServer.Spawn(instance);
        }

        /// <summary>Destrói de forma consistente (replicado no host).</summary>
        public static void Despawn(GameObject instance)
        {
            if (instance == null)
                return;

            if (NetSync.IsServer && instance.GetComponent<NetworkIdentity>() != null)
            {
                NetworkServer.Destroy(instance);
                return;
            }

            if (!NetSync.IsOnline)
                Object.Destroy(instance);
            // Client puro: não destrói manualmente; espera o despawn do servidor.
        }
    }
}

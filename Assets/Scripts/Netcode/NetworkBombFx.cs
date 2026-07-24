using Mirror;
using UnityEngine;

namespace Assets.Scripts.Netcode
{
    /// <summary>Uma célula do visual de explosão a replicar.</summary>
    public struct ExplosionFx
    {
        public Vector2 pos;
        public byte part;   // BombExplosion.ExplosionPart (Start/Middle/End)
        public Vector2 dir;
    }

    /// <summary>
    /// F2 fatia B — replica o VISUAL da explosão (host-autoritativo).
    ///
    /// O objeto BombExplosion NÃO é replicado (usa pool estático incompatível
    /// com o spawn do Mirror). Em vez disso, o host coleta as células visuais
    /// da explosão (centro + braços) e envia por ClientRpc; o cliente recria o
    /// visual localmente usando o mesmo pool, SEM colisão de dano (o dano é
    /// resolvido só no host).
    ///
    /// Vive no Player (dono da bomba) — o BombController do mesmo GameObject
    /// chama <see cref="ServerEmit"/> ao final de ExplodeBomb.
    /// </summary>
    [RequireComponent(typeof(NetworkIdentity))]
    [RequireComponent(typeof(BombController))]
    public class NetworkBombFx : NetworkBehaviour
    {
        BombController bomb;

        void Awake() => bomb = GetComponent<BombController>();

        [Server]
        public void ServerEmit(Vector2 origin, ExplosionFx[] parts, bool pierce, float duration)
        {
            if (parts == null || parts.Length == 0)
                return;
            RpcPlay(origin, parts, pierce, duration);
        }

        [ClientRpc]
        void RpcPlay(Vector2 origin, ExplosionFx[] parts, bool pierce, float duration)
        {
            // No host o visual já foi tocado localmente pela simulação.
            if (isServer || bomb == null)
                return;

            for (int i = 0; i < parts.Length; i++)
                bomb.PlayNetworkExplosionVisual(parts[i].pos, parts[i].part, parts[i].dir, origin, duration, pierce);
        }
    }
}

using Mirror;
using UnityEngine;

namespace Assets.Scripts.Netcode
{
    /// <summary>
    /// F6 (Passo B2) — replica os OVOS que seguem o player em fila (estilo snake).
    ///
    /// A <c>MountEggQueue</c> continua HOST-ONLY e inalterada na lógica: ela
    /// instancia o seguidor localmente e o host o replica via
    /// <c>NetSpawn.Server</c>. O host dirige a POSIÇÃO do seu objeto local (a fila
    /// move os ovos ao longo do rastro do dono) e a NetworkTransform (World,
    /// ServerToClient) replica essa posição aos clientes.
    ///
    /// Nos CLIENTES o seguidor é passivo:
    ///   - FACING: derivado do próprio movimento (delta de posição replicada) →
    ///     <c>EggFollowerDirectionalVisual.ApplyMoveDelta</c> (mesma função que a
    ///     fila usa no host). Não precisa replicar direção.
    ///   - VFX de destruição (omelete/fumaça): o host aciona por uma SyncVar
    ///     ANTES de despawnar; o cliente toca a animação correspondente.
    /// </summary>
    [RequireComponent(typeof(NetworkIdentity))]
    public class NetworkEggFollower : NetworkBehaviour
    {
        // Queue simulation stays on the server; clients display followers and destruction.
        public static bool ReplicationEnabled = true;

        // 0 = vivo; 1 = destruído normal; 2 = destruído por explosão.
        [SyncVar(hook = nameof(OnDestroyKindChanged))] byte destroyKind;

        EggFollowerDirectionalVisual directional;
        EggFollowerDestroyVisual destroyVisual;

        Vector3 lastPos;
        bool hasLastPos;

        void Awake()
        {
            directional = GetComponentInChildren<EggFollowerDirectionalVisual>(true);
            destroyVisual = GetComponentInChildren<EggFollowerDestroyVisual>(true);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (isServer) return;
            foreach (var hitbox in GetComponentsInChildren<Collider2D>(true)) hitbox.enabled = false;
        }

        void LateUpdate()
        {
            // Só o cliente puro deriva o facing do movimento replicado; no host a
            // MountEggQueue já dirige o EggFollowerDirectionalVisual. Ao destruir,
            // para de dirigir (a VFX assume os renderers).
            if (!ReplicationEnabled || !NetSync.IsOnline || !isClient || isServer || directional == null || destroyKind != 0)
                return;

            Vector3 p = transform.position;
            if (!hasLastPos)
            {
                lastPos = p;
                hasLastPos = true;
                return;
            }

            Vector3 delta = p - lastPos;
            lastPos = p;
            directional.ApplyMoveDelta(delta);
        }

        /// <summary>Host: aciona a VFX de destruição nos clientes antes de despawnar.</summary>
        [Server]
        public void ServerBeginDestroyVisual(bool byExplosion)
        {
            byte k = (byte)(byExplosion ? 2 : 1);
            if (destroyKind != k) destroyKind = k;
        }

        void OnDestroyKindChanged(byte _, byte now)
        {
            if (isServer || destroyVisual == null || now == 0)
                return;

            if (now == 2) destroyVisual.PlayExplosionDestroy();
            else          destroyVisual.PlayDestroy();
        }
    }
}

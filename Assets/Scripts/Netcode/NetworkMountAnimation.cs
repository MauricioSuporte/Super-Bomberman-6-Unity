using Mirror;
using UnityEngine;

namespace Assets.Scripts.Netcode
{
    /// <summary>
    /// F6 (Passo B) — Replicação da SAÍDA de animação da MONTARIA (Louie/Mole/
    /// Tank...), no mesmo modelo client-autoritativo do <see cref="NetworkPlayerAnimation"/>.
    ///
    /// A montaria é instanciada em RUNTIME (no host pelo caminho real de mount; no
    /// cliente por PlayerMountCompanion.ShowMountVisualClient a partir da SyncVar
    /// mountType). Por isso o array de renderers NÃO pode ser cacheado no Awake:
    /// é re-derivado sempre que a montaria muda. Como os dois lados instanciam o
    /// MESMO prefab (mountType seleciona o prefab), a ordem de
    /// GetComponentsInChildren fica idêntica → índices alinhados.
    ///
    /// DONO: a montaria dele já anima corretamente (o MountVisualController lê o
    /// facing/direção do MovementController do player, que o dono simula
    /// localmente) — então apenas AMOSTRAMOS o renderer ligado e sincronizamos.
    ///
    /// Cliente remoto (não-dono): o MovementController do clone não é simulado, então
    /// o MountVisualController congelaria em idle-down. Desligamos esse controller e
    /// aplicamos o renderer replicado em modo manual (frame a frame), igual ao player.
    ///
    /// No host (para o player de um cliente) o mount real já anima; deixamos o host
    /// usar a própria animação (não mexemos) — só clientes puros precisam do apply.
    /// </summary>
    [RequireComponent(typeof(NetworkIdentity))]
    [RequireComponent(typeof(PlayerMountCompanion))]
    public class NetworkMountAnimation : NetworkBehaviour
    {
        const byte NoRenderer = 255;

        [SyncVar] byte activeIndex = NoRenderer;
        [SyncVar] bool activeIdle = true;
        [SyncVar] bool activeFlip;
        [SyncVar] byte activeFrame;

        PlayerMountCompanion companion;

        GameObject trackedMount;
        AnimatedSpriteRenderer[] renderers;
        MountVisualController visualController;

        int appliedIndex = -2;
        bool appliedIdle;
        bool appliedFlip;
        int appliedFrame = -1;

        void Awake()
        {
            companion = GetComponent<PlayerMountCompanion>();
            syncInterval = 0.03f;
            // Client-auth (igual ao NetworkPlayerAnimation): garante em código que o
            // sentido é ClientToServer, sem depender só do prefab.
            syncDirection = SyncDirection.ClientToServer;
        }

        void LateUpdate()
        {
            GameObject mount = companion != null ? companion.GetMountedLouieObject() : null;
            if (mount != trackedMount)
                RebindMount(mount);

            if (renderers == null)
                return;

            if (isOwned)
            {
                SampleOwned();
                return;
            }

            // No host (isServer) o mount real anima sozinho; só clientes puros aplicam.
            if (!isServer)
                ClientApply();
        }

        void RebindMount(GameObject mount)
        {
            trackedMount = mount;
            appliedIndex = -2;
            appliedFrame = -1;

            if (mount == null)
            {
                renderers = null;
                visualController = null;
                return;
            }

            renderers = mount.GetComponentsInChildren<AnimatedSpriteRenderer>(true);
            visualController = mount.GetComponentInChildren<MountVisualController>(true);

            // Cliente remoto puro: desliga o driver de animação do mount (leria um
            // facing não-simulado e congelaria); nós dirigimos os frames replicados.
            if (!isOwned && !isServer && visualController != null)
                visualController.enabled = false;
        }

        void SampleOwned()
        {
            AnimatedSpriteRenderer active = null;
            byte idx = NoRenderer;
            for (int i = 0; i < renderers.Length && i < NoRenderer; i++)
            {
                if (renderers[i] != null && renderers[i].enabled)
                {
                    active = renderers[i];
                    idx = (byte)i;
                    break;
                }
            }

            bool idle = active == null || active.idle;
            bool flip = false;
            byte frame = 0;
            if (active != null)
            {
                var sr = active.GetComponent<SpriteRenderer>();
                if (sr != null)
                    flip = sr.flipX;
                frame = (byte)Mathf.Clamp(active.CurrentFrame, 0, 254);
            }

            if (idx != activeIndex) activeIndex = idx;
            if (idle != activeIdle) activeIdle = idle;
            if (flip != activeFlip) activeFlip = flip;
            if (frame != activeFrame) activeFrame = frame;
        }

        void ClientApply()
        {
            if (activeIndex != appliedIndex)
            {
                for (int i = 0; i < renderers.Length; i++)
                    SetEnabled(renderers[i], i == activeIndex);

                if (activeIndex != NoRenderer && activeIndex < renderers.Length && renderers[activeIndex] != null)
                    renderers[activeIndex].SetManualAnimationUpdate(true);

                appliedIndex = activeIndex;
                appliedIdle = !activeIdle;
                appliedFrame = -1;
                appliedFlip = !activeFlip;
            }

            if (activeIndex == NoRenderer || activeIndex >= renderers.Length)
                return;

            var active = renderers[activeIndex];
            if (active == null)
                return;

            bool dirty = false;

            if (activeIdle != appliedIdle)
            {
                active.idle = activeIdle;
                appliedIdle = activeIdle;
                dirty = true;
            }

            if (activeFrame != appliedFrame)
            {
                active.CurrentFrame = activeFrame;
                appliedFrame = activeFrame;
                dirty = true;
            }

            if (activeFlip != appliedFlip)
            {
                var sr = active.GetComponent<SpriteRenderer>();
                if (sr != null)
                    sr.flipX = activeFlip;
                appliedFlip = activeFlip;
                dirty = true;
            }

            if (dirty)
                active.RefreshFrame();
        }

        static void SetEnabled(AnimatedSpriteRenderer r, bool on)
        {
            if (r != null && r.enabled != on)
                r.enabled = on;
        }
    }
}

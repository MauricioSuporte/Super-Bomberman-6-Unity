using Mirror;
using UnityEngine;

namespace Assets.Scripts.Netcode
{
    /// <summary>
    /// F1 — Replicação da SAÍDA de animação do player (host-autoritativo).
    ///
    /// O host sincroniza o resultado visual que produz:
    ///   - índice do AnimatedSpriteRenderer visível (o jogo mantém só 1 ligado),
    ///   - flag idle, flipX do sprite, e o FRAME atual da animação.
    ///
    /// O cliente liga o renderer indicado (desligando o resto), coloca-o em
    /// modo MANUAL (para não auto-animar) e espelha exatamente o frame do host.
    /// Espelhar o frame é o que cobre animações não-loopadas re-disparadas pelo
    /// host (AFK/emote, cornered, morte) — que, deixadas em auto-advance no
    /// cliente, tocariam uma vez e congelariam.
    ///
    /// Amostramos o renderer realmente HABILITADO (não move.ActiveSpriteRenderer)
    /// para capturar qualquer sistema que ligue um renderer diretamente
    /// (InactivityAnimation, CorneredAnimation, StunReceiver, etc.).
    ///
    /// A ordem de <see cref="renderers"/> vem de GetComponentsInChildren, idêntica
    /// entre host e cliente por ser o MESMO prefab.
    /// </summary>
    [RequireComponent(typeof(NetworkIdentity))]
    [RequireComponent(typeof(MovementController))]
    public class NetworkPlayerAnimation : NetworkBehaviour
    {
        const byte NoRenderer = 255;

        [SyncVar] byte activeIndex = NoRenderer;
        [SyncVar] bool activeIdle = true;
        [SyncVar] bool activeFlip;
        [SyncVar] byte activeFrame;

        AnimatedSpriteRenderer[] renderers;

        int appliedIndex = -2;
        bool appliedIdle;
        bool appliedFlip;
        int appliedFrame = -1;

        void Awake()
        {
            renderers = GetComponentsInChildren<AnimatedSpriteRenderer>(true);
            // Sincroniza frames com frequência suficiente para animação fluida.
            syncInterval = 0.03f;
        }

        void LateUpdate()
        {
            if (isServer)
                ServerSample();
            else
                ClientApply();
        }

        [Server]
        void ServerSample()
        {
            AnimatedSpriteRenderer active = null;
            byte idx = NoRenderer;
            if (renderers != null)
            {
                for (int i = 0; i < renderers.Length && i < NoRenderer; i++)
                {
                    if (renderers[i] != null && renderers[i].enabled)
                    {
                        active = renderers[i];
                        idx = (byte)i;
                        break;
                    }
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
            if (renderers == null)
                return;

            // Troca de renderer visível: desliga todos, liga o indicado em modo
            // manual (nós dirigimos o frame; sem auto-advance para não brigar).
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

            // Aplica o frame/idle atual (idle mostra idleSprite; senão o frame).
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

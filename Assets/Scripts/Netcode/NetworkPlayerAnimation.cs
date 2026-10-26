using System.Collections.Generic;
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
        [SyncVar] Vector3 activeOffset;
        [SyncVar] int activeSortingLayer, activeSortingOrder;
        [SyncVar(hook = nameof(OnVisualTime))] double visualTime;

        struct VisualFrame
        {
            public double time;
            public byte index, frame;
            public bool idle, flip;
            public Vector3 offset;
            public int sortingLayer, sortingOrder;
        }

        readonly List<VisualFrame> visualFrames = new(64);
        NetworkTransformReliable positionSync;
        float nextDiagnostic;
        bool diagnostics;

        void OnVisualTime(double previous, double current)
        {
            if (isServer || (isLocalPlayer && GetComponent<NetworkPlayerInput>()?.OwnerUsesAuthorityPresentation != true)) return;
            double adjustment = 0;
            if (positionSync != null)
                adjustment = NetworkClient.sendInterval * (positionSync.sendIntervalMultiplier - 1 +
                    (positionSync.timelineOffset ? positionSync.sendIntervalMultiplier : 0));
            visualFrames.Add(new VisualFrame { time = current + adjustment, index = activeIndex,
                frame = activeFrame, idle = activeIdle, flip = activeFlip, offset = activeOffset, sortingLayer = activeSortingLayer, sortingOrder = activeSortingOrder });
            if (visualFrames.Count > 64) visualFrames.RemoveAt(0);
        }

        public void ClearOwnerVisualHistory() => visualFrames.Clear();

        AnimatedSpriteRenderer[] renderers;

        int appliedIndex = -2;
        bool appliedIdle;
        bool appliedFlip;
        int appliedFrame = -1;

        MovementController move;
        bool wasPredicting;

        void Awake()
        {
            renderers = GetComponentsInChildren<AnimatedSpriteRenderer>(true);
            move = GetComponent<MovementController>();
            positionSync = GetComponent<NetworkTransformReliable>();
            diagnostics = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-online-motion-logs") >= 0;
            // Sincroniza frames com frequência suficiente para animação fluida.
            syncInterval = 0.03f;
            // The server samples animation for humans and COM.
            syncDirection = SyncDirection.ServerToClient;
        }

        void LateUpdate()
        {
            if (!NetSync.IsOnline || netIdentity == null || (!isServer && !isClient))
                return;

            if (!BombermanNetworkManager.IsCoopStage && GameManager.Instance != null && GameManager.Instance.OnlineRoundPresentationActive && move != null && move.IsEndingStage)
                return;
            // Walking owners animate immediately. Special poses and observers retain
            // authoritative frame playback; replay never emits animation RPCs.
            bool predicting = !isServer && isLocalPlayer && move != null && move.PredictLocally;
            if (predicting != wasPredicting)
            {
                wasPredicting = predicting;
                visualFrames.Clear();
                appliedIndex = -2;
                foreach (var renderer in renderers)
                    if (renderer != null) renderer.SetManualAnimationUpdate(!predicting);
            }
            if (!isServer && GetComponent<NetworkBombFx>()?.RidingArcActive == true) return;
            if (!isServer && isLocalPlayer && GetComponent<NetworkBombFx>()?.MoleStartupActive == true) return;
            if (predicting)
            {
                // Replicated ordering can change during an external ride. Keep
                // confirmed sorting while the owner chooses its own pose/frame.
                ApplyOwnerConfirmedSorting();
                return;
            }
            if (isServer)
            {
                SampleOwned();
                return;
            }

            if (!GamePauseController.IsPaused) ClientApply();
        }

        void ApplyOwnerConfirmedSorting()
        {
            if (renderers == null || activeIndex == NoRenderer || activeIndex >= renderers.Length) return;
            var active = renderers[activeIndex];
            if (active != null && active.TryGetComponent<SpriteRenderer>(out var sprite))
            { sprite.sortingLayerID = activeSortingLayer; sprite.sortingOrder = activeSortingOrder; }
        }

        void SampleOwned()
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
            int sortingLayer = 0, sortingOrder = 0;
            byte frame = 0;
            if (active != null)
            {
                var sr = active.GetComponent<SpriteRenderer>();
                if (sr != null)
                { flip = sr.flipX; sortingLayer = sr.sortingLayerID; sortingOrder = sr.sortingOrder; }
                frame = (byte)Mathf.Clamp(active.CurrentFrame, 0, 254);
            }

            if (idx != activeIndex) activeIndex = idx;
            if (idle != activeIdle) activeIdle = idle;
            if (flip != activeFlip) activeFlip = flip;
            if (frame != activeFrame) activeFrame = frame;
            activeOffset = active != null ? active.RuntimeBaseLocalOffset : Vector3.zero;
            activeSortingLayer = sortingLayer; activeSortingOrder = sortingOrder;
            visualTime = NetworkTime.localTime;
        }

        void ClientApply()
        {
            byte activeIndex = this.activeIndex, activeFrame = this.activeFrame;
            bool activeIdle = this.activeIdle, activeFlip = this.activeFlip;
            Vector3 activeOffset = this.activeOffset;
            int sortingLayer = activeSortingLayer, sortingOrder = activeSortingOrder;
            if ((!isLocalPlayer || GetComponent<NetworkPlayerInput>()?.OwnerUsesAuthorityPresentation == true) && visualFrames.Count > 0)
            {
                while (visualFrames.Count > 1 && visualFrames[1].time <= NetworkTime.time) visualFrames.RemoveAt(0);
                var sample = visualFrames[0];
                activeIndex = sample.index; activeFrame = sample.frame;
                activeIdle = sample.idle; activeFlip = sample.flip; activeOffset = sample.offset;
                sortingLayer = sample.sortingLayer; sortingOrder = sample.sortingOrder;
                if (visualFrames.Count > 1)
                {
                    var next = visualFrames[1]; double interval = next.time - sample.time;
                    if (sample.index == next.index && interval > 0 && interval <= 0.25 && Vector3.Distance(sample.offset, next.offset) <= 4)
                        activeOffset = Vector3.Lerp(sample.offset, next.offset, Mathf.Clamp01((float)((NetworkTime.time - sample.time) / interval)));
                }
            }
            if (diagnostics && Time.unscaledTime >= nextDiagnostic)
            {
                nextDiagnostic = Time.unscaledTime + 1f;
                Debug.Log($"[OnlineMotion] player={netId} rttMs={NetworkTime.rtt * 1000:F1} visualBuffer={visualFrames.Count} positionBuffer={(positionSync != null ? positionSync.clientSnapshots.Count : 0)} position={transform.position} animation={activeIndex}:{activeFrame} idle={activeIdle} offset={activeOffset}");
            }
            if (renderers == null)
                return;

            // Troca de renderer visível: desliga todos, liga o indicado em modo
            // manual (nós dirigimos o frame; sem auto-advance para não brigar).
            if (activeIndex != appliedIndex)
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] != null && i != activeIndex) renderers[i].ClearRuntimeBaseOffset();
                    SetEnabled(renderers[i], i == activeIndex);
                }

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

            if (active.TryGetComponent<SpriteRenderer>(out var orderedSprite))
            { orderedSprite.sortingLayerID = sortingLayer; orderedSprite.sortingOrder = sortingOrder; }
            bool dirty = false;
            if (active.RuntimeBaseLocalOffset != activeOffset) active.SetRuntimeBaseLocalOffset(activeOffset);

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

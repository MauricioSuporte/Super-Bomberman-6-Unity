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
        [SyncVar] NetworkVisualPose activePose;
        [SyncVar] int activeSortingLayer, activeSortingOrder;
        [SyncVar(hook = nameof(OnVisualTime))] double visualTime;

        struct VisualFrame
        {
            public double time;
            public byte index, frame;
            public bool idle, flip;
            public Vector3 offset;
            public NetworkVisualPose pose;
            public int sortingLayer, sortingOrder;
        }

        readonly List<VisualFrame> visualFrames = new(64);
        NetworkTransformReliable positionSync;
        float nextDiagnostic, nextVisualTrace;
        bool diagnostics;

        void OnVisualTime(double previous, double current)
        {
            if (isServer || (isLocalPlayer && GetComponent<NetworkPlayerInput>()?.OwnerUsesAuthorityPresentation != true)) return;
            double adjustment = 0;
            if (positionSync != null)
                adjustment = NetworkClient.sendInterval * (positionSync.sendIntervalMultiplier - 1 +
                    (positionSync.timelineOffset ? positionSync.sendIntervalMultiplier : 0));
            visualFrames.Add(new VisualFrame { time = current + adjustment, index = activeIndex,
                frame = activeFrame, idle = activeIdle, flip = activeFlip, offset = activeOffset, pose = activePose, sortingLayer = activeSortingLayer, sortingOrder = activeSortingOrder });
            if (visualFrames.Count > 64) visualFrames.RemoveAt(0);
        }

        NetworkVisualPose.Baseline[] ownerVisualBaseline;
        public void ClearOwnerVisualHistory()
        {
            visualFrames.Clear();
            if (GetComponent<NetworkPlayerInput>()?.OwnerUsesAuthorityPresentation == true)
            { ownerVisualBaseline ??= NetworkVisualPose.CaptureBaseline(renderers, transform); }
            else
            { NetworkVisualPose.RestoreBaseline(ownerVisualBaseline); ownerVisualBaseline = null; }
        }

        AnimatedSpriteRenderer[] renderers;

        int appliedIndex = -2;
        bool appliedIdle;
        bool appliedFlip;
        int appliedFrame = -1;

        MovementController move;
        bool wasPredicting;
        public void InvalidateOwnerPose() { appliedIndex = -2; appliedFrame = -1; visualFrames.Clear(); }

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
            if (!isServer && !isLocalPlayer && R01DenseDiagnostics) PresentObserverPosition();
            // Walking owners animate immediately. Special poses and observers retain
            // authoritative frame playback; replay never emits animation RPCs.
            bool predicting = !isServer && isLocalPlayer && move != null && move.PredictLocally;
            bool resumingPrediction = predicting && !wasPredicting;
            if (predicting != wasPredicting)
            {
                wasPredicting = predicting;
                visualFrames.Clear();
                appliedIndex = -2;
                foreach (var renderer in renderers)
                    if (renderer != null) renderer.SetManualAnimationUpdate(!predicting);
            }
            if (resumingPrediction && GetComponent<NetworkBombFx>()?.ClientPlayerMotionActive != true)
            {
                foreach (var renderer in renderers) SetEnabled(renderer, false);
                move.EnableExclusiveFromState();
                GetComponent<NetworkMountAnimation>()?.RestoreOwnerLocomotion();
                appliedIndex = -1;
            }
            // Diagnostics only: keep logging the presented pose while another presenter owns the root.
            if (!isServer && GetComponent<NetworkBombFx>()?.RidingArcActive == true) { TracePlayerVisual(); return; }
            if (!isServer && isLocalPlayer && GetComponent<NetworkBombFx>()?.MoleStartupActive == true) { TracePlayerVisual(); return; }
            if (!isServer && isLocalPlayer && GetComponent<NetworkBombFx>()?.OwnerBoatPredictionReady == true) { TracePlayerVisual(); return; }
            if (predicting)
            {
                // Replicated ordering can change during an external ride. Keep
                // confirmed sorting while the owner chooses its own pose/frame.
                ApplyOwnerConfirmedSorting();
                TracePlayerVisual(); return;
            }
            if (isServer)
            {
                SampleOwned();
                TracePlayerVisual(); return;
            }

            if (!GamePauseController.IsPaused) ClientApply();
            TracePlayerVisual();
        }

        /// <summary>
        /// R01 A/B switches: only honoured in Development builds. Evaluated lazily on the first frame that needs it
        /// (Debug.isDebugBuild cannot be read from a MonoBehaviour type initializer).
        /// </summary>
        internal static bool DevelopmentSwitch(string argument, ref int cache)
        {
            if (cache == 0) cache = Debug.isDebugBuild && System.Array.IndexOf(System.Environment.GetCommandLineArgs(), argument) >= 0 ? 2 : 1;
            return cache == 2;
        }

        static int poseEnforceDisabledCache;

        // R01 measurement rows (edges, root back-steps, the 0.05 s server reference, the 64 MiB cap) only in Development
        // builds: the cost runs of 07/11/2026 measured +56 KiB/s of server allocations from them while the checked-in
        // GameplayLatencyDiagnostics.json enables the capture in every build. Release keeps the pre-R01 capture density.
        internal static bool R01DenseDiagnostics => Debug.isDebugBuild;

        // Why a remote player is not measured this frame: another presenter owns or freezes its root.
        string ObserverExclusion()
        {
            positionSync = positionSync != null ? positionSync : GetComponent<NetworkTransformReliable>();
            var input = GetComponent<NetworkPlayerInput>();
            var fx = GetComponent<NetworkBombFx>();
            return positionSync == null || positionSync.clientSnapshots.Count == 0 ? "no_snapshots" :
                GamePauseController.IsPaused ? "paused" :
                input != null && input.UsesSpecialPresentationTimeline ? "launch_or_cart" :
                fx != null && fx.ClientPlayerMotionActive ? "ability_motion" :
                move != null && (move.isDead || move.IsEndingStage) ? "dead_or_ending" : null;
        }

        // R01 diagnostics on observers (no presentation change): Mirror's NetworkTransform presents the root at
        // NetworkTime.time and the pose/mount presenters sample NetworkPlayerInput.AuthorityPresentationTime, which is the same
        // time for a remote player; a frame where they differ is counted. (A presentation clock that held Mirror's back-steps
        // was measured in a controlled A/B on 07/11 and removed: no difference in back-steps along the server path.)
        void PresentObserverPosition()
        {
            if (!GameplayLatencyDiagnostics.Enabled) return;
            double time = NetworkTime.time;
            presentationExcluded = ObserverExclusion();
            if (presentationExcluded == null && TryGetComponent<NetworkPlayerInput>(out var presentedInput) && presentedInput.AuthorityPresentationTime != time) rootMisaligned++;
            TraceRootBackStep(time);
        }

        // R01 measurement: a presented root step
        // against the authoritative path over the same presented interval (sampled from the current snapshot buffer) is a
        // visible back-step. A forward catch-up jump through a real server reversal is not one. Same exclusions
        // with the clock on or off (presenters owning the root, pause, death, teleport-sized steps). 1 px = 1/16 u.
        string presentationExcluded;
        Vector3 lastTracedRoot; double lastTracedTime; bool lastTracedRootValid;
        int rootMovingFrames, rootBackStepsPixel, rootBackStepsVisible, rootMisaligned; float rootMaxBackStep, rootSummaryAt;
        void TraceRootBackStep(double time)
        {
            Vector3 root = transform.localPosition;
            bool usable = presentationExcluded == null && positionSync != null && positionSync.clientSnapshots.Count >= 2;
            if (usable && lastTracedRootValid)
            {
                var buffer = positionSync.clientSnapshots;
                Vector3 step = root - lastTracedRoot;
                float maxStep = move != null ? Mathf.Max(0.01f, move.tileSize * 1.5f) : 1.5f;
                // Time advanced: the reference is the authoritative path between the two presented times, so a step through
                // a real server reversal agrees with it. Time went back: the reference is the path's forward motion ending
                // at the presented time, so replaying a position already passed is a back-step.
                double since = time > lastTracedTime ? lastTracedTime : time - 1.0 / 60.0;
                if (step.magnitude <= maxStep && TrySampleRoot(buffer, time, out Vector3 now) && TrySampleRoot(buffer, since, out Vector3 before))
                {
                    Vector3 forward = now - before;
                    if (forward.sqrMagnitude > 1f / 4096f)
                    {
                        rootMovingFrames++;
                        float back = -Vector3.Dot(step, forward.normalized);
                        if (back >= 1f / 16f) rootBackStepsPixel++;
                        if (back > 0.1f) rootBackStepsVisible++;
                        if (back > rootMaxBackStep) rootMaxBackStep = back;
                        if (back >= 0.02f)
                        {
                            var row = GameplayLatencyDiagnostics.For("root_backstep", move, detail: "units=" + back.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) +
                                ";ptime=" + time.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) +
                                ";pprev=" + lastTracedTime.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) +
                                ";mirror=" + NetworkTime.time.ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
                            row.delta = step; GameplayLatencyDiagnostics.Write(row);
                        }
                    }
                }
            }
            lastTracedRoot = root; lastTracedTime = time; lastTracedRootValid = usable;
            if (Time.unscaledTime >= rootSummaryAt)
            {
                if (rootSummaryAt > 0 && rootMovingFrames > 0)
                    GameplayLatencyDiagnostics.Write(GameplayLatencyDiagnostics.For("root_motion_summary", move, detail: "moving_frames=" + rootMovingFrames +
                        ";backsteps_px=" + rootBackStepsPixel + ";backsteps_visible=" + rootBackStepsVisible + ";max_back=" +
                        rootMaxBackStep.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + ";misaligned=" + rootMisaligned));
                rootSummaryAt = Time.unscaledTime + 5f;
                rootMovingFrames = rootBackStepsPixel = rootBackStepsVisible = rootMisaligned = 0; rootMaxBackStep = 0;
            }
        }

        static bool TrySampleRoot(SortedList<double, TransformSnapshot> buffer, double time, out Vector3 position)
        {
            position = default;
            if (buffer.Count < 2) return false;
            SnapshotInterpolation.Sample(buffer, time, out int from, out int to, out double t);
            if (from < 0 || to < 0 || from >= buffer.Count || to >= buffer.Count) return false;
            position = Vector3.Lerp(buffer.Values[from].position, buffer.Values[to].position, Mathf.Clamp01((float)t));
            return true;
        }

        // R01: observers re-assert the authoritative renderer set after every other presenter has run this frame.
        // Presenters that end an effect (riding arc end, mount transitions) call EnableExclusiveFromState(), which shows the
        // foot body; NetworkPlayerAnimation already ran earlier in the frame, so that body used to reach the screen for one
        // frame before the next ClientApply replaced it. Same guards as LateUpdate: nothing is applied while another
        // presenter owns the pose, while predicting (owner), while paused or in an end-of-stage presentation.
        // -online-no-pose-enforce (Development builds only) disables it for A/B measurements.
        static bool poseEnforceDisabled => DevelopmentSwitch("-online-no-pose-enforce", ref poseEnforceDisabledCache);
        // -online-dense-server-reference (Development only): the server writes its visual samples every frame, so every
        // authoritative state an observer can interpolate is in the reference; a frame-number gap marks a capture loss.
        static int denseServerReferenceCache;
        internal static bool DenseServerReference => DevelopmentSwitch("-online-dense-server-reference", ref denseServerReferenceCache);
        public void EnforceAuthorityPose()
        {
            if (poseEnforceDisabled || !NetSync.IsOnline || netIdentity == null || isServer || !isClient || renderers == null || move == null) return;
            if (!BombermanNetworkManager.IsCoopStage && GameManager.Instance != null && GameManager.Instance.OnlineRoundPresentationActive && move.IsEndingStage) return;
            if (GamePauseController.IsPaused) return;
            var fx = GetComponent<NetworkBombFx>();
            if (fx != null && fx.RidingArcActive) return;
            if (isLocalPlayer && fx != null && (fx.MoleStartupActive || fx.OwnerBoatPredictionReady)) return;
            if (isLocalPlayer && move.PredictLocally) return;
            if (wasPredicting) return;
            int before = GameplayLatencyDiagnostics.Enabled ? VisibleBodyMask() : 0;
            ClientApply();
            if (GameplayLatencyDiagnostics.Enabled)
            {
                int after = VisibleBodyMask();
                if (after != before)
                {
                    var row = GameplayLatencyDiagnostics.For("player_pose_enforced", move, detail: "before=" + BodyNames(before) + ";after=" + BodyNames(after) + ";ptime=" +
                        (GetComponent<NetworkPlayerInput>()?.AuthorityPresentationTime ?? NetworkTime.time).ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
                    GameplayLatencyDiagnostics.Write(row);
                }
            }
        }

        int VisibleBodyMask()
        {
            int mask = 0;
            for (int i = 0; i < renderers.Length && i < 30; i++)
                if (renderers[i] != null && renderers[i].GetComponentInParent<MountVisualController>(true) == null &&
                    renderers[i].TryGetComponent<SpriteRenderer>(out var s) && s.enabled && s.gameObject.activeInHierarchy) mask |= 1 << i;
            return mask;
        }

        string BodyNames(int mask)
        {
            var names = new List<string>();
            for (int i = 0; i < renderers.Length && i < 30; i++) if ((mask & (1 << i)) != 0) names.Add(renderers[i].name);
            return string.Join(",", names);
        }

        // R01: edge-triggered row for the visible player renderer set (same reason as NetworkMountAnimation.TraceBodyEdge).
        bool[] edgeMountChild; float edgeMountChildAt; int lastEdgeRenderers = -1;
        void TracePlayerEdge()
        {
            if (edgeMountChild == null || edgeMountChild.Length != renderers.Length || Time.unscaledTime >= edgeMountChildAt)
            {
                edgeMountChild = new bool[renderers.Length];
                for (int i = 0; i < renderers.Length; i++) edgeMountChild[i] = renderers[i] != null && renderers[i].GetComponentInParent<MountVisualController>(true) != null;
                edgeMountChildAt = Time.unscaledTime + 1f;
            }
            int mask = 0;
            for (int i = 0; i < renderers.Length && i < 30; i++)
                if (renderers[i] != null && !edgeMountChild[i] && renderers[i].TryGetComponent<SpriteRenderer>(out var s) && s.enabled && s.gameObject.activeInHierarchy) mask |= 1 << i;
            if (mask == lastEdgeRenderers) return;
            bool first = lastEdgeRenderers == -1;
            lastEdgeRenderers = mask;
            if (first) return;
            var visible = new List<string>();
            for (int i = 0; i < renderers.Length && i < 30; i++) if ((mask & (1 << i)) != 0) visible.Add(renderers[i].name);
            var edge = GameplayLatencyDiagnostics.For("player_renderer_edge", move, detail: "renderers=" + string.Join(",", visible) + ";ptime=" +
                (GetComponent<NetworkPlayerInput>()?.AuthorityPresentationTime ?? NetworkTime.time).ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
            GameplayLatencyDiagnostics.Write(edge);
        }

        void TracePlayerVisual()
        {
            if (GameplayLatencyDiagnostics.Enabled && R01DenseDiagnostics && renderers != null) TracePlayerEdge();
            if (!GameplayLatencyDiagnostics.Enabled || Time.unscaledTime < nextVisualTrace || renderers == null) return;
            // R01: the server is the reference for every observer comparison, so it is sampled twice as densely (and frame
            // time is logged) to tell sparse logging from a real server hitch.
            nextVisualTrace = Time.unscaledTime + (isServer && R01DenseDiagnostics ? (DenseServerReference ? 0f : .05f) : .1f);
            var visible = new List<string>();
            foreach (var renderer in renderers)
                if (renderer != null && renderer.GetComponentInParent<MountVisualController>(true) == null && renderer.TryGetComponent<SpriteRenderer>(out var sprite) && sprite.enabled && sprite.gameObject.activeInHierarchy)
                    visible.Add(renderer.name);
            var entry = GameplayLatencyDiagnostics.For("player_visual_sample", move, detail: "renderers=" + string.Join(",", visible) + ";ptime=" + (GetComponent<NetworkPlayerInput>()?.AuthorityPresentationTime ?? NetworkTime.time).ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + ";dt=" + (Time.unscaledDeltaTime * 1000f).ToString("F1", System.Globalization.CultureInfo.InvariantCulture));
            entry.accepted = isServer ? move.CanPredictNetworkMovement : isLocalPlayer && move.PredictLocally;
            GameplayLatencyDiagnostics.Write(entry);
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
                    if (renderers[i] != null && renderers[i].isActiveAndEnabled &&
                        (!renderers[i].TryGetComponent<SpriteRenderer>(out var visibleSprite) || visibleSprite.enabled))
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
            activePose = NetworkVisualPose.Capture(transform, active != null ? active.GetComponent<SpriteRenderer>() : null);
            activeSortingLayer = sortingLayer; activeSortingOrder = sortingOrder;
            visualTime = NetworkTime.localTime;
        }

        void ClientApply()
        {
            double timeline = GetComponent<NetworkPlayerInput>()?.AuthorityPresentationTime ?? NetworkTime.time;
            byte activeIndex = this.activeIndex, activeFrame = this.activeFrame;
            bool activeIdle = this.activeIdle, activeFlip = this.activeFlip;
            Vector3 activeOffset = this.activeOffset;
            NetworkVisualPose pose = activePose;
            int sortingLayer = activeSortingLayer, sortingOrder = activeSortingOrder;
            if ((!isLocalPlayer || GetComponent<NetworkPlayerInput>()?.OwnerUsesAuthorityPresentation == true) && visualFrames.Count > 0)
            {
                while (visualFrames.Count > 1 && visualFrames[1].time <= timeline) visualFrames.RemoveAt(0);
                var sample = visualFrames[0];
                activeIndex = sample.index; activeFrame = sample.frame;
                activeIdle = sample.idle; activeFlip = sample.flip; activeOffset = sample.offset; pose = sample.pose;
                sortingLayer = sample.sortingLayer; sortingOrder = sample.sortingOrder;
                if (visualFrames.Count > 1)
                {
                    var next = visualFrames[1]; double interval = next.time - sample.time;
                    float offsetLimit = NetworkVisualPose.ApprovedOffsetLimit(sample.pose, next.pose);
                    if (sample.index == next.index && interval > 0 && interval <= 0.25 && Vector3.Distance(sample.offset, next.offset) <= offsetLimit)
                    {
                        float t = Mathf.Clamp01((float)((timeline - sample.time) / interval));
                        activeOffset = Vector3.Lerp(sample.offset, next.offset, t);
                        if (Vector3.Distance(sample.pose.position, next.pose.position) <= offsetLimit) pose = NetworkVisualPose.Interpolate(sample.pose, next.pose, t);
                    }
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

            // Other presenters may have touched visibility without changing the
            // approved renderer index. End poses must remain exclusive too.
            for (int i = 0; i < renderers.Length; i++)
            {
                if (i == activeIndex && renderers[i] != null && !renderers[i].enabled) appliedFrame = -1;
                SetEnabled(renderers[i], i == activeIndex);
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
            if (active.TryGetComponent<SpriteRenderer>(out var posedSprite)) pose.Apply(transform, posedSprite);
        }

        static void SetEnabled(AnimatedSpriteRenderer r, bool on)
        {
            if (r == null) return;
            if (r.enabled != on) r.enabled = on;
            if (r.TryGetComponent<SpriteRenderer>(out var sprite)) sprite.enabled = on;
        }
    }
}

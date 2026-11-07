using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace Assets.Scripts.Netcode
{
    // Replicate every animated branch, including simultaneous dash/shoot effects.
    [DefaultExecutionOrder(1000)]
    [RequireComponent(typeof(NetworkIdentity), typeof(PlayerMountCompanion))]
    public class NetworkMountAnimation : NetworkBehaviour
    {
        public readonly SyncList<NetworkWorldMount.MountFrame> frames = new();
        [SyncVar] MountedType frameType;
        [SyncVar] uint frameRevision;
        [SyncVar(hook = nameof(OnMountVisualTime))] double frameTime;
        struct MountVisualSample
        {
            public double time;
            public MountedType type;
            public uint revision;
            public NetworkWorldMount.MountFrame[] values;
        }
        readonly List<MountVisualSample> visualHistory = new(64);
        NetworkTransformReliable positionSync;

        void OnMountVisualTime(double previous, double current)
        {
            if (NetSync.Mode != NetSync.NetMode.Client || !NetworkClient.active || isServer) return;
            positionSync = positionSync != null ? positionSync : GetComponent<NetworkTransformReliable>();
            double adjustment = positionSync != null ? NetworkClient.sendInterval * (positionSync.sendIntervalMultiplier - 1 +
                (positionSync.timelineOffset ? positionSync.sendIntervalMultiplier : 0)) : 0;
            var copy = new NetworkWorldMount.MountFrame[frames.Count];
            for (int i = 0; i < copy.Length; i++) copy[i] = frames[i];
            visualHistory.Add(new MountVisualSample { time = current + adjustment, type = frameType, revision = frameRevision, values = copy });
            if (visualHistory.Count > 64) visualHistory.RemoveAt(0);
        }

        public bool TrySampleMountFrame(double timeline, MountedType type, int index, out NetworkWorldMount.MountFrame frame)
        {
            frame = default;
            int selected = -1, next = -1;
            for (int i = 0; i < visualHistory.Count; i++)
            {
                var sample = visualHistory[i];
                if (sample.type != type || sample.revision != frameRevision || index < 0 || index >= sample.values.Length) continue;
                if (selected < 0 || sample.time <= timeline) selected = i;
                if (sample.time > timeline) { next = i; break; }
            }
            if (selected < 0) return false;
            var from = visualHistory[selected]; frame = from.values[index];
            if (next > selected)
            {
                var to = visualHistory[next]; var target = to.values[index];
                double interval = to.time - from.time;
                float offsetLimit = NetworkVisualPose.ApprovedOffsetLimit(frame.pose, target.pose);
                if (interval > 0 && interval <= 0.25 && frame.visible && target.visible && frame.idle == target.idle &&
                    Vector3.Distance(frame.localPosition, target.localPosition) <= offsetLimit)
                {
                    float t = Mathf.Clamp01((float)((timeline - from.time) / interval));
                    frame.localPosition = Vector3.Lerp(frame.localPosition, target.localPosition, t);
                    if (Vector3.Distance(frame.pose.position, target.pose.position) <= offsetLimit) frame.pose = NetworkVisualPose.Interpolate(frame.pose, target.pose, t);
                }
            }
            return true;
        }

        PlayerMountCompanion companion;
        GameObject trackedMount;
        AnimatedSpriteRenderer[] renderers;
        SpriteRenderer[] sprites;
        float nextSample, nextVisualTrace;
        GreenLouieDashAnimator greenDash;
        BlackLouieDashAnimator blackDash;
        MountVisualController ownerVisual;

        void Awake()
        {
            companion = GetComponent<PlayerMountCompanion>();
            syncInterval = 0.03f;
            syncDirection = SyncDirection.ServerToClient;
        }

        void LateUpdate()
        {
            LateUpdateCore();
            // Runs at order 1000, after every default-order presenter: the last word on which player body is visible.
            if (!isServer) GetComponent<NetworkPlayerAnimation>()?.EnforceAuthorityPose();
            if (!isServer) HideMountBeforeAuthorityBody();
            if (GameplayLatencyDiagnostics.Enabled && NetworkPlayerAnimation.R01DenseDiagnostics && renderers != null) TraceBodyEdge();
        }

        // R01: the mount frames and the player body come from two streams. For a frame or two the mount can already be shown
        // (mount / dismount / mount change) while the body is still the plain on-foot sprite: a full body next to the mount.
        // Observers: the mount stream led the buffered body. Predicting owner: the replicated mount is applied directly the
        // frame it arrives, while the owner's own body becomes mounted (HeadOnly/Mouted) one frame later (measured: Pink -> Mole,
        // body Left + Mole for 1 frame). That frame keeps the mount hidden; the next frame both agree. Never applied to ability
        // motion, the Mole startup, dead players, end of stage or while paused; never touches the body or gameplay state.
        // -online-no-mount-gate (Development builds only) disables it for A/B measurements.
        static int mountGateDisabledCache;
        static bool mountGateDisabled => NetworkPlayerAnimation.DevelopmentSwitch("-online-no-mount-gate", ref mountGateDisabledCache);
        void HideMountBeforeAuthorityBody()
        {
            if (mountGateDisabled || !NetSync.IsOnline || isServer || !isClient || renderers == null || sprites == null || trackedMount == null || GamePauseController.IsPaused) return;
            var fx = GetComponent<NetworkBombFx>();
            if (fx != null && (fx.ClientPlayerMotionActive || fx.MoleStartupActive)) return;
            if (!TryGetComponent<MovementController>(out var movement) || movement.isDead || movement.IsEndingStage || !movement.IsPlainFootBodyVisible()) return;
            bool hidden = false;
            foreach (var sprite in sprites) if (sprite != null && sprite.enabled) { sprite.enabled = false; hidden = true; }
            if (hidden && GameplayLatencyDiagnostics.Enabled)
                GameplayLatencyDiagnostics.Write(GameplayLatencyDiagnostics.For("mount_gated_by_body", movement, detail: "type=" + companion.GetMountPresentationType() + ";ptime=" +
                    (GetComponent<NetworkPlayerInput>()?.AuthorityPresentationTime ?? NetworkTime.time).ToString("F3", System.Globalization.CultureInfo.InvariantCulture)));
        }

        void LateUpdateCore()
        {
            if (!NetSync.IsOnline || netIdentity == null || (!isServer && !isClient)) return;
            // A consumed world mount is pending outside the actor until landing.
            // Its approved frames must already have a client view during that arc.
            if (!isServer && companion != null && companion.GetMountedLouieObject() == null &&
                frameType != MountedType.None && frames.Count > 0 && GetComponent<NetworkBombFx>()?.MountingArcActive == true)
                companion.ShowMountVisualClient(frameType);
            var mount = companion != null ? (isServer ? companion.GetMountPresentationObject() : companion.GetMountedLouieObject()) : null;
            if (mount == null) mount = null; // a destroyed mount is no mount
            // A destroyed tracked mount compares equal to a destroyed/null mount; its renderers must not be touched again
            // (R01 mounted-death fixture: dying on a Tank destroyed it while the reference stayed tracked; NRE every frame).
            if (mount != trackedMount || !ReferenceEquals(trackedMount, null) && trackedMount == null || RenderersDestroyed()) Rebind(mount);
            if (renderers == null) return;
            if (!isServer && isLocalPlayer && GetComponent<NetworkBombFx>()?.OwnerBoatPredictionReady == true)
            {
                foreach (var sprite in sprites) if (sprite != null) sprite.enabled = false;
                return;
            }
            if (isServer)
            {
                if (Time.unscaledTime < nextSample) return;
                nextSample = Time.unscaledTime + syncInterval;
                bool changed = false;
                for (int i = 0; i < renderers.Length; i++)
                {
                    var animation = renderers[i];
                    var sprite = sprites[i];
                    var frame = new NetworkWorldMount.MountFrame
                    {
                        visible = sprite != null && sprite.enabled && sprite.gameObject.activeInHierarchy,
                        idle = animation.idle, frame = animation.CurrentFrame,
                        flipX = sprite != null && sprite.flipX, flipY = sprite != null && sprite.flipY,
                        color = sprite != null ? sprite.color : Color.white,
                        localPosition = animation.transform.localPosition, pose = NetworkVisualPose.Capture(transform, sprite)
                    };
                    if (i >= frames.Count) { frames.Add(frame); changed = true; }
                    else if (!frames[i].Equals(frame)) { frames[i] = frame; changed = true; }
                }
                if (changed) { frameType = companion.GetMountPresentationType(); frameTime = NetworkTime.localTime; }
                TraceVisual();
                return;
            }
            if (GamePauseController.IsPaused) return;
            // Riding presents only the player arc; keep the ground mount on its approved timeline.
            if (TryGetComponent<NetworkBombFx>(out var bombFx) && (bombFx.PlayerBounceActive || bombFx.PinkJumpVisualActive || (isLocalPlayer && bombFx.HasLocalMountAbilityPose && !bombFx.RidingArcActive))) { TraceVisual(); return; }
            if (bombFx?.RidingArcActive != true && isLocalPlayer && TryGetComponent<MovementController>(out var movement) && movement.PredictLocally && movement.IsMounted && ownerVisual != null)
            {
                ownerVisual.PresentNetworkLocomotion();
                TraceVisual(); return;
            }
            for (int i = 0; i < renderers.Length && i < frames.Count; i++)
            {
                var animation = renderers[i];
                var sprite = sprites[i];
                var frame = frames[i];
                bool buffered = !isLocalPlayer || GetComponent<NetworkPlayerInput>()?.OwnerUsesAuthorityPresentation == true;
                if (buffered && visualHistory.Count > 0 && !TrySampleMountFrame(GetComponent<NetworkPlayerInput>()?.AuthorityPresentationTime ?? NetworkTime.time, companion.GetMountedLouieType(), i, out frame)) continue;
                if (frame.visible)
                    for (var branch = animation.transform; branch != null && branch != trackedMount.transform; branch = branch.parent)
                        if (!branch.gameObject.activeSelf) branch.gameObject.SetActive(true);
                animation.idle = frame.idle;
                animation.CurrentFrame = frame.frame;
                animation.RefreshFrame();
                animation.transform.localPosition = frame.localPosition;
                if (sprite == null) continue;
                sprite.enabled = frame.visible;
                sprite.flipX = frame.flipX; sprite.flipY = frame.flipY;
                sprite.color = frame.color;
                frame.pose.Apply(transform, sprite);
                if (frame.visible && greenDash != null) greenDash.PresentNetworkAfterimage(animation);
                if (frame.visible && blackDash != null) blackDash.PresentNetworkAfterimage(animation);
            }
            TraceVisual();
        }

        // R01: edge-triggered rows. The 0.05/0.1 s samples can miss (or alias) a one-frame state, so every change of
        // the visible body / mount renderers is logged on the frame it happens, with root, revision and presentation time.
        AnimatedSpriteRenderer[] edgeBodies; float edgeBodiesAt; long lastEdgeMask = -1;
        void TraceBodyEdge()
        {
            if (edgeBodies == null || Time.unscaledTime >= edgeBodiesAt) { edgeBodies = GetComponentsInChildren<AnimatedSpriteRenderer>(true); edgeBodiesAt = Time.unscaledTime + 1f; }
            int mask = 0;
            for (int i = 0; i < edgeBodies.Length && i < 30; i++)
                if (edgeBodies[i] != null && (trackedMount == null || !edgeBodies[i].transform.IsChildOf(trackedMount.transform)) &&
                    edgeBodies[i].TryGetComponent<SpriteRenderer>(out var s) && s.enabled && s.gameObject.activeInHierarchy) mask |= 1 << i;
            int mountMask = 0;
            for (int i = 0; i < renderers.Length && i < 8; i++)
                if (sprites[i] != null && sprites[i].enabled && sprites[i].gameObject.activeInHierarchy) mountMask |= 1 << i;
            long combined = ((long)mask << 8) | (uint)(mountMask & 0xFF);
            if (combined == lastEdgeMask) return;
            bool first = lastEdgeMask == -1;
            lastEdgeMask = combined;
            if (first) return;
            var bodies = new List<string>(); Vector2 offset = default;
            for (int i = 0; i < edgeBodies.Length && i < 30; i++)
                if ((mask & (1 << i)) != 0) { bodies.Add(edgeBodies[i].name); offset = edgeBodies[i].transform.position - transform.position; }
            var visible = new List<string>();
            for (int i = 0; i < renderers.Length && i < 8; i++) if ((mountMask & (1 << i)) != 0) visible.Add(renderers[i].name);
            var fx = GetComponent<NetworkBombFx>();
            var edge = GameplayLatencyDiagnostics.For("mount_body_edge", GetComponent<MovementController>(), detail:
                "type=" + companion.GetMountPresentationType() + ";riding=" + (GetComponent<PlayerRidingController>()?.IsPresentingArc == true) + ";pink=" + (fx?.PinkJumpVisualActive == true) +
                ";mount=" + string.Join(",", visible) + ";body=" + string.Join(",", bodies) + ";rev=" + frameRevision + ";ptime=" +
                (GetComponent<NetworkPlayerInput>()?.AuthorityPresentationTime ?? NetworkTime.time).ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
            edge.delta = offset; GameplayLatencyDiagnostics.Write(edge);
        }

        void TraceVisual()
        {
            if (!GameplayLatencyDiagnostics.Enabled || Time.unscaledTime < nextVisualTrace || renderers == null) return;
            nextVisualTrace = Time.unscaledTime + (isServer && NetworkPlayerAnimation.R01DenseDiagnostics ? (NetworkPlayerAnimation.DenseServerReference ? 0f : .05f) : .1f); // R01: denser server reference (Development only)
            var visible = new List<string>();
            for (int i = 0; i < renderers.Length; i++)
                if (sprites[i] != null && sprites[i].enabled && sprites[i].gameObject.activeInHierarchy) visible.Add(renderers[i].name);
            var bodies = new List<string>(); Vector2 offset = default;
            foreach (var body in GetComponentsInChildren<AnimatedSpriteRenderer>(true))
                if ((trackedMount == null || !body.transform.IsChildOf(trackedMount.transform)) && body.TryGetComponent<SpriteRenderer>(out var sprite) && sprite.enabled && sprite.gameObject.activeInHierarchy)
                { bodies.Add(body.name); offset = body.transform.position - transform.position; }
            var fx = GetComponent<NetworkBombFx>();
            var entry = GameplayLatencyDiagnostics.For("mount_visual_sample", GetComponent<MovementController>(), detail:
                "type=" + companion.GetMountPresentationType() + ";riding=" + (GetComponent<PlayerRidingController>()?.IsPresentingArc == true) + ";pink=" + (fx?.PinkJumpVisualActive == true) + ";mount=" + string.Join(",", visible) + ";body=" + string.Join(",", bodies) + ";rev=" + frameRevision + ";ptime=" + (GetComponent<NetworkPlayerInput>()?.AuthorityPresentationTime ?? NetworkTime.time).ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
            entry.delta = offset; GameplayLatencyDiagnostics.Write(entry);
        }

        public void RestoreOwnerLocomotion()
        {
            if (!isLocalPlayer || ownerVisual == null) return;
            ownerVisual.RefreshHeadOnlyPlayerOffsets();
            ownerVisual.PresentNetworkLocomotion();
        }

        NetworkVisualPose.Baseline[] ownerVisualBaseline;
        public void OwnerAuthorityPresentationChanged(bool active)
        {
            if (active) ownerVisualBaseline ??= NetworkVisualPose.CaptureBaseline(renderers, transform);
            else { NetworkVisualPose.RestoreBaseline(ownerVisualBaseline); ownerVisualBaseline = null; RestoreOwnerLocomotion(); }
        }

        bool RenderersDestroyed()
        {
            if (renderers == null) return false;
            foreach (var renderer in renderers) if (renderer == null) return true;
            return false;
        }

        void Rebind(GameObject mount)
        {
            NetworkVisualPose.RestoreBaseline(ownerVisualBaseline); ownerVisualBaseline = null;
            trackedMount = mount;
            ownerVisual = mount != null ? mount.GetComponentInChildren<MountVisualController>(true) : null;
            if (!isServer && isLocalPlayer && ownerVisual != null && TryGetComponent<MovementController>(out var movement))
            {
                movement.SetUseHeadOnlyWhenMounted(ownerVisual.useHeadOnlyPlayerVisual);
                movement.SetMountedSpritesLocalYOverride(companion.GetMountedLouieType() == MountedType.Pink, movement.pinkMountedSpritesLocalY);
                ownerVisual.RefreshHeadOnlyPlayerOffsets();
            }
            greenDash = mount != null ? mount.GetComponentInChildren<GreenLouieDashAnimator>(true) : null;
            blackDash = mount != null ? mount.GetComponentInChildren<BlackLouieDashAnimator>(true) : null;
            renderers = mount != null ? mount.GetComponentsInChildren<AnimatedSpriteRenderer>(true) : null;
            sprites = renderers != null ? new SpriteRenderer[renderers.Length] : null;
            if (isServer) { ++frameRevision; frames.Clear(); }
            if (renderers == null) return;
            for (int i = 0; i < renderers.Length; i++)
            {
                sprites[i] = renderers[i].GetComponent<SpriteRenderer>();
                if (!isServer) renderers[i].SetManualAnimationUpdate(true);
            }
            if (!isServer)
                foreach (var behaviour in mount.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    behaviour.StopAllCoroutines();
                    behaviour.enabled = false;
                }
            if (isLocalPlayer && GetComponent<NetworkPlayerInput>()?.OwnerUsesAuthorityPresentation == true)
                ownerVisualBaseline = NetworkVisualPose.CaptureBaseline(renderers, transform);
            if (isLocalPlayer) GetComponent<NetworkBombFx>()?.ReapplyMountAbilityPose();
        }
    }
}

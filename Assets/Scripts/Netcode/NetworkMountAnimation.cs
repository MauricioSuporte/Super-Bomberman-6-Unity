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
                if (interval > 0 && interval <= 0.25 && frame.visible && target.visible && frame.idle == target.idle &&
                    Vector3.Distance(frame.localPosition, target.localPosition) <= 4)
                {
                    float t = Mathf.Clamp01((float)((timeline - from.time) / interval));
                    frame.localPosition = Vector3.Lerp(frame.localPosition, target.localPosition, t);
                    if (Vector3.Distance(frame.pose.position, target.pose.position) <= 4) frame.pose = NetworkVisualPose.Interpolate(frame.pose, target.pose, t);
                }
            }
            return true;
        }

        PlayerMountCompanion companion;
        GameObject trackedMount;
        AnimatedSpriteRenderer[] renderers;
        SpriteRenderer[] sprites;
        float nextSample;
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
            if (!NetSync.IsOnline || netIdentity == null || (!isServer && !isClient)) return;
            var mount = companion != null ? companion.GetMountedLouieObject() : null;
            if (mount != trackedMount) Rebind(mount);
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
                if (changed) { frameType = companion.GetMountedLouieType(); frameTime = NetworkTime.localTime; }
                return;
            }
            if (GamePauseController.IsPaused) return;
            if (TryGetComponent<NetworkBombFx>(out var bombFx) && (bombFx.RidingArcActive || bombFx.PlayerBounceActive || bombFx.PinkJumpVisualActive || (isLocalPlayer && bombFx.HasLocalMountAbilityPose))) return;
            if (isLocalPlayer && TryGetComponent<MovementController>(out var movement) && movement.PredictLocally && movement.IsMounted && ownerVisual != null)
            {
                ownerVisual.PresentNetworkLocomotion();
                return;
            }
            for (int i = 0; i < renderers.Length && i < frames.Count; i++)
            {
                var animation = renderers[i];
                var sprite = sprites[i];
                var frame = frames[i];
                bool buffered = !isLocalPlayer || GetComponent<NetworkPlayerInput>()?.OwnerUsesAuthorityPresentation == true;
                if (buffered && visualHistory.Count > 0 && !TrySampleMountFrame(NetworkTime.time, companion.GetMountedLouieType(), i, out frame)) continue;
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
        }

        NetworkVisualPose.Baseline[] ownerVisualBaseline;
        public void OwnerAuthorityPresentationChanged(bool active)
        {
            if (active) ownerVisualBaseline ??= NetworkVisualPose.CaptureBaseline(renderers, transform);
            else { NetworkVisualPose.RestoreBaseline(ownerVisualBaseline); ownerVisualBaseline = null; }
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

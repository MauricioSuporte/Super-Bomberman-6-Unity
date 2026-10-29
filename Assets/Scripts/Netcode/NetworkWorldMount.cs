using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace Assets.Scripts.Netcode
{
    // World pickups share their gameplay prefab with attached mount visuals.
    // A separate identity avoids introducing nested identities on players.
    [RequireComponent(typeof(NetworkIdentity))]
    public sealed class NetworkWorldMount : NetworkBehaviour
    {
        [SerializeField] PlayerMountCompanion mountCatalog;
        [SyncVar] MountedType mountType;
        [SyncVar] bool contactAvailable;
        [SyncVar] Vector3 worldPosition;
        [SyncVar] Quaternion worldRotation;
        [SyncVar] Vector3 worldScale;
        public readonly SyncList<MountFrame> frames = new();
        [SyncVar(hook = nameof(OnWorldMountTime))] double sampleTime;
        struct WorldMountSample
        {
            public double time;
            public Vector3 position, scale;
            public Quaternion rotation;
            public MountFrame[] frames;
        }
        readonly List<WorldMountSample> worldHistory = new(32);
        bool worldPresented;
        MountFrame[] sampledWorldFrames;

        void OnWorldMountTime(double previous, double current)
        {
            if (isServer || NetSync.Mode != NetSync.NetMode.Client || !NetworkClient.active || !double.IsFinite(current)) return;
            if (worldHistory.Count > 0 && current <= worldHistory[worldHistory.Count - 1].time) return;
            var copy = new MountFrame[frames.Count];
            for (int i = 0; i < copy.Length; i++) copy[i] = frames[i];
            worldHistory.Add(new WorldMountSample { time = current, position = worldPosition, rotation = worldRotation, scale = worldScale, frames = copy });
            if (worldHistory.Count > 32) worldHistory.RemoveAt(0);
        }

        public bool SampleWorldMount(double timeline, out Vector3 position, out Quaternion rotation, out Vector3 scale, out MountFrame[] values)
        {
            position = worldPosition; rotation = worldRotation; scale = worldScale; values = null;
            if (worldHistory.Count == 0) return false;
            var a = worldHistory[0]; var b = a;
            for (int i = 1; i < worldHistory.Count; i++) { b = worldHistory[i]; if (b.time >= timeline) break; a = b; }
            if (b.time - a.time > .25 || Vector3.Distance(a.position, b.position) > 4) { a = b; }
            float t = b.time > a.time ? Mathf.Clamp01((float)((timeline - a.time) / (b.time - a.time))) : 0;
            position = Vector3.Lerp(a.position, b.position, t); rotation = Quaternion.Slerp(a.rotation, b.rotation, t); scale = Vector3.Lerp(a.scale, b.scale, t);
            if (sampledWorldFrames == null || sampledWorldFrames.Length != a.frames.Length) sampledWorldFrames = new MountFrame[a.frames.Length];
            values = sampledWorldFrames;
            for (int i = 0; i < values.Length; i++)
            {
                values[i] = a.frames[i];
                if (i < b.frames.Length && values[i].visible && b.frames[i].visible)
                { var value = values[i]; value.pose = NetworkVisualPose.Interpolate(value.pose, b.frames[i].pose, t); values[i] = value; }
            }
            return true;
        }

        public struct MountFrame : IEquatable<MountFrame>
        {
            public bool visible, idle, flipX, flipY;
            public int frame;
            public Color color;
            public Vector3 localPosition;
            public NetworkVisualPose pose;
            public bool Equals(MountFrame other) => visible == other.visible && idle == other.idle &&
                flipX == other.flipX && flipY == other.flipY && frame == other.frame &&
                color.Equals(other.color) && localPosition.Equals(other.localPosition) && pose.Equals(other.pose);
        }

        MountWorldPickup source;
        AnimatedSpriteRenderer[] animations;
        SpriteRenderer[] sprites;
        GameObject visual;
        float nextSample;

        public static bool Spawn(MountWorldPickup pickup)
        {
            if (!NetworkServer.active || pickup == null || pickup.Type == MountedType.None) return false;
            if (pickup.NetworkPresentation != null) return true;
            var manager = NetworkManager.singleton;
            if (manager == null) return false;
            foreach (var prefab in manager.spawnPrefabs)
            {
                if (prefab == null || !prefab.TryGetComponent<NetworkWorldMount>(out var definition)) continue;
                if (definition.mountCatalog == null || definition.mountCatalog.GetMountPrefabForType(pickup.Type) == null) return false;
                var proxy = Instantiate(prefab).GetComponent<NetworkWorldMount>();
                proxy.source = pickup;
                pickup.NetworkPresentation = proxy;
                proxy.mountType = pickup.Type;
                proxy.BindAnimations(pickup.gameObject);
                proxy.Capture();
                NetworkServer.Spawn(proxy.gameObject);
                Debug.Log($"[WorldMount] spawned type={pickup.Type} netId={proxy.netId}");
                return true;
            }
            return false;
        }

        void Awake() => syncInterval = 0.05f;

        void BindAnimations(GameObject target)
        {
            animations = target.GetComponentsInChildren<AnimatedSpriteRenderer>(true);
            sprites = new SpriteRenderer[animations.Length];
            for (int i = 0; i < animations.Length; i++) sprites[i] = animations[i].GetComponent<SpriteRenderer>();
        }

        public override void OnStartClient()
        {
            if (isServer) return;
            var prefab = mountCatalog != null ? mountCatalog.GetMountPrefabForType(mountType) : null;
            if (prefab == null) return;
            visual = Instantiate(prefab, transform);
            // This clone only presents authoritative output. It cannot collide,
            // collect a pickup, die or run an ownerless movement controller.
            foreach (var behaviour in visual.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
            foreach (var collider in visual.GetComponentsInChildren<Collider2D>(true)) collider.enabled = false;
            foreach (var body in visual.GetComponentsInChildren<Rigidbody2D>(true)) body.simulated = false;
            BindAnimations(visual);
            foreach (var animation in animations) animation.SetManualAnimationUpdate(true);
            OnWorldMountTime(0, sampleTime);
            Apply();
        }

        // Read only: native clone components remain disabled. Unknown/moving
        // pickups wait for the approved arc rather than guessing their destination.
        public bool TryGetPredictedMountContact(Collider2D actor, out Vector3 target, out MountVisualController definition, out MountedType type)
        {
            target = default; definition = null; type = MountedType.None;
            if (NetSync.Mode != NetSync.NetMode.Client || !NetworkClient.active || isServer ||
                netId == 0 || !contactAvailable || visual == null || actor == null || !actor.enabled || !actor.gameObject.activeInHierarchy ||
                actor.gameObject.scene != gameObject.scene || worldHistory.Count < 2 ||
                !NetworkClient.spawned.TryGetValue(netId, out var spawned) || spawned != netIdentity ||
                NetworkTime.time - sampleTime > .25 || sampleTime > NetworkTime.predictedTime + .1 ||
                Vector3.Distance(worldHistory[worldHistory.Count - 2].position, worldPosition) > .001f ||
                !visual.TryGetComponent<CircleCollider2D>(out var shape)) return false;
            bool visible = false;
            foreach (var frame in frames) visible |= frame.visible;
            if (!visible) return false;
            Vector2 center = shape.transform.TransformPoint(shape.offset);
            float radius = shape.radius * Mathf.Max(Mathf.Abs(shape.transform.lossyScale.x), Mathf.Abs(shape.transform.lossyScale.y));
            if (Vector2.Distance(actor.ClosestPoint(center), center) > radius) return false;
            definition = visual.GetComponentInChildren<MountVisualController>(true);
            if (definition == null || mountType == MountedType.None) return false;
            target = MountWorldPickup.ResolveLandingWorldPosition(center); type = mountType;
            return true;
        }

        public override void OnStopClient()
        {
            worldHistory.Clear(); worldPresented = false; sampledWorldFrames = null;
            if (!isServer && visual != null) Destroy(visual);
            visual = null;
            base.OnStopClient();
        }

        public override void OnStopServer()
        {
            if (source != null && source.NetworkPresentation == this) source.NetworkPresentation = null;
            base.OnStopServer();
        }

        void LateUpdate()
        {
            if (isServer)
            {
                // Consumption transfers the original mount to PlayerMountCompanion;
                // NetworkPlayerState then supplies its attached client visual.
                if (source == null || !source.IsAvailable || source.transform.parent != null)
                {
                    Debug.Log($"[WorldMount] removed type={mountType} netId={netId}");
                    NetworkServer.Destroy(gameObject);
                    return;
                }
                if (Time.unscaledTime >= nextSample)
                {
                    nextSample = Time.unscaledTime + syncInterval;
                    Capture();
                }
            }
            else if (isClient && visual != null) Apply();
        }

        void Capture()
        {
            contactAvailable = source.IsAvailable && source.TryGetComponent<Collider2D>(out var sourceCollider) && sourceCollider.enabled;
            worldPosition = source.transform.position;
            worldRotation = source.transform.rotation;
            worldScale = source.transform.lossyScale;
            for (int i = 0; i < animations.Length; i++)
            {
                var animation = animations[i];
                var sprite = sprites[i];
                var frame = new MountFrame
                {
                    visible = sprite != null && sprite.enabled && sprite.gameObject.activeInHierarchy,
                    idle = animation.idle, frame = animation.CurrentFrame,
                    flipX = sprite != null && sprite.flipX, flipY = sprite != null && sprite.flipY,
                    color = sprite != null ? sprite.color : Color.white,
                    localPosition = animation.transform.localPosition, pose = NetworkVisualPose.Capture(source.transform, sprite)
                };
                if (frames.Count <= i) frames.Add(frame);
                else if (!frames[i].Equals(frame)) frames[i] = frame;
            }
            sampleTime = NetworkTime.localTime;
        }

        void Apply()
        {
            if (GamePauseController.IsPaused && worldPresented) return;
            SampleWorldMount(NetworkTime.time, out var position, out var rotation, out var scale, out var buffered);
            transform.SetPositionAndRotation(position, rotation);
            transform.localScale = scale; worldPresented = true;
            for (int i = 0; i < animations.Length && i < frames.Count; i++)
            {
                var animation = animations[i];
                var sprite = sprites[i];
                var frame = buffered != null && i < buffered.Length ? buffered[i] : frames[i];
                if (frame.visible && !animation.gameObject.activeSelf) animation.gameObject.SetActive(true);
                animation.idle = frame.idle;
                animation.CurrentFrame = frame.frame;
                animation.RefreshFrame();
                animation.transform.localPosition = frame.localPosition;
                if (sprite == null) continue;
                sprite.enabled = frame.visible;
                sprite.flipX = frame.flipX; sprite.flipY = frame.flipY;
                sprite.color = frame.color;
                frame.pose.Apply(transform, sprite);
            }
        }
    }
}

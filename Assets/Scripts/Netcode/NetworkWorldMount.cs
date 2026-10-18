using System;
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
        [SyncVar] Vector3 worldPosition;
        [SyncVar] Quaternion worldRotation;
        [SyncVar] Vector3 worldScale;
        public readonly SyncList<MountFrame> frames = new();

        public struct MountFrame : IEquatable<MountFrame>
        {
            public bool visible, idle, flipX, flipY;
            public int frame;
            public Color color;
            public Vector3 localPosition;
            public bool Equals(MountFrame other) => visible == other.visible && idle == other.idle &&
                flipX == other.flipX && flipY == other.flipY && frame == other.frame &&
                color.Equals(other.color) && localPosition.Equals(other.localPosition);
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
            Apply();
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
                    localPosition = animation.transform.localPosition
                };
                if (frames.Count <= i) frames.Add(frame);
                else if (!frames[i].Equals(frame)) frames[i] = frame;
            }
        }

        void Apply()
        {
            transform.SetPositionAndRotation(worldPosition, worldRotation);
            transform.localScale = worldScale;
            for (int i = 0; i < animations.Length && i < frames.Count; i++)
            {
                var animation = animations[i];
                var sprite = sprites[i];
                var frame = frames[i];
                if (frame.visible && !animation.gameObject.activeSelf) animation.gameObject.SetActive(true);
                animation.idle = frame.idle;
                animation.CurrentFrame = frame.frame;
                animation.RefreshFrame();
                animation.transform.localPosition = frame.localPosition;
                if (sprite == null) continue;
                sprite.enabled = frame.visible;
                sprite.flipX = frame.flipX; sprite.flipY = frame.flipY;
                sprite.color = frame.color;
            }
        }
    }
}

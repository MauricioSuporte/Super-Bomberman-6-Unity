using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace Assets.Scripts.Netcode
{
    // Queue gameplay and NetworkTransform remain authoritative. Visual state is
    // timestamped to the same observer timeline instead of inferring idle/facing
    // from a stationary interpolated frame or losing queue visibility changes.
    [DefaultExecutionOrder(250)]
    [RequireComponent(typeof(NetworkIdentity))]
    public class NetworkEggFollower : NetworkBehaviour
    {
        // Queue simulation stays on the server; clients display followers and destruction.
        public static bool ReplicationEnabled = true;

        // 0 = vivo; 1 = destruído normal; 2 = destruído por explosão.
        [SyncVar(hook = nameof(OnDestroyKindChanged))] byte destroyKind;

        public struct QueuePresentation
        {
            public double time;
            public Vector2 facing;
            public int sortingLayer, sortingOrder;
            public bool valid, visible, idle;
        }

        [SyncVar(hook = nameof(OnQueuePresentationChanged))] QueuePresentation queuePresentation;
        readonly List<QueuePresentation> pendingPresentation = new();
        readonly Dictionary<Collider2D, bool> hiddenHitboxes = new();
        SpriteRenderer[] sprites;
        NetworkTransformReliable positionSync;
        EggFollowerDirectionalVisual directional;
        EggFollowerDestroyVisual destroyVisual;

        Vector3 lastPos;
        bool hasLastPos;

        void Awake()
        {
            sprites = GetComponentsInChildren<SpriteRenderer>(true);
            positionSync = GetComponent<NetworkTransformReliable>();
            directional = GetComponentInChildren<EggFollowerDirectionalVisual>(true);
            destroyVisual = GetComponentInChildren<EggFollowerDestroyVisual>(true);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (isServer) return;
            if (sprites == null) Awake();
            ReceiveQueuePresentation(queuePresentation);
            ApplyQueuePresentation(NetworkTime.time);
            foreach (var hitbox in GetComponentsInChildren<Collider2D>(true)) hitbox.enabled = false;
        }

        void LateUpdate()
        {
            // Keep the queue's approved state aligned with transform interpolation.
            // Destruction owns the renderers once its authoritative event arrives.
            if (!ReplicationEnabled || !NetSync.IsOnline || !isClient || isServer || directional == null || destroyKind != 0)
                return;

            if (pendingPresentation.Count > 0)
            {
                ApplyQueuePresentation(NetworkTime.time);
                return;
            }
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

        public void CaptureQueuePresentation(bool visible)
        {
            if (!ReplicationEnabled || !NetSync.IsServer || !isServer || netId == 0 || destroyKind != 0) return;
            if (sprites == null) Awake();
            var first = sprites.Length > 0 ? sprites[0] : null;
            var next = new QueuePresentation { valid = true, visible = visible,
                facing = directional != null ? directional.facing : Vector2.down,
                idle = directional == null || directional.IsIdle,
                sortingLayer = first != null ? first.sortingLayerID : 0,
                sortingOrder = first != null ? first.sortingOrder : 0 };
            var old = queuePresentation;
            if (old.valid && old.visible == next.visible && old.idle == next.idle && old.facing == next.facing &&
                old.sortingLayer == next.sortingLayer && old.sortingOrder == next.sortingOrder) return;
            next.time = NetworkTime.localTime;
            queuePresentation = next;
        }

        public bool SetServerQueueVisibility(bool visible)
        {
            if (!ReplicationEnabled || !NetSync.IsServer || !isServer || netId == 0) return false;
            if (sprites == null) Awake();
            foreach (var sprite in sprites) sprite.forceRenderingOff = !visible;
            if (!visible && hiddenHitboxes.Count == 0)
                foreach (var hitbox in GetComponentsInChildren<Collider2D>(true))
                {
                    hiddenHitboxes[hitbox] = hitbox.enabled;
                    hitbox.enabled = false;
                }
            if (visible)
            {
                foreach (var pair in hiddenHitboxes)
                    if (pair.Key != null) pair.Key.enabled = pair.Value;
                hiddenHitboxes.Clear();
            }
            // Mirror excludes inactive identities when a new observer joins.
            // Keep the transport alive while matching the original hidden hitboxes.
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            return true;
        }

        void OnQueuePresentationChanged(QueuePresentation previous, QueuePresentation current) => ReceiveQueuePresentation(current);

        public void ReceiveQueuePresentation(QueuePresentation state)
        {
            if (!state.valid || NetSync.Mode != NetSync.NetMode.Client || isServer || !NetworkClient.active) return;
            if (positionSync == null) positionSync = GetComponent<NetworkTransformReliable>();
            if (positionSync != null)
                state.time += NetworkClient.sendInterval * (positionSync.sendIntervalMultiplier - 1 +
                    (positionSync.timelineOffset ? positionSync.sendIntervalMultiplier : 0));
            if (pendingPresentation.Count > 0 && state.time <= pendingPresentation[^1].time) return;
            pendingPresentation.Add(state);
            if (pendingPresentation.Count > 32) pendingPresentation.RemoveAt(0);
        }

        void ApplyQueuePresentation(double timeline)
        {
            if (NetSync.Mode != NetSync.NetMode.Client || isServer || !NetworkClient.active || destroyKind != 0 ||
                pendingPresentation.Count == 0) return;
            while (pendingPresentation.Count > 1 && pendingPresentation[1].time <= timeline) pendingPresentation.RemoveAt(0);
            var state = pendingPresentation[0];
            if (sprites == null) Awake();
            directional?.PresentNetworkState(state.facing, state.idle);
            foreach (var sprite in sprites)
            {
                sprite.forceRenderingOff = !state.visible;
                sprite.sortingLayerID = state.sortingLayer;
                sprite.sortingOrder = state.sortingOrder;
            }
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

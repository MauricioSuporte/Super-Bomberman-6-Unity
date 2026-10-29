using System.Collections.Generic;
using Assets.Scripts.Netcode;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Assets.Scripts.Netcode
{
    public struct NetworkItemMotion : NetworkMessage
    {
        public string scene;
        public uint netId, revision;
        public Vector2 start, end, direction;
        public Quaternion rotation;
        public float duration;
        public bool moving, pushed, recovering;
        public float elapsed;
    }
}

public partial class ItemPickup
{
    uint itemMotionRevision, receivedItemRevision;
    bool hasItemRevision, itemPresentationActive;
    NetworkItemMotion serverItemMotion, clientItemMotion;
    readonly Queue<NetworkItemMotion> itemMotionQueue = new();
    readonly Dictionary<int, uint> itemMotionPeers = new();
    float itemPresentationElapsed, itemHeartbeatAt, itemPushSampleAt, serverItemStartedAt;
    Vector2 itemPushOrigin;
    Quaternion itemPushRotation, presentedItemRotation;
    Vector2 presentedItemPosition;

    static bool FiniteItemVector(Vector2 value) => float.IsFinite(value.x) && float.IsFinite(value.y);
    public static bool ValidNetworkItemMotion(NetworkItemMotion motion) => motion.netId != 0 &&
        FiniteItemVector(motion.start) && FiniteItemVector(motion.end) && FiniteItemVector(motion.direction) &&
        float.IsFinite(motion.duration) && motion.duration >= 0 && motion.duration <= 2 &&
        float.IsFinite(motion.elapsed) && motion.elapsed >= 0 && motion.elapsed <= 2 &&
        float.IsFinite(motion.rotation.x) && float.IsFinite(motion.rotation.y) &&
        float.IsFinite(motion.rotation.z) && float.IsFinite(motion.rotation.w);

    void SendNetworkItemMotion(NetworkItemMotion motion)
    {
        if (!NetSync.IsServer || !NetworkServer.active || !TryGetComponent<NetworkIdentity>(out var identity) || identity.netId == 0) return;
        motion.scene = gameObject.scene.name; motion.netId = identity.netId; motion.revision = ++itemMotionRevision;
        serverItemMotion = motion; serverItemStartedAt = Time.time; itemHeartbeatAt = Time.unscaledTime + .25f;
        SendNetworkItemToNewPeers();
    }
    void SendNetworkItemSegment(Vector2 start, Vector2 end, Vector2 direction, float duration) =>
        SendNetworkItemMotion(new NetworkItemMotion { start = start, end = end, direction = direction,
            duration = duration, moving = true, rotation = Quaternion.identity });
    void SendNetworkItemStop(Vector2 position) =>
        SendNetworkItemMotion(new NetworkItemMotion { start = position, end = position, rotation = Quaternion.identity });
    void SendNetworkItemPushPose()
    {
        if (Time.unscaledTime < itemPushSampleAt) return;
        itemPushSampleAt = Time.unscaledTime + .05f;
        SendNetworkItemMotion(new NetworkItemMotion { start = transform.position, end = transform.position,
            rotation = transform.rotation, duration = .05f, moving = true, pushed = true });
    }
    void PublishNetworkItemHeartbeat()
    {
        if (!NetSync.IsServer || !NetworkServer.active || serverItemMotion.netId == 0 || Time.unscaledTime < itemHeartbeatAt) return;
        itemHeartbeatAt = Time.unscaledTime + .25f;
        SendNetworkItemToNewPeers(); // Recover late observers without idle broadcast traffic.
    }

    void SendNetworkItemToNewPeers()
    {
        foreach (var peer in NetworkServer.connections.Values)
        {
            if (peer == null || peer == NetworkServer.localConnection || !peer.isReady || !peer.isAuthenticated ||
                (itemMotionPeers.TryGetValue(peer.connectionId, out uint revision) && revision == serverItemMotion.revision)) continue;
            var motion = serverItemMotion;
            if (!itemMotionPeers.ContainsKey(peer.connectionId))
            {
                motion.recovering = true;
                motion.elapsed = motion.pushed ? motion.duration : Mathf.Clamp(Time.time - serverItemStartedAt, 0, motion.duration);
            }
            peer.Send(motion); itemMotionPeers[peer.connectionId] = serverItemMotion.revision;
        }
    }

    public void ReceiveNetworkItemMotion(NetworkItemMotion motion)
    {
        if (NetSync.Mode != NetSync.NetMode.Client || !NetworkClient.active || NetworkServer.active ||
            !TryGetComponent<NetworkIdentity>(out var identity) || identity.netId != motion.netId ||
            motion.scene != gameObject.scene.name || !ValidNetworkItemMotion(motion) ||
            (hasItemRevision && !NetworkInputHistory.IsNewer(motion.revision, receivedItemRevision))) return;
        hasItemRevision = true; receivedItemRevision = motion.revision;
        if (itemPresentationActive && clientItemMotion.moving && itemPresentationElapsed < clientItemMotion.duration && !clientItemMotion.pushed)
        {
            if (itemMotionQueue.Count < 32) { itemMotionQueue.Enqueue(motion); return; }
            itemMotionQueue.Clear(); // Bounded recovery after a prolonged delivery stall.
        }
        BeginNetworkItemMotion(motion);
    }
    void BeginNetworkItemMotion(NetworkItemMotion motion)
    {
        itemPushOrigin = itemPresentationActive ? presentedItemPosition : (Vector2)transform.position;
        itemPushRotation = itemPresentationActive ? presentedItemRotation : transform.rotation;
        clientItemMotion = motion; itemPresentationElapsed = motion.recovering ? motion.elapsed : 0; itemPresentationActive = true;
    }
    public static Vector2 SampleNetworkItemSegment(Vector2 start, Vector2 end, Vector2 direction, float progress)
    {
        progress = Mathf.Clamp01(progress);
        bool horizontal = Mathf.Abs(direction.x) >= Mathf.Abs(direction.y);
        bool wrap = horizontal ? Mathf.Abs(end.x - start.x) > 1.5f : Mathf.Abs(end.y - start.y) > 1.5f;
        return wrap ? (progress < .5f ? start : end) : Vector2.Lerp(start, end, Mathf.SmoothStep(0, 1, progress));
    }
    public void PresentNetworkItemMotion(float dt)
    {
        if (!itemPresentationActive || NetSync.Mode != NetSync.NetMode.Client || NetworkServer.active || !NetworkClient.active) return;
        if (!GamePauseController.IsPaused) itemPresentationElapsed += Mathf.Max(0, dt);
        // Preserve short rolls and their final stop in receive order; never invent the next landing tile.
        int transitions = 0;
        while (itemPresentationElapsed >= clientItemMotion.duration && itemMotionQueue.Count > 0 && transitions++ < 32)
        {
            float remainder = Mathf.Min(.02f, Mathf.Max(0, itemPresentationElapsed - clientItemMotion.duration));
            BeginNetworkItemMotion(itemMotionQueue.Dequeue()); itemPresentationElapsed = Mathf.Max(itemPresentationElapsed, remainder);
        }
        var motion = clientItemMotion;
        float progress = motion.duration <= 0 ? 1 : Mathf.Clamp01(itemPresentationElapsed / motion.duration);
        Vector2 position = !motion.moving ? motion.end : motion.pushed ? Vector2.Lerp(itemPushOrigin, motion.end, progress) :
            SampleNetworkItemSegment(motion.start, motion.end, motion.direction, progress);
        transform.position = new Vector3(position.x, position.y, transform.position.z);
        bool horizontal = Mathf.Abs(motion.direction.x) >= Mathf.Abs(motion.direction.y);
        transform.rotation = !motion.moving ? motion.rotation : motion.pushed ? Quaternion.Slerp(itemPushRotation, motion.rotation, progress) :
            progress >= 1 ? Quaternion.identity : horizontal ? Quaternion.Euler(0, (motion.direction.x >= 0 ? -1 : 1) * progress * 360, 0) :
            Quaternion.Euler((motion.direction.y >= 0 ? 1 : -1) * progress * 360, 0, 0);
        presentedItemPosition = position; presentedItemRotation = transform.rotation;
    }
    bool TickNetworkItemPresentation()
    {
        if (NetSync.Mode != NetSync.NetMode.Client || !NetworkClient.active || !TryGetComponent<NetworkIdentity>(out var identity) || identity.netId == 0) return false;
        PresentNetworkItemMotion(Time.deltaTime); return true;
    }
    void OnDisable()
    {
        itemMotionQueue.Clear(); itemMotionPeers.Clear(); itemPresentationActive = false; hasItemRevision = false;
    }
}

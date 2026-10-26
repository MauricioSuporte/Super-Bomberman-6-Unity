using System.Collections.Generic;
using Assets.Scripts.Netcode;
using Mirror;
using UnityEngine;

namespace Assets.Scripts.Netcode
{
    public struct CartPortalStar : NetworkMessage
    {
        public string scene, path;
        public uint revision;
        public double time;
        public Vector3 position, scale;
        public Vector2 drift;
        public float angle, spin, life, frameSeconds;
        public int sprite, sorting;
        public bool clear;
    }

    public struct MinecartView : NetworkMessage
    {
        public string scene, path;
        public uint revision, rider;
        public double time;
        public Vector3 position, scale;
        public Vector2 direction;
        public byte renderer;
        public NetworkWorldMount.MountFrame frame;
        public bool loop;
    }
}

public sealed partial class BattleMode9MinecartController
{
    uint networkStarRevision, receivedStarRevision;

    void PublishNetworkStar(Transform star, Sprite sprite, Vector2 drift, float spin, bool clear = false)
    {
        if (!NetSync.IsServer || !NetworkServer.active || NetworkServer.isLoadingScene || BombermanNetworkManager.IsCoopStage || !IsSupportedSceneActive()) return;
        NetworkServer.SendToReady(new CartPortalStar { scene = gameObject.scene.name, path = NetworkCartPath,
            revision = ++networkStarRevision, time = NetworkTime.localTime, clear = clear,
            position = star != null ? star.position : Vector3.zero, scale = star != null ? star.localScale : Vector3.one,
            angle = star != null ? star.eulerAngles.z : 0, drift = drift, spin = spin,
            life = teleportStarLifetime, frameSeconds = teleportStarAnimationFrameTime,
            sprite = teleportStarSprites != null ? System.Array.IndexOf(teleportStarSprites, sprite) : -1, sorting = teleportStarSortingOrder });
    }

    public void ReceiveNetworkStar(CartPortalStar view)
    {
        if (NetSync.Mode != NetSync.NetMode.Client || !NetworkClient.active || NetworkServer.active || BombermanNetworkManager.IsCoopStage ||
            !isActiveAndEnabled || view.scene != gameObject.scene.name || !IsSupportedScene(view.scene) || view.path != NetworkCartPath ||
            !NetworkInputHistory.IsNewer(view.revision, receivedStarRevision)) return;
        receivedStarRevision = view.revision;
        if (view.clear) { lastStarClearRevision = view.revision; ClearActiveTeleportStars(); return; }
        if ((float)(NetworkBombFx.KickServerNow - view.time) > Mathf.Max(1, view.life + 0.5f)) return;
        StartCoroutine(PresentNetworkStar(view));
    }

    System.Collections.IEnumerator PresentNetworkStar(CartPortalStar view)
    {
        float pausedWait = 0;
        while (GamePauseController.IsPaused || NetworkTime.time < view.time + cartTimelineOffset)
        {
            if (!isActiveAndEnabled || !NetworkClient.active || !NetworkInputHistory.IsNewer(view.revision, lastStarClearRevision)) yield break;
            if (GamePauseController.IsPaused) pausedWait += Time.unscaledDeltaTime;
            yield return null;
        }
        if (!isActiveAndEnabled || !NetworkClient.active || !NetworkInputHistory.IsNewer(view.revision, lastStarClearRevision)) yield break;
        float age = Mathf.Clamp((float)(NetworkTime.time - view.time - cartTimelineOffset) - pausedWait, 0, 0.15f);
        var star = CreateNetworkStarView(view, age);
        if (star != null) yield return AnimateTeleportStar(star.transform, star.GetComponent<SpriteRenderer>(), view.drift, view.spin,
            age, view.life, view.frameSeconds);
    }

    uint lastStarClearRevision;

    public GameObject CreateNetworkStarView(CartPortalStar view, float age)
    {
        if (NetSync.Mode != NetSync.NetMode.Client || !NetworkClient.active || NetworkServer.active || view.clear ||
            view.scene != gameObject.scene.name || view.path != NetworkCartPath || teleportStarSprites == null ||
            view.sprite < 0 || view.sprite >= teleportStarSprites.Length || teleportStarSprites[view.sprite] == null) return null;
        var star = new GameObject("ApprovedCartPortalStar"); star.transform.SetParent(transform, worldPositionStays: true);
        star.transform.SetPositionAndRotation(view.position + (Vector3)view.drift * age, Quaternion.Euler(0, 0, view.angle + view.spin * age));
        star.transform.localScale = view.scale;
        var renderer = star.AddComponent<SpriteRenderer>(); renderer.sprite = teleportStarSprites[view.sprite]; renderer.sortingOrder = view.sorting;
        activeTeleportStars.Add(star);
        return star;
    }

    readonly List<MinecartView> networkCartHistory = new(64);
    string networkCartPath;
    uint networkCartRevision, receivedCartRevision;
    float nextCartSample, nextCartHeartbeat;
    bool networkRideLoop;
    double cartTimelineOffset;
    MinecartView lastCartSent;
    public string NetworkCartPath
    {
        get
        {
            if (networkCartPath != null) return networkCartPath;
            networkCartPath = name;
            for (var parent = transform.parent; parent != null; parent = parent.parent) networkCartPath = parent.name + "/" + networkCartPath;
            return networkCartPath;
        }
    }

    AnimatedSpriteRenderer CartRenderer(byte index) => index switch { 0 => down, 1 => up, 2 => left, 3 => right, _ => null };

    void PublishNetworkCart(bool hidden = false)
    {
        if (!NetSync.IsServer || !NetworkServer.active || NetworkServer.isLoadingScene || BombermanNetworkManager.IsCoopStage || !IsSupportedSceneActive()) return;
        if (!hidden && Time.unscaledTime < nextCartSample) return;
        nextCartSample = Time.unscaledTime + 0.05f;
        var view = new MinecartView { scene = gameObject.scene.name, path = NetworkCartPath,
            position = transform.position, scale = Vector3.one, direction = currentCartDirection, renderer = 255,
            rider = currentRider != null && currentRider.TryGetComponent<NetworkIdentity>(out var rider) ? rider.netId : 0,
            loop = !hidden && audioSource != null && audioSource.loop && audioSource.clip == rideLoopSfx && !rideLoopPausedForJump };
        if (!hidden)
            for (byte i = 0; i < 4; i++)
            {
                var animation = CartRenderer(i);
                if (animation == null || !animation.TryGetComponent<SpriteRenderer>(out var sprite) || !sprite.enabled || !sprite.gameObject.activeInHierarchy) continue;
                view.renderer = i; view.scale = animation.transform.localScale;
                view.frame = new NetworkWorldMount.MountFrame { visible = true, frame = animation.CurrentFrame, idle = animation.idle,
                    flipX = sprite.flipX, flipY = sprite.flipY, color = sprite.color,
                    localPosition = transform.InverseTransformPoint(animation.transform.position) };
                break;
            }
        bool changed = view.position != lastCartSent.position || view.scale != lastCartSent.scale || view.direction != lastCartSent.direction || view.renderer != lastCartSent.renderer ||
            view.rider != lastCartSent.rider || view.loop != lastCartSent.loop || !view.frame.Equals(lastCartSent.frame);
        if (!hidden && !changed && Time.unscaledTime < nextCartHeartbeat) return;
        nextCartHeartbeat = Time.unscaledTime + 0.5f;
        view.time = NetworkTime.localTime; view.revision = ++networkCartRevision;
        lastCartSent = view; NetworkServer.SendToReady(view);
    }

    public void ReceiveNetworkCart(MinecartView view)
    {
        if (NetSync.Mode != NetSync.NetMode.Client || !NetworkClient.active || NetworkServer.active || BombermanNetworkManager.IsCoopStage ||
            !isActiveAndEnabled || view.scene != gameObject.scene.name || !IsSupportedScene(view.scene) || view.path != NetworkCartPath ||
            !NetworkInputHistory.IsNewer(view.revision, receivedCartRevision)) return;
        receivedCartRevision = view.revision;
        if (NetworkClient.spawned.TryGetValue(view.rider, out var identity) && identity.TryGetComponent<NetworkTransformReliable>(out var sync))
            cartTimelineOffset = NetworkClient.sendInterval * (sync.sendIntervalMultiplier - 1 + (sync.timelineOffset ? sync.sendIntervalMultiplier : 0));
        view.time += cartTimelineOffset;
        networkCartHistory.Add(view);
        if (networkCartHistory.Count > 64) networkCartHistory.RemoveAt(0);
    }

    public static Vector3 SampleNetworkCartPosition(MinecartView from, MinecartView to, double timeline, float tile)
    {
        double interval = to.time - from.time;
        float t = interval > 0 ? Mathf.Clamp01((float)((timeline - from.time) / interval)) : 1;
        if (interval > 0.25 || Vector3.Distance(from.position, to.position) > Mathf.Max(0.01f, tile * 1.5f)) return t < 1 ? from.position : to.position;
        // Preserve an approved rail corner instead of drawing a diagonal through it.
        Vector3 delta = to.position - from.position;
        if (Mathf.Abs(delta.x) > 0.0001f && Mathf.Abs(delta.y) > 0.0001f && from.direction != to.direction)
        {
            Vector3 corner = Mathf.Abs(from.direction.x) > Mathf.Abs(from.direction.y) ?
                new Vector3(to.position.x, from.position.y, from.position.z) : new Vector3(from.position.x, to.position.y, from.position.z);
            float first = Vector3.Distance(from.position, corner), second = Vector3.Distance(corner, to.position);
            float travel = (first + second) * t;
            return travel <= first ? Vector3.Lerp(from.position, corner, first > 0 ? travel / first : 1) :
                Vector3.Lerp(corner, to.position, second > 0 ? (travel - first) / second : 1);
        }
        return Vector3.Lerp(from.position, to.position, t);
    }

    void PresentNetworkCart(double timeline)
    {
        if (NetSync.Mode != NetSync.NetMode.Client || !NetworkClient.active || NetworkServer.active || networkCartHistory.Count == 0 || GamePauseController.IsPaused) return;
        while (networkCartHistory.Count > 1 && networkCartHistory[1].time <= timeline) networkCartHistory.RemoveAt(0);
        var sample = networkCartHistory[0];
        float tileSize = groundTilemap != null ? Mathf.Max(0.0001f, Vector2.Distance(GetCellCenter(Vector3Int.zero), GetCellCenter(Vector3Int.right))) : 1f;
        transform.position = networkCartHistory.Count > 1 ? SampleNetworkCartPosition(sample, networkCartHistory[1], timeline, tileSize) : sample.position;
        EnsureCartVisuals();
        var target = CartRenderer(sample.renderer);
        ForceOnlyCartNoEnsure(target);
        if (target != null)
        {
            target.SetManualAnimationUpdate(true); target.idle = sample.frame.idle; target.CurrentFrame = sample.frame.frame;
            target.RefreshFrame();
            Vector3 offset = sample.frame.localPosition, scale = sample.scale;
            if (networkCartHistory.Count > 1)
            {
                var next = networkCartHistory[1]; double interval = next.time - sample.time;
                if (next.renderer == sample.renderer && next.frame.visible && interval > 0 && interval <= 0.25)
                {
                    float t = Mathf.Clamp01((float)((timeline - sample.time) / interval));
                    offset = Vector3.Lerp(offset, next.frame.localPosition, t); scale = Vector3.Lerp(scale, next.scale, t);
                }
            }
            target.transform.localScale = scale;
            target.transform.position = transform.TransformPoint(offset);
            if (target.TryGetComponent<SpriteRenderer>(out var sprite))
            { sprite.enabled = sample.frame.visible; sprite.flipX = sample.frame.flipX; sprite.flipY = sample.frame.flipY; sprite.color = sample.frame.color; }
        }
        if (sample.loop != networkRideLoop)
        {
            networkRideLoop = sample.loop;
            if (networkRideLoop) PlayRideLoopSfx(); else StopRideLoopSfx();
        }
        SyncRideLoopSfxWithPause();
    }

    void ClearNetworkCart()
    {
        networkCartHistory.Clear(); receivedCartRevision = 0;
        networkRideLoop = false; cartTimelineOffset = 0; receivedStarRevision = lastStarClearRevision = 0;
    }
}

using System.Collections.Generic;
using Assets.Scripts.Netcode;
using Mirror;
using UnityEngine;

namespace Assets.Scripts.Netcode
{
    public struct PortalStarView : NetworkMessage
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

    public enum MinecartPhase : byte { Idle, Enter, Rail, Jump, PortalSink, PortalHidden, PortalRise, Exit }

    public struct MinecartView : NetworkMessage
    {
        public string scene, path;
        public uint revision, rider, generation;
        public MinecartPhase phase;
        public int segment, carrier;
        public float progress, speed;
        public Vector3 riderOffset, carrierOffset;
        public double time;
        public Vector3 position, scale;
        public Vector2 direction;
        public byte renderer;
        public NetworkWorldMount.MountFrame frame;
        public bool loop, paused;
    }
}

public sealed partial class BattleMode9MinecartController
{
    uint networkStarRevision, receivedStarRevision, networkRideGeneration;
    MinecartPhase networkRidePhase;
    int networkRideSegment;
    float networkRideProgress, networkRideSpeed;
    NetworkPlayerInput presentedCartRider;
    float nextCartTrace;
    double cartPlaybackTime, cartPlaybackLastWall;
    bool cartPlaybackActive;
    int cartPlaybackFrame = -1;
    public double RiderPresentationTime => GetCartPresentationTime(BombermanNetworkManager.IsCoopStage ? BombermanNetworkManager.CoopPresentationTime : NetworkTime.time) +
        (BombermanNetworkManager.IsCoopStage ? cartTimelineOffset : 0);

    public double GetCartPresentationTime(double timeline)
    {
        if (cartPlaybackFrame == Time.frameCount) return cartPlaybackActive ? cartPlaybackTime : timeline;
        cartPlaybackFrame = Time.frameCount;
        double now = Time.realtimeSinceStartupAsDouble;
        double result = AdvanceCartPresentationTime(timeline, (float)(now - cartPlaybackLastWall));
        cartPlaybackLastWall = now; return result;
    }
    public double AdvanceCartPresentationTime(double timeline, float delta)
    {
        if (!cartPlaybackActive)
        {
            foreach (var view in networkCartHistory)
                if (view.phase != MinecartPhase.Idle && view.time <= timeline)
                { cartPlaybackTime = view.time; cartPlaybackActive = true; return cartPlaybackTime; }
            return timeline;
        }
        if (networkCartHistory.Count > 0) timeline = System.Math.Min(timeline, networkCartHistory[^1].time);
        if (!GamePauseController.IsPaused)
        {
            var current = networkCartHistory.Count > 0 ? networkCartHistory[0] : default;
            foreach (var view in networkCartHistory)
                if (view.time <= cartPlaybackTime) current = view; else break;
            float limit = current.phase is MinecartPhase.Rail or MinecartPhase.Jump or MinecartPhase.PortalHidden ? .1f : .05f;
            double target = System.Math.Max(cartPlaybackTime, System.Math.Min(timeline, cartPlaybackTime + Mathf.Clamp(delta, 0, limit)));
            // Retain each approved phase boundary even when rail playback catches up.
            foreach (var view in networkCartHistory)
                if (view.time > cartPlaybackTime && view.time <= target &&
                    (view.phase != current.phase || view.generation != current.generation || view.rider != current.rider))
                { target = view.time; break; }
            cartPlaybackTime = target;
        }
        if (networkCartHistory.Count > 0) cartPlaybackTime = System.Math.Max(cartPlaybackTime, networkCartHistory[0].time);
        if (!GamePauseController.IsPaused)
            for (int i = 0; i < networkCartHistory.Count; i++)
            {
                var paused = networkCartHistory[i];
                if (!paused.paused || paused.time > cartPlaybackTime) continue;
                int end = i + 1;
                while (end < networkCartHistory.Count && networkCartHistory[end].generation == paused.generation && networkCartHistory[end].paused) end++;
                if (end < networkCartHistory.Count && networkCartHistory[end].generation == paused.generation && networkCartHistory[end].time <= timeline)
                { cartPlaybackTime = System.Math.Max(cartPlaybackTime, networkCartHistory[end].time); i = end - 1; }
            }
        return cartPlaybackTime;
    }
    bool hasPresentedRiderPosition;
    Vector3 presentedRiderPosition;

    void PublishNetworkStar(Transform star, Sprite sprite, Vector2 drift, float spin, bool clear = false)
    {
        if (!NetSync.IsServer || !NetworkServer.active || NetworkServer.isLoadingScene || !IsSupportedSceneActive()) return;
        NetworkServer.SendToReady(new PortalStarView { scene = gameObject.scene.name, path = NetworkCartPath,
            revision = ++networkStarRevision, time = NetworkTime.localTime, clear = clear,
            position = star != null ? star.position : Vector3.zero, scale = star != null ? star.localScale : Vector3.one,
            angle = star != null ? star.eulerAngles.z : 0, drift = drift, spin = spin,
            life = teleportStarLifetime, frameSeconds = teleportStarAnimationFrameTime,
            sprite = teleportStarSprites != null ? System.Array.IndexOf(teleportStarSprites, sprite) : -1, sorting = teleportStarSortingOrder });
    }

    public void ReceiveNetworkStar(PortalStarView view)
    {
        if (NetSync.Mode != NetSync.NetMode.Client || !NetworkClient.active || NetworkServer.active ||
            !isActiveAndEnabled || view.scene != gameObject.scene.name || !IsSupportedScene(view.scene) || view.path != NetworkCartPath ||
            !NetworkInputHistory.IsNewer(view.revision, receivedStarRevision)) return;
        receivedStarRevision = view.revision;
        if (view.clear) { lastStarClearRevision = view.revision; ClearActiveTeleportStars(); return; }
        if ((float)(NetworkBombFx.KickServerNow - view.time) > Mathf.Max(1, view.life + 0.5f)) return;
        StartCoroutine(PresentNetworkStar(view));
    }

    System.Collections.IEnumerator PresentNetworkStar(PortalStarView view)
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

    public GameObject CreateNetworkStarView(PortalStarView view, float age)
    {
        if (NetSync.Mode != NetSync.NetMode.Client || !NetworkClient.active || NetworkServer.active || view.clear ||
            view.scene != gameObject.scene.name || view.path != NetworkCartPath || teleportStarSprites == null ||
            view.sprite < 0 || view.sprite >= teleportStarSprites.Length || teleportStarSprites[view.sprite] == null) return null;
        var star = new GameObject("ApprovedPortalStarView"); star.transform.SetParent(transform, worldPositionStays: true);
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

    void PublishNetworkCart(bool hidden = false, bool force = false)
    {
        if (!NetSync.IsServer || !NetworkServer.active || NetworkServer.isLoadingScene || !IsSupportedSceneActive()) return;
        if (!hidden && !force && Time.unscaledTime < nextCartSample) return;
        nextCartSample = Time.unscaledTime + 0.05f;
        var view = new MinecartView { scene = gameObject.scene.name, path = NetworkCartPath,
            position = transform.position, scale = Vector3.one, direction = currentCartDirection, renderer = 255,
            generation = networkRideGeneration, phase = networkRidePhase, segment = networkRideSegment, paused = GamePauseController.IsPaused,
            progress = networkRideProgress, speed = networkRideSpeed,
            riderOffset = currentRider != null ? currentRider.transform.position - transform.position : Vector3.zero,
            rider = currentRider != null && currentRider.TryGetComponent<NetworkIdentity>(out var rider) ? rider.netId : 0,
            loop = !hidden && audioSource != null && audioSource.loop && audioSource.clip == rideLoopSfx && !rideLoopPausedForJump };
        if (!hidden)
            for (byte i = 0; i < 4; i++)
            {
                var animation = CartRenderer(i);
                if (animation == null || !animation.TryGetComponent<SpriteRenderer>(out var sprite) || !sprite.enabled || !sprite.gameObject.activeInHierarchy) continue;
                view.carrier = sprite.GetEntityId().GetHashCode();
                view.carrierOffset = currentRider != null ? currentRider.transform.position - sprite.transform.position : Vector3.zero;
                view.renderer = i; view.scale = animation.transform.localScale;
                view.frame = new NetworkWorldMount.MountFrame { visible = true, frame = animation.CurrentFrame, idle = animation.idle,
                    flipX = sprite.flipX, flipY = sprite.flipY, color = sprite.color,
                    localPosition = transform.InverseTransformPoint(animation.transform.position) };
                break;
            }
        bool changed = view.paused != lastCartSent.paused || view.generation != lastCartSent.generation || view.phase != lastCartSent.phase || view.segment != lastCartSent.segment ||
            view.progress != lastCartSent.progress || view.speed != lastCartSent.speed || view.riderOffset != lastCartSent.riderOffset ||
            view.carrier != lastCartSent.carrier || view.carrierOffset != lastCartSent.carrierOffset || view.position != lastCartSent.position || view.scale != lastCartSent.scale || view.direction != lastCartSent.direction || view.renderer != lastCartSent.renderer ||
            view.rider != lastCartSent.rider || view.loop != lastCartSent.loop || !view.frame.Equals(lastCartSent.frame);
        if (!hidden && !force && !changed && Time.unscaledTime < nextCartHeartbeat) return;
        nextCartHeartbeat = Time.unscaledTime + 0.5f;
        view.time = NetworkTime.localTime; view.revision = ++networkCartRevision;
        lastCartSent = view; NetworkServer.SendToReady(view);
    }

    public void ReceiveNetworkCart(MinecartView view)
    {
        if (NetSync.Mode != NetSync.NetMode.Client || !NetworkClient.active || NetworkServer.active ||
            !isActiveAndEnabled || !ValidCartView(view) || view.scene != gameObject.scene.name || !IsSupportedScene(view.scene) || view.path != NetworkCartPath ||
            !NetworkInputHistory.IsNewer(view.revision, receivedCartRevision)) return;
        receivedCartRevision = view.revision;
        if (NetworkClient.spawned.TryGetValue(view.rider, out var identity) && identity.TryGetComponent<NetworkTransformReliable>(out var sync))
            cartTimelineOffset = NetworkClient.sendInterval * (sync.sendIntervalMultiplier - 1 + (sync.timelineOffset ? sync.sendIntervalMultiplier : 0));
        if (!BombermanNetworkManager.IsCoopStage) view.time += cartTimelineOffset;
        if (BombermanNetworkManager.IsCoopStage && view.carrier != 0) BombermanNetworkManager.RegisterCoopCartCarrier(view.carrier, this);
        networkCartHistory.Add(view);
        if (networkCartHistory.Count > 64) networkCartHistory.RemoveAt(0);
        // Claim the clock before animation/reconciliation can prune the short
        // boarding prefix; applying its root still belongs to the presenter.
        if (presentedCartRider == null && view.phase != MinecartPhase.Idle &&
            NetworkClient.spawned.TryGetValue(view.rider, out var actor) && actor.TryGetComponent<NetworkPlayerInput>(out var input) &&
            input.ClaimApprovedCart(this)) presentedCartRider = input;
    }

    public static bool ValidCartView(MinecartView view) => double.IsFinite(view.time) && (byte)view.phase <= (byte)MinecartPhase.Exit &&
        FiniteCartVector(view.position) && FiniteCartVector(view.riderOffset) && FiniteCartVector(view.carrierOffset) &&
        float.IsFinite(view.progress) && view.progress >= 0 && view.progress <= 1.0001f && float.IsFinite(view.speed) && view.speed >= 0;
    static bool FiniteCartVector(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);

    public static Vector3 SampleNetworkCartPosition(MinecartView from, MinecartView to, double timeline, float tile)
    {
        double interval = to.time - from.time;
        float t = interval > 0 ? Mathf.Clamp01((float)((timeline - from.time) / interval)) : 1;
        if (from.generation != to.generation || from.rider != to.rider || from.phase != to.phase || interval > 0.25 || Vector3.Distance(from.position, to.position) > Mathf.Max(0.01f, tile * 1.5f)) return t < 1 ? from.position : to.position;
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
        if (NetSync.Mode != NetSync.NetMode.Client || !NetworkClient.active || NetworkServer.active || networkCartHistory.Count == 0) return;
        if (GamePauseController.IsPaused)
        {
            if (presentedCartRider != null && hasPresentedRiderPosition) presentedCartRider.PresentApprovedCart(this, presentedRiderPosition);
            return;
        }
        while (networkCartHistory.Count > 1 && networkCartHistory[1].time <= timeline) networkCartHistory.RemoveAt(0);
        var sample = networkCartHistory[0];
        if (sample.time > timeline) return;
        PresentApprovedRider(sample, timeline);
        if (sample.phase == MinecartPhase.Idle) cartPlaybackActive = false;
        if (BombermanNetworkManager.IsCoopStage)
        {
            PresentCartLoop(sample); return; // Existing co-op sprites own the vehicle body.
        }
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
        PresentCartLoop(sample);
    }

    void PresentCartLoop(MinecartView sample)
    {
        if (sample.loop != networkRideLoop)
        {
            networkRideLoop = sample.loop;
            if (networkRideLoop) PlayRideLoopSfx(); else StopRideLoopSfx();
        }
        SyncRideLoopSfxWithPause();
    }

    void PresentApprovedRider(MinecartView sample, double timeline)
    {
        NetworkPlayerInput rider = null;
        if (sample.rider != 0 && sample.phase != MinecartPhase.Idle && NetworkClient.spawned.TryGetValue(sample.rider, out var identity))
            rider = identity.GetComponent<NetworkPlayerInput>();
        if (presentedCartRider != rider)
        {
            if (presentedCartRider != null)
            {
                if (GameplayLatencyDiagnostics.Enabled) GameplayLatencyDiagnostics.Write(GameplayLatencyDiagnostics.For("minecart_end", presentedCartRider.GetComponent<MovementController>(), detail: "cart=" + NetworkCartPath));
                presentedCartRider.ReleaseApprovedCart(this);
            }
            presentedCartRider = rider; hasPresentedRiderPosition = false;
        }
        if (rider == null) return;
        if (sample.phase == MinecartPhase.PortalHidden)
        {
            rider.PresentApprovedCart(this, hasPresentedRiderPosition ? presentedRiderPosition : sample.position);
            return;
        }
        Vector3 position, offset = sample.riderOffset, visualOffset = sample.frame.localPosition, carrierOffset = sample.carrierOffset;
        if (networkCartHistory.Count > 1)
        {
            var next = networkCartHistory[1]; double interval = next.time - sample.time;
            if (next.generation == sample.generation && next.phase == sample.phase && next.rider == sample.rider && interval > 0 && interval <= .25)
            {
                float t = Mathf.Clamp01((float)((timeline - sample.time) / interval));
                offset = Vector3.Lerp(offset, next.riderOffset, t);
                if (sample.carrier == next.carrier)
                {
                    visualOffset = Vector3.Lerp(visualOffset, next.frame.localPosition, t);
                    carrierOffset = Vector3.Lerp(carrierOffset, next.carrierOffset, t);
                }
            }
        }
        if (BombermanNetworkManager.IsCoopStage)
        {
            var ground = networkCartHistory.Count > 1 ? SampleNetworkCartPosition(sample, networkCartHistory[1], timeline, 1) : sample.position;
            if (!BombermanNetworkManager.TryPresentCoopCarrier(sample.carrier, ground + visualOffset, out var carrier)) return;
            position = carrier + carrierOffset;
        }
        else position = (networkCartHistory.Count > 1 ? SampleNetworkCartPosition(sample, networkCartHistory[1], timeline, 1) : sample.position) + offset;
        if (!rider.PresentApprovedCart(this, position)) return;
        presentedRiderPosition = position; hasPresentedRiderPosition = true;
        if (GameplayLatencyDiagnostics.Enabled && Time.unscaledTime >= nextCartTrace)
        {
            nextCartTrace = Time.unscaledTime + .05f;
            var entry = GameplayLatencyDiagnostics.For("minecart_present", rider.GetComponent<MovementController>(),
                detail: "cart=" + NetworkCartPath + ";generation=" + sample.generation + ";phase=" + sample.phase + ";segment=" + sample.segment);
            entry.delta = position - (Vector3)sample.position; GameplayLatencyDiagnostics.Write(entry);
        }
    }

    void ClearNetworkCart()
    {
        if (presentedCartRider != null) presentedCartRider.ReleaseApprovedCart(this);
        presentedCartRider = null; hasPresentedRiderPosition = false;
        networkCartHistory.Clear(); receivedCartRevision = 0;
        cartPlaybackActive = false; cartPlaybackFrame = -1; cartPlaybackLastWall = 0;
        networkRideLoop = false; cartTimelineOffset = 0; receivedStarRevision = lastStarClearRevision = 0;
    }
}

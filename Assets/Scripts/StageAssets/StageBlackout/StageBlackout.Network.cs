using System;
using System.Collections.Generic;
using Assets.Scripts.Netcode;
using Mirror;
using UnityEngine;

namespace Assets.Scripts.Netcode
{
    public struct BlackoutSpotlight : IEquatable<BlackoutSpotlight>
    {
        public int id;
        public Vector2 position, halfSize;
        public float intensity;
        public bool Equals(BlackoutSpotlight other) => id == other.id && position == other.position && halfSize == other.halfSize && intensity == other.intensity;
    }

    public struct BlackoutView : NetworkMessage
    {
        public string scene, path;
        public uint revision;
        public double time;
        public bool active, world, roomActive;
        public float alpha, tileSize, extraTiles, softness;
        public Vector3 vision;
        public BlackoutSpotlight[] explosions;
        public Vector4[] roomLights;
        public bool Matches(BlackoutView other)
        {
            if (active != other.active || world != other.world || roomActive != other.roomActive || alpha != other.alpha || tileSize != other.tileSize ||
                extraTiles != other.extraTiles || softness != other.softness || vision != other.vision ||
                (explosions?.Length ?? 0) != (other.explosions?.Length ?? 0) ||
                (roomLights?.Length ?? 0) != (other.roomLights?.Length ?? 0)) return false;
            for (int i = 0; i < (explosions?.Length ?? 0); i++) if (!explosions[i].Equals(other.explosions[i])) return false;
            for (int i = 0; i < (roomLights?.Length ?? 0); i++) if (roomLights[i] != other.roomLights[i]) return false;
            return true;
        }
    }
}

public sealed partial class StageBlackout
{
    readonly List<BlackoutView> networkBlackoutHistory = new(64);
    readonly List<BlackoutSpotlight> networkSpotlightBuffer = new(36);
    BlackoutView lastNetworkBlackout;
    uint networkBlackoutRevision, receivedNetworkBlackoutRevision, appliedNetworkBlackoutRevision;
    float nextNetworkBlackoutSample, nextNetworkBlackoutHeartbeat;

    public string NetworkBlackoutPath
    {
        get
        {
            string path = name;
            for (var parent = transform.parent; parent != null; parent = parent.parent) path = parent.name + "/" + path;
            return path;
        }
    }

    public BlackoutView CaptureNetworkBlackout()
    {
        networkSpotlightBuffer.Clear();
        if (_active)
            foreach (var pair in _activeExplosionSpotlights)
            {
                if (pair.Value.Intensity <= 0) continue;
                networkSpotlightBuffer.Add(new BlackoutSpotlight { id = pair.Key, position = pair.Value.Transform != null
                    ? (Vector2)pair.Value.Transform.position : pair.Value.LastKnownWorldPosition,
                    halfSize = pair.Value.HalfSizeInTiles, intensity = pair.Value.Intensity });
                if (networkSpotlightBuffer.Count == Mathf.Clamp(maxExplosionSpotlights, 1, ShaderMaxSpotlights)) break;
            }
        return new BlackoutView { scene = gameObject.scene.name, path = NetworkBlackoutPath, time = NetworkTime.localTime,
            active = _active && (worldOverlay != null ? worldOverlay.IsVisible : blackoutImage != null && blackoutImage.isActiveAndEnabled),
            world = worldOverlay != null, roomActive = worldOverlay != null && worldOverlay.NetworkRoomActive,
            alpha = Mathf.Clamp01(_currentA), tileSize = tileWorldSize,
            extraTiles = extraTilesAroundExplosion, softness = explosionSpotlightSoftness,
            vision = worldOverlay != null ? worldOverlay.NetworkVision : Vector3.zero,
            explosions = networkSpotlightBuffer.ToArray(), roomLights = _active && worldOverlay != null
                ? worldOverlay.CaptureNetworkRoomLights() : Array.Empty<Vector4>() };
    }

    public void PublishNetworkBlackout()
    {
        if (!NetSync.IsServer || !NetworkServer.active || !BombermanNetworkManager.IsCoopStage || NetworkServer.isLoadingScene ||
            Time.unscaledTime < nextNetworkBlackoutSample) return;
        nextNetworkBlackoutSample = Time.unscaledTime + .05f;
        var view = CaptureNetworkBlackout();
        if (lastNetworkBlackout.path != null && view.Matches(lastNetworkBlackout) && Time.unscaledTime < nextNetworkBlackoutHeartbeat) return;
        nextNetworkBlackoutHeartbeat = Time.unscaledTime + .5f;
        view.revision = ++networkBlackoutRevision; lastNetworkBlackout = view;
        NetworkServer.SendToReady(view);
    }

    void PublishNetworkBlackoutClear()
    {
        if (!NetSync.IsServer || !NetworkServer.active || !BombermanNetworkManager.IsCoopStage) return;
        var view = lastNetworkBlackout.path != null ? lastNetworkBlackout : CaptureNetworkBlackout();
        view.time = NetworkTime.localTime; view.active = false; view.alpha = 0;
        view.explosions = Array.Empty<BlackoutSpotlight>(); view.roomLights = Array.Empty<Vector4>();
        view.revision = ++networkBlackoutRevision; NetworkServer.SendToReady(view);
    }

    static bool NetworkBlackoutFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    static bool NetworkBlackoutFinite(Vector2 value) => NetworkBlackoutFinite(value.x) && NetworkBlackoutFinite(value.y);
    public void ReceiveNetworkBlackout(BlackoutView view)
    {
        if (NetSync.Mode != NetSync.NetMode.Client || !NetworkClient.active || NetworkServer.active ||
            view.scene != gameObject.scene.name || view.path != NetworkBlackoutPath || view.world != (worldOverlay != null) ||
            view.explosions == null || view.roomLights == null || view.explosions.Length > 36 || view.roomLights.Length > 16 ||
            !NetworkInputHistory.IsNewer(view.revision, receivedNetworkBlackoutRevision) || double.IsNaN(view.time) || double.IsInfinity(view.time) ||
            !NetworkBlackoutFinite(view.alpha) || view.alpha < 0 || view.alpha > 1 || !NetworkBlackoutFinite(view.tileSize) || view.tileSize <= 0 ||
            !NetworkBlackoutFinite(view.extraTiles) || view.extraTiles < 0 || !NetworkBlackoutFinite(view.softness) || view.softness < 0) return;
        if (view.world && (!NetworkBlackoutFinite((Vector2)view.vision) || !NetworkBlackoutFinite(view.vision.z) ||
            view.vision.x <= 0 || view.vision.y < 0 || view.vision.z <= 0)) return;
        for (int i = 0; i < view.explosions.Length; i++)
        {
            var light = view.explosions[i];
            if (!NetworkBlackoutFinite(light.position) || !NetworkBlackoutFinite(light.halfSize) || light.halfSize.x <= 0 || light.halfSize.y <= 0 ||
                !NetworkBlackoutFinite(light.intensity) || light.intensity < 0 || light.intensity > 1) return;
            for (int j = 0; j < i; j++) if (view.explosions[j].id == light.id) return;
        }
        foreach (var light in view.roomLights)
            if (!NetworkBlackoutFinite(new Vector2(light.x, light.y)) || !NetworkBlackoutFinite(light.z) || light.z <= 0) return;
        receivedNetworkBlackoutRevision = view.revision;
        networkBlackoutHistory.Add(view);
        if (networkBlackoutHistory.Count > 64) networkBlackoutHistory.RemoveAt(0);
    }

    public void PresentNetworkBlackout(double timeline)
    {
        if (NetSync.Mode != NetSync.NetMode.Client || !NetworkClient.active || NetworkServer.active ||
            networkBlackoutHistory.Count == 0 || networkBlackoutHistory[0].time > timeline) return;
        if (!visualInitialized) Awake();
        if (_matInstance == null) return;
        while (networkBlackoutHistory.Count > 1 && networkBlackoutHistory[1].time <= timeline) networkBlackoutHistory.RemoveAt(0);
        var view = networkBlackoutHistory[0]; float alpha = view.alpha;
        if (networkBlackoutHistory.Count > 1)
        {
            var next = networkBlackoutHistory[1]; double interval = next.time - view.time;
            if (view.active == next.active && interval > 0 && interval <= .25)
                alpha = Mathf.Lerp(alpha, next.alpha, Mathf.Clamp01((float)((timeline - view.time) / interval)));
        }
        if (view.revision != appliedNetworkBlackoutRevision)
        {
            _activeExplosionSpotlights.Clear();
            if (view.active)
                foreach (var light in view.explosions)
                    _activeExplosionSpotlights[light.id] = new ExplosionSpotlightData { LastKnownWorldPosition = light.position,
                        HalfSizeInTiles = light.halfSize, Intensity = light.intensity };
            tileWorldSize = view.tileSize; extraTilesAroundExplosion = view.extraTiles; explosionSpotlightSoftness = view.softness;
            worldOverlay?.ApplyNetworkRoomLights(view.roomLights, view.vision, view.roomActive);
            appliedNetworkBlackoutRevision = view.revision;
        }
        // Native blackout fading uses unscaled time, including during a gameplay
        // pause. Sample the approved clock rather than starting a local timer.
        _active = view.active; _currentA = _targetA = alpha;
        SetOutputVisible(_active); ApplyFullBlackout(alpha); FlushSpotlightsToShader(); _spotlightsDirty = false;
    }
}

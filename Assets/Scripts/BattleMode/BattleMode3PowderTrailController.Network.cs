using System.Collections.Generic;
using Assets.Scripts.Netcode;
using Mirror;
using UnityEngine;

namespace Assets.Scripts.Netcode
{
    public struct PowderTrailFrame : NetworkMessage
    {
        public string scene, path;
        public uint revision;
        public double time;
        public int frame; // -1 is the approved end; no client hitboxes or ignition.
    }
}

public sealed partial class BattleMode3PowderTrailController
{
    readonly List<PowderTrailFrame> networkPowderHistory = new(32);
    uint networkPowderRevision, receivedNetworkPowderRevision, appliedNetworkPowderRevision;
    int networkPowderFrame = -1, presentedNetworkPowderFrame = -1;
    float nextNetworkPowderHeartbeat;
    bool networkPowderVisible;
    string networkPowderPath;

    public string NetworkPowderPath
    {
        get
        {
            if (networkPowderPath != null) return networkPowderPath;
            networkPowderPath = name;
            for (var parent = transform.parent; parent != null; parent = parent.parent) networkPowderPath = parent.name + "/" + networkPowderPath;
            return networkPowderPath;
        }
    }

    void Update()
    {
        if (NetSync.Mode == NetSync.NetMode.Client) PresentNetworkPowder();
        else PublishNetworkPowder();
    }

    void ReportNetworkPowderFrame(int frame)
    {
        networkPowderFrame = frame;
        PublishNetworkPowder(force: true);
    }

    void PublishNetworkPowder(bool force = false)
    {
        if (!NetSync.IsServer || !NetworkServer.active || NetworkServer.isLoadingScene ||
            gameObject.scene.name != "BattleMode_3" || (!force && Time.unscaledTime < nextNetworkPowderHeartbeat)) return;
        nextNetworkPowderHeartbeat = Time.unscaledTime + 0.5f;
        NetworkServer.SendToReady(new PowderTrailFrame { scene = gameObject.scene.name, path = NetworkPowderPath,
            revision = ++networkPowderRevision, time = NetworkTime.localTime, frame = networkPowderFrame });
    }

    public void ReceiveNetworkPowder(PowderTrailFrame state)
    {
        if (NetSync.Mode != NetSync.NetMode.Client || !NetworkClient.active || NetworkServer.active ||
            state.scene != gameObject.scene.name || state.path != NetworkPowderPath || state.frame < -1 ||
            double.IsNaN(state.time) || double.IsInfinity(state.time) ||
            !NetworkInputHistory.IsNewer(state.revision, receivedNetworkPowderRevision)) return;
        BuildExplosionTiles();
        if (state.frame >= GetMaxExplosionFrameCount()) return;
        receivedNetworkPowderRevision = state.revision;
        if (NetworkClient.localPlayer != null && NetworkClient.localPlayer.TryGetComponent<NetworkTransformReliable>(out var sync))
            state.time += NetworkClient.sendInterval * (sync.sendIntervalMultiplier - 1 + (sync.timelineOffset ? sync.sendIntervalMultiplier : 0));
        networkPowderHistory.Add(state);
        if (networkPowderHistory.Count > 32) networkPowderHistory.RemoveAt(0);
    }

    void PresentNetworkPowder()
    {
        if (NetSync.Mode != NetSync.NetMode.Client || !NetworkClient.active || NetworkServer.active || GamePauseController.IsPaused ||
            networkPowderHistory.Count == 0 || networkPowderHistory[0].time > NetworkTime.time) return;
        while (networkPowderHistory.Count > 1 && networkPowderHistory[1].time <= NetworkTime.time) networkPowderHistory.RemoveAt(0);
        var state = networkPowderHistory[0];
        if (state.revision == appliedNetworkPowderRevision) return;
        ResolveReferences();
        if (groundTilemap == null) return;
        if (state.frame < 0)
        {
            if (networkPowderVisible) RestoreOriginalTiles();
            originalTiles.Clear(); originalTransforms.Clear(); networkPowderVisible = false;
        }
        else
        {
            if (!networkPowderVisible) { BuildTrailCells(); CacheOriginalTiles(captureStableState: false); }
            if (!networkPowderVisible || presentedNetworkPowderFrame != state.frame) ApplyExplosionTileFrame(state.frame);
            networkPowderVisible = true;
        }
        presentedNetworkPowderFrame = state.frame;
        appliedNetworkPowderRevision = state.revision;
    }

    void ClearNetworkPowderHistory()
    {
        networkPowderHistory.Clear(); networkPowderVisible = false;
        presentedNetworkPowderFrame = -1; appliedNetworkPowderRevision = 0;
    }
}

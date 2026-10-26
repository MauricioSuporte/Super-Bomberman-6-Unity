using System.Collections.Generic;
using Assets.Scripts.Netcode;
using Mirror;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Assets.Scripts.Netcode
{
    public struct MagnetFieldPhase : NetworkMessage
    {
        public string scene, path;
        public uint revision;
        public double time;
        public Vector3Int[] cells;
        public byte[] directions;
    }
}

public sealed partial class MagnetIndestructibleTileHandler
{
    readonly List<MagnetFieldPhase> networkFieldHistory = new(32);
    readonly List<Vector3Int> networkFieldCells = new();
    readonly List<byte> networkFieldDirections = new();
    MagnetFieldPhase lastNetworkField;
    uint networkFieldRevision, receivedNetworkFieldRevision;
    float nextNetworkFieldSample, nextNetworkFieldHeartbeat;
    bool networkFieldReady;
    string networkFieldPath;
    public string NetworkFieldPath
    {
        get
        {
            if (networkFieldPath != null) return networkFieldPath;
            networkFieldPath = name;
            for (var parent = transform.parent; parent != null; parent = parent.parent) networkFieldPath = parent.name + "/" + networkFieldPath;
            return networkFieldPath;
        }
    }

    int DirectionIndex(TileBase tile) => tile == magnetUp ? 1 : tile == magnetRight ? 2 : tile == magnetDown ? 3 : 0;

    void Update()
    {
        if (NetSync.Mode == NetSync.NetMode.Client) PresentNetworkField();
        else PublishNetworkField();
    }

    void PublishNetworkField(bool force = false)
    {
        if (!NetSync.IsServer || !NetworkServer.active || NetworkServer.isLoadingScene ||
            BombermanNetworkManager.IsCoopStage || indestructibleTilemap == null) return;
        if (!force && Time.unscaledTime < nextNetworkFieldSample) return;
        nextNetworkFieldSample = Time.unscaledTime + 0.1f;
        networkFieldCells.Clear(); networkFieldDirections.Clear();
        foreach (var cell in _magnetCells)
        {
            var tile = indestructibleTilemap.GetTile(cell);
            if (!IsAnyMagnetTile(tile)) continue;
            networkFieldCells.Add(cell); networkFieldDirections.Add((byte)DirectionIndex(tile));
            if (networkFieldCells.Count == 256) break;
        }
        bool changed = lastNetworkField.cells == null || lastNetworkField.cells.Length != networkFieldCells.Count;
        if (!changed)
            for (int i = 0; i < networkFieldCells.Count; i++)
                if (lastNetworkField.cells[i] != networkFieldCells[i] || lastNetworkField.directions[i] != networkFieldDirections[i]) { changed = true; break; }
        if (!force && !changed && Time.unscaledTime < nextNetworkFieldHeartbeat) return;
        nextNetworkFieldHeartbeat = Time.unscaledTime + 0.5f;
        lastNetworkField = new MagnetFieldPhase { scene = gameObject.scene.name, path = NetworkFieldPath,
            revision = ++networkFieldRevision, time = NetworkTime.localTime,
            cells = networkFieldCells.ToArray(), directions = networkFieldDirections.ToArray() };
        NetworkServer.SendToReady(lastNetworkField);
    }

    public void ReceiveNetworkField(MagnetFieldPhase phase)
    {
        if (NetSync.Mode != NetSync.NetMode.Client || !NetworkClient.active || NetworkServer.active ||
            BombermanNetworkManager.IsCoopStage || phase.scene != gameObject.scene.name || phase.path != NetworkFieldPath ||
            phase.cells == null || phase.directions == null || phase.cells.Length != phase.directions.Length || phase.cells.Length > 256 ||
            !NetworkInputHistory.IsNewer(phase.revision, receivedNetworkFieldRevision)) return;
        for (int i = 0; i < phase.directions.Length; i++) if (phase.directions[i] > 3) return;
        receivedNetworkFieldRevision = phase.revision;
        if (NetworkClient.localPlayer != null && NetworkClient.localPlayer.TryGetComponent<NetworkTransformReliable>(out var sync))
            phase.time += NetworkClient.sendInterval * (sync.sendIntervalMultiplier - 1 + (sync.timelineOffset ? sync.sendIntervalMultiplier : 0));
        networkFieldHistory.Add(phase);
        if (networkFieldHistory.Count > 32) networkFieldHistory.RemoveAt(0);
    }

    void PresentNetworkField()
    {
        if (NetSync.Mode != NetSync.NetMode.Client || !NetworkClient.active || NetworkServer.active || GamePauseController.IsPaused ||
            indestructibleTilemap == null || networkFieldHistory.Count == 0 || networkFieldHistory[0].time > NetworkTime.time) return;
        while (networkFieldHistory.Count > 1 && networkFieldHistory[1].time <= NetworkTime.time) networkFieldHistory.RemoveAt(0);
        var phase = networkFieldHistory[0];
        _magnetCells.Clear(); _dirIndexByCell.Clear();
        for (int i = 0; i < phase.cells.Length; i++)
        {
            // A confirmed sudden-death replacement must never be changed back
            // into a magnet by an older buffered visual phase.
            var cell = phase.cells[i];
            if (!IsAnyMagnetTile(indestructibleTilemap.GetTile(cell))) continue;
            _magnetCells.Add(cell); _dirIndexByCell[cell] = phase.directions[i];
            var tile = GetTileForIndex(phase.directions[i]);
            if (indestructibleTilemap.GetTile(cell) != tile) ApplyDirectionTile(cell, phase.directions[i]);
        }
        networkFieldReady = true;
    }
}

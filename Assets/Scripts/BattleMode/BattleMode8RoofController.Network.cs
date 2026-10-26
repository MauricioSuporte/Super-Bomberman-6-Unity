using System.Collections.Generic;
using Assets.Scripts.Netcode;
using Mirror;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Assets.Scripts.Netcode
{
    public struct RoofTileState : NetworkMessage
    {
        public string scene, path;
        public uint revision;
        public double time;
        public Vector3Int[] cells;
        // Index into the authored swaps, four tiles per swap; -1 clears a cell.
        public int[] tiles;
    }
}

public sealed partial class BattleMode8RoofController
{
    const int MaxNetworkRoofCells = 512;
    readonly HashSet<Vector3Int> networkRoofCells = new();
    readonly List<RoofTileState> networkRoofHistory = new(32);
    readonly List<Vector3Int> networkRoofCellBuffer = new();
    readonly List<int> networkRoofTileBuffer = new();
    uint networkRoofRevision, receivedNetworkRoofRevision, appliedNetworkRoofRevision;
    float nextNetworkRoofHeartbeat;
    string networkRoofPath;

    public string NetworkRoofPath
    {
        get
        {
            if (networkRoofPath != null) return networkRoofPath;
            networkRoofPath = name;
            for (var parent = transform.parent; parent != null; parent = parent.parent) networkRoofPath = parent.name + "/" + networkRoofPath;
            return networkRoofPath;
        }
    }

    TileBase NetworkRoofTile(int index)
    {
        if (index < 0 || roofTiles == null || index / 4 >= roofTiles.Length) return null;
        var swap = roofTiles[index / 4];
        return (index % 4) switch { 0 => swap.closedTile, 1 => swap.firstTransitionTile, 2 => swap.secondTransitionTile, _ => swap.openTile };
    }

    int NetworkRoofTileIndex(TileBase tile)
    {
        if (tile == null || roofTiles == null) return -1;
        for (int i = 0; i < roofTiles.Length * 4; i++) if (NetworkRoofTile(i) == tile) return i;
        return -2;
    }

    void PublishNetworkRoof(bool force = false)
    {
        if (!NetSync.IsServer || !NetworkServer.active || NetworkServer.isLoadingScene || roofTilemap == null ||
            !IsBattleMode8Active() || (!force && Time.unscaledTime < nextNetworkRoofHeartbeat)) return;
        if (!CaptureNetworkRoofTiles()) return;
        nextNetworkRoofHeartbeat = Time.unscaledTime + 0.5f;
        NetworkServer.SendToReady(new RoofTileState { scene = gameObject.scene.name, path = NetworkRoofPath,
            revision = ++networkRoofRevision, time = NetworkTime.localTime,
            cells = networkRoofCellBuffer.ToArray(), tiles = networkRoofTileBuffer.ToArray() });
    }

    bool CaptureNetworkRoofTiles()
    {
        if (roofTilemap == null) return false;
        foreach (var cell in roofTilemap.cellBounds.allPositionsWithin)
            if (NetworkRoofTileIndex(roofTilemap.GetTile(cell)) >= 0) networkRoofCells.Add(cell);
        // An open tile may be null; include remembered cells so late clients clear it.
        foreach (var cell in closedTileByClearedCell.Keys) networkRoofCells.Add(cell);
        if (networkRoofCells.Count > MaxNetworkRoofCells) return false;
        networkRoofCellBuffer.Clear(); networkRoofTileBuffer.Clear();
        foreach (var cell in networkRoofCells)
        {
            int index = NetworkRoofTileIndex(roofTilemap.GetTile(cell));
            if (index < -1) continue; // Never replace tiles outside this controller's catalog.
            networkRoofCellBuffer.Add(cell); networkRoofTileBuffer.Add(index);
        }
        return true;
    }

    public void ReceiveNetworkRoof(RoofTileState state)
    {
        if (NetSync.Mode != NetSync.NetMode.Client || !NetworkClient.active || NetworkServer.active ||
            state.scene != gameObject.scene.name || state.path != NetworkRoofPath ||
            state.cells == null || state.tiles == null || state.cells.Length != state.tiles.Length || state.cells.Length > MaxNetworkRoofCells ||
            double.IsNaN(state.time) || double.IsInfinity(state.time) ||
            !NetworkInputHistory.IsNewer(state.revision, receivedNetworkRoofRevision)) return;
        for (int i = 0; i < state.tiles.Length; i++)
            if (state.tiles[i] < -1 || (state.tiles[i] >= 0 && NetworkRoofTile(state.tiles[i]) == null)) return;
        receivedNetworkRoofRevision = state.revision;
        if (NetworkClient.localPlayer != null && NetworkClient.localPlayer.TryGetComponent<NetworkTransformReliable>(out var sync))
            state.time += NetworkClient.sendInterval * (sync.sendIntervalMultiplier - 1 + (sync.timelineOffset ? sync.sendIntervalMultiplier : 0));
        networkRoofHistory.Add(state);
        if (networkRoofHistory.Count > 32) networkRoofHistory.RemoveAt(0);
    }

    void PresentNetworkRoof()
    {
        if (NetSync.Mode != NetSync.NetMode.Client || !NetworkClient.active || NetworkServer.active || GamePauseController.IsPaused ||
            networkRoofHistory.Count == 0 || networkRoofHistory[0].time > NetworkTime.time) return;
        while (networkRoofHistory.Count > 1 && networkRoofHistory[1].time <= NetworkTime.time) networkRoofHistory.RemoveAt(0);
        var state = networkRoofHistory[0];
        if (state.revision == appliedNetworkRoofRevision) return;
        ResolveReferences();
        if (roofTilemap == null) return;
        for (int i = 0; i < state.cells.Length; i++)
        {
            var tile = NetworkRoofTile(state.tiles[i]);
            if (roofTilemap.GetTile(state.cells[i]) != tile) roofTilemap.SetTile(state.cells[i], tile);
        }
        appliedNetworkRoofRevision = state.revision;
    }
}

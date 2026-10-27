using System.Collections.Generic;
using Assets.Scripts.Netcode;
using Mirror;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Assets.Scripts.Netcode
{
    public struct RopeTileView : NetworkMessage
    {
        public string scene, path;
        public uint revision;
        public double time;
        public Vector3Int[] cells;
        public int[] entries, frames; // Frame zero is rest, then impulse and preparation.
    }
}

public sealed partial class BattleMode11ImpulseRopeController
{
    const int MaxNetworkRopeCells = 64;
    readonly List<RopeTileView> ropeViewHistory = new(64);
    readonly List<Vector3Int> ropeCellBuffer = new();
    readonly List<int> ropeEntryBuffer = new(), ropeFrameBuffer = new();
    readonly Dictionary<Vector3Int, (TileBase tile, Color color, TileFlags flags)> hiddenRopeCells = new();
    readonly Dictionary<Tile, Tile> ropeViewTiles = new();
    readonly List<Vector3Int> retiredRopeCells = new();
    Tilemap ropeViewMap;
    RopeTileView lastRopeView;
    uint ropeViewRevision, receivedRopeViewRevision, appliedRopeViewRevision;
    float nextRopeViewHeartbeat;

    public string NetworkRopePath
    {
        get
        {
            string path = name;
            for (var parent = transform.parent; parent != null; parent = parent.parent) path = parent.name + "/" + path;
            return path;
        }
    }

    Tile RopeViewTile(int entry, int frame)
    {
        if (ropeTileAnimations == null || entry < 0 || entry >= ropeTileAnimations.Length || frame < 0) return null;
        var definition = ropeTileAnimations[entry];
        if (definition == null) return null;
        if (frame == 0) return definition.tile as Tile;
        int impulseCount = definition.impulseAnimationTiles?.Length ?? 0;
        if (frame <= impulseCount) return definition.impulseAnimationTiles[frame - 1] as Tile;
        int index = frame - impulseCount - 1;
        return definition.preparingAnimationTiles != null && index < definition.preparingAnimationTiles.Length
            ? definition.preparingAnimationTiles[index] as Tile : null;
    }

    bool RopeViewIndex(TileBase tile, out int entry, out int frame)
    {
        entry = frame = -1;
        if (tile == null || ropeTileAnimations == null) return false;
        for (int i = 0; i < ropeTileAnimations.Length; i++)
        {
            var definition = ropeTileAnimations[i];
            if (definition == null) continue;
            int count = 1 + (definition.impulseAnimationTiles?.Length ?? 0) + (definition.preparingAnimationTiles?.Length ?? 0);
            for (int j = 0; j < count; j++)
                if (RopeViewTile(i, j) == tile) { entry = i; frame = j; return true; }
        }
        return false;
    }

    bool CaptureNetworkRope()
    {
        ropeCellBuffer.Clear(); ropeEntryBuffer.Clear(); ropeFrameBuffer.Clear();
        if (indestructibleTilemap == null) return false;
        foreach (var cell in indestructibleTilemap.cellBounds.allPositionsWithin)
        {
            if (!RopeViewIndex(indestructibleTilemap.GetTile(cell), out int entry, out int frame)) continue;
            if (ropeCellBuffer.Count == MaxNetworkRopeCells) return false;
            ropeCellBuffer.Add(cell); ropeEntryBuffer.Add(entry); ropeFrameBuffer.Add(frame);
        }
        return true;
    }

    void LateUpdate()
    {
        if (!NetSync.IsServer || !NetworkServer.active || NetworkServer.isLoadingScene || !IsBattleMode11Active() ||
            !CaptureNetworkRope()) return;
        bool changed = lastRopeView.cells == null || lastRopeView.cells.Length != ropeCellBuffer.Count;
        if (!changed)
            for (int i = 0; i < ropeCellBuffer.Count; i++)
                if (lastRopeView.cells[i] != ropeCellBuffer[i] || lastRopeView.entries[i] != ropeEntryBuffer[i] ||
                    lastRopeView.frames[i] != ropeFrameBuffer[i]) { changed = true; break; }
        if (!changed && Time.unscaledTime < nextRopeViewHeartbeat) return;
        nextRopeViewHeartbeat = Time.unscaledTime + .5f;
        lastRopeView = new RopeTileView { scene = gameObject.scene.name, path = NetworkRopePath,
            revision = ++ropeViewRevision, time = NetworkTime.localTime, cells = ropeCellBuffer.ToArray(),
            entries = ropeEntryBuffer.ToArray(), frames = ropeFrameBuffer.ToArray() };
        NetworkServer.SendToReady(lastRopeView);
    }

    public void ReceiveNetworkRope(RopeTileView view)
    {
        if (NetSync.Mode != NetSync.NetMode.Client || !NetworkClient.active || NetworkServer.active ||
            view.scene != gameObject.scene.name || view.path != NetworkRopePath || view.cells == null || view.entries == null ||
            view.frames == null || view.cells.Length > MaxNetworkRopeCells || view.cells.Length != view.entries.Length ||
            view.cells.Length != view.frames.Length || double.IsNaN(view.time) || double.IsInfinity(view.time) ||
            !NetworkInputHistory.IsNewer(view.revision, receivedRopeViewRevision)) return;
        for (int i = 0; i < view.cells.Length; i++)
        {
            var tile = RopeViewTile(view.entries[i], view.frames[i]);
            var rest = RopeViewTile(view.entries[i], 0);
            if (tile == null || rest == null || tile.gameObject != null || rest.gameObject != null) return;
            for (int j = 0; j < i; j++) if (view.cells[j] == view.cells[i]) return;
        }
        receivedRopeViewRevision = view.revision;
        if (NetworkClient.localPlayer != null && NetworkClient.localPlayer.TryGetComponent<NetworkTransformReliable>(out var sync))
            view.time += NetworkClient.sendInterval * (sync.sendIntervalMultiplier - 1 +
                (sync.timelineOffset ? sync.sendIntervalMultiplier : 0));
        ropeViewHistory.Add(view);
        if (ropeViewHistory.Count > 64) ropeViewHistory.RemoveAt(0);
    }

    void PresentNetworkRope()
    {
        retiredRopeCells.Clear();
        foreach (var pair in hiddenRopeCells)
            if (indestructibleTilemap == null || indestructibleTilemap.GetTile(pair.Key) != pair.Value.tile)
            {
                if (ropeViewMap != null) ropeViewMap.SetTile(pair.Key, null);
                retiredRopeCells.Add(pair.Key);
            }
        foreach (var cell in retiredRopeCells) hiddenRopeCells.Remove(cell);
        if (!NetworkClient.active || NetworkServer.active || GamePauseController.IsPaused || ropeViewHistory.Count == 0 ||
            ropeViewHistory[0].time > NetworkTime.time || indestructibleTilemap == null) return;
        while (ropeViewHistory.Count > 1 && ropeViewHistory[1].time <= NetworkTime.time) ropeViewHistory.RemoveAt(0);
        var view = ropeViewHistory[0];
        if (view.revision == appliedRopeViewRevision) return;
        RestoreNetworkRopeCells();
        for (int i = 0; i < view.cells.Length; i++)
        {
            var cell = view.cells[i];
            var source = indestructibleTilemap.GetTile(cell);
            // Sudden death or another terrain replacement supersedes this view.
            if (source != RopeViewTile(view.entries[i], 0) || view.frames[i] == 0) continue;
            var tile = RopeViewTile(view.entries[i], view.frames[i]);
            if (tile == null) continue;
            EnsureNetworkRopeMap();
            if (!ropeViewTiles.TryGetValue(tile, out var plain))
            {
                plain = ScriptableObject.CreateInstance<Tile>();
                plain.sprite = tile.sprite; plain.colliderType = Tile.ColliderType.None;
                plain.flags = TileFlags.None;
                ropeViewTiles.Add(tile, plain);
            }
            var color = indestructibleTilemap.GetColor(cell);
            var flags = indestructibleTilemap.GetTileFlags(cell);
            hiddenRopeCells[cell] = (source, color, flags);
            indestructibleTilemap.SetTileFlags(cell, flags & ~TileFlags.LockColor);
            var hidden = color; hidden.a = 0; indestructibleTilemap.SetColor(cell, hidden);
            ropeViewMap.SetTile(cell, plain);
            ropeViewMap.SetColor(cell, color);
            ropeViewMap.SetTransformMatrix(cell, tile.transform);
        }
        appliedRopeViewRevision = view.revision;
    }

    void EnsureNetworkRopeMap()
    {
        if (ropeViewMap != null) return;
        var go = new GameObject("ApprovedRopeVisual", typeof(Tilemap), typeof(TilemapRenderer));
        go.transform.SetParent(indestructibleTilemap.transform.parent, false);
        go.transform.localPosition = indestructibleTilemap.transform.localPosition;
        go.transform.localRotation = indestructibleTilemap.transform.localRotation;
        go.transform.localScale = indestructibleTilemap.transform.localScale;
        ropeViewMap = go.GetComponent<Tilemap>();
        ropeViewMap.tileAnchor = indestructibleTilemap.tileAnchor;
        ropeViewMap.orientation = indestructibleTilemap.orientation;
        ropeViewMap.orientationMatrix = indestructibleTilemap.orientationMatrix;
        ropeViewMap.color = indestructibleTilemap.color;
        if (indestructibleTilemap.TryGetComponent<TilemapRenderer>(out var source))
        {
            var renderer = go.GetComponent<TilemapRenderer>();
            renderer.sharedMaterial = source.sharedMaterial; renderer.sortingLayerID = source.sortingLayerID;
            renderer.sortingOrder = source.sortingOrder; renderer.mode = source.mode;
            renderer.maskInteraction = source.maskInteraction;
        }
    }

    void RestoreNetworkRopeCells()
    {
        if (indestructibleTilemap != null)
            foreach (var pair in hiddenRopeCells)
                if (indestructibleTilemap.GetTile(pair.Key) == pair.Value.tile)
                {
                    indestructibleTilemap.SetTileFlags(pair.Key, pair.Value.flags & ~TileFlags.LockColor);
                    indestructibleTilemap.SetColor(pair.Key, pair.Value.color);
                    indestructibleTilemap.SetTileFlags(pair.Key, pair.Value.flags);
                }
        hiddenRopeCells.Clear();
        if (ropeViewMap != null) ropeViewMap.ClearAllTiles();
    }

    void OnDisable()
    {
        RestoreNetworkRopeCells(); ropeViewHistory.Clear(); appliedRopeViewRevision = 0;
        foreach (var pair in runningSwaps)
        {
            if (pair.Value.routine != null) StopCoroutine(pair.Value.routine);
            if (pair.Value.tilemap != null) pair.Value.tilemap.SetTile(pair.Key, pair.Value.originalTile);
        }
        runningSwaps.Clear();
        if (NetworkServer.active && NetSync.IsServer && IsBattleMode11Active())
            NetworkServer.SendToReady(new RopeTileView { scene = gameObject.scene.name, path = NetworkRopePath,
                revision = ++ropeViewRevision, time = NetworkTime.localTime, cells = System.Array.Empty<Vector3Int>(),
                entries = System.Array.Empty<int>(), frames = System.Array.Empty<int>() });
    }

    void OnDestroy()
    {
        RestoreNetworkRopeCells();
        if (ropeViewMap != null) Destroy(ropeViewMap.gameObject);
        foreach (var tile in ropeViewTiles.Values) if (tile != null) Destroy(tile);
        ropeViewTiles.Clear();
    }
}

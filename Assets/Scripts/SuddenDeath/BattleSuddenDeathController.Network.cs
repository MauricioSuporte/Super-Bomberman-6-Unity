using System;
using System.Collections.Generic;
using Assets.Scripts.Netcode;
using Mirror;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Assets.Scripts.Netcode
{
    public struct SuddenDeathFall { public Vector3Int cell; public Vector3 start, end; public float began, duration; }
    public struct SuddenDeathState : NetworkMessage
    {
        public string scene;
        public uint revision;
        public double time;
        public bool active, paused;
        public float elapsed, remaining, musicWait;
        public Vector3Int[] path, committed;
        public SuddenDeathFall[] falls;
    }
}

public sealed partial class BattleSuddenDeathController
{
    readonly Dictionary<Vector3Int, SuddenDeathFall> networkFalls = new();
    readonly HashSet<Vector3Int> networkCommitted = new();
    readonly List<SuddenDeathState> networkHistory = new(32);
    readonly HashSet<Vector3Int> networkAppliedCells = new();
    readonly Dictionary<Vector3Int, (GameObject block, GameObject shadow)> networkViews = new();
    readonly List<Vector3Int> networkRemoveViews = new();
    readonly Dictionary<Vector3Int, Vector2> networkShadowStarts = new();
    uint networkRevision, receivedNetworkRevision;
    float networkStarted, networkRemaining, networkMusicWait, nextNetworkSample, presentedNetworkElapsed, nextNetworkDiagnostic;
    bool networkBegun, clientNetworkBegun;

    static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);

    void BeginNetworkSuddenDeath(float remaining, float musicWait)
    {
        if (!NetSync.IsServer || !NetworkServer.active) return;
        networkBegun = true; networkStarted = Time.time; networkRemaining = remaining; networkMusicWait = musicWait;
        networkFalls.Clear(); networkCommitted.Clear();
        PublishNetworkSuddenDeath(true);
    }

    SuddenDeathState CaptureNetworkSuddenDeath(bool active = true)
    {
        var falls = new SuddenDeathFall[networkFalls.Count]; networkFalls.Values.CopyTo(falls, 0);
        var committed = new Vector3Int[networkCommitted.Count]; networkCommitted.CopyTo(committed);
        return new SuddenDeathState { scene = gameObject.scene.name, revision = networkRevision,
            time = NetworkTime.localTime, active = active, paused = GamePauseController.IsPaused || Time.timeScale == 0,
            elapsed = Mathf.Max(0, Time.time - networkStarted), remaining = networkRemaining, musicWait = networkMusicWait,
            path = suddenDeathPath.ToArray(), falls = falls, committed = committed };
    }

    void PublishNetworkSuddenDeath(bool force = false, bool active = true)
    {
        if (!NetSync.IsServer || !NetworkServer.active || NetworkServer.isLoadingScene || !networkBegun ||
            (!force && Time.unscaledTime < nextNetworkSample)) return;
        nextNetworkSample = Time.unscaledTime + .1f;
        ++networkRevision;
        NetworkServer.SendToReady(CaptureNetworkSuddenDeath(active));
    }

    void ReportNetworkFall(Vector3Int cell, Vector3 start, Vector3 end, float duration)
    {
        if (!NetSync.IsServer || !NetworkServer.active || !networkBegun) return;
        networkFalls[cell] = new SuddenDeathFall { cell = cell, start = start, end = end,
            began = Mathf.Max(0, Time.time - networkStarted), duration = duration };
        PublishNetworkSuddenDeath(true);
    }

    void ReportNetworkCommit(Vector3Int cell)
    {
        if (!NetSync.IsServer || !NetworkServer.active || !networkBegun) return;
        networkFalls.Remove(cell);
        if (indestructibleTilemap.GetTile(cell) == currentStageIndestructibleTile) networkCommitted.Add(cell);
        PublishNetworkSuddenDeath(true);
    }

    public void ReceiveNetworkSuddenDeath(SuddenDeathState state)
    {
        if (NetSync.Mode != NetSync.NetMode.Client || !NetworkClient.active || NetworkServer.active ||
            state.scene != gameObject.scene.name || !NetworkInputHistory.IsNewer(state.revision, receivedNetworkRevision) ||
            double.IsNaN(state.time) || double.IsInfinity(state.time) || !Finite(state.elapsed) || state.elapsed < 0 ||
            !Finite(state.remaining) || state.remaining < 0 || !Finite(state.musicWait) || state.musicWait < 0 ||
            state.path == null || state.path.Length > 1024 || state.committed == null || state.committed.Length > state.path.Length ||
            state.falls == null || state.falls.Length > state.path.Length) return;
        var cells = new HashSet<Vector3Int>(state.path);
        if (cells.Count != state.path.Length) return;
        var used = new HashSet<Vector3Int>();
        foreach (var cell in state.committed) if (!cells.Contains(cell) || !used.Add(cell)) return;
        foreach (var fall in state.falls)
            if (!cells.Contains(fall.cell) || !used.Add(fall.cell) || !Finite(fall.start) || !Finite(fall.end) ||
                !Finite(fall.began) || fall.began < 0 || fall.began > state.elapsed ||
                !Finite(fall.duration) || fall.duration <= 0 || fall.duration > 30) return;
        // Reject malformed snapshots before acknowledging their serial.
        receivedNetworkRevision = state.revision;
        if (!state.active) { ClearNetworkSuddenDeath(); return; }
        if (NetworkClient.localPlayer != null && NetworkClient.localPlayer.TryGetComponent<NetworkTransformReliable>(out var sync))
            state.time += NetworkClient.sendInterval * (sync.sendIntervalMultiplier - 1 + (sync.timelineOffset ? sync.sendIntervalMultiplier : 0));
        state.path = (Vector3Int[])state.path.Clone(); state.committed = (Vector3Int[])state.committed.Clone();
        state.falls = (SuddenDeathFall[])state.falls.Clone();
        networkHistory.Add(state);
        if (networkHistory.Count > 32) networkHistory.RemoveAt(0);
    }

    void PresentNetworkSuddenDeath()
    {
        if (NetSync.Mode != NetSync.NetMode.Client || !NetworkClient.active || NetworkServer.active ||
            GamePauseController.IsPaused || networkHistory.Count == 0 || networkHistory[0].time > NetworkTime.time) return;
        while (networkHistory.Count > 1 && networkHistory[1].time <= NetworkTime.time) networkHistory.RemoveAt(0);
        var state = networkHistory[0];
        float elapsed = state.elapsed + (state.paused ? 0 : Mathf.Clamp((float)(NetworkTime.time - state.time), 0, .25f));
        presentedNetworkElapsed = Mathf.Max(presentedNetworkElapsed, elapsed);
        if (!clientNetworkBegun)
        {
            clientNetworkBegun = true;
            BeginSuddenDeath(state.remaining, state.path, Mathf.Max(0, state.musicWait - elapsed), elapsed < state.musicWait);
        }
        EnsureQueuedShadows(presentedNetworkElapsed, GetDropWindowDuration());
        UpdateQueuedShadows(presentedNetworkElapsed);
        bool terrainChanged = false;
        foreach (var cell in state.committed)
        {
            if (networkAppliedCells.Contains(cell) || indestructibleTilemap == null || currentStageIndestructibleTile == null) continue;
            // Only an explicit server landing commits terrain. No cleanup, damage,
            // item spawn or native GameManager callbacks run on a pure client.
            if (destructibleTilemap != null) destructibleTilemap.SetTile(destructibleTilemap.WorldToCell(indestructibleTilemap.GetCellCenterWorld(cell)), null);
            indestructibleTilemap.SetTile(cell, currentStageIndestructibleTile);
            terrainChanged = true; networkAppliedCells.Add(cell);
            RemoveNetworkFallView(cell);
            if (queuedShadowVisuals.Remove(cell, out var queued) && queued.ShadowObject != null) DestroyNetworkVisual(queued.ShadowObject);
            scheduledShadowCells.Add(cell);
            if (GameplayLatencyDiagnostics.Enabled)
                GameplayLatencyDiagnostics.Write(GameplayLatencyDiagnostics.For("sudden_death_commit_apply", detail:$"cell={cell.x},{cell.y}"));
        }
        if (terrainChanged)
        {
            if (indestructibleTilemap.TryGetComponent<TilemapCollider2D>(out var solid) && solid.isActiveAndEnabled) solid.ProcessTilemapChanges();
            if (destructibleTilemap != null && destructibleTilemap.TryGetComponent<TilemapCollider2D>(out var removed) && removed.isActiveAndEnabled) removed.ProcessTilemapChanges();
        }
        foreach (var fall in state.falls)
        {
            if (networkAppliedCells.Contains(fall.cell)) continue;
            if (!networkViews.TryGetValue(fall.cell, out var view))
            {
                if (!TryGetCurrentTileSprite(out var sprite)) continue;
                GameObject shadow = null;
                if (queuedShadowVisuals.Remove(fall.cell, out var queued)) shadow = queued.ShadowObject;
                if (enableShadowVisual && shadow == null) shadow = CreateShadowVisual(fall.end + shadowWorldOffset);
                view = (CreateFallingVisual(sprite, fall.start), shadow); networkViews[fall.cell] = view;
                networkShadowStarts[fall.cell] = shadow != null ? new Vector2(shadow.transform.localScale.x,
                    shadow.TryGetComponent<SpriteRenderer>(out var shadowRenderer) ? shadowRenderer.color.a : 0) : new Vector2(shadowStartSize, 0);
                scheduledShadowCells.Add(fall.cell);
            }
            float t = Mathf.Clamp01((presentedNetworkElapsed - fall.began) / fall.duration);
            if (view.block != null) view.block.transform.position = Vector3.Lerp(fall.start, fall.end, t);
            if (view.shadow != null)
            {
                Vector2 initial = networkShadowStarts[fall.cell];
                float size = Mathf.Lerp(initial.x, shadowEndSize, t);
                view.shadow.transform.localScale = new Vector3(size, size, 1);
                if (view.shadow.TryGetComponent<SpriteRenderer>(out var renderer)) renderer.color = new Color(0, 0, 0, Mathf.Lerp(initial.y, shadowAlpha, t));
            }
        }
        networkRemoveViews.Clear();
        foreach (var pair in networkViews)
        {
            bool found = false; foreach (var fall in state.falls) if (fall.cell == pair.Key) { found = true; break; }
            if (!found) networkRemoveViews.Add(pair.Key);
        }
        foreach (var cell in networkRemoveViews) RemoveNetworkFallView(cell);
        if (GameplayLatencyDiagnostics.Enabled && Time.unscaledTime >= nextNetworkDiagnostic)
        {
            nextNetworkDiagnostic = Time.unscaledTime + .1f;
            var sample = GameplayLatencyDiagnostics.For("sudden_death_present",
                detail: FormattableString.Invariant($"falls={networkViews.Count};committed={networkAppliedCells.Count};elapsed={presentedNetworkElapsed:F3};paused={state.paused}"));
            sample.authoritativeTick = state.revision; GameplayLatencyDiagnostics.Write(sample);
        }
    }

    static void DestroyNetworkVisual(GameObject visual)
    {
        if (Application.isPlaying) Destroy(visual); else DestroyImmediate(visual);
    }

    void RemoveNetworkFallView(Vector3Int cell)
    {
        if (!networkViews.Remove(cell, out var view)) return;
        networkShadowStarts.Remove(cell);
        if (view.block != null) DestroyNetworkVisual(view.block);
        if (view.shadow != null) DestroyNetworkVisual(view.shadow);
    }

    void ClearNetworkSuddenDeath()
    {
        foreach (var view in networkViews.Values)
        { if (view.block != null) DestroyNetworkVisual(view.block); if (view.shadow != null) DestroyNetworkVisual(view.shadow); }
        networkViews.Clear(); networkShadowStarts.Clear(); networkRemoveViews.Clear(); networkHistory.Clear(); networkAppliedCells.Clear();
        networkFalls.Clear(); networkCommitted.Clear(); clientNetworkBegun = networkBegun = false; presentedNetworkElapsed = 0;
        ClearAllQueuedShadowVisuals();
        suddenDeathStarted = suddenDeathFinished = false;
        scheduledShadowCells.Clear(); activeFallingCellApplyTimes.Clear();
    }
}

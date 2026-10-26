using Assets.Scripts.Netcode;
using Mirror;
using UnityEngine;

public sealed partial class BattleMode5ConveyorController
{
    public struct NetworkMotorState
    {
        public string path;
        public bool clockwise, fast, hasTarget;
        public Vector3Int target;
        public Vector2 incoming;
    }

    string networkPath;
    uint networkPhaseRevision = 1, receivedNetworkPhaseRevision;
    float nextNetworkPhase;

    public bool HasNetworkMotor => IsConveyorSceneActive();
    public string NetworkPath
    {
        get
        {
            if (networkPath != null) return networkPath;
            networkPath = name;
            for (var parent = transform.parent; parent != null; parent = parent.parent) networkPath = parent.name + "/" + networkPath;
            return networkPath;
        }
    }

    void PublishNetworkPhase(bool force = false)
    {
        if (!NetworkServer.active || !NetSync.IsOnline || !HasNetworkMotor || NetworkServer.isLoadingScene) return;
        if (force) ++networkPhaseRevision;
        else if (Time.unscaledTime < nextNetworkPhase) return;
        nextNetworkPhase = Time.unscaledTime + 0.5f;
        NetworkServer.SendToReady(new ConveyorPhase { scene = gameObject.scene.name, path = NetworkPath,
            revision = networkPhaseRevision, clockwise = clockwise, fast = fast });
        if (force && GameplayLatencyDiagnostics.Enabled)
            GameplayLatencyDiagnostics.Write(GameplayLatencyDiagnostics.For("conveyor_phase_send", detail: NetworkPath + ":" + networkPhaseRevision + ":cw=" + clockwise + ":fast=" + fast));
    }

    public void ApplyNetworkPhase(ConveyorPhase phase)
    {
        if (NetSync.Mode != NetSync.NetMode.Client || phase.scene != gameObject.scene.name || phase.path != NetworkPath ||
            !NetworkInputHistory.IsNewer(phase.revision, receivedNetworkPhaseRevision)) return;
        receivedNetworkPhaseRevision = phase.revision;
        clockwise = phase.clockwise; fast = phase.fast;
        RefreshControlTileVisuals(); RefreshConveyorAnimatedTileVisuals();
        if (GameplayLatencyDiagnostics.Enabled)
            GameplayLatencyDiagnostics.Write(GameplayLatencyDiagnostics.For("conveyor_phase_apply", detail: NetworkPath + ":" + phase.revision + ":cw=" + clockwise + ":fast=" + fast));
    }

    public NetworkMotorState CaptureNetworkMotor(MovementController mover, NetworkMotorState acknowledged)
    {
        var state = NetSync.ShouldSimulateLocally ? new NetworkMotorState { path = NetworkPath, clockwise = clockwise, fast = fast } : acknowledged;
        int key = GetObjectKey(mover.gameObject);
        state.hasTarget = lockedTargetCellByObject.TryGetValue(key, out state.target);
        lockedIncomingDirectionByObject.TryGetValue(key, out state.incoming);
        return state;
    }

    public void RestoreNetworkMotor(MovementController mover, NetworkMotorState state)
    {
        int key = GetObjectKey(mover.gameObject); ClearObjectConveyorState(key);
        if (state.path != NetworkPath || !state.hasTarget) return;
        lockedTargetCellByObject[key] = state.target; lockedIncomingDirectionByObject[key] = state.incoming;
    }

    public void SimulateNetworkMotor(MovementController mover, NetworkMotorState state)
    {
        if (mover == null || mover.Rigidbody == null || mover.isDead || mover.IsEndingStage || GamePauseController.IsPaused || !HasNetworkMotor) return;
        if (!NetSync.ShouldSimulateLocally && (!mover.PredictLocally || state.path != NetworkPath)) return;
        ResolveReferences();
        if (groundTilemap == null) return;
        if (conveyorCells.Count == 0) BuildConveyorPath();
        int key = GetObjectKey(mover.gameObject);
        if (ShouldIgnorePlayerConveyorEffect(mover)) { ClearObjectConveyorState(key); return; }
        bool phaseClockwise = NetSync.ShouldSimulateLocally ? clockwise : state.clockwise;
        bool phaseFast = NetSync.ShouldSimulateLocally ? fast : state.fast;
        if (!TryGetConveyorTarget(key, mover.Rigidbody.position, out var target, out var targetCell, out var sourceCell, out _, out _, phaseClockwise)) return;
        if (NetworkConveyorBlocked(mover, sourceCell)) { ClearObjectConveyorState(key); return; }
        Vector2 direction = GetConveyorDirection(sourceCell, phaseClockwise);
        float dot = Vector2.Dot(NormalizeCardinal(mover.Direction), direction);
        if (dot > 0.5f) mover.ApplyExternalMovementSpeedMultiplier(phaseFast ? playerWithFastConveyorSpeedMultiplier : playerWithSlowConveyorSpeedMultiplier, playerSpeedMultiplierRefreshSeconds);
        else if (dot < -0.5f) mover.ApplyExternalMovementSpeedMultiplier(phaseFast ? playerAgainstFastConveyorSpeedMultiplier : playerAgainstSlowConveyorSpeedMultiplier, playerSpeedMultiplierRefreshSeconds);

        if (NetworkConveyorBlocked(mover, targetCell))
        {
            ClearObjectConveyorState(key); target = GetCellCenterWorld(sourceCell);
            if (Vector2.Dot(target - mover.Rigidbody.position, direction) < -0.001f) return;
        }
        float distance = (phaseFast ? fastSpeedTilesPerSecond : slowSpeedTilesPerSecond) * ResolveTileSize() * Time.fixedDeltaTime;
        Vector2 next = Vector2.MoveTowards(mover.Rigidbody.position, target, distance);
        mover.Rigidbody.position = next;
        mover.transform.position = new Vector3(next.x, next.y, mover.transform.position.z);
    }

    bool NetworkConveyorBlocked(MovementController mover, Vector3Int cell)
    {
        if (IsBlockedForConveyor(cell, null, false)) return true;
        return !NetSync.ShouldSimulateLocally && mover.TryGetComponent<NetworkBombFx>(out var fx) &&
            mover.TryGetComponent<NetworkPlayerInput>(out var input) && fx.HasPredictedPlacementAt(GetCellCenterWorld(cell), ResolveTileSize(), input.CurrentMovementTick);
    }
}

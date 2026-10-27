using Assets.Scripts.Netcode;
using Mirror;
using UnityEngine;

public partial class MovementController
{
    MonoBehaviour approvedEnemyForceSource;
    NetworkEnemyForceState approvedEnemyForceReceipt, networkEnemyForce;
    Vector2 approvedEnemyForceTarget;
    double approvedEnemyForceStep = double.NegativeInfinity;
    bool ambiguousEnemyForceStep;

    bool HasCurrentEnemyForceReceipt => approvedEnemyForceStep == Time.fixedTimeAsDouble && approvedEnemyForceReceipt.sourceId != 0;
    public string NetworkEnemyForceDiagnosticState => CaptureEnemyForceState().kind.ToString();

    public void RegisterNetworkEnemyForce(MonoBehaviour source, NetworkEnemyForceKind kind, Vector2 expectedTarget,
        float speed, Vector2 direction, float remainingDistance = float.MaxValue, bool allowPull = true)
    {
        if (!NetSync.IsServer || !networkTickDriven || source == null || !source.isActiveAndEnabled || source is not INetworkEnemyForceSource ||
            !TryGetComponent<NetworkIdentity>(out var identity) || !identity.isServer || identity.netId == 0) return;
        bool sameStep = approvedEnemyForceStep == Time.fixedTimeAsDouble;
        ambiguousEnemyForceStep = sameStep && (ambiguousEnemyForceStep || approvedEnemyForceSource != source);
        approvedEnemyForceStep = Time.fixedTimeAsDouble; approvedEnemyForceSource = source;
        approvedEnemyForceTarget = expectedTarget;
        approvedEnemyForceReceipt = new NetworkEnemyForceState { sourceId = source.GetEntityId().GetHashCode(), kind = kind,
            direction = direction, speed = speed, remainingDistance = remainingDistance, allowPull = allowPull };
    }

    NetworkEnemyForceState CaptureEnemyForceState()
    {
        if (!NetSync.ShouldSimulateLocally) return networkEnemyForce.IsValid && NetworkForceSceneMatches(networkEnemyForce.scene) ? networkEnemyForce : default;
        if (!HasCurrentEnemyForceReceipt || ambiguousEnemyForceStep || approvedEnemyForceSource == null || !approvedEnemyForceSource.isActiveAndEnabled ||
            approvedPusherStep == Time.fixedTimeAsDouble || Rigidbody == null || !CanPredictNetworkMovement ||
            (Rigidbody.position - approvedEnemyForceTarget).sqrMagnitude > 0.000001f) return default;
        var state = ((INetworkEnemyForceSource)approvedEnemyForceSource).CaptureNetworkForce(this, approvedEnemyForceReceipt);
        state.tileSize = tileSize; state.scene = approvedEnemyForceSource.gameObject.scene.name;
        if (!float.IsFinite(tileSize) || tileSize <= 0) return default;
        state.safeCell = new Vector2(Mathf.Round(Rigidbody.position.x / tileSize) * tileSize, Mathf.Round(Rigidbody.position.y / tileSize) * tileSize);
        if (!state.IsValid || !NetworkForceSceneMatches(state.scene) || !state.IsAligned(Rigidbody.position) ||
            (state.kind != NetworkEnemyForceKind.Bubble && state.Contains(state.anchor))) return default;
        Vector2 axis = state.kind == NetworkEnemyForceKind.Bubble ? state.direction : state.anchor - Rigidbody.position;
        return IsBlockedAtPosition(state.safeCell, axis, false) || HasNetworkForcePreviewHazard(state.safeCell) ? default : state;
    }

    void RestoreEnemyForceState(NetworkEnemyForceState state, double serverTime)
    {
        networkEnemyForce = state.IsValid && NetworkForceSceneMatches(state.scene) && FreshNetworkForceApproval(serverTime, state.remaining) ? state : default;
        if (networkPusher.IsValid && networkEnemyForce.IsValid) { networkPusher = default; networkEnemyForce = default; }
    }

    void ClearNetworkEnemyForce()
    {
        networkEnemyForce = default; approvedEnemyForceReceipt = default; approvedEnemyForceSource = null;
        approvedEnemyForceStep = double.NegativeInfinity; ambiguousEnemyForceStep = false;
    }

    void SimulateEnemyForceTick()
    {
        if (NetSync.Mode != NetSync.NetMode.Client || !predictLocally || !hasNetworkMovementContext || !networkEnemyForce.IsValid) return;
        if (GamePauseController.IsPaused) return;
        var state = networkEnemyForce;
        float dt = Mathf.Min(Time.fixedDeltaTime, state.remaining);
        if (!CanPredictNetworkMovement || !NetworkForceSceneMatches(state.scene) || dt <= 0 || !state.Contains(Rigidbody.position) || !state.IsAligned(Rigidbody.position))
        { networkEnemyForce = default; return; }
        if (state.kind == NetworkEnemyForceKind.Hurricane && NetworkEnemyForceState.IsFleeing(direction, Rigidbody.position - state.anchor))
        {
            // Same acknowledged phase and deterministic input rule; never writes
            // native stats or a wall-clock modifier while replaying the owner.
            ApplyExternalMovementSpeedMultiplier(0.5f, Time.fixedDeltaTime * 2);
            state.remaining = Mathf.Max(0, state.remaining - dt);
            networkEnemyForce = state.remaining > 0.000001f ? state : default;
            return;
        }
        if (!state.allowPull) { networkEnemyForce = default; return; }
        float step = Mathf.Min(state.speed * dt, state.remainingDistance);
        Vector2 candidate = state.kind == NetworkEnemyForceKind.Bubble
            ? Rigidbody.position + state.direction * step : Vector2.MoveTowards(Rigidbody.position, state.anchor, step);
        if (!state.Contains(candidate) || IsBlockedAtPosition(candidate, candidate - Rigidbody.position, false) || HasNetworkForcePreviewHazard(candidate))
        { networkEnemyForce = default; return; }
        state.remainingDistance = Mathf.Max(0, state.remainingDistance - Vector2.Distance(candidate, Rigidbody.position));
        SetNetworkPosition(candidate);
        state.remaining = Mathf.Max(0, state.remaining - dt);
        networkEnemyForce = state.remaining > 0.000001f && state.remainingDistance > 0.000001f ? state : default;
    }
}

using Assets.Scripts.Netcode;
using Mirror;
using UnityEngine;

public partial class MovementController
{
    PusherPatrolAndPush approvedPusher;
    double approvedPusherStep = double.NegativeInfinity;
    bool ambiguousPusherStep;
    PusherPatrolAndPush.NetworkPushState networkPusher;
    readonly RaycastHit2D[] networkPusherHits = new RaycastHit2D[16];
    readonly Collider2D[] networkForceHazards = new Collider2D[16];

    public bool HasPusherMovementContext => NetSync.ShouldSimulateLocally
        ? CapturePusherState().sourceId != 0 : networkPusher.IsValid && NetworkForceSceneMatches(networkPusher.scene);

    public void RegisterNetworkPusherContact(PusherPatrolAndPush source)
    {
        if (!NetSync.IsServer || !networkTickDriven || source == null || !source.isActiveAndEnabled || !TryGetComponent<NetworkIdentity>(out var identity) || !identity.isServer || identity.netId == 0) return;
        bool sameStep = approvedPusherStep == Time.fixedTimeAsDouble;
        ambiguousPusherStep = sameStep && (ambiguousPusherStep || approvedPusher != source);
        approvedPusherStep = Time.fixedTimeAsDouble;
        approvedPusher = source;
    }

    PusherPatrolAndPush.NetworkPushState CapturePusherState()
    {
        if (!NetSync.ShouldSimulateLocally) return networkPusher.IsValid && NetworkForceSceneMatches(networkPusher.scene) ? networkPusher : default;
        var state = approvedPusherStep == Time.fixedTimeAsDouble && !ambiguousPusherStep && approvedPusher != null
            ? approvedPusher.CaptureNetworkContinuation(this) : default;
        if (state.sourceId == 0 || !NetworkForceSceneMatches(state.scene) || HasCurrentEnemyForceReceipt) return default;
        Vector2 axis = state.horizontal ? Vector2.right : Vector2.up;
        return IsBlockedAtPosition(state.safeCell, axis, false) || HasNetworkForcePreviewHazard(state.safeCell) ? default : state;
    }

    void RestorePusherState(PusherPatrolAndPush.NetworkPushState state, double serverTime)
    {
        // Do not resurrect a contact after a transport stall or clock discontinuity.
        // Replay alone advances its phase; message age never adds another lead.
        networkPusher = state.IsValid && NetworkForceSceneMatches(state.scene) && FreshNetworkForceApproval(serverTime, state.remaining) ? state : default;
    }

    static bool NetworkForceSceneMatches(string scene) => scene == UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

    static bool FreshNetworkForceApproval(double serverTime, float remaining)
    {
        double age = NetworkClient.active ? NetworkBombFx.KickServerNow - serverTime : 0;
        return !NetworkClient.active || GamePauseController.IsPaused ||
            (double.IsFinite(age) && age >= -PusherPatrolAndPush.NetworkContinuationSeconds && age <= remaining);
    }

    void ClearNetworkPusher()
    {
        networkPusher = default; approvedPusher = null; ambiguousPusherStep = false;
        approvedPusherStep = double.NegativeInfinity;
    }

    void SimulatePusherTick()
    {
        // Native server contacts already run once on the world clock, after the
        // input batch. This branch is ONLY an owner's bounded confirmed continuation.
        if (NetSync.Mode != NetSync.NetMode.Client || !predictLocally || !hasNetworkMovementContext || !networkPusher.IsValid) return;
        if (GamePauseController.IsPaused) return;
        if (!CanPredictNetworkMovement || !NetworkForceSceneMatches(networkPusher.scene)) { networkPusher = default; return; }
        var state = networkPusher;
        float dt = Mathf.Min(Time.fixedDeltaTime, state.remaining);
        if (dt <= 0 || !state.Contains(Rigidbody.position)) { networkPusher = default; return; }
        var filter = new ContactFilter2D { useLayerMask = true, layerMask = state.playerMask, useTriggers = true };
        int count = Physics2D.BoxCast(state.center, state.size, 0, state.velocity.normalized, filter,
            networkPusherHits, state.velocity.magnitude * dt + state.skin);
        bool contact = false;
        if (count < networkPusherHits.Length)
            for (int i = 0; i < count; i++)
            {
                var hit = networkPusherHits[i];
                if (hit.collider != null && hit.rigidbody == Rigidbody && state.Accepts(hit)) { contact = true; break; }
            }
        Vector2 displacement = state.horizontal ? new Vector2(state.velocity.x * dt, 0) : new Vector2(0, state.velocity.y * dt);
        Vector2 candidate = Rigidbody.position + displacement;
        if (!contact || !state.Contains(candidate) || IsBlockedAtPosition(candidate, displacement, false) || HasNetworkForcePreviewHazard(candidate))
        {
            networkPusher = default;
            return;
        }
        SetNetworkPosition(candidate);
        state.center += state.velocity * dt;
        state.remaining = Mathf.Max(0, state.remaining - dt);
        networkPusher = state.remaining > 0.000001f ? state : default;
    }

    bool HasNetworkForcePreviewHazard(Vector2 position)
    {
        var filter = new ContactFilter2D { useLayerMask = true, layerMask = obstacleMask | LayerMask.GetMask("Stage"), useTriggers = true };
        int count = Physics2D.OverlapBox(position, Vector2.one * 0.45f, 0, filter, networkForceHazards);
        if (count == networkForceHazards.Length) return true;
        for (int i = 0; i < count; i++)
            if (networkForceHazards[i] != null && networkForceHazards[i].CompareTag("Hole")) return true;
        return false;
    }
}

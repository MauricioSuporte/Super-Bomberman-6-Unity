using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace Assets.Scripts.Netcode
{
    // Presentation-only work hosted by the existing network manager. Its order
    // must follow NetworkTransform; ticking from NetworkManager.LateUpdate would
    // allow an older transform snapshot to overwrite the bomb later that frame.
    [DefaultExecutionOrder(200)]
    [DisallowMultipleComponent]
    public sealed class NetworkBombMotionLifetime : MonoBehaviour
    {
        readonly HashSet<NetworkBombFx> retained = new();
        readonly List<NetworkBombFx> expired = new();

        static bool CanRetain(NetworkBombFx presenter)
        {
            return NetSync.Mode == NetSync.NetMode.Client && NetworkClient.active && !NetworkServer.active &&
                presenter != null && presenter.TryGetComponent<NetworkIdentity>(out var identity) && !identity.isServer &&
                identity.netId != 0 && NetworkClient.spawned.TryGetValue(identity.netId, out var spawned) && spawned == identity;
        }

        static NetworkBombMotionLifetime GetOrCreate(NetworkBombFx presenter)
        {
            if (!CanRetain(presenter) || NetworkManager.singleton is not BombermanNetworkManager manager || manager == null) return null;
            var lifetime = manager.GetComponent<NetworkBombMotionLifetime>();
            if (lifetime == null) lifetime = manager.gameObject.AddComponent<NetworkBombMotionLifetime>();
            return lifetime;
        }

        public static bool Retain(NetworkBombFx presenter)
        {
            var lifetime = GetOrCreate(presenter);
            if (lifetime == null) return false;
            lifetime.retained.Add(presenter);
            return true;
        }

        public static MonoBehaviour ApprovedVisualHost(NetworkBombFx presenter)
        {
            if (!CanRetain(presenter)) return null;
            var lifetime = GetOrCreate(presenter);
            if (lifetime != null && lifetime.isActiveAndEnabled) return lifetime;
            return presenter.isActiveAndEnabled ? presenter : null;
        }

        public static void Forget(NetworkBombFx presenter)
        {
            if (NetworkManager.singleton is BombermanNetworkManager manager && manager != null && manager.TryGetComponent<NetworkBombMotionLifetime>(out var lifetime))
                lifetime.retained.Remove(presenter);
        }

        void LateUpdate() => Present(Time.deltaTime, Time.unscaledDeltaTime);

        public void Present(float deltaTime, float unscaledDeltaTime)
        {
            if (NetSync.Mode != NetSync.NetMode.Client || !NetworkClient.active || NetworkServer.active) { Clear(); return; }
            expired.Clear();
            foreach (var presenter in retained)
            {
                if (!CanRetain(presenter))
                {
                    if (presenter != null) presenter.ClearRetainedBombMotions();
                    expired.Add(presenter);
                }
                else if (presenter.isActiveAndEnabled) expired.Add(presenter); // Its own LateUpdate resumes ownership.
                else presenter.PresentRetainedBombMotions(deltaTime, unscaledDeltaTime);
            }
            foreach (var presenter in expired) retained.Remove(presenter);
        }

        public void Clear()
        {
            StopAllCoroutines(); // Only approved visual timeline waits are hosted here.
            foreach (var presenter in retained)
                if (presenter != null) presenter.ClearRetainedBombMotions();
            retained.Clear(); expired.Clear();
        }

        void OnDestroy() => Clear();
    }

    public partial class NetworkBombFx
    {
        bool presentationOwnerSuspended;
        void OnEnable()
        {
            if (presentationOwnerSuspended && GameplayLatencyDiagnostics.Enabled && NetSync.Mode == NetSync.NetMode.Client)
                GameplayLatencyDiagnostics.Write(GameplayLatencyDiagnostics.For("bomb_motion_owner_reactivated", GetComponent<MovementController>(),
                    detail: "retained_motions=" + kickPlayback.Count));
            presentationOwnerSuspended = false;
            NetworkBombMotionLifetime.Forget(this);
        }

        void OnDisable()
        {
            presentationOwnerSuspended = true;
            if (!NetworkBombMotionLifetime.Retain(this)) { TransferApprovedBombMotions(); ClearKickPlayback(); return; }
            // Cancel speculative input/poses while keeping only already-approved
            // bomb motion. No player input, abilities or gameplay get ticked.
            expiredKicks.Clear();
            foreach (var pair in kickPlayback)
                if (pair.Value.unconfirmed)
                {
                    RollbackPrediction(pair.Key, pair.Value, "presenter_disabled");
                    RestoreMotionCollider(pair.Key, pair.Value);
                    expiredKicks.Add(pair.Key);
                }
            foreach (var bomb in expiredKicks) kickPlayback.Remove(bomb);
            rejectedStarts.Clear();
            ClearPlayerActionPresentations();
            if (GameplayLatencyDiagnostics.Enabled)
                GameplayLatencyDiagnostics.Write(GameplayLatencyDiagnostics.For("bomb_motion_owner_inactive", GetComponent<MovementController>(),
                    detail: "confirmed_motions=" + kickPlayback.Count + ";player_prediction_cancelled"));
        }

        void StartBombMotionLifetime()
        {
            if (!isActiveAndEnabled)
            {
                presentationOwnerSuspended = true;
                NetworkBombMotionLifetime.Retain(this);
            }
        }

        public override void OnStopClient()
        {
            TransferApprovedBombMotions();
            NetworkBombMotionLifetime.Forget(this); ClearKickPlayback(); base.OnStopClient();
        }

        void OnDestroy()
        {
            TransferApprovedBombMotions();
            NetworkBombMotionLifetime.Forget(this); ClearKickPlayback();
        }

        bool CanReceiveTransferredMotion(NetworkBombFx candidate, Bomb target) => candidate != null && candidate != this &&
            candidate.gameObject.scene == target.gameObject.scene && candidate.TryGetComponent<NetworkIdentity>(out var identity) &&
            identity.isClient && !identity.isServer && identity.netId != 0 && NetworkClient.spawned.TryGetValue(identity.netId, out var current) && current == identity;

        NetworkBombFx FindBombMotionReplacement(Bomb target)
        {
            if (NetworkClient.localPlayer != null && NetworkClient.localPlayer.TryGetComponent<NetworkBombFx>(out var local) &&
                CanReceiveTransferredMotion(local, target)) return local;
            foreach (var identity in NetworkClient.spawned.Values)
                if (identity != null && identity.TryGetComponent<NetworkBombFx>(out var candidate) && CanReceiveTransferredMotion(candidate, target))
                    return candidate;
            return null;
        }

        void TransferApprovedBombMotions()
        {
            if (NetSync.Mode != NetSync.NetMode.Client || !NetworkClient.isConnected || NetworkServer.active || NetworkClient.isLoadingScene) return;
            expiredKicks.Clear();
            foreach (var pair in kickPlayback)
                if (pair.Key != null && !pair.Key.HasExploded && pair.Key.isActiveAndEnabled && pair.Key.ClientBombMotionPresenter == this &&
                    !pair.Value.unconfirmed && pair.Value.motion.moving && pair.Value.motion.kind != BombMotionKind.Lift && pair.Value.motion.kind != BombMotionKind.Carry)
                    expiredKicks.Add(pair.Key);
            foreach (var target in expiredKicks)
            {
                var replacement = FindBombMotionReplacement(target);
                if (replacement == null) continue;
                var state = kickPlayback[target];
                // Detach before Claim: the old presenter's Forget must not restore
                // colliders/sorting that still belong to this approved trajectory.
                kickPlayback.Remove(target);
                if (!target.ClaimClientPresentation(replacement, target.ClientMotionRevision))
                { kickPlayback[target] = state; continue; }
                state.motion.worldMotion = true; state.motion.inputTick = 0;
                state.startInputTick = 0; state.history = null; state.approvedOnly = true;
                int queued = state.pendingArcs.Count;
                for (int i = 0; i < queued; i++)
                {
                    var motion = state.pendingArcs.Dequeue(); motion.worldMotion = true; motion.inputTick = 0;
                    state.pendingArcs.Enqueue(motion);
                }
                replacement.kickPlayback[target] = state; replacement.rejectedStarts.Remove(target);
                replacement.StartBombMotionLifetime();
                state.correction = state.presented - replacement.SamplePlayback(target, state);
                replacement.ApplyMotionCollider(target, state);
                if (GameplayLatencyDiagnostics.Enabled)
                    GameplayLatencyDiagnostics.Write(GameplayLatencyDiagnostics.For("bomb_motion_presenter_transfer",
                        replacement.GetComponent<MovementController>(), target, "approved_only;kind=" + state.motion.kind + ";queued=" + queued));
            }
            expiredKicks.Clear();
        }

        internal void PresentRetainedBombMotions(float deltaTime, float unscaledDeltaTime)
        {
            if (lastNativeBombPresentationFrame == Time.frameCount) return;
            PresentBombMotions(deltaTime, unscaledDeltaTime);
        }

        internal void ClearRetainedBombMotions() => ClearKickPlayback();
    }
}

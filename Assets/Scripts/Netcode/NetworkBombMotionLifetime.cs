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

        public static bool Retain(NetworkBombFx presenter)
        {
            if (!CanRetain(presenter) || NetworkManager.singleton is not BombermanNetworkManager manager) return false;
            var lifetime = manager.GetComponent<NetworkBombMotionLifetime>();
            if (lifetime == null) lifetime = manager.gameObject.AddComponent<NetworkBombMotionLifetime>();
            lifetime.retained.Add(presenter);
            return true;
        }

        public static void Forget(NetworkBombFx presenter)
        {
            if (NetworkManager.singleton is BombermanNetworkManager manager && manager.TryGetComponent<NetworkBombMotionLifetime>(out var lifetime))
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
            if (!NetworkBombMotionLifetime.Retain(this)) { ClearKickPlayback(); return; }
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
            NetworkBombMotionLifetime.Forget(this); ClearKickPlayback(); base.OnStopClient();
        }

        void OnDestroy()
        {
            NetworkBombMotionLifetime.Forget(this); ClearKickPlayback();
        }

        internal void PresentRetainedBombMotions(float deltaTime, float unscaledDeltaTime)
        {
            if (lastNativeBombPresentationFrame == Time.frameCount) return;
            PresentBombMotions(deltaTime, unscaledDeltaTime);
        }

        internal void ClearRetainedBombMotions() => ClearKickPlayback();
    }
}

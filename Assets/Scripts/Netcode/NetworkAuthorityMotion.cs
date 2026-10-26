using Mirror;
using UnityEngine;

namespace Assets.Scripts.Netcode
{
    public partial class NetworkPlayerInput
    {
        uint authorityMotionSequence, authorityMotionToken;
        bool ownerAuthorityPresentation;
        NetworkTransformReliable authorityPositionSync;
        public bool OwnerUsesAuthorityPresentation => ownerAuthorityPresentation;

        // A native authoritative routine marks its lifetime; this never starts it
        // on a client, chooses a destination or changes movement eligibility.
        public readonly struct AuthorityMotionScope : System.IDisposable
        {
            readonly NetworkPlayerInput input;
            readonly uint token;
            internal AuthorityMotionScope(NetworkPlayerInput input, uint token) { this.input = input; this.token = token; }
            public void Dispose()
            {
                if (input != null && input.authorityMotionToken == token)
                {
                    input.authorityMotionToken = 0;
                    if (NetworkServer.active && input.isServer && input.netId != 0) input.ServerReconcileMovementNow();
                }
            }
        }

        public static AuthorityMotionScope PresentAuthorityMotion(MovementController actor)
        {
            if (!NetSync.IsServer || actor == null || !actor.TryGetComponent<NetworkPlayerInput>(out var input) ||
                !input.isServer || input.netId == 0) return default;
            uint token = ++input.authorityMotionSequence;
            if (token == 0) token = ++input.authorityMotionSequence;
            input.authorityMotionToken = token;
            return new AuthorityMotionScope(input, token);
        }

        public static void SyncAuthorityMotionTransform(MovementController actor)
        {
            if (!NetSync.IsServer || actor == null || actor.Rigidbody == null ||
                !actor.TryGetComponent<NetworkPlayerInput>(out var input) || !input.isServer || input.netId == 0) return;
            Vector2 position = actor.Rigidbody.position;
            actor.transform.position = new Vector3(position.x, position.y, actor.transform.position.z);
        }

        void SetOwnerAuthorityPresentation(bool value)
        {
            if (ownerAuthorityPresentation == value) return;
            ownerAuthorityPresentation = value;
            GetComponent<NetworkPlayerAnimation>()?.ClearOwnerVisualHistory();
            GetComponent<NetworkMountAnimation>()?.OwnerAuthorityPresentationChanged(value);
            if (GameplayLatencyDiagnostics.Enabled)
                GameplayLatencyDiagnostics.Write(GameplayLatencyDiagnostics.For(value ? "authority_motion_begin" : "authority_motion_end",
                movement, detail: "approved_snapshots_only"));
        }

        public static Vector3 SampleApprovedAuthoritySegment(TransformSnapshot from, TransformSnapshot to, double t, float maxStep)
        {
            // A wrap/teleport or delivery outage is a discontinuity, never a trip
            // through the map. Hold the old sample until the approved cut.
            if (to.remoteTime - from.remoteTime > 0.25 || Vector3.Distance(from.position, to.position) > Mathf.Max(0.01f, maxStep))
                return t < 1 ? from.position : to.position;
            return Vector3.Lerp(from.position, to.position, Mathf.Clamp01((float)t));
        }

        void UpdateOwnerAuthorityPresentation()
        {
            if (!ownerAuthorityPresentation) return;
            if (NetSync.Mode != NetSync.NetMode.Client || !NetworkClient.active || isServer || !isLocalPlayer ||
                movement == null || movement.Rigidbody == null || movement.isDead || movement.IsEndingStage ||
                GetComponent<NetworkBombFx>()?.ClientPlayerMotionActive == true)
            { SetOwnerAuthorityPresentation(false); return; }
            if (GamePauseController.IsPaused || Blocked) return;
            authorityPositionSync = authorityPositionSync != null ? authorityPositionSync : GetComponent<NetworkTransformReliable>();
            if (authorityPositionSync == null || authorityPositionSync.clientSnapshots.Count == 0) return;
            SnapshotInterpolation.StepInterpolation(authorityPositionSync.clientSnapshots, NetworkTime.time,
                out TransformSnapshot from, out TransformSnapshot to, out double t);
            Vector3 position = SampleApprovedAuthoritySegment(from, to, t, movement.tileSize * 1.5f);
            movement.Rigidbody.position = position;
            transform.position = position;
        }

        void OnDisable()
        {
            SetOwnerAuthorityPresentation(false);
            authorityMotionToken = 0;
        }
    }
}

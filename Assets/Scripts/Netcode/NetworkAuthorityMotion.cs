using Mirror;
using UnityEngine;

namespace Assets.Scripts.Netcode
{
    public partial class NetworkPlayerInput
    {
        uint authorityMotionSequence, authorityMotionToken;
        float authorityVisualOffsetLimit = 4f;
        bool ownerAuthorityPresentation;
        NetworkTransformReliable authorityPositionSync;
        public bool OwnerUsesAuthorityPresentation => ownerAuthorityPresentation;
        public float AuthorityVisualOffsetLimit => authorityMotionToken != 0 ? authorityVisualOffsetLimit : 4f;

        BattleMode9MinecartController approvedCart;
        MovementController.NetworkMovementState cartMotorState;
        bool hasCartMotorState;
        readonly System.Collections.Generic.Queue<NetworkBombFx.PlayerBounceMotion> cartLandingBounces = new();
        public bool ApprovedCartActive => approvedCart != null;

        public bool ClaimApprovedCart(BattleMode9MinecartController cart)
        {
            if (NetSync.Mode != NetSync.NetMode.Client || !NetworkClient.active || isServer || cart == null ||
                movement == null || movement.isDead || movement.IsEndingStage || !gameObject.activeInHierarchy ||
                (approvedCart != null && approvedCart != cart)) return false;
            approvedCart = cart;
            movement.SetNetworkCartPresentation(true); movement.SetPredictLocally(false);
            if (isLocalPlayer) SetOwnerAuthorityPresentation(true);
            return true;
        }
        public bool PresentApprovedCart(BattleMode9MinecartController cart, Vector3 position)
        {
            if (!ClaimApprovedCart(cart)) return false;
            if (movement.Rigidbody != null) movement.Rigidbody.position = position;
            transform.position = position;
            return true;
        }
        public void ReleaseApprovedCart(BattleMode9MinecartController cart)
        {
            if (approvedCart != cart) return;
            approvedCart = null; movement?.SetNetworkCartPresentation(false);
            var bounces = cartLandingBounces.ToArray(); cartLandingBounces.Clear();
            var cartFx = GetComponent<NetworkBombFx>();
            if (cartFx != null && movement != null && !movement.isDead && !movement.IsEndingStage && gameObject.activeInHierarchy)
                foreach (var bounce in bounces) cartFx.ReceivePlayerBounce(bounce);
            if (cartFx?.PlayerBounceActive == true) SetOwnerAuthorityPresentation(false);
            if (isLocalPlayer && hasCartMotorState && cartMotorState.canPredict && movement != null && !movement.isDead &&
                !movement.IsEndingStage && !Blocked && !ApprovedLaunchActive && GetComponent<NetworkBombFx>()?.ClientPlayerMotionActive != true)
            {
                movement.RestoreNetworkMovementState(cartMotorState); SetOwnerAuthorityPresentation(false);
                bool complete = pending.Count == 0 || pending[0].tick == unchecked(lastLaunchAck + 1);
                movement.SetPredictLocally(complete && movement.CanPredictNetworkMovement);
                if (complete) ReplayMovementHistory();
            }
            hasCartMotorState = false;
        }
        public bool DeferBounceForApprovedCart(NetworkBombFx.PlayerBounceMotion motion)
        {
            if (!ApprovedCartActive || (!motion.moving && cartLandingBounces.Count == 0)) return false;
            if (cartLandingBounces.Count >= 32) cartLandingBounces.Dequeue();
            cartLandingBounces.Enqueue(motion); return true;
        }
        void ClearApprovedCart()
        {
            cartLandingBounces.Clear();
            approvedCart = null; hasCartMotorState = false; movement?.SetNetworkCartPresentation(false);
        }

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

        public static AuthorityMotionScope PresentAuthorityMotion(MovementController actor, float visualOffsetLimit = 4f)
        {
            if (!NetSync.IsServer || actor == null || !actor.TryGetComponent<NetworkPlayerInput>(out var input) ||
                !input.isServer || input.netId == 0) return default;
            uint token = ++input.authorityMotionSequence;
            if (token == 0) token = ++input.authorityMotionSequence;
            input.authorityMotionToken = token;
            input.authorityVisualOffsetLimit = float.IsNaN(visualOffsetLimit) || float.IsInfinity(visualOffsetLimit)
                ? 4f : Mathf.Clamp(visualOffsetLimit, 4f, 128f);
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
            if (!value) launchOwnerTimeline = false;
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
            if (!ownerAuthorityPresentation || ApprovedCartActive) return;
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
            ClearApprovedLaunchHistory();
            ClearApprovedCart();
            authorityMotionToken = 0;
        }
    }
}

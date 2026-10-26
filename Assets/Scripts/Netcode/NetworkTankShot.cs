using Mirror;
using UnityEngine;

namespace Assets.Scripts.Netcode
{
    [DefaultExecutionOrder(200)]
    [RequireComponent(typeof(NetworkIdentity), typeof(TankShot))]
    public sealed class NetworkTankShot : NetworkBehaviour
    {
        [SyncVar] Vector2 direction;
        [SyncVar] uint ownerId, inputTick;
        [SyncVar] Vector2 launchPosition, castSize;
        [SyncVar] float launchSpeed;
        [SyncVar] int launchMask;
        [SyncVar] double launchTime;
        Transform ownerVisual;
        Vector3 originalVisualLocal;
        Vector2 ownerPosition;
        float ownerLead;
        bool ownerWasPaused, awaitingResumeSample;
        double resumeAfter, resumeWaitStarted;
        NetworkBombFx ownerFx;
        readonly RaycastHit2D[] previewHits = new RaycastHit2D[16];
        public uint InputTick => inputTick;
        public bool OwnerPresentationActive => ownerVisual != null;

        public void SetLaunch(GameObject owner)
        {
            if (!NetSync.IsServer || owner == null || !owner.TryGetComponent<NetworkIdentity>(out var identity)) return;
            ownerId = identity.netId;
            inputTick = owner.TryGetComponent<NetworkPlayerInput>(out var input) ? input.CurrentActionCTick : 0;
        }

        public override void OnStartServer()
        {
            direction = GetComponent<TankShot>().Direction;
            var shot = GetComponent<TankShot>(); launchSpeed = shot.Speed; launchMask = shot.HitMask;
            launchPosition = transform.position; launchTime = NetworkTime.localTime;
            if (TryGetComponent<BoxCollider2D>(out var box)) castSize = Vector2.Scale(box.size, transform.lossyScale);
        }

        public override void OnStartClient()
        {
            if (isServer) return;
            var shot = GetComponent<TankShot>();
            shot.Init(direction, launchSpeed, 0, null);
            foreach (var collider in GetComponentsInChildren<Collider2D>(true)) collider.enabled = false;
            foreach (var body in GetComponentsInChildren<Rigidbody2D>(true)) body.simulated = false;
            if (NetworkClient.spawned.TryGetValue(ownerId, out var owner) && owner.isLocalPlayer && owner.TryGetComponent<NetworkBombFx>(out ownerFx))
                ownerFx.ReceiveTankShotSpawn(this);
        }

        public bool AdoptOwnerPresentation(Vector2 position)
        {
            if (isServer || NetSync.Mode != NetSync.NetMode.Client || !NetworkClient.active || ownerFx == null || !ownerFx.isLocalPlayer) return false;
            var sprite = GetComponentInChildren<SpriteRenderer>(true);
            if (sprite == null || sprite.transform == transform) return false;
            ownerVisual = sprite.transform; originalVisualLocal = ownerVisual.localPosition;
            ownerPosition = position;
            ownerWasPaused = awaitingResumeSample = false;
            ownerLead = Mathf.Clamp(Vector2.Dot(position - launchPosition, direction) / Mathf.Max(0.1f, launchSpeed) -
                Mathf.Max(0, (float)(NetworkBombFx.KickServerNow - launchTime)), 0, 0.15f);
            ApplyOwnerPosition(); return true;
        }

        void LateUpdate()
        {
            if (ownerVisual == null) return;
            if (GamePauseController.IsPaused) { ownerWasPaused = true; ApplyOwnerPosition(); return; }
            if (ownerWasPaused)
            {
                ownerWasPaused = false; awaitingResumeSample = true;
                resumeAfter = NetworkBombFx.KickServerNow; resumeWaitStarted = Time.realtimeSinceStartupAsDouble;
            }
            Vector2 approved = launchPosition; double time = launchTime;
            if (TryGetComponent<NetworkTransformReliable>(out var sync) && sync.clientSnapshots.Count > 0)
            {
                var snapshot = sync.clientSnapshots.Values[sync.clientSnapshots.Count - 1]; approved = snapshot.position; time = snapshot.remoteTime;
            }
            if (awaitingResumeSample)
            {
                if (time < resumeAfter)
                {
                    if (Time.realtimeSinceStartupAsDouble - resumeWaitStarted > 1) ReleaseOwnerPresentation();
                    else ApplyOwnerPosition();
                    return;
                }
                awaitingResumeSample = false;
            }
            float age = Mathf.Max(0, (float)(NetworkBombFx.KickServerNow - time));
            if (age > 1) { ReleaseOwnerPresentation(); return; }
            float lead = Mathf.Min(launchSpeed * Mathf.Clamp(age + ownerLead, 0, 0.15f), 2);
            Vector2 expected = approved + direction * lead;
            Vector2 next = ownerPosition + direction * (launchSpeed * Time.deltaTime);
            next = Vector2.MoveTowards(next, expected, launchSpeed * Time.deltaTime * 0.5f);
            if (Vector2.Dot(next - approved, direction) > lead) next = expected;
            ownerPosition = TankShot.PreviewNetworkTravel(ownerPosition, next, castSize, launchMask, ownerFx != null ? ownerFx.gameObject : null, previewHits);
            ApplyOwnerPosition();
        }

        void ApplyOwnerPosition()
        {
            if (ownerVisual != null) ownerVisual.position = new Vector3(ownerPosition.x, ownerPosition.y, transform.position.z) + transform.TransformVector(originalVisualLocal);
        }

        public void ReleaseOwnerPresentation()
        {
            if (ownerVisual != null) ownerVisual.localPosition = originalVisualLocal;
            ownerVisual = null;
        }

        public override void OnStopClient()
        {
            ReleaseOwnerPresentation(); ownerFx?.TankShotEnded(this); ownerFx = null; base.OnStopClient();
        }
    }
}

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
        Transform observerVisual;
        Vector3 observerLocal;
        Vector2 observerPosition;
        bool observerPaused, observerAwaitingResume;
        double observerTime, observerLatestTime, observerResumeAfter;
        readonly RaycastHit2D[] previewHits = new RaycastHit2D[16];
        public uint InputTick => inputTick;
        public bool OwnerPresentationActive => ownerVisual != null;
        internal bool ObserverPresentationActive => observerVisual != null;
        internal double ObserverPresentationTime => observerTime;
        internal double ObserverLatestTime => observerLatestTime;

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
            ReleaseObserverPresentation();
            ownerVisual = sprite.transform; originalVisualLocal = ownerVisual.localPosition;
            ownerPosition = position;
            ownerWasPaused = awaitingResumeSample = false;
            ownerLead = Mathf.Clamp(Vector2.Dot(position - launchPosition, direction) / Mathf.Max(0.1f, launchSpeed) -
                Mathf.Max(0, (float)(NetworkBombFx.KickServerNow - launchTime)), 0, 0.15f);
            ApplyOwnerPosition(); return true;
        }

        void LateUpdate()
        {
            if (ownerVisual == null) { PresentApprovedObserver(); return; }
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

        void PresentApprovedObserver()
        {
            if (isServer || NetSync.Mode != NetSync.NetMode.Client || !NetworkClient.active || launchSpeed <= 0 ||
                BombermanNetworkManager.CoopR05Legacy) return;
            bool first = observerVisual == null;
            if (first)
            {
                var sprite = GetComponentInChildren<SpriteRenderer>(true);
                if (sprite == null || sprite.transform == transform) return;
                observerVisual = sprite.transform; observerLocal = observerVisual.localPosition;
                observerPosition = launchPosition; observerTime = launchTime;
            }
            if (GamePauseController.IsPaused)
            {
                observerPaused = true; ApplyObserverPosition(); return;
            }
            double now = NetworkBombFx.KickServerNow;
            if (observerPaused)
            {
                observerPaused = false; observerAwaitingResume = true; observerResumeAfter = now;
            }
            Vector2 approved = launchPosition; double approvedTime = launchTime;
            if (TryGetComponent<NetworkTransformReliable>(out var sync) && sync.clientSnapshots.Count > 0)
            {
                var sample = sync.clientSnapshots.Values[sync.clientSnapshots.Count - 1];
                approved = sample.position;
                // Mirror shifts transform timestamps for buffering. They are not the
                // actual authority time at which this projectile occupied the point.
                approvedTime = sample.remoteTime - BombermanNetworkManager.TransformVisualOffset(netIdentity);
            }
            observerLatestTime = approvedTime;
            if (observerAwaitingResume && observerLatestTime < observerResumeAfter) { ApplyObserverPosition(); return; }
            observerAwaitingResume = false;
            float lead = launchSpeed * Mathf.Clamp((float)(System.Math.Max(observerTime, now)-approvedTime), 0, Mathf.Min(.15f, 1f/launchSpeed));
            Vector2 expected = TankShot.PreviewNetworkTravel(approved, approved + direction * lead, castSize, launchMask,
                NetworkClient.spawned.TryGetValue(ownerId, out var owner) ? owner.gameObject : null, previewHits);
            observerPosition = first ? expected : Vector2.MoveTowards(observerPosition, expected,
                launchSpeed * Mathf.Clamp(Time.unscaledDeltaTime, 0, .05f) * 1.5f);
            observerTime = approvedTime + Vector2.Dot(observerPosition-approved, direction)/launchSpeed;
            ApplyObserverPosition();
        }

        void ApplyObserverPosition()
        {
            if (observerVisual != null) observerVisual.position = new Vector3(observerPosition.x, observerPosition.y, transform.position.z) + transform.TransformVector(observerLocal);
        }

        void ReleaseObserverPresentation()
        {
            if (observerVisual != null) observerVisual.localPosition = observerLocal;
            observerVisual = null; observerPaused = observerAwaitingResume = false;
        }

        void OnDisable() => ReleaseObserverPresentation();

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
            ReleaseObserverPresentation();
            ReleaseOwnerPresentation(); ownerFx?.TankShotEnded(this); ownerFx = null; base.OnStopClient();
        }
    }
}

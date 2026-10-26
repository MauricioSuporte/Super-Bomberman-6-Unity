using Mirror;
using UnityEngine;

namespace Assets.Scripts.Netcode
{
    public partial class NetworkBombFx
    {
        bool hasMountActionState, tankReady, tankHasAction, tankPending, tankSoundPlayed, tankLocksMovement;
        uint tankTick, receivedTankTick;
        float tankAge, tankDuration, tankNextAllowed;
        TankMountShootAbility.NetworkShotPreview tankPreview;
        SpriteRenderer tankProxy;
        Sprite[] tankFrames;
        float tankFrameTime;
        Vector2 tankPosition;
        TankMountShootAnimator tankAnimator;
        NetworkTankShot tankReplica;
        readonly RaycastHit2D[] tankPreviewHits = new RaycastHit2D[16];
        public bool TankShotPoseActive => tankHasAction && tankAge < tankDuration;
        bool TankShotPoseLocksMovement => TankShotPoseActive && tankLocksMovement;

        public bool TryPredictTankShot(int held, uint tick)
        {
            if (NetSync.Mode != NetSync.NetMode.Client || !NetworkClient.active || isServer || !isLocalPlayer ||
                !hasMountActionState || !tankReady || tankPending || Time.time < tankNextAllowed || AbilityPredictionLocksMovement || ClientPlayerMotionActive ||
                GamePauseController.IsPaused || BombermanNetworkManager.BattleStartPending ||
                !TryGetComponent<MovementController>(out var movement) || !movement.CanPredictNetworkMovement ||
                (TryGetComponent<NetworkPlayerInput>(out var input) && !input.ServerAllowsMovementPrediction) ||
                !TryGetComponent<PlayerMountCompanion>(out var companion) || companion.GetMountedLouieType() != MountedType.Tank) return false;
            var ability = GetComponent<TankMountShootAbility>() ?? gameObject.AddComponent<TankMountShootAbility>();
            if (!ability.TryPreviewNetworkShot(MotionDiagnosticProbe.DirectionFromMask(held), out var preview)) return false;
            return BeginTankShotPrediction(tick, preview);
        }

        public bool BeginTankShotPrediction(uint tick, TankMountShootAbility.NetworkShotPreview preview)
        {
            if (NetSync.Mode != NetSync.NetMode.Client || !NetworkClient.active || isServer || !isLocalPlayer || tankPending || preview.prefab == null ||
                !TryGetComponent<MovementController>(out var movement) || !movement.CanPredictNetworkMovement ||
                (TryGetComponent<CharacterHealth>(out var health) && health.life <= 0) || ClientPlayerMotionActive || GamePauseController.IsPaused) return false;
            var source = preview.prefab.GetComponentInChildren<SpriteRenderer>(true);
            var animation = source != null ? source.GetComponent<AnimatedSpriteRenderer>() : null;
            Sprite sprite = source != null && source.sprite != null ? source.sprite :
                animation != null && animation.animationSprite != null && animation.animationSprite.Length > 0 ? animation.animationSprite[0] : null;
            if (source == null || sprite == null) return false;
            ClearTankPrediction("new_shot");
            tankHasAction = tankPending = true; tankTick = tick; tankAge = 0; tankPreview = preview;
            tankDuration = preview.poseDuration; tankLocksMovement = preview.locksMovement;
            tankPosition = preview.start; tankNextAllowed = Time.time + preview.poseDuration + preview.cooldown;
            tankFrames = animation != null ? animation.animationSprite : null;
            tankFrameTime = animation != null ? Mathf.Max(0.01f, animation.animationTime) : 0.1f;
            tankProxy = new GameObject("PredictedTankShot", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            tankProxy.sprite = sprite; tankProxy.sharedMaterial = source.sharedMaterial; tankProxy.color = source.color;
            tankProxy.sortingLayerID = source.sortingLayerID; tankProxy.sortingOrder = Mathf.Max(3, source.sortingOrder);
            tankProxy.flipX = preview.direction == Vector2.right; tankProxy.transform.localScale = source.transform.lossyScale;
            tankProxy.transform.position = preview.start;
            tankSoundPlayed = PlayMountAbilitySound(MountedType.Tank); PresentTankPose();
            TraceTankPrediction("tank_prediction_begin", "visual_only"); return true;
        }

        [Server]
        public void ServerTankShot(GameObject projectile, Vector2 direction, float duration, float cooldown)
        {
            if (projectile == null || !projectile.TryGetComponent<NetworkIdentity>(out var identity) || identity.netId == 0) return;
            RpcTankShot(TryGetComponent<NetworkPlayerInput>(out var input) ? input.CurrentActionCTick : 0,
                identity.netId, direction, duration, cooldown, NetworkTime.localTime);
        }

        [ClientRpc]
        void RpcTankShot(uint tick, uint shotId, Vector2 direction, float duration, float cooldown, double serverTime)
            => ReceiveTankShot(tick, shotId, direction, duration, cooldown, serverTime);

        public void ReceiveTankShot(uint tick, uint shotId, Vector2 direction, float duration, float cooldown, double serverTime)
        {
            if (NetSync.Mode != NetSync.NetMode.Client || isServer || !NetworkClient.active || !NetworkInputHistory.IsNewer(tick, receivedTankTick)) return;
            receivedTankTick = tick;
            bool predicted = isLocalPlayer && tankHasAction && tankTick == tick;
            if (!predicted || !tankSoundPlayed) PlayMountAbilitySound(MountedType.Tank);
            if (!isLocalPlayer) return;
            if (!TryGetComponent<MovementController>(out var movement) || movement.isDead || movement.IsEndingStage ||
                (TryGetComponent<CharacterHealth>(out var health) && health.life <= 0)) { ClearTankPrediction("late_receipt_after_death"); return; }
            float age = Mathf.Max(0, (float)(KickServerNow - serverTime));
            if (tankHasAction && NetworkInputHistory.IsNewer(tankTick, tick)) return;
            if (!predicted)
            {
                ClearTankPrediction("authority_sequence"); tankHasAction = true; tankTick = tick; tankAge = age;
                tankPreview.direction = direction; tankLocksMovement = false;
            }
            tankDuration = duration; tankPending = false; tankSoundPlayed = true;
            tankNextAllowed = Mathf.Max(tankNextAllowed, Time.time + Mathf.Max(0, duration + cooldown - age));
            if (NetworkClient.spawned.TryGetValue(shotId, out var identity) && identity.TryGetComponent<NetworkTankShot>(out var shot)) ReceiveTankShotSpawn(shot);
            TraceTankPrediction("tank_prediction_confirm", "server_spawn");
        }

        public void ReceiveTankShotSpawn(NetworkTankShot shot)
        {
            if (shot == null || !isLocalPlayer || !tankHasAction || shot.InputTick != tankTick) return;
            if (tankReplica == shot) return;
            Vector2 position = tankProxy != null ? tankPosition : (Vector2)shot.transform.position;
            if (!shot.AdoptOwnerPresentation(position)) return;
            tankReplica = shot; tankPending = false; ReleaseTankProxy();
        }

        public void ReconcileTankShot(uint acknowledgedTick, MountActionState state)
        {
            hasMountActionState = true; tankReady = state.tankReady;
            if (tankHasAction && NetworkInputHistory.IsNewer(tankTick, acknowledgedTick)) return;
            if (tankPending) ClearTankPrediction("input_processed_without_shot");
            tankNextAllowed = state.tankReady ? Time.time : Mathf.Max(tankNextAllowed, Time.time + state.tankCooldown);
            if (TryGetComponent<PlayerMountCompanion>(out var companion) && companion.GetMountedLouieType() == MountedType.Tank)
            {
                var ability = GetComponent<TankMountShootAbility>() ?? gameObject.AddComponent<TankMountShootAbility>();
                ability.PresentNetworkCooldown(Mathf.Max(0, tankNextAllowed - Time.time));
            }
        }

        void UpdateTankPrediction(float deltaTime)
        {
            if (!tankHasAction) return;
            if (!TankShotPoseActive && !tankPending && tankProxy == null && tankReplica == null) return;
            if (!TryGetComponent<MovementController>(out var movement) || movement.isDead || movement.IsEndingStage ||
                (TryGetComponent<CharacterHealth>(out var health) && health.life <= 0) ||
                !TryGetComponent<PlayerMountCompanion>(out var companion) || companion.GetMountedLouieType() != MountedType.Tank || ClientPlayerMotionActive)
            { ClearTankPrediction("interrupted"); return; }
            if (GamePauseController.IsPaused) return;
            float previous = tankAge; tankAge += deltaTime;
            if (tankProxy != null)
            {
                float travel = Mathf.Max(0, Mathf.Min(tankAge, 0.15f) - Mathf.Min(previous, 0.15f)) * tankPreview.speed;
                tankPosition = TankShot.PreviewNetworkTravel(tankPosition, tankPosition + tankPreview.direction * travel,
                    tankPreview.castSize, tankPreview.mask, gameObject, tankPreviewHits);
                tankProxy.transform.position = tankPosition;
                if (tankFrames != null && tankFrames.Length > 0) tankProxy.sprite = tankFrames[Mathf.FloorToInt(tankAge / tankFrameTime) % tankFrames.Length];
            }
            if (tankAge >= tankDuration && tankAnimator != null) { tankAnimator.Stop(); tankAnimator = null; }
            if ((tankPending || tankProxy != null) && tankAge > Mathf.Clamp((float)NetworkTime.rtt * 3, 0.5f, 1)) ClearTankPrediction("confirmation_timeout");
            else PresentTankPose();
        }

        void PresentTankPose(bool restart = false)
        {
            if (!TankShotPoseActive || !TryGetComponent<PlayerMountCompanion>(out var companion)) return;
            var mount = companion.GetMountedLouieObject();
            var visual = mount != null ? mount.GetComponentInChildren<TankMountShootAnimator>(true) : null;
            if (restart || visual != tankAnimator) { tankAnimator?.Stop(); tankAnimator = visual; tankAnimator?.Play(tankPreview.direction); }
            tankAnimator?.PresentPredictedProgress(tankAge / Mathf.Max(0.01f, tankDuration));
        }

        public void TankShotEnded(NetworkTankShot shot) { if (tankReplica == shot) tankReplica = null; }
        void ReleaseTankProxy() { if (tankProxy != null) ReleaseTileVisual(tankProxy.gameObject); tankProxy = null; }
        void ClearTankPrediction(string reason)
        {
            if (tankHasAction) TraceTankPrediction("tank_prediction_end", reason);
            ReleaseTankProxy(); tankReplica?.ReleaseOwnerPresentation(); tankReplica = null;
            tankAnimator?.Stop(); tankAnimator = null; tankPending = tankHasAction = tankSoundPlayed = false;
        }
        void TraceTankPrediction(string kind, string reason)
        {
            if (!GameplayLatencyDiagnostics.Enabled) return;
            var entry = GameplayLatencyDiagnostics.For(kind, GetComponent<MovementController>(), detail: reason);
            entry.inputTick = tankTick; entry.durationMs = tankAge * 1000; GameplayLatencyDiagnostics.Write(entry);
        }
    }
}

using Mirror;
using UnityEngine;

namespace Assets.Scripts.Netcode
{
    public partial class NetworkBombFx
    {
        bool greenDashActive, greenDashConfirmed, greenDashSoundPlayed, serverGreenDashActive;
        uint greenDashTick, serverGreenDashTick, serverGreenDashRevision, receivedGreenDashRevision;
        float greenDashSpeed, greenDashAge, greenDashSilence;
        Vector2 greenDashDirection, greenDashPosition, greenDashReplica, greenDashCorrection;
        GreenLouieDashAnimator greenDashVisual;
        public bool GreenDashActive => greenDashActive;
        public bool ClientPlayerMotionActive => PlayerBounceActive || GreenDashActive;

        public bool TryPredictGreenDash(int held, uint tick)
        {
            if (NetSync.Mode != NetSync.NetMode.Client || !NetworkClient.active || !isLocalPlayer || isServer) return false;
            if (!TryGetComponent<PlayerMountCompanion>(out var companion) || companion.GetMountedLouieType() != MountedType.Green) return false;
            var movement = GetComponent<MovementController>();
            if (movement == null) return false;
            Vector2 direction = MotionDiagnosticProbe.DirectionFromMask(held);
            if (direction == Vector2.zero) direction = movement.FacingDirection;
            direction = Mathf.Abs(direction.x) > Mathf.Abs(direction.y) ? new Vector2(Mathf.Sign(direction.x), 0) : new Vector2(0, Mathf.Sign(direction.y));
            var ability = GetComponent<GreenLouieDashAbility>() ?? gameObject.AddComponent<GreenLouieDashAbility>();
            return BeginGreenDashPrediction(tick, direction, movement.speed * ability.dashSpeedMultiplier, movement.transform.position);
        }

        public bool BeginGreenDashPrediction(uint tick, Vector2 direction, float speed, Vector2 position, bool confirmed = false)
        {
            var movement = GetComponent<MovementController>();
            if (NetSync.Mode != NetSync.NetMode.Client || !NetworkClient.active || !isLocalPlayer || isServer ||
                greenDashActive || PlayerBounceActive || GamePauseController.IsPaused || BombermanNetworkManager.BattleStartPending ||
                movement == null || movement.isDead || movement.IsEndingStage || direction == Vector2.zero || speed <= 0 ||
                (!confirmed && (!movement.CanPredictNetworkMovement || AbilityPredictionLocksMovement))) return false;
            var ability = GetComponent<GreenLouieDashAbility>() ?? gameObject.AddComponent<GreenLouieDashAbility>();
            if (!confirmed && !ability.TryPreviewNetworkDashStep(position, direction, speed * Time.fixedDeltaTime, out _)) return false;
            ResetLocalAbilityPresentation();
            greenDashTick = tick; greenDashActive = true; greenDashConfirmed = confirmed; greenDashDirection = direction;
            greenDashPosition = greenDashReplica = position; greenDashSpeed = speed; greenDashAge = greenDashSilence = 0;
            greenDashCorrection = Vector2.zero;
            movement.SetPredictLocally(false);
            greenDashSoundPlayed = PlayGreenDashSound();
            TraceGreenDash("player_dash_prediction_begin", "bounded_local_start");
            RestoreGreenDashPosition(); PresentGreenDash(0);
            return true;
        }

        [Server]
        public void ServerGreenDash(bool active, Vector2 direction, float speed, Vector2 position)
        {
            if (netId == 0 || (!active && !serverGreenDashActive)) return;
            if (active)
            {
                serverGreenDashTick = TryGetComponent<NetworkPlayerInput>(out var input) ? input.CurrentActionCTick : 0;
                PlayGreenDashSound();
            }
            serverGreenDashActive = active;
            RpcGreenDash(++serverGreenDashRevision, serverGreenDashTick, active, direction, speed, position, NetworkTime.localTime);
        }

        [ClientRpc]
        void RpcGreenDash(uint revision, uint tick, bool active, Vector2 direction, float speed, Vector2 position, double time)
        {
            if (isServer || !NetworkInputHistory.IsNewer(revision, receivedGreenDashRevision)) return;
            receivedGreenDashRevision = revision;
            if (!isLocalPlayer) { if (active) PlayGreenDashSound(); return; }
            ReceiveGreenDash(tick, active, direction, speed, position, time);
        }

        public void ReceiveGreenDash(uint tick, bool active, Vector2 direction, float speed, Vector2 position, double time)
        {
            if (NetSync.Mode != NetSync.NetMode.Client || !isLocalPlayer || isServer || !NetworkClient.active) return;
            if (!active)
            {
                if (!greenDashActive || tick != greenDashTick) return;
                greenDashPosition = position; RestoreGreenDashPosition(); ClearGreenDash("server_stop"); return;
            }
            if (greenDashActive && greenDashTick != tick) ClearGreenDash("new_action");
            bool wasPredicted = greenDashActive;
            if (!greenDashActive && !BeginGreenDashPrediction(tick, direction, speed, position, true)) return;
            greenDashConfirmed = true; greenDashDirection = direction; greenDashSpeed = speed; greenDashSilence = 0;
            if (wasPredicted && !greenDashSoundPlayed) greenDashSoundPlayed = PlayGreenDashSound();
            TraceGreenDash("player_dash_prediction_confirm", "authority_start");
            // First confirmation preserves the owner's ahead-of-server timeline.
            if (!wasPredicted) CorrectGreenDash(position, time);
        }

        public void ReconcileGreenDash(uint acknowledgedTick, MovementController.NetworkMovementState state)
        {
            greenDashReplica = state.position;
            if (!greenDashActive || NetworkInputHistory.IsNewer(greenDashTick, acknowledgedTick)) return;
            if (!greenDashConfirmed) { ClearGreenDash("input_processed_without_dash"); return; }
            if (state.canPredict || state.mount != MountedType.Green) { ClearGreenDash("authority_released_motion"); return; }
            greenDashSilence = 0;
            CorrectGreenDash(state.position, state.serverTime);
        }

        void CorrectGreenDash(Vector2 position, double serverTime)
        {
            var ability = GetComponent<GreenLouieDashAbility>();
            float lead = Mathf.Clamp((float)(KickServerNow - serverTime + NetworkTime.rtt * 0.5), 0, 0.15f);
            Vector2 target = position;
            if (ability != null) ability.TryPreviewNetworkDashStep(position, greenDashDirection, greenDashSpeed * lead, out target);
            Vector2 error = target - greenDashPosition;
            float tile = GetComponent<MovementController>().tileSize;
            if (error.magnitude > Mathf.Max(0.1f, tile)) { greenDashPosition = target; greenDashCorrection = Vector2.zero; }
            else greenDashCorrection = error;
        }

        void UpdateGreenDash(float deltaTime, float unscaledDeltaTime)
        {
            if (!greenDashActive) return;
            var movement = GetComponent<MovementController>();
            if (movement == null || movement.isDead || movement.IsEndingStage) { ClearGreenDash("dead_or_ending"); return; }
            if (GamePauseController.IsPaused) { PresentGreenDash(0); return; }
            float previousAge = greenDashAge, previousSilence = greenDashSilence;
            greenDashAge += unscaledDeltaTime; greenDashSilence += unscaledDeltaTime;
            if ((!greenDashConfirmed && greenDashAge > Mathf.Clamp((float)NetworkTime.rtt * 3, 0.5f, 1)) ||
                (greenDashConfirmed && greenDashSilence > 0.5f))
            { ClearGreenDash("confirmation_timeout"); if (movement.TryGetComponent<Rigidbody2D>(out var replicaBody)) replicaBody.position = greenDashReplica; movement.transform.position = greenDashReplica; return; }
            float allowedDelta = Mathf.Min(deltaTime, Mathf.Max(0, 0.15f - (greenDashConfirmed ? previousSilence : previousAge)));
            var ability = GetComponent<GreenLouieDashAbility>();
            if (ability != null && ability.TryPreviewNetworkDashStep(greenDashPosition, greenDashDirection, greenDashSpeed * allowedDelta, out var next))
                greenDashPosition = next;
            Vector2 remaining = Vector2.MoveTowards(greenDashCorrection, Vector2.zero, greenDashSpeed * deltaTime * 0.5f);
            greenDashPosition += greenDashCorrection - remaining; greenDashCorrection = remaining;
            RestoreGreenDashPosition(); PresentGreenDash(deltaTime);
        }

        public void RestoreGreenDashPosition()
        {
            if (!greenDashActive) return;
            if (TryGetComponent<Rigidbody2D>(out var body)) body.position = greenDashPosition;
            transform.position = new Vector3(greenDashPosition.x, greenDashPosition.y, transform.position.z);
        }

        void PresentGreenDash(float deltaTime)
        {
            if (!TryGetComponent<PlayerMountCompanion>(out var companion)) return;
            var mount = companion.GetMountedLouieObject();
            var visual = mount != null ? mount.GetComponentInChildren<GreenLouieDashAnimator>(true) : null;
            if (visual != greenDashVisual) { greenDashVisual = visual; }
            greenDashVisual?.PresentPredictedDash(greenDashDirection, deltaTime);
        }

        void ClearGreenDash(string reason)
        {
            if (!greenDashActive) return;
            TraceGreenDash(reason == "input_processed_without_dash" || reason == "confirmation_timeout" ? "player_dash_prediction_reject" : "player_dash_end", reason);
            greenDashActive = false; greenDashConfirmed = false; greenDashCorrection = Vector2.zero;
            if (greenDashVisual != null)
            {
                if (TryGetComponent<MovementController>(out var movement) && movement.isDead) greenDashVisual.CancelForDeath();
                else greenDashVisual.Stop();
            }
            greenDashVisual = null;
        }

        bool PlayGreenDashSound()
        {
            if (!TryGetComponent<PlayerMountCompanion>(out var companion)) return false;
            var mount = companion.GetMountedLouieObject() ?? companion.GetMountPrefabForType(MountedType.Green);
            var config = mount != null ? mount.GetComponentInChildren<MountAbilitySfxConfig>(true) : null;
            if (config == null || config.abilitySfx == null || !TryGetComponent<AudioSource>(out var audio)) return false;
            audio.PlayOneShot(config.abilitySfx, GameAudioSettings.ApplySfxVolume(config.abilityVolume)); return true;
        }

        void TraceGreenDash(string kind, string reason)
        {
            if (!GameplayLatencyDiagnostics.Enabled) return;
            var entry = GameplayLatencyDiagnostics.For(kind, GetComponent<MovementController>(), detail: reason);
            entry.inputTick = greenDashTick; entry.durationMs = greenDashAge * 1000; entry.delta = greenDashCorrection;
            GameplayLatencyDiagnostics.Write(entry);
        }
    }
}

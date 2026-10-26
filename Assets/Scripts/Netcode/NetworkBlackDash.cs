using Mirror;
using UnityEngine;

namespace Assets.Scripts.Netcode
{
    public partial class NetworkBombFx
    {
        public struct BlackDashMotion
        {
            public uint revision, inputTick;
            public Vector2 start, direction, position;
            public float distance, duration, cooldown;
            public double serverTime;
            public bool active, holding, first;
        }
        BlackDashMotion blackMotion, serverBlackMotion;
        bool blackActive, blackConfirmed, blackHolding, blackEnding, blackSoundPlayed, serverBlackActive;
        bool hasBlackActionState, blackEligible;
        uint serverBlackRevision, receivedBlackRevision;
        float blackAge, blackSilence, blackElapsed, blackNextAllowed;
        Vector2 blackPosition, blackReplica, blackCorrection;
        BlackLouieDashAnimator blackVisual;
        public bool BlackDashActive => blackActive;
        public bool BlackDashOwnsPose => blackActive && !blackEnding;
        public bool ServerHasBlackDash => serverBlackActive;

        public bool TryPredictBlackDash(int held, uint tick)
        {
            if (NetSync.Mode != NetSync.NetMode.Client || !NetworkClient.active || isServer || !isLocalPlayer ||
                !hasBlackActionState || !blackEligible || Time.time < blackNextAllowed ||
                !TryGetComponent<PlayerMountCompanion>(out var mount) || mount.GetMountedLouieType() != MountedType.Black ||
                (TryGetComponent<NetworkPlayerInput>(out var input) && !input.ServerAllowsMovementPrediction)) return false;
            var movement = GetComponent<MovementController>();
            if (movement == null) return false;
            Vector2 direction = MotionDiagnosticProbe.DirectionFromMask(held);
            if (direction == Vector2.zero) direction = movement.FacingDirection;
            direction = Mathf.Abs(direction.x) > Mathf.Abs(direction.y) ? new Vector2(Mathf.Sign(direction.x), 0) : new Vector2(0, Mathf.Sign(direction.y));
            var ability = GetComponent<BlackLouieDashPushAbility>() ?? gameObject.AddComponent<BlackLouieDashPushAbility>();
            return BeginBlackDashPrediction(new BlackDashMotion { inputTick = tick, start = movement.transform.position,
                position = movement.transform.position, direction = direction, duration = Mathf.Max(0.01f, ability.dashMoveSeconds),
                distance = Mathf.Max(1, ability.dashTiles) * movement.tileSize, cooldown = Mathf.Max(0.01f, ability.dashCooldownSeconds), active = true });
        }

        public bool BeginBlackDashPrediction(BlackDashMotion motion, bool confirmed = false)
        {
            var movement = GetComponent<MovementController>();
            if (NetSync.Mode != NetSync.NetMode.Client || !NetworkClient.active || !isLocalPlayer || isServer || blackActive ||
                movement == null || movement.isDead || movement.IsEndingStage || motion.direction == Vector2.zero || motion.distance <= 0 ||
                (TryGetComponent<CharacterHealth>(out var health) && health.life <= 0) ||
                (!confirmed && (ClientPlayerMotionActive || AbilityPredictionLocksMovement || !movement.CanPredictNetworkMovement || GamePauseController.IsPaused || BombermanNetworkManager.BattleStartPending))) return false;
            var ability = GetComponent<BlackLouieDashPushAbility>() ?? gameObject.AddComponent<BlackLouieDashPushAbility>();
            motion.direction = Mathf.Abs(motion.direction.x) > Mathf.Abs(motion.direction.y) ? new Vector2(Mathf.Sign(motion.direction.x), 0) : new Vector2(0, Mathf.Sign(motion.direction.y));
            motion.duration = Mathf.Max(0.01f, motion.duration);
            if (!confirmed && !ability.TryPreviewNetworkDashStep(motion.start, motion.direction, 0.05f, movement.AcknowledgedDestructiblePass, out _)) return false;
            if (confirmed) { ClearGreenDash("black_dash"); ClearPinkJump("black_dash"); ClearPlayerBouncePresentation(); }
            ResetLocalAbilityPresentation();
            blackMotion = motion; blackActive = true; blackConfirmed = confirmed; blackHolding = motion.holding; blackEnding = false;
            blackPosition = blackReplica = motion.position; blackCorrection = Vector2.zero;
            blackAge = blackSilence = 0; blackElapsed = confirmed ? Mathf.Max(0, (float)(KickServerNow - motion.serverTime)) : 0;
            blackNextAllowed = Time.time + Mathf.Max(0, motion.cooldown - (confirmed ? Mathf.Max(0, (float)(NetworkTime.predictedTime - motion.serverTime)) : 0));
            movement.SetPredictLocally(false);
            blackSoundPlayed = PlayMountAbilitySound(MountedType.Black);
            RestoreBlackDashPosition(); PresentBlackDash(); TraceBlackDash("black_dash_prediction_begin", confirmed ? "authority" : "visual_only");
            return true;
        }

        [Server]
        public void ServerBlackDashStart(Vector2 position, Vector2 direction, float distance, float duration, float cooldown)
        {
            if (netId == 0) return;
            serverBlackActive = true;
            serverBlackMotion = new BlackDashMotion { inputTick = TryGetComponent<NetworkPlayerInput>(out var input) ? input.CurrentActionCTick : 0,
                start = position, position = position, direction = direction, distance = distance, duration = Mathf.Max(0.01f, duration),
                cooldown = cooldown, serverTime = NetworkTime.localTime, active = true, first = true, revision = ++serverBlackRevision };
            PlayMountAbilitySound(MountedType.Black); RpcBlackDash(serverBlackMotion);
        }

        [Server]
        public void ServerBlackDashHold(Vector2 position)
        {
            if (!serverBlackActive || netId == 0) return;
            serverBlackMotion.position = position; serverBlackMotion.holding = true; serverBlackMotion.first = false;
            serverBlackMotion.revision = ++serverBlackRevision; RpcBlackDash(serverBlackMotion);
        }

        [Server]
        public void ServerBlackDashStop(Vector2 position)
        {
            if (!serverBlackActive || netId == 0) return;
            serverBlackActive = false; serverBlackMotion.active = false; serverBlackMotion.first = false;
            serverBlackMotion.position = position; serverBlackMotion.revision = ++serverBlackRevision; RpcBlackDash(serverBlackMotion);
        }

        [ClientRpc]
        void RpcBlackDash(BlackDashMotion motion) => ReceiveBlackDash(motion);

        public void ReceiveBlackDash(BlackDashMotion motion)
        {
            if (NetSync.Mode != NetSync.NetMode.Client || isServer || !NetworkClient.active || !NetworkInputHistory.IsNewer(motion.revision, receivedBlackRevision)) return;
            receivedBlackRevision = motion.revision;
            if (!isLocalPlayer) { if (motion.active && motion.first) PlayMountAbilitySound(MountedType.Black); return; }
            if (!motion.active)
            {
                if (!blackActive || motion.inputTick != blackMotion.inputTick) return;
                blackPosition = motion.position; blackEnding = true; blackSilence = 0;
                RestoreBlackDashPosition(); StopBlackVisual(); return; // Keep root ownership until the post-dash movement snapshot.
            }
            bool predicted = blackActive && blackMotion.inputTick == motion.inputTick;
            if (blackActive && !predicted) ClearBlackDash("new_sequence");
            if (!blackActive && !BeginBlackDashPrediction(motion, true)) return;
            blackMotion = motion; blackConfirmed = true; blackSilence = 0;
            blackHolding = motion.holding;
            if (motion.holding) { blackPosition = motion.position; blackCorrection = Vector2.zero; RestoreBlackDashPosition(); }
            else if (!predicted) CorrectBlackDash(motion.position, motion.serverTime);
            if (!blackSoundPlayed) blackSoundPlayed = PlayMountAbilitySound(MountedType.Black);
            TraceBlackDash("black_dash_prediction_confirm", motion.holding ? "authority_impact" : "authority_start");
        }

        public void ReconcileBlackDash(uint tick, MovementController.NetworkMovementState state, MountActionState actions)
        {
            hasBlackActionState = true; blackEligible = actions.blackEligible; blackReplica = state.position;
            if (blackActive && NetworkInputHistory.IsNewer(blackMotion.inputTick, tick)) return;
            float projectedRemaining = Mathf.Max(0, actions.blackCooldown - Mathf.Max(0, (float)(NetworkTime.predictedTime - state.serverTime)));
            blackNextAllowed = actions.blackCooldown <= 0 && actions.blackEligible ? Time.time : Mathf.Max(blackNextAllowed, Time.time + projectedRemaining);
            if (!blackActive) return;
            if (!blackConfirmed) { ClearBlackDash("input_processed_without_dash"); return; }
            if (state.canPredict || state.mount != MountedType.Black) { ClearBlackDash("authority_released_motion"); return; }
            blackSilence = 0;
            if (!blackHolding && !blackEnding) CorrectBlackDash(state.position, state.serverTime);
        }

        void CorrectBlackDash(Vector2 position, double time)
        {
            var ability = GetComponent<BlackLouieDashPushAbility>(); var movement = GetComponent<MovementController>();
            float speed = blackMotion.distance / Mathf.Max(0.01f, blackMotion.duration);
            float lead = Mathf.Clamp((float)(NetworkTime.predictedTime - time), 0, 0.15f);
            float remaining = Mathf.Max(0, blackMotion.distance - Vector2.Dot(position - blackMotion.start, blackMotion.direction));
            Vector2 target = position;
            ability.TryPreviewNetworkDashStep(position, blackMotion.direction, Mathf.Min(speed * lead, remaining), movement.AcknowledgedDestructiblePass, out target);
            Vector2 error = target - blackPosition;
            if (error.magnitude > Mathf.Max(0.1f, movement.tileSize)) { blackPosition = target; blackCorrection = Vector2.zero; }
            else blackCorrection = error;
        }

        void UpdateBlackDash(float deltaTime, float unscaledDeltaTime)
        {
            if (!blackActive) return;
            var movement = GetComponent<MovementController>();
            if (movement == null || movement.isDead || movement.IsEndingStage || (TryGetComponent<CharacterHealth>(out var health) && health.life <= 0)) { ClearBlackDash("dead_or_ending"); return; }
            if (GamePauseController.IsPaused) { RestoreBlackDashPosition(); PresentBlackDash(); return; }
            float previous = blackConfirmed ? blackSilence : blackAge;
            blackAge += unscaledDeltaTime; blackSilence += unscaledDeltaTime; blackElapsed += deltaTime;
            if ((!blackConfirmed && blackAge > Mathf.Clamp((float)NetworkTime.rtt * 3, 0.5f, 1)) || (blackConfirmed && blackSilence > 0.5f))
            { blackPosition = blackReplica; RestoreBlackDashPosition(); ClearBlackDash("confirmation_timeout"); return; }
            if (!blackHolding && !blackEnding)
            {
                var ability = GetComponent<BlackLouieDashPushAbility>();
                float speed = blackMotion.distance / Mathf.Max(0.01f, blackMotion.duration);
                float remaining = Mathf.Max(0, blackMotion.distance - Vector2.Dot(blackPosition - blackMotion.start, blackMotion.direction));
                float distance = Mathf.Min(remaining, speed * Mathf.Min(deltaTime, Mathf.Max(0, 0.15f - previous)));
                if (ability.TryPreviewNetworkDashStep(blackPosition, blackMotion.direction, distance, movement.AcknowledgedDestructiblePass, out var next)) blackPosition = next;
                var correctionRemaining = Vector2.MoveTowards(blackCorrection, Vector2.zero, speed * deltaTime * 0.5f);
                Vector2 correction = blackCorrection - correctionRemaining; blackCorrection = correctionRemaining;
                if (correction.sqrMagnitude > 0.000001f && ability.TryPreviewNetworkDashStep(blackPosition, correction.normalized, correction.magnitude, movement.AcknowledgedDestructiblePass, out next)) blackPosition = next;
                float overrun = Vector2.Dot(blackPosition - blackMotion.start, blackMotion.direction) - blackMotion.distance;
                if (overrun > 0) blackPosition -= blackMotion.direction * overrun;
            }
            RestoreBlackDashPosition(); PresentBlackDash();
        }

        public void RestoreBlackDashPosition()
        {
            if (!blackActive) return;
            if (TryGetComponent<Rigidbody2D>(out var body)) body.position = blackPosition;
            transform.position = new Vector3(blackPosition.x, blackPosition.y, transform.position.z);
        }

        void PresentBlackDash(bool restart = false)
        {
            if (!BlackDashOwnsPose || !TryGetComponent<PlayerMountCompanion>(out var companion)) return;
            var mount = companion.GetMountedLouieObject(); var visual = mount != null ? mount.GetComponentInChildren<BlackLouieDashAnimator>(true) : null;
            if (restart || visual != blackVisual) { StopBlackVisual(); blackVisual = visual; }
            blackVisual?.PresentPredictedDash(blackMotion.direction, blackElapsed / Mathf.Max(0.01f, blackMotion.duration), blackHolding);
        }

        void StopBlackVisual()
        {
            blackVisual?.Stop(); blackVisual = null;
            if (TryGetComponent<PlayerMountCompanion>(out var companion) && companion.GetMountedLouieObject() != null)
                foreach (var visual in companion.GetMountedLouieObject().GetComponentsInChildren<MountVisualController>(true)) visual.enabled = false;
        }

        void ClearBlackDash(string reason)
        {
            if (!blackActive) return;
            TraceBlackDash("black_dash_prediction_end", reason); StopBlackVisual();
            blackActive = blackConfirmed = blackHolding = blackEnding = false; blackCorrection = Vector2.zero;
        }

        void TraceBlackDash(string kind, string reason)
        {
            if (!GameplayLatencyDiagnostics.Enabled) return;
            var entry = GameplayLatencyDiagnostics.For(kind, GetComponent<MovementController>(), detail: reason);
            entry.inputTick = blackMotion.inputTick; entry.durationMs = blackAge * 1000; entry.delta = blackCorrection; GameplayLatencyDiagnostics.Write(entry);
        }
    }
}

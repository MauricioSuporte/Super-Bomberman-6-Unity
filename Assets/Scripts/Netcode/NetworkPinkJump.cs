using Mirror;
using UnityEngine;

namespace Assets.Scripts.Netcode
{
    public partial class NetworkBombFx
    {
        public struct PinkJumpMotion
        {
            public uint revision, inputTick;
            public Vector2 start, end, facing;
            public float duration, height, cooldown;
            public double serverTime;
            public bool active, completed;
        }
        PinkJumpMotion pinkJumpMotion, serverPinkJumpMotion;
        bool pinkJumpActive, pinkJumpConfirmed, pinkJumpEndPending, serverPinkJumpActive;
        float pinkJumpElapsed, pinkJumpSilence, pinkJumpPauseOffset, pinkJumpStartedLocal, pinkNextAllowedLocal;
        uint receivedPinkJumpRevision, serverPinkJumpRevision;
        Vector2 pinkJumpPosition, pinkJumpReplica, pinkJumpCorrection;
        bool pinkJumpSoundPlayed;
        public bool PinkJumpActive => pinkJumpActive;
        public bool PinkJumpVisualActive => pinkJumpActive && PinkJumpProgress >= 0;

        float PinkJumpProgress
        {
            get
            {
                if (isLocalPlayer) return pinkJumpElapsed / Mathf.Max(0.01f, pinkJumpMotion.duration);
                double offset = 0;
                if (TryGetComponent<NetworkTransformReliable>(out var sync))
                    offset = NetworkClient.sendInterval * (sync.sendIntervalMultiplier - 1 + (sync.timelineOffset ? sync.sendIntervalMultiplier : 0));
                return (float)(NetworkTime.time - pinkJumpMotion.serverTime - offset - pinkJumpPauseOffset) / Mathf.Max(0.01f, pinkJumpMotion.duration);
            }
        }

        public bool TryPredictPinkJump(int held, uint tick)
        {
            if (NetSync.Mode != NetSync.NetMode.Client || !NetworkClient.active || !isLocalPlayer || isServer || Time.time < pinkNextAllowedLocal ||
                !TryGetComponent<PlayerMountCompanion>(out var companion) || companion.GetMountedLouieType() != MountedType.Pink) return false;
            var ability = GetComponent<PinkLouieJumpAbility>() ?? gameObject.AddComponent<PinkLouieJumpAbility>();
            if (!ability.TryPreviewNetworkJump(MotionDiagnosticProbe.DirectionFromMask(held), out var geometry)) return false;
            return BeginPinkJumpPrediction(new PinkJumpMotion { inputTick = tick, start = geometry.start, end = geometry.end,
                facing = geometry.direction != Vector2.zero ? geometry.direction : geometry.facing, duration = ability.jumpDurationSeconds,
                height = ability.jumpArcHeight, cooldown = ability.jumpCooldownSeconds, active = true });
        }

        public bool BeginPinkJumpPrediction(PinkJumpMotion motion, bool confirmed = false)
        {
            var movement = GetComponent<MovementController>();
            if (NetSync.Mode != NetSync.NetMode.Client || !NetworkClient.active || isServer || pinkJumpActive ||
                movement == null || movement.isDead || movement.IsEndingStage || (TryGetComponent<CharacterHealth>(out var health) && health.life <= 0) ||
                (!confirmed && (!isLocalPlayer || ClientPlayerMotionActive || !movement.CanPredictNetworkMovement ||
                    AbilityPredictionLocksMovement || GamePauseController.IsPaused || BombermanNetworkManager.BattleStartPending))) return false;
            if (confirmed) { ClearGreenDash("pink_jump"); ClearPlayerBouncePresentation(); }
            ResetLocalAbilityPresentation();
            pinkJumpMotion = motion; pinkJumpActive = true; pinkJumpConfirmed = confirmed; pinkJumpEndPending = false;
            pinkJumpElapsed = pinkJumpSilence = pinkJumpPauseOffset = 0; pinkJumpStartedLocal = Time.time;
            pinkJumpPosition = pinkJumpReplica = motion.start; pinkJumpCorrection = Vector2.zero;
            if (isLocalPlayer) movement.SetPredictLocally(false);
            if (GetComponent<PinkLouieJumpAbility>() == null) gameObject.AddComponent<PinkLouieJumpAbility>();
            pinkJumpSoundPlayed = PlayMountAbilitySound(MountedType.Pink);
            RestorePinkJumpPosition(); PresentPinkJump(0);
            TracePinkJump("player_jump_prediction_begin", confirmed ? "authority_event" : "local_visual_only");
            return true;
        }

        [Server]
        public void ServerPinkJump(Vector2 start, Vector2 end, Vector2 facing, float duration, float height, float cooldown)
        {
            if (netId == 0) return;
            serverPinkJumpMotion = new PinkJumpMotion { revision = ++serverPinkJumpRevision,
                inputTick = TryGetComponent<NetworkPlayerInput>(out var input) ? input.CurrentActionCTick : 0,
                start = start, end = end, facing = facing, duration = Mathf.Max(0.01f, duration), height = height,
                cooldown = cooldown, serverTime = NetworkTime.localTime, active = true };
            serverPinkJumpActive = true; PlayMountAbilitySound(MountedType.Pink); RpcPinkJump(serverPinkJumpMotion);
        }

        [Server]
        public void ServerPinkJumpStop(Vector2 position, bool completed)
        {
            if (!serverPinkJumpActive || netId == 0) return;
            serverPinkJumpActive = false;
            var stop = serverPinkJumpMotion; stop.revision = ++serverPinkJumpRevision;
            stop.active = false; stop.completed = completed; stop.end = position; RpcPinkJump(stop);
        }

        [ClientRpc]
        void RpcPinkJump(PinkJumpMotion motion) => ReceivePinkJump(motion);

        public void ReceivePinkJump(PinkJumpMotion motion)
        {
            if (NetSync.Mode != NetSync.NetMode.Client || isServer || !NetworkClient.active ||
                !NetworkInputHistory.IsNewer(motion.revision, receivedPinkJumpRevision)) return;
            receivedPinkJumpRevision = motion.revision;
            if (!motion.active)
            {
                if (!pinkJumpActive || motion.inputTick != pinkJumpMotion.inputTick) return;
                pinkJumpEndPending = true; pinkJumpMotion.end = motion.end;
                if (!motion.completed)
                {
                    pinkJumpPosition = motion.end; RestorePinkJumpPosition(); ClearPinkJump("authority_interrupted");
                }
                return;
            }
            bool predicted = pinkJumpActive && isLocalPlayer && pinkJumpMotion.inputTick == motion.inputTick;
            if (!predicted && pinkJumpActive) ClearPinkJump("new_sequence");
            if (!pinkJumpActive && !BeginPinkJumpPrediction(motion, true)) return;
            Vector2 before = pinkJumpPosition;
            pinkJumpMotion = motion; pinkJumpConfirmed = true; pinkJumpSilence = 0;
            if (predicted)
            {
                pinkJumpCorrection = before - Vector2.Lerp(motion.start, motion.end, Mathf.Clamp01(PinkJumpProgress));
                if (!pinkJumpSoundPlayed) pinkJumpSoundPlayed = PlayMountAbilitySound(MountedType.Pink);
            }
            TracePinkJump("player_jump_prediction_confirm", "authority_geometry");
        }

        public void ReconcilePinkJump(uint acknowledgedTick, MovementController.NetworkMovementState state)
        {
            pinkJumpReplica = state.position;
            if (!pinkJumpActive || !isLocalPlayer || NetworkInputHistory.IsNewer(pinkJumpMotion.inputTick, acknowledgedTick)) return;
            if (!pinkJumpConfirmed) { ClearPinkJump("input_processed_without_jump"); return; }
            pinkJumpSilence = 0;
            if (state.mount != MountedType.Pink) { ClearPinkJump("mount_lost"); return; }
            if (state.canPredict && PinkJumpProgress >= 1) { pinkJumpPosition = state.position; RestorePinkJumpPosition(); ClearPinkJump("authority_released_motion"); }
        }

        void UpdatePinkJump(float deltaTime, float unscaledDeltaTime)
        {
            if (!pinkJumpActive) return;
            var movement = GetComponent<MovementController>();
            if (movement == null || movement.isDead || movement.IsEndingStage || (TryGetComponent<CharacterHealth>(out var health) && health.life <= 0)) { ClearPinkJump("dead_or_ending"); return; }
            if (GamePauseController.IsPaused)
            {
                if (!isLocalPlayer) pinkJumpPauseOffset += unscaledDeltaTime;
                return;
            }
            pinkJumpElapsed += deltaTime; pinkJumpSilence += unscaledDeltaTime;
            if ((!pinkJumpConfirmed && pinkJumpSilence > Mathf.Clamp((float)NetworkTime.rtt * 3, 0.5f, 1)) ||
                pinkJumpSilence > Mathf.Max(1, pinkJumpMotion.duration + 0.5f))
            {
                if (isLocalPlayer) { pinkJumpPosition = pinkJumpReplica; RestorePinkJumpPosition(); }
                ClearPinkJump("confirmation_timeout"); return;
            }
            float progress = PinkJumpProgress;
            if (progress < 0) return;
            pinkJumpCorrection = Vector2.MoveTowards(pinkJumpCorrection, Vector2.zero, deltaTime * 5);
            pinkJumpPosition = Vector2.Lerp(pinkJumpMotion.start, pinkJumpMotion.end, Mathf.Clamp01(progress)) + pinkJumpCorrection;
            RestorePinkJumpPosition(); PresentPinkJump(deltaTime);
            if (pinkJumpEndPending && progress >= 1)
            {
                pinkJumpPosition = pinkJumpMotion.end; RestorePinkJumpPosition();
                pinkNextAllowedLocal = Mathf.Max(pinkNextAllowedLocal, pinkJumpStartedLocal + pinkJumpMotion.duration + pinkJumpMotion.cooldown);
                ClearPinkJump("server_landing");
            }
        }

        public void RestorePinkJumpPosition()
        {
            if (!pinkJumpActive || !isLocalPlayer) return;
            if (TryGetComponent<Rigidbody2D>(out var body)) body.position = pinkJumpPosition;
            transform.position = new Vector3(pinkJumpPosition.x, pinkJumpPosition.y, transform.position.z);
        }

        void PresentPinkJump(float deltaTime, bool restart = false)
        {
            if (!pinkJumpActive || PinkJumpProgress < 0) return;
            GetComponent<PinkLouieJumpAbility>()?.PresentNetworkJump(pinkJumpMotion.facing,
                isLocalPlayer ? pinkJumpPosition : (Vector2)transform.position, Mathf.Clamp01(PinkJumpProgress), pinkJumpMotion.height, restart);
        }

        void ClearPinkJump(string reason)
        {
            if (!pinkJumpActive) return;
            TracePinkJump(reason == "confirmation_timeout" || reason == "input_processed_without_jump" ? "player_jump_prediction_reject" : "player_jump_end", reason);
            pinkJumpActive = false; pinkJumpConfirmed = false; pinkJumpCorrection = Vector2.zero;
            GetComponent<PinkLouieJumpAbility>()?.StopNetworkJumpPresentation();
        }

        void TracePinkJump(string kind, string reason)
        {
            if (!GameplayLatencyDiagnostics.Enabled) return;
            var entry = GameplayLatencyDiagnostics.For(kind, GetComponent<MovementController>(), detail: reason);
            entry.inputTick = pinkJumpMotion.inputTick; entry.durationMs = pinkJumpElapsed * 1000; entry.delta = pinkJumpCorrection;
            GameplayLatencyDiagnostics.Write(entry);
        }
    }
}

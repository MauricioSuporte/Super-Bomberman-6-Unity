using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace Assets.Scripts.Netcode
{
    public partial class NetworkBombFx
    {
        public struct RidingMotion
        {
            public uint revision, inputTick;
            public PlayerRidingController.NetworkArcState arc;
            public double serverTime, sampleTime, endTime;
            public float elapsed;
            public bool active, completed, paused;
        }

        RidingMotion ridingMotion;
        [SyncVar(hook = nameof(OnRidingStateChanged))] RidingMotion serverRidingMotion;
        float serverRidingNextSample;
        uint serverRidingRevision, receivedRidingRevision;
        bool ridingActive, ridingPending, ridingEnding, hasDismountState, dismountReady;
        float ridingElapsed, ridingSilence;
        double ridingSnapshotTime;
        Vector2 ridingReplica;
        Vector3 ridingPosition, ridingCorrection;
        PlayerRidingController.NetworkArcState dismountTemplate;
        PlayerRidingController ridingVisual;
        readonly Queue<PlayerBounceMotion> ridingLandingBounces = new();
        public bool RidingArcActive => ridingActive;

        public bool TryPredictDismount(int held, uint tick)
        {
            if (NetSync.Mode != NetSync.NetMode.Client || !NetworkClient.active || isServer || !isLocalPlayer ||
                !hasDismountState || !dismountReady || ridingActive || ClientPlayerMotionActive || AbilityPredictionLocksMovement ||
                GamePauseController.IsPaused || BombermanNetworkManager.BattleStartPending ||
                !TryGetComponent<MovementController>(out var movement) || !movement.CanPredictNetworkMovement ||
                !movement.IsMounted || !TryGetComponent<PlayerRidingController>(out _) ||
                (TryGetComponent<NetworkPlayerInput>(out var input) && !input.ServerAllowsMovementPrediction)) return false;
            var arc = dismountTemplate;
            arc.kind = PlayerRidingController.RidingArcType.Dismount;
            arc.start = arc.target = transform.position;
            arc.facing = MotionDiagnosticProbe.DirectionFromMask(held);
            if (arc.facing == Vector2.zero) arc.facing = movement.FacingDirection;
            return BeginRidingPresentation(new RidingMotion { inputTick = tick, arc = arc, active = true }, false);
        }

        bool BeginRidingPresentation(RidingMotion motion, bool confirmed)
        {
            bool revengeReturn = confirmed && GetComponent<NetworkPlayerState>()?.HasApprovedRevengeReturn == true;
            if (!TryGetComponent<PlayerRidingController>(out ridingVisual) || !TryGetComponent<MovementController>(out var movement) ||
                (!revengeReturn && movement.isDead) || movement.IsEndingStage ||
                (!revengeReturn && TryGetComponent<CharacterHealth>(out var health) && health.life <= 0)) return false;
            ClearGreenDash("riding"); ClearPinkJump("riding"); ClearBlackDash("riding"); ClearPlayerBouncePresentation();
            ClearPurpleLineCast("riding"); ClearMoleStartup("riding"); ClearRedPunch("riding"); ClearTankPrediction("riding");
            ResetLocalAbilityPresentation();
            motion.arc.duration = Mathf.Max(0.01f, motion.arc.duration);
            if (!hasDismountState) ridingReplica = motion.arc.start;
            ridingMotion = motion; ridingActive = true; ridingPending = !confirmed; ridingEnding = false;
            ridingSilence = 0; ridingCorrection = Vector3.zero;
            double sampleTime = motion.sampleTime > 0 ? motion.sampleTime : motion.serverTime;
            float age = confirmed && !GamePauseController.IsPaused && !motion.paused ?
                Mathf.Max(0, (float)((isLocalPlayer ? NetworkTime.predictedTime : NetworkTime.time) - sampleTime)) : 0;
            ridingElapsed = Mathf.Clamp((confirmed ? motion.elapsed : 0) + Mathf.Min(age, Mathf.Min(0.15f, motion.arc.duration * 0.25f)), 0, motion.arc.duration);
            ridingPosition = Vector3.Lerp(motion.arc.start, motion.arc.target, Mathf.Clamp01(ridingElapsed / motion.arc.duration));
            movement.SetPredictLocally(false); RestoreRidingPosition(); PresentRidingArc();
            TraceRiding("riding_prediction_begin", confirmed ? "authority" : "gesture_only"); return true;
        }

        [Server]
        public void ServerRidingArcStart(PlayerRidingController.NetworkArcState arc, uint inputTick)
        {
            if (netId == 0) return;
            serverRidingMotion = new RidingMotion { revision = ++serverRidingRevision, inputTick = inputTick,
                arc = arc, serverTime = NetworkTime.localTime, sampleTime = NetworkTime.localTime,
                active = true, paused = GamePauseController.IsPaused };
            serverRidingNextSample = Time.unscaledTime + 0.05f;
            RpcRidingArc(serverRidingMotion);
        }

        [Server]
        public void ServerRidingArcStop(bool completed)
        {
            if (netId == 0 || !serverRidingMotion.active) return;
            var motion = serverRidingMotion;
            motion.active = false; motion.completed = completed; motion.paused = false;
            motion.endTime = motion.sampleTime = NetworkTime.localTime; motion.revision = ++serverRidingRevision;
            if (completed) motion.elapsed = motion.arc.duration;
            serverRidingMotion = motion; RpcRidingArc(motion);
        }

        [Server]
        public void ServerRidingArcProgress(float elapsed, bool paused)
        {
            if (!serverRidingMotion.active || (paused == serverRidingMotion.paused &&
                (paused || Time.unscaledTime < serverRidingNextSample))) return;
            serverRidingNextSample = Time.unscaledTime + 0.05f;
            var motion = serverRidingMotion; motion.elapsed = Mathf.Clamp(elapsed, 0, motion.arc.duration);
            motion.sampleTime = NetworkTime.localTime; motion.paused = paused; serverRidingMotion = motion;
        }

        void OnRidingStateChanged(RidingMotion previous, RidingMotion current) => ReceiveRidingArc(current);
        public override void OnStartClient()
        {
            base.OnStartClient();
            StartBombMotionLifetime();
            if (!isServer && serverRidingMotion.active) ReceiveRidingArc(serverRidingMotion);
        }

        [ClientRpc]
        void RpcRidingArc(RidingMotion motion) => ReceiveRidingArc(motion);

        public void ReceiveRidingArc(RidingMotion motion)
        {
            if (NetSync.Mode != NetSync.NetMode.Client || isServer || !NetworkClient.active ||
                !NetworkInputHistory.IsNewer(motion.revision, receivedRidingRevision)) return;
            receivedRidingRevision = motion.revision;
            if (!motion.active)
            {
                if (!ridingActive || ridingMotion.inputTick != motion.inputTick) return;
                if (!motion.completed) { RollbackRiding("authority_cancelled"); return; }
                ridingMotion.endTime = motion.endTime; ridingMotion.revision = motion.revision; ridingMotion.paused = false;
                ridingEnding = true; ridingPending = false; ridingSilence = 0;
                return;
            }
            bool predicted = ridingActive && ridingPending && ridingMotion.inputTick == motion.inputTick &&
                ridingMotion.arc.kind == PlayerRidingController.RidingArcType.Dismount;
            if (!predicted)
            {
                ClearRidingPresentation("new_sequence");
                double sampleTime = motion.sampleTime > 0 ? motion.sampleTime : motion.serverTime;
                if (!GamePauseController.IsPaused && !motion.paused &&
                    Mathf.Max(0, (float)(NetworkTime.time - sampleTime)) > Mathf.Max(1, motion.arc.duration + 0.5f)) return;
                BeginRidingPresentation(motion, true); return;
            }
            float progress = Mathf.Clamp01(ridingElapsed / Mathf.Max(0.01f, ridingMotion.arc.duration));
            ridingMotion = motion; ridingMotion.arc.duration = Mathf.Max(0.01f, motion.arc.duration);
            ridingElapsed = progress * ridingMotion.arc.duration; ridingPending = false; ridingSilence = 0;
            ridingCorrection = ridingPosition - Vector3.Lerp(motion.arc.start, motion.arc.target, progress);
            float tileSize = GetComponent<MovementController>().tileSize;
            if (ridingCorrection.magnitude > Mathf.Max(0.1f, tileSize)) ridingCorrection = Vector3.zero;
            TraceRiding("riding_prediction_confirm", "phase_preserved");
        }

        public void ReconcileRiding(uint tick, MovementController.NetworkMovementState state, MountActionState actions)
        {
            ridingReplica = state.position; ridingSnapshotTime = state.serverTime;
            hasDismountState = true; dismountReady = actions.dismountEligible; dismountTemplate = actions.dismountArc;
            if (!ridingActive || !isLocalPlayer) return;
            if (ridingPending && !NetworkInputHistory.IsNewer(ridingMotion.inputTick, tick)) { RollbackRiding("input_processed_without_riding"); return; }
            if (!ridingPending)
            {
                ridingSilence = 0;
                if (state.canPredict && state.serverTime >= ridingMotion.serverTime + ridingMotion.arc.duration)
                { ridingEnding = true; ridingMotion.endTime = state.serverTime; }
            }
            TryFinishRiding();
        }

        public void RestoreRidingPosition()
        {
            if (!ridingActive) return;
            if (TryGetComponent<Rigidbody2D>(out var body)) body.position = ridingPosition;
            transform.position = ridingPosition;
        }

        public void FinishConfirmedRevengeReturnPresentation() => ClearPlayerActionPresentations();

        void UpdateRidingPresentation(float deltaTime, float unscaledDeltaTime)
        {
            if (!ridingActive) return;
            bool revengeReturn = GetComponent<NetworkPlayerState>()?.HasApprovedRevengeReturn == true;
            if (!TryGetComponent<MovementController>(out var movement) || (!revengeReturn && movement.isDead) || movement.IsEndingStage ||
                ridingVisual == null || (!revengeReturn && TryGetComponent<CharacterHealth>(out var health) && health.life <= 0))
            { RollbackRiding("interrupted"); return; }
            bool authorityPaused = !ridingPending && (serverRidingMotion.revision == ridingMotion.revision ?
                serverRidingMotion.active && serverRidingMotion.paused : ridingMotion.paused);
            if (!GamePauseController.IsPaused && !authorityPaused)
            {
                ridingSilence += unscaledDeltaTime;
                if (isLocalPlayer || NetworkTime.time >= ridingMotion.serverTime) ridingElapsed += deltaTime;
                ridingCorrection = Vector3.MoveTowards(ridingCorrection, Vector3.zero, Mathf.Max(1, movement.tileSize * 4) * deltaTime);
            }
            float timeout = ridingPending ? Mathf.Clamp((float)NetworkTime.rtt * 3, 0.5f, 1) : Mathf.Max(1, ridingMotion.arc.duration + 0.5f);
            if (ridingSilence > timeout || (!ridingPending && !ridingEnding && ridingElapsed > ridingMotion.arc.duration + 1)) { RollbackRiding("confirmation_timeout"); return; }
            ridingPosition = Vector3.Lerp(ridingMotion.arc.start, ridingMotion.arc.target,
                Mathf.Clamp01(ridingElapsed / ridingMotion.arc.duration)) + ridingCorrection;
            RestoreRidingPosition(); PresentRidingArc(); TryFinishRiding();
        }

        void PresentRidingArc()
        {
            if (ridingActive && ridingVisual != null) ridingVisual.PresentNetworkArc(ridingMotion.arc, ridingElapsed);
        }

        void TryFinishRiding()
        {
            if (!ridingActive || !ridingEnding || ridingElapsed < ridingMotion.arc.duration) return;
            if (ridingLandingBounces.Count > 0)
            {
                var landing = ridingLandingBounces.ToArray(); ridingLandingBounces.Clear();
                ClearRidingPresentation("landing_bounce");
                foreach (var bounce in landing) ReceivePlayerBounce(bounce);
            }
            else if (isLocalPlayer ? ridingSnapshotTime >= ridingMotion.endTime : NetworkTime.time >= ridingMotion.endTime)
                ClearRidingPresentation("authority_released");
        }

        bool DeferBounceForRiding(PlayerBounceMotion motion)
        {
            if (!ridingActive || (!motion.moving && !PlayerBounceActive && ridingLandingBounces.Count == 0)) return false;
            if (!ridingEnding || ridingElapsed >= ridingMotion.arc.duration)
            { ClearRidingPresentation("authority_bounce"); return false; }
            if (ridingLandingBounces.Count >= 32) { ClearRidingPresentation("landing_queue_overflow"); return false; }
            ridingLandingBounces.Enqueue(motion); return true;
        }

        void RollbackRiding(string reason)
        {
            if (isLocalPlayer) { ridingPosition = new Vector3(ridingReplica.x, ridingReplica.y, transform.position.z); RestoreRidingPosition(); }
            ClearRidingPresentation(reason);
        }

        void ClearRidingPresentation(string reason)
        {
            if (!ridingActive) return;
            ridingVisual?.StopNetworkArcVisual(); ridingVisual = null;
            ridingActive = ridingPending = ridingEnding = false; ridingLandingBounces.Clear();
            TraceRiding("riding_prediction_end", reason);
        }

        void TraceRiding(string kind, string reason)
        {
            if (!GameplayLatencyDiagnostics.Enabled) return;
            var entry = GameplayLatencyDiagnostics.For(kind, GetComponent<MovementController>(), detail: reason +
                ";hold_ms=" + (Mathf.Max(0, ridingElapsed - ridingMotion.arc.duration) * 1000).ToString("F1", System.Globalization.CultureInfo.InvariantCulture));
            entry.inputTick = ridingMotion.inputTick; entry.durationMs = ridingElapsed * 1000;
            entry.delta = ridingCorrection; GameplayLatencyDiagnostics.Write(entry);
        }
    }
}

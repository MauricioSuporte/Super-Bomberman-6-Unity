using Mirror;
using UnityEngine;

namespace Assets.Scripts.Netcode
{
    public partial class NetworkBombFx
    {
        public struct MoleStartupMotion
        {
            public uint revision, inputTick;
            public Vector2 direction;
            public float duration;
            public double serverTime;
            public bool active;
        }

        MoleStartupMotion moleStartup, serverMoleStartup;
        uint moleStartupRevision, receivedMoleStartupRevision;
        bool moleStartupActive, moleStartupPending, moleStartupSound;
        float moleStartupElapsed, moleStartupAge;
        MoleMountDrillAbility moleStartupAbility;
        public bool MoleStartupActive => moleStartupActive;

        public bool TryPredictMoleStartup(int held, uint tick)
        {
            if (NetSync.Mode != NetSync.NetMode.Client || !NetworkClient.active || !isLocalPlayer || isServer ||
                moleStartupActive || ClientPlayerMotionActive || AbilityPredictionLocksMovement || GamePauseController.IsPaused ||
                BombermanNetworkManager.BattleStartPending || !TryGetComponent<MovementController>(out var movement) ||
                !movement.CanPredictNetworkMovement ||
                (TryGetComponent<NetworkPlayerInput>(out var input) && !input.ServerAllowsMovementPrediction) ||
                !TryGetComponent<PlayerMountCompanion>(out var companion) ||
                companion.GetMountedLouieType() != MountedType.Mole || !TryGetComponent<MoleMountDrillAbility>(out var ability) ||
                !ability.IsEnabled) return false;
            Vector2 direction = MotionDiagnosticProbe.DirectionFromMask(held);
            if (direction == Vector2.zero) direction = movement.FacingDirection;
            return BeginMoleStartup(new MoleStartupMotion { inputTick = tick, direction = direction,
                duration = ability.StartupInvulnerabilityDelaySeconds, active = true }, false);
        }

        bool BeginMoleStartup(MoleStartupMotion motion, bool confirmed)
        {
            if (!TryGetComponent<MoleMountDrillAbility>(out moleStartupAbility) ||
                !TryGetComponent<MovementController>(out var movement) || movement.isDead || movement.IsEndingStage ||
                (TryGetComponent<CharacterHealth>(out var health) && health.life <= 0)) return false;
            moleStartup = motion; moleStartup.duration = Mathf.Max(0.01f, motion.duration);
            moleStartupActive = true; moleStartupPending = !confirmed; moleStartupAge = 0;
            moleStartupElapsed = confirmed ? Mathf.Clamp((float)(NetworkTime.time - motion.serverTime), 0, moleStartup.duration) : 0;
            movement.SetPredictLocally(false);
            moleStartupSound = PlayMountAbilitySound(MountedType.Mole);
            PresentMoleStartup(); TraceMoleStartup("mole_startup_prediction_begin", confirmed ? "authority" : "visual_only");
            return true;
        }

        [Server]
        public void ServerMoleStartup(Vector2 direction, float duration)
        {
            if (netId == 0) return;
            serverMoleStartup = new MoleStartupMotion { revision = ++moleStartupRevision,
                inputTick = TryGetComponent<NetworkPlayerInput>(out var input) ? input.CurrentActionCTick : 0,
                direction = direction, duration = duration, serverTime = NetworkTime.localTime, active = true };
            PlayMountAbilitySound(MountedType.Mole); RpcMoleStartup(serverMoleStartup);
        }

        [Server]
        public void ServerMoleStartupEnd()
        {
            if (netId == 0 || !serverMoleStartup.active) return;
            serverMoleStartup.active = false; serverMoleStartup.revision = ++moleStartupRevision;
            RpcMoleStartup(serverMoleStartup);
        }

        [ClientRpc]
        void RpcMoleStartup(MoleStartupMotion motion) => ReceiveMoleStartup(motion);

        public void ReceiveMoleStartup(MoleStartupMotion motion)
        {
            if (NetSync.Mode != NetSync.NetMode.Client || !NetworkClient.active || isServer ||
                !NetworkInputHistory.IsNewer(motion.revision, receivedMoleStartupRevision)) return;
            receivedMoleStartupRevision = motion.revision;
            if (!isLocalPlayer) { if (motion.active) PlayMountAbilitySound(MountedType.Mole); return; }
            if (!motion.active)
            {
                if (moleStartupActive && motion.inputTick == moleStartup.inputTick) ClearMoleStartup("authority_next_phase");
                return;
            }
            if (!TryGetComponent<PlayerMountCompanion>(out var companion) || companion.GetMountedLouieType() != MountedType.Mole) return;
            if (moleStartupActive && NetworkInputHistory.IsNewer(moleStartup.inputTick, motion.inputTick)) return;
            if (!moleStartupActive || moleStartup.inputTick != motion.inputTick)
            {
                ClearMoleStartup("new_sequence"); BeginMoleStartup(motion, true); return;
            }
            moleStartupPending = false; moleStartup.duration = Mathf.Max(0.01f, motion.duration);
            if (!moleStartupSound) moleStartupSound = PlayMountAbilitySound(MountedType.Mole);
            TraceMoleStartup("mole_startup_prediction_confirm", "phase_preserved");
        }

        public void ReconcileMoleStartup(uint tick, MovementController.NetworkMovementState state)
        {
            if (!moleStartupActive || !isLocalPlayer) return;
            if (state.mount != MountedType.Mole) { ClearMoleStartup("mount_lost"); return; }
            if (NetworkInputHistory.IsNewer(moleStartup.inputTick, tick)) return;
            if (moleStartupPending) ClearMoleStartup("input_processed_without_drill");
            else if (state.canPredict) ClearMoleStartup("authority_released");
        }

        void UpdateMoleStartup(float deltaTime, float unscaledDeltaTime)
        {
            if (!moleStartupActive) return;
            if (moleStartupAbility == null || !moleStartupAbility.IsEnabled ||
                !TryGetComponent<MovementController>(out var movement) || movement.isDead || movement.IsEndingStage ||
                (TryGetComponent<CharacterHealth>(out var health) && health.life <= 0) ||
                !TryGetComponent<PlayerMountCompanion>(out var companion) || companion.GetMountedLouieType() != MountedType.Mole || ClientPlayerMotionActive)
            { ClearMoleStartup("interrupted"); return; }
            if (GamePauseController.IsPaused) return;
            moleStartupElapsed += deltaTime; moleStartupAge += unscaledDeltaTime;
            float timeout = Mathf.Clamp((float)NetworkTime.rtt * 3, 0.5f, 1);
            if (moleStartupAge > (moleStartupPending ? timeout : moleStartup.duration + timeout))
            { ClearMoleStartup("confirmation_timeout"); return; }
            PresentMoleStartup();
        }

        void PresentMoleStartup()
        {
            if (moleStartupActive && moleStartupAbility != null)
                moleStartupAbility.PresentNetworkStartup(moleStartup.direction, Mathf.Min(moleStartupElapsed, moleStartup.duration));
        }

        void ClearMoleStartup(string reason)
        {
            if (!moleStartupActive) return;
            TraceMoleStartup("mole_startup_prediction_end", reason);
            moleStartupAbility?.StopNetworkStartup(); moleStartupAbility = null;
            moleStartupActive = moleStartupPending = moleStartupSound = false;
        }

        void TraceMoleStartup(string kind, string reason)
        {
            if (!GameplayLatencyDiagnostics.Enabled) return;
            var entry = GameplayLatencyDiagnostics.For(kind, GetComponent<MovementController>(), detail: reason);
            entry.inputTick = moleStartup.inputTick; entry.durationMs = moleStartupElapsed * 1000; GameplayLatencyDiagnostics.Write(entry);
        }
    }
}

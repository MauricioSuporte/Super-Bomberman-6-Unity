using Mirror;
using UnityEngine;

namespace Assets.Scripts.Netcode
{
    public partial class NetworkBombFx
    {
        bool redPunchPending, redPunchPose;
        uint redPunchTick, redPunchReceivedTick;
        float redPunchElapsed, redPunchDuration, redPunchNextAllowed;
        Vector2 redPunchDirection;
        RedLouiePunchAnimator redPunchVisual;

        public bool RedPunchPoseActive => redPunchPose;

        // Only the gesture is speculative. Hit testing, stun, item drops and the
        // longer success cooldown are decided by RedLouiePunchStunAbility on server.
        public bool TryPredictRedPunch(int held, uint tick)
        {
            if (NetSync.Mode != NetSync.NetMode.Client || !NetworkClient.active || !isLocalPlayer || isServer ||
                redPunchPending || redPunchPose || Time.time < redPunchNextAllowed || ClientPlayerMotionActive ||
                AbilityPredictionLocksMovement || GamePauseController.IsPaused || BombermanNetworkManager.BattleStartPending ||
                !TryGetComponent<MovementController>(out var movement) || !movement.CanPredictNetworkMovement ||
                !TryGetComponent<PlayerMountCompanion>(out var companion) || companion.GetMountedLouieType() != MountedType.Red) return false;
            var ability = GetComponent<RedLouiePunchStunAbility>() ?? gameObject.AddComponent<RedLouiePunchStunAbility>();
            Vector2 direction = MotionDiagnosticProbe.DirectionFromMask(held);
            if (direction == Vector2.zero) direction = movement.FacingDirection;
            direction = Mathf.Abs(direction.x) > Mathf.Abs(direction.y) ? new Vector2(Mathf.Sign(direction.x), 0) : new Vector2(0, Mathf.Sign(direction.y));
            if (direction == Vector2.zero) direction = Vector2.down;
            redPunchTick = tick; redPunchPending = true; redPunchElapsed = 0;
            redPunchDuration = Mathf.Max(0.01f, ability.punchCooldownSeconds);
            redPunchDirection = direction; redPunchPose = true;
            redPunchNextAllowed = Time.time + redPunchDuration;
            PresentRedPunch(); TraceRedPunch("mount_gesture_prediction_begin", "visual_only");
            return true;
        }

        [Server]
        public void ServerRedPunch(Vector2 direction, float duration, float cooldown, bool hit)
        {
            if (netId == 0) return;
            uint tick = TryGetComponent<NetworkPlayerInput>(out var input) ? input.CurrentActionCTick : 0;
            RpcRedPunch(tick, direction, duration, cooldown, hit, NetworkTime.localTime);
        }

        [ClientRpc]
        void RpcRedPunch(uint tick, Vector2 direction, float duration, float cooldown, bool hit, double serverTime)
            => ReceiveRedPunch(tick, direction, duration, cooldown, hit, serverTime);

        public void ReceiveRedPunch(uint tick, Vector2 direction, float duration, float cooldown, bool hit, double serverTime)
        {
            if (NetSync.Mode != NetSync.NetMode.Client || isServer || !NetworkClient.active ||
                !NetworkInputHistory.IsNewer(tick, redPunchReceivedTick)) return;
            redPunchReceivedTick = tick;
            // Offline plays this sound only on a confirmed hit; don't invent one
            // for a locally predicted miss or play the confirmation twice.
            if (hit) PlayMountAbilitySound(MountedType.Red);
            if (!isLocalPlayer) return; // Observers already receive all mount frames.
            float age = Mathf.Max(0, (float)(NetworkTime.time - serverTime));
            redPunchNextAllowed = Mathf.Max(redPunchNextAllowed, Time.time + Mathf.Max(0, cooldown - age));
            if (redPunchPending && redPunchTick == tick)
            {
                redPunchPending = false;
                redPunchDuration = Mathf.Max(0.01f, duration);
                TraceRedPunch("mount_gesture_prediction_confirm", "server_validated");
                return; // Keep the phase already displayed; never restart at frame zero.
            }
            if (redPunchPending && NetworkInputHistory.IsNewer(redPunchTick, tick)) return;
            ClearRedPunch("authority_sequence");
            redPunchTick = tick; redPunchDirection = direction;
            redPunchDuration = Mathf.Max(0.01f, duration); redPunchElapsed = age;
            redPunchPose = age < redPunchDuration;
            PresentRedPunch();
        }

        public void ReconcileRedPunch(uint acknowledgedTick, MovementController.NetworkMovementState state)
        {
            if (!isLocalPlayer) return;
            if (redPunchPending && !NetworkInputHistory.IsNewer(redPunchTick, acknowledgedTick))
            {
                redPunchNextAllowed = Time.time;
                ClearRedPunch("input_processed_without_punch");
            }
            else if (state.mount != MountedType.Red) ClearRedPunch("mount_lost");
        }

        void UpdateRedPunch(float deltaTime)
        {
            if (!redPunchPose && !redPunchPending) return;
            if (!TryGetComponent<MovementController>(out var movement) || movement.isDead || movement.IsEndingStage ||
                (TryGetComponent<CharacterHealth>(out var health) && health.life <= 0) ||
                !TryGetComponent<PlayerMountCompanion>(out var companion) || companion.GetMountedLouieType() != MountedType.Red || ClientPlayerMotionActive)
            { ClearRedPunch("interrupted"); return; }
            if (GamePauseController.IsPaused) return;
            redPunchElapsed += deltaTime;
            if (redPunchPose && redPunchElapsed >= redPunchDuration)
            {
                StopRedPunchVisual(); redPunchPose = false;
            }
            if (redPunchPending && redPunchElapsed > Mathf.Max(1, (float)NetworkTime.rtt * 3))
                ClearRedPunch("confirmation_timeout");
            else PresentRedPunch();
        }

        void PresentRedPunch(bool restart = false)
        {
            if (!redPunchPose || !TryGetComponent<PlayerMountCompanion>(out var companion)) return;
            var mount = companion.GetMountedLouieObject();
            var visual = mount != null ? mount.GetComponentInChildren<RedLouiePunchAnimator>(true) : null;
            if (restart || visual != redPunchVisual)
            {
                StopRedPunchVisual(); redPunchVisual = visual;
                redPunchVisual?.Play(redPunchDirection);
            }
            redPunchVisual?.PresentPredictedProgress(redPunchElapsed / Mathf.Max(0.01f, redPunchDuration));
        }

        void StopRedPunchVisual()
        {
            redPunchVisual?.Stop(); redPunchVisual = null;
            if (TryGetComponent<PlayerMountCompanion>(out var companion))
                foreach (var visual in companion.GetMountedLouieObject() != null ? companion.GetMountedLouieObject().GetComponentsInChildren<MountVisualController>(true) : System.Array.Empty<MountVisualController>())
                    visual.enabled = false;
        }

        void ClearRedPunch(string reason)
        {
            if (!redPunchPose && !redPunchPending) return;
            TraceRedPunch("mount_gesture_end", reason); StopRedPunchVisual(); redPunchPose = redPunchPending = false;
        }

        void TraceRedPunch(string kind, string reason)
        {
            if (!GameplayLatencyDiagnostics.Enabled) return;
            var entry = GameplayLatencyDiagnostics.For(kind, GetComponent<MovementController>(), detail: "Red:" + reason);
            entry.inputTick = redPunchTick; entry.durationMs = redPunchElapsed * 1000; GameplayLatencyDiagnostics.Write(entry);
        }
    }
}

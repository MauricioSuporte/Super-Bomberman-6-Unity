using Mirror;
using UnityEngine;

namespace Assets.Scripts.Netcode
{
    public partial class NetworkBombFx
    {
        // Actions share the real input tick; never synthesize one tick per bomb.
        // 0 = ordinary plant, 1 = LineBomb A, 2 = Purple Louie C.
        public bool TryPredictLinePlacement(uint tick, int held, bool purple)
        {
            if (NetSync.Mode != NetSync.NetMode.Client || !NetworkClient.active || !isLocalPlayer || isServer ||
                !hasPlacementState || (purple ? !placementState.purpleAllowed : !placementState.lineAllowed) ||
                AbilityPredictionLocksMovement || ClientPlayerMotionActive || CarryingBombLocally || GamePauseController.IsPaused ||
                BombermanNetworkManager.BattleStartPending || !TryGetComponent<MovementController>(out var movement) ||
                !movement.CanPredictNetworkMovement ||
                (TryGetComponent<NetworkPlayerInput>(out var input) && !input.ServerAllowsMovementPrediction)) return false;
            if (purple && (!TryGetComponent<PlayerMountCompanion>(out var mount) || mount.GetMountedLouieType() != MountedType.Purple)) return false;
            Vector2 origin = LineBombAbility.GetLineOrigin(movement);
            if (!purple && !LineBombAbility.HasLineSeed(movement, origin) && !HasPredictedPlacementAt(origin, movement.tileSize, tick)) return false;
            Vector2 direction = purple ? MotionDiagnosticProbe.DirectionFromMask(held) : movement.FacingDirection;
            if (direction == Vector2.zero) direction = movement.FacingDirection;
            if (direction == Vector2.zero) direction = Vector2.down;
            direction = Mathf.Abs(direction.x) >= Mathf.Abs(direction.y) ? new Vector2(Mathf.Sign(direction.x), 0) : new Vector2(0, Mathf.Sign(direction.y));
            var controller = bomb != null ? bomb : GetComponent<BombController>();
            if (controller == null) return false;
            byte action = purple ? (byte)2 : (byte)1;
            if (purple && purpleLineActive) return false;
            int available = Mathf.Clamp(placementState.available - placements.Count, 0, 64);
            int placed = 0;
            for (int i = 0; i < available; i++)
            {
                Vector2 candidate = origin + direction * (movement.tileSize * (i + 1));
                if (Physics2D.OverlapBox(candidate, Vector2.one * 0.4f, 0, LayerMask.GetMask("Stage", "Enemy")) != null ||
                    !controller.TryPreviewNetworkPlacementAt(candidate, out var position)) break;
                uint prefabId = placements.Count > 0 ? placementState.continuationPrefabAssetId : placementState.prefabAssetId;
                if (!NetworkClient.prefabs.TryGetValue(prefabId, out var prefab) ||
                    !BeginPlacementPrediction(tick, position, prefab, action, playSound: placed == 0)) break;
                placed++;
            }
            return purple ? BeginPurpleLineCast(tick, direction, placementState.purpleDuration) : placed > 0;
        }

        bool purpleLineActive, purpleLinePending;
        uint purpleLineTick, receivedPurpleLineTick;
        float purpleLineElapsed, purpleLineAge, purpleLineDuration;
        Vector2 purpleLineDirection;
        PurpleLouieBombLineAnimator purpleLineVisual;
        public bool PurpleLinePoseActive => purpleLineActive && purpleLineElapsed < purpleLineDuration;

        bool BeginPurpleLineCast(uint tick, Vector2 direction, float duration)
        {
            if (purpleLineActive || !TryGetComponent<MovementController>(out var movement) || movement.isDead || movement.IsEndingStage ||
                (TryGetComponent<CharacterHealth>(out var health) && health.life <= 0)) return false;
            purpleLineActive = purpleLinePending = true; purpleLineTick = tick; purpleLineDirection = direction;
            purpleLineElapsed = purpleLineAge = 0; purpleLineDuration = Mathf.Max(0.01f, duration);
            PresentPurpleLineCast(); TracePlacement("line_cast_prediction_begin", tick, transform.position, 0, "Purple:visual_only");
            return true;
        }

        [Server]
        public void ServerPurpleLineCast(Vector2 direction, float duration)
        {
            if (netId == 0) return;
            RpcPurpleLineCast(TryGetComponent<NetworkPlayerInput>(out var input) ? input.CurrentActionCTick : 0,
                direction, Mathf.Max(0.01f, duration), NetworkTime.localTime);
        }

        [ClientRpc]
        void RpcPurpleLineCast(uint tick, Vector2 direction, float duration, double serverTime)
            => ReceivePurpleLineCast(tick, direction, duration, serverTime);

        public void ReceivePurpleLineCast(uint tick, Vector2 direction, float duration, double serverTime)
        {
            if (NetSync.Mode != NetSync.NetMode.Client || !NetworkClient.active || isServer || !isLocalPlayer ||
                !NetworkInputHistory.IsNewer(tick, receivedPurpleLineTick)) return;
            receivedPurpleLineTick = tick;
            if (!TryGetComponent<PlayerMountCompanion>(out var companion) || companion.GetMountedLouieType() != MountedType.Purple) return;
            if (purpleLineActive && NetworkInputHistory.IsNewer(purpleLineTick, tick)) return;
            if (!purpleLineActive || purpleLineTick != tick)
            {
                ClearPurpleLineCast("authority_sequence");
                if (!BeginPurpleLineCast(tick, direction, duration)) return;
                purpleLineElapsed = Mathf.Max(0, (float)(NetworkTime.predictedTime - serverTime));
            }
            purpleLineDuration = Mathf.Max(0.01f, duration); purpleLinePending = false;
            PresentPurpleLineCast(); TracePlacement("line_cast_prediction_confirm", tick, transform.position, purpleLineElapsed * 1000, "phase_preserved");
        }

        public void ReconcilePurpleLineCast(uint tick, MovementController.NetworkMovementState state)
        {
            if (!purpleLineActive || !isLocalPlayer) return;
            if (state.mount != MountedType.Purple) { ClearPurpleLineCast("mount_lost"); return; }
            if (NetworkInputHistory.IsNewer(purpleLineTick, tick)) return;
            if (purpleLinePending) ClearPurpleLineCast("input_processed_without_cast");
            else if (state.canPredict) ClearPurpleLineCast("authority_released");
        }

        void UpdatePurpleLineCast(float deltaTime, float unscaledDeltaTime)
        {
            if (!purpleLineActive) return;
            if (!TryGetComponent<MovementController>(out var movement) || movement.isDead || movement.IsEndingStage ||
                (TryGetComponent<CharacterHealth>(out var health) && health.life <= 0) || ClientPlayerMotionActive ||
                !TryGetComponent<PlayerMountCompanion>(out var companion) || companion.GetMountedLouieType() != MountedType.Purple)
            { ClearPurpleLineCast("interrupted"); return; }
            if (GamePauseController.IsPaused) return;
            purpleLineElapsed += deltaTime; purpleLineAge += unscaledDeltaTime;
            if ((purpleLinePending && purpleLineAge > Mathf.Clamp((float)NetworkTime.rtt * 3, 0.5f, 1)) ||
                (!purpleLinePending && purpleLineElapsed >= purpleLineDuration)) { ClearPurpleLineCast("completed_or_timeout"); return; }
            if (PurpleLinePoseActive) PresentPurpleLineCast(); else StopPurpleLineVisual();
        }

        void PresentPurpleLineCast()
        {
            if (!PurpleLinePoseActive || !TryGetComponent<PlayerMountCompanion>(out var companion)) return;
            var mount = companion.GetMountedLouieObject();
            var visual = mount != null ? mount.GetComponentInChildren<PurpleLouieBombLineAnimator>(true) : null;
            if (visual != purpleLineVisual) { StopPurpleLineVisual(); purpleLineVisual = visual; }
            purpleLineVisual?.PresentNetworkCast(purpleLineDirection, purpleLineElapsed / purpleLineDuration);
        }

        void StopPurpleLineVisual()
        {
            purpleLineVisual?.ForceStop(); purpleLineVisual = null;
            if (TryGetComponent<PlayerMountCompanion>(out var companion) && companion.GetMountedLouieObject() != null)
                foreach (var visual in companion.GetMountedLouieObject().GetComponentsInChildren<MountVisualController>(true)) visual.enabled = false;
        }

        void ClearPurpleLineCast(string reason)
        {
            if (!purpleLineActive) return;
            StopPurpleLineVisual(); purpleLineActive = purpleLinePending = false;
            TracePlacement("line_cast_prediction_end", purpleLineTick, transform.position, purpleLineElapsed * 1000, reason);
        }

        [Server]
        public void ServerLinePlacementSound(byte action)
        {
            PlayPlacementSoundLocally();
            uint tick = TryGetComponent<NetworkPlayerInput>(out var input) ? (action == 2 ? input.CurrentActionCTick : input.CurrentActionATick) : 0;
            RpcLinePlacementSound(tick, action);
        }

        [ClientRpc]
        void RpcLinePlacementSound(uint tick, byte action)
        {
            if (isServer || (isLocalPlayer && PlacementSoundWasPredicted(tick, action))) return;
            PlayPlacementSoundLocally();
        }
    }
}

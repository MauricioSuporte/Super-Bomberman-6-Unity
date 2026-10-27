using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace Assets.Scripts.Netcode
{
    public struct NetworkInputFrame
    {
        public uint tick;
        public int held, pressed;
    }

    /// <summary>Bounded input history; sequence comparisons also handle uint rollover.</summary>
    public sealed class NetworkInputHistory
    {
        public const int Capacity = 128;
        readonly List<NetworkInputFrame> frames = new(Capacity);
        public int Count => frames.Count;
        public NetworkInputFrame this[int index] => frames[index];
        public static bool IsNewer(uint tick, uint previous) => unchecked((int)(tick - previous)) > 0;
        public void Clear() => frames.Clear();
        public void Add(NetworkInputFrame frame)
        {
            if (frames.Count == Capacity) frames.RemoveAt(0);
            frames.Add(frame);
        }
        public void DiscardThrough(uint tick)
        {
            int count = 0;
            while (count < frames.Count && !IsNewer(frames[count].tick, tick)) count++;
            if (count > 0) frames.RemoveRange(0, count);
        }
    }

    // Credits accrue from server physics time, never from incoming packet count.
    // Unused steps cover short delivery bursts; floods cannot increase sustained speed.
    public sealed class NetworkInputStepBudget
    {
        const int MaxCredits = 8;
        int credits;
        public void Advance() => credits = Mathf.Min(MaxCredits, credits + 1);
        public bool TryConsume()
        {
            if (credits == 0) return false;
            credits--;
            return true;
        }
        public void Reset() => credits = 0;
    }

    /// <summary>Ticked owner prediction with authoritative movement rollback and input replay.</summary>
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(NetworkIdentity), typeof(NetworkPlayerSetup))]
    public partial class NetworkPlayerInput : NetworkBehaviour
    {
        static readonly PlayerAction[] Actions =
        {
            PlayerAction.MoveUp, PlayerAction.MoveDown, PlayerAction.MoveLeft,
            PlayerAction.MoveRight, PlayerAction.ActionA, PlayerAction.ActionB,
            PlayerAction.ActionC, PlayerAction.Select, PlayerAction.ActionL, PlayerAction.ActionR
        };
        const int ValidMask = (1 << 10) - 1;
        const int MaxQueuedTicks = 8;
        readonly Queue<(NetworkInputFrame frame, double receivedAt)> serverInputs = new(MaxQueuedTicks);
        readonly NetworkInputHistory pending = new();
        readonly NetworkInputStepBudget serverStepBudget = new();
        NetworkPlayerSetup setup;
        MovementController movement;
        uint localTick, receivedTick, processedTick, movementTick;
        int sampledHeld, pendingPressed, serverHeldMask;
        float nextSnapshot;
        uint lastSnapshotTick;
        Vector2 lastSnapshotPosition;
        bool sentSnapshot;
        double lastServerInputAt = double.NegativeInfinity;
        const double HeldInputGraceSeconds = 0.1;
        bool receivedSnapshot, serverAllowsPrediction;
        public bool ServerAllowsMovementPrediction => receivedSnapshot && serverAllowsPrediction;
        public uint CurrentActionATick { get; private set; }
        public uint CurrentActionBTick { get; private set; }
        public uint CurrentSelectTick { get; private set; }
        public uint CurrentActionCTick { get; private set; }
        public uint CurrentInputTick => isServer ? processedTick : localTick;
        public uint CurrentMovementTick => isServer ? processedTick : movementTick;
        public int PendingInputCount => pending.Count;
        public int QueuedInputCount => serverInputs.Count;

        static bool Blocked => GamePauseController.IsPaused || BombermanNetworkManager.BattleStartPending ||
            (BombermanNetworkManager.IsCoopStage && StageIntroTransition.Instance != null && StageIntroTransition.Instance.IntroRunning) ||
            (GameManager.Instance != null && GameManager.Instance.OnlineRoundPresentationActive);

        void Awake()
        {
            setup = GetComponent<NetworkPlayerSetup>();
            movement = GetComponent<MovementController>();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            // Host-local hardware and server COM retain their normal offline simulation loop.
            if (connectionToClient != null && connectionToClient != NetworkServer.localConnection)
                movement.SetNetworkTickDriven(true);
        }

        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();
            if (isServer) return;
            movement.SetNetworkTickDriven(true);
            // Only observers interpolate. The owner is placed by reconciliation, including
            // during authoritative-only movement such as launchers and mounting.
            if (TryGetComponent<NetworkTransformReliable>(out var sync)) sync.enabled = false;
        }

        void Update()
        {
            if (!NetSync.IsOnline || setup == null || movement == null) return;
            var input = PlayerInputManager.Instance;
            if (isLocalPlayer && !isServer && input != null)
            {
                sampledHeld = 0;
                int pressedNow = 0;
                if (Blocked) pendingPressed = 0;
                else for (int i = 0; i < Actions.Length; i++)
                {
                    if (input.Get(1, Actions[i])) sampledHeld |= 1 << i;
                    if (i >= 4 && input.GetDown(1, Actions[i])) { pendingPressed |= 1 << i; pressedNow |= 1 << i; }
                }
                if (Blocked) movement.SetPredictLocally(false);
                else
                {
                    var fx = GetComponent<NetworkBombFx>();
                    fx?.PredictBombAbilityInput(sampledHeld, pressedNow, unchecked(localTick + 1));
                    if ((pressedNow & 16) != 0) fx?.TryPredictPlacement(unchecked(localTick + 1));
                    if ((pressedNow & 128) != 0) fx?.TryPredictDismount(sampledHeld, unchecked(localTick + 1));
                    if ((pressedNow & 64) != 0)
                    {
                        fx?.TryPredictGreenDash(sampledHeld, unchecked(localTick + 1));
                        fx?.TryPredictPinkJump(sampledHeld, unchecked(localTick + 1));
                        fx?.TryPredictRedPunch(sampledHeld, unchecked(localTick + 1));
                        fx?.TryPredictTankShot(sampledHeld, unchecked(localTick + 1));
                        fx?.TryPredictBlackDash(sampledHeld, unchecked(localTick + 1));
                        fx?.TryPredictMoleStartup(sampledHeld, unchecked(localTick + 1));
                        fx?.TryPredictLinePlacement(unchecked(localTick + 1), sampledHeld, true);
                    }
                }
            }
            if (!isServer || connectionToClient == null || connectionToClient == NetworkServer.localConnection) return;
            // Pause uses unscaled Update; stale gameplay inputs must not survive resume.
            if (Blocked)
            {
                serverInputs.Clear(); processedTick = receivedTick; serverHeldMask = 0;
                serverStepBudget.Reset();
                return;
            }
            ApplyServerControls(input, serverHeldMask, 0);
        }

        void FixedUpdate()
        {
            if (!NetSync.IsOnline || movement == null || movement.Rigidbody == null) return;
            if (isLocalPlayer && !isServer && NetworkClient.ready)
            {
                var frame = new NetworkInputFrame { tick = ++localTick,
                    held = Blocked ? 0 : sampledHeld, pressed = Blocked ? 0 : pendingPressed };
                pendingPressed = 0;
                MotionDiagnosticProbe.NetworkInput(gameObject, "input_send", frame.held, frame.pressed);
                CmdControls(frame);
                // History retains the command actually sent. Rejection/completion
                // must be able to replay its original directional intent.
                frame = RecordLocalFrame(frame);
                bool predict = receivedSnapshot && serverAllowsPrediction && !Blocked && movement.CanPredictNetworkMovement && GetComponent<NetworkBombFx>()?.ClientPlayerMotionActive != true;
                movement.SetPredictLocally(predict);
                if (predict)
                {
                    movementTick = frame.tick;
                    var kickFx = GetComponent<NetworkBombFx>();
                    kickFx?.PrepareClientMovementTick(frame.tick, false, frame.held);
                    movement.RestorePredictedKickReference(kickFx != null ? kickFx.LastKickForTick(frame.tick) : null);
                    movement.SimulateNetworkMovementTick(frame.held);
                }
            }
            if (!isServer || connectionToClient == null || connectionToClient == NetworkServer.localConnection) return;
            serverStepBudget.Advance();
            if (Blocked)
            {
                serverInputs.Clear(); processedTick = receivedTick; serverHeldMask = 0;
                lastServerInputAt = double.NegativeInfinity;
                serverStepBudget.Reset();
                ApplyServerControls(PlayerInputManager.Instance, 0, 0);
                return;
            }
            int actionEdges = 0, steps = 0;
            while (steps < 4 && serverInputs.Count > 0 && serverStepBudget.TryConsume())
            {
                var queued = serverInputs.Dequeue();
                var frame = queued.frame;
                processedTick = frame.tick;
                serverHeldMask = frame.held;
                actionEdges |= frame.pressed;
                if ((frame.pressed & 16) != 0) CurrentActionATick = frame.tick;
                if ((frame.pressed & 32) != 0) CurrentActionBTick = frame.tick;
                if ((frame.pressed & 128) != 0) CurrentSelectTick = frame.tick;
                if ((frame.pressed & 64) != 0) CurrentActionCTick = frame.tick;
                lastServerInputAt = Time.realtimeSinceStartupAsDouble;
                MotionDiagnosticProbe.TickApplied(gameObject,
                    (Time.realtimeSinceStartupAsDouble - queued.receivedAt) * 1000);
                // Each acknowledged tick gets its complete motor step. Immediate
                // motor positioning prevents MovePosition overwriting earlier steps
                // when a packet batch is replayed before the physics solver runs.
                ApplyServerControls(PlayerInputManager.Instance, serverHeldMask, 0);
                movement.SimulateNetworkMovementTick(serverHeldMask);
                steps++;
            }
            if (steps == 0 && !RetainHeldInput(Time.realtimeSinceStartupAsDouble - lastServerInputAt))
                serverHeldMask = 0;
            // Held buttons survive ordinary message gaps. Only transmitted action
            // edges become GetDown, once for the gameplay Update in this render frame.
            ApplyServerControls(PlayerInputManager.Instance, serverHeldMask, actionEdges);
        }

        public static bool RetainHeldInput(double ageSeconds) => ageSeconds >= 0 && ageSeconds <= HeldInputGraceSeconds;

        void ApplyServerControls(PlayerInputManager input, int held, int pressed)
        {
            if (input == null) return;
            if (pressed != 0 && GameplayLatencyDiagnostics.Enabled)
            {
                var entry = GameplayLatencyDiagnostics.For("network_action_edge", movement,
                    detail: "explicit network press; held transitions never synthesize action edges");
                entry.held = held; entry.pressed = pressed;
                GameplayLatencyDiagnostics.Write(entry);
            }
            for (int i = 0; i < Actions.Length; i++)
            {
                input.SetSyntheticHeld(setup.PlayerId, Actions[i], (held & (1 << i)) != 0, explicitEdges: true);
                if (i >= 4 && (pressed & (1 << i)) != 0) input.TapSynthetic(setup.PlayerId, Actions[i]);
            }
        }

        [Command]
        void CmdControls(NetworkInputFrame frame)
        {
            MotionDiagnosticProbe.NetworkInput(gameObject, "input_receive", frame.held, frame.pressed, Blocked);
            if (!NetworkInputHistory.IsNewer(frame.tick, receivedTick)) return;
            receivedTick = frame.tick;
            if (((frame.held | frame.pressed) & ValidMask) != 0) BombermanNetworkManager.RegisterRoomActivity();
            if (Blocked) { processedTick = frame.tick; return; }
            frame.held &= ValidMask;
            frame.pressed &= ValidMask & ~15;
            // Bound queued time as well as memory after a network stall. Dropped ticks
            // are acknowledged only with the state of a later actually simulated tick.
            if (serverInputs.Count == MaxQueuedTicks)
            {
                var dropped = serverInputs.Dequeue();
                frame.pressed |= dropped.frame.pressed;
                MotionDiagnosticProbe.QueueEvent(gameObject, "queue_full_dropped_oldest_tick", dropped.frame.tick);
            }
            serverInputs.Enqueue((frame, Time.realtimeSinceStartupAsDouble));
        }

        void LateUpdate()
        {
            UpdateOwnerAuthorityPresentation();
            if (!isServer || Time.unscaledTime < nextSnapshot) return;
            nextSnapshot = Time.unscaledTime + 1f / 30f;
            SendMovementSnapshot(false);
        }

        [Server]
        public void ServerReconcileMovementNow()
        {
            nextSnapshot = Time.unscaledTime + 1f / 30f;
            SendMovementSnapshot(true);
        }

        void SendMovementSnapshot(bool force)
        {
            if (!isServer || connectionToClient == null || connectionToClient == NetworkServer.localConnection ||
                movement == null || movement.Rigidbody == null) return;
            // LateUpdate captures the result AFTER the physics step, never an unexecuted MovePosition.
            var state = movement.CaptureNetworkMovementState();
            state.canPredict &= !Blocked;
            state.continuousPresentation = authorityMotionToken != 0 && !state.canPredict;
            state.serverTime = NetworkTime.localTime;
            // A world force or its resolver can move a player without consuming
            // another input. Publish that state even when its acknowledgement repeats.
            if (!force && state.canPredict && sentSnapshot && processedTick == lastSnapshotTick &&
                state.position == lastSnapshotPosition) return;
            if (sentSnapshot && processedTick == lastSnapshotTick && state.position != lastSnapshotPosition && GameplayLatencyDiagnostics.Enabled)
            {
                var entry = GameplayLatencyDiagnostics.For("movement_world_snapshot", movement,
                    detail: "changed_position_with_same_input_ack");
                entry.delta = state.position - lastSnapshotPosition; entry.authoritativeTick = processedTick;
                GameplayLatencyDiagnostics.Write(entry);
            }
            sentSnapshot = true;
            lastSnapshotTick = processedTick;
            lastSnapshotPosition = state.position;
            var fx = GetComponent<NetworkBombFx>();
            TargetReconcile(connectionToClient, processedTick, state, fx?.CapturePlacementState() ?? default, fx?.CaptureMountActionState() ?? default);
        }

        NetworkInputFrame RecordLocalFrame(NetworkInputFrame frame)
        {
            pending.Add(frame);
            if (GetComponent<NetworkBombFx>()?.AbilityPredictionLocksMovement == true) frame.held &= ~15;
            return frame;
        }

        [TargetRpc]
        void TargetReconcile(NetworkConnectionToClient target, uint acknowledgedTick, MovementController.NetworkMovementState state, NetworkBombFx.PlacementState placement, NetworkBombFx.MountActionState mountActions)
            => ReceiveMovementState(acknowledgedTick, state, placement, mountActions);

        public void ReceiveMovementState(uint acknowledgedTick, MovementController.NetworkMovementState state, NetworkBombFx.PlacementState placement, NetworkBombFx.MountActionState mountActions)
        {
            if (isServer || !isLocalPlayer || movement == null || movement.Rigidbody == null) return;
            Vector2 before = movement.transform.position;
            receivedSnapshot = true;
            serverAllowsPrediction = state.canPredict;
            pending.DiscardThrough(acknowledgedTick);
            var kickFx = GetComponent<NetworkBombFx>();
            kickFx?.ReconcilePredictedKicks(acknowledgedTick);
            kickFx?.ReconcilePlacement(acknowledgedTick, placement);
            kickFx?.ReconcilePurpleLineCast(acknowledgedTick, state);
            kickFx?.RestoreClientKickPositions();
            movement.RestoreNetworkMovementState(state);
            kickFx?.ReconcileOwnerBoat(state);
            kickFx?.RestorePlayerBouncePosition();
            kickFx?.ReconcileGreenDash(acknowledgedTick, state);
            kickFx?.RestoreGreenDashPosition();
            kickFx?.ReconcilePinkJump(acknowledgedTick, state);
            kickFx?.RestorePinkJumpPosition();
            kickFx?.ReconcileMoleStartup(acknowledgedTick, state);
            kickFx?.ReconcileRedPunch(acknowledgedTick, state);
            kickFx?.ReconcileTankShot(acknowledgedTick, mountActions, state);
            kickFx?.ReconcileBlackDash(acknowledgedTick, state, mountActions);
            kickFx?.RestoreBlackDashPosition();
            kickFx?.ReconcileRiding(acknowledgedTick, state, mountActions);
            kickFx?.RestoreRidingPosition();
            SetOwnerAuthorityPresentation(state.continuousPresentation && !state.canPredict &&
                !movement.isDead && !movement.IsEndingStage && kickFx?.ClientPlayerMotionActive != true);
            // If an outage exceeded history, wait until the server catches up rather
            // than replaying a partial history as if no inputs had been lost.
            bool completeHistory = pending.Count == 0 || pending[0].tick == unchecked(acknowledgedTick + 1);
            bool predict = state.canPredict && completeHistory && !Blocked && movement.CanPredictNetworkMovement && GetComponent<NetworkBombFx>()?.ClientPlayerMotionActive != true;
            serverAllowsPrediction &= completeHistory;
            movement.SetPredictLocally(predict);
            if (predict)
                for (int i = 0; i < pending.Count; i++)
                {
                    movementTick = pending[i].tick;
                    int replayHeld = pending[i].held;
                    if (kickFx?.AbilityPredictionLocksMovement == true) replayHeld &= ~15;
                    kickFx?.PrepareClientMovementTick(movementTick, true, replayHeld);
                    movement.RestorePredictedKickReference(kickFx != null ? kickFx.LastKickForTick(movementTick) : null);
                    movement.SimulateNetworkMovementTick(replayHeld, replay: true);
                }
            kickFx?.RestoreClientKickPositions();
            kickFx?.RestoreRidingPosition();
            if (ownerAuthorityPresentation)
            {
                authorityPositionSync = authorityPositionSync != null ? authorityPositionSync : GetComponent<NetworkTransformReliable>();
                if (authorityPositionSync != null && authorityPositionSync.clientSnapshots.Count > 0)
                {
                    movement.Rigidbody.position = before;
                    transform.position = new Vector3(before.x, before.y, transform.position.z);
                }
            }
            kickFx?.RestorePausedBoatPosition(before);
            MotionDiagnosticProbe.Reconciliation(gameObject, before, acknowledgedTick, predict);
            MotionDiagnosticProbe.MovementReconciliation(gameObject, before, state.position, acknowledgedTick, predict);
        }

        public override void OnStopServer()
        {
            serverInputs.Clear();
            lastServerInputAt = double.NegativeInfinity;
            serverStepBudget.Reset();
            sentSnapshot = false;
            if (setup != null) PlayerInputManager.Instance?.ClearSyntheticPlayer(setup.PlayerId);
            if (movement != null) movement.SetNetworkTickDriven(false);
            base.OnStopServer();
        }

        public override void OnStopClient()
        {
            SetOwnerAuthorityPresentation(false);
            pending.Clear();
            if (!isServer && movement != null)
            {
                movement.SetPredictLocally(false); movement.SetNetworkTickDriven(false);
            }
            base.OnStopClient();
        }

        // Kicks stay authoritative; collision queries during replay never issue commands.
        public bool TryClientKickBomb(Bomb bomb, Vector2 dir, float tileSize, int obstacleMask) => false;
    }
}

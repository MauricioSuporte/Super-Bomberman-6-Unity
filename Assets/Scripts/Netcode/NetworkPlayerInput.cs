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

    /// <summary>Ticked owner prediction with authoritative movement rollback and input replay.</summary>
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(NetworkIdentity), typeof(NetworkPlayerSetup))]
    public class NetworkPlayerInput : NetworkBehaviour
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
        NetworkPlayerSetup setup;
        MovementController movement;
        uint localTick, receivedTick, processedTick;
        int sampledHeld, pendingPressed, serverHeldMask;
        float nextSnapshot;
        bool receivedSnapshot, serverAllowsPrediction;
        public uint CurrentInputTick => isServer ? processedTick : localTick;
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
                if (Blocked) pendingPressed = 0;
                else for (int i = 0; i < Actions.Length; i++)
                {
                    if (input.Get(1, Actions[i])) sampledHeld |= 1 << i;
                    if (i >= 4 && input.GetDown(1, Actions[i])) pendingPressed |= 1 << i;
                }
                if (Blocked) movement.SetPredictLocally(false);
            }
            if (!isServer || connectionToClient == null || connectionToClient == NetworkServer.localConnection) return;
            // Pause uses unscaled Update; stale gameplay inputs must not survive resume.
            if (Blocked)
            {
                serverInputs.Clear(); processedTick = receivedTick; serverHeldMask = 0;
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
                pending.Add(frame);
                MotionDiagnosticProbe.NetworkInput(gameObject, "input_send", frame.held, frame.pressed);
                CmdControls(frame);
                bool predict = receivedSnapshot && serverAllowsPrediction && !Blocked && movement.CanPredictNetworkMovement;
                movement.SetPredictLocally(predict);
                if (predict) movement.SimulateNetworkMovementTick(frame.held);
            }
            if (!isServer || connectionToClient == null || connectionToClient == NetworkServer.localConnection) return;
            int held = 0, pressed = 0;
            if (Blocked) { serverInputs.Clear(); processedTick = receivedTick; }
            else if (serverInputs.Count > 0)
            {
                var queued = serverInputs.Dequeue();
                var frame = queued.frame;
                processedTick = frame.tick; held = frame.held; pressed = frame.pressed;
                MotionDiagnosticProbe.TickApplied(gameObject,
                    (Time.realtimeSinceStartupAsDouble - queued.receivedAt) * 1000);
            }
            serverHeldMask = held;
            ApplyServerControls(PlayerInputManager.Instance, held, pressed);
            // One input step per server physics tick: clients cannot select dt or speed.
            movement.SimulateNetworkMovementTick(held);
        }

        void ApplyServerControls(PlayerInputManager input, int held, int pressed)
        {
            if (input == null) return;
            for (int i = 0; i < Actions.Length; i++)
            {
                input.SetSyntheticHeld(setup.PlayerId, Actions[i], (held & (1 << i)) != 0);
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
            if (serverInputs.Count == MaxQueuedTicks) serverInputs.Dequeue();
            serverInputs.Enqueue((frame, Time.realtimeSinceStartupAsDouble));
        }

        void LateUpdate()
        {
            if (!isServer || connectionToClient == null || connectionToClient == NetworkServer.localConnection ||
                movement == null || movement.Rigidbody == null || Time.unscaledTime < nextSnapshot) return;
            nextSnapshot = Time.unscaledTime + 1f / 30f;
            // LateUpdate captures the result AFTER the physics step, never an unexecuted MovePosition.
            var state = movement.CaptureNetworkMovementState();
            state.canPredict &= !Blocked;
            TargetReconcile(connectionToClient, processedTick, state);
        }

        [TargetRpc]
        void TargetReconcile(NetworkConnectionToClient target, uint acknowledgedTick, MovementController.NetworkMovementState state)
        {
            if (isServer || !isLocalPlayer || movement == null || movement.Rigidbody == null) return;
            receivedSnapshot = true;
            serverAllowsPrediction = state.canPredict;
            pending.DiscardThrough(acknowledgedTick);
            movement.RestoreNetworkMovementState(state);
            // If an outage exceeded history, wait until the server catches up rather
            // than replaying a partial history as if no inputs had been lost.
            bool completeHistory = pending.Count == 0 || pending[0].tick == unchecked(acknowledgedTick + 1);
            bool predict = state.canPredict && completeHistory && !Blocked && movement.CanPredictNetworkMovement;
            serverAllowsPrediction &= completeHistory;
            movement.SetPredictLocally(predict);
            if (!predict) return;
            for (int i = 0; i < pending.Count; i++) movement.SimulateNetworkMovementTick(pending[i].held, replay: true);
        }

        public override void OnStopServer()
        {
            serverInputs.Clear();
            if (setup != null) PlayerInputManager.Instance?.ClearSyntheticPlayer(setup.PlayerId);
            if (movement != null) movement.SetNetworkTickDriven(false);
            base.OnStopServer();
        }

        public override void OnStopClient()
        {
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

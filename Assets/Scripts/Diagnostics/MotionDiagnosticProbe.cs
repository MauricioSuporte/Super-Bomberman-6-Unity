using System.Collections.Generic;
using Assets.Scripts.Netcode;
using Mirror;
using UnityEngine;

// Attached to the shared Player prefab; does no sampling or I/O while capture is off.
[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
[RequireComponent(typeof(MovementController))]
public sealed class MotionDiagnosticProbe : MonoBehaviour
{
    static readonly PlayerAction[] Actions = { PlayerAction.MoveUp, PlayerAction.MoveDown, PlayerAction.MoveLeft,
        PlayerAction.MoveRight, PlayerAction.ActionA, PlayerAction.ActionB, PlayerAction.ActionC, PlayerAction.Select, PlayerAction.ActionL, PlayerAction.ActionR };
    struct SeenBomb { public Vector2 position; public double contactAt; public bool responded; }
    readonly Dictionary<Bomb, SeenBomb> bombs = new();
    readonly List<Bomb> expiredBombs = new();
    readonly Dictionary<Bomb, (double at, Vector2 direction)> kickContacts = new();
    double nextTickApply, kickDiagnosticUntil, nextQueueEvent, nextMovementReconcile;
    MovementController movement;
    NetworkPlayerInput networkInput;
    NetworkTransformReliable positionSync;
    Vector2 previousPosition, pendingDirection;
    double pendingAt, receivedAt = -1, nextSample, nextSimulation, nextNetworkLog, nextKickDetail, nextKickResult;
    int lastLocalHeld = -1, lastNetworkHeld = -1, lastAppliedHeld = -1, sentCount, receivedCount;
    bool pending, noMotionReported, sampling;
    string pendingContext;

    void Awake()
    {
        movement = GetComponent<MovementController>();
        networkInput = GetComponent<NetworkPlayerInput>();
        positionSync = GetComponent<NetworkTransformReliable>();
    }

    void Update()
    {
        if (!GameplayLatencyDiagnostics.Enabled) { sampling = false; return; }
        if (!sampling)
        {
            sampling = true;
            previousPosition = transform.position;
            lastLocalHeld = lastNetworkHeld = lastAppliedHeld = -1;
            receivedAt = -1; pending = false; bombs.Clear(); kickContacts.Clear();
            GameplayLatencyDiagnostics.Write(GameplayLatencyDiagnostics.For("actor_start", movement));
        }
        bool local = NetSync.IsOnline ? networkInput != null && networkInput.isLocalPlayer :
            !TryGetComponent<BattleModeComController>(out var computer) || !computer.enabled;
        if (!local || PlayerInputManager.Instance == null) return;
        int slot = NetSync.IsOnline ? 1 : movement.PlayerId;
        int held = 0;
        for (int i = 0; i < Actions.Length; i++)
            if (PlayerInputManager.Instance.Get(slot, Actions[i])) held |= 1 << i;
        if (held == lastLocalHeld) return;
        int previous = lastLocalHeld;
        lastLocalHeld = held;
        TrackResponse(held, previous);
        var entry = GameplayLatencyDiagnostics.For("input_edge", movement, detail: "accepted local keyboard/controller/touch input");
        entry.held = held;
        GameplayLatencyDiagnostics.Write(entry);
    }

    void TrackResponse(int held, int previous)
    {
        if ((held & 15) == (previous & 15)) return;
        pendingDirection = DirectionFromMask(held);
        pending = pendingDirection != Vector2.zero;
        pendingAt = Time.realtimeSinceStartupAsDouble;
        pendingContext = State();
        noMotionReported = false;
    }

    public static Vector2 DirectionFromMask(int mask) => new Vector2(
        ((mask & 8) != 0 ? 1 : 0) - ((mask & 4) != 0 ? 1 : 0),
        ((mask & 1) != 0 ? 1 : 0) - ((mask & 2) != 0 ? 1 : 0)).normalized;

    void LateUpdate()
    {
        if (!GameplayLatencyDiagnostics.Enabled || !sampling) return;
        double now = Time.realtimeSinceStartupAsDouble;
        Vector2 position = transform.position;
        Vector2 delta = position - previousPosition;
        previousPosition = position;
        if (pending && Vector2.Dot(delta, pendingDirection) > 0.001f)
        {
            var response = GameplayLatencyDiagnostics.For("input_to_motion", movement, detail: "at_input:" + pendingContext + ";at_response:" + State());
            response.durationMs = (now - pendingAt) * 1000;
            response.delta = delta;
            response.held = lastLocalHeld >= 0 ? lastLocalHeld : lastNetworkHeld;
            GameplayLatencyDiagnostics.Write(response);
            pending = false;
        }
        if (pending && !noMotionReported && now - pendingAt >= 0.5)
        {
            var wait = GameplayLatencyDiagnostics.For("input_no_motion_500ms", movement, detail: State());
            wait.durationMs = (now - pendingAt) * 1000;
            GameplayLatencyDiagnostics.Write(wait);
            noMotionReported = true;
        }
        bool sample = now >= nextSample;
        if (sample)
        {
            nextSample = now + 0.1;
            var entry = GameplayLatencyDiagnostics.For("motion_sample", movement, detail: State());
            entry.delta = delta;
            entry.held = lastLocalHeld >= 0 ? lastLocalHeld : lastNetworkHeld;
            entry.positionSnapshots = positionSync != null ? positionSync.clientSnapshots.Count : 0;
            entry.sentCommands = sentCount; entry.receivedCommands = receivedCount;
            entry.durationMs = receivedAt >= 0 ? (now - receivedAt) * 1000 : -1;
            GameplayLatencyDiagnostics.Write(entry);
        }
        ObserveBombs(now, sample);
    }

    public static string ClassifyPredictionBlock(MovementController motor, NetworkPlayerInput input, NetworkBombFx fx)
    {
        if (GamePauseController.IsPaused) return "paused";
        if (NetSync.Mode == NetSync.NetMode.Client && input != null && input.isLocalPlayer && !input.ServerAllowsMovementPrediction) return "server_authoritative";
        if (motor == null) return "missing_motor";
        if (motor.NetworkPredictionBlockReason != null) return motor.NetworkPredictionBlockReason;
        if (NetSync.Mode == NetSync.NetMode.Client && fx != null && fx.isLocalPlayer)
        {
            if (fx.ClientPlayerMotionActive) return "ability_motion";
            if (fx.AbilityPredictionLocksMovement) return "ability_pose";
        }
        return "none";
    }

    string State() => "prediction_block=" + ClassifyPredictionBlock(movement, networkInput, GetComponent<NetworkBombFx>()) +
        " mounted=" + movement.IsMounted + " boat=" + movement.HasBoatMovementContext + " speed=" + movement.speed + " locked=" + movement.InputLocked + " dead=" + movement.isDead +
        " paused=" + GamePauseController.IsPaused + " intro=" + (StageIntroTransition.Instance != null && StageIntroTransition.Instance.IntroRunning) +
        " kickAbility=" + (TryGetComponent<BombKickAbility>(out var kick) && kick.IsEnabled) + " conveyor=" + movement.NetworkConveyorDiagnosticState;

    void ObserveBombs(double now, bool sample)
    {
        foreach (var bomb in Bomb.ActiveBombs)
        {
            if (bomb == null || Vector2.Distance(transform.position, bomb.transform.position) > 2.5f) continue;
            bool known = bombs.TryGetValue(bomb, out var seen);
            if (!known) seen = new SeenBomb { position = bomb.transform.position, contactAt = -1 };
            Vector2 toward = (Vector2)bomb.transform.position - (Vector2)transform.position;
            int held = lastLocalHeld >= 0 ? lastLocalHeld : lastNetworkHeld;
            bool approaching = toward.magnitude < 1.6f && Vector2.Dot(toward.normalized, DirectionFromMask(held)) > 0.8f;
            if (approaching && seen.contactAt < 0)
            {
                seen.contactAt = now; seen.responded = false;
                var contact = GameplayLatencyDiagnostics.For("bomb_near_input", movement, bomb, State());
                contact.held = held;
                GameplayLatencyDiagnostics.Write(contact);
            }
            Vector2 delta = (Vector2)bomb.transform.position - seen.position;
            if (seen.contactAt >= 0 && !seen.responded && delta.sqrMagnitude > 0.0001f)
            {
                var response = GameplayLatencyDiagnostics.For("bomb_first_observed_motion", movement, bomb,
                    "Observed displacement after near input, not a kick acknowledgement; correlate bombNetId with server kick events.");
                response.durationMs = (now - seen.contactAt) * 1000; response.delta = delta;
                GameplayLatencyDiagnostics.Write(response);
                seen.responded = true;
            }
            if (!approaching) { seen.contactAt = -1; seen.responded = false; }
            if (sample && (approaching || delta.sqrMagnitude > 0.0001f))
            {
                var entry = GameplayLatencyDiagnostics.For("bomb_sample", movement, bomb,
                    "authoritative=" + NetSync.ShouldSimulateLocally + " kicked=" + bomb.IsBeingKicked + " solid=" + bomb.IsSolid);
                entry.delta = delta;
                GameplayLatencyDiagnostics.Write(entry);
            }
            seen.position = bomb.transform.position;
            bombs[bomb] = seen;
        }
        if (!sample) return;
        expiredBombs.Clear();
        foreach (var pair in bombs)
            if (pair.Key == null || Vector2.Distance(transform.position, pair.Key.transform.position) > 2.5f) expiredBombs.Add(pair.Key);
        foreach (var bomb in expiredBombs) { bombs.Remove(bomb); kickContacts.Remove(bomb); }
        expiredBombs.Clear();
        foreach (var pair in kickContacts)
            if (pair.Key == null || now - pair.Value.at > 5) expiredBombs.Add(pair.Key);
        foreach (var bomb in expiredBombs) kickContacts.Remove(bomb);
    }

    public static void NetworkInput(GameObject player, string kind, int held, int pressed = 0, bool blocked = false)
    {
        if (!GameplayLatencyDiagnostics.Enabled || !player.TryGetComponent<MotionDiagnosticProbe>(out var probe)) return;
        double now = Time.realtimeSinceStartupAsDouble;
        if (kind == "input_send") probe.sentCount++;
        if (kind == "input_receive")
        {
            probe.receivedCount++;
            probe.receivedAt = now;
            probe.TrackResponse(held, probe.lastNetworkHeld);
        }
        bool changed = held != probe.lastNetworkHeld;
        probe.lastNetworkHeld = held;
        if (!changed && pressed == 0 && now < probe.nextNetworkLog) return;
        probe.nextNetworkLog = now + 1;
        var entry = GameplayLatencyDiagnostics.For(kind, probe.movement, detail: "blocked=" + blocked);
        entry.held = held; entry.pressed = pressed;
        GameplayLatencyDiagnostics.Write(entry);
    }

    public static void TickApplied(GameObject player, double queueMs)
    {
        if (!GameplayLatencyDiagnostics.Enabled || !player.TryGetComponent<MotionDiagnosticProbe>(out var probe)) return;
        double now = Time.realtimeSinceStartupAsDouble;
        if (now < probe.nextTickApply) return;
        probe.nextTickApply = now + 0.1;
        var entry = GameplayLatencyDiagnostics.For("network_tick_apply", probe.movement,
            detail: "durationMs=server_queue_wait; excludes network transit");
        entry.durationMs = queueMs;
        GameplayLatencyDiagnostics.Write(entry);
    }

    public static void KickContact(MovementController movement, Bomb bomb, Vector2 direction, float distance)
    {
        if (!GameplayLatencyDiagnostics.Enabled || !movement.TryGetComponent<MotionDiagnosticProbe>(out var probe)) return;
        if (probe.kickContacts.TryGetValue(bomb, out var previous) && previous.direction == direction) return;
        probe.kickContacts[bomb] = (Time.realtimeSinceStartupAsDouble, direction);
        probe.kickDiagnosticUntil = Time.realtimeSinceStartupAsDouble + 2;
        var entry = GameplayLatencyDiagnostics.For("kick_client_contact", movement, bomb,
            "candidate at predicted position; not permission to kick");
        entry.kickDistance = distance;
        GameplayLatencyDiagnostics.Write(entry);
    }

    public static void KickFeedback(GameObject player, Bomb bomb, bool moving, uint authoritativeTick, double ageMs)
    {
        if (!GameplayLatencyDiagnostics.Enabled || !player.TryGetComponent<MotionDiagnosticProbe>(out var probe)) return;
        var entry = GameplayLatencyDiagnostics.For(moving ? "kick_segment_receive" : "kick_stop_receive", probe.movement, bomb);
        entry.authoritativeTick = authoritativeTick;
        entry.messageAgeMs = ageMs;
        entry.accepted = true;
        if (moving && probe.kickContacts.TryGetValue(bomb, out var contact))
        {
            entry.durationMs = (Time.realtimeSinceStartupAsDouble - contact.at) * 1000;
            probe.kickContacts.Remove(bomb);
        }
        GameplayLatencyDiagnostics.Write(entry);
    }

    public static void Reconciliation(GameObject player, Vector2 before, uint acknowledgedTick, bool replayed)
    {
        if (!GameplayLatencyDiagnostics.Enabled || !player.TryGetComponent<MotionDiagnosticProbe>(out var probe) ||
            Time.realtimeSinceStartupAsDouble > probe.kickDiagnosticUntil) return;
        var entry = GameplayLatencyDiagnostics.For("kick_player_reconcile", probe.movement,
            detail: replayed ? "after_restore_and_replay" : "authoritative_only");
        entry.delta = (Vector2)player.transform.position - before;
        entry.authoritativeTick = acknowledgedTick;
        GameplayLatencyDiagnostics.Write(entry);
    }

    public static void MovementReconciliation(GameObject player, Vector2 before, Vector2 approved, uint tick, bool replayed)
    {
        if (!GameplayLatencyDiagnostics.Enabled || !player.TryGetComponent<MotionDiagnosticProbe>(out var probe)) return;
        Vector2 correction = (Vector2)player.transform.position - before;
        if (correction.sqrMagnitude < 0.000001f) return;
        double now = Time.realtimeSinceStartupAsDouble;
        if (now < probe.nextMovementReconcile) return;
        probe.nextMovementReconcile = now + 0.1;
        var entry = GameplayLatencyDiagnostics.For("movement_reconcile", probe.movement,
            detail: replayed ? "after_restore_and_replay" : "authoritative_only");
        entry.position = approved;
        entry.delta = correction;
        entry.predictionError = approved - before;
        entry.authoritativeTick = tick;
        GameplayLatencyDiagnostics.Write(entry);
    }

    public static void QueueEvent(GameObject player, string reason, uint tick)
    {
        if (!GameplayLatencyDiagnostics.Enabled || !player.TryGetComponent<MotionDiagnosticProbe>(out var probe)) return;
        double now = Time.realtimeSinceStartupAsDouble;
        if (now < probe.nextQueueEvent) return;
        probe.nextQueueEvent = now + 0.25;
        var entry = GameplayLatencyDiagnostics.For("network_input_queue", probe.movement, detail: reason);
        entry.authoritativeTick = tick;
        GameplayLatencyDiagnostics.Write(entry);
    }

    public static void InputApplied(GameObject player, int held, double ageMs)
    {
        if (!GameplayLatencyDiagnostics.Enabled || !player.TryGetComponent<MotionDiagnosticProbe>(out var probe) || held == probe.lastAppliedHeld) return;
        probe.lastAppliedHeld = held;
        var entry = GameplayLatencyDiagnostics.For("input_apply", probe.movement);
        entry.held = held; entry.durationMs = ageMs;
        GameplayLatencyDiagnostics.Write(entry);
    }

    public static void SimulationTick(MovementController movement)
    {
        if (!GameplayLatencyDiagnostics.Enabled || !movement.TryGetComponent<MotionDiagnosticProbe>(out var probe)) return;
        double now = Time.realtimeSinceStartupAsDouble;
        if (now < probe.nextSimulation) return;
        probe.nextSimulation = now + 0.1;
        var entry = GameplayLatencyDiagnostics.For("simulation_tick", movement, detail: probe.State());
        entry.durationMs = probe.receivedAt >= 0 ? (now - probe.receivedAt) * 1000 : -1;
        GameplayLatencyDiagnostics.Write(entry);
    }

    public static void KickDetail(MovementController movement, string message, bool verbose)
    {
        if (!GameplayLatencyDiagnostics.Enabled || verbose || movement == null || !movement.TryGetComponent<MotionDiagnosticProbe>(out var probe)) return;
        double now = Time.realtimeSinceStartupAsDouble;
        // At most four detail lines per player/second; authoritative accept/reject events are separate.
        if (now < probe.nextKickDetail) return;
        probe.nextKickDetail = now + 0.25;
        GameplayLatencyDiagnostics.Write(GameplayLatencyDiagnostics.For("kick_detail", movement, detail: message));
    }

    public static void KickResult(MovementController movement, Bomb bomb, bool accepted, float distance, float axisOffset, float perpendicularOffset)
    {
        if (!GameplayLatencyDiagnostics.Enabled || movement == null || !movement.TryGetComponent<MotionDiagnosticProbe>(out var probe)) return;
        double now = Time.realtimeSinceStartupAsDouble;
        if (!accepted && now < probe.nextKickResult) return;
        probe.nextKickResult = now + 0.25;
        var entry = GameplayLatencyDiagnostics.For("kick_result", movement, bomb);
        entry.accepted = accepted; entry.kickDistance = distance;
        entry.kickAxisOffset = axisOffset; entry.kickPerpendicularOffset = perpendicularOffset;
        GameplayLatencyDiagnostics.Write(entry);
    }
}

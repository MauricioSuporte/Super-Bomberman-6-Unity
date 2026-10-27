#if UNITY_EDITOR
using System.Reflection;
using Assets.Scripts.Netcode;
using Mirror;
using NUnit.Framework;
using UnityEngine;

public sealed partial class OnlineRoomEditModeTests
{
    [Test]
    public void OrdinaryWalkingSendsOnlyOnePusherPresenceByte()
    {
        using var writer = NetworkWriterPool.Get(); writer.Write(default(PusherPatrolAndPush.NetworkPushState));
        Assert.AreEqual(1, writer.Position);
        Assert.AreEqual(0, new NetworkReader(writer.ToArray()).Read<PusherPatrolAndPush.NetworkPushState>().sourceId);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ApprovedPusherContinuesImmediatelyForTheOwnerWithoutRunningTheSource(bool circle)
    {
        using var fixture = new PusherFixture(circle);
        var state = PreparePusherOwner(fixture);
        var sourceBody = fixture.source.GetComponent<Rigidbody2D>();
        Vector2 sourcePosition = sourceBody.position;
        Vector2 before = fixture.body.position;
        fixture.movement.SimulateNetworkMovementTick(0);
        Assert.That(fixture.body.position.y - before.y, Is.EqualTo(state.pusher.velocity.y * Time.fixedDeltaTime).Within(0.001f));
        Assert.AreEqual(fixture.body.position, (Vector2)fixture.actor.transform.position);
        Assert.AreEqual(sourcePosition, sourceBody.position, "The disabled client mechanism must never be advanced by the owner motor.");
        Assert.IsFalse(fixture.source.enabled);
        Assert.IsTrue(fixture.movement.HasPusherMovementContext);
    }

    [Test]
    public void PusherOwnerCanWalkWhileTheConfirmedForceContinues()
    {
        using var fixture = new PusherFixture(circle: true);
        var state = PreparePusherOwner(fixture); Vector2 before = fixture.body.position;
        var walkingOnly = state; walkingOnly.pusher = default;
        fixture.movement.RestoreNetworkMovementState(walkingOnly); fixture.movement.SimulateNetworkMovementTick(8);
        Vector2 walked = fixture.body.position;
        fixture.movement.RestoreNetworkMovementState(state); fixture.movement.SimulateNetworkMovementTick(8);
        Assert.Greater(fixture.body.position.x, before.x);
        Assert.That(fixture.body.position.x, Is.EqualTo(walked.x).Within(0.001f));
        Assert.That(fixture.body.position.y - walked.y, Is.EqualTo(state.pusher.velocity.y * Time.fixedDeltaTime).Within(0.001f),
            "Walking can recenter the perpendicular row; compare the added force with the same motor step without it.");
        Assert.IsFalse(fixture.movement.InputLocked);
        Assert.IsFalse(fixture.movement.ExternalMovementOverride);
    }

    [Test]
    public void PusherReconciliationRestoresTheForceHistoryBeforeReplayingInputs()
    {
        using var fixture = new PusherFixture(circle: true);
        var state = PreparePusherOwner(fixture);
        fixture.movement.SimulateNetworkMovementTick(8); fixture.movement.SimulateNetworkMovementTick(8);
        Vector2 expected = fixture.body.position;
        using var writer = NetworkWriterPool.Get(); writer.Write(state);
        state = new NetworkReader(writer.ToArray()).Read<MovementController.NetworkMovementState>();
        fixture.movement.RestoreNetworkMovementState(state);
        fixture.movement.SimulateNetworkMovementTick(8, replay: true); fixture.movement.SimulateNetworkMovementTick(8, replay: true);
        Assert.That(Vector2.Distance(expected, fixture.body.position), Is.LessThan(0.001f));
        Assert.AreEqual(state.pusher.sourceId, fixture.movement.CaptureNetworkMovementState().pusher.sourceId);
        var captured = fixture.movement.CaptureNetworkMovementState();
        Assert.Less(captured.pusher.remaining, state.pusher.remaining);
        Assert.Greater(captured.pusher.center.y, state.pusher.center.y);
    }

    [Test]
    public void ServerInputCatchupNeverAppliesAnApprovedPusherASecondTime()
    {
        using var fixture = new PusherFixture();
        fixture.source.enabled = true;
        typeof(PusherPatrolAndPush).GetMethod("FixedUpdate", PusherFlags).Invoke(fixture.source, null); fixture.AdvancePhysics();
        var state = fixture.movement.CaptureNetworkMovementState(); Assert.IsTrue(state.pusher.IsValid);
        fixture.movement.enabled = true; fixture.movement.RestoreNetworkMovementState(state);
        Vector2 before = fixture.body.position;
        for (int i = 0; i < 4; i++) fixture.movement.SimulateNetworkMovementTick(0);
        Assert.AreEqual(before, fixture.body.position);
    }

    [Test]
    public void PusherPredictionExpiresAndNeverInventsTheNextPatrolTurn()
    {
        using var fixture = new PusherFixture();
        var state = PreparePusherOwner(fixture); Vector2 before = fixture.body.position;
        for (int i = 0; i < 40; i++) fixture.movement.SimulateNetworkMovementTick(0);
        Assert.LessOrEqual(fixture.body.position.y - before.y, state.pusher.velocity.y * PusherPatrolAndPush.NetworkContinuationSeconds + 0.001f);
        Assert.IsFalse(fixture.movement.HasPusherMovementContext);
        Vector2 stopped = fixture.body.position;
        fixture.movement.SimulateNetworkMovementTick(0); Assert.AreEqual(stopped, fixture.body.position);
    }

    [Test]
    public void PusherStopsBeforeLeavingItsConfirmedSafeCell()
    {
        using var fixture = new PusherFixture();
        var state = PreparePusherOwner(fixture);
        state.position = state.pusher.safeCell + Vector2.up * 0.49f;
        state.pusher.center.y += state.position.y - fixture.body.position.y;
        fixture.movement.RestoreNetworkMovementState(state); Vector2 before = fixture.body.position;
        fixture.movement.SimulateNetworkMovementTick(0);
        Assert.AreEqual(before, fixture.body.position);
        Assert.IsFalse(fixture.movement.HasPusherMovementContext);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void PusherContinuationStopsAtKnownWallsAndHoles(bool hole)
    {
        using var fixture = new PusherFixture(); PreparePusherOwner(fixture);
        var obstacle = new GameObject("PusherPredictionBoundary"); obstacle.layer = LayerMask.NameToLayer("Stage");
        if (hole) obstacle.tag = "Hole";
        try
        {
            obstacle.transform.position = fixture.body.position + Vector2.up * 0.25f;
            var collider = obstacle.AddComponent<BoxCollider2D>(); collider.size = Vector2.one * 0.1f; collider.isTrigger = hole;
            fixture.movement.obstacleMask = LayerMask.GetMask("Stage"); fixture.movement.RefreshObstacleContactFilter(); Physics2D.SyncTransforms();
            Vector2 before = fixture.body.position; fixture.movement.SimulateNetworkMovementTick(0);
            Assert.AreEqual(before, fixture.body.position);
            Assert.IsFalse(fixture.movement.HasPusherMovementContext);
            Assert.IsFalse(fixture.movement.isDead, "A read-only boundary query must not apply hole death locally.");
        }
        finally { Object.DestroyImmediate(obstacle); }
    }

    [Test]
    public void MovingAwayFromApprovedContactDoesNotKeepDraggingTheOwner()
    {
        using var fixture = new PusherFixture(); PreparePusherOwner(fixture);
        fixture.body.position += Vector2.right * 0.49f; fixture.actor.transform.position = fixture.body.position; Physics2D.SyncTransforms();
        Vector2 before = fixture.body.position; fixture.movement.SimulateNetworkMovementTick(0);
        Assert.AreEqual(before, fixture.body.position);
        Assert.IsFalse(fixture.movement.HasPusherMovementContext);
    }

    [Test]
    public void PusherPauseFreezesBothForceAndItsRemainingBudget()
    {
        using var fixture = new PusherFixture(); var state = PreparePusherOwner(fixture);
        PusherFixture.Paused.SetValue(null, true);
        for (int i = 0; i < 5; i++) fixture.movement.SimulateNetworkMovementTick(0);
        Assert.AreEqual(state.position, fixture.body.position);
        Assert.AreEqual(state.pusher.remaining, fixture.movement.CaptureNetworkMovementState().pusher.remaining);
        PusherFixture.Paused.SetValue(null, false); fixture.movement.SimulateNetworkMovementTick(0);
        Assert.Greater(fixture.body.position.y, state.position.y);
    }

    [Test]
    public void ApprovedStopBounceLockAndNetworkShutdownReleasePusherPrediction()
    {
        using var fixture = new PusherFixture(); var state = PreparePusherOwner(fixture);
        var stop = state; stop.pusher = default;
        fixture.movement.RestoreNetworkMovementState(stop); fixture.movement.SimulateNetworkMovementTick(0);
        Assert.AreEqual(state.position, fixture.body.position);
        fixture.movement.RestoreNetworkMovementState(state); fixture.movement.SetInputLocked(true, false);
        fixture.movement.SimulateNetworkMovementTick(0); Assert.AreEqual(state.position, fixture.body.position);
        Assert.IsFalse(fixture.movement.HasPusherMovementContext);
        fixture.movement.SetInputLocked(false, false); fixture.movement.RestoreNetworkMovementState(state);
        fixture.movement.SetNetworkTickDriven(false); Assert.IsFalse(fixture.movement.HasPusherMovementContext);
    }

    [Test]
    public void InvalidAndUnsupportedPusherContextsNeverEnableLocalWorldSimulation()
    {
        using var fixture = new PusherFixture(); var state = PreparePusherOwner(fixture);
        state.pusher.velocity = new Vector2(float.NaN, 1); fixture.movement.RestoreNetworkMovementState(state);
        fixture.movement.SimulateNetworkMovementTick(0); Assert.AreEqual(state.position, fixture.body.position);
        Assert.IsFalse(fixture.movement.HasPusherMovementContext);
        NetSync.Mode = NetSync.NetMode.Host; fixture.source.enabled = true;
        fixture.source.GetComponent<BoxCollider2D>().edgeRadius = 0.2f;
        Assert.AreEqual(0, fixture.source.CaptureNetworkContinuation(fixture.movement).sourceId);
    }

    [Test]
    public void MultiplePusherContactsAndAnExpiredWorldReceiptDoNotGrantContinuation()
    {
        using var fixture = new PusherFixture(); fixture.source.enabled = true;
        var other = PusherFixture.MakeSource("AmbiguousPusher"); other.enabled = true;
        try
        {
            fixture.movement.RegisterNetworkPusherContact(fixture.source);
            Assert.IsTrue(fixture.movement.CaptureNetworkMovementState().pusher.IsValid);
            fixture.movement.RegisterNetworkPusherContact(other);
            Assert.AreEqual(0, fixture.movement.CaptureNetworkMovementState().pusher.sourceId);
            typeof(MovementController).GetField("approvedPusherStep", PusherFlags).SetValue(fixture.movement, Time.fixedTimeAsDouble - 1);
            fixture.movement.RegisterNetworkPusherContact(fixture.source);
            Assert.IsTrue(fixture.movement.CaptureNetworkMovementState().pusher.IsValid);
            typeof(MovementController).GetField("approvedPusherStep", PusherFlags).SetValue(fixture.movement, Time.fixedTimeAsDouble - 1);
            Assert.AreEqual(0, fixture.movement.CaptureNetworkMovementState().pusher.sourceId);
        }
        finally { Object.DestroyImmediate(other.gameObject); }
    }

    [TestCase(-1)]
    [TestCase(1)]
    public void DelayedAndFuturePusherApprovalsCannotRestartAnExpiredContact(int offset)
    {
        using var fixture = new PusherFixture(); var state = PreparePusherOwner(fixture);
        var connectionState = typeof(NetworkClient).GetField("connectState", BindingFlags.Static | BindingFlags.NonPublic);
        object previous = connectionState.GetValue(null);
        try
        {
            connectionState.SetValue(null, System.Enum.Parse(connectionState.FieldType, "Connected"));
            state.serverTime = NetworkBombFx.KickServerNow + offset;
            fixture.movement.RestoreNetworkMovementState(state); fixture.movement.SimulateNetworkMovementTick(0);
            Assert.AreEqual(state.position, fixture.body.position); Assert.IsFalse(fixture.movement.HasPusherMovementContext);
            state.serverTime = NetworkBombFx.KickServerNow;
            fixture.movement.RestoreNetworkMovementState(state); fixture.movement.SimulateNetworkMovementTick(0);
            Assert.Greater(fixture.body.position.y, state.position.y, "A genuinely fresh confirmation must still work.");
        }
        finally { connectionState.SetValue(null, previous); }
    }

    [Test]
    public void ConfirmedPusherEndpointBoundsTheOwnerWithoutPredictingAReversal()
    {
        using var fixture = new PusherFixture(); var state = PreparePusherOwner(fixture, endpointDistance: 0.1f);
        Assert.Less(state.pusher.remaining, PusherPatrolAndPush.NetworkContinuationSeconds);
        for (int i = 0; i < 20; i++) fixture.movement.SimulateNetworkMovementTick(0);
        Assert.LessOrEqual(fixture.body.position.y - state.position.y, state.pusher.velocity.y * state.pusher.remaining + 0.001f);
        Assert.IsFalse(fixture.movement.HasPusherMovementContext);
    }

    [Test]
    public void PusherHeartbeatAndClearAreSentWithTheSameInputAckAndPosition()
    {
        using var fixture = new PusherFixture(); fixture.source.enabled = true;
        fixture.movement.RegisterNetworkPusherContact(fixture.source);
        Assert.IsTrue(fixture.movement.CaptureNetworkMovementState().pusher.IsValid);
        bool wasServer = NetworkServer.active; var oldTransport = Transport.active;
        var transportObject = new GameObject("PusherClearTransport"); transportObject.SetActive(false);
        int messages = 0;
        System.Action<NetworkDiagnostics.MessageInfo> count = info => { if (info.message is RpcMessage) messages++; };
        try
        {
            Transport.active = transportObject.AddComponent<TelepathyTransport>();
            typeof(NetworkServer).GetProperty("active").SetValue(null, true);
            typeof(NetworkIdentity).GetProperty("connectionToClient").SetValue(fixture.identity, new NetworkConnectionToClient(9846));
            var send = typeof(NetworkPlayerInput).GetMethod("SendMovementSnapshot", PusherFlags);
            NetworkDiagnostics.OutMessageEvent += count;
            send.Invoke(fixture.input, new object[] { false }); Assert.AreEqual(1, messages);
            send.Invoke(fixture.input, new object[] { false }); Assert.AreEqual(2, messages, "A confirmed ongoing contact needs fresh phase despite an unchanged ack.");
            fixture.source.enabled = false;
            send.Invoke(fixture.input, new object[] { false }); Assert.AreEqual(3, messages, "A stop must revoke the previous contact even without movement or input.");
            send.Invoke(fixture.input, new object[] { false }); Assert.AreEqual(3, messages);
        }
        finally
        {
            NetworkDiagnostics.OutMessageEvent -= count;
            typeof(NetworkIdentity).GetProperty("connectionToClient").SetValue(fixture.identity, null);
            typeof(NetworkServer).GetProperty("active").SetValue(null, wasServer);
            Transport.active = oldTransport; Object.DestroyImmediate(transportObject);
        }
    }

    static MovementController.NetworkMovementState PreparePusherOwner(PusherFixture fixture, float endpointDistance = 0)
    {
        fixture.source.enabled = true;
        typeof(PusherPatrolAndPush).GetMethod("FixedUpdate", PusherFlags).Invoke(fixture.source, null); fixture.AdvancePhysics();
        if (endpointDistance > 0)
            typeof(PusherPatrolAndPush).GetField("pointB", PusherFlags).SetValue(fixture.source,
                fixture.source.GetComponent<Rigidbody2D>().position + Vector2.up * endpointDistance);
        var state = fixture.movement.CaptureNetworkMovementState(); Assert.IsTrue(state.pusher.IsValid, "Native confirmed contact must create the fixture's approval.");
        using (var writer = NetworkWriterPool.Get()) { writer.Write(state); state = new NetworkReader(writer.ToArray()).Read<MovementController.NetworkMovementState>(); }
        fixture.source.enabled = false; NetSync.Mode = NetSync.NetMode.Client;
        typeof(NetworkIdentity).GetProperty("isServer").SetValue(fixture.identity, false);
        fixture.movement.enabled = true; fixture.movement.SetPredictLocally(true);
        fixture.movement.RestoreNetworkMovementState(state);
        return state;
    }
}
#endif

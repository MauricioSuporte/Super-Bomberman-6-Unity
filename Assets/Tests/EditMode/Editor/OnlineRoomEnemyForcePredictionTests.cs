#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using Assets.Scripts.Netcode;
using Mirror;
using NUnit.Framework;
using UnityEngine;

public sealed partial class OnlineRoomEditModeTests
{
    [Test]
    public void WalkingEnemyForceDescriptorCostsOnlyItsAbsenceByte()
    {
        using var writer = NetworkWriterPool.Get(); writer.Write(default(NetworkEnemyForceState));
        Assert.AreEqual(1, writer.Position); Assert.AreEqual(NetworkEnemyForceKind.None, new NetworkReader(writer.ToArray()).Read<NetworkEnemyForceState>().kind);
    }

    [TestCase(NetworkEnemyForceKind.Hurricane)]
    [TestCase(NetworkEnemyForceKind.Magnet)]
    [TestCase(NetworkEnemyForceKind.Bubble)]
    public void EnemyForceOwnerContinuesConfirmedMotionWithoutRunningTheEnemy(NetworkEnemyForceKind kind)
    {
        using var fixture = new EnemyForceFixture(kind); var state = fixture.PrepareOwner();
        Vector2 before = fixture.player.body.position, enemyBefore = fixture.enemyBody.position;
        fixture.player.movement.SimulateNetworkMovementTick(0);
        float sign = kind == NetworkEnemyForceKind.Bubble ? 1 : -1;
        Assert.That(fixture.player.body.position.y - before.y, Is.EqualTo(sign * state.enemyForce.speed * Time.fixedDeltaTime).Within(0.001f));
        Assert.AreEqual(fixture.player.body.position, (Vector2)fixture.player.actor.transform.position);
        Assert.AreEqual(enemyBefore, fixture.enemyBody.position); Assert.IsFalse(fixture.enemy.enabled);
        Assert.IsFalse(fixture.player.movement.InputLocked); Assert.IsFalse(fixture.player.movement.ExternalMovementOverride);
    }

    [TestCase(NetworkEnemyForceKind.Hurricane)]
    [TestCase(NetworkEnemyForceKind.Magnet)]
    [TestCase(NetworkEnemyForceKind.Bubble)]
    public void EnemyForceReplayRestoresBudgetAndLeavesAuthoritativeWorldUntouched(NetworkEnemyForceKind kind)
    {
        using var fixture = new EnemyForceFixture(kind); var state = fixture.PrepareOwner();
        var random = Random.state;
        fixture.player.movement.SimulateNetworkMovementTick(0); fixture.player.movement.SimulateNetworkMovementTick(0);
        Vector2 expected = fixture.player.body.position;
        var expectedBudget = fixture.player.movement.CaptureNetworkMovementState().enemyForce.remaining;
        using (var writer = NetworkWriterPool.Get()) { writer.Write(state); state = new NetworkReader(writer.ToArray()).Read<MovementController.NetworkMovementState>(); }
        fixture.player.movement.RestoreNetworkMovementState(state);
        fixture.player.movement.SimulateNetworkMovementTick(0, replay: true); fixture.player.movement.SimulateNetworkMovementTick(0, replay: true);
        Assert.That(Vector2.Distance(expected, fixture.player.body.position), Is.LessThan(0.001f));
        Assert.AreEqual(expectedBudget, fixture.player.movement.CaptureNetworkMovementState().enemyForce.remaining);
        Assert.AreEqual(random, Random.state);
    }

    [TestCase(NetworkEnemyForceKind.Hurricane)]
    [TestCase(NetworkEnemyForceKind.Magnet)]
    [TestCase(NetworkEnemyForceKind.Bubble)]
    public void NativeEnemyForceIsNeverReappliedInServerInputCatchup(NetworkEnemyForceKind kind)
    {
        using var fixture = new EnemyForceFixture(kind); fixture.NativeStep(); fixture.player.AdvancePhysics();
        var state = fixture.player.movement.CaptureNetworkMovementState(); Assert.IsTrue(state.enemyForce.IsValid);
        fixture.player.movement.enabled = true; fixture.player.movement.RestoreNetworkMovementState(state);
        Vector2 before = fixture.player.body.position;
        for (int i = 0; i < 4; i++) fixture.player.movement.SimulateNetworkMovementTick(0);
        Assert.AreEqual(before, fixture.player.body.position);
    }

    [TestCase(NetworkEnemyForceKind.Hurricane)]
    [TestCase(NetworkEnemyForceKind.Magnet)]
    [TestCase(NetworkEnemyForceKind.Bubble)]
    public void PendingEnemyMovePositionDoesNotPublishUnexecutedLargeDisplacement(NetworkEnemyForceKind kind)
    {
        using var fixture = new EnemyForceFixture(kind); Vector2 before = fixture.player.body.position;
        fixture.NativeStep(); Assert.AreEqual(before, fixture.player.body.position);
        Assert.AreEqual(0, fixture.player.movement.CaptureNetworkMovementState().enemyForce.sourceId);
        fixture.player.AdvancePhysics(); Assert.IsTrue(fixture.player.movement.CaptureNetworkMovementState().enemyForce.IsValid);
    }

    [TestCase(NetworkEnemyForceKind.Hurricane)]
    [TestCase(NetworkEnemyForceKind.Magnet)]
    [TestCase(NetworkEnemyForceKind.Bubble)]
    public void OwnerCanWalkAcrossEnemyForceWhileKeepingTheSameMotorContribution(NetworkEnemyForceKind kind)
    {
        using var fixture = new EnemyForceFixture(kind); var state = fixture.PrepareOwner();
        var walkingOnly = state; walkingOnly.enemyForce = default;
        fixture.player.movement.RestoreNetworkMovementState(walkingOnly); fixture.player.movement.SimulateNetworkMovementTick(8);
        Vector2 walked = fixture.player.body.position;
        fixture.player.movement.RestoreNetworkMovementState(state); fixture.player.movement.SimulateNetworkMovementTick(8);
        Assert.That(fixture.player.body.position.x, Is.EqualTo(walked.x).Within(0.005f));
        Assert.Greater(fixture.player.body.position.x, state.position.x);
        if (kind == NetworkEnemyForceKind.Hurricane && NetworkEnemyForceState.IsFleeing(Vector2.right, walked - state.enemyForce.anchor))
        {
            Assert.AreEqual(walked, fixture.player.body.position, "The native radial flee rule can classify a sideways step as fleeing.");
            Assert.AreEqual(0.5f, fixture.player.movement.CaptureNetworkMovementState().speedMultiplier);
        }
        else Assert.Greater(Vector2.Distance(fixture.player.body.position, walked), 0.005f);
    }

    [Test]
    public void HurricaneFleePreviewUsesTheNativeRuleAndOnlyHistoricalSpeedContext()
    {
        using var fixture = new EnemyForceFixture(NetworkEnemyForceKind.Hurricane); var state = fixture.PrepareOwner();
        var movement = fixture.player.movement;
        float nativeMultiplier = (float)typeof(MovementController).GetField("externalMovementSpeedMultiplier", PusherFlags).GetValue(movement);
        movement.SimulateNetworkMovementTick(1);
        var predicted = movement.CaptureNetworkMovementState();
        Assert.Greater(fixture.player.body.position.y, state.position.y);
        Assert.AreEqual(0.5f, predicted.speedMultiplier); Assert.Greater(predicted.speedModifierRemaining, 0);
        Assert.AreEqual(nativeMultiplier, typeof(MovementController).GetField("externalMovementSpeedMultiplier", PusherFlags).GetValue(movement));
        movement.RestoreNetworkMovementState(state); movement.SimulateNetworkMovementTick(1, replay: true);
        Assert.That(Vector2.Distance(predicted.position, fixture.player.body.position), Is.LessThan(0.001f));
    }

    [Test]
    public void AConfirmedHurricaneSlowDoesNotGrantAnUnconfirmedPullOnRelease()
    {
        using var fixture = new EnemyForceFixture(NetworkEnemyForceKind.Hurricane);
        fixture.player.movement.enabled = true; fixture.player.movement.SimulateNetworkMovementTick(1);
        var state = fixture.PrepareOwner(); Assert.IsFalse(state.enemyForce.allowPull); Assert.AreEqual(0.5f, state.speedMultiplier);
        var noForce = state; noForce.enemyForce = default;
        fixture.player.movement.RestoreNetworkMovementState(noForce); fixture.player.movement.SimulateNetworkMovementTick(0);
        Vector2 expected = fixture.player.body.position;
        fixture.player.movement.RestoreNetworkMovementState(state); fixture.player.movement.SimulateNetworkMovementTick(0);
        Assert.AreEqual(expected, fixture.player.body.position);
        Assert.AreEqual(NetworkEnemyForceKind.None, fixture.player.movement.CaptureNetworkMovementState().enemyForce.kind);
    }

    [TestCase(NetworkEnemyForceKind.Hurricane)]
    [TestCase(NetworkEnemyForceKind.Magnet)]
    [TestCase(NetworkEnemyForceKind.Bubble)]
    public void EnemyForcePauseStopAndSceneChangesReleaseTheCorrectContext(NetworkEnemyForceKind kind)
    {
        using var fixture = new EnemyForceFixture(kind); var state = fixture.PrepareOwner();
        PusherFixture.Paused.SetValue(null, true);
        for (int i = 0; i < 4; i++) fixture.player.movement.SimulateNetworkMovementTick(0);
        Assert.AreEqual(state.position, fixture.player.body.position);
        Assert.AreEqual(state.enemyForce.remaining, fixture.player.movement.CaptureNetworkMovementState().enemyForce.remaining);
        PusherFixture.Paused.SetValue(null, false);
        var wrongScene = state; wrongScene.enemyForce.scene = "OtherStage";
        fixture.player.movement.RestoreNetworkMovementState(wrongScene); fixture.player.movement.SimulateNetworkMovementTick(0);
        Assert.AreEqual(state.position, fixture.player.body.position);
        var stopped = state; stopped.enemyForce = default;
        fixture.player.movement.RestoreNetworkMovementState(stopped); fixture.player.movement.SimulateNetworkMovementTick(0);
        Assert.AreEqual(state.position, fixture.player.body.position);
        fixture.player.movement.RestoreNetworkMovementState(state); fixture.player.movement.SetNetworkTickDriven(false);
        Assert.AreEqual(NetworkEnemyForceKind.None, fixture.player.movement.CaptureNetworkMovementState().enemyForce.kind);
    }

    [TestCase(NetworkEnemyForceKind.Hurricane, false)]
    [TestCase(NetworkEnemyForceKind.Hurricane, true)]
    [TestCase(NetworkEnemyForceKind.Magnet, false)]
    [TestCase(NetworkEnemyForceKind.Magnet, true)]
    [TestCase(NetworkEnemyForceKind.Bubble, false)]
    [TestCase(NetworkEnemyForceKind.Bubble, true)]
    public void EnemyForcePreviewStopsBeforeKnownWallsAndNeverStartsDamage(NetworkEnemyForceKind kind, bool hole)
    {
        using var fixture = new EnemyForceFixture(kind); var state = fixture.PrepareOwner();
        var wall = new GameObject("EnemyForceWall"); wall.layer = LayerMask.NameToLayer("Stage");
        try
        {
            wall.transform.position = state.position + Vector2.up * (kind == NetworkEnemyForceKind.Bubble ? 0.25f : -0.25f);
            var shape = wall.AddComponent<BoxCollider2D>(); shape.size = Vector2.one * 0.1f; shape.isTrigger = hole;
            if (hole) wall.tag = "Hole";
            fixture.player.movement.obstacleMask = LayerMask.GetMask("Stage"); fixture.player.movement.RefreshObstacleContactFilter(); Physics2D.SyncTransforms();
            fixture.player.movement.SimulateNetworkMovementTick(0);
            Assert.AreEqual(state.position, fixture.player.body.position); Assert.IsFalse(fixture.player.movement.isDead);
        }
        finally { Object.DestroyImmediate(wall); }
    }

    [Test]
    public void BubbleNativePauseDoesNotSpendItsRemainingPushDistance()
    {
        using var fixture = new EnemyForceFixture(NetworkEnemyForceKind.Bubble); fixture.NativeStep(); fixture.player.AdvancePhysics();
        float remaining = fixture.player.movement.CaptureNetworkMovementState().enemyForce.remainingDistance;
        PusherFixture.Paused.SetValue(null, true); fixture.NativeStep();
        Assert.AreEqual(remaining, fixture.player.movement.CaptureNetworkMovementState().enemyForce.remainingDistance);
        PusherFixture.Paused.SetValue(null, false); fixture.NativeStep(); fixture.player.AdvancePhysics();
        Assert.Less(fixture.player.movement.CaptureNetworkMovementState().enemyForce.remainingDistance, remaining);
    }

    [Test]
    public void UnrelatedPusherAndEnemyWorldForcesNeverSumTheirForecasts()
    {
        using var fixture = new EnemyForceFixture(NetworkEnemyForceKind.Magnet); fixture.NativeStep(); fixture.player.AdvancePhysics();
        Assert.IsTrue(fixture.player.movement.CaptureNetworkMovementState().enemyForce.IsValid);
        fixture.player.source.enabled = true; fixture.player.movement.RegisterNetworkPusherContact(fixture.player.source);
        var state = fixture.player.movement.CaptureNetworkMovementState();
        Assert.AreEqual(0, state.pusher.sourceId); Assert.AreEqual(0, state.enemyForce.sourceId);
    }

    [TestCase(NetworkEnemyForceKind.Hurricane)]
    [TestCase(NetworkEnemyForceKind.Magnet)]
    [TestCase(NetworkEnemyForceKind.Bubble)]
    public void PureClientsCannotStartNativeEnemyForcesOrConsumeTheirRng(NetworkEnemyForceKind kind)
    {
        using var fixture = new EnemyForceFixture(kind); NetSync.Mode = NetSync.NetMode.Client;
        var random = Random.state; Vector2 before = fixture.player.body.position;
        fixture.NativeStep(); fixture.player.AdvancePhysics();
        Assert.AreEqual(before, fixture.player.body.position);
        Assert.AreEqual(random, Random.state);
    }

    [Test]
    public void PusherSceneAndExpiredPhaseApprovalsCannotResumeInAnotherTimeline()
    {
        using var fixture = new PusherFixture(); var state = PreparePusherOwner(fixture);
        var wrongScene = state; wrongScene.pusher.scene = "OtherStage";
        fixture.movement.RestoreNetworkMovementState(wrongScene); fixture.movement.SimulateNetworkMovementTick(0);
        Assert.AreEqual(state.position, fixture.body.position);
        var connectionState = typeof(NetworkClient).GetField("connectState", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        object previous = connectionState.GetValue(null);
        try
        {
            connectionState.SetValue(null, System.Enum.Parse(connectionState.FieldType, "Connected"));
            state.serverTime = NetworkBombFx.KickServerNow - 0.05; state.pusher.remaining = 0.02f;
            fixture.movement.RestoreNetworkMovementState(state); fixture.movement.SimulateNetworkMovementTick(0);
            Assert.AreEqual(state.position, fixture.body.position);
        }
        finally { connectionState.SetValue(null, previous); }
    }

    [TestCase(NetworkEnemyForceKind.Hurricane)]
    [TestCase(NetworkEnemyForceKind.Magnet)]
    [TestCase(NetworkEnemyForceKind.Bubble)]
    public void EnemyForceContinuationNeverExceedsItsTimeAndSafeCellBudget(NetworkEnemyForceKind kind)
    {
        using var fixture = new EnemyForceFixture(kind); var state = fixture.PrepareOwner();
        for (int i = 0; i < 60; i++) fixture.player.movement.SimulateNetworkMovementTick(0);
        Assert.LessOrEqual(Vector2.Distance(state.position, fixture.player.body.position), state.enemyForce.speed * state.enemyForce.remaining + 0.001f);
        Assert.IsTrue(state.enemyForce.Contains(fixture.player.body.position));
        Assert.AreEqual(NetworkEnemyForceKind.None, fixture.player.movement.CaptureNetworkMovementState().enemyForce.kind);
    }

    [TestCase(NetworkEnemyForceKind.Hurricane)]
    [TestCase(NetworkEnemyForceKind.Magnet)]
    [TestCase(NetworkEnemyForceKind.Bubble)]
    public void DelayedEnemyForcePacketsCannotResurrectTheOldPhase(NetworkEnemyForceKind kind)
    {
        using var fixture = new EnemyForceFixture(kind); var state = fixture.PrepareOwner();
        var connectionState = typeof(NetworkClient).GetField("connectState", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        object previous = connectionState.GetValue(null);
        try
        {
            connectionState.SetValue(null, System.Enum.Parse(connectionState.FieldType, "Connected"));
            state.serverTime = NetworkBombFx.KickServerNow - 1;
            fixture.player.movement.RestoreNetworkMovementState(state); fixture.player.movement.SimulateNetworkMovementTick(0);
            Assert.AreEqual(state.position, fixture.player.body.position);
            state.serverTime = NetworkBombFx.KickServerNow;
            fixture.player.movement.RestoreNetworkMovementState(state); fixture.player.movement.SimulateNetworkMovementTick(0);
            Assert.Greater(Vector2.Distance(state.position, fixture.player.body.position), 0.005f);
        }
        finally { connectionState.SetValue(null, previous); }
    }

    [TestCase(NetworkEnemyForceKind.Hurricane)]
    [TestCase(NetworkEnemyForceKind.Magnet)]
    [TestCase(NetworkEnemyForceKind.Bubble)]
    public void EnemyForceStopIsSentWithAnUnchangedActorAndInputAck(NetworkEnemyForceKind kind)
    {
        using var fixture = new EnemyForceFixture(kind); fixture.NativeStep(); fixture.player.AdvancePhysics();
        Assert.IsTrue(fixture.player.movement.CaptureNetworkMovementState().enemyForce.IsValid);
        bool wasServer = NetworkServer.active; var oldTransport = Transport.active;
        var transportObject = new GameObject("EnemyForceClearTransport"); transportObject.SetActive(false);
        int messages = 0;
        System.Action<NetworkDiagnostics.MessageInfo> count = info => { if (info.message is RpcMessage) messages++; };
        try
        {
            Transport.active = transportObject.AddComponent<TelepathyTransport>(); typeof(NetworkServer).GetProperty("active").SetValue(null, true);
            typeof(NetworkIdentity).GetProperty("connectionToClient").SetValue(fixture.player.identity, new NetworkConnectionToClient(9847));
            var send = typeof(NetworkPlayerInput).GetMethod("SendMovementSnapshot", PusherFlags); NetworkDiagnostics.OutMessageEvent += count;
            send.Invoke(fixture.player.input, new object[] { false }); send.Invoke(fixture.player.input, new object[] { false }); Assert.AreEqual(2, messages);
            fixture.enemy.enabled = false; send.Invoke(fixture.player.input, new object[] { false }); Assert.AreEqual(3, messages);
            send.Invoke(fixture.player.input, new object[] { false }); Assert.AreEqual(3, messages);
        }
        finally
        {
            NetworkDiagnostics.OutMessageEvent -= count; typeof(NetworkIdentity).GetProperty("connectionToClient").SetValue(fixture.player.identity, null);
            typeof(NetworkServer).GetProperty("active").SetValue(null, wasServer); Transport.active = oldTransport; Object.DestroyImmediate(transportObject);
        }
    }

    [TestCase(NetworkEnemyForceKind.Hurricane)]
    [TestCase(NetworkEnemyForceKind.Magnet)]
    public void EnemyVisionListsKeepAllBodiesWithMoreThanSixteenShapes(NetworkEnemyForceKind kind)
    {
        using var fixture = new EnemyForceFixture(kind); using var second = new PusherFixture(circle: true);
        second.body.position = fixture.player.body.position + Vector2.up * 0.1f; second.actor.transform.position = second.body.position;
        for (int i = 0; i < 20; i++) { var shape = fixture.player.actor.AddComponent<CircleCollider2D>(); shape.radius = 0.2f; shape.offset = new Vector2(0, -0.1f); }
        Physics2D.SyncTransforms();
        if (kind == NetworkEnemyForceKind.Magnet)
        {
            ((MagnetMovementController)fixture.enemy).playerLayerMask = 1 << fixture.player.actor.layer;
            object[] args = { null, null };
            Assert.IsTrue((bool)typeof(MagnetMovementController).GetMethod("TryMagnetAnyDirection", PusherFlags).Invoke(fixture.enemy, args));
            Assert.AreEqual(Vector2.up, args[0]); Assert.AreEqual(2, ((List<Rigidbody2D>)args[1]).Count);
            Assert.IsTrue((bool)typeof(MagnetMovementController).GetMethod("TryMagnetAnyDirection", PusherFlags).Invoke(fixture.enemy, args));
            Assert.AreEqual(2, ((List<Rigidbody2D>)args[1]).Count, "Reusing the buffer must not keep bodies from the previous scan.");
        }
        else
        {
            Vector2 firstBefore = fixture.player.body.position, secondBefore = second.body.position;
            fixture.NativeStep(); fixture.player.AdvancePhysics();
            Assert.Less(fixture.player.body.position.y, firstBefore.y); Assert.Less(second.body.position.y, secondBefore.y);
            Assert.AreEqual(22, ((List<Collider2D>)typeof(HurricaneMovementController).GetField("playerHits", PusherFlags).GetValue(fixture.enemy)).Count);
        }
        fixture.player.body.position += Vector2.right * 20; fixture.player.actor.transform.position = fixture.player.body.position;
        second.body.position += Vector2.right * 20; second.actor.transform.position = second.body.position; Physics2D.SyncTransforms();
        if (kind == NetworkEnemyForceKind.Magnet)
        {
            object[] args = { null, null };
            Assert.IsFalse((bool)typeof(MagnetMovementController).GetMethod("TryMagnetAnyDirection", PusherFlags).Invoke(fixture.enemy, args));
        }
        else
        {
            fixture.NativeStep();
            Assert.AreEqual(0, ((List<Collider2D>)typeof(HurricaneMovementController).GetField("playerHits", PusherFlags).GetValue(fixture.enemy)).Count);
        }
    }

    [Test]
    public void HurricanePhaseCanBridgeKnownAdjacentActiveFramesButNotTheNextCycle()
    {
        using var fixture = new EnemyForceFixture(NetworkEnemyForceKind.Hurricane); fixture.NativeStep(); fixture.player.AdvancePhysics();
        var animation = (AnimatedSpriteRenderer)typeof(EnemyMovementController).GetField("activeSprite", PusherFlags).GetValue(fixture.enemy);
        var timer = typeof(AnimatedSpriteRenderer).GetField("frameTimer", PusherFlags);
        animation.CurrentFrame = 12; timer.SetValue(animation, 0.24f);
        Assert.AreEqual(NetworkEnemyForceState.MaximumSeconds, fixture.player.movement.CaptureNetworkMovementState().enemyForce.remaining);
        animation.CurrentFrame = 13; timer.SetValue(animation, 0.24f);
        Assert.That(fixture.player.movement.CaptureNetworkMovementState().enemyForce.remaining, Is.EqualTo(0.01f).Within(0.0001f));
        animation.CurrentFrame = 0;
        Assert.AreEqual(NetworkEnemyForceKind.None, fixture.player.movement.CaptureNetworkMovementState().enemyForce.kind);
    }

    sealed class EnemyForceFixture : System.IDisposable
    {
        public readonly PusherFixture player = new(circle: true);
        public readonly MonoBehaviour enemy;
        public readonly Rigidbody2D enemyBody;
        readonly NetworkEnemyForceKind kind;
        IEnumerator bubble;

        public EnemyForceFixture(NetworkEnemyForceKind kind)
        {
            this.kind = kind;
            var go = new GameObject("ConfirmedEnemyForce " + kind); go.SetActive(false); go.layer = LayerMask.NameToLayer("Enemy");
            go.AddComponent<BoxCollider2D>();
            enemy = kind == NetworkEnemyForceKind.Hurricane ? go.AddComponent<HurricaneMovementController>() :
                kind == NetworkEnemyForceKind.Magnet ? go.AddComponent<MagnetMovementController>() : go.AddComponent<SubmarineBubbleMovementController>();
            foreach (var behaviour in go.GetComponents<MonoBehaviour>()) behaviour.enabled = false;
            enemyBody = go.GetComponent<Rigidbody2D>(); enemyBody.bodyType = RigidbodyType2D.Kinematic; enemyBody.gravityScale = 0;
            typeof(EnemyMovementController).GetField("rb", PusherFlags).SetValue(enemy, enemyBody);
            ((EnemyMovementController)enemy).obstacleMask = 0;
            if (kind == NetworkEnemyForceKind.Hurricane)
            {
                var visual = new GameObject("ConfirmedPullPhase", typeof(SpriteRenderer)); visual.transform.SetParent(go.transform, false);
                var animation = visual.AddComponent<AnimatedSpriteRenderer>(); animation.enabled = false; animation.animationSprite = new Sprite[14];
                animation.frameDurations = new float[14]; for (int i = 0; i < 14; i++) animation.frameDurations[i] = 0.25f;
                animation.CurrentFrame = 12;
                typeof(EnemyMovementController).GetField("activeSprite", PusherFlags).SetValue(enemy, animation);
                typeof(HurricaneMovementController).GetField("playerLayerMask", PusherFlags).SetValue(enemy, (LayerMask)(1 << player.actor.layer));
            }
            if (kind == NetworkEnemyForceKind.Magnet)
            {
                ((MagnetMovementController)enemy).magnetBlockMask = 0;
                typeof(MagnetMovementController).GetField("_isMagnetActive", PusherFlags).SetValue(enemy, true);
            }
            go.SetActive(true); enemy.enabled = true;
            enemyBody.position = player.body.position - Vector2.up * 2; go.transform.position = enemyBody.position; Physics2D.SyncTransforms();
        }

        public void NativeStep()
        {
            if (kind == NetworkEnemyForceKind.Hurricane) typeof(HurricaneMovementController).GetMethod("PullAlignedPlayers", PusherFlags).Invoke(enemy, null);
            else if (kind == NetworkEnemyForceKind.Magnet) typeof(MagnetMovementController).GetMethod("PullPlayers", PusherFlags).Invoke(enemy, new object[] { new List<Rigidbody2D> { player.body }, Vector2.up });
            else
            {
                if (bubble == null)
                {
                    ((HashSet<MovementController>)typeof(SubmarineBubbleMovementController).GetField("pushedPlayers", PusherFlags).GetValue(enemy)).Add(player.movement);
                    bubble = (IEnumerator)typeof(SubmarineBubbleMovementController).GetMethod("PushPlayerRoutine", PusherFlags).Invoke(enemy, new object[] { player.movement, Vector2.up });
                }
                bubble.MoveNext();
            }
        }

        public MovementController.NetworkMovementState PrepareOwner()
        {
            NativeStep(); player.AdvancePhysics();
            var state = player.movement.CaptureNetworkMovementState();
            Assert.IsTrue(state.enemyForce.IsValid, "Native " + kind + " approval: sourceScene='" + enemy.gameObject.scene.name +
                "' active='" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name + "' block=" + player.movement.NetworkPredictionBlockReason +
                ";receipt=" + JsonUtility.ToJson(typeof(MovementController).GetField("approvedEnemyForceReceipt", PusherFlags).GetValue(player.movement)) +
                ";body=" + player.body.position + ";target=" + typeof(MovementController).GetField("approvedEnemyForceTarget", PusherFlags).GetValue(player.movement));
            state.baseWorldSpeed = 4; state.worldSpeed = 4 * state.speedMultiplier;
            using (var writer = NetworkWriterPool.Get()) { writer.Write(state); state = new NetworkReader(writer.ToArray()).Read<MovementController.NetworkMovementState>(); }
            enemy.enabled = false; NetSync.Mode = NetSync.NetMode.Client;
            typeof(NetworkIdentity).GetProperty("isServer").SetValue(player.identity, false);
            player.movement.enabled = true; player.movement.SetPredictLocally(true); player.movement.RestoreNetworkMovementState(state);
            return state;
        }
        public void Dispose()
        {
            (bubble as System.IDisposable)?.Dispose(); Object.DestroyImmediate(enemy.gameObject); player.Dispose();
        }
    }
}
#endif

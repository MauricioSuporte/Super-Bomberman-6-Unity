#if UNITY_EDITOR
using System;
using System.Reflection;
using Assets.Scripts.Netcode;
using Mirror;
using NUnit.Framework;
using UnityEngine;

public sealed partial class OnlineRoomEditModeTests
{
    const BindingFlags PusherFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(4)]
    public void PusherWorldStepComposesOnceAfterTheServerInputBatch(int inputSteps)
    {
        using var fixture = new PusherFixture();
        fixture.movement.enabled = true;
        Vector2 initial = fixture.body.position;
        for (int i = 0; i < inputSteps; i++) fixture.movement.SimulateNetworkMovementTick(8);
        Vector2 walked = fixture.body.position;
        if (inputSteps > 0) Assert.Greater(walked.x, initial.x, "The fixture must actually walk before applying the world force.");
        fixture.AlignSourceToActor();
        fixture.Push(Vector2.up * 0.2f);
        Assert.AreEqual(walked, fixture.body.position, "The cast must keep its pending target until physics, preserving other pushers' queries.");
        fixture.AdvancePhysics();
        Assert.That(Vector2.Distance(walked + Vector2.up * 0.2f, fixture.body.position), Is.LessThan(0.01f));
        Assert.AreEqual((Vector2)fixture.actor.transform.position, fixture.body.position);
        Assert.AreEqual(fixture.body.position, fixture.movement.CaptureNetworkMovementState().position);
    }

    [Test]
    public void SimultaneousPushersKeepTheNativeLastTargetInsteadOfSummingForces()
    {
        using var fixture = new PusherFixture();
        var other = PusherFixture.MakeSource("SecondPusher");
        try
        {
            Vector2 start = fixture.body.position;
            fixture.Push(Vector2.up * 0.2f);
            var otherBody = other.GetComponent<Rigidbody2D>(); otherBody.position = start - Vector2.right * 1.05f;
            other.transform.position = otherBody.position;
            typeof(PusherPatrolAndPush).GetField("playerLayerMask", PusherFlags).SetValue(other, (LayerMask)(1 << fixture.actor.layer));
            typeof(PusherPatrolAndPush).GetField("pushHorizontallyInsteadOfVertically", PusherFlags).SetValue(other, true);
            Physics2D.SyncTransforms();
            typeof(PusherPatrolAndPush).GetMethod("PushPlayersIfValidContact", PusherFlags).Invoke(other, new object[] { Vector2.right * 0.2f });
            fixture.AdvancePhysics();
            Assert.That(Vector2.Distance(start + Vector2.right * 0.2f, fixture.body.position), Is.LessThan(0.01f));
            fixture.AlignSourceToActor(); fixture.Push(Vector2.up * 0.2f); fixture.AdvancePhysics();
            Assert.That(Vector2.Distance(start + new Vector2(0.2f, 0.2f), fixture.body.position), Is.LessThan(0.01f));
        }
        finally { UnityEngine.Object.DestroyImmediate(other.gameObject); }
    }

    [TestCase(NetSync.NetMode.Offline, false)]
    [TestCase(NetSync.NetMode.Host, false)]
    [TestCase(NetSync.NetMode.Host, true)]
    public void NativePusherRetainsOfflineHostLocalComAndUnspawnedSimulation(NetSync.NetMode mode, bool server)
    {
        using var fixture = new PusherFixture(); NetSync.Mode = mode;
        typeof(NetworkIdentity).GetProperty("isServer").SetValue(fixture.identity, server);
        typeof(NetworkIdentity).GetProperty("netId").SetValue(fixture.identity, 0u);
        fixture.movement.SetNetworkTickDriven(false);
        Vector2 before = fixture.body.position;
        fixture.Push(Vector2.up * 0.2f); fixture.AdvancePhysics();
        Assert.That(Vector2.Distance(before + Vector2.up * 0.2f, fixture.body.position), Is.LessThan(0.01f));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void NativePusherCannotAdvanceOrPushOnAPureClientOrDuringPause(bool paused)
    {
        using var fixture = new PusherFixture();
        PusherFixture.Paused.SetValue(null, paused);
        if (!paused) NetSync.Mode = NetSync.NetMode.Client;
        Vector2 sourceStart = fixture.source.GetComponent<Rigidbody2D>().position;
        Vector2 playerStart = fixture.body.position;
        typeof(PusherPatrolAndPush).GetMethod("FixedUpdate", PusherFlags).Invoke(fixture.source, null);
        fixture.Push(Vector2.up * 0.2f); fixture.AdvancePhysics();
        Assert.AreEqual(sourceStart, fixture.source.GetComponent<Rigidbody2D>().position);
        Assert.AreEqual(playerStart, fixture.body.position);
    }

    [Test]
    public void NativePusherAlsoMovesTheAuthoredKinematicCircleShape()
    {
        using var fixture = new PusherFixture(circle: true);
        Vector2 before = fixture.body.position;
        fixture.Push(Vector2.up * 0.2f); fixture.AdvancePhysics();
        Assert.That(Vector2.Distance(before + Vector2.up * 0.2f, fixture.body.position), Is.LessThan(0.001f));
    }

    [Test]
    public void PusherCastWithTwoPlayerShapesNotifiesTheActorOnlyOnce()
    {
        using var fixture = new PusherFixture();
        fixture.actor.AddComponent<BoxCollider2D>().size = new Vector2(0.8f, 0.8f);
        Physics2D.SyncTransforms(); Vector2 before = fixture.body.position;
        fixture.Push(Vector2.up * 0.2f);
        var bodies = (System.Collections.Generic.HashSet<Rigidbody2D>)typeof(PusherPatrolAndPush).GetField("pushedBodies", PusherFlags).GetValue(fixture.source);
        Assert.AreEqual(1, bodies.Count);
        fixture.AdvancePhysics();
        Assert.That(Vector2.Distance(before + Vector2.up * 0.2f, fixture.body.position), Is.LessThan(0.01f));
    }

    [Test]
    public void PusherAndResolverChangesAreSentEvenWhenTheInputAcknowledgementRepeats()
    {
        using var fixture = new PusherFixture();
        bool oldServer = NetworkServer.active;
        Transport oldTransport = Transport.active;
        var transportObject = new GameObject("PusherSnapshotTransport"); transportObject.SetActive(false);
        var connection = new NetworkConnectionToClient(9845);
        int messages = 0;
        Action<NetworkDiagnostics.MessageInfo> count = info => { if (info.message is RpcMessage) messages++; };
        try
        {
            Transport.active = transportObject.AddComponent<TelepathyTransport>();
            typeof(NetworkServer).GetProperty("active").SetValue(null, true);
            typeof(NetworkIdentity).GetProperty("connectionToClient").SetValue(fixture.identity, connection);
            typeof(NetworkPlayerInput).GetField("processedTick", PusherFlags).SetValue(fixture.input, 17u);
            NetworkDiagnostics.OutMessageEvent += count;
            var send = typeof(NetworkPlayerInput).GetMethod("SendMovementSnapshot", PusherFlags);
            send.Invoke(fixture.input, new object[] { false }); Assert.AreEqual(1, messages);
            send.Invoke(fixture.input, new object[] { false }); Assert.AreEqual(1, messages, "Unchanged duplicate acknowledgements should still be skipped.");
            fixture.Push(Vector2.up * 0.2f); fixture.AdvancePhysics();
            send.Invoke(fixture.input, new object[] { false }); Assert.AreEqual(2, messages, "A world force must be sent without requiring new client input.");
            fixture.body.position = new Vector2(710, 710); fixture.actor.transform.position = fixture.body.position;
            send.Invoke(fixture.input, new object[] { false }); Assert.AreEqual(3, messages, "The resolver's later grid correction must also be sent.");
            Assert.AreEqual(17u, typeof(NetworkPlayerInput).GetField("lastSnapshotTick", PusherFlags).GetValue(fixture.input));
        }
        finally
        {
            NetworkDiagnostics.OutMessageEvent -= count;
            typeof(NetworkIdentity).GetProperty("connectionToClient").SetValue(fixture.identity, null);
            typeof(NetworkServer).GetProperty("active").SetValue(null, oldServer);
            Transport.active = oldTransport; UnityEngine.Object.DestroyImmediate(transportObject);
        }
    }

    sealed class PusherFixture : IDisposable
    {
        public static readonly FieldInfo Paused = typeof(GamePauseController).GetField("<IsPaused>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        readonly NetSync.NetMode oldMode = NetSync.Mode;
        readonly bool oldPaused = (bool)Paused.GetValue(null);
        public readonly GameObject actor;
        public readonly MovementController movement;
        public readonly Rigidbody2D body;
        public readonly NetworkIdentity identity;
        public readonly NetworkPlayerInput input;
        public readonly PusherPatrolAndPush source;

        public PusherFixture(bool circle = false)
        {
            NetSync.Mode = NetSync.NetMode.Host; Paused.SetValue(null, false);
            actor = new GameObject("PusherNetworkActor"); actor.SetActive(false); actor.tag = "Player"; actor.layer = 30;
            if (circle) { var shape = actor.AddComponent<CircleCollider2D>(); shape.radius = 0.2f; shape.offset = new Vector2(0, -0.1f); }
            else actor.AddComponent<BoxCollider2D>();
            movement = actor.AddComponent<MovementController>();
            identity = actor.AddComponent<NetworkIdentity>(); input = actor.AddComponent<NetworkPlayerInput>();
            typeof(NetworkIdentity).GetMethod("InitializeNetworkBehaviours", PusherFlags).Invoke(identity, null);
            typeof(NetworkIdentity).GetProperty("netId").SetValue(identity, 9845u);
            typeof(NetworkIdentity).GetProperty("isServer").SetValue(identity, true);
            body = actor.GetComponent<Rigidbody2D>(); body.bodyType = RigidbodyType2D.Kinematic; body.gravityScale = 0; body.freezeRotation = true;
            typeof(MovementController).GetProperty(nameof(MovementController.Rigidbody)).SetValue(movement, body);
            typeof(NetworkPlayerInput).GetMethod("Awake", PusherFlags).Invoke(input, null);
            foreach (var behaviour in actor.GetComponents<MonoBehaviour>()) behaviour.enabled = false;
            actor.SetActive(true); movement.SetNetworkTickDriven(true); movement.SetPlayerId(1);
            body.position = new Vector2(710, 710); actor.transform.position = body.position;
            movement.obstacleMask = 0; movement.RefreshObstacleContactFilter();
            Assert.AreEqual(new Vector2(710, 710), body.position);
            source = MakeSource("PusherNetworkSource"); AlignSourceToActor();
            typeof(PusherPatrolAndPush).GetField("playerLayerMask", PusherFlags).SetValue(source, (LayerMask)(1 << actor.layer));
            AdvancePhysics();
        }

        public void AlignSourceToActor()
        {
            var actorBounds = actor.GetComponent<Collider2D>().bounds;
            var sourceBody = source.GetComponent<Rigidbody2D>(); sourceBody.position = (Vector2)actorBounds.center - Vector2.up * (actorBounds.extents.y + 0.55f);
            source.transform.position = sourceBody.position;
            typeof(PusherPatrolAndPush).GetField("pointB", PusherFlags).SetValue(source, sourceBody.position + Vector2.up * 5);
            Physics2D.SyncTransforms();
        }

        public void Push(Vector2 delta)
        {
            if (NetSync.ShouldSimulateLocally && !GamePauseController.IsPaused)
            {
                var sourceCollider = source.GetComponent<Collider2D>(); var actorCollider = actor.GetComponent<Collider2D>();
                var hits = new RaycastHit2D[16];
                int count = sourceCollider.Cast(delta.normalized, new ContactFilter2D { useLayerMask = true, layerMask = 1 << actor.layer, useTriggers = true }, hits, delta.magnitude + 0.02f);
                string diagnostic = "source=" + sourceCollider.bounds + ";actor=" + actorCollider.bounds + ";simulated=" + body.simulated + ";layer=" + actor.layer + ";hits=" + count;
                Assert.Greater(count, 0, "The fixture must establish native contact: " + diagnostic);
                Assert.That(Vector2.Dot(hits[0].normal, delta.normalized), Is.LessThan(-0.7f), "Unexpected native contact normal " + hits[0].normal + ";" + diagnostic);
            }
            typeof(PusherPatrolAndPush).GetMethod("PushPlayersIfValidContact", PusherFlags).Invoke(source, new object[] { delta });
        }

        public void AdvancePhysics()
        {
            var mode = Physics2D.simulationMode;
            try { Physics2D.simulationMode = SimulationMode2D.Script; Assert.IsTrue(Physics2D.Simulate(Time.fixedDeltaTime)); }
            finally { Physics2D.simulationMode = mode; }
        }

        public static PusherPatrolAndPush MakeSource(string name)
        {
            var go = new GameObject(name); go.SetActive(false); go.AddComponent<BoxCollider2D>();
            var pusher = go.AddComponent<PusherPatrolAndPush>();
            foreach (var behaviour in go.GetComponents<MonoBehaviour>()) behaviour.enabled = false;
            typeof(PusherPatrolAndPush).GetMethod("Awake", PusherFlags).Invoke(pusher, null);
            go.SetActive(true); return pusher;
        }

        public void Dispose()
        {
            typeof(NetworkIdentity).GetProperty("isServer").SetValue(identity, false);
            UnityEngine.Object.DestroyImmediate(actor); UnityEngine.Object.DestroyImmediate(source.gameObject);
            NetSync.Mode = oldMode; Paused.SetValue(null, oldPaused);
        }
    }
}
#endif

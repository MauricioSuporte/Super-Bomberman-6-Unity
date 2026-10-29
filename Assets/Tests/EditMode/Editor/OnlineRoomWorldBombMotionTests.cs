using System;
using System.Reflection;
using Assets.Scripts.Netcode;
using Mirror;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed partial class OnlineRoomEditModeTests
{
    [TestCase(false)] [TestCase(true)]
    public void ApprovedMotionRecoversAReconnectedPeerEvenWhenItsConnectionIdIsReused(bool world)
    {
        using var fixture = new WorldBombFixture(false);
        const int id = int.MaxValue - 245;
        Assert.IsFalse(NetworkServer.connections.ContainsKey(id));
        bool previousServer = NetworkServer.active; var previousTransport = Transport.active; var previousMode = NetSync.Mode;
        var transportObject = new GameObject("ReconnectMotionTransport"); transportObject.SetActive(false);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        int messages = 0;
        Action<NetworkDiagnostics.MessageInfo> count = info =>
        { if (world ? info.message is WorldBombMotion : info.message is NetworkItemMotion) messages++; };
        try
        {
            NetSync.Mode = NetSync.NetMode.Host;
            Transport.active = transportObject.AddComponent<TelepathyTransport>();
            typeof(NetworkServer).GetProperty("active").SetValue(null, true);
            var first = new NetworkConnectionToClient(id) { isAuthenticated = true, isReady = true };
            NetworkServer.connections[id] = first;
            object target, state = null; MethodInfo send; System.Collections.IDictionary cache;
            if (world)
            {
                var stateType = typeof(BombermanNetworkManager).GetNestedType("WorldBombState", BindingFlags.NonPublic);
                state = Activator.CreateInstance(stateType, true);
                stateType.GetField("message").SetValue(state, new WorldBombMotion { motion = fixture.Motion(1, NetworkBombFx.BombMotionKind.Kick) });
                stateType.GetField("excludedConnection").SetValue(state, first);
                target = fixture.manager; send = typeof(BombermanNetworkManager).GetMethod("SendWorldBombState", flags);
                cache = (System.Collections.IDictionary)stateType.GetField("sent").GetValue(state);
            }
            else
            {
                var item = fixture.replica.AddComponent<ItemPickup>(); target = item;
                typeof(ItemPickup).GetField("serverItemMotion", flags).SetValue(item,
                    new NetworkItemMotion { netId = WorldBombFixture.BombId, revision = 1, rotation = Quaternion.identity });
                send = typeof(ItemPickup).GetMethod("SendNetworkItemToNewPeers", flags);
                cache = (System.Collections.IDictionary)typeof(ItemPickup).GetField("itemMotionPeers", flags).GetValue(item);
            }
            void Send() => send.Invoke(target, world ? new[] { state } : null);
            NetworkDiagnostics.OutMessageEvent += count;
            Send(); int initial = messages; Assert.AreEqual(world ? 0 : 1, initial);
            NetworkServer.connections[id] = new NetworkConnectionToClient(id) { isAuthenticated = true, isReady = true };
            Send(); Assert.AreEqual(initial + 1, messages, "A replacement connection must recover even the excluded owner's previous id.");
            Send(); Assert.AreEqual(initial + 1, messages, "The same connection should not receive duplicate heartbeats.");
            NetworkServer.connections.Remove(id); Send(); Assert.AreEqual(0, cache.Count, "Disconnected peers must not accumulate in an idle motion cache.");
        }
        finally
        {
            NetworkDiagnostics.OutMessageEvent -= count; NetworkServer.connections.Remove(id);
            typeof(NetworkServer).GetProperty("active").SetValue(null, previousServer);
            Transport.active = previousTransport; Object.DestroyImmediate(transportObject); NetSync.Mode = previousMode;
        }
    }

    [TestCase(typeof(GreenLouieDashAbility), "ReportNetworkDashStop")]
    [TestCase(typeof(PinkLouieJumpAbility), "ReportNetworkJumpStop")]
    [TestCase(typeof(MoleMountDrillAbility), "Cancel")]
    public void MountAbilityCleanupDoesNotPublishAfterServerStops(Type abilityType, string method)
    {
        Assert.IsFalse(NetworkServer.active);
        var previous = NetSync.Mode;
        var root = new GameObject("StoppedServerAbility"); root.SetActive(false);
        try
        {
            NetSync.Mode = NetSync.NetMode.Host;
            var identity = root.AddComponent<NetworkIdentity>();
            root.AddComponent<BoxCollider2D>(); root.AddComponent<Rigidbody2D>();
            root.AddComponent<NetworkBombFx>(); var ability = root.AddComponent(abilityType);
            typeof(NetworkIdentity).GetMethod("InitializeNetworkBehaviours", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(identity, null);
            typeof(NetworkIdentity).GetProperty("netId").SetValue(identity, uint.MaxValue - 245);
            typeof(NetworkIdentity).GetProperty("isServer").SetValue(identity, true);
            var arguments = method == "ReportNetworkJumpStop" ? new object[] { false } : null;
            abilityType.GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ability, arguments);
            UnityEngine.TestTools.LogAssert.NoUnexpectedReceived();
        }
        finally { Object.DestroyImmediate(root); NetSync.Mode = previous; }
    }

    [Test]
    public void BombMotionCleanupIgnoresAnAlreadyDestroyedManager()
    {
        var singleton = typeof(NetworkManager).GetProperty("singleton");
        var previous = singleton.GetValue(null);
        var root = new GameObject("DestroyedBombMotionManager"); root.SetActive(false);
        var manager = root.AddComponent<BombermanNetworkManager>();
        try
        {
            Object.DestroyImmediate(root);
            // Unity's destroyed wrapper still passes a C# type pattern.
            singleton.SetValue(null, manager);
            Assert.DoesNotThrow(() => NetworkBombMotionLifetime.Forget(null));
        }
        finally
        {
            singleton.SetValue(null, previous);
            if (root != null) Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void DiagnosticClientNamesAreExplicitDevelopmentOnlyAndDoNotPersist()
    {
        string original = OnlineVersionAuthenticator.PlayerName;
        var arguments = new[] { "game", "-local-diagnostic-name", "SB6Smoke1" };
        Assert.AreEqual("SB6Smoke1", OnlineVersionAuthenticator.ResolveDiagnosticPlayerName("Saved", arguments, true));
        Assert.AreEqual("Saved", OnlineVersionAuthenticator.ResolveDiagnosticPlayerName("Saved", arguments, false));
        Assert.AreEqual("Saved", OnlineVersionAuthenticator.ResolveDiagnosticPlayerName("Saved", new[] { "-local-diagnostic-name" }, true));
        Assert.AreEqual("Saved", OnlineVersionAuthenticator.ResolveDiagnosticPlayerName("Saved", new[] { "-local-diagnostic-name", "" }, true));
        Assert.AreEqual(original, OnlineVersionAuthenticator.PlayerName);
    }

    [Test]
    public void PresentationCostClassificationUsesGameplayFamilyAndClearsItsCache()
    {
        using var fixture = new WorldBombFixture(false);
        var enemy = new GameObject("MetricsEnemy"); enemy.SetActive(false); enemy.AddComponent<BoxCollider2D>();
        var child = new GameObject("MetricsSprite", typeof(SpriteRenderer)); child.transform.SetParent(enemy.transform, false);
        try
        {
            enemy.AddComponent<EnemyMovementController>();
            var coordinator = fixture.manager;
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var classify = typeof(BombermanNetworkManager).GetMethod("CoopPresentationFamily", flags);
            Assert.AreEqual(nameof(EnemyMovementController), classify.Invoke(coordinator, new object[] { child.GetComponent<SpriteRenderer>() }));
            var cache = (System.Collections.IDictionary)typeof(BombermanNetworkManager).GetField("coopMetricFamilies", flags).GetValue(coordinator);
            Assert.AreEqual(1, cache.Count);
            classify.Invoke(coordinator, new object[] { child.GetComponent<SpriteRenderer>() }); Assert.AreEqual(1, cache.Count);
            typeof(BombermanNetworkManager).GetMethod("ClearCoopPresentationMetrics", flags).Invoke(coordinator, null);
            Assert.AreEqual(0, cache.Count);
        }
        finally { Object.DestroyImmediate(enemy); }
    }
    [TestCase(false)] [TestCase(true)]
    public void WorldBombMotionDoesNotGrantAnAbilityAndKeepsTheArcBeforeItsStop(bool local)
    {
        using var fixture = new WorldBombFixture(local);
        var motion = fixture.Motion(1, NetworkBombFx.BombMotionKind.Punch);
        Assert.IsFalse(fixture.fx.BeginAbilityPrediction(fixture.bomb, motion, Vector2.right, 1));
        fixture.Receive(motion); fixture.Present(.1f);
        Assert.Greater(fixture.replica.transform.position.y, 710.1f);
        Assert.IsFalse(fixture.fx.AbilityPredictionLocksMovement);
        var stop = motion; stop.revision = 2; stop.moving = false; stop.first = false;
        stop.start = stop.end; stop.duration = 0; stop.arcHeight = 0;
        fixture.Receive(stop);
        Assert.Greater(fixture.replica.transform.position.y, 710.1f);
        fixture.Present(.3f); Assert.AreEqual(new Vector3(713, 710), fixture.replica.transform.position);
        fixture.Receive(motion); fixture.Present(0);
        Assert.AreEqual(new Vector3(713, 710), fixture.replica.transform.position);
        Assert.IsFalse(fixture.fx.AbilityPredictionLocksMovement);
    }

    [Test]
    public void WorldBombMotionCanTransferToAnotherSpawnedPresenterAfterItsOriginalActorIsRemoved()
    {
        using var fixture = new WorldBombFixture(false);
        fixture.Receive(fixture.Motion(1, NetworkBombFx.BombMotionKind.Kick));
        Assert.AreSame(fixture.fx, fixture.bomb.ClientBombMotionPresenter);
        NetworkClient.spawned.Remove(WorldBombFixture.ActorId);
        Object.DestroyImmediate(fixture.actor);
        var replacement = fixture.MakeActor(WorldBombFixture.ActorId + 1, false);
        try
        {
            fixture.Receive(fixture.Motion(2, NetworkBombFx.BombMotionKind.Kick));
            Assert.AreSame(replacement.GetComponent<NetworkBombFx>(), fixture.bomb.ClientBombMotionPresenter);
            Assert.IsFalse(replacement.GetComponent<NetworkBombFx>().AbilityPredictionLocksMovement);
        }
        finally { NetworkClient.spawned.Remove(WorldBombFixture.ActorId + 1); Object.DestroyImmediate(replacement); }
    }

    [Test]
    public void WorldBombMotionValidatesFiniteStateAndRequiresTheWorldFlag()
    {
        using var fixture = new WorldBombFixture(false);
        var motion = fixture.Motion(1, NetworkBombFx.BombMotionKind.Punch);
        Assert.IsTrue(BombermanNetworkManager.ValidWorldBombMotion(motion));
        motion.worldMotion = false; Assert.IsFalse(BombermanNetworkManager.ValidWorldBombMotion(motion));
        motion.worldMotion = true; motion.duration = float.NaN; Assert.IsFalse(BombermanNetworkManager.ValidWorldBombMotion(motion));
        motion.duration = .3f; motion.serverTime = double.NaN; Assert.IsFalse(BombermanNetworkManager.ValidWorldBombMotion(motion));
        motion.serverTime = 0; motion.bombId = 0; Assert.IsFalse(BombermanNetworkManager.ValidWorldBombMotion(motion));
    }

    sealed class WorldBombFixture : IDisposable
    {
        public const uint ActorId = uint.MaxValue - 240, BombId = uint.MaxValue - 241;
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        readonly NetSync.NetMode mode = NetSync.Mode;
        readonly FieldInfo clientState = typeof(NetworkClient).GetField("connectState", BindingFlags.Static | BindingFlags.NonPublic);
        readonly object previousClient;
        readonly FieldInfo paused = typeof(GamePauseController).GetField("<IsPaused>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        readonly object previousPause;
        readonly bool previousLoading = NetworkClient.isLoadingScene;
        readonly GameObject managerObject;
        public readonly BombermanNetworkManager manager;
        public readonly GameObject actor, replica;
        public readonly NetworkBombFx fx;
        public readonly Bomb bomb;
        public WorldBombFixture(bool local)
        {
            previousClient = clientState.GetValue(null); previousPause = paused.GetValue(null);
            NetSync.Mode = NetSync.NetMode.Client; clientState.SetValue(null, Enum.Parse(clientState.FieldType, "Connected"));
            paused.SetValue(null, false); NetworkClient.isLoadingScene = false;
            managerObject = new GameObject("WorldBombCoordinator"); managerObject.SetActive(false);
            manager = managerObject.AddComponent<BombermanNetworkManager>();
            actor = MakeActor(ActorId, local); fx = actor.GetComponent<NetworkBombFx>();
            replica = new GameObject("WorldBombReplica"); replica.SetActive(false); replica.AddComponent<CircleCollider2D>();
            bomb = replica.AddComponent<Bomb>(); var identity = replica.AddComponent<NetworkIdentity>();
            typeof(NetworkIdentity).GetProperty("netId").SetValue(identity, BombId); NetworkClient.spawned.Add(BombId, identity);
            replica.transform.position = new Vector3(710, 710); replica.SetActive(true);
        }
        public GameObject MakeActor(uint id, bool local)
        {
            var result = new GameObject("WorldBombPresenter"); result.SetActive(false); result.AddComponent<BoxCollider2D>();
            var identity = result.AddComponent<NetworkIdentity>(); result.AddComponent<NetworkBombFx>();
            typeof(NetworkIdentity).GetMethod("InitializeNetworkBehaviours", Flags).Invoke(identity, null);
            typeof(NetworkIdentity).GetProperty("netId").SetValue(identity, id);
            typeof(NetworkIdentity).GetProperty("isClient").SetValue(identity, true);
            typeof(NetworkIdentity).GetProperty("isLocalPlayer").SetValue(identity, local);
            NetworkClient.spawned.Add(id, identity); return result;
        }
        public NetworkBombFx.KickMotion Motion(uint revision, NetworkBombFx.BombMotionKind kind) => new()
        {
            bombId = BombId, worldMotion = true, revision = revision, kind = kind,
            start = new Vector2(710, 710), end = new Vector2(713, 710), duration = .3f,
            arcHeight = kind == NetworkBombFx.BombMotionKind.Kick ? 0 : 1, moving = true, first = true,
            serverTime = NetworkBombFx.KickServerNow
        };
        public void Receive(NetworkBombFx.KickMotion motion) => typeof(BombermanNetworkManager).GetMethod("ReceiveWorldBombMotion", Flags)
            .Invoke(manager, new object[] { new WorldBombMotion { scene = replica.scene.name, motion = motion } });
        public void Present(float dt) => typeof(NetworkBombFx).GetMethod("PresentBombMotions", Flags).Invoke(fx, new object[] { dt, dt });
        public void Dispose()
        {
            NetworkClient.spawned.Remove(ActorId); NetworkClient.spawned.Remove(BombId);
            if (actor != null) Object.DestroyImmediate(actor);
            Object.DestroyImmediate(replica); Object.DestroyImmediate(managerObject);
            NetSync.Mode = mode; clientState.SetValue(null, previousClient); paused.SetValue(null, previousPause); NetworkClient.isLoadingScene = previousLoading;
        }
    }
}

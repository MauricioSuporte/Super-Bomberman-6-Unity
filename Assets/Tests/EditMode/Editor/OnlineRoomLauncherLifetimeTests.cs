using System;
using System.Collections;
using System.Reflection;
using Assets.Scripts.Netcode;
using Mirror;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed partial class OnlineRoomEditModeTests
{
    sealed class CannonLifetimeFixture : IDisposable
    {
        readonly NetSync.NetMode oldMode = NetSync.Mode;
        readonly GameObject actor = new("CannonLifetimeActor"), mechanism = new("CannonLifetimeMechanism");
        public readonly MovementController movement;
        public readonly CannonLauncher cannon;
        public readonly Collider2D collider;
        public readonly BombController bombs;
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        public CannonLifetimeFixture()
        {
            NetSync.Mode = NetSync.NetMode.Offline;
            actor.SetActive(false); actor.tag = "Player";
            collider = actor.AddComponent<BoxCollider2D>();
            movement = actor.AddComponent<MovementController>();
            typeof(MovementController).GetProperty("Rigidbody").SetValue(movement, actor.GetComponent<Rigidbody2D>());
            bombs = actor.GetComponent<BombController>();
            mechanism.AddComponent<BoxCollider2D>();
            cannon = mechanism.AddComponent<CannonLauncher>();
            typeof(CannonLauncher).GetField("warmupSeconds", Private).SetValue(cannon, 10f);
        }

        public IEnumerator Start(string method) => (IEnumerator)typeof(CannonLauncher).GetMethod(method, Private)
            .Invoke(cannon, method == "LaunchPlayerArc" ? new object[] { movement, Vector2.right } : new object[] { movement });

        public void Enter() => typeof(CannonLauncher).GetMethod("OnTriggerEnter2D", Private).Invoke(cannon, new object[] { collider });
        public void Disable(bool componentOnly = false)
        {
            if (componentOnly) cannon.enabled = false;
            else mechanism.SetActive(false);
        }
        public void SkipWarmup()
        {
            foreach (var field in new[] { "warmupSeconds", "smokeSeconds" })
                typeof(CannonLauncher).GetField(field, Private).SetValue(cannon, 0f);
        }
        public NetworkPlayerInput ConfigureServer()
        {
            var input = actor.AddComponent<NetworkPlayerInput>();
            var identity = actor.GetComponent<NetworkIdentity>();
            typeof(NetworkIdentity).GetMethod("InitializeNetworkBehaviours", Private).Invoke(identity, null);
            typeof(NetworkIdentity).GetProperty("netId").SetValue(identity, 731u);
            typeof(NetworkIdentity).GetProperty("isServer").SetValue(identity, true);
            NetSync.Mode = NetSync.NetMode.Host;
            return input;
        }
        public void Dispose()
        {
            if (actor.TryGetComponent<NetworkIdentity>(out var identity))
                typeof(NetworkIdentity).GetProperty("isServer").SetValue(identity, false);
            Object.DestroyImmediate(mechanism); Object.DestroyImmediate(actor); NetSync.Mode = oldMode;
        }
    }

    [TestCase(true, false)] [TestCase(false, true)]
    public void CannonFlightDisposalRestoresPriorCollisionBombAndProtection(bool enabledBefore, bool invulnerableBefore)
    {
        using var fixture = new CannonLifetimeFixture();
        fixture.collider.enabled = fixture.bombs.enabled = enabledBefore;
        fixture.movement.SetExplosionInvulnerable(invulnerableBefore);
        var flight = fixture.Start("LaunchPlayerArc");
        try
        {
            Assert.IsTrue(flight.MoveNext());
            Assert.IsFalse(fixture.collider.enabled); Assert.IsFalse(fixture.bombs.enabled);
            Assert.IsTrue(fixture.movement.explosionInvulnerable);
        }
        finally { (flight as IDisposable)?.Dispose(); }
        Assert.AreEqual(enabledBefore, fixture.collider.enabled);
        Assert.AreEqual(enabledBefore, fixture.bombs.enabled);
        Assert.AreEqual(invulnerableBefore, fixture.movement.explosionInvulnerable);
    }

    [Test]
    public void CannonWarmupDisposalReleasesTheActorAndCanBeUsedAgain()
    {
        using var fixture = new CannonLifetimeFixture();
        for (int attempt = 0; attempt < 2; attempt++)
        {
            var fire = fixture.Start("FireRoutine");
            try { Assert.IsTrue(fire.MoveNext()); Assert.IsTrue(fixture.movement.InputLocked); }
            finally { (fire as IDisposable)?.Dispose(); }
            Assert.IsFalse(fixture.movement.InputLocked); Assert.IsFalse(fixture.movement.VisualOverrideActive);
        }
    }

    [UnityTest]
    public IEnumerator DisablingCannonDuringNativeWarmupOrFlightRestoresActor()
    {
        // Use the engine lifecycle: EditMode objects do not receive ordinary
        // MonoBehaviour OnDisable just because their GameObject is toggled.
        yield return new EnterPlayMode();
        foreach (var phase in new[] { (flight: false, component: false), (flight: true, component: false),
            (flight: false, component: true), (flight: true, component: true) })
        {
            using var fixture = new CannonLifetimeFixture();
            if (phase.flight) fixture.SkipWarmup();
            fixture.Enter(); Assert.IsTrue(fixture.movement.InputLocked);
            if (phase.flight)
            {
                float deadline = Time.realtimeSinceStartup + 2;
                while (fixture.collider.enabled && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.IsFalse(fixture.collider.enabled, "Native flight must begin before testing interruption.");
            }
            fixture.Disable(phase.component);
            fixture.Enter(); // A disabled trigger must not acquire the actor again.
            Assert.IsFalse(fixture.movement.InputLocked); Assert.IsFalse(fixture.movement.VisualOverrideActive);
            Assert.IsTrue(fixture.collider.enabled); Assert.IsTrue(fixture.bombs.enabled);
            Assert.IsFalse(fixture.movement.explosionInvulnerable);
        }
        yield return new ExitPlayMode();
    }

    [Test]
    public void CannonApprovedMotionCoversWarmupAndEndsAfterRestoringControl()
    {
        using var fixture = new CannonLifetimeFixture();
        var input = fixture.ConfigureServer();
        var token = typeof(NetworkPlayerInput).GetField("authorityMotionToken", BindingFlags.Instance | BindingFlags.NonPublic);
        var fire = fixture.Start("FireRoutine");
        try { Assert.IsTrue(fire.MoveNext()); Assert.AreNotEqual(0u, token.GetValue(input)); }
        finally { (fire as IDisposable)?.Dispose(); }
        Assert.AreEqual(0u, token.GetValue(input));
        Assert.IsFalse(fixture.movement.InputLocked);
    }

    [Test]
    public void DisposingCannonParentDuringItsYieldedFlightRestoresAllActorState()
    {
        using var fixture = new CannonLifetimeFixture();
        fixture.SkipWarmup();
        var fire = fixture.Start("FireRoutine");
        try
        {
            Assert.IsTrue(fire.MoveNext());
            Assert.IsFalse(((IEnumerator)fire.Current).MoveNext());
            Assert.IsTrue(fire.MoveNext());
            Assert.IsTrue(((IEnumerator)fire.Current).MoveNext());
            Assert.IsFalse(fixture.collider.enabled); Assert.IsFalse(fixture.bombs.enabled);
            Assert.IsTrue(fixture.movement.explosionInvulnerable);
        }
        finally { (fire as IDisposable)?.Dispose(); }
        Assert.IsFalse(fixture.movement.InputLocked); Assert.IsFalse(fixture.movement.VisualOverrideActive);
        Assert.IsTrue(fixture.collider.enabled); Assert.IsTrue(fixture.bombs.enabled);
        Assert.IsFalse(fixture.movement.explosionInvulnerable);
    }

    [Test]
    public void CannonDoesNotKeepTheCancelledLouieDashInputLock()
    {
        using var fixture = new CannonLifetimeFixture();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var dash = fixture.movement.gameObject.AddComponent<GreenLouieDashAbility>();
        typeof(GreenLouieDashAbility).GetMethod("Awake", flags).Invoke(dash, null);
        typeof(GreenLouieDashAbility).GetField("dashActive", flags).SetValue(dash, true);
        fixture.movement.SetInputLocked(true, forceIdle: false);
        var fire = fixture.Start("FireRoutine");
        try { Assert.IsTrue(fire.MoveNext()); Assert.IsFalse(dash.DashActive); }
        finally { (fire as IDisposable)?.Dispose(); }
        Assert.IsFalse(fixture.movement.InputLocked);
    }
}

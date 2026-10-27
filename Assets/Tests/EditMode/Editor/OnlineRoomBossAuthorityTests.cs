using System;
using System.Reflection;
using Assets.Scripts.Netcode;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed partial class OnlineRoomEditModeTests
{
    [TestCase(typeof(SunMaskMovement), "FixedUpdate", "hitStopTimer")]
    [TestCase(typeof(ClownMaskMovement), "FixedUpdate", "hitStopTimer")]
    [TestCase(typeof(SunMaskBoss), "Kill", "isDead")]
    [TestCase(typeof(ClownMaskBoss), "Kill", "isDead")]
    [TestCase(typeof(FreezerVenusBoss), "Kill", "dead")]
    [TestCase(typeof(FreezerVenusBoss), "BeginCombat", "<CombatActive>k__BackingField")]
    [TestCase(typeof(MechaBossSequence), "Start", "initialized")]
    [TestCase(typeof(MechaBossSequence), "TryStartSequence", "sequenceStarted")]
    [TestCase(typeof(SunMaskHeartProjectile), "TriggerDestroy", "dying")]
    [TestCase(typeof(StarProjectile), "Initialize", "initialized")]
    [TestCase(typeof(StarProjectile), "InitializeOrbit", "orbiting")]
    [TestCase(typeof(SunMaskHeartProjectile), "Initialize", "initialized")]
    public void PureClientBossEntriesCannotChooseAttackMoveOrStartNativeSequence(Type type, string methodName, string fieldName)
    {
        var oldMode = NetSync.Mode; var random = UnityEngine.Random.state;
        var actor = new GameObject("BossAuthority"); actor.SetActive(false); actor.AddComponent<BoxCollider2D>();
        var probe = new GameObject("BossAuthorityBody");
        var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        try
        {
            NetSync.Mode = NetSync.NetMode.Client;
            var component = actor.AddComponent(type);
            var bodyField = type.GetField("rb", flags) ?? type.GetField("bossRb", flags);
            Rigidbody2D body = null;
            if (bodyField != null)
            {
                body = probe.AddComponent<Rigidbody2D>(); body.gravityScale = 0;
                bodyField.SetValue(component, body);
                body.position = new Vector2(20, 21); body.linearVelocity = new Vector2(2, 3);
                Assert.AreEqual(new Vector2(20, 21), body.position, "Assert the active native fixture before invoking gameplay.");
                Assert.AreEqual(new Vector2(2, 3), body.linearVelocity);
            }
            var field = type.GetField(fieldName, flags); Assert.IsNotNull(field);
            if (field.FieldType == typeof(float)) field.SetValue(component, 1f);
            if (type == typeof(MechaBossSequence) && methodName == "TryStartSequence")
                type.GetField("initialized", flags).SetValue(component, true);
            var before = field.GetValue(component);
            var method = type.GetMethod(methodName, flags); Assert.IsNotNull(method);
            var parameters = method.GetParameters(); var args = new object[parameters.Length];
            for (int i = 0; i < args.Length; i++)
                args[i] = parameters[i].ParameterType == typeof(float) ? 99f :
                    parameters[i].ParameterType == typeof(int) ? 3 :
                    parameters[i].ParameterType == typeof(Vector2) ? Vector2.up : null;
            method.Invoke(component, args);
            Assert.AreEqual(before, field.GetValue(component));
            Assert.AreEqual(random, UnityEngine.Random.state);
            if (body != null)
            {
                Assert.AreEqual(new Vector2(20, 21), body.position);
                Assert.AreEqual(new Vector2(2, 3), body.linearVelocity);
            }
        }
        finally { Object.DestroyImmediate(actor); Object.DestroyImmediate(probe); NetSync.Mode = oldMode; UnityEngine.Random.state = random; }
    }

    [TestCase(typeof(StageAssets.RocketPenguinController), "ScheduleNextShot", "waitRemaining")]
    [TestCase(typeof(StageAssets.RocketPenguinController), "BeginShot", "firing")]
    [TestCase(typeof(StageAssets.SnowBallTrap), "Update", "rolling")]
    public void PureClientEnvironmentalHazardCannotChoosePhaseOrStartMotion(Type type, string methodName, string fieldName)
    {
        var oldMode = NetSync.Mode; var random = UnityEngine.Random.state;
        var actor = new GameObject("EnvironmentAuthority"); actor.SetActive(false);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        try
        {
            NetSync.Mode = NetSync.NetMode.Client; var component = actor.AddComponent(type);
            var field = type.GetField(fieldName, flags); Assert.IsNotNull(field);
            if (type == typeof(StageAssets.SnowBallTrap)) field.SetValue(component, true);
            var targets = type.GetField("availableTargets", flags)?.GetValue(component) as System.Collections.IList;
            targets?.Add(Vector3.one);
            var before = field.GetValue(component);
            type.GetMethod(methodName, flags).Invoke(component, null);
            if (targets != null) Assert.AreEqual(1, targets.Count, "Client cannot consume the server's target candidates.");
            Assert.AreEqual(before, field.GetValue(component));
            Assert.AreEqual(random, UnityEngine.Random.state);
            Assert.AreEqual(Vector3.zero, actor.transform.position);
        }
        finally { Object.DestroyImmediate(actor); NetSync.Mode = oldMode; UnityEngine.Random.state = random; }
    }

    [Test]
    public void PureClientBarrelPillarsCannotConsumeExplosionOrStartFall()
    {
        var oldMode = NetSync.Mode;
        var actor = new GameObject("BarrelAuthority"); actor.SetActive(false);
        try
        {
            NetSync.Mode = NetSync.NetMode.Client;
            var trap = actor.AddComponent<StageAssets.BarrelPillarTrap>();
            var a = new GameObject("PillarA"); a.transform.SetParent(actor.transform);
            var b = new GameObject("PillarB"); b.transform.SetParent(actor.transform);
            var first = a.AddComponent<StageAssets.BarrelPillarTrapPillar>();
            var second = b.AddComponent<StageAssets.BarrelPillarTrapPillar>();
            Assert.IsFalse(first.TryDestroyByExplosion(BombExplosion.ExplosionPart.Start));
            Assert.IsFalse(second.TryDestroyByExplosion(BombExplosion.ExplosionPart.Start));
            var notify = typeof(StageAssets.BarrelPillarTrap).GetMethod("NotifyPillarDestroyed", BindingFlags.Instance | BindingFlags.NonPublic);
            notify.Invoke(trap, new object[] { first }); notify.Invoke(trap, new object[] { second });
            Assert.IsFalse(trap.AreBothPillarsDestroyed);
            Assert.IsFalse((bool)typeof(StageAssets.BarrelPillarTrap).GetField("fallStarted", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(trap));
            Assert.AreEqual(Vector3.zero, actor.transform.position);
        }
        finally { Object.DestroyImmediate(actor); NetSync.Mode = oldMode; }
    }

    [Test]
    public void BossProjectileAndSharedContactHelpersCannotDamageAPlayerOnPureClient()
    {
        var oldMode = NetSync.Mode;
        var source = new GameObject("BossContacts"); source.SetActive(false); source.AddComponent<BoxCollider2D>();
        var target = new GameObject("BossContactPlayer"); target.SetActive(false); target.tag = "Player";
        var collider = target.AddComponent<BoxCollider2D>(); target.AddComponent<MovementController>();
        var health = target.GetComponent<CharacterHealth>();
        if (health == null) health = target.AddComponent<CharacterHealth>();
        health.life = 3;
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        try
        {
            NetSync.Mode = NetSync.NetMode.Client;
            Assert.IsFalse(FreezerVenusBoss.TryHitPlayer(collider));
            var star = source.AddComponent<StarProjectile>();
            Assert.IsFalse((bool)typeof(StarProjectile).GetMethod("TryApplyPlayerHit", flags).Invoke(star, new object[] { collider }));
            foreach (var type in new[] { typeof(SunMaskBoss), typeof(ClownMaskBoss), typeof(FreezerVenusBoss) })
            {
                var component = source.AddComponent(type);
                foreach (string method in new[] { "OnTriggerEnter2D", "OnTriggerStay2D" })
                    type.GetMethod(method, flags).Invoke(component, new object[] { collider });
            }
            Assert.AreEqual(3, health.life);
        }
        finally { Object.DestroyImmediate(source); Object.DestroyImmediate(target); NetSync.Mode = oldMode; }
    }

    [Test]
    public void ClientHeartCannotChangeGlobalLayerCollisionRules()
    {
        var oldMode = NetSync.Mode;
        int playerLayer = LayerMask.NameToLayer("Player"); Assert.GreaterOrEqual(playerLayer, 0);
        bool ignored = Physics2D.GetIgnoreLayerCollision(0, playerLayer);
        var actor = new GameObject("HeartCollisionRules"); actor.SetActive(false); actor.AddComponent<CircleCollider2D>();
        try
        {
            var heart = actor.AddComponent<SunMaskHeartProjectile>();
            var apply = typeof(SunMaskHeartProjectile).GetMethod("ApplyLayerIgnores", BindingFlags.Instance | BindingFlags.NonPublic);
            Physics2D.IgnoreLayerCollision(0, playerLayer, false);
            NetSync.Mode = NetSync.NetMode.Client; apply.Invoke(heart, null);
            Assert.IsFalse(Physics2D.GetIgnoreLayerCollision(0, playerLayer));
            NetSync.Mode = NetSync.NetMode.Offline; apply.Invoke(heart, null);
            Assert.IsTrue(Physics2D.GetIgnoreLayerCollision(0, playerLayer));
        }
        finally { Object.DestroyImmediate(actor); Physics2D.IgnoreLayerCollision(0, playerLayer, ignored); NetSync.Mode = oldMode; }
    }

    [Test]
    public void SunMaskDirectionStillUsesNativeRandomOfflineButDoesNotDrawOnClient()
    {
        var oldMode = NetSync.Mode; var random = UnityEngine.Random.state;
        var actor = new GameObject("SunMaskDirection"); actor.SetActive(false);
        try
        {
            var movement = actor.AddComponent<SunMaskMovement>(); movement.randomizeStartDirection = true;
            var choose = typeof(SunMaskMovement).GetMethod("PickInitialDirection", BindingFlags.Instance | BindingFlags.NonPublic);
            UnityEngine.Random.InitState(358); var before = UnityEngine.Random.state;
            NetSync.Mode = NetSync.NetMode.Client; choose.Invoke(movement, null);
            Assert.AreEqual(before, UnityEngine.Random.state);
            NetSync.Mode = NetSync.NetMode.Offline; choose.Invoke(movement, null);
            Assert.AreNotEqual(before, UnityEngine.Random.state);
            Assert.AreEqual(1f, movement.GetCurrentDirection().magnitude, .0001f);
        }
        finally { Object.DestroyImmediate(actor); NetSync.Mode = oldMode; UnityEngine.Random.state = random; }
    }
}

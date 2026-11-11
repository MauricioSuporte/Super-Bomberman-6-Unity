using System.Reflection;
using Assets.Scripts.Netcode;
using NUnit.Framework;
using UnityEngine;

public sealed partial class OnlineRoomEditModeTests
{
    [Test]
    public void PlainCoopRendererRegistersBeforeDiscoveryWithoutDuplicatingOrOwningGameplay()
    {
        bool oldNetworkScene = NetSync.IsNetworkedScene;
        var host = new GameObject("ProjectileRegistration"); host.SetActive(false);
        var actor = new GameObject("PlainProjectile", typeof(SpriteRenderer));
        var parent = new GameObject("AlreadyNetworked", typeof(Mirror.NetworkIdentity));
        var catalog = ScriptableObject.CreateInstance<CoopSceneCatalog>();
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        try
        {
            var manager = host.AddComponent<BombermanNetworkManager>();
            typeof(BombermanNetworkManager).GetField("coopCatalog", flags).SetValue(manager, catalog);
            typeof(BombermanNetworkManager).GetField("coopScene", flags).SetValue(manager, actor.scene.name);
            var track = typeof(BombermanNetworkManager).GetMethod("TrackCoopRenderer", flags);
            var pending = (System.Collections.Generic.HashSet<SpriteRenderer>)typeof(BombermanNetworkManager)
                .GetField("coopPendingRenderers", flags).GetValue(manager);
            var renderer = actor.GetComponent<SpriteRenderer>();
            track.Invoke(manager, new object[] { renderer }); track.Invoke(manager, new object[] { renderer });
            Assert.AreEqual(1, pending.Count); Assert.IsTrue(pending.Contains(renderer));
            Assert.IsNull(actor.GetComponent<Rigidbody2D>()); Assert.IsNull(actor.GetComponent<Mirror.NetworkIdentity>());
            typeof(BombermanNetworkManager).GetMethod("ClearCoopSprites", flags).Invoke(manager, null);
            Assert.AreEqual(0, pending.Count); Assert.IsNotNull(renderer);
            actor.transform.SetParent(parent.transform);
            track.Invoke(manager, new object[] { renderer }); Assert.AreEqual(0, pending.Count);
            actor.transform.SetParent(null);
            typeof(BombermanNetworkManager).GetField("coopScene", flags).SetValue(manager, "OtherScene");
            track.Invoke(manager, new object[] { renderer }); Assert.AreEqual(0, pending.Count);
        }
        finally
        {
            Object.DestroyImmediate(host); Object.DestroyImmediate(actor); Object.DestroyImmediate(parent);
            Object.DestroyImmediate(catalog); NetSync.IsNetworkedScene = oldNetworkScene;
        }
    }

    [Test]
    public void ClientProjectileFactoriesNeverCreateGameplayObjectsOrChooseTargets()
    {
        var oldMode = NetSync.Mode; var random = Random.state;
        int bodies = Object.FindObjectsByType<Rigidbody2D>(FindObjectsInactive.Include).Length;
        try
        {
            NetSync.Mode = NetSync.NetMode.Client;
            HeliProjectile.Create(Vector2.zero, Vector2.up, null, null, null, null);
            IcicleIceProjectile.Create(Vector2.zero, Vector2.up, null, null, null, null);
            Assert.IsNull(MatangoProjectile.Create(Vector2.zero, Vector2.up, 4, 1, .2f, null));
            Assert.IsNull(FreezerVenusProjectile.Create(null, FreezerVenusProjectile.AttackKind.Tornado,
                System.Array.Empty<Sprite>(), Vector3.zero, Vector2.up));
            Assert.IsNull(QueenOyobeeProjectile.Create(null, true, 0));
            Assert.IsNull(QueenOyobeeProjectile.CreateTelegraph(null, null));
            Assert.IsNull(QueenOyobeeProjectile.CreateRitual(null));
            DespiderWebProjectile.Launch(null, null, Vector2.zero, Vector2.up, 1);
            var fire = new GameObject("ClientFarohLaunch");
            try
            {
                fire.AddComponent<FarohFireProjectile>().Launch(Vector2.up, 2, 1, 5, .7f, 0);
                Assert.IsNull(fire.GetComponent<CoopProjectileVisual>());
                Assert.AreEqual(Vector3.zero, fire.transform.position);
            }
            finally { Object.DestroyImmediate(fire); }
            Assert.AreEqual(bodies, Object.FindObjectsByType<Rigidbody2D>(FindObjectsInactive.Include).Length);
            Assert.AreEqual(random, Random.state);
        }
        finally { NetSync.Mode = oldMode; Random.state = random; }
    }

    [TestCase(typeof(HeliProjectile))]
    [TestCase(typeof(IcicleIceProjectile))]
    [TestCase(typeof(FishManSpear))]
    [TestCase(typeof(MatangoProjectile))]
    [TestCase(typeof(FreezerVenusProjectile))]
    [TestCase(typeof(QueenOyobeeProjectile))]
    [TestCase(typeof(BirdRobotRocket))]
    [TestCase(typeof(DespiderWebProjectile))]
    [TestCase(typeof(FarohFireProjectile))]
    [TestCase(typeof(JellyFishShot))]
    public void PureClientProjectileCannotAdvanceNativeMotionOrRunContacts(System.Type type)
    {
        var oldMode = NetSync.Mode; var random = Random.state;
        var actor = new GameObject("ClientProjectileAuthority"); actor.SetActive(false);
        try
        {
            NetSync.Mode = NetSync.NetMode.Client;
            actor.transform.position = new Vector3(21, 22, 0);
            actor.AddComponent<Rigidbody2D>(); actor.AddComponent<CircleCollider2D>();
            var component = actor.AddComponent(type);
            Assert.IsNotNull(component);
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            type.GetMethod("FixedUpdate", flags).Invoke(component, null);
            type.GetMethod("OnTriggerEnter2D", flags)?.Invoke(component, new object[] { null });
            Assert.AreEqual(new Vector3(21, 22, 0), actor.transform.position);
            Assert.AreEqual(random, Random.state);
            BombermanNetworkManager.CoopTrackRenderer(actor.GetComponent<SpriteRenderer>());
            BombermanNetworkManager.CoopTrackAnimation(null);
            Assert.IsNotNull(actor);
        }
        finally { Object.DestroyImmediate(actor); NetSync.Mode = oldMode; Random.state = random; }
    }

    [Test]
    public void R05ProjectileEdgesPreserveRotationScaleBlinkAndStopWithoutAClientCollider()
    {
        var actor = new GameObject("R05Edges", typeof(SpriteRenderer));
        var host = new GameObject("R05Stop"); host.SetActive(false);
        var catalog = ScriptableObject.CreateInstance<CoopSceneCatalog>();
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var type = typeof(BombermanNetworkManager);
        try
        {
            catalog.sprites = System.Array.Empty<Sprite>();
            var manager = host.AddComponent<BombermanNetworkManager>();
            type.GetField("coopCatalog", flags).SetValue(manager, catalog);
            var renderer = actor.GetComponent<SpriteRenderer>();
            var frame = new CoopSpriteFrame { id = renderer.GetEntityId().GetHashCode(), visible = true,
                rotation = Quaternion.identity, scale = Vector3.one, color = Color.white, time = Mirror.NetworkTime.localTime - .1 };
            var edge = type.GetMethod("NeedsCoopProjectileEdge", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsFalse((bool)edge.Invoke(null, new object[] { renderer, frame }));
            actor.transform.rotation = Quaternion.Euler(0, 0, 90);
            Assert.IsTrue((bool)edge.Invoke(null, new object[] { renderer, frame }));
            actor.transform.rotation = Quaternion.identity; renderer.enabled = false;
            Assert.IsTrue((bool)edge.Invoke(null, new object[] { renderer, frame }));
            renderer.enabled = true; renderer.flipX = true;
            Assert.IsTrue((bool)edge.Invoke(null, new object[] { renderer, frame }));
            renderer.flipX = false; actor.transform.localScale = Vector3.one * 2;
            Assert.IsTrue((bool)edge.Invoke(null, new object[] { renderer, frame }));
            var frames = (System.Collections.Generic.Dictionary<int, CoopSpriteFrame>)type.GetField("coopLastSprites", flags).GetValue(manager);
            frames[frame.id] = frame;
            actor.transform.position = Vector3.right * 4;
            type.GetMethod("PublishCoopProjectileEnd", flags).Invoke(manager, new object[] { renderer });
            var stopped = frames[frame.id];
            Assert.IsFalse(stopped.visible);
            Assert.AreEqual(actor.transform.position, stopped.position);
            Assert.Greater(stopped.time, frame.time);
            Assert.IsNull(actor.GetComponent<Collider2D>());
            Assert.IsNull(actor.GetComponent<Rigidbody2D>());
            type.GetMethod("ClearCoopSprites", flags).Invoke(manager, null);
            Assert.AreEqual(0, ((System.Collections.IDictionary)type.GetField("coopProjectiles", flags).GetValue(manager)).Count);
        }
        finally { Object.DestroyImmediate(host); Object.DestroyImmediate(actor); Object.DestroyImmediate(catalog); }
    }

    [Test]
    public void R05SameFrameFullStateFinishesInitializationWithoutRewindingOrRevivingRemoval()
    {
        var host = new GameObject("R05IntraFrame"); host.SetActive(false);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        try
        {
            var manager = host.AddComponent<BombermanNetworkManager>(); var type = typeof(BombermanNetworkManager);
            var append = type.GetMethod("AppendCoopSpriteFrame", flags);
            var all = (System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<CoopVisualSample>>)type.GetField("coopVisualSamples", flags).GetValue(manager);
            var pending = new CoopSpriteFrame { id = 9, time = 10, visible = false, sprite = -1 };
            append.Invoke(manager, new object[] { pending });
            var ready = pending; ready.visible = true; ready.sprite = 5;
            append.Invoke(manager, new object[] { ready });
            Assert.AreEqual(1, all[9].Count);
            Assert.IsTrue(all[9][0].frame.visible);
            Assert.AreEqual(5, all[9][0].frame.sprite);
            pending.time = 9; append.Invoke(manager, new object[] { pending });
            Assert.IsTrue(all[9][0].frame.visible, "An older frame cannot undo initialization.");
            ((System.Collections.Generic.Dictionary<int, double>)type.GetField("coopVisualRemovals", flags).GetValue(manager))[9] = 10;
            append.Invoke(manager, new object[] { pending }); append.Invoke(manager, new object[] { ready });
            Assert.AreEqual(1, all[9].Count, "A removed identity cannot be revived by a duplicate full frame.");
        }
        finally { Object.DestroyImmediate(host); }
    }

    [Test]
    public void R05PureClientFlameLaunchesDoNotCreatePhysicsOrStartNativeCoroutines()
    {
        var oldMode = NetSync.Mode;
        var actor = new GameObject("R05PureFlame");
        try
        {
            NetSync.Mode = NetSync.NetMode.Client;
            actor.AddComponent<FuramaFlameVisual>().Play(Vector2.one, Vector2.up, 1, 3, 1, .5f);
            actor.AddComponent<BubbleFuramaFlame>().Launch(null, Vector2.up, 1, 3, null, null);
            typeof(BubbleFuramaFlame).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(actor.GetComponent<BubbleFuramaFlame>(), null);
            Assert.AreEqual(Vector3.zero, actor.transform.position);
            Assert.IsNull(actor.GetComponent<Collider2D>());
            Assert.IsNull(actor.GetComponent<Rigidbody2D>());
        }
        finally { Object.DestroyImmediate(actor); NetSync.Mode = oldMode; }
    }

    [Test]
    public void R05PendingMergePreservesR03FirstShadowFrameBeforePeriodicDiscovery()
    {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic; var type = typeof(BombermanNetworkManager);
        var host = new GameObject("R05FreshShadowManager"); host.SetActive(false);
        var shadow = new GameObject("R03FreshShadow", typeof(SpriteRenderer));
        var catalog = ScriptableObject.CreateInstance<CoopSceneCatalog>();
        try
        {
            catalog.sprites = System.Array.Empty<Sprite>();
            var manager = host.AddComponent<BombermanNetworkManager>(); var renderer = shadow.GetComponent<SpriteRenderer>();
            int id = renderer.GetEntityId().GetHashCode();
            type.GetField("coopScene", flags).SetValue(manager, shadow.scene.name);
            type.GetField("coopCatalog", flags).SetValue(manager, catalog);
            ((System.Collections.Generic.Dictionary<int, float>)type.GetField("coopEnemyVisualLimits", flags).GetValue(manager))[id] = 1.5f;
            ((System.Collections.Generic.HashSet<SpriteRenderer>)type.GetField("coopPendingRenderers", flags).GetValue(manager)).Add(renderer);
            type.GetMethod("MergeCoopPendingRenderers", flags).Invoke(manager, null);
            Assert.AreEqual(0, ((System.Collections.Generic.HashSet<SpriteRenderer>)type.GetField("coopPendingRenderers", flags).GetValue(manager)).Count);
            type.GetMethod("PublishCoopEnemyTransitions", flags).Invoke(manager, new object[] { false });
            var frames = (System.Collections.Generic.List<CoopSpriteFrame>)type.GetField("coopEnemyTransitions", flags).GetValue(manager);
            Assert.AreEqual(1, frames.Count, "Merging the renderer must retain its native first shadow edge.");
            Assert.AreEqual(id, frames[0].id); Assert.IsTrue(frames[0].visible);
            type.GetMethod("PublishCoopEnemyTransitions", flags).Invoke(manager, new object[] { false });
            Assert.AreEqual(0, frames.Count, "The initial shadow is published once.");
        }
        finally { Object.DestroyImmediate(host); Object.DestroyImmediate(shadow); Object.DestroyImmediate(catalog); }
    }

    [Test]
    public void R05ProjectileSnapshotDoesNotInventFramesOrFreezeNativeAnimator()
    {
        var actor = new GameObject("R05NativeAnimation", typeof(SpriteRenderer));
        try
        {
            var animation = actor.AddComponent<AnimatedSpriteRenderer>();
            animation.idle = false;
            var before = animation.CapturePlayback();
            var approved = (AnimatedSpriteRenderer.PlaybackState)typeof(BombermanNetworkManager).GetMethod("CaptureCoopProjectilePlayback", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { animation });
            Assert.AreEqual(0, approved.speed);
            Assert.AreEqual(before.frame, approved.frame);
            Assert.AreEqual(before.speed, animation.CapturePlayback().speed);
            Assert.IsFalse(animation.ManualAnimationUpdate);
        }
        finally { Object.DestroyImmediate(actor); }
    }

    [Test]
    public void R05ObserverFreezesVisualWhileDecodedRootMovesAndWaitsForResumeSample()
    {
        var oldMode = NetSync.Mode;
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var clientState = typeof(Mirror.NetworkClient).GetField("connectState", BindingFlags.NonPublic | BindingFlags.Static);
        var oldState = clientState.GetValue(null);
        var paused = typeof(GamePauseController).GetProperty(nameof(GamePauseController.IsPaused));
        bool oldPaused = GamePauseController.IsPaused;
        var projectile = new GameObject("R05ObserverShot"); projectile.SetActive(false);
        var child = new GameObject("ShotSprite", typeof(SpriteRenderer)); child.transform.SetParent(projectile.transform, false);
        try
        {
            NetSync.Mode = NetSync.NetMode.Client;
            clientState.SetValue(null, System.Enum.Parse(clientState.FieldType, "Connected"));
            projectile.AddComponent<BoxCollider2D>();
            var identity = projectile.AddComponent<Mirror.NetworkIdentity>();
            var shot = projectile.AddComponent<NetworkTankShot>();
            typeof(Mirror.NetworkIdentity).GetMethod("InitializeNetworkBehaviours", flags).Invoke(identity, null);
            typeof(NetworkTankShot).GetField("direction", flags).SetValue(shot, Vector2.right);
            typeof(NetworkTankShot).GetField("launchSpeed", flags).SetValue(shot, 8f);
            typeof(NetworkTankShot).GetField("launchTime", flags).SetValue(shot, NetworkBombFx.KickServerNow - 10);
            paused.SetValue(null, false);
            var update = typeof(NetworkTankShot).GetMethod("LateUpdate", flags);
            update.Invoke(shot, null);
            Assert.IsTrue((bool)typeof(NetworkTankShot).GetProperty("ObserverPresentationActive", flags).GetValue(shot));
            Assert.That(child.transform.position.x, Is.InRange(0f, 1f), "Confirmed flight has a bounded preview even with old snapshots.");
            Assert.AreEqual(Vector3.zero, projectile.transform.position);
            Vector3 displayed = child.transform.position;
            paused.SetValue(null, true); projectile.transform.position = Vector3.right * 10;
            update.Invoke(shot, null);
            Assert.That(Vector3.Distance(displayed, child.transform.position), Is.LessThan(.00001f));
            Assert.AreEqual(Vector3.right * 10, projectile.transform.position);
            paused.SetValue(null, false); update.Invoke(shot, null);
            Assert.That(Vector3.Distance(displayed, child.transform.position), Is.LessThan(.00001f));
            typeof(NetworkTankShot).GetMethod("OnDisable", flags).Invoke(shot, null);
            Assert.AreEqual(Vector3.zero, child.transform.localPosition);
            Assert.IsFalse((bool)typeof(NetworkTankShot).GetProperty("ObserverPresentationActive", flags).GetValue(shot));
        }
        finally
        {
            paused.SetValue(null, oldPaused); NetSync.Mode = oldMode; clientState.SetValue(null, oldState);
            Object.DestroyImmediate(projectile);
        }
    }
}

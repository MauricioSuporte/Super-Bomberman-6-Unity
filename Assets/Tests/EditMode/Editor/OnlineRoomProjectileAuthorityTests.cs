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
}

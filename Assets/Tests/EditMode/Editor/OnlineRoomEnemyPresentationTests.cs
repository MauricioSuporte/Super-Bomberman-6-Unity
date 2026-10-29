using System.Collections.Generic;
using System.Reflection;
using Assets.Scripts.Netcode;
using Mirror;
using NUnit.Framework;
using UnityEngine;

public sealed partial class OnlineRoomEditModeTests
{
    [TestCase(typeof(EnemyMovementController))]
    [TestCase(typeof(JunctionTurningEnemyMovementController))]
    [TestCase(typeof(FrogEnemyMovementController))]
    [TestCase(typeof(HopRobotMovementController))]
    [TestCase(typeof(SnowManMovementController))]
    [TestCase(typeof(KorobokkuruMovementController))]
    public void PureClientEnemyCannotStartNavigationAdvanceAIOrPickRandomDirection(System.Type type)
    {
        var oldMode = NetSync.Mode; var random = Random.state;
        var actor = new GameObject("NativeEnemyAuthority"); actor.SetActive(false);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        try
        {
            NetSync.Mode = NetSync.NetMode.Client;
            actor.AddComponent<Rigidbody2D>(); actor.AddComponent<BoxCollider2D>();
            actor.transform.position = new Vector3(21.3f, 22.4f);
            var enemy = (EnemyMovementController)actor.AddComponent(type); Assert.IsNotNull(enemy);
            type.GetMethod("Start", flags).Invoke(enemy, null); type.GetMethod("FixedUpdate", flags).Invoke(enemy, null);
            typeof(EnemyMovementController).GetMethod("ChooseInitialDirection", flags).Invoke(enemy, null);
            typeof(EnemyMovementController).GetMethod("OnTriggerEnter2D", flags).Invoke(enemy, new object[] { null });
            enemy.Kill();
            Assert.AreEqual(new Vector3(21.3f, 22.4f), actor.transform.position);
            Assert.IsFalse((bool)typeof(EnemyMovementController).GetField("isDead", flags).GetValue(enemy));
            Assert.AreEqual(random, Random.state); Assert.IsTrue(actor.GetComponent<Collider2D>().enabled);
            if (enemy is SnowManMovementController)
            {
                Assert.IsFalse((bool)type.GetMethod("TryBeginJump", flags).Invoke(enemy, null));
                type.GetMethod("OnBombDetonating", flags).Invoke(enemy, new object[] { Vector2.zero, 5 });
            }
        }
        finally { Object.DestroyImmediate(actor); NetSync.Mode = oldMode; Random.state = random; }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ApprovedEnemyBodyAndShadowShareTimelineFreezePresentedPoseAndCutWraps(bool projectile)
    {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var oldMode = NetSync.Mode; bool oldNetworkScene = NetSync.IsNetworkedScene;
        var clientState = typeof(NetworkClient).GetField("connectState", BindingFlags.Static | BindingFlags.NonPublic); var oldClient = clientState.GetValue(null);
        var timeline = typeof(NetworkClient).GetField("localTimeline", BindingFlags.Static | BindingFlags.NonPublic); var oldTimeline = timeline.GetValue(null);
        var paused = typeof(GamePauseController).GetField("<IsPaused>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic); var oldPaused = paused.GetValue(null);
        var host = new GameObject("EnemyViewCoordinator"); host.SetActive(false);
        var root = new GameObject("EnemyApprovedViews");
        var catalog = ScriptableObject.CreateInstance<CoopSceneCatalog>();
        var texture = new Texture2D(4, 4); var sprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), Vector2.one * .5f);
        BombermanNetworkManager manager = null;
        try
        {
            NetSync.Mode = NetSync.NetMode.Client; clientState.SetValue(null, System.Enum.Parse(clientState.FieldType, "Connected")); paused.SetValue(null, false);
            manager = host.AddComponent<BombermanNetworkManager>(); catalog.sprites = new[] { sprite }; catalog.materials = System.Array.Empty<Material>();
            typeof(BombermanNetworkManager).GetField("coopCatalog", flags).SetValue(manager, catalog);
            typeof(BombermanNetworkManager).GetField("coopViewRoot", flags).SetValue(manager, root);
            var samples = (Dictionary<int, List<CoopSpriteFrame>>)typeof(BombermanNetworkManager).GetField("coopVisualSamples", flags).GetValue(manager);
            var views = (Dictionary<int, SpriteRenderer>)typeof(BombermanNetworkManager).GetField("coopViews", flags).GetValue(manager);
            double clock = (double)typeof(BombermanNetworkManager).GetProperty("CoopVisualClock", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            var body = new CoopSpriteFrame { id = 31, time = clock - .1, sprite = 0, material = -1, visible = true,
                position = new Vector3(710, 710.5f), scale = Vector3.one, rotation = Quaternion.identity, color = Color.white, enemyStepLimit = projectile ? 0 : 1.5f, pauseWithGameplay = projectile };
            using (var writer = NetworkWriterPool.Get())
            { writer.Write(body); body = new NetworkReader(writer.ToArray()).Read<CoopSpriteFrame>(); }
            Assert.AreEqual(projectile ? 0 : 1.5f, body.enemyStepLimit); Assert.AreEqual(projectile, body.pauseWithGameplay);
            var nextBody = body; nextBody.time = clock + .1; nextBody.position = new Vector3(711, 711);
            var shadow = body; shadow.id = 32; shadow.position = new Vector3(710, 710);
            var nextShadow = shadow; nextShadow.time = nextBody.time; nextShadow.position = new Vector3(711, 710);
            samples[31] = new List<CoopSpriteFrame> { body, nextBody }; samples[32] = new List<CoopSpriteFrame> { shadow, nextShadow };
            var present = typeof(BombermanNetworkManager).GetMethod("PresentOnlineSprites", flags); present.Invoke(manager, null);
            Assert.AreEqual(710.5f, views[31].transform.position.x, .0001f); Assert.AreEqual(710.75f, views[31].transform.position.y, .0001f);
            Assert.AreEqual(710.5f, views[32].transform.position.x, .0001f); Assert.AreEqual(710, views[32].transform.position.y);
            Assert.IsNull(views[31].GetComponent<EnemyMovementController>()); Assert.IsNull(views[31].GetComponent<Rigidbody2D>());
            Assert.IsNull(views[31].GetComponent<Collider2D>());
            Vector3 frozen = views[31].transform.position;
            paused.SetValue(null, true); timeline.SetValue(null, (double)oldTimeline + .15); present.Invoke(manager, null);
            Assert.AreEqual(frozen, views[31].transform.position, "Buffered samples must not change an already presented paused enemy.");
            paused.SetValue(null, false); timeline.SetValue(null, oldTimeline);
            if (projectile) return; // This case proves pause without enabling the enemy wrap profile.
            var wrap = nextBody; wrap.position = body.position + Vector3.left * 12;
            samples[31] = new List<CoopSpriteFrame> { body, wrap }; present.Invoke(manager, null);
            Assert.AreEqual(body.position, views[31].transform.position, "A wrap is a cut at its approved timestamp, not movement across the room.");
            var from = body; from.time = clock - 1; var afterGap = nextBody; afterGap.time = clock + .1;
            samples[31] = new List<CoopSpriteFrame> { from, afterGap }; present.Invoke(manager, null);
            Assert.AreEqual(from.position, views[31].transform.position);
            timeline.SetValue(null, (double)oldTimeline + .2); present.Invoke(manager, null);
            Assert.AreEqual(afterGap.position, views[31].transform.position);
            typeof(BombermanNetworkManager).GetField("coopViewRoot", flags).SetValue(manager, null);
            views.Clear();
        }
        finally
        {
            if (manager != null)
            {
                typeof(BombermanNetworkManager).GetField("coopViewRoot", flags).SetValue(manager, null);
                ((Dictionary<int, SpriteRenderer>)typeof(BombermanNetworkManager).GetField("coopViews", flags).GetValue(manager)).Clear();
            }
            Object.DestroyImmediate(host); Object.DestroyImmediate(root); Object.DestroyImmediate(catalog);
            Object.DestroyImmediate(sprite); Object.DestroyImmediate(texture); NetSync.Mode = oldMode; NetSync.IsNetworkedScene = oldNetworkScene;
            clientState.SetValue(null, oldClient); timeline.SetValue(null, oldTimeline); paused.SetValue(null, oldPaused);
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Assets.Scripts.Netcode;
using Mirror;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class OnlineRoomEditModeTests
{
    [Test]
    public void BlackoutRoomGeometryRemainsQueryableWithDisabledColliderAndRotatedOffsetBounds()
    {
        var oldMode = NetSync.Mode; var actor = new GameObject("DisabledBlackoutRoom"); actor.SetActive(false);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        try
        {
            NetSync.Mode = NetSync.NetMode.Client;
            var box = actor.AddComponent<BoxCollider2D>(); box.size = new Vector2(8, 6); box.offset = new Vector2(2, -1);
            actor.transform.SetPositionAndRotation(new Vector3(23, 24), Quaternion.Euler(0, 0, 31));
            actor.transform.localScale = new Vector3(2, .75f, 1); box.enabled = false;
            var output = actor.AddComponent<WorldBlackoutRenderer>();
            typeof(WorldBlackoutRenderer).GetField("roomBounds", flags).SetValue(output, box);
            Bounds expected = new(actor.transform.TransformPoint(box.offset - box.size * .5f), Vector3.zero);
            foreach (float x in new[] { -1f, 1f }) foreach (float y in new[] { -1f, 1f })
                expected.Encapsulate(actor.transform.TransformPoint(box.offset + Vector2.Scale(box.size * .5f, new Vector2(x, y))));
            Assert.Less(Vector3.Distance(expected.center, output.RoomWorldBounds.center), .0001f);
            Assert.Less(Vector3.Distance(expected.size, output.RoomWorldBounds.size), .0001f);
            var contains = typeof(WorldBlackoutRenderer).GetMethod("RoomContains", flags);
            Assert.IsTrue((bool)contains.Invoke(output, new object[] { (Vector2)actor.transform.TransformPoint(box.offset) }));
            Assert.IsFalse((bool)contains.Invoke(output, new object[] { (Vector2)actor.transform.TransformPoint(box.offset + Vector2.right * 10) }));
            Assert.IsFalse(box.enabled); Assert.IsNull(actor.GetComponent<Rigidbody2D>());
            NetSync.Mode = NetSync.NetMode.Offline;
            Assert.AreEqual(box.bounds, output.RoomWorldBounds, "Offline keeps native collider bounds, including disabled rooms.");
            Assert.IsFalse((bool)contains.Invoke(output, new object[] { (Vector2)actor.transform.TransformPoint(box.offset) }));
        }
        finally { Object.DestroyImmediate(actor); NetSync.Mode = oldMode; }
    }

    [Test]
    public void BlackoutMaskRecipeRoundTripsIsolatesMaterialsAndDoesNotRevealWhenOccluderIsUnknown()
    {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        bool oldNetworkScene = NetSync.IsNetworkedScene;
        var host = new GameObject("BlackoutMaterialCoordinator"); host.SetActive(false);
        var first = new GameObject("FirstMask", typeof(SpriteRenderer)); var second = new GameObject("SecondMask", typeof(SpriteRenderer));
        var catalog = ScriptableObject.CreateInstance<CoopSceneCatalog>();
        var texture = new Texture2D(4, 4); var sprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), Vector2.one * .5f);
        BombermanNetworkManager manager = null;
        try
        {
            var template = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/BlackoutVisibleParts.mat"); Assert.IsNotNull(template);
            Color original = template.GetColor("_Color");
            manager = host.AddComponent<BombermanNetworkManager>();
            catalog.materials = new[] { template }; catalog.sprites = new[] { sprite };
            typeof(BombermanNetworkManager).GetField("coopCatalog", flags).SetValue(manager, catalog);
            typeof(BombermanNetworkManager).GetField("coopDefaultSpriteMaterial", flags).SetValue(manager, first.GetComponent<SpriteRenderer>().sharedMaterial);
            var recipe = new CoopBlackoutMask { template = 0, spriteBounds = new Vector4(-.5f, -.5f, 1, 1),
                region = new Vector4(0, 0, 1, 1), roomBounds = new Vector4(-10, -10, 10, 10),
                colors = new[] { (Vector4)Color.red }, tolerance = .01f, barrel = true, barrelSprite = 0,
                barrelBounds = new Vector4(-.5f, -.5f, 1, 1), barrelUv = new Vector4(0, 0, 1, 1),
                barrelFlip = new Vector4(-1, 1), barrelWorldToLocal = Matrix4x4.Translate(Vector3.left), barrelAlpha = .7f };
            var frame = new CoopSpriteFrame { id = 71, blackout = recipe };
            using (var writer = NetworkWriterPool.Get())
            { writer.Write(frame); frame = new NetworkReader(writer.ToArray()).Read<CoopSpriteFrame>(); }
            Assert.IsTrue(recipe.Equals(frame.blackout)); Assert.IsTrue(frame.blackout.IsValid());
            var apply = typeof(BombermanNetworkManager).GetMethod("ApplyCoopBlackoutMask", flags);
            var a = first.GetComponent<SpriteRenderer>(); var b = second.GetComponent<SpriteRenderer>();
            Assert.IsTrue((bool)apply.Invoke(manager, new object[] { 71, a, frame.blackout }));
            Assert.IsTrue((bool)apply.Invoke(manager, new object[] { 72, b, frame.blackout }));
            Assert.AreNotSame(template, a.sharedMaterial); Assert.AreNotSame(a.sharedMaterial, b.sharedMaterial);
            Assert.AreEqual(recipe.region, a.sharedMaterial.GetVector("_VisibleRegion"));
            Assert.AreSame(texture, a.sharedMaterial.GetTexture("_BarrelTex"));
            Assert.AreEqual(recipe.barrelWorldToLocal, a.sharedMaterial.GetMatrix("_BarrelWorldToLocal"));
            frame.blackout.colors = new Vector4[8]; frame.blackout.colors[7] = Color.magenta;
            Assert.IsTrue((bool)apply.Invoke(manager, new object[] { 71, a, frame.blackout }));
            Assert.AreEqual((Vector4)Color.magenta, a.sharedMaterial.GetVectorArray("_VisibleColors")[7]);
            Assert.AreEqual((Vector4)Color.red, b.sharedMaterial.GetVectorArray("_VisibleColors")[0]);
            Assert.AreEqual(original, template.GetColor("_Color"));
            var invalid = frame.blackout; invalid.barrelSprite = 99;
            Assert.IsFalse((bool)apply.Invoke(manager, new object[] { 71, a, invalid }));
            invalid.barrelSprite = 0; invalid.colors = new Vector4[9]; Assert.IsFalse(invalid.IsValid());
            var ordinary = new CoopSpriteFrame { id = 72 };
            using (var writer = NetworkWriterPool.Get())
            { writer.Write(ordinary); ordinary = new NetworkReader(writer.ToArray()).Read<CoopSpriteFrame>(); }
            Assert.IsNull(ordinary.blackout);
            typeof(BombermanNetworkManager).GetMethod("ReleaseCoopMaskMaterial", flags).Invoke(manager, new object[] { 71, a });
            Assert.AreNotSame(b.sharedMaterial, a.sharedMaterial);
        }
        finally
        {
            if (manager != null) typeof(BombermanNetworkManager).GetMethod("ClearCoopBlackoutMasks", flags).Invoke(manager, null);
            Object.DestroyImmediate(host); Object.DestroyImmediate(first); Object.DestroyImmediate(second);
            Object.DestroyImmediate(catalog); Object.DestroyImmediate(sprite); Object.DestroyImmediate(texture);
            NetSync.IsNetworkedScene = oldNetworkScene;
        }
    }

    [Test]
    public void ApprovedBlackoutCanWakeInactiveOutputFadeDuringPauseAndRejectNativeTimersAndOldStates()
    {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var oldMode = NetSync.Mode; var instance = typeof(StageBlackout).GetProperty("Instance"); var oldInstance = instance.GetValue(null);
        var clientState = typeof(NetworkClient).GetField("connectState", BindingFlags.Static | BindingFlags.NonPublic); var oldClient = clientState.GetValue(null);
        var paused = typeof(GamePauseController).GetField("<IsPaused>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic); var oldPaused = paused.GetValue(null);
        var actor = new GameObject("ApprovedBlackoutUI", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)); actor.SetActive(false);
        var template = new Material(Shader.Find("UI/Default"));
        StageBlackout blackout = null;
        try
        {
            NetSync.Mode = NetSync.NetMode.Client; clientState.SetValue(null, System.Enum.Parse(clientState.FieldType, "Connected")); paused.SetValue(null, true); instance.SetValue(null, null);
            actor.GetComponent<Image>().material = template; blackout = actor.AddComponent<StageBlackout>();
            typeof(StageBlackout).GetField("blackoutImage", flags).SetValue(blackout, actor.GetComponent<Image>());
            var view = new BlackoutView { scene = actor.scene.name, path = blackout.NetworkBlackoutPath, revision = 1, time = 10,
                active = true, alpha = .2f, tileSize = 1, explosions = new[] { new BlackoutSpotlight { id = 5,
                    position = Vector2.one, halfSize = Vector2.one, intensity = .7f } }, roomLights = System.Array.Empty<Vector4>() };
            using (var writer = NetworkWriterPool.Get())
            { writer.Write(view); view = new NetworkReader(writer.ToArray()).Read<BlackoutView>(); }
            blackout.ReceiveNetworkBlackout(view); blackout.PresentNetworkBlackout(9); Assert.IsFalse(actor.activeSelf);
            var next = view; next.revision = 2; next.time = 10.2; next.alpha = .8f;
            blackout.ReceiveNetworkBlackout(next); blackout.PresentNetworkBlackout(10.1);
            Assert.IsTrue(actor.activeSelf); Assert.AreEqual(.5f, (float)typeof(StageBlackout).GetField("_currentA", flags).GetValue(blackout), .0001f);
            var material = (Material)typeof(StageBlackout).GetField("_matInstance", flags).GetValue(blackout);
            typeof(StageBlackout).GetMethod("Awake", flags).Invoke(blackout, null);
            Assert.AreSame(material, typeof(StageBlackout).GetField("_matInstance", flags).GetValue(blackout));
            blackout.SetBlackoutActive(false); blackout.StartOrRenewTimedBlackout(1); blackout.RegisterExplosionSpotlight(99, Vector2.zero, Vector2.one);
            Assert.IsTrue(actor.activeSelf); Assert.IsNull(typeof(StageBlackout).GetField("_timedBlackoutRoutine", flags).GetValue(blackout));
            Assert.AreEqual(1, ((IDictionary)typeof(StageBlackout).GetField("_activeExplosionSpotlights", flags).GetValue(blackout)).Count);
            var clear = next; clear.revision = 3; clear.time = 10.3; clear.active = false; clear.alpha = 0;
            clear.explosions = System.Array.Empty<BlackoutSpotlight>(); blackout.ReceiveNetworkBlackout(clear); blackout.PresentNetworkBlackout(10.4);
            Assert.IsFalse(actor.activeSelf); blackout.ReceiveNetworkBlackout(view); blackout.PresentNetworkBlackout(10.5); Assert.IsFalse(actor.activeSelf);
            var malformed = clear; malformed.revision = 4; malformed.alpha = float.NaN; blackout.ReceiveNetworkBlackout(malformed);
            var history = (List<BlackoutView>)typeof(StageBlackout).GetField("networkBlackoutHistory", flags).GetValue(blackout); Assert.AreEqual(1, history.Count);
            malformed = clear; malformed.revision = 4; malformed.path = "Other"; blackout.ReceiveNetworkBlackout(malformed); Assert.AreEqual(1, history.Count);
            malformed = clear; malformed.revision = 4; malformed.scene = "Other"; blackout.ReceiveNetworkBlackout(malformed); Assert.AreEqual(1, history.Count);
            malformed = clear; malformed.revision = 4; malformed.roomLights = new Vector4[17]; blackout.ReceiveNetworkBlackout(malformed); Assert.AreEqual(1, history.Count);
            for (uint i = 4; i < 80; i++) { clear.revision = i; blackout.ReceiveNetworkBlackout(clear); }
            Assert.AreEqual(64, history.Count);
        }
        finally
        {
            if (blackout != null)
            {
                var material = (Material)typeof(StageBlackout).GetField("_matInstance", flags).GetValue(blackout);
                if (material != null) Object.DestroyImmediate(material);
                typeof(StageBlackout).GetField("_matInstance", flags).SetValue(blackout, null);
            }
            Object.DestroyImmediate(actor); Object.DestroyImmediate(template); instance.SetValue(null, oldInstance);
            NetSync.Mode = oldMode; clientState.SetValue(null, oldClient); paused.SetValue(null, oldPaused);
        }
    }
}

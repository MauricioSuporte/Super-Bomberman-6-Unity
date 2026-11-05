using System.Collections;
using System.Reflection;
using Assets.Scripts.Netcode;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed partial class OnlineRoomEditModeTests
{
    [Test]
    public void PortalWorld3CatalogIncludesRuntimeBigWormFrames()
    {
        CoopSceneCatalogBuilder.Generate();
        var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<CoopSceneCatalog>("Assets/Resources/CoopScenes/Stage_3-4.asset");
        var frames = Resources.LoadAll<Sprite>("StageAssets/BigWorm");
        Assert.AreEqual(13, frames.Length);
        foreach (var frame in frames) CollectionAssert.Contains(catalog.sprites, frame);
    }

    [Test]
    public void PortalStageSandstormCanDisableBeforeStartOrTwice()
    {
        var root = new GameObject("portal-sandstorm-cleanup");
        Sprite sprite = null;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var effect = root.AddComponent<StageAssets.Stage34SandstormEffect>();
        try
        {
            var cleanup = typeof(StageAssets.Stage34SandstormEffect).GetMethod("OnDisable", flags);
            Assert.DoesNotThrow(() => cleanup.Invoke(effect, null));
            Assert.DoesNotThrow(() => cleanup.Invoke(effect, null));
            typeof(StageAssets.Stage34SandstormEffect).GetField("pixelsPerUnit", flags).SetValue(effect, 16);
            sprite = (Sprite)typeof(StageAssets.Stage34SandstormEffect).GetMethod("CreateGrainSprite", flags).Invoke(effect, null);
            Assert.IsTrue(sprite.texture.isReadable, "Co-op must be able to transmit the generated sand grain.");
            Assert.AreEqual(1, sprite.texture.GetPixels32().Length);
        }
        finally
        {
            // This EditMode test owns the generated native assets; runtime cleanup
            // uses deferred Destroy and is covered separately by the rendered smoke.
            var textureField = typeof(StageAssets.Stage34SandstormEffect).GetField("grainTexture", flags);
            var texture = (Texture2D)textureField.GetValue(effect);
            textureField.SetValue(effect, null);
            if (sprite != null) Object.DestroyImmediate(sprite);
            if (texture != null) Object.DestroyImmediate(texture);
            Object.DestroyImmediate(root);
        }
    }

    [UnityTest]
    public IEnumerator PortalCancellationDisposesTravelAndPreservesExistingProtection()
    {
        yield return new EnterPlayMode(); yield return SceneManager.LoadSceneAsync("TitleScreen");
        var oldMode = NetSync.Mode; NetSync.Mode = NetSync.NetMode.Offline;
        var original = SceneManager.GetActiveScene(); var scene = SceneManager.CreateScene("BattleMode_7"); SceneManager.SetActiveScene(scene);
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        try
        {
            foreach (bool sink in new[] { false, true })
            {
                var actor = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab"));
                actor.transform.position = new Vector3(1000, 1000);
                var mover = actor.GetComponent<MovementController>(); mover.enabled = false;
                var health = actor.GetComponent<CharacterHealth>(); health.SetExternalInvulnerability(!sink);
                mover.SetExternalMovementOverride(sink); mover.SetVisualOverrideActive(sink);
                var host = new GameObject("PortalLifetime"); var portal = host.AddComponent<BattleMode7PortalController>();
                try
                {
                    typeof(BattleMode7PortalController).GetField("usePortalSinkVisual", flags).SetValue(portal, sink);
                    typeof(BattleMode7PortalController).GetField("teleportSeconds", flags).SetValue(portal, 10f);
                    typeof(BattleMode7PortalController).GetField("portalEntrySeconds", flags).SetValue(portal, 10f);
                    var method = typeof(BattleMode7PortalController).GetMethod("TeleportRoutine", flags);
                    portal.StartCoroutine((IEnumerator)method.Invoke(portal, new object[] { mover, 0, 1 }));
                    yield return null;
                    Assert.IsTrue(mover.InputLocked);
                    host.SetActive(false);
                    Assert.IsFalse(mover.InputLocked); Assert.AreEqual(sink, mover.ExternalMovementOverride); Assert.AreEqual(sink, mover.VisualOverrideActive);
                    Assert.AreEqual(!sink, health.HasExternalInvulnerability, "Portal cleanup must restore exactly the prior protection.");
                    Assert.IsTrue(actor.GetComponent<Collider2D>().enabled);
                    Vector2 held = mover.Rigidbody.position;
                    yield return null; yield return null;
                    Assert.That(Vector2.Distance(held, mover.Rigidbody.position), Is.LessThan(.001f), "Cancelled native travel cannot continue moving its actor.");
                    host.SetActive(true);
                    portal.StartCoroutine((IEnumerator)method.Invoke(portal, new object[] { mover, 0, 1 }));
                    Assert.IsTrue(mover.InputLocked, "Cancelled portal remains reusable."); host.SetActive(false);
                    host.SetActive(true);
                    var glove = actor.GetComponent<PowerGloveAbility>();
                    if (glove == null) glove = actor.AddComponent<PowerGloveAbility>();
                    glove.enabled = false;
                    portal.StartCoroutine((IEnumerator)method.Invoke(portal, new object[] { mover, 0, 1 }));
                    mover.isDead = true; host.SetActive(false);
                    Assert.IsTrue(mover.InputLocked); Assert.IsFalse(actor.GetComponent<Collider2D>().enabled);
                    Assert.IsFalse(actor.GetComponent<BombController>().enabled);
                    Assert.IsFalse((bool)typeof(PowerGloveAbility).GetField("externalTeleportVisualSuppressed", flags).GetValue(glove));
                    mover.isDead = false;
                    if (!sink)
                    {
                        host.SetActive(true);
                        var bombObject = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Bombs/Bomb.prefab"));
                        try
                        {
                            var bomb = bombObject.GetComponent<Bomb>(); var collider = bombObject.GetComponent<Collider2D>();
                            var renderer = bombObject.GetComponent<SpriteRenderer>(); var notifier = bombObject.GetComponent<BombAtGroundTileNotifier>();
                            bool colliderBefore = collider.enabled, rendererBefore = renderer.enabled, notifierBefore = notifier != null && notifier.enabled;
                            var bombMethod = typeof(BattleMode7PortalController).GetMethod("TeleportAndPunchBombRoutine", flags);
                            portal.StartCoroutine((IEnumerator)bombMethod.Invoke(portal, new object[] { bomb, 0 }));
                            Assert.IsFalse(collider.enabled); Assert.IsFalse(renderer.enabled);
                            host.SetActive(false);
                            Assert.AreEqual(colliderBefore, collider.enabled); Assert.AreEqual(rendererBefore, renderer.enabled);
                            if (notifier != null) Assert.AreEqual(notifierBefore, notifier.enabled);
                            Assert.IsFalse(bomb.HasExploded, "Cancelling presentation cannot detonate the bomb.");
                        }
                        finally { Object.DestroyImmediate(bombObject); }
                    }
                }
                finally { Object.DestroyImmediate(host); Object.DestroyImmediate(actor); }
            }
        }
        finally { NetSync.Mode = oldMode; SceneManager.SetActiveScene(original); }
        yield return SceneManager.UnloadSceneAsync(scene); yield return new ExitPlayMode();
    }
}

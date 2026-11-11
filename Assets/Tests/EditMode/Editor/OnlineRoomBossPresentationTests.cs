using System.Reflection;
using Assets.Scripts.Netcode;
using NUnit.Framework;
using Mirror;
using UnityEngine;

public sealed partial class OnlineRoomEditModeTests
{
    static object R06Invoke(string name, params object[] args) => typeof(BombermanNetworkManager)
        .GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);

    [Test]
    public void R06FloorMaskPreservesBottomPivotAndScaledGeometry()
    {
        var go = new GameObject("Pixel Portal Floor Mask");
        var sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(.5f, 0), 1);
        try
        {
            var mask = go.AddComponent<SpriteMask>(); mask.sprite = sprite;
            mask.isCustomRangeActive = true; mask.frontSortingOrder = 10005; mask.backSortingOrder = 10004;
            go.transform.position = new Vector3(2, 3, 0); go.transform.localScale = new Vector3(4, 6, 1);
            var frame = (PortalMaskView)R06Invoke("CapturePortalMask", mask);
            Assert.IsTrue(frame.custom); Assert.AreEqual(10004, frame.backOrder); Assert.AreEqual(10005, frame.frontOrder);
            Assert.AreEqual(new Vector3(2, 6, 0), frame.position);
            Assert.AreEqual(4, frame.scale.x); Assert.AreEqual(6, frame.scale.y); Assert.AreEqual(1, frame.scale.z);
            Assert.AreEqual(3, frame.position.y - frame.scale.y * .5f);
            go.transform.rotation = Quaternion.Euler(0, 0, 90);
            frame = (PortalMaskView)R06Invoke("CapturePortalMask", mask);
            Assert.Less(Vector3.Distance(go.transform.TransformPoint(sprite.bounds.center), frame.position), .0001f);
            Assert.IsTrue((bool)R06Invoke("IsApprovedPortalMask", go.name));
            Assert.IsTrue((bool)R06Invoke("IsApprovedPortalMask", "Stage34PortalFixedMask"));
            Assert.IsFalse((bool)R06Invoke("IsApprovedPortalMask", "UnrelatedMask"));
        }
        finally { Object.DestroyImmediate(go); Object.DestroyImmediate(sprite); }
    }

    [Test]
    public void R06PortalRangeMovesBothWaysWithoutClampingToOldBounds()
    {
        var go = new GameObject("Range");
        try
        {
            var mask = go.AddComponent<SpriteMask>();
            foreach (var frame in new[] {
                new PortalMaskView { custom = true, backOrder = 10004, frontOrder = 10006 },
                new PortalMaskView { custom = true, backOrder = -10, frontOrder = -5 },
                new PortalMaskView { custom = false, backOrder = 0, frontOrder = 5 } })
            {
                R06Invoke("ConfigurePortalMaskRange", mask, frame);
                Assert.AreEqual(frame.backOrder, mask.backSortingOrder);
                Assert.AreEqual(frame.frontOrder, mask.frontSortingOrder);
                Assert.AreEqual(frame.custom, mask.isCustomRangeActive);
            }
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void R06BossPlaybackHoldsApprovedFrameWithoutChangingNativeAnimation()
    {
        var source = new GameObject("NativeBossPose", typeof(SpriteRenderer));
        var view = new GameObject("BossView", typeof(SpriteRenderer));
        var sprites = new Sprite[3];
        try
        {
            for (int i = 0; i < sprites.Length; i++)
                sprites[i] = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * .5f, 1);
            var native = source.AddComponent<AnimatedSpriteRenderer>();
            native.animationSprite = sprites; native.idle = false; native.loop = true; native.animationTime = .1f;
            native.CurrentFrame = 1;
            var before = native.CapturePlayback();
            var approved = (AnimatedSpriteRenderer.PlaybackState)R06Invoke("CaptureCoopBossPlayback", native);
            var remote = view.AddComponent<AnimatedSpriteRenderer>();
            remote.animationSprite = sprites; remote.animationTime = .1f; remote.loop = true;
            remote.SetManualAnimationUpdate(true); remote.PresentPlayback(approved, .8f);
            Assert.AreEqual(0, approved.speed); Assert.AreEqual(1, remote.CurrentFrame);
            Assert.AreSame(sprites[1], view.GetComponent<SpriteRenderer>().sprite);
            Assert.AreEqual(before.speed, native.CapturePlayback().speed);
            Assert.IsFalse(native.ManualAnimationUpdate);
        }
        finally
        {
            Object.DestroyImmediate(source); Object.DestroyImmediate(view);
            foreach (var sprite in sprites) if (sprite != null) Object.DestroyImmediate(sprite);
        }
    }

    [Test]
    public void R06AbsorptionProducerDataSurvivesMissingServerShader()
    {
        var host = new GameObject("ShaderlessServer"); host.SetActive(false);
        var actor = new GameObject("Absorbed", typeof(SpriteRenderer));
        var singleton = typeof(NetworkManager).GetProperty("singleton"); var old = singleton.GetValue(null);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        try
        {
            var manager = host.AddComponent<BombermanNetworkManager>(); singleton.SetValue(null, manager);
            var renderer = actor.GetComponent<SpriteRenderer>(); renderer.sharedMaterial = null;
            var sources = (System.Collections.Generic.Dictionary<int, float>)typeof(BombermanNetworkManager)
                .GetField("coopAbsorptionSources", flags).GetValue(manager);
            sources[renderer.GetEntityId().GetHashCode()] = .6f;
            var light = (CoopAbsorptionLight)R06Invoke("CaptureCoopAbsorption", renderer);
            Assert.AreEqual(.6f, light.amount);
            typeof(BombermanNetworkManager).GetMethod("ClearCoopAbsorption", flags).Invoke(manager, null);
            Assert.IsNull(R06Invoke("CaptureCoopAbsorption", renderer)); Assert.AreEqual(0, sources.Count);
        }
        finally { singleton.SetValue(null, old); Object.DestroyImmediate(host); Object.DestroyImmediate(actor); }
    }

    [Test]
    public void R06ExplicitHeldSpriteWinsOverEmptyIdleAnimationRecipe()
    {
        var host = new GameObject("HeldSpriteManager"); host.SetActive(false);
        var root = new GameObject("Views");
        var catalog = ScriptableObject.CreateInstance<CoopSceneCatalog>();
        var sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * .5f, 1);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        try
        {
            var manager = host.AddComponent<BombermanNetworkManager>();
            catalog.sprites = new[] { sprite }; catalog.materials = System.Array.Empty<Material>();
            typeof(BombermanNetworkManager).GetField("coopCatalog", flags).SetValue(manager, catalog);
            typeof(BombermanNetworkManager).GetField("coopViewRoot", flags).SetValue(manager, root);
            var definitions = (System.Collections.Generic.Dictionary<int, CoopAnimationDefinition>)typeof(BombermanNetworkManager)
                .GetField("coopAnimationDefinitions", flags).GetValue(manager);
            definitions[1] = new CoopAnimationDefinition { id = 1, idleSprite = -1, sprites = new[] { 0 }, animationTime = .1f };
            var frame = new CoopSpriteFrame { id = 18, sprite = 0, material = -1, animation = 1, visible = true,
                scale = Vector3.one, rotation = Quaternion.identity, color = Color.white,
                playback = new AnimatedSpriteRenderer.PlaybackState { idle = true, speed = 0 } };
            typeof(BombermanNetworkManager).GetMethod("PresentCoopSprite", flags).Invoke(manager, new object[] { frame, 0d });
            var views = (System.Collections.Generic.Dictionary<int, SpriteRenderer>)typeof(BombermanNetworkManager)
                .GetField("coopViews", flags).GetValue(manager);
            Assert.IsTrue(views[18].enabled); Assert.AreSame(sprite, views[18].sprite);
        }
        finally { Object.DestroyImmediate(host); Object.DestroyImmediate(root); Object.DestroyImmediate(catalog); Object.DestroyImmediate(sprite); }
    }

    [Test]
    public void R06AbsorptionCarriesOnlyValidatedAuthoritativeLight()
    {
        var go = new GameObject("Absorption", typeof(SpriteRenderer));
        var material = new Material(Resources.Load<Shader>("StageAssets/FreezerVenusAbsorption"));
        try
        {
            var renderer = go.GetComponent<SpriteRenderer>(); renderer.sharedMaterial = material;
            material.SetFloat("_LightAmount", .625f);
            var light = (CoopAbsorptionLight)R06Invoke("CaptureCoopAbsorption", renderer);
            Assert.AreEqual(.625f, light.amount); Assert.IsTrue(light.IsValid());
            Assert.IsTrue(light.Equals(new CoopAbsorptionLight { amount = .625f }));
            Assert.IsFalse(light.Equals(new CoopAbsorptionLight { amount = .5f }));
            foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, -.1f, 1.1f })
                Assert.IsFalse(new CoopAbsorptionLight { amount = invalid }.IsValid());
            var frame = new CoopSpriteFrame { absorption = light, rotation = Quaternion.identity };
            using (var writer = NetworkWriterPool.Get())
            { writer.Write(frame); frame = new NetworkReader(writer.ToArray()).Read<CoopSpriteFrame>(); }
            Assert.AreEqual(.625f, frame.absorption.amount);
            Assert.IsTrue(frame.Matches(frame));
            var changed = frame; changed.absorption = new CoopAbsorptionLight { amount = .5f };
            Assert.IsFalse(frame.Matches(changed));
            renderer.sharedMaterial = null;
            Assert.IsNull(R06Invoke("CaptureCoopAbsorption", renderer));
        }
        finally { Object.DestroyImmediate(go); Object.DestroyImmediate(material); }
    }

    [Test]
    public void R06AbsorptionViewsHaveIndependentLightAndReleaseTheirProperties()
    {
        var host = new GameObject("AbsorptionManager"); host.SetActive(false);
        var first = new GameObject("First", typeof(SpriteRenderer));
        var second = new GameObject("Second", typeof(SpriteRenderer));
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        try
        {
            var manager = host.AddComponent<BombermanNetworkManager>();
            var apply = typeof(BombermanNetworkManager).GetMethod("ApplyCoopAbsorption", flags);
            var a = first.GetComponent<SpriteRenderer>(); var b = second.GetComponent<SpriteRenderer>();
            Assert.IsTrue((bool)apply.Invoke(manager, new object[] { a, new CoopAbsorptionLight { amount = .2f } }));
            Assert.IsTrue((bool)apply.Invoke(manager, new object[] { b, new CoopAbsorptionLight { amount = .8f } }));
            var properties = new MaterialPropertyBlock(); a.GetPropertyBlock(properties);
            Assert.AreEqual(.2f, properties.GetFloat("_LightAmount"));
            b.GetPropertyBlock(properties); Assert.AreEqual(.8f, properties.GetFloat("_LightAmount"));
            Assert.AreSame(a.sharedMaterial, b.sharedMaterial);
            typeof(BombermanNetworkManager).GetMethod("ClearCoopAbsorption", flags).Invoke(manager, null);
            a.GetPropertyBlock(properties); Assert.IsTrue(properties.isEmpty);
            b.GetPropertyBlock(properties); Assert.IsTrue(properties.isEmpty);
        }
        finally { Object.DestroyImmediate(host); Object.DestroyImmediate(first); Object.DestroyImmediate(second); }
    }

    [Test]
    public void R06BossMaskEdgeCannotWaitForPeriodicWorldSample()
    {
        var go = new GameObject("Reveal", typeof(SpriteRenderer));
        try
        {
            var renderer = go.GetComponent<SpriteRenderer>(); renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            var previous = new CoopSpriteFrame { visible = true, mask = (byte)SpriteMaskInteraction.None };
            Assert.IsTrue((bool)R06Invoke("NeedsCoopEnemyTransition", renderer, previous));
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void R06MechaDeferredStartDoesNotClaimSequenceBeforeSceneInitialization()
    {
        var go = new GameObject("DeferredMecha"); go.SetActive(false);
        try
        {
            var sequence = go.AddComponent<MechaBossSequence>();
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;
            var start = (System.Collections.IEnumerator)typeof(MechaBossSequence)
                .GetMethod("StartSequenceAfterSceneInitialization", flags).Invoke(sequence, null);
            Assert.IsTrue(start.MoveNext()); Assert.IsNull(start.Current);
            Assert.IsFalse((bool)typeof(MechaBossSequence).GetField("sequenceStarted", flags).GetValue(sequence));
            // Cancelling this deferred coroutine must leave OnEnable free to start the sequence.
            (start as System.IDisposable)?.Dispose();
            Assert.IsFalse((bool)typeof(MechaBossSequence).GetField("sequenceStarted", flags).GetValue(sequence));
        }
        finally { Object.DestroyImmediate(go); }
    }
}

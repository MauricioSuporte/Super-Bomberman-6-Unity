using System;
using System.Reflection;
using Assets.Scripts.Netcode;
using Mirror;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed partial class OnlineRoomEditModeTests
{
    [TestCase(typeof(StageAssets.Stage32VulcanFlameLeap), "StartBurst", 1f)]
    [TestCase(typeof(StageAssets.Stage33DestructibleBubbleBurst), "SpawnBurst", null)]
    [TestCase(typeof(StageAssets.Stage33AmbientBubbleEffect), "Update", null)]
    [TestCase(typeof(StageAssets.Stage32VolcanoEyesAnimator), "Update", null)]
    [TestCase(typeof(StageAssets.World3GateOpenedSequenceController), "HandleAllCoreMechanismsDestroyed", null)]
    public void R08PureClientEnvironmentEntryDoesNotDrawRandomOrCreateEffects(Type type, string method, object argument)
    {
        var mode = NetSync.Mode; var random = UnityEngine.Random.state;
        var go = new GameObject("R08Guard"); go.SetActive(false);
        try
        {
            NetSync.Mode = NetSync.NetMode.Client;
            var component = go.AddComponent(type);
            var entry = type.GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
            var parameters = entry.GetParameters();
            object[] args = parameters.Length == 0 ? null : new[] { argument ?? (object)Vector3Int.zero };
            int count = go.transform.childCount;
            entry.Invoke(component, args);
            Assert.AreEqual(random, UnityEngine.Random.state);
            Assert.AreEqual(count, go.transform.childCount);
        }
        finally { NetSync.Mode = mode; Object.DestroyImmediate(go); UnityEngine.Random.state = random; }
    }

    [Test]
    public void R08ManualEnvironmentSpriteAndVisibilityEdgesAreNotDelayedUntilWorldHeartbeat()
    {
        var go = new GameObject("OceanBubble_01", typeof(SpriteRenderer));
        var sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * .5f, 1);
        try
        {
            var renderer = go.GetComponent<SpriteRenderer>(); renderer.sprite = sprite;
            var frame = new CoopSpriteFrame { visible = true, color = Color.white, sortingLayer = renderer.sortingLayerID, order = 0 };
            var method = typeof(BombermanNetworkManager).GetMethod("NeedsCoopEnemyTransition", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsFalse((bool)method.Invoke(null, new object[] { renderer, frame }));
            renderer.flipY = true;
            Assert.IsTrue((bool)method.Invoke(null, new object[] { renderer, frame }), "A volcano can flip at the apex without changing its sprite.");
            renderer.flipY = false;
            renderer.color = new Color(1, 1, 1, .25f);
            Assert.IsTrue((bool)method.Invoke(null, new object[] { renderer, frame }), "Native fade edge must be approved immediately.");
            renderer.enabled = false;
            Assert.IsTrue((bool)method.Invoke(null, new object[] { renderer, frame }));
        }
        finally { Object.DestroyImmediate(go); Object.DestroyImmediate(sprite); }
    }

    [Test]
    public void R08LoopRecipeRoundtripPreservesPauseTailAndPhaseAndRejectsNonfiniteData()
    {
        var frame = new CoopEnvironmentLoopFrame { id = 19, clip = 4, phase = 1.25f, gain = 2.5f, paused = true, looping = false };
        var message = new CoopEnvironmentLoops { scene = "Stage_3-5", time = 22, revision = 9, frames = new[] { frame } };
        using var writer = NetworkWriterPool.Get(); writer.Write(message);
        using var reader = NetworkReaderPool.Get(writer.ToArraySegment()); var decoded = reader.Read<CoopEnvironmentLoops>();
        Assert.AreEqual(message.scene, decoded.scene); Assert.AreEqual(message.revision, decoded.revision);
        Assert.AreEqual(frame.phase, decoded.frames[0].phase); Assert.IsTrue(decoded.frames[0].paused); Assert.IsFalse(decoded.frames[0].looping);
        var valid = typeof(BombermanNetworkManager).GetMethod("ValidEnvironmentLoop", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsTrue((bool)valid.Invoke(null, new object[] { frame }));
        frame.phase = float.NaN; Assert.IsFalse((bool)valid.Invoke(null, new object[] { frame }));
        frame.phase = 1; frame.gain = float.PositiveInfinity; Assert.IsFalse((bool)valid.Invoke(null, new object[] { frame }));
        frame.gain = 2; frame.clip = -1; Assert.IsFalse((bool)valid.Invoke(null, new object[] { frame }));
    }
    [Test]
    public void R08LoopCpuClockFreezesDuringPauseAndFinishesOnlyTheCurrentTailIteration()
    {
        var method = typeof(BombermanNetworkManager).GetMethod("AdvanceEnvironmentLoop", BindingFlags.Static | BindingFlags.NonPublic);
        object[] args = { .8f, .3f, 1f, true, false, false };
        Assert.AreEqual(.1f, (float)method.Invoke(null, args), .0001f); Assert.IsFalse((bool)args[5]);
        args = new object[] { .8f, 5f, 1f, false, true, false };
        Assert.AreEqual(.8f, method.Invoke(null, args)); Assert.IsFalse((bool)args[5]);
        args = new object[] { .8f, .3f, 1f, false, false, false };
        Assert.AreEqual(1.1f, (float)method.Invoke(null, args), .0001f); Assert.IsTrue((bool)args[5]);
    }

    [Test]
    public void R08SceneCleanupReleasesLoopAudioAndGeneratedClip()
    {
        var go = new GameObject("R08Manager"); go.SetActive(false);
        var audio = new GameObject("R08ApprovedLoop", typeof(AudioSource));
        var clip = AudioClip.Create("R08OwnedPCM", 100, 1, 1000, false);
        try
        {
            var manager = go.AddComponent<BombermanNetworkManager>();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var type = typeof(BombermanNetworkManager).GetNestedType("EnvironmentLoopView", BindingFlags.NonPublic);
            var view = Activator.CreateInstance(type, true);
            type.GetField("source").SetValue(view, audio.GetComponent<AudioSource>());
            type.GetField("ownedClip").SetValue(view, clip);
            var views = (System.Collections.IDictionary)typeof(BombermanNetworkManager).GetField("coopLoopViews", flags).GetValue(manager);
            views.Add(1, view);
            typeof(BombermanNetworkManager).GetMethod("ClearCoopEnvironment", flags).Invoke(manager, null);
            Assert.AreEqual(0, views.Count); Assert.IsTrue(audio == null); Assert.IsTrue(clip == null);
        }
        finally
        {
            if (audio != null) Object.DestroyImmediate(audio);
            if (clip != null) Object.DestroyImmediate(clip);
            Object.DestroyImmediate(go);
        }
    }

}

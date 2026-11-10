using System.Collections.Generic;
using System.Reflection;
using Assets.Scripts.Netcode;
using Mirror;
using NUnit.Framework;
using UnityEngine;

public sealed partial class OnlineRoomEditModeTests
{
    [Test]
    public void R03WaterAndRegularMaterialTransitionsRestorePropertiesAndClearViewCaches()
    {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var type = typeof(BombermanNetworkManager);
        var root = new GameObject("R03MaterialStateManager"); root.SetActive(false);
        var viewsRoot = new GameObject("R03MaterialViews");
        var catalog = ScriptableObject.CreateInstance<CoopSceneCatalog>();
        catalog.sprites = System.Array.Empty<Sprite>();
        catalog.materials = System.Array.Empty<Material>();
        Material regular = null;
        try
        {
            var manager = root.AddComponent<BombermanNetworkManager>();
            type.GetField("coopViewRoot", flags).SetValue(manager, viewsRoot);
            type.GetField("coopCatalog", flags).SetValue(manager, catalog);
            var present = type.GetMethod("PresentCoopSprite", flags);
            var frame = new CoopSpriteFrame { id = 31, sprite = -1, material = -1, visible = true,
                rotation = Quaternion.identity, scale = Vector3.one, color = Color.white };
            present.Invoke(manager, new object[] { frame, 0d });
            var view = ((Dictionary<int, SpriteRenderer>)type.GetField("coopViews", flags).GetValue(manager))[frame.id];
            var defaultMaterial = view.sharedMaterial;
            regular = new Material(defaultMaterial); catalog.materials = new[] { regular };
            frame.water = new CoopWaterSurface { parameters = new Vector4(12.25f, .15f, 1, 0) };
            present.Invoke(manager, new object[] { frame, 0d });
            present.Invoke(manager, new object[] { frame, 0d });
            var properties = new MaterialPropertyBlock(); view.GetPropertyBlock(properties);
            Assert.AreEqual(12.25f, properties.GetFloat("_WaterSurfaceY"));
            frame.water = new CoopWaterSurface { parameters = new Vector4(15.5f, .15f, 0, 1) };
            present.Invoke(manager, new object[] { frame, 0d }); view.GetPropertyBlock(properties);
            Assert.AreEqual(15.5f, properties.GetFloat("_WaterSurfaceY"));
            frame.water = null;
            present.Invoke(manager, new object[] { frame, 0d });
            Assert.IsFalse(view.HasPropertyBlock()); Assert.AreSame(defaultMaterial, view.sharedMaterial);
            frame.material = 0; present.Invoke(manager, new object[] { frame, 0d });
            Assert.AreSame(regular, view.sharedMaterial);
            frame.material = -1; present.Invoke(manager, new object[] { frame, 0d });
            Assert.AreSame(defaultMaterial, view.sharedMaterial, "Unknown/default must not retain the previous catalog material.");
            frame.water = new CoopWaterSurface { parameters = new Vector4(12.25f, .15f, 1, 0) };
            present.Invoke(manager, new object[] { frame, 0d }); view.GetPropertyBlock(properties);
            Assert.AreEqual(12.25f, properties.GetFloat("_WaterSurfaceY"));
            type.GetMethod("ClearCoopBlackoutMasks", flags).Invoke(manager, null);
            Assert.AreEqual(0, ((Dictionary<SpriteRenderer, Vector4>)type.GetField("coopWaterViewStates", flags).GetValue(manager)).Count);
            Assert.AreEqual(0, ((Dictionary<int, int>)type.GetField("coopViewMaterialIds", flags).GetValue(manager)).Count);
        }
        finally
        {
            Object.DestroyImmediate(viewsRoot); Object.DestroyImmediate(root); Object.DestroyImmediate(catalog);
            if (regular != null) Object.DestroyImmediate(regular);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void R03SpriteEdgesKeepManualAndAutomaticFramesAtNativeBoundaries(bool manual)
    {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var type = typeof(BombermanNetworkManager);
        var root = new GameObject("R03ClipEdgeManager"); root.SetActive(false);
        var actor = new GameObject("R03ClipEdge", typeof(SpriteRenderer));
        var texture = new Texture2D(4, 4);
        var first = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.zero);
        var second = Sprite.Create(texture, new Rect(2, 0, 2, 2), Vector2.zero);
        try
        {
            var manager = root.AddComponent<BombermanNetworkManager>();
            var renderer = actor.GetComponent<SpriteRenderer>();
            var animation = actor.AddComponent<AnimatedSpriteRenderer>();
            animation.animationSprite = new[] { first, second }; animation.idle = false;
            animation.loop = true; animation.SetManualAnimationUpdate(manual); animation.CurrentFrame = 1;
            renderer.sprite = second;
            int id = renderer.GetEntityId().GetHashCode();
            type.GetField("coopRenderers", flags).SetValue(manager, new[] { renderer });
            var sprites = (Dictionary<Sprite, int>)type.GetField("coopSpriteIds", flags).GetValue(manager);
            sprites[first] = 1; sprites[second] = 2;
            var frames = (Dictionary<int, CoopSpriteFrame>)type.GetField("coopLastSprites", flags).GetValue(manager);
            var limits = (Dictionary<int, float>)type.GetField("coopEnemyVisualLimits", flags).GetValue(manager);
            frames[id] = new CoopSpriteFrame { id = id, sprite = 1, visible = true, enemyStepLimit = 24,
                time = NetworkTime.localTime - .1, position = actor.transform.position, rotation = Quaternion.identity,
                scale = Vector3.one, playback = R03Playback(animation) };
            limits[id] = 24;
            type.GetMethod("PublishCoopEnemyTransitions", flags).Invoke(manager, new object[] { false });
            var published = (List<CoopSpriteFrame>)type.GetField("coopEnemyTransitions", flags).GetValue(manager);
            Assert.AreEqual(1, published.Count, "Automatic clips also need approved edges at quantized native frame boundaries.");
            Assert.AreEqual(2, published[0].sprite);
            Assert.AreEqual(manual ? 0 : animation.CapturePlayback().speed, published[0].playback.speed);
        }
        finally
        {
            Object.DestroyImmediate(actor); Object.DestroyImmediate(root);
            Object.DestroyImmediate(first); Object.DestroyImmediate(second); Object.DestroyImmediate(texture);
        }
    }

    [Test]
    public void R03DiscreteEnemyPosePreservesTheRestVertexBeforeMovement()
    {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var type = typeof(BombermanNetworkManager);
        var legacy = type.GetField("coopLegacyState", BindingFlags.Static | BindingFlags.NonPublic);
        object oldLegacy = legacy.GetValue(null);
        var root = new GameObject("R03RestEdgeManager"); root.SetActive(false);
        var actor = new GameObject("R03RestEdge", typeof(SpriteRenderer));
        try
        {
            legacy.SetValue(null, 2);
            var manager = root.AddComponent<BombermanNetworkManager>();
            var renderer = actor.GetComponent<SpriteRenderer>();
            int id = renderer.GetEntityId().GetHashCode();
            double now = NetworkTime.localTime;
            type.GetField("coopRenderers", flags).SetValue(manager, new[] { renderer });
            var frames = (Dictionary<int, CoopSpriteFrame>)type.GetField("coopLastSprites", flags).GetValue(manager);
            var sent = (Dictionary<int, double>)type.GetField("coopLastSentTime", flags).GetValue(manager);
            var poses = (Dictionary<int, Vector3>)type.GetField("coopPreviousPose", flags).GetValue(manager);
            var limits = (Dictionary<int, float>)type.GetField("coopEnemyVisualLimits", flags).GetValue(manager);
            frames[id] = new CoopSpriteFrame { id = id, sprite = 99, visible = true, enemyStepLimit = 24,
                time = now - .0167, position = Vector3.zero, rotation = Quaternion.identity, scale = Vector3.one };
            sent[id] = now - .2; poses[id] = Vector3.zero; limits[id] = 24;
            type.GetField("coopPreviousFrameTime", flags).SetValue(manager, now - .0167);
            actor.transform.position = Vector3.up;
            type.GetMethod("PublishCoopEnemyTransitions", flags).Invoke(manager, new object[] { false });
            var published = (List<CoopSpriteFrame>)type.GetField("coopEnemyTransitions", flags).GetValue(manager);
            Assert.AreEqual(2, published.Count, "The pose edge must preserve the approved rest before its new position.");
            Assert.AreEqual(now - .0167, published[0].time, 1e-9);
            Assert.AreEqual(Vector3.zero, published[0].position);
            Assert.AreEqual(Vector3.up, published[1].position);
            Assert.Greater(published[1].time, published[0].time);
        }
        finally { Object.DestroyImmediate(actor); Object.DestroyImmediate(root); legacy.SetValue(null, oldLegacy); }
    }

    [Test]
    public void R03ApprovedWaterlineRoundTripsWithoutClientTerrainQueries()
    {
        var managerObject = new GameObject("R03WaterManager"); managerObject.SetActive(false);
        var actor = new GameObject("R03WaterSource");
        var observer = new GameObject("R03WaterObserver");
        Material runtime = null;
        try
        {
            var manager = managerObject.AddComponent<BombermanNetworkManager>();
            var source = actor.AddComponent<SpriteRenderer>();
            var view = observer.AddComponent<SpriteRenderer>();
            runtime = new Material(Resources.Load<Material>("Materials/PlayerWaterSubmersion"));
            source.sharedMaterial = runtime;
            var properties = new MaterialPropertyBlock();
            properties.SetFloat("_WaterSurfaceY", 12.25f); properties.SetFloat("_SurfaceLineHeight", .15f);
            properties.SetFloat("_UseSurfaceLine", 1); properties.SetFloat("_PremultiplyUnderwaterOpacity", 1);
            source.SetPropertyBlock(properties);
            var state = (CoopWaterSurface)typeof(BombermanNetworkManager).GetMethod("CaptureCoopWater", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(manager, new object[] { source });
            var writer = new NetworkWriter(); writer.Write(new CoopSpriteFrame { water = state });
            var received = new NetworkReader(writer.ToArray()).Read<CoopSpriteFrame>().water;
            Assert.IsTrue(state.Equals(received));
            Assert.IsTrue((bool)typeof(BombermanNetworkManager).GetMethod("ApplyCoopWater", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(manager, new object[] { view, received }));
            view.GetPropertyBlock(properties);
            Assert.AreEqual(12.25f, properties.GetFloat("_WaterSurfaceY"));
            Assert.AreEqual(.15f, properties.GetFloat("_SurfaceLineHeight"));
            Assert.AreEqual(1, properties.GetFloat("_UseSurfaceLine"));
            Assert.AreEqual(1, properties.GetFloat("_PremultiplyUnderwaterOpacity"));
            Assert.IsFalse(new CoopWaterSurface { parameters = new Vector4(float.NaN, .15f, 1, 0) }.IsValid());
            Assert.IsFalse(new CoopWaterSurface { parameters = new Vector4(0, 2, 1, 0) }.IsValid());
            Assert.IsFalse(new CoopWaterSurface { parameters = new Vector4(0, .15f, 2, 0) }.IsValid());
            Assert.IsFalse(new CoopSpriteFrame { water = state }.Matches(new CoopSpriteFrame()));
        }
        finally
        {
            Object.DestroyImmediate(observer); Object.DestroyImmediate(actor); Object.DestroyImmediate(managerObject);
            if (runtime != null) Object.DestroyImmediate(runtime);
        }
    }

    static AnimatedSpriteRenderer.PlaybackState R03Playback(AnimatedSpriteRenderer source) =>
        (AnimatedSpriteRenderer.PlaybackState)typeof(BombermanNetworkManager).GetMethod("CaptureCoopPlayback", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { source });
    static bool R03Transition(SpriteRenderer renderer, CoopSpriteFrame previous) =>
        (bool)typeof(BombermanNetworkManager).GetMethod("NeedsCoopEnemyTransition", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { renderer, previous });
    [TestCase(true)]
    [TestCase(false)]
    public void R03ManualEnemyFrameStaysApprovedWhileAutomaticAnimationAdvances(bool manual)
    {
        var actor = new GameObject("R03NativeManualPose"); actor.SetActive(false);
        var texture = new Texture2D(4, 4);
        var first = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.zero);
        var second = Sprite.Create(texture, new Rect(2, 0, 2, 2), Vector2.zero);
        try
        {
            actor.AddComponent<SpriteRenderer>();
            var animation = actor.AddComponent<AnimatedSpriteRenderer>();
            animation.animationSprite = new[] { first, second };
            animation.animationTime = .1f;
            animation.loop = true; animation.idle = false;
            animation.CurrentFrame = 0;
            animation.SetManualAnimationUpdate(manual);
            var state = R03Playback(animation);
            Assert.AreEqual(manual ? 0f : Time.timeScale, state.speed);
            animation.PresentPlayback(state, .15f, false);
            Assert.AreEqual(manual ? 0 : 1, animation.CurrentFrame,
                "An observer must not invent the Frog/SnowMan controller's next manually selected frame.");
        }
        finally { Object.DestroyImmediate(actor); Object.DestroyImmediate(first); Object.DestroyImmediate(second); Object.DestroyImmediate(texture); }
    }

    [Test]
    public void R03EnemyVisibilityAndManualFrameEdgesAreApprovedDiscreteChanges()
    {
        var actor = new GameObject("R03ApprovedEdge"); actor.SetActive(false);
        try
        {
            var renderer = actor.AddComponent<SpriteRenderer>();
            var animation = actor.AddComponent<AnimatedSpriteRenderer>();
            var frame = new CoopSpriteFrame { visible = false, playback = animation.CapturePlayback() };
            Assert.IsFalse(R03Transition(renderer, frame));
            // Renderer visibility is authoritative even when a native animator is disabled.
            frame.visible = true;
            Assert.IsTrue(R03Transition(renderer, frame));
            frame.visible = false; renderer.enabled = false;
            Assert.IsFalse(R03Transition(renderer, frame));
        }
        finally { Object.DestroyImmediate(actor); }
    }

    [Test]
    public void R03SnowSceneProceduralFlakeCanBePublishedWithoutMissingCatalogAsset()
    {
        var actor = new GameObject("R03SnowScene"); actor.SetActive(false);
        Sprite sprite = null;
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var effect = actor.AddComponent<StageAssets.Stage35SnowfallEffect>();
        typeof(StageAssets.Stage35SnowfallEffect).GetField("pixelsPerUnit", flags).SetValue(effect, 16);
        try
        {
            sprite = (Sprite)typeof(StageAssets.Stage35SnowfallEffect).GetMethod("CreateFlakeSprite", flags).Invoke(effect, null);
            Assert.IsTrue(sprite.texture.isReadable, "CoopSpriteId must be able to publish the generated flake's pixels.");
            Assert.Greater(sprite.texture.GetPixels32().Length, 0);
        }
        finally
        {
            if (sprite != null) { Object.DestroyImmediate(sprite.texture); Object.DestroyImmediate(sprite); }
            typeof(StageAssets.Stage35SnowfallEffect).GetField("flakeTexture", flags).SetValue(effect, null);
            Object.DestroyImmediate(actor);
        }
    }

    [Test]
    public void R03LeavingAManualPosePublishesTheReturnToAutomaticPlayback()
    {
        float oldScale = Time.timeScale;
        var actor = new GameObject("R03ManualHandoff"); actor.SetActive(false);
        try
        {
            Time.timeScale = 1;
            var renderer = actor.AddComponent<SpriteRenderer>();
            var animation = actor.AddComponent<AnimatedSpriteRenderer>();
            actor.SetActive(true);
            animation.SetManualAnimationUpdate(true);
            var previous = new CoopSpriteFrame { visible = true, playback = R03Playback(animation) };
            animation.SetManualAnimationUpdate(false);
            Assert.IsTrue(R03Transition(renderer, previous),
                "A landing must not keep the previous frozen playback until the periodic full capture.");
        }
        finally { Object.DestroyImmediate(actor); Time.timeScale = oldScale; }
    }

    [TestCase(false, false, false)]
    [TestCase(true, false, true)]
    [TestCase(true, true, false)]
    public void R03ActualMirrorClientsNeverStartTheRawKcpLoadGenerator(bool client, bool join, bool expected)
    {
        var args = new List<string>();
        if (client) args.Add("-load-test-client");
        if (join) { args.Add("-online-join"); args.Add("kcp://127.0.0.1:29730"); }
        var method = typeof(LocalServerLoadTest).GetMethod("UseSyntheticLoadClients", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.AreEqual(expected, method.Invoke(null, new object[] { args.ToArray() }));
    }

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
            if (enemy is HopRobotMovementController hop) Assert.IsFalse(hop.TryBarrelCrushStun(1));
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
            var samples = (Dictionary<int, List<CoopVisualSample>>)typeof(BombermanNetworkManager).GetField("coopVisualSamples", flags).GetValue(manager);
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
            samples[31] = Samples(body, nextBody); samples[32] = Samples(shadow, nextShadow);
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
            samples[31] = Samples(body, wrap); present.Invoke(manager, null);
            Assert.AreEqual(body.position, views[31].transform.position, "A wrap is a cut at its approved timestamp, not movement across the room.");
            var from = body; from.time = clock - 1; var afterGap = nextBody; afterGap.time = clock + .1;
            samples[31] = Samples(from, afterGap); present.Invoke(manager, null);
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

    // R02: position-only samples continue an approved visible stream; they never create, revive or reorder views,
    // keep the playback origin of their full frame, and are not serialized as client-only state.
    [Test]
    public void CoopMotionContinuesOnlyApprovedVisibleStreamsAndKeepsPlaybackOrigin()
    {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var host = new GameObject("CoopMotionClient"); host.SetActive(false);
        try
        {
            var manager = host.AddComponent<BombermanNetworkManager>();
            var type = typeof(BombermanNetworkManager);
            var samples = (Dictionary<int, List<CoopVisualSample>>)type.GetField("coopVisualSamples", flags).GetValue(manager);
            var removals = (Dictionary<int, double>)type.GetField("coopVisualRemovals", flags).GetValue(manager);
            var append = type.GetMethod("AppendCoopMotion", flags);
            var full = new CoopSpriteFrame { id = 5, time = 10, visible = true, position = new Vector3(1, 1), animation = 3, enemyStepLimit = 1.5f };
            var hidden = full; hidden.id = 6; hidden.visible = false;
            samples[5] = Samples(full); samples[6] = Samples(hidden);
            samples[7] = Samples(full); removals[7] = 9;
            append.Invoke(manager, new object[] { new[] {
                new CoopMotionEntry { id = 5, position = new Vector3(2, 1) }, new CoopMotionEntry { id = 6, position = new Vector3(9, 9) },
                new CoopMotionEntry { id = 7, position = new Vector3(9, 9) }, new CoopMotionEntry { id = 8, position = new Vector3(9, 9) } }, 10.05 });
            Assert.AreEqual(2, samples[5].Count);
            Assert.AreEqual(new Vector3(2, 1), samples[5][1].frame.position); Assert.AreEqual(10.05, samples[5][1].frame.time, 1e-9);
            Assert.AreEqual(3, samples[5][1].frame.animation); Assert.AreEqual(1.5f, samples[5][1].frame.enemyStepLimit);
            Assert.AreEqual(10d, samples[5][1].playbackOrigin, "The animation phase stays anchored to the full frame.");
            Assert.IsFalse(samples[5][1].cut, "A position-only sample is never a reposition.");
            Assert.AreEqual(1, samples[6].Count, "A hidden approved state is never moved by a position-only sample.");
            Assert.AreEqual(1, samples[7].Count, "A removed view is never revived.");
            Assert.IsFalse(samples.ContainsKey(8), "A position-only sample never creates a view.");
            append.Invoke(manager, new object[] { new[] { new CoopMotionEntry { id = 5, position = new Vector3(5, 5) } }, 10.05 });
            append.Invoke(manager, new object[] { new[] { new CoopMotionEntry { id = 5, position = new Vector3(5, 5) } }, 10.01 });
            Assert.AreEqual(2, samples[5].Count, "Duplicate or older samples are rejected.");
            for (int i = 0; i < 80; i++)
                append.Invoke(manager, new object[] { new[] { new CoopMotionEntry { id = 5, position = new Vector3(i, 0) } }, 11d + i });
            Assert.AreEqual(64, samples[5].Count, "The per-view history stays bounded.");
            samples[5] = Samples(full);
            type.GetMethod("ApplyCoopMotion", flags).Invoke(manager, new object[] { new CoopMotion { scene = "Other", time = 20,
                entries = new[] { new CoopMotionEntry { id = 5, position = Vector3.zero } } } });
            Assert.AreEqual(1, samples[5].Count, "A message for another scene is ignored.");

            using var writer = NetworkWriterPool.Get();
            writer.Write(new CoopMotion { scene = "Stage_1-1", time = 2, anchorTime = 1.98,
                anchors = new[] { new CoopMotionEntry { id = 4, position = new Vector3(1, 2) } }, entries = new[] { new CoopMotionEntry { id = 4, position = new Vector3(1, 3) } } });
            using var reader = NetworkReaderPool.Get(writer.ToArraySegment());
            var motion = reader.Read<CoopMotion>();
            Assert.AreEqual(1.98, motion.anchorTime); Assert.AreEqual(new Vector3(1, 2), motion.anchors[0].position); Assert.AreEqual(new Vector3(1, 3), motion.entries[0].position);
            // The playback origin and the reposition flag are client-only: the network frame carries neither.
            var all = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Assert.IsNull(typeof(CoopSpriteFrame).GetField("playbackTime", all));
            Assert.IsNull(typeof(CoopSpriteFrame).GetField("playbackOrigin", all));
            Assert.IsNull(typeof(CoopSpriteFrame).GetField("cut", all));
        }
        finally { Object.DestroyImmediate(host); }
    }

    // R02: a renderer that starts moving after a rest is anchored at its approved pose of the previous server frame;
    // continuous movers and the Development legacy stream are not.
    [Test]
    public void CoopMotionAnchorsRestStartsAndSkipsRotationChanges()
    {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var host = new GameObject("CoopMotionServer"); host.SetActive(false);
        var actor = new GameObject("ApprovedProp", typeof(SpriteRenderer));
        var type = typeof(BombermanNetworkManager);
        var legacy = type.GetField("coopLegacyState", BindingFlags.Static | BindingFlags.NonPublic);
        object oldLegacy = legacy.GetValue(null);
        try
        {
            legacy.SetValue(null, 2);
            var manager = host.AddComponent<BombermanNetworkManager>();
            var renderer = actor.GetComponent<SpriteRenderer>();
            int id = renderer.GetEntityId().GetHashCode();
            type.GetField("coopRenderers", flags).SetValue(manager, new[] { renderer });
            var last = (Dictionary<int, CoopSpriteFrame>)type.GetField("coopLastSprites", flags).GetValue(manager);
            var sent = (Dictionary<int, double>)type.GetField("coopLastSentTime", flags).GetValue(manager);
            var entries = (List<CoopMotionEntry>)type.GetField("coopMotionEntries", flags).GetValue(manager);
            var anchors = (List<CoopMotionEntry>)type.GetField("coopMotionAnchors", flags).GetValue(manager);
            var previousTime = type.GetField("coopPreviousFrameTime", flags);
            var publish = type.GetMethod("PublishCoopMotion", flags);
            double now = Time.unscaledTimeAsDouble;
            actor.transform.position = new Vector3(3, 4);
            last[id] = new CoopSpriteFrame { id = id, time = now - 1, visible = true, position = actor.transform.position,
                rotation = actor.transform.rotation, scale = actor.transform.lossyScale };
            sent[id] = now - 1;
            publish.Invoke(manager, new object[] { false });
            Assert.AreEqual(0, entries.Count, "The first observation only records the pose.");

            actor.transform.position = new Vector3(3.25f, 4); previousTime.SetValue(manager, now - .0167);
            publish.Invoke(manager, new object[] { false });
            Assert.AreEqual(1, entries.Count); Assert.AreEqual(new Vector3(3.25f, 4), entries[0].position);
            Assert.AreEqual(1, anchors.Count, "Motion after a rest starts from the approved pose of the previous frame.");
            Assert.AreEqual(new Vector3(3, 4), anchors[0].position); Assert.AreEqual(now, sent[id], 1e-9);

            actor.transform.position = new Vector3(3.5f, 4); previousTime.SetValue(manager, now - .0167); sent[id] = now - .0167;
            publish.Invoke(manager, new object[] { false });
            Assert.AreEqual(1, entries.Count); Assert.AreEqual(0, anchors.Count, "A continuous mover is not re-anchored.");

            actor.transform.position = new Vector3(3.75f, 4); actor.transform.rotation = Quaternion.Euler(0, 0, 30);
            previousTime.SetValue(manager, now - .0167); sent[id] = now - .0167;
            publish.Invoke(manager, new object[] { false });
            Assert.AreEqual(0, entries.Count, "A rotation change waits for the next full frame.");

            actor.transform.rotation = Quaternion.identity; actor.transform.position = new Vector3(4, 4);
            previousTime.SetValue(manager, now - .0167); sent[id] = now - .0167;
            var approved = last[id]; approved.visible = false; last[id] = approved;
            publish.Invoke(manager, new object[] { false });
            Assert.AreEqual(0, entries.Count, "A hidden approved state is not streamed.");

            approved.visible = true; approved.position = new Vector3(4, 4); approved.time = now - .2; last[id] = approved;
            var frame = approved; frame.time = now; frame.position = new Vector3(4.5f, 4);
            var poses = (Dictionary<int, Vector3>)type.GetField("coopPreviousPose", flags).GetValue(manager);
            poses[id] = new Vector3(4.1f, 4); previousTime.SetValue(manager, now - .0167); sent[id] = now - .2;
            var anchor = type.GetMethod("TryCoopRestAnchor", flags);
            var args = new object[] { id, approved, frame, null };
            Assert.IsTrue((bool)anchor.Invoke(manager, args));
            var started = (CoopSpriteFrame)args[3];
            Assert.AreEqual(now - .0167, started.time, 1e-9); Assert.AreEqual(new Vector3(4.1f, 4), started.position);
            args = new object[] { id, approved, frame, null };
            Assert.IsFalse((bool)anchor.Invoke(manager, args), "The same frame is anchored only once.");
            var still = approved; still.time = now;
            sent[id] = now - .2;
            Assert.IsFalse((bool)anchor.Invoke(manager, new object[] { id, approved, still, null }), "A sprite-only change needs no pose anchor.");

            legacy.SetValue(null, 1);
            sent[id] = now - .2;
            Assert.IsFalse((bool)anchor.Invoke(manager, new object[] { id, approved, frame, null }), "The Development legacy stream has no anchors.");
            actor.transform.position = new Vector3(9, 9); poses[id] = new Vector3(4, 4);
            publish.Invoke(manager, new object[] { false });
            Assert.AreEqual(new Vector3(4, 4), poses[id], "The Development legacy stream sends no position-only samples.");

            legacy.SetValue(null, 2);
            var overlay = new GameObject("ScreenOverlayCamera", typeof(Camera));
            try
            {
                actor.transform.SetParent(overlay.transform, true);
                type.GetField("coopMotionScreenSpace", flags).SetValue(manager, new Dictionary<int, bool>());
                poses.Remove(id); approved.position = actor.transform.position; approved.rotation = actor.transform.rotation;
                approved.scale = actor.transform.lossyScale; last[id] = approved; sent[id] = now - .0167;
                publish.Invoke(manager, new object[] { false });
                actor.transform.position += Vector3.right; previousTime.SetValue(manager, now - .0167);
                publish.Invoke(manager, new object[] { false });
                Assert.AreEqual(0, entries.Count, "Camera-attached screen-space overlays keep the 20 Hz full-frame stream.");
                Assert.IsFalse(poses.ContainsKey(id), "No per-frame pose is tracked for a screen-space overlay.");
            }
            finally { actor.transform.SetParent(null, true); Object.DestroyImmediate(overlay); }
        }
        finally { legacy.SetValue(null, oldLegacy); Object.DestroyImmediate(host); Object.DestroyImmediate(actor); }
    }

    // R02: the client interpolates between position-only samples exactly like full frames.
    [Test]
    public void CoopPresentationInterpolatesPositionOnlySamples()
    {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var oldMode = NetSync.Mode;
        var clientState = typeof(NetworkClient).GetField("connectState", BindingFlags.Static | BindingFlags.NonPublic); var oldClient = clientState.GetValue(null);
        var host = new GameObject("CoopMotionPresenter"); host.SetActive(false);
        var root = new GameObject("CoopMotionViews");
        var catalog = ScriptableObject.CreateInstance<CoopSceneCatalog>();
        var texture = new Texture2D(4, 4); var sprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), Vector2.one * .5f);
        BombermanNetworkManager manager = null;
        try
        {
            NetSync.Mode = NetSync.NetMode.Client; clientState.SetValue(null, System.Enum.Parse(clientState.FieldType, "Connected"));
            manager = host.AddComponent<BombermanNetworkManager>(); catalog.sprites = new[] { sprite }; catalog.materials = System.Array.Empty<Material>();
            var type = typeof(BombermanNetworkManager);
            type.GetField("coopCatalog", flags).SetValue(manager, catalog);
            type.GetField("coopViewRoot", flags).SetValue(manager, root);
            var samples = (Dictionary<int, List<CoopVisualSample>>)type.GetField("coopVisualSamples", flags).GetValue(manager);
            var views = (Dictionary<int, SpriteRenderer>)type.GetField("coopViews", flags).GetValue(manager);
            double clock = (double)type.GetProperty("CoopVisualClock", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            var full = new CoopSpriteFrame { id = 41, time = clock - .1, sprite = 0, material = -1, visible = true, position = new Vector3(0, 0),
                scale = Vector3.one, rotation = Quaternion.identity, color = Color.white };
            samples[41] = Samples(full);
            var append = type.GetMethod("AppendCoopMotion", flags);
            append.Invoke(manager, new object[] { new[] { new CoopMotionEntry { id = 41, position = new Vector3(1, 0) } }, clock - .02 });
            append.Invoke(manager, new object[] { new[] { new CoopMotionEntry { id = 41, position = new Vector3(2, 0) } }, clock + .03 });
            type.GetMethod("PresentOnlineSprites", flags).Invoke(manager, null);
            Assert.AreEqual(1.4f, views[41].transform.position.x, .0001f);
            Assert.AreEqual(2, samples[41].Count, "Samples behind the presentation clock are pruned.");
            type.GetField("coopViewRoot", flags).SetValue(manager, null); views.Clear();
        }
        finally
        {
            if (manager != null)
            {
                typeof(BombermanNetworkManager).GetField("coopViewRoot", flags).SetValue(manager, null);
                ((Dictionary<int, SpriteRenderer>)typeof(BombermanNetworkManager).GetField("coopViews", flags).GetValue(manager)).Clear();
            }
            Object.DestroyImmediate(host); Object.DestroyImmediate(root); Object.DestroyImmediate(catalog);
            Object.DestroyImmediate(sprite); Object.DestroyImmediate(texture); NetSync.Mode = oldMode; clientState.SetValue(null, oldClient);
        }
    }
    static List<CoopVisualSample> Samples(params CoopSpriteFrame[] frames)
    {
        var list = new List<CoopVisualSample>(frames.Length);
        foreach (var frame in frames) list.Add(CoopVisualSample.Full(frame));
        return list;
    }

    // R02: a screen-space overlay jump (sand grain wrap) is the approved end of the old segment followed by an explicit
    // cut, whichever of the full frame and the motion message arrives first; it never revives or creates a view.
    [Test]
    public void CoopOverlayRepositionIsAnExplicitCutInBothArrivalOrders()
    {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var host = new GameObject("CoopRepositionClient"); host.SetActive(false);
        try
        {
            var manager = host.AddComponent<BombermanNetworkManager>();
            var type = typeof(BombermanNetworkManager);
            var samples = (Dictionary<int, List<CoopVisualSample>>)type.GetField("coopVisualSamples", flags).GetValue(manager);
            var apply = type.GetMethod("ApplyCoopRepositions", flags);
            var grain = new CoopSpriteFrame { id = 9, time = 10, visible = true, position = Vector3.zero, sprite = 1 };
            var jumped = grain; jumped.time = 10.05; jumped.position = new Vector3(15, 0); jumped.sprite = 2;
            var anchor = new[] { new CoopMotionEntry { id = 9, position = new Vector3(.5f, 0) } };
            var target = new[] { new CoopMotionEntry { id = 9, position = new Vector3(15, 0) } };

            samples[9] = Samples(grain, jumped);
            apply.Invoke(manager, new object[] { anchor, target, 10.0333, 10.05 });
            var list = samples[9];
            Assert.AreEqual(3, list.Count, "Full frame first: the old segment gets its approved end before the cut.");
            Assert.AreEqual(10.0333, list[1].frame.time, 1e-9); Assert.AreEqual(new Vector3(.5f, 0), list[1].frame.position);
            Assert.AreEqual(1, list[1].frame.sprite, "The end of the old segment keeps its appearance.");
            Assert.IsFalse(list[1].cut); Assert.IsTrue(list[2].cut);
            Assert.IsTrue(CoopVisualSample.Interpolates(list[0], list[1]), "The old segment follows its approved trajectory.");
            Assert.IsFalse(CoopVisualSample.Interpolates(list[1], list[2]), "The jump is never interpolated across the screen.");

            samples[9] = Samples(grain);
            apply.Invoke(manager, new object[] { anchor, target, 10.0167, 10.0333 });
            list = samples[9];
            Assert.AreEqual(3, list.Count, "Motion message first: anchor, then the cut sample.");
            Assert.AreEqual(new Vector3(.5f, 0), list[1].frame.position); Assert.IsFalse(list[1].cut);
            Assert.AreEqual(new Vector3(15, 0), list[2].frame.position); Assert.AreEqual(10.0333, list[2].frame.time, 1e-9); Assert.IsTrue(list[2].cut);
            var next = jumped; next.position = new Vector3(15.5f, 0);
            list.Add(CoopVisualSample.Full(next));
            Assert.IsTrue(CoopVisualSample.Interpolates(list[2], list[3]), "The new segment continues from the reposition.");

            var hidden = grain; hidden.visible = false; samples[9] = Samples(hidden);
            apply.Invoke(manager, new object[] { null, target, 10.0, 10.1 });
            Assert.AreEqual(1, samples[9].Count, "A hidden approved state is never repositioned.");
            apply.Invoke(manager, new object[] { null, new[] { new CoopMotionEntry { id = 77, position = Vector3.one } }, 10.0, 10.1 });
            Assert.IsFalse(samples.ContainsKey(77), "A reposition never creates a view.");

            samples[9] = Samples(grain);
            for (int i = 0; i < 80; i++)
                apply.Invoke(manager, new object[] { anchor, new[] { new CoopMotionEntry { id = 9, position = Vector3.right * i } }, 10.4 + i, 11d + i });
            Assert.AreEqual(64, samples[9].Count, "The per-view history stays bounded.");
        }
        finally { Object.DestroyImmediate(host); }
    }

    // R02: the server detects an overlay jump between two server frames and queues the approved reposition;
    // ordinary drift is not a jump and overlays never get per-frame position samples.
    [Test]
    public void CoopOverlayJumpQueuesApprovedRepositionWithoutPerFrameSamples()
    {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var type = typeof(BombermanNetworkManager);
        var legacy = type.GetField("coopLegacyState", BindingFlags.Static | BindingFlags.NonPublic);
        object oldLegacy = legacy.GetValue(null);
        var host = new GameObject("CoopOverlayServer"); host.SetActive(false);
        var camera = new GameObject("OverlayCamera", typeof(Camera));
        var grain = new GameObject("SandGrain", typeof(SpriteRenderer));
        try
        {
            legacy.SetValue(null, 2);
            grain.transform.SetParent(camera.transform, false);
            var manager = host.AddComponent<BombermanNetworkManager>();
            var renderer = grain.GetComponent<SpriteRenderer>();
            int id = renderer.GetEntityId().GetHashCode();
            type.GetField("coopRenderers", flags).SetValue(manager, new[] { renderer });
            var last = (Dictionary<int, CoopSpriteFrame>)type.GetField("coopLastSprites", flags).GetValue(manager);
            last[id] = new CoopSpriteFrame { id = id, visible = true, rotation = Quaternion.identity, scale = Vector3.one };
            var track = type.GetMethod("TrackCoopOverlays", flags);
            var anchors = (List<CoopMotionEntry>)type.GetField("coopRepositionAnchors", flags).GetValue(manager);
            var repositions = (List<CoopMotionEntry>)type.GetField("coopRepositions", flags).GetValue(manager);
            var ids = (HashSet<int>)type.GetField("coopRepositionIds", flags).GetValue(manager);
            var entries = (List<CoopMotionEntry>)type.GetField("coopMotionEntries", flags).GetValue(manager);

            grain.transform.position = new Vector3(1, 1); track.Invoke(manager, new object[] { false });
            Assert.AreEqual(0, repositions.Count, "The first observation only records the pose.");
            grain.transform.position = new Vector3(2, 1); track.Invoke(manager, new object[] { false });
            Assert.AreEqual(0, repositions.Count, "Drift is not a reposition.");
            grain.transform.position = new Vector3(-14, 2); track.Invoke(manager, new object[] { false });
            Assert.AreEqual(1, repositions.Count); Assert.AreEqual(new Vector3(-14, 2), repositions[0].position);
            Assert.AreEqual(1, anchors.Count); Assert.AreEqual(new Vector3(2, 1), anchors[0].position, "The old segment ends at the previous frame pose.");
            Assert.IsTrue(ids.Contains(id));
            type.GetMethod("PublishCoopMotion", flags).Invoke(manager, new object[] { false });
            Assert.AreEqual(0, entries.Count, "Overlays keep the 20 Hz full-frame stream without per-frame samples.");
            track.Invoke(manager, new object[] { false });
            Assert.AreEqual(0, repositions.Count); Assert.AreEqual(0, ids.Count, "A reposition is queued once.");

            var approved = last[id]; approved.visible = false; last[id] = approved;
            grain.transform.position = new Vector3(10, 2); track.Invoke(manager, new object[] { false });
            Assert.AreEqual(0, repositions.Count, "A hidden approved overlay is not repositioned.");
            legacy.SetValue(null, 1);
            approved.visible = true; last[id] = approved;
            grain.transform.position = new Vector3(-10, 2); track.Invoke(manager, new object[] { false });
            Assert.AreEqual(0, repositions.Count, "The Development legacy stream has no repositions.");
        }
        finally { legacy.SetValue(null, oldLegacy); Object.DestroyImmediate(host); Object.DestroyImmediate(grain); Object.DestroyImmediate(camera); }
    }

    // R02: one position sample per renderer per Mirror send tick at most; the Development comparison modes thin the
    // samples (half: every second frame; segments: heartbeat plus the approved turn vertex).
    [Test]
    public void CoopMotionFrequencyIsCappedAndComparisonModesThinSamples()
    {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var statics = BindingFlags.Static | BindingFlags.NonPublic;
        var type = typeof(BombermanNetworkManager);
        var legacy = type.GetField("coopLegacyState", statics); object oldLegacy = legacy.GetValue(null);
        var modeField = type.GetField("coopMotionModeState", statics); object oldMode = modeField.GetValue(null);
        var host = new GameObject("CoopMotionModes"); host.SetActive(false);
        var actor = new GameObject("MovingProp", typeof(SpriteRenderer));
        try
        {
            legacy.SetValue(null, 2); modeField.SetValue(null, 1);
            var manager = host.AddComponent<BombermanNetworkManager>();
            var renderer = actor.GetComponent<SpriteRenderer>();
            int id = renderer.GetEntityId().GetHashCode();
            type.GetField("coopRenderers", flags).SetValue(manager, new[] { renderer });
            var last = (Dictionary<int, CoopSpriteFrame>)type.GetField("coopLastSprites", flags).GetValue(manager);
            var sent = (Dictionary<int, double>)type.GetField("coopLastSentTime", flags).GetValue(manager);
            var poses = (Dictionary<int, Vector3>)type.GetField("coopPreviousPose", flags).GetValue(manager);
            var moved = (HashSet<int>)type.GetField("coopMovedLastFrame", flags).GetValue(manager);
            var entries = (List<CoopMotionEntry>)type.GetField("coopMotionEntries", flags).GetValue(manager);
            var anchors = (List<CoopMotionEntry>)type.GetField("coopMotionAnchors", flags).GetValue(manager);
            var previousTime = type.GetField("coopPreviousFrameTime", flags);
            var publish = type.GetMethod("PublishCoopMotion", flags);
            double now = Time.unscaledTimeAsDouble;
            last[id] = new CoopSpriteFrame { id = id, time = now - 1, visible = true, rotation = actor.transform.rotation, scale = actor.transform.lossyScale };
            void Step(float x, float y, double sentAgo)
            {
                poses[id] = actor.transform.position; moved.Add(id); sent[id] = now - sentAgo;
                previousTime.SetValue(manager, now - .0167); actor.transform.position = new Vector3(x, y);
                publish.Invoke(manager, new object[] { false });
            }

            actor.transform.position = Vector3.zero;
            Step(.1f, 0, .01);
            Assert.AreEqual(0, entries.Count, "Frequency cap: no second sample inside one Mirror send tick.");
            Step(.2f, 0, .0167);
            Assert.AreEqual(1, entries.Count, "Frame mode: one sample per server frame.");

            modeField.SetValue(null, 2);
            Step(.3f, 0, .0167);
            Assert.AreEqual(0, entries.Count, "Half mode skips every other frame.");
            Step(.4f, 0, .0334);
            Assert.AreEqual(1, entries.Count);

            modeField.SetValue(null, 3);
            var segments = (System.Collections.IDictionary)type.GetField("coopSegments", flags).GetValue(manager);
            var segment = segments[id];
            var reset = segment.GetType().GetMethod("Reset"); var add = segment.GetType().GetMethod("Add");
            reset.Invoke(segment, new object[] { now - .0334, new Vector3(.4f, 0) }); add.Invoke(segment, new object[] { now - .0167, new Vector3(.5f, 0) });
            actor.transform.position = new Vector3(.5f, 0);
            Step(.6f, 0, .0334);
            Assert.AreEqual(0, entries.Count, "A straight segment waits for the heartbeat."); Assert.AreEqual(0, anchors.Count);
            reset.Invoke(segment, new object[] { now - .0334, new Vector3(.6f, 0) }); add.Invoke(segment, new object[] { now - .0167, new Vector3(.7f, 0) });
            actor.transform.position = new Vector3(.7f, 0);
            Step(.7f, .1f, .0334);
            Assert.AreEqual(0, entries.Count);
            Assert.AreEqual(1, anchors.Count, "A turn sends the approved vertex of the previous frame."); Assert.AreEqual(new Vector3(.7f, 0), anchors[0].position);
            Assert.AreEqual(now - .0167, sent[id], 1e-9);
            Step(.7f, .2f, .05);
            Assert.AreEqual(1, entries.Count, "The heartbeat bounds the segment length.");
        }
        finally { legacy.SetValue(null, oldLegacy); modeField.SetValue(null, oldMode); Object.DestroyImmediate(host); Object.DestroyImmediate(actor); }
    }

    // R02: the residual metric separates an approved hold (the next sample repeats the pose: the native object stopped)
    // from real starvation (the pose changed while the client held it), and splits delay into network lag and hold.
    [Test]
    public void CoopArrivalSeparatesApprovedHoldsFromRealStarvationAndSplitsDelay()
    {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var host = new GameObject("CoopArrivalStats"); host.SetActive(false);
        try
        {
            var manager = host.AddComponent<BombermanNetworkManager>();
            var type = typeof(BombermanNetworkManager);
            double clock = (double)type.GetProperty("CoopVisualClock", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            var arrival = type.GetMethod("RecordCoopArrival", flags);
            var stats = (System.Collections.IDictionary)type.GetField("coopPresentStats", flags).GetValue(manager);
            object Stat(int id, string field) => stats[id].GetType().GetField(field).GetValue(stats[id]);

            var still = new CoopSpriteFrame { id = 3, time = clock - .05, visible = true, position = Vector3.one };
            var same = still; same.time = clock - .01;
            arrival.Invoke(manager, new object[] { 3, Samples(still), CoopVisualSample.Full(same) });
            Assert.AreEqual(1, Stat(3, "approvedHolds")); Assert.AreEqual(0, Stat(3, "moveStarved"));
            var stillOther = still; stillOther.id = 4;
            var movedFrame = stillOther; movedFrame.time = clock - .01; movedFrame.position = new Vector3(2, 1);
            arrival.Invoke(manager, new object[] { 4, Samples(stillOther), CoopVisualSample.Full(movedFrame) });
            Assert.AreEqual(0, Stat(4, "approvedHolds")); Assert.AreEqual(1, Stat(4, "moveStarved"));
            Assert.Greater((double)Stat(4, "moveStarvedMs"), 0d);

            var presented = type.GetMethod("RecordCoopPresented", flags);
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            presented.Invoke(manager, new object[] { 5, still, false, clock, clock, started });
            var step = still; step.position = new Vector3(2, 1);
            presented.Invoke(manager, new object[] { 5, step, false, clock - .3, clock, started });
            Assert.AreEqual(300f, (float)Stat(5, "holdMax"), .5f, "A non-interpolated old state is presentation hold.");
            Assert.AreEqual((float)Stat(5, "delayMax") - 300f, (float)Stat(5, "lagMax"), .5f, "The remainder is network/timeline lag.");
            int binned = 0; foreach (int count in (int[])Stat(5, "delayBins")) binned += count;
            Assert.AreEqual(1, binned, "Only moving presented frames enter the delay histogram.");
        }
        finally { Object.DestroyImmediate(host); }
    }
    // R02: the server samples the co-op world after every LateUpdate that moves renderers (camera follow, screen-space
    // overlays), never in the manager's own early LateUpdate, so each sample is the pose rendered in the stamped frame.
    [Test]
    public void CoopCaptureRunsAfterEveryLateUpdateThatMovesOverlays()
    {
        static int Order(System.Type type) =>
            ((DefaultExecutionOrder)System.Attribute.GetCustomAttribute(type, typeof(DefaultExecutionOrder)))?.order ?? 0;
        int capture = Order(typeof(CoopLateCaptureDriver));
        Assert.Greater(capture, Order(typeof(BombermanNetworkManager)));
        Assert.Greater(capture, Order(typeof(StageAssets.Stage34SandstormEffect)));
        Assert.Greater(capture, Order(typeof(CameraFollowClamp2D)));
        Assert.Greater(capture, Order(typeof(UrpPixelPerfectCameraFallback)));
        foreach (var type in typeof(BombermanNetworkManager).Assembly.GetTypes())
            if (typeof(MonoBehaviour).IsAssignableFrom(type) && type != typeof(CoopLateCaptureDriver))
                Assert.Less(Order(type), capture, type.FullName + " runs after the co-op capture");
        Assert.IsTrue((bool)typeof(BombermanNetworkManager).GetProperty("CoopLateCapture", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null),
            "Release and default Development builds capture late; only -coop-r02-capture-early restores the old phase.");
    }
    // R02 targeted allocation measurement (per message and per path). The removed client-only field is reproduced by a
    // stand-in element with one extra double; the process-level KiB/s comparison is reported separately in the docs.
    readonly struct CoopSpriteFrameWithPlaybackTime
    {
        public readonly CoopSpriteFrame frame; public readonly double playbackTime;
        public CoopSpriteFrameWithPlaybackTime(in CoopSpriteFrame frame, double playbackTime) { this.frame = frame; this.playbackTime = playbackTime; }
    }

    [Test]
    public void CoopSpriteMessageAllocationsArePerPathMeasured()
    {
        const int frames = 48, rounds = 64;
        var source = new CoopSpriteFrame[frames];
        for (int i = 0; i < frames; i++)
            source[i] = new CoopSpriteFrame { id = i + 1, sprite = 3, visible = true, position = new Vector3(i, 2), scale = Vector3.one,
                rotation = Quaternion.identity, color = Color.white };
        object sink = null;
        // Unity's Mono returns 0 from GC.GetAllocatedBytesForCurrentThread, so the managed heap growth of a short window is
        // used instead; a window with a collection inside is discarded (at most 20 attempts, the smallest valid one wins).
        long Measure(System.Action action)
        {
            for (int i = 0; i < 8; i++) action();
            long best = long.MaxValue;
            for (int attempt = 0; attempt < 20; attempt++)
            {
                int collections = System.GC.CollectionCount(0);
                long before = System.GC.GetTotalMemory(false);
                for (int i = 0; i < rounds; i++) action();
                long grown = System.GC.GetTotalMemory(false) - before;
                if (System.GC.CollectionCount(0) == collections && grown >= 0) best = System.Math.Min(best, grown / rounds);
            }
            Assert.AreNotEqual(long.MaxValue, best, "every measurement window had a collection");
            return best;
        }
        long chunk = Measure(() => sink = new CoopSpriteFrame[frames]);
        long chunkWithField = Measure(() => sink = new CoopSpriteFrameWithPlaybackTime[frames]);
        var standIn = new CoopSpriteFrameWithPlaybackTime(source[1], 2);
        Assert.AreEqual(2, standIn.frame.id); Assert.AreEqual(2.0, standIn.playbackTime);
        var message = new CoopSprites { time = 1, scene = "Stage_3-4", frames = source, removed = System.Array.Empty<int>() };
        var writer = new NetworkWriter();
        long write = Measure(() => { writer.Reset(); writer.Write(message); });
        writer.Reset(); writer.Write(message);
        var bytes = writer.ToArray();
        var reader = new NetworkReader(bytes);
        long read = Measure(() => { reader.SetBuffer(new System.ArraySegment<byte>(bytes)); sink = reader.Read<CoopSprites>(); });
        int frameSize = Unity.Collections.LowLevel.Unsafe.UnsafeUtility.SizeOf<CoopSpriteFrame>();
        int oldFrameSize = Unity.Collections.LowLevel.Unsafe.UnsafeUtility.SizeOf<CoopSpriteFrameWithPlaybackTime>();
        int sampleSize = Unity.Collections.LowLevel.Unsafe.UnsafeUtility.SizeOf<CoopVisualSample>();
        System.GC.KeepAlive(sink);
        TestContext.WriteLine("R02 alloc per message (48 frames): server_chunk=" + chunk + " server_chunk_with_field=" + chunkWithField +
            " serialize=" + write + " client_read=" + read + " client_read_with_field_estimate=" + (read + chunkWithField - chunk) +
            " wire_bytes=" + bytes.Length + " frame_struct=" + frameSize + " frame_struct_with_field=" + oldFrameSize + " client_history_sample=" + sampleSize);
        // Every chunk length the sender can produce (the last chunk of a capture holds count % 48 frames). Reported only:
        // arrays above ~2 KiB are whole heap blocks and measure exactly; smaller ones come from per-thread free lists that
        // are refilled in batches, so their windows are below the resolution of the heap counter.
        var bySize = new System.Text.StringBuilder();
        int differing = 0; long savedTotal = 0;
        for (int n = 1; n <= frames; n++)
        {
            int length = n;
            long now = Measure(() => sink = new CoopSpriteFrame[length]);
            long withField = Measure(() => sink = new CoopSpriteFrameWithPlaybackTime[length]);
            if (length * (long)oldFrameSize >= 2048 && withField != now) { differing++; savedTotal += withField - now; }
            bySize.Append(n).Append(':').Append(now).Append('/').Append(withField).Append(' ');
        }
        TestContext.WriteLine("R02 chunk alloc now/with_field by frame count: " + bySize + "| large_counts_with_saving=" + differing + " saved_bytes_sum=" + savedTotal);
        Assert.AreEqual(8, oldFrameSize - frameSize, "The removed double was 8 bytes of every frame element.");
        Assert.Less(write, 64, "Serialization into a reused writer allocates nothing per frame.");
        Assert.GreaterOrEqual(chunk, frames * (long)frameSize, "The server chunk array holds every frame element.");
        Assert.GreaterOrEqual(chunkWithField, chunk, "The removed double can only shrink the server chunk allocation.");
        Assert.GreaterOrEqual(read, frames * (long)frameSize, "The client read allocates at least the frame array.");
        Assert.LessOrEqual(sampleSize, oldFrameSize + 8, "The client-only history element costs at most one extra padding slot.");
    }
}

using System;
using System.Reflection;
using Assets.Scripts.Netcode;
using Mirror;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed partial class OnlineRoomEditModeTests
{
    [Test]
    public void LaunchGeyserSceneCatalogContainsEveryRuntimeAmbientBubbleFrame()
    {
        CoopSceneCatalogBuilder.Generate();
        var bubbles = UnityEditor.AssetDatabase.LoadAssetAtPath<StageAssets.Stage33BubbleAnimationCatalog>(
            "Assets/Resources/StageAssets/Stage33BubbleAnimations.asset");
        var scene = UnityEditor.AssetDatabase.LoadAssetAtPath<CoopSceneCatalog>("Assets/Resources/CoopScenes/Stage_3-3.asset");
        Assert.IsNotNull(bubbles); Assert.IsNotNull(scene);
        int frames = 0;
        foreach (var animation in bubbles.animations)
            foreach (var sprite in animation.sprites)
            {
                Assert.IsNotNull(sprite);
                CollectionAssert.Contains(scene.sprites, sprite);
                frames++;
            }
        Assert.Greater(frames, 40, "Exercise the full resource animation, not just its idle sprites.");
    }

    [TestCase(0f, 2f, 3f)] [TestCase(.25f, 3f, 4.5f)] [TestCase(.5f, 4f, 5f)]
    [TestCase(.75f, 5f, 4.5f)] [TestCase(1f, 6f, 3f)] [TestCase(2f, 6f, 3f)]
    public void LaunchParabolaKeepsNativeRootAndVisualVariants(float time, float x, float y)
    {
        var arc = new PlayerLaunchArc { start = new(2, 3), end = new(6, 3), duration = 1, height = 2, rootCarriesHeight = true };
        Assert.AreEqual(new Vector2(x, y), arc.Position(time)); Assert.AreEqual(0, arc.VisualHeight(time));
        arc.rootCarriesHeight = false;
        Assert.AreEqual(new Vector2(x, 3), arc.Position(time)); Assert.AreEqual(y - 3, arc.VisualHeight(time));
    }

    [TestCase(0f, 0f)] [TestCase(.05f, 10f)] [TestCase(.1f, 20f)] [TestCase(.4f, 20f)]
    [TestCase(.7f, 20f)] [TestCase(.75f, 10f)] [TestCase(.8f, 0f)] [TestCase(1f, 0f)]
    public void LaunchBattleSpringPlateauKeepsNativeAscendHoldAndDescend(float time, float expected)
    {
        var arc = new PlayerLaunchArc { duration = .8f, height = 20, plateau = true, ascend = .1f, descend = .1f };
        Assert.AreEqual(expected, arc.Height(time), .0001f);
    }

    [TestCase(-5.25f, 0f, 4.75f, 0f)] [TestCase(5.25f, 0f, -4.75f, 0f)]
    [TestCase(5f, 0f, 5f, 0f)] [TestCase(0f, 5.1f, 0f, -4f)] [TestCase(0f, -5.1f, 0f, 4f)]
    public void LaunchRoomWrapPreservesOvershootEdgesAndInnerVerticalLanding(float x, float y, float expectedX, float expectedY)
    {
        Assert.That(Vector2.Distance(new Vector2(expectedX, expectedY), PlayerLaunchArc.Wrap(new Vector2(x, y), true, new Vector4(-5, -5, 5, 5), 1)), Is.LessThan(.00001f));
    }

    [Test]
    public void LaunchRoomHeightClampsBeforeQuantizationAndKeepsGroundSeparate()
    {
        var arc = new PlayerLaunchArc { start = new(0, 3), end = new(0, 3), duration = 1, height = 3,
            bounded = true, room = new Vector4(-5, -5, 5, 5), tileSize = 1, quantizeVisual = true };
        Assert.AreEqual(1, arc.Height(.5f)); Assert.AreEqual(new Vector2(0, 3), arc.Position(.5f));
        arc.bounded = false; arc.height = 1.3f;
        Assert.AreEqual(1.3f, arc.Height(.5f)); Assert.AreEqual(1.3125f, arc.VisualHeight(.5f));
        arc.plateau = true; arc.ascend = arc.descend = 0;
        Assert.AreEqual(1.3f, arc.Height(0)); Assert.AreEqual(0, arc.Height(1));
    }

    [TestCase(0u, 0, 47)] [TestCase(4u, 1, 56)] [TestCase(4u, -1, 56)]
    public void LaunchPoseWireMetadataIsConditionalAndRoundTrips(uint sequence, int mode, int expectedBytes)
    {
        var pose = new NetworkVisualPose { valid = true, position = new(1, 2, 3), scale = Vector3.one,
            rotation = Quaternion.identity, offsetLimit = 24, launchSequence = sequence,
            launchLiftMode = (sbyte)mode, launchLift = sequence == 0 ? 0 : mode * 1.25f };
        using var writer = NetworkWriterPool.Get(); writer.Write(pose);
        Assert.AreEqual(expectedBytes, writer.Position);
        using var reader = NetworkReaderPool.Get(writer.ToArraySegment());
        Assert.IsTrue(pose.Equals(reader.Read<NetworkVisualPose>()));
    }

    sealed class LaunchClientFixture : IDisposable
    {
        readonly NetSync.NetMode oldMode = NetSync.Mode;
        readonly FieldInfo clientState = typeof(NetworkClient).GetField("connectState", BindingFlags.NonPublic | BindingFlags.Static);
        readonly object oldClientState;
        public readonly GameObject actor = new("LaunchClientFixture");
        public readonly NetworkPlayerInput input;
        public readonly MovementController movement;
        public readonly Rigidbody2D body;
        public readonly Collider2D collider;
        public readonly NetworkIdentity identity;
        public const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        public LaunchClientFixture(bool owner)
        {
            oldClientState = clientState.GetValue(null);
            NetSync.Mode = NetSync.NetMode.Client; clientState.SetValue(null, Enum.Parse(clientState.FieldType, "Connected"));
            actor.SetActive(false); actor.tag = "Player"; collider = actor.AddComponent<BoxCollider2D>();
            actor.AddComponent<MovementController>();
            input = actor.AddComponent<NetworkPlayerInput>(); identity = actor.GetComponent<NetworkIdentity>();
            typeof(NetworkIdentity).GetMethod("InitializeNetworkBehaviours", Private).Invoke(identity, null);
            typeof(NetworkIdentity).GetProperty("isLocalPlayer").SetValue(identity, owner);
            movement = actor.GetComponent<MovementController>(); body = actor.GetComponent<Rigidbody2D>(); body.bodyType = RigidbodyType2D.Kinematic;
            typeof(MovementController).GetProperty("Rigidbody").SetValue(movement, body);
            typeof(NetworkPlayerInput).GetMethod("Awake", Private).Invoke(input, null);
            foreach (var behaviour in actor.GetComponents<MonoBehaviour>()) behaviour.enabled = false;
            actor.SetActive(true); movement.SetNetworkTickDriven(true);
        }
        public void Dispose() { Object.DestroyImmediate(actor); NetSync.Mode = oldMode; clientState.SetValue(null, oldClientState); }
    }

    static NetworkPlayerInput.LaunchState LaunchReceipt(double time, bool rootHeight = true) => new()
    {
        sequence = 1, revision = 1, time = time, elapsed = .25f, active = true,
        arc = new PlayerLaunchArc { start = new(2, 3), end = new(6, 3), duration = 1, height = 2, rootCarriesHeight = rootHeight }
    };

    [TestCase(true)] [TestCase(false)]
    public void LaunchApprovedCurveKeepsRootContinuousAcrossReconciliationWithoutGrantingGameplay(bool owner)
    {
        using var f = new LaunchClientFixture(owner);
        double now = owner ? NetworkTime.predictedTime : NetworkTime.time;
        var state = LaunchReceipt(now - .25);
        f.input.ReceiveLaunchState(state); f.input.RestoreApprovedLaunchPosition();
        Assert.That(Vector2.Distance(new Vector2(4, 5), f.body.position), Is.LessThan(.001f));
        Assert.IsFalse(f.movement.InputLocked); Assert.IsFalse(f.movement.explosionInvulnerable); Assert.IsTrue(f.collider.enabled);
        if (owner)
        {
            f.input.ReceiveMovementState(3, new MovementController.NetworkMovementState { position = new(9, 9),
                worldSpeed = 4, canPredict = false, continuousPresentation = true, serverTime = now }, default, default);
            Assert.That(Vector2.Distance(new Vector2(4, 5), f.body.position), Is.LessThan(.001f));
        }
        var stale = state; stale.arc.end += Vector2.right * 10;
        f.input.ReceiveLaunchState(stale); f.input.RestoreApprovedLaunchPosition();
        Assert.That(Vector2.Distance(new Vector2(4, 5), f.body.position), Is.LessThan(.001f));
    }

    [Test]
    public void LaunchLateObserverUsesRetainedPausedPhaseAndDoesNotReviveAfterStop()
    {
        using var f = new LaunchClientFixture(false);
        var state = LaunchReceipt(10); state.paused = true;
        f.input.ReceiveLaunchState(state);
        Assert.IsFalse(f.input.TrySampleApprovedLaunch(9.9, out _, out _));
        Assert.IsTrue(f.input.TrySampleApprovedLaunch(10.5, out _, out float elapsed)); Assert.AreEqual(.25f, elapsed);
        state.revision = 2; state.active = false; state.time = 11; f.input.ReceiveLaunchState(state);
        Assert.IsTrue(f.input.TrySampleApprovedLaunch(10.9, out _, out _));
        Assert.IsFalse(f.input.TrySampleApprovedLaunch(11, out _, out _));
        state.revision = 1; state.active = true; f.input.ReceiveLaunchState(state);
        Assert.IsFalse(f.input.TrySampleApprovedLaunch(11.1, out _, out _));
    }

    [Test]
    public void LaunchExactVisualLiftPreservesFrameBobbingAndNeverMovesGameplayRoot()
    {
        using var f = new LaunchClientFixture(false);
        var go = new GameObject("LaunchApprovedSprite", typeof(SpriteRenderer)); go.transform.SetParent(f.actor.transform, false);
        var state = LaunchReceipt(NetworkTime.time - .25, false); f.input.ReceiveLaunchState(state); f.input.RestoreApprovedLaunchPosition();
        var pose = new NetworkVisualPose { valid = true, position = new(0, 1.1f, 0), scale = Vector3.one, rotation = Quaternion.identity,
            launchSequence = 1, launchLiftMode = 1, launchLift = 1 };
        pose.Apply(f.actor.transform, go.GetComponent<SpriteRenderer>());
        Assert.AreEqual(5.1f, go.transform.position.y, .001f); Assert.AreEqual(new Vector2(4, 3), f.body.position);
        pose.launchSequence = 2; pose.Apply(f.actor.transform, go.GetComponent<SpriteRenderer>());
        Assert.AreEqual(4.1f, go.transform.position.y, .001f, "A different launch generation cannot borrow its curve.");
    }

    [Test]
    public void LaunchInitialPausedRecipeAndGlobalPauseRetainPositionWithoutAnOriginSnap()
    {
        using var f = new LaunchClientFixture(true);
        var paused = typeof(GamePauseController).GetProperty(nameof(GamePauseController.IsPaused)); bool prior = GamePauseController.IsPaused;
        try
        {
            paused.SetValue(null, true);
            var state = LaunchReceipt(NetworkTime.predictedTime - .5); state.paused = true;
            f.input.ReceiveLaunchState(state); f.input.RestoreApprovedLaunchPosition();
            var position = f.body.position; Assert.That(Vector2.Distance(new Vector2(3, 4.5f), position), Is.LessThan(.001f));
            f.input.ReceiveMovementState(3, new MovementController.NetworkMovementState { position = new(9, 9),
                worldSpeed = 4, canPredict = false, continuousPresentation = true }, default, default);
            Assert.AreEqual(position, f.body.position);
        }
        finally { paused.SetValue(null, prior); }
    }

    [TestCase(true)] [TestCase(false)]
    public void LaunchLongApprovedPauseRetainsItsGenerationAndResumesAtTheSamePhase(bool owner)
    {
        using var f = new LaunchClientFixture(owner);
        double now = owner ? NetworkTime.predictedTime : NetworkTime.time;
        var state = LaunchReceipt(now - 20); state.paused = true; state.elapsed = .25f;
        f.input.ReceiveLaunchState(state); f.input.RestoreApprovedLaunchPosition();
        Assert.IsTrue(f.input.ApprovedLaunchActive);
        Assert.AreEqual(state.arc.Position(.25f), f.body.position);
        state.revision = 2; state.paused = false; state.time = now - .1;
        f.input.ReceiveLaunchState(state); f.input.RestoreApprovedLaunchPosition();
        Assert.IsTrue(f.input.ApprovedLaunchActive);
        Assert.AreEqual(state.arc.Position(.35f).x, f.body.position.x, .02f);
    }

    [TestCase(true)] [TestCase(false)]
    public void LaunchNewSampleCannotRewindAnAlreadyPresentedPhaseInTheSameGeneration(bool owner)
    {
        using var f = new LaunchClientFixture(owner);
        double now = owner ? NetworkTime.predictedTime : NetworkTime.time;
        var state = LaunchReceipt(now - .5, false); state.elapsed = 0;
        f.input.ReceiveLaunchState(state); f.input.RestoreApprovedLaunchPosition();
        Vector2 before = f.body.position;
        state.revision = 2; state.time = now - .4;
        f.input.ReceiveLaunchState(state); f.input.RestoreApprovedLaunchPosition();
        Assert.AreEqual(before, f.body.position);
        Assert.IsTrue(f.input.TryApprovedLaunchLift(state.sequence, 1, out float height));
        Assert.AreEqual(state.arc.VisualHeight(.5f), height, .01f);
        state.sequence = 2; state.revision = 3; state.time = now;
        f.input.ReceiveLaunchState(state); f.input.RestoreApprovedLaunchPosition();
        Assert.AreEqual(state.arc.start.x, f.body.position.x, .02f, "A new generation starts its own phase.");
    }

    [Test]
    public void LaunchAuthoritativeHandoffReplaysOnlyWalkingAndKeepsBombInputOutOfGameplay()
    {
        using var f = new LaunchClientFixture(true);
        double now = NetworkTime.predictedTime;
        f.movement.enabled = true;
        var state = LaunchReceipt(now - .75); f.input.ReceiveLaunchState(state); f.input.RestoreApprovedLaunchPosition();
        var history = (NetworkInputHistory)typeof(NetworkPlayerInput).GetField("pending", LaunchClientFixture.Private).GetValue(f.input);
        history.Add(new NetworkInputFrame { tick = 5, held = 8 | 16, pressed = 16 });
        f.input.ReceiveMovementState(4, new MovementController.NetworkMovementState { position = new(6, 3), facing = Vector2.right,
            worldSpeed = 4, animationSpeed = PlayerPersistentStats.BaseSpeedNormal, canPredict = true, serverTime = now }, default, default);
        Assert.IsFalse(f.input.ApprovedLaunchActive); Assert.IsTrue(f.movement.PredictLocally);
        Assert.Greater(f.body.position.x, 6); Assert.AreEqual(3, f.body.position.y, .001f);
        var planted = (System.Collections.ICollection)typeof(BombController).GetField("plantedBombs", LaunchClientFixture.Private).GetValue(f.actor.GetComponent<BombController>());
        Assert.AreEqual(0, planted.Count);
    }

    [Test]
    public void LaunchApprovedBattleShadowStaysOnGroundAndIsReleasedWithTheView()
    {
        using var f = new LaunchClientFixture(false);
        var state = LaunchReceipt(NetworkTime.time - .25, false); state.arc.groundShadow = true;
        state.arc.shadowColor = new Color(0, 0, 0, .45f); state.arc.shadowScale = new(.75f, .32f); state.arc.shadowOrder = -1;
        f.input.ReceiveLaunchState(state); f.input.RestoreApprovedLaunchPosition();
        var shadow = (SpriteRenderer)typeof(NetworkPlayerInput).GetField("launchShadow", LaunchClientFixture.Private).GetValue(f.input);
        Assert.IsNotNull(shadow); Assert.IsTrue(shadow.enabled); Assert.AreEqual(new Vector3(4, 3, 0), shadow.transform.position);
        Assert.AreEqual(state.arc.shadowColor, shadow.color); Assert.AreEqual(-1, shadow.sortingOrder);
        Assert.IsNull(shadow.GetComponent<Collider2D>()); Assert.IsNull(shadow.GetComponent<NetworkIdentity>());
        typeof(NetworkPlayerInput).GetMethod("ClearApprovedLaunchHistory", LaunchClientFixture.Private).Invoke(f.input, null);
        Assert.IsTrue(shadow == null);
    }

    [Test]
    public void LaunchDeathCancelsEvenAFutureRecipeAndOldSamplesCannotResurrectAfterward()
    {
        using var f = new LaunchClientFixture(false);
        double now = NetworkTime.time;
        var state = LaunchReceipt(now + .1); f.input.ReceiveLaunchState(state);
        f.movement.isDead = true; f.input.RestoreApprovedLaunchPosition(); f.movement.isDead = false;
        Assert.IsFalse(f.input.TrySampleApprovedLaunch(now + .3, out _, out _));
        state.revision = 2; state.time = now; f.input.ReceiveLaunchState(state);
        Assert.IsFalse(f.input.TrySampleApprovedLaunch(now + .3, out _, out _));
    }

    [Test]
    public void LaunchLandingBounceAndStopWaitForTheMainFlightInsteadOfTruncatingIt()
    {
        using var f = new LaunchClientFixture(true);
        f.actor.SetActive(false); var fx = f.actor.AddComponent<NetworkBombFx>();
        typeof(NetworkIdentity).GetMethod("InitializeNetworkBehaviours", LaunchClientFixture.Private).Invoke(f.identity, null);
        f.actor.SetActive(true);
        var state = LaunchReceipt(NetworkTime.predictedTime - .25); f.input.ReceiveLaunchState(state); f.input.RestoreApprovedLaunchPosition();
        var bounce = new NetworkBombFx.PlayerBounceMotion { revision = 1, first = true, moving = true, start = new(6, 3), end = new(7, 3), duration = .2f, curveDuration = .2f, height = .5f };
        fx.ReceivePlayerBounce(bounce); Assert.IsFalse(fx.PlayerBounceActive); Assert.IsTrue(f.input.ApprovedLaunchActive);
        bounce.revision = 2; bounce.first = false; bounce.moving = false; fx.ReceivePlayerBounce(bounce);
        state.revision = 2; state.active = false; state.time = NetworkTime.predictedTime;
        f.input.ReceiveLaunchState(state); f.input.RestoreApprovedLaunchPosition();
        Assert.IsTrue(fx.PlayerBounceActive); Assert.IsFalse(f.input.ApprovedLaunchActive);
        typeof(NetworkBombFx).GetMethod("UpdatePlayerBouncePresentation", LaunchClientFixture.Private).Invoke(fx, new object[] { .25f, .25f });
        Assert.IsFalse(fx.PlayerBounceActive); Assert.AreEqual(new Vector2(7, 3), f.body.position); Assert.IsTrue(f.collider.enabled);
    }

    [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
    public void LaunchMalformedRecipeCannotStartPresentation(int invalid)
    {
        var state = LaunchReceipt(0);
        if (invalid == 0) state.arc.duration = float.NaN;
        if (invalid == 1) state.arc.end.x = float.PositiveInfinity;
        if (invalid == 2) state.arc.bounded = true;
        if (invalid == 3) state.elapsed = float.NaN;
        Assert.IsFalse(NetworkPlayerInput.ValidLaunch(state));
    }

    [Test]
    public void LaunchNativeCannonDisposalPublishesStopForItsOwnGeneration()
    {
        using var f = new CannonLifetimeFixture(); var input = f.ConfigureServer(); var flight = f.Start("LaunchPlayerArc");
        Assert.IsTrue(flight.MoveNext());
        var field = typeof(NetworkPlayerInput).GetField("serverLaunch", LaunchClientFixture.Private);
        var state = (NetworkPlayerInput.LaunchState)field.GetValue(input); Assert.IsTrue(state.active);
        (flight as IDisposable).Dispose();
        var ended = (NetworkPlayerInput.LaunchState)field.GetValue(input);
        Assert.IsFalse(ended.active); Assert.AreEqual(state.sequence, ended.sequence); Assert.Greater(ended.revision, state.revision);
    }
    [UnityEngine.TestTools.UnityTest]
    public System.Collections.IEnumerator LaunchBattleSpringDisableRestoresControlAtCurrentPositionAndPreservesPriorProtection()
    {
        yield return new UnityEngine.TestTools.EnterPlayMode();
        yield return UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("TitleScreen");
        var oldMode = NetSync.Mode; NetSync.Mode = NetSync.NetMode.Offline;
        try
        {
            foreach (bool componentOnly in new[] { true, false })
            {
                var actor = new GameObject("BattleSpringLifetimeActor"); actor.SetActive(false);
                var mechanism = new GameObject("BattleSpringLifetimeMechanism");
                var grid = new GameObject("BattleSpringLifetimeGrid", typeof(Grid));
                var map = new GameObject("Ground", typeof(UnityEngine.Tilemaps.Tilemap)); map.transform.SetParent(grid.transform, false);
                try
                {
                    grid.transform.position = new Vector3(99.5f, 99.5f, 0); actor.transform.position = new Vector3(100, 100, 0);
                    var collider = actor.AddComponent<BoxCollider2D>(); var mover = actor.AddComponent<MovementController>();
                    actor.GetComponent<Rigidbody2D>().gravityScale = 0;
                    actor.SetActive(true); actor.tag = "Player"; mover.enabled = false;
                    var bombs = actor.GetComponent<BombController>(); bombs.enabled = true;
                    var health = actor.GetComponent<CharacterHealth>(); if (health == null) health = actor.AddComponent<CharacterHealth>();
                    health.SetExternalInvulnerability(true);
                    var spring = mechanism.AddComponent<BattleMode4SpringTileController>();
                    const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                    typeof(BattleMode4SpringTileController).GetField("groundTilemap", flags).SetValue(spring, map.GetComponent<UnityEngine.Tilemaps.Tilemap>());
                    typeof(BattleMode4SpringTileController).GetField("jumpSeconds", flags).SetValue(spring, 10f);
                    var routine = (System.Collections.IEnumerator)typeof(BattleMode4SpringTileController).GetMethod("SpringJumpRoutine", flags)
                        .Invoke(spring, new object[] { mover, Vector3Int.zero, new Vector3Int(3, 0) });
                    var active = (System.Collections.IDictionary)typeof(BattleMode4SpringTileController).GetField("jumpRoutines", flags).GetValue(spring);
                    active.Add(mover, routine); spring.StartCoroutine(routine);
                    Assert.IsTrue(mover.InputLocked); Assert.IsFalse(collider.enabled);
                    yield return null;
                    Vector2 before = mover.Rigidbody.position;
                    if (componentOnly) spring.enabled = false; else mechanism.SetActive(false);
                    Assert.AreEqual(before, mover.Rigidbody.position, "Cancellation cannot finish an uncompleted trajectory.");
                    Assert.IsFalse(mover.InputLocked); Assert.IsFalse(mover.ExternalMovementOverride); Assert.IsFalse(mover.VisualOverrideActive);
                    Assert.IsTrue(collider.enabled); Assert.IsTrue(bombs.enabled); Assert.IsTrue(health.HasExternalInvulnerability);
                    Assert.IsFalse(mover.explosionInvulnerable); Assert.AreEqual(0, active.Count);
                }
                finally { Object.DestroyImmediate(mechanism); Object.DestroyImmediate(actor); Object.DestroyImmediate(grid); }
            }
        }
        finally { NetSync.Mode = oldMode; }
        yield return new UnityEngine.TestTools.ExitPlayMode();
    }

}

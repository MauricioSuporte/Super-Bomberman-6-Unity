using System;
using System.Collections;
using System.Reflection;
using Assets.Scripts.Netcode;
using Mirror;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed partial class OnlineRoomEditModeTests
{
    [TestCase(true)] [TestCase(false)]
    public void MinecartPassengerUsesTheSameCornerAsTheCartWithoutGrantingBoarding(bool owner)
    {
        using var f = new LaunchClientFixture(owner);
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene(); scene.name = "BattleMode_9";
        var cartObject = new GameObject("CartAttachment"); SceneManager.MoveGameObjectToScene(cartObject, scene);
        var cart = cartObject.AddComponent<BattleMode9MinecartController>();
        var flags = LaunchClientFixture.Private;
        try
        {
            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject("CartBranch"); go.transform.SetParent(cartObject.transform, false); go.AddComponent<SpriteRenderer>();
                var animation = go.AddComponent<AnimatedSpriteRenderer>();
                typeof(BattleMode9MinecartController).GetField(new[] { "down", "up", "left", "right" }[i], flags).SetValue(cart, animation);
            }
            typeof(NetworkIdentity).GetProperty("netId").SetValue(f.identity, 778u); NetworkClient.spawned.Add(778, f.identity);
            var from = new MinecartView { scene = scene.name, path = cart.NetworkCartPath, generation = 1, phase = MinecartPhase.Rail,
                revision = 1, time = 10, rider = 778, position = Vector3.zero, scale = Vector3.one,
                direction = Vector2.right, riderOffset = Vector3.up * .5f, frame = new NetworkWorldMount.MountFrame { visible = true, color = Color.white } };
            var to = from; to.revision = 2; to.time = 10.1; to.position = new Vector3(1, 1); to.direction = Vector2.up;
            cart.ReceiveNetworkCart(from); cart.ReceiveNetworkCart(to);
            Assert.IsTrue(f.input.ApprovedCartActive, "Claim the animation clock before its history can be pruned by server-now.");
            Assert.AreEqual(Vector2.zero, f.body.position, "Receipt does not prematurely apply the retained trajectory.");
            var history = (System.Collections.Generic.List<MinecartView>)typeof(BattleMode9MinecartController).GetField("networkCartHistory", flags).GetValue(cart);
            double mid = history[0].time + .05;
            var present = typeof(BattleMode9MinecartController).GetMethod("PresentNetworkCart", flags);
            present.Invoke(cart, new object[] { mid });
            Assert.That(Vector3.Distance(new Vector3(1, 0), cartObject.transform.position), Is.LessThan(.001f));
            Assert.That(Vector2.Distance(new Vector2(1, .5f), f.body.position), Is.LessThan(.001f));
            Assert.IsTrue(f.input.ApprovedCartActive); Assert.AreEqual("cart_transport", f.movement.NetworkPredictionBlockReason);
            Assert.IsTrue(f.collider.enabled); Assert.IsFalse(f.movement.InputLocked); Assert.IsFalse(f.movement.explosionInvulnerable);
            if (owner)
            {
                f.input.ReceiveMovementState(1, new MovementController.NetworkMovementState { position = new(9, 9), worldSpeed = 4, canPredict = true }, default, default);
                Assert.IsFalse(f.movement.PredictLocally, "A free-motor ack cannot release the still-presented ride.");
                present.Invoke(cart, new object[] { mid });
                Assert.That(Vector2.Distance(new Vector2(1, .5f), f.body.position), Is.LessThan(.001f));
            }
            var paused = typeof(GamePauseController).GetProperty(nameof(GamePauseController.IsPaused)); bool oldPause = GamePauseController.IsPaused;
            try
            {
                paused.SetValue(null, true); f.body.position = new Vector2(40, 40);
                present.Invoke(cart, new object[] { mid + 20 });
                Assert.That(Vector2.Distance(new Vector2(1, .5f), f.body.position), Is.LessThan(.001f));
            }
            finally { paused.SetValue(null, oldPause); }
            var hidden = to; hidden.revision = 3; hidden.time = 10.2; hidden.phase = MinecartPhase.PortalHidden; hidden.position = new Vector3(20, 20);
            cart.ReceiveNetworkCart(hidden); present.Invoke(cart, new object[] { history[^1].time });
            Assert.That(Vector2.Distance(new Vector2(1, .5f), f.body.position), Is.LessThan(.001f), "Hidden portal transit never draws a journey across the map.");
            var idle = hidden; idle.revision = 4; idle.time = 10.3; idle.rider = 0; idle.phase = MinecartPhase.Idle;
            cart.ReceiveNetworkCart(idle); present.Invoke(cart, new object[] { history[^1].time });
            Assert.IsFalse(f.input.ApprovedCartActive); Assert.AreNotEqual("cart_transport", f.movement.NetworkPredictionBlockReason);
            if (owner) Assert.That(Vector2.Distance(new Vector2(9, 9), f.body.position), Is.LessThan(.001f));
        }
        finally { NetworkClient.spawned.Remove(778); Object.DestroyImmediate(cartObject); UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
    }

    [TestCase(true)] [TestCase(false)]
    public void MinecartLandingBounceAndStopWaitForApprovedExit(bool owner)
    {
        using var f = new LaunchClientFixture(owner);
        var cartHost = new GameObject("CartLanding");
        try
        {
            var cart = cartHost.AddComponent<BattleMode9MinecartController>();
            f.actor.SetActive(false); var fx = f.actor.AddComponent<NetworkBombFx>();
            typeof(NetworkIdentity).GetMethod("InitializeNetworkBehaviours", LaunchClientFixture.Private).Invoke(f.identity, null);
            f.actor.SetActive(true);
            Assert.IsTrue(f.input.PresentApprovedCart(cart, new Vector3(6, 3)));
            var bounce = new NetworkBombFx.PlayerBounceMotion { revision = 1, first = true, moving = true, start = new(6, 3), end = new(7, 3), duration = .2f, curveDuration = .2f, height = .5f };
            fx.ReceivePlayerBounce(bounce); Assert.IsFalse(fx.PlayerBounceActive); Assert.IsTrue(f.input.ApprovedCartActive);
            bounce.revision = 2; bounce.first = false; bounce.moving = false; fx.ReceivePlayerBounce(bounce);
            Assert.IsTrue(f.collider.enabled, "A queued recovery bounce cannot take the cart's presentation early.");
            f.input.ReleaseApprovedCart(cart);
            Assert.IsTrue(fx.PlayerBounceActive); Assert.IsFalse(f.input.ApprovedCartActive);
            typeof(NetworkBombFx).GetMethod("UpdatePlayerBouncePresentation", LaunchClientFixture.Private).Invoke(fx, new object[] { .25f, .25f });
            Assert.IsFalse(fx.PlayerBounceActive); Assert.AreEqual(new Vector2(7, 3), f.body.position); Assert.IsTrue(f.collider.enabled);
        }
        finally { Object.DestroyImmediate(cartHost); }
    }

    [Test]
    public void MinecartCoopBodyFreezesWithGameplayEvenWithoutARigidbody()
    {
        var host = new GameObject("CartPauseClassification"); var managerHost = new GameObject("CartPauseManager");
        try
        {
            host.AddComponent<BattleMode9MinecartController>();
            var child = new GameObject("CartBody"); child.transform.SetParent(host.transform, false);
            var body = child.AddComponent<SpriteRenderer>();
            var manager = managerHost.AddComponent<BombermanNetworkManager>();
            var method = typeof(BombermanNetworkManager).GetMethod("CoopPauseWithGameplay", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNull(body.GetComponentInParent<Rigidbody2D>(true));
            Assert.IsTrue((bool)method.Invoke(manager, new object[] { body }), "Cart frames and animation must pause along with their approved rider.");
        }
        finally { Object.DestroyImmediate(host); Object.DestroyImmediate(managerHost); }
    }

    [Test]
    public void MinecartBufferedClockRetainsShortBoardingAfterDeliveryOrRenderStall()
    {
        using var f = new LaunchClientFixture(false);
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene(); scene.name = "BattleMode_9";
        var host = new GameObject("CartBufferedClock"); SceneManager.MoveGameObjectToScene(host, scene);
        var cart = host.AddComponent<BattleMode9MinecartController>();
        var paused = typeof(GamePauseController).GetProperty(nameof(GamePauseController.IsPaused)); bool old = GamePauseController.IsPaused;
        try
        {
            var view = new MinecartView { scene = scene.name, path = cart.NetworkCartPath, time = 10, generation = 1, revision = 1, phase = MinecartPhase.Enter };
            cart.ReceiveNetworkCart(view); view.revision++; view.time = 10.2; view.phase = MinecartPhase.Rail; cart.ReceiveNetworkCart(view);
            Assert.AreEqual(10, cart.AdvanceCartPresentationTime(10.5, .5f), .00001, "Start the retained approved prefix in the presentation frame.");
            Assert.AreEqual(10.05, cart.AdvanceCartPresentationTime(10.5, .5f), .00001, "A stalled frame cannot consume the entire short phase.");
            paused.SetValue(null, true); Assert.AreEqual(10.05, cart.AdvanceCartPresentationTime(11, 1), .00001);
            paused.SetValue(null, false); Assert.AreEqual(10.1, cart.AdvanceCartPresentationTime(11, .05f), .00001);
            Assert.AreEqual(10.1, cart.AdvanceCartPresentationTime(9, .05f), .00001, "A network timeline correction cannot rewind an approved phase.");
            for (int i = 0; i < 20; i++) cart.AdvanceCartPresentationTime(20, .05f);
            Assert.AreEqual(10.2, cart.AdvanceCartPresentationTime(20, .05f), .00001, "Without new approvals, body and pose must hold the last approved time.");
        }
        finally { paused.SetValue(null, old); Object.DestroyImmediate(host); UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
    }

    [TestCase(MinecartPhase.Enter, .05, false)] [TestCase(MinecartPhase.Rail, .1, false)] [TestCase(MinecartPhase.Jump, .1, false)] [TestCase(MinecartPhase.Rail, .06, true)]
    public void MinecartLongTransportDoesNotRunAtHalfSpeedAtTenFramesPerSecond(MinecartPhase phase, double step, bool boundary)
    {
        using var f = new LaunchClientFixture(false);
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene(); scene.name = "BattleMode_9";
        var host = new GameObject("CartLowFps"); SceneManager.MoveGameObjectToScene(host, scene);
        var cart = host.AddComponent<BattleMode9MinecartController>();
        try
        {
            var view = new MinecartView { scene = scene.name, path = cart.NetworkCartPath, time = 10, generation = 1, revision = 1, phase = phase };
            cart.ReceiveNetworkCart(view);
            if (boundary) { view.time = 10.06; view.phase = MinecartPhase.PortalSink; view.revision++; cart.ReceiveNetworkCart(view); }
            view.time = 10.5; view.revision++; cart.ReceiveNetworkCart(view);
            Assert.AreEqual(10, cart.AdvanceCartPresentationTime(10.5, .1f));
            Assert.AreEqual(10 + step, cart.AdvanceCartPresentationTime(10.5, .1f), .00001);
        }
        finally { Object.DestroyImmediate(host); UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
    }

    [Test]
    public void MinecartResumeSkipsOnlyTheConfirmedPauseInterval()
    {
        using var f = new LaunchClientFixture(false);
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene(); scene.name = "BattleMode_9";
        var host = new GameObject("CartLongPause"); SceneManager.MoveGameObjectToScene(host, scene);
        var cart = host.AddComponent<BattleMode9MinecartController>();
        try
        {
            var view = new MinecartView { scene = scene.name, path = cart.NetworkCartPath, time = 10, generation = 1, revision = 1, phase = MinecartPhase.Rail, paused = true };
            cart.ReceiveNetworkCart(view);
            Assert.AreEqual(10, cart.AdvanceCartPresentationTime(10, .05f));
            view.time = 50; view.revision++; cart.ReceiveNetworkCart(view);
            Assert.AreEqual(10.05, cart.AdvanceCartPresentationTime(50, .05f), .00001, "Without a confirmed resume, do not invent travel.");
            view.time = 50.1; view.revision++; view.paused = false; cart.ReceiveNetworkCart(view);
            Assert.AreEqual(50.1, cart.AdvanceCartPresentationTime(50.1, .05f), .00001, "Do not replay forty seconds of frozen transport after resume.");
        }
        finally { Object.DestroyImmediate(host); UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
    }

    [TestCase(MinecartPhase.PortalHidden)] [TestCase(MinecartPhase.PortalRise)] [TestCase(MinecartPhase.Exit)]
    public void MinecartPhaseAndGenerationCutsDoNotInterpolateThroughAPortal(MinecartPhase phase)
    {
        var from = new MinecartView { time = 10, generation = 1, rider = 1, phase = MinecartPhase.Rail };
        var to = from; to.time = 10.1; to.position = new Vector3(1, 0); to.phase = phase;
        Assert.AreEqual(Vector3.zero, BattleMode9MinecartController.SampleNetworkCartPosition(from, to, 10.05, 1));
        Assert.AreEqual(to.position, BattleMode9MinecartController.SampleNetworkCartPosition(from, to, 10.1, 1));
        to.phase = from.phase; to.generation = 2;
        Assert.AreEqual(Vector3.zero, BattleMode9MinecartController.SampleNetworkCartPosition(from, to, 10.05, 1));
        to = from; to.position = new Vector3(float.NaN, 0); Assert.IsFalse(BattleMode9MinecartController.ValidCartView(to));
        to = from; to.progress = 2; Assert.IsFalse(BattleMode9MinecartController.ValidCartView(to));
    }

    [UnityTest]
    public IEnumerator MinecartNativeCancellationDisposesNestedRideAndRestoresExistingProtection()
    {
        yield return new EnterPlayMode(); yield return SceneManager.LoadSceneAsync("TitleScreen");
        var oldMode = NetSync.Mode; NetSync.Mode = NetSync.NetMode.Offline;
        Scene original = SceneManager.GetActiveScene();
        var scene = SceneManager.CreateScene("BattleMode_9"); SceneManager.SetActiveScene(scene);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        try
        {
            foreach (bool duringRail in new[] { false, true })
            {
                var actor = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab"));
                actor.transform.position = new Vector3(1000, 1000); var mover = actor.GetComponent<MovementController>(); mover.enabled = false;
                var health = actor.GetComponent<CharacterHealth>(); health.SetExternalInvulnerability(true);
                var host = new GameObject("NativeCartLifetime"); var cart = host.AddComponent<BattleMode9MinecartController>();
                try
                {
                    typeof(BattleMode9MinecartController).GetField("enterAnimationSeconds", flags).SetValue(cart, duringRail ? .01f : 10f);
                    typeof(BattleMode9MinecartController).GetField("stage9TravelSeconds", flags).SetValue(cart, 100f);
                    var visualRoot = (Transform)typeof(BattleMode9MinecartController).GetField("cartVisualRoot", flags).GetValue(cart);
                    Assert.IsNotNull(visualRoot);
                    var unusedBranch = new GameObject("InactiveCartBranch"); unusedBranch.transform.SetParent(visualRoot, false);
                    unusedBranch.AddComponent<SpriteRenderer>().enabled = false; unusedBranch.transform.localScale = new Vector3(2, 3, 1);
                    var method = typeof(BattleMode9MinecartController).GetMethod("RideRoutine", flags);
                    cart.StartCoroutine((IEnumerator)method.Invoke(cart, new object[] { mover }));
                    float deadline = Time.realtimeSinceStartup + 3;
                    while (Time.realtimeSinceStartup < deadline && (!mover.InputLocked || (duringRail && (MinecartPhase)typeof(BattleMode9MinecartController).GetField("networkRidePhase", flags).GetValue(cart) != MinecartPhase.Rail))) yield return null;
                    Assert.IsTrue(mover.InputLocked); Assert.IsTrue(cart.RideActive);
                    Vector2 shown = mover.transform.position;
                    unusedBranch.transform.localScale = Vector3.zero;
                    if (duringRail) host.SetActive(false); else cart.enabled = false;
                    Assert.IsFalse(mover.InputLocked); Assert.IsFalse(mover.ExternalMovementOverride); Assert.IsFalse(mover.VisualOverrideActive);
                    Assert.AreEqual(new Vector3(2, 3, 1), unusedBranch.transform.localScale, "Cancellation restores even a branch inactive at boarding.");
                    Assert.IsTrue(health.HasExternalInvulnerability, "Cleanup must not erase protection from another source.");
                    Assert.IsTrue(actor.GetComponent<Collider2D>().enabled); Assert.IsFalse(cart.RideActive);
                    yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
                    Assert.That(Vector2.Distance(shown, mover.transform.position), Is.LessThan(.001f), "A disabled cart must not continue its nested motion.");
                    if (duringRail) host.SetActive(true); else cart.enabled = true;
                    cart.StartCoroutine((IEnumerator)method.Invoke(cart, new object[] { mover })); yield return null;
                    Assert.IsTrue(cart.RideActive, "The cancelled controller remains reusable."); host.SetActive(false);
                }
                finally { Object.DestroyImmediate(host); Object.DestroyImmediate(actor); }
            }
        }
        finally { NetSync.Mode = oldMode; SceneManager.SetActiveScene(original); }
        yield return SceneManager.UnloadSceneAsync(scene); yield return new ExitPlayMode();
    }
}

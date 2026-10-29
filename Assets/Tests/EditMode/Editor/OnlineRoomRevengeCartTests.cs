using System;
using System.Reflection;
using Assets.Scripts.Netcode;
using Mirror;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public sealed partial class OnlineRoomEditModeTests
{
    const BindingFlags RevengeFields = BindingFlags.Instance | BindingFlags.NonPublic;

    [Test]
    public void ApprovedKickProgressRetainsForwardPresentationWithoutPredictingAnotherCell()
    {
        Assert.AreEqual(.08f, NetworkBombFx.PreserveApprovedKickProgress(new Vector2(.8f, 0), Vector2.zero, Vector2.right, .1f, .02f), .0001f);
        Assert.AreEqual(.1f, NetworkBombFx.PreserveApprovedKickProgress(Vector2.right * 3, Vector2.zero, Vector2.right, .1f, .02f), .0001f);
        Assert.AreEqual(.17f, NetworkBombFx.PreserveApprovedKickProgress(Vector2.right, Vector2.zero, Vector2.right, 1, .02f), .0001f);
        Assert.AreEqual(.07f, NetworkBombFx.PreserveApprovedKickProgress(Vector2.left, Vector2.zero, Vector2.right, .1f, .07f), .0001f);
        Assert.AreEqual(.2f, NetworkBombFx.PreserveApprovedKickProgress(Vector2.zero, Vector2.zero, Vector2.right, .1f, .2f), .0001f);
        Assert.AreEqual(.08f, NetworkBombFx.PreserveApprovedKickProgress(new Vector2(0, -.8f), Vector2.zero, Vector2.down, .1f, .02f), .0001f);
    }

    [TestCase(PlayerAction.ActionA, 16)]
    [TestCase(PlayerAction.ActionC, 64)]
    [TestCase(PlayerAction.MoveUp, 1)]
    [TestCase(PlayerAction.MoveDown, 2)]
    [TestCase(PlayerAction.MoveLeft, 4)]
    [TestCase(PlayerAction.MoveRight, 8)]
    [TestCase(PlayerAction.Start, 0)]
    [TestCase(PlayerAction.ActionB, 0)]
    public void RevengeCartHardwareActionsUseTheCartProtocolMask(PlayerAction action, int expected)
    {
        Assert.AreEqual(expected, BattleRevengeController.NetworkCartActionMask(action));
    }

    [Test]
    public void RevengePacketsCannotBuySimulationTimeAndOverflowCancelsInsteadOfReleasingA()
    {
        var queue = new RevengeCartInputBuffer();
        for (uint i = 1; i <= 8; i++) Assert.IsTrue(queue.Push(i, 16 | 8 | 512, 1));
        Assert.IsFalse(queue.Consume(out _), "Packets create no simulation credits.");
        queue.Advance(); Assert.IsTrue(queue.Consume(out int held)); Assert.AreEqual(24, held);
        Assert.IsFalse(queue.Consume(out _));
        Assert.IsFalse(queue.Push(8, 0, 1)); Assert.IsFalse(queue.Overflowed);
        Assert.IsTrue(queue.Push(9, 16, 1));
        Assert.IsFalse(queue.Push(10, 0, 1)); Assert.IsTrue(queue.Overflowed);
        Assert.AreEqual(0, queue.Count); Assert.AreEqual(0, queue.Held); Assert.AreEqual(10u, queue.Acknowledged);
        Assert.IsFalse(queue.Push(9, 0, 2)); Assert.IsFalse(queue.Overflowed);
        Assert.IsTrue(queue.Push(11, 0, 2)); queue.Advance(); Assert.IsTrue(queue.Consume(out held)); Assert.AreEqual(0, held);
        queue.Push(12, 16, 2); queue.Advance(); Assert.IsTrue(queue.Consume(out held));
        queue.Advance(); queue.Advance(); queue.Pause(); Assert.AreEqual(16, queue.Held);
        Assert.IsTrue(queue.Push(13, 16, 3)); Assert.IsFalse(queue.Consume(out _), "Paused time cannot fund a movement burst after resume.");
        queue.Advance(); Assert.IsTrue(queue.Consume(out held)); Assert.AreEqual(16, held);
    }

    [Test]
    public void RevengeInputPreservesQuickTapOrderAndUnsignedWrap()
    {
        var queue = new RevengeCartInputBuffer();
        Assert.IsTrue(queue.Push(uint.MaxValue, 16, 1)); Assert.IsTrue(queue.Push(0, 0, 1));
        queue.Heartbeat(2); queue.Advance(); Assert.IsTrue(queue.Consume(out int held)); Assert.AreEqual(16, held);
        queue.Advance(); Assert.IsTrue(queue.Consume(out held)); Assert.AreEqual(0, held);
        Assert.AreEqual(0u, queue.Acknowledged); Assert.AreEqual(2, queue.HeardAt);
        Assert.IsFalse(queue.Push(uint.MaxValue, 16, 3)); Assert.IsFalse(queue.Push(1, 16, double.NaN));
    }

    [TestCase("valid", true)]
    [TestCase("owner", false)]
    [TestCase("identity", false)]
    [TestCase("auth", false)]
    [TestCase("scene", false)]
    [TestCase("generation", false)]
    [TestCase("cart", false)]
    public void RevengeCartInputIsBoundToConnectionSlotIdentitySceneAndGeneration(string change, bool expected)
    {
        var state = new RevengeCartState { id = 43, owner = 4, generation = 8 };
        var input = new RevengeCartInput { scene = "BattleMode_1", cart = 43, generation = 8, tick = 1, held = 8 };
        if (change == "scene") input.scene = "BattleMode_2";
        if (change == "generation") input.generation--;
        if (change == "cart") input.cart++;
        Assert.AreEqual(expected, BombermanNetworkManager.MatchesRevengeCartInput(input, state,
            change == "owner" ? 1 : 4, "BattleMode_1", change != "auth", change != "identity"));
    }

    sealed class RevengeCartFixture : IDisposable
    {
        public readonly Scene scene;
        public readonly BattleRevengeController cart;
        public RevengeCartFixture()
        {
            scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/StageAssets/MadBomberCart.prefab");
            var go = Object.Instantiate(prefab); SceneManager.MoveGameObjectToScene(go, scene);
            go.SetActive(false); cart = go.GetComponent<BattleRevengeController>();
            cart.InitializeNetworkView(null, null);
            cart.ConfigureBounds(-10, 5, -7, 5);
            go.SetActive(true);
        }
        public void Dispose() { Object.DestroyImmediate(cart.gameObject); UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
    }

    [TestCase(1)] [TestCase(2)] [TestCase(4)] [TestCase(8)]
    [TestCase(9)] [TestCase(6)] [TestCase(3)] [TestCase(12)] [TestCase(72)]
    public void RevengePredictedMotorMatchesNativePerimeterIncludingCornersAndBoostWithoutActions(int held)
    {
        var oldMode = NetSync.Mode; var random = UnityEngine.Random.state;
        using var fixture = new RevengeCartFixture(); var cart = fixture.cart;
        try
        {
            NetSync.Mode = NetSync.NetMode.Client;
            float length = (float)typeof(BattleRevengeController).GetMethod("GetPerimeterLength", RevengeFields).Invoke(cart, null);
            foreach (float initial in new[] { 0f, .03f, 13.4f, 25.9f, length - .03f })
            {
                cart.SetNetworkPerimeter(initial);
                for (int i = 0; i < 30; i++)
                {
                    var position = cart.transform.position; float before = cart.NetworkPerimeter;
                    float predicted = cart.PredictNetworkPerimeter(before, held, .02f);
                    Assert.AreEqual(position, cart.transform.position); Assert.AreEqual(before, cart.NetworkPerimeter);
                    cart.StepNetworkMotor(held, .02f);
                    Assert.AreEqual(predicted, cart.NetworkPerimeter, .0001f);
                }
            }
            Assert.IsFalse((bool)typeof(BattleRevengeController).GetField("isChargingLaunch", RevengeFields).GetValue(cart));
            Assert.AreEqual(0u, cart.NetworkLaunchSerial); Assert.AreEqual(random, UnityEngine.Random.state);
            Assert.Greater(length, 40);
        }
        finally { NetSync.Mode = oldMode; UnityEngine.Random.state = random; }
    }

    [Test]
    public void RevengeReplayRetainsHistoricalStepDurationAndDoesNotRepeatCharge()
    {
        using var fixture = new RevengeCartFixture(); var cart = fixture.cart;
        cart.SetNetworkPerimeter(2);
        var history = new RevengeCartPredictionHistory();
        float expected = cart.NetworkPerimeter;
        history.Add(uint.MaxValue, 8, .02f); expected = cart.PredictNetworkPerimeter(expected, 8, .02f);
        float acknowledged = expected;
        history.Add(0, 72, .04f); expected = cart.PredictNetworkPerimeter(expected, 72, .04f);
        history.Add(1, 1, .01f); expected = cart.PredictNetworkPerimeter(expected, 1, .01f);
        history.DiscardThrough(uint.MaxValue); Assert.AreEqual(2, history.Count);
        float replay = acknowledged;
        for (int i = 0; i < history.Count; i++) replay = cart.PredictNetworkPerimeter(replay, history[i].held, history[i].dt);
        Assert.AreEqual(expected, replay, .0001f); Assert.AreEqual(2, cart.NetworkPerimeter);
        for (uint i = 2; i < 150; i++) history.Add(i, 8, .02f);
        Assert.AreEqual(NetworkInputHistory.Capacity, history.Count);
        history.DiscardThrough(149); Assert.AreEqual(0, history.Count);
    }

    [Test]
    public void RevengeApprovedCartPoseRoundTripsAndContainsOnlyPresentation()
    {
        using var fixture = new RevengeCartFixture(); var cart = fixture.cart;
        cart.SetNetworkPerimeter(3);
        var indicator = (SpriteRenderer)typeof(BattleRevengeController).GetField("landingIndicatorRenderer", RevengeFields).GetValue(cart);
        Assert.IsNotNull(indicator); indicator.enabled = true;
        var state = cart.CaptureNetworkCart(12, 6); state.owner = 4;
        state.animating = true; state.position = new Vector3(20, 21); state.landingPosition = new Vector3(31, 29); state.recoilOffset = Vector3.right * .1f;
        state.generation = uint.MaxValue; Assert.Greater(state.sprites.Length, 0);
        var item = state.sprites[0]; item.sortingOrder = 77; item.frame.color = Color.magenta;
        state.sprites[0] = item;
        using (var writer = NetworkWriterPool.Get())
        {
            writer.Write(new RevengeCartRoster { scene = "BattleMode_1", revision = 5, time = 1, carts = new[] { state } });
            state = new NetworkReader(writer.ToArray()).Read<RevengeCartRoster>().carts[0];
        }
        Assert.AreEqual(uint.MaxValue, state.generation); Assert.AreEqual(6u, state.acknowledged);
        var next = state; next.position += Vector3.right * 4;
        cart.PresentNetworkCart(state, next, .5f);
        Assert.AreEqual(new Vector3(22, 21), cart.transform.position);
        Assert.AreEqual(state.landingPosition, indicator.transform.position, "Approved target cells must not drift with a predicted cart root.");
        var renderers = cart.GetComponentsInChildren<SpriteRenderer>(true);
        Assert.AreEqual(77, renderers[item.index].sortingOrder); Assert.AreEqual(Color.magenta, renderers[item.index].color);
        var beforeRecoil = renderers[item.index].transform.position;
        cart.PresentPausedNetworkRecoil(Vector3.right * .18f);
        Assert.AreEqual(new Vector3(22, 21), cart.transform.position);
        Assert.AreEqual(state.landingPosition, indicator.transform.position);
        if (renderers[item.index] != indicator)
            Assert.AreEqual(beforeRecoil.x + .08f, renderers[item.index].transform.position.x, .0001f);
        Assert.AreEqual(Color.magenta, renderers[item.index].color);
        foreach (var behaviour in cart.GetComponentsInChildren<MonoBehaviour>(true))
            if (behaviour is AnimatedSpriteRenderer animation)
                Assert.IsTrue((bool)typeof(AnimatedSpriteRenderer).GetField("manualAnimationUpdate", RevengeFields).GetValue(animation));
            else if (behaviour != cart) Assert.IsFalse(behaviour.enabled);
        foreach (var collider in cart.GetComponentsInChildren<Collider2D>(true)) Assert.IsFalse(collider.enabled);
        Assert.AreEqual(0, cart.GetComponentsInChildren<NetworkIdentity>(true).Length);
        Assert.AreEqual(0u, cart.NetworkLaunchSerial);
    }

    [TestCase(NetSync.NetMode.Offline)]
    [TestCase(NetSync.NetMode.Host)]
    [TestCase(NetSync.NetMode.Client)]
    public void UnspawnedPlayerCannotReactivateFromRevengePresentation(NetSync.NetMode mode)
    {
        var old = NetSync.Mode; var actor = new GameObject("UnspawnedRevenge"); actor.SetActive(false);
        try
        {
            NetSync.Mode = mode; var state = actor.AddComponent<NetworkPlayerState>();
            state.ReceiveConfirmedRevengeReturn(new RevengeReturnState { revision = 1, jumping = true });
            state.ReceiveConfirmedRevengeReturn(new RevengeReturnState { revision = 2, life = 1, bombs = 8 });
            Assert.IsFalse(actor.activeSelf); Assert.IsFalse(state.HasApprovedRevengeReturn);
        }
        finally { Object.DestroyImmediate(actor); NetSync.Mode = old; }
    }

    [Test]
    public void EliminatedPlayerPresentsOnlyConfirmedRevengeRidingArcWithoutReviving()
    {
        var old = NetSync.Mode; var actor = new GameObject("RevengeRidingApproval");
        actor.SetActive(false); actor.AddComponent<BoxCollider2D>();
        try
        {
            NetSync.Mode = NetSync.NetMode.Client;
            var identity = actor.AddComponent<NetworkIdentity>(); var state = actor.AddComponent<NetworkPlayerState>();
            var fx = actor.AddComponent<NetworkBombFx>(); var movement = actor.GetComponent<MovementController>();
            var health = actor.GetComponent<CharacterHealth>(); var body = actor.GetComponent<Rigidbody2D>();
            var riding = actor.AddComponent<PlayerRidingController>();
            typeof(NetworkIdentity).GetMethod("InitializeNetworkBehaviours", RevengeFields).Invoke(identity, null);
            typeof(NetworkIdentity).GetProperty("netId").SetValue(identity, 124u);
            typeof(NetworkIdentity).GetProperty("isClient").SetValue(identity, true);
            typeof(NetworkPlayerState).GetField("move", RevengeFields).SetValue(state, movement);
            typeof(NetworkPlayerState).GetField("health", RevengeFields).SetValue(state, health);
            typeof(MovementController).GetProperty(nameof(MovementController.Rigidbody)).SetValue(movement, body);
            movement.isDead = true; health.life = 0;
            var begin = typeof(NetworkBombFx).GetMethod("BeginRidingPresentation", RevengeFields);
            var arc = new NetworkBombFx.RidingMotion { active = true, revision = 1, serverTime = NetworkTime.time,
                arc = new PlayerRidingController.NetworkArcState { start = Vector3.zero, target = Vector3.right,
                    duration = 1, facing = Vector2.right } };
            Assert.IsFalse((bool)begin.Invoke(fx, new object[] { arc, true }));
            state.ReceiveConfirmedRevengeReturn(new RevengeReturnState { revision = 1, jumping = true });
            Assert.IsFalse((bool)begin.Invoke(fx, new object[] { arc, false }), "A dead player cannot invent a gesture without a confirmed arc.");
            Assert.IsTrue((bool)begin.Invoke(fx, new object[] { arc, true }));
            typeof(NetworkBombFx).GetMethod("UpdateRidingPresentation", RevengeFields).Invoke(fx, new object[] { .05f, .05f });
            Assert.IsTrue(fx.RidingArcActive);
            Assert.IsTrue((bool)typeof(PlayerRidingController).GetField("networkArcVisual", RevengeFields).GetValue(riding));
            Assert.IsTrue(movement.isDead); Assert.AreEqual(0, health.life); Assert.IsFalse(actor.GetComponent<Collider2D>().enabled);
        }
        finally { Object.DestroyImmediate(actor); NetSync.Mode = old; }
    }

    [Test]
    public void ConfirmedRevengeReturnKeepsPlayerDeadDuringArcThenRestoresOnlyApprovedStats()
    {
        PlayerPersistentStats.EnsureSessionBooted();
        var stageActive = typeof(PlayerPersistentStats).GetField("stageActive", BindingFlags.Static | BindingFlags.NonPublic);
        bool originalStageActive = (bool)stageActive.GetValue(null);
        stageActive.SetValue(null, true); // Keep base and stage runtime distinct without copying unrelated players.
        var copy = typeof(PlayerPersistentStats).GetMethod("CopyState", BindingFlags.Static | BindingFlags.NonPublic);
        var originalBase = new PlayerPersistentStats.PlayerState(); var originalRuntime = new PlayerPersistentStats.PlayerState();
        var baseStats = PlayerPersistentStats.Get(1); var runtime = PlayerPersistentStats.GetRuntime(1);
        copy.Invoke(null, new object[] { baseStats, originalBase }); copy.Invoke(null, new object[] { runtime, originalRuntime });
        var oldMode = NetSync.Mode;
        var actor = new GameObject("ConfirmedRevengeReturn"); actor.SetActive(false); actor.AddComponent<BoxCollider2D>();
        try
        {
            NetSync.Mode = NetSync.NetMode.Client;
            var identity = actor.AddComponent<NetworkIdentity>(); var input = actor.AddComponent<NetworkPlayerInput>();
            var state = actor.AddComponent<NetworkPlayerState>(); var movement = actor.AddComponent<MovementController>();
            var health = actor.GetComponent<CharacterHealth>() ?? actor.AddComponent<CharacterHealth>();
            var bomb = actor.AddComponent<BombController>();
            typeof(MovementController).GetField("bombController", RevengeFields).SetValue(movement, bomb);
            typeof(NetworkPlayerState).GetField("bomb", RevengeFields).SetValue(state, bomb);
            var body = actor.GetComponent<Rigidbody2D>() ?? actor.AddComponent<Rigidbody2D>();
            typeof(MovementController).GetProperty(nameof(MovementController.Rigidbody)).SetValue(movement, body);
            typeof(NetworkIdentity).GetMethod("InitializeNetworkBehaviours", RevengeFields).Invoke(identity, null);
            typeof(NetworkIdentity).GetProperty("netId").SetValue(identity, 123u);
            typeof(NetworkIdentity).GetProperty("isClient").SetValue(identity, true);
            foreach (var pair in new[] { ("move", (object)movement), ("health", health), ("setup", actor.GetComponent<NetworkPlayerSetup>()) })
                typeof(NetworkPlayerState).GetField(pair.Item1, RevengeFields).SetValue(state, pair.Item2);
            typeof(NetworkPlayerInput).GetMethod("Awake", RevengeFields).Invoke(input, null);
            typeof(CharacterHealth).GetMethod("Awake", RevengeFields).Invoke(health, null);
            movement.isDead = true; health.life = 0; movement.ApplySpeedInternal(PlayerPersistentStats.BaseSpeedNormal + 16); baseStats.BombAmount = 8; runtime.BombAmount = 1;
            var begin = new RevengeReturnState { revision = 1, inputAck = 40, jumping = true, position = new Vector2(20, 20) };
            using (var writer = NetworkWriterPool.Get()) { writer.Write(begin); begin = new NetworkReader(writer.ToArray()).Read<RevengeReturnState>(); }
            state.ReceiveConfirmedRevengeReturn(begin);
            Assert.IsTrue(actor.activeSelf); Assert.IsTrue(movement.isDead); Assert.AreEqual(0, health.life);
            Assert.IsTrue(state.HasApprovedRevengeReturn); Assert.IsFalse(actor.GetComponent<Collider2D>().enabled); Assert.IsFalse(body.simulated);
            Assert.AreEqual(40u, input.CurrentInputTick);
            typeof(NetworkPlayerState).GetField("eliminated", RevengeFields).SetValue(state, true);
            typeof(NetworkPlayerState).GetMethod("OnEliminatedChanged", RevengeFields).Invoke(state, new object[] { false, true });
            Assert.IsTrue(actor.activeSelf, "The approved jump stays visible while eliminated remains true.");
            var end = new RevengeReturnState { revision = 2, inputAck = 41, position = new Vector2(22, 20),
                life = 1, bombs = 2, radius = 3, speed = PlayerPersistentStats.BaseSpeedNormal };
            state.ReceiveConfirmedRevengeReturn(end);
            Assert.IsFalse(state.HasApprovedRevengeReturn); Assert.IsFalse(movement.isDead);
            Assert.AreEqual(1, health.life); Assert.AreEqual(2, runtime.BombAmount); Assert.AreEqual(2, bomb.bombAmout);
            Assert.AreEqual(end.speed, movement.SpeedInternal, "Confirmed movement speed replaces the pre-death speed before prediction resumes.");
            Assert.AreEqual(8, baseStats.BombAmount, "A confirmed return must not load or edit the machine's base loadout.");
            Assert.AreEqual((Vector3)end.position, actor.transform.position);
            Assert.IsTrue(body.simulated); Assert.IsTrue(actor.GetComponent<Collider2D>().enabled);
            Assert.IsFalse(movement.PredictLocally); Assert.AreEqual(0, input.PendingInputCount);
            state.ReceiveConfirmedRevengeReturn(begin); Assert.IsFalse(state.HasApprovedRevengeReturn);
            var rejected = end; rejected.revision = 3; rejected.life = 0; rejected.position = Vector2.zero;
            state.ReceiveConfirmedRevengeReturn(rejected); Assert.AreEqual((Vector3)end.position, actor.transform.position);
        }
        finally
        {
            Object.DestroyImmediate(actor); copy.Invoke(null, new object[] { originalBase, baseStats });
            copy.Invoke(null, new object[] { originalRuntime, runtime }); stageActive.SetValue(null, originalStageActive); NetSync.Mode = oldMode;
        }
    }
}

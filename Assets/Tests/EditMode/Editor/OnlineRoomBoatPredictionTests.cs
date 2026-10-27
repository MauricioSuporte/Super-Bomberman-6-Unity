#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using Assets.Scripts.Netcode;
using Mirror;
using NUnit.Framework;
using UnityEngine;

public sealed partial class OnlineRoomEditModeTests
{
    const BindingFlags BoatFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    [Test]
    public void BoatMotionDiagnosticsUseTheApprovedAttachmentWithoutNativeMounting()
    {
        using var fixture = new OwnerBoatFixture();
        var probe = fixture.fx.gameObject.AddComponent<MotionDiagnosticProbe>();
        typeof(MotionDiagnosticProbe).GetField("movement", BoatFlags).SetValue(probe, fixture.movement);
        fixture.movement.RestoreNetworkMovementState(fixture.state); fixture.fx.ReconcileOwnerBoat(fixture.state);
        Assert.IsTrue(fixture.movement.HasBoatMovementContext); Assert.IsFalse(BoatRideZone.IsRidingBoat(fixture.movement));
        StringAssert.Contains("boat=True", (string)typeof(MotionDiagnosticProbe).GetMethod("State", BoatFlags).Invoke(probe, null));
        var state = fixture.state; state.boat = default;
        fixture.movement.RestoreNetworkMovementState(state); fixture.fx.ReconcileOwnerBoat(state);
        StringAssert.Contains("boat=False", (string)typeof(MotionDiagnosticProbe).GetMethod("State", BoatFlags).Invoke(probe, null));
    }

    [Test]
    public void WalkingSnapshotsDoNotSendAnEmptyBoatDescriptor()
    {
        using var writer = NetworkWriterPool.Get(); writer.Write(default(BoatRideZone.NetworkRideState));
        Assert.AreEqual(1, writer.Position, "Players outside a boat should send only its absence bit.");
        Assert.IsNull(new NetworkReader(writer.ToArray()).Read<BoatRideZone.NetworkRideState>().path);
    }

    [Test]
    public void BoatWaterPassReplayUsesApprovedContextInsteadOfCurrentNativeFlags()
    {
        var oldMode = NetSync.Mode;
        var player = new GameObject("BoatPassReplay"); player.SetActive(false);
        var wall = new GameObject("BoatReplayWater");
        try
        {
            NetSync.Mode = NetSync.NetMode.Client; player.tag = "Player"; player.AddComponent<BoxCollider2D>();
            var body = player.AddComponent<Rigidbody2D>(); body.bodyType = RigidbodyType2D.Kinematic;
            var movement = player.AddComponent<MovementController>();
            typeof(MovementController).GetProperty(nameof(MovementController.Rigidbody)).SetValue(movement, body);
            player.SetActive(true); movement.SetNetworkTickDriven(true); movement.SetPredictLocally(true);
            wall.layer = LayerMask.NameToLayer("Stage"); wall.tag = "Water";
            wall.transform.position = new Vector2(61002, 61000); wall.AddComponent<BoxCollider2D>(); Physics2D.SyncTransforms();
            movement.obstacleMask = LayerMask.GetMask("Stage"); movement.RefreshObstacleContactFilter();
            var state = new MovementController.NetworkMovementState { position = new Vector2(61000, 61000), facing = Vector2.right,
                worldSpeed = 4.1f, animationSpeed = PlayerPersistentStats.BaseSpeedNormal, taggedPass = true, passTag = "Water", canPredict = true };
            using (var writer = NetworkWriterPool.Get()) { writer.Write(state); state = new NetworkReader(writer.ToArray()).Read<MovementController.NetworkMovementState>(); }
            Assert.IsTrue(state.taggedPass); Assert.AreEqual("Water", state.passTag);
            movement.SetPassTaggedObstacles(false, "Water"); movement.RestoreNetworkMovementState(state);
            for (int i = 0; i < 40; i++) movement.SimulateNetworkMovementTick(8, replay: true);
            Assert.Greater(body.position.x, 61002, "Approved water pass must work before any native client boat state exists.");
            Assert.IsTrue(movement.CaptureNetworkMovementState().taggedPass);
            movement.SetPassTaggedObstacles(true, "Water"); state.taggedPass = false; movement.RestoreNetworkMovementState(state);
            for (int i = 0; i < 40; i++) movement.SimulateNetworkMovementTick(8, replay: true);
            Assert.LessOrEqual(body.position.x, 61001, "A newer native permission must not rewrite the acknowledged movement history.");
        }
        finally { UnityEngine.Object.DestroyImmediate(player); UnityEngine.Object.DestroyImmediate(wall); NetSync.Mode = oldMode; }
    }

    [TestCase(NetSync.NetMode.Offline)]
    [TestCase(NetSync.NetMode.Host)]
    public void NativeBoatMountUnmountAndUnspawnedNetworkComponentsKeepOfflineBehavior(NetSync.NetMode mode)
    {
        var oldMode = NetSync.Mode;
        var player = new GameObject("NativeBoatRider"); player.SetActive(false);
        var source = new GameObject("NativeBoat"); source.SetActive(false);
        try
        {
            NetSync.Mode = mode; player.tag = "Player"; player.AddComponent<BoxCollider2D>();
            var movement = player.AddComponent<MovementController>(); var body = player.GetComponent<Rigidbody2D>();
            typeof(MovementController).GetProperty(nameof(MovementController.Rigidbody)).SetValue(movement, body);
            player.AddComponent<NetworkIdentity>(); player.AddComponent<NetworkPlayerInput>();
            var definition = new GameObject("Down", typeof(SpriteRenderer)); definition.transform.SetParent(source.transform, false);
            var down = definition.AddComponent<AnimatedSpriteRenderer>();
            var boat = source.AddComponent<BoatRideZone>(); typeof(BoatRideZone).GetField("down", BoatFlags).SetValue(boat, down);
            typeof(BoatRideZone).GetMethod("Awake", BoatFlags).Invoke(boat, null);
            Assert.IsTrue(boat.TryMount(movement)); Assert.IsTrue(BoatRideZone.IsRidingBoat(movement)); Assert.IsTrue(movement.SuppressInactivityAnimation);
            Assert.IsTrue((bool)typeof(MovementController).GetField("canPassTaggedObstacles", BoatFlags).GetValue(movement));
            var state = BoatRideZone.CaptureNetworkRide(movement);
            Assert.AreEqual(mode == NetSync.NetMode.Host, !string.IsNullOrEmpty(state.path));
            if (mode == NetSync.NetMode.Host) Assert.AreEqual(definition.GetComponent<SpriteRenderer>().GetEntityId().GetHashCode(), state.downId);
            Assert.IsTrue(boat.TryUnmount(movement)); Assert.IsFalse(BoatRideZone.IsRidingBoat(movement)); Assert.IsFalse(movement.SuppressInactivityAnimation);
            Assert.IsFalse((bool)typeof(MovementController).GetField("canPassTaggedObstacles", BoatFlags).GetValue(movement));
        }
        finally
        {
            if (source != null && player != null) source.GetComponent<BoatRideZone>()?.TryUnmount(player.GetComponent<MovementController>());
            UnityEngine.Object.DestroyImmediate(source); UnityEngine.Object.DestroyImmediate(player); NetSync.Mode = oldMode;
        }
    }

    [Test]
    public void ClientBoatNativeEntryPointsAndZonesCannotMountProtectOrSnapPlayer()
    {
        var oldMode = NetSync.Mode;
        var source = new GameObject("ClientNativeBoat"); source.SetActive(false); source.transform.position = new Vector3(20, 30);
        var player = new GameObject("ClientBoatGateRider"); player.SetActive(false);
        var mountZone = new GameObject("ClientBoatMountZone"); mountZone.SetActive(false);
        var unmountZone = new GameObject("ClientBoatUnmountZone"); unmountZone.SetActive(false);
        try
        {
            NetSync.Mode = NetSync.NetMode.Client; player.tag = "Player";
            var collider = player.AddComponent<BoxCollider2D>(); var movement = player.AddComponent<MovementController>();
            var boat = source.AddComponent<BoatRideZone>(); var position = source.transform.position;
            typeof(BoatRideZone).GetMethod("Awake", BoatFlags).Invoke(boat, null);
            Assert.IsFalse(source.GetComponent<BoxCollider2D>().isTrigger, "Client startup must not run native anchoring/physics setup.");
            Assert.IsFalse(boat.CanMount(movement)); Assert.IsFalse(boat.TryMount(movement)); Assert.IsFalse(boat.TryUnmount(movement));
            typeof(BoatRideZone).GetMethod("LateUpdate", BoatFlags).Invoke(boat, null); Assert.AreEqual(position, source.transform.position);
            var enter = mountZone.AddComponent<BoatMountZone>(); var exit = unmountZone.AddComponent<BoatUnmountZone>();
            typeof(BoatMountZone).GetField("boat", BoatFlags).SetValue(enter, boat); typeof(BoatUnmountZone).GetField("boat", BoatFlags).SetValue(exit, boat);
            var before = player.transform.position;
            typeof(BoatMountZone).GetMethod("OnTriggerEnter2D", BoatFlags).Invoke(enter, new object[] { collider });
            typeof(BoatUnmountZone).GetMethod("OnTriggerEnter2D", BoatFlags).Invoke(exit, new object[] { collider });
            Assert.AreEqual(before, player.transform.position); Assert.IsFalse(boat.HasRider); Assert.IsFalse(BoatRideZone.IsRidingBoat(movement));
            Assert.IsFalse((bool)typeof(MovementController).GetField("canPassTaggedObstacles", BoatFlags).GetValue(movement));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(source); UnityEngine.Object.DestroyImmediate(player);
            UnityEngine.Object.DestroyImmediate(mountZone); UnityEngine.Object.DestroyImmediate(unmountZone); NetSync.Mode = oldMode;
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ApprovedOwnerBoatFollowsPredictedRootTurnsImmediatelyPausesAndCleansUp(bool stopClient)
    {
        using var fixture = new OwnerBoatFixture();
        var fx = fixture.fx; var movement = fixture.movement; var body = movement.Rigidbody;
        movement.RestoreNetworkMovementState(fixture.state); movement.SetPredictLocally(true); fx.ReconcileOwnerBoat(fixture.state);
        Assert.IsTrue(fx.OwnerBoatPredictionReady); Assert.IsFalse(fixture.source.HasRider); Assert.IsFalse(BoatRideZone.IsRidingBoat(movement));
        Assert.IsFalse((bool)typeof(MovementController).GetField("canPassTaggedObstacles", BoatFlags).GetValue(movement));
        Assert.IsFalse(movement.ExternalVisualSuppressed, "The view must not take ownership of another ability's native suppression flag.");
        Assert.IsFalse(fixture.remote.enabled);
        fixture.Present();
        var view = (GameObject)typeof(NetworkBombFx).GetField("ownerBoatView", BoatFlags).GetValue(fx);
        Assert.IsEmpty(view.GetComponentsInChildren<Collider2D>(true)); Assert.IsEmpty(view.GetComponentsInChildren<Rigidbody2D>(true));
        Assert.IsEmpty(view.GetComponentsInChildren<NetworkIdentity>(true));
        Assert.AreEqual((Vector3)(body.position + fixture.state.boat.followOffset), view.transform.position);
        Assert.IsTrue(fixture.heads[0].GetComponent<SpriteRenderer>().enabled); Assert.IsFalse(fixture.bodyVisual.GetComponent<SpriteRenderer>().enabled);
        body.position += Vector2.right;
        typeof(MovementController).GetField("direction", BoatFlags).SetValue(movement, Vector2.up);
        typeof(MovementController).GetField("facingDirection", BoatFlags).SetValue(movement, Vector2.up);
        fixture.Present();
        Assert.AreEqual((Vector3)(body.position + fixture.state.boat.followOffset), view.transform.position);
        Assert.IsTrue(fixture.heads[1].GetComponent<SpriteRenderer>().enabled); Assert.IsFalse(fixture.heads[0].GetComponent<SpriteRenderer>().enabled);
        Assert.AreEqual(new Vector3(0.25f, 0.5f), fixture.heads[1].transform.localPosition);
        var pausedPosition = body.position; var pausedView = view.transform.position; int frame = fixture.heads[1].CurrentFrame;
        OwnerBoatFixture.Paused.SetValue(null, true);
        movement.RestoreNetworkMovementState(fixture.state); fx.ReconcileOwnerBoat(fixture.state); fx.RestorePausedBoatPosition(pausedPosition); fixture.Present();
        Assert.AreEqual(pausedPosition, body.position); Assert.AreEqual(pausedView, view.transform.position); Assert.AreEqual(frame, fixture.heads[1].CurrentFrame);
        Assert.IsFalse(fixture.heads[1].idle, "An older idle snapshot must not replace the displayed moving frame during pause.");
        OwnerBoatFixture.Paused.SetValue(null, false);
        if (stopClient) fx.OnStopClient();
        else { UnityEngine.Object.DestroyImmediate(fixture.source.gameObject); fixture.Present(); }
        Assert.IsFalse(fx.OwnerBoatPredictionReady); Assert.AreEqual(Vector3.zero, fixture.heads[1].transform.localPosition);
        Assert.IsTrue(fixture.bodyVisual.GetComponent<SpriteRenderer>().enabled); Assert.IsFalse(movement.SuppressInactivityAnimation);
        Assert.AreEqual(0, fixture.Suppressed.Count); Assert.IsTrue(view == null); Assert.IsTrue(fixture.remote.enabled);
    }

    [Test]
    public void BoatAttachmentWireAndMissingOrMalformedDefinitionsFailClosed()
    {
        using var fixture = new OwnerBoatFixture();
        var state = fixture.state;
        using (var writer = NetworkWriterPool.Get()) { writer.Write(state); state = new NetworkReader(writer.ToArray()).Read<MovementController.NetworkMovementState>(); }
        Assert.AreEqual(fixture.state.boat.path, state.boat.path); Assert.AreEqual(4501, state.boat.downId);
        Assert.AreEqual(fixture.state.boat.headUp, state.boat.headUp); Assert.AreEqual(Quaternion.identity, state.boat.rotation);
        Assert.AreEqual(Vector3.one, state.boat.scale); Assert.IsTrue(state.taggedPass); Assert.AreEqual("Water", state.passTag);
        var original = fixture.source.NetworkBoatPath; fixture.source.transform.position += Vector3.right * 7;
        Assert.AreEqual(original, fixture.source.NetworkBoatPath, "The authored key cannot change as the native boat follows its rider.");
        state.boat.path = "MissingBoat"; fixture.fx.ReconcileOwnerBoat(state); Assert.IsFalse(fixture.fx.OwnerBoatPredictionReady);
        state = fixture.state; state.boat.scale.x = float.NaN; fixture.fx.ReconcileOwnerBoat(state); Assert.IsFalse(fixture.fx.OwnerBoatPredictionReady);
        state = fixture.state; state.boat.scale = Vector3.zero; fixture.fx.ReconcileOwnerBoat(state); Assert.IsFalse(fixture.fx.OwnerBoatPredictionReady);
        state = fixture.state; fixture.fx.ReconcileOwnerBoat(state); Assert.IsTrue(fixture.fx.OwnerBoatPredictionReady);
        state.boat = default; fixture.fx.ReconcileOwnerBoat(state); Assert.IsFalse(fixture.fx.OwnerBoatPredictionReady); Assert.AreEqual(0, fixture.Suppressed.Count);
    }

    [Test]
    public void BoatProjectionYieldsToApprovedTransportAndCanResumeAfterItsScopeEnds()
    {
        using var fixture = new OwnerBoatFixture();
        fixture.fx.ReconcileOwnerBoat(fixture.state); fixture.Present(); Assert.IsTrue(fixture.fx.OwnerBoatPredictionReady);
        var state = fixture.state; state.canPredict = false; state.continuousPresentation = true;
        fixture.fx.ReconcileOwnerBoat(state); Assert.IsFalse(fixture.fx.OwnerBoatPredictionReady);
        Assert.IsTrue(fixture.remote.enabled); Assert.AreEqual(0, fixture.Suppressed.Count);
        fixture.fx.ReconcileOwnerBoat(fixture.state); Assert.IsTrue(fixture.fx.OwnerBoatPredictionReady); Assert.IsFalse(fixture.remote.enabled);
    }

    [Test]
    public void OldBoatPresenterCleanupCannotReleaseANewerViewClaim()
    {
        using var fixture = new OwnerBoatFixture();
        var next = new GameObject("NewBoatClaimOwner"); next.SetActive(false); next.AddComponent<BoxCollider2D>();
        try
        {
            var other = next.AddComponent<NetworkBombFx>(); var ids = new[] { 4501 };
            fixture.fx.ReconcileOwnerBoat(fixture.state);
            BombermanNetworkManager.SetOwnerBoatSpriteSuppression(other, ids, true);
            fixture.fx.ReconcileOwnerBoat(default);
            Assert.AreSame(other, fixture.Suppressed[4501]); Assert.IsFalse(fixture.remote.enabled);
            BombermanNetworkManager.SetOwnerBoatSpriteSuppression(other, ids, false);
            Assert.IsTrue(fixture.remote.enabled); Assert.AreEqual(0, fixture.Suppressed.Count);
        }
        finally { UnityEngine.Object.DestroyImmediate(next); }
    }

    [Test]
    public void PlayerAndMountReplicationDoNotOverwriteApprovedBoatHeadOrShowTheLouie()
    {
        using var fixture = new OwnerBoatFixture();
        var actor = fixture.fx.gameObject;
        var playerAnimation = actor.AddComponent<NetworkPlayerAnimation>();
        var mountAnimation = actor.AddComponent<NetworkMountAnimation>();
        var companion = actor.GetComponent<PlayerMountCompanion>();
        var mount = new GameObject("BoatLouie"); mount.transform.SetParent(actor.transform, false);
        try
        {
            var visual = fixture.MakeAnimation(mount.transform, "LouieDirection");
            var shadow = new GameObject("LouieShadow", typeof(SpriteRenderer)); shadow.transform.SetParent(mount.transform, false);
            typeof(PlayerMountCompanion).GetField("currentLouie", BoatFlags).SetValue(companion, mount);
            typeof(NetworkMountAnimation).GetField("companion", BoatFlags).SetValue(mountAnimation, companion);
            typeof(NetworkMountAnimation).GetField("trackedMount", BoatFlags).SetValue(mountAnimation, mount);
            typeof(NetworkMountAnimation).GetField("renderers", BoatFlags).SetValue(mountAnimation, new[] { visual });
            typeof(NetworkMountAnimation).GetField("sprites", BoatFlags).SetValue(mountAnimation, new[] { visual.GetComponent<SpriteRenderer>() });
            typeof(NetworkPlayerAnimation).GetMethod("Awake", BoatFlags).Invoke(playerAnimation, null);
            var identity = actor.GetComponent<NetworkIdentity>();
            typeof(NetworkIdentity).GetMethod("InitializeNetworkBehaviours", BoatFlags).Invoke(identity, null);
            typeof(NetworkIdentity).GetProperty("isClient").SetValue(identity, true);
            fixture.movement.RestoreNetworkMovementState(fixture.state); fixture.fx.ReconcileOwnerBoat(fixture.state); fixture.Present();
            Assert.IsFalse(shadow.GetComponent<SpriteRenderer>().enabled);
            fixture.movement.SetPredictLocally(false);
            typeof(NetworkPlayerAnimation).GetMethod("LateUpdate", BoatFlags).Invoke(playerAnimation, null);
            Assert.IsTrue(fixture.heads[0].GetComponent<SpriteRenderer>().enabled, "Approved boat presentation also owns the head while normal prediction is blocked.");
            visual.GetComponent<SpriteRenderer>().enabled = true;
            typeof(NetworkMountAnimation).GetMethod("LateUpdate", BoatFlags).Invoke(mountAnimation, null);
            Assert.IsFalse(visual.GetComponent<SpriteRenderer>().enabled, "A later mount presenter cannot resurrect the hidden Louie over its rider's boat.");
        }
        finally
        {
            typeof(PlayerMountCompanion).GetField("currentLouie", BoatFlags).SetValue(companion, null);
            UnityEngine.Object.DestroyImmediate(mount);
        }
    }

    sealed class OwnerBoatFixture : System.IDisposable
    {
        public static readonly FieldInfo Paused = typeof(GamePauseController).GetField("<IsPaused>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        readonly NetSync.NetMode oldMode = NetSync.Mode;
        readonly bool oldNetworkedScene = NetSync.IsNetworkedScene, wasPaused = (bool)Paused.GetValue(null);
        readonly FieldInfo clientState = typeof(NetworkClient).GetField("connectState", BindingFlags.Static | BindingFlags.NonPublic);
        readonly object oldClientState, oldManager;
        readonly GameObject actor, managerObject;
        readonly Texture2D texture;
        readonly Sprite sprite;
        readonly uint actorId = uint.MaxValue - 450;
        public readonly NetworkBombFx fx;
        public readonly MovementController movement;
        public readonly BoatRideZone source;
        public readonly AnimatedSpriteRenderer[] heads = new AnimatedSpriteRenderer[4];
        public readonly AnimatedSpriteRenderer bodyVisual;
        public readonly SpriteRenderer remote;
        public readonly MovementController.NetworkMovementState state;
        public readonly Dictionary<int, NetworkBombFx> Suppressed;

        public OwnerBoatFixture()
        {
            oldClientState = clientState.GetValue(null); oldManager = typeof(NetworkManager).GetProperty("singleton").GetValue(null);
            NetSync.Mode = NetSync.NetMode.Client; clientState.SetValue(null, System.Enum.Parse(clientState.FieldType, "Connected")); Paused.SetValue(null, false);
            texture = new Texture2D(1, 1); sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.zero);
            managerObject = new GameObject("OwnerBoatManager"); managerObject.SetActive(false);
            var manager = managerObject.AddComponent<BombermanNetworkManager>(); manager.enabled = false;
            typeof(NetworkManager).GetProperty("singleton").SetValue(null, manager); managerObject.SetActive(true);
            Suppressed = (Dictionary<int, NetworkBombFx>)typeof(BombermanNetworkManager).GetField("coopOwnerBoatSprites", BoatFlags).GetValue(manager);
            remote = new GameObject("RemoteBoatSprite", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>(); remote.transform.SetParent(managerObject.transform);
            ((Dictionary<int, SpriteRenderer>)typeof(BombermanNetworkManager).GetField("coopViews", BoatFlags).GetValue(manager)).Add(4501, remote);
            actor = new GameObject("OwnerBoatRider"); actor.SetActive(false); actor.tag = "Player"; actor.AddComponent<BoxCollider2D>();
            movement = actor.AddComponent<MovementController>(); var body = actor.GetComponent<Rigidbody2D>(); body.bodyType = RigidbodyType2D.Kinematic;
            typeof(MovementController).GetProperty(nameof(MovementController.Rigidbody)).SetValue(movement, body);
            bodyVisual = MakeAnimation(actor.transform, "WalkingBody");
            typeof(MovementController).GetField("spriteRendererDown", BoatFlags).SetValue(movement, bodyVisual);
            string[] names = { "HeadOnlyDown", "HeadOnlyUp", "HeadOnlyLeft", "HeadOnlyRight" };
            for (int i = 0; i < 4; i++) heads[i] = MakeAnimation(actor.transform, names[i]);
            var identity = actor.AddComponent<NetworkIdentity>(); fx = actor.AddComponent<NetworkBombFx>();
            typeof(NetworkIdentity).GetMethod("InitializeNetworkBehaviours", BoatFlags).Invoke(identity, null);
            typeof(NetworkIdentity).GetProperty("netId").SetValue(identity, actorId); typeof(NetworkIdentity).GetProperty("isLocalPlayer").SetValue(identity, true);
            NetworkClient.spawned.Add(actorId, identity);
            foreach (var behaviour in actor.GetComponents<MonoBehaviour>()) behaviour.enabled = false;
            fx.enabled = true; actor.SetActive(true); body.position = new Vector2(62000, 62000); actor.transform.position = body.position;
            var definition = new GameObject("BoatDefinition45"); definition.SetActive(false); definition.transform.position = new Vector3(63000, 63000);
            source = definition.AddComponent<BoatRideZone>();
            string[] directions = { "down", "up", "left", "right" };
            for (int i = 0; i < 4; i++) typeof(BoatRideZone).GetField(directions[i], BoatFlags).SetValue(source, MakeAnimation(definition.transform, directions[i]));
            typeof(BoatRideZone).GetMethod("Awake", BoatFlags).Invoke(source, null);
            source.enabled = false; definition.GetComponent<BoxCollider2D>().enabled = false; definition.SetActive(true);
            state = new MovementController.NetworkMovementState { position = body.position, facing = Vector2.down, worldSpeed = 4.1f,
                animationSpeed = PlayerPersistentStats.BaseSpeedNormal, canPredict = true, taggedPass = true, passTag = "Water",
                boat = new BoatRideZone.NetworkRideState { path = source.NetworkBoatPath, downId = 4501, upId = 4502, leftId = 4503, rightId = 4504,
                    scale = Vector3.one, rotation = Quaternion.identity, followOffset = new Vector2(0.25f, 0), hideBody = true,
                    headOffsets = true, headUp = new Vector2(0.25f, 0.5f) } };
        }

        public AnimatedSpriteRenderer MakeAnimation(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(SpriteRenderer)); go.transform.SetParent(parent, false);
            var animation = go.AddComponent<AnimatedSpriteRenderer>(); animation.idleSprite = sprite; animation.animationSprite = new[] { sprite, sprite }; animation.animationTime = 0.05f;
            typeof(AnimatedSpriteRenderer).GetMethod("Awake", BoatFlags).Invoke(animation, null);
            animation.enabled = false; return animation;
        }

        public void Present() => typeof(NetworkBombFx).GetMethod("UpdateOwnerBoat", BoatFlags).Invoke(fx, new object[] { 0.06f, 0.06f });
        public void Dispose()
        {
            NetworkClient.spawned.Remove(actorId); UnityEngine.Object.DestroyImmediate(actor);
            if (source != null) UnityEngine.Object.DestroyImmediate(source.gameObject);
            typeof(NetworkManager).GetProperty("singleton").SetValue(null, oldManager); UnityEngine.Object.DestroyImmediate(managerObject);
            UnityEngine.Object.DestroyImmediate(sprite); UnityEngine.Object.DestroyImmediate(texture);
            NetSync.Mode = oldMode; NetSync.IsNetworkedScene = oldNetworkedScene; clientState.SetValue(null, oldClientState); Paused.SetValue(null, wasPaused);
        }
    }
}
#endif

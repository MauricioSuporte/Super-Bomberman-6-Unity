using System;
using System.Reflection;
using Assets.Scripts.Netcode;
using Mirror;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed partial class OnlineRoomEditModeTests
{
    [TestCase(0, 1, true, true, true)]
    [TestCase(0, 1, false, true, false)]
    [TestCase(0, 1, true, false, false)]
    [TestCase(2, 1, true, true, false)]
    [TestCase(0, 0, true, true, false)]
    public void MountRemovalWaitsOnlyForAnAliveRiderAlreadyPresentingItsExit(int approved, int displayed, bool arc, bool alive, bool deferred)
    {
        Assert.AreEqual(deferred, NetworkPlayerState.DeferMountVisualRemoval(approved, displayed, arc, alive));
    }

    [Test]
    public void WorldMountBuffersRootAndFramesTogetherAndCutsLargeDiscontinuities()
    {
        using var fixture = new ItemMotionFixture();
        var actor = new GameObject("ApprovedWorldMount"); actor.SetActive(false);
        try
        {
            var mount = actor.AddComponent<NetworkWorldMount>();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(NetworkIdentity).GetMethod("InitializeNetworkBehaviours", flags).Invoke(actor.GetComponent<NetworkIdentity>(), null);
            mount.frames.IsWritable = () => true;
            var position = typeof(NetworkWorldMount).GetField("worldPosition", flags);
            var rotation = typeof(NetworkWorldMount).GetField("worldRotation", flags);
            var scale = typeof(NetworkWorldMount).GetField("worldScale", flags);
            var receive = typeof(NetworkWorldMount).GetMethod("OnWorldMountTime", flags);
            position.SetValue(mount, Vector3.zero); rotation.SetValue(mount, Quaternion.identity); scale.SetValue(mount, Vector3.one);
            mount.frames.Add(new NetworkWorldMount.MountFrame { visible = true, frame = 1, color = Color.white });
            receive.Invoke(mount, new object[] { 0d, 10d });
            position.SetValue(mount, Vector3.right * 2); scale.SetValue(mount, Vector3.one * 2);
            mount.frames[0] = new NetworkWorldMount.MountFrame { visible = true, frame = 3, color = Color.red };
            receive.Invoke(mount, new object[] { 10d, 10.2d });
            Assert.IsTrue(mount.SampleWorldMount(10.1, out var sampled, out _, out var sampledScale, out var frames));
            Assert.AreEqual(1, sampled.x, .0001f); Assert.AreEqual(1.5f, sampledScale.x, .0001f);
            Assert.AreEqual(1, frames[0].frame); Assert.AreEqual(Color.white, frames[0].color);
            position.SetValue(mount, Vector3.right * 100); receive.Invoke(mount, new object[] { 10.2d, 11d });
            mount.SampleWorldMount(10.7, out sampled, out _, out _, out _); Assert.AreEqual(100, sampled.x);
            for (int i = 1; i <= 40; i++) receive.Invoke(mount, new object[] { 0d, 11d + i });
            var history = (System.Collections.IList)typeof(NetworkWorldMount).GetField("worldHistory", flags).GetValue(mount);
            Assert.AreEqual(32, history.Count); mount.OnStopClient(); Assert.AreEqual(0, history.Count);
        }
        finally { Object.DestroyImmediate(actor); }
    }

    [TestCase(.25f, .15625f)] [TestCase(.5f, .5f)] [TestCase(1f, 1f)]
    public void ItemApprovedRollUsesNativeSmoothStep(float progress, float expected)
    {
        Assert.AreEqual(expected, ItemPickup.SampleNetworkItemSegment(Vector2.zero, Vector2.right, Vector2.right, progress).x, .0001f);
        Assert.AreEqual(-expected, ItemPickup.SampleNetworkItemSegment(Vector2.zero, Vector2.down, Vector2.down, progress).y, .0001f);
        Assert.AreEqual(Vector2.left * 8, ItemPickup.SampleNetworkItemSegment(Vector2.left * 8, Vector2.right * 6, Vector2.right, .49f));
        Assert.AreEqual(Vector2.right * 6, ItemPickup.SampleNetworkItemSegment(Vector2.left * 8, Vector2.right * 6, Vector2.right, .5f));
    }

    [Test]
    public void ClientItemEntryPointsCannotChooseLandingOrRunGameplayCoroutines()
    {
        using var fixture = new ItemMotionFixture(); var item = fixture.item; item.type = ItemType.Skull;
        Assert.IsFalse(item.TryExpelItem(Vector2.right, 1, null, 3));
        Assert.IsFalse(item.TryBounceSkull(Vector2.right));
        Assert.IsFalse(item.UpdateKickedBombPushSegment(Vector2.right, 1, null, Vector2.zero, .5f));
        Assert.IsFalse(item.TryMoveSkullInFrontOfKickedBomb(Vector2.right, 1, null, Vector2.zero, 1, true));
        Assert.IsTrue(item.GetComponent<Collider2D>().enabled);
        Assert.AreEqual(Vector3.zero, item.transform.position);
    }

    [Test]
    public void ApprovedItemRollQueuesStopAndContinuationWithoutATileBoundaryFrameWait()
    {
        using var fixture = new ItemMotionFixture(); var item = fixture.item;
        var first = fixture.Motion(uint.MaxValue, 0, 1); item.ReceiveNetworkItemMotion(first);
        var stop = fixture.Motion(0, 1, 1); stop.moving = false; stop.duration = 0; item.ReceiveNetworkItemMotion(stop);
        item.ReceiveNetworkItemMotion(fixture.Motion(1, 1, 2));
        item.PresentNetworkItemMotion(.06f); Assert.AreEqual(.5f, item.transform.position.x, .0001f);
        item.transform.position = Vector3.one * 99; item.PresentNetworkItemMotion(0);
        Assert.AreEqual(.5f, item.transform.position.x, .0001f);
        ItemMotionFixture.Pause.SetValue(null, true); item.PresentNetworkItemMotion(1);
        Assert.AreEqual(.5f, item.transform.position.x, .0001f);
        ItemMotionFixture.Pause.SetValue(null, false); item.PresentNetworkItemMotion(.08f);
        Assert.Greater(item.transform.position.x, 1, "Intermediate stops must not insert a tile-boundary frame wait.");
        stop = fixture.Motion(2, 2, 2); stop.duration = 0; stop.moving = false; item.ReceiveNetworkItemMotion(stop);
        item.PresentNetworkItemMotion(.3f); Assert.AreEqual(2, item.transform.position.x, .0001f);
        item.ReceiveNetworkItemMotion(first); item.PresentNetworkItemMotion(.1f); Assert.AreEqual(2, item.transform.position.x, .0001f);
        Assert.IsTrue(item.GetComponent<Collider2D>().enabled);
    }

    [Test]
    public void ApprovedItemRollRestoresNativeSortingOnStopAndDisable()
    {
        using var fixture = new ItemMotionFixture(); var item = fixture.item;
        var low = item.GetComponent<SpriteRenderer>(); low.sortingOrder = 2; low.flipX = true;
        var child = new GameObject("HighItemSprite", typeof(SpriteRenderer)); child.transform.SetParent(item.transform, false);
        var high = child.GetComponent<SpriteRenderer>(); high.sortingOrder = 5;
        item.ReceiveNetworkItemMotion(fixture.Motion(1, 0, 1)); item.PresentNetworkItemMotion(.03f);
        Assert.AreEqual(3, low.sortingOrder); Assert.AreEqual(5, high.sortingOrder);
        item.PresentNetworkItemMotion(.01f);
        var stop = fixture.Motion(2, 1, 1); stop.moving = false; stop.duration = 0;
        item.ReceiveNetworkItemMotion(stop); item.PresentNetworkItemMotion(.2f);
        Assert.AreEqual(2, low.sortingOrder); Assert.AreEqual(5, high.sortingOrder); Assert.IsTrue(low.flipX);
        item.ReceiveNetworkItemMotion(fixture.Motion(3, 1, 2)); item.PresentNetworkItemMotion(.03f);
        Assert.AreEqual(3, low.sortingOrder);
        typeof(ItemPickup).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(item, null);
        Assert.AreEqual(2, low.sortingOrder); Assert.AreEqual(5, high.sortingOrder);
        Assert.IsTrue(item.GetComponent<Collider2D>().enabled);
    }

    [Test]
    public void ApprovedItemMotionRejectsWrongIdentitySceneAndInvalidGeometry()
    {
        using var fixture = new ItemMotionFixture(); var item = fixture.item;
        var motion = fixture.Motion(1, 0, 1); motion.netId++; item.ReceiveNetworkItemMotion(motion);
        motion = fixture.Motion(2, 0, 1); motion.scene = "another"; item.ReceiveNetworkItemMotion(motion);
        motion = fixture.Motion(3, 0, 1); motion.end.x = float.NaN; item.ReceiveNetworkItemMotion(motion);
        item.PresentNetworkItemMotion(1); Assert.AreEqual(Vector3.zero, item.transform.position);
        Assert.IsFalse(ItemPickup.ValidNetworkItemMotion(motion));
        motion = fixture.Motion(4, 0, 1); motion.duration = 100; Assert.IsFalse(ItemPickup.ValidNetworkItemMotion(motion));
    }

    [Test]
    public void LateItemObserverStartsAtApprovedPhaseInsteadOfReplayingTheWholeRoll()
    {
        using var fixture = new ItemMotionFixture();
        var motion = fixture.Motion(1, 0, 1); motion.recovering = true; motion.elapsed = .06f;
        fixture.item.ReceiveNetworkItemMotion(motion); fixture.item.PresentNetworkItemMotion(0);
        Assert.AreEqual(.5f, fixture.item.transform.position.x, .0001f);
        motion = fixture.Motion(2, 1, 2); motion.pushed = true; motion.recovering = true; motion.elapsed = motion.duration;
        fixture.item.ReceiveNetworkItemMotion(motion); fixture.item.PresentNetworkItemMotion(.2f);
        Assert.AreEqual(2, fixture.item.transform.position.x, .0001f);
    }

    sealed class ItemMotionFixture : IDisposable
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        readonly NetSync.NetMode oldMode = NetSync.Mode;
        readonly FieldInfo state = typeof(NetworkClient).GetField("connectState", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        readonly object previousState;
        public static readonly FieldInfo Pause = typeof(GamePauseController).GetField("<IsPaused>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        readonly bool paused = (bool)Pause.GetValue(null);
        public readonly ItemPickup item;
        readonly GameObject actor;
        public ItemMotionFixture()
        {
            previousState = state.GetValue(null); state.SetValue(null, Enum.Parse(state.FieldType, "Connected"));
            NetSync.Mode = NetSync.NetMode.Client; Pause.SetValue(null, false);
            actor = new GameObject("ApprovedItemMotion"); actor.SetActive(false); actor.AddComponent<BoxCollider2D>();
            var identity = actor.AddComponent<NetworkIdentity>(); item = actor.AddComponent<ItemPickup>();
            typeof(NetworkIdentity).GetMethod("InitializeNetworkBehaviours", Flags).Invoke(identity, null);
            typeof(NetworkIdentity).GetProperty("netId").SetValue(identity, 912u);
            typeof(NetworkIdentity).GetProperty("isClient").SetValue(identity, true);
        }
        public NetworkItemMotion Motion(uint revision, float start, float end) => new()
        {
            scene = actor.scene.name, netId = 912, revision = revision, start = Vector2.right * start,
            end = Vector2.right * end, direction = Vector2.right, duration = .12f, moving = true, rotation = Quaternion.identity
        };
        public void Dispose()
        {
            Object.DestroyImmediate(actor); NetSync.Mode = oldMode; state.SetValue(null, previousState); Pause.SetValue(null, paused);
        }
    }
}

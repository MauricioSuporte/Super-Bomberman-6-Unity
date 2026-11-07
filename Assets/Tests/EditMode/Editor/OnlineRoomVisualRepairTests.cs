#if UNITY_EDITOR
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
    public void VisualRepairOwnerLocomotionHidesForeignJumpAndAfkBranchesEvenWhenDirectionDidNotChange()
    {
        using var f = new OwnerBoatFixture();
        var mount = new GameObject("LeakedMountViews"); mount.transform.SetParent(f.fx.transform, false); mount.SetActive(false);
        var down = f.MakeAnimation(mount.transform, "Down");
        var up = f.MakeAnimation(mount.transform, "Up");
        var emote = f.MakeAnimation(mount.transform, "AfkAlt");
        var jump = f.MakeAnimation(mount.transform, "ExternalJumpUp");
        var visual = mount.AddComponent<MountVisualController>(); visual.louieDown = down; visual.louieUp = up;
        mount.SetActive(true); visual.Bind(f.movement); visual.enabled = false;
        f.movement.ForceFacingDirection(Vector2.down); visual.PresentNetworkLocomotion();
        foreach (var leaked in new[] { up, emote, jump }) { leaked.enabled = true; leaked.GetComponent<SpriteRenderer>().enabled = true; }
        visual.PresentNetworkLocomotion();
        Assert.IsTrue(down.enabled); Assert.IsTrue(down.GetComponent<SpriteRenderer>().enabled);
        foreach (var leaked in new[] { up, emote, jump })
        { Assert.IsFalse(leaked.enabled, leaked.name); Assert.IsFalse(leaked.GetComponent<SpriteRenderer>().enabled, leaked.name); }
        Assert.IsFalse(visual.enabled, "Native mount lifecycle stays disabled on a pure client.");
    }

    [TestCase(MountedType.Mole)] [TestCase(MountedType.Tank)]
    public void VisualRepairOwnerRestoresAllAuthoredHeadOffsetsAfterApprovedPoseRelease(MountedType type)
    {
        using var f = new OwnerBoatFixture();
        var actor = f.fx.gameObject; var animation = actor.AddComponent<NetworkMountAnimation>();
        var companion = actor.GetComponent<PlayerMountCompanion>();
        var mount = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Mounts/" + type + ".prefab"), actor.transform);
        typeof(PlayerMountCompanion).GetField("currentLouie", BoatFlags).SetValue(companion, mount);
        typeof(PlayerMountCompanion).GetField("mountedType", BoatFlags).SetValue(companion, type);
        string[] names = { "Down", "Up", "Left", "Right" };
        for (int i = 0; i < 4; i++) typeof(MovementController).GetField("headOnly" + names[i], BoatFlags).SetValue(f.movement, f.heads[i]);
        f.movement.SetMountedOnLouie(true);
        var visual = mount.GetComponentInChildren<MountVisualController>(true); visual.Bind(f.movement);
        typeof(NetworkMountAnimation).GetField("companion", BoatFlags).SetValue(animation, companion);
        typeof(NetworkIdentity).GetMethod("InitializeNetworkBehaviours", BoatFlags).Invoke(actor.GetComponent<NetworkIdentity>(), null);
        typeof(NetworkIdentity).GetProperty("isClient").SetValue(actor.GetComponent<NetworkIdentity>(), true);
        typeof(NetworkMountAnimation).GetMethod("Rebind", BoatFlags).Invoke(animation, new object[] { mount });
        for (int i = 0; i < 4; i++) f.heads[i].ClearRuntimeBaseOffset();
        animation.RestoreOwnerLocomotion();
        for (int i = 0; i < 4; i++)
        {
            var expected = (Vector2)typeof(MountVisualController).GetField("headOnly" + names[i] + "LocalOffset", BoatFlags).GetValue(visual);
            Assert.AreEqual((Vector3)expected, f.heads[i].RuntimeBaseLocalOffset, names[i]);
        }
    }

    [Test]
    public void VisualRepairPendingGroundMountAlreadyVisibleBeforeGameplayMountIsGranted()
    {
        using var f = new OwnerBoatFixture();
        var actor = f.fx.gameObject; var animation = actor.AddComponent<NetworkMountAnimation>();
        var companion = actor.GetComponent<PlayerMountCompanion>();
        companion.blueLouiePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Mounts/BlueLouie.prefab");
        typeof(PlayerMountCompanion).GetMethod("Awake", BoatFlags).Invoke(companion, null);
        typeof(NetworkMountAnimation).GetField("companion", BoatFlags).SetValue(animation, companion);
        var id = actor.GetComponent<NetworkIdentity>();
        typeof(NetworkIdentity).GetMethod("InitializeNetworkBehaviours", BoatFlags).Invoke(id, null);
        typeof(NetworkIdentity).GetProperty("isClient").SetValue(id, true);
        typeof(NetworkBombFx).GetField("ridingActive", BoatFlags).SetValue(f.fx, true);
        typeof(NetworkMountAnimation).GetField("frameType", BoatFlags).SetValue(animation, MountedType.Blue);
        animation.frames.IsWritable = () => true;
        int count = companion.blueLouiePrefab.GetComponentsInChildren<AnimatedSpriteRenderer>(true).Length;
        for (int i = 0; i < count; i++) animation.frames.Add(new NetworkWorldMount.MountFrame { visible = i == 0, idle = true, color = Color.white });
        typeof(NetworkMountAnimation).GetMethod("LateUpdate", BoatFlags).Invoke(animation, null);
        var mount = companion.GetMountedLouieObject(); Assert.IsNotNull(mount);
        Assert.IsTrue(mount.GetComponentsInChildren<AnimatedSpriteRenderer>(true)[0].GetComponent<SpriteRenderer>().enabled);
        Assert.IsFalse(f.movement.IsMounted, "A renderer is not a gameplay mount grant.");
        foreach (var collider in mount.GetComponentsInChildren<Collider2D>(true)) Assert.IsFalse(collider.enabled);
        typeof(NetworkBombFx).GetField("ridingActive", BoatFlags).SetValue(f.fx, false);
    }

    [Test]
    public void VisualRepairApprovedRidingArcPreventsHeadConfigurationFromReplacingTheAirbornePlayer()
    {
        using var f = new OwnerBoatFixture();
        var riding = f.fx.GetComponent<PlayerRidingController>();
        if (riding == null) riding = f.fx.gameObject.AddComponent<PlayerRidingController>();
        riding.mountAscendDown = f.bodyVisual;
        riding.PresentNetworkArc(new PlayerRidingController.NetworkArcState { duration = 1, facing = Vector2.down,
            startHeight = 0, endHeight = 1, peak = 1 }, .25f);
        Assert.IsFalse(riding.IsPlaying); Assert.IsTrue(f.movement.IsRidingPlaying());
        f.movement.SetUseHeadOnlyWhenMounted(true);
        f.movement.SetHeadOnlyMountedOffsets(Vector2.up, Vector2.up, Vector2.up, Vector2.up);
        Assert.IsTrue(f.bodyVisual.enabled); Assert.IsTrue(f.bodyVisual.GetComponent<SpriteRenderer>().enabled);
        riding.StopNetworkArcVisual(); Assert.IsFalse(f.movement.IsRidingPlaying());
    }

    [TestCase("Emote")] [TestCase("Afk")]
    public void VisualRepairAuthoritativePlayerPoseRemainsExclusiveWhenItsIndexDidNotChange(string poseName)
    {
        using var f = new OwnerBoatFixture();
        var foreign = f.MakeAnimation(f.fx.transform, poseName);
        var player = f.fx.gameObject.AddComponent<NetworkPlayerAnimation>();
        typeof(NetworkPlayerAnimation).GetMethod("Awake", BoatFlags).Invoke(player, null);
        typeof(NetworkIdentity).GetMethod("InitializeNetworkBehaviours", BoatFlags).Invoke(f.fx.GetComponent<NetworkIdentity>(), null);
        var renderers = (AnimatedSpriteRenderer[])typeof(NetworkPlayerAnimation).GetField("renderers", BoatFlags).GetValue(player);
        byte index = (byte)Array.IndexOf(renderers, f.bodyVisual);
        typeof(NetworkPlayerAnimation).GetField("activeIndex", BoatFlags).SetValue(player, index);
        typeof(NetworkPlayerAnimation).GetField("activeFrame", BoatFlags).SetValue(player, (byte)1);
        typeof(NetworkPlayerAnimation).GetField("activeIdle", BoatFlags).SetValue(player, false);
        var apply = typeof(NetworkPlayerAnimation).GetMethod("ClientApply", BoatFlags);
        apply.Invoke(player, null);
        foreign.enabled = true; foreign.GetComponent<SpriteRenderer>().enabled = true;
        f.bodyVisual.enabled = false;
        apply.Invoke(player, null);
        Assert.IsFalse(foreign.enabled); Assert.IsFalse(foreign.GetComponent<SpriteRenderer>().enabled);
        Assert.IsTrue(f.bodyVisual.GetComponent<SpriteRenderer>().enabled); Assert.AreEqual(1, f.bodyVisual.CurrentFrame);
    }

    [TestCase("Emote")] [TestCase("Afk")]
    public void VisualRepairOwnerWalkingHandoffReleasesTheApprovedPlayerPose(string poseName)
    {
        using var f = new OwnerBoatFixture();
        var foreign = f.MakeAnimation(f.fx.transform, poseName);
        var player = f.fx.gameObject.AddComponent<NetworkPlayerAnimation>();
        typeof(NetworkPlayerAnimation).GetMethod("Awake", BoatFlags).Invoke(player, null);
        var identity = f.fx.GetComponent<NetworkIdentity>();
        typeof(NetworkIdentity).GetMethod("InitializeNetworkBehaviours", BoatFlags).Invoke(identity, null);
        typeof(NetworkIdentity).GetProperty("isClient").SetValue(identity, true);
        foreign.enabled = true; foreign.GetComponent<SpriteRenderer>().enabled = true;
        f.movement.SetPredictLocally(true);
        typeof(NetworkPlayerAnimation).GetMethod("LateUpdate", BoatFlags).Invoke(player, null);
        Assert.IsFalse(foreign.enabled); Assert.IsFalse(foreign.GetComponent<SpriteRenderer>().enabled);
        Assert.IsTrue(f.bodyVisual.GetComponent<SpriteRenderer>().enabled);
    }

    [Test]
    public void VisualRepairNetworkEmoteDirectionUsesAnExplicitPressAndHeldANeverCreatesASecondPress()
    {
        using var f = new OwnerBoatFixture();
        var sender = f.fx.gameObject.AddComponent<NetworkPlayerInput>();
        typeof(NetworkPlayerInput).GetMethod("Awake", BoatFlags).Invoke(sender, null);
        typeof(NetworkIdentity).GetMethod("InitializeNetworkBehaviours", BoatFlags).Invoke(f.fx.GetComponent<NetworkIdentity>(), null);
        typeof(NetworkPlayerSetup).GetField("netPlayerId", BoatFlags).SetValue(f.fx.GetComponent<NetworkPlayerSetup>(), 6);
        var input = PlayerInputManager.Instance; var owned = input == null ? new GameObject("ExplicitEmoteInput") : null;
        if (owned != null) input = owned.AddComponent<PlayerInputManager>();
        try
        {
            input.ClearSyntheticPlayer(6);
            var taps = (int[,])typeof(PlayerInputManager).GetField("syntheticTapFrame", BoatFlags).GetValue(input);
            for (int i = 0; i < taps.GetLength(1); i++) taps[6, i] = -1;
            var apply = typeof(NetworkPlayerInput).GetMethod("ApplyServerControls", BoatFlags);
            apply.Invoke(sender, new object[] { input, 1 | 16 | 256, 0 });
            Assert.IsFalse(input.GetDown(6, PlayerAction.MoveUp)); Assert.IsFalse(input.GetDown(6, PlayerAction.ActionA));
            apply.Invoke(sender, new object[] { input, 1 | 16 | 256, 1 });
            Assert.IsTrue(input.GetDown(6, PlayerAction.MoveUp)); Assert.IsTrue(input.Get(6, PlayerAction.ActionL));
            Assert.IsFalse(input.GetDown(6, PlayerAction.ActionA));
        }
        finally { input.ClearSyntheticPlayer(6); if (owned != null) Object.DestroyImmediate(owned); }
    }

    [Test]
    public void VisualRepairMountedInactivityRemainsAuthoritativeUntilInputCancelsIt()
    {
        using var f = new OwnerBoatFixture();
        f.movement.SetMountedOnLouie(true);
        f.movement.SetInactivityMountedDownOverride(true);
        Assert.AreEqual("inactivity_pose", f.movement.NetworkPredictionBlockReason);
        f.movement.SetInactivityMountedDownOverride(false);
        Assert.AreNotEqual("inactivity_pose", f.movement.NetworkPredictionBlockReason);
    }

    [UnityEngine.TestTools.UnityTest]
    public System.Collections.IEnumerator VisualRepairNativeMountDeathPreservesExplosionCauseAndAdvancesTheSelectedFrames()
    {
        yield return new UnityEngine.TestTools.EnterPlayMode();
        yield return UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("TitleScreen");
        var oldMode = NetSync.Mode; NetSync.Mode = NetSync.NetMode.Offline;
        try
        {
            foreach (bool explosion in new[] { false, true })
            {
                GameObject actor = null, mount = null;
                try
                {
                    actor = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab"));
                    actor.transform.position = new Vector3(1000, 1000, 0);
                    var companion = actor.GetComponent<PlayerMountCompanion>();
                    companion.ClearMountedStateForForcedArenaRemoval(); companion.RestoreMounted(MountedType.Blue);
                    mount = companion.GetMountedLouieObject(); Assert.IsNotNull(mount);
                    var health = companion.GetMountedLouieHealth(); health.StopInvulnerability(); health.SetExternalInvulnerability(false);
                    if (!explosion)
                    {
                        health.StartTemporaryInvulnerability(10, withBlink: false);
                        companion.OnMountedLouieHit(999, true);
                        Assert.AreSame(mount, companion.GetMountedLouieObject(), "An ignored blast must not mark a later death as an explosion.");
                        Assert.AreEqual(0u, companion.MountDeathRevision);
                        health.StopInvulnerability();
                    }
                    companion.OnMountedLouieHit(999, explosion);
                    var mover = mount.GetComponent<MovementController>(); Assert.IsTrue(mover.isDead);
                    Assert.AreEqual(explosion, mover.DeathWasByExplosion);
                    var chosen = explosion && mover.spriteRendererDeathByExplosion != null ? mover.spriteRendererDeathByExplosion : mover.spriteRendererDeath;
                    Assert.IsTrue(chosen.GetComponent<SpriteRenderer>().enabled); Assert.IsFalse(chosen.idle);
                    Assert.AreEqual(1u, companion.MountDeathRevision);
                    yield return new WaitForSeconds(.2f);
                    if (chosen.animationSprite.Length > 1) Assert.Greater(chosen.CurrentFrame, 0);
                }
                finally { if (actor != null) Object.DestroyImmediate(actor); if (mount != null) Object.DestroyImmediate(mount); }
            }
        }
        finally { NetSync.Mode = oldMode; }
        yield return new UnityEngine.TestTools.ExitPlayMode();
    }

    [Test]
    public void VisualRepairPinkJumpStopDoesNotRestartNativeMountLifecycleOnPureClient()
    {
        using var f = new OwnerBoatFixture();
        var go = new GameObject("PinkJumpStop"); go.transform.SetParent(f.fx.transform, false);
        var visual = go.AddComponent<MountVisualController>(); visual.owner = f.movement; visual.enabled = false;
        var animator = go.AddComponent<PinkLouieJumpAnimator>();
        animator.jumpUp = f.MakeAnimation(go.transform, "JumpUp"); animator.Play(Vector2.up); animator.Stop();
        Assert.IsFalse(visual.enabled); Assert.IsFalse(animator.jumpUp.GetComponent<SpriteRenderer>().enabled);
    }

    // R01 / mount-cycles "Dismount predicted=4 confirmed=3": a burst after a stall overflowed the server queue and dropped the
    // Select frame. PlayerManualDismount reacts to the held transition, so the dismount ran on a held-only frame and was
    // published with the previous dismount's tick. The dropped edges must reach the next queued frame with their own tick.
    [Test]
    public void R01ServerInputQueueOverflowKeepsDroppedEdgesAheadOfHeldOnlyFramesWithTheirTick()
    {
        var queue = new System.Collections.Generic.Queue<NetworkPlayerInput.QueuedInput>();
        for (uint tick = 839; tick < 847; tick++)
            Assert.IsFalse(NetworkPlayerInput.EnqueueBounded(queue, new NetworkInputFrame { tick = tick, held = 128, pressed = tick == 839 ? 128 : 0 }, tick, 8, out _));
        Assert.IsTrue(NetworkPlayerInput.EnqueueBounded(queue, new NetworkInputFrame { tick = 847, held = 128 }, 847, 8, out uint dropped));
        Assert.AreEqual(839u, dropped);
        Assert.AreEqual(8, queue.Count);
        var head = queue.Peek();
        Assert.AreEqual(840u, head.frame.tick); Assert.AreEqual(128, head.frame.pressed); Assert.AreEqual(839u, head.EdgeTick(128));
        var items = queue.ToArray();
        Assert.AreEqual(847u, items[7].frame.tick); Assert.AreEqual(0, items[7].frame.pressed); Assert.AreEqual(847u, items[7].EdgeTick(128));
        for (int i = 1; i < 7; i++) Assert.AreEqual(0, items[i].frame.pressed, "only the next frame carries the dropped edge");

        // Another overflow drops the carrier: the edge moves on again with its original tick; the newest frame keeps its own.
        Assert.IsTrue(NetworkPlayerInput.EnqueueBounded(queue, new NetworkInputFrame { tick = 848, held = 128 }, 848, 8, out dropped));
        Assert.AreEqual(840u, dropped);
        Assert.AreEqual(841u, queue.Peek().frame.tick); Assert.AreEqual(128, queue.Peek().frame.pressed, "the carried edge moves on with its tick");
        Assert.AreEqual(839u, queue.Peek().EdgeTick(128));

        // Capacity 1 has no next queued frame: the incoming frame takes the edge and its tick.
        var single = new System.Collections.Generic.Queue<NetworkPlayerInput.QueuedInput>();
        NetworkPlayerInput.EnqueueBounded(single, new NetworkInputFrame { tick = 5, pressed = 16 }, 0, 1, out _);
        Assert.IsTrue(NetworkPlayerInput.EnqueueBounded(single, new NetworkInputFrame { tick = 6 }, 0, 1, out dropped));
        Assert.AreEqual(5u, dropped); Assert.AreEqual(16, single.Peek().frame.pressed); Assert.AreEqual(5u, single.Peek().EdgeTick(16));
    }

    // R01: preserving the dropped tick must not duplicate an action or re-label another one. Every press is applied by exactly
    // one dequeued frame, the carrier's own press keeps its own tick, a repeated press of the same button stays one edge, and
    // a burst of overflows never multiplies edges.
    [Test]
    public void R01ServerInputQueueOverflowNeverDuplicatesOrRelabelsActions()
    {
        var queue = new System.Collections.Generic.Queue<NetworkPlayerInput.QueuedInput>();
        NetworkPlayerInput.EnqueueBounded(queue, new NetworkInputFrame { tick = 10, pressed = 128 }, 0, 2, out _);
        NetworkPlayerInput.EnqueueBounded(queue, new NetworkInputFrame { tick = 11, pressed = 16 | 128 }, 0, 2, out _);
        Assert.IsTrue(NetworkPlayerInput.EnqueueBounded(queue, new NetworkInputFrame { tick = 12 }, 0, 2, out uint dropped));
        Assert.AreEqual(10u, dropped);
        var carrier = queue.Peek();
        Assert.AreEqual(16 | 128, carrier.frame.pressed, "the repeated Select is one edge, not two");
        Assert.AreEqual(11u, carrier.EdgeTick(16), "the carrier's own A keeps its own tick");
        Assert.AreEqual(10u, carrier.EdgeTick(128), "the merged Select keeps the dropped (earlier) tick");

        // A long burst through a full queue: count applied edges per button after draining.
        var burst = new System.Collections.Generic.Queue<NetworkPlayerInput.QueuedInput>();
        int sentA = 0, sentSelect = 0;
        for (uint tick = 1; tick <= 40; tick++)
        {
            int pressed = (tick % 7 == 0 ? 16 : 0) | (tick % 11 == 0 ? 128 : 0);
            if ((pressed & 16) != 0) sentA++;
            if ((pressed & 128) != 0) sentSelect++;
            NetworkPlayerInput.EnqueueBounded(burst, new NetworkInputFrame { tick = tick, pressed = pressed }, 0, 8, out _);
        }
        int appliedA = 0, appliedSelect = 0;
        var seenA = new System.Collections.Generic.HashSet<uint>();
        var seenSelect = new System.Collections.Generic.HashSet<uint>();
        while (burst.Count > 0)
        {
            var item = burst.Dequeue();
            if ((item.frame.pressed & 16) != 0) { appliedA++; Assert.IsTrue(seenA.Add(item.EdgeTick(16)), "an A edge tick was applied twice"); Assert.AreEqual(0u, item.EdgeTick(16) % 7); }
            if ((item.frame.pressed & 128) != 0) { appliedSelect++; Assert.IsTrue(seenSelect.Add(item.EdgeTick(128)), "a Select edge tick was applied twice"); Assert.AreEqual(0u, item.EdgeTick(128) % 11); }
        }
        // Without draining, presses cascade into one carrier: some are lost (as before R01), none is invented or doubled.
        Assert.That(appliedA, Is.InRange(1, sentA), "no A press was invented");
        Assert.That(appliedSelect, Is.InRange(1, sentSelect), "no Select press was invented");
    }

    // R01: the predicting owner applied a replicated mount (Pink -> Mole) on the frame it arrived while its own body was still
    // the plain on-foot sprite for one frame. The gate hides only the mount for that frame; HeadOnly/mounted bodies keep it.
    [Test]
    public void R01MountGateHidesArrivingMountForPredictingOwnerWhileFootBodyIsVisible()
    {
        using var fixture = new OwnerBoatFixture();
        var actor = fixture.fx.gameObject;
        var mountAnimation = actor.AddComponent<NetworkMountAnimation>();
        var companion = actor.GetComponent<PlayerMountCompanion>();
        var mount = new GameObject("ArrivingMole"); mount.transform.SetParent(actor.transform, false);
        try
        {
            var visual = fixture.MakeAnimation(mount.transform, "Movimentation");
            var mountSprite = visual.GetComponent<SpriteRenderer>();
            typeof(NetworkMountAnimation).GetField("companion", BoatFlags).SetValue(mountAnimation, companion);
            typeof(NetworkMountAnimation).GetField("trackedMount", BoatFlags).SetValue(mountAnimation, mount);
            typeof(NetworkMountAnimation).GetField("renderers", BoatFlags).SetValue(mountAnimation, new[] { visual });
            typeof(NetworkMountAnimation).GetField("sprites", BoatFlags).SetValue(mountAnimation, new[] { mountSprite });
            var identity = actor.GetComponent<NetworkIdentity>();
            typeof(NetworkIdentity).GetMethod("InitializeNetworkBehaviours", BoatFlags).Invoke(identity, null);
            typeof(NetworkIdentity).GetProperty("isClient").SetValue(identity, true);
            var gate = typeof(NetworkMountAnimation).GetMethod("HideMountBeforeAuthorityBody", BoatFlags);
            fixture.movement.SetPredictLocally(true);
            fixture.bodyVisual.enabled = true; fixture.bodyVisual.GetComponent<SpriteRenderer>().enabled = true;
            foreach (var head in fixture.heads) { head.enabled = false; head.GetComponent<SpriteRenderer>().enabled = false; }
            mountSprite.enabled = true;
            Assert.IsTrue(fixture.movement.IsPlainFootBodyVisible());
            gate.Invoke(mountAnimation, null);
            Assert.IsFalse(mountSprite.enabled, "foot body + arriving mount must not reach the screen on the predicting owner");

            fixture.bodyVisual.enabled = false; fixture.bodyVisual.GetComponent<SpriteRenderer>().enabled = false;
            fixture.heads[2].enabled = true; fixture.heads[2].GetComponent<SpriteRenderer>().enabled = true;
            mountSprite.enabled = true;
            gate.Invoke(mountAnimation, null);
            Assert.IsTrue(mountSprite.enabled, "a HeadOnly body keeps its mount");

            fixture.bodyVisual.enabled = true; fixture.bodyVisual.GetComponent<SpriteRenderer>().enabled = true;
            NetSync.Mode = NetSync.NetMode.Offline;
            gate.Invoke(mountAnimation, null);
            Assert.IsTrue(mountSprite.enabled, "offline presentation is never touched");
        }
        finally { UnityEngine.Object.DestroyImmediate(mount); }
    }

    // R01 round-reload_r1 (server frames 7167-7227): a stunned Mole rider showed HeadOnlyUp and HeadOnlyDown together for
    // the whole 1 s stun. The Mole has no animated stun renderer (shake only), so StunReceiver kept the rider's sprites and
    // enabled the head-down stun pose next to the current head. Same code offline, host, owner and server (observers copy
    // the server's single pose).
    [Test]
    public void R01MountedShakeOnlyStunShowsOneRiderHeadAndRestoresTheSeatedPose()
    {
        var root = new GameObject("MoleStunFixture"); root.SetActive(false);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        try
        {
            var actor = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab"), root.transform);
            var mount = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Mounts/Mole.prefab"), actor.transform);
            foreach (var animation in actor.GetComponentsInChildren<AnimatedSpriteRenderer>(true))
                typeof(AnimatedSpriteRenderer).GetMethod("Awake", flags).Invoke(animation, null);
            var movement = actor.GetComponent<MovementController>();
            var stun = actor.GetComponent<StunReceiver>();
            var visual = mount.GetComponentInChildren<MountVisualController>(true);
            Assert.IsTrue(visual.useHeadOnlyPlayerVisual);
            typeof(MovementController).GetField("isMounted", flags).SetValue(movement, true);
            typeof(MovementController).GetField("useHeadOnlyWhenMountedRuntime", flags).SetValue(movement, true);
            typeof(StunReceiver).GetField("cachedMovement", flags).SetValue(stun, movement);
            typeof(StunReceiver).GetField("activeMountedStunVisual", flags).SetValue(stun, visual);
            AnimatedSpriteRenderer Head(string name) => (AnimatedSpriteRenderer)typeof(MovementController).GetField(name, flags).GetValue(movement);
            var body = new System.Collections.Generic.List<AnimatedSpriteRenderer>();
            movement.CollectMountedBodyRenderers(body);
            void Show(AnimatedSpriteRenderer keep)
            {
                foreach (var r in body) { r.enabled = r == keep; r.GetComponent<SpriteRenderer>().enabled = r == keep; }
            }
            string Shown()
            {
                var names = new System.Collections.Generic.List<string>();
                foreach (var r in body) if (r.enabled && r.GetComponent<SpriteRenderer>().enabled) names.Add(r.name);
                return string.Join(",", names);
            }
            var showStun = typeof(StunReceiver).GetMethod("ShowMountedPlayerDownForMountedStun", flags);
            var hideMethod = typeof(StunReceiver).GetMethod("HideMountedPlayerDownForMountedStun", flags);
            var stunRendererField = typeof(StunReceiver).GetField("activeMountedPlayerStunRenderer", flags);
            void HideStun() { hideMethod.Invoke(stun, null); stunRendererField.SetValue(stun, null); } // as every stun exit does

            Show(Head("headOnlyUp"));
            showStun.Invoke(stun, null);
            Assert.AreEqual("HeadOnlyDown", Shown(), "stun must replace the current head, not add a second one");
            showStun.Invoke(stun, null); // re-entry after a damaged/external suppression keeps one head
            Assert.AreEqual("HeadOnlyDown", Shown());
            HideStun();
            Assert.AreEqual("HeadOnlyUp", Shown(), "the seated pose comes back after the stun");

            // Already facing down: nothing else to hide, and the down head stays after the stun.
            Show(Head("headOnlyDown"));
            showStun.Invoke(stun, null);
            Assert.AreEqual("HeadOnlyDown", Shown());
            HideStun();
            Assert.AreEqual("HeadOnlyDown", Shown());

            // Something else chose a pose meanwhile (or the rider dismounted): the old head is not brought back on top.
            Show(Head("headOnlyRight"));
            showStun.Invoke(stun, null);
            Head("headOnlyLeft").enabled = true; Head("headOnlyLeft").GetComponent<SpriteRenderer>().enabled = true;
            HideStun();
            Assert.AreEqual("HeadOnlyLeft", Shown());
            Show(Head("headOnlyRight"));
            showStun.Invoke(stun, null);
            typeof(MovementController).GetField("isMounted", flags).SetValue(movement, false);
            HideStun();
            Assert.AreEqual("", Shown());
        }
        finally { Object.DestroyImmediate(root); }
    }
}
#endif

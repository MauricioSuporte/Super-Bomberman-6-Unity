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
}
#endif

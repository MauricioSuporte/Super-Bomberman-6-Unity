using System.Collections;
using System.Reflection;
using Assets.Scripts.Netcode;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed partial class OnlineRoomEditModeTests
{
    [UnityTest]
    public IEnumerator RopeCancellationRestoresControlPoseAndProtectionWithoutCompletingItsFlight()
    {
        yield return new EnterPlayMode();
        yield return SceneManager.LoadSceneAsync("TitleScreen");
        Scene previousScene = SceneManager.GetActiveScene();
        Scene arena = SceneManager.CreateScene("BattleMode_11");
        SceneManager.SetActiveScene(arena);
        var oldMode = NetSync.Mode;
        NetSync.Mode = NetSync.NetMode.Offline;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        try
        {
            foreach (var state in new[] { (flight: false, component: true, protection: false),
                (flight: true, component: false, protection: false), (flight: true, component: true, protection: true) })
            {
                var actor = new GameObject("RopeLifetimeActor"); actor.SetActive(false);
                var mechanism = new GameObject("RopeLifetimeMechanism");
                try
                {
                    actor.transform.position = new Vector3(100, 100, 0);
                    actor.AddComponent<BoxCollider2D>();
                    var movement = actor.AddComponent<MovementController>();
                    actor.GetComponent<Rigidbody2D>().gravityScale = 0;
                    AnimatedSpriteRenderer Visual(string name)
                    {
                        var child = new GameObject(name);
                        child.transform.SetParent(actor.transform, false);
                        child.transform.localPosition = new Vector3(.125f, .25f, 0);
                        child.AddComponent<SpriteRenderer>();
                        var renderer = child.AddComponent<AnimatedSpriteRenderer>();
                        renderer.enabled = false;
                        child.GetComponent<SpriteRenderer>().enabled = false;
                        return renderer;
                    }
                    var preparing = Visual("PreparingLeft"); var dash = Visual("DashLeft");
                    Vector3 originalPose = preparing.transform.localPosition;
                    actor.SetActive(true); actor.tag = "Player";
                    movement.enabled = false;
                    movement.SetExplosionInvulnerable(state.protection);
                    var rope = mechanism.AddComponent<BattleMode11ImpulseRopeController>();
                    typeof(BattleMode11ImpulseRopeController).GetField("playerPreparingSeconds", flags).SetValue(rope, state.flight ? .01f : 10f);
                    typeof(BattleMode11ImpulseRopeController).GetField("playerImpulseSeconds", flags).SetValue(rope, 10f);
                    var method = typeof(BattleMode11ImpulseRopeController).GetMethod("PlayerPreparingImpulseRoutine", flags);
                    void Start() => rope.StartCoroutine((IEnumerator)method.Invoke(rope, new object[] { movement, Vector3Int.zero, Vector2.right }));
                    Start();
                    Assert.IsTrue(movement.InputLocked, "Preparation must start in the input frame, without another scheduler yield.");
                    float deadline = Time.realtimeSinceStartup + 2;
                    while (!(state.flight ? dash.enabled : preparing.enabled) && Time.realtimeSinceStartup < deadline) yield return null;
                    Assert.IsTrue(state.flight ? dash.enabled : preparing.enabled, "Native action must begin before cancellation.");
                    Assert.IsTrue(movement.InputLocked); Assert.IsTrue(movement.ExternalMovementOverride);
                    Vector2 displayed = movement.Rigidbody.position;
                    if (state.component) rope.enabled = false;
                    else mechanism.SetActive(false);
                    Assert.IsFalse(movement.InputLocked);
                    Assert.IsFalse(movement.ExternalMovementOverride); Assert.IsFalse(movement.ExternalVisualSuppressed);
                    Assert.IsFalse(movement.ExternalMovementAllowsHazardDamage);
                    Assert.AreEqual(state.protection, movement.explosionInvulnerable);
                    Assert.IsFalse(preparing.enabled); Assert.IsFalse(dash.enabled);
                    Assert.That(Vector3.Distance(originalPose, preparing.transform.localPosition), Is.LessThan(.0001f));
                    Assert.That(Vector2.Distance(displayed, movement.Rigidbody.position), Is.LessThan(.0001f));
                    if (state.component) rope.enabled = true;
                    else mechanism.SetActive(true);
                    Start(); yield return null;
                    Assert.IsTrue(movement.InputLocked, "A cancelled actor must be able to use the rope again.");
                    mechanism.SetActive(false);
                    Assert.IsFalse(movement.InputLocked);
                }
                finally { Object.DestroyImmediate(mechanism); Object.DestroyImmediate(actor); }
            }
        }
        finally { NetSync.Mode = oldMode; SceneManager.SetActiveScene(previousScene); }
        yield return SceneManager.UnloadSceneAsync(arena);
        yield return new ExitPlayMode();
    }
}

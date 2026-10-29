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
    public IEnumerator DisablingSpringDuringNativePreparationOrFlightRestoresActor()
    {
        yield return new EnterPlayMode();
        yield return SceneManager.LoadSceneAsync("TitleScreen");
        var oldMode = NetSync.Mode;
        NetSync.Mode = NetSync.NetMode.Offline;
        try
        {
            foreach (var state in new[] { (prepare: 10f, protection: false, component: true),
                (prepare: 0f, protection: false, component: false), (prepare: 0f, protection: true, component: true) })
            {
                var actor = new GameObject("SpringLifetimeActor"); actor.SetActive(false);
                var mechanism = new GameObject("SpringLifetimeMechanism");
                try
                {
                    actor.transform.position = mechanism.transform.position = new Vector3(100, 100, 0);
                    var collider = actor.AddComponent<BoxCollider2D>();
                    var movement = actor.AddComponent<MovementController>();
                    actor.GetComponent<Rigidbody2D>().gravityScale = 0;
                    // Awake runs before adopting the Player tag so this fixture
                    // cannot load or overwrite the user's persistent loadout.
                    actor.SetActive(true); actor.tag = "Player";
                    movement.enabled = false;
                    var bombs = actor.GetComponent<BombController>();
                    bombs.enabled = true;
                    movement.SetExplosionInvulnerable(state.protection);
                    mechanism.AddComponent<BoxCollider2D>();
                    var spring = mechanism.AddComponent<SpringLauncher>();
                    spring.Configure(null, null, 3, state.prepare, 10, 2);
                    var trigger = typeof(SpringLauncher).GetMethod("OnTriggerEnter2D", BindingFlags.Instance | BindingFlags.NonPublic);
                    trigger.Invoke(spring, new object[] { collider });
                    Assert.IsTrue(movement.InputLocked);
                    if (state.prepare == 0)
                    {
                        float deadline = Time.realtimeSinceStartup + 2;
                        while (collider.enabled && Time.realtimeSinceStartup < deadline) yield return null;
                        Assert.IsFalse(collider.enabled, "Native flight must begin before cancellation.");
                    }
                    if (state.component) spring.enabled = false;
                    else mechanism.SetActive(false);
                    trigger.Invoke(spring, new object[] { collider });
                    Assert.IsFalse(movement.InputLocked);
                    Assert.IsFalse(movement.VisualOverrideActive);
                    Assert.IsTrue(collider.enabled); Assert.IsTrue(bombs.enabled);
                    Assert.AreEqual(state.protection, movement.explosionInvulnerable);
                }
                finally { Object.DestroyImmediate(mechanism); Object.DestroyImmediate(actor); }
            }
        }
        finally { NetSync.Mode = oldMode; }
        yield return new ExitPlayMode();
    }
}

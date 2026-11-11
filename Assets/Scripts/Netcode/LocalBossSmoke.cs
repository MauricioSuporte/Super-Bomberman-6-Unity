#if UNITY_EDITOR || DEBUG
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Assets.Scripts.Netcode
{
    // Opt-in native progression: damage on the authority advances the authored Mecha sequence.
    public sealed class LocalBossSmoke : MonoBehaviour
    {
        internal static readonly bool CaptureBackbuffer = Array.IndexOf(Environment.GetCommandLineArgs(), "-r06-backbuffer-captures") >= 0;

        static Scene revealScene;
        static int revealFrame;
        static float revealStart = -1, revealNext;

        internal static void ObserveFloorReveal()
        {
            var scene = SceneManager.GetActiveScene();
            if (revealScene != scene)
            { revealScene = scene; revealStart = -1; revealFrame = 0; revealNext = 0; }
            if (revealStart < 0) revealStart = Time.unscaledTime;
        }

        internal static bool TryCapture(out string filename)
        {
            filename = null;
            if (revealStart < 0 || revealFrame >= 20 || Time.unscaledTime > revealStart + 1.1f ||
                Time.unscaledTime < revealNext) return false;
            revealNext = Time.unscaledTime + .05f;
            filename = "r06-reveal-" + (revealFrame++).ToString("D2") + "-" + DateTime.UtcNow.ToString("HHmmss.fff") + ".png";
            return true;
        }

        float nextOfflineSample;
        bool offline;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-r06-offline-stage");
            if (!Debug.isDebugBuild || index < 0 || index + 1 >= args.Length) return;
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SB6_SAVE_DIRECTORY")))
                throw new InvalidOperationException("R06 offline requires an isolated SB6_SAVE_DIRECTORY");
            var go = new GameObject("R06OfflineNativeBossProbe"); DontDestroyOnLoad(go);
            var probe = go.AddComponent<LocalBossSmoke>();
            probe.StartCoroutine(probe.RunOffline(args[index + 1]));
        }

        IEnumerator RunOffline(string scene)
        {
            Application.runInBackground = true; Application.targetFrameRate = 60; QualitySettings.vSyncCount = 0;
            if (!scene.StartsWith("Stage_", StringComparison.Ordinal) ||
                SceneUtility.GetBuildIndexByScenePath("Assets/Scenes/" + scene + ".unity") < 0)
                throw new ArgumentException("R06 requires an enabled stage");
            for (int i = 0; i < 30; i++) yield return null;
            global::SaveSystem.SetActiveSlot(0); global::SaveSystem.ResetSlot(0);
            PlayerPersistentStats.ResetToDefaultsAll(); GameSession.Instance?.SetActivePlayerIds(new[] { 1, 2 });
            offline = true; SceneManager.LoadScene(scene);
            yield return new WaitForSecondsRealtime(14);
            yield return Run();
            yield return new WaitForSecondsRealtime(45);
            Debug.Log("[R06Offline] completed scene=" + scene); Application.Quit();
        }

        void LateUpdate()
        {
            if (!offline || NetSync.IsOnline) return;
            foreach (var health in FindObjectsByType<CharacterHealth>())
                if (health.TryGetComponent<PlayerIdentity>(out _))
                { health.life = 200; health.SetExternalInvulnerability(true); }
            if (!GameplayLatencyDiagnostics.Enabled || Time.unscaledTime < nextOfflineSample) return;
            nextOfflineSample = Time.unscaledTime + .05f;
            foreach (var renderer in FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include))
            {
                if (!BombermanNetworkManager.IsCoopBossRenderer(renderer)) continue;
                var light = BombermanNetworkManager.CaptureCoopAbsorption(renderer);
                var row = GameplayLatencyDiagnostics.For("r06_offline", detail:
                    "branch=" + renderer.name + ";sprite=" + (renderer.sprite != null ? renderer.sprite.name : "none") +
                    ";mask=" + (byte)renderer.maskInteraction + ";visible=" +
                    (renderer.enabled && renderer.gameObject.activeInHierarchy && !renderer.forceRenderingOff ? 1 : 0) +
                    ";light=" + (light?.amount ?? -1).ToString("R", System.Globalization.CultureInfo.InvariantCulture));
                row.position = renderer.transform.position; GameplayLatencyDiagnostics.Write(row);
            }
        }

        internal static IEnumerator Run()
        {
            if (!NetSync.ShouldSimulateLocally) yield break;
            yield return LocalProjectileSmoke.EnsureEncounter();
            if (SceneManager.GetActiveScene().name != "Stage_1-6") yield break;
            var sequence = UnityEngine.Object.FindAnyObjectByType<MechaBossSequence>();
            if (sequence == null) throw new InvalidOperationException("R06 missing Mecha sequence");
            var actors = new[] { sequence.whiteMecha, sequence.blackMecha, sequence.goldenMecha };
            for (int i = 0; i < actors.Length; i++)
            {
                var actor = actors[i];
                float deadline = Time.realtimeSinceStartup + 60;
                while (Time.realtimeSinceStartup < deadline &&
                    (actor == null || !actor.gameObject.activeInHierarchy || actor.isDead || MechaBossSequence.MechaIntroRunning))
                    yield return null;
                if (actor == null || !actor.gameObject.activeInHierarchy || actor.isDead || MechaBossSequence.MechaIntroRunning)
                    throw new InvalidOperationException("R06 Mecha never entered combat: " + i);
                Debug.Log("[LoadTest] r06 mecha combat index=" + i);
                yield return new WaitForSecondsRealtime(8);
                if (i == actors.Length - 1) break;
                var health = actor.GetComponent<CharacterHealth>();
                if (health == null) throw new InvalidOperationException("R06 Mecha has no health");
                deadline = Time.realtimeSinceStartup + 15;
                while (!actor.isDead && Time.realtimeSinceStartup < deadline)
                {
                    health.SetExternalInvulnerability(false);
                    health.TakeDamage(9999, true);
                    yield return null;
                }
                if (!actor.isDead) throw new InvalidOperationException("R06 Mecha native death did not run: " + i);
            }
            Debug.Log("[LoadTest] r06 mecha sequence verified");
        }
    }
}
#endif

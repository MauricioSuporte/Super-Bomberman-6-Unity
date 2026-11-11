#if UNITY_EDITOR || DEBUG
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Assets.Scripts.Netcode
{
    // Opt-in, isolated-save reproduction through the real hardware input owner.
    [DefaultExecutionOrder(-250)]
    public sealed class LocalPredictionSmoke : MonoBehaviour
    {
        double began = -1;
        bool offline;
        MovementController[] actors = Array.Empty<MovementController>();
        double refreshActors;
        static bool Enabled => Array.IndexOf(Environment.GetCommandLineArgs(), "-p0-prediction") >= 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!Debug.isDebugBuild || !Enabled) return;
            var go = new GameObject("P0PredictionReproduction"); DontDestroyOnLoad(go);
            var probe = go.AddComponent<LocalPredictionSmoke>();
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-p0-offline") >= 0)
            {
                if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SB6_SAVE_DIRECTORY")))
                    throw new InvalidOperationException("P0 offline requires an isolated save directory");
                probe.StartCoroutine(probe.Offline());
            }
        }

        IEnumerator Offline()
        {
            Application.runInBackground = true; Application.targetFrameRate = 60; QualitySettings.vSyncCount = 0;
            for (int i = 0; i < 30; i++) yield return null;
            global::SaveSystem.SetActiveSlot(0); global::SaveSystem.ResetSlot(0);
            PlayerPersistentStats.ResetToDefaultsAll(); GameSession.Instance?.SetActivePlayerIds(new[] { 1, 2 });
            offline = true; SceneManager.LoadScene("Stage_1-1");
            yield return new WaitForSecondsRealtime(14);
            yield return Run();
            Debug.Log("[P0] offline completed"); Application.Quit();
        }

        internal static IEnumerator Run()
        {
            if (!Enabled || !NetSync.ShouldSimulateLocally) yield break;
            var manager = FindAnyObjectByType<GameManager>();
            var source = FindAnyObjectByType<BombController>();
            if (manager == null || source == null || manager.destructibleTilemap == null)
                throw new InvalidOperationException("P0 missing native stage geometry");
            // Native terrain publication keeps client collision geometry identical.
            foreach (var cell in manager.destructibleTilemap.cellBounds.allPositionsWithin)
                if (manager.destructibleTilemap.HasTile(cell))
                    source.ClearDestructibleForEffect(manager.destructibleTilemap.GetCellCenterWorld(cell), spawnDestructiblePrefab: false, spawnHiddenObject: false);
            foreach (var health in FindObjectsByType<CharacterHealth>())
                if (health.TryGetComponent<PlayerIdentity>(out _))
                { health.life = 200; health.SetExternalInvulnerability(true); }
            Debug.Log("[P0] cohort walking sequence=horizontal-v1");
            yield return new WaitForSecondsRealtime(25);
            var pause = GamePauseController.Instance;
            var toggle = typeof(GamePauseController).GetMethod("TogglePause", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (pause == null || toggle == null) throw new InvalidOperationException("P0 missing native pause");
            toggle.Invoke(pause, null);
            if (!GamePauseController.IsPaused) throw new InvalidOperationException("P0 pause rejected");
            Debug.Log("[P0] intentional pause started");
            yield return new WaitForSecondsRealtime(2);
            toggle.Invoke(pause, null);
            if (GamePauseController.IsPaused) throw new InvalidOperationException("P0 resume rejected");
            Debug.Log("[P0] intentional pause completed");
            yield return new WaitForSecondsRealtime(30);
            foreach (var mount in FindObjectsByType<PlayerMountCompanion>())
                if (mount.TryGetComponent<PlayerIdentity>(out _)) mount.MountBlueLouie();
            Debug.Log("[P0] cohort mounted sequence=horizontal-v1");
            yield return new WaitForSecondsRealtime(55);
            Debug.Log("[P0] reproduction completed");
        }

        void Update()
        {
            if (PlayerInputManager.Instance == null || SceneManager.GetActiveScene().name != "Stage_1-1") return;
            if (!offline && (!NetSync.IsOnline || !Mirror.NetworkClient.active)) return;
            if (Time.realtimeSinceStartupAsDouble >= refreshActors)
            {
                actors = FindObjectsByType<MovementController>();
                refreshActors = Time.realtimeSinceStartupAsDouble + 1;
            }
            foreach (var motor in actors)
            {
                if (motor == null || !motor.TryGetComponent<PlayerIdentity>(out _)) continue;
                if (!offline && (!motor.TryGetComponent<NetworkPlayerInput>(out var input) || !input.isLocalPlayer)) continue;
                if (began < 0) { began = Time.realtimeSinceStartupAsDouble; Debug.Log("[P0] input started sequence=horizontal-v1"); }
                int slot = offline ? motor.PlayerId : 1;
                double phase = (Time.realtimeSinceStartupAsDouble - began) % 1.6;
                PlayerInputManager.Instance.SetSyntheticHeld(slot, PlayerAction.MoveRight, phase < .2);
                PlayerInputManager.Instance.SetSyntheticHeld(slot, PlayerAction.MoveLeft, phase >= .8 && phase < 1);
            }
        }

        void LateUpdate()
        {
            if (!offline) return;
            foreach (var actor in actors)
                if (actor != null && actor.TryGetComponent<CharacterHealth>(out var health))
                { health.life = 200; health.SetExternalInvulnerability(true); }
        }

        void OnDisable()
        {
            if (PlayerInputManager.Instance == null) return;
            PlayerInputManager.Instance.ClearSyntheticPlayer(1);
            if (offline) PlayerInputManager.Instance.ClearSyntheticPlayer(2);
        }
    }
}
#endif

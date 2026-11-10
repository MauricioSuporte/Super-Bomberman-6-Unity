using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using Mirror;
using UnityEngine;

namespace Assets.Scripts.Netcode
{
    public partial class BombermanNetworkManager
    {
#if UNITY_EDITOR || DEBUG
        static bool? r03ProbeEnabled;
        static bool R03Probe => (UnityEngine.Debug.isDebugBuild || Application.isEditor) &&
            (r03ProbeEnabled ??= Array.IndexOf(Environment.GetCommandLineArgs(), "-r03-enemy-probe") >= 0);
        readonly Dictionary<int, EnemyMovementController> r03MotionOwners = new();
        readonly Dictionary<int, string> r03RendererLabels = new();
        readonly Dictionary<int, (double next, bool visible)> r03SampleTimes = new();
        bool R03SampleDue(int id, bool visible, bool authority = false)
        {
            double now = Time.realtimeSinceStartupAsDouble;
            if (r03SampleTimes.TryGetValue(id, out var last) && visible == last.visible && (!visible || (!authority && now < last.next))) return false;
            r03SampleTimes[id] = (now + 1d / 20, visible);
            return true;
        }
        static readonly CultureInfo R03Culture = CultureInfo.InvariantCulture;
        string R03WaterDetail(SpriteRenderer renderer)
        {
            var water = CaptureCoopWater(renderer);
            return water == null ? ";water=0" : ";water=1;surface=" + water.parameters.x.ToString("R", R03Culture) +
                ";line=" + water.parameters.y.ToString("R", R03Culture) + ";useLine=" + water.parameters.z.ToString("R", R03Culture) +
                ";premultiply=" + water.parameters.w.ToString("R", R03Culture);
        }
        int R03SpriteKey(Sprite sprite)
        {
            if (sprite == null) return -1;
            if (coopSpriteIds.TryGetValue(sprite, out int id) || coopDynamicIds.TryGetValue(sprite, out id)) return id;
            foreach (var entry in coopDynamicSprites) if (entry.Value == sprite) return entry.Key;
            return -2; // Unknown is recorded, never treated as a matching approved sprite.
        }
#endif
        [Conditional("UNITY_EDITOR"), Conditional("DEBUG")]
        void ClearR03Diagnostics()
        {
#if UNITY_EDITOR || DEBUG
            r03MotionOwners.Clear(); r03RendererLabels.Clear(); r03SampleTimes.Clear();
#endif
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEBUG")]
        void TrackR03Owner(SpriteRenderer renderer, EnemyMovementController owner)
        {
#if UNITY_EDITOR || DEBUG
            if (R03Probe && renderer != null && owner != null)
                r03MotionOwners[renderer.GetEntityId().GetHashCode()] = owner;
#endif
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEBUG")]
        void RecordR03Authority()
        {
#if UNITY_EDITOR || DEBUG
            if (!R03Probe || !GameplayLatencyDiagnostics.Enabled || !IsCoopStage || coopCatalog == null || coopRenderers == null) return;
            foreach (var renderer in coopRenderers)
            {
                if (renderer == null) continue;
                int id = renderer.GetEntityId().GetHashCode();
                if (!r03MotionOwners.TryGetValue(id, out var owner))
                {
                    owner = renderer.GetComponentInParent<EnemyMovementController>(true);
                    if (owner == null) continue;
                    r03MotionOwners[id] = owner;
                }
                if (owner == null || owner is not (FrogEnemyMovementController or HopRobotMovementController or SnowManMovementController or KorobokkuruMovementController)) continue;
                bool visible = renderer.enabled && renderer.gameObject.activeInHierarchy && !renderer.forceRenderingOff;
                if (!R03SampleDue(id, visible, true)) continue;
                if (!r03RendererLabels.TryGetValue(id, out string label))
                {
                    label = "id=" + id + ";owner=" + owner.GetEntityId().GetHashCode() + ";family=" + owner.GetType().Name +
                        ";branch=" + renderer.name;
                    r03RendererLabels[id] = label;
                    GameplayLatencyDiagnostics.Write(GameplayLatencyDiagnostics.For("r03_enemy_definition", detail: label));
                }
                var row = GameplayLatencyDiagnostics.For("r03_enemy_sample", detail:
                    "id=" + id + ";ptime=" + NetworkTime.localTime.ToString("R", R03Culture) +
                    ";sprite=" + R03SpriteKey(renderer.sprite) +
                    ";visible=" + (renderer.enabled && renderer.gameObject.activeInHierarchy && !renderer.forceRenderingOff ? 1 : 0) +
                    ";paused=" + (GamePauseController.IsPaused ? 1 : 0) + R03WaterDetail(renderer));
                row.position = renderer.transform.position;
                GameplayLatencyDiagnostics.Write(row);
            }
#endif
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEBUG")]
        void RecordR03View(in CoopSpriteFrame frame, double clock, SpriteRenderer view)
        {
#if UNITY_EDITOR || DEBUG
            if (!R03Probe || !GameplayLatencyDiagnostics.Enabled || frame.enemyStepLimit <= 0 || view == null) return;
            if (!R03SampleDue(frame.id, view.enabled && view.gameObject.activeInHierarchy)) return;
            bool interpolates = coopVisualSamples.TryGetValue(frame.id, out var samples) && samples.Count > 1 && CoopVisualSample.Interpolates(samples[0], samples[1]);
            double newest = samples != null && samples.Count > 0 ? samples[^1].frame.time : frame.time;
            var row = GameplayLatencyDiagnostics.For("r03_enemy_sample", detail:
                "id=" + frame.id + ";ptime=" + clock.ToString("R", R03Culture) +
                ";state=" + (interpolates ? clock : frame.time).ToString("R", R03Culture) +
                ";newest=" + newest.ToString("R", R03Culture) + ";interpolates=" + (interpolates ? 1 : 0) +
                ";advancing=" + (frame.animation != 0 && frame.playback.speed > 0 && !frame.playback.idle && !frame.playback.frozen ? 1 : 0) +
                ";sprite=" + R03SpriteKey(view.sprite) +
                ";visible=" + (view.enabled && view.gameObject.activeInHierarchy ? 1 : 0) +
                ";paused=" + (GamePauseController.IsPaused ? 1 : 0) + R03WaterDetail(view));
            row.position = view.transform.position;
            GameplayLatencyDiagnostics.Write(row);
#endif
        }
    }
}

#if UNITY_EDITOR || DEBUG
namespace Assets.Scripts.Netcode
{
    // Opt-in native reference for both offline and server runs; no client simulation.
    [DefaultExecutionOrder(31999)]
    internal sealed class R03NativeCurveProbe : MonoBehaviour
    {
        readonly Dictionary<EnemyMovementController, float> sampled = new();
        const System.Reflection.BindingFlags Fields = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        static T Read<T>(object source, string name)
        {
            for (Type type = source.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField(name, Fields);
                if (field != null) return (T)field.GetValue(source);
            }
            throw new InvalidOperationException("Missing native R03 field " + name);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!UnityEngine.Debug.isDebugBuild) return;
            var args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-r03-native-curves") < 0) return;
            var root = new GameObject("R03NativeCurveProbe"); DontDestroyOnLoad(root);
            var probe = root.AddComponent<R03NativeCurveProbe>();
            int index = Array.IndexOf(args, "-r03-offline-stage");
            if (index >= 0 && index + 1 < args.Length) probe.StartCoroutine(probe.Offline(args[index + 1]));
        }

        System.Collections.IEnumerator Offline(string scene)
        {
            Application.runInBackground = true; Application.targetFrameRate = 60; QualitySettings.vSyncCount = 0;
            if (!scene.StartsWith("Stage_", StringComparison.Ordinal) || UnityEngine.SceneManagement.SceneUtility.GetBuildIndexByScenePath("Assets/Scenes/" + scene + ".unity") < 0)
                throw new ArgumentException("R03 offline reference requires an enabled stage");
            for (int i = 0; i < 30; i++) yield return null;
            global::SaveSystem.SetActiveSlot(0); global::SaveSystem.ResetSlot(0);
            PlayerPersistentStats.ResetToDefaultsAll();
            GameSession.Instance?.SetActivePlayerIds(new[] { 1, 2 });
            UnityEngine.SceneManagement.SceneManager.LoadScene(scene);
            yield return new WaitForSecondsRealtime(12);
            yield return BombermanNetworkManager.CheckLoadTestEnemyTransitions();
            yield return new WaitForSecondsRealtime(25);
            UnityEngine.Debug.Log("[R03Offline] completed scene=" + scene);
            Application.Quit();
        }

        void LateUpdate()
        {
            if (!NetSync.ShouldSimulateLocally || !GameplayLatencyDiagnostics.Enabled) return;
            if (!NetSync.IsOnline)
                foreach (var health in FindObjectsByType<CharacterHealth>()) if (!health.IsDead) health.life = 200;
            foreach (var enemy in FindObjectsByType<EnemyMovementController>())
            {
                if (enemy == null || !enemy.isActiveAndEnabled || Read<bool>(enemy, "isDead") || Read<bool>(enemy, "isInDamagedLoop") ||
                    (enemy.TryGetComponent<StunReceiver>(out var stun) && stun.IsStunned)) continue;
                if (sampled.TryGetValue(enemy, out float previous) && previous == Time.fixedTime) continue;
                sampled[enemy] = Time.fixedTime;
                float progress, height;
                Vector2 from, to;
                bool ease;
                AnimatedSpriteRenderer visual;
                switch (enemy)
                {
                    case FrogEnemyMovementController frog when Read<object>(frog, "state").ToString() == "Jumping":
                        progress = Read<float>(frog, "stateTimer") / 1.5f; height = 2 * frog.tileSize; ease = true;
                        from = Read<Vector2>(frog, "jumpStart"); to = Read<Vector2>(frog, "jumpTarget"); visual = Read<AnimatedSpriteRenderer>(frog, "activeSprite"); break;
                    case HopRobotMovementController hop when Read<object>(hop, "phase").ToString() == "Airborne":
                        progress = Read<float>(hop, "phaseElapsed") / Read<float>(hop, "jumpSeconds"); height = Read<float>(hop, "jumpHeight"); ease = false;
                        from = Read<Vector2>(hop, "jumpStart"); to = Read<Vector2>(hop, "targetTile"); visual = Read<AnimatedSpriteRenderer>(hop, "activeSprite"); break;
                    case SnowManMovementController snow when Read<bool>(snow, "jumping"):
                        progress = Time.fixedTime - Read<float>(snow, "jumpStartedAt"); height = 3 * snow.tileSize; ease = false;
                        from = Read<Vector2>(snow, "jumpStart"); to = Read<Vector2>(snow, "jumpEnd"); visual = Read<AnimatedSpriteRenderer>(snow, "activeSprite"); break;
                    case KorobokkuruMovementController koro when Read<object>(koro, "state").ToString() == "Jumping":
                        progress = Read<float>(koro, "stateTimer") / Read<float>(koro, "jumpSeconds"); height = Read<float>(koro, "jumpHeightInTiles") * koro.tileSize; ease = true;
                        from = Read<Vector2>(koro, "jumpStart"); to = Read<Vector2>(koro, "jumpTarget"); visual = Read<AnimatedSpriteRenderer>(koro, "jumpingSprite"); break;
                    default: continue;
                }
                progress = Mathf.Clamp01(progress);
                float horizontal = ease ? 1 - (1-progress)*(1-progress) : progress;
                Vector2 expected = Vector2.Lerp(from, to, horizontal);
                var body = enemy.GetComponent<Rigidbody2D>();
                float groundError = Vector2.Distance(body.position, expected) * 16;
                float lift = visual != null ? visual.RestingLocalPosition.y - Read<Vector3>(visual, "initialVisualLocalPosition").y - visual.RuntimeBaseLocalOffset.y : 0;
                float liftError = Mathf.Abs(lift - Mathf.Sin(progress*Mathf.PI)*height)*16;
                var row = GameplayLatencyDiagnostics.For("r03_native_curve", detail:
                    "family=" + enemy.GetType().Name + ";mode=" + (NetSync.IsOnline ? "server" : "offline") +
                    ";progress=" + progress.ToString("R", CultureInfo.InvariantCulture) +
                    ";groundErrorPx=" + groundError.ToString("R", CultureInfo.InvariantCulture) +
                    ";liftErrorPx=" + liftError.ToString("R", CultureInfo.InvariantCulture));
                row.position = body.position; GameplayLatencyDiagnostics.Write(row);
            }
            // A probe can outlive scene actors; do not retain every old scene's controller.
            if (sampled.Count > 128)
            {
                var dead = new List<EnemyMovementController>();
                foreach (var actor in sampled.Keys) if (actor == null) dead.Add(actor);
                foreach (var actor in dead) sampled.Remove(actor);
            }
        }
    }
}
#endif

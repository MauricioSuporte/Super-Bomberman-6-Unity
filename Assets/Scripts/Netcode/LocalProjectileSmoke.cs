#if UNITY_EDITOR || DEBUG
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Assets.Scripts.Netcode
{
    public struct R05VisualDefinition : NetworkMessage { public string scene, family, fixtureCase; public int id; }

    // Development-only measurement. Factories and native motion stay identical offline/server.
    [DefaultExecutionOrder(31999)]
    public sealed class LocalProjectileSmoke : MonoBehaviour
    {
        static readonly Dictionary<SpriteRenderer, string> native = new();
        static readonly Dictionary<int, string> views = new();
        static readonly Dictionary<int, string> viewCases = new();
        static readonly List<SpriteRenderer> dead = new();
        static readonly Dictionary<SpriteRenderer, string> cases = new();
        static readonly Dictionary<string, int> ordinals = new();
        static string launchCase;
        static Vector2? launchLane;
        static bool observing;
        static double lastObservedClock;
        static float captureFrom = -1, captureNext;
        static int captureFrame;
        static bool Enabled => Array.IndexOf(Environment.GetCommandLineArgs(), "-r05-projectiles") >= 0;
        static bool Measuring => GameplayLatencyDiagnostics.Enabled && Enabled;
        const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;
        static readonly CultureInfo CI = CultureInfo.InvariantCulture;
        static T Read<T>(object actor, string name)
        {
            var field = actor.GetType().GetField(name, Private);
            object value = field?.GetValue(actor);
            if (value == null && actor is Component source)
            {
                // A later room may still be inactive, so its Awake caches do not exist.
                // Read authored visual children; never activate an enemy just to get art.
                if (name == "webs")
                {
                    var found = new List<AnimatedSpriteRenderer>();
                    foreach (string child in new[] { "WebUp", "WebRigth", "WebDown", "WebLeft" })
                        foreach (var visual in source.GetComponentsInChildren<AnimatedSpriteRenderer>(true))
                            if (visual.name == child) found.Add(visual);
                    if (found.Count == 4) value = found.ToArray();
                }
                else if (typeof(T) == typeof(AnimatedSpriteRenderer))
                {
                    string child = name switch { "projectileAnimation" => "Projectile", "expand" => "Expand", "flame" => "Flame", _ => name };
                    foreach (var visual in source.GetComponentsInChildren<AnimatedSpriteRenderer>(true))
                        if (string.Equals(visual.name, child, StringComparison.OrdinalIgnoreCase)) { value = visual; break; }
                }
            }
            if (value == null) throw new InvalidOperationException("R05 missing authored reference " + actor.GetType().Name + "." + name);
            return (T)value;
        }

        internal static void Track(SpriteRenderer renderer, string family)
        {
            if (!Measuring || renderer == null) return;
            native[renderer] = family;
            if (launchCase != null && !cases.ContainsKey(renderer))
            {
                ordinals.TryGetValue(family, out int ordinal); ordinals[family] = ordinal + 1;
                cases[renderer] = launchCase + "/" + family + "/" + ordinal;
            }
            if (NetworkServer.active) NetworkServer.SendToReady(new R05VisualDefinition
                { scene = renderer.gameObject.scene.name, id = renderer.GetEntityId().GetHashCode(), family = family,
                    fixtureCase = cases.TryGetValue(renderer, out string label) ? label : null });
        }

        static void TrackFixtureTankShots()
        {
            if (!Measuring) return;
            foreach (var shot in FindObjectsByType<NetworkTankShot>())
            {
                var renderer = shot.GetComponentInChildren<SpriteRenderer>(true);
                if (renderer == null || cases.ContainsKey(renderer)) continue;
                ordinals.TryGetValue(nameof(TankShot), out int ordinal); ordinals[nameof(TankShot)] = ordinal + 1;
                string label = launchCase + "/" + nameof(TankShot) + "/" + ordinal;
                cases[renderer] = label;
                if (NetworkServer.active && shot.netId != 0)
                    NetworkServer.SendToReady(new R05VisualDefinition { scene = shot.gameObject.scene.name,
                        id = (int)shot.netId, family = nameof(TankShot), fixtureCase = label });
            }
        }

        internal static void RegisterMessages() => NetworkClient.RegisterHandler<R05VisualDefinition>(message =>
        {
            if (Measuring && !NetworkServer.active && message.scene == SceneManager.GetActiveScene().name)
            {
                views[message.id] = message.family;
                if (!string.IsNullOrEmpty(message.fixtureCase)) viewCases[message.id] = message.fixtureCase;
            }
        });

        internal static void View(CoopSpriteFrame frame, double time, SpriteRenderer view, double stateTime, double newest)
        {
            if (!Measuring || !views.TryGetValue(frame.id, out string family) || view == null) return;
            viewCases.TryGetValue(frame.id, out string label);
            if (captureFrom < 0 && label != null && label.StartsWith(SceneManager.GetActiveScene().name + "/0/", StringComparison.Ordinal) && view.enabled)
                captureFrom = Time.unscaledTime;
            bool animating = frame.animation != 0 && frame.playback.speed > 0 && !frame.playback.idle && !frame.playback.frozen;
            Sample("r05_view", frame.id, family, time, view, label, stateTime, newest, animating);
        }

        internal static void Removed(int id, double time)
        {
            if (!Measuring || !views.TryGetValue(id, out string family)) return;
            GameplayLatencyDiagnostics.Write(GameplayLatencyDiagnostics.For("r05_removed", detail:
                "id=" + id + ";family=" + family + ";ptime=" + time.ToString("R", CI)));
            views.Remove(id);
            viewCases.Remove(id);
        }

        internal static void ClearViews()
        {
            if (Measuring && observing) Epoch("r05_epoch_end", lastObservedClock);
            views.Clear(); viewCases.Clear(); observing = false; captureFrom = -1; captureNext = 0; captureFrame = 0;
        }

        static void Epoch(string kind, double clock) => GameplayLatencyDiagnostics.Write(
            GameplayLatencyDiagnostics.For(kind, detail: "ptime=" + clock.ToString("R", CI)));

        internal static void BeginPresent(double clock)
        {
            if (!Measuring) return;
            if (!observing) { observing = true; lastObservedClock = clock; Epoch("r05_epoch_begin", clock); }
            lastObservedClock = Math.Max(lastObservedClock, clock);
        }

        internal static void SendDefinitions(NetworkConnectionToClient peer)
        {
            if (!Measuring) return;
            foreach (var pair in native)
                if (pair.Key != null && pair.Key.gameObject.scene.name == SceneManager.GetActiveScene().name)
                    peer.Send(new R05VisualDefinition { scene = pair.Key.gameObject.scene.name,
                        id = pair.Key.GetEntityId().GetHashCode(), family = pair.Value,
                        fixtureCase = cases.TryGetValue(pair.Key, out string label) ? label : null });
        }

        internal static bool TryCapture(out string filename)
        {
            filename = null;
            if (!Measuring || captureFrom < 0 || captureFrame >= 16 || Time.unscaledTime > captureFrom + .9f || Time.unscaledTime < captureNext) return false;
            captureNext = Time.unscaledTime + .05f;
            filename = "r05-" + (captureFrame++).ToString("D2", CI) + "-" + DateTime.UtcNow.ToString("HHmmss.fff", CI) + ".png";
            return true;
        }

        static void Sample(string kind, int id, string family, double time, SpriteRenderer renderer, string caseOverride = null,
            double stateTime = double.NaN, double newest = double.NaN, bool animating = false)
        {
            var row = GameplayLatencyDiagnostics.For(kind, detail: "id=" + id + ";family=" + family +
                ";ptime=" + time.ToString("R", CI) + ";visible=" +
                (renderer.enabled && renderer.gameObject.activeInHierarchy && !renderer.forceRenderingOff ? 1 : 0) +
                ";sprite=" + (renderer.sprite != null ? renderer.sprite.name : "none") +
                ";case=" + (caseOverride ?? (cases.TryGetValue(renderer, out string label) ? label : "autonomous")) +
                ";state=" + (double.IsNaN(stateTime) ? time : stateTime).ToString("R", CI) +
                ";stateMeasured=" + (!double.IsNaN(stateTime) ? 1 : 0) +
                ";newest=" + (double.IsNaN(newest) ? time : newest).ToString("R", CI) + ";animating=" + (animating ? 1 : 0) +
                ";paused=" + (GamePauseController.IsPaused ? 1 : 0));
            row.position = renderer.transform.position;
            GameplayLatencyDiagnostics.Write(row);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!Debug.isDebugBuild || !Enabled) return;
            native.Clear(); views.Clear(); viewCases.Clear(); cases.Clear(); ordinals.Clear(); observing = false;
            var go = new GameObject("R05NativeProjectileProbe"); DontDestroyOnLoad(go);
            var probe = go.AddComponent<LocalProjectileSmoke>();
            var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-r05-offline-stage");
            if (index >= 0 && index + 1 < args.Length) probe.StartCoroutine(probe.Offline(args[index + 1]));
        }

        IEnumerator Offline(string scene)
        {
            Application.runInBackground = true; Application.targetFrameRate = 60; QualitySettings.vSyncCount = 0;
            if (!scene.StartsWith("Stage_", StringComparison.Ordinal) || SceneUtility.GetBuildIndexByScenePath("Assets/Scenes/" + scene + ".unity") < 0)
                throw new ArgumentException("R05 requires an enabled stage");
            for (int i = 0; i < 30; i++) yield return null;
            global::SaveSystem.SetActiveSlot(0); global::SaveSystem.ResetSlot(0); PlayerPersistentStats.ResetToDefaultsAll();
            GameSession.Instance?.SetActivePlayerIds(new[] { 1, 2 });
            SceneManager.LoadScene(scene); yield return new WaitForSecondsRealtime(14);
            yield return Run();
            Debug.Log("[R05Offline] completed scene=" + scene); Application.Quit();
        }

        void LateUpdate()
        {
            if (!GameplayLatencyDiagnostics.Enabled) return;
            if (SceneManager.GetActiveScene().name == "Stage_2-3")
                foreach (var shot in FindObjectsByType<NetworkTankShot>())
                {
                    if (NetSync.IsOnline && shot.netId == 0) continue;
                    var renderer = shot.GetComponentInChildren<SpriteRenderer>(true);
                    if (renderer == null) continue;
                    if (!NetworkServer.active && captureFrom < 0) captureFrom = Time.unscaledTime;
                    bool authority = NetSync.ShouldSimulateLocally;
                    double clock = authority ? NetworkTime.localTime : shot.ObserverPresentationActive ? NetworkBombFx.KickServerNow :
                        NetworkTime.time - BombermanNetworkManager.TransformVisualOffset(shot.netIdentity);
                    string label;
                    if (authority) cases.TryGetValue(renderer, out label); else viewCases.TryGetValue((int)shot.netId, out label);
                    Sample(authority ? "r05_native" : "r05_view", NetSync.IsOnline ? (int)shot.netId : renderer.GetEntityId().GetHashCode(),
                        nameof(TankShot), clock, renderer, label ?? "autonomous",
                        shot.ObserverPresentationActive ? shot.ObserverPresentationTime : double.NaN,
                        shot.ObserverPresentationActive ? shot.ObserverLatestTime : double.NaN);
                }
            if (!NetSync.ShouldSimulateLocally) return;
            if (!NetSync.IsOnline) Protect();
            dead.Clear();
            foreach (var pair in native)
            {
                if (pair.Key == null) { dead.Add(pair.Key); continue; }
                Sample("r05_native", pair.Key.GetEntityId().GetHashCode(), pair.Value, NetworkTime.localTime, pair.Key);
            }
            foreach (var renderer in dead) { native.Remove(renderer); cases.Remove(renderer); }
        }

        static void Protect()
        {
            foreach (var health in FindObjectsByType<CharacterHealth>())
                if (health.GetComponent<MovementController>() != null) { health.life = 200; health.SetExternalInvulnerability(true); }
        }

        public static IEnumerator Run()
        {
            if (!NetSync.ShouldSimulateLocally) yield break;
            yield return EnsureEncounter();
            float deadline = Time.realtimeSinceStartup + 60;
            var venus = FindAnyObjectByType<FreezerVenusBoss>();
            while ((venus != null && !venus.CombatActive || ClownMaskBoss.BossIntroRunning || BossIntroFlowBase.BossIntroRunning ||
                StageIntroTransition.Instance != null && StageIntroTransition.Instance.IntroRunning) && Time.realtimeSinceStartup < deadline) yield return null;
            if (ClownMaskBoss.BossIntroRunning || BossIntroFlowBase.BossIntroRunning) throw new InvalidOperationException("R05 boss intro did not release combat");
            launchLane = null;
            Protect();
            // Repeated native launches include impact/lifetime, pause, owner-independent
            // effects and short particles. Use authored visuals from this scene's catalog.
            for (int round = 0; round < 3; round++)
            {
                Protect(); ordinals.Clear(); launchCase = SceneManager.GetActiveScene().name + "/" + round;
                try { Launch(); }
                finally { launchCase = null; }
                Debug.Log("[LoadTest] r05 launch round=" + round);
                if (round == 2)
                {
                    yield return new WaitForSecondsRealtime(.05f);
                    int removed = 0;
                    foreach (var bird in FindObjectsByType<BirdRobotRedMovementController>(FindObjectsInactive.Include)) { Destroy(bird.gameObject); removed++; }
                    if (removed > 0) Debug.Log("[LoadTest] r05 rocket owners removed count=" + removed);
                    yield return new WaitForSecondsRealtime(.3f);
                }
                else yield return new WaitForSecondsRealtime(round == 1 ? .05f : .35f);
                if (round == 1)
                {
                    var pause = GamePauseController.Instance;
                    var toggle = typeof(GamePauseController).GetMethod("TogglePause", Private);
                    if (pause == null || toggle == null) throw new InvalidOperationException("R05 native pause missing");
                    toggle.Invoke(pause, null);
                    if (!GamePauseController.IsPaused) throw new InvalidOperationException("R05 pause rejected");
                    Debug.Log("[LoadTest] r05 paused"); yield return new WaitForSecondsRealtime(1.2f);
                    toggle.Invoke(pause, null);
                    if (GamePauseController.IsPaused) throw new InvalidOperationException("R05 resume rejected");
                    Debug.Log("[LoadTest] r05 resumed");
                }
                yield return new WaitForSecondsRealtime(7);
            }
            Debug.Log("[LoadTest] r05 projectiles completed scene=" + SceneManager.GetActiveScene().name);
        }

        static void Launch()
        {
            var player = FindAnyObjectByType<MovementController>();
            if (player == null) throw new InvalidOperationException("R05 fixture has no native player");
            if (!launchLane.HasValue) launchLane = FindLane(player.transform.position);
            Vector2 origin = launchLane.Value;
            foreach (var heli in FindObjectsByType<HeliMovementController>(FindObjectsInactive.Include))
                HeliProjectile.Create(origin, Vector2.right, heli.gameObject, Read<Sprite>(heli, "projectileSprite"), Read<Sprite>(heli, "projectileImpactSprite"), Read<Sprite>(heli, "projectileImpactEndSprite"));
            foreach (var ice in FindObjectsByType<IcicleMovementController>(FindObjectsInactive.Include))
                IcicleIceProjectile.Create(origin, Vector2.right, ice.gameObject, Read<Sprite>(ice, "projectileSprite"), Read<Sprite>(ice, "particleSpriteOne"), Read<Sprite>(ice, "particleSpriteTwo"));
            foreach (var jelly in FindObjectsByType<JellyFishAttackController>(FindObjectsInactive.Include))
                Instantiate(Read<GameObject>(jelly, "projectilePrefab"), origin, Quaternion.identity).GetComponent<JellyFishShot>().Init(Vector2.right, jelly.gameObject);
            foreach (var furama in FindObjectsByType<FuramaMovementController>(FindObjectsInactive.Include))
                Instantiate(Read<FuramaFlameVisual>(furama, "flameVisualPrefab"), origin, Quaternion.identity).Play(origin, Vector2.right, 1, 3, 1, .5f);
            foreach (var bubble in FindObjectsByType<BubbleFuramaMovementController>(FindObjectsInactive.Include))
            {
                var flame = Instantiate(Read<AnimatedSpriteRenderer>(bubble, "flame"), origin, Quaternion.identity);
                flame.gameObject.SetActive(true);
                flame.gameObject.AddComponent<BubbleFuramaFlame>().Launch(flame, Vector2.right, 1, 3, null, Array.Empty<Collider2D>());
            }
            foreach (var poyo in FindObjectsByType<PoyoTankEnemyMovementController>(FindObjectsInactive.Include))
            {
                // Exercise the actual enemy spawn method, including NetworkServer.Spawn.
                // A detached owner avoids activating an entire sleeping room of enemies.
                var owner = Instantiate(poyo, origin - Vector2.right - Read<Vector2>(poyo, "projectileLocalOffset"), Quaternion.identity);
                owner.gameObject.name = "R05 Poyo owner"; owner.gameObject.SetActive(true);
                typeof(PoyoTankEnemyMovementController).GetMethod("SpawnProjectile", Private).Invoke(owner, new object[] { Vector2.right });
                TrackFixtureTankShots();
                Destroy(owner.gameObject, 2);
            }
            foreach (var fish in FindObjectsByType<FishManMovementController>(FindObjectsInactive.Include))
                Instantiate(Read<GameObject>(fish, "spearPrefab"), origin, Quaternion.identity).GetComponent<FishManSpear>().Init(Vector2.right, fish.gameObject);
            foreach (var matango in FindObjectsByType<MatangoMovementController>(FindObjectsInactive.Include))
                MatangoProjectile.Create(origin, Vector2.right, 5, 5, .15f, Read<AnimatedSpriteRenderer>(matango, "projectileAnimation"));
            foreach (var faroh in FindObjectsByType<FarohMovementController>(FindObjectsInactive.Include))
            {
                var fire = Instantiate(Read<AnimatedSpriteRenderer>(faroh, "fireVisual").gameObject, origin, Quaternion.identity);
                fire.SetActive(true); fire.AddComponent<FarohFireProjectile>().Launch(Vector2.right, 2, 1, 5, .7f, LayerMask.GetMask("Player", "Bomb"));
            }
            foreach (var spider in FindObjectsByType<DespiderMovementController>(FindObjectsInactive.Include))
                DespiderWebProjectile.Launch(Read<AnimatedSpriteRenderer[]>(spider, "webs")[0], Read<AnimatedSpriteRenderer>(spider, "expand"), origin, Vector2.right, 1);
            foreach (var bird in FindObjectsByType<BirdRobotRedMovementController>(FindObjectsInactive.Include))
            {
                var rocket = Instantiate(Read<AnimatedSpriteRenderer>(bird, "rocketLeft").gameObject, origin, Quaternion.identity);
                rocket.SetActive(true);
                var animation = rocket.GetComponent<AnimatedSpriteRenderer>(); animation.enabled = true; animation.idle = false; animation.RestartAnimation();
                rocket.GetComponent<SpriteRenderer>().flipX = true;
                rocket.AddComponent<BirdRobotRocket>().Init(Vector2.right, bird.gameObject, Read<AnimatedSpriteRenderer>(bird, "explosion"), 1);
            }
            foreach (var gurun in FindObjectsByType<GurunMovementController>(FindObjectsInactive.Include))
            {
                var rocket = Instantiate(Read<AnimatedSpriteRenderer>(gurun, "rocketLeft").gameObject, origin, Quaternion.identity);
                rocket.SetActive(true);
                var animation = rocket.GetComponent<AnimatedSpriteRenderer>(); animation.enabled = true; animation.idle = false; animation.RestartAnimation();
                rocket.GetComponent<SpriteRenderer>().flipX = true;
                rocket.AddComponent<BirdRobotRocket>().Init(Vector2.right, gurun.gameObject, Read<AnimatedSpriteRenderer>(gurun, "explosion"), 1, true);
            }
            foreach (var clown in FindObjectsByType<ClownMaskBoss>())
            {
                var star = Instantiate(clown.clownStarProjectile, origin, Quaternion.identity);
                star.InitializeCurved(Vector2.up, 3, 4, 90);
                var orbit = Instantiate(clown.clownStarProjectile, origin, Quaternion.identity);
                orbit.InitializeOrbit(clown.transform, 2, 0, 120, 5, .2f, 1, .5f, 0, 1, .3f);
            }
            foreach (var sun in FindObjectsByType<SunMaskBoss>())
            {
                var heart = Instantiate(Read<SunMaskHeartProjectile>(sun, "heartPrefab"), origin, Quaternion.identity);
                heart.Initialize(2.25f, 5, .08f, 3.5f);
                var star = Instantiate(Read<StarProjectile>(sun, "starProjectilePrefab"), origin, Quaternion.identity);
                star.Initialize(Vector2.up, 5, 4);
            }
            foreach (var boss in FindObjectsByType<FreezerVenusBoss>())
            {
                if (!boss.CombatActive) throw new InvalidOperationException("R05 Venus combat not ready");
                foreach (var kind in new[] { FreezerVenusProjectile.AttackKind.Tornado, FreezerVenusProjectile.AttackKind.Ice, FreezerVenusProjectile.AttackKind.Doll })
                    FreezerVenusProjectile.Create(boss, kind, kind == FreezerVenusProjectile.AttackKind.Tornado ? boss.tornadoFrames : kind == FreezerVenusProjectile.AttackKind.Ice ? boss.iceFrames : boss.dollFrames, boss.GroundPosition + Vector3.down, Vector2.down);
                var queen = boss.GetComponent<QueenOyobeeController>();
                QueenOyobeeProjectile.Create(queen, false, 0);
                var path = new[] { (Vector2)queen.CrownOrigin, (Vector2)queen.CrownOrigin + Vector2.down * 2, (Vector2)queen.CrownOrigin + Vector2.left };
                var trail = QueenOyobeeProjectile.CreateTelegraph(queen, path); Destroy(trail, 1.5f);
                QueenOyobeeProjectile.Create(queen, true, 0, path, 1.2f);
            }
        }

        public static IEnumerator EnsureEncounter()
        {
            if (SceneManager.GetActiveScene().name != global::StageAssets.World3HallPortalSelectionController.SceneName) yield break;
            var sequence = FindAnyObjectByType<global::StageAssets.World3HallStageSevenSequence>();
            var portal = FindAnyObjectByType<global::StageAssets.World3HallPortalSelectionController>();
            if (sequence == null || portal == null || !sequence.CanPlayStageEight)
                throw new InvalidOperationException("R05 native World3 Hall encounter is not configured");
            if (!sequence.DuelActive)
            {
                typeof(global::StageAssets.World3HallPortalSelectionController).GetMethod("BeginHallBattle", Private).Invoke(portal, new object[] { true });
                Debug.Log("[LoadTest] r05 native Hall encounter requested");
            }
            float deadline = Time.realtimeSinceStartup + 70;
            while (Time.realtimeSinceStartup < deadline)
            {
                var boss = FindAnyObjectByType<FreezerVenusBoss>();
                if (sequence.DuelActive && boss != null && boss.CombatActive)
                {
                    Debug.Log("[LoadTest] r05 native Hall combat ready"); yield break;
                }
                yield return null;
            }
            throw new InvalidOperationException("R05 native Hall encounter never reached combat");
        }

        static Vector2 FindLane(Vector2 near)
        {
            var manager = FindAnyObjectByType<GameManager>();
            var arena = new FreezerVenusArena(manager);
            var cell = arena.Cell(near);
            float distance = float.PositiveInfinity; Vector2 result = default; bool found = false;
            for (int y = -4; y <= 4; y++)
                for (int x = -4; x <= 4; x++)
                {
                    var start = cell + new Vector3Int(x, y, 0);
                    bool clear = true; int blocks = 0;
                    for (int i = 0; i < 3; i++)
                    {
                        var next = start + Vector3Int.right * i;
                        var world = arena.Center(next);
                        if (!arena.IsFloor(next) || manager.indestructibleTilemap != null &&
                            manager.indestructibleTilemap.HasTile(manager.indestructibleTilemap.WorldToCell(world))) { clear = false; break; }
                        if (manager.destructibleTilemap != null && manager.destructibleTilemap.HasTile(manager.destructibleTilemap.WorldToCell(world))) blocks++;
                        // The whole corridor must be clear of players/bombs, not just
                        // its origin. Otherwise a native hit after one physics step
                        // cannot establish sustained flight coverage.
                        if (Physics2D.OverlapBox(world, Vector2.one * .6f, 0, LayerMask.GetMask("Player", "Bomb")) != null) { clear = false; break; }
                    }
                    if (!clear) continue;
                    Vector2 position = arena.Center(start);
                    if (Physics2D.OverlapBox(position, Vector2.one * .6f, 0, LayerMask.GetMask("Player", "Bomb")) != null) continue;
                    float candidate = (position-near).sqrMagnitude + blocks * 100;
                    if (candidate >= distance) continue;
                    found = true; distance = candidate; result = position;
                }
            if (!found) throw new InvalidOperationException("R05 fixture could not find a native three-cell flight lane");
            // Controlled Development setup, identical offline/online and in the A/B.
            // Remove only destructibles in this authored floor lane, never solid geometry.
            if (manager.destructibleTilemap != null)
            {
                for (int i = 0; i < 3; i++)
                    manager.destructibleTilemap.SetTile(manager.destructibleTilemap.WorldToCell(result + Vector2.right * i), null);
                if (manager.destructibleTilemap.TryGetComponent<UnityEngine.Tilemaps.TilemapCollider2D>(out var collider)) collider.ProcessTilemapChanges();
                Physics2D.SyncTransforms();
            }
            Debug.Log("[LoadTest] r05 authored lane=" + result);
            return result;
        }
    }
}
#endif

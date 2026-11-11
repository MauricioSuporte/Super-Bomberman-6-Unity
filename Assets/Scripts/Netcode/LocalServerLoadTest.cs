#if UNITY_EDITOR || DEBUG
using System;
using System.Collections.Generic;
using Mirror;
using kcp2k;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Assets.Scripts.Netcode
{
    struct PresenterSmokeReady : NetworkMessage { public string scene; }
    struct MountVisualSmokeStep : NetworkMessage { public int phase; }
    struct MechanismSmokeStep : NetworkMessage { public string kind; public int phase; public int[] renderers; public string blackout; }
    struct RopeSmokeStep : NetworkMessage { public int phase; }
    struct RopeSmokeAck : NetworkMessage { public int phase; public uint tick; }
    // Development-only command-line harness. No remote message can enable it.
    public partial class BombermanNetworkManager
    {
        bool loadTestConfigured, loadTestEnabled, loadTestStarted, loadTestSpawnsChecked, loadTestPresenterChecked, loadTestRevengeChecked, loadTestLaunchersChecked, loadTestVisualsChecked;
        string loadTestLauncherKind;
        bool loadTestMechanismChecked, loadTestCoopWorldChecked;
        readonly HashSet<int> loadTestPresenterReady = new();
        readonly Dictionary<int, RopeSmokeAck> loadTestRopeAcks = new();
        int loadTestPlayers;
        float loadTestStart, loadTestDuration, loadTestNextHealth;
        // R01 (Development only): deterministic server frame stalls. A stall longer than the client's buffer makes Mirror
        // clamp the client timeline back when snapshots resume, the condition behind the remote back-steps measured on the
        // older CPU. Same schedule with and without the remote presentation clock gives a controlled A/B.
        int loadTestStallMs, loadTestStallEvery;
        float loadTestNextStall;

        void TickLocalLoadTest()
        {
            if (!dedicatedServer || !NetworkServer.active) return;
            if (!loadTestConfigured)
            {
                loadTestConfigured = true;
                loadTestEnabled = Debug.isDebugBuild && HasArg("-load-test-server");
                if (!loadTestEnabled) return;
                if (HasArg("-load-test-presenter-removal") || HasArg("-load-test-mount-visuals") || HasArg("-load-test-launchers"))
                    NetworkServer.RegisterHandler<PresenterSmokeReady>((peer, message) =>
                    {
                        if (peer.isReady && peer.identity != null && message.scene == SceneManager.GetActiveScene().name)
                            loadTestPresenterReady.Add(peer.connectionId);
                    });
                if (HasArg("-load-test-launchers"))
                    NetworkServer.RegisterHandler<RopeSmokeAck>((peer, message) =>
                    {
                        if (peer.isReady && peer.identity != null && message.phase >= 0 && message.phase <= 4)
                            loadTestRopeAcks[peer.connectionId] = message;
                    });
                var args = Environment.GetCommandLineArgs();
                loadTestPlayers = ReadDedicatedNumber(args, "-load-test-players", RoomCapacity, 2, RoomCapacity);
                loadTestDuration = ReadDedicatedNumber(args, "-load-test-duration", 180, 30, 1800);
                loadTestStallMs = ReadDedicatedNumber(args, "-load-test-server-stall-ms", 0, 0, 1000);
                loadTestStallEvery = ReadDedicatedNumber(args, "-load-test-server-stall-every", 3, 1, 60);
                if (loadTestStallMs > 0) Debug.Log("[LoadTest] server-stall schedule ms=" + loadTestStallMs + " every=" + loadTestStallEvery + "s");
                int launcherArg = Array.IndexOf(args, "-load-test-launchers");
                if (launcherArg >= 0)
                {
                    loadTestLauncherKind = launcherArg + 1 < args.Length ? args[launcherArg + 1] : "";
                    if (loadTestLauncherKind != "arena" && loadTestLauncherKind != "spring" && loadTestLauncherKind != "cannon" && loadTestLauncherKind != "rope" && !loadTestLauncherKind.StartsWith("portal", StringComparison.Ordinal) && !loadTestLauncherKind.StartsWith("cart", StringComparison.Ordinal))
                        throw new ArgumentException("Unknown Development launcher fixture");
                }
                loadTestStart = Time.realtimeSinceStartup;
                Debug.Log("[LoadTest] Synthetic durable actors; network controls at 30 Hz. Not normal game rules.");
            }
            if (!loadTestEnabled) return;
            TickBombAbilitySmoke();
            if (Time.realtimeSinceStartup - loadTestStart > loadTestDuration)
            {
                Debug.Log("[LoadTest] completed");
                leavingRoom = true; // Mark shutdown before StopServer disconnect callbacks.
                StopServer(); Application.Quit(); return;
            }
            if (loadTestStallMs > 0 && loadTestStarted && !InLobbyScene() && Time.realtimeSinceStartup >= loadTestNextStall)
            {
                bool stall = loadTestNextStall > 0;
                loadTestNextStall = Time.realtimeSinceStartup + loadTestStallEvery;
                if (stall)
                {
                    System.Threading.Thread.Sleep(loadTestStallMs);
                    Debug.Log("[LoadTest] server-stall ms=" + loadTestStallMs + " t=" + (Time.realtimeSinceStartup - loadTestStart).ToString("F1", System.Globalization.CultureInfo.InvariantCulture));
                }
            }
            if (!loadTestStarted && InLobbyScene() && RoomPlayers.Length == loadTestPlayers)
            {
                loadTestStarted = true;
                if (IsCoopRoom)
                {
                    var args = Environment.GetCommandLineArgs();
                    int index = Array.IndexOf(args, "-load-test-scene");
                    string scene = index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
                    if (scene != null && (!scene.StartsWith("Stage_", StringComparison.Ordinal) ||
                        SceneUtility.GetBuildIndexByScenePath("Assets/Scenes/" + scene + ".unity") < 0))
                        throw new ArgumentException("Load-test scene must be an enabled stage.");
                    ServerStartCoop(scene);
                }
                else
                {
                    PlayerPersistentStats.ResetToDefaultsAll();
                    if(HasArg("-load-test-mechanisms")&&LoadTestMechanismScene()=="BattleMode_15")
                        global::SaveSystem.SetBattleModeSuddenDeathSetting(BattleModeSuddenDeathSetting.On);
                    if (HasArg("-load-test-revenge-input")) global::SaveSystem.SetBattleModeRevengeBomberEnabled(true);
                    var modes = global::SaveSystem.GetBattleModePlayerControlModes();
                    bool fillCom = !HasArg("-load-test-bomb-abilities") && !HasArg("-load-test-mechanisms") && !HasArg("-load-test-mount-visuals") && HasArg("-load-test-com") && !HasArg("-load-test-mount-cycles") && loadTestLauncherKind == null;
                    if (fillCom) global::SaveSystem.SetBattleModeComputerLevel(BattleModeComputerLevel.Hard);
                    for (int i = 0; i < modes.Length; i++) modes[i] = IsConnectedSlot(i + 1) ? BattleModePlayerControlMode.Man :
                        fillCom ? BattleModePlayerControlMode.Com : BattleModePlayerControlMode.Off;
                    global::SaveSystem.SetBattleModePlayerControlModes(modes);
                    GameSession.Instance?.SetActivePlayerIds(RoomPlayers);
                    ServerLaunchConfiguredBattle(HasArg("-load-test-mechanisms") ? LoadTestMechanismScene() : loadTestLauncherKind == "portal7" ? "BattleMode_7" : loadTestLauncherKind == "cart9" ? "BattleMode_9" : loadTestLauncherKind == "cart12" ? "BattleMode_12" : loadTestLauncherKind == "arena" ? "BattleMode_4" : loadTestLauncherKind == "rope" ? "BattleMode_11" : "BattleMode_15");
                }
            }
            // Keep combat running rather than measuring an empty arena after bot deaths.
            if (loadTestStarted && !InLobbyScene() && Time.unscaledTime >= loadTestNextHealth)
            {
                loadTestNextHealth = Time.unscaledTime + 1;
                foreach (var health in FindObjectsByType<CharacterHealth>())
                    if (!health.IsDead) health.life = 200;
                if (!loadTestSpawnsChecked && !HasArg("-load-test-bomb-abilities") && HasArg("-load-test-spawns") && !IsCoopRoom && Time.timeSinceLevelLoad > 3)
                {
                    loadTestSpawnsChecked = true;
                    StartCoroutine(CheckLoadTestSpawns());
                }
                if (!loadTestVisualsChecked && HasArg("-load-test-mount-visuals") && Time.timeSinceLevelLoad > 12 && !BattleStartPending && loadTestPresenterReady.Count >= loadTestPlayers)
                {
                    loadTestVisualsChecked = true; StartCoroutine(CheckLoadTestMountVisuals());
                }
                if (!loadTestLaunchersChecked && loadTestLauncherKind != null && Time.timeSinceLevelLoad > 12 &&
                    (!IsCoopRoom || OnlineCoopPlayersReady) && !BattleStartPending &&
                    ((loadTestLauncherKind != "rope" && !loadTestLauncherKind.StartsWith("portal", StringComparison.Ordinal) && !loadTestLauncherKind.StartsWith("cart", StringComparison.Ordinal)) || loadTestPresenterReady.Count >= loadTestPlayers))
                {
                    loadTestLaunchersChecked = true;
                    StartCoroutine(CheckLoadTestLaunchers());
                }
                if (!loadTestRevengeChecked && HasArg("-load-test-revenge-input") && !IsCoopRoom && Time.timeSinceLevelLoad > 12)
                {
                    loadTestRevengeChecked = true;
                    StartCoroutine(CheckLoadTestRevengeInput());
                }
                if (!loadTestMechanismChecked && HasArg("-load-test-mechanisms") && Time.timeSinceLevelLoad > 15 &&
                    NetworkServer.connections.Count >= loadTestPlayers && (!IsCoopRoom || OnlineCoopPlayersReady))
                {
                    loadTestMechanismChecked = true; StartCoroutine(CheckLoadTestMechanism());
                }
                if (!loadTestCoopWorldChecked && HasArg("-load-test-coop-world") && IsCoopRoom && Time.timeSinceLevelLoad > 12 &&
                    OnlineCoopPlayersReady && NetworkServer.connections.Count >= loadTestPlayers)
                {
                    loadTestCoopWorldChecked = true;
                    StartCoroutine(CheckLoadTestCoopWorld());
                }
                if (!loadTestPresenterChecked && HasArg("-load-test-presenter-removal") && IsCoopRoom &&
                    Time.timeSinceLevelLoad > 12 && OnlineCoopPlayersReady && loadTestPresenterReady.Count >= loadTestPlayers &&
                    NetworkServer.connections.Count >= loadTestPlayers)
                {
                    loadTestPresenterChecked = true;
                    StartCoroutine(CheckLoadTestPresenterRemoval());
                }
            }
        }

        // R02: native co-op world for 2 real clients: steady autonomous actors, a native pause, a stage reload
        // (scene cleanup + fresh late-observer snapshot) and a second pause. No gameplay rule is changed.
        System.Collections.IEnumerator CheckLoadTestCoopWorld()
        {
            string scene = SceneManager.GetActiveScene().name;
            static string U() => " u=" + (DateTime.UtcNow.Ticks / 10000 - 63900000000000L).ToString(System.Globalization.CultureInfo.InvariantCulture);
            Debug.Log("[LoadTest] coop-world started scene=" + scene + U());
            if (HasArg("-load-test-enemies")) yield return CheckLoadTestEnemyTransitions();
            if (HasArg("-r06-bosses")) yield return LocalBossSmoke.Run();
            if (HasArg("-r08-environment")) yield return LocalEnvironmentSmoke.Run();
            if (HasArg("-r05-projectiles")) yield return LocalProjectileSmoke.Run();
            // 30 s of steady native world first: the pre-registered cost window ends at the first pause.
            // R02 controlled reproduction: "-load-test-hitch-ms N" blocks the server main thread N ms every 2.5 s (12 times)
            // inside that window, so long server frames happen at known instants. Without the flag nothing changes.
            int hitchArg = Array.IndexOf(Environment.GetCommandLineArgs(), "-load-test-hitch-ms");
            int hitchMs = 0;
            if (hitchArg >= 0 && hitchArg + 1 < Environment.GetCommandLineArgs().Length)
                int.TryParse(Environment.GetCommandLineArgs()[hitchArg + 1], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out hitchMs);
            if (hitchMs <= 0) yield return new WaitForSecondsRealtime(30f);
            else
                for (int hitch = 1; hitch <= 12; hitch++)
                {
                    yield return new WaitForSecondsRealtime(2.5f);
                    Debug.Log("[LoadTest] coop-world hitch index=" + hitch + " ms=" + hitchMs + U());
                    System.Threading.Thread.Sleep(Mathf.Clamp(hitchMs, 1, 1000));
                }
            ToggleNativePause(true); Debug.Log("[LoadTest] coop-world paused index=1" + U());
            yield return new WaitForSecondsRealtime(2f);
            ToggleNativePause(false); Debug.Log("[LoadTest] coop-world resumed index=1" + U());
            yield return new WaitForSecondsRealtime(10f);
            Debug.Log("[LoadTest] coop-world reload scene=" + scene + U());
            ServerChangeScene(scene);
            yield return null;
            float deadline = Time.realtimeSinceStartup + 60f;
            while (Time.realtimeSinceStartup < deadline && (NetworkServer.isLoadingScene || SceneManager.GetActiveScene().name != scene ||
                Time.timeSinceLevelLoad < 12f || !OnlineCoopPlayersReady)) yield return null;
            if (!OnlineCoopPlayersReady || SceneManager.GetActiveScene().name != scene) throw new InvalidOperationException("Co-op world reload did not become ready");
            Debug.Log("[LoadTest] coop-world reloaded scene=" + scene + U());
            if (HasArg("-r05-projectiles") || HasArg("-r06-bosses")) yield return LocalProjectileSmoke.EnsureEncounter();
            yield return new WaitForSecondsRealtime(3f);
            ToggleNativePause(true); Debug.Log("[LoadTest] coop-world paused index=2" + U());
            yield return new WaitForSecondsRealtime(2f);
            ToggleNativePause(false); Debug.Log("[LoadTest] coop-world resumed index=2" + U());
            yield return new WaitForSecondsRealtime(5f);
            Debug.Log("[LoadTest] coop-world completed scene=" + scene + U());
        }

        // R03: exercise the real scene enemies, not a client-side clone or an invented path.
        internal static System.Collections.IEnumerator CheckLoadTestEnemyTransitions()
        {
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            bool HasPriorityEnemy()
            {
                foreach (var actor in FindObjectsByType<EnemyMovementController>())
                    if (actor is FrogEnemyMovementController or HopRobotMovementController or SnowManMovementController or KorobokkuruMovementController) return true;
                return false;
            }
            // World 3 suspends enemies in unvisited rooms. Traverse the authored transition,
            // including camera/room lifecycle replication, rather than claiming coverage of inactive prefabs.
            for (int room = 0; !HasPriorityEnemy() && room < 3; room++)
            {
                global::StageAssets.World3RoomTwoTransition transition = null;
                foreach (var candidate in FindObjectsByType<global::StageAssets.World3RoomTwoTransition>())
                    if ((Camera)typeof(global::StageAssets.World3RoomTwoTransition).GetField("sourceRoomCamera", flags).GetValue(candidate) == Camera.main)
                    { transition = candidate; break; }
                if (transition == null) break;
                var type = typeof(global::StageAssets.World3RoomTwoTransition);
                type.GetField("transitionStarted", flags).SetValue(transition, true);
                type.GetField("transitionSourcePosition", flags).SetValue(transition, (Vector2)Camera.main.transform.position);
                yield return (System.Collections.IEnumerator)type.GetMethod("TransitionRoutine", flags).Invoke(transition, null);
                foreach (var peer in NetworkServer.connections.Values)
                    if (peer.identity != null && peer.identity.TryGetComponent<NetworkPlayerInput>(out var input)) ServerReconcileFixturePlayer(input);
                yield return new WaitForSecondsRealtime(1);
            }
            if (!HasPriorityEnemy()) throw new InvalidOperationException("R03 fixture never reached an active priority enemy");
            var enemies = FindObjectsByType<EnemyMovementController>();
            foreach (var enemy in enemies)
            {
                if (enemy == null) continue;
                Debug.Log("[LoadTest] r03 native family=" + enemy.GetType().Name);
                // SnowMan normally needs a bomb ahead. Ask its native eligibility/landing path;
                // every selected destination still passes the real water/obstacle/body queries.
                if (enemy is SnowManMovementController snow)
                {
                    bool began = (bool)typeof(SnowManMovementController).GetField("jumping", flags).GetValue(snow);
                    var directionField = typeof(EnemyMovementController).GetField("direction", flags);
                    var oldDirection = directionField.GetValue(snow);
                    foreach (var direction in new[] { Vector2.up, Vector2.down, Vector2.left, Vector2.right })
                    {
                        if (began) break;
                        directionField.SetValue(snow, direction);
                        if ((bool)typeof(SnowManMovementController).GetMethod("TryBeginJump", flags).Invoke(snow, null))
                        { began = true; break; }
                    }
                    if (!began) directionField.SetValue(snow, oldDirection);
                    Debug.Log("[LoadTest] r03 snow native jump=" + began);
                }
            }
            yield return new WaitForSecondsRealtime(.25f);
            ToggleNativePause(true); Debug.Log("[LoadTest] r03 pause in native motion");
            yield return new WaitForSecondsRealtime(1.2f);
            ToggleNativePause(false); Debug.Log("[LoadTest] r03 resume native motion");
            yield return new WaitForSecondsRealtime(4f);
            foreach (var enemy in enemies)
                if (enemy != null && enemy.TryGetComponent<StunReceiver>(out var stun)) stun.Stun(.5f);
            Debug.Log("[LoadTest] r03 native stun");
            yield return new WaitForSecondsRealtime(2f);
            // Kill one instance, leave its siblings alive to measure normal walking/jump curves.
            if (HasArg("-r03-enemy-interruptions"))
            {
                foreach (var enemy in enemies)
                {
                    if (enemy == null || enemy is not (FrogEnemyMovementController or HopRobotMovementController or SnowManMovementController or KorobokkuruMovementController)) continue;
                    if (enemy is HopRobotMovementController hop)
                    {
                        bool crushed = hop.TryBarrelCrushStun(.5f);
                        Debug.Log("[LoadTest] r03 native barrel family=" + enemy.GetType().Name + " applied=" + crushed);
                        yield return new WaitForSecondsRealtime(.75f);
                    }
                    // Exercise real GameObject teardown before death, including separately parented shadows.
                    enemy.gameObject.SetActive(false);
                    yield return null;
                    enemy.gameObject.SetActive(true);
                    yield return new WaitForSecondsRealtime(.25f);
                    Debug.Log("[LoadTest] r03 native disable and death family=" + enemy.GetType().Name);
                    enemy.Kill();
                }
                yield return new WaitForSecondsRealtime(3f);
            }
            else if (enemies.Length > 0 && enemies[0] != null)
            {
                Debug.Log("[LoadTest] r03 native death family=" + enemies[0].GetType().Name);
                enemies[0].Kill();
            }
            Debug.Log("[LoadTest] r03 transitions completed");
        }

        static void ToggleNativePause(bool paused)
        {
            var pause = GamePauseController.Instance;
            var toggle = typeof(GamePauseController).GetMethod("TogglePause", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (pause == null || toggle == null) throw new InvalidOperationException("Missing native pause");
            toggle.Invoke(pause, null);
            if (GamePauseController.IsPaused != paused) throw new InvalidOperationException(paused ? "Native pause rejected" : "Native resume rejected");
        }

        System.Collections.IEnumerator CheckLoadTestMountVisuals()
        {
            int index = 0;
            CharacterHealth lastHealth = null; PlayerMountCompanion lastCompanion = null; MovementController lastMover = null;
            foreach (var peer in NetworkServer.connections.Values)
            {
                var actor = peer.identity;
                if (actor == null) throw new InvalidOperationException("Visual fixture lost its actor");
                var mover = actor.GetComponent<MovementController>(); var companion = actor.GetComponent<PlayerMountCompanion>();
                var health = actor.GetComponent<CharacterHealth>(); bool protectedBefore = health.HasExternalInvulnerability;
                health.SetExternalInvulnerability(true);
                foreach (bool mounted in new[] { false, true })
                {
                    if (mounted)
                    {
                        AutoItemDatabase.BuildIfNeeded(); var prefab = AutoItemDatabase.Get(ItemType.BlueLouieEgg);
                        if (prefab == null) throw new InvalidOperationException("Missing native Blue egg");
                        var egg = Instantiate(prefab, actor.transform.position, Quaternion.identity); NetSpawn.Server(egg.gameObject);
                        yield return new WaitForSecondsRealtime(1.8f);
                        if (!mover.IsMounted || companion.GetMountedLouieType() != MountedType.Blue) throw new InvalidOperationException("Native egg pickup did not mount Blue");
                        Debug.Log("[LoadTest] visual egg mounted player=" + mover.PlayerId);
                    }
                    // Start from confirmed locomotion, rather than an automatic AFK
                    // pose left running while clients warmed their render pipeline.
                    peer.Send(new MountVisualSmokeStep { phase = 2 }); yield return new WaitForSecondsRealtime(.5f);
                    peer.Send(new MountVisualSmokeStep()); yield return new WaitForSecondsRealtime(.2f);
                    if (!mover.CanPredictNetworkMovement) throw new InvalidOperationException("Visual fixture did not reach locomotion before emote");
                    peer.Send(new MountVisualSmokeStep { phase = 1 });
                    float emoteDeadline = Time.realtimeSinceStartup + 2f;
                    while (mover.CanPredictNetworkMovement && Time.realtimeSinceStartup < emoteDeadline) yield return null;
                    if (mover.CanPredictNetworkMovement) throw new InvalidOperationException("Native emote never took presentation ownership");
                    // Capture can briefly stall a GPU batch client beyond the normal
                    // held-input grace. Assert the actual start, then release explicitly.
                    yield return new WaitForSecondsRealtime(.15f);
                    peer.Send(new MountVisualSmokeStep { phase = 2 }); yield return new WaitForSecondsRealtime(.5f);
                    peer.Send(new MountVisualSmokeStep()); yield return new WaitForSecondsRealtime(.2f);
                    if (!mover.CanPredictNetworkMovement) throw new InvalidOperationException("Emote did not release on native movement input");
                    Debug.Log("[LoadTest] visual emote released player=" + mover.PlayerId + " mounted=" + mounted);
                    yield return new WaitForSecondsRealtime(5.6f);
                    if (mover.CanPredictNetworkMovement) throw new InvalidOperationException("Native AFK pose never started");
                    peer.Send(new MountVisualSmokeStep { phase = 2 }); yield return new WaitForSecondsRealtime(.5f);
                    peer.Send(new MountVisualSmokeStep()); yield return new WaitForSecondsRealtime(.2f);
                    if (!mover.CanPredictNetworkMovement) throw new InvalidOperationException("AFK pose did not release on native movement input");
                    Debug.Log("[LoadTest] visual afk released player=" + mover.PlayerId + " mounted=" + mounted);
                }
                companion.GetMountedLouieHealth().StopInvulnerability(); companion.GetMountedLouieHealth().SetExternalInvulnerability(false);
                companion.OnMountedLouieHit(999, false); yield return new WaitForSecondsRealtime(2.2f);
                if (companion.HasMountedLouie()) throw new InvalidOperationException("Native regular mount death did not detach");
                Debug.Log("[LoadTest] visual death player=" + mover.PlayerId + " explosion=False");
                companion.MountPinkLouie(); yield return new WaitForSecondsRealtime(1.4f);
                peer.Send(new MountVisualSmokeStep { phase = 4 });
                float jumpDeadline = Time.realtimeSinceStartup + 2f;
                while (!actor.GetComponent<PinkLouieJumpAbility>().JumpActive && Time.realtimeSinceStartup < jumpDeadline) yield return null;
                if (!actor.GetComponent<PinkLouieJumpAbility>().JumpActive) throw new InvalidOperationException("Native Pink jump did not start from client input");
                peer.Send(new MountVisualSmokeStep { phase = 2 }); yield return new WaitForSecondsRealtime(1.2f);
                peer.Send(new MountVisualSmokeStep { phase = 5 }); yield return new WaitForSecondsRealtime(.5f);
                peer.Send(new MountVisualSmokeStep()); yield return new WaitForSecondsRealtime(.2f);
                Debug.Log("[LoadTest] visual pink sides player=" + mover.PlayerId);
                companion.GetMountedLouieHealth().StopInvulnerability(); companion.GetMountedLouieHealth().SetExternalInvulnerability(false);
                companion.OnMountedLouieHit(999, true); yield return new WaitForSecondsRealtime(2.2f);
                if (companion.HasMountedLouie()) throw new InvalidOperationException("Native explosion mount death did not detach");
                Debug.Log("[LoadTest] visual death player=" + mover.PlayerId + " explosion=True");
                companion.RestoreMounted(index == 0 ? MountedType.Mole : MountedType.Tank); yield return new WaitForSecondsRealtime(.4f);
                foreach (int phase in new[] { 2, 5, 6, 7 }) { peer.Send(new MountVisualSmokeStep { phase = phase }); yield return new WaitForSecondsRealtime(.4f); }
                peer.Send(new MountVisualSmokeStep());
                Debug.Log("[LoadTest] visual head player=" + mover.PlayerId + " type=" + companion.GetMountedLouieType());
                // R01: native pause while the player is mounted and walking, then resume; observers must keep pose and mount.
                peer.Send(new MountVisualSmokeStep { phase = 2 }); yield return new WaitForSecondsRealtime(.3f);
                ToggleNativePause(true); yield return new WaitForSecondsRealtime(1f);
                ToggleNativePause(false); yield return new WaitForSecondsRealtime(.4f);
                peer.Send(new MountVisualSmokeStep()); yield return new WaitForSecondsRealtime(.3f);
                Debug.Log("[LoadTest] visual pause mounted player=" + mover.PlayerId + " type=" + companion.GetMountedLouieType());
                // R01: native 1 s stun of a rider facing up (round-reload_r1 showed two Mole heads for a whole stun).
                if (mover.TryGetComponent<StunReceiver>(out var stunReceiver))
                {
                    var seated = new List<AnimatedSpriteRenderer>();
                    string Seated()
                    {
                        mover.CollectMountedBodyRenderers(seated);
                        var shown = new List<string>();
                        foreach (var r in seated) if (r.TryGetComponent<SpriteRenderer>(out var s) && s.enabled && s.gameObject.activeInHierarchy) shown.Add(r.name);
                        return string.Join(",", shown);
                    }
                    mover.ForceFacingDirection(Vector2.up); yield return new WaitForSecondsRealtime(.3f);
                    string before = Seated();
                    stunReceiver.ForceStun(1f, false); yield return new WaitForSecondsRealtime(.5f);
                    string during = Seated();
                    yield return new WaitForSecondsRealtime(.9f);
                    string after = Seated();
                    Debug.Log("[LoadTest] visual stun mounted player=" + mover.PlayerId + " type=" + companion.GetMountedLouieType() + " before=" + before + " during=" + during + " after=" + after);
                    if (during.Contains(",")) throw new InvalidOperationException("Stunned rider shows more than one seated pose: " + during);
                }
                health.SetExternalInvulnerability(protectedBefore); index++; lastHealth = health; lastCompanion = companion; lastMover = mover;
            }
            if (index != 2) throw new InvalidOperationException("Visual fixture requires two owners");
            Debug.Log("[LoadTest] mount visuals completed players=2");
            // R01: native death of the last owner while it is still mounted (Mole/Tank). In a two-player battle this also ends
            // the round, so the reload exercises pose/mount cleanup for a player that died mounted. The outcome is logged as is.
            yield return new WaitForSecondsRealtime(1f);
            if (lastHealth != null && lastCompanion != null)
            {
                bool mountedBefore = lastMover.IsMounted; string typeBefore = lastCompanion.GetMountedLouieType().ToString();
                lastHealth.StopInvulnerability(); lastHealth.SetExternalInvulnerability(false);
                lastHealth.TakeDamage(lastHealth.life, true);
                Debug.Log("[LoadTest] visual mounted death player=" + lastMover.PlayerId + " dead=" + lastHealth.IsDead + " mountedBefore=" + mountedBefore +
                    " typeBefore=" + typeBefore + " mountedAfter=" + lastMover.IsMounted);
            }
        }

        System.Collections.IEnumerator WaitForNeutralRopeInput(NetworkConnectionToClient peer, NetworkPlayerInput input, MovementController mover)
        {
            uint before = input.CurrentInputTick;
            peer.Send(new RopeSmokeStep());
            float deadline = Time.unscaledTime + 5;
            bool NeutralConfirmed() => loadTestRopeAcks.TryGetValue(peer.connectionId, out var ack) && ack.phase == 0 &&
                NetworkInputHistory.IsNewer(ack.tick, before) && (input.CurrentInputTick == ack.tick || NetworkInputHistory.IsNewer(input.CurrentInputTick, ack.tick)) &&
                PlayerInputManager.Instance != null && GetRopeFixtureDirection(mover.PlayerId) == Vector2.zero;
            while (!NeutralConfirmed() && Time.unscaledTime < deadline) yield return null;
            if (!NeutralConfirmed()) throw new InvalidOperationException("Rope fixture did not drain the preceding directional input");
        }
        static Vector2 GetRopeFixtureDirection(int player)
        {
            var input = PlayerInputManager.Instance;
            return new Vector2((input.Get(player, PlayerAction.MoveRight) ? 1 : 0) - (input.Get(player, PlayerAction.MoveLeft) ? 1 : 0),
                (input.Get(player, PlayerAction.MoveUp) ? 1 : 0) - (input.Get(player, PlayerAction.MoveDown) ? 1 : 0));
        }

        System.Collections.IEnumerator CheckLoadTestPortals()
        {
            var portal = FindAnyObjectByType<BattleMode7PortalController>();
            if (portal == null) throw new InvalidOperationException("Missing native player portal");
            var cells = new List<Vector2Int>(); portal.CopyPortalCells(cells);
            if (cells.Count < 2 || !portal.TryGetPortalWorldCenter(cells[0], out var entry) ||
                !portal.TryGetClockwiseDestination(cells[0], out var destinationCell) || !portal.TryGetPortalWorldCenter(destinationCell, out var exit))
                throw new InvalidOperationException("Missing authored portal endpoints");
            int owners = 0;
            foreach (var peer in NetworkServer.connections.Values)
            {
                var actor = peer.identity;
                if (actor == null) throw new InvalidOperationException("Missing portal actor");
                var mover = actor.GetComponent<MovementController>(); var input = actor.GetComponent<NetworkPlayerInput>();
                var health = actor.GetComponent<CharacterHealth>(); bool protection = health.HasExternalInvulnerability;
                health.SetExternalInvulnerability(true);
                actor.GetComponent<PlayerMountCompanion>().RestoreMounted(owners == 0 ? MountedType.None : MountedType.Pink);
                yield return new WaitForSecondsRealtime(.6f);
                peer.Send(new RopeSmokeStep { phase = 2 }); yield return new WaitForSecondsRealtime(.4f);
                yield return WaitForNeutralRopeInput(peer, input, mover);
                Vector2 origin = mover.Rigidbody.position;
                mover.SnapToWorldPoint(entry, roundToGrid: false); ServerReconcileFixturePlayer(input);
                float deadline = Time.unscaledTime + 5;
                while (!mover.InputLocked && Time.unscaledTime < deadline) yield return null;
                if (!mover.InputLocked) throw new InvalidOperationException("Native portal did not claim the actor");
                deadline = Time.unscaledTime + 10;
                while (mover.InputLocked && Time.unscaledTime < deadline) yield return null;
                if (mover.InputLocked || Vector2.Distance(mover.Rigidbody.position, exit) > .25f || !health.HasExternalInvulnerability)
                    throw new InvalidOperationException("Native portal failed destination/control/protection validation");
                var expected = GameplayLatencyDiagnostics.For("portal_smoke_expected", mover, detail: "fixture=" + loadTestLauncherKind);
                expected.bombPosition = entry; expected.delta = exit - entry; expected.position = exit; GameplayLatencyDiagnostics.Write(expected);
                uint before = input.CurrentInputTick; peer.Send(new RopeSmokeStep { phase = 4 }); deadline = Time.unscaledTime + 10;
                bool Released() => loadTestRopeAcks.TryGetValue(peer.connectionId, out var ack) && ack.phase == 4 && NetworkInputHistory.IsNewer(ack.tick, before);
                while (!Released() && Time.unscaledTime < deadline) yield return null;
                if (!Released()) throw new InvalidOperationException("Portal owner did not release authority presentation");
                yield return new WaitForSecondsRealtime(.6f);
                mover.SnapToWorldPoint(origin, roundToGrid: false); ServerReconcileFixturePlayer(input);
                peer.Send(new RopeSmokeStep { phase = 2 }); yield return new WaitForSecondsRealtime(.4f);
                yield return WaitForNeutralRopeInput(peer, input, mover);
                if (!mover.CanPredictNetworkMovement) throw new InvalidOperationException("Portal did not return to native walking");
                health.SetExternalInvulnerability(protection); owners++;
                Debug.Log("[LoadTest] portal input restored player=" + mover.PlayerId);
            }
            if (owners != 2) throw new InvalidOperationException("Portal fixture requires two owners");
            Debug.Log("[LoadTest] launchers completed kind=" + loadTestLauncherKind + " players=2");
        }

        System.Collections.IEnumerator CheckLoadTestMinecarts()
        {
            var carts = FindObjectsByType<BattleMode9MinecartController>();
            if (carts.Length == 0) throw new InvalidOperationException("Missing native minecart");
            // Stage 3-2 Room 1 has the audited wrap and jump; choose its authored station.
            BattleMode9MinecartController cart = null;
            foreach (var candidate in carts)
                if (loadTestLauncherKind != "cart32" || candidate.StationTile == new Vector2Int(4, -2)) { cart = candidate; break; }
            if (cart == null) throw new InvalidOperationException("Missing Room 1 shuttle");
            int owners = 0;
            foreach (var peer in NetworkServer.connections.Values)
            {
                var actor = peer.identity;
                if (actor == null) throw new InvalidOperationException("Missing minecart actor");
                var mover = actor.GetComponent<MovementController>(); var input = actor.GetComponent<NetworkPlayerInput>();
                var health = actor.GetComponent<CharacterHealth>(); bool protection = health.HasExternalInvulnerability;
                health.SetExternalInvulnerability(true);
                actor.GetComponent<PlayerMountCompanion>().RestoreMounted(owners == 0 ? MountedType.None : MountedType.Pink);
                yield return new WaitForSecondsRealtime(.6f);
                // Native AFK owns the visual after renderer warmup. Cancel it
                // through real input before placing a standing actor at the station.
                peer.Send(new RopeSmokeStep { phase = 2 }); yield return new WaitForSecondsRealtime(.4f);
                yield return WaitForNeutralRopeInput(peer, input, mover);
                if (mover.VisualOverrideActive) throw new InvalidOperationException("Minecart fixture did not leave the native AFK pose");
                Vector2 origin = mover.Rigidbody.position;
                cart.enabled = true;
                // The cart rests exactly at its current station before boarding.
                mover.SnapToWorldPoint(cart.transform.position, roundToGrid: false); ServerReconcileFixturePlayer(input);
                float deadline = Time.unscaledTime + 5;
                while (!cart.RideActive && Time.unscaledTime < deadline) yield return null;
                if (cart.CurrentRider != mover) throw new InvalidOperationException("Native station did not board the intended rider; block=" + mover.NetworkPredictionBlockReason + ";position=" + mover.Rigidbody.position + ";cart=" + cart.transform.position + ";station=" + cart.StationTile);
                Debug.Log("[LoadTest] minecart boarded player=" + mover.PlayerId);
                deadline = Time.unscaledTime + 30;
                while (cart.RideActive && Time.unscaledTime < deadline) yield return null;
                if (cart.RideActive || mover.InputLocked || mover.ExternalMovementOverride)
                    throw new InvalidOperationException("Native minecart did not release the rider");
                if (!health.HasExternalInvulnerability) throw new InvalidOperationException("Minecart erased preexisting protection");
                Debug.Log("[LoadTest] minecart released player=" + mover.PlayerId);
                uint beforeRelease = input.CurrentInputTick;
                peer.Send(new RopeSmokeStep { phase = 3 });
                deadline = Time.unscaledTime + 12;
                bool PresentedExit() => loadTestRopeAcks.TryGetValue(peer.connectionId, out var ack) && ack.phase == 3 && NetworkInputHistory.IsNewer(ack.tick, beforeRelease);
                while (!PresentedExit() && Time.unscaledTime < deadline) yield return null;
                if (!PresentedExit()) throw new InvalidOperationException("Client did not release its approved minecart presentation");
                mover.SnapToWorldPoint(origin, roundToGrid: false); ServerReconcileFixturePlayer(input);
                peer.Send(new RopeSmokeStep { phase = 2 }); yield return new WaitForSecondsRealtime(.4f);
                yield return WaitForNeutralRopeInput(peer, input, mover);
                if (!mover.CanPredictNetworkMovement) throw new InvalidOperationException("Native locomotion did not resume after the presented exit");
                Debug.Log("[LoadTest] minecart input restored player=" + mover.PlayerId);
                yield return new WaitForSecondsRealtime(1);
                health.SetExternalInvulnerability(protection); owners++;
            }
            if (owners != 2) throw new InvalidOperationException("Minecart fixture requires two owners");
            Debug.Log("[LoadTest] launchers completed kind=" + loadTestLauncherKind + " players=2");
        }

        System.Collections.IEnumerator CheckLoadTestLaunchers()
        {
            if (loadTestLauncherKind.StartsWith("portal", StringComparison.Ordinal))
            { yield return CheckLoadTestPortals(); yield break; }
            if (loadTestLauncherKind.StartsWith("cart", StringComparison.Ordinal))
            { yield return CheckLoadTestMinecarts(); yield break; }
            int index = 0;
            var positions = new List<Vector2>();
            foreach (var peer in NetworkServer.connections.Values)
            {
                if (peer.identity == null) throw new InvalidOperationException("Launcher fixture requires two live actors");
                var mover = peer.identity.GetComponent<MovementController>();
                var input = peer.identity.GetComponent<NetworkPlayerInput>();
                var companion = peer.identity.GetComponent<PlayerMountCompanion>();
                var health = peer.identity.GetComponent<CharacterHealth>();
                if (mover == null || input == null || companion == null || health == null) throw new InvalidOperationException("Missing launcher actor components");
                bool priorProtection = health.HasExternalInvulnerability;
                health.SetExternalInvulnerability(true);
                Vector2 original = mover.Rigidbody.position;
                companion.RestoreMounted(index == 0 ? MountedType.None : MountedType.Pink);
                yield return new WaitForSecondsRealtime(.5f);
                uint previous = input.ApprovedLaunchSnapshot.sequence;
                MonoBehaviour mechanism = null;
                Vector2 entry;
                if (loadTestLauncherKind == "rope")
                {
                    mechanism = FindAnyObjectByType<BattleMode11ImpulseRopeController>();
                    if (mechanism == null) throw new InvalidOperationException("Rope fixture missing its native controller");
                    mechanism.enabled = true;
                    entry = new Vector2(index == 0 ? -7 : 5, 4);
                }
                else if (loadTestLauncherKind == "arena")
                {
                    var arena = FindAnyObjectByType<BattleMode4SpringTileController>();
                    if (arena == null) throw new InvalidOperationException("Arena fixture missing its native spring controller");
                    arena.CopySpringWorldPositions(positions);
                    if (positions.Count == 0) throw new InvalidOperationException("Arena fixture has no native spring tile");
                    entry = positions[index % positions.Count];
                }
                else
                {
                    mechanism = loadTestLauncherKind == "cannon" ? (MonoBehaviour)FindAnyObjectByType<CannonLauncher>() : FindAnyObjectByType<SpringLauncher>();
                    if (mechanism == null || !mechanism.TryGetComponent<Collider2D>(out var trigger))
                        throw new InvalidOperationException("Scene does not contain the requested native launcher " + loadTestLauncherKind);
                    mechanism.enabled = true;
                    entry = trigger.bounds.center;
                }
                if (loadTestLauncherKind == "rope") yield return WaitForNeutralRopeInput(peer, input, mover);
                mover.SnapToWorldPoint(entry, roundToGrid: false);
                ServerReconcileFixturePlayer(input);
                if (loadTestLauncherKind == "rope") peer.Send(new RopeSmokeStep { phase = 1 });
                float deadline = Time.unscaledTime + 10;
                while (Time.unscaledTime < deadline && (!input.ApprovedLaunchSnapshot.active || input.ApprovedLaunchSnapshot.sequence == previous)) yield return null;
                var launch = input.ApprovedLaunchSnapshot;
                if (!launch.active || launch.sequence == previous) throw new InvalidOperationException("Native launcher did not produce an approved curve " + loadTestLauncherKind);
                Debug.Log("[LoadTest] launcher started kind=" + launch.arc.kind + " player=" + mover.PlayerId + " sequence=" + launch.sequence + " mounted=" + mover.IsMounted);
                if (loadTestLauncherKind == "rope") peer.Send(new RopeSmokeStep { phase = 2 });
                bool cancel = index == 1 && mechanism != null;
                if (cancel)
                {
                    yield return new WaitForSecondsRealtime(.2f);
                    if (loadTestLauncherKind == "rope" && !input.ApprovedLaunchSnapshot.active)
                        throw new InvalidOperationException("Rope cancellation fixture ended before disable");
                    mechanism.enabled = false;
                }
                deadline = Time.unscaledTime + 10;
                while (Time.unscaledTime < deadline && input.ApprovedLaunchSnapshot.active) yield return null;
                if (input.ApprovedLaunchSnapshot.active) throw new InvalidOperationException("Native launcher failed to end its approved curve");
                if (loadTestLauncherKind == "rope" && !cancel && input.ApprovedLaunchSnapshot.elapsed < launch.arc.duration - .02f)
                    throw new InvalidOperationException("Rope fixture did not exercise the complete flight");
                if (mechanism != null) mechanism.enabled = false; // Stop an idle spring's deliberate repeat.
                yield return new WaitForSecondsRealtime(.5f);
                if (mover.InputLocked || mover.ExternalMovementOverride || mover.VisualOverrideActive)
                    throw new InvalidOperationException("Native launcher left player control locked");
                Debug.Log("[LoadTest] launcher finished kind=" + launch.arc.kind + " player=" + mover.PlayerId + " cancelled=" + cancel);
                if (loadTestLauncherKind == "rope" && index == 0)
                {
                    // The adjacent pillar truncates this second flight before a
                    // normal SyncVar interval. Begin/stop must still reach both views.
                    yield return WaitForNeutralRopeInput(peer, input, mover);
                    mechanism.enabled = true;
                    mover.SnapToWorldPoint(new Vector2(-6, 4), roundToGrid: false);
                    ServerReconcileFixturePlayer(input);
                    previous = input.ApprovedLaunchSnapshot.sequence;
                    peer.Send(new RopeSmokeStep { phase = 1 });
                    deadline = Time.unscaledTime + 6;
                    while (Time.unscaledTime < deadline && input.ApprovedLaunchSnapshot.sequence == previous) yield return null;
                    while (Time.unscaledTime < deadline && input.ApprovedLaunchSnapshot.active) yield return null;
                    var blocked = input.ApprovedLaunchSnapshot;
                    if (blocked.sequence == previous || blocked.active || blocked.elapsed <= 0 || blocked.elapsed >= blocked.arc.duration)
                        throw new InvalidOperationException("Rope fixture did not exercise an early blocker");
                    mechanism.enabled = false;
                    peer.Send(new RopeSmokeStep());
                    yield return new WaitForSecondsRealtime(.5f);
                    if (mover.InputLocked || mover.ExternalMovementOverride) throw new InvalidOperationException("Blocked rope did not restore control");
                    Debug.Log("[LoadTest] rope blocked prefix player=" + mover.PlayerId + " phase=" + blocked.elapsed);
                }
                if (loadTestLauncherKind == "rope") yield return WaitForNeutralRopeInput(peer, input, mover);
                mover.SnapToWorldPoint(original, roundToGrid: false); ServerReconcileFixturePlayer(input);
                health.SetExternalInvulnerability(priorProtection);
                index++;
            }
            if (index != 2) throw new InvalidOperationException("Launcher fixture requires exactly two actors");
            Debug.Log("[LoadTest] launchers completed kind=" + loadTestLauncherKind + " players=2");
        }
        static void ServerReconcileFixturePlayer(NetworkPlayerInput input)
        {
            if (NetworkServer.active && input != null && input.isServer)
            {
                NetworkPlayerInput.SyncAuthorityMotionTransform(input.GetComponent<MovementController>());
                input.ServerReconcileMovementNow();
            }
        }

        System.Collections.IEnumerator CheckLoadTestRevengeInput()
        {
            var owners = new HashSet<int>();
            foreach (var peer in NetworkServer.connections.Values)
            {
                var player = peer.identity;
                if (player == null || !player.TryGetComponent<CharacterHealth>(out var health) ||
                    !player.TryGetComponent<NetworkPlayerSetup>(out var setup))
                    throw new InvalidOperationException("Revenge fixture requires connected player identities");
                health.TakeDamage(health.life);
                if (!health.IsDead) throw new InvalidOperationException("Revenge fixture could not perform native player death");
                owners.Add(setup.PlayerId);
                Debug.Log("[LoadTest] revenge-input native death player=" + setup.PlayerId);
            }
            var boosted = new HashSet<int>();
            var completed = new HashSet<int>();
            var positions = new Dictionary<int, Vector3>();
            float deadline = Time.unscaledTime + 55;
            while (Time.unscaledTime < deadline && completed.Count < owners.Count)
            {
                RefreshRevengeCarts();
                foreach (var cart in revengeCarts)
                {
                    if (!owners.Contains(cart.OwnerPlayerId)) continue;
                    var buffer = GetRevengeServerInput(cart).buffer;
                    if (positions.TryGetValue(cart.OwnerPlayerId, out var previous) && (buffer.Held & 64) != 0 &&
                        Vector3.Distance(previous, cart.transform.position) > .0001f && boosted.Add(cart.OwnerPlayerId))
                        Debug.Log("[LoadTest] revenge-input boost accepted player=" + cart.OwnerPlayerId);
                    positions[cart.OwnerPlayerId] = cart.transform.position;
                    if (cart.NetworkLaunchSerial >= 3 && boosted.Contains(cart.OwnerPlayerId) && completed.Add(cart.OwnerPlayerId))
                        Debug.Log("[LoadTest] revenge-input confirmed player=" + cart.OwnerPlayerId + " shots=" + cart.NetworkLaunchSerial);
                }
                yield return null;
            }
            if (owners.Count != loadTestPlayers || completed.Count != owners.Count)
                throw new InvalidOperationException("Revenge input fixture did not confirm boost and three native shots for every client");
            Debug.Log("[LoadTest] revenge-input completed players=" + completed.Count);
        }

        int localRevengeSmokePhase, localRevengeSmokeShots;
        float localRevengeSmokeDeadline;
        uint localRevengeSmokeLaunch;

        // Called only by the explicit Development client flag. Inputs use the
        // same hardware slot and production transport as keyboard/controller.
        internal void TickLocalRevengeInputSmoke()
        {
            if (NetworkServer.active || !InRevengeArena || localRevengeSmokePhase == 5) return;
            var input = PlayerInputManager.Instance;
            if (input == null) return;
            foreach (var view in revengeViews.Values)
            {
                if (view.current.owner != LocalRoomPlayerId || view.cart == null) continue;
                if (localRevengeSmokePhase == 0 && CanPredictRevenge(view))
                {
                    input.SetSyntheticHeld(1, PlayerAction.MoveUp, true);
                    input.SetSyntheticHeld(1, PlayerAction.ActionC, true);
                    localRevengeSmokeDeadline = Time.unscaledTime + 3;
                    localRevengeSmokePhase = 1;
                    Debug.Log("[LoadTest] revenge-input boost sent player=" + LocalRoomPlayerId);
                }
                else if (localRevengeSmokePhase == 1 && Time.unscaledTime >= localRevengeSmokeDeadline)
                {
                    input.SetSyntheticHeld(1, PlayerAction.MoveUp, false);
                    input.SetSyntheticHeld(1, PlayerAction.ActionC, false);
                    localRevengeSmokePhase = 2;
                }
                else if (localRevengeSmokePhase == 2 && CanPredictRevenge(view) && view.current.chargeReady &&
                    Vector3.Distance(view.cart.transform.position, view.current.position) <= .125f)
                {
                    int distance = view.current.minCharge;
                    for (int i = 0; i < view.current.chargeTargets.Length; i++)
                        if (view.current.chargeTargets[i].z > 0) distance = i + 3;
                    if (distance <= view.current.minCharge) continue;
                    localRevengeSmokeLaunch = view.current.launchSerial;
                    localRevengeSmokeDeadline = Time.unscaledTime + (distance - view.current.minCharge + 1) * view.current.chargeStep;
                    input.SetSyntheticHeld(1, PlayerAction.ActionA, true);
                    localRevengeSmokePhase = 3;
                }
                else if (localRevengeSmokePhase == 3 && Time.unscaledTime >= localRevengeSmokeDeadline)
                {
                    if (!view.charge.Active || view.charge.Distance <= view.current.minCharge ||
                        view.cart.CaptureApprovedLandingPosition() == null)
                        throw new InvalidOperationException("Revenge input did not present the charged landing indicator");
                    Debug.Log("[LoadTest] revenge-input charged release player=" + LocalRoomPlayerId + " distance=" + view.charge.Distance);
                    input.SetSyntheticHeld(1, PlayerAction.ActionA, false);
                    localRevengeSmokePhase = 4;
                }
                else if (localRevengeSmokePhase == 4 && NetworkInputHistory.IsNewer(view.current.launchSerial, localRevengeSmokeLaunch))
                {
                    localRevengeSmokeShots++;
                    Debug.Log("[LoadTest] revenge-input shot acknowledged player=" + LocalRoomPlayerId + " shots=" + localRevengeSmokeShots);
                    localRevengeSmokePhase = localRevengeSmokeShots >= 3 ? 5 : 2;
                }
                break;
            }
        }

        static string LoadTestMechanismScene()
        {
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-load-test-mechanisms");
            return (at>=0&&at+1<args.Length?args[at+1]:"") switch
            { "falling"=>"BattleMode_2", "powder"=>"BattleMode_3", "roof"=>"BattleMode_8", "sudden"=>"BattleMode_15", _=>"BattleMode_14" };
        }

        System.Collections.IEnumerator CheckLoadTestMechanism()
        {
            var args=Environment.GetCommandLineArgs();int arg=Array.IndexOf(args,"-load-test-mechanisms");
            string kind=arg+1<args.Length?args[arg+1]:"";
            BombController source=null;
            foreach(var peer in NetworkServer.connections.Values)
                if(peer.identity!=null&&peer.identity.TryGetComponent(out source))break;
            if(source==null)throw new InvalidOperationException("Mechanism smoke needs a source");
            if(kind=="rocket" || kind=="world-blackout")
            {
                var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                // Traverse the authored transitions into the room that owns the mechanism.
                int roomSteps=kind=="rocket"?2:1;
                for(int roomStep=0;roomStep<roomSteps;roomStep++)
                {
                global::StageAssets.World3RoomTwoTransition transition=null;
                foreach(var candidate in FindObjectsByType<global::StageAssets.World3RoomTwoTransition>())
                    if((Camera)typeof(global::StageAssets.World3RoomTwoTransition).GetField("sourceRoomCamera",flags).GetValue(candidate)==Camera.main){transition=candidate;break;}
                if(transition==null)throw new InvalidOperationException("Rocket fixture needs native room transition");
                typeof(global::StageAssets.World3RoomTwoTransition).GetField("transitionStarted",flags).SetValue(transition,true);
                typeof(global::StageAssets.World3RoomTwoTransition).GetField("transitionSourcePosition",flags).SetValue(transition,(Vector2)Camera.main.transform.position);
                yield return (System.Collections.IEnumerator)typeof(global::StageAssets.World3RoomTwoTransition).GetMethod("TransitionRoutine",flags).Invoke(transition,null);
                foreach(var peer in NetworkServer.connections.Values)
                    if(peer.identity!=null&&peer.identity.TryGetComponent<NetworkPlayerInput>(out var input))ServerReconcileFixturePlayer(input);
                yield return new WaitForSecondsRealtime(1);
                }
            }
            int[] renderers=Array.Empty<int>();
            NetworkServer.SendToReady(new MechanismSmokeStep {kind=kind,phase=1,renderers=renderers});
            yield return new WaitForSecondsRealtime(.2f);
            if(kind=="sudden")
            {
                int player=0;
                foreach(var peer in NetworkServer.connections.Values)
                    if(peer.identity!=null&&peer.identity.TryGetComponent<NetworkPlayerInput>(out var input))
                    {
                        var mover=input.GetComponent<MovementController>();
                        Vector2 position=new(player++==0?-1:1,-1);
                        source.ClearDestructibleForEffect(position,false,false);
                        mover.Rigidbody.position=position;mover.transform.position=position;
                        ServerReconcileFixturePlayer(input);
                    }
                GameManager.Instance.ApplyNetworkTimer(48,true);
                yield return new WaitForSecondsRealtime(2);
                var pause=GamePauseController.Instance;
                var toggle=typeof(GamePauseController).GetMethod("TogglePause",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                if(pause==null)throw new InvalidOperationException("Missing native pause");
                toggle.Invoke(pause,null);
                if(!GamePauseController.IsPaused)throw new InvalidOperationException("Native pause rejected");
                yield return new WaitForSecondsRealtime(1);
                toggle.Invoke(pause,null);
                if(GamePauseController.IsPaused)throw new InvalidOperationException("Native resume rejected");
                yield return new WaitForSecondsRealtime(4);
            }
            else if(kind=="powder")
            {
                var powder=FindAnyObjectByType<BattleMode3PowderTrailController>();var positions=new List<Vector2>();
                if(powder==null)throw new InvalidOperationException("Missing native powder");
                powder.CopyTrailWorldPositions(positions);
                if(positions.Count==0)throw new InvalidOperationException("No powder trail cells");
                var map=source.groundTiles;var cell=map.WorldToCell(positions[0]);
                powder.OnExplosionHit(source,positions[0],cell,map.GetTile(cell));
                yield return new WaitForSecondsRealtime(2);
                if(powder.IgnitionRunning)throw new InvalidOperationException("Powder did not end");
            }
            else if(kind=="falling"||kind=="roof") yield return new WaitForSecondsRealtime(8);
            else if(kind=="boiler" || kind=="blackout")
            {
                var ground=source.groundTiles;var resolver=FindAnyObjectByType<GroundTileResolver>();
                if(ground==null||resolver==null)throw new InvalidOperationException("Mechanism ground missing");
                var cells=new List<Vector3Int>();
                foreach(var cell in ground.cellBounds.allPositionsWithin)
                    if(resolver.TryGetHandler(ground.GetTile(cell),out var handler) &&
                        (kind=="boiler"?handler is BoilerTriggerGroundTileHandler:handler is SwapGroundTileBlackoutHandler))cells.Add(cell);
                if(cells.Count==0)throw new InvalidOperationException("No native mechanism cells: "+kind);
                if(kind=="boiler")
                {
                    var cell=cells[0];Vector2 position=ground.GetCellCenterWorld(cell);
                    var instance=Instantiate(source.bombPrefab,position,Quaternion.identity);var bomb=instance.GetComponent<Bomb>();bomb.Initialize(source);bomb.SetFuseSeconds(20);NetSpawn.Server(instance);
                    resolver.TryGetHandler(ground.GetTile(cell),out var handler);
                    var expected=GameplayLatencyDiagnostics.For("mechanism_bomb_expected",bomb:bomb,detail:"fixture=boiler");
                    expected.position=position+Vector2.up*(int)typeof(BoilerTriggerGroundTileHandler).GetField("pullTilesUp",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(handler);
                    expected.bombPosition=position;GameplayLatencyDiagnostics.Write(expected);
                    ((BoilerTriggerGroundTileHandler)handler).OnBombAt(source,position,cell,ground.GetTile(cell),instance);
                    yield return new WaitForSecondsRealtime(3);
                    if(bomb!=null&&!bomb.HasExploded)throw new InvalidOperationException("Boiler did not finish its native capture/explosion");
                }
                else
                {
                    if(StageBlackout.Instance==null)throw new InvalidOperationException("No native blackout");
                    var cell=cells[0];
                    for(int i=0;i<2;i++)
                    {
                        resolver.TryGetHandler(ground.GetTile(cell),out var handler);
                        ((SwapGroundTileBlackoutHandler)handler).OnExplosionHit(source,ground.GetCellCenterWorld(cell),cell,ground.GetTile(cell));
                        yield return new WaitForSecondsRealtime(1.2f);
                    }
                    if(StageBlackout.Instance.CaptureNetworkBlackout().active)
                    {
                        resolver.TryGetHandler(ground.GetTile(cell),out var handler);
                        ((SwapGroundTileBlackoutHandler)handler).OnExplosionHit(source,ground.GetCellCenterWorld(cell),cell,ground.GetTile(cell));
                    }
                    yield return new WaitForSecondsRealtime(.7f);
                    if(StageBlackout.Instance.CaptureNetworkBlackout().active)throw new InvalidOperationException("Native swap did not release blackout");
                }
            }
            else if(kind=="world-blackout")
            {
                StageBlackout blackout=null;
                foreach(var candidate in FindObjectsByType<StageBlackout>(FindObjectsInactive.Include))
                {
                    var view=candidate.CaptureNetworkBlackout();var overlay=candidate.GetComponent<WorldBlackoutRenderer>();
                    if(view.world&&view.roomActive&&overlay!=null&&overlay.RoomWorldBounds.Contains(source.transform.position)){blackout=candidate;break;}
                }
                if(blackout==null)throw new InvalidOperationException("No active native world blackout");
                NetworkServer.SendToReady(new MechanismSmokeStep {kind=kind,phase=1,renderers=renderers,blackout=blackout.NetworkBlackoutPath});
                blackout.SetBlackoutActive(true);yield return new WaitForSecondsRealtime(1.5f);
                blackout.SetBlackoutActive(false);yield return new WaitForSecondsRealtime(1.5f);
                blackout.SetBlackoutActive(true);yield return new WaitForSecondsRealtime(1.5f);
            }
            else if(kind=="rocket" || kind=="dynamite")
            {
                var map=source.destructibleTiles;var resolver=FindAnyObjectByType<DestructibleTileResolver>();
                if(map==null||resolver==null)throw new InvalidOperationException("Mechanism destructibles missing");
                bool triggered=false;
                foreach(var cell in map.cellBounds.allPositionsWithin)
                {
                    if(!resolver.TryGetHandler(map.GetTile(cell),out var handler)||
                        !(kind=="rocket"?handler is RocketTileHandler:handler is DynamiteTileHandler))continue;
                    Vector3 position=map.GetCellCenterWorld(cell);
                    if(kind=="rocket")
                    {
                        var room=global::StageAssets.World3RoomProgressionController.FindRoomBoundsContaining(position);
                        if(room==null||!global::StageAssets.World3RoomProgressionController.IsRoomOccupied(room))continue;
                        Vector3 viewport=Camera.main.WorldToViewportPoint(position);
                        if(viewport.z<=0||viewport.x<.1f||viewport.x>.9f||viewport.y<.1f||viewport.y>.9f)continue;
                    }
                    triggered=handler.HandleHit(source,position,cell);if(triggered)break;
                }
                if(!triggered)throw new InvalidOperationException("No native occupied mechanism: "+kind);
                if(kind=="rocket")
                {
                    var flight=FindAnyObjectByType<global::StageAssets.DestructibleRocketController>();
                    if(flight==null)throw new InvalidOperationException("Native rocket did not launch");
                    var visuals=flight.GetComponentsInChildren<SpriteRenderer>(true);renderers=new int[visuals.Length];
                    for(int i=0;i<visuals.Length;i++)renderers[i]=visuals[i].GetEntityId().GetHashCode();
                    NetworkServer.SendToReady(new MechanismSmokeStep {kind=kind,phase=1,renderers=renderers});
                    yield return new WaitForSecondsRealtime(7);
                    if(flight!=null)throw new InvalidOperationException("Native rocket did not complete");
                }
                else yield return new WaitForSecondsRealtime(2);
            }
            else throw new InvalidOperationException("Unknown mechanism fixture: "+kind);
            NetworkServer.SendToReady(new MechanismSmokeStep {kind=kind,phase=2,renderers=renderers});
            Debug.Log("[LoadTest] mechanism completed kind="+kind);
        }

        internal void TraceMechanismViews(string kind,int phase,int[] renderers,string blackoutPath)
        {
            if(renderers!=null)foreach(int id in renderers)
                if(coopViews.TryGetValue(id,out var view)&&view!=null)
                {
                    var sample=GameplayLatencyDiagnostics.For("mechanism_view",detail:"fixture="+kind+";phase="+phase+";visible="+view.enabled+";sprite="+(view.sprite!=null?view.sprite.name:"")+";shader="+(view.sharedMaterial!=null?view.sharedMaterial.shader.name:""));
                    sample.entity=id;sample.position=view.transform.position;GameplayLatencyDiagnostics.Write(sample);
                }
            const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            if(kind=="sudden")
                GameplayLatencyDiagnostics.Write(GameplayLatencyDiagnostics.For("mechanism_sudden_pause",detail:"paused="+GamePauseController.IsPaused));
            if(kind=="falling")
            {
                var target=FindAnyObjectByType<BattleMode2FallingBombController>();
                if(target!=null){bool active=target.TryGetActiveTargetWarning(out _,out _,out _);
                    GameplayLatencyDiagnostics.Write(GameplayLatencyDiagnostics.For("mechanism_target",detail:"active="+active+";bombs="+FindObjectsByType<Bomb>().Length));}
            }
            if(kind=="powder")
            {
                var powder=FindAnyObjectByType<BattleMode3PowderTrailController>();
                if(powder!=null)GameplayLatencyDiagnostics.Write(GameplayLatencyDiagnostics.For("mechanism_powder",detail:"frame="+typeof(BattleMode3PowderTrailController).GetField("presentedNetworkPowderFrame",flags).GetValue(powder)));
            }
            if(kind=="roof")
            {
                var roof=FindAnyObjectByType<BattleMode8RoofController>();
                if(roof!=null)
                {
                    var history=(List<RoofTileState>)typeof(BattleMode8RoofController).GetField("networkRoofHistory",flags).GetValue(roof);
                    if(history.Count>0){var state=history[0];int tile=state.tiles.Length>0?state.tiles[0]:-9;
                        GameplayLatencyDiagnostics.Write(GameplayLatencyDiagnostics.For("mechanism_roof",detail:"tile="+tile));}
                }
            }
            StageBlackout blackout=StageBlackout.Instance;
            if(!string.IsNullOrEmpty(blackoutPath))
                foreach(var candidate in coopBlackoutSources)if(candidate!=null&&candidate.NetworkBlackoutPath==blackoutPath){blackout=candidate;break;}
            if((kind=="blackout"||kind=="world-blackout")&&blackout!=null)
            {
                var state=blackout.CaptureNetworkBlackout();
                GameplayLatencyDiagnostics.Write(GameplayLatencyDiagnostics.For("mechanism_blackout",detail:"active="+state.active+";alpha="+state.alpha.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            }
            if(kind=="world-blackout"||kind=="rocket")
            {
                int unsupported=0;
                foreach(var material in coopViewMaskMaterials.Values)if(material.instance!=null&&!material.instance.shader.isSupported)unsupported++;
                GameplayLatencyDiagnostics.Write(GameplayLatencyDiagnostics.For("mechanism_materials",detail:"masked="+coopViewMaskMaterials.Count+";unsupported="+unsupported));
            }
        }

        System.Collections.IEnumerator CheckLoadTestFieldBombMotion(BombController controller)
        {
            // Native motion in an isolated open corridor, not normal arena rules.
            foreach (var kind in new[] { NetworkBombFx.BombMotionKind.Magnet, NetworkBombFx.BombMotionKind.Search, NetworkBombFx.BombMotionKind.Kick })
            {
                Vector2 origin = new(30, 30);
                Vector2 corner = origin + Vector2.right * 2;
                Vector2 target = kind == NetworkBombFx.BombMotionKind.Kick ? corner + Vector2.up * 2 : corner;
                var wall = new GameObject("FieldSmokeStop", typeof(BoxCollider2D));
                wall.layer = LayerMask.NameToLayer("Stage");
                wall.transform.position = target + (kind == NetworkBombFx.BombMotionKind.Kick ? Vector2.up : Vector2.right);
                var instance = Instantiate(controller.bombPrefab, origin, Quaternion.identity);
                var bomb = instance.GetComponent<Bomb>(); bomb.Initialize(null); bomb.SetFuseSeconds(20); bomb.kickSpeed = 2;
                NetSpawn.Server(instance); Physics2D.SyncTransforms();
                try
                {
                    bool started = kind == NetworkBombFx.BombMotionKind.Kick
                        ? bomb.StartKick(Vector2.right, 1, LayerMask.GetMask("Stage"), null)
                        : bomb.StartMagnetPull(Vector2.right, 1, 2, LayerMask.GetMask("Stage"), null, .2f,
                            LayerMask.GetMask("Stage"), .6f, .9f, false, kind == NetworkBombFx.BombMotionKind.Search);
                    if (!started) throw new InvalidOperationException("Field smoke could not start native " + kind);
                    bool turned = false;
                    float deadline = Time.unscaledTime + 6;
                    while ((bomb.IsBeingKicked || bomb.IsBeingMagnetPulled) && Time.unscaledTime < deadline)
                    {
                        if (kind == NetworkBombFx.BombMotionKind.Kick && !turned && bomb.GetComponent<Rigidbody2D>().position.x > corner.x - .4f)
                        {
                            turned = bomb.TryRedirectKick(Vector2.up);
                            if (!turned) throw new InvalidOperationException("Native redirection rejected");
                        }
                        yield return null;
                    }
                    if (bomb.HasExploded || bomb.IsBeingKicked || bomb.IsBeingMagnetPulled || Vector2.Distance(bomb.transform.position, target) > .05f)
                        throw new InvalidOperationException("Field smoke native endpoint failed " + kind + " at " + bomb.transform.position);
                    var expected = GameplayLatencyDiagnostics.For("field_motion_expected", bomb: bomb, detail: "kind=" + kind);
                    expected.bombPosition = origin; expected.delta = corner - origin; expected.position = target;
                    GameplayLatencyDiagnostics.Write(expected);
                    Debug.Log("[LoadTest] field-motion completed kind=" + kind);
                    yield return new WaitForSecondsRealtime(1);
                }
                finally { NetworkServer.Destroy(instance); Destroy(wall); }
                yield return null;
            }
        }

        System.Collections.IEnumerator CheckLoadTestPresenterRemoval()
        {
            // Both local test clients stopped their senders; let already-sent
            // input drain before deliberately removing the network actor.
            yield return new WaitForSecondsRealtime(1);
            GameObject source = null; BombController controller = null; NetworkBombFx presenter = null;
            foreach (var peer in NetworkServer.connections.Values)
                if (peer.identity != null && peer.identity.TryGetComponent(out controller) && peer.identity.TryGetComponent(out presenter))
                { source = peer.identity.gameObject; break; }
            if (source == null || controller.bombPrefab == null) throw new InvalidOperationException("Presenter fixture requires an active player and bomb prefab");
            if (HasArg("-load-test-field-motion")) yield return CheckLoadTestFieldBombMotion(controller);
            var instance = Instantiate(controller.bombPrefab, source.transform.position, Quaternion.identity);
            var bomb = instance.GetComponent<Bomb>(); bomb.Initialize(null); bomb.SetFuseSeconds(10); bomb.punchDuration = 1.2f;
            NetSpawn.Server(instance);
            uint bombId = instance.GetComponent<NetworkIdentity>().netId;
            Vector2 direction = source.transform.position.x <= 0 ? Vector2.right : Vector2.left;
            if (bombId == 0 || !bomb.StartPunch(direction, 1, 1, 0, null, presenter: presenter))
                throw new InvalidOperationException("Presenter fixture failed to launch its native arc");
            Debug.Log("[LoadTest] presenter-removal started bomb=" + bombId);
            yield return new WaitForSecondsRealtime(.25f);
            if (bomb == null || !bomb.IsBeingPunched) throw new InvalidOperationException("Presenter fixture lost its arc before removal");
            NetworkServer.Destroy(source);
            Debug.Log("[LoadTest] presenter-removal removed source bomb=" + bombId);
            float deadline = Time.unscaledTime + 6;
            while (bomb != null && bomb.IsBeingPunched && Time.unscaledTime < deadline) yield return null;
            if (bomb == null || bomb.IsBeingPunched) throw new InvalidOperationException("Presenter fixture did not finish its native arc");
            Debug.Log("[LoadTest] presenter-removal completed bomb=" + bombId);
            NetworkServer.Destroy(instance);
        }

        System.Collections.IEnumerator CheckLoadTestSpawns()
        {
            var manager = FindAnyObjectByType<GameManager>();
            var spawner = FindAnyObjectByType<PlayersSpawner>();
            var prefabs = new[] { Resources.Load<GameObject>("Items/LineBomb"),
                spawner != null ? spawner.GetPlayerMountPrefabForType(MountedType.Mole) : null,
                spawner != null ? spawner.GetPlayerMountPrefabForType(MountedType.Tank) : null };
            var spawned = new List<GameObject>();
            for (int i = 0; i < prefabs.Length; i++)
            {
                if (manager == null || prefabs[i] == null) throw new InvalidOperationException("Spawn regression fixture unavailable");
                Vector3 position = new Vector3(30 + i * 2, 30, 0);
                var instance = Instantiate(prefabs[i], position, Quaternion.identity);
                manager.PrepareSpawnedHiddenObject(instance, prefabs[i], position);
                NetSpawn.Server(instance);
                spawned.Add(instance);
                Debug.Log("[LoadTest] spawn regression revealed=" + prefabs[i].name);
            }
            if (HasArg("-load-test-item-motion"))
            {
                AutoItemDatabase.BuildIfNeeded();
                var player = FindAnyObjectByType<PlayerIdentity>();
                if (player == null) throw new InvalidOperationException("Item motion fixture requires a player");
                int offset = 1;
                foreach (var itemType in new[] { ItemType.Skull, ItemType.LineBomb })
                {
                    var prefab = AutoItemDatabase.Get(itemType);
                    if (prefab == null) throw new InvalidOperationException("Item motion fixture missing " + itemType);
                    Vector3 position = player.transform.position + Vector3.right * offset++;
                    var item = Instantiate(prefab, position, Quaternion.identity); NetSpawn.Server(item.gameObject);
                    if (!item.TryExpelItem(Vector2.right, 1, null, 3)) throw new InvalidOperationException("Item expel rejected " + itemType);
                    spawned.Add(item.gameObject); Debug.Log("[LoadTest] item-motion launched type=" + itemType);
                }
            }
            if (HasArg("-load-test-full-input"))
            {
                yield return new WaitForSecondsRealtime(7);
                var mountedPlayers = new List<PlayerMountCompanion>();
                var equippedMounts = new List<GameObject>();
                foreach (var connection in NetworkServer.connections.Values)
                {
                    if (connection.identity == null) continue;
                    var companion = connection.identity.GetComponent<PlayerMountCompanion>();
                    if (companion == null) throw new InvalidOperationException("Mount input fixture missing player companion");
                    var type = mountedPlayers.Count == 0 ? MountedType.Mole : MountedType.Tank;
                    companion.RestoreMounted(type);
                    companion.ApplyRidingTransitionInvulnerability(15f, false, companion.GetMountedLouieObject());
                    if (HasArg("-load-test-mount-cycles"))
                    {
                        var queue = companion.GetComponent<MountEggQueue>();
                        if (queue == null || !queue.TryEnqueue(ItemType.BlueLouieEgg, null, null, 1))
                            throw new InvalidOperationException("Mount cycle fixture could not enqueue its egg");
                    }
                    mountedPlayers.Add(companion);
                    equippedMounts.Add(companion.GetMountedLouieObject());
                    Debug.Log("[LoadTest] full-input equipped=" + type);
                }
                float deadline = Time.unscaledTime + 8;
                while (Time.unscaledTime < deadline)
                {
                    bool allDismounted = true;
                    foreach (var companion in mountedPlayers)
                        allDismounted &= companion.GetMountedLouieType() == MountedType.None;
                    // Keep COM from recollecting the fixture before it is audited.
                    foreach (var mount in equippedMounts)
                        if (mount != null && mount.TryGetComponent<MountWorldPickup>(out _) &&
                            mount.TryGetComponent<Collider2D>(out var collider)) collider.enabled = false;
                    if (allDismounted) break;
                    yield return null;
                }
                yield return null; // newly added pickup components register in Start.
                foreach (var companion in mountedPlayers)
                    if (companion.GetMountedLouieType() != MountedType.None)
                    {
                        var movement = companion.GetComponent<MovementController>();
                        var manual = companion.GetComponent<PlayerManualDismount>();
                        var rider = companion.GetComponent<PlayerRidingController>();
                        throw new InvalidOperationException($"Network Select did not dismount player={movement.PlayerId} " +
                            $"manualEnabled={manual != null && manual.enabled} locked={movement.InputLocked} " +
                            $"mounted={movement.IsMounted} riding={rider != null && rider.IsPlaying}");
                    }
                foreach (var mount in equippedMounts)
                {
                    var pickup = mount != null ? mount.GetComponent<MountWorldPickup>() : null;
                    if (pickup == null || pickup.NetworkPresentation == null || pickup.NetworkPresentation.netId == 0)
                        throw new InvalidOperationException("Dismounted world pickup was not replicated");
                    spawned.Add(mount);
                    Debug.Log("[LoadTest] full-input world pickup replicated type=" + pickup.Type);
                }
                Debug.Log("[LoadTest] full-input dismount confirmed players=" + mountedPlayers.Count);
                if (HasArg("-load-test-mount-cycles"))
                {
                    for (int cycle = 1; cycle <= 3; cycle++)
                    {
                        yield return new WaitForSecondsRealtime(1);
                        for (int i = 0; i < mountedPlayers.Count; i++)
                        {
                            var worldMount = equippedMounts[i];
                            var queue = worldMount.GetComponent<MountEggQueue>();
                            if (queue == null || queue.Count != 1 || mountedPlayers[i].GetComponent<MountEggQueue>().Count != 0)
                                throw new InvalidOperationException("Mount cycle lost detached egg ownership");
                            // Re-enable real trigger contact only after the dismount completes.
                            worldMount.GetComponent<Collider2D>().enabled = true;
                        }
                        deadline = Time.unscaledTime + 8;
                        bool allMounted = false;
                        while (Time.unscaledTime < deadline)
                        {
                            allMounted = true;
                            foreach (var companion in mountedPlayers)
                                allMounted &= companion.HasMountedLouie() && !companion.GetComponent<PlayerRidingController>().IsPlaying;
                            if (allMounted) break;
                            yield return null;
                        }
                        if (!allMounted) throw new InvalidOperationException("Mount cycle failed native pickup remount");
                        foreach (var companion in mountedPlayers)
                            if (companion.GetComponent<MountEggQueue>().Count != 1)
                                throw new InvalidOperationException("Mount cycle lost remounted egg ownership");
                        Debug.Log("[LoadTest] mount-cycle remount confirmed cycle=" + cycle + " players=" + mountedPlayers.Count + " eggs=1");
                        deadline = Time.unscaledTime + 8;
                        bool allDetached = false;
                        while (Time.unscaledTime < deadline)
                        {
                            allDetached = true;
                            foreach (var companion in mountedPlayers)
                                allDetached &= companion.GetMountedLouieType() == MountedType.None;
                            foreach (var worldMount in equippedMounts)
                                if (worldMount != null && worldMount.TryGetComponent<MountWorldPickup>(out _))
                                    worldMount.GetComponent<Collider2D>().enabled = false;
                            if (allDetached) break;
                            yield return null;
                        }
                        if (!allDetached) throw new InvalidOperationException("Mount cycle failed repeated Select dismount");
                        Debug.Log("[LoadTest] mount-cycle dismount confirmed cycle=" + cycle + " players=" + mountedPlayers.Count);
                    }
                    Debug.Log("[LoadTest] mount-cycles completed cycles=3 players=" + mountedPlayers.Count);
                }
            }
            yield return new WaitForSecondsRealtime(10);
            foreach (var instance in spawned) NetSpawn.Despawn(instance);
            yield return null;
            Debug.Log("[LoadTest] spawn regression removed LineBomb/Mole/Tank");
        }
    }

    // Test-only synthetic hardware input on an otherwise complete Mirror client.
    // No custom server command: Select travels through NetworkPlayerInput.
    [DefaultExecutionOrder(-150)]
    public sealed class LocalNetworkInputSmoke : MonoBehaviour
    {
        MountedType observedMount;
        float mountedAt, releaseTime, mountTravelAt;
        bool sent, completed, repeatMountCycles;
        readonly HashSet<ItemType> observedMovingItems = new();
        float nextItemCheck;
        string captureDirectory;
        bool capturedRiding, captureWarmed;
        float portalCaptureUntil, portalCaptureNext, portalPoseNext;
        int portalCaptureFrame;
        int captureArc, captureFrame;
        float captureNext, visualCaptureAt = float.PositiveInfinity, visualAfkCaptureAt = float.PositiveInfinity;
        int visualCaptureSerial;
        bool checkMountVisuals, mountVisualHandler, mountVisualReady;
        string mechanismKind,mechanismBlackoutPath;
        bool mechanismHandler;
        int mechanismPhase,mechanismFrame;
        int[] mechanismRenderers;
        float mechanismCaptureUntil,mechanismCaptureNext,mechanismPhaseAt;
        int mountVisualPhase, ropeSmokePhase, ropeAckPending = -1;
        uint ropeAckAfterTick;
        bool checkRopeInput, ropeSmokeHandler, checkPortalInput;
        bool checkLauncherInput, checkItemMotion, runRealInput, suspendPresenterInputs, revengeInput, quitAfterDisconnect, sawConnection;
        bool remoteWalk, coopWorld, bombAbilitySmoke, bombAbilityHandler;
        int bombAbilityHeld, bombAbilitySerial;
        uint bombAbilityAckAfter;
        float bombAbilityCaptureNext;
        // R02 reconnect: survive the first disconnect (the supported flow returns to the browser and joins the next room).
        bool awaitRejoin, leftRoom;
        int disconnects;
        float leaveAfter = -1, stageReadyAt = -1;
        float coopWorldCaptureNext;
        int coopWorldCaptureSerial;
        float walkStartedAt = -1, walkCaptureNext;
        int walkCaptureSerial;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var args = Environment.GetCommandLineArgs();
            if (!Debug.isDebugBuild || (Array.IndexOf(args, "-load-test-real-input") < 0 &&
                Array.IndexOf(args, "-load-test-item-motion") < 0 && Array.IndexOf(args, "-load-test-presenter-removal") < 0 &&
                Array.IndexOf(args, "-load-test-revenge-input") < 0 && Array.IndexOf(args, "-load-test-client") < 0)) return;
            Application.runInBackground = true;
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            var root = new GameObject("LocalNetworkInputSmoke");
            DontDestroyOnLoad(root);
            var smoke = root.AddComponent<LocalNetworkInputSmoke>();
            int mechanismArg=Array.IndexOf(args,"-load-test-mechanisms");
            smoke.mechanismKind=mechanismArg>=0&&mechanismArg+1<args.Length?args[mechanismArg+1]:null;
            smoke.checkMountVisuals = Array.IndexOf(args, "-load-test-mount-visuals") >= 0;
            smoke.checkLauncherInput = Array.IndexOf(args, "-load-test-launchers") >= 0;
            smoke.checkRopeInput = smoke.checkLauncherInput && (Array.IndexOf(args, "rope") >= 0 || Array.Exists(args, arg => (arg.StartsWith("cart", StringComparison.Ordinal) || arg.StartsWith("portal", StringComparison.Ordinal))));
            smoke.checkPortalInput = Array.Exists(args, arg => arg.StartsWith("portal", StringComparison.Ordinal));
            smoke.checkItemMotion = Array.IndexOf(args, "-load-test-item-motion") >= 0;
            smoke.runRealInput = Array.IndexOf(args, "-load-test-real-input") >= 0;
            smoke.repeatMountCycles = Array.IndexOf(args, "-load-test-mount-cycles") >= 0;
            smoke.suspendPresenterInputs = Array.IndexOf(args, "-load-test-presenter-removal") >= 0;
            smoke.revengeInput = Array.IndexOf(args, "-load-test-revenge-input") >= 0;
            smoke.remoteWalk = Array.IndexOf(args, "-load-test-remote-walk") >= 0;
            smoke.bombAbilitySmoke = Array.IndexOf(args, "-load-test-bomb-abilities") >= 0;
            smoke.coopWorld = Array.IndexOf(args, "-load-test-coop-world") >= 0;
            smoke.quitAfterDisconnect = Array.IndexOf(args, "-load-test-client") >= 0;
            smoke.awaitRejoin = Array.IndexOf(args, "-load-test-rejoin") >= 0;
            int leaveArg = Array.IndexOf(args, "-load-test-leave-after");
            if (leaveArg >= 0 && leaveArg + 1 < args.Length &&
                float.TryParse(args[leaveArg + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float leave))
                smoke.leaveAfter = leave;
            int captureArg = Array.IndexOf(args, "-load-test-mount-captures");
            if (captureArg >= 0 && captureArg + 1 < args.Length)
            {
                if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                    throw new InvalidOperationException("Mount capture requires an active graphics device");
                smoke.captureDirectory = System.IO.Path.GetFullPath(args[captureArg + 1]);
                System.IO.Directory.CreateDirectory(smoke.captureDirectory);
                root.AddComponent<LocalNetworkSmokeCapture>().source = smoke;
                Debug.Log("[LoadTest] mount-capture graphics=" + SystemInfo.graphicsDeviceType);
            }
        }

        // R02 reconnect: this client leaves through the supported room flow a fixed time after its co-op stage is live.
        void DriveRoomLeave()
        {
            if (leaveAfter < 0 || leftRoom || !NetworkClient.isConnected || !SceneManager.GetActiveScene().name.StartsWith("Stage_", StringComparison.Ordinal))
            { if (!NetworkClient.isConnected) stageReadyAt = -1; return; }
            if (stageReadyAt < 0) { stageReadyAt = Time.unscaledTime; return; }
            if (Time.unscaledTime - stageReadyAt < leaveAfter) return;
            leftRoom = true;
            Debug.Log("[LoadTest] reconnect leaving room scene=" + SceneManager.GetActiveScene().name);
            BombermanNetworkManager.LeaveRoomToBrowser();
        }

        void Update()
        {
            DriveRemoteWalk();
            if (bombAbilitySmoke && NetworkClient.active)
            {
                if (!bombAbilityHandler)
                {
                    bombAbilityHandler = true;
                    NetworkClient.RegisterHandler<BombAbilitySmokeStep>(step =>
                    {
                        bombAbilityHeld = step.held; bombAbilitySerial = step.serial;
                        bombAbilityAckAfter = NetworkClient.localPlayer != null ? NetworkClient.localPlayer.GetComponent<NetworkPlayerInput>().CurrentInputTick + 2 : 0;
                        Debug.Log("[LoadTest] a14 input " + step.label + " serial=" + step.serial);
                    });
                }
                if (NetworkClient.isConnected && NetworkClient.localPlayer != null && PlayerInputManager.Instance != null)
                {
                    var actions = new[] { PlayerAction.MoveUp, PlayerAction.MoveDown, PlayerAction.MoveLeft, PlayerAction.MoveRight,
                        PlayerAction.ActionA, PlayerAction.ActionB, PlayerAction.ActionC };
                    for (int i = 0; i < actions.Length; i++) PlayerInputManager.Instance.SetSyntheticHeld(1, actions[i], (bombAbilityHeld & (1 << i)) != 0);
                    var tick = NetworkClient.localPlayer.GetComponent<NetworkPlayerInput>().CurrentInputTick;
                    if (NetworkInputHistory.IsNewer(tick, bombAbilityAckAfter))
                    {
                        NetworkClient.Send(new BombAbilitySmokeAck { serial = bombAbilitySerial, tick = tick });
                        bombAbilityAckAfter = unchecked(tick + 20);
                    }
                }
            }
            if(mechanismKind!=null&&!mechanismHandler&&NetworkClient.active)
            {
                mechanismHandler=true;NetworkClient.RegisterHandler<MechanismSmokeStep>(message=>
                {
                    if(message.kind!=mechanismKind)return;
                    mechanismPhase=message.phase;mechanismRenderers=message.renderers;if(!string.IsNullOrEmpty(message.blackout))mechanismBlackoutPath=message.blackout;mechanismPhaseAt=Time.unscaledTime;
                    mechanismCaptureUntil=Time.unscaledTime+(message.phase==1?9f:1f);mechanismCaptureNext=0;
                    Debug.Log("[LoadTest] mechanism phase="+message.phase+" kind="+message.kind);
                });
            }
            DriveRoomLeave();
            if (quitAfterDisconnect)
            {
                if (NetworkClient.isConnected) sawConnection = true;
                else if (sawConnection && awaitRejoin && disconnects == 0)
                {
                    disconnects++; sawConnection = false;
                    Debug.Log("[LoadTest] client disconnected; awaiting rejoin left=" + leftRoom);
                }
                else if (sawConnection)
                {
                    // Let Application.quitting flush diagnostics before the smoke
                    // copies the file. A live StreamWriter can expose half a line.
                    Debug.Log("[LoadTest] client disconnected; closing capture");
                    enabled = false;
                    Application.Quit();
                    return;
                }
            }
            if (checkMountVisuals && NetworkClient.isConnected)
            {
                if (!mountVisualHandler)
                {
                    mountVisualHandler = true;
                    NetworkClient.RegisterHandler<MountVisualSmokeStep>(step =>
                    {
                        mountVisualPhase = step.phase; visualCaptureAt = Time.unscaledTime + .3f;
                        visualAfkCaptureAt = step.phase == 0 ? Time.unscaledTime + 5.3f : float.PositiveInfinity;
                        Debug.Log("[LoadTest] visual step=" + step.phase);
                    });
                }
                if (!mountVisualReady && (string.IsNullOrEmpty(captureDirectory) || captureWarmed) && SceneManager.GetActiveScene().name.StartsWith("BattleMode_", StringComparison.Ordinal))
                {
                    mountVisualReady = true; NetworkClient.Send(new PresenterSmokeReady { scene = SceneManager.GetActiveScene().name });
                }
                var visualInput = PlayerInputManager.Instance;
                if (visualInput != null)
                {
                    visualInput.SetSyntheticHeld(1, PlayerAction.ActionL, mountVisualPhase == 1);
                    visualInput.SetSyntheticHeld(1, PlayerAction.ActionC, mountVisualPhase == 4);
                    visualInput.SetSyntheticHeld(1, PlayerAction.MoveUp, mountVisualPhase == 1 || mountVisualPhase == 4 || mountVisualPhase == 6);
                    visualInput.SetSyntheticHeld(1, PlayerAction.MoveDown, mountVisualPhase == 7);
                    visualInput.SetSyntheticHeld(1, PlayerAction.MoveRight, mountVisualPhase == 2);
                    visualInput.SetSyntheticHeld(1, PlayerAction.MoveLeft, mountVisualPhase == 5);
                }
            }
            if (checkLauncherInput && NetworkClient.isConnected && NetworkClient.localPlayer != null && PlayerInputManager.Instance != null)
            {
                var launcher = NetworkClient.localPlayer.GetComponent<NetworkPlayerInput>();
                if (checkRopeInput)
                {
                    if (!ropeSmokeHandler)
                    {
                        ropeSmokeHandler = true;
                        NetworkClient.RegisterHandler<RopeSmokeStep>(step =>
                        {
                            ropeSmokePhase = ropeAckPending = step.phase;
                            ropeAckAfterTick = unchecked(launcher.CurrentInputTick + 1);
                        });
                    }
                    if (!mountVisualReady && (string.IsNullOrEmpty(captureDirectory) || captureWarmed) && (SceneManager.GetActiveScene().name == "BattleMode_11" || SceneManager.GetActiveScene().name == "BattleMode_9" || SceneManager.GetActiveScene().name == "BattleMode_12" || (SceneManager.GetActiveScene().name == "Stage_3-2" || SceneManager.GetActiveScene().name == "Stage_3-4" || SceneManager.GetActiveScene().name == "BattleMode_7")))
                    {
                        mountVisualReady = true; NetworkClient.Send(new PresenterSmokeReady { scene = SceneManager.GetActiveScene().name });
                    }
                    PlayerInputManager.Instance.SetSyntheticHeld(1, PlayerAction.MoveUp, ropeSmokePhase == 1);
                    // At least two FixedUpdate ticks after receipt: the first can
                    // precede Update's new hardware sample. Ack follows the actual Command.
                    if (ropeAckPending >= 0 && (ropeAckPending != 3 || !launcher.ApprovedCartActive) && (ropeAckPending != 4 || !launcher.OwnerUsesAuthorityPresentation) && NetworkInputHistory.IsNewer(launcher.CurrentInputTick, ropeAckAfterTick))
                    {
                        NetworkClient.Send(new RopeSmokeAck { phase = ropeAckPending, tick = launcher.CurrentInputTick });
                        ropeAckPending = -1;
                    }
                }
                PlayerInputManager.Instance.SetSyntheticHeld(1, PlayerAction.MoveRight, checkRopeInput ? ropeSmokePhase == 2 :
                    launcher != null && launcher.OwnerUsesAuthorityPresentation && !launcher.ApprovedLaunchActive);
            }
            if (revengeInput && NetworkManager.singleton is BombermanNetworkManager manager && manager != null)
                manager.TickLocalRevengeInputSmoke();
            if (checkItemMotion && NetworkClient.isConnected && Time.unscaledTime >= nextItemCheck)
            {
                nextItemCheck = Time.unscaledTime + .05f;
                foreach (var identity in NetworkClient.spawned.Values)
                    if (identity != null && identity.TryGetComponent<ItemPickup>(out var item) && item.HasPresentedApprovedItemMotion && observedMovingItems.Add(item.type))
                        Debug.Log("[LoadTest] item-motion presented type=" + item.type);
            }
            if (suspendPresenterInputs && NetworkClient.isConnected && NetworkClient.localPlayer != null &&
                SceneManager.GetActiveScene().name.StartsWith("Stage_", StringComparison.Ordinal) && Time.timeSinceLevelLoad > 8 &&
                NetworkClient.localPlayer.TryGetComponent<NetworkPlayerInput>(out var sender) && sender.enabled)
            {
                sender.enabled = false; // Drain in-flight commands before the server destroys this test actor.
                NetworkClient.Send(new PresenterSmokeReady { scene = SceneManager.GetActiveScene().name });
                Debug.Log("[LoadTest] presenter-removal input suspended netId=" + NetworkClient.localPlayer.netId);
            }
            if (!runRealInput || (completed && !repeatMountCycles) || !NetworkClient.isConnected || NetworkClient.localPlayer == null) return;
            var companion = NetworkClient.localPlayer.GetComponent<PlayerMountCompanion>();
            var input = PlayerInputManager.Instance;
            if (companion == null || input == null) return;
            var current = companion.GetMountedLouieType();
            if (completed && repeatMountCycles)
            {
                var movement = NetworkClient.localPlayer.GetComponent<MovementController>();
                bool right = movement != null && movement.transform.position.x < 0;
                float travel = Time.unscaledTime - mountTravelAt;
                bool outbound = travel < .35f, inbound = travel >= .95f && travel < 1.5f;
                input.SetSyntheticHeld(1, PlayerAction.MoveRight, current == MountedType.None && (right ? outbound : inbound));
                input.SetSyntheticHeld(1, PlayerAction.MoveLeft, current == MountedType.None && (right ? inbound : outbound));
                if (current == MountedType.None) return;
                completed = sent = false; observedMount = MountedType.None;
            }
            if (observedMount == MountedType.None)
            {
                if (current != MountedType.Mole && current != MountedType.Tank) return;
                observedMount = current; mountedAt = Time.unscaledTime;
            }
            if (!sent && Time.unscaledTime - mountedAt >= 2f)
            {
                sent = true; releaseTime = Time.unscaledTime + 0.5f;
                input.SetSyntheticHeld(1, PlayerAction.Select, true);
                Debug.Log("[LoadTest] full-input Select sent type=" + observedMount + " held=" + input.Get(1, PlayerAction.Select));
            }
            if (sent && Time.unscaledTime >= releaseTime)
            {
                input.SetSyntheticHeld(1, PlayerAction.Select, false);
                if (current == MountedType.None)
                {
                    completed = true; mountTravelAt = Time.unscaledTime;
                    Debug.Log("[LoadTest] full-input dismount acknowledged type=" + observedMount);
                }
            }
        }

        internal void CapturePresentationFrame()
        {
            if (string.IsNullOrEmpty(captureDirectory) || !NetworkClient.isConnected || NetworkClient.localPlayer == null) return;
            if (checkPortalInput && Time.unscaledTime >= portalPoseNext)
            {
                portalPoseNext = Time.unscaledTime + .05f;
                foreach (var identity in NetworkClient.spawned.Values)
                {
                    if (identity == null || !identity.TryGetComponent<MovementController>(out var mover)) continue;
                    // Sample after the production presenters, at the pose rendered this frame.
                    GameplayLatencyDiagnostics.Write(GameplayLatencyDiagnostics.For("portal_pose_sample", mover));
                }
            }
            var camera = Camera.main;
            if (camera == null) return;
            if (LocalBossSmoke.TryCapture(out string bossFrame))
            {
                CaptureMountFrame(camera, bossFrame);
                return;
            }
            if (LocalProjectileSmoke.TryCapture(out string projectileFrame))
            {
                CaptureMountFrame(camera, projectileFrame);
                return;
            }
            if (!captureWarmed && (SceneManager.GetActiveScene().name.StartsWith("BattleMode_", StringComparison.Ordinal) || SceneManager.GetActiveScene().name.StartsWith("Stage_", StringComparison.Ordinal)) && Time.timeSinceLevelLoad > 1)
            {
                CaptureMountFrame(camera, "warmup.png"); captureWarmed = true;
                return;
            }
            if(mechanismKind!=null&&mechanismPhase>0&&mechanismFrame<100&&Time.unscaledTime<mechanismCaptureUntil&&Time.unscaledTime>=mechanismCaptureNext)
            {
                float elapsed=Time.unscaledTime-mechanismPhaseAt;
                float interval=elapsed<1.5f&&(mechanismKind=="boiler"||mechanismKind=="dynamite"||mechanismKind=="powder")?.025f:
                    elapsed<1.5f||mechanismKind=="rocket"&&elapsed>4.8f&&elapsed<6.6f?.1f:.2f;
                mechanismCaptureNext=Time.unscaledTime+interval;
                if(NetworkManager.singleton is BombermanNetworkManager manager)manager.TraceMechanismViews(mechanismKind,mechanismPhase,mechanismRenderers,mechanismBlackoutPath);
                GameplayLatencyDiagnostics.Write(GameplayLatencyDiagnostics.For("mechanism_capture",detail:"fixture="+mechanismKind+";phase="+mechanismPhase+";explosions="+FindObjectsByType<BombExplosion>().Length));
                CaptureMountFrame(camera,$"mechanism-{mechanismPhase:D2}-{mechanismFrame++:D2}.png");
            }
            if (remoteWalk && walkStartedAt >= 0 && walkCaptureSerial < 60 && Time.unscaledTime >= walkCaptureNext)
            {
                // R01: this client's view of the OTHER (walking) player; UTC in the name matches frames to the motion logs.
                walkCaptureNext = Time.unscaledTime + .5f;
                CaptureMountFrame(camera, $"walk-{++walkCaptureSerial:D2}-{DateTime.UtcNow:HHmmss.fff}.png");
            }
            if (coopWorld && captureWarmed && coopWorldCaptureSerial < 100 && Time.unscaledTime >= coopWorldCaptureNext &&
                SceneManager.GetActiveScene().name.StartsWith("Stage_", StringComparison.Ordinal))
            {
                // R02: rendered co-op world view; the UTC in the name matches each PNG to the motion logs.
                coopWorldCaptureNext = Time.unscaledTime + (GamePauseController.IsPaused ? .25f : 1f);
                CaptureMountFrame(camera, $"world-{++coopWorldCaptureSerial:D3}-{(GamePauseController.IsPaused ? "paused" : "live")}-{DateTime.UtcNow:HHmmss.fff}.png");
            }
            if (bombAbilitySmoke && captureWarmed && Time.unscaledTime >= bombAbilityCaptureNext)
            {
                bombAbilityCaptureNext = Time.unscaledTime + .25f;
                CaptureMountFrame(camera, $"a14-{bombAbilitySerial:D3}-{Time.frameCount}.png");
            }
            if (checkMountVisuals) CaptureMountChangeFrames(camera);
            if (checkMountVisuals && visualCaptureSerial < 80 && (Time.unscaledTime >= visualCaptureAt || Time.unscaledTime >= visualAfkCaptureAt))
            {
                if (Time.unscaledTime >= visualCaptureAt) visualCaptureAt = float.PositiveInfinity;
                else visualAfkCaptureAt = float.PositiveInfinity;
                CaptureMountFrame(camera, $"visual-{++visualCaptureSerial:D2}-phase-{mountVisualPhase:D2}.png");
            }
            var fx = NetworkClient.localPlayer.GetComponent<NetworkBombFx>();
            bool riding = fx != null && (fx.RidingArcActive || checkMountVisuals && fx.PinkJumpVisualActive) || NetworkClient.localPlayer.GetComponent<NetworkPlayerInput>()?.ApprovedLaunchActive == true || NetworkClient.localPlayer.GetComponent<NetworkPlayerInput>()?.ApprovedCartActive == true || checkPortalInput && NetworkClient.localPlayer.GetComponent<NetworkPlayerInput>()?.OwnerUsesAuthorityPresentation == true;
            if (riding && !capturedRiding) { captureArc++; captureFrame = 0; captureNext = Time.unscaledTime; }
            if (checkPortalInput && capturedRiding && !riding)
            { portalCaptureUntil = Time.unscaledTime + .4f; portalCaptureNext = 0; }
            capturedRiding = riding;
            if (checkPortalInput && !riding && Time.unscaledTime < portalCaptureUntil && Time.unscaledTime >= portalCaptureNext)
            {
                portalCaptureNext = Time.unscaledTime + .12f;
                CaptureMountFrame(camera, $"portal-arrival-{portalCaptureFrame++:D2}.png");
            }
            if (!riding || captureArc > (checkMountVisuals ? 12 : 3) || Time.unscaledTime < captureNext) return;
            captureNext = Time.unscaledTime + 0.12f;
            CaptureMountFrame(camera, $"arc-{captureArc:D2}-{captureFrame++:D2}.png");
        }

        // R01: frame-by-frame evidence of mount / mount-type changes (Mole, Tank, Louies), for the owner and the observed
        // player. Every rendered frame after the change is saved for a short window, after all presenters (order 1500), so a
        // one-frame foot body next to a mount is visible in the PNGs. PNG encoding slows those frames; frames stay consecutive.
        readonly Dictionary<uint, (MountedType type, bool mounted)> mountChangeState = new();
        int mountChangeSerial, mountChangeFramesLeft, mountChangeFrame;
        string mountChangeLabel;
        void CaptureMountChangeFrames(Camera camera)
        {
            foreach (var identity in NetworkClient.spawned.Values)
            {
                if (identity == null || !identity.TryGetComponent<MovementController>(out var mover) || !identity.TryGetComponent<PlayerMountCompanion>(out var companion)) continue;
                var state = (companion.GetMountPresentationType(), mover.IsMounted);
                if (mountChangeState.TryGetValue(identity.netId, out var previous) && previous != state && mountChangeSerial < 24)
                {
                    mountChangeSerial++; mountChangeFramesLeft = 8; mountChangeFrame = 0;
                    mountChangeLabel = $"{(identity.isLocalPlayer ? "owner" : "observed")}-p{mover.PlayerId}-{previous.type}-{state.Item1}";
                    GameplayLatencyDiagnostics.Write(GameplayLatencyDiagnostics.For("mount_change_capture", mover, detail: "serial=" + mountChangeSerial + ";label=" + mountChangeLabel));
                }
                mountChangeState[identity.netId] = state;
            }
            if (mountChangeFramesLeft <= 0) return;
            mountChangeFramesLeft--;
            CaptureMountFrame(camera, $"mountchange-{mountChangeSerial:D2}-{mountChangeFrame++:D2}-f{Time.frameCount}-{mountChangeLabel}.png");
        }

        void CaptureMountFrame(Camera camera, string filename)
        {
            if (LocalBossSmoke.CaptureBackbuffer)
            {
                // Opt-in visible clients: capture the actual end-of-frame, including stencil and HUD.
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(captureDirectory, filename));
                return;
            }
            // Batch clients do not present their backbuffer. Render the actual
            // gameplay camera through URP instead of saving a black screenshot.
            var target = RenderTexture.GetTemporary(Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height), 24, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            Texture2D pixels = null;
            int previousMask=camera.cullingMask;
            var canvases=new List<(Canvas canvas,RenderMode mode,Camera camera,float distance,bool sorting,int order)>();
            try
            {
                if(mechanismKind!=null)
                {
                    // A camera request omits ScreenSpaceOverlay canvases. Temporarily
                    // render them through this camera so legacy blackout/UI is visible.
                    foreach(var canvas in FindObjectsByType<Canvas>())
                        if(canvas.isRootCanvas&&canvas.isActiveAndEnabled&&canvas.renderMode!=RenderMode.WorldSpace&&
                           (canvas.renderMode==RenderMode.ScreenSpaceOverlay||canvas.worldCamera!=camera))
                        {
                            canvases.Add((canvas,canvas.renderMode,canvas.worldCamera,canvas.planeDistance,canvas.overrideSorting,canvas.sortingOrder));
                            int order=canvas.sortingOrder;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;
                            canvas.planeDistance=camera.nearClipPlane+.5f;canvas.overrideSorting=true;canvas.sortingOrder=Mathf.Clamp(32000+order,30000,32760);
                        }
                    camera.cullingMask|=LayerMask.GetMask("UI");Canvas.ForceUpdateCanvases();
                }
                var request = new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = target };
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = target;
                pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); pixels.Apply();
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(captureDirectory, filename), pixels.EncodeToPNG());
            }
            finally
            {
                camera.cullingMask=previousMask;
                foreach(var state in canvases)if(state.canvas!=null)
                {
                    state.canvas.renderMode=state.mode;state.canvas.worldCamera=state.camera;state.canvas.planeDistance=state.distance;
                    state.canvas.overrideSorting=state.sorting;state.canvas.sortingOrder=state.order;
                }
                if(canvases.Count>0)Canvas.ForceUpdateCanvases();
                RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target);
                if (pixels != null) Destroy(pixels);
            }
        }

        // R01 (Development only): deterministic walking through the normal synthetic hardware slot so that the
        // OTHER client can be measured against the server. It only drives this process's own player; it never
        // supplies another player's input and it does not run unless -load-test-remote-walk is passed.
        void DriveRemoteWalk()
        {
            var input = PlayerInputManager.Instance;
            if (!remoteWalk || input == null || !NetworkClient.isConnected || NetworkClient.localPlayer == null) return;
            var mover = NetworkClient.localPlayer.GetComponent<MovementController>();
            if (mover == null || !mover.CanPredictNetworkMovement || GamePauseController.IsPaused) { ReleaseRemoteWalk(input); return; }
            if (walkStartedAt < 0)
            {
                walkStartedAt = Time.unscaledTime;
                Debug.Log("[LoadTest] remote-walk started player=" + mover.PlayerId);
            }
            // 14 s cycle with stops between segments; odd/even players are offset so the two clients rarely overlap.
            float t = (Time.unscaledTime - walkStartedAt + (mover.PlayerId & 1) * 3.5f) % 14f;
            input.SetSyntheticHeld(1, PlayerAction.MoveRight, t < 2.4f);
            input.SetSyntheticHeld(1, PlayerAction.MoveLeft, t >= 3f && t < 5.4f);
            input.SetSyntheticHeld(1, PlayerAction.MoveUp, t >= 6f && t < 8.4f);
            input.SetSyntheticHeld(1, PlayerAction.MoveDown, t >= 9f && t < 11.4f);
        }

        static void ReleaseRemoteWalk(PlayerInputManager input)
        {
            foreach (var action in new[] { PlayerAction.MoveLeft, PlayerAction.MoveRight, PlayerAction.MoveUp, PlayerAction.MoveDown })
                input.SetSyntheticHeld(1, action, false);
        }

        void OnDisable()
        {
            if (PlayerInputManager.Instance == null) return;
            if (remoteWalk) ReleaseRemoteWalk(PlayerInputManager.Instance);
            if (checkMountVisuals)
                foreach (var action in new[] { PlayerAction.ActionL, PlayerAction.ActionC, PlayerAction.MoveUp, PlayerAction.MoveDown, PlayerAction.MoveRight, PlayerAction.MoveLeft })
                    PlayerInputManager.Instance.SetSyntheticHeld(1, action, false);
            if (runRealInput || checkLauncherInput)
                foreach (var action in new[] { PlayerAction.Select, PlayerAction.MoveLeft, PlayerAction.MoveRight, PlayerAction.MoveUp })
                    PlayerInputManager.Instance.SetSyntheticHeld(1, action, false);
            if (!revengeInput) return;
            foreach (var action in new[] { PlayerAction.MoveUp, PlayerAction.ActionA, PlayerAction.ActionC })
                PlayerInputManager.Instance.SetSyntheticHeld(1, action, false);
        }
    }

    [DefaultExecutionOrder(1500)]
    public sealed class LocalNetworkSmokeCapture : MonoBehaviour
    {
        public LocalNetworkInputSmoke source;
        void LateUpdate() { if (source != null) source.CapturePresentationFrame(); }
    }

    public sealed class LocalOnlineFlowSmoke : MonoBehaviour
    {
        float nextInput, stageAt;
        bool configured, left, observedStage;
        string lastScene = "";
        string originalControls;
        [Serializable] sealed class View { public int state = 0, index = 0; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var args = Environment.GetCommandLineArgs();
            if (!Debug.isDebugBuild || Array.IndexOf(args, "-load-test-owner-flow") < 0 || Array.IndexOf(args, "-server") >= 0) return;
            Application.runInBackground = true; Application.targetFrameRate = 60; QualitySettings.vSyncCount = 0;
            var root = new GameObject("LocalOnlineFlowSmoke");
            DontDestroyOnLoad(root); root.AddComponent<LocalOnlineFlowSmoke>();
        }

        void Update()
        {
            string scene = SceneManager.GetActiveScene().name;
            if (scene != lastScene)
            {
                lastScene = scene;
                Debug.Log("[OwnerFlowSmoke] scene=" + scene + " player=" + BombermanNetworkManager.LocalRoomPlayerId);
            }
            if (observedStage && scene == "TitleScreen")
            {
                Debug.Log("[OwnerFlowSmoke] returned to title disconnected=" + !NetworkClient.active);
                enabled = false; return;
            }
            if (!NetworkClient.isConnected || !NetworkClient.connection.isAuthenticated) return;
            originalControls ??= JsonUtility.ToJson(global::SaveSystem.Data.controls[0]);
            if (JsonUtility.ToJson(global::SaveSystem.Data.controls[0]) != originalControls)
                throw new InvalidOperationException("Owner flow changed local hardware controls");
            if (scene.StartsWith("Stage_", StringComparison.Ordinal))
            {
                if (NetworkClient.localPlayer == null || !NetworkClient.ready) return;
                if (!observedStage)
                {
                    observedStage = true; stageAt = Time.unscaledTime;
                    Debug.Log("[OwnerFlowSmoke] gameplay slot=" + global::SaveSystem.Data.activeSlotIndex +
                        " difficulty=" + global::SaveSystem.GetActiveNormalGameDifficulty() + " players=" + BombermanNetworkManager.RoomPlayers.Length +
                        " boss=" + BossRushSession.IsActive + " bossDifficulty=" + BossRushSession.CurrentDifficulty);
                }
                // The non-owner exercises closing the entire room as requested.
                if (!left && !BombermanNetworkManager.IsRoomAdmin && Time.unscaledTime - stageAt > 8)
                { left = true; BombermanNetworkManager.LeaveRoom(); }
                return;
            }
            if (Time.unscaledTime < nextInput) return;
            nextInput = Time.unscaledTime + 1;
            if (scene == NetworkLobby.SceneName && BombermanNetworkManager.IsRoomAdmin && BombermanNetworkManager.RoomPlayers.Length >= 2 && !configured)
            {
                configured = true;
                var fixture = new SaveData { activeSlotIndex = 1, bossRushUnlocked = true, nightmareUnlocked = true };
                fixture.slots[1].started = true;
                fixture.slots[1].difficulty = (int)Assets.Scripts.SaveSystem.NormalGameDifficulty.Hard;
                fixture.slots[1].unlockedStages.Add("Stage_1-1");
                fixture.slots[1].unlockedStages.Add("Stage_2-5");
                NetworkClient.Send(new OnlineConfigureRoomMessage { ownerSave = JsonUtility.ToJson(fixture) });
            }
            else if (scene == "SkinSelect")
            {
                var characters = FindAnyObjectByType<BomberSkinSelectMenu>();
                if (characters != null && characters.ActiveOnlineSelectionPlayerId == BombermanNetworkManager.LocalRoomPlayerId) Press(PlayerAction.ActionA, scene);
            }
            else if (BombermanNetworkManager.IsRoomAdmin && scene == "SaveFileMenu" && !string.IsNullOrEmpty(BombermanNetworkManager.FlowView))
            {
                var view = JsonUtility.FromJson<View>(BombermanNetworkManager.FlowView);
                int desired = 1; // Continue, then the owner's second save slot.
                Press(view.index == desired ? PlayerAction.ActionA : PlayerAction.MoveDown, scene);
            }
            else if (BombermanNetworkManager.IsRoomAdmin && scene == "WorldMap") Press(PlayerAction.ActionA, scene);
            else if (BombermanNetworkManager.IsRoomAdmin && scene == "BossRush" && !string.IsNullOrEmpty(BombermanNetworkManager.FlowView))
            {
                var view = JsonUtility.FromJson<View>(BombermanNetworkManager.FlowView);
                Press(view.index == (int)BossRushDifficulty.HARD ? PlayerAction.ActionA : PlayerAction.MoveDown, scene);
            }
        }

        static void Press(PlayerAction action, string scene)
        {
            NetworkClient.Send(new OnlineSelectionInputMessage { pressed = 1 << (int)action, scene = scene });
            Debug.Log("[OwnerFlowSmoke] input=" + action + " scene=" + scene);
        }
    }

    // Many real KCP peers in one process; receives/acknowledges every packet but
    // does not load, instantiate or render the replicated client world.
    public sealed class LocalServerLoadTest : MonoBehaviour
    {
        readonly List<Peer> peers = new();
        float started, duration, nextReport;
        bool configureWhenReady, configurationSent;

        internal static bool UseSyntheticLoadClients(string[] args) =>
            Array.IndexOf(args, "-load-test-client") >= 0 && Array.IndexOf(args, "-online-join") < 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            // Real rendered clients also use -load-test-client for capture/quit, but join through Mirror.
            // Starting raw KCP load peers there races the scene bootstrap and targets an unrelated default port.
            if (!Debug.isDebugBuild || !UseSyntheticLoadClients(Environment.GetCommandLineArgs())) return;
            SceneManager.LoadScene(NetworkLobby.SceneName);
            var root = new GameObject("LocalServerLoadTest");
            DontDestroyOnLoad(root);
            root.AddComponent<LocalServerLoadTest>();
        }

        void Start()
        {
            var args = Environment.GetCommandLineArgs();
            configureWhenReady = Array.IndexOf(args, "-load-test-configure") >= 0;
            int first = BombermanNetworkManager.ReadDedicatedNumber(args, "-load-test-port", 18777, 1024, 65000);
            int rooms = BombermanNetworkManager.ReadDedicatedNumber(args, "-load-test-rooms", 1, 1, 32);
            int players = BombermanNetworkManager.ReadDedicatedNumber(args, "-load-test-players", 4, 2, 6);
            duration = BombermanNetworkManager.ReadDedicatedNumber(args, "-load-test-duration", 180, 30, 1800);
            var prefab = NetworkManager.singleton.playerPrefab;
            byte inputIndex = (byte)Array.FindIndex(prefab.GetComponents<NetworkBehaviour>(), b => b is NetworkPlayerInput);
            for (int room = 0; room < rooms; room++)
                for (int player = 0; player < players; player++) peers.Add(new Peer((ushort)(first + room), player, inputIndex));
            started = Time.realtimeSinceStartup;
            Application.runInBackground = true; Application.targetFrameRate = 60; QualitySettings.vSyncCount = 0;
        }

        void Update()
        {
            foreach (var peer in peers) peer.Tick();
            if (configureWhenReady && !configurationSent && peers.TrueForAll(peer => peer.RoomReady))
            {
                configurationSent = true;
                foreach (var peer in peers) peer.ConfigureRoom();
            }
            if (Time.realtimeSinceStartup >= nextReport)
            {
                nextReport = Time.realtimeSinceStartup + 5;
                int connected = 0, spawned = 0; long received = 0;
                foreach (var peer in peers) { if (peer.client.connected) connected++; if (peer.netId != 0) spawned++; received += peer.received; }
                Debug.Log($"[LoadClients] connected={connected}/{peers.Count} spawned={spawned} receivedBytes={received}");
            }
            if (Time.realtimeSinceStartup - started >= duration) Application.Quit();
        }

        void OnDestroy() { foreach (var peer in peers) peer.client.Disconnect(); }

        sealed class Peer
        {
            public readonly KcpClient client;
            public uint netId;
            public long received;
            readonly Unbatcher unbatcher = new();
            readonly Batcher batcher = new(1200);
            readonly int player;
            readonly byte inputIndex;
            float nextInput, nextBomb;
            bool authenticated, controlsPaused;
            bool roomOwner;
            int roomPlayers;
            // Lobby slots precede gameplay objects. Requiring a spawned netId
            // here deadlocks a normal release room before its owner can start.
            public bool RoomReady => client.connected && authenticated && roomPlayers >= 2;

            public void ConfigureRoom()
            {
                if (roomOwner) Send(new OnlineConfigureRoomMessage { ownerSave = global::SaveSystem.CaptureRoomOwnerSave() });
            }
            // Cecil full signature used by Mirror's weaver (verified via the server delegate).
            static readonly ushort ControlsHash = (ushort)("System.Void Assets.Scripts.Netcode.NetworkPlayerInput::CmdControls(System.Int32,System.Int32)".GetStableHashCode() & 0xffff);

            public Peer(ushort port, int player, byte inputIndex)
            {
                this.player = player; this.inputIndex = inputIndex;
                client = new KcpClient(() => Send(new OnlineVersionAuthenticator.Request { gameVersion = Application.version, protocol = OnlineVersionAuthenticator.ProtocolVersion }),
                    Receive, () => Debug.Log("[LoadClients] disconnected port=" + port),
                    (error, message) => Debug.LogWarning("[LoadClients] " + error + ": " + message),
                    new KcpConfig(DualMode: false, SendWindowSize: 4096, ReceiveWindowSize: 4096));
                client.Connect("127.0.0.1", port);
            }

            void Send<T>(T message) where T : struct, NetworkMessage
            {
                using var writer = NetworkWriterPool.Get();
                NetworkMessages.Pack(message, writer);
                batcher.AddMessage(writer.ToArraySegment(), Time.realtimeSinceStartupAsDouble);
            }

            void Receive(ArraySegment<byte> data, KcpChannel channel)
            {
                received += data.Count;
                if (!unbatcher.AddBatch(data)) throw new InvalidOperationException("Invalid server batch");
                while (unbatcher.GetNextMessage(out var message, out _))
                {
                    using var reader = NetworkReaderPool.Get(message);
                    if (!NetworkMessages.UnpackId(reader, out ushort id)) continue;
                    if (id == NetworkMessages.GetId<OnlineVersionAuthenticator.Response>())
                    {
                        authenticated = reader.Read<OnlineVersionAuthenticator.Response>().compatible;
                        if (authenticated) { Send(new ReadyMessage()); Send(new AddPlayerMessage()); }
                    }
                    else if (id == NetworkMessages.GetId<OnlineRoomMessage>())
                    {
                        var room = reader.Read<OnlineRoomMessage>();
                        roomOwner = room.playerId > 0 && room.playerId == room.adminPlayerId;
                        roomPlayers = room.players?.Length ?? 0;
                    }
                    else if (id == NetworkMessages.GetId<NotReadyMessage>())
                    {
                        // Match a real Mirror client's input gate while the
                        // server prepares a scene change; the next spawn restores it.
                        netId = 0;
                    }
                    else if (id == NetworkMessages.GetId<RoundOverMessage>())
                    {
                        reader.Read<RoundOverMessage>();
                        controlsPaused = true;
                    }
                    else if (id == NetworkMessages.GetId<SceneMessage>())
                    {
                        reader.Read<SceneMessage>(); netId = 0; controlsPaused = false;
                        Send(new ReadyMessage()); Send(new AddPlayerMessage());
                    }
                    else if (id == NetworkMessages.GetId<SpawnMessage>())
                    {
                        var spawn = reader.Read<SpawnMessage>();
                        if (spawn.isLocalPlayer) netId = spawn.netId;
                    }
                    else if (id == NetworkMessages.GetId<NetworkPingMessage>())
                        Send(new NetworkPongMessage(reader.Read<NetworkPingMessage>().localTime, 0, 0));
                    else if (id == NetworkMessages.GetId<ObjectDestroyMessage>())
                    {
                        if (reader.Read<ObjectDestroyMessage>().netId == netId) netId = 0;
                    }
                }
            }

            public void Tick()
            {
                client.TickIncoming();
                float now = Time.realtimeSinceStartup;
                if (authenticated && !controlsPaused && netId != 0 && now >= nextInput)
                {
                    nextInput = now + 1f / 30f;
                    int direction = ((int)(now * 0.7f) + player) % 4;
                    int pressed = now >= nextBomb ? 16 : 0;
                    if (pressed != 0) nextBomb = now + 1.2f;
                    using var payload = NetworkWriterPool.Get();
                    payload.WriteInt(1 << direction); payload.WriteInt(pressed);
                    Send(new CommandMessage { netId = netId, componentIndex = inputIndex, functionHash = ControlsHash, payload = payload.ToArraySegment() });
                    Send(new TimeSnapshotMessage());
                }
                using (var batch = NetworkWriterPool.Get())
                    while (batcher.GetBatch(batch)) { client.Send(batch.ToArraySegment(), KcpChannel.Reliable); batch.Reset(); }
                client.TickOutgoing();
            }
        }
    }
}
#endif

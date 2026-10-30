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
    // Development-only command-line harness. No remote message can enable it.
    public partial class BombermanNetworkManager
    {
        bool loadTestConfigured, loadTestEnabled, loadTestStarted, loadTestSpawnsChecked, loadTestPresenterChecked, loadTestRevengeChecked, loadTestLaunchersChecked, loadTestVisualsChecked;
        string loadTestLauncherKind;
        readonly HashSet<int> loadTestPresenterReady = new();
        int loadTestPlayers;
        float loadTestStart, loadTestDuration, loadTestNextHealth;

        void TickLocalLoadTest()
        {
            if (!dedicatedServer || !NetworkServer.active) return;
            if (!loadTestConfigured)
            {
                loadTestConfigured = true;
                loadTestEnabled = Debug.isDebugBuild && HasArg("-load-test-server");
                if (!loadTestEnabled) return;
                if (HasArg("-load-test-presenter-removal") || HasArg("-load-test-mount-visuals"))
                    NetworkServer.RegisterHandler<PresenterSmokeReady>((peer, message) =>
                    {
                        if (peer.isReady && peer.identity != null && message.scene == SceneManager.GetActiveScene().name)
                            loadTestPresenterReady.Add(peer.connectionId);
                    });
                var args = Environment.GetCommandLineArgs();
                loadTestPlayers = ReadDedicatedNumber(args, "-load-test-players", RoomCapacity, 2, RoomCapacity);
                loadTestDuration = ReadDedicatedNumber(args, "-load-test-duration", 180, 30, 1800);
                int launcherArg = Array.IndexOf(args, "-load-test-launchers");
                if (launcherArg >= 0)
                {
                    loadTestLauncherKind = launcherArg + 1 < args.Length ? args[launcherArg + 1] : "";
                    if (loadTestLauncherKind != "arena" && loadTestLauncherKind != "spring" && loadTestLauncherKind != "cannon")
                        throw new ArgumentException("Unknown Development launcher fixture");
                }
                loadTestStart = Time.realtimeSinceStartup;
                Debug.Log("[LoadTest] Synthetic durable actors; network controls at 30 Hz. Not normal game rules.");
            }
            if (!loadTestEnabled) return;
            if (Time.realtimeSinceStartup - loadTestStart > loadTestDuration)
            {
                Debug.Log("[LoadTest] completed");
                leavingRoom = true; // Mark shutdown before StopServer disconnect callbacks.
                StopServer(); Application.Quit(); return;
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
                    if (HasArg("-load-test-revenge-input")) global::SaveSystem.SetBattleModeRevengeBomberEnabled(true);
                    var modes = global::SaveSystem.GetBattleModePlayerControlModes();
                    bool fillCom = !HasArg("-load-test-mount-visuals") && HasArg("-load-test-com") && !HasArg("-load-test-mount-cycles") && loadTestLauncherKind == null;
                    if (fillCom) global::SaveSystem.SetBattleModeComputerLevel(BattleModeComputerLevel.Hard);
                    for (int i = 0; i < modes.Length; i++) modes[i] = IsConnectedSlot(i + 1) ? BattleModePlayerControlMode.Man :
                        fillCom ? BattleModePlayerControlMode.Com : BattleModePlayerControlMode.Off;
                    global::SaveSystem.SetBattleModePlayerControlModes(modes);
                    GameSession.Instance?.SetActivePlayerIds(RoomPlayers);
                    ServerLaunchConfiguredBattle(loadTestLauncherKind == "arena" ? "BattleMode_4" : "BattleMode_15");
                }
            }
            // Keep combat running rather than measuring an empty arena after bot deaths.
            if (loadTestStarted && !InLobbyScene() && Time.unscaledTime >= loadTestNextHealth)
            {
                loadTestNextHealth = Time.unscaledTime + 1;
                foreach (var health in FindObjectsByType<CharacterHealth>())
                    if (!health.IsDead) health.life = 200;
                if (!loadTestSpawnsChecked && HasArg("-load-test-spawns") && !IsCoopRoom && Time.timeSinceLevelLoad > 3)
                {
                    loadTestSpawnsChecked = true;
                    StartCoroutine(CheckLoadTestSpawns());
                }
                if (!loadTestVisualsChecked && HasArg("-load-test-mount-visuals") && Time.timeSinceLevelLoad > 12 && !BattleStartPending && loadTestPresenterReady.Count >= loadTestPlayers)
                {
                    loadTestVisualsChecked = true; StartCoroutine(CheckLoadTestMountVisuals());
                }
                if (!loadTestLaunchersChecked && loadTestLauncherKind != null && Time.timeSinceLevelLoad > 12 &&
                    (!IsCoopRoom || OnlineCoopPlayersReady) && !BattleStartPending)
                {
                    loadTestLaunchersChecked = true;
                    StartCoroutine(CheckLoadTestLaunchers());
                }
                if (!loadTestRevengeChecked && HasArg("-load-test-revenge-input") && !IsCoopRoom && Time.timeSinceLevelLoad > 12)
                {
                    loadTestRevengeChecked = true;
                    StartCoroutine(CheckLoadTestRevengeInput());
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

        System.Collections.IEnumerator CheckLoadTestMountVisuals()
        {
            int index = 0;
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
                health.SetExternalInvulnerability(protectedBefore); index++;
            }
            if (index != 2) throw new InvalidOperationException("Visual fixture requires two owners");
            Debug.Log("[LoadTest] mount visuals completed players=2");
        }

        System.Collections.IEnumerator CheckLoadTestLaunchers()
        {
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
                if (loadTestLauncherKind == "arena")
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
                mover.SnapToWorldPoint(entry, roundToGrid: false);
                ServerReconcileFixturePlayer(input);
                float deadline = Time.unscaledTime + 10;
                while (Time.unscaledTime < deadline && (!input.ApprovedLaunchSnapshot.active || input.ApprovedLaunchSnapshot.sequence == previous)) yield return null;
                var launch = input.ApprovedLaunchSnapshot;
                if (!launch.active || launch.sequence == previous) throw new InvalidOperationException("Native launcher did not produce an approved curve " + loadTestLauncherKind);
                Debug.Log("[LoadTest] launcher started kind=" + launch.arc.kind + " player=" + mover.PlayerId + " sequence=" + launch.sequence + " mounted=" + mover.IsMounted);
                bool cancel = index == 1 && mechanism != null;
                if (cancel)
                {
                    yield return new WaitForSecondsRealtime(.2f);
                    mechanism.enabled = false;
                }
                deadline = Time.unscaledTime + 10;
                while (Time.unscaledTime < deadline && input.ApprovedLaunchSnapshot.active) yield return null;
                if (input.ApprovedLaunchSnapshot.active) throw new InvalidOperationException("Native launcher failed to end its approved curve");
                if (mechanism != null) mechanism.enabled = false; // Stop an idle spring's deliberate repeat.
                yield return new WaitForSecondsRealtime(.5f);
                if (mover.InputLocked || mover.ExternalMovementOverride || mover.VisualOverrideActive)
                    throw new InvalidOperationException("Native launcher left player control locked");
                Debug.Log("[LoadTest] launcher finished kind=" + launch.arc.kind + " player=" + mover.PlayerId + " cancelled=" + cancel);
                mover.SnapToWorldPoint(original, roundToGrid: false); ServerReconcileFixturePlayer(input);
                health.SetExternalInvulnerability(priorProtection);
                index++;
            }
            if (index != 2) throw new InvalidOperationException("Launcher fixture requires exactly two actors");
            Debug.Log("[LoadTest] launchers completed kind=" + loadTestLauncherKind + " players=2");
        }
        static void ServerReconcileFixturePlayer(NetworkPlayerInput input)
        {
            if (NetworkServer.active && input != null && input.isServer) input.ServerReconcileMovementNow();
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
        int captureArc, captureFrame;
        float captureNext, visualCaptureAt = float.PositiveInfinity, visualAfkCaptureAt = float.PositiveInfinity;
        int visualCaptureSerial;
        bool checkMountVisuals, mountVisualHandler, mountVisualReady;
        int mountVisualPhase;
        bool checkLauncherInput, checkItemMotion, runRealInput, suspendPresenterInputs, revengeInput, quitAfterDisconnect, sawConnection;

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
            smoke.checkMountVisuals = Array.IndexOf(args, "-load-test-mount-visuals") >= 0;
            smoke.checkLauncherInput = Array.IndexOf(args, "-load-test-launchers") >= 0;
            smoke.checkItemMotion = Array.IndexOf(args, "-load-test-item-motion") >= 0;
            smoke.runRealInput = Array.IndexOf(args, "-load-test-real-input") >= 0;
            smoke.repeatMountCycles = Array.IndexOf(args, "-load-test-mount-cycles") >= 0;
            smoke.suspendPresenterInputs = Array.IndexOf(args, "-load-test-presenter-removal") >= 0;
            smoke.revengeInput = Array.IndexOf(args, "-load-test-revenge-input") >= 0;
            smoke.quitAfterDisconnect = Array.IndexOf(args, "-load-test-client") >= 0;
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

        void Update()
        {
            if (quitAfterDisconnect)
            {
                if (NetworkClient.isConnected) sawConnection = true;
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
                PlayerInputManager.Instance.SetSyntheticHeld(1, PlayerAction.MoveRight, launcher != null && launcher.OwnerUsesAuthorityPresentation && !launcher.ApprovedLaunchActive);
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
            var camera = Camera.main;
            if (camera == null) return;
            if (!captureWarmed && (SceneManager.GetActiveScene().name.StartsWith("BattleMode_", StringComparison.Ordinal) || SceneManager.GetActiveScene().name.StartsWith("Stage_", StringComparison.Ordinal)) && Time.timeSinceLevelLoad > 1)
            {
                CaptureMountFrame(camera, "warmup.png"); captureWarmed = true;
                return;
            }
            if (checkMountVisuals && visualCaptureSerial < 80 && (Time.unscaledTime >= visualCaptureAt || Time.unscaledTime >= visualAfkCaptureAt))
            {
                if (Time.unscaledTime >= visualCaptureAt) visualCaptureAt = float.PositiveInfinity;
                else visualAfkCaptureAt = float.PositiveInfinity;
                CaptureMountFrame(camera, $"visual-{++visualCaptureSerial:D2}-phase-{mountVisualPhase:D2}.png");
            }
            var fx = NetworkClient.localPlayer.GetComponent<NetworkBombFx>();
            bool riding = fx != null && (fx.RidingArcActive || checkMountVisuals && fx.PinkJumpVisualActive) || NetworkClient.localPlayer.GetComponent<NetworkPlayerInput>()?.ApprovedLaunchActive == true;
            if (riding && !capturedRiding) { captureArc++; captureFrame = 0; captureNext = Time.unscaledTime; }
            capturedRiding = riding;
            if (!riding || captureArc > (checkMountVisuals ? 12 : 3) || Time.unscaledTime < captureNext) return;
            captureNext = Time.unscaledTime + 0.12f;
            CaptureMountFrame(camera, $"arc-{captureArc:D2}-{captureFrame++:D2}.png");
        }

        void CaptureMountFrame(Camera camera, string filename)
        {
            // Batch clients do not present their backbuffer. Render the actual
            // gameplay camera through URP instead of saving a black screenshot.
            var target = RenderTexture.GetTemporary(Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height), 24, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            Texture2D pixels = null;
            try
            {
                var request = new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = target };
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = target;
                pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); pixels.Apply();
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(captureDirectory, filename), pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target);
                if (pixels != null) Destroy(pixels);
            }
        }

        void OnDisable()
        {
            if (PlayerInputManager.Instance == null) return;
            if (checkMountVisuals)
                foreach (var action in new[] { PlayerAction.ActionL, PlayerAction.ActionC, PlayerAction.MoveUp, PlayerAction.MoveDown, PlayerAction.MoveRight, PlayerAction.MoveLeft })
                    PlayerInputManager.Instance.SetSyntheticHeld(1, action, false);
            if (runRealInput || checkLauncherInput)
                foreach (var action in new[] { PlayerAction.Select, PlayerAction.MoveLeft, PlayerAction.MoveRight })
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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!Debug.isDebugBuild || Array.IndexOf(Environment.GetCommandLineArgs(), "-load-test-client") < 0) return;
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

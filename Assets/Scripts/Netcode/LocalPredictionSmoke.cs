#if UNITY_EDITOR || DEBUG
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Assets.Scripts.Netcode
{
    public struct NormalWaterInputStep : Mirror.NetworkMessage { public string scene; public int held; public float seconds; }
    // Opt-in, isolated-save reproduction through the real hardware input owner.
    [DefaultExecutionOrder(-250)]
    public sealed class LocalPredictionSmoke : MonoBehaviour
    {
        static readonly string[] CommandLine = Environment.GetCommandLineArgs();
        internal static bool ChatInputReproduction => NormalGamePlaytest && NormalCase == "chat";
        readonly System.Collections.Generic.Dictionary<int, (string scene, int held, double until)> waterInputs = new();
        bool waterInputRegistered;
        static readonly PlayerAction[] WaterActions = { PlayerAction.MoveUp, PlayerAction.MoveDown, PlayerAction.MoveLeft, PlayerAction.MoveRight, PlayerAction.ActionA, PlayerAction.ActionC };
        UnityEngine.InputSystem.Keyboard chatKeyboard;
        int chatPhase, chatMessageIndex;
        double nextChatStep;
        readonly string[] chatMessages = { "PT: fonte maior e correção.", "EN: larger online text.", "ES: texto más grande.", "JP: オンライン通信テスト", "Rascunho longo para conferir o final da frase, o cursor e o envio completo no chat online." };
        double began = -1;
        bool offline;
        MovementController[] actors = Array.Empty<MovementController>();
        double refreshActors;
        internal static bool NormalGamePlaytest => Array.IndexOf(CommandLine, "-normal-game-playtest") >= 0;
        static string Argument(string name, string fallback)
        {
            var args = CommandLine; int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
        }
        internal static string NormalCase => Argument("-normal-game-case", "easy");
        internal static bool LocalHostPlaytest => Array.IndexOf(CommandLine, "-normal-game-host") >= 0;
        internal static bool UiPreview => Array.IndexOf(CommandLine, "-normal-ui-preview") >= 0;
        static bool Enabled => UiPreview || NormalGamePlaytest || Array.IndexOf(CommandLine, "-p0-prediction") >= 0;
        readonly System.Collections.Generic.HashSet<int> observedDead = new();
        readonly System.Collections.Generic.HashSet<int> observedRevived = new();
        readonly System.Collections.Generic.Dictionary<int, double> revivedAt = new();
        readonly System.Collections.Generic.Dictionary<int, Vector2> revivedPositions = new();
        readonly System.Collections.Generic.Dictionary<int, MovementController> observedActors = new();
        readonly System.Collections.Generic.HashSet<int> recoveredMovement = new();
        double creditsAt = -1;
        int creditsInputPhase;
        bool creditsFinalSeen;
        double inspectionTraceAt;
        BoilerTrace boilerTrace;
        readonly System.Collections.Generic.Dictionary<int, float> portalBodyAlpha = new();
        readonly System.Collections.Generic.HashSet<int> portalBlinkSeen = new(), portalZeroSeen = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!Debug.isDebugBuild || !Enabled) return;
            ValidateSaveIsolationGuard();
            Application.runInBackground = true;
            ApplyPlaytestVideoOutput();
            var go = new GameObject("P0PredictionReproduction"); DontDestroyOnLoad(go);
            var probe = go.AddComponent<LocalPredictionSmoke>();
            if (NormalGamePlaytest && NormalCase == "pink" && SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null) probe.StartCoroutine(probe.ObservePinkEndFrames());
            if (NormalGamePlaytest && NormalCase == "rooms" && SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null) probe.StartCoroutine(probe.ObserveRoomEndFrames());
            if (NormalGamePlaytest && (NormalCase is "cart" or "geyser" or "water") && SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null) probe.StartCoroutine(probe.ObserveNativeFinalFrames());
            if (NormalGamePlaytest && NormalCase == "uiscale" && SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null) probe.StartCoroutine(probe.ObserveUiScale());
            if (UiPreview)
            {
                if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SB6_SAVE_DIRECTORY")))
                    throw new InvalidOperationException("UI preview requires an isolated save directory");
                probe.StartCoroutine(probe.PreviewLobby()); return;
            }
            if (LocalHostPlaytest)
            {
                if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SB6_SAVE_DIRECTORY")))
                    throw new InvalidOperationException("Host playtest requires an isolated save directory");
                probe.StartCoroutine(probe.Host()); return;
            }
            if (Array.IndexOf(CommandLine, "-p0-offline") >= 0)
            {
                if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SB6_SAVE_DIRECTORY")))
                    throw new InvalidOperationException("P0 offline requires an isolated save directory");
                probe.StartCoroutine(probe.Offline());
            }
        }

        static void ApplyPlaytestVideoOutput()
        {
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                bool fullscreen = Array.IndexOf(CommandLine, "-normal-native-fullscreen") >= 0;
                global::SaveSystem.SetVideoSettings(fullscreen, 2);
                int width = int.TryParse(Argument("-normal-window-width", "512"), out int requestedWidth) ? Mathf.Clamp(requestedWidth, 256, 3840) : 512;
                int height = int.TryParse(Argument("-normal-window-height", "448"), out int requestedHeight) ? Mathf.Clamp(requestedHeight, 224, 2160) : 448;
                Screen.SetResolution(width, height, fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
            }
        }

        internal static void ValidateSaveIsolationGuard()
        {
            string sandbox = Environment.GetEnvironmentVariable("SB6_SAVE_DIRECTORY");
            if (string.IsNullOrWhiteSpace(sandbox) ||
                !string.Equals(System.IO.Path.GetFullPath(sandbox), global::SaveSystem.SaveDirectoryPath, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Rendered smoke refused to use a non-isolated save path");
            Debug.Log("[NormalPlaytest] save isolation confirmed path=" + global::SaveSystem.SaveDirectoryPath);
        }

        IEnumerator Host()
        {
            Application.runInBackground = true; Application.targetFrameRate = 60; QualitySettings.vSyncCount = 0;
            for (int i = 0; i < 30; i++) yield return null;
            ApplyPlaytestVideoOutput();
            global::SaveSystem.SetActiveSlot(0); global::SaveSystem.ResetSlot(0, Assets.Scripts.SaveSystem.NormalGameDifficulty.Easy);
            global::SaveSystem.Data.videoSettings.displayMode = 0; global::SaveSystem.SetVideoSettings(false, 2);
            PlayerPersistentStats.ResetToDefaultsAll();
            SceneManager.LoadScene(NetworkLobby.SceneName); yield return null;
            var manager = Mirror.NetworkManager.singleton as BombermanNetworkManager;
            if (manager == null || manager.transport is not kcp2k.KcpTransport transport)
                throw new InvalidOperationException("Native host fixture requires the lobby manager/KCP");
            if (NormalCase == "uiscale" && OnlineRoomRules.ReadMode(CommandLine) == OnlineRoomMode.BattleMode)
                ConfigureUiBattlePlayers();
            transport.Port = ushort.Parse(Argument("-normal-host-port", "18890"), System.Globalization.CultureInfo.InvariantCulture);
            manager.StartHost();
            Debug.Log("[NormalPlaytest] native host started port=" + transport.Port);
            double deadline = Time.realtimeSinceStartupAsDouble + 60;
            while (BombermanNetworkManager.RoomPlayers.Length != 2 && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            if (BombermanNetworkManager.RoomPlayers.Length != 2) throw new InvalidOperationException("Host fixture requires local owner and one real remote client");
            string scene = Argument("-normal-game-scene", "Stage_1-1");
            if (!(scene.StartsWith("Stage_", StringComparison.Ordinal) || NormalCase == "uiscale" && scene.StartsWith("BattleMode_", StringComparison.Ordinal)) || SceneUtility.GetBuildIndexByScenePath("Assets/Scenes/" + scene + ".unity") < 0)
                throw new InvalidOperationException("Host fixture requires an enabled stage scene");
            if (NormalCase == "uiscale" && OnlineRoomRules.ReadMode(CommandLine) == OnlineRoomMode.BattleMode)
                BombermanNetworkManager.ServerLaunchConfiguredBattle(scene);
            else
            typeof(BombermanNetworkManager).GetMethod("ServerStartCoop", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(manager, new object[] { scene });
            deadline = Time.realtimeSinceStartupAsDouble + 60;
            while ((SceneManager.GetActiveScene().name != scene || !BombermanNetworkManager.OnlineCoopPlayersReady || Time.timeSinceLevelLoad < 12) && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            if (SceneManager.GetActiveScene().name != scene || !BombermanNetworkManager.OnlineCoopPlayersReady)
                throw new InvalidOperationException("Native host stage did not become ready");
            if (Argument("-normal-preview-output", "") != "") StartCoroutine(CaptureOfflineFrames());
            yield return Run(); yield return new WaitForSecondsRealtime(2);
            Debug.Log("[NormalPlaytest] native host gameplay completed");
            if (NormalCase == "credits")
            {
                if (SceneManager.GetActiveScene().name != "TitleScreen" || Mirror.NetworkClient.active || Mirror.NetworkServer.active)
                    throw new InvalidOperationException("Native host ending did not return to TitleScreen and close transport");
            }
            else if (manager != null) manager.StopHost();
            yield return null;
            Debug.Log("[NormalPlaytest] native host stopped client=" + Mirror.NetworkClient.active + " server=" + Mirror.NetworkServer.active);
            Application.Quit();
        }

        IEnumerator Offline()
        {
            Application.runInBackground = true; Application.targetFrameRate = 60; QualitySettings.vSyncCount = 0;
            for (int i = 0; i < 30; i++) yield return null;
            ApplyPlaytestVideoOutput();
            global::SaveSystem.SetActiveSlot(0); global::SaveSystem.ResetSlot(0, NormalGamePlaytest ? Assets.Scripts.SaveSystem.NormalGameDifficulty.Easy : Assets.Scripts.SaveSystem.NormalGameDifficulty.Normal);
            PlayerPersistentStats.ResetToDefaultsAll(); GameSession.Instance?.SetActivePlayerIds(new[] { 1, 2 });
            string scene = Argument("-normal-game-scene", "Stage_1-1");
            if (!(scene.StartsWith("Stage_", StringComparison.Ordinal) || NormalCase == "uiscale" && scene.StartsWith("BattleMode_", StringComparison.Ordinal)) || SceneUtility.GetBuildIndexByScenePath("Assets/Scenes/" + scene + ".unity") < 0)
                throw new InvalidOperationException("Normal playtest requires an enabled stage scene");
            if (NormalGamePlaytest)
            {
                global::SaveSystem.Data.videoSettings.displayMode = 0;
                global::SaveSystem.SetVideoSettings(false, 2);
            }
            if (NormalCase == "uiscale")
            {
                if (scene.StartsWith("BattleMode_", StringComparison.Ordinal))
                { ConfigureUiBattlePlayers(); GameSession.Instance?.BeginBattleMatch(scene, false); }
                else if (OnlineRoomRules.ReadMode(CommandLine) == OnlineRoomMode.BossRush)
                    BossRushSession.StartRun(BossRushDifficulty.NORMAL, new BossRushLoadoutPreset { difficulty = BossRushDifficulty.NORMAL, life = 4, bombAmount = 3, explosionRadius = 3, speedUps = 2 });
            }
            offline = true; SceneManager.LoadScene(scene);
            if (Argument("-normal-preview-output", "") != "") StartCoroutine(CaptureOfflineFrames());
            yield return new WaitForSecondsRealtime(14);
            yield return Run();
            Debug.Log("[P0] offline completed"); Application.Quit();
        }

        internal static IEnumerator Run()
        {
            if (!Enabled || !NetSync.ShouldSimulateLocally) yield break;
            if (NormalGamePlaytest)
            {
                if (NormalCase == "easy") yield return RunNormalGamePlaytest();
                else yield return RunNormalInspection();
                yield break;
            }
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
            if (NormalGamePlaytest) { if (NormalCase == "uiscale") TickUiScaleInput(); if (NormalCase is "water" or "combat" or "pink" or "boat" or "cart" or "music") TickWaterInput(); if (NormalCase == "chat") TickChatInput(); if (NormalCase == "unlock") TickUnlockReproduction(); ObserveNormalGamePlaytest(); return; }
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

        void TickWaterInput()
        {
            if (!Mirror.NetworkClient.active) waterInputRegistered = false;
            else if (!Mirror.NetworkServer.active && !waterInputRegistered)
            {
                waterInputRegistered = true;
                Mirror.NetworkClient.RegisterHandler<NormalWaterInputStep>(step =>
                {
                    if (step.scene != SceneManager.GetActiveScene().name || step.held < 0 || step.held > 63 || !float.IsFinite(step.seconds) || step.seconds < 0 || step.seconds > 5) return;
                    waterInputs[1] = (step.scene, step.held, Time.realtimeSinceStartupAsDouble + step.seconds);
                    Debug.Log("[NormalPlaytest] water owner input held=" + step.held);
                });
            }
            if (PlayerInputManager.Instance == null) return;
            foreach (var pair in waterInputs)
            {
                int held = pair.Value.scene == SceneManager.GetActiveScene().name && Time.realtimeSinceStartupAsDouble < pair.Value.until ? pair.Value.held : 0;
                for (int index = 0; index < WaterActions.Length; index++) PlayerInputManager.Instance.SetSyntheticHeld(pair.Key, WaterActions[index], (held & (1 << index)) != 0);
            }
        }
        static int WaterMask(Vector2 direction) => direction.y > .5f ? 1 : direction.y < -.5f ? 2 : direction.x < -.5f ? 4 : 8;
        static void DriveWaterOwner(MovementController actor, int held, float seconds)
        {
            string scene = SceneManager.GetActiveScene().name;
            var input = actor.GetComponent<NetworkPlayerInput>();
            if (Mirror.NetworkServer.active && input != null && input.connectionToClient != null && input.connectionToClient != Mirror.NetworkServer.localConnection)
                input.connectionToClient.Send(new NormalWaterInputStep { scene = scene, held = held, seconds = seconds });
            else
            {
                var probe = FindAnyObjectByType<LocalPredictionSmoke>();
                probe.waterInputs[actor.PlayerId] = (scene, held, Time.realtimeSinceStartupAsDouble + seconds);
            }
        }

        bool unlockReproductionStarted, sawUnlockToast;
        readonly System.Collections.Generic.HashSet<int> observedUnlockStages = new();
        readonly System.Collections.Generic.HashSet<string> observedUnlockToasts = new();
        internal static bool UnlockToastWasObserved => FindAnyObjectByType<LocalPredictionSmoke>()?.sawUnlockToast == true;
        void OnEnable()
        {
            if (NormalCase == "unlock") UnlockProgress.OnBattleModeStageUnlocked += ObserveNativeStageUnlockEvent;
        }
        void ObserveNativeStageUnlockEvent(int stage)
        {
            if (!observedUnlockStages.Add(stage)) throw new InvalidOperationException("Unlock delta replayed the same stage notification: " + stage);
            Debug.Log("[NormalPlaytest] unlock event battleStage=" + stage + " role=" + BombermanNetworkManager.LocalRoomPlayerId);
        }
        void TickUnlockReproduction()
        {
            if (unlockReproductionStarted || !Mirror.NetworkServer.active || !BombermanNetworkManager.OnlineCoopPlayersReady || !SceneManager.GetActiveScene().name.StartsWith("Stage_", StringComparison.Ordinal)) return;
            unlockReproductionStarted = true; StartCoroutine(InspectNativeUnlocks());
        }
        static IEnumerator InspectNativeUnlocks()
        {
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            if (NetSync.IsOnline)
            {
                var manager = Mirror.NetworkManager.singleton as BombermanNetworkManager;
                bool configured = manager != null && (bool)typeof(BombermanNetworkManager).GetField("ownerFlowConfigured", flags).GetValue(manager);
                if (!configured || global::SaveSystem.Data.activeSlotIndex != 1 || global::SaveSystem.GetActiveNormalGameDifficulty() != Assets.Scripts.SaveSystem.NormalGameDifficulty.Hard)
                    throw new InvalidOperationException("Unlock reproduction requires the real configured owner-save/menu flow");
            }
            foreach (var actor in FindObjectsByType<MovementController>())
                if (actor.CompareTag("Player")) { actor.GetComponent<CharacterHealth>().ResetForRespawn(200); actor.GetComponent<CharacterHealth>().SetExternalInvulnerability(true); }
            while (Time.timeSinceLevelLoad < 12) yield return null;
            yield return new WaitForSecondsRealtime(3);
            int chosen = !UnlockProgress.IsBattleModeStage11Unlocked() ? 11 : !UnlockProgress.IsBattleModeStage12Unlocked() ? 12 :
                !UnlockProgress.IsBattleModeStage13Unlocked() ? 13 : !UnlockProgress.IsBattleModeStage14Unlocked() ? 14 : 15;
            if (!UnlockProgress.UnlockBattleModeStage(chosen)) throw new InvalidOperationException("Native stage unlock was rejected");
            Debug.Log("[NormalPlaytest] native unlock granted battleStage=" + chosen + " configured=" + NetSync.IsOnline);
            yield return new WaitForSecondsRealtime(8);
            if (UnlockProgress.UnlockBattleModeStage(chosen)) throw new InvalidOperationException("Duplicate native unlock was accepted");
            if (!Mirror.NetworkServer.active && !UnlockToastWasObserved) throw new InvalidOperationException("Native unlock toast did not render offline");
            Debug.Log("[NormalPlaytest] native duplicate unlock rejected");
            Debug.Log("[NormalPlaytest] inspection completed case=unlock");
        }
        void ObserveUnlockToast()
        {
            if (observedUnlockStages.Count == 0 || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            var presenter = FindAnyObjectByType<UnlockToastPresenter>();
            if (presenter == null) return;
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var group = (CanvasGroup)typeof(UnlockToastPresenter).GetField("canvasGroup", flags).GetValue(presenter);
            var title = (TMPro.TMP_Text)typeof(UnlockToastPresenter).GetField("titleText", flags).GetValue(presenter);
            if (group == null || group.alpha < .9f || !group.gameObject.activeInHierarchy || title == null || string.IsNullOrEmpty(title.text)) return;
            title.ForceMeshUpdate();
            if (title.textInfo.characterCount == 0) return;
            var subtitle = (TMPro.TMP_Text)typeof(UnlockToastPresenter).GetField("subtitleText", flags).GetValue(presenter);
            string text = title.text + "|" + (subtitle != null ? subtitle.text : "");
            if (!observedUnlockToasts.Add(text)) return;
            sawUnlockToast = true; CaptureNormalFrame("toast-" + observedUnlockToasts.Count);
            Debug.Log("[NormalPlaytest] unlock toast rendered role=" + BombermanNetworkManager.LocalRoomPlayerId + " title=" + title.text + " subtitle=" + (subtitle != null ? subtitle.text.Replace("\n", " ") : ""));
        }

        void TickChatInput()
        {
            if (chatPhase >= 99 || Mirror.NetworkServer.active || !Mirror.NetworkClient.active || !SceneManager.GetActiveScene().name.StartsWith("Stage_", StringComparison.Ordinal) || Time.timeSinceLevelLoad < 12 || BombermanNetworkManager.LocalRoomPlayerId <= 0 || Time.realtimeSinceStartupAsDouble < nextChatStep) return;
            var chat = FindAnyObjectByType<OnlineChatOverlay>();
            if (chat == null) return;
            if (chatKeyboard == null)
            {
                chatKeyboard = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
                UnityEngine.InputSystem.InputSystem.EnableDevice(chatKeyboard);
                nextChatStep = Time.realtimeSinceStartupAsDouble + BombermanNetworkManager.LocalRoomPlayerId;
                Debug.Log("[NormalPlaytest] chat InputSystem reproduction ready role=" + BombermanNetworkManager.LocalRoomPlayerId);
                return;
            }
            chatKeyboard.MakeCurrent();
            if (chatPhase == 0)
            {
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(chatKeyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.T));
                UnityEngine.InputSystem.InputSystem.QueueTextEvent(chatKeyboard, 't');
                chatPhase = 1; nextChatStep = Time.realtimeSinceStartupAsDouble + .25;
            }
            else if (chatPhase == 1)
            {
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(chatKeyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState());
                foreach (char character in chatMessages[chatMessageIndex]) UnityEngine.InputSystem.InputSystem.QueueTextEvent(chatKeyboard, character);
                chatPhase = 2; nextChatStep = Time.realtimeSinceStartupAsDouble + .35;
            }
            else if (chatPhase == 2)
            {
                TMPro.TMP_Text entry = null;
                foreach (var label in chat.GetComponentsInChildren<TMPro.TMP_Text>(true)) if (label.name == "Entry") { entry = label; break; }
                if (entry == null || !entry.gameObject.activeInHierarchy || entry.text.Length <= 2)
                { chatPhase = 99; throw new InvalidOperationException("Native chat did not retain the InputSystem draft"); }
                entry.ForceMeshUpdate();
                int visible = 0; for (int index = 0; index < entry.textInfo.characterCount; index++) if (entry.textInfo.characterInfo[index].isVisible) visible++;
                if (visible <= 1 || entry.GetPreferredValues(entry.text, float.PositiveInfinity, float.PositiveInfinity).x > entry.rectTransform.rect.width + .1f)
                { chatPhase = 99; throw new InvalidOperationException("Native chat draft glyphs/cursor do not fit"); }
                CaptureNormalFrame("draft-" + chatMessageIndex);
                Debug.Log("[NormalPlaytest] chat draft rendered index=" + chatMessageIndex + " visibleGlyphs=" + visible + " preferredHeight=" + entry.preferredHeight + " rectHeight=" + entry.rectTransform.rect.height);
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(chatKeyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.Enter));
                chatPhase = 3; nextChatStep = Time.realtimeSinceStartupAsDouble + .25;
            }
            else if (chatPhase == 3)
            {
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(chatKeyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState());
                chatPhase = 4; nextChatStep = Time.realtimeSinceStartupAsDouble + .75;
            }
            else
            {
                var lines = (System.Collections.IEnumerable)typeof(OnlineChatOverlay).GetField("roomLines", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(chat);
                bool echoed = false;
                foreach (var line in lines)
                {
                    var text = (string)line.GetType().GetField("text", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance).GetValue(line);
                    if (text == chatMessages[chatMessageIndex]) echoed = true;
                }
                if (!echoed) { chatPhase = 99; throw new InvalidOperationException("Native chat did not receive its server echo index=" + chatMessageIndex); }
                CaptureNormalFrame("history-" + chatMessageIndex);
                Debug.Log("[NormalPlaytest] chat server echo confirmed role=" + BombermanNetworkManager.LocalRoomPlayerId + " index=" + chatMessageIndex);
                chatMessageIndex++; chatPhase = chatMessageIndex == chatMessages.Length ? 99 : 0;
                if (chatPhase == 99) Debug.Log("[NormalPlaytest] chat InputSystem completed role=" + BombermanNetworkManager.LocalRoomPlayerId);
                nextChatStep = Time.realtimeSinceStartupAsDouble + .5;
            }
        }
        static void CaptureNormalFrame(string name)
        {
            string output = Argument("-load-test-mount-captures", Argument("-normal-preview-output", ""));
            if (string.IsNullOrEmpty(output)) return;
            System.IO.Directory.CreateDirectory(output);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(output, NormalCase + "-" + name + ".png"));
        }
        void OnDestroy()
        {
            nativeAudioClips.Clear();
            if (chatKeyboard != null && chatKeyboard.added) UnityEngine.InputSystem.InputSystem.RemoveDevice(chatKeyboard);
        }

        void LateUpdate()
        {
            if (!offline || NormalGamePlaytest) return;
            foreach (var actor in actors)
                if (actor != null && actor.TryGetComponent<CharacterHealth>(out var health))
                { health.life = 200; health.SetExternalInvulnerability(true); }
        }

        IEnumerator PreviewLobby()
        {
            string output = Argument("-normal-preview-output", "");
            if (string.IsNullOrWhiteSpace(output)) throw new InvalidOperationException("UI preview requires an output directory");
            System.IO.Directory.CreateDirectory(output);
            Application.runInBackground = true; Application.targetFrameRate = 60; QualitySettings.vSyncCount = 0;
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            foreach (GameLanguage language in Enum.GetValues(typeof(GameLanguage)))
            {
                global::SaveSystem.Data.language = (int)language;
                global::SaveSystem.Data.videoSettings.displayMode = 0;
                global::SaveSystem.SetVideoSettings(false, 2);
                SceneManager.LoadScene(NetworkLobby.SceneName);
                yield return new WaitForSecondsRealtime(2);
                var menu = FindAnyObjectByType<OnlineLobbyMenu>(); var browser = FindAnyObjectByType<OnlineRoomBrowser>();
                if (menu == null || browser == null) throw new InvalidOperationException("UI preview missing native lobby");
                browser.StopAllCoroutines(); browser.enabled = false; browser.Rooms.Clear();
                browser.Rooms.Add(new OnlineListedRoom { id = "preview", creator = "PLAYTEST123456789", name = "SALA DE TESTE COM NOME LONGO", mode = "NormalGame", players = 2, maxPlayers = 6, available = true });
                typeof(OnlineLobbyMenu).GetMethod("CloseName", flags).Invoke(menu, null);
                typeof(OnlineLobbyMenu).GetField("creationOpen", flags).SetValue(menu, false);
                Screen.SetResolution(512, 448, FullScreenMode.Windowed);
                foreach (var label in FindObjectsByType<TMPro.TMP_Text>(FindObjectsInactive.Include)) LocalizedTmpFontFallback.Apply(label);
                yield return new WaitForSecondsRealtime(.5f);
                Canvas.ForceUpdateCanvases(); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(output, language + "-browser.png"));
                yield return new WaitForSecondsRealtime(.5f);
                typeof(OnlineLobbyMenu).GetField("creationOpen", flags).SetValue(menu, true);
                yield return new WaitForSecondsRealtime(.5f);
                Canvas.ForceUpdateCanvases(); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(output, language + "-creation.png"));
                yield return new WaitForSecondsRealtime(.5f);
                typeof(OnlineLobbyMenu).GetMethod("OpenRoomName", flags).Invoke(menu, null);
                typeof(OnlineLobbyMenu).GetMethod("BeginNameEdit", flags).Invoke(menu, new object[] { "SALA DE TESTE COM NOME LONGO" });
                yield return new WaitForSecondsRealtime(.5f);
                Canvas.ForceUpdateCanvases(); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(output, language + "-name.png"));
                yield return new WaitForSecondsRealtime(.5f);
            }
            Debug.Log("[NormalPlaytest] UI preview completed languages=4"); Application.Quit();
        }

        IEnumerator CaptureOfflineFrames()
        {
            string output = Argument("-normal-preview-output", ""); System.IO.Directory.CreateDirectory(output);
            int frame = 0; string prefix = LocalHostPlaytest ? "host-" : "offline-";
            while (frame < 150)
            {
                yield return new WaitForSecondsRealtime(1);
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(output, prefix + (++frame).ToString("D3") + ".png"));
            }
        }

        internal static IEnumerator RunNormalInspection()
        {
            if (NormalCase == "uiscale") { yield return InspectNativeUiScale(); yield break; }
            if (NormalCase == "submerge") { yield return InspectNativeSubmersion(); yield break; }
            if (NormalCase == "waterbomb") { yield return InspectNativeWaterBomb(); yield break; }
            if (NormalCase == "items") { yield return InspectNativeCampaignItems(); yield break; }
            if (NormalCase == "blink") { yield return InspectNativeBlinkOwnership(); yield break; }
            if (NormalCase == "geyser") { yield return InspectNativeGeyserBarrier(); yield break; }
            if (NormalCase == "bossitems") { yield return InspectNativeBossItems(); yield break; }
            if (NormalCase == "cart") { yield return InspectNativeCartLanding(); yield break; }
            if (NormalCase == "force") { yield return InspectNativeHurricane(); yield break; }
            if (NormalCase == "rooms") { yield return InspectNativeRoomTransitions(); yield break; }
            if (NormalCase == "pink") { yield return InspectNativePink(); yield break; }
            if (NormalCase == "boat") { yield return InspectNativeBoatSeats(); yield break; }
            if (NormalCase == "unlock") { yield return InspectNativeUnlocks(); yield break; }
            if (NormalCase == "chip")
            {
                string chipStage = SceneManager.GetActiveScene().name;
                if (StageUnlockProgress.IsCleared(chipStage)) throw new InvalidOperationException("Chip reproduction requires a first clear");
                foreach (var actor in FindObjectsByType<MovementController>())
                    if (actor.CompareTag("Player"))
                    {
                        actor.GetComponent<CharacterHealth>().ResetForRespawn(200);
                        actor.GetComponent<CharacterHealth>().SetExternalInvulnerability(true);
                        actor.GetComponent<PlayerMountCompanion>().RestoreMounted(actor.PlayerId == 1 ? MountedType.Blue : MountedType.Pink);
                    }
                yield return new WaitForSecondsRealtime(.8f);
                yield return LocalEnvironmentSmoke.Run();
                EndStagePortal chipPortal = null;
                foreach (var candidate in FindObjectsByType<EndStagePortal>()) if (candidate.name == "World3InvisibleEndStagePortal") { chipPortal = candidate; break; }
                if (chipPortal == null) throw new InvalidOperationException("Native bubble release did not create the chip collection portal");
                var collector = FindAnyObjectByType<PlayerIdentity>().GetComponent<MovementController>();
                Vector2 target = chipPortal.transform.position;
                collector.Rigidbody.position = target; collector.transform.position = target;
                NetworkPlayerInput.SyncAuthorityMotionTransform(collector);
                if (Mirror.NetworkServer.active) collector.GetComponent<NetworkPlayerInput>().ServerReconcileMovementNow();
                Physics2D.SyncTransforms();
                Debug.Log("[NormalPlaytest] native chip collection entered stage=" + chipStage);
                double deadline = Time.realtimeSinceStartupAsDouble + 60;
                string hall = global::StageAssets.World3HallPortalSelectionController.SceneName;
                while ((SceneManager.GetActiveScene().name != hall || Time.timeSinceLevelLoad < 12 || !BombermanNetworkManager.OnlineCoopPlayersReady) && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                if (SceneManager.GetActiveScene().name != hall || !BombermanNetworkManager.OnlineCoopPlayersReady) throw new InvalidOperationException("Native chip collection did not return to Hall");
                if (!StageUnlockProgress.IsCleared(chipStage)) throw new InvalidOperationException("Native chip collection did not commit stage completion");
                foreach (var actor in FindObjectsByType<MovementController>())
                    if (actor.CompareTag("Player") && actor.GetComponent<PlayerMountCompanion>().GetMountedLouieType() != (actor.PlayerId == 1 ? MountedType.Blue : MountedType.Pink))
                        throw new InvalidOperationException("Native stage collection/commit lost the mounted Louie during Hall return");
                Debug.Log("[NormalPlaytest] native chip collection and mounted Hall return completed");
                Debug.Log("[NormalPlaytest] inspection completed case=chip"); yield break;
            }
            if (NormalCase == "environment")
            {
                string scene = SceneManager.GetActiveScene().name;
                if (scene == "Stage_3-1") yield return InspectNativeRoomGate();
                else
                {
                    if (scene == "Stage_3-5") yield return InspectNativeIce();
                    yield return LocalEnvironmentSmoke.Run();
                }
                yield return new WaitForSecondsRealtime(2);
                Debug.Log("[NormalPlaytest] inspection completed case=environment"); yield break;
            }
            if (NormalCase == "music") { yield return InspectNativeMusic(); yield break; }
            if (NormalCase == "chat")
            {
                foreach (var actor in FindObjectsByType<MovementController>()) if (actor.CompareTag("Player")) actor.GetComponent<CharacterHealth>().ResetForRespawn(200);
                yield return new WaitForSecondsRealtime(35);
                Debug.Log("[NormalPlaytest] inspection completed case=chat"); yield break;
            }
            if (NormalCase == "combat")
            {
                var combatActors = new System.Collections.Generic.List<MovementController>();
                foreach (var actor in FindObjectsByType<MovementController>())
                    if (actor.CompareTag("Player"))
                    { actor.GetComponent<CharacterHealth>().ResetForRespawn(200); actor.SetSuppressInactivityAnimation(true); combatActors.Add(actor); }
                if (combatActors.Count != 2) throw new InvalidOperationException("Combat cost fixture requires two native owners");
                int observedCombatBombs = 0;
                Debug.Log("[NormalPlaytest] combat cost window started");
                for (int step = 0; step < 10; step++)
                {
                    foreach (var actor in combatActors)
                    {
                        int move = step % 2 == 1 ? 1 : actor.PlayerId == 1 ? 8 : 4;
                        DriveWaterOwner(actor, move | 16, .12f);
                    }
                    yield return new WaitForSecondsRealtime(.15f);
                    int liveBombs = FindObjectsByType<Bomb>().Length;
                    observedCombatBombs += liveBombs;
                    Debug.Log("[NormalPlaytest] combat live bombs step=" + step + " count=" + liveBombs);
                    foreach (var actor in combatActors)
                    { int move = step % 2 == 1 ? 1 : actor.PlayerId == 1 ? 8 : 4; DriveWaterOwner(actor, move, 3.8f); }
                    yield return new WaitForSecondsRealtime(3.85f);
                }
                foreach (var actor in combatActors) DriveWaterOwner(actor, 0, 0);
                if (observedCombatBombs == 0) throw new InvalidOperationException("Combat cost cohort never produced a native bomb");
                Debug.Log("[NormalPlaytest] inspection completed case=combat"); yield break;
            }
            if (NormalCase == "idle")
            {
                foreach (var actor in FindObjectsByType<MovementController>())
                    if (actor.CompareTag("Player")) actor.GetComponent<CharacterHealth>().ResetForRespawn(200);
                Debug.Log("[NormalPlaytest] idle cost window started");
                yield return new WaitForSecondsRealtime(30);
                Debug.Log("[NormalPlaytest] inspection completed case=idle"); yield break;
            }
            if (NormalCase == "blocks")
            {
                var blocksResolver = FindAnyObjectByType<IndestructibleTileResolver>();
                var blocksGame = FindAnyObjectByType<GameManager>();
                var blocksSource = FindAnyObjectByType<PlayerIdentity>()?.GetComponent<BombController>();
                var blocksCamera = Camera.main;
                if (blocksResolver == null || blocksGame == null || blocksGame.indestructibleTilemap == null || blocksSource == null || blocksCamera == null)
                    throw new InvalidOperationException("Block inspection requires native stage geometry/source/camera");
                var blocksMap = blocksGame.indestructibleTilemap;
                var blocksFollow = blocksCamera.GetComponent<CameraFollowClamp2D>();
                bool followEnabled = blocksFollow != null && blocksFollow.enabled;
                Vector3 originalCamera = blocksCamera.transform.position;
                foreach (var actor in FindObjectsByType<MovementController>())
                    if (actor.CompareTag("Player")) actor.GetComponent<CharacterHealth>().ResetForRespawn(200);
                try
                {
                    if (blocksFollow != null) blocksFollow.enabled = false;
                    for (int attempt = 0; attempt < 6; attempt++)
                    {
                        PushIndestructibleTileHandler selectedHandler = null;
                        Vector3Int selectedCell = default, selectedDirection = default;
                        foreach (var cell in blocksMap.cellBounds.allPositionsWithin)
                        {
                            var tile = blocksMap.GetTile(cell);
                            if (tile == null || !blocksResolver.TryGetHandler(tile, out var candidate) || candidate is not PushIndestructibleTileHandler pushHandler) continue;
                            foreach (var direction in new[] { Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down })
                            {
                                object[] query = { cell + direction, false };
                                if (!(bool)typeof(PushIndestructibleTileHandler).GetMethod("CanMoveToCell", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(pushHandler, query)) continue;
                                selectedHandler = pushHandler; selectedCell = cell; selectedDirection = direction; break;
                            }
                            if (selectedHandler != null) break;
                        }
                        if (selectedHandler == null) throw new InvalidOperationException("No passable native block target for attempt=" + attempt);
                        Vector3 center = blocksMap.GetCellCenterWorld(selectedCell);
                        blocksCamera.transform.position = new Vector3(center.x, center.y, originalCamera.z);
                        yield return new WaitForSecondsRealtime(.25f);
                        var selectedTile = blocksMap.GetTile(selectedCell);
                        selectedHandler.HandleExplosionHit(blocksSource, (Vector2)(center - (Vector3)selectedDirection), selectedCell, selectedTile);
                        if (blocksMap.GetTile(selectedCell) != null) throw new InvalidOperationException("Native block did not start moving");
                        Debug.Log("[NormalPlaytest] native block moving attempt=" + attempt + " cell=" + selectedCell + " direction=" + selectedDirection);
                        yield return new WaitForSecondsRealtime(.8f);
                    }
                }
                finally
                {
                    blocksCamera.transform.position = originalCamera;
                    if (blocksFollow != null) blocksFollow.enabled = followEnabled;
                }
                Debug.Log("[NormalPlaytest] inspection completed case=blocks"); yield break;
            }
            if (NormalCase == "boiler")
            {
                var resolver = FindAnyObjectByType<GroundTileResolver>();
                var source = FindAnyObjectByType<PlayerIdentity>()?.GetComponent<BombController>();
                if (resolver == null || source == null || source.groundTiles == null)
                    throw new InvalidOperationException("Boiler inspection missing native ground/owner");
                Vector3Int? entry = null;
                foreach (var cell in source.groundTiles.cellBounds.allPositionsWithin)
                    if (resolver.TryGetHandler(source.groundTiles.GetTile(cell), out var handler) && handler is BoilerTriggerGroundTileHandler)
                    { entry = cell; break; }
                if (!entry.HasValue) throw new InvalidOperationException("Boiler inspection missing authored trigger tile");
                Vector3 boilerFocus = source.groundTiles.GetCellCenterWorld(entry.Value);
                int boilerPlayerIndex = 0;
                foreach (var actor in FindObjectsByType<MovementController>())
                    if (actor.CompareTag("Player"))
                    {
                        actor.GetComponent<CharacterHealth>().ResetForRespawn(200);
                        Vector3 target = boilerFocus + Vector3.down * 2 + Vector3.right * boilerPlayerIndex++;
                        actor.Rigidbody.position = target; actor.transform.position = target;
                        NetworkPlayerInput.SyncAuthorityMotionTransform(actor);
                        if (Mirror.NetworkServer.active && actor.TryGetComponent<NetworkPlayerInput>(out var input)) input.ServerReconcileMovementNow();
                    }
                foreach (var camera in FindObjectsByType<CameraFollowClamp2D>()) camera.ForceSnapNow(refreshPlayersNow: true);
                while (StageIntroTransition.Instance != null && StageIntroTransition.Instance.IntroRunning) yield return null;
                if (!source.TryPlaceBombAtIgnoringInputLock(boilerFocus, out var bomb, consumeBomb: false, trackOwnerBombTraversal: false) || bomb == null)
                    throw new InvalidOperationException("Native boiler bomb placement rejected");
                Debug.Log("[NormalPlaytest] boiler native bomb placed cell=" + entry.Value);
                yield return new WaitForSecondsRealtime(18);
                var trace = FindAnyObjectByType<LocalPredictionSmoke>()?.boilerTrace;
                if (trace == null || !trace.FlamesComplete || trace.DoorCount == 0)
                    throw new InvalidOperationException("Boiler inspection did not cover the authored flame path/doors");
                boilerPlayerIndex = 0;
                foreach (var actor in FindObjectsByType<MovementController>())
                    if (actor.CompareTag("Player"))
                    {
                        actor.SetSuppressInactivityAnimation(true);
                        Vector3 target = trace.FinalPosition + Vector3.down * 2 + Vector3.left * boilerPlayerIndex++;
                        actor.Rigidbody.position = target; actor.transform.position = target;
                        NetworkPlayerInput.SyncAuthorityMotionTransform(actor);
                        if (Mirror.NetworkServer.active && actor.TryGetComponent<NetworkPlayerInput>(out var input)) input.ServerReconcileMovementNow();
                    }
                foreach (var camera in FindObjectsByType<CameraFollowClamp2D>()) camera.ForceSnapNow(refreshPlayersNow: true);
                Debug.Log("[NormalPlaytest] boiler final tiles camera focus=" + trace.FinalPosition);
                yield return new WaitForSecondsRealtime(20);
                Debug.Log("[NormalPlaytest] inspection completed case=boiler"); yield break;
            }
            if (NormalCase == "credits")
            {
                var slot = global::SaveSystem.ActiveSlot;
                if (slot == null) throw new InvalidOperationException("Credits inspection requires an isolated active slot");
                slot.difficulty = (int)Assets.Scripts.SaveSystem.NormalGameDifficulty.Normal;
                for (int world = 1; world <= 3; world++)
                    for (int stage = 1; stage <= (world == 3 ? 8 : 7); stage++)
                    {
                        string name = "Stage_" + world + "-" + stage;
                        if (!slot.unlockedStages.Contains(name)) slot.unlockedStages.Add(name);
                        if (name != "Stage_3-8" && !slot.clearedStages.Contains(name)) slot.clearedStages.Add(name);
                    }
                yield return LocalProjectileSmoke.EnsureEncounter();
                var boss = FindAnyObjectByType<FreezerVenusBoss>();
                if (boss == null || !boss.CombatActive) throw new InvalidOperationException("Credits inspection missing final native boss combat");
                foreach (var actor in FindObjectsByType<MovementController>())
                    if (actor.CompareTag("Player")) actor.GetComponent<CharacterHealth>().ResetForRespawn(200);
                boss.Kill(); Debug.Log("[NormalPlaytest] final native boss defeated");
                double deadline = Time.realtimeSinceStartupAsDouble + 60;
                while ((StageIntroTransition.Instance == null || !StageIntroTransition.Instance.EndingRunning) && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                if (StageIntroTransition.Instance == null || !StageIntroTransition.Instance.EndingRunning)
                    throw new InvalidOperationException("Final campaign completion did not start native credits");
                Debug.Log("[NormalPlaytest] credits started from native campaign completion");
                yield return new WaitForSecondsRealtime(12);
                if (!NetSync.IsOnline)
                {
                    deadline = Time.realtimeSinceStartupAsDouble + 10;
                    while (SceneManager.GetActiveScene().name != "TitleScreen" && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                    if (SceneManager.GetActiveScene().name != "TitleScreen") throw new InvalidOperationException("Native credits did not return offline to title");
                    Debug.Log("[NormalPlaytest] credits returned to title offline");
                }
                Debug.Log("[NormalPlaytest] inspection completed case=credits"); yield break;
            }
            if (NormalCase == "portal")
            {
                if (SceneManager.GetActiveScene().name != global::StageAssets.World3HallPortalSelectionController.SceneName)
                    throw new InvalidOperationException("Portal inspection requires the native World3 Hall");
                var portalSelection = FindAnyObjectByType<global::StageAssets.World3HallPortalSelectionController>();
                global::StageAssets.World3HallPortal entryPortal = null;
                foreach (var candidate in FindObjectsByType<global::StageAssets.World3HallPortal>(FindObjectsInactive.Include))
                    if (candidate.DestinationScene == "Stage_3-1") { entryPortal = candidate; break; }
                if (portalSelection == null || entryPortal == null) throw new InvalidOperationException("Portal inspection missing native selection/entry");
                foreach (var portalActor in FindObjectsByType<MovementController>())
                    if (portalActor.CompareTag("Player")) portalActor.GetComponent<CharacterHealth>().ResetForRespawn(200);
                while (StageIntroTransition.Instance != null && StageIntroTransition.Instance.IntroRunning) yield return null;
                StageUnlockProgress.Unlock("Stage_3-1");
                entryPortal.SetAvailable(false); yield return entryPortal.Reveal(null);
                const System.Reflection.BindingFlags portalFlags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                bool selected = (bool)typeof(global::StageAssets.World3HallPortalSelectionController).GetMethod("TrySelectStage", portalFlags).Invoke(portalSelection, new object[] { "Stage_3-1" });
                if (!selected) throw new InvalidOperationException("Native Hall could not select the revealed portal");
                typeof(global::StageAssets.World3HallPortalSelectionController).GetMethod("ConfirmSelection", portalFlags).Invoke(portalSelection, null);
                Debug.Log("[NormalPlaytest] native portal entry requested");
                double portalDeadline = Time.realtimeSinceStartupAsDouble + 60;
                while ((SceneManager.GetActiveScene().name != "Stage_3-1" || Mirror.NetworkServer.isLoadingScene || Time.timeSinceLevelLoad < 12 || !BombermanNetworkManager.OnlineCoopPlayersReady) && Time.realtimeSinceStartupAsDouble < portalDeadline) yield return null;
                if (SceneManager.GetActiveScene().name != "Stage_3-1" || !BombermanNetworkManager.OnlineCoopPlayersReady)
                    throw new InvalidOperationException("Native portal did not reach its destination");
                var portalProbe = FindAnyObjectByType<LocalPredictionSmoke>();
                if (portalProbe == null || !portalProbe.portalBlinkSeen.Contains(1) || !portalProbe.portalBlinkSeen.Contains(2) || !portalProbe.portalZeroSeen.Contains(1) || !portalProbe.portalZeroSeen.Contains(2))
                    throw new InvalidOperationException("Native portal did not blink both player bodies before absorption");
                foreach (var portalActor in FindObjectsByType<MovementController>())
                    if (portalActor.CompareTag("Player") && (portalActor.isDead || portalActor.InputLocked || portalActor.ExternalMovementOverride))
                        throw new InvalidOperationException("Portal destination retained an invalid player state");
                Debug.Log("[NormalPlaytest] native portal destination restored players");
                Debug.Log("[NormalPlaytest] inspection completed case=portal"); yield break;
            }
            if (NormalCase == "hall")
            {
                // Re-enter with persisted mounts, as the player does after a stage;
                // mounting after the intro would miss entry/handoff visibility bugs.
                PlayerPersistentStats.RollbackStage();
                foreach (var actor in FindObjectsByType<MovementController>())
                    if (actor.CompareTag("Player")) PlayerPersistentStats.Get(actor.PlayerId).MountedLouie = actor.PlayerId == 1 ? MountedType.Blue : MountedType.Pink;
                StageUnlockProgress.MarkCleared("Stage_3-1");
                global::StageAssets.World3HallChipAssemblyController.PrepareReturn("Stage_3-1", true);
                Debug.Log("[NormalPlaytest] hall native chip return prepared mounted=2");
                string hallScene = global::StageAssets.World3HallPortalSelectionController.SceneName;
                if (Mirror.NetworkServer.active) Mirror.NetworkManager.singleton.ServerChangeScene(hallScene);
                else SceneManager.LoadScene(hallScene);
                yield return null;
                double hallDeadline = Time.realtimeSinceStartupAsDouble + 60;
                while ((SceneManager.GetActiveScene().name != hallScene || Mirror.NetworkServer.isLoadingScene ||
                    Time.timeSinceLevelLoad < 12 || !BombermanNetworkManager.OnlineCoopPlayersReady) && Time.realtimeSinceStartupAsDouble < hallDeadline) yield return null;
                if (SceneManager.GetActiveScene().name != hallScene || !BombermanNetworkManager.OnlineCoopPlayersReady)
                    throw new InvalidOperationException("Mounted Hall entry did not become ready");
                foreach (var actor in FindObjectsByType<MovementController>())
                    if (actor.CompareTag("Player"))
                    {
                        actor.GetComponent<CharacterHealth>().ResetForRespawn(200);
                        if (actor.GetComponent<PlayerMountCompanion>().GetMountedLouieType() != (actor.PlayerId == 1 ? MountedType.Blue : MountedType.Pink))
                            throw new InvalidOperationException("Native Hall entry lost the persisted Blue/Pink Louie");
                    }
                Debug.Log("[NormalPlaytest] hall persisted mounts entered native intro");
                if (FindAnyObjectByType<global::StageAssets.World3HallChipAssemblyController>() == null)
                    throw new InvalidOperationException("Hall inspection missing native chip sequence");
                var hallSelection = FindAnyObjectByType<global::StageAssets.World3HallPortalSelectionController>();
                double deliveryDeadline = Time.realtimeSinceStartupAsDouble + 30;
                while (hallSelection != null && hallSelection.PresentationBlocked && Time.realtimeSinceStartupAsDouble < deliveryDeadline) yield return null;
                if (hallSelection == null || hallSelection.PresentationBlocked)
                    throw new InvalidOperationException("Native Hall chip delivery did not release selection");
                Debug.Log("[NormalPlaytest] hall native chip delivery completed mounted=2");
                Debug.Log("[NormalPlaytest] inspection completed case=hall");
                yield break;
            }
            if (NormalCase == "water")
            {
                const System.Reflection.BindingFlags waterFields = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                while (StageIntroTransition.Instance != null && StageIntroTransition.Instance.IntroRunning) yield return null;
                var waterGame = FindAnyObjectByType<GameManager>();
                if (waterGame == null || waterGame.groundTilemap == null) throw new InvalidOperationException("Water inspection missing native ground");
                var waterActors = new System.Collections.Generic.List<MovementController>();
                foreach (var actor in FindObjectsByType<MovementController>()) if (actor.CompareTag("Player")) waterActors.Add(actor);
                waterActors.Sort((left, right) => left.PlayerId.CompareTo(right.PlayerId));
                if (waterActors.Count != 2) throw new InvalidOperationException("Water inspection requires two native owners");
                foreach (var actor in waterActors)
                for (int cohort = 0; cohort < 2; cohort++)
                {
                    // Keep all four approaches in the authored Room3 corridor.
                    // A prior room activation can disable other current colliders.
                    LocalEnvironmentSmoke.PlacePlayers(global::StageAssets.World3RoomProgressionController.FindRoomBounds("Room 3"));
                    yield return new WaitForFixedUpdate();
                    WaterCycleCurrent selectedCurrent = null;
                    Vector2 selectedDirection = default, selectedApproach = default, entryPosition = default;
                    int bestClear = -1;
                    foreach (var current in FindObjectsByType<WaterCycleCurrent>())
                    {
                        var collider = current.GetComponent<BoxCollider2D>();
                        if (!current.isActiveAndEnabled || !collider.enabled) continue;
                        var directionValue = (WaterCycleCurrent.CurrentDirection)typeof(WaterCycleCurrent).GetField("direction", waterFields).GetValue(current);
                        Vector2 direction = directionValue switch { WaterCycleCurrent.CurrentDirection.Up => Vector2.up, WaterCycleCurrent.CurrentDirection.Down => Vector2.down, WaterCycleCurrent.CurrentDirection.Left => Vector2.left, _ => Vector2.right };
                        Vector2 center = collider.bounds.center;
                        var authoredRoom = global::StageAssets.World3RoomProgressionController.FindRoomBounds("Room 3");
                        if (authoredRoom == null || !authoredRoom.OverlapPoint(center)) continue;
                        float extent = Mathf.Abs(direction.x) * collider.bounds.extents.x + Mathf.Abs(direction.y) * collider.bounds.extents.y;
                        foreach (int side in new[] { -1, 1 })
                        {
                            if (side != (cohort == 0 ? 1 : -1)) continue;
                            Vector2 entry = center + direction * side * (extent + actor.tileSize * .75f);
                            var cell = waterGame.groundTilemap.WorldToCell(entry);
                            if (!waterGame.groundTilemap.HasTile(cell) || waterGame.indestructibleTilemap != null && waterGame.indestructibleTilemap.HasTile(waterGame.indestructibleTilemap.WorldToCell(entry))) continue;
                            Vector2 contact = center + direction * side * (extent + actor.tileSize * .2f);
                            int clear = 0;
                            for (; clear < 6; clear++)
                            {
                                Vector2 snappedContact = waterGame.groundTilemap.GetCellCenterWorld(waterGame.groundTilemap.WorldToCell(contact));
                                Vector2 probe = snappedContact + direction * actor.tileSize * clear;
                                if (!waterGame.groundTilemap.HasTile(waterGame.groundTilemap.WorldToCell(probe)) ||
                                    waterGame.indestructibleTilemap != null && waterGame.indestructibleTilemap.HasTile(waterGame.indestructibleTilemap.WorldToCell(probe))) break;
                            }
                            Debug.Log("[NormalPlaytest] water candidate=" + current.name + " center=" + center + " side=" + side + " clear=" + clear);
                            if (clear > bestClear && clear > 0) { bestClear = clear; selectedCurrent = current; selectedDirection = direction; selectedApproach = -side * direction; entryPosition = entry; }
                        }
                    }
                    if (selectedCurrent == null) throw new InvalidOperationException("Water inspection has no walkable current approach");
                    var pushes = (System.Collections.IDictionary)typeof(WaterCycleCurrent).GetField("activePushes", waterFields).GetValue(selectedCurrent);
                    var currentRoom = global::StageAssets.World3RoomProgressionController.FindRoomBoundsContaining(entryPosition);
                    if (currentRoom == null) throw new InvalidOperationException("Current approach has no authored room");
                    // Terrain preparation uses the native destruction owner, then
                    // the actor must walk into the unchanged current naturally.
                    // A bomb cannot be planted inside the authored cover itself.
                    Vector2 parking = entryPosition + new Vector2(-selectedDirection.y, selectedDirection.x) * 3;
                    LocalEnvironmentSmoke.PlacePlayers(currentRoom, parking);
                    var waterSource = actor.GetComponent<BombController>();
                    waterSource.ClearDestructibleForEffect(entryPosition, spawnDestructiblePrefab: false, spawnHiddenObject: false);
                    Vector2 currentCenter = selectedCurrent.GetComponent<BoxCollider2D>().bounds.center;
                    waterSource.ClearDestructibleForEffect(currentCenter, spawnDestructiblePrefab: false, spawnHiddenObject: false);
                    Vector2 corridorStart = waterGame.groundTilemap.GetCellCenterWorld(waterGame.groundTilemap.WorldToCell(currentCenter + selectedDirection * (cohort == 0 ? .65f : -.65f)));
                    for (int cell = 0; cell < bestClear; cell++)
                        waterSource.ClearDestructibleForEffect(corridorStart + selectedDirection * actor.tileSize * cell, spawnDestructiblePrefab: false, spawnHiddenObject: false);
                    yield return new WaitForSecondsRealtime(.5f);
                    if (waterGame.destructibleTilemap.HasTile(waterGame.destructibleTilemap.WorldToCell(currentCenter)) ||
                        waterGame.destructibleTilemap.HasTile(waterGame.destructibleTilemap.WorldToCell(entryPosition)))
                        throw new InvalidOperationException("Native terrain preparation retained current cover");

                    Vector2 original = actor.Rigidbody.position;
                    actor.GetComponent<CharacterHealth>().ResetForRespawn(200); actor.SetSuppressInactivityAnimation(true);
                    actor.Rigidbody.position = entryPosition; actor.transform.position = entryPosition;
                    NetworkPlayerInput.SyncAuthorityMotionTransform(actor);
                    if (Mirror.NetworkServer.active && actor.TryGetComponent<NetworkPlayerInput>(out var owner))
                    {
                        actor.GetComponent<PlayerNetworkTransform>().ServerRoomCut(actor.transform.position, actor.transform.rotation);
                        owner.ServerReconcileMovementNow();
                    }
                    foreach (var camera in FindObjectsByType<CameraFollowClamp2D>()) camera.ForceSnapNow(refreshPlayersNow: true);
                    yield return new WaitForSecondsRealtime(.3f);
                    DriveWaterOwner(actor, WaterMask(selectedApproach), 3);
                    double entryDeadline = Time.realtimeSinceStartupAsDouble + 4;
                    while (!pushes.Contains(actor) && Time.realtimeSinceStartupAsDouble < entryDeadline) yield return null;
                    if (!pushes.Contains(actor))
                    {
                        bool blocked = (bool)typeof(MovementController).GetMethod("IsBlockedAtPosition", waterFields).Invoke(actor,
                            new object[] { actor.Rigidbody.position + selectedApproach * actor.tileSize * .5f, selectedApproach, false });
                        var touched = (System.Collections.Generic.HashSet<MovementController>)typeof(WaterCycleCurrent).GetField("pushedPlayers", waterFields).GetValue(selectedCurrent);
                        throw new InvalidOperationException("Native walking did not enter current player=" + actor.PlayerId + " position=" + actor.Rigidbody.position +
                            " blocked=" + blocked + " touched=" + touched.Contains(actor) + " locked=" + actor.InputLocked + " external=" + actor.ExternalMovementOverride +
                            " barrier=" + WaterCycleCurrent.CaptureNetworkBarrier(actor).active + " trigger=" + selectedCurrent.GetComponent<BoxCollider2D>().bounds + " body=" + actor.GetComponent<Collider2D>().bounds);
                    }
                    Vector2 pushStart = actor.Rigidbody.position;
                    Debug.Log("[NormalPlaytest] water native walking entered player=" + actor.PlayerId + " position=" + pushStart + " clear=" + bestClear + " sense=" + (cohort == 0 ? "against" : "with"));
                    yield return new WaitForSecondsRealtime(.06f);
                    if (actor.PlayerId == 1 && cohort == 0)
                    {
                        var pause = GamePauseController.Instance;
                        var toggle = typeof(GamePauseController).GetMethod("TogglePause", waterFields);
                        if (pause == null || toggle == null) throw new InvalidOperationException("Water phase has no native pause controller");
                        toggle.Invoke(pause, null);
                        if (!GamePauseController.IsPaused) throw new InvalidOperationException("Water phase pause rejected");
                        Vector2 frozen = actor.Rigidbody.position;
                        Debug.Log("[NormalPlaytest] water active phase paused position=" + frozen);
                        yield return new WaitForSecondsRealtime(1);
                        if (Vector2.Distance(frozen, actor.Rigidbody.position) > .001f) throw new InvalidOperationException("Native water phase moved while paused");
                        toggle.Invoke(pause, null);
                        if (GamePauseController.IsPaused) throw new InvalidOperationException("Water phase resume rejected");
                    }
                    DriveWaterOwner(actor, WaterMask(-selectedDirection), 2);
                    double pushDeadline = Time.realtimeSinceStartupAsDouble + 4;
                    while (pushes.Contains(actor) && Time.realtimeSinceStartupAsDouble < pushDeadline) yield return null;
                    DriveWaterOwner(actor, 0, 0);
                    if (pushes.Contains(actor) || actor.InputLocked || actor.ExternalMovementOverride || actor.isDead) throw new InvalidOperationException("Native current did not release its owner");
                    Debug.Log("[NormalPlaytest] water countersteering completed player=" + actor.PlayerId + " position=" + actor.Rigidbody.position + " displacement=" + Vector2.Distance(pushStart, actor.Rigidbody.position));
                    yield return new WaitForSecondsRealtime(.5f);
                    actor.Rigidbody.position = original; actor.transform.position = original;
                    NetworkPlayerInput.SyncAuthorityMotionTransform(actor);
                    if (Mirror.NetworkServer.active && actor.TryGetComponent<NetworkPlayerInput>(out var restored)) restored.ServerReconcileMovementNow();
                }
                Debug.Log("[NormalPlaytest] inspection completed case=water"); yield break;
            }
            Vector3 focus = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
            EndStageGateAnimated gate = null;
            if (NormalCase == "stage23")
            {
                var boat = FindAnyObjectByType<BoatRideZone>();
                if (boat == null) throw new InvalidOperationException("Inspection missing native boat");
                focus = boat.transform.position;
                Debug.Log("[NormalPlaytest] inspection boats=" + FindObjectsByType<BoatRideZone>().Length + " springs=" + FindObjectsByType<SpringLauncher>().Length);
            }
            else if (NormalCase == "gate")
            {
                gate = FindAnyObjectByType<EndStageGateAnimated>();
                if (gate == null) throw new InvalidOperationException("Inspection missing native gate");
                focus = gate.transform.position;
            }
            else if (NormalCase != "intro") throw new InvalidOperationException("Unknown Normal inspection case: " + NormalCase);
            int index = 0;
            foreach (var actor in FindObjectsByType<MovementController>())
            {
                if (!actor.CompareTag("Player")) continue;
                actor.GetComponent<CharacterHealth>().ResetForRespawn(200);
                if (NormalCase == "intro") continue;
                Vector3 target = focus + Vector3.down * (NormalCase == "gate" ? 4 : 2) + Vector3.right * index++;
                actor.Rigidbody.position = target; actor.transform.position = target;
                NetworkPlayerInput.SyncAuthorityMotionTransform(actor);
                if (Mirror.NetworkServer.active && actor.TryGetComponent<NetworkPlayerInput>(out var input)) input.ServerReconcileMovementNow();
            }
            foreach (var camera in FindObjectsByType<CameraFollowClamp2D>()) camera.ForceSnapNow(refreshPlayersNow: true);
            yield return new WaitForSecondsRealtime(3);
            if (gate != null) gate.ForceUnlock();
            yield return new WaitForSecondsRealtime(8);
            if (NormalCase == "stage23")
            {
                var spring = FindAnyObjectByType<SpringLauncher>();
                if (spring == null) throw new InvalidOperationException("Inspection missing native spring");
                foreach (var actor in FindObjectsByType<MovementController>())
                    if (actor.CompareTag("Player"))
                    {
                        Vector3 target = spring.transform.position + Vector3.down;
                        actor.Rigidbody.position = target; actor.transform.position = target;
                        NetworkPlayerInput.SyncAuthorityMotionTransform(actor);
                    }
                foreach (var camera in FindObjectsByType<CameraFollowClamp2D>()) camera.ForceSnapNow(refreshPlayersNow: true);
                yield return new WaitForSecondsRealtime(8);
            }
            Debug.Log("[NormalPlaytest] inspection completed case=" + NormalCase);
        }

        internal static IEnumerator RunNormalGamePlaytest()
        {
            var actors = new System.Collections.Generic.List<MovementController>();
            foreach (var actor in FindObjectsByType<MovementController>())
                if (actor.CompareTag("Player") && !actor.isDead) actors.Add(actor);
            if (actors.Count != 2) throw new InvalidOperationException("Normal playtest requires two native players");
            foreach (var actor in actors)
                if (!actor.HasEasyNormalGameRespawn) throw new InvalidOperationException("Easy respawn not enabled on native player");
            Debug.Log("[NormalPlaytest] easy started players=" + actors.Count);
            for (int cycle = 0; cycle < 3; cycle++)
            {
                yield return new WaitForSecondsRealtime(3f);
                if (cycle == 2)
                {
                    var pause = GamePauseController.Instance;
                    var toggle = typeof(GamePauseController).GetMethod("TogglePause", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    if (pause == null || toggle == null) throw new InvalidOperationException("Normal playtest requires native pause");
                    toggle.Invoke(pause, null);
                    if (!GamePauseController.IsPaused) throw new InvalidOperationException("Native pause rejected while players alive");
                    yield return new WaitForSecondsRealtime(1);
                    toggle.Invoke(pause, null);
                    if (GamePauseController.IsPaused) throw new InvalidOperationException("Native pause failed to resume");
                    Debug.Log("[NormalPlaytest] alive pause resumed");
                }
                var killed = cycle < 2 ? new[] { actors[cycle] } : actors.ToArray();
                foreach (var actor in killed)
                {
                    var health = actor.GetComponent<CharacterHealth>();
                    health.ResetForRespawn(1); health.SetExternalInvulnerability(false);
                    if (cycle == 1) actor.KillByHole(); else health.TakeDamage(1, fromExplosion: true);
                    if (!actor.isDead) throw new InvalidOperationException("Native death rejected in Easy fixture");
                    Debug.Log("[NormalPlaytest] death cycle=" + cycle + " player=" + actor.PlayerId + " kind=" + (cycle == 1 ? "hole" : "explosion"));
                }
                if (cycle == 2)
                {
                    var pause = GamePauseController.Instance;
                    var toggle = typeof(GamePauseController).GetMethod("TogglePause", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    if (pause == null || toggle == null) throw new InvalidOperationException("Normal playtest requires native pause");
                    toggle.Invoke(pause, null);
                    if (GamePauseController.IsPaused) throw new InvalidOperationException("Normal Game unexpectedly allowed pause during death");
                    Debug.Log("[NormalPlaytest] death pause correctly rejected");
                }
                double deadline = Time.realtimeSinceStartupAsDouble + 8;
                bool alive;
                do
                {
                    alive = true;
                    foreach (var actor in killed) alive &= actor != null && actor.gameObject.activeInHierarchy && !actor.isDead && !actor.GetComponent<CharacterHealth>().IsDead;
                    if (!alive) yield return null;
                } while (!alive && Time.realtimeSinceStartupAsDouble < deadline);
                if (!alive) throw new InvalidOperationException("Easy respawn did not complete");
                foreach (var actor in killed)
                {
                    if (actor.Rigidbody == null || !actor.Rigidbody.simulated || !actor.GetComponent<Collider2D>().enabled)
                        throw new InvalidOperationException("Easy respawn left native physics disabled");
                    Debug.Log("[NormalPlaytest] revived cycle=" + cycle + " player=" + actor.PlayerId);
                }
                // Leave each local/remote owner time to execute its normal input
                // after respawn before the next injected death.
                yield return new WaitForSecondsRealtime(5);
            }
            yield return new WaitForSecondsRealtime(6);
            Debug.Log("[NormalPlaytest] easy completed");
        }

        sealed class BoilerTrace
        {
            const System.Reflection.BindingFlags Fields = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            readonly Assets.Scripts.Explosions.BoilerPowderTrailIgniterPrefab boiler;
            readonly UnityEngine.Tilemaps.Tilemap ground, doors;
            readonly UnityEngine.Tilemaps.TileBase opening, opened;
            readonly System.Collections.Generic.HashSet<string> flameNames = new();
            readonly System.Collections.Generic.HashSet<Vector3Int> expected = new(), seen = new();
            readonly System.Collections.Generic.List<Vector3Int> doorCells = new();
            readonly System.Collections.Generic.HashSet<int> doorFrames = new();
            readonly System.Collections.Generic.HashSet<Vector3Int> finalCells = new();
            readonly string finalCaptureOutput;
            int finalCaptures;
            double nextFinalCapture;
            bool flamesReported, doorsReported;
            public bool Current => boiler != null && boiler.gameObject.scene == SceneManager.GetActiveScene();
            public bool FlamesComplete => expected.Count > 0 && expected.IsSubsetOf(seen);
            public int DoorCount => doorCells.Count;
            public Vector3 FinalPosition { get; private set; }
            static T Read<T>(object owner, string field) => (T)owner.GetType().GetField(field, Fields).GetValue(owner);
            public BoilerTrace(Assets.Scripts.Explosions.BoilerPowderTrailIgniterPrefab source)
            {
                boiler = source; ground = Read<UnityEngine.Tilemaps.Tilemap>(source, "groundTilemap");
                var path = Read<System.Collections.Generic.List<Assets.Scripts.Explosions.BoilerPowderTrailIgniterPrefab.PowderTileEntry>>(source, "orderedTiles");
                bool validate = Read<bool>(source, "validateTileExistsOnGround");
                finalCaptureOutput = Argument("-normal-preview-output", Argument("-load-test-mount-captures", ""));
                if (ground != null && path != null && path.Count > 0)
                {
                    FinalPosition = ground.GetCellCenterWorld(path[path.Count - 1].Cell);
                    for (int index = Mathf.Max(0, path.Count - 3); index < path.Count; index++) finalCells.Add(path[index].Cell);
                }
                if (ground != null && path != null)
                    foreach (var tile in path) if (!validate || ground.HasTile(tile.Cell)) expected.Add(tile.Cell);
                var prefab = Read<PowderIgniterFlame>(source, "powderIgniterPrefab");
                if (prefab != null)
                    foreach (var animation in new[] { prefab.horizontal, prefab.vertical, prefab.curveDownLeft, prefab.curveDownRight, prefab.curveUpLeft, prefab.curveUpRight })
                    {
                        if (animation == null) continue;
                        if (animation.idleSprite != null) flameNames.Add(animation.idleSprite.name);
                        if (animation.animationSprite != null) foreach (var sprite in animation.animationSprite) if (sprite != null) flameNames.Add(sprite.name);
                    }
                var handler = source.GetComponent<BoilerTriggerGroundTileHandler>();
                if (handler == null) return;
                var closed = Read<UnityEngine.Tilemaps.TileBase>(handler, "closedDoorTile");
                opening = Read<AnimatedTile>(handler, "openDoorAnimationTile");
                opened = Read<UnityEngine.Tilemaps.TileBase>(handler, "openedDoorTile");
                doors = Read<UnityEngine.Tilemaps.Tilemap>(handler, "doorTilemapOverride");
                if (doors == null)
                    foreach (var candidate in FindObjectsByType<UnityEngine.Tilemaps.Tilemap>())
                        foreach (var cell in candidate.cellBounds.allPositionsWithin)
                            if (closed != null && candidate.GetTile(cell) == closed) { doors = candidate; break; }
                if (doors != null && closed != null)
                    foreach (var cell in doors.cellBounds.allPositionsWithin) if (doors.GetTile(cell) == closed) doorCells.Add(cell);
                Debug.Log("[NormalPlaytest] boiler trace expected=" + expected.Count + " doors=" + doorCells.Count);
            }
            void CaptureFinalSprite(SpriteRenderer sprite, Vector3Int cell)
            {
                if (!finalCells.Contains(cell) || finalCaptures >= 12 || Time.realtimeSinceStartupAsDouble < nextFinalCapture || string.IsNullOrEmpty(finalCaptureOutput) || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
                var camera = Camera.main;
                if (camera == null) return;
                var viewport = camera.WorldToViewportPoint(sprite.bounds.center);
                if (viewport.z <= 0 || viewport.x < .05f || viewport.x > .95f || viewport.y < .05f || viewport.y > .9f) return;
                nextFinalCapture = Time.realtimeSinceStartupAsDouble + .1;
                string file = "boiler-final-" + (++finalCaptures).ToString("D2") + "-cell-" + cell.x + "-" + cell.y + ".png";
                System.IO.Directory.CreateDirectory(finalCaptureOutput);
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(finalCaptureOutput, file));
                Debug.Log(FormattableString.Invariant($"[NormalPlaytest] final flame capture={file} cell={cell} screen={camera.WorldToScreenPoint(sprite.bounds.center):F2} size={sprite.bounds.size:F3} order={sprite.sortingOrder} alpha={sprite.color.a:F3}"));
            }
            public void Sample()
            {
                if (ground != null)
                    foreach (var sprite in FindObjectsByType<SpriteRenderer>())
                        if (sprite.enabled && !sprite.forceRenderingOff && sprite.gameObject.activeInHierarchy && sprite.sprite != null && flameNames.Contains(sprite.sprite.name))
                        {
                            var cell = ground.WorldToCell(sprite.transform.position);
                            if (!flamesReported) seen.Add(cell);
                            CaptureFinalSprite(sprite, cell);
                        }
                if (!flamesReported && FlamesComplete)
                { flamesReported = true; Debug.Log("[NormalPlaytest] boiler flame coverage complete expected=" + expected.Count + " observed=" + seen.Count); }
                if (doors == null || doorsReported || doorCells.Count == 0) return;
                bool complete = true;
                foreach (var cell in doorCells)
                {
                    var tile = doors.GetTile(cell);
                    if (tile == opening && doorFrames.Add(doors.GetAnimationFrame(cell)) && !string.IsNullOrEmpty(finalCaptureOutput) && SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                    {
                        System.IO.Directory.CreateDirectory(finalCaptureOutput);
                        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(finalCaptureOutput, "boiler-door-frame-" + doors.GetAnimationFrame(cell) + ".png"));
                    }
                    complete &= tile == opened;
                }
                if (complete)
                { doorsReported = true; Debug.Log("[NormalPlaytest] boiler door animation frames=" + doorFrames.Count + " doors=" + doorCells.Count); }
            }
        }

        void TraceInspectionRenderers()
        {
            if (Time.realtimeSinceStartupAsDouble < inspectionTraceAt) return;
            inspectionTraceAt = Time.realtimeSinceStartupAsDouble + 1;
            foreach (var map in FindObjectsByType<UnityEngine.Tilemaps.Tilemap>())
            {
                var renderer = map.GetComponent<UnityEngine.Tilemaps.TilemapRenderer>();
                if (renderer != null) Debug.Log($"[NormalPlaytest] map={map.name} bounds={map.localBounds} rendererBounds={renderer.bounds} order={renderer.sortingOrder} mode={renderer.mode} color={map.color}");
            }
            var camera = Camera.main;
            if (camera != null) Debug.Log($"[NormalPlaytest] camera={camera.name} sort={camera.transparencySortMode} axis={camera.transparencySortAxis} mask={camera.cullingMask}");
            var names = new System.Collections.Generic.HashSet<string>();
            foreach (var boat in FindObjectsByType<BoatRideZone>(FindObjectsInactive.Include))
                foreach (var sprite in boat.GetComponentsInChildren<SpriteRenderer>(true))
                    if (sprite.sprite != null) names.Add(sprite.sprite.name);
            foreach (var spring in FindObjectsByType<SpringLauncher>(FindObjectsInactive.Include))
                foreach (var sprite in spring.GetComponentsInChildren<SpriteRenderer>(true))
                    if (sprite.sprite != null) names.Add(sprite.sprite.name);
            foreach (var gate in FindObjectsByType<EndStageGateAnimated>(FindObjectsInactive.Include))
                foreach (var sprite in gate.GetComponentsInChildren<SpriteRenderer>(true))
                    if (sprite.sprite != null) names.Add(sprite.sprite.name);
            foreach (var sprite in FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include))
                if (sprite.sprite != null && names.Contains(sprite.sprite.name))
                    Debug.Log(FormattableString.Invariant($"[NormalPlaytest] renderer object={sprite.name} parent={sprite.transform.parent?.name} sprite={sprite.sprite.name} enabled={sprite.enabled} active={sprite.gameObject.activeInHierarchy} forceOff={sprite.forceRenderingOff} layer={sprite.sortingLayerID} order={sprite.sortingOrder} priority={sprite.rendererPriority} position={sprite.transform.position:F3} scale={sprite.transform.lossyScale:F3} bounds={sprite.bounds.size:F3} center={sprite.bounds.center:F3} color={sprite.color} mask={sprite.maskInteraction} sortPoint={sprite.spriteSortPoint} objectLayer={sprite.gameObject.layer} shader={sprite.sharedMaterial?.shader?.name}"));
        }

        readonly float[] audioProbeSamples = new float[256];
        readonly System.Collections.Generic.HashSet<int> audibleSources = new();
        double nextAudioProbe;
        static readonly System.Collections.Generic.Dictionary<AudioSource, AudioClip> nativeAudioClips = new();
        internal static void RecordNativeSound(AudioSource source, AudioClip clip)
        {
            if (!NormalGamePlaytest || source == null || clip == null) return;
            if (nativeAudioClips.Count >= 128 && !nativeAudioClips.ContainsKey(source)) nativeAudioClips.Clear();
            nativeAudioClips[source] = clip;
        }
        static string NativeAudioClipName(AudioSource source) =>
            nativeAudioClips.TryGetValue(source, out var clip) && clip != null ? clip.name : source.clip != null ? source.clip.name : "";

        void ObserveEnvironmentAudio()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null || Time.realtimeSinceStartupAsDouble < nextAudioProbe) return;
            nextAudioProbe = Time.realtimeSinceStartupAsDouble + .02;
            foreach (var source in FindObjectsByType<AudioSource>())
            {
                int id = source.GetEntityId().GetHashCode();
                if (!source.isPlaying) { audibleSources.Remove(id); continue; }
                if (audibleSources.Contains(id) || source == GameMusicController.Instance?.GetMusicSource()) continue;
                source.GetOutputData(audioProbeSamples, 0);
                float peak = 0; foreach (float sample in audioProbeSamples) peak = Mathf.Max(peak, Mathf.Abs(sample));
                if (peak < .00001f) continue;
                audibleSources.Add(source.GetEntityId().GetHashCode());
                Debug.Log(FormattableString.Invariant($"[NormalPlaytest] audio output source={source.name} clip={NativeAudioClipName(source)} peak={peak:F6} volume={source.volume:F3} sfxSetting={GameAudioSettings.SfxVolume:F3} dsp={AudioSettings.dspTime:F3}"));
            }
        }

        static IEnumerator InspectNativeRoomGate()
        {
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
            var room = global::StageAssets.World3RoomProgressionController.FindRoomBounds("Room 2");
            LocalEnvironmentSmoke.PlacePlayers(room);
            yield return new WaitForSecondsRealtime(.3f);
            var progression = FindAnyObjectByType<global::StageAssets.World3RoomProgressionController>();
            var rooms = (Array)progression.GetType().GetField("rooms", flags).GetValue(progression);
            object selected = null;
            foreach (var candidate in rooms)
                if ((Collider2D)candidate.GetType().GetField("roomBounds", flags).GetValue(candidate) == room) selected = candidate;
            if (selected == null) throw new InvalidOperationException("Native gate room not found");
            var type = selected.GetType();
            var cells = (System.Collections.Generic.HashSet<Vector3Int>)type.GetField("remainingCores", flags).GetValue(selected);
            var cores = (System.Collections.Generic.HashSet<CoreMechanismsDestructible>)type.GetField("remainingSceneCores", flags).GetValue(selected);
            var map = (UnityEngine.Tilemaps.Tilemap)progression.GetType().GetField("coreTilemap", flags).GetValue(progression);
            var source = FindAnyObjectByType<PlayerIdentity>().GetComponent<BombController>();
            int count = cells.Count + cores.Count;
            if (count == 0) throw new InvalidOperationException("Native Room 2 fixture requires live cores");
            foreach (var cell in new System.Collections.Generic.List<Vector3Int>(cells)) source.ClearDestructibleForEffect(map.GetCellCenterWorld(cell), spawnDestructiblePrefab: true);
            foreach (var core in new System.Collections.Generic.List<CoreMechanismsDestructible>(cores)) if (core != null) core.PlayDeath();
            yield return new WaitForSecondsRealtime(6);
            var blocker = (global::StageAssets.ExitBlocker)type.GetField("bambooToOpen", flags).GetValue(selected);
            if (blocker == null || !blocker.IsExitOpen) throw new InvalidOperationException("Native Room 2 core completion did not open gate");
            Debug.Log("[NormalPlaytest] native Room 2 gate opened cores=" + count);
        }

        static IEnumerator InspectNativeIce()
        {
            var room = global::StageAssets.World3RoomProgressionController.FindRoomBounds("Room 1");
            FragileFloor floor = null;
            foreach (var candidate in FindObjectsByType<FragileFloor>())
                if (room != null && room.OverlapPoint(candidate.transform.position)) { floor = candidate; break; }
            if (floor == null) throw new InvalidOperationException("Native Room 1 ice not found");
            var actor = FindAnyObjectByType<PlayerIdentity>().GetComponent<MovementController>();
            Vector2 center = floor.transform.position;
            LocalEnvironmentSmoke.PlacePlayers(room, center + Vector2.down * 2);
            actor.GetComponent<BombController>().ClearDestructibleForEffect(center, spawnDestructiblePrefab: false, spawnHiddenObject: false);
            for (int step = 0; step < 4; step++)
            {
                yield return new WaitForSecondsRealtime(.4f);
                Vector2 position = step % 2 == 0 ? center : center + Vector2.down * 2;
                actor.Rigidbody.position = position; actor.transform.position = position;
                NetworkPlayerInput.SyncAuthorityMotionTransform(actor);
                if (Mirror.NetworkServer.active) actor.GetComponent<NetworkPlayerInput>().ServerReconcileMovementNow();
                Physics2D.SyncTransforms();
            }
            yield return new WaitForSecondsRealtime(2);
            var game = FindAnyObjectByType<GameManager>();
            if (!game.indestructibleTilemap.HasTile(game.indestructibleTilemap.WorldToCell(center))) throw new InvalidOperationException("Native cracked ice did not block after second exit");
            Debug.Log("[NormalPlaytest] native ice crack and block completed");
        }
        double nextMusicProbe;
        AudioClip previousMusicClip;
        float previousMusicTime;
        int observedMusicWraps;
        void ObserveNativeMusic()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null || Time.realtimeSinceStartupAsDouble < nextMusicProbe) return;
            nextMusicProbe = Time.realtimeSinceStartupAsDouble + .25;
            var source = GameMusicController.Instance?.GetMusicSource();
            if (source == null || source.clip == null) return;
            if (previousMusicClip != source.clip) observedMusicWraps = 0;
            else if (source.loop && previousMusicTime > source.time + 1)
                Debug.Log("[NormalPlaytest] music native loop wrap=" + (++observedMusicWraps) + " clip=" + source.clip.name);
            previousMusicClip = source.clip; previousMusicTime = source.time;
            source.GetOutputData(audioProbeSamples, 0);
            float peak = 0; foreach (float sample in audioProbeSamples) peak = Mathf.Max(peak, Mathf.Abs(sample));
            Debug.Log(FormattableString.Invariant($"[NormalPlaytest] music output clip={source.clip.name} time={source.time:F3} length={source.clip.length:F3} loop={source.loop} playing={source.isPlaying} focus={Application.isFocused} volume={source.volume:F3} peak={peak:F6} dsp={AudioSettings.dspTime:F3}"));
        }

        readonly System.Collections.Generic.Queue<PlayerAction> uiScaleActions = new();
        bool uiScaleCompleted;
        readonly System.Collections.Generic.Dictionary<string, Vector2> uiReferenceBounds = new();
        double nextUiScaleAction;
        void TickUiScaleInput()
        {
            if (uiScaleActions.Count == 0 || PlayerInputManager.Instance == null || Time.realtimeSinceStartupAsDouble < nextUiScaleAction) return;
            PlayerInputManager.Instance.TapSynthetic(1, uiScaleActions.Dequeue());
            nextUiScaleAction = Time.realtimeSinceStartupAsDouble + .3;
        }

        static void ConfigureUiBattlePlayers()
        {
            var modes = global::SaveSystem.GetBattleModePlayerControlModes();
            for (int i = 0; i < modes.Length; i++) modes[i] = i < 2 ? BattleModePlayerControlMode.Man : BattleModePlayerControlMode.Off;
            global::SaveSystem.SetBattleModePlayerControlModes(modes);
        }

        static IEnumerator InspectNativeUiScale()
        {
            const System.Reflection.BindingFlags fields = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var pause = GamePauseController.Instance;
            var blocked = typeof(GamePauseController).GetMethod("IsPauseBlockedByGameplayState", fields);
            double deadline = Time.realtimeSinceStartupAsDouble + 60;
            while ((pause == null || (bool)blocked.Invoke(pause, null)) && Time.realtimeSinceStartupAsDouble < deadline)
            { pause = GamePauseController.Instance; yield return null; }
            if (pause == null || (bool)blocked.Invoke(pause, null)) throw new InvalidOperationException("UI scale native pause never became available");
            foreach (var actor in FindObjectsByType<MovementController>())
                if (actor.CompareTag("Player"))
                {
                    actor.GetComponent<CharacterHealth>().ResetForRespawn(200);
                    actor.GetComponent<CharacterHealth>().SetExternalInvulnerability(true);
                    actor.SetSuppressInactivityAnimation(true);
                }
            string scene = SceneManager.GetActiveScene().name;
            if (GamePauseController.IsPaused) throw new InvalidOperationException("UI scale requires an initially unpaused stage");
            typeof(GamePauseController).GetMethod("TogglePause", fields).Invoke(pause, null);
            if (!GamePauseController.IsPaused) throw new InvalidOperationException("UI scale native pause rejected");
            Debug.Log("[NormalPlaytest] uiscale native pause started scene=" + scene);
            deadline = Time.realtimeSinceStartupAsDouble + 100;
            while (GamePauseController.IsPaused && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            if (GamePauseController.IsPaused || SceneManager.GetActiveScene().name != scene) throw new InvalidOperationException("UI scale owner did not cancel confirmation and resume natively");
            yield return new WaitForSecondsRealtime(1);
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null && !FindAnyObjectByType<LocalPredictionSmoke>().uiScaleCompleted)
                throw new InvalidOperationException("Native UI scale rendered sequence incomplete");
            Debug.Log("[NormalPlaytest] inspection completed case=uiscale scene=" + scene);
        }

        IEnumerator ObserveUiScale()
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 75;
            StageLabel label = null;
            while (Time.realtimeSinceStartupAsDouble < deadline)
            {
                label = StageIntroTransition.Instance != null ? StageIntroTransition.Instance.stageLabel : null;
                if (label != null && (SceneManager.GetActiveScene().name.StartsWith("BattleMode_", StringComparison.Ordinal) ||
                    !GamePauseController.IsPaused && (StageIntroTransition.Instance.IntroRunning || !NetSync.ShouldSimulateLocally) &&
                    label.gameObject.activeInHierarchy && label.stageText != null && label.stageText.text.Contains("<size="))) break;
                yield return null;
            }
            if (label == null) throw new InvalidOperationException("UI scale has no authored StageLabel");
            bool battle = SceneManager.GetActiveScene().name.StartsWith("BattleMode_", StringComparison.Ordinal);
            var sizes = new[] { new Vector2Int(384, 336), new Vector2Int(512, 448), new Vector2Int(1920, 1080) };
            if (!battle)
                foreach (var size in sizes) yield return SampleUiScale(label, size, "intro");
            else Debug.Log("[NormalPlaytest] uiscale Battle has no campaign stage intro label; pause remains applicable");
            deadline = Time.realtimeSinceStartupAsDouble + 90;
            while (!GamePauseController.IsPaused && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            if (!GamePauseController.IsPaused) throw new InvalidOperationException("UI scale did not receive native pause");
            bool owner = !NetSync.IsOnline || BombermanNetworkManager.IsRoomAdmin;
            var pause = GamePauseController.Instance;
            var refresh = typeof(GamePauseController).GetMethod("RefreshPauseUI", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            GameLanguage original = global::SaveSystem.GetLanguage();
            double began = Time.realtimeSinceStartupAsDouble;
            foreach (GameLanguage language in Enum.GetValues(typeof(GameLanguage)))
            {
                global::SaveSystem.SetLanguage(language); refresh.Invoke(pause, null);
                foreach (var size in sizes) yield return SampleUiScale(label, size, "pause-" + language);
            }
            while (Time.realtimeSinceStartupAsDouble - began < 15) yield return null;
            if (owner)
            {
                uiScaleActions.Enqueue(PlayerAction.MoveDown); yield return new WaitForSecondsRealtime(.6f);
                uiScaleActions.Enqueue(PlayerAction.Start);
            }
            deadline = Time.realtimeSinceStartupAsDouble + 12;
            while (!pause.CaptureOnlinePause().confirming && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            if (!pause.CaptureOnlinePause().confirming) throw new InvalidOperationException("UI scale owner confirmation input did not reach native menu");
            began = Time.realtimeSinceStartupAsDouble;
            foreach (GameLanguage language in Enum.GetValues(typeof(GameLanguage)))
            {
                global::SaveSystem.SetLanguage(language); refresh.Invoke(pause, null);
                foreach (var size in sizes) yield return SampleUiScale(label, size, "confirm-" + language);
            }
            while (Time.realtimeSinceStartupAsDouble - began < 15) yield return null;
            if (owner)
            {
                uiScaleActions.Enqueue(PlayerAction.Start); yield return new WaitForSecondsRealtime(.6f);
                uiScaleActions.Enqueue(PlayerAction.MoveUp); yield return new WaitForSecondsRealtime(.6f);
                uiScaleActions.Enqueue(PlayerAction.Start);
            }
            deadline = Time.realtimeSinceStartupAsDouble + 12;
            while (GamePauseController.IsPaused && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            if (GamePauseController.IsPaused) throw new InvalidOperationException("UI scale native resume input failed");
            global::SaveSystem.SetLanguage(original);
            uiScaleCompleted = true;
            Debug.Log("[NormalPlaytest] uiscale rendered sequence completed scene=" + SceneManager.GetActiveScene().name + " owner=" + owner + " languages=4 pauseSizes=12 confirmationSizes=12");
        }

        IEnumerator SampleUiScale(StageLabel label, Vector2Int size, string phase)
        {
            Screen.SetResolution(size.x, size.y, FullScreenMode.Windowed);
            yield return new WaitForSecondsRealtime(.2f);
            yield return new WaitForEndOfFrame();
            if (phase == "intro" && GamePauseController.IsPaused) throw new InvalidOperationException("A pause menu cannot count as an intro sample");
            if (Screen.width != size.x || Screen.height != size.y || !label.gameObject.activeInHierarchy)
                throw new InvalidOperationException("UI scale actual viewport/label does not match requested sample phase=" + phase + " screen=" + Screen.width + "x" + Screen.height);
            Canvas.ForceUpdateCanvases(); label.stageText.ForceMeshUpdate(true, true);
            var viewport = PixelPerfectViewport.ResolveFinalPixelRect(Camera.main);
            float zoom = Mathf.Max(1, Mathf.Floor(Mathf.Min(viewport.width / 256, viewport.height / 224) + .0001f));
            bool battle = SceneManager.GetActiveScene().name.StartsWith("BattleMode_", StringComparison.Ordinal);
            float expected = (battle ? 38f : 44f) * zoom / 4;
            float actual = -1;
            foreach (var character in label.stageText.textInfo.characterInfo)
                if (character.isVisible) { actual = character.pointSize * label.stageText.canvas.scaleFactor; break; }
            float expectedBase = 36f * zoom / 4;
            if (Mathf.Abs(label.stageText.fontSize * label.stageText.canvas.scaleFactor - expectedBase) > .06f)
                throw new InvalidOperationException("UI scale left TMP base font and whitespace at a fixed size");
            if (Mathf.Abs(actual - expected) > .06f)
                throw new InvalidOperationException("UI scale font is not proportional to safe frame phase=" + phase + " actual=" + actual + " expected=" + expected);
            Vector2 preferredPixels = new Vector2(label.stageText.preferredWidth, label.stageText.preferredHeight) * label.stageText.canvas.scaleFactor;
            Vector2 normalized = preferredPixels / zoom;
            string layoutKey = SceneManager.GetActiveScene().name + ":" + phase;
            if (uiReferenceBounds.TryGetValue(layoutKey, out var baseline))
            {
                if (Mathf.Abs(normalized.x - baseline.x) > 1 || Mathf.Abs(normalized.y - baseline.y) > 1)
                    throw new InvalidOperationException("UI scale spacing/layout changed relative to safe frame phase=" + phase + " baseline=" + baseline + " actual=" + normalized);
            }
            else uiReferenceBounds.Add(layoutKey, normalized);
            Debug.Log(FormattableString.Invariant($"[NormalPlaytest] uiscale sample phase={phase} scene={SceneManager.GetActiveScene().name} screen={Screen.width}x{Screen.height} viewport={viewport.width}x{viewport.height} zoom={zoom} canvas={label.stageText.canvas.scaleFactor:F4} fontPixels={actual:F4} expected={expected:F4} baseFontPixels={label.stageText.fontSize * label.stageText.canvas.scaleFactor:F4} textWidthPixels={preferredPixels.x:F4} textHeightPixels={preferredPixels.y:F4} paused={GamePauseController.IsPaused} confirming={GamePauseController.Instance.CaptureOnlinePause().confirming}"));
            CaptureNormalFrame("ui-" + phase + "-" + size.x + "x" + size.y);
            yield return new WaitForSecondsRealtime(.1f);
        }

        static IEnumerator InspectNativeMusic()
        {
            // Keep the long audio observation in this scene, without an unrelated
            // enemy death/Game Over replacing the soundtrack while minimized.
            foreach (var actor in FindObjectsByType<MovementController>())
                if (actor.CompareTag("Player"))
                {
                    var health = actor.GetComponent<CharacterHealth>();
                    health.ResetForRespawn(200); health.SetExternalInvulnerability(true);
                    actor.SetSuppressInactivityAnimation(true);
                }
            Debug.Log("[NormalPlaytest] music minimize preparation window started");
            yield return new WaitForSecondsRealtime(20);
            if (GameMusicController.Instance == null) throw new InvalidOperationException("Native music controller not found");
            GameMusicController.Instance.PlayDefaultMusic(true);
            Debug.Log("[NormalPlaytest] native music intro restarted for minimized transition");
            var music = GameMusicController.Instance;
            if (music.defaultMusic == null || music.defaultMusicLoop == null) throw new InvalidOperationException("Music reproduction requires an authored intro and loop pair");
            float duration = music.defaultMusic.length + music.defaultMusicLoop.length * 2 + 30;
            if (duration > 500) throw new InvalidOperationException("Music reproduction exceeds its bounded duration");
            Debug.Log("[NormalPlaytest] music two-wrap observation seconds=" + duration);
            double musicDeadline = Time.realtimeSinceStartupAsDouble + duration;
            while (Time.realtimeSinceStartupAsDouble < musicDeadline)
            {
                yield return new WaitForSecondsRealtime((float)Math.Min(90, musicDeadline - Time.realtimeSinceStartupAsDouble));
                if (musicDeadline - Time.realtimeSinceStartupAsDouble < 1) break;
                // Preserve the production 5-minute room inactivity rule. Send a
                // short real owner input during this longer bounded audio test.
                foreach (var actor in FindObjectsByType<MovementController>())
                    if (actor.CompareTag("Player") && actor.PlayerId == 1)
                    {
                        DriveWaterOwner(actor, WaterMask(Vector2.right), .1f);
                        yield return new WaitForSecondsRealtime(.2f);
                        DriveWaterOwner(actor, WaterMask(Vector2.left), .1f);
                        yield return new WaitForSecondsRealtime(.2f);
                        DriveWaterOwner(actor, 0, 0);
                        Debug.Log("[NormalPlaytest] music real owner activity sent");
                        break;
                    }
            }
            if (!Mirror.NetworkServer.active && FindAnyObjectByType<LocalPredictionSmoke>().observedMusicWraps < 2)
                throw new InvalidOperationException("Native music did not complete two loop wraps");
            var pause = GamePauseController.Instance;
            var toggle = typeof(GamePauseController).GetMethod("TogglePause", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (pause == null || toggle == null) throw new InvalidOperationException("Music pause controller missing");
            toggle.Invoke(pause, null);
            if (!GamePauseController.IsPaused) throw new InvalidOperationException("Native music pause rejected");
            var source = (AudioSource)typeof(GameMusicController).GetField("musicSource", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(music);
            float frozen = source.time;
            yield return new WaitForSecondsRealtime(.75f);
            float advanced = source.time - frozen;
            if (advanced < 0 && source.clip != null) advanced += source.clip.length;
            // Native pause freezes gameplay while leaving the soundtrack playing.
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null && (!source.isPlaying || advanced < .25f))
                throw new InvalidOperationException("Native pause stopped the stage soundtrack");
            toggle.Invoke(pause, null);
            if (GamePauseController.IsPaused) throw new InvalidOperationException("Native music resume rejected");
            yield return new WaitForSecondsRealtime(1);
            Debug.Log("[NormalPlaytest] native music pause and resume completed");
            Debug.Log("[NormalPlaytest] inspection completed case=music");
        }

        readonly System.Collections.Generic.HashSet<int> pinkJumpSeen = new(), pinkShadowRestored = new();
        IEnumerator ObservePinkEndFrames()
        {
            var end = new WaitForEndOfFrame();
            while (true)
            {
                yield return end;
                foreach (var actor in FindObjectsByType<MovementController>())
                {
                    if (!actor.CompareTag("Player")) continue;
                    var shadow = actor.GetComponent<PinkLouieJumpAbility>()?.shadow;
                    if (shadow == null) continue;
                    int id = actor.PlayerId;
                    if (shadow.HasGroundedJump) pinkJumpSeen.Add(id);
                    if (!pinkJumpSeen.Contains(id) || pinkShadowRestored.Contains(id) || shadow.HasGroundedJump || shadow.animator == null) continue;
                    var walk = shadow.animator.walkShadow?.GetComponent<SpriteRenderer>();
                    var jump = shadow.animator.jumpShadow?.GetComponent<SpriteRenderer>();
                    if (walk == null || !walk.enabled || !walk.gameObject.activeInHierarchy || walk.forceRenderingOff || jump != null && jump.enabled) continue;
                    pinkShadowRestored.Add(id); CaptureNormalFrame("p" + id + "-grounded-shadow");
                    Debug.Log("[NormalPlaytest] pink grounded shadow restored player=" + id);
                }
            }
        }
        static IEnumerator InspectNativePink()
        {
            var game = FindAnyObjectByType<GameManager>();
            var list = new System.Collections.Generic.List<MovementController>();
            foreach (var actor in FindObjectsByType<MovementController>()) if (actor.CompareTag("Player")) list.Add(actor);
            foreach (var actor in list)
            {
                actor.GetComponent<CharacterHealth>().ResetForRespawn(200); actor.GetComponent<CharacterHealth>().SetExternalInvulnerability(true);
                actor.SetSuppressInactivityAnimation(true); actor.CancelPlayerPoses();
                actor.GetComponent<PlayerMountCompanion>().RestoreMounted(MountedType.Pink);
                Vector2? origin = null; Vector2 direction = default;
                Vector2 focus = Camera.main != null ? Camera.main.transform.position : actor.transform.position;
                float nearestLane = float.PositiveInfinity;
                foreach (var cell in game.groundTilemap.cellBounds.allPositionsWithin)
                {
                    if (!game.groundTilemap.HasTile(cell)) continue;
                    foreach (Vector3Int axis in new[] { Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right })
                    {
                        bool clear = true;
                        for (int step = 0; step < 3; step++)
                        {
                            Vector3 position = game.groundTilemap.GetCellCenterWorld(cell + axis * step);
                            if (!game.groundTilemap.HasTile(cell + axis * step) || game.indestructibleTilemap.HasTile(game.indestructibleTilemap.WorldToCell(position))) { clear = false; break; }
                        }
                        if (!clear) continue;
                        Vector2 start = game.groundTilemap.GetCellCenterWorld(cell);
                        float score = (start - focus).sqrMagnitude;
                        if (score >= nearestLane) continue;
                        nearestLane = score; origin = start; direction = new Vector2(axis.x, axis.y);
                    }
                }
                if (!origin.HasValue) throw new InvalidOperationException("Pink reproduction has no authored floor lane");
                for (int step = 0; step < 3; step++) actor.GetComponent<BombController>().ClearDestructibleForEffect(origin.Value + direction * step, spawnDestructiblePrefab: false, spawnHiddenObject: false);
                foreach (var other in list)
                    if (other != actor)
                    {
                        Vector2 parking = origin.Value + new Vector2(-direction.y, direction.x) * 3;
                        other.Rigidbody.position = parking; other.transform.position = parking;
                        NetworkPlayerInput.SyncAuthorityMotionTransform(other);
                        if (Mirror.NetworkServer.active) other.GetComponent<NetworkPlayerInput>().ServerReconcileMovementNow();
                    }
                actor.Rigidbody.position = origin.Value; actor.transform.position = origin.Value;
                NetworkPlayerInput.SyncAuthorityMotionTransform(actor);
                if (Mirror.NetworkServer.active) actor.GetComponent<NetworkPlayerInput>().ServerReconcileMovementNow();
                foreach (var camera in FindObjectsByType<CameraFollowClamp2D>()) camera.ForceSnapNow(refreshPlayersNow: true);
                yield return new WaitForSecondsRealtime(1);
                var jump = actor.GetComponent<PinkLouieJumpAbility>();
                DriveWaterOwner(actor, WaterMask(direction) | 32, .18f);
                double deadline = Time.realtimeSinceStartupAsDouble + 3;
                while (!jump.JumpActive && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                if (!jump.JumpActive) throw new InvalidOperationException("Native owner command did not begin Pink jump");
                DriveWaterOwner(actor, 0, 0);
                Debug.Log("[NormalPlaytest] native Pink jump started player=" + actor.PlayerId);
                deadline = Time.realtimeSinceStartupAsDouble + 4;
                while (jump.JumpActive && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                yield return new WaitForSecondsRealtime(.7f);
                if (jump.JumpActive || actor.InputLocked || actor.ExternalMovementOverride || jump.shadow == null || jump.shadow.HasGroundedJump || !jump.shadow.animator.walkShadow.GetComponent<SpriteRenderer>().enabled)
                    throw new InvalidOperationException("Native Pink landing did not restore walking/shadow");
                Debug.Log("[NormalPlaytest] native Pink landing restored player=" + actor.PlayerId);
            }
            Debug.Log("[NormalPlaytest] inspection completed case=pink");
        }

        static IEnumerator InspectNativeBoatSeats()
        {
            const System.Reflection.BindingFlags fields = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var boats = FindObjectsByType<BoatRideZone>();
            var actors = new System.Collections.Generic.List<MovementController>();
            foreach (var actor in FindObjectsByType<MovementController>()) if (actor.CompareTag("Player")) actors.Add(actor);
            if (boats.Length == 0 || actors.Count != 2) throw new InvalidOperationException("Boat seat reproduction requires two native actors and an authored boat");
            int ordinal = 0;
            foreach (var actor in actors)
            {
                var boat = boats[ordinal++ % boats.Length];
                foreach (var other in actors)
                    if (other != actor)
                    {
                        Vector2 parking = (Vector2)boat.transform.position + Vector2.down * 3;
                        other.Rigidbody.position = parking; other.transform.position = parking;
                        NetworkPlayerInput.SyncAuthorityMotionTransform(other);
                        if (Mirror.NetworkServer.active) other.GetComponent<NetworkPlayerInput>().ServerReconcileMovementNow();
                    }
                var mount = actor.GetComponent<PlayerMountCompanion>();
                MountedType type = actor.PlayerId == 1 ? MountedType.Mole : MountedType.Tank;
                actor.GetComponent<CharacterHealth>().ResetForRespawn(200); actor.SetSuppressInactivityAnimation(true); actor.CancelPlayerPoses();
                mount.RestoreMounted(type);
                yield return new WaitForSecondsRealtime(.8f);
                Vector2 original = actor.Rigidbody.position;
                actor.Rigidbody.position = boat.transform.position; actor.transform.position = boat.transform.position;
                NetworkPlayerInput.SyncAuthorityMotionTransform(actor);
                if (!boat.IsRider(actor) && !boat.TryMount(actor)) throw new InvalidOperationException("Native boat rejected the HeadOnly rider");
                // Begin the steering cohort in open authored water, outside the
                // shore triggers that legitimately dismount a returning boat.
                Vector2? openWater = null;
                float nearestWater = float.PositiveInfinity;
                foreach (var waterMap in FindObjectsByType<UnityEngine.Tilemaps.Tilemap>())
                {
                    if (!waterMap.CompareTag("Water") && !waterMap.name.Contains("Water", StringComparison.OrdinalIgnoreCase)) continue;
                    foreach (var cell in waterMap.cellBounds.allPositionsWithin)
                    {
                        if (!waterMap.HasTile(cell)) continue;
                        Vector2 center = waterMap.GetCellCenterWorld(cell);
                        bool nearShore = false;
                        foreach (var zone in FindObjectsByType<BoatUnmountZone>())
                            if (Vector2.Distance(zone.GetComponent<Collider2D>().ClosestPoint(center), center) < 2.5f) { nearShore = true; break; }
                        if (nearShore) continue;
                        float distance = (center - (Vector2)boat.transform.position).sqrMagnitude;
                        if (distance < nearestWater) { nearestWater = distance; openWater = center; }
                    }
                }
                if (!openWater.HasValue) throw new InvalidOperationException("Boat cohort has no open water outside shore triggers");
                actor.Rigidbody.position = openWater.Value; actor.transform.position = openWater.Value;
                NetworkPlayerInput.SyncAuthorityMotionTransform(actor); BoatRideZone.ReconcileNetworkRider(actor);
                foreach (var camera in FindObjectsByType<CameraFollowClamp2D>()) camera.ForceSnapNow(refreshPlayersNow: true);
                foreach (Vector2 direction in new[] { Vector2.down, Vector2.up, Vector2.left, Vector2.right })
                {
                    DriveWaterOwner(actor, WaterMask(direction), .3f);
                    yield return new WaitForSecondsRealtime(.4f);
                    DriveWaterOwner(actor, 0, 0);
                    yield return new WaitForSecondsRealtime(.4f);
                    int index = direction == Vector2.up ? 1 : direction == Vector2.left ? 2 : direction == Vector2.right ? 3 : 0;
                    string field = index == 1 ? "headOnlyUpOffset" : index == 2 ? "headOnlyLeftOffset" : index == 3 ? "headOnlyRightOffset" : "headOnlyDownOffset";
                    var head = boat.NetworkHeadAnimations(actor.transform)[index];
                    Vector3 initial = (Vector3)typeof(AnimatedSpriteRenderer).GetField("initialVisualLocalPosition", fields).GetValue(head);
                    Vector3 expected = initial + (Vector3)(Vector2)typeof(BoatRideZone).GetField(field, fields).GetValue(boat);
                    if (!boat.IsRider(actor) || Vector3.Distance(expected, head.RestingLocalPosition) > .001f) throw new InvalidOperationException("Boat seat mismatch riding=" + boat.IsRider(actor) + " expected=" + expected + " resting=" + head.RestingLocalPosition + " actual=" + head.transform.localPosition);
                    Debug.Log("[NormalPlaytest] native boat seat player=" + actor.PlayerId + " type=" + type + " direction=" + direction + " local=" + head.transform.localPosition);
                }
                actor.Rigidbody.position = original; actor.transform.position = original;
                NetworkPlayerInput.SyncAuthorityMotionTransform(actor);
                if (!boat.TryUnmount(actor) || mount.GetMountedLouieType() != type) throw new InvalidOperationException("Native boat did not restore the previous HeadOnly mount");
                BoatRideZone.ReconcileNetworkRider(actor);
                yield return new WaitForSecondsRealtime(.6f);
            }
            Debug.Log("[NormalPlaytest] inspection completed case=boat");
        }

        readonly System.Collections.Generic.HashSet<int> hurricaneForceSeen = new();
        void ObserveHurricaneApproval()
        {
            if (!Mirror.NetworkClient.active || Mirror.NetworkServer.active) return;
            foreach (var actor in FindObjectsByType<MovementController>())
                if (actor.TryGetComponent<NetworkPlayerInput>(out var input) && input.isLocalPlayer && actor.NetworkEnemyForceDiagnosticState == "Hurricane" && hurricaneForceSeen.Add(actor.PlayerId))
                { CaptureNormalFrame("p" + actor.PlayerId + "-approved-pull"); Debug.Log("[NormalPlaytest] hurricane approval observed owner=" + actor.PlayerId); }
        }
        static IEnumerator InspectNativeHurricane()
        {
            const System.Reflection.BindingFlags fields = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var all = FindObjectsByType<HurricaneMovementController>(FindObjectsInactive.Include);
            if (all.Length == 0) throw new InvalidOperationException("Native Hurricane not found");
            var source = all[0];
            var room = global::StageAssets.World3RoomProgressionController.FindRoomBoundsContaining(source.transform.position);
            LocalEnvironmentSmoke.PlacePlayers(room, source.transform.position + Vector3.down * 2);
            yield return new WaitForSecondsRealtime(.3f);
            var game = FindAnyObjectByType<GameManager>();
            foreach (var actor in FindObjectsByType<MovementController>())
            {
                if (!actor.CompareTag("Player")) continue;
                actor.GetComponent<CharacterHealth>().ResetForRespawn(200); actor.GetComponent<CharacterHealth>().SetExternalInvulnerability(true);
                actor.SetSuppressInactivityAnimation(true); actor.CancelPlayerPoses();
                double deadline = Time.realtimeSinceStartupAsDouble + 15;
                AnimatedSpriteRenderer animation;
                do
                {
                    animation = (AnimatedSpriteRenderer)typeof(EnemyMovementController).GetField("activeSprite", fields).GetValue(source);
                    if (animation != null && animation.CurrentFrame is 12 or 13) break;
                    yield return null;
                } while (Time.realtimeSinceStartupAsDouble < deadline);
                if (animation == null || animation.CurrentFrame is not (12 or 13)) throw new InvalidOperationException("Native Hurricane did not reach its expanded pull phase");
                Vector2? start = null; Vector2 axis = default;
                foreach (Vector2 direction in new[] { Vector2.right, Vector2.left, Vector2.up, Vector2.down })
                {
                    Vector2 target = (Vector2)source.transform.position + direction * 2;
                    if (!game.groundTilemap.HasTile(game.groundTilemap.WorldToCell(target)) || game.indestructibleTilemap.HasTile(game.indestructibleTilemap.WorldToCell(target))) continue;
                    start = target; axis = direction; break;
                }
                if (!start.HasValue) throw new InvalidOperationException("Native Hurricane has no clear aligned player lane");
                actor.GetComponent<BombController>().ClearDestructibleForEffect(start.Value, spawnDestructiblePrefab: false, spawnHiddenObject: false);
                actor.Rigidbody.position = start.Value; actor.transform.position = start.Value;
                actor.ApplyDirectionFromVector(Vector2.zero); NetworkPlayerInput.SyncAuthorityMotionTransform(actor);
                if (Mirror.NetworkServer.active) actor.GetComponent<NetworkPlayerInput>().ServerReconcileMovementNow();
                Physics2D.SyncTransforms();
                foreach (var camera in FindObjectsByType<CameraFollowClamp2D>()) camera.ForceSnapNow(refreshPlayersNow: true);
                yield return new WaitForFixedUpdate(); yield return null;
                float pull = Vector2.Dot(actor.Rigidbody.position - start.Value, -axis);
                if (pull < .002f) throw new InvalidOperationException("Expanded native Hurricane did not pull the idle player; delta=" + (actor.Rigidbody.position - start.Value));
                Debug.Log("[NormalPlaytest] native Hurricane pulled player=" + actor.PlayerId + " distance=" + pull);
                yield return new WaitForSecondsRealtime(.4f);
            }
            Debug.Log("[NormalPlaytest] inspection completed case=force");
        }

        static IEnumerator InspectNativeRoomTransitions()
        {
            const System.Reflection.BindingFlags fields = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            foreach (var actor in FindObjectsByType<MovementController>())
                if (actor.CompareTag("Player")) { actor.GetComponent<CharacterHealth>().ResetForRespawn(200); actor.GetComponent<CharacterHealth>().SetExternalInvulnerability(true); actor.SetSuppressInactivityAnimation(true); actor.CancelPlayerPoses(); }
            BattleMode9MinecartController boarded = null;
            if (SceneManager.GetActiveScene().name == "Stage_3-2")
            {
                foreach (var cart in FindObjectsByType<BattleMode9MinecartController>()) if (cart.StationTile == new Vector2Int(4, -2)) { boarded = cart; break; }
                if (boarded == null) throw new InvalidOperationException("Room transition requires the authored Room 1 shuttle");
                var actor = FindAnyObjectByType<PlayerIdentity>().GetComponent<MovementController>();
                actor.Rigidbody.position = boarded.transform.position; actor.transform.position = boarded.transform.position;
                NetworkPlayerInput.SyncAuthorityMotionTransform(actor);
                if (Mirror.NetworkServer.active) actor.GetComponent<NetworkPlayerInput>().ServerReconcileMovementNow();
                double deadline = Time.realtimeSinceStartupAsDouble + 3;
                while (boarded.CurrentRider != actor && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                if (boarded.CurrentRider != actor) throw new InvalidOperationException("Native shuttle boarding failed before room transition");
                Debug.Log("[NormalPlaytest] room transition native passenger boarded");
            }
            for (int room = 2; room <= 3; room++)
            {
                global::StageAssets.World3RoomTwoTransition transition = null;
                foreach (var candidate in FindObjectsByType<global::StageAssets.World3RoomTwoTransition>())
                    if ((Camera)candidate.GetType().GetField("sourceRoomCamera", fields).GetValue(candidate) == Camera.main) { transition = candidate; break; }
                if (transition == null) throw new InvalidOperationException("Missing native transition to room=" + room);
                var destination = (Camera)transition.GetType().GetField("destinationRoomCamera", fields).GetValue(transition);
                transition.GetType().GetField("transitionStarted", fields).SetValue(transition, true);
                transition.GetType().GetField("transitionSourcePosition", fields).SetValue(transition, (Vector2)Camera.main.transform.position);
                yield return (IEnumerator)transition.GetType().GetMethod("TransitionRoutine", fields).Invoke(transition, null);
                if (Camera.main != destination) throw new InvalidOperationException("Native room transition did not select its destination camera");
                if (boarded != null && boarded.RideActive) throw new InvalidOperationException("Room transition retained a minecart passenger");
                foreach (var actor in FindObjectsByType<MovementController>())
                    if (actor.CompareTag("Player"))
                    {
                        Vector3 view = destination.WorldToViewportPoint(actor.transform.position);
                        if (actor.InputLocked || actor.ExternalMovementOverride || view.x < 0 || view.x > 1 || view.y < 0 || view.y > 1) throw new InvalidOperationException("Room destination retained a lock or started outside camera focus");
                        Debug.Log("[NormalPlaytest] native room arrival room=" + room + " player=" + actor.PlayerId + " viewport=" + view);
                    }
                yield return new WaitForSecondsRealtime(2);
            }
            Debug.Log("[NormalPlaytest] inspection completed case=rooms");
        }

        static IEnumerator InspectNativeCartLanding()
        {
            const System.Reflection.BindingFlags fields = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            BattleMode9MinecartController cart = null;
            var room = global::StageAssets.World3RoomProgressionController.FindRoomBounds("Room 1");
            foreach (var candidate in FindObjectsByType<BattleMode9MinecartController>())
                if (room != null && room.OverlapPoint(candidate.transform.position)) { cart = candidate; break; }
            if (cart == null) throw new InvalidOperationException("Room 1 native minecart not found");
            var actors = new System.Collections.Generic.List<MovementController>();
            foreach (var actor in FindObjectsByType<MovementController>()) if (actor.CompareTag("Player")) actors.Add(actor);
            if (actors.Count != 2) throw new InvalidOperationException("Cart landing requires two native actors");
            var map = (UnityEngine.Tilemaps.Tilemap)typeof(BattleMode9MinecartController).GetField("groundTilemap", fields).GetValue(cart);
            foreach (var actor in actors)
            {
                var other = actors[0] == actor ? actors[1] : actors[0];
                foreach (var player in actors)
                {
                    player.GetComponent<CharacterHealth>().ResetForRespawn(200); player.GetComponent<CharacterHealth>().SetExternalInvulnerability(true);
                    player.SetSuppressInactivityAnimation(true); player.CancelPlayerPoses();
                    Vector2 parking = (Vector2)cart.transform.position + Vector2.up * 3 + Vector2.right * player.PlayerId;
                    player.Rigidbody.position = parking; player.transform.position = parking;
                    NetworkPlayerInput.SyncAuthorityMotionTransform(player);
                    if (Mirror.NetworkServer.active) player.GetComponent<NetworkPlayerInput>().ServerReconcileMovementNow();
                }
                actor.GetComponent<PlayerMountCompanion>().RestoreMounted(actor.PlayerId == 1 ? MountedType.None : MountedType.Pink);
                yield return new WaitForSecondsRealtime(.8f);
                var tile = cart.ExitTile;
                if (SceneManager.GetActiveScene().name == "Stage_3-2")
                {
                    var stationA = (Vector2Int)typeof(BattleMode9MinecartController).GetField("stage32StationA", fields).GetValue(cart);
                    var stationB = (Vector2Int)typeof(BattleMode9MinecartController).GetField("stage32StationB", fields).GetValue(cart);
                    // Both authored Room 1 directions end with a rightward leg.
                    tile = (cart.StationTile == stationA ? stationB : stationA) + Vector2Int.right;
                }
                Vector2 exit = map.GetCellCenterWorld(new Vector3Int(tile.x, tile.y, 0));
                Vector2 otherParking = other.Rigidbody.position;
                other.Rigidbody.position = exit; other.transform.position = exit;
                NetworkPlayerInput.SyncAuthorityMotionTransform(other);
                if (Mirror.NetworkServer.active) other.GetComponent<NetworkPlayerInput>().ServerReconcileMovementNow();
                Physics2D.SyncTransforms();
                var source = other.GetComponent<BombController>();
                if (!source.TryPlaceBombAtIgnoringInputLock(exit, out var placed, consumeBomb: false, trackOwnerBombTraversal: false) || placed == null) throw new InvalidOperationException("Native landing bomb placement rejected");
                var bomb = placed.GetComponent<Bomb>(); bomb.SetFusePaused(true);
                yield return new WaitForFixedUpdate();
                other.Rigidbody.position = otherParking; other.transform.position = otherParking;
                NetworkPlayerInput.SyncAuthorityMotionTransform(other);
                if (Mirror.NetworkServer.active) other.GetComponent<NetworkPlayerInput>().ServerReconcileMovementNow();
                Physics2D.SyncTransforms();
                yield return new WaitForSecondsRealtime(.3f);
                if (!bomb.IsSolid || !bomb.IsFusePaused) throw new InvalidOperationException("Landing cohort requires a solid, live other-owner bomb solid=" + bomb.IsSolid + " paused=" + bomb.IsFusePaused + " exit=" + exit);
                actor.Rigidbody.position = cart.transform.position; actor.transform.position = cart.transform.position;
                NetworkPlayerInput.SyncAuthorityMotionTransform(actor);
                if (Mirror.NetworkServer.active) actor.GetComponent<NetworkPlayerInput>().ServerReconcileMovementNow();
                Physics2D.SyncTransforms();
                double deadline = Time.realtimeSinceStartupAsDouble + 3;
                while (cart.CurrentRider != actor && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                if (cart.CurrentRider != actor) throw new InvalidOperationException("Native cart did not board the landing owner");
                Debug.Log("[NormalPlaytest] cart solid-bomb rider boarded player=" + actor.PlayerId + " exit=" + exit);
                foreach (var camera in FindObjectsByType<CameraFollowClamp2D>()) camera.ForceSnapNow(refreshPlayersNow: true);
                deadline = Time.realtimeSinceStartupAsDouble + 30;
                while (cart.RideActive && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                yield return new WaitForSecondsRealtime(1);
                if (cart.RideActive || actor.InputLocked || actor.ExternalMovementOverride || Vector2.Distance(actor.Rigidbody.position, exit) < .5f)
                    throw new InvalidOperationException("Native minecart exit did not bounce off the solid bomb into a free tile");
                Debug.Log("[NormalPlaytest] native cart landing bounce completed player=" + actor.PlayerId + " from=" + exit + " to=" + actor.Rigidbody.position);
                if (bomb != null) { bomb.SetFusePaused(false); source.ExplodeBomb(placed); }
                yield return new WaitForSecondsRealtime(1.5f);
                Vector2 released = actor.Rigidbody.position;
                var blocked = typeof(MovementController).GetMethod("IsBlockedAtPosition", fields);
                bool recovered = false;
                foreach (Vector2 recoveryDirection in new[] { Vector2.left, Vector2.right, Vector2.up, Vector2.down })
                {
                    Vector2 destination = actor.Rigidbody.position + recoveryDirection * actor.tileSize;
                    if (!map.HasTile(map.WorldToCell(destination)) ||
                        (bool)blocked.Invoke(actor, new object[] { actor.Rigidbody.position + recoveryDirection * actor.tileSize * .5f, recoveryDirection, false })) continue;
                    DriveWaterOwner(actor, WaterMask(recoveryDirection), .25f);
                    yield return new WaitForSecondsRealtime(.4f);
                    DriveWaterOwner(actor, 0, 0);
                    if (Vector2.Distance(released, actor.Rigidbody.position) > .05f) { recovered = true; break; }
                }
                if (!recovered) throw new InvalidOperationException("Cart landing did not return real owner input on a walkable neighbor player=" + actor.PlayerId);
                Debug.Log("[NormalPlaytest] cart owner input recovered player=" + actor.PlayerId + " from=" + released + " to=" + actor.Rigidbody.position);
            }
            Debug.Log("[NormalPlaytest] inspection completed case=cart");
        }

        IEnumerator ObserveRoomEndFrames()
        {
            var end = new WaitForEndOfFrame(); Camera previous = null; int count = 0; double next = 0;
            while (true)
            {
                yield return end;
                if (!SceneManager.GetActiveScene().name.StartsWith("Stage_", StringComparison.Ordinal)) continue;
                var camera = Camera.main;
                if (camera != previous) { previous = camera; count = 0; next = 0; }
                if (camera == null || count >= 10 || Time.realtimeSinceStartupAsDouble < next) continue;
                next = Time.realtimeSinceStartupAsDouble + .05;
                int active = 0;
                foreach (var candidate in FindObjectsByType<Camera>()) if (candidate.CompareTag("MainCamera") && candidate.isActiveAndEnabled) active++;
                string file = "camera-" + camera.GetEntityId().GetHashCode() + "-" + count++;
                CaptureNormalFrame(file);
                Debug.Log("[NormalPlaytest] room camera frame=" + file + " camera=" + camera.name + " active=" + active + " position=" + camera.transform.position);
                foreach (var actor in FindObjectsByType<MovementController>())
                    if (actor.CompareTag("Player")) Debug.Log("[NormalPlaytest] room camera player=" + actor.PlayerId + " viewport=" + camera.WorldToViewportPoint(actor.transform.position));
            }
        }

        void ObserveNormalGamePlaytest()
        {
            ObserveRenderOutput();
            ObserveCampaignRadius();
            if (NormalCase == "submerge") ObserveSubmersion();
            if (NormalCase == "waterbomb") ObserveWaterBomb();
            if (NormalCase == "items") ObserveCampaignItems();
            if (NormalCase == "geyser") ObserveGeyserBarrier();
            if (NormalCase == "bossitems") ObserveBossItems();
            if (NormalCase == "force") ObserveHurricaneApproval();
            if (NormalCase == "unlock") { ObserveUnlockToast(); ObserveEnvironmentAudio(); }
            if (NormalCase is "environment" or "chip") ObserveEnvironmentAudio();
            if (NormalCase == "music") ObserveNativeMusic();
            if (NormalCase == "portal" && SceneManager.GetActiveScene().name == global::StageAssets.World3HallPortalSelectionController.SceneName)
                foreach (var portalActor in FindObjectsByType<MovementController>())
                    if (portalActor.CompareTag("Player"))
                        foreach (var animation in portalActor.GetComponentsInChildren<AnimatedSpriteRenderer>(true))
                        {
                            if (animation.GetComponentInParent<MountVisualController>(true) != null || !animation.TryGetComponent<SpriteRenderer>(out var sprite) || !sprite.enabled || !sprite.gameObject.activeInHierarchy) continue;
                            float alpha = sprite.color.a; int id = portalActor.PlayerId;
                            if (alpha > .01f && alpha < .5f) portalBlinkSeen.Add(id);
                            if (alpha <= .01f) portalZeroSeen.Add(id);
                            if (!portalBodyAlpha.TryGetValue(id, out float previousAlpha) || !Mathf.Approximately(previousAlpha, alpha))
                            {
                                portalBodyAlpha[id] = alpha;
                                Debug.Log(FormattableString.Invariant($"[NormalPlaytest] portal body alpha player={id} alpha={alpha:F3} sprite={sprite.name}"));
                            }
                        }
            if (NormalCase == "boiler")
            {
                if (boilerTrace == null || !boilerTrace.Current)
                {
                    var boiler = FindAnyObjectByType<Assets.Scripts.Explosions.BoilerPowderTrailIgniterPrefab>();
                    if (boiler != null) boilerTrace = new BoilerTrace(boiler);
                }
                boilerTrace?.Sample();
            }
            if (NormalCase is "stage23" or "gate") TraceInspectionRenderers();
            if (NormalCase == "intro" && Time.realtimeSinceStartupAsDouble >= inspectionTraceAt)
            {
                inspectionTraceAt = Time.realtimeSinceStartupAsDouble + .2;
                var clown = FindAnyObjectByType<StageIntroClownMaskBoss>(FindObjectsInactive.Include);
                var image = clown != null ? clown.spotlightImage : null;
                var material = image != null ? image.material : null;
                bool spotlight = material != null && material.HasProperty("_EllipseX");
                Debug.Log($"[NormalPlaytest] spotlight image={image != null} enabled={image != null && image.enabled} active={image != null && image.gameObject.activeInHierarchy} color={image?.color} shader={material?.shader?.name} supported={spotlight} alpha={(spotlight ? material.GetColor("_Color").a : -1)} center={(spotlight ? material.GetVector("_Center") : Vector4.zero)}");
            }
            if (NormalCase == "credits")
            {
                var ending = EndingScreenController.Instance;
                if (ending == null || !ending.Running) return;
                if (creditsAt < 0) { creditsAt = Time.realtimeSinceStartupAsDouble; Debug.Log("[NormalPlaytest] client credits visible"); }
                if (ending.ShowingFinalMessage && !creditsFinalSeen)
                { creditsFinalSeen = true; Debug.Log("[NormalPlaytest] client credits final message visible"); }
                if (!(offline || BombermanNetworkManager.IsRoomAdmin) || PlayerInputManager.Instance == null) return;
                double age = Time.realtimeSinceStartupAsDouble - creditsAt;
                if (creditsInputPhase == 0 && age >= 4 || creditsInputPhase == 1 && age >= 9)
                {
                    PlayerInputManager.Instance.TapSynthetic(1, PlayerAction.Start);
                    Debug.Log("[NormalPlaytest] owner credits Start phase=" + creditsInputPhase++);
                }
                return;
            }
            if (NormalCase != "easy") return;
            if (!offline && (!Mirror.NetworkClient.active || Mirror.NetworkServer.active && !LocalHostPlaytest)) return;
            foreach (var actor in FindObjectsByType<MovementController>())
            {
                if (!actor.CompareTag("Player") || !actor.TryGetComponent<CharacterHealth>(out var health)) continue;
                int id = actor.PlayerId;
                bool owned = offline || (actor.TryGetComponent<NetworkPlayerInput>(out var input) && input.isLocalPlayer);
                if (!observedActors.TryGetValue(id, out var previousActor) || previousActor != actor)
                {
                    observedActors[id] = actor; observedDead.Remove(id); observedRevived.Remove(id);
                    revivedAt.Remove(id); revivedPositions.Remove(id); recoveredMovement.Remove(id);
                }
                var fall = actor.spriteRendererFall;
                bool visibleFall = fall != null && fall.gameObject.activeInHierarchy && fall.TryGetComponent<SpriteRenderer>(out var fallingSprite) && fallingSprite.enabled && !fallingSprite.forceRenderingOff;
                if (health.life == 0 || actor.isDead || visibleFall)
                {
                    observedDead.Add(id); observedRevived.Remove(id);
                    revivedAt.Remove(id); revivedPositions.Remove(id); recoveredMovement.Remove(id);
                }
                else if (!actor.isDead && observedDead.Contains(id) && !observedRevived.Contains(id))
                {
                    observedRevived.Add(id);
                    Debug.Log("[NormalPlaytest] client revived player=" + id + " owned=" + owned + " dead=" + actor.isDead + " physics=" + actor.Rigidbody.simulated);
                    if (owned) { revivedAt[id] = Time.realtimeSinceStartupAsDouble; revivedPositions[id] = actor.transform.position; }
                }
                if (!owned || !revivedAt.TryGetValue(id, out double returnedAt) || PlayerInputManager.Instance == null) continue;
                double age = Time.realtimeSinceStartupAsDouble - returnedAt;
                int slot = offline ? id : 1;
                PlayerInputManager.Instance.SetSyntheticHeld(slot, PlayerAction.MoveRight, age >= 3 && age < 3.3);
                PlayerInputManager.Instance.SetSyntheticHeld(slot, PlayerAction.MoveLeft, age >= 4 && age < 4.3);
                if (!recoveredMovement.Contains(id) && age >= 3 && Vector2.Distance(actor.transform.position, revivedPositions[id]) > .05f)
                { recoveredMovement.Add(id); Debug.Log("[NormalPlaytest] client movement recovered player=" + id); }
            }
        }


        static IEnumerator InspectNativeSubmersion()
        {
            var room = global::StageAssets.World3RoomProgressionController.FindRoomBounds("Room 1");
            LocalEnvironmentSmoke.PlacePlayers(room);
            var actors = new System.Collections.Generic.List<MovementController>();
            foreach (var actor in FindObjectsByType<MovementController>()) if (actor.CompareTag("Player")) actors.Add(actor);
            if (actors.Count != 2) throw new InvalidOperationException("Submersion requires two native players");
            foreach (var actor in actors)
                actor.GetComponent<BombController>().ClearDestructibleForEffect(actor.transform.position, spawnDestructiblePrefab: false, spawnHiddenObject: false);
            for (int phase = 0; phase < 2; phase++)
            {
                foreach (var actor in actors)
                {
                    MountedType mount = phase == 0 ? (actor.PlayerId == 1 ? MountedType.None : MountedType.Blue) : (actor.PlayerId == 1 ? MountedType.Pink : MountedType.None);
                    var companion = actor.GetComponent<PlayerMountCompanion>();
                    companion.ClearMountedStateForForcedArenaRemoval();
                    companion.RestoreMounted(mount);
                    actor.SetSuppressInactivityAnimation(true); actor.CancelPlayerPoses();
                }
                yield return new WaitForSecondsRealtime(2);
                foreach (var actor in actors)
                {
                    int visibleWater = CountVisibleSubmergedSprites(actor);
                    if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                    {
                        var companion = actor.GetComponent<PlayerMountCompanion>();
                        var target = companion.GetMountedLouieObject();
                        if (target != null && (!target.TryGetComponent<PlayerWaterSubmersionEffect>(out var effect) || !effect.enabled))
                            throw new InvalidOperationException("Headless mount lacks the native water effect player=" + actor.PlayerId);
                        // Player root materials initialize in OnStartClient, which
                        // a dedicated process does not run. Real clients must
                        // still expose all four expected player/mount views.
                        Debug.Log("[NormalPlaytest] headless submersion configuration player=" + actor.PlayerId + " mount=" + companion.GetMountedLouieType() + " rootGraphicsInapplicable=True");
                    }
                    else if (visibleWater == 0) throw new InvalidOperationException("Native player/mount has no visible water treatment player=" + actor.PlayerId);
                    Debug.Log("[NormalPlaytest] native submersion phase=" + phase + " player=" + actor.PlayerId + " visible=" + visibleWater);
                }
                CaptureNormalFrame("native-phase-" + phase);
                yield return new WaitForSecondsRealtime(2);
            }
            Debug.Log("[NormalPlaytest] inspection completed case=submerge");
        }

        static int CountVisibleSubmergedSprites(MovementController actor)
        {
            int count = 0;
            foreach (var sprite in actor.GetComponentsInChildren<SpriteRenderer>(true))
                if (sprite.enabled && sprite.gameObject.activeInHierarchy && !sprite.forceRenderingOff && sprite.sprite != null && sprite.sharedMaterial != null &&
                    sprite.sharedMaterial.HasProperty("_WaterSurfaceY")) count++;
            return count;
        }
        readonly System.Collections.Generic.HashSet<string> submersionVisuals = new();
        void ObserveSubmersion()
        {
            foreach (var actor in FindObjectsByType<MovementController>())
                if (actor.CompareTag("Player") && CountVisibleSubmergedSprites(actor) > 0)
                {
                    string key = actor.PlayerId + "-" + actor.GetComponent<PlayerMountCompanion>().GetMountPresentationType();
                    if (submersionVisuals.Add(key))
                    { CaptureNormalFrame("approved-" + key); Debug.Log("[NormalPlaytest] submersion rendered playerMount=" + key + " waterSprites=" + CountVisibleSubmergedSprites(actor)); }
                }
        }

        static IEnumerator InspectNativeWaterBomb()
        {
            const System.Reflection.BindingFlags fields = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var game = FindAnyObjectByType<GameManager>(); var actors = new System.Collections.Generic.List<MovementController>();
            foreach (var actor in FindObjectsByType<MovementController>()) if (actor.CompareTag("Player")) { actors.Add(actor); actor.GetComponent<CharacterHealth>().ResetForRespawn(200); }
            if (game == null || game.groundTilemap == null || actors.Count != 2) throw new InvalidOperationException("Water bomb requires native scene geometry and two players");
            foreach (var actor in actors)
            {
                var source = actor.GetComponent<BombController>();
                var water = (UnityEngine.Tilemaps.Tilemap)typeof(BombController).GetField("waterTiles", fields).GetValue(source);
                if (water == null) throw new InvalidOperationException("Water bomb has no authored water map");
                Vector2? chosenWater = null, chosenGround = null; float nearest = float.PositiveInfinity;
                foreach (var cell in water.cellBounds.allPositionsWithin)
                {
                    if (!water.HasTile(cell)) continue;
                    Vector2 wet = water.GetCellCenterWorld(cell);
                    foreach (var direction in new[] { Vector2.up, Vector2.down, Vector2.left, Vector2.right })
                    {
                        Vector2 dry = wet + direction;
                        if (water.HasTile(water.WorldToCell(dry)) || !game.groundTilemap.HasTile(game.groundTilemap.WorldToCell(dry)) ||
                            game.indestructibleTilemap != null && game.indestructibleTilemap.HasTile(game.indestructibleTilemap.WorldToCell(dry))) continue;
                        float distance = (dry - actor.Rigidbody.position).sqrMagnitude;
                        if (distance < nearest) { nearest = distance; chosenWater = wet; chosenGround = dry; }
                    }
                }
                if (!chosenWater.HasValue || !chosenGround.HasValue) throw new InvalidOperationException("No authored shore for native bomb entry");
                source.ClearDestructibleForEffect(chosenGround.Value, spawnDestructiblePrefab: false, spawnHiddenObject: false);
                actor.Rigidbody.position = chosenGround.Value + Vector2.down * 2; actor.transform.position = actor.Rigidbody.position;
                NetworkPlayerInput.SyncAuthorityMotionTransform(actor);
                if (Mirror.NetworkServer.active) actor.GetComponent<NetworkPlayerInput>().ServerReconcileMovementNow();
                Physics2D.SyncTransforms();
                foreach (var camera in FindObjectsByType<CameraFollowClamp2D>()) camera.ForceSnapNow(refreshPlayersNow: true);
                if (!source.TryPlaceBombAtIgnoringInputLock(chosenGround.Value, out var placed, consumeBomb: false, trackOwnerBombTraversal: false) || placed == null)
                    throw new InvalidOperationException("Native shore bomb placement failed");
                placed.GetComponent<Bomb>().SetFusePaused(true);
                yield return new WaitForSecondsRealtime(1);
                var bomb = placed.GetComponent<Bomb>();
                source.NotifyBombAtHoleWithVisualOffset(chosenWater.Value, placed, 0, refund: false);
                yield return null;
                if (!bomb.SinkVisualActive) throw new InvalidOperationException("Native bomb did not begin the authored water sink");
                Debug.Log("[NormalPlaytest] native bomb entered water player=" + actor.PlayerId + " position=" + chosenWater.Value + " netId=" + bomb.GetComponent<Mirror.NetworkIdentity>()?.netId);
                yield return new WaitForSecondsRealtime(2);
                if (placed != null) throw new InvalidOperationException("Native water sink did not destroy the bomb");
            }
            Debug.Log("[NormalPlaytest] inspection completed case=waterbomb");
        }
        readonly System.Collections.Generic.HashSet<int> sinkingBombVisuals = new();
        void ObserveWaterBomb()
        {
            foreach (var bomb in FindObjectsByType<Bomb>())
                if (bomb.TryGetComponent<SpriteRenderer>(out var sprite) && sprite.enabled && sprite.color.b > sprite.color.r + .05f && bomb.transform.localScale.x < .99f)
                {
                    int id = bomb.GetEntityId().GetHashCode();
                    if (sinkingBombVisuals.Add(id))
                    { CaptureNormalFrame("sink-" + id); Debug.Log("[NormalPlaytest] water bomb sink rendered id=" + id + " scale=" + bomb.transform.localScale + " color=" + sprite.color); }
                }
        }

        static IEnumerator InspectNativeCampaignItems()
        {
            const System.Reflection.BindingFlags fields = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var game = FindAnyObjectByType<GameManager>();
            if (game == null || game.destructibleTilemap == null) throw new InvalidOperationException("Campaign item test requires native terrain");
            var assignments = (System.Collections.Generic.Dictionary<Vector3Int, GameObject>)typeof(GameManager).GetField("hiddenObjectSpawnsByCell", fields).GetValue(game);
            Vector3Int? eggCell = null;
            foreach (var entry in assignments)
                if (entry.Value != null && entry.Value.TryGetComponent<ItemPickup>(out var item))
                {
                    if ((int)item.type >= (int)ItemType.BlueLouieEgg && (int)item.type <= (int)ItemType.RedLouieEgg) throw new InvalidOperationException("Campaign still assigned a colored egg before collection");
                    if (item.type == ItemType.RandomLouieEgg) eggCell = entry.Key;
                }
            if (!eggCell.HasValue) throw new InvalidOperationException("Native Stage2-5 has no generic assigned egg");
            MovementController collector = null;
            foreach (var actor in FindObjectsByType<MovementController>())
                if (actor.CompareTag("Player"))
                {
                    actor.GetComponent<CharacterHealth>().ResetForRespawn(200); actor.GetComponent<CharacterHealth>().SetExternalInvulnerability(true);
                    actor.GetComponent<PlayerMountCompanion>().ClearMountedStateForForcedArenaRemoval();
                    if (actor.PlayerId == 2) collector = actor;
                }
            if (collector == null) throw new InvalidOperationException("Guest item collector missing");
            var source = collector.GetComponent<BombController>();
            var originalTile = game.destructibleTilemap.GetTile(eggCell.Value);
            Vector2 center = game.destructibleTilemap.GetCellCenterWorld(eggCell.Value);
            foreach (var actor in FindObjectsByType<MovementController>())
                if (actor.CompareTag("Player"))
                {
                    Vector2 parking = center + Vector2.down * 3 + Vector2.right * actor.PlayerId;
                    actor.Rigidbody.position = parking; actor.transform.position = parking;
                    NetworkPlayerInput.SyncAuthorityMotionTransform(actor);
                    if (Mirror.NetworkServer.active)
                    {
                        actor.GetComponent<PlayerNetworkTransform>().ServerRoomCut(actor.transform.position, actor.transform.rotation);
                        actor.GetComponent<NetworkPlayerInput>().ServerReconcileMovementNow();
                    }
                }
            source.ClearDestructibleForEffect(center, spawnDestructiblePrefab: true, spawnHiddenObject: true);
            yield return new WaitForSecondsRealtime(2);
            ItemPickup revealed = null;
            foreach (var item in FindObjectsByType<ItemPickup>())
                if (Vector2.Distance(item.transform.position, center) < .4f) { revealed = item; break; }
            if (revealed == null || revealed.type != ItemType.RandomLouieEgg || revealed.GetComponent<SpriteRenderer>().sprite == null)
                throw new InvalidOperationException("Native hidden reveal did not create the visible generic egg");
            if (assignments.ContainsKey(eggCell.Value)) throw new InvalidOperationException("Native reveal retained its one-shot reward assignment");
            foreach (var camera in FindObjectsByType<CameraFollowClamp2D>()) camera.ForceSnapNow(refreshPlayersNow: true);
            Debug.Log("[NormalPlaytest] campaign generic egg revealed cell=" + eggCell.Value + " sprite=" + revealed.GetComponent<SpriteRenderer>().sprite.name);
            yield return new WaitForSecondsRealtime(2);
            collector.Rigidbody.position = center; collector.transform.position = center;
            NetworkPlayerInput.SyncAuthorityMotionTransform(collector);
            if (Mirror.NetworkServer.active)
            {
                collector.GetComponent<PlayerNetworkTransform>().ServerRoomCut(collector.transform.position, collector.transform.rotation);
                collector.GetComponent<NetworkPlayerInput>().ServerReconcileMovementNow();
            }
            Physics2D.SyncTransforms();
            yield return new WaitForSecondsRealtime(3);
            var chosen = collector.GetComponent<PlayerMountCompanion>().GetMountedLouieType();
            if (revealed != null || chosen == MountedType.None || chosen == MountedType.Mole || chosen == MountedType.Tank)
                throw new InvalidOperationException("Native generic egg did not reveal one Louie on guest collection remaining=" + (revealed != null) + " chosen=" + chosen + " position=" + collector.Rigidbody.position + " target=" + center + " collider=" + collector.GetComponent<Collider2D>().enabled + " simulated=" + collector.Rigidbody.simulated);
            Debug.Log("[NormalPlaytest] campaign random egg collected guest=2 mounted=" + chosen);
            collector.Rigidbody.position = center + Vector2.down * 3; collector.transform.position = collector.Rigidbody.position;
            NetworkPlayerInput.SyncAuthorityMotionTransform(collector);
            if (Mirror.NetworkServer.active)
            {
                collector.GetComponent<PlayerNetworkTransform>().ServerRoomCut(collector.transform.position, collector.transform.rotation);
                collector.GetComponent<NetworkPlayerInput>().ServerReconcileMovementNow();
            }
            game.destructibleTilemap.SetTile(eggCell.Value, originalTile);
            yield return new WaitForSecondsRealtime(.5f);
            source.ClearDestructibleForEffect(center, spawnDestructiblePrefab: true, spawnHiddenObject: true);
            yield return new WaitForSecondsRealtime(2);
            foreach (var item in FindObjectsByType<ItemPickup>())
                if (Vector2.Distance(item.transform.position, center) < .4f) throw new InvalidOperationException("Consumed hidden reward returned after the same cell regenerated");
            Debug.Log("[NormalPlaytest] hidden reward one-shot verified cell=" + eggCell.Value);
            Debug.Log("[NormalPlaytest] inspection completed case=items");
        }
        bool genericEggObserved, randomMountObserved;
        void ObserveCampaignItems()
        {
            if (!genericEggObserved)
                foreach (var item in FindObjectsByType<ItemPickup>())
                    if (item.type == ItemType.RandomLouieEgg && item.TryGetComponent<SpriteRenderer>(out var sprite) && sprite.enabled && sprite.sprite != null && sprite.color.a > .9f)
                    { genericEggObserved = true; CaptureNormalFrame("generic-egg"); Debug.Log("[NormalPlaytest] generic egg rendered sprite=" + sprite.sprite.name); break; }
            if (!genericEggObserved || randomMountObserved) return;
            foreach (var actor in FindObjectsByType<MovementController>())
                if (actor.CompareTag("Player") && actor.PlayerId == 2 && actor.GetComponent<PlayerMountCompanion>().GetMountPresentationType() != MountedType.None)
                { randomMountObserved = true; CaptureNormalFrame("collected-louie"); Debug.Log("[NormalPlaytest] collected Louie rendered guest=2 type=" + actor.GetComponent<PlayerMountCompanion>().GetMountPresentationType()); }
        }

        static IEnumerator InspectNativeBlinkOwnership()
        {
            const System.Reflection.BindingFlags fields = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            AttachedCoreMechanismsController attachment = null;
            foreach (var candidate in FindObjectsByType<AttachedCoreMechanismsController>(FindObjectsInactive.Include))
                if (candidate.host != null && candidate.attachedRenderer != null) { attachment = candidate; break; }
            if (attachment == null) throw new InvalidOperationException("Native carrier/core attachment not found");
            var room = global::StageAssets.World3RoomProgressionController.FindRoomBoundsContaining(attachment.host.transform.position);
            LocalEnvironmentSmoke.PlacePlayers(room, attachment.host.transform.position + Vector3.down * 3);
            yield return new WaitForSecondsRealtime(.5f);
            var health = attachment.host.GetComponent<CharacterHealth>();
            if (health == null) throw new InvalidOperationException("Native carrier has no health");
            health.ResetForRespawn(4); health.SetExternalInvulnerability(false); health.hitInvulnerableDuration = 3;
            LocalProjectileSmoke.Track(attachment.attachedRenderer, "BlinkCore");
            foreach (var sprite in attachment.host.GetComponentsInChildren<SpriteRenderer>(true))
                if (sprite.GetComponentInParent<AttachedCoreMechanismsController>(true) == null)
                    LocalProjectileSmoke.Track(sprite, "BlinkCarrier");
            health.TakeDamage(1, fromExplosion: true);
            float minimumCoreAlpha = 1; bool enemyBlinkSeen = false;
            double deadline = Time.realtimeSinceStartupAsDouble + 3;
            while (Time.realtimeSinceStartupAsDouble < deadline)
            {
                minimumCoreAlpha = Mathf.Min(minimumCoreAlpha, attachment.attachedRenderer.color.a);
                foreach (var sprite in attachment.host.GetComponentsInChildren<SpriteRenderer>(true))
                    if (sprite.enabled && sprite.gameObject.activeInHierarchy && sprite.GetComponentInParent<AttachedCoreMechanismsController>(true) == null && sprite.color.a < .5f) enemyBlinkSeen = true;
                yield return null;
            }
            if (!enemyBlinkSeen || minimumCoreAlpha < .99f) throw new InvalidOperationException("Native carrier blink affected the objective or never blinked");
            Debug.Log("[NormalPlaytest] native carrier blink isolated coreMinimumAlpha=" + minimumCoreAlpha);
            var room3 = global::StageAssets.World3RoomProgressionController.FindRoomBounds("Room 3");
            LocalEnvironmentSmoke.PlacePlayers(room3);
            yield return new WaitForSecondsRealtime(.5f);
            GurunMovementController gurun = null;
            foreach (var candidate in FindObjectsByType<GurunMovementController>())
                if (room3 != null && room3.OverlapPoint(candidate.transform.position)) { gurun = candidate; break; }
            if (gurun == null || gurun.rocketLeft == null) throw new InvalidOperationException("Native Room3 Gurun not found");
            var gurunHealth = gurun.GetComponent<CharacterHealth>();
            gurunHealth.ResetForRespawn(5); gurunHealth.SetExternalInvulnerability(false);
            gurunHealth.StartTemporaryInvulnerability(3, withBlink: true);
            var template = gurun.rocketLeft.GetComponent<SpriteRenderer>();
            deadline = Time.realtimeSinceStartupAsDouble + 2;
            while (template.color.a > .5f && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            if (template.color.a > .5f) throw new InvalidOperationException("Gurun projectile template never entered the source blink");
            typeof(GurunMovementController).GetField("attackDirection", fields).SetValue(gurun, Vector2.right);
            typeof(GurunMovementController).GetMethod("LaunchRocket", fields).Invoke(gurun, null);
            BirdRobotRocket rocket = null;
            foreach (var candidate in FindObjectsByType<BirdRobotRocket>())
                if (candidate.name == "GurunRocket" && Vector2.Distance(candidate.transform.position, gurun.transform.position) < .1f) { rocket = candidate; break; }
            if (rocket == null || rocket.GetComponent<SpriteRenderer>().color.a < .99f) throw new InvalidOperationException("Native Gurun shot inherited the invisible blink phase");
            LocalProjectileSmoke.Track(rocket.GetComponent<SpriteRenderer>(), "BlinkRocket");
            Debug.Log("[NormalPlaytest] native Gurun shot while blinking sourceAlpha=" + template.color.a + " shotAlpha=" + rocket.GetComponent<SpriteRenderer>().color.a);
            for (int i = 0; i < 10; i++) { CaptureNormalFrame("rocket-" + i); yield return new WaitForSecondsRealtime(.05f); }
            yield return new WaitForSecondsRealtime(2);
            Debug.Log("[NormalPlaytest] inspection completed case=blink");
        }

        // Development-only perceptual probes: run after all production presenters.
        IEnumerator ObserveNativeFinalFrames()
        {
            var eof = new WaitForEndOfFrame();
            var bounceSamples = new System.Collections.Generic.Dictionary<int, int>();
            var nativeBounce = typeof(PlayerPushedOutOfInvalidTile).GetField("_resolveRoutine", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var flameCells = new System.Collections.Generic.HashSet<Vector3Int>();
            var flameSprites = new System.Collections.Generic.HashSet<Sprite>();
            double nextWaterSample = 0;
            var prefab = Resources.Load<BombExplosion>("Explosions/BombExplosion");
            if (NormalCase == "geyser" && prefab != null)
                foreach (var animation in prefab.GetComponentsInChildren<AnimatedSpriteRenderer>(true))
                {
                    if (animation.idleSprite != null) flameSprites.Add(animation.idleSprite);
                    if (animation.animationSprite != null)
                        foreach (var sprite in animation.animationSprite) if (sprite != null) flameSprites.Add(sprite);
                }
            while (true)
            {
                yield return eof;
                if (NormalCase == "cart")
                    foreach (var actor in FindObjectsByType<MovementController>())
                    {
                        if (!actor.CompareTag("Player") || !actor.TryGetComponent<NetworkBombFx>(out var fx)) continue;
                        bool native = NetSync.ShouldSimulateLocally && actor.TryGetComponent<PlayerPushedOutOfInvalidTile>(out var resolver) && nativeBounce.GetValue(resolver) != null;
                        if (!fx.PlayerBounceActive && !native) continue;
                        bounceSamples.TryGetValue(actor.PlayerId, out int count);
                        bounceSamples[actor.PlayerId] = ++count;
                        Debug.Log(FormattableString.Invariant($"[NormalPlaytest] final bounce player={actor.PlayerId} x={actor.transform.position.x:F4} y={actor.transform.position.y:F4} owned={actor.TryGetComponent<NetworkPlayerInput>(out var input) && input.isLocalPlayer}"));
                        if (count <= 20) CaptureNormalFrame("final-bounce-p" + actor.PlayerId + "-" + count);
                    }
                if (NormalCase == "water" && Time.realtimeSinceStartupAsDouble >= nextWaterSample)
                {
                    nextWaterSample = Time.realtimeSinceStartupAsDouble + .05;
                    foreach (var actor in FindObjectsByType<MovementController>())
                        if (actor.CompareTag("Player") && actor.TryGetComponent<NetworkPlayerInput>(out var input))
                            Debug.Log(FormattableString.Invariant($"[NormalPlaytest] final water player={actor.PlayerId} x={actor.transform.position.x:F5} y={actor.transform.position.y:F5} pt={input.AuthorityPresentationTime:F6} nt={Mirror.NetworkTime.time:F6} rt={Time.realtimeSinceStartupAsDouble:F6} owned={input.isLocalPlayer}"));
                }
                if (NormalCase != "geyser") continue;
                var game = FindAnyObjectByType<GameManager>();
                if (game == null || game.groundTilemap == null) continue;
                foreach (var renderer in FindObjectsByType<SpriteRenderer>())
                {
                    if (!renderer.enabled || !renderer.gameObject.activeInHierarchy || renderer.forceRenderingOff || renderer.color.a <= .01f || !flameSprites.Contains(renderer.sprite)) continue;
                    Vector3 position = renderer.transform.position;
                    // This authored fixture uses the Room2 geyser (23,-1), with
                    // bombs below it. The native setup verifies that same target.
                    if (Mathf.Abs(position.x - 23) > .25f || position.y < -3.25f || position.y > .25f) continue;
                    if (position.y > -1.25f) throw new InvalidOperationException("Final rendered flame crossed the authored geyser barrier at=" + position);
                    var cell = game.groundTilemap.WorldToCell(position);
                    if (flameCells.Add(cell))
                    {
                        Debug.Log("[NormalPlaytest] final geyser flame sprite=" + renderer.sprite.name + " position=" + position);
                        CaptureNormalFrame("final-geyser-flame-" + flameCells.Count);
                    }
                }
            }
        }


        static IEnumerator InspectNativeGeyserBarrier()
        {
            var game = FindAnyObjectByType<GameManager>();
            if (game == null || game.groundTilemap == null) throw new InvalidOperationException("Native geyser fixture requires ground geometry");
            GeyserLauncherBootstrap chosen = null; Vector2 direction = default, sourcePosition = default;
            foreach (var geyser in FindObjectsByType<GeyserLauncherBootstrap>(FindObjectsInactive.Include))
            {
                Vector2 center = geyser.transform.position;
                if (Vector2.Distance(center, new Vector2(23, -1)) > .01f) continue;
                foreach (var candidate in new[] { Vector2.up })
                {
                    bool clear = true;
                    for (int distance = 1; distance <= 2; distance++)
                    {
                        Vector2 p = center - candidate * distance;
                        if (!game.groundTilemap.HasTile(game.groundTilemap.WorldToCell(p)) ||
                            game.indestructibleTilemap != null && game.indestructibleTilemap.HasTile(game.indestructibleTilemap.WorldToCell(p))) clear = false;
                    }
                    if (!clear) continue;
                    chosen = geyser; direction = candidate; sourcePosition = center - candidate * 2; break;
                }
                if (chosen != null) break;
            }
            if (chosen == null) throw new InvalidOperationException("No authored geyser with a two-cell bomb approach");
            var room = global::StageAssets.World3RoomProgressionController.FindRoomBoundsContaining(chosen.transform.position);
            LocalEnvironmentSmoke.PlacePlayers(room, sourcePosition + new Vector2(-direction.y, direction.x) * 3);
            var actors = new System.Collections.Generic.List<MovementController>();
            foreach (var actor in FindObjectsByType<MovementController>()) if (actor.CompareTag("Player")) actors.Add(actor);
            foreach (var actor in actors)
            {
                var source = actor.GetComponent<BombController>(); source.explosionRadius = 4;
                source.ClearDestructibleForEffect(sourcePosition, spawnDestructiblePrefab: false, spawnHiddenObject: false);
                source.ClearDestructibleForEffect(sourcePosition + direction, spawnDestructiblePrefab: false, spawnHiddenObject: false);
                yield return new WaitForSecondsRealtime(.5f);
                if (!source.TryPlaceBombAtIgnoringInputLock(sourcePosition, out var placed, consumeBomb: false, trackOwnerBombTraversal: false) || placed == null)
                    throw new InvalidOperationException("Native geyser test bomb placement failed");
                yield return new WaitForSecondsRealtime(.5f);
                source.ExplodeBomb(placed);
                yield return null;
                bool approached = false;
                foreach (var flame in FindObjectsByType<BombExplosion>())
                {
                    Vector2 p = flame.transform.position;
                    if (Vector2.Distance(p, sourcePosition + direction) < .25f) approached = true;
                    if (Vector2.Distance(p, chosen.transform.position) < .25f || Vector2.Distance(p, (Vector2)chosen.transform.position + direction) < .25f)
                        throw new InvalidOperationException("Native explosion crossed the geyser barrier");
                }
                if (!approached) throw new InvalidOperationException("Explosion never reached the geyser's adjacent cell; invalid fixture");
                Debug.Log("[NormalPlaytest] geyser blocked actual explosion player=" + actor.PlayerId + " center=" + chosen.transform.position + " source=" + sourcePosition);
                CaptureNormalFrame("native-player-" + actor.PlayerId);
                yield return new WaitForSecondsRealtime(2);
            }
            if (Vector2.Distance(chosen.transform.position, new Vector2(23, -1)) > .01f) throw new InvalidOperationException("Geyser fixture selected a different authored barrier");
            Debug.Log("[NormalPlaytest] inspection completed case=geyser");
        }
        readonly System.Collections.Generic.HashSet<string> geyserFlamePositions = new();
        void ObserveGeyserBarrier()
        {
            foreach (var flame in FindObjectsByType<BombExplosion>())
                foreach (var geyser in FindObjectsByType<GeyserLauncherBootstrap>(FindObjectsInactive.Include))
                {
                    float distance = Vector2.Distance(flame.transform.position, geyser.transform.position);
                    if (distance < .25f) throw new InvalidOperationException("Rendered explosion entered the geyser barrier cell");
                    if (distance > 1.25f) continue;
                    string key = flame.transform.position.ToString();
                    if (geyserFlamePositions.Add(key))
                    { CaptureNormalFrame("approved-flame-" + geyserFlamePositions.Count); Debug.Log("[NormalPlaytest] approved flame stopped adjacent to geyser at=" + key); }
                }
        }

        static IEnumerator InspectNativeBossItems()
        {
            const System.Reflection.BindingFlags fields = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            foreach (var actor in FindObjectsByType<MovementController>())
                if (actor.CompareTag("Player")) { actor.GetComponent<CharacterHealth>().ResetForRespawn(200); actor.GetComponent<CharacterHealth>().SetExternalInvulnerability(true); }
            string scene = SceneManager.GetActiveScene().name;
            if (scene == global::StageAssets.World3HallPortalSelectionController.SceneName)
            {
                var portal = FindAnyObjectByType<global::StageAssets.World3HallPortalSelectionController>();
                typeof(global::StageAssets.World3HallPortalSelectionController).GetMethod("BeginHallBattle", fields).Invoke(portal, new object[] { false });
                double deadline = Time.realtimeSinceStartupAsDouble + 70;
                while (FindAnyObjectByType<PrettyBomberDefeatSequence>() == null && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                var pretty = FindAnyObjectByType<PrettyBomberDefeatSequence>();
                if (pretty == null) throw new InvalidOperationException("Native Hall Pretty encounter did not enter");
                var sequence = FindAnyObjectByType<global::StageAssets.World3HallStageSevenSequence>();
                while (!sequence.DuelActive && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                if (!sequence.DuelActive) throw new InvalidOperationException("Native Pretty duel did not become active");
                var health = pretty.GetComponent<CharacterHealth>(); health.life = 2; health.SetExternalInvulnerability(false); health.TakeDamage(1, fromExplosion: true);
                yield return WaitForVisibleNativeReward(ItemType.SearchBomb, 15);
            }
            else if (scene == "Stage_2-6")
            {
                var escape = FindAnyObjectByType<BossEscapeOnLastLife>();
                if (escape == null) throw new InvalidOperationException("Native Magnet escape component not found");
                while (StageIntroTransition.Instance != null && StageIntroTransition.Instance.IntroRunning) yield return null;
                var health = escape.GetComponent<CharacterHealth>(); health.life = 2; health.SetExternalInvulnerability(false); health.TakeDamage(1, fromExplosion: true);
                yield return WaitForVisibleNativeReward(ItemType.MagnetBomb, 15);
            }
            else if (scene == "Stage_1-6")
            {
                var sequence = FindAnyObjectByType<MechaBossSequence>();
                if (sequence == null) throw new InvalidOperationException("Native Mecha sequence not found");
                double deadline = Time.realtimeSinceStartupAsDouble + 60;
                while (MechaBossSequence.MechaIntroRunning && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                if (MechaBossSequence.MechaIntroRunning) throw new InvalidOperationException("Mecha intro never released combat");
                int before = FindObjectsByType<ItemPickup>().Length;
                typeof(MechaBossSequence).GetMethod("TrySpawnItem", fields).Invoke(sequence, null);
                yield return new WaitForSecondsRealtime(1);
                if (FindObjectsByType<ItemPickup>().Length <= before) throw new InvalidOperationException("Native Mecha random item factory did not spawn");
                Debug.Log("[NormalPlaytest] native Mecha regular item factory spawned");
                foreach (var mecha in new[] { sequence.whiteMecha, sequence.blackMecha, sequence.goldenMecha })
                {
                    deadline = Time.realtimeSinceStartupAsDouble + 45;
                    while ((mecha == null || !mecha.gameObject.activeInHierarchy || MechaBossSequence.MechaIntroRunning) && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                    if (mecha == null || !mecha.gameObject.activeInHierarchy || MechaBossSequence.MechaIntroRunning) throw new InvalidOperationException("Native Mecha combat phase unavailable");
                    var health = mecha.GetComponent<CharacterHealth>(); health.SetExternalInvulnerability(false); health.TakeDamage(9999, fromExplosion: true);
                    deadline = Time.realtimeSinceStartupAsDouble + 12;
                    while (!mecha.isDead && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                    if (!mecha.isDead) throw new InvalidOperationException("Native Mecha damage did not finish its phase");
                    yield return new WaitForSecondsRealtime(.5f);
                }
                yield return WaitForVisibleNativeReward(ItemType.BombKick, 15);
            }
            else throw new InvalidOperationException("Unsupported native boss item scene");
            yield return new WaitForSecondsRealtime(3);
            Debug.Log("[NormalPlaytest] inspection completed case=bossitems");
        }
        static IEnumerator WaitForVisibleNativeReward(ItemType expected, float seconds)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + seconds; ItemPickup found = null;
            while (Time.realtimeSinceStartupAsDouble < deadline)
            {
                foreach (var item in FindObjectsByType<ItemPickup>()) if (item.type == expected) { found = item; break; }
                if (found != null) break;
                yield return null;
            }
            if (found == null || !found.TryGetComponent<SpriteRenderer>(out var sprite) || sprite.sprite == null)
                throw new InvalidOperationException("Native boss reward missing or invisible: " + expected);
            if (Mirror.NetworkServer.active && (!found.TryGetComponent<Mirror.NetworkIdentity>(out var identity) || identity.netId == 0))
                throw new InvalidOperationException("Native boss reward is not spawned on the authority: " + expected);
            Debug.Log("[NormalPlaytest] native boss reward visible type=" + expected + " position=" + found.transform.position);
        }
        readonly System.Collections.Generic.HashSet<int> visibleBossItems = new();
        void ObserveBossItems()
        {
            foreach (var item in FindObjectsByType<ItemPickup>())
                if (item.TryGetComponent<SpriteRenderer>(out var sprite) && sprite.enabled && sprite.gameObject.activeInHierarchy && sprite.sprite != null && sprite.color.a > .9f && visibleBossItems.Add(item.GetEntityId().GetHashCode()))
                { CaptureNormalFrame("visible-" + item.type + "-" + visibleBossItems.Count); Debug.Log("[NormalPlaytest] boss item rendered type=" + item.type + " position=" + item.transform.position); }
        }

        double nextRenderOutputProbe;
        void ObserveRenderOutput()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null || Time.realtimeSinceStartupAsDouble < nextRenderOutputProbe) return;
            nextRenderOutputProbe = Time.realtimeSinceStartupAsDouble + 8;
            Debug.Log("[NormalPlaytest] render output width=" + Screen.width + " height=" + Screen.height + " mode=" + Screen.fullScreenMode + " focused=" + Application.isFocused);
        }

        readonly System.Collections.Generic.HashSet<string> campaignRadiusSeen = new();
        double nextCampaignRadiusProbe;
        void ObserveCampaignRadius()
        {
            if (Time.realtimeSinceStartupAsDouble < nextCampaignRadiusProbe) return;
            nextCampaignRadiusProbe = Time.realtimeSinceStartupAsDouble + 1;
            string scene = SceneManager.GetActiveScene().name;
            if (!scene.StartsWith("Stage_", StringComparison.Ordinal) || Time.timeSinceLevelLoad < 12) return;
            foreach (var actor in FindObjectsByType<MovementController>())
                if (actor.CompareTag("Player") && actor.TryGetComponent<BombController>(out var source))
                {
                    string key = scene + "/" + actor.PlayerId;
                    if (!campaignRadiusSeen.Add(key)) continue;
                    Debug.Log("[NormalPlaytest] campaign radius scene=" + scene + " player=" + actor.PlayerId + " radius=" + source.explosionRadius);
                    if (scene != "Stage_1-1" && scene != "Stage_World3Hall" && source.explosionRadius < 2)
                        throw new InvalidOperationException("Campaign radius remained one after Stage1-1");
                }
        }
        void OnDisable()
        {
            UnlockProgress.OnBattleModeStageUnlocked -= ObserveNativeStageUnlockEvent;
            if (PlayerInputManager.Instance == null) return;
            PlayerInputManager.Instance.ClearSyntheticPlayer(1);
            if (offline) PlayerInputManager.Instance.ClearSyntheticPlayer(2);
        }
    }
}
#endif

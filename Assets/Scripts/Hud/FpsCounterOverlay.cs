using System;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public sealed class FpsCounterOverlay : MonoBehaviour
{
    const float RefreshIntervalSeconds = 0.25f;
    const string OverlayObjectName = "FpsCounterOverlay";

    static FpsCounterOverlay instance;

    bool isVisible;
    int sampledFrames;
    float sampledSeconds;
    string displayedText = string.Empty;
    GUIStyle labelStyle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null)
            return;

        GameObject go = new GameObject(OverlayObjectName);
        instance = go.AddComponent<FpsCounterOverlay>();
        DontDestroyOnLoad(go);
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Update()
    {
        HandleToggleShortcut();

        if (!isVisible)
            return;

        sampledFrames++;
        sampledSeconds += Time.unscaledDeltaTime;

        if (sampledSeconds < RefreshIntervalSeconds)
            return;

        float framesPerSecond = sampledSeconds > 0f ? sampledFrames / sampledSeconds : 0f;
        displayedText = $"FPS: {framesPerSecond:0.0}";
        sampledFrames = 0;
        sampledSeconds = 0f;
    }

    void OnGUI()
    {
        if (!isVisible)
            return;

        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
        }

        GUI.Box(new Rect(10f, 10f, 112f, 34f), displayedText, labelStyle);
    }

    void HandleToggleShortcut()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.fKey.wasPressedThisFrame)
            return;

        bool controlHeld = keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed;
        bool shiftHeld = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
        if (!controlHeld || shiftHeld)
            return;

        SetVisible(!isVisible);
    }

    public static void SetOverlayVisible(bool visible)
    {
        if (instance != null)
            instance.SetVisible(visible);
    }

    void SetVisible(bool visible)
    {
        isVisible = visible;
        sampledFrames = 0;
        sampledSeconds = 0f;
        displayedText = isVisible ? "FPS: ..." : string.Empty;
    }
}

public static class BattleModePerformanceMarkers
{
    public const string PlayerUpdateName = "SB6.BattlePerf.Player.Update";
    public const string PlayerFixedUpdateName = "SB6.BattlePerf.Player.FixedUpdate";
    public const string BombControllerUpdateName = "SB6.BattlePerf.BombController.Update";
    public const string AnimatedSpriteUpdateName = "SB6.BattlePerf.AnimatedSprite.Update";
    public const string BattleHudLateUpdateName = "SB6.BattlePerf.Hud.LateUpdate";
    public const string HudBackgroundLateUpdateName = "SB6.BattlePerf.Hud.Background";
    public const string HudGridLateUpdateName = "SB6.BattlePerf.Hud.Grid";
    public const string HudPortraitLateUpdateName = "SB6.BattlePerf.Hud.Portrait";
    public const string HudStatsLateUpdateName = "SB6.BattlePerf.Hud.Stats";
    public const string HudPushStartLateUpdateName = "SB6.BattlePerf.Hud.PushStart";
    public const string HudLifePreviewLateUpdateName = "SB6.BattlePerf.Hud.LifePreview";
    public const string ArenaUpdateName = "SB6.BattlePerf.Arena.Update";
    public const string InputUpdateName = "SB6.BattlePerf.Input.Update";
    public const string AbilityUpdateName = "SB6.BattlePerf.Ability.Update";
    public const string PlayerAuxUpdateName = "SB6.BattlePerf.PlayerAux.Update";
    public const string EggQueueUpdateName = "SB6.BattlePerf.EggQueue.LateUpdate";
    public const string MountCompanionUpdateName = "SB6.BattlePerf.MountCompanion.Update";
    public const string PlayerStateAnimationUpdateName = "SB6.BattlePerf.PlayerStateAnimation.Update";
    public const string InactivityAnimationUpdateName = "SB6.BattlePerf.InactivityAnimation.Update";
    public const string CorneredAnimationUpdateName = "SB6.BattlePerf.CorneredAnimation.Update";
    public const string ComUpdateName = "SB6.BattlePerf.COM.Update";
    public const string ComThinkName = "SB6.BattlePerf.COM.Think";

    public static readonly ProfilerMarker PlayerUpdate = new(PlayerUpdateName);
    public static readonly ProfilerMarker PlayerFixedUpdate = new(PlayerFixedUpdateName);
    public static readonly ProfilerMarker BombControllerUpdate = new(BombControllerUpdateName);
    public static readonly ProfilerMarker AnimatedSpriteUpdate = new(AnimatedSpriteUpdateName);
    public static readonly ProfilerMarker BattleHudLateUpdate = new(BattleHudLateUpdateName);
    public static readonly ProfilerMarker HudBackgroundLateUpdate = new(HudBackgroundLateUpdateName);
    public static readonly ProfilerMarker HudGridLateUpdate = new(HudGridLateUpdateName);
    public static readonly ProfilerMarker HudPortraitLateUpdate = new(HudPortraitLateUpdateName);
    public static readonly ProfilerMarker HudStatsLateUpdate = new(HudStatsLateUpdateName);
    public static readonly ProfilerMarker HudPushStartLateUpdate = new(HudPushStartLateUpdateName);
    public static readonly ProfilerMarker HudLifePreviewLateUpdate = new(HudLifePreviewLateUpdateName);
    public static readonly ProfilerMarker ArenaUpdate = new(ArenaUpdateName);
    public static readonly ProfilerMarker InputUpdate = new(InputUpdateName);
    public static readonly ProfilerMarker AbilityUpdate = new(AbilityUpdateName);
    public static readonly ProfilerMarker PlayerAuxUpdate = new(PlayerAuxUpdateName);
    public static readonly ProfilerMarker EggQueueUpdate = new(EggQueueUpdateName);
    public static readonly ProfilerMarker MountCompanionUpdate = new(MountCompanionUpdateName);
    public static readonly ProfilerMarker PlayerStateAnimationUpdate = new(PlayerStateAnimationUpdateName);
    public static readonly ProfilerMarker InactivityAnimationUpdate = new(InactivityAnimationUpdateName);
    public static readonly ProfilerMarker CorneredAnimationUpdate = new(CorneredAnimationUpdateName);
    public static readonly ProfilerMarker ComUpdate = new(ComUpdateName);
    public static readonly ProfilerMarker ComThink = new(ComThinkName);

    public static void EnsureInitialized()
    {
    }
}

[DefaultExecutionOrder(-32000)]
public sealed class BattleModePerformanceDiagnostics : MonoBehaviour
{
    const float ReportIntervalSeconds = 2f;
    static BattleModePerformanceDiagnostics instance;
    readonly GameplayFrameStatistics statistics = new();
    readonly GameplayPerformanceCapture.Frame[] window = new GameplayPerformanceCapture.Frame[GameplayFrameStatistics.Capacity];
    readonly GameplayPerformanceCapture.Frame[] worst = new GameplayPerformanceCapture.Frame[3];
    readonly System.Collections.Generic.List<PlayerIdentity> players = new(6);
    readonly System.Text.StringBuilder report = new(8192);
    Metric[] metrics;
    int windowCount;
    int startFrame;
    int lastCollectedFrame;
    double previousUpdateTime;
    double elapsedSeconds;
    bool isCapturing;
    bool applicationPaused;
    bool editorPaused = false;
    string activeScene;
    GameplayPerformanceInterruption pendingInterruption;
    double interruptionDuration;
    GameplayPerformanceInterruption interruptionReasons;

    sealed class Metric
    {
        public readonly string Name;
        readonly ProfilerCategory category;
        readonly string marker;
        readonly double scale;
        ProfilerRecorder recorder;

        public Metric(string name, ProfilerCategory category, string marker, double scale = 0.000001d)
        {
            Name = name;
            this.category = category;
            this.marker = marker;
            this.scale = scale;
        }

        public void Start()
        {
            recorder = ProfilerRecorder.StartNew(category, marker, 1,
                ProfilerRecorderOptions.StartImmediately |
                ProfilerRecorderOptions.WrapAroundWhenCapacityReached |
                ProfilerRecorderOptions.SumAllSamplesInFrame);
        }

        public double ReadCompletedFrame()
        {
            if (!recorder.Valid || !recorder.IsRunning)
                return double.NaN;
            if (recorder.Count == 0)
                return marker.StartsWith("SB6.", StringComparison.Ordinal) ? 0d : double.NaN;
            // Resetting here interrupts Main Thread (already open for this frame)
            // and discards FixedUpdate work collected before this Update. Keep the
            // recorder running; SumAllSamplesInFrame supplies completed frame sums.
            double value = recorder.LastValue * scale;
            return marker == "Main Thread" && value <= 0 ? double.NaN : value;
        }

        public void Dispose()
        {
            recorder.Dispose();
            recorder = default;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null)
            return;
        var go = new GameObject("BattleModePerformanceDiagnostics");
        instance = go.AddComponent<BattleModePerformanceDiagnostics>();
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
        activeScene = SceneManager.GetActiveScene().name;
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.pauseStateChanged += OnEditorPauseChanged;
#endif
    }

    void OnDestroy()
    {
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.pauseStateChanged -= OnEditorPauseChanged;
#endif
        if (instance != this)
            return;
        GameplayPerformanceCapture.Stop();
        DisposeRecorders();
        instance = null;
    }

    void OnActiveSceneChanged(Scene previous, Scene next)
    {
        activeScene = next.name;
        MarkInterruption(GameplayPerformanceInterruption.SceneChange);
    }

    void OnApplicationFocus(bool focused)
    {
        // Both departure and return can affect the interval since the last Update.
        MarkInterruption(GameplayPerformanceInterruption.FocusLost);
    }

    void OnApplicationPause(bool paused)
    {
        applicationPaused = paused;
        MarkInterruption(GameplayPerformanceInterruption.ApplicationPause);
    }

#if UNITY_EDITOR
    void OnEditorPauseChanged(UnityEditor.PauseState state)
    {
        editorPaused = state == UnityEditor.PauseState.Paused;
        MarkInterruption(GameplayPerformanceInterruption.EditorPause);
    }
#endif

    void MarkInterruption(GameplayPerformanceInterruption reason)
    {
        if (!isCapturing)
            return;
        pendingInterruption |= reason;
        GameplayPerformanceCapture.GetFrame(Time.frameCount).Interruption |= reason;
    }

    GameplayPerformanceInterruption CurrentInterruption()
    {
        var reason = GameplayPerformanceInterruption.None;
        if (!Application.isFocused) reason |= GameplayPerformanceInterruption.FocusLost;
        if (applicationPaused) reason |= GameplayPerformanceInterruption.ApplicationPause;
        if (editorPaused) reason |= GameplayPerformanceInterruption.EditorPause;
        if (Time.timeScale == 0f) reason |= GameplayPerformanceInterruption.GamePaused;
        if (!IsGameplayDiagnosticsScene(activeScene)) reason |= GameplayPerformanceInterruption.OutsideGameplay;
        return reason;
    }

    void Update()
    {
        HandleShortcut();
        HandleBattleTimeUpShortcut();
        if (!isCapturing)
            return;
        using (GameplayPerformanceCapture.Measure(GameplayPerformancePhase.Capture))
            CollectFrameSample();
        if (elapsedSeconds >= ReportIntervalSeconds || windowCount == window.Length)
            LogReport();
    }

    void LateUpdate()
    {
        if (!isCapturing)
            return;
        using var capture = GameplayPerformanceCapture.Measure(GameplayPerformancePhase.Capture);
        var frame = GameplayPerformanceCapture.GetFrame(Time.frameCount);
        frame.HasState = true;
        frame.Scene = activeScene;
        frame.TimeScale = Time.timeScale;
        frame.Interruption |= CurrentInterruption();
        frame.Bombs = Bomb.ActiveBombs.Count;
        PlayerIdentity.GetActivePlayers(players);
        int playerMask = 0;
        for (int i = 0; i < players.Count; i++)
        {
            PlayerIdentity player = players[i];
            if (player == null || !player.CompareTag("Player") ||
                player.playerId < GameSession.MinPlayerId || player.playerId > GameSession.MaxPlayerId)
                continue;
            int bit = 1 << player.playerId;
            if ((playerMask & bit) != 0)
                continue;
            playerMask |= bit;
            frame.Players++;
            if (player.TryGetComponent<BattleModeComController>(out var com) && com.isActiveAndEnabled)
                frame.ComPlayers++;
            if (player.TryGetComponent<PlayerMountCompanion>(out var companion) && companion.HasMountedLouie())
                frame.Mounted++;
            if (player.TryGetComponent<MountEggQueue>(out var eggs))
                frame.Eggs += eggs.Count;
        }
    }

    void HandleShortcut()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.fKey.wasPressedThisFrame)
            return;

        bool controlHeld = keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed;
        bool shiftHeld = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
        if (!controlHeld || !shiftHeld)
            return;

        if (isCapturing)
            StopCapture();
        else
            StartCapture();
    }

    void HandleBattleTimeUpShortcut()
    {
        if (!isCapturing || !IsBattleModeScene())
            return;

        if (!AnyPlayerPressedActionLAndR())
            return;

        GameManager manager = GameManager.Instance != null
            ? GameManager.Instance
            : FindAnyObjectByType<GameManager>();

        if (manager == null)
            return;

        Debug.Log("[BattlePerf] DEBUG TIME UP acionado por L+R");
        manager.DebugTriggerBattleTimeUp();
    }

    static bool AnyPlayerPressedActionLAndR()
    {
        PlayerInputManager input = PlayerInputManager.Instance;
        if (input == null)
            return false;

        for (int playerId = GameSession.MinPlayerId; playerId <= GameSession.MaxPlayerId; playerId++)
        {
            if (GameSession.Instance != null && !GameSession.Instance.IsPlayerActive(playerId))
                continue;

            bool lHeld = input.Get(playerId, PlayerAction.ActionL);
            bool rHeld = input.Get(playerId, PlayerAction.ActionR);
            bool lDown = input.GetDown(playerId, PlayerAction.ActionL);
            bool rDown = input.GetDown(playerId, PlayerAction.ActionR);

            if ((lHeld && rDown) || (rHeld && lDown))
                return true;
        }

        return false;
    }


    void StartCapture()
    {
        DisposeRecorders();
        BattleModePerformanceMarkers.EnsureInitialized();
        metrics ??= CreateMetrics();
        GameplayPerformanceCapture.Start();
        foreach (Metric metric in metrics)
            metric.Start();
        ResetWindow();
        activeScene = SceneManager.GetActiveScene().name;
        pendingInterruption = GameplayPerformanceInterruption.None;
        interruptionDuration = 0;
        interruptionReasons = GameplayPerformanceInterruption.None;
        startFrame = Time.frameCount;
        lastCollectedFrame = startFrame - 1;
        isCapturing = true;
        FpsCounterOverlay.SetOverlayVisible(true);
        Debug.Log($"[BattlePerf] CAPTURA INICIADA | scene={activeScene} mode={Mode(activeScene)} " +
            $"environment={(Application.isEditor ? "Editor" : "Player")} platform={Application.platform} " +
            $"unity={Application.unityVersion} resolution={Screen.width}x{Screen.height} " +
            $"vSync={QualitySettings.vSyncCount} targetFps={Application.targetFrameRate} " +
            $"cpu={SystemInfo.processorType} gpu={SystemInfo.graphicsDeviceName} " +
            "| desligar: Ctrl+Shift+F | phase times inclusive; nao somar fases aninhadas");
        // Startup allocation/recorder creation/logging is not a steady gameplay sample.
        previousUpdateTime = Time.realtimeSinceStartupAsDouble;
    }

    void StopCapture()
    {
        CollectFrameSample();
        if (windowCount > 0)
            LogReport();
        isCapturing = false;
        GameplayPerformanceCapture.Stop();
        DisposeRecorders();
        FpsCounterOverlay.SetOverlayVisible(false);
        Debug.Log("[BattlePerf] CAPTURA ENCERRADA");
    }

    void CollectFrameSample()
    {
        double now = Time.realtimeSinceStartupAsDouble;
        int completedFrame = Time.frameCount - 1;
        if (completedFrame <= lastCollectedFrame)
            return;
        double duration = now - previousUpdateTime;
        previousUpdateTime = now;
        lastCollectedFrame = completedFrame;
        var frame = GameplayPerformanceCapture.GetFrame(completedFrame);
        for (int i = 0; i < metrics.Length; i++)
            frame.Metrics[i] = metrics[i].ReadCompletedFrame();
        // Skip the partial startup frame; still drain profiler samples above.
        if (completedFrame <= startFrame)
        {
            pendingInterruption = GameplayPerformanceInterruption.None;
            return;
        }
        // Unity's delta describes the completed frame. Wall time between Updates
        // also contains the *current* frame's FixedUpdate work, so keep it separate.
        frame.Milliseconds = Time.unscaledDeltaTime * 1000f;
        frame.WallGapMilliseconds = duration * 1000d;
        frame.Interruption |= pendingInterruption | CurrentInterruption();
        pendingInterruption = GameplayPerformanceInterruption.None;
        if (!frame.HasState)
            frame.Scene = activeScene;
        if (frame.IsGameplaySample)
        {
            frame.ResumedAfterMilliseconds = interruptionDuration;
            frame.ResumedReason = interruptionReasons;
            interruptionDuration = 0;
            interruptionReasons = GameplayPerformanceInterruption.None;
        }
        else
        {
            interruptionDuration += frame.WallGapMilliseconds;
            interruptionReasons |= frame.Interruption;
        }
        window[windowCount++] = frame;
        elapsedSeconds += Math.Max(duration, Time.unscaledDeltaTime);
        if (frame.IsGameplaySample)
            statistics.Add(frame.Milliseconds);
    }

    void LogReport()
    {
        using var reportSample = GameplayPerformanceCapture.Measure(GameplayPerformancePhase.Report);
        if (windowCount == 0)
            return;
        report.Clear();
        double average = statistics.AverageMilliseconds;
        var last = window[windowCount - 1];
        report.Append($"[BattlePerf] scene={activeScene} mode={Mode(activeScene)} " +
            $"players={last.Players} com={last.ComPlayers} bombs={last.Bombs} mounted={last.Mounted} eggs={last.Eggs} " +
            $"frames={window[0].Number}..{window[windowCount - 1].Number} gameplay={statistics.Count}/{windowCount} " +
            $"fpsAvg={(average > 0 ? (1000d / average).ToString("0.0") : "N/A")} " +
            $"frameAvg={Format(average, statistics.Count > 0)}ms " +
            $"p95={Format(statistics.Percentile(0.95f), statistics.Count > 0)}ms " +
            $"p99={Format(statistics.Percentile(0.99f), statistics.Count > 0)}ms " +
            $"worst={Format(statistics.MaximumMilliseconds, statistics.Count > 0)}ms " +
            $"over16.67={statistics.OverBudget} slow20={statistics.Slow20} slow33.33={statistics.Slow33} " +
            $"vSync={QualitySettings.vSyncCount} targetFps={Application.targetFrameRate}");
        for (int metric = 0; metric < metrics.Length; metric++)
        {
            double total = 0;
            int available = 0;
            for (int i = 0; i < windowCount; i++)
            {
                var frame = window[i];
                if (!IsGameplaySample(frame) || double.IsNaN(frame.Metrics[metric])) continue;
                total += frame.Metrics[metric];
                available++;
            }
            report.Append($" {metrics[metric].Name}={Format(available > 0 ? total / available : 0, available > 0)}");
        }
        report.Append(" | phases gameplay totalMs/maxCallMs/calls (inclusive):");
        for (int phase = 0; phase < (int)GameplayPerformancePhase.Count; phase++)
        {
            double total = 0, maximum = 0;
            int calls = 0;
            for (int i = 0; i < windowCount; i++)
            {
                // Include interruption frames for diagnostics overhead, clearly labelled below.
                if (phase < (int)GameplayPerformancePhase.Capture && !IsGameplaySample(window[i])) continue;
                var sample = window[i].Phases[phase];
                total += sample.Milliseconds;
                maximum = Math.Max(maximum, sample.MaximumMilliseconds);
                calls += sample.Calls;
            }
            report.Append($" {(GameplayPerformancePhase)phase}{(phase >= (int)GameplayPerformancePhase.Capture ? "(allFrames)" : "")}={total:0.000}/{maximum:0.000}/{calls}");
        }
        AppendInterruptions();
        Array.Clear(worst, 0, worst.Length);
        for (int i = 0; i < windowCount; i++)
        {
            var frame = window[i];
            if (!IsGameplaySample(frame) || frame.Milliseconds < 20f) continue;
            for (int rank = 0; rank < worst.Length; rank++)
            {
                if (worst[rank] != null && frame.Milliseconds <= worst[rank].Milliseconds) continue;
                for (int shift = worst.Length - 1; shift > rank; shift--) worst[shift] = worst[shift - 1];
                worst[rank] = frame;
                break;
            }
        }
        for (int i = 0; i < worst.Length && worst[i] != null; i++)
        {
            var frame = worst[i];
            report.Append($"\n[BattlePerf] SLOW frame={frame.Number} scene={frame.Scene} " +
                $"frame={frame.Milliseconds:0.00}ms main={Format(frame.Metrics[0])}ms " +
                $"wallGap={frame.WallGapMilliseconds:0.00}ms " +
                $"gc={Format(frame.Metrics[1])}KB players={frame.Players} com={frame.ComPlayers} " +
                $"bombs={frame.Bombs} mounted={frame.Mounted} eggs={frame.Eggs} timeScale={frame.TimeScale:0.00} stateAvailable={frame.HasState}");
            for (int metric = 2; metric < metrics.Length; metric++)
                report.Append($" {metrics[metric].Name}={Format(frame.Metrics[metric])}");
            for (int phase = 0; phase < (int)GameplayPerformancePhase.Count; phase++)
            {
                var sample = frame.Phases[phase];
                report.Append($" {(GameplayPerformancePhase)phase}={sample.Milliseconds:0.000}/{sample.MaximumMilliseconds:0.000}/{sample.Calls}");
            }
            for (int callIndex = 0; callIndex < frame.ExpensiveCalls.Length; callIndex++)
            {
                var call = frame.ExpensiveCalls[callIndex];
                if (call.Operation == null) break;
                report.Append($"\n[BattlePerf] HOT frame={frame.Number} player={call.PlayerId} " +
                    $"method={call.Operation} time={call.Milliseconds:0.000}ms alloc={call.AllocatedBytes / 1024d:0.000}KB inclusive=true");
            }
        }
        // One Console entry per report, without an expensive managed stack trace.
        Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "{0}", report.ToString());
        ResetWindow();
    }

    void AppendInterruptions()
    {
        // Coalesce consecutive excluded frames with the same reason; retain every duration.
        for (int first = 0; first < windowCount;)
        {
            var frame = window[first];
            if (frame.ResumedReason != GameplayPerformanceInterruption.None)
                report.Append($"\n[BattlePerf] RESUME frame={frame.Number} scene={frame.Scene} " +
                    $"interruptionDuration={frame.ResumedAfterMilliseconds:0.00}ms reason={frame.ResumedReason}");
            if (IsGameplaySample(frame)) { first++; continue; }
            int end = first;
            double duration = 0;
            while (end < windowCount && !IsGameplaySample(window[end]) &&
                window[end].Interruption == frame.Interruption && window[end].HasState == frame.HasState)
                duration += window[end++].WallGapMilliseconds;
            report.Append($"\n[BattlePerf] INTERRUPTION frames={frame.Number}..{window[end - 1].Number} " +
                $"duration={duration:0.00}ms reason={frame.Interruption} stateAvailable={frame.HasState} " +
                $"scene={frame.Scene}");
            first = end;
        }
    }

    static bool IsGameplaySample(GameplayPerformanceCapture.Frame frame) =>
        frame.IsGameplaySample;

    static string Format(double value, bool available = true) =>
        available && !double.IsNaN(value) ? value.ToString("0.000") : "N/A";

    void ResetWindow()
    {
        windowCount = 0;
        elapsedSeconds = 0;
        statistics.Reset();
    }

    void DisposeRecorders()
    {
        if (metrics == null) return;
        foreach (Metric metric in metrics) metric.Dispose();
    }

    static bool IsBattleModeScene() => IsBattleModeScene(SceneManager.GetActiveScene().name);
    static bool IsBattleModeScene(string scene) => scene.StartsWith("BattleMode_", StringComparison.OrdinalIgnoreCase);
    static bool IsGameplayDiagnosticsScene(string scene) => IsBattleModeScene(scene) || scene.StartsWith("Stage_", StringComparison.OrdinalIgnoreCase);
    static string Mode(string scene) => IsBattleModeScene(scene) ? "Battle" : scene.StartsWith("Stage_", StringComparison.OrdinalIgnoreCase) ? "NormalGame" : "Other";

    static Metric[] CreateMetrics() => new[]
    {
        new Metric("mainMs", ProfilerCategory.Internal, "Main Thread"),
        new Metric("gcKB", ProfilerCategory.Memory, "GC Allocated In Frame", 1d / 1024d),
        new Metric("draw", ProfilerCategory.Render, "Draw Calls Count", 1d),
        new Metric("batches", ProfilerCategory.Render, "Batches Count", 1d),
        new Metric("PlayerUpdateMs", ProfilerCategory.Scripts, BattleModePerformanceMarkers.PlayerUpdateName),
        new Metric("PlayerFixedUpdateMs", ProfilerCategory.Scripts, BattleModePerformanceMarkers.PlayerFixedUpdateName),
        new Metric("BombControllerUpdateMs", ProfilerCategory.Scripts, BattleModePerformanceMarkers.BombControllerUpdateName),
        new Metric("AnimatedSpriteUpdateMs", ProfilerCategory.Scripts, BattleModePerformanceMarkers.AnimatedSpriteUpdateName),
        new Metric("BattleHudLateUpdateMs", ProfilerCategory.Scripts, BattleModePerformanceMarkers.BattleHudLateUpdateName),
        new Metric("HudBackgroundLateUpdateMs", ProfilerCategory.Scripts, BattleModePerformanceMarkers.HudBackgroundLateUpdateName),
        new Metric("HudGridLateUpdateMs", ProfilerCategory.Scripts, BattleModePerformanceMarkers.HudGridLateUpdateName),
        new Metric("HudPortraitLateUpdateMs", ProfilerCategory.Scripts, BattleModePerformanceMarkers.HudPortraitLateUpdateName),
        new Metric("HudStatsLateUpdateMs", ProfilerCategory.Scripts, BattleModePerformanceMarkers.HudStatsLateUpdateName),
        new Metric("HudPushStartLateUpdateMs", ProfilerCategory.Scripts, BattleModePerformanceMarkers.HudPushStartLateUpdateName),
        new Metric("HudLifePreviewLateUpdateMs", ProfilerCategory.Scripts, BattleModePerformanceMarkers.HudLifePreviewLateUpdateName),
        new Metric("ArenaUpdateMs", ProfilerCategory.Scripts, BattleModePerformanceMarkers.ArenaUpdateName),
        new Metric("InputUpdateMs", ProfilerCategory.Scripts, BattleModePerformanceMarkers.InputUpdateName),
        new Metric("AbilityUpdateMs", ProfilerCategory.Scripts, BattleModePerformanceMarkers.AbilityUpdateName),
        new Metric("PlayerAuxUpdateMs", ProfilerCategory.Scripts, BattleModePerformanceMarkers.PlayerAuxUpdateName),
        new Metric("EggQueueUpdateMs", ProfilerCategory.Scripts, BattleModePerformanceMarkers.EggQueueUpdateName),
        new Metric("MountCompanionUpdateMs", ProfilerCategory.Scripts, BattleModePerformanceMarkers.MountCompanionUpdateName),
        new Metric("PlayerStateAnimationUpdateMs", ProfilerCategory.Scripts, BattleModePerformanceMarkers.PlayerStateAnimationUpdateName),
        new Metric("InactivityAnimationUpdateMs", ProfilerCategory.Scripts, BattleModePerformanceMarkers.InactivityAnimationUpdateName),
        new Metric("CorneredAnimationUpdateMs", ProfilerCategory.Scripts, BattleModePerformanceMarkers.CorneredAnimationUpdateName),
        new Metric("ComUpdateMs", ProfilerCategory.Scripts, BattleModePerformanceMarkers.ComUpdateName),
        new Metric("ComThinkMs", ProfilerCategory.Scripts, BattleModePerformanceMarkers.ComThinkName),
        new Metric("gcCollectMs", ProfilerCategory.Memory, "GC.Collect"),
        new Metric("playerLoopMs", ProfilerCategory.Internal, "PlayerLoop"),
        new Metric("editorLoopMs", ProfilerCategory.Internal, "EditorLoop"),
        new Metric("presentWaitMs", ProfilerCategory.Render, "Gfx.WaitForPresentOnGfxThread"),
    };
}

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
    public const string ArenaUpdateName = "SB6.BattlePerf.Arena.Update";
    public const string InputUpdateName = "SB6.BattlePerf.Input.Update";
    public const string AbilityUpdateName = "SB6.BattlePerf.Ability.Update";
    public const string PlayerAuxUpdateName = "SB6.BattlePerf.PlayerAux.Update";
    public const string EggQueueUpdateName = "SB6.BattlePerf.EggQueue.LateUpdate";
    public const string MountCompanionUpdateName = "SB6.BattlePerf.MountCompanion.Update";
    public const string PlayerStateAnimationUpdateName = "SB6.BattlePerf.PlayerStateAnimation.Update";
    public const string InactivityAnimationUpdateName = "SB6.BattlePerf.InactivityAnimation.Update";
    public const string CorneredAnimationUpdateName = "SB6.BattlePerf.CorneredAnimation.Update";

    public static readonly ProfilerMarker PlayerUpdate = new(PlayerUpdateName);
    public static readonly ProfilerMarker PlayerFixedUpdate = new(PlayerFixedUpdateName);
    public static readonly ProfilerMarker BombControllerUpdate = new(BombControllerUpdateName);
    public static readonly ProfilerMarker AnimatedSpriteUpdate = new(AnimatedSpriteUpdateName);
    public static readonly ProfilerMarker BattleHudLateUpdate = new(BattleHudLateUpdateName);
    public static readonly ProfilerMarker ArenaUpdate = new(ArenaUpdateName);
    public static readonly ProfilerMarker InputUpdate = new(InputUpdateName);
    public static readonly ProfilerMarker AbilityUpdate = new(AbilityUpdateName);
    public static readonly ProfilerMarker PlayerAuxUpdate = new(PlayerAuxUpdateName);
    public static readonly ProfilerMarker EggQueueUpdate = new(EggQueueUpdateName);
    public static readonly ProfilerMarker MountCompanionUpdate = new(MountCompanionUpdateName);
    public static readonly ProfilerMarker PlayerStateAnimationUpdate = new(PlayerStateAnimationUpdateName);
    public static readonly ProfilerMarker InactivityAnimationUpdate = new(InactivityAnimationUpdateName);
    public static readonly ProfilerMarker CorneredAnimationUpdate = new(CorneredAnimationUpdateName);

    public static void EnsureInitialized()
    {
    }
}

public sealed class BattleModePerformanceDiagnostics : MonoBehaviour
{
    const string ObjectName = "BattleModePerformanceDiagnostics";
    const float ReportIntervalSeconds = 2f;
    const float SlowFrameThresholdMilliseconds = 20f;

    static BattleModePerformanceDiagnostics instance;

    ProfilerRecorder mainThreadRecorder;
    ProfilerRecorder gcAllocatedRecorder;
    ProfilerRecorder drawCallsRecorder;
    ProfilerRecorder batchesRecorder;
    ProfilerRecorder playerUpdateRecorder;
    ProfilerRecorder playerFixedUpdateRecorder;
    ProfilerRecorder bombControllerRecorder;
    ProfilerRecorder animatedSpriteRecorder;
    ProfilerRecorder hudRecorder;
    ProfilerRecorder arenaRecorder;
    ProfilerRecorder inputRecorder;
    ProfilerRecorder abilityRecorder;
    ProfilerRecorder playerAuxRecorder;
    ProfilerRecorder eggQueueRecorder;
    ProfilerRecorder mountCompanionRecorder;
    ProfilerRecorder playerStateAnimationRecorder;
    ProfilerRecorder inactivityAnimationRecorder;
    ProfilerRecorder corneredAnimationRecorder;

    bool isCapturing;
    int sampledFrames;
    int slowFrames;
    float elapsedSeconds;
    float totalFrameMilliseconds;
    float worstFrameMilliseconds;
    double totalMainThreadMilliseconds;
    long totalGcAllocatedBytes;
    long totalDrawCalls;
    long totalBatches;
    double totalPlayerUpdateMilliseconds;
    double totalPlayerFixedUpdateMilliseconds;
    double totalBombControllerMilliseconds;
    double totalAnimatedSpriteMilliseconds;
    double totalHudMilliseconds;
    double totalArenaMilliseconds;
    double totalInputMilliseconds;
    double totalAbilityMilliseconds;
    double totalPlayerAuxMilliseconds;
    double totalEggQueueMilliseconds;
    double totalMountCompanionMilliseconds;
    double totalPlayerStateAnimationMilliseconds;
    double totalInactivityAnimationMilliseconds;
    double totalCorneredAnimationMilliseconds;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null)
            return;

        GameObject go = new GameObject(ObjectName);
        instance = go.AddComponent<BattleModePerformanceDiagnostics>();
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
        HandleShortcut();

        if (!isCapturing || !IsBattleModeScene())
            return;

        CollectFrameSample();

        if (elapsedSeconds >= ReportIntervalSeconds)
            LogReport();
    }

    void OnDestroy()
    {
        DisposeRecorders();
        if (instance == this)
            instance = null;
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

    void StartCapture()
    {
        DisposeRecorders();
        BattleModePerformanceMarkers.EnsureInitialized();

        mainThreadRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1);
        gcAllocatedRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
        drawCallsRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count", 1);
        batchesRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count", 1);

        playerUpdateRecorder = CreateMarkerRecorder(BattleModePerformanceMarkers.PlayerUpdate);
        playerFixedUpdateRecorder = CreateMarkerRecorder(BattleModePerformanceMarkers.PlayerFixedUpdate);
        bombControllerRecorder = CreateMarkerRecorder(BattleModePerformanceMarkers.BombControllerUpdate);
        animatedSpriteRecorder = CreateMarkerRecorder(BattleModePerformanceMarkers.AnimatedSpriteUpdate);
        hudRecorder = CreateMarkerRecorder(BattleModePerformanceMarkers.BattleHudLateUpdate);
        arenaRecorder = CreateMarkerRecorder(BattleModePerformanceMarkers.ArenaUpdate);
        inputRecorder = CreateMarkerRecorder(BattleModePerformanceMarkers.InputUpdate);
        abilityRecorder = CreateMarkerRecorder(BattleModePerformanceMarkers.AbilityUpdate);
        playerAuxRecorder = CreateMarkerRecorder(BattleModePerformanceMarkers.PlayerAuxUpdate);
        eggQueueRecorder = CreateMarkerRecorder(BattleModePerformanceMarkers.EggQueueUpdate);
        mountCompanionRecorder = CreateMarkerRecorder(BattleModePerformanceMarkers.MountCompanionUpdate);
        playerStateAnimationRecorder = CreateMarkerRecorder(BattleModePerformanceMarkers.PlayerStateAnimationUpdate);
        inactivityAnimationRecorder = CreateMarkerRecorder(BattleModePerformanceMarkers.InactivityAnimationUpdate);
        corneredAnimationRecorder = CreateMarkerRecorder(BattleModePerformanceMarkers.CorneredAnimationUpdate);

        ResetWindow();
        isCapturing = true;
        FpsCounterOverlay.SetOverlayVisible(true);

        Debug.Log(
            $"[BattlePerf] CAPTURA INICIADA | scene={SceneManager.GetActiveScene().name} " +
            "| use uma partida de 6 jogadores por alguns segundos | desligar: Ctrl+Shift+F");
    }

    void StopCapture()
    {
        if (sampledFrames > 0)
            LogReport();

        isCapturing = false;
        DisposeRecorders();
        FpsCounterOverlay.SetOverlayVisible(false);
        Debug.Log("[BattlePerf] CAPTURA ENCERRADA");
    }

    void CollectFrameSample()
    {
        float frameMilliseconds = Time.unscaledDeltaTime * 1000f;
        sampledFrames++;
        elapsedSeconds += Time.unscaledDeltaTime;
        totalFrameMilliseconds += frameMilliseconds;
        worstFrameMilliseconds = Mathf.Max(worstFrameMilliseconds, frameMilliseconds);

        if (frameMilliseconds >= SlowFrameThresholdMilliseconds)
            slowFrames++;

        totalMainThreadMilliseconds += RecorderMilliseconds(mainThreadRecorder);
        totalGcAllocatedBytes += RecorderValue(gcAllocatedRecorder);
        totalDrawCalls += RecorderValue(drawCallsRecorder);
        totalBatches += RecorderValue(batchesRecorder);
        totalPlayerUpdateMilliseconds += RecorderMilliseconds(playerUpdateRecorder);
        totalPlayerFixedUpdateMilliseconds += RecorderMilliseconds(playerFixedUpdateRecorder);
        totalBombControllerMilliseconds += RecorderMilliseconds(bombControllerRecorder);
        totalAnimatedSpriteMilliseconds += RecorderMilliseconds(animatedSpriteRecorder);
        totalHudMilliseconds += RecorderMilliseconds(hudRecorder);
        totalArenaMilliseconds += RecorderMilliseconds(arenaRecorder);
        totalInputMilliseconds += RecorderMilliseconds(inputRecorder);
        totalAbilityMilliseconds += RecorderMilliseconds(abilityRecorder);
        totalPlayerAuxMilliseconds += RecorderMilliseconds(playerAuxRecorder);
        totalEggQueueMilliseconds += RecorderMilliseconds(eggQueueRecorder);
        totalMountCompanionMilliseconds += RecorderMilliseconds(mountCompanionRecorder);
        totalPlayerStateAnimationMilliseconds += RecorderMilliseconds(playerStateAnimationRecorder);
        totalInactivityAnimationMilliseconds += RecorderMilliseconds(inactivityAnimationRecorder);
        totalCorneredAnimationMilliseconds += RecorderMilliseconds(corneredAnimationRecorder);
    }

    void LogReport()
    {
        if (sampledFrames <= 0)
            return;

        float averageFrameMilliseconds = totalFrameMilliseconds / sampledFrames;
        float averageFps = averageFrameMilliseconds > 0f ? 1000f / averageFrameMilliseconds : 0f;
        float mainThreadMilliseconds = Average(totalMainThreadMilliseconds);
        float gcKilobytesPerFrame = Average(totalGcAllocatedBytes) / 1024f;

        MovementController[] activePlayers = FindObjectsByType<MovementController>(FindObjectsInactive.Exclude);
        int players = activePlayers.Length;
        int mountedPlayers = 0;
        int queuedEggs = 0;
        for (int i = 0; i < activePlayers.Length; i++)
        {
            MovementController player = activePlayers[i];
            if (player == null || !player.CompareTag("Player"))
                continue;

            if (player.TryGetComponent<PlayerMountCompanion>(out var companion) &&
                companion != null &&
                companion.HasMountedLouie())
            {
                mountedPlayers++;
            }

            if (player.TryGetComponent<MountEggQueue>(out var eggQueue) && eggQueue != null)
                queuedEggs += eggQueue.Count;
        }

        int bombs = Bomb.ActiveBombs.Count;
        int explosions = FindObjectsByType<BombExplosion>(FindObjectsInactive.Exclude).Length;
        AnimatedSpriteRenderer[] animators = FindObjectsByType<AnimatedSpriteRenderer>(FindObjectsInactive.Exclude);
        int runningAnimators = 0;
        for (int i = 0; i < animators.Length; i++)
        {
            if (animators[i] != null && animators[i].isActiveAndEnabled)
                runningAnimators++;
        }

        Debug.Log(
            $"[BattlePerf] scene={SceneManager.GetActiveScene().name} players={players} " +
            $"| fpsAvg={averageFps:0.0} frameAvg={averageFrameMilliseconds:0.00}ms worst={worstFrameMilliseconds:0.00}ms " +
            $"slow>={SlowFrameThresholdMilliseconds:0}ms={slowFrames}/{sampledFrames} " +
            $"| main={mainThreadMilliseconds:0.00}ms gc={gcKilobytesPerFrame:0.00}KB/f " +
            $"draw={Average(totalDrawCalls):0.0}/f batches={Average(totalBatches):0.0}/f " +
            $"| scripts playerU={Average(totalPlayerUpdateMilliseconds):0.000}ms " +
            $"playerF={Average(totalPlayerFixedUpdateMilliseconds):0.000}ms " +
            $"bomb={Average(totalBombControllerMilliseconds):0.000}ms " +
            $"anim={Average(totalAnimatedSpriteMilliseconds):0.000}ms " +
            $"hud={Average(totalHudMilliseconds):0.000}ms arena={Average(totalArenaMilliseconds):0.000}ms " +
            $"input={Average(totalInputMilliseconds):0.000}ms abilities={Average(totalAbilityMilliseconds):0.000}ms " +
            $"aux={Average(totalPlayerAuxMilliseconds):0.000}ms egg={Average(totalEggQueueMilliseconds):0.000}ms " +
            $"mount={Average(totalMountCompanionMilliseconds):0.000}ms stateAnim={Average(totalPlayerStateAnimationMilliseconds):0.000}ms " +
            $"idleAnim={Average(totalInactivityAnimationMilliseconds):0.000}ms cornered={Average(totalCorneredAnimationMilliseconds):0.000}ms " +
            $"| entities bombs={bombs} explosions={explosions} mounted={mountedPlayers} eggs={queuedEggs} " +
            $"animators={runningAnimators}/{animators.Length}");

        ResetWindow();
    }

    void ResetWindow()
    {
        sampledFrames = 0;
        slowFrames = 0;
        elapsedSeconds = 0f;
        totalFrameMilliseconds = 0f;
        worstFrameMilliseconds = 0f;
        totalMainThreadMilliseconds = 0d;
        totalGcAllocatedBytes = 0L;
        totalDrawCalls = 0L;
        totalBatches = 0L;
        totalPlayerUpdateMilliseconds = 0d;
        totalPlayerFixedUpdateMilliseconds = 0d;
        totalBombControllerMilliseconds = 0d;
        totalAnimatedSpriteMilliseconds = 0d;
        totalHudMilliseconds = 0d;
        totalArenaMilliseconds = 0d;
        totalInputMilliseconds = 0d;
        totalAbilityMilliseconds = 0d;
        totalPlayerAuxMilliseconds = 0d;
        totalEggQueueMilliseconds = 0d;
        totalMountCompanionMilliseconds = 0d;
        totalPlayerStateAnimationMilliseconds = 0d;
        totalInactivityAnimationMilliseconds = 0d;
        totalCorneredAnimationMilliseconds = 0d;
    }

    void DisposeRecorders()
    {
        mainThreadRecorder.Dispose();
        gcAllocatedRecorder.Dispose();
        drawCallsRecorder.Dispose();
        batchesRecorder.Dispose();
        playerUpdateRecorder.Dispose();
        playerFixedUpdateRecorder.Dispose();
        bombControllerRecorder.Dispose();
        animatedSpriteRecorder.Dispose();
        hudRecorder.Dispose();
        arenaRecorder.Dispose();
        inputRecorder.Dispose();
        abilityRecorder.Dispose();
        playerAuxRecorder.Dispose();
        eggQueueRecorder.Dispose();
        mountCompanionRecorder.Dispose();
        playerStateAnimationRecorder.Dispose();
        inactivityAnimationRecorder.Dispose();
        corneredAnimationRecorder.Dispose();
    }

    static ProfilerRecorder CreateMarkerRecorder(ProfilerMarker marker)
    {
        return ProfilerRecorder.StartNew(
            marker,
            1,
            ProfilerRecorderOptions.WrapAroundWhenCapacityReached |
            ProfilerRecorderOptions.SumAllSamplesInFrame);
    }

    static long RecorderValue(ProfilerRecorder recorder)
    {
        return recorder.Valid && recorder.Count > 0 ? recorder.LastValue : 0L;
    }

    static double RecorderMilliseconds(ProfilerRecorder recorder)
    {
        return RecorderValue(recorder) * 0.000001d;
    }

    float Average(double total)
    {
        return sampledFrames > 0 ? (float)(total / sampledFrames) : 0f;
    }

    float Average(long total)
    {
        return sampledFrames > 0 ? (float)total / sampledFrames : 0f;
    }

    static bool IsBattleModeScene()
    {
        return SceneManager.GetActiveScene().name.StartsWith("BattleMode_", StringComparison.OrdinalIgnoreCase);
    }
}

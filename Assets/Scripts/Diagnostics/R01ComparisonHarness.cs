using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Assets.Scripts.Netcode;
using Mirror;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// R01 measurement harness (Development builds only, inert without its -r01-* arguments). It deliberately uses only APIs
/// that predate R01 so the same file can be compiled into the pre-R01 baseline build for the cost comparison.
///   -r01-compare-walk      drives player 1 (the local player 1 online, or local player 1 offline) through a fixed input
///                          script through the normal synthetic hardware slot; no other player is ever driven.
///   -r01-offline-compare   offline only: sets up a 2-player Battle Mode match in BattleMode_15 (P1/P2 Man, others Off)
///                          from the title screen, then quits when the script has finished.
///   -r01-compare-sd-off    Sudden Death Off for this run (restored on quit), so a random setting cannot split runs.
///   -r01-compare-capture P per-frame JSONL samples of players 1 and 2 (position, visible renderers, role).
///   -r01-frame-stats       one "[R01Frame]" log row per second with this process's main-thread frame work time (EarlyUpdate
///                          start to the end of PostLateUpdate, so the frame-rate wait in TimeUpdate is excluded), frame
///                          interval, managed allocations ("GC Allocated In Frame") and GC collections. Same code in both builds.
/// </summary>
[DefaultExecutionOrder(1600)]
public sealed class R01ComparisonHarness : MonoBehaviour
{
    // Input script: one cycle = Right, Left, Up, Down, each held 2.4 s then released 0.6 s.
    static readonly PlayerAction[] ScriptActions = { PlayerAction.MoveRight, PlayerAction.MoveLeft, PlayerAction.MoveUp, PlayerAction.MoveDown };
    const float HoldSeconds = 2.4f, GapSeconds = 0.6f;
    const int Cycles = 3;
    const float SettleSeconds = 3f;

    bool walk, offline, sdOff;
    string capturePath;
    StreamWriter writer;
    int pendingLines;
    BattleModePlayerControlMode[] savedModes;
    BattleModeSuddenDeathSetting savedSuddenDeath;
    bool savedSettings, restored;

    MovementController driver;
    float scriptStart = -1f;
    int currentSegment = -1;
    bool scriptDone;
    float doneAt;
    readonly Dictionary<MovementController, SpriteRenderer[]> bodyRenderers = new();
    readonly HashSet<string> setupWritten = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (!Debug.isDebugBuild) return;
        var args = Environment.GetCommandLineArgs();
        bool walk = Array.IndexOf(args, "-r01-compare-walk") >= 0, offline = Array.IndexOf(args, "-r01-offline-compare") >= 0,
            sdOff = Array.IndexOf(args, "-r01-compare-sd-off") >= 0;
        if (Array.IndexOf(args, "-r01-frame-stats") >= 0) FrameStats.Install();
        int captureIndex = Array.IndexOf(args, "-r01-compare-capture");
        string capture = captureIndex >= 0 && captureIndex + 1 < args.Length ? args[captureIndex + 1] : null;
        if (!walk && !offline && !sdOff && capture == null) return;
        var root = new GameObject("R01ComparisonHarness");
        DontDestroyOnLoad(root);
        var harness = root.AddComponent<R01ComparisonHarness>();
        harness.walk = walk; harness.offline = offline; harness.sdOff = sdOff; harness.capturePath = capture;
        harness.Begin();
    }

    void Begin()
    {
        if (offline || sdOff)
        {
            savedModes = (BattleModePlayerControlMode[])SaveSystem.GetBattleModePlayerControlModes().Clone();
            savedSuddenDeath = SaveSystem.GetBattleModeSuddenDeathSetting();
            savedSettings = true;
        }
        if (sdOff) SaveSystem.SetBattleModeSuddenDeathSetting(BattleModeSuddenDeathSetting.Off);
        if (capturePath != null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(capturePath)));
            writer = new StreamWriter(capturePath, false, new UTF8Encoding(false), 1 << 16);
        }
        Log("harness_start", "walk=" + walk + ";offline=" + offline + ";sdOff=" + sdOff + ";online=" + NetSync.IsOnline +
            ";hold=" + Num(HoldSeconds) + ";gap=" + Num(GapSeconds) + ";cycles=" + Cycles);
        if (offline) StartCoroutine(StartOfflineBattle());
    }

    // Same preparation as the dedicated load-test server before it launches BattleMode_15 for two connected players;
    // the same frame pacing as a -load-test-client process.
    IEnumerator StartOfflineBattle()
    {
        Application.runInBackground = true;
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 0;
        while (SceneManager.GetActiveScene().name != "TitleScreen") yield return null;
        for (int i = 0; i < 30; i++) yield return null;
        PlayerPersistentStats.ResetToDefaultsAll();
        var modes = new BattleModePlayerControlMode[savedModes.Length];
        for (int i = 0; i < modes.Length; i++) modes[i] = i < 2 ? BattleModePlayerControlMode.Man : BattleModePlayerControlMode.Off;
        SaveSystem.SetBattleModePlayerControlModes(modes);
        GameSession.Instance?.SetActivePlayerIds(new[] { 1, 2 });
        Log("offline_battle_load", "scene=BattleMode_15;players=1,2");
        SceneManager.LoadScene("BattleMode_15");
    }

    void Update()
    {
        if (!walk || scriptDone) return;
        var input = PlayerInputManager.Instance;
        if (input == null) return;
        if (driver == null) driver = FindDriver();
        if (driver == null) return;
        if (scriptStart < 0)
        {
            if (!driver.CanPredictNetworkMovement || GamePauseController.IsPaused) return;
            scriptStart = Time.unscaledTime;
            Log("script_start", "player=" + driver.PlayerId);
        }
        float t = Time.unscaledTime - scriptStart;
        float cycle = HoldSeconds + GapSeconds;
        int segment = Mathf.FloorToInt(t / cycle);
        bool holding = segment < ScriptActions.Length * Cycles && t - segment * cycle < HoldSeconds;
        PlayerAction action = ScriptActions[Mathf.Min(segment, ScriptActions.Length * Cycles - 1) % ScriptActions.Length];
        foreach (var a in ScriptActions) input.SetSyntheticHeld(1, a, holding && a == action);
        int active = holding ? segment : -1;
        if (active != currentSegment)
        {
            if (currentSegment >= 0) Log("segment_end", "index=" + currentSegment + ";st=" + Num(t));
            if (active >= 0) Log("segment_start", "index=" + active + ";action=" + action + ";st=" + Num(t));
            currentSegment = active;
        }
        if (segment >= ScriptActions.Length * Cycles)
        {
            foreach (var a in ScriptActions) input.SetSyntheticHeld(1, a, false);
            scriptDone = true; doneAt = Time.unscaledTime;
            Log("script_done", "st=" + Num(t));
            if (offline) StartCoroutine(QuitAfterSettle());
        }
    }

    IEnumerator QuitAfterSettle()
    {
        yield return new WaitForSecondsRealtime(SettleSeconds);
        Log("harness_quit", "settled=" + Num(Time.unscaledTime - doneAt));
        Restore();
        Application.Quit();
    }

    MovementController FindDriver()
    {
        if (NetSync.IsOnline)
        {
            if (!NetworkClient.active || NetworkClient.localPlayer == null) return null;
            var local = NetworkClient.localPlayer.GetComponent<MovementController>();
            return local != null && local.PlayerId == 1 ? local : null;
        }
        foreach (var mover in FindObjectsByType<MovementController>())
            if (mover != null && mover.PlayerId == 1 && mover.CompareTag("Player")) return mover;
        return null;
    }

    void LateUpdate()
    {
        if (writer == null) return;
        foreach (var mover in FindObjectsByType<MovementController>())
        {
            if (mover == null || !mover.CompareTag("Player") || (mover.PlayerId != 1 && mover.PlayerId != 2)) continue;
            string role = RoleOf(mover);
            if (setupWritten.Add(role + mover.PlayerId + SceneManager.GetActiveScene().name)) WriteSetup(mover, role);
            var position = mover.transform.position;
            var line = new StringBuilder(160);
            line.Append("{\"k\":\"s\",\"role\":\"").Append(role).Append("\",\"f\":").Append(Time.frameCount)
                .Append(",\"t\":").Append(Num(Time.unscaledTime)).Append(",\"u\":").Append(UtcMs()).Append(",\"st\":").Append(Num(scriptStart < 0 ? -1f : Time.unscaledTime - scriptStart))
                .Append(",\"p\":").Append(mover.PlayerId).Append(",\"x\":").Append(Num(position.x)).Append(",\"y\":").Append(Num(position.y))
                .Append(",\"r\":\"").Append(VisibleBody(mover)).Append("\",\"m\":").Append(mover.IsMounted ? "true" : "false")
                .Append(",\"dead\":").Append(mover.isDead ? "true" : "false").Append('}');
            WriteLine(line.ToString());
        }
    }

    static string RoleOf(MovementController mover)
    {
        if (!NetSync.IsOnline) return "offline";
        if (NetworkServer.active && !NetworkClient.active) return "server";
        return mover.TryGetComponent<NetworkIdentity>(out var identity) && identity.isLocalPlayer ? "owner" : "observer";
    }

    // Same visibility rule as the R01 renderer edges: enabled SpriteRenderers of the player body, mount children excluded.
    string VisibleBody(MovementController mover)
    {
        if (!bodyRenderers.TryGetValue(mover, out var renderers) || renderers == null)
        {
            var list = new List<SpriteRenderer>();
            foreach (var sprite in mover.GetComponentsInChildren<SpriteRenderer>(true))
                if (sprite.GetComponentInParent<MountVisualController>(true) == null) list.Add(sprite);
            renderers = list.ToArray();
            bodyRenderers[mover] = renderers;
        }
        var names = new List<string>();
        foreach (var sprite in renderers)
            if (sprite != null && sprite.enabled && sprite.gameObject.activeInHierarchy) names.Add(sprite.name);
        names.Sort(StringComparer.Ordinal);
        return string.Join(",", names);
    }

    void WriteSetup(MovementController mover, string role)
    {
        var bombs = mover.GetComponent<BombController>();
        int players = 0;
        foreach (var other in FindObjectsByType<MovementController>())
            if (other != null && other.CompareTag("Player") && other.PlayerId >= 1) players++;
        Log("setup", "role=" + role + ";player=" + mover.PlayerId + ";scene=" + SceneManager.GetActiveScene().name + ";players=" + players +
            ";spawnX=" + Num(mover.transform.position.x) + ";spawnY=" + Num(mover.transform.position.y) + ";speed=" + Num(mover.speed) +
            ";bombs=" + (bombs != null ? bombs.bombAmout : -1) + ";radius=" + (bombs != null ? bombs.explosionRadius : -1) +
            ";suddenDeath=" + SaveSystem.GetBattleModeSuddenDeathSetting() + ";destructibles=" + DestructibleFingerprint() +
            ";fps=" + Application.targetFrameRate + ";vsync=" + QualitySettings.vSyncCount);
    }

    static string DestructibleFingerprint()
    {
        // Order-independent (a sum of per-cell hashes): FindObjectsByType does not return the tilemaps in a stable order.
        int cells = 0; long hash = 17;
        foreach (var map in FindObjectsByType<Tilemap>())
        {
            if (map == null || map.name.IndexOf("Destruct", StringComparison.OrdinalIgnoreCase) < 0) continue;
            foreach (var cell in map.cellBounds.allPositionsWithin)
                if (map.HasTile(cell)) { cells++; hash = unchecked(hash + ((long)cell.x * 73856093 ^ (long)cell.y * 19349663)); }
        }
        return cells + ":" + hash.ToString("x", CultureInfo.InvariantCulture);
    }

    void Log(string kind, string detail)
    {
        WriteLine("{\"k\":\"" + kind + "\",\"f\":" + Time.frameCount + ",\"t\":" + Num(Time.unscaledTime) + ",\"u\":" + UtcMs() + ",\"d\":\"" + detail + "\"}");
        Debug.Log("[R01Compare] " + kind + " " + detail + " u=" + UtcMs());
    }

    void WriteLine(string line)
    {
        if (writer == null) return;
        writer.WriteLine(line);
        if (++pendingLines >= 120) { writer.Flush(); pendingLines = 0; }
    }

    static string Num(float value) => value.ToString("0.####", CultureInfo.InvariantCulture);

    // Wall clock shared by the processes of one run on the same machine (Time.unscaledTime is per process).
    static string UtcMs() => (DateTime.UtcNow.Ticks / 10000.0 - 63900000000000.0).ToString("0.###", CultureInfo.InvariantCulture);

    void Restore()
    {
        if (restored) return;
        restored = true;
        if (PlayerInputManager.Instance != null)
            foreach (var a in ScriptActions) PlayerInputManager.Instance.SetSyntheticHeld(1, a, false);
        if (savedSettings)
        {
            SaveSystem.SetBattleModePlayerControlModes(savedModes);
            SaveSystem.SetBattleModeSuddenDeathSetting(savedSuddenDeath);
        }
        if (writer != null) { writer.Flush(); writer.Dispose(); writer = null; }
    }

    static class FrameStats
    {
        struct WorkStartMarker { }
        struct WorkEndMarker { }
        static readonly float[] work = new float[1024];
        static int count, gcAtSecond;
        static long frameStart, workEnd;
        static double secondStart = -1, gcBytes, dtMax;
        static ProfilerRecorder gcRecorder;
        static bool installed;

        internal static void Install()
        {
            if (installed) return;
            installed = true;
            var loop = PlayerLoop.GetCurrentPlayerLoop();
            for (int i = 0; i < loop.subSystemList.Length; i++)
            {
                var type = loop.subSystemList[i].type;
                if (type != typeof(EarlyUpdate) && type != typeof(PostLateUpdate)) continue;
                var list = new List<PlayerLoopSystem>(loop.subSystemList[i].subSystemList ?? new PlayerLoopSystem[0]);
                if (type == typeof(EarlyUpdate)) list.Insert(0, new PlayerLoopSystem { type = typeof(WorkStartMarker), updateDelegate = WorkStart });
                else list.Add(new PlayerLoopSystem { type = typeof(WorkEndMarker), updateDelegate = WorkEnd });
                loop.subSystemList[i].subSystemList = list.ToArray();
            }
            PlayerLoop.SetPlayerLoop(loop);
            gcRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
            gcAtSecond = GC.CollectionCount(0);
            Application.quitting += () => gcRecorder.Dispose();
            Debug.Log("[R01Frame] installed gcRecorderValid=" + gcRecorder.Valid + " motionCapture=" + GameplayLatencyDiagnostics.Enabled);
        }

        static void WorkStart()
        {
            long now = System.Diagnostics.Stopwatch.GetTimestamp();
            // The previous frame is complete here: its work (start..end) and its allocations are final.
            if (frameStart != 0 && workEnd > frameStart && count < work.Length)
                work[count++] = (float)((workEnd - frameStart) * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
            if (gcRecorder.Valid && frameStart != 0) gcBytes += gcRecorder.LastValue;
            dtMax = Math.Max(dtMax, Time.unscaledDeltaTime * 1000.0);
            double realtime = Time.realtimeSinceStartupAsDouble;
            if (secondStart < 0) secondStart = realtime;
            if (realtime - secondStart >= 1 && count > 0) Flush(realtime);
            frameStart = now;
            workEnd = 0;
        }

        static void WorkEnd() => workEnd = System.Diagnostics.Stopwatch.GetTimestamp();

        static void Flush(double realtime)
        {
            Array.Sort(work, 0, count);
            double sum = 0;
            for (int i = 0; i < count; i++) sum += work[i];
            int gc = GC.CollectionCount(0);
            var c = CultureInfo.InvariantCulture;
            Debug.Log("[R01Frame] {\"u\":" + UtcMs() + ",\"seconds\":" + (realtime - secondStart).ToString("0.###", c) + ",\"frames\":" + count +
                ",\"workMeanMs\":" + (sum / count).ToString("0.###", c) + ",\"workP50Ms\":" + work[count / 2].ToString("0.###", c) +
                ",\"workP95Ms\":" + work[Math.Min(count - 1, (int)Math.Ceiling(count * 0.95) - 1)].ToString("0.###", c) +
                ",\"workMaxMs\":" + work[count - 1].ToString("0.###", c) + ",\"dtMaxMs\":" + dtMax.ToString("0.###", c) +
                ",\"gcAllocKiB\":" + (gcRecorder.Valid ? (gcBytes / 1024.0).ToString("0.###", c) : "null") + ",\"gcCollections\":" + (gc - gcAtSecond) +
                ",\"scene\":\"" + SceneManager.GetActiveScene().name + "\"}");
            secondStart = realtime; count = 0; gcBytes = 0; dtMax = 0; gcAtSecond = gc;
        }
    }

    void OnApplicationQuit() => Restore();
    void OnDestroy() => Restore();
}

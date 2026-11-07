using System;
using System.Diagnostics;
using System.Globalization;
using Mirror;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;
using UnityEngine.SceneManagement;

// R01 diagnostics only. Active with -online-hitch-probe (Development builds); otherwise it installs nothing.
// It marks the start of each PlayerLoop phase so a long frame can be attributed to a phase, to GC, to the motion
// capture itself (format vs disk) or to time spent outside the loop, without depending on the motion log.
// It never changes timing, buffers or any gameplay value.
public static class FrameHitchProbe
{
    struct M0 { } struct M1 { } struct M2 { } struct M3 { } struct M4 { } struct M5 { } struct M6 { } struct M7 { }
    static readonly string[] PhaseNames = { "time", "early", "fixed", "pre", "update", "preLate", "postLate" };
    static readonly long[] marks = new long[8];
    static bool active;
    static double hitchMs = 50;
    static int lastGc0, lastGc1, lastGc2, frames, secondHitches, secondFrames;
    static long lastFormat, lastIo, lastRows, secondFormat, secondIo;
    static double secondMax, secondStart = -1, previousClockOffset = double.NaN;
    static bool wasPaused;
    static readonly CultureInfo C = CultureInfo.InvariantCulture;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        var args = Environment.GetCommandLineArgs();
        if (!UnityEngine.Debug.isDebugBuild || Array.IndexOf(args, "-online-hitch-probe") < 0) return;
        int at = Array.IndexOf(args, "-online-hitch-ms");
        if (at >= 0 && at + 1 < args.Length && double.TryParse(args[at + 1], NumberStyles.Float, C, out double ms) && ms >= 20 && ms <= 2000) hitchMs = ms;
        var loop = PlayerLoop.GetCurrentPlayerLoop();
        Type[] markers = { typeof(M0), typeof(M1), typeof(M2), typeof(M3), typeof(M4), typeof(M5), typeof(M6) };
        Type[] phases = { typeof(TimeUpdate), typeof(EarlyUpdate), typeof(FixedUpdate), typeof(PreUpdate), typeof(Update), typeof(PreLateUpdate), typeof(PostLateUpdate) };
        PlayerLoopSystem.UpdateFunction[] marks_ = { Mark0, Mark1, Mark2, Mark3, Mark4, Mark5, Mark6 };
        for (int i = 0; i < loop.subSystemList.Length; i++)
        {
            int p = Array.IndexOf(phases, loop.subSystemList[i].type);
            if (p < 0) continue;
            var list = new System.Collections.Generic.List<PlayerLoopSystem>(loop.subSystemList[i].subSystemList ?? new PlayerLoopSystem[0]);
            list.Insert(0, new PlayerLoopSystem { type = markers[p], updateDelegate = marks_[p] });
            if (p == 6) list.Add(new PlayerLoopSystem { type = typeof(M7), updateDelegate = End });
            loop.subSystemList[i].subSystemList = list.ToArray();
        }
        PlayerLoop.SetPlayerLoop(loop);
        active = true;
        lastGc0 = GC.CollectionCount(0); lastGc1 = GC.CollectionCount(1); lastGc2 = GC.CollectionCount(2);
        SceneManager.sceneLoaded += (scene, mode) => Log("scene", "\"event\":\"loaded\",\"name\":\"" + scene.name + "\",\"mode\":\"" + mode + "\"");
        SceneManager.sceneUnloaded += scene => Log("scene", "\"event\":\"unloaded\",\"name\":\"" + scene.name + "\"");
        UnityEngine.Debug.Log("[HitchProbe] installed thresholdMs=" + hitchMs.ToString("F0", C) + " motionBudget=" + GameplayLatencyDiagnostics.EventBudget);
    }

    // The frame is evaluated at the START of the next frame (Mark0): its wall time is then known exactly
    // (previous TimeUpdate start to this one), including time outside the loop (sleep, native waits, GC pauses between phases).
    static void Mark0() { long now = Stopwatch.GetTimestamp(); if (havePrev) Evaluate(now); marks[0] = now; for (int i = 1; i < 8; i++) marks[i] = 0; havePrev = true; }
    static void Mark1() => marks[1] = Stopwatch.GetTimestamp();
    static void Mark2() => marks[2] = Stopwatch.GetTimestamp();
    static void Mark3() => marks[3] = Stopwatch.GetTimestamp();
    static void Mark4() => marks[4] = Stopwatch.GetTimestamp();
    static void Mark5() => marks[5] = Stopwatch.GetTimestamp();
    static void Mark6() => marks[6] = Stopwatch.GetTimestamp();
    static bool havePrev;

    static double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

    static void Log(string kind, string body)
    {
        UnityEngine.Debug.Log("[HitchProbe] {\"kind\":\"" + kind + "\",\"frame\":" + Time.frameCount + ",\"realtime\":" + Time.realtimeSinceStartupAsDouble.ToString("F3", C) +
            ",\"ptime\":" + (NetworkTime.time).ToString("F3", C) + "," + body + "}");
    }

    static void End() { if (active) marks[7] = Stopwatch.GetTimestamp(); }

    static void Evaluate(long now)
    {
        if (!active) return;
        double frameMs = Ms(now - marks[0]);
        double loopMs = marks[7] != 0 ? Ms(marks[7] - marks[0]) : frameMs;
        int g0 = GC.CollectionCount(0), g1 = GC.CollectionCount(1), g2 = GC.CollectionCount(2);
        long format = GameplayLatencyDiagnostics.FormatTicks, io = GameplayLatencyDiagnostics.IoTicks, rows = GameplayLatencyDiagnostics.RowsWritten;
        double formatMs = Ms(format - lastFormat), ioMs = Ms(io - lastIo);
        double realtime = Time.realtimeSinceStartupAsDouble;
        if (secondStart < 0) secondStart = realtime;
        secondFrames++; secondFormat += format - lastFormat; secondIo += io - lastIo;
        if (frameMs > secondMax) secondMax = frameMs;
        bool paused = GamePauseController.IsPaused;
        if (paused != wasPaused) { wasPaused = paused; Log("pause", "\"paused\":" + (paused ? "true" : "false")); }
        if (frameMs >= hitchMs)
        {
            secondHitches++;
            var sb = new System.Text.StringBuilder();
            sb.Append("\"dtMs\":").Append(frameMs.ToString("F1", C)).Append(",\"loopMs\":").Append(loopMs.ToString("F1", C))
              .Append(",\"outsideMs\":").Append((frameMs - loopMs).ToString("F1", C)).Append(",\"phasesMs\":{");
            for (int i = 0; i < 7; i++)
            {
                double d = 0;
                if (marks[i] != 0)
                {
                    int next = i + 1;
                    while (next < 7 && marks[next] == 0) next++;
                    long end = next == 7 ? marks[7] : marks[next];
                    d = end != 0 ? Ms(end - marks[i]) : 0;   // a frame that never reached End (scene load) keeps its phase unknown (0)
                }
                sb.Append(i == 0 ? "" : ",").Append('"').Append(PhaseNames[i]).Append("\":").Append(d.ToString("F1", C));
            }
            sb.Append("},\"gc0\":").Append(g0 - lastGc0).Append(",\"gc1\":").Append(g1 - lastGc1).Append(",\"gc2\":").Append(g2 - lastGc2)
              .Append(",\"managedMiB\":").Append((GC.GetTotalMemory(false) / 1048576.0).ToString("F1", C))
              .Append(",\"captureFormatMs\":").Append(formatMs.ToString("F2", C)).Append(",\"captureIoMs\":").Append(ioMs.ToString("F2", C))
              .Append(",\"captureRows\":").Append(rows - lastRows).Append(",\"captureDropped\":").Append(GameplayLatencyDiagnostics.DroppedRows)
              .Append(",\"paused\":").Append(paused ? "true" : "false").Append(",\"scene\":\"").Append(SceneManager.GetActiveScene().name).Append("\"");
            Log("hitch", sb.ToString());
        }
        if (NetworkClient.active && !NetworkServer.active)
        {
            double offset = NetworkTime.time - NetworkTime.localTime;
            if (!double.IsNaN(previousClockOffset) && Math.Abs(offset - previousClockOffset) * 1000 >= 5)
                Log("clock", "\"deltaMs\":" + ((offset - previousClockOffset) * 1000).ToString("F1", C) + ",\"rttMs\":" + (NetworkTime.rtt * 1000).ToString("F1", C) +
                    ",\"rttVarMs\":" + (Math.Sqrt(Math.Max(0, NetworkTime.rttVariance)) * 1000).ToString("F1", C) + ",\"dtMs\":" + frameMs.ToString("F1", C));
            previousClockOffset = offset;
        }
        if (realtime - secondStart >= 1)
        {
            Log("second", "\"frames\":" + secondFrames + ",\"maxDtMs\":" + secondMax.ToString("F1", C) + ",\"hitches\":" + secondHitches +
                ",\"gc0\":" + (g0 - lastGc0Second) + ",\"gc1\":" + (g1 - lastGc1Second) + ",\"gc2\":" + (g2 - lastGc2Second) +
                ",\"captureFormatMs\":" + Ms(secondFormat).ToString("F1", C) + ",\"captureIoMs\":" + Ms(secondIo).ToString("F1", C) +
                ",\"captureRows\":" + (rows - lastRowsSecond) + ",\"captureDropped\":" + GameplayLatencyDiagnostics.DroppedRows);
            secondStart = realtime; secondMax = 0; secondHitches = secondFrames = 0; secondFormat = secondIo = 0;
            lastGc0Second = g0; lastGc1Second = g1; lastGc2Second = g2; lastRowsSecond = rows;
        }
        lastGc0 = g0; lastGc1 = g1; lastGc2 = g2; lastFormat = format; lastIo = io; lastRows = rows;
    }
    static int lastGc0Second, lastGc1Second, lastGc2Second;
    static long lastRowsSecond;
}

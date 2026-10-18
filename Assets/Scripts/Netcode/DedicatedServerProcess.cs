using System;
using System.IO;
using System.Diagnostics;
using kcp2k;
using UnityEngine;
using UnityEngine.Profiling;
using Unity.Profiling;
using Mirror;
using Debug = UnityEngine.Debug;

namespace Assets.Scripts.Netcode
{
    public partial class BombermanNetworkManager
    {
        float dedicatedLastOccupied, dedicatedNextReport;
        int dedicatedIdleSeconds;
        string dedicatedRoomId, dedicatedStatusPath, dedicatedToken;
        Process dedicatedProcess;
        double dedicatedCpuTime;
        float dedicatedMetricsStart, dedicatedNextStatus, dedicatedMaxFrame;
        int dedicatedFrames, dedicatedGcCount;
        readonly int[] dedicatedFrameHistogram = new int[4097];
        int dedicatedSlowFrames;
        ulong dedicatedMetricsSceneHandle;
        bool dedicatedWindowIncludesTransition;
        long dedicatedGcAllocated;
        ProfilerRecorder dedicatedGcRecorder;
        long dedicatedIncomingBytes, dedicatedOutgoingBytes;
        bool dedicatedNetworkDiagnosticsReady;
        readonly string[] dedicatedMarkerNames = { BattleModePerformanceMarkers.PlayerUpdateName,
            BattleModePerformanceMarkers.PlayerFixedUpdateName, BattleModePerformanceMarkers.BombControllerUpdateName,
            BattleModePerformanceMarkers.ComUpdateName, BattleModePerformanceMarkers.ComThinkName,
            BattleModePerformanceMarkers.AbilityUpdateName, BattleModePerformanceMarkers.ArenaUpdateName,
            BattleModePerformanceMarkers.InputUpdateName, "SB6.Coop.Presentation" };
        ProfilerRecorder[] dedicatedRecorders;
        double[] dedicatedMarkerTotals;
        string dedicatedLogDirectory;
        readonly object dedicatedLogLock = new();

        [Serializable]
        sealed class DedicatedEvent
        {
            public string utc, room, level, message, stack;
        }

        [Serializable]
        sealed class DedicatedStatus
        {
            public string token;
            public int players;
            public bool available;
        }

        [Serializable]
        sealed class DedicatedMetrics
        {
            public int metricsVersion = 2;
            public bool includesTransition;
            public string utc, room, scene;
            public int players, objects, gcCollections;
            public float fps, maxFrameMs, p50FrameMs, p95FrameMs, p99FrameMs;
            public int frames, framesOver33Ms;
            public double gcAllocatedKiBPerSecond;
            public double cpuPercentOneCore, residentMiB, managedMiB, unityAllocatedMiB;
            public double incomingKiBPerSecond, outgoingKiBPerSecond;
            public string[] markerNames;
            public double[] averageMarkerMs;
        }

        public static int ReadDedicatedNumber(string[] args, string option, int fallback, int minimum, int maximum)
        {
            int index = Array.IndexOf(args, option);
            if (index < 0) return fallback;
            if (index + 1 >= args.Length || !int.TryParse(args[index + 1], out int value) || value < minimum || value > maximum)
                throw new ArgumentException($"{option} must be between {minimum} and {maximum}.");
            return value;
        }

        void ConfigureDedicatedProcess()
        {
            var args = Environment.GetCommandLineArgs();
            int port = ReadDedicatedNumber(args, "-port", 7777, 1, ushort.MaxValue);
            int fps = ReadDedicatedNumber(args, "-server-fps", 60, 10, 240);
            dedicatedIdleSeconds = ReadDedicatedNumber(args, "-server-idle-seconds", 300, 0, 86400);
            if (transport is not KcpTransport kcp)
                throw new InvalidOperationException("Dedicated server requires KcpTransport.");
            kcp.Port = (ushort)port;
            Application.runInBackground = true;
            Application.targetFrameRate = fps;
            QualitySettings.vSyncCount = 0;
            dedicatedLastOccupied = Time.realtimeSinceStartup;
            dedicatedRoomId = Environment.GetEnvironmentVariable("SB6_ROOM_ID") ?? "standalone";
            dedicatedStatusPath = Environment.GetEnvironmentVariable("SB6_ROOM_STATUS");
            dedicatedToken = Environment.GetEnvironmentVariable("SB6_ROOM_TOKEN");
            if (!string.IsNullOrEmpty(dedicatedStatusPath))
            {
                dedicatedLogDirectory = Path.GetDirectoryName(dedicatedStatusPath);
                Application.logMessageReceivedThreaded += CaptureDedicatedEvent;
            }
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            dedicatedProcess = Process.GetCurrentProcess();
            dedicatedCpuTime = dedicatedProcess.TotalProcessorTime.TotalSeconds;
            dedicatedMetricsStart = Time.realtimeSinceStartup;
            dedicatedNextReport = dedicatedMetricsStart + 10;
            dedicatedGcCount = GC.CollectionCount(0);
            BattleModePerformanceMarkers.EnsureInitialized();
            dedicatedRecorders = new ProfilerRecorder[dedicatedMarkerNames.Length];
            dedicatedMarkerTotals = new double[dedicatedMarkerNames.Length];
            for (int i = 0; i < dedicatedRecorders.Length; i++)
                dedicatedRecorders[i] = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, dedicatedMarkerNames[i], 1,
                    ProfilerRecorderOptions.StartImmediately | ProfilerRecorderOptions.WrapAroundWhenCapacityReached |
                    ProfilerRecorderOptions.SumAllSamplesInFrame);
            dedicatedGcRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
            Debug.Log($"[Dedicated] configured UDP port={port}; fps={fps}; idleSeconds={dedicatedIdleSeconds}; protocol={OnlineVersionAuthenticator.ProtocolVersion}");
        }

        void TickDedicatedProcess()
        {
            if (!dedicatedServer || !Mirror.NetworkServer.active) return;
            if (!dedicatedNetworkDiagnosticsReady)
            {
                // A Linux server boots directly into the lobby. Mirror resets its
                // diagnostics in AfterSceneLoad, after our Awake configuration.
                // Subscribe on the first running frame, after those resets finish.
                NetworkDiagnostics.InMessageEvent += DedicatedIncoming;
                NetworkDiagnostics.OutMessageEvent += DedicatedOutgoing;
                dedicatedNetworkDiagnosticsReady = true;
            }
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            ulong sceneHandle = scene.handle.GetRawData();
            dedicatedWindowIncludesTransition |= sceneHandle != dedicatedMetricsSceneHandle ||
                NetworkServer.isLoadingScene || (GameManager.Instance != null && GameManager.Instance.OnlineRoundPresentationActive);
            dedicatedMetricsSceneHandle = sceneHandle;
            dedicatedFrames++;
            float frameMs = Time.unscaledDeltaTime * 1000;
            dedicatedFrameHistogram[Mathf.Clamp((int)Math.Ceiling(frameMs * 4), 0, dedicatedFrameHistogram.Length - 1)]++;
            if (frameMs > 33.333f) dedicatedSlowFrames++;
            if (dedicatedGcRecorder.Valid) dedicatedGcAllocated += dedicatedGcRecorder.LastValue;
            dedicatedMaxFrame = Mathf.Max(dedicatedMaxFrame, Time.unscaledDeltaTime);
            for (int i = 0; i < dedicatedRecorders.Length; i++)
                if (dedicatedRecorders[i].Valid) dedicatedMarkerTotals[i] += dedicatedRecorders[i].LastValue / 1000000d;
            if (playerIdByConnection.Count > 0) dedicatedLastOccupied = Time.realtimeSinceStartup;
            if (Time.realtimeSinceStartup >= dedicatedNextStatus)
            {
                dedicatedNextStatus = Time.realtimeSinceStartup + 2;
                if (!string.IsNullOrEmpty(dedicatedStatusPath))
                {
                    try
                    {
                        string temporary = dedicatedStatusPath + ".tmp";
                        File.WriteAllText(temporary, JsonUtility.ToJson(new DedicatedStatus
                        {
                            token = dedicatedToken, players = playerIdByConnection.Count,
                            available = !leavingRoom && AcceptingRoomPlayers
                        }), new System.Text.UTF8Encoding(false));
                        if (File.Exists(dedicatedStatusPath)) File.Delete(dedicatedStatusPath);
                        File.Move(temporary, dedicatedStatusPath);
                    }
                    catch (IOException error) { Debug.LogError("[Dedicated] status write failed: " + error.Message); }
                }
            }
            if (Time.realtimeSinceStartup >= dedicatedNextReport)
            {
                dedicatedNextReport = Time.realtimeSinceStartup + 10;
                dedicatedProcess.Refresh();
                double cpu = dedicatedProcess.TotalProcessorTime.TotalSeconds;
                float elapsed = Mathf.Max(0.001f, Time.realtimeSinceStartup - dedicatedMetricsStart);
                string metrics = JsonUtility.ToJson(new DedicatedMetrics
                {
                    utc = DateTime.UtcNow.ToString("O"), room = dedicatedRoomId,
                    scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                    includesTransition = dedicatedWindowIncludesTransition,
                    players = playerIdByConnection.Count, objects = Mirror.NetworkServer.spawned.Count,
                    fps = dedicatedFrames / elapsed, maxFrameMs = dedicatedMaxFrame * 1000,
                    frames = dedicatedFrames, framesOver33Ms = dedicatedSlowFrames,
                    p50FrameMs = DedicatedFramePercentile(0.50f), p95FrameMs = DedicatedFramePercentile(0.95f), p99FrameMs = DedicatedFramePercentile(0.99f),
                    gcAllocatedKiBPerSecond = dedicatedGcRecorder.Valid ? dedicatedGcAllocated / elapsed / 1024d : -1,
                    cpuPercentOneCore = (cpu - dedicatedCpuTime) / elapsed * 100,
                    residentMiB = dedicatedProcess.WorkingSet64 > 0 ? dedicatedProcess.WorkingSet64 / 1048576d : -1,  // Mono on Windows may not expose RSS.
                    managedMiB = GC.GetTotalMemory(false) / 1048576d,
                    unityAllocatedMiB = Profiler.GetTotalAllocatedMemoryLong() / 1048576d,
                    gcCollections = GC.CollectionCount(0) - dedicatedGcCount,
                    incomingKiBPerSecond = dedicatedIncomingBytes / elapsed / 1024d ,
                    outgoingKiBPerSecond = dedicatedOutgoingBytes / elapsed / 1024d ,
                    markerNames = dedicatedMarkerNames ,
                    averageMarkerMs = DedicatedMarkerAverages()
                });
                Debug.Log("[ServerMetrics] " + metrics);
                AppendDedicatedLog("metrics.jsonl", metrics);
                dedicatedCpuTime = cpu;
                dedicatedMetricsStart = Time.realtimeSinceStartup;
                dedicatedFrames = dedicatedSlowFrames = 0;
                dedicatedWindowIncludesTransition = false;
                dedicatedGcAllocated = 0;
                Array.Clear(dedicatedFrameHistogram, 0, dedicatedFrameHistogram.Length);
                dedicatedMaxFrame = 0;
                dedicatedGcCount = GC.CollectionCount(0);
                dedicatedIncomingBytes = dedicatedOutgoingBytes = 0;
                Array.Clear(dedicatedMarkerTotals, 0, dedicatedMarkerTotals.Length);
            }
            if (dedicatedIdleSeconds > 0 && Time.realtimeSinceStartup - dedicatedLastOccupied >= dedicatedIdleSeconds)
            {
                Debug.Log("[Dedicated] idle timeout; stopping room process.");
                StopServer();
                Application.Quit();
            }
        }

        float DedicatedFramePercentile(float percentile)
        {
            int target = Mathf.CeilToInt(dedicatedFrames * percentile), total = 0;
            for (int i = 0; i < dedicatedFrameHistogram.Length; i++)
            {
                total += dedicatedFrameHistogram[i];
                if (total >= target) return i == dedicatedFrameHistogram.Length - 1 ? dedicatedMaxFrame * 1000 : i * 0.25f;
            }
            return 0;
        }

        double[] DedicatedMarkerAverages()
        {
            var result = new double[dedicatedMarkerTotals.Length];
            for (int i = 0; i < result.Length; i++)
                result[i] = dedicatedRecorders[i].Valid ? dedicatedMarkerTotals[i] / Mathf.Max(1, dedicatedFrames) : -1;
            return result;
        }

        void DedicatedIncoming(NetworkDiagnostics.MessageInfo info) => dedicatedIncomingBytes += (long)info.bytes * info.count;
        void DedicatedOutgoing(NetworkDiagnostics.MessageInfo info) => dedicatedOutgoingBytes += (long)info.bytes * info.count;

        void DisposeDedicatedMetrics()
        {
            Application.logMessageReceivedThreaded -= CaptureDedicatedEvent;
            NetworkDiagnostics.InMessageEvent -= DedicatedIncoming;
            NetworkDiagnostics.OutMessageEvent -= DedicatedOutgoing;
            if (dedicatedRecorders != null)
                foreach (var recorder in dedicatedRecorders) recorder.Dispose();
            dedicatedGcRecorder.Dispose();
            dedicatedProcess?.Dispose();
        }

        void CaptureDedicatedEvent(string message, string stack, LogType type)
        {
            if (type == LogType.Log && !message.StartsWith("[Room]", StringComparison.Ordinal) &&
                !message.StartsWith("[Dedicated]", StringComparison.Ordinal) &&
                !message.StartsWith("[WorldMount]", StringComparison.Ordinal)) return;
            AppendDedicatedLog("events.jsonl", JsonUtility.ToJson(new DedicatedEvent
            {
                utc = DateTime.UtcNow.ToString("O"), room = dedicatedRoomId,
                level = type.ToString(), message = message, stack = stack
            }));
        }

        void AppendDedicatedLog(string file, string line)
        {
            if (string.IsNullOrEmpty(dedicatedLogDirectory)) return;
            lock (dedicatedLogLock)
            {
                try { File.AppendAllText(Path.Combine(dedicatedLogDirectory, file), line + "\n", new System.Text.UTF8Encoding(false)); }
                catch (IOException) { /* Unity's primary log remains the fallback when the disk is full. */ }
                catch (UnauthorizedAccessException) { /* Preserve the original error without recursive logging. */ }
            }
        }
    }
}

using System;
using System.IO;
using System.Text;
using Assets.Scripts.Netcode;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GameplayLatencyDiagnostics
{
    public const string PreferenceKey = "Diagnostics.GameplayLatency";
    public const string BuildSettingsPath = "GameplayLatencyDiagnostics";
    // R01: 8 MiB ended 90 s Development smokes at ~57 s (capture_limit). The larger cap is Development-only so a release
    // build with the capture enabled keeps its previous disk footprint (R01 cost measurement, 07/11/2026).
    static long maxCaptureBytes = 64L * 1024 * 1024;
    static long MaxBytes => Debug.isDebugBuild ? maxCaptureBytes : 8L * 1024 * 1024;
    const double MaxSeconds = 600;
    const int DefaultEventsPerSecond = 180;
    static int maxEventsPerSecond = DefaultEventsPerSecond; // R01: -online-motion-log-budget N changes capture density only; default unchanged
    // R01: cost of the capture itself, read by FrameHitchProbe (ticks of Stopwatch.GetTimestamp).
    public static long FormatTicks, IoTicks, RowsWritten;
    public static int DroppedRows => dropped;
    public static int EventBudget => maxEventsPerSecond;

    [Serializable] public sealed class Settings { public bool enabled = false; }
    [Serializable] public sealed class Entry
    {
        public string kind, detail, utc, scene, role, version, build, device;
        public int frame, player, entity, held, pressed, dropped, sentCommands, receivedCommands, protocol, bombEntity;
        public uint netId, bombNetId, inputTick, authoritativeTick;
        public int pendingInputs, queuedInputs;
        public double messageAgeMs;
        public Vector2 position, delta, direction, bombPosition, predictionError;
        public bool accepted, localPlayer, physicsAuthority;
        public double realtime, timeline, rttMs, rttJitterMs, bufferMs, durationMs = -1;
        public float frameMs, fixedStepMs, sendIntervalMs;
        public float kickDistance, kickAxisOffset, kickPerpendicularOffset;
        public int positionSnapshots;
    }

    static bool initialized, enabled;
    static StreamWriter writer;
    static long bytes;
    static double started, flushAt, budgetWindow;
    static int budgetCount, dropped;
    public static string CurrentFile { get; private set; }
    public static bool Enabled { get { Initialize(); return enabled; } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        Close();
        initialized = false;
        CurrentFile = null;
        bytes = budgetCount = dropped = 0;
        FormatTicks = IoTicks = RowsWritten = 0;
        maxEventsPerSecond = DefaultEventsPerSecond;
        maxCaptureBytes = 64L * 1024 * 1024;
        started = flushAt = budgetWindow = 0;
        Application.quitting -= Close;
        Application.quitting += Close;
    }

    static void Initialize()
    {
        if (initialized) return;
        initialized = true;
        var settings = Resources.Load<TextAsset>(BuildSettingsPath);
        bool inBuild = settings != null && JsonUtility.FromJson<Settings>(settings.text)?.enabled == true;
        var args = Environment.GetCommandLineArgs();
        enabled = inBuild || PlayerPrefs.GetInt(PreferenceKey, 0) != 0 ||
            Array.IndexOf(args, "-gameplay-latency-logs") >= 0 || Array.IndexOf(args, "-online-motion-logs") >= 0;
        if (Array.IndexOf(args, "-online-motion-logs-off") >= 0) enabled = false; // R01: lets a hitch run have no capture although the build setting enables it
        int budgetArg = Array.IndexOf(args, "-online-motion-log-budget");
        if (budgetArg >= 0 && budgetArg + 1 < args.Length && int.TryParse(args[budgetArg + 1], out int budget) && budget >= DefaultEventsPerSecond && budget <= 5000)
            maxEventsPerSecond = budget;
        int limitArg = Array.IndexOf(args, "-online-motion-log-limit-mib");
        if (Debug.isDebugBuild && limitArg >= 0 && limitArg + 1 < args.Length &&
            int.TryParse(args[limitArg + 1], out int limit) && limit >= 64 && limit <= 256)
            maxCaptureBytes = (long)limit * 1024 * 1024;
    }

    public static void SetEnabled(bool value)
    {
        Initialize();
        enabled = value;
        PlayerPrefs.SetInt(PreferenceKey, value ? 1 : 0);
        PlayerPrefs.Save();
        if (!value) Close();
    }

    public static Entry For(string kind, MovementController movement = null, Bomb bomb = null, string detail = null)
    {
        var target = movement != null ? movement.gameObject : bomb != null ? bomb.gameObject : null;
        uint id = target != null && target.TryGetComponent<NetworkIdentity>(out var identity) ? identity.netId : 0;
        var input = movement != null ? movement.GetComponent<NetworkPlayerInput>() : null;
        return new Entry
        {
            kind = kind, detail = detail, player = movement != null ? movement.PlayerId : 0,
            inputTick = input != null ? input.CurrentInputTick : 0,
            pendingInputs = input != null ? input.PendingInputCount : 0,
            queuedInputs = input != null ? input.QueuedInputCount : 0,
            entity = target != null ? target.GetEntityId().GetHashCode() : 0, netId = id,
            position = target != null ? (Vector2)target.transform.position : Vector2.zero,
            direction = movement != null ? movement.Direction : Vector2.zero,
            localPlayer = target != null && target.TryGetComponent<NetworkIdentity>(out var localIdentity) && localIdentity.isLocalPlayer,
            physicsAuthority = NetSync.ShouldSimulateLocally,
            bombEntity = bomb != null ? bomb.GetEntityId().GetHashCode() : 0,
            bombPosition = bomb != null ? (Vector2)bomb.transform.position : Vector2.zero,
            bombNetId = bomb != null && bomb.TryGetComponent<NetworkIdentity>(out var bombIdentity) ? bombIdentity.netId : 0
        };
    }

    public static void Write(Entry entry)
    {
        if (!Enabled) return;
        double now = Time.realtimeSinceStartupAsDouble;
        if (now - budgetWindow >= 1) { budgetWindow = now; budgetCount = 0; }
        if (++budgetCount > maxEventsPerSecond) { dropped++; return; }
        try
        {
            if (writer == null)
            {
                string directory = Path.Combine(Application.persistentDataPath, "Diagnostics", "Movement");
                Directory.CreateDirectory(directory);
                CurrentFile = Path.Combine(directory, "motion-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".jsonl");
                writer = new StreamWriter(CurrentFile, false, new UTF8Encoding(false), 32768);
                bytes = 0; started = now; flushAt = now + 1;
                writer.WriteLine(JsonUtility.ToJson(new Entry { kind = "capture_start", utc = DateTime.UtcNow.ToString("O"),
                    version = Application.version, build = OnlineVersionAuthenticator.BuildIdentity, protocol = OnlineVersionAuthenticator.ProtocolVersion,
                    detail = "Local elapsed durations are exact to the rendered frame; timeline is interpolated, not a shared wall clock. Input bits: Up=1 Down=2 Left=4 Right=8 A=16 B=32 C=64 Select=128 L=256 R=512. Contact-to-feedback durations use one local clock; authoritativeTick correlates kick feedback with the server input tick. Budget=" + maxEventsPerSecond + ";limitMiB=" + (MaxBytes / (1024 * 1024)),
                    device = Application.platform.ToString() }));
                Debug.Log("[GameplayLatency] Capture: " + CurrentFile);
            }
            if (bytes >= MaxBytes || now - started >= MaxSeconds)
            {
                writer.WriteLine("{\"kind\":\"capture_limit\"}");
                Close(); enabled = false;
                Debug.Log("[GameplayLatency] Capture stopped at " + (MaxBytes / (1024 * 1024)) + " MiB or 10 minutes. Enable again for another capture.");
                return;
            }
            entry.utc = DateTime.UtcNow.ToString("O");
            entry.realtime = now; entry.timeline = NetSync.IsOnline ? NetworkTime.time : now;
            entry.frame = Time.frameCount; entry.scene = SceneManager.GetActiveScene().name;
            entry.role = !NetSync.IsOnline ? "offline" : NetworkServer.active ? NetworkClient.active ? "host" : "server" : "client";
            entry.frameMs = Time.unscaledDeltaTime * 1000; entry.fixedStepMs = Time.fixedDeltaTime * 1000;
            entry.rttMs = NetworkClient.active && !NetworkServer.active ? NetworkTime.rtt * 1000 : 0;
            entry.rttJitterMs = NetworkClient.active && !NetworkServer.active ? Math.Sqrt(Math.Max(0, NetworkTime.rttVariance)) * 1000 : 0;
            entry.bufferMs = NetworkClient.active && !NetworkServer.active ? NetworkClient.bufferTime * 1000 : 0;
            entry.sendIntervalMs = NetSync.IsOnline ? NetworkServer.sendInterval * 1000 : 0;
            entry.dropped = dropped;
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            string line = JsonUtility.ToJson(entry);
            bytes += Encoding.UTF8.GetByteCount(line) + 1;
            long t1 = System.Diagnostics.Stopwatch.GetTimestamp();
            writer.WriteLine(line);
            if (now >= flushAt) { writer.Flush(); flushAt = now + 1; }
            FormatTicks += t1 - t0; IoTicks += System.Diagnostics.Stopwatch.GetTimestamp() - t1; RowsWritten++;
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
        {
            enabled = false;
            Close();
            Debug.LogWarning("[GameplayLatency] Capture disabled: " + exception.Message);
        }
    }

    public static void Kick(Bomb bomb, string kind, string reason, Vector2 direction)
    {
        if (!Enabled) return;
        var entry = For(kind, bomb: bomb, detail: reason);
        entry.direction = direction;
        Write(entry);
    }

    public static void Close()
    {
        try { writer?.Dispose(); }
        catch (IOException) { /* Diagnostics must not interrupt gameplay on storage failure. */ }
        finally { writer = null; }
    }
}

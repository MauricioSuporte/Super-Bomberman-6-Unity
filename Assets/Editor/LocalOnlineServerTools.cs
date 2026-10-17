using System;
using System.Diagnostics;
using System.IO;
using Assets.Scripts.Netcode;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>Runs the same bounded directory and dedicated processes on loopback.</summary>
public static class LocalOnlineServerTools
{
    const string Menu = "Tools/Super Bomberman 6/Online/";
    const string StopKey = "SB6.LocalServer.StopFile";
    const string PidKey = "SB6.LocalServer.Pid";
    const string StartKey = "SB6.LocalServer.StartTime";

    [MenuItem(Menu + "Build and Start Local Server")]
    public static void BuildAndStart()
    {
        if (EditorApplication.isPlaying || BuildPipeline.isBuildingPlayer)
            throw new InvalidOperationException("Stop Play Mode and wait for the current build first.");
        Stop();
        DedicatedServerBuild.BuildWindowsTestServer();
        Start();
    }

    [MenuItem(Menu + "Start Local Server (Existing Build)")]
    public static void Start()
    {
        if (IsRunning()) { UseLocal(); return; }
        string root = Directory.GetParent(Application.dataPath).FullName;
        string binary = Path.Combine(root, "Build/WindowsServer/SB6Server.exe");
        if (!File.Exists(binary))
            throw new FileNotFoundException("Use Build and Start Local Server first.", binary);
        string logs = Path.Combine(root, "Build/LocalServerLogs");
        Directory.CreateDirectory(logs);
        string stop = Path.Combine(logs, "stop-" + Guid.NewGuid().ToString("N"));
        // SB6_PYTHON can point to an installed Python executable when python is not on PATH.
        string python = Environment.GetEnvironmentVariable("SB6_PYTHON");
        if (string.IsNullOrWhiteSpace(python)) python = "python";
        var start = new ProcessStartInfo(python,
            Quote(Path.Combine(root, "Tools/RoomDirectory/supervisor.py")) +
            " --binary " + Quote(binary) + " --host 127.0.0.1 --bind 127.0.0.1 --port 18081" +
            " --first-port 17777 --capacity 4 --logs " + Quote(logs) + " --stop-file " + Quote(stop))
        {
            UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = root,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        using var process = Process.Start(start);
        SessionState.SetInt(PidKey, process.Id);
        SessionState.SetString(StartKey, process.StartTime.ToUniversalTime().Ticks.ToString());
        SessionState.SetString(StopKey, stop);
        UseLocal();
        Debug.Log("[LocalServer] Starting API at " + OnlineRoomRules.LocalDirectory +
            "; UDP 17777–17780. Logs: " + logs);
    }

    static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";

    static bool IsRunning()
    {
        int pid = SessionState.GetInt(PidKey, 0);
        if (pid <= 0) return false;
        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited && process.StartTime.ToUniversalTime().Ticks.ToString() ==
                SessionState.GetString(StartKey, "");
        }
        catch (ArgumentException) { return false; }
        catch (InvalidOperationException) { return false; }
    }

    [MenuItem(Menu + "Stop Local Server")]
    public static void Stop()
    {
        if (IsRunning())
        {
            string stop = SessionState.GetString(StopKey, "");
            if (!string.IsNullOrEmpty(stop)) File.WriteAllText(stop, "stop", new System.Text.UTF8Encoding(false));
            using var process = Process.GetProcessById(SessionState.GetInt(PidKey, 0));
            if (!process.WaitForExit(7000))
                throw new InvalidOperationException("Local server is still stopping; wait and try again. See Local Server Logs.");
        }
        SessionState.EraseInt(PidKey);
    }

    [MenuItem(Menu + "Use Local Server")]
    public static void UseLocal()
    {
        PlayerPrefs.SetString(OnlineRoomRules.DirectoryOverrideKey, OnlineRoomRules.LocalDirectory);
        PlayerPrefs.Save();
        Debug.Log("[LocalServer] Online lobby uses loopback. Re-enter OnlineLobby to apply.");
    }

    [MenuItem(Menu + "Use Public Server")]
    public static void UsePublic()
    {
        PlayerPrefs.DeleteKey(OnlineRoomRules.DirectoryOverrideKey);
        PlayerPrefs.Save();
        Debug.Log("[LocalServer] Online lobby uses the configured public server. Re-enter OnlineLobby to apply.");
    }

    [MenuItem(Menu + "Open Local Server Logs")]
    public static void OpenLogs()
    {
        string logs = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Build/LocalServerLogs");
        Directory.CreateDirectory(logs);
        EditorUtility.RevealInFinder(logs);
    }
}

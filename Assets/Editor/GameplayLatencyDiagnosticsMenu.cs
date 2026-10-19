using System.IO;
using UnityEditor;
using UnityEngine;

public static class GameplayLatencyDiagnosticsMenu
{
    const string Root = "Tools/Super Bomberman 6/Diagnostics/Movement and Kick/";
    const string SettingsPath = "Assets/Resources/GameplayLatencyDiagnostics.json";

    [MenuItem(Root + "Start Capture (This Device)")]
    public static void StartCapture()
    {
        GameplayLatencyDiagnostics.SetEnabled(true);
        Debug.Log("[GameplayLatency] Capture enabled for this Editor. Play Offline or Online; files: " + LogDirectory);
    }

    [MenuItem(Root + "Stop Capture (This Device)")]
    public static void StopCapture()
    {
        GameplayLatencyDiagnostics.SetEnabled(false);
        Debug.Log("[GameplayLatency] Capture stopped. Files: " + LogDirectory);
    }

    [MenuItem(Root + "Capture in Builds")]
    public static void ToggleBuildCapture()
    {
        var settings = ReadBuildSettings();
        settings.enabled = !settings.enabled;
        File.WriteAllText(SettingsPath, JsonUtility.ToJson(settings, true) + "\n", new System.Text.UTF8Encoding(false));
        AssetDatabase.ImportAsset(SettingsPath);
        Debug.Log("[GameplayLatency] Capture in subsequent builds: " + settings.enabled);
    }

    [MenuItem(Root + "Capture in Builds", true)]
    static bool ValidateBuildCapture()
    {
        Menu.SetChecked(Root + "Capture in Builds", ReadBuildSettings().enabled);
        return true;
    }

    [MenuItem(Root + "Open Log Folder")]
    public static void OpenLogFolder()
    {
        Directory.CreateDirectory(LogDirectory);
        EditorUtility.RevealInFinder(LogDirectory);
    }

    static string LogDirectory => Path.Combine(Application.persistentDataPath, "Diagnostics", "Movement");
    static GameplayLatencyDiagnostics.Settings ReadBuildSettings() => File.Exists(SettingsPath)
        ? JsonUtility.FromJson<GameplayLatencyDiagnostics.Settings>(File.ReadAllText(SettingsPath, System.Text.Encoding.UTF8))
        : new GameplayLatencyDiagnostics.Settings();
}

using System.Linq;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// F5d — build do servidor dedicado headless (Linux, subtarget Server, Mono).
/// Dispare por: menu "Build > Linux Dedicated Server" (no editor, com barra de
/// progresso), ou por linha de comando em batchmode:
///   Unity -batchmode -quit -projectPath &lt;proj&gt; -executeMethod DedicatedServerBuild.BuildLinuxServer -logFile -
/// Saída: Build/LinuxServer/SB6.x86_64
/// </summary>
public static class DedicatedServerBuild
{
    [MenuItem("Build/Linux Dedicated Server")]
    public static void BuildLinuxServer() => Build(BuildTarget.StandaloneLinux64,
        "Build/LinuxServer/SB6.x86_64", StandaloneBuildSubtarget.Server);

    [MenuItem("Build/Linux Dedicated Server (Profiling)")]
    public static void BuildLinuxProfilingServer() => Build(BuildTarget.StandaloneLinux64,
        "Build/LinuxServerBenchmark/SB6.x86_64", StandaloneBuildSubtarget.Server, BuildOptions.Development);

    [MenuItem("Build/Windows Headless Server (Local Test)")]
    public static void BuildWindowsTestServer() => Build(BuildTarget.StandaloneWindows64,
        "Build/WindowsServer/SB6Server.exe", StandaloneBuildSubtarget.Player);

    // Invoked by the isolated Windows Actions checkout. All outputs are release.
    public static void BuildReleaseArtifacts()
    {
        var timer = System.Diagnostics.Stopwatch.StartNew();
        BuildLinuxServer();
        Debug.Log($"[ReleaseTiming] Linux server: {timer.Elapsed.TotalSeconds:F1}s");
        timer.Restart();
        OnlineClientBuild.BuildWindows();
        Debug.Log($"[ReleaseTiming] Windows client: {timer.Elapsed.TotalSeconds:F1}s");
    }

    static void Build(BuildTarget target, string outputPath, StandaloneBuildSubtarget subtarget,
        BuildOptions options = BuildOptions.None)
    {
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, target))
            throw new System.InvalidOperationException("Install the Unity build support module for " + target + " first.");
        var buildGroup = subtarget == StandaloneBuildSubtarget.Server ? NamedBuildTarget.Server : NamedBuildTarget.Standalone;
        var previousBackend = PlayerSettings.GetScriptingBackend(buildGroup);
        var previousSubtarget = EditorUserBuildSettings.standaloneBuildSubtarget;

        // O servidor DEVE bootar na OnlineLobby (onde vive o NetworkManager).
        // Forçamos ela como cena 0 aqui, sem depender da ordem do EditorBuildSettings
        // (que é temp e se perde em checkout/rebase). Isso vale só para o build.
        const string LobbyScene = "Assets/Scenes/OnlineLobby.unity";
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToList();
        scenes.Remove(LobbyScene);
        scenes.Insert(0, LobbyScene);

        var opts = new BuildPlayerOptions
        {
            scenes = scenes.ToArray(),
            locationPathName = outputPath,
            target = target,
            targetGroup = BuildTargetGroup.Standalone,
            subtarget = (int)subtarget,
            options = options,
        };

        BuildReport report;
        try
        {
            PlayerSettings.SetScriptingBackend(buildGroup, ScriptingImplementation.Mono2x);
            EditorUserBuildSettings.standaloneBuildSubtarget = subtarget;
            report = BuildPipeline.BuildPlayer(opts);
        }
        finally
        {
            EditorUserBuildSettings.standaloneBuildSubtarget = previousSubtarget;
            PlayerSettings.SetScriptingBackend(buildGroup, previousBackend);
        }

        BuildSummary s = report.summary;
        Debug.Log($"[DedicatedServerBuild] result={s.result} errors={s.totalErrors} " +
                  $"sizeMB={(s.totalSize / (1024f * 1024f)):F0} out={s.outputPath}");

        if (s.result != BuildResult.Succeeded || s.totalErrors > 0 || !File.Exists(outputPath))
            throw new BuildFailedException("Dedicated server build did not produce a valid executable. Check installed platform modules and restart the Editor after installing them.");

        // em batchmode, sinaliza falha com exit code != 0
        if (s.result != BuildResult.Succeeded && (Application.isBatchMode))
            EditorApplication.Exit(1);
    }
}

using System.Linq;
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
    const string OutputPath = "Build/LinuxServer/SB6.x86_64";

    [MenuItem("Build/Linux Dedicated Server")]
    public static void BuildLinuxServer()
    {
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Server, ScriptingImplementation.Mono2x);
        EditorUserBuildSettings.standaloneBuildSubtarget = StandaloneBuildSubtarget.Server;

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
            locationPathName = OutputPath,
            target = BuildTarget.StandaloneLinux64,
            targetGroup = BuildTargetGroup.Standalone,
            subtarget = (int)StandaloneBuildSubtarget.Server,
            options = BuildOptions.Development,
        };

        BuildReport report = BuildPipeline.BuildPlayer(opts);
        // volta o subtarget pra Player para não confundir builds futuros do editor.
        EditorUserBuildSettings.standaloneBuildSubtarget = StandaloneBuildSubtarget.Player;

        BuildSummary s = report.summary;
        Debug.Log($"[DedicatedServerBuild] result={s.result} errors={s.totalErrors} " +
                  $"sizeMB={(s.totalSize / (1024f * 1024f)):F0} out={s.outputPath}");

        // em batchmode, sinaliza falha com exit code != 0
        if (s.result != BuildResult.Succeeded && (Application.isBatchMode))
            EditorApplication.Exit(1);
    }
}

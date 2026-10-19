using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// F5d — build do CLIENTE online (player gráfico normal) que inicia na TitleScreen
/// (Battle Mode: Online/Offline). Diferente do servidor dedicado, o cliente NÃO é headless: é
/// um player comum que renderiza o jogo e mostra o menu OnGUI de Join.
///
/// Dispare por: menu "Build > Online Client (Linux/Windows)".
/// Força a TitleScreen como cena 0 (independente do EditorBuildSettings) e o
/// subtarget Player (não Server). Saída em Build/OnlineClient/&lt;plataforma&gt;.
/// </summary>
public static class OnlineClientBuild
{
    const string EntryScene = "Assets/Scenes/TitleScreen.unity";

    [MenuItem("Build/Online Client (Linux)")]
    public static void BuildLinux()
    {
        Build(BuildTarget.StandaloneLinux64, "Build/OnlineClient/Linux/SB6.x86_64");
    }

    [MenuItem("Build/Online Client (Windows)")]
    public static void BuildWindows()
    {
        Build(BuildTarget.StandaloneWindows64, "Build/OnlineClient/Windows/Super Bomberman 6.exe");
    }

    [MenuItem("Tools/Super Bomberman 6/Online/Build Performance Test Player")]
    public static void BuildPerformancePlayer() => Build(BuildTarget.StandaloneWindows64,
        "Build/ServerBenchmark/Windows/SB6.exe", BuildOptions.Development);

    static void Build(BuildTarget target, string outputPath, BuildOptions options = BuildOptions.None)
    {
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        // Cliente = player gráfico normal (NÃO o subtarget Server).
        EditorUserBuildSettings.standaloneBuildSubtarget = StandaloneBuildSubtarget.Player;

        // TitleScreen como cena 0 (boot), sem depender da ordem do EditorBuildSettings.
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToList();
        scenes.Remove(EntryScene);
        scenes.Insert(0, EntryScene);

        var opts = new BuildPlayerOptions
        {
            scenes = scenes.ToArray(),
            locationPathName = outputPath,
            target = target,
            targetGroup = BuildTargetGroup.Standalone,
            subtarget = (int)StandaloneBuildSubtarget.Player,
            options = options,
        };

        BuildReport report = BuildPipeline.BuildPlayer(opts);
        BuildSummary s = report.summary;
        Debug.Log($"[OnlineClientBuild] {target} result={s.result} errors={s.totalErrors} " +
                  $"sizeMB={(s.totalSize / (1024f * 1024f)):F0} out={s.outputPath}");

        if (s.result != BuildResult.Succeeded || s.totalErrors > 0 || !System.IO.File.Exists(outputPath))
            throw new BuildFailedException("Online client build failed. See the build report.");
    }
}

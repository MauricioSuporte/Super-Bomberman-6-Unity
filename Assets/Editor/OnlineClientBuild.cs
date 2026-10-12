using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// F5d — build do CLIENTE online (player gráfico normal) que boota na OnlineLobby
/// (menu Host/Join). Diferente do servidor dedicado, o cliente NÃO é headless: é
/// um player comum que renderiza o jogo e mostra o menu OnGUI de Join.
///
/// Dispare por: menu "Build > Online Client (Linux/Windows)".
/// Força a OnlineLobby como cena 0 (independente do EditorBuildSettings) e o
/// subtarget Player (não Server). Saída em Build/OnlineClient/&lt;plataforma&gt;.
/// </summary>
public static class OnlineClientBuild
{
    const string LobbyScene = "Assets/Scenes/OnlineLobby.unity";

    [MenuItem("Build/Online Client (Linux)")]
    public static void BuildLinux()
    {
        Build(BuildTarget.StandaloneLinux64, "Build/OnlineClient/Linux/SB6.x86_64");
    }

    [MenuItem("Build/Online Client (Windows)")]
    public static void BuildWindows()
    {
        Build(BuildTarget.StandaloneWindows64, "Build/OnlineClient/Windows/SB6.exe");
    }

    static void Build(BuildTarget target, string outputPath)
    {
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        // Cliente = player gráfico normal (NÃO o subtarget Server).
        EditorUserBuildSettings.standaloneBuildSubtarget = StandaloneBuildSubtarget.Player;

        // OnlineLobby como cena 0 (boot), sem depender da ordem do EditorBuildSettings.
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToList();
        scenes.Remove(LobbyScene);
        scenes.Insert(0, LobbyScene);

        var opts = new BuildPlayerOptions
        {
            scenes = scenes.ToArray(),
            locationPathName = outputPath,
            target = target,
            targetGroup = BuildTargetGroup.Standalone,
            subtarget = (int)StandaloneBuildSubtarget.Player,
            options = BuildOptions.None, // release; Debug.Log ainda vai pro Player.log
        };

        BuildReport report = BuildPipeline.BuildPlayer(opts);
        BuildSummary s = report.summary;
        Debug.Log($"[OnlineClientBuild] {target} result={s.result} errors={s.totalErrors} " +
                  $"sizeMB={(s.totalSize / (1024f * 1024f)):F0} out={s.outputPath}");

        if (s.result != BuildResult.Succeeded && Application.isBatchMode)
            EditorApplication.Exit(1);
    }
}

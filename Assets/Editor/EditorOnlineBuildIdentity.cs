using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>Gives Play Mode the checkout identity for testing alongside matching release clients.</summary>
[InitializeOnLoad]
public static class EditorOnlineBuildIdentity
{
    const string StampPath = "Assets/Resources/OnlineBuildIdentity.txt";

    static EditorOnlineBuildIdentity()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.ExitingEditMode && !Application.isBatchMode)
                RefreshIdentity();
        };
    }

    [MenuItem("Tools/Super Bomberman 6/Online/Refresh Editor Build Identity")]
    public static void RefreshIdentity()
    {
        if (Application.isBatchMode || BuildPipeline.isBuildingPlayer) return;
        string root = Directory.GetParent(Application.dataPath).FullName;
        string identity = ResolveIdentity(root);
        string path = Path.Combine(root, StampPath);
        if (!File.Exists(path) || File.ReadAllText(path, Encoding.UTF8).Trim() != identity)
        {
            File.WriteAllText(path, identity, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(StampPath, ImportAssetOptions.ForceSynchronousImport);
        }
        if (identity != "local-development" && HasPlayerChanges(root))
            Debug.LogWarning("[Online Editor] Local player changes are present. Play Mode uses HEAD for " +
                "testing with the matching GitHub artifact. Gameplay changes may require a matching local server.");
        Debug.Log(identity == "local-development"
            ? "[Online Editor] Git identity is unavailable. Using local-development; test with a matching local server."
            : "[Online Editor] Play Mode build identity: " + identity);
    }

    public static string ResolveIdentity(string projectRoot)
    {
        try
        {
            string commit = Git(projectRoot, "rev-parse HEAD").Trim();
            if (commit.Length != 40) return "local-development";
            foreach (char c in commit)
                if (!(c >= '0' && c <= '9') && !(c >= 'a' && c <= 'f')) return "local-development";
            return commit;
        }
        catch (Exception error) when (error is IOException || error is InvalidOperationException ||
            error is System.ComponentModel.Win32Exception || error is UnauthorizedAccessException)
        {
            return "local-development";
        }
    }

    static bool HasPlayerChanges(string projectRoot)
    {
        try
        {
            return !string.IsNullOrWhiteSpace(Git(projectRoot,
                "status --porcelain --untracked-files=normal -- Assets Packages ProjectSettings " +
                "\":(exclude)Assets/Editor/**\" \":(exclude)Assets/Tests/EditMode/Editor/**\""));
        }
        catch (Exception error) when (error is IOException || error is InvalidOperationException ||
            error is System.ComponentModel.Win32Exception || error is UnauthorizedAccessException)
        {
            return true;
        }
    }

    static string Git(string root, string arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("git", arguments)
            {
                WorkingDirectory = root,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };
        process.Start();
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(3000))
        {
            process.Kill();
            throw new IOException("Git timed out while checking the Editor build identity.");
        }
        if (process.ExitCode != 0) throw new IOException(errors.GetAwaiter().GetResult());
        return output.GetAwaiter().GetResult();
    }
}

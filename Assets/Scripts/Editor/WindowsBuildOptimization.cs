#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Editor-only: never starts a build on reload or changes mobile build options.
[InitializeOnLoad]
public sealed class WindowsBuildOptimization : IPostprocessBuildWithReport
{
    public int callbackOrder => 1000;

    static WindowsBuildOptimization()
    {
        BuildPlayerWindow.RegisterBuildPlayerHandler(BuildFromWindow);
    }

    static bool IsWindows(BuildTarget target) =>
        target == BuildTarget.StandaloneWindows || target == BuildTarget.StandaloneWindows64;

    static void BuildFromWindow(BuildPlayerOptions options)
    {
        if (IsWindows(options.target))
        {
            options.options &= ~BuildOptions.CompressWithLz4;
            options.options |= BuildOptions.CompressWithLz4HC | BuildOptions.DetailedBuildReport;
        }

        BuildPlayerWindow.DefaultBuildMethods.BuildPlayer(options);
    }

    [MenuItem("Tools/Windows Build/Selected Audio/Quality 80 (candidate)")]
    static void Quality80() => SetSelectedQuality(0.8f);

    [MenuItem("Tools/Windows Build/Selected Audio/Quality 70 (candidate)")]
    static void Quality70() => SetSelectedQuality(0.7f);

    [MenuItem("Tools/Windows Build/Selected Audio/Quality 60 (music candidate)")]
    static void Quality60() => SetSelectedQuality(0.6f);

    [MenuItem("Tools/Windows Build/Selected Audio/Quality 100 (reference)")]
    static void Quality100() => SetSelectedQuality(1f);

    static void SetSelectedQuality(float quality)
    {
        int changed = 0;
        foreach (AudioClip clip in Selection.GetFiltered<AudioClip>(SelectionMode.Assets))
        {
            var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(clip)) as AudioImporter;
            if (importer == null)
                continue;

            // Copy the default to preserve sample rate, conversion and preload settings.
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.quality = quality;
            if (!importer.SetOverrideSampleSettings("Standalone", settings))
                throw new InvalidOperationException($"Could not set desktop audio override: {importer.assetPath}");
            importer.SaveAndReimport();
            changed++;
        }
        Debug.Log($"Desktop audio quality {quality:P0} applied to {changed} selected clips. Listening validation is required; Default and Android settings were preserved.");
    }

    [MenuItem("Tools/Windows Build/Export Latest Build Report")]
    static void ExportLatest()
    {
        BuildReport report = BuildReport.GetLatestReport();
        if (report == null || !IsWindows(report.summary.platform))
        {
            Debug.LogWarning("No Windows build report is available. This command does not start a build.");
            return;
        }
        ExportReport(report);
    }

    public void OnPostprocessBuild(BuildReport report)
    {
        if (!IsWindows(report.summary.platform))
            return;
        try
        {
            ExportReport(report);
        }
        catch (Exception exception)
        {
            // Reporting failure must not invalidate an otherwise usable player.
            Debug.LogWarning($"Windows build report export failed: {exception.Message}");
        }
    }

    static void ExportReport(BuildReport report)
    {
        string directory = Path.Combine("output", "WindowsBuildReports",
            DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
        Directory.CreateDirectory(directory);
        var rows = report.packedAssets.SelectMany(pack => pack.contents)
            .GroupBy(asset => new { asset.sourceAssetPath, asset.type })
            .Select(group => new
            {
                Path = group.Key.sourceAssetPath,
                Type = group.Key.type == null ? "Unknown" : group.Key.type.Name,
                Bytes = group.Aggregate(0UL, (sum, asset) => sum + asset.packedSize)
            }).OrderByDescending(row => row.Bytes).ToArray();

        var assets = new StringBuilder("Asset,Type,PackedBytes\n");
        foreach (var row in rows)
            assets.AppendLine($"{Csv(row.Path)},{Csv(row.Type)},{row.Bytes}");
        File.WriteAllText(Path.Combine(directory, "assets.csv"), assets.ToString());

        var categories = new StringBuilder("Type,PackedBytes\n");
        foreach (var group in rows.GroupBy(row => row.Type)
                     .OrderByDescending(group => group.Sum(row => (decimal)row.Bytes)))
            categories.AppendLine($"{Csv(group.Key)},{group.Sum(row => (decimal)row.Bytes)}");
        File.WriteAllText(Path.Combine(directory, "categories.csv"), categories.ToString());

        var files = new StringBuilder("File,Role,Bytes\n");
        foreach (var file in report.GetFiles().OrderByDescending(file => file.size))
            files.AppendLine($"{Csv(file.path)},{Csv(file.role)},{file.size}");
        File.WriteAllText(Path.Combine(directory, "files.csv"), files.ToString());
        File.WriteAllText(Path.Combine(directory, "summary.txt"),
            $"Target: {report.summary.platform}\nResult: {report.summary.result}\n" +
            $"Output: {report.summary.outputPath}\nOptions: {report.summary.options}\n" +
            $"Build report bytes: {report.summary.totalSize}\n" +
            "Packed asset sizes are attribution data, not final compressed disk sizes.\n" +
            "Measure the installed folder and ZIP separately with Tools/Measure-WindowsBuild.ps1.\n" +
            (rows.Length == 0 ? "No packed assets available; enable DetailedBuildReport on the next authorized build.\n" : ""));
        Debug.Log($"Windows build report exported to {Path.GetFullPath(directory)}");
    }

    static string Csv(string value) => "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";
}
#endif

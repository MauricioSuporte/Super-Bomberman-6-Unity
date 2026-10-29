using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Assets.Scripts.Netcode;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class CoopSceneCatalogBuilder : IPreprocessBuildWithReport
{
    public int callbackOrder => -100;
    public void OnPreprocessBuild(BuildReport report) => Generate();

    [MenuItem("Tools/Super Bomberman 6/Online/Refresh Co-op Scene Catalogs")]
    public static void Generate()
    {
        GenerateBattleAudio();
        const string root = "Assets/Resources/CoopScenes";
        if (!AssetDatabase.IsValidFolder(root)) AssetDatabase.CreateFolder("Assets/Resources", "CoopScenes");
        // Resources used by scripts at runtime may not be serialized scene dependencies.
        var shared = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Resources" })
            .Select(AssetDatabase.GUIDToAssetPath).Where(p => !p.Contains("/Skin"))
            .ToArray();
        foreach (var scene in EditorBuildSettings.scenes.Where(s => s.enabled && s.path.Contains("/Stage_")))
        {
            // The geyser scene loads these animation frames through Resources,
            // outside the serialized scene/prefab dependency graph.
            var runtimeResources = scene.path.EndsWith("/Stage_3-3.unity", StringComparison.Ordinal)
                ? new[] { "Assets/Resources/StageAssets/Stage33BubbleAnimations.asset" } : Array.Empty<string>();
            var dependencies = AssetDatabase.GetDependencies(new[] { scene.path }.Concat(shared).Concat(runtimeResources).ToArray(), true);
            var objects = dependencies.Where(p => p.StartsWith("Assets/", StringComparison.Ordinal) &&
                !p.StartsWith(root + "/", StringComparison.Ordinal) && !p.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                .SelectMany(AssetDatabase.LoadAllAssetsAtPath).Where(o => o is Sprite or TileBase or Material or AudioClip)
                .Distinct().OrderBy(Key, StringComparer.Ordinal).ToArray();
            string path = root + "/" + System.IO.Path.GetFileNameWithoutExtension(scene.path) + ".asset";
            var catalog = AssetDatabase.LoadAssetAtPath<CoopSceneCatalog>(path);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<CoopSceneCatalog>();
                AssetDatabase.CreateAsset(catalog, path);
            }
            var soundPaths = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Resources" })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p, StringComparer.Ordinal)
                .Select(p => p.Substring("Assets/Resources/".Length))
                .Select(p => p.Substring(0, p.LastIndexOf('.'))).ToArray();
            using var hash = SHA256.Create();
            string checksum = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(
                string.Join("\n", objects.Select(Key).Concat(soundPaths))))).Replace("-", "");
            if (catalog.contentHash == checksum) continue;
            catalog.contentHash = checksum;
            catalog.resourceSoundPaths = soundPaths;
            catalog.sprites = objects.OfType<Sprite>().ToArray();
            catalog.tiles = objects.OfType<TileBase>().ToArray();
            catalog.materials = objects.OfType<Material>().ToArray();
            catalog.sounds = objects.OfType<AudioClip>().ToArray();
            EditorUtility.SetDirty(catalog);
        }
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Tools/Super Bomberman 6/Online/Refresh Battle Audio Catalog")]
    public static void GenerateBattleAudio()
    {
        const string path = "Assets/Resources/OnlineBattleAudio.asset";
        var sounds = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Sounds", "Assets/Resources/Sounds" })
            .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<AudioClip>)
            .Where(clip => clip != null).OrderBy(Key, StringComparer.Ordinal).ToArray();
        var catalog = AssetDatabase.LoadAssetAtPath<CoopSceneCatalog>(path);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<CoopSceneCatalog>();
            AssetDatabase.CreateAsset(catalog, path);
        }
        if (catalog.sounds != null && catalog.sounds.SequenceEqual(sounds)) return;
        catalog.sounds = sounds;
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
    }

    static string Key(UnityEngine.Object value)
    {
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out string guid, out long id);
        return guid + ":" + id;
    }
}

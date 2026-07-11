#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Object = UnityEngine.Object;

public static class BomberSkinSheetGenerator
{
    const string BombersRootAssetPath = "Assets/Resources/Sprites/Bombers";

    readonly struct CharacterSkinSheetSource
    {
        public CharacterSkinSheetSource(
            string characterFolderName,
            string sourceSheetAssetPath,
            string paletteAssetPath,
            string outputFolderAssetPath)
        {
            CharacterFolderName = characterFolderName;
            SourceSheetAssetPath = sourceSheetAssetPath;
            PaletteAssetPath = paletteAssetPath;
            OutputFolderAssetPath = outputFolderAssetPath;
        }

        public string CharacterFolderName { get; }
        public string SourceSheetAssetPath { get; }
        public string PaletteAssetPath { get; }
        public string OutputFolderAssetPath { get; }
    }

    static readonly BomberSkin[] BombermanSkins =
    {
        BomberSkin.White,
        BomberSkin.Black,
        BomberSkin.Red,
        BomberSkin.Blue,
        BomberSkin.Green,
        BomberSkin.Purple,
        BomberSkin.Pink,
        BomberSkin.Magenta,
        BomberSkin.Gray,
        BomberSkin.Brown,
        BomberSkin.DarkGreen,
        BomberSkin.DarkBlue,
        BomberSkin.Orange,
        BomberSkin.Cyan,
        BomberSkin.Aqua,
        BomberSkin.DarkPurple,
        BomberSkin.Yellow,
        BomberSkin.LightBlue,
        BomberSkin.Gold,
        BomberSkin.Golden,
        BomberSkin.Nightmare,
        BomberSkin.Alternative1,
        BomberSkin.Alternative2,
        BomberSkin.Alternative3,
        BomberSkin.Alternative4
    };

    [InitializeOnLoadMethod]
    static void GenerateOnEditorLoad()
    {
        EditorApplication.delayCall += GenerateMissingBombermanSheets;
    }

    [MenuItem("Tools/Bomberman/Generate Missing Bomber Skin Sheets")]
    public static void GenerateMissingBombermanSheets()
    {
        if (EditorApplication.isCompiling)
        {
            EditorApplication.delayCall += GenerateMissingBombermanSheets;
            return;
        }

        bool generatedAny = false;

        foreach (CharacterSkinSheetSource source in FindCharacterSources())
        {
            if (GenerateMissingSheets(source))
                generatedAny = true;
        }

        if (generatedAny)
            AssetDatabase.Refresh();

        BomberHudPortraitGenerator.GenerateAll();
    }

    static bool GenerateMissingSheets(CharacterSkinSheetSource sheetSource)
    {
        if (!File.Exists(sheetSource.SourceSheetAssetPath) || !File.Exists(sheetSource.PaletteAssetPath))
            return false;

        EnsureFolder(sheetSource.OutputFolderAssetPath);

        Texture2D source = LoadTexture(sheetSource.SourceSheetAssetPath);
        Texture2D palette = LoadTexture(sheetSource.PaletteAssetPath);

        if (source == null || palette == null)
            return false;

        int skinCount = Mathf.Min(BombermanSkins.Length, palette.width - 1);
        bool generatedAny = false;

        for (int i = 0; i < skinCount; i++)
        {
            BomberSkin skin = BombermanSkins[i];
            int paletteColumn = i + 1;
            string sheetName = BomberSkinResourceCatalog.GetSheetName(sheetSource.CharacterFolderName, skin);
            string outputPath = $"{sheetSource.OutputFolderAssetPath}/{sheetName}.png";

            if (File.Exists(outputPath))
                continue;

            Dictionary<Color32, Color32> colorMap = BuildPaletteMap(palette, paletteColumn);
            Texture2D generated = Recolor(source, colorMap);
            File.WriteAllBytes(outputPath, generated.EncodeToPNG());

            Object.DestroyImmediate(generated);
            WriteMetaFromSource(sheetSource.SourceSheetAssetPath, outputPath, sheetName);
            generatedAny = true;
        }

        Object.DestroyImmediate(source);
        Object.DestroyImmediate(palette);

        return generatedAny;
    }

    static IEnumerable<CharacterSkinSheetSource> FindCharacterSources()
    {
        if (!Directory.Exists(BombersRootAssetPath))
            yield break;

        string[] characterFolders = Directory.GetDirectories(BombersRootAssetPath, "*", SearchOption.TopDirectoryOnly);
        Array.Sort(characterFolders, StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < characterFolders.Length; i++)
        {
            string characterFolder = characterFolders[i].Replace('\\', '/');
            string characterFolderName = Path.GetFileName(characterFolder);
            string palettePath = FindPalettePath(characterFolder);
            string sourceSheetPath = FindSourceSheetPath(characterFolder);

            if (palettePath == null || sourceSheetPath == null)
            {
                Debug.LogWarning($"[BomberSkinSheetGenerator] '{characterFolderName}' needs one source sprite sheet and one palette PNG.");
                continue;
            }

            yield return new CharacterSkinSheetSource(
                characterFolderName,
                sourceSheetPath,
                palettePath,
                $"{characterFolder}/Generated/{characterFolderName}");
        }
    }

    static string FindPalettePath(string characterFolder)
    {
        string[] pngPaths = Directory.GetFiles(characterFolder, "*.png", SearchOption.TopDirectoryOnly);
        Array.Sort(pngPaths, StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < pngPaths.Length; i++)
        {
            string fileName = Path.GetFileNameWithoutExtension(pngPaths[i]);
            if (fileName.IndexOf("palette", StringComparison.OrdinalIgnoreCase) >= 0 ||
                fileName.IndexOf("pallete", StringComparison.OrdinalIgnoreCase) >= 0)
                return pngPaths[i].Replace('\\', '/');
        }

        return null;
    }

    static string FindSourceSheetPath(string characterFolder)
    {
        string[] pngPaths = Directory.GetFiles(characterFolder, "*.png", SearchOption.TopDirectoryOnly);
        Array.Sort(pngPaths, StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < pngPaths.Length; i++)
        {
            string fileName = Path.GetFileNameWithoutExtension(pngPaths[i]);
            if (fileName.IndexOf("palette", StringComparison.OrdinalIgnoreCase) < 0 &&
                fileName.IndexOf("pallete", StringComparison.OrdinalIgnoreCase) < 0)
                return pngPaths[i].Replace('\\', '/');
        }

        return null;
    }

    static Texture2D LoadTexture(string assetPath)
    {
        byte[] bytes = File.ReadAllBytes(assetPath);
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        texture.name = Path.GetFileNameWithoutExtension(assetPath);

        if (!texture.LoadImage(bytes))
        {
            Object.DestroyImmediate(texture);
            return null;
        }

        return texture;
    }

    static Dictionary<Color32, Color32> BuildPaletteMap(Texture2D palette, int targetColumn)
    {
        Dictionary<Color32, Color32> map = new();

        for (int y = 0; y < palette.height; y++)
        {
            Color32 reference = palette.GetPixel(0, y);
            Color32 target = palette.GetPixel(targetColumn, y);

            if (reference.a == 0)
                continue;

            map[reference] = target;
        }

        return map;
    }

    static Texture2D Recolor(Texture2D source, Dictionary<Color32, Color32> colorMap)
    {
        Texture2D generated = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
        Color32[] pixels = source.GetPixels32();

        for (int i = 0; i < pixels.Length; i++)
        {
            if (pixels[i].a == 0)
                continue;

            if (colorMap.TryGetValue(pixels[i], out Color32 replacement))
                pixels[i] = replacement;
        }

        generated.SetPixels32(pixels);
        generated.Apply(false, false);
        generated.filterMode = FilterMode.Point;
        return generated;
    }

    static void WriteMetaFromSource(string sourceSheetAssetPath, string outputPath, string sheetName)
    {
        string sourceMetaPath = sourceSheetAssetPath + ".meta";
        string outputMetaPath = outputPath + ".meta";

        if (!File.Exists(sourceMetaPath) || File.Exists(outputMetaPath))
            return;

        string meta = File.ReadAllText(sourceMetaPath);
        string sourceSheetName = Path.GetFileNameWithoutExtension(sourceSheetAssetPath);
        meta = Regex.Replace(meta, @"^guid: \w+", "guid: " + GUID.Generate().ToString(), RegexOptions.Multiline);
        meta = meta.Replace(sourceSheetName + "_", sheetName + "_");
        File.WriteAllText(outputMetaPath, meta);
    }

    static void EnsureFolder(string assetFolderPath)
    {
        string[] parts = assetFolderPath.Split('/');
        string current = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);

            current = next;
        }
    }
}

sealed class BomberSkinSheetBuildPreprocessor : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        BomberSkinSheetGenerator.GenerateMissingBombermanSheets();
    }
}
#endif

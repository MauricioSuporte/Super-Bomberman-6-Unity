#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class BomberHudPortraitGenerator
{
    const int PortraitSize = 32;
    const int BorderSize = 1;
    const int BorderedSize = PortraitSize + (BorderSize * 2);
    const int PortraitCount = 6;

    readonly struct SourceFolder
    {
        public SourceFolder(string input, string output)
        {
            Input = input;
            Output = output;
        }

        public string Input { get; }
        public string Output { get; }
    }

    static readonly SourceFolder[] SourceFolders =
    {
        new("Assets/Resources/Sprites/Bomberman/Generated/Bomberman", "Assets/Resources/Sprites/Portraits/Bomberman"),
        new("Assets/Resources/Sprites/LadyBomber/Generated/LadyBomber", "Assets/Resources/Sprites/Portraits/LadyBomber"),
        new("Assets/Resources/Sprites/TinyBomber/Generated/TinyBomber", "Assets/Resources/Sprites/Portraits/TinyBomber")
    };

    [MenuItem("Tools/Bomberman/Generate HUD Portraits")]
    public static void GenerateAll()
    {
        int generatedCount = 0;
        List<string> errors = new();

        for (int i = 0; i < SourceFolders.Length; i++)
            GenerateFolder(SourceFolders[i], ref generatedCount, errors);

        AssetDatabase.Refresh();

        if (errors.Count > 0)
            Debug.LogWarning($"[BomberHudPortraitGenerator] Generated {generatedCount} portraits with {errors.Count} errors:\n{string.Join("\n", errors)}");
        else
            Debug.Log($"[BomberHudPortraitGenerator] Generated or updated {generatedCount} portraits.");
    }

    static void GenerateFolder(SourceFolder folder, ref int generatedCount, List<string> errors)
    {
        if (!Directory.Exists(folder.Input))
        {
            errors.Add($"Source folder not found: {folder.Input}");
            return;
        }

        string[] sourcePaths = Directory.GetFiles(folder.Input, "*.png", SearchOption.TopDirectoryOnly);
        Array.Sort(sourcePaths, StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < sourcePaths.Length; i++)
        {
            string sourcePath = sourcePaths[i].Replace('\\', '/');
            string sheetName = Path.GetFileNameWithoutExtension(sourcePath);
            string outputFolder = $"{folder.Output}/{sheetName}";

            if (!NeedsGeneration(sourcePath, outputFolder, sheetName))
                continue;

            Texture2D source = LoadTexture(sourcePath);
            if (source == null)
            {
                errors.Add($"Could not read {sourcePath}");
                continue;
            }

            if (!TryFindPortraits(source, out RectInt[] portraitRects))
            {
                errors.Add($"Expected six red-bordered 32x32 portraits in {sourcePath}");
                UnityEngine.Object.DestroyImmediate(source);
                continue;
            }

            Directory.CreateDirectory(outputFolder);

            for (int portraitIndex = 0; portraitIndex < portraitRects.Length; portraitIndex++)
            {
                Texture2D portrait = Crop(source, portraitRects[portraitIndex]);
                string outputPath = $"{outputFolder}/{sheetName}_Portrait_{portraitIndex}.png";
                byte[] png = portrait.EncodeToPNG();

                if (!File.Exists(outputPath) || !BytesEqual(File.ReadAllBytes(outputPath), png))
                {
                    File.WriteAllBytes(outputPath, png);
                    generatedCount++;
                }

                UnityEngine.Object.DestroyImmediate(portrait);
            }

            UnityEngine.Object.DestroyImmediate(source);
        }
    }

    static bool NeedsGeneration(string sourcePath, string outputFolder, string sheetName)
    {
        DateTime sourceWriteTime = File.GetLastWriteTimeUtc(sourcePath);

        for (int portraitIndex = 0; portraitIndex < PortraitCount; portraitIndex++)
        {
            string outputPath = $"{outputFolder}/{sheetName}_Portrait_{portraitIndex}.png";
            if (!File.Exists(outputPath) || File.GetLastWriteTimeUtc(outputPath) < sourceWriteTime)
                return true;
        }

        return false;
    }

    static bool TryFindPortraits(Texture2D texture, out RectInt[] portraits)
    {
        portraits = null;
        List<RectInt> candidates = new();

        for (int y = 0; y <= texture.height - BorderedSize; y++)
        {
            for (int x = 0; x <= texture.width - BorderedSize; x++)
            {
                if (IsRedBorder(texture, x, y))
                    candidates.Add(new RectInt(x + BorderSize, y + BorderSize, PortraitSize, PortraitSize));
            }
        }

        for (int i = 0; i < candidates.Count; i++)
        {
            RectInt first = candidates[i];
            RectInt[] column = new RectInt[PortraitCount];
            column[0] = first;
            bool complete = true;

            for (int portraitIndex = 1; portraitIndex < PortraitCount; portraitIndex++)
            {
                RectInt expected = new(first.x, first.y - (portraitIndex * (PortraitSize + BorderSize)), PortraitSize, PortraitSize);
                if (!candidates.Contains(expected))
                {
                    complete = false;
                    break;
                }

                column[portraitIndex] = expected;
            }

            if (complete)
            {
                portraits = column;
                return true;
            }
        }

        return false;
    }

    static bool IsRedBorder(Texture2D texture, int x, int y)
    {
        int maxX = x + BorderedSize - 1;
        int maxY = y + BorderedSize - 1;

        for (int offset = 0; offset < BorderedSize; offset++)
        {
            if (!IsMarkerRed(texture.GetPixel(x + offset, y)) ||
                !IsMarkerRed(texture.GetPixel(x + offset, maxY)) ||
                !IsMarkerRed(texture.GetPixel(x, y + offset)) ||
                !IsMarkerRed(texture.GetPixel(maxX, y + offset)))
                return false;
        }

        return true;
    }

    static bool IsMarkerRed(Color color)
    {
        Color32 pixel = color;
        return pixel.a > 0 && pixel.r >= 200 && pixel.g <= 60 && pixel.b <= 60;
    }

    static Texture2D LoadTexture(string assetPath)
    {
        Texture2D texture = new(2, 2, TextureFormat.RGBA32, false);
        if (texture.LoadImage(File.ReadAllBytes(assetPath)))
            return texture;

        UnityEngine.Object.DestroyImmediate(texture);
        return null;
    }

    static Texture2D Crop(Texture2D source, RectInt rect)
    {
        Texture2D portrait = new(PortraitSize, PortraitSize, TextureFormat.RGBA32, false);
        portrait.SetPixels(source.GetPixels(rect.x, rect.y, rect.width, rect.height));
        portrait.filterMode = FilterMode.Point;
        portrait.Apply(false, false);
        return portrait;
    }

    static bool BytesEqual(byte[] left, byte[] right)
    {
        if (left.Length != right.Length)
            return false;

        for (int i = 0; i < left.Length; i++)
        {
            if (left[i] != right[i])
                return false;
        }

        return true;
    }

}

sealed class BomberHudPortraitImportProcessor : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/Resources/Sprites/Portraits/", StringComparison.Ordinal))
            return;

        TextureImporter importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 16f;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
    }
}
#endif

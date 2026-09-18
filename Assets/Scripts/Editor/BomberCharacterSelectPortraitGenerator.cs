#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class BomberCharacterSelectPortraitGenerator
{
    const string SourceRoot = "Assets/Sprites/CharacterSelect";
    const string OutputRoot = "Assets/Resources/Sprites/CharacterSelect";
    const string PalettePath = OutputRoot + "/SelectionPalette.png";
    const int Size = 32;

    [MenuItem("Tools/Sprites/Generate Character Select Portraits")]
    public static void GenerateAll()
    {
        if (!Directory.Exists(SourceRoot))
            return;

        GenerateCursorSprites();

        foreach (string sourcePath in Directory.GetFiles(SourceRoot, "*.png"))
        {
            string character = Path.GetFileNameWithoutExtension(sourcePath);
            if (character == "SkinSelectCursor" || !File.Exists(PalettePath))
                continue;

            Texture2D source = BomberSkinSheetGenerator.LoadTexture(sourcePath);
            Texture2D palette = BomberSkinSheetGenerator.LoadTexture(PalettePath);
            try
            {
                if (source == null || palette == null)
                    continue;
                if (source.width != Size || source.height != Size * 2)
                {
                    Debug.LogWarning($"Character selection portraits must be 32x64: {sourcePath}");
                    continue;
                }

                string folder = $"{OutputRoot}/{character}";
                Directory.CreateDirectory(folder);
                int count = Mathf.Min(palette.width - 1, BomberSkinResourceCatalog.BombermanSkins.Length);
                for (int i = 0; i < count; i++)
                {
                    string sheet = BomberSkinResourceCatalog.GetSheetName(character, BomberSkinResourceCatalog.BombermanSkins[i]);
                    var colorMap = BomberSkinSheetGenerator.BuildPaletteMap(palette, i + 1);
                    // Unauthored palette entries preserve the source color, not transparency.
                    foreach (Color32 reference in colorMap.Keys.ToArray())
                        if (colorMap[reference].a == 0) colorMap[reference] = reference;
                    Texture2D recolored = BomberSkinSheetGenerator.Recolor(source, colorMap);
                    try
                    {
                        for (int frame = 0; frame < 2; frame++)
                        {
                            Texture2D portrait = new(Size, Size, TextureFormat.RGBA32, false);
                            portrait.SetPixels(recolored.GetPixels(0, source.height - Size * (frame + 1), Size, Size));
                            portrait.Apply();
                            byte[] bytes = portrait.EncodeToPNG();
                            UnityEngine.Object.DestroyImmediate(portrait);
                            string output = $"{folder}/{sheet}_{frame}.png";
                            if (File.Exists(output) && File.ReadAllBytes(output).SequenceEqual(bytes))
                                continue;
                            File.WriteAllBytes(output, bytes);
                            AssetDatabase.ImportAsset(output);
                        }
                    }
                    finally { UnityEngine.Object.DestroyImmediate(recolored); }
                }
            }
            finally
            {
                if (source != null) UnityEngine.Object.DestroyImmediate(source);
                if (palette != null) UnityEngine.Object.DestroyImmediate(palette);
            }
        }
    }

    static void GenerateCursorSprites()
    {
        string sourcePath = SourceRoot + "/SkinSelectCursor.png";
        if (!File.Exists(sourcePath)) return;
        Texture2D source = BomberSkinSheetGenerator.LoadTexture(sourcePath);
        if (source == null) return;
        try
        {
            if (source.width != 40 || source.height != 24)
            {
                Debug.LogWarning($"Selection cursor sheet must be 40x24: {sourcePath}");
                return;
            }
            string[] names = { "1", "2", "3", "4", "5", "6", "P", "TopLeft", "TopRight", "BottomLeft", "BottomRight" };
            Vector2Int[] cells = { new(0, 0), new(1, 0), new(2, 0), new(0, 1), new(1, 1), new(2, 1), new(0, 2), new(3, 0), new(4, 0), new(3, 1), new(4, 1) };
            Directory.CreateDirectory(OutputRoot + "/Cursor");
            for (int i = 0; i < names.Length; i++)
            {
                Color[] pixels = source.GetPixels(cells[i].x * 8, source.height - (cells[i].y + 1) * 8, 8, 8);
                for (int p = 0; p < pixels.Length; p++)
                {
                    Color32 pixel = pixels[p];
                    if (pixel.r == 251 && pixel.g == 255 && pixel.b == 0 && pixel.a > 0)
                        pixels[p] = Color.white; // UI tint changes the ink while keeping black outlines.
                }
                Texture2D tile = new(8, 8, TextureFormat.RGBA32, false);
                tile.SetPixels(pixels);
                tile.Apply();
                byte[] bytes = tile.EncodeToPNG();
                UnityEngine.Object.DestroyImmediate(tile);
                string path = $"{OutputRoot}/Cursor/{names[i]}.png";
                if (File.Exists(path) && File.ReadAllBytes(path).SequenceEqual(bytes)) continue;
                File.WriteAllBytes(path, bytes);
                AssetDatabase.ImportAsset(path);
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(source); }
    }
}

sealed class BomberCharacterSelectPortraitImportProcessor : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/Resources/Sprites/CharacterSelect/", StringComparison.Ordinal) &&
            !assetPath.StartsWith("Assets/Sprites/CharacterSelect/", StringComparison.Ordinal))
            return;

        TextureImporter importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 16f;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.isReadable = assetPath.EndsWith("/SelectionPalette.png", StringComparison.Ordinal);
        foreach (string platform in new[] { "Standalone", "Android", "iPhone", "WebGL" })
            importer.ClearPlatformTextureSettings(platform);
    }
}
#endif

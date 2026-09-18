#if UNITY_EDITOR
using System;
using System.Collections.Generic;
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
    const int MaxWriteAttempts = 10;
    static readonly Dictionary<string, byte[]> pendingWrites = new();
    static int retryAttempts;
    static double nextRetryTime;

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
                            WriteGeneratedAsset(output, bytes);
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
            if (source.width != 77 || source.height != 56)
            {
                Debug.LogWarning($"Selection cursor sheet must be 77x56: {sourcePath}");
                return;
            }
            string[] names = { "1", "2", "3", "4", "5", "6", "P", "TopLeft", "TopRight", "BottomLeft", "BottomRight", "L", "R", "Plus", "Frame" };
            Vector2Int[] cells = { new(0, 0), new(1, 0), new(2, 0), new(0, 1), new(1, 1), new(2, 1), new(0, 2), new(3, 0), new(4, 0), new(3, 1), new(4, 1), new(1, 2), new(2, 2), new(3, 2), new(0, 3) };
            Directory.CreateDirectory(OutputRoot + "/Cursor");
            for (int i = 0; i < names.Length; i++)
            {
                int tileSize = names[i] == "Frame" ? 32 : 8;
                Color[] pixels = source.GetPixels(cells[i].x * 8, source.height - cells[i].y * 8 - tileSize, tileSize, tileSize);
                for (int p = 0; p < pixels.Length; p++)
                {
                    Color32 pixel = pixels[p];
                    if (pixel.r == 251 && pixel.g == 255 && pixel.b == 0 && pixel.a > 0)
                        pixels[p] = Color.white; // UI tint changes the ink while keeping black outlines.
                }
                Texture2D tile = new(tileSize, tileSize, TextureFormat.RGBA32, false);
                tile.SetPixels(pixels);
                tile.Apply();
                byte[] bytes = tile.EncodeToPNG();
                UnityEngine.Object.DestroyImmediate(tile);
                string path = $"{OutputRoot}/Cursor/{names[i]}.png";
                WriteGeneratedAsset(path, bytes);
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(source); }
    }

    static void WriteGeneratedAsset(string path, byte[] bytes)
    {
        if (TryWriteGeneratedAsset(path, bytes, out _))
        {
            pendingWrites.Remove(path);
            return;
        }

        if (pendingWrites.Count == 0) retryAttempts = 0;
        pendingWrites[path] = bytes;
        nextRetryTime = EditorApplication.timeSinceStartup + 0.5;
        EditorApplication.update -= RetryPendingWrites;
        EditorApplication.update += RetryPendingWrites;
    }

    static bool TryWriteGeneratedAsset(string path, byte[] bytes, out string error)
    {
        error = null;
        try
        {
            if (File.Exists(path))
            {
                byte[] existing = File.ReadAllBytes(path);
                // PNG encoders can produce different bytes for identical pixels.
                if (existing.SequenceEqual(bytes) || SamePixels(existing, bytes)) return true;
            }

            // Never truncate a PNG that Unity may currently have memory-mapped.
            // Replace only the image; its .meta and GUID remain untouched.
            string temporary = Path.Combine(Path.GetDirectoryName(path),
                "." + Path.GetFileName(path) + "." + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                File.WriteAllBytes(temporary, bytes);
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }

            AssetDatabase.ImportAsset(path);
            return true;
        }
        catch (IOException exception) when (IsFileInUse(exception))
        {
            error = exception.Message;
            return false;
        }
    }

    static bool IsFileInUse(IOException exception)
    {
        int code = exception.HResult & 0xffff;
        return code == 32 || code == 33 || code == 1224;
    }

    static bool SamePixels(byte[] left, byte[] right)
    {
        Texture2D a = new(2, 2, TextureFormat.RGBA32, false);
        Texture2D b = new(2, 2, TextureFormat.RGBA32, false);
        try
        {
            return a.LoadImage(left) && b.LoadImage(right) &&
                a.width == b.width && a.height == b.height &&
                a.GetPixels32().SequenceEqual(b.GetPixels32());
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(a);
            UnityEngine.Object.DestroyImmediate(b);
        }
    }

    static void RetryPendingWrites()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.timeSinceStartup < nextRetryTime) return;

        nextRetryTime = EditorApplication.timeSinceStartup + 0.5;
        retryAttempts++;
        foreach (var pending in pendingWrites.ToArray())
        {
            try
            {
                if (TryWriteGeneratedAsset(pending.Key, pending.Value, out string error))
                    pendingWrites.Remove(pending.Key);
                else if (retryAttempts >= MaxWriteAttempts)
                {
                    pendingWrites.Remove(pending.Key);
                    Debug.LogError($"Could not update selection asset after {MaxWriteAttempts} retries: {pending.Key}\n{error}");
                }
            }
            catch (Exception exception)
            {
                pendingWrites.Remove(pending.Key);
                Debug.LogException(exception);
            }
        }
        if (pendingWrites.Count == 0) EditorApplication.update -= RetryPendingWrites;
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

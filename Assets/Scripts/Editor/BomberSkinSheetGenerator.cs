#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class BomberSkinSheetGenerator
{
    const string LogPrefix = "[BomberSkinSheetGenerator]";
    const string SourceSheetAssetPath = "Assets/Resources/Sprites/Bomberman/Bomberman.png";
    const string PaletteAssetPath = "Assets/Resources/Sprites/Bomberman/BombermanPallete.png";
    const string OutputFolderAssetPath = "Assets/Resources/Sprites/Bomberman/Generated/Bomberman";

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

    [MenuItem("Tools/Bomberman/Generate Missing Bomberman Skin Sheets")]
    public static void GenerateMissingBombermanSheets()
    {
        if (EditorApplication.isCompiling)
        {
            Debug.Log($"{LogPrefix} Unity is compiling; delaying generation.");
            EditorApplication.delayCall += GenerateMissingBombermanSheets;
            return;
        }

        if (!File.Exists(SourceSheetAssetPath) || !File.Exists(PaletteAssetPath))
        {
            Debug.LogWarning($"{LogPrefix} Missing source sheet or palette. source='{SourceSheetAssetPath}' palette='{PaletteAssetPath}'");
            return;
        }

        EnsureFolder(OutputFolderAssetPath);

        Texture2D source = LoadTexture(SourceSheetAssetPath);
        Texture2D palette = LoadTexture(PaletteAssetPath);

        if (source == null || palette == null)
        {
            Debug.LogWarning($"{LogPrefix} Could not decode source sheet or palette.");
            return;
        }

        int skinCount = Mathf.Min(BombermanSkins.Length, palette.width - 1);
        int created = 0;
        int skipped = 0;

        Debug.Log($"{LogPrefix} Start | source={SourceSheetAssetPath} palette={PaletteAssetPath} paletteSize={palette.width}x{palette.height} skins={skinCount}");

        for (int i = 0; i < skinCount; i++)
        {
            BomberSkin skin = BombermanSkins[i];
            int paletteColumn = i + 1;
            string sheetName = BomberSkinResourceCatalog.GetSheetName(BomberCharacter.Bomberman, skin);
            string outputPath = $"{OutputFolderAssetPath}/{sheetName}.png";

            if (File.Exists(outputPath))
            {
                skipped++;
                Debug.Log($"{LogPrefix} Skip | skin={skin} path={outputPath}");
                continue;
            }

            Dictionary<Color32, Color32> colorMap = BuildPaletteMap(palette, paletteColumn);
            Texture2D generated = Recolor(source, colorMap);
            File.WriteAllBytes(outputPath, generated.EncodeToPNG());

            Object.DestroyImmediate(generated);
            WriteMetaFromSource(outputPath, sheetName);
            created++;

            Debug.Log($"{LogPrefix} Created | skin={skin} paletteColumn={paletteColumn} mappedColors={colorMap.Count} path={outputPath}");
        }

        Object.DestroyImmediate(source);
        Object.DestroyImmediate(palette);

        if (created > 0)
            AssetDatabase.Refresh();

        Debug.Log($"{LogPrefix} Done | created={created} skipped={skipped} output={OutputFolderAssetPath}");
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

    static void WriteMetaFromSource(string outputPath, string sheetName)
    {
        string sourceMetaPath = SourceSheetAssetPath + ".meta";
        string outputMetaPath = outputPath + ".meta";

        if (!File.Exists(sourceMetaPath) || File.Exists(outputMetaPath))
            return;

        string meta = File.ReadAllText(sourceMetaPath);
        meta = meta.Replace("guid: 160e5ada7044944489c9570364dd86f5", "guid: " + GUID.Generate().ToString());
        meta = meta.Replace("Bomberman_", sheetName + "_");
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
#endif

using UnityEngine;
using System.Collections.Generic;

public static class HudCharacterPortraitCatalog
{
    public const int DefaultExpression = 0;
    public const int DeadExpression = 1;
    public const int TimeUpExpression = 2;
    public const int CorneredExpression = 3;
    public const int InactivityExpression = 4;
    public const int VictoryExpression = 5;

    public const int LiveExpression = DefaultExpression;

    static readonly Dictionary<string, Sprite> cache = new();

    // Selection sheets contain neutral and focused portraits, top to bottom.
    public static Sprite LoadSelection(BomberCharacter character, BomberSkin skin, int expressionIndex)
    {
        string sheet = BomberSkinResourceCatalog.GetSheetName(character, skin);
        string folder = BomberSkinResourceCatalog.GetCharacterFolderName(character);
        string path = $"Sprites/CharacterSelect/{folder}/{sheet}_{Mathf.Clamp(expressionIndex, 0, 1)}";
        if (!cache.TryGetValue(path, out Sprite portrait))
        {
            string defaultSheet = BomberSkinResourceCatalog.GetSheetName(character, BomberSkin.Palette1);
            string defaultPath = $"Sprites/CharacterSelect/{folder}/{defaultSheet}_{Mathf.Clamp(expressionIndex, 0, 1)}";
            portrait = Resources.Load<Sprite>(path) ?? Resources.Load<Sprite>(defaultPath) ??
                Load(character, skin, DefaultExpression);
            cache[path] = portrait;
        }

        return portrait;
    }

    public static Sprite Load(BomberCharacter character, BomberSkin skin, int expressionIndex)
    {
        BomberSkin normalizedSkin = BomberSkinResourceCatalog.NormalizeGeneratedSkin(character, skin);
        string sheetName = BomberSkinResourceCatalog.GetSheetName(character, normalizedSkin);
        string characterFolder = BomberSkinResourceCatalog.GetCharacterFolderName(character);
        string path = $"Sprites/Portraits/{characterFolder}/{sheetName}/{sheetName}_Portrait_{expressionIndex}";

        if (cache.TryGetValue(path, out Sprite cached))
            return cached;

        Sprite sprite = Resources.Load<Sprite>(path);
        cache[path] = sprite;
        return sprite;
    }
}

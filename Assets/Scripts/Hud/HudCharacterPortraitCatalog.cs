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

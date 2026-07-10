using UnityEngine;

public static class HudCharacterPortraitCatalog
{
    public const int LiveExpression = 0;
    public const int DeadExpression = 5;

    public static Sprite Load(BomberCharacter character, BomberSkin skin, int expressionIndex)
    {
        BomberSkin normalizedSkin = BomberSkinResourceCatalog.NormalizeGeneratedSkin(character, skin);
        string sheetName = BomberSkinResourceCatalog.GetSheetName(character, normalizedSkin);
        string characterFolder = GetCharacterFolder(character);
        string path = $"Sprites/Portraits/{characterFolder}/{sheetName}/{sheetName}_Portrait_{expressionIndex}";
        return Resources.Load<Sprite>(path);
    }

    static string GetCharacterFolder(BomberCharacter character)
    {
        return character switch
        {
            BomberCharacter.LadyBomber => "LadyBomber",
            BomberCharacter.TinyBomber => "TinyBomber",
            _ => "Bomberman"
        };
    }
}

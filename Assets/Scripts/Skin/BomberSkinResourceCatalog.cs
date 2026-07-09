public static class BomberSkinResourceCatalog
{
    public const string BombermanGeneratedResourcesPath = "Sprites/Bomberman/Generated/Bomberman";

    public static string GetGeneratedResourcesPath(BomberCharacter character)
    {
        return character switch
        {
            BomberCharacter.Bomberman => BombermanGeneratedResourcesPath,
            _ => BombermanGeneratedResourcesPath
        };
    }

    public static string GetSheetName(BomberCharacter character, BomberSkin skin)
    {
        string characterSuffix = character switch
        {
            BomberCharacter.Bomberman => "Bomber",
            _ => "Bomber"
        };

        return skin + characterSuffix;
    }
}

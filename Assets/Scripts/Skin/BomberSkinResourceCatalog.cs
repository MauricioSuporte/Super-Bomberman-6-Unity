public static class BomberSkinResourceCatalog
{
    public const string BombermanGeneratedResourcesPath = "Sprites/Bombers/Bomberman/Generated/Bomberman";
    public const string LadyBomberGeneratedResourcesPath = "Sprites/Bombers/LadyBomber/Generated/LadyBomber";
    public const string TinyBomberGeneratedResourcesPath = "Sprites/Bombers/TinyBomber/Generated/TinyBomber";

    public static readonly BomberSkin[] BombermanSkins =
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

    public static string GetGeneratedResourcesPath(BomberCharacter character)
    {
        return character switch
        {
            BomberCharacter.Bomberman => BombermanGeneratedResourcesPath,
            BomberCharacter.LadyBomber => LadyBomberGeneratedResourcesPath,
            BomberCharacter.TinyBomber => TinyBomberGeneratedResourcesPath,
            _ => BombermanGeneratedResourcesPath
        };
    }

    public static string GetSheetName(BomberCharacter character, BomberSkin skin)
    {
        return GetSheetName(GetCharacterFolderName(character), skin);
    }

    public static string GetSheetName(string characterFolderName, BomberSkin skin)
    {
        string characterSuffix = characterFolderName switch
        {
            "Bomberman" => "Bomber",
            _ => characterFolderName
        };

        return skin + characterSuffix;
    }

    public static string GetCharacterFolderName(BomberCharacter character)
    {
        return character switch
        {
            BomberCharacter.LadyBomber => "LadyBomber",
            BomberCharacter.TinyBomber => "TinyBomber",
            _ => "Bomberman"
        };
    }

    public static bool IsGeneratedSkin(BomberCharacter character, BomberSkin skin)
    {
        BomberSkin[] skins = character switch
        {
            BomberCharacter.Bomberman => BombermanSkins,
            _ => BombermanSkins
        };

        for (int i = 0; i < skins.Length; i++)
        {
            if (skins[i] == skin)
                return true;
        }

        return false;
    }

    public static BomberSkin NormalizeGeneratedSkin(BomberCharacter character, BomberSkin skin)
    {
        return IsGeneratedSkin(character, skin) ? skin : BomberSkin.White;
    }
}

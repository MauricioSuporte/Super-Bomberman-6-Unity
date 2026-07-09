public static class BomberSkinResourceCatalog
{
    public const string BombermanGeneratedResourcesPath = "Sprites/Bomberman/Generated/Bomberman";
    public const string LadyBomberGeneratedResourcesPath = "Sprites/LadyBomber/Generated/LadyBomber";

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
            _ => BombermanGeneratedResourcesPath
        };
    }

    public static string GetSheetName(BomberCharacter character, BomberSkin skin)
    {
        string characterSuffix = character switch
        {
            BomberCharacter.Bomberman => "Bomber",
            BomberCharacter.LadyBomber => "LadyBomber",
            _ => "Bomber"
        };

        return skin + characterSuffix;
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

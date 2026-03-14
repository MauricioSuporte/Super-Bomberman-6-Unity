public static class SkinUnlockHintCatalog
{
    public static string GetHint(BomberSkin skin)
    {
        switch (skin)
        {
            case BomberSkin.Gray:
                return @"UNLOCKED BY A CHEAT ""KODE""";

            case BomberSkin.Orange:
                return "CLEAR ALL STAGES";

            case BomberSkin.Purple:
                return "CLEAR ALL STAGES";

            case BomberSkin.Olive:
                return "CLEAR BOSS RUSH ON EASY";

            case BomberSkin.Cyan:
                return "CLEAR BOSS RUSH ON NORMAL";

            case BomberSkin.Brown:
                return "CLEAR BOSS RUSH ON HARD";

            case BomberSkin.DarkGreen:
                return "CLEAR BOSS RUSH ON EASY UNDER 4:00";

            case BomberSkin.DarkBlue:
                return "CLEAR BOSS RUSH ON NORMAL UNDER 4:00";

            case BomberSkin.Magenta:
                return "CLEAR BOSS RUSH ON HARD UNDER 5:00";

            case BomberSkin.Nightmare:
                return "CLEAR BOSS RUSH ON NIGHTMARE";

            case BomberSkin.Gold:
                return "CLEAR ALL STAGES WITHOUT USING ANY ITEMS";

            case BomberSkin.Golden:
                return "UNLOCK ALL OTHER BOMBERS";

            default:
                return "";
        }
    }
}
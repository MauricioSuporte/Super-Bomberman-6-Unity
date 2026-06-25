public static class SkinUnlockHintCatalog
{
    public static string GetHint(BomberSkin skin)
    {
        UnlockText text = GameTextDatabase.Unlocks;

        switch (skin)
        {
            case BomberSkin.Gray:
                return text.HintCheatKode;

            case BomberSkin.Orange:
                return text.HintClearNormalNormal;

            case BomberSkin.Purple:
                return text.HintClearNormalHard;

            case BomberSkin.Olive:
                return text.HintClearBossRushEasy;

            case BomberSkin.Cyan:
                return text.HintClearBossRushNormal;

            case BomberSkin.Brown:
                return text.HintClearBossRushHard;

            case BomberSkin.DarkGreen:
                return text.HintClearBossRushEasyUnder4;

            case BomberSkin.DarkBlue:
                return text.HintClearBossRushNormalUnder4;

            case BomberSkin.Magenta:
                return text.HintClearBossRushHardUnder5;

            case BomberSkin.Nightmare:
                return text.HintClearBossRushNightmare;

            case BomberSkin.Gold:
                return text.HintClearNormalHardcore;

            case BomberSkin.Golden:
                return text.HintUnlockAllOtherAchievements;

            default:
                return "";
        }
    }
}

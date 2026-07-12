public static class SkinUnlockHintCatalog
{
    public static string GetHint(BomberSkin skin)
    {
        UnlockText text = GameTextDatabase.Unlocks;

        switch (skin)
        {
            case BomberSkin.Palette9:
                return text.HintCheatKode;

            case BomberSkin.Palette13:
                return text.HintClearNormalNormal;

            case BomberSkin.Palette6:
                return text.HintClearNormalHard;

            case BomberSkin.Palette18:
                return text.HintClearBossRushEasy;

            case BomberSkin.Palette14:
                return text.HintClearBossRushNormal;

            case BomberSkin.Palette10:
                return text.HintClearBossRushHard;

            case BomberSkin.Palette11:
                return text.HintClearBossRushEasyUnder4;

            case BomberSkin.Palette12:
                return text.HintClearBossRushNormalUnder4;

            case BomberSkin.Palette8:
                return text.HintClearBossRushHardUnder5;

            case BomberSkin.Palette21:
                return text.HintClearBossRushNightmare;

            case BomberSkin.Palette19:
                return text.HintClearNormalHardcore;

            case BomberSkin.Palette20:
                return text.HintUnlockAllOtherAchievements;

            default:
                return "";
        }
    }
}

using System;
using UnityEngine;

public static class AchievementCatalog
{
    public readonly struct AchievementInfo
    {
        public readonly string Id;
        public readonly string Name;
        public readonly string Hint;
        public readonly string RewardText;
        public readonly Func<bool> IsUnlocked;
        public readonly Func<Sprite> LoadIcon;
        public readonly bool RequiredForGolden;

        public AchievementInfo(
            string id,
            string name,
            string hint,
            string rewardText,
            Func<bool> isUnlocked,
            Func<Sprite> loadIcon,
            bool requiredForGolden = true)
        {
            Id = id;
            Name = name;
            Hint = hint;
            RewardText = rewardText;
            IsUnlocked = isUnlocked;
            LoadIcon = loadIcon;
            RequiredForGolden = requiredForGolden;
        }
    }

    public static AchievementInfo[] All => BuildAll();

    private static AchievementInfo[] BuildAll()
    {
        UnlockText text = GameTextDatabase.Unlocks;

        return new[]
        {
            Skin(BomberSkin.Gray, "GRAY"),
            Skin(BomberSkin.Orange, "ORANGE"),
            Skin(BomberSkin.Purple, "PURPLE"),
            Skin(BomberSkin.Cyan, "CYAN"),
            Skin(BomberSkin.Brown, "BROWN"),
            Skin(BomberSkin.DarkGreen, "DARK GREEN"),
            Skin(BomberSkin.DarkBlue, "DARK BLUE"),
            Skin(BomberSkin.Magenta, "MAGENTA"),
            Skin(BomberSkin.Nightmare, "NIGHTMARE"),
            Skin(BomberSkin.Gold, "GOLD"),
            new(
                "BossRush",
                text.AchievementBossRush,
                text.HintClearNormalAny,
                text.RewardBossRush,
                () => SaveSystem.Data.bossRushUnlocked,
                UnlockToastCatalog.LoadBossRushIcon
            ),
            new(
                "BossRushNightmare",
                text.AchievementBossRushNightmare,
                text.HintClearBossRushHard,
                text.RewardBossRushNightmare,
                SaveSystem.IsNightmareUnlocked,
                UnlockToastCatalog.LoadNightmareIcon
            ),
            new(
                "Hardcore",
                text.AchievementHardcore,
                text.HintClearNormalHard,
                text.RewardHardcore,
                UnlockProgress.IsHardcoreUnlocked,
                UnlockToastCatalog.LoadHardcoreIcon
            ),
            BattleStage(11, text.HintWinBattleStage10, UnlockProgress.IsBattleModeStage11Unlocked),
            BattleStage(12, text.HintWinBattleStages7And9, UnlockProgress.IsBattleModeStage12Unlocked),
            BattleStage(13, text.HintWinAnyBattleStage, UnlockProgress.IsBattleModeStage13Unlocked),
            BattleStage(14, text.HintWin7BattleStages, UnlockProgress.IsBattleModeStage14Unlocked),
            BattleStage(15, text.HintWinAllOtherBattleStages, UnlockProgress.IsBattleModeStage15Unlocked),
            Skin(BomberSkin.Golden, text.AchievementGolden, requiredForGolden: false)
        };
    }

    public static bool AreAllRequiredForGoldenUnlocked()
    {
        for (int i = 0; i < All.Length; i++)
        {
            AchievementInfo achievement = All[i];
            if (!achievement.RequiredForGolden)
                continue;

            if (achievement.IsUnlocked == null || !achievement.IsUnlocked())
                return false;
        }

        return true;
    }

    private static AchievementInfo Skin(
        BomberSkin skin,
        string name,
        bool requiredForGolden = true)
    {
        UnlockText text = GameTextDatabase.Unlocks;

        return new AchievementInfo(
            skin.ToString(),
            name,
            SkinUnlockHintCatalog.GetHint(skin),
            string.Format(text.RewardSkin, name),
            () => SaveSystem.Data.unlockedSkins.Contains(skin.ToString()),
            () => UnlockToastCatalog.LoadIcon(skin),
            requiredForGolden
        );
    }

    private static AchievementInfo BattleStage(int stageIndex, string hint, Func<bool> isUnlocked)
    {
        UnlockText text = GameTextDatabase.Unlocks;

        return new AchievementInfo(
            "BattleModeStage" + stageIndex,
            string.Format(text.AchievementBattleStage, stageIndex),
            hint,
            string.Format(text.RewardBattleStage, stageIndex),
            isUnlocked,
            () => UnlockToastCatalog.LoadBattleModeStageIcon(stageIndex)
        );
    }
}

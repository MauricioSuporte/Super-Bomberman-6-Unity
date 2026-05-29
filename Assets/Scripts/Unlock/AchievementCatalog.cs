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

    public static readonly AchievementInfo[] All =
    {
        Skin(BomberSkin.Gray, "GRAY", "Gray Bomber"),
        Skin(BomberSkin.Orange, "ORANGE", "Orange Bomber"),
        Skin(BomberSkin.Purple, "PURPLE", "Purple Bomber"),
        Skin(BomberSkin.Olive, "OLIVE", "Olive Bomber"),
        Skin(BomberSkin.Cyan, "CYAN", "Cyan Bomber"),
        Skin(BomberSkin.Brown, "BROWN", "Brown Bomber"),
        Skin(BomberSkin.DarkGreen, "DARK GREEN", "Dark Green Bomber"),
        Skin(BomberSkin.DarkBlue, "DARK BLUE", "Dark Blue Bomber"),
        Skin(BomberSkin.Magenta, "MAGENTA", "Magenta Bomber"),
        Skin(BomberSkin.Nightmare, "NIGHTMARE", "Nightmare Bomber"),
        Skin(BomberSkin.Gold, "GOLD", "Gold Bomber"),
        new(
            "BossRush",
            "BOSS RUSH",
            "CLEAR NORMAL GAME ON ANY DIFFICULTY",
            "Boss Rush Mode",
            () => SaveSystem.Data.bossRushUnlocked,
            UnlockToastCatalog.LoadBossRushIcon
        ),
        new(
            "BossRushNightmare",
            "BOSS RUSH NIGHTMARE",
            "CLEAR BOSS RUSH ON HARD",
            "Boss Rush Nightmare Difficulty",
            SaveSystem.IsNightmareUnlocked,
            UnlockToastCatalog.LoadNightmareIcon
        ),
        new(
            "BattleModeStage11",
            "BATTLE STAGE 11",
            "WIN STAGE 10 IN BATTLE MODE",
            "Battle Mode Stage 11",
            UnlockProgress.IsBattleModeStage11Unlocked,
            UnlockToastCatalog.LoadBattleModeStage11Icon
        ),
        Skin(BomberSkin.Golden, "GOLDEN", "Golden Bomber", requiredForGolden: false)
    };

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
        string rewardText,
        bool requiredForGolden = true)
    {
        return new AchievementInfo(
            skin.ToString(),
            name,
            SkinUnlockHintCatalog.GetHint(skin),
            rewardText,
            () => SaveSystem.Data.unlockedSkins.Contains(skin.ToString()),
            () => UnlockToastCatalog.LoadIcon(skin),
            requiredForGolden
        );
    }
}

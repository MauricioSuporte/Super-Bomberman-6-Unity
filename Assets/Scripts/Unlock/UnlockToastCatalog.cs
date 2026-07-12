using UnityEngine;

public static class UnlockToastCatalog
{
    private static readonly bool EnableSurgicalLogs = false;

    public readonly struct ToastInfo
    {
        public readonly string Title;
        public readonly string Subtitle;
        public readonly string IconResourcePath;

        public ToastInfo(string title, string subtitle, string iconResourcePath)
        {
            Title = title;
            Subtitle = subtitle;
            IconResourcePath = iconResourcePath;
        }
    }

    public static ToastInfo Get(BomberSkin skin)
    {
        UnlockText text = GameTextDatabase.Unlocks;

        switch (skin)
        {
            case BomberSkin.Palette9:
                return new ToastInfo(
                    text.ToastNewCharacter,
                    string.Format(text.ToastSkinUnlocked, GetSkinDisplayName(skin)),
                    "UI/Unlocks/Icons/GrayBomber"
                );

            case BomberSkin.Palette13:
                return new ToastInfo(
                    text.ToastNewCharacter,
                    string.Format(text.ToastSkinUnlocked, GetSkinDisplayName(skin)),
                    "UI/Unlocks/Icons/OrangeBomber"
                );

            case BomberSkin.Palette6:
                return new ToastInfo(
                    text.ToastNewCharacter,
                    string.Format(text.ToastSkinUnlocked, GetSkinDisplayName(skin)),
                    "UI/Unlocks/Icons/PurpleBomber"
                );

            case BomberSkin.Palette18:
                return new ToastInfo(
                    text.ToastNewCharacter,
                    string.Format(text.ToastSkinUnlocked, GetSkinDisplayName(skin)),
                    "UI/Unlocks/Icons/NeonGreenBomber"
                );

            case BomberSkin.Palette14:
                return new ToastInfo(
                    text.ToastNewCharacter,
                    string.Format(text.ToastSkinUnlocked, GetSkinDisplayName(skin)),
                    "UI/Unlocks/Icons/CyanBomber"
                );

            case BomberSkin.Palette10:
                return new ToastInfo(
                    text.ToastNewCharacter,
                    string.Format(text.ToastSkinUnlocked, GetSkinDisplayName(skin)),
                    "UI/Unlocks/Icons/BrownBomber"
                );

            case BomberSkin.Palette11:
                return new ToastInfo(
                    text.ToastNewCharacter,
                    string.Format(text.ToastSkinUnlocked, GetSkinDisplayName(skin)),
                    "UI/Unlocks/Icons/DarkGreenBomber"
                );

            case BomberSkin.Palette12:
                return new ToastInfo(
                    text.ToastNewCharacter,
                    string.Format(text.ToastSkinUnlocked, GetSkinDisplayName(skin)),
                    "UI/Unlocks/Icons/DarkBlueBomber"
                );

            case BomberSkin.Palette8:
                return new ToastInfo(
                    text.ToastNewCharacter,
                    string.Format(text.ToastSkinUnlocked, GetSkinDisplayName(skin)),
                    "UI/Unlocks/Icons/MagentaBomber"
                );

            case BomberSkin.Palette21:
                return new ToastInfo(
                    text.ToastNewCharacter,
                    string.Format(text.ToastSkinUnlocked, GetSkinDisplayName(skin)),
                    "UI/Unlocks/Icons/NightmareBomber"
                );

            case BomberSkin.Palette19:
                return new ToastInfo(
                    text.ToastNewCharacter,
                    string.Format(text.ToastSkinUnlocked, GetSkinDisplayName(skin)),
                    "UI/Unlocks/Icons/GoldBomber"
                );

            case BomberSkin.Palette20:
                return new ToastInfo(
                    text.ToastNewCharacter,
                    string.Format(text.ToastSkinUnlocked, GetSkinDisplayName(skin)),
                    "UI/Unlocks/Icons/GoldenBomber"
                );

            default:
                return new ToastInfo(
                    text.ToastNewCharacter,
                    string.Format(text.ToastSkinUnlocked, GetSkinDisplayName(skin)),
                    $"UI/Unlocks/Icons/{skin}"
                );
        }
    }

    private static string GetSkinDisplayName(BomberSkin skin)
    {
        return $"Palette {BomberSkinResourceCatalog.GetPaletteNumber(skin)}";
    }

    public static ToastInfo GetBossRush()
    {
        return new ToastInfo(
            GameTextDatabase.Unlocks.ToastBossRushTitle,
            GameTextDatabase.Unlocks.ToastBossRushSubtitle,
            "UI/Unlocks/Icons/BossRush"
        );
    }

    public static ToastInfo GetNightmare()
    {
        return new ToastInfo(
            GameTextDatabase.Unlocks.ToastNightmareTitle,
            GameTextDatabase.Unlocks.ToastNightmareSubtitle,
            "UI/Unlocks/Icons/NightmareDifficulty"
        );
    }

    public static ToastInfo GetHardcore()
    {
        return new ToastInfo(
            GameTextDatabase.Unlocks.ToastHardcoreTitle,
            GameTextDatabase.Unlocks.ToastHardcoreSubtitle,
            "UI/Unlocks/Icons/Hardcore"
        );
    }

    public static ToastInfo GetBattleModeStage(int stageIndex)
    {
        int normalized = Mathf.Clamp(stageIndex, 11, 15);
        return new ToastInfo(
            GameTextDatabase.Unlocks.ToastBattleStageTitle,
            string.Format(GameTextDatabase.Unlocks.ToastBattleStageSubtitle, normalized),
            $"UI/Unlocks/Icons/Stage{normalized}"
        );
    }

    public static ToastInfo GetBattleModeStage11()
    {
        return GetBattleModeStage(11);
    }

    public static Sprite LoadIcon(BomberSkin skin)
    {
        ToastInfo info = Get(skin);

        if (string.IsNullOrWhiteSpace(info.IconResourcePath))
        {
            SLog($"LoadIcon | skin={skin} | resource path is empty");
            return null;
        }

        Sprite sprite = Resources.Load<Sprite>(info.IconResourcePath);

        SLog($"LoadIcon | skin={skin} | path={info.IconResourcePath} | found={(sprite != null)}");

        return sprite;
    }

    public static Sprite LoadBossRushIcon()
    {
        ToastInfo info = GetBossRush();

        if (string.IsNullOrWhiteSpace(info.IconResourcePath))
        {
            SLog("LoadBossRushIcon | resource path is empty");
            return null;
        }

        Sprite sprite = Resources.Load<Sprite>(info.IconResourcePath);

        SLog($"LoadBossRushIcon | path={info.IconResourcePath} | found={(sprite != null)}");

        return sprite;
    }

    public static Sprite LoadNightmareIcon()
    {
        ToastInfo info = GetNightmare();

        if (string.IsNullOrWhiteSpace(info.IconResourcePath))
        {
            SLog("LoadNightmareIcon | resource path is empty");
            return null;
        }

        Sprite sprite = Resources.Load<Sprite>(info.IconResourcePath);

        SLog($"LoadNightmareIcon | path={info.IconResourcePath} | found={(sprite != null)}");

        return sprite;
    }

    public static Sprite LoadHardcoreIcon()
    {
        ToastInfo info = GetHardcore();

        if (string.IsNullOrWhiteSpace(info.IconResourcePath))
        {
            SLog("LoadHardcoreIcon | resource path is empty");
            return null;
        }

        Sprite sprite = Resources.Load<Sprite>(info.IconResourcePath);

        SLog($"LoadHardcoreIcon | path={info.IconResourcePath} | found={(sprite != null)}");

        return sprite;
    }

    public static Sprite LoadBattleModeStageIcon(int stageIndex)
    {
        ToastInfo info = GetBattleModeStage(stageIndex);

        if (string.IsNullOrWhiteSpace(info.IconResourcePath))
        {
            SLog($"LoadBattleModeStageIcon | stage={stageIndex} | resource path is empty");
            return null;
        }

        Sprite sprite = Resources.Load<Sprite>(info.IconResourcePath);

        SLog($"LoadBattleModeStageIcon | stage={stageIndex} | path={info.IconResourcePath} | found={(sprite != null)}");

        return sprite;
    }

    public static Sprite LoadBattleModeStage11Icon()
    {
        return LoadBattleModeStageIcon(11);
    }

    private static void SLog(string message)
    {
        if (!EnableSurgicalLogs)
            return;

        Debug.Log($"[UnlockToastCatalog] {message}");
    }
}

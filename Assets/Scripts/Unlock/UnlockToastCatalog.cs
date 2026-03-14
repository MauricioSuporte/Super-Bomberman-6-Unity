using UnityEngine;

public static class UnlockToastCatalog
{
    private const bool EnableSurgicalLogs = true;

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
        switch (skin)
        {
            case BomberSkin.Gray:
                return new ToastInfo(
                    "You Know The One",
                    "Gray Bomber Unlocked",
                    "UI/Unlocks/Icons/GrayBomber"
                );

            case BomberSkin.Orange:
                return new ToastInfo(
                    "Orange You Glad?",
                    "Orange Bomber Unlocked",
                    "UI/Unlocks/Icons/OrangeBomber"
                );

            case BomberSkin.Purple:
                return new ToastInfo(
                    "That’s A Grape Idea!",
                    "Purple Bomber Unlocked",
                    "UI/Unlocks/Icons/PurpleBomber"
                );

            case BomberSkin.Olive:
                return new ToastInfo(
                    "Olive You!",
                    "Olive Bomber Unlocked",
                    "UI/Unlocks/Icons/OliveBomber"
                );

            case BomberSkin.Cyan:
                return new ToastInfo(
                    "Cyan is the impostor!",
                    "Cyan Bomber Unlocked",
                    "UI/Unlocks/Icons/CyanBomber"
                );

            case BomberSkin.Brown:
                return new ToastInfo(
                    "That Stinks!",
                    "Brown Bomber Unlocked",
                    "UI/Unlocks/Icons/BrownBomber"
                );

            default:
                return new ToastInfo(
                    "New Character",
                    $"{skin} Unlocked",
                    $"UI/Unlocks/Icons/{skin}"
                );
        }
    }

    public static ToastInfo GetBossRush()
    {
        return new ToastInfo(
            "It’s Time To Duel!",
            "Boss Rush Unlocked",
            "UI/Unlocks/Icons/BossRush"
        );
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

    private static void SLog(string message)
    {
        if (!EnableSurgicalLogs)
            return;

        Debug.Log($"[UnlockToastCatalog] {message}");
    }
}
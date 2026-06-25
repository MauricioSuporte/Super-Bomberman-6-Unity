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
            case BomberSkin.Gray:
                return new ToastInfo(
                    text.ToastNewCharacter,
                    string.Format(text.ToastSkinUnlocked, GetSkinDisplayName(skin)),
                    "UI/Unlocks/Icons/GrayBomber"
                );

            case BomberSkin.Orange:
                return new ToastInfo(
                    text.ToastNewCharacter,
                    string.Format(text.ToastSkinUnlocked, GetSkinDisplayName(skin)),
                    "UI/Unlocks/Icons/OrangeBomber"
                );

            case BomberSkin.Purple:
                return new ToastInfo(
                    text.ToastNewCharacter,
                    string.Format(text.ToastSkinUnlocked, GetSkinDisplayName(skin)),
                    "UI/Unlocks/Icons/PurpleBomber"
                );

            case BomberSkin.Olive:
                return new ToastInfo(
                    text.ToastNewCharacter,
                    string.Format(text.ToastSkinUnlocked, GetSkinDisplayName(skin)),
                    "UI/Unlocks/Icons/OliveBomber"
                );

            case BomberSkin.Cyan:
                return new ToastInfo(
                    text.ToastNewCharacter,
                    string.Format(text.ToastSkinUnlocked, GetSkinDisplayName(skin)),
                    "UI/Unlocks/Icons/CyanBomber"
                );

            case BomberSkin.Brown:
                return new ToastInfo(
                    text.ToastNewCharacter,
                    string.Format(text.ToastSkinUnlocked, GetSkinDisplayName(skin)),
                    "UI/Unlocks/Icons/BrownBomber"
                );

            case BomberSkin.DarkGreen:
                return new ToastInfo(
                    text.ToastNewCharacter,
                    string.Format(text.ToastSkinUnlocked, GetSkinDisplayName(skin)),
                    "UI/Unlocks/Icons/DarkGreenBomber"
                );

            case BomberSkin.DarkBlue:
                return new ToastInfo(
                    text.ToastNewCharacter,
                    string.Format(text.ToastSkinUnlocked, GetSkinDisplayName(skin)),
                    "UI/Unlocks/Icons/DarkBlueBomber"
                );

            case BomberSkin.Magenta:
                return new ToastInfo(
                    text.ToastNewCharacter,
                    string.Format(text.ToastSkinUnlocked, GetSkinDisplayName(skin)),
                    "UI/Unlocks/Icons/MagentaBomber"
                );

            case BomberSkin.Nightmare:
                return new ToastInfo(
                    text.ToastNewCharacter,
                    string.Format(text.ToastSkinUnlocked, GetSkinDisplayName(skin)),
                    "UI/Unlocks/Icons/NightmareBomber"
                );

            case BomberSkin.Gold:
                return new ToastInfo(
                    text.ToastNewCharacter,
                    string.Format(text.ToastSkinUnlocked, GetSkinDisplayName(skin)),
                    "UI/Unlocks/Icons/GoldBomber"
                );

            case BomberSkin.Golden:
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
        return SaveSystem.GetLanguage() switch
        {
            GameLanguage.Japanese => skin switch
            {
                BomberSkin.Gray => "グレー",
                BomberSkin.Orange => "オレンジ",
                BomberSkin.Purple => "パープル",
                BomberSkin.Olive => "オリーブ",
                BomberSkin.Cyan => "シアン",
                BomberSkin.Brown => "ブラウン",
                BomberSkin.DarkGreen => "ダークグリーン",
                BomberSkin.DarkBlue => "ダークブルー",
                BomberSkin.Magenta => "マゼンタ",
                BomberSkin.Nightmare => "ナイトメア",
                BomberSkin.Gold => "ゴールド",
                BomberSkin.Golden => "ゴールデン",
                _ => skin.ToString()
            },
            GameLanguage.Spanish => skin switch
            {
                BomberSkin.Gray => "Gris",
                BomberSkin.Orange => "Naranja",
                BomberSkin.Purple => "Morado",
                BomberSkin.Olive => "Oliva",
                BomberSkin.Cyan => "Cian",
                BomberSkin.Brown => "Marrón",
                BomberSkin.DarkGreen => "Verde Oscuro",
                BomberSkin.DarkBlue => "Azul Oscuro",
                BomberSkin.Magenta => "Magenta",
                BomberSkin.Nightmare => "Pesadilla",
                BomberSkin.Gold => "Dorado",
                BomberSkin.Golden => "Áureo",
                _ => skin.ToString()
            },
            GameLanguage.PortugueseBr => skin switch
            {
                BomberSkin.Gray => "Cinza",
                BomberSkin.Orange => "Laranja",
                BomberSkin.Purple => "Roxo",
                BomberSkin.Olive => "Oliva",
                BomberSkin.Cyan => "Ciano",
                BomberSkin.Brown => "Marrom",
                BomberSkin.DarkGreen => "Verde Escuro",
                BomberSkin.DarkBlue => "Azul Escuro",
                BomberSkin.Magenta => "Magenta",
                BomberSkin.Nightmare => "Pesadelo",
                BomberSkin.Gold => "Dourado",
                BomberSkin.Golden => "Áureo",
                _ => skin.ToString()
            },
            _ => skin switch
            {
                BomberSkin.DarkGreen => "Dark Green",
                BomberSkin.DarkBlue => "Dark Blue",
                _ => skin.ToString()
            }
        };
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

using System;
using UnityEngine;

public static class BomberSkinUnlockProgress
{
    public static string SaveDirectoryPath => SB6SaveSystem.SaveDirectoryPath;
    public static string SaveFilePath => SB6SaveSystem.SaveFilePath;

    public static void ReloadFromDisk()
    {
        SB6SaveSystem.Reload();
    }

    public static bool IsUnlocked(BomberSkin skin)
    {
        return SB6SaveSystem.Data.unlockedSkins.Contains(skin.ToString());
    }

    public static bool Unlock(BomberSkin skin)
    {
        string name = skin.ToString();

        if (SB6SaveSystem.Data.unlockedSkins.Contains(name))
            return false;

        SB6SaveSystem.Data.unlockedSkins.Add(name);
        SB6SaveSystem.Save();
        return true;
    }

    public static bool UnlockGray()
    {
        return Unlock(BomberSkin.Gray);
    }

    public static void ResetProgress()
    {
        SB6SaveSystem.Data.unlockedSkins.Clear();

        BomberSkin[] defaults =
        {
            BomberSkin.Golden,
            BomberSkin.White,
            BomberSkin.Black,
            BomberSkin.Red,
            BomberSkin.Orange,
            BomberSkin.Yellow,
            BomberSkin.Lime,
            BomberSkin.Green,
            BomberSkin.Cyan,
            BomberSkin.Aqua,
            BomberSkin.Blue,
            BomberSkin.DarkBlue,
            BomberSkin.Purple,
            BomberSkin.Magenta,
            BomberSkin.Pink,
            BomberSkin.Brown,
            BomberSkin.DarkGreen,
            BomberSkin.Nightmare,
            BomberSkin.Gold
        };

        for (int i = 0; i < defaults.Length; i++)
        {
            string name = defaults[i].ToString();
            if (!SB6SaveSystem.Data.unlockedSkins.Contains(name))
                SB6SaveSystem.Data.unlockedSkins.Add(name);
        }

        for (int p = 1; p <= 4; p++)
            PlayerPersistentStats.ClampSelectedSkinIfLocked(p);

        SB6SaveSystem.Save();
    }

    public static BomberSkin GetFallbackUnlockedSkin(BomberSkin preferredFallback = BomberSkin.White)
    {
        if (IsUnlocked(preferredFallback))
            return preferredFallback;

        foreach (BomberSkin skin in Enum.GetValues(typeof(BomberSkin)))
        {
            if (IsUnlocked(skin))
                return skin;
        }

        return BomberSkin.White;
    }

    public static BomberSkin ClampToUnlocked(BomberSkin skin, BomberSkin preferredFallback = BomberSkin.White)
    {
        return IsUnlocked(skin) ? skin : GetFallbackUnlockedSkin(preferredFallback);
    }

    public static bool SaveFileExists()
    {
        return System.IO.File.Exists(SaveFilePath);
    }
}
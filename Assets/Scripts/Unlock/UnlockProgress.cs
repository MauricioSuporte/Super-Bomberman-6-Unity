using System;
using UnityEngine;

public static class UnlockProgress
{
    private const bool EnableSurgicalLogs = true;

    public static event Action<BomberSkin> OnSkinUnlocked;
    public static event Action OnBossRushUnlocked;

    public static string SaveDirectoryPath => SaveSystem.SaveDirectoryPath;
    public static string SaveFilePath => SaveSystem.SaveFilePath;

    public static void ReloadFromDisk()
    {
        SaveSystem.Reload();
        SLog("ReloadFromDisk called");
    }

    public static bool IsUnlocked(BomberSkin skin)
    {
        return SaveSystem.Data.unlockedSkins.Contains(skin.ToString());
    }

    public static bool IsBossRushUnlocked()
    {
        return SaveSystem.Data.bossRushUnlocked;
    }

    public static bool Unlock(BomberSkin skin)
    {
        string name = skin.ToString();

        SLog($"Unlock requested | skin={skin} | alreadyUnlocked={SaveSystem.Data.unlockedSkins.Contains(name)} | listeners={(OnSkinUnlocked == null ? 0 : OnSkinUnlocked.GetInvocationList().Length)}");

        if (SaveSystem.Data.unlockedSkins.Contains(name))
        {
            SLog($"Unlock skipped | skin={skin} already unlocked");
            return false;
        }

        SaveSystem.Data.unlockedSkins.Add(name);
        SaveSystem.Save();

        SLog($"Unlock persisted | skin={skin} | invoking event now");
        OnSkinUnlocked?.Invoke(skin);
        SLog($"Unlock event invocation finished | skin={skin}");

        return true;
    }

    public static bool UnlockBossRush()
    {
        SLog($"UnlockBossRush requested | alreadyUnlocked={SaveSystem.Data.bossRushUnlocked} | listeners={(OnBossRushUnlocked == null ? 0 : OnBossRushUnlocked.GetInvocationList().Length)}");

        if (SaveSystem.Data.bossRushUnlocked)
        {
            SLog("UnlockBossRush skipped | already unlocked");
            return false;
        }

        SaveSystem.Data.bossRushUnlocked = true;
        SaveSystem.Save();

        SLog("UnlockBossRush persisted | invoking event now");
        OnBossRushUnlocked?.Invoke();
        SLog("UnlockBossRush event invocation finished");

        return true;
    }

    public static void ResetProgress()
    {
        SaveSystem.Data.unlockedSkins.Clear();
        SaveSystem.Data.bossRushUnlocked = false;

        BomberSkin[] defaults =
        {
            BomberSkin.White,
            BomberSkin.Black,
            BomberSkin.Blue,
            BomberSkin.Red,
            BomberSkin.Yellow,
            BomberSkin.Green,
            BomberSkin.Aqua,
            BomberSkin.Pink,
        };

        for (int i = 0; i < defaults.Length; i++)
        {
            string name = defaults[i].ToString();
            if (!SaveSystem.Data.unlockedSkins.Contains(name))
                SaveSystem.Data.unlockedSkins.Add(name);
        }

        for (int p = 1; p <= 4; p++)
            PlayerPersistentStats.ClampSelectedSkinIfLocked(p);

        SaveSystem.Save();
        SLog("ResetProgress completed");
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

    private static void SLog(string message)
    {
        if (!EnableSurgicalLogs)
            return;

        Debug.Log($"[UnlockProgress] {message}");
    }
}
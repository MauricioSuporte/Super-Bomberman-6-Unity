using System;
using UnityEngine;

public static class UnlockProgress
{
    private const bool EnableSurgicalLogs = false;

    private static readonly BomberSkin[] skinsRequiredForGoldenUnlock =
    {
        BomberSkin.White,
        BomberSkin.Gray,
        BomberSkin.Black,
        BomberSkin.Red,
        BomberSkin.Orange,
        BomberSkin.Yellow,
        BomberSkin.Olive,
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

    public static event Action<BomberSkin> OnSkinUnlocked;
    public static event Action OnBossRushUnlocked;

    public static string SaveDirectoryPath => SaveSystem.SaveDirectoryPath;
    public static string SaveFilePath => SaveSystem.SaveFilePath;

    public static void ReloadFromDisk()
    {
        SaveSystem.Reload();
        TryUnlockGoldenBomberIfEligible(raiseEvent: false);
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

        TryUnlockGoldenBomberIfEligible(raiseEvent: true);

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

    private static void TryUnlockGoldenBomberIfEligible(bool raiseEvent)
    {
        if (IsUnlocked(BomberSkin.Golden))
        {
            SLog("TryUnlockGoldenBomberIfEligible | Golden already unlocked");
            return;
        }

        if (!AreAllOtherBombersUnlocked())
        {
            SLog("TryUnlockGoldenBomberIfEligible | requirements not met");
            return;
        }

        string goldenName = BomberSkin.Golden.ToString();
        SaveSystem.Data.unlockedSkins.Add(goldenName);
        SaveSystem.Save();

        SLog($"TryUnlockGoldenBomberIfEligible | Golden unlocked | raiseEvent={raiseEvent}");

        if (raiseEvent)
            OnSkinUnlocked?.Invoke(BomberSkin.Golden);
    }

    private static bool AreAllOtherBombersUnlocked()
    {
        for (int i = 0; i < skinsRequiredForGoldenUnlock.Length; i++)
        {
            BomberSkin skin = skinsRequiredForGoldenUnlock[i];

            if (!IsUnlocked(skin))
            {
                SLog($"AreAllOtherBombersUnlocked | missing skin={skin}");
                return false;
            }
        }

        return true;
    }

    private static void SLog(string message)
    {
        if (!EnableSurgicalLogs)
            return;

        Debug.Log($"[UnlockProgress] {message}");
    }
}
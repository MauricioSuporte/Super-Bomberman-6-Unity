using System;
using UnityEngine;

public static class UnlockProgress
{
    private static readonly bool EnableSurgicalLogs = false;

    public static event Action<BomberSkin> OnSkinUnlocked;
    public static event Action OnBossRushUnlocked;
    public static event Action OnBattleModeStage11Unlocked;
    public static event Action<int> OnBattleModeStageUnlocked;

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

    public static bool IsBattleModeStage11Unlocked()
    {
        return SaveSystem.Data.battleModeStage11Unlocked;
    }

    public static bool IsBattleModeStage12Unlocked()
    {
        return SaveSystem.Data.battleModeStage12Unlocked;
    }

    public static bool IsBattleModeStage13Unlocked()
    {
        return SaveSystem.Data.battleModeStage13Unlocked;
    }

    public static bool IsBattleModeStage14Unlocked()
    {
        return SaveSystem.Data.battleModeStage14Unlocked;
    }

    public static bool IsBattleModeStage15Unlocked()
    {
        return SaveSystem.Data.battleModeStage15Unlocked;
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

        TryUnlockGoldenBomberIfEligible(raiseEvent: true);

        return true;
    }

    public static bool UnlockBattleModeStage11()
    {
        return UnlockBattleModeStage(11);
    }

    public static bool UnlockBattleModeStage(int stageIndex)
    {
        int normalized = Mathf.Clamp(stageIndex, 11, 15);
        SLog($"UnlockBattleModeStage requested | stage={normalized} | alreadyUnlocked={IsBattleModeStageUnlockedFlag(normalized)} | listeners={(OnBattleModeStageUnlocked == null ? 0 : OnBattleModeStageUnlocked.GetInvocationList().Length)}");

        if (IsBattleModeStageUnlockedFlag(normalized))
        {
            SLog($"UnlockBattleModeStage skipped | stage={normalized} already unlocked");
            return false;
        }

        SetBattleModeStageUnlockedFlag(normalized);
        SaveSystem.Save();

        SLog($"UnlockBattleModeStage persisted | stage={normalized} | invoking event now");
        if (normalized == 11)
            OnBattleModeStage11Unlocked?.Invoke();
        OnBattleModeStageUnlocked?.Invoke(normalized);
        SLog($"UnlockBattleModeStage event invocation finished | stage={normalized}");

        TryUnlockGoldenBomberIfEligible(raiseEvent: true);

        return true;
    }

    public static void EvaluateBattleModeStageUnlocks()
    {
        if (SaveSystem.HasBattleModeManStageWin(10))
            UnlockBattleModeStage(11);

        if (SaveSystem.HasBattleModeManStageWin(7) && SaveSystem.HasBattleModeManStageWin(9))
            UnlockBattleModeStage(12);

        if (SaveSystem.GetBattleModeManStageWinCount() >= 1)
            UnlockBattleModeStage(13);

        if (SaveSystem.GetBattleModeManStageWinCount() >= 7)
            UnlockBattleModeStage(14);

        if (SaveSystem.GetBattleModeManStageWinCount(14) >= 14)
            UnlockBattleModeStage(15);
    }

    public static bool TryUnlockGoldenBomberIfEligible()
    {
        return TryUnlockGoldenBomberIfEligible(raiseEvent: true);
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

    private static bool TryUnlockGoldenBomberIfEligible(bool raiseEvent)
    {
        if (IsUnlocked(BomberSkin.Golden))
        {
            SLog("TryUnlockGoldenBomberIfEligible | Golden already unlocked");
            return false;
        }

        if (!AchievementCatalog.AreAllRequiredForGoldenUnlocked())
        {
            SLog("TryUnlockGoldenBomberIfEligible | requirements not met");
            return false;
        }

        string goldenName = BomberSkin.Golden.ToString();
        SaveSystem.Data.unlockedSkins.Add(goldenName);
        SaveSystem.Save();

        SLog($"TryUnlockGoldenBomberIfEligible | Golden unlocked | raiseEvent={raiseEvent}");

        if (raiseEvent)
            OnSkinUnlocked?.Invoke(BomberSkin.Golden);

        return true;
    }

    private static bool IsBattleModeStageUnlockedFlag(int stageIndex)
    {
        return stageIndex switch
        {
            11 => SaveSystem.Data.battleModeStage11Unlocked,
            12 => SaveSystem.Data.battleModeStage12Unlocked,
            13 => SaveSystem.Data.battleModeStage13Unlocked,
            14 => SaveSystem.Data.battleModeStage14Unlocked,
            15 => SaveSystem.Data.battleModeStage15Unlocked,
            _ => true
        };
    }

    private static void SetBattleModeStageUnlockedFlag(int stageIndex)
    {
        switch (stageIndex)
        {
            case 11:
                SaveSystem.Data.battleModeStage11Unlocked = true;
                break;
            case 12:
                SaveSystem.Data.battleModeStage12Unlocked = true;
                break;
            case 13:
                SaveSystem.Data.battleModeStage13Unlocked = true;
                break;
            case 14:
                SaveSystem.Data.battleModeStage14Unlocked = true;
                break;
            case 15:
                SaveSystem.Data.battleModeStage15Unlocked = true;
                break;
        }
    }

    private static void SLog(string message)
    {
        if (!EnableSurgicalLogs)
            return;

        Debug.Log($"[UnlockProgress] {message}");
    }
}

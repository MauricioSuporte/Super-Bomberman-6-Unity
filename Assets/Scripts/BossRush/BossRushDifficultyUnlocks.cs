using UnityEngine;

public static class BossRushDifficultyUnlocks
{
    const string PrefNightmareUnlocked = "bossrush_nightmare_unlocked";

    public static bool IsNightmareUnlocked()
    {
        return PlayerPrefs.GetInt(PrefNightmareUnlocked, 0) == 1;
    }

    public static void UnlockNightmare()
    {
        PlayerPrefs.SetInt(PrefNightmareUnlocked, 1);
        PlayerPrefs.Save();
    }

    public static void LockNightmare()
    {
        PlayerPrefs.SetInt(PrefNightmareUnlocked, 0);
        PlayerPrefs.Save();
    }
}
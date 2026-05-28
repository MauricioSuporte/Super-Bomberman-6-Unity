public static class BossRushDifficultyUnlocks
{
    public static bool IsNightmareUnlocked()
    {
        return SaveSystem.IsNightmareUnlocked();
    }

    public static bool UnlockNightmare()
    {
        bool unlocked = SaveSystem.UnlockNightmare();
        if (unlocked)
            UnlockProgress.TryUnlockGoldenBomberIfEligible();

        return unlocked;
    }
}

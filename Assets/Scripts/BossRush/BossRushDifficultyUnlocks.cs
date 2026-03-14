public static class BossRushDifficultyUnlocks
{
    public static bool IsNightmareUnlocked()
    {
        return SaveSystem.IsNightmareUnlocked();
    }

    public static bool UnlockNightmare()
    {
        return SaveSystem.UnlockNightmare();
    }
}
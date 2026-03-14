using System.Collections.Generic;

public static class BossRushTimesProgress
{
    private const int MaxTopTimes = 3;

    public static List<float> GetTopTimes(BossRushDifficulty difficulty)
    {
        return SaveSystem.GetBossRushTopTimes(difficulty);
    }

    public static int RegisterTime(BossRushDifficulty difficulty, float seconds)
    {
        if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0f)
            return -1;

        var times = SaveSystem.GetBossRushTopTimes(difficulty);

        int insertIndex = 0;
        while (insertIndex < times.Count && times[insertIndex] <= seconds)
            insertIndex++;

        bool entersTop3 = insertIndex < MaxTopTimes || times.Count < MaxTopTimes;
        if (!entersTop3)
            return -1;

        times.Insert(insertIndex, seconds);

        if (times.Count > MaxTopTimes)
            times.RemoveRange(MaxTopTimes, times.Count - MaxTopTimes);

        SaveSystem.SetBossRushTopTimes(difficulty, times);

        return insertIndex;
    }

    public static bool HasAnyRecordedTime(BossRushDifficulty difficulty)
    {
        return SaveSystem.HasBossRushRecordedTime(difficulty);
    }
}
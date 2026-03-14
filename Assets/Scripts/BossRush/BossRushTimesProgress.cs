using System.Collections.Generic;
using UnityEngine;

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

    public static float GetUnlockTargetTime(BossRushDifficulty difficulty)
    {
        return SaveSystem.GetBossRushUnlockTargetTime(difficulty);
    }

    public static bool MeetsUnlockTarget(BossRushDifficulty difficulty, float seconds)
    {
        if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0f)
            return false;

        float target = GetUnlockTargetTime(difficulty);
        if (target <= 0f)
            return false;

        return seconds <= target;
    }

    public static bool HasMetUnlockTarget(BossRushDifficulty difficulty)
    {
        float target = GetUnlockTargetTime(difficulty);
        if (target <= 0f)
            return true;

        List<float> times = GetTopTimes(difficulty);
        if (times == null || times.Count == 0)
            return false;

        float bestTime = times[0];

        if (float.IsNaN(bestTime) || float.IsInfinity(bestTime) || bestTime < 0f)
            return false;

        return bestTime <= target;
    }

    public static string FormatTime(float seconds)
    {
        if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0f)
            return "--:--.--";

        int totalCentiseconds = Mathf.FloorToInt(seconds * 100f);
        int minutes = totalCentiseconds / 6000;
        int secs = (totalCentiseconds / 100) % 60;
        int centiseconds = totalCentiseconds % 100;

        return $"{minutes:00}:{secs:00}.{centiseconds:00}";
    }
}
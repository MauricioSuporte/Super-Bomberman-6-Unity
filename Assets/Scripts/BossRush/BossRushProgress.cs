using System.Collections.Generic;
using UnityEngine;

public static class BossRushProgress
{
    private const string SelectedDifficultyKey = "BossRush_SelectedDifficulty";

    public static BossRushDifficulty GetSelectedDifficulty()
    {
        return (BossRushDifficulty)PlayerPrefs.GetInt(SelectedDifficultyKey, (int)BossRushDifficulty.NORMAL);
    }

    public static void SetSelectedDifficulty(BossRushDifficulty difficulty)
    {
        PlayerPrefs.SetInt(SelectedDifficultyKey, (int)difficulty);
        PlayerPrefs.Save();
    }

    public static int GetStartingItemAmount(BossRushDifficulty difficulty)
    {
        switch (difficulty)
        {
            case BossRushDifficulty.EASY: return 4;
            case BossRushDifficulty.NORMAL: return 3;
            case BossRushDifficulty.HARD: return 2;
            case BossRushDifficulty.NIGHTMARE: return 1;
            default: return 3;
        }
    }

    public static List<float> GetTopTimes(BossRushDifficulty difficulty)
    {
        // Placeholder por enquanto.
        // Depois a gente troca para ler PlayerPrefs / arquivo / save real.
        return new List<float>();
    }

    public static string FormatTime(float seconds)
    {
        if (seconds < 0f)
            seconds = 0f;

        int totalMs = Mathf.RoundToInt(seconds * 1000f);
        int minutes = totalMs / 60000;
        int remain = totalMs % 60000;
        int secs = remain / 1000;
        int ms = remain % 1000;

        return $"{minutes:00}:{secs:00}.{ms:000}";
    }
}
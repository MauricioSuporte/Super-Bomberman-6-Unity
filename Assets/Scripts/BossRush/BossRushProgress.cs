using System.Collections.Generic;
using UnityEngine;

public static class BossRushProgress
{
    static BossRushDifficulty? onlineDifficulty;
    public static void ClearOnlineSelection() => onlineDifficulty = null;
    public static void ApplyOnlineSelection(BossRushDifficulty difficulty, bool owner)
    {
        onlineDifficulty = difficulty;
        if (!owner) return;
        if (Application.isBatchMode && !string.IsNullOrWhiteSpace(System.Environment.GetEnvironmentVariable("SB6_SAVE_DIRECTORY"))) return;
        PlayerPrefs.SetInt(SelectedDifficultyKey, (int)difficulty);
        PlayerPrefs.Save();
    }

    private const string SelectedDifficultyKey = "BossRush_SelectedDifficulty";

    public static BossRushDifficulty GetSelectedDifficulty()
    {
        return onlineDifficulty ?? (BossRushDifficulty)PlayerPrefs.GetInt(SelectedDifficultyKey, (int)BossRushDifficulty.NORMAL);
    }

    public static void SetSelectedDifficulty(BossRushDifficulty difficulty)
    {
        if (Assets.Scripts.Netcode.NetSync.IsOnline) { onlineDifficulty = difficulty; return; }
        PlayerPrefs.SetInt(SelectedDifficultyKey, (int)difficulty);
        PlayerPrefs.Save();
    }

    public static List<float> GetTopTimes(BossRushDifficulty difficulty)
    {
        return BossRushTimesProgress.GetTopTimes(difficulty);
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
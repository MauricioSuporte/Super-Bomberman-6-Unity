using System;
using System.Collections.Generic;
using UnityEngine;

public static class BossRushTimesProgress
{
    private const string TimesKeyPrefix = "SB6_BossRushTimes_";
    private const char Separator = '|';
    private const int MaxTopTimes = 3;

    private static bool loaded;
    private static readonly Dictionary<BossRushDifficulty, List<float>> topTimesByDifficulty = new();

    public static List<float> GetTopTimes(BossRushDifficulty difficulty)
    {
        Load();

        if (!topTimesByDifficulty.TryGetValue(difficulty, out var times))
            return new List<float>();

        return new List<float>(times);
    }

    /// <summary>
    /// Registra um tempo na dificuldade informada.
    /// Retorna a posição do ranking (0, 1, 2) se entrou no top 3.
    /// Retorna -1 se não entrou.
    /// </summary>
    public static int RegisterTime(BossRushDifficulty difficulty, float seconds)
    {
        Load();

        if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0f)
            return -1;

        var times = GetOrCreateList(difficulty);

        int insertIndex = 0;
        while (insertIndex < times.Count && times[insertIndex] <= seconds)
            insertIndex++;

        bool entersTop3 = insertIndex < MaxTopTimes || times.Count < MaxTopTimes;
        if (!entersTop3)
            return -1;

        times.Insert(insertIndex, seconds);

        if (times.Count > MaxTopTimes)
            times.RemoveRange(MaxTopTimes, times.Count - MaxTopTimes);

        SaveDifficulty(difficulty);
        PlayerPrefs.Save();

        return insertIndex;
    }

    public static void ResetDifficulty(BossRushDifficulty difficulty)
    {
        Load();

        GetOrCreateList(difficulty).Clear();
        SaveDifficulty(difficulty);
        PlayerPrefs.Save();
    }

    public static void ResetAll()
    {
        loaded = true;
        topTimesByDifficulty.Clear();

        foreach (BossRushDifficulty difficulty in Enum.GetValues(typeof(BossRushDifficulty)))
            PlayerPrefs.DeleteKey(GetTimesKey(difficulty));

        PlayerPrefs.Save();
    }

    private static void Load()
    {
        if (loaded)
            return;

        loaded = true;
        topTimesByDifficulty.Clear();

        foreach (BossRushDifficulty difficulty in Enum.GetValues(typeof(BossRushDifficulty)))
        {
            var list = new List<float>();
            string raw = PlayerPrefs.GetString(GetTimesKey(difficulty), string.Empty);

            if (!string.IsNullOrWhiteSpace(raw))
            {
                string[] parts = raw.Split(Separator);

                for (int i = 0; i < parts.Length; i++)
                {
                    if (float.TryParse(parts[i], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float value))
                    {
                        if (!float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f)
                            list.Add(value);
                    }
                }

                list.Sort((a, b) => a.CompareTo(b));

                if (list.Count > MaxTopTimes)
                    list.RemoveRange(MaxTopTimes, list.Count - MaxTopTimes);
            }

            topTimesByDifficulty[difficulty] = list;
        }
    }

    private static List<float> GetOrCreateList(BossRushDifficulty difficulty)
    {
        if (!topTimesByDifficulty.TryGetValue(difficulty, out var list))
        {
            list = new List<float>();
            topTimesByDifficulty[difficulty] = list;
        }

        return list;
    }

    private static void SaveDifficulty(BossRushDifficulty difficulty)
    {
        var list = GetOrCreateList(difficulty);
        list.Sort((a, b) => a.CompareTo(b));

        if (list.Count > MaxTopTimes)
            list.RemoveRange(MaxTopTimes, list.Count - MaxTopTimes);

        string[] values = new string[list.Count];
        for (int i = 0; i < list.Count; i++)
            values[i] = list[i].ToString("0.000", System.Globalization.CultureInfo.InvariantCulture);

        string raw = string.Join(Separator.ToString(), values);
        PlayerPrefs.SetString(GetTimesKey(difficulty), raw);
    }

    private static string GetTimesKey(BossRushDifficulty difficulty)
    {
        return $"{TimesKeyPrefix}{difficulty}";
    }
}
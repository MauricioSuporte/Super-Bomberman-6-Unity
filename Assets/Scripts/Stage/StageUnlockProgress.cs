using System.Collections.Generic;
using UnityEngine;

public static class StageUnlockProgress
{
    private const string UnlockedStagesKey = "SB6_UnlockedStages";
    private const string ClearedStagesKey = "SB6_ClearedStages";
    private const string StageOrderKey = "SB6_StageOrder";
    private const char Separator = '|';

    private static bool loaded;
    private static readonly HashSet<string> unlockedStages = new();
    private static readonly HashSet<string> clearedStages = new();
    private static List<string> stageOrder = new();

    public static void RegisterStageOrder(IEnumerable<string> orderedSceneNames)
    {
        Load();

        List<string> newOrder = new();

        if (orderedSceneNames != null)
        {
            foreach (string sceneName in orderedSceneNames)
            {
                string normalized = Normalize(sceneName);

                if (string.IsNullOrEmpty(normalized))
                    continue;

                if (!newOrder.Contains(normalized))
                    newOrder.Add(normalized);
            }
        }

        if (newOrder.Count <= 0)
        {
            EnsureDefaultUnlocked();
            return;
        }

        bool orderChanged = !SequenceEquals(stageOrder, newOrder);

        if (orderChanged)
        {
            stageOrder = newOrder;
            SaveStageOrder();
        }

        EnsureDefaultUnlocked();
    }

    public static bool IsUnlocked(string sceneName)
    {
        Load();
        EnsureDefaultUnlocked();

        string normalized = Normalize(sceneName);
        if (string.IsNullOrEmpty(normalized))
            return false;

        return unlockedStages.Contains(normalized);
    }

    public static bool IsCleared(string sceneName)
    {
        Load();
        EnsureDefaultUnlocked();

        string normalized = Normalize(sceneName);
        if (string.IsNullOrEmpty(normalized))
            return false;

        return clearedStages.Contains(normalized);
    }

    public static void Unlock(string sceneName)
    {
        Load();
        EnsureDefaultUnlocked();

        string normalized = Normalize(sceneName);
        if (string.IsNullOrEmpty(normalized))
            return;

        if (unlockedStages.Add(normalized))
            SaveUnlockedStages();
    }

    public static void MarkCleared(string sceneName)
    {
        Load();
        EnsureDefaultUnlocked();

        string normalized = Normalize(sceneName);
        if (string.IsNullOrEmpty(normalized))
            return;

        bool changed = false;

        if (unlockedStages.Add(normalized))
            changed = true;

        if (clearedStages.Add(normalized))
            changed = true;

        if (changed)
        {
            SaveUnlockedStages();
            SaveClearedStages();
        }
    }

    public static void UnlockCurrentAndNext(string currentSceneName)
    {
        Load();
        EnsureDefaultUnlocked();

        string normalizedCurrent = Normalize(currentSceneName);
        if (string.IsNullOrEmpty(normalizedCurrent))
            return;

        bool unlockedChanged = false;
        bool clearedChanged = false;

        if (unlockedStages.Add(normalizedCurrent))
            unlockedChanged = true;

        if (clearedStages.Add(normalizedCurrent))
            clearedChanged = true;

        int currentIndex = stageOrder.IndexOf(normalizedCurrent);
        if (currentIndex >= 0)
        {
            int nextIndex = currentIndex + 1;
            if (nextIndex < stageOrder.Count)
            {
                string nextScene = stageOrder[nextIndex];
                if (!string.IsNullOrEmpty(nextScene) && unlockedStages.Add(nextScene))
                    unlockedChanged = true;
            }
        }

        if (unlockedChanged)
            SaveUnlockedStages();

        if (clearedChanged)
            SaveClearedStages();
    }

    public static void ResetProgress()
    {
        loaded = true;
        unlockedStages.Clear();
        clearedStages.Clear();
        stageOrder.Clear();

        PlayerPrefs.DeleteKey(UnlockedStagesKey);
        PlayerPrefs.DeleteKey(ClearedStagesKey);
        PlayerPrefs.DeleteKey(StageOrderKey);
        PlayerPrefs.Save();
    }

    private static void EnsureDefaultUnlocked()
    {
        if (unlockedStages.Count > 0)
            return;

        string firstStage = null;

        if (stageOrder.Count > 0)
            firstStage = stageOrder[0];

        if (string.IsNullOrEmpty(firstStage))
            firstStage = "Stage_1-1";

        unlockedStages.Add(firstStage);
        SaveUnlockedStages();
    }

    private static void Load()
    {
        if (loaded)
            return;

        loaded = true;
        unlockedStages.Clear();
        clearedStages.Clear();
        stageOrder.Clear();

        string unlockedRaw = PlayerPrefs.GetString(UnlockedStagesKey, string.Empty);
        if (!string.IsNullOrEmpty(unlockedRaw))
        {
            string[] parts = unlockedRaw.Split(Separator);
            for (int i = 0; i < parts.Length; i++)
            {
                string normalized = Normalize(parts[i]);
                if (!string.IsNullOrEmpty(normalized))
                    unlockedStages.Add(normalized);
            }
        }

        string clearedRaw = PlayerPrefs.GetString(ClearedStagesKey, string.Empty);
        if (!string.IsNullOrEmpty(clearedRaw))
        {
            string[] parts = clearedRaw.Split(Separator);
            for (int i = 0; i < parts.Length; i++)
            {
                string normalized = Normalize(parts[i]);
                if (!string.IsNullOrEmpty(normalized))
                    clearedStages.Add(normalized);
            }
        }

        string orderRaw = PlayerPrefs.GetString(StageOrderKey, string.Empty);
        if (!string.IsNullOrEmpty(orderRaw))
        {
            string[] parts = orderRaw.Split(Separator);
            for (int i = 0; i < parts.Length; i++)
            {
                string normalized = Normalize(parts[i]);
                if (!string.IsNullOrEmpty(normalized) && !stageOrder.Contains(normalized))
                    stageOrder.Add(normalized);
            }
        }
    }

    private static void SaveUnlockedStages()
    {
        string raw = string.Join(Separator.ToString(), new List<string>(unlockedStages));
        PlayerPrefs.SetString(UnlockedStagesKey, raw);
        PlayerPrefs.Save();
    }

    private static void SaveClearedStages()
    {
        string raw = string.Join(Separator.ToString(), new List<string>(clearedStages));
        PlayerPrefs.SetString(ClearedStagesKey, raw);
        PlayerPrefs.Save();
    }

    private static void SaveStageOrder()
    {
        string raw = string.Join(Separator.ToString(), stageOrder);
        PlayerPrefs.SetString(StageOrderKey, raw);
        PlayerPrefs.Save();
    }

    private static string Normalize(string sceneName)
    {
        return string.IsNullOrWhiteSpace(sceneName) ? string.Empty : sceneName.Trim();
    }

    private static bool SequenceEquals(List<string> a, List<string> b)
    {
        if (ReferenceEquals(a, b))
            return true;

        if (a == null || b == null)
            return false;

        if (a.Count != b.Count)
            return false;

        for (int i = 0; i < a.Count; i++)
        {
            if (a[i] != b[i])
                return false;
        }

        return true;
    }
}
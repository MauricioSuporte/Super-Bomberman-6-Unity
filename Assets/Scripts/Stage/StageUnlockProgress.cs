using System.Collections.Generic;
using UnityEngine;

public static class StageUnlockProgress
{
    private const string UnlockedStagesKey = "SB6_UnlockedStages";
    private const string StageOrderKey = "SB6_StageOrder";
    private const char Separator = '|';

    private static bool loaded;
    private static readonly HashSet<string> unlockedStages = new();
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

    public static void UnlockCurrentAndNext(string currentSceneName)
    {
        Load();
        EnsureDefaultUnlocked();

        string normalizedCurrent = Normalize(currentSceneName);
        if (string.IsNullOrEmpty(normalizedCurrent))
            return;

        bool changed = false;

        if (unlockedStages.Add(normalizedCurrent))
            changed = true;

        int currentIndex = stageOrder.IndexOf(normalizedCurrent);
        if (currentIndex >= 0)
        {
            int nextIndex = currentIndex + 1;
            if (nextIndex < stageOrder.Count)
            {
                string nextScene = stageOrder[nextIndex];
                if (!string.IsNullOrEmpty(nextScene) && unlockedStages.Add(nextScene))
                    changed = true;
            }
        }

        if (changed)
            SaveUnlockedStages();
    }

    public static void ResetProgress()
    {
        loaded = true;
        unlockedStages.Clear();
        stageOrder.Clear();

        PlayerPrefs.DeleteKey(UnlockedStagesKey);
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
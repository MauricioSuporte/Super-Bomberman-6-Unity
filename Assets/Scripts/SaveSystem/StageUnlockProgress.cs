using System.Collections.Generic;
using UnityEngine;

public static class StageUnlockProgress
{
    public static void ReloadFromPrefs()
    {
        SB6SaveSystem.Reload();
        EnsureDefaultUnlocked();
        TryUnlockBossRush();
    }

    public static void RegisterStageOrder(IEnumerable<string> orderedSceneNames)
    {
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

        if (newOrder.Count > 0)
            SB6SaveSystem.Data.stageOrder = newOrder;

        EnsureDefaultUnlocked();
        TryUnlockBossRush();
        SB6SaveSystem.Save();
    }

    public static bool IsUnlocked(string sceneName)
    {
        EnsureDefaultUnlocked();

        string normalized = Normalize(sceneName);
        if (string.IsNullOrEmpty(normalized))
            return false;

        return SB6SaveSystem.Data.unlockedStages.Contains(normalized);
    }

    public static bool IsCleared(string sceneName)
    {
        EnsureDefaultUnlocked();

        string normalized = Normalize(sceneName);
        if (string.IsNullOrEmpty(normalized))
            return false;

        return SB6SaveSystem.Data.clearedStages.Contains(normalized);
    }

    public static bool IsPerfect(string sceneName)
    {
        EnsureDefaultUnlocked();

        string normalized = Normalize(sceneName);
        if (string.IsNullOrEmpty(normalized))
            return false;

        return SB6SaveSystem.Data.perfectStages.Contains(normalized);
    }

    public static bool HasClearedAllRegisteredStages()
    {
        EnsureDefaultUnlocked();

        if (SB6SaveSystem.Data.stageOrder == null || SB6SaveSystem.Data.stageOrder.Count <= 0)
            return false;

        for (int i = 0; i < SB6SaveSystem.Data.stageOrder.Count; i++)
        {
            string sceneName = SB6SaveSystem.Data.stageOrder[i];
            if (string.IsNullOrEmpty(sceneName))
                continue;

            if (!SB6SaveSystem.Data.clearedStages.Contains(sceneName))
                return false;
        }

        return true;
    }

    public static bool IsBossRushUnlocked()
    {
        return SB6SaveSystem.Data.bossRushUnlocked;
    }

    public static void UnlockBossRushPermanently()
    {
        if (SB6SaveSystem.Data.bossRushUnlocked)
            return;

        SB6SaveSystem.Data.bossRushUnlocked = true;
        SB6SaveSystem.Save();
    }

    public static void ResetBossRushUnlock()
    {
        SB6SaveSystem.Data.bossRushUnlocked = false;
        SB6SaveSystem.Save();
    }

    public static int GetRegisteredStageCount()
    {
        return SB6SaveSystem.Data.stageOrder != null ? SB6SaveSystem.Data.stageOrder.Count : 0;
    }

    public static int GetClearedRegisteredStageCount()
    {
        if (SB6SaveSystem.Data.stageOrder == null || SB6SaveSystem.Data.stageOrder.Count <= 0)
            return 0;

        int count = 0;

        for (int i = 0; i < SB6SaveSystem.Data.stageOrder.Count; i++)
        {
            string sceneName = SB6SaveSystem.Data.stageOrder[i];
            if (!string.IsNullOrEmpty(sceneName) && SB6SaveSystem.Data.clearedStages.Contains(sceneName))
                count++;
        }

        return count;
    }

    public static void Unlock(string sceneName)
    {
        if (ShouldIgnoreProgressPersistence())
            return;

        EnsureDefaultUnlocked();

        string normalized = Normalize(sceneName);
        if (string.IsNullOrEmpty(normalized))
            return;

        if (!SB6SaveSystem.Data.unlockedStages.Contains(normalized))
        {
            SB6SaveSystem.Data.unlockedStages.Add(normalized);
            SB6SaveSystem.Save();
        }
    }

    public static void MarkCleared(string sceneName)
    {
        if (ShouldIgnoreProgressPersistence())
            return;

        EnsureDefaultUnlocked();

        string normalized = Normalize(sceneName);
        if (string.IsNullOrEmpty(normalized))
            return;

        bool changed = false;

        if (!SB6SaveSystem.Data.unlockedStages.Contains(normalized))
        {
            SB6SaveSystem.Data.unlockedStages.Add(normalized);
            changed = true;
        }

        if (!SB6SaveSystem.Data.clearedStages.Contains(normalized))
        {
            SB6SaveSystem.Data.clearedStages.Add(normalized);
            changed = true;
        }

        bool bossRushBefore = SB6SaveSystem.Data.bossRushUnlocked;
        TryUnlockBossRush();

        if (changed || SB6SaveSystem.Data.bossRushUnlocked != bossRushBefore)
            SB6SaveSystem.Save();
    }

    public static void MarkPerfect(string sceneName)
    {
        if (ShouldIgnoreProgressPersistence())
            return;

        EnsureDefaultUnlocked();

        string normalized = Normalize(sceneName);
        if (string.IsNullOrEmpty(normalized))
            return;

        bool changed = false;

        if (!SB6SaveSystem.Data.unlockedStages.Contains(normalized))
        {
            SB6SaveSystem.Data.unlockedStages.Add(normalized);
            changed = true;
        }

        if (!SB6SaveSystem.Data.clearedStages.Contains(normalized))
        {
            SB6SaveSystem.Data.clearedStages.Add(normalized);
            changed = true;
        }

        if (!SB6SaveSystem.Data.perfectStages.Contains(normalized))
        {
            SB6SaveSystem.Data.perfectStages.Add(normalized);
            changed = true;
        }

        bool bossRushBefore = SB6SaveSystem.Data.bossRushUnlocked;
        TryUnlockBossRush();

        if (changed || SB6SaveSystem.Data.bossRushUnlocked != bossRushBefore)
            SB6SaveSystem.Save();
    }

    public static void UnlockCurrentAndNext(string currentSceneName)
    {
        if (ShouldIgnoreProgressPersistence())
            return;

        EnsureDefaultUnlocked();

        string normalizedCurrent = Normalize(currentSceneName);
        if (string.IsNullOrEmpty(normalizedCurrent))
            return;

        bool changed = false;

        if (!SB6SaveSystem.Data.unlockedStages.Contains(normalizedCurrent))
        {
            SB6SaveSystem.Data.unlockedStages.Add(normalizedCurrent);
            changed = true;
        }

        if (!SB6SaveSystem.Data.clearedStages.Contains(normalizedCurrent))
        {
            SB6SaveSystem.Data.clearedStages.Add(normalizedCurrent);
            changed = true;
        }

        int currentIndex = SB6SaveSystem.Data.stageOrder.IndexOf(normalizedCurrent);
        if (currentIndex >= 0)
        {
            int nextIndex = currentIndex + 1;
            if (nextIndex < SB6SaveSystem.Data.stageOrder.Count)
            {
                string nextScene = SB6SaveSystem.Data.stageOrder[nextIndex];
                if (!string.IsNullOrEmpty(nextScene) && !SB6SaveSystem.Data.unlockedStages.Contains(nextScene))
                {
                    SB6SaveSystem.Data.unlockedStages.Add(nextScene);
                    changed = true;
                }
            }
        }

        bool bossRushBefore = SB6SaveSystem.Data.bossRushUnlocked;
        TryUnlockBossRush();

        if (changed || SB6SaveSystem.Data.bossRushUnlocked != bossRushBefore)
            SB6SaveSystem.Save();
    }

    public static void ResetProgress()
    {
        SB6SaveSystem.Data.unlockedStages.Clear();
        SB6SaveSystem.Data.clearedStages.Clear();
        SB6SaveSystem.Data.perfectStages.Clear();
        SB6SaveSystem.Data.stageOrder.Clear();
        SB6SaveSystem.Data.bossRushUnlocked = false;

        EnsureDefaultUnlocked();
        SB6SaveSystem.Save();
    }

    private static void EnsureDefaultUnlocked()
    {
        if (SB6SaveSystem.Data.unlockedStages.Count > 0)
            return;

        string firstStage = null;

        if (SB6SaveSystem.Data.stageOrder.Count > 0)
            firstStage = SB6SaveSystem.Data.stageOrder[0];

        if (string.IsNullOrEmpty(firstStage))
            firstStage = "Stage_1-1";

        SB6SaveSystem.Data.unlockedStages.Add(firstStage);
    }

    private static void TryUnlockBossRush()
    {
        if (SB6SaveSystem.Data.bossRushUnlocked)
            return;

        if (HasClearedAllRegisteredStages())
            SB6SaveSystem.Data.bossRushUnlocked = true;
    }

    private static string Normalize(string sceneName)
    {
        return string.IsNullOrWhiteSpace(sceneName) ? string.Empty : sceneName.Trim();
    }

    private static bool ShouldIgnoreProgressPersistence()
    {
        return BossRushSession.IsActive;
    }
}
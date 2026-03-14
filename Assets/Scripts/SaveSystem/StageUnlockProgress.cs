using System.Collections.Generic;

public static class StageUnlockProgress
{
    public static void ReloadFromPrefs()
    {
        SaveSystem.Reload();
        EnsureDefaultUnlocked();
        TryUnlockAllClearRewards();
    }

    public static void RegisterStageOrder(IEnumerable<string> orderedSceneNames)
    {
        var slot = SaveSystem.ActiveSlot;
        if (slot == null)
            return;

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
            slot.stageOrder = newOrder;

        EnsureDefaultUnlocked();
        TryUnlockAllClearRewards();
        SaveSystem.Save();
    }

    public static bool IsUnlocked(string sceneName)
    {
        var slot = SaveSystem.ActiveSlot;
        if (slot == null)
            return false;

        EnsureDefaultUnlocked();

        string normalized = Normalize(sceneName);
        if (string.IsNullOrEmpty(normalized))
            return false;

        return slot.unlockedStages.Contains(normalized);
    }

    public static bool IsCleared(string sceneName)
    {
        var slot = SaveSystem.ActiveSlot;
        if (slot == null)
            return false;

        EnsureDefaultUnlocked();

        string normalized = Normalize(sceneName);
        if (string.IsNullOrEmpty(normalized))
            return false;

        return slot.clearedStages.Contains(normalized);
    }

    public static bool IsPerfect(string sceneName)
    {
        var slot = SaveSystem.ActiveSlot;
        if (slot == null)
            return false;

        EnsureDefaultUnlocked();

        string normalized = Normalize(sceneName);
        if (string.IsNullOrEmpty(normalized))
            return false;

        return slot.perfectStages.Contains(normalized);
    }

    public static bool HasClearedAllRegisteredStages()
    {
        var slot = SaveSystem.ActiveSlot;
        if (slot == null)
            return false;

        EnsureDefaultUnlocked();

        if (slot.stageOrder == null || slot.stageOrder.Count <= 0)
            return false;

        for (int i = 0; i < slot.stageOrder.Count; i++)
        {
            string sceneName = slot.stageOrder[i];
            if (string.IsNullOrEmpty(sceneName))
                continue;

            if (!slot.clearedStages.Contains(sceneName))
                return false;
        }

        return true;
    }

    public static bool IsBossRushUnlocked()
    {
        return SaveSystem.Data.bossRushUnlocked;
    }

    public static void UnlockBossRushPermanently()
    {
        if (SaveSystem.Data.bossRushUnlocked)
            return;

        UnlockProgress.UnlockBossRush();
    }

    public static void ResetBossRushUnlock()
    {
        SaveSystem.Data.bossRushUnlocked = false;
        SaveSystem.Save();
    }

    public static int GetRegisteredStageCount()
    {
        var slot = SaveSystem.ActiveSlot;
        if (slot == null)
            return 0;

        return slot.stageOrder != null ? slot.stageOrder.Count : 0;
    }

    public static int GetClearedRegisteredStageCount()
    {
        var slot = SaveSystem.ActiveSlot;
        if (slot == null)
            return 0;

        if (slot.stageOrder == null || slot.stageOrder.Count <= 0)
            return 0;

        int count = 0;

        for (int i = 0; i < slot.stageOrder.Count; i++)
        {
            string sceneName = slot.stageOrder[i];
            if (!string.IsNullOrEmpty(sceneName) && slot.clearedStages.Contains(sceneName))
                count++;
        }

        return count;
    }

    public static void Unlock(string sceneName)
    {
        if (ShouldIgnoreProgressPersistence())
            return;

        var slot = SaveSystem.ActiveSlot;
        if (slot == null)
            return;

        EnsureDefaultUnlocked();

        string normalized = Normalize(sceneName);
        if (string.IsNullOrEmpty(normalized))
            return;

        if (!slot.unlockedStages.Contains(normalized))
        {
            slot.unlockedStages.Add(normalized);
            SaveSystem.Save();
        }
    }

    public static void MarkCleared(string sceneName)
    {
        if (ShouldIgnoreProgressPersistence())
            return;

        var slot = SaveSystem.ActiveSlot;
        if (slot == null)
            return;

        EnsureDefaultUnlocked();

        string normalized = Normalize(sceneName);
        if (string.IsNullOrEmpty(normalized))
            return;

        bool changed = false;

        if (!slot.unlockedStages.Contains(normalized))
        {
            slot.unlockedStages.Add(normalized);
            changed = true;
        }

        if (!slot.clearedStages.Contains(normalized))
        {
            slot.clearedStages.Add(normalized);
            changed = true;
        }

        bool rewardsChanged = TryUnlockAllClearRewards();

        if (changed || rewardsChanged)
            SaveSystem.Save();
    }

    public static void MarkPerfect(string sceneName)
    {
        if (ShouldIgnoreProgressPersistence())
            return;

        var slot = SaveSystem.ActiveSlot;
        if (slot == null)
            return;

        EnsureDefaultUnlocked();

        string normalized = Normalize(sceneName);
        if (string.IsNullOrEmpty(normalized))
            return;

        bool changed = false;

        if (!slot.unlockedStages.Contains(normalized))
        {
            slot.unlockedStages.Add(normalized);
            changed = true;
        }

        if (!slot.clearedStages.Contains(normalized))
        {
            slot.clearedStages.Add(normalized);
            changed = true;
        }

        if (!slot.perfectStages.Contains(normalized))
        {
            slot.perfectStages.Add(normalized);
            changed = true;
        }

        bool rewardsChanged = TryUnlockAllClearRewards();

        if (changed || rewardsChanged)
            SaveSystem.Save();
    }

    public static void UnlockCurrentAndNext(string currentSceneName)
    {
        if (ShouldIgnoreProgressPersistence())
            return;

        var slot = SaveSystem.ActiveSlot;
        if (slot == null)
            return;

        EnsureDefaultUnlocked();

        string normalizedCurrent = Normalize(currentSceneName);
        if (string.IsNullOrEmpty(normalizedCurrent))
            return;

        bool changed = false;

        if (!slot.unlockedStages.Contains(normalizedCurrent))
        {
            slot.unlockedStages.Add(normalizedCurrent);
            changed = true;
        }

        if (!slot.clearedStages.Contains(normalizedCurrent))
        {
            slot.clearedStages.Add(normalizedCurrent);
            changed = true;
        }

        int currentIndex = slot.stageOrder.IndexOf(normalizedCurrent);
        if (currentIndex >= 0)
        {
            int nextIndex = currentIndex + 1;
            if (nextIndex < slot.stageOrder.Count)
            {
                string nextScene = slot.stageOrder[nextIndex];
                if (!string.IsNullOrEmpty(nextScene) && !slot.unlockedStages.Contains(nextScene))
                {
                    slot.unlockedStages.Add(nextScene);
                    changed = true;
                }
            }
        }

        bool rewardsChanged = TryUnlockAllClearRewards();

        if (changed || rewardsChanged)
            SaveSystem.Save();
    }

    public static void ResetProgress()
    {
        var slot = SaveSystem.ActiveSlot;
        if (slot == null)
            return;

        slot.unlockedStages.Clear();
        slot.clearedStages.Clear();
        slot.perfectStages.Clear();
        slot.stageOrder.Clear();
        SaveSystem.Data.bossRushUnlocked = false;

        EnsureDefaultUnlocked();
        SaveSystem.Save();
    }

    private static void EnsureDefaultUnlocked()
    {
        var slot = SaveSystem.ActiveSlot;
        if (slot == null)
            return;

        if (slot.unlockedStages.Count > 0)
            return;

        string firstStage = null;

        if (slot.stageOrder.Count > 0)
            firstStage = slot.stageOrder[0];

        if (string.IsNullOrEmpty(firstStage))
            firstStage = "Stage_1-1";

        slot.unlockedStages.Add(firstStage);
    }

    private static bool TryUnlockAllClearRewards()
    {
        if (!HasClearedAllRegisteredStages())
            return false;

        bool changed = false;

        if (UnlockProgress.Unlock(BomberSkin.Orange))
            changed = true;

        if (UnlockProgress.Unlock(BomberSkin.Purple))
            changed = true;

        if (UnlockProgress.UnlockBossRush())
            changed = true;

        if (changed)
        {
            for (int p = 1; p <= 4; p++)
                PlayerPersistentStats.ClampSelectedSkinIfLocked(p);
        }

        return changed;
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
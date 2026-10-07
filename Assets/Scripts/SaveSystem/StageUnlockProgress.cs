using System.Collections.Generic;

public static class StageUnlockProgress
{
    static readonly string[] World3InitialStages =
    {
        "Stage_3-1", "Stage_3-2", "Stage_3-3", "Stage_3-4", "Stage_3-5"
    };

    static readonly string[] CampaignStageOrder =
    {
        "Stage_1-1", "Stage_1-2", "Stage_1-3", "Stage_1-4", "Stage_1-5", "Stage_1-6", "Stage_1-7",
        "Stage_2-1", "Stage_2-2", "Stage_2-3", "Stage_2-4", "Stage_2-5", "Stage_2-6", "Stage_2-7",
        "Stage_3-1", "Stage_3-2", "Stage_3-3", "Stage_3-4", "Stage_3-5", "Stage_3-6", "Stage_3-7", "Stage_3-8"
    };

    public static void ReloadFromPrefs()
    {
        SaveSystem.Reload();
        bool progressChanged = EnsureCampaignStageOrder();
        bool rewardsChanged = TryUnlockAllClearRewards();

        if (progressChanged || rewardsChanged)
            SaveSystem.Save();
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

        if (newOrder.Count > 0 && !HasSameOrder(slot.stageOrder, newOrder))
            slot.stageOrder = newOrder;

        EnsureCampaignStageOrder();
        TryUnlockAllClearRewards();
        SaveSystem.Save();
    }

    public static bool EnsureCampaignStageOrder()
    {
        var slot = SaveSystem.ActiveSlot;
        if (slot == null)
            return false;

        bool changed = false;

        if (!HasSameOrder(slot.stageOrder, CampaignStageOrder))
        {
            slot.stageOrder = new List<string>(CampaignStageOrder);
            changed = true;
        }

        if (EnsureDefaultUnlocked(slot))
            changed = true;

        for (int i = 0; i < CampaignStageOrder.Length - 1; i++)
        {
            string completedStage = CampaignStageOrder[i];
            string nextStage = CampaignStageOrder[i + 1];

            if (!slot.clearedStages.Contains(completedStage))
                break;

            if (!slot.unlockedStages.Contains(nextStage))
            {
                slot.unlockedStages.Add(nextStage);
                changed = true;
            }
        }

        if (EnsureWorld3Unlocks(slot))
            changed = true;

        if (changed)
            SaveSystem.Save();

        return changed;
    }

    public static bool IsUnlocked(string sceneName)
    {
        var slot = SaveSystem.ActiveSlot;
        if (slot == null)
            return false;

        EnsureCampaignStageOrder();

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

        EnsureCampaignStageOrder();

        string normalized = Normalize(sceneName);
        if (string.IsNullOrEmpty(normalized))
            return false;

        return slot.clearedStages.Contains(normalized);
    }

    public static bool HasClearedAllRegisteredStages()
    {
        var slot = SaveSystem.ActiveSlot;
        if (slot == null)
            return false;

        EnsureCampaignStageOrder();

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

        EnsureCampaignStageOrder();
        return CampaignStageOrder.Length;
    }

    public static int GetClearedRegisteredStageCount()
    {
        var slot = SaveSystem.ActiveSlot;
        if (slot == null)
            return 0;

        EnsureCampaignStageOrder();
        return GetClearedCampaignStageCount(slot);
    }

    public static int GetCampaignStageCount()
    {
        return CampaignStageOrder.Length;
    }

    public static int GetClearedCampaignStageCount(Assets.Scripts.SaveSystem.StageSlot slot)
    {
        if (slot == null || slot.clearedStages == null)
            return 0;

        int count = 0;
        for (int i = 0; i < CampaignStageOrder.Length; i++)
            if (slot.clearedStages.Contains(CampaignStageOrder[i]))
                count++;

        return count;
    }

    public static Assets.Scripts.SaveSystem.NormalGameDifficulty GetClearedDifficulty(string sceneName)
    {
        var slot = SaveSystem.ActiveSlot;
        if (slot == null)
            return Assets.Scripts.SaveSystem.NormalGameDifficulty.Normal;

        string normalized = Normalize(sceneName);
        if (string.IsNullOrEmpty(normalized))
            return Assets.Scripts.SaveSystem.NormalGameDifficulty.Normal;

        if (slot.hardcoreClearedStages != null && slot.hardcoreClearedStages.Contains(normalized))
            return Assets.Scripts.SaveSystem.NormalGameDifficulty.Hardcore;

        if (slot.hardClearedStages != null && slot.hardClearedStages.Contains(normalized))
            return Assets.Scripts.SaveSystem.NormalGameDifficulty.Hard;

        if (slot.normalClearedStages != null && slot.normalClearedStages.Contains(normalized))
            return Assets.Scripts.SaveSystem.NormalGameDifficulty.Normal;

        return SaveSystem.GetActiveNormalGameDifficulty();
    }

    public static void Unlock(string sceneName)
    {
        if (ShouldIgnoreProgressPersistence())
            return;

        var slot = SaveSystem.ActiveSlot;
        if (slot == null)
            return;

        EnsureCampaignStageOrder();

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

        EnsureCampaignStageOrder();

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

        if (MarkClearedForActiveDifficulty(slot, normalized))
            changed = true;

        if (EnsureWorld3Unlocks(slot))
            changed = true;

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

        EnsureCampaignStageOrder();

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

        if (MarkClearedForActiveDifficulty(slot, normalizedCurrent))
            changed = true;

        int currentIndex = slot.stageOrder.IndexOf(normalizedCurrent);
        if (currentIndex >= 0)
        {
            int nextIndex = currentIndex + 1;
            if (nextIndex < slot.stageOrder.Count)
            {
                string nextScene = slot.stageOrder[nextIndex];
                if (nextScene != "Stage_3-6" && !string.IsNullOrEmpty(nextScene) && !slot.unlockedStages.Contains(nextScene))
                {
                    slot.unlockedStages.Add(nextScene);
                    changed = true;
                }
            }
        }

        if (EnsureWorld3Unlocks(slot))
            changed = true;

        bool rewardsChanged = TryUnlockAllClearRewards();

        if (changed || rewardsChanged)
            SaveSystem.Save();
    }

    private static bool EnsureWorld3Unlocks(Assets.Scripts.SaveSystem.StageSlot slot)
    {
        bool changed = false;
        bool allInitialStagesCleared = true;
        bool world2Cleared = slot.clearedStages.Contains("Stage_2-7");

        foreach (string sceneName in World3InitialStages)
        {
            if (world2Cleared && !slot.unlockedStages.Contains(sceneName))
            {
                slot.unlockedStages.Add(sceneName);
                changed = true;
            }

            if (!slot.clearedStages.Contains(sceneName))
                allInitialStagesCleared = false;
        }

        if (allInitialStagesCleared)
        {
            if (!slot.unlockedStages.Contains("Stage_3-6"))
            {
                slot.unlockedStages.Add("Stage_3-6");
                changed = true;
            }
        }
        else if (slot.unlockedStages.Remove("Stage_3-6"))
        {
            changed = true;
        }

        return changed;
    }

    private static bool EnsureDefaultUnlocked(Assets.Scripts.SaveSystem.StageSlot slot)
    {
        if (slot == null)
            return false;

        if (slot.unlockedStages.Count > 0)
            return false;

        string firstStage = null;

        if (slot.stageOrder.Count > 0)
            firstStage = slot.stageOrder[0];

        if (string.IsNullOrEmpty(firstStage))
            firstStage = "Stage_1-1";

        slot.unlockedStages.Add(firstStage);
        return true;
    }

    private static bool HasSameOrder(IList<string> left, IList<string> right)
    {
        if (left == null || right == null || left.Count != right.Count)
            return false;

        for (int i = 0; i < left.Count; i++)
            if (!string.Equals(left[i], right[i], System.StringComparison.Ordinal))
                return false;

        return true;
    }

    private static bool TryUnlockAllClearRewards()
    {
        bool changed = false;

        if (HasClearedAllRegisteredStages())
        {
            Assets.Scripts.SaveSystem.NormalGameDifficulty difficulty = SaveSystem.GetActiveNormalGameDifficulty();
            BomberSkin skinReward = difficulty switch
            {
                Assets.Scripts.SaveSystem.NormalGameDifficulty.Hard => BomberSkin.Palette6,
                Assets.Scripts.SaveSystem.NormalGameDifficulty.Hardcore => BomberSkin.Palette19,
                _ => BomberSkin.Palette13
            };

            if (UnlockProgress.Unlock(skinReward))
                changed = true;

            if (difficulty == Assets.Scripts.SaveSystem.NormalGameDifficulty.Hard &&
                UnlockProgress.UnlockHardcore())
            {
                changed = true;
            }

            if (UnlockProgress.UnlockBossRush())
                changed = true;
        }

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

    private static bool MarkClearedForActiveDifficulty(Assets.Scripts.SaveSystem.StageSlot slot, string sceneName)
    {
        if (slot == null || string.IsNullOrEmpty(sceneName))
            return false;

        List<string> target = SaveSystem.GetActiveNormalGameDifficulty() switch
        {
            Assets.Scripts.SaveSystem.NormalGameDifficulty.Hard => slot.hardClearedStages,
            Assets.Scripts.SaveSystem.NormalGameDifficulty.Hardcore => slot.hardcoreClearedStages,
            _ => slot.normalClearedStages
        };

        if (target == null || target.Contains(sceneName))
            return false;

        target.Add(sceneName);
        return true;
    }

    private static bool ShouldIgnoreProgressPersistence()
    {
        return BossRushSession.IsActive;
    }
}

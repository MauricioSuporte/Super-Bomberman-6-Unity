using System;
using UnityEngine;

public static class BossRushSession
{
    static readonly string[] stageOrder =
    {
        "Stage_1-6",
        "Stage_1-7",
        "Stage_2-6",
        "Stage_2-7"
    };

    static bool active;
    static bool timerPaused;
    static int currentStageIndex;
    static BossRushDifficulty selectedDifficulty;
    static BossRushLoadoutPreset selectedPreset;

    static float elapsedSeconds;

    static bool hasLastCompletedRun;
    static BossRushDifficulty lastCompletedDifficulty;
    static float lastCompletedTime;
    static int lastCompletedRank = -1;

    public static bool IsActive => active;
    public static bool HasLastCompletedRun => hasLastCompletedRun;
    public static BossRushDifficulty LastCompletedDifficulty => lastCompletedDifficulty;
    public static float LastCompletedTime => lastCompletedTime;
    public static int LastCompletedRank => lastCompletedRank;

    public static void StartRun(BossRushDifficulty difficulty, BossRushLoadoutPreset preset)
    {
        PlayerPersistentStats.EnsureSessionBooted();

        active = true;
        timerPaused = false;
        selectedDifficulty = difficulty;
        selectedPreset = preset;
        currentStageIndex = 0;
        elapsedSeconds = 0f;

        hasLastCompletedRun = false;
        lastCompletedRank = -1;
        lastCompletedTime = 0f;

        ApplySelectedLoadoutToAllPlayers();
    }

    public static void CancelRun()
    {
        ClearRuntimeState();
    }

    public static void PauseTimer()
    {
        if (!active)
            return;

        timerPaused = true;
    }

    public static int CompleteRunAndStoreTime()
    {
        if (!active)
        {
            return -1;
        }

        timerPaused = true;

        BossRushDifficulty completedDifficulty = selectedDifficulty;
        float completedTime = elapsedSeconds;
        int rank = BossRushTimesProgress.RegisterTime(completedDifficulty, completedTime);

        hasLastCompletedRun = true;
        lastCompletedDifficulty = completedDifficulty;
        lastCompletedTime = completedTime;
        lastCompletedRank = rank;

        ClearRuntimeState(keepLastCompletedRun: true);
        return rank;
    }

    public static void ClearLastCompletedRun()
    {
        hasLastCompletedRun = false;
        lastCompletedTime = 0f;
        lastCompletedRank = -1;
    }

    public static void AddElapsed(float deltaSeconds)
    {
        if (!active || timerPaused)
            return;

        if (deltaSeconds <= 0f)
            return;

        elapsedSeconds += deltaSeconds;
    }

    public static string GetFormattedElapsed()
    {
        if (elapsedSeconds < 0f)
            elapsedSeconds = 0f;

        int totalCentiseconds = Mathf.FloorToInt(elapsedSeconds * 100f);
        int minutes = totalCentiseconds / 6000;
        int seconds = (totalCentiseconds / 100) % 60;
        int centiseconds = totalCentiseconds % 100;

        return $"{minutes:00}:{seconds:00}.{centiseconds:00}";
    }

    public static string GetCurrentStageSceneName()
    {
        if (stageOrder.Length == 0)
            return string.Empty;

        currentStageIndex = Mathf.Clamp(currentStageIndex, 0, stageOrder.Length - 1);
        return stageOrder[currentStageIndex];
    }

    public static bool TryAdvanceToNextStage(out string nextSceneName)
    {
        nextSceneName = null;

        if (!active)
        {
            return false;
        }

        int nextIndex = currentStageIndex + 1;
        if (nextIndex >= stageOrder.Length)
        {
            return false;
        }

        currentStageIndex = nextIndex;
        nextSceneName = stageOrder[currentStageIndex];
        return true;
    }

    public static bool IsBossRushScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
            return false;

        for (int i = 0; i < stageOrder.Length; i++)
        {
            if (string.Equals(stageOrder[i], sceneName, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    public static void NotifySceneLoaded(string sceneName)
    {
        if (!active || string.IsNullOrWhiteSpace(sceneName))
            return;

        for (int i = 0; i < stageOrder.Length; i++)
        {
            if (string.Equals(stageOrder[i], sceneName, StringComparison.Ordinal))
            {
                currentStageIndex = i;
                timerPaused = false;
                return;
            }
        }
    }

    static void ApplySelectedLoadoutToAllPlayers()
    {
        for (int playerId = 1; playerId <= 4; playerId++)
        {
            var state = PlayerPersistentStats.Get(playerId);
            if (state == null)
                continue;

            BomberSkin preservedSkin = state.Skin;

            state.Life = 1;
            state.BombAmount = 1;
            state.ExplosionRadius = 1;
            state.SpeedInternal = PlayerPersistentStats.BaseSpeedNormal;

            state.CanKickBombs = false;
            state.CanPunchBombs = false;
            state.HasPowerGlove = false;
            state.CanPassBombs = false;
            state.CanPassDestructibles = false;
            state.HasPierceBombs = false;
            state.HasControlBombs = false;
            state.HasPowerBomb = false;
            state.HasRubberBombs = false;
            state.HasFullFire = false;
            state.MountedLouie = MountedType.None;
            state.QueuedEggs.Clear();

            selectedPreset?.ApplyTo(state);

            state.Skin = preservedSkin;
        }
    }

    static void ClearRuntimeState(bool keepLastCompletedRun = false)
    {
        active = false;
        timerPaused = false;
        currentStageIndex = 0;
        selectedPreset = null;
        elapsedSeconds = 0f;

        if (!keepLastCompletedRun)
        {
            hasLastCompletedRun = false;
            lastCompletedTime = 0f;
            lastCompletedRank = -1;
        }
    }
}
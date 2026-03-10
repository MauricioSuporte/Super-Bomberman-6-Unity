using System;
using System.Collections.Generic;
using UnityEngine;

public static class BossRushSession
{
    const string LOG = "[BossRushSession]";
    static bool enableSurgicalLogs = true;

    static readonly string[] stageOrder =
    {
        "Stage_1-6",
        "Stage_1-7",
        "Stage_2-6",
        "Stage_2-7"
    };

    static bool active;
    static int currentStageIndex;
    static BossRushDifficulty selectedDifficulty;
    static BossRushLoadoutPreset selectedPreset;

    static float elapsedSeconds;

    public static bool IsActive => active;
    public static BossRushDifficulty SelectedDifficulty => selectedDifficulty;
    public static float ElapsedSeconds => elapsedSeconds;

    public static IReadOnlyList<string> StageOrder => stageOrder;

    public static void StartRun(BossRushDifficulty difficulty, BossRushLoadoutPreset preset)
    {
        PlayerPersistentStats.EnsureSessionBooted();

        active = true;
        selectedDifficulty = difficulty;
        selectedPreset = preset;
        currentStageIndex = 0;
        elapsedSeconds = 0f;

        ApplySelectedLoadoutToAllPlayers();

        SLog(
            $"StartRun | difficulty={difficulty} presetNull={preset == null} " +
            $"currentStageIndex={currentStageIndex} currentStage={GetCurrentStageSceneName()}"
        );
    }

    public static void CancelRun()
    {
        SLog(
            $"CancelRun | prevActive={active} prevStageIndex={currentStageIndex} " +
            $"prevElapsed={GetFormattedElapsed()}"
        );

        active = false;
        currentStageIndex = 0;
        selectedPreset = null;
        elapsedSeconds = 0f;
    }

    public static void FinishRun()
    {
        SLog(
            $"FinishRun | prevActive={active} prevStageIndex={currentStageIndex} " +
            $"prevElapsed={GetFormattedElapsed()}"
        );

        active = false;
        currentStageIndex = 0;
        selectedPreset = null;
        elapsedSeconds = 0f;
    }

    public static void AddElapsed(float deltaSeconds)
    {
        if (!active)
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
            SLog("TryAdvanceToNextStage | inactive");
            return false;
        }

        int nextIndex = currentStageIndex + 1;
        if (nextIndex >= stageOrder.Length)
        {
            SLog($"TryAdvanceToNextStage | no next stage | currentStageIndex={currentStageIndex}");
            return false;
        }

        currentStageIndex = nextIndex;
        nextSceneName = stageOrder[currentStageIndex];

        SLog($"TryAdvanceToNextStage | nextStageIndex={currentStageIndex} nextScene={nextSceneName}");
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
        SLog(
            $"NotifySceneLoaded | scene={sceneName} active={active} " +
            $"elapsed={GetFormattedElapsed()}"
        );

        if (!active || string.IsNullOrWhiteSpace(sceneName))
            return;

        for (int i = 0; i < stageOrder.Length; i++)
        {
            if (string.Equals(stageOrder[i], sceneName, StringComparison.Ordinal))
            {
                currentStageIndex = i;
                SLog($"NotifySceneLoaded | matched stage index={i}");
                return;
            }
        }

        SLog("NotifySceneLoaded | scene is not part of boss rush order");
    }

    public static void ReapplyLoadoutToAllPlayers()
    {
        if (!active)
            return;

        ApplySelectedLoadoutToAllPlayers();
        SLog("ReapplyLoadoutToAllPlayers | reapplied");
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

        SLog("ApplySelectedLoadoutToAllPlayers | applied for players 1..4");
    }

    static void SLog(string message)
    {
        if (!enableSurgicalLogs)
            return;

        Debug.Log($"{LOG} {message}");
    }
}
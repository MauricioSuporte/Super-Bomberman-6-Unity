
using System;
using System.Collections.Generic;
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
    static int currentStageIndex;
    static BossRushDifficulty selectedDifficulty;
    static BossRushLoadoutPreset selectedPreset;

    public static bool IsActive => active;
    public static BossRushDifficulty SelectedDifficulty => selectedDifficulty;

    public static IReadOnlyList<string> StageOrder => stageOrder;

    public static void StartRun(BossRushDifficulty difficulty, BossRushLoadoutPreset preset)
    {
        PlayerPersistentStats.EnsureSessionBooted();

        active = true;
        selectedDifficulty = difficulty;
        selectedPreset = preset;
        currentStageIndex = 0;

        ApplySelectedLoadoutToAllPlayers();
    }

    public static void CancelRun()
    {
        active = false;
        currentStageIndex = 0;
        selectedPreset = null;
    }

    public static void FinishRun()
    {
        active = false;
        currentStageIndex = 0;
        selectedPreset = null;
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
            return false;

        int nextIndex = currentStageIndex + 1;
        if (nextIndex >= stageOrder.Length)
            return false;

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
                return;
            }
        }
    }

    public static void ReapplyLoadoutToAllPlayers()
    {
        if (!active)
            return;

        ApplySelectedLoadoutToAllPlayers();
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
}
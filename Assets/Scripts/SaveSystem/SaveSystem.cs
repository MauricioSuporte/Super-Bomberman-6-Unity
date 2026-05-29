using Assets.Scripts.SaveSystem;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using static SaveData;

public static class SaveSystem
{
    private const string SaveFolderName = "SaveData";
    private const string SaveFileName = "save.dat";
    private const int MaxBossRushTopTimes = 3;
    private const int BattleModeStageCount = 15;

    private static bool loaded;
    private static SaveData data;

    private static readonly BomberSkin[] defaultUnlockedSkins =
    {
        BomberSkin.White,
        BomberSkin.Black,
        BomberSkin.Blue,
        BomberSkin.Red,
        BomberSkin.Yellow,
        BomberSkin.Green,
        BomberSkin.Aqua,
        BomberSkin.Pink,
    };

    private static readonly PlayerAction[] controlActions =
    {
        PlayerAction.MoveUp,
        PlayerAction.MoveDown,
        PlayerAction.MoveLeft,
        PlayerAction.MoveRight,
        PlayerAction.Start,
        PlayerAction.ActionA,
        PlayerAction.ActionB,
        PlayerAction.ActionC,
        PlayerAction.ActionL,
        PlayerAction.ActionR
    };

    public static SaveData Data
    {
        get
        {
            EnsureLoaded();
            return data;
        }
    }

    public static StageSlot ActiveSlot
    {
        get
        {
            EnsureLoaded();

            if (data.activeSlotIndex < 0 || data.activeSlotIndex >= data.slots.Count)
                return null;

            return data.slots[data.activeSlotIndex];
        }
    }

    public static string SaveDirectoryPath => Path.Combine(Application.persistentDataPath, SaveFolderName);
    public static string SaveFilePath => Path.Combine(SaveDirectoryPath, SaveFileName);

    public static void SetActiveSlot(int slotIndex)
    {
        EnsureLoaded();
        data.activeSlotIndex = Mathf.Clamp(slotIndex, 0, data.slots.Count - 1);
        Save();
    }

    public static StageSlot GetSlot(int slotIndex)
    {
        EnsureLoaded();

        if (slotIndex < 0 || slotIndex >= data.slots.Count)
            return null;

        return data.slots[slotIndex];
    }

    public static bool SlotExists(int slotIndex)
    {
        StageSlot slot = GetSlot(slotIndex);
        if (slot == null)
            return false;

        return slot.started;
    }

    public static NormalGameDifficulty GetActiveNormalGameDifficulty()
    {
        StageSlot slot = ActiveSlot;
        return slot != null && slot.started
            ? NormalizeNormalGameDifficulty(slot.difficulty)
            : NormalGameDifficulty.Normal;
    }

    public static void ResetSlot(int slotIndex, NormalGameDifficulty difficulty = NormalGameDifficulty.Normal)
    {
        StageSlot slot = GetSlot(slotIndex);
        if (slot == null)
            return;

        slot.started = true;
        slot.difficulty = (int)NormalizeNormalGameDifficulty((int)difficulty);
        slot.unlockedStages.Clear();
        slot.clearedStages.Clear();
        slot.normalClearedStages.Clear();
        slot.hardClearedStages.Clear();
        slot.hardcoreClearedStages.Clear();
        slot.perfectStages.Clear();
        slot.stageOrder.Clear();

        slot.unlockedStages.Add("Stage_1-1");

        Save();
    }

    public static void DeleteSlot(int slotIndex)
    {
        StageSlot slot = GetSlot(slotIndex);
        if (slot == null)
            return;

        slot.started = false;
        slot.difficulty = (int)NormalGameDifficulty.Normal;
        slot.unlockedStages.Clear();
        slot.clearedStages.Clear();
        slot.normalClearedStages.Clear();
        slot.hardClearedStages.Clear();
        slot.hardcoreClearedStages.Clear();
        slot.perfectStages.Clear();
        slot.stageOrder.Clear();

        Save();
    }

    public static void Reload()
    {
        loaded = false;
        EnsureLoaded();
    }

    public static void Save()
    {
        EnsureLoaded();

        try
        {
            EnsureDirectoryExists();
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(SaveFilePath, json);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SaveSystem] Save failed: {ex.Message}");
        }
    }

    public static void SaveControlsFromInputManager()
    {
        EnsureLoaded();

        var input = PlayerInputManager.Instance;
        if (input == null)
            return;

        EnsureControlProfiles(data);

        for (int i = 0; i < 6; i++)
        {
            int playerId = i + 1;
            var runtimeProfile = input.GetPlayer(playerId);
            if (runtimeProfile == null)
                continue;

            var savedProfile = data.controls[i];
            if (savedProfile == null)
            {
                savedProfile = new SavedPlayerControls();
                data.controls[i] = savedProfile;
            }

            savedProfile.playerId = playerId;
            savedProfile.active = GameSession.Instance != null
                ? GameSession.Instance.IsPlayerActive(playerId)
                : savedProfile.active;
            savedProfile.joyIndex = Mathf.Clamp(runtimeProfile.joyIndex, 1, 11);
            savedProfile.gamepadDeviceId = runtimeProfile.gamepadDeviceId;
            savedProfile.gamepadProduct = runtimeProfile.gamepadProduct ?? "";

            if (savedProfile.bindings == null)
                savedProfile.bindings = new List<SavedBinding>();
            else
                savedProfile.bindings.Clear();

            for (int a = 0; a < controlActions.Length; a++)
            {
                var action = controlActions[a];
                var binding = runtimeProfile.GetBinding(action);

                savedProfile.bindings.Add(new SavedBinding
                {
                    action = (int)action,
                    kind = (int)binding.kind,
                    key = (int)binding.key,
                    dpadDir = binding.dpadDir,
                    joyIndex = binding.joyIndex,
                    joyButton = binding.joyButton
                });
            }
        }

        Save();
    }

    public static void LoadControlsIntoInputManager()
    {
        EnsureLoaded();

        var input = PlayerInputManager.Instance;
        if (input == null)
            return;

        EnsureControlProfiles(data);

        for (int i = 0; i < 6; i++)
        {
            int playerId = i + 1;
            var runtimeProfile = input.GetPlayer(playerId);
            var savedProfile = data.controls[i];

            if (runtimeProfile == null || savedProfile == null)
                continue;

            runtimeProfile.ResetToDefault();

            runtimeProfile.joyIndex = Mathf.Clamp(savedProfile.joyIndex, 1, 11);
            runtimeProfile.gamepadDeviceId = savedProfile.gamepadDeviceId;
            runtimeProfile.gamepadProduct = savedProfile.gamepadProduct ?? "";

            if (savedProfile.bindings == null)
                continue;

            for (int b = 0; b < savedProfile.bindings.Count; b++)
            {
                var savedBinding = savedProfile.bindings[b];

                if (!Enum.IsDefined(typeof(PlayerAction), savedBinding.action))
                    continue;

                if (!Enum.IsDefined(typeof(BindKind), savedBinding.kind))
                    continue;

                var action = (PlayerAction)savedBinding.action;
                var kind = (BindKind)savedBinding.kind;

                Binding binding = kind switch
                {
                    BindKind.Key => Binding.FromKey((KeyCode)savedBinding.key),
                    BindKind.DPad => Binding.FromDpad(savedBinding.joyIndex, savedBinding.dpadDir),
                    BindKind.JoyButton => Binding.FromJoyButton(savedBinding.joyIndex, savedBinding.joyButton),
                    _ => runtimeProfile.GetBinding(action)
                };

                runtimeProfile.SetBinding(action, binding);
            }
        }

        LoadControlActivePlayersIntoGameSession();
    }

    public static List<float> GetBossRushTopTimes(BossRushDifficulty difficulty)
    {
        EnsureLoaded();

        BossRushDifficultyTimesSave entry = GetOrCreateBossRushTimesEntry(difficulty);
        return new List<float>(entry.topTimes);
    }

    public static void SetBossRushTopTimes(BossRushDifficulty difficulty, List<float> times)
    {
        EnsureLoaded();

        BossRushDifficultyTimesSave entry = GetOrCreateBossRushTimesEntry(difficulty);

        if (entry.topTimes == null)
            entry.topTimes = new List<float>();
        else
            entry.topTimes.Clear();

        if (times != null)
        {
            for (int i = 0; i < times.Count; i++)
            {
                float value = times[i];

                if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
                    continue;

                entry.topTimes.Add(value);
            }
        }

        entry.topTimes.Sort((a, b) => a.CompareTo(b));

        if (entry.topTimes.Count > MaxBossRushTopTimes)
            entry.topTimes.RemoveRange(MaxBossRushTopTimes, entry.topTimes.Count - MaxBossRushTopTimes);

        Save();
    }

    public static bool HasBossRushRecordedTime(BossRushDifficulty difficulty)
    {
        EnsureLoaded();

        BossRushDifficultyTimesSave entry = GetOrCreateBossRushTimesEntry(difficulty);
        return entry.topTimes != null && entry.topTimes.Count > 0;
    }

    public static BattleModeRules.MatchMode GetBattleModeMatchMode()
    {
        EnsureLoaded();
        return NormalizeBattleModeMatchMode(data.battleModeMatchMode);
    }

    public static void SetBattleModeMatchMode(BattleModeRules.MatchMode matchMode)
    {
        EnsureLoaded();

        BattleModeRules.MatchMode normalized = NormalizeBattleModeMatchMode((int)matchMode);
        if (data.battleModeMatchMode == (int)normalized)
            return;

        data.battleModeMatchMode = (int)normalized;
        Save();
    }

    public static BattleModePlayerControlMode[] GetBattleModePlayerControlModes()
    {
        EnsureLoaded();
        EnsureBattleModePlayerControlModes(data);

        BattleModePlayerControlMode[] result = new BattleModePlayerControlMode[6];
        for (int i = 0; i < result.Length; i++)
            result[i] = NormalizeBattleModePlayerControlMode(data.battleModePlayerControlModes[i]);

        return result;
    }

    public static void SetBattleModePlayerControlModes(IReadOnlyList<BattleModePlayerControlMode> modes)
    {
        EnsureLoaded();
        EnsureBattleModePlayerControlModes(data);

        bool changed = false;
        for (int i = 0; i < data.battleModePlayerControlModes.Length; i++)
        {
            BattleModePlayerControlMode mode = modes != null && i < modes.Count
                ? modes[i]
                : GetDefaultBattleModePlayerControlMode(i);

            mode = NormalizeBattleModePlayerControlMode((int)mode);
            int value = (int)mode;

            if (data.battleModePlayerControlModes[i] == value)
                continue;

            data.battleModePlayerControlModes[i] = value;
            changed = true;
        }

        if (changed)
            Save();
    }

    public static void SetBattleModePlayerControlMode(int playerId, BattleModePlayerControlMode mode)
    {
        EnsureLoaded();
        EnsureBattleModePlayerControlModes(data);

        playerId = Mathf.Clamp(playerId, 1, 6);
        int index = playerId - 1;

        mode = NormalizeBattleModePlayerControlMode((int)mode);

        if (data.battleModePlayerControlModes[index] == (int)mode)
            return;

        data.battleModePlayerControlModes[index] = (int)mode;
        Save();
    }

    public static BattleModePlayerControlMode GetBattleModePlayerControlMode(int playerId)
    {
        EnsureLoaded();
        EnsureBattleModePlayerControlModes(data);

        playerId = Mathf.Clamp(playerId, 1, 6);
        return NormalizeBattleModePlayerControlMode(data.battleModePlayerControlModes[playerId - 1]);
    }

    public static BattleModeRules.TeamId[] GetBattleModePlayerTeams()
    {
        EnsureLoaded();
        EnsureBattleModePlayerTeams(data);

        BattleModeRules.TeamId[] result = new BattleModeRules.TeamId[6];
        for (int i = 0; i < result.Length; i++)
            result[i] = NormalizeBattleModeTeamId(data.battleModePlayerTeams[i]);

        return result;
    }

    public static void SetBattleModePlayerTeams(IReadOnlyList<BattleModeRules.TeamId> teams)
    {
        EnsureLoaded();
        EnsureBattleModePlayerTeams(data);

        bool changed = false;
        for (int i = 0; i < data.battleModePlayerTeams.Length; i++)
        {
            BattleModeRules.TeamId team = teams != null && i < teams.Count
                ? teams[i]
                : BattleModeRules.GetDefaultTeamForPlayer(i + 1);

            team = NormalizeBattleModeTeamId((int)team);
            int value = (int)team;

            if (data.battleModePlayerTeams[i] == value)
                continue;

            data.battleModePlayerTeams[i] = value;
            changed = true;
        }

        if (changed)
            Save();
    }

    public static BattleModeComputerLevel GetBattleModeComputerLevel()
    {
        EnsureLoaded();
        return NormalizeBattleModeComputerLevel(data.battleModeComputerLevel);
    }

    public static void SetBattleModeComputerLevel(BattleModeComputerLevel level)
    {
        EnsureLoaded();

        BattleModeComputerLevel normalized = NormalizeBattleModeComputerLevel((int)level);
        if (data.battleModeComputerLevel == (int)normalized)
            return;

        data.battleModeComputerLevel = (int)normalized;
        Save();
    }

    public static int GetBattleModeBattlesToWin()
    {
        EnsureLoaded();
        return Mathf.Clamp(data.battleModeBattlesToWin, 1, 5);
    }

    public static void SetBattleModeBattlesToWin(int battlesToWin)
    {
        EnsureLoaded();

        int normalized = Mathf.Clamp(battlesToWin, 1, 5);
        if (data.battleModeBattlesToWin == normalized)
            return;

        data.battleModeBattlesToWin = normalized;
        Save();
    }

    public static BattleModeRules.RoundTimerMode GetBattleModeRoundTimerMode()
    {
        EnsureLoaded();
        return NormalizeBattleModeRoundTimerMode(data.battleModeRoundTimerMode);
    }

    public static void SetBattleModeRoundTimerMode(BattleModeRules.RoundTimerMode timerMode)
    {
        EnsureLoaded();

        BattleModeRules.RoundTimerMode normalized = NormalizeBattleModeRoundTimerMode((int)timerMode);
        if (data.battleModeRoundTimerMode == (int)normalized)
            return;

        data.battleModeRoundTimerMode = (int)normalized;
        Save();
    }

    public static BattleModeSuddenDeathSetting GetBattleModeSuddenDeathSetting()
    {
        EnsureLoaded();
        return NormalizeBattleModeSuddenDeathSetting(data.battleModeSuddenDeath);
    }

    public static void SetBattleModeSuddenDeathSetting(BattleModeSuddenDeathSetting setting)
    {
        EnsureLoaded();

        BattleModeSuddenDeathSetting normalized = NormalizeBattleModeSuddenDeathSetting((int)setting);
        if (data.battleModeSuddenDeath == (int)normalized)
            return;

        data.battleModeSuddenDeath = (int)normalized;
        Save();
    }

    public static bool GetBattleModeRevengeBomberEnabled()
    {
        EnsureLoaded();
        return data.battleModeRevengeBomber;
    }

    public static void SetBattleModeRevengeBomberEnabled(bool enabled)
    {
        EnsureLoaded();

        if (data.battleModeRevengeBomber == enabled)
            return;

        data.battleModeRevengeBomber = enabled;
        Save();
    }

    public static int GetBattleModeStageIndex()
    {
        EnsureLoaded();
        int stageIndex = Mathf.Clamp(data.battleModeStageIndex, 1, 15);
        if (!IsBattleModeStageUnlocked(stageIndex))
            stageIndex = 1;

        return stageIndex;
    }

    public static void SetBattleModeStageIndex(int stageIndex)
    {
        EnsureLoaded();

        int normalized = Mathf.Clamp(stageIndex, 1, 15);
        if (!IsBattleModeStageUnlocked(normalized))
            normalized = 1;

        if (data.battleModeStageIndex == normalized)
            return;

        data.battleModeStageIndex = normalized;
        Save();
    }

    public static bool IsBattleModeStageUnlocked(int stageIndex)
    {
        EnsureLoaded();

        int normalized = Mathf.Clamp(stageIndex, 1, BattleModeStageCount);
        return normalized switch
        {
            11 => data.battleModeStage11Unlocked,
            12 => data.battleModeStage12Unlocked,
            13 => data.battleModeStage13Unlocked,
            14 => data.battleModeStage14Unlocked,
            15 => data.battleModeStage15Unlocked,
            _ => true
        };
    }

    public static bool HasBattleModeManStageWin(int stageIndex)
    {
        EnsureLoaded();
        EnsureBattleModeManStageWins(data);

        int normalized = Mathf.Clamp(stageIndex, 1, BattleModeStageCount);
        return data.battleModeManStageWins[normalized - 1];
    }

    public static int GetBattleModeManStageWinCount(int maxStageIndex = BattleModeStageCount)
    {
        EnsureLoaded();
        EnsureBattleModeManStageWins(data);

        int max = Mathf.Clamp(maxStageIndex, 1, BattleModeStageCount);
        int count = 0;
        for (int i = 0; i < max; i++)
        {
            if (data.battleModeManStageWins[i])
                count++;
        }

        return count;
    }

    public static bool RecordBattleModeManStageWin(int stageIndex)
    {
        EnsureLoaded();
        EnsureBattleModeManStageWins(data);

        int normalized = Mathf.Clamp(stageIndex, 1, BattleModeStageCount);
        int index = normalized - 1;
        if (data.battleModeManStageWins[index])
            return false;

        data.battleModeManStageWins[index] = true;
        Save();
        return true;
    }

    public static int GetBattleModeMusicSelectionMask()
    {
        EnsureLoaded();
        return NormalizeBattleModeMusicSelectionMask(data.battleModeMusicSelectionMask);
    }

    public static void SetBattleModeMusicSelectionMask(int selectionMask)
    {
        EnsureLoaded();

        int normalized = NormalizeBattleModeMusicSelectionMask(selectionMask);
        if (data.battleModeMusicSelectionMask == normalized)
            return;

        data.battleModeMusicSelectionMask = normalized;
        Save();
    }

    public static BattleModeHandicapSave GetBattleModeHandicapForStage(int stageIndex)
    {
        EnsureLoaded();
        EnsureBattleModeHandicapProfiles(data);

        BattleModeHandicapSave source = GetBattleModeHandicapProfile(data, stageIndex);
        return CloneBattleModeHandicap(source, GetBattleModeHandicapProfileKind(stageIndex));
    }

    public static void SetBattleModeHandicapForStage(int stageIndex, BattleModeHandicapSave handicap)
    {
        EnsureLoaded();
        EnsureBattleModeHandicapProfiles(data);

        BattleModeHandicapProfileKind kind = GetBattleModeHandicapProfileKind(stageIndex);
        BattleModeHandicapSave normalized = CloneBattleModeHandicap(handicap, kind);

        switch (kind)
        {
            case BattleModeHandicapProfileKind.PowerZone:
                data.battleModeHandicapPowerZone = normalized;
                break;
            case BattleModeHandicapProfileKind.Stage6:
                data.battleModeHandicapStage6 = normalized;
                data.battleModeHandicapStage6Initialized = true;
                break;
            default:
                data.battleModeHandicapGeneric = normalized;
                break;
        }

        Save();
    }

    private static void NormalizeBattleModeRandomEggRange(int[] amounts)
    {
        if (amounts == null)
            return;

        int minIndex = GetBattleModeHiddenDropEntryIndex(GameManager.BattleModeHiddenDropEntryKind.RandomEggsMin);
        int maxIndex = GetBattleModeHiddenDropEntryIndex(GameManager.BattleModeHiddenDropEntryKind.RandomEggsMax);

        if (minIndex < 0 || maxIndex < 0)
            return;

        if (minIndex >= amounts.Length || maxIndex >= amounts.Length)
            return;

        amounts[minIndex] = Mathf.Clamp(amounts[minIndex], 0, 99);
        amounts[maxIndex] = Mathf.Clamp(amounts[maxIndex], 0, 99);

        if (amounts[maxIndex] < amounts[minIndex])
            amounts[maxIndex] = amounts[minIndex];
    }

    private static int GetBattleModeHiddenDropEntryIndex(GameManager.BattleModeHiddenDropEntryKind kind)
    {
        for (int i = 0; i < GameManager.BattleModeHiddenDropEntries.Length; i++)
        {
            if (GameManager.BattleModeHiddenDropEntries[i].Kind == kind)
                return i;
        }

        return -1;
    }

    public static float GetBossRushUnlockTargetTime(BossRushDifficulty difficulty)
    {
        EnsureLoaded();

        BossRushDifficultyTimesSave entry = GetOrCreateBossRushTimesEntry(difficulty);
        return entry.targetUnlockTime;
    }

    public static bool IsNightmareUnlocked()
    {
        EnsureLoaded();
        return data.nightmareUnlocked;
    }

    public static bool UnlockNightmare()
    {
        EnsureLoaded();

        if (data.nightmareUnlocked)
            return false;

        data.nightmareUnlocked = true;
        Save();
        return true;
    }

    public static bool IsHardcoreUnlocked()
    {
        EnsureLoaded();
        return data.hardcoreUnlocked;
    }

    public static bool UnlockHardcore()
    {
        EnsureLoaded();

        if (data.hardcoreUnlocked)
            return false;

        data.hardcoreUnlocked = true;
        Save();
        return true;
    }

    public static SavedVideoSettings GetVideoSettings()
    {
        EnsureLoaded();

        data.videoSettings ??= new SavedVideoSettings();

        return data.videoSettings;
    }

    public static void SetVideoSettings(bool fullscreen, int windowSizeMultiplier)
    {
        EnsureLoaded();

        data.videoSettings ??= new SavedVideoSettings();

        data.videoSettings.fullscreen = fullscreen;
        data.videoSettings.windowSizeMultiplier = Mathf.Max(1, windowSizeMultiplier);

        Save();
    }

    private static BossRushDifficultyTimesSave GetOrCreateBossRushTimesEntry(BossRushDifficulty difficulty)
    {
        EnsureLoaded();

        if (data.bossRushTimes == null)
            data.bossRushTimes = new List<BossRushDifficultyTimesSave>();

        for (int i = 0; i < data.bossRushTimes.Count; i++)
        {
            BossRushDifficultyTimesSave current = data.bossRushTimes[i];
            if (current == null)
                continue;

            if (current.difficulty == (int)difficulty)
            {
                if (current.topTimes == null)
                    current.topTimes = new List<float>();

                NormalizeBossRushTimesList(current.topTimes);
                current.targetUnlockTime = NormalizeBossRushUnlockTargetTime(current.targetUnlockTime, difficulty);
                return current;
            }
        }

        BossRushDifficultyTimesSave created = new BossRushDifficultyTimesSave
        {
            difficulty = (int)difficulty,
            topTimes = new List<float>(),
            targetUnlockTime = GetDefaultBossRushUnlockTargetTime(difficulty)
        };

        data.bossRushTimes.Add(created);
        return created;
    }

    private static void EnsureLoaded()
    {
        if (loaded)
            return;

        loaded = true;

        if (!File.Exists(SaveFilePath))
        {
            data = CreateDefaultData();
            Save();
            return;
        }

        try
        {
            string json = File.ReadAllText(SaveFilePath);
            data = JsonUtility.FromJson<SaveData>(json);

            if (data == null)
                data = CreateDefaultData();

            NormalizeData(data);
            Save();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SaveSystem] Load failed: {ex.Message}");
            data = CreateDefaultData();
            Save();
        }
    }

    private static SaveData CreateDefaultData()
    {
        SaveData d = new SaveData();
        NormalizeData(d);
        return d;
    }

    private static void NormalizeData(SaveData d)
    {
        if (d == null)
            return;

        if (d.unlockedSkins == null)
            d.unlockedSkins = new List<string>();

        if (d.slots == null)
            d.slots = new List<StageSlot>();

        while (d.slots.Count < 3)
            d.slots.Add(new StageSlot());

        if (d.slots.Count > 3)
            d.slots.RemoveRange(3, d.slots.Count - 3);

        for (int i = 0; i < d.slots.Count; i++)
        {
            if (d.slots[i] == null)
                d.slots[i] = new StageSlot();

            if (d.slots[i].unlockedStages == null)
                d.slots[i].unlockedStages = new List<string>();

            if (d.slots[i].clearedStages == null)
                d.slots[i].clearedStages = new List<string>();

            if (d.slots[i].normalClearedStages == null)
                d.slots[i].normalClearedStages = new List<string>();

            if (d.slots[i].hardClearedStages == null)
                d.slots[i].hardClearedStages = new List<string>();

            if (d.slots[i].hardcoreClearedStages == null)
                d.slots[i].hardcoreClearedStages = new List<string>();

            if (d.slots[i].perfectStages == null)
                d.slots[i].perfectStages = new List<string>();

            if (d.slots[i].stageOrder == null)
                d.slots[i].stageOrder = new List<string>();

            d.slots[i].difficulty = (int)NormalizeNormalGameDifficulty(d.slots[i].difficulty);

            if (d.slots[i].unlockedStages.Count == 0)
                d.slots[i].unlockedStages.Add("Stage_1-1");
        }

        EnsureControlProfiles(d);
        EnsureBossRushTimes(d);

        for (int i = 0; i < defaultUnlockedSkins.Length; i++)
        {
            string skinName = defaultUnlockedSkins[i].ToString();
            if (!d.unlockedSkins.Contains(skinName))
                d.unlockedSkins.Add(skinName);
        }

        if (d.unlockedSkins.Contains(BomberSkin.Purple.ToString()))
            d.hardcoreUnlocked = true;

        if (!Enum.IsDefined(typeof(BomberSkin), d.player1SelectedSkin))
            d.player1SelectedSkin = (int)BomberSkin.White;

        if (!Enum.IsDefined(typeof(BomberSkin), d.player2SelectedSkin))
            d.player2SelectedSkin = (int)BomberSkin.Black;

        if (!Enum.IsDefined(typeof(BomberSkin), d.player3SelectedSkin))
            d.player3SelectedSkin = (int)BomberSkin.Blue;

        if (!Enum.IsDefined(typeof(BomberSkin), d.player4SelectedSkin))
            d.player4SelectedSkin = (int)BomberSkin.Red;

        if (!Enum.IsDefined(typeof(BomberSkin), d.player5SelectedSkin))
            d.player5SelectedSkin = (int)BomberSkin.Green;

        if (!Enum.IsDefined(typeof(BomberSkin), d.player6SelectedSkin))
            d.player6SelectedSkin = (int)BomberSkin.Yellow;

        if (d.activeSlotIndex < -1 || d.activeSlotIndex >= d.slots.Count)
            d.activeSlotIndex = -1;

        d.videoSettings ??= new SavedVideoSettings();
        EnsureBattleModeItemAmounts(d, GameManager.GetDefaultBattleModeHiddenItemAmounts());
        EnsureBattleModeLouieAmounts(d, GameManager.GetDefaultBattleModeLouieAmounts());
        EnsureBattleModeHandicapProfiles(d);

        if (d.videoSettings.windowSizeMultiplier < 1)
            d.videoSettings.windowSizeMultiplier = 4;

        d.battleModeMatchMode = (int)NormalizeBattleModeMatchMode(d.battleModeMatchMode);
        d.battleModeComputerLevel = (int)NormalizeBattleModeComputerLevel(d.battleModeComputerLevel);
        d.battleModeBattlesToWin = Mathf.Clamp(d.battleModeBattlesToWin, 1, 5);
        d.battleModeRoundTimerMode = (int)NormalizeBattleModeRoundTimerMode(d.battleModeRoundTimerMode);
        d.battleModeSuddenDeath = (int)NormalizeBattleModeSuddenDeathSetting(d.battleModeSuddenDeath);
        d.battleModeStageIndex = Mathf.Clamp(d.battleModeStageIndex, 1, 15);
        d.battleModeMusicSelectionMask = NormalizeBattleModeMusicSelectionMask(d.battleModeMusicSelectionMask);
        EnsureBattleModePlayerControlModes(d);
        EnsureBattleModePlayerTeams(d);
        EnsureBattleModeManStageWins(d);
        NormalizeBattleModeStageUnlocks(d);
    }

    private static void NormalizeBattleModeStageUnlocks(SaveData d)
    {
        if (d.battleModeStage11Unlocked)
            d.battleModeManStageWins[9] = true;

        if (d.battleModeManStageWins[9])
            d.battleModeStage11Unlocked = true;

        if (d.battleModeManStageWins[6] && d.battleModeManStageWins[8])
            d.battleModeStage12Unlocked = true;

        if (CountBattleModeManStageWins(d, BattleModeStageCount) >= 1)
            d.battleModeStage13Unlocked = true;

        if (CountBattleModeManStageWins(d, BattleModeStageCount) >= 7)
            d.battleModeStage14Unlocked = true;

        if (CountBattleModeManStageWins(d, 14) >= 14)
            d.battleModeStage15Unlocked = true;
    }

    private static int CountBattleModeManStageWins(SaveData d, int maxStageIndex)
    {
        EnsureBattleModeManStageWins(d);

        int max = Mathf.Clamp(maxStageIndex, 1, BattleModeStageCount);
        int count = 0;
        for (int i = 0; i < max; i++)
        {
            if (d.battleModeManStageWins[i])
                count++;
        }

        return count;
    }

    private static void EnsureBattleModeManStageWins(SaveData d)
    {
        if (d.battleModeManStageWins == null || d.battleModeManStageWins.Length != BattleModeStageCount)
        {
            bool[] normalized = new bool[BattleModeStageCount];
            if (d.battleModeManStageWins != null)
            {
                int copy = Mathf.Min(d.battleModeManStageWins.Length, normalized.Length);
                for (int i = 0; i < copy; i++)
                    normalized[i] = d.battleModeManStageWins[i];
            }

            d.battleModeManStageWins = normalized;
        }
    }

    private static BattleModeRules.MatchMode NormalizeBattleModeMatchMode(int matchMode)
    {
        if (Enum.IsDefined(typeof(BattleModeRules.MatchMode), matchMode))
            return (BattleModeRules.MatchMode)matchMode;

        return BattleModeRules.MatchMode.SingleMatch;
    }

    private static BattleModeComputerLevel NormalizeBattleModeComputerLevel(int level)
    {
        if (Enum.IsDefined(typeof(BattleModeComputerLevel), level))
            return (BattleModeComputerLevel)level;

        return BattleModeComputerLevel.Normal;
    }

    private static BattleModeRules.RoundTimerMode NormalizeBattleModeRoundTimerMode(int timerMode)
    {
        if (timerMode >= (int)BattleModeRules.RoundTimerMode.OneMinute &&
            timerMode <= (int)BattleModeRules.RoundTimerMode.Infinite)
        {
            return (BattleModeRules.RoundTimerMode)timerMode;
        }

        return BattleModeRules.RoundTimerMode.TwoMinutes;
    }

    private static BattleModeSuddenDeathSetting NormalizeBattleModeSuddenDeathSetting(int setting)
    {
        if (Enum.IsDefined(typeof(BattleModeSuddenDeathSetting), setting))
            return (BattleModeSuddenDeathSetting)setting;

        return BattleModeSuddenDeathSetting.Random;
    }

    private static int NormalizeBattleModeMusicSelectionMask(int mask)
    {
        int validMask = 0;
        foreach (BattleModeRules.BattleMusicSelection selection in Enum.GetValues(typeof(BattleModeRules.BattleMusicSelection)))
        {
            if (selection == BattleModeRules.BattleMusicSelection.Random)
                continue;

            int bit = 1 << ((int)selection - 1);
            validMask |= bit;
        }

        int normalized = mask & validMask;
        return normalized;
    }

    private static void EnsureBattleModePlayerControlModes(SaveData d)
    {
        if (d == null)
            return;

        if (d.battleModePlayerControlModes == null || d.battleModePlayerControlModes.Length != 6)
        {
            int[] previous = d.battleModePlayerControlModes;
            d.battleModePlayerControlModes = new int[6];

            for (int i = 0; i < d.battleModePlayerControlModes.Length; i++)
            {
                d.battleModePlayerControlModes[i] = previous != null && i < previous.Length
                    ? previous[i]
                    : (int)GetDefaultBattleModePlayerControlMode(i);
            }
        }

        for (int i = 0; i < d.battleModePlayerControlModes.Length; i++)
        {
            d.battleModePlayerControlModes[i] =
                (int)NormalizeBattleModePlayerControlMode(d.battleModePlayerControlModes[i]);
        }
    }

    private static BattleModePlayerControlMode NormalizeBattleModePlayerControlMode(int mode)
    {
        if (Enum.IsDefined(typeof(BattleModePlayerControlMode), mode))
            return (BattleModePlayerControlMode)mode;

        return BattleModePlayerControlMode.Off;
    }

    private static BattleModePlayerControlMode GetDefaultBattleModePlayerControlMode(int playerIndex)
    {
        return playerIndex == 0
            ? BattleModePlayerControlMode.Man
            : BattleModePlayerControlMode.Com;
    }

    private static void EnsureBattleModePlayerTeams(SaveData d)
    {
        if (d == null)
            return;

        if (d.battleModePlayerTeams == null || d.battleModePlayerTeams.Length != 6)
        {
            int[] previous = d.battleModePlayerTeams;
            d.battleModePlayerTeams = new int[6];

            for (int i = 0; i < d.battleModePlayerTeams.Length; i++)
            {
                d.battleModePlayerTeams[i] = previous != null && i < previous.Length
                    ? previous[i]
                    : (int)BattleModeRules.GetDefaultTeamForPlayer(i + 1);
            }
        }

        for (int i = 0; i < d.battleModePlayerTeams.Length; i++)
            d.battleModePlayerTeams[i] = (int)NormalizeBattleModeTeamId(d.battleModePlayerTeams[i]);
    }

    private static BattleModeRules.TeamId NormalizeBattleModeTeamId(int team)
    {
        if (Enum.IsDefined(typeof(BattleModeRules.TeamId), team))
            return (BattleModeRules.TeamId)team;

        return BattleModeRules.TeamId.Blue;
    }

    private static void EnsureControlProfiles(SaveData d)
    {
        d.controls ??= new List<SavedPlayerControls>();

        while (d.controls.Count < 6)
        {
            int playerId = d.controls.Count + 1;
            d.controls.Add(new SavedPlayerControls
            {
                playerId = playerId,
                active = playerId == 1
            });
        }

        if (d.controls.Count > 6)
            d.controls.RemoveRange(6, d.controls.Count - 6);

        bool hasAnyActive = false;

        for (int i = 0; i < d.controls.Count; i++)
        {
            if (d.controls[i] == null)
            {
                d.controls[i] = new SavedPlayerControls
                {
                    playerId = i + 1,
                    active = i == 0
                };
            }

            d.controls[i].playerId = i + 1;

            if (d.controls[i].joyIndex < 1)
                d.controls[i].joyIndex = 1;

            if (d.controls[i].bindings == null)
                d.controls[i].bindings = new List<SavedBinding>();

            if (d.controls[i].active)
                hasAnyActive = true;
        }

        if (!hasAnyActive && d.controls.Count > 0)
            d.controls[0].active = true;
    }

    private static void EnsureBossRushTimes(SaveData d)
    {
        if (d.bossRushTimes == null)
            d.bossRushTimes = new List<BossRushDifficultyTimesSave>();

        for (int i = d.bossRushTimes.Count - 1; i >= 0; i--)
        {
            if (d.bossRushTimes[i] == null)
                d.bossRushTimes.RemoveAt(i);
        }

        foreach (BossRushDifficulty difficulty in Enum.GetValues(typeof(BossRushDifficulty)))
        {
            BossRushDifficultyTimesSave entry = null;

            for (int i = 0; i < d.bossRushTimes.Count; i++)
            {
                if (d.bossRushTimes[i].difficulty == (int)difficulty)
                {
                    entry = d.bossRushTimes[i];
                    break;
                }
            }

            if (entry == null)
            {
                entry = new BossRushDifficultyTimesSave
                {
                    difficulty = (int)difficulty,
                    topTimes = new List<float>(),
                    targetUnlockTime = GetDefaultBossRushUnlockTargetTime(difficulty)
                };

                d.bossRushTimes.Add(entry);
            }

            if (entry.topTimes == null)
                entry.topTimes = new List<float>();

            NormalizeBossRushTimesList(entry.topTimes);
            entry.targetUnlockTime = NormalizeBossRushUnlockTargetTime(entry.targetUnlockTime, difficulty);
        }
    }

    private static void NormalizeBossRushTimesList(List<float> times)
    {
        if (times == null)
            return;

        for (int i = times.Count - 1; i >= 0; i--)
        {
            float value = times[i];
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
                times.RemoveAt(i);
        }

        times.Sort((a, b) => a.CompareTo(b));

        if (times.Count > MaxBossRushTopTimes)
            times.RemoveRange(MaxBossRushTopTimes, times.Count - MaxBossRushTopTimes);
    }

    private static float NormalizeBossRushUnlockTargetTime(float currentValue, BossRushDifficulty difficulty)
    {
        if (!float.IsNaN(currentValue) && !float.IsInfinity(currentValue) && currentValue > 0f)
            return currentValue;

        return GetDefaultBossRushUnlockTargetTime(difficulty);
    }

    private static float GetDefaultBossRushUnlockTargetTime(BossRushDifficulty difficulty)
    {
        switch (difficulty)
        {
            case BossRushDifficulty.EASY:
                return 4f * 60f;

            case BossRushDifficulty.NORMAL:
                return 4f * 60f;

            case BossRushDifficulty.HARD:
                return 5f * 60f;

            default:
                return -1f;
        }
    }

    public static int[] GetBattleModeItemAmounts(IReadOnlyList<int> fallbackAmounts)
    {
        EnsureLoaded();
        EnsureBattleModeItemAmounts(data, fallbackAmounts);

        int itemCount = GameManager.BattleModeHiddenDropEntries.Length;
        int[] result = new int[itemCount];

        for (int i = 0; i < itemCount; i++)
            result[i] = Mathf.Clamp(data.battleModeItemAmounts[i], 0, 99);

        NormalizeBattleModeRandomEggRange(result);
        return result;
    }

    public static void SetBattleModeItemAmounts(IReadOnlyList<int> amounts)
    {
        EnsureLoaded();
        EnsureBattleModeItemAmounts(data, GameManager.GetDefaultBattleModeHiddenItemAmounts());

        int itemCount = GameManager.BattleModeHiddenDropEntries.Length;
        bool changed = false;

        for (int i = 0; i < itemCount; i++)
        {
            int value = amounts != null && i < amounts.Count
                ? amounts[i]
                : data.battleModeItemAmounts[i];

            value = Mathf.Clamp(value, 0, 99);

            if (data.battleModeItemAmounts[i] == value)
                continue;

            data.battleModeItemAmounts[i] = value;
            changed = true;
        }

        int[] beforeNormalize = new int[itemCount];
        Array.Copy(data.battleModeItemAmounts, beforeNormalize, itemCount);

        NormalizeBattleModeRandomEggRange(data.battleModeItemAmounts);

        for (int i = 0; i < itemCount; i++)
        {
            if (beforeNormalize[i] != data.battleModeItemAmounts[i])
            {
                changed = true;
                break;
            }
        }

        if (changed)
            Save();
    }

    private static NormalGameDifficulty NormalizeNormalGameDifficulty(int difficulty)
    {
        if (Enum.IsDefined(typeof(NormalGameDifficulty), difficulty))
            return (NormalGameDifficulty)difficulty;

        return NormalGameDifficulty.Normal;
    }

    public static int[] GetBattleModeLouieAmounts(IReadOnlyList<int> fallbackAmounts)
    {
        EnsureLoaded();
        EnsureBattleModeLouieAmounts(data, fallbackAmounts);

        int count = GameManager.BattleModeRandomEggMountTypes.Length;
        int[] result = new int[count];
        for (int i = 0; i < count; i++)
            result[i] = Mathf.Clamp(data.battleModeLouieAmounts[i], 0, 99);

        return result;
    }

    public static void SetBattleModeLouieAmounts(IReadOnlyList<int> amounts)
    {
        EnsureLoaded();
        EnsureBattleModeLouieAmounts(data, GameManager.GetDefaultBattleModeLouieAmounts());

        int count = GameManager.BattleModeRandomEggMountTypes.Length;
        bool changed = false;
        for (int i = 0; i < count; i++)
        {
            int value = amounts != null && i < amounts.Count
                ? amounts[i]
                : data.battleModeLouieAmounts[i];

            value = Mathf.Clamp(value, 0, 99);
            if (data.battleModeLouieAmounts[i] == value)
                continue;

            data.battleModeLouieAmounts[i] = value;
            changed = true;
        }

        if (changed)
            Save();
    }

    private enum BattleModeHandicapProfileKind
    {
        Generic,
        PowerZone,
        Stage6
    }

    private static void EnsureBattleModeHandicapProfiles(SaveData d)
    {
        if (d == null)
            return;

        d.battleModeHandicapGeneric = NormalizeBattleModeHandicap(d.battleModeHandicapGeneric, BattleModeHandicapProfileKind.Generic);
        d.battleModeHandicapPowerZone = NormalizeBattleModeHandicap(d.battleModeHandicapPowerZone, BattleModeHandicapProfileKind.PowerZone);
        d.battleModeHandicapStage6 = NormalizeBattleModeHandicap(d.battleModeHandicapStage6, BattleModeHandicapProfileKind.Stage6);
        if (!d.battleModeHandicapStage6Initialized)
        {
            ApplyStage6DefaultHandicap(d.battleModeHandicapStage6);
            d.battleModeHandicapStage6Initialized = true;
        }
    }

    private static BattleModeHandicapSave GetBattleModeHandicapProfile(SaveData d, int stageIndex)
    {
        return GetBattleModeHandicapProfileKind(stageIndex) switch
        {
            BattleModeHandicapProfileKind.PowerZone => d.battleModeHandicapPowerZone,
            BattleModeHandicapProfileKind.Stage6 => d.battleModeHandicapStage6,
            _ => d.battleModeHandicapGeneric
        };
    }

    private static BattleModeHandicapProfileKind GetBattleModeHandicapProfileKind(int stageIndex)
    {
        int normalizedStage = Mathf.Clamp(stageIndex, 1, BattleModeStageCount);
        if (normalizedStage == 10 || normalizedStage == 11)
            return BattleModeHandicapProfileKind.PowerZone;

        if (normalizedStage == 6)
            return BattleModeHandicapProfileKind.Stage6;

        return BattleModeHandicapProfileKind.Generic;
    }

    private static BattleModeHandicapSave CloneBattleModeHandicap(
        BattleModeHandicapSave source,
        BattleModeHandicapProfileKind profileKind)
    {
        return NormalizeBattleModeHandicap(source, profileKind);
    }

    private static BattleModeHandicapSave NormalizeBattleModeHandicap(
        BattleModeHandicapSave source,
        BattleModeHandicapProfileKind profileKind)
    {
        BattleModeHandicapSave normalized = new();
        BattleModeHandicapPlayerSave[] previous = source?.players;
        bool migrateLegacyDefaultHearts = ShouldMigrateLegacyBattleModeHandicapHearts(previous);
        normalized.players = new BattleModeHandicapPlayerSave[6];

        for (int i = 0; i < normalized.players.Length; i++)
        {
            BattleModeHandicapPlayerSave sourcePlayer = previous != null && i < previous.Length
                ? previous[i]
                : null;

            BattleModeHandicapPlayerSave player = new();
            if (sourcePlayer == null && profileKind == BattleModeHandicapProfileKind.PowerZone)
                ApplyPowerZoneDefaultHandicap(player);

            if (sourcePlayer != null)
            {
                player.mountedLouie = sourcePlayer.mountedLouie;
                player.life = sourcePlayer.life;
                player.bombAmount = sourcePlayer.bombAmount;
                player.blastRadius = sourcePlayer.blastRadius;
                player.speedLevel = sourcePlayer.speedLevel;
                player.bombType = sourcePlayer.bombType;
                player.punchBomb = sourcePlayer.punchBomb;
                player.powerGlove = sourcePlayer.powerGlove;
                player.movementAbility = sourcePlayer.movementAbility;
                player.fullFire = sourcePlayer.fullFire;
                player.destructiblePass = sourcePlayer.destructiblePass;
            }

            player.mountedLouie = NormalizeMountedLouie(player.mountedLouie);
            if (migrateLegacyDefaultHearts)
                player.life = 0;

            player.life = Mathf.Clamp(player.life, 0, 9);
            player.bombAmount = Mathf.Clamp(player.bombAmount, 1, PlayerPersistentStats.MaxBombAmount);
            player.blastRadius = Mathf.Clamp(player.blastRadius, 1, PlayerPersistentStats.MaxExplosionRadius);
            player.speedLevel = Mathf.Clamp(player.speedLevel, 1, PlayerPersistentStats.MaxSpeedUps + 1);
            player.bombType = Enum.IsDefined(typeof(BattleModeHandicapBombType), player.bombType)
                ? player.bombType
                : (int)BattleModeHandicapBombType.Default;
            player.movementAbility = Enum.IsDefined(typeof(BattleModeHandicapMovementAbility), player.movementAbility)
                ? player.movementAbility
                : (int)BattleModeHandicapMovementAbility.None;

            if (profileKind == BattleModeHandicapProfileKind.PowerZone &&
                ShouldUsePowerZoneDefaultHandicap(previous))
            {
                ApplyPowerZoneDefaultHandicap(player);
            }

            if (profileKind == BattleModeHandicapProfileKind.PowerZone)
                player.mountedLouie = (int)MountedType.None;

            normalized.players[i] = player;
        }

        return normalized;
    }

    private static bool ShouldMigrateLegacyBattleModeHandicapHearts(BattleModeHandicapPlayerSave[] players)
    {
        if (players == null || players.Length != 6)
            return false;

        for (int i = 0; i < players.Length; i++)
        {
            BattleModeHandicapPlayerSave player = players[i];
            if (player == null)
                return false;

            if (player.life != 1 ||
                player.bombAmount != 1 ||
                player.blastRadius != 2 ||
                player.speedLevel != 2 ||
                player.mountedLouie != (int)MountedType.None ||
                player.bombType != (int)BattleModeHandicapBombType.Default ||
                player.punchBomb ||
                player.powerGlove ||
                (player.movementAbility != (int)BattleModeHandicapMovementAbility.None &&
                 player.movementAbility != (int)BattleModeHandicapMovementAbility.Kick) ||
                player.fullFire ||
                player.destructiblePass)
            {
                return false;
            }
        }

        return true;
    }

    private static bool ShouldUsePowerZoneDefaultHandicap(BattleModeHandicapPlayerSave[] players)
    {
        if (players == null || players.Length != 6)
            return true;

        for (int i = 0; i < players.Length; i++)
        {
            BattleModeHandicapPlayerSave player = players[i];
            if (player == null)
                return true;

            bool defaultHeart = player.life == 0 || player.life == 1;
            bool defaultMovement =
                player.movementAbility == (int)BattleModeHandicapMovementAbility.None ||
                player.movementAbility == (int)BattleModeHandicapMovementAbility.Kick;

            if (!defaultHeart ||
                player.bombAmount != 1 ||
                player.blastRadius != 2 ||
                player.speedLevel != 2 ||
                player.mountedLouie != (int)MountedType.None ||
                player.bombType != (int)BattleModeHandicapBombType.Default ||
                player.punchBomb ||
                player.powerGlove ||
                !defaultMovement ||
                player.fullFire ||
                player.destructiblePass)
            {
                return false;
            }
        }

        return true;
    }

    private static void ApplyPowerZoneDefaultHandicap(BattleModeHandicapPlayerSave player)
    {
        if (player == null)
            return;

        player.life = 0;
        player.bombAmount = PlayerPersistentStats.MaxBombAmount;
        player.blastRadius = PlayerPersistentStats.MaxExplosionRadius;
        player.speedLevel = PlayerPersistentStats.MaxSpeedUps + 1;
        player.bombType = (int)BattleModeHandicapBombType.Default;
        player.punchBomb = true;
        player.powerGlove = true;
        player.movementAbility = (int)BattleModeHandicapMovementAbility.Kick;
        player.fullFire = false;
        player.destructiblePass = false;
    }

    private static void ApplyStage6DefaultHandicap(BattleModeHandicapSave handicap)
    {
        if (handicap?.players == null)
            return;

        for (int i = 0; i < handicap.players.Length; i++)
        {
            if (handicap.players[i] == null)
                handicap.players[i] = new BattleModeHandicapPlayerSave();

            handicap.players[i].movementAbility = (int)BattleModeHandicapMovementAbility.Kick;
        }
    }

    private static int NormalizeMountedLouie(int mountedLouie)
    {
        if (!Enum.IsDefined(typeof(MountedType), mountedLouie))
            return (int)MountedType.None;

        MountedType type = (MountedType)mountedLouie;
        if (type == MountedType.None)
            return mountedLouie;

        for (int i = 0; i < GameManager.BattleModeRandomEggMountTypes.Length; i++)
        {
            if (GameManager.BattleModeRandomEggMountTypes[i] == type)
                return mountedLouie;
        }

        return (int)MountedType.None;
    }

    private static void EnsureBattleModeItemAmounts(SaveData d, IReadOnlyList<int> fallbackAmounts)
    {
        if (d == null)
            return;

        int itemCount = GameManager.BattleModeHiddenDropEntries.Length;

        if (d.battleModeItemAmounts == null || d.battleModeItemAmounts.Length != itemCount)
        {
            int[] previous = d.battleModeItemAmounts;

            d.battleModeItemAmounts = ConvertBattleModeItemAmounts(previous, fallbackAmounts, itemCount);
            NormalizeBattleModeRandomEggRange(d.battleModeItemAmounts);
            Save();
            return;
        }

        bool changed = false;

        for (int i = 0; i < d.battleModeItemAmounts.Length; i++)
        {
            int normalized = Mathf.Clamp(d.battleModeItemAmounts[i], 0, 99);

            if (d.battleModeItemAmounts[i] == normalized)
                continue;

            d.battleModeItemAmounts[i] = normalized;
            changed = true;
        }

        int[] beforeNormalize = new int[itemCount];
        Array.Copy(d.battleModeItemAmounts, beforeNormalize, itemCount);

        NormalizeBattleModeRandomEggRange(d.battleModeItemAmounts);

        for (int i = 0; i < itemCount; i++)
        {
            if (beforeNormalize[i] != d.battleModeItemAmounts[i])
            {
                changed = true;
                break;
            }
        }

        if (changed)
            Save();
    }

    private static void EnsureBattleModeLouieAmounts(SaveData d, IReadOnlyList<int> fallbackAmounts)
    {
        if (d == null)
            return;

        int count = GameManager.BattleModeRandomEggMountTypes.Length;
        if (d.battleModeLouieAmounts == null || d.battleModeLouieAmounts.Length != count)
        {
            int[] previous = d.battleModeLouieAmounts;
            d.battleModeLouieAmounts = new int[count];
            for (int i = 0; i < count; i++)
            {
                int value = previous != null && i < previous.Length
                    ? previous[i]
                    : fallbackAmounts != null && i < fallbackAmounts.Count
                        ? fallbackAmounts[i]
                        : 1;

                d.battleModeLouieAmounts[i] = Mathf.Clamp(value, 0, 99);
            }

            Save();
            return;
        }

        bool changed = false;
        for (int i = 0; i < d.battleModeLouieAmounts.Length; i++)
        {
            int value = Mathf.Clamp(d.battleModeLouieAmounts[i], 0, 99);
            if (d.battleModeLouieAmounts[i] == value)
                continue;

            d.battleModeLouieAmounts[i] = value;
            changed = true;
        }

        if (changed)
            Save();
    }

    private static int[] ConvertBattleModeItemAmounts(
    int[] previous,
    IReadOnlyList<int> fallbackAmounts,
    int itemCount)
    {
        int[] converted = BuildBattleModeItemAmountsFromFallback(fallbackAmounts, itemCount);

        if (previous == null)
            return converted;

        if (previous.Length == 25 && itemCount == GameManager.BattleModeHiddenDropEntries.Length)
        {
            int copyCount = Mathf.Min(16, Mathf.Min(previous.Length, converted.Length));
            for (int i = 0; i < copyCount; i++)
                converted[i] = Mathf.Clamp(previous[i], 0, 99);

            if (converted.Length > 18 && previous.Length > 24)
                converted[18] = Mathf.Clamp(previous[24], 0, 99);

            NormalizeBattleModeRandomEggRange(converted);
            return converted;
        }

        for (int i = 0; i < converted.Length && i < previous.Length; i++)
            converted[i] = Mathf.Clamp(previous[i], 0, 99);

        NormalizeBattleModeRandomEggRange(converted);
        return converted;
    }

    private static int[] BuildBattleModeItemAmountsFromFallback(IReadOnlyList<int> fallbackAmounts, int itemCount)
    {
        int[] result = new int[itemCount];

        for (int i = 0; i < itemCount; i++)
        {
            int value = fallbackAmounts != null && i < fallbackAmounts.Count
                ? fallbackAmounts[i]
                : 0;

            result[i] = Mathf.Clamp(value, 0, 99);
        }

        NormalizeBattleModeRandomEggRange(result);
        return result;
    }

    private static void EnsureDirectoryExists()
    {
        if (!Directory.Exists(SaveDirectoryPath))
            Directory.CreateDirectory(SaveDirectoryPath);
    }

    public static void LoadControlActivePlayersIntoGameSession()
    {
        EnsureLoaded();
        EnsureControlProfiles(data);

        var session = GameSession.Instance;
        if (session == null)
            return;

        List<int> activePlayerIds = new();

        for (int i = 0; i < data.controls.Count && i < 6; i++)
        {
            SavedPlayerControls control = data.controls[i];
            if (control == null)
                continue;

            if (control.active)
                activePlayerIds.Add(i + 1);
        }

        if (activePlayerIds.Count <= 0)
            activePlayerIds.Add(1);

        session.SetActivePlayerIds(activePlayerIds);
    }

    public static void SaveControlActivePlayersFromGameSession()
    {
        EnsureLoaded();
        EnsureControlProfiles(data);

        var session = GameSession.Instance;
        if (session == null)
            return;

        for (int i = 0; i < data.controls.Count && i < 6; i++)
        {
            SavedPlayerControls control = data.controls[i];
            if (control == null)
                continue;

            int playerId = i + 1;
            control.playerId = playerId;
            control.active = session.IsPlayerActive(playerId);
        }

        Save();
    }

    public static void SetControlPlayerActive(int playerId, bool active)
    {
        EnsureLoaded();
        EnsureControlProfiles(data);

        playerId = Mathf.Clamp(playerId, 1, 6);
        int index = playerId - 1;

        data.controls[index].playerId = playerId;
        data.controls[index].active = active;

        var session = GameSession.Instance;
        if (session != null)
            session.SetPlayerActive(playerId, active);

        SetBattleModePlayerControlMode(
            playerId,
            active ? BattleModePlayerControlMode.Man : BattleModePlayerControlMode.Com
        );
    }
}

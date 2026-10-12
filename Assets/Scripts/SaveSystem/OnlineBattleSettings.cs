using System;
using UnityEngine;

public static partial class SaveSystem
{
    static SaveData onlineBattleData;
    public static SaveData BattleConfigurationData { get { EnsureLoaded(); return BattleData; } }
    static SaveData BattleData => onlineBattleData ?? data;
    public static bool HasOnlineBattleSettings => onlineBattleData != null;
    static void SaveBattleConfiguration() { if (onlineBattleData == null) Save(); }

    public static void BeginOnlineBattleSettings()
    {
        EnsureLoaded();
        if (onlineBattleData == null) onlineBattleData = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data));
    }
    public static void EndOnlineBattleSettings() => onlineBattleData = null;
    public static void ApplyOnlineBattleSettings(string json)
    {
        if (string.IsNullOrEmpty(json)) return;
        BeginOnlineBattleSettings();
        JsonUtility.FromJsonOverwrite(json, onlineBattleData);
        PlayerPersistentStats.ApplyOnlineSelections(onlineBattleData);
    }
    public static string CaptureOnlineBattleSettings()
    {
        var value = BattleConfigurationData;
        var snapshot = new BattleConfigurationSnapshot
        {
            battleModeMatchMode = value.battleModeMatchMode,
            battleModeComputerLevel = value.battleModeComputerLevel,
            battleModeBattlesToWin = value.battleModeBattlesToWin,
            battleModeRoundTimerMode = value.battleModeRoundTimerMode,
            battleModeSuddenDeath = value.battleModeSuddenDeath,
            battleModeRevengeBomber = value.battleModeRevengeBomber,
            battleModeStageIndex = value.battleModeStageIndex,
            battleModeMusicSelectionMask = value.battleModeMusicSelectionMask,
            battleModePlayerControlModes = value.battleModePlayerControlModes,
            battleModePlayerTeams = value.battleModePlayerTeams,
            player1SelectedSkin = value.player1SelectedSkin,
            player2SelectedSkin = value.player2SelectedSkin,
            player3SelectedSkin = value.player3SelectedSkin,
            player4SelectedSkin = value.player4SelectedSkin,
            player5SelectedSkin = value.player5SelectedSkin,
            player6SelectedSkin = value.player6SelectedSkin,
            player1SelectedCharacter = value.player1SelectedCharacter,
            player2SelectedCharacter = value.player2SelectedCharacter,
            player3SelectedCharacter = value.player3SelectedCharacter,
            player4SelectedCharacter = value.player4SelectedCharacter,
            player5SelectedCharacter = value.player5SelectedCharacter,
            player6SelectedCharacter = value.player6SelectedCharacter,
            battleModeItemAmounts = value.battleModeItemAmounts,
            battleModeLouieAmounts = value.battleModeLouieAmounts,
            battleModeHandicapGeneric = value.battleModeHandicapGeneric,
            battleModeHandicapPowerZone = value.battleModeHandicapPowerZone,
            battleModeHandicapPowerZoneInitialized = value.battleModeHandicapPowerZoneInitialized,
            battleModeHandicapStage6 = value.battleModeHandicapStage6,
            battleModeHandicapStage6Initialized = value.battleModeHandicapStage6Initialized,
        };
        return JsonUtility.ToJson(snapshot);
    }

    [Serializable]
    sealed class BattleConfigurationSnapshot
    {
        public int battleModeMatchMode;
        public int battleModeComputerLevel;
        public int battleModeBattlesToWin;
        public int battleModeRoundTimerMode;
        public int battleModeSuddenDeath;
        public bool battleModeRevengeBomber;
        public int battleModeStageIndex;
        public int battleModeMusicSelectionMask;
        public int[] battleModePlayerControlModes;
        public int[] battleModePlayerTeams;
        public int player1SelectedSkin;
        public int player2SelectedSkin;
        public int player3SelectedSkin;
        public int player4SelectedSkin;
        public int player5SelectedSkin;
        public int player6SelectedSkin;
        public int player1SelectedCharacter;
        public int player2SelectedCharacter;
        public int player3SelectedCharacter;
        public int player4SelectedCharacter;
        public int player5SelectedCharacter;
        public int player6SelectedCharacter;
        public int[] battleModeItemAmounts;
        public int[] battleModeLouieAmounts;
        public SaveData.BattleModeHandicapSave battleModeHandicapGeneric;
        public SaveData.BattleModeHandicapSave battleModeHandicapPowerZone;
        public bool battleModeHandicapPowerZoneInitialized;
        public SaveData.BattleModeHandicapSave battleModeHandicapStage6;
        public bool battleModeHandicapStage6Initialized;
    }
}

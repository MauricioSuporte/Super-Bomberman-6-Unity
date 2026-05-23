using Assets.Scripts.SaveSystem;
using System;
using System.Collections.Generic;

[Serializable]
public sealed class SaveData
{
    public int activeSlotIndex = -1;

    public List<string> unlockedSkins = new();

    public bool bossRushUnlocked = false;
    public bool nightmareUnlocked = false;
    public int battleModeMatchMode = (int)BattleModeRules.MatchMode.SingleMatch;
    public int battleModeComputerLevel = (int)BattleModeComputerLevel.Normal;
    public int battleModeBattlesToWin = 3;
    public int battleModeRoundTimerMode = (int)BattleModeRules.RoundTimerMode.TwoMinutes;
    public int battleModeSuddenDeath = (int)BattleModeSuddenDeathSetting.On;
    public bool battleModeRevengeBomber = true;
    public int battleModeStageIndex = 1;
    public int battleModeMusicSelectionMask = 0;
    public int[] battleModePlayerControlModes =
    {
        (int)BattleModePlayerControlMode.Man,
        (int)BattleModePlayerControlMode.Com,
        (int)BattleModePlayerControlMode.Com,
        (int)BattleModePlayerControlMode.Com,
        (int)BattleModePlayerControlMode.Com,
        (int)BattleModePlayerControlMode.Com
    };
    public int[] battleModePlayerTeams =
    {
        (int)BattleModeRules.TeamId.Blue,
        (int)BattleModeRules.TeamId.Red,
        (int)BattleModeRules.TeamId.Green,
        (int)BattleModeRules.TeamId.Blue,
        (int)BattleModeRules.TeamId.Red,
        (int)BattleModeRules.TeamId.Green
    };

    public int player1SelectedSkin = (int)BomberSkin.White;
    public int player2SelectedSkin = (int)BomberSkin.Black;
    public int player3SelectedSkin = (int)BomberSkin.Blue;
    public int player4SelectedSkin = (int)BomberSkin.Red;
    public int player5SelectedSkin = (int)BomberSkin.Green;
    public int player6SelectedSkin = (int)BomberSkin.Yellow;

    public List<StageSlot> slots = new()
    {
        new StageSlot(),
        new StageSlot(),
        new StageSlot()
    };

    public List<SavedPlayerControls> controls = new()
    {
        new SavedPlayerControls { playerId = 1, active = true },
        new SavedPlayerControls { playerId = 2, active = false },
        new SavedPlayerControls { playerId = 3, active = false },
        new SavedPlayerControls { playerId = 4, active = false },
        new SavedPlayerControls { playerId = 5, active = false },
        new SavedPlayerControls { playerId = 6, active = false }
    };

    public List<BossRushDifficultyTimesSave> bossRushTimes = new();
    public int[] battleModeItemAmounts;
    public int[] battleModeLouieAmounts;
    public BattleModeHandicapSave battleModeHandicapGeneric = new();
    public BattleModeHandicapSave battleModeHandicapPowerZone = new();
    public BattleModeHandicapSave battleModeHandicapStage6 = new();
    public bool battleModeHandicapStage6Initialized;
    public SavedVideoSettings videoSettings = new();

    [Serializable]
    public sealed class BattleModeHandicapSave
    {
        public BattleModeHandicapPlayerSave[] players =
        {
            new(),
            new(),
            new(),
            new(),
            new(),
            new()
        };
    }

    [Serializable]
    public sealed class BattleModeHandicapPlayerSave
    {
        public int mountedLouie = (int)MountedType.None;
        public int life = 0;
        public int bombAmount = 1;
        public int blastRadius = 2;
        public int speedLevel = 2;
        public int bombType = (int)BattleModeHandicapBombType.Default;
        public bool punchBomb;
        public bool powerGlove;
        public int movementAbility = (int)BattleModeHandicapMovementAbility.None;
        public bool fullFire;
        public bool destructiblePass;
    }
}

public enum BattleModeHandicapBombType
{
    Default = 0,
    Power = 1,
    Rubber = 2,
    Pierce = 3,
    Control = 4,
    Magnet = 5
}

public enum BattleModeHandicapMovementAbility
{
    None = 0,
    Kick = 1,
    BombPass = 2
}

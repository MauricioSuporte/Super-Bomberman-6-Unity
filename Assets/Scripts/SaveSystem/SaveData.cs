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
    public int[] battleModePlayerControlModes =
    {
        (int)BattleModePlayerControlMode.Man,
        (int)BattleModePlayerControlMode.Com,
        (int)BattleModePlayerControlMode.Com,
        (int)BattleModePlayerControlMode.Com,
        (int)BattleModePlayerControlMode.Off,
        (int)BattleModePlayerControlMode.Off
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
        new SavedPlayerControls { playerId = 1 },
        new SavedPlayerControls { playerId = 2 },
        new SavedPlayerControls { playerId = 3 },
        new SavedPlayerControls { playerId = 4 },
        new SavedPlayerControls { playerId = 5 },
        new SavedPlayerControls { playerId = 6 }
    };

    public List<BossRushDifficultyTimesSave> bossRushTimes = new();

    public SavedVideoSettings videoSettings = new();
}

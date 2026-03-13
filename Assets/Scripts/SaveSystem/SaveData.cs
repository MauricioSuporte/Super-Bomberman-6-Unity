
using Assets.Scripts.SaveSystem;
using System;
using System.Collections.Generic;

[Serializable]
public sealed class SaveData
{
    public int activeSlotIndex = -1;

    public List<string> unlockedSkins = new();

    public bool bossRushUnlocked = false;

    public int player1SelectedSkin = (int)BomberSkin.White;
    public int player2SelectedSkin = (int)BomberSkin.White;
    public int player3SelectedSkin = (int)BomberSkin.White;
    public int player4SelectedSkin = (int)BomberSkin.White;

    public List<StageSlot> slots = new()
    {
        new StageSlot(),
        new StageSlot(),
        new StageSlot()
    };
}
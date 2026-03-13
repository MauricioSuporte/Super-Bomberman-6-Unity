using Assets.Scripts.SaveSystem;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class SaveSystem
{
    private const string SaveFolderName = "SaveData";
    private const string SaveFileName = "save.dat";

    private static bool loaded;
    private static SaveData data;

    private static readonly BomberSkin[] defaultUnlockedSkins =
    {
        BomberSkin.Golden,
        BomberSkin.White,
        BomberSkin.Black,
        BomberSkin.Red,
        BomberSkin.Orange,
        BomberSkin.Yellow,
        BomberSkin.Lime,
        BomberSkin.Green,
        BomberSkin.Cyan,
        BomberSkin.Aqua,
        BomberSkin.Blue,
        BomberSkin.DarkBlue,
        BomberSkin.Purple,
        BomberSkin.Magenta,
        BomberSkin.Pink,
        BomberSkin.Brown,
        BomberSkin.DarkGreen,
        BomberSkin.Nightmare,
        BomberSkin.Gold
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

        return (slot.clearedStages != null && slot.clearedStages.Count > 0) ||
               (slot.unlockedStages != null && slot.unlockedStages.Count > 1);
    }

    public static void ResetSlot(int slotIndex)
    {
        StageSlot slot = GetSlot(slotIndex);
        if (slot == null)
            return;

        slot.unlockedStages.Clear();
        slot.clearedStages.Clear();
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

        slot.unlockedStages.Clear();
        slot.clearedStages.Clear();
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

        for (int i = 0; i < 4; i++)
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

        for (int i = 0; i < 4; i++)
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

            if (d.slots[i].perfectStages == null)
                d.slots[i].perfectStages = new List<string>();

            if (d.slots[i].stageOrder == null)
                d.slots[i].stageOrder = new List<string>();

            if (d.slots[i].unlockedStages.Count == 0)
                d.slots[i].unlockedStages.Add("Stage_1-1");
        }

        EnsureControlProfiles(d);

        for (int i = 0; i < defaultUnlockedSkins.Length; i++)
        {
            string skinName = defaultUnlockedSkins[i].ToString();
            if (!d.unlockedSkins.Contains(skinName))
                d.unlockedSkins.Add(skinName);
        }

        if (!Enum.IsDefined(typeof(BomberSkin), d.player1SelectedSkin))
            d.player1SelectedSkin = (int)BomberSkin.White;

        if (!Enum.IsDefined(typeof(BomberSkin), d.player2SelectedSkin))
            d.player2SelectedSkin = (int)BomberSkin.White;

        if (!Enum.IsDefined(typeof(BomberSkin), d.player3SelectedSkin))
            d.player3SelectedSkin = (int)BomberSkin.White;

        if (!Enum.IsDefined(typeof(BomberSkin), d.player4SelectedSkin))
            d.player4SelectedSkin = (int)BomberSkin.White;

        if (d.activeSlotIndex < -1 || d.activeSlotIndex >= d.slots.Count)
            d.activeSlotIndex = -1;
    }

    private static void EnsureControlProfiles(SaveData d)
    {
        if (d.controls == null)
            d.controls = new List<SavedPlayerControls>();

        while (d.controls.Count < 4)
            d.controls.Add(new SavedPlayerControls { playerId = d.controls.Count + 1 });

        if (d.controls.Count > 4)
            d.controls.RemoveRange(4, d.controls.Count - 4);

        for (int i = 0; i < d.controls.Count; i++)
        {
            if (d.controls[i] == null)
                d.controls[i] = new SavedPlayerControls();

            d.controls[i].playerId = i + 1;

            if (d.controls[i].joyIndex < 1)
                d.controls[i].joyIndex = 1;

            if (d.controls[i].bindings == null)
                d.controls[i].bindings = new List<SavedBinding>();
        }
    }

    private static void EnsureDirectoryExists()
    {
        if (!Directory.Exists(SaveDirectoryPath))
            Directory.CreateDirectory(SaveDirectoryPath);
    }
}
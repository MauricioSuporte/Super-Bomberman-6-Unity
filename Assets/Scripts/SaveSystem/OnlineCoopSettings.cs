using UnityEngine;

public static partial class SaveSystem
{
    static SaveData coopOfflineData;
    public static bool HasOnlineCoopSettings => coopOfflineData != null;

    public static void BeginOnlineCoopSettings()
    {
        EnsureLoaded();
        if (coopOfflineData != null) return;
        coopOfflineData = data;
        data = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data));
        // Co-op runs have their own temporary campaign; never borrow the selected offline slot.
        data.slots = new System.Collections.Generic.List<Assets.Scripts.SaveSystem.StageSlot>
        {
            new() { started = true, difficulty = (int)Assets.Scripts.SaveSystem.NormalGameDifficulty.Normal }
        };
        data.activeSlotIndex = 0;
        data.slots[0].unlockedStages.Add("Stage_1-1");
    }

    public static void EndOnlineCoopSettings()
    {
        if (coopOfflineData == null) return;
        data = coopOfflineData;
        coopOfflineData = null;
    }
}

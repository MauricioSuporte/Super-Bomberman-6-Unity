using System;
using System.IO;
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
    }

    // Only gameplay/progress travels over the network. Hardware bindings,
    // language, touch layout and video settings always belong to this machine.
    static SaveData GameplayCopy(SaveData source)
    {
        var copy = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(source));
        copy.controls = null; copy.touchControls = null; copy.videoSettings = null;
        copy.language = 0;
        return copy;
    }

    public static string CaptureRoomOwnerSave() => JsonUtility.ToJson(GameplayCopy(coopOfflineData ?? Data));
    public static string CaptureRoomProgress() => JsonUtility.ToJson(GameplayCopy(Data));

    public static bool ApplyRoomProgress(string json, bool persistOwner)
    {
        if (string.IsNullOrEmpty(json) || json.Length > 131072) return false;
        SaveData incoming;
        try { incoming = JsonUtility.FromJson<SaveData>(json); }
        catch (ArgumentException) { return false; }
        if (incoming == null || incoming.slots == null || incoming.slots.Count != 3) return false;
        BeginOnlineCoopSettings();
        incoming.controls = data.controls; incoming.touchControls = data.touchControls;
        incoming.videoSettings = data.videoSettings; incoming.language = data.language;
        NormalizeData(incoming);
        data = incoming;
        onlineBattleData = data;
        if (persistOwner)
        {
            var saved = GameplayCopy(incoming);
            saved.controls = coopOfflineData.controls; saved.touchControls = coopOfflineData.touchControls;
            saved.videoSettings = coopOfflineData.videoSettings; saved.language = coopOfflineData.language;
            coopOfflineData = saved;
            try
            {
                EnsureDirectoryExists();
                File.WriteAllText(SaveFilePath, JsonUtility.ToJson(saved, true), System.Text.Encoding.UTF8);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { Debug.LogWarning("[SaveSystem] Online owner save failed: " + error.Message); }
        }
        return true;
    }

    public static void EndOnlineCoopSettings()
    {
        if (coopOfflineData == null) return;
        data = coopOfflineData;
        coopOfflineData = null;
    }
}

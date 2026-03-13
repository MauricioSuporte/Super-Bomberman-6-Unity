using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public sealed class SB6SaveData
{
    public List<string> unlockedStages = new();
    public List<string> clearedStages = new();
    public List<string> perfectStages = new();
    public List<string> stageOrder = new();

    public List<string> unlockedSkins = new();

    public bool bossRushUnlocked = false;

    public int player1SelectedSkin = (int)BomberSkin.White;
    public int player2SelectedSkin = (int)BomberSkin.White;
    public int player3SelectedSkin = (int)BomberSkin.White;
    public int player4SelectedSkin = (int)BomberSkin.White;
}

public static class SB6SaveSystem
{
    private const string SaveFolderName = "SaveData";
    private const string SaveFileName = "sb6_save.dat";

    private static bool loaded;
    private static SB6SaveData data;

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

    public static SB6SaveData Data
    {
        get
        {
            EnsureLoaded();
            return data;
        }
    }

    public static string SaveDirectoryPath => Path.Combine(Application.persistentDataPath, SaveFolderName);
    public static string SaveFilePath => Path.Combine(SaveDirectoryPath, SaveFileName);

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
            Debug.LogWarning($"[SB6SaveSystem] Falha ao salvar arquivo: {ex.Message}");
        }
    }

    public static void ResetAll()
    {
        data = CreateDefaultData();
        loaded = true;
        Save();
    }

    private static void EnsureLoaded()
    {
        if (loaded)
            return;

        loaded = true;
        data = CreateDefaultData();

        try
        {
            if (!File.Exists(SaveFilePath))
            {
                Save();
                return;
            }

            string json = File.ReadAllText(SaveFilePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                Save();
                return;
            }

            SB6SaveData loadedData = JsonUtility.FromJson<SB6SaveData>(json);
            if (loadedData == null)
            {
                Save();
                return;
            }

            data = loadedData;
            NormalizeData();
            Save();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SB6SaveSystem] Falha ao carregar arquivo: {ex.Message}");
            data = CreateDefaultData();
            Save();
        }
    }

    private static SB6SaveData CreateDefaultData()
    {
        SB6SaveData d = new SB6SaveData();
        NormalizeData(d);
        return d;
    }

    private static void NormalizeData()
    {
        NormalizeData(data);
    }

    private static void NormalizeData(SB6SaveData d)
    {
        if (d.unlockedStages == null)
            d.unlockedStages = new List<string>();

        if (d.clearedStages == null)
            d.clearedStages = new List<string>();

        if (d.perfectStages == null)
            d.perfectStages = new List<string>();

        if (d.stageOrder == null)
            d.stageOrder = new List<string>();

        if (d.unlockedSkins == null)
            d.unlockedSkins = new List<string>();

        if (d.unlockedStages.Count == 0)
            d.unlockedStages.Add("Stage_1-1");

        for (int i = 0; i < defaultUnlockedSkins.Length; i++)
        {
            string name = defaultUnlockedSkins[i].ToString();
            if (!d.unlockedSkins.Contains(name))
                d.unlockedSkins.Add(name);
        }

        if (!Enum.IsDefined(typeof(BomberSkin), d.player1SelectedSkin))
            d.player1SelectedSkin = (int)BomberSkin.White;

        if (!Enum.IsDefined(typeof(BomberSkin), d.player2SelectedSkin))
            d.player2SelectedSkin = (int)BomberSkin.White;

        if (!Enum.IsDefined(typeof(BomberSkin), d.player3SelectedSkin))
            d.player3SelectedSkin = (int)BomberSkin.White;

        if (!Enum.IsDefined(typeof(BomberSkin), d.player4SelectedSkin))
            d.player4SelectedSkin = (int)BomberSkin.White;
    }

    private static void EnsureDirectoryExists()
    {
        if (!Directory.Exists(SaveDirectoryPath))
            Directory.CreateDirectory(SaveDirectoryPath);
    }
}
using System;
using System.Collections.Generic;
using UnityEngine;

public static class BomberSkinResourceCatalog
{
    const string BombersResourcesPath = "Sprites/Bombers";
    const int DynamicCharacterIdOffset = 1000;

    public const string BombermanGeneratedResourcesPath = "Sprites/Bombers/Bomberman/Generated/Bomberman";
    public const string LadyBomberGeneratedResourcesPath = "Sprites/Bombers/LadyBomber/Generated/LadyBomber";
    public const string TinyBomberGeneratedResourcesPath = "Sprites/Bombers/TinyBomber/Generated/TinyBomber";

    static readonly BomberCharacter[] BuiltInCharacters =
    {
        BomberCharacter.Bomberman,
        BomberCharacter.LadyBomber,
        BomberCharacter.TinyBomber
    };

    static readonly Dictionary<BomberCharacter, string> characterFolders = new();
    static BomberCharacter[] availableCharacters;

    public static readonly BomberSkin[] BombermanSkins =
    {
        BomberSkin.White,
        BomberSkin.Black,
        BomberSkin.Red,
        BomberSkin.Blue,
        BomberSkin.Green,
        BomberSkin.Purple,
        BomberSkin.Pink,
        BomberSkin.Magenta,
        BomberSkin.Gray,
        BomberSkin.Brown,
        BomberSkin.DarkGreen,
        BomberSkin.DarkBlue,
        BomberSkin.Orange,
        BomberSkin.Cyan,
        BomberSkin.Aqua,
        BomberSkin.DarkPurple,
        BomberSkin.Yellow,
        BomberSkin.NeonGreen,
        BomberSkin.Gold,
        BomberSkin.Golden,
        BomberSkin.Nightmare,
        BomberSkin.Alternative1,
        BomberSkin.Alternative2,
        BomberSkin.Alternative3,
        BomberSkin.Alternative4,
        BomberSkin.Palette26,
        BomberSkin.Palette27,
        BomberSkin.Palette28,
        BomberSkin.Palette29,
        BomberSkin.Palette30,
        BomberSkin.Palette31,
        BomberSkin.Palette32,
        BomberSkin.Palette33,
        BomberSkin.Palette34,
        BomberSkin.Palette35,
        BomberSkin.Palette36,
        BomberSkin.Palette37,
        BomberSkin.Palette38,
        BomberSkin.Palette39,
        BomberSkin.Palette40,
        BomberSkin.Palette41,
        BomberSkin.Palette42,
        BomberSkin.Palette43
    };

    public static BomberCharacter[] GetAvailableCharacters()
    {
        EnsureCharacterCatalog();
        return availableCharacters;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetCharacterCatalog()
    {
        availableCharacters = null;
        characterFolders.Clear();
    }

    public static bool IsAvailableCharacter(BomberCharacter character)
    {
        EnsureCharacterCatalog();
        return characterFolders.ContainsKey(character);
    }

    public static string GetGeneratedResourcesPath(BomberCharacter character)
    {
        if (character == BomberCharacter.Bomberman)
            return BombermanGeneratedResourcesPath;

        if (character == BomberCharacter.LadyBomber)
            return LadyBomberGeneratedResourcesPath;

        if (character == BomberCharacter.TinyBomber)
            return TinyBomberGeneratedResourcesPath;

        string folderName = GetCharacterFolderName(character);
        return $"{BombersResourcesPath}/{folderName}/Generated/{folderName}";
    }

    public static string GetSheetName(BomberCharacter character, BomberSkin skin)
    {
        return GetSheetName(GetCharacterFolderName(character), skin);
    }

    public static string GetSheetName(string characterFolderName, BomberSkin skin)
    {
        string characterSuffix = characterFolderName == "Bomberman" ? "Bomber" : characterFolderName;
        return skin + characterSuffix;
    }

    public static string GetCharacterFolderName(BomberCharacter character)
    {
        EnsureCharacterCatalog();
        return characterFolders.TryGetValue(character, out string folderName)
            ? folderName
            : "Bomberman";
    }

    public static bool IsGeneratedSkin(BomberCharacter character, BomberSkin skin)
    {
        if (!IsAvailableCharacter(character))
            return false;

        string sheetPath = $"{GetGeneratedResourcesPath(character)}/{GetSheetName(character, skin)}";
        return Resources.LoadAll<Sprite>(sheetPath).Length > 0;
    }

    public static BomberSkin NormalizeGeneratedSkin(BomberCharacter character, BomberSkin skin)
    {
        if (IsGeneratedSkin(character, skin))
            return skin;

        for (int i = 0; i < BombermanSkins.Length; i++)
        {
            if (IsGeneratedSkin(character, BombermanSkins[i]))
                return BombermanSkins[i];
        }

        return BomberSkin.White;
    }

    static void EnsureCharacterCatalog()
    {
        if (availableCharacters != null)
            return;

        characterFolders.Clear();
        AddCharacter(BomberCharacter.Bomberman, "Bomberman");
        AddCharacter(BomberCharacter.LadyBomber, "LadyBomber");
        AddCharacter(BomberCharacter.TinyBomber, "TinyBomber");

        Texture2D[] textures = Resources.LoadAll<Texture2D>(BombersResourcesPath);
        List<string> dynamicFolderNames = new();

        for (int i = 0; i < textures.Length; i++)
        {
            Texture2D texture = textures[i];
            if (texture == null || IsPalette(texture.name) || IsGeneratedSheet(texture.name))
                continue;

            if (string.Equals(texture.name, "Bomberman", StringComparison.Ordinal) ||
                string.Equals(texture.name, "LadyBomber", StringComparison.Ordinal) ||
                string.Equals(texture.name, "TinyBomber", StringComparison.Ordinal) ||
                dynamicFolderNames.Contains(texture.name))
                continue;

            dynamicFolderNames.Add(texture.name);
        }

        dynamicFolderNames.Sort(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < dynamicFolderNames.Count; i++)
        {
            string folderName = dynamicFolderNames[i];
            BomberCharacter character = (BomberCharacter)ComputeDynamicCharacterId(folderName);

            if (characterFolders.ContainsKey(character))
            {
                Debug.LogWarning($"[BomberSkinResourceCatalog] Character id collision for '{folderName}'. Rename the folder to make its id unique.");
                continue;
            }

            AddCharacter(character, folderName);
        }

        List<BomberCharacter> characters = new(BuiltInCharacters);
        for (int i = 0; i < dynamicFolderNames.Count; i++)
        {
            BomberCharacter character = (BomberCharacter)ComputeDynamicCharacterId(dynamicFolderNames[i]);
            if (characterFolders.ContainsKey(character))
                characters.Add(character);
        }

        availableCharacters = characters.ToArray();
    }

    static void AddCharacter(BomberCharacter character, string folderName)
    {
        characterFolders[character] = folderName;
    }

    static bool IsPalette(string assetName)
    {
        return assetName.IndexOf("palette", StringComparison.OrdinalIgnoreCase) >= 0 ||
               assetName.IndexOf("pallete", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static bool IsGeneratedSheet(string assetName)
    {
        for (int i = 0; i < BombermanSkins.Length; i++)
        {
            if (assetName.StartsWith(BombermanSkins[i].ToString(), StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    static int ComputeDynamicCharacterId(string folderName)
    {
        unchecked
        {
            uint hash = 2166136261;
            for (int i = 0; i < folderName.Length; i++)
            {
                hash ^= char.ToUpperInvariant(folderName[i]);
                hash *= 16777619;
            }

            return DynamicCharacterIdOffset + (int)(hash & 0x3fffffff);
        }
    }
}

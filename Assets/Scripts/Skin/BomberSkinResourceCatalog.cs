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
        BomberSkin.Palette1,
        BomberSkin.Palette2,
        BomberSkin.Palette3,
        BomberSkin.Palette4,
        BomberSkin.Palette5,
        BomberSkin.Palette6,
        BomberSkin.Palette7,
        BomberSkin.Palette8,
        BomberSkin.Palette9,
        BomberSkin.Palette10,
        BomberSkin.Palette11,
        BomberSkin.Palette12,
        BomberSkin.Palette13,
        BomberSkin.Palette14,
        BomberSkin.Palette15,
        BomberSkin.Palette16,
        BomberSkin.Palette17,
        BomberSkin.Palette18,
        BomberSkin.Palette19,
        BomberSkin.Palette20,
        BomberSkin.Palette21,
        BomberSkin.Palette22,
        BomberSkin.Palette23,
        BomberSkin.Palette24,
        BomberSkin.Palette25,
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
        return characterFolderName + GetPaletteNumber(skin);
    }

    public static int GetPaletteNumber(BomberSkin skin)
    {
        int value = (int)skin;
        return value >= 1 ? value : 1;
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

        return BomberSkin.Palette1;
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
        if (string.IsNullOrWhiteSpace(assetName))
            return false;

        int i = assetName.Length - 1;
        if (i < 0 || !char.IsDigit(assetName[i]))
            return false;

        while (i >= 0 && char.IsDigit(assetName[i]))
            i--;

        return i >= 0;
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

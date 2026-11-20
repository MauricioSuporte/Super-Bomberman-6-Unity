using System;
using System.Collections.Generic;
using UnityEngine;

public static class BomberSkinResourceCatalog
{
    const string BombersResourcesPath = "Sprites/Bombers";
    const int DynamicCharacterIdOffset = 1000;
    public const string MetadataResourcePath = "BomberSkinCatalog";
    public const string MetadataHeader = "SB6-SKIN-CATALOG-1";

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
    static readonly HashSet<string> generatedSheets = new(StringComparer.Ordinal);

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
        generatedSheets.Clear();
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
        return generatedSheets.Contains(sheetPath);
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

        // Discovering the roster must not decode/upload every generated palette sheet.
        // The existing Editor skin generator writes the same names and sprite presence.
        TextAsset metadata = Resources.Load<TextAsset>(MetadataResourcePath);
        if (metadata == null)
            throw new InvalidOperationException("Missing bomber skin catalog. Run Generate Missing Bomber Skin Sheets before building.");
        string[] lines = metadata.text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0 || lines[0] != MetadataHeader)
            throw new InvalidOperationException("Invalid bomber skin catalog header.");

        List<string> dynamicFolderNames = new();
        generatedSheets.Clear();
        for (int i = 1; i < lines.Length; i++)
        {
            if (lines[i].StartsWith("skin:", StringComparison.Ordinal))
                generatedSheets.Add(lines[i].Substring(5));
            else if (lines[i].StartsWith("character:", StringComparison.Ordinal))
            {
                string name = lines[i].Substring(10);
                if (name != "Bomberman" && name != "LadyBomber" && name != "TinyBomber" &&
                    !dynamicFolderNames.Contains(name))
                    dynamicFolderNames.Add(name);
            }
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

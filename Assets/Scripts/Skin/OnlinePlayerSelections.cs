using System;

public static partial class PlayerPersistentStats
{
    public static void ApplyOnlineSelections(SaveData settings)
    {
        if (settings == null) return;
        int[] characters = { settings.player1SelectedCharacter, settings.player2SelectedCharacter,
            settings.player3SelectedCharacter, settings.player4SelectedCharacter, settings.player5SelectedCharacter, settings.player6SelectedCharacter };
        int[] skins = { settings.player1SelectedSkin, settings.player2SelectedSkin, settings.player3SelectedSkin,
            settings.player4SelectedSkin, settings.player5SelectedSkin, settings.player6SelectedSkin };
        for (int id = 1; id <= GameSession.MaxPlayerId; id++)
        {
            var character = BomberSkinResourceCatalog.IsAvailableCharacter((BomberCharacter)characters[id - 1])
                ? (BomberCharacter)characters[id - 1] : BomberCharacter.Bomberman;
            var skin = Enum.IsDefined(typeof(BomberSkin), skins[id - 1])
                ? (BomberSkin)skins[id - 1] : GetDefaultSkinForPlayer(id);
            skin = BomberSkinResourceCatalog.NormalizeGeneratedSkin(character, skin);
            var state = Get(id);
            state.Character = character;
            state.Skin = skin;
            var runtime = GetRuntime(id);
            runtime.Character = character;
            runtime.Skin = skin;
        }
    }
}

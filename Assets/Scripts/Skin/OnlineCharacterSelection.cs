using System;
using Assets.Scripts.Netcode;
using UnityEngine;

public partial class BomberSkinSelectMenu
{
    public int ActiveOnlineSelectionPlayerId => GetCursorInputPlayerId(GetActiveSelectionCursor());
    [Serializable]
    sealed class OnlineCursorView
    {
        public int player;
        public int index;
        public int palette;
        public int character;
        public int skin;
        public bool confirmed;
    }
    [Serializable]
    sealed class OnlineCharacterView { public OnlineCursorView[] players; }

    public string CaptureOnlineView()
    {
        var views = new OnlineCursorView[players.Count];
        for (int i = 0; i < players.Count; i++)
        {
            var p = players[i];
            views[i] = new OnlineCursorView { player = p.playerId, index = p.index, palette = p.selectedPaletteIndex,
                character = (int)p.selectedCharacter, skin = (int)p.selected, confirmed = p.confirmed };
        }
        return JsonUtility.ToJson(new OnlineCharacterView { players = views });
    }

    void ApplyOnlineCursorView()
    {
        if (string.IsNullOrEmpty(BombermanNetworkManager.CharacterView)) return;
        var view = JsonUtility.FromJson<OnlineCharacterView>(BombermanNetworkManager.CharacterView);
        if (view?.players == null) return;
        if (selectedBySlot != null) Array.Clear(selectedBySlot, 0, selectedBySlot.Length);
        foreach (var entry in view.players)
        {
            var p = players.Find(value => value.playerId == entry.player);
            if (p == null) continue;
            bool newlyConfirmed = entry.confirmed && !p.confirmed;
            p.index = entry.index;
            p.selectedPaletteIndex = entry.palette;
            p.selectedCharacter = (BomberCharacter)entry.character;
            p.selected = (BomberSkin)entry.skin;
            p.confirmed = entry.confirmed;
            p.selectedIndex = entry.confirmed ? entry.index : -1;
            if (entry.confirmed && selectedBySlot != null && entry.index >= 0 && entry.index < selectedBySlot.Length)
                selectedBySlot[entry.index] = entry.player;
            if (newlyConfirmed) StartEndStageForCursor(p);
            if (!entry.confirmed) StopEndStageForCursor(p);
            var state = PlayerPersistentStats.Get(entry.player);
            state.Character = p.selectedCharacter;
            state.Skin = p.selected;
        }
        UpdateSlotVisuals();
        UpdateSelectedSkinsListVisuals();
        UpdateAllCursorsToSelected();
        UpdateFooterHintVisual();
    }
}

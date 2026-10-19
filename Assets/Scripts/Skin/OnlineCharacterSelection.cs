using System;
using Assets.Scripts.Netcode;
using UnityEngine;

public partial class BomberSkinSelectMenu
{
    public int ActiveOnlineSelectionPlayerId => GetCursorInputPlayerId(GetActiveSelectionCursor());
    int onlineSoundSequence, onlineSoundIndex, appliedOnlineSoundSequence;
    float onlineSoundVolume;
    AudioClip[] OnlineSounds => new[] { moveCursorSfx, confirmSfx, lockedConfirmSfx, returnSfx, changePaletteSfx };
    void RecordOnlineSound(AudioClip clip, float volume)
    {
        if (!Assets.Scripts.Netcode.NetSync.IsServer || clip == null) return;
        int index = System.Array.IndexOf(OnlineSounds, clip);
        if (index < 0) return;
        onlineSoundIndex = index; onlineSoundVolume = volume; onlineSoundSequence++;
    }
    void ApplyOnlineSound(int sequence, int index, float volume)
    {
        if (sequence == appliedOnlineSoundSequence) return;
        appliedOnlineSoundSequence = sequence;
        if (sequence > 0 && index >= 0 && index < OnlineSounds.Length)
            PlaySfx(OnlineSounds[index], volume);
    }

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
    sealed class OnlineCharacterView { public OnlineCursorView[] players; public int soundSequence, soundIndex; public float soundVolume, fade; public bool fadeVisible; }

    public string CaptureOnlineView()
    {
        var views = new OnlineCursorView[players.Count];
        for (int i = 0; i < players.Count; i++)
        {
            var p = players[i];
            views[i] = new OnlineCursorView { player = p.playerId, index = p.index, palette = p.selectedPaletteIndex,
                character = (int)p.selectedCharacter, skin = (int)p.selected, confirmed = p.confirmed };
        }
        return JsonUtility.ToJson(new OnlineCharacterView { players = views, soundSequence = onlineSoundSequence, soundIndex = onlineSoundIndex, soundVolume = onlineSoundVolume, fade = fadeImage != null ? fadeImage.color.a : 0, fadeVisible = fadeImage != null && fadeImage.gameObject.activeSelf });
    }

    string appliedOnlineCharacterJson;

    void ApplyOnlineCursorView()
    {
        if (string.IsNullOrEmpty(BombermanNetworkManager.CharacterView)) return;
        string json = BombermanNetworkManager.CharacterView;
        if (json == appliedOnlineCharacterJson) return;
        var view = JsonUtility.FromJson<OnlineCharacterView>(json);
        if (view?.players == null) return;
        appliedOnlineCharacterJson = json;
        ApplyOnlineSound(view.soundSequence, view.soundIndex, view.soundVolume);
        if (fadeInCoroutine != null) { StopCoroutine(fadeInCoroutine); fadeInCoroutine = null; }
        if (fadeImage != null)
        {
            fadeImage.gameObject.SetActive(view.fadeVisible); SetFadeAlpha(view.fade);
            if (view.fadeVisible) fadeImage.transform.SetAsLastSibling();
        }
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

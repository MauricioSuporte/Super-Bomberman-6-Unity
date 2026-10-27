using System;
using System.Collections;
using Assets.Scripts.Netcode;
using UnityEngine;

public sealed partial class BattleModeMenu
{
    [Serializable]
    public sealed class OnlineView
    {
        public string step;
        public bool characters;
        public int matchMode;
        public BattleModePlayerControlMode[] modes;
        public int selectedPlayer;
        public int stage;
        public int wins;
        public int timer;
        public int difficulty;
        public int suddenDeath;
        public bool revenge;
        public int shuffle;
        public int[] items;
        public int[] louies;
        public SaveData.BattleModeHandicapSave handicap;
        public BattleModeRules.TeamId[] teams;
        public bool[] teamAssigned;
        public int teamPlayer, teamIndex, ruleIndex, itemIndex, louieIndex, musicIndex, musicMask, specificIndex, handicapRow, handicapColumn;
        public bool cursorConfirmed, startConfirmed, restoreVisible;
        public int restoreIndex;
        public int carouselFrom, carouselDirection;
        public float carouselElapsed;
        public bool carouselAnimating;
        public bool fadeVisible, advanceAnimating;
        public float fadeAlpha;
        public int advanceFrame, soundSequence, soundIndex;
        public float soundVolume;
        public Vector2 startCursorPosition;
        public int startCursorFrame;
        public bool startVisible;
        public float startBlinkTimer;
    }

    public bool OnlineCharacterSelectionActive => state == MenuState.SkinSelect;

    public string CaptureOnlineView()
    {
        return JsonUtility.ToJson(new OnlineView { step = state.ToString(), characters = OnlineCharacterSelectionActive,
            matchMode = (int)SelectedMatchMode, modes = playerModes, selectedPlayer = selectedPlayerIndex + 1,
            stage = selectedStageIndex, wins = ruleBattlesToWin, timer = (int)ruleTimerMode,
            difficulty = (int)ruleComputerLevel, suddenDeath = (int)ruleSuddenDeath, revenge = ruleRevengeBomber, shuffle = ruleShufflePlayers,
            items = workingBattleItemAmounts, louies = workingBattleLouieAmounts,
            handicap = workingBattleHandicap, teams = workingTeams, teamAssigned = teamAssigned,
            teamPlayer = currentTeamPlayerIndex, teamIndex = selectedTeamIndex, ruleIndex = selectedRuleIndex,
            itemIndex = selectedItemIndex, louieIndex = selectedLouieIndex, musicIndex = selectedMusicIndex,
            musicMask = workingBattleMusicSelectionMask, specificIndex = selectedSpecificSettingIndex,
            handicapRow = selectedHandicapRow, handicapColumn = selectedHandicapColumn,
            cursorConfirmed = cursorConfirmVisual, startConfirmed = specificStartConfirmed,
            restoreVisible = restoreDefaultsRoot != null && restoreDefaultsRoot.gameObject.activeSelf,
            restoreIndex = onlineRestoreIndex, carouselFrom = stageCarouselFromIndex,
            carouselDirection = stageCarouselDirection, carouselElapsed = stageCarouselElapsed,
            carouselAnimating = stageCarouselAnimating,
            fadeVisible = fadeImage != null && fadeImage.gameObject.activeSelf,
            fadeAlpha = fadeImage != null ? fadeImage.color.a : 0f,
            advanceAnimating = optionCursorAdvanceAnimating,
            advanceFrame = leftPanel != null && leftPanel.CursorRenderer != null ? leftPanel.CursorRenderer.CurrentFrame : 0,
            soundSequence = onlineSoundSequence, soundIndex = onlineSoundIndex, soundVolume = onlineSoundVolume,
            startCursorPosition = specificConfirmedCursorPosition,
            startCursorFrame = specificCursorRenderer != null ? specificCursorRenderer.CurrentFrame : 0,
            startVisible = battleStartVisible, startBlinkTimer = battleStartBlinkTimer });
    }

    int onlineRestoreIndex;
    int onlineSoundSequence, onlineSoundIndex, appliedOnlineSoundSequence;
    float onlineSoundVolume;

    AudioClip[] OnlineSounds => new[] { moveCursorSfx, confirmSfx, returnSfx, deniedSfx, null, restoreDefaultSfx };

    void RecordOnlineSound(AudioClip clip, float volume)
    {
        if (!NetSync.IsServer) return;
        int index = Array.IndexOf(OnlineSounds, clip);
        if (index < 0) return;
        onlineSoundIndex = index;
        onlineSoundVolume = volume;
        onlineSoundSequence++;
    }

    void ApplyOnlineFeedback(OnlineView view, bool starting)
    {
        if (view.soundSequence != appliedOnlineSoundSequence)
        {
            appliedOnlineSoundSequence = view.soundSequence;
            if (view.soundSequence > 0 && view.soundIndex >= 0 && view.soundIndex < OnlineSounds.Length)
                PlaySfx(OnlineSounds[view.soundIndex], view.soundVolume);
        }
        if (starting)
        {
            GameMusicController.Instance?.StopMusic();
            battleStartBlinkTimer = 0f;
            battleStartVisible = false;
            StartSpecificSettingsCursorConfirmAnimation();
        }
        if (view.startConfirmed)
        {
            battleStartVisible = view.startVisible;
            battleStartBlinkTimer = view.startBlinkTimer;
            LayoutBattleStartImage();
            if (specificCursorRenderer != null)
            {
                specificCursorRenderer.SetManualAnimationUpdate(true);
                specificCursorRenderer.CurrentFrame = view.startCursorFrame;
                specificCursorRenderer.RefreshFrame();
            }
        }
        var cursor = leftPanel != null ? leftPanel.CursorRenderer : null;
        if (cursor != null)
        {
            cursor.SetManualAnimationUpdate(view.advanceAnimating);
            if (view.advanceAnimating)
            {
                cursor.CurrentFrame = view.advanceFrame;
                cursor.RefreshFrame();
            }
        }
        if (fadeImage != null)
        {
            fadeImage.gameObject.SetActive(view.fadeVisible);
            SetFadeAlpha(view.fadeAlpha);
            if (view.fadeVisible) BringFadeImageToFront();
        }
    }

    OnlineView appliedOnlineMenuView;
    static string parsedOnlineMenuJson;
    static OnlineView parsedOnlineMenuView;

    public void ApplyOnlineMenuView(OnlineView view)
    {
        if (view == null || !Enum.TryParse(view.step, out MenuState next)) return;
        if (ReferenceEquals(appliedOnlineMenuView, view) && menuActive) return;
        bool restoreChanged = appliedOnlineMenuView == null || appliedOnlineMenuView.restoreVisible != view.restoreVisible;
        appliedOnlineMenuView = view;
        bool changed = state != next || !menuActive;
        bool starting = view.startConfirmed && !specificStartConfirmed;
        state = next;
        leftPanel?.SetCursorVisibilitySuppressed(next != MenuState.MatchMode && next != MenuState.PlayerSelect);
        selectedIndex = view.matchMode;
        selectedPlayerIndex = view.selectedPlayer - 1;
        playerModes = view.modes ?? SaveSystem.GetBattleModePlayerControlModes();
        workingTeams = view.teams;
        teamAssigned = view.teamAssigned;
        currentTeamPlayerIndex = view.teamPlayer;
        selectedTeamIndex = view.teamIndex;
        selectedRuleIndex = view.ruleIndex;
        ruleComputerLevel = (BattleModeComputerLevel)view.difficulty;
        ruleBattlesToWin = view.wins;
        ruleTimerMode = (BattleModeRules.RoundTimerMode)view.timer;
        ruleSuddenDeath = (BattleModeSuddenDeathSetting)view.suddenDeath;
        ruleRevengeBomber = view.revenge;
        ruleShufflePlayers = Mathf.Clamp(view.shuffle, 0, 2);
        selectedStageIndex = view.stage;
        stageCarouselFromIndex = view.carouselFrom;
        stageCarouselDirection = view.carouselDirection;
        stageCarouselElapsed = view.carouselElapsed;
        stageCarouselAnimating = view.carouselAnimating;
        workingBattleItemAmounts = view.items;
        workingBattleLouieAmounts = view.louies;
        workingBattleHandicap = view.handicap;
        selectedItemIndex = view.itemIndex;
        selectedLouieIndex = view.louieIndex;
        selectedMusicIndex = view.musicIndex;
        workingBattleMusicSelectionMask = view.musicMask;
        selectedSpecificSettingIndex = view.specificIndex;
        selectedHandicapRow = view.handicapRow;
        selectedHandicapColumn = view.handicapColumn;
        cursorConfirmVisual = view.cursorConfirmed;
        specificStartConfirmed = view.startConfirmed;
        specificConfirmedCursorPosition = view.startCursorPosition;
        if (changed || restoreChanged)
        {
            foreach (var panel in new[] { teamSelectRoot, ruleConfigRoot, stageSelectRoot, specificSettingsRoot,
                musicSelectRoot, itemSelectRoot, louieSelectRoot, handicapSelectRoot, restoreDefaultsRoot })
                if (panel != null) panel.gameObject.SetActive(false);
            if (leftPanel != null) leftPanel.gameObject.SetActive(false);
        }
        switch (state)
        {
            case MenuState.MatchMode:
                if (changed) BuildMatchModeMenu();
                if (leftPanel != null) leftPanel.gameObject.SetActive(true);
                break;
            case MenuState.PlayerSelect:
                if (changed) BuildPlayerSelectMenu();
                if (leftPanel != null) leftPanel.gameObject.SetActive(true);
                RefreshPlayerSelectEntries();
                break;
            case MenuState.TeamSelect:
                BuildTeamSelectionPlayerIds();
                teamAssigned ??= new bool[GameSession.MaxPlayerId + 1];
                workingTeams ??= SaveSystem.GetBattleModePlayerTeams();
                teamCelebrationTimers ??= new float[GameSession.MaxPlayerId + 1];
                teamCelebrationCompleted ??= new bool[GameSession.MaxPlayerId + 1];
                EnsureTeamSelectBuilt();
                UpdateTeamSelectVisuals(true);
                break;
            case MenuState.RuleConfig:
                SyncRuleConfigLayoutFromPlayerSelect();
                EnsureRuleConfigBuilt(); UpdateRuleConfigVisuals();
                break;
            case MenuState.StageSelect:
                EnsureStageSelectBuilt(); UpdateStageSelectVisuals();
                break;
            case MenuState.SpecificSettings:
                EnsureSpecificSettingsBuilt(); UpdateSpecificSettingsVisuals();
                break;
            case MenuState.MusicSelect:
                EnsureMusicSelectBuilt(); UpdateMusicSelectVisuals();
                break;
            case MenuState.ItemSelect:
                RefreshItemSelectEntryOrder(); EnsureItemSelectBuilt(); UpdateItemSelectVisuals();
                break;
            case MenuState.LouieSelect:
                EnsureLouieSelectBuilt(); UpdateLouieSelectVisuals();
                break;
            case MenuState.HandicapSelect:
                EnsureHandicapSelectBuilt(); UpdateHandicapSelectVisuals();
                break;
        }
        if (view.restoreVisible)
        {
            restoreDefaultsQuestion = state == MenuState.ItemSelect ? GameTextDatabase.BattleModeMenu.RestoreItemsDefaultsQuestion :
                state == MenuState.LouieSelect ? GameTextDatabase.BattleModeMenu.RestoreLouiesDefaultsQuestion :
                GameTextDatabase.BattleModeMenu.RestoreHandicapDefaultsQuestion;
            EnsureRestoreDefaultsConfirmationBuilt();
            var panel = state == MenuState.ItemSelect ? itemSelectRoot : state == MenuState.LouieSelect ? louieSelectRoot : handicapSelectRoot;
            if (panel != null) panel.gameObject.SetActive(false);
            restoreDefaultsRoot.gameObject.SetActive(true);
            restoreDefaultsRoot.SetAsLastSibling();
            UpdateRestoreDefaultsConfirmationVisuals(view.restoreIndex);
        }
        menuActive = true;
        UpdateOptionVisuals(); UpdatePromptTitle();
        if (changed) { ResetBackgroundSpriteSwap(); ApplyCurrentBackgroundSprite(true); ApplyDynamicScaleIfNeeded(true); }
        ApplyOnlineFeedback(view, starting);
    }

    public static OnlineView ReadOnlineView()
    {
        string json = BombermanNetworkManager.MenuView;
        if (json != parsedOnlineMenuJson)
        {
            parsedOnlineMenuJson = json;
            parsedOnlineMenuView = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<OnlineView>(json);
        }
        return parsedOnlineMenuView;
    }

    IEnumerator ObserveOnlineMenu()
    {
        root.SetActive(true);
        if (leftPanel != null) leftPanel.gameObject.SetActive(false);
        if (fadeImage != null) fadeImage.gameObject.SetActive(false);
        StartSelectMusic();
        while (BombermanNetworkManager.InConfiguration)
        {
            var view = ReadOnlineView();
            if (view != null && view.characters && skinSelectMenu != null)
            {
                ApplyOnlineMenuView(view);
                menuActive = false;
                leftPanel?.HideCursor();
                yield return skinSelectMenu.SelectSkinRoutine();
                RestoreRootAfterEmbeddedSkinSelect();
                menuActive = false;
            }
            else
            {
                ApplyOnlineMenuView(view);
            }
            yield return null;
        }
    }
}

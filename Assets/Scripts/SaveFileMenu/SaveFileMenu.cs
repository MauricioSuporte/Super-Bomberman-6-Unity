using System.Collections;
using System.Collections.Generic;
using System.IO;
using Assets.Scripts.SaveSystem;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SaveFileMenu : MonoBehaviour
{
    public static bool SelectNewGameOnNextOpen { get; set; }

    private enum MenuState
    {
        Main = 0,
        SelectNewGameSlot = 1,
        SelectContinueSlot = 2,
        SelectDeleteSlot = 3,
        SelectNewGameDifficulty = 4
    }

    [System.Serializable]
    private sealed class BackgroundSet
    {
        public Sprite[] sprites = new Sprite[2];
    }

    private sealed class SaveSlotInfo
    {
        public int SlotIndex;
        public bool Exists;
        public int RegisteredStageCount;
        public int ClearedStageCount;
        public int CompletionPercent;
        public NormalGameDifficulty Difficulty;
    }

    [Header("UI Root")]
    [SerializeField] private GameObject root;
    [SerializeField] private Image backgroundImage;

    [Header("Reference Frame")]
    [SerializeField] private RectTransform referenceRect;

    [Header("Fade")]
    [SerializeField] private Image fadeImage;
    [SerializeField, Min(0.01f)] private float fadeDuration = 0.5f;

    [Header("Flow")]
    [SerializeField] private bool useWorldMapAfterSelection = true;
    [SerializeField] private string worldMapSceneName = "WorldMap";
    [SerializeField] private string titleSceneName = "TitleScreen";
    [SerializeField] private string backSceneName = "SkinSelect";

    [Header("Prompt Title (optional)")]
    [SerializeField] private TextMeshProUGUI promptTitleText;
    [SerializeField] private string mainPrompt = "NORMAL GAME";
    [SerializeField] private string newGamePrompt = "START WHICH FILE?";
    [SerializeField] private string difficultyPrompt = "SELECT DIFFICULTY";
    [SerializeField] private string continuePrompt = "CONTINUE WHICH FILE?";
    [SerializeField] private string deletePrompt = "DELETE WHICH FILE?";

    [Header("Options Panel")]
    [SerializeField] private SaveFileMenuOptions leftPanel;

    [Header("Save Slots")]
    [SerializeField, Min(1)] private int slotCount = 3;
    [SerializeField] private string slotLabelPrefix = "File ";
    [SerializeField] private string emptySlotDisplay = "- - -";
    [SerializeField, Min(0)] private int progressColumnSpacing = 1;
    [SerializeField] private float deleteFeedbackSeconds = 0.5f;

    [Header("Animated Backgrounds")]
    [SerializeField] private BackgroundSet mainMenuBackgrounds = new();
    [SerializeField] private BackgroundSet newGameBackgrounds = new();
    [SerializeField] private BackgroundSet difficultyBackgrounds = new();
    [SerializeField] private BackgroundSet continueBackgrounds = new();
    [SerializeField] private BackgroundSet deleteBackgrounds = new();
    [SerializeField, Min(0.01f)] private float backgroundSwapInterval = 2f;
    [SerializeField] private bool backgroundSwapLoop = true;

    [Header("Music")]
    [SerializeField] private AudioClip selectMusic;
    [SerializeField] private AudioClip selectMusicLoop;
    [SerializeField, Range(0f, 1f)] private float selectMusicVolume = 1f;
    [SerializeField] private bool loopSelectMusic = true;

    [Header("SFX")]
    [SerializeField] private AudioClip moveCursorSfx;
    [SerializeField, Range(0f, 1f)] private float moveCursorSfxVolume = 1f;
    [SerializeField] private AudioClip confirmSfx;
    [SerializeField, Range(0f, 1f)] private float confirmSfxVolume = 1f;
    [SerializeField] private AudioClip returnSfx;
    [SerializeField, Range(0f, 1f)] private float returnSfxVolume = 1f;
    [SerializeField] private AudioClip deniedOptionSfx;
    [SerializeField, Range(0f, 1f)] private float deniedOptionVolume = 1f;

    [Header("Disabled / Feedback Message")]
    [SerializeField] private TextMeshProUGUI disabledOptionText;
    [SerializeField] private string disabledNewGameMessage = "NO EMPTY SLOT";
    [SerializeField] private string disabledContinueMessage = "NO SAVE DATA";
    [SerializeField] private string disabledDeleteMessage = "NO SAVE DATA";
    [SerializeField] private string disabledHardcoreMessage = "CLEAR NORMAL GAME ON HARD";
    [SerializeField] private float disabledMessageShowSeconds = 1.5f;
    [SerializeField] private int disabledMessageFontSize = 22;
    [SerializeField] private Color disabledMessageFaceColor = new Color32(231, 63, 63, 255);
    [SerializeField] private Color disabledMessageOutlineColor = Color.black;
    [SerializeField] private TMP_FontAsset disabledMessageFontAsset;
    [SerializeField] private Material disabledMessageFontMaterialPreset;
    [SerializeField] private bool forceDisabledMessageBold = true;
    [SerializeField] private bool useDisabledMessageOutline = true;
    [SerializeField, Range(0f, 1f)] private float disabledMessageOutlineWidth = 0.35f;
    [SerializeField, Range(0f, 1f)] private float disabledMessageOutlineSoftness = 0f;
    [SerializeField, Range(-1f, 1f)] private float disabledMessageFaceDilate = 0.2f;
    [SerializeField, Range(0f, 1f)] private float disabledMessageFaceSoftness = 0f;
    [SerializeField] private bool enableDisabledMessageUnderlay = true;
    [SerializeField] private Color disabledMessageUnderlayColor = new Color(0f, 0f, 0f, 1f);
    [SerializeField, Range(-1f, 1f)] private float disabledMessageUnderlayDilate = 0.1f;
    [SerializeField, Range(0f, 1f)] private float disabledMessageUnderlaySoftness = 0f;
    [SerializeField, Range(-2f, 2f)] private float disabledMessageUnderlayOffsetX = 0.25f;
    [SerializeField, Range(-2f, 2f)] private float disabledMessageUnderlayOffsetY = -0.25f;
    [SerializeField] private float disabledMessageBottomMargin = 12f;

    [Header("Dynamic Scale (Pixel Perfect SNES)")]
    [SerializeField] private bool dynamicScale = true;
    [SerializeField] private int referenceWidth = 256;
    [SerializeField] private int referenceHeight = 224;
    [SerializeField] private bool useIntegerUpscale = true;
    [SerializeField, Min(1)] private int designUpscale = 4;
    [SerializeField, Min(0.01f)] private float extraScaleMultiplier = 1f;
    [SerializeField, Min(0.01f)] private float minScale = 0.5f;
    [SerializeField, Min(0.01f)] private float maxScale = 10f;

    private readonly List<SaveFileOption> _mainOptions = new()
    {
        SaveFileOption.NewGame,
        SaveFileOption.Continue,
        SaveFileOption.DeleteFile
    };

    private readonly List<string> _displayEntries = new();
    private readonly List<bool> _displayEnabled = new();
    private readonly List<int> _displayDifficultyStyles = new();
    private readonly NormalGameDifficulty[] _newGameDifficulties =
    {
        NormalGameDifficulty.Normal,
        NormalGameDifficulty.Hard,
        NormalGameDifficulty.Hardcore
    };

    private MenuState _state = MenuState.Main;

    private int selectedIndex;
    private bool confirmed;
    private bool menuActive;
    private bool _cursorConfirmVisual;
    private int _pendingNewGameSlotIndex = -1;

    private int backgroundSpriteIndex;
    private float backgroundSwapTimer;
    private MenuState _lastAppliedBackgroundState = (MenuState)(-1);

    private Coroutine fadeInCoroutine;
    private Coroutine disabledMessageRoutine;

    private float _currentUiScale = 1f;
    private int _currentBaseScaleInt = 1;
    private int _lastScreenW = -1;
    private int _lastScreenH = -1;
    private Rect _lastCameraRect;
    private Rect _lastRefPixelRect;

    private RectTransform disabledMessageRect;
    private Material disabledMessageRuntimeMaterial;

    public SaveFileOption SelectedOption
    {
        get
        {
            if (_state != MenuState.Main)
                return SaveFileOption.NewGame;

            if (_mainOptions.Count <= 0)
                return SaveFileOption.NewGame;

            return _mainOptions[Mathf.Clamp(selectedIndex, 0, _mainOptions.Count - 1)];
        }
    }

    public static int ActiveSlotIndex => SaveSystem.Data.activeSlotIndex;
    public static bool HasActiveSlot => ActiveSlotIndex >= 0;

    public static void SaveCurrentProgressToActiveSlot()
    {
        if (!HasActiveSlot)
            return;

        SaveSystem.Save();
    }

    public static void LoadActiveSlotToCurrentProgress()
    {
        if (!HasActiveSlot)
            return;

        StageUnlockProgress.ReloadFromPrefs();
    }

    private void Awake()
    {
        if (root == null)
            root = gameObject;

        ApplyCurrentBackgroundSprite(true);
        EnsureDisabledOptionText();
        UpdatePromptTitle();

        if (root != null)
            root.SetActive(false);
    }

    private void Start()
    {
        StartCoroutine(OpenMenuRoutine());
    }

    private void OnDestroy()
    {
        if (disabledMessageRuntimeMaterial != null)
            Destroy(disabledMessageRuntimeMaterial);
    }

    private void Update()
    {
        if (root != null && root.activeInHierarchy)
        {
            ApplyDynamicScaleIfNeeded(false);
            TickBackgroundSpriteSwap();
        }

        if (!menuActive)
            return;

        if (leftPanel != null)
            leftPanel.UpdateOptionVisuals(selectedIndex, confirmed || _cursorConfirmVisual);
    }

    private IEnumerator OpenMenuRoutine()
    {
        if (root == null)
            root = gameObject;

        root.transform.SetAsLastSibling();
        root.SetActive(true);

        if (fadeImage != null)
        {
            fadeImage.gameObject.SetActive(true);
            fadeImage.transform.SetAsLastSibling();
            SetFadeAlpha(1f);
        }

        EnsureDisabledOptionText();
        HideDisabledMessageImmediate();

        confirmed = false;
        _cursorConfirmVisual = false;
        menuActive = false;

        ResetBackgroundSpriteSwap();
        ApplyCurrentBackgroundSprite(true);

        yield return null;
        Canvas.ForceUpdateCanvases();
        yield return new WaitForEndOfFrame();
        Canvas.ForceUpdateCanvases();

        ApplyDynamicScaleIfNeeded(true);

        if (leftPanel != null)
            leftPanel.Initialize(_currentUiScale);

        BuildMainMenu();

        Canvas.ForceUpdateCanvases();
        yield return null;

        if (leftPanel != null)
            leftPanel.ShowCursor();

        UpdateOptionVisuals();
        StartSelectMusic();

        var input = PlayerInputManager.Instance;

        while (input != null &&
               (input.Get(1, PlayerAction.ActionA) ||
                input.Get(1, PlayerAction.ActionB) ||
                input.Get(1, PlayerAction.Start) ||
                input.Get(1, PlayerAction.MoveUp) ||
                input.Get(1, PlayerAction.MoveDown)))
        {
            yield return null;
        }

        yield return null;

        menuActive = true;

        if (fadeInCoroutine != null)
            StopCoroutine(fadeInCoroutine);

        fadeInCoroutine = StartCoroutine(FadeInRoutine());

        while (!confirmed)
        {
            if (input == null)
            {
                yield return null;
                continue;
            }

            bool moved = false;

            if (input.GetDown(1, PlayerAction.MoveUp))
            {
                selectedIndex = GetPreviousSelectableIndex(selectedIndex);
                moved = true;
            }
            else if (input.GetDown(1, PlayerAction.MoveDown))
            {
                selectedIndex = GetNextSelectableIndex(selectedIndex);
                moved = true;
            }

            if (moved)
            {
                PlaySfx(moveCursorSfx, moveCursorSfxVolume);
                HideDisabledMessageImmediate();
                UpdateOptionVisuals();
            }

            if (input.GetDown(1, PlayerAction.ActionB))
            {
                if (_state != MenuState.Main)
                {
                    PlaySfx(returnSfx, returnSfxVolume);
                    HideDisabledMessageImmediate();
                    _cursorConfirmVisual = false;

                    if (_state == MenuState.SelectNewGameDifficulty)
                        BuildSlotMenu(MenuState.SelectNewGameSlot);
                    else
                        BuildMainMenu();

                    UpdateOptionVisuals();
                    yield return null;
                    continue;
                }

                PlaySfx(returnSfx, returnSfxVolume);
                confirmed = true;
                yield return FadeOutRoutine();
                Hide();
                LoadBackScene();
                yield break;
            }

            if (input.GetDown(1, PlayerAction.ActionA) || input.GetDown(1, PlayerAction.Start))
            {
                if (!IsCurrentSelectionEnabled())
                {
                    PlayDeniedSfx();
                    ShowDisabledMessageForCurrentContext();
                    yield return null;
                    continue;
                }

                yield return HandleCurrentSelection();
                if (confirmed)
                    yield break;
            }

            yield return null;
        }
    }

    private IEnumerator HandleCurrentSelection()
    {
        if (_state == MenuState.Main)
        {
            switch (SelectedOption)
            {
                case SaveFileOption.NewGame:
                    if (!HasAnyAvailableNewGameSlot())
                    {
                        PlayDeniedSfx();
                        ShowDisabledMessage(GameTextDatabase.SaveFile.NoEmptySlot, disabledMessageShowSeconds);
                        yield break;
                    }

                    PlaySfx(confirmSfx, confirmSfxVolume);
                    BuildSlotMenu(MenuState.SelectNewGameSlot);
                    UpdateOptionVisuals();
                    break;

                case SaveFileOption.Continue:
                    if (!HasAnyExistingSlot())
                    {
                        PlayDeniedSfx();
                        ShowDisabledMessage(GameTextDatabase.SaveFile.NoSaveData, disabledMessageShowSeconds);
                        yield break;
                    }

                    PlaySfx(confirmSfx, confirmSfxVolume);
                    BuildSlotMenu(MenuState.SelectContinueSlot);
                    UpdateOptionVisuals();
                    break;

                case SaveFileOption.DeleteFile:
                    if (!HasAnyExistingSlot())
                    {
                        PlayDeniedSfx();
                        ShowDisabledMessage(GameTextDatabase.SaveFile.NoSaveData, disabledMessageShowSeconds);
                        yield break;
                    }

                    PlaySfx(confirmSfx, confirmSfxVolume);
                    BuildSlotMenu(MenuState.SelectDeleteSlot);
                    UpdateOptionVisuals();
                    break;
            }

            yield break;
        }

        int slotZeroBased = selectedIndex;
        int slotDisplayIndex = slotZeroBased + 1;

        switch (_state)
        {
            case MenuState.SelectNewGameSlot:
                PlaySfx(confirmSfx, confirmSfxVolume);

                _pendingNewGameSlotIndex = slotZeroBased;
                BuildDifficultyMenu();
                UpdateOptionVisuals();
                yield break;

            case MenuState.SelectNewGameDifficulty:
                PlaySfx(confirmSfx, confirmSfxVolume);

                int pendingSlotIndex = _pendingNewGameSlotIndex;
                if (pendingSlotIndex < 0 || pendingSlotIndex >= slotCount || SaveSystem.SlotExists(pendingSlotIndex))
                {
                    _pendingNewGameSlotIndex = -1;
                    BuildSlotMenu(MenuState.SelectNewGameSlot);
                    UpdateOptionVisuals();
                    yield break;
                }

                NormalGameDifficulty difficulty = _newGameDifficulties[Mathf.Clamp(selectedIndex, 0, _newGameDifficulties.Length - 1)];

                SaveSystem.DeleteSlot(pendingSlotIndex);
                SaveSystem.SetActiveSlot(pendingSlotIndex);
                SaveSystem.ResetSlot(pendingSlotIndex, difficulty);
                EnsureActiveSlotStageOrderExistsFromBuildSettings();
                StageUnlockProgress.ReloadFromPrefs();

                confirmed = true;
                yield return FadeOutRoutine();
                Hide();
                LoadWorldMapAfterSelection();
                yield break;

            case MenuState.SelectContinueSlot:
                PlaySfx(confirmSfx, confirmSfxVolume);

                SaveSystem.SetActiveSlot(slotZeroBased);
                EnsureActiveSlotStageOrderExistsFromBuildSettings();
                StageUnlockProgress.ReloadFromPrefs();

                confirmed = true;
                yield return FadeOutRoutine();
                Hide();
                LoadWorldMapAfterSelection();
                yield break;

            case MenuState.SelectDeleteSlot:
                PlaySfx(confirmSfx, confirmSfxVolume);

                _cursorConfirmVisual = true;
                UpdateOptionVisuals();

                SaveSystem.DeleteSlot(slotZeroBased);

                if (SaveSystem.Data.activeSlotIndex == slotZeroBased)
                {
                    SaveSystem.Data.activeSlotIndex = -1;
                    SaveSystem.Save();
                }

                yield return new WaitForSecondsRealtime(deleteFeedbackSeconds);

                _cursorConfirmVisual = false;
                HideDisabledMessageImmediate();
                BuildMainMenu();
                UpdateOptionVisuals();
                yield break;
        }
    }

    private void LoadWorldMapAfterSelection()
    {
        GamePauseController.ClearPauseFlag();
        Time.timeScale = 1f;
        GameSession.Instance?.ResetNormalGameLivesSession();

        if (useWorldMapAfterSelection && !string.IsNullOrWhiteSpace(worldMapSceneName))
        {
            SceneManager.LoadScene(worldMapSceneName);
            return;
        }

        if (!string.IsNullOrWhiteSpace(titleSceneName))
            SceneManager.LoadScene(titleSceneName);
    }

    private void LoadBackScene()
    {
        GamePauseController.ClearPauseFlag();
        Time.timeScale = 1f;

        if (!string.IsNullOrWhiteSpace(backSceneName))
        {
            SceneManager.LoadScene(backSceneName);
            return;
        }

        if (!string.IsNullOrWhiteSpace(titleSceneName))
            SceneManager.LoadScene(titleSceneName);
    }

    private void BuildMainMenu()
    {
        _state = MenuState.Main;
        _pendingNewGameSlotIndex = -1;
        _displayEntries.Clear();
        _displayEnabled.Clear();
        _displayDifficultyStyles.Clear();

        for (int i = 0; i < _mainOptions.Count; i++)
        {
            SaveFileOption option = _mainOptions[i];
            _displayEntries.Add(GetMainOptionDisplayName(option));
            _displayEnabled.Add(IsMainOptionEnabled(option));
        }

        if (SelectNewGameOnNextOpen)
        {
            SelectNewGameOnNextOpen = false;

            int newGameIndex = _mainOptions.IndexOf(SaveFileOption.NewGame);
            selectedIndex =
                newGameIndex >= 0 &&
                newGameIndex < _displayEnabled.Count &&
                _displayEnabled[newGameIndex]
                    ? newGameIndex
                    : GetDefaultMainMenuSelectedIndex();
        }
        else
        {
            selectedIndex = GetDefaultMainMenuSelectedIndex();
        }

        ApplyEntriesToPanel();
        UpdatePromptTitle();
        ApplyCurrentBackgroundSprite(true);
    }

    private void BuildSlotMenu(MenuState targetState)
    {
        _state = targetState;
        _displayEntries.Clear();
        _displayEnabled.Clear();
        _displayDifficultyStyles.Clear();

        for (int i = 1; i <= slotCount; i++)
        {
            SaveSlotInfo info = GetSaveSlotInfo(i);
            _displayEntries.Add(BuildSlotDisplayText(info));
            _displayDifficultyStyles.Add(info.Exists ? (int)info.Difficulty : -1);

            bool enabled = targetState switch
            {
                MenuState.SelectNewGameSlot => !info.Exists,
                MenuState.SelectContinueSlot => info.Exists,
                MenuState.SelectDeleteSlot => info.Exists,
                _ => false
            };

            _displayEnabled.Add(enabled);
        }

        selectedIndex = GetFirstSelectableIndex();
        ApplyEntriesToPanel();
        UpdatePromptTitle();
        ApplyCurrentBackgroundSprite(true);
    }

    private void BuildDifficultyMenu()
    {
        _state = MenuState.SelectNewGameDifficulty;
        _displayEntries.Clear();
        _displayEnabled.Clear();
        _displayDifficultyStyles.Clear();

        for (int i = 0; i < _newGameDifficulties.Length; i++)
        {
            NormalGameDifficulty difficulty = _newGameDifficulties[i];
            _displayEntries.Add(GetDifficultyDisplayName(difficulty));
            _displayEnabled.Add(IsNewGameDifficultyUnlocked(difficulty));
        }

        selectedIndex = 0;
        ApplyEntriesToPanel();
        UpdatePromptTitle();
        ApplyCurrentBackgroundSprite(true);
    }

    private void ApplyEntriesToPanel()
    {
        if (leftPanel == null)
            return;

        leftPanel.SetDifficultySelectionMode(_state == MenuState.SelectNewGameDifficulty);
        leftPanel.SetColoredDifficultyColumnMode(
            _state == MenuState.SelectContinueSlot || _state == MenuState.SelectDeleteSlot);
        leftPanel.SetDifficultyColumnStyles(_displayDifficultyStyles);
        leftPanel.SetEntries(_displayEntries, _displayEnabled);
    }

    private string GetMainOptionDisplayName(SaveFileOption option)
    {
        SaveFileMenuText text = GameTextDatabase.SaveFile;
        return option switch
        {
            SaveFileOption.NewGame => text.NewGame,
            SaveFileOption.Continue => text.Continue,
            SaveFileOption.DeleteFile => text.DeleteFile,
            _ => option.ToString()
        };
    }

    private bool IsMainOptionEnabled(SaveFileOption option)
    {
        return option switch
        {
            SaveFileOption.NewGame => HasAnyAvailableNewGameSlot(),
            SaveFileOption.Continue => HasAnyExistingSlot(),
            SaveFileOption.DeleteFile => HasAnyExistingSlot(),
            _ => true
        };
    }

    private bool HasAnyExistingSlot()
    {
        int count = Mathf.Min(slotCount, 3);

        for (int i = 0; i < count; i++)
        {
            if (SaveSystem.SlotExists(i))
                return true;
        }

        return false;
    }

    private bool HasAnyAvailableNewGameSlot()
    {
        int count = Mathf.Min(slotCount, 3);

        for (int i = 0; i < count; i++)
        {
            if (!SaveSystem.SlotExists(i))
                return true;
        }

        return false;
    }

    private bool IsCurrentSelectionEnabled()
    {
        if (selectedIndex < 0 || selectedIndex >= _displayEnabled.Count)
            return false;

        return _displayEnabled[selectedIndex];
    }

    private int GetDefaultMainMenuSelectedIndex()
    {
        bool hasExistingSlot = HasAnyExistingSlot();

        if (hasExistingSlot)
        {
            int continueIndex = _mainOptions.IndexOf(SaveFileOption.Continue);
            if (continueIndex >= 0 &&
                continueIndex < _displayEnabled.Count &&
                _displayEnabled[continueIndex])
            {
                return continueIndex;
            }
        }

        return GetFirstSelectableIndex();
    }

    private int GetFirstSelectableIndex()
    {
        for (int i = 0; i < _displayEnabled.Count; i++)
        {
            if (_displayEnabled[i])
                return i;
        }

        return Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, _displayEnabled.Count - 1));
    }

    private int GetNextSelectableIndex(int currentIndex)
    {
        if (_displayEnabled.Count <= 0)
            return 0;

        int start = Mathf.Clamp(currentIndex, 0, _displayEnabled.Count - 1);
        if (_state == MenuState.SelectNewGameDifficulty)
            return (start + 1) % _displayEnabled.Count;

        for (int step = 1; step <= _displayEnabled.Count; step++)
        {
            int idx = (start + step) % _displayEnabled.Count;
            if (_displayEnabled[idx])
                return idx;
        }

        return start;
    }

    private int GetPreviousSelectableIndex(int currentIndex)
    {
        if (_displayEnabled.Count <= 0)
            return 0;

        int start = Mathf.Clamp(currentIndex, 0, _displayEnabled.Count - 1);
        if (_state == MenuState.SelectNewGameDifficulty)
            return (start - 1 + _displayEnabled.Count) % _displayEnabled.Count;

        for (int step = 1; step <= _displayEnabled.Count; step++)
        {
            int idx = (start - step + _displayEnabled.Count) % _displayEnabled.Count;
            if (_displayEnabled[idx])
                return idx;
        }

        return start;
    }

    private void UpdatePromptTitle()
    {
        if (promptTitleText == null)
            return;

        SaveFileMenuText text = GameTextDatabase.SaveFile;
        LocalizedTmpFontFallback.Apply(promptTitleText);
        promptTitleText.text = _state switch
        {
            MenuState.SelectNewGameSlot => text.NewGamePrompt,
            MenuState.SelectNewGameDifficulty => text.DifficultyPrompt,
            MenuState.SelectContinueSlot => text.ContinuePrompt,
            MenuState.SelectDeleteSlot => text.DeletePrompt,
            _ => text.MainPrompt
        };
    }

    private string BuildSlotDisplayText(SaveSlotInfo info)
    {
        SaveFileMenuText text = GameTextDatabase.SaveFile;
        string fileText = $"{text.SlotLabelPrefix}{info.SlotIndex}";
        string progressText = info.Exists
            ? $"{Mathf.Clamp(info.CompletionPercent, 0, 200):000}%"
            : text.EmptySlot;

        int spacing = Mathf.Min(Mathf.Max(0, progressColumnSpacing), 1);
        return $"{fileText}{new string(' ', spacing)}{progressText}";
    }

    private string GetDifficultyDisplayName(NormalGameDifficulty difficulty)
    {
        CommonMenuText text = GameTextDatabase.Common;
        return difficulty switch
        {
            NormalGameDifficulty.Normal => text.Normal,
            NormalGameDifficulty.Hard => text.Hard,
            NormalGameDifficulty.Hardcore => text.Hardcore,
            _ => text.Normal
        };
    }

    private bool IsNewGameDifficultyUnlocked(NormalGameDifficulty difficulty)
    {
        return difficulty != NormalGameDifficulty.Hardcore || UnlockProgress.IsHardcoreUnlocked();
    }

    private SaveSlotInfo GetSaveSlotInfo(int slotIndex)
    {
        int zeroBasedIndex = slotIndex - 1;
        var slot = SaveSystem.GetSlot(zeroBasedIndex);

        SaveSlotInfo info = new SaveSlotInfo
        {
            SlotIndex = slotIndex,
            Exists = false,
            RegisteredStageCount = 0,
            ClearedStageCount = 0,
            CompletionPercent = 0,
            Difficulty = NormalGameDifficulty.Normal
        };

        if (slot == null)
            return info;

        info.Exists = SaveSystem.SlotExists(zeroBasedIndex);
        info.RegisteredStageCount = slot.stageOrder != null ? slot.stageOrder.Count : 0;
        info.ClearedStageCount = slot.clearedStages != null ? slot.clearedStages.Count : 0;
        info.Difficulty = System.Enum.IsDefined(typeof(NormalGameDifficulty), slot.difficulty)
            ? (NormalGameDifficulty)slot.difficulty
            : NormalGameDifficulty.Normal;
        info.CompletionPercent = ComputeCompletionPercent(info.RegisteredStageCount, info.ClearedStageCount, info.Difficulty);

        return info;
    }

    private int ComputeCompletionPercent(int totalStages, int clearedCount, NormalGameDifficulty difficulty)
    {
        if (totalStages <= 0)
            return 0;

        int clampedCleared = Mathf.Clamp(clearedCount, 0, totalStages);
        if (clampedCleared >= totalStages)
        {
            return difficulty switch
            {
                NormalGameDifficulty.Hard => 103,
                NormalGameDifficulty.Hardcore => 105,
                _ => 100
            };
        }

        return Mathf.RoundToInt((clampedCleared / (float)totalStages) * 100f);
    }

    private static void EnsureActiveSlotStageOrderExistsFromBuildSettings()
    {
        var slot = SaveSystem.ActiveSlot;
        if (slot == null)
            return;

        if (slot.stageOrder != null && slot.stageOrder.Count > 0)
            return;

        List<string> buildStages = GetStageSceneNamesFromBuildSettings();
        if (buildStages.Count <= 0)
            return;

        slot.stageOrder = buildStages;
        SaveSystem.Save();
    }

    private static List<string> GetStageSceneNamesFromBuildSettings()
    {
        List<string> result = new();

        int sceneCount = SceneManager.sceneCountInBuildSettings;
        for (int i = 0; i < sceneCount; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            if (string.IsNullOrWhiteSpace(path))
                continue;

            string sceneName = Path.GetFileNameWithoutExtension(path);
            if (string.IsNullOrWhiteSpace(sceneName))
                continue;

            if (!sceneName.StartsWith("Stage_"))
                continue;

            if (!result.Contains(sceneName))
                result.Add(sceneName);
        }

        return result;
    }

    private void UpdateOptionVisuals()
    {
        if (leftPanel != null)
            leftPanel.UpdateOptionVisuals(selectedIndex, confirmed || _cursorConfirmVisual);
    }

    private void PlaySfx(AudioClip clip, float volume)
    {
        if (clip == null)
            return;

        GameMusicController music = GameMusicController.Instance;
        if (music == null)
            return;

        music.PlaySfx(clip, volume);
    }

    private void StartSelectMusic()
    {
        GameMusicController music = GameMusicController.Instance;
        if (music == null || selectMusic == null)
            return;

        PreloadSelectMusic();

        if (selectMusicLoop != null)
        {
            music.PlayMusicIntroThenLoop(
                selectMusic,
                selectMusicVolume,
                selectMusicLoop,
                selectMusicVolume);
            return;
        }

        music.PlayMusic(selectMusic, selectMusicVolume, loopSelectMusic);
    }

    private void PreloadSelectMusic()
    {
        if (selectMusic != null && selectMusic.loadState == AudioDataLoadState.Unloaded)
            selectMusic.LoadAudioData();

        if (selectMusicLoop != null && selectMusicLoop.loadState == AudioDataLoadState.Unloaded)
            selectMusicLoop.LoadAudioData();
    }

    private void PlayDeniedSfx()
    {
        PlaySfx(deniedOptionSfx, deniedOptionVolume);
    }

    public void Hide()
    {
        menuActive = false;
        HideDisabledMessageImmediate();
        _cursorConfirmVisual = false;

        if (leftPanel != null)
            leftPanel.HideCursor();

        if (root != null)
            root.SetActive(false);
        else
            gameObject.SetActive(false);
    }

    private void SetFadeAlpha(float a)
    {
        if (fadeImage == null)
            return;

        Color c = fadeImage.color;
        c.a = a;
        fadeImage.color = c;
    }

    private IEnumerator FadeInRoutine()
    {
        if (fadeImage == null)
            yield break;

        float duration = Mathf.Max(0.001f, fadeDuration);
        float t = 0f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float a = 1f - Mathf.Clamp01(t / duration);
            SetFadeAlpha(a);
            yield return null;
        }

        SetFadeAlpha(0f);
        fadeImage.gameObject.SetActive(false);
    }

    private IEnumerator FadeOutRoutine()
    {
        if (fadeImage == null)
            yield break;

        fadeImage.gameObject.SetActive(true);
        fadeImage.transform.SetAsLastSibling();
        SetFadeAlpha(0f);

        float duration = Mathf.Max(0.001f, fadeDuration);
        float t = 0f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(t / duration);
            SetFadeAlpha(a);
            yield return null;
        }

        SetFadeAlpha(1f);
    }

    private void ResetBackgroundSpriteSwap()
    {
        backgroundSpriteIndex = 0;
        backgroundSwapTimer = 0f;
        _lastAppliedBackgroundState = (MenuState)(-1);
    }

    private void TickBackgroundSpriteSwap()
    {
        if (backgroundImage == null)
            return;

        Sprite[] currentSprites = GetCurrentBackgroundSprites();
        if (currentSprites == null || currentSprites.Length == 0)
            return;

        int validCount = GetValidSpriteCount(currentSprites);
        if (validCount <= 0)
            return;

        if (_lastAppliedBackgroundState != _state)
            ApplyCurrentBackgroundSprite(true);

        if (validCount == 1)
        {
            if (backgroundImage.sprite != GetSpriteFromSet(currentSprites, 0))
                backgroundImage.sprite = GetSpriteFromSet(currentSprites, 0);
            return;
        }

        backgroundSwapTimer += Time.unscaledDeltaTime;

        float interval = Mathf.Max(0.01f, backgroundSwapInterval);

        while (backgroundSwapTimer >= interval)
        {
            backgroundSwapTimer -= interval;
            backgroundSpriteIndex++;

            if (backgroundSpriteIndex >= validCount)
            {
                if (backgroundSwapLoop)
                    backgroundSpriteIndex = 0;
                else
                    backgroundSpriteIndex = validCount - 1;
            }
        }

        ApplyCurrentBackgroundSprite(false);
    }

    private void ApplyCurrentBackgroundSprite(bool force)
    {
        if (backgroundImage == null)
            return;

        Sprite[] currentSprites = GetCurrentBackgroundSprites();
        if (currentSprites == null || currentSprites.Length == 0)
            return;

        int validCount = GetValidSpriteCount(currentSprites);
        if (validCount <= 0)
            return;

        int clampedIndex = validCount == 1
            ? 0
            : Mathf.Clamp(backgroundSpriteIndex, 0, validCount - 1);

        Sprite sprite = GetSpriteFromSet(currentSprites, clampedIndex);
        if (sprite == null)
            return;

        if (force || backgroundImage.sprite != sprite)
            backgroundImage.sprite = sprite;

        _lastAppliedBackgroundState = _state;
    }

    private Sprite[] GetCurrentBackgroundSprites()
    {
        return _state switch
        {
            MenuState.SelectNewGameSlot => newGameBackgrounds != null ? newGameBackgrounds.sprites : null,
            MenuState.SelectNewGameDifficulty => difficultyBackgrounds != null ? difficultyBackgrounds.sprites : null,
            MenuState.SelectContinueSlot => continueBackgrounds != null ? continueBackgrounds.sprites : null,
            MenuState.SelectDeleteSlot => deleteBackgrounds != null ? deleteBackgrounds.sprites : null,
            _ => mainMenuBackgrounds != null ? mainMenuBackgrounds.sprites : null
        };
    }

    private int GetValidSpriteCount(Sprite[] sprites)
    {
        if (sprites == null || sprites.Length == 0)
            return 0;

        int count = 0;

        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] != null)
                count++;
        }

        return count;
    }

    private Sprite GetSpriteFromSet(Sprite[] sprites, int index)
    {
        if (sprites == null || sprites.Length == 0)
            return null;

        List<Sprite> validSprites = null;

        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] == null)
                continue;

            validSprites ??= new List<Sprite>(sprites.Length);
            validSprites.Add(sprites[i]);
        }

        if (validSprites == null || validSprites.Count == 0)
            return null;

        index = Mathf.Clamp(index, 0, validSprites.Count - 1);
        return validSprites[index];
    }

    private Canvas GetRootCanvas()
    {
        if (root != null)
            return root.GetComponentInParent<Canvas>();

        return GetComponentInParent<Canvas>();
    }

    private Camera GetMainCameraSafe()
    {
        Camera cam = Camera.main;
        if (cam != null)
            return cam;

        return FindAnyObjectByType<Camera>();
    }

    private Rect GetReferencePixelRect(out string source)
    {
        source = "SCREEN";

        if (referenceRect == null)
            return new Rect(0, 0, Screen.width, Screen.height);

        Canvas canvas = GetRootCanvas();
        Camera cam = null;

        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            cam = canvas.worldCamera != null ? canvas.worldCamera : GetMainCameraSafe();

        Vector3[] corners = new Vector3[4];
        referenceRect.GetWorldCorners(corners);

        Vector2 bl = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
        Vector2 tr = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);

        float xMin = Mathf.Min(bl.x, tr.x);
        float yMin = Mathf.Min(bl.y, tr.y);
        float xMax = Mathf.Max(bl.x, tr.x);
        float yMax = Mathf.Max(bl.y, tr.y);

        float width = xMax - xMin;
        float height = yMax - yMin;

        if (width <= 1f || height <= 1f)
        {
            source = "SCREEN_FALLBACK";
            return new Rect(0, 0, Screen.width, Screen.height);
        }

        source = "REFERENCE_WORLD_CORNERS";
        return new Rect(xMin, yMin, width, height);
    }

    private float ComputeUiScaleForRect(float usedW, float usedH, out int baseScaleInt)
    {
        baseScaleInt = 1;

        if (!dynamicScale)
            return 1f;

        float sx = usedW / Mathf.Max(1f, referenceWidth);
        float sy = usedH / Mathf.Max(1f, referenceHeight);

        float baseScaleRaw = Mathf.Min(sx, sy);
        float baseScaleForUi = useIntegerUpscale ? Mathf.Floor(baseScaleRaw) : baseScaleRaw;
        if (baseScaleForUi < 1f)
            baseScaleForUi = 1f;

        baseScaleInt = Mathf.Max(1, Mathf.RoundToInt(baseScaleForUi));

        float normalized = baseScaleInt / Mathf.Max(1f, designUpscale);
        float ui = normalized * Mathf.Max(0.01f, extraScaleMultiplier);
        ui = Mathf.Clamp(ui, minScale, maxScale);
        return ui;
    }

    private void ApplyDynamicScaleIfNeeded(bool force = false)
    {
        int sw = Screen.width;
        int sh = Screen.height;

        Camera cam = GetMainCameraSafe();
        Rect camRect = cam != null ? cam.rect : new Rect(0, 0, 1, 1);

        string refSource;
        Rect refPx = GetReferencePixelRect(out refSource);

        bool changed =
            force ||
            sw != _lastScreenW ||
            sh != _lastScreenH ||
            camRect != _lastCameraRect ||
            !ApproximatelyRect(refPx, _lastRefPixelRect);

        if (!changed)
            return;

        _lastScreenW = sw;
        _lastScreenH = sh;
        _lastCameraRect = camRect;
        _lastRefPixelRect = refPx;

        _currentUiScale = ComputeUiScaleForRect(refPx.width, refPx.height, out _currentBaseScaleInt);

        if (leftPanel != null)
            leftPanel.SetUiScale(_currentUiScale);

        ApplyDisabledMessageVisualStyle();
    }

    private static bool ApproximatelyRect(Rect a, Rect b)
    {
        return
            Mathf.Abs(a.x - b.x) < 0.01f &&
            Mathf.Abs(a.y - b.y) < 0.01f &&
            Mathf.Abs(a.width - b.width) < 0.01f &&
            Mathf.Abs(a.height - b.height) < 0.01f;
    }

    private void EnsureDisabledOptionText()
    {
        if (root == null)
            return;

        if (disabledOptionText == null)
        {
            GameObject go = new GameObject("DisabledOptionText", typeof(RectTransform));
            go.transform.SetParent(root.transform, false);

            disabledOptionText = go.AddComponent<TextMeshProUGUI>();
            disabledOptionText.raycastTarget = false;
        }

        disabledMessageRect = disabledOptionText.rectTransform;

        disabledMessageRect.anchorMin = new Vector2(0f, 0f);
        disabledMessageRect.anchorMax = new Vector2(1f, 0f);
        disabledMessageRect.pivot = new Vector2(0.5f, 0f);
        disabledMessageRect.anchoredPosition = new Vector2(0f, disabledMessageBottomMargin * _currentUiScale);
        disabledMessageRect.sizeDelta = new Vector2(0f, 0f);
        disabledMessageRect.localScale = Vector3.one;

        ApplyDisabledMessageVisualStyle();

        disabledOptionText.text = string.Empty;
        disabledOptionText.gameObject.SetActive(false);
    }

    private void ShowDisabledMessageForCurrentContext()
    {
        string message = _state switch
        {
            MenuState.Main => GetMainDisabledMessage(),
            MenuState.SelectContinueSlot => GameTextDatabase.SaveFile.NoSaveData,
            MenuState.SelectDeleteSlot => GameTextDatabase.SaveFile.NoSaveData,
            MenuState.SelectNewGameSlot => GameTextDatabase.SaveFile.NoEmptySlot,
            MenuState.SelectNewGameDifficulty => GameTextDatabase.SaveFile.HardcoreLocked,
            _ => GameTextDatabase.SaveFile.NoSaveData
        };

        ShowDisabledMessage(message, disabledMessageShowSeconds);
    }

    private string GetMainDisabledMessage()
    {
        SaveFileOption option = SelectedOption;

        return option switch
        {
            SaveFileOption.NewGame => GameTextDatabase.SaveFile.NoEmptySlot,
            SaveFileOption.Continue => GameTextDatabase.SaveFile.NoSaveData,
            SaveFileOption.DeleteFile => GameTextDatabase.SaveFile.NoSaveData,
            _ => GameTextDatabase.SaveFile.NoSaveData
        };
    }

    private void ShowDisabledMessage(string message, float duration)
    {
        EnsureDisabledOptionText();

        if (disabledOptionText == null)
            return;

        if (disabledMessageRoutine != null)
        {
            StopCoroutine(disabledMessageRoutine);
            disabledMessageRoutine = null;
        }

        ApplyDisabledMessageVisualStyle();

        LocalizedTmpFontFallback.Apply(disabledOptionText);
        disabledOptionText.text = message;
        disabledOptionText.gameObject.SetActive(true);
        disabledOptionText.transform.SetAsLastSibling();

        disabledMessageRoutine = StartCoroutine(HideDisabledMessageRoutine(duration));
    }

    private IEnumerator HideDisabledMessageRoutine(float duration)
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0.05f, duration));
        HideDisabledMessageImmediate();
    }

    private void HideDisabledMessageImmediate()
    {
        if (disabledMessageRoutine != null)
        {
            StopCoroutine(disabledMessageRoutine);
            disabledMessageRoutine = null;
        }

        if (disabledOptionText != null)
            disabledOptionText.gameObject.SetActive(false);
    }

    private void ApplyDisabledMessageVisualStyle()
    {
        if (disabledOptionText == null)
            return;

        if (disabledMessageFontAsset != null)
            disabledOptionText.font = disabledMessageFontAsset;

        disabledOptionText.alignment = TextAlignmentOptions.Center;
        disabledOptionText.textWrappingMode = TextWrappingModes.NoWrap;
        disabledOptionText.overflowMode = TextOverflowModes.Overflow;
        disabledOptionText.extraPadding = true;
        disabledOptionText.fontSize = Mathf.Clamp(Mathf.RoundToInt(disabledMessageFontSize * _currentUiScale), 8, 300);
        disabledOptionText.color = disabledMessageFaceColor;
        disabledOptionText.margin = Vector4.zero;
        disabledOptionText.raycastTarget = false;

        if (forceDisabledMessageBold)
            disabledOptionText.fontStyle |= FontStyles.Bold;
        else
            disabledOptionText.fontStyle &= ~FontStyles.Bold;

        Material runtimeMat = GetOrCreateDisabledMessageRuntimeMaterial();
        ApplyDisabledMessageMaterialStyle(runtimeMat);

        if (runtimeMat != null)
            disabledOptionText.fontMaterial = runtimeMat;

        disabledOptionText.UpdateMeshPadding();
        disabledOptionText.ForceMeshUpdate();
        disabledOptionText.SetVerticesDirty();
    }

    private Material GetOrCreateDisabledMessageRuntimeMaterial()
    {
        if (disabledOptionText == null)
            return null;

        if (disabledMessageRuntimeMaterial != null)
            return disabledMessageRuntimeMaterial;

        Material baseMat = null;

        if (disabledMessageFontMaterialPreset != null)
            baseMat = disabledMessageFontMaterialPreset;
        else if (disabledOptionText.fontSharedMaterial != null)
            baseMat = disabledOptionText.fontSharedMaterial;
        else if (disabledOptionText.font != null)
            baseMat = disabledOptionText.font.material;

        if (baseMat == null)
            return null;

        disabledMessageRuntimeMaterial = new Material(baseMat);
        disabledMessageRuntimeMaterial.name = baseMat.name + "_SaveFileDisabledRuntime";
        return disabledMessageRuntimeMaterial;
    }

    private void ApplyDisabledMessageMaterialStyle(Material mat)
    {
        if (mat == null)
            return;

        TrySetColor(mat, "_FaceColor", disabledMessageFaceColor);

        if (useDisabledMessageOutline)
        {
            TrySetColor(mat, "_OutlineColor", disabledMessageOutlineColor);
            TrySetFloat(mat, "_OutlineWidth", disabledMessageOutlineWidth);
            TrySetFloat(mat, "_OutlineSoftness", disabledMessageOutlineSoftness);
        }
        else
        {
            TrySetFloat(mat, "_OutlineWidth", 0f);
            TrySetFloat(mat, "_OutlineSoftness", 0f);
        }

        TrySetFloat(mat, "_FaceDilate", disabledMessageFaceDilate);
        TrySetFloat(mat, "_FaceSoftness", disabledMessageFaceSoftness);

        if (enableDisabledMessageUnderlay)
        {
            TrySetColor(mat, "_UnderlayColor", disabledMessageUnderlayColor);
            TrySetFloat(mat, "_UnderlayDilate", disabledMessageUnderlayDilate);
            TrySetFloat(mat, "_UnderlaySoftness", disabledMessageUnderlaySoftness);
            TrySetFloat(mat, "_UnderlayOffsetX", disabledMessageUnderlayOffsetX);
            TrySetFloat(mat, "_UnderlayOffsetY", disabledMessageUnderlayOffsetY);
        }
        else
        {
            TrySetFloat(mat, "_UnderlayDilate", 0f);
            TrySetFloat(mat, "_UnderlaySoftness", 0f);
            TrySetFloat(mat, "_UnderlayOffsetX", 0f);
            TrySetFloat(mat, "_UnderlayOffsetY", 0f);
        }
    }

    private static void TrySetFloat(Material mat, string prop, float value)
    {
        if (mat != null && mat.HasProperty(prop))
            mat.SetFloat(prop, value);
    }

    private static void TrySetColor(Material mat, string prop, Color value)
    {
        if (mat != null && mat.HasProperty(prop))
            mat.SetColor(prop, value);
    }
}

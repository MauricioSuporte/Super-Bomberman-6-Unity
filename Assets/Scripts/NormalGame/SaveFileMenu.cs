using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SaveFileMenu : MonoBehaviour
{
    private enum MenuState
    {
        Main = 0,
        SelectNewGameSlot = 1,
        SelectContinueSlot = 2,
        SelectDeleteSlot = 3
    }

    private sealed class SaveSlotInfo
    {
        public int SlotIndex;
        public bool Exists;
        public int RegisteredStageCount;
        public int ClearedStageCount;
        public int PerfectStageCount;
        public int CompletionPercent;
    }

    private const string ActiveSlotKey = "SB6_ActiveSaveSlot";
    private const string LiveUnlockedStagesKey = "SB6_UnlockedStages";
    private const string LiveClearedStagesKey = "SB6_ClearedStages";
    private const string LivePerfectStagesKey = "SB6_PerfectStages";
    private const string LiveStageOrderKey = "SB6_StageOrder";

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

    [Header("Prompt Title (optional)")]
    [SerializeField] private TextMeshProUGUI promptTitleText;
    [SerializeField] private string mainPrompt = "NORMAL GAME";
    [SerializeField] private string newGamePrompt = "START WHICH FILE?";
    [SerializeField] private string continuePrompt = "CONTINUE WHICH FILE?";
    [SerializeField] private string deletePrompt = "DELETE WHICH FILE?";

    [Header("Options Panel")]
    [SerializeField] private SaveFileMenuOptions leftPanel;

    [Header("Save Slots")]
    [SerializeField, Min(1)] private int slotCount = 3;
    [SerializeField] private string slotLabelPrefix = "File ";
    [SerializeField] private string emptySlotDisplay = "- - -";
    [SerializeField] private string noEmptyFileMessage = "NO EMPTY FILE";
    [SerializeField] private string deletedMessage = "FILE DELETED";
    [SerializeField] private float deleteFeedbackSeconds = 1.0f;

    [Header("Background Sprite")]
    [SerializeField] private Sprite[] backgroundSprites = new Sprite[2];
    [SerializeField] private float backgroundSwapInterval = 2f;
    [SerializeField] private bool backgroundSwapLoop = true;

    [Header("Music")]
    [SerializeField] private AudioClip selectMusic;
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
    [SerializeField] private string disabledContinueMessage = "NO SAVE DATA";
    [SerializeField] private string disabledDeleteMessage = "NO SAVE DATA";
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

    private MenuState _state = MenuState.Main;

    private int selectedIndex;
    private bool confirmed;
    private bool menuActive;

    private int backgroundSpriteIndex;
    private float backgroundSwapTimer;

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

    public static int ActiveSlotIndex => PlayerPrefs.GetInt(ActiveSlotKey, -1);

    public static bool HasActiveSlot => ActiveSlotIndex >= 1;

    public static void SaveCurrentProgressToActiveSlot()
    {
        int activeSlot = ActiveSlotIndex;
        if (activeSlot < 1)
            return;

        SaveLiveProgressToSlot(activeSlot);
    }

    public static void LoadActiveSlotToCurrentProgress()
    {
        int activeSlot = ActiveSlotIndex;
        if (activeSlot < 1)
            return;

        LoadSlotIntoLiveProgress(activeSlot);
        StageUnlockProgress.ReloadFromPrefs();
    }

    private void Awake()
    {
        if (root == null)
            root = gameObject;

        ApplyCurrentBackgroundSprite();
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
        if (!menuActive)
            return;

        ApplyDynamicScaleIfNeeded(false);
        TickBackgroundSpriteSwap();

        if (leftPanel != null)
            leftPanel.UpdateOptionVisuals(selectedIndex, confirmed);
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
        menuActive = false;

        ResetBackgroundSpriteSwap();
        ApplyCurrentBackgroundSprite();

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
                    BuildMainMenu();
                    UpdateOptionVisuals();
                    yield return null;
                    continue;
                }

                PlaySfx(returnSfx, returnSfxVolume);
                confirmed = true;
                yield return FadeOutRoutine();
                Hide();
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
                    PlaySfx(confirmSfx, confirmSfxVolume);
                    BuildSlotMenu(MenuState.SelectNewGameSlot);
                    UpdateOptionVisuals();
                    break;

                case SaveFileOption.Continue:
                    if (!HasAnyExistingSlot())
                    {
                        PlayDeniedSfx();
                        ShowDisabledMessage(disabledContinueMessage, disabledMessageShowSeconds);
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
                        ShowDisabledMessage(disabledDeleteMessage, disabledMessageShowSeconds);
                        yield break;
                    }

                    PlaySfx(confirmSfx, confirmSfxVolume);
                    BuildSlotMenu(MenuState.SelectDeleteSlot);
                    UpdateOptionVisuals();
                    break;
            }

            yield break;
        }

        int slotIndex = selectedIndex + 1;

        switch (_state)
        {
            case MenuState.SelectNewGameSlot:
                PlaySfx(confirmSfx, confirmSfxVolume);

                DeleteSlot(slotIndex);
                SetActiveSlot(slotIndex);
                StageUnlockProgress.ResetProgress();
                EnsureLiveStageOrderExistsFromBuildSettings();
                SaveLiveProgressToSlot(slotIndex);
                StageUnlockProgress.ReloadFromPrefs();

                confirmed = true;
                yield return FadeOutRoutine();
                Hide();
                LoadWorldMapAfterSelection();
                yield break;

            case MenuState.SelectContinueSlot:
                PlaySfx(confirmSfx, confirmSfxVolume);

                SetActiveSlot(slotIndex);
                LoadSlotIntoLiveProgress(slotIndex);
                StageUnlockProgress.ReloadFromPrefs();

                confirmed = true;
                yield return FadeOutRoutine();
                Hide();
                LoadWorldMapAfterSelection();
                yield break;

            case MenuState.SelectDeleteSlot:
                PlaySfx(confirmSfx, confirmSfxVolume);

                DeleteSlot(slotIndex);

                if (ActiveSlotIndex == slotIndex)
                    PlayerPrefs.DeleteKey(ActiveSlotKey);

                ShowDisabledMessage(deletedMessage, deleteFeedbackSeconds);

                BuildSlotMenu(MenuState.SelectDeleteSlot);
                UpdateOptionVisuals();
                yield break;
        }
    }

    private void LoadWorldMapAfterSelection()
    {
        GamePauseController.ClearPauseFlag();
        Time.timeScale = 1f;

        if (useWorldMapAfterSelection && !string.IsNullOrWhiteSpace(worldMapSceneName))
        {
            SceneManager.LoadScene(worldMapSceneName);
            return;
        }

        if (!string.IsNullOrWhiteSpace(titleSceneName))
            SceneManager.LoadScene(titleSceneName);
    }

    private void BuildMainMenu()
    {
        _state = MenuState.Main;
        _displayEntries.Clear();
        _displayEnabled.Clear();

        for (int i = 0; i < _mainOptions.Count; i++)
        {
            SaveFileOption option = _mainOptions[i];
            _displayEntries.Add(GetMainOptionDisplayName(option));
            _displayEnabled.Add(IsMainOptionEnabled(option));
        }

        selectedIndex = GetFirstSelectableIndex();
        ApplyEntriesToPanel();
        UpdatePromptTitle();
    }

    private void BuildSlotMenu(MenuState targetState)
    {
        _state = targetState;
        _displayEntries.Clear();
        _displayEnabled.Clear();

        for (int i = 1; i <= slotCount; i++)
        {
            SaveSlotInfo info = GetSaveSlotInfo(i);
            _displayEntries.Add(BuildSlotDisplayText(info));

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
    }

    private void ApplyEntriesToPanel()
    {
        if (leftPanel == null)
            return;

        leftPanel.SetEntries(_displayEntries, _displayEnabled);
    }

    private string GetMainOptionDisplayName(SaveFileOption option)
    {
        return option switch
        {
            SaveFileOption.NewGame => "New Game",
            SaveFileOption.Continue => "Continue",
            SaveFileOption.DeleteFile => "Delete File",
            _ => option.ToString()
        };
    }

    private bool IsMainOptionEnabled(SaveFileOption option)
    {
        return option switch
        {
            SaveFileOption.Continue => HasAnyExistingSlot(),
            SaveFileOption.DeleteFile => HasAnyExistingSlot(),
            _ => true
        };
    }

    private bool HasAnyExistingSlot()
    {
        for (int i = 1; i <= slotCount; i++)
        {
            if (SlotExists(i))
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

        promptTitleText.text = _state switch
        {
            MenuState.SelectNewGameSlot => newGamePrompt,
            MenuState.SelectContinueSlot => continuePrompt,
            MenuState.SelectDeleteSlot => deletePrompt,
            _ => mainPrompt
        };
    }

    private string BuildSlotDisplayText(SaveSlotInfo info)
    {
        string fileText = $"{slotLabelPrefix}{info.SlotIndex}";
        string progressText = info.Exists
            ? $"{Mathf.Clamp(info.CompletionPercent, 0, 200):000}%"
            : emptySlotDisplay;

        return $"{fileText,-8} {progressText}";
    }

    private SaveSlotInfo GetSaveSlotInfo(int slotIndex)
    {
        string unlockedRaw = PlayerPrefs.GetString(GetSlotUnlockedKey(slotIndex), string.Empty);
        string clearedRaw = PlayerPrefs.GetString(GetSlotClearedKey(slotIndex), string.Empty);
        string perfectRaw = PlayerPrefs.GetString(GetSlotPerfectKey(slotIndex), string.Empty);
        string orderRaw = PlayerPrefs.GetString(GetSlotStageOrderKey(slotIndex), string.Empty);

        bool exists = SlotExists(slotIndex);

        SaveSlotInfo info = new SaveSlotInfo
        {
            SlotIndex = slotIndex,
            Exists = exists,
            RegisteredStageCount = 0,
            ClearedStageCount = 0,
            PerfectStageCount = 0,
            CompletionPercent = 0
        };

        if (!exists)
            return info;

        int clearedCount = CountSeparatedEntries(clearedRaw);
        int perfectCount = CountSeparatedEntries(perfectRaw);
        int registeredStageCount = ResolveRegisteredStageCount(slotIndex, orderRaw);

        info.RegisteredStageCount = registeredStageCount;
        info.ClearedStageCount = clearedCount;
        info.PerfectStageCount = perfectCount;
        info.CompletionPercent = ComputeCompletionPercent(registeredStageCount, clearedCount, perfectCount);

        return info;
    }

    private int ResolveRegisteredStageCount(int slotIndex, string slotOrderRaw)
    {
        int slotOrderCount = CountSeparatedEntries(slotOrderRaw);
        if (slotOrderCount > 0)
            return slotOrderCount;

        string liveOrderRaw = PlayerPrefs.GetString(LiveStageOrderKey, string.Empty);
        int liveOrderCount = CountSeparatedEntries(liveOrderRaw);
        if (liveOrderCount > 0)
        {
            EnsureSlotStageOrderRaw(slotIndex, liveOrderRaw);
            return liveOrderCount;
        }

        string buildOrderRaw = BuildStageOrderRawFromBuildSettings();
        int buildOrderCount = CountSeparatedEntries(buildOrderRaw);
        if (buildOrderCount > 0)
        {
            EnsureSlotStageOrderRaw(slotIndex, buildOrderRaw);
            return buildOrderCount;
        }

        return 0;
    }

    private int ComputeCompletionPercent(int totalStages, int clearedCount, int perfectCount)
    {
        if (totalStages <= 0)
            return 0;

        float clearedPercent = (Mathf.Clamp(clearedCount, 0, totalStages) / (float)totalStages) * 100f;
        float perfectPercent = (Mathf.Clamp(perfectCount, 0, totalStages) / (float)totalStages) * 100f;
        return Mathf.RoundToInt(clearedPercent + perfectPercent);
    }

    private int CountSeparatedEntries(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return 0;

        string[] parts = raw.Split('|');
        int count = 0;

        for (int i = 0; i < parts.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(parts[i]))
                count++;
        }

        return count;
    }

    private static void SetActiveSlot(int slotIndex)
    {
        PlayerPrefs.SetInt(ActiveSlotKey, slotIndex);
        PlayerPrefs.Save();
    }

    private static bool SlotExists(int slotIndex)
    {
        if (PlayerPrefs.GetInt(GetSlotExistsKey(slotIndex), 0) == 1)
            return true;

        bool hasAnyData =
            HasNonEmptyKey(GetSlotUnlockedKey(slotIndex)) ||
            HasNonEmptyKey(GetSlotClearedKey(slotIndex)) ||
            HasNonEmptyKey(GetSlotPerfectKey(slotIndex)) ||
            HasNonEmptyKey(GetSlotStageOrderKey(slotIndex));

        if (hasAnyData)
        {
            PlayerPrefs.SetInt(GetSlotExistsKey(slotIndex), 1);
            PlayerPrefs.Save();
            return true;
        }

        return false;
    }

    private static bool HasNonEmptyKey(string key)
    {
        return PlayerPrefs.HasKey(key) && !string.IsNullOrWhiteSpace(PlayerPrefs.GetString(key, string.Empty));
    }

    private static void SaveLiveProgressToSlot(int slotIndex)
    {
        EnsureLiveStageOrderExistsFromBuildSettings();

        CopyStringKey(LiveUnlockedStagesKey, GetSlotUnlockedKey(slotIndex));
        CopyStringKey(LiveClearedStagesKey, GetSlotClearedKey(slotIndex));
        CopyStringKey(LivePerfectStagesKey, GetSlotPerfectKey(slotIndex));
        CopyStringKey(LiveStageOrderKey, GetSlotStageOrderKey(slotIndex));

        PlayerPrefs.SetInt(GetSlotExistsKey(slotIndex), 1);
        PlayerPrefs.Save();
    }

    private static void LoadSlotIntoLiveProgress(int slotIndex)
    {
        EnsureSlotStageOrderExistsFromBuildSettings(slotIndex);

        CopyStringKey(GetSlotUnlockedKey(slotIndex), LiveUnlockedStagesKey);
        CopyStringKey(GetSlotClearedKey(slotIndex), LiveClearedStagesKey);
        CopyStringKey(GetSlotPerfectKey(slotIndex), LivePerfectStagesKey);
        CopyStringKey(GetSlotStageOrderKey(slotIndex), LiveStageOrderKey);

        PlayerPrefs.Save();
    }

    private static void DeleteSlot(int slotIndex)
    {
        PlayerPrefs.DeleteKey(GetSlotUnlockedKey(slotIndex));
        PlayerPrefs.DeleteKey(GetSlotClearedKey(slotIndex));
        PlayerPrefs.DeleteKey(GetSlotPerfectKey(slotIndex));
        PlayerPrefs.DeleteKey(GetSlotStageOrderKey(slotIndex));
        PlayerPrefs.DeleteKey(GetSlotExistsKey(slotIndex));
        PlayerPrefs.Save();
    }

    private static void CopyStringKey(string sourceKey, string targetKey)
    {
        if (PlayerPrefs.HasKey(sourceKey))
            PlayerPrefs.SetString(targetKey, PlayerPrefs.GetString(sourceKey, string.Empty));
        else
            PlayerPrefs.DeleteKey(targetKey);
    }

    private static void EnsureSlotStageOrderRaw(int slotIndex, string orderRaw)
    {
        if (string.IsNullOrWhiteSpace(orderRaw))
            return;

        string slotKey = GetSlotStageOrderKey(slotIndex);
        if (!HasNonEmptyKey(slotKey))
        {
            PlayerPrefs.SetString(slotKey, orderRaw);
            PlayerPrefs.SetInt(GetSlotExistsKey(slotIndex), 1);
            PlayerPrefs.Save();
        }
    }

    private static void EnsureLiveStageOrderExistsFromBuildSettings()
    {
        if (HasNonEmptyKey(LiveStageOrderKey))
            return;

        string orderRaw = BuildStageOrderRawFromBuildSettings();
        if (string.IsNullOrWhiteSpace(orderRaw))
            return;

        PlayerPrefs.SetString(LiveStageOrderKey, orderRaw);
        PlayerPrefs.Save();
    }

    private static void EnsureSlotStageOrderExistsFromBuildSettings(int slotIndex)
    {
        string slotKey = GetSlotStageOrderKey(slotIndex);
        if (HasNonEmptyKey(slotKey))
            return;

        string liveOrderRaw = PlayerPrefs.GetString(LiveStageOrderKey, string.Empty);
        if (!string.IsNullOrWhiteSpace(liveOrderRaw))
        {
            PlayerPrefs.SetString(slotKey, liveOrderRaw);
            PlayerPrefs.SetInt(GetSlotExistsKey(slotIndex), 1);
            PlayerPrefs.Save();
            return;
        }

        string buildOrderRaw = BuildStageOrderRawFromBuildSettings();
        if (string.IsNullOrWhiteSpace(buildOrderRaw))
            return;

        PlayerPrefs.SetString(slotKey, buildOrderRaw);
        PlayerPrefs.SetInt(GetSlotExistsKey(slotIndex), 1);
        PlayerPrefs.Save();
    }

    private static string BuildStageOrderRawFromBuildSettings()
    {
        List<string> stageNames = GetStageSceneNamesFromBuildSettings();
        if (stageNames.Count <= 0)
            return string.Empty;

        return string.Join("|", stageNames);
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

    private static string GetSlotExistsKey(int slotIndex) => $"SB6_SaveSlot_{slotIndex}_Exists";
    private static string GetSlotUnlockedKey(int slotIndex) => $"SB6_UnlockedStages_Slot{slotIndex}";
    private static string GetSlotClearedKey(int slotIndex) => $"SB6_ClearedStages_Slot{slotIndex}";
    private static string GetSlotPerfectKey(int slotIndex) => $"SB6_PerfectStages_Slot{slotIndex}";
    private static string GetSlotStageOrderKey(int slotIndex) => $"SB6_StageOrder_Slot{slotIndex}";

    private void UpdateOptionVisuals()
    {
        if (leftPanel != null)
            leftPanel.UpdateOptionVisuals(selectedIndex, confirmed);
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

        music.PlayMusic(selectMusic, selectMusicVolume, loopSelectMusic);
    }

    private void PlayDeniedSfx()
    {
        PlaySfx(deniedOptionSfx, deniedOptionVolume);
    }

    public void Hide()
    {
        menuActive = false;
        HideDisabledMessageImmediate();

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
    }

    private void TickBackgroundSpriteSwap()
    {
        if (backgroundImage == null)
            return;

        if (backgroundSprites == null || backgroundSprites.Length == 0)
            return;

        if (backgroundSprites.Length == 1)
        {
            if (backgroundImage.sprite != backgroundSprites[0])
                backgroundImage.sprite = backgroundSprites[0];
            return;
        }

        backgroundSwapTimer += Time.unscaledDeltaTime;

        if (backgroundSwapTimer < Mathf.Max(0.01f, backgroundSwapInterval))
            return;

        backgroundSwapTimer = 0f;
        backgroundSpriteIndex++;

        if (backgroundSpriteIndex >= backgroundSprites.Length)
        {
            if (backgroundSwapLoop)
                backgroundSpriteIndex = 0;
            else
                backgroundSpriteIndex = backgroundSprites.Length - 1;
        }

        ApplyCurrentBackgroundSprite();
    }

    private void ApplyCurrentBackgroundSprite()
    {
        if (backgroundImage == null)
            return;

        if (backgroundSprites == null || backgroundSprites.Length == 0)
            return;

        backgroundSpriteIndex = Mathf.Clamp(backgroundSpriteIndex, 0, backgroundSprites.Length - 1);

        if (backgroundSprites[backgroundSpriteIndex] != null)
            backgroundImage.sprite = backgroundSprites[backgroundSpriteIndex];
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

        return FindFirstObjectByType<Camera>();
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
            MenuState.SelectNewGameSlot => noEmptyFileMessage,
            MenuState.SelectContinueSlot => disabledContinueMessage,
            MenuState.SelectDeleteSlot => disabledDeleteMessage,
            _ => disabledContinueMessage
        };

        ShowDisabledMessage(message, disabledMessageShowSeconds);
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
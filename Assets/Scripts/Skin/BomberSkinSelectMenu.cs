using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class BomberSkinSelectMenu : MonoBehaviour
{
    static readonly Vector2 FooterHintItemSelectAnchoredPos = new(0f, -300f);
    static readonly Vector2 FooterHintItemSelectSize = new(900f, 120f);
    const int FooterHintItemSelectFontSize = 24;
    const float FooterHintItemSelectLineSpacing = 18f;
    const float NormalGameFooterHintYOffset = 20f;

    [Header("Auto Fix Layout")]
    [SerializeField] bool forceRootPanelStretchToParent = false;
    [SerializeField] bool forceBackgroundStretchToRootPanel = false;

    [Header("UI")]
    [SerializeField] GameObject root;
    [SerializeField] Image backgroundImage;
    [SerializeField] SaveFileMenuOptions leftPanel;

    [Header("Fade")]
    [SerializeField] Image fadeImage;
    [SerializeField] float fadeDuration = 1f;
    [SerializeField] float fadeOutOnConfirmDuration = 2f;

    [Header("Embedded Flow")]
    [SerializeField] bool useFadeTransitions = true;
    [SerializeField] bool manageSelectMusic = true;
    [SerializeField] bool useBattleModeComAssignment;
    [SerializeField, Min(0f)] float postConfirmHoldSeconds;

    [Header("Grid")]
    [SerializeField] Transform gridRoot;
    [SerializeField] Image skinItemPrefab;
    [SerializeField] int columns = 4;
    [SerializeField] Vector2 cellSize = new(120f, 120f);
    [SerializeField] Vector2 spacing = new(16f, 16f);
    [SerializeField, Min(0.1f)] float skinSpriteScaleMultiplier = 2f;
    [SerializeField] Color lockedTint = new(1f, 1f, 1f, 0.35f);
    [SerializeField] Color normalTint = Color.white;
    [SerializeField] Color selectedTint = Color.white;

    [Header("Input Timing")]
    [SerializeField, Min(0.01f)] float directionalRepeatInitialDelay = 0.22f;
    [SerializeField, Min(0.01f)] float directionalRepeatInterval = 0.12f;

    [Header("Cursor Prefab (RectTransform)")]
    [SerializeField] RectTransform skinCursorPrefab;

    [Header("Cursor Per Player")]
    [SerializeField] bool staggerCursorStartByPlayer = true;

    [Tooltip("Optional: override cursor sprite per player (index 0 = P1, ..., 5 = P6).")]
    [SerializeField] Sprite[] cursorSpriteByPlayer = new Sprite[GameSession.MaxPlayerId];
    [SerializeField] Vector2 cursorPadding = new(18f, 18f);
    [SerializeField] Vector2 cursorSizeMultiplier = new(0.9f, 0.9f);
    [SerializeField] float cursorYOffset = 8f;

    [Header("Cursor Blink (Idle)")]
    [SerializeField] bool cursorBlinkWhileNotConfirmed = true;
    [SerializeField] float cursorBlinkSpeed = 5.5f;
    [SerializeField, Range(0f, 1f)] float cursorBlinkMinAlpha = 0.25f;
    [SerializeField, Range(0f, 1f)] float cursorBlinkMaxAlpha = 1f;

    [Header("Background Sprite")]
    [SerializeField] Sprite[] backgroundSprites = new Sprite[2];
    [SerializeField] float backgroundSwapInterval = 2f;
    [SerializeField] bool backgroundSwapLoop = true;

    int _backgroundSpriteIndex;
    float _backgroundSwapTimer;

    [Header("Resources")]
    [SerializeField] int idleFrameIndex = 2;

    [Header("Preview Animations")]
    [SerializeField] float downFrameTime = 0.22f;
    [SerializeField] float endStageFrameTime = 0.1f;
    [SerializeField, Min(0.01f)] float ladyBomberEndStageSpeedMultiplier = 1.5f;
    [SerializeField] int[] downFrames = new[] { 1, 0, 1, 2, 3, 4, 3, 2 };

    [Header("EndStage Offset + Stop")]
    [SerializeField] float endStageYOffset = 10f;
    [SerializeField] int endStageLoopsToStop = 1;

    [Header("Music")]
    [SerializeField] AudioClip selectMusic;
    [SerializeField] AudioClip selectMusicLoop;
    [SerializeField, Range(0f, 1f)] float selectMusicVolume = 1f;
    [SerializeField] bool loopSelectMusic = true;

    [Header("SFX")]
    [SerializeField] AudioClip moveCursorSfx;
    [SerializeField, Range(0f, 1f)] float moveCursorSfxVolume = 1f;
    [SerializeField] AudioClip confirmSfx;
    [SerializeField, Range(0f, 1f)] float confirmSfxVolume = 1f;
    [SerializeField] AudioClip lockedConfirmSfx;
    [SerializeField, Range(0f, 1f)] float lockedConfirmSfxVolume = 1f;

    [Header("SFX (Return)")]
    [FormerlySerializedAs("backToTitleSfx")]
    [SerializeField] AudioClip returnSfx;
    [FormerlySerializedAs("backToTitleSfxVolume")]
    [SerializeField, Range(0f, 1f)] float returnSfxVolume = 1f;

    [Header("Reference Frame (SafeFrame4x3)")]
    [SerializeField] RectTransform referenceRect;

    [Header("Layout Root")]
    [SerializeField] RectTransform rootPanel;

    [Header("Dynamic Scale (Pixel Perfect SNES)")]
    [SerializeField] bool dynamicScale = true;
    [SerializeField] int referenceWidth = 256;
    [SerializeField] int referenceHeight = 224;
    [SerializeField] bool useIntegerUpscale = true;
    [SerializeField, Min(1)] int designUpscale = 4;
    [SerializeField, Min(0.01f)] float extraScaleMultiplier = 1f;
    [SerializeField, Min(0.01f)] float minScale = 0.5f;
    [SerializeField, Min(0.01f)] float maxScale = 10f;

    [Header("Grid Position")]
    [SerializeField] Vector2 gridBaseAnchoredPos = new(0f, -10f);

    [Header("Unlock Hint Message UI")]
    [SerializeField] TextMeshProUGUI unlockHintText;
    [SerializeField] int unlockHintFontSize = 26;
    [SerializeField] float unlockHintBottomMargin = 18f;

    [Header("Unlock Hint Message Rect")]
    [SerializeField] float unlockHintWidth = 220f;
    [SerializeField] float unlockHintHeight = 48f;
    [SerializeField] float unlockHintMinHeight = 32f;

    [Header("Unlock Hint Message TMP")]
    [SerializeField] TMP_FontAsset unlockHintFontAsset;
    [SerializeField] Material unlockHintFontMaterialPreset;
    [SerializeField] bool forceUnlockHintBold = true;

    [Header("Unlock Hint Message Colors")]
    [SerializeField] Color unlockHintFaceColor = new Color32(231, 63, 48, 255);
    [SerializeField] Color unlockHintOutlineColor = Color.black;

    [Header("Unlock Hint Message TMP Outline")]
    [SerializeField] bool useUnlockHintOutline = true;
    [SerializeField, Range(0f, 1f)] float unlockHintOutlineWidth = 0.35f;
    [SerializeField, Range(0f, 1f)] float unlockHintOutlineSoftness = 0f;

    [Header("Unlock Hint Message TMP Face")]
    [SerializeField, Range(-1f, 1f)] float unlockHintFaceDilate = 0.2f;
    [SerializeField, Range(0f, 1f)] float unlockHintFaceSoftness = 0f;

    [Header("Unlock Hint Message TMP Underlay")]
    [SerializeField] bool enableUnlockHintUnderlay = true;
    [SerializeField] Color unlockHintUnderlayColor = new Color(0f, 0f, 0f, 1f);
    [SerializeField, Range(-1f, 1f)] float unlockHintUnderlayDilate = 0.1f;
    [SerializeField, Range(0f, 1f)] float unlockHintUnderlaySoftness = 0f;
    [SerializeField, Range(-2f, 2f)] float unlockHintUnderlayOffsetX = 0.25f;
    [SerializeField, Range(-2f, 2f)] float unlockHintUnderlayOffsetY = -0.25f;

    [Header("Unlock Hint Position")]
    [SerializeField, Range(0f, 0.25f)] float unlockHintBottomPercentOfReferenceHeight = 0.040179f;

    [Header("Selected Skins List")]
    [SerializeField] RectTransform selectedSkinsListRoot;
    [SerializeField] Vector2 selectedSkinsListBaseAnchoredPos = new(0f, 74f);
    [SerializeField] float selectedSkinEntryBaseSpacing = 12f;
    [Tooltip("Spacing used by the selected skins list when more than four players/cursors are visible. Negative values overlap entries so large player counts can fit inside the 4x3 safe frame.")]
    [SerializeField] float selectedSkinEntryCompactSpacing = -20f;
    const float selectedSkinSpriteYOffset = -8f;
    [SerializeField, Range(0.1f, 1f)] float selectedSkinsListSafeFrameWidthRatio = 0.96f;
    [SerializeField] float selectedSkinCurrentCursorAlpha = 0.45f;
    [SerializeField] float selectedSkinPendingCursorAlpha = 0.25f;
    [SerializeField] float selectedSkinPreviewAlpha = 0.55f;
    [SerializeField, Min(0.05f)] float selectedSkinConfirmAnimationSeconds = 0.85f;

    [Header("Footer Hints")]
    [SerializeField] TextMeshProUGUI footerHintText;
    [SerializeField] TMP_FontAsset footerHintFontAsset;
    [SerializeField] Material footerHintFontMaterialPreset;
    [SerializeField] Vector2 footerHintBaseAnchoredPos = new(0f, -300f);
    [SerializeField] Vector2 footerHintBaseSize = new(900f, 120f);
    [SerializeField] int footerHintFontSize = 24;
    [SerializeField] float footerHintLineSpacing = 18f;

    float _currentUiScale = 1f;
    int _currentBaseScaleInt = 1;

    int _lastScreenW = -1;
    int _lastScreenH = -1;
    Rect _lastRefPixelRect;
    Rect _lastCameraRect;

    Vector2 _baseCellSize;
    Vector2 _baseSpacing;
    Vector2 _baseCursorPadding;
    float _baseCursorYOffset;
    float _baseEndStageYOffset;
    bool _baseValuesCaptured;

    Coroutine fadeInCoroutine;
    public bool ReturnToTitleRequested { get; private set; }
    public bool ResumeBattleModeSelectionFromSavedSkins { get; set; }

    RectTransform unlockHintRect;
    Material unlockHintRuntimeMaterial;
    Material footerHintRuntimeMaterial;
    string _lastUnlockHintMessage = string.Empty;

    [Header("Characters (menu order)")]
    [SerializeField]
    List<BomberCharacter> selectableCharacters = new()
    {
        BomberCharacter.Bomberman,
        BomberCharacter.LadyBomber,
        BomberCharacter.TinyBomber
    };

    [Header("Palettes (L/R order)")]
    [SerializeField]
    List<BomberSkin> selectableSkins = new(BomberSkinResourceCatalog.BombermanSkins);

    [SerializeField] AudioClip changePaletteSfx;
    [SerializeField, Range(0f, 1f)] float changePaletteSfxVolume = 1f;

    // Sprite sheets are loaded only when a character/skin becomes visible.
    // This keeps opening time independent from the total number of registered skins.
    readonly Dictionary<string, Sprite> idleCache = new();
    readonly Dictionary<string, Dictionary<int, Sprite>> sheetFrameCache = new();
    readonly Dictionary<string, bool> generatedSkinAvailabilityCache = new();

    readonly List<RectTransform> slotRoots = new();
    readonly List<Image> slotImages = new();

    AudioClip previousClip;
    float previousVolume;
    bool previousLoop;
    bool capturedPreviousMusic;

    float downTimer;
    int downFrameIdx;

    sealed class EndStageState
    {
        public int slotIndex;
        public BomberCharacter character;
        public BomberSkin skin;
        public float timer;
        public int frameIdx;
        public int loopsDone;
        public bool stopped;
        public Vector2 baseAnchoredPos;
        public bool baseCaptured;
    }

    readonly Dictionary<int, EndStageState> endStageBySlot = new();

    float cursorBlinkT;
    int overlapZTick;

    int[] selectedBySlot;

    sealed class PlayerCursorState
    {
        public int playerId;
        public int inputPlayerId;
        public int index;
        public bool confirmed;
        public BomberCharacter selectedCharacter;
        public BomberSkin selected;
        public int selectedPaletteIndex;
        public int selectedIndex = -1;
        public bool battleComCursor;
        public RectTransform cursorRt;
        public Image cursorImg;
        public float endStageTimer;
        public int endStageFrameIdx;
        public int endStageLoopsDone;
        public bool endStageStopped;
    }

    sealed class SelectedSkinStatusVisual
    {
        public RectTransform root;
        public Image cursorImage;
        public Image image;
    }

    readonly List<PlayerCursorState> players = new();
    readonly List<SelectedSkinStatusVisual> selectedStatusVisuals = new();
    readonly List<int> configuredPlayerIds = new(GameSession.MaxPlayerId);
    readonly List<int> battleSkinTargetPlayerIds = new(GameSession.MaxPlayerId);
    readonly List<int> battleComQueue = new(GameSession.MaxPlayerId);
    readonly bool[,] previousDirectionalHeld = new bool[GameSession.MaxPlayerId + 1, 4];
    readonly float[,] nextDirectionalRepeatTime = new float[GameSession.MaxPlayerId + 1, 4];
    readonly bool[] battleBackReleaseRequired = new bool[GameSession.MaxPlayerId + 1];
    readonly Dictionary<int, List<PlayerCursorState>> battleConfirmedByInput = new();
    int nextBattleComQueueIndex;

    bool menuActive;
    int SelectableSlotCount => selectableCharacters != null ? selectableCharacters.Count : 0;
    int VisibleColumnCount => Mathf.Max(1, Mathf.Min(columns, Mathf.Max(1, SelectableSlotCount)));

    void Awake()
    {
        if (changePaletteSfx == null)
            changePaletteSfx = Resources.Load<AudioClip>("Sounds/Skin/change");

        if (root == null)
            root = gameObject;

        CaptureBaseValuesIfNeeded();
        ApplyAutoFixesIfEnabled();
        ResolveLeftPanel();
        EnsureSelectableCharacters();

        ApplyCurrentBackgroundSprite();

        BuildGrid();
        ApplyDynamicScaleIfNeeded(true);
        EnsureUnlockHintText();
        EnsureFooterHintText();

        if (root != null)
            root.SetActive(false);
    }

    void OnDestroy()
    {
        if (unlockHintRuntimeMaterial != null)
            Destroy(unlockHintRuntimeMaterial);

        if (footerHintRuntimeMaterial != null)
            Destroy(footerHintRuntimeMaterial);
    }

    void Update()
    {
        if (!menuActive)
            return;

        ApplyDynamicScaleIfNeeded(false);

        TickBackgroundSpriteSwap();
        TickCursorBlink();
        TickDownClock();
        TickEndStageClocks();
        UpdateSlotVisuals();
        UpdateSelectedSkinsListVisuals();
        UpdateFooterHintVisual();
        UpdateUnlockHint();

    }

    void LateUpdate()
    {
        if (!menuActive)
            return;

        Canvas.ForceUpdateCanvases();
        UpdateAllCursorsToSelected();
        CycleOverlappedCursors();
        UpdateSelectedSkinsListVisuals();

    }

    public void Hide()
    {
        menuActive = false;

        HideUnlockHintImmediate();

        RestoreAllSlotPositions();
        endStageBySlot.Clear();
        battleConfirmedByInput.Clear();
        battleSkinTargetPlayerIds.Clear();
        battleComQueue.Clear();
        nextBattleComQueueIndex = 0;

        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].cursorRt != null)
                Destroy(players[i].cursorRt.gameObject);
        }

        players.Clear();
        ClearSelectedSkinsListVisuals();
        HideFooterHintImmediate();

        if (gridRoot != null)
            gridRoot.gameObject.SetActive(false);

        if (root != null) root.SetActive(false);
        else gameObject.SetActive(false);
    }

    public IEnumerator SelectSkinRoutine()
    {
        bool resumeFromSavedSkins = ResumeBattleModeSelectionFromSavedSkins;
        ResumeBattleModeSelectionFromSavedSkins = false;

        CanvasGroup temporaryHiddenCanvasGroup = null;
        float previousCanvasGroupAlpha = 1f;
        bool shouldRestoreCanvasGroupAlpha = false;

        if (root == null)
            root = gameObject;

        if (resumeFromSavedSkins && root != null)
        {
            temporaryHiddenCanvasGroup = root.GetComponent<CanvasGroup>();
            if (temporaryHiddenCanvasGroup == null)
                temporaryHiddenCanvasGroup = root.AddComponent<CanvasGroup>();

            previousCanvasGroupAlpha = temporaryHiddenCanvasGroup.alpha;
            temporaryHiddenCanvasGroup.alpha = 0f;
            shouldRestoreCanvasGroupAlpha = true;
        }

        root.transform.SetAsLastSibling();
        root.SetActive(true);

        ApplyAutoFixesIfEnabled();
        ApplyDynamicScaleIfNeeded(true);
        EnsureUnlockHintText();
        HideUnlockHintImmediate();
        HideFooterHintImmediate();

        if (gridRoot != null)
            gridRoot.gameObject.SetActive(true);

        if (useFadeTransitions && fadeImage != null)
        {
            fadeImage.gameObject.SetActive(true);
            fadeImage.transform.SetAsLastSibling();

            if (resumeFromSavedSkins)
            {
                SetFadeAlpha(0f);
                fadeImage.gameObject.SetActive(false);
            }
            else
            {
                SetFadeAlpha(1f);
            }
        }

        ResetBackgroundSpriteSwap();
        ApplyCurrentBackgroundSprite();

        EnsureSelectableCharacters();

        if (slotImages.Count != SelectableSlotCount)
            BuildGrid();

        PlayerPersistentStats.EnsureSessionBooted();

        ReturnToTitleRequested = false;

        downTimer = 0f;
        downFrameIdx = 0;

        cursorBlinkT = 0f;
        overlapZTick = 0;

        RestoreAllSlotPositions();
        endStageBySlot.Clear();

        selectedBySlot = new int[SelectableSlotCount];
        for (int i = 0; i < selectedBySlot.Length; i++)
            selectedBySlot[i] = 0;

        if (manageSelectMusic)
            StartSelectMusic();

        var input = PlayerInputManager.Instance;

        if (useBattleModeComAssignment)
            PopulateBattleModeSkinSelectionIds(configuredPlayerIds);
        else
            PopulateConfiguredPlayerIds(configuredPlayerIds);

        ApplyDynamicScaleIfNeeded(true);

        Canvas.ForceUpdateCanvases();
        if (gridRoot is RectTransform gridRtBefore)
            LayoutRebuilder.ForceRebuildLayoutImmediate(gridRtBefore);
        Canvas.ForceUpdateCanvases();

        BuildPlayerCursors(configuredPlayerIds);
        RebuildSelectedSkinsListVisuals();

        if (!resumeFromSavedSkins)
        {
            for (int i = 0; i < configuredPlayerIds.Count; i++)
            {
                int p = configuredPlayerIds[i];
                while (input != null &&
                       (input.Get(p, PlayerAction.ActionA) ||
                        input.Get(p, PlayerAction.ActionB) ||
                        input.Get(p, PlayerAction.Start) ||
                        input.Get(p, PlayerAction.MoveLeft) ||
                        input.Get(p, PlayerAction.MoveRight) ||
                        input.Get(p, PlayerAction.MoveUp) ||
                        input.Get(p, PlayerAction.MoveDown)))
                {
                    yield return null;
                }
            }
        }

        for (int i = 0; i < players.Count; i++)
            InitializeCursorSelection(players[i]);

        if (resumeFromSavedSkins)
            PreconfirmBattleModeSelectionFromSavedSkins();

        UpdateSlotVisuals();
        UpdateSelectedSkinsListVisuals();
        UpdateFooterHintVisual();
        ApplyFinalEndStageVisualsImmediate();
        UpdateUnlockHint();

        Canvas.ForceUpdateCanvases();
        if (gridRoot is RectTransform gridRtAfter)
            LayoutRebuilder.ForceRebuildLayoutImmediate(gridRtAfter);
        Canvas.ForceUpdateCanvases();

        UpdateSlotVisuals();
        UpdateSelectedSkinsListVisuals();
        UpdateFooterHintVisual();
        ApplyFinalEndStageVisualsImmediate();
        UpdateAllCursorsToSelected();
        CycleOverlappedCursors();

        Canvas.ForceUpdateCanvases();

        if (shouldRestoreCanvasGroupAlpha && temporaryHiddenCanvasGroup != null)
            temporaryHiddenCanvasGroup.alpha = previousCanvasGroupAlpha;

        menuActive = true;
        UpdateFooterHintVisual();
        ResetDirectionalRepeatState();

        if (useFadeTransitions && fadeInCoroutine != null)
            StopCoroutine(fadeInCoroutine);

        if (useFadeTransitions && !resumeFromSavedSkins)
            fadeInCoroutine = StartCoroutine(FadeInRoutine());
        else if (fadeImage != null)
        {
            SetFadeAlpha(0f);
            fadeImage.gameObject.SetActive(false);
        }

        yield return null;

        bool done = false;
        while (!done)
        {
            bool anyReturnToTitle = false;

            if (useBattleModeComAssignment && HandleCompletedBattleModeBackInputs(input))
            {
                PlaySfx(returnSfx, returnSfxVolume);
                UpdateSlotVisuals();
                UpdateSelectedSkinsListVisuals();
                UpdateUnlockHint();
                yield return null;
                continue;
            }

            PlayerCursorState ps = GetActiveSelectionCursor();

            if (ps == null)
            {
                if (AllPlayersConfirmed())
                {
                    fadeDuration = fadeOutOnConfirmDuration;
                    done = true;
                }

                yield return null;
                continue;
            }

            int pid = GetCursorInputPlayerId(ps);

            bool upDown = DirectionalPressed(input, pid, PlayerAction.MoveUp);
            bool downDown = DirectionalPressed(input, pid, PlayerAction.MoveDown);
            bool leftDown = DirectionalPressed(input, pid, PlayerAction.MoveLeft);
            bool rightDown = DirectionalPressed(input, pid, PlayerAction.MoveRight);
            bool aDown = input != null && input.GetDown(pid, PlayerAction.ActionA);
            bool bDown = useBattleModeComAssignment
                ? BattleModeBackPressed(input, pid)
                : input != null && input.GetDown(pid, PlayerAction.ActionB);
            bool lDown = input != null && input.GetDown(pid, PlayerAction.ActionL);
            bool rDown = input != null && input.GetDown(pid, PlayerAction.ActionR);
            bool startDown = input != null && input.GetDown(pid, PlayerAction.Start);

            bool moved = false;
            int nextIndex = ps.index;

            if (leftDown) { nextIndex = MoveLeftWrap(ps.index); moved = true; }
            else if (rightDown) { nextIndex = MoveRightWrap(ps.index); moved = true; }
            else if (upDown) { nextIndex = MoveUpWrap(ps.index); moved = true; }
            else if (downDown) { nextIndex = MoveDownWrap(ps.index); moved = true; }

            bool confirmPressed = startDown || aDown;
            bool backPressed = bDown;

            if (moved)
            {
                if (nextIndex != ps.index)
                {
                    ps.index = nextIndex;
                    ps.selectedCharacter = GetCharacterAtSlot(ps.index);
                    PlaySfx(moveCursorSfx, moveCursorSfxVolume);
                    UpdateSlotVisuals();
                    UpdateSelectedSkinsListVisuals();
                    UpdateUnlockHint();
                }
            }
            else if (lDown || rDown)
            {
                if (CyclePalette(ps, rDown ? 1 : -1))
                {
                    PlaySfx(changePaletteSfx, changePaletteSfxVolume);
                    UpdateSlotVisuals();
                    UpdateSelectedSkinsListVisuals();
                    UpdateUnlockHint();
                }
            }
            else if (backPressed)
            {
                if (HandleSequentialBack(ps))
                {
                    PlaySfx(returnSfx, returnSfxVolume);
                    UpdateSlotVisuals();
                    UpdateSelectedSkinsListVisuals();
                    UpdateUnlockHint();
                    continue;
                }

                anyReturnToTitle = true;
            }
            else if (confirmPressed)
            {
                TryConfirm(ps);
                UpdateSlotVisuals();
                UpdateSelectedSkinsListVisuals();
                UpdateUnlockHint();

                if (AllPlayersConfirmed())
                {
                    fadeDuration = fadeOutOnConfirmDuration;
                    done = true;
                }
            }

            if (anyReturnToTitle)
            {
                ReturnToTitleRequested = true;
                PlaySfx(returnSfx, returnSfxVolume);
                done = true;
            }

            yield return null;
        }

        if (!ReturnToTitleRequested && postConfirmHoldSeconds > 0f)
        {
            float hold = 0f;
            float duration = Mathf.Max(0f, postConfirmHoldSeconds);
            while (hold < duration)
            {
                hold += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        if (fadeInCoroutine != null)
        {
            StopCoroutine(fadeInCoroutine);
            fadeInCoroutine = null;
        }

        if (useFadeTransitions)
            yield return FadeOutRoutine();

        if (manageSelectMusic)
            StopSelectMusicAndRestorePrevious(restorePrevious: ReturnToTitleRequested);

        Hide();

        if (useFadeTransitions && fadeImage != null)
            fadeImage.gameObject.SetActive(false);
    }

    public BomberSkin GetSelectedSkin(int playerId)
    {
        playerId = Mathf.Clamp(playerId, 1, 6);

        for (int i = 0; i < players.Count; i++)
            if (players[i].playerId == playerId)
                return players[i].selected;

        return PlayerPersistentStats.Get(playerId).Skin;
    }

    void EnsureSelectableCharacters()
    {
        selectableCharacters ??= new List<BomberCharacter>();

        selectableCharacters.Clear();
        selectableCharacters.AddRange(BomberSkinResourceCatalog.GetAvailableCharacters());

        selectableSkins ??= new List<BomberSkin>();
        selectableSkins.Clear();
        selectableSkins.AddRange(BomberSkinResourceCatalog.BombermanSkins);
    }

    BomberCharacter GetCharacterAtSlot(int slot)
    {
        if (selectableCharacters == null || selectableCharacters.Count == 0)
            return BomberCharacter.Bomberman;

        return selectableCharacters[Mathf.Clamp(slot, 0, selectableCharacters.Count - 1)];
    }

    BomberSkin GetCursorPalette(PlayerCursorState cursor)
    {
        if (cursor == null)
            return BomberSkin.Palette1;

        if (selectableSkins == null || selectableSkins.Count == 0)
            return BomberSkin.Palette1;

        cursor.selectedPaletteIndex = Mathf.Clamp(cursor.selectedPaletteIndex, 0, selectableSkins.Count - 1);
        cursor.selectedPaletteIndex = FindAvailablePaletteIndex(GetCharacterAtSlot(cursor.index), cursor.selectedPaletteIndex, 1);
        cursor.selected = selectableSkins[cursor.selectedPaletteIndex];
        return cursor.selected;
    }

    int GetPaletteIndex(BomberSkin skin, int playerId)
    {
        if (selectableSkins == null || selectableSkins.Count == 0)
            return 0;

        int index = selectableSkins.IndexOf(skin);
        if (index >= 0)
            return index;

        BomberSkin fallback = PlayerPersistentStats.GetDefaultSkinForPlayer(playerId);
        index = selectableSkins.IndexOf(fallback);
        return index >= 0 ? index : 0;
    }

    bool CyclePalette(PlayerCursorState cursor, int direction)
    {
        if (cursor == null || selectableSkins == null || selectableSkins.Count == 0)
            return false;

        int count = selectableSkins.Count;
        int next = FindAvailablePaletteIndex(GetCharacterAtSlot(cursor.index), cursor.selectedPaletteIndex + direction, direction);

        if (next == cursor.selectedPaletteIndex)
            return false;

        cursor.selectedPaletteIndex = next;
        cursor.selected = selectableSkins[next];
        return true;
    }

    int FindAvailablePaletteIndex(BomberCharacter character, int startIndex, int direction)
    {
        if (selectableSkins == null || selectableSkins.Count == 0)
            return 0;

        int count = selectableSkins.Count;
        int step = direction < 0 ? -1 : 1;
        int index = startIndex % count;
        if (index < 0)
            index += count;

        for (int attempts = 0; attempts < count; attempts++)
        {
            BomberSkin candidate = selectableSkins[index];
            if (IsGeneratedSkinCached(character, candidate))
                return index;

            index = (index + step) % count;
            if (index < 0)
                index += count;
        }

        return 0;
    }

    bool IsGeneratedSkinCached(BomberCharacter character, BomberSkin skin)
    {
        string key = GetSheetCacheKey(character, skin);
        if (!generatedSkinAvailabilityCache.TryGetValue(key, out bool isGenerated))
        {
            isGenerated = BomberSkinResourceCatalog.IsGeneratedSkin(character, skin);
            generatedSkinAvailabilityCache[key] = isGenerated;
        }

        return isGenerated;
    }

    BomberSkin GetPreviewSkinForSlot(int slot)
    {
        PlayerCursorState active = GetActiveSelectionCursor();
        if (active != null && !active.confirmed && active.index == slot)
            return GetCursorPalette(active);

        int owner = GetSelectedOwner(slot);
        if (owner != 0)
            return PlayerPersistentStats.Get(owner).Skin;

        return selectableSkins != null && selectableSkins.Count > 0
            ? selectableSkins[0]
            : BomberSkin.Palette1;
    }

    static string GetSheetCacheKey(BomberCharacter character, BomberSkin skin)
    {
        return character + ":" + skin;
    }

    int[] GetEndStageFrames(BomberCharacter character)
    {
        return PlayerBomberSkinController.GetEndStageFrames(character);
    }

    static bool ShouldLoopEndStage(BomberCharacter character)
    {
        return character == BomberCharacter.LadyBomber;
    }

    int GetEndStageFrame(BomberCharacter character, int frameIndex)
    {
        int[] frames = GetEndStageFrames(character);
        if (frames == null || frames.Length == 0)
            return idleFrameIndex;

        int index = ShouldLoopEndStage(character)
            ? Mathf.Abs(frameIndex) % frames.Length
            : Mathf.Clamp(frameIndex, 0, frames.Length - 1);

        return frames[index];
    }

    float GetEndStageFrameTime(BomberCharacter character)
    {
        float baseTime = Mathf.Max(0.001f, endStageFrameTime);
        if (character != BomberCharacter.LadyBomber)
            return baseTime;

        return baseTime / Mathf.Max(0.01f, ladyBomberEndStageSpeedMultiplier);
    }

    float GetSelectedSkinConfirmFrameTime(BomberCharacter character)
    {
        int[] frames = GetEndStageFrames(character);
        int frameCount = frames != null && frames.Length > 0 ? frames.Length : 1;
        float duration = Mathf.Max(0.05f, selectedSkinConfirmAnimationSeconds);
        return Mathf.Max(0.001f, duration / frameCount);
    }

    public BomberCharacter GetSelectedCharacter(int playerId)
    {
        playerId = Mathf.Clamp(playerId, 1, 6);

        for (int i = 0; i < players.Count; i++)
            if (players[i].playerId == playerId)
                return players[i].selectedCharacter;

        return PlayerPersistentStats.Get(playerId).Character;
    }

    public Sprite GetBattleModeTeamPreviewSprite(int playerId, int frameIndex)
    {
        var state = PlayerPersistentStats.Get(playerId);
        return GetBattleModeTeamPreviewSprite(state.Character, state.Skin, frameIndex);
    }

    public Sprite GetBattleModeTeamPreviewSprite(BomberSkin skin, int frameIndex)
    {
        return GetBattleModeTeamPreviewSprite(BomberCharacter.Bomberman, skin, frameIndex);
    }

    public Sprite GetBattleModeTeamPreviewSprite(BomberCharacter character, BomberSkin skin, int frameIndex)
    {
        int frame = idleFrameIndex;
        if (downFrames != null && downFrames.Length > 0)
            frame = downFrames[Mathf.Abs(frameIndex) % downFrames.Length];

        return GetSpriteByFrame(character, skin, frame) ?? GetIdleSprite(character, skin);
    }

    public Sprite GetBattleModeTeamCelebrationSprite(int playerId, int frameIndex)
    {
        var state = PlayerPersistentStats.Get(playerId);
        return GetBattleModeTeamCelebrationSprite(state.Character, state.Skin, frameIndex);
    }

    public Sprite GetBattleModeTeamCelebrationSprite(BomberSkin skin, int frameIndex)
    {
        return GetBattleModeTeamCelebrationSprite(BomberCharacter.Bomberman, skin, frameIndex);
    }

    public Sprite GetBattleModeTeamCelebrationSprite(BomberCharacter character, BomberSkin skin, int frameIndex)
    {
        int frame = idleFrameIndex;
        int[] frames = GetEndStageFrames(character);
        if (frames != null && frames.Length > 0)
        {
            if (ShouldLoopEndStage(character) && frameIndex == int.MaxValue)
                frameIndex = Mathf.FloorToInt(Time.unscaledTime / GetEndStageFrameTime(character));
            else if (character == BomberCharacter.LadyBomber)
                frameIndex = Mathf.FloorToInt(frameIndex * Mathf.Max(0.01f, ladyBomberEndStageSpeedMultiplier));

            int index = ShouldLoopEndStage(character)
                ? Mathf.Abs(frameIndex) % frames.Length
                : Mathf.Clamp(frameIndex, 0, frames.Length - 1);

            frame = frames[index];
        }

        return GetSpriteByFrame(character, skin, frame) ?? GetIdleSprite(character, skin);
    }

    void TryConfirm(int playerId)
    {
        var ps = GetPlayerState(playerId);
        if (ps == null)
            return;

        TryConfirm(ps);
    }

    void TryConfirm(PlayerCursorState ps)
    {
        if (ps == null)
            return;

        int slot = Mathf.Clamp(ps.index, 0, SelectableSlotCount - 1);
        BomberCharacter selectedCharacter = GetCharacterAtSlot(slot);
        BomberSkin skin = GetCursorPalette(ps);

        if (!UnlockProgress.IsUnlocked(skin))
        {
            PlaySfx(lockedConfirmSfx, lockedConfirmSfxVolume);
            return;
        }

        selectedBySlot[slot] = ps.playerId;

        ps.selectedCharacter = selectedCharacter;
        ps.selected = skin;
        ps.selectedIndex = slot;
        ps.confirmed = true;

        PlayerPersistentStats.Get(ps.playerId).Character = selectedCharacter;
        PlayerPersistentStats.Get(ps.playerId).Skin = skin;
        PlayerPersistentStats.SaveSelectedSkin(ps.playerId);

        if (useBattleModeComAssignment)
            PushBattleConfirmedCursor(ps);

        StartEndStageForCursor(ps);

        if (ps.cursorImg != null)
        {
            var c = ps.cursorImg.color;
            c.a = 1f;
            ps.cursorImg.color = c;
        }

        PlaySfx(confirmSfx, confirmSfxVolume);
    }

    void DeselectPlayer(int playerId)
    {
        var ps = GetPlayerState(playerId);
        if (ps == null)
            return;

        UnconfirmCursor(ps);
    }

    void UnconfirmCursor(PlayerCursorState ps)
    {
        if (ps == null || !ps.confirmed || ps.selectedIndex < 0)
        {
            return;
        }

        int slot = ps.selectedIndex;

        ps.confirmed = false;
        ps.selectedIndex = -1;
        StopEndStageForCursor(ps);
        RebuildSelectedSlotOwner(slot);

    }

    PlayerCursorState GetActiveSelectionCursor()
    {
        return GetLowestUnconfirmedCursor();
    }

    bool IsActiveSelectionCursor(PlayerCursorState ps)
    {
        PlayerCursorState active = GetActiveSelectionCursor();
        return active != null && ReferenceEquals(active, ps);
    }

    bool HandleSequentialBack(PlayerCursorState activeCursor)
    {
        if (activeCursor == null)
            return false;

        if (useBattleModeComAssignment)
        {
            PlayerCursorState previousBattleCursor = GetHighestConfirmedBefore(activeCursor.playerId);
            if (previousBattleCursor == null)
                return false;

            RemoveBattleConfirmedCursorFromStack(previousBattleCursor);
            UnconfirmCursor(previousBattleCursor);

            if (previousBattleCursor.cursorRt != null)
                previousBattleCursor.cursorRt.gameObject.SetActive(true);

            return true;
        }

        PlayerCursorState previous = GetHighestConfirmedBefore(activeCursor.playerId);
        if (previous == null)
            return false;

        UnconfirmCursor(previous);
        return true;
    }

    PlayerCursorState GetHighestConfirmedBefore(int playerId)
    {
        PlayerCursorState best = null;

        for (int i = 0; i < players.Count; i++)
        {
            PlayerCursorState ps = players[i];
            if (ps == null || !ps.confirmed || ps.playerId >= playerId)
                continue;

            if (best == null || ps.playerId > best.playerId)
                best = ps;
        }

        return best;
    }

    PlayerCursorState GetPlayerState(int playerId)
    {
        for (int i = 0; i < players.Count; i++)
            if (players[i].playerId == playerId)
                return players[i];

        return null;
    }

    int GetSelectedOwner(int slotIndex)
    {
        if (selectedBySlot == null || slotIndex < 0 || slotIndex >= selectedBySlot.Length)
            return 0;

        return selectedBySlot[slotIndex];
    }

    bool IsSlotSelected(int slotIndex)
    {
        return GetSelectedOwner(slotIndex) != 0;
    }

    void RebuildSelectedSlotOwner(int slotIndex)
    {
        if (selectedBySlot == null || slotIndex < 0 || slotIndex >= selectedBySlot.Length)
            return;

        selectedBySlot[slotIndex] = 0;

        for (int i = 0; i < players.Count; i++)
        {
            PlayerCursorState cursor = players[i];
            if (cursor == null || !cursor.confirmed || cursor.selectedIndex != slotIndex)
                continue;

            selectedBySlot[slotIndex] = cursor.playerId;
            return;
        }
    }

    void StartEndStageForSlot(int slotIndex, BomberCharacter character, BomberSkin skin)
    {
        if (!endStageBySlot.TryGetValue(slotIndex, out var st) || st == null)
        {
            st = new EndStageState();
            endStageBySlot[slotIndex] = st;
        }

        st.slotIndex = slotIndex;
        st.character = character;
        st.skin = skin;
        st.timer = 0f;
        st.frameIdx = 0;
        st.loopsDone = 0;
        st.stopped = false;
        st.baseCaptured = false;
    }

    void StartEndStageForCursor(PlayerCursorState cursor)
    {
        if (cursor == null)
            return;

        cursor.endStageTimer = 0f;
        cursor.endStageFrameIdx = 0;
        cursor.endStageLoopsDone = 0;
        cursor.endStageStopped = false;
    }

    void StopEndStageForCursor(PlayerCursorState cursor)
    {
        if (cursor == null)
            return;

        cursor.endStageTimer = 0f;
        cursor.endStageFrameIdx = 0;
        cursor.endStageLoopsDone = 0;
        cursor.endStageStopped = false;
    }

    void StartEndStageFinalForSlot(int slotIndex, BomberCharacter character, BomberSkin skin)
    {
        if (!endStageBySlot.TryGetValue(slotIndex, out var st) || st == null)
        {
            st = new EndStageState();
            endStageBySlot[slotIndex] = st;
        }

        int[] frames = GetEndStageFrames(character);
        bool loopEndStage = ShouldLoopEndStage(character);
        int finalFrameIndex = 0;
        if (frames != null && frames.Length > 0)
            finalFrameIndex = loopEndStage ? 0 : frames.Length - 1;

        st.slotIndex = slotIndex;
        st.character = character;
        st.skin = skin;
        st.timer = 0f;
        st.frameIdx = finalFrameIndex;
        st.loopsDone = loopEndStage ? 0 : Mathf.Max(1, endStageLoopsToStop);
        st.stopped = !loopEndStage;

        if (slotIndex >= 0 && slotIndex < slotImages.Count)
        {
            Image img = slotImages[slotIndex];
            if (img != null && img.rectTransform != null)
            {
                st.baseAnchoredPos = Vector2.zero;
                st.baseCaptured = true;

                img.rectTransform.anchoredPosition = st.baseAnchoredPos + new Vector2(0f, endStageYOffset);

                if (frames != null && frames.Length > 0)
                {
                    int frame = frames[Mathf.Clamp(finalFrameIndex, 0, frames.Length - 1)];
                    img.sprite = GetSpriteByFrame(character, skin, frame) ?? GetIdleSprite(character, skin);
                }
                else
                {
                    img.sprite = GetIdleSprite(character, skin);
                }

                img.color = selectedTint;
                img.enabled = img.sprite != null;
            }
        }
        else
        {
            st.baseCaptured = false;
        }
    }

    void ApplyFinalEndStageVisualsImmediate()
    {
        if (endStageBySlot == null || endStageBySlot.Count <= 0)
            return;

        foreach (var kv in endStageBySlot)
        {
            EndStageState st = kv.Value;
            if (st == null)
                continue;

            int slotIndex = st.slotIndex;
            if (slotIndex < 0 || slotIndex >= slotImages.Count)
                continue;

            Image img = slotImages[slotIndex];
            if (img == null)
                continue;

            if (img.rectTransform != null)
            {
                if (!st.baseCaptured)
                {
                    st.baseAnchoredPos = Vector2.zero;
                    st.baseCaptured = true;
                }

                img.rectTransform.anchoredPosition = st.baseAnchoredPos + new Vector2(0f, endStageYOffset);
            }

            int[] frames = GetEndStageFrames(st.character);
            if (frames != null && frames.Length > 0)
            {
                int frameIndex = ShouldLoopEndStage(st.character)
                    ? Mathf.Abs(st.frameIdx) % frames.Length
                    : Mathf.Clamp(st.frameIdx, 0, frames.Length - 1);
                int frame = frames[frameIndex];
                img.sprite = GetSpriteByFrame(st.character, st.skin, frame) ?? GetIdleSprite(st.character, st.skin);
            }
            else
            {
                img.sprite = GetIdleSprite(st.character, st.skin);
            }

            img.color = selectedTint;
            img.enabled = img.sprite != null;
        }
    }

    void StopEndStageForSlot(int slotIndex)
    {
        if (endStageBySlot.TryGetValue(slotIndex, out var st) && st != null)
        {
            if (slotIndex >= 0 && slotIndex < slotImages.Count)
            {
                var img = slotImages[slotIndex];
                if (img != null && img.rectTransform != null)
                    img.rectTransform.anchoredPosition = st.baseCaptured ? st.baseAnchoredPos : Vector2.zero;
            }
        }

        endStageBySlot.Remove(slotIndex);
    }

    bool AllPlayersConfirmed()
    {
        if (useBattleModeComAssignment)
            return AllBattleModeTargetsConfirmed();

        for (int i = 0; i < players.Count; i++)
            if (!players[i].confirmed)
                return false;

        return players.Count > 0;
    }

    bool AllBattleModeTargetsConfirmed()
    {
        if (battleSkinTargetPlayerIds.Count <= 0)
            return false;

        for (int i = 0; i < battleSkinTargetPlayerIds.Count; i++)
        {
            int targetPlayerId = battleSkinTargetPlayerIds[i];
            bool found = false;

            for (int p = 0; p < players.Count; p++)
            {
                if (players[p].playerId == targetPlayerId && players[p].confirmed)
                {
                    found = true;
                    break;
                }
            }

            if (!found)
                return false;
        }

        for (int i = 0; i < players.Count; i++)
        {
            if (!players[i].confirmed)
                return false;
        }

        return true;
    }

    void TickCursorBlink()
    {
        if (!cursorBlinkWhileNotConfirmed)
            return;

        cursorBlinkT += Time.unscaledDeltaTime * Mathf.Max(0.01f, cursorBlinkSpeed);
        float s = (Mathf.Sin(cursorBlinkT) + 1f) * 0.5f;
        float a = Mathf.Lerp(cursorBlinkMinAlpha, cursorBlinkMaxAlpha, s);

        for (int i = 0; i < players.Count; i++)
        {
            var ps = players[i];
            if (ps.cursorImg == null || ps.cursorRt == null || !ps.cursorRt.gameObject.activeSelf)
                continue;

            if (ps.confirmed)
            {
                var c0 = ps.cursorImg.color;
                if (c0.a != 1f)
                {
                    c0.a = 1f;
                    ps.cursorImg.color = c0;
                }
                continue;
            }

            var col = ps.cursorImg.color;
            if (!Mathf.Approximately(col.a, a))
            {
                col.a = a;
                ps.cursorImg.color = col;
            }
        }
    }

    void TickDownClock()
    {
        if (downFrames == null || downFrames.Length == 0)
            return;

        float ft = Mathf.Max(0.001f, downFrameTime);
        downTimer += Time.unscaledDeltaTime;

        while (downTimer >= ft)
        {
            downTimer -= ft;
            downFrameIdx = (downFrameIdx + 1) % downFrames.Length;
        }
    }

    void TickEndStageClocks()
    {
        for (int i = 0; i < players.Count; i++)
        {
            PlayerCursorState cursor = players[i];
            if (cursor == null || !cursor.confirmed || cursor.endStageStopped)
                continue;

            int[] frames = GetEndStageFrames(cursor.selectedCharacter);
            if (frames == null || frames.Length == 0)
                continue;

            float ft = GetSelectedSkinConfirmFrameTime(cursor.selectedCharacter);
            cursor.endStageTimer += Time.unscaledDeltaTime;

            while (cursor.endStageTimer >= ft)
            {
                cursor.endStageTimer -= ft;
                int next = cursor.endStageFrameIdx + 1;

                if (next >= frames.Length)
                {
                    cursor.endStageLoopsDone++;
                    cursor.endStageStopped = true;
                    cursor.endStageFrameIdx = frames.Length - 1;
                    break;
                }

                cursor.endStageFrameIdx = next;
            }
        }
        foreach (var kv in endStageBySlot)
        {
            var st = kv.Value;
            if (st == null || st.stopped)
                continue;

            int[] frames = GetEndStageFrames(st.character);
            if (frames == null || frames.Length == 0)
                continue;

            float ft = GetEndStageFrameTime(st.character);
            st.timer += Time.unscaledDeltaTime;

            while (st.timer >= ft)
            {
                st.timer -= ft;

                int next = st.frameIdx + 1;

                if (next >= frames.Length)
                {
                    if (ShouldLoopEndStage(st.character))
                    {
                        st.loopsDone++;
                        next = 0;
                    }
                    else
                    {
                        st.loopsDone++;

                        if (st.loopsDone >= Mathf.Max(1, endStageLoopsToStop))
                        {
                            st.stopped = true;
                            st.frameIdx = frames.Length - 1;
                            break;
                        }

                        next = 0;
                    }
                }

                st.frameIdx = next;
            }
        }
    }

    void UpdateSlotVisuals()
    {
        if (SelectableSlotCount == 0)
            return;

        for (int i = 0; i < slotImages.Count; i++)
        {
            var img = slotImages[i];
            if (img == null)
                continue;

            BomberCharacter slotCharacter = GetCharacterAtSlot(i);
            BomberSkin skin = GetPreviewSkinForSlot(i);
            bool unlocked = UnlockProgress.IsUnlocked(skin);
            bool activeCursorHere = IsActiveCursorOnSlot(i);

            if (!unlocked)
                img.color = lockedTint;
            else if (activeCursorHere)
                img.color = selectedTint;
            else
                img.color = normalTint;

            if (img.rectTransform != null)
                img.rectTransform.anchoredPosition = Vector2.zero;

            if (unlocked && activeCursorHere)
            {
                int f = downFrames != null && downFrames.Length > 0
                    ? downFrames[Mathf.Clamp(downFrameIdx, 0, downFrames.Length - 1)]
                    : idleFrameIndex;

                img.sprite = GetSpriteByFrame(slotCharacter, skin, f) ?? GetIdleSprite(slotCharacter, skin);
            }
            else
            {
                img.sprite = GetIdleSprite(slotCharacter, skin);
            }

            img.enabled = img.sprite != null;
        }
    }

    bool IsActiveCursorOnSlot(int slotIndex)
    {
        PlayerCursorState active = GetActiveSelectionCursor();
        return active != null && !active.confirmed && active.index == slotIndex;
    }

    void RestoreAllSlotPositions()
    {
        for (int i = 0; i < slotImages.Count; i++)
        {
            var img = slotImages[i];
            if (img != null && img.rectTransform != null)
                img.rectTransform.anchoredPosition = Vector2.zero;
        }
    }

    void BuildPlayerCursors(List<int> activePlayerIds)
    {
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].cursorRt != null)
                Destroy(players[i].cursorRt.gameObject);
        }

        players.Clear();

        if (activePlayerIds == null || activePlayerIds.Count <= 0)
            return;

        activePlayerIds.Sort();

        for (int i = 0; i < activePlayerIds.Count; i++)
        {
            int p = Mathf.Clamp(activePlayerIds[i], GameSession.MinPlayerId, GameSession.MaxPlayerId);
            bool isBattleCom = useBattleModeComAssignment && IsBattleModeComPlayer(p);
            int inputPlayerId = isBattleCom ? GameSession.MinPlayerId : p;
            var st = CreateCursorState(inputPlayerId, p, isBattleCom);
            players.Add(st);
        }
    }

    bool DirectionalPressed(PlayerInputManager input, int playerId, PlayerAction action)
    {
        int directionIndex = action switch
        {
            PlayerAction.MoveUp => 0,
            PlayerAction.MoveDown => 1,
            PlayerAction.MoveLeft => 2,
            PlayerAction.MoveRight => 3,
            _ => -1
        };

        int clampedPlayerId = Mathf.Clamp(playerId, GameSession.MinPlayerId, GameSession.MaxPlayerId);
        if (input == null || directionIndex < 0)
            return false;

        bool held = input.Get(clampedPlayerId, action);
        if (!held)
        {
            previousDirectionalHeld[clampedPlayerId, directionIndex] = false;
            nextDirectionalRepeatTime[clampedPlayerId, directionIndex] = 0f;
            return false;
        }

        float now = Time.unscaledTime;
        if (!previousDirectionalHeld[clampedPlayerId, directionIndex])
        {
            previousDirectionalHeld[clampedPlayerId, directionIndex] = true;
            nextDirectionalRepeatTime[clampedPlayerId, directionIndex] =
                now + Mathf.Max(0.01f, directionalRepeatInitialDelay);
            return true;
        }

        if (now < nextDirectionalRepeatTime[clampedPlayerId, directionIndex])
            return false;

        nextDirectionalRepeatTime[clampedPlayerId, directionIndex] =
            now + Mathf.Max(0.01f, directionalRepeatInterval);
        return true;
    }

    void ResetDirectionalRepeatState()
    {
        System.Array.Clear(battleBackReleaseRequired, 0, battleBackReleaseRequired.Length);

        for (int playerId = 0; playerId <= GameSession.MaxPlayerId; playerId++)
        {
            for (int directionIndex = 0; directionIndex < 4; directionIndex++)
            {
                previousDirectionalHeld[playerId, directionIndex] = false;
                nextDirectionalRepeatTime[playerId, directionIndex] = 0f;
            }
        }
    }

    PlayerCursorState CreateCursorState(int inputPlayerId, int targetPlayerId, bool battleComCursor)
    {
        int input = Mathf.Clamp(inputPlayerId, GameSession.MinPlayerId, GameSession.MaxPlayerId);
        int target = Mathf.Clamp(targetPlayerId, GameSession.MinPlayerId, GameSession.MaxPlayerId);
        var st = new PlayerCursorState
        {
            playerId = target,
            inputPlayerId = input,
            battleComCursor = battleComCursor
        };

        if (skinCursorPrefab != null)
        {
            var c = Instantiate(skinCursorPrefab, gridRoot);
            c.gameObject.SetActive(false);
            c.SetAsLastSibling();
            st.cursorRt = c;

            st.cursorImg = c.GetComponent<Image>();
            if (st.cursorImg != null)
            {
                st.cursorImg.raycastTarget = false;

                int spriteIdx = target - 1;

                if (cursorSpriteByPlayer != null &&
                    spriteIdx >= 0 &&
                    spriteIdx < cursorSpriteByPlayer.Length &&
                    cursorSpriteByPlayer[spriteIdx] != null)
                {
                    st.cursorImg.sprite = cursorSpriteByPlayer[spriteIdx];
                    st.cursorImg.preserveAspect = true;
                }

                var col = st.cursorImg.color;
                col.a = 1f;
                st.cursorImg.color = col;
            }
        }

        return st;
    }

    void PopulateBattleModeSkinSelectionIds(List<int> results)
    {
        if (results == null)
            return;

        results.Clear();
        battleSkinTargetPlayerIds.Clear();
        battleComQueue.Clear();
        battleConfirmedByInput.Clear();
        nextBattleComQueueIndex = 0;

        BattleModePlayerControlMode[] modes = SaveSystem.GetBattleModePlayerControlModes();
        for (int i = 0; i < modes.Length && i < GameSession.MaxPlayerId; i++)
        {
            int playerId = i + 1;
            BattleModePlayerControlMode mode = modes[i];

            if (mode == BattleModePlayerControlMode.Off)
                continue;

            battleSkinTargetPlayerIds.Add(playerId);
            results.Add(playerId);

            if (mode == BattleModePlayerControlMode.Com)
                battleComQueue.Add(playerId);
        }

        if (battleSkinTargetPlayerIds.Count <= 0)
            battleSkinTargetPlayerIds.Add(GameSession.MinPlayerId);

        if (results.Count <= 0)
            results.AddRange(battleSkinTargetPlayerIds);

        for (int i = 0; i < results.Count; i++)
            battleComQueue.Remove(results[i]);
    }

    bool IsBattleModeComPlayer(int playerId)
    {
        if (!useBattleModeComAssignment)
            return false;

        BattleModePlayerControlMode[] modes = SaveSystem.GetBattleModePlayerControlModes();
        int index = Mathf.Clamp(playerId, GameSession.MinPlayerId, GameSession.MaxPlayerId) - 1;

        return modes != null &&
               index >= 0 &&
               index < modes.Length &&
               modes[index] == BattleModePlayerControlMode.Com;
    }

    int GetCursorInputPlayerId(PlayerCursorState ps)
    {
        if (ps == null)
            return GameSession.MinPlayerId;

        int input = ps.inputPlayerId != 0 ? ps.inputPlayerId : ps.playerId;
        return Mathf.Clamp(input, GameSession.MinPlayerId, GameSession.MaxPlayerId);
    }

    void PushBattleConfirmedCursor(PlayerCursorState ps)
    {
        int inputPlayerId = GetCursorInputPlayerId(ps);

        if (!battleConfirmedByInput.TryGetValue(inputPlayerId, out var stack) || stack == null)
        {
            stack = new List<PlayerCursorState>();
            battleConfirmedByInput[inputPlayerId] = stack;
        }

        if (!stack.Contains(ps))
            stack.Add(ps);

    }

    void RemoveBattleConfirmedCursorFromStack(PlayerCursorState ps)
    {
        if (ps == null)
            return;

        int inputPlayerId = GetCursorInputPlayerId(ps);
        if (!battleConfirmedByInput.TryGetValue(inputPlayerId, out var stack) || stack == null)
            return;

        stack.Remove(ps);
    }

    void AssignNextBattleComCursor(int inputPlayerId)
    {
        if (!useBattleModeComAssignment)
            return;

        if (nextBattleComQueueIndex < 0 || nextBattleComQueueIndex >= battleComQueue.Count)
            return;

        int targetPlayerId = battleComQueue[nextBattleComQueueIndex++];
        var cursor = CreateCursorState(inputPlayerId, targetPlayerId, battleComCursor: true);
        InitializeCursorSelection(cursor);
        players.Add(cursor);

        UpdateAllCursorsToSelected();
    }

    bool HandleBattleModeBack(PlayerCursorState activeCursor)
    {
        if (activeCursor == null)
            return false;

        int inputPlayerId = GetCursorInputPlayerId(activeCursor);
        return HandleBattleModeBack(inputPlayerId, activeCursor);
    }

    bool HandleBattleModeBack(int inputPlayerId, PlayerCursorState activeCursor)
    {
        inputPlayerId = Mathf.Clamp(inputPlayerId, GameSession.MinPlayerId, GameSession.MaxPlayerId);

        if (!battleConfirmedByInput.TryGetValue(inputPlayerId, out var stack) || stack == null || stack.Count <= 0)
        {
            return false;
        }

        PlayerCursorState previous = stack[stack.Count - 1];
        stack.RemoveAt(stack.Count - 1);

        UnconfirmCursor(previous);
        previous.inputPlayerId = inputPlayerId;

        if (previous.cursorRt != null)
            previous.cursorRt.gameObject.SetActive(true);

        return true;
    }

    bool HandleCompletedBattleModeBackInputs(PlayerInputManager input)
    {
        if (input == null)
            return false;

        if (GetActiveSelectionCursor() != null)
            return false;

        for (int playerId = GameSession.MinPlayerId; playerId <= GameSession.MaxPlayerId; playerId++)
        {
            if (!BattleModeBackPressed(input, playerId))
                continue;

            if (HasActiveUnconfirmedCursorForInput(playerId))
                continue;

            if (HandleBattleModeBack(playerId, null))
                return true;
        }

        return false;
    }

    bool HasActiveUnconfirmedCursorForInput(int inputPlayerId)
    {
        inputPlayerId = Mathf.Clamp(inputPlayerId, GameSession.MinPlayerId, GameSession.MaxPlayerId);

        for (int i = 0; i < players.Count; i++)
        {
            PlayerCursorState ps = players[i];
            if (ps == null || ps.confirmed)
                continue;

            if (GetCursorInputPlayerId(ps) == inputPlayerId)
                return true;
        }

        return false;
    }

    void RemoveCursor(PlayerCursorState cursor)
    {
        if (cursor == null)
            return;

        if (cursor.cursorRt != null)
            Destroy(cursor.cursorRt.gameObject);

        players.Remove(cursor);
    }

    void InitializeCursorSelection(PlayerCursorState cursor)
    {
        ApplySavedSkinToCursor(cursor);
    }

    void PreconfirmBattleModeSelectionFromSavedSkins()
    {
        if (!useBattleModeComAssignment)
            return;

        if (battleSkinTargetPlayerIds == null || battleSkinTargetPlayerIds.Count <= 0)
            return;

        int inputPlayerId = GetDefaultBattleModeResumeInputPlayerId();
        int lastTargetIndex = battleSkinTargetPlayerIds.Count - 1;

        nextBattleComQueueIndex = 0;

        for (int i = 0; i < battleSkinTargetPlayerIds.Count; i++)
        {
            int targetPlayerId = battleSkinTargetPlayerIds[i];
            bool shouldLeaveUnconfirmed = i == lastTargetIndex;

            PlayerCursorState cursor = GetPlayerState(targetPlayerId);

            if (cursor == null)
            {
                cursor = CreateCursorState(inputPlayerId, targetPlayerId, battleComCursor: true);
                players.Add(cursor);
                nextBattleComQueueIndex++;
            }

            cursor.inputPlayerId = cursor.battleComCursor ? inputPlayerId : cursor.playerId;

            ApplySavedSkinToCursor(cursor);

            if (shouldLeaveUnconfirmed)
            {
                cursor.confirmed = false;
                cursor.selectedIndex = -1;

                if (cursor.cursorRt != null)
                    cursor.cursorRt.gameObject.SetActive(true);

                continue;
            }

            ForceConfirmCursorWithoutSfx(cursor);
        }

        UpdateNextBattleComQueueIndexFromCreatedComCursors();
    }

    int GetDefaultBattleModeResumeInputPlayerId()
    {
        if (configuredPlayerIds != null && configuredPlayerIds.Count > 0)
            return Mathf.Clamp(configuredPlayerIds[0], GameSession.MinPlayerId, GameSession.MaxPlayerId);

        BattleModePlayerControlMode[] modes = SaveSystem.GetBattleModePlayerControlModes();

        if (modes != null)
        {
            for (int i = 0; i < modes.Length && i < GameSession.MaxPlayerId; i++)
            {
                if (modes[i] == BattleModePlayerControlMode.Man)
                    return i + 1;
            }
        }

        return GameSession.MinPlayerId;
    }

    void ApplySavedSkinToCursor(PlayerCursorState cursor)
    {
        if (cursor == null)
            return;

        var state = PlayerPersistentStats.Get(cursor.playerId);
        BomberCharacter savedCharacter = state.Character;
        BomberSkin requestedSkin = state.Skin;
        int index = selectableCharacters.IndexOf(savedCharacter);

        if (index < 0)
        {
            savedCharacter = BomberCharacter.Bomberman;
            index = selectableCharacters.IndexOf(savedCharacter);
        }

        if (index < 0)
            index = 0;

        BomberSkin savedSkin = BomberSkinResourceCatalog.NormalizeGeneratedSkin(
            savedCharacter,
            UnlockProgress.ClampToUnlocked(requestedSkin, PlayerPersistentStats.GetDefaultSkinForPlayer(cursor.playerId)));

        cursor.index = index;
        cursor.selectedCharacter = savedCharacter;
        cursor.selected = savedSkin;
        cursor.selectedPaletteIndex = GetPaletteIndex(savedSkin, cursor.playerId);
        cursor.selectedPaletteIndex = FindAvailablePaletteIndex(savedCharacter, cursor.selectedPaletteIndex, 1);
        cursor.selected = selectableSkins != null && selectableSkins.Count > 0
            ? selectableSkins[cursor.selectedPaletteIndex]
            : savedSkin;
        cursor.selectedIndex = -1;
        cursor.confirmed = false;
    }

    void ForceConfirmCursorWithoutSfx(PlayerCursorState cursor)
    {
        if (cursor == null)
            return;

        int slot = Mathf.Clamp(cursor.index, 0, SelectableSlotCount - 1);
        BomberCharacter selectedCharacter = GetCharacterAtSlot(slot);
        BomberSkin skin = GetCursorPalette(cursor);

        if (!UnlockProgress.IsUnlocked(skin))
            return;

        selectedBySlot[slot] = cursor.playerId;

        cursor.selectedCharacter = selectedCharacter;
        cursor.selected = skin;
        cursor.selectedIndex = slot;
        cursor.confirmed = true;

        PlayerPersistentStats.Get(cursor.playerId).Character = selectedCharacter;
        PlayerPersistentStats.Get(cursor.playerId).Skin = skin;

        PushBattleConfirmedCursor(cursor);

        StartEndStageForCursor(cursor);

        if (cursor.cursorImg != null)
        {
            Color c = cursor.cursorImg.color;
            c.a = 1f;
            cursor.cursorImg.color = c;
        }

        if (cursor.cursorRt != null)
            cursor.cursorRt.gameObject.SetActive(true);
    }

    void UpdateNextBattleComQueueIndexFromCreatedComCursors()
    {
        int createdComCursors = 0;

        for (int i = 0; i < players.Count; i++)
        {
            PlayerCursorState cursor = players[i];
            if (cursor != null && cursor.battleComCursor)
                createdComCursors++;
        }

        nextBattleComQueueIndex = Mathf.Clamp(createdComCursors, 0, battleComQueue.Count);
    }

    void PopulateConfiguredPlayerIds(List<int> results)
    {
        if (results == null)
            return;

        results.Clear();

        if (GameSession.Instance != null)
            GameSession.Instance.GetActivePlayerIds(results);

        if (results.Count <= 0)
            results.Add(GameSession.MinPlayerId);
    }

    void BuildGrid()
    {
        slotRoots.Clear();
        slotImages.Clear();

        if (gridRoot == null || skinItemPrefab == null)
            return;

        if (gridRoot.TryGetComponent<GridLayoutGroup>(out var grid))
        {
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = VisibleColumnCount;
            grid.cellSize = cellSize;
            grid.spacing = spacing;
        }

        for (int i = gridRoot.childCount - 1; i >= 0; i--)
        {
            var child = gridRoot.GetChild(i);
            if (child == null)
                continue;
            if (skinItemPrefab != null && child == skinItemPrefab.transform)
                continue;
            if (skinCursorPrefab != null && child == skinCursorPrefab.transform)
                continue;
            Destroy(child.gameObject);
        }

        skinItemPrefab.gameObject.SetActive(false);

        for (int i = 0; i < SelectableSlotCount; i++)
        {
            var slotGo = new GameObject($"SkinSlot_{i}", typeof(RectTransform));
            var slotRt = slotGo.GetComponent<RectTransform>();
            slotRt.SetParent(gridRoot, false);
            slotRt.localScale = Vector3.one;
            slotRt.localRotation = Quaternion.identity;
            slotRt.anchorMin = new Vector2(0.5f, 0.5f);
            slotRt.anchorMax = new Vector2(0.5f, 0.5f);
            slotRt.pivot = new Vector2(0.5f, 0.5f);

            var img = Instantiate(skinItemPrefab, slotRt);
            img.gameObject.SetActive(true);
            img.preserveAspect = true;
            img.enabled = false;

            var imgRt = img.rectTransform;
            imgRt.anchorMin = new Vector2(0.5f, 0.5f);
            imgRt.anchorMax = new Vector2(0.5f, 0.5f);
            imgRt.pivot = new Vector2(0.5f, 0.5f);
            imgRt.anchoredPosition = Vector2.zero;
            imgRt.sizeDelta = GetSkinSpriteDisplaySize();
            imgRt.localScale = Vector3.one;
            imgRt.localRotation = Quaternion.identity;

            slotRoots.Add(slotRt);
            slotImages.Add(img);
        }
    }

    void EnsureSelectedSkinsListRoot()
    {
        if (selectedSkinsListRoot != null)
            return;

        Transform parent = rootPanel != null
            ? rootPanel
            : root != null
                ? root.transform
                : transform;

        GameObject go = new("SelectedSkinsList", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        selectedSkinsListRoot = go.GetComponent<RectTransform>();
        selectedSkinsListRoot.anchorMin = new Vector2(0.5f, 0.5f);
        selectedSkinsListRoot.anchorMax = new Vector2(0.5f, 0.5f);
        selectedSkinsListRoot.pivot = new Vector2(0.5f, 0.5f);
    }

    void RebuildSelectedSkinsListVisuals()
    {
        EnsureSelectedSkinsListRoot();

        for (int i = selectedStatusVisuals.Count - 1; i >= 0; i--)
        {
            if (selectedStatusVisuals[i]?.root != null)
                Destroy(selectedStatusVisuals[i].root.gameObject);
        }

        selectedStatusVisuals.Clear();

        if (selectedSkinsListRoot == null)
            return;

        for (int i = 0; i < players.Count; i++)
        {
            PlayerCursorState cursor = players[i];
            if (cursor == null)
                continue;

            GameObject entryGo = new($"SelectedSkin_P{cursor.playerId}", typeof(RectTransform));
            entryGo.transform.SetParent(selectedSkinsListRoot, false);
            RectTransform entryRt = entryGo.GetComponent<RectTransform>();

            GameObject cursorGo = new("Cursor", typeof(RectTransform), typeof(Image));
            cursorGo.transform.SetParent(entryRt, false);
            Image cursorImage = cursorGo.GetComponent<Image>();
            cursorImage.raycastTarget = false;
            cursorImage.preserveAspect = true;
            cursorImage.sprite = GetCursorSpriteForPlayer(cursor.playerId);

            GameObject imageGo = new("Character", typeof(RectTransform), typeof(Image));
            imageGo.transform.SetParent(entryRt, false);
            Image image = imageGo.GetComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = true;

            selectedStatusVisuals.Add(new SelectedSkinStatusVisual
            {
                root = entryRt,
                cursorImage = cursorImage,
                image = image
            });
        }
    }

    void ClearSelectedSkinsListVisuals()
    {
        for (int i = selectedStatusVisuals.Count - 1; i >= 0; i--)
        {
            if (selectedStatusVisuals[i]?.root != null)
                Destroy(selectedStatusVisuals[i].root.gameObject);
        }

        selectedStatusVisuals.Clear();

        if (selectedSkinsListRoot != null)
            selectedSkinsListRoot.gameObject.SetActive(false);
    }

    void UpdateSelectedSkinsListVisuals()
    {
        EnsureSelectedSkinsListRoot();

        if (selectedSkinsListRoot == null)
            return;

        int count = Mathf.Min(players.Count, selectedStatusVisuals.Count);
        selectedSkinsListRoot.gameObject.SetActive(count > 0);

        float scale = Mathf.Max(0.01f, _currentUiScale);
        Vector2 cursorSize = GetGridCursorDisplaySize();
        Vector2 spriteSize = GetSkinSpriteDisplaySize();
        Vector2 entrySize = new(
            Mathf.Max(cursorSize.x, spriteSize.x),
            Mathf.Max(cursorSize.y, spriteSize.y)
        );
        float spacingPx = GetSelectedSkinsListSpacing(count, entrySize.x, scale);
        float totalWidth = count * entrySize.x + Mathf.Max(0, count - 1) * spacingPx;
        float startX = -totalWidth * 0.5f + entrySize.x * 0.5f;

        selectedSkinsListRoot.anchoredPosition = selectedSkinsListBaseAnchoredPos * scale;
        selectedSkinsListRoot.sizeDelta = new Vector2(totalWidth, entrySize.y);

        PlayerCursorState active = GetActiveSelectionCursor();

        for (int i = 0; i < count; i++)
        {
            PlayerCursorState cursor = players[i];
            SelectedSkinStatusVisual visual = selectedStatusVisuals[i];
            if (cursor == null || visual == null || visual.root == null)
                continue;

            bool isActive = active != null && ReferenceEquals(active, cursor);
            BomberCharacter character = cursor.confirmed
                ? cursor.selectedCharacter
                : GetCharacterAtSlot(cursor.index);
            BomberSkin skin = cursor.confirmed
                ? cursor.selected
                : GetCursorPalette(cursor);

            int frame = idleFrameIndex;
            if (cursor.confirmed)
            {
                int[] frames = GetEndStageFrames(character);
                if (frames != null && frames.Length > 0)
                {
                    int frameIndex = Mathf.Clamp(cursor.endStageFrameIdx, 0, frames.Length - 1);
                    frame = frames[frameIndex];
                }
            }
            else if (isActive && downFrames != null && downFrames.Length > 0)
            {
                frame = downFrames[Mathf.Clamp(downFrameIdx, 0, downFrames.Length - 1)];
            }

            visual.root.gameObject.SetActive(true);
            visual.root.anchorMin = new Vector2(0.5f, 0.5f);
            visual.root.anchorMax = new Vector2(0.5f, 0.5f);
            visual.root.pivot = new Vector2(0.5f, 0.5f);
            visual.root.anchoredPosition = new Vector2(startX + i * (entrySize.x + spacingPx), 0f);
            visual.root.sizeDelta = entrySize;
            visual.root.localScale = Vector3.one;

            if (visual.cursorImage != null)
            {
                RectTransform cursorRt = visual.cursorImage.rectTransform;
                cursorRt.anchorMin = new Vector2(0.5f, 0.5f);
                cursorRt.anchorMax = new Vector2(0.5f, 0.5f);
                cursorRt.pivot = new Vector2(0.5f, 0.5f);
                cursorRt.anchoredPosition = Vector2.zero;
                cursorRt.sizeDelta = cursorSize;

                visual.cursorImage.sprite = GetCursorSpriteForPlayer(cursor.playerId);
                Color cursorColor = Color.white;
                if (!cursor.confirmed)
                    cursorColor.a = isActive ? selectedSkinCurrentCursorAlpha : selectedSkinPendingCursorAlpha;
                visual.cursorImage.color = cursorColor;
                visual.cursorImage.enabled = visual.cursorImage.sprite != null;
            }

            if (visual.image != null)
            {
                RectTransform imageRt = visual.image.rectTransform;
                imageRt.anchorMin = new Vector2(0.5f, 0.5f);
                imageRt.anchorMax = new Vector2(0.5f, 0.5f);
                imageRt.pivot = new Vector2(0.5f, 0.5f);
                imageRt.anchoredPosition = new Vector2(0f, selectedSkinSpriteYOffset * scale);
                imageRt.sizeDelta = spriteSize;

                visual.image.sprite = GetSpriteByFrame(character, skin, frame) ?? GetIdleSprite(character, skin);
                Color imageColor = Color.white;
                if (!cursor.confirmed)
                    imageColor.a = selectedSkinPreviewAlpha;
                visual.image.color = imageColor;
                visual.image.enabled = (cursor.confirmed || isActive) && visual.image.sprite != null;
            }
        }
    }

    bool BattleModeBackPressed(PlayerInputManager input, int playerId)
    {
        int clampedPlayerId = Mathf.Clamp(playerId, GameSession.MinPlayerId, GameSession.MaxPlayerId);
        if (input == null)
            return false;

        bool held = input.Get(clampedPlayerId, PlayerAction.ActionB);
        if (!held)
        {
            battleBackReleaseRequired[clampedPlayerId] = false;
            return false;
        }

        if (battleBackReleaseRequired[clampedPlayerId])
            return false;

        if (!input.GetDown(clampedPlayerId, PlayerAction.ActionB))
            return false;

        battleBackReleaseRequired[clampedPlayerId] = true;
        return true;
    }

    float GetSelectedSkinsListSpacing(int count, float entryWidth, float scale)
    {
        bool useCompactSpacing = count > 4;
        float defaultSpacing = useCompactSpacing
            ? selectedSkinEntryCompactSpacing
            : selectedSkinEntryBaseSpacing;
        float requestedSpacingPx = defaultSpacing * scale;

        if (!useCompactSpacing)
            return Mathf.Max(0f, requestedSpacingPx);

        float maxWidth = GetSelectedSkinsListMaxWidth();
        if (maxWidth <= 0f)
            return requestedSpacingPx;

        float maxSpacingToFit = (maxWidth - (count * entryWidth)) / Mathf.Max(1, count - 1);
        float finalSpacingPx = Mathf.Min(requestedSpacingPx, maxSpacingToFit);

        return finalSpacingPx;
    }

    float GetSelectedSkinsListMaxWidth()
    {
        RectTransform safeFrame = referenceRect != null
            ? referenceRect
            : rootPanel != null
                ? rootPanel
                : root != null
                    ? root.GetComponent<RectTransform>()
                    : null;

        if (safeFrame == null)
            return 0f;

        return Mathf.Max(1f, safeFrame.rect.width * Mathf.Clamp01(selectedSkinsListSafeFrameWidthRatio));
    }

    Sprite GetCursorSpriteForPlayer(int playerId)
    {
        int spriteIdx = Mathf.Clamp(playerId, GameSession.MinPlayerId, GameSession.MaxPlayerId) - 1;

        if (cursorSpriteByPlayer != null &&
            spriteIdx >= 0 &&
            spriteIdx < cursorSpriteByPlayer.Length &&
            cursorSpriteByPlayer[spriteIdx] != null)
        {
            return cursorSpriteByPlayer[spriteIdx];
        }

        return skinCursorPrefab != null && skinCursorPrefab.TryGetComponent(out Image prefabImage)
            ? prefabImage.sprite
            : null;
    }

    void EnsureFooterHintText()
    {
        if (footerHintText != null)
            return;

        Transform parent = rootPanel != null
            ? rootPanel
            : root != null
                ? root.transform
                : transform;

        GameObject go = new("SkinSelectHints", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        footerHintText = go.GetComponent<TextMeshProUGUI>();
        footerHintText.raycastTarget = false;
        footerHintText.textWrappingMode = TextWrappingModes.NoWrap;
        footerHintText.overflowMode = TextOverflowModes.Overflow;
        footerHintText.alignment = TextAlignmentOptions.Center;
        footerHintText.color = GetHintTextColor();
        ApplyFooterHintTextStyle();
    }

    void HideFooterHintImmediate()
    {
        if (footerHintText != null)
            footerHintText.gameObject.SetActive(false);
    }

    void UpdateFooterHintVisual()
    {
        EnsureFooterHintText();

        if (footerHintText == null)
            return;

        RectTransform hintRt = footerHintText.rectTransform;
        float hintScale = GetFooterHintScale();
        hintRt.anchorMin = new Vector2(0.5f, 0.5f);
        hintRt.anchorMax = new Vector2(0.5f, 0.5f);
        hintRt.pivot = new Vector2(0.5f, 0.5f);
        Vector2 anchoredPosition = FooterHintItemSelectAnchoredPos * hintScale;
        if (!useBattleModeComAssignment)
            anchoredPosition.y += NormalGameFooterHintYOffset;

        hintRt.anchoredPosition = anchoredPosition;
        hintRt.sizeDelta = FooterHintItemSelectSize;
        hintRt.localScale = Vector3.one * hintScale;
        footerHintText.color = GetHintTextColor();
        footerHintText.text = GameTextDatabase.BattleModeMenu.SkinSelectHints;
        ApplyFooterHintTextStyle();
        footerHintText.fontSize = FooterHintItemSelectFontSize;
        footerHintText.lineSpacing = FooterHintItemSelectLineSpacing;
        footerHintText.alignment = TextAlignmentOptions.Center;
        footerHintText.gameObject.SetActive(menuActive);
        if (!menuActive)
            return;

    }

    float GetFooterHintScale()
    {
        return Mathf.Max(0.01f, _currentUiScale / Mathf.Max(0.01f, extraScaleMultiplier));
    }

    void ResolveLeftPanel()
    {
        if (leftPanel != null)
            return;

        Transform searchRoot = root != null ? root.transform : transform;
        leftPanel = searchRoot.GetComponentInParent<SaveFileMenuOptions>(true);

        if (leftPanel == null && rootPanel != null)
            leftPanel = rootPanel.GetComponentInParent<SaveFileMenuOptions>(true);

        if (leftPanel == null)
        {
            Canvas canvas = GetComponentInParent<Canvas>(true);
            if (canvas != null)
                leftPanel = canvas.GetComponentInChildren<SaveFileMenuOptions>(true);
        }

        if (leftPanel == null)
            leftPanel = FindAnyObjectByType<SaveFileMenuOptions>(FindObjectsInactive.Include);
    }

    Color GetHintTextColor()
    {
        return leftPanel != null
            ? leftPanel.NormalColor
            : new Color32(0, 180, 0, 255);
    }

    void ApplyFooterHintTextStyle()
    {
        if (footerHintText == null)
            return;

        ResolveLeftPanel();

        if (leftPanel != null)
        {
            leftPanel.ApplyOptionTextStyleTo(footerHintText, footerHintText.color);
        }
        else
        {
            TMP_FontAsset pressStartFont = ResolveFooterHintFontAsset();
            if (pressStartFont != null)
                footerHintText.font = pressStartFont;

            if (footerHintFontMaterialPreset != null)
                footerHintText.fontMaterial = footerHintFontMaterialPreset;

            ApplyFooterHintFallbackStyle();
        }

        LocalizedTmpFontFallback.Apply(footerHintText);
        footerHintText.UpdateMeshPadding();
        footerHintText.ForceMeshUpdate();
        footerHintText.SetVerticesDirty();
    }

    TMP_FontAsset ResolveFooterHintFontAsset()
    {
        if (footerHintFontAsset != null)
            return footerHintFontAsset;

        footerHintFontAsset = Resources.Load<TMP_FontAsset>("Fonts/PressStart2P-Regular SDF");
        if (footerHintFontAsset != null)
            return footerHintFontAsset;

        return leftPanel != null ? leftPanel.OptionFontAsset : null;
    }

    void ApplyFooterHintFallbackStyle()
    {
        footerHintText.richText = true;
        footerHintText.enableAutoSizing = false;
        footerHintText.extraPadding = true;
        footerHintText.fontStyle |= FontStyles.Bold;
        footerHintText.color = new Color32(0, 210, 0, 255);
        footerHintText.faceColor = new Color32(0, 210, 0, 255);
        footerHintText.outlineColor = Color.black;
        footerHintText.outlineWidth = 0.28f;
        footerHintText.margin = Vector4.zero;
        footerHintText.textWrappingMode = TextWrappingModes.NoWrap;
        footerHintText.overflowMode = TextOverflowModes.Overflow;

        Material runtimeMat = GetOrCreateFooterHintRuntimeMaterial();
        ApplyFooterHintMaterialStyle(runtimeMat);

        if (runtimeMat != null)
            footerHintText.fontMaterial = runtimeMat;
    }

    Material GetOrCreateFooterHintRuntimeMaterial()
    {
        if (footerHintText == null)
            return null;

        if (footerHintRuntimeMaterial != null)
            return footerHintRuntimeMaterial;

        Material baseMat = null;

        if (footerHintFontMaterialPreset != null)
            baseMat = footerHintFontMaterialPreset;
        else if (footerHintText.fontSharedMaterial != null)
            baseMat = footerHintText.fontSharedMaterial;
        else if (footerHintText.font != null)
            baseMat = footerHintText.font.material;

        if (baseMat == null)
            return null;

        footerHintRuntimeMaterial = new Material(baseMat);
        footerHintRuntimeMaterial.name = baseMat.name + "_SkinFooterHintRuntime";
        return footerHintRuntimeMaterial;
    }

    void ApplyFooterHintMaterialStyle(Material mat)
    {
        if (mat == null)
            return;

        Color faceColor = new Color32(0, 210, 0, 255);
        TrySetColor(mat, "_FaceColor", faceColor);
        TrySetColor(mat, "_OutlineColor", Color.black);
        TrySetFloat(mat, "_OutlineWidth", 0.28f);
        TrySetFloat(mat, "_OutlineSoftness", 0f);
        TrySetFloat(mat, "_FaceDilate", 0.2f);
        TrySetFloat(mat, "_FaceSoftness", 0f);
        TrySetColor(mat, "_UnderlayColor", Color.black);
        TrySetFloat(mat, "_UnderlayDilate", 0.1f);
        TrySetFloat(mat, "_UnderlaySoftness", 0f);
        TrySetFloat(mat, "_UnderlayOffsetX", 0.25f);
        TrySetFloat(mat, "_UnderlayOffsetY", -0.25f);
    }

    void UpdateAllCursorsToSelected()
    {
        PlayerCursorState activeCursor = GetActiveSelectionCursor();

        for (int i = 0; i < players.Count; i++)
        {
            var ps = players[i];
            if (ps.cursorRt == null)
                continue;

            if (activeCursor == null || !ReferenceEquals(activeCursor, ps))
            {
                ps.cursorRt.gameObject.SetActive(false);
                continue;
            }

            int idx = ps.index;

            if (idx < 0 || idx >= slotRoots.Count || slotRoots[idx] == null)
            {
                ps.cursorRt.gameObject.SetActive(false);
                continue;
            }

            var slotRt = slotRoots[idx];

            ps.cursorRt.gameObject.SetActive(true);
            ps.cursorRt.SetParent(slotRt, false);
            ps.cursorRt.SetAsLastSibling();

            ps.cursorRt.anchorMin = new Vector2(0.5f, 0.5f);
            ps.cursorRt.anchorMax = new Vector2(0.5f, 0.5f);
            ps.cursorRt.pivot = new Vector2(0.5f, 0.5f);
            ps.cursorRt.anchoredPosition = new Vector2(0f, cursorYOffset);

            var baseSize = slotRt.rect.size;
            var targetSize = new Vector2(
                baseSize.x * cursorSizeMultiplier.x,
                baseSize.y * cursorSizeMultiplier.y
            ) + cursorPadding;

            ps.cursorRt.sizeDelta = targetSize;
            ps.cursorRt.localScale = Vector3.one;
            ps.cursorRt.localRotation = Quaternion.identity;
        }
    }

    void CycleOverlappedCursors()
    {
        overlapZTick++;

        for (int slot = 0; slot < slotRoots.Count; slot++)
        {
            var slotRt = slotRoots[slot];
            if (slotRt == null)
                continue;

            List<PlayerCursorState> here = null;

            for (int i = 0; i < players.Count; i++)
            {
                var ps = players[i];
                if (ps.cursorRt == null)
                    continue;
                if (!ps.cursorRt.gameObject.activeSelf)
                    continue;

                int idx = ps.confirmed ? ps.selectedIndex : ps.index;
                if (idx != slot)
                    continue;

                here ??= new List<PlayerCursorState>(4);
                here.Add(ps);
            }

            if (here == null || here.Count <= 1)
                continue;

            int n = here.Count;
            int shift = overlapZTick % n;

            for (int k = 0; k < n; k++)
            {
                int pick = (k + shift) % n;
                var ps = here[pick];
                if (ps.cursorRt == null)
                    continue;
                ps.cursorRt.SetSiblingIndex(1 + k);
            }
        }
    }

    void PlaySfx(AudioClip clip, float volume)
    {
        if (clip == null)
            return;

        var music = GameMusicController.Instance;
        if (music == null)
            return;

        music.PlaySfx(clip, volume);
    }

    void StartSelectMusic()
    {
        var music = GameMusicController.Instance;
        if (music == null || selectMusic == null)
            return;

        if (!capturedPreviousMusic)
        {
            var src = music.GetComponent<AudioSource>();
            if (src != null)
            {
                previousClip = src.clip;
                previousVolume = src.volume;
                previousLoop = src.loop;
                capturedPreviousMusic = true;
            }
        }

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

    void PreloadSelectMusic()
    {
        if (selectMusic != null && selectMusic.loadState == AudioDataLoadState.Unloaded)
            selectMusic.LoadAudioData();

        if (selectMusicLoop != null && selectMusicLoop.loadState == AudioDataLoadState.Unloaded)
            selectMusicLoop.LoadAudioData();
    }

    void StopSelectMusicAndRestorePrevious(bool restorePrevious)
    {
        var music = GameMusicController.Instance;
        if (music == null)
            return;

        if (!restorePrevious)
        {
            music.StopMusic();
            return;
        }

        if (!capturedPreviousMusic)
        {
            music.StopMusic();
            return;
        }

        if (previousClip != null)
            music.PlayMusic(previousClip, previousVolume, previousLoop);
        else
            music.StopMusic();
    }

    Sprite GetIdleSprite(BomberCharacter character, BomberSkin skin)
    {
        string key = GetSheetCacheKey(character, skin);
        if (idleCache.TryGetValue(key, out var cached))
            return cached;

        var s = GetSpriteByFrame(character, skin, idleFrameIndex);
        idleCache[key] = s;
        return s;
    }

    Sprite GetSpriteByFrame(BomberCharacter character, BomberSkin skin, int frameIndex)
    {
        var map = GetOrBuildFrameMap(character, skin);
        if (map == null)
            return null;

        if (map.TryGetValue(frameIndex, out var s))
            return s;

        return null;
    }

    Dictionary<int, Sprite> GetOrBuildFrameMap(BomberCharacter character, BomberSkin skin)
    {
        string cacheKey = GetSheetCacheKey(character, skin);
        if (sheetFrameCache.TryGetValue(cacheKey, out var map) && map != null)
            return map;

        string spritesResourcesPath = BomberSkinResourceCatalog.GetGeneratedResourcesPath(character);
        string sheetName = BomberSkinResourceCatalog.GetSheetName(character, skin);
        string sheetPath = $"{spritesResourcesPath}/{sheetName}";
        var sprites = Resources.LoadAll<Sprite>(sheetPath);

        map = new Dictionary<int, Sprite>();

        if (sprites != null && sprites.Length > 0)
        {
            for (int i = 0; i < sprites.Length; i++)
            {
                var sp = sprites[i];
                if (sp == null)
                    continue;

                string n = sp.name;
                int u = n.LastIndexOf('_');

                if (u < 0 || u >= n.Length - 1)
                    continue;

                if (int.TryParse(n.Substring(u + 1), out int idx))
                {
                    if (!map.ContainsKey(idx))
                        map.Add(idx, sp);
                }
            }
        }

        sheetFrameCache[cacheKey] = map;
        return map;
    }

    void SetFadeAlpha(float a)
    {
        if (fadeImage == null)
            return;

        var c = fadeImage.color;
        c.a = a;
        fadeImage.color = c;
    }

    void OnValidate()
    {
        EnsureSelectableCharacters();
    }

    IEnumerator FadeInRoutine()
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

    IEnumerator FadeOutRoutine()
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

    int MoveLeftWrap(int current)
    {
        int columnCount = VisibleColumnCount;
        int col = current % columnCount;
        int rowStart = current - col;

        if (col > 0)
            return current - 1;

        int lastInRow = Mathf.Min(rowStart + (columnCount - 1), SelectableSlotCount - 1);
        return lastInRow;
    }

    int MoveRightWrap(int current)
    {
        int columnCount = VisibleColumnCount;
        int col = current % columnCount;
        int rowStart = current - col;
        int rowEnd = Mathf.Min(rowStart + (columnCount - 1), SelectableSlotCount - 1);

        if (current < rowEnd)
            return current + 1;

        return rowStart;
    }

    int MoveUpWrap(int current)
    {
        int columnCount = VisibleColumnCount;
        int col = current % columnCount;
        int count = SelectableSlotCount;

        int prev = current - columnCount;
        if (prev >= 0)
            return prev;

        int lastRowStart = ((count - 1) / columnCount) * columnCount;
        int candidate = lastRowStart + col;

        while (candidate >= count)
            candidate -= columnCount;

        return Mathf.Clamp(candidate, 0, count - 1);
    }

    int MoveDownWrap(int current)
    {
        int columnCount = VisibleColumnCount;
        int col = current % columnCount;
        int count = SelectableSlotCount;

        int next = current + columnCount;
        if (next < count)
            return next;

        int candidate = col;

        while (candidate >= count)
            candidate -= columnCount;

        return Mathf.Clamp(candidate, 0, count - 1);
    }

    Canvas GetRootCanvas()
    {
        if (root != null)
            return root.GetComponentInParent<Canvas>();

        return GetComponentInParent<Canvas>();
    }

    Camera GetMainCameraSafe()
    {
        var cam = Camera.main;
        if (cam != null)
            return cam;

        var any = FindAnyObjectByType<Camera>();
        return any;
    }

    Rect GetReferencePixelRect(out string source)
    {
        var canvas = GetRootCanvas();
        if (canvas == null)
        {
            source = "NO_CANVAS";
            return new Rect(0, 0, Screen.width, Screen.height);
        }

        RectTransform rt = referenceRect;
        if (rt == null)
        {
            if (rootPanel != null) rt = rootPanel;
            else if (root != null) rt = root.GetComponent<RectTransform>();
        }

        if (rt == null)
        {
            source = "FALLBACK_SCREEN";
            return new Rect(0, 0, Screen.width, Screen.height);
        }

        source = rt.name;

        Rect px = RectTransformUtility.PixelAdjustRect(rt, canvas);
        if (px.width <= 1f || px.height <= 1f)
        {
            var r = rt.rect;
            px = new Rect(0, 0, r.width, r.height);
            source += "+rt.rect";
        }
        else
        {
            source += "+PixelAdjustRect";
        }

        return px;
    }

    float ComputeUiScaleForRect(float usedW, float usedH, out int baseScaleInt)
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

    static bool ApproximatelyRect(Rect a, Rect b)
    {
        return
            Mathf.Abs(a.x - b.x) < 0.01f &&
            Mathf.Abs(a.y - b.y) < 0.01f &&
            Mathf.Abs(a.width - b.width) < 0.01f &&
            Mathf.Abs(a.height - b.height) < 0.01f;
    }

    void CaptureBaseValuesIfNeeded()
    {
        if (_baseValuesCaptured)
            return;

        _baseCellSize = cellSize;
        _baseSpacing = spacing;
        _baseCursorPadding = cursorPadding;
        _baseCursorYOffset = cursorYOffset;
        _baseEndStageYOffset = endStageYOffset;
        _baseValuesCaptured = true;
    }

    void ApplyDynamicScaleIfNeeded(bool force = false)
    {
        CaptureBaseValuesIfNeeded();
        ApplyAutoFixesIfEnabled();

        int sw = Screen.width;
        int sh = Screen.height;

        var cam = GetMainCameraSafe();
        Rect camRect = cam != null ? cam.rect : new Rect(0, 0, 1, 1);

        Rect refPx = GetReferencePixelRect(out string refSource);

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

        cellSize = _baseCellSize * _currentUiScale;
        spacing = _baseSpacing * _currentUiScale;
        cursorPadding = _baseCursorPadding * _currentUiScale;
        cursorYOffset = _baseCursorYOffset * _currentUiScale;
        endStageYOffset = _baseEndStageYOffset * _currentUiScale;

        ApplyScaledLayout();
        UpdateFooterHintVisual();
        ApplyUnlockHintVisualStyle();

        float bottomOffset;
        if (referenceRect != null)
            bottomOffset = referenceRect.rect.height * unlockHintBottomPercentOfReferenceHeight;
        else
            bottomOffset = unlockHintBottomMargin * _currentUiScale;

        if (unlockHintRect != null)
        {
            unlockHintRect.anchoredPosition = new Vector2(0f, bottomOffset);
            unlockHintRect.sizeDelta = new Vector2(
                unlockHintWidth * _currentUiScale,
                unlockHintHeight * _currentUiScale
            );
        }

    }

    void ApplyScaledLayout()
    {
        if (gridRoot == null)
            return;

        if (gridRoot.TryGetComponent<GridLayoutGroup>(out var grid))
        {
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = VisibleColumnCount;
            grid.cellSize = cellSize;
            grid.spacing = spacing;
        }

        ApplySkinSpriteDisplaySizes();

        var gridRt = gridRoot as RectTransform;
        if (gridRt != null)
        {
            gridRt.anchorMin = new Vector2(0.5f, 0.5f);
            gridRt.anchorMax = new Vector2(0.5f, 0.5f);
            gridRt.pivot = new Vector2(0.5f, 0.5f);
            gridRt.anchoredPosition = gridBaseAnchoredPos * _currentUiScale;

            int visibleColumns = VisibleColumnCount;
            int rows = Mathf.CeilToInt(SelectableSlotCount / (float)visibleColumns);
            float totalW = visibleColumns * cellSize.x + (visibleColumns - 1) * spacing.x;
            float totalH = rows * cellSize.y + (rows - 1) * spacing.y;

            gridRt.sizeDelta = new Vector2(totalW, totalH);
        }
    }

    Vector2 GetSkinSpriteDisplaySize()
    {
        return cellSize * Mathf.Max(0.1f, skinSpriteScaleMultiplier);
    }

    public float GetPreviewMovementFrameSeconds()
    {
        return Mathf.Max(0.01f, downFrameTime);
    }

    public Vector2 GetPreviewSkinDisplaySize()
    {
        ApplyDynamicScaleIfNeeded();
        return GetSkinSpriteDisplaySize();
    }

    Vector2 GetGridCursorDisplaySize()
    {
        return new Vector2(
            cellSize.x * cursorSizeMultiplier.x,
            cellSize.y * cursorSizeMultiplier.y
        ) + cursorPadding;
    }

    void ApplySkinSpriteDisplaySizes()
    {
        Vector2 displaySize = GetSkinSpriteDisplaySize();

        for (int i = 0; i < slotImages.Count; i++)
        {
            Image img = slotImages[i];
            if (img == null || img.rectTransform == null)
                continue;

            RectTransform imgRt = img.rectTransform;
            imgRt.anchorMin = new Vector2(0.5f, 0.5f);
            imgRt.anchorMax = new Vector2(0.5f, 0.5f);
            imgRt.pivot = new Vector2(0.5f, 0.5f);
            imgRt.sizeDelta = displaySize;
        }
    }

    void ApplyAutoFixesIfEnabled()
    {
        if (forceRootPanelStretchToParent && rootPanel != null)
            StretchToParent(rootPanel);

        if (forceBackgroundStretchToRootPanel && backgroundImage != null)
            StretchToParent(backgroundImage.rectTransform);
    }

    static void StretchToParent(RectTransform rt)
    {
        if (rt == null)
            return;

        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
    }

    void ResetBackgroundSpriteSwap()
    {
        _backgroundSpriteIndex = 0;
        _backgroundSwapTimer = 0f;
    }

    void TickBackgroundSpriteSwap()
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

        _backgroundSwapTimer += Time.unscaledDeltaTime;

        if (_backgroundSwapTimer < Mathf.Max(0.01f, backgroundSwapInterval))
            return;

        _backgroundSwapTimer = 0f;
        _backgroundSpriteIndex++;

        if (_backgroundSpriteIndex >= backgroundSprites.Length)
        {
            if (backgroundSwapLoop)
                _backgroundSpriteIndex = 0;
            else
                _backgroundSpriteIndex = backgroundSprites.Length - 1;
        }

        ApplyCurrentBackgroundSprite();
    }

    void ApplyCurrentBackgroundSprite()
    {
        if (backgroundImage == null)
            return;

        if (backgroundSprites == null || backgroundSprites.Length == 0)
            return;

        _backgroundSpriteIndex = Mathf.Clamp(_backgroundSpriteIndex, 0, backgroundSprites.Length - 1);

        if (backgroundSprites[_backgroundSpriteIndex] != null)
            backgroundImage.sprite = backgroundSprites[_backgroundSpriteIndex];
    }

    void EnsureUnlockHintText()
    {
        if (root == null)
            return;

        if (unlockHintText == null)
        {
            GameObject go = new GameObject("UnlockHintText", typeof(RectTransform));
            go.transform.SetParent(root.transform, false);

            unlockHintText = go.AddComponent<TextMeshProUGUI>();
            unlockHintText.raycastTarget = false;

        }

        unlockHintRect = unlockHintText.rectTransform;

        unlockHintRect.anchorMin = new Vector2(0.5f, 0f);
        unlockHintRect.anchorMax = new Vector2(0.5f, 0f);
        unlockHintRect.pivot = new Vector2(0.5f, 0f);

        float bottomOffset = 0f;

        if (referenceRect != null)
            bottomOffset = referenceRect.rect.height * unlockHintBottomPercentOfReferenceHeight;
        else
            bottomOffset = unlockHintBottomMargin * _currentUiScale;

        unlockHintRect.anchoredPosition = new Vector2(0f, bottomOffset);
        unlockHintRect.sizeDelta = new Vector2(
            unlockHintWidth * _currentUiScale,
            unlockHintHeight * _currentUiScale
        );
        unlockHintRect.localScale = Vector3.one;

        if (unlockHintFontAsset == null)
            unlockHintFontAsset = Resources.Load<TMP_FontAsset>("Font/Retro Gaming SDF");

        ApplyUnlockHintVisualStyle();

        unlockHintText.text = string.Empty;
        unlockHintText.gameObject.SetActive(false);
        _lastUnlockHintMessage = string.Empty;

    }

    void ApplyUnlockHintVisualStyle()
    {
        if (unlockHintText == null)
            return;

        if (unlockHintFontAsset == null)
            unlockHintFontAsset = Resources.Load<TMP_FontAsset>("Font/Retro Gaming SDF");

        if (unlockHintFontAsset != null)
            unlockHintText.font = unlockHintFontAsset;

        unlockHintText.alignment = TextAlignmentOptions.Center;
        unlockHintText.textWrappingMode = TextWrappingModes.Normal;
        unlockHintText.overflowMode = TextOverflowModes.Overflow;
        unlockHintText.extraPadding = true;
        unlockHintText.fontSize = Mathf.Clamp(Mathf.RoundToInt(unlockHintFontSize * _currentUiScale), 8, 300);
        unlockHintText.color = unlockHintFaceColor;
        unlockHintText.margin = Vector4.zero;
        unlockHintText.raycastTarget = false;

        if (forceUnlockHintBold)
            unlockHintText.fontStyle |= FontStyles.Bold;
        else
            unlockHintText.fontStyle &= ~FontStyles.Bold;

        Material runtimeMat = GetOrCreateUnlockHintRuntimeMaterial();
        ApplyUnlockHintMaterialStyle(runtimeMat);

        if (runtimeMat != null)
            unlockHintText.fontMaterial = runtimeMat;

        unlockHintText.UpdateMeshPadding();
        unlockHintText.ForceMeshUpdate();
        unlockHintText.SetVerticesDirty();
    }

    Material GetOrCreateUnlockHintRuntimeMaterial()
    {
        if (unlockHintText == null)
            return null;

        if (unlockHintRuntimeMaterial != null)
            return unlockHintRuntimeMaterial;

        Material baseMat = null;

        if (unlockHintFontMaterialPreset != null)
            baseMat = unlockHintFontMaterialPreset;
        else if (unlockHintText.fontSharedMaterial != null)
            baseMat = unlockHintText.fontSharedMaterial;
        else if (unlockHintText.font != null)
            baseMat = unlockHintText.font.material;

        if (baseMat == null)
            return null;

        unlockHintRuntimeMaterial = new Material(baseMat);
        unlockHintRuntimeMaterial.name = baseMat.name + "_SkinUnlockHintRuntime";
        return unlockHintRuntimeMaterial;
    }

    void ApplyUnlockHintMaterialStyle(Material mat)
    {
        if (mat == null)
            return;

        TrySetColor(mat, "_FaceColor", unlockHintFaceColor);

        if (useUnlockHintOutline)
        {
            TrySetColor(mat, "_OutlineColor", unlockHintOutlineColor);
            TrySetFloat(mat, "_OutlineWidth", unlockHintOutlineWidth);
            TrySetFloat(mat, "_OutlineSoftness", unlockHintOutlineSoftness);
        }
        else
        {
            TrySetFloat(mat, "_OutlineWidth", 0f);
            TrySetFloat(mat, "_OutlineSoftness", 0f);
        }

        TrySetFloat(mat, "_FaceDilate", unlockHintFaceDilate);
        TrySetFloat(mat, "_FaceSoftness", unlockHintFaceSoftness);

        if (enableUnlockHintUnderlay)
        {
            TrySetColor(mat, "_UnderlayColor", unlockHintUnderlayColor);
            TrySetFloat(mat, "_UnderlayDilate", unlockHintUnderlayDilate);
            TrySetFloat(mat, "_UnderlaySoftness", unlockHintUnderlaySoftness);
            TrySetFloat(mat, "_UnderlayOffsetX", unlockHintUnderlayOffsetX);
            TrySetFloat(mat, "_UnderlayOffsetY", unlockHintUnderlayOffsetY);
        }
        else
        {
            TrySetFloat(mat, "_UnderlayDilate", 0f);
            TrySetFloat(mat, "_UnderlaySoftness", 0f);
            TrySetFloat(mat, "_UnderlayOffsetX", 0f);
            TrySetFloat(mat, "_UnderlayOffsetY", 0f);
        }
    }

    void ShowUnlockHint(string message)
    {
        EnsureUnlockHintText();

        if (unlockHintText == null)
            return;

        if (string.IsNullOrWhiteSpace(message))
        {
            HideUnlockHintImmediate();
            return;
        }

        ApplyUnlockHintVisualStyle();

        LocalizedTmpFontFallback.Apply(unlockHintText);
        unlockHintText.text = message;
        unlockHintText.gameObject.SetActive(true);
        unlockHintText.transform.SetAsLastSibling();

        if (unlockHintRect != null)
        {
            float width = unlockHintWidth * _currentUiScale;
            float baseHeight = unlockHintHeight * _currentUiScale;

            unlockHintRect.sizeDelta = new Vector2(width, baseHeight);

            unlockHintText.ForceMeshUpdate();

            float preferredHeight = unlockHintText.GetPreferredValues(message, width, 0f).y;
            float minHeight = unlockHintMinHeight * _currentUiScale;
            float finalHeight = Mathf.Max(minHeight, preferredHeight);

            unlockHintRect.sizeDelta = new Vector2(width, finalHeight);

        }

        _lastUnlockHintMessage = message;
    }

    void HideUnlockHintImmediate()
    {
        if (unlockHintText == null)
            return;

        unlockHintText.text = string.Empty;
        unlockHintText.gameObject.SetActive(false);
        _lastUnlockHintMessage = string.Empty;
    }

    PlayerCursorState GetLowestUnconfirmedCursor()
    {
        if (players == null || players.Count == 0)
            return null;

        PlayerCursorState lowest = null;

        for (int i = 0; i < players.Count; i++)
        {
            PlayerCursorState ps = players[i];

            if (ps == null)
                continue;

            if (ps.confirmed)
                continue;

            if (ps.index < 0 || ps.index >= SelectableSlotCount)
                continue;

            if (lowest == null || ps.playerId < lowest.playerId)
                lowest = ps;
        }

        return lowest;
    }

    void UpdateUnlockHint()
    {
        if (unlockHintText == null)
            return;

        PlayerCursorState hintCursor = GetLowestUnconfirmedCursor();

        if (hintCursor == null)
        {
            HideUnlockHintImmediate();
            return;
        }

        int index = hintCursor.index;

        if (index < 0 || index >= SelectableSlotCount)
        {
            HideUnlockHintImmediate();
            return;
        }

        BomberSkin skin = GetCursorPalette(hintCursor);

        if (UnlockProgress.IsUnlocked(skin))
        {
            HideUnlockHintImmediate();
            return;
        }

        string hint = SkinUnlockHintCatalog.GetHint(skin);

        if (string.IsNullOrWhiteSpace(hint))
        {
            HideUnlockHintImmediate();
            return;
        }

        if (_lastUnlockHintMessage == hint && unlockHintText.gameObject.activeSelf)
            return;

        ShowUnlockHint(hint);
    }

    static void TrySetFloat(Material mat, string prop, float value)
    {
        if (mat != null && mat.HasProperty(prop))
            mat.SetFloat(prop, value);
    }

    static void TrySetColor(Material mat, string prop, Color value)
    {
        if (mat != null && mat.HasProperty(prop))
            mat.SetColor(prop, value);
    }

}

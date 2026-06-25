using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class BomberSkinSelectMenu : MonoBehaviour
{
    const string LOG = "[BomberSkinSelectMenu]";

    [Header("Debug (Surgical Logs)")]
    [SerializeField] bool enableSurgicalLogs = false;
    [SerializeField] bool logHintSpacingEveryUpdate = false;

    [Header("Auto Fix Layout")]
    [SerializeField] bool forceRootPanelStretchToParent = false;
    [SerializeField] bool forceBackgroundStretchToRootPanel = false;

    [Header("UI")]
    [SerializeField] GameObject root;
    [SerializeField] Image backgroundImage;

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
    [SerializeField] bool logCursorPositionDebug = true;
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
    [SerializeField] string spritesResourcesPath = "Sprites/Bombers/Bomberman";
    [SerializeField] int idleFrameIndex = 16;

    [Header("Preview Animations")]
    [SerializeField] float downFrameTime = 0.22f;
    [SerializeField] float endStageFrameTime = 0.1f;
    [SerializeField] int[] downFrames = new[] { 14, 16, 18, 16 };
    [SerializeField] int[] endStageFrames = new[] { 148, 148, 146, 148, 147, 148, 146, 148, 147, 147 };

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
    string _lastUnlockHintMessage = string.Empty;

    readonly Vector3[] _hintWorldCorners = new Vector3[4];
    readonly Vector3[] _refWorldCorners = new Vector3[4];

    [Header("Skins (menu order)")]
    [SerializeField]
    List<BomberSkin> selectableSkins = new()
    {
        BomberSkin.Golden,
        BomberSkin.White,
        BomberSkin.Gray,
        BomberSkin.Black,
        BomberSkin.Red,
        BomberSkin.Orange,
        BomberSkin.Yellow,
        BomberSkin.Olive,
        BomberSkin.Green,
        BomberSkin.Cyan,
        BomberSkin.Aqua,
        BomberSkin.Blue,
        BomberSkin.DarkBlue,
        BomberSkin.Purple,
        BomberSkin.Magenta,
        BomberSkin.Pink,
        BomberSkin.Brown,
        BomberSkin.DarkGreen,
        BomberSkin.Nightmare,
        BomberSkin.Gold
    };

    readonly Dictionary<BomberSkin, Sprite> idleCache = new();
    readonly Dictionary<BomberSkin, Dictionary<int, Sprite>> sheetFrameCache = new();

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
    int cursorPositionDebugFramesRemaining;
    string cursorPositionDebugContext = string.Empty;

    sealed class PlayerCursorState
    {
        public int playerId;
        public int inputPlayerId;
        public int index;
        public bool confirmed;
        public BomberSkin selected;
        public int selectedIndex = -1;
        public bool battleComCursor;
        public RectTransform cursorRt;
        public Image cursorImg;
    }

    readonly List<PlayerCursorState> players = new();
    readonly List<int> configuredPlayerIds = new(GameSession.MaxPlayerId);
    readonly List<int> battleSkinTargetPlayerIds = new(GameSession.MaxPlayerId);
    readonly List<int> battleComQueue = new(GameSession.MaxPlayerId);
    readonly bool[,] previousDirectionalHeld = new bool[GameSession.MaxPlayerId + 1, 4];
    readonly float[,] nextDirectionalRepeatTime = new float[GameSession.MaxPlayerId + 1, 4];
    readonly Dictionary<int, List<PlayerCursorState>> battleConfirmedByInput = new();
    int nextBattleComQueueIndex;

    bool menuActive;

    void Awake()
    {
        if (root == null)
            root = gameObject;

        CaptureBaseValuesIfNeeded();
        ApplyAutoFixesIfEnabled();

        ApplyCurrentBackgroundSprite();

        BuildGrid();
        ApplyDynamicScaleIfNeeded(true);
        EnsureUnlockHintText();

        SLog(
            $"Awake | root={(root != null ? root.name : "NULL")} " +
            $"referenceRect={(referenceRect != null ? referenceRect.name : "NULL")} " +
            $"rootPanel={(rootPanel != null ? rootPanel.name : "NULL")} " +
            $"uiScale={_currentUiScale:0.###} baseScaleInt={_currentBaseScaleInt}"
        );

        if (root != null)
            root.SetActive(false);
    }

    void OnDestroy()
    {
        if (unlockHintRuntimeMaterial != null)
            Destroy(unlockHintRuntimeMaterial);
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
        UpdateUnlockHint();

        if (logHintSpacingEveryUpdate)
            DumpHintSpacing("Update");
    }

    void LateUpdate()
    {
        if (!menuActive)
            return;

        Canvas.ForceUpdateCanvases();
        UpdateAllCursorsToSelected();
        CycleOverlappedCursors();

        if (logHintSpacingEveryUpdate)
            DumpHintSpacing("LateUpdate");
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

        if (gridRoot != null)
            gridRoot.gameObject.SetActive(false);

        SLog("Hide | menuActive=false");

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

        if (slotImages.Count != selectableSkins.Count)
            BuildGrid();

        PlayerPersistentStats.EnsureSessionBooted();

        ReturnToTitleRequested = false;

        downTimer = 0f;
        downFrameIdx = 0;

        cursorBlinkT = 0f;
        overlapZTick = 0;

        RestoreAllSlotPositions();
        endStageBySlot.Clear();

        selectedBySlot = new int[selectableSkins.Count];
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

        PreloadIdleSprites();

        for (int i = 0; i < players.Count; i++)
            InitializeCursorSelection(players[i]);

        if (resumeFromSavedSkins)
            PreconfirmBattleModeSelectionFromSavedSkins();

        UpdateSlotVisuals();
        ApplyFinalEndStageVisualsImmediate();
        UpdateUnlockHint();

        Canvas.ForceUpdateCanvases();
        if (gridRoot is RectTransform gridRtAfter)
            LayoutRebuilder.ForceRebuildLayoutImmediate(gridRtAfter);
        Canvas.ForceUpdateCanvases();

        UpdateSlotVisuals();
        ApplyFinalEndStageVisualsImmediate();
        UpdateAllCursorsToSelected();
        CycleOverlappedCursors();

        Canvas.ForceUpdateCanvases();

        if (shouldRestoreCanvasGroupAlpha && temporaryHiddenCanvasGroup != null)
            temporaryHiddenCanvasGroup.alpha = previousCanvasGroupAlpha;

        menuActive = true;
        ResetDirectionalRepeatState();

        DumpHintSpacing("SelectSkinRoutine.BeforeFadeIn");

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
            int cursorCountThisFrame = players.Count;

            if (useBattleModeComAssignment && HandleCompletedBattleModeBackInputs(input))
            {
                PlaySfx(returnSfx, returnSfxVolume);
                UpdateSlotVisuals();
                ApplyFinalEndStageVisualsImmediate();
                UpdateUnlockHint();
                yield return null;
                continue;
            }

            for (int i = 0; i < cursorCountThisFrame && i < players.Count; i++)
            {
                var ps = players[i];
                if (ps.confirmed)
                {
                    if (!useBattleModeComAssignment)
                    {
                        int confirmedPid = ps.playerId;
                        if (input != null && input.GetDown(confirmedPid, PlayerAction.ActionB))
                        {
                            DeselectPlayer(confirmedPid);
                            PlaySfx(returnSfx, returnSfxVolume);
                            UpdateSlotVisuals();
                            ApplyFinalEndStageVisualsImmediate();
                            UpdateUnlockHint();
                        }
                    }

                    continue;
                }

                int pid = GetCursorInputPlayerId(ps);

                bool upDown = DirectionalPressed(input, pid, PlayerAction.MoveUp);
                bool downDown = DirectionalPressed(input, pid, PlayerAction.MoveDown);
                bool leftDown = DirectionalPressed(input, pid, PlayerAction.MoveLeft);
                bool rightDown = DirectionalPressed(input, pid, PlayerAction.MoveRight);
                bool aDown = input != null && input.GetDown(pid, PlayerAction.ActionA);
                bool bDown = input != null && input.GetDown(pid, PlayerAction.ActionB);
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
                        PlaySfx(moveCursorSfx, moveCursorSfxVolume);
                        UpdateSlotVisuals();
                        ApplyFinalEndStageVisualsImmediate();
                        UpdateUnlockHint();
                    }
                }
                else if (backPressed)
                {
                    if (useBattleModeComAssignment)
                    {
                        if (HandleBattleModeBack(ps))
                        {
                            PlaySfx(returnSfx, returnSfxVolume);
                            UpdateSlotVisuals();
                            ApplyFinalEndStageVisualsImmediate();
                            UpdateUnlockHint();
                            continue;
                        }
                    }

                    anyReturnToTitle = true;
                }
                else if (confirmPressed)
                {
                    TryConfirm(ps);
                    UpdateSlotVisuals();
                    ApplyFinalEndStageVisualsImmediate();
                    UpdateUnlockHint();

                    if (ps.confirmed && useBattleModeComAssignment)
                        AssignNextBattleComCursor(pid);

                    if (AllPlayersConfirmed())
                    {
                        fadeDuration = fadeOutOnConfirmDuration;
                        done = true;
                        break;
                    }
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

    public Sprite GetBattleModeTeamPreviewSprite(BomberSkin skin, int frameIndex)
    {
        int frame = idleFrameIndex;
        if (downFrames != null && downFrames.Length > 0)
            frame = downFrames[Mathf.Abs(frameIndex) % downFrames.Length];

        return GetSpriteByFrame(skin, frame) ?? GetIdleSprite(skin);
    }

    public Sprite GetBattleModeTeamCelebrationSprite(BomberSkin skin, int frameIndex)
    {
        int frame = idleFrameIndex;
        if (endStageFrames != null && endStageFrames.Length > 0)
            frame = endStageFrames[Mathf.Clamp(frameIndex, 0, endStageFrames.Length - 1)];

        return GetSpriteByFrame(skin, frame) ?? GetIdleSprite(skin);
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

        int slot = Mathf.Clamp(ps.index, 0, selectableSkins.Count - 1);
        var skin = selectableSkins[slot];

        if (!UnlockProgress.IsUnlocked(skin))
        {
            PlaySfx(lockedConfirmSfx, lockedConfirmSfxVolume);
            return;
        }

        int owner = GetSelectedOwner(slot);
        if (owner != 0 && owner != ps.playerId)
        {
            PlaySfx(lockedConfirmSfx, lockedConfirmSfxVolume);
            return;
        }

        selectedBySlot[slot] = ps.playerId;

        ps.selected = skin;
        ps.selectedIndex = slot;
        ps.confirmed = true;

        PlayerPersistentStats.Get(ps.playerId).Skin = skin;
        PlayerPersistentStats.SaveSelectedSkin(ps.playerId);

        if (useBattleModeComAssignment)
            PushBattleConfirmedCursor(ps);

        StartEndStageForSlot(slot, skin);

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
            return;

        int slot = ps.selectedIndex;

        if (slot >= 0 && slot < selectedBySlot.Length && selectedBySlot[slot] == ps.playerId)
            selectedBySlot[slot] = 0;

        StopEndStageForSlot(slot);

        ps.confirmed = false;
        ps.selectedIndex = -1;
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

    void StartEndStageForSlot(int slotIndex, BomberSkin skin)
    {
        if (!endStageBySlot.TryGetValue(slotIndex, out var st) || st == null)
        {
            st = new EndStageState();
            endStageBySlot[slotIndex] = st;
        }

        st.slotIndex = slotIndex;
        st.skin = skin;
        st.timer = 0f;
        st.frameIdx = 0;
        st.loopsDone = 0;
        st.stopped = false;
        st.baseCaptured = false;
    }

    void StartEndStageFinalForSlot(int slotIndex, BomberSkin skin)
    {
        if (!endStageBySlot.TryGetValue(slotIndex, out var st) || st == null)
        {
            st = new EndStageState();
            endStageBySlot[slotIndex] = st;
        }

        int finalFrameIndex = 0;
        if (endStageFrames != null && endStageFrames.Length > 0)
            finalFrameIndex = endStageFrames.Length - 1;

        st.slotIndex = slotIndex;
        st.skin = skin;
        st.timer = 0f;
        st.frameIdx = finalFrameIndex;
        st.loopsDone = Mathf.Max(1, endStageLoopsToStop);
        st.stopped = true;

        if (slotIndex >= 0 && slotIndex < slotImages.Count)
        {
            Image img = slotImages[slotIndex];
            if (img != null && img.rectTransform != null)
            {
                st.baseAnchoredPos = Vector2.zero;
                st.baseCaptured = true;

                img.rectTransform.anchoredPosition = st.baseAnchoredPos + new Vector2(0f, endStageYOffset);

                if (endStageFrames != null && endStageFrames.Length > 0)
                {
                    int frame = endStageFrames[Mathf.Clamp(finalFrameIndex, 0, endStageFrames.Length - 1)];
                    img.sprite = GetSpriteByFrame(skin, frame) ?? GetIdleSprite(skin);
                }
                else
                {
                    img.sprite = GetIdleSprite(skin);
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

            if (endStageFrames != null && endStageFrames.Length > 0)
            {
                int frameIndex = Mathf.Clamp(st.frameIdx, 0, endStageFrames.Length - 1);
                int frame = endStageFrames[frameIndex];
                img.sprite = GetSpriteByFrame(st.skin, frame) ?? GetIdleSprite(st.skin);
            }
            else
            {
                img.sprite = GetIdleSprite(st.skin);
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
        if (endStageFrames == null || endStageFrames.Length == 0)
            return;

        float ft = Mathf.Max(0.001f, endStageFrameTime);

        foreach (var kv in endStageBySlot)
        {
            var st = kv.Value;
            if (st == null || st.stopped)
                continue;

            st.timer += Time.unscaledDeltaTime;

            while (st.timer >= ft)
            {
                st.timer -= ft;

                int next = st.frameIdx + 1;

                if (next >= endStageFrames.Length)
                {
                    st.loopsDone++;

                    if (st.loopsDone >= Mathf.Max(1, endStageLoopsToStop))
                    {
                        st.stopped = true;
                        st.frameIdx = endStageFrames.Length - 1;
                        break;
                    }

                    next = 0;
                }

                st.frameIdx = next;
            }
        }
    }

    void UpdateSlotVisuals()
    {
        if (selectableSkins == null || selectableSkins.Count == 0)
            return;

        for (int i = 0; i < slotImages.Count; i++)
        {
            var img = slotImages[i];
            if (img == null)
                continue;

            var skin = selectableSkins[i];
            bool unlocked = UnlockProgress.IsUnlocked(skin);
            bool selected = IsSlotSelected(i);

            bool anyCursorHere = false;
            for (int p = 0; p < players.Count; p++)
            {
                if (!players[p].confirmed && players[p].index == i)
                {
                    anyCursorHere = true;
                    break;
                }
            }

            if (!unlocked)
                img.color = lockedTint;
            else if (selected || anyCursorHere)
                img.color = selectedTint;
            else
                img.color = normalTint;

            if (selected && endStageBySlot.TryGetValue(i, out var st) && st != null)
            {
                var rt = img.rectTransform;
                if (rt != null)
                {
                    if (!st.baseCaptured)
                    {
                        st.baseAnchoredPos = rt.anchoredPosition;
                        st.baseCaptured = true;
                    }

                    rt.anchoredPosition = st.baseAnchoredPos + new Vector2(0f, endStageYOffset);
                }

                int f = endStageFrames[Mathf.Clamp(st.frameIdx, 0, endStageFrames.Length - 1)];
                img.sprite = GetSpriteByFrame(st.skin, f) ?? GetIdleSprite(st.skin);
            }
            else
            {
                if (img.rectTransform != null)
                    img.rectTransform.anchoredPosition = Vector2.zero;

                if (unlocked && anyCursorHere && !selected)
                {
                    int f = downFrames != null && downFrames.Length > 0
                        ? downFrames[Mathf.Clamp(downFrameIdx, 0, downFrames.Length - 1)]
                        : idleFrameIndex;

                    img.sprite = GetSpriteByFrame(skin, f) ?? GetIdleSprite(skin);
                }
                else
                {
                    img.sprite = GetIdleSprite(skin);
                }
            }

            img.enabled = img.sprite != null;
        }
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

        for (int i = 0; i < activePlayerIds.Count; i++)
        {
            int p = Mathf.Clamp(activePlayerIds[i], GameSession.MinPlayerId, GameSession.MaxPlayerId);
            var st = CreateCursorState(p, p, false);
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

    static string FormatPlayerIds(List<int> playerIds)
    {
        if (playerIds == null || playerIds.Count == 0)
            return string.Empty;

        string s = string.Empty;
        for (int i = 0; i < playerIds.Count; i++)
        {
            if (i > 0)
                s += ",";
            s += playerIds[i].ToString();
        }

        return s;
    }

    static string FormatVec2(Vector2 value)
    {
        return $"({value.x:0.##},{value.y:0.##})";
    }

    static string FormatVec3(Vector3 value)
    {
        return $"({value.x:0.##},{value.y:0.##},{value.z:0.##})";
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

            if (mode == BattleModePlayerControlMode.Man)
                results.Add(playerId);
            else if (mode == BattleModePlayerControlMode.Com)
                battleComQueue.Add(playerId);
        }

        if (battleSkinTargetPlayerIds.Count <= 0)
            battleSkinTargetPlayerIds.Add(GameSession.MinPlayerId);

        if (results.Count <= 0)
            results.Add(battleSkinTargetPlayerIds[0]);

        for (int i = 0; i < results.Count; i++)
            battleComQueue.Remove(results[i]);
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
            return false;

        if (activeCursor != null && activeCursor.battleComCursor && !activeCursor.confirmed)
        {
            if (activeCursor.playerId >= GameSession.MinPlayerId &&
                activeCursor.playerId <= GameSession.MaxPlayerId &&
                nextBattleComQueueIndex > 0)
            {
                nextBattleComQueueIndex = Mathf.Max(0, nextBattleComQueueIndex - 1);
            }

            RemoveCursor(activeCursor);
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

        for (int playerId = GameSession.MinPlayerId; playerId <= GameSession.MaxPlayerId; playerId++)
        {
            if (!input.GetDown(playerId, PlayerAction.ActionB))
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
        if (cursor == null)
            return;

        int idx;
        if (staggerCursorStartByPlayer)
            idx = (cursor.playerId - 1) % selectableSkins.Count;
        else
        {
            idx = selectableSkins.IndexOf(PlayerPersistentStats.Get(cursor.playerId).Skin);
            if (idx < 0) idx = 0;
        }

        cursor.index = idx;
        cursor.selected = PlayerPersistentStats.Get(cursor.playerId).Skin;
        cursor.confirmed = false;
        cursor.selectedIndex = -1;
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

        BomberSkin savedSkin = PlayerPersistentStats.Get(cursor.playerId).Skin;
        int index = selectableSkins.IndexOf(savedSkin);

        if (index < 0)
            index = Mathf.Clamp(cursor.playerId - 1, 0, selectableSkins.Count - 1);

        cursor.index = index;
        cursor.selected = selectableSkins[Mathf.Clamp(index, 0, selectableSkins.Count - 1)];
        cursor.selectedIndex = -1;
        cursor.confirmed = false;
    }

    void ForceConfirmCursorWithoutSfx(PlayerCursorState cursor)
    {
        if (cursor == null)
            return;

        int slot = Mathf.Clamp(cursor.index, 0, selectableSkins.Count - 1);
        BomberSkin skin = selectableSkins[slot];

        if (!UnlockProgress.IsUnlocked(skin))
            return;

        int owner = GetSelectedOwner(slot);
        if (owner != 0 && owner != cursor.playerId)
            return;

        selectedBySlot[slot] = cursor.playerId;

        cursor.selected = skin;
        cursor.selectedIndex = slot;
        cursor.confirmed = true;

        PlayerPersistentStats.Get(cursor.playerId).Skin = skin;

        PushBattleConfirmedCursor(cursor);

        StartEndStageFinalForSlot(slot, skin);

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
            grid.constraintCount = Mathf.Max(1, columns);
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

        for (int i = 0; i < selectableSkins.Count; i++)
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
            imgRt.anchorMin = Vector2.zero;
            imgRt.anchorMax = Vector2.one;
            imgRt.pivot = new Vector2(0.5f, 0.5f);
            imgRt.offsetMin = Vector2.zero;
            imgRt.offsetMax = Vector2.zero;
            imgRt.localScale = Vector3.one;
            imgRt.localRotation = Quaternion.identity;

            slotRoots.Add(slotRt);
            slotImages.Add(img);
        }
    }

    void UpdateAllCursorsToSelected()
    {
        bool shouldLog = logCursorPositionDebug && cursorPositionDebugFramesRemaining > 0;

        for (int i = 0; i < players.Count; i++)
        {
            var ps = players[i];
            if (ps.cursorRt == null)
                continue;

            int idx = ps.confirmed ? ps.selectedIndex : ps.index;

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

        if (shouldLog)
            cursorPositionDebugFramesRemaining--;
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

    Sprite GetIdleSprite(BomberSkin skin)
    {
        if (idleCache.TryGetValue(skin, out var cached))
            return cached;

        var s = GetSpriteByFrame(skin, idleFrameIndex);
        idleCache[skin] = s;
        return s;
    }

    Sprite GetSpriteByFrame(BomberSkin skin, int frameIndex)
    {
        var map = GetOrBuildFrameMap(skin);
        if (map == null)
            return null;

        if (map.TryGetValue(frameIndex, out var s))
            return s;

        return null;
    }

    Dictionary<int, Sprite> GetOrBuildFrameMap(BomberSkin skin)
    {
        if (sheetFrameCache.TryGetValue(skin, out var map) && map != null)
            return map;

        string sheetName = skin + "Bomber";
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

        sheetFrameCache[skin] = map;
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

    void PreloadIdleSprites()
    {
        for (int i = 0; i < selectableSkins.Count; i++)
            GetIdleSprite(selectableSkins[i]);
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
        int col = current % columns;
        int rowStart = current - col;

        if (col > 0)
            return current - 1;

        int lastInRow = Mathf.Min(rowStart + (columns - 1), selectableSkins.Count - 1);
        return lastInRow;
    }

    int MoveRightWrap(int current)
    {
        int col = current % columns;
        int rowStart = current - col;
        int rowEnd = Mathf.Min(rowStart + (columns - 1), selectableSkins.Count - 1);

        if (current < rowEnd)
            return current + 1;

        return rowStart;
    }

    int MoveUpWrap(int current)
    {
        int col = current % columns;
        int count = selectableSkins.Count;

        int prev = current - columns;
        if (prev >= 0)
            return prev;

        int lastRowStart = ((count - 1) / columns) * columns;
        int candidate = lastRowStart + col;

        while (candidate >= count)
            candidate -= columns;

        return Mathf.Clamp(candidate, 0, count - 1);
    }

    int MoveDownWrap(int current)
    {
        int col = current % columns;
        int count = selectableSkins.Count;

        int next = current + columns;
        if (next < count)
            return next;

        int candidate = col;

        while (candidate >= count)
            candidate -= columns;

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
        ApplyUnlockHintVisualStyle();

        float bottomOffsetForLog;
        if (referenceRect != null)
            bottomOffsetForLog = referenceRect.rect.height * unlockHintBottomPercentOfReferenceHeight;
        else
            bottomOffsetForLog = unlockHintBottomMargin * _currentUiScale;

        if (unlockHintRect != null)
        {
            unlockHintRect.anchoredPosition = new Vector2(0f, bottomOffsetForLog);
            unlockHintRect.sizeDelta = new Vector2(
                unlockHintWidth * _currentUiScale,
                unlockHintHeight * _currentUiScale
            );
        }

        SLog(
            $"ApplyDynamicScaleIfNeeded | " +
            $"Screen=({sw}x{sh}) camRect={camRect} " +
            $"refSource={refSource} refPx={refPx} " +
            $"uiScale={_currentUiScale:0.###} baseScaleInt={_currentBaseScaleInt} " +
            $"hintBottomMargin={unlockHintBottomMargin:0.###} " +
            $"hintBottomPercent={unlockHintBottomPercentOfReferenceHeight:0.######} " +
            $"resolvedBottomOffset={bottomOffsetForLog:0.###} " +
            $"hintWidth={unlockHintWidth:0.###} scaledHintWidth={unlockHintWidth * _currentUiScale:0.###} " +
            $"hintHeight={unlockHintHeight:0.###} scaledHintHeight={unlockHintHeight * _currentUiScale:0.###}"
        );

        DumpHintSpacing("ApplyDynamicScaleIfNeeded");
    }

    void ApplyScaledLayout()
    {
        if (gridRoot == null)
            return;

        if (gridRoot.TryGetComponent<GridLayoutGroup>(out var grid))
        {
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = Mathf.Max(1, columns);
            grid.cellSize = cellSize;
            grid.spacing = spacing;
        }

        var gridRt = gridRoot as RectTransform;
        if (gridRt != null)
        {
            gridRt.anchorMin = new Vector2(0.5f, 0.5f);
            gridRt.anchorMax = new Vector2(0.5f, 0.5f);
            gridRt.pivot = new Vector2(0.5f, 0.5f);
            gridRt.anchoredPosition = gridBaseAnchoredPos * _currentUiScale;

            int rows = Mathf.CeilToInt(selectableSkins.Count / (float)Mathf.Max(1, columns));
            float totalW = columns * cellSize.x + (columns - 1) * spacing.x;
            float totalH = rows * cellSize.y + (rows - 1) * spacing.y;

            gridRt.sizeDelta = new Vector2(totalW, totalH);
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

            SLog("EnsureUnlockHintText | created runtime TMP");
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

        SLog(
            $"EnsureUnlockHintText | " +
            $"anchoredPos={unlockHintRect.anchoredPosition} " +
            $"sizeDelta={unlockHintRect.sizeDelta} " +
            $"fontSize={unlockHintText.fontSize:0.###} " +
            $"font={(unlockHintText.font != null ? unlockHintText.font.name : "NULL")} " +
            $"bottomOffsetResolved={bottomOffset:0.###}"
        );

        DumpHintSpacing("EnsureUnlockHintText");
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

            SLog(
                $"ShowUnlockHint | " +
                $"message='{message}' " +
                $"width={width:0.###} baseHeight={baseHeight:0.###} " +
                $"preferredHeight={preferredHeight:0.###} minHeight={minHeight:0.###} finalHeight={finalHeight:0.###} " +
                $"anchoredPos={unlockHintRect.anchoredPosition}"
            );
        }

        _lastUnlockHintMessage = message;
        DumpHintSpacing("ShowUnlockHint");
    }

    void HideUnlockHintImmediate()
    {
        if (unlockHintText == null)
            return;

        unlockHintText.text = string.Empty;
        unlockHintText.gameObject.SetActive(false);
        _lastUnlockHintMessage = string.Empty;
    }

    void DumpHintSpacing(string context)
    {
        if (!enableSurgicalLogs)
            return;

        if (unlockHintRect == null)
        {
            SLog($"{context} | unlockHintRect=NULL");
            return;
        }

        if (referenceRect == null)
        {
            SLog($"{context} | referenceRect=NULL");
            return;
        }

        Canvas canvas = GetRootCanvas();
        Camera cam = null;

        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            cam = canvas.worldCamera != null ? canvas.worldCamera : GetMainCameraSafe();

        referenceRect.GetWorldCorners(_refWorldCorners);
        unlockHintRect.GetWorldCorners(_hintWorldCorners);

        Vector2 refBL = RectTransformUtility.WorldToScreenPoint(cam, _refWorldCorners[0]);
        Vector2 refTL = RectTransformUtility.WorldToScreenPoint(cam, _refWorldCorners[1]);
        Vector2 refTR = RectTransformUtility.WorldToScreenPoint(cam, _refWorldCorners[2]);

        Vector2 hintBL = RectTransformUtility.WorldToScreenPoint(cam, _hintWorldCorners[0]);
        Vector2 hintTL = RectTransformUtility.WorldToScreenPoint(cam, _hintWorldCorners[1]);
        Vector2 hintTR = RectTransformUtility.WorldToScreenPoint(cam, _hintWorldCorners[2]);

        float bottomGapPx = hintBL.y - refBL.y;
        float leftInsetPx = hintBL.x - refBL.x;
        float rightInsetPx = refTR.x - hintTR.x;

        float normalizedBottomGap = referenceRect.rect.height > 0.001f
            ? bottomGapPx / referenceRect.rect.height
            : 0f;

        float resolvedBottomOffset = 0f;

        if (referenceRect != null)
            resolvedBottomOffset = referenceRect.rect.height * unlockHintBottomPercentOfReferenceHeight;
        else
            resolvedBottomOffset = unlockHintBottomMargin * _currentUiScale;

        SLog(
            $"{context} | " +
            $"screen=({Screen.width}x{Screen.height}) " +
            $"uiScale={_currentUiScale:0.###} baseScaleInt={_currentBaseScaleInt} " +
            $"hintBottomMargin={unlockHintBottomMargin:0.###} " +
            $"hintBottomPercent={unlockHintBottomPercentOfReferenceHeight:0.######} " +
            $"resolvedBottomOffset={resolvedBottomOffset:0.###} " +
            $"hintAnchoredPos={unlockHintRect.anchoredPosition} hintSize={unlockHintRect.sizeDelta} " +
            $"refRectLocalSize={referenceRect.rect.size} refScreenBL={refBL} refScreenTL={refTL} " +
            $"hintScreenBL={hintBL} hintScreenTL={hintTL} " +
            $"bottomGapPx={bottomGapPx:0.###} normalizedBottomGap={normalizedBottomGap:0.######} " +
            $"leftInsetPx={leftInsetPx:0.###} rightInsetPx={rightInsetPx:0.###}"
        );
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

            if (ps.index < 0 || ps.index >= selectableSkins.Count)
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

        if (index < 0 || index >= selectableSkins.Count)
        {
            HideUnlockHintImmediate();
            return;
        }

        BomberSkin skin = selectableSkins[index];

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

    void SLog(string message)
    {
        if (!enableSurgicalLogs)
            return;

        Debug.Log($"{LOG} {message}", this);
    }
}

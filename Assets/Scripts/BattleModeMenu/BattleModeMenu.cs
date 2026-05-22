using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class BattleModeMenu : MonoBehaviour
{
    private enum MenuState
    {
        MatchMode = 0,
        PlayerSelect = 1,
        SkinSelect = 2,
        TeamSelect = 3,
        RuleConfig = 4,
        StageSelect = 5,
        SpecificSettings = 6,
        MusicSelect = 7
    }

    [System.Serializable]
    private sealed class BackgroundSet
    {
        public Sprite[] sprites = new Sprite[2];
    }

    [System.Serializable]
    private sealed class OptionPanelLayout
    {
        public int fontSize = 32;
        public float optionItemHeight = 54f;
        public Vector2 optionSpacing = new(0f, 18f);
        public float optionContentOffsetX = 90f;
        public float optionContentOffsetY = -6f;
        public float cursorExtraOffsetY = 0f;
        [Range(0f, 1f)] public float blockCenterX = 0.454f;
        [Range(0f, 1f)] public float blockCenterY = 1f;
        public float cursorReservedWidth = 56f;
        public float extraBlockWidth = 8f;
        public float extraBlockHeight = 0f;
        public Vector2 cursorOffset = new(-52f, 0f);
        public float cursorHeightMultiplier = 1.15f;
        public float minCursorSize = 30f;
        public bool useRichTextEntryColors;
    }

    private sealed class TeamRowVisual
    {
        public BattleModeRules.TeamId teamId;
        public RectTransform root;
        public Image background;
        public TextMeshProUGUI label;
        public readonly List<Image> members = new();
    }

    private sealed class RuleRowVisual
    {
        public RectTransform root;
        public TextMeshProUGUI label;
        public TextMeshProUGUI value;
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
    [SerializeField] private string titleSceneName = "TitleScreen";
    [SerializeField] private string nextSceneName = "";
    [SerializeField] private bool loadNextSceneAfterSelection;
    [SerializeField, Min(0f)] private float confirmFeedbackSeconds = 0.5f;

    [Header("Input Timing")]
    [SerializeField, Min(0.01f)] private float directionalRepeatInitialDelay = 0.22f;
    [SerializeField, Min(0.01f)] private float directionalRepeatInterval = 0.12f;

    [Header("Prompt Title (optional)")]
    [SerializeField] private TextMeshProUGUI promptTitleText;
    [SerializeField] private string matchModePrompt = "BATTLE MODE";
    [SerializeField] private string playerSelectPrompt = "PLAYER SELECT";
    [SerializeField] private string skinSelectPrompt = "CHARACTER SELECT";
    [SerializeField] private string teamSelectPrompt = "TEAM MEMBERS";
    [SerializeField] private string ruleSelectPrompt = "RULE CONFIG";
    [SerializeField] private string stageSelectPrompt = "STAGE SELECT";
    [SerializeField] private string specificSettingsPrompt = "SPECIFIC SETTINGS";
    [SerializeField] private string musicSelectPrompt = "SELECT MUSIC";

    [Header("Options Panel")]
    [SerializeField] private SaveFileMenuOptions leftPanel;
    [SerializeField] private BomberSkinSelectMenu skinSelectMenu;
    [SerializeField] private OptionPanelLayout matchModeLayout = new();
    [SerializeField] private OptionPanelLayout playerSelectLayout = new()
    {
        fontSize = 32,
        optionItemHeight = 34f,
        optionSpacing = new Vector2(0f, 10f),
        optionContentOffsetX = 90f,
        optionContentOffsetY = -6f,
        cursorExtraOffsetY = 0f,
        blockCenterX = 0.454f,
        blockCenterY = 1f,
        cursorReservedWidth = 56f,
        extraBlockWidth = 8f,
        extraBlockHeight = 0f,
        cursorOffset = new Vector2(-52f, 0f),
        cursorHeightMultiplier = 1.83f,
        minCursorSize = 62f,
        useRichTextEntryColors = true
    };

    [Header("Animated Backgrounds")]
    [SerializeField] private BackgroundSet matchModeBackgrounds = new();
    [SerializeField] private BackgroundSet playerSelectBackgrounds = new();
    [SerializeField] private BackgroundSet skinSelectBackgrounds = new();
    [SerializeField] private BackgroundSet teamSelectBackgrounds = new();
    [SerializeField] private BackgroundSet ruleConfigBackgrounds = new();
    [SerializeField] private BackgroundSet stageSelectBackgrounds = new();
    [SerializeField] private BackgroundSet specificSettingsBackgrounds = new();
    [SerializeField] private BackgroundSet musicSelectBackgrounds = new();
    [SerializeField, Min(0.01f)] private float backgroundSwapInterval = 2f;
    [SerializeField] private bool backgroundSwapLoop = true;

    [Header("Player Select Colors")]
    [SerializeField] private string playerLabelHex = "#00FF3C";
    [SerializeField] private string manHex = "#00FF3C";
    [SerializeField] private string comHex = "#FF3030";
    [SerializeField] private string offHex = "#2F90FF";
    [SerializeField, Min(1)] private int playerModeColumnSpaces = 6;

    [Header("Team Select")]
    [SerializeField] private Vector2 teamRowsOffset = new(0f, -8f);
    [SerializeField] private Vector2 teamRowSize = new(620f, 94f);
    [SerializeField, Min(0f)] private float teamRowSpacing = 10f;
    [SerializeField] private Vector2 teamMemberSize = new(96f, 96f);
    [SerializeField, Min(1f)] private float teamMemberSpacing = 72f;
    [SerializeField] private float teamMembersCenterOffsetX = 40f;
    [SerializeField] private float teamWalkingMemberOffsetY = 0f;
    [SerializeField] private float teamCelebrationMemberOffsetY = 0f;
    [SerializeField] private int teamLabelFontSize = 16;
    [SerializeField] private Vector2 teamLabelOffsetMin = new(12f, 0f);
    [SerializeField] private Vector2 teamLabelSize = new(120f, 94f);
    [SerializeField] private Vector2 teamCursorOffset = new(-360f, 0f);
    [SerializeField] private Vector2 teamCursorSize = new(62f, 62f);
    [SerializeField, Range(0f, 1f)] private float teamCurrentMinAlpha = 0.25f;
    [SerializeField, Range(0f, 1f)] private float teamCurrentMaxAlpha = 1f;
    [SerializeField, Min(0.01f)] private float teamCurrentBlinkSpeed = 5.5f;
    [SerializeField] private Color teamRedColor = new(0.58f, 0.03f, 0.16f, 0.92f);
    [SerializeField] private Color teamBlueColor = new(0.05f, 0.24f, 0.82f, 0.92f);
    [SerializeField] private Color teamGreenColor = new(0.05f, 0.36f, 0.12f, 0.92f);

    [Header("Rule Config")]
    [SerializeField] private bool syncRuleConfigLayoutWithPlayerSelect = true;
    [SerializeField] private bool syncRuleConfigFontWithPlayerSelect;
    [SerializeField] private Vector2 ruleRowsOffset = new(0f, -4f);
    [SerializeField] private Vector2 ruleRowSize = new(760f, 34f);
    [SerializeField, Min(0f)] private float ruleRowSpacing = 70f;
    [SerializeField] private int ruleFontSize = 42;
    [SerializeField] private Vector2 ruleLabelOffset = new(-300f, 0f);
    [SerializeField] private Vector2 ruleLabelSize = new(430f, 34f);
    [SerializeField] private Vector2 ruleValueOffset = new(230f, 0f);
    [SerializeField] private Vector2 ruleValueSize = new(300f, 34f);
    [SerializeField] private Vector2 ruleCursorOffset = new(-420f, 0f);
    [SerializeField] private Vector2 ruleCursorSize = new(62f, 62f);
    [SerializeField] private string ruleBlueHex = "#2F90FF";
    [SerializeField] private string ruleGreenHex = "#00FF3C";
    [SerializeField] private string ruleRedHex = "#FF3030";

    [Header("Stage Select")]
    [SerializeField, Min(1)] private int battleStageCount = 15;
    [SerializeField] private string battleStageScenePrefix = "BattleMode_";
    [SerializeField] private string battleStageThumbnailResourceFormat = "BattleMode/BM{0} Miniature";
    [SerializeField] private Sprite[] battleStageThumbnails = new Sprite[15];
    [SerializeField] private string[] battleStageNames = new string[15];
    [SerializeField] private Vector2 stageSelectRootOffset = Vector2.zero;
    [SerializeField] private Vector2 stageTitleOffset = new(0f, 272f);
    [SerializeField] private Vector2 stageTitleSize = new(520f, 42f);
    [SerializeField] private Vector2 stageThumbnailOffset = Vector2.zero;
    [SerializeField] private Vector2 stageThumbnailSize = new(112f, 112f);
    [SerializeField] private Vector2 stageSideThumbnailOffset = new(330f, 0f);
    [SerializeField, Min(0.01f)] private float stageSideThumbnailScale = 0.333f;
    [SerializeField, Min(0.01f)] private float stageCarouselTransitionSeconds = 0.25f;
    [SerializeField] private Vector2 stageCarouselMaskSize = Vector2.zero;
    [SerializeField, Min(0f)] private float stageCarouselMaskHorizontalInset = 13f;
    [SerializeField] private Vector2 stageNameOffset = new(0f, -272f);
    [SerializeField] private Vector2 stageNameSize = new(620f, 42f);
    [SerializeField] private int stageSelectFontSize = 32;
    [SerializeField] private Color stageThumbnailFallbackColor = new(0f, 0f, 0f, 0.65f);

    [Header("Specific Settings")]
    [SerializeField] private Vector2 specificSettingsRootOffset = Vector2.zero;
    [SerializeField] private Vector2 specificStageImageOffset = new(-280f, 0f);
    [SerializeField] private Vector2 specificStageImageSize = new(112f, 112f);
    [SerializeField] private Vector2 specificOptionsOffset = new(250f, 0f);
    [SerializeField] private Vector2 specificOptionRowSize = new(360f, 42f);
    [SerializeField] private float specificOptionRowSpacing = 22f;
    [SerializeField] private int specificSettingsFontSize = 32;
    [SerializeField] private Vector2 specificCursorOffset = new(-210f, 0f);
    [SerializeField] private Vector2 specificCursorSize = new(62f, 62f);
    [SerializeField, Min(0f)] private float specificStartWaitSeconds = 4f;
    [SerializeField, Min(0.01f)] private float specificStartFadeSeconds = 1f;
    [SerializeField] private Color specificStageImageFallbackColor = new(0f, 0f, 0f, 0.65f);
    [SerializeField] private Texture2D battleStartTexture;
    [SerializeField] private Vector2 battleStartOffset = Vector2.zero;
    [SerializeField] private Vector2 battleStartSize = new(190f, 19f);
    [SerializeField, Min(0.01f)] private float battleStartBlinkSeconds = 0.05f;
    [SerializeField] private Vector2 musicSelectRootOffset = Vector2.zero;
    [SerializeField] private Vector2 musicSelectOptionsOffset = new(120f, 0f);
    [SerializeField] private Vector2 musicSelectNameColumnOffset = new(-180f, 0f);
    [SerializeField] private Vector2 musicSelectCheckboxColumnOffset = new(260f, 0f);
    [SerializeField] private Vector2 musicSelectNameColumnSize = new(520f, 36f);
    [SerializeField] private Vector2 musicSelectCheckboxColumnSize = new(110f, 36f);
    [SerializeField] private float musicSelectOptionRowSpacing = 16f;
    [SerializeField] private int musicSelectFontSize = 28;
    [SerializeField] private Vector2 musicSelectCursorOffset = new(-300f, 0f);
    [SerializeField] private Vector2 musicSelectCursorSize = new(62f, 62f);
    [SerializeField, Range(0f, 1f)] private float musicSelectPreviewVolumeMultiplier = 0.6f;

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
    [SerializeField] private AudioClip deniedSfx;
    [SerializeField, Range(0f, 1f)] private float deniedSfxVolume = 1f;
    [SerializeField] private AudioClip specificStartSfx;
    [SerializeField, Range(0f, 1f)] private float specificStartSfxVolume = 1f;

    [Header("Dynamic Scale (Pixel Perfect SNES)")]
    [SerializeField] private bool dynamicScale = true;
    [SerializeField] private int referenceWidth = 256;
    [SerializeField] private int referenceHeight = 224;
    [SerializeField] private bool useIntegerUpscale = true;
    [SerializeField, Min(1)] private int designUpscale = 4;
    [SerializeField, Min(0.01f)] private float extraScaleMultiplier = 1f;
    [SerializeField, Min(0.01f)] private float minScale = 0.5f;
    [SerializeField, Min(0.01f)] private float maxScale = 10f;

    private static readonly BattleModeRules.MatchMode[] MatchModes =
    {
        BattleModeRules.MatchMode.SingleMatch,
        BattleModeRules.MatchMode.TagMatch
    };

    private static readonly BattleModeRules.TeamId[] TeamIds =
    {
        BattleModeRules.TeamId.Red,
        BattleModeRules.TeamId.Blue,
        BattleModeRules.TeamId.Green
    };

    private static readonly BattleModeComputerLevel[] ComputerLevels =
    {
        BattleModeComputerLevel.Easy,
        BattleModeComputerLevel.Normal,
        BattleModeComputerLevel.Hard
    };

    private static readonly BattleModeRules.RoundTimerMode[] RuleTimerModes =
    {
        BattleModeRules.RoundTimerMode.OneMinute,
        BattleModeRules.RoundTimerMode.TwoMinutes,
        BattleModeRules.RoundTimerMode.ThreeMinutes,
        BattleModeRules.RoundTimerMode.FourMinutes,
        BattleModeRules.RoundTimerMode.FiveMinutes,
        BattleModeRules.RoundTimerMode.Infinite
    };

    private static readonly BattleModeSuddenDeathSetting[] SuddenDeathSettings =
    {
        BattleModeSuddenDeathSetting.Off,
        BattleModeSuddenDeathSetting.On,
        BattleModeSuddenDeathSetting.Random
    };
    private static readonly string[] SpecificSettingsOptions =
    {
        "Handicap",
        "Items",
        "Louies",
        "Music",
        "Start"
    };

    private const int PlayerActionCount = (int)PlayerAction.ActionR + 1;

    private readonly List<string> matchModeEntries = new()
    {
        "Single Match",
        "Tag Match"
    };

    private readonly List<string> playerEntries = new();
    private readonly List<int> battleEnabledPlayerIds = new(GameSession.MaxPlayerId);
    private readonly List<int> battleHumanPlayerIds = new(GameSession.MaxPlayerId);
    private readonly List<TeamRowVisual> teamRows = new();
    private readonly List<RuleRowVisual> ruleRows = new();
    private readonly List<int> teamSelectionPlayerIds = new(GameSession.MaxPlayerId);

    private readonly List<bool> displayEnabled = new()
    {
        true,
        true,
        true,
        true,
        true,
        true
    };

    private BattleModePlayerControlMode[] playerModes;
    private MenuState state = MenuState.MatchMode;
    private int selectedIndex;
    private int selectedPlayerIndex;
    private bool confirmed;
    private bool menuActive;
    private bool cursorConfirmVisual;

    private int backgroundSpriteIndex;
    private float backgroundSwapTimer;

    private Coroutine fadeInCoroutine;

    private float currentUiScale = 1f;
    private int lastScreenW = -1;
    private int lastScreenH = -1;
    private Rect lastCameraRect;
    private Rect lastRefPixelRect;
    private RectTransform teamSelectRoot;
    private RectTransform teamCursorRt;
    private AnimatedSpriteRenderer teamCursorRenderer;
    private BattleModeRules.TeamId[] workingTeams;
    private bool[] teamAssigned;
    private float[] teamCelebrationTimers;
    private bool[] teamCelebrationCompleted;
    private int currentTeamPlayerIndex;
    private int selectedTeamIndex;
    private float teamPreviewTimer;
    private bool teamSelectionReturnedToSkinSelect;
    private RectTransform ruleConfigRoot;
    private RectTransform ruleCursorRt;
    private AnimatedSpriteRenderer ruleCursorRenderer;
    private int selectedRuleIndex;
    private BattleModeComputerLevel ruleComputerLevel;
    private int ruleBattlesToWin;
    private BattleModeRules.RoundTimerMode ruleTimerMode;
    private BattleModeSuddenDeathSetting ruleSuddenDeath;
    private bool ruleRevengeBomber;
    private bool ruleConfigReturnedToSkinSelect;
    private bool ruleConfigReturnedToTeamSelect;
    private RectTransform stageSelectRoot;
    private RectTransform stageCarouselViewport;
    private TextMeshProUGUI stageTitleText;
    private Image[] stageThumbnailImages;
    private TextMeshProUGUI stageNameText;
    private int selectedStageIndex;
    private int stageCarouselFromIndex;
    private int stageCarouselDirection;
    private float stageCarouselElapsed;
    private bool stageCarouselAnimating;
    private bool stageSelectionReturnedToRuleConfig;
    private Sprite[] battleStageThumbnailResourceCache;
    private RectTransform specificSettingsRoot;
    private Image specificStageImage;
    private RawImage battleStartImage;
    private readonly List<TextMeshProUGUI> specificOptionTexts = new();
    private RectTransform specificCursorRt;
    private AnimatedSpriteRenderer specificCursorRenderer;
    private RectTransform musicSelectRoot;
    private readonly List<TextMeshProUGUI> musicSelectOptionTexts = new();
    private readonly List<TextMeshProUGUI> musicSelectCheckboxTexts = new();
    private RectTransform musicSelectCursorRt;
    private AnimatedSpriteRenderer musicSelectCursorRenderer;
    private BattleModeRules.BattleMusicSelection[] musicSelections;
    private int selectedMusicIndex;
    private int workingBattleMusicSelectionMask;
    private bool musicSelectPreviewPlaying;
    private float musicSelectCursorConfirmTimer;
    private int selectedSpecificSettingIndex;
    private bool specificSettingsReturnedToStageSelect;
    private bool specificStartConfirmed;
    private Vector2 specificConfirmedCursorPosition;
    private float battleStartBlinkTimer;
    private bool battleStartVisible;
    private readonly bool[] previousMenuHeld = new bool[PlayerActionCount];
    private readonly bool[] menuHoldConsumed = new bool[PlayerActionCount];
    private readonly float[] nextMenuRepeatTime = new float[PlayerActionCount];

    private BattleModeRules.MatchMode SelectedMatchMode => MatchModes[Mathf.Clamp(selectedIndex, 0, MatchModes.Length - 1)];

    private void Awake()
    {
        if (root == null)
            root = gameObject;

        selectedIndex = GetInitialSelectedIndex();
        selectedPlayerIndex = 0;
        playerModes = SaveSystem.GetBattleModePlayerControlModes();

        if (skinSelectMenu == null)
            skinSelectMenu = GetComponent<BomberSkinSelectMenu>();

        ApplyCurrentBackgroundSprite(true);
        UpdatePromptTitle();

        if (root != null)
            root.SetActive(false);
    }

    private void Start()
    {
        StartCoroutine(OpenMenuRoutine());
    }

    private void Update()
    {
        if (root != null && root.activeInHierarchy)
        {
            ApplyDynamicScaleIfNeeded(false);

            if (state != MenuState.SkinSelect)
                TickBackgroundSpriteSwap();
        }

        if (state == MenuState.TeamSelect)
            UpdateTeamSelectVisuals(false);
        else if (state == MenuState.RuleConfig)
            UpdateRuleConfigVisuals();
        else if (state == MenuState.StageSelect)
            UpdateStageSelectVisuals();
        else if (state == MenuState.SpecificSettings)
            UpdateSpecificSettingsVisuals();
        else if (state == MenuState.MusicSelect)
            UpdateMusicSelectVisuals();

        if (!menuActive || state == MenuState.SkinSelect)
            return;

        UpdateOptionVisuals();
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

        confirmed = false;
        cursorConfirmVisual = false;
        menuActive = false;
        state = MenuState.MatchMode;

        ResetBackgroundSpriteSwap();
        ApplyCurrentBackgroundSprite(true);
        UpdatePromptTitle();

        yield return null;
        Canvas.ForceUpdateCanvases();
        yield return new WaitForEndOfFrame();
        Canvas.ForceUpdateCanvases();

        ApplyDynamicScaleIfNeeded(true);
        BuildMatchModeMenu();
        StartSelectMusic();

        var input = PlayerInputManager.Instance;

        while (input != null && HasAnyRelevantHeldInput(input, out _, out _))
        {
            yield return null;
        }

        yield return null;
        CapturePreviousHeldInputs(input);
        menuActive = true;

        if (fadeInCoroutine != null)
            StopCoroutine(fadeInCoroutine);

        fadeInCoroutine = StartCoroutine(FadeInRoutine());

        while (!confirmed)
        {
            if (input == null)
            {
                input = PlayerInputManager.Instance;
                yield return null;
                continue;
            }

            if (state == MenuState.MatchMode)
                yield return TickMatchModeInput(input);
            else if (state == MenuState.PlayerSelect)
                yield return TickPlayerSelectInput(input);
            else
                yield return null;

            CapturePreviousHeldInputs(input);
            yield return null;
        }
    }

    private IEnumerator TickMatchModeInput(PlayerInputManager input)
    {
        bool moved = false;
        if (MenuDirectionalPressed(input, PlayerAction.MoveUp, out _))
        {
            selectedIndex = WrapIndex(selectedIndex - 1, MatchModes.Length);
            moved = true;
        }
        else if (MenuDirectionalPressed(input, PlayerAction.MoveDown, out _))
        {
            selectedIndex = WrapIndex(selectedIndex + 1, MatchModes.Length);
            moved = true;
        }

        if (moved)
        {
            PlaySfx(moveCursorSfx, moveCursorSfxVolume);
            UpdateOptionVisuals();
        }

        if (MenuButtonPressed(input, PlayerAction.ActionB, out _))
        {
            PlaySfx(returnSfx, returnSfxVolume);
            confirmed = true;
            yield return FadeOutRoutine();
            Hide();
            LoadTitleScene();
            yield break;
        }

        bool actionA = MenuButtonPressed(input, PlayerAction.ActionA, out _);
        bool start = MenuButtonPressed(input, PlayerAction.Start, out _);
        if (actionA || start)
        {
            yield return ConfirmMatchModeSelection();
        }

    }

    private IEnumerator TickPlayerSelectInput(PlayerInputManager input)
    {
        bool moved = false;
        if (MenuDirectionalPressed(input, PlayerAction.MoveUp, out _))
        {
            selectedPlayerIndex = WrapIndex(selectedPlayerIndex - 1, playerModes.Length);
            moved = true;
        }
        else if (MenuDirectionalPressed(input, PlayerAction.MoveDown, out _))
        {
            selectedPlayerIndex = WrapIndex(selectedPlayerIndex + 1, playerModes.Length);
            moved = true;
        }

        if (moved)
        {
            PlaySfx(moveCursorSfx, moveCursorSfxVolume);
            UpdateOptionVisuals();
        }

        bool moveLeft = MenuDirectionalPressed(input, PlayerAction.MoveLeft, out _);
        bool actionL = MenuButtonPressed(input, PlayerAction.ActionL, out _);
        bool moveRight = MenuDirectionalPressed(input, PlayerAction.MoveRight, out _);
        bool actionR = MenuButtonPressed(input, PlayerAction.ActionR, out _);
        bool previousMode = moveLeft || actionL;
        bool nextMode = moveRight || actionR;

        if (previousMode || nextMode)
        {
            CycleSelectedPlayerMode(nextMode ? 1 : -1);
            PlaySfx(moveCursorSfx, moveCursorSfxVolume);
            RefreshPlayerSelectEntries();
        }

        if (MenuButtonPressed(input, PlayerAction.ActionB, out _))
        {
            PlaySfx(returnSfx, returnSfxVolume);
            cursorConfirmVisual = false;
            yield return BuildMatchModeMenuAfterTransition("PlayerSelect.ActionB");
            yield break;
        }

        bool actionA = MenuButtonPressed(input, PlayerAction.ActionA, out _);
        bool start = MenuButtonPressed(input, PlayerAction.Start, out _);
        if (actionA || start)
        {
            yield return ConfirmPlayerSelection();
        }

    }

    private IEnumerator ConfirmMatchModeSelection()
    {
        PlaySfx(confirmSfx, confirmSfxVolume);
        SaveSystem.SetBattleModeMatchMode(SelectedMatchMode);

        cursorConfirmVisual = true;
        UpdateOptionVisuals();
        float wait = Mathf.Max(0f, confirmFeedbackSeconds);
        if (wait > 0f)
            yield return new WaitForSecondsRealtime(wait);

        cursorConfirmVisual = false;
        yield return BuildPlayerSelectMenuAfterTransition("ConfirmMatchMode");
    }

    private IEnumerator ConfirmPlayerSelection()
    {
        PlaySfx(confirmSfx, confirmSfxVolume);
        SaveSystem.SetBattleModePlayerControlModes(playerModes);

        cursorConfirmVisual = true;
        UpdateOptionVisuals();
        float wait = Mathf.Max(0f, confirmFeedbackSeconds);
        if (wait > 0f)
            yield return new WaitForSecondsRealtime(wait);

        cursorConfirmVisual = false;

        if (skinSelectMenu != null)
        {
            yield return OpenSkinSelectMenu();
            yield break;
        }

        if (loadNextSceneAfterSelection && !string.IsNullOrWhiteSpace(nextSceneName))
        {
            confirmed = true;
            yield return FadeOutRoutine();
            Hide();
            LoadScene(nextSceneName);
            yield break;
        }

        UpdateOptionVisuals();
    }

    private void BuildMatchModeMenu()
    {
        state = MenuState.MatchMode;
        ApplyPanelLayout(matchModeLayout);

        if (leftPanel != null)
        {
            leftPanel.HideCursor();
            leftPanel.SetEntries(matchModeEntries, displayEnabled);
        }

        UpdatePromptTitle();
        ApplyCurrentBackgroundSprite(true);
        UpdateOptionVisuals();
    }

    private void BuildPlayerSelectMenu()
    {
        state = MenuState.PlayerSelect;
        playerModes ??= SaveSystem.GetBattleModePlayerControlModes();
        BuildPlayerEntries();
        ApplyPanelLayout(playerSelectLayout);

        if (leftPanel != null)
        {
            leftPanel.HideCursor();
            leftPanel.SetEntries(playerEntries, displayEnabled);
        }

        UpdatePromptTitle();
        ApplyCurrentBackgroundSprite(true);
        UpdateOptionVisuals();
    }

    private IEnumerator BuildMatchModeMenuAfterTransition(string context)
    {
        yield return BuildOptionMenuAfterTransition(context, buildPlayerSelect: false);
    }

    private IEnumerator BuildPlayerSelectMenuAfterTransition(string context)
    {
        yield return BuildOptionMenuAfterTransition(context, buildPlayerSelect: true);
    }

    private IEnumerator BuildOptionMenuAfterTransition(string context, bool buildPlayerSelect)
    {
        if (leftPanel != null)
            leftPanel.SetCursorVisibilitySuppressed(true);

        if (buildPlayerSelect)
            BuildPlayerSelectMenu();
        else
            BuildMatchModeMenu();

        Canvas.ForceUpdateCanvases();
        yield return null;
        Canvas.ForceUpdateCanvases();
        ApplyDynamicScaleIfNeeded(true);

        if (leftPanel != null)
        {
            UpdateOptionVisuals();
            leftPanel.SetCursorVisibilitySuppressed(false);
            UpdateOptionVisuals();
        }

    }

    private IEnumerator OpenSkinSelectMenu()
    {
        while (true)
        {
            ruleConfigReturnedToSkinSelect = false;
            ruleConfigReturnedToTeamSelect = false;
            state = MenuState.SkinSelect;
            menuActive = false;
            cursorConfirmVisual = false;
            ApplyBattleModeActivePlayerIds(includeComPlayers: false);

            if (teamSelectRoot != null)
                teamSelectRoot.gameObject.SetActive(false);
            if (ruleConfigRoot != null)
                ruleConfigRoot.gameObject.SetActive(false);
            if (stageSelectRoot != null)
                stageSelectRoot.gameObject.SetActive(false);

            if (leftPanel != null)
            {
                leftPanel.HideCursor();
                leftPanel.gameObject.SetActive(false);
            }

            UpdatePromptTitle();
            ApplyCurrentBackgroundSprite(true);

            yield return skinSelectMenu.SelectSkinRoutine();

            ApplyBattleModeActivePlayerIds(includeComPlayers: true);
            RestoreRootAfterEmbeddedSkinSelect();

            if (skinSelectMenu.ReturnToTitleRequested)
                break;

            bool reopenSkinSelect = false;
            bool rulesConfirmed = false;
            while (!confirmed)
            {
                while (!rulesConfirmed)
                {
                    if (SelectedMatchMode == BattleModeRules.MatchMode.TagMatch)
                    {
                        yield return OpenTeamSelectMenu(ruleConfigReturnedToTeamSelect);

                        if (teamSelectionReturnedToSkinSelect)
                        {
                            reopenSkinSelect = true;
                            break;
                        }
                    }

                    yield return OpenRuleConfigMenu();

                    if (ruleConfigReturnedToSkinSelect)
                    {
                        reopenSkinSelect = true;
                        break;
                    }

                    if (ruleConfigReturnedToTeamSelect)
                        continue;

                    rulesConfirmed = true;
                }

                if (reopenSkinSelect)
                    break;

                yield return OpenStageSelectMenu();

                if (stageSelectionReturnedToRuleConfig)
                {
                    rulesConfirmed = false;
                    continue;
                }

                yield return OpenSpecificSettingsMenu();

                if (specificSettingsReturnedToStageSelect)
                    continue;

                break;
            }

            if (reopenSkinSelect)
                continue;

            if (confirmed)
                yield break;

            break;
        }

        ShowPlayerSelectAfterSkinOrTeamReturn();

        PlayerInputManager input = PlayerInputManager.Instance;
        while (input != null && HasAnyRelevantHeldInput(input, out _, out _))
            yield return null;

        yield return null;
        CapturePreviousHeldInputs(input);
        menuActive = true;
    }

    private void RestoreRootAfterEmbeddedSkinSelect()
    {
        if (root != null)
            root.SetActive(true);

        if (fadeImage != null)
        {
            fadeImage.gameObject.SetActive(false);
            SetFadeAlpha(0f);
        }
    }

    private IEnumerator OpenTeamSelectMenu(bool resumeAtLastMember = false)
    {
        teamSelectionReturnedToSkinSelect = false;
        state = MenuState.TeamSelect;
        menuActive = false;
        cursorConfirmVisual = false;

        if (leftPanel != null)
        {
            leftPanel.HideCursor();
            leftPanel.gameObject.SetActive(false);
        }

        EnsureTeamSelectBuilt();
        ResetBackgroundSpriteSwap();
        ApplyCurrentBackgroundSprite(true);
        UpdatePromptTitle();
        BuildTeamSelectionPlayerIds();
        InitializeTeamSelectionState();
        if (resumeAtLastMember)
            ResumeTeamSelectionAtLastMember();
        UpdateTeamSelectVisuals(true);
        Canvas.ForceUpdateCanvases();
        ApplyDynamicScaleIfNeeded(true);

        PlayerInputManager input = PlayerInputManager.Instance;
        while (input != null && HasAnyRelevantHeldInput(input, out _, out _))
            yield return null;

        bool done = teamSelectionPlayerIds.Count <= 0;
        while (!done)
        {
            int inputPlayerId = GameSession.MinPlayerId;
            bool moved = false;

            if (input != null)
            {
                if (input.GetDown(inputPlayerId, PlayerAction.MoveUp) ||
                    input.GetDown(inputPlayerId, PlayerAction.MoveLeft))
                {
                    selectedTeamIndex = WrapIndex(selectedTeamIndex - 1, TeamIds.Length);
                    moved = true;
                }
                else if (input.GetDown(inputPlayerId, PlayerAction.MoveDown) ||
                         input.GetDown(inputPlayerId, PlayerAction.MoveRight))
                {
                    selectedTeamIndex = WrapIndex(selectedTeamIndex + 1, TeamIds.Length);
                    moved = true;
                }
            }

            if (moved)
            {
                PlaySfx(moveCursorSfx, moveCursorSfxVolume);
                UpdateTeamSelectVisuals(true);
            }
            else if (input != null && input.GetDown(inputPlayerId, PlayerAction.ActionB))
            {
                if (currentTeamPlayerIndex <= 0)
                {
                    teamSelectionReturnedToSkinSelect = true;
                    PlaySfx(returnSfx, returnSfxVolume);
                    done = true;
                }
                else
                {
                    currentTeamPlayerIndex--;
                    int playerId = teamSelectionPlayerIds[currentTeamPlayerIndex];
                    teamAssigned[playerId] = false;
                    if (teamCelebrationTimers != null && playerId >= 0 && playerId < teamCelebrationTimers.Length)
                        teamCelebrationTimers[playerId] = -1f;
                    if (teamCelebrationCompleted != null && playerId >= 0 && playerId < teamCelebrationCompleted.Length)
                        teamCelebrationCompleted[playerId] = false;
                    selectedTeamIndex = IndexOfTeam(workingTeams[playerId - 1]);
                    PlaySfx(returnSfx, returnSfxVolume);
                    UpdateTeamSelectVisuals(true);
                }
            }
            else if (input != null &&
                     (input.GetDown(inputPlayerId, PlayerAction.ActionA) ||
                      input.GetDown(inputPlayerId, PlayerAction.Start)))
            {
                int playerId = teamSelectionPlayerIds[currentTeamPlayerIndex];
                BattleModeRules.TeamId selectedTeam = TeamIds[Mathf.Clamp(selectedTeamIndex, 0, TeamIds.Length - 1)];
                if (IsLastTeamSelectionPlayer() && WouldAllPlayersBeOnSameTeam(playerId, selectedTeam))
                {
                    PlaySfx(deniedSfx, deniedSfxVolume);
                    UpdateTeamSelectVisuals(true);
                    yield return null;
                    continue;
                }

                workingTeams[playerId - 1] = selectedTeam;
                teamAssigned[playerId] = true;
                if (teamCelebrationTimers != null && playerId >= 0 && playerId < teamCelebrationTimers.Length)
                    teamCelebrationTimers[playerId] = 0f;
                if (teamCelebrationCompleted != null && playerId >= 0 && playerId < teamCelebrationCompleted.Length)
                    teamCelebrationCompleted[playerId] = false;
                PlaySfx(confirmSfx, confirmSfxVolume);

                currentTeamPlayerIndex++;
                if (currentTeamPlayerIndex >= teamSelectionPlayerIds.Count)
                {
                    SaveSystem.SetBattleModePlayerTeams(workingTeams);
                    done = true;
                }
                else
                {
                    int nextPlayerId = teamSelectionPlayerIds[currentTeamPlayerIndex];
                    selectedTeamIndex = IndexOfTeam(workingTeams[nextPlayerId - 1]);
                    UpdateTeamSelectVisuals(true);
                }
            }

            teamPreviewTimer += Time.unscaledDeltaTime;
            TickTeamCelebrationTimers();
            UpdateTeamSelectVisuals(false);
            yield return null;
        }

        if (teamSelectRoot != null)
            teamSelectRoot.gameObject.SetActive(false);
        if (ruleConfigRoot != null)
            ruleConfigRoot.gameObject.SetActive(false);
        if (stageSelectRoot != null)
            stageSelectRoot.gameObject.SetActive(false);
        if (musicSelectRoot != null)
            musicSelectRoot.gameObject.SetActive(false);
        if (specificSettingsRoot != null)
            specificSettingsRoot.gameObject.SetActive(false);

        state = MenuState.SkinSelect;
    }

    private void ShowPlayerSelectAfterSkinOrTeamReturn()
    {
        if (root != null)
            root.SetActive(true);

        if (fadeImage != null)
        {
            fadeImage.gameObject.SetActive(false);
            SetFadeAlpha(0f);
        }

        if (teamSelectRoot != null)
            teamSelectRoot.gameObject.SetActive(false);
        if (ruleConfigRoot != null)
            ruleConfigRoot.gameObject.SetActive(false);
        if (stageSelectRoot != null)
            stageSelectRoot.gameObject.SetActive(false);
        if (specificSettingsRoot != null)
            specificSettingsRoot.gameObject.SetActive(false);
        if (musicSelectRoot != null)
            musicSelectRoot.gameObject.SetActive(false);

        if (leftPanel != null)
        {
            leftPanel.SetCursorVisibilitySuppressed(false);
            leftPanel.gameObject.SetActive(true);
        }

        BuildPlayerSelectMenu();

        if (leftPanel != null)
        {
            leftPanel.SetCursorVisibilitySuppressed(true);
            UpdateOptionVisuals();
        }

        Canvas.ForceUpdateCanvases();
        ApplyDynamicScaleIfNeeded(true);

        if (leftPanel != null)
        {
            UpdateOptionVisuals();
            leftPanel.SetCursorVisibilitySuppressed(false);
            UpdateOptionVisuals();
        }
    }

    private void EnsureTeamSelectBuilt()
    {
        if (teamSelectRoot != null)
        {
            teamSelectRoot.gameObject.SetActive(true);
            teamSelectRoot.SetAsLastSibling();
            return;
        }

        Transform parent = referenceRect != null ? referenceRect : (root != null ? root.transform : transform);
        GameObject rootGo = new("TeamSelectRoot", typeof(RectTransform));
        rootGo.transform.SetParent(parent, false);
        teamSelectRoot = rootGo.GetComponent<RectTransform>();
        teamSelectRoot.anchorMin = new Vector2(0.5f, 0.5f);
        teamSelectRoot.anchorMax = new Vector2(0.5f, 0.5f);
        teamSelectRoot.pivot = new Vector2(0.5f, 0.5f);
        teamSelectRoot.anchoredPosition = Vector2.zero;
        teamSelectRoot.sizeDelta = Vector2.zero;
        teamSelectRoot.localScale = Vector3.one * currentUiScale;
        teamSelectRoot.SetAsLastSibling();

        teamRows.Clear();

        for (int i = 0; i < TeamIds.Length; i++)
        {
            BattleModeRules.TeamId teamId = TeamIds[i];
            TeamRowVisual row = CreateTeamRow(teamId, i);
            teamRows.Add(row);
        }

        CreateTeamCursor();
    }

    private void CreateTeamCursor()
    {
        AnimatedSpriteRenderer source = leftPanel != null ? leftPanel.CursorRenderer : null;
        GameObject cursorGo;

        if (source != null)
        {
            cursorGo = Instantiate(source.gameObject, teamSelectRoot, false);
            cursorGo.name = "TeamCursor";
            teamCursorRenderer = cursorGo.GetComponent<AnimatedSpriteRenderer>();
        }
        else
        {
            cursorGo = new GameObject("TeamCursor", typeof(RectTransform));
        }

        teamCursorRt = cursorGo.transform as RectTransform;
        if (teamCursorRt == null)
            teamCursorRt = cursorGo.AddComponent<RectTransform>();

        teamCursorRt.anchorMin = new Vector2(0.5f, 0.5f);
        teamCursorRt.anchorMax = new Vector2(0.5f, 0.5f);
        teamCursorRt.pivot = new Vector2(0.5f, 0.5f);
        teamCursorRt.sizeDelta = teamCursorSize;
        teamCursorRt.localScale = Vector3.one;

        if (teamCursorRenderer != null)
        {
            teamCursorRenderer.idle = true;
            teamCursorRenderer.loop = true;
            teamCursorRenderer.CurrentFrame = 0;
            teamCursorRenderer.RefreshFrame();
        }
    }

    private TeamRowVisual CreateTeamRow(BattleModeRules.TeamId teamId, int rowIndex)
    {
        GameObject rowGo = new($"TeamRow_{teamId}", typeof(RectTransform), typeof(Image), typeof(Outline));
        rowGo.transform.SetParent(teamSelectRoot, false);

        RectTransform rowRt = rowGo.GetComponent<RectTransform>();
        rowRt.anchorMin = new Vector2(0.5f, 0.5f);
        rowRt.anchorMax = new Vector2(0.5f, 0.5f);
        rowRt.pivot = new Vector2(0.5f, 0.5f);
        rowRt.sizeDelta = teamRowSize;
        rowRt.anchoredPosition = GetTeamRowPosition(rowIndex);

        Image bg = rowGo.GetComponent<Image>();
        bg.color = GetTeamColor(teamId);
        bg.raycastTarget = false;

        Outline outline = rowGo.GetComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(2f, -2f);

        GameObject labelGo = new("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelGo.transform.SetParent(rowRt, false);
        TextMeshProUGUI label = labelGo.GetComponent<TextMeshProUGUI>();
        label.text = GetTeamDisplayName(teamId);
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.fontSize = teamLabelFontSize;
        label.color = Color.white;
        label.raycastTarget = false;
        if (promptTitleText != null && promptTitleText.font != null)
            label.font = promptTitleText.font;
        ApplyTeamLabelFont(label);

        RectTransform labelRt = label.rectTransform;
        labelRt.anchorMin = new Vector2(0f, 0.5f);
        labelRt.anchorMax = new Vector2(0f, 0.5f);
        labelRt.pivot = new Vector2(0f, 0.5f);
        labelRt.anchoredPosition = teamLabelOffsetMin;
        labelRt.sizeDelta = teamLabelSize;

        return new TeamRowVisual
        {
            teamId = teamId,
            root = rowRt,
            background = bg,
            label = label
        };
    }

    private Vector2 GetTeamRowPosition(int rowIndex)
    {
        float totalHeight = (TeamIds.Length - 1) * (teamRowSize.y + teamRowSpacing);
        float y = (totalHeight * 0.5f) - (rowIndex * (teamRowSize.y + teamRowSpacing));
        return teamRowsOffset + new Vector2(0f, y);
    }

    private void BuildTeamSelectionPlayerIds()
    {
        teamSelectionPlayerIds.Clear();

        if (playerModes == null)
            playerModes = SaveSystem.GetBattleModePlayerControlModes();

        for (int i = 0; i < playerModes.Length && i < GameSession.MaxPlayerId; i++)
        {
            if (playerModes[i] != BattleModePlayerControlMode.Off)
                teamSelectionPlayerIds.Add(i + 1);
        }
    }

    private void InitializeTeamSelectionState()
    {
        workingTeams = SaveSystem.GetBattleModePlayerTeams();
        if (workingTeams == null || workingTeams.Length != GameSession.MaxPlayerId)
            workingTeams = new BattleModeRules.TeamId[GameSession.MaxPlayerId];

        for (int i = 0; i < workingTeams.Length; i++)
        {
            if ((int)workingTeams[i] < (int)BattleModeRules.TeamId.Blue ||
                (int)workingTeams[i] > (int)BattleModeRules.TeamId.Green)
            {
                workingTeams[i] = BattleModeRules.GetDefaultTeamForPlayer(i + 1);
            }
        }

        teamAssigned = new bool[GameSession.MaxPlayerId + 1];
        teamCelebrationTimers = new float[GameSession.MaxPlayerId + 1];
        teamCelebrationCompleted = new bool[GameSession.MaxPlayerId + 1];
        for (int i = 0; i < teamCelebrationTimers.Length; i++)
            teamCelebrationTimers[i] = -1f;

        currentTeamPlayerIndex = 0;
        teamPreviewTimer = 0f;

        int firstPlayerId = teamSelectionPlayerIds.Count > 0 ? teamSelectionPlayerIds[0] : GameSession.MinPlayerId;
        selectedTeamIndex = IndexOfTeam(workingTeams[firstPlayerId - 1]);
    }

    private void ResumeTeamSelectionAtLastMember()
    {
        if (teamSelectionPlayerIds.Count <= 0 || teamAssigned == null)
            return;

        currentTeamPlayerIndex = teamSelectionPlayerIds.Count - 1;

        for (int i = 0; i < teamSelectionPlayerIds.Count; i++)
        {
            int playerId = teamSelectionPlayerIds[i];
            if (playerId < 0 || playerId >= teamAssigned.Length)
                continue;

            teamAssigned[playerId] = i < currentTeamPlayerIndex;
            if (teamCelebrationCompleted != null && playerId < teamCelebrationCompleted.Length)
                teamCelebrationCompleted[playerId] = i < currentTeamPlayerIndex;
            if (teamCelebrationTimers != null && playerId < teamCelebrationTimers.Length)
                teamCelebrationTimers[playerId] = -1f;
        }

        int lastPlayerId = teamSelectionPlayerIds[currentTeamPlayerIndex];
        selectedTeamIndex = IndexOfTeam(workingTeams[Mathf.Clamp(lastPlayerId - 1, 0, workingTeams.Length - 1)]);
    }

    private void UpdateTeamSelectVisuals(bool force)
    {
        if (teamSelectRoot == null || !teamSelectRoot.gameObject.activeInHierarchy)
            return;

        for (int i = 0; i < teamRows.Count; i++)
        {
            TeamRowVisual row = teamRows[i];
            if (row == null)
                continue;

            row.root.anchoredPosition = GetTeamRowPosition(i);
            row.root.sizeDelta = teamRowSize;
            row.background.color = GetTeamColor(row.teamId);
            if (row.label != null)
            {
                RectTransform labelRt = row.label.rectTransform;
                labelRt.anchoredPosition = teamLabelOffsetMin;
                labelRt.sizeDelta = teamLabelSize;
                row.label.fontSize = teamLabelFontSize;
                row.label.alignment = TextAlignmentOptions.MidlineLeft;
                ApplyTeamLabelFont(row.label);
            }

            List<int> visiblePlayerIds = GetVisibleTeamMemberPlayerIds(row.teamId);
            EnsureTeamMemberImageCount(row, visiblePlayerIds.Count);

            int imageIndex = 0;
            for (int p = 0; p < visiblePlayerIds.Count; p++)
            {
                int playerId = visiblePlayerIds[p];
                bool isCurrent = IsCurrentTeamSelectionPlayer(playerId);
                bool celebrating = GetTeamCelebrationTime(playerId) >= 0f;

                Image member = row.members[imageIndex++];
                member.gameObject.SetActive(true);
                member.sprite = GetTeamMemberSprite(playerId, isCurrent);
                member.color = GetTeamMemberColor(playerId, isCurrent);
                member.rectTransform.sizeDelta = teamMemberSize;
                member.rectTransform.anchoredPosition = GetTeamMemberPosition(
                    imageIndex - 1,
                    visiblePlayerIds.Count,
                    celebrating || HasCompletedTeamCelebration(playerId));
                member.enabled = member.sprite != null;
            }

            for (int m = imageIndex; m < row.members.Count; m++)
                row.members[m].gameObject.SetActive(false);
        }

        if (teamCursorRt != null)
        {
            int rowIndex = Mathf.Clamp(selectedTeamIndex, 0, TeamIds.Length - 1);
            teamCursorRt.gameObject.SetActive(currentTeamPlayerIndex < teamSelectionPlayerIds.Count);
            teamCursorRt.sizeDelta = teamCursorSize;
            teamCursorRt.anchoredPosition = GetTeamRowPosition(rowIndex) + teamCursorOffset;
        }

    }

    private int CountMembersForTeam(BattleModeRules.TeamId teamId)
    {
        return Mathf.Max(1, GetVisibleTeamMemberPlayerIds(teamId).Count);
    }

    private List<int> GetVisibleTeamMemberPlayerIds(BattleModeRules.TeamId teamId)
    {
        List<int> result = new(GameSession.MaxPlayerId);
        for (int i = 0; i < teamSelectionPlayerIds.Count; i++)
        {
            int playerId = teamSelectionPlayerIds[i];
            bool isCurrent = i == currentTeamPlayerIndex;
            if (playerId >= 1 &&
                playerId < teamAssigned.Length &&
                teamAssigned[playerId] &&
                workingTeams[playerId - 1] == teamId)
            {
                result.Add(playerId);
            }
            else if (isCurrent && TeamIds[Mathf.Clamp(selectedTeamIndex, 0, TeamIds.Length - 1)] == teamId)
            {
                result.Add(playerId);
            }
        }

        return result;
    }

    private void EnsureTeamMemberImageCount(TeamRowVisual row, int count)
    {
        while (row.members.Count < count)
        {
            GameObject go = new($"Member_{row.members.Count}", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(row.root, false);
            Image image = go.GetComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
            row.members.Add(image);
        }
    }

    private bool IsCurrentTeamSelectionPlayer(int playerId)
    {
        return currentTeamPlayerIndex >= 0 &&
               currentTeamPlayerIndex < teamSelectionPlayerIds.Count &&
               teamSelectionPlayerIds[currentTeamPlayerIndex] == playerId;
    }

    private void ApplyTeamLabelFont(TextMeshProUGUI label)
    {
        if (label == null)
            return;

        if (leftPanel != null && leftPanel.OptionFontAsset != null)
            label.font = leftPanel.OptionFontAsset;

        if (leftPanel != null && leftPanel.OptionFontMaterialPreset != null)
            label.fontMaterial = leftPanel.OptionFontMaterialPreset;
    }

    private Vector2 GetTeamMemberPosition(int index, int count, bool celebrating)
    {
        float spacing = Mathf.Max(1f, teamMemberSpacing);
        float startX = teamMembersCenterOffsetX - ((Mathf.Max(1, count) - 1) * spacing * 0.5f);
        float y = celebrating ? teamCelebrationMemberOffsetY : teamWalkingMemberOffsetY;
        return new Vector2(startX + (index * spacing), y);
    }

    private Sprite GetTeamMemberSprite(int playerId, bool isCurrent)
    {
        BomberSkin skin = PlayerPersistentStats.Get(playerId).Skin;
        bool assigned = playerId >= 1 && playerId < teamAssigned.Length && teamAssigned[playerId];
        float celebrationTime = GetTeamCelebrationTime(playerId);

        if (celebrationTime >= 0f)
        {
            int frame = Mathf.FloorToInt(celebrationTime / 0.1f);
            return skinSelectMenu != null
                ? skinSelectMenu.GetBattleModeTeamCelebrationSprite(skin, frame)
                : null;
        }

        if (assigned)
        {
            return skinSelectMenu != null
                ? skinSelectMenu.GetBattleModeTeamCelebrationSprite(skin, int.MaxValue)
                : null;
        }

        int previewFrame = Mathf.FloorToInt(teamPreviewTimer / 0.22f);
        return skinSelectMenu != null
            ? skinSelectMenu.GetBattleModeTeamPreviewSprite(skin, previewFrame)
            : null;
    }

    private Color GetTeamMemberColor(int playerId, bool isCurrent)
    {
        Color color = Color.white;
        bool assigned = playerId >= 1 && playerId < teamAssigned.Length && teamAssigned[playerId];

        if (isCurrent && !assigned)
        {
            float pulse = (Mathf.Sin(Time.unscaledTime * Mathf.Max(0.01f, teamCurrentBlinkSpeed)) + 1f) * 0.5f;
            color.a = Mathf.Lerp(teamCurrentMinAlpha, teamCurrentMaxAlpha, pulse);
        }

        return color;
    }

    private void TickTeamCelebrationTimers()
    {
        if (teamCelebrationTimers == null)
            return;

        float duration = Mathf.Max(0f, confirmFeedbackSeconds);
        for (int i = 0; i < teamCelebrationTimers.Length; i++)
        {
            if (teamCelebrationTimers[i] < 0f)
                continue;

            teamCelebrationTimers[i] += Time.unscaledDeltaTime;
            if (teamCelebrationTimers[i] >= duration)
            {
                teamCelebrationTimers[i] = -1f;
                if (teamCelebrationCompleted != null && i < teamCelebrationCompleted.Length)
                    teamCelebrationCompleted[i] = true;
            }
        }
    }

    private float GetTeamCelebrationTime(int playerId)
    {
        if (teamCelebrationTimers == null || playerId < 0 || playerId >= teamCelebrationTimers.Length)
            return -1f;

        return teamCelebrationTimers[playerId];
    }

    private bool HasCompletedTeamCelebration(int playerId)
    {
        return teamCelebrationCompleted != null &&
               playerId >= 0 &&
               playerId < teamCelebrationCompleted.Length &&
               teamCelebrationCompleted[playerId];
    }

    private static int IndexOfTeam(BattleModeRules.TeamId teamId)
    {
        for (int i = 0; i < TeamIds.Length; i++)
            if (TeamIds[i] == teamId)
                return i;

        return 0;
    }

    private bool IsLastTeamSelectionPlayer()
    {
        return currentTeamPlayerIndex >= teamSelectionPlayerIds.Count - 1;
    }

    private bool WouldAllPlayersBeOnSameTeam(int pendingPlayerId, BattleModeRules.TeamId pendingTeam)
    {
        if (teamSelectionPlayerIds.Count <= 1)
            return true;

        BattleModeRules.TeamId? firstTeam = null;
        for (int i = 0; i < teamSelectionPlayerIds.Count; i++)
        {
            int playerId = teamSelectionPlayerIds[i];
            BattleModeRules.TeamId team = playerId == pendingPlayerId
                ? pendingTeam
                : workingTeams[Mathf.Clamp(playerId - 1, 0, workingTeams.Length - 1)];

            if (!firstTeam.HasValue)
            {
                firstTeam = team;
                continue;
            }

            if (firstTeam.Value != team)
                return false;
        }

        return true;
    }

    private Color GetTeamColor(BattleModeRules.TeamId teamId)
    {
        return teamId switch
        {
            BattleModeRules.TeamId.Red => teamRedColor,
            BattleModeRules.TeamId.Green => teamGreenColor,
            _ => teamBlueColor
        };
    }

    private static string GetTeamDisplayName(BattleModeRules.TeamId teamId)
    {
        return teamId switch
        {
            BattleModeRules.TeamId.Red => "Red",
            BattleModeRules.TeamId.Green => "Green",
            _ => "Blue"
        };
    }

    private IEnumerator OpenRuleConfigMenu()
    {
        ruleConfigReturnedToSkinSelect = false;
        ruleConfigReturnedToTeamSelect = false;
        state = MenuState.RuleConfig;
        menuActive = false;
        cursorConfirmVisual = false;

        if (leftPanel != null)
        {
            leftPanel.HideCursor();
            leftPanel.gameObject.SetActive(false);
        }

        if (teamSelectRoot != null)
            teamSelectRoot.gameObject.SetActive(false);
        if (stageSelectRoot != null)
            stageSelectRoot.gameObject.SetActive(false);

        SyncRuleConfigLayoutFromPlayerSelect();
        EnsureRuleConfigBuilt();
        LoadRuleConfigFromSave();
        selectedRuleIndex = Mathf.Clamp(selectedRuleIndex, 0, ruleRows.Count - 1);
        ResetBackgroundSpriteSwap();
        ApplyCurrentBackgroundSprite(true);
        UpdatePromptTitle();
        UpdateRuleConfigVisuals();
        Canvas.ForceUpdateCanvases();
        ApplyDynamicScaleIfNeeded(true);

        PlayerInputManager input = PlayerInputManager.Instance;
        while (input != null && HasAnyRelevantHeldInput(input, out _, out _))
            yield return null;

        bool done = false;
        while (!done)
        {
            if (input == null)
            {
                input = PlayerInputManager.Instance;
                yield return null;
                continue;
            }

            bool moved = false;
            if (input.GetDown(GameSession.MinPlayerId, PlayerAction.MoveUp))
            {
                selectedRuleIndex = WrapIndex(selectedRuleIndex - 1, ruleRows.Count);
                moved = true;
            }
            else if (input.GetDown(GameSession.MinPlayerId, PlayerAction.MoveDown))
            {
                selectedRuleIndex = WrapIndex(selectedRuleIndex + 1, ruleRows.Count);
                moved = true;
            }

            if (moved)
            {
                PlaySfx(moveCursorSfx, moveCursorSfxVolume);
                UpdateRuleConfigVisuals();
            }

            bool previousValue = input.GetDown(GameSession.MinPlayerId, PlayerAction.MoveLeft) ||
                                 input.GetDown(GameSession.MinPlayerId, PlayerAction.ActionL);
            bool nextValue = input.GetDown(GameSession.MinPlayerId, PlayerAction.MoveRight) ||
                             input.GetDown(GameSession.MinPlayerId, PlayerAction.ActionR);
            if (previousValue || nextValue)
            {
                CycleSelectedRuleValue(nextValue ? 1 : -1);
                PlaySfx(moveCursorSfx, moveCursorSfxVolume);
                UpdateRuleConfigVisuals();
            }
            else if (input.GetDown(GameSession.MinPlayerId, PlayerAction.ActionB))
            {
                PlaySfx(returnSfx, returnSfxVolume);
                if (SelectedMatchMode == BattleModeRules.MatchMode.TagMatch)
                    ruleConfigReturnedToTeamSelect = true;
                else
                    ruleConfigReturnedToSkinSelect = true;

                done = true;
            }
            else if (input.GetDown(GameSession.MinPlayerId, PlayerAction.ActionA) ||
                     input.GetDown(GameSession.MinPlayerId, PlayerAction.Start))
            {
                PlaySfx(confirmSfx, confirmSfxVolume);
                SaveRuleConfig();
                done = true;
            }

            UpdateRuleConfigVisuals();
            yield return null;
        }

        if (ruleConfigRoot != null)
            ruleConfigRoot.gameObject.SetActive(false);

        state = SelectedMatchMode == BattleModeRules.MatchMode.TagMatch && ruleConfigReturnedToTeamSelect
            ? MenuState.TeamSelect
            : MenuState.SkinSelect;
    }

    private void EnsureRuleConfigBuilt()
    {
        SyncRuleConfigLayoutFromPlayerSelect();

        if (ruleConfigRoot != null)
        {
            ruleConfigRoot.gameObject.SetActive(true);
            ruleConfigRoot.SetAsLastSibling();
            return;
        }

        Transform parent = referenceRect != null ? referenceRect : (root != null ? root.transform : transform);
        GameObject rootGo = new("RuleConfigRoot", typeof(RectTransform));
        rootGo.transform.SetParent(parent, false);
        ruleConfigRoot = rootGo.GetComponent<RectTransform>();
        ruleConfigRoot.anchorMin = new Vector2(0.5f, 0.5f);
        ruleConfigRoot.anchorMax = new Vector2(0.5f, 0.5f);
        ruleConfigRoot.pivot = new Vector2(0.5f, 0.5f);
        ruleConfigRoot.anchoredPosition = Vector2.zero;
        ruleConfigRoot.sizeDelta = Vector2.zero;
        ruleConfigRoot.localScale = Vector3.one * currentUiScale;
        ruleConfigRoot.SetAsLastSibling();

        ruleRows.Clear();
        for (int i = 0; i < 5; i++)
            ruleRows.Add(CreateRuleRow(i));

        CreateRuleCursor();
    }

    private void SyncRuleConfigLayoutFromPlayerSelect()
    {
        if (!syncRuleConfigLayoutWithPlayerSelect || playerSelectLayout == null)
            return;

        if (syncRuleConfigFontWithPlayerSelect)
            ruleFontSize = playerSelectLayout.fontSize;

        ruleRowSize = new Vector2(ruleRowSize.x, playerSelectLayout.optionItemHeight);
        ruleRowSpacing = Mathf.Max(0f, playerSelectLayout.optionSpacing.y);

        float cursorSize = Mathf.Max(
            playerSelectLayout.minCursorSize,
            playerSelectLayout.optionItemHeight * Mathf.Max(0.01f, playerSelectLayout.cursorHeightMultiplier));
        ruleCursorSize = new Vector2(cursorSize, cursorSize);

        ruleLabelSize = new Vector2(ruleLabelSize.x, playerSelectLayout.optionItemHeight);
        ruleValueSize = new Vector2(ruleValueSize.x, playerSelectLayout.optionItemHeight);
    }

    private RuleRowVisual CreateRuleRow(int rowIndex)
    {
        GameObject rowGo = new($"RuleRow_{rowIndex}", typeof(RectTransform));
        rowGo.transform.SetParent(ruleConfigRoot, false);

        RectTransform rowRt = rowGo.GetComponent<RectTransform>();
        rowRt.anchorMin = new Vector2(0.5f, 0.5f);
        rowRt.anchorMax = new Vector2(0.5f, 0.5f);
        rowRt.pivot = new Vector2(0.5f, 0.5f);
        rowRt.sizeDelta = ruleRowSize;
        rowRt.anchoredPosition = GetRuleRowPosition(rowIndex);

        TextMeshProUGUI label = CreateRuleText(rowRt, "Label");
        TextMeshProUGUI value = CreateRuleText(rowRt, "Value");

        return new RuleRowVisual
        {
            root = rowRt,
            label = label,
            value = value
        };
    }

    private TextMeshProUGUI CreateRuleText(RectTransform parent, string name)
    {
        GameObject go = new(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.fontSize = ruleFontSize;
        text.color = Color.white;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        ApplyRuleTextStyle(text);
        return text;
    }

    private void CreateRuleCursor()
    {
        AnimatedSpriteRenderer source = leftPanel != null ? leftPanel.CursorRenderer : null;
        GameObject cursorGo;

        if (source != null)
        {
            cursorGo = Instantiate(source.gameObject, ruleConfigRoot, false);
            cursorGo.name = "RuleCursor";
            ruleCursorRenderer = cursorGo.GetComponent<AnimatedSpriteRenderer>();
        }
        else
        {
            cursorGo = new GameObject("RuleCursor", typeof(RectTransform));
        }

        ruleCursorRt = cursorGo.transform as RectTransform;
        if (ruleCursorRt == null)
            ruleCursorRt = cursorGo.AddComponent<RectTransform>();

        ruleCursorRt.anchorMin = new Vector2(0.5f, 0.5f);
        ruleCursorRt.anchorMax = new Vector2(0.5f, 0.5f);
        ruleCursorRt.pivot = new Vector2(0.5f, 0.5f);
        ruleCursorRt.sizeDelta = ruleCursorSize;
        ruleCursorRt.localScale = Vector3.one;

        if (ruleCursorRenderer != null)
        {
            ruleCursorRenderer.idle = true;
            ruleCursorRenderer.loop = true;
            ruleCursorRenderer.CurrentFrame = 0;
            ruleCursorRenderer.RefreshFrame();
        }
    }

    private Vector2 GetRuleRowPosition(int rowIndex)
    {
        float totalHeight = (ruleRows.Count - 1) * (ruleRowSize.y + ruleRowSpacing);
        float y = (totalHeight * 0.5f) - (rowIndex * (ruleRowSize.y + ruleRowSpacing));
        return ruleRowsOffset + new Vector2(0f, y);
    }

    private void LoadRuleConfigFromSave()
    {
        ruleComputerLevel = SaveSystem.GetBattleModeComputerLevel();
        ruleBattlesToWin = SaveSystem.GetBattleModeBattlesToWin();
        ruleTimerMode = SaveSystem.GetBattleModeRoundTimerMode();
        ruleSuddenDeath = SaveSystem.GetBattleModeSuddenDeathSetting();
        ruleRevengeBomber = SaveSystem.GetBattleModeRevengeBomberEnabled();
    }

    private void SaveRuleConfig()
    {
        SaveSystem.SetBattleModeComputerLevel(ruleComputerLevel);
        SaveSystem.SetBattleModeBattlesToWin(ruleBattlesToWin);
        SaveSystem.SetBattleModeRoundTimerMode(ruleTimerMode);
        SaveSystem.SetBattleModeSuddenDeathSetting(ruleSuddenDeath);
        SaveSystem.SetBattleModeRevengeBomberEnabled(ruleRevengeBomber);
    }

    private void UpdateRuleConfigVisuals()
    {
        if (ruleConfigRoot == null || !ruleConfigRoot.gameObject.activeInHierarchy)
            return;

        SyncRuleConfigLayoutFromPlayerSelect();

        string[] labels =
        {
            "Computer Level",
            "Battles To Win",
            "Time Limit",
            "Sudden Death",
            "Revenge Bomber"
        };

        string[] values =
        {
            ColorizeRuleValue(GetComputerLevelDisplayName(ruleComputerLevel), GetComputerLevelColor(ruleComputerLevel)),
            ColorizeRuleValue(ruleBattlesToWin.ToString(), ruleGreenHex),
            ColorizeRuleValue(GetTimerModeDisplayName(ruleTimerMode), ruleGreenHex),
            ColorizeRuleValue(GetSuddenDeathDisplayName(ruleSuddenDeath), GetSuddenDeathColor(ruleSuddenDeath)),
            ColorizeRuleValue(ruleRevengeBomber ? "ON" : "OFF", ruleRevengeBomber ? ruleGreenHex : ruleBlueHex)
        };

        for (int i = 0; i < ruleRows.Count; i++)
        {
            RuleRowVisual row = ruleRows[i];
            if (row == null)
                continue;

            row.root.anchoredPosition = GetRuleRowPosition(i);
            row.root.sizeDelta = ruleRowSize;

            if (row.label != null)
            {
                RectTransform labelRt = row.label.rectTransform;
                labelRt.anchoredPosition = ruleLabelOffset;
                labelRt.sizeDelta = ruleLabelSize;
                row.label.fontSize = ruleFontSize;
                row.label.richText = true;
                row.label.text = i < labels.Length ? ColorizeRuleValue(labels[i], ruleGreenHex) : "";
                ApplyRuleTextStyle(row.label);
            }

            if (row.value != null)
            {
                RectTransform valueRt = row.value.rectTransform;
                valueRt.anchoredPosition = ruleValueOffset;
                valueRt.sizeDelta = ruleValueSize;
                row.value.fontSize = ruleFontSize;
                row.value.richText = true;
                row.value.text = i < values.Length ? values[i] : "";
                ApplyRuleTextStyle(row.value);
            }
        }

        if (ruleCursorRt != null)
        {
            int rowIndex = Mathf.Clamp(selectedRuleIndex, 0, Mathf.Max(0, ruleRows.Count - 1));
            ruleCursorRt.gameObject.SetActive(ruleRows.Count > 0);
            ruleCursorRt.sizeDelta = ruleCursorSize;
            ruleCursorRt.anchoredPosition = GetRuleRowPosition(rowIndex) + ruleCursorOffset;
        }

    }

    private void CycleSelectedRuleValue(int direction)
    {
        switch (selectedRuleIndex)
        {
            case 0:
                ruleComputerLevel = ComputerLevels[WrapIndex(IndexOfComputerLevel(ruleComputerLevel) + direction, ComputerLevels.Length)];
                break;
            case 1:
                ruleBattlesToWin = WrapIndex((ruleBattlesToWin - 1) + direction, 5) + 1;
                break;
            case 2:
                ruleTimerMode = RuleTimerModes[WrapIndex(IndexOfTimerMode(ruleTimerMode) + direction, RuleTimerModes.Length)];
                break;
            case 3:
                ruleSuddenDeath = SuddenDeathSettings[WrapIndex(IndexOfSuddenDeath(ruleSuddenDeath) + direction, SuddenDeathSettings.Length)];
                break;
            case 4:
                ruleRevengeBomber = !ruleRevengeBomber;
                break;
        }
    }

    private void ApplyRuleTextStyle(TextMeshProUGUI text)
    {
        if (text == null)
            return;

        if (leftPanel != null)
        {
            leftPanel.ApplyOptionTextStyleTo(text, Color.white);
            text.fontSize = ruleFontSize;
            text.richText = true;
            text.UpdateMeshPadding();
            text.ForceMeshUpdate();
            text.SetVerticesDirty();
            return;
        }

        ApplyTeamLabelFont(text);
        text.fontSize = ruleFontSize;
        text.richText = true;
        text.UpdateMeshPadding();
        text.ForceMeshUpdate();
        text.SetVerticesDirty();
    }

    private static int IndexOfComputerLevel(BattleModeComputerLevel level)
    {
        for (int i = 0; i < ComputerLevels.Length; i++)
            if (ComputerLevels[i] == level)
                return i;

        return 1;
    }

    private static int IndexOfTimerMode(BattleModeRules.RoundTimerMode timerMode)
    {
        for (int i = 0; i < RuleTimerModes.Length; i++)
            if (RuleTimerModes[i] == timerMode)
                return i;

        return 1;
    }

    private static int IndexOfSuddenDeath(BattleModeSuddenDeathSetting setting)
    {
        for (int i = 0; i < SuddenDeathSettings.Length; i++)
            if (SuddenDeathSettings[i] == setting)
                return i;

        return 2;
    }

    private static string GetComputerLevelDisplayName(BattleModeComputerLevel level)
    {
        return level switch
        {
            BattleModeComputerLevel.Easy => "Easy",
            BattleModeComputerLevel.Hard => "Hard",
            _ => "Normal"
        };
    }

    private string GetComputerLevelColor(BattleModeComputerLevel level)
    {
        return level switch
        {
            BattleModeComputerLevel.Easy => ruleBlueHex,
            BattleModeComputerLevel.Hard => ruleRedHex,
            _ => ruleGreenHex
        };
    }

    private static string GetTimerModeDisplayName(BattleModeRules.RoundTimerMode timerMode)
    {
        return timerMode switch
        {
            BattleModeRules.RoundTimerMode.OneMinute => "1:00",
            BattleModeRules.RoundTimerMode.TwoMinutes => "2:00",
            BattleModeRules.RoundTimerMode.ThreeMinutes => "3:00",
            BattleModeRules.RoundTimerMode.FourMinutes => "4:00",
            BattleModeRules.RoundTimerMode.FiveMinutes => "5:00",
            _ => "Infinite"
        };
    }

    private static string GetSuddenDeathDisplayName(BattleModeSuddenDeathSetting setting)
    {
        return setting switch
        {
            BattleModeSuddenDeathSetting.Off => "OFF",
            BattleModeSuddenDeathSetting.On => "ON",
            _ => "Random"
        };
    }

    private string GetSuddenDeathColor(BattleModeSuddenDeathSetting setting)
    {
        return setting switch
        {
            BattleModeSuddenDeathSetting.Off => ruleBlueHex,
            BattleModeSuddenDeathSetting.On => ruleGreenHex,
            _ => ruleRedHex
        };
    }

    private static string ColorizeRuleValue(string value, string colorHex)
    {
        return $"<color={colorHex}>{value}</color>";
    }

    private IEnumerator OpenStageSelectMenu()
    {
        stageSelectionReturnedToRuleConfig = false;
        state = MenuState.StageSelect;
        menuActive = false;
        cursorConfirmVisual = false;

        if (leftPanel != null)
        {
            leftPanel.HideCursor();
            leftPanel.gameObject.SetActive(false);
        }

        if (teamSelectRoot != null)
            teamSelectRoot.gameObject.SetActive(false);
        if (ruleConfigRoot != null)
            ruleConfigRoot.gameObject.SetActive(false);

        EnsureStageSelectBuilt();
        selectedStageIndex = Mathf.Clamp(SaveSystem.GetBattleModeStageIndex() - 1, 0, GetBattleStageCount() - 1);
        stageCarouselFromIndex = selectedStageIndex;
        stageCarouselDirection = 0;
        stageCarouselElapsed = 0f;
        stageCarouselAnimating = false;
        ResetBackgroundSpriteSwap();
        ApplyCurrentBackgroundSprite(true);
        UpdatePromptTitle();
        UpdateStageSelectVisuals();

        Canvas.ForceUpdateCanvases();
        ApplyDynamicScaleIfNeeded(true);

        PlayerInputManager input = PlayerInputManager.Instance;
        while (input != null && HasAnyRelevantHeldInput(input, out _, out _))
            yield return null;

        bool done = false;
        while (!done)
        {
            if (input == null)
            {
                input = PlayerInputManager.Instance;
                yield return null;
                continue;
            }

            bool moved = false;
            if (!stageCarouselAnimating &&
                (input.GetDown(GameSession.MinPlayerId, PlayerAction.MoveLeft) ||
                input.GetDown(GameSession.MinPlayerId, PlayerAction.MoveUp) ||
                input.GetDown(GameSession.MinPlayerId, PlayerAction.ActionL)))
            {
                BeginStageCarouselMove(-1);
                moved = true;
            }
            else if (!stageCarouselAnimating &&
                     (input.GetDown(GameSession.MinPlayerId, PlayerAction.MoveRight) ||
                     input.GetDown(GameSession.MinPlayerId, PlayerAction.MoveDown) ||
                     input.GetDown(GameSession.MinPlayerId, PlayerAction.ActionR)))
            {
                BeginStageCarouselMove(1);
                moved = true;
            }

            if (moved)
            {
                PlaySfx(moveCursorSfx, moveCursorSfxVolume);
                UpdateStageSelectVisuals();
            }
            else if (input.GetDown(GameSession.MinPlayerId, PlayerAction.ActionB))
            {
                PlaySfx(returnSfx, returnSfxVolume);
                stageSelectionReturnedToRuleConfig = true;
                done = true;
            }
            else if (input.GetDown(GameSession.MinPlayerId, PlayerAction.ActionA) ||
                     input.GetDown(GameSession.MinPlayerId, PlayerAction.Start))
            {
                PlaySfx(confirmSfx, confirmSfxVolume);
                int stageIndex = selectedStageIndex + 1;
                SaveSystem.SetBattleModeStageIndex(stageIndex);
                done = true;
            }

            UpdateStageSelectVisuals();
            yield return null;
        }

        if (stageSelectRoot != null)
            stageSelectRoot.gameObject.SetActive(false);

        state = MenuState.RuleConfig;
    }

    private void EnsureStageSelectBuilt()
    {
        if (stageSelectRoot != null)
        {
            stageSelectRoot.gameObject.SetActive(true);
            stageSelectRoot.SetAsLastSibling();
            return;
        }

        Transform parent = referenceRect != null ? referenceRect : (root != null ? root.transform : transform);
        GameObject rootGo = new("StageSelectRoot", typeof(RectTransform));
        rootGo.transform.SetParent(parent, false);
        stageSelectRoot = rootGo.GetComponent<RectTransform>();
        stageSelectRoot.anchorMin = new Vector2(0.5f, 0.5f);
        stageSelectRoot.anchorMax = new Vector2(0.5f, 0.5f);
        stageSelectRoot.pivot = new Vector2(0.5f, 0.5f);
        stageSelectRoot.anchoredPosition = stageSelectRootOffset;
        stageSelectRoot.sizeDelta = Vector2.zero;
        stageSelectRoot.localScale = Vector3.one * currentUiScale;
        stageSelectRoot.SetAsLastSibling();

        stageTitleText = CreateStageText(stageSelectRoot, "StageTitle", TextAlignmentOptions.Center);
        stageCarouselViewport = CreateStageCarouselViewport(stageSelectRoot);
        stageThumbnailImages = new Image[5];
        for (int i = 0; i < stageThumbnailImages.Length; i++)
            stageThumbnailImages[i] = CreateStageThumbnail(stageCarouselViewport, $"StageThumbnail_{i}");
        stageNameText = CreateStageText(stageSelectRoot, "StageName", TextAlignmentOptions.Center);
    }

    private RectTransform CreateStageCarouselViewport(RectTransform parent)
    {
        GameObject go = new("StageCarouselViewport", typeof(RectTransform), typeof(RectMask2D));
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = GetStageCarouselMaskUiSize();

        return rt;
    }

    private TextMeshProUGUI CreateStageText(RectTransform parent, string name, TextAlignmentOptions alignment)
    {
        GameObject go = new(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.alignment = alignment;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        ApplyStageTextStyle(text);
        return text;
    }

    private Image CreateStageThumbnail(RectTransform parent, string name)
    {
        GameObject go = new(name, typeof(RectTransform), typeof(Image), typeof(Outline));
        go.transform.SetParent(parent, false);

        Image image = go.GetComponent<Image>();
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.pixelsPerUnitMultiplier = 1f;

        Outline outline = go.GetComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(2f, -2f);

        return image;
    }

    private void UpdateStageSelectVisuals()
    {
        if (stageSelectRoot == null || !stageSelectRoot.gameObject.activeInHierarchy)
            return;

        stageSelectRoot.anchoredPosition = stageSelectRootOffset;
        TickStageCarouselAnimation();

        if (stageCarouselViewport != null)
        {
            stageCarouselViewport.anchoredPosition = Vector2.zero;
            stageCarouselViewport.sizeDelta = GetStageCarouselMaskUiSize();
        }

        int stageIndex = Mathf.Clamp(selectedStageIndex + 1, 1, GetBattleStageCount());

        if (stageTitleText != null)
        {
            stageTitleText.text = $"STAGE {stageIndex}";
            stageTitleText.fontSize = stageSelectFontSize;
            stageTitleText.rectTransform.anchoredPosition = stageTitleOffset;
            stageTitleText.rectTransform.sizeDelta = stageTitleSize;
            ApplyStageTextStyle(stageTitleText);
        }

        UpdateStageCarouselImages();

        if (stageNameText != null)
        {
            stageNameText.text = GetBattleStageDisplayName(stageIndex);
            stageNameText.fontSize = stageSelectFontSize;
            stageNameText.rectTransform.anchoredPosition = stageNameOffset;
            stageNameText.rectTransform.sizeDelta = stageNameSize;
            ApplyStageTextStyle(stageNameText);
        }
    }

    private void BeginStageCarouselMove(int direction)
    {
        int normalizedDirection = direction < 0 ? -1 : 1;
        stageCarouselFromIndex = Mathf.Clamp(selectedStageIndex, 0, GetBattleStageCount() - 1);
        selectedStageIndex = WrapIndex(selectedStageIndex + normalizedDirection, GetBattleStageCount());
        stageCarouselDirection = normalizedDirection;
        stageCarouselElapsed = 0f;
        stageCarouselAnimating = true;
    }

    private void TickStageCarouselAnimation()
    {
        if (!stageCarouselAnimating)
            return;

        stageCarouselElapsed += Time.unscaledDeltaTime;
        if (stageCarouselElapsed < Mathf.Max(0.01f, stageCarouselTransitionSeconds))
            return;

        stageCarouselAnimating = false;
        stageCarouselFromIndex = selectedStageIndex;
        stageCarouselDirection = 0;
        stageCarouselElapsed = 0f;
    }

    private void UpdateStageCarouselImages()
    {
        if (stageThumbnailImages == null || stageThumbnailImages.Length == 0)
            return;

        int count = GetBattleStageCount();
        int direction = stageCarouselAnimating ? stageCarouselDirection : 0;
        float progress = stageCarouselAnimating
            ? Mathf.Clamp01(stageCarouselElapsed / Mathf.Max(0.01f, stageCarouselTransitionSeconds))
            : 0f;
        float easedProgress = Mathf.SmoothStep(0f, 1f, progress);
        int firstOffset = direction < 0 ? -3 : -1;
        if (!stageCarouselAnimating)
            firstOffset = -2;

        for (int i = 0; i < stageThumbnailImages.Length; i++)
        {
            Image image = stageThumbnailImages[i];
            if (image == null)
                continue;

            int offset = firstOffset + i;
            int zeroBasedStageIndex = WrapIndex(stageCarouselFromIndex + offset, count);
            float relativePosition = offset - (direction * easedProgress);
            ApplyStageCarouselImage(image, zeroBasedStageIndex + 1, relativePosition);
        }
    }

    private void ApplyStageCarouselImage(Image image, int stageIndex, float relativePosition)
    {
        RectTransform thumbnailRt = image.rectTransform;
        thumbnailRt.anchorMin = new Vector2(0.5f, 0.5f);
        thumbnailRt.anchorMax = new Vector2(0.5f, 0.5f);
        thumbnailRt.pivot = new Vector2(0.5f, 0.5f);

        float absRelative = Mathf.Abs(relativePosition);
        bool visible = absRelative <= 2.01f;
        image.gameObject.SetActive(visible);
        if (!visible)
            return;

        Vector2 sideOffset = new(
            stageSideThumbnailOffset.x * relativePosition,
            stageSideThumbnailOffset.y * relativePosition);
        thumbnailRt.anchoredPosition = stageThumbnailOffset + sideOffset;

        float centerToSide = Mathf.Clamp01(absRelative);
        Vector2 centerSize = GetStageThumbnailUiSize();
        float sideScale = Mathf.Max(0.01f, stageSideThumbnailScale);
        thumbnailRt.sizeDelta = Vector2.Lerp(centerSize, centerSize * sideScale, centerToSide);

        Sprite sprite = GetBattleStageThumbnail(stageIndex);
        image.sprite = sprite;
        Color color = sprite != null ? Color.white : stageThumbnailFallbackColor;
        if (absRelative > 1f)
            color.a *= Mathf.Clamp01(2f - absRelative);

        image.color = color;
        image.enabled = true;
        if (absRelative < 0.5f)
            image.transform.SetAsLastSibling();
        else
            image.transform.SetAsFirstSibling();
    }

    private void ApplyStageTextStyle(TextMeshProUGUI text)
    {
        if (text == null)
            return;

        if (leftPanel != null)
        {
            leftPanel.ApplyOptionTextStyleTo(text, Color.white);
            text.fontSize = stageSelectFontSize;
            text.richText = true;
            text.alignment = TextAlignmentOptions.Center;
            text.UpdateMeshPadding();
            text.ForceMeshUpdate();
            text.SetVerticesDirty();
            return;
        }

        ApplyTeamLabelFont(text);
        text.fontSize = stageSelectFontSize;
        text.richText = true;
        text.alignment = TextAlignmentOptions.Center;
    }

    private int GetBattleStageCount()
    {
        return Mathf.Clamp(battleStageCount, 1, 15);
    }

    private string GetBattleStageSceneName(int stageIndex)
    {
        int normalized = Mathf.Clamp(stageIndex, 1, 15);
        return $"{battleStageScenePrefix}{normalized}";
    }

    private Sprite GetBattleStageThumbnail(int stageIndex)
    {
        int index = stageIndex - 1;
        Sprite resourceSprite = GetBattleStageThumbnailFromResources(index, stageIndex);
        if (resourceSprite != null)
            return resourceSprite;

        return battleStageThumbnails != null && index >= 0 && index < battleStageThumbnails.Length
            ? battleStageThumbnails[index]
            : null;
    }

    private Vector2 GetStageThumbnailUiSize()
    {
        float authoredScale = Mathf.Max(1, designUpscale);
        return new Vector2(
            Mathf.Max(1f, stageThumbnailSize.x) * authoredScale,
            Mathf.Max(1f, stageThumbnailSize.y) * authoredScale);
    }

    private Vector2 GetStageCarouselMaskUiSize()
    {
        float authoredScale = Mathf.Max(1, designUpscale);
        Vector2 baseSize = stageCarouselMaskSize;
        if (baseSize.x <= 0f || baseSize.y <= 0f)
            baseSize = new Vector2(Mathf.Max(1, referenceWidth), Mathf.Max(1, referenceHeight));

        float horizontalInset = Mathf.Max(0f, stageCarouselMaskHorizontalInset) * 2f;
        baseSize.x = Mathf.Max(1f, baseSize.x - horizontalInset);

        return new Vector2(
            Mathf.Max(1f, baseSize.x) * authoredScale,
            Mathf.Max(1f, baseSize.y) * authoredScale);
    }

    private Sprite GetBattleStageThumbnailFromResources(int index, int stageIndex)
    {
        if (string.IsNullOrWhiteSpace(battleStageThumbnailResourceFormat) || index < 0)
            return null;

        int count = GetBattleStageCount();
        if (battleStageThumbnailResourceCache == null || battleStageThumbnailResourceCache.Length != count)
            battleStageThumbnailResourceCache = new Sprite[count];

        if (index >= battleStageThumbnailResourceCache.Length)
            return null;

        if (battleStageThumbnailResourceCache[index] != null)
            return battleStageThumbnailResourceCache[index];

        string resourcePath = string.Format(battleStageThumbnailResourceFormat, stageIndex);
        Sprite sprite = Resources.Load<Sprite>(resourcePath);
        if (sprite == null)
        {
            Sprite[] sprites = Resources.LoadAll<Sprite>(resourcePath);
            if (sprites != null && sprites.Length > 0)
                sprite = sprites[0];
        }

        battleStageThumbnailResourceCache[index] = sprite;
        return sprite;
    }

    private string GetBattleStageDisplayName(int stageIndex)
    {
        int index = stageIndex - 1;
        if (battleStageNames != null &&
            index >= 0 &&
            index < battleStageNames.Length &&
            !string.IsNullOrWhiteSpace(battleStageNames[index]))
        {
            return battleStageNames[index];
        }

        return $"Battle Stage {stageIndex}";
    }

    private IEnumerator OpenSpecificSettingsMenu()
    {
        specificSettingsReturnedToStageSelect = false;
        specificStartConfirmed = false;
        battleStartBlinkTimer = 0f;
        battleStartVisible = false;
        state = MenuState.SpecificSettings;
        menuActive = false;
        cursorConfirmVisual = false;

        if (leftPanel != null)
        {
            leftPanel.HideCursor();
            leftPanel.gameObject.SetActive(false);
        }

        if (teamSelectRoot != null)
            teamSelectRoot.gameObject.SetActive(false);
        if (ruleConfigRoot != null)
            ruleConfigRoot.gameObject.SetActive(false);
        if (stageSelectRoot != null)
            stageSelectRoot.gameObject.SetActive(false);

        EnsureSpecificSettingsBuilt();
        selectedSpecificSettingIndex = SpecificSettingsOptions.Length - 1;
        ResetBackgroundSpriteSwap();
        ApplyCurrentBackgroundSprite(true);
        UpdatePromptTitle();
        UpdateSpecificSettingsVisuals();

        Canvas.ForceUpdateCanvases();
        ApplyDynamicScaleIfNeeded(true);

        PlayerInputManager input = PlayerInputManager.Instance;
        while (input != null && HasAnyRelevantHeldInput(input, out _, out _))
            yield return null;

        bool done = false;
        while (!done)
        {
            if (input == null)
            {
                input = PlayerInputManager.Instance;
                yield return null;
                continue;
            }

            if (input.GetDown(GameSession.MinPlayerId, PlayerAction.MoveUp))
            {
                selectedSpecificSettingIndex = WrapIndex(selectedSpecificSettingIndex - 1, SpecificSettingsOptions.Length);
                PlaySfx(moveCursorSfx, moveCursorSfxVolume);
                UpdateSpecificSettingsVisuals();
            }
            else if (input.GetDown(GameSession.MinPlayerId, PlayerAction.MoveDown))
            {
                selectedSpecificSettingIndex = WrapIndex(selectedSpecificSettingIndex + 1, SpecificSettingsOptions.Length);
                PlaySfx(moveCursorSfx, moveCursorSfxVolume);
                UpdateSpecificSettingsVisuals();
            }
            else if (input.GetDown(GameSession.MinPlayerId, PlayerAction.ActionB))
            {
                PlaySfx(returnSfx, returnSfxVolume);
                specificSettingsReturnedToStageSelect = true;
                done = true;
            }
            else if (input.GetDown(GameSession.MinPlayerId, PlayerAction.ActionA) ||
                     input.GetDown(GameSession.MinPlayerId, PlayerAction.Start))
            {
                LogSpecificStartTransition("InputConfirm.Down");
                if (selectedSpecificSettingIndex == SpecificSettingsOptions.Length - 1)
                {
                    yield return ConfirmSpecificSettingsStart();
                    yield break;
                }

                if (string.Equals(SpecificSettingsOptions[selectedSpecificSettingIndex], "Music", System.StringComparison.Ordinal))
                {
                    PlaySfx(confirmSfx, confirmSfxVolume);
                    yield return OpenMusicSelectMenu();
                    continue;
                }

                PlaySfx(deniedSfx, deniedSfxVolume);
            }

            UpdateSpecificSettingsVisuals();
            yield return null;
        }

        if (specificSettingsRoot != null)
            specificSettingsRoot.gameObject.SetActive(false);

        state = MenuState.StageSelect;
    }

    private IEnumerator OpenMusicSelectMenu()
    {
        state = MenuState.MusicSelect;
        musicSelectPreviewPlaying = false;
        selectedMusicIndex = Mathf.Clamp(selectedMusicIndex, 0, GetBattleMusicSelections().Length - 1);
        workingBattleMusicSelectionMask = SaveSystem.GetBattleModeMusicSelectionMask();

        if (specificSettingsRoot != null)
            specificSettingsRoot.gameObject.SetActive(false);

        EnsureMusicSelectBuilt();
        ResetBackgroundSpriteSwap();
        ApplyCurrentBackgroundSprite(true);
        UpdatePromptTitle();
        UpdateMusicSelectVisuals();

        Canvas.ForceUpdateCanvases();
        ApplyDynamicScaleIfNeeded(true);

        PlayerInputManager input = PlayerInputManager.Instance;
        while (input != null && HasAnyRelevantHeldInput(input, out _, out _))
            yield return null;

        bool done = false;
        while (!done)
        {
            if (input == null)
            {
                input = PlayerInputManager.Instance;
                yield return null;
                continue;
            }

            if (input.GetDown(GameSession.MinPlayerId, PlayerAction.MoveUp))
            {
                selectedMusicIndex = WrapIndex(selectedMusicIndex - 1, GetBattleMusicSelections().Length);
                PlaySfx(moveCursorSfx, moveCursorSfxVolume);
                UpdateMusicSelectVisuals();
            }
            else if (input.GetDown(GameSession.MinPlayerId, PlayerAction.MoveDown))
            {
                selectedMusicIndex = WrapIndex(selectedMusicIndex + 1, GetBattleMusicSelections().Length);
                PlaySfx(moveCursorSfx, moveCursorSfxVolume);
                UpdateMusicSelectVisuals();
            }
            else if (input.GetDown(GameSession.MinPlayerId, PlayerAction.ActionB))
            {
                PlaySfx(returnSfx, returnSfxVolume);
                SaveSystem.SetBattleModeMusicSelectionMask(workingBattleMusicSelectionMask);
                done = true;
            }
            else if (input.GetDown(GameSession.MinPlayerId, PlayerAction.ActionA) ||
                     input.GetDown(GameSession.MinPlayerId, PlayerAction.Start))
            {
                bool changedMusic = ToggleSelectedBattleMusic(out bool shouldPreviewMusic);
                PlaySfx(confirmSfx, confirmSfxVolume);
                if (changedMusic)
                    musicSelectCursorConfirmTimer = Mathf.Max(0.01f, confirmFeedbackSeconds);
                if (shouldPreviewMusic)
                    PreviewSelectedBattleMusic();
                UpdateMusicSelectVisuals();
            }

            UpdateMusicSelectVisuals();
            musicSelectCursorConfirmTimer = Mathf.Max(0f, musicSelectCursorConfirmTimer - Time.unscaledDeltaTime);
            yield return null;
        }

        if (musicSelectPreviewPlaying)
            StartSelectMusic();

        if (musicSelectRoot != null)
            musicSelectRoot.gameObject.SetActive(false);

        if (specificSettingsRoot != null)
            specificSettingsRoot.gameObject.SetActive(true);

        state = MenuState.SpecificSettings;
        UpdatePromptTitle();
        UpdateSpecificSettingsVisuals();
    }

    private void EnsureMusicSelectBuilt()
    {
        if (musicSelectRoot != null)
        {
            if (musicSelectCheckboxTexts.Count != musicSelectOptionTexts.Count)
            {
                for (int i = musicSelectCheckboxTexts.Count; i < musicSelectOptionTexts.Count; i++)
                    musicSelectCheckboxTexts.Add(CreateMusicSelectCheckboxText(i));
            }

            musicSelectRoot.gameObject.SetActive(true);
            musicSelectRoot.SetAsLastSibling();
            return;
        }

        Transform parent = GetMenuContentParent();
        GameObject rootGo = new("MusicSelectRoot", typeof(RectTransform));
        rootGo.transform.SetParent(parent, false);
        musicSelectRoot = rootGo.GetComponent<RectTransform>();
        musicSelectRoot.anchorMin = new Vector2(0.5f, 0.5f);
        musicSelectRoot.anchorMax = new Vector2(0.5f, 0.5f);
        musicSelectRoot.pivot = new Vector2(0.5f, 0.5f);
        musicSelectRoot.anchoredPosition = musicSelectRootOffset;
        musicSelectRoot.sizeDelta = Vector2.zero;
        musicSelectRoot.localScale = Vector3.one * currentUiScale;
        musicSelectRoot.SetAsLastSibling();

        musicSelectOptionTexts.Clear();
        musicSelectCheckboxTexts.Clear();
        BattleModeRules.BattleMusicSelection[] selections = GetBattleMusicSelections();
        for (int i = 0; i < selections.Length; i++)
        {
            musicSelectOptionTexts.Add(CreateMusicSelectOptionText(i));
            musicSelectCheckboxTexts.Add(CreateMusicSelectCheckboxText(i));
        }

        CreateMusicSelectCursor();
    }

    private TextMeshProUGUI CreateMusicSelectOptionText(int rowIndex)
    {
        GameObject go = new($"MusicOption_{rowIndex}", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(musicSelectRoot, false);

        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.fontSize = musicSelectFontSize;
        text.color = Color.white;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        ApplySpecificSettingsTextStyle(text);
        return text;
    }

    private TextMeshProUGUI CreateMusicSelectCheckboxText(int rowIndex)
    {
        GameObject go = new($"MusicCheckbox_{rowIndex}", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(musicSelectRoot, false);

        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = musicSelectFontSize;
        text.color = Color.white;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        ApplySpecificSettingsTextStyle(text);
        text.alignment = TextAlignmentOptions.Center;
        return text;
    }

    private void CreateMusicSelectCursor()
    {
        AnimatedSpriteRenderer source = leftPanel != null ? leftPanel.CursorRenderer : null;
        GameObject cursorGo;

        if (source != null)
        {
            cursorGo = Instantiate(source.gameObject, musicSelectRoot, false);
            cursorGo.name = "MusicSelectCursor";
            musicSelectCursorRenderer = cursorGo.GetComponent<AnimatedSpriteRenderer>();
        }
        else
        {
            cursorGo = new GameObject("MusicSelectCursor", typeof(RectTransform));
        }

        musicSelectCursorRt = cursorGo.transform as RectTransform;
        if (musicSelectCursorRt == null)
            musicSelectCursorRt = cursorGo.AddComponent<RectTransform>();

        musicSelectCursorRt.anchorMin = new Vector2(0.5f, 0.5f);
        musicSelectCursorRt.anchorMax = new Vector2(0.5f, 0.5f);
        musicSelectCursorRt.pivot = new Vector2(0.5f, 0.5f);
        musicSelectCursorRt.sizeDelta = musicSelectCursorSize;
        musicSelectCursorRt.localScale = Vector3.one;

        if (musicSelectCursorRenderer != null)
        {
            musicSelectCursorRenderer.SetFrozen(false);
            musicSelectCursorRenderer.frameOffsets = null;
            musicSelectCursorRenderer.idle = true;
            musicSelectCursorRenderer.loop = true;
            musicSelectCursorRenderer.CurrentFrame = 0;
            musicSelectCursorRenderer.RefreshFrame();
        }
    }

    private void UpdateMusicSelectVisuals()
    {
        if (musicSelectRoot == null || !musicSelectRoot.gameObject.activeInHierarchy)
            return;

        musicSelectRoot.anchoredPosition = musicSelectRootOffset;
        BattleModeRules.BattleMusicSelection[] selections = GetBattleMusicSelections();

        for (int i = 0; i < musicSelectOptionTexts.Count; i++)
        {
            TextMeshProUGUI text = musicSelectOptionTexts[i];
            if (text == null)
                continue;

            RectTransform textRt = text.rectTransform;
            textRt.anchorMin = new Vector2(0.5f, 0.5f);
            textRt.anchorMax = new Vector2(0.5f, 0.5f);
            textRt.pivot = new Vector2(0.5f, 0.5f);
            textRt.anchoredPosition = GetMusicSelectOptionPosition(i) + musicSelectNameColumnOffset;
            textRt.sizeDelta = musicSelectNameColumnSize;
            text.fontSize = musicSelectFontSize;

            bool selected = GameMusicController.IsBattleModeMusicSelected(workingBattleMusicSelectionMask, selections[i]);
            text.text = GameMusicController.FormatBattleModeMusicDisplayName(
                GameMusicController.GetBattleModeMusicDisplayName(selections[i]));
            text.color = i == selectedMusicIndex ? Color.green : Color.white;
            ApplySpecificSettingsTextStyle(text);

            if (i >= musicSelectCheckboxTexts.Count || musicSelectCheckboxTexts[i] == null)
                continue;

            TextMeshProUGUI checkboxText = musicSelectCheckboxTexts[i];
            RectTransform checkboxRt = checkboxText.rectTransform;
            checkboxRt.anchorMin = new Vector2(0.5f, 0.5f);
            checkboxRt.anchorMax = new Vector2(0.5f, 0.5f);
            checkboxRt.pivot = new Vector2(0.5f, 0.5f);
            checkboxRt.anchoredPosition = GetMusicSelectOptionPosition(i) + musicSelectCheckboxColumnOffset;
            checkboxRt.sizeDelta = musicSelectCheckboxColumnSize;
            checkboxText.fontSize = musicSelectFontSize;
            checkboxText.text = selected ? "[x]" : "[ ]";
            checkboxText.color = i == selectedMusicIndex ? Color.green : Color.white;
            ApplySpecificSettingsTextStyle(checkboxText);
            checkboxText.alignment = TextAlignmentOptions.Center;
        }

        if (musicSelectCursorRt != null)
        {
            int rowIndex = Mathf.Clamp(selectedMusicIndex, 0, Mathf.Max(0, musicSelectOptionTexts.Count - 1));
            musicSelectCursorRt.gameObject.SetActive(musicSelectOptionTexts.Count > 0);
            musicSelectCursorRt.sizeDelta = musicSelectCursorSize;
            musicSelectCursorRt.anchoredPosition = GetMusicSelectOptionPosition(rowIndex) + musicSelectNameColumnOffset + musicSelectCursorOffset;
            musicSelectCursorRenderer?.SetExternalBaseLocalPosition(musicSelectCursorRt.localPosition);
            UpdateMusicSelectCursorAnimationState();
        }
    }

    private void UpdateMusicSelectCursorAnimationState()
    {
        if (musicSelectCursorRenderer == null)
            return;

        bool confirming = musicSelectCursorConfirmTimer > 0f;
        if (confirming)
        {
            if (musicSelectCursorRenderer.idle)
            {
                musicSelectCursorRenderer.idle = false;
                musicSelectCursorRenderer.loop = true;
                musicSelectCursorRenderer.CurrentFrame = 0;
            }
        }
        else if (!musicSelectCursorRenderer.idle)
        {
            musicSelectCursorRenderer.idle = true;
            musicSelectCursorRenderer.loop = true;
            musicSelectCursorRenderer.CurrentFrame = 0;
        }

        musicSelectCursorRenderer.RefreshFrame();
    }

    private bool ToggleSelectedBattleMusic(out bool shouldPreviewMusic)
    {
        shouldPreviewMusic = false;
        BattleModeRules.BattleMusicSelection[] selections = GetBattleMusicSelections();
        if (selections.Length <= 0)
            return false;

        BattleModeRules.BattleMusicSelection selection = selections[Mathf.Clamp(selectedMusicIndex, 0, selections.Length - 1)];
        bool currentlySelected = GameMusicController.IsBattleModeMusicSelected(workingBattleMusicSelectionMask, selection);

        if (selection == BattleModeRules.BattleMusicSelection.Random)
        {
            if (!currentlySelected)
            {
                workingBattleMusicSelectionMask = 0;
                SaveSystem.SetBattleModeMusicSelectionMask(workingBattleMusicSelectionMask);
                return true;
            }

            return false;
        }

        if (currentlySelected && CountSelectedBattleMusic(workingBattleMusicSelectionMask) <= 1)
            return false;

        workingBattleMusicSelectionMask = GameMusicController.SetBattleModeMusicSelected(
            workingBattleMusicSelectionMask,
            selection,
            !currentlySelected);
        SaveSystem.SetBattleModeMusicSelectionMask(workingBattleMusicSelectionMask);
        shouldPreviewMusic = !currentlySelected;
        return true;
    }

    private void PreviewSelectedBattleMusic()
    {
        BattleModeRules.BattleMusicSelection[] selections = GetBattleMusicSelections();
        if (selections.Length <= 0 || GameMusicController.Instance == null)
            return;

        BattleModeRules.BattleMusicSelection selection = selections[Mathf.Clamp(selectedMusicIndex, 0, selections.Length - 1)];
        musicSelectPreviewPlaying =
            GameMusicController.Instance.PlayBattleModeMusicPreview(selection, musicSelectPreviewVolumeMultiplier) ||
            musicSelectPreviewPlaying;
    }

    private BattleModeRules.BattleMusicSelection[] GetBattleMusicSelections()
    {
        musicSelections ??= GameMusicController.GetBattleModeMusicSelections();
        return musicSelections;
    }

    private Vector2 GetMusicSelectOptionPosition(int rowIndex)
    {
        float rowHeight = Mathf.Max(musicSelectNameColumnSize.y, musicSelectCheckboxColumnSize.y);
        float totalHeight = (GetBattleMusicSelections().Length - 1) * (rowHeight + musicSelectOptionRowSpacing);
        float y = (totalHeight * 0.5f) - (rowIndex * (rowHeight + musicSelectOptionRowSpacing));
        return musicSelectOptionsOffset + new Vector2(0f, y);
    }

    private static int CountSelectedBattleMusic(int selectionMask)
    {
        int count = 0;
        BattleModeRules.BattleMusicSelection[] selections = GameMusicController.GetBattleModeMusicSelections();
        for (int i = 0; i < selections.Length; i++)
        {
            if (GameMusicController.IsBattleModeMusicSelected(selectionMask, selections[i]))
                count++;
        }

        return count;
    }

    private IEnumerator ConfirmSpecificSettingsStart()
    {
        specificStartConfirmed = true;
        battleStartBlinkTimer = 0f;
        battleStartVisible = true;
        if (specificCursorRt != null)
            specificConfirmedCursorPosition = specificCursorRt.anchoredPosition;
        LogSpecificStartTransition("ConfirmPressed.BeforeFreeze");
        StartSpecificSettingsCursorConfirmAnimation();
        LogSpecificStartTransition("ConfirmPressed.AfterFreeze");

        GameMusicController.Instance?.StopMusic();
        PlaySfx(specificStartSfx != null ? specificStartSfx : confirmSfx, specificStartSfx != null ? specificStartSfxVolume : confirmSfxVolume);
        confirmed = true;
        LogSpecificStartTransition("ConfirmPressed.AfterConfirmedFlag");

        if (specificStartWaitSeconds > 0f)
            yield return ProbeSpecificStartWait();

        LogSpecificStartTransition("BeforeFadeOut");
        yield return FadeOutRoutine(specificStartFadeSeconds);
        LogSpecificStartTransition("AfterFadeOut.BeforeHide");
        Hide();
        LogSpecificStartTransition("AfterHide.BeforeSceneLoad");
        LoadScene(GetBattleStageSceneName(SaveSystem.GetBattleModeStageIndex()));
    }

    private IEnumerator ProbeSpecificStartWait()
    {
        float wait = Mathf.Max(0f, specificStartWaitSeconds);
        if (wait <= 0f)
            yield break;

        LogSpecificStartTransition("WaitStart");
        float elapsed = 0f;
        bool loggedHalf = false;

        while (elapsed < wait)
        {
            elapsed += Time.unscaledDeltaTime;

            if (!loggedHalf && elapsed >= wait * 0.5f)
            {
                loggedHalf = true;
                LogSpecificStartTransition("WaitHalf");
            }

            yield return null;
        }

        LogSpecificStartTransition("WaitEnd");
    }

    private void EnsureSpecificSettingsBuilt()
    {
        Transform parent = GetMenuContentParent();

        if (specificSettingsRoot != null)
        {
            if (parent != null && specificSettingsRoot.parent != parent)
                specificSettingsRoot.SetParent(parent, false);

            specificSettingsRoot.gameObject.SetActive(true);
            specificSettingsRoot.SetAsLastSibling();
            if (battleStartImage == null)
                CreateBattleStartImage();
            return;
        }

        GameObject rootGo = new("SpecificSettingsRoot", typeof(RectTransform));
        rootGo.transform.SetParent(parent, false);
        specificSettingsRoot = rootGo.GetComponent<RectTransform>();
        specificSettingsRoot.anchorMin = new Vector2(0.5f, 0.5f);
        specificSettingsRoot.anchorMax = new Vector2(0.5f, 0.5f);
        specificSettingsRoot.pivot = new Vector2(0.5f, 0.5f);
        specificSettingsRoot.anchoredPosition = specificSettingsRootOffset;
        specificSettingsRoot.sizeDelta = Vector2.zero;
        specificSettingsRoot.localScale = Vector3.one * currentUiScale;
        specificSettingsRoot.SetAsLastSibling();

        GameObject stageGo = new("SpecificStageImage", typeof(RectTransform), typeof(Image), typeof(Outline));
        stageGo.transform.SetParent(specificSettingsRoot, false);
        specificStageImage = stageGo.GetComponent<Image>();
        specificStageImage.type = Image.Type.Simple;
        specificStageImage.preserveAspect = true;
        specificStageImage.raycastTarget = false;
        specificStageImage.pixelsPerUnitMultiplier = 1f;
        Outline stageOutline = stageGo.GetComponent<Outline>();
        stageOutline.effectColor = Color.black;
        stageOutline.effectDistance = new Vector2(2f, -2f);

        CreateBattleStartImage();

        specificOptionTexts.Clear();
        for (int i = 0; i < SpecificSettingsOptions.Length; i++)
            specificOptionTexts.Add(CreateSpecificSettingsOptionText(i));

        CreateSpecificSettingsCursor();
    }

    private void CreateBattleStartImage()
    {
        GameObject go = new("BattleStartImage", typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(specificSettingsRoot, false);

        battleStartImage = go.GetComponent<RawImage>();
        battleStartImage.raycastTarget = false;
        battleStartImage.texture = battleStartTexture;
        battleStartImage.enabled = false;
    }

    private TextMeshProUGUI CreateSpecificSettingsOptionText(int rowIndex)
    {
        GameObject go = new($"SpecificOption_{rowIndex}", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(specificSettingsRoot, false);

        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.fontSize = specificSettingsFontSize;
        text.color = Color.white;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        ApplySpecificSettingsTextStyle(text);
        return text;
    }

    private void CreateSpecificSettingsCursor()
    {
        AnimatedSpriteRenderer source = leftPanel != null ? leftPanel.CursorRenderer : null;
        GameObject cursorGo;

        if (source != null)
        {
            cursorGo = Instantiate(source.gameObject, specificSettingsRoot, false);
            cursorGo.name = "SpecificSettingsCursor";
            specificCursorRenderer = cursorGo.GetComponent<AnimatedSpriteRenderer>();
        }
        else
        {
            cursorGo = new GameObject("SpecificSettingsCursor", typeof(RectTransform));
        }

        specificCursorRt = cursorGo.transform as RectTransform;
        if (specificCursorRt == null)
            specificCursorRt = cursorGo.AddComponent<RectTransform>();

        specificCursorRt.anchorMin = new Vector2(0.5f, 0.5f);
        specificCursorRt.anchorMax = new Vector2(0.5f, 0.5f);
        specificCursorRt.pivot = new Vector2(0.5f, 0.5f);
        specificCursorRt.sizeDelta = specificCursorSize;
        specificCursorRt.localScale = Vector3.one;

        if (specificCursorRenderer != null)
        {
            specificCursorRenderer.SetFrozen(false);
            specificCursorRenderer.frameOffsets = null;
            specificCursorRenderer.idle = true;
            specificCursorRenderer.loop = true;
            specificCursorRenderer.CurrentFrame = 0;
            specificCursorRenderer.RefreshFrame();
        }
    }

    private void UpdateSpecificSettingsVisuals()
    {
        if (specificSettingsRoot == null || !specificSettingsRoot.gameObject.activeInHierarchy)
            return;

        if (specificStartConfirmed)
        {
            LockSpecificSettingsCursorPosition();
            UpdateBattleStartBlink();
            LayoutBattleStartImage();
            return;
        }

        specificSettingsRoot.anchoredPosition = specificSettingsRootOffset;

        int stageIndex = SaveSystem.GetBattleModeStageIndex();
        if (specificStageImage != null)
        {
            RectTransform imageRt = specificStageImage.rectTransform;
            imageRt.anchorMin = new Vector2(0.5f, 0.5f);
            imageRt.anchorMax = new Vector2(0.5f, 0.5f);
            imageRt.pivot = new Vector2(0.5f, 0.5f);
            imageRt.anchoredPosition = specificStageImageOffset;
            imageRt.sizeDelta = GetSpecificStageImageUiSize();
            specificStageImage.sprite = GetBattleStageThumbnail(stageIndex);
            specificStageImage.color = specificStageImage.sprite != null ? Color.white : specificStageImageFallbackColor;
            specificStageImage.enabled = true;
        }

        battleStartBlinkTimer = 0f;
        battleStartVisible = false;
        LayoutBattleStartImage();

        for (int i = 0; i < specificOptionTexts.Count; i++)
        {
            TextMeshProUGUI text = specificOptionTexts[i];
            if (text == null)
                continue;

            RectTransform textRt = text.rectTransform;
            textRt.anchorMin = new Vector2(0.5f, 0.5f);
            textRt.anchorMax = new Vector2(0.5f, 0.5f);
            textRt.pivot = new Vector2(0.5f, 0.5f);
            textRt.anchoredPosition = GetSpecificOptionPosition(i);
            textRt.sizeDelta = specificOptionRowSize;
            text.fontSize = specificSettingsFontSize;
            text.text = SpecificSettingsOptions[i];
            text.color = i == SpecificSettingsOptions.Length - 1 ? Color.green : Color.white;
            ApplySpecificSettingsTextStyle(text);
        }

        if (specificCursorRt != null)
        {
            int rowIndex = Mathf.Clamp(selectedSpecificSettingIndex, 0, specificOptionTexts.Count - 1);
            specificCursorRt.gameObject.SetActive(specificOptionTexts.Count > 0);
            specificCursorRt.sizeDelta = specificCursorSize;
            specificCursorRt.anchoredPosition = GetSpecificOptionPosition(rowIndex) + specificCursorOffset;
            specificCursorRenderer?.SetExternalBaseLocalPosition(specificCursorRt.localPosition);
        }
    }

    private void UpdateBattleStartBlink()
    {
        if (battleStartImage == null)
            return;

        battleStartBlinkTimer += Time.unscaledDeltaTime;
        float blinkSeconds = Mathf.Max(0.01f, battleStartBlinkSeconds);
        while (battleStartBlinkTimer >= blinkSeconds)
        {
            battleStartBlinkTimer -= blinkSeconds;
            battleStartVisible = !battleStartVisible;
        }
    }

    private void LayoutBattleStartImage()
    {
        if (battleStartImage == null)
            return;

        RectTransform rt = battleStartImage.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = battleStartOffset;
        rt.sizeDelta = GetBattleStartUiSize();
        rt.SetAsLastSibling();

        battleStartImage.texture = battleStartTexture;
        battleStartImage.color = Color.white;
        battleStartImage.enabled = specificStartConfirmed && battleStartVisible && battleStartTexture != null;
    }

    private void StartSpecificSettingsCursorConfirmAnimation()
    {
        if (specificCursorRt == null)
            return;

        specificCursorRt.anchoredPosition = specificConfirmedCursorPosition;
        specificCursorRt.localScale = Vector3.one;
        specificCursorRenderer?.SetExternalBaseLocalPosition(specificCursorRt.localPosition);

        AnimatedSpriteRenderer[] renderers = specificCursorRt.GetComponentsInChildren<AnimatedSpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].enabled = true;
            renderers[i].frameOffsets = null;
            renderers[i].SetFrozen(false);
            renderers[i].idle = false;
            renderers[i].loop = true;
            renderers[i].CurrentFrame = 0;
            renderers[i].RefreshFrame();
        }

        Animator[] animators = specificCursorRt.GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
            animators[i].enabled = false;

        Image[] images = specificCursorRt.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
            images[i].enabled = true;

        SpriteRenderer[] spriteRenderers = specificCursorRt.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < spriteRenderers.Length; i++)
            spriteRenderers[i].enabled = true;
    }

    private void LockSpecificSettingsCursorPosition()
    {
        if (specificCursorRt == null)
            return;

        specificCursorRt.anchoredPosition = specificConfirmedCursorPosition;
        specificCursorRt.localScale = Vector3.one;
        specificCursorRenderer?.SetExternalBaseLocalPosition(specificCursorRt.localPosition);
    }

    private Transform GetMenuContentParent()
    {
        if (root != null)
            return root.transform;

        if (referenceRect != null)
            return referenceRect;

        return transform;
    }

    private Vector2 GetSpecificOptionPosition(int rowIndex)
    {
        float totalHeight = (SpecificSettingsOptions.Length - 1) * (specificOptionRowSize.y + specificOptionRowSpacing);
        float y = (totalHeight * 0.5f) - (rowIndex * (specificOptionRowSize.y + specificOptionRowSpacing));
        return specificOptionsOffset + new Vector2(0f, y);
    }

    private Vector2 GetSpecificStageImageUiSize()
    {
        float authoredScale = Mathf.Max(1, designUpscale);
        return new Vector2(
            Mathf.Max(1f, specificStageImageSize.x) * authoredScale,
            Mathf.Max(1f, specificStageImageSize.y) * authoredScale);
    }

    private Vector2 GetBattleStartUiSize()
    {
        float authoredScale = Mathf.Max(1, designUpscale);
        return new Vector2(
            Mathf.Max(1f, battleStartSize.x) * authoredScale,
            Mathf.Max(1f, battleStartSize.y) * authoredScale);
    }

    private void ApplySpecificSettingsTextStyle(TextMeshProUGUI text)
    {
        if (text == null)
            return;

        if (leftPanel != null)
        {
            leftPanel.ApplyOptionTextStyleTo(text, text.color);
            text.fontSize = specificSettingsFontSize;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.UpdateMeshPadding();
            text.ForceMeshUpdate();
            text.SetVerticesDirty();
            return;
        }

        ApplyTeamLabelFont(text);
    }

    private void LogSpecificStartTransition(string context)
    {
        Debug.Log(
            "[BattleModeMenu/StartTransition] " +
            $"context={context} " +
            $"frame={Time.frameCount} " +
            $"time={Time.unscaledTime:0.###} " +
            $"state={state} " +
            $"confirmed={confirmed} " +
            $"selectedIndex={selectedSpecificSettingIndex} " +
            $"startConfirmed={specificStartConfirmed} " +
            $"cursor={FormatRectState(specificCursorRt)} " +
            $"frozenAnchored={FormatVector2(specificConfirmedCursorPosition)} " +
            $"cursorRenderer={FormatAnimatedRendererState(specificCursorRenderer)} " +
            $"specificRoot={FormatGameObjectState(specificSettingsRoot != null ? specificSettingsRoot.gameObject : null)} " +
            $"stageImage={FormatGraphicState(specificStageImage)} " +
            $"startOption={FormatSpecificOptionState(SpecificSettingsOptions.Length - 1)} " +
            $"background={FormatGraphicState(backgroundImage)} " +
            $"fade={FormatGraphicState(fadeImage)} " +
            $"fadeAlpha={(fadeImage != null ? fadeImage.color.a : -1f):0.###} " +
            $"fadeSibling={(fadeImage != null ? fadeImage.transform.GetSiblingIndex() : -1)}");
    }

    private static string FormatVector2(Vector2 v)
    {
        return $"({v.x:0.###},{v.y:0.###})";
    }

    private static string FormatVector3(Vector3 v)
    {
        return $"({v.x:0.###},{v.y:0.###},{v.z:0.###})";
    }

    private static string FormatRectState(RectTransform rt)
    {
        if (rt == null)
            return "null";

        return $"activeSelf={rt.gameObject.activeSelf},activeHierarchy={rt.gameObject.activeInHierarchy}," +
            $"anchored={FormatVector2(rt.anchoredPosition)},local={FormatVector3(rt.localPosition)}," +
            $"world={FormatVector3(rt.position)},size={FormatVector2(rt.sizeDelta)},sibling={rt.GetSiblingIndex()}";
    }

    private static string FormatGameObjectState(GameObject go)
    {
        if (go == null)
            return "null";

        return $"activeSelf={go.activeSelf},activeHierarchy={go.activeInHierarchy},sibling={go.transform.GetSiblingIndex()}";
    }

    private static string FormatGraphicState(Graphic graphic)
    {
        if (graphic == null)
            return "null";

        return $"activeSelf={graphic.gameObject.activeSelf},activeHierarchy={graphic.gameObject.activeInHierarchy}," +
            $"enabled={graphic.enabled},alpha={graphic.color.a:0.###},sibling={graphic.transform.GetSiblingIndex()}";
    }

    private string FormatSpecificOptionState(int optionIndex)
    {
        if (optionIndex < 0 || optionIndex >= specificOptionTexts.Count || specificOptionTexts[optionIndex] == null)
            return "null";

        TextMeshProUGUI text = specificOptionTexts[optionIndex];
        RectTransform rt = text.rectTransform;
        return $"text='{text.text}',colorAlpha={text.color.a:0.###}," +
            $"activeSelf={text.gameObject.activeSelf},activeHierarchy={text.gameObject.activeInHierarchy}," +
            $"anchored={FormatVector2(rt.anchoredPosition)},size={FormatVector2(rt.sizeDelta)}";
    }

    private static string FormatAnimatedRendererState(AnimatedSpriteRenderer renderer)
    {
        if (renderer == null)
            return "null";

        return $"enabled={renderer.enabled},idle={renderer.idle},loop={renderer.loop}," +
            $"frame={renderer.CurrentFrame},timer={renderer.DebugFrameTimer:0.###}," +
            $"local={FormatVector3(renderer.transform.localPosition)},world={FormatVector3(renderer.transform.position)}";
    }

    private void RefreshPlayerSelectEntries()
    {
        BuildPlayerEntries();

        if (leftPanel != null)
            leftPanel.UpdateEntryTexts(playerEntries);

        UpdateOptionVisuals();
    }

    private void BuildPlayerEntries()
    {
        playerEntries.Clear();

        for (int i = 0; i < 6; i++)
        {
            BattleModePlayerControlMode mode = i < playerModes.Length ? playerModes[i] : BattleModePlayerControlMode.Off;
            string modeText = GetPlayerModeDisplayName(mode);
            string modeColor = GetPlayerModeColor(mode);
            string columnSpaces = new string(' ', Mathf.Max(1, playerModeColumnSpaces));
            playerEntries.Add($"<color={playerLabelHex}>{i + 1}PLAYER</color>{columnSpaces}<color={modeColor}>{modeText}</color>");
        }
    }

    private void ApplyBattleModeActivePlayerIds(bool includeComPlayers)
    {
        List<int> playerIds = includeComPlayers ? battleEnabledPlayerIds : battleHumanPlayerIds;
        playerIds.Clear();

        if (playerModes != null)
        {
            for (int i = 0; i < playerModes.Length && i < GameSession.MaxPlayerId; i++)
            {
                BattleModePlayerControlMode mode = playerModes[i];
                if (mode == BattleModePlayerControlMode.Off)
                    continue;

                if (!includeComPlayers && mode != BattleModePlayerControlMode.Man)
                    continue;

                playerIds.Add(i + 1);
            }
        }

        if (playerIds.Count <= 0)
            playerIds.Add(GameSession.MinPlayerId);

        if (GameSession.Instance != null)
            GameSession.Instance.SetActivePlayerIds(playerIds);
    }

    private void ApplyPanelLayout(OptionPanelLayout layout)
    {
        if (leftPanel == null || layout == null)
            return;

        leftPanel.ConfigureLayout(
            layout.fontSize,
            layout.optionItemHeight,
            layout.optionSpacing,
            layout.optionContentOffsetX,
            layout.optionContentOffsetY,
            layout.cursorExtraOffsetY,
            layout.blockCenterX,
            layout.blockCenterY,
            layout.cursorReservedWidth,
            layout.extraBlockWidth,
            layout.extraBlockHeight,
            layout.cursorOffset,
            layout.cursorHeightMultiplier,
            layout.minCursorSize,
            layout.useRichTextEntryColors);

        leftPanel.Initialize(currentUiScale);
    }

    private void CycleSelectedPlayerMode(int direction)
    {
        if (playerModes == null || playerModes.Length == 0)
            return;

        int current = (int)playerModes[Mathf.Clamp(selectedPlayerIndex, 0, playerModes.Length - 1)];
        int next = WrapIndex(current + direction, 3);
        playerModes[selectedPlayerIndex] = (BattleModePlayerControlMode)next;
    }

    private string GetPlayerModeDisplayName(BattleModePlayerControlMode mode)
    {
        return mode switch
        {
            BattleModePlayerControlMode.Man => "MAN",
            BattleModePlayerControlMode.Com => "COM",
            BattleModePlayerControlMode.Off => "OFF",
            _ => "OFF"
        };
    }

    private string GetPlayerModeColor(BattleModePlayerControlMode mode)
    {
        return mode switch
        {
            BattleModePlayerControlMode.Man => manHex,
            BattleModePlayerControlMode.Com => comHex,
            BattleModePlayerControlMode.Off => offHex,
            _ => offHex
        };
    }

    private void Hide()
    {
        menuActive = false;
        cursorConfirmVisual = false;

        if (leftPanel != null)
            leftPanel.HideCursor();

        if (teamSelectRoot != null)
            teamSelectRoot.gameObject.SetActive(false);
        if (ruleConfigRoot != null)
            ruleConfigRoot.gameObject.SetActive(false);
        if (stageSelectRoot != null)
            stageSelectRoot.gameObject.SetActive(false);
        if (specificSettingsRoot != null)
            specificSettingsRoot.gameObject.SetActive(false);

        if (root != null)
            root.SetActive(false);
        else
            gameObject.SetActive(false);
    }

    private void LoadTitleScene()
    {
        if (GameMusicController.Instance != null)
            GameMusicController.Instance.StopMusic();

        TitleScreenBootstrap.SkipNextIntroSequence();

        if (!string.IsNullOrWhiteSpace(titleSceneName))
            LoadScene(titleSceneName);
    }

    private static void LoadScene(string sceneName)
    {
        GamePauseController.ClearPauseFlag();
        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneName);
    }

    private int GetInitialSelectedIndex()
    {
        BattleModeRules.MatchMode savedMode = SaveSystem.GetBattleModeMatchMode();

        for (int i = 0; i < MatchModes.Length; i++)
        {
            if (MatchModes[i] == savedMode)
                return i;
        }

        return 0;
    }

    private static int WrapIndex(int value, int count)
    {
        if (count <= 0)
            return 0;

        value %= count;
        if (value < 0)
            value += count;

        return value;
    }

    private static bool AnyPlayerGet(PlayerInputManager input, PlayerAction action)
    {
        if (input == null)
            return false;

        for (int playerId = 1; playerId <= 6; playerId++)
        {
            if (input.Get(playerId, action))
                return true;
        }

        return false;
    }

    private bool MenuButtonPressed(PlayerInputManager input, PlayerAction action, out int triggeringPlayerId)
    {
        triggeringPlayerId = 0;

        int actionIndex = (int)action;
        if (input == null || actionIndex < 0 || actionIndex >= previousMenuHeld.Length)
            return false;

        bool held = AnyPlayerGet(input, action, out triggeringPlayerId);
        bool pressed = held && !previousMenuHeld[actionIndex];

        if (pressed)
        {
            menuHoldConsumed[actionIndex] = true;
            return true;
        }

        if (held && !menuHoldConsumed[actionIndex])
        {
            menuHoldConsumed[actionIndex] = true;
            return true;
        }

        return false;
    }

    private bool MenuDirectionalPressed(PlayerInputManager input, PlayerAction action, out int triggeringPlayerId)
    {
        triggeringPlayerId = 0;

        int actionIndex = (int)action;
        if (input == null || actionIndex < 0 || actionIndex >= previousMenuHeld.Length)
            return false;

        bool held = AnyPlayerGet(input, action, out triggeringPlayerId);
        if (!held)
            return false;

        float now = Time.unscaledTime;
        if (!previousMenuHeld[actionIndex])
        {
            nextMenuRepeatTime[actionIndex] = now + Mathf.Max(0.01f, directionalRepeatInitialDelay);
            return true;
        }

        if (now >= nextMenuRepeatTime[actionIndex])
        {
            nextMenuRepeatTime[actionIndex] = now + Mathf.Max(0.01f, directionalRepeatInterval);
            return true;
        }

        return false;
    }

    private void CapturePreviousHeldInputs(PlayerInputManager input)
    {
        PlayerAction[] actions = GetRelevantInputActions();

        for (int i = 0; i < actions.Length; i++)
        {
            PlayerAction action = actions[i];
            int actionIndex = (int)action;
            if (actionIndex < 0 || actionIndex >= previousMenuHeld.Length)
                continue;

            bool held = AnyPlayerGet(input, action);
            previousMenuHeld[actionIndex] = held;

            if (!held)
            {
                menuHoldConsumed[actionIndex] = false;
                nextMenuRepeatTime[actionIndex] = 0f;
            }
        }
    }

    private static bool HasAnyRelevantHeldInput(PlayerInputManager input, out int playerId, out PlayerAction action)
    {
        PlayerAction[] actions = GetRelevantInputActions();
        for (int i = 0; i < actions.Length; i++)
        {
            if (AnyPlayerGet(input, actions[i], out playerId))
            {
                action = actions[i];
                return true;
            }
        }

        playerId = 0;
        action = PlayerAction.ActionA;
        return false;
    }

    private static PlayerAction[] GetRelevantInputActions()
    {
        return new[]
        {
            PlayerAction.ActionA,
            PlayerAction.ActionB,
            PlayerAction.Start,
            PlayerAction.MoveUp,
            PlayerAction.MoveDown,
            PlayerAction.MoveLeft,
            PlayerAction.MoveRight,
            PlayerAction.ActionL,
            PlayerAction.ActionR
        };
    }

    private static bool AnyPlayerGet(PlayerInputManager input, PlayerAction action, out int triggeringPlayerId)
    {
        triggeringPlayerId = 0;

        if (input == null)
            return false;

        for (int playerId = 1; playerId <= 6; playerId++)
        {
            if (input.Get(playerId, action))
            {
                triggeringPlayerId = playerId;
                return true;
            }
        }

        return false;
    }

    private void UpdateOptionVisuals()
    {
        if (leftPanel == null)
            return;

        int index = state == MenuState.PlayerSelect ? selectedPlayerIndex : selectedIndex;
        leftPanel.UpdateOptionVisuals(index, confirmed || cursorConfirmVisual);
    }

    private void UpdatePromptTitle()
    {
        if (promptTitleText == null)
            return;

        promptTitleText.text = state switch
        {
            MenuState.PlayerSelect => playerSelectPrompt,
            MenuState.SkinSelect => skinSelectPrompt,
            MenuState.TeamSelect => teamSelectPrompt,
            MenuState.RuleConfig => ruleSelectPrompt,
            MenuState.StageSelect => stageSelectPrompt,
            MenuState.SpecificSettings => specificSettingsPrompt,
            MenuState.MusicSelect => musicSelectPrompt,
            _ => matchModePrompt
        };
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

        if (selectMusic.loadState == AudioDataLoadState.Unloaded)
            selectMusic.LoadAudioData();

        music.PlayMusic(selectMusic, selectMusicVolume, loopSelectMusic);
    }

    private void SetFadeAlpha(float a)
    {
        if (fadeImage == null)
            return;

        Color c = fadeImage.color;
        c.a = a;
        fadeImage.color = c;
    }

    private void BringFadeImageToFront()
    {
        if (fadeImage == null)
            return;

        fadeImage.transform.SetAsLastSibling();
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
            SetFadeAlpha(1f - Mathf.Clamp01(t / duration));
            yield return null;
        }

        SetFadeAlpha(0f);
        fadeImage.gameObject.SetActive(false);
    }

    private IEnumerator FadeOutRoutine()
    {
        yield return FadeOutRoutine(fadeDuration);
    }

    private IEnumerator FadeOutRoutine(float durationSeconds)
    {
        if (fadeImage == null)
            yield break;

        bool probeBattleModeStart = state == MenuState.SpecificSettings && specificStartConfirmed;
        if (probeBattleModeStart)
            LogSpecificStartTransition("FadeOut.Enter");

        SetFadeAlpha(0f);
        fadeImage.gameObject.SetActive(true);
        BringFadeImageToFront();
        SetFadeAlpha(0f);
        Canvas.ForceUpdateCanvases();
        if (probeBattleModeStart)
            LogSpecificStartTransition("FadeOut.AfterActivateBeforeFirstYield");
        yield return null;

        float duration = Mathf.Max(0.001f, durationSeconds);
        float t = 0f;
        int nextProbeStep = 1;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            BringFadeImageToFront();
            float progress = Mathf.Clamp01(t / duration);
            SetFadeAlpha(progress);
            if (probeBattleModeStart && nextProbeStep <= 3 && progress >= nextProbeStep * 0.25f)
            {
                LogSpecificStartTransition($"FadeOut.Progress{nextProbeStep * 25}");
                nextProbeStep++;
            }
            yield return null;
        }

        BringFadeImageToFront();
        SetFadeAlpha(1f);
        if (probeBattleModeStart)
            LogSpecificStartTransition("FadeOut.Complete");
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

        Sprite[] currentSprites = GetCurrentBackgroundSprites();
        int validCount = GetValidSpriteCount(currentSprites);
        if (validCount <= 0)
            return;

        if (validCount == 1)
        {
            Sprite sprite = GetSpriteFromSet(currentSprites, 0);
            if (backgroundImage.sprite != sprite)
                backgroundImage.sprite = sprite;
            return;
        }

        backgroundSwapTimer += Time.unscaledDeltaTime;

        float interval = Mathf.Max(0.01f, backgroundSwapInterval);
        while (backgroundSwapTimer >= interval)
        {
            backgroundSwapTimer -= interval;
            backgroundSpriteIndex++;

            if (backgroundSpriteIndex >= validCount)
                backgroundSpriteIndex = backgroundSwapLoop ? 0 : validCount - 1;
        }

        ApplyCurrentBackgroundSprite(false);
    }

    private void ApplyCurrentBackgroundSprite(bool force)
    {
        if (backgroundImage == null)
            return;

        Sprite[] currentSprites = GetCurrentBackgroundSprites();
        int validCount = GetValidSpriteCount(currentSprites);
        if (validCount <= 0)
            return;

        int clampedIndex = validCount == 1 ? 0 : Mathf.Clamp(backgroundSpriteIndex, 0, validCount - 1);
        Sprite sprite = GetSpriteFromSet(currentSprites, clampedIndex);
        if (sprite == null)
            return;

        if (force || backgroundImage.sprite != sprite)
            backgroundImage.sprite = sprite;
    }

    private Sprite[] GetCurrentBackgroundSprites()
    {
        return state switch
        {
            MenuState.PlayerSelect => playerSelectBackgrounds?.sprites,
            MenuState.SkinSelect => skinSelectBackgrounds?.sprites,
            MenuState.TeamSelect => teamSelectBackgrounds?.sprites,
            MenuState.RuleConfig => ruleConfigBackgrounds?.sprites,
            MenuState.StageSelect => stageSelectBackgrounds?.sprites,
            MenuState.SpecificSettings => specificSettingsBackgrounds?.sprites,
            MenuState.MusicSelect => musicSelectBackgrounds?.sprites,
            _ => matchModeBackgrounds?.sprites
        };
    }

    private static int GetValidSpriteCount(Sprite[] sprites)
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

    private static Sprite GetSpriteFromSet(Sprite[] sprites, int index)
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

        return validSprites[Mathf.Clamp(index, 0, validSprites.Count - 1)];
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

    private Rect GetReferencePixelRect()
    {
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
            return new Rect(0, 0, Screen.width, Screen.height);

        return new Rect(xMin, yMin, width, height);
    }

    private float ComputeUiScaleForRect(float usedW, float usedH)
    {
        if (!dynamicScale)
            return 1f;

        float sx = usedW / Mathf.Max(1f, referenceWidth);
        float sy = usedH / Mathf.Max(1f, referenceHeight);

        float baseScaleRaw = Mathf.Min(sx, sy);
        float baseScaleForUi = useIntegerUpscale ? Mathf.Floor(baseScaleRaw) : baseScaleRaw;
        if (baseScaleForUi < 1f)
            baseScaleForUi = 1f;

        int baseScaleInt = Mathf.Max(1, Mathf.RoundToInt(baseScaleForUi));
        float normalized = baseScaleInt / Mathf.Max(1f, designUpscale);
        float ui = normalized * Mathf.Max(0.01f, extraScaleMultiplier);
        return Mathf.Clamp(ui, minScale, maxScale);
    }

    private void ApplyDynamicScaleIfNeeded(bool force)
    {
        int sw = Screen.width;
        int sh = Screen.height;

        Camera cam = GetMainCameraSafe();
        Rect camRect = cam != null ? cam.rect : new Rect(0, 0, 1, 1);
        Rect refPx = GetReferencePixelRect();

        bool changed =
            force ||
            sw != lastScreenW ||
            sh != lastScreenH ||
            camRect != lastCameraRect ||
            !ApproximatelyRect(refPx, lastRefPixelRect);

        if (!changed)
            return;

        lastScreenW = sw;
        lastScreenH = sh;
        lastCameraRect = camRect;
        lastRefPixelRect = refPx;

        currentUiScale = ComputeUiScaleForRect(refPx.width, refPx.height);

        if (leftPanel != null)
            leftPanel.SetUiScale(currentUiScale);

        if (teamSelectRoot != null)
            teamSelectRoot.localScale = Vector3.one * currentUiScale;
        if (ruleConfigRoot != null)
            ruleConfigRoot.localScale = Vector3.one * currentUiScale;
        if (stageSelectRoot != null)
            stageSelectRoot.localScale = Vector3.one * currentUiScale;
        if (specificSettingsRoot != null)
            specificSettingsRoot.localScale = Vector3.one * currentUiScale;
        if (musicSelectRoot != null)
            musicSelectRoot.localScale = Vector3.one * currentUiScale;
    }

    private static bool ApproximatelyRect(Rect a, Rect b)
    {
        return
            Mathf.Abs(a.x - b.x) < 0.01f &&
            Mathf.Abs(a.y - b.y) < 0.01f &&
            Mathf.Abs(a.width - b.width) < 0.01f &&
            Mathf.Abs(a.height - b.height) < 0.01f;
    }
}

using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class BattleModeMenu : MonoBehaviour
{
    public static bool OpenDirectlyAtStageSelect { get; set; }

    private enum MenuState
    {
        MatchMode = 0,
        PlayerSelect = 1,
        SkinSelect = 2,
        TeamSelect = 3,
        RuleConfig = 4,
        StageSelect = 5,
        SpecificSettings = 6,
        MusicSelect = 7,
        ItemSelect = 8,
        LouieSelect = 9,
        HandicapSelect = 10
    }

    private enum ItemSelectEntryId
    {
        ExtraBomb,
        BlastRadius,
        SpeedIncrese,
        BombKick,
        BombPunch,
        PierceBomb,
        ControlBomb,
        PowerBomb,
        RubberBomb,
        MagnetBomb,
        FullFire,
        BombPass,
        DestructiblePass,
        InvincibleSuit,
        Heart,
        PowerGlove,
        RandomEggsMin,
        RandomEggsMax,
        Skull
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

    private sealed class HandicapRowVisual
    {
        public RectTransform root;
        public Image playerImage;
        public Image mountImage;
        public Outline mountOutline;
        public AnimatedSpriteRenderer mountRenderer;
        public MountedType mountedType = MountedType.None;
        public readonly List<Image> optionImages = new();
        public readonly List<TextMeshProUGUI> optionTexts = new();
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
    [SerializeField, Min(0f)] private float optionCursorAdvanceSeconds = 0.25f;

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
    [SerializeField] private string itemSelectPrompt = "SELECT ITEMS";
    [SerializeField] private string handicapSelectPrompt = "SELECT HANDICAP";

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
    [SerializeField] private BackgroundSet itemSelectBackgrounds = new();
    [SerializeField] private BackgroundSet louieSelectBackgrounds = new();
    [SerializeField] private BackgroundSet handicapSelectBackgrounds = new();
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
    [SerializeField] private Vector2 stageLockedHintOffset = new(0f, -324f);
    [SerializeField] private Vector2 stageLockedHintSize = new(900f, 42f);
    [SerializeField] private string stageLockedHintMessage = "WIN STAGE 10 IN BATTLE MODE";
    [SerializeField] private string stageLockedHintHex = "#FF3B30";
    [SerializeField, Min(0f)] private float stageLockedHintShowSeconds = 5f;
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
    [SerializeField] private Vector2 itemSelectRootOffset = Vector2.zero;
    [SerializeField] private List<ItemSelectEntryId> itemSelectEntryOrder = new()
    {
        ItemSelectEntryId.ExtraBomb,
        ItemSelectEntryId.BlastRadius,
        ItemSelectEntryId.SpeedIncrese,
        ItemSelectEntryId.BombKick,
        ItemSelectEntryId.BombPunch,
        ItemSelectEntryId.PierceBomb,
        ItemSelectEntryId.ControlBomb,
        ItemSelectEntryId.PowerBomb,
        ItemSelectEntryId.RubberBomb,
        ItemSelectEntryId.MagnetBomb,
        ItemSelectEntryId.FullFire,
        ItemSelectEntryId.BombPass,
        ItemSelectEntryId.DestructiblePass,
        ItemSelectEntryId.InvincibleSuit,
        ItemSelectEntryId.Heart,
        ItemSelectEntryId.PowerGlove,
        ItemSelectEntryId.RandomEggsMin,
        ItemSelectEntryId.RandomEggsMax,
        ItemSelectEntryId.Skull
    };
    [SerializeField] private Vector2 itemSelectGridOffset = new(0f, 8f);
    [SerializeField, Min(1)] private int itemSelectColumns = 5;
    [SerializeField] private Vector2 itemSelectCellSize = new(150f, 52f);
    [SerializeField] private Vector2 itemSelectIconOffset = new(-42f, 0f);
    [SerializeField] private Vector2 itemSelectIconSize = new(40f, 40f);
    [SerializeField, Min(0.01f)] private float itemSelectRandomEggIconScale = 1.35f;
    [SerializeField] private Vector2 itemSelectAmountOffset = new(36f, 0f);
    [SerializeField] private Vector2 itemSelectAmountSize = new(64f, 36f);
    [SerializeField] private int itemSelectAmountFontSize = 28;
    [SerializeField] private Vector2 itemSelectCursorOffset = Vector2.zero;
    [SerializeField] private Vector2 itemSelectCursorSize = new(54f, 54f);
    [SerializeField] private Sprite[] itemSelectCursorSprites = new Sprite[3];
    [SerializeField, Min(0.01f)] private float itemSelectCursorFrameSeconds = 0.08f;
    [SerializeField] private Vector2 itemSelectHintOffset = new(0f, -300f);
    [SerializeField] private Vector2 itemSelectHintSize = new(900f, 76f);
    [SerializeField] private int itemSelectHintFontSize = 18;
    [SerializeField, Min(0f)] private float itemSelectHintLineSpacing = 18f;
    [SerializeField, Min(0)] private int itemSelectMaxAmount = 99;
    [SerializeField] private Vector2 louieSelectRootOffset = Vector2.zero;
    [SerializeField] private Vector2 louieSelectGridOffset = new(0f, 8f);
    [SerializeField, Min(1)] private int louieSelectColumns = 5;
    [SerializeField] private Vector2 louieSelectCellSize = new(150f, 100f);
    [SerializeField] private Vector2 louieSelectIconOffset = new(-42f, 0f);
    [SerializeField] private Vector2 louieSelectIconSize = new(64f, 64f);
    [SerializeField, Min(0.01f)] private float louieSelectMountSizeMultiplier = 1f;
    [SerializeField, Min(0.01f)] private float louieSelectPinkLouieSizeMultiplier = 1f;
    [SerializeField, Min(0.01f)] private float louieSelectAnimationSpeedMultiplier = 1f;
    [SerializeField] private Vector2 louieSelectAmountOffset = new(28f, 0f);
    [SerializeField] private Vector2 louieSelectAmountSize = new(64f, 36f);
    [SerializeField] private int louieSelectAmountFontSize = 32;
    [SerializeField] private Vector2 louieSelectCursorOffset = Vector2.zero;
    [SerializeField] private Vector2 louieSelectCursorSize = new(62f, 62f);
    [SerializeField] private Vector2 louieSelectHintOffset = new(0f, -300f);
    [SerializeField] private Vector2 louieSelectHintSize = new(900f, 76f);
    [SerializeField] private int louieSelectHintFontSize = 32;
    [SerializeField, Min(0f)] private float louieSelectHintLineSpacing = 18f;
    [SerializeField, Min(0)] private int louieSelectMaxAmount = 99;
    [SerializeField] private GameObject[] louieSelectMountPrefabs = new GameObject[9];
    [SerializeField] private Color louieSelectDisabledTint = new(0.08f, 0.08f, 0.08f, 0.85f);
    [SerializeField, Range(0f, 1f)] private float handicapSelectEmptyMountAlpha = 0.45f;
    [SerializeField, Min(0.01f)] private float handicapSelectMountSizeMultiplier = 1f;
    [SerializeField] private Vector2 handicapSelectRootOffset = Vector2.zero;
    [SerializeField] private Vector2 handicapSelectRowsOffset = new(0f, 2f);
    [SerializeField] private Vector2 handicapSelectRowSize = new(900f, 96f);
    [SerializeField, Min(0f)] private float handicapSelectRowSpacing = 4f;
    [SerializeField] private Vector2 handicapSelectPlayerOffset = new(-410f, 0f);
    [SerializeField] private Vector2 handicapSelectPlayerSize = new(96f, 96f);
    [SerializeField, Min(0.01f)] private float handicapSelectPlayerFrameSeconds = 0.22f;
    [SerializeField] private Vector2 handicapSelectMountOffset = new(-304f, 0f);
    [SerializeField] private Vector2 handicapSelectOptionStartOffset = new(-190f, 0f);
    [SerializeField] private Vector2 handicapSelectOptionSpacing = new(80f, 0f);
    [SerializeField] private Vector2 handicapSelectOptionIconSize = new(40f, 40f);
    [SerializeField] private Vector2 handicapSelectOptionNumberOffset = new(0f, 9f);
    [SerializeField] private Vector2 handicapSelectOptionTextSize = new(60f, 30f);
    [SerializeField] private int handicapSelectOptionFontSize = 22;
    [SerializeField] private Vector2 handicapSelectCursorOffset = new(-46f, 0f);
    [SerializeField] private Vector2 handicapSelectCursorSize = new(62f, 62f);
    [SerializeField] private Vector2 handicapSelectHintOffset = new(0f, -300f);
    [SerializeField] private Vector2 handicapSelectHintSize = new(900f, 76f);
    [SerializeField] private int handicapSelectHintFontSize = 22;
    [SerializeField, Min(0f)] private float handicapSelectHintLineSpacing = 18f;

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
    private bool optionCursorAdvanceAnimating;
    private bool directCharacterReturnedToPlayerSelect;

    private int backgroundSpriteIndex;
    private float backgroundSwapTimer;

    private Coroutine fadeInCoroutine;
    private Coroutine revealLeftPanelCursorCoroutine;

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
    private TextMeshProUGUI stageLockedHintText;
    private int selectedStageIndex;
    private int stageCarouselFromIndex;
    private int stageCarouselDirection;
    private float stageCarouselElapsed;
    private bool stageCarouselAnimating;
    private bool stageSelectionReturnedToRuleConfig;
    private float stageLockedHintHideRealtime = -1f;
    private int stageLockedHintStageIndex = -1;
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
    private RectTransform itemSelectRoot;
    private readonly List<RectTransform> itemSelectCells = new();
    private readonly List<Image> itemSelectIconImages = new();
    private readonly List<TextMeshProUGUI> itemSelectIconLabelTexts = new();
    private readonly List<TextMeshProUGUI> itemSelectAmountTexts = new();
    private TextMeshProUGUI itemSelectHintText;
    private RectTransform itemSelectCursorRt;
    private Image itemSelectCursorImage;
    private AnimatedSpriteRenderer itemSelectCursorRenderer;
    private Sprite[] itemSelectIconSprites;
    private Sprite itemSelectRandomEggSprite;
    private readonly List<ItemSelectEntryId> resolvedItemSelectEntryOrder = new();
    private int[] workingBattleItemAmounts;
    private int selectedItemIndex;
    private float itemSelectCursorConfirmTimer;
    private RectTransform louieSelectRoot;
    private readonly List<RectTransform> louieSelectCells = new();
    private readonly List<Image> louieSelectImages = new();
    private readonly List<AnimatedSpriteRenderer> louieSelectRenderers = new();
    private readonly List<TextMeshProUGUI> louieSelectAmountTexts = new();
    private TextMeshProUGUI louieSelectHintText;
    private RectTransform louieSelectCursorRt;
    private AnimatedSpriteRenderer louieSelectCursorRenderer;
    private int[] workingBattleLouieAmounts;
    private int selectedLouieIndex;
    private float louieSelectCursorConfirmTimer;
    private RectTransform handicapSelectRoot;
    private readonly List<HandicapRowVisual> handicapRows = new();
    private TextMeshProUGUI handicapSelectHintText;
    private RectTransform handicapSelectCursorRt;
    private AnimatedSpriteRenderer handicapSelectCursorRenderer;
    private SaveData.BattleModeHandicapSave workingBattleHandicap;
    private int selectedHandicapRow;
    private int selectedHandicapColumn;
    private float handicapSelectCursorConfirmTimer;
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

    private static readonly ItemSelectEntryId[] DefaultItemSelectEntryOrder =
    {
        ItemSelectEntryId.ExtraBomb,
        ItemSelectEntryId.BlastRadius,
        ItemSelectEntryId.SpeedIncrese,
        ItemSelectEntryId.BombKick,
        ItemSelectEntryId.BombPunch,
        ItemSelectEntryId.PierceBomb,
        ItemSelectEntryId.ControlBomb,
        ItemSelectEntryId.PowerBomb,
        ItemSelectEntryId.RubberBomb,
        ItemSelectEntryId.MagnetBomb,
        ItemSelectEntryId.FullFire,
        ItemSelectEntryId.BombPass,
        ItemSelectEntryId.DestructiblePass,
        ItemSelectEntryId.InvincibleSuit,
        ItemSelectEntryId.Heart,
        ItemSelectEntryId.PowerGlove,
        ItemSelectEntryId.RandomEggsMin,
        ItemSelectEntryId.RandomEggsMax,
        ItemSelectEntryId.Skull
    };

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
        if (OpenDirectlyAtStageSelect)
        {
            OpenDirectlyAtStageSelect = false;
            StartCoroutine(OpenDirectlyAtStageSelectRoutine());
            return;
        }

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
        else if (state == MenuState.ItemSelect)
            UpdateItemSelectVisuals();
        else if (state == MenuState.LouieSelect)
            UpdateLouieSelectVisuals();
        else if (state == MenuState.HandicapSelect)
            UpdateHandicapSelectVisuals();

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

    private IEnumerator OpenDirectlyAtStageSelectRoutine()
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
        state = MenuState.StageSelect;

        if (leftPanel != null)
        {
            leftPanel.HideCursor();
            leftPanel.gameObject.SetActive(false);
        }

        ResetBackgroundSpriteSwap();
        ApplyCurrentBackgroundSprite(true);
        UpdatePromptTitle();

        yield return null;
        Canvas.ForceUpdateCanvases();
        yield return new WaitForEndOfFrame();
        Canvas.ForceUpdateCanvases();

        ApplyDynamicScaleIfNeeded(true);
        StartSelectMusic();

        if (fadeInCoroutine != null)
            StopCoroutine(fadeInCoroutine);

        fadeInCoroutine = StartCoroutine(FadeInRoutine());

        MenuState flowState = MenuState.StageSelect;
        bool resumeTeamSelectionAtLastMember = false;

        while (!confirmed)
        {
            if (flowState == MenuState.StageSelect)
            {
                yield return OpenStageSelectMenu();

                if (stageSelectionReturnedToRuleConfig)
                {
                    stageSelectionReturnedToRuleConfig = false;
                    flowState = MenuState.RuleConfig;
                    continue;
                }

                flowState = MenuState.SpecificSettings;
                continue;
            }

            if (flowState == MenuState.SpecificSettings)
            {
                yield return OpenSpecificSettingsMenu();

                if (specificSettingsReturnedToStageSelect)
                {
                    specificSettingsReturnedToStageSelect = false;
                    flowState = MenuState.StageSelect;
                    continue;
                }

                yield break;
            }

            if (flowState == MenuState.RuleConfig)
            {
                yield return OpenRuleConfigMenu();

                if (ruleConfigReturnedToTeamSelect)
                {
                    ruleConfigReturnedToTeamSelect = false;
                    resumeTeamSelectionAtLastMember = true;
                    flowState = MenuState.TeamSelect;
                    continue;
                }

                if (ruleConfigReturnedToSkinSelect)
                {
                    ruleConfigReturnedToSkinSelect = false;
                    flowState = MenuState.SkinSelect;
                    continue;
                }

                flowState = MenuState.StageSelect;
                continue;
            }

            if (flowState == MenuState.TeamSelect)
            {
                yield return OpenTeamSelectMenu(resumeTeamSelectionAtLastMember);
                resumeTeamSelectionAtLastMember = false;

                if (teamSelectionReturnedToSkinSelect)
                {
                    teamSelectionReturnedToSkinSelect = false;
                    flowState = MenuState.SkinSelect;
                    continue;
                }

                flowState = MenuState.RuleConfig;
                continue;
            }

            if (flowState == MenuState.SkinSelect)
            {
                yield return OpenCharacterSelectOnlyForDirectFlow();

                if (directCharacterReturnedToPlayerSelect)
                {
                    directCharacterReturnedToPlayerSelect = false;

                    ShowPlayerSelectAfterSkinOrTeamReturn();
                    yield return ContinueDirectFlowFromPlayerSelectRoutine();

                    yield break;
                }

                flowState = SelectedMatchMode == BattleModeRules.MatchMode.TagMatch
                    ? MenuState.TeamSelect
                    : MenuState.RuleConfig;

                continue;
            }

            yield break;
        }
    }

    private IEnumerator OpenCharacterSelectOnlyForDirectFlow()
    {
        directCharacterReturnedToPlayerSelect = false;

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
        if (specificSettingsRoot != null)
            specificSettingsRoot.gameObject.SetActive(false);
        if (musicSelectRoot != null)
            musicSelectRoot.gameObject.SetActive(false);
        if (itemSelectRoot != null)
            itemSelectRoot.gameObject.SetActive(false);
        if (louieSelectRoot != null)
            louieSelectRoot.gameObject.SetActive(false);
        if (handicapSelectRoot != null)
            handicapSelectRoot.gameObject.SetActive(false);

        if (leftPanel != null)
        {
            leftPanel.HideCursor();
            leftPanel.gameObject.SetActive(false);
        }

        UpdatePromptTitle();
        ApplyCurrentBackgroundSprite(true);

        if (skinSelectMenu == null)
        {
            directCharacterReturnedToPlayerSelect = true;
            yield break;
        }

        float wait = Mathf.Min(0.25f, Mathf.Max(0f, confirmFeedbackSeconds));
        if (wait > 0f)
            yield return new WaitForSecondsRealtime(wait);

        skinSelectMenu.ResumeBattleModeSelectionFromSavedSkins = true;
        yield return skinSelectMenu.SelectSkinRoutine();

        ApplyBattleModeActivePlayerIds(includeComPlayers: true);
        RestoreRootAfterEmbeddedSkinSelect();

        if (skinSelectMenu.ReturnToTitleRequested)
        {
            directCharacterReturnedToPlayerSelect = true;
            yield break;
        }

        directCharacterReturnedToPlayerSelect = false;
    }

    private IEnumerator ContinueDirectFlowFromPlayerSelectRoutine()
    {
        PlayerInputManager input = PlayerInputManager.Instance;

        while (input != null && HasAnyRelevantHeldInput(input, out _, out _))
            yield return null;

        yield return null;

        CapturePreviousHeldInputs(input);
        menuActive = true;

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

        yield return PlayLeftPanelCursorAdvanceAnimation();
        yield return BuildPlayerSelectMenuAfterTransition("ConfirmMatchMode");
    }

    private IEnumerator ConfirmPlayerSelection()
    {
        PlaySfx(confirmSfx, confirmSfxVolume);
        SaveSystem.SetBattleModePlayerControlModes(playerModes);

        yield return PlayLeftPanelCursorAdvanceAnimation();

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

    private IEnumerator PlayLeftPanelCursorAdvanceAnimation()
    {
        cursorConfirmVisual = true;
        UpdateOptionVisuals();

        AnimatedSpriteRenderer cursor = leftPanel != null ? leftPanel.CursorRenderer : null;
        yield return PlayOptionCursorAdvanceAnimation(cursor);

        cursorConfirmVisual = false;
        UpdateOptionVisuals();
    }

    private IEnumerator PlayOptionCursorAdvanceAnimation(AnimatedSpriteRenderer cursor)
    {
        float duration = Mathf.Max(0f, optionCursorAdvanceSeconds);
        if (duration <= 0f)
            yield break;

        if (cursor == null || cursor.animationSprite == null || cursor.animationSprite.Length <= 0 || !cursor.gameObject.activeInHierarchy)
        {
            yield return new WaitForSecondsRealtime(duration);
            yield break;
        }

        bool previousIdle = cursor.idle;
        bool previousLoop = cursor.loop;
        bool previousUseSequenceDuration = cursor.useSequenceDuration;
        float previousSequenceDuration = cursor.sequenceDuration;
        float previousAnimationTime = cursor.animationTime;

        optionCursorAdvanceAnimating = true;
        cursor.SetFrozen(false);
        cursor.SetManualAnimationUpdate(true);
        cursor.idle = false;
        cursor.loop = false;
        cursor.useSequenceDuration = true;
        cursor.sequenceDuration = duration;
        cursor.RestartAnimation();

        int frameCount = cursor.pingPong && cursor.animationSprite.Length > 1
            ? (cursor.animationSprite.Length * 2) - 2
            : cursor.animationSprite.Length;
        float frameSeconds = duration / Mathf.Max(1, frameCount);

        for (int frameStep = 0; frameStep < frameCount; frameStep++)
        {
            int frameIndex = GetAdvanceAnimationFrameIndex(cursor, frameStep);
            cursor.CurrentFrame = frameIndex;
            cursor.RefreshFrame();
            yield return new WaitForSecondsRealtime(frameSeconds);
        }

        cursor.idle = previousIdle;
        cursor.loop = previousLoop;
        cursor.useSequenceDuration = previousUseSequenceDuration;
        cursor.sequenceDuration = previousSequenceDuration;
        cursor.animationTime = previousAnimationTime;
        cursor.CurrentFrame = 0;
        cursor.RefreshFrame();
        cursor.SetManualAnimationUpdate(false);
        optionCursorAdvanceAnimating = false;
    }

    private void RestartOptionCursorAdvanceFeedback(AnimatedSpriteRenderer cursor, ref float timer)
    {
        timer = Mathf.Max(0f, optionCursorAdvanceSeconds);
        if (timer <= 0f || cursor == null || cursor.animationSprite == null || cursor.animationSprite.Length <= 0 || !cursor.gameObject.activeInHierarchy)
            return;

        cursor.SetFrozen(false);
        cursor.SetManualAnimationUpdate(false);
        cursor.idle = false;
        cursor.loop = false;
        cursor.useSequenceDuration = true;
        cursor.sequenceDuration = timer;
        cursor.RestartAnimation();
    }

    private static int GetAdvanceAnimationFrameIndex(AnimatedSpriteRenderer cursor, int frameStep)
    {
        int frameCount = cursor.animationSprite != null ? cursor.animationSprite.Length : 0;
        if (frameCount <= 1 || !cursor.pingPong)
            return Mathf.Clamp(frameStep, 0, Mathf.Max(0, frameCount - 1));

        int lastFrame = frameCount - 1;
        return frameStep <= lastFrame
            ? frameStep
            : Mathf.Max(0, lastFrame - (frameStep - lastFrame));
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
            if (specificSettingsRoot != null)
                specificSettingsRoot.gameObject.SetActive(false);
            if (musicSelectRoot != null)
                musicSelectRoot.gameObject.SetActive(false);
            if (itemSelectRoot != null)
                itemSelectRoot.gameObject.SetActive(false);
            if (louieSelectRoot != null)
                louieSelectRoot.gameObject.SetActive(false);
            if (handicapSelectRoot != null)
                handicapSelectRoot.gameObject.SetActive(false);

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
            bool returnToRuleConfigFromStageSelect = false;

            while (!confirmed)
            {
                while (!rulesConfirmed)
                {
                    if (SelectedMatchMode == BattleModeRules.MatchMode.TagMatch && !returnToRuleConfigFromStageSelect)
                    {
                        yield return OpenTeamSelectMenu(ruleConfigReturnedToTeamSelect);

                        if (teamSelectionReturnedToSkinSelect)
                        {
                            teamSelectionReturnedToSkinSelect = false;
                            reopenSkinSelect = true;
                            break;
                        }
                    }

                    returnToRuleConfigFromStageSelect = false;
                    yield return OpenRuleConfigMenu();

                    if (ruleConfigReturnedToSkinSelect)
                    {
                        ruleConfigReturnedToSkinSelect = false;
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
                    stageSelectionReturnedToRuleConfig = false;
                    rulesConfirmed = false;
                    returnToRuleConfigFromStageSelect = true;
                    continue;
                }

                yield return OpenSpecificSettingsMenu();

                if (specificSettingsReturnedToStageSelect)
                {
                    specificSettingsReturnedToStageSelect = false;
                    continue;
                }

                break;
            }

            if (reopenSkinSelect)
            {
                if (skinSelectMenu != null)
                    skinSelectMenu.ResumeBattleModeSelectionFromSavedSkins = true;

                continue;
            }

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

                if (IsLastTeamSelectionPlayer())
                {
                    yield return PlayOptionCursorAdvanceAnimation(teamCursorRenderer);
                    SaveSystem.SetBattleModePlayerTeams(workingTeams);
                    done = true;
                }
                else
                {
                    currentTeamPlayerIndex++;
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
        if (itemSelectRoot != null)
            itemSelectRoot.gameObject.SetActive(false);
        if (louieSelectRoot != null)
            louieSelectRoot.gameObject.SetActive(false);
        if (louieSelectRoot != null)
            louieSelectRoot.gameObject.SetActive(false);
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
        if (itemSelectRoot != null)
            itemSelectRoot.gameObject.SetActive(false);
        if (louieSelectRoot != null)
            louieSelectRoot.gameObject.SetActive(false);

        if (leftPanel != null)
        {
            leftPanel.SetCursorVisibilitySuppressed(true);
            leftPanel.gameObject.SetActive(true);
        }

        BuildPlayerSelectMenu();

        if (leftPanel != null)
        {
            UpdateOptionVisuals();
        }

        Canvas.ForceUpdateCanvases();
        ApplyDynamicScaleIfNeeded(true);

        if (leftPanel != null)
        {
            UpdateOptionVisuals();
        }

        if (revealLeftPanelCursorCoroutine != null)
            StopCoroutine(revealLeftPanelCursorCoroutine);

        revealLeftPanelCursorCoroutine = StartCoroutine(RevealLeftPanelCursorAfterLayoutRoutine());
    }

    private IEnumerator RevealLeftPanelCursorAfterLayoutRoutine()
    {
        yield return new WaitForEndOfFrame();

        Canvas.ForceUpdateCanvases();
        ApplyDynamicScaleIfNeeded(true);
        UpdateOptionVisuals();

        if (leftPanel != null)
        {
            leftPanel.SetCursorVisibilitySuppressed(false);
            UpdateOptionVisuals();
        }

        revealLeftPanelCursorCoroutine = null;
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
            teamCursorRenderer.SetFrozen(false);
            teamCursorRenderer.frameOffsets = null;
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
            teamCursorRenderer?.SetExternalBaseLocalPosition(teamCursorRt.localPosition);
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
        bool assigned = teamAssigned != null &&
            playerId >= 1 &&
            playerId < teamAssigned.Length &&
            teamAssigned[playerId];
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
                yield return PlayOptionCursorAdvanceAnimation(ruleCursorRenderer);
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
            ruleCursorRenderer.SetFrozen(false);
            ruleCursorRenderer.frameOffsets = null;
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
            ruleCursorRenderer?.SetExternalBaseLocalPosition(ruleCursorRt.localPosition);
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
        HideStageLockedHint();
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
                HideStageLockedHint();
                moved = true;
            }
            else if (!stageCarouselAnimating &&
                     (input.GetDown(GameSession.MinPlayerId, PlayerAction.MoveRight) ||
                     input.GetDown(GameSession.MinPlayerId, PlayerAction.MoveDown) ||
                     input.GetDown(GameSession.MinPlayerId, PlayerAction.ActionR)))
            {
                BeginStageCarouselMove(1);
                HideStageLockedHint();
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
                int stageIndex = selectedStageIndex + 1;
                if (!SaveSystem.IsBattleModeStageUnlocked(stageIndex))
                {
                    PlaySfx(deniedSfx, deniedSfxVolume);
                    ShowStageLockedHint(stageIndex);
                    yield return null;
                    continue;
                }

                PlaySfx(confirmSfx, confirmSfxVolume);
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
            if (stageLockedHintText == null)
            {
                stageLockedHintText = CreateStageText(stageSelectRoot, "StageLockedHint", TextAlignmentOptions.Center);
                stageLockedHintText.gameObject.SetActive(false);
            }
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
        stageLockedHintText = CreateStageText(stageSelectRoot, "StageLockedHint", TextAlignmentOptions.Center);
        stageLockedHintText.gameObject.SetActive(false);
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

        UpdateStageLockedHintVisual(stageIndex);
    }

    private void ShowStageLockedHint(int stageIndex)
    {
        stageLockedHintStageIndex = stageIndex;
        stageLockedHintHideRealtime = Time.unscaledTime + Mathf.Max(0f, stageLockedHintShowSeconds);
        UpdateStageLockedHintVisual(stageIndex);
    }

    private void HideStageLockedHint()
    {
        stageLockedHintHideRealtime = -1f;
        stageLockedHintStageIndex = -1;

        if (stageLockedHintText != null)
            stageLockedHintText.gameObject.SetActive(false);
    }

    private void UpdateStageLockedHintVisual(int focusedStageIndex)
    {
        if (stageLockedHintText == null)
            return;

        bool shouldShow = stageLockedHintStageIndex == focusedStageIndex &&
                          stageLockedHintHideRealtime >= 0f &&
                          Time.unscaledTime < stageLockedHintHideRealtime &&
                          !SaveSystem.IsBattleModeStageUnlocked(focusedStageIndex);

        if (!shouldShow)
        {
            stageLockedHintText.gameObject.SetActive(false);
            return;
        }

        stageLockedHintText.gameObject.SetActive(true);
        stageLockedHintText.text = $"<color={stageLockedHintHex}>{stageLockedHintMessage}</color>";
        stageLockedHintText.fontSize = stageSelectFontSize;
        stageLockedHintText.rectTransform.anchoredPosition = stageLockedHintOffset;
        stageLockedHintText.rectTransform.sizeDelta = stageLockedHintSize;
        ApplyStageTextStyle(stageLockedHintText);
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
        bool unlocked = SaveSystem.IsBattleModeStageUnlocked(stageIndex);
        Color color = sprite != null ? Color.white : stageThumbnailFallbackColor;
        if (!unlocked)
            color = new Color(0.16f, 0.16f, 0.16f, color.a);
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
                if (selectedSpecificSettingIndex == SpecificSettingsOptions.Length - 1)
                {
                    yield return PlayOptionCursorAdvanceAnimation(specificCursorRenderer);
                    yield return ConfirmSpecificSettingsStart();
                    yield break;
                }

                if (string.Equals(SpecificSettingsOptions[selectedSpecificSettingIndex], "Items", System.StringComparison.Ordinal))
                {
                    PlaySfx(confirmSfx, confirmSfxVolume);
                    yield return PlayOptionCursorAdvanceAnimation(specificCursorRenderer);
                    yield return OpenItemSelectMenu();
                    continue;
                }

                if (string.Equals(SpecificSettingsOptions[selectedSpecificSettingIndex], "Handicap", System.StringComparison.Ordinal))
                {
                    PlaySfx(confirmSfx, confirmSfxVolume);
                    yield return PlayOptionCursorAdvanceAnimation(specificCursorRenderer);
                    yield return OpenHandicapSelectMenu();
                    continue;
                }

                if (string.Equals(SpecificSettingsOptions[selectedSpecificSettingIndex], "Louies", System.StringComparison.Ordinal))
                {
                    PlaySfx(confirmSfx, confirmSfxVolume);
                    yield return PlayOptionCursorAdvanceAnimation(specificCursorRenderer);
                    yield return OpenLouieSelectMenu();
                    continue;
                }

                if (string.Equals(SpecificSettingsOptions[selectedSpecificSettingIndex], "Music", System.StringComparison.Ordinal))
                {
                    PlaySfx(confirmSfx, confirmSfxVolume);
                    yield return PlayOptionCursorAdvanceAnimation(specificCursorRenderer);
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
        musicSelectCursorConfirmTimer = 0f;
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
                ToggleSelectedBattleMusic(out bool shouldPreviewMusic);
                PlaySfx(confirmSfx, confirmSfxVolume);
                if (shouldPreviewMusic)
                    PreviewSelectedBattleMusic();
                UpdateMusicSelectVisuals();
                RestartOptionCursorAdvanceFeedback(musicSelectCursorRenderer, ref musicSelectCursorConfirmTimer);
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
            text.color = GetMusicSelectTextColor(selected);
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
            checkboxText.color = GetMusicSelectTextColor(selected);
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

        if (optionCursorAdvanceAnimating)
            return;

        bool confirming = musicSelectCursorConfirmTimer > 0f;
        if (confirming)
        {
            musicSelectCursorRenderer.SetFrozen(false);
            musicSelectCursorRenderer.idle = false;
            musicSelectCursorRenderer.loop = false;
            musicSelectCursorRenderer.useSequenceDuration = true;
            musicSelectCursorRenderer.sequenceDuration = Mathf.Max(0.01f, optionCursorAdvanceSeconds);
        }
        else if (!musicSelectCursorRenderer.idle)
        {
            musicSelectCursorRenderer.idle = true;
            musicSelectCursorRenderer.loop = true;
            musicSelectCursorRenderer.CurrentFrame = 0;
        }

        musicSelectCursorRenderer.RefreshFrame();
    }

    private Color GetMusicSelectTextColor(bool highlighted)
    {
        if (leftPanel != null)
            return highlighted ? leftPanel.SelectedColor : leftPanel.NormalColor;

        return highlighted
            ? new Color32(0, 255, 60, 255)
            : new Color32(0, 180, 0, 255);
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

    private IEnumerator OpenItemSelectMenu()
    {
        state = MenuState.ItemSelect;
        RefreshItemSelectEntryOrder();
        selectedItemIndex = Mathf.Clamp(selectedItemIndex, 0, Mathf.Max(0, GetItemSelectEntryCount() - 1));

        workingBattleItemAmounts = SaveSystem.GetBattleModeItemAmounts(
            GameManager.GetDefaultBattleModeHiddenItemAmounts());

        if (specificSettingsRoot != null)
            specificSettingsRoot.gameObject.SetActive(false);

        EnsureItemSelectBuilt();
        ResetBackgroundSpriteSwap();
        ApplyCurrentBackgroundSprite(true);
        UpdatePromptTitle();
        UpdateItemSelectVisuals();

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

            int itemCount = GetItemSelectEntryCount();
            int columns = Mathf.Max(1, itemSelectColumns);

            if (itemCount <= 0)
            {
                SaveCurrentBattleItemAmounts();
                done = true;
            }
            else if (input.GetDown(GameSession.MinPlayerId, PlayerAction.MoveLeft))
            {
                selectedItemIndex = WrapIndex(selectedItemIndex - 1, itemCount);
                PlaySfx(moveCursorSfx, moveCursorSfxVolume);
                UpdateItemSelectVisuals();
            }
            else if (input.GetDown(GameSession.MinPlayerId, PlayerAction.MoveRight))
            {
                selectedItemIndex = WrapIndex(selectedItemIndex + 1, itemCount);
                PlaySfx(moveCursorSfx, moveCursorSfxVolume);
                UpdateItemSelectVisuals();
            }
            else if (input.GetDown(GameSession.MinPlayerId, PlayerAction.MoveUp))
            {
                selectedItemIndex = WrapIndex(selectedItemIndex - columns, itemCount);
                PlaySfx(moveCursorSfx, moveCursorSfxVolume);
                UpdateItemSelectVisuals();
            }
            else if (input.GetDown(GameSession.MinPlayerId, PlayerAction.MoveDown))
            {
                selectedItemIndex = WrapIndex(selectedItemIndex + columns, itemCount);
                PlaySfx(moveCursorSfx, moveCursorSfxVolume);
                UpdateItemSelectVisuals();
            }
            else if (input.GetDown(GameSession.MinPlayerId, PlayerAction.ActionB))
            {
                PlaySfx(returnSfx, returnSfxVolume);
                done = true;
            }
            else if (input.GetDown(GameSession.MinPlayerId, PlayerAction.Start))
            {
                PlaySfx(confirmSfx, confirmSfxVolume);
                done = true;
            }
            else if (input.GetDown(GameSession.MinPlayerId, PlayerAction.ActionA))
            {
                ChangeSelectedBattleItemAmount(1);
                PlaySfx(confirmSfx, confirmSfxVolume);
                itemSelectCursorConfirmTimer = Mathf.Max(0.01f, confirmFeedbackSeconds);
                UpdateItemSelectVisuals();
            }
            else if (input.GetDown(GameSession.MinPlayerId, PlayerAction.ActionC))
            {
                ChangeSelectedBattleItemAmount(-1);
                PlaySfx(confirmSfx, confirmSfxVolume);
                itemSelectCursorConfirmTimer = Mathf.Max(0.01f, confirmFeedbackSeconds);
                UpdateItemSelectVisuals();
            }

            UpdateItemSelectVisuals();
            itemSelectCursorConfirmTimer = Mathf.Max(0f, itemSelectCursorConfirmTimer - Time.unscaledDeltaTime);
            yield return null;
        }

        SaveCurrentBattleItemAmounts();

        if (itemSelectRoot != null)
            itemSelectRoot.gameObject.SetActive(false);

        if (specificSettingsRoot != null)
            specificSettingsRoot.gameObject.SetActive(true);

        state = MenuState.SpecificSettings;
        UpdatePromptTitle();
        UpdateSpecificSettingsVisuals();
    }

    private void EnsureItemSelectBuilt()
    {
        RefreshItemSelectEntryOrder();

        if (itemSelectRoot != null)
        {
            EnsureItemSelectCellCount();
            itemSelectRoot.gameObject.SetActive(true);
            itemSelectRoot.SetAsLastSibling();
            return;
        }

        Transform parent = GetMenuContentParent();
        GameObject rootGo = new("ItemSelectRoot", typeof(RectTransform));
        rootGo.transform.SetParent(parent, false);
        itemSelectRoot = rootGo.GetComponent<RectTransform>();
        itemSelectRoot.anchorMin = new Vector2(0.5f, 0.5f);
        itemSelectRoot.anchorMax = new Vector2(0.5f, 0.5f);
        itemSelectRoot.pivot = new Vector2(0.5f, 0.5f);
        itemSelectRoot.anchoredPosition = itemSelectRootOffset;
        itemSelectRoot.sizeDelta = Vector2.zero;
        itemSelectRoot.localScale = Vector3.one * currentUiScale;
        itemSelectRoot.SetAsLastSibling();

        itemSelectCells.Clear();
        itemSelectIconImages.Clear();
        itemSelectIconLabelTexts.Clear();
        itemSelectAmountTexts.Clear();
        itemSelectIconSprites = BuildItemSelectIconSprites();

        EnsureItemSelectCellCount();
        CreateItemSelectHintText();
        CreateItemSelectCursor();
    }

    private void EnsureItemSelectCellCount()
    {
        int itemCount = GetItemSelectEntryCount();
        while (itemSelectCells.Count < itemCount)
            CreateItemSelectCell(itemSelectCells.Count);
    }

    private void CreateItemSelectCell(int index)
    {
        GameObject cellGo = new($"ItemCell_{index}", typeof(RectTransform));
        cellGo.transform.SetParent(itemSelectRoot, false);
        RectTransform cell = cellGo.GetComponent<RectTransform>();
        cell.anchorMin = new Vector2(0.5f, 0.5f);
        cell.anchorMax = new Vector2(0.5f, 0.5f);
        cell.pivot = new Vector2(0.5f, 0.5f);
        itemSelectCells.Add(cell);

        GameObject iconGo = new($"ItemIcon_{index}", typeof(RectTransform), typeof(Image), typeof(Outline));
        iconGo.transform.SetParent(cell, false);
        Image icon = iconGo.GetComponent<Image>();
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        Outline outline = iconGo.GetComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(1f, -1f);
        itemSelectIconImages.Add(icon);

        GameObject iconLabelGo = new($"ItemIconLabel_{index}", typeof(RectTransform), typeof(TextMeshProUGUI));
        iconLabelGo.transform.SetParent(cell, false);
        TextMeshProUGUI iconLabel = iconLabelGo.GetComponent<TextMeshProUGUI>();
        iconLabel.alignment = TextAlignmentOptions.Center;
        iconLabel.raycastTarget = false;
        iconLabel.textWrappingMode = TextWrappingModes.NoWrap;
        iconLabel.overflowMode = TextOverflowModes.Overflow;
        itemSelectIconLabelTexts.Add(iconLabel);

        GameObject amountGo = new($"ItemAmount_{index}", typeof(RectTransform), typeof(TextMeshProUGUI));
        amountGo.transform.SetParent(cell, false);
        TextMeshProUGUI amountText = amountGo.GetComponent<TextMeshProUGUI>();
        amountText.alignment = TextAlignmentOptions.MidlineRight;
        amountText.raycastTarget = false;
        amountText.textWrappingMode = TextWrappingModes.NoWrap;
        amountText.overflowMode = TextOverflowModes.Overflow;
        itemSelectAmountTexts.Add(amountText);
    }

    private void CreateItemSelectHintText()
    {
        GameObject go = new("ItemSelectHints", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(itemSelectRoot, false);

        itemSelectHintText = go.GetComponent<TextMeshProUGUI>();
        itemSelectHintText.raycastTarget = false;
        itemSelectHintText.textWrappingMode = TextWrappingModes.NoWrap;
        itemSelectHintText.overflowMode = TextOverflowModes.Overflow;
        itemSelectHintText.text = "\u2190 \u2192\u2191\u2193: Choose\nA/C: Change Number\nB: Back";
        ApplySpecificSettingsTextStyle(itemSelectHintText);
    }

    private void CreateItemSelectCursor()
    {
        GameObject cursorGo = new("ItemSelectCursor", typeof(RectTransform), typeof(Image), typeof(AnimatedSpriteRenderer));
        cursorGo.transform.SetParent(itemSelectRoot, false);
        itemSelectCursorRt = cursorGo.transform as RectTransform;
        itemSelectCursorImage = cursorGo.GetComponent<Image>();
        itemSelectCursorRenderer = cursorGo.GetComponent<AnimatedSpriteRenderer>();

        itemSelectCursorRt.anchorMin = new Vector2(0.5f, 0.5f);
        itemSelectCursorRt.anchorMax = new Vector2(0.5f, 0.5f);
        itemSelectCursorRt.pivot = new Vector2(0.5f, 0.5f);
        itemSelectCursorRt.sizeDelta = itemSelectCursorSize;
        itemSelectCursorRt.localScale = Vector3.one;

        if (itemSelectCursorImage != null)
        {
            itemSelectCursorImage.raycastTarget = false;
            itemSelectCursorImage.preserveAspect = true;
            itemSelectCursorImage.sprite = GetItemSelectCursorSprite(0);
        }

        if (itemSelectCursorRenderer != null)
        {
            Sprite[] animation = GetItemSelectCursorAnimationSprites();
            itemSelectCursorRenderer.idleSprite = GetItemSelectCursorSprite(0);
            itemSelectCursorRenderer.animationSprite = animation;
            itemSelectCursorRenderer.animationTime = itemSelectCursorFrameSeconds;
            itemSelectCursorRenderer.useSequenceDuration = false;
            itemSelectCursorRenderer.SetFrozen(false);
            itemSelectCursorRenderer.frameOffsets = null;
            itemSelectCursorRenderer.idle = false;
            itemSelectCursorRenderer.loop = true;
            itemSelectCursorRenderer.CurrentFrame = 0;
            itemSelectCursorRenderer.RefreshFrame();
        }
    }

    private void UpdateItemSelectVisuals()
    {
        if (itemSelectRoot == null || !itemSelectRoot.gameObject.activeInHierarchy)
            return;

        RefreshItemSelectEntryOrder();
        itemSelectRoot.anchoredPosition = itemSelectRootOffset;
        int itemCount = GetItemSelectEntryCount();
        int columns = Mathf.Max(1, itemSelectColumns);
        int rows = Mathf.Max(1, Mathf.CeilToInt(itemCount / (float)columns));

        for (int i = 0; i < itemSelectCells.Count; i++)
        {
            bool active = i < itemCount;
            RectTransform cell = itemSelectCells[i];
            if (cell == null)
                continue;

            cell.gameObject.SetActive(active);
            if (!active)
                continue;

            cell.sizeDelta = itemSelectCellSize;
            cell.anchoredPosition = GetItemSelectCellPosition(i, columns, rows);

            if (i < itemSelectIconImages.Count && itemSelectIconImages[i] != null)
            {
                Image icon = itemSelectIconImages[i];
                RectTransform iconRt = icon.rectTransform;
                iconRt.anchorMin = new Vector2(0.5f, 0.5f);
                iconRt.anchorMax = new Vector2(0.5f, 0.5f);
                iconRt.pivot = new Vector2(0.5f, 0.5f);
                iconRt.anchoredPosition = itemSelectIconOffset;
                iconRt.sizeDelta = GetItemSelectIconSize(i);
                icon.sprite = GetItemSelectIconSprite(i);
                icon.enabled = icon.sprite != null;
                icon.color = Color.white;
            }

            if (i < itemSelectIconLabelTexts.Count && itemSelectIconLabelTexts[i] != null)
            {
                TextMeshProUGUI iconLabel = itemSelectIconLabelTexts[i];
                RectTransform labelRt = iconLabel.rectTransform;
                labelRt.anchorMin = new Vector2(0.5f, 0.5f);
                labelRt.anchorMax = new Vector2(0.5f, 0.5f);
                labelRt.pivot = new Vector2(0.5f, 0.5f);
                labelRt.anchoredPosition = itemSelectIconOffset;
                labelRt.sizeDelta = GetItemSelectIconSize(i);
                iconLabel.text = GetItemSelectIconLabel(i);
                iconLabel.enabled = !string.IsNullOrEmpty(iconLabel.text);
                iconLabel.color = Color.white;
                ApplySpecificSettingsTextStyle(iconLabel);
                iconLabel.fontSize = Mathf.Max(8, Mathf.RoundToInt(itemSelectAmountFontSize * 0.55f));
                iconLabel.alignment = TextAlignmentOptions.Center;
            }

            if (i < itemSelectAmountTexts.Count && itemSelectAmountTexts[i] != null)
            {
                TextMeshProUGUI amountText = itemSelectAmountTexts[i];
                RectTransform amountRt = amountText.rectTransform;
                amountRt.anchorMin = new Vector2(0.5f, 0.5f);
                amountRt.anchorMax = new Vector2(0.5f, 0.5f);
                amountRt.pivot = new Vector2(0.5f, 0.5f);
                amountRt.anchoredPosition = itemSelectAmountOffset;
                amountRt.sizeDelta = itemSelectAmountSize;
                amountText.text = GetWorkingBattleItemAmount(i).ToString();
                amountText.color = i == selectedItemIndex ? GetMusicSelectTextColor(true) : GetMusicSelectTextColor(false);
                ApplySpecificSettingsTextStyle(amountText);
                amountText.fontSize = itemSelectAmountFontSize;
                amountText.alignment = TextAlignmentOptions.MidlineRight;
            }
        }

        if (itemSelectHintText != null)
        {
            RectTransform hintRt = itemSelectHintText.rectTransform;
            hintRt.anchorMin = new Vector2(0.5f, 0.5f);
            hintRt.anchorMax = new Vector2(0.5f, 0.5f);
            hintRt.pivot = new Vector2(0.5f, 0.5f);
            hintRt.anchoredPosition = itemSelectHintOffset;
            hintRt.sizeDelta = itemSelectHintSize;

            itemSelectHintText.color = GetMusicSelectTextColor(false);
            itemSelectHintText.text = "←→↑↓: Choose\nA/C: Change Number\nB: Back";
            ApplySpecificSettingsTextStyle(itemSelectHintText);
            itemSelectHintText.fontSize = itemSelectHintFontSize;
            itemSelectHintText.lineSpacing = itemSelectHintLineSpacing;
            itemSelectHintText.alignment = TextAlignmentOptions.Center;
        }

        if (itemSelectCursorRt != null)
        {
            int rowIndex = Mathf.Clamp(selectedItemIndex, 0, Mathf.Max(0, itemCount - 1));
            itemSelectCursorRt.gameObject.SetActive(itemCount > 0);
            itemSelectCursorRt.sizeDelta = itemSelectCursorSize;
            itemSelectCursorRt.anchoredPosition = GetItemSelectCellPosition(rowIndex, columns, rows) + itemSelectIconOffset + itemSelectCursorOffset;
            itemSelectCursorRenderer?.SetExternalBaseLocalPosition(itemSelectCursorRt.localPosition);
            UpdateItemSelectCursorAnimationState();
        }
    }

    private Vector2 GetItemSelectCellPosition(int itemIndex, int columns, int rows)
    {
        int col = itemIndex % Mathf.Max(1, columns);
        int row = itemIndex / Mathf.Max(1, columns);
        float x = (col - ((columns - 1) * 0.5f)) * itemSelectCellSize.x;
        float y = (((rows - 1) * 0.5f) - row) * itemSelectCellSize.y;
        return itemSelectGridOffset + new Vector2(x, y);
    }

    private Vector2 GetItemSelectIconSize(int index)
    {
        return IsRandomEggEntry(index)
            ? itemSelectIconSize * Mathf.Max(0.01f, itemSelectRandomEggIconScale)
            : itemSelectIconSize;
    }

    private Sprite GetItemSelectCursorSprite(int index)
    {
        if (itemSelectCursorSprites == null ||
            index < 0 ||
            index >= itemSelectCursorSprites.Length)
        {
            return null;
        }

        return itemSelectCursorSprites[index];
    }

    private Sprite[] GetItemSelectCursorAnimationSprites()
    {
        Sprite frame0 = GetItemSelectCursorSprite(0);
        Sprite frame1 = GetItemSelectCursorSprite(1);
        Sprite frame2 = GetItemSelectCursorSprite(2);

        if (frame0 == null || frame1 == null || frame2 == null)
            return System.Array.Empty<Sprite>();

        return new[] { frame0, frame1, frame2, frame1 };
    }

    private void UpdateItemSelectCursorAnimationState()
    {
        if (itemSelectCursorRenderer == null)
            return;

        Sprite[] animation = GetItemSelectCursorAnimationSprites();
        if (animation.Length > 0)
            itemSelectCursorRenderer.animationSprite = animation;

        itemSelectCursorRenderer.idleSprite = GetItemSelectCursorSprite(0);
        itemSelectCursorRenderer.animationTime = Mathf.Max(0.01f, itemSelectCursorFrameSeconds);
        itemSelectCursorRenderer.idle = false;
        itemSelectCursorRenderer.loop = true;
        itemSelectCursorRenderer.SetFrozen(false);

        itemSelectCursorRenderer.RefreshFrame();
    }

    private void ChangeSelectedBattleItemAmount(int delta)
    {
        if (workingBattleItemAmounts == null || workingBattleItemAmounts.Length == 0)
            return;

        int index = GetItemSelectCanonicalIndex(selectedItemIndex);
        if (index < 0 || index >= workingBattleItemAmounts.Length)
            return;

        int max = Mathf.Max(0, itemSelectMaxAmount);
        workingBattleItemAmounts[index] = Mathf.Clamp(workingBattleItemAmounts[index] + delta, 0, max);

        NormalizeWorkingRandomEggRange(index);
        SaveCurrentBattleItemAmounts();
    }

    private void NormalizeWorkingRandomEggRange(int changedIndex)
    {
        if (workingBattleItemAmounts == null)
            return;

        int minIndex = GetBattleModeHiddenDropEntryIndex(GameManager.BattleModeHiddenDropEntryKind.RandomEggsMin);
        int maxIndex = GetBattleModeHiddenDropEntryIndex(GameManager.BattleModeHiddenDropEntryKind.RandomEggsMax);
        if (minIndex < 0 ||
            maxIndex < 0 ||
            minIndex >= workingBattleItemAmounts.Length ||
            maxIndex >= workingBattleItemAmounts.Length)
        {
            return;
        }

        if (changedIndex == minIndex && workingBattleItemAmounts[minIndex] > workingBattleItemAmounts[maxIndex])
            workingBattleItemAmounts[maxIndex] = workingBattleItemAmounts[minIndex];
        else if (changedIndex == maxIndex && workingBattleItemAmounts[maxIndex] < workingBattleItemAmounts[minIndex])
            workingBattleItemAmounts[minIndex] = workingBattleItemAmounts[maxIndex];
    }

    private static int GetBattleModeHiddenDropEntryIndex(GameManager.BattleModeHiddenDropEntryKind kind)
    {
        GameManager.BattleModeHiddenDropEntry[] entries = GameManager.BattleModeHiddenDropEntries;
        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i].Kind == kind)
                return i;
        }

        return -1;
    }

    private void RefreshItemSelectEntryOrder()
    {
        List<ItemSelectEntryId> nextOrder = new(DefaultItemSelectEntryOrder.Length);
        HashSet<ItemSelectEntryId> used = new();

        if (itemSelectEntryOrder != null)
        {
            for (int i = 0; i < itemSelectEntryOrder.Count; i++)
            {
                ItemSelectEntryId entryId = itemSelectEntryOrder[i];
                if (IsKnownItemSelectEntry(entryId) && used.Add(entryId))
                    nextOrder.Add(entryId);
            }
        }

        for (int i = 0; i < DefaultItemSelectEntryOrder.Length; i++)
        {
            ItemSelectEntryId entryId = DefaultItemSelectEntryOrder[i];
            if (used.Add(entryId))
                nextOrder.Add(entryId);
        }

        bool changed = resolvedItemSelectEntryOrder.Count != nextOrder.Count;
        if (!changed)
        {
            for (int i = 0; i < nextOrder.Count; i++)
            {
                if (resolvedItemSelectEntryOrder[i] == nextOrder[i])
                    continue;

                changed = true;
                break;
            }
        }

        if (!changed)
            return;

        resolvedItemSelectEntryOrder.Clear();
        resolvedItemSelectEntryOrder.AddRange(nextOrder);
        itemSelectIconSprites = null;
    }

    private static bool IsKnownItemSelectEntry(ItemSelectEntryId entryId)
    {
        for (int i = 0; i < DefaultItemSelectEntryOrder.Length; i++)
        {
            if (DefaultItemSelectEntryOrder[i] == entryId)
                return true;
        }

        return false;
    }

    private int GetItemSelectEntryCount()
    {
        if (resolvedItemSelectEntryOrder.Count == 0)
            RefreshItemSelectEntryOrder();

        return resolvedItemSelectEntryOrder.Count;
    }

    private ItemSelectEntryId GetItemSelectEntryId(int visualIndex)
    {
        if (resolvedItemSelectEntryOrder.Count == 0)
            RefreshItemSelectEntryOrder();

        if (resolvedItemSelectEntryOrder.Count == 0)
            return ItemSelectEntryId.ExtraBomb;

        return resolvedItemSelectEntryOrder[Mathf.Clamp(visualIndex, 0, resolvedItemSelectEntryOrder.Count - 1)];
    }

    private GameManager.BattleModeHiddenDropEntry GetItemSelectDropEntry(int visualIndex)
    {
        return GetItemSelectDropEntry(GetItemSelectEntryId(visualIndex));
    }

    private static GameManager.BattleModeHiddenDropEntry GetItemSelectDropEntry(ItemSelectEntryId entryId)
    {
        return entryId switch
        {
            ItemSelectEntryId.ExtraBomb => new GameManager.BattleModeHiddenDropEntry(GameManager.BattleModeHiddenDropEntryKind.Item, ItemType.ExtraBomb),
            ItemSelectEntryId.BlastRadius => new GameManager.BattleModeHiddenDropEntry(GameManager.BattleModeHiddenDropEntryKind.Item, ItemType.BlastRadius),
            ItemSelectEntryId.SpeedIncrese => new GameManager.BattleModeHiddenDropEntry(GameManager.BattleModeHiddenDropEntryKind.Item, ItemType.SpeedIncrese),
            ItemSelectEntryId.BombKick => new GameManager.BattleModeHiddenDropEntry(GameManager.BattleModeHiddenDropEntryKind.Item, ItemType.BombKick),
            ItemSelectEntryId.BombPunch => new GameManager.BattleModeHiddenDropEntry(GameManager.BattleModeHiddenDropEntryKind.Item, ItemType.BombPunch),
            ItemSelectEntryId.PierceBomb => new GameManager.BattleModeHiddenDropEntry(GameManager.BattleModeHiddenDropEntryKind.Item, ItemType.PierceBomb),
            ItemSelectEntryId.ControlBomb => new GameManager.BattleModeHiddenDropEntry(GameManager.BattleModeHiddenDropEntryKind.Item, ItemType.ControlBomb),
            ItemSelectEntryId.PowerBomb => new GameManager.BattleModeHiddenDropEntry(GameManager.BattleModeHiddenDropEntryKind.Item, ItemType.PowerBomb),
            ItemSelectEntryId.RubberBomb => new GameManager.BattleModeHiddenDropEntry(GameManager.BattleModeHiddenDropEntryKind.Item, ItemType.RubberBomb),
            ItemSelectEntryId.MagnetBomb => new GameManager.BattleModeHiddenDropEntry(GameManager.BattleModeHiddenDropEntryKind.Item, ItemType.MagnetBomb),
            ItemSelectEntryId.FullFire => new GameManager.BattleModeHiddenDropEntry(GameManager.BattleModeHiddenDropEntryKind.Item, ItemType.FullFire),
            ItemSelectEntryId.BombPass => new GameManager.BattleModeHiddenDropEntry(GameManager.BattleModeHiddenDropEntryKind.Item, ItemType.BombPass),
            ItemSelectEntryId.DestructiblePass => new GameManager.BattleModeHiddenDropEntry(GameManager.BattleModeHiddenDropEntryKind.Item, ItemType.DestructiblePass),
            ItemSelectEntryId.InvincibleSuit => new GameManager.BattleModeHiddenDropEntry(GameManager.BattleModeHiddenDropEntryKind.Item, ItemType.InvincibleSuit),
            ItemSelectEntryId.Heart => new GameManager.BattleModeHiddenDropEntry(GameManager.BattleModeHiddenDropEntryKind.Item, ItemType.Heart),
            ItemSelectEntryId.PowerGlove => new GameManager.BattleModeHiddenDropEntry(GameManager.BattleModeHiddenDropEntryKind.Item, ItemType.PowerGlove),
            ItemSelectEntryId.RandomEggsMin => new GameManager.BattleModeHiddenDropEntry(GameManager.BattleModeHiddenDropEntryKind.RandomEggsMin),
            ItemSelectEntryId.RandomEggsMax => new GameManager.BattleModeHiddenDropEntry(GameManager.BattleModeHiddenDropEntryKind.RandomEggsMax),
            ItemSelectEntryId.Skull => new GameManager.BattleModeHiddenDropEntry(GameManager.BattleModeHiddenDropEntryKind.Item, ItemType.Skull),
            _ => new GameManager.BattleModeHiddenDropEntry(GameManager.BattleModeHiddenDropEntryKind.Item, ItemType.ExtraBomb)
        };
    }

    private int GetItemSelectCanonicalIndex(int visualIndex)
    {
        GameManager.BattleModeHiddenDropEntry visualEntry = GetItemSelectDropEntry(visualIndex);
        GameManager.BattleModeHiddenDropEntry[] entries = GameManager.BattleModeHiddenDropEntries;
        for (int i = 0; i < entries.Length; i++)
        {
            if (IsSameBattleModeHiddenDropEntry(entries[i], visualEntry))
                return i;
        }

        return -1;
    }

    private static bool IsSameBattleModeHiddenDropEntry(
        GameManager.BattleModeHiddenDropEntry a,
        GameManager.BattleModeHiddenDropEntry b)
    {
        return a.Kind == b.Kind &&
            (a.Kind != GameManager.BattleModeHiddenDropEntryKind.Item || a.ItemType == b.ItemType);
    }

    private int GetWorkingBattleItemAmount(int index)
    {
        int canonicalIndex = GetItemSelectCanonicalIndex(index);
        if (workingBattleItemAmounts == null || canonicalIndex < 0 || canonicalIndex >= workingBattleItemAmounts.Length)
            return 0;

        return workingBattleItemAmounts[canonicalIndex];
    }

    private void SaveCurrentBattleItemAmounts()
    {
        if (workingBattleItemAmounts == null)
        {
            workingBattleItemAmounts = SaveSystem.GetBattleModeItemAmounts(
                GameManager.GetDefaultBattleModeHiddenItemAmounts());
        }

        SaveSystem.SetBattleModeItemAmounts(workingBattleItemAmounts);
    }

    private Sprite[] BuildItemSelectIconSprites()
    {
        AutoItemDatabase.BuildIfNeeded();
        int itemCount = GetItemSelectEntryCount();
        Sprite[] sprites = new Sprite[itemCount];
        itemSelectRandomEggSprite = GetItemIconSprite(ItemType.BlueLouieEgg);
        for (int i = 0; i < itemCount; i++)
        {
            GameManager.BattleModeHiddenDropEntry entry = GetItemSelectDropEntry(i);
            sprites[i] = entry.Kind == GameManager.BattleModeHiddenDropEntryKind.Item
                ? GetItemIconSprite(entry.ItemType)
                : itemSelectRandomEggSprite;
        }

        return sprites;
    }

    private Sprite GetItemSelectIconSprite(int index)
    {
        if (itemSelectIconSprites == null || itemSelectIconSprites.Length != GetItemSelectEntryCount())
            itemSelectIconSprites = BuildItemSelectIconSprites();

        if (index < 0 || index >= itemSelectIconSprites.Length)
            return null;

        return itemSelectIconSprites[index];
    }

    private string GetItemSelectIconLabel(int index)
    {
        GameManager.BattleModeHiddenDropEntry entry = GetItemSelectDropEntry(index);

        return entry.Kind switch
        {
            GameManager.BattleModeHiddenDropEntryKind.RandomEggsMin => "MIN",
            GameManager.BattleModeHiddenDropEntryKind.RandomEggsMax => "MAX",
            _ => string.Empty
        };
    }

    private bool IsRandomEggEntry(int index)
    {
        GameManager.BattleModeHiddenDropEntry entry = GetItemSelectDropEntry(index);

        return entry.Kind == GameManager.BattleModeHiddenDropEntryKind.RandomEggsMin ||
            entry.Kind == GameManager.BattleModeHiddenDropEntryKind.RandomEggsMax;
    }

    private static Sprite GetItemIconSprite(ItemType itemType)
    {
        ItemPickup prefab = AutoItemDatabase.Get(itemType);
        if (prefab == null)
            return null;

        if (prefab.idleRenderer != null && prefab.idleRenderer.idleSprite != null)
            return prefab.idleRenderer.idleSprite;

        SpriteRenderer spriteRenderer = prefab.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null && spriteRenderer.sprite != null)
            return spriteRenderer.sprite;

        spriteRenderer = prefab.GetComponentInChildren<SpriteRenderer>(true);
        return spriteRenderer != null ? spriteRenderer.sprite : null;
    }

    private IEnumerator OpenLouieSelectMenu()
    {
        state = MenuState.LouieSelect;
        louieSelectCursorConfirmTimer = 0f;
        selectedLouieIndex = Mathf.Clamp(selectedLouieIndex, 0, Mathf.Max(0, GetLouieSelectEntryCount() - 1));
        workingBattleLouieAmounts = SaveSystem.GetBattleModeLouieAmounts(GameManager.GetDefaultBattleModeLouieAmounts());

        if (specificSettingsRoot != null)
            specificSettingsRoot.gameObject.SetActive(false);

        EnsureLouieSelectBuilt();
        ResetBackgroundSpriteSwap();
        ApplyCurrentBackgroundSprite(true);
        UpdatePromptTitle();
        UpdateLouieSelectVisuals();

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

            int count = GetLouieSelectEntryCount();
            int columns = Mathf.Max(1, louieSelectColumns);
            if (input.GetDown(GameSession.MinPlayerId, PlayerAction.MoveLeft))
            {
                selectedLouieIndex = WrapIndex(selectedLouieIndex - 1, count);
                PlaySfx(moveCursorSfx, moveCursorSfxVolume);
            }
            else if (input.GetDown(GameSession.MinPlayerId, PlayerAction.MoveRight))
            {
                selectedLouieIndex = WrapIndex(selectedLouieIndex + 1, count);
                PlaySfx(moveCursorSfx, moveCursorSfxVolume);
            }
            else if (input.GetDown(GameSession.MinPlayerId, PlayerAction.MoveUp))
            {
                selectedLouieIndex = WrapIndex(selectedLouieIndex - columns, count);
                PlaySfx(moveCursorSfx, moveCursorSfxVolume);
            }
            else if (input.GetDown(GameSession.MinPlayerId, PlayerAction.MoveDown))
            {
                selectedLouieIndex = WrapIndex(selectedLouieIndex + columns, count);
                PlaySfx(moveCursorSfx, moveCursorSfxVolume);
            }
            else if (input.GetDown(GameSession.MinPlayerId, PlayerAction.ActionB))
            {
                PlaySfx(returnSfx, returnSfxVolume);
                SaveCurrentBattleLouieAmounts();
                done = true;
            }
            else if (input.GetDown(GameSession.MinPlayerId, PlayerAction.ActionA))
            {
                ChangeSelectedBattleLouieAmount(1);
                PlaySfx(confirmSfx, confirmSfxVolume);
                UpdateLouieSelectVisuals();
                RestartOptionCursorAdvanceFeedback(louieSelectCursorRenderer, ref louieSelectCursorConfirmTimer);
            }
            else if (input.GetDown(GameSession.MinPlayerId, PlayerAction.ActionC))
            {
                ChangeSelectedBattleLouieAmount(-1);
                PlaySfx(confirmSfx, confirmSfxVolume);
                UpdateLouieSelectVisuals();
                RestartOptionCursorAdvanceFeedback(louieSelectCursorRenderer, ref louieSelectCursorConfirmTimer);
            }

            UpdateLouieSelectVisuals();
            louieSelectCursorConfirmTimer = Mathf.Max(0f, louieSelectCursorConfirmTimer - Time.unscaledDeltaTime);
            yield return null;
        }

        if (louieSelectRoot != null)
            louieSelectRoot.gameObject.SetActive(false);

        if (specificSettingsRoot != null)
            specificSettingsRoot.gameObject.SetActive(true);

        state = MenuState.SpecificSettings;
        UpdatePromptTitle();
        UpdateSpecificSettingsVisuals();
    }

    private void EnsureLouieSelectBuilt()
    {
        if (louieSelectRoot != null)
        {
            EnsureLouieSelectCellCount();
            louieSelectRoot.gameObject.SetActive(true);
            louieSelectRoot.SetAsLastSibling();
            return;
        }

        GameObject rootGo = new("LouieSelectRoot", typeof(RectTransform));
        rootGo.transform.SetParent(GetMenuContentParent(), false);
        louieSelectRoot = rootGo.GetComponent<RectTransform>();
        louieSelectRoot.anchorMin = new Vector2(0.5f, 0.5f);
        louieSelectRoot.anchorMax = new Vector2(0.5f, 0.5f);
        louieSelectRoot.pivot = new Vector2(0.5f, 0.5f);
        louieSelectRoot.anchoredPosition = louieSelectRootOffset;
        louieSelectRoot.sizeDelta = Vector2.zero;
        louieSelectRoot.localScale = Vector3.one * currentUiScale;
        louieSelectRoot.SetAsLastSibling();

        louieSelectCells.Clear();
        louieSelectImages.Clear();
        louieSelectRenderers.Clear();
        louieSelectAmountTexts.Clear();

        EnsureLouieSelectCellCount();
        CreateLouieSelectHintText();
        CreateLouieSelectCursor();
    }

    private void EnsureLouieSelectCellCount()
    {
        int count = GetLouieSelectEntryCount();
        while (louieSelectCells.Count < count)
            CreateLouieSelectCell(louieSelectCells.Count);
    }

    private void CreateLouieSelectCell(int index)
    {
        GameObject cellGo = new($"LouieCell_{index}", typeof(RectTransform));
        cellGo.transform.SetParent(louieSelectRoot, false);
        RectTransform cell = cellGo.GetComponent<RectTransform>();
        cell.anchorMin = new Vector2(0.5f, 0.5f);
        cell.anchorMax = new Vector2(0.5f, 0.5f);
        cell.pivot = new Vector2(0.5f, 0.5f);
        louieSelectCells.Add(cell);

        GameObject imageGo = new($"LouieIcon_{index}", typeof(RectTransform), typeof(Image), typeof(AnimatedSpriteRenderer));
        imageGo.transform.SetParent(cell, false);
        Image image = imageGo.GetComponent<Image>();
        image.preserveAspect = true;
        image.raycastTarget = false;
        louieSelectImages.Add(image);

        AnimatedSpriteRenderer renderer = imageGo.GetComponent<AnimatedSpriteRenderer>();
        ConfigureLouieSelectRenderer(renderer, index);
        louieSelectRenderers.Add(renderer);

        GameObject amountGo = new($"LouieAmount_{index}", typeof(RectTransform), typeof(TextMeshProUGUI));
        amountGo.transform.SetParent(cell, false);
        TextMeshProUGUI amountText = amountGo.GetComponent<TextMeshProUGUI>();
        amountText.alignment = TextAlignmentOptions.MidlineRight;
        amountText.raycastTarget = false;
        amountText.textWrappingMode = TextWrappingModes.NoWrap;
        amountText.overflowMode = TextOverflowModes.Overflow;
        louieSelectAmountTexts.Add(amountText);
    }

    private void CreateLouieSelectHintText()
    {
        GameObject go = new("LouieSelectHints", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(louieSelectRoot, false);
        louieSelectHintText = go.GetComponent<TextMeshProUGUI>();
        louieSelectHintText.raycastTarget = false;
        louieSelectHintText.textWrappingMode = TextWrappingModes.NoWrap;
        louieSelectHintText.overflowMode = TextOverflowModes.Overflow;
        louieSelectHintText.text = "\u2190\u2192\u2191\u2193: Choose\nA/C: Change Number\nB: Back";
        ApplySpecificSettingsTextStyle(louieSelectHintText);
    }

    private void CreateLouieSelectCursor()
    {
        AnimatedSpriteRenderer source = specificCursorRenderer != null
            ? specificCursorRenderer
            : leftPanel != null ? leftPanel.CursorRenderer : null;
        GameObject cursorGo = source != null
            ? Instantiate(source.gameObject, louieSelectRoot, false)
            : new GameObject("LouieSelectCursor", typeof(RectTransform));
        cursorGo.name = "LouieSelectCursor";

        louieSelectCursorRt = cursorGo.transform as RectTransform;
        if (louieSelectCursorRt == null)
            louieSelectCursorRt = cursorGo.AddComponent<RectTransform>();

        louieSelectCursorRenderer = cursorGo.GetComponent<AnimatedSpriteRenderer>();
        louieSelectCursorRt.anchorMin = new Vector2(0.5f, 0.5f);
        louieSelectCursorRt.anchorMax = new Vector2(0.5f, 0.5f);
        louieSelectCursorRt.pivot = new Vector2(0.5f, 0.5f);
        louieSelectCursorRt.sizeDelta = louieSelectCursorSize;
        louieSelectCursorRt.localScale = Vector3.one;

        if (louieSelectCursorRenderer != null)
        {
            louieSelectCursorRenderer.SetFrozen(false);
            louieSelectCursorRenderer.frameOffsets = null;
            louieSelectCursorRenderer.idle = true;
            louieSelectCursorRenderer.loop = true;
            louieSelectCursorRenderer.CurrentFrame = 0;
            louieSelectCursorRenderer.RefreshFrame();
        }
    }

    private void UpdateLouieSelectVisuals()
    {
        if (louieSelectRoot == null || !louieSelectRoot.gameObject.activeInHierarchy)
            return;

        louieSelectRoot.anchoredPosition = louieSelectRootOffset;
        int count = GetLouieSelectEntryCount();
        int columns = Mathf.Max(1, louieSelectColumns);
        int rows = Mathf.Max(1, Mathf.CeilToInt(count / (float)columns));

        for (int i = 0; i < louieSelectCells.Count; i++)
        {
            bool active = i < count;
            RectTransform cell = louieSelectCells[i];
            if (cell == null)
                continue;

            cell.gameObject.SetActive(active);
            if (!active)
                continue;

            int amount = GetWorkingBattleLouieAmount(i);
            cell.sizeDelta = louieSelectCellSize;
            cell.anchoredPosition = GetLouieSelectCellPosition(i, columns, rows);

            if (i < louieSelectImages.Count && louieSelectImages[i] != null)
            {
                Image image = louieSelectImages[i];
                RectTransform imageRt = image.rectTransform;
                imageRt.anchorMin = new Vector2(0.5f, 0.5f);
                imageRt.anchorMax = new Vector2(0.5f, 0.5f);
                imageRt.pivot = new Vector2(0.5f, 0.5f);
                imageRt.anchoredPosition = louieSelectIconOffset;
                imageRt.sizeDelta = GetLouieSelectIconSize(i);
                image.color = amount > 0 ? Color.white : louieSelectDisabledTint;
            }

            if (i < louieSelectRenderers.Count && louieSelectRenderers[i] != null)
            {
                AnimatedSpriteRenderer renderer = louieSelectRenderers[i];
                if (renderer.animationSprite == null || renderer.animationSprite.Length == 0)
                    ConfigureLouieSelectRenderer(renderer, i);
                ApplyLouieSelectRendererTiming(renderer, i);
                renderer.idle = false;
                renderer.loop = true;
            }

            if (i < louieSelectAmountTexts.Count && louieSelectAmountTexts[i] != null)
            {
                TextMeshProUGUI amountText = louieSelectAmountTexts[i];
                RectTransform amountRt = amountText.rectTransform;
                amountRt.anchorMin = new Vector2(0.5f, 0.5f);
                amountRt.anchorMax = new Vector2(0.5f, 0.5f);
                amountRt.pivot = new Vector2(0.5f, 0.5f);
                amountRt.anchoredPosition = louieSelectAmountOffset;
                amountRt.sizeDelta = louieSelectAmountSize;
                amountText.text = amount.ToString();
                amountText.color = GetMusicSelectTextColor(amount > 0);
                ApplySpecificSettingsTextStyle(amountText);
                amountText.fontSize = louieSelectAmountFontSize;
                amountText.alignment = TextAlignmentOptions.MidlineRight;
            }
        }

        if (louieSelectHintText != null)
        {
            RectTransform hintRt = louieSelectHintText.rectTransform;
            hintRt.anchorMin = new Vector2(0.5f, 0.5f);
            hintRt.anchorMax = new Vector2(0.5f, 0.5f);
            hintRt.pivot = new Vector2(0.5f, 0.5f);
            hintRt.anchoredPosition = louieSelectHintOffset;
            hintRt.sizeDelta = louieSelectHintSize;
            louieSelectHintText.color = GetMusicSelectTextColor(false);
            louieSelectHintText.text = "\u2190\u2192\u2191\u2193: Choose\nA/C: Change Number\nB: Back";
            ApplySpecificSettingsTextStyle(louieSelectHintText);
            louieSelectHintText.fontSize = louieSelectHintFontSize;
            louieSelectHintText.alignment = TextAlignmentOptions.Center;
            louieSelectHintText.lineSpacing = louieSelectHintLineSpacing;
        }

        if (louieSelectCursorRt != null)
        {
            int rowIndex = Mathf.Clamp(selectedLouieIndex, 0, Mathf.Max(0, count - 1));
            louieSelectCursorRt.gameObject.SetActive(count > 0);
            louieSelectCursorRt.sizeDelta = louieSelectCursorSize;
            louieSelectCursorRt.anchoredPosition = GetLouieSelectCellPosition(rowIndex, columns, rows) + louieSelectIconOffset + louieSelectCursorOffset;
            louieSelectCursorRt.SetAsLastSibling();
            louieSelectCursorRenderer?.SetExternalBaseLocalPosition(louieSelectCursorRt.localPosition);
            UpdateLouieSelectCursorAnimationState();
        }
    }

    private void ConfigureLouieSelectRenderer(AnimatedSpriteRenderer renderer, int index)
    {
        if (renderer == null)
            return;

        AnimatedSpriteRenderer source = GetLouieDownRenderer(index);
        if (source != null)
        {
            renderer.idleSprite = source.idleSprite;
            renderer.animationSprite = source.animationSprite;
            renderer.animationTime = Mathf.Max(0.001f, source.animationTime / Mathf.Max(0.01f, louieSelectAnimationSpeedMultiplier));
            renderer.useSequenceDuration = source.useSequenceDuration;
            renderer.sequenceDuration = Mathf.Max(0.001f, source.sequenceDuration / Mathf.Max(0.01f, louieSelectAnimationSpeedMultiplier));
        }

        renderer.frameOffsets = null;
        renderer.idle = false;
        renderer.loop = true;
        renderer.CurrentFrame = 0;
        renderer.RefreshFrame();
    }

    private void ApplyLouieSelectRendererTiming(AnimatedSpriteRenderer renderer, int index)
    {
        if (renderer == null)
            return;

        AnimatedSpriteRenderer source = GetLouieDownRenderer(index);
        if (source == null)
            return;

        float speed = Mathf.Max(0.01f, louieSelectAnimationSpeedMultiplier);
        renderer.animationTime = Mathf.Max(0.001f, source.animationTime / speed);
        renderer.sequenceDuration = Mathf.Max(0.001f, source.sequenceDuration / speed);
    }

    private AnimatedSpriteRenderer GetLouieDownRenderer(int index)
    {
        GameObject prefab = louieSelectMountPrefabs != null && index >= 0 && index < louieSelectMountPrefabs.Length
            ? louieSelectMountPrefabs[index]
            : null;

        if (prefab == null)
            return null;

        MountedType type = GetLouieSelectMountType(index);

        if (type == MountedType.Mole)
        {
            AnimatedSpriteRenderer movimentation = FindAnimatedRendererByName(prefab.transform, "Movimentation");
            if (movimentation != null)
                return movimentation;
        }

        MountMovementController movement = prefab.GetComponent<MountMovementController>();
        if (movement != null && movement.spriteRendererDown != null)
            return movement.spriteRendererDown;

        AnimatedSpriteRenderer down = FindAnimatedRendererByName(prefab.transform, "Down");
        if (down != null)
            return down;

        return prefab.GetComponentInChildren<AnimatedSpriteRenderer>(true);
    }

    private static AnimatedSpriteRenderer FindAnimatedRendererByName(Transform rootTransform, string objectName)
    {
        if (rootTransform == null || string.IsNullOrEmpty(objectName))
            return null;

        AnimatedSpriteRenderer[] renderers = rootTransform.GetComponentsInChildren<AnimatedSpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            AnimatedSpriteRenderer renderer = renderers[i];
            if (renderer != null && renderer.gameObject.name == objectName)
                return renderer;
        }

        return null;
    }

    private int GetLouieSelectEntryCount()
    {
        return louieSelectMountPrefabs != null && louieSelectMountPrefabs.Length > 0
            ? louieSelectMountPrefabs.Length
            : GameManager.BattleModeRandomEggMountTypes.Length;
    }

    private MountedType GetLouieSelectMountType(int index)
    {
        GameObject prefab = louieSelectMountPrefabs != null && index >= 0 && index < louieSelectMountPrefabs.Length
            ? louieSelectMountPrefabs[index]
            : null;

        if (prefab != null)
        {
            MountedType fromName = ResolveLouieSelectMountTypeFromName(prefab.name);
            if (fromName != MountedType.None)
                return fromName;
        }

        return index >= 0 && index < GameManager.BattleModeRandomEggMountTypes.Length
            ? GameManager.BattleModeRandomEggMountTypes[index]
            : MountedType.None;
    }

    private int GetLouieSelectCanonicalIndex(int visualIndex)
    {
        MountedType type = GetLouieSelectMountType(visualIndex);
        for (int i = 0; i < GameManager.BattleModeRandomEggMountTypes.Length; i++)
        {
            if (GameManager.BattleModeRandomEggMountTypes[i] == type)
                return i;
        }

        return -1;
    }

    private static MountedType ResolveLouieSelectMountTypeFromName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return MountedType.None;

        string normalized = name.ToLowerInvariant();
        if (normalized.Contains("blue")) return MountedType.Blue;
        if (normalized.Contains("black")) return MountedType.Black;
        if (normalized.Contains("purple")) return MountedType.Purple;
        if (normalized.Contains("green")) return MountedType.Green;
        if (normalized.Contains("yellow")) return MountedType.Yellow;
        if (normalized.Contains("pink")) return MountedType.Pink;
        if (normalized.Contains("red")) return MountedType.Red;
        if (normalized.Contains("mole")) return MountedType.Mole;
        if (normalized.Contains("tank")) return MountedType.Tank;
        return MountedType.None;
    }

    private Vector2 GetLouieSelectCellPosition(int index, int columns, int rows)
    {
        int col = index % Mathf.Max(1, columns);
        int row = index / Mathf.Max(1, columns);
        float x = (col - ((columns - 1) * 0.5f)) * louieSelectCellSize.x;
        float y = (((rows - 1) * 0.5f) - row) * louieSelectCellSize.y;
        return louieSelectGridOffset + new Vector2(x, y);
    }

    private Vector2 GetLouieSelectIconSize(int index)
    {
        float multiplier = Mathf.Max(0.01f, louieSelectMountSizeMultiplier);
        if (GetLouieSelectMountType(index) == MountedType.Pink)
        {
            multiplier *= Mathf.Max(0.01f, louieSelectPinkLouieSizeMultiplier);
        }

        return louieSelectIconSize * multiplier;
    }

    private void UpdateLouieSelectCursorAnimationState()
    {
        if (louieSelectCursorRenderer == null)
            return;

        if (optionCursorAdvanceAnimating)
            return;

        bool confirming = louieSelectCursorConfirmTimer > 0f;
        louieSelectCursorRenderer.idle = !confirming;
        louieSelectCursorRenderer.loop = !confirming;
        if (confirming)
        {
            louieSelectCursorRenderer.useSequenceDuration = true;
            louieSelectCursorRenderer.sequenceDuration = Mathf.Max(0.01f, optionCursorAdvanceSeconds);
        }
        louieSelectCursorRenderer.SetFrozen(false);
        louieSelectCursorRenderer.RefreshFrame();
    }

    private void ChangeSelectedBattleLouieAmount(int delta)
    {
        if (workingBattleLouieAmounts == null || workingBattleLouieAmounts.Length == 0)
            return;

        int index = GetLouieSelectCanonicalIndex(selectedLouieIndex);
        if (index < 0 || index >= workingBattleLouieAmounts.Length)
            return;

        int max = Mathf.Max(0, louieSelectMaxAmount);
        workingBattleLouieAmounts[index] = Mathf.Clamp(workingBattleLouieAmounts[index] + delta, 0, max);
        SaveCurrentBattleLouieAmounts();
    }

    private int GetWorkingBattleLouieAmount(int index)
    {
        int canonicalIndex = GetLouieSelectCanonicalIndex(index);
        if (workingBattleLouieAmounts == null || canonicalIndex < 0 || canonicalIndex >= workingBattleLouieAmounts.Length)
            return 0;

        return workingBattleLouieAmounts[canonicalIndex];
    }

    private void SaveCurrentBattleLouieAmounts()
    {
        if (workingBattleLouieAmounts == null)
            return;

        SaveSystem.SetBattleModeLouieAmounts(workingBattleLouieAmounts);
    }

    private IEnumerator OpenHandicapSelectMenu()
    {
        state = MenuState.HandicapSelect;
        handicapSelectCursorConfirmTimer = 0f;
        playerModes = SaveSystem.GetBattleModePlayerControlModes();
        selectedHandicapRow = Mathf.Clamp(selectedHandicapRow, 0, GameSession.MaxPlayerId - 1);
        selectedHandicapRow = GetNextVisibleHandicapRow(selectedHandicapRow, 1);
        selectedHandicapColumn = Mathf.Clamp(selectedHandicapColumn, 0, GetHandicapSelectColumnCount() - 1);
        workingBattleHandicap = SaveSystem.GetBattleModeHandicapForStage(SaveSystem.GetBattleModeStageIndex());

        if (specificSettingsRoot != null)
            specificSettingsRoot.gameObject.SetActive(false);

        EnsureHandicapSelectBuilt();
        ResetBackgroundSpriteSwap();
        ApplyCurrentBackgroundSprite(true);
        UpdatePromptTitle();
        UpdateHandicapSelectVisuals();

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

            if (input.GetDown(GameSession.MinPlayerId, PlayerAction.MoveLeft))
            {
                selectedHandicapColumn = WrapIndex(selectedHandicapColumn - 1, GetHandicapSelectColumnCount());
                PlaySfx(moveCursorSfx, moveCursorSfxVolume);
            }
            else if (input.GetDown(GameSession.MinPlayerId, PlayerAction.MoveRight))
            {
                selectedHandicapColumn = WrapIndex(selectedHandicapColumn + 1, GetHandicapSelectColumnCount());
                PlaySfx(moveCursorSfx, moveCursorSfxVolume);
            }
            else if (input.GetDown(GameSession.MinPlayerId, PlayerAction.MoveUp))
            {
                selectedHandicapRow = GetNextVisibleHandicapRow(selectedHandicapRow - 1, -1);
                PlaySfx(moveCursorSfx, moveCursorSfxVolume);
            }
            else if (input.GetDown(GameSession.MinPlayerId, PlayerAction.MoveDown))
            {
                selectedHandicapRow = GetNextVisibleHandicapRow(selectedHandicapRow + 1, 1);
                PlaySfx(moveCursorSfx, moveCursorSfxVolume);
            }
            else if (input.GetDown(GameSession.MinPlayerId, PlayerAction.ActionB))
            {
                PlaySfx(returnSfx, returnSfxVolume);
                done = true;
            }
            else if (input.GetDown(GameSession.MinPlayerId, PlayerAction.Start))
            {
                PlaySfx(confirmSfx, confirmSfxVolume);
                done = true;
            }
            else if (input.GetDown(GameSession.MinPlayerId, PlayerAction.ActionA))
            {
                if (ChangeSelectedHandicapValue(1))
                {
                    PlaySfx(confirmSfx, confirmSfxVolume);
                    UpdateHandicapSelectVisuals();
                    RestartOptionCursorAdvanceFeedback(handicapSelectCursorRenderer, ref handicapSelectCursorConfirmTimer);
                }
                else
                {
                    PlaySfx(deniedSfx, deniedSfxVolume);
                }
            }
            else if (input.GetDown(GameSession.MinPlayerId, PlayerAction.ActionC))
            {
                if (ChangeSelectedHandicapValue(-1))
                {
                    PlaySfx(confirmSfx, confirmSfxVolume);
                    UpdateHandicapSelectVisuals();
                    RestartOptionCursorAdvanceFeedback(handicapSelectCursorRenderer, ref handicapSelectCursorConfirmTimer);
                }
                else
                {
                    PlaySfx(deniedSfx, deniedSfxVolume);
                }
            }

            UpdateHandicapSelectVisuals();
            handicapSelectCursorConfirmTimer = Mathf.Max(0f, handicapSelectCursorConfirmTimer - Time.unscaledDeltaTime);
            yield return null;
        }

        SaveCurrentBattleHandicap();

        if (handicapSelectRoot != null)
            handicapSelectRoot.gameObject.SetActive(false);

        if (specificSettingsRoot != null)
            specificSettingsRoot.gameObject.SetActive(true);

        state = MenuState.SpecificSettings;
        UpdatePromptTitle();
        UpdateSpecificSettingsVisuals();
    }

    private void EnsureHandicapSelectBuilt()
    {
        if (handicapSelectRoot != null)
        {
            handicapSelectRoot.gameObject.SetActive(true);
            handicapSelectRoot.SetAsLastSibling();
            return;
        }

        Transform parent = GetMenuContentParent();
        GameObject rootGo = new("HandicapSelectRoot", typeof(RectTransform));
        rootGo.transform.SetParent(parent, false);
        handicapSelectRoot = rootGo.GetComponent<RectTransform>();
        handicapSelectRoot.anchorMin = new Vector2(0.5f, 0.5f);
        handicapSelectRoot.anchorMax = new Vector2(0.5f, 0.5f);
        handicapSelectRoot.pivot = new Vector2(0.5f, 0.5f);
        handicapSelectRoot.anchoredPosition = handicapSelectRootOffset;
        handicapSelectRoot.sizeDelta = Vector2.zero;
        handicapSelectRoot.localScale = Vector3.one * currentUiScale;
        handicapSelectRoot.SetAsLastSibling();

        handicapRows.Clear();
        for (int i = 0; i < GameSession.MaxPlayerId; i++)
            handicapRows.Add(CreateHandicapSelectRow(i));

        CreateHandicapSelectHintText();
        CreateHandicapSelectCursor();
    }

    private HandicapRowVisual CreateHandicapSelectRow(int rowIndex)
    {
        HandicapRowVisual row = new();
        GameObject rowGo = new($"HandicapRow_{rowIndex}", typeof(RectTransform));
        rowGo.transform.SetParent(handicapSelectRoot, false);
        row.root = rowGo.GetComponent<RectTransform>();
        row.root.anchorMin = new Vector2(0.5f, 0.5f);
        row.root.anchorMax = new Vector2(0.5f, 0.5f);
        row.root.pivot = new Vector2(0.5f, 0.5f);

        row.playerImage = CreateHandicapImage(row.root, $"HandicapPlayer_{rowIndex}", false);
        row.mountImage = CreateHandicapImage(row.root, $"HandicapMount_{rowIndex}", false);
        row.mountOutline = row.mountImage.gameObject.AddComponent<Outline>();
        row.mountOutline.effectColor = Color.black;
        row.mountOutline.effectDistance = new Vector2(1f, -1f);
        row.mountOutline.enabled = false;
        row.mountRenderer = row.mountImage.gameObject.AddComponent<AnimatedSpriteRenderer>();

        for (int i = 0; i < GetHandicapOptionColumnCount(); i++)
        {
            row.optionImages.Add(CreateHandicapImage(row.root, $"HandicapOption_{rowIndex}_{i}", true));
            row.optionTexts.Add(CreateHandicapText(row.root, $"HandicapValue_{rowIndex}_{i}"));
        }

        return row;
    }

    private Image CreateHandicapImage(Transform parent, string name, bool withOutline)
    {
        GameObject go = withOutline
            ? new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Outline))
            : new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.preserveAspect = true;
        image.raycastTarget = false;

        if (withOutline)
        {
            Outline outline = go.GetComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(1f, -1f);
        }

        return image;
    }

    private TextMeshProUGUI CreateHandicapText(Transform parent, string name)
    {
        GameObject go = new(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        ApplySpecificSettingsTextStyle(text);
        return text;
    }

    private void CreateHandicapSelectHintText()
    {
        GameObject go = new("HandicapSelectHints", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(handicapSelectRoot, false);

        handicapSelectHintText = go.GetComponent<TextMeshProUGUI>();
        handicapSelectHintText.raycastTarget = false;
        handicapSelectHintText.textWrappingMode = TextWrappingModes.NoWrap;
        handicapSelectHintText.overflowMode = TextOverflowModes.Overflow;
        handicapSelectHintText.text = "\u2190\u2192\u2191\u2193: Choose\nA/C: Change Number\nB: Back";
        handicapSelectHintText.text = "←→↑↓: Choose\nA/C: Change Number\nB: Back";

        handicapSelectHintText.text = "\u2190\u2192\u2191\u2193: Choose\nA/C: Change Number\nB: Back";
        ApplySpecificSettingsTextStyle(handicapSelectHintText);

        handicapSelectHintText.enableAutoSizing = false;
        handicapSelectHintText.fontSize = handicapSelectHintFontSize;
        handicapSelectHintText.lineSpacing = handicapSelectHintLineSpacing;
        handicapSelectHintText.alignment = TextAlignmentOptions.Center;

    }

    private void CreateHandicapSelectCursor()
    {
        AnimatedSpriteRenderer source = leftPanel != null ? leftPanel.CursorRenderer : null;
        GameObject cursorGo = source != null
            ? Instantiate(source.gameObject, handicapSelectRoot, false)
            : new GameObject("HandicapSelectCursor", typeof(RectTransform));

        cursorGo.name = "HandicapSelectCursor";
        handicapSelectCursorRenderer = cursorGo.GetComponent<AnimatedSpriteRenderer>();
        handicapSelectCursorRt = cursorGo.transform as RectTransform;
        if (handicapSelectCursorRt == null)
            handicapSelectCursorRt = cursorGo.AddComponent<RectTransform>();

        handicapSelectCursorRt.anchorMin = new Vector2(0.5f, 0.5f);
        handicapSelectCursorRt.anchorMax = new Vector2(0.5f, 0.5f);
        handicapSelectCursorRt.pivot = new Vector2(0.5f, 0.5f);
        handicapSelectCursorRt.sizeDelta = handicapSelectCursorSize;
        handicapSelectCursorRt.localScale = Vector3.one;

        if (handicapSelectCursorRenderer != null)
        {
            handicapSelectCursorRenderer.SetFrozen(false);
            handicapSelectCursorRenderer.frameOffsets = null;
            handicapSelectCursorRenderer.idle = true;
            handicapSelectCursorRenderer.loop = true;
            handicapSelectCursorRenderer.CurrentFrame = 0;
            handicapSelectCursorRenderer.RefreshFrame();
        }
    }

    private void UpdateHandicapSelectVisuals()
    {
        if (handicapSelectRoot == null || !handicapSelectRoot.gameObject.activeInHierarchy)
            return;

        EnsureWorkingBattleHandicap();
        teamPreviewTimer += Time.unscaledDeltaTime;
        handicapSelectRoot.anchoredPosition = handicapSelectRootOffset;
        handicapSelectRoot.localScale = Vector3.one * currentUiScale;

        for (int rowIndex = 0; rowIndex < handicapRows.Count; rowIndex++)
        {
            HandicapRowVisual row = handicapRows[rowIndex];
            if (row?.root == null)
                continue;

            bool visible = IsHandicapPlayerVisible(rowIndex);
            row.root.gameObject.SetActive(visible);
            if (!visible)
                continue;

            SaveData.BattleModeHandicapPlayerSave player = GetHandicapPlayer(rowIndex);
            row.root.sizeDelta = handicapSelectRowSize;
            row.root.anchoredPosition = GetHandicapRowPosition(rowIndex);

            RectTransform playerRt = row.playerImage.rectTransform;
            playerRt.anchorMin = new Vector2(0.5f, 0.5f);
            playerRt.anchorMax = new Vector2(0.5f, 0.5f);
            playerRt.pivot = new Vector2(0.5f, 0.5f);
            playerRt.anchoredPosition = handicapSelectPlayerOffset;
            playerRt.sizeDelta = GetHandicapPlayerSize();
            row.playerImage.sprite = GetHandicapPlayerSprite(rowIndex + 1);
            row.playerImage.color = Color.white;

            UpdateHandicapMountVisual(row, player);
            UpdateHandicapOptionVisuals(row, player);
        }

        if (handicapSelectHintText != null)
        {
            RectTransform hintRt = handicapSelectHintText.rectTransform;
            hintRt.anchorMin = new Vector2(0.5f, 0.5f);
            hintRt.anchorMax = new Vector2(0.5f, 0.5f);
            hintRt.pivot = new Vector2(0.5f, 0.5f);
            hintRt.anchoredPosition = handicapSelectHintOffset;
            hintRt.sizeDelta = handicapSelectHintSize;
            handicapSelectHintText.color = GetMusicSelectTextColor(false);

            ApplySpecificSettingsTextStyle(handicapSelectHintText);

            handicapSelectHintText.enableAutoSizing = false;
            handicapSelectHintText.fontSize = handicapSelectHintFontSize;
            handicapSelectHintText.lineSpacing = handicapSelectHintLineSpacing;
            handicapSelectHintText.alignment = TextAlignmentOptions.Center;
        }

        if (handicapSelectCursorRt != null)
        {
            selectedHandicapRow = GetNextVisibleHandicapRow(selectedHandicapRow, 1);
            handicapSelectCursorRt.gameObject.SetActive(handicapRows.Count > 0 && IsHandicapPlayerVisible(selectedHandicapRow));
            handicapSelectCursorRt.sizeDelta = handicapSelectCursorSize;
            handicapSelectCursorRt.anchoredPosition = GetHandicapCellPosition(selectedHandicapRow, selectedHandicapColumn) + handicapSelectCursorOffset;
            handicapSelectCursorRt.SetAsLastSibling();
            handicapSelectCursorRenderer?.SetExternalBaseLocalPosition(handicapSelectCursorRt.localPosition);
            UpdateHandicapSelectCursorAnimationState();
        }
    }

    private void UpdateHandicapMountVisual(HandicapRowVisual row, SaveData.BattleModeHandicapPlayerSave player)
    {
        MountedType mountedType = player != null ? (MountedType)player.mountedLouie : MountedType.None;
        int visualIndex = GetHandicapMountVisualIndex(mountedType) - 1;
        bool hasMount = mountedType != MountedType.None && visualIndex >= 0;
        int displayIndex = hasMount ? visualIndex : Mathf.Max(0, GetHandicapMountVisualIndex(MountedType.Blue) - 1);

        RectTransform mountRt = row.mountImage.rectTransform;
        mountRt.anchorMin = new Vector2(0.5f, 0.5f);
        mountRt.anchorMax = new Vector2(0.5f, 0.5f);
        mountRt.pivot = new Vector2(0.5f, 0.5f);
        mountRt.anchoredPosition = handicapSelectMountOffset;
        mountRt.sizeDelta = GetLouieSelectIconSize(displayIndex) * Mathf.Max(0.01f, handicapSelectMountSizeMultiplier);
        row.mountImage.enabled = true;
        row.mountImage.color = hasMount
            ? Color.white
            : new Color(0f, 0f, 0f, Mathf.Clamp01(handicapSelectEmptyMountAlpha));
        if (row.mountOutline != null)
            row.mountOutline.enabled = !hasMount;

        if (row.mountRenderer == null)
            return;

        if (displayIndex >= 0)
        {
            if (row.mountRenderer.animationSprite == null ||
                row.mountRenderer.animationSprite.Length == 0 ||
                row.mountRenderer.idleSprite == null ||
                row.mountedType != mountedType)
            {
                ConfigureLouieSelectRenderer(row.mountRenderer, displayIndex);
                row.mountedType = mountedType;
            }

            ApplyLouieSelectRendererTiming(row.mountRenderer, displayIndex);
            row.mountRenderer.SetExternalBaseLocalPosition(mountRt.localPosition);
            row.mountRenderer.idle = !hasMount;
            row.mountRenderer.loop = true;
            if (!hasMount)
                row.mountRenderer.RefreshFrame();
        }
        else
        {
            row.mountRenderer.animationSprite = null;
            row.mountRenderer.idleSprite = null;
            row.mountedType = MountedType.None;
            row.mountRenderer.RefreshFrame();
        }
    }

    private Sprite GetHandicapPlayerSprite(int playerId)
    {
        if (skinSelectMenu == null)
            return null;

        BomberSkin skin = PlayerPersistentStats.Get(playerId).Skin;
        int previewFrame = Mathf.FloorToInt(teamPreviewTimer / Mathf.Max(0.01f, handicapSelectPlayerFrameSeconds));
        return skinSelectMenu.GetBattleModeTeamPreviewSprite(skin, previewFrame);
    }

    private void UpdateHandicapOptionVisuals(HandicapRowVisual row, SaveData.BattleModeHandicapPlayerSave player)
    {
        for (int i = 0; i < row.optionImages.Count; i++)
        {
            Image image = row.optionImages[i];
            TextMeshProUGUI text = row.optionTexts[i];
            RectTransform imageRt = image.rectTransform;
            imageRt.anchorMin = new Vector2(0.5f, 0.5f);
            imageRt.anchorMax = new Vector2(0.5f, 0.5f);
            imageRt.pivot = new Vector2(0.5f, 0.5f);
            imageRt.anchoredPosition = GetHandicapOptionPosition(i);
            imageRt.sizeDelta = handicapSelectOptionIconSize;
            image.sprite = GetHandicapOptionSprite(i, player);
            image.color = IsHandicapOptionEnabled(i, player) ? Color.white : louieSelectDisabledTint;
            image.enabled = image.sprite != null;

            RectTransform textRt = text.rectTransform;
            textRt.anchorMin = new Vector2(0.5f, 0.5f);
            textRt.anchorMax = new Vector2(0.5f, 0.5f);
            textRt.pivot = new Vector2(0.5f, 0.5f);
            textRt.anchoredPosition = GetHandicapOptionPosition(i) + handicapSelectOptionNumberOffset;
            textRt.sizeDelta = handicapSelectOptionTextSize;
            text.text = GetHandicapOptionText(i, player);
            text.color = GetMusicSelectTextColor(IsHandicapOptionEnabled(i, player));
            text.fontSize = handicapSelectOptionFontSize;
            text.alignment = TextAlignmentOptions.Center;
            ApplySpecificSettingsTextStyle(text);
        }
    }

    private Sprite GetHandicapOptionSprite(int optionIndex, SaveData.BattleModeHandicapPlayerSave player)
    {
        return optionIndex switch
        {
            0 => GetItemIconSprite(ItemType.Heart),
            1 => GetItemIconSprite(ItemType.ExtraBomb),
            2 => GetItemIconSprite(ItemType.BlastRadius),
            3 => GetItemIconSprite(ItemType.SpeedIncrese),
            4 => GetHandicapBombTypeSprite(player),
            5 => GetHandicapMovementSprite(player),
            6 => GetItemIconSprite(ItemType.BombPunch),
            7 => GetItemIconSprite(ItemType.PowerGlove),
            8 => GetItemIconSprite(ItemType.FullFire),
            9 => GetItemIconSprite(ItemType.DestructiblePass),
            _ => null
        };
    }

    private Sprite GetHandicapBombTypeSprite(SaveData.BattleModeHandicapPlayerSave player)
    {
        BattleModeHandicapBombType bombType = player != null
            ? (BattleModeHandicapBombType)player.bombType
            : BattleModeHandicapBombType.Default;

        return bombType switch
        {
            BattleModeHandicapBombType.Power => GetItemIconSprite(ItemType.PowerBomb),
            BattleModeHandicapBombType.Rubber => GetItemIconSprite(ItemType.RubberBomb),
            BattleModeHandicapBombType.Pierce => GetItemIconSprite(ItemType.PierceBomb),
            BattleModeHandicapBombType.Control => GetItemIconSprite(ItemType.ControlBomb),
            BattleModeHandicapBombType.Magnet => GetItemIconSprite(ItemType.MagnetBomb),
            _ => GetItemIconSprite(ItemType.ExtraBomb)
        };
    }

    private Sprite GetHandicapMovementSprite(SaveData.BattleModeHandicapPlayerSave player)
    {
        BattleModeHandicapMovementAbility movement = player != null
            ? (BattleModeHandicapMovementAbility)player.movementAbility
            : BattleModeHandicapMovementAbility.None;

        return movement == BattleModeHandicapMovementAbility.BombPass
            ? GetItemIconSprite(ItemType.BombPass)
            : GetItemIconSprite(ItemType.BombKick);
    }

    private string GetHandicapOptionText(int optionIndex, SaveData.BattleModeHandicapPlayerSave player)
    {
        if (player == null)
            return string.Empty;

        return optionIndex switch
        {
            0 => player.life.ToString(),
            1 => player.bombAmount.ToString(),
            2 => player.blastRadius.ToString(),
            3 => player.speedLevel.ToString(),
            _ => string.Empty
        };
    }

    private static bool IsHandicapOptionEnabled(int optionIndex, SaveData.BattleModeHandicapPlayerSave player)
    {
        if (player == null)
            return false;

        return optionIndex switch
        {
            0 => player.life > 0,
            1 => true,
            2 => true,
            3 => true,
            4 => player.bombType != (int)BattleModeHandicapBombType.Default,
            5 => IsHandicapKickEnabled(player),
            6 => player.punchBomb,
            7 => player.powerGlove,
            8 => player.fullFire,
            9 => player.destructiblePass,
            _ => false
        };
    }

    private static bool IsHandicapKickEnabled(SaveData.BattleModeHandicapPlayerSave player)
    {
        return player != null && player.movementAbility != (int)BattleModeHandicapMovementAbility.None;
    }

    private bool ChangeSelectedHandicapValue(int delta)
    {
        EnsureWorkingBattleHandicap();
        SaveData.BattleModeHandicapPlayerSave player = GetHandicapPlayer(selectedHandicapRow);
        if (player == null || !IsHandicapPlayerVisible(selectedHandicapRow))
            return false;

        if (IsCurrentBattleModePowerZoneStage())
            player.mountedLouie = (int)MountedType.None;

        switch (selectedHandicapColumn)
        {
            case 0:
                if (IsCurrentBattleModePowerZoneStage())
                    return false;

                SetHandicapMountedLouie(player, GetHandicapMountVisualIndex((MountedType)player.mountedLouie) + delta);
                break;

            case 1:
                player.life = WrapValue(player.life + delta, 0, 9);
                break;

            case 2:
                player.bombAmount = WrapValue(player.bombAmount + delta, 1, PlayerPersistentStats.MaxBombAmount);
                break;

            case 3:
                player.blastRadius = WrapValue(player.blastRadius + delta, 1, PlayerPersistentStats.MaxExplosionRadius);
                break;

            case 4:
                player.speedLevel = WrapValue(player.speedLevel + delta, 1, PlayerPersistentStats.MaxSpeedUps + 1);
                break;

            case 5:
                player.bombType = WrapValue(player.bombType + delta, 0, 5);
                break;

            case 6:
                player.movementAbility = WrapValue(player.movementAbility + delta, 0, 2);
                break;

            case 7:
                player.punchBomb = !player.punchBomb;
                break;

            case 8:
                player.powerGlove = !player.powerGlove;
                break;

            case 9:
                player.fullFire = !player.fullFire;
                break;

            case 10:
                player.destructiblePass = !player.destructiblePass;
                break;
        }

        SaveCurrentBattleHandicap();
        return true;
    }

    private void SetHandicapMountedLouie(SaveData.BattleModeHandicapPlayerSave player, int visualIndexWithNone)
    {
        int count = GetLouieSelectEntryCount() + 1;
        int wrapped = WrapIndex(visualIndexWithNone, count);
        player.mountedLouie = wrapped == 0
            ? (int)MountedType.None
            : (int)GetLouieSelectMountType(wrapped - 1);
    }

    private int GetHandicapMountVisualIndex(MountedType type)
    {
        if (type == MountedType.None)
            return 0;

        for (int i = 0; i < GetLouieSelectEntryCount(); i++)
        {
            if (GetLouieSelectMountType(i) == type)
                return i + 1;
        }

        return 0;
    }

    private static bool IsCurrentBattleModePowerZoneStage()
    {
        int stageIndex = SaveSystem.GetBattleModeStageIndex();
        return stageIndex == 10 || stageIndex == 11;
    }

    private SaveData.BattleModeHandicapPlayerSave GetHandicapPlayer(int rowIndex)
    {
        EnsureWorkingBattleHandicap();
        if (workingBattleHandicap?.players == null || workingBattleHandicap.players.Length == 0)
            return null;

        return workingBattleHandicap.players[Mathf.Clamp(rowIndex, 0, workingBattleHandicap.players.Length - 1)];
    }

    private void EnsureWorkingBattleHandicap()
    {
        if (workingBattleHandicap?.players != null && workingBattleHandicap.players.Length == GameSession.MaxPlayerId)
            return;

        workingBattleHandicap = SaveSystem.GetBattleModeHandicapForStage(SaveSystem.GetBattleModeStageIndex());
    }

    private void SaveCurrentBattleHandicap()
    {
        if (workingBattleHandicap == null)
            return;

        SaveSystem.SetBattleModeHandicapForStage(SaveSystem.GetBattleModeStageIndex(), workingBattleHandicap);
        workingBattleHandicap = SaveSystem.GetBattleModeHandicapForStage(SaveSystem.GetBattleModeStageIndex());
    }

    private void UpdateHandicapSelectCursorAnimationState()
    {
        if (handicapSelectCursorRenderer == null)
            return;

        if (optionCursorAdvanceAnimating)
            return;

        bool confirming = handicapSelectCursorConfirmTimer > 0f;
        handicapSelectCursorRenderer.idle = !confirming;
        handicapSelectCursorRenderer.loop = !confirming;
        if (confirming)
        {
            handicapSelectCursorRenderer.useSequenceDuration = true;
            handicapSelectCursorRenderer.sequenceDuration = Mathf.Max(0.01f, optionCursorAdvanceSeconds);
        }
        handicapSelectCursorRenderer.SetFrozen(false);
        handicapSelectCursorRenderer.RefreshFrame();
    }

    private Vector2 GetHandicapRowPosition(int rowIndex)
    {
        float rowStep = handicapSelectRowSize.y + handicapSelectRowSpacing;
        float totalHeight = (GameSession.MaxPlayerId - 1) * rowStep;
        float y = (totalHeight * 0.5f) - (rowIndex * rowStep);
        return handicapSelectRowsOffset + new Vector2(0f, y);
    }

    private Vector2 GetHandicapPlayerSize()
    {
        return handicapSelectPlayerSize == Vector2.zero
            ? teamMemberSize
            : handicapSelectPlayerSize;
    }

    private Vector2 GetHandicapCellPosition(int rowIndex, int columnIndex)
    {
        Vector2 rowPosition = GetHandicapRowPosition(Mathf.Clamp(rowIndex, 0, GameSession.MaxPlayerId - 1));
        if (columnIndex <= 0)
            return rowPosition + handicapSelectMountOffset;

        return rowPosition + GetHandicapOptionPosition(columnIndex - 1);
    }

    private Vector2 GetHandicapOptionPosition(int optionIndex)
    {
        return handicapSelectOptionStartOffset + (handicapSelectOptionSpacing * optionIndex);
    }

    private bool IsHandicapPlayerVisible(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= GameSession.MaxPlayerId)
            return false;

        if (playerModes == null || playerModes.Length != GameSession.MaxPlayerId)
            playerModes = SaveSystem.GetBattleModePlayerControlModes();

        if (playerModes == null || rowIndex >= playerModes.Length)
            return rowIndex == 0;

        return playerModes[rowIndex] != BattleModePlayerControlMode.Off;
    }

    private int GetNextVisibleHandicapRow(int startRow, int direction)
    {
        int count = GameSession.MaxPlayerId;
        int step = direction < 0 ? -1 : 1;
        int row = WrapIndex(startRow, count);

        for (int i = 0; i < count; i++)
        {
            if (IsHandicapPlayerVisible(row))
                return row;

            row = WrapIndex(row + step, count);
        }

        return 0;
    }

    private static int GetHandicapOptionColumnCount()
    {
        return 10;
    }

    private static int GetHandicapSelectColumnCount()
    {
        return GetHandicapOptionColumnCount() + 1;
    }

    private static int WrapValue(int value, int min, int max)
    {
        if (max <= min)
            return min;

        if (value < min)
            return max;

        if (value > max)
            return min;

        return value;
    }

    private IEnumerator ConfirmSpecificSettingsStart()
    {
        specificStartConfirmed = true;
        battleStartBlinkTimer = 0f;
        battleStartVisible = true;
        if (specificCursorRt != null)
            specificConfirmedCursorPosition = specificCursorRt.anchoredPosition;
        StartSpecificSettingsCursorConfirmAnimation();

        GameMusicController.Instance?.StopMusic();
        PlaySfx(specificStartSfx != null ? specificStartSfx : confirmSfx, specificStartSfx != null ? specificStartSfxVolume : confirmSfxVolume);
        confirmed = true;

        if (specificStartWaitSeconds > 0f)
            yield return ProbeSpecificStartWait();

        yield return FadeOutRoutine(specificStartFadeSeconds);
        Hide();
        LoadScene(GetBattleStageSceneName(SaveSystem.GetBattleModeStageIndex()));
    }

    private IEnumerator ProbeSpecificStartWait()
    {
        float wait = Mathf.Max(0f, specificStartWaitSeconds);
        if (wait <= 0f)
            yield break;

        float elapsed = 0f;

        while (elapsed < wait)
        {
            elapsed += Time.unscaledDeltaTime;

            yield return null;
        }
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
        if (musicSelectRoot != null)
            musicSelectRoot.gameObject.SetActive(false);
        if (itemSelectRoot != null)
            itemSelectRoot.gameObject.SetActive(false);
        if (handicapSelectRoot != null)
            handicapSelectRoot.gameObject.SetActive(false);

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
            MenuState.ItemSelect => itemSelectPrompt,
            MenuState.LouieSelect => "SELECT LOUIES",
            MenuState.HandicapSelect => handicapSelectPrompt,
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

        SetFadeAlpha(0f);
        fadeImage.gameObject.SetActive(true);
        BringFadeImageToFront();
        SetFadeAlpha(0f);
        Canvas.ForceUpdateCanvases();
        yield return null;

        float duration = Mathf.Max(0.001f, durationSeconds);
        float t = 0f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            BringFadeImageToFront();
            float progress = Mathf.Clamp01(t / duration);
            SetFadeAlpha(progress);
            yield return null;
        }

        BringFadeImageToFront();
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
            MenuState.ItemSelect => GetValidSpriteCount(itemSelectBackgrounds?.sprites) > 0
                ? itemSelectBackgrounds?.sprites
                : specificSettingsBackgrounds?.sprites,
            MenuState.LouieSelect => GetValidSpriteCount(louieSelectBackgrounds?.sprites) > 0
                ? louieSelectBackgrounds?.sprites
                : specificSettingsBackgrounds?.sprites,
            MenuState.HandicapSelect => GetValidSpriteCount(handicapSelectBackgrounds?.sprites) > 0
                ? handicapSelectBackgrounds?.sprites
                : specificSettingsBackgrounds?.sprites,
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
        if (itemSelectRoot != null)
            itemSelectRoot.localScale = Vector3.one * currentUiScale;
        if (louieSelectRoot != null)
            louieSelectRoot.localScale = Vector3.one * currentUiScale;
        if (handicapSelectRoot != null)
            handicapSelectRoot.localScale = Vector3.one * currentUiScale;
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

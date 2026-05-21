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
        RuleConfig = 4
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

    [Header("Debug")]
    [SerializeField] private bool logTeamSelectLayoutDebug = true;
    [SerializeField, Min(1)] private int teamSelectLayoutDebugFrames = 12;
    [SerializeField, Min(0.05f)] private float teamSelectLayoutDebugInterval = 0.25f;
    [SerializeField] private bool logRuleConfigLayoutDebug;
    [SerializeField, Min(1)] private int ruleConfigLayoutDebugFrames = 12;
    [SerializeField, Min(0.05f)] private float ruleConfigLayoutDebugInterval = 0.25f;

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
    private int teamLayoutDebugFramesRemaining;
    private float nextTeamLayoutDebugTime;
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
    private int ruleLayoutDebugFramesRemaining;
    private float nextRuleLayoutDebugTime;
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
            LogOptionCursorPosition("PlayerSelect.ActionB.BeforeBuildMatchMode");
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
        LogOptionCursorPosition("ConfirmMatchMode.BeforeWait");

        float wait = Mathf.Max(0f, confirmFeedbackSeconds);
        if (wait > 0f)
            yield return new WaitForSecondsRealtime(wait);

        cursorConfirmVisual = false;
        LogOptionCursorPosition("ConfirmMatchMode.BeforeBuildPlayerSelect");
        yield return BuildPlayerSelectMenuAfterTransition("ConfirmMatchMode");
    }

    private IEnumerator ConfirmPlayerSelection()
    {
        PlaySfx(confirmSfx, confirmSfxVolume);
        SaveSystem.SetBattleModePlayerControlModes(playerModes);

        cursorConfirmVisual = true;
        UpdateOptionVisuals();
        LogOptionCursorPosition("ConfirmPlayerSelect.BeforeWait");

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
        LogOptionCursorPosition("BuildMatchModeMenu.AfterPosition");
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
        LogOptionCursorPosition("BuildPlayerSelectMenu.AfterPosition");
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

        LogOptionCursorPosition($"{context}.AfterBuildSuppressed");

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

        LogOptionCursorPosition($"{context}.AfterRevealCursor");
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
            LogOptionCursorPosition("OpenSkinSelectMenu.BeforeHideOptions");

            ApplyBattleModeActivePlayerIds(includeComPlayers: false);

            if (teamSelectRoot != null)
                teamSelectRoot.gameObject.SetActive(false);
            if (ruleConfigRoot != null)
                ruleConfigRoot.gameObject.SetActive(false);

            if (leftPanel != null)
            {
                leftPanel.HideCursor();
                leftPanel.gameObject.SetActive(false);
            }

            LogOptionCursorPosition("OpenSkinSelectMenu.AfterHideOptions");

            UpdatePromptTitle();
            ApplyCurrentBackgroundSprite(true);

            yield return skinSelectMenu.SelectSkinRoutine();

            ApplyBattleModeActivePlayerIds(includeComPlayers: true);
            RestoreRootAfterEmbeddedSkinSelect();

            if (skinSelectMenu.ReturnToTitleRequested)
                break;

            bool reopenSkinSelect = false;
            bool rulesConfirmed = false;
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
                continue;

            if (rulesConfirmed &&
                loadNextSceneAfterSelection &&
                !string.IsNullOrWhiteSpace(nextSceneName))
            {
                confirmed = true;
                Hide();
                LoadScene(nextSceneName);
                yield break;
            }

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
        BeginTeamLayoutDebug();

        Canvas.ForceUpdateCanvases();
        ApplyDynamicScaleIfNeeded(true);
        LogTeamSelectLayout("OpenTeamSelectMenu.AfterInitialLayout", force: true);

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

        if (leftPanel != null)
        {
            leftPanel.SetCursorVisibilitySuppressed(false);
            leftPanel.gameObject.SetActive(true);
        }

        BuildPlayerSelectMenu();
        LogOptionCursorPosition("OpenSkinSelectMenu.AfterReturnBuildPlayerSelect");

        if (leftPanel != null)
        {
            leftPanel.SetCursorVisibilitySuppressed(true);
            UpdateOptionVisuals();
            LogOptionCursorPosition("OpenSkinSelectMenu.AfterReturnSuppressCursor");
        }

        Canvas.ForceUpdateCanvases();
        ApplyDynamicScaleIfNeeded(true);

        if (leftPanel != null)
        {
            UpdateOptionVisuals();
            leftPanel.SetCursorVisibilitySuppressed(false);
            UpdateOptionVisuals();
            LogOptionCursorPosition("OpenSkinSelectMenu.AfterReturnRevealCursor");
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

        LogTeamSelectLayout(force ? "UpdateTeamSelectVisuals.Forced" : "UpdateTeamSelectVisuals.Tick", force);
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

    private void BeginTeamLayoutDebug()
    {
        teamLayoutDebugFramesRemaining = logTeamSelectLayoutDebug ? Mathf.Max(1, teamSelectLayoutDebugFrames) : 0;
        nextTeamLayoutDebugTime = 0f;
    }

    private void LogTeamSelectLayout(string context, bool force)
    {
        if (!logTeamSelectLayoutDebug)
            return;

        if (!force)
        {
            if (teamLayoutDebugFramesRemaining <= 0 || Time.unscaledTime < nextTeamLayoutDebugTime)
                return;
        }

        if (teamLayoutDebugFramesRemaining > 0)
            teamLayoutDebugFramesRemaining--;

        nextTeamLayoutDebugTime = Time.unscaledTime + Mathf.Max(0.05f, teamSelectLayoutDebugInterval);

        Debug.Log(
            $"[BattleModeMenu/TeamLayout] frame={Time.frameCount} time={Time.unscaledTime:0.000} " +
            $"context={context} scale={currentUiScale:0.###} currentIndex={currentTeamPlayerIndex} " +
            $"selectedTeam={TeamIds[Mathf.Clamp(selectedTeamIndex, 0, TeamIds.Length - 1)]} " +
            $"root={FormatRectInfo(teamSelectRoot)} cursor={FormatRectInfo(teamCursorRt)}");

        for (int r = 0; r < teamRows.Count; r++)
        {
            TeamRowVisual row = teamRows[r];
            if (row == null)
                continue;

            string labelInfo = row.label != null ? FormatRectInfo(row.label.rectTransform) : "NULL";
            Debug.Log(
                $"[BattleModeMenu/TeamLayout] row={row.teamId} rect={FormatRectInfo(row.root)} " +
                $"label={labelInfo} members={row.members.Count}");

            for (int m = 0; m < row.members.Count; m++)
            {
                Image member = row.members[m];
                if (member == null || !member.gameObject.activeInHierarchy)
                    continue;

                Debug.Log(
                    $"[BattleModeMenu/TeamLayout] row={row.teamId} memberIndex={m} " +
                    $"rect={FormatRectInfo(member.rectTransform)} sprite={(member.sprite != null ? member.sprite.name : "NULL")} " +
                    $"alpha={member.color.a:0.###}");
            }
        }
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

        SyncRuleConfigLayoutFromPlayerSelect();
        EnsureRuleConfigBuilt();
        LoadRuleConfigFromSave();
        selectedRuleIndex = Mathf.Clamp(selectedRuleIndex, 0, ruleRows.Count - 1);
        ResetBackgroundSpriteSwap();
        ApplyCurrentBackgroundSprite(true);
        UpdatePromptTitle();
        UpdateRuleConfigVisuals();
        BeginRuleLayoutDebug();

        Canvas.ForceUpdateCanvases();
        ApplyDynamicScaleIfNeeded(true);
        LogRuleConfigLayout("OpenRuleConfigMenu.AfterInitialLayout", force: true);

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

        LogRuleConfigLayout("UpdateRuleConfigVisuals", force: false);
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

    private void BeginRuleLayoutDebug()
    {
        ruleLayoutDebugFramesRemaining = logRuleConfigLayoutDebug ? Mathf.Max(1, ruleConfigLayoutDebugFrames) : 0;
        nextRuleLayoutDebugTime = 0f;
    }

    private void LogRuleConfigLayout(string context, bool force)
    {
        if (!logRuleConfigLayoutDebug)
            return;

        if (!force)
        {
            if (ruleLayoutDebugFramesRemaining <= 0 || Time.unscaledTime < nextRuleLayoutDebugTime)
                return;
        }

        if (ruleLayoutDebugFramesRemaining > 0)
            ruleLayoutDebugFramesRemaining--;

        nextRuleLayoutDebugTime = Time.unscaledTime + Mathf.Max(0.05f, ruleConfigLayoutDebugInterval);

        Debug.Log(
            $"[BattleModeMenu/RuleLayout] frame={Time.frameCount} time={Time.unscaledTime:0.000} " +
            $"context={context} scale={currentUiScale:0.###} selectedRuleIndex={selectedRuleIndex} " +
            $"syncLayoutWithPlayerSelect={syncRuleConfigLayoutWithPlayerSelect} syncFontWithPlayerSelect={syncRuleConfigFontWithPlayerSelect} " +
            $"playerSelectLayout(font={playerSelectLayout.fontSize}, itemHeight={playerSelectLayout.optionItemHeight:0.##}, spacing={FormatVec2(playerSelectLayout.optionSpacing)}, " +
            $"cursorOffset={FormatVec2(playerSelectLayout.cursorOffset)}, cursorMin={playerSelectLayout.minCursorSize:0.##}, cursorMult={playerSelectLayout.cursorHeightMultiplier:0.##}) " +
            $"ruleConfig(rowSize={FormatVec2(ruleRowSize)}, spacing={ruleRowSpacing:0.##}, font={ruleFontSize}, " +
            $"labelOffset={FormatVec2(ruleLabelOffset)}, valueOffset={FormatVec2(ruleValueOffset)}, cursorOffset={FormatVec2(ruleCursorOffset)}, cursorSize={FormatVec2(ruleCursorSize)}) " +
            $"root={FormatRectInfo(ruleConfigRoot)} cursor={FormatRectInfo(ruleCursorRt)}");

        for (int i = 0; i < ruleRows.Count; i++)
        {
            RuleRowVisual row = ruleRows[i];
            if (row == null)
                continue;

            Debug.Log(
                $"[BattleModeMenu/RuleLayout] rowIndex={i} selected={i == selectedRuleIndex} row={FormatRectInfo(row.root)} " +
                $"label={FormatTextInfo(row.label)} value={FormatTextInfo(row.value)}");
        }
    }

    private static string FormatRectInfo(RectTransform rt)
    {
        if (rt == null)
            return "NULL";

        return $"anchored={FormatVec2(rt.anchoredPosition)} size={FormatVec2(rt.sizeDelta)} local={FormatVec3(rt.localPosition)} world={FormatVec3(rt.position)} active={rt.gameObject.activeInHierarchy}";
    }

    private static string FormatTextInfo(TextMeshProUGUI text)
    {
        if (text == null)
            return "NULL";

        string fontName = text.font != null ? text.font.name : "NULL";
        string materialName = text.fontMaterial != null ? text.fontMaterial.name : "NULL";
        return $"rect={FormatRectInfo(text.rectTransform)} text=\"{text.text}\" fontSize={text.fontSize:0.##} style={text.fontStyle} alignment={text.alignment} font={fontName} material={materialName} color={text.color}";
    }

    private void RefreshPlayerSelectEntries()
    {
        BuildPlayerEntries();

        if (leftPanel != null)
            leftPanel.UpdateEntryTexts(playerEntries);

        UpdateOptionVisuals();
        LogOptionCursorPosition("RefreshPlayerSelectEntries.AfterPosition");
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

    private void LogOptionCursorPosition(string context)
    {
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

    private static string FormatVec2(Vector2 value)
    {
        return $"({value.x:0.##},{value.y:0.##})";
    }

    private static string FormatVec3(Vector3 value)
    {
        return $"({value.x:0.##},{value.y:0.##},{value.z:0.##})";
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
            SetFadeAlpha(Mathf.Clamp01(t / duration));
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

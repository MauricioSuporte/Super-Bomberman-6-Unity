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
        SkinSelect = 2
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
    [SerializeField, Min(0.01f)] private float backgroundSwapInterval = 2f;
    [SerializeField] private bool backgroundSwapLoop = true;

    [Header("Player Select Colors")]
    [SerializeField] private string playerLabelHex = "#00FF3C";
    [SerializeField] private string manHex = "#00FF3C";
    [SerializeField] private string comHex = "#FF3030";
    [SerializeField] private string offHex = "#2F90FF";
    [SerializeField, Min(1)] private int playerModeColumnSpaces = 6;

    [Header("Debug")]
    [SerializeField] private bool logCursorPositionDebug = true;

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

    private const int PlayerActionCount = (int)PlayerAction.ActionR + 1;

    private readonly List<string> matchModeEntries = new()
    {
        "Single Match",
        "Tag Match"
    };

    private readonly List<string> playerEntries = new();
    private readonly List<int> battleEnabledPlayerIds = new(GameSession.MaxPlayerId);
    private readonly List<int> battleHumanPlayerIds = new(GameSession.MaxPlayerId);

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
        state = MenuState.SkinSelect;
        menuActive = false;
        cursorConfirmVisual = false;
        LogOptionCursorPosition("OpenSkinSelectMenu.BeforeHideOptions");

        ApplyBattleModeActivePlayerIds(includeComPlayers: false);

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

        if (!skinSelectMenu.ReturnToTitleRequested &&
            loadNextSceneAfterSelection &&
            !string.IsNullOrWhiteSpace(nextSceneName))
        {
            confirmed = true;
            Hide();
            LoadScene(nextSceneName);
            yield break;
        }

        if (root != null)
            root.SetActive(true);

        if (fadeImage != null)
        {
            fadeImage.gameObject.SetActive(false);
            SetFadeAlpha(0f);
        }

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
        yield return null;
        Canvas.ForceUpdateCanvases();
        ApplyDynamicScaleIfNeeded(true);

        if (leftPanel != null)
        {
            UpdateOptionVisuals();
            leftPanel.SetCursorVisibilitySuppressed(false);
            UpdateOptionVisuals();
            LogOptionCursorPosition("OpenSkinSelectMenu.AfterReturnRevealCursor");
        }

        PlayerInputManager input = PlayerInputManager.Instance;
        while (input != null && HasAnyRelevantHeldInput(input, out _, out _))
            yield return null;

        yield return null;
        CapturePreviousHeldInputs(input);
        menuActive = true;
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
        if (!logCursorPositionDebug || leftPanel == null)
            return;

        if (!leftPanel.TryGetCursorDebugInfo(
                out bool active,
                out Vector3 localPosition,
                out Vector2 anchoredPosition,
                out Vector3 worldPosition,
                out Vector2 sizeDelta,
                out string parentName))
        {
            Debug.Log(
                $"[BattleModeMenu/CursorPosition] frame={Time.frameCount} time={Time.unscaledTime:0.000} " +
                $"context={context} state={state} selectedIndex={selectedIndex} selectedPlayerIndex={selectedPlayerIndex} cursor=NULL",
                this);
            return;
        }

        Debug.Log(
            $"[BattleModeMenu/CursorPosition] frame={Time.frameCount} time={Time.unscaledTime:0.000} " +
            $"context={context} state={state} active={active} selectedIndex={selectedIndex} selectedPlayerIndex={selectedPlayerIndex} " +
            $"parent={parentName} local={FormatVec3(localPosition)} anchored={FormatVec2(anchoredPosition)} " +
            $"world={FormatVec3(worldPosition)} size={FormatVec2(sizeDelta)}",
            this);
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

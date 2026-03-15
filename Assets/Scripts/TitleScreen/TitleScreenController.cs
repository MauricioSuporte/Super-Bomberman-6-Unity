using System.Collections;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TitleScreenController : MonoBehaviour
{
    const string LOG = "[TitleScreenLayout]";

    [Header("Debug (Surgical Logs)")]
    [SerializeField] bool enableSurgicalLogs = true;

    readonly Vector3[] _menuWorldCorners = new Vector3[4];
    readonly Vector3[] _refWorldCorners = new Vector3[4];
    readonly Vector3[] _layoutWorldCorners = new Vector3[4];

    [Header("Menu Box (BASE @ designUpscale)")]
    [SerializeField] Vector2 menuBoxSize = new(520f, 260f);

    Coroutine _stabilizedRefreshRoutine;

    int _lastScreenWidth = -1;
    int _lastScreenHeight = -1;
    Vector2 _lastReferenceRectSize = new(-1f, -1f);

    [Header("Menu SFX")]
    public AudioClip moveOptionSfx;
    [Range(0f, 1f)] public float moveOptionVolume = 1f;

    public AudioClip selectOptionSfx;
    [Range(0f, 1f)] public float selectOptionVolume = 1f;

    [Header("Back SFX")]
    [SerializeField] AudioClip backOptionSfx;
    [SerializeField, Range(0f, 1f)] float backOptionVolume = 1f;

    [Header("Denied SFX")]
    [SerializeField] AudioClip deniedOptionSfx;
    [SerializeField, Range(0f, 1f)] float deniedOptionVolume = 1f;

    [Header("Reset Save SFX")]
    [SerializeField] AudioClip resetSaveCompletedSfx;
    [SerializeField, Range(0f, 1f)] float resetSaveCompletedVolume = 1f;

    [Header("Reset Save Message")]
    [SerializeField] string resetSaveCompletedMessage = "SAVE DATA ERASED";
    [SerializeField] float resetSaveCompletedShowSeconds = 2f;

    [Header("UI / Title")]
    public RawImage titleScreenRawImage;

    [Tooltip("Sprite 256x224 para a tela de título (pixel perfect).")]
    [SerializeField] Sprite titleScreenSprite;

    [Header("Menu Text (TMP)")]
    public TMP_Text menuText;

    [Header("Reference Frame (SafeFrame4x3)")]
    [SerializeField] RectTransform referenceRect;

    [Header("Layout Root (use SafeFrame4x3)")]
    [SerializeField] RectTransform layoutRoot;

    [Header("Menu Layout (BASE @ designUpscale)")]
    [SerializeField] Vector2 menuAnchoredPos = new(-91.2f, 60f);
    [SerializeField] int menuFontSize = 46;

    [Header("Extra Offsets (BASE @ designUpscale)")]
    [SerializeField] float menuExtraYOffset = -180f;
    [SerializeField] float pushStartExtraYOffset = 0f;

    [Header("Menu Centering")]
    [SerializeField] bool forceMenuCenterX = true;

    [Header("Dynamic Scale (relative ao layoutRoot)")]
    [SerializeField] bool dynamicScale = true;
    [SerializeField] int referenceWidth = 256;
    [SerializeField] int referenceHeight = 224;
    [SerializeField, Min(1)] int designUpscale = 4;
    [SerializeField, Min(0.01f)] float extraScaleMultiplier = 1f;
    [SerializeField, Min(0.01f)] float minScale = 0.5f;
    [SerializeField, Min(0.01f)] float maxScale = 10f;

    [Header("Text Style (SB5-like)")]
    [SerializeField] bool forceBold = true;

    [Header("Outline (TMP SDF)")]
    [SerializeField] Color outlineColor = Color.black;
    [SerializeField, Range(0f, 1f)] float outlineWidth = 0.35f;
    [SerializeField, Range(0f, 1f)] float outlineSoftness = 0.05f;

    [Header("Face Thickness (TMP SDF)")]
    [SerializeField, Range(-1f, 1f)] float faceDilate = 0.18f;
    [SerializeField, Range(0f, 1f)] float faceSoftness = 0.02f;

    [Header("Underlay (Shadow)")]
    [SerializeField] bool enableUnderlay = false;
    [SerializeField] Color underlayColor = new(0f, 0f, 0f, 1f);
    [SerializeField, Range(-1f, 1f)] float underlayDilate = 0.05f;
    [SerializeField, Range(0f, 1f)] float underlaySoftness = 0.2f;
    [SerializeField, Range(-2f, 2f)] float underlayOffsetX = 0f;
    [SerializeField, Range(-2f, 2f)] float underlayOffsetY = -0.2f;

    [Header("Video Settings")]
    [SerializeField] bool allowVideoMenu = true;
    [SerializeField] bool defaultFullscreen = true;
    [SerializeField] int defaultWindowSizeMultiplier = 4;
    [SerializeField] int[] windowSizeMultipliers = new[] { 2, 3, 4, 5, 6, 7, 8 };

    [Header("Video Values (Separate TMP)")]
    [SerializeField] TextMeshProUGUI videoValuesText;
    [SerializeField] float videoValuesRightPadding = 100f;

    [Header("Audio")]
    public AudioClip titleMusic;
    [Range(0f, 1f)] public float titleMusicVolume = 1f;

    [Header("Exit")]
    public float exitDelayRealtime = 1f;

    [Header("Start Game Timing")]
    [SerializeField] float startGameFadeOutDuration = 0.25f;

    [Header("Cursor")]
    public AnimatedSpriteRenderer cursorRenderer;
    [SerializeField] Vector2 cursorOffset = new(-28f, 5f);

    [Header("Cursor Scaling")]
    [SerializeField] bool scaleCursorWithUi = true;

    [Header("Push Start (TMP)")]
    [SerializeField] TextMeshProUGUI pushStartText;
    [SerializeField] string pushStartLabel = "PUSH START BUTTON";
    [SerializeField] string pushStartHex = "#FFA621";
    [SerializeField] int pushStartFontSize = 46;
    [SerializeField] float pushStartBlinkInterval = 1f;
    [SerializeField] float pushStartYOffset = 18f;

    [Header("Footer Message (TMP)")]
    [SerializeField] TextMeshProUGUI footerText;
    [SerializeField] int footerFontSize = 36;
    [SerializeField] float footerOffsetFromLastLineY = -40f;
    [SerializeField] float footerShowSeconds = 2f;

    [Header("Boss Rush Lock")]
    [SerializeField] bool forceBossRushUnlocked = true;
    [SerializeField, Range(0.05f, 1f)] float bossRushLockedAlpha = 0.35f;
    [SerializeField] string bossRushLockedMessage = "UNLOCKED BY CLEARING ALL STAGES";
    [SerializeField] string bossRushLockedMessageHex = "#FF3B30";
    [SerializeField] TextMeshProUGUI bossRushLockedText;
    [SerializeField] int bossRushLockedFontSize = 34;
    [SerializeField] float bossRushLockedBottomMargin = 30f;
    [SerializeField] float bossRushLockedShowSeconds = 2f;

    [Header("Controls Scene")]
    [SerializeField] string controlsSceneName = "ControlsMenu";

    public bool ControlsRequested { get; private set; }
    public bool Running { get; private set; }
    public bool NormalGameRequested { get; private set; }
    public bool BossRushRequested { get; private set; }
    public bool ExitRequested { get; private set; }

    bool bossRushInspectorDefaultUnlocked;
    public bool BossRushInspectorOverrideUnlocked => bossRushInspectorDefaultUnlocked;

    enum MenuMode
    {
        Main = 0,
        PlayerCount = 1,
        Options = 2,
        Video = 3,
        ResetSaveConfirm = 4
    }

    enum StartFlowMode
    {
        None = 0,
        Normal = 1,
        BossRush = 2
    }

    MenuMode menuMode = MenuMode.Main;
    StartFlowMode pendingStartFlow = StartFlowMode.None;

    const int MAIN_IDX_NORMAL = 0;
    const int MAIN_IDX_BOSS_RUSH = 1;
    const int MAIN_IDX_OPTIONS = 2;

    const int OPTIONS_IDX_CONTROLS = 0;
    const int OPTIONS_IDX_VIDEO = 1;
    const int OPTIONS_IDX_RESET_SAVE = 2;

    const int VIDEO_IDX_FULLSCREEN = 0;
    const int VIDEO_IDX_WINDOWSIZE = 1;

    const int RESET_SAVE_IDX_CANCEL = 0;
    const int RESET_SAVE_IDX_CONFIRM = 1;
    const int RESET_SAVE_SELECTABLE_LINE_START = 6;

    int menuIndex;
    bool locked;
    bool ignoreStartKeyUntilRelease;
    bool bootedSession;

    Material runtimeMenuMat;
    RectTransform menuRect;

    Coroutine pushStartRoutine;
    bool pushStartVisible = true;
    RectTransform pushStartRect;

    Coroutine footerRoutine;
    RectTransform footerRect;

    Coroutine bossRushLockedRoutine;
    RectTransform bossRushLockedRect;

    RectTransform videoValuesRect;

    Vector3 _cursorBaseLocalScale = Vector3.one;
    bool _cursorBaseScaleCaptured;

    bool _videoFullscreen;
    int _videoWindowMult;
    int _videoWindowMultWindowed;

    const string PREF_FULLSCREEN = "ts_video_fullscreen";
    const string PREF_WINMULT = "ts_video_window_mult";

    Coroutine _postResolutionRefreshRoutine;
    Coroutine _fullscreenWatchdog;

    float _currentUiScale = 1f;

    int ScaledFont(int baseSize) => Mathf.Clamp(Mathf.RoundToInt(baseSize * _currentUiScale), 10, 500);
    float ScaledFloat(float baseValue) => baseValue * _currentUiScale;
    Vector2 ScaledVec(Vector2 v) => v * _currentUiScale;

    int MenuFontSizeScaled => ScaledFont(menuFontSize);
    int PushStartFontSizeScaled => ScaledFont(pushStartFontSize);
    float PushStartYOffsetScaled => ScaledFloat(pushStartYOffset);
    Vector2 MenuAnchoredPosScaled => ScaledVec(menuAnchoredPos);
    Vector2 CursorOffsetScaled => ScaledVec(cursorOffset);
    int FooterFontSizeScaled => ScaledFont(footerFontSize);
    float FooterOffsetFromLastLineYScaled => ScaledFloat(footerOffsetFromLastLineY);
    int BossRushLockedFontSizeScaled => ScaledFont(bossRushLockedFontSize);
    float BossRushLockedBottomMarginScaled => ScaledFloat(bossRushLockedBottomMargin);
    float MenuExtraYOffsetScaled => ScaledFloat(menuExtraYOffset);
    float PushStartExtraYOffsetScaled => ScaledFloat(pushStartExtraYOffset);

    public void SetBossRushUnlocked(bool unlocked)
    {
        forceBossRushUnlocked = unlocked;

        if (menuText != null && menuText.gameObject.activeInHierarchy)
            RefreshMenuText();
    }

    void Awake()
    {
        bossRushInspectorDefaultUnlocked = forceBossRushUnlocked;

        if (titleScreenRawImage == null)
            Debug.LogWarning($"{LOG} titleScreenRawImage não foi atribuído no Inspector.", this);

        if (menuText != null)
            menuRect = menuText.rectTransform;

        if (referenceRect == null)
            referenceRect = ResolveReferenceRect();

        if (layoutRoot == null)
            layoutRoot = ResolveLayoutRoot();

        if (cursorRenderer != null)
        {
            RectTransform root = GetEffectiveLayoutRoot();
            if (root != null)
                cursorRenderer.transform.SetParent(root, false);

            _cursorBaseLocalScale = cursorRenderer.transform.localScale;
            _cursorBaseScaleCaptured = true;
        }

        LoadVideoPrefs();
        ApplyVideoSettingsImmediate();

        EnsurePushStartText();
        EnsureFooterText();
        EnsureBossRushLockedText();
        EnsureVideoValuesText();

        ForceHide();
    }

    void OnEnable()
    {
        RequestStabilizedLayoutRefresh("OnEnable");
    }

    void Start()
    {
        RequestStabilizedLayoutRefresh("Start");
    }

    void OnDestroy()
    {
        if (runtimeMenuMat != null)
            Destroy(runtimeMenuMat);
    }

    RectTransform ResolveReferenceRect()
    {
        if (referenceRect != null)
            return referenceRect;

        Canvas canvas = GetRootCanvas();
        if (canvas != null)
        {
            Transform t = canvas.transform.Find("SafeFrame4x3");
            if (t is RectTransform rt)
                return rt;
        }

        if (menuText != null && menuText.transform.parent is RectTransform p)
            return p;

        return transform as RectTransform;
    }

    RectTransform ResolveLayoutRoot()
    {
        if (layoutRoot != null)
            return layoutRoot;

        if (referenceRect == null)
            referenceRect = ResolveReferenceRect();

        if (referenceRect != null)
            return referenceRect;

        Canvas canvas = GetRootCanvas();
        if (canvas != null)
            return canvas.transform as RectTransform;

        return transform as RectTransform;
    }

    Canvas GetRootCanvas()
    {
        if (menuText != null && menuText.canvas != null)
            return menuText.canvas;

        return GetComponentInParent<Canvas>();
    }

    RectTransform GetEffectiveLayoutRoot()
    {
        if (layoutRoot == null)
            layoutRoot = ResolveLayoutRoot();

        return layoutRoot;
    }

    void LoadVideoPrefs()
    {
        _videoFullscreen = PlayerPrefs.GetInt(PREF_FULLSCREEN, defaultFullscreen ? 1 : 0) == 1;

        int fallback = defaultWindowSizeMultiplier;
        if (windowSizeMultipliers != null && windowSizeMultipliers.Length > 0)
        {
            int fallbackIdx = IndexOf(windowSizeMultipliers, defaultWindowSizeMultiplier);
            if (fallbackIdx < 0) fallbackIdx = 0;
            fallback = windowSizeMultipliers[Mathf.Clamp(fallbackIdx, 0, windowSizeMultipliers.Length - 1)];
        }

        _videoWindowMultWindowed = PlayerPrefs.GetInt(PREF_WINMULT, fallback);

        if (windowSizeMultipliers != null && windowSizeMultipliers.Length > 0)
        {
            int idx = IndexOf(windowSizeMultipliers, _videoWindowMultWindowed);
            if (idx < 0)
                _videoWindowMultWindowed = windowSizeMultipliers[0];
        }
        else
        {
            _videoWindowMultWindowed = Mathf.Max(1, _videoWindowMultWindowed);
        }

        _videoWindowMult = _videoFullscreen
            ? GetBestWindowMultiplierForCurrentMonitor()
            : _videoWindowMultWindowed;
    }

    void SaveVideoPrefs()
    {
        PlayerPrefs.SetInt(PREF_FULLSCREEN, _videoFullscreen ? 1 : 0);
        PlayerPrefs.SetInt(PREF_WINMULT, Mathf.Max(1, _videoWindowMultWindowed));
        PlayerPrefs.Save();
    }

    int GetBestWindowMultiplierForCurrentMonitor()
    {
        int rw = Mathf.Max(1, referenceWidth);
        int rh = Mathf.Max(1, referenceHeight);

        Resolution res = Screen.currentResolution;
        float sx = res.width / (float)rw;
        float sy = res.height / (float)rh;

        int target = Mathf.Max(1, Mathf.RoundToInt(Mathf.Min(sx, sy)));

        if (windowSizeMultipliers == null || windowSizeMultipliers.Length == 0)
            return target;

        int best = windowSizeMultipliers[0];
        int bestDist = Mathf.Abs(best - target);

        for (int i = 1; i < windowSizeMultipliers.Length; i++)
        {
            int m = windowSizeMultipliers[i];
            int d = Mathf.Abs(m - target);
            if (d < bestDist)
            {
                bestDist = d;
                best = m;
            }
        }

        return Mathf.Max(1, best);
    }

    void ApplyVideoSettingsImmediate()
    {
        if (!allowVideoMenu)
            return;

        Resolution curRes = Screen.currentResolution;

        if (_videoFullscreen)
        {
            _videoWindowMult = GetBestWindowMultiplierForCurrentMonitor();

            int w = Mathf.Max(64, curRes.width);
            int h = Mathf.Max(64, curRes.height);

            Screen.SetResolution(w, h, FullScreenMode.FullScreenWindow);
            Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
            Screen.fullScreen = true;
        }
        else
        {
            _videoWindowMult = Mathf.Max(1, _videoWindowMultWindowed);

            int w = Mathf.Max(64, referenceWidth * _videoWindowMult);
            int h = Mathf.Max(64, referenceHeight * _videoWindowMult);

            Screen.SetResolution(w, h, FullScreenMode.Windowed);
            Screen.fullScreenMode = FullScreenMode.Windowed;
            Screen.fullScreen = false;
        }

        ForceRecomputeLayoutAfterResolutionChange();
        StartFullscreenWatchdogIfNeeded();
    }

    void StartFullscreenWatchdogIfNeeded()
    {
        if (_fullscreenWatchdog != null)
            StopCoroutine(_fullscreenWatchdog);

        _fullscreenWatchdog = StartCoroutine(FullscreenWatchdogRoutine());
    }

    IEnumerator FullscreenWatchdogRoutine()
    {
        for (int i = 0; i < 12; i++)
        {
            yield return null;

            bool fsOk = Screen.fullScreen == _videoFullscreen;
            bool modeOk = _videoFullscreen
                ? Screen.fullScreenMode == FullScreenMode.FullScreenWindow
                : Screen.fullScreenMode == FullScreenMode.Windowed;

            if (fsOk && modeOk)
            {
                _fullscreenWatchdog = null;
                yield break;
            }

            if (_videoFullscreen)
            {
                Resolution r = Screen.currentResolution;
                Screen.SetResolution(r.width, r.height, FullScreenMode.FullScreenWindow);
                Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
                Screen.fullScreen = true;
            }
        }

        _fullscreenWatchdog = null;
    }

    void ForceRecomputeLayoutAfterResolutionChange()
    {
        if (_postResolutionRefreshRoutine != null)
            StopCoroutine(_postResolutionRefreshRoutine);

        _postResolutionRefreshRoutine = StartCoroutine(PostResolutionRefreshRoutine());
    }

    IEnumerator PostResolutionRefreshRoutine()
    {
        yield return null;
        RequestStabilizedLayoutRefresh("PostResChange");
        _postResolutionRefreshRoutine = null;
    }

    static int IndexOf(int[] arr, int value)
    {
        if (arr == null) return -1;
        for (int i = 0; i < arr.Length; i++)
            if (arr[i] == value) return i;
        return -1;
    }

    float ComputeUiScaleFromLayoutRoot()
    {
        RectTransform root = GetEffectiveLayoutRoot();
        if (root == null)
            return 1f;

        Rect r = root.rect;
        float usedW = Mathf.Max(1f, r.width);
        float usedH = Mathf.Max(1f, r.height);

        if (!dynamicScale)
            return 1f;

        float sx = usedW / Mathf.Max(1f, referenceWidth);
        float sy = usedH / Mathf.Max(1f, referenceHeight);
        float baseScaleRaw = Mathf.Min(sx, sy);

        float ui = (baseScaleRaw / Mathf.Max(1f, designUpscale)) * Mathf.Max(0.01f, extraScaleMultiplier);
        ui = Mathf.Clamp(ui, minScale, maxScale);
        return ui;
    }

    void ApplyResolvedLayout(string where)
    {
        if (!isActiveAndEnabled)
            return;

        if (referenceRect == null)
            referenceRect = ResolveReferenceRect();

        if (layoutRoot == null)
            layoutRoot = ResolveLayoutRoot();

        if (menuRect == null && menuText != null)
            menuRect = menuText.rectTransform;

        Canvas.ForceUpdateCanvases();

        _currentUiScale = ComputeUiScaleFromLayoutRoot();

        ApplyMenuAnchoredPosition();
        ApplyCursorScale();

        EnsurePushStartText();
        EnsureFooterText();
        EnsureBossRushLockedText();
        EnsureVideoValuesText();

        ApplyScaledFontSettings();
        RefreshMenuTextInternal();
        UpdateCursorPosition();
        UpdatePushStartPosition();
        UpdateFooterPosition();
        UpdateBossRushLockedPosition();
        UpdateVideoValuesPosition();

        DumpLayout(where);
    }

    void ApplyScaledFontSettings()
    {
        if (menuText != null)
        {
            menuText.fontSize = MenuFontSizeScaled;
            menuText.extraPadding = true;
            menuText.textWrappingMode = TextWrappingModes.NoWrap;
            menuText.overflowMode = TextOverflowModes.Overflow;
        }

        if (pushStartText != null)
            pushStartText.fontSize = PushStartFontSizeScaled;

        if (footerText != null)
            footerText.fontSize = FooterFontSizeScaled;

        if (bossRushLockedText != null)
            bossRushLockedText.fontSize = BossRushLockedFontSizeScaled;

        if (videoValuesText != null)
            videoValuesText.fontSize = MenuFontSizeScaled;
    }

    void ApplyCursorScale()
    {
        if (!scaleCursorWithUi || cursorRenderer == null)
            return;

        if (!_cursorBaseScaleCaptured)
        {
            _cursorBaseLocalScale = cursorRenderer.transform.localScale;
            _cursorBaseScaleCaptured = true;
        }

        float s = _currentUiScale;
        Vector3 baseScale = _cursorBaseLocalScale;
        cursorRenderer.transform.localScale = new Vector3(baseScale.x * s, baseScale.y * s, baseScale.z);
    }

    void EnsureBootSession()
    {
        if (bootedSession)
            return;

        PlayerPersistentStats.EnsureSessionBooted();
        bootedSession = true;
    }

    public void SetIgnoreStartKeyUntilRelease()
    {
        ignoreStartKeyUntilRelease = true;
    }

    void SetupMenuTextMaterial()
    {
        if (menuText == null)
            return;

        menuText.richText = true;
        menuText.textWrappingMode = TextWrappingModes.NoWrap;
        menuText.overflowMode = TextOverflowModes.Overflow;
        menuText.extraPadding = true;

        if (forceBold)
            menuText.fontStyle |= FontStyles.Bold;

        menuText.alignment = TextAlignmentOptions.Center;

        Material baseMat = menuText.fontMaterial;
        if (baseMat == null)
            baseMat = menuText.fontSharedMaterial;
        if (baseMat == null && menuText.font != null)
            baseMat = menuText.font.material;
        if (baseMat == null)
            return;

        if (runtimeMenuMat != null)
            Destroy(runtimeMenuMat);

        runtimeMenuMat = new Material(baseMat);

        TrySetColor(runtimeMenuMat, "_OutlineColor", outlineColor);
        TrySetFloat(runtimeMenuMat, "_OutlineWidth", outlineWidth);
        TrySetFloat(runtimeMenuMat, "_OutlineSoftness", outlineSoftness);

        TrySetFloat(runtimeMenuMat, "_FaceDilate", faceDilate);
        TrySetFloat(runtimeMenuMat, "_FaceSoftness", faceSoftness);

        if (enableUnderlay)
        {
            TrySetFloat(runtimeMenuMat, "_UnderlayDilate", underlayDilate);
            TrySetFloat(runtimeMenuMat, "_UnderlaySoftness", underlaySoftness);
            TrySetFloat(runtimeMenuMat, "_UnderlayOffsetX", underlayOffsetX);
            TrySetFloat(runtimeMenuMat, "_UnderlayOffsetY", underlayOffsetY);
            TrySetColor(runtimeMenuMat, "_UnderlayColor", underlayColor);
        }
        else
        {
            TrySetFloat(runtimeMenuMat, "_UnderlayDilate", 0f);
            TrySetFloat(runtimeMenuMat, "_UnderlaySoftness", 0f);
            TrySetFloat(runtimeMenuMat, "_UnderlayOffsetX", 0f);
            TrySetFloat(runtimeMenuMat, "_UnderlayOffsetY", 0f);
        }

        menuText.fontMaterial = runtimeMenuMat;
        menuText.UpdateMeshPadding();
        menuText.SetVerticesDirty();

        if (pushStartText != null) pushStartText.fontMaterial = runtimeMenuMat;
        if (footerText != null) footerText.fontMaterial = runtimeMenuMat;
        if (bossRushLockedText != null) bossRushLockedText.fontMaterial = runtimeMenuMat;
        if (videoValuesText != null) videoValuesText.fontMaterial = runtimeMenuMat;
    }

    void ApplyMenuAnchoredPosition()
    {
        if (menuRect == null)
            menuRect = menuText != null ? menuText.rectTransform : null;

        RectTransform root = GetEffectiveLayoutRoot();
        if (menuRect == null || root == null)
            return;

        Rect r = root.rect;
        float relX = r.width / Mathf.Max(1f, referenceWidth * Mathf.Max(1, designUpscale));
        float relY = r.height / Mathf.Max(1f, referenceHeight * Mathf.Max(1, designUpscale));

        Vector2 pos = new Vector2(
            menuAnchoredPos.x * relX,
            menuAnchoredPos.y * relY
        );

        if (forceMenuCenterX)
            pos.x = 0f;

        pos.y += menuExtraYOffset * relY;

        menuRect.anchorMin = new Vector2(0.5f, 0.5f);
        menuRect.anchorMax = new Vector2(0.5f, 0.5f);
        menuRect.pivot = new Vector2(0.5f, 0.5f);

        menuRect.sizeDelta = new Vector2(
            Mathf.Round(menuBoxSize.x * _currentUiScale),
            Mathf.Round(menuBoxSize.y * _currentUiScale)
        );

        menuRect.anchoredPosition = new Vector2(
            Mathf.Round(pos.x),
            Mathf.Round(pos.y)
        );
    }

    static void TrySetFloat(Material m, string prop, float value)
    {
        if (m != null && m.HasProperty(prop))
            m.SetFloat(prop, value);
    }

    static void TrySetColor(Material m, string prop, Color value)
    {
        if (m != null && m.HasProperty(prop))
            m.SetColor(prop, value);
    }

    RectTransform GetUiRootForGeneratedText()
    {
        RectTransform root = GetEffectiveLayoutRoot();
        if (root != null)
            return root;

        if (referenceRect != null)
            return referenceRect;

        Canvas canvas = GetRootCanvas();
        if (canvas != null)
            return canvas.transform as RectTransform;

        return null;
    }

    void EnsureVideoValuesText()
    {
        if (menuText == null)
            return;

        if (videoValuesText == null)
        {
            GameObject go = new("VideoValuesText", typeof(RectTransform));
            RectTransform root = GetUiRootForGeneratedText();
            if (root != null) go.transform.SetParent(root, false);
            else go.transform.SetParent(menuText.transform, false);

            videoValuesText = go.AddComponent<TextMeshProUGUI>();
            videoValuesText.raycastTarget = false;
        }

        videoValuesRect = videoValuesText.rectTransform;
        videoValuesRect.anchorMin = new Vector2(1f, 0.5f);
        videoValuesRect.anchorMax = new Vector2(1f, 0.5f);
        videoValuesRect.pivot = new Vector2(1f, 1f);
        videoValuesRect.sizeDelta = Vector2.zero;
        videoValuesRect.localScale = Vector3.one;

        videoValuesText.richText = false;
        videoValuesText.font = menuText.font;
        videoValuesText.fontStyle = menuText.fontStyle;
        videoValuesText.fontSize = MenuFontSizeScaled;
        videoValuesText.textWrappingMode = TextWrappingModes.NoWrap;
        videoValuesText.overflowMode = TextOverflowModes.Overflow;
        videoValuesText.extraPadding = true;
        videoValuesText.fontMaterial = runtimeMenuMat != null ? runtimeMenuMat : menuText.fontMaterial;
        videoValuesText.alignment = TextAlignmentOptions.TopRight;
        videoValuesText.color = Color.white;
        videoValuesText.gameObject.SetActive(false);
    }

    void EnsurePushStartText()
    {
        if (menuText == null)
            return;

        if (pushStartText == null)
        {
            GameObject go = new("PushStartText", typeof(RectTransform));
            RectTransform root = GetUiRootForGeneratedText();
            if (root != null) go.transform.SetParent(root, false);
            else go.transform.SetParent(menuText.transform, false);

            pushStartText = go.AddComponent<TextMeshProUGUI>();
            pushStartText.raycastTarget = false;
        }

        pushStartRect = pushStartText.rectTransform;
        pushStartRect.anchorMin = new Vector2(0f, 0.5f);
        pushStartRect.anchorMax = new Vector2(1f, 0.5f);
        pushStartRect.pivot = new Vector2(0.5f, 0.5f);
        pushStartRect.sizeDelta = Vector2.zero;
        pushStartRect.localScale = Vector3.one;

        pushStartText.richText = true;
        pushStartText.font = menuText.font;
        pushStartText.fontSize = PushStartFontSizeScaled;
        pushStartText.fontStyle = menuText.fontStyle;
        pushStartText.textWrappingMode = TextWrappingModes.NoWrap;
        pushStartText.overflowMode = TextOverflowModes.Overflow;
        pushStartText.extraPadding = true;
        pushStartText.fontMaterial = runtimeMenuMat != null ? runtimeMenuMat : menuText.fontMaterial;
        pushStartText.alignment = TextAlignmentOptions.Center;
        pushStartText.color = Color.white;
        pushStartText.text = $"<color={pushStartHex}>{pushStartLabel}</color>";
        pushStartText.gameObject.SetActive(false);
    }

    void EnsureFooterText()
    {
        if (menuText == null)
            return;

        if (footerText == null)
        {
            GameObject go = new("FooterText", typeof(RectTransform));
            RectTransform root = GetUiRootForGeneratedText();
            if (root != null) go.transform.SetParent(root, false);
            else go.transform.SetParent(menuText.transform, false);

            footerText = go.AddComponent<TextMeshProUGUI>();
            footerText.raycastTarget = false;
        }

        footerRect = footerText.rectTransform;
        footerText.richText = true;
        footerText.font = menuText.font;
        footerText.fontSize = FooterFontSizeScaled;
        footerText.fontStyle = menuText.fontStyle;
        footerText.textWrappingMode = TextWrappingModes.NoWrap;
        footerText.overflowMode = TextOverflowModes.Overflow;
        footerText.extraPadding = true;
        footerText.fontMaterial = runtimeMenuMat != null ? runtimeMenuMat : menuText.fontMaterial;
        footerText.alignment = TextAlignmentOptions.Center;
        footerText.color = Color.white;
        footerText.text = "";
        footerText.gameObject.SetActive(false);
    }

    void EnsureBossRushLockedText()
    {
        if (menuText == null)
            return;

        if (bossRushLockedText == null)
        {
            GameObject go = new("BossRushLockedText", typeof(RectTransform));
            RectTransform root = GetUiRootForGeneratedText();
            if (root != null) go.transform.SetParent(root, false);
            else go.transform.SetParent(menuText.transform, false);

            bossRushLockedText = go.AddComponent<TextMeshProUGUI>();
            bossRushLockedText.raycastTarget = false;
        }

        bossRushLockedRect = bossRushLockedText.rectTransform;
        bossRushLockedText.richText = true;
        bossRushLockedText.font = menuText.font;
        bossRushLockedText.fontStyle = menuText.fontStyle;
        bossRushLockedText.fontSize = BossRushLockedFontSizeScaled;
        bossRushLockedText.textWrappingMode = TextWrappingModes.NoWrap;
        bossRushLockedText.overflowMode = TextOverflowModes.Overflow;
        bossRushLockedText.extraPadding = true;
        bossRushLockedText.fontMaterial = runtimeMenuMat != null ? runtimeMenuMat : menuText.fontMaterial;
        bossRushLockedText.alignment = TextAlignmentOptions.Center;
        bossRushLockedText.text = "";
        bossRushLockedText.gameObject.SetActive(false);

        bossRushLockedRect.anchorMin = new Vector2(0f, 0f);
        bossRushLockedRect.anchorMax = new Vector2(1f, 0f);
        bossRushLockedRect.pivot = new Vector2(0.5f, 0f);
        bossRushLockedRect.anchoredPosition = new Vector2(0f, BossRushLockedBottomMarginScaled);
        bossRushLockedRect.sizeDelta = Vector2.zero;
        bossRushLockedRect.localScale = Vector3.one;
    }

    void UpdateBossRushLockedPosition()
    {
        if (bossRushLockedRect == null || bossRushLockedText == null)
            return;

        if (!bossRushLockedText.gameObject.activeSelf)
            return;

        bossRushLockedText.fontSize = BossRushLockedFontSizeScaled;
        bossRushLockedRect.anchoredPosition = new Vector2(0f, Mathf.Round(BossRushLockedBottomMarginScaled));
    }

    void UpdateVideoValuesPosition()
    {
        if (videoValuesText == null || videoValuesRect == null || menuText == null)
            return;

        bool show = menuMode == MenuMode.Video && allowVideoMenu;
        if (!show)
        {
            if (videoValuesText.gameObject.activeSelf)
                videoValuesText.gameObject.SetActive(false);
            return;
        }

        videoValuesText.fontSize = MenuFontSizeScaled;
        videoValuesText.font = menuText.font;
        videoValuesText.fontStyle = menuText.fontStyle;
        videoValuesText.fontMaterial = runtimeMenuMat != null ? runtimeMenuMat : menuText.fontMaterial;

        menuText.ForceMeshUpdate();
        TMP_TextInfo ti = menuText.textInfo;
        if (ti == null || ti.lineCount <= 0)
            return;

        RectTransform root = GetUiRootForGeneratedText();
        if (root == null)
            return;

        TMP_LineInfo li0 = ti.lineInfo[0];
        float yTopMenuLocal = li0.ascender;

        Vector3 world = menuText.rectTransform.TransformPoint(new Vector3(0f, yTopMenuLocal, 0f));
        Vector3 rootLocal = root.InverseTransformPoint(world);

        float x = -Mathf.Round(ScaledFloat(videoValuesRightPadding));
        float y = Mathf.Round(rootLocal.y);

        videoValuesRect.anchoredPosition = new Vector2(x, y);

        if (!videoValuesText.gameObject.activeSelf)
            videoValuesText.gameObject.SetActive(true);
    }

    public void ForceHide()
    {
        Running = false;
        locked = false;

        NormalGameRequested = false;
        BossRushRequested = false;
        ExitRequested = false;
        ControlsRequested = false;

        menuMode = MenuMode.Main;
        pendingStartFlow = StartFlowMode.None;

        StopPushStartBlink();
        HideFooterMessageImmediate();
        HideBossRushLockedMessageImmediate();

        if (titleScreenRawImage != null) titleScreenRawImage.gameObject.SetActive(false);
        if (menuText != null) menuText.gameObject.SetActive(false);
        if (cursorRenderer != null) cursorRenderer.gameObject.SetActive(false);
        if (pushStartText != null) pushStartText.gameObject.SetActive(false);
        if (footerText != null) footerText.gameObject.SetActive(false);
        if (bossRushLockedText != null) bossRushLockedText.gameObject.SetActive(false);
        if (videoValuesText != null) videoValuesText.gameObject.SetActive(false);
    }

    void ApplyTitleVisualNow()
    {
        if (titleScreenRawImage == null)
            return;

        titleScreenRawImage.gameObject.SetActive(true);

        if (titleScreenSprite != null)
        {
            Texture tex = titleScreenSprite.texture;
            if (tex != null)
            {
                tex.filterMode = FilterMode.Point;
                tex.wrapMode = TextureWrapMode.Clamp;
            }

            titleScreenRawImage.texture = tex;

            Rect r = titleScreenSprite.textureRect;
            Rect tr = tex != null ? new Rect(0f, 0f, tex.width, tex.height) : r;
            Rect uv = tex != null && tr.width > 0f && tr.height > 0f
                ? new Rect(r.x / tr.width, r.y / tr.height, r.width / tr.width, r.height / tr.height)
                : new Rect(0f, 0f, 1f, 1f);

            titleScreenRawImage.uvRect = uv;
        }
    }

    void ShowTitleScreenNow()
    {
        if (enableSurgicalLogs && referenceRect != null)
            Debug.Log($"{LOG} ShowTitleScreenNow BEFORE ApplyResolvedLayout | RefSize={referenceRect.rect.size}", this);

        ApplyTitleVisualNow();

        if (menuText != null)
        {
            menuText.gameObject.SetActive(true);
            SetupMenuTextMaterial();
        }

        EnsurePushStartText();
        EnsureFooterText();
        EnsureBossRushLockedText();
        EnsureVideoValuesText();

        if (cursorRenderer != null)
        {
            cursorRenderer.gameObject.SetActive(true);
            cursorRenderer.RefreshFrame();
        }

        ApplyResolvedLayout("ShowTitleScreenNow");
    }

    bool TryGetAnyPlayerDown(PlayerAction action, out int pid)
    {
        pid = 1;

        PlayerInputManager input = PlayerInputManager.Instance;
        if (input == null) return false;

        for (int p = 1; p <= 4; p++)
        {
            if (input.GetDown(p, action))
            {
                pid = p;
                return true;
            }
        }

        return false;
    }

    bool TryGetAnyPlayerDownEither(PlayerAction a, PlayerAction b, out int pid)
    {
        if (TryGetAnyPlayerDown(a, out pid)) return true;
        if (TryGetAnyPlayerDown(b, out pid)) return true;
        pid = 1;
        return false;
    }

    bool AnyPlayerHeld(PlayerAction action)
    {
        PlayerInputManager input = PlayerInputManager.Instance;
        if (input == null) return false;

        for (int p = 1; p <= 4; p++)
        {
            if (input.Get(p, action))
                return true;
        }

        return false;
    }

    bool AnyPlayerHeldAnyMenuKey()
    {
        return AnyPlayerHeld(PlayerAction.ActionA) ||
               AnyPlayerHeld(PlayerAction.ActionB) ||
               AnyPlayerHeld(PlayerAction.ActionC) ||
               AnyPlayerHeld(PlayerAction.Start) ||
               AnyPlayerHeld(PlayerAction.MoveUp) ||
               AnyPlayerHeld(PlayerAction.MoveDown) ||
               AnyPlayerHeld(PlayerAction.MoveLeft) ||
               AnyPlayerHeld(PlayerAction.MoveRight);
    }

    public IEnumerator Play(Image fadeToHideOptional)
    {
        EnsureBootSession();

        ForceHide();

        Running = true;
        locked = false;

        menuMode = MenuMode.Main;
        menuIndex = 0;
        pendingStartFlow = StartFlowMode.None;

        NormalGameRequested = false;
        BossRushRequested = false;
        ExitRequested = false;
        ControlsRequested = false;

        if (fadeToHideOptional != null)
        {
            fadeToHideOptional.gameObject.SetActive(true);
            fadeToHideOptional.transform.SetAsLastSibling();
        }

        yield return StartCoroutine(StabilizeLayoutBeforeShow());

        ShowTitleScreenNow();

        if (fadeToHideOptional != null)
            fadeToHideOptional.gameObject.SetActive(false);

        if (titleMusic != null && GameMusicController.Instance != null)
            GameMusicController.Instance.PlayMusic(titleMusic, titleMusicVolume, true);

        if (ignoreStartKeyUntilRelease ||
            AnyPlayerHeld(PlayerAction.Start) ||
            AnyPlayerHeld(PlayerAction.ActionA))
        {
            ignoreStartKeyUntilRelease = false;

            while (AnyPlayerHeld(PlayerAction.Start) || AnyPlayerHeld(PlayerAction.ActionA))
                yield return null;

            yield return null;
        }

        RefreshMenuText();
        StartPushStartBlink();

        while (Running && !locked)
        {
            int itemCount = GetMenuItemCount();

            if (TryGetAnyPlayerDown(PlayerAction.MoveUp, out _))
            {
                menuIndex = Wrap(menuIndex - 1, itemCount);
                PlayMoveSfx();
                HideFooterMessageImmediate();
                HideBossRushLockedMessageImmediate();
                RefreshMenuText();
            }

            if (TryGetAnyPlayerDown(PlayerAction.MoveDown, out _))
            {
                menuIndex = Wrap(menuIndex + 1, itemCount);
                PlayMoveSfx();
                HideFooterMessageImmediate();
                HideBossRushLockedMessageImmediate();
                RefreshMenuText();
            }

            if ((menuMode == MenuMode.PlayerCount || menuMode == MenuMode.Options || menuMode == MenuMode.Video || menuMode == MenuMode.ResetSaveConfirm) &&
                TryGetAnyPlayerDown(PlayerAction.ActionB, out _))
            {
                PlayBackSfx();

                if (menuMode == MenuMode.PlayerCount)
                {
                    menuMode = MenuMode.Main;
                    menuIndex = 0;
                    pendingStartFlow = StartFlowMode.None;
                }
                else if (menuMode == MenuMode.Options)
                {
                    menuMode = MenuMode.Main;
                    menuIndex = MAIN_IDX_OPTIONS;
                }
                else if (menuMode == MenuMode.Video)
                {
                    menuMode = MenuMode.Options;
                    menuIndex = OPTIONS_IDX_VIDEO;
                }
                else if (menuMode == MenuMode.ResetSaveConfirm)
                {
                    menuMode = MenuMode.Options;
                    menuIndex = OPTIONS_IDX_RESET_SAVE;
                }

                HideFooterMessageImmediate();
                HideBossRushLockedMessageImmediate();
                RefreshMenuText();

                while (AnyPlayerHeld(PlayerAction.ActionB))
                    yield return null;

                yield return null;
                continue;
            }

            if (menuMode == MenuMode.Video)
            {
                bool left = TryGetAnyPlayerDown(PlayerAction.MoveLeft, out _);
                bool right = TryGetAnyPlayerDown(PlayerAction.MoveRight, out _);
                bool confirm = TryGetAnyPlayerDownEither(PlayerAction.Start, PlayerAction.ActionA, out _);

                if (left || right || confirm)
                {
                    bool changed = false;

                    if (menuIndex == VIDEO_IDX_FULLSCREEN)
                    {
                        _videoFullscreen = !_videoFullscreen;
                        _videoWindowMult = _videoFullscreen
                            ? GetBestWindowMultiplierForCurrentMonitor()
                            : Mathf.Max(1, _videoWindowMultWindowed);
                        changed = true;
                    }
                    else if (menuIndex == VIDEO_IDX_WINDOWSIZE)
                    {
                        if (!_videoFullscreen)
                        {
                            int idx = IndexOf(windowSizeMultipliers, _videoWindowMultWindowed);
                            if (idx < 0) idx = 0;

                            if (left) idx--;
                            else idx++;

                            idx = Wrap(idx, windowSizeMultipliers.Length);

                            _videoWindowMultWindowed = windowSizeMultipliers[idx];
                            _videoWindowMult = _videoWindowMultWindowed;
                            changed = true;
                        }
                        else
                        {
                            PlayDeniedSfx();
                        }
                    }

                    if (changed)
                    {
                        PlaySelectSfx();
                        SaveVideoPrefs();
                        ApplyVideoSettingsImmediate();
                        RefreshMenuText();

                        while (AnyPlayerHeld(PlayerAction.Start) || AnyPlayerHeld(PlayerAction.ActionA) ||
                               AnyPlayerHeld(PlayerAction.MoveLeft) || AnyPlayerHeld(PlayerAction.MoveRight))
                            yield return null;

                        yield return null;
                        continue;
                    }

                    if (confirm && menuIndex == VIDEO_IDX_WINDOWSIZE && _videoFullscreen)
                    {
                        while (AnyPlayerHeld(PlayerAction.Start) || AnyPlayerHeld(PlayerAction.ActionA))
                            yield return null;

                        yield return null;
                        continue;
                    }
                }

                yield return null;
                continue;
            }

            if (TryGetAnyPlayerDownEither(PlayerAction.Start, PlayerAction.ActionA, out _))
            {
                if (menuMode == MenuMode.Main)
                {
                    if (menuIndex == MAIN_IDX_NORMAL)
                    {
                        locked = true;
                        PlaySelectSfx();

                        if (cursorRenderer != null)
                            yield return cursorRenderer.PlayCycles(2);

                        pendingStartFlow = StartFlowMode.Normal;
                        menuMode = MenuMode.PlayerCount;
                        menuIndex = 0;
                        locked = false;

                        HideFooterMessageImmediate();
                        HideBossRushLockedMessageImmediate();
                        RefreshMenuText();

                        while (AnyPlayerHeld(PlayerAction.Start) || AnyPlayerHeld(PlayerAction.ActionA))
                            yield return null;

                        yield return null;
                        continue;
                    }

                    if (menuIndex == MAIN_IDX_BOSS_RUSH)
                    {
                        if (!forceBossRushUnlocked)
                        {
                            PlayDeniedSfx();
                            ShowBossRushLockedMessage(bossRushLockedMessage, bossRushLockedMessageHex, bossRushLockedShowSeconds);

                            while (AnyPlayerHeld(PlayerAction.Start) || AnyPlayerHeld(PlayerAction.ActionA))
                                yield return null;

                            yield return null;
                            continue;
                        }

                        locked = true;
                        PlaySelectSfx();

                        if (cursorRenderer != null)
                            yield return cursorRenderer.PlayCycles(2);

                        pendingStartFlow = StartFlowMode.BossRush;
                        menuMode = MenuMode.PlayerCount;
                        menuIndex = 0;
                        locked = false;

                        HideFooterMessageImmediate();
                        HideBossRushLockedMessageImmediate();
                        RefreshMenuText();

                        while (AnyPlayerHeld(PlayerAction.Start) || AnyPlayerHeld(PlayerAction.ActionA))
                            yield return null;

                        yield return null;
                        continue;
                    }

                    if (menuIndex == MAIN_IDX_OPTIONS)
                    {
                        locked = true;
                        PlaySelectSfx();

                        if (cursorRenderer != null)
                            yield return cursorRenderer.PlayCycles(2);

                        menuMode = MenuMode.Options;
                        menuIndex = 0;
                        locked = false;

                        HideFooterMessageImmediate();
                        HideBossRushLockedMessageImmediate();
                        RefreshMenuText();

                        while (AnyPlayerHeld(PlayerAction.Start) || AnyPlayerHeld(PlayerAction.ActionA))
                            yield return null;

                        yield return null;
                        continue;
                    }

                    locked = true;
                    PlaySelectSfx();

                    if (cursorRenderer != null)
                        yield return cursorRenderer.PlayCycles(2);

                    ExitRequested = true;
                    yield return ExitGame();
                    yield break;
                }

                if (menuMode == MenuMode.Options)
                {
                    locked = true;
                    PlaySelectSfx();

                    if (cursorRenderer != null)
                        yield return cursorRenderer.PlayCycles(2);

                    if (menuIndex == OPTIONS_IDX_CONTROLS)
                    {
                        ControlsRequested = true;

                        HideFooterMessageImmediate();
                        HideBossRushLockedMessageImmediate();
                        StopPushStartBlink();

                        if (!string.IsNullOrEmpty(controlsSceneName))
                        {
                            SceneManager.LoadScene(controlsSceneName);
                            yield break;
                        }

                        ControlsRequested = false;
                        locked = false;

                        while (AnyPlayerHeldAnyMenuKey())
                            yield return null;

                        yield return null;
                        continue;
                    }

                    if (menuIndex == OPTIONS_IDX_VIDEO)
                    {
                        locked = false;

                        if (allowVideoMenu)
                        {
                            menuMode = MenuMode.Video;
                            menuIndex = 0;
                            HideFooterMessageImmediate();
                            HideBossRushLockedMessageImmediate();
                            RefreshMenuText();

                            while (AnyPlayerHeld(PlayerAction.Start) || AnyPlayerHeld(PlayerAction.ActionA))
                                yield return null;

                            yield return null;
                            continue;
                        }

                        while (AnyPlayerHeld(PlayerAction.Start) || AnyPlayerHeld(PlayerAction.ActionA))
                            yield return null;

                        yield return null;
                        continue;
                    }

                    if (menuIndex == OPTIONS_IDX_RESET_SAVE)
                    {
                        menuMode = MenuMode.ResetSaveConfirm;
                        menuIndex = RESET_SAVE_IDX_CANCEL;
                        locked = false;

                        HideFooterMessageImmediate();
                        HideBossRushLockedMessageImmediate();
                        RefreshMenuText();

                        while (AnyPlayerHeld(PlayerAction.Start) || AnyPlayerHeld(PlayerAction.ActionA))
                            yield return null;

                        yield return null;
                        continue;
                    }
                }

                if (menuMode == MenuMode.ResetSaveConfirm)
                {
                    if (menuIndex == RESET_SAVE_IDX_CANCEL)
                    {
                        PlayBackSfx();

                        menuMode = MenuMode.Options;
                        menuIndex = OPTIONS_IDX_RESET_SAVE;
                        locked = false;

                        HideFooterMessageImmediate();
                        HideBossRushLockedMessageImmediate();
                        RefreshMenuText();

                        while (AnyPlayerHeld(PlayerAction.Start) || AnyPlayerHeld(PlayerAction.ActionA))
                            yield return null;

                        yield return null;
                        continue;
                    }

                    locked = true;
                    PlaySelectSfx();

                    if (cursorRenderer != null)
                        yield return cursorRenderer.PlayCycles(2);

                    ResetEntireSaveFile();
                    forceBossRushUnlocked = bossRushInspectorDefaultUnlocked;

                    PlayResetSaveCompletedSfx();

                    menuMode = MenuMode.Options;
                    menuIndex = OPTIONS_IDX_RESET_SAVE;
                    locked = false;

                    HideFooterMessageImmediate();
                    RefreshMenuText();

                    ShowBossRushLockedMessage(
                        resetSaveCompletedMessage,
                        bossRushLockedMessageHex,
                        resetSaveCompletedShowSeconds
                    );

                    while (AnyPlayerHeld(PlayerAction.Start) || AnyPlayerHeld(PlayerAction.ActionA))
                        yield return null;

                    yield return null;
                    continue;
                }

                if (menuMode == MenuMode.PlayerCount)
                {
                    int chosenCount = menuIndex + 1;

                    if (GameSession.Instance != null)
                        GameSession.Instance.SetActivePlayerCount(chosenCount);

                    locked = true;
                    PlaySelectSfx();

                    if (cursorRenderer != null)
                        yield return cursorRenderer.PlayCycles(2);

                    if (pendingStartFlow == StartFlowMode.BossRush)
                    {
                        BossRushRequested = true;
                        yield return StartSelectedGameFlow();
                        yield break;
                    }

                    NormalGameRequested = true;
                    yield return StartSelectedGameFlow();
                    yield break;
                }
            }

            yield return null;
        }
    }

    int GetMenuItemCount()
    {
        if (menuMode == MenuMode.PlayerCount) return 4;
        if (menuMode == MenuMode.Options) return 3;
        if (menuMode == MenuMode.Video) return 2;
        if (menuMode == MenuMode.ResetSaveConfirm) return 2;
        return 4;
    }

    IEnumerator StartSelectedGameFlow()
    {
        float d = Mathf.Max(0.01f, startGameFadeOutDuration);

        StageIntroTransition transition = StageIntroTransition.Instance;
        if (transition != null)
            transition.StartFadeOut(d, false);

        yield return new WaitForSecondsRealtime(d);

        if (GameMusicController.Instance != null)
            GameMusicController.Instance.StopMusic();

        if (titleScreenRawImage != null) titleScreenRawImage.gameObject.SetActive(false);
        if (menuText != null) menuText.gameObject.SetActive(false);
        if (cursorRenderer != null) cursorRenderer.gameObject.SetActive(false);
        if (pushStartText != null) pushStartText.gameObject.SetActive(false);
        if (footerText != null) footerText.gameObject.SetActive(false);
        if (bossRushLockedText != null) bossRushLockedText.gameObject.SetActive(false);
        if (videoValuesText != null) videoValuesText.gameObject.SetActive(false);

        StopPushStartBlink();
        HideBossRushLockedMessageImmediate();
        Running = false;
    }

    IEnumerator ExitGame()
    {
        float wait = Mathf.Max(0f, exitDelayRealtime);
        if (wait > 0f)
            yield return new WaitForSecondsRealtime(wait);

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif

        StopPushStartBlink();
        HideBossRushLockedMessageImmediate();
        Running = false;
    }

    static string ColorWithAlpha(string rgbHexNoAlpha, float alpha01)
    {
        int a = Mathf.Clamp(Mathf.RoundToInt(alpha01 * 255f), 0, 255);
        return $"#{rgbHexNoAlpha}{a:X2}";
    }

    public void RefreshMenuText()
    {
        RefreshMenuTextInternal();
        UpdateCursorPosition();

        if (menuMode == MenuMode.ResetSaveConfirm)
            StopPushStartBlink();
        else if (Running && pushStartRoutine == null)
            StartPushStartBlink();

        UpdatePushStartPosition();
        UpdateFooterPosition();
        UpdateBossRushLockedPosition();
        UpdateVideoValuesPosition();
    }

    void RefreshMenuTextInternal()
    {
        if (menuText == null)
            return;

        const string baseRgb = "FFFFE7";
        const string warnRgb = "#FF5A4A";
        int size = MenuFontSizeScaled;

        if (menuMode == MenuMode.Main)
        {
            string normal = $"<color=#{baseRgb}FF>NORMAL GAME</color>";
            string bossRush = forceBossRushUnlocked
                ? $"<color=#{baseRgb}FF>BOSS RUSH</color>"
                : $"<color={ColorWithAlpha(baseRgb, bossRushLockedAlpha)}>BOSS RUSH</color>";
            string options = $"<color=#{baseRgb}FF>OPTIONS</color>";
            string exit = $"<color=#{baseRgb}FF>EXIT</color>";

            menuText.text =
                "<align=left>" +
                $"<size={size}>{normal}</size>\n" +
                $"<size={size}>{bossRush}</size>\n" +
                $"<size={size}>{options}</size>\n" +
                $"<size={size}>{exit}</size>" +
                "</align>";

            if (videoValuesText != null) videoValuesText.gameObject.SetActive(false);
            return;
        }

        if (menuMode == MenuMode.Options)
        {
            string controls = $"<color=#{baseRgb}FF>CONTROLS</color>";
            string video = $"<color=#{baseRgb}FF>VIDEO</color>";
            string resetSave = $"<color={warnRgb}>RESET SAVE</color>";

            menuText.text =
                "<align=left>" +
                $"<size={size}>{controls}</size>\n" +
                $"<size={size}>{video}</size>\n" +
                $"<size={size}>{resetSave}</size>" +
                "</align>";

            if (videoValuesText != null) videoValuesText.gameObject.SetActive(false);
            return;
        }

        if (menuMode == MenuMode.PlayerCount)
        {
            string p1 = $"<color=#{baseRgb}FF>1 PLAYER</color>";
            string p2 = $"<color=#{baseRgb}FF>2 PLAYERS</color>";
            string p3 = $"<color=#{baseRgb}FF>3 PLAYERS</color>";
            string p4 = $"<color=#{baseRgb}FF>4 PLAYERS</color>";

            menuText.text =
                "<align=left>" +
                $"<size={size}>{p1}</size>\n" +
                $"<size={size}>{p2}</size>\n" +
                $"<size={size}>{p3}</size>\n" +
                $"<size={size}>{p4}</size>" +
                "</align>";

            if (videoValuesText != null) videoValuesText.gameObject.SetActive(false);
            return;
        }

        if (menuMode == MenuMode.ResetSaveConfirm)
        {
            string l2 = $"<color={warnRgb}>THIS WILL ERASE:</color>";
            string l3 = $"<color={pushStartHex}>ALL NORMAL GAME SAVES</color>";
            string l4 = $"<color={pushStartHex}>UNLOCKED SKINS / MODES</color>";
            string l5 = $"<color={pushStartHex}>BOSS RUSH RECORDS</color>";
            string l6 = $"<color={pushStartHex}>CONTROLS</color>";
            string cancel = $"<color=#{baseRgb}FF>CANCEL</color>";
            string confirm = $"<color={warnRgb}>RESET SAVE</color>";

            menuText.text =
                "<align=left>" +
                $"<size={size}>{l2}</size>\n" +
                $"<size={size}>{l3}</size>\n" +
                $"<size={size}>{l4}</size>\n" +
                $"<size={size}>{l5}</size>\n" +
                $"<size={size}>{l6}</size>\n" +
                "\n" +
                $"<size={size}>{cancel}</size>\n" +
                $"<size={size}>{confirm}</size>" +
                "</align>";

            if (videoValuesText != null)
                videoValuesText.gameObject.SetActive(false);

            return;
        }

        if (_videoFullscreen)
            _videoWindowMult = GetBestWindowMultiplierForCurrentMonitor();
        else
            _videoWindowMult = Mathf.Max(1, _videoWindowMultWindowed);

        menuText.text =
            "<align=left>" +
            $"<size={size}>FULLSCREEN</size>\n" +
            $"<size={size}>WINDOW SIZE</size>" +
            "</align>";

        EnsureVideoValuesText();

        if (videoValuesText != null)
        {
            string fs = _videoFullscreen ? "ON" : "OFF";
            string ws = $"{Mathf.Max(1, _videoWindowMult)}x";
            videoValuesText.text = $"{fs}\n{ws}";
        }
    }

    void StartPushStartBlink()
    {
        EnsurePushStartText();

        if (pushStartText == null)
            return;

        if (menuMode == MenuMode.ResetSaveConfirm)
        {
            pushStartText.gameObject.SetActive(false);
            return;
        }

        StopPushStartBlink();

        pushStartVisible = true;
        pushStartText.gameObject.SetActive(true);
        UpdatePushStartPosition();

        pushStartRoutine = StartCoroutine(PushStartBlinkRoutine());
    }

    void StopPushStartBlink()
    {
        if (pushStartRoutine != null)
        {
            StopCoroutine(pushStartRoutine);
            pushStartRoutine = null;
        }

        if (pushStartText != null)
            pushStartText.gameObject.SetActive(false);
    }

    IEnumerator PushStartBlinkRoutine()
    {
        WaitForSecondsRealtime wait = new(Mathf.Max(0.05f, pushStartBlinkInterval));

        while (Running)
        {
            if (menuMode == MenuMode.ResetSaveConfirm)
            {
                if (pushStartText != null)
                    pushStartText.gameObject.SetActive(false);

                pushStartRoutine = null;
                yield break;
            }

            pushStartVisible = !pushStartVisible;

            if (pushStartText != null)
                pushStartText.gameObject.SetActive(pushStartVisible);

            yield return wait;
        }

        pushStartRoutine = null;
    }

    void UpdatePushStartPosition()
    {
        if (menuText == null || pushStartRect == null)
            return;

        RectTransform root = GetUiRootForGeneratedText();
        if (root == null)
            return;

        menuText.ForceMeshUpdate();
        TMP_TextInfo ti = menuText.textInfo;
        if (ti == null || ti.lineCount <= 0)
            return;

        TMP_LineInfo first = ti.lineInfo[0];
        float yMenuLocal = first.ascender + PushStartYOffsetScaled + PushStartExtraYOffsetScaled;

        Vector3 world = menuText.rectTransform.TransformPoint(new Vector3(0f, yMenuLocal, 0f));
        Vector3 yRootLocal = root.InverseTransformPoint(world);
        float y = Mathf.Round(yRootLocal.y);

        pushStartRect.anchoredPosition = new Vector2(0f, y);
    }

    void UpdateFooterPosition()
    {
        if (menuText == null || footerRect == null || footerText == null)
            return;

        if (!footerText.gameObject.activeSelf)
            return;

        menuText.ForceMeshUpdate();
        TMP_TextInfo ti = menuText.textInfo;
        if (ti == null || ti.lineCount <= 0)
            return;

        float minX = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;

        for (int i = 0; i < ti.lineCount; i++)
        {
            TMP_LineInfo li = ti.lineInfo[i];
            minX = Mathf.Min(minX, li.lineExtents.min.x);
            maxX = Mathf.Max(maxX, li.lineExtents.max.x);
        }

        float centerX = (minX + maxX) * 0.5f;
        TMP_LineInfo last = ti.lineInfo[ti.lineCount - 1];
        float y = last.descender + FooterOffsetFromLastLineYScaled;

        footerRect.localPosition = new Vector3(Mathf.Round(centerX), Mathf.Round(y), 0f);
    }

    void ShowBossRushLockedMessage(string msg, string hex, float seconds)
    {
        EnsureBossRushLockedText();
        if (bossRushLockedText == null)
            return;

        if (bossRushLockedRoutine != null)
        {
            StopCoroutine(bossRushLockedRoutine);
            bossRushLockedRoutine = null;
        }

        bossRushLockedText.fontSize = BossRushLockedFontSizeScaled;
        bossRushLockedText.text = $"<color={hex}>{msg}</color>";
        bossRushLockedText.gameObject.SetActive(true);
        UpdateBossRushLockedPosition();

        bossRushLockedRoutine = StartCoroutine(BossRushLockedRoutine(seconds));
    }

    IEnumerator BossRushLockedRoutine(float seconds)
    {
        float t = Mathf.Max(0.05f, seconds);
        yield return new WaitForSecondsRealtime(t);
        HideBossRushLockedMessageImmediate();
    }

    void PlayResetSaveCompletedSfx()
    {
        if (resetSaveCompletedSfx == null || GameMusicController.Instance == null)
            return;

        GameMusicController.Instance.PlaySfx(resetSaveCompletedSfx, resetSaveCompletedVolume);
    }

    void ShowFooterMessage(string msg, string hex, float seconds)
    {
        EnsureFooterText();
        if (footerText == null)
            return;

        if (footerRoutine != null)
        {
            StopCoroutine(footerRoutine);
            footerRoutine = null;
        }

        footerText.fontSize = FooterFontSizeScaled;
        footerText.text = $"<color={hex}>{msg}</color>";
        footerText.gameObject.SetActive(true);
        UpdateFooterPosition();

        footerRoutine = StartCoroutine(FooterMessageRoutine(seconds));
    }

    IEnumerator FooterMessageRoutine(float seconds)
    {
        float t = Mathf.Max(0.05f, seconds);
        yield return new WaitForSecondsRealtime(t);
        HideFooterMessageImmediate();
    }

    void HideFooterMessageImmediate()
    {
        if (footerRoutine != null)
        {
            StopCoroutine(footerRoutine);
            footerRoutine = null;
        }

        if (footerText != null)
            footerText.gameObject.SetActive(false);
    }

    void PlayMoveSfx()
    {
        if (moveOptionSfx == null || GameMusicController.Instance == null)
            return;

        GameMusicController.Instance.PlaySfx(moveOptionSfx, moveOptionVolume);
    }

    void PlaySelectSfx()
    {
        if (selectOptionSfx == null || GameMusicController.Instance == null)
            return;

        GameMusicController.Instance.PlaySfx(selectOptionSfx, selectOptionVolume);
    }

    void PlayBackSfx()
    {
        if (backOptionSfx == null || GameMusicController.Instance == null)
            return;

        GameMusicController.Instance.PlaySfx(backOptionSfx, backOptionVolume);
    }

    void PlayDeniedSfx()
    {
        if (GameMusicController.Instance == null)
            return;

        if (deniedOptionSfx != null)
            GameMusicController.Instance.PlaySfx(deniedOptionSfx, deniedOptionVolume);
        else if (backOptionSfx != null)
            GameMusicController.Instance.PlaySfx(backOptionSfx, backOptionVolume);
        else if (selectOptionSfx != null)
            GameMusicController.Instance.PlaySfx(selectOptionSfx, selectOptionVolume);
    }

    int Wrap(int v, int count)
    {
        if (count <= 0) return 0;
        v %= count;
        if (v < 0) v += count;
        return v;
    }

    void UpdateCursorPosition()
    {
        if (menuText == null || cursorRenderer == null)
            return;

        RectTransform root = GetEffectiveLayoutRoot();
        if (root == null)
            return;

        menuText.ForceMeshUpdate();
        TMP_TextInfo ti = menuText.textInfo;
        if (ti == null || ti.lineCount <= 0)
            return;

        int line = GetCursorVisualLineIndex(ti.lineCount);
        TMP_LineInfo li = ti.lineInfo[line];

        float yLocalMenu = (li.ascender + li.descender) * 0.5f;
        float xLocalMenu = li.lineExtents.min.x;

        Vector2 offs = CursorOffsetScaled;

        Vector3 world = menuText.rectTransform.TransformPoint(
            new Vector3(xLocalMenu + offs.x, yLocalMenu + offs.y, 0f)
        );

        Vector3 rootLocal = root.InverseTransformPoint(world);
        rootLocal.x = Mathf.Round(rootLocal.x);
        rootLocal.y = Mathf.Round(rootLocal.y);
        rootLocal.z = 0f;

        cursorRenderer.SetExternalBaseLocalPosition(rootLocal);
    }

    int GetCursorVisualLineIndex(int lineCount)
    {
        if (menuMode == MenuMode.ResetSaveConfirm)
            return Mathf.Clamp(RESET_SAVE_SELECTABLE_LINE_START + menuIndex, 0, lineCount - 1);

        return Mathf.Clamp(menuIndex, 0, lineCount - 1);
    }

    void ResetEntireSaveFile()
    {
        try
        {
            string filePath = SaveSystem.SaveFilePath;
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
        catch (System.Exception)
        {
        }

        SaveSystem.Reload();
    }

    void HideBossRushLockedMessageImmediate()
    {
        if (bossRushLockedRoutine != null)
        {
            StopCoroutine(bossRushLockedRoutine);
            bossRushLockedRoutine = null;
        }

        if (bossRushLockedText != null)
            bossRushLockedText.gameObject.SetActive(false);
    }

    void DumpLayout(string context)
    {
        if (!enableSurgicalLogs)
            return;

        string screenInfo = $"Screen=({Screen.width},{Screen.height})";
        string scaleInfo = $"uiScale={_currentUiScale:0.###}";

        string refInfo = "Reference=NULL";
        if (referenceRect != null)
        {
            referenceRect.GetWorldCorners(_refWorldCorners);

            Vector2 refBL = RectTransformUtility.WorldToScreenPoint(null, _refWorldCorners[0]);
            Vector2 refTL = RectTransformUtility.WorldToScreenPoint(null, _refWorldCorners[1]);
            Vector2 refTR = RectTransformUtility.WorldToScreenPoint(null, _refWorldCorners[2]);

            refInfo =
                $"RefBL=({refBL.x:0.###},{refBL.y:0.###}) " +
                $"RefTL=({refTL.x:0.###},{refTL.y:0.###}) " +
                $"RefTR=({refTR.x:0.###},{refTR.y:0.###})";
        }

        string layoutInfo = "LayoutRoot=NULL";
        RectTransform root = GetEffectiveLayoutRoot();
        if (root != null)
        {
            root.GetWorldCorners(_layoutWorldCorners);

            Vector2 bl = RectTransformUtility.WorldToScreenPoint(null, _layoutWorldCorners[0]);
            Vector2 tl = RectTransformUtility.WorldToScreenPoint(null, _layoutWorldCorners[1]);
            Vector2 tr = RectTransformUtility.WorldToScreenPoint(null, _layoutWorldCorners[2]);

            layoutInfo =
                $"LayoutBL=({bl.x:0.###},{bl.y:0.###}) " +
                $"LayoutTL=({tl.x:0.###},{tl.y:0.###}) " +
                $"LayoutTR=({tr.x:0.###},{tr.y:0.###}) " +
                $"LayoutSize=({root.rect.width:0.###},{root.rect.height:0.###})";
        }

        string menuInfo = "Menu=NULL";
        if (menuRect != null)
        {
            menuRect.GetWorldCorners(_menuWorldCorners);

            Vector2 bl = RectTransformUtility.WorldToScreenPoint(null, _menuWorldCorners[0]);
            Vector2 tl = RectTransformUtility.WorldToScreenPoint(null, _menuWorldCorners[1]);

            menuInfo =
                $"MenuBL=({bl.x:0.###},{bl.y:0.###}) " +
                $"MenuTL=({tl.x:0.###},{tl.y:0.###}) " +
                $"anchored=({menuRect.anchoredPosition.x:0.###},{menuRect.anchoredPosition.y:0.###})";
        }

        Debug.Log($"{LOG} {context} | {screenInfo} | {scaleInfo} | {refInfo} | {layoutInfo} | {menuInfo}", this);
    }

    void RequestStabilizedLayoutRefresh(string context)
    {
        if (!isActiveAndEnabled)
            return;

        if (_stabilizedRefreshRoutine != null)
            StopCoroutine(_stabilizedRefreshRoutine);

        _stabilizedRefreshRoutine = StartCoroutine(StabilizedLayoutRefreshRoutine(context));
    }

    IEnumerator StabilizedLayoutRefreshRoutine(string context)
    {
        if (referenceRect == null)
            referenceRect = ResolveReferenceRect();

        Vector2 prev = Vector2.zero;
        int stableFrames = 0;

        for (int i = 0; i < 12; i++)
        {
            yield return null;
            Canvas.ForceUpdateCanvases();

            if (referenceRect == null)
                yield break;

            Vector2 current = referenceRect.rect.size;

            if ((current - prev).sqrMagnitude < 0.01f)
                stableFrames++;
            else
                stableFrames = 0;

            prev = current;

            if (stableFrames >= 2)
                break;
        }

        ApplyResolvedLayout(context);
        _stabilizedRefreshRoutine = null;
    }

    IEnumerator StabilizeLayoutBeforeShow()
    {
        if (referenceRect == null)
            referenceRect = ResolveReferenceRect();

        if (layoutRoot == null)
            layoutRoot = ResolveLayoutRoot();

        ForceApplySafeFrameViewportNow();
        Canvas.ForceUpdateCanvases();

        Vector2 prev = Vector2.zero;
        int stableFrames = 0;

        for (int i = 0; i < 12; i++)
        {
            yield return null;

            ForceApplySafeFrameViewportNow();
            Canvas.ForceUpdateCanvases();

            if (referenceRect == null)
                yield break;

            Vector2 current = referenceRect.rect.size;

            if (enableSurgicalLogs)
                Debug.Log($"{LOG} StabilizeLayoutBeforeShow | frame={i} | RefSize={current}", this);

            if ((current - prev).sqrMagnitude < 0.01f)
                stableFrames++;
            else
                stableFrames = 0;

            prev = current;

            if (stableFrames >= 2)
                break;
        }

        ApplyResolvedLayout("StabilizeLayoutBeforeShow");
    }

    void ForceApplySafeFrameViewportNow()
    {
        if (referenceRect == null)
            return;

        UICameraViewportFitterPixelRect fitter = referenceRect.GetComponent<UICameraViewportFitterPixelRect>();
        if (fitter == null)
            return;

        fitter.ForceApplyNow();
    }

    void Update()
    {
        WatchForRuntimeLayoutChanges();
    }

    void WatchForRuntimeLayoutChanges()
    {
        if (!isActiveAndEnabled)
            return;

        bool changed = false;
        string reason = "";

        if (Screen.width != _lastScreenWidth || Screen.height != _lastScreenHeight)
        {
            reason += $"Screen({_lastScreenWidth}x{_lastScreenHeight} -> {Screen.width}x{Screen.height}) ";
            _lastScreenWidth = Screen.width;
            _lastScreenHeight = Screen.height;
            changed = true;
        }

        if (referenceRect == null)
            referenceRect = ResolveReferenceRect();

        if (referenceRect != null)
        {
            Vector2 currentRefSize = referenceRect.rect.size;
            if ((currentRefSize - _lastReferenceRectSize).sqrMagnitude > 0.01f)
            {
                reason += $"RefSize({_lastReferenceRectSize} -> {currentRefSize}) ";
                _lastReferenceRectSize = currentRefSize;
                changed = true;
            }
        }

        if (changed)
        {
            if (enableSurgicalLogs)
                Debug.Log($"{LOG} WatchForRuntimeLayoutChanges => {reason}", this);

            RequestStabilizedLayoutRefresh("RuntimeResolutionChange");
        }
    }

    void OnRectTransformDimensionsChange()
    {
        if (!isActiveAndEnabled)
            return;

        RequestStabilizedLayoutRefresh("OnRectTransformDimensionsChange");
    }
}
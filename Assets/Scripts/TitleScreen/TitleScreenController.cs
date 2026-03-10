using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(RawImage))]
public class TitleScreenController : MonoBehaviour
{
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

    [Header("UI / Title")]
    public RawImage titleScreenRawImage;

    [Tooltip("Sprite 256x224 para a tela de título (pixel perfect).")]
    [SerializeField] Sprite titleScreenSprite;

    [Header("Menu Text (TMP)")]
    public TMP_Text menuText;

    [Header("Menu Layout (BASE @ designUpscale)")]
    [SerializeField] Vector2 menuAnchoredPos = new(-70f, 55f);
    [SerializeField] int menuFontSize = 46;

    [Header("Extra Offsets (BASE @ designUpscale)")]
    [SerializeField] float menuExtraYOffset = -10f;
    [SerializeField] float pushStartExtraYOffset = -8f;

    [Header("Menu Centering")]
    [SerializeField] bool forceMenuCenterX = true;

    [Header("Dynamic Scale (Pixel Perfect friendly)")]
    [SerializeField] bool dynamicScale = true;

    [SerializeField] int referenceWidth = 256;
    [SerializeField] int referenceHeight = 224;

    [Tooltip("If true, uses integer upscale steps like PixelPerfectCamera.")]
    [SerializeField] bool useIntegerUpscale = true;

    [Tooltip("The upscale you tuned your BASE sizes for. Example: FullHD looks like ~4x, set 4.")]
    [SerializeField, Min(1)] int designUpscale = 4;

    [Tooltip("Optional extra multiplier after normalization.")]
    [SerializeField, Min(0.01f)] float extraScaleMultiplier = 1f;

    [SerializeField, Min(0.01f)] float minScale = 0.5f;
    [SerializeField, Min(0.01f)] float maxScale = 10f;

    [Header("Text Style (SB5-like)")]
    [SerializeField] bool forceBold = true;

    [Header("Outline (TMP SDF)")]
    [SerializeField] Color outlineColor = Color.black;
    [SerializeField, Range(0f, 1f)] float outlineWidth = 0.42f;
    [SerializeField, Range(0f, 1f)] float outlineSoftness = 0.0f;

    [Header("Face Thickness (TMP SDF)")]
    [SerializeField, Range(-1f, 1f)] float faceDilate = 0.38f;
    [SerializeField, Range(0f, 1f)] float faceSoftness = 0.0f;

    [Header("Underlay (Shadow)")]
    [SerializeField] bool enableUnderlay = true;
    [SerializeField] Color underlayColor = new(0f, 0f, 0f, 1f);
    [SerializeField, Range(-1f, 1f)] float underlayDilate = 0.18f;
    [SerializeField, Range(0f, 1f)] float underlaySoftness = 0.0f;
    [SerializeField, Range(-2f, 2f)] float underlayOffsetX = 0.35f;
    [SerializeField, Range(-2f, 2f)] float underlayOffsetY = -0.35f;

    [Header("Video Settings")]
    [SerializeField] bool allowVideoMenu = true;
    [SerializeField] bool defaultFullscreen = true;
    [SerializeField] int defaultWindowSizeMultiplier = 4;
    [SerializeField] int[] windowSizeMultipliers = new int[] { 2, 3, 4, 5, 6, 7, 8 };

    [Header("Video Values (Separate TMP)")]
    [SerializeField] TextMeshProUGUI videoValuesText;

    [Tooltip("Recuo da borda direita da camera/canvas (BASE @ designUpscale).")]
    [SerializeField] float videoValuesRightPadding = 18f;

    [Header("Audio")]
    public AudioClip titleMusic;
    [Range(0f, 1f)] public float titleMusicVolume = 1f;

    [Header("Exit")]
    public float exitDelayRealtime = 1f;

    [Header("Start Game Timing")]
    [SerializeField] float startGameFadeOutDuration = 0.25f;

    [Header("Cursor (AnimatedSpriteRenderer)")]
    public AnimatedSpriteRenderer cursorRenderer;
    [SerializeField] Vector2 cursorOffset = new(-30f, 0f);
    [SerializeField] bool cursorAsChildOfMenuText = true;

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
    [SerializeField] float footerShowSeconds = 2.0f;

    [Header("Boss Rush Lock (separate bottom message)")]
    [SerializeField] bool bossRushUnlocked = true;
    [SerializeField, Range(0.05f, 1f)] float bossRushLockedAlpha = 0.35f;
    private readonly string bossRushLockedMessage = "UNLOCKED BY COMPLETING NORMAL MODE";
    [SerializeField] string bossRushLockedMessageHex = "#FF3B30";
    [SerializeField] TextMeshProUGUI bossRushLockedText;
    [SerializeField] int bossRushLockedFontSize = 34;
    [SerializeField] float bossRushLockedBottomMargin = 8f;
    [SerializeField] float bossRushLockedShowSeconds = 2.0f;

    [Header("Controls Scene")]
    [SerializeField] string controlsSceneName = "ControlsMenu";

    public bool ControlsRequested { get; private set; }

    RectTransform cursorRect;

    public bool Running { get; private set; }
    public bool NormalGameRequested { get; private set; }
    public bool BossRushRequested { get; private set; }
    public bool ExitRequested { get; private set; }

    enum MenuMode
    {
        Main = 0,
        PlayerCount = 1,
        Video = 2
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
    const int MAIN_IDX_CONTROLS = 2;
    const int MAIN_IDX_VIDEO = 3;
    const int MAIN_IDX_EXIT = 4;

    const int VIDEO_IDX_FULLSCREEN = 0;
    const int VIDEO_IDX_WINDOWSIZE = 1;

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

    Rect _lastCamRect;
    int _lastBaseScaleInt = -999;
    float _lastUiScale = -999f;

    float _currentUiScale = 1f;
    int _currentBaseScaleInt = 1;

    Vector3 _cursorBaseLocalScale = Vector3.one;
    bool _cursorBaseScaleCaptured;

    bool _videoFullscreen;

    int _videoWindowMult;
    int _videoWindowMultWindowed;

    const string PREF_FULLSCREEN = "ts_video_fullscreen";
    const string PREF_WINMULT = "ts_video_window_mult";

    Coroutine _postResolutionRefreshRoutine;

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

    void Awake()
    {
        if (titleScreenRawImage == null)
            titleScreenRawImage = GetComponent<RawImage>();

        if (menuText != null)
            menuRect = menuText.rectTransform;

        if (cursorRenderer != null)
        {
            cursorRect = cursorRenderer.GetComponent<RectTransform>();

            if (cursorAsChildOfMenuText && menuText != null)
                cursorRenderer.transform.SetParent(menuText.transform, false);

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
        _lastCamRect = default;
        _lastBaseScaleInt = -999;
        _lastUiScale = -999f;

        _currentUiScale = 1f;
        _currentBaseScaleInt = 1;

        ApplyDynamicLayoutIfNeeded(true, "OnEnable");
    }

    void Update()
    {
        ApplyDynamicLayoutIfNeeded(false, "Update");
    }

    void OnDestroy()
    {
        if (runtimeMenuMat != null)
            Destroy(runtimeMenuMat);
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
            if (idx < 0) _videoWindowMultWindowed = windowSizeMultipliers[0];
        }
        else
        {
            _videoWindowMultWindowed = Mathf.Max(1, _videoWindowMultWindowed);
        }

        if (_videoFullscreen)
            _videoWindowMult = GetBestWindowMultiplierForCurrentMonitor();
        else
            _videoWindowMult = _videoWindowMultWindowed;
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

        var res = Screen.currentResolution;
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
        if (!allowVideoMenu) return;

        var curRes = Screen.currentResolution;

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

    Coroutine _fullscreenWatchdog;

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
                var r = Screen.currentResolution;
                Screen.SetResolution(r.width, r.height, FullScreenMode.FullScreenWindow);
                Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
                Screen.fullScreen = true;
            }
        }

        _fullscreenWatchdog = null;
    }

    void ForceRecomputeLayoutAfterResolutionChange()
    {
        _lastCamRect = default;
        _lastBaseScaleInt = -999;
        _lastUiScale = -999f;

        if (_postResolutionRefreshRoutine != null)
            StopCoroutine(_postResolutionRefreshRoutine);

        _postResolutionRefreshRoutine = StartCoroutine(PostResolutionRefreshRoutine());
    }

    IEnumerator PostResolutionRefreshRoutine()
    {
        yield return null;
        ApplyDynamicLayoutIfNeeded(true, "PostResChange:f1");

        yield return null;
        ApplyDynamicLayoutIfNeeded(true, "PostResChange:f2");

        yield return new WaitForSecondsRealtime(0.05f);
        ApplyDynamicLayoutIfNeeded(true, "PostResChange:0.05s");

        _postResolutionRefreshRoutine = null;
    }

    static int IndexOf(int[] arr, int value)
    {
        if (arr == null) return -1;
        for (int i = 0; i < arr.Length; i++)
            if (arr[i] == value) return i;
        return -1;
    }

    float ComputeUiScaleForSize(float usedW, float usedH, out int baseScaleInt, out float baseScaleRawOut)
    {
        baseScaleInt = 1;
        baseScaleRawOut = 1f;

        if (!dynamicScale)
        {
            if (menuText == null) return 1f;
            var c = menuText.canvas;
            if (c == null) return 1f;
            return Mathf.Max(0.01f, c.scaleFactor);
        }

        float sx = usedW / Mathf.Max(1f, referenceWidth);
        float sy = usedH / Mathf.Max(1f, referenceHeight);
        float baseScaleRaw = Mathf.Min(sx, sy);
        baseScaleRawOut = baseScaleRaw;

        float baseScaleForUi = useIntegerUpscale ? Mathf.Round(baseScaleRaw) : baseScaleRaw;
        if (baseScaleForUi < 1f) baseScaleForUi = 1f;

        baseScaleInt = Mathf.Max(1, Mathf.RoundToInt(baseScaleForUi));

        float normalized = baseScaleInt / Mathf.Max(1f, designUpscale);

        float ui = normalized * Mathf.Max(0.01f, extraScaleMultiplier);
        ui = Mathf.Clamp(ui, minScale, maxScale);

        return ui;
    }

    void ApplyDynamicLayoutIfNeeded(bool force, string where)
    {
        var cam = Camera.main;
        Rect camRect = cam != null ? cam.pixelRect : new Rect(0, 0, Screen.width, Screen.height);

        float usedW = Screen.width;
        float usedH = Screen.height;

        if (_videoFullscreen)
        {
            var res = Screen.currentResolution;
            if (res.width > 0 && res.height > 0)
            {
                usedW = res.width;
                usedH = res.height;
            }
        }

        float ui = ComputeUiScaleForSize(usedW, usedH, out int baseScaleInt, out float baseScaleRaw);

        bool rectChanged = camRect != _lastCamRect;
        bool scaleChanged = Mathf.Abs(ui - _lastUiScale) > 0.0001f || baseScaleInt != _lastBaseScaleInt;

        if (!force && !rectChanged && !scaleChanged)
            return;

        _currentUiScale = ui;
        _currentBaseScaleInt = baseScaleInt;

        _lastCamRect = camRect;
        _lastUiScale = ui;
        _lastBaseScaleInt = baseScaleInt;

        ApplyMenuAnchoredPosition();
        ApplyCursorScale();

        EnsurePushStartText();
        EnsureFooterText();
        EnsureBossRushLockedText();
        EnsureVideoValuesText();

        RefreshMenuText();
        UpdateCursorPosition();
        UpdatePushStartPosition();
        UpdateFooterPosition();
        UpdateBossRushLockedPosition();
        UpdateVideoValuesPosition();
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
        var baseScale = _cursorBaseLocalScale;
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

        ApplyMenuAnchoredPosition();

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

        if (pushStartText != null)
            pushStartText.fontMaterial = runtimeMenuMat;

        if (footerText != null)
            footerText.fontMaterial = runtimeMenuMat;

        if (bossRushLockedText != null)
            bossRushLockedText.fontMaterial = runtimeMenuMat;

        if (videoValuesText != null)
            videoValuesText.fontMaterial = runtimeMenuMat;
    }

    void ApplyMenuAnchoredPosition()
    {
        if (menuRect == null)
            menuRect = menuText != null ? menuText.rectTransform : null;

        if (menuRect == null)
            return;

        Vector2 pos = MenuAnchoredPosScaled;
        if (forceMenuCenterX)
            pos.x = 0f;

        pos.y += MenuExtraYOffsetScaled;

        menuRect.anchoredPosition = pos;
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
        if (menuText == null) return null;

        var parent = menuText.transform.parent as RectTransform;
        if (parent != null) return parent;

        return menuText.rectTransform;
    }

    void EnsureVideoValuesText()
    {
        if (menuText == null)
            return;

        if (videoValuesText == null)
        {
            var go = new GameObject("VideoValuesText", typeof(RectTransform));
            var root = GetUiRootForGeneratedText();
            if (root != null) go.transform.SetParent(root, false);
            else go.transform.SetParent(menuText.transform, false);

            videoValuesText = go.AddComponent<TextMeshProUGUI>();
            videoValuesText.raycastTarget = false;
        }

        videoValuesRect = videoValuesText.rectTransform;

        if (videoValuesRect != null)
        {
            videoValuesRect.anchorMin = new Vector2(1f, 0.5f);
            videoValuesRect.anchorMax = new Vector2(1f, 0.5f);
            videoValuesRect.pivot = new Vector2(1f, 1f);
            videoValuesRect.sizeDelta = Vector2.zero;
            videoValuesRect.localScale = Vector3.one;
        }

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

        if (videoValuesText != null)
            videoValuesText.gameObject.SetActive(false);
    }

    void EnsurePushStartText()
    {
        if (menuText == null)
            return;

        if (pushStartText == null)
        {
            var go = new GameObject("PushStartText", typeof(RectTransform));
            var root = GetUiRootForGeneratedText();
            if (root != null) go.transform.SetParent(root, false);
            else go.transform.SetParent(menuText.transform, false);

            pushStartText = go.AddComponent<TextMeshProUGUI>();
            pushStartText.raycastTarget = false;
        }

        pushStartRect = pushStartText.rectTransform;

        if (pushStartRect != null)
        {
            pushStartRect.anchorMin = new Vector2(0f, 0.5f);
            pushStartRect.anchorMax = new Vector2(1f, 0.5f);
            pushStartRect.pivot = new Vector2(0.5f, 0.5f);
            pushStartRect.sizeDelta = Vector2.zero;
            pushStartRect.localScale = Vector3.one;
        }

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
            var go = new GameObject("FooterText", typeof(RectTransform));
            var root = GetUiRootForGeneratedText();
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
            var go = new GameObject("BossRushLockedText", typeof(RectTransform));
            var root = GetUiRootForGeneratedText();
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

        if (bossRushLockedRect != null)
        {
            bossRushLockedRect.anchorMin = new Vector2(0f, 0f);
            bossRushLockedRect.anchorMax = new Vector2(1f, 0f);
            bossRushLockedRect.pivot = new Vector2(0.5f, 0f);
            bossRushLockedRect.anchoredPosition = new Vector2(0f, BossRushLockedBottomMarginScaled);
            bossRushLockedRect.sizeDelta = new Vector2(0f, 0f);
            bossRushLockedRect.localScale = Vector3.one;
        }
    }

    void UpdateBossRushLockedPosition()
    {
        if (bossRushLockedRect == null || bossRushLockedText == null)
            return;

        if (!bossRushLockedText.gameObject.activeSelf)
            return;

        bossRushLockedText.fontSize = BossRushLockedFontSizeScaled;
        bossRushLockedRect.anchoredPosition = new Vector2(0f, BossRushLockedBottomMarginScaled);
    }

    void UpdateVideoValuesPosition()
    {
        if (videoValuesText == null || videoValuesRect == null || menuText == null)
            return;

        bool show = (menuMode == MenuMode.Video) && allowVideoMenu;
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
        var ti = menuText.textInfo;
        if (ti == null || ti.lineCount <= 0)
            return;

        var root = GetUiRootForGeneratedText();
        if (root == null)
            return;

        var li0 = ti.lineInfo[0];
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

        if (titleScreenRawImage != null)
            titleScreenRawImage.gameObject.SetActive(false);

        if (menuText != null)
            menuText.gameObject.SetActive(false);

        if (cursorRenderer != null)
            cursorRenderer.gameObject.SetActive(false);

        if (pushStartText != null)
            pushStartText.gameObject.SetActive(false);

        if (footerText != null)
            footerText.gameObject.SetActive(false);

        if (bossRushLockedText != null)
            bossRushLockedText.gameObject.SetActive(false);

        if (videoValuesText != null)
            videoValuesText.gameObject.SetActive(false);
    }

    void ApplyTitleVisualNow()
    {
        if (titleScreenRawImage == null)
            return;

        titleScreenRawImage.gameObject.SetActive(true);

        if (titleScreenSprite != null)
        {
            var tex = titleScreenSprite.texture;
            if (tex != null)
            {
                tex.filterMode = FilterMode.Point;
                tex.wrapMode = TextureWrapMode.Clamp;
            }

            titleScreenRawImage.texture = tex;

            var r = titleScreenSprite.textureRect;
            var tr = tex != null ? new Rect(0f, 0f, tex.width, tex.height) : r;
            var uv = tex != null && tr.width > 0f && tr.height > 0f
                ? new Rect(r.x / tr.width, r.y / tr.height, r.width / tr.width, r.height / tr.height)
                : new Rect(0f, 0f, 1f, 1f);

            titleScreenRawImage.uvRect = uv;
        }
    }

    void ShowTitleScreenNow()
    {
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

        ApplyDynamicLayoutIfNeeded(true, "ShowTitleScreenNow");
    }

    bool TryGetAnyPlayerDown(PlayerAction action, out int pid)
    {
        pid = 1;

        var input = PlayerInputManager.Instance;
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
        var input = PlayerInputManager.Instance;
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
            fadeToHideOptional.gameObject.SetActive(false);

        ShowTitleScreenNow();

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

            if ((menuMode == MenuMode.PlayerCount || menuMode == MenuMode.Video) && TryGetAnyPlayerDown(PlayerAction.ActionB, out _))
            {
                PlayBackSfx();

                menuMode = MenuMode.Main;
                menuIndex = 0;
                pendingStartFlow = StartFlowMode.None;

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

                        if (_videoFullscreen)
                            _videoWindowMult = GetBestWindowMultiplierForCurrentMonitor();
                        else
                            _videoWindowMult = Mathf.Max(1, _videoWindowMultWindowed);

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

            if (TryGetAnyPlayerDownEither(PlayerAction.Start, PlayerAction.ActionA, out int pidConfirm))
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
                        if (!bossRushUnlocked)
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

                    locked = true;
                    PlaySelectSfx();

                    if (cursorRenderer != null)
                        yield return cursorRenderer.PlayCycles(2);

                    if (menuIndex == MAIN_IDX_CONTROLS)
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

                    if (menuIndex == MAIN_IDX_VIDEO)
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

                    ExitRequested = true;
                    yield return ExitGame();
                    yield break;
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
        if (menuMode == MenuMode.PlayerCount)
            return 4;

        if (menuMode == MenuMode.Video)
            return 2;

        return 5;
    }

    IEnumerator StartSelectedGameFlow()
    {
        float d = Mathf.Max(0.01f, startGameFadeOutDuration);

        var transition = StageIntroTransition.Instance;
        if (transition != null)
            transition.StartFadeOut(d, false);

        yield return new WaitForSecondsRealtime(d);

        if (GameMusicController.Instance != null)
            GameMusicController.Instance.StopMusic();

        if (titleScreenRawImage != null)
            titleScreenRawImage.gameObject.SetActive(false);

        if (menuText != null)
            menuText.gameObject.SetActive(false);

        if (cursorRenderer != null)
            cursorRenderer.gameObject.SetActive(false);

        if (pushStartText != null)
            pushStartText.gameObject.SetActive(false);

        if (footerText != null)
            footerText.gameObject.SetActive(false);

        if (bossRushLockedText != null)
            bossRushLockedText.gameObject.SetActive(false);

        if (videoValuesText != null)
            videoValuesText.gameObject.SetActive(false);

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

    void RefreshMenuText()
    {
        if (menuText == null)
            return;

        const string baseRgb = "FFFFE7";
        int size = MenuFontSizeScaled;

        if (menuMode == MenuMode.Main)
        {
            string normal = $"<color=#{baseRgb}FF>NORMAL GAME</color>";
            string bossRush;
            if (bossRushUnlocked)
                bossRush = $"<color=#{baseRgb}FF>BOSS RUSH</color>";
            else
                bossRush = $"<color={ColorWithAlpha(baseRgb, bossRushLockedAlpha)}>BOSS RUSH</color>";
            string controls = $"<color=#{baseRgb}FF>CONTROLS</color>";
            string video = $"<color=#{baseRgb}FF>VIDEO</color>";
            string exit = $"<color=#{baseRgb}FF>EXIT</color>";

            menuText.text =
                "<align=left>" +
                $"<size={size}>{normal}</size>\n" +
                $"<size={size}>{bossRush}</size>\n" +
                $"<size={size}>{controls}</size>\n" +
                $"<size={size}>{video}</size>\n" +
                $"<size={size}>{exit}</size>" +
                "</align>";

            if (videoValuesText != null) videoValuesText.gameObject.SetActive(false);

            UpdateCursorPosition();
            UpdatePushStartPosition();
            UpdateFooterPosition();
            UpdateBossRushLockedPosition();
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

            UpdateCursorPosition();
            UpdatePushStartPosition();
            UpdateFooterPosition();
            UpdateBossRushLockedPosition();
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

        UpdateCursorPosition();
        UpdatePushStartPosition();
        UpdateFooterPosition();
        UpdateBossRushLockedPosition();
        UpdateVideoValuesPosition();
    }

    void StartPushStartBlink()
    {
        EnsurePushStartText();

        if (pushStartText == null)
            return;

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
        var wait = new WaitForSecondsRealtime(Mathf.Max(0.05f, pushStartBlinkInterval));

        while (Running)
        {
            pushStartVisible = !pushStartVisible;

            if (pushStartText != null)
                pushStartText.gameObject.SetActive(pushStartVisible);

            yield return wait;
        }
    }

    void UpdatePushStartPosition()
    {
        if (menuText == null || pushStartRect == null)
            return;

        var root = GetUiRootForGeneratedText();
        if (root == null)
            return;

        menuText.ForceMeshUpdate();
        var ti = menuText.textInfo;
        if (ti == null || ti.lineCount <= 0)
            return;

        var first = ti.lineInfo[0];
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
        var ti = menuText.textInfo;
        if (ti == null || ti.lineCount <= 0)
            return;

        float minX = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;

        for (int i = 0; i < ti.lineCount; i++)
        {
            var li = ti.lineInfo[i];
            minX = Mathf.Min(minX, li.lineExtents.min.x);
            maxX = Mathf.Max(maxX, li.lineExtents.max.x);
        }

        float centerX = (minX + maxX) * 0.5f;

        var last = ti.lineInfo[ti.lineCount - 1];
        float y = last.descender + FooterOffsetFromLastLineYScaled;

        footerRect.localPosition = new Vector3(centerX, y, 0f);
    }

    void ShowBossRushLockedMessage(string msg, string hex, float seconds)
    {
        EnsureBossRushLockedText();
        if (bossRushLockedText == null) return;

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

        menuText.ForceMeshUpdate();

        var ti = menuText.textInfo;
        if (ti == null || ti.lineCount <= 0)
            return;

        int line = Mathf.Clamp(menuIndex, 0, ti.lineCount - 1);
        var li = ti.lineInfo[line];

        float y = (li.ascender + li.descender) * 0.5f;
        float x = li.lineExtents.min.x;

        Vector2 offs = CursorOffsetScaled;

        Vector3 localPos = new(x + offs.x, y + offs.y, 0f);
        cursorRenderer.SetExternalBaseLocalPosition(localPos);
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
}
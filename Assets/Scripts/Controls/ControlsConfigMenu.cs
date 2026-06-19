using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class ControlsConfigMenu : MonoBehaviour
{
    [Header("Menu Owner")]
    [SerializeField, Range(1, 6)] int ownerPlayerId = 1;

    [Header("UI")]
    [SerializeField] GameObject root;
    [SerializeField] RawImage backgroundImage;

    [Header("Reference Frame")]
    [SerializeField] RectTransform referenceRect;

    [Header("Layout Root")]
    [SerializeField] RectTransform menuLayoutRoot;
    [SerializeField] float menuGlobalYOffset = 140;
    [SerializeField] bool usePixelPerfectMenuLayout = true;
    [SerializeField] Vector2 menuTextBoxReferenceSize = new(246f, 190f);
    [SerializeField] Vector2 selectMenuOffset = new(0f, 0f);
    [SerializeField] Vector2 waitMenuOffset = new(0f, 0f);
    [SerializeField] Vector2 remapMenuOffset = new(0f, 0f);
    [SerializeField, Range(0, 12)] int selectTitleToBodyGapLines = 2;

    [Header("Fade")]
    [SerializeField] Image fadeImage;
    [SerializeField] float fadeDuration = 0.5f;
    [SerializeField, Range(0.001f, 0.1f)] float maxFadeStepDelta = 0.033f;
#pragma warning disable CS0414
    [SerializeField] bool logOpenFlowDiagnostics = false;
#pragma warning restore CS0414

    [Header("Music")]
    [SerializeField] AudioClip controlsMusic;
    [SerializeField] AudioClip controlsMusicLoop;
    [SerializeField, Range(0f, 1f)] float controlsMusicVolume = 1f;

    [Header("Text (TMP)")]
    [SerializeField] TMP_Text menuText;

    [Header("Localized Font Fallback")]
    [SerializeField] TMP_FontAsset localizedFallbackFontAsset;
    [SerializeField] string[] localizedFallbackOsFontNames =
    {
        "Yu Gothic",
        "Yu Gothic UI",
        "Yu Mincho",
        "Meiryo",
        "Meiryo UI",
        "MS Gothic",
        "MS UI Gothic",
        "MS Mincho",
        "Noto Sans CJK JP",
        "Noto Sans JP",
        "Segoe UI",
        "Arial",
        "Liberation Sans"
    };
    [SerializeField, Min(16)] int localizedFallbackSamplingPointSize = 90;

    [Header("Title")]
    [SerializeField] int titleFontSize = 46;
    [SerializeField] float titleVOffset = 26f;
    [SerializeField] int headerTopPaddingLines = 0;
    [SerializeField] int headerBottomPaddingLines = 0;

    [Header("Body")]
    [SerializeField] int bodyFontSize = 30;

    [Header("Footer")]
    [SerializeField] TMP_Text footerText;
    [SerializeField] int footerGapLines = 0;
    [SerializeField] int footerFontSize = 22;
    [SerializeField, Range(0, 10)] int footerExtraNewLines = 0;
    [SerializeField, Range(0f, 80f)] float footerBottomPadding = 12f;

    [Header("Select Player Blocks")]
    [SerializeField] int selectGridFontSize = 24;
    [SerializeField, Range(0, 6)] int playerBlockGapLines = 1;

    [Header("Effective Minimum Font Sizes")]
    [SerializeField] int minTitleFontSize = 42;
    [SerializeField] int minBodyFontSize = 34;
    [SerializeField] int minFooterFontSize = 26;
    [SerializeField] int minSelectGridFontSize = 32;
    [SerializeField] int minRemapGridFontSize = 30;

    [Header("Text Style")]
    [SerializeField] bool forceBold = true;

    [Header("Outline")]
    [SerializeField] Color outlineColor = Color.black;
    [SerializeField, Range(0f, 1f)] float outlineWidth = 0.42f;
    [SerializeField, Range(0f, 1f)] float outlineSoftness = 0.0f;

    [Header("Cursor")]
    [SerializeField] AnimatedSpriteRenderer cursorRenderer;
    [SerializeField] Vector2 cursorOffset = new(-100f, 0f);
    [SerializeField, Range(0f, 200f)] float cursorGapLeft = 18f;
    [SerializeField, Range(-40f, 40f)] float cursorLineCenterAdjustY = 6f;

    [Header("SFX")]
    [SerializeField] AudioClip confirmSfx;
    [SerializeField, Range(0f, 1f)] float confirmVolume = 1f;

    [SerializeField] AudioClip moveOptionSfx;
    [SerializeField, Range(0f, 1f)] float moveOptionVolume = 1f;

    [SerializeField] AudioClip backSfx;
    [SerializeField, Range(0f, 1f)] float backVolume = 1f;

    [SerializeField] AudioClip resetSfx;
    [SerializeField, Range(0f, 1f)] float resetVolume = 1f;

    [SerializeField] AudioClip lockedSfx;
    [SerializeField, Range(0f, 1f)] float lockedVolume = 1f;

    [Header("Players Block - Global Indent")]
    [SerializeField] float playersBlockIndentX = 400f;

    [Header("Dynamic Scale")]
    [SerializeField] bool dynamicScale = true;
    [SerializeField] int referenceWidth = 256;
    [SerializeField] int referenceHeight = 224;
    [SerializeField] bool useIntegerUpscale = true;
    [SerializeField, Min(1)] int designUpscale = 4;
    [SerializeField, Min(0.01f)] float extraScaleMultiplier = 1f;
    [SerializeField, Min(0.01f)] float minScale = 0.5f;
    [SerializeField, Min(0.01f)] float maxScale = 10f;

    [Header("Cursor Scaling")]
    [SerializeField] bool scaleCursorWithUi = true;

    [Header("Cursor Rounding")]
    [SerializeField] bool roundCursorToWholePixels = true;

    Vector2 _lastMouseScreenPosition;
    bool _hasLastMouseScreenPosition;

    const string colorNormal = "#FFFFE7";
    const string colorHint = "#FFA621";
    const string colorWhite = "#FFFFFF";
    const string colorBlueSoft = "#8FD3FF";
    const string colorPlayerGreen = "#8CFFB3";
    const string colorPlayerSelectedRed = "#FF5A5A";

    const int MaxConfigurablePlayers = GameSession.MaxPlayerId;

    const float COLUMN_LEFT_LABEL_BASE = -350f;
    const float COLUMN_LEFT_VALUE_BASE = -190f;
    const float COLUMN_RIGHT_LABEL_BASE = 200f;
    const float COLUMN_RIGHT_VALUE_BASE = 260f;

    const string LINK_WAIT_START = "wait_start";
    const string LINK_WAIT_PREFIX = "wait_";
    const string LINK_RESET_YES = "reset_yes";
    const string LINK_RESET_NO = "reset_no";

    enum MenuState
    {
        SelectPlayer,
        ConfirmReset,
        WaitForInput,
        BulkRemap
    }

    MenuState state;

    int playerSelectIndex;
    int targetPlayerId;
    int bulkStep;

    int confirmResetIndex;
    int confirmResetPlayerId;

    Material runtimeMenuMat;
    readonly List<TMP_FontAsset> runtimeLanguageFallbackFontAssets = new();
    bool warnedMissingLanguageFallback;
    Coroutine cursorPulseRoutine;
    Dictionary<PlayerAction, Binding> bulkSnapshot;

    Vector2 menuLayoutRootBasePos;
    bool menuLayoutRootCached;

    float blockedMessageUntil;
    string blockedMessageLine;

    float _currentUiScale = 1f;
    int _currentBaseScaleInt = 1;

    int _lastScreenW = -1;
    int _lastScreenH = -1;

    Rect _lastCameraRect;
    Rect _lastRefPixelRect;

    Vector2 _menuTextBaseSizeDelta;
    bool _menuTextBaseCaptured;

    Vector3 _cursorBaseLocalScale = Vector3.one;
    bool _cursorBaseScaleCaptured;

    static readonly PlayerAction[] BulkActions = new[]
    {
        PlayerAction.MoveUp,
        PlayerAction.MoveDown,
        PlayerAction.MoveLeft,
        PlayerAction.MoveRight,
        PlayerAction.Start,
        PlayerAction.ActionA,
        PlayerAction.ActionB,
        PlayerAction.ActionC,
        PlayerAction.ActionL,
        PlayerAction.ActionR
    };

    struct PointerState
    {
        public bool valid;
        public Vector2 screenPosition;
        public bool pressedThisFrame;
        public bool secondaryPressedThisFrame;
    }

    int ScaledFont(int baseSize) => Mathf.Clamp(Mathf.RoundToInt(baseSize * _currentUiScale), 10, 500);
    int EffectiveTitleFontSize => Mathf.Max(titleFontSize, minTitleFontSize);
    int EffectiveBodyFontSize => Mathf.Max(bodyFontSize, minBodyFontSize);
    int EffectiveFooterFontSize => Mathf.Max(footerFontSize, minFooterFontSize);
    int EffectiveSelectGridFontSize => Mathf.Max(selectGridFontSize, minSelectGridFontSize);
    int EffectiveRemapGridFontSize => Mathf.Max(selectGridFontSize, minRemapGridFontSize);
    int EffectiveSelectTitleToBodyGapLines => Mathf.Max(selectTitleToBodyGapLines, 5);
    int EffectiveWaitTitleToBodyGapLines => Mathf.Max(selectTitleToBodyGapLines, 5);
    int EffectiveConfirmTitleToBodyGapLines => Mathf.Max(selectTitleToBodyGapLines, 5);
    int EffectiveRemapTitleToBodyGapLines => Mathf.Max(selectTitleToBodyGapLines, 3);
    float ScaledFloat(float baseValue) => baseValue * _currentUiScale;

    Canvas GetRootCanvas()
    {
        if (menuText != null)
            return menuText.canvas;

        if (root != null)
            return root.GetComponentInParent<Canvas>();

        return GetComponentInParent<Canvas>();
    }

    Camera GetMainCameraSafe()
    {
        var cam = Camera.main;
        if (cam != null) return cam;
        return UnityEngine.Object.FindAnyObjectByType<Camera>();
    }

    Rect GetReferencePixelRect(out string source)
    {
        source = "NONE";

        var canvas = GetRootCanvas();
        if (canvas == null)
        {
            source = "NO_CANVAS";
            return new Rect(0, 0, Screen.width, Screen.height);
        }

        RectTransform rt = referenceRect;
        if (rt == null)
        {
            if (menuLayoutRoot != null) rt = menuLayoutRoot;
            else if (root != null) rt = root.GetComponent<RectTransform>();
            else if (menuText != null) rt = menuText.rectTransform;
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
        float baseScaleForUi = usePixelPerfectMenuLayout
            ? baseScaleRaw
            : (useIntegerUpscale ? Mathf.Floor(baseScaleRaw) : baseScaleRaw);

        if (baseScaleForUi < 1f) baseScaleForUi = 1f;

        baseScaleInt = Mathf.Max(1, Mathf.RoundToInt(baseScaleForUi));

        float normalized = baseScaleInt / Mathf.Max(1f, designUpscale);
        float ui = normalized * Mathf.Max(0.01f, extraScaleMultiplier);
        ui = Mathf.Clamp(ui, minScale, maxScale);
        return ui;
    }

    Vector2 GetReferenceRectSize()
    {
        RectTransform rt = referenceRect;
        if (rt == null)
        {
            if (menuLayoutRoot != null) rt = menuLayoutRoot;
            else if (root != null) rt = root.GetComponent<RectTransform>();
            else if (menuText != null) rt = menuText.rectTransform;
        }

        if (rt != null)
        {
            Rect r = rt.rect;
            if (r.width > 1f && r.height > 1f)
                return r.size;
        }

        Rect px = GetReferencePixelRect(out _);
        return px.size;
    }

    float ComputeReferencePixelScale()
    {
        Vector2 size = GetReferenceRectSize();
        float sx = size.x / Mathf.Max(1f, referenceWidth);
        float sy = size.y / Mathf.Max(1f, referenceHeight);
        return Mathf.Max(1f, Mathf.Min(sx, sy));
    }

    void ApplyDynamicScaleIfNeeded(bool force = false)
    {
        int sw = Screen.width;
        int sh = Screen.height;

        var cam = GetMainCameraSafe();
        Rect camRect = cam != null ? cam.rect : new Rect(0, 0, 1, 1);

        Rect refPx = GetReferencePixelRect(out _);

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

        ApplyCursorScale();
        ApplyMenuGlobalOffset();
        ApplyMenuTextRectScale();
        RefreshText();
    }

    static bool ApproximatelyRect(Rect a, Rect b)
    {
        return
            Mathf.Abs(a.x - b.x) < 0.01f &&
            Mathf.Abs(a.y - b.y) < 0.01f &&
            Mathf.Abs(a.width - b.width) < 0.01f &&
            Mathf.Abs(a.height - b.height) < 0.01f;
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
        cursorRenderer.transform.localScale = new Vector3(
            _cursorBaseLocalScale.x * s,
            _cursorBaseLocalScale.y * s,
            _cursorBaseLocalScale.z
        );
    }

    void Awake()
    {
        if (root == null)
            root = gameObject;

        SetupMenuTextMaterial();
        EnsureFooterText();
        ResolveMenuLayoutRoot();
        CacheMenuLayoutRootBasePos();

        if (root != null)
            root.SetActive(false);

        if (menuText != null && !_menuTextBaseCaptured)
        {
            _menuTextBaseSizeDelta = menuText.rectTransform.sizeDelta;
            _menuTextBaseCaptured = true;
        }

        if (backgroundImage != null)
            backgroundImage.gameObject.SetActive(false);

        if (cursorRenderer != null)
        {
            _cursorBaseLocalScale = cursorRenderer.transform.localScale;
            _cursorBaseScaleCaptured = true;
            cursorRenderer.gameObject.SetActive(false);
        }

        _lastCameraRect = new Rect(-999, -999, -999, -999);
        _lastRefPixelRect = new Rect(-999, -999, -999, -999);

    }

    void ApplyMenuTextRectScale()
    {
        if (menuText == null || !_menuTextBaseCaptured)
            return;

        var rt = menuText.rectTransform;

        if (usePixelPerfectMenuLayout)
        {
            float pixelScale = ComputeReferencePixelScale();
            Vector2 size = new(
                Mathf.Round(menuTextBoxReferenceSize.x * pixelScale),
                Mathf.Round(menuTextBoxReferenceSize.y * pixelScale)
            );

            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = size;
            ApplyFooterTextRect(size);
            return;
        }

        rt.sizeDelta = _menuTextBaseSizeDelta * _currentUiScale;
        ApplyFooterTextRect(rt.sizeDelta);
    }

    void ApplyFooterTextRect(Vector2 menuTextSize)
    {
        EnsureFooterText();

        if (footerText == null)
            return;

        var rt = footerText.rectTransform;
        int footerSize = ScaledFont(EffectiveFooterFontSize);
        int footerLineCount = EstimateFooterLineCount();
        float footerHeight = Mathf.Ceil((footerSize * ((footerLineCount * 1.25f) + 0.35f)) + ScaledFloat(footerBottomPadding));
        float bottomPadding = ScaledFloat(footerBottomPadding);
        float footerAreaHeight = usePixelPerfectMenuLayout
            ? GetReferenceRectSize().y
            : menuTextSize.y;

        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(menuTextSize.x, footerHeight);
        rt.anchoredPosition = new Vector2(
            0f,
            Mathf.Round((-footerAreaHeight * 0.5f) + (footerHeight * 0.5f) + bottomPadding)
        );
    }

    int EstimateFooterLineCount()
    {
        bool hasBlockedMessage = Time.unscaledTime < blockedMessageUntil && !string.IsNullOrEmpty(blockedMessageLine);
        return state switch
        {
            MenuState.BulkRemap => hasBlockedMessage ? 9 : 8,
            MenuState.SelectPlayer => hasBlockedMessage ? 5 : 4,
            MenuState.WaitForInput => 1,
            _ => 3
        };
    }

    void OnDisable()
    {
        if (backgroundImage != null)
            backgroundImage.gameObject.SetActive(false);
    }

    void Update()
    {
        if (root != null && root.activeInHierarchy)
            ApplyDynamicScaleIfNeeded(false);
    }

    void OnDestroy()
    {
        if (runtimeMenuMat != null)
            Destroy(runtimeMenuMat);

        for (int i = 0; i < runtimeLanguageFallbackFontAssets.Count; i++)
        {
            if (runtimeLanguageFallbackFontAssets[i] != null)
                Destroy(runtimeLanguageFallbackFontAssets[i]);
        }

        runtimeLanguageFallbackFontAssets.Clear();
    }

    void ResolveMenuLayoutRoot()
    {
        if (menuLayoutRoot != null)
            return;

        if (root != null && root.TryGetComponent<RectTransform>(out var rt))
        {
            menuLayoutRoot = rt;
            return;
        }

        if (menuText != null)
        {
            var parentRt = menuText.transform.parent as RectTransform;
            if (parentRt != null)
            {
                menuLayoutRoot = parentRt;
                return;
            }

            var textRt = menuText.rectTransform;
            if (textRt != null)
            {
                menuLayoutRoot = textRt;
                return;
            }
        }
    }

    void CacheMenuLayoutRootBasePos()
    {
        if (menuLayoutRootCached)
            return;

        if (menuLayoutRoot != null)
        {
            menuLayoutRootBasePos = menuLayoutRoot.anchoredPosition;
            menuLayoutRootCached = true;
        }
    }

    void ApplyMenuGlobalOffset()
    {
        ResolveMenuLayoutRoot();
        CacheMenuLayoutRootBasePos();

        if (usePixelPerfectMenuLayout)
        {
            if (menuLayoutRoot == null)
                return;

            float relX = 1f;
            float relY = 1f;

            Vector2 refSize = GetReferenceRectSize();
            if (refSize.x > 1f && refSize.y > 1f)
            {
                relX = refSize.x / Mathf.Max(1f, referenceWidth * Mathf.Max(1, designUpscale));
                relY = refSize.y / Mathf.Max(1f, referenceHeight * Mathf.Max(1, designUpscale));
            }

            Vector2 offset = GetCurrentMenuOffset();
            Vector2 pos = new(
                offset.x * relX,
                (menuGlobalYOffset + offset.y) * relY
            );

            menuLayoutRoot.anchorMin = new Vector2(0.5f, 0.5f);
            menuLayoutRoot.anchorMax = new Vector2(0.5f, 0.5f);
            menuLayoutRoot.pivot = new Vector2(0.5f, 0.5f);
            menuLayoutRoot.anchoredPosition = new Vector2(Mathf.Round(pos.x), Mathf.Round(pos.y));
            return;
        }

        if (menuLayoutRoot != null && menuLayoutRootCached)
        {
            float y = menuGlobalYOffset;
            menuLayoutRoot.anchoredPosition = menuLayoutRootBasePos + new Vector2(0f, y);
        }
    }

    Vector2 GetCurrentMenuOffset()
    {
        return state switch
        {
            MenuState.WaitForInput => waitMenuOffset,
            MenuState.BulkRemap => remapMenuOffset,
            _ => selectMenuOffset
        };
    }

    void SetupMenuTextMaterial()
    {
        if (menuText == null)
            return;

        menuText.textWrappingMode = TextWrappingModes.NoWrap;
        menuText.overflowMode = TextOverflowModes.Overflow;
        menuText.extraPadding = true;

        if (forceBold)
            menuText.fontStyle |= FontStyles.Bold;

        menuText.alignment = TextAlignmentOptions.Center;
        menuText.verticalAlignment = VerticalAlignmentOptions.Top;
        Material baseMat = menuText.fontSharedMaterial;
        if (baseMat == null) baseMat = menuText.fontMaterial;
        if (baseMat == null && menuText.font != null) baseMat = menuText.font.material;
        if (baseMat == null) return;

        if (runtimeMenuMat != null)
            Destroy(runtimeMenuMat);

        runtimeMenuMat = new Material(baseMat);

        TrySetColor(runtimeMenuMat, "_OutlineColor", outlineColor);
        TrySetFloat(runtimeMenuMat, "_OutlineWidth", outlineWidth);
        TrySetFloat(runtimeMenuMat, "_OutlineSoftness", outlineSoftness);

        TrySetFloat(runtimeMenuMat, "_FaceDilate", 0f);
        TrySetFloat(runtimeMenuMat, "_FaceSoftness", 0f);

        TrySetFloat(runtimeMenuMat, "_UnderlayDilate", 0f);
        TrySetFloat(runtimeMenuMat, "_UnderlaySoftness", 0f);
        TrySetFloat(runtimeMenuMat, "_UnderlayOffsetX", 0f);
        TrySetFloat(runtimeMenuMat, "_UnderlayOffsetY", 0f);
        TrySetColor(runtimeMenuMat, "_UnderlayColor", new Color(0f, 0f, 0f, 0f));
        runtimeMenuMat.DisableKeyword("UNDERLAY_ON");
        runtimeMenuMat.DisableKeyword("UNDERLAY_INNER");

        menuText.fontMaterial = runtimeMenuMat;
        menuText.havePropertiesChanged = true;
        menuText.UpdateMeshPadding();
        menuText.SetAllDirty();

        if (footerText != null)
            SetupFooterTextStyle();
    }

    void EnsureFooterText()
    {
        if (footerText != null || menuText == null)
        {
            if (footerText != null)
                SetupFooterTextStyle();

            return;
        }

        var parent = menuText.transform.parent;
        if (parent == null)
            return;

        footerText = Instantiate(menuText, parent);
        footerText.name = $"{menuText.name}_Footer";
        footerText.text = string.Empty;
        footerText.transform.SetSiblingIndex(menuText.transform.GetSiblingIndex() + 1);
        SetupFooterTextStyle();
    }

    void SetupFooterTextStyle()
    {
        if (footerText == null)
            return;

        footerText.textWrappingMode = TextWrappingModes.NoWrap;
        footerText.overflowMode = TextOverflowModes.Overflow;
        footerText.extraPadding = true;
        footerText.alignment = TextAlignmentOptions.Center;
        footerText.verticalAlignment = VerticalAlignmentOptions.Bottom;
        footerText.raycastTarget = false;

        if (forceBold)
            footerText.fontStyle |= FontStyles.Bold;

        if (runtimeMenuMat != null)
        {
            footerText.fontMaterial = runtimeMenuMat;
            footerText.havePropertiesChanged = true;
            footerText.UpdateMeshPadding();
            footerText.SetAllDirty();
        }
    }

    void ApplyLanguageFontFallback()
    {
        GameLanguage language = SaveSystem.GetLanguage();
        if (language == GameLanguage.English)
            return;

        char probeCharacter = GetFallbackProbeCharacter(language);
        TMP_FontAsset fallback = ResolveLanguageFallbackFontAsset(probeCharacter);
        if (fallback == null)
            return;

        AddFallbackFont(menuText != null ? menuText.font : null, fallback);
        AddFallbackFont(footerText != null ? footerText.font : null, fallback);
    }

    static char GetFallbackProbeCharacter(GameLanguage language)
    {
        if (language == GameLanguage.Japanese)
            return '日';

        return language == GameLanguage.Spanish ? 'ñ' : 'ç';
    }

    TMP_FontAsset ResolveLanguageFallbackFontAsset(char probeCharacter)
    {
        if (localizedFallbackFontAsset != null)
        {
            if (localizedFallbackFontAsset.HasCharacter(probeCharacter, true, true))
                return localizedFallbackFontAsset;

            if (!warnedMissingLanguageFallback)
            {
                warnedMissingLanguageFallback = true;
                Debug.LogWarning($"[ControlsMenu] Assigned fallback font [{localizedFallbackFontAsset.name}] does not contain required character [{probeCharacter}].");
            }
        }

        for (int i = 0; i < runtimeLanguageFallbackFontAssets.Count; i++)
        {
            TMP_FontAsset fallback = runtimeLanguageFallbackFontAssets[i];
            if (fallback != null && fallback.HasCharacter(probeCharacter, false, true))
                return fallback;
        }

        if (localizedFallbackOsFontNames != null)
        {
            for (int i = 0; i < localizedFallbackOsFontNames.Length; i++)
            {
                string familyName = localizedFallbackOsFontNames[i];
                if (string.IsNullOrWhiteSpace(familyName))
                    continue;

                TMP_FontAsset created = CreateFallbackFromOsFont(familyName, probeCharacter);
                if (created == null)
                    continue;

                runtimeLanguageFallbackFontAssets.Add(created);
                return created;
            }
        }

        if (!warnedMissingLanguageFallback)
        {
            warnedMissingLanguageFallback = true;
            Debug.LogWarning($"[ControlsMenu] No TMP fallback font was found for required character [{probeCharacter}]. Assign localizedFallbackFontAsset or install a compatible OS font.");
        }

        return null;
    }

    TMP_FontAsset CreateFallbackFromOsFont(string familyName, char probeCharacter)
    {
        try
        {
            int samplingPointSize = Mathf.Max(16, localizedFallbackSamplingPointSize);
            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
                familyName,
                "Regular",
                samplingPointSize
            );

            if (fontAsset == null)
                return null;

            fontAsset.name = familyName + " Controls Fallback";
            fontAsset.atlasPopulationMode = AtlasPopulationMode.DynamicOS;
            fontAsset.isMultiAtlasTexturesEnabled = true;

            if (!fontAsset.HasCharacter(probeCharacter, false, true))
            {
                Destroy(fontAsset);
                return null;
            }

            return fontAsset;
        }
        catch (Exception)
        {
            return null;
        }
    }

    static void AddFallbackFont(TMP_FontAsset source, TMP_FontAsset fallback)
    {
        if (source == null || fallback == null || source == fallback)
            return;

        if (source.fallbackFontAssetTable == null)
            source.fallbackFontAssetTable = new List<TMP_FontAsset>();

        if (!source.fallbackFontAssetTable.Contains(fallback))
            source.fallbackFontAssetTable.Add(fallback);
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

    bool TryGetAnyPlayerDown(PlayerAction action, out int pid)
    {
        pid = 1;

        var input = PlayerInputManager.Instance;
        if (input == null) return false;

        for (int p = 1; p <= MaxConfigurablePlayers; p++)
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

        for (int p = 1; p <= MaxConfigurablePlayers; p++)
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
               AnyPlayerHeld(PlayerAction.ActionL) ||
               AnyPlayerHeld(PlayerAction.ActionR) ||
               AnyPlayerHeld(PlayerAction.Start) ||
               AnyPlayerHeld(PlayerAction.MoveUp) ||
               AnyPlayerHeld(PlayerAction.MoveDown) ||
               AnyPlayerHeld(PlayerAction.MoveLeft) ||
               AnyPlayerHeld(PlayerAction.MoveRight);
    }

    PlayerAction CurrentBulkAction()
    {
        return BulkActions[Mathf.Clamp(bulkStep, 0, BulkActions.Length - 1)];
    }

    void BeginBulkRemap()
    {
        targetPlayerId = Mathf.Clamp(targetPlayerId, 1, MaxConfigurablePlayers);

        var p = PlayerInputManager.Instance.GetPlayer(targetPlayerId);
        bulkSnapshot = p.CloneBindings();
        bulkStep = 0;
        state = MenuState.BulkRemap;
        blockedMessageUntil = 0f;
        blockedMessageLine = null;
    }

    string CurrentWaitLinkId()
    {
        return LINK_WAIT_PREFIX + CurrentBulkAction();
    }

    bool IsPlayerActiveForMenu(int playerId)
    {
        playerId = Mathf.Clamp(playerId, 1, MaxConfigurablePlayers);

        var session = GameSession.Instance;
        if (session == null)
            return playerId == 1;

        return session.IsPlayerActive(playerId);
    }

    bool ToggleSelectedPlayerActive()
    {
        int playerId = Mathf.Clamp(playerSelectIndex + 1, 1, MaxConfigurablePlayers);

        var session = GameSession.Instance;
        if (session == null)
        {
            ShowBlockedMessage(GameTextDatabase.Controls.PlayerActivationUnavailable);
            return false;
        }

        bool active = session.IsPlayerActive(playerId);
        bool nextActive = !active;

        SaveSystem.SetControlPlayerActive(playerId, nextActive);

        return session.IsPlayerActive(playerId) != active;
    }

    void ShowBlockedMessageForKey(KeyCode key)
    {
        ControlsMenuText text = GameTextDatabase.Controls;
        ShowBlockedMessage(string.Format(text.KeyAlreadyInUse, PrettyKeyName(key)) + "\n" + text.PleasePressAnotherKey);
    }

    void ShowBlockedMessage(string message)
    {
        blockedMessageUntil = Time.unscaledTime + 1.25f;
        blockedMessageLine = $"<color={colorPlayerSelectedRed}>{message}</color>";
    }

    bool IsKeyUsedByOtherPlayers(int targetPid, KeyCode key)
    {
        var mgr = PlayerInputManager.Instance;
        if (mgr == null) return false;

        for (int p = 1; p <= MaxConfigurablePlayers; p++)
        {
            if (p == targetPid) continue;

            var profile = mgr.GetPlayer(p);
            if (profile == null) continue;

            for (int i = 0; i < BulkActions.Length; i++)
            {
                var b = profile.GetBinding(BulkActions[i]);
                if (b.kind == BindKind.Key && b.key == key)
                    return true;
            }
        }

        return false;
    }

    bool IsKeyAlreadyAssignedInThisBulkRemap(PlayerInputProfile targetProfile, PlayerAction currentAction, KeyCode key)
    {
        if (targetProfile == null) return false;

        int max = Mathf.Clamp(bulkStep, 0, BulkActions.Length);
        for (int i = 0; i < max; i++)
        {
            var a = BulkActions[i];
            if (a == currentAction) continue;

            var b = targetProfile.GetBinding(a);
            if (b.kind == BindKind.Key && b.key == key)
                return true;
        }

        return false;
    }

    public IEnumerator OpenRoutine(int openerPlayerId, AudioClip restoreMusic, float restoreMusicVolume = 1f)
    {
        ownerPlayerId = Mathf.Clamp(openerPlayerId, 1, MaxConfigurablePlayers);

        SaveSystem.LoadControlsIntoInputManager();

        if (root == null) root = gameObject;

        if (fadeImage != null)
        {
            fadeImage.gameObject.SetActive(true);
            fadeImage.transform.SetAsLastSibling();
            SetFadeAlpha(1f);
        }

        root.transform.SetAsLastSibling();
        root.SetActive(true);
        if (fadeImage != null)
            fadeImage.transform.SetAsLastSibling();

        if (backgroundImage != null)
        {
            backgroundImage.gameObject.SetActive(true);
            backgroundImage.color = Color.white;
            backgroundImage.transform.SetAsFirstSibling();
        }

        SetupMenuTextMaterial();
        ApplyDynamicScaleIfNeeded(true);

        if (controlsMusic != null && GameMusicController.Instance != null)
        {
            PlayControlsMusic();
        }

        PrepareInitialMenuStateForOpen();

        yield return null;

        if (fadeImage != null)
        {
            fadeImage.gameObject.SetActive(true);
            fadeImage.transform.SetAsLastSibling();
            yield return FadeTo(0f, fadeDuration);
            fadeImage.gameObject.SetActive(false);
        }
        else
        {
        }

        if (cursorRenderer != null)
        {
            cursorRenderer.gameObject.SetActive(true);
            cursorRenderer.RefreshFrame();
        }

        while (AnyPlayerHeldAnyMenuKey())
            yield return null;

        yield return null;

        RefreshText();

        bool done = false;
        while (!done)
        {
            var pointer = GetPointerState();
            if (pointer.valid)
                TryHandlePointerHover(pointer.screenPosition);

            bool leftClick = pointer.valid && pointer.pressedThisFrame;
            bool rightClick = pointer.valid && pointer.secondaryPressedThisFrame;

            if (state == MenuState.SelectPlayer)
            {
                int prev = playerSelectIndex;

                if (TryGetAnyPlayerDown(PlayerAction.MoveUp, out int pidUp))
                {
                    ownerPlayerId = pidUp;
                    playerSelectIndex = Mathf.Clamp(playerSelectIndex - 1, 0, MaxConfigurablePlayers - 1);
                }
                else if (TryGetAnyPlayerDown(PlayerAction.MoveDown, out int pidDown))
                {
                    ownerPlayerId = pidDown;
                    playerSelectIndex = Mathf.Clamp(playerSelectIndex + 1, 0, MaxConfigurablePlayers - 1);
                }

                if (playerSelectIndex != prev)
                {
                    targetPlayerId = playerSelectIndex + 1;
                    PlaySfx(moveOptionSfx, moveOptionVolume);
                    RefreshText();
                }

                bool backByPad = TryGetAnyPlayerDown(PlayerAction.ActionB, out int pidBack);
                bool backByMouse = rightClick;

                if (backByPad || backByMouse)
                {
                    if (backByPad) ownerPlayerId = pidBack;
                    PlaySfx(backSfx, backVolume);

                    if (cursorRenderer != null)
                    {
                        if (cursorPulseRoutine != null)
                            StopCoroutine(cursorPulseRoutine);
                        cursorPulseRoutine = StartCoroutine(cursorRenderer.PlayCycles(1));
                    }

                    done = true;
                    yield return null;
                    continue;
                }

                bool toggleLeft = TryGetAnyPlayerDown(PlayerAction.MoveLeft, out int pidToggleLeft);
                bool toggleRight = TryGetAnyPlayerDown(PlayerAction.MoveRight, out int pidToggleRight);
                bool toggleByPad = toggleLeft || toggleRight;

                if (toggleByPad)
                {
                    ownerPlayerId = toggleLeft ? pidToggleLeft : pidToggleRight;

                    if (ToggleSelectedPlayerActive())
                        PlaySfx(confirmSfx, confirmVolume);
                    else
                        PlaySfx(lockedSfx, lockedVolume);

                    RefreshText();
                    yield return PulseCursor();
                    yield return null;
                    continue;
                }

                if (TryGetAnyPlayerDown(PlayerAction.ActionC, out int pidAskReset))
                {
                    ownerPlayerId = pidAskReset;
                    confirmResetPlayerId = playerSelectIndex + 1;
                    confirmResetIndex = 1;
                    state = MenuState.ConfirmReset;
                    PlaySfx(confirmSfx, confirmVolume);
                    RefreshText();
                    yield return PulseCursor();
                    yield return null;
                    continue;
                }

                bool confirmByPad = TryGetAnyPlayerDownEither(PlayerAction.Start, PlayerAction.ActionA, out int pidConfirm);
                bool confirmByMouse = leftClick && TryGetPointerHoveredLinkId(pointer.screenPosition, out string clickedSelLink)
                                      && TryResolveLinkIdToSelectionIndex(clickedSelLink, out _);

                if (confirmByPad || confirmByMouse)
                {
                    if (confirmByPad)
                        ownerPlayerId = pidConfirm;

                    targetPlayerId = playerSelectIndex + 1;

                    state = MenuState.WaitForInput;
                    blockedMessageUntil = 0f;
                    blockedMessageLine = null;
                    PlaySfx(confirmSfx, confirmVolume);
                    RefreshText();
                    yield return PulseCursor();
                    yield return null;
                    continue;
                }

                yield return null;
                continue;
            }

            if (state == MenuState.ConfirmReset)
            {
                int prev = confirmResetIndex;

                if (TryGetAnyPlayerDown(PlayerAction.MoveUp, out int pidUp))
                {
                    ownerPlayerId = pidUp;
                    confirmResetIndex = Mathf.Clamp(confirmResetIndex - 1, 0, 1);
                }
                else if (TryGetAnyPlayerDown(PlayerAction.MoveDown, out int pidDown))
                {
                    ownerPlayerId = pidDown;
                    confirmResetIndex = Mathf.Clamp(confirmResetIndex + 1, 0, 1);
                }
                else if (TryGetAnyPlayerDown(PlayerAction.MoveLeft, out int pidLeft))
                {
                    ownerPlayerId = pidLeft;
                    confirmResetIndex = 0;
                }
                else if (TryGetAnyPlayerDown(PlayerAction.MoveRight, out int pidRight))
                {
                    ownerPlayerId = pidRight;
                    confirmResetIndex = 1;
                }

                if (confirmResetIndex != prev)
                {
                    PlaySfx(moveOptionSfx, moveOptionVolume);
                    RefreshText();
                }

                bool cancelByPad = TryGetAnyPlayerDown(PlayerAction.ActionB, out int pidCancel);
                bool cancelByMouse = rightClick;

                if (cancelByPad || cancelByMouse)
                {
                    if (cancelByPad) ownerPlayerId = pidCancel;
                    state = MenuState.SelectPlayer;
                    PlaySfx(backSfx, backVolume);
                    RefreshText();
                    yield return PulseCursor();
                    yield return null;
                    continue;
                }

                bool confirmResetByPad = TryGetAnyPlayerDownEither(PlayerAction.Start, PlayerAction.ActionA, out int pidYesNo);
                bool confirmResetByMouse = leftClick
                                           && TryGetPointerHoveredLinkId(pointer.screenPosition, out string clickedResetLink)
                                           && TryResolveLinkIdToSelectionIndex(clickedResetLink, out _);

                if (confirmResetByPad || confirmResetByMouse)
                {
                    if (confirmResetByPad) ownerPlayerId = pidYesNo;

                    if (confirmResetIndex == 0)
                    {
                        var pReset = PlayerInputManager.Instance.GetPlayer(confirmResetPlayerId);
                        pReset.ResetToDefault();

                        SaveSystem.SetControlPlayerActive(confirmResetPlayerId, false);
                        SaveSystem.SaveControlsFromInputManager();

                        PlaySfx(resetSfx, resetVolume);
                        state = MenuState.SelectPlayer;
                        RefreshText();
                        yield return PulseCursor();
                        yield return null;
                        continue;
                    }

                    state = MenuState.SelectPlayer;
                    PlaySfx(backSfx, backVolume);
                    RefreshText();
                    yield return PulseCursor();
                    yield return null;
                    continue;
                }

                yield return null;
                continue;
            }

            if (state == MenuState.WaitForInput)
            {
                bool escCancel = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
                bool actionBCancel = TryGetAnyPlayerDown(PlayerAction.ActionB, out int pidCancel);
                bool mouseCancel = rightClick;

                var dpad = ReadAnyDpadDownThisFrame();
                var joyBtn = ReadAnyGamepadButtonDownThisFrame();
                KeyCode? key = ReadAnyKeyboardKeyDownNoMouse();

                bool rawGamepadActionBCancel = joyBtn.HasValue && joyBtn.Value.btn == 1;
                bool rawKeyboardEscCancel = key.HasValue && key.Value == KeyCode.Escape;

                if (escCancel || actionBCancel || mouseCancel || rawGamepadActionBCancel || rawKeyboardEscCancel)
                {
                    if (actionBCancel) ownerPlayerId = pidCancel;

                    state = MenuState.SelectPlayer;
                    PlaySfx(backSfx, backVolume);
                    RefreshText();
                    yield return PulseCursor();
                    yield return null;
                    continue;
                }

                bool startByMappedInput = TryGetAnyPlayerDownEither(PlayerAction.Start, PlayerAction.ActionA, out int pidStart);
                bool startByMouse = leftClick &&
                                    TryGetPointerHoveredLinkId(pointer.screenPosition, out string clickedWaitLink) &&
                                    clickedWaitLink == LINK_WAIT_START;
                bool startByRawInput = dpad.HasValue || joyBtn.HasValue || key.HasValue;

                if (startByMappedInput || startByMouse || startByRawInput)
                {
                    if (startByMappedInput)
                        ownerPlayerId = pidStart;

                    BeginBulkRemap();
                    PlaySfx(confirmSfx, confirmVolume);
                    RefreshText();
                    yield return PulseCursor();
                    yield return null;
                    continue;
                }

                yield return null;
                continue;
            }

            if (state == MenuState.BulkRemap)
            {
                bool escCancel = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
                bool mouseCancel = rightClick;

                var p = PlayerInputManager.Instance.GetPlayer(targetPlayerId);

                if (escCancel || mouseCancel)
                {
                    if (bulkSnapshot != null)
                        p.ApplyBindings(bulkSnapshot);

                    bulkSnapshot = null;
                    bulkStep = 0;
                    state = MenuState.SelectPlayer;

                    PlaySfx(backSfx, backVolume);
                    RefreshText();
                    yield return null;
                    continue;
                }

                var dpad = ReadAnyDpadDownThisFrame();
                var joyBtn = ReadAnyGamepadButtonDownThisFrame();
                KeyCode? key = ReadAnyKeyboardKeyDownNoMouse();

                if (key.HasValue)
                {
                    var pressed = key.Value;
                    var action = CurrentBulkAction();

                    bool usedByOther = IsKeyUsedByOtherPlayers(targetPlayerId, pressed);
                    bool usedEarlierInThisBulk = IsKeyAlreadyAssignedInThisBulkRemap(p, action, pressed);

                    if (usedByOther || usedEarlierInThisBulk)
                    {
                        PlaySfx(lockedSfx, lockedVolume);
                        ShowBlockedMessageForKey(pressed);
                        RefreshText();
                        yield return null;
                        continue;
                    }
                }

                if (dpad.HasValue || joyBtn.HasValue || key.HasValue)
                {
                    var action = CurrentBulkAction();

                    if (dpad.HasValue)
                    {
                        blockedMessageUntil = 0f;
                        blockedMessageLine = null;

                        p.joyIndex = dpad.Value.joyIndex;
                        p.gamepadDeviceId = dpad.Value.deviceId;
                        p.gamepadProduct = dpad.Value.product ?? "";
                        p.SetBinding(action, Binding.FromDpad(p.joyIndex, dpad.Value.dir));
                    }
                    else if (joyBtn.HasValue)
                    {
                        blockedMessageUntil = 0f;
                        blockedMessageLine = null;

                        p.joyIndex = joyBtn.Value.joyIndex;
                        p.gamepadDeviceId = joyBtn.Value.deviceId;
                        p.gamepadProduct = joyBtn.Value.product ?? "";
                        p.SetBinding(action, Binding.FromJoyButton(p.joyIndex, joyBtn.Value.btn));
                    }
                    else if (key.HasValue)
                    {
                        blockedMessageUntil = 0f;
                        blockedMessageLine = null;
                        p.SetBinding(action, Binding.FromKey(key.Value));
                    }

                    PlaySfx(confirmSfx, confirmVolume);
                    yield return PulseCursor();

                    bulkStep++;

                    if (bulkStep >= BulkActions.Length)
                    {
                        SaveSystem.SaveControlsFromInputManager();

                        ActivateTargetPlayerAfterSuccessfulRemap();

                        bulkSnapshot = null;
                        bulkStep = 0;
                        state = MenuState.SelectPlayer;
                        RefreshText();
                        yield return null;
                        continue;
                    }

                    RefreshText();
                }

                yield return null;
                continue;
            }

            yield return null;
        }

        if (fadeImage != null)
        {
            fadeImage.gameObject.SetActive(true);
            fadeImage.transform.SetAsLastSibling();
            SetFadeAlpha(0f);
            yield return FadeTo(1f, fadeDuration);
        }

        if (cursorRenderer != null)
            cursorRenderer.gameObject.SetActive(false);

        if (menuLayoutRoot != null && menuLayoutRootCached)
            menuLayoutRoot.anchoredPosition = menuLayoutRootBasePos;

        if (backgroundImage != null)
            backgroundImage.gameObject.SetActive(false);

        if (root != null)
            root.SetActive(false);

        if (fadeImage != null)
            fadeImage.gameObject.SetActive(false);

        if (restoreMusic != null && GameMusicController.Instance != null)
            GameMusicController.Instance.PlayMusic(restoreMusic, Mathf.Clamp01(restoreMusicVolume), true);
    }

    void PrepareInitialMenuStateForOpen()
    {
        state = MenuState.SelectPlayer;
        playerSelectIndex = Mathf.Clamp(ownerPlayerId - 1, 0, MaxConfigurablePlayers - 1);
        targetPlayerId = playerSelectIndex + 1;
        bulkStep = 0;
        bulkSnapshot = null;

        confirmResetIndex = 1;
        confirmResetPlayerId = 1;

        blockedMessageUntil = 0f;
        blockedMessageLine = null;

        _hasLastMouseScreenPosition = false;
        _lastMouseScreenPosition = Vector2.zero;

        if (cursorRenderer != null)
        {
            cursorRenderer.gameObject.SetActive(true);
            cursorRenderer.RefreshFrame();
        }

        RefreshText();
    }

    void PlayControlsMusic()
    {
        var music = GameMusicController.Instance;
        if (music == null || controlsMusic == null)
        {
            return;
        }

        PreloadControlsMusic();

        if (controlsMusicLoop != null)
        {
            music.PlayMusicIntroThenLoop(
                controlsMusic,
                controlsMusicVolume,
                controlsMusicLoop,
                controlsMusicVolume);
            return;
        }

        music.PlayMusic(controlsMusic, controlsMusicVolume, true);
    }

    void PreloadControlsMusic()
    {
        if (controlsMusic != null && controlsMusic.loadState == AudioDataLoadState.Unloaded)
        {
            controlsMusic.LoadAudioData();
        }

        if (controlsMusicLoop != null && controlsMusicLoop.loadState == AudioDataLoadState.Unloaded)
        {
            controlsMusicLoop.LoadAudioData();
        }
    }

    static UniversalControllerInput.DpadHit? ReadAnyDpadDownThisFrame()
    {
        return UniversalControllerInput.TryReadAnyDpadDownThisFrame(out var hit) ? hit : null;
    }

    static UniversalControllerInput.ButtonHit? ReadAnyGamepadButtonDownThisFrame()
    {
        return UniversalControllerInput.TryReadAnyButtonDownThisFrame(out var hit) ? hit : null;
    }

    static KeyCode? ReadAnyKeyboardKeyDownNoMouse()
    {
        var kb = Keyboard.current;
        if (kb == null) return null;

        if (!kb.anyKey.wasPressedThisFrame)
            return null;

        foreach (var k in kb.allKeys)
        {
            if (k == null) continue;
            if (!k.wasPressedThisFrame) continue;

            if (TryMapInputSystemKeyToUnityKeyCode(k.keyCode, out var kc))
                return kc;
        }

        return null;
    }

    static bool TryMapInputSystemKeyToUnityKeyCode(Key key, out KeyCode kc)
    {
        kc = KeyCode.None;
        string name = key.ToString();

        switch (key)
        {
            case Key.Enter: kc = KeyCode.Return; return true;
            case Key.Escape: kc = KeyCode.Escape; return true;
        }

        if (name.StartsWith("Digit", StringComparison.OrdinalIgnoreCase) && name.Length == 6)
        {
            char d = name[5];
            if (d >= '0' && d <= '9')
            {
                kc = (KeyCode)Enum.Parse(typeof(KeyCode), "Alpha" + d);
                return true;
            }
        }

        if (name.StartsWith("Numpad", StringComparison.OrdinalIgnoreCase))
        {
            if (name.Length == 7)
            {
                char d = name[6];
                if (d >= '0' && d <= '9')
                {
                    kc = (KeyCode)Enum.Parse(typeof(KeyCode), "Keypad" + d);
                    return true;
                }
            }

            if (name.Equals("NumpadEnter", StringComparison.OrdinalIgnoreCase)) { kc = KeyCode.KeypadEnter; return true; }
            if (name.Equals("NumpadPlus", StringComparison.OrdinalIgnoreCase)) { kc = KeyCode.KeypadPlus; return true; }
            if (name.Equals("NumpadMinus", StringComparison.OrdinalIgnoreCase)) { kc = KeyCode.KeypadMinus; return true; }
            if (name.Equals("NumpadMultiply", StringComparison.OrdinalIgnoreCase)) { kc = KeyCode.KeypadMultiply; return true; }
            if (name.Equals("NumpadDivide", StringComparison.OrdinalIgnoreCase)) { kc = KeyCode.KeypadDivide; return true; }
            if (name.Equals("NumpadPeriod", StringComparison.OrdinalIgnoreCase)) { kc = KeyCode.KeypadPeriod; return true; }
        }

        if (name.Equals("LeftCtrl", StringComparison.OrdinalIgnoreCase)) { kc = KeyCode.LeftControl; return true; }
        if (name.Equals("RightCtrl", StringComparison.OrdinalIgnoreCase)) { kc = KeyCode.RightControl; return true; }

        if (Enum.TryParse(name, true, out KeyCode parsed))
        {
            kc = parsed;
            return kc != KeyCode.None;
        }

        if (name.Equals("Backquote", StringComparison.OrdinalIgnoreCase)) { kc = KeyCode.BackQuote; return true; }
        if (name.Equals("Minus", StringComparison.OrdinalIgnoreCase)) { kc = KeyCode.Minus; return true; }
        if (name.Equals("Equals", StringComparison.OrdinalIgnoreCase)) { kc = KeyCode.Equals; return true; }
        if (name.Equals("LeftBracket", StringComparison.OrdinalIgnoreCase)) { kc = KeyCode.LeftBracket; return true; }
        if (name.Equals("RightBracket", StringComparison.OrdinalIgnoreCase)) { kc = KeyCode.RightBracket; return true; }
        if (name.Equals("Semicolon", StringComparison.OrdinalIgnoreCase)) { kc = KeyCode.Semicolon; return true; }
        if (name.Equals("Quote", StringComparison.OrdinalIgnoreCase)) { kc = KeyCode.Quote; return true; }
        if (name.Equals("Backslash", StringComparison.OrdinalIgnoreCase)) { kc = KeyCode.Backslash; return true; }
        if (name.Equals("Slash", StringComparison.OrdinalIgnoreCase)) { kc = KeyCode.Slash; return true; }
        if (name.Equals("Comma", StringComparison.OrdinalIgnoreCase)) { kc = KeyCode.Comma; return true; }
        if (name.Equals("Period", StringComparison.OrdinalIgnoreCase)) { kc = KeyCode.Period; return true; }

        return false;
    }

    IEnumerator PulseCursor()
    {
        if (cursorRenderer == null)
            yield break;

        if (cursorPulseRoutine != null)
            StopCoroutine(cursorPulseRoutine);

        cursorPulseRoutine = StartCoroutine(cursorRenderer.PlayCycles(1));
        yield return cursorPulseRoutine;
        cursorPulseRoutine = null;
    }

    void RefreshText()
    {
        if (menuText == null)
            return;

        ApplyLanguageFontFallback();

        ApplyMenuGlobalOffset();
        ApplyMenuTextRectScale();

        int titleSize = ScaledFont(EffectiveTitleFontSize);
        float titleVO = usePixelPerfectMenuLayout ? 0f : ScaledFloat(titleVOffset);
        int bodySize = ScaledFont(EffectiveBodyFontSize);
        int footerSize = ScaledFont(EffectiveFooterFontSize);
        int gridSize = ScaledFont(state == MenuState.BulkRemap ? EffectiveRemapGridFontSize : EffectiveSelectGridFontSize);
        ControlsMenuText text = GameTextDatabase.Controls;

        string header =
            RepeatNewLine(Mathf.Max(0, headerTopPaddingLines)) +
            $"<align=center><size={titleSize}><color={colorHint}><voffset={titleVO}>{text.Title}</voffset></color></size></align>" +
            RepeatNewLine(Mathf.Max(0, headerBottomPaddingLines)) +
            "\n";

        string body = string.Empty;
        string footer = string.Empty;

        if (state == MenuState.SelectPlayer)
        {
            body += "<align=center>";
            body += RepeatSizedSpacerLines(EffectiveSelectTitleToBodyGapLines, bodySize);
            body += $"<size={bodySize}><color={colorBlueSoft}>{text.ChoosePlayer}</color></size>\n\n";
            body += "</align><align=left>";

            int selectorRowGap = Mathf.Max(0, playerBlockGapLines - 1);
            for (int i = 0; i < MaxConfigurablePlayers; i++)
            {
                AppendPlayerSelectRow(ref body, i, gridSize);

                if (i < MaxConfigurablePlayers - 1)
                    body += RepeatNewLine(selectorRowGap);
            }

            if (Time.unscaledTime < blockedMessageUntil && !string.IsNullOrEmpty(blockedMessageLine))
                footer += $"<size={footerSize}>{blockedMessageLine}</size>\n";

            body += "</align>";

            footer +=
                RepeatNewLine(Mathf.Max(0, footerGapLines + footerExtraNewLines)) +
                $"<align=center>" +
                $"<size={footerSize}>" +
                $"<color={colorHint}>A / START:</color> <color={colorWhite}>{text.MapControls}</color>\n" +
                $"<color={colorHint}>{text.MoveLeft} / {text.MoveRight}:</color> <color={colorWhite}>{text.TogglePlayer}</color>\n" +
                $"<color={colorHint}>C:</color> <color={colorWhite}>{text.RestoreDefaultKeys}</color>\n" +
                $"<color={colorHint}>B:</color> <color={colorWhite}>{text.Return}</color>" +
                $"</size></align>";
        }
        else if (state == MenuState.ConfirmReset)
        {
            string yesText = confirmResetIndex == 0 ? $"<color={colorHint}>{text.Yes}</color>" : $"<color={colorWhite}>{text.Yes}</color>";
            string noText = confirmResetIndex == 1 ? $"<color={colorHint}>{text.No}</color>" : $"<color={colorWhite}>{text.No}</color>";

            body += "<align=center>";
            body += RepeatSizedSpacerLines(EffectiveConfirmTitleToBodyGapLines, bodySize);
            body +=
                $"<size={bodySize}>" +
                $"<color={colorHint}>{text.RestoreDefaultKeysQuestion}</color>\n" +
                $"<color={colorWhite}>{text.Player} {confirmResetPlayerId}</color>\n\n" +
                $"<link=\"{LINK_RESET_YES}\">{yesText}</link>    <link=\"{LINK_RESET_NO}\">{noText}</link>" +
                $"</size></align>";

            footer +=
                $"<align=center><size={footerSize}>" +
                $"<color={colorHint}>A / START:</color> <color={colorWhite}>{text.Confirm}</color>    " +
                $"<color={colorHint}>B:</color> <color={colorWhite}>{text.Cancel}</color>" +
                $"</size></align>";
        }
        else if (state == MenuState.WaitForInput)
        {
            body += "<align=center>";
            body += RepeatSizedSpacerLines(EffectiveWaitTitleToBodyGapLines, bodySize);
            body +=
                $"<size={bodySize}><color={colorBlueSoft}>{text.ConfiguringPlayer} {targetPlayerId}</color></size>\n\n" +
                $"<size={footerSize}>" +
                $"<color={colorWhite}>{text.PressControlsYouWant}</color>\n" +
                $"<color={colorWhite}>{text.ToUseForThisPlayer}</color>\n\n" +
                $"<link=\"{LINK_WAIT_START}\"><color={colorHint}>{text.PressAnyKeyOrButton}</color></link>\n" +
                $"<color={colorWhite}>{text.ToStartMapping}</color>" +
                $"</size></align>";

            footer +=
                $"<align=center><size={footerSize}>" +
                $"<color={colorPlayerSelectedRed}>{text.EscBToCancel}</color>" +
                $"</size></align>";
        }
        else
        {
            var a = CurrentBulkAction();

            string blocked = (Time.unscaledTime < blockedMessageUntil && !string.IsNullOrEmpty(blockedMessageLine))
                ? (blockedMessageLine + "\n")
                : string.Empty;

            body += "<align=center>";
            body +=
                $"<size={bodySize}><color={colorBlueSoft}>{text.ConfiguringPlayer} {targetPlayerId}</color></size>\n";
            body += RepeatSizedSpacerLines(EffectiveRemapTitleToBodyGapLines, bodySize);
            body +=
                $"<size={gridSize}><color={colorPlayerSelectedRed}>{text.Player} {targetPlayerId}</color></size>\n";
            body += "</align>";

            AppendBulkRemapVerticalList(ref body, targetPlayerId, gridSize);

            footer +=
                $"<align=center><size={footerSize}>" +
                $"<color={colorHint}>{text.ChooseButtonFor}</color> <color={colorWhite}>{ActionToLabel(a)}</color>\n" +
                blocked +
                $"<color={colorPlayerSelectedRed}>{text.EscToCancel}</color>\n\n" +
                $"<color={colorHint}>A / START:</color> <color={colorWhite}>{text.ConfirmPlaceBomb}</color>\n" +
                $"<color={colorHint}>B:</color> <color={colorWhite}>{text.ReturnExplodeControlBomb}</color>\n" +
                $"<color={colorHint}>C:</color> <color={colorWhite}>{text.RestoreDefaultKeysAbilities}</color>\n" +
                $"<color={colorHint}>L ({text.Riding}):</color> <color={colorWhite}>{text.Dismount}</color>\n" +
                $"<color={colorHint}>R:</color> <color={colorWhite}>{text.StopKickedBombs}</color>" +
                $"</size></align>";
        }

        menuText.text = header + body;
        SetFooterText(footer);

        if (state == MenuState.SelectPlayer)
        {
            UpdateCursorPosition_ByLinkId($"sel{playerSelectIndex}");
        }
        else if (state == MenuState.ConfirmReset)
        {
            UpdateCursorPosition_ByLinkId(confirmResetIndex == 0 ? LINK_RESET_YES : LINK_RESET_NO);
        }
        else if (state == MenuState.WaitForInput)
        {
            UpdateCursorPosition_ByLinkId(LINK_WAIT_START);
        }
        else
        {
            UpdateCursorPosition_ByLinkId(CurrentWaitLinkId());
        }
    }

    void SetFooterText(string text)
    {
        EnsureFooterText();
        ApplyLanguageFontFallback();

        if (footerText == null)
            return;

        footerText.gameObject.SetActive(!string.IsNullOrEmpty(text));
        footerText.text = text ?? string.Empty;
    }

    void AppendPlayerSelectRow(ref string body, int index, int gridSize)
    {
        int playerId = index + 1;
        bool selected = playerSelectIndex == index;
        bool active = IsPlayerActiveForMenu(playerId);

        string playerColor = selected ? colorPlayerSelectedRed : colorPlayerGreen;
        string statusColor = active ? colorPlayerGreen : colorPlayerSelectedRed;
        ControlsMenuText text = GameTextDatabase.Controls;
        string status = active ? text.On : text.Off;
        string playerLabel = $"{text.Player} {playerId}";
        float labelX = ScaledFloat(250f);
        float valueX = ScaledFloat(580f);
        float statusX = valueX + (status.Length == 2 ? gridSize * 0.5f : 0f);

        body +=
            $"<link=\"sel{index}\">" +
            $"<size={gridSize}>" +
            $"<pos={labelX}><color={playerColor}>{playerLabel}</color></pos>" +
            $"<pos={statusX}><color={statusColor}>{status}</color></pos>" +
            $"</size>" +
            $"</link>\n";
    }

    void AppendBulkRemapVerticalList(ref string body, int playerId, int gridSize)
    {
        var p = PlayerInputManager.Instance.GetPlayer(playerId);
        if (p == null)
            return;

        float labelX = ScaledFloat(250f);
        float valueX = ScaledFloat(480f);

        body += "<align=left>";

        for (int i = 0; i < BulkActions.Length; i++)
        {
            PlayerAction action = BulkActions[i];

            string label = $"{ActionToLabel(action)}:";
            string binding = BindingToShort(p.GetBinding(action));

            bool current = CurrentBulkAction() == action;

            string labelText = current
                ? $"<link=\"{CurrentWaitLinkId()}\"><color={colorHint}>{label}</color></link>"
                : $"<color={colorHint}>{label}</color>";

            body +=
                $"<size={gridSize}>" +
                $"<pos={labelX}>{labelText}</pos>" +
                $"<pos={valueX}><color={colorNormal}>{binding}</color></pos>" +
                $"</size>\n";
        }

        body += "</align>";
    }

    string LabelMaybeWait(PlayerAction action, string label, string ch)
    {
        if (state == MenuState.BulkRemap && CurrentBulkAction() == action)
            return $"<link=\"{CurrentWaitLinkId()}\"><color={ch}>{label}</color></link>";

        return $"<color={ch}>{label}</color>";
    }

    string PlayerLine(int pid, int lineIndex, string cn, string ch, int selIndex, int gridSize)
    {
        var p = PlayerInputManager.Instance.GetPlayer(pid);

        bool selected = playerSelectIndex == selIndex;
        string playerColor = selected ? colorPlayerSelectedRed : colorPlayerGreen;
        ControlsMenuText text = GameTextDatabase.Controls;
        string tag = $"<color={playerColor}>{text.Player} {pid}</color>";

        string u = BindingToShort(p.GetBinding(PlayerAction.MoveUp));
        string d = BindingToShort(p.GetBinding(PlayerAction.MoveDown));
        string l = BindingToShort(p.GetBinding(PlayerAction.MoveLeft));
        string r = BindingToShort(p.GetBinding(PlayerAction.MoveRight));

        string st = BindingToShort(p.GetBinding(PlayerAction.Start));
        string a = BindingToShort(p.GetBinding(PlayerAction.ActionA));
        string b = BindingToShort(p.GetBinding(PlayerAction.ActionB));
        string c = BindingToShort(p.GetBinding(PlayerAction.ActionC));

        string lBtn = BindingToShort(p.GetBinding(PlayerAction.ActionL));
        string rBtn = BindingToShort(p.GetBinding(PlayerAction.ActionR));

        float indent = playersBlockIndentX * _currentUiScale;

        float ll = ScaledFloat(COLUMN_LEFT_LABEL_BASE) + indent;
        float lv = ScaledFloat(COLUMN_LEFT_VALUE_BASE) + indent;
        float rl = ScaledFloat(COLUMN_RIGHT_LABEL_BASE) + indent;
        float rv = ScaledFloat(COLUMN_RIGHT_VALUE_BASE) + indent;

        bool isTarget = state == MenuState.BulkRemap && pid == targetPlayerId;

        string Lbl(PlayerAction act, string s)
        {
            if (isTarget) return LabelMaybeWait(act, s, ch);
            return $"<color={ch}>{s}</color>";
        }

        string txt = lineIndex switch
        {
            0 => $"<align=center>{tag}</align>",
            1 => $"<pos={ll}>{Lbl(PlayerAction.MoveUp, text.MoveUp + ":")}</pos><pos={lv}>{u}</pos>" +
                 $"<pos={rl}>{Lbl(PlayerAction.ActionA, "A:")}</pos><pos={rv}>{a}</pos>",
            2 => $"<pos={ll}>{Lbl(PlayerAction.MoveDown, text.MoveDown + ":")}</pos><pos={lv}>{d}</pos>" +
                 $"<pos={rl}>{Lbl(PlayerAction.ActionB, "B:")}</pos><pos={rv}>{b}</pos>",
            3 => $"<pos={ll}>{Lbl(PlayerAction.MoveLeft, text.MoveLeft + ":")}</pos><pos={lv}>{l}</pos>" +
                 $"<pos={rl}>{Lbl(PlayerAction.ActionC, "C:")}</pos><pos={rv}>{c}</pos>",
            4 => $"<pos={ll}>{Lbl(PlayerAction.MoveRight, text.MoveRight + ":")}</pos><pos={lv}>{r}</pos>" +
                 $"<pos={rl}>{Lbl(PlayerAction.ActionL, "L:")}</pos><pos={rv}>{lBtn}</pos>",
            5 => $"<pos={ll}>{Lbl(PlayerAction.Start, text.Start + ":")}</pos><pos={lv}>{st}</pos>" +
                 $"<pos={rl}>{Lbl(PlayerAction.ActionR, "R:")}</pos><pos={rv}>{rBtn}</pos>",
            _ => string.Empty
        };

        return $"<size={gridSize}><color={cn}>{txt}</color></size>";
    }

    static string BindingToShort(Binding b)
    {
        if (b.kind == BindKind.Key)
        {
            if (b.key == KeyCode.None)
                return "---";

            return PrettyKeyName(b.key);
        }

        if (b.kind == BindKind.DPad)
        {
            if (b.dpadDir < 0)
                return "---";

            return b.dpadDir switch
            {
                0 => "DPAD UP",
                1 => "DPAD DOWN",
                2 => "DPAD LEFT",
                3 => "DPAD RIGHT",
                _ => "---"
            };
        }

        if (b.kind == BindKind.JoyButton)
        {
            if (b.joyButton < 0)
                return "---";

            return $"JOY {b.joyIndex} BTN {b.joyButton}";
        }

        return "---";
    }

    void UpdateCursorPosition_ByLinkId(string linkId)
    {
        if (menuText == null || cursorRenderer == null)
            return;

        cursorRenderer.gameObject.SetActive(true);

        menuText.ForceMeshUpdate();
        var ti = menuText.textInfo;
        if (ti == null || ti.linkCount <= 0)
            return;

        bool foundAny = false;
        float bestY = float.NegativeInfinity;
        Vector3 bestLocalPos = default;

        float gapLeft = ScaledFloat(cursorGapLeft);
        float lineAdjustY = ScaledFloat(cursorLineCenterAdjustY);
        Vector2 offs = new Vector2(ScaledFloat(cursorOffset.x), ScaledFloat(cursorOffset.y));

        for (int i = 0; i < ti.linkCount; i++)
        {
            var li = ti.linkInfo[i];
            if (li.GetLinkID() != linkId)
                continue;

            int first = li.linkTextfirstCharacterIndex;
            int last = first + li.linkTextLength - 1;
            if (first < 0 || first >= ti.characterCount)
                continue;

            last = Mathf.Min(last, ti.characterCount - 1);

            int anchorChar = -1;
            for (int c = first; c <= last; c++)
            {
                var ch = ti.characterInfo[c];
                if (!ch.isVisible) continue;

                char cc = ch.character;
                if (cc != ' ' && cc != '\u00A0' && cc != '\n' && cc != '\r' && cc != '\t')
                {
                    anchorChar = c;
                    break;
                }
            }

            if (anchorChar < 0) anchorChar = first;
            if (anchorChar < 0 || anchorChar >= ti.characterCount) continue;

            var ci = ti.characterInfo[anchorChar];

            float x = ci.bottomLeft.x - gapLeft;
            float y = (ci.ascender + ci.descender) * 0.5f + lineAdjustY;

            if (y > bestY)
            {
                bestY = y;
                bestLocalPos = new Vector3(x + offs.x, y + offs.y, 0f);
                foundAny = true;
            }
        }

        if (!foundAny)
            return;

        if (roundCursorToWholePixels)
        {
            bestLocalPos.x = Mathf.Round(bestLocalPos.x);
            bestLocalPos.y = Mathf.Round(bestLocalPos.y);
        }

        cursorRenderer.SetExternalBaseLocalPosition(bestLocalPos);
    }

    void PlaySfx(AudioClip clip, float volume)
    {
        if (clip == null) return;

        var music = GameMusicController.Instance;
        if (music == null) return;

        music.PlaySfx(clip, volume);
    }

    void SetFadeAlpha(float a)
    {
        if (fadeImage == null) return;
        var c = fadeImage.color;
        c.a = a;
        fadeImage.color = c;
    }

    IEnumerator FadeTo(float targetA, float duration)
    {
        if (fadeImage == null)
            yield break;

        float d = Mathf.Max(0.001f, duration);
        float start = fadeImage.color.a;
        float t = 0f;
        int lastLoggedPercent = -1;

        while (t < d)
        {
            float stepDelta = Mathf.Min(Time.unscaledDeltaTime, Mathf.Max(0.001f, maxFadeStepDelta));
            t += stepDelta;
            float a = Mathf.Lerp(start, targetA, Mathf.Clamp01(t / d));
            SetFadeAlpha(a);

            int percent = Mathf.FloorToInt(Mathf.Clamp01(t / d) * 100f);
            if (lastLoggedPercent < 0 || percent >= 100 || percent / 25 != lastLoggedPercent / 25)
            {
                lastLoggedPercent = percent;
            }

            yield return null;
        }

        SetFadeAlpha(targetA);
    }

    static string RepeatNewLine(int count)
    {
        if (count <= 0) return string.Empty;
        return new string('\n', count);
    }

    static string RepeatSizedSpacerLines(int count, int fontSize)
    {
        if (count <= 0) return string.Empty;

        var sb = new StringBuilder(count * 24);
        int safeFontSize = Mathf.Max(1, fontSize);
        for (int i = 0; i < count; i++)
            sb.Append("<size=").Append(safeFontSize).Append("> </size>\n");

        return sb.ToString();
    }

    static string CenterText(string text, int width)
    {
        text ??= string.Empty;
        if (text.Length >= width)
            return text;

        int totalPadding = width - text.Length;
        int leftPadding = totalPadding / 2;
        int rightPadding = totalPadding - leftPadding;
        return new string('\u00A0', leftPadding) + text + new string('\u00A0', rightPadding);
    }

    string ActionToLabel(PlayerAction a)
    {
        ControlsMenuText text = GameTextDatabase.Controls;

        return a switch
        {
            PlayerAction.MoveUp => text.MoveUp,
            PlayerAction.MoveDown => text.MoveDown,
            PlayerAction.MoveLeft => text.MoveLeft,
            PlayerAction.MoveRight => text.MoveRight,
            PlayerAction.Start => text.Start,
            PlayerAction.ActionA => text.ActionA,
            PlayerAction.ActionB => text.ActionB,
            PlayerAction.ActionC => text.ActionC,
            PlayerAction.ActionL => text.ActionL,
            PlayerAction.ActionR => text.ActionR,
            _ => a.ToString().ToUpperInvariant(),
        };
    }

    static string PrettyKeyName(KeyCode key)
    {
        return key switch
        {
            KeyCode.UpArrow => "UP ARROW",
            KeyCode.DownArrow => "DOWN ARROW",
            KeyCode.LeftArrow => "LEFT ARROW",
            KeyCode.RightArrow => "RIGHT ARROW",
            KeyCode.LeftShift => "LEFT SHIFT",
            KeyCode.RightShift => "RIGHT SHIFT",
            KeyCode.LeftControl => "LEFT CTRL",
            KeyCode.RightControl => "RIGHT CTRL",
            KeyCode.LeftAlt => "LEFT ALT",
            KeyCode.RightAlt => "RIGHT ALT",
            KeyCode.Return => "ENTER",
            KeyCode.Escape => "ESC",
            KeyCode.Backspace => "BACK SPACE",
            KeyCode.Delete => "DELETE",
            KeyCode.Space => "SPACE",
            KeyCode.Keypad0 => "KEYPAD 0",
            KeyCode.Keypad1 => "KEYPAD 1",
            KeyCode.Keypad2 => "KEYPAD 2",
            KeyCode.Keypad3 => "KEYPAD 3",
            KeyCode.Keypad4 => "KEYPAD 4",
            KeyCode.Keypad5 => "KEYPAD 5",
            KeyCode.Keypad6 => "KEYPAD 6",
            KeyCode.Keypad7 => "KEYPAD 7",
            KeyCode.Keypad8 => "KEYPAD 8",
            KeyCode.Keypad9 => "KEYPAD 9",
            KeyCode.KeypadEnter => "KEYPAD ENTER",
            KeyCode.Comma => ",",
            KeyCode.Period => ".",
            KeyCode.Slash => "/",
            _ => SplitCamelCase(key.ToString()).ToUpperInvariant()
        };
    }

    static string SplitCamelCase(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var sb = new System.Text.StringBuilder(input.Length * 2);
        sb.Append(input[0]);

        for (int i = 1; i < input.Length; i++)
        {
            char c = input[i];
            if (char.IsUpper(c) && !char.IsUpper(input[i - 1]))
                sb.Append(' ');

            sb.Append(c);
        }

        return sb.ToString();
    }

    PointerState GetPointerState()
    {
        PointerState state = default;

        if (Touchscreen.current != null)
        {
            var touches = Touchscreen.current.touches;
            for (int i = 0; i < touches.Count; i++)
            {
                var touch = touches[i];
                if (!touch.press.isPressed && !touch.press.wasPressedThisFrame)
                    continue;

                state.valid = true;
                state.screenPosition = touch.position.ReadValue();
                state.pressedThisFrame = touch.press.wasPressedThisFrame;
                state.secondaryPressedThisFrame = false;
                return state;
            }
        }

        if (Mouse.current != null)
        {
            Vector2 currentMousePosition = Mouse.current.position.ReadValue();

            bool movedThisFrame = !_hasLastMouseScreenPosition ||
                                  (currentMousePosition - _lastMouseScreenPosition).sqrMagnitude > 0.0001f;

            _lastMouseScreenPosition = currentMousePosition;
            _hasLastMouseScreenPosition = true;

            state.valid = movedThisFrame ||
                          Mouse.current.leftButton.wasPressedThisFrame ||
                          Mouse.current.rightButton.wasPressedThisFrame;

            if (!state.valid)
                return state;

            state.screenPosition = currentMousePosition;
            state.pressedThisFrame = Mouse.current.leftButton.wasPressedThisFrame;
            state.secondaryPressedThisFrame = Mouse.current.rightButton.wasPressedThisFrame;
            return state;
        }

        return state;
    }

    bool TryGetPointerHoveredLinkId(Vector2 screenPosition, out string hoveredLinkId)
    {
        hoveredLinkId = null;

        if (menuText == null)
            return false;

        Canvas canvas = GetRootCanvas();
        Camera cam = null;
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            cam = canvas.worldCamera != null ? canvas.worldCamera : Camera.main;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                menuText.rectTransform, screenPosition, cam, out Vector2 localPoint))
            return false;

        menuText.ForceMeshUpdate();
        TMP_TextInfo ti = menuText.textInfo;
        if (ti == null || ti.linkCount <= 0)
            return false;

        float padX = ScaledFloat(20f);
        float padY = ScaledFloat(6f);

        for (int li = 0; li < ti.linkCount; li++)
        {
            var link = ti.linkInfo[li];
            string id = link.GetLinkID();

            int first = link.linkTextfirstCharacterIndex;
            int last = first + link.linkTextLength - 1;
            last = Mathf.Clamp(last, 0, ti.characterCount - 1);

            float minX = float.PositiveInfinity, maxX = float.NegativeInfinity;
            float minY = float.PositiveInfinity, maxY = float.NegativeInfinity;
            bool anyVisible = false;

            for (int c = first; c <= last; c++)
            {
                var ci = ti.characterInfo[c];
                if (!ci.isVisible) continue;

                anyVisible = true;
                minX = Mathf.Min(minX, ci.bottomLeft.x);
                maxX = Mathf.Max(maxX, ci.topRight.x);
                minY = Mathf.Min(minY, ci.descender);
                maxY = Mathf.Max(maxY, ci.ascender);
            }

            if (!anyVisible) continue;

            if (localPoint.x >= minX - padX && localPoint.x <= maxX + padX &&
                localPoint.y >= minY - padY && localPoint.y <= maxY + padY)
            {
                hoveredLinkId = id;
                return true;
            }
        }

        return false;
    }

    bool TryResolveLinkIdToSelectionIndex(string linkId, out int index)
    {
        index = -1;

        if (state == MenuState.SelectPlayer)
        {
            if (linkId != null && linkId.StartsWith("sel", StringComparison.Ordinal) && linkId.Length > 3)
            {
                if (int.TryParse(linkId.Substring(3), out int i) && i >= 0 && i < MaxConfigurablePlayers)
                {
                    index = i;
                    return true;
                }
            }
            return false;
        }

        if (state == MenuState.ConfirmReset)
        {
            if (linkId == LINK_RESET_YES) { index = 0; return true; }
            if (linkId == LINK_RESET_NO) { index = 1; return true; }
            return false;
        }

        if (state == MenuState.BulkRemap)
        {
            return false;
        }

        return false;
    }

    bool TryHandlePointerHover(Vector2 screenPosition)
    {
        if (!TryGetPointerHoveredLinkId(screenPosition, out string linkId))
            return false;

        if (!TryResolveLinkIdToSelectionIndex(linkId, out int idx))
            return false;

        bool changed = false;

        if (state == MenuState.SelectPlayer && idx != playerSelectIndex)
        {
            playerSelectIndex = idx;
            targetPlayerId = playerSelectIndex + 1;
            changed = true;
        }
        else if (state == MenuState.ConfirmReset && idx != confirmResetIndex)
        {
            confirmResetIndex = idx;
            changed = true;
        }

        if (changed)
        {
            PlaySfx(moveOptionSfx, moveOptionVolume);
            RefreshText();
        }

        return changed;
    }

    void ActivateTargetPlayerAfterSuccessfulRemap()
    {
        int playerId = Mathf.Clamp(targetPlayerId, 1, MaxConfigurablePlayers);

        SaveSystem.SetControlPlayerActive(playerId, true);
    }
}

using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class BossRushTimerPresenter : MonoBehaviour
{
    const string RootName = "__BossRushTimerPresenter";
    const string TextName = "BossRushTimerText";

    static BossRushTimerPresenter instanceInScene;

    [Header("Text")]
    [SerializeField] TMP_Text timerText;
    [SerializeField] TMP_FontAsset fontAsset;
    [SerializeField] Material fontMaterial;

    [Header("Text Colors")]
    Color textColor = new Color(1f, 0.85f, 0.29f);
    Color outlineColor = new Color(0.29f, 0.19f, 0f);
    [SerializeField, Range(0f, 1f)] float outlineWidth = 0.22f;

    [Header("Dynamic Scale (Pixel Perfect friendly)")]
    [SerializeField] bool dynamicScale = true;
    [SerializeField] int referenceWidth = 256;
    [SerializeField] int referenceHeight = 224;
    [SerializeField] bool useIntegerUpscale = true;
    [SerializeField, Min(1)] int designUpscale = 4;
    [SerializeField, Min(0.01f)] float extraScaleMultiplier = 1f;
    [SerializeField, Min(0.01f)] float minScale = 0.5f;
    [SerializeField, Min(0.01f)] float maxScale = 10f;

    [Header("Font")]
    [SerializeField, Min(1)] int baseFontSizeAtDesign = 22;

    [Header("RectTransform Scaling")]
    [SerializeField, Min(1f)] float baseRectWidthAtDesign = 128f;
    [SerializeField, Min(1f)] float baseRectHeightAtDesign = 24f;
    [SerializeField] bool autoResizeRectTransform = true;

    [Header("Top Right Offset At Design")]
    [SerializeField] Vector2 anchoredOffsetAtDesign = new(-8f, -8f);

    RectTransform targetRoot;
    string lastRenderedText = string.Empty;
    bool lastVisibleState;

    float _lastUiScale = -999f;
    int _lastBaseScaleInt = -999;
    Rect _lastCamPixelRect;
    Rect _lastCamViewportRect;

    Material runtimeFontMaterial;

    public static void EnsureInScene()
    {
        var existing = FindFirstObjectByType<BossRushTimerPresenter>();
        if (existing != null)
        {
            instanceInScene = existing;
            existing.EnsureBuilt();
            return;
        }

        RectTransform parentRoot = FindTargetRoot();
        if (parentRoot == null)
        {
            Debug.LogWarning("BossRushTimerPresenter EnsureInScene | no valid UI root found");
            return;
        }

        GameObject root = new GameObject(RootName, typeof(RectTransform));
        root.transform.SetParent(parentRoot, false);

        instanceInScene = root.AddComponent<BossRushTimerPresenter>();
        instanceInScene.targetRoot = parentRoot;
        instanceInScene.EnsureBuilt();
    }

    static RectTransform FindTargetRoot()
    {
        var safeFrameGo = GameObject.Find("SafeFrame4x3");
        if (safeFrameGo != null && safeFrameGo.TryGetComponent<RectTransform>(out var safeFrameRt))
            return safeFrameRt;

        var fitters = FindObjectsByType<UICameraViewportFitter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < fitters.Length; i++)
        {
            if (fitters[i] == null)
                continue;

            var rt = fitters[i].transform as RectTransform;
            if (rt != null)
                return rt;
        }

        var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Canvas bestCanvas = null;

        for (int i = 0; i < canvases.Length; i++)
        {
            var c = canvases[i];
            if (c == null || !c.isActiveAndEnabled)
                continue;

            if (bestCanvas == null)
                bestCanvas = c;

            if (c.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                bestCanvas = c;
                break;
            }

            if (Camera.main != null && c.worldCamera == Camera.main)
            {
                bestCanvas = c;
                break;
            }
        }

        if (bestCanvas != null)
            return bestCanvas.transform as RectTransform;

        return null;
    }

    bool IsCanvasRoot(RectTransform rt)
    {
        return rt != null && rt.GetComponent<Canvas>() != null;
    }

    Rect GetMainCameraViewportRect()
    {
        var cam = Camera.main;
        if (cam == null)
            return new Rect(0f, 0f, 1f, 1f);

        return cam.rect;
    }

    Rect GetMainCameraPixelRect()
    {
        var cam = Camera.main;
        if (cam == null)
            return new Rect(0f, 0f, Screen.width, Screen.height);

        return cam.pixelRect;
    }

    float UiScale
    {
        get
        {
            float canvasScale = 1f;
            if (timerText != null && timerText.canvas != null)
                canvasScale = Mathf.Max(0.01f, timerText.canvas.scaleFactor);

            if (!dynamicScale)
            {
                float fallback = 1f / canvasScale;
                _lastBaseScaleInt = -1;
                _lastUiScale = fallback;
                return fallback;
            }

            Rect camPixelRect = GetMainCameraPixelRect();

            float usedW = camPixelRect.width;
            float usedH = camPixelRect.height;

            float sx = usedW / Mathf.Max(1f, referenceWidth);
            float sy = usedH / Mathf.Max(1f, referenceHeight);
            float baseScaleRaw = Mathf.Min(sx, sy);

            float baseScaleForUi = useIntegerUpscale ? Mathf.Round(baseScaleRaw) : baseScaleRaw;
            if (baseScaleForUi < 1f)
                baseScaleForUi = 1f;

            int baseScaleInt = Mathf.Max(1, Mathf.RoundToInt(baseScaleForUi));

            float normalized = baseScaleInt / Mathf.Max(1f, designUpscale);
            float ui = normalized * Mathf.Max(0.01f, extraScaleMultiplier);

            ui /= canvasScale;
            ui = Mathf.Clamp(ui, minScale, maxScale);

            _lastBaseScaleInt = baseScaleInt;
            _lastUiScale = ui;

            return ui;
        }
    }

    int S(int baseSize) => Mathf.Clamp(Mathf.RoundToInt(baseSize * UiScale), 8, 300);
    float Px(float basePx) => Mathf.Round(basePx * UiScale);

    void Awake()
    {
        instanceInScene = this;

        if (targetRoot == null)
            targetRoot = transform.parent as RectTransform;

        EnsureBuilt();
    }

    void OnEnable()
    {
        _lastUiScale = -999f;
        _lastBaseScaleInt = -999;
        _lastCamPixelRect = default;
        _lastCamViewportRect = default;
        EnsureBuilt();
    }

    void OnDestroy()
    {
        if (runtimeFontMaterial != null)
            Destroy(runtimeFontMaterial);
    }

    void Update()
    {
        if (!BossRushSession.IsActive)
        {
            SetVisible(false);
            return;
        }

        BossRushSession.AddElapsed(Time.unscaledDeltaTime);

        ApplyRectScaleIfNeeded();
        SetVisible(true);

        string nextText = BossRushSession.GetFormattedElapsed();
        if (lastRenderedText != nextText)
        {
            lastRenderedText = nextText;
            timerText.text = nextText;
        }
    }

    void EnsureBuilt()
    {
        if (targetRoot == null)
            targetRoot = FindTargetRoot();

        RectTransform rootRect = transform as RectTransform;
        ApplyRootStretch(rootRect);

        transform.SetAsLastSibling();

        timerText = GetComponentInChildren<TMP_Text>(true);

        if (timerText == null)
        {
            GameObject textGo = new GameObject(TextName, typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(transform, false);

            RectTransform rect = textGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = anchoredOffsetAtDesign;
            rect.sizeDelta = new Vector2(baseRectWidthAtDesign, baseRectHeightAtDesign);

            timerText = textGo.GetComponent<TextMeshProUGUI>();
        }

        if (fontAsset == null)
            fontAsset = Resources.Load<TMP_FontAsset>("Fonts/PressStart2P-Regular SDF");

        if (fontAsset != null)
            timerText.font = fontAsset;

        Material sourceMaterial = null;

        if (fontMaterial != null)
            sourceMaterial = fontMaterial;
        else if (timerText.font != null && timerText.font.material != null)
            sourceMaterial = timerText.font.material;

        if (sourceMaterial != null)
        {
            if (runtimeFontMaterial != null)
                Destroy(runtimeFontMaterial);

            runtimeFontMaterial = new Material(sourceMaterial);
            ApplyMaterialColors(runtimeFontMaterial);

            timerText.fontSharedMaterial = runtimeFontMaterial;
        }

        timerText.raycastTarget = false;
        timerText.color = textColor;
        timerText.alignment = TextAlignmentOptions.TopRight;
        timerText.textWrappingMode = TextWrappingModes.NoWrap;
        timerText.overflowMode = TextOverflowModes.Overflow;
        timerText.fontSize = S(baseFontSizeAtDesign);
        timerText.text = BossRushSession.IsActive ? BossRushSession.GetFormattedElapsed() : "00:00.00";

        ApplyRectScaleIfNeeded(force: true);
        SetVisible(BossRushSession.IsActive);
    }

    void ApplyMaterialColors(Material mat)
    {
        if (mat == null)
            return;

        if (mat.HasProperty("_FaceColor"))
            mat.SetColor("_FaceColor", textColor);

        if (mat.HasProperty("_OutlineColor"))
            mat.SetColor("_OutlineColor", outlineColor);

        if (mat.HasProperty("_OutlineWidth"))
            mat.SetFloat("_OutlineWidth", outlineWidth);
    }

    void ApplyRootStretch(RectTransform rootRect)
    {
        if (rootRect == null)
            return;

        bool useCameraViewport = IsCanvasRoot(targetRoot);

        if (useCameraViewport)
        {
            Rect vr = GetMainCameraViewportRect();

            rootRect.anchorMin = new Vector2(vr.xMin, vr.yMin);
            rootRect.anchorMax = new Vector2(vr.xMax, vr.yMax);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            rootRect.anchoredPosition = Vector2.zero;
            rootRect.localScale = Vector3.one;
            return;
        }

        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;
        rootRect.anchoredPosition = Vector2.zero;
        rootRect.localScale = Vector3.one;
    }

    void ApplyRectScaleIfNeeded(bool force = false)
    {
        if (timerText == null)
            return;

        Rect camPixelRect = GetMainCameraPixelRect();
        Rect camViewportRect = GetMainCameraViewportRect();
        float uiScale = UiScale;

        bool changed =
            force ||
            camPixelRect != _lastCamPixelRect ||
            camViewportRect != _lastCamViewportRect ||
            Mathf.Abs(_lastUiScale - uiScale) > 0.001f;

        if (!changed)
            return;

        _lastCamPixelRect = camPixelRect;
        _lastCamViewportRect = camViewportRect;

        ApplyRootStretch(transform as RectTransform);

        timerText.fontSize = S(baseFontSizeAtDesign);

        var rt = timerText.rectTransform;
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(Px(anchoredOffsetAtDesign.x), Px(anchoredOffsetAtDesign.y));

        if (autoResizeRectTransform)
        {
            float rectWGame = baseRectWidthAtDesign / Mathf.Max(1f, designUpscale);
            float rectHGame = baseRectHeightAtDesign / Mathf.Max(1f, designUpscale);

            float rectW = Mathf.Round(rectWGame * Mathf.Max(1, _lastBaseScaleInt));
            float rectH = Mathf.Round(rectHGame * Mathf.Max(1, _lastBaseScaleInt));

            rt.sizeDelta = new Vector2(rectW, rectH);
        }
    }

    void SetVisible(bool visible)
    {
        if (timerText == null)
            return;

        if (timerText.gameObject.activeSelf != visible)
            timerText.gameObject.SetActive(visible);

        lastVisibleState = visible;
    }
}
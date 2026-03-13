using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class UnlockToastPresenter : MonoBehaviour
{
    const string RootName = "__UnlockToastPresenter";
    const string ToastRootName = "UnlockToastRoot";
    const string BackgroundName = "Background";
    const string IconName = "Icon";
    const string TitleName = "Title";
    const string SubtitleName = "Subtitle";
    const bool EnableSurgicalLogs = true;

    static UnlockToastPresenter instanceInScene;

    [Header("Resources")]
    [SerializeField] string backgroundSpriteResourcePath = "UI/Unlocks/ToastBackground";
    [SerializeField] TMP_FontAsset fontAsset;
    [SerializeField] Material fontMaterial;

    [Header("Toast Duration")]
    [SerializeField, Min(0.01f)] float fadeInDuration = 0.16f;
    [SerializeField, Min(0.01f)] float visibleDuration = 2.6f;
    [SerializeField, Min(0.01f)] float fadeOutDuration = 0.20f;
    [SerializeField, Min(0f)] float queueGapDuration = 0.05f;

    [Header("Dynamic Scale (Pixel Perfect friendly)")]
    [SerializeField] bool dynamicScale = true;
    [SerializeField] int referenceWidth = 256;
    [SerializeField] int referenceHeight = 224;
    [SerializeField] bool useIntegerUpscale = true;
    [SerializeField, Min(1)] int designUpscale = 4;
    [SerializeField, Min(0.01f)] float extraScaleMultiplier = 1f;
    [SerializeField, Min(0.01f)] float minScale = 0.5f;
    [SerializeField, Min(0.01f)] float maxScale = 10f;

    [Header("Toast Design Size")]
    [SerializeField, Min(1)] float baseToastWidthAtDesign = 160f;
    [SerializeField, Min(1)] float baseToastHeightAtDesign = 48f;
    [SerializeField] Vector2 anchoredOffsetAtDesign = new(-8f, -8f);

    [Header("Toast Extra Scaling")]
    [SerializeField, Min(0.5f)] float toastScaleMultiplier = 1.75f;
    [SerializeField, Min(0.5f)] float textScaleMultiplier = 1.10f;
    [SerializeField, Range(0.20f, 0.80f)] float maxToastWidthPercentOfRoot = 0.42f;

    [Header("Layout")]
    [SerializeField, Min(1)] float baseLeftPadding = 8f;
    [SerializeField, Min(1)] float baseRightPadding = 8f;
    [SerializeField, Min(1)] float baseTopPadding = 8f;
    [SerializeField, Min(1)] float baseBottomPadding = 8f;
    [SerializeField, Min(1)] float baseIconSize = 32f;
    [SerializeField, Min(0)] float baseGapAfterIcon = 8f;
    [SerializeField, Min(0)] float baseTextVerticalOffset = 0f;
    [SerializeField, Min(1)] int baseTitleFontSizeAtDesign = 16;
    [SerializeField, Min(1)] int baseSubtitleFontSizeAtDesign = 12;
    [SerializeField, Min(0)] float baseLineSpacing = 2f;
    [SerializeField, Min(0)] float baseSafetyRightPadding = 6f;

    [Header("TMP Auto Size")]
    [SerializeField, Range(0.4f, 1f)] float titleMinScaleRatio = 0.72f;
    [SerializeField, Range(0.4f, 1f)] float subtitleMinScaleRatio = 0.78f;

    [Header("Colors")]
    [SerializeField] Color backgroundColor = new(1f, 1f, 1f, 1f);
    [SerializeField] Color iconColor = Color.white;
    [SerializeField] Color titleColor = new(0.98f, 0.90f, 0.36f, 1f);
    [SerializeField] Color subtitleColor = new(0.45f, 0.87f, 1f, 1f);
    [SerializeField] Color outlineColor = new(0.11f, 0.11f, 0.11f, 1f);
    [SerializeField, Range(0f, 1f)] float outlineWidth = 0.22f;

    RectTransform targetRoot;
    RectTransform toastRoot;
    Image backgroundImage;
    Image iconImage;
    TMP_Text titleText;
    TMP_Text subtitleText;
    CanvasGroup canvasGroup;

    Material runtimeTitleMaterial;
    Material runtimeSubtitleMaterial;

    readonly Queue<ToastRequest> queue = new();
    Coroutine playRoutine;

    float lastUiScale = -999f;
    int lastBaseScaleInt = -999;
    Rect lastCamPixelRect;
    Rect lastCamViewportRect;
    Vector2 lastRootSize = new(float.MinValue, float.MinValue);

    sealed class ToastRequest
    {
        public string Title;
        public string Subtitle;
        public Sprite Icon;
    }

    public static void EnsureInScene()
    {
        if (instanceInScene != null && instanceInScene.gameObject != null)
        {
            if (!instanceInScene.gameObject.activeInHierarchy)
            {
                SLog("EnsureInScene | cached instance inactive, trying MoveToActiveRoot");
                instanceInScene.MoveToActiveRoot();
            }

            instanceInScene.RefreshTargetRoot();
            instanceInScene.EnsureBuilt();
            SLog($"EnsureInScene | reused cached instance | activeInHierarchy={instanceInScene.gameObject.activeInHierarchy}");
            return;
        }

        var existing = FindAnyPresenter();
        if (existing != null)
        {
            instanceInScene = existing;

            if (!instanceInScene.gameObject.activeInHierarchy)
            {
                SLog("EnsureInScene | existing presenter inactive, trying MoveToActiveRoot");
                instanceInScene.MoveToActiveRoot();
            }

            instanceInScene.RefreshTargetRoot();
            instanceInScene.EnsureBuilt();
            SLog($"EnsureInScene | found existing presenter | activeInHierarchy={instanceInScene.gameObject.activeInHierarchy}");
            return;
        }

        RectTransform parentRoot = FindTargetRoot();
        if (parentRoot == null)
        {
            SLog("EnsureInScene | no valid active UI root found");
            return;
        }

        GameObject root = new GameObject(RootName, typeof(RectTransform));
        root.transform.SetParent(parentRoot, false);
        root.SetActive(true);

        instanceInScene = root.AddComponent<UnlockToastPresenter>();
        instanceInScene.targetRoot = parentRoot;
        instanceInScene.EnsureBuilt();

        SLog($"EnsureInScene | created presenter under {parentRoot.name} | activeInHierarchy={root.activeInHierarchy}");
    }

    public static void ShowSkinUnlocked(BomberSkin skin)
    {
        EnsureInScene();

        if (instanceInScene == null)
        {
            SLog($"ShowSkinUnlocked aborted | instance null | skin={skin}");
            return;
        }

        var info = BomberSkinUnlockToastCatalog.Get(skin);
        var icon = BomberSkinUnlockToastCatalog.LoadIcon(skin);

        SLog($"ShowSkinUnlocked | skin={skin} | title={info.Title} | subtitle={info.Subtitle} | iconLoaded={(icon != null)}");

        instanceInScene.Enqueue(info.Title, info.Subtitle, icon);
    }

    public static void Show(string title, string subtitle, Sprite icon = null)
    {
        EnsureInScene();

        if (instanceInScene == null)
        {
            SLog("Show aborted | instance null");
            return;
        }

        SLog($"Show | title={title} | subtitle={subtitle} | iconLoaded={(icon != null)}");
        instanceInScene.Enqueue(title, subtitle, icon);
    }

    void Awake()
    {
        if (instanceInScene != null && instanceInScene != this)
        {
            SLog("Awake | duplicate presenter destroyed");
            Destroy(gameObject);
            return;
        }

        instanceInScene = this;

        if (targetRoot == null)
            targetRoot = transform.parent as RectTransform;

        SLog($"Awake | targetRoot={(targetRoot != null ? targetRoot.name : "null")}");

        EnsureBuilt();
    }

    void OnEnable()
    {
        BomberSkinUnlockProgress.OnSkinUnlocked -= HandleSkinUnlocked;
        BomberSkinUnlockProgress.OnSkinUnlocked += HandleSkinUnlocked;

        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;

        lastUiScale = -999f;
        lastBaseScaleInt = -999;
        lastCamPixelRect = default;
        lastCamViewportRect = default;
        lastRootSize = new(float.MinValue, float.MinValue);

        EnsureBuilt();
        SLog("OnEnable");
    }

    void OnDisable()
    {
        BomberSkinUnlockProgress.OnSkinUnlocked -= HandleSkinUnlocked;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SLog("OnDisable");
    }

    void OnDestroy()
    {
        if (instanceInScene == this)
            instanceInScene = null;

        if (runtimeTitleMaterial != null)
            Destroy(runtimeTitleMaterial);

        if (runtimeSubtitleMaterial != null)
            Destroy(runtimeSubtitleMaterial);

        SLog("OnDestroy");
    }

    void Update()
    {
        RefreshTargetRootIfNeeded();
        ApplyRectScaleIfNeeded();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (this == null)
            return;

        RefreshTargetRoot();
        EnsureBuilt();
        SLog($"OnSceneLoaded | scene={scene.name} | mode={mode}");
    }

    void HandleSkinUnlocked(BomberSkin skin)
    {
        SLog($"HandleSkinUnlocked received | skin={skin}");
        ShowSkinUnlocked(skin);
    }

    void Enqueue(string title, string subtitle, Sprite icon)
    {
        queue.Enqueue(new ToastRequest
        {
            Title = title,
            Subtitle = subtitle,
            Icon = icon
        });

        SLog($"Enqueue | title={title} | queueCount={queue.Count} | playRoutineNull={playRoutine == null} | activeInHierarchy={gameObject.activeInHierarchy}");

        if (playRoutine != null)
            return;

        if (!gameObject.activeInHierarchy || !isActiveAndEnabled)
        {
            SLog("Enqueue | presenter inactive, attempting MoveToActiveRoot before starting coroutine");
            MoveToActiveRoot();
        }

        if (!gameObject.activeInHierarchy || !isActiveAndEnabled)
        {
            SLog("Enqueue aborted | presenter still inactive after MoveToActiveRoot");
            return;
        }

        playRoutine = StartCoroutine(PlayQueue());
        SLog("Enqueue | started PlayQueue coroutine");
    }

    IEnumerator PlayQueue()
    {
        SLog($"PlayQueue started | initialCount={queue.Count}");

        while (queue.Count > 0)
        {
            ToastRequest request = queue.Dequeue();

            SLog($"PlayQueue dequeue | title={request.Title} | remaining={queue.Count}");

            RefreshTargetRoot();
            EnsureBuilt();
            ApplyToast(request);

            yield return AnimateToast(0f, 1f, fadeInDuration);
            yield return new WaitForSecondsRealtime(visibleDuration);
            yield return AnimateToast(1f, 0f, fadeOutDuration);

            if (queueGapDuration > 0f)
                yield return new WaitForSecondsRealtime(queueGapDuration);
        }

        playRoutine = null;
        SLog("PlayQueue finished");
    }

    IEnumerator AnimateToast(float from, float to, float duration)
    {
        if (canvasGroup == null || toastRoot == null)
        {
            SLog($"AnimateToast aborted | canvasGroupNull={canvasGroup == null} | toastRootNull={toastRoot == null}");
            yield break;
        }

        float t = 0f;
        Vector2 shown = GetShownPosition();
        Vector2 hidden = GetHiddenPosition();

        SLog($"AnimateToast start | from={from} | to={to} | duration={duration:0.000} | shown={shown} | hidden={hidden}");

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float p = duration <= 0f ? 1f : Mathf.Clamp01(t / duration);
            float eased = 1f - Mathf.Pow(1f - p, 3f);

            canvasGroup.alpha = Mathf.Lerp(from, to, eased);
            toastRoot.anchoredPosition = Vector2.Lerp(
                from < to ? hidden : shown,
                from < to ? shown : hidden,
                eased);

            yield return null;
        }

        canvasGroup.alpha = to;
        toastRoot.anchoredPosition = to > 0.001f ? shown : hidden;

        SLog($"AnimateToast end | finalAlpha={canvasGroup.alpha:0.000} | finalPos={toastRoot.anchoredPosition}");
    }

    void ApplyToast(ToastRequest request)
    {
        if (titleText == null || subtitleText == null || iconImage == null || canvasGroup == null)
        {
            SLog($"ApplyToast aborted | titleNull={titleText == null} | subtitleNull={subtitleText == null} | iconNull={iconImage == null} | canvasGroupNull={canvasGroup == null}");
            return;
        }

        titleText.text = request.Title ?? "";
        subtitleText.text = request.Subtitle ?? "";

        if (request.Icon != null)
        {
            iconImage.sprite = request.Icon;
            iconImage.enabled = true;
            iconImage.color = iconColor;
        }
        else
        {
            iconImage.sprite = null;
            iconImage.enabled = false;
        }

        canvasGroup.alpha = 0f;
        ApplyRectScaleIfNeeded(force: true);
        toastRoot.anchoredPosition = GetHiddenPosition();

        SLog($"ApplyToast | title={titleText.text} | subtitle={subtitleText.text} | iconEnabled={iconImage.enabled} | toastSize={(toastRoot != null ? toastRoot.sizeDelta.ToString() : "null")} | hiddenPos={toastRoot.anchoredPosition}");
    }

    void EnsureBuilt()
    {
        if (targetRoot == null)
            targetRoot = FindTargetRoot();

        RectTransform rootRect = transform as RectTransform;
        ApplyRootStretch(rootRect);

        transform.SetAsLastSibling();

        if (toastRoot == null)
        {
            GameObject toastGo = new GameObject(ToastRootName, typeof(RectTransform), typeof(CanvasGroup));
            toastGo.transform.SetParent(transform, false);
            toastRoot = toastGo.GetComponent<RectTransform>();
            canvasGroup = toastGo.GetComponent<CanvasGroup>();
            SLog("EnsureBuilt | created toastRoot + canvasGroup");
        }

        if (backgroundImage == null)
        {
            GameObject bgGo = new GameObject(BackgroundName, typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(toastRoot, false);
            backgroundImage = bgGo.GetComponent<Image>();
            backgroundImage.raycastTarget = false;
            SLog("EnsureBuilt | created background image");
        }

        if (iconImage == null)
        {
            GameObject iconGo = new GameObject(IconName, typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(toastRoot, false);
            iconImage = iconGo.GetComponent<Image>();
            iconImage.raycastTarget = false;
            iconImage.preserveAspect = true;
            SLog("EnsureBuilt | created icon image");
        }

        if (titleText == null)
        {
            GameObject titleGo = new GameObject(TitleName, typeof(RectTransform), typeof(TextMeshProUGUI));
            titleGo.transform.SetParent(toastRoot, false);
            titleText = titleGo.GetComponent<TextMeshProUGUI>();
            SLog("EnsureBuilt | created title text");
        }

        if (subtitleText == null)
        {
            GameObject subtitleGo = new GameObject(SubtitleName, typeof(RectTransform), typeof(TextMeshProUGUI));
            subtitleGo.transform.SetParent(toastRoot, false);
            subtitleText = subtitleGo.GetComponent<TextMeshProUGUI>();
            SLog("EnsureBuilt | created subtitle text");
        }

        if (fontAsset == null)
            fontAsset = Resources.Load<TMP_FontAsset>("Fonts/PressStart2P-Regular SDF");

        SetupText(titleText, true);
        SetupText(subtitleText, false);

        Sprite bgSprite = null;
        if (!string.IsNullOrWhiteSpace(backgroundSpriteResourcePath))
            bgSprite = Resources.Load<Sprite>(backgroundSpriteResourcePath);

        backgroundImage.sprite = bgSprite;
        backgroundImage.type = bgSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        backgroundImage.color = backgroundColor;

        ApplyRectScaleIfNeeded(force: true);

        SLog($"EnsureBuilt | targetRoot={(targetRoot != null ? targetRoot.name : "null")} | fontLoaded={(fontAsset != null)} | bgLoaded={(bgSprite != null)}");
    }

    void SetupText(TMP_Text text, bool isTitle)
    {
        if (text == null)
            return;

        if (fontAsset != null)
            text.font = fontAsset;

        Material sourceMaterial = null;
        if (fontMaterial != null)
            sourceMaterial = fontMaterial;
        else if (text.font != null && text.font.material != null)
            sourceMaterial = text.font.material;

        if (sourceMaterial != null)
        {
            Material runtimeMaterial = new Material(sourceMaterial);
            ApplyMaterialColors(runtimeMaterial, isTitle ? titleColor : subtitleColor);

            if (isTitle)
            {
                if (runtimeTitleMaterial != null)
                    Destroy(runtimeTitleMaterial);

                runtimeTitleMaterial = runtimeMaterial;
                text.fontSharedMaterial = runtimeTitleMaterial;
            }
            else
            {
                if (runtimeSubtitleMaterial != null)
                    Destroy(runtimeSubtitleMaterial);

                runtimeSubtitleMaterial = runtimeMaterial;
                text.fontSharedMaterial = runtimeSubtitleMaterial;
            }
        }

        text.raycastTarget = false;
        text.alignment = TextAlignmentOptions.Left;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.enableAutoSizing = true;
        text.color = isTitle ? titleColor : subtitleColor;

        SLog($"SetupText | isTitle={isTitle} | hasFont={(text.font != null)} | hasMaterial={(sourceMaterial != null)}");
    }

    void ApplyMaterialColors(Material mat, Color faceColor)
    {
        if (mat == null)
            return;

        if (mat.HasProperty("_FaceColor"))
            mat.SetColor("_FaceColor", faceColor);

        if (mat.HasProperty("_OutlineColor"))
            mat.SetColor("_OutlineColor", outlineColor);

        if (mat.HasProperty("_OutlineWidth"))
            mat.SetFloat("_OutlineWidth", outlineWidth);
    }

    void RefreshTargetRoot()
    {
        RectTransform newRoot = FindTargetRoot();
        if (newRoot == null)
        {
            SLog("RefreshTargetRoot | no valid target root found");
            return;
        }

        if (targetRoot != newRoot)
            SLog($"RefreshTargetRoot | target changed from {(targetRoot != null ? targetRoot.name : "null")} to {newRoot.name}");

        targetRoot = newRoot;

        RectTransform rootRect = transform as RectTransform;
        if (rootRect != null && rootRect.parent != targetRoot)
        {
            rootRect.SetParent(targetRoot, false);
            ApplyRootStretch(rootRect);
            transform.SetAsLastSibling();
            ApplyRectScaleIfNeeded(force: true);
            SLog($"RefreshTargetRoot | presenter reparented to {targetRoot.name}");
        }
    }

    void RefreshTargetRootIfNeeded()
    {
        if (targetRoot == null || !targetRoot.gameObject.scene.isLoaded)
        {
            SLog($"RefreshTargetRootIfNeeded | currentRoot={(targetRoot != null ? targetRoot.name : "null")} | loaded={(targetRoot != null && targetRoot.gameObject.scene.isLoaded)}");
            RefreshTargetRoot();
            return;
        }

        Vector2 rootSize = targetRoot.rect.size;
        if (rootSize != lastRootSize)
        {
            lastRootSize = rootSize;
            ApplyRectScaleIfNeeded(force: true);
            SLog($"RefreshTargetRootIfNeeded | root size changed to {rootSize}");
        }
    }

    static UnlockToastPresenter FindAnyPresenter()
    {
        var presenters = FindObjectsByType<UnlockToastPresenter>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (presenters == null || presenters.Length == 0)
            return null;

        UnlockToastPresenter first = presenters[0];

        for (int i = 1; i < presenters.Length; i++)
        {
            if (presenters[i] != null && presenters[i] != first)
            {
                SLog($"FindAnyPresenter | destroying duplicate presenter={presenters[i].name}");
                Destroy(presenters[i].gameObject);
            }
        }

        return first;
    }

    static RectTransform FindTargetRoot()
    {
        var safeFrameGo = GameObject.Find("SafeFrame4x3");
        if (safeFrameGo != null &&
            safeFrameGo.activeInHierarchy &&
            safeFrameGo.TryGetComponent<RectTransform>(out var safeFrameRt))
        {
            SLog("FindTargetRoot | using active SafeFrame4x3");
            return safeFrameRt;
        }

        var fitters = FindObjectsByType<UICameraViewportFitter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < fitters.Length; i++)
        {
            if (fitters[i] == null || !fitters[i].gameObject.activeInHierarchy)
                continue;

            var rt = fitters[i].transform as RectTransform;
            if (rt != null && rt.gameObject.activeInHierarchy)
            {
                SLog($"FindTargetRoot | using active UICameraViewportFitter root={rt.name}");
                return rt;
            }
        }

        var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Canvas bestCanvas = null;

        for (int i = 0; i < canvases.Length; i++)
        {
            var c = canvases[i];
            if (c == null || !c.isActiveAndEnabled || !c.gameObject.activeInHierarchy)
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
        {
            SLog($"FindTargetRoot | using active canvas root={bestCanvas.name} | renderMode={bestCanvas.renderMode}");
            return bestCanvas.transform as RectTransform;
        }

        SLog("FindTargetRoot | nothing active found");
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
            if (backgroundImage != null && backgroundImage.canvas != null)
                canvasScale = Mathf.Max(0.01f, backgroundImage.canvas.scaleFactor);

            float returnValue;

            if (!dynamicScale)
            {
                returnValue = 1f / canvasScale;
                lastBaseScaleInt = -1;
                lastUiScale = returnValue;
                return returnValue;
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

            lastBaseScaleInt = baseScaleInt;
            lastUiScale = ui;
            returnValue = ui;

            return returnValue;
        }
    }

    int TextS(int baseSize)
    {
        int returnValue = Mathf.Clamp(Mathf.RoundToInt(baseSize * UiScale * textScaleMultiplier), 8, 300);
        return returnValue;
    }

    float ToastPx(float basePx)
    {
        float returnValue = Mathf.Round(basePx * UiScale * toastScaleMultiplier);
        return returnValue;
    }

    float TextPx(float basePx)
    {
        float returnValue = Mathf.Round(basePx * UiScale * textScaleMultiplier);
        return returnValue;
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
        if (toastRoot == null || backgroundImage == null || iconImage == null || titleText == null || subtitleText == null)
            return;

        Rect camPixelRect = GetMainCameraPixelRect();
        Rect camViewportRect = GetMainCameraViewportRect();
        float uiScale = UiScale;
        Vector2 rootSize = targetRoot != null ? targetRoot.rect.size : Vector2.zero;

        bool changed =
            force ||
            camPixelRect != lastCamPixelRect ||
            camViewportRect != lastCamViewportRect ||
            Mathf.Abs(lastUiScale - uiScale) > 0.001f ||
            rootSize != lastRootSize;

        if (!changed)
            return;

        lastCamPixelRect = camPixelRect;
        lastCamViewportRect = camViewportRect;
        lastRootSize = rootSize;

        ApplyRootStretch(transform as RectTransform);

        float left = ToastPx(baseLeftPadding);
        float right = ToastPx(baseRightPadding + baseSafetyRightPadding);
        float top = ToastPx(baseTopPadding);
        float bottom = ToastPx(baseBottomPadding);
        float iconSize = ToastPx(baseIconSize);
        float gap = ToastPx(baseGapAfterIcon);
        float textOffsetY = TextPx(baseTextVerticalOffset);
        float lineSpacing = TextPx(baseLineSpacing);

        int titleMaxSize = TextS(baseTitleFontSizeAtDesign);
        int subtitleMaxSize = TextS(baseSubtitleFontSizeAtDesign);

        titleText.enableAutoSizing = true;
        titleText.fontSizeMax = titleMaxSize;
        titleText.fontSizeMin = Mathf.Max(8, Mathf.RoundToInt(titleMaxSize * titleMinScaleRatio));
        titleText.textWrappingMode = TextWrappingModes.NoWrap;
        titleText.overflowMode = TextOverflowModes.Ellipsis;

        subtitleText.enableAutoSizing = true;
        subtitleText.fontSizeMax = subtitleMaxSize;
        subtitleText.fontSizeMin = Mathf.Max(8, Mathf.RoundToInt(subtitleMaxSize * subtitleMinScaleRatio));
        subtitleText.textWrappingMode = TextWrappingModes.NoWrap;
        subtitleText.overflowMode = TextOverflowModes.Ellipsis;

        float minWidth = ToastPx(baseToastWidthAtDesign);
        float minHeight = ToastPx(baseToastHeightAtDesign);

        float maxWidthFromRoot = rootSize.x > 0f
            ? Mathf.Floor(rootSize.x * maxToastWidthPercentOfRoot)
            : minWidth;

        if (maxWidthFromRoot < minWidth)
            maxWidthFromRoot = minWidth;

        float availableTextWidthAtMax = Mathf.Max(16f, maxWidthFromRoot - left - iconSize - gap - right);

        titleText.rectTransform.sizeDelta = new Vector2(availableTextWidthAtMax, titleMaxSize + 8f);
        subtitleText.rectTransform.sizeDelta = new Vector2(availableTextWidthAtMax, subtitleMaxSize + 8f);

        titleText.ForceMeshUpdate();
        subtitleText.ForceMeshUpdate();

        float preferredTextWidth = Mathf.Max(titleText.preferredWidth, subtitleText.preferredWidth);
        float contentWidth = left + iconSize + gap + preferredTextWidth + right;
        float finalWidth = Mathf.Clamp(Mathf.Ceil(contentWidth), minWidth, maxWidthFromRoot);

        float titleHeight = Mathf.Ceil(titleMaxSize + TextPx(6f));
        float subtitleHeight = Mathf.Ceil(subtitleMaxSize + TextPx(6f));
        float contentHeight = Mathf.Max(minHeight, top + bottom + titleHeight + lineSpacing + subtitleHeight);
        float finalHeight = Mathf.Ceil(contentHeight);

        toastRoot.anchorMin = new Vector2(1f, 1f);
        toastRoot.anchorMax = new Vector2(1f, 1f);
        toastRoot.pivot = new Vector2(1f, 1f);
        toastRoot.sizeDelta = new Vector2(finalWidth, finalHeight);
        toastRoot.anchoredPosition = canvasGroup != null && canvasGroup.alpha > 0.001f ? GetShownPosition() : GetHiddenPosition();

        RectTransform bgRt = backgroundImage.rectTransform;
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;

        RectTransform iconRt = iconImage.rectTransform;
        iconRt.anchorMin = new Vector2(0f, 0.5f);
        iconRt.anchorMax = new Vector2(0f, 0.5f);
        iconRt.pivot = new Vector2(0f, 0.5f);
        iconRt.sizeDelta = new Vector2(iconSize, iconSize);
        iconRt.anchoredPosition = new Vector2(left, 0f);

        float textLeft = left + iconSize + gap;
        float textRightInset = right;

        RectTransform titleRt = titleText.rectTransform;
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0f, 1f);
        titleRt.offsetMin = new Vector2(textLeft, -(top + titleHeight + textOffsetY));
        titleRt.offsetMax = new Vector2(-textRightInset, -(top + textOffsetY));

        RectTransform subtitleRt = subtitleText.rectTransform;
        subtitleRt.anchorMin = new Vector2(0f, 1f);
        subtitleRt.anchorMax = new Vector2(1f, 1f);
        subtitleRt.pivot = new Vector2(0f, 1f);
        subtitleRt.offsetMin = new Vector2(textLeft, -(top + titleHeight + lineSpacing + subtitleHeight + textOffsetY));
        subtitleRt.offsetMax = new Vector2(-textRightInset, -(top + titleHeight + lineSpacing + textOffsetY));

        SLog(
            $"ApplyRectScaleIfNeeded | force={force} | uiScale={uiScale:0.000} | baseScaleInt={lastBaseScaleInt} | rootSize={rootSize} | " +
            $"minWidth={minWidth:0.00} | maxWidth={maxWidthFromRoot:0.00} | preferredTextWidth={preferredTextWidth:0.00} | " +
            $"finalToast=({finalWidth:0.00}, {finalHeight:0.00}) | shown={GetShownPosition()} | hidden={GetHiddenPosition()} | camPixelRect={camPixelRect}");
    }

    void MoveToActiveRoot()
    {
        RectTransform newRoot = FindTargetRoot();
        if (newRoot == null)
        {
            SLog("MoveToActiveRoot | no active target root found");
            return;
        }

        targetRoot = newRoot;

        RectTransform rootRect = transform as RectTransform;
        if (rootRect != null)
        {
            rootRect.SetParent(newRoot, false);
            ApplyRootStretch(rootRect);
            transform.SetAsLastSibling();
        }

        gameObject.SetActive(true);

        SLog($"MoveToActiveRoot | moved presenter to {newRoot.name} | activeInHierarchy={gameObject.activeInHierarchy}");
    }

    Vector2 GetShownPosition()
    {
        Vector2 returnValue = new Vector2(ToastPx(anchoredOffsetAtDesign.x), ToastPx(anchoredOffsetAtDesign.y));
        return returnValue;
    }

    Vector2 GetHiddenPosition()
    {
        Vector2 shown = GetShownPosition();
        float extra = toastRoot != null ? toastRoot.sizeDelta.x + ToastPx(10f) : ToastPx(baseToastWidthAtDesign);
        Vector2 returnValue = new Vector2(shown.x + extra, shown.y);
        return returnValue;
    }

    static void SLog(string message)
    {
        if (!EnableSurgicalLogs)
            return;

        Debug.Log($"[UnlockToastPresenter] {message}");
    }
}
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StageLabel : MonoBehaviour
{
    public TMP_Text stageText;

    static readonly string NBSP = "\u00A0";

    const int SizeStageLabel = 44;
    const int SizeStageNumber = 40;
    const int SizePauseTitle = 38;
    const int SizeMenuItem = 34;
    const int SizeConfirmTitle = 30;
    const int SizeConfirmSubtitle = 26;

    const int OptionsIndentPxBase = -18;

    [Header("Dynamic Scale (Pixel Perfect friendly)")]
    [SerializeField] bool dynamicScale = true;

    [SerializeField] int referenceWidth = 256;
    [SerializeField] int referenceHeight = 224;

    [SerializeField] bool useIntegerUpscale = true;
    [SerializeField, Min(1)] int designUpscale = 4;
    [SerializeField, Min(0.01f)] float extraScaleMultiplier = 1f;
    [SerializeField, Min(0.01f)] float minScale = 0.5f;
    [SerializeField, Min(0.01f)] float maxScale = 10f;

    [Header("RectTransform Scaling")]
    [SerializeField, Min(1f)] float baseRectWidthAtDesign = 600f;
    [SerializeField, Min(1f)] float baseRectHeightAtDesign = 200f;
    [SerializeField] bool autoResizeRectTransform = true;

    [Header("Pause Window")]
    [SerializeField] bool showPauseWindow = true;
    [SerializeField] Color pauseWindowFill = new Color(0f, 0f, 0f, 0.5f);
    [SerializeField] Color pauseWindowOuterBorder = new Color(0.16f, 0.08f, 0f, 1f);
    [SerializeField] Color pauseWindowGoldBorder = new Color(1f, 0.64f, 0f, 1f);
    [SerializeField] Color pauseWindowHighlightBorder = new Color(1f, 0.96f, 0.08f, 1f);
    [SerializeField] Color pauseWindowInnerBorder = new Color(0.05f, 0f, 0f, 1f);
    [SerializeField] Color pauseWindowShadow = new Color(0.22f, 0.08f, 0f, 0.82f);
    [SerializeField] Vector2 pauseWindowPaddingAtDesign = new Vector2(44f, 28f);
    [SerializeField, Min(1f)] float pauseWindowOuterBorderThicknessAtDesign = 5f;
    [SerializeField, Min(1f)] float pauseWindowGoldBorderThicknessAtDesign = 5f;
    [SerializeField, Min(1f)] float pauseWindowHighlightThicknessAtDesign = 3f;
    [SerializeField, Min(1f)] float pauseWindowInnerBorderThicknessAtDesign = 5f;
    [SerializeField] Vector2 pauseWindowShadowDistanceAtDesign = new Vector2(7f, -7f);
    [SerializeField] bool logPauseWindowDiagnostics = false;

    RectTransform pauseWindowRect;
    RectTransform pauseWindowFillRect;
    Image pauseWindowImage;
    Image pauseWindowFillImage;
    Shadow pauseWindowShadowEffect;
    readonly RectTransform[] pauseWindowFrameRects = new RectTransform[16];
    readonly Image[] pauseWindowFrameImages = new Image[16];
    bool pauseWindowLastVisibleState;
    Vector2 lastLoggedWindowSize = new Vector2(float.MinValue, float.MinValue);
    Vector2 lastLoggedWindowPosition = new Vector2(float.MinValue, float.MinValue);
    float lastLoggedPreferredWidth = float.MinValue;
    int lastLoggedTextSiblingIndex = int.MinValue;
    int lastLoggedWindowSiblingIndex = int.MinValue;

    float _lastUiScale = -999f;
    int _lastBaseScaleInt = -999;

    float UiScale
    {
        get
        {
            float canvasScale = 1f;
            if (stageText != null && stageText.canvas != null)
                canvasScale = Mathf.Max(0.01f, stageText.canvas.scaleFactor);

            if (!dynamicScale)
            {
                float fallback = 1f / canvasScale;

                _lastBaseScaleInt = -1;
                _lastUiScale = fallback;

                return fallback;
            }

            var cam = Camera.main;

            float usedW = cam != null ? cam.pixelRect.width : Screen.width;
            float usedH = cam != null ? cam.pixelRect.height : Screen.height;

            float sx = usedW / Mathf.Max(1f, referenceWidth);
            float sy = usedH / Mathf.Max(1f, referenceHeight);
            float baseScaleRaw = Mathf.Min(sx, sy);

            float baseScaleForUi = useIntegerUpscale ? Mathf.Round(baseScaleRaw) : baseScaleRaw;
            if (baseScaleForUi < 1f) baseScaleForUi = 1f;

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

    int S(int baseSize) => Mathf.Clamp(Mathf.RoundToInt(baseSize * UiScale), 10, 500);
    int Px(int basePx) => Mathf.RoundToInt(basePx * UiScale);

    void Awake()
    {
        ApplyRectScale("Awake");
    }

    void OnEnable()
    {
        _lastUiScale = -999f;
        _lastBaseScaleInt = -999;
        ApplyRectScale("OnEnable");
    }

    void OnDisable()
    {
        HidePauseWindow();
    }

    void Update()
    {
        if (stageText == null) return;
        ApplyRectScale("Update");
    }

    void EnsureNoWrap()
    {
        if (stageText == null) return;
        stageText.textWrappingMode = TextWrappingModes.NoWrap;
        stageText.overflowMode = TextOverflowModes.Overflow;
        stageText.richText = true;
    }

    public void SetStage(int world, int stage)
    {
        EnsureNoWrap();
        SetPauseWindowVisible(false);

        string stageNumber = $"{world}-{stage}";

        stageText.text =
            "<align=center>" +
            $"<size={S(SizeStageLabel)}><color=#1ABC00>STAGE</color></size>  " +
            $"<size={S(SizeStageNumber)}><color=#E8E8E8>{stageNumber}</color></size>" +
            "</align>";
    }

    public void SetPauseMenu(int world, int stage, int selectedIndex, bool isBossRush)
    {
        EnsureNoWrap();

        string stageNumber = $"{world}-{stage}";

        string resume = selectedIndex == 0
            ? "<color=#FF6F31>> RESUME</color>"
            : "<color=#E8E8E8>  RESUME</color>";

        string secondOptionText = isBossRush
            ? $"RETURN{NBSP}TO{NBSP}BOSS{NBSP}RUSH"
            : $"RETURN{NBSP}TO{NBSP}WORLD{NBSP}MAP";

        string retSecondOption = selectedIndex == 1
            ? $"<color=#FF6F31>> {secondOptionText}</color>"
            : $"<color=#E8E8E8>  {secondOptionText}</color>";

        string titleText = $"RETURN{NBSP}TO{NBSP}TITLE";
        string retTitle = selectedIndex == 2
            ? $"<color=#FF6F31>> {titleText}</color>"
            : $"<color=#E8E8E8>  {titleText}</color>";

        stageText.text =
            "<align=center>" +
            $"<size={S(SizeStageLabel)}><color=#1ABC00>STAGE</color></size>  " +
            $"<size={S(SizeStageNumber)}><color=#E8E8E8>{stageNumber}</color></size>\n" +
            $"<size={S(SizePauseTitle)}><color=#3392FF>PAUSE!</color></size>\n\n" +
            $"<size={S(SizeMenuItem)}>{resume}</size>\n" +
            $"<size={S(SizeMenuItem)}>{retSecondOption}</size>\n" +
            $"<size={S(SizeMenuItem)}>{retTitle}</size>" +
            "</align>";

        SetPauseWindowVisible(true);
    }

    public void SetPauseConfirmReturnToWorldMap(int world, int stage, int selectedIndex)
    {
        SetPauseConfirmQuestion(world, stage, selectedIndex, "Return to World Map?", true);
    }

    public void SetPauseConfirmReturnToBossRush(int world, int stage, int selectedIndex)
    {
        SetPauseConfirmQuestion(world, stage, selectedIndex, "Return to Boss Rush?", true);
    }

    public void SetPauseConfirmReturnToTitle(int world, int stage, int selectedIndex)
    {
        SetPauseConfirmQuestion(world, stage, selectedIndex, "Return to Title Screen?", true);
    }

    public void SetBattleModePauseConfirmReturnToTitle(int world, int stage, int selectedIndex)
    {
        SetPauseConfirmQuestion(world, stage, selectedIndex, "Return to Title Screen?", false);
    }

    void SetPauseConfirmQuestion(int world, int stage, int selectedIndex, string question, bool showStageHeader)
    {
        EnsureNoWrap();

        string stageNumber = $"{world}-{stage}";
        string stageHeader = showStageHeader
            ? $"<size={S(SizeStageLabel)}><color=#1ABC00>STAGE</color></size>  " +
              $"<size={S(SizeStageNumber)}><color=#E8E8E8>{stageNumber}</color></size>\n"
            : string.Empty;

        string ArrowVisible(string text) => $"<color=#FF6F31>> </color>{text}";
        string ArrowHidden(string text) => $"<color=#00000000>> </color>{text}";

        string noOpt = selectedIndex == 0
            ? ArrowVisible("<color=#FF6F31>NO</color>")
            : ArrowHidden("<color=#E8E8E8>NO</color>");

        string yesOpt = selectedIndex == 1
            ? ArrowVisible("<color=#FF6F31>YES</color>")
            : ArrowHidden("<color=#E8E8E8>YES</color>");

        int indent = Px(OptionsIndentPxBase);

        stageText.text =
            "<align=center>" +
            stageHeader +
            $"<size={S(SizePauseTitle)}><color=#3392FF>PAUSE!</color></size>\n\n" +
            $"<size={S(SizeConfirmTitle)}><color=#E8E8E8>{question}</color></size>\n\n" +
            $"<size={S(SizeMenuItem)}><indent={indent}>{noOpt}</indent></size>\n" +
            $"<size={S(SizeMenuItem)}><indent={indent}>{yesOpt}</indent></size>" +
            "</align>";

        SetPauseWindowVisible(true);
    }

    void ApplyRectScale(string tag)
    {
        if (!autoResizeRectTransform || stageText == null) return;

        var cam = Camera.main;
        Rect r = cam != null ? cam.pixelRect : new Rect(0, 0, Screen.width, Screen.height);

        _ = UiScale;

        if (_lastBaseScaleInt <= 0) return;

        float rectWGame = baseRectWidthAtDesign / Mathf.Max(1f, designUpscale);
        float rectHGame = baseRectHeightAtDesign / Mathf.Max(1f, designUpscale);

        float rectW = Mathf.Round(rectWGame * _lastBaseScaleInt);
        float rectH = Mathf.Round(rectHGame * _lastBaseScaleInt);

        var rt = stageText.rectTransform;

        if (Mathf.Abs(rt.sizeDelta.x - rectW) > 0.01f || Mathf.Abs(rt.sizeDelta.y - rectH) > 0.01f)
            rt.sizeDelta = new Vector2(rectW, rectH);

        SyncPauseWindow();
    }

    public void SetBattleModePauseMenu(int world, int stage, int selectedIndex)
    {
        EnsureNoWrap();

        string resume = selectedIndex == 0
            ? "<color=#FF6F31>> RESUME</color>"
            : "<color=#E8E8E8>  RESUME</color>";

        string restartRound = selectedIndex == 1
            ? "<color=#FF6F31>> RESTART ROUND</color>"
            : "<color=#E8E8E8>  RESTART ROUND</color>";

        string stageSelectText = $"RETURN{NBSP}TO{NBSP}STAGE{NBSP}SELECT";
        string retStageSelect = selectedIndex == 2
            ? $"<color=#FF6F31>> {stageSelectText}</color>"
            : $"<color=#E8E8E8>  {stageSelectText}</color>";

        string titleText = $"RETURN{NBSP}TO{NBSP}TITLE";
        string retTitle = selectedIndex == 3
            ? $"<color=#FF6F31>> {titleText}</color>"
            : $"<color=#E8E8E8>  {titleText}</color>";

        stageText.text =
            "<align=center>" +
            $"<size={S(SizePauseTitle)}><color=#3392FF>PAUSE!</color></size>\n\n" +
            $"<size={S(SizeMenuItem)}>{resume}</size>\n" +
            $"<size={S(SizeMenuItem)}>{restartRound}</size>\n" +
            $"<size={S(SizeMenuItem)}>{retStageSelect}</size>\n" +
            $"<size={S(SizeMenuItem)}>{retTitle}</size>" +
            "</align>";

        SetPauseWindowVisible(true);
    }

    public void SetPauseConfirmRestartRound(int world, int stage, int selectedIndex)
    {
        SetPauseConfirmQuestion(world, stage, selectedIndex, "Restart Round?", false);
    }

    public void SetPauseConfirmReturnToStageSelect(int world, int stage, int selectedIndex)
    {
        SetPauseConfirmQuestion(world, stage, selectedIndex, "Return to Stage Select?", false);
    }

    public void HidePauseWindow()
    {
        SetPauseWindowVisible(false);
    }

    void SetPauseWindowVisible(bool visible)
    {
        if (!showPauseWindow || stageText == null)
        {
            if (pauseWindowRect != null)
                pauseWindowRect.gameObject.SetActive(false);
            return;
        }

        EnsurePauseWindow();

        if (pauseWindowRect == null)
            return;

        pauseWindowRect.gameObject.SetActive(visible);
        if (pauseWindowLastVisibleState != visible)
        {
            pauseWindowLastVisibleState = visible;
            LogPauseWindow($"Visibility changed -> {visible}");
        }

        if (visible)
            SyncPauseWindow();
    }

    void EnsurePauseWindow()
    {
        if (pauseWindowRect != null || stageText == null)
            return;

        RectTransform textRect = stageText.rectTransform;
        RectTransform parent = textRect.parent as RectTransform;
        if (parent == null)
            return;

        GameObject go = new GameObject("PauseMenuWindow", typeof(RectTransform));
        go.layer = stageText.gameObject.layer;
        pauseWindowRect = go.GetComponent<RectTransform>();
        pauseWindowRect.SetParent(parent, false);

        pauseWindowImage = go.AddComponent<Image>();
        pauseWindowImage.color = new Color(0f, 0f, 0f, 0f);
        pauseWindowImage.raycastTarget = false;

        pauseWindowShadowEffect = go.AddComponent<Shadow>();
        pauseWindowShadowEffect.effectColor = pauseWindowShadow;
        pauseWindowShadowEffect.useGraphicAlpha = true;

        GameObject fillGo = new GameObject("PauseMenuWindowFill", typeof(RectTransform));
        fillGo.layer = stageText.gameObject.layer;
        pauseWindowFillRect = fillGo.GetComponent<RectTransform>();
        pauseWindowFillRect.SetParent(pauseWindowRect, false);
        pauseWindowFillRect.anchorMin = new Vector2(0.5f, 0.5f);
        pauseWindowFillRect.anchorMax = new Vector2(0.5f, 0.5f);
        pauseWindowFillRect.pivot = new Vector2(0.5f, 0.5f);

        pauseWindowFillImage = fillGo.AddComponent<Image>();
        pauseWindowFillImage.color = pauseWindowFill;
        pauseWindowFillImage.raycastTarget = false;

        CreatePauseWindowFrame();

        PlacePauseWindowBehindText();
        LogPauseWindow("Created runtime pause window");
        SyncPauseWindow();
    }

    void SyncPauseWindow()
    {
        if (pauseWindowRect == null || stageText == null)
            return;

        RectTransform textRect = stageText.rectTransform;
        Canvas.ForceUpdateCanvases();
        stageText.ForceMeshUpdate(true, true);
        pauseWindowRect.anchorMin = textRect.anchorMin;
        pauseWindowRect.anchorMax = textRect.anchorMax;
        pauseWindowRect.pivot = textRect.pivot;
        pauseWindowRect.anchoredPosition = textRect.anchoredPosition;

        float preferredWidth = stageText.preferredWidth;
        float preferredHeight = stageText.preferredHeight;
        float targetWidth = Mathf.Max(textRect.sizeDelta.x, preferredWidth);
        float targetHeight = Mathf.Max(textRect.sizeDelta.y, preferredHeight);
        Vector2 padding = pauseWindowPaddingAtDesign * UiScale;
        pauseWindowRect.sizeDelta = new Vector2(
            targetWidth + padding.x,
            targetHeight + padding.y);

        if (pauseWindowImage != null)
            pauseWindowImage.color = new Color(0f, 0f, 0f, 0f);

        if (pauseWindowShadowEffect != null)
        {
            pauseWindowShadowEffect.effectColor = pauseWindowShadow;
            pauseWindowShadowEffect.effectDistance = pauseWindowShadowDistanceAtDesign * UiScale;
        }

        SyncPauseWindowFrame();

        PlacePauseWindowBehindText();
        LogPauseWindow(
            $"Synced window. textSize={textRect.sizeDelta} preferred=({preferredWidth:F2},{preferredHeight:F2}) " +
            $"windowSize={pauseWindowRect.sizeDelta} pos={pauseWindowRect.anchoredPosition}");
    }

    void CreatePauseWindowFrame()
    {
        for (int i = 0; i < pauseWindowFrameRects.Length; i++)
        {
            GameObject segmentGo = new GameObject($"PauseMenuWindowFrame_{i}", typeof(RectTransform));
            segmentGo.layer = stageText.gameObject.layer;

            RectTransform rt = segmentGo.GetComponent<RectTransform>();
            rt.SetParent(pauseWindowRect, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            pauseWindowFrameRects[i] = rt;

            Image image = segmentGo.AddComponent<Image>();
            image.raycastTarget = false;
            pauseWindowFrameImages[i] = image;
        }
    }

    void SyncPauseWindowFrame()
    {
        if (pauseWindowRect == null)
            return;

        float scale = UiScale;
        float outer = Mathf.Max(1f, pauseWindowOuterBorderThicknessAtDesign * scale);
        float gold = Mathf.Max(1f, pauseWindowGoldBorderThicknessAtDesign * scale);
        float highlight = Mathf.Max(1f, pauseWindowHighlightThicknessAtDesign * scale);
        float inner = Mathf.Max(1f, pauseWindowInnerBorderThicknessAtDesign * scale);
        Vector2 outerSize = pauseWindowRect.sizeDelta;
        Vector2 goldSize = Shrink(outerSize, outer);
        Vector2 highlightSize = Shrink(goldSize, gold);
        Vector2 fillSize = Shrink(highlightSize, highlight + inner);

        SetFrameLayer(0, outerSize, outer, pauseWindowOuterBorder);
        SetFrameLayer(4, goldSize, gold, pauseWindowGoldBorder);
        SetFrameLayer(8, highlightSize, highlight, pauseWindowHighlightBorder);
        SetFrameLayer(12, Shrink(highlightSize, highlight), inner, pauseWindowInnerBorder);

        if (pauseWindowFillRect != null)
        {
            pauseWindowFillRect.anchoredPosition = Vector2.zero;
            pauseWindowFillRect.sizeDelta = fillSize;
            pauseWindowFillRect.SetAsLastSibling();
        }

        if (pauseWindowFillImage != null)
            pauseWindowFillImage.color = pauseWindowFill;
    }

    Vector2 Shrink(Vector2 size, float amount)
    {
        return new Vector2(
            Mathf.Max(1f, size.x - (amount * 2f)),
            Mathf.Max(1f, size.y - (amount * 2f)));
    }

    void SetFrameLayer(int index, Vector2 size, float thickness, Color color)
    {
        float halfWidth = size.x * 0.5f;
        float halfHeight = size.y * 0.5f;
        float inset = thickness * 0.5f;
        float verticalHeight = Mathf.Max(1f, size.y);

        SetFrameSegment(index, new Vector2(0f, halfHeight - inset), new Vector2(size.x, thickness), color);
        SetFrameSegment(index + 1, new Vector2(0f, -halfHeight + inset), new Vector2(size.x, thickness), color);
        SetFrameSegment(index + 2, new Vector2(-halfWidth + inset, 0f), new Vector2(thickness, verticalHeight), color);
        SetFrameSegment(index + 3, new Vector2(halfWidth - inset, 0f), new Vector2(thickness, verticalHeight), color);
    }

    void SetFrameSegment(int index, Vector2 anchoredPosition, Vector2 sizeDelta, Color color)
    {
        if (index < 0 || index >= pauseWindowFrameRects.Length)
            return;

        RectTransform rt = pauseWindowFrameRects[index];
        if (rt == null)
            return;

        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = sizeDelta;
        rt.SetAsLastSibling();

        if (pauseWindowFrameImages[index] != null)
            pauseWindowFrameImages[index].color = color;
    }

    void PlacePauseWindowBehindText()
    {
        if (pauseWindowRect == null || stageText == null)
            return;

        RectTransform textRect = stageText.rectTransform;
        int targetIndex = Mathf.Max(0, textRect.GetSiblingIndex() - 1);
        if (pauseWindowRect.GetSiblingIndex() != targetIndex)
            pauseWindowRect.SetSiblingIndex(targetIndex);
    }

    void LogPauseWindow(string message)
    {
        if (!logPauseWindowDiagnostics || pauseWindowRect == null || stageText == null)
            return;

        RectTransform textRect = stageText.rectTransform;
        float preferredWidth = stageText.preferredWidth;
        bool changed =
            lastLoggedWindowSize != pauseWindowRect.sizeDelta ||
            lastLoggedWindowPosition != pauseWindowRect.anchoredPosition ||
            !Mathf.Approximately(lastLoggedPreferredWidth, preferredWidth) ||
            lastLoggedTextSiblingIndex != textRect.GetSiblingIndex() ||
            lastLoggedWindowSiblingIndex != pauseWindowRect.GetSiblingIndex();

        if (!changed && !message.StartsWith("Visibility changed") && !message.StartsWith("Created runtime"))
            return;

        lastLoggedWindowSize = pauseWindowRect.sizeDelta;
        lastLoggedWindowPosition = pauseWindowRect.anchoredPosition;
        lastLoggedPreferredWidth = preferredWidth;
        lastLoggedTextSiblingIndex = textRect.GetSiblingIndex();
        lastLoggedWindowSiblingIndex = pauseWindowRect.GetSiblingIndex();

        Debug.Log(
            $"[StageLabel][PauseWindow] {message} | textSibling={textRect.GetSiblingIndex()} " +
            $"windowSibling={pauseWindowRect.GetSiblingIndex()} textPos={textRect.anchoredPosition} " +
            $"windowPos={pauseWindowRect.anchoredPosition} textRect={textRect.sizeDelta} " +
            $"preferred=({stageText.preferredWidth:F2},{stageText.preferredHeight:F2}) windowRect={pauseWindowRect.sizeDelta}",
            this);
    }
}

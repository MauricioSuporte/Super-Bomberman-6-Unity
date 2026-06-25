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
    [SerializeField] Vector2 pauseWindowPaddingAtDesign = new Vector2(44f, 40f);
    [SerializeField, Min(0f)] float pauseWindowMinVerticalPaddingAtDesign = 56f;
    [SerializeField, Min(1f)] float pauseWindowOuterBorderThicknessAtDesign = 5f;
    [SerializeField, Min(1f)] float pauseWindowGoldBorderThicknessAtDesign = 5f;
    [SerializeField, Min(1f)] float pauseWindowHighlightThicknessAtDesign = 3f;
    [SerializeField, Min(1f)] float pauseWindowInnerBorderThicknessAtDesign = 5f;
    [SerializeField] Vector2 pauseWindowShadowDistanceAtDesign = new Vector2(7f, -7f);
    RectTransform pauseWindowRect;
    RectTransform pauseWindowFillRect;
    Image pauseWindowImage;
    Image pauseWindowFillImage;
    Shadow pauseWindowShadowEffect;
    readonly RectTransform[] pauseWindowFrameRects = new RectTransform[16];
    readonly Image[] pauseWindowFrameImages = new Image[16];
    readonly Vector3[] textWorldCorners = new Vector3[4];
    float pauseWindowOptionsWidth = -1f;

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
        DestroyPauseWindow();
    }

    void OnDestroy()
    {
        DestroyPauseWindow();
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
        LocalizedTmpFontFallback.Apply(stageText);
    }

    public void SetStage(int world, int stage)
    {
        EnsureNoWrap();
        pauseWindowOptionsWidth = -1f;
        SetPauseWindowVisible(false);

        string stageNumber = $"{world}-{stage}";
        PauseMenuText text = GameTextDatabase.Pause;

        stageText.text =
            "<align=center>" +
            $"<size={S(SizeStageLabel)}><color=#1ABC00>{text.Stage}</color></size>  " +
            $"<size={S(SizeStageNumber)}><color=#E8E8E8>{stageNumber}</color></size>" +
            "</align>";
    }

    public void SetPauseMenu(int world, int stage, int selectedIndex, bool isBossRush)
    {
        EnsureNoWrap();

        string stageNumber = $"{world}-{stage}";
        PauseMenuText text = GameTextDatabase.Pause;

        string resume = FormatPauseOption(text.Resume, selectedIndex == 0);

        string secondOptionText = isBossRush
            ? NoWrap(text.ReturnToBossRush)
            : NoWrap(text.ReturnToWorldMap);

        string retSecondOption = FormatPauseOption(secondOptionText, selectedIndex == 1);

        string titleText = NoWrap(text.ReturnToTitle);
        string retTitle = FormatPauseOption(titleText, selectedIndex == 2);

        pauseWindowOptionsWidth = MeasureLargestPauseOptionWidth(resume, retSecondOption, retTitle);

        stageText.text =
            "<align=center>" +
            $"<size={S(SizeStageLabel)}><color=#1ABC00>{text.Stage}</color></size>  " +
            $"<size={S(SizeStageNumber)}><color=#E8E8E8>{stageNumber}</color></size>\n" +
            $"<size={S(SizePauseTitle)}><color=#3392FF>{text.Pause}</color></size>\n\n" +
            $"<size={S(SizeMenuItem)}>{resume}</size>\n" +
            $"<size={S(SizeMenuItem)}>{retSecondOption}</size>\n" +
            $"<size={S(SizeMenuItem)}>{retTitle}</size>" +
            "</align>";

        ApplyRectScale("SetPauseMenu");
        SetPauseWindowVisible(true);
    }

    public void SetPauseConfirmReturnToWorldMap(int world, int stage, int selectedIndex)
    {
        SetPauseConfirmQuestion(world, stage, selectedIndex, GameTextDatabase.Pause.ReturnToWorldMapQuestion, true);
    }

    public void SetPauseConfirmReturnToBossRush(int world, int stage, int selectedIndex)
    {
        SetPauseConfirmQuestion(world, stage, selectedIndex, GameTextDatabase.Pause.ReturnToBossRushQuestion, true);
    }

    public void SetPauseConfirmReturnToTitle(int world, int stage, int selectedIndex)
    {
        SetPauseConfirmQuestion(world, stage, selectedIndex, GameTextDatabase.Pause.ReturnToTitleQuestion, true);
    }

    public void SetBattleModePauseConfirmReturnToTitle(int world, int stage, int selectedIndex)
    {
        SetPauseConfirmQuestion(world, stage, selectedIndex, GameTextDatabase.Pause.ReturnToTitleQuestion, false);
    }

    void SetPauseConfirmQuestion(int world, int stage, int selectedIndex, string question, bool showStageHeader)
    {
        EnsureNoWrap();

        string stageNumber = $"{world}-{stage}";
        PauseMenuText text = GameTextDatabase.Pause;
        CommonMenuText common = GameTextDatabase.Common;
        string stageHeader = showStageHeader
            ? $"<size={S(SizeStageLabel)}><color=#1ABC00>{text.Stage}</color></size>  " +
              $"<size={S(SizeStageNumber)}><color=#E8E8E8>{stageNumber}</color></size>\n"
            : string.Empty;

        string ArrowVisible(string text) => $"<color=#FF6F31>></color>{text}";
        string ArrowHidden(string text) => $"<color=#00000000>></color>{text}";

        string noOpt = selectedIndex == 0
            ? ArrowVisible($"<color=#FF6F31>{common.No}</color>")
            : ArrowHidden($"<color=#E8E8E8>{common.No}</color>");

        string yesOpt = selectedIndex == 1
            ? ArrowVisible($"<color=#FF6F31>{common.Yes}</color>")
            : ArrowHidden($"<color=#E8E8E8>{common.Yes}</color>");

        int indent = Px(OptionsIndentPxBase);
        pauseWindowOptionsWidth = Mathf.Max(
            MeasureRichTextWidth(SizeConfirmTitle, $"<color=#E8E8E8>{question}</color>"),
            MeasureLargestPauseOptionWidth(noOpt, yesOpt));

        stageText.text =
            "<align=center>" +
            stageHeader +
            $"<size={S(SizePauseTitle)}><color=#3392FF>{text.Pause}</color></size>\n\n" +
            $"<size={S(SizeConfirmTitle)}><color=#E8E8E8>{question}</color></size>\n\n" +
            $"<size={S(SizeMenuItem)}><indent={indent}>{noOpt}</indent></size>\n" +
            $"<size={S(SizeMenuItem)}><indent={indent}>{yesOpt}</indent></size>" +
            "</align>";

        ApplyRectScale("SetPauseConfirmQuestion");
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
        PauseMenuText text = GameTextDatabase.Pause;

        string resume = FormatPauseOption(text.Resume, selectedIndex == 0);

        string restartRound = FormatPauseOption(text.RestartRound, selectedIndex == 1);

        string stageSelectText = NoWrap(text.ReturnToStageSelect);
        string retStageSelect = FormatPauseOption(stageSelectText, selectedIndex == 2);

        string titleText = NoWrap(text.ReturnToTitle);
        string retTitle = FormatPauseOption(titleText, selectedIndex == 3);

        pauseWindowOptionsWidth = MeasureLargestPauseOptionWidth(resume, restartRound, retStageSelect, retTitle);

        stageText.text =
            "<align=center>" +
            $"<size={S(SizePauseTitle)}><color=#3392FF>{text.Pause}</color></size>\n\n" +
            $"<size={S(SizeMenuItem)}>{resume}</size>\n" +
            $"<size={S(SizeMenuItem)}>{restartRound}</size>\n" +
            $"<size={S(SizeMenuItem)}>{retStageSelect}</size>\n" +
            $"<size={S(SizeMenuItem)}>{retTitle}</size>" +
            "</align>";

        ApplyRectScale("SetBattleModePauseMenu");
        SetPauseWindowVisible(true);
    }

    public void SetPauseConfirmRestartRound(int world, int stage, int selectedIndex)
    {
        SetPauseConfirmQuestion(world, stage, selectedIndex, GameTextDatabase.Pause.RestartRoundQuestion, false);
    }

    public void SetPauseConfirmReturnToStageSelect(int world, int stage, int selectedIndex)
    {
        SetPauseConfirmQuestion(world, stage, selectedIndex, GameTextDatabase.Pause.ReturnToStageSelectQuestion, false);
    }

    static string NoWrap(string value)
    {
        return string.IsNullOrEmpty(value) ? string.Empty : value.Replace(" ", NBSP);
    }

    static string FormatPauseOption(string text, bool selected)
    {
        string arrow = selected
            ? "<color=#FF6F31>></color>"
            : "<color=#00000000>></color>";
        string color = selected ? "#FF6F31" : "#E8E8E8";
        return $"{arrow}<color={color}>{text}</color>";
    }

    float MeasureLargestPauseOptionWidth(params string[] options)
    {
        if (stageText == null || options == null || options.Length == 0)
            return -1f;

        float maxWidth = -1f;
        for (int i = 0; i < options.Length; i++)
        {
            if (string.IsNullOrEmpty(options[i]))
                continue;

            maxWidth = Mathf.Max(maxWidth, MeasureRichTextWidth(SizeMenuItem, options[i]));
        }

        return maxWidth;
    }

    float MeasureRichTextWidth(int size, string text)
    {
        if (stageText == null || string.IsNullOrEmpty(text))
            return -1f;

        Vector2 preferred = stageText.GetPreferredValues($"<size={S(size)}>{text}</size>");
        return preferred.x;
    }

    public void HidePauseWindow()
    {
        if (pauseWindowRect == null)
            return;

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

        if (!visible && pauseWindowRect == null)
            return;

        EnsurePauseWindow();

        if (pauseWindowRect == null)
            return;

        pauseWindowRect.gameObject.SetActive(visible);

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
        SyncPauseWindow();
    }

    void SyncPauseWindow()
    {
        if (pauseWindowRect == null || stageText == null)
            return;

        RectTransform textRect = stageText.rectTransform;
        Canvas.ForceUpdateCanvases();
        stageText.ForceMeshUpdate(true, true);
        RectTransform parent = pauseWindowRect.parent as RectTransform;
        pauseWindowRect.anchorMin = new Vector2(0.5f, 0.5f);
        pauseWindowRect.anchorMax = new Vector2(0.5f, 0.5f);
        pauseWindowRect.pivot = new Vector2(0.5f, 0.5f);
        pauseWindowRect.anchoredPosition = GetTextCenterInParent(parent, textRect);

        float preferredWidth = stageText.preferredWidth;
        float preferredHeight = stageText.preferredHeight;
        float targetWidth = pauseWindowOptionsWidth > 1f
            ? pauseWindowOptionsWidth
            : (preferredWidth > 1f ? preferredWidth : textRect.sizeDelta.x);
        float targetHeight = preferredHeight > 1f ? preferredHeight : textRect.sizeDelta.y;
        Vector2 paddingAtDesign = pauseWindowPaddingAtDesign;
        paddingAtDesign.y = Mathf.Max(paddingAtDesign.y, pauseWindowMinVerticalPaddingAtDesign);
        Vector2 padding = paddingAtDesign * UiScale;
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
    }

    void DestroyPauseWindow()
    {
        if (pauseWindowRect == null)
            return;

        GameObject go = pauseWindowRect.gameObject;
        pauseWindowRect = null;
        pauseWindowFillRect = null;
        pauseWindowImage = null;
        pauseWindowFillImage = null;
        pauseWindowShadowEffect = null;

        for (int i = 0; i < pauseWindowFrameRects.Length; i++)
        {
            pauseWindowFrameRects[i] = null;
            pauseWindowFrameImages[i] = null;
        }

        if (Application.isPlaying)
            Destroy(go);
        else
            DestroyImmediate(go);
    }

    Vector2 GetTextCenterInParent(RectTransform parent, RectTransform textRect)
    {
        if (parent == null || textRect == null)
            return textRect != null ? textRect.anchoredPosition : Vector2.zero;

        textRect.GetWorldCorners(textWorldCorners);
        Vector3 worldCenter = (textWorldCorners[0] + textWorldCorners[2]) * 0.5f;
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(stageText.canvas != null ? stageText.canvas.worldCamera : null, worldCenter);

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPoint, stageText.canvas != null ? stageText.canvas.worldCamera : null, out Vector2 localPoint))
        {
            Vector2 centerAnchorReference = new Vector2(
                parent.rect.width * (0.5f - parent.pivot.x),
                parent.rect.height * (0.5f - parent.pivot.y));

            return localPoint - centerAnchorReference;
        }

        return textRect.anchoredPosition;
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
}

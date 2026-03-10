using TMPro;
using UnityEngine;

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

    float _lastUiScale = -999f;
    int _lastBaseScaleInt = -999;
    Rect _lastCamRect;

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
        _lastCamRect = default;
        ApplyRectScale("OnEnable");
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
    }

    public void SetPauseConfirmReturnToWorldMap(int world, int stage, int selectedIndex)
    {
        SetPauseConfirmQuestion(world, stage, selectedIndex, "Return to World Map?");
    }

    public void SetPauseConfirmReturnToBossRush(int world, int stage, int selectedIndex)
    {
        SetPauseConfirmQuestion(world, stage, selectedIndex, "Return to Boss Rush?");
    }

    public void SetPauseConfirmReturnToTitle(int world, int stage, int selectedIndex)
    {
        SetPauseConfirmQuestion(world, stage, selectedIndex, "Return to Title Screen?");
    }

    void SetPauseConfirmQuestion(int world, int stage, int selectedIndex, string question)
    {
        EnsureNoWrap();

        string stageNumber = $"{world}-{stage}";

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
            $"<size={S(SizeStageLabel)}><color=#1ABC00>STAGE</color></size>  " +
            $"<size={S(SizeStageNumber)}><color=#E8E8E8>{stageNumber}</color></size>\n" +
            $"<size={S(SizePauseTitle)}><color=#3392FF>PAUSE!</color></size>\n\n" +
            $"<size={S(SizeConfirmTitle)}><color=#E8E8E8>{question}</color></size>\n\n" +
            $"<size={S(SizeMenuItem)}><indent={indent}>{noOpt}</indent></size>\n" +
            $"<size={S(SizeMenuItem)}><indent={indent}>{yesOpt}</indent></size>" +
            "</align>";
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
    }
}
using TMPro;
using UnityEngine;

public class StageLabel : MonoBehaviour
{
    private const string LOG = "[StageLabel]";
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

    [Tooltip("If true, uses integer upscale steps like PixelPerfectCamera.")]
    [SerializeField] bool useIntegerUpscale = true;

    [Tooltip("The upscale you tuned your BASE SIZES for. Example: FullHD gameplay looks like ~4x, set 4.")]
    [SerializeField, Min(1)] int designUpscale = 4;

    [Tooltip("Optional extra multiplier after normalization.")]
    [SerializeField, Min(0.01f)] float extraScaleMultiplier = 1f;

    [SerializeField, Min(0.01f)] float minScale = 0.5f;
    [SerializeField, Min(0.01f)] float maxScale = 10f;

    [Header("RectTransform Scaling")]
    [Tooltip("Your current TMP RectTransform width at the DESIGN upscale (ex: 600 when designUpscale=4).")]
    [SerializeField, Min(1f)] float baseRectWidthAtDesign = 600f;

    [Tooltip("Your current TMP RectTransform height at the DESIGN upscale (ex: 200 when designUpscale=4).")]
    [SerializeField, Min(1f)] float baseRectHeightAtDesign = 200f;

    [SerializeField] bool autoResizeRectTransform = true;

    [Header("Debug")]
    [SerializeField] bool enableSurgicalLogs = true;
    [SerializeField, Min(0.05f)] float logInterval = 0.5f;

    float _nextLogTime;
    float _lastUiScale = -999f;
    int _lastBaseScaleInt = -999;
    Rect _lastCamRect;

    float UiScale
    {
        get
        {
            if (!dynamicScale)
            {
                if (stageText == null) return 1f;
                var c = stageText.canvas;
                if (c == null) return 1f;
                return Mathf.Max(0.01f, c.scaleFactor);
            }

            var cam = Camera.main;

            float usedW = (cam != null) ? cam.pixelRect.width : Screen.width;
            float usedH = (cam != null) ? cam.pixelRect.height : Screen.height;

            float sx = usedW / Mathf.Max(1f, referenceWidth);
            float sy = usedH / Mathf.Max(1f, referenceHeight);
            float baseScaleRaw = Mathf.Min(sx, sy);

            float baseScaleForUi = useIntegerUpscale ? Mathf.Round(baseScaleRaw) : baseScaleRaw;
            if (baseScaleForUi < 1f) baseScaleForUi = 1f;

            int baseScaleInt = Mathf.Max(1, Mathf.RoundToInt(baseScaleForUi));

            float normalized = baseScaleInt / Mathf.Max(1f, designUpscale);

            float ui = normalized * Mathf.Max(0.01f, extraScaleMultiplier);
            ui = Mathf.Clamp(ui, minScale, maxScale);

            MaybeLogScale(cam, usedW, usedH, sx, sy, baseScaleRaw, baseScaleInt, normalized, ui);

            _lastBaseScaleInt = baseScaleInt;
            _lastUiScale = ui;

            return ui;
        }
    }

    int S(int baseSize) => Mathf.Clamp(Mathf.RoundToInt(baseSize * UiScale), 10, 500);
    int Px(int basePx) => Mathf.RoundToInt(basePx * UiScale);

    void Awake()
    {
        MaybeLogStaticSetup("Awake");
        ApplyRectScale("Awake");
    }

    void OnEnable()
    {
        MaybeLogStaticSetup("OnEnable");
        _nextLogTime = 0f;
        _lastUiScale = -999f;
        _lastBaseScaleInt = -999;
        _lastCamRect = default;
        ApplyRectScale("OnEnable");
    }

    void Update()
    {
        if (stageText == null) return;

        ApplyRectScale("Update");

        if (!enableSurgicalLogs) return;
        if (Time.unscaledTime < _nextLogTime) return;

        _nextLogTime = Time.unscaledTime + logInterval;
        MaybeLogTextMetrics("Tick");
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

        MaybeLogTextMetrics("SetStage");
    }

    public void SetPauseMenu(int world, int stage, int selectedIndex)
    {
        EnsureNoWrap();

        string stageNumber = $"{world}-{stage}";

        string resume = (selectedIndex == 0)
            ? "<color=#FF6F31>> RESUME</color>"
            : "<color=#E8E8E8>  RESUME</color>";

        string returnToTitleText = $"RETURN{NBSP}TO{NBSP}TITLE";

        string ret = (selectedIndex == 1)
            ? $"<color=#FF6F31>> {returnToTitleText}</color>"
            : $"<color=#E8E8E8>  {returnToTitleText}</color>";

        stageText.text =
            "<align=center>" +
            $"<size={S(SizeStageLabel)}><color=#1ABC00>STAGE</color></size>  " +
            $"<size={S(SizeStageNumber)}><color=#E8E8E8>{stageNumber}</color></size>\n" +
            $"<size={S(SizePauseTitle)}><color=#3392FF>PAUSE!</color></size>\n\n" +
            $"<size={S(SizeMenuItem)}>{resume}</size>\n" +
            $"<size={S(SizeMenuItem)}>{ret}</size>" +
            "</align>";

        MaybeLogTextMetrics("SetPauseMenu");
    }

    public void SetPauseConfirmReturnToTitle(int world, int stage, int selectedIndex)
    {
        EnsureNoWrap();

        string stageNumber = $"{world}-{stage}";

        string ArrowVisible(string text) => $"<color=#FF6F31>> </color>{text}";
        string ArrowHidden(string text) => $"<color=#00000000>> </color>{text}";

        string noOpt = (selectedIndex == 0)
            ? ArrowVisible("<color=#FF6F31>NO</color>")
            : ArrowHidden("<color=#E8E8E8>NO</color>");

        string yesOpt = (selectedIndex == 1)
            ? ArrowVisible("<color=#FF6F31>YES</color>")
            : ArrowHidden("<color=#E8E8E8>YES</color>");

        int indent = Px(OptionsIndentPxBase);

        stageText.text =
            "<align=center>" +
            $"<size={S(SizeStageLabel)}><color=#1ABC00>STAGE</color></size>  " +
            $"<size={S(SizeStageNumber)}><color=#E8E8E8>{stageNumber}</color></size>\n" +
            $"<size={S(SizePauseTitle)}><color=#3392FF>PAUSE!</color></size>\n\n" +
            $"<size={S(SizeConfirmTitle)}><color=#E8E8E8>Return to Title Screen?</color></size>\n\n" +
            $"<size={S(SizeMenuItem)}><indent={indent}>{noOpt}</indent></size>\n" +
            $"<size={S(SizeMenuItem)}><indent={indent}>{yesOpt}</indent></size>" +
            "</align>";

        MaybeLogTextMetrics("SetPauseConfirm");
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
        {
            rt.sizeDelta = new Vector2(rectW, rectH);

            if (enableSurgicalLogs)
            {
                Debug.Log($"{LOG} [t={Time.unscaledTime:0.00}] Rect | {tag} | baseScaleInt={_lastBaseScaleInt} designUpscale={designUpscale} rectGame={rectWGame:0.##}x{rectHGame:0.##} => rectPx={rectW:0}x{rectH:0} | camRect=({r.x:0},{r.y:0},{r.width:0},{r.height:0})", this);
            }
        }
    }

    void MaybeLogStaticSetup(string tag)
    {
        if (!enableSurgicalLogs) return;

        var cam = Camera.main;
        Rect r = cam != null ? cam.pixelRect : new Rect(0, 0, Screen.width, Screen.height);

        string canvasInfo = "Canvas=NULL";
        if (stageText != null && stageText.canvas != null)
        {
            var c = stageText.canvas;
            var scaler = c.GetComponent<UnityEngine.UI.CanvasScaler>();
            string scalerMode = scaler != null ? scaler.uiScaleMode.ToString() : "NO_SCALER";
            canvasInfo = $"Canvas={c.name} scaleFactor={c.scaleFactor} scalerMode={scalerMode} pixelPerfect={c.pixelPerfect}";
        }

        Debug.Log($"{LOG} [t={Time.unscaledTime:0.00}] {tag} | Screen={Screen.width}x{Screen.height} | Cam={(cam ? cam.name : "NULL")} rect=({r.x:0},{r.y:0},{r.width:0},{r.height:0}) | {canvasInfo} | dyn={dynamicScale} int={useIntegerUpscale} designUpscale={designUpscale} extraMult={extraScaleMultiplier:0.###} clamp[{minScale:0.###},{maxScale:0.###}]",
            this);
    }

    void MaybeLogScale(Camera cam, float usedW, float usedH, float sx, float sy, float baseScaleRaw, int baseScaleInt, float normalized, float ui)
    {
        if (!enableSurgicalLogs) return;

        Rect r = cam != null ? cam.pixelRect : new Rect(0, 0, Screen.width, Screen.height);
        bool camChanged = r != _lastCamRect;
        bool scaleChanged = Mathf.Abs(ui - _lastUiScale) > 0.0001f;

        if (!camChanged && !scaleChanged) return;

        _lastCamRect = r;

        Debug.Log(
            $"{LOG} [t={Time.unscaledTime:0.00}] Scale | Screen={Screen.width}x{Screen.height} | camRect=({r.x:0},{r.y:0},{r.width:0},{r.height:0}) usedWH={usedW:0}x{usedH:0} | ref={referenceWidth}x{referenceHeight} | sx={sx:0.###} sy={sy:0.###} baseRaw={baseScaleRaw:0.###} baseInt={baseScaleInt} | designUpscale={designUpscale} normalized={normalized:0.###} extraMult={extraScaleMultiplier:0.###} => UiScale={ui:0.###}",
            this
        );
    }

    void MaybeLogTextMetrics(string tag)
    {
        if (!enableSurgicalLogs || stageText == null) return;

        var rt = stageText.rectTransform;
        var pref = stageText.GetPreferredValues(stageText.text);

        Debug.Log(
            $"{LOG} [t={Time.unscaledTime:0.00}] Text | {tag} | baseInt={_lastBaseScaleInt} UiScale={_lastUiScale:0.###} fontSize={stageText.fontSize:0.##} | rect={rt.rect.width:0}x{rt.rect.height:0} pref={pref.x:0}x{pref.y:0} | wrapping={stageText.textWrappingMode} overflow={stageText.overflowMode} autoSize={stageText.enableAutoSizing}",
            this
        );
    }
}
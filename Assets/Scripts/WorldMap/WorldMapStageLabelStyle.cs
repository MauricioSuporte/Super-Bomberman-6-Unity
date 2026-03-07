using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class WorldMapStageLabelStyle : MonoBehaviour
{
    [Header("Text References")]
    [SerializeField] Text mainText;
    [SerializeField] Text shadowText;
    [SerializeField] Text highlightText;

    [Header("Safe Frame Reference")]
    [SerializeField] RectTransform safeFrame;

    [Header("SNES Reference")]
    [SerializeField] int referenceWidth = 256;
    [SerializeField] int referenceHeight = 224;
    [SerializeField] bool useIntegerScale = true;
    [SerializeField] float extraScaleMultiplier = 1f;
    [SerializeField] float minScale = 1f;
    [SerializeField] float maxScale = 20f;

    [Header("Base Layout")]
    [SerializeField] Vector2 baseAnchoredPosition = new Vector2(3f, -3f);
    [SerializeField] Vector2 baseSize = new Vector2(80f, 12f);
    [SerializeField] int baseFontSize = 8;

    [Header("Layer Pixel Offsets")]
    [SerializeField] Vector2 shadowOffset = new Vector2(1f, -1f);
    [SerializeField] Vector2 highlightOffset = new Vector2(-1f, 1f);

    [Header("Colors")]
    [SerializeField] Color mainColor = new Color32(230, 150, 150, 255);
    [SerializeField] Color shadowColor = new Color32(120, 40, 40, 255);
    [SerializeField] Color highlightColor = new Color32(255, 220, 220, 255);

    [Header("Optional Source")]
    [SerializeField] Text sourceText;

    void LateUpdate()
    {
        ApplyStyle();
    }

    public void ApplyStyle()
    {
        if (mainText == null || shadowText == null || highlightText == null || safeFrame == null)
            return;

        float scale = GetScale();

        int fontSize = Mathf.RoundToInt(baseFontSize * scale);
        Vector2 size = baseSize * scale;
        Vector2 pos = baseAnchoredPosition * scale;

        ApplyToText(mainText, pos, size, fontSize, mainColor);
        ApplyToText(shadowText, pos + shadowOffset * scale, size, fontSize, shadowColor);
        ApplyToText(highlightText, pos + highlightOffset * scale, size, fontSize, highlightColor);

        string value = sourceText != null ? sourceText.text : mainText.text;

        mainText.text = value;
        shadowText.text = value;
        highlightText.text = value;

        shadowText.font = mainText.font;
        highlightText.font = mainText.font;

        shadowText.fontStyle = mainText.fontStyle;
        highlightText.fontStyle = mainText.fontStyle;

        shadowText.alignment = mainText.alignment;
        highlightText.alignment = mainText.alignment;

        shadowText.horizontalOverflow = mainText.horizontalOverflow;
        highlightText.horizontalOverflow = mainText.horizontalOverflow;

        shadowText.verticalOverflow = mainText.verticalOverflow;
        highlightText.verticalOverflow = mainText.verticalOverflow;
    }

    void ApplyToText(Text txt, Vector2 anchoredPos, Vector2 size, int fontSize, Color color)
    {
        if (txt == null)
            return;

        RectTransform rt = txt.rectTransform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        rt.localScale = Vector3.one;

        txt.fontSize = fontSize;
        txt.color = color;
        txt.supportRichText = false;
        txt.resizeTextForBestFit = false;
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;
        txt.verticalOverflow = VerticalWrapMode.Overflow;
        txt.alignment = TextAnchor.UpperLeft;
    }

    float GetScale()
    {
        Canvas canvas = safeFrame.GetComponentInParent<Canvas>();
        if (canvas == null)
            return 1f;

        Rect safePx = RectTransformUtility.PixelAdjustRect(safeFrame, canvas);

        float sx = safePx.width / Mathf.Max(1f, referenceWidth);
        float sy = safePx.height / Mathf.Max(1f, referenceHeight);

        float rawScale = Mathf.Min(sx, sy);
        float usedScale = useIntegerScale ? Mathf.Floor(rawScale) : rawScale;

        if (usedScale < 1f)
            usedScale = 1f;

        usedScale *= Mathf.Max(0.01f, extraScaleMultiplier);
        usedScale = Mathf.Clamp(usedScale, minScale, maxScale);

        return usedScale;
    }
}
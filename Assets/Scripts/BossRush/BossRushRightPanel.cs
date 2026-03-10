using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BossRushRightPanel : MonoBehaviour
{
    const string LOG = "[BossRushRightPanel]";

    [Header("Debug (Surgical Logs)")]
    [SerializeField] bool enableSurgicalLogs = true;

    [Header("Title")]
    [SerializeField] TextMeshProUGUI titleText;
    [SerializeField] string titleFormat = "{0}";
    [SerializeField] int titleFontSize = 18;
    [SerializeField] float titleHeight = 28f;
    [SerializeField] float titleOffsetX = 0f;

    [Header("Title TMP")]
    [SerializeField] TMP_FontAsset titleFontAsset;
    [SerializeField] Material titleFontMaterialPreset;
    [SerializeField] bool forceTitleBold = true;

    [Header("Title Selected Difficulty Colors")]
    [SerializeField] Color easySelectedColor = new Color32(56, 201, 54, 255);
    [SerializeField] Color normalSelectedColor = new Color32(45, 117, 255, 255);
    [SerializeField] Color hardSelectedColor = new Color32(231, 63, 63, 255);
    [SerializeField] Color nightmareSelectedColor = Color.black;

    [Header("Title Outline Colors")]
    [SerializeField] Color defaultTitleOutlineColor = Color.black;
    [SerializeField] Color nightmareTitleOutlineColor = new Color32(231, 63, 63, 255);

    [Header("Title TMP Outline")]
    [SerializeField] bool useTitleOutline = true;
    [SerializeField, Range(0f, 1f)] float titleOutlineWidth = 0.35f;
    [SerializeField, Range(0f, 1f)] float titleOutlineSoftness = 0f;

    [Header("Title TMP Face")]
    [SerializeField, Range(-1f, 1f)] float titleFaceDilate = 0.2f;
    [SerializeField, Range(0f, 1f)] float titleFaceSoftness = 0f;

    [Header("Title TMP Underlay")]
    [SerializeField] bool enableTitleUnderlay = true;
    [SerializeField] Color titleUnderlayColor = new Color(0f, 0f, 0f, 1f);
    [SerializeField, Range(-1f, 1f)] float titleUnderlayDilate = 0.1f;
    [SerializeField, Range(0f, 1f)] float titleUnderlaySoftness = 0f;
    [SerializeField, Range(-2f, 2f)] float titleUnderlayOffsetX = 0.25f;
    [SerializeField, Range(-2f, 2f)] float titleUnderlayOffsetY = -0.25f;

    [Header("Rows")]
    [SerializeField] RectTransform firstRow;
    [SerializeField] RectTransform secondRow;
    [SerializeField] RectTransform thirdRow;

    [SerializeField] Text firstRankText;
    [SerializeField] Text secondRankText;
    [SerializeField] Text thirdRankText;

    [SerializeField] TextMeshProUGUI firstTimeText;
    [SerializeField] TextMeshProUGUI secondTimeText;
    [SerializeField] TextMeshProUGUI thirdTimeText;

    [SerializeField] int rowFontSize = 16;
    [SerializeField] float rowHeight = 24f;

    [Header("Row Widths")]
    [SerializeField] float rankPreferredWidth = 120f;
    [SerializeField] float timePreferredWidth = 220f;

    [Header("Labels")]
    [SerializeField] string firstLabel = "1";
    [SerializeField] string secondLabel = "2";
    [SerializeField] string thirdLabel = "3";
    [SerializeField] string defaultTimeText = "--:--.--";

    [Header("Rank Visuals")]
    [SerializeField] Image firstRankBackground;
    [SerializeField] Image secondRankBackground;
    [SerializeField] Image thirdRankBackground;

    [SerializeField] float rankBadgeSize = 56f;
    [SerializeField] float rankRotationZ = -8f;
    [SerializeField] Color rankTextColor = Color.black;
    [SerializeField] Color rankShadowColor = Color.white;
    [SerializeField] Vector2 rankShadowDistance = new Vector2(3f, -3f);

    [SerializeField] Color firstRankBackgroundColor = new Color32(214, 183, 24, 255);
    [SerializeField] Color secondRankBackgroundColor = new Color32(201, 201, 201, 255);
    [SerializeField] Color thirdRankBackgroundColor = new Color32(224, 151, 98, 255);

    [Header("Time TMP Font")]
    [SerializeField] TMP_FontAsset timeFontAsset;
    [SerializeField] Material timeFontMaterialPreset;
    [SerializeField] bool forceTimeBold = true;

    [Header("Time TMP Colors")]
    [SerializeField] Color timeTextColor = Color.white;
    [SerializeField] Color timeOutlineColor = Color.black;

    [Header("Time TMP Outline")]
    [SerializeField, Range(0f, 1f)] float timeOutlineWidth = 0.35f;
    [SerializeField, Range(0f, 1f)] float timeOutlineSoftness = 0f;

    [Header("Time TMP Face")]
    [SerializeField, Range(-1f, 1f)] float timeFaceDilate = 0.25f;
    [SerializeField, Range(0f, 1f)] float timeFaceSoftness = 0f;

    [Header("Time TMP Underlay")]
    [SerializeField] bool enableTimeUnderlay = true;
    [SerializeField] Color timeUnderlayColor = new Color(0f, 0f, 0f, 1f);
    [SerializeField, Range(-1f, 1f)] float timeUnderlayDilate = 0.15f;
    [SerializeField, Range(0f, 1f)] float timeUnderlaySoftness = 0f;
    [SerializeField, Range(-2f, 2f)] float timeUnderlayOffsetX = 0.25f;
    [SerializeField, Range(-2f, 2f)] float timeUnderlayOffsetY = -0.25f;

    [Header("Generated Badge")]
    [SerializeField] int generatedCircleTextureSize = 64;
    [SerializeField] float generatedCircleSoftEdge = 1.5f;
    [SerializeField] bool autoCreateRankBackground = true;

    [Header("Panel Offset")]
    [SerializeField] float contentOffsetY = 0f;

    struct DifficultyVisualStyle
    {
        public Color FaceColor;
        public Color OutlineColor;

        public DifficultyVisualStyle(Color faceColor, Color outlineColor)
        {
            FaceColor = faceColor;
            OutlineColor = outlineColor;
        }
    }

    static Sprite s_circleSprite;

    readonly Dictionary<TMP_Text, Material> runtimeTimeMaterials = new Dictionary<TMP_Text, Material>();
    readonly Dictionary<TMP_Text, Material> runtimeTitleMaterials = new Dictionary<TMP_Text, Material>();

    int _baseTopPadding;
    int _baseBottomPadding;
    int _baseLeftPadding;
    int _baseRightPadding;
    bool _layoutPaddingCaptured;

    float _currentUiScale = 1f;

    int ScaledFont(int baseSize) => Mathf.Clamp(Mathf.RoundToInt(baseSize * _currentUiScale), 8, 300);
    float ScaledFloat(float baseValue) => baseValue * _currentUiScale;

    public void Initialize(float uiScale)
    {
        _currentUiScale = uiScale;
        ResolveMissingReferences();
        CaptureBaseLayoutPadding();
        ApplyScaledFonts();
        ApplyLayoutFormatting();
        SetDifficulty(BossRushDifficulty.NORMAL);
    }

    public void SetUiScale(float uiScale)
    {
        _currentUiScale = uiScale;
        ResolveMissingReferences();
        ApplyScaledFonts();
        ApplyLayoutFormatting();
    }

    public void SetDifficulty(BossRushDifficulty difficulty)
    {
        ResolveMissingReferences();

        ApplyScaledFonts();
        ApplyLayoutFormatting();

        if (titleText != null)
        {
            titleText.text = string.Format(titleFormat, GetDifficultyDisplayName(difficulty));
            ApplyTitleStyleForDifficulty(difficulty);
        }

        if (firstRankText != null) firstRankText.text = firstLabel;
        if (secondRankText != null) secondRankText.text = secondLabel;
        if (thirdRankText != null) thirdRankText.text = thirdLabel;

        List<float> times = BossRushProgress.GetTopTimes(difficulty);

        SetTimeText(firstTimeText, GetFormattedTime(times, 0));
        SetTimeText(secondTimeText, GetFormattedTime(times, 1));
        SetTimeText(thirdTimeText, GetFormattedTime(times, 2));

        RefreshRankBackgroundPositions();

        SLog(
            $"SetDifficulty | difficulty={difficulty} " +
            $"t0={GetFormattedTime(times, 0)} t1={GetFormattedTime(times, 1)} t2={GetFormattedTime(times, 2)}"
        );
    }

    void OnDestroy()
    {
        foreach (var kv in runtimeTimeMaterials)
        {
            if (kv.Value != null)
                Destroy(kv.Value);
        }

        foreach (var kv in runtimeTitleMaterials)
        {
            if (kv.Value != null)
                Destroy(kv.Value);
        }

        runtimeTimeMaterials.Clear();
        runtimeTitleMaterials.Clear();
    }

    void ResolveMissingReferences()
    {
        if (firstRow == null && firstRankText != null)
            firstRow = firstRankText.transform.parent as RectTransform;

        if (secondRow == null && secondRankText != null)
            secondRow = secondRankText.transform.parent as RectTransform;

        if (thirdRow == null && thirdRankText != null)
            thirdRow = thirdRankText.transform.parent as RectTransform;

        if (firstRankBackground == null && firstRankText != null)
            firstRankBackground = FindGeneratedBackground(firstRankText);

        if (secondRankBackground == null && secondRankText != null)
            secondRankBackground = FindGeneratedBackground(secondRankText);

        if (thirdRankBackground == null && thirdRankText != null)
            thirdRankBackground = FindGeneratedBackground(thirdRankText);
    }

    void ApplyScaledFonts()
    {
        if (titleText != null)
            titleText.fontSize = ScaledFont(titleFontSize);

        if (firstRankText != null)
            firstRankText.fontSize = ScaledFont(rowFontSize);

        if (secondRankText != null)
            secondRankText.fontSize = ScaledFont(rowFontSize);

        if (thirdRankText != null)
            thirdRankText.fontSize = ScaledFont(rowFontSize);

        ApplyTimeFontSizing(firstTimeText);
        ApplyTimeFontSizing(secondTimeText);
        ApplyTimeFontSizing(thirdTimeText);
    }

    void ApplyTimeFontSizing(TextMeshProUGUI target)
    {
        if (target == null)
            return;

        float maxSize = ScaledFont(rowFontSize);
        float minSize = Mathf.Max(8f, Mathf.Floor(maxSize * 0.60f));

        target.enableAutoSizing = true;
        target.fontSizeMax = maxSize;
        target.fontSizeMin = minSize;
        target.fontSize = maxSize;
    }

    void ApplyLayoutFormatting()
    {
        ResolveMissingReferences();
        ApplyPanelVerticalOffset();

        ConfigureTitleText(titleText);

        ConfigureRankText(firstRankText);
        ConfigureRankText(secondRankText);
        ConfigureRankText(thirdRankText);

        ConfigureTimeText(firstTimeText);
        ConfigureTimeText(secondTimeText);
        ConfigureTimeText(thirdTimeText);

        ConfigureRow(firstRow);
        ConfigureRow(secondRow);
        ConfigureRow(thirdRow);

        ConfigureLayoutElementWidth(firstTimeText, timePreferredWidth);
        ConfigureLayoutElementWidth(secondTimeText, timePreferredWidth);
        ConfigureLayoutElementWidth(thirdTimeText, timePreferredWidth);

        ConfigureRankSlot(firstRankText, ref firstRankBackground, firstRankBackgroundColor);
        ConfigureRankSlot(secondRankText, ref secondRankBackground, secondRankBackgroundColor);
        ConfigureRankSlot(thirdRankText, ref thirdRankBackground, thirdRankBackgroundColor);

        ForceRebuildLayouts();
        RefreshRankBackgroundPositions();

        ConfigureTitleHeight();
        ConfigureTitleWidthAndPosition();
    }

    void ForceRebuildLayouts()
    {
        Canvas.ForceUpdateCanvases();

        if (firstRow != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(firstRow);

        if (secondRow != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(secondRow);

        if (thirdRow != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(thirdRow);

        RectTransform root = transform as RectTransform;
        if (root != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(root);

        Canvas.ForceUpdateCanvases();
    }

    void ConfigureTitleHeight()
    {
        if (titleText == null)
            return;

        RectTransform rt = titleText.rectTransform;
        if (rt == null)
            return;

        float h = Mathf.Max(ScaledFloat(titleHeight), titleText.fontSize + ScaledFloat(12f));
        rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, h);

        LayoutElement le = titleText.GetComponent<LayoutElement>();
        if (le == null)
            le = titleText.gameObject.AddComponent<LayoutElement>();

        le.minHeight = h;
        le.preferredHeight = h;
        le.flexibleHeight = 0f;
    }

    void ConfigureTitleWidthAndPosition()
    {
        if (titleText == null)
            return;

        RectTransform rt = titleText.rectTransform;
        if (rt == null)
            return;

        float rowSpacing = GetRowSpacing();
        float contentWidth =
            ScaledFloat(rankBadgeSize) +
            rowSpacing +
            ScaledFloat(timePreferredWidth);

        rt.anchorMin = new Vector2(0.5f, rt.anchorMin.y);
        rt.anchorMax = new Vector2(0.5f, rt.anchorMax.y);
        rt.pivot = new Vector2(0.5f, rt.pivot.y);
        rt.anchoredPosition = new Vector2(ScaledFloat(titleOffsetX), rt.anchoredPosition.y);
        rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, contentWidth);

        LayoutElement le = titleText.GetComponent<LayoutElement>();
        if (le == null)
            le = titleText.gameObject.AddComponent<LayoutElement>();

        le.minWidth = contentWidth;
        le.preferredWidth = contentWidth;
        le.flexibleWidth = 0f;
    }

    float GetRowSpacing()
    {
        RectTransform row = firstRow != null ? firstRow : secondRow != null ? secondRow : thirdRow;
        if (row == null)
            return 0f;

        HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
        if (layout == null)
            return 0f;

        return layout.spacing;
    }

    void ConfigureRow(RectTransform row)
    {
        if (row == null)
            return;

        float badgeSize = ScaledFloat(rankBadgeSize);
        float h = Mathf.Max(ScaledFloat(rowHeight), ScaledFont(rowFontSize) + ScaledFloat(12f), badgeSize);

        row.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, h);

        LayoutElement le = row.GetComponent<LayoutElement>();
        if (le == null)
            le = row.gameObject.AddComponent<LayoutElement>();

        le.preferredHeight = h;
        le.minHeight = h;
        le.flexibleHeight = 0f;

        HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
        if (layout != null)
        {
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.MiddleLeft;
        }
    }

    void ConfigureLayoutElementWidth(Component target, float preferredWidth)
    {
        if (target == null)
            return;

        float w = ScaledFloat(preferredWidth);

        LayoutElement le = target.GetComponent<LayoutElement>();
        if (le == null)
            le = target.gameObject.AddComponent<LayoutElement>();

        le.minWidth = w;
        le.preferredWidth = w;
        le.flexibleWidth = 0f;
    }

    void ConfigureTitleText(TextMeshProUGUI target)
    {
        if (target == null)
            return;

        if (titleFontAsset != null)
            target.font = titleFontAsset;

        target.alignment = TextAlignmentOptions.Center;
        target.textWrappingMode = TextWrappingModes.NoWrap;
        target.overflowMode = TextOverflowModes.Overflow;
        target.enableAutoSizing = false;
        target.extraPadding = true;
        target.margin = Vector4.zero;

        if (forceTitleBold)
            target.fontStyle |= FontStyles.Bold;
        else
            target.fontStyle &= ~FontStyles.Bold;
    }

    void ApplyTitleStyleForDifficulty(BossRushDifficulty difficulty)
    {
        if (titleText == null)
            return;

        DifficultyVisualStyle style = GetTitleSelectedStyle(difficulty);

        titleText.color = style.FaceColor;

        Material runtimeMat = GetOrCreateRuntimeTitleMaterial(titleText);
        ApplyTitleMaterialStyle(runtimeMat, style);

        titleText.fontMaterial = runtimeMat;
        titleText.UpdateMeshPadding();
        titleText.ForceMeshUpdate();
        titleText.SetVerticesDirty();
    }

    DifficultyVisualStyle GetTitleSelectedStyle(BossRushDifficulty difficulty)
    {
        switch (difficulty)
        {
            case BossRushDifficulty.EASY:
                return new DifficultyVisualStyle(easySelectedColor, defaultTitleOutlineColor);

            case BossRushDifficulty.NORMAL:
                return new DifficultyVisualStyle(normalSelectedColor, defaultTitleOutlineColor);

            case BossRushDifficulty.HARD:
                return new DifficultyVisualStyle(hardSelectedColor, defaultTitleOutlineColor);

            case BossRushDifficulty.NIGHTMARE:
                return new DifficultyVisualStyle(nightmareSelectedColor, nightmareTitleOutlineColor);

            default:
                return new DifficultyVisualStyle(Color.white, defaultTitleOutlineColor);
        }
    }

    Material GetOrCreateRuntimeTitleMaterial(TMP_Text target)
    {
        if (target == null)
            return null;

        if (runtimeTitleMaterials.TryGetValue(target, out Material runtimeMat) && runtimeMat != null)
            return runtimeMat;

        Material baseMat = null;

        if (titleFontMaterialPreset != null)
            baseMat = titleFontMaterialPreset;
        else if (target.fontSharedMaterial != null)
            baseMat = target.fontSharedMaterial;
        else if (target.font != null)
            baseMat = target.font.material;

        if (baseMat == null)
            return null;

        runtimeMat = new Material(baseMat);
        runtimeMat.name = baseMat.name + "_BossRushTitleRuntime";
        runtimeTitleMaterials[target] = runtimeMat;
        return runtimeMat;
    }

    void ApplyTitleMaterialStyle(Material mat, DifficultyVisualStyle style)
    {
        if (mat == null)
            return;

        TrySetColor(mat, "_FaceColor", style.FaceColor);

        if (useTitleOutline)
        {
            TrySetColor(mat, "_OutlineColor", style.OutlineColor);
            TrySetFloat(mat, "_OutlineWidth", titleOutlineWidth);
            TrySetFloat(mat, "_OutlineSoftness", titleOutlineSoftness);
        }
        else
        {
            TrySetFloat(mat, "_OutlineWidth", 0f);
            TrySetFloat(mat, "_OutlineSoftness", 0f);
        }

        TrySetFloat(mat, "_FaceDilate", titleFaceDilate);
        TrySetFloat(mat, "_FaceSoftness", titleFaceSoftness);

        if (enableTitleUnderlay)
        {
            TrySetColor(mat, "_UnderlayColor", titleUnderlayColor);
            TrySetFloat(mat, "_UnderlayDilate", titleUnderlayDilate);
            TrySetFloat(mat, "_UnderlaySoftness", titleUnderlaySoftness);
            TrySetFloat(mat, "_UnderlayOffsetX", titleUnderlayOffsetX);
            TrySetFloat(mat, "_UnderlayOffsetY", titleUnderlayOffsetY);
        }
        else
        {
            TrySetFloat(mat, "_UnderlayDilate", 0f);
            TrySetFloat(mat, "_UnderlaySoftness", 0f);
            TrySetFloat(mat, "_UnderlayOffsetX", 0f);
            TrySetFloat(mat, "_UnderlayOffsetY", 0f);
        }
    }

    void ConfigureRankText(Text target)
    {
        if (target == null)
            return;

        target.alignment = TextAnchor.MiddleCenter;
        target.horizontalOverflow = HorizontalWrapMode.Overflow;
        target.verticalOverflow = VerticalWrapMode.Overflow;
        target.resizeTextForBestFit = false;
        target.supportRichText = false;
        target.color = rankTextColor;

        RectTransform rt = target.rectTransform;
        if (rt != null)
            rt.localRotation = Quaternion.Euler(0f, 0f, rankRotationZ);

        Shadow shadow = target.GetComponent<Shadow>();
        if (shadow == null)
            shadow = target.gameObject.AddComponent<Shadow>();

        shadow.effectColor = rankShadowColor;
        shadow.effectDistance = ScaledVector(rankShadowDistance);
        shadow.useGraphicAlpha = false;
    }

    void ConfigureTimeText(TextMeshProUGUI target)
    {
        if (target == null)
            return;

        if (timeFontAsset != null)
            target.font = timeFontAsset;

        target.alignment = TextAlignmentOptions.MidlineRight;
        target.textWrappingMode = TextWrappingModes.NoWrap;
        target.overflowMode = TextOverflowModes.Truncate;
        target.enableAutoSizing = true;
        target.extraPadding = true;
        target.color = timeTextColor;
        target.margin = Vector4.zero;

        float maxSize = ScaledFont(rowFontSize);
        float minSize = Mathf.Max(8f, Mathf.Floor(maxSize * 0.60f));
        target.fontSizeMax = maxSize;
        target.fontSizeMin = minSize;
        target.fontSize = maxSize;

        if (forceTimeBold)
            target.fontStyle |= FontStyles.Bold;
        else
            target.fontStyle &= ~FontStyles.Bold;

        Material runtimeMat = GetOrCreateRuntimeTimeMaterial(target);
        ApplyTimeMaterialStyle(runtimeMat);

        target.fontMaterial = runtimeMat;
        target.UpdateMeshPadding();
        target.ForceMeshUpdate();
        target.SetVerticesDirty();
    }

    Material GetOrCreateRuntimeTimeMaterial(TMP_Text target)
    {
        if (target == null)
            return null;

        if (runtimeTimeMaterials.TryGetValue(target, out Material runtimeMat) && runtimeMat != null)
            return runtimeMat;

        Material baseMat = null;

        if (timeFontMaterialPreset != null)
            baseMat = timeFontMaterialPreset;
        else if (target.fontSharedMaterial != null)
            baseMat = target.fontSharedMaterial;
        else if (target.font != null)
            baseMat = target.font.material;

        if (baseMat == null)
            return null;

        runtimeMat = new Material(baseMat);
        runtimeMat.name = baseMat.name + "_BossRushRuntime";
        runtimeTimeMaterials[target] = runtimeMat;
        return runtimeMat;
    }

    void ApplyTimeMaterialStyle(Material mat)
    {
        if (mat == null)
            return;

        TrySetColor(mat, "_FaceColor", timeTextColor);

        TrySetColor(mat, "_OutlineColor", timeOutlineColor);
        TrySetFloat(mat, "_OutlineWidth", timeOutlineWidth);
        TrySetFloat(mat, "_OutlineSoftness", timeOutlineSoftness);

        TrySetFloat(mat, "_FaceDilate", timeFaceDilate);
        TrySetFloat(mat, "_FaceSoftness", timeFaceSoftness);

        if (enableTimeUnderlay)
        {
            TrySetColor(mat, "_UnderlayColor", timeUnderlayColor);
            TrySetFloat(mat, "_UnderlayDilate", timeUnderlayDilate);
            TrySetFloat(mat, "_UnderlaySoftness", timeUnderlaySoftness);
            TrySetFloat(mat, "_UnderlayOffsetX", timeUnderlayOffsetX);
            TrySetFloat(mat, "_UnderlayOffsetY", timeUnderlayOffsetY);
        }
        else
        {
            TrySetFloat(mat, "_UnderlayDilate", 0f);
            TrySetFloat(mat, "_UnderlaySoftness", 0f);
            TrySetFloat(mat, "_UnderlayOffsetX", 0f);
            TrySetFloat(mat, "_UnderlayOffsetY", 0f);
        }
    }

    void ConfigureRankSlot(Text rankText, ref Image background, Color backgroundColor)
    {
        if (rankText == null)
            return;

        float badgeSize = ScaledFloat(rankBadgeSize);

        LayoutElement textLayout = rankText.GetComponent<LayoutElement>();
        if (textLayout == null)
            textLayout = rankText.gameObject.AddComponent<LayoutElement>();

        textLayout.preferredWidth = badgeSize;
        textLayout.minWidth = badgeSize;
        textLayout.preferredHeight = badgeSize;
        textLayout.minHeight = badgeSize;
        textLayout.flexibleWidth = 0f;
        textLayout.flexibleHeight = 0f;

        RectTransform textRt = rankText.rectTransform;
        if (textRt != null)
        {
            textRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, badgeSize);
            textRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, badgeSize);
        }

        if (background == null && autoCreateRankBackground)
            background = GetOrCreateGeneratedBackground(rankText);

        if (background == null)
            return;

        background.sprite = GetOrCreateCircleSprite();
        background.type = Image.Type.Simple;
        background.preserveAspect = false;
        background.raycastTarget = false;
        background.color = backgroundColor;

        RectTransform bgRt = background.rectTransform;
        if (bgRt != null)
        {
            bgRt.anchorMin = textRt.anchorMin;
            bgRt.anchorMax = textRt.anchorMax;
            bgRt.pivot = textRt.pivot;
            bgRt.anchoredPosition = textRt.anchoredPosition;
            bgRt.localRotation = Quaternion.identity;
            bgRt.localScale = Vector3.one;
            bgRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, badgeSize);
            bgRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, badgeSize);
        }

        LayoutElement bgLayout = background.GetComponent<LayoutElement>();
        if (bgLayout == null)
            bgLayout = background.gameObject.AddComponent<LayoutElement>();

        bgLayout.ignoreLayout = true;
        bgLayout.preferredWidth = badgeSize;
        bgLayout.minWidth = badgeSize;
        bgLayout.preferredHeight = badgeSize;
        bgLayout.minHeight = badgeSize;
        bgLayout.flexibleWidth = 0f;
        bgLayout.flexibleHeight = 0f;
    }

    void RefreshRankBackgroundPositions()
    {
        RefreshSingleBackground(firstRankText, firstRankBackground);
        RefreshSingleBackground(secondRankText, secondRankBackground);
        RefreshSingleBackground(thirdRankText, thirdRankBackground);
    }

    void RefreshSingleBackground(Text rankText, Image background)
    {
        if (rankText == null || background == null)
            return;

        RectTransform textRt = rankText.rectTransform;
        RectTransform bgRt = background.rectTransform;

        if (textRt == null || bgRt == null)
            return;

        float badgeSize = ScaledFloat(rankBadgeSize);

        bgRt.anchorMin = textRt.anchorMin;
        bgRt.anchorMax = textRt.anchorMax;
        bgRt.pivot = textRt.pivot;
        bgRt.anchoredPosition = textRt.anchoredPosition;
        bgRt.localRotation = Quaternion.identity;
        bgRt.localScale = Vector3.one;
        bgRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, badgeSize);
        bgRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, badgeSize);

        Transform parent = background.transform.parent;
        if (parent != null)
            background.transform.SetSiblingIndex(0);
    }

    Image GetOrCreateGeneratedBackground(Text rankText)
    {
        if (rankText == null)
            return null;

        RectTransform row = rankText.transform.parent as RectTransform;
        if (row == null)
            return null;

        string bgName = rankText.gameObject.name + "_GeneratedBadge";

        Transform existing = row.Find(bgName);
        if (existing != null)
        {
            Image existingImage = existing.GetComponent<Image>();
            if (existingImage != null)
            {
                existing.SetSiblingIndex(0);
                return existingImage;
            }
        }

        GameObject bgObject = new GameObject(bgName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        bgObject.transform.SetParent(row, false);
        bgObject.transform.SetSiblingIndex(0);

        Image bg = bgObject.GetComponent<Image>();
        bg.raycastTarget = false;

        LayoutElement le = bgObject.GetComponent<LayoutElement>();
        le.ignoreLayout = true;

        return bg;
    }

    Image FindGeneratedBackground(Text rankText)
    {
        if (rankText == null)
            return null;

        Transform parent = rankText.transform.parent;
        if (parent == null)
            return null;

        Transform bg = parent.Find(rankText.gameObject.name + "_GeneratedBadge");
        if (bg == null)
            return null;

        return bg.GetComponent<Image>();
    }

    Sprite GetOrCreateCircleSprite()
    {
        if (s_circleSprite != null)
            return s_circleSprite;

        int size = Mathf.Clamp(generatedCircleTextureSize, 16, 512);
        float softEdge = Mathf.Max(0.25f, generatedCircleSoftEdge);

        Texture2D texture = new Texture2D(size, size, TextureFormat.ARGB32, false);
        texture.name = "BossRushGeneratedCircle";
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        float radius = (size - 1) * 0.5f;
        Vector2 center = new Vector2(radius, radius);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x, y);
                float dist = Vector2.Distance(p, center);

                float alpha = 1f;
                float edgeStart = radius - softEdge;

                if (dist > radius)
                {
                    alpha = 0f;
                }
                else if (dist > edgeStart)
                {
                    alpha = 1f - Mathf.InverseLerp(edgeStart, radius, dist);
                }

                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();

        s_circleSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            size
        );

        return s_circleSprite;
    }

    static void TrySetFloat(Material mat, string prop, float value)
    {
        if (mat != null && mat.HasProperty(prop))
            mat.SetFloat(prop, value);
    }

    static void TrySetColor(Material mat, string prop, Color value)
    {
        if (mat != null && mat.HasProperty(prop))
            mat.SetColor(prop, value);
    }

    Vector2 ScaledVector(Vector2 value)
    {
        return new Vector2(ScaledFloat(value.x), ScaledFloat(value.y));
    }

    void SetTimeText(TextMeshProUGUI target, string value)
    {
        if (target != null)
            target.text = value;
    }

    string GetFormattedTime(List<float> times, int index)
    {
        if (times == null || index < 0 || index >= times.Count)
            return defaultTimeText;

        return BossRushProgress.FormatTime(times[index]);
    }

    string GetDifficultyDisplayName(BossRushDifficulty difficulty)
    {
        switch (difficulty)
        {
            case BossRushDifficulty.EASY: return "EASY";
            case BossRushDifficulty.NORMAL: return "NORMAL";
            case BossRushDifficulty.HARD: return "HARD";
            case BossRushDifficulty.NIGHTMARE: return "NIGHTMARE";
            default: return difficulty.ToString();
        }
    }

    void CaptureBaseLayoutPadding()
    {
        if (_layoutPaddingCaptured)
            return;

        VerticalLayoutGroup layout = GetComponent<VerticalLayoutGroup>();
        if (layout == null)
            return;

        _baseTopPadding = layout.padding.top;
        _baseBottomPadding = layout.padding.bottom;
        _baseLeftPadding = layout.padding.left;
        _baseRightPadding = layout.padding.right;
        _layoutPaddingCaptured = true;
    }

    void ApplyPanelVerticalOffset()
    {
        VerticalLayoutGroup layout = GetComponent<VerticalLayoutGroup>();
        if (layout == null)
            return;

        CaptureBaseLayoutPadding();

        layout.padding.top = _baseTopPadding + Mathf.RoundToInt(ScaledFloat(contentOffsetY));
        layout.padding.bottom = _baseBottomPadding;
        layout.padding.left = _baseLeftPadding;
        layout.padding.right = _baseRightPadding;
    }

    void SLog(string message)
    {
        if (!enableSurgicalLogs)
            return;

        Debug.Log($"{LOG} {message}", this);
    }
}
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class BossRushTopPanel : MonoBehaviour
{
    const string LOG = "[BossRushTopPanel]";

    [Header("Debug (Surgical Logs)")]
    [SerializeField] bool enableSurgicalLogs = true;

    [Header("Header")]
    [SerializeField] TextMeshProUGUI startWithText;
    [SerializeField] string startWithLabel = "Start with";
    [SerializeField] int headerFontSize = 16;
    [SerializeField] float headerPreferredWidth = 180f;
    [SerializeField] float headerRightSpacing = 24f;

    [Header("Item Roots")]
    [SerializeField] RectTransform bombAmountRoot;
    [SerializeField] RectTransform fireBlastRoot;
    [SerializeField] RectTransform speedRoot;
    [SerializeField] RectTransform heartRoot;

    [Header("Icons")]
    [SerializeField] Image bombAmountIcon;
    [SerializeField] Image fireBlastIcon;
    [SerializeField] Image speedIcon;
    [SerializeField] Image heartIcon;

    [Header("Amounts")]
    [SerializeField] TextMeshProUGUI bombAmountText;
    [SerializeField] TextMeshProUGUI fireBlastText;
    [SerializeField] TextMeshProUGUI speedText;
    [SerializeField] TextMeshProUGUI heartText;

    [Header("Text")]
    [SerializeField] int amountFontSize = 16;

    [Header("TMP Font")]
    [SerializeField] TMP_FontAsset topPanelFontAsset;
    [SerializeField] Material topPanelFontMaterialPreset;
    [SerializeField] bool forceBold = true;

    [Header("TMP Colors")]
    [SerializeField] Color textColor = Color.white;
    [SerializeField] Color outlineColor = Color.black;

    [Header("TMP Outline")]
    [SerializeField] bool useOutline = true;
    [SerializeField, Range(0f, 1f)] float outlineWidth = 0.35f;
    [SerializeField, Range(0f, 1f)] float outlineSoftness = 0f;

    [Header("TMP Face")]
    [SerializeField, Range(-1f, 1f)] float faceDilate = 0.2f;
    [SerializeField, Range(0f, 1f)] float faceSoftness = 0f;

    [Header("TMP Underlay")]
    [SerializeField] bool enableUnderlay = true;
    [SerializeField] Color underlayColor = new Color(0f, 0f, 0f, 1f);
    [SerializeField, Range(-1f, 1f)] float underlayDilate = 0.1f;
    [SerializeField, Range(0f, 1f)] float underlaySoftness = 0f;
    [SerializeField, Range(-2f, 2f)] float underlayOffsetX = 0.25f;
    [SerializeField, Range(-2f, 2f)] float underlayOffsetY = -0.25f;

    [Header("Sizing")]
    [SerializeField] float rowHeight = 28f;
    [SerializeField] float iconPreferredWidth = 28f;
    [SerializeField] float iconPreferredHeight = 28f;
    [SerializeField] float amountPreferredWidth = 44f;

    [Header("Root Layout")]
    [SerializeField] float rootSpacing = 18f;
    [SerializeField] TextAnchor rootAlignment = TextAnchor.MiddleLeft;
    [SerializeField] float rootLeftPadding = 140f;

    [Header("Item Layout")]
    [SerializeField] float itemSpacing = 8f;

    [Header("Layout")]
    [SerializeField] float contentOffsetX = 0f;
    [SerializeField] float contentOffsetY = 0f;

    float _currentUiScale = 1f;

    int _baseTopPadding;
    int _baseBottomPadding;
    int _baseLeftPadding;
    int _baseRightPadding;
    bool _layoutPaddingCaptured;

    bool _rootRectCaptured;
    Vector2 _baseAnchoredPosition;
    Vector2 _baseSizeDelta;

    readonly Dictionary<TMP_Text, Material> runtimeMaterials = new Dictionary<TMP_Text, Material>();

    int ScaledFont(int baseSize) => Mathf.Clamp(Mathf.RoundToInt(baseSize * _currentUiScale), 8, 300);
    float ScaledSize(float baseValue) => baseValue * _currentUiScale;

    public void Initialize(float uiScale)
    {
        _currentUiScale = uiScale;

        EnsureRootLayout();
        EnsureItemLayouts();

        CaptureBaseLayoutPadding();
        CaptureBaseRootRect();

        ApplyScaledRootRect();
        ApplyScaledFonts();
        ApplyLayoutFormatting();

        SetDifficulty(BossRushDifficulty.NORMAL);
    }

    public void SetUiScale(float uiScale)
    {
        _currentUiScale = uiScale;

        EnsureRootLayout();
        EnsureItemLayouts();

        CaptureBaseLayoutPadding();
        CaptureBaseRootRect();

        ApplyScaledRootRect();
        ApplyScaledFonts();
        ApplyLayoutFormatting();
    }

    public void SetDifficulty(BossRushDifficulty difficulty)
    {
        EnsureRootLayout();
        EnsureItemLayouts();

        if (startWithText != null)
            startWithText.text = startWithLabel;

        int itemAmount = GetItemAmount(difficulty);
        int heartAmount = GetHeartAmount(difficulty);

        SetAmountText(bombAmountText, itemAmount);
        SetAmountText(fireBlastText, itemAmount);
        SetAmountText(speedText, itemAmount);
        SetAmountText(heartText, heartAmount);

        ApplyScaledRootRect();
        ApplyScaledFonts();
        ApplyLayoutFormatting();
        ForceRebuild();

        SLog(
            $"SetDifficulty | difficulty={difficulty} " +
            $"bomb={itemAmount} fire={itemAmount} speed={itemAmount} heart={heartAmount} " +
            $"uiScale={_currentUiScale:0.###}"
        );
    }

    void OnDestroy()
    {
        foreach (var kv in runtimeMaterials)
        {
            if (kv.Value != null)
                Destroy(kv.Value);
        }

        runtimeMaterials.Clear();
    }

    int GetItemAmount(BossRushDifficulty difficulty)
    {
        switch (difficulty)
        {
            case BossRushDifficulty.EASY: return 4;
            case BossRushDifficulty.NORMAL: return 3;
            case BossRushDifficulty.HARD: return 2;
            case BossRushDifficulty.NIGHTMARE: return 1;
            default: return 3;
        }
    }

    int GetHeartAmount(BossRushDifficulty difficulty)
    {
        switch (difficulty)
        {
            case BossRushDifficulty.EASY: return 4;
            case BossRushDifficulty.NORMAL: return 3;
            case BossRushDifficulty.HARD: return 0;
            case BossRushDifficulty.NIGHTMARE: return 0;
            default: return 3;
        }
    }

    void EnsureRootLayout()
    {
        HorizontalLayoutGroup rootLayout = GetComponent<HorizontalLayoutGroup>();
        if (rootLayout == null)
            rootLayout = gameObject.AddComponent<HorizontalLayoutGroup>();

        RectTransform rootRt = transform as RectTransform;
        if (rootRt != null)
        {
            rootRt.anchorMin = new Vector2(0.5f, 1f);
            rootRt.anchorMax = new Vector2(0.5f, 1f);
            rootRt.pivot = new Vector2(0.5f, 1f);
            rootRt.localScale = Vector3.one;
        }
    }

    void EnsureItemLayouts()
    {
        EnsureItemLayout(bombAmountRoot);
        EnsureItemLayout(fireBlastRoot);
        EnsureItemLayout(speedRoot);
        EnsureItemLayout(heartRoot);

        EnsureLayoutElement(startWithText);

        EnsureLayoutElement(bombAmountIcon);
        EnsureLayoutElement(fireBlastIcon);
        EnsureLayoutElement(speedIcon);
        EnsureLayoutElement(heartIcon);

        EnsureLayoutElement(bombAmountText);
        EnsureLayoutElement(fireBlastText);
        EnsureLayoutElement(speedText);
        EnsureLayoutElement(heartText);
    }

    void EnsureItemLayout(RectTransform root)
    {
        if (root == null)
            return;

        HorizontalLayoutGroup layout = root.GetComponent<HorizontalLayoutGroup>();
        if (layout == null)
            layout = root.gameObject.AddComponent<HorizontalLayoutGroup>();

        LayoutElement layoutElement = root.GetComponent<LayoutElement>();
        if (layoutElement == null)
            layoutElement = root.gameObject.AddComponent<LayoutElement>();
    }

    void EnsureLayoutElement(Component component)
    {
        if (component == null)
            return;

        if (component.GetComponent<LayoutElement>() == null)
            component.gameObject.AddComponent<LayoutElement>();
    }

    void CaptureBaseRootRect()
    {
        if (_rootRectCaptured)
            return;

        RectTransform rt = transform as RectTransform;
        if (rt == null)
            return;

        _baseAnchoredPosition = rt.anchoredPosition;
        _baseSizeDelta = rt.sizeDelta;
        _rootRectCaptured = true;

        SLog($"CaptureBaseRootRect | anchoredPos={_baseAnchoredPosition} sizeDelta={_baseSizeDelta}");
    }

    void ApplyScaledRootRect()
    {
        RectTransform rt = transform as RectTransform;
        if (rt == null)
            return;

        CaptureBaseRootRect();

        Vector2 scaledPos = new Vector2(
            ScaledSize(_baseAnchoredPosition.x),
            ScaledSize(_baseAnchoredPosition.y)
        );

        Vector2 scaledSize = new Vector2(
            ScaledSize(_baseSizeDelta.x),
            ScaledSize(_baseSizeDelta.y)
        );

        rt.anchoredPosition = scaledPos;
        rt.sizeDelta = scaledSize;

        SLog(
            $"ApplyScaledRootRect | uiScale={_currentUiScale:0.###} " +
            $"anchoredPos={scaledPos} sizeDelta={scaledSize}"
        );
    }

    void SetAmountText(TextMeshProUGUI target, int value)
    {
        if (target != null)
            target.text = $"x{value}";
    }

    void ApplyScaledFonts()
    {
        ApplyTmpFontStyle(startWithText, headerFontSize);
        ApplyTmpFontStyle(bombAmountText, amountFontSize);
        ApplyTmpFontStyle(fireBlastText, amountFontSize);
        ApplyTmpFontStyle(speedText, amountFontSize);
        ApplyTmpFontStyle(heartText, amountFontSize);
    }

    void ApplyTmpFontStyle(TextMeshProUGUI target, int baseFontSize)
    {
        if (target == null)
            return;

        if (topPanelFontAsset != null)
            target.font = topPanelFontAsset;

        target.fontSize = ScaledFont(baseFontSize);
        target.enableAutoSizing = false;
        target.textWrappingMode = TextWrappingModes.NoWrap;
        target.overflowMode = TextOverflowModes.Overflow;
        target.extraPadding = true;
        target.margin = Vector4.zero;
        target.color = textColor;

        if (forceBold)
            target.fontStyle |= FontStyles.Bold;
        else
            target.fontStyle &= ~FontStyles.Bold;

        Material runtimeMat = GetOrCreateRuntimeMaterial(target);
        ApplyMaterialStyle(runtimeMat);

        target.fontMaterial = runtimeMat;
        target.UpdateMeshPadding();
        target.ForceMeshUpdate();
        target.SetVerticesDirty();
    }

    Material GetOrCreateRuntimeMaterial(TMP_Text target)
    {
        if (target == null)
            return null;

        if (runtimeMaterials.TryGetValue(target, out Material runtimeMat) && runtimeMat != null)
            return runtimeMat;

        Material baseMat = null;

        if (topPanelFontMaterialPreset != null)
            baseMat = topPanelFontMaterialPreset;
        else if (target.fontSharedMaterial != null)
            baseMat = target.fontSharedMaterial;
        else if (target.font != null)
            baseMat = target.font.material;

        if (baseMat == null)
            return null;

        runtimeMat = new Material(baseMat);
        runtimeMat.name = baseMat.name + "_BossRushTopRuntime";
        runtimeMaterials[target] = runtimeMat;
        return runtimeMat;
    }

    void ApplyMaterialStyle(Material mat)
    {
        if (mat == null)
            return;

        TrySetColor(mat, "_FaceColor", textColor);

        if (useOutline)
        {
            TrySetColor(mat, "_OutlineColor", outlineColor);
            TrySetFloat(mat, "_OutlineWidth", outlineWidth);
            TrySetFloat(mat, "_OutlineSoftness", outlineSoftness);
        }
        else
        {
            TrySetFloat(mat, "_OutlineWidth", 0f);
            TrySetFloat(mat, "_OutlineSoftness", 0f);
        }

        TrySetFloat(mat, "_FaceDilate", faceDilate);
        TrySetFloat(mat, "_FaceSoftness", faceSoftness);

        if (enableUnderlay)
        {
            TrySetColor(mat, "_UnderlayColor", underlayColor);
            TrySetFloat(mat, "_UnderlayDilate", underlayDilate);
            TrySetFloat(mat, "_UnderlaySoftness", underlaySoftness);
            TrySetFloat(mat, "_UnderlayOffsetX", underlayOffsetX);
            TrySetFloat(mat, "_UnderlayOffsetY", underlayOffsetY);
        }
        else
        {
            TrySetFloat(mat, "_UnderlayDilate", 0f);
            TrySetFloat(mat, "_UnderlaySoftness", 0f);
            TrySetFloat(mat, "_UnderlayOffsetX", 0f);
            TrySetFloat(mat, "_UnderlayOffsetY", 0f);
        }
    }

    void ApplyLayoutFormatting()
    {
        ApplyPanelOffset();
        ConfigureRootLayout();

        ConfigureHeaderText(startWithText);
        ConfigureItemGroup(bombAmountRoot, bombAmountIcon, bombAmountText);
        ConfigureItemGroup(fireBlastRoot, fireBlastIcon, fireBlastText);
        ConfigureItemGroup(speedRoot, speedIcon, speedText);
        ConfigureItemGroup(heartRoot, heartIcon, heartText);

        ConfigureHeaderWidth(startWithText, headerPreferredWidth);

        ConfigureAmountWidth(bombAmountText);
        ConfigureAmountWidth(fireBlastText);
        ConfigureAmountWidth(speedText);
        ConfigureAmountWidth(heartText);

        ConfigureIconSize(bombAmountIcon);
        ConfigureIconSize(fireBlastIcon);
        ConfigureIconSize(speedIcon);
        ConfigureIconSize(heartIcon);

        ForceRebuild();
    }

    void ConfigureRootLayout()
    {
        HorizontalLayoutGroup layout = GetComponent<HorizontalLayoutGroup>();
        if (layout == null)
            return;

        layout.spacing = ScaledSize(rootSpacing);
        layout.childAlignment = rootAlignment;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
    }

    void ConfigureHeaderText(TextMeshProUGUI target)
    {
        if (target == null)
            return;

        target.alignment = TextAlignmentOptions.MidlineLeft;
        target.textWrappingMode = TextWrappingModes.NoWrap;
        target.overflowMode = TextOverflowModes.Overflow;
        target.enableAutoSizing = false;

        float h = Mathf.Max(ScaledSize(rowHeight), target.fontSize + ScaledSize(12f));

        RectTransform rt = target.rectTransform;
        rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, h);

        LayoutElement le = target.GetComponent<LayoutElement>();
        if (le == null)
            le = target.gameObject.AddComponent<LayoutElement>();

        le.minHeight = h;
        le.preferredHeight = h;
        le.flexibleHeight = 0f;
        le.flexibleWidth = 0f;
    }

    void ConfigureItemGroup(RectTransform root, Image icon, TextMeshProUGUI amountText)
    {
        if (root == null)
            return;

        float h = Mathf.Max(ScaledSize(rowHeight), ScaledFont(amountFontSize) + ScaledSize(12f));
        root.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, h);

        LayoutElement rootLe = root.GetComponent<LayoutElement>();
        if (rootLe == null)
            rootLe = root.gameObject.AddComponent<LayoutElement>();

        rootLe.minHeight = h;
        rootLe.preferredHeight = h;
        rootLe.flexibleHeight = 0f;
        rootLe.flexibleWidth = 0f;
        rootLe.minWidth = -1f;
        rootLe.preferredWidth = -1f;

        HorizontalLayoutGroup layout = root.GetComponent<HorizontalLayoutGroup>();
        if (layout != null)
        {
            layout.spacing = ScaledSize(itemSpacing);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
        }

        ConfigureIcon(icon);
        ConfigureAmountText(amountText);
    }

    void ConfigureIcon(Image target)
    {
        if (target == null)
            return;

        target.preserveAspect = false;
    }

    void ConfigureAmountText(TextMeshProUGUI target)
    {
        if (target == null)
            return;

        target.alignment = TextAlignmentOptions.MidlineLeft;
        target.textWrappingMode = TextWrappingModes.NoWrap;
        target.overflowMode = TextOverflowModes.Overflow;
        target.enableAutoSizing = false;
    }

    void ConfigureHeaderWidth(TextMeshProUGUI target, float preferredWidth)
    {
        if (target == null)
            return;

        LayoutElement le = target.GetComponent<LayoutElement>();
        if (le == null)
            le = target.gameObject.AddComponent<LayoutElement>();

        float scaledBaseWidth = ScaledSize(preferredWidth);
        float textRequiredWidth = target.preferredWidth + ScaledSize(headerRightSpacing);
        float finalWidth = Mathf.Max(scaledBaseWidth, textRequiredWidth);

        le.minWidth = finalWidth;
        le.preferredWidth = finalWidth;
        le.flexibleWidth = 0f;
    }

    void ConfigureAmountWidth(TextMeshProUGUI target)
    {
        if (target == null)
            return;

        LayoutElement le = target.GetComponent<LayoutElement>();
        if (le == null)
            le = target.gameObject.AddComponent<LayoutElement>();

        float w = ScaledSize(amountPreferredWidth);

        le.minWidth = w;
        le.preferredWidth = w;
        le.flexibleWidth = 0f;
    }

    void ConfigureIconSize(Image target)
    {
        if (target == null)
            return;

        LayoutElement le = target.GetComponent<LayoutElement>();
        if (le == null)
            le = target.gameObject.AddComponent<LayoutElement>();

        float w = ScaledSize(iconPreferredWidth);
        float h = ScaledSize(iconPreferredHeight);

        le.minWidth = w;
        le.preferredWidth = w;
        le.flexibleWidth = 0f;

        le.minHeight = h;
        le.preferredHeight = h;
        le.flexibleHeight = 0f;

        RectTransform rt = target.rectTransform;
        rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, w);
        rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, h);
    }

    void CaptureBaseLayoutPadding()
    {
        if (_layoutPaddingCaptured)
            return;

        HorizontalLayoutGroup layout = GetComponent<HorizontalLayoutGroup>();
        if (layout == null)
            return;

        _baseTopPadding = layout.padding.top;
        _baseBottomPadding = layout.padding.bottom;
        _baseLeftPadding = layout.padding.left;
        _baseRightPadding = layout.padding.right;
        _layoutPaddingCaptured = true;
    }

    void ApplyPanelOffset()
    {
        HorizontalLayoutGroup layout = GetComponent<HorizontalLayoutGroup>();
        if (layout == null)
            return;

        CaptureBaseLayoutPadding();

        layout.padding.left =
            _baseLeftPadding +
            Mathf.RoundToInt(ScaledSize(rootLeftPadding)) +
            Mathf.RoundToInt(ScaledSize(contentOffsetX));

        layout.padding.top =
            _baseTopPadding +
            Mathf.RoundToInt(ScaledSize(contentOffsetY));

        layout.padding.right = _baseRightPadding;
        layout.padding.bottom = _baseBottomPadding;
    }

    void ForceRebuild()
    {
        Canvas.ForceUpdateCanvases();

        RectTransform rt = transform as RectTransform;
        if (rt != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);

        if (bombAmountRoot != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(bombAmountRoot);

        if (fireBlastRoot != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(fireBlastRoot);

        if (speedRoot != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(speedRoot);

        if (heartRoot != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(heartRoot);
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

    void SLog(string message)
    {
        if (!enableSurgicalLogs)
            return;

        Debug.Log($"{LOG} {message}", this);
    }
}
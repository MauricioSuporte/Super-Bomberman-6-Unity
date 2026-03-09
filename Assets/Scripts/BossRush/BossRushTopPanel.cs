using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class BossRushTopPanel : MonoBehaviour
{
    const string LOG = "[BossRushTopPanel]";

    [Header("Debug (Surgical Logs)")]
    [SerializeField] bool enableSurgicalLogs = true;

    [Header("Header")]
    [SerializeField] Text startWithText;
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
    [SerializeField] Text bombAmountText;
    [SerializeField] Text fireBlastText;
    [SerializeField] Text speedText;
    [SerializeField] Text heartText;

    [Header("Text")]
    [SerializeField] int amountFontSize = 16;

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

        SLog(
            $"CaptureBaseRootRect | anchoredPos={_baseAnchoredPosition} sizeDelta={_baseSizeDelta}"
        );
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

    void SetAmountText(Text target, int value)
    {
        if (target != null)
            target.text = $"x{value}";
    }

    void ApplyScaledFonts()
    {
        if (startWithText != null)
            startWithText.fontSize = ScaledFont(headerFontSize);

        if (bombAmountText != null)
            bombAmountText.fontSize = ScaledFont(amountFontSize);

        if (fireBlastText != null)
            fireBlastText.fontSize = ScaledFont(amountFontSize);

        if (speedText != null)
            speedText.fontSize = ScaledFont(amountFontSize);

        if (heartText != null)
            heartText.fontSize = ScaledFont(amountFontSize);
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

    void ConfigureHeaderText(Text target)
    {
        if (target == null)
            return;

        target.alignment = TextAnchor.MiddleLeft;
        target.horizontalOverflow = HorizontalWrapMode.Overflow;
        target.verticalOverflow = VerticalWrapMode.Overflow;
        target.resizeTextForBestFit = false;
        target.supportRichText = false;

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

    void ConfigureItemGroup(RectTransform root, Image icon, Text amountText)
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

    void ConfigureAmountText(Text target)
    {
        if (target == null)
            return;

        target.alignment = TextAnchor.MiddleLeft;
        target.horizontalOverflow = HorizontalWrapMode.Overflow;
        target.verticalOverflow = VerticalWrapMode.Overflow;
        target.resizeTextForBestFit = false;
        target.supportRichText = false;
    }

    void ConfigureHeaderWidth(Text target, float preferredWidth)
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

    void ConfigureAmountWidth(Text target)
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

    void SLog(string message)
    {
        if (!enableSurgicalLogs)
            return;

        Debug.Log($"{LOG} {message}", this);
    }
}
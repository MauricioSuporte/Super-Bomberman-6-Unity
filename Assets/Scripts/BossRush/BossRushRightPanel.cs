using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BossRushRightPanel : MonoBehaviour
{
    const string LOG = "[BossRushRightPanel]";

    [Header("Debug (Surgical Logs)")]
    [SerializeField] bool enableSurgicalLogs = true;

    [Header("Title")]
    [SerializeField] Text titleText;
    [SerializeField] string titleFormat = "{0}";
    [SerializeField] int titleFontSize = 18;
    [SerializeField] float titleHeight = 28f;
    [SerializeField] float titleOffsetX = 0f;

    [Header("Rows")]
    [SerializeField] RectTransform firstRow;
    [SerializeField] RectTransform secondRow;
    [SerializeField] RectTransform thirdRow;

    [SerializeField] Text firstRankText;
    [SerializeField] Text firstTimeText;
    [SerializeField] Text secondRankText;
    [SerializeField] Text secondTimeText;
    [SerializeField] Text thirdRankText;
    [SerializeField] Text thirdTimeText;

    [SerializeField] int rowFontSize = 16;
    [SerializeField] float rowHeight = 24f;

    [Header("Row Widths")]
    [SerializeField] float rankPreferredWidth = 120f;
    [SerializeField] float timePreferredWidth = 220f;

    [Header("Labels")]
    [SerializeField] string firstLabel = "1ST";
    [SerializeField] string secondLabel = "2ND";
    [SerializeField] string thirdLabel = "3RD";
    [SerializeField] string defaultTimeText = "--:--.--";

    [Header("Panel Offset")]
    [SerializeField] float contentOffsetY = 0f;

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
        CaptureBaseLayoutPadding();
        ApplyScaledFonts();
        ApplyLayoutFormatting();
        SetDifficulty(BossRushDifficulty.NORMAL);
    }

    public void SetUiScale(float uiScale)
    {
        _currentUiScale = uiScale;
        ApplyScaledFonts();
        ApplyLayoutFormatting();
    }

    public void SetDifficulty(BossRushDifficulty difficulty)
    {
        ApplyScaledFonts();
        ApplyLayoutFormatting();

        if (titleText != null)
            titleText.text = string.Format(titleFormat, GetDifficultyDisplayName(difficulty));

        if (firstRankText != null) firstRankText.text = firstLabel;
        if (secondRankText != null) secondRankText.text = secondLabel;
        if (thirdRankText != null) thirdRankText.text = thirdLabel;

        List<float> times = BossRushProgress.GetTopTimes(difficulty);

        SetTimeText(firstTimeText, GetFormattedTime(times, 0));
        SetTimeText(secondTimeText, GetFormattedTime(times, 1));
        SetTimeText(thirdTimeText, GetFormattedTime(times, 2));

        SLog(
            $"SetDifficulty | difficulty={difficulty} " +
            $"t0={GetFormattedTime(times, 0)} t1={GetFormattedTime(times, 1)} t2={GetFormattedTime(times, 2)}"
        );
    }

    void ApplyScaledFonts()
    {
        if (titleText != null)
            titleText.fontSize = ScaledFont(titleFontSize);

        if (firstRankText != null)
            firstRankText.fontSize = ScaledFont(rowFontSize);

        if (firstTimeText != null)
            firstTimeText.fontSize = ScaledFont(rowFontSize);

        if (secondRankText != null)
            secondRankText.fontSize = ScaledFont(rowFontSize);

        if (secondTimeText != null)
            secondTimeText.fontSize = ScaledFont(rowFontSize);

        if (thirdRankText != null)
            thirdRankText.fontSize = ScaledFont(rowFontSize);

        if (thirdTimeText != null)
            thirdTimeText.fontSize = ScaledFont(rowFontSize);
    }

    void ApplyLayoutFormatting()
    {
        ApplyPanelVerticalOffset();

        ConfigureTitleText(titleText);
        ConfigureLabelText(firstRankText);
        ConfigureLabelText(secondRankText);
        ConfigureLabelText(thirdRankText);

        ConfigureTimeText(firstTimeText);
        ConfigureTimeText(secondTimeText);
        ConfigureTimeText(thirdTimeText);

        ConfigureRow(firstRow);
        ConfigureRow(secondRow);
        ConfigureRow(thirdRow);

        ConfigureLayoutElementWidth(firstRankText, rankPreferredWidth);
        ConfigureLayoutElementWidth(secondRankText, rankPreferredWidth);
        ConfigureLayoutElementWidth(thirdRankText, rankPreferredWidth);

        ConfigureLayoutElementWidth(firstTimeText, timePreferredWidth);
        ConfigureLayoutElementWidth(secondTimeText, timePreferredWidth);
        ConfigureLayoutElementWidth(thirdTimeText, timePreferredWidth);

        ConfigureTitleHeight();
        ConfigureTitleWidthAndPosition();
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
            ScaledFloat(rankPreferredWidth) +
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

        float h = Mathf.Max(ScaledFloat(rowHeight), ScaledFont(rowFontSize) + ScaledFloat(12f));
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

    void ConfigureLayoutElementWidth(Text target, float preferredWidth)
    {
        if (target == null)
            return;

        LayoutElement le = target.GetComponent<LayoutElement>();
        if (le == null)
            le = target.gameObject.AddComponent<LayoutElement>();

        le.preferredWidth = ScaledFloat(preferredWidth);
        le.minWidth = 0f;
        le.flexibleWidth = 0f;
    }

    void ConfigureTitleText(Text target)
    {
        if (target == null)
            return;

        target.alignment = TextAnchor.MiddleCenter;
        target.horizontalOverflow = HorizontalWrapMode.Overflow;
        target.verticalOverflow = VerticalWrapMode.Overflow;
        target.resizeTextForBestFit = false;
        target.supportRichText = false;
    }

    void ConfigureLabelText(Text target)
    {
        if (target == null)
            return;

        target.alignment = TextAnchor.MiddleLeft;
        target.horizontalOverflow = HorizontalWrapMode.Overflow;
        target.verticalOverflow = VerticalWrapMode.Overflow;
        target.resizeTextForBestFit = false;
        target.supportRichText = false;
    }

    void ConfigureTimeText(Text target)
    {
        if (target == null)
            return;

        target.alignment = TextAnchor.MiddleRight;
        target.horizontalOverflow = HorizontalWrapMode.Overflow;
        target.verticalOverflow = VerticalWrapMode.Overflow;
        target.resizeTextForBestFit = false;
        target.supportRichText = false;
    }

    void SetTimeText(Text target, string value)
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
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SaveFileMenuOptions : MonoBehaviour
{
    [Header("Option List")]
    [SerializeField] private RectTransform optionListRoot;
    [SerializeField] private TextMeshProUGUI optionItemPrefab;
    [SerializeField] private int fontSize = 20;
    [SerializeField] private float optionItemHeight = 34f;
    [SerializeField] private Vector2 optionSpacing = new Vector2(0f, 12f);
    [SerializeField] private float optionContentOffsetX = 26f;
    [SerializeField] private float optionContentOffsetY = 0f;
    [SerializeField] private float cursorExtraOffsetY = 0f;

    [Header("Menu Block Position")]
    [SerializeField, Range(0f, 1f)] private float blockCenterX = 0.36f;
    [SerializeField, Range(0f, 1f)] private float blockCenterY = 0.47f;
    [SerializeField] private float cursorReservedWidth = 56f;
    [SerializeField] private float extraBlockWidth = 8f;
    [SerializeField] private float extraBlockHeight = 0f;

    [Header("Colors")]
    [SerializeField] private Color normalColor = new Color32(0, 180, 0, 255);
    [SerializeField] private Color selectedColor = new Color32(0, 255, 60, 255);
    [SerializeField] private Color disabledColor = new Color32(0, 120, 0, 180);

    [Header("TMP")]
    [SerializeField] private TMP_FontAsset optionFontAsset;
    [SerializeField] private Material optionFontMaterialPreset;
    [SerializeField] private bool forceBold = true;
    [SerializeField] private bool autoSize = false;
    [SerializeField, Range(0.25f, 1f)] private float autoSizeMinRatio = 0.75f;

    [Header("TMP Outline")]
    [SerializeField] private bool useOutline = true;
    [SerializeField] private Color outlineColor = Color.black;
    [SerializeField, Range(0f, 1f)] private float outlineWidth = 0.35f;
    [SerializeField, Range(0f, 1f)] private float outlineSoftness = 0f;

    [Header("TMP Face")]
    [SerializeField, Range(-1f, 1f)] private float faceDilate = 0.2f;
    [SerializeField, Range(0f, 1f)] private float faceSoftness = 0f;

    [Header("TMP Underlay")]
    [SerializeField] private bool enableUnderlay = true;
    [SerializeField] private Color underlayColor = new Color(0f, 0f, 0f, 1f);
    [SerializeField, Range(-1f, 1f)] private float underlayDilate = 0.1f;
    [SerializeField, Range(0f, 1f)] private float underlaySoftness = 0f;
    [SerializeField, Range(-2f, 2f)] private float underlayOffsetX = 0.25f;
    [SerializeField, Range(-2f, 2f)] private float underlayOffsetY = -0.25f;

    [Header("Cursor")]
    [SerializeField] private AnimatedSpriteRenderer cursorRenderer;
    [SerializeField] private Vector2 cursorOffset = new Vector2(-30f, 0f);
    [SerializeField] private bool roundCursorToWholePixels = true;
    [SerializeField] private float cursorHeightMultiplier = 1.45f;
    [SerializeField] private float minCursorSize = 24f;

    private readonly List<TextMeshProUGUI> optionTexts = new();
    private readonly Dictionary<TMP_Text, Material> runtimeMaterials = new();
    private readonly List<string> entries = new();
    private readonly List<bool> entryEnabled = new();

    private float _currentUiScale = 1f;

    private Vector2 _cursorBaseSizeDelta = new Vector2(16f, 16f);
    private bool _cursorBaseSizeCaptured;

    private bool _cursorBaseIdle = true;
    private bool _cursorBaseLoop = true;
    private bool _cursorAnimationStateCaptured;
    private bool _cursorAnimatingConfirmed;

    private int _baseLayoutPaddingLeft;
    private int _baseLayoutPaddingRight;
    private int _baseLayoutPaddingTop;
    private int _baseLayoutPaddingBottom;
    private bool _layoutPaddingCaptured;

    public int Count => entries.Count;

    public void Awake()
    {
        if (cursorRenderer != null)
        {
            RectTransform cursorRt = cursorRenderer.transform as RectTransform;
            if (cursorRt != null)
            {
                _cursorBaseSizeDelta = cursorRt.sizeDelta;
                _cursorBaseSizeCaptured = true;
            }

            _cursorBaseIdle = cursorRenderer.idle;
            _cursorBaseLoop = cursorRenderer.loop;
            _cursorAnimationStateCaptured = true;
            cursorRenderer.gameObject.SetActive(false);
        }

        CaptureBaseLayoutPadding();
    }

    private void OnDestroy()
    {
        foreach (var kv in runtimeMaterials)
        {
            if (kv.Value != null)
                Destroy(kv.Value);
        }

        runtimeMaterials.Clear();
    }

    public void Initialize(float uiScale)
    {
        _currentUiScale = uiScale;
        CaptureBaseLayoutPadding();
        ApplyCursorScale();
        ApplyOptionListLayoutSettings();
    }

    public void SetUiScale(float uiScale)
    {
        _currentUiScale = uiScale;

        ApplyCursorScale();
        ApplyOptionListLayoutSettings();

        for (int i = 0; i < optionTexts.Count; i++)
        {
            if (optionTexts[i] == null)
                continue;

            bool enabled = i < entryEnabled.Count ? entryEnabled[i] : true;
            ApplyOptionTextStyle(optionTexts[i], enabled ? normalColor : disabledColor);

            RectTransform rt = optionTexts[i].rectTransform;
            rt.sizeDelta = new Vector2(0f, Mathf.Round(ScaledFloat(optionItemHeight)));

            LayoutElement le = optionTexts[i].GetComponent<LayoutElement>();
            if (le != null)
            {
                le.minHeight = Mathf.Round(ScaledFloat(optionItemHeight));
                le.preferredHeight = Mathf.Round(ScaledFloat(optionItemHeight));
            }
        }

        VerticalLayoutGroup layout = optionListRoot != null ? optionListRoot.GetComponent<VerticalLayoutGroup>() : null;
        if (layout != null)
            layout.spacing = Mathf.Round(ScaledFloat(optionSpacing.y));

        Canvas.ForceUpdateCanvases();
        if (optionListRoot != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(optionListRoot);
    }

    public void SetEntries(IReadOnlyList<string> newEntries, IReadOnlyList<bool> enabledStates)
    {
        entries.Clear();
        entryEnabled.Clear();

        if (newEntries != null)
        {
            for (int i = 0; i < newEntries.Count; i++)
                entries.Add(newEntries[i] ?? string.Empty);
        }

        if (enabledStates != null)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                bool enabled = i < enabledStates.Count && enabledStates[i];
                entryEnabled.Add(enabled);
            }
        }
        else
        {
            for (int i = 0; i < entries.Count; i++)
                entryEnabled.Add(true);
        }

        BuildOptionList();
    }

    public bool IsEntryEnabled(int index)
    {
        if (index < 0 || index >= entryEnabled.Count)
            return false;

        return entryEnabled[index];
    }

    public string GetEntryAt(int index)
    {
        if (index < 0 || index >= entries.Count)
            return string.Empty;

        return entries[index];
    }

    public void BuildOptionList()
    {
        optionTexts.Clear();

        if (optionListRoot == null || optionItemPrefab == null)
            return;

        ApplyOptionListLayoutSettings();

        for (int i = optionListRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = optionListRoot.GetChild(i);
            if (child == null)
                continue;

            if (child == optionItemPrefab.transform)
                continue;

            if (cursorRenderer != null && child == cursorRenderer.transform)
                continue;

            Destroy(child.gameObject);
        }

        optionItemPrefab.gameObject.SetActive(false);

        for (int i = 0; i < entries.Count; i++)
        {
            TextMeshProUGUI txt = Instantiate(optionItemPrefab, optionListRoot);
            txt.gameObject.SetActive(true);
            txt.enabled = true;
            txt.text = entries[i];
            txt.transform.SetAsLastSibling();

            Color faceColor = entryEnabled[i] ? normalColor : disabledColor;
            ApplyOptionTextStyle(txt, faceColor);

            RectTransform rt = txt.rectTransform;
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, Mathf.Round(ScaledFloat(optionItemHeight)));
            rt.offsetMin = new Vector2(0f, 0f);
            rt.offsetMax = new Vector2(0f, 0f);

            LayoutElement le = txt.GetComponent<LayoutElement>();
            if (le == null)
                le = txt.gameObject.AddComponent<LayoutElement>();

            le.ignoreLayout = false;
            le.minHeight = Mathf.Round(ScaledFloat(optionItemHeight));
            le.preferredHeight = Mathf.Round(ScaledFloat(optionItemHeight));
            le.flexibleHeight = 0f;
            le.minWidth = 0f;
            le.preferredWidth = 0f;
            le.flexibleWidth = 1f;

            optionTexts.Add(txt);
        }

        if (cursorRenderer != null)
            cursorRenderer.transform.SetAsLastSibling();

        ApplyOptionListLayoutSettings();

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(optionListRoot);
    }

    public void UpdateOptionVisuals(int selectedIndex, bool confirmed)
    {
        for (int i = 0; i < optionTexts.Count; i++)
        {
            TextMeshProUGUI txt = optionTexts[i];
            if (txt == null)
                continue;

            bool isSelected = i == selectedIndex;
            bool enabled = i < entryEnabled.Count ? entryEnabled[i] : true;

            txt.text = i < entries.Count ? entries[i] : string.Empty;

            Color faceColor = isSelected ? selectedColor : normalColor;
            if (!enabled)
                faceColor = disabledColor;

            ApplyOptionTextStyle(txt, faceColor);
        }

        UpdateCursorAnimationState(confirmed);
        UpdateCursorPosition(selectedIndex);
    }

    public void ShowCursor()
    {
        if (cursorRenderer != null)
        {
            cursorRenderer.gameObject.SetActive(true);
            cursorRenderer.RefreshFrame();
        }
    }

    public void HideCursor()
    {
        if (cursorRenderer != null)
        {
            UpdateCursorAnimationState(false);
            cursorRenderer.gameObject.SetActive(false);
        }
    }

    public void UpdateCursorPosition(int selectedIndex)
    {
        if (cursorRenderer == null)
            return;

        if (selectedIndex < 0 || selectedIndex >= optionTexts.Count)
        {
            cursorRenderer.gameObject.SetActive(false);
            return;
        }

        TextMeshProUGUI txt = optionTexts[selectedIndex];
        if (txt == null)
        {
            cursorRenderer.gameObject.SetActive(false);
            return;
        }

        cursorRenderer.gameObject.SetActive(true);

        RectTransform txtRt = txt.rectTransform;
        RectTransform cursorRt = cursorRenderer.transform as RectTransform;

        Vector3 localPos = txtRt.localPosition;
        localPos.x += Mathf.Round(ScaledFloat(cursorOffset.x));

        float textCenterOffsetY = 0f;
        if (cursorRt != null)
        {
            float textHeight = txtRt.rect.height;
            float cursorHeight = cursorRt.rect.height;
            textCenterOffsetY = (textHeight - cursorHeight) * 0.5f;
        }

        localPos.y += Mathf.Round(ScaledFloat(cursorOffset.y) + ScaledFloat(cursorExtraOffsetY) - textCenterOffsetY);
        localPos.z = 0f;

        if (roundCursorToWholePixels)
        {
            localPos.x = Mathf.Round(localPos.x);
            localPos.y = Mathf.Round(localPos.y);
        }

        cursorRenderer.SetExternalBaseLocalPosition(localPos);
    }

    private void ApplyOptionTextStyle(TextMeshProUGUI txt, Color faceColor)
    {
        if (txt == null)
            return;

        if (optionFontAsset != null)
            txt.font = optionFontAsset;

        txt.textWrappingMode = TextWrappingModes.NoWrap;
        txt.overflowMode = TextOverflowModes.Overflow;
        txt.enableAutoSizing = autoSize;
        txt.extraPadding = true;
        txt.alignment = TextAlignmentOptions.MidlineLeft;
        txt.color = faceColor;
        txt.margin = Vector4.zero;

        float scaledSize = ScaledFont(fontSize);
        txt.fontSize = scaledSize;

        if (autoSize)
        {
            txt.fontSizeMax = scaledSize;
            txt.fontSizeMin = Mathf.Max(8f, Mathf.Floor(scaledSize * autoSizeMinRatio));
        }

        if (forceBold)
            txt.fontStyle |= FontStyles.Bold;
        else
            txt.fontStyle &= ~FontStyles.Bold;

        Material runtimeMat = GetOrCreateRuntimeMaterial(txt);
        ApplyMaterialStyle(runtimeMat, faceColor);

        if (runtimeMat != null)
            txt.fontMaterial = runtimeMat;

        txt.UpdateMeshPadding();
        txt.ForceMeshUpdate();
        txt.SetVerticesDirty();
    }

    private Material GetOrCreateRuntimeMaterial(TMP_Text target)
    {
        if (target == null)
            return null;

        if (runtimeMaterials.TryGetValue(target, out Material runtimeMat) && runtimeMat != null)
            return runtimeMat;

        Material baseMat = null;

        if (optionFontMaterialPreset != null)
            baseMat = optionFontMaterialPreset;
        else if (target.fontSharedMaterial != null)
            baseMat = target.fontSharedMaterial;
        else if (target.font != null)
            baseMat = target.font.material;

        if (baseMat == null)
            return null;

        runtimeMat = new Material(baseMat);
        runtimeMat.name = baseMat.name + "_SaveFileMenuOptionsRuntime";
        runtimeMaterials[target] = runtimeMat;
        return runtimeMat;
    }

    private void ApplyMaterialStyle(Material mat, Color faceColor)
    {
        if (mat == null)
            return;

        TrySetColor(mat, "_FaceColor", faceColor);

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

    private void CaptureBaseLayoutPadding()
    {
        if (_layoutPaddingCaptured || optionListRoot == null)
            return;

        VerticalLayoutGroup layout = optionListRoot.GetComponent<VerticalLayoutGroup>();
        if (layout == null)
            return;

        _baseLayoutPaddingLeft = layout.padding.left;
        _baseLayoutPaddingRight = layout.padding.right;
        _baseLayoutPaddingTop = layout.padding.top;
        _baseLayoutPaddingBottom = layout.padding.bottom;
        _layoutPaddingCaptured = true;
    }

    private void ApplyOptionListLayoutSettings()
    {
        if (optionListRoot == null)
            return;

        VerticalLayoutGroup layout = optionListRoot.GetComponent<VerticalLayoutGroup>();
        if (layout == null)
            return;

        CaptureBaseLayoutPadding();

        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.spacing = Mathf.Round(ScaledFloat(optionSpacing.y));

        RectTransform panelRt = transform as RectTransform;
        if (panelRt == null)
            return;

        float panelWidth = panelRt.rect.width;
        float panelHeight = panelRt.rect.height;

        if (panelWidth <= 0f || panelHeight <= 0f)
            return;

        float blockWidth = GetVisualBlockWidth();
        float blockHeight = GetVisualBlockHeight();

        int centeredLeft = Mathf.RoundToInt((panelWidth * blockCenterX) - (blockWidth * 0.5f));
        int centeredTop = Mathf.RoundToInt((panelHeight * (1f - blockCenterY)) - (blockHeight * 0.5f));

        int finalLeft = Mathf.Max(0, _baseLayoutPaddingLeft + centeredLeft + Mathf.RoundToInt(ScaledFloat(optionContentOffsetX)));
        int finalTop = Mathf.Max(0, _baseLayoutPaddingTop + centeredTop + Mathf.RoundToInt(ScaledFloat(optionContentOffsetY)));

        layout.padding.left = finalLeft;
        layout.padding.right = _baseLayoutPaddingRight;
        layout.padding.top = finalTop;
        layout.padding.bottom = _baseLayoutPaddingBottom;
    }

    private float GetVisualBlockWidth()
    {
        float maxTextWidth = GetMaxTextWidth();
        return Mathf.Ceil(ScaledFloat(cursorReservedWidth) + maxTextWidth + ScaledFloat(extraBlockWidth));
    }

    private float GetVisualBlockHeight()
    {
        float itemHeight = Mathf.Round(ScaledFloat(optionItemHeight));
        float spacingY = Mathf.Round(ScaledFloat(optionSpacing.y));
        float totalSpacing = Mathf.Max(0, entries.Count - 1) * spacingY;
        return Mathf.Ceil((entries.Count * itemHeight) + totalSpacing + ScaledFloat(extraBlockHeight));
    }

    private float GetMaxTextWidth()
    {
        TMP_Text measureTarget = optionItemPrefab != null ? optionItemPrefab : null;
        if (measureTarget == null)
            return 0f;

        if (optionFontAsset != null)
            measureTarget.font = optionFontAsset;

        float scaledSize = ScaledFont(fontSize);
        measureTarget.fontSize = scaledSize;
        measureTarget.enableAutoSizing = false;

        if (forceBold)
            measureTarget.fontStyle |= FontStyles.Bold;
        else
            measureTarget.fontStyle &= ~FontStyles.Bold;

        float max = 0f;

        for (int i = 0; i < entries.Count; i++)
        {
            Vector2 preferred = measureTarget.GetPreferredValues(entries[i]);
            if (preferred.x > max)
                max = preferred.x;
        }

        return Mathf.Ceil(max);
    }

    private void ApplyCursorScale()
    {
        if (cursorRenderer == null)
            return;

        RectTransform cursorRt = cursorRenderer.transform as RectTransform;
        if (cursorRt == null)
            return;

        if (!_cursorBaseSizeCaptured)
        {
            _cursorBaseSizeDelta = cursorRt.sizeDelta;
            _cursorBaseSizeCaptured = true;
        }

        float targetHeight = Mathf.Round(ScaledFloat(optionItemHeight));
        float targetSize = Mathf.Max(targetHeight * cursorHeightMultiplier, minCursorSize);

        float baseAspect = _cursorBaseSizeDelta.y > 0f
            ? _cursorBaseSizeDelta.x / _cursorBaseSizeDelta.y
            : 1f;

        cursorRt.sizeDelta = new Vector2(
            Mathf.Round(targetSize * baseAspect),
            Mathf.Round(targetSize)
        );

        cursorRt.localScale = Vector3.one;
    }

    private void UpdateCursorAnimationState(bool confirmed)
    {
        if (cursorRenderer == null)
            return;

        if (!_cursorAnimationStateCaptured)
        {
            _cursorBaseIdle = cursorRenderer.idle;
            _cursorBaseLoop = cursorRenderer.loop;
            _cursorAnimationStateCaptured = true;
        }

        if (_cursorAnimatingConfirmed == confirmed)
            return;

        _cursorAnimatingConfirmed = confirmed;

        if (confirmed)
        {
            cursorRenderer.idle = false;
            cursorRenderer.loop = true;
            cursorRenderer.CurrentFrame = 0;
            cursorRenderer.RefreshFrame();
        }
        else
        {
            cursorRenderer.idle = _cursorBaseIdle;
            cursorRenderer.loop = _cursorBaseLoop;
            cursorRenderer.CurrentFrame = 0;
            cursorRenderer.RefreshFrame();
        }
    }

    private int ScaledFont(int baseSize)
    {
        return Mathf.Clamp(Mathf.RoundToInt(baseSize * _currentUiScale), 8, 300);
    }

    private float ScaledFloat(float baseValue)
    {
        return baseValue * _currentUiScale;
    }

    private static void TrySetFloat(Material mat, string prop, float value)
    {
        if (mat != null && mat.HasProperty(prop))
            mat.SetFloat(prop, value);
    }

    private static void TrySetColor(Material mat, string prop, Color value)
    {
        if (mat != null && mat.HasProperty(prop))
            mat.SetColor(prop, value);
    }
}
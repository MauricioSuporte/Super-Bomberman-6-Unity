using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BossRushLeftPanel : MonoBehaviour
{
    const string LOG = "[BossRushLeftPanel]";

    [Header("Debug (Surgical Logs)")]
    [SerializeField] bool enableSurgicalLogs = true;
    [SerializeField] bool dumpDifficultyLayoutEveryUpdate = false;

    [Header("Difficulty List")]
    [SerializeField] RectTransform difficultyListRoot;
    [SerializeField] TextMeshProUGUI difficultyItemPrefab;
    [SerializeField] Color difficultyNormalColor = Color.white;
    [SerializeField] int fontSize = 18;
    [SerializeField] float difficultyItemHeight = 32f;
    [SerializeField] Vector2 difficultySpacing = new Vector2(0f, 10f);
    [SerializeField] float difficultyContentOffsetX = 32f;
    [SerializeField] float difficultyContentOffsetY = 0f;
    [SerializeField] float cursorExtraOffsetY = 0f;

    [Header("Selected Difficulty Colors")]
    [SerializeField] Color easySelectedColor = new Color32(56, 201, 54, 255);
    [SerializeField] Color normalSelectedColor = new Color32(45, 117, 255, 255);
    [SerializeField] Color hardSelectedColor = new Color32(231, 63, 63, 255);
    [SerializeField] Color nightmareSelectedColor = Color.black;

    [Header("Outline Colors")]
    [SerializeField] Color defaultOutlineColor = Color.black;
    [SerializeField] Color nightmareOutlineColor = new Color32(231, 63, 63, 255);

    [Header("Difficulty TMP")]
    [SerializeField] TMP_FontAsset difficultyFontAsset;
    [SerializeField] Material difficultyFontMaterialPreset;
    [SerializeField] bool forceDifficultyBold = true;
    [SerializeField] bool difficultyAutoSize = false;
    [SerializeField, Range(0.25f, 1f)] float difficultyAutoSizeMinRatio = 0.75f;

    [Header("Difficulty TMP Outline")]
    [SerializeField] bool useDifficultyOutline = true;
    [SerializeField, Range(0f, 1f)] float difficultyOutlineWidth = 0.35f;
    [SerializeField, Range(0f, 1f)] float difficultyOutlineSoftness = 0f;

    [Header("Difficulty TMP Face")]
    [SerializeField, Range(-1f, 1f)] float difficultyFaceDilate = 0.2f;
    [SerializeField, Range(0f, 1f)] float difficultyFaceSoftness = 0f;

    [Header("Difficulty TMP Underlay")]
    [SerializeField] bool enableDifficultyUnderlay = true;
    [SerializeField] Color difficultyUnderlayColor = new Color(0f, 0f, 0f, 1f);
    [SerializeField, Range(-1f, 1f)] float difficultyUnderlayDilate = 0.1f;
    [SerializeField, Range(0f, 1f)] float difficultyUnderlaySoftness = 0f;
    [SerializeField, Range(-2f, 2f)] float difficultyUnderlayOffsetX = 0.25f;
    [SerializeField, Range(-2f, 2f)] float difficultyUnderlayOffsetY = -0.25f;

    [Header("Cursor")]
    [SerializeField] AnimatedSpriteRenderer cursorRenderer;
    [SerializeField] Vector2 cursorOffset = new Vector2(-28f, 0f);
    [SerializeField] bool scaleCursorWithUi = true;
    [SerializeField] bool roundCursorToWholePixels = true;
    [SerializeField] float cursorHeightMultiplier = 1.5f;
    [SerializeField] float minCursorSize = 24f;
    bool _cursorBaseIdle = true;
    bool _cursorBaseLoop = true;
    bool _cursorAnimationStateCaptured;
    bool _cursorAnimatingConfirmed;

    readonly List<TextMeshProUGUI> difficultyTexts = new();
    readonly Dictionary<TMP_Text, Material> runtimeDifficultyMaterials = new();
    readonly List<BossRushDifficulty> difficulties = new()
    {
        BossRushDifficulty.EASY,
        BossRushDifficulty.NORMAL,
        BossRushDifficulty.HARD,
        BossRushDifficulty.NIGHTMARE
    };

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

    float _currentUiScale = 1f;

    Vector2 _cursorBaseSizeDelta = new Vector2(16f, 16f);
    bool _cursorBaseSizeCaptured;

    int _baseLayoutPaddingLeft;
    int _baseLayoutPaddingRight;
    int _baseLayoutPaddingTop;
    int _baseLayoutPaddingBottom;
    bool _layoutPaddingCaptured;

    public IReadOnlyList<BossRushDifficulty> Difficulties => difficulties;
    public BossRushDifficulty GetDifficultyAt(int index) => difficulties[Mathf.Clamp(index, 0, difficulties.Count - 1)];
    public int Count => difficulties.Count;

    int ScaledFont(int baseSize) => Mathf.Clamp(Mathf.RoundToInt(baseSize * _currentUiScale), 8, 300);
    float ScaledFloat(float baseValue) => baseValue * _currentUiScale;

    void Awake()
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

    void OnDestroy()
    {
        foreach (var kv in runtimeDifficultyMaterials)
        {
            if (kv.Value != null)
                Destroy(kv.Value);
        }

        runtimeDifficultyMaterials.Clear();
    }

    void Update()
    {
        if (dumpDifficultyLayoutEveryUpdate)
            DumpDifficultyState("Update");
    }

    public void Initialize(float uiScale)
    {
        _currentUiScale = uiScale;
        CaptureBaseLayoutPadding();
        ApplyCursorScale();
        ApplyDifficultyListLayoutSettings();
        BuildDifficultyList();
    }

    public void SetUiScale(float uiScale)
    {
        _currentUiScale = uiScale;
        ApplyCursorScale();
        ApplyDifficultyListLayoutSettings();

        if (difficultyTexts.Count > 0)
        {
            for (int i = 0; i < difficultyTexts.Count; i++)
            {
                if (difficultyTexts[i] == null)
                    continue;

                DifficultyVisualStyle style = GetUnselectedStyle();
                ApplyDifficultyTextStyle(difficultyTexts[i], style);

                RectTransform rt = difficultyTexts[i].rectTransform;
                rt.sizeDelta = new Vector2(0f, ScaledFloat(difficultyItemHeight));

                LayoutElement le = difficultyTexts[i].GetComponent<LayoutElement>();
                if (le != null)
                {
                    le.minHeight = ScaledFloat(difficultyItemHeight);
                    le.preferredHeight = ScaledFloat(difficultyItemHeight);
                }
            }

            VerticalLayoutGroup layout = difficultyListRoot != null ? difficultyListRoot.GetComponent<VerticalLayoutGroup>() : null;
            if (layout != null)
                layout.spacing = ScaledFloat(difficultySpacing.y);

            Canvas.ForceUpdateCanvases();
            if (difficultyListRoot != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(difficultyListRoot);
        }
    }

    public void BuildDifficultyList()
    {
        difficultyTexts.Clear();

        if (difficultyListRoot == null || difficultyItemPrefab == null)
        {
            SLog($"BuildDifficultyList ABORT | difficultyListRoot={(difficultyListRoot == null ? "NULL" : difficultyListRoot.name)} difficultyItemPrefab={(difficultyItemPrefab == null ? "NULL" : difficultyItemPrefab.name)}");
            return;
        }

        SLog($"BuildDifficultyList START | root={difficultyListRoot.name} childCount(before)={difficultyListRoot.childCount} prefabActiveSelf={difficultyItemPrefab.gameObject.activeSelf}");

        ApplyDifficultyListLayoutSettings();

        VerticalLayoutGroup layout = difficultyListRoot.GetComponent<VerticalLayoutGroup>();
        if (layout != null)
            SLog($"BuildDifficultyList | VerticalLayoutGroup found spacing={layout.spacing} childAlignment={layout.childAlignment} paddingLeft={layout.padding.left}");
        else
            SLog("BuildDifficultyList | VerticalLayoutGroup NOT FOUND on difficultyListRoot");

        for (int i = difficultyListRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = difficultyListRoot.GetChild(i);
            if (child == null)
                continue;

            if (child == difficultyItemPrefab.transform)
                continue;

            if (cursorRenderer != null && child == cursorRenderer.transform)
                continue;

            SLog($"BuildDifficultyList | Destroy old child='{child.name}' index={i}");
            Destroy(child.gameObject);
        }

        difficultyItemPrefab.gameObject.SetActive(false);

        for (int i = 0; i < difficulties.Count; i++)
        {
            TextMeshProUGUI txt = Instantiate(difficultyItemPrefab, difficultyListRoot);
            txt.gameObject.SetActive(true);
            txt.enabled = true;
            txt.text = GetDifficultyDisplayName(difficulties[i]);
            txt.transform.SetAsLastSibling();

            ApplyDifficultyTextStyle(txt, GetUnselectedStyle());

            RectTransform rt = txt.rectTransform;
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, ScaledFloat(difficultyItemHeight));
            rt.offsetMin = new Vector2(0f, 0f);
            rt.offsetMax = new Vector2(0f, 0f);

            LayoutElement le = txt.GetComponent<LayoutElement>();
            if (le == null)
                le = txt.gameObject.AddComponent<LayoutElement>();

            le.ignoreLayout = false;
            le.minHeight = ScaledFloat(difficultyItemHeight);
            le.preferredHeight = ScaledFloat(difficultyItemHeight);
            le.flexibleHeight = 0f;
            le.minWidth = 0f;
            le.preferredWidth = 0f;
            le.flexibleWidth = 1f;

            difficultyTexts.Add(txt);

            SLog(
                $"BuildDifficultyList | Created index={i} name='{txt.name}' text='{txt.text}' " +
                $"activeSelf={txt.gameObject.activeSelf} activeInHierarchy={txt.gameObject.activeInHierarchy} " +
                $"parent='{txt.transform.parent.name}' siblingIndex={txt.transform.GetSiblingIndex()} " +
                $"anchoredPos={rt.anchoredPosition} sizeDelta={rt.sizeDelta} rect={rt.rect} " +
                $"anchorMin={rt.anchorMin} anchorMax={rt.anchorMax} pivot={rt.pivot} " +
                $"color={txt.color} font={(txt.font != null ? txt.font.name : "NULL")} fontSize={txt.fontSize}"
            );
        }

        if (cursorRenderer != null)
            cursorRenderer.transform.SetAsLastSibling();

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(difficultyListRoot);
        DumpDifficultyState("BuildDifficultyList END");
    }

    public void UpdateDifficultyVisuals(int selectedIndex, bool confirmed)
    {
        for (int i = 0; i < difficultyTexts.Count; i++)
        {
            TextMeshProUGUI txt = difficultyTexts[i];
            if (txt == null)
            {
                SLog($"UpdateDifficultyVisuals | index={i} text=NULL");
                continue;
            }

            bool isSelected = i == selectedIndex;
            BossRushDifficulty difficulty = difficulties[i];

            txt.text = GetDifficultyDisplayName(difficulty);

            DifficultyVisualStyle style = isSelected
                ? GetSelectedStyle(difficulty)
                : GetUnselectedStyle();

            ApplyDifficultyTextStyle(txt, style);
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

        if (selectedIndex < 0 || selectedIndex >= difficultyTexts.Count)
        {
            cursorRenderer.gameObject.SetActive(false);
            return;
        }

        TextMeshProUGUI txt = difficultyTexts[selectedIndex];
        if (txt == null)
        {
            cursorRenderer.gameObject.SetActive(false);
            return;
        }

        cursorRenderer.gameObject.SetActive(true);

        Vector3 localPos = txt.rectTransform.localPosition;
        localPos.x += ScaledFloat(cursorOffset.x);
        localPos.y += ScaledFloat(cursorOffset.y) + ScaledFloat(cursorExtraOffsetY);
        localPos.z = 0f;

        if (roundCursorToWholePixels)
        {
            localPos.x = Mathf.Round(localPos.x);
            localPos.y = Mathf.Round(localPos.y);
        }

        cursorRenderer.SetExternalBaseLocalPosition(localPos);
    }

    public void DumpState(string context)
    {
        DumpDifficultyState(context);
    }

    public int GetScaledFontSize()
    {
        return ScaledFont(fontSize);
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

    DifficultyVisualStyle GetUnselectedStyle()
    {
        return new DifficultyVisualStyle(difficultyNormalColor, defaultOutlineColor);
    }

    DifficultyVisualStyle GetSelectedStyle(BossRushDifficulty difficulty)
    {
        switch (difficulty)
        {
            case BossRushDifficulty.EASY:
                return new DifficultyVisualStyle(easySelectedColor, defaultOutlineColor);

            case BossRushDifficulty.NORMAL:
                return new DifficultyVisualStyle(normalSelectedColor, defaultOutlineColor);

            case BossRushDifficulty.HARD:
                return new DifficultyVisualStyle(hardSelectedColor, defaultOutlineColor);

            case BossRushDifficulty.NIGHTMARE:
                return new DifficultyVisualStyle(nightmareSelectedColor, nightmareOutlineColor);

            default:
                return new DifficultyVisualStyle(difficultyNormalColor, defaultOutlineColor);
        }
    }

    void ApplyDifficultyTextStyle(TextMeshProUGUI txt, DifficultyVisualStyle style)
    {
        if (txt == null)
            return;

        if (difficultyFontAsset != null)
            txt.font = difficultyFontAsset;

        txt.textWrappingMode = TextWrappingModes.NoWrap;
        txt.overflowMode = TextOverflowModes.Overflow;
        txt.enableAutoSizing = difficultyAutoSize;
        txt.extraPadding = true;
        txt.alignment = TextAlignmentOptions.MidlineLeft;
        txt.color = style.FaceColor;
        txt.margin = Vector4.zero;

        float scaledSize = ScaledFont(fontSize);
        txt.fontSize = scaledSize;

        if (difficultyAutoSize)
        {
            txt.fontSizeMax = scaledSize;
            txt.fontSizeMin = Mathf.Max(8f, Mathf.Floor(scaledSize * difficultyAutoSizeMinRatio));
        }

        if (forceDifficultyBold)
            txt.fontStyle |= FontStyles.Bold;
        else
            txt.fontStyle &= ~FontStyles.Bold;

        Material runtimeMat = GetOrCreateRuntimeDifficultyMaterial(txt);
        ApplyDifficultyMaterialStyle(runtimeMat, style);

        txt.fontMaterial = runtimeMat;
        txt.UpdateMeshPadding();
        txt.ForceMeshUpdate();
        txt.SetVerticesDirty();
    }

    Material GetOrCreateRuntimeDifficultyMaterial(TMP_Text target)
    {
        if (target == null)
            return null;

        if (runtimeDifficultyMaterials.TryGetValue(target, out Material runtimeMat) && runtimeMat != null)
            return runtimeMat;

        Material baseMat = null;

        if (difficultyFontMaterialPreset != null)
            baseMat = difficultyFontMaterialPreset;
        else if (target.fontSharedMaterial != null)
            baseMat = target.fontSharedMaterial;
        else if (target.font != null)
            baseMat = target.font.material;

        if (baseMat == null)
            return null;

        runtimeMat = new Material(baseMat);
        runtimeMat.name = baseMat.name + "_BossRushLeftRuntime";
        runtimeDifficultyMaterials[target] = runtimeMat;
        return runtimeMat;
    }

    void ApplyDifficultyMaterialStyle(Material mat, DifficultyVisualStyle style)
    {
        if (mat == null)
            return;

        TrySetColor(mat, "_FaceColor", style.FaceColor);

        if (useDifficultyOutline)
        {
            TrySetColor(mat, "_OutlineColor", style.OutlineColor);
            TrySetFloat(mat, "_OutlineWidth", difficultyOutlineWidth);
            TrySetFloat(mat, "_OutlineSoftness", difficultyOutlineSoftness);
        }
        else
        {
            TrySetFloat(mat, "_OutlineWidth", 0f);
            TrySetFloat(mat, "_OutlineSoftness", 0f);
        }

        TrySetFloat(mat, "_FaceDilate", difficultyFaceDilate);
        TrySetFloat(mat, "_FaceSoftness", difficultyFaceSoftness);

        if (enableDifficultyUnderlay)
        {
            TrySetColor(mat, "_UnderlayColor", difficultyUnderlayColor);
            TrySetFloat(mat, "_UnderlayDilate", difficultyUnderlayDilate);
            TrySetFloat(mat, "_UnderlaySoftness", difficultyUnderlaySoftness);
            TrySetFloat(mat, "_UnderlayOffsetX", difficultyUnderlayOffsetX);
            TrySetFloat(mat, "_UnderlayOffsetY", difficultyUnderlayOffsetY);
        }
        else
        {
            TrySetFloat(mat, "_UnderlayDilate", 0f);
            TrySetFloat(mat, "_UnderlaySoftness", 0f);
            TrySetFloat(mat, "_UnderlayOffsetX", 0f);
            TrySetFloat(mat, "_UnderlayOffsetY", 0f);
        }
    }

    void CaptureBaseLayoutPadding()
    {
        if (_layoutPaddingCaptured || difficultyListRoot == null)
            return;

        VerticalLayoutGroup layout = difficultyListRoot.GetComponent<VerticalLayoutGroup>();
        if (layout == null)
            return;

        _baseLayoutPaddingLeft = layout.padding.left;
        _baseLayoutPaddingRight = layout.padding.right;
        _baseLayoutPaddingTop = layout.padding.top;
        _baseLayoutPaddingBottom = layout.padding.bottom;
        _layoutPaddingCaptured = true;
    }

    void ApplyDifficultyListLayoutSettings()
    {
        if (difficultyListRoot == null)
            return;

        VerticalLayoutGroup layout = difficultyListRoot.GetComponent<VerticalLayoutGroup>();
        if (layout == null)
            return;

        CaptureBaseLayoutPadding();

        layout.spacing = ScaledFloat(difficultySpacing.y);
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        layout.childControlHeight = true;
        layout.childControlWidth = true;

        layout.padding.left = _baseLayoutPaddingLeft + Mathf.RoundToInt(ScaledFloat(difficultyContentOffsetX));
        layout.padding.right = _baseLayoutPaddingRight;
        layout.padding.top = _baseLayoutPaddingTop + Mathf.RoundToInt(ScaledFloat(difficultyContentOffsetY));
        layout.padding.bottom = _baseLayoutPaddingBottom;
    }

    void ApplyCursorScale()
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

        float targetHeight = ScaledFloat(difficultyItemHeight);
        float targetSize = Mathf.Max(targetHeight * cursorHeightMultiplier, minCursorSize);

        float baseAspect = _cursorBaseSizeDelta.y > 0f
            ? _cursorBaseSizeDelta.x / _cursorBaseSizeDelta.y
            : 1f;

        cursorRt.sizeDelta = new Vector2(
            Mathf.Round(targetSize * baseAspect),
            Mathf.Round(targetSize)
        );

        cursorRt.localScale = Vector3.one;

        SLog(
            $"ApplyCursorScale | " +
            $"uiScale={_currentUiScale:0.###} " +
            $"targetHeight={targetHeight:0.##} " +
            $"cursorHeightMultiplier={cursorHeightMultiplier:0.##} " +
            $"cursorSize={cursorRt.sizeDelta}"
        );
    }

    void DumpDifficultyState(string context)
    {
        if (!enableSurgicalLogs)
            return;

        if (difficultyListRoot == null)
        {
            Debug.Log($"{LOG} {context} | difficultyListRoot=NULL", this);
            return;
        }

        RectTransform rootRt = difficultyListRoot;
        Rect rootRect = rootRt.rect;
        VerticalLayoutGroup layout = difficultyListRoot.GetComponent<VerticalLayoutGroup>();

        Debug.Log(
            $"{LOG} {context} | " +
            $"difficultyListRoot='{difficultyListRoot.name}' activeSelf={difficultyListRoot.gameObject.activeSelf} activeInHierarchy={difficultyListRoot.gameObject.activeInHierarchy} " +
            $"childCount={difficultyListRoot.childCount} difficultyTexts.Count={difficultyTexts.Count} " +
            $"rect={rootRect} sizeDelta={rootRt.sizeDelta} anchoredPos={rootRt.anchoredPosition} " +
            $"anchorMin={rootRt.anchorMin} anchorMax={rootRt.anchorMax} pivot={rootRt.pivot} " +
            $"hasVerticalLayout={(layout != null)} uiScale={_currentUiScale:0.###}" +
            $"{(layout != null ? $" paddingLeft={layout.padding.left}" : "")}",
            this
        );

        for (int i = 0; i < difficultyTexts.Count; i++)
        {
            TextMeshProUGUI txt = difficultyTexts[i];
            if (txt == null)
            {
                Debug.Log($"{LOG} {context} | item[{i}] = NULL", this);
                continue;
            }

            GameObject go = txt.gameObject;
            RectTransform rt = txt.rectTransform;
            Color col = txt.color;

            Debug.Log(
                $"{LOG} {context} | item[{i}] name='{go.name}' text='{txt.text}' " +
                $"activeSelf={go.activeSelf} activeInHierarchy={go.activeInHierarchy} enabled={txt.enabled} " +
                $"rect={rt.rect} sizeDelta={rt.sizeDelta} anchoredPos={rt.anchoredPosition} localPos={rt.localPosition} " +
                $"anchorMin={rt.anchorMin} anchorMax={rt.anchorMax} pivot={rt.pivot} siblingIndex={go.transform.GetSiblingIndex()} " +
                $"color=({col.r:0.###},{col.g:0.###},{col.b:0.###},{col.a:0.###}) " +
                $"font={(txt.font != null ? txt.font.name : "NULL")} fontSize={txt.fontSize} " +
                $"canvasRendererCull={txt.canvasRenderer.cull}",
                this
            );
        }
    }

    void UpdateCursorAnimationState(bool confirmed)
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

            SLog("UpdateCursorAnimationState | confirmed=True -> cursor animating in loop");
        }
        else
        {
            cursorRenderer.idle = _cursorBaseIdle;
            cursorRenderer.loop = _cursorBaseLoop;
            cursorRenderer.CurrentFrame = 0;
            cursorRenderer.RefreshFrame();

            SLog("UpdateCursorAnimationState | confirmed=False -> cursor restored to base state");
        }
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
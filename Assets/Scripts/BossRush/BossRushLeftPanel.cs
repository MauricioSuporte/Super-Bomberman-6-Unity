using System.Collections.Generic;
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
    [SerializeField] Text difficultyItemPrefab;
    [SerializeField] Color difficultyNormalColor = Color.white;
    [SerializeField] Color difficultySelectedColor = Color.yellow;
    [SerializeField] Color difficultyConfirmedColor = new Color(1f, 0.8f, 0.2f, 1f);
    [SerializeField] int fontSize = 18;
    [SerializeField] float difficultyItemHeight = 32f;
    [SerializeField] Vector2 difficultySpacing = new Vector2(0f, 10f);
    [SerializeField] float difficultyContentOffsetX = 32f;

    [Header("Cursor")]
    [SerializeField] AnimatedSpriteRenderer cursorRenderer;
    [SerializeField] Vector2 cursorOffset = new Vector2(-28f, 0f);
    [SerializeField] bool scaleCursorWithUi = true;
    [SerializeField] bool roundCursorToWholePixels = true;

    readonly List<Text> difficultyTexts = new();
    readonly List<BossRushDifficulty> difficulties = new()
    {
        BossRushDifficulty.Easy,
        BossRushDifficulty.Normal,
        BossRushDifficulty.Hard,
        BossRushDifficulty.Nightmare
    };

    float _currentUiScale = 1f;

    Vector3 _cursorBaseLocalScale = Vector3.one;
    bool _cursorBaseScaleCaptured;

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
            _cursorBaseLocalScale = cursorRenderer.transform.localScale;
            _cursorBaseScaleCaptured = true;
            cursorRenderer.gameObject.SetActive(false);
        }

        CaptureBaseLayoutPadding();
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

                difficultyTexts[i].fontSize = ScaledFont(fontSize);

                var rt = difficultyTexts[i].rectTransform;
                rt.sizeDelta = new Vector2(0f, ScaledFloat(difficultyItemHeight));

                var le = difficultyTexts[i].GetComponent<LayoutElement>();
                if (le != null)
                {
                    le.minHeight = ScaledFloat(difficultyItemHeight);
                    le.preferredHeight = ScaledFloat(difficultyItemHeight);
                }
            }

            var layout = difficultyListRoot != null ? difficultyListRoot.GetComponent<VerticalLayoutGroup>() : null;
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

        var layout = difficultyListRoot.GetComponent<VerticalLayoutGroup>();
        if (layout != null)
            SLog($"BuildDifficultyList | VerticalLayoutGroup found spacing={layout.spacing} childAlignment={layout.childAlignment} paddingLeft={layout.padding.left}");
        else
            SLog("BuildDifficultyList | VerticalLayoutGroup NOT FOUND on difficultyListRoot");

        for (int i = difficultyListRoot.childCount - 1; i >= 0; i--)
        {
            var child = difficultyListRoot.GetChild(i);
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
            var txt = Instantiate(difficultyItemPrefab, difficultyListRoot);
            txt.gameObject.SetActive(true);
            txt.enabled = true;
            txt.text = GetDifficultyDisplayName(difficulties[i]);
            txt.fontSize = ScaledFont(fontSize);
            txt.color = difficultyNormalColor;
            txt.transform.SetAsLastSibling();
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            txt.alignment = TextAnchor.MiddleLeft;
            txt.resizeTextForBestFit = false;

            var rt = txt.rectTransform;
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, ScaledFloat(difficultyItemHeight));
            rt.offsetMin = new Vector2(0f, 0f);
            rt.offsetMax = new Vector2(0f, 0f);

            var le = txt.GetComponent<LayoutElement>();
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
            var txt = difficultyTexts[i];
            if (txt == null)
            {
                SLog($"UpdateDifficultyVisuals | index={i} text=NULL");
                continue;
            }

            txt.fontSize = ScaledFont(fontSize);

            bool isSelected = i == selectedIndex;

            txt.text = GetDifficultyDisplayName(difficulties[i]);
            txt.color = confirmed && isSelected
                ? difficultyConfirmedColor
                : isSelected
                    ? difficultySelectedColor
                    : difficultyNormalColor;
        }

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
            cursorRenderer.gameObject.SetActive(false);
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

        var txt = difficultyTexts[selectedIndex];
        if (txt == null)
        {
            cursorRenderer.gameObject.SetActive(false);
            return;
        }

        cursorRenderer.gameObject.SetActive(true);

        Vector3 localPos = txt.rectTransform.localPosition;
        localPos.x += ScaledFloat(cursorOffset.x);
        localPos.y += ScaledFloat(cursorOffset.y);
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
            case BossRushDifficulty.Easy: return "Easy";
            case BossRushDifficulty.Normal: return "Normal";
            case BossRushDifficulty.Hard: return "Hard";
            case BossRushDifficulty.Nightmare: return "Nightmare";
            default: return difficulty.ToString();
        }
    }

    void CaptureBaseLayoutPadding()
    {
        if (_layoutPaddingCaptured || difficultyListRoot == null)
            return;

        var layout = difficultyListRoot.GetComponent<VerticalLayoutGroup>();
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

        var layout = difficultyListRoot.GetComponent<VerticalLayoutGroup>();
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
        layout.padding.top = _baseLayoutPaddingTop;
        layout.padding.bottom = _baseLayoutPaddingBottom;
    }

    void ApplyCursorScale()
    {
        if (!scaleCursorWithUi || cursorRenderer == null)
            return;

        if (!_cursorBaseScaleCaptured)
        {
            _cursorBaseLocalScale = cursorRenderer.transform.localScale;
            _cursorBaseScaleCaptured = true;
        }

        float s = _currentUiScale;
        cursorRenderer.transform.localScale = new Vector3(
            _cursorBaseLocalScale.x * s,
            _cursorBaseLocalScale.y * s,
            _cursorBaseLocalScale.z
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

        var rootRt = difficultyListRoot;
        var rootRect = rootRt.rect;
        var layout = difficultyListRoot.GetComponent<VerticalLayoutGroup>();

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
            var txt = difficultyTexts[i];
            if (txt == null)
            {
                Debug.Log($"{LOG} {context} | item[{i}] = NULL", this);
                continue;
            }

            var go = txt.gameObject;
            var rt = txt.rectTransform;
            var col = txt.color;

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

    void SLog(string message)
    {
        if (!enableSurgicalLogs)
            return;

        Debug.Log($"{LOG} {message}", this);
    }
}
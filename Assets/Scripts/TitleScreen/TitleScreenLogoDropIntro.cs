using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class TitleScreenLogoDropIntro : MonoBehaviour
{
    const string LOG = "[TitleScreenLogoDropIntro]";

    [Header("References")]
    [SerializeField] Image logoImage;
    [SerializeField] Sprite logoSprite;

    [Header("Layout")]
    [SerializeField] RectTransform layoutRoot;
    [SerializeField] bool useLayoutRootAsParent = true;

    [Header("Logo Size (base reference pixels 256x224)")]
    [SerializeField] Vector2 logoSize = new(233f, 69f);
    [SerializeField] bool useSpriteNativeSizeAsLogoSize = false;

    [Header("Final Position (anchored, top-center based)")]
    [SerializeField] Vector2 finalAnchoredPosition = new(0f, -12f);

    [Header("Start Offset")]
    [SerializeField] float startOffsetAboveScreen = 80f;

    [Header("Timing")]
    [SerializeField, Min(0.01f)] float dropDuration = 0.75f;
    [SerializeField] bool useUnscaledTime = true;
    [SerializeField] AnimationCurve dropCurve = null;

    [Header("Pixel Perfect")]
    [SerializeField] bool roundAnchoredPosition = true;
    [SerializeField] bool applyPointFilter = true;

    [Header("Debug (Surgical Logs)")]
    [SerializeField] bool enableSurgicalLogs = true;

    Coroutine currentRoutine;
    RectTransform _logoRect;
    float _uiScale = 1f;

    public bool IsPlaying => currentRoutine != null;

    public void SetPixelFrameScale(float scale)
    {
        _uiScale = Mathf.Max(0.01f, scale);
        ApplyCurrentScaledLayout();
        DumpLogoState("SetPixelFrameScale");
    }

    void Awake()
    {
        if (dropCurve == null || dropCurve.length == 0)
            dropCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        EnsureLogo();

        if (logoImage != null)
            logoImage.gameObject.SetActive(false);

        DumpLogoState("Awake");
    }

    public void SetLayoutRoot(RectTransform root)
    {
        layoutRoot = root;

        if (logoImage != null && useLayoutRootAsParent && layoutRoot != null)
            logoImage.transform.SetParent(layoutRoot, false);

        EnsureLogo();
        ApplyCurrentScaledLayout();
        DumpLogoState("SetLayoutRoot");
    }

    public void HideImmediate()
    {
        StopIntro();

        if (logoImage != null)
            logoImage.gameObject.SetActive(false);

        DumpLogoState("HideImmediate");
    }

    public void PrepareAboveTop()
    {
        if (!EnsureLogo())
            return;

        ApplySprite();

        logoImage.gameObject.SetActive(true);

        Vector2 startPos = GetStartAnchoredPosition();
        ApplyAnchoredPosition(startPos);

        if (enableSurgicalLogs)
        {
            Debug.Log(
                $"{LOG} PrepareAboveTop | start={startPos} | final={finalAnchoredPosition} | startOffsetAboveScreen={startOffsetAboveScreen}",
                this
            );
        }

        DumpLogoState("PrepareAboveTop");
    }

    public IEnumerator PlayIntro()
    {
        StopIntro();

        if (!EnsureLogo())
            yield break;

        ApplySprite();
        logoImage.gameObject.SetActive(true);

        Vector2 startPos = GetStartAnchoredPosition();
        Vector2 endPos = finalAnchoredPosition;

        ApplyAnchoredPosition(startPos);

        if (enableSurgicalLogs)
        {
            Debug.Log(
                $"{LOG} PlayIntro | start={startPos} | final={endPos} | duration={dropDuration} | uiScale={_uiScale:0.###}",
                this
            );
        }

        DumpLogoState("PlayIntro-BeforeRoutine");

        currentRoutine = StartCoroutine(PlayRoutine(startPos, endPos));
        yield return currentRoutine;
    }

    public void StopIntro()
    {
        if (currentRoutine != null)
        {
            StopCoroutine(currentRoutine);
            currentRoutine = null;
        }
    }

    IEnumerator PlayRoutine(Vector2 startPos, Vector2 endPos)
    {
        float duration = Mathf.Max(0.01f, dropDuration);
        float t = 0f;

        while (t < duration)
        {
            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            t += dt;

            float linear = Mathf.Clamp01(t / duration);
            float eased = dropCurve != null ? dropCurve.Evaluate(linear) : linear;

            Vector2 pos = Vector2.LerpUnclamped(startPos, endPos, eased);
            ApplyAnchoredPosition(pos);

            yield return null;
        }

        ApplyAnchoredPosition(endPos);
        currentRoutine = null;

        DumpLogoState("PlayRoutine-End");
    }

    bool EnsureLogo()
    {
        if (logoImage == null)
        {
            Transform found = transform.Find("TitleLogo");
            if (found != null)
                logoImage = found.GetComponent<Image>();
        }

        if (logoImage == null)
        {
            GameObject go = new("TitleLogo", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            logoImage = go.GetComponent<Image>();

            Transform parent = useLayoutRootAsParent && layoutRoot != null
                ? layoutRoot
                : transform;

            go.transform.SetParent(parent, false);
        }

        _logoRect = logoImage.rectTransform;

        Transform desiredParent = useLayoutRootAsParent && layoutRoot != null
            ? layoutRoot
            : transform;

        if (_logoRect.parent != desiredParent)
            _logoRect.SetParent(desiredParent, false);

        _logoRect.anchorMin = new Vector2(0.5f, 1f);
        _logoRect.anchorMax = new Vector2(0.5f, 1f);
        _logoRect.pivot = new Vector2(0.5f, 1f);
        _logoRect.localScale = Vector3.one;
        _logoRect.localRotation = Quaternion.identity;

        logoImage.preserveAspect = true;
        logoImage.raycastTarget = false;
        logoImage.enabled = true;

        ApplyCurrentScaledLayout();
        DumpLogoState("EnsureLogo");

        return logoImage != null;
    }

    void ApplySprite()
    {
        if (logoImage == null || logoSprite == null)
            return;

        Texture tex = logoSprite.texture;
        if (tex != null && applyPointFilter)
            tex.filterMode = FilterMode.Point;

        logoImage.sprite = logoSprite;

        ApplyCurrentScaledLayout();

        if (enableSurgicalLogs)
        {
            Rect spriteRect = logoSprite.textureRect;
            Debug.Log(
                $"{LOG} ApplySprite | spriteRect=({spriteRect.width}x{spriteRect.height}) | texture=({logoSprite.texture.width}x{logoSprite.texture.height}) | preserveAspect={logoImage.preserveAspect}",
                this
            );
        }

        DumpLogoState("ApplySprite");
    }

    Vector2 GetStartAnchoredPosition()
    {
        return new Vector2(finalAnchoredPosition.x, finalAnchoredPosition.y + startOffsetAboveScreen);
    }

    void ApplyAnchoredPosition(Vector2 pos)
    {
        if (_logoRect == null)
            return;

        if (roundAnchoredPosition)
        {
            pos.x = Mathf.Round(pos.x);
            pos.y = Mathf.Round(pos.y);
        }

        _logoRect.anchoredPosition = pos;
    }

    void ApplyCurrentScaledLayout()
    {
        if (_logoRect == null)
            return;

        Vector2 baseSize = GetEffectiveBaseLogoSize();

        Vector2 scaledSize = new Vector2(
            Mathf.Round(baseSize.x * _uiScale),
            Mathf.Round(baseSize.y * _uiScale)
        );

        _logoRect.sizeDelta = scaledSize;

        if (enableSurgicalLogs)
        {
            Debug.Log(
                $"{LOG} ApplyCurrentScaledLayout | baseSize={baseSize} | uiScale={_uiScale:0.###} | appliedSize={scaledSize}",
                this
            );
        }
    }

Vector2 GetEffectiveBaseLogoSize()
{
    if (useSpriteNativeSizeAsLogoSize && logoSprite != null)
    {
        if (logoSprite.texture != null)
            return new Vector2(logoSprite.texture.width, logoSprite.texture.height);

        Rect r = logoSprite.rect;
        return new Vector2(r.width, r.height);
    }

    return logoSize;
}

    void DumpLogoState(string context)
    {
        if (!enableSurgicalLogs)
            return;

        string spriteInfo = "sprite=NULL";
        if (logoSprite != null)
        {
            Rect sr = logoSprite.textureRect;
            Texture tex = logoSprite.texture;
            spriteInfo =
                $"spriteRect=({sr.width:0.###}x{sr.height:0.###}) " +
                $"spriteTex=({(tex != null ? tex.width : 0)}x{(tex != null ? tex.height : 0)})";
        }

        string rootInfo = "layoutRoot=NULL";
        if (layoutRoot != null)
        {
            rootInfo =
                $"layoutRoot={layoutRoot.name} " +
                $"rootRect=({layoutRoot.rect.width:0.###}x{layoutRoot.rect.height:0.###})";
        }

        string rectInfo = "logoRect=NULL";
        if (_logoRect != null)
        {
            rectInfo =
                $"rectSize=({_logoRect.rect.width:0.###}x{_logoRect.rect.height:0.###}) " +
                $"sizeDelta=({_logoRect.sizeDelta.x:0.###}x{_logoRect.sizeDelta.y:0.###}) " +
                $"anchored=({_logoRect.anchoredPosition.x:0.###},{_logoRect.anchoredPosition.y:0.###}) " +
                $"anchorMin=({_logoRect.anchorMin.x:0.###},{_logoRect.anchorMin.y:0.###}) " +
                $"anchorMax=({_logoRect.anchorMax.x:0.###},{_logoRect.anchorMax.y:0.###}) " +
                $"pivot=({_logoRect.pivot.x:0.###},{_logoRect.pivot.y:0.###})";
        }

        string parentInfo = "parent=NULL";
        if (_logoRect != null && _logoRect.parent != null)
            parentInfo = $"parent={_logoRect.parent.name}";

        Debug.Log(
            $"{LOG} {context} | {_uiScale:0.###=} | baseLogoSize={GetEffectiveBaseLogoSize()} | {spriteInfo} | {rootInfo} | {rectInfo} | {parentInfo}",
            this
        );
    }
}
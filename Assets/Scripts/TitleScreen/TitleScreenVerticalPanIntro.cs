using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class TitleScreenVerticalPanIntro : MonoBehaviour
{
    const string LOG = "[TitleScreenVerticalPanIntro]";

    [Header("References")]
    [SerializeField] RawImage targetRawImage;
    [SerializeField] Sprite sourceSprite;

    [Header("Visible Area In Texture Pixels")]
    [SerializeField, Min(1)] int visibleWidthPixels = 256;
    [SerializeField, Min(1)] int visibleHeightPixels = 224;

    [Header("Pan Source Texture Pixels")]
    [SerializeField, Min(0)] int startBottomPixel = 0;
    [SerializeField] bool autoComputeTopFromTexture = true;
    [SerializeField, Min(0)] int topBottomPixel = 110;

    [Header("Timing")]
    [SerializeField, Min(0.01f)] float introDuration = 3f;
    [SerializeField] bool useUnscaledTime = true;
    [SerializeField] AnimationCurve panCurve = null;

    [Header("Behavior")]
    [SerializeField] bool applyPointFilter = true;
    [SerializeField] bool clampWrapMode = true;
    [SerializeField] bool setNativeTextureOnPrepare = true;
    [SerializeField] bool snapToPixelPerfectUv = false;

    [Header("Debug (Surgical Logs)")]
    [SerializeField] bool enableSurgicalLogs = true;

    Coroutine currentRoutine;
    Texture runtimeTexture;

    public bool IsPlaying => currentRoutine != null;

    void Reset()
    {
        targetRawImage = GetComponent<RawImage>();
    }

    void Awake()
    {
        if (targetRawImage == null)
            targetRawImage = GetComponent<RawImage>();

        if (panCurve == null || panCurve.length == 0)
            panCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    }

    public void PrepareStaticBottomFrame()
    {
        if (!TryPrepareTexture(out Texture tex, out Rect spriteRect))
            return;

        Rect uv = BuildUvRect(
            spriteRect,
            visibleWidthPixels,
            visibleHeightPixels,
            startBottomPixel
        );

        ApplyUvRect(uv, tex);
    }

    public void PrepareStaticTopFrame()
    {
        if (!TryPrepareTexture(out Texture tex, out Rect spriteRect))
            return;

        int targetBottom = GetTargetTopBottomPixel(spriteRect);

        Rect uv = BuildUvRect(
            spriteRect,
            visibleWidthPixels,
            visibleHeightPixels,
            targetBottom
        );

        ApplyUvRect(uv, tex);
    }

    public IEnumerator PlayIntro()
    {
        StopIntro();

        if (!TryPrepareTexture(out Texture tex, out Rect spriteRect))
            yield break;

        int fromBottom = startBottomPixel;
        int toBottom = GetTargetTopBottomPixel(spriteRect);

        if (enableSurgicalLogs)
        {
            Debug.Log(
                $"{LOG} PlayIntro | tex=({tex.width}x{tex.height}) | spriteRect=({spriteRect.x},{spriteRect.y},{spriteRect.width},{spriteRect.height}) | fromBottom={fromBottom} | toBottom={toBottom} | visible=({visibleWidthPixels}x{visibleHeightPixels}) | duration={introDuration}",
                this
            );
        }

        currentRoutine = StartCoroutine(PlayRoutine(tex, spriteRect, fromBottom, toBottom));
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

    IEnumerator PlayRoutine(Texture tex, Rect spriteRect, int fromBottom, int toBottom)
    {
        float duration = Mathf.Max(0.01f, introDuration);
        float t = 0f;

        while (t < duration)
        {
            float delta = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            t += delta;

            float linear = Mathf.Clamp01(t / duration);
            float eased = panCurve != null ? panCurve.Evaluate(linear) : linear;

            float currentBottom = Mathf.Lerp(fromBottom, toBottom, eased);
            int bottomPx = snapToPixelPerfectUv
                ? Mathf.RoundToInt(currentBottom)
                : Mathf.FloorToInt(currentBottom);

            Rect uv = BuildUvRect(
                spriteRect,
                visibleWidthPixels,
                visibleHeightPixels,
                bottomPx
            );

            ApplyUvRect(uv, tex);
            yield return null;
        }

        Rect finalUv = BuildUvRect(
            spriteRect,
            visibleWidthPixels,
            visibleHeightPixels,
            toBottom
        );

        ApplyUvRect(finalUv, tex);
        currentRoutine = null;
    }

    bool TryPrepareTexture(out Texture tex, out Rect spriteRect)
    {
        tex = null;
        spriteRect = default;

        if (targetRawImage == null)
        {
            Debug.LogWarning($"{LOG} targetRawImage não foi atribuído.", this);
            return false;
        }

        if (sourceSprite == null)
        {
            Debug.LogWarning($"{LOG} sourceSprite não foi atribuído.", this);
            return false;
        }

        tex = sourceSprite.texture;
        if (tex == null)
        {
            Debug.LogWarning($"{LOG} sourceSprite.texture é null.", this);
            return false;
        }

        runtimeTexture = tex;
        spriteRect = sourceSprite.textureRect;

        if (applyPointFilter)
            tex.filterMode = FilterMode.Point;

        if (clampWrapMode)
            tex.wrapMode = TextureWrapMode.Clamp;

        if (setNativeTextureOnPrepare)
            targetRawImage.texture = tex;

        targetRawImage.uvRect = new Rect(0f, 0f, 1f, 1f);

        return true;
    }

    int GetTargetTopBottomPixel(Rect spriteRect)
    {
        if (!autoComputeTopFromTexture)
            return Mathf.Max(0, topBottomPixel);

        int spriteHeight = Mathf.RoundToInt(spriteRect.height);
        int maxBottom = Mathf.Max(0, spriteHeight - visibleHeightPixels);
        return maxBottom;
    }

    Rect BuildUvRect(Rect spriteRect, int visibleW, int visibleH, int bottomPixelWithinSprite)
    {
        float texW = Mathf.Max(1f, runtimeTexture.width);
        float texH = Mathf.Max(1f, runtimeTexture.height);

        float spriteX = spriteRect.x;
        float spriteY = spriteRect.y;
        float spriteW = spriteRect.width;
        float spriteH = spriteRect.height;

        float clampedVisibleW = Mathf.Clamp(visibleW, 1f, spriteW);
        float clampedVisibleH = Mathf.Clamp(visibleH, 1f, spriteH);

        float maxBottom = Mathf.Max(0f, spriteH - clampedVisibleH);
        float bottom = Mathf.Clamp(bottomPixelWithinSprite, 0f, maxBottom);

        float u = spriteX / texW;
        float v = (spriteY + bottom) / texH;
        float w = clampedVisibleW / texW;
        float h = clampedVisibleH / texH;

        return new Rect(u, v, w, h);
    }

    void ApplyUvRect(Rect uv, Texture tex)
    {
        if (targetRawImage == null)
            return;

        if (targetRawImage.texture != tex)
            targetRawImage.texture = tex;

        targetRawImage.uvRect = uv;
    }
}
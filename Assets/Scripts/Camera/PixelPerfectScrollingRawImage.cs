using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[RequireComponent(typeof(RawImage))]
public class PixelPerfectScrollingRawImage : MonoBehaviour
{
    [Header("References")]
    [SerializeField] RawImage targetImage;
    [SerializeField] RectTransform referenceRect;

    [Header("Texture Source")]
    [SerializeField] Texture textureOverride;
    [SerializeField] Sprite spriteOverride;

    [Header("Virtual Viewport (SNES)")]
    [SerializeField, Min(1)] int viewportWidth = 256;
    [SerializeField, Min(1)] int viewportHeight = 224;

    [Header("Scroll")]
    [SerializeField] bool scroll = true;
    [SerializeField] bool unscaledTime = true;
    [SerializeField] float scrollSpeedPixelsPerSecond = 16f;
    [SerializeField] ScrollDirection direction = ScrollDirection.LeftToRight;

    [Header("Pixel Perfect")]
    [SerializeField] bool snapUvToTexturePixels = true;
    [SerializeField] bool applySizeToReferenceRect = true;

    [Header("Debug")]
    [SerializeField] bool logChanges = false;

    float scrollPixels;
    int lastScreenW = -1;
    int lastScreenH = -1;
    Rect lastRefRect;
    Texture lastAppliedTexture;

    public enum ScrollDirection
    {
        LeftToRight,
        RightToLeft
    }

    void Reset()
    {
        targetImage = GetComponent<RawImage>();
    }

    void Awake()
    {
        EnsureRefs();
        ApplyTexture();
        Rebuild(forceLog: true);
    }

    void OnEnable()
    {
        EnsureRefs();
        ApplyTexture();
        Rebuild(forceLog: true);
    }

    void LateUpdate()
    {
        EnsureRefs();
        ApplyTexture();

        bool sizeChanged = false;

        if (referenceRect != null)
        {
            Rect rr = referenceRect.rect;
            if (!ApproximatelyRect(rr, lastRefRect))
            {
                lastRefRect = rr;
                sizeChanged = true;
            }
        }

        if (Screen.width != lastScreenW || Screen.height != lastScreenH)
        {
            lastScreenW = Screen.width;
            lastScreenH = Screen.height;
            sizeChanged = true;
        }

        if (targetImage != null && targetImage.texture != lastAppliedTexture)
        {
            lastAppliedTexture = targetImage.texture;
            sizeChanged = true;
        }

        if (Application.isPlaying && scroll)
        {
            float dt = unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float dir = direction == ScrollDirection.LeftToRight ? -1f : 1f;
            scrollPixels += scrollSpeedPixelsPerSecond * dt * dir;
        }

        if (sizeChanged || (Application.isPlaying && scroll) || !Application.isPlaying)
            Rebuild(forceLog: false);
    }

    void EnsureRefs()
    {
        if (targetImage == null)
            targetImage = GetComponent<RawImage>();
    }

    void ApplyTexture()
    {
        if (targetImage == null)
            return;

        Texture tex = null;

        if (textureOverride != null)
            tex = textureOverride;
        else if (spriteOverride != null)
            tex = spriteOverride.texture;

        if (tex != null && targetImage.texture != tex)
            targetImage.texture = tex;
    }

    void Rebuild(bool forceLog)
    {
        if (targetImage == null || targetImage.texture == null)
            return;

        Texture tex = targetImage.texture;

        int texW = Mathf.Max(1, tex.width);
        int texH = Mathf.Max(1, tex.height);

        if (applySizeToReferenceRect && referenceRect != null)
        {
            RectTransform rt = targetImage.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
            rt.localScale = Vector3.one;
        }

        float visibleU = viewportWidth / (float)texW;
        float visibleV = viewportHeight / (float)texH;

        visibleU = Mathf.Clamp01(visibleU);
        visibleV = Mathf.Clamp01(visibleV);

        float pixelOffsetX = scrollPixels;

        if (snapUvToTexturePixels)
            pixelOffsetX = Mathf.Round(pixelOffsetX);

        float uX = texW > 0 ? pixelOffsetX / texW : 0f;

        if (uX > 1f || uX < -1f)
            uX = Mathf.Repeat(uX, 1f);

        targetImage.uvRect = new Rect(uX, 0f, visibleU, visibleV);
    }

    static bool ApproximatelyRect(Rect a, Rect b)
    {
        return
            Mathf.Abs(a.x - b.x) < 0.01f &&
            Mathf.Abs(a.y - b.y) < 0.01f &&
            Mathf.Abs(a.width - b.width) < 0.01f &&
            Mathf.Abs(a.height - b.height) < 0.01f;
    }
}
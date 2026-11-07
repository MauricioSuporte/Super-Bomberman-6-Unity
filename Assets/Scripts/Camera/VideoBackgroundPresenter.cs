using System.Collections;
using UnityEngine;

/// <summary>Paints only the unused output area; the game and its safe-frame UI stay untouched.</summary>
[RequireComponent(typeof(Camera))]
public sealed class VideoBackgroundPresenter : MonoBehaviour
{
    Camera targetCamera;
    Texture2D image;
    Texture2D patternBase;
    int loadedBackground = -1;
    RenderTexture capture;
    RenderTexture cropped;
    RenderTexture blurred;
    Material blurMaterial;
    Coroutine captureRoutine;
    readonly WaitForEndOfFrame endOfFrame = new WaitForEndOfFrame();

    void Awake() => targetCamera = GetComponent<Camera>();

    void OnEnable() => captureRoutine = StartCoroutine(CaptureBackground());

    void OnDisable()
    {
        if (captureRoutine != null)
            StopCoroutine(captureRoutine);
        captureRoutine = null;
        ReleaseBuffers();
        if (blurMaterial != null)
            Destroy(blurMaterial);
        blurMaterial = null;
        if (image != null)
            Resources.UnloadAsset(image);
        image = null;
        if (patternBase != null)
            Resources.UnloadAsset(patternBase);
        patternBase = null;
        loadedBackground = -1;
    }

    bool OwnsOutput => targetCamera != null && targetCamera == Camera.main && targetCamera.targetTexture == null;

    bool HasBars(Rect viewport) => viewport.width < Screen.width || viewport.height < Screen.height;

    IEnumerator CaptureBackground()
    {
        while (true)
        {
            yield return endOfFrame;
            if (!OwnsOutput || SaveSystem.GetVideoSettings().background != 1)
            {
                ReleaseBuffers();
                continue;
            }

            Rect viewport = PixelPerfectViewport.ResolveFinalPixelRect(targetCamera);
            if (!HasBars(viewport))
            {
                ReleaseBuffers();
                continue;
            }

            if (blurMaterial == null)
            {
                Shader shader = Resources.Load<Shader>("VideoBackgroundBlur");
                if (shader == null || !shader.isSupported)
                    continue;
                blurMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            }

            EnsureBuffers();
            // Capture after UI. Sampling strictly inside the game rectangle prevents feedback
            // from the background painted during the preceding frame.
            ScreenCapture.CaptureScreenshotIntoRenderTexture(capture);
            Graphics.Blit(capture, cropped,
                new Vector2(viewport.width / Screen.width, viewport.height / Screen.height),
                new Vector2(viewport.x / Screen.width, viewport.y / Screen.height));
            // Separable passes sample adjacent pixels, avoiding the grid produced
            // by widely spaced two-dimensional taps when enlarged to a full screen.
            for (int pass = 0; pass < 3; pass++)
            {
                blurMaterial.SetVector("_BlurDirection", new Vector4(1f, 0f, 0f, 0f));
                Graphics.Blit(cropped, blurred, blurMaterial);
                blurMaterial.SetVector("_BlurDirection", new Vector4(0f, 1f, 0f, 0f));
                Graphics.Blit(blurred, cropped, blurMaterial);
            }
        }
    }

    void EnsureBuffers()
    {
        if (capture != null && capture.width == Screen.width && capture.height == Screen.height)
            return;
        ReleaseBuffers();
        capture = CreateBuffer(Screen.width, Screen.height);
        cropped = CreateBuffer(256, 224);
        blurred = CreateBuffer(256, 224);
    }

    static RenderTexture CreateBuffer(int width, int height)
    {
        var texture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        texture.Create();
        return texture;
    }

    void ReleaseBuffers()
    {
        Release(ref capture);
        Release(ref cropped);
        Release(ref blurred);
    }

    static void Release(ref RenderTexture texture)
    {
        if (texture == null)
            return;
        texture.Release();
        Destroy(texture);
        texture = null;
    }

    void OnGUI()
    {
        if (Event.current.type != EventType.Repaint || !OwnsOutput)
            return;
        int background = Mathf.Clamp(SaveSystem.GetVideoSettings().background, 0, 10);
        Rect viewport = PixelPerfectViewport.ResolveFinalPixelRect(targetCamera);
        if (loadedBackground != background)
        {
            if (image != null)
                Resources.UnloadAsset(image);
            if (patternBase != null)
                Resources.UnloadAsset(patternBase);
            image = background >= 2 ? Resources.Load<Texture2D>($"VideoBackgrounds/Background{background - 1:00}") : null;
            // The supplied patterns are transparent overlays, paired with the supplied
            // yellow/red backgrounds rather than displaying their invisible RGB pixels.
            patternBase = background == 2 ? Resources.Load<Texture2D>("VideoBackgrounds/Background04") :
                background == 3 ? Resources.Load<Texture2D>("VideoBackgrounds/Background03") : null;
            loadedBackground = background;
        }
        if (background == 0 || !HasBars(viewport))
            return;
        Texture texture = background == 1 ? cropped : image;
        if (texture == null)
            return;

        // GUI coordinates start at the top; camera pixel rectangles start at the bottom.
        Rect game = new Rect(viewport.x, Screen.height - viewport.yMax, viewport.width, viewport.height);
        float aspect = background == 1 ? viewport.width / viewport.height : (float)texture.width / texture.height;
        float width = Screen.width;
        float height = width / aspect;
        if (height < Screen.height)
        {
            height = Screen.height;
            width = height * aspect;
        }
        Rect output = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
        Color oldColor = GUI.color;
        int oldDepth = GUI.depth;
        GUI.color = Color.white;
        GUI.depth = 1000;
        DrawBar(new Rect(0, 0, Screen.width, game.yMin), output, texture);
        DrawBar(new Rect(0, game.yMax, Screen.width, Screen.height - game.yMax), output, texture);
        DrawBar(new Rect(0, game.yMin, game.xMin, game.height), output, texture);
        DrawBar(new Rect(game.xMax, game.yMin, Screen.width - game.xMax, game.height), output, texture);
        GUI.color = oldColor;
        GUI.depth = oldDepth;
    }

    void DrawBar(Rect bar, Rect output, Texture texture)
    {
        if (bar.width <= 0 || bar.height <= 0)
            return;
        Rect uv = new Rect((bar.x - output.x) / output.width,
            1f - (bar.yMax - output.y) / output.height,
            bar.width / output.width, bar.height / output.height);
        if (texture is RenderTexture && SystemInfo.graphicsUVStartsAtTop)
        {
            uv.y = 1f - uv.y;
            uv.height = -uv.height;
        }
        if (patternBase != null)
            GUI.DrawTextureWithTexCoords(bar, patternBase, uv, false);
        GUI.DrawTextureWithTexCoords(bar, texture, uv, true);
    }
}

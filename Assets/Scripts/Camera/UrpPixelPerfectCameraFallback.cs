using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.U2D;

/// <summary>
/// Applies the legacy Pixel Perfect Camera viewport rules when the project is
/// rendered by URP. The installed PixelPerfectCamera package only implements
/// its render-texture path for the Built-in Render Pipeline, so under URP its
/// Canvas can be fitted while SpriteRenderer content still fills the display.
/// </summary>
[DefaultExecutionOrder(-1000)]
[RequireComponent(typeof(Camera))]
public sealed class UrpPixelPerfectCameraFallback : MonoBehaviour
{
    const string BackgroundCameraName = "__PixelPerfectBlackBars";

    Camera targetCamera;
    PixelPerfectCamera legacyPixelPerfectCamera;
    Camera backgroundCamera;

    void Awake()
    {
        targetCamera = GetComponent<Camera>();
        legacyPixelPerfectCamera = GetComponent<PixelPerfectCamera>();

        // Its OnPreCull/OnPostRender implementation is intended for Built-in
        // rendering and fights the URP viewport applied below.
        if (legacyPixelPerfectCamera != null && legacyPixelPerfectCamera.enabled)
            legacyPixelPerfectCamera.enabled = false;

        EnsureBlackBarsCamera();
        ApplyViewport();
    }

    void OnEnable()
    {
        ApplyViewport();
    }

    void LateUpdate()
    {
        ApplyViewport();
    }

    void OnDestroy()
    {
        if (backgroundCamera != null)
            Destroy(backgroundCamera.gameObject);
    }

    void ApplyViewport()
    {
        if (targetCamera == null)
            targetCamera = GetComponent<Camera>();

        if (targetCamera == null || legacyPixelPerfectCamera == null)
            return;

        Rect outputRect = PixelPerfectViewport.ResolveFinalPixelRect(targetCamera);
        float screenWidth = Mathf.Max(1f, Screen.width);
        float screenHeight = Mathf.Max(1f, Screen.height);

        targetCamera.forceIntoRenderTexture = false;
        targetCamera.rect = new Rect(
            outputRect.xMin / screenWidth,
            outputRect.yMin / screenHeight,
            outputRect.width / screenWidth,
            outputRect.height / screenHeight);
        targetCamera.orthographicSize = ResolveOrthographicSize(outputRect);

        if (backgroundCamera != null)
        {
            backgroundCamera.depth = targetCamera.depth - 1f;
            backgroundCamera.rect = new Rect(0f, 0f, 1f, 1f);
        }
    }

    float ResolveOrthographicSize(Rect outputRect)
    {
        float ppu = Mathf.Max(1, legacyPixelPerfectCamera.assetsPPU);
        float referenceWidth = Mathf.Max(1, legacyPixelPerfectCamera.refResolutionX);
        float referenceHeight = Mathf.Max(1, legacyPixelPerfectCamera.refResolutionY);

        if (legacyPixelPerfectCamera.cropFrameY)
            return referenceHeight * 0.5f / ppu;

        if (legacyPixelPerfectCamera.cropFrameX)
        {
            float aspect = outputRect.width / Mathf.Max(1f, outputRect.height);
            return referenceWidth * 0.5f / Mathf.Max(0.0001f, aspect) / ppu;
        }

        int zoom = Mathf.Max(1, Mathf.Min(
            Screen.width / Mathf.RoundToInt(referenceWidth),
            Screen.height / Mathf.RoundToInt(referenceHeight)));
        return outputRect.height * 0.5f / (zoom * ppu);
    }

    void EnsureBlackBarsCamera()
    {
        if (backgroundCamera != null || targetCamera == null)
            return;

        GameObject background = new GameObject(BackgroundCameraName);
        background.hideFlags = HideFlags.DontSave;
        background.transform.SetParent(transform, false);
        backgroundCamera = background.AddComponent<Camera>();
        backgroundCamera.clearFlags = CameraClearFlags.SolidColor;
        backgroundCamera.backgroundColor = Color.black;
        backgroundCamera.cullingMask = 0;
        backgroundCamera.orthographic = true;
        backgroundCamera.depth = targetCamera.depth - 1f;
        backgroundCamera.allowHDR = false;
        backgroundCamera.allowMSAA = false;
    }
}

/// <summary>Creates the URP-compatible adapter for every scene camera that uses PixelPerfectCamera.</summary>
public static class UrpPixelPerfectCameraFallbackBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Initialize()
    {
        SceneManager.sceneLoaded += (_, _) => InstallForLoadedCameras();
        InstallForLoadedCameras();
    }

    static void InstallForLoadedCameras()
    {
        PixelPerfectCamera[] cameras = Object.FindObjectsByType<PixelPerfectCamera>();
        for (int i = 0; i < cameras.Length; i++)
        {
            PixelPerfectCamera pixelPerfectCamera = cameras[i];
            if (pixelPerfectCamera != null && pixelPerfectCamera.GetComponent<UrpPixelPerfectCameraFallback>() == null)
                pixelPerfectCamera.gameObject.AddComponent<UrpPixelPerfectCameraFallback>();
        }
    }
}

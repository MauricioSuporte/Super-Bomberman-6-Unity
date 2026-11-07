using UnityEngine;
using UnityEngine.UI;
using UnityEngine.U2D;

/// <summary>One GPU-only multiplicative draw over the game and UI, fitted to the safe frame.</summary>
[RequireComponent(typeof(Camera))]
public sealed class VideoFilterPresenter : MonoBehaviour
{
    static readonly int FilterId = Shader.PropertyToID("_Filter");
    static readonly int StrengthId = Shader.PropertyToID("_Strength");
    static readonly int ReferenceSizeId = Shader.PropertyToID("_ReferenceSize");
    static readonly int OutputSizeId = Shader.PropertyToID("_OutputSize");
    Camera targetCamera;
    Canvas filterCanvas;
    Material filterMaterial;

    void OnEnable() => targetCamera = GetComponent<Camera>();

    void LateUpdate()
    {
        SavedVideoSettings settings = SaveSystem.GetVideoSettings();
        bool visible = settings.screenFilter > 0 && settings.filterIntensity > 0 &&
            targetCamera != null && targetCamera == Camera.main && targetCamera.targetTexture == null;
        if (!visible)
        {
            if (filterCanvas != null) filterCanvas.enabled = false;
            return;
        }
        if (filterCanvas == null && !CreateOverlay())
            return;
        filterCanvas.enabled = true;
        Rect output = PixelPerfectViewport.ResolveFinalPixelRect(targetCamera);
        PixelPerfectCamera pixelCamera = targetCamera.GetComponent<PixelPerfectCamera>();
        int referenceWidth = pixelCamera != null ? pixelCamera.refResolutionX : 256;
        int referenceHeight = pixelCamera != null ? pixelCamera.refResolutionY : 224;
        filterMaterial.SetFloat(FilterId, settings.screenFilter);
        filterMaterial.SetFloat(StrengthId, settings.filterIntensity / 100f);
        filterMaterial.SetVector(ReferenceSizeId, new Vector4(referenceWidth, referenceHeight, 0f, 0f));
        filterMaterial.SetVector(OutputSizeId, new Vector4(output.width, output.height, 0f, 0f));
    }

    bool CreateOverlay()
    {
        Shader shader = Resources.Load<Shader>("VideoScreenFilter");
        if (shader == null || !shader.isSupported)
            return false;
        filterMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        var root = new GameObject("__VideoScreenFilter", typeof(RectTransform), typeof(Canvas));
        root.hideFlags = HideFlags.DontSave;
        root.transform.SetParent(transform, false);
        filterCanvas = root.GetComponent<Canvas>();
        filterCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        filterCanvas.sortingOrder = 32760;
        var frame = new GameObject("SafeFrame", typeof(RectTransform), typeof(UICameraViewportFitter), typeof(Image));
        frame.transform.SetParent(root.transform, false);
        var image = frame.GetComponent<Image>();
        image.material = filterMaterial;
        image.raycastTarget = false;
        image.maskable = false;
        // There is intentionally no GraphicRaycaster: input passes through the effect.
        frame.GetComponent<UICameraViewportFitter>().ForceApplyNow();
        return true;
    }

    void OnDisable()
    {
        if (filterCanvas != null) filterCanvas.enabled = false;
    }

    void OnDestroy()
    {
        if (filterCanvas != null) Destroy(filterCanvas.gameObject);
        if (filterMaterial != null) Destroy(filterMaterial);
    }
}

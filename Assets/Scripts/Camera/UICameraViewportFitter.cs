using UnityEngine;

[ExecuteAlways]
[DefaultExecutionOrder(-1000)]
public class UICameraViewportFitter : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;

    Rect lastPixelRect = new(float.MinValue, float.MinValue, float.MinValue, float.MinValue);
    Vector2 lastAnchorMin = new(float.MinValue, float.MinValue);
    Vector2 lastAnchorMax = new(float.MinValue, float.MinValue);
    Vector2 lastAnchoredPosition = new(float.MinValue, float.MinValue);
    Vector2 lastSizeDelta = new(float.MinValue, float.MinValue);
    Vector3 lastLocalScale = new(float.MinValue, float.MinValue, float.MinValue);

    void OnEnable()
    {
        ApplyViewport();
    }

    void LateUpdate()
    {
        ApplyViewport();
    }

    public void ForceApplyNow()
    {
        ApplyViewport();
    }

    void ApplyViewport()
    {
        if (!targetCamera)
            targetCamera = Camera.main;

        if (!targetCamera)
            return;

        var rt = (RectTransform)transform;
        Rect pixelRect = PixelPerfectViewport.ResolveFinalPixelRect(targetCamera);
        Rect canvasRect = ResolveCanvasPixelRect();

        if (pixelRect.width <= 0f || pixelRect.height <= 0f ||
            canvasRect.width <= 0f || canvasRect.height <= 0f)
            return;

        Vector2 anchorMin = new(
            (pixelRect.xMin - canvasRect.xMin) / canvasRect.width,
            (pixelRect.yMin - canvasRect.yMin) / canvasRect.height);
        Vector2 anchorMax = new(
            (pixelRect.xMax - canvasRect.xMin) / canvasRect.width,
            (pixelRect.yMax - canvasRect.yMin) / canvasRect.height);

        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        rt.localScale = Vector3.one;

        bool changed =
            !Approximately(pixelRect, lastPixelRect) ||
            rt.anchorMin != lastAnchorMin ||
            rt.anchorMax != lastAnchorMax ||
            rt.anchoredPosition != lastAnchoredPosition ||
            rt.sizeDelta != lastSizeDelta ||
            rt.localScale != lastLocalScale;

        if (!changed)
            return;

        lastPixelRect = pixelRect;
        lastAnchorMin = rt.anchorMin;
        lastAnchorMax = rt.anchorMax;
        lastAnchoredPosition = rt.anchoredPosition;
        lastSizeDelta = rt.sizeDelta;
        lastLocalScale = rt.localScale;
    }

    Rect ResolveCanvasPixelRect()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        Canvas rootCanvas = canvas != null ? canvas.rootCanvas : null;

        if (rootCanvas != null && rootCanvas.pixelRect.width > 0f && rootCanvas.pixelRect.height > 0f)
            return rootCanvas.pixelRect;

        return new Rect(0f, 0f, Mathf.Max(1f, Screen.width), Mathf.Max(1f, Screen.height));
    }

    static bool Approximately(Rect a, Rect b)
    {
        return
            Mathf.Abs(a.x - b.x) < 0.01f &&
            Mathf.Abs(a.y - b.y) < 0.01f &&
            Mathf.Abs(a.width - b.width) < 0.01f &&
            Mathf.Abs(a.height - b.height) < 0.01f;
    }
}

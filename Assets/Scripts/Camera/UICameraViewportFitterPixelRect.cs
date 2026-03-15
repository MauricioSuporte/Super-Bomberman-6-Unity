using UnityEngine;

[ExecuteAlways]
public class UICameraViewportFitterPixelRect : MonoBehaviour
{
    [SerializeField] Camera targetCamera;

    RectTransform rt;
    Rect lastPixelRect = new(float.MinValue, float.MinValue, float.MinValue, float.MinValue);

    void OnEnable()
    {
        ApplyNow();
    }

    void LateUpdate()
    {
        ApplyNow();
    }

    public void ForceApplyNow()
    {
        ApplyNow();
    }

    void ApplyNow()
    {
        if (rt == null)
            rt = transform as RectTransform;

        if (rt == null)
            return;

        if (!targetCamera)
            targetCamera = Camera.main;

        if (!targetCamera)
            return;

        Rect pixelRect = targetCamera.pixelRect;
        if (pixelRect.width <= 0f || pixelRect.height <= 0f)
            return;

        if (Approximately(pixelRect, lastPixelRect))
            return;

        float screenW = Mathf.Max(1f, Screen.width);
        float screenH = Mathf.Max(1f, Screen.height);

        rt.anchorMin = new Vector2(pixelRect.xMin / screenW, pixelRect.yMin / screenH);
        rt.anchorMax = new Vector2(pixelRect.xMax / screenW, pixelRect.yMax / screenH);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        rt.localScale = Vector3.one;

        lastPixelRect = pixelRect;
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
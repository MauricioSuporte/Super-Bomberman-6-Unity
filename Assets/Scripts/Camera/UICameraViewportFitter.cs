using UnityEngine;

[ExecuteAlways]
public class UICameraViewportFitter : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;

    Rect lastViewportRect;
    Vector2 lastAnchorMin = new(float.MinValue, float.MinValue);
    Vector2 lastAnchorMax = new(float.MinValue, float.MinValue);
    Vector2 lastAnchoredPosition = new(float.MinValue, float.MinValue);
    Vector2 lastSizeDelta = new(float.MinValue, float.MinValue);
    Vector3 lastLocalScale = new(float.MinValue, float.MinValue, float.MinValue);

    void LateUpdate()
    {
        if (!targetCamera) targetCamera = Camera.main;

        var rt = (RectTransform)transform;
        Rect vr = targetCamera.rect;

        rt.anchorMin = new Vector2(vr.xMin, vr.yMin);
        rt.anchorMax = new Vector2(vr.xMax, vr.yMax);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        rt.localScale = Vector3.one;

        bool changed =
            vr != lastViewportRect ||
            rt.anchorMin != lastAnchorMin ||
            rt.anchorMax != lastAnchorMax ||
            rt.anchoredPosition != lastAnchoredPosition ||
            rt.sizeDelta != lastSizeDelta ||
            rt.localScale != lastLocalScale;

        if (!changed)
            return;

        lastViewportRect = vr;
        lastAnchorMin = rt.anchorMin;
        lastAnchorMax = rt.anchorMax;
        lastAnchoredPosition = rt.anchoredPosition;
        lastSizeDelta = rt.sizeDelta;
        lastLocalScale = rt.localScale;
    }
}
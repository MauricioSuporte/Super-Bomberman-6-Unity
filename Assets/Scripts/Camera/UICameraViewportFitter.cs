using UnityEngine;

[ExecuteAlways]
public class UICameraViewportFitter : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;

    void LateUpdate()
    {
        if (!targetCamera) targetCamera = Camera.main;
        if (!targetCamera) return;

        var rt = (RectTransform)transform;

        Rect vr = targetCamera.rect;

        rt.anchorMin = new Vector2(vr.xMin, vr.yMin);
        rt.anchorMax = new Vector2(vr.xMax, vr.yMax);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        rt.localScale = Vector3.one;
    }
}
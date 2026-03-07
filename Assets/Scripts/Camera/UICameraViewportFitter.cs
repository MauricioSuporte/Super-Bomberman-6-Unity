using UnityEngine;

[ExecuteAlways]
public class UICameraViewportFitter : MonoBehaviour
{
    const string LOG = "[UICameraViewportFitter]";

    [SerializeField] private Camera targetCamera;

    [Header("Debug (Surgical Logs)")]
    [SerializeField] private bool enableSurgicalLogs = true;
    [SerializeField] private bool logOnLateUpdateWhenChanged = true;
    [SerializeField] private bool logMissingCamera = true;

    Rect lastViewportRect;
    Vector2 lastAnchorMin = new Vector2(float.MinValue, float.MinValue);
    Vector2 lastAnchorMax = new Vector2(float.MinValue, float.MinValue);
    Vector2 lastAnchoredPosition = new Vector2(float.MinValue, float.MinValue);
    Vector2 lastSizeDelta = new Vector2(float.MinValue, float.MinValue);
    Vector3 lastLocalScale = new Vector3(float.MinValue, float.MinValue, float.MinValue);

    void LateUpdate()
    {
        if (!targetCamera) targetCamera = Camera.main;
        if (!targetCamera)
        {
            if (enableSurgicalLogs && logMissingCamera)
                Debug.LogWarning($"{LOG} No target camera found.", this);
            return;
        }

        var rt = (RectTransform)transform;
        Rect vr = targetCamera.rect;

        rt.anchorMin = new Vector2(vr.xMin, vr.yMin);
        rt.anchorMax = new Vector2(vr.xMax, vr.yMax);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        rt.localScale = Vector3.one;

        if (!enableSurgicalLogs || !logOnLateUpdateWhenChanged)
            return;

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

        Debug.Log(
            $"{LOG} Applied viewport fit | " +
            $"camera='{targetCamera.name}' pixelRect={targetCamera.pixelRect} viewportRect={vr} " +
            $"anchorMin={rt.anchorMin} anchorMax={rt.anchorMax} anchoredPosition={rt.anchoredPosition} " +
            $"sizeDelta={rt.sizeDelta} localScale={rt.localScale}",
            this);
    }
}
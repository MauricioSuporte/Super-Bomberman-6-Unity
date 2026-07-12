using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public sealed class HudBackground : MonoBehaviour
{
    const float alturaHud = 23f;
    const float alturaTotal = 224f;

    RectTransform _rt;

    void LateUpdate()
    {
        using var performanceSample = BattleModePerformanceMarkers.HudBackgroundLateUpdate.Auto();

        if (_rt == null)
            _rt = (RectTransform)transform;

        float yMin = (alturaTotal - alturaHud) / alturaTotal;
        Vector2 anchorMin = new(0f, yMin);

        if (_rt.anchorMin != anchorMin)
            _rt.anchorMin = anchorMin;
        if (_rt.anchorMax != Vector2.one)
            _rt.anchorMax = Vector2.one;
        if (_rt.anchoredPosition != Vector2.zero)
            _rt.anchoredPosition = Vector2.zero;
        if (_rt.sizeDelta != Vector2.zero)
            _rt.sizeDelta = Vector2.zero;
        if (_rt.localScale != Vector3.one)
            _rt.localScale = Vector3.one;
    }
}

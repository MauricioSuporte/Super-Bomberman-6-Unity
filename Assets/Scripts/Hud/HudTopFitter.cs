using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public sealed class HudTopFitter : MonoBehaviour
{
    const float alturaHud = 23f;
    const float alturaTotal = 224f;

    RectTransform _rt;

    void LateUpdate()
    {
        if (_rt == null)
            _rt = (RectTransform)transform;

        float yMin = (alturaTotal - alturaHud) / alturaTotal;

        _rt.anchorMin = new Vector2(0f, yMin);
        _rt.anchorMax = new Vector2(1f, 1f);
        _rt.anchoredPosition = Vector2.zero;
        _rt.sizeDelta = Vector2.zero;
        _rt.localScale = Vector3.one;
    }
}
using UnityEngine;

[DisallowMultipleComponent]
public class WorldMapFloatingSprite : MonoBehaviour
{
    [Header("Pixel-Perfect Float")]
    [SerializeField, Min(1f)] float pixelsPerUnit = 16f;
    [SerializeField, Min(0)] int verticalAmplitudePixels = 2;
    [SerializeField, Min(0.1f)] float cycleDurationSeconds = 2f;
    [SerializeField, Range(0f, 1f)] float cycleOffset;

    Vector3 authoredLocalPosition;

    void Awake()
    {
        authoredLocalPosition = transform.localPosition;
    }

    void OnDisable()
    {
        transform.localPosition = authoredLocalPosition;
    }

    void LateUpdate()
    {
        if (verticalAmplitudePixels == 0)
            return;

        float cycle = (Time.unscaledTime / cycleDurationSeconds + cycleOffset) * Mathf.PI * 2f;
        float offsetPixels = Mathf.Sin(cycle) * verticalAmplitudePixels;
        float offsetUnits = Mathf.Round(offsetPixels) / pixelsPerUnit;

        transform.localPosition = new Vector3(
            authoredLocalPosition.x,
            authoredLocalPosition.y + offsetUnits,
            authoredLocalPosition.z);
    }
}

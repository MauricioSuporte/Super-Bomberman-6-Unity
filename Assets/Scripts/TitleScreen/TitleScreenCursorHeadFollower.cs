using UnityEngine;
using UnityEngine.UI;

/// <summary>Draws the static green cursor head behind independently rendered eyes.</summary>
public sealed class TitleScreenCursorHeadFollower : MonoBehaviour
{
    [SerializeField] RectTransform eyes;
    [SerializeField] TitleScreenCursorEyeIdle eyePose;
    RectTransform rectTransformCache;
    Image headImage;

    void Awake()
    {
        rectTransformCache = transform as RectTransform;
        headImage = GetComponent<Image>();
        if (eyePose == null && eyes != null)
            eyePose = eyes.GetComponent<TitleScreenCursorEyeIdle>();
    }

    void LateUpdate()
    {
        if (rectTransformCache == null || eyes == null) return;
        bool visible = eyes.gameObject.activeInHierarchy;
        if (headImage != null) headImage.enabled = visible;
        if (!visible) return;
        Vector3 eyeOffset = eyePose != null ? eyePose.CurrentEyeLocalOffset : Vector3.zero;
        rectTransformCache.localPosition = eyes.localPosition - eyeOffset;
        rectTransformCache.localScale = eyes.localScale;
    }
}

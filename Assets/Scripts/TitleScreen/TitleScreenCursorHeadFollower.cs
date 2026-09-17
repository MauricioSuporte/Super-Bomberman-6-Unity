using UnityEngine;
using UnityEngine.UI;

/// <summary>Draws the static green cursor head behind independently rendered eyes.</summary>
public sealed class TitleScreenCursorHeadFollower : MonoBehaviour
{
    [SerializeField] RectTransform eyes;
    RectTransform rectTransformCache;
    Image headImage;

    void Awake()
    {
        rectTransformCache = transform as RectTransform;
        headImage = GetComponent<Image>();
    }

    void LateUpdate()
    {
        if (rectTransformCache == null || eyes == null) return;
        bool visible = eyes.gameObject.activeInHierarchy;
        if (headImage != null) headImage.enabled = visible;
        if (!visible) return;
        rectTransformCache.localPosition = eyes.localPosition;
        rectTransformCache.localScale = eyes.localScale;
    }
}

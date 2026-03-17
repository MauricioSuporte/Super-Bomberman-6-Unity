using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MobileButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [SerializeField] private PlayerAction action;
    [SerializeField] private RectTransform visualTarget;
    [SerializeField] private Vector3 releasedScale = Vector3.one;
    [SerializeField] private Vector3 pressedScale = new Vector3(0.9f, 0.9f, 1f);

    [Header("Visual Fill")]
    [SerializeField] private bool stretchVisualToHitbox = true;
    [SerializeField] private bool disableVisualPreserveAspect = true;
    [SerializeField] private bool disableVisualRaycastTarget = true;

    [Header("Debug Hitbox")]
    [SerializeField] private bool showDebugHitbox;
    [SerializeField] private Color debugHitboxColor = new Color(1f, 0f, 0f, 0.20f);

    private Image _hitboxImage;
    private Color _originalColor;

    void Awake()
    {
        _hitboxImage = GetComponent<Image>();

        if (_hitboxImage != null)
        {
            _originalColor = _hitboxImage.color;

            if (showDebugHitbox)
                _hitboxImage.color = debugHitboxColor;
            else
                _hitboxImage.color = _originalColor;
        }

        ConfigureVisualToMatchHitbox();
        ApplyReleasedVisual();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (MobileInputBridge.Instance != null)
            MobileInputBridge.Instance.Press(action);

        ApplyPressedVisual();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (MobileInputBridge.Instance != null)
            MobileInputBridge.Instance.Release(action);

        ApplyReleasedVisual();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (MobileInputBridge.Instance != null)
            MobileInputBridge.Instance.Release(action);

        ApplyReleasedVisual();
    }

    void ApplyPressedVisual()
    {
        if (visualTarget != null)
            visualTarget.localScale = pressedScale;
    }

    void ApplyReleasedVisual()
    {
        if (visualTarget != null)
            visualTarget.localScale = releasedScale;
    }

    void ConfigureVisualToMatchHitbox()
    {
        if (visualTarget == null)
            return;

        if (stretchVisualToHitbox)
        {
            visualTarget.anchorMin = Vector2.zero;
            visualTarget.anchorMax = Vector2.one;
            visualTarget.pivot = new Vector2(0.5f, 0.5f);
            visualTarget.anchoredPosition = Vector2.zero;
            visualTarget.offsetMin = Vector2.zero;
            visualTarget.offsetMax = Vector2.zero;
            visualTarget.localScale = releasedScale;
            visualTarget.localRotation = Quaternion.identity;
        }

        var visualImage = visualTarget.GetComponent<Image>();
        if (visualImage != null)
        {
            if (disableVisualPreserveAspect)
                visualImage.preserveAspect = false;

            if (disableVisualRaycastTarget)
                visualImage.raycastTarget = false;
        }
    }

    void OnValidate()
    {
        var image = GetComponent<Image>();
        if (image != null)
        {
            if (showDebugHitbox)
                image.color = debugHitboxColor;
            else if (Application.isPlaying)
                image.color = _originalColor;
        }

        ConfigureVisualToMatchHitbox();
    }
}
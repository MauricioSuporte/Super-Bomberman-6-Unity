using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MobileButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [SerializeField] private PlayerAction action;
    [SerializeField] private RectTransform visualTarget;
    [SerializeField] private Vector3 releasedScale = Vector3.one;
    [SerializeField] private Vector3 pressedScale = new Vector3(0.9f, 0.9f, 1f);

    [Header("Pressed Sprite")]
    [SerializeField] private Image visualImage;
    [SerializeField] private Sprite releasedSprite;
    [SerializeField] private Sprite pressedSprite;

    [Header("Button Icon")]
    [SerializeField] private RectTransform iconTarget;
    [SerializeField] private Vector2 pressedIconOffset = new Vector2(0f, -2f);

    [Header("Visual Fill")]
    [SerializeField] private bool stretchVisualToHitbox = true;
    [SerializeField] private bool disableVisualPreserveAspect = true;
    [SerializeField] private bool disableVisualRaycastTarget = true;

    private Image _hitboxImage;
    private Vector2 _releasedIconPosition;
    private Sprite _defaultIconSprite;

    public PlayerAction Action => action;

    void Awake()
    {
        _hitboxImage = GetComponent<Image>();

        ConfigureVisualToMatchHitbox();
        CacheReleasedIconPosition();
        CacheDefaultIconSprite();
        ApplyReleasedVisual();
    }

    public void SetContextIcon(Sprite contextSprite)
    {
        if (iconTarget == null)
            return;

        if (!iconTarget.TryGetComponent<Image>(out var iconImage))
            return;

        iconImage.sprite = contextSprite != null ? contextSprite : _defaultIconSprite;
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

        if (visualImage != null && pressedSprite != null)
            visualImage.sprite = pressedSprite;

        if (iconTarget != null)
            iconTarget.anchoredPosition = _releasedIconPosition + pressedIconOffset;
    }

    void ApplyReleasedVisual()
    {
        if (visualTarget != null)
            visualTarget.localScale = releasedScale;

        if (visualImage != null && releasedSprite != null)
            visualImage.sprite = releasedSprite;

        if (iconTarget != null)
            iconTarget.anchoredPosition = _releasedIconPosition;
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

        if (visualTarget.TryGetComponent<Image>(out var visualImage))
        {
            this.visualImage = visualImage;

            if (disableVisualPreserveAspect)
                visualImage.preserveAspect = false;

            if (disableVisualRaycastTarget)
                visualImage.raycastTarget = false;
        }
    }

    void CacheReleasedIconPosition()
    {
        if (iconTarget != null)
            _releasedIconPosition = iconTarget.anchoredPosition;
    }

    void CacheDefaultIconSprite()
    {
        if (iconTarget != null && iconTarget.TryGetComponent<Image>(out var iconImage))
            _defaultIconSprite = iconImage.sprite;
    }

    void OnValidate()
    {
        ConfigureVisualToMatchHitbox();
    }
}

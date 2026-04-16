using UnityEngine;
using UnityEngine.EventSystems;

public class MobileDynamicJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("References")]
    [SerializeField] private RectTransform touchArea;
    [SerializeField] private RectTransform baseVisual;
    [SerializeField] private RectTransform handleVisual;
    [SerializeField] private Canvas canvas;

    [Header("Behavior")]
    [SerializeField] private bool leftSideOnly = true;
    [SerializeField] private float radius = 120f;
    [SerializeField] private float deadZone = 20f;
    [SerializeField] private bool hideWhenReleased = true;
    [SerializeField] private bool returnToRestPositionOnRelease = true;
    [SerializeField] private bool snapToCardinal = false;
    [SerializeField, Range(0f, 1f)] private float cardinalBias = 0.15f;

    private Camera uiCamera;
    private int activePointerId = int.MinValue;
    private Vector2 pointerDownPosition;
    private Vector2 restBaseAnchoredPosition;
    private bool hasRestBaseAnchoredPosition;
    private bool engaged;

    void Awake()
    {
        if (canvas == null)
            canvas = GetComponentInParent<Canvas>();

        if (touchArea == null)
            touchArea = transform as RectTransform;

        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            uiCamera = canvas.worldCamera;

        CacheRestBasePosition();

        SetVisualVisible(!hideWhenReleased);
        ResetVisuals();
    }

    void OnEnable()
    {
        RestoreBasePosition();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (activePointerId != int.MinValue)
            return;

        if (leftSideOnly && eventData.position.x > Screen.width * 0.5f)
            return;

        activePointerId = eventData.pointerId;
        pointerDownPosition = eventData.position;
        engaged = true;

        UpdateBasePosition(pointerDownPosition);
        ResetVisuals();
        SetVisualVisible(true);

        if (MobileInputBridge.Instance != null)
            MobileInputBridge.Instance.ClearMoveVector();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!engaged)
            return;

        if (eventData.pointerId != activePointerId)
            return;

        Vector2 delta = eventData.position - pointerDownPosition;
        float distance = delta.magnitude;

        if (distance <= deadZone)
        {
            UpdateHandle(Vector2.zero);

            if (MobileInputBridge.Instance != null)
                MobileInputBridge.Instance.ClearMoveVector();

            return;
        }

        Vector2 normalized = radius > 0.0001f ? delta / radius : Vector2.zero;
        normalized = Vector2.ClampMagnitude(normalized, 1f);

        if (snapToCardinal)
            normalized = SnapVectorToCardinal(normalized, cardinalBias);

        UpdateHandle(normalized);

        if (MobileInputBridge.Instance != null)
            MobileInputBridge.Instance.SetMoveVector(normalized);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId != activePointerId)
            return;

        activePointerId = int.MinValue;
        engaged = false;

        if (MobileInputBridge.Instance != null)
            MobileInputBridge.Instance.ClearMoveVector();

        RestoreBasePosition();
        ResetVisuals();
        SetVisualVisible(!hideWhenReleased);
    }

    private void UpdateBasePosition(Vector2 screenPosition)
    {
        if (baseVisual == null || touchArea == null)
            return;

        bool success = RectTransformUtility.ScreenPointToLocalPointInRectangle(
            touchArea,
            screenPosition,
            uiCamera,
            out Vector2 localPoint);

        if (success)
            baseVisual.anchoredPosition = localPoint;
    }

    private void UpdateHandle(Vector2 normalizedInput)
    {
        if (handleVisual == null)
            return;

        Vector2 target = normalizedInput * radius;
        handleVisual.anchoredPosition = target;
    }

    private void CacheRestBasePosition()
    {
        if (baseVisual == null)
        {
            hasRestBaseAnchoredPosition = false;
            return;
        }

        restBaseAnchoredPosition = baseVisual.anchoredPosition;
        hasRestBaseAnchoredPosition = true;
    }

    private void RestoreBasePosition()
    {
        if (!returnToRestPositionOnRelease || !hasRestBaseAnchoredPosition || baseVisual == null)
            return;

        baseVisual.anchoredPosition = restBaseAnchoredPosition;
    }

    private void ResetVisuals()
    {
        if (handleVisual != null)
            handleVisual.anchoredPosition = Vector2.zero;
    }

    private void SetVisualVisible(bool visible)
    {
        if (baseVisual != null)
            baseVisual.gameObject.SetActive(visible);

        if (handleVisual != null)
            handleVisual.gameObject.SetActive(visible);
    }

    private Vector2 SnapVectorToCardinal(Vector2 input, float bias)
    {
        if (input == Vector2.zero)
            return Vector2.zero;

        float absX = Mathf.Abs(input.x);
        float absY = Mathf.Abs(input.y);

        if (absX > absY + bias)
            return new Vector2(Mathf.Sign(input.x), 0f);

        if (absY > absX + bias)
            return new Vector2(0f, Mathf.Sign(input.y));

        if (absX >= absY)
            return new Vector2(Mathf.Sign(input.x), 0f);

        return new Vector2(0f, Mathf.Sign(input.y));
    }

}

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MobileDynamicJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [SerializeField] private RectTransform touchArea;
    [SerializeField] private RectTransform baseVisual;
    [SerializeField] private RectTransform handleVisual;
    [SerializeField] private Canvas canvas;
    private readonly Sprite[] dpad = new Sprite[5];
    private Sprite analogSprite;
    private Image baseImage;
    private int pointerId = int.MinValue;
    private Vector2 origin;
    private Vector2 restPosition;
    private Camera UiCamera => canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
    public RectTransform Visual => baseVisual;
    public bool Engaged => pointerId != int.MinValue;

    void Awake()
    {
        if (canvas == null) canvas = GetComponentInParent<Canvas>();
        if (touchArea == null) touchArea = (RectTransform)transform;
        baseImage = baseVisual.GetComponent<Image>();
        analogSprite = baseImage.sprite;
        for (int i = 0; i < dpad.Length; i++) dpad[i] = Resources.Load<Sprite>("UI/JoyStickBase" + (i + 1));
        restPosition = baseVisual.anchoredPosition;
        RefreshVisual();
    }

    public void RefreshVisual()
    {
        if (baseImage == null) return;
        bool analog = SaveSystem.GetTouchControls().analog;
        baseImage.sprite = analog ? analogSprite : dpad[0];
        handleVisual.sizeDelta = baseVisual.rect.size * (100f / 220f);
        handleVisual.gameObject.SetActive(analog);
        baseVisual.gameObject.SetActive(true);
    }

    public void OnPointerDown(PointerEventData e)
    {
        if (Engaged || MobileControlsRoot.Editing) return;
        var settings = SaveSystem.GetTouchControls();
        bool dynamic = settings.analog && settings.dynamicAnalog;
        if (!dynamic && !RectTransformUtility.RectangleContainsScreenPoint(baseVisual, e.position, UiCamera)) return;
        pointerId = e.pointerId;
        restPosition = baseVisual.anchoredPosition;
        if (dynamic)
        {
            RectTransformUtility.ScreenPointToWorldPointInRectangle(touchArea, e.position, UiCamera, out var point);
            baseVisual.position = point;
        }
        origin = RectTransformUtility.WorldToScreenPoint(UiCamera, baseVisual.position);
        MobileControlsRoot.Instance?.NotifyTouch();
        OnDrag(e);
    }

    public void OnDrag(PointerEventData e)
    {
        if (e.pointerId != pointerId || MobileControlsRoot.Editing) return;
        var settings = SaveSystem.GetTouchControls();
        RectTransformUtility.ScreenPointToLocalPointInRectangle(baseVisual, e.position, UiCamera, out var current);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(baseVisual, origin, UiCamera, out var center);
        Vector2 input = (current - center) / Mathf.Max(1f, baseVisual.rect.width * 0.5f);
        input = ResolveDirection(input, settings.deadzone, settings.blockDiagonals);
        MobileInputBridge.Instance?.SetMoveVector(input);
        if (settings.analog) handleVisual.anchoredPosition = input * baseVisual.rect.width * 0.3f;
        else baseImage.sprite = dpad[input == Vector2.zero ? 0 : Mathf.Abs(input.y) >= Mathf.Abs(input.x) ? (input.y > 0 ? 1 : 3) : (input.x > 0 ? 2 : 4)];
        MobileControlsRoot.Instance?.NotifyTouch();
    }

    // Output is digital after the touch-specific deadzone, so the shared gamepad threshold
    // cannot silently override the user's touch deadzone.
    public static Vector2 ResolveDirection(Vector2 input, float deadzone, bool blockDiagonals)
    {
        if (input.magnitude <= deadzone) return Vector2.zero;
        if (blockDiagonals) return Mathf.Abs(input.x) > Mathf.Abs(input.y)
            ? new Vector2(Mathf.Sign(input.x), 0) : new Vector2(0, Mathf.Sign(input.y));
        float angle = Mathf.Atan2(input.y, input.x) * Mathf.Rad2Deg;
        float snapped = Mathf.Round(angle / 45f) * 45f * Mathf.Deg2Rad;
        return new Vector2(Mathf.Round(Mathf.Cos(snapped)), Mathf.Round(Mathf.Sin(snapped)));
    }

    public void OnPointerUp(PointerEventData e)
    {
        if (e.pointerId == pointerId) Release();
    }

    public void Release()
    {
        if (Engaged) baseVisual.anchoredPosition = restPosition;
        pointerId = int.MinValue;
        MobileInputBridge.Instance?.ClearMoveVector();
        if (handleVisual != null) handleVisual.anchoredPosition = Vector2.zero;
        RefreshVisual();
    }

    void OnDisable() => Release();
    void OnApplicationFocus(bool focus) { if (!focus) Release(); }
}
using UnityEngine;
using UnityEngine.EventSystems;

public class MobileButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [SerializeField] private PlayerAction action;
    [SerializeField] private RectTransform visualTarget;
    [SerializeField] private Vector3 releasedScale = Vector3.one;
    [SerializeField] private Vector3 pressedScale = new Vector3(0.9f, 0.9f, 1f);

    void Awake()
    {
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
}
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class MobileControlLayoutHandle : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    public int Index { get; set; }
    public RectTransform Target { get; set; }
    private Vector2 offset;
    private int pointer = int.MinValue;

    public void OnPointerDown(PointerEventData e)
    {
        if (!MobileControlsRoot.Editing) { if (Index == 0) GetComponentInParent<MobileDynamicJoystick>().OnPointerDown(e); return; }
        if (pointer != int.MinValue) return;
        pointer = e.pointerId;
        MobileControlsRoot.Instance.SelectedControl = Index;
        RectTransformUtility.ScreenPointToWorldPointInRectangle(Target, e.position, e.pressEventCamera, out var point);
        offset = (Vector2)Target.position - (Vector2)point;
    }

    public void OnDrag(PointerEventData e)
    {
        if (!MobileControlsRoot.Editing) { if (Index == 0) GetComponentInParent<MobileDynamicJoystick>().OnDrag(e); return; }
        if (e.pointerId != pointer) return;
        RectTransformUtility.ScreenPointToWorldPointInRectangle(Target, e.position, e.pressEventCamera, out var point);
        Target.position = (Vector2)point + offset;
        MobileControlsRoot.Instance.StorePosition(Index);
    }

    public void OnPointerUp(PointerEventData e)
    {
        if (!MobileControlsRoot.Editing) { if (Index == 0) GetComponentInParent<MobileDynamicJoystick>().OnPointerUp(e); return; }
        if (e.pointerId != pointer) return;
        pointer = int.MinValue;
        SaveSystem.SaveTouchControls();
    }
    void OnDisable() => pointer = int.MinValue;
}
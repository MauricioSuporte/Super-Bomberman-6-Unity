using UnityEngine;
using UnityEngine.EventSystems;

public class MobileButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [SerializeField] private PlayerAction action;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (MobileInputBridge.Instance == null)
            return;

        MobileInputBridge.Instance.Press(action);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (MobileInputBridge.Instance == null)
            return;

        MobileInputBridge.Instance.Release(action);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (MobileInputBridge.Instance == null)
            return;

        MobileInputBridge.Instance.Release(action);
    }
}
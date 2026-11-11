using UnityEngine;

// Sample after player/bomb/mount presentation; input polling keeps its early order.
[DefaultExecutionOrder(31999)]
[DisallowMultipleComponent]
[RequireComponent(typeof(MotionDiagnosticProbe))]
public sealed class MotionDiagnosticLateProbe : MonoBehaviour
{
    MotionDiagnosticProbe owner;
    void Awake() => owner = GetComponent<MotionDiagnosticProbe>();
    void LateUpdate()
    {
        if (owner != null && owner.isActiveAndEnabled) owner.ObservePresentedFrame();
    }
}

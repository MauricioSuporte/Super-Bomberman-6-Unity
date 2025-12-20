using System.Collections;
using UnityEngine;

public interface IBombPunchExternalAnimator
{
    IEnumerator Play(Vector2 dir, float punchLockTime);
    void ForceStop();
}
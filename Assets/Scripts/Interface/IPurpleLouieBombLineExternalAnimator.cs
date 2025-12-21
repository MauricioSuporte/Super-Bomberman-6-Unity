using System.Collections;
using UnityEngine;

public interface IPurpleLouieBombLineExternalAnimator
{
    IEnumerator Play(Vector2 dir, float lockSeconds);
    void ForceStop();
}
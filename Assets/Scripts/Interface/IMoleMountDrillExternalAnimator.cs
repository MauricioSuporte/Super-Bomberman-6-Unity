using UnityEngine;

public interface IMoleMountDrillExternalAnimator
{
    void PlayPhase(int phase, Vector2 dir);
    void Stop();
}
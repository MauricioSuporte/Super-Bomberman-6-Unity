using UnityEngine;

namespace Assets.Scripts.Interface
{
    public interface IPinkLouieJumpExternalAnimator
    {
        void Play(Vector2 dir);
        void Stop();
    }
}

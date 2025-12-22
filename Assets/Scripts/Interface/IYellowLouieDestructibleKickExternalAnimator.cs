using UnityEngine;

namespace Assets.Scripts.Interface
{
    public interface IYellowLouieDestructibleKickExternalAnimator
    {
        void Play(Vector2 dir);
        void Stop();
    }
}

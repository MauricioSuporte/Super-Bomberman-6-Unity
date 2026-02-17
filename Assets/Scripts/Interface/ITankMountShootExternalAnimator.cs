using UnityEngine;

namespace Assets.Scripts.Interface
{
    public interface ITankMountShootExternalAnimator
    {
        void Play(Vector2 dir);
        void Stop();
    }
}

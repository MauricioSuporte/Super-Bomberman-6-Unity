using UnityEngine;

namespace Assets.Scripts.Interface
{
    public interface IRedLouiePunchExternalAnimator
    {
        void Play(Vector2 dir);
        void Stop();
    }
}

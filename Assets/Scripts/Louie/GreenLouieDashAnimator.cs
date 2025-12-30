using System.Collections.Generic;
using UnityEngine;

public class GreenLouieDashAnimator : MonoBehaviour, IGreenLouieDashExternalAnimator
{
    public AnimatedSpriteRenderer rollUp;
    public AnimatedSpriteRenderer rollDown;
    public AnimatedSpriteRenderer rollLeft;
    public AnimatedSpriteRenderer rollRight;

    AnimatedSpriteRenderer active;

    struct CachedState
    {
        public AnimatedSpriteRenderer asr;
        public bool enabled;
        public bool idle;
        public bool loop;
    }

    readonly List<CachedState> cachedMovementStates = new();
    bool dashing;

    public void Play(Vector2 dir)
    {
        CacheAndDisableAllMovementSprites();

        dashing = true;

        active = GetRoll(dir);
        DisableAllRolls();

        if (active != null)
        {
            if (active.TryGetComponent<SpriteRenderer>(out var sr))
                sr.flipX = (dir == Vector2.right);

            active.enabled = true;
            active.idle = false;
            active.loop = true;
            active.RefreshFrame();
        }
    }

    public void Stop()
    {
        dashing = false;

        if (active != null)
        {
            if (active.TryGetComponent<SpriteRenderer>(out var sr))
                sr.flipX = false;

            active.enabled = false;
        }

        active = null;
        DisableAllRolls();
        RestoreAllMovementSprites();
    }

    void LateUpdate()
    {
        if (!dashing)
            return;

        for (int i = 0; i < cachedMovementStates.Count; i++)
        {
            var s = cachedMovementStates[i].asr;
            if (s == null)
                continue;

            if (s.enabled)
                s.enabled = false;
        }
    }

    AnimatedSpriteRenderer GetRoll(Vector2 dir)
    {
        if (dir == Vector2.up) return rollUp;
        if (dir == Vector2.down) return rollDown;
        if (dir == Vector2.left) return rollLeft;
        if (dir == Vector2.right) return rollRight;
        return rollDown;
    }

    void DisableAllRolls()
    {
        if (rollUp) rollUp.enabled = false;
        if (rollDown) rollDown.enabled = false;
        if (rollLeft) rollLeft.enabled = false;
        if (rollRight) rollRight.enabled = false;
    }

    void CacheAndDisableAllMovementSprites()
    {
        cachedMovementStates.Clear();

        var sprites = GetComponentsInChildren<AnimatedSpriteRenderer>(true);
        foreach (var s in sprites)
        {
            if (s == null)
                continue;

            if (s == rollUp || s == rollDown || s == rollLeft || s == rollRight)
                continue;

            cachedMovementStates.Add(new CachedState
            {
                asr = s,
                enabled = s.enabled,
                idle = s.idle,
                loop = s.loop
            });

            s.enabled = false;
        }
    }

    void RestoreAllMovementSprites()
    {
        for (int i = 0; i < cachedMovementStates.Count; i++)
        {
            var st = cachedMovementStates[i];
            if (st.asr == null)
                continue;

            st.asr.idle = st.idle;
            st.asr.loop = st.loop;
            st.asr.enabled = st.enabled;

            if (st.asr.enabled)
                st.asr.RefreshFrame();
        }

        cachedMovementStates.Clear();
    }
}

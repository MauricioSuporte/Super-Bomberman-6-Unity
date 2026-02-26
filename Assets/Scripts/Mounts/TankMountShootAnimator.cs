using Assets.Scripts.Interface;
using System.Collections.Generic;
using UnityEngine;

public sealed class TankMountShootAnimator : MonoBehaviour, ITankMountShootExternalAnimator
{
    [Header("Shot Sprites (4 directions)")]
    [SerializeField] private AnimatedSpriteRenderer shotUp;
    [SerializeField] private AnimatedSpriteRenderer shotDown;
    [SerializeField] private AnimatedSpriteRenderer shotLeft;
    [SerializeField] private AnimatedSpriteRenderer shotRight;
    [SerializeField] private bool forceFlipXFalse = true;

    private struct CachedState
    {
        public AnimatedSpriteRenderer asr;
        public bool enabled;
        public bool idle;
        public bool loop;
        public bool pingPong;
        public int frame;
    }

    private readonly List<CachedState> cached = new();
    private AnimatedSpriteRenderer active;
    private bool playing;

    public void Play(Vector2 dir)
    {
        CacheAndDisableAllNonAbilitySprites();

        playing = true;

        active = Resolve(dir);
        DisableAllAbilitySprites();

        if (active != null)
        {
            active.enabled = true;
            active.idle = false;
            active.loop = true;
            active.pingPong = false;
            active.CurrentFrame = 0;
            active.RefreshFrame();

            if (forceFlipXFalse && active.TryGetComponent<SpriteRenderer>(out var sr) && sr != null)
                sr.flipX = false;
        }
    }

    public void Stop()
    {
        playing = false;

        if (active != null)
            active.enabled = false;

        active = null;
        DisableAllAbilitySprites();
        RestoreAllNonAbilitySprites();
    }

    private void LateUpdate()
    {
        if (!playing)
            return;

        for (int i = 0; i < cached.Count; i++)
        {
            var s = cached[i].asr;
            if (s != null && s.enabled)
                s.enabled = false;
        }
    }

    private AnimatedSpriteRenderer Resolve(Vector2 dir)
    {
        dir = Cardinalize(dir);

        if (dir == Vector2.up) return shotUp;
        if (dir == Vector2.down) return shotDown;
        if (dir == Vector2.left) return shotLeft;
        if (dir == Vector2.right) return shotRight;

        return shotDown != null ? shotDown :
               shotLeft != null ? shotLeft :
               shotUp;
    }

    private static Vector2 Cardinalize(Vector2 v)
    {
        if (v == Vector2.zero)
            return Vector2.down;

        if (Mathf.Abs(v.x) >= Mathf.Abs(v.y))
            return v.x >= 0f ? Vector2.right : Vector2.left;

        return v.y >= 0f ? Vector2.up : Vector2.down;
    }

    private void DisableAllAbilitySprites()
    {
        if (shotUp != null) shotUp.enabled = false;
        if (shotDown != null) shotDown.enabled = false;
        if (shotLeft != null) shotLeft.enabled = false;
        if (shotRight != null) shotRight.enabled = false;
    }

    private bool IsAbilitySprite(AnimatedSpriteRenderer s)
    {
        return s == shotUp || s == shotDown || s == shotLeft || s == shotRight;
    }

    private void CacheAndDisableAllNonAbilitySprites()
    {
        cached.Clear();

        var sprites = GetComponentsInChildren<AnimatedSpriteRenderer>(true);
        for (int i = 0; i < sprites.Length; i++)
        {
            var s = sprites[i];
            if (s == null)
                continue;

            if (IsAbilitySprite(s))
                continue;

            cached.Add(new CachedState
            {
                asr = s,
                enabled = s.enabled,
                idle = s.idle,
                loop = s.loop,
                pingPong = s.pingPong,
                frame = s.CurrentFrame
            });

            s.enabled = false;
        }
    }

    private void RestoreAllNonAbilitySprites()
    {
        for (int i = 0; i < cached.Count; i++)
        {
            var st = cached[i];
            if (st.asr == null)
                continue;

            st.asr.idle = st.idle;
            st.asr.loop = st.loop;
            st.asr.pingPong = st.pingPong;
            st.asr.CurrentFrame = st.frame;
            st.asr.enabled = st.enabled;

            if (st.asr.enabled)
                st.asr.RefreshFrame();
        }

        cached.Clear();
    }

    public void CancelForDeath()
    {
        playing = false;

        if (active != null)
        {
            active.enabled = false;

            if (forceFlipXFalse && active.TryGetComponent<SpriteRenderer>(out var sr) && sr != null)
                sr.flipX = false;
        }

        active = null;

        DisableAllAbilitySprites();

        cached.Clear();

        enabled = false;
    }
}

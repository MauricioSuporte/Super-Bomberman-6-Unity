using System.Collections.Generic;
using UnityEngine;

public class MoleMountDrillAnimator : MonoBehaviour, IMoleMountDrillExternalAnimator
{
    [Header("Phase 1 (Start)")]
    public AnimatedSpriteRenderer phase1;

    [Header("Phase 2 (Drill Loop)")]
    public AnimatedSpriteRenderer phase2;

    [Header("Phase 3 (Deep)")]
    public AnimatedSpriteRenderer phase3;

    [Header("Phase 2 Reverse (End)")]
    public AnimatedSpriteRenderer phase2Reverse;

    [Header("Phase Durations")]
    [SerializeField, Min(0.01f)] private float phase1Duration = 1f;
    [SerializeField, Min(0.01f)] private float phase2Duration = 0.5f;
    [SerializeField, Min(0.01f)] private float phase3Duration = 0.5f;
    [SerializeField, Min(0.01f)] private float phase2ReverseDuration = 0.5f;

    public float Phase1Duration => phase1Duration;
    public float Phase2Duration => phase2Duration;
    public float Phase3Duration => phase3Duration;
    public float Phase2ReverseDuration => phase2ReverseDuration;

    struct CachedState
    {
        public AnimatedSpriteRenderer asr;
        public bool enabled;
        public bool idle;
        public bool loop;
        public bool pingPong;
        public int frame;
    }

    readonly List<CachedState> cached = new();
    AnimatedSpriteRenderer active;
    bool playing;

    public void PlayPhase(int phase, Vector2 dir)
    {
        CacheAndDisableAllNonAbilitySprites();

        playing = true;

        active = GetByPhase(phase);
        DisableAllAbilitySprites();

        if (active != null)
        {
            active.enabled = true;
            active.idle = false;

            if (phase == 1 || phase == 2)
                active.loop = true;
            else
                active.loop = false;

            active.pingPong = false;
            active.CurrentFrame = 0;
            active.RefreshFrame();
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

    void LateUpdate()
    {
        if (!playing)
            return;

        for (int i = 0; i < cached.Count; i++)
        {
            var s = cached[i].asr;
            if (s == null)
                continue;

            if (s.enabled)
                s.enabled = false;
        }
    }

    AnimatedSpriteRenderer GetByPhase(int phase)
    {
        switch (phase)
        {
            case 1: return phase1;
            case 2: return phase2;
            case 3: return phase3;
            case 4: return phase2Reverse;
        }

        return phase1 != null ? phase1 :
               (phase2 != null ? phase2 :
               (phase3 != null ? phase3 : phase2Reverse));
    }

    void DisableAllAbilitySprites()
    {
        Disable(phase1);
        Disable(phase2);
        Disable(phase3);
        Disable(phase2Reverse);
    }

    static void Disable(AnimatedSpriteRenderer a)
    {
        if (a != null) a.enabled = false;
    }

    bool IsAbilitySprite(AnimatedSpriteRenderer s)
    {
        return s == phase1 || s == phase2 || s == phase3 || s == phase2Reverse;
    }

    void CacheAndDisableAllNonAbilitySprites()
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

    void RestoreAllNonAbilitySprites()
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
}

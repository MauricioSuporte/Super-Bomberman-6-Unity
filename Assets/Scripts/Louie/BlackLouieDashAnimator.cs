using System.Collections.Generic;
using UnityEngine;

public class BlackLouieDashAnimator : MonoBehaviour, IBlackLouieDashExternalAnimator
{
    public AnimatedSpriteRenderer dashUp;
    public AnimatedSpriteRenderer dashDown;
    public AnimatedSpriteRenderer dashLeft;
    public AnimatedSpriteRenderer dashRight;

    AnimatedSpriteRenderer activeDash;
    LouieRiderVisual riderVisual;

    readonly List<AnimatedSpriteRenderer> cachedAnimators = new();
    readonly List<bool> cachedAnimatorEnabled = new();

    readonly List<SpriteRenderer> cachedSpriteRenderers = new();
    readonly List<bool> cachedSpriteEnabled = new();

    bool playing;

    void Awake()
    {
        riderVisual = GetComponent<LouieRiderVisual>();
    }

    void OnDisable() => Stop();
    void OnDestroy() => Stop();

    public void Play(Vector2 dir)
    {
        if (playing)
            Stop();

        if (dir == Vector2.zero)
            dir = Vector2.down;

        CacheEnabledStates();

        if (riderVisual != null)
            riderVisual.enabled = false;

        DisableAllRenderers();

        activeDash = GetDashSprite(dir);

        if (activeDash != null)
        {
            if (activeDash.TryGetComponent<SpriteRenderer>(out var sr))
                sr.flipX = (dir == Vector2.right);

            activeDash.enabled = true;
            activeDash.idle = false;
            activeDash.loop = false;
            activeDash.CurrentFrame = 0;
            activeDash.RefreshFrame();

            if (activeDash.TryGetComponent<SpriteRenderer>(out var psr))
                psr.enabled = true;
        }

        playing = true;
    }

    public void Stop()
    {
        if (!playing)
            return;

        if (activeDash != null)
        {
            if (activeDash.TryGetComponent<SpriteRenderer>(out var sr))
                sr.flipX = false;

            activeDash.enabled = false;

            if (activeDash.TryGetComponent<SpriteRenderer>(out var psr))
                psr.enabled = false;
        }

        activeDash = null;

        RestoreEnabledStates();

        if (riderVisual != null)
            riderVisual.enabled = true;

        playing = false;
    }

    AnimatedSpriteRenderer GetDashSprite(Vector2 dir)
    {
        if (dir == Vector2.up) return dashUp;
        if (dir == Vector2.down) return dashDown;
        if (dir == Vector2.left) return dashLeft;
        if (dir == Vector2.right) return dashRight;
        return dashDown;
    }

    void CacheEnabledStates()
    {
        cachedAnimators.Clear();
        cachedAnimatorEnabled.Clear();
        cachedSpriteRenderers.Clear();
        cachedSpriteEnabled.Clear();

        var anims = GetComponentsInChildren<AnimatedSpriteRenderer>(true);
        for (int i = 0; i < anims.Length; i++)
        {
            cachedAnimators.Add(anims[i]);
            cachedAnimatorEnabled.Add(anims[i] != null && anims[i].enabled);
        }

        var srs = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < srs.Length; i++)
        {
            cachedSpriteRenderers.Add(srs[i]);
            cachedSpriteEnabled.Add(srs[i] != null && srs[i].enabled);
        }
    }

    void DisableAllRenderers()
    {
        for (int i = 0; i < cachedAnimators.Count; i++)
        {
            if (cachedAnimators[i] != null)
                cachedAnimators[i].enabled = false;
        }

        for (int i = 0; i < cachedSpriteRenderers.Count; i++)
        {
            if (cachedSpriteRenderers[i] != null)
                cachedSpriteRenderers[i].enabled = false;
        }
    }

    void RestoreEnabledStates()
    {
        for (int i = 0; i < cachedAnimators.Count; i++)
        {
            if (cachedAnimators[i] != null)
                cachedAnimators[i].enabled = cachedAnimatorEnabled[i];
        }

        for (int i = 0; i < cachedSpriteRenderers.Count; i++)
        {
            if (cachedSpriteRenderers[i] != null)
                cachedSpriteRenderers[i].enabled = cachedSpriteEnabled[i];
        }
    }
}

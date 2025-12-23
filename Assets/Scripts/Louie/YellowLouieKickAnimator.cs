using Assets.Scripts.Interface;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class YellowLouieKickAnimator : MonoBehaviour, IYellowLouieDestructibleKickExternalAnimator
{
    public AnimatedSpriteRenderer kickUp;
    public AnimatedSpriteRenderer kickDown;
    public AnimatedSpriteRenderer kickLeft;
    public AnimatedSpriteRenderer kickRight;

    AnimatedSpriteRenderer activeKick;
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

    void OnDisable()
    {
        Stop();
    }

    void OnDestroy()
    {
        Stop();
    }

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

        activeKick = GetKickSprite(dir);

        if (activeKick != null)
        {
            if (activeKick.TryGetComponent<SpriteRenderer>(out var sr))
                sr.flipX = (dir == Vector2.right);

            activeKick.enabled = true;
            activeKick.idle = false;
            activeKick.loop = false;
            activeKick.CurrentFrame = 0;
            activeKick.RefreshFrame();

            if (activeKick.TryGetComponent<SpriteRenderer>(out var ksr))
                ksr.enabled = true;
        }

        playing = true;
    }

    public void Stop()
    {
        if (!playing)
            return;

        if (activeKick != null)
        {
            if (activeKick.TryGetComponent<SpriteRenderer>(out var sr))
                sr.flipX = false;

            activeKick.enabled = false;

            if (activeKick.TryGetComponent<SpriteRenderer>(out var ksr))
                ksr.enabled = false;
        }

        activeKick = null;

        RestoreEnabledStates();

        if (riderVisual != null)
            riderVisual.enabled = true;

        playing = false;
    }

    AnimatedSpriteRenderer GetKickSprite(Vector2 dir)
    {
        if (dir == Vector2.up) return kickUp;
        if (dir == Vector2.down) return kickDown;
        if (dir == Vector2.left) return kickLeft;
        if (dir == Vector2.right) return kickRight;
        return kickDown;
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

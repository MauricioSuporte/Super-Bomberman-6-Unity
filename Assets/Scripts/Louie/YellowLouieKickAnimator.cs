using Assets.Scripts.Interface;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LouieRiderVisual))]
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

        if (riderVisual != null)
        {
            var owner = riderVisual.owner;
            if (owner != null && !owner.isDead)
            {
                var isIdle = owner.Direction == Vector2.zero;
                var faceDir = isIdle ? owner.FacingDirection : owner.Direction;

                AnimatedSpriteRenderer target;
                if (faceDir == Vector2.up) target = riderVisual.louieUp;
                else if (faceDir == Vector2.down) target = riderVisual.louieDown;
                else target = riderVisual.louieLeft;

                if (target != null)
                {
                    target.idle = isIdle;
                    if (target.TryGetComponent<SpriteRenderer>(out var tsr))
                    {
                        if (faceDir == Vector2.right) tsr.flipX = true;
                        else if (faceDir == Vector2.left) tsr.flipX = false;
                    }
                    target.RefreshFrame();
                }
            }
        }

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

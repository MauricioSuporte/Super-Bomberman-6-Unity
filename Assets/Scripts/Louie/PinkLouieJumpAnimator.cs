using Assets.Scripts.Interface;
using System.Collections.Generic;
using UnityEngine;

public class PinkLouieJumpAnimator : MonoBehaviour, IPinkLouieJumpExternalAnimator
{
    public AnimatedSpriteRenderer jumpUp;
    public AnimatedSpriteRenderer jumpDown;
    public AnimatedSpriteRenderer jumpLeft;
    public AnimatedSpriteRenderer jumpRight;

    [Header("Fix Right Local X")]
    public bool fixRightLocalX = true;
    public float rightLocalX = -0.3f;

    AnimatedSpriteRenderer active;
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

        active = GetSprite(dir);

        if (active != null)
        {
            if (active.TryGetComponent<SpriteRenderer>(out var sr))
                sr.flipX = (dir == Vector2.right);

            if (fixRightLocalX)
            {
                if (dir == Vector2.right)
                    active.SetRuntimeBaseLocalX(rightLocalX);
                else
                    active.ClearRuntimeBaseLocalX();
            }

            active.enabled = true;
            active.idle = false;
            active.loop = true;
            active.CurrentFrame = 0;
            active.RefreshFrame();

            if (active.TryGetComponent<SpriteRenderer>(out var asr))
                asr.enabled = true;
        }

        playing = true;
    }

    public void Stop()
    {
        if (!playing)
            return;

        if (active != null)
        {
            if (active.TryGetComponent<SpriteRenderer>(out var sr))
                sr.flipX = false;

            if (fixRightLocalX)
                active.ClearRuntimeBaseLocalX();

            active.enabled = false;

            if (active.TryGetComponent<SpriteRenderer>(out var asr))
                asr.enabled = false;
        }

        active = null;

        RestoreEnabledStates();

        if (riderVisual != null)
            riderVisual.enabled = true;

        playing = false;
    }

    AnimatedSpriteRenderer GetSprite(Vector2 dir)
    {
        if (dir == Vector2.up) return jumpUp;
        if (dir == Vector2.down) return jumpDown;
        if (dir == Vector2.left) return jumpLeft;
        if (dir == Vector2.right) return jumpRight;
        return jumpDown;
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
            if (cachedAnimators[i] != null) cachedAnimators[i].enabled = false;

        for (int i = 0; i < cachedSpriteRenderers.Count; i++)
            if (cachedSpriteRenderers[i] != null) cachedSpriteRenderers[i].enabled = false;
    }

    void RestoreEnabledStates()
    {
        for (int i = 0; i < cachedAnimators.Count; i++)
            if (cachedAnimators[i] != null) cachedAnimators[i].enabled = cachedAnimatorEnabled[i];

        for (int i = 0; i < cachedSpriteRenderers.Count; i++)
            if (cachedSpriteRenderers[i] != null) cachedSpriteRenderers[i].enabled = cachedSpriteEnabled[i];
    }
}

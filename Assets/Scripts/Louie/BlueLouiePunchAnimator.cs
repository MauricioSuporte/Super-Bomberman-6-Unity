using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class BlueLouiePunchAnimator : MonoBehaviour, IBombPunchExternalAnimator
{
    public AnimatedSpriteRenderer punchUp;
    public AnimatedSpriteRenderer punchDown;
    public AnimatedSpriteRenderer punchLeft;
    public AnimatedSpriteRenderer punchRight;

    AnimatedSpriteRenderer activePunch;
    LouieRiderVisual riderVisual;

    readonly List<AnimatedSpriteRenderer> cachedAnimators = new();
    readonly List<bool> cachedAnimatorEnabled = new();

    readonly List<SpriteRenderer> cachedSpriteRenderers = new();
    readonly List<bool> cachedSpriteEnabled = new();

    bool playing;

    void Awake()
    {
        riderVisual = GetComponent<LouieRiderVisual>();
        if (riderVisual == null)
            riderVisual = GetComponentInParent<LouieRiderVisual>();
        if (riderVisual == null)
            riderVisual = GetComponentInChildren<LouieRiderVisual>(true);
    }

    void OnDisable() => ForceStop();
    void OnDestroy() => ForceStop();

    public IEnumerator Play(Vector2 dir, float punchLockTime)
    {
        if (playing)
            ForceStop();

        if (dir == Vector2.zero)
            dir = Vector2.down;

        CacheEnabledStates();

        if (riderVisual != null)
            riderVisual.enabled = false;

        DisableAllRenderers();

        activePunch = GetPunchSprite(dir);

        if (activePunch != null)
        {
            if (activePunch.TryGetComponent<SpriteRenderer>(out var sr))
            {
                sr.enabled = true;
                sr.flipX = (dir == Vector2.right);
            }

            activePunch.enabled = true;
            activePunch.idle = false;
            activePunch.loop = false;
            activePunch.CurrentFrame = 0;
            activePunch.RefreshFrame();
        }

        playing = true;

        yield return new WaitForSeconds(punchLockTime);

        ForceStop();
    }

    public void ForceStop()
    {
        if (!playing && activePunch == null && cachedAnimators.Count == 0 && cachedSpriteRenderers.Count == 0)
            return;

        if (activePunch != null)
        {
            if (activePunch.TryGetComponent<SpriteRenderer>(out var sr))
            {
                sr.flipX = false;
                sr.enabled = false;
            }

            activePunch.enabled = false;
        }

        activePunch = null;

        RestoreEnabledStates();

        if (riderVisual != null)
            riderVisual.enabled = true;

        playing = false;
    }

    AnimatedSpriteRenderer GetPunchSprite(Vector2 dir)
    {
        if (dir == Vector2.up) return punchUp;
        if (dir == Vector2.down) return punchDown;
        if (dir == Vector2.left) return punchLeft;
        if (dir == Vector2.right) return punchRight;
        return punchDown;
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

        cachedAnimators.Clear();
        cachedAnimatorEnabled.Clear();
        cachedSpriteRenderers.Clear();
        cachedSpriteEnabled.Clear();
    }
}

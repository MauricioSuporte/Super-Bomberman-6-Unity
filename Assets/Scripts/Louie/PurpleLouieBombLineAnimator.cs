using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PurpleLouieBombLineAnimator : MonoBehaviour, IPurpleLouieBombLineExternalAnimator
{
    [Header("Magic Sprites (PURPLE LOUIE)")]
    public AnimatedSpriteRenderer magicUp;
    public AnimatedSpriteRenderer magicDown;
    public AnimatedSpriteRenderer magicLeft;
    public AnimatedSpriteRenderer magicRight;

    AnimatedSpriteRenderer activeMagic;
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

    public IEnumerator Play(Vector2 dir, float lockSeconds)
    {
        if (playing)
            ForceStop();

        if (dir == Vector2.zero)
            dir = Vector2.down;

        CacheEnabledStates();

        if (riderVisual != null)
            riderVisual.enabled = false;

        DisableAllRenderers();

        activeMagic = GetMagic(dir);

        if (activeMagic != null)
        {
            if (activeMagic.TryGetComponent<SpriteRenderer>(out var sr))
            {
                sr.enabled = true;
                sr.flipX = (dir == Vector2.right);
            }

            activeMagic.enabled = true;
            activeMagic.idle = false;
            activeMagic.loop = false;
            activeMagic.CurrentFrame = 0;
            activeMagic.RefreshFrame();
        }

        playing = true;

        yield return new WaitForSeconds(lockSeconds);

        ForceStop();
    }

    public void ForceStop()
    {
        if (!playing && activeMagic == null && cachedAnimators.Count == 0 && cachedSpriteRenderers.Count == 0)
            return;

        if (activeMagic != null)
        {
            if (activeMagic.TryGetComponent<SpriteRenderer>(out var sr))
            {
                sr.flipX = false;
                sr.enabled = false;
            }

            activeMagic.enabled = false;
        }

        activeMagic = null;

        RestoreEnabledStates();

        if (riderVisual != null)
            riderVisual.enabled = true;

        playing = false;
    }

    AnimatedSpriteRenderer GetMagic(Vector2 dir)
    {
        if (dir == Vector2.up) return magicUp;
        if (dir == Vector2.down) return magicDown;
        if (dir == Vector2.left) return magicLeft;
        if (dir == Vector2.right) return magicRight;
        return magicDown;
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

using Assets.Scripts.Interface;
using System.Collections.Generic;
using UnityEngine;

public class RedLouiePunchAnimator : MonoBehaviour, IRedLouiePunchExternalAnimator
{
    public AnimatedSpriteRenderer punchUp;
    public AnimatedSpriteRenderer punchDown;
    public AnimatedSpriteRenderer punchLeft;
    public AnimatedSpriteRenderer punchRight;

    AnimatedSpriteRenderer activePunch;
    LouieRidingVisual riderVisual;

    readonly List<AnimatedSpriteRenderer> cachedAnimators = new();
    readonly List<bool> cachedAnimatorEnabled = new();

    readonly List<SpriteRenderer> cachedSpriteRenderers = new();
    readonly List<bool> cachedSpriteEnabled = new();

    readonly List<GameObject> cachedDirectionObjects = new();
    readonly List<bool> cachedDirectionObjectsActive = new();

    bool playing;

    void Awake()
    {
        riderVisual = GetComponent<LouieRidingVisual>();
        if (riderVisual == null)
            riderVisual = GetComponentInParent<LouieRidingVisual>();
        if (riderVisual == null)
            riderVisual = GetComponentInChildren<LouieRidingVisual>(true);
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
        DisableDirectionalObjects();

        activePunch = GetPunchSprite(dir);

        if (activePunch != null)
        {
            if (activePunch.TryGetComponent<SpriteRenderer>(out var sr))
                sr.flipX = (dir == Vector2.right);

            activePunch.enabled = true;
            activePunch.idle = false;
            activePunch.loop = false;
            activePunch.CurrentFrame = 0;
            activePunch.RefreshFrame();

            if (activePunch.TryGetComponent<SpriteRenderer>(out var psr))
                psr.enabled = true;
        }

        playing = true;
    }

    public void Stop()
    {
        if (!playing && activePunch == null && cachedAnimators.Count == 0 && cachedSpriteRenderers.Count == 0 && cachedDirectionObjects.Count == 0)
            return;

        if (activePunch != null)
        {
            if (activePunch.TryGetComponent<SpriteRenderer>(out var sr))
                sr.flipX = false;

            activePunch.enabled = false;

            if (activePunch.TryGetComponent<SpriteRenderer>(out var psr))
                psr.enabled = false;
        }

        activePunch = null;

        RestoreEnabledStates();
        RestoreDirectionalObjects();

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
        cachedDirectionObjects.Clear();
        cachedDirectionObjectsActive.Clear();

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

        CacheDirectionObjectByName("Up");
        CacheDirectionObjectByName("Down");
        CacheDirectionObjectByName("Left");
        CacheDirectionObjectByName("Right");
    }

    void CacheDirectionObjectByName(string childName)
    {
        var t = transform.Find(childName);
        if (t == null)
        {
            t = transform.parent != null ? transform.parent.Find(childName) : null;
            if (t == null && transform.root != null)
                t = transform.root.Find(childName);
        }

        if (t == null)
            return;

        cachedDirectionObjects.Add(t.gameObject);
        cachedDirectionObjectsActive.Add(t.gameObject.activeSelf);
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

    void DisableDirectionalObjects()
    {
        for (int i = 0; i < cachedDirectionObjects.Count; i++)
        {
            var go = cachedDirectionObjects[i];
            if (go != null)
                go.SetActive(false);
        }
    }

    void RestoreDirectionalObjects()
    {
        for (int i = 0; i < cachedDirectionObjects.Count; i++)
        {
            var go = cachedDirectionObjects[i];
            if (go != null)
                go.SetActive(cachedDirectionObjectsActive[i]);
        }

        cachedDirectionObjects.Clear();
        cachedDirectionObjectsActive.Clear();
    }
}

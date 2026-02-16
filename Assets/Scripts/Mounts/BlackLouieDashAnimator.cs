using System.Collections.Generic;
using UnityEngine;

public class BlackLouieDashAnimator : MonoBehaviour, IBlackLouieDashExternalAnimator
{
    public AnimatedSpriteRenderer dashUp;
    public AnimatedSpriteRenderer dashDown;
    public AnimatedSpriteRenderer dashLeft;
    public AnimatedSpriteRenderer dashRight;

    AnimatedSpriteRenderer activeDash;
    MountVisualController riderVisual;

    readonly List<AnimatedSpriteRenderer> cachedAnimators = new();
    readonly List<bool> cachedAnimatorEnabled = new();

    readonly List<SpriteRenderer> cachedSpriteRenderers = new();
    readonly List<bool> cachedSpriteEnabled = new();

    readonly List<GameObject> cachedDirectionObjects = new();
    readonly List<bool> cachedDirectionObjectsActive = new();

    bool playing;

    void Awake()
    {
        riderVisual = GetComponent<MountVisualController>();
        if (riderVisual == null)
            riderVisual = GetComponentInParent<MountVisualController>();
        if (riderVisual == null)
            riderVisual = GetComponentInChildren<MountVisualController>(true);
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
        if (!playing && activeDash == null && cachedAnimators.Count == 0 && cachedSpriteRenderers.Count == 0 && cachedDirectionObjects.Count == 0)
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
        RestoreDirectionalObjects();

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

    void DisableDirectionalObjects()
    {
        for (int i = 0; i < cachedDirectionObjects.Count; i++)
        {
            var go = cachedDirectionObjects[i];
            if (go != null)
                go.SetActive(false);
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

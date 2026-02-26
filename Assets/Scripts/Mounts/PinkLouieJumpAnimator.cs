using Assets.Scripts.Interface;
using UnityEngine;
using System.Collections.Generic;

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
    MountVisualController riderVisual;

    readonly List<GameObject> cachedDirectionObjects = new();
    readonly List<bool> cachedDirectionObjectsActive = new();

    readonly List<Behaviour> cachedBehaviours = new();
    readonly List<bool> cachedBehavioursEnabled = new();

    readonly List<Renderer> cachedRenderers = new();
    readonly List<bool> cachedRenderersEnabled = new();

    bool playing;

    void Awake()
    {
        riderVisual = GetComponent<MountVisualController>();
        if (riderVisual == null)
            riderVisual = GetComponentInParent<MountVisualController>();
        if (riderVisual == null)
            riderVisual = GetComponentInChildren<MountVisualController>(true);

        ForceOffJumpSprites();
    }

    void OnDisable() => Stop();
    void OnDestroy() => Stop();

    public void Play(Vector2 dir)
    {
        if (playing)
            Stop();

        if (dir == Vector2.zero)
            dir = Vector2.down;

        CacheDirectionalObjects();
        DisableDirectionalObjects();

        ForceDisableRiderVisualRenderers();

        if (riderVisual != null)
            riderVisual.enabled = false;

        active = GetSprite(dir);

        SetRendererEnabled(jumpUp, active == jumpUp);
        SetRendererEnabled(jumpDown, active == jumpDown);
        SetRendererEnabled(jumpLeft, active == jumpLeft);
        SetRendererEnabled(jumpRight, active == jumpRight);

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
            else
            {
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
        if (!playing && active == null && cachedDirectionObjects.Count == 0 && cachedBehaviours.Count == 0 && cachedRenderers.Count == 0)
            return;

        if (active != null)
        {
            if (active.TryGetComponent<SpriteRenderer>(out var sr))
                sr.flipX = false;

            if (fixRightLocalX)
                active.ClearRuntimeBaseLocalX();

            SetRendererEnabled(active, false);
        }

        active = null;

        ForceOffJumpSprites();

        if (riderVisual != null)
        {
            riderVisual.enabled = true;
            ForceDisableRiderVisualRenderers();
        }

        RestoreDirectionalObjects();

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

    void ForceOffJumpSprites()
    {
        SetRendererEnabled(jumpUp, false);
        SetRendererEnabled(jumpDown, false);
        SetRendererEnabled(jumpLeft, false);
        SetRendererEnabled(jumpRight, false);
    }

    void SetRendererEnabled(AnimatedSpriteRenderer r, bool on)
    {
        if (r == null)
            return;

        r.enabled = on;

        if (r.TryGetComponent<SpriteRenderer>(out var sr))
            sr.enabled = on;
    }

    void ForceDisableRiderVisualRenderers()
    {
        if (riderVisual == null)
            return;

        SetRendererEnabled(riderVisual.louieUp, false);
        SetRendererEnabled(riderVisual.louieDown, false);
        SetRendererEnabled(riderVisual.louieLeft, false);
        SetRendererEnabled(riderVisual.louieRight, false);
        SetRendererEnabled(riderVisual.louieEndStage, false);
    }

    void CacheDirectionalObjects()
    {
        cachedDirectionObjects.Clear();
        cachedDirectionObjectsActive.Clear();

        cachedBehaviours.Clear();
        cachedBehavioursEnabled.Clear();

        cachedRenderers.Clear();
        cachedRenderersEnabled.Clear();

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

        var go = t.gameObject;

        cachedDirectionObjects.Add(go);
        cachedDirectionObjectsActive.Add(go.activeSelf);

        var anims = go.GetComponentsInChildren<AnimatedSpriteRenderer>(true);
        for (int i = 0; i < anims.Length; i++)
        {
            if (anims[i] == null)
                continue;

            cachedBehaviours.Add(anims[i]);
            cachedBehavioursEnabled.Add(anims[i].enabled);
        }

        var renderers = go.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;

            cachedRenderers.Add(renderers[i]);
            cachedRenderersEnabled.Add(renderers[i].enabled);
        }
    }

    void DisableDirectionalObjects()
    {
        for (int i = 0; i < cachedBehaviours.Count; i++)
        {
            var b = cachedBehaviours[i];
            if (b != null)
                b.enabled = false;
        }

        for (int i = 0; i < cachedRenderers.Count; i++)
        {
            var r = cachedRenderers[i];
            if (r != null)
                r.enabled = false;
        }

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

        for (int i = 0; i < cachedBehaviours.Count; i++)
        {
            var b = cachedBehaviours[i];
            if (b != null)
                b.enabled = cachedBehavioursEnabled[i];
        }

        for (int i = 0; i < cachedRenderers.Count; i++)
        {
            var r = cachedRenderers[i];
            if (r != null)
                r.enabled = cachedRenderersEnabled[i];
        }

        cachedDirectionObjects.Clear();
        cachedDirectionObjectsActive.Clear();

        cachedBehaviours.Clear();
        cachedBehavioursEnabled.Clear();

        cachedRenderers.Clear();
        cachedRenderersEnabled.Clear();
    }

    public void CancelForDeath()
    {
        if (active != null)
        {
            if (active.TryGetComponent<SpriteRenderer>(out var sr))
                sr.flipX = false;

            if (fixRightLocalX)
                active.ClearRuntimeBaseLocalX();

            SetRendererEnabled(active, false);
        }

        active = null;
        playing = false;

        ForceOffJumpSprites();

        cachedDirectionObjects.Clear();
        cachedDirectionObjectsActive.Clear();

        cachedBehaviours.Clear();
        cachedBehavioursEnabled.Clear();

        cachedRenderers.Clear();
        cachedRenderersEnabled.Clear();

        enabled = false;
    }
}

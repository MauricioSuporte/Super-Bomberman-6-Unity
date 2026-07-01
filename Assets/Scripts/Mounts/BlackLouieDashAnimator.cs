using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BlackLouieDashAnimator : MonoBehaviour, IBlackLouieDashExternalAnimator
{
    public AnimatedSpriteRenderer dashUp;
    public AnimatedSpriteRenderer dashDown;
    public AnimatedSpriteRenderer dashLeft;
    public AnimatedSpriteRenderer dashRight;

    [Header("Dash Afterimage")]
    [SerializeField, Min(0.01f)] private float afterimageInterval = 0.06f;
    [SerializeField, Min(0.01f)] private float afterimageDuration = 0.22f;
    [SerializeField, Range(0f, 1f)] private float afterimageInitialAlpha = 0.45f;
    [SerializeField] private Color afterimageTint = new(0.65f, 0.65f, 0.75f, 1f);

    AnimatedSpriteRenderer activeDash;
    MountVisualController riderVisual;

    readonly List<AnimatedSpriteRenderer> cachedAnimators = new();
    readonly List<bool> cachedAnimatorEnabled = new();

    readonly List<SpriteRenderer> cachedSpriteRenderers = new();
    readonly List<bool> cachedSpriteEnabled = new();

    readonly List<GameObject> cachedDirectionObjects = new();
    readonly List<bool> cachedDirectionObjectsActive = new();

    bool playing;
    bool emitAfterimages;
    float nextAfterimageTime;

    Coroutine holdRoutine;

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
        emitAfterimages = true;
        nextAfterimageTime = Time.time;
    }

    /// <summary>
    /// Congela imediatamente no último frame (clampando via int.MaxValue) por alguns segundos,
    /// e só então executa Stop() (restaurando estados anteriores).
    /// </summary>
    public void HoldImpact(float seconds)
    {
        if (!playing || activeDash == null)
            return;

        seconds = Mathf.Max(0f, seconds);

        if (holdRoutine != null)
        {
            StopCoroutine(holdRoutine);
            holdRoutine = null;
        }

        ForceLastFrameAndFreeze(activeDash);
        emitAfterimages = false;

        if (seconds <= 0f)
        {
            Stop();
            return;
        }

        holdRoutine = StartCoroutine(HoldThenStopRoutine(seconds));
    }

    IEnumerator HoldThenStopRoutine(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        holdRoutine = null;
        Stop();
    }

    void ForceLastFrameAndFreeze(AnimatedSpriteRenderer anim)
    {
        if (anim == null)
            return;

        anim.loop = false;

        // Força o último frame (muito comum renderers clamparem internamente).
        anim.CurrentFrame = int.MaxValue;
        anim.RefreshFrame();

        // Garante que ele não continue avançando frames.
        anim.idle = true;

        // Garante visibilidade.
        anim.enabled = true;

        if (anim.TryGetComponent<SpriteRenderer>(out var sr))
            sr.enabled = true;
    }

    public void Stop()
    {
        if (holdRoutine != null)
        {
            StopCoroutine(holdRoutine);
            holdRoutine = null;
        }

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
        emitAfterimages = false;
    }

    void LateUpdate()
    {
        if (!playing || !emitAfterimages || activeDash == null)
            return;

        if (Time.time < nextAfterimageTime)
            return;

        SpawnAfterimage();
        nextAfterimageTime = Time.time + Mathf.Max(0.01f, afterimageInterval);
    }

    void SpawnAfterimage()
    {
        if (activeDash == null || !activeDash.enabled ||
            !activeDash.TryGetComponent(out SpriteRenderer source) ||
            source == null || !source.enabled || source.sprite == null)
            return;

        GameObject ghost = new($"{name}_DashAfterimage");
        ghost.layer = source.gameObject.layer;
        ghost.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
        ghost.transform.localScale = source.transform.lossyScale;

        SpriteRenderer ghostRenderer = ghost.AddComponent<SpriteRenderer>();
        ghostRenderer.sprite = source.sprite;
        ghostRenderer.flipX = source.flipX;
        ghostRenderer.flipY = source.flipY;
        ghostRenderer.sortingLayerID = source.sortingLayerID;
        ghostRenderer.sortingOrder = source.sortingOrder - 1;
        ghostRenderer.maskInteraction = source.maskInteraction;
        ghostRenderer.spriteSortPoint = source.spriteSortPoint;
        ghostRenderer.sharedMaterial = source.sharedMaterial;

        Color color = source.color * afterimageTint;
        color.a = source.color.a * afterimageTint.a * Mathf.Clamp01(afterimageInitialAlpha);
        ghostRenderer.color = color;

        LouieDashAfterimage fade = ghost.AddComponent<LouieDashAfterimage>();
        fade.Initialize(ghostRenderer, Mathf.Max(0.01f, afterimageDuration));
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

    public void CancelForDeath()
    {
        if (holdRoutine != null)
        {
            StopCoroutine(holdRoutine);
            holdRoutine = null;
        }

        if (activeDash != null)
        {
            if (activeDash.TryGetComponent<SpriteRenderer>(out var sr))
                sr.flipX = false;

            activeDash.enabled = false;

            if (activeDash.TryGetComponent<SpriteRenderer>(out var psr))
                psr.enabled = false;
        }

        activeDash = null;
        playing = false;
        emitAfterimages = false;

        cachedAnimators.Clear();
        cachedAnimatorEnabled.Clear();
        cachedSpriteRenderers.Clear();
        cachedSpriteEnabled.Clear();
        cachedDirectionObjects.Clear();
        cachedDirectionObjectsActive.Clear();

        enabled = false;
    }
}

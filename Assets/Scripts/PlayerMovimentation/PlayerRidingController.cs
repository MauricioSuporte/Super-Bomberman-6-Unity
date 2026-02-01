using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MovementController))]
public sealed class PlayerRidingController : MonoBehaviour
{
    [Header("Riding Sprites (Directional)")]
    public AnimatedSpriteRenderer ridingUp;
    public AnimatedSpriteRenderer ridingDown;
    public AnimatedSpriteRenderer ridingLeft;
    public AnimatedSpriteRenderer ridingRight;

    [Header("Timing")]
    public float ridingSeconds = 1f;

    MovementController movement;
    BombController bomb;

    Coroutine routine;
    bool isPlaying;

    SpriteRenderer[] cachedSpriteRenderers;
    AnimatedSpriteRenderer[] cachedAnimatedRenderers;

    readonly HashSet<AnimatedSpriteRenderer> allowedAnims = new();
    readonly HashSet<SpriteRenderer> allowedSrs = new();

    public bool IsPlaying => isPlaying;

    void Awake()
    {
        movement = GetComponent<MovementController>();
        bomb = GetComponent<BombController>();

        CacheRenderers();
        RebuildAllowedSets();

        SetAnimEnabled(ridingUp, false);
        SetAnimEnabled(ridingDown, false);
        SetAnimEnabled(ridingLeft, false);
        SetAnimEnabled(ridingRight, false);
    }

    void OnValidate()
    {
        RebuildAllowedSets();
    }

    void LateUpdate()
    {
        if (!isPlaying)
            return;

        EnforceRidingOnly();
    }

    public bool TryPlayRiding(Vector2 facing, System.Action onComplete = null, System.Action onStart = null)
    {
        if (isPlaying || movement == null)
            return false;

        if (routine != null)
            StopCoroutine(routine);

        isPlaying = true;

        CacheRenderers();
        RebuildAllowedSets();

        movement.SetInputLocked(true, forceIdle: true);
        movement.SetAllSpritesVisible(false);

        var r = PickRidingRenderer(facing);

        DisableAllRiding();

        if (r != null)
        {
            SetAnimEnabled(r, true);
            r.CurrentFrame = 0;
            r.RefreshFrame();
        }

        onStart?.Invoke();

        routine = StartCoroutine(FinishRoutine(onComplete));
        return true;
    }

    IEnumerator FinishRoutine(System.Action onComplete)
    {
        yield return null;

        yield return new WaitForSeconds(ridingSeconds);

        DisableAllRiding();

        onComplete?.Invoke();

        movement.EnableExclusiveFromState();
        movement.SetInputLocked(false, forceIdle: true);

        isPlaying = false;
        routine = null;
    }

    void CacheRenderers()
    {
        cachedSpriteRenderers = movement != null
            ? movement.GetComponentsInChildren<SpriteRenderer>(true)
            : GetComponentsInChildren<SpriteRenderer>(true);

        cachedAnimatedRenderers = movement != null
            ? movement.GetComponentsInChildren<AnimatedSpriteRenderer>(true)
            : GetComponentsInChildren<AnimatedSpriteRenderer>(true);
    }

    void RebuildAllowedSets()
    {
        allowedAnims.Clear();
        allowedSrs.Clear();

        AddAllowed(ridingUp);
        AddAllowed(ridingDown);
        AddAllowed(ridingLeft);
        AddAllowed(ridingRight);
    }

    void AddAllowed(AnimatedSpriteRenderer r)
    {
        if (r == null)
            return;

        allowedAnims.Add(r);

        var srs = r.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < srs.Length; i++)
            if (srs[i] != null)
                allowedSrs.Add(srs[i]);
    }

    void EnforceRidingOnly()
    {
        if (cachedAnimatedRenderers != null)
        {
            for (int i = 0; i < cachedAnimatedRenderers.Length; i++)
            {
                var a = cachedAnimatedRenderers[i];
                if (a == null) continue;

                if (!allowedAnims.Contains(a) && a.enabled)
                    a.enabled = false;
            }
        }

        if (cachedSpriteRenderers != null)
        {
            for (int i = 0; i < cachedSpriteRenderers.Length; i++)
            {
                var sr = cachedSpriteRenderers[i];
                if (sr == null) continue;

                if (!allowedSrs.Contains(sr) && sr.enabled)
                    sr.enabled = false;
            }
        }
    }

    AnimatedSpriteRenderer PickRidingRenderer(Vector2 facing)
    {
        var f = facing;
        if (f == Vector2.zero)
            f = movement != null ? movement.FacingDirection : Vector2.down;

        if (f == Vector2.up) return ridingUp;
        if (f == Vector2.down) return ridingDown;
        if (f == Vector2.left) return ridingLeft;
        if (f == Vector2.right) return ridingRight;

        return ridingDown;
    }

    void DisableAllRiding()
    {
        SetAnimEnabled(ridingUp, false);
        SetAnimEnabled(ridingDown, false);
        SetAnimEnabled(ridingLeft, false);
        SetAnimEnabled(ridingRight, false);
    }

    static void SetAnimEnabled(AnimatedSpriteRenderer r, bool on)
    {
        if (r == null) return;

        r.enabled = on;

        if (r.TryGetComponent<SpriteRenderer>(out var sr))
            sr.enabled = on;
    }
}

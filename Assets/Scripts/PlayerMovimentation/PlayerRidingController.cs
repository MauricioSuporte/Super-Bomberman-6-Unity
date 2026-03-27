using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MovementController))]
public sealed class PlayerRidingController : MonoBehaviour
{
    public enum RidingArcType
    {
        Mount,
        Dismount
    }

    [Header("Mount Ascend Sprites")]
    public AnimatedSpriteRenderer mountAscendUp;
    public AnimatedSpriteRenderer mountAscendDown;
    public AnimatedSpriteRenderer mountAscendLeft;
    public AnimatedSpriteRenderer mountAscendRight;

    [Header("Mount Descend Sprites")]
    public AnimatedSpriteRenderer mountDescendUp;
    public AnimatedSpriteRenderer mountDescendDown;
    public AnimatedSpriteRenderer mountDescendLeft;
    public AnimatedSpriteRenderer mountDescendRight;

    [Header("Timing")]
    public float ridingSeconds = 1f;

    [Header("Arc")]
    [SerializeField] private float pixelsPerUnit = 16f;
    [SerializeField] private int jumpPeakPixels = 32;
    [SerializeField] private bool quantizeToPixel = true;

    [Header("Mount Heights (pixels)")]
    [SerializeField] private int mountStartHeightPixels = 0;
    [SerializeField] private int mountEndHeightPixels = 0;

    [Header("Dismount Heights (pixels)")]
    [SerializeField] private int dismountStartHeightPixels = 0;
    [SerializeField] private int dismountEndHeightPixels = 0;

    MovementController movement;
    BombController bomb;

    Coroutine routine;
    bool isPlaying;

    SpriteRenderer[] cachedSpriteRenderers;
    AnimatedSpriteRenderer[] cachedAnimatedRenderers;

    readonly HashSet<AnimatedSpriteRenderer> allowedAnims = new HashSet<AnimatedSpriteRenderer>();
    readonly HashSet<SpriteRenderer> allowedSrs = new HashSet<SpriteRenderer>();

    public bool IsPlaying => isPlaying;

    float JumpPeakWorld => jumpPeakPixels / Mathf.Max(1f, pixelsPerUnit);

    void Awake()
    {
        movement = GetComponent<MovementController>();
        bomb = GetComponent<BombController>();

        CacheRenderers();
        RebuildAllowedSets();
        DisableAllRiding();
        ClearAllRuntimeOffsets();
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
        return TryPlayMount(facing, onComplete, onStart);
    }

    public bool TryPlayMount(Vector2 facing, System.Action onComplete = null, System.Action onStart = null)
    {
        Vector3 startWorldPos = transform.position;
        Vector3 targetWorldPos = transform.position;

        return TryPlayMountArcInternal(
            RidingArcType.Mount,
            facing,
            startWorldPos,
            targetWorldPos,
            onComplete,
            onStart);
    }

    public bool TryPlayDismount(Vector2 facing, System.Action onComplete = null, System.Action onStart = null)
    {
        Vector3 startWorldPos = transform.position;
        Vector3 targetWorldPos = transform.position;

        return TryPlayMountArcInternal(
            RidingArcType.Dismount,
            facing,
            startWorldPos,
            targetWorldPos,
            onComplete,
            onStart);
    }

    public bool TryPlayMountArc(
        Vector2 facing,
        Vector3 startWorldPos,
        Vector3 targetWorldPos,
        System.Action onComplete = null,
        System.Action onStart = null)
    {
        return TryPlayMountArcInternal(
            RidingArcType.Mount,
            facing,
            startWorldPos,
            targetWorldPos,
            onComplete,
            onStart);
    }

    public bool TryPlayDismountArc(
        Vector2 facing,
        Vector3 startWorldPos,
        Vector3 targetWorldPos,
        System.Action onComplete = null,
        System.Action onStart = null)
    {
        return TryPlayMountArcInternal(
            RidingArcType.Dismount,
            facing,
            startWorldPos,
            targetWorldPos,
            onComplete,
            onStart);
    }

    bool TryPlayMountArcInternal(
        RidingArcType arcType,
        Vector2 facing,
        Vector3 startWorldPos,
        Vector3 targetWorldPos,
        System.Action onComplete = null,
        System.Action onStart = null)
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

        DisableAllRiding();
        ClearAllRuntimeOffsets();

        onStart?.Invoke();

        routine = StartCoroutine(PlayMountArcRoutine(
            arcType,
            facing,
            startWorldPos,
            targetWorldPos,
            onComplete));

        return true;
    }

    public bool CancelRiding()
    {
        if (!isPlaying)
            return false;

        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        DisableAllRiding();
        ClearAllRuntimeOffsets();

        if (movement != null)
        {
            movement.EnableExclusiveFromState();
            movement.SetInputLocked(false, forceIdle: true);
        }

        isPlaying = false;
        return true;
    }

    IEnumerator PlayMountArcRoutine(
        RidingArcType arcType,
        Vector2 facing,
        Vector3 startWorldPos,
        Vector3 targetWorldPos,
        System.Action onComplete)
    {
        yield return null;

        float duration = Mathf.Max(0.01f, ridingSeconds);
        float elapsed = 0f;

        float startHeightWorld = GetStartHeightWorld(arcType);
        float endHeightWorld = GetEndHeightWorld(arcType);

        transform.position = startWorldPos;

        while (elapsed < duration)
        {
            if (GamePauseController.IsPaused)
            {
                yield return null;
                continue;
            }

            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / duration);

            Vector3 flat = Vector3.Lerp(startWorldPos, targetWorldPos, t);

            float baseHeight = Mathf.Lerp(startHeightWorld, endHeightWorld, t);
            float parabolaHeight = 4f * JumpPeakWorld * t * (1f - t);
            float totalHeight = baseHeight + parabolaHeight;

            if (quantizeToPixel)
                totalHeight = QuantizeWorldToPixel(totalHeight);

            AnimatedSpriteRenderer renderer = PickRendererForPhase(facing, t < 0.5f);

            ApplyExclusiveRenderer(renderer);
            ApplyRendererArcOffset(renderer, totalHeight);

            flat.z = transform.position.z;
            transform.position = flat;

            yield return null;
        }

        Vector3 finalPos = targetWorldPos;
        finalPos.z = transform.position.z;
        transform.position = finalPos;

        AnimatedSpriteRenderer finalRenderer = PickRendererForPhase(facing, false);
        ApplyExclusiveRenderer(finalRenderer);
        ApplyRendererArcOffset(finalRenderer, endHeightWorld);

        DisableAllRiding();
        ClearAllRuntimeOffsets();

        onComplete?.Invoke();

        movement.EnableExclusiveFromState();
        movement.SetInputLocked(false, forceIdle: true);

        isPlaying = false;
        routine = null;
    }

    float GetStartHeightWorld(RidingArcType arcType)
    {
        int pixels = arcType == RidingArcType.Mount
            ? mountStartHeightPixels
            : dismountStartHeightPixels;

        return PixelsToWorld(pixels);
    }

    float GetEndHeightWorld(RidingArcType arcType)
    {
        int pixels = arcType == RidingArcType.Mount
            ? mountEndHeightPixels
            : dismountEndHeightPixels;

        return PixelsToWorld(pixels);
    }

    float PixelsToWorld(float pixels)
    {
        return pixels / Mathf.Max(1f, pixelsPerUnit);
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

        AddAllowed(mountAscendUp);
        AddAllowed(mountAscendDown);
        AddAllowed(mountAscendLeft);
        AddAllowed(mountAscendRight);

        AddAllowed(mountDescendUp);
        AddAllowed(mountDescendDown);
        AddAllowed(mountDescendLeft);
        AddAllowed(mountDescendRight);
    }

    void AddAllowed(AnimatedSpriteRenderer r)
    {
        if (r == null)
            return;

        allowedAnims.Add(r);

        SpriteRenderer[] srs = r.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < srs.Length; i++)
        {
            if (srs[i] != null)
                allowedSrs.Add(srs[i]);
        }
    }

    void EnforceRidingOnly()
    {
        if (cachedAnimatedRenderers != null)
        {
            for (int i = 0; i < cachedAnimatedRenderers.Length; i++)
            {
                AnimatedSpriteRenderer a = cachedAnimatedRenderers[i];
                if (a == null)
                    continue;

                if (!allowedAnims.Contains(a) && a.enabled)
                    a.enabled = false;
            }
        }

        if (cachedSpriteRenderers != null)
        {
            for (int i = 0; i < cachedSpriteRenderers.Length; i++)
            {
                SpriteRenderer sr = cachedSpriteRenderers[i];
                if (sr == null)
                    continue;

                if (!allowedSrs.Contains(sr) && sr.enabled)
                    sr.enabled = false;
            }
        }
    }

    AnimatedSpriteRenderer PickRendererForPhase(Vector2 facing, bool ascending)
    {
        Vector2 f = facing;
        if (f == Vector2.zero)
            f = movement != null ? movement.FacingDirection : Vector2.down;

        if (Mathf.Abs(f.x) >= Mathf.Abs(f.y))
            f = f.x >= 0f ? Vector2.right : Vector2.left;
        else
            f = f.y >= 0f ? Vector2.up : Vector2.down;

        if (ascending)
        {
            if (f == Vector2.up) return mountAscendUp;
            if (f == Vector2.down) return mountAscendDown;
            if (f == Vector2.left) return mountAscendLeft;
            return mountAscendRight;
        }

        if (f == Vector2.up) return mountDescendUp;
        if (f == Vector2.down) return mountDescendDown;
        if (f == Vector2.left) return mountDescendLeft;
        return mountDescendRight;
    }

    void ApplyExclusiveRenderer(AnimatedSpriteRenderer target)
    {
        SetAnimEnabled(mountAscendUp, target == mountAscendUp);
        SetAnimEnabled(mountAscendDown, target == mountAscendDown);
        SetAnimEnabled(mountAscendLeft, target == mountAscendLeft);
        SetAnimEnabled(mountAscendRight, target == mountAscendRight);

        SetAnimEnabled(mountDescendUp, target == mountDescendUp);
        SetAnimEnabled(mountDescendDown, target == mountDescendDown);
        SetAnimEnabled(mountDescendLeft, target == mountDescendLeft);
        SetAnimEnabled(mountDescendRight, target == mountDescendRight);

        if (target != null)
            target.RefreshFrame();
    }

    void ApplyRendererArcOffset(AnimatedSpriteRenderer active, float arcY)
    {
        ClearAllRuntimeOffsetsExcept(active);

        if (active == null)
            return;

        active.SetRuntimeBaseLocalY(arcY);
        active.RefreshFrame();
    }

    void DisableAllRiding()
    {
        SetAnimEnabled(mountAscendUp, false);
        SetAnimEnabled(mountAscendDown, false);
        SetAnimEnabled(mountAscendLeft, false);
        SetAnimEnabled(mountAscendRight, false);

        SetAnimEnabled(mountDescendUp, false);
        SetAnimEnabled(mountDescendDown, false);
        SetAnimEnabled(mountDescendLeft, false);
        SetAnimEnabled(mountDescendRight, false);
    }

    void ClearAllRuntimeOffsets()
    {
        ClearRuntimeOffset(mountAscendUp);
        ClearRuntimeOffset(mountAscendDown);
        ClearRuntimeOffset(mountAscendLeft);
        ClearRuntimeOffset(mountAscendRight);

        ClearRuntimeOffset(mountDescendUp);
        ClearRuntimeOffset(mountDescendDown);
        ClearRuntimeOffset(mountDescendLeft);
        ClearRuntimeOffset(mountDescendRight);
    }

    void ClearAllRuntimeOffsetsExcept(AnimatedSpriteRenderer keep)
    {
        ClearRuntimeOffsetIfNot(keep, mountAscendUp);
        ClearRuntimeOffsetIfNot(keep, mountAscendDown);
        ClearRuntimeOffsetIfNot(keep, mountAscendLeft);
        ClearRuntimeOffsetIfNot(keep, mountAscendRight);

        ClearRuntimeOffsetIfNot(keep, mountDescendUp);
        ClearRuntimeOffsetIfNot(keep, mountDescendDown);
        ClearRuntimeOffsetIfNot(keep, mountDescendLeft);
        ClearRuntimeOffsetIfNot(keep, mountDescendRight);
    }

    static void ClearRuntimeOffset(AnimatedSpriteRenderer r)
    {
        if (r == null)
            return;

        r.ClearRuntimeBaseOffset();
    }

    static void ClearRuntimeOffsetIfNot(AnimatedSpriteRenderer keep, AnimatedSpriteRenderer current)
    {
        if (current == null || current == keep)
            return;

        current.ClearRuntimeBaseOffset();
    }

    float QuantizeWorldToPixel(float worldValue)
    {
        float ppu = Mathf.Max(1f, pixelsPerUnit);
        return Mathf.Round(worldValue * ppu) / ppu;
    }

    static void SetAnimEnabled(AnimatedSpriteRenderer r, bool on)
    {
        if (r == null)
            return;

        r.enabled = on;

        if (r.TryGetComponent<SpriteRenderer>(out SpriteRenderer sr))
            sr.enabled = on;
    }
}
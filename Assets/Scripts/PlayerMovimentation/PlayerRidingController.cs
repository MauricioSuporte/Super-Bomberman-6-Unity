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

    public struct NetworkArcState
    {
        public RidingArcType kind;
        public Vector3 start, target;
        public Vector2 facing;
        public float duration, peak, startHeight, endHeight, pixelsPerUnit;
        public bool quantize;
    }

    // Presentation only. It never runs the riding coroutine or completion hooks.
    bool networkArcVisual;

    public NetworkArcState CaptureNetworkArcState(RidingArcType kind, Vector2 facing, Vector3 start, Vector3 target, float duration = -1)
    {
        start.z = target.z = transform.position.z;
        return new NetworkArcState { kind = kind, facing = facing, start = start, target = target,
            duration = Mathf.Max(0.01f, duration < 0 ? ridingSeconds : duration), peak = JumpPeakWorld,
            startHeight = GetStartHeightWorld(kind), endHeight = GetEndHeightWorld(kind),
            pixelsPerUnit = Mathf.Max(1, pixelsPerUnit), quantize = quantizeToPixel };
    }

    public static float SampleNetworkArcHeight(NetworkArcState arc, float progress)
    {
        float t = Mathf.Clamp01(progress);
        float height = Mathf.Lerp(arc.startHeight, arc.endHeight, t) + 4f * arc.peak * t * (1f - t);
        return arc.quantize ? Mathf.Round(height * Mathf.Max(1, arc.pixelsPerUnit)) / Mathf.Max(1, arc.pixelsPerUnit) : height;
    }

    public void PresentNetworkArc(NetworkArcState arc, float elapsed)
    {
        if (Assets.Scripts.Netcode.NetSync.Mode != Assets.Scripts.Netcode.NetSync.NetMode.Client || isPlaying) return;
        movement = movement != null ? movement : GetComponent<MovementController>();
        if (movement == null || (movement.isDead && GetComponent<Assets.Scripts.Netcode.NetworkPlayerState>()?.HasApprovedRevengeReturn != true) || movement.IsEndingStage) return;
        if (!networkArcVisual)
        {
            networkArcVisual = true; CacheRenderers(); RebuildAllowedSets();
            DisableAllRiding(); ClearAllRuntimeOffsets();
        }
        float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, arc.duration));
        EnforceRidingOnly();
        var active = PickRendererForPhase(arc.facing, t < 0.5f);
        ApplyExclusiveRenderer(active);
        if (active != null)
        {
            active.SetManualAnimationUpdate(true); active.idle = false; active.loop = false;
            int count = active.animationSprite != null ? active.animationSprite.Length : 0;
            float phaseTime = t < 0.5f ? elapsed : elapsed - arc.duration * 0.5f;
            active.CurrentFrame = Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(0, phaseTime) / Mathf.Max(0.0001f, active.animationTime)), 0, Mathf.Max(0, count - 1));
        }
        ApplyRendererArcOffset(active, SampleNetworkArcHeight(arc, t));
    }

    public void StopNetworkArcVisual()
    {
        if (!networkArcVisual) return;
        networkArcVisual = false; DisableAllRiding(); ClearAllRuntimeOffsets();
        if (movement != null && !movement.isDead && !movement.IsEndingStage) movement.EnableExclusiveFromState();
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

    void OnDisable()
    {
        ResetRidingState(restoreMovementState: false);
    }

    void LateUpdate()
    {
        using var performanceSample = BattleModePerformanceMarkers.PlayerAuxUpdate.Auto();

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

    public bool TryPlayDismount(Vector2 facing, System.Action onComplete = null, System.Action onStart = null, uint networkInputTick = 0)
    {
        Vector3 startWorldPos = transform.position;
        Vector3 targetWorldPos = transform.position;

        return TryPlayMountArcInternal(
            RidingArcType.Dismount,
            facing,
            startWorldPos,
            targetWorldPos,
            onComplete,
            onStart, networkInputTick);
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
        System.Action onStart = null,
        uint networkInputTick = 0)
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
        if (Assets.Scripts.Netcode.NetSync.IsServer && TryGetComponent<Assets.Scripts.Netcode.NetworkBombFx>(out var fx) && fx.isServer && fx.netId != 0)
            fx.ServerRidingArcStart(CaptureNetworkArcState(arcType, facing, startWorldPos, targetWorldPos), networkInputTick);

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

        ResetRidingState(restoreMovementState: true);
        return true;
    }

    public void ResetStaleRidingState()
    {
        ResetRidingState(restoreMovementState: false);
    }

    void ResetRidingState(bool restoreMovementState)
    {
        StopNetworkArcVisual();
        ReportNetworkRidingStop(false);
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        DisableAllRiding();
        ClearAllRuntimeOffsets();

        isPlaying = false;

        if (restoreMovementState && movement != null)
        {
            movement.EnableExclusiveFromState();
            movement.SetInputLocked(false, forceIdle: true);
            ReconcileNetworkRidingNow();
        }
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
                ReportNetworkRidingProgress(elapsed, true);
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
            SyncNetworkRidingBody();
            ReportNetworkRidingProgress(elapsed, false);

            yield return null;
        }

        Vector3 finalPos = targetWorldPos;
        finalPos.z = transform.position.z;
        transform.position = finalPos;
        SyncNetworkRidingBody();

        AnimatedSpriteRenderer finalRenderer = PickRendererForPhase(facing, false);
        ApplyExclusiveRenderer(finalRenderer);
        ApplyRendererArcOffset(finalRenderer, endHeightWorld);

        DisableAllRiding();
        ClearAllRuntimeOffsets();

        // Finalize the mounted visual after leaving the riding-only state.
        isPlaying = false;
        ReportNetworkRidingStop(true);
        onComplete?.Invoke();

        movement.EnableExclusiveFromState();
        movement.SetInputLocked(false, forceIdle: true);
        ReconcileNetworkRidingNow();

        routine = null;
    }

    void SyncNetworkRidingBody()
    {
        if (!Assets.Scripts.Netcode.NetSync.IsServer || !TryGetComponent<Assets.Scripts.Netcode.NetworkBombFx>(out var fx) || !fx.isServer || fx.netId == 0) return;
        var body = movement != null ? movement.Rigidbody : null;
        if (body != null) { body.position = transform.position; body.linearVelocity = Vector2.zero; body.angularVelocity = 0; }
    }

    void ReportNetworkRidingProgress(float elapsed, bool paused)
    {
        if (Assets.Scripts.Netcode.NetSync.IsServer && TryGetComponent<Assets.Scripts.Netcode.NetworkBombFx>(out var fx) && fx.isServer && fx.netId != 0)
            fx.ServerRidingArcProgress(elapsed, paused);
    }

    void ReportNetworkRidingStop(bool completed)
    {
        if (Assets.Scripts.Netcode.NetSync.IsServer && TryGetComponent<Assets.Scripts.Netcode.NetworkBombFx>(out var fx) && fx.isServer && fx.netId != 0)
            fx.ServerRidingArcStop(completed);
    }

    void ReconcileNetworkRidingNow()
    {
        if (Assets.Scripts.Netcode.NetSync.IsServer && TryGetComponent<Assets.Scripts.Netcode.NetworkPlayerInput>(out var input) && input.isServer && input.netId != 0)
            input.ServerReconcileMovementNow();
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

                if (a.GetComponentInParent<MountVisualController>(true) != null)
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

                if (sr.GetComponentInParent<MountVisualController>(true) != null)
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

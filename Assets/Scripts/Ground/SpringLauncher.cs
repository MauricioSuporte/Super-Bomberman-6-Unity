using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(AnimatedSpriteRenderer))]
public sealed class SpringLauncher : MonoBehaviour
{
    [Header("Visual")]
    [SerializeField] private AnimatedSpriteRenderer springAnim;

    [Header("Channel")]
    [SerializeField, Min(0f)] private float channelSeconds = 0.5f;

    [Header("Jump")]
    [SerializeField, Min(0.05f)] private float jumpSeconds = 1f;
    [SerializeField, Min(0f)] private float arcHeightTiles = 3f;

    [Header("Distances (tiles)")]
    [SerializeField, Min(0)] private int idleJumpUpTiles = 3;
    [SerializeField, Min(0)] private int horizontalJumpTiles = 4;
    [SerializeField, Min(0)] private int verticalJumpTiles = 5;

    [Header("SFX")]
    [SerializeField] private AudioClip jumpSfx;

    [Header("Safety")]
    [SerializeField, Min(0f)] private float rearmSeconds = 0.05f;

    private readonly HashSet<MovementController> active = new();

    private void Reset()
    {
        if (TryGetComponent<Collider2D>(out var col))
            col.isTrigger = true;

        if (springAnim == null)
            springAnim = GetComponent<AnimatedSpriteRenderer>();

        SetSpringIdle(true);
    }

    private void Awake()
    {
        var col = GetComponent<Collider2D>();
        if (col != null && !col.isTrigger)
            col.isTrigger = true;

        if (springAnim == null)
            springAnim = GetComponent<AnimatedSpriteRenderer>();

        SetSpringIdle(true);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null)
            return;

        if (!other.CompareTag("Player"))
            return;

        var mover = other.GetComponent<MovementController>();
        if (mover == null || mover.Rigidbody == null)
            return;

        if (mover.isDead || mover.IsEndingStage)
            return;

        var powerGlove = other.GetComponent<PowerGloveAbility>();
        if (powerGlove != null && powerGlove.IsEnabled && powerGlove.IsHoldingBomb)
            powerGlove.DestroyHeldBombIfHolding();

        if (!active.Add(mover))
            return;

        StartCoroutine(SpringRoutine(mover));
    }

    private IEnumerator SpringRoutine(MovementController mover)
    {
        SetSpringIdle(false);

        Rigidbody2D rb = mover.Rigidbody;
        float tileSize = Mathf.Max(0.0001f, mover.tileSize);

        bool prevInputLocked = mover.InputLocked;

        var playerCol = mover.GetComponent<Collider2D>();
        bool prevColliderEnabled = (playerCol != null) && playerCol.enabled;

        var bombController = mover.GetComponent<BombController>();
        bool prevBombEnabled = (bombController != null) && bombController.enabled;

        var audio = mover.GetComponent<AudioSource>();
        var riding = mover.GetComponent<PlayerRidingController>();

        mover.SetInputLocked(true, forceIdle: false);

        try
        {
            while (true)
            {
                if (mover == null || rb == null || mover.isDead || mover.IsEndingStage || !mover.gameObject.activeInHierarchy)
                    break;

                Vector2 center = GetTileCenterWorld(tileSize);

                rb.linearVelocity = Vector2.zero;
                rb.position = center;

                Vector2 heldDir = Vector2.zero;
                Vector2 prepFaceDir = mover.FacingDirection;
                if (prepFaceDir == Vector2.zero)
                    prepFaceDir = Vector2.down;

                float compressStepTimer = 0f;
                float compressInterval = 0.1f;
                float compressStep = 0.05f;

                float tEnd = Time.time + Mathf.Max(0f, channelSeconds);

                while (Time.time < tEnd)
                {
                    heldDir = ReadHeldCardinal(mover);

                    if (heldDir != Vector2.zero)
                        prepFaceDir = heldDir;

                    if (!mover.IsMounted)
                    {
                        mover.ShowSpringLauncherLookUp(prepFaceDir);
                    }
                    else
                    {
                        mover.ClearSpringLauncherLookUp();
                        ApplyIdleFacing(mover, prepFaceDir);
                    }

                    compressStepTimer += Time.deltaTime;

                    if (compressStepTimer >= compressInterval)
                    {
                        compressStepTimer -= compressInterval;

                        Vector2 p = rb.position;
                        p.y -= compressStep;
                        rb.position = p;
                    }

                    yield return null;
                }

                rb.position = center;

                heldDir = ReadHeldCardinal(mover);
                mover.ClearSpringLauncherLookUp();

                if (heldDir != Vector2.zero)
                    ApplyIdleFacing(mover, heldDir);
                else
                    ApplyIdleFacing(mover, Vector2.zero);

                if (jumpSfx != null)
                {
                    if (audio != null)
                        audio.PlayOneShot(jumpSfx);
                    else
                        AudioSource.PlayClipAtPoint(jumpSfx, mover.transform.position);
                }

                mover.SetExplosionInvulnerable(true);

                if (bombController != null)
                    bombController.enabled = false;

                if (playerCol != null)
                    playerCol.enabled = false;

                Vector2 start = rb.position;
                Vector2 end = start;

                bool isIdleBounce = (heldDir == Vector2.zero);

                if (!isIdleBounce)
                {
                    if (heldDir == Vector2.left || heldDir == Vector2.right)
                        end = start + heldDir * (horizontalJumpTiles * tileSize);
                    else if (heldDir == Vector2.up || heldDir == Vector2.down)
                        end = start + heldDir * (verticalJumpTiles * tileSize);
                }

                float duration = Mathf.Max(0.05f, jumpSeconds);
                bool isUnmounted = !mover.IsMounted;

                if (isUnmounted)
                {
                    Vector2 visualDir = heldDir != Vector2.zero ? heldDir : mover.FacingDirection;
                    if (visualDir == Vector2.zero)
                        visualDir = Vector2.down;

                    mover.SetVisualOverrideActive(true);
                    mover.SetAllSpritesVisible(false);

                    if (isIdleBounce)
                    {
                        yield return JumpArcUnmountedWithMountSprites(
                            mover,
                            riding,
                            rb,
                            start,
                            start,
                            idleJumpUpTiles * tileSize,
                            duration,
                            visualDir);
                    }
                    else
                    {
                        yield return JumpArcUnmountedWithMountSprites(
                            mover,
                            riding,
                            rb,
                            start,
                            end,
                            arcHeightTiles * tileSize,
                            duration,
                            visualDir);
                    }

                    mover.SetVisualOverrideActive(false);
                    mover.EnableExclusiveFromState();
                }
                else
                {
                    if (isIdleBounce)
                        yield return JumpArcWithFixedIdleFacing(
                            mover,
                            rb,
                            start,
                            start,
                            idleJumpUpTiles * tileSize,
                            duration,
                            Vector2.zero);
                    else
                        yield return JumpArcWithFixedIdleFacing(
                            mover,
                            rb,
                            start,
                            end,
                            arcHeightTiles * tileSize,
                            duration,
                            heldDir);
                }

                rb.linearVelocity = Vector2.zero;

                if (playerCol != null)
                    playerCol.enabled = prevColliderEnabled;

                if (bombController != null)
                    bombController.enabled = prevBombEnabled;

                mover.SetExplosionInvulnerable(false);

                Vector2 afterHeld = ReadHeldCardinal(mover);

                bool stillOnCenter = Vector2.Distance(rb.position, center) <= (tileSize * 0.15f);
                bool keepBouncing = isIdleBounce && afterHeld == Vector2.zero && stillOnCenter;

                if (!keepBouncing)
                    break;

                if (rearmSeconds > 0f)
                    yield return new WaitForSeconds(rearmSeconds);
            }
        }
        finally
        {
            if (mover != null)
            {
                mover.ClearSpringLauncherLookUp();
                mover.SetVisualOverrideActive(false);
                mover.SetInputLocked(prevInputLocked, forceIdle: false);
                mover.EnableExclusiveFromState();
            }

            active.Remove(mover);

            if (active.Count == 0)
                SetSpringIdle(true);
        }
    }

    private void ApplyIdleFacing(MovementController mover, Vector2 faceDir)
    {
        if (mover == null)
            return;

        if (faceDir != Vector2.zero)
            mover.ApplyDirectionFromVector(faceDir);

        mover.ApplyDirectionFromVector(Vector2.zero);
    }

    private void SetSpringIdle(bool idle)
    {
        if (springAnim == null)
            return;

        springAnim.idle = idle;
        springAnim.loop = !idle;

        if (!idle)
            springAnim.CurrentFrame = 0;

        springAnim.RefreshFrame();
    }

    private IEnumerator JumpArcWithFixedIdleFacing(
        MovementController mover,
        Rigidbody2D rb,
        Vector2 start,
        Vector2 end,
        float arcWorld,
        float duration,
        Vector2 fixedFaceDir)
    {
        if (mover != null)
        {
            if (fixedFaceDir != Vector2.zero)
                mover.ApplyDirectionFromVector(fixedFaceDir);

            mover.ApplyDirectionFromVector(Vector2.zero);
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;

            Vector2 flat = Vector2.Lerp(start, end, t);
            float parabola = 4f * t * (1f - t);

            Vector2 pos = flat + Vector2.up * (arcWorld * parabola);

            rb.position = pos;

            elapsed += Time.deltaTime;
            yield return null;
        }

        rb.position = end;
    }

    private IEnumerator JumpArcUnmountedWithMountSprites(
        MovementController mover,
        PlayerRidingController riding,
        Rigidbody2D rb,
        Vector2 start,
        Vector2 end,
        float arcWorld,
        float duration,
        Vector2 fixedFaceDir)
    {
        if (mover == null || rb == null)
            yield break;

        if (riding == null)
        {
            yield return JumpArcWithFixedIdleFacing(mover, rb, start, end, arcWorld, duration, fixedFaceDir);
            yield break;
        }

        DisableAllUnmountedSpringArcSprites(riding);
        ClearAllUnmountedSpringArcOffsets(riding);

        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = Mathf.Clamp01(elapsed / duration);

            Vector2 flat = Vector2.Lerp(start, end, t);
            float parabola = 4f * t * (1f - t);
            float arcY = arcWorld * parabola;

            AnimatedSpriteRenderer activeRenderer = PickUnmountedSpringArcRenderer(
                riding,
                fixedFaceDir,
                t < 0.5f);

            ApplyExclusiveUnmountedSpringArcRenderer(riding, activeRenderer);
            ApplyUnmountedSpringArcOffset(riding, activeRenderer, arcY);

            rb.position = flat;

            elapsed += Time.deltaTime;
            yield return null;
        }

        rb.position = end;

        DisableAllUnmountedSpringArcSprites(riding);
        ClearAllUnmountedSpringArcOffsets(riding);
    }

    private AnimatedSpriteRenderer PickUnmountedSpringArcRenderer(
        PlayerRidingController riding,
        Vector2 facing,
        bool ascending)
    {
        Vector2 f = facing;
        if (f == Vector2.zero)
            f = Vector2.down;

        if (Mathf.Abs(f.x) >= Mathf.Abs(f.y))
            f = f.x >= 0f ? Vector2.right : Vector2.left;
        else
            f = f.y >= 0f ? Vector2.up : Vector2.down;

        if (ascending)
        {
            if (f == Vector2.up) return riding.mountAscendUp;
            if (f == Vector2.down) return riding.mountAscendDown;
            if (f == Vector2.left) return riding.mountAscendLeft;
            return riding.mountAscendRight;
        }

        if (f == Vector2.up) return riding.mountDescendUp;
        if (f == Vector2.down) return riding.mountDescendDown;
        if (f == Vector2.left) return riding.mountDescendLeft;
        return riding.mountDescendRight;
    }

    private void ApplyExclusiveUnmountedSpringArcRenderer(
        PlayerRidingController riding,
        AnimatedSpriteRenderer target)
    {
        SetAnimEnabled(riding.mountAscendUp, target == riding.mountAscendUp);
        SetAnimEnabled(riding.mountAscendDown, target == riding.mountAscendDown);
        SetAnimEnabled(riding.mountAscendLeft, target == riding.mountAscendLeft);
        SetAnimEnabled(riding.mountAscendRight, target == riding.mountAscendRight);

        SetAnimEnabled(riding.mountDescendUp, target == riding.mountDescendUp);
        SetAnimEnabled(riding.mountDescendDown, target == riding.mountDescendDown);
        SetAnimEnabled(riding.mountDescendLeft, target == riding.mountDescendLeft);
        SetAnimEnabled(riding.mountDescendRight, target == riding.mountDescendRight);

        if (target != null)
            target.RefreshFrame();
    }

    private void ApplyUnmountedSpringArcOffset(
        PlayerRidingController riding,
        AnimatedSpriteRenderer activeRenderer,
        float arcY)
    {
        ClearUnmountedSpringArcOffsetsExcept(riding, activeRenderer);

        if (activeRenderer == null)
            return;

        activeRenderer.SetRuntimeBaseLocalY(arcY);
        activeRenderer.RefreshFrame();
    }

    private void DisableAllUnmountedSpringArcSprites(PlayerRidingController riding)
    {
        if (riding == null)
            return;

        SetAnimEnabled(riding.mountAscendUp, false);
        SetAnimEnabled(riding.mountAscendDown, false);
        SetAnimEnabled(riding.mountAscendLeft, false);
        SetAnimEnabled(riding.mountAscendRight, false);

        SetAnimEnabled(riding.mountDescendUp, false);
        SetAnimEnabled(riding.mountDescendDown, false);
        SetAnimEnabled(riding.mountDescendLeft, false);
        SetAnimEnabled(riding.mountDescendRight, false);
    }

    private void ClearAllUnmountedSpringArcOffsets(PlayerRidingController riding)
    {
        if (riding == null)
            return;

        ClearRuntimeOffset(riding.mountAscendUp);
        ClearRuntimeOffset(riding.mountAscendDown);
        ClearRuntimeOffset(riding.mountAscendLeft);
        ClearRuntimeOffset(riding.mountAscendRight);

        ClearRuntimeOffset(riding.mountDescendUp);
        ClearRuntimeOffset(riding.mountDescendDown);
        ClearRuntimeOffset(riding.mountDescendLeft);
        ClearRuntimeOffset(riding.mountDescendRight);
    }

    private void ClearUnmountedSpringArcOffsetsExcept(
        PlayerRidingController riding,
        AnimatedSpriteRenderer keep)
    {
        if (riding == null)
            return;

        ClearRuntimeOffsetIfNot(keep, riding.mountAscendUp);
        ClearRuntimeOffsetIfNot(keep, riding.mountAscendDown);
        ClearRuntimeOffsetIfNot(keep, riding.mountAscendLeft);
        ClearRuntimeOffsetIfNot(keep, riding.mountAscendRight);

        ClearRuntimeOffsetIfNot(keep, riding.mountDescendUp);
        ClearRuntimeOffsetIfNot(keep, riding.mountDescendDown);
        ClearRuntimeOffsetIfNot(keep, riding.mountDescendLeft);
        ClearRuntimeOffsetIfNot(keep, riding.mountDescendRight);
    }

    private static void ClearRuntimeOffset(AnimatedSpriteRenderer r)
    {
        if (r == null)
            return;

        r.ClearRuntimeBaseOffset();
    }

    private static void ClearRuntimeOffsetIfNot(AnimatedSpriteRenderer keep, AnimatedSpriteRenderer current)
    {
        if (current == null || current == keep)
            return;

        current.ClearRuntimeBaseOffset();
    }

    private static void SetAnimEnabled(AnimatedSpriteRenderer r, bool on)
    {
        if (r == null)
            return;

        r.enabled = on;

        if (r.TryGetComponent<SpriteRenderer>(out SpriteRenderer sr))
            sr.enabled = on;
    }

    private Vector2 GetTileCenterWorld(float tileSize)
    {
        Vector2 p = transform.position;
        return new Vector2(
            Mathf.Round(p.x / tileSize) * tileSize,
            Mathf.Round(p.y / tileSize) * tileSize
        );
    }

    private Vector2 ReadHeldCardinal(MovementController mover)
    {
        if (mover == null)
            return Vector2.zero;

        int pid = mover.PlayerId;
        var input = PlayerInputManager.Instance;
        if (input == null)
            return Vector2.zero;

        if (input.Get(pid, PlayerAction.MoveUp)) return Vector2.up;
        if (input.Get(pid, PlayerAction.MoveDown)) return Vector2.down;
        if (input.Get(pid, PlayerAction.MoveLeft)) return Vector2.left;
        if (input.Get(pid, PlayerAction.MoveRight)) return Vector2.right;

        return Vector2.zero;
    }
}
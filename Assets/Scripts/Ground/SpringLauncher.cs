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

    public void Configure(
        AnimatedSpriteRenderer visual,
        AudioClip sfx,
        int launchDistanceTiles,
        float prepareSeconds = 0.5f,
        float flightDurationSeconds = 0.75f,
        float launchArcHeightTiles = 3f)
    {
        springAnim = visual;
        jumpSfx = sfx;
        channelSeconds = Mathf.Max(0f, prepareSeconds);
        jumpSeconds = Mathf.Max(0.05f, flightDurationSeconds);
        arcHeightTiles = Mathf.Max(0f, launchArcHeightTiles);
        horizontalJumpTiles = Mathf.Max(0, launchDistanceTiles);
        verticalJumpTiles = Mathf.Max(0, launchDistanceTiles);
    }

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

        int playerId = mover.PlayerId;
        var inputManager = PlayerInputManager.Instance;

        mover.SetInputLocked(true, forceIdle: false);

        if (inputManager != null)
            inputManager.SetSpringLauncherInputGate(playerId, true);

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
                        GameAudioSettings.PlaySfx(audio, jumpSfx);
                    else
                        GameAudioSettings.PlaySfxAtPoint(jumpSfx, mover.transform.position);
                }

                mover.SetExplosionInvulnerable(true);

                if (bombController != null)
                    bombController.enabled = false;

                if (playerCol != null)
                    playerCol.enabled = false;

                Vector2 start = rb.position;
                Vector2 end = start;
                Collider2D launchRoomBounds =
                    StageAssets.World3RoomProgressionController.FindRoomBoundsContaining(start);

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
                            visualDir,
                            launchRoomBounds,
                            tileSize);
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
                            visualDir,
                            launchRoomBounds,
                            tileSize);
                    }

                    mover.SetVisualOverrideActive(false);
                    mover.EnableExclusiveFromState();
                }
                else
                {
                    MountVisualController mountVisual = mover.GetComponentInChildren<MountVisualController>(true);
                    bool useMountJumpVisual = mountVisual != null && mountVisual.HasJumpVisuals();

                    PinkLouieShadowController pinkShadow = null;
                    if (mountVisual != null)
                    {
                        pinkShadow = mountVisual.GetComponentInChildren<PinkLouieShadowController>(true);
                        if (pinkShadow != null)
                            pinkShadow.BindToPinkLouieRoot(mountVisual.transform);
                    }

                    Vector2 jumpFaceDir = heldDir != Vector2.zero ? heldDir : mover.FacingDirection;
                    if (jumpFaceDir == Vector2.zero)
                        jumpFaceDir = Vector2.down;

                    if (useMountJumpVisual)
                        mountVisual.SetJumpVisual(true, jumpFaceDir, descending: false);

                    if (pinkShadow != null)
                        pinkShadow.BeginJump(start);

                    if (isIdleBounce)
                    {
                        yield return JumpArcMountedWithJumpSprites(
                            mover,
                            mountVisual,
                            useMountJumpVisual,
                            pinkShadow,
                            rb,
                            start,
                            start,
                            idleJumpUpTiles * tileSize,
                            duration,
                            jumpFaceDir,
                            launchRoomBounds,
                            tileSize);
                    }
                    else
                    {
                        yield return JumpArcMountedWithJumpSprites(
                            mover,
                            mountVisual,
                            useMountJumpVisual,
                            pinkShadow,
                            rb,
                            start,
                            end,
                            arcHeightTiles * tileSize,
                            duration,
                            jumpFaceDir,
                            launchRoomBounds,
                            tileSize);
                    }

                    if (pinkShadow != null)
                        pinkShadow.EndJump();

                    if (useMountJumpVisual)
                        mountVisual.SetJumpVisual(false, jumpFaceDir);
                }

                rb.linearVelocity = Vector2.zero;

                Vector2 wrappedLanding = WrapFlightPosition(end, launchRoomBounds, tileSize);
                bool wrapApplied = Vector2.SqrMagnitude(wrappedLanding - end) > 0.0001f;

                if (wrapApplied)
                {
                    // The arc runs from Update while the player normally
                    // moves in FixedUpdate. Keep control locked through one
                    // physics tick, then reassert the wrapped landing point
                    // before collision and player movement resume.
                    yield return new WaitForFixedUpdate();

                    SetFlightPosition(rb, wrappedLanding);
                    QueueInvalidWrappedLandingBounce(mover, heldDir);
                }

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
            if (inputManager != null)
                inputManager.SetSpringLauncherInputGate(playerId, false);

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
        Vector2 fixedFaceDir,
        Collider2D launchRoomBounds,
        float tileSize)
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

            Vector2 flat = WrapFlightPosition(
                Vector2.Lerp(start, end, t), launchRoomBounds, tileSize);
            float parabola = 4f * t * (1f - t);
            float arcY = ClampArcHeightToRoom(
                flat.y, arcWorld * parabola, launchRoomBounds, tileSize);

            Vector2 pos = flat + Vector2.up * arcY;

            SetFlightPosition(rb, pos);

            elapsed += Time.deltaTime;
            yield return null;
        }

        SetFlightPosition(rb, WrapFlightPosition(end, launchRoomBounds, tileSize));
    }

    private IEnumerator JumpArcMountedWithJumpSprites(
        MovementController mover,
        MountVisualController mountVisual,
        bool useMountJumpVisual,
        PinkLouieShadowController pinkShadow,
        Rigidbody2D rb,
        Vector2 start,
        Vector2 end,
        float arcWorld,
        float duration,
        Vector2 fixedFaceDir,
        Collider2D launchRoomBounds,
        float tileSize)
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
            float t = Mathf.Clamp01(elapsed / duration);
            bool descendingNow = t >= 0.5f;

            if (useMountJumpVisual && mountVisual != null)
                mountVisual.SetJumpPhase(descendingNow);

            Vector2 flat = WrapFlightPosition(
                Vector2.Lerp(start, end, t), launchRoomBounds, tileSize);

            if (pinkShadow != null)
                pinkShadow.SetJumpGroundPosition(flat);

            float parabola = 4f * t * (1f - t);
            float arcY = ClampArcHeightToRoom(
                flat.y, arcWorld * parabola, launchRoomBounds, tileSize);
            Vector2 pos = flat + Vector2.up * arcY;

            SetFlightPosition(rb, pos);

            elapsed += Time.deltaTime;
            yield return null;
        }

        SetFlightPosition(rb, WrapFlightPosition(end, launchRoomBounds, tileSize));

        if (pinkShadow != null)
            pinkShadow.SetJumpGroundPosition(
                WrapFlightPosition(end, launchRoomBounds, tileSize));
    }

    private IEnumerator JumpArcUnmountedWithMountSprites(
        MovementController mover,
        PlayerRidingController riding,
        Rigidbody2D rb,
        Vector2 start,
        Vector2 end,
        float arcWorld,
        float duration,
        Vector2 fixedFaceDir,
        Collider2D launchRoomBounds,
        float tileSize)
    {
        if (mover == null || rb == null)
            yield break;

        if (riding == null)
        {
            yield return JumpArcWithFixedIdleFacing(
                mover,
                rb,
                start,
                end,
                arcWorld,
                duration,
                fixedFaceDir,
                launchRoomBounds,
                tileSize);
            yield break;
        }

        DisableAllUnmountedSpringArcSprites(riding);
        ClearAllUnmountedSpringArcOffsets(riding);

        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = Mathf.Clamp01(elapsed / duration);

            Vector2 flat = WrapFlightPosition(
                Vector2.Lerp(start, end, t), launchRoomBounds, tileSize);
            float parabola = 4f * t * (1f - t);
            float arcY = ClampArcHeightToRoom(
                flat.y, arcWorld * parabola, launchRoomBounds, tileSize);

            AnimatedSpriteRenderer activeRenderer = PickUnmountedSpringArcRenderer(
                riding,
                fixedFaceDir,
                t < 0.5f);

            ApplyExclusiveUnmountedSpringArcRenderer(riding, activeRenderer);
            ApplyUnmountedSpringArcOffset(riding, activeRenderer, arcY);

            SetFlightPosition(rb, flat);

            elapsed += Time.deltaTime;
            yield return null;
        }

        SetFlightPosition(rb, WrapFlightPosition(end, launchRoomBounds, tileSize));

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

    /// <summary>
    /// Keeps a geyser flight in the room it started from. World 3 rooms are
    /// tile-aligned, so wrapping the horizontal path this way preserves both
    /// the launch direction and any sub-tile overshoot at the camera edge.
    /// A null bound leaves every other spring-launcher scene unchanged.
    /// </summary>
    private static Vector2 WrapFlightPosition(
        Vector2 position,
        Collider2D roomBounds,
        float tileSize)
    {
        if (roomBounds == null)
            return position;

        Bounds bounds = roomBounds.bounds;

        // The flight position is continuous, unlike a bomb's discrete tile
        // center. Wrap only after crossing the actual collider edge so the
        // player does not jump early to a point outside the opposite edge.
        float minX = bounds.min.x;
        float maxX = bounds.max.x;
        float minY = bounds.min.y;
        float maxY = bounds.max.y;

        float width = bounds.size.x;
        float safeTileSize = Mathf.Max(0.0001f, tileSize);

        if (width > 0f)
        {
            while (position.x < minX)
                position.x += width;
            while (position.x > maxX)
                position.x -= width;
        }

        if (bounds.size.y > safeTileSize)
        {
            // The room collider includes the top and bottom indestructible
            // wall tiles. A vertical wrap must land one tile inside those
            // walls, not on the wall tile itself.
            float bottomLandingY = Mathf.Ceil((minY + safeTileSize) / safeTileSize) * safeTileSize;
            float topLandingY = Mathf.Floor((maxY - safeTileSize) / safeTileSize) * safeTileSize;

            if (position.y < minY)
                position.y = topLandingY;
            else if (position.y > maxY)
                position.y = bottomLandingY;
        }

        return position;
    }

    private static void SetFlightPosition(Rigidbody2D body, Vector2 position)
    {
        if (body == null)
            return;

        body.position = position;

        Vector3 transformPosition = body.transform.position;
        transformPosition.x = position.x;
        transformPosition.y = position.y;
        body.transform.position = transformPosition;
    }

    private static float ClampArcHeightToRoom(
        float groundY,
        float requestedArcY,
        Collider2D roomBounds,
        float tileSize)
    {
        if (roomBounds == null)
            return requestedArcY;

        Bounds bounds = roomBounds.bounds;
        float safeTileSize = Mathf.Max(0.0001f, tileSize);
        float minVisibleY = bounds.min.y + safeTileSize;
        float maxVisibleY = bounds.max.y - safeTileSize;
        float clampedY = Mathf.Clamp(groundY + requestedArcY, minVisibleY, maxVisibleY);
        return clampedY - groundY;
    }

    private void QueueInvalidWrappedLandingBounce(
        MovementController mover,
        Vector2 launchDirection)
    {
        launchDirection = launchDirection.sqrMagnitude > 0.01f
            ? launchDirection.normalized
            : mover.FacingDirection;

        if (launchDirection == Vector2.zero ||
            !mover.TryGetComponent<PlayerPushedOutOfInvalidTile>(out var resolver) ||
            resolver == null)
        {
            return;
        }

        resolver.NotifyExternalPushed(launchDirection);
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

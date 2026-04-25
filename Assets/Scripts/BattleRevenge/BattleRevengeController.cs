using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public sealed class BattleRevengeController : MonoBehaviour
{
    private enum CartEdge
    {
        Left = 0,
        Right = 1,
        Top = 2,
        Bottom = 3
    }
    private enum CartCorner
    {
        None = 0,
        TopLeft = 1,
        TopRight = 2,
        BottomRight = 3,
        BottomLeft = 4
    }
    private enum CartSegment
    {
        Top = 0,
        TopRight = 1,
        Right = 2,
        BottomRight = 3,
        Bottom = 4,
        BottomLeft = 5,
        Left = 6,
        TopLeft = 7
    }

    [Header("Body")]
    [SerializeField] private AnimatedSpriteRenderer bodyUp;
    [SerializeField] private AnimatedSpriteRenderer bodyDown;
    [SerializeField] private AnimatedSpriteRenderer bodyLeft;
    [SerializeField] private AnimatedSpriteRenderer bodyRight;

    [Header("Head")]
    [SerializeField] private AnimatedSpriteRenderer headUp;
    [SerializeField] private AnimatedSpriteRenderer headDown;
    [SerializeField] private AnimatedSpriteRenderer headLeft;
    [SerializeField] private AnimatedSpriteRenderer headRight;

    [Header("Body - Corner Transition")]
    [SerializeField] private AnimatedSpriteRenderer bodyTopLeft;
    [SerializeField] private AnimatedSpriteRenderer bodyTopRight;
    [SerializeField] private AnimatedSpriteRenderer bodyBottomLeft;
    [SerializeField] private AnimatedSpriteRenderer bodyBottomRight;

    [Header("Head - Corner Transition")]
    [SerializeField] private AnimatedSpriteRenderer headTopLeft;
    [SerializeField] private AnimatedSpriteRenderer headTopRight;
    [SerializeField] private AnimatedSpriteRenderer headBottomLeft;
    [SerializeField] private AnimatedSpriteRenderer headBottomRight;

    [Header("Per-Corner Offsets (Head)")]
    [SerializeField] private Vector2 headOffsetTopLeft;
    [SerializeField] private Vector2 headOffsetTopRight;
    [SerializeField] private Vector2 headOffsetBottomLeft;
    [SerializeField] private Vector2 headOffsetBottomRight;

    [Header("Debug")]
    [SerializeField] private bool debugCornerTransition;
    [SerializeField, Min(0.01f)] private float debugCornerTransitionInterval = 0.1f;
    [SerializeField] private bool debugActionALaunch;
    [SerializeField, Min(0.01f)] private float debugActionAInterval = 0.08f;

    [Header("Segment Alignment")]
    [SerializeField, Min(0.001f)] private float tileAlignmentTolerance = 0.03f;

    [Header("Explicit Movement Bounds")]
    [FormerlySerializedAs("topBottomDiagonalLeftX")]
    [SerializeField] private float horizontalWallMinX = -9f;
    [FormerlySerializedAs("topBottomDiagonalRightX")]
    [SerializeField] private float horizontalWallMaxX = 4f;
    [SerializeField] private float verticalWallMinY = -6.5f;
    [SerializeField] private float verticalWallMaxY = 4f;
    [SerializeField] private float topWallY = 5f;
    [SerializeField] private float bottomWallY = -7f;
    [SerializeField] private float leftWallX = -10f;
    [SerializeField] private float rightWallX = 5f;

    [Header("Movement")]
    [SerializeField, Min(0.1f)] private float moveSpeed = 4f;

    [Header("Movement Tilt")]
    [SerializeField] private bool useMovementTilt = true;
    [SerializeField, Min(0f)] private float tiltAngle = 6f;
    [SerializeField, Min(0.01f)] private float tiltInDuration = 0.08f;
    [SerializeField, Min(0.01f)] private float tiltOutDuration = 0.12f;
    [SerializeField, Min(0f)] private float tiltHoldDelay = 0.03f;

    [Header("Per-Edge Offsets (Head)")]
    [SerializeField] private Vector2 headOffsetLeft;
    [SerializeField] private Vector2 headOffsetRight;
    [SerializeField] private Vector2 headOffsetTop;
    [SerializeField] private Vector2 headOffsetBottom;

    [Header("Entrance / Exit Animation")]
    [SerializeField, Min(0.01f)] private float enterDuration = 0.35f;
    [SerializeField, Min(0.01f)] private float exitDuration = 0.25f;

    [Header("Offscreen")]
    [SerializeField, Min(0.1f)] private float offscreenDistanceTiles = 2f;

    [Header("Charged Throw")]
    [SerializeField, Min(3)] private int minLaunchDistanceTiles = 3;
    [SerializeField, Min(3)] private int maxLaunchDistanceTiles = 7;
    [SerializeField, Min(0.01f)] private float chargeStepSeconds = 0.12f;

    [Header("Landing Indicator")]
    [SerializeField] private SpriteRenderer landingIndicatorRenderer;
    [SerializeField, Range(0f, 1f)] private float indicatorMinAlpha = 0.25f;
    [SerializeField, Range(0f, 1f)] private float indicatorMaxAlpha = 0.6f;
    [SerializeField, Min(0.01f)] private float indicatorBlinkSpeed = 6f;

    [Header("Recharge Indicator")]
    [SerializeField] private SpriteRenderer rechargeIndicatorRenderer;
    [SerializeField] private string rechargeSpritesResourcesPath = "Sprites/MadBomber";
    [SerializeField] private string rechargeSpriteNamePrefix = "spr_revenge_recharge_";

    [Header("Recharge Indicator Visual")]
    [SerializeField, Range(0f, 1f)] private float rechargeIndicatorMinAlpha = 0.18f;
    [SerializeField, Range(0f, 1f)] private float rechargeIndicatorMaxAlpha = 0.45f;
    [SerializeField, Min(0.01f)] private float rechargeIndicatorBlinkSpeed = 10f;

    [Header("Recharge Indicator Offsets")]
    [SerializeField] private Vector2 rechargeOffsetLeft;
    [SerializeField] private Vector2 rechargeOffsetRight;
    [SerializeField] private Vector2 rechargeOffsetTop;
    [SerializeField] private Vector2 rechargeOffsetBottom;

    [Header("SFX")]
    [SerializeField] private AudioClip launchBombSfx;
    [SerializeField, Range(0f, 1f)] private float launchBombSfxVolume = 1f;

    private const int RechargeFrameCount = 33;
    private const float PixelsPerUnit = 16f;
    private const float MinSegmentLength = 0.0001f;

    private bool isAnimating;
    private Coroutine activeAnimationRoutine;

    private BattleRevengeSystem system;
    private int ownerPlayerId;
    private CartEdge currentEdge = CartEdge.Left;
    private CartSegment currentSegment = CartSegment.Left;

    private bool boundsConfigured;

    private float currentTilt;
    private float targetTilt;
    private float tiltHoldTimer;
    private CartEdge? lastInputTargetWall;

    private float nextBombAllowedAt;

    private bool isChargingLaunch;
    private float nextChargeStepAt;
    private int chargedLaunchDistanceTiles = 3;

    private Sprite[] rechargeSprites;
    private bool rechargeSpritesLoaded;

    private float perimeterPosition;
    private float spawnPerimeterPosition;
    private bool suppressCornerVisualAtSpawn;

    private float nextActionADebugAt;
    private bool lastHoldingActionA;

    public int OwnerPlayerId => ownerPlayerId;
    public bool DebugCornerTransitionEnabled => debugCornerTransition;
    public float DebugCornerTransitionInterval => debugCornerTransitionInterval;

    private float nextCornerDebugAt;
    private float nextExplicitBoundsDebugAt;
    private CartCorner lastDebugCorner = CartCorner.None;
    private CartEdge lastDebugEdge = CartEdge.Left;

    public Vector2 LaunchDirection => currentEdge switch
    {
        CartEdge.Left => Vector2.right,
        CartEdge.Right => Vector2.left,
        CartEdge.Top => Vector2.down,
        CartEdge.Bottom => Vector2.up,
        _ => Vector2.right
    };

    public bool IsOnLeftEdge => currentEdge == CartEdge.Left;

    public bool IsOnRightEdge => currentEdge == CartEdge.Right;

    private CartCorner currentCorner = CartCorner.None;

    public Vector2 LaunchStartWorldPosition => GetCurrentLogicalPosition();

    public bool IsInLaunchableSegment => IsLaunchableWallSegment(currentSegment);

    private bool CanLaunchBombNow()
    {
        if (!IsInLaunchableSegment)
            return false;

        if (system == null)
            return true;

        return system.IsPredictedLandingPositionValid(this, GetCurrentLaunchDistanceTiles());
    }

    void Awake()
    {
        RefreshVisualByEdge();
        HideLandingIndicator();
        HideRechargeIndicator();
        EnsureRechargeSpritesLoaded();
    }

    void Update()
    {
        if (isAnimating)
            return;

        if (!Application.isPlaying || system == null || ownerPlayerId <= 0)
            return;

        if (!system.IsRuntimeEnabled || GamePauseController.IsPaused)
            return;

        PlayerInputManager input = PlayerInputManager.Instance;
        if (input == null)
            return;

        bool hasMovementInput = TryGetTargetWallFromInput(input, out CartEdge targetWall);

        if (hasMovementInput)
        {
            if (!lastInputTargetWall.HasValue || lastInputTargetWall.Value != targetWall)
            {
                currentTilt = 0f;
                tiltHoldTimer = 0f;
                lastInputTargetWall = targetWall;
            }

            Vector3 beforeMove = transform.position;
            MoveTowardWallMidpoint(targetWall);
            Vector3 afterMove = transform.position;

            UpdateMovementTilt(afterMove - beforeMove, true);
        }
        else
        {
            lastInputTargetWall = null;
            UpdateMovementTilt(Vector3.zero, false);
        }

        RefreshVisualByEdge();
        ApplyTiltToActiveVisuals();

        UpdateRechargeIndicator();

        bool canLaunchBomb = CanLaunchBombNow();

        bool holdingActionA = input.Get(ownerPlayerId, PlayerAction.ActionA);
        DebugActionALaunchState(holdingActionA, canLaunchBomb, isChargingLaunch, "Read");

        if (!canLaunchBomb)
        {
            DebugActionALaunchState(holdingActionA, canLaunchBomb, isChargingLaunch, "BlockedByCorner");

            if (isChargingLaunch)
            {
                isChargingLaunch = false;
                chargedLaunchDistanceTiles = Mathf.Clamp(minLaunchDistanceTiles, 3, 7);
            }

            HideLandingIndicator();
        }

        if (holdingActionA && canLaunchBomb)
        {
            if (!isChargingLaunch)
            {
                isChargingLaunch = true;
                chargedLaunchDistanceTiles = Mathf.Clamp(minLaunchDistanceTiles, 3, 7);
                nextChargeStepAt = Time.unscaledTime + chargeStepSeconds;
            }
            else if (Time.unscaledTime >= nextChargeStepAt)
            {
                if (chargedLaunchDistanceTiles < Mathf.Min(maxLaunchDistanceTiles, 7))
                    chargedLaunchDistanceTiles++;

                nextChargeStepAt = Time.unscaledTime + chargeStepSeconds;
            }

            canLaunchBomb = CanLaunchBombNow();
            if (canLaunchBomb)
            {
                UpdateLandingIndicator();
            }
            else
            {
                isChargingLaunch = false;
                chargedLaunchDistanceTiles = Mathf.Clamp(minLaunchDistanceTiles, 3, 7);
                HideLandingIndicator();
            }
        }
        else
        {
            if (isChargingLaunch)
            {
                HideLandingIndicator();

                if (Time.unscaledTime >= nextBombAllowedAt)
                {
                    DebugActionALaunchState(holdingActionA, canLaunchBomb, isChargingLaunch, "ReleaseTryingLaunch");

                    if (system.TryLaunchBombFromCart(this, chargedLaunchDistanceTiles))
                    {
                        nextBombAllowedAt = Time.unscaledTime + system.CartBombCooldownSeconds;

                        if (GameMusicController.Instance != null)
                            GameMusicController.Instance.PlaySfx(launchBombSfx, launchBombSfxVolume);
                    }
                }

                isChargingLaunch = false;
                chargedLaunchDistanceTiles = Mathf.Clamp(minLaunchDistanceTiles, 3, 7);
            }
            else
            {
                HideLandingIndicator();
            }
        }
    }

    void OnDisable()
    {
        HideLandingIndicator();
        HideRechargeIndicator();
        currentTilt = 0f;
        targetTilt = 0f;
        tiltHoldTimer = 0f;
        lastInputTargetWall = null;
        ApplyTiltToActiveVisuals();
    }

    private void EnsureRechargeSpritesLoaded()
    {
        if (rechargeSpritesLoaded)
            return;

        rechargeSpritesLoaded = true;
        rechargeSprites = new Sprite[RechargeFrameCount];

        for (int i = 0; i < RechargeFrameCount; i++)
        {
            string resourcePath = $"{rechargeSpritesResourcesPath}/{rechargeSpriteNamePrefix}{i}";
            rechargeSprites[i] = Resources.Load<Sprite>(resourcePath);
        }
    }

    private void UpdateLandingIndicator()
    {
        if (landingIndicatorRenderer == null || system == null)
            return;

        if (!system.TryGetPredictedLandingPosition(this, chargedLaunchDistanceTiles, out Vector2 landingPos))
        {
            HideLandingIndicator();
            return;
        }

        landingIndicatorRenderer.transform.position = new Vector3(
            landingPos.x,
            landingPos.y,
            landingIndicatorRenderer.transform.position.z);

        float t = Mathf.PingPong(Time.unscaledTime * indicatorBlinkSpeed, 1f);
        float alpha = Mathf.Lerp(indicatorMinAlpha, indicatorMaxAlpha, t);

        Color c = landingIndicatorRenderer.color;
        c.a = alpha;
        landingIndicatorRenderer.color = c;

        landingIndicatorRenderer.enabled = true;
    }

    private void HideLandingIndicator()
    {
        if (landingIndicatorRenderer == null)
            return;

        landingIndicatorRenderer.enabled = false;

        Color c = landingIndicatorRenderer.color;
        c.a = indicatorMaxAlpha;
        landingIndicatorRenderer.color = c;
    }

    private void UpdateRechargeIndicator()
    {
        if (rechargeIndicatorRenderer == null)
            return;

        float remaining = Mathf.Max(0f, nextBombAllowedAt - Time.unscaledTime);

        if (remaining <= 0f)
        {
            rechargeIndicatorRenderer.enabled = false;
            return;
        }

        EnsureRechargeSpritesLoaded();

        float totalCooldown = Mathf.Max(0.01f, system != null ? system.CartBombCooldownSeconds : 1f);
        float normalized = 1f - Mathf.Clamp01(remaining / totalCooldown);

        int frameIndex = Mathf.Clamp(
            Mathf.RoundToInt(normalized * (RechargeFrameCount - 1)),
            0,
            RechargeFrameCount - 1);

        if (rechargeSprites != null &&
            frameIndex >= 0 &&
            frameIndex < rechargeSprites.Length &&
            rechargeSprites[frameIndex] != null)
        {
            rechargeIndicatorRenderer.sprite = rechargeSprites[frameIndex];
        }

        Vector3 basePos = transform.position;
        Vector2 offset = GetRechargeOffset();

        rechargeIndicatorRenderer.transform.position = new Vector3(
            basePos.x + offset.x,
            basePos.y + offset.y,
            rechargeIndicatorRenderer.transform.position.z);

        float blinkT = Mathf.PingPong(Time.unscaledTime * rechargeIndicatorBlinkSpeed, 1f);
        float alpha = Mathf.Lerp(rechargeIndicatorMinAlpha, rechargeIndicatorMaxAlpha, blinkT);

        Color c = rechargeIndicatorRenderer.color;
        c.a = alpha;
        rechargeIndicatorRenderer.color = c;

        rechargeIndicatorRenderer.enabled = true;
    }

    private void HideRechargeIndicator()
    {
        if (rechargeIndicatorRenderer == null)
            return;

        rechargeIndicatorRenderer.enabled = false;

        Color c = rechargeIndicatorRenderer.color;
        c.a = rechargeIndicatorMaxAlpha;
        rechargeIndicatorRenderer.color = c;
    }

    public void ConfigureBounds(float minX, float maxX, float minY, float maxY)
    {
        boundsConfigured = true;

        Vector2 logicalPosition = GetLogicalPositionForCurrentEdgeCenter();
        perimeterPosition = GetPerimeterPositionFromEdge(currentEdge, logicalPosition);

        ApplyTransformFromPerimeterPosition();
        RefreshVisualByEdge();
    }

    public void Activate(
        BattleRevengeSystem ownerSystem,
        int newOwnerPlayerId,
        MovementController sourceVisuals,
        Vector2 startPosition,
        Vector2 inwardDirection)
    {
        system = ownerSystem;
        ownerPlayerId = Mathf.Clamp(newOwnerPlayerId, GameSession.MinPlayerId, GameSession.MaxPlayerId);
        nextBombAllowedAt = 0f;

        ConfigureHeadVisuals(sourceVisuals);
        currentEdge = ResolveEdgeFromDirection(inwardDirection);

        Vector2 logicalTarget = ClampLogicalPositionToEdge(currentEdge, startPosition);
        perimeterPosition = GetPerimeterPositionFromEdge(currentEdge, logicalTarget);
        spawnPerimeterPosition = perimeterPosition;
        suppressCornerVisualAtSpawn = IsOnCornerBoundary(perimeterPosition);

        Vector3 targetPosition = BuildWorldPositionFromPerimeterPosition(perimeterPosition, out currentEdge, out currentSegment);
        Vector3 spawnPosition = GetOffscreenPosition(targetPosition);

        StopActiveAnimation();

        transform.position = spawnPosition;
        RefreshVisualByEdge();
        HideLandingIndicator();
        HideRechargeIndicator();

        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        activeAnimationRoutine = StartCoroutine(EnterRoutine(spawnPosition, targetPosition));
    }

    public void PlayExit(Action onExitFinished)
    {
        if (!gameObject.activeInHierarchy)
        {
            onExitFinished?.Invoke();
            return;
        }

        StopActiveAnimation();
        activeAnimationRoutine = StartCoroutine(ExitRoutine(onExitFinished));
    }

    public void ReenterAs(
        int newOwnerPlayerId,
        MovementController newOwnerVisuals,
        Vector2 startPosition,
        Vector2 inwardDirection)
    {
        ownerPlayerId = Mathf.Clamp(newOwnerPlayerId, GameSession.MinPlayerId, GameSession.MaxPlayerId);
        nextBombAllowedAt = 0f;

        ConfigureHeadVisuals(newOwnerVisuals);
        currentEdge = ResolveEdgeFromDirection(inwardDirection);

        Vector2 logicalTarget = ClampLogicalPositionToEdge(currentEdge, startPosition);
        perimeterPosition = GetPerimeterPositionFromEdge(currentEdge, logicalTarget);
        spawnPerimeterPosition = perimeterPosition;
        suppressCornerVisualAtSpawn = IsOnCornerBoundary(perimeterPosition);

        Vector3 targetPosition = BuildWorldPositionFromPerimeterPosition(perimeterPosition, out currentEdge, out currentSegment);
        Vector3 spawnPosition = GetOffscreenPosition(targetPosition);

        StopActiveAnimation();

        transform.position = spawnPosition;
        RefreshVisualByEdge();
        HideLandingIndicator();
        HideRechargeIndicator();

        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        activeAnimationRoutine = StartCoroutine(EnterRoutine(spawnPosition, targetPosition));
    }

    public void RebindOwner(int newOwnerPlayerId, MovementController sourceVisuals)
    {
        ownerPlayerId = Mathf.Clamp(newOwnerPlayerId, GameSession.MinPlayerId, GameSession.MaxPlayerId);
        nextBombAllowedAt = Time.unscaledTime + 0.1f;

        ConfigureHeadVisuals(sourceVisuals);
        RefreshVisualByEdge();
    }

    private IEnumerator ExitRoutine(Action onExitFinished)
    {
        isAnimating = true;

        HideLandingIndicator();
        HideRechargeIndicator();

        Vector3 start = transform.position;
        Vector3 end = GetOffscreenPosition(start);

        float timer = 0f;
        while (timer < exitDuration)
        {
            timer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(timer / exitDuration);
            transform.position = PixelPerfectMove(start, end, t);
            yield return null;
        }

        transform.position = end;
        isAnimating = false;
        activeAnimationRoutine = null;

        onExitFinished?.Invoke();
    }

    private IEnumerator EnterRoutine(Vector3 start, Vector3 target)
    {
        isAnimating = true;

        float timer = 0f;
        while (timer < enterDuration)
        {
            timer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(timer / enterDuration);
            transform.position = PixelPerfectMove(start, target, t);
            yield return null;
        }

        transform.position = target;
        isAnimating = false;
        activeAnimationRoutine = null;
    }

    private void StopActiveAnimation()
    {
        if (activeAnimationRoutine != null)
        {
            StopCoroutine(activeAnimationRoutine);
            activeAnimationRoutine = null;
        }

        isAnimating = false;
    }

    private Vector3 PixelPerfectMove(Vector3 from, Vector3 to, float t)
    {
        Vector3 raw = Vector3.Lerp(from, to, t);
        raw.x = Mathf.Round(raw.x * PixelsPerUnit) / PixelsPerUnit;
        raw.y = Mathf.Round(raw.y * PixelsPerUnit) / PixelsPerUnit;
        return raw;
    }

    private Vector3 GetOffscreenPosition(Vector3 target)
    {
        float offset = offscreenDistanceTiles;

        return currentEdge switch
        {
            CartEdge.Left => target + Vector3.left * offset,
            CartEdge.Right => target + Vector3.right * offset,
            CartEdge.Top => target + Vector3.up * offset,
            CartEdge.Bottom => target + Vector3.down * offset,
            _ => target
        };
    }

    private void ConfigureHeadVisuals(MovementController sourceVisuals)
    {
        if (sourceVisuals == null)
            return;

        sourceVisuals.CopyHeadOnlyVisualsTo(
            headDown,
            headUp,
            headRight,
            headLeft
        );

        CopyHeadVisuals(headLeft, headTopLeft);
        CopyHeadVisuals(headLeft, headBottomLeft);

        CopyHeadVisuals(headRight, headTopRight);
        CopyHeadVisuals(headRight, headBottomRight);
    }

    private void CopyHeadVisuals(AnimatedSpriteRenderer source, AnimatedSpriteRenderer target)
    {
        if (source == null || target == null)
            return;

        target.idleSprite = source.idleSprite;

        if (source.animationSprite != null)
        {
            target.animationSprite = new Sprite[source.animationSprite.Length];

            for (int i = 0; i < source.animationSprite.Length; i++)
                target.animationSprite[i] = source.animationSprite[i];
        }
        else
        {
            target.animationSprite = null;
        }

        if (source.frameOffsets != null)
        {
            target.frameOffsets = new Vector2[source.frameOffsets.Length];

            for (int i = 0; i < source.frameOffsets.Length; i++)
                target.frameOffsets[i] = source.frameOffsets[i];
        }
        else
        {
            target.frameOffsets = null;
        }

        target.allowFlipX = source.allowFlipX;
        target.animationTime = source.animationTime;
        target.useSequenceDuration = source.useSequenceDuration;
        target.sequenceDuration = source.sequenceDuration;
        target.loop = true;
        target.idle = false;
        target.pingPong = source.pingPong;

        target.CurrentFrame = source.CurrentFrame;
        target.RefreshFrame();
    }

    private void RefreshVisualByEdge()
    {
        bool keepSpawnEdgeVisual =
            suppressCornerVisualAtSpawn &&
            IsOnCornerBoundary(spawnPerimeterPosition) &&
            Mathf.Abs(GetShortestPerimeterDelta(spawnPerimeterPosition, perimeterPosition)) <= tileAlignmentTolerance;

        currentCorner = keepSpawnEdgeVisual
            ? CartCorner.None
            : GetCornerForSegment(currentSegment);

        if (currentCorner != CartCorner.None)
        {
            SetCornerVisual(currentCorner);
            ApplyHeadOffsetForCurrentEdge();
            return;
        }

        switch (currentEdge)
        {
            case CartEdge.Top:
                SetDirectionVisual(Vector2.up);
                break;
            case CartEdge.Bottom:
                SetDirectionVisual(Vector2.down);
                break;
            case CartEdge.Left:
                SetDirectionVisual(Vector2.left);
                break;
            case CartEdge.Right:
                SetDirectionVisual(Vector2.right);
                break;
        }

        ApplyHeadOffsetForCurrentEdge();
    }

    private void ApplyHeadOffsetForCurrentEdge()
    {
        Vector2 offset = GetHeadOffset();
        AnimatedSpriteRenderer activeHead = GetActiveHeadRenderer();

        ClearHeadRuntimeOffsets(headUp);
        ClearHeadRuntimeOffsets(headDown);
        ClearHeadRuntimeOffsets(headLeft);
        ClearHeadRuntimeOffsets(headRight);
        ClearHeadRuntimeOffsets(headTopLeft);
        ClearHeadRuntimeOffsets(headTopRight);
        ClearHeadRuntimeOffsets(headBottomLeft);
        ClearHeadRuntimeOffsets(headBottomRight);

        if (activeHead != null)
        {
            activeHead.SetRuntimeBaseLocalX(offset.x);
            activeHead.SetRuntimeBaseLocalY(offset.y);
        }
    }

    private void ClearHeadRuntimeOffsets(AnimatedSpriteRenderer renderer)
    {
        if (renderer == null)
            return;

        renderer.ClearRuntimeBaseOffset();
        renderer.RefreshFrame();
    }

    private Vector2 GetHeadOffset()
    {
        if (currentCorner != CartCorner.None)
        {
            return currentCorner switch
            {
                CartCorner.TopLeft => headOffsetTopLeft,
                CartCorner.TopRight => headOffsetTopRight,
                CartCorner.BottomLeft => headOffsetBottomLeft,
                CartCorner.BottomRight => headOffsetBottomRight,
                _ => Vector2.zero
            };
        }

        return currentEdge switch
        {
            CartEdge.Left => headOffsetLeft,
            CartEdge.Right => headOffsetRight,
            CartEdge.Top => headOffsetTop,
            CartEdge.Bottom => headOffsetBottom,
            _ => Vector2.zero
        };
    }

    private Vector2 GetRechargeOffset() => currentEdge switch
    {
        CartEdge.Left => rechargeOffsetLeft,
        CartEdge.Right => rechargeOffsetRight,
        CartEdge.Top => rechargeOffsetTop,
        CartEdge.Bottom => rechargeOffsetBottom,
        _ => Vector2.zero
    };

    private AnimatedSpriteRenderer GetActiveHeadRenderer()
    {
        if (currentCorner != CartCorner.None)
        {
            return currentCorner switch
            {
                CartCorner.TopLeft => headTopLeft,
                CartCorner.TopRight => headTopRight,
                CartCorner.BottomLeft => headBottomLeft,
                CartCorner.BottomRight => headBottomRight,
                _ => null
            };
        }

        return currentEdge switch
        {
            CartEdge.Left => headLeft,
            CartEdge.Right => headRight,
            CartEdge.Top => headUp,
            CartEdge.Bottom => headDown,
            _ => null
        };
    }

    private void SetDirectionVisual(Vector2 direction)
    {
        AnimatedSpriteRenderer body = PickDirectionalRenderer(bodyUp, bodyDown, bodyLeft, bodyRight, direction);
        AnimatedSpriteRenderer head = PickDirectionalRenderer(headUp, headDown, headLeft, headRight, direction);

        SetRendererState(bodyUp, body == bodyUp);
        SetRendererState(bodyDown, body == bodyDown);
        SetRendererState(bodyLeft, body == bodyLeft);
        SetRendererState(bodyRight, body == bodyRight);

        SetRendererState(headUp, head == headUp);
        SetRendererState(headDown, head == headDown);
        SetRendererState(headLeft, head == headLeft);
        SetRendererState(headRight, head == headRight);

        SetRendererState(bodyTopLeft, false);
        SetRendererState(bodyTopRight, false);
        SetRendererState(bodyBottomLeft, false);
        SetRendererState(bodyBottomRight, false);

        SetRendererState(headTopLeft, false);
        SetRendererState(headTopRight, false);
        SetRendererState(headBottomLeft, false);
        SetRendererState(headBottomRight, false);
    }

    private static AnimatedSpriteRenderer PickDirectionalRenderer(
        AnimatedSpriteRenderer up,
        AnimatedSpriteRenderer down,
        AnimatedSpriteRenderer left,
        AnimatedSpriteRenderer right,
        Vector2 direction)
    {
        if (direction == Vector2.up) return up;
        if (direction == Vector2.down) return down;
        if (direction == Vector2.left) return left;
        if (direction == Vector2.right) return right;

        return down != null ? down : right;
    }

    private static void SetRendererState(AnimatedSpriteRenderer renderer, bool active)
    {
        if (renderer == null)
            return;

        renderer.enabled = active;

        if (renderer.TryGetComponent<SpriteRenderer>(out var sr))
            sr.enabled = active;

        if (!active)
            return;

        renderer.idle = false;
        renderer.loop = true;
        renderer.RefreshFrame();
    }

    private static CartEdge ResolveEdgeFromDirection(Vector2 inwardDirection)
    {
        if (inwardDirection == Vector2.right) return CartEdge.Left;
        if (inwardDirection == Vector2.left) return CartEdge.Right;
        if (inwardDirection == Vector2.down) return CartEdge.Top;
        if (inwardDirection == Vector2.up) return CartEdge.Bottom;
        return CartEdge.Left;
    }

    public void HideImmediately()
    {
        StopActiveAnimation();
        gameObject.SetActive(false);
    }

    private float GetMinAllowedXForHorizontalEdge()
    {
        return Mathf.Min(horizontalWallMinX, horizontalWallMaxX);
    }

    private float GetMaxAllowedXForHorizontalEdge()
    {
        return Mathf.Max(horizontalWallMinX, horizontalWallMaxX);
    }

    private float GetMinAllowedYForVerticalEdge()
    {
        return Mathf.Min(verticalWallMinY, verticalWallMaxY);
    }

    private float GetMaxAllowedYForVerticalEdge()
    {
        return Mathf.Max(verticalWallMinY, verticalWallMaxY);
    }

    private float GetTopSegmentLength()
    {
        return Mathf.Max(MinSegmentLength, GetMaxAllowedXForHorizontalEdge() - GetMinAllowedXForHorizontalEdge());
    }

    private float GetTopRightSegmentLength()
    {
        return GetDistance(
            new Vector2(GetMaxAllowedXForHorizontalEdge(), GetLogicalTopY()),
            new Vector2(GetLogicalRightX(), GetMaxAllowedYForVerticalEdge()));
    }

    private float GetBottomSegmentLength()
    {
        return Mathf.Max(MinSegmentLength, GetMaxAllowedXForHorizontalEdge() - GetMinAllowedXForHorizontalEdge());
    }

    private float GetBottomRightSegmentLength()
    {
        return GetDistance(
            new Vector2(GetLogicalRightX(), GetMinAllowedYForVerticalEdge()),
            new Vector2(GetMaxAllowedXForHorizontalEdge(), GetLogicalBottomY()));
    }

    private float GetBottomLeftSegmentLength()
    {
        return GetDistance(
            new Vector2(GetMinAllowedXForHorizontalEdge(), GetLogicalBottomY()),
            new Vector2(GetLogicalLeftX(), GetMinAllowedYForVerticalEdge()));
    }

    private float GetLeftSegmentLength()
    {
        return Mathf.Max(MinSegmentLength, GetMaxAllowedYForVerticalEdge() - GetMinAllowedYForVerticalEdge());
    }

    private float GetTopLeftSegmentLength()
    {
        return GetDistance(
            new Vector2(GetLogicalLeftX(), GetMaxAllowedYForVerticalEdge()),
            new Vector2(GetMinAllowedXForHorizontalEdge(), GetLogicalTopY()));
    }

    private float GetRightSegmentLength()
    {
        return Mathf.Max(MinSegmentLength, GetMaxAllowedYForVerticalEdge() - GetMinAllowedYForVerticalEdge());
    }

    private float GetPerimeterLength()
    {
        return
            GetTopSegmentLength() +
            GetTopRightSegmentLength() +
            GetRightSegmentLength() +
            GetBottomRightSegmentLength() +
            GetBottomSegmentLength() +
            GetBottomLeftSegmentLength() +
            GetLeftSegmentLength() +
            GetTopLeftSegmentLength();
    }

    private float GetDistance(Vector2 a, Vector2 b)
    {
        return Mathf.Max(MinSegmentLength, Vector2.Distance(a, b));
    }

    private float GetLogicalLeftX()
    {
        return leftWallX;
    }

    private float GetLogicalRightX()
    {
        return rightWallX;
    }

    private float GetLogicalTopY()
    {
        return topWallY;
    }

    private float GetLogicalBottomY()
    {
        return bottomWallY;
    }

    private Vector2 GetLogicalPositionForCurrentEdgeCenter()
    {
        switch (currentEdge)
        {
            case CartEdge.Left:
                return new Vector2(GetLogicalLeftX(), (GetMinAllowedYForVerticalEdge() + GetMaxAllowedYForVerticalEdge()) * 0.5f);

            case CartEdge.Right:
                return new Vector2(GetLogicalRightX(), (GetMinAllowedYForVerticalEdge() + GetMaxAllowedYForVerticalEdge()) * 0.5f);

            case CartEdge.Top:
                return new Vector2((GetMinAllowedXForHorizontalEdge() + GetMaxAllowedXForHorizontalEdge()) * 0.5f, GetLogicalTopY());

            case CartEdge.Bottom:
                return new Vector2((GetMinAllowedXForHorizontalEdge() + GetMaxAllowedXForHorizontalEdge()) * 0.5f, GetLogicalBottomY());

            default:
                return Vector2.zero;
        }
    }

    private Vector2 ClampLogicalPositionToEdge(CartEdge edge, Vector2 rawPosition)
    {
        switch (edge)
        {
            case CartEdge.Left:
                return new Vector2(
                    GetLogicalLeftX(),
                    Mathf.Clamp(rawPosition.y, GetMinAllowedYForVerticalEdge(), GetMaxAllowedYForVerticalEdge()));

            case CartEdge.Right:
                return new Vector2(
                    GetLogicalRightX(),
                    Mathf.Clamp(rawPosition.y, GetMinAllowedYForVerticalEdge(), GetMaxAllowedYForVerticalEdge()));

            case CartEdge.Top:
                return new Vector2(
                    Mathf.Clamp(rawPosition.x, GetMinAllowedXForHorizontalEdge(), GetMaxAllowedXForHorizontalEdge()),
                    GetLogicalTopY());

            case CartEdge.Bottom:
                return new Vector2(
                    Mathf.Clamp(rawPosition.x, GetMinAllowedXForHorizontalEdge(), GetMaxAllowedXForHorizontalEdge()),
                    GetLogicalBottomY());

            default:
                return rawPosition;
        }
    }

    private float NormalizePerimeterPosition(float value)
    {
        float perimeter = GetPerimeterLength();
        if (perimeter <= 0f)
            return 0f;

        return Mathf.Repeat(value, perimeter);
    }

    private float GetPerimeterPositionFromEdge(CartEdge edge, Vector2 logicalPosition)
    {
        float minX = GetMinAllowedXForHorizontalEdge();
        float maxX = GetMaxAllowedXForHorizontalEdge();
        float minY = GetMinAllowedYForVerticalEdge();
        float maxY = GetMaxAllowedYForVerticalEdge();

        float topLen = GetTopSegmentLength();
        float topRightLen = GetTopRightSegmentLength();
        float rightLen = GetRightSegmentLength();
        float bottomRightLen = GetBottomRightSegmentLength();
        float bottomLen = GetBottomSegmentLength();
        float bottomLeftLen = GetBottomLeftSegmentLength();

        switch (edge)
        {
            case CartEdge.Top:
                return Mathf.Clamp(logicalPosition.x, minX, maxX) - minX;

            case CartEdge.Right:
                return topLen + topRightLen + (maxY - Mathf.Clamp(logicalPosition.y, minY, maxY));

            case CartEdge.Bottom:
                return topLen + topRightLen + rightLen + bottomRightLen + (maxX - Mathf.Clamp(logicalPosition.x, minX, maxX));

            case CartEdge.Left:
                return topLen + topRightLen + rightLen + bottomRightLen + bottomLen + bottomLeftLen + (Mathf.Clamp(logicalPosition.y, minY, maxY) - minY);

            default:
                return 0f;
        }
    }

    void LateUpdate()
    {
        if (!debugCornerTransition || isAnimating)
            return;

        if (!Application.isPlaying || system == null || ownerPlayerId <= 0)
            return;

        if (!system.IsRuntimeEnabled || GamePauseController.IsPaused)
            return;

        DebugExplicitMovementBounds("LateUpdate");
    }

    private void GetEdgeAndLogicalPositionFromPerimeter(float perimeterValue, out CartEdge edge, out Vector2 logicalPosition)
    {
        GetSegmentAndLogicalPositionFromPerimeter(perimeterValue, out _, out edge, out logicalPosition);
    }

    private void GetSegmentAndLogicalPositionFromPerimeter(
        float perimeterValue,
        out CartSegment segment,
        out CartEdge edge,
        out Vector2 logicalPosition)
    {
        float s = NormalizePerimeterPosition(perimeterValue);

        float minX = GetMinAllowedXForHorizontalEdge();
        float maxX = GetMaxAllowedXForHorizontalEdge();
        float minY = GetMinAllowedYForVerticalEdge();
        float maxY = GetMaxAllowedYForVerticalEdge();

        float leftX = GetLogicalLeftX();
        float rightX = GetLogicalRightX();
        float topY = GetLogicalTopY();
        float bottomY = GetLogicalBottomY();

        float topLen = GetTopSegmentLength();
        float topRightLen = GetTopRightSegmentLength();
        float rightLen = GetRightSegmentLength();
        float bottomRightLen = GetBottomRightSegmentLength();
        float bottomLen = GetBottomSegmentLength();
        float bottomLeftLen = GetBottomLeftSegmentLength();
        float leftLen = GetLeftSegmentLength();
        float topLeftLen = GetTopLeftSegmentLength();

        if (s <= topLen)
        {
            segment = CartSegment.Top;
            edge = CartEdge.Top;
            logicalPosition = new Vector2(minX + s, topY);
            return;
        }

        s -= topLen;
        if (s < topRightLen)
        {
            float t = Mathf.Clamp01(s / topRightLen);
            segment = CartSegment.TopRight;
            edge = t < 0.5f ? CartEdge.Top : CartEdge.Right;
            logicalPosition = Vector2.Lerp(
                new Vector2(maxX, topY),
                new Vector2(rightX, maxY),
                t);
            return;
        }

        s -= topRightLen;
        if (s <= rightLen)
        {
            segment = CartSegment.Right;
            edge = CartEdge.Right;
            logicalPosition = new Vector2(rightX, maxY - s);
            return;
        }

        s -= rightLen;
        if (s < bottomRightLen)
        {
            float t = Mathf.Clamp01(s / bottomRightLen);
            segment = CartSegment.BottomRight;
            edge = t < 0.5f ? CartEdge.Right : CartEdge.Bottom;
            logicalPosition = Vector2.Lerp(
                new Vector2(rightX, minY),
                new Vector2(maxX, bottomY),
                t);
            return;
        }

        s -= bottomRightLen;
        if (s <= bottomLen)
        {
            segment = CartSegment.Bottom;
            edge = CartEdge.Bottom;
            logicalPosition = new Vector2(maxX - s, bottomY);
            return;
        }

        s -= bottomLen;
        if (s < bottomLeftLen)
        {
            float t = Mathf.Clamp01(s / bottomLeftLen);
            segment = CartSegment.BottomLeft;
            edge = t < 0.5f ? CartEdge.Bottom : CartEdge.Left;
            logicalPosition = Vector2.Lerp(
                new Vector2(minX, bottomY),
                new Vector2(leftX, minY),
                t);
            return;
        }

        s -= bottomLeftLen;
        if (s <= leftLen)
        {
            segment = CartSegment.Left;
            edge = CartEdge.Left;
            logicalPosition = new Vector2(leftX, minY + s);
            return;
        }

        s -= leftLen;
        if (s < topLeftLen)
        {
            float t = Mathf.Clamp01(s / topLeftLen);
            segment = CartSegment.TopLeft;
            edge = t < 0.5f ? CartEdge.Left : CartEdge.Top;
            logicalPosition = Vector2.Lerp(
                new Vector2(leftX, maxY),
                new Vector2(minX, topY),
                t);
            return;
        }

        segment = CartSegment.Top;
        edge = CartEdge.Top;
        logicalPosition = new Vector2(minX, topY);
    }

    private Vector3 BuildWorldPosition(CartEdge edge, Vector2 logicalPosition)
    {
        Vector3 result = new Vector3(
            logicalPosition.x,
            logicalPosition.y,
            0f);

        result.x = Mathf.Round(result.x * PixelsPerUnit) / PixelsPerUnit;
        result.y = Mathf.Round(result.y * PixelsPerUnit) / PixelsPerUnit;
        return result;
    }

    private void ApplyTransformFromPerimeterPosition()
    {
        Vector3 targetWorldPosition = BuildWorldPositionFromPerimeterPosition(perimeterPosition, out CartEdge edge, out CartSegment segment);
        transform.position = targetWorldPosition;
        currentEdge = edge;
        currentSegment = segment;

        DebugExplicitMovementBounds("ApplyTransform", targetWorldPosition);
    }

    private float GetWallMidpointPerimeterPosition(CartEdge wall)
    {
        float topLen = GetTopSegmentLength();
        float topRightLen = GetTopRightSegmentLength();
        float rightLen = GetRightSegmentLength();
        float bottomRightLen = GetBottomRightSegmentLength();
        float bottomLen = GetBottomSegmentLength();
        float bottomLeftLen = GetBottomLeftSegmentLength();
        float leftLen = GetLeftSegmentLength();

        switch (wall)
        {
            case CartEdge.Top:
                return topLen * 0.5f;

            case CartEdge.Right:
                return topLen + topRightLen + (rightLen * 0.5f);

            case CartEdge.Bottom:
                return topLen + topRightLen + rightLen + bottomRightLen + (bottomLen * 0.5f);

            case CartEdge.Left:
                return topLen + topRightLen + rightLen + bottomRightLen + bottomLen + bottomLeftLen + (leftLen * 0.5f);

            default:
                return 0f;
        }
    }

    private float GetShortestPerimeterDelta(float from, float to)
    {
        float perimeter = GetPerimeterLength();
        if (perimeter <= 0f)
            return 0f;

        float clockwise = Mathf.Repeat(to - from, perimeter);
        float counterClockwise = clockwise - perimeter;

        return Mathf.Abs(clockwise) <= Mathf.Abs(counterClockwise)
            ? clockwise
            : counterClockwise;
    }

    private void MoveTowardWallMidpoint(CartEdge targetWall)
    {
        float beforePerimeter = perimeterPosition;
        Vector3 beforePosition = transform.position;
        CartEdge beforeEdge = currentEdge;
        CartCorner beforeCorner = currentCorner;

        float targetPerimeter = GetWallMidpointPerimeterPosition(targetWall);
        float delta = GetShortestPerimeterDelta(perimeterPosition, targetPerimeter);

        if (Mathf.Approximately(delta, 0f))
            return;

        float step = moveSpeed * Time.unscaledDeltaTime;
        float move = Mathf.Clamp(delta, -step, step);

        perimeterPosition = NormalizePerimeterPosition(perimeterPosition + move);
        ApplyTransformFromPerimeterPosition();

        if (debugCornerTransition)
        {
            CartCorner afterCorner = ResolveCornerTransitionAt(perimeterPosition);

            bool changedState =
                beforeCorner != afterCorner ||
                beforeEdge != currentEdge ||
                lastDebugCorner != afterCorner ||
                lastDebugEdge != currentEdge;

            bool canPrintByInterval = Time.unscaledTime >= nextCornerDebugAt;

            if (changedState || canPrintByInterval)
            {
                nextCornerDebugAt = Time.unscaledTime + debugCornerTransitionInterval;
                lastDebugCorner = afterCorner;
                lastDebugEdge = currentEdge;

                Debug.Log(
                    $"[BattleRevenge][CornerMove] " +
                    $"targetWall={targetWall} " +
                    $"beforeEdge={beforeEdge} afterEdge={currentEdge} " +
                    $"beforeCorner={beforeCorner} afterCorner={afterCorner} " +
                    $"beforePerimeter={beforePerimeter:F3} afterPerimeter={perimeterPosition:F3} " +
                    $"targetPerimeter={targetPerimeter:F3} delta={delta:F3} step={step:F3} move={move:F3} " +
                    $"beforePos={beforePosition} afterPos={transform.position} " +
                    $"distanceMoved={Vector3.Distance(beforePosition, transform.position):F3}");
            }
        }
    }

    private bool TryGetTargetWallFromInput(PlayerInputManager input, out CartEdge targetWall)
    {
        bool holdUp = input.Get(ownerPlayerId, PlayerAction.MoveUp);
        bool holdDown = input.Get(ownerPlayerId, PlayerAction.MoveDown);
        bool holdLeft = input.Get(ownerPlayerId, PlayerAction.MoveLeft);
        bool holdRight = input.Get(ownerPlayerId, PlayerAction.MoveRight);

        bool canUp = holdUp && !holdDown;
        bool canDown = holdDown && !holdUp;
        bool canLeft = holdLeft && !holdRight;
        bool canRight = holdRight && !holdLeft;

        bool found = false;
        float bestDistance = float.MaxValue;
        CartEdge bestWall = currentEdge;

        if (canLeft)
            EvaluateTargetCandidate(CartEdge.Left, ref found, ref bestDistance, ref bestWall);

        if (canRight)
            EvaluateTargetCandidate(CartEdge.Right, ref found, ref bestDistance, ref bestWall);

        if (canUp)
            EvaluateTargetCandidate(CartEdge.Top, ref found, ref bestDistance, ref bestWall);

        if (canDown)
            EvaluateTargetCandidate(CartEdge.Bottom, ref found, ref bestDistance, ref bestWall);

        targetWall = bestWall;
        return found;
    }

    private void EvaluateTargetCandidate(
        CartEdge candidate,
        ref bool found,
        ref float bestDistance,
        ref CartEdge bestWall)
    {
        float targetPerimeter = GetWallMidpointPerimeterPosition(candidate);
        float delta = GetShortestPerimeterDelta(perimeterPosition, targetPerimeter);
        float distance = Mathf.Abs(delta);

        if (!found || distance < bestDistance)
        {
            found = true;
            bestDistance = distance;
            bestWall = candidate;
        }
    }

    private void UpdateMovementTilt(Vector3 movementDelta, bool hasInput)
    {
        if (!useMovementTilt)
        {
            currentTilt = 0f;
            targetTilt = 0f;
            return;
        }

        if (!hasInput || movementDelta.sqrMagnitude <= 0.000001f)
        {
            targetTilt = 0f;
            tiltHoldTimer = 0f;

            currentTilt = Mathf.MoveTowards(
                currentTilt,
                targetTilt,
                (tiltAngle / tiltOutDuration) * Time.unscaledDeltaTime);

            return;
        }

        Vector2 dir = movementDelta.normalized;

        float sign;

        if (Mathf.Abs(dir.x) >= Mathf.Abs(dir.y))
            sign = dir.x > 0f ? -1f : 1f;
        else
            sign = dir.y > 0f ? 1f : -1f;

        tiltHoldTimer += Time.unscaledDeltaTime;

        targetTilt = sign * tiltAngle;

        float speed = tiltAngle / tiltInDuration;

        currentTilt = Mathf.MoveTowards(
            currentTilt,
            targetTilt,
            speed * Time.unscaledDeltaTime);

        if (tiltHoldTimer >= tiltHoldDelay)
            currentTilt = targetTilt;
    }

    private void ApplyTiltToActiveVisuals()
    {
        ResetTilt(bodyUp);
        ResetTilt(bodyDown);
        ResetTilt(bodyLeft);
        ResetTilt(bodyRight);
        ResetTilt(bodyTopLeft);
        ResetTilt(bodyTopRight);
        ResetTilt(bodyBottomLeft);
        ResetTilt(bodyBottomRight);

        ResetTilt(headUp);
        ResetTilt(headDown);
        ResetTilt(headLeft);
        ResetTilt(headRight);
        ResetTilt(headTopLeft);
        ResetTilt(headTopRight);
        ResetTilt(headBottomLeft);
        ResetTilt(headBottomRight);

        ApplyTilt(GetActiveBodyRenderer());
        ApplyTilt(GetActiveHeadRenderer());
    }

    private void ApplyTilt(AnimatedSpriteRenderer renderer)
    {
        if (renderer == null)
            return;

        renderer.transform.localRotation = Quaternion.Euler(0f, 0f, currentTilt);
    }

    private void ResetTilt(AnimatedSpriteRenderer renderer)
    {
        if (renderer == null)
            return;

        renderer.transform.localRotation = Quaternion.identity;
    }

    private AnimatedSpriteRenderer GetActiveBodyRenderer()
    {
        if (currentCorner != CartCorner.None)
        {
            return currentCorner switch
            {
                CartCorner.TopLeft => bodyTopLeft,
                CartCorner.TopRight => bodyTopRight,
                CartCorner.BottomLeft => bodyBottomLeft,
                CartCorner.BottomRight => bodyBottomRight,
                _ => null
            };
        }

        return currentEdge switch
        {
            CartEdge.Left => bodyLeft,
            CartEdge.Right => bodyRight,
            CartEdge.Top => bodyUp,
            CartEdge.Bottom => bodyDown,
            _ => null
        };
    }

    private bool IsOnCornerBoundary(float perimeterValue)
    {
        if (!boundsConfigured)
            return false;

        float perimeter = GetPerimeterLength();
        if (perimeter <= MinSegmentLength * 4f)
            return false;

        float s = NormalizePerimeterPosition(perimeterValue);
        float topLen = GetTopSegmentLength();
        float topRightLen = GetTopRightSegmentLength();
        float rightLen = GetRightSegmentLength();
        float bottomRightLen = GetBottomRightSegmentLength();
        float bottomLen = GetBottomSegmentLength();
        float bottomLeftLen = GetBottomLeftSegmentLength();
        float leftLen = GetLeftSegmentLength();

        return
            DistanceOnPerimeter(s, 0f, perimeter) <= tileAlignmentTolerance ||
            DistanceOnPerimeter(s, topLen, perimeter) <= tileAlignmentTolerance ||
            DistanceOnPerimeter(s, topLen + topRightLen, perimeter) <= tileAlignmentTolerance ||
            DistanceOnPerimeter(s, topLen + topRightLen + rightLen, perimeter) <= tileAlignmentTolerance ||
            DistanceOnPerimeter(s, topLen + topRightLen + rightLen + bottomRightLen, perimeter) <= tileAlignmentTolerance ||
            DistanceOnPerimeter(s, topLen + topRightLen + rightLen + bottomRightLen + bottomLen, perimeter) <= tileAlignmentTolerance ||
            DistanceOnPerimeter(s, topLen + topRightLen + rightLen + bottomRightLen + bottomLen + bottomLeftLen, perimeter) <= tileAlignmentTolerance ||
            DistanceOnPerimeter(s, topLen + topRightLen + rightLen + bottomRightLen + bottomLen + bottomLeftLen + leftLen, perimeter) <= tileAlignmentTolerance;
    }

    private CartCorner ResolveCornerTransitionAt(float perimeterValue)
    {
        if (!boundsConfigured)
            return CartCorner.None;

        float perimeter = GetPerimeterLength();

        if (perimeter <= MinSegmentLength * 4f)
            return CartCorner.None;

        GetSegmentAndLogicalPositionFromPerimeter(perimeterValue, out CartSegment segment, out _, out _);
        CartCorner result = GetCornerForSegment(segment);

        if (debugCornerTransition && result != CartCorner.None && Time.unscaledTime >= nextCornerDebugAt)
        {
            nextCornerDebugAt = Time.unscaledTime + debugCornerTransitionInterval;

            Debug.Log(
                $"[BattleRevenge][CornerResolve] " +
                $"perimeterValue={perimeterValue:F3} normalized={NormalizePerimeterPosition(perimeterValue):F3} " +
                $"segment={segment} result={result} perimeter={perimeter:F3}");
        }

        return result;
    }

    private float DistanceOnPerimeter(float a, float b, float perimeter)
    {
        float d = Mathf.Abs(a - b);
        return Mathf.Min(d, perimeter - d);
    }

    private void SetCornerVisual(CartCorner corner)
    {
        AnimatedSpriteRenderer body = corner switch
        {
            CartCorner.TopLeft => bodyTopLeft,
            CartCorner.TopRight => bodyTopRight,
            CartCorner.BottomLeft => bodyBottomLeft,
            CartCorner.BottomRight => bodyBottomRight,
            _ => null
        };

        AnimatedSpriteRenderer head = corner switch
        {
            CartCorner.TopLeft => headTopLeft,
            CartCorner.TopRight => headTopRight,
            CartCorner.BottomLeft => headBottomLeft,
            CartCorner.BottomRight => headBottomRight,
            _ => null
        };

        SetRendererState(bodyUp, body == bodyUp);
        SetRendererState(bodyDown, body == bodyDown);
        SetRendererState(bodyLeft, body == bodyLeft);
        SetRendererState(bodyRight, body == bodyRight);

        SetRendererState(bodyTopLeft, body == bodyTopLeft);
        SetRendererState(bodyTopRight, body == bodyTopRight);
        SetRendererState(bodyBottomLeft, body == bodyBottomLeft);
        SetRendererState(bodyBottomRight, body == bodyBottomRight);

        SetRendererState(headUp, head == headUp);
        SetRendererState(headDown, head == headDown);
        SetRendererState(headLeft, head == headLeft);
        SetRendererState(headRight, head == headRight);

        SetRendererState(headTopLeft, head == headTopLeft);
        SetRendererState(headTopRight, head == headTopRight);
        SetRendererState(headBottomLeft, head == headBottomLeft);
        SetRendererState(headBottomRight, head == headBottomRight);
    }

    private Vector3 BuildWorldPositionFromPerimeterPosition(float perimeterValue, out CartEdge edge)
    {
        return BuildWorldPositionFromPerimeterPosition(perimeterValue, out edge, out _);
    }

    private Vector3 BuildWorldPositionFromPerimeterPosition(float perimeterValue, out CartEdge edge, out CartSegment segment)
    {
        GetSegmentAndLogicalPositionFromPerimeter(perimeterValue, out segment, out edge, out Vector2 logicalPosition);

        if (IsLaunchableWallSegment(segment))
            return BuildWorldPosition(edge, logicalPosition);

        return BuildDiagonalWorldPosition(segment, logicalPosition);
    }

    private Vector3 BuildDiagonalWorldPosition(CartSegment segment, Vector2 logicalPosition)
    {
        GetDiagonalEndpoints(segment, out CartEdge startEdge, out Vector2 start, out CartEdge endEdge, out Vector2 end);

        float length = GetDistance(start, end);
        float t = length <= MinSegmentLength
            ? 0f
            : Mathf.Clamp01(Vector2.Distance(start, logicalPosition) / length);

        Vector3 startWorld = BuildWorldPosition(startEdge, start);
        Vector3 endWorld = BuildWorldPosition(endEdge, end);
        Vector3 result = Vector3.Lerp(startWorld, endWorld, t);

        result.x = Mathf.Round(result.x * PixelsPerUnit) / PixelsPerUnit;
        result.y = Mathf.Round(result.y * PixelsPerUnit) / PixelsPerUnit;
        result.z = 0f;
        return result;
    }

    private void GetDiagonalEndpoints(
        CartSegment segment,
        out CartEdge startEdge,
        out Vector2 start,
        out CartEdge endEdge,
        out Vector2 end)
    {
        float minX = GetMinAllowedXForHorizontalEdge();
        float maxX = GetMaxAllowedXForHorizontalEdge();
        float minY = GetMinAllowedYForVerticalEdge();
        float maxY = GetMaxAllowedYForVerticalEdge();
        float leftX = GetLogicalLeftX();
        float rightX = GetLogicalRightX();
        float topY = GetLogicalTopY();
        float bottomY = GetLogicalBottomY();

        switch (segment)
        {
            case CartSegment.TopRight:
                startEdge = CartEdge.Top;
                start = new Vector2(maxX, topY);
                endEdge = CartEdge.Right;
                end = new Vector2(rightX, maxY);
                return;

            case CartSegment.BottomRight:
                startEdge = CartEdge.Right;
                start = new Vector2(rightX, minY);
                endEdge = CartEdge.Bottom;
                end = new Vector2(maxX, bottomY);
                return;

            case CartSegment.BottomLeft:
                startEdge = CartEdge.Bottom;
                start = new Vector2(minX, bottomY);
                endEdge = CartEdge.Left;
                end = new Vector2(leftX, minY);
                return;

            case CartSegment.TopLeft:
                startEdge = CartEdge.Left;
                start = new Vector2(leftX, maxY);
                endEdge = CartEdge.Top;
                end = new Vector2(minX, topY);
                return;
        }

        startEdge = EdgeFromSegment(segment);
        start = Vector2.zero;
        endEdge = startEdge;
        end = Vector2.zero;
    }

    private CartEdge EdgeFromSegment(CartSegment segment)
    {
        return segment switch
        {
            CartSegment.Right => CartEdge.Right,
            CartSegment.Bottom => CartEdge.Bottom,
            CartSegment.Left => CartEdge.Left,
            _ => CartEdge.Top
        };
    }

    private Vector2 GetCurrentLogicalPosition()
    {
        GetSegmentAndLogicalPositionFromPerimeter(perimeterPosition, out _, out _, out Vector2 logicalPosition);
        return logicalPosition;
    }

    private int GetCurrentLaunchDistanceTiles()
    {
        int requestedDistance = isChargingLaunch
            ? chargedLaunchDistanceTiles
            : minLaunchDistanceTiles;

        return Mathf.Clamp(requestedDistance, 3, 7);
    }

    private bool IsLaunchableWallSegment(CartSegment segment)
    {
        return
            segment == CartSegment.Top ||
            segment == CartSegment.Right ||
            segment == CartSegment.Bottom ||
            segment == CartSegment.Left;
    }

    private CartCorner GetCornerForSegment(CartSegment segment)
    {
        return segment switch
        {
            CartSegment.TopRight => CartCorner.TopRight,
            CartSegment.BottomRight => CartCorner.BottomRight,
            CartSegment.BottomLeft => CartCorner.BottomLeft,
            CartSegment.TopLeft => CartCorner.TopLeft,
            _ => CartCorner.None
        };
    }

    private void DebugActionALaunchState(
        bool holdingActionA,
        bool canLaunchBomb,
        bool wasChargingBeforeUpdate,
        string phase)
    {
        if (!debugActionALaunch)
            return;

        bool actionChanged = holdingActionA != lastHoldingActionA;
        bool shouldPrint = actionChanged || Time.unscaledTime >= nextActionADebugAt;

        if (!shouldPrint)
            return;

        nextActionADebugAt = Time.unscaledTime + debugActionAInterval;
        lastHoldingActionA = holdingActionA;

        CartCorner physicalCorner = ResolveCornerTransitionAt(perimeterPosition);
        CartCorner visualCorner = ResolveCornerVisualTransitionAt(perimeterPosition);

        float perimeter = GetPerimeterLength();
        float s = NormalizePerimeterPosition(perimeterPosition);
        Vector2 logicalPosition = GetCurrentLogicalPosition();

        Debug.Log(
            $"[BattleRevenge][ActionA:{phase}] " +
            $"owner={ownerPlayerId} " +
            $"holding={holdingActionA} " +
            $"canLaunch={canLaunchBomb} " +
            $"isChargingBefore={wasChargingBeforeUpdate} " +
            $"currentEdge={currentEdge} " +
            $"currentSegment={currentSegment} " +
            $"currentVisualCorner={currentCorner} " +
            $"physicalCorner={physicalCorner} " +
            $"visualCorner={visualCorner} " +
            $"launchableSegment={IsInLaunchableSegment} " +
            $"pos={transform.position} " +
            $"logicalPos={logicalPosition} " +
            $"perimeter={perimeterPosition:F3} normalized={s:F3} " +
            $"perimeterLength={perimeter:F3} " +
            $"cooldownRemaining={Mathf.Max(0f, nextBombAllowedAt - Time.unscaledTime):F3} " +
            $"chargedDistance={chargedLaunchDistanceTiles} " +
            $"launchDirection={LaunchDirection}");
    }

    private CartCorner ResolveCornerVisualTransitionAt(float perimeterValue)
    {
        if (!boundsConfigured)
            return CartCorner.None;

        GetSegmentAndLogicalPositionFromPerimeter(perimeterValue, out CartSegment segment, out _, out _);
        return GetCornerForSegment(segment);
    }

    private void DebugExplicitMovementBounds(string phase)
    {
        Vector3 expectedWorldPosition = BuildWorldPositionFromPerimeterPosition(
            perimeterPosition,
            out _,
            out _);

        DebugExplicitMovementBounds(phase, expectedWorldPosition);
    }

    private void DebugExplicitMovementBounds(string phase, Vector3 expectedWorldPosition)
    {
        if (!debugCornerTransition)
            return;

        GetSegmentAndLogicalPositionFromPerimeter(
            perimeterPosition,
            out CartSegment segment,
            out CartEdge edge,
            out Vector2 logicalPosition);

        bool isUsefulWall =
            segment == CartSegment.Left ||
            segment == CartSegment.Right ||
            segment == CartSegment.Top ||
            segment == CartSegment.Bottom;

        bool shouldLog = !isUsefulWall;

        if (segment == CartSegment.Left)
            shouldLog |= Mathf.Abs(transform.position.x - leftWallX) > tileAlignmentTolerance;
        else if (segment == CartSegment.Right)
            shouldLog |= Mathf.Abs(transform.position.x - rightWallX) > tileAlignmentTolerance;
        else if (segment == CartSegment.Top)
            shouldLog |= Mathf.Abs(transform.position.y - topWallY) > tileAlignmentTolerance;
        else if (segment == CartSegment.Bottom)
            shouldLog |= Mathf.Abs(transform.position.y - bottomWallY) > tileAlignmentTolerance;

        float worldExpectedDelta = Vector3.Distance(transform.position, expectedWorldPosition);
        shouldLog |= worldExpectedDelta > tileAlignmentTolerance;

        if (transform.parent != null)
            shouldLog |= Vector3.Distance(transform.localPosition, transform.position) > tileAlignmentTolerance;

        if (!shouldLog)
            return;

        if (Time.unscaledTime < nextExplicitBoundsDebugAt && worldExpectedDelta <= tileAlignmentTolerance)
            return;

        nextExplicitBoundsDebugAt = Time.unscaledTime + debugCornerTransitionInterval;

        Vector3 parentWorldPosition = transform.parent != null
            ? transform.parent.position
            : Vector3.zero;

        Debug.Log(
            $"[BattleRevenge][ExplicitBounds:{phase}] " +
            $"owner={ownerPlayerId} edge={edge} segment={segment} " +
            $"worldPos={transform.position} localPos={transform.localPosition} " +
            $"expectedWorld={expectedWorldPosition} parent={parentWorldPosition} " +
            $"logicalPos={logicalPosition} perimeter={perimeterPosition:F3} normalized={NormalizePerimeterPosition(perimeterPosition):F3} " +
            $"boundsX(horizontal={GetMinAllowedXForHorizontalEdge():F3}..{GetMaxAllowedXForHorizontalEdge():F3}, leftWall={leftWallX:F3}, rightWall={rightWallX:F3}) " +
            $"boundsY(vertical={GetMinAllowedYForVerticalEdge():F3}..{GetMaxAllowedYForVerticalEdge():F3}, topWall={topWallY:F3}, bottomWall={bottomWallY:F3}) " +
            $"worldMinusLogical=({transform.position.x - logicalPosition.x:F3}, {transform.position.y - logicalPosition.y:F3}) " +
            $"worldMinusExpected={worldExpectedDelta:F3}");
    }
}

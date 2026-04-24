using System;
using System.Collections;
using UnityEngine;

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

    [Header("Corner Transition")]
    [SerializeField, Min(0f)] private float cornerTransitionDistanceTiles = 0.85f;
    [SerializeField, Min(0f)] private float cornerVisualDistanceTiles = 0.4f;
    [SerializeField, Range(0.1f, 1f)] private float cornerWorldSpeedMultiplier = 1f;
    [SerializeField, Min(0.001f)] private float tileAlignmentTolerance = 0.03f;


    [Header("Movement")]
    [SerializeField, Min(0.1f)] private float moveSpeed = 4f;

    [Header("Movement Tilt")]
    [SerializeField] private bool useMovementTilt = true;
    [SerializeField, Min(0f)] private float tiltAngle = 6f;
    [SerializeField, Min(0.01f)] private float tiltInDuration = 0.08f;
    [SerializeField, Min(0.01f)] private float tiltOutDuration = 0.12f;
    [SerializeField, Min(0f)] private float tiltHoldDelay = 0.03f;

    [Header("Edge Offset")]
    [SerializeField, Min(0f)] private float horizontalInnerOffsetTiles = 1f;

    [Header("Per-Edge Offsets (Cart)")]
    [SerializeField] private Vector2 offsetLeft;
    [SerializeField] private Vector2 offsetRight;
    [SerializeField] private Vector2 offsetTop;
    [SerializeField] private Vector2 offsetBottom;

    [Header("Per-Edge Offsets (Head)")]
    [SerializeField] private Vector2 headOffsetLeft;
    [SerializeField] private Vector2 headOffsetRight;
    [SerializeField] private Vector2 headOffsetTop;
    [SerializeField] private Vector2 headOffsetBottom;

    [Header("Entrance / Exit Animation")]
    [SerializeField, Min(0.01f)] private float enterDuration = 0.35f;
    [SerializeField, Min(0.01f)] private float exitDuration = 0.25f;

    [Header("Blocked Edge Tiles")]
    [SerializeField, Min(0f)] private float blockedTilesOnLeftRightEdges = 1f;
    [SerializeField, Min(0f)] private float blockedTilesOnTopBottomEdges = 2f;
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

    private float minEdgeX;
    private float maxEdgeX;
    private float minEdgeY;
    private float maxEdgeY;
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

    public int OwnerPlayerId => ownerPlayerId;

    private float HorizontalInnerOffsetWorld => horizontalInnerOffsetTiles;

    private float nextCornerDebugAt;
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

    private bool CanLaunchBombNow()
    {
        return !IsInPhysicalCornerTransition();
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

        if (!canLaunchBomb)
        {
            if (isChargingLaunch)
            {
                isChargingLaunch = false;
                chargedLaunchDistanceTiles = Mathf.Clamp(minLaunchDistanceTiles, 3, 7);
            }

            HideLandingIndicator();
        }

        bool holdingActionA = input.Get(ownerPlayerId, PlayerAction.ActionA);

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

            UpdateLandingIndicator();
        }
        else
        {
            if (isChargingLaunch)
            {
                HideLandingIndicator();

                if (Time.unscaledTime >= nextBombAllowedAt)
                {
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

        Vector2 landingPos = system.GetPredictedLandingPosition(this, chargedLaunchDistanceTiles);

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

        if (Time.unscaledTime >= nextBombAllowedAt)
        {
            HideRechargeIndicator();
            return;
        }

        EnsureRechargeSpritesLoaded();

        float totalCooldown = Mathf.Max(0.01f, system != null ? system.CartBombCooldownSeconds : 1f);
        float remaining = Mathf.Max(0f, nextBombAllowedAt - Time.unscaledTime);
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
        minEdgeX = Mathf.Min(minX, maxX);
        maxEdgeX = Mathf.Max(minX, maxX);
        minEdgeY = Mathf.Min(minY, maxY);
        maxEdgeY = Mathf.Max(minY, maxY);
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

        Vector3 targetPosition = BuildWorldPosition(currentEdge, logicalTarget);
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

        Vector3 targetPosition = BuildWorldPosition(currentEdge, logicalTarget);
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
            : ResolveCornerTransitionAt(perimeterPosition, GetCornerVisualTransitionDistance());

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
        return minEdgeX + blockedTilesOnTopBottomEdges;
    }

    private float GetMaxAllowedXForHorizontalEdge()
    {
        return maxEdgeX - blockedTilesOnTopBottomEdges;
    }

    private float GetMinAllowedYForVerticalEdge()
    {
        return minEdgeY + blockedTilesOnLeftRightEdges;
    }

    private float GetMaxAllowedYForVerticalEdge()
    {
        return maxEdgeY - blockedTilesOnLeftRightEdges;
    }

    private float GetTopSegmentLength()
    {
        return Mathf.Max(MinSegmentLength, GetMaxAllowedXForHorizontalEdge() - GetMinAllowedXForHorizontalEdge());
    }

    private float GetBottomSegmentLength()
    {
        return Mathf.Max(MinSegmentLength, GetMaxAllowedXForHorizontalEdge() - GetMinAllowedXForHorizontalEdge());
    }

    private float GetLeftSegmentLength()
    {
        return Mathf.Max(MinSegmentLength, GetMaxAllowedYForVerticalEdge() - GetMinAllowedYForVerticalEdge());
    }

    private float GetRightSegmentLength()
    {
        return Mathf.Max(MinSegmentLength, GetMaxAllowedYForVerticalEdge() - GetMinAllowedYForVerticalEdge());
    }

    private float GetPerimeterLength()
    {
        return GetTopSegmentLength() + GetRightSegmentLength() + GetBottomSegmentLength() + GetLeftSegmentLength();
    }

    private float GetLogicalLeftX()
    {
        return Mathf.Min(maxEdgeX, minEdgeX + HorizontalInnerOffsetWorld);
    }

    private float GetLogicalRightX()
    {
        return Mathf.Max(minEdgeX, maxEdgeX - HorizontalInnerOffsetWorld);
    }

    private float GetLogicalTopY()
    {
        return maxEdgeY;
    }

    private float GetLogicalBottomY()
    {
        return minEdgeY;
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
        float rightLen = GetRightSegmentLength();
        float bottomLen = GetBottomSegmentLength();

        switch (edge)
        {
            case CartEdge.Top:
                return Mathf.Clamp(logicalPosition.x, minX, maxX) - minX;

            case CartEdge.Right:
                return topLen + (maxY - Mathf.Clamp(logicalPosition.y, minY, maxY));

            case CartEdge.Bottom:
                return topLen + rightLen + (maxX - Mathf.Clamp(logicalPosition.x, minX, maxX));

            case CartEdge.Left:
                return topLen + rightLen + bottomLen + (Mathf.Clamp(logicalPosition.y, minY, maxY) - minY);

            default:
                return 0f;
        }
    }

    private void GetEdgeAndLogicalPositionFromPerimeter(float perimeterValue, out CartEdge edge, out Vector2 logicalPosition)
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
        float rightLen = GetRightSegmentLength();
        float bottomLen = GetBottomSegmentLength();
        float leftLen = GetLeftSegmentLength();

        if (s < topLen)
        {
            edge = CartEdge.Top;
            logicalPosition = new Vector2(minX + s, topY);
            return;
        }

        s -= topLen;
        if (s < rightLen)
        {
            edge = CartEdge.Right;
            logicalPosition = new Vector2(rightX, maxY - s);
            return;
        }

        s -= rightLen;
        if (s < bottomLen)
        {
            edge = CartEdge.Bottom;
            logicalPosition = new Vector2(maxX - s, bottomY);
            return;
        }

        s -= bottomLen;
        if (s < leftLen)
        {
            edge = CartEdge.Left;
            logicalPosition = new Vector2(leftX, minY + s);
            return;
        }

        edge = CartEdge.Top;
        logicalPosition = new Vector2(minX, topY);
    }

    private Vector3 BuildWorldPosition(CartEdge edge, Vector2 logicalPosition)
    {
        Vector2 offset = edge switch
        {
            CartEdge.Left => offsetLeft,
            CartEdge.Right => offsetRight,
            CartEdge.Top => offsetTop,
            CartEdge.Bottom => offsetBottom,
            _ => Vector2.zero
        };

        Vector3 result = new Vector3(
            logicalPosition.x + offset.x,
            logicalPosition.y + offset.y,
            0f);

        result.x = Mathf.Round(result.x * PixelsPerUnit) / PixelsPerUnit;
        result.y = Mathf.Round(result.y * PixelsPerUnit) / PixelsPerUnit;
        return result;
    }

    private void ApplyTransformFromPerimeterPosition()
    {
        transform.position = BuildWorldPositionFromPerimeterPosition(perimeterPosition, out CartEdge edge);
        currentEdge = edge;
    }

    private float GetWallMidpointPerimeterPosition(CartEdge wall)
    {
        float topLen = GetTopSegmentLength();
        float rightLen = GetRightSegmentLength();
        float bottomLen = GetBottomSegmentLength();
        float leftLen = GetLeftSegmentLength();

        switch (wall)
        {
            case CartEdge.Top:
                return topLen * 0.5f;

            case CartEdge.Right:
                return topLen + (rightLen * 0.5f);

            case CartEdge.Bottom:
                return topLen + rightLen + (bottomLen * 0.5f);

            case CartEdge.Left:
                return topLen + rightLen + bottomLen + (leftLen * 0.5f);

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
        float maxCornerWorldStep = step * cornerWorldSpeedMultiplier;

        if (IsWithinCornerTransitionDistance(beforePerimeter) ||
            IsWithinCornerTransitionDistance(NormalizePerimeterPosition(perimeterPosition + move)))
        {
            move = ClampCornerMoveByWorldDistance(
                beforePosition,
                perimeterPosition,
                move,
                maxCornerWorldStep);
        }

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
                    $"distanceMoved={Vector3.Distance(beforePosition, transform.position):F3} " +
                    $"transitionTiles={cornerTransitionDistanceTiles:F3}");
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

    private float GetCornerVisualTransitionDistance()
    {
        if (cornerVisualDistanceTiles > 0f)
            return Mathf.Min(cornerTransitionDistanceTiles, cornerVisualDistanceTiles);

        return cornerTransitionDistanceTiles;
    }

    private bool IsWithinCornerTransitionDistance(float perimeterValue)
    {
        if (!boundsConfigured || cornerTransitionDistanceTiles <= 0f)
            return false;

        float perimeter = GetPerimeterLength();
        if (perimeter <= MinSegmentLength * 4f)
            return false;

        float s = NormalizePerimeterPosition(perimeterValue);
        float topLen = GetTopSegmentLength();
        float rightLen = GetRightSegmentLength();
        float bottomLen = GetBottomSegmentLength();
        float transition = Mathf.Max(0.001f, cornerTransitionDistanceTiles);

        return
            DistanceOnPerimeter(s, 0f, perimeter) <= transition ||
            DistanceOnPerimeter(s, topLen, perimeter) <= transition ||
            DistanceOnPerimeter(s, topLen + rightLen, perimeter) <= transition ||
            DistanceOnPerimeter(s, topLen + rightLen + bottomLen, perimeter) <= transition;
    }

    private bool IsInPhysicalCornerTransition()
    {
        return IsWithinCornerTransitionDistance(perimeterPosition);
    }

    private float ClampCornerMoveByWorldDistance(
        Vector3 fromWorldPosition,
        float fromPerimeter,
        float requestedMove,
        float maxWorldDistance)
    {
        if (Mathf.Approximately(requestedMove, 0f) || maxWorldDistance <= 0f)
            return requestedMove;

        Vector3 requestedWorldPosition = BuildWorldPositionFromPerimeterPosition(
            NormalizePerimeterPosition(fromPerimeter + requestedMove),
            out _);

        if (Vector3.Distance(fromWorldPosition, requestedWorldPosition) <= maxWorldDistance)
            return requestedMove;

        float direction = Mathf.Sign(requestedMove);
        float low = 0f;
        float high = Mathf.Abs(requestedMove);

        for (int i = 0; i < 10; i++)
        {
            float mid = (low + high) * 0.5f;
            Vector3 candidateWorldPosition = BuildWorldPositionFromPerimeterPosition(
                NormalizePerimeterPosition(fromPerimeter + direction * mid),
                out _);

            if (Vector3.Distance(fromWorldPosition, candidateWorldPosition) <= maxWorldDistance)
                low = mid;
            else
                high = mid;
        }

        return direction * low;
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
        float rightLen = GetRightSegmentLength();
        float bottomLen = GetBottomSegmentLength();

        return
            DistanceOnPerimeter(s, 0f, perimeter) <= tileAlignmentTolerance ||
            DistanceOnPerimeter(s, topLen, perimeter) <= tileAlignmentTolerance ||
            DistanceOnPerimeter(s, topLen + rightLen, perimeter) <= tileAlignmentTolerance ||
            DistanceOnPerimeter(s, topLen + rightLen + bottomLen, perimeter) <= tileAlignmentTolerance;
    }

    private CartCorner ResolveCornerTransitionAt(float perimeterValue)
    {
        return ResolveCornerTransitionAt(perimeterValue, cornerTransitionDistanceTiles);
    }

    private CartCorner ResolveCornerTransitionAt(float perimeterValue, float transitionDistance)
    {
        if (!boundsConfigured || transitionDistance <= 0f)
            return CartCorner.None;

        float topLen = GetTopSegmentLength();
        float rightLen = GetRightSegmentLength();
        float bottomLen = GetBottomSegmentLength();
        float perimeter = GetPerimeterLength();

        if (perimeter <= MinSegmentLength * 4f)
            return CartCorner.None;

        float s = NormalizePerimeterPosition(perimeterValue);

        float topLeft = 0f;
        float topRight = topLen;
        float bottomRight = topLen + rightLen;
        float bottomLeft = topLen + rightLen + bottomLen;

        float distTopLeft = DistanceOnPerimeter(s, topLeft, perimeter);
        float distTopRight = DistanceOnPerimeter(s, topRight, perimeter);
        float distBottomRight = DistanceOnPerimeter(s, bottomRight, perimeter);
        float distBottomLeft = DistanceOnPerimeter(s, bottomLeft, perimeter);

        CartCorner result = CartCorner.None;

        if (distTopLeft <= transitionDistance)
            result = CartCorner.TopLeft;
        else if (distTopRight <= transitionDistance)
            result = CartCorner.TopRight;
        else if (distBottomRight <= transitionDistance)
            result = CartCorner.BottomRight;
        else if (distBottomLeft <= transitionDistance)
            result = CartCorner.BottomLeft;

        if (debugCornerTransition && result != CartCorner.None && Time.unscaledTime >= nextCornerDebugAt)
        {
            nextCornerDebugAt = Time.unscaledTime + debugCornerTransitionInterval;

            Debug.Log(
                $"[BattleRevenge][CornerResolve] " +
                $"perimeterValue={perimeterValue:F3} normalized={s:F3} result={result} " +
                $"transitionTiles={transitionDistance:F3} " +
                $"distTL={distTopLeft:F3} distTR={distTopRight:F3} distBR={distBottomRight:F3} distBL={distBottomLeft:F3} " +
                $"topLen={topLen:F3} rightLen={rightLen:F3} bottomLen={bottomLen:F3} perimeter={perimeter:F3}");
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
        CartCorner corner = ResolveCornerTransitionAt(perimeterValue);

        if (corner != CartCorner.None)
            return BuildCornerTransitionWorldPosition(perimeterValue, corner, out edge);

        GetEdgeAndLogicalPositionFromPerimeter(perimeterValue, out edge, out Vector2 logicalPosition);
        return BuildWorldPosition(edge, logicalPosition);
    }

    private Vector3 BuildCornerTransitionWorldPosition(float perimeterValue, CartCorner corner, out CartEdge edge)
    {
        float perimeter = GetPerimeterLength();
        float s = NormalizePerimeterPosition(perimeterValue);

        float topLen = GetTopSegmentLength();
        float rightLen = GetRightSegmentLength();
        float bottomLen = GetBottomSegmentLength();

        float center = corner switch
        {
            CartCorner.TopLeft => 0f,
            CartCorner.TopRight => topLen,
            CartCorner.BottomRight => topLen + rightLen,
            CartCorner.BottomLeft => topLen + rightLen + bottomLen,
            _ => 0f
        };

        float signed = GetSignedDistanceFromCorner(center, s, perimeter);
        float transition = Mathf.Max(0.001f, cornerTransitionDistanceTiles);
        float t = Mathf.Clamp01(Mathf.Abs(signed) / transition);

        Vector3 cornerPoint = GetCornerWorldPosition(corner);
        Vector3 edgePoint = BuildLinearWorldPositionFromPerimeter(
            NormalizePerimeterPosition(center + (signed < 0f ? -transition : transition)),
            out CartEdge targetEdge);

        Vector3 control = GetCornerBranchControlWorldPosition(corner, signed < 0f);

        edge = targetEdge;

        Vector3 raw = EvaluateQuadraticBezier(cornerPoint, control, edgePoint, t);

        Vector3 result = raw;
        result.x = Mathf.Round(result.x * PixelsPerUnit) / PixelsPerUnit;
        result.y = Mathf.Round(result.y * PixelsPerUnit) / PixelsPerUnit;
        result.z = 0f;

        if (debugCornerTransition && Time.unscaledTime >= nextCornerDebugAt)
        {
            nextCornerDebugAt = Time.unscaledTime + debugCornerTransitionInterval;

            Debug.Log(
                $"[BattleRevenge][CornerBuild] " +
                $"corner={corner} edge={edge} perimeterValue={perimeterValue:F3} normalized={s:F3} " +
                $"center={center:F3} signed={signed:F3} transition={transition:F3} t={t:F3}");
        }

        return result;
    }

    private Vector3 BuildLinearWorldPositionFromPerimeter(float perimeterValue, out CartEdge edge)
    {
        GetEdgeAndLogicalPositionFromPerimeter(perimeterValue, out edge, out Vector2 logicalPosition);
        return BuildWorldPosition(edge, logicalPosition);
    }

    private Vector3 GetCornerWorldPosition(CartCorner corner)
    {
        switch (corner)
        {
            case CartCorner.TopLeft:
                return BuildWorldPosition(CartEdge.Top, new Vector2(GetMinAllowedXForHorizontalEdge(), GetLogicalTopY()));

            case CartCorner.TopRight:
                return BuildWorldPosition(CartEdge.Top, new Vector2(GetMaxAllowedXForHorizontalEdge(), GetLogicalTopY()));

            case CartCorner.BottomRight:
                return BuildWorldPosition(CartEdge.Bottom, new Vector2(GetMaxAllowedXForHorizontalEdge(), GetLogicalBottomY()));

            case CartCorner.BottomLeft:
                return BuildWorldPosition(CartEdge.Bottom, new Vector2(GetMinAllowedXForHorizontalEdge(), GetLogicalBottomY()));

            default:
                return transform.position;
        }
    }

    private Vector3 GetCornerBranchControlWorldPosition(CartCorner corner, bool useNegativeBranch)
    {
        Vector3 cornerPoint = GetCornerWorldPosition(corner);
        Vector3 edgeAnchor;
        Vector3 control;

        switch (corner)
        {
            case CartCorner.TopLeft:
                edgeAnchor = useNegativeBranch
                    ? BuildWorldPosition(CartEdge.Left, new Vector2(GetLogicalLeftX(), GetMaxAllowedYForVerticalEdge()))
                    : BuildWorldPosition(CartEdge.Top, new Vector2(GetMinAllowedXForHorizontalEdge(), GetLogicalTopY()));
                control = useNegativeBranch
                    ? new Vector3(edgeAnchor.x, cornerPoint.y, 0f)
                    : cornerPoint;
                break;

            case CartCorner.TopRight:
                edgeAnchor = useNegativeBranch
                    ? BuildWorldPosition(CartEdge.Top, new Vector2(GetMaxAllowedXForHorizontalEdge(), GetLogicalTopY()))
                    : BuildWorldPosition(CartEdge.Right, new Vector2(GetLogicalRightX(), GetMaxAllowedYForVerticalEdge()));
                control = useNegativeBranch
                    ? cornerPoint
                    : new Vector3(edgeAnchor.x, cornerPoint.y, 0f);
                break;

            case CartCorner.BottomRight:
                edgeAnchor = useNegativeBranch
                    ? BuildWorldPosition(CartEdge.Right, new Vector2(GetLogicalRightX(), GetMinAllowedYForVerticalEdge()))
                    : BuildWorldPosition(CartEdge.Bottom, new Vector2(GetMaxAllowedXForHorizontalEdge(), GetLogicalBottomY()));
                control = useNegativeBranch
                    ? new Vector3(edgeAnchor.x, cornerPoint.y, 0f)
                    : cornerPoint;
                break;

            case CartCorner.BottomLeft:
                edgeAnchor = useNegativeBranch
                    ? BuildWorldPosition(CartEdge.Bottom, new Vector2(GetMinAllowedXForHorizontalEdge(), GetLogicalBottomY()))
                    : BuildWorldPosition(CartEdge.Left, new Vector2(GetLogicalLeftX(), GetMinAllowedYForVerticalEdge()));
                control = useNegativeBranch
                    ? cornerPoint
                    : new Vector3(edgeAnchor.x, cornerPoint.y, 0f);
                break;

            default:
                return cornerPoint;
        }

        control.z = 0f;
        return control;
    }

    private static Vector3 EvaluateQuadraticBezier(Vector3 start, Vector3 control, Vector3 end, float t)
    {
        float oneMinusT = 1f - t;
        return
            (oneMinusT * oneMinusT * start) +
            (2f * oneMinusT * t * control) +
            (t * t * end);
    }

    private float GetSignedDistanceFromCorner(float cornerPosition, float currentPosition, float perimeter)
    {
        if (perimeter <= 0f)
            return 0f;

        float delta = Mathf.Repeat(currentPosition - cornerPosition + perimeter * 0.5f, perimeter) - perimeter * 0.5f;
        return delta;
    }
}

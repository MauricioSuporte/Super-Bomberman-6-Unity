using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BattleRevengeCartController : MonoBehaviour
{
    private enum CartEdge
    {
        Left = 0,
        Right = 1,
        Top = 2,
        Bottom = 3
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

    [Header("Movement")]
    [SerializeField, Min(0.1f)] private float moveSpeed = 4f;

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

    private const int RechargeFrameCount = 33;

    private bool isAnimating;
    private Coroutine activeAnimationRoutine;

    private BattleRevengeSystem system;
    private int ownerPlayerId;
    private CartEdge currentEdge = CartEdge.Left;

    private float minEdgeX;
    private float maxEdgeX;
    private float minEdgeY;
    private float maxEdgeY;

    private float nextBombAllowedAt;

    private bool isChargingLaunch;
    private float nextChargeStepAt;
    private int chargedLaunchDistanceTiles = 3;

    private Sprite[] rechargeSprites;
    private bool rechargeSpritesLoaded;

    public int OwnerPlayerId => ownerPlayerId;

    private float HorizontalInnerOffsetWorld => horizontalInnerOffsetTiles;

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

        bool holdUp = input.Get(ownerPlayerId, PlayerAction.MoveUp);
        bool holdDown = input.Get(ownerPlayerId, PlayerAction.MoveDown);
        bool holdLeft = input.Get(ownerPlayerId, PlayerAction.MoveLeft);
        bool holdRight = input.Get(ownerPlayerId, PlayerAction.MoveRight);

        float verticalAxis = holdUp && !holdDown ? 1f :
                             holdDown && !holdUp ? -1f : 0f;

        float horizontalAxis = holdRight && !holdLeft ? 1f :
                               holdLeft && !holdRight ? -1f : 0f;

        Vector3 position = transform.position;

        switch (currentEdge)
        {
            case CartEdge.Left:
            case CartEdge.Right:
                {
                    if (verticalAxis != 0f)
                    {
                        float minAllowedY = GetMinAllowedYForVerticalEdge();
                        float maxAllowedY = GetMaxAllowedYForVerticalEdge();

                        position.y = Mathf.Clamp(
                            position.y + (verticalAxis * moveSpeed * Time.unscaledDeltaTime),
                            minAllowedY,
                            maxAllowedY);
                    }

                    if (horizontalAxis != 0f)
                        currentEdge = position.y >= GetVerticalMidpoint() ? CartEdge.Top : CartEdge.Bottom;

                    break;
                }

            case CartEdge.Top:
            case CartEdge.Bottom:
                {
                    if (horizontalAxis != 0f)
                    {
                        float minAllowedX = GetMinAllowedXForHorizontalEdge();
                        float maxAllowedX = GetMaxAllowedXForHorizontalEdge();

                        position.x = Mathf.Clamp(
                            position.x + (horizontalAxis * moveSpeed * Time.unscaledDeltaTime),
                            minAllowedX,
                            maxAllowedX);
                    }

                    if (verticalAxis != 0f)
                        currentEdge = position.x >= GetHorizontalMidpoint() ? CartEdge.Right : CartEdge.Left;

                    break;
                }
        }

        ApplyEdgePosition(ref position);
        transform.position = position;
        RefreshVisualByEdge();

        UpdateRechargeIndicator();

        bool holdingActionA = input.Get(ownerPlayerId, PlayerAction.ActionA);

        if (holdingActionA)
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
                        nextBombAllowedAt = Time.unscaledTime + system.CartBombCooldownSeconds;
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

        Vector3 position = transform.position;
        position.x = Mathf.Clamp(position.x, minEdgeX, maxEdgeX);
        position.y = Mathf.Clamp(position.y, minEdgeY, maxEdgeY);

        ApplyEdgePosition(ref position);
        transform.position = position;

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

        Vector3 targetPosition = new Vector3(startPosition.x, startPosition.y, 0f);
        ApplyEdgePosition(ref targetPosition);

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

        Vector3 targetPosition = new Vector3(startPosition.x, startPosition.y, 0f);
        ApplyEdgePosition(ref targetPosition);

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
        const float pixelsPerUnit = 16f;

        Vector3 raw = Vector3.Lerp(from, to, t);
        raw.x = Mathf.Round(raw.x * pixelsPerUnit) / pixelsPerUnit;
        raw.y = Mathf.Round(raw.y * pixelsPerUnit) / pixelsPerUnit;
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
    }

    private void RefreshVisualByEdge()
    {
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

    private void ApplyEdgePosition(ref Vector3 position)
    {
        switch (currentEdge)
        {
            case CartEdge.Left:
                position.x = Mathf.Min(maxEdgeX, minEdgeX + HorizontalInnerOffsetWorld);
                position.y = Mathf.Clamp(
                    position.y,
                    GetMinAllowedYForVerticalEdge(),
                    GetMaxAllowedYForVerticalEdge());
                break;

            case CartEdge.Right:
                position.x = Mathf.Max(minEdgeX, maxEdgeX - HorizontalInnerOffsetWorld);
                position.y = Mathf.Clamp(
                    position.y,
                    GetMinAllowedYForVerticalEdge(),
                    GetMaxAllowedYForVerticalEdge());
                break;

            case CartEdge.Top:
                position.y = maxEdgeY;
                position.x = Mathf.Clamp(
                    position.x,
                    GetMinAllowedXForHorizontalEdge(),
                    GetMaxAllowedXForHorizontalEdge());
                break;

            case CartEdge.Bottom:
                position.y = minEdgeY;
                position.x = Mathf.Clamp(
                    position.x,
                    GetMinAllowedXForHorizontalEdge(),
                    GetMaxAllowedXForHorizontalEdge());
                break;
        }

        Vector2 cartOffset = GetCartOffset();
        position.x += cartOffset.x;
        position.y += cartOffset.y;
    }

    private void ApplyHeadOffsetForCurrentEdge()
    {
        Vector2 offset = GetHeadOffset();
        AnimatedSpriteRenderer activeHead = GetActiveHeadRenderer();

        ClearHeadRuntimeOffsets(headUp);
        ClearHeadRuntimeOffsets(headDown);
        ClearHeadRuntimeOffsets(headLeft);
        ClearHeadRuntimeOffsets(headRight);

        if (activeHead != null)
        {
            activeHead.SetRuntimeBaseLocalX(offset.x);
            activeHead.SetRuntimeBaseLocalY(offset.y);
            activeHead.RefreshFrame();
        }
    }

    private void ClearHeadRuntimeOffsets(AnimatedSpriteRenderer renderer)
    {
        if (renderer == null)
            return;

        renderer.ClearRuntimeBaseOffset();
        renderer.RefreshFrame();
    }

    private Vector2 GetCartOffset() => currentEdge switch
    {
        CartEdge.Left => offsetLeft,
        CartEdge.Right => offsetRight,
        CartEdge.Top => offsetTop,
        CartEdge.Bottom => offsetBottom,
        _ => Vector2.zero
    };

    private Vector2 GetHeadOffset() => currentEdge switch
    {
        CartEdge.Left => headOffsetLeft,
        CartEdge.Right => headOffsetRight,
        CartEdge.Top => headOffsetTop,
        CartEdge.Bottom => headOffsetBottom,
        _ => Vector2.zero
    };

    private Vector2 GetRechargeOffset() => currentEdge switch
    {
        CartEdge.Left => rechargeOffsetLeft,
        CartEdge.Right => rechargeOffsetRight,
        CartEdge.Top => rechargeOffsetTop,
        CartEdge.Bottom => rechargeOffsetBottom,
        _ => Vector2.zero
    };

    private AnimatedSpriteRenderer GetActiveHeadRenderer() => currentEdge switch
    {
        CartEdge.Left => headLeft,
        CartEdge.Right => headRight,
        CartEdge.Top => headUp,
        CartEdge.Bottom => headDown,
        _ => null
    };

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

        renderer.idle = true;
        renderer.enabled = active;
        renderer.RefreshFrame();

        if (renderer.TryGetComponent<SpriteRenderer>(out var sr))
            sr.enabled = active;
    }

    private float GetHorizontalMidpoint() => (minEdgeX + maxEdgeX) * 0.5f;
    private float GetVerticalMidpoint() => (minEdgeY + maxEdgeY) * 0.5f;

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
}
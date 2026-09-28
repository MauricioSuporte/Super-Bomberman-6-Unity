using UnityEngine;

/// <summary>
/// A junction-turning enemy that pursues a player it can see in a cardinal
/// direction, then slides until an obstacle stops it when the player is close enough.
/// </summary>
public sealed class EskimoMovementController : JunctionTurningEnemyMovementController
{
    private const int SlideTileCount = 3;
    private const int SlidePixelsPerUnit = 16;

    [Header("Player Pursuit")]
    [SerializeField, Min(0.1f)] private float visionDistance = 10f;
    [SerializeField, Min(0.001f)] private float alignedToleranceTiles = 0.15f;
    [SerializeField, Range(0.1f, 1f)] private float scanBoxSizePercent = 0.6f;
    [SerializeField] private LayerMask playerLayerMask;
    [SerializeField, Min(1f)] private float pursuitSpeedMultiplier = 1.5f;

    [Header("Slide")]
    [SerializeField, Min(0.01f)] private float slideSpeed = 3.75f;
    [SerializeField] private AnimatedSpriteRenderer slideUp;
    [SerializeField] private AnimatedSpriteRenderer slideDown;
    [SerializeField] private AnimatedSpriteRenderer slideLeft;
    [SerializeField] private AnimatedSpriteRenderer slideRight;

    private float normalSpeed;
    private bool isSliding;
    private float slideElapsed;
    private Vector2 slideStart;

    protected override void Awake()
    {
        base.Awake();

        normalSpeed = speed;

        if (playerLayerMask.value == 0)
            playerLayerMask = LayerMask.GetMask("Player");
    }

    protected override void FixedUpdate()
    {
        if (!isSliding)
        {
            base.FixedUpdate();
            return;
        }

        if (isDead || IsTemporarilyUnableToMove())
            return;

        AdvanceSlide();
    }

    protected override void DecideNextTile()
    {
        if (isSliding)
            return;

        if (TryGetPlayerDirection(out Vector2 playerDirection, out int playerDistanceInTiles))
        {
            speed = normalSpeed * pursuitSpeedMultiplier;
            direction = playerDirection;
            UpdateSpriteDirection(direction);

            if (playerDistanceInTiles <= SlideTileCount)
            {
                BeginSlide();
                return;
            }

            targetTile = rb.position + direction * tileSize;
            return;
        }

        speed = normalSpeed;
        base.DecideNextTile();
    }

    protected override void UpdateSpriteDirection(Vector2 dir)
    {
        if (isSliding)
            return;

        base.UpdateSpriteDirection(dir);
    }

    protected override void OnTriggerEnter2D(Collider2D other)
    {
        if (isSliding && other != null && other.gameObject.layer == LayerMask.NameToLayer("Bomb"))
        {
            EndSlideAndResumeWalking();
            return;
        }

        base.OnTriggerEnter2D(other);
    }

    protected override void Die()
    {
        if (isDead)
            return;

        isSliding = false;
        slideElapsed = 0f;
        HideSlideVisuals();

        base.Die();
    }

    private void BeginSlide()
    {
        isSliding = true;
        slideElapsed = 0f;
        slideStart = rb.position;
        targetTile = rb.position;

        ShowSlideVisual(direction);
    }

    private void AdvanceSlide()
    {
        slideElapsed += Time.fixedDeltaTime;
        float distance = Mathf.Round(slideElapsed * slideSpeed * SlidePixelsPerUnit) / SlidePixelsPerUnit;

        // Check every tile crossed this physics step. The slide has no distance
        // limit; only a blocked tile ends it.
        float currentDistance = Vector2.Dot(rb.position - slideStart, direction);
        int firstTile = Mathf.FloorToInt(currentDistance / tileSize) + 1;
        int lastTile = Mathf.FloorToInt(distance / tileSize);
        for (int tile = firstTile; tile <= lastTile; tile++)
        {
            if (IsTileBlocked(slideStart + direction * (tile * tileSize)))
            {
                EndSlideAndResumeWalking();
                return;
            }
        }

        // Sample the whole trajectory from its origin, without pauses at tile
        // boundaries or a final grid snap. Each position advances in whole pixels.
        rb.MovePosition(slideStart + direction * distance);
    }

    private void EndSlideAndResumeWalking()
    {
        isSliding = false;
        slideElapsed = 0f;
        speed = normalSpeed;
        HideSlideVisuals();
        base.UpdateSpriteDirection(direction);
        base.DecideNextTile();
    }

    private bool IsTemporarilyUnableToMove()
    {
        if (TryGetComponent<StunReceiver>(out var stun) && stun != null && stun.IsStunned)
        {
            rb.linearVelocity = Vector2.zero;
            return true;
        }

        if (isInDamagedLoop)
        {
            rb.linearVelocity = Vector2.zero;
            return true;
        }

        return false;
    }

    private bool TryGetPlayerDirection(out Vector2 directionToPlayer, out int distanceInTiles)
    {
        directionToPlayer = Vector2.zero;
        distanceInTiles = 0;

        Vector2[] directions = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
        int maxSteps = Mathf.Max(1, Mathf.FloorToInt(visionDistance / tileSize));
        float boxSize = Mathf.Clamp(scanBoxSizePercent, 0.1f, 1f) * tileSize;
        float alignmentTolerance = Mathf.Max(0.001f, alignedToleranceTiles * tileSize);

        for (int directionIndex = 0; directionIndex < directions.Length; directionIndex++)
        {
            Vector2 scanDirection = directions[directionIndex];
            bool verticalScan = scanDirection == Vector2.up || scanDirection == Vector2.down;

            for (int step = 1; step <= maxSteps; step++)
            {
                Vector2 tileCenter = rb.position + step * tileSize * scanDirection;

                // Obstacles are tested before the player so a bomb, destructible
                // tile, or indestructible tile cannot be seen through.
                if (IsTileBlocked(tileCenter))
                    break;

                Collider2D[] hits = Physics2D.OverlapBoxAll(tileCenter, Vector2.one * boxSize, 0f, playerLayerMask);
                for (int hitIndex = 0; hitIndex < hits.Length; hitIndex++)
                {
                    Collider2D playerCollider = hits[hitIndex];
                    if (playerCollider == null)
                        continue;

                    Vector2 playerPosition = playerCollider.attachedRigidbody != null
                        ? playerCollider.attachedRigidbody.position
                        : (Vector2)playerCollider.transform.position;

                    bool aligned = verticalScan
                        ? Mathf.Abs(playerPosition.x - rb.position.x) <= alignmentTolerance
                        : Mathf.Abs(playerPosition.y - rb.position.y) <= alignmentTolerance;

                    if (!aligned)
                        continue;

                    directionToPlayer = scanDirection;
                    distanceInTiles = step;
                    return true;
                }
            }
        }

        return false;
    }

    private void ShowSlideVisual(Vector2 slideDirection)
    {
        if (spriteUp != null) spriteUp.enabled = false;
        if (spriteDown != null) spriteDown.enabled = false;
        if (spriteLeft != null) spriteLeft.enabled = false;
        if (spriteRight != null) spriteRight.enabled = false;

        AnimatedSpriteRenderer selected = slideDirection == Vector2.up ? slideUp :
            slideDirection == Vector2.down ? slideDown :
            slideDirection == Vector2.right && slideRight != null ? slideRight : slideLeft;

        HideSlideVisuals();

        if (selected == null)
            return;

        selected.enabled = true;
        selected.idle = false;
        selected.loop = true;
        activeSprite = selected;

        if (slideDirection == Vector2.right && selected == slideLeft && selected.TryGetComponent<SpriteRenderer>(out var renderer))
            renderer.flipX = true;
        else if (selected.TryGetComponent<SpriteRenderer>(out var resetRenderer))
            resetRenderer.flipX = false;
    }

    private void HideSlideVisuals()
    {
        if (slideUp != null) slideUp.enabled = false;
        if (slideDown != null) slideDown.enabled = false;
        if (slideLeft != null) slideLeft.enabled = false;
        if (slideRight != null) slideRight.enabled = false;
    }
}

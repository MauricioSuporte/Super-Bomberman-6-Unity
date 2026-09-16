using UnityEngine;

/// <summary>
/// A junction-turning enemy that pursues a player it can see in a cardinal
/// direction, then slides three tiles when the player is close enough.
/// </summary>
public sealed class EskimoMovementController : JunctionTurningEnemyMovementController
{
    private const int JumpTileCount = 3;
    private static Sprite slideShadowSprite;

    [Header("Player Pursuit")]
    [SerializeField, Min(0.1f)] private float visionDistance = 10f;
    [SerializeField, Min(0.001f)] private float alignedToleranceTiles = 0.15f;
    [SerializeField, Range(0.1f, 1f)] private float scanBoxSizePercent = 0.6f;
    [SerializeField] private LayerMask playerLayerMask;
    [SerializeField, Min(1f)] private float pursuitSpeedMultiplier = 1.5f;

    [Header("Slide")]
    [SerializeField, Min(0.01f)] private float slideDuration = 0.5f;
    [SerializeField, Min(0f)] private float slideHeightTiles = 0.5f;
    [SerializeField, Min(1)] private int pixelsPerUnit = 16;
    [SerializeField, Min(0f)] private float recoveryPauseSeconds = 0.5f;
    [SerializeField] private AnimatedSpriteRenderer slideUp;
    [SerializeField] private AnimatedSpriteRenderer slideDown;
    [SerializeField] private AnimatedSpriteRenderer slideLeft;
    [SerializeField] private AnimatedSpriteRenderer slideRight;

    [Header("Slide Shadow")]
    [SerializeField] private Color shadowColor = new(0f, 0f, 0f, 0.45f);
    [SerializeField] private Vector2 shadowScale = new(0.9f, 0.9f);
    [SerializeField] private Vector2 shadowOffset = new(0f, -0.1875f);

    private float normalSpeed;
    private bool isSliding;
    private bool isRecovering;
    private bool slideBlocked;
    private float slideElapsed;
    private float recoveryElapsed;
    private int remainingSlideTiles;
    private Vector2 slideTarget;
    private Vector2 slideGroundStart;
    private GameObject slideShadow;

    protected override void Awake()
    {
        base.Awake();

        normalSpeed = speed;

        if (playerLayerMask.value == 0)
            playerLayerMask = LayerMask.GetMask("Player");
    }

    protected override void FixedUpdate()
    {
        if (!isSliding && !isRecovering)
        {
            base.FixedUpdate();
            return;
        }

        if (isDead || IsTemporarilyUnableToMove())
            return;

        if (isRecovering)
        {
            rb.linearVelocity = Vector2.zero;
            recoveryElapsed += Time.fixedDeltaTime;

            if (recoveryElapsed >= recoveryPauseSeconds)
                ResumeJunctionWalking();

            return;
        }

        AdvanceSlide();
    }

    protected override void DecideNextTile()
    {
        if (isSliding || isRecovering)
            return;

        if (TryGetPlayerDirection(out Vector2 playerDirection, out int playerDistanceInTiles))
        {
            speed = normalSpeed * pursuitSpeedMultiplier;
            direction = playerDirection;
            UpdateSpriteDirection(direction);

            if (playerDistanceInTiles <= JumpTileCount)
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
        if (isSliding || isRecovering)
            return;

        base.UpdateSpriteDirection(dir);
    }

    protected override void OnTriggerEnter2D(Collider2D other)
    {
        if (isSliding && other != null && other.gameObject.layer == LayerMask.NameToLayer("Bomb"))
        {
            StopSlideHorizontalMotion();
            return;
        }

        base.OnTriggerEnter2D(other);
    }

    protected override void Die()
    {
        DestroySlideShadow();
        ClearSlideArc();
        base.Die();
    }

    protected override void OnDestroy()
    {
        DestroySlideShadow();
        base.OnDestroy();
    }

    private void LateUpdate()
    {
        if (slideShadow != null && isSliding)
            UpdateSlideShadowPosition();
    }

    private void BeginSlide()
    {
        isSliding = true;
        slideBlocked = false;
        slideElapsed = 0f;
        remainingSlideTiles = JumpTileCount;
        targetTile = rb.position;
        slideTarget = rb.position + direction * tileSize;
        slideGroundStart = rb.position;

        ShowSlideVisual(direction);
        CreateSlideShadow();
    }

    private void AdvanceSlide()
    {
        float duration = Mathf.Max(0.01f, slideDuration);
        slideElapsed = Mathf.Min(slideElapsed + Time.fixedDeltaTime, duration);
        ApplySlideArc(slideElapsed / duration);

        // A collision stops only the horizontal motion. The arc timer keeps
        // advancing so a blocked Eskimo visibly descends from its current
        // height instead of snapping straight down to the ground.
        if (!slideBlocked && IsTileBlocked(slideTarget))
        {
            StopSlideHorizontalMotion();
        }

        if (!slideBlocked)
        {
            float slideSpeed = JumpTileCount * tileSize / duration;
            rb.MovePosition(Vector2.MoveTowards(rb.position, slideTarget, slideSpeed * Time.fixedDeltaTime));

            if (ReachedSlideTile())
            {
                SnapToGrid();
                remainingSlideTiles--;

                if (remainingSlideTiles > 0)
                    slideTarget = rb.position + direction * tileSize;
            }
        }

        if (slideElapsed >= duration)
        {
            BeginRecovery();
        }
    }

    private bool ReachedSlideTile()
    {
        return Vector2.Distance(rb.position, slideTarget) < 0.01f;
    }

    private void BeginRecovery()
    {
        bool completedSlide = !slideBlocked;

        // The slide ends on a grid cell. Without this, a fractional final
        // slide position can make the subsequent walking target fractional
        // too, causing EnemyMovementController.SnapToGrid to visibly jump.
        if (completedSlide)
            SnapToGrid();

        isSliding = false;
        isRecovering = true;
        slideBlocked = false;
        recoveryElapsed = 0f;
        remainingSlideTiles = 0;
        targetTile = rb.position;

        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        ClearSlideArc();
        SetSlideShadowVisible(false);
    }

    private void StopSlideHorizontalMotion()
    {
        slideBlocked = true;
        remainingSlideTiles = 0;
        targetTile = rb.position;

        if (rb != null)
            rb.linearVelocity = Vector2.zero;
    }

    private void ResumeJunctionWalking()
    {
        isRecovering = false;
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
        ClearSlideArc();
        if (slideUp != null) slideUp.enabled = false;
        if (slideDown != null) slideDown.enabled = false;
        if (slideLeft != null) slideLeft.enabled = false;
        if (slideRight != null) slideRight.enabled = false;
    }

    private void ApplySlideArc(float progress)
    {
        if (activeSprite == null)
            return;

        activeSprite.SetExternalBaseOffsetFromInitial(Vector3.up * GetSlideArcHeight(progress));
    }

    private void ClearSlideArc()
    {
        if (slideUp != null) slideUp.ClearExternalBase();
        if (slideDown != null) slideDown.ClearExternalBase();
        if (slideLeft != null) slideLeft.ClearExternalBase();
        if (slideRight != null) slideRight.ClearExternalBase();
    }

    private void CreateSlideShadow()
    {
        if (slideShadow == null)
        {
            slideShadow = new GameObject("EskimoSlideShadow");
            slideShadow.transform.localScale = new Vector3(shadowScale.x, shadowScale.y, 1f);

            SpriteRenderer shadowRenderer = slideShadow.AddComponent<SpriteRenderer>();
            shadowRenderer.sprite = GetSlideShadowSprite();
            shadowRenderer.color = shadowColor;

            AnimatedSpriteRenderer visual = activeSprite != null ? activeSprite : spriteDown;
            if (visual != null && visual.TryGetComponent(out SpriteRenderer visualRenderer))
            {
                shadowRenderer.sortingLayerID = visualRenderer.sortingLayerID;
                shadowRenderer.sortingOrder = visualRenderer.sortingOrder - 1;
            }
        }

        SetSlideShadowVisible(true);
        UpdateSlideShadowPosition();
    }

    private void UpdateSlideShadowPosition()
    {
        if (slideShadow == null)
            return;

        // Match Pink Louie's jump-shadow convention: this is the projected
        // ground position, never the elevated visual position of the jumper.
        // A horizontal slide advances only the shadow's X; a vertical slide
        // advances only its Y, with no arc offset applied to either axis.
        Vector2 groundPosition = rb != null ? rb.position : (Vector2)transform.position;
        if (direction.x != 0f)
            groundPosition.y = slideGroundStart.y;
        else if (direction.y != 0f)
            groundPosition.x = slideGroundStart.x;

        Vector3 position = groundPosition + shadowOffset;
        slideShadow.transform.position = new Vector3(position.x, position.y, 0f);
    }

    private float GetSlideArcHeight(float progress)
    {
        float height = Mathf.Sin(Mathf.Clamp01(progress) * Mathf.PI) * slideHeightTiles * tileSize;
        float ppu = Mathf.Max(1, pixelsPerUnit);
        return Mathf.Round(height * ppu) / ppu;
    }

    private void SetSlideShadowVisible(bool visible)
    {
        if (slideShadow != null)
            slideShadow.SetActive(visible);
    }

    private void DestroySlideShadow()
    {
        if (slideShadow != null)
            Destroy(slideShadow);

        slideShadow = null;
    }

    private static Sprite GetSlideShadowSprite()
    {
        if (slideShadowSprite != null)
            return slideShadowSprite;

        Texture2D texture = new(16, 16, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            name = "EskimoSlideShadow"
        };

        Vector2 center = new(7.5f, 7.5f);
        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                Vector2 point = new((x - center.x) / 7.5f, (y - center.y) / 4.5f);
                texture.SetPixel(x, y, point.sqrMagnitude <= 1f ? Color.white : Color.clear);
            }
        }

        texture.Apply();
        slideShadowSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, 16f, 16f),
            new Vector2(0.5f, 0.5f),
            16f,
            0,
            SpriteMeshType.FullRect);
        slideShadowSprite.name = "EskimoSlideShadowSprite";
        return slideShadowSprite;
    }
}

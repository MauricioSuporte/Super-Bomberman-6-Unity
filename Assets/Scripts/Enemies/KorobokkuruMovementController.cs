using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Korobokkuru walks vertically using the normal junction navigation. A
/// horizontal choice is converted into a two-tile hop: it transforms at the
/// centre of its current tile, then hops and can chain a few more hops before
/// returning to normal walking.
/// </summary>
public sealed class KorobokkuruMovementController : JunctionTurningEnemyMovementController
{
    private const int JumpDistanceInTiles = 2;

    private enum MovementState
    {
        Walking,
        Transforming,
        Jumping,
        JumpIdle,
        LandingTransform,
    }

    [Header("Korobokkuru timing")]
    [SerializeField, Min(0.01f)] private float transformSeconds = 0.2f;
    [SerializeField, Min(0.01f)] private float jumpSeconds = 1.5f;
    [SerializeField, Min(0f)] private float jumpIdleSeconds = 0.25f;
    [SerializeField, Range(0f, 1f)] private float continueJumpChance = 0.65f;

    [Header("Korobokkuru jump")]
    [SerializeField, Min(0f)] private float jumpHeightInTiles = 2f;
    [SerializeField, Range(0.1f, 1f)] private float landingCheckSizeInTiles = 0.8f;

    private AnimatedSpriteRenderer jumpingSprite;
    private AnimatedSpriteRenderer transformSprite;
    private GameObject bigShadow;
    private GameObject smallShadow;
    private SpriteRenderer bigShadowRenderer;
    private SpriteRenderer smallShadowRenderer;
    private CharacterHealth korobokkuruHealth;
    private Tilemap groundTilemap;
    private Tilemap destructibleTilemap;
    private Tilemap indestructibleTilemap;
    private MovementState state;
    private float stateTimer;
    private Vector2 jumpStart;
    private Vector2 jumpTarget;
    private Vector2 pendingJumpDirection;
    private bool jumpInvulnerabilityApplied;

    protected override void Awake()
    {
        CacheVisuals();
        korobokkuruHealth = GetComponent<CharacterHealth>();
        base.Awake();
    }

    protected override void Start()
    {
        ResolveTilemaps();
        state = MovementState.Walking;
        base.Start();

        // Junction navigation has no adjacent free tile in a fully enclosed
        // spawn, so it leaves targetTile on the current cell. Give the
        // Korobokkuru one immediate chance to escape with its two-tile jump.
        if (state == MovementState.Walking && ReachedTile())
        {
            ChooseVerticalWalkingTile();
        }

        SetShadows(jumping: false);
    }

    protected override void FixedUpdate()
    {
        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        if (isDead || isInDamagedLoop || IsStunned())
            return;

        if (GamePauseController.IsPaused)
            return;

        switch (state)
        {
            case MovementState.Walking:
                UpdateWalking();
                break;
            case MovementState.Transforming:
                UpdateTransform();
                break;
            case MovementState.Jumping:
                UpdateJump();
                break;
            case MovementState.JumpIdle:
                UpdateJumpIdle();
                break;
            case MovementState.LandingTransform:
                UpdateLandingTransform();
                break;
        }
    }

    protected override void DecideNextTile()
    {
        if (state != MovementState.Walking)
            return;

        base.DecideNextTile();

        if (!IsHorizontal(direction) || targetTile == rb.position)
            return;

        Vector2 landing = rb.position + direction * tileSize * JumpDistanceInTiles;
        if (IsLandingTileAvailable(landing))
        {
            BeginTransform(direction);
            return;
        }

        ChooseVerticalWalkingTile();
    }

    protected override void OnTriggerEnter2D(Collider2D other)
    {
        if (other != null && other.gameObject.layer == LayerMask.NameToLayer("Explosion") &&
            IsExplosionImmuneDuringJump())
        {
            return;
        }

        base.OnTriggerEnter2D(other);
    }

    protected override void Die()
    {
        SetJumpInvulnerability(false);
        ClearJumpOffsets();
        SetShadows(jumping: false);
        SetVisualEnabled(jumpingSprite, false);
        SetVisualEnabled(transformSprite, false);
        base.Die();
    }

    private void BeginTransform(Vector2 jumpDirection)
    {
        SnapToGrid();
        pendingJumpDirection = jumpDirection;
        targetTile = rb.position;
        isStuck = false;
        state = MovementState.Transforming;
        stateTimer = 0f;
        ShowTransformVisual();
    }

    private void UpdateTransform()
    {
        stateTimer += Time.fixedDeltaTime;
        if (stateTimer < transformSeconds)
            return;

        Vector2 landing = rb.position + pendingJumpDirection * tileSize * JumpDistanceInTiles;
        if (!IsLandingTileAvailable(landing))
        {
            ResumeWalking();
            return;
        }

        BeginJump(landing);
    }

    private void BeginJump(Vector2 landing)
    {
        jumpStart = rb.position;
        jumpTarget = landing;
        direction = (jumpTarget - jumpStart).normalized;
        state = MovementState.Jumping;
        stateTimer = 0f;
        ShowJumpVisual(animate: true);
        SetShadows(jumping: true);
        UpdateJumpInvulnerability();
    }

    private void UpdateJump()
    {
        stateTimer += Time.fixedDeltaTime;
        float progress = Mathf.Clamp01(stateTimer / jumpSeconds);
        float horizontalProgress = EaseOutQuad(progress);
        rb.MovePosition(Vector2.Lerp(jumpStart, jumpTarget, horizontalProgress));
        ApplyJumpOffset(Mathf.Sin(progress * Mathf.PI) * jumpHeightInTiles * tileSize);
        UpdateJumpInvulnerability();

        if (progress < 1f)
            return;

        rb.position = jumpTarget;
        SnapToGrid();
        ClearJumpOffsets();
        SetShadows(jumping: false);
        SetJumpInvulnerability(false);
        state = MovementState.JumpIdle;
        stateTimer = 0f;
        ShowJumpVisual(animate: false);
    }

    private void UpdateJumpIdle()
    {
        stateTimer += Time.fixedDeltaTime;
        if (stateTimer < jumpIdleSeconds)
            return;

        if (Random.value <= continueJumpChance && TryChooseJumpLanding(out Vector2 landing))
        {
            BeginJump(landing);
            return;
        }

        BeginLandingTransform();
    }

    private void BeginLandingTransform()
    {
        state = MovementState.LandingTransform;
        stateTimer = 0f;
        ClearJumpOffsets();
        SetShadows(jumping: false);
        SetJumpInvulnerability(false);
        ShowTransformVisual();
    }

    private void UpdateLandingTransform()
    {
        stateTimer += Time.fixedDeltaTime;
        if (stateTimer < transformSeconds)
            return;

        ResumeWalking();
    }

    private void ResumeWalking()
    {
        state = MovementState.Walking;
        stateTimer = 0f;
        ClearJumpOffsets();
        HideTransformVisual();
        SetShadows(jumping: false);
        SetJumpInvulnerability(false);
        UpdateSpriteDirection(direction);
        ChooseVerticalWalkingTile();
    }

    private void UpdateWalking()
    {
        if (!isStuck)
        {
            base.FixedUpdate();
            return;
        }

        stuckTimer += Time.fixedDeltaTime;
        if (stuckTimer >= recheckStuckEverySeconds)
        {
            stuckTimer = 0f;
            ChooseVerticalWalkingTile();
        }
    }

    private bool TryChooseJumpLanding(out Vector2 landing)
    {
        List<Vector2> validDirections = new(Dirs.Length);
        foreach (Vector2 candidate in Dirs)
        {
            Vector2 candidateLanding = rb.position + candidate * tileSize * JumpDistanceInTiles;
            if (IsLandingTileAvailable(candidateLanding))
                validDirections.Add(candidate);
        }

        if (validDirections.Count == 0)
        {
            landing = rb.position;
            return false;
        }

        Vector2 chosenDirection = validDirections[Random.Range(0, validDirections.Count)];
        landing = rb.position + chosenDirection * tileSize * JumpDistanceInTiles;
        return true;
    }

    private void ChooseVerticalWalkingTile()
    {
        List<Vector2> verticalDirections = new(2);
        bool upBlocked = IsTileBlocked(rb.position + Vector2.up * tileSize);
        bool downBlocked = IsTileBlocked(rb.position + Vector2.down * tileSize);

        if (!upBlocked)
            verticalDirections.Add(Vector2.up);
        if (!downBlocked)
            verticalDirections.Add(Vector2.down);

        if (verticalDirections.Count > 0)
        {
            direction = verticalDirections[Random.Range(0, verticalDirections.Count)];
            targetTile = rb.position + direction * tileSize;
            isStuck = false;
            UpdateSpriteDirection(direction);
            return;
        }

        if (TryChooseJumpLanding(out Vector2 escapeLanding))
        {
            BeginTransform((escapeLanding - rb.position).normalized);
            return;
        }

        // Do not let the base stuck behavior select a one-tile horizontal
        // walk: Korobokkuru only moves sideways by jumping.
        targetTile = rb.position;
        isStuck = true;
        stuckTimer = 0f;
    }

    private bool IsLandingTileAvailable(Vector2 worldPosition)
    {
        ResolveTilemaps();
        if (groundTilemap == null || !groundTilemap.HasTile(groundTilemap.WorldToCell(worldPosition)))
            return false;

        if (HasTileAt(destructibleTilemap, worldPosition) || HasTileAt(indestructibleTilemap, worldPosition))
            return false;

        if (IsTileBlocked(worldPosition))
            return false;

        Vector2 checkSize = Vector2.one * tileSize * landingCheckSizeInTiles;
        foreach (Collider2D hit in Physics2D.OverlapBoxAll(worldPosition, checkSize, 0f))
        {
            if (hit == null || hit.transform.IsChildOf(transform))
                continue;

            if (hit.GetComponentInParent<CoreMechanismsDestructible>() != null ||
                hit.GetComponentInParent<Bomb>() != null ||
                hit.GetComponentInParent<EnemyMovementController>() != null)
            {
                return false;
            }
        }

        return true;
    }

    private void CacheVisuals()
    {
        jumpingSprite = FindChildAnimatedSprite("Jumping");
        transformSprite = FindChildAnimatedSprite("Transform");
        bigShadow = FindChild("BigShadow");
        smallShadow = FindChild("SmallShadow") ?? FindChild("SmalShadow");
        bigShadowRenderer = bigShadow != null ? bigShadow.GetComponent<SpriteRenderer>() : null;
        smallShadowRenderer = smallShadow != null ? smallShadow.GetComponent<SpriteRenderer>() : null;
    }

    private AnimatedSpriteRenderer FindChildAnimatedSprite(string childName)
    {
        Transform child = transform.Find(childName);
        return child != null ? child.GetComponent<AnimatedSpriteRenderer>() : null;
    }

    private GameObject FindChild(string childName)
    {
        Transform child = transform.Find(childName);
        return child != null ? child.gameObject : null;
    }

    private void ShowTransformVisual()
    {
        HideSpecialVisualsExcept(transformSprite);
        if (transformSprite == null)
            return;

        SetVisualEnabled(transformSprite, true);
        transformSprite.idle = false;
        transformSprite.loop = false;
        transformSprite.RestartAnimation();
    }

    private void HideTransformVisual()
    {
        SetVisualEnabled(transformSprite, false);
    }

    private void ShowJumpVisual(bool animate)
    {
        HideSpecialVisualsExcept(jumpingSprite);
        if (jumpingSprite == null)
            return;

        SetVisualEnabled(jumpingSprite, true);
        jumpingSprite.idle = !animate;
        if (animate)
        {
            jumpingSprite.loop = true;
            jumpingSprite.RestartAnimation();
        }
        else
        {
            jumpingSprite.CurrentFrame = 0;
            jumpingSprite.RefreshFrame();
        }
    }

    private void HideSpecialVisualsExcept(AnimatedSpriteRenderer keep)
    {
        if (jumpingSprite != keep)
            SetVisualEnabled(jumpingSprite, false);
        if (transformSprite != keep)
            SetVisualEnabled(transformSprite, false);
        SetVisualEnabled(spriteUp, false);
        SetVisualEnabled(spriteDown, false);
        SetVisualEnabled(spriteLeft, false);
        SetVisualEnabled(spriteRight, false);
    }

    private void SetShadows(bool jumping)
    {
        if (bigShadow != null)
            bigShadow.SetActive(true);
        if (smallShadow != null)
            smallShadow.SetActive(true);

        // The small shadow is deliberately not given the jump arc. It stays
        // on the Rigidbody's ground position while only the Jumping visual is
        // offset upward, matching SnowMan's ground-shadow behavior.
        if (bigShadowRenderer != null)
            bigShadowRenderer.enabled = !jumping;
        if (smallShadowRenderer != null)
            smallShadowRenderer.enabled = jumping;
    }

    private void ApplyJumpOffset(float height)
    {
        if (jumpingSprite != null)
            jumpingSprite.SetExternalBaseOffsetFromInitial(Vector3.up * height);
    }

    private void ClearJumpOffsets()
    {
        if (jumpingSprite != null)
            jumpingSprite.ClearExternalBase();
    }

    private void UpdateJumpInvulnerability()
    {
        SetJumpInvulnerability(IsExplosionImmuneDuringJump());
    }

    private bool IsExplosionImmuneDuringJump()
    {
        return state == MovementState.Jumping && jumpingSprite != null && jumpingSprite.enabled &&
            jumpingSprite.animationSprite != null && jumpingSprite.animationSprite.Length > 0 &&
            jumpingSprite.animationSprite[jumpingSprite.CurrentFrame] != jumpingSprite.idleSprite;
    }

    private void SetJumpInvulnerability(bool value)
    {
        if (jumpInvulnerabilityApplied == value)
            return;

        jumpInvulnerabilityApplied = value;
        if (korobokkuruHealth != null)
            korobokkuruHealth.SetExternalInvulnerability(value);

    }

    private void ResolveTilemaps()
    {
        GameManager gameManager = GameManager.Instance != null
            ? GameManager.Instance
            : FindAnyObjectByType<GameManager>();

        if (gameManager != null)
        {
            groundTilemap ??= gameManager.groundTilemap;
            destructibleTilemap ??= gameManager.destructibleTilemap;
            indestructibleTilemap ??= gameManager.indestructibleTilemap;
        }
    }

    private bool IsStunned()
    {
        return TryGetComponent(out StunReceiver stun) && stun != null && stun.IsStunned;
    }

    private static bool HasTileAt(Tilemap tilemap, Vector2 worldPosition)
    {
        return tilemap != null && tilemap.HasTile(tilemap.WorldToCell(worldPosition));
    }

    private static bool IsHorizontal(Vector2 value)
    {
        return value == Vector2.left || value == Vector2.right;
    }

    private static float EaseOutQuad(float time)
    {
        float inverseTime = 1f - Mathf.Clamp01(time);
        return 1f - inverseTime * inverseTime;
    }

    private static void SetVisualEnabled(AnimatedSpriteRenderer sprite, bool enabled)
    {
        if (sprite == null)
            return;

        sprite.enabled = enabled;
        if (sprite.TryGetComponent(out SpriteRenderer spriteRenderer))
            spriteRenderer.enabled = enabled;
    }

}

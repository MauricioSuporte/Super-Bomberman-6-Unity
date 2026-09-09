using UnityEngine;

/// <summary>
/// Moves tile by tile until it pauses on a tile, prepares in its current
/// direction, and then enters an invulnerable scrolling movement burst.
/// </summary>
public sealed class CrawlerMovementController : JunctionTurningEnemyMovementController
{
    private enum MovementState
    {
        Walking,
        Preparing,
        Scrolling
    }

    [Header("Crawler Cycle")]
    [SerializeField, Min(0.01f)] private float walkMinDurationSeconds = 3f;
    [SerializeField, Min(0.01f)] private float walkMaxDurationSeconds = 6f;
    [SerializeField, Min(0.01f)] private float preparingDurationSeconds = 0.5f;
    [SerializeField, Min(0.01f)] private float scrollingMinDurationSeconds = 2f;
    [SerializeField, Min(0.01f)] private float scrollingMaxDurationSeconds = 4f;

    [Header("Crawler Sprites")]
    [SerializeField] private AnimatedSpriteRenderer preparingUp;
    [SerializeField] private AnimatedSpriteRenderer preparingDown;
    [SerializeField] private AnimatedSpriteRenderer preparingLeft;
    [SerializeField] private AnimatedSpriteRenderer scrollingUp;
    [SerializeField] private AnimatedSpriteRenderer scrollingDown;
    [SerializeField] private AnimatedSpriteRenderer scrollingLeft;

    private CharacterHealth crawlerHealth;
    private MovementState state;
    private float stateTimer;
    private bool wasStunned;

    protected override void Awake()
    {
        ResolveSprites();
        base.Awake();
        crawlerHealth = GetComponent<CharacterHealth>();
        DisableCrawlerSprites();
    }

    protected override void Start()
    {
        base.Start();
        state = MovementState.Walking;
        ScheduleWalking();
        SetVisual(direction);
    }

    protected override void FixedUpdate()
    {
        if (isDead)
            return;

        bool stunned = TryGetComponent(out StunReceiver stun) && stun != null && stun.IsStunned;
        if (stunned)
        {
            wasStunned = true;
            CancelScrollingInvulnerability();
            if (rb != null)
                rb.linearVelocity = Vector2.zero;
            return;
        }

        if (wasStunned)
        {
            wasStunned = false;
            state = MovementState.Walking;
            SnapToGrid();
            ScheduleWalking();
            SetVisual(direction);
            DecideNextTile();
            return;
        }

        if (isInDamagedLoop)
        {
            if (rb != null)
                rb.linearVelocity = Vector2.zero;
            return;
        }

        switch (state)
        {
            case MovementState.Preparing:
                UpdatePreparing();
                return;
            case MovementState.Scrolling:
                UpdateScrolling();
                return;
            default:
                UpdateWalking();
                return;
        }
    }

    protected override void UpdateSpriteDirection(Vector2 dir)
    {
        if (isDead || isInDamagedLoop)
            return;

        SetVisual(dir);
    }

    protected override void Die()
    {
        CancelScrollingInvulnerability();
        DisableCrawlerSprites();
        base.Die();
    }

    protected override void OnDestroy()
    {
        CancelScrollingInvulnerability();
        base.OnDestroy();
    }

    private void UpdateWalking()
    {
        stateTimer -= Time.fixedDeltaTime;

        if (isStuck)
        {
            HandleStuck();
            return;
        }

        if (HasBombAt(targetTile))
            HandleBombAhead();

        MoveTowardsTile();

        if (!ReachedTile())
            return;

        SnapToGrid();
        if (stateTimer <= 0f)
        {
            StartPreparing();
            return;
        }

        DecideNextTile();
    }

    private void UpdatePreparing()
    {
        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        stateTimer -= Time.fixedDeltaTime;
        if (stateTimer <= 0f)
            StartScrolling();
    }

    private void UpdateScrolling()
    {
        stateTimer -= Time.fixedDeltaTime;

        if (HasBombAt(targetTile))
            HandleBombAhead();

        rb.MovePosition(Vector2.MoveTowards(rb.position, targetTile, speed * Time.fixedDeltaTime));

        if (!ReachedTile())
            return;

        SnapToGrid();
        if (stateTimer <= 0f)
        {
            StopScrolling();
            return;
        }

        DecideNextTile();
        SetVisual(direction);
    }

    private void StartPreparing()
    {
        state = MovementState.Preparing;
        stateTimer = preparingDurationSeconds;
        targetTile = rb.position;
        SetVisual(direction);
    }

    private void StartScrolling()
    {
        state = MovementState.Scrolling;
        stateTimer = RandomDuration(scrollingMinDurationSeconds, scrollingMaxDurationSeconds);
        crawlerHealth?.SetExternalInvulnerability(true);
        SetVisual(direction);

        if (IsTileBlocked(rb.position + direction * tileSize))
            DecideNextTile();
        else
            targetTile = rb.position + direction * tileSize;
    }

    private void StopScrolling()
    {
        CancelScrollingInvulnerability();
        state = MovementState.Walking;
        ScheduleWalking();
        SetVisual(direction);
        DecideNextTile();
    }

    private void ScheduleWalking()
    {
        stateTimer = RandomDuration(walkMinDurationSeconds, walkMaxDurationSeconds);
    }

    private static float RandomDuration(float min, float max)
    {
        float lower = Mathf.Max(0.01f, Mathf.Min(min, max));
        float upper = Mathf.Max(lower, Mathf.Max(min, max));
        return Random.Range(lower, upper);
    }

    private void CancelScrollingInvulnerability()
    {
        if (crawlerHealth != null)
            crawlerHealth.SetExternalInvulnerability(false);
    }

    private void ResolveSprites()
    {
        preparingUp ??= FindSprite("PreparingUp");
        preparingDown ??= FindSprite("PreparingDown");
        preparingLeft ??= FindSprite("PreparingLeft");
        scrollingUp ??= FindSprite("ScrollingUp");
        scrollingDown ??= FindSprite("ScrollingDown");
        scrollingLeft ??= FindSprite("ScrollingLeft");
    }

    private AnimatedSpriteRenderer FindSprite(string childName)
    {
        Transform child = transform.Find(childName);
        return child != null ? child.GetComponent<AnimatedSpriteRenderer>() : null;
    }

    private void SetVisual(Vector2 visualDirection)
    {
        AnimatedSpriteRenderer next = PickSprite(visualDirection);
        if (next == null)
            return;

        if (activeSprite != next || activeSprite == null || !activeSprite.enabled)
        {
            DisableCrawlerSprites();
            if (spriteUp != null) spriteUp.enabled = false;
            if (spriteDown != null) spriteDown.enabled = false;
            if (spriteLeft != null) spriteLeft.enabled = false;
            if (spriteRight != null) spriteRight.enabled = false;

            activeSprite = next;
            activeSprite.enabled = true;
            activeSprite.idle = false;
        }

        if (activeSprite.TryGetComponent(out SpriteRenderer renderer))
            renderer.flipX = visualDirection == Vector2.right;

    }

    private AnimatedSpriteRenderer PickSprite(Vector2 visualDirection)
    {
        if (state == MovementState.Preparing)
            return PickDirectional(preparingUp, preparingDown, preparingLeft, visualDirection);

        if (state == MovementState.Scrolling)
            return PickDirectional(scrollingUp, scrollingDown, scrollingLeft, visualDirection);

        return PickDirectional(spriteUp, spriteDown, spriteLeft, visualDirection);
    }

    private static AnimatedSpriteRenderer PickDirectional(
        AnimatedSpriteRenderer up,
        AnimatedSpriteRenderer down,
        AnimatedSpriteRenderer left,
        Vector2 visualDirection)
    {
        if (visualDirection == Vector2.up)
            return up;
        if (visualDirection == Vector2.down)
            return down;
        return left;
    }

    private void DisableCrawlerSprites()
    {
        if (preparingUp != null) preparingUp.enabled = false;
        if (preparingDown != null) preparingDown.enabled = false;
        if (preparingLeft != null) preparingLeft.enabled = false;
        if (scrollingUp != null) scrollingUp.enabled = false;
        if (scrollingDown != null) scrollingDown.enabled = false;
        if (scrollingLeft != null) scrollingLeft.enabled = false;
    }
}

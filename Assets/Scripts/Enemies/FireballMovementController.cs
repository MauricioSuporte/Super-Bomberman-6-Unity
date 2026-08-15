using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(CharacterHealth))]
[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(StunReceiver))]
public sealed class FireballMovementController : JunctionTurningEnemyMovementController
{
    [Header("Bomb pursuit")]
    [SerializeField, Min(0.1f)] private float visionDistance = 12f;
    [SerializeField, Min(1f)] private float pursuitSpeedMultiplier = 2f;
    [SerializeField, Min(0f)] private float prepareDuration = 0.25f;
    [SerializeField, Min(0.001f)] private float alignedToleranceTiles = 0.15f;

    [Header("Ability sprites")]
    [SerializeField] private AnimatedSpriteRenderer abilityUp;
    [SerializeField] private AnimatedSpriteRenderer abilityDown;
    [SerializeField] private AnimatedSpriteRenderer abilityLeft;
    [SerializeField] private AnimatedSpriteRenderer abilityIndicator;

    private float baseSpeed;
    private float prepareRemaining;
    private bool preparingPursuit;
    private bool pursuingBomb;
    private Vector2 pursuitDirection = Vector2.down;

    protected override void Awake()
    {
        base.Awake();
        baseSpeed = speed;
        DisableAbilitySprites();
    }

    private void OnDisable()
    {
        DisableAbilitySprites();
    }

    protected override void FixedUpdate()
    {
        if (isDead)
            return;

        if (TryGetComponent(out StunReceiver stun) && stun != null && stun.IsStunned)
        {
            CancelBombPursuit();
            base.FixedUpdate();
            return;
        }

        if (isInDamagedLoop)
        {
            CancelBombPursuit();
            base.FixedUpdate();
            return;
        }

        if (preparingPursuit)
        {
            if (!TryGetBombDirection(out Vector2 currentDirection))
            {
                CancelBombPursuit();
                base.FixedUpdate();
                return;
            }

            pursuitDirection = currentDirection;
            direction = pursuitDirection;
            ApplyPursuitSprite(idle: true);

            rb.linearVelocity = Vector2.zero;
            prepareRemaining -= Time.fixedDeltaTime;
            if (prepareRemaining <= 0f)
            {
                preparingPursuit = false;
                pursuingBomb = true;
                speed = baseSpeed * pursuitSpeedMultiplier;
                ApplyPursuitSprite(idle: false);
            }

            return;
        }

        if (pursuingBomb)
        {
            if (!TryGetBombDirection(out Vector2 currentDirection))
            {
                CancelBombPursuit();
                DecideNextTile();
                return;
            }

            pursuitDirection = currentDirection;
            direction = pursuitDirection;
            speed = baseSpeed * pursuitSpeedMultiplier;
            ApplyPursuitSprite(idle: false);
            MoveTowardBomb();
            return;
        }

        if (TryGetBombDirection(out Vector2 detectedDirection))
        {
            StartBombPursuit(detectedDirection);
            return;
        }

        speed = baseSpeed;
        base.FixedUpdate();
    }

    protected override void Die()
    {
        CancelBombPursuit();
        DisableAbilitySprites();
        base.Die();
    }

    protected override void OnTriggerEnter2D(Collider2D other)
    {
        if (other != null && other.gameObject.layer == LayerMask.NameToLayer("Bomb") &&
            (preparingPursuit || pursuingBomb))
            return;

        base.OnTriggerEnter2D(other);
    }

    private void StartBombPursuit(Vector2 newDirection)
    {
        isStuck = false;
        SnapToGrid();
        pursuitDirection = newDirection;
        direction = newDirection;
        targetTile = rb.position;
        preparingPursuit = true;
        pursuingBomb = false;
        prepareRemaining = prepareDuration;
        speed = baseSpeed;
        ApplyPursuitSprite(idle: true);

        if (prepareDuration <= 0f)
        {
            preparingPursuit = false;
            pursuingBomb = true;
            speed = baseSpeed * pursuitSpeedMultiplier;
            ApplyPursuitSprite(idle: false);
        }
    }

    private void CancelBombPursuit()
    {
        preparingPursuit = false;
        pursuingBomb = false;
        prepareRemaining = 0f;
        speed = baseSpeed;
        DisableAbilitySprites();
        UpdateSpriteDirection(direction);
    }

    private void MoveTowardBomb()
    {
        if (HasBombAt(rb.position))
        {
            rb.linearVelocity = Vector2.zero;
            targetTile = rb.position;
            return;
        }

        if (ReachedTile())
        {
            SnapToGrid();
            targetTile = rb.position + pursuitDirection * tileSize;
        }

        if (IsBlockedForPursuit(targetTile))
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        rb.MovePosition(Vector2.MoveTowards(rb.position, targetTile, speed * Time.fixedDeltaTime));

        if (ReachedTile())
            SnapToGrid();
    }

    private bool IsBlockedForPursuit(Vector2 tileCenter)
    {
        int bombLayer = LayerMask.NameToLayer("Bomb");
        int mask = obstacleMask.value;
        if (bombLayer >= 0)
            mask &= ~(1 << bombLayer);

        Collider2D[] hits = Physics2D.OverlapBoxAll(tileCenter, Vector2.one * (tileSize * 0.8f), 0f, mask);
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i] != null && hits[i].gameObject != gameObject)
                return true;
        }

        return false;
    }

    private bool TryGetBombDirection(out Vector2 directionToBomb)
    {
        directionToBomb = pursuitDirection;
        Vector2[] directions = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
        int maxSteps = Mathf.Max(1, Mathf.FloorToInt(visionDistance / tileSize));
        Vector2 origin = rb.position;
        float tolerance = Mathf.Max(0.001f, alignedToleranceTiles * tileSize);

        if (HasBombAt(origin))
            return true;

        for (int dirIndex = 0; dirIndex < directions.Length; dirIndex++)
        {
            Vector2 scanDirection = directions[dirIndex];
            bool vertical = scanDirection == Vector2.up || scanDirection == Vector2.down;

            for (int step = 1; step <= maxSteps; step++)
            {
                Vector2 tileCenter = origin + scanDirection * tileSize * step;
                Collider2D[] hits = Physics2D.OverlapBoxAll(
                    tileCenter,
                    Vector2.one * (tileSize * 0.7f),
                    0f,
                    bombLayerMask);

                for (int hitIndex = 0; hitIndex < hits.Length; hitIndex++)
                {
                    Collider2D hit = hits[hitIndex];
                    if (hit == null)
                        continue;

                    Vector2 bombPosition = hit.attachedRigidbody != null
                        ? hit.attachedRigidbody.position
                        : (Vector2)hit.transform.position;
                    bool aligned = vertical
                        ? Mathf.Abs(bombPosition.x - origin.x) <= tolerance
                        : Mathf.Abs(bombPosition.y - origin.y) <= tolerance;

                    if (!aligned)
                        continue;

                    directionToBomb = scanDirection;
                    return true;
                }
            }
        }

        directionToBomb = Vector2.zero;
        return false;
    }

    private void ApplyPursuitSprite(bool idle)
    {
        AnimatedSpriteRenderer selected = GetAbilitySprite(pursuitDirection);
        if (selected == null)
            return;

        DisableWalkingSprites();
        DisableAbilitySprites(selected);
        activeSprite = selected;
        selected.enabled = true;
        selected.idle = idle;
        selected.loop = true;

        if (selected.TryGetComponent(out SpriteRenderer renderer))
            renderer.flipX = pursuitDirection == Vector2.right;

        if (abilityIndicator != null)
        {
            abilityIndicator.enabled = idle;
            abilityIndicator.idle = true;
        }
    }

    private AnimatedSpriteRenderer GetAbilitySprite(Vector2 dir)
    {
        if (dir == Vector2.up)
            return abilityUp;
        if (dir == Vector2.down)
            return abilityDown;
        return abilityLeft;
    }

    private void DisableWalkingSprites()
    {
        if (spriteUp != null) spriteUp.enabled = false;
        if (spriteDown != null) spriteDown.enabled = false;
        if (spriteLeft != null) spriteLeft.enabled = false;
        if (spriteRight != null) spriteRight.enabled = false;
    }

    private void DisableAbilitySprites(AnimatedSpriteRenderer except = null)
    {
        if (abilityUp != null && abilityUp != except) abilityUp.enabled = false;
        if (abilityDown != null && abilityDown != except) abilityDown.enabled = false;
        if (abilityLeft != null && abilityLeft != except) abilityLeft.enabled = false;
        if (abilityIndicator != null) abilityIndicator.enabled = false;
    }
}

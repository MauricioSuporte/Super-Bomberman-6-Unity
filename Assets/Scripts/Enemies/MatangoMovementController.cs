using UnityEngine;

/// <summary>
/// Junction-turning Matango that pauses to fire at a player it can see in a
/// cardinal lane.
/// </summary>
public sealed class MatangoMovementController : JunctionTurningEnemyMovementController
{
    [Header("Player Detection")]
    [SerializeField, Min(0.1f)] private float visionDistance = 10f;
    [SerializeField] private LayerMask playerLayerMask;

    [Header("Projectile Attack")]
    [SerializeField, Min(0.01f)] private float attackDurationSeconds = 0.5f;
    [SerializeField, Min(0.01f)] private float attackCooldownSeconds = 10f;
    [SerializeField, Min(0.01f)] private float projectileSpeed = 5f;
    [SerializeField, Min(0.01f)] private float projectileLifetimeSeconds = 5f;
    [SerializeField, Range(0.05f, 0.5f)] private float projectileRadius = 0.15f;

    private AnimatedSpriteRenderer attackingUp;
    private AnimatedSpriteRenderer attackingDown;
    private AnimatedSpriteRenderer attackingLeft;
    private AnimatedSpriteRenderer projectileAnimation;
    private StunReceiver stunReceiver;
    private bool isAttacking;
    private float attackEndsAt;
    private float nextAttackAt;
    private Vector2 attackDirection;

    protected override void Awake()
    {
        base.Awake();

        attackingUp = FindAnimation("AtackingUp");
        attackingDown = FindAnimation("AtackingDown");
        attackingLeft = FindAnimation("AtackingLeft");

        projectileAnimation = FindAnimation("Projectile");

        SetAttackVisualsVisible(false);
    }

    protected override void Start()
    {
        base.Start();
        stunReceiver = GetComponent<StunReceiver>();

        if (playerLayerMask.value == 0)
            playerLayerMask = LayerMask.GetMask("Player");
    }

    protected override void FixedUpdate()
    {
        if (isDead)
        {
            StopAttackVisuals();
            return;
        }

        if (isInDamagedLoop || (stunReceiver != null && stunReceiver.IsStunned))
        {
            CancelAttack();
            base.FixedUpdate();
            return;
        }

        if (isAttacking)
        {
            if (rb != null)
                rb.linearVelocity = Vector2.zero;

            if (Time.time >= attackEndsAt)
                FireProjectile();

            return;
        }

        if (Time.time >= nextAttackAt && TrySeePlayer(out Vector2 seenDirection))
        {
            StartAttack(seenDirection);
            return;
        }

        base.FixedUpdate();
    }

    protected override void Die()
    {
        StopAttackVisuals();
        base.Die();
    }

    protected override void OnDestroy()
    {
        StopAttackVisuals();
        base.OnDestroy();
    }

    private void StartAttack(Vector2 seenDirection)
    {
        attackDirection = ToCardinal(seenDirection);
        direction = attackDirection;
        isAttacking = true;
        attackEndsAt = Time.time + attackDurationSeconds;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            targetTile = rb.position;
        }

        SetMovementVisualsVisible(false);
        SetAttackVisualsVisible(false);
        AnimatedSpriteRenderer attackVisual = GetAttackVisual(attackDirection);
        if (attackVisual == null)
            return;

        SetVisualEnabled(attackVisual, true);
        attackVisual.loop = true;
        attackVisual.idle = false;
        attackVisual.RestartAnimation();
        if (attackVisual.TryGetComponent(out SpriteRenderer renderer))
            renderer.flipX = attackDirection == Vector2.right;

        activeSprite = attackVisual;

    }

    private void FireProjectile()
    {
        Vector2 spawnPosition = rb != null
            ? rb.position + attackDirection * (tileSize * 0.45f)
            : (Vector2)transform.position;

        MatangoProjectile.Create(
            spawnPosition,
            attackDirection,
            projectileSpeed,
            projectileLifetimeSeconds,
            projectileRadius,
            projectileAnimation);

        isAttacking = false;
        nextAttackAt = Time.time + attackCooldownSeconds;
        StopAttackVisuals();
        UpdateSpriteDirection(direction);
        SetVisualEnabled(activeSprite, true);
        DecideNextTile();
    }

    private void CancelAttack()
    {
        if (!isAttacking)
            return;

        isAttacking = false;
        StopAttackVisuals();
        if (!isDead && !isInDamagedLoop)
        {
            UpdateSpriteDirection(direction);
            SetVisualEnabled(activeSprite, true);
        }
    }

    private bool TrySeePlayer(out Vector2 seenDirection)
    {
        seenDirection = Vector2.zero;
        if (rb == null || playerLayerMask.value == 0)
            return false;

        int collisionMask = obstacleMask | playerLayerMask;
        for (int i = 0; i < Dirs.Length; i++)
        {
            Vector2 scanDirection = Dirs[i];
            Vector2 origin = rb.position + scanDirection * (tileSize * 0.5f);
            RaycastHit2D hit = Physics2D.Raycast(origin, scanDirection, visionDistance, collisionMask);
            if (hit.collider == null)
                continue;

            if (((1 << hit.collider.gameObject.layer) & playerLayerMask.value) == 0)
                continue;

            seenDirection = scanDirection;
            return true;
        }

        return false;
    }

    private AnimatedSpriteRenderer FindAnimation(string childName)
    {
        Transform child = transform.Find(childName);
        return child != null ? child.GetComponent<AnimatedSpriteRenderer>() : null;
    }

    private AnimatedSpriteRenderer GetAttackVisual(Vector2 facing)
    {
        if (facing == Vector2.up) return attackingUp;
        if (facing == Vector2.down) return attackingDown;
        if (facing == Vector2.left) return attackingLeft;
        return attackingLeft;
    }

    private void StopAttackVisuals()
    {
        SetAttackVisualsVisible(false);
    }

    private void SetAttackVisualsVisible(bool visible)
    {
        SetVisualEnabled(attackingUp, visible);
        SetVisualEnabled(attackingDown, visible);
        SetVisualEnabled(attackingLeft, visible);
    }

    private void SetMovementVisualsVisible(bool visible)
    {
        SetVisualEnabled(spriteUp, visible);
        SetVisualEnabled(spriteDown, visible);
        SetVisualEnabled(spriteLeft, visible);
        SetVisualEnabled(spriteRight, visible);
    }

    private static void SetVisualEnabled(AnimatedSpriteRenderer animation, bool enabled)
    {
        if (animation == null)
            return;

        animation.enabled = enabled;
        if (animation.TryGetComponent(out SpriteRenderer renderer))
            renderer.enabled = enabled;
    }

    private static Vector2 ToCardinal(Vector2 value)
    {
        if (Mathf.Abs(value.x) >= Mathf.Abs(value.y))
            return value.x >= 0f ? Vector2.right : Vector2.left;

        return value.y >= 0f ? Vector2.up : Vector2.down;
    }
}

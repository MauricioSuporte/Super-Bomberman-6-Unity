using System.Collections;
using UnityEngine;

/// <summary>
/// Walks through junctions and periodically strikes the tile in front of its
/// closest player. Attack visuals are authored per direction and are never
/// left enabled after an attack or death.
/// </summary>
public sealed class IsoginchakkuMovementController : JunctionTurningEnemyMovementController
{
    [Header("Attack")]
    [SerializeField, Min(0.01f)] private float attackCooldown = 8f;
    [SerializeField, Min(0.01f)] private float attackDuration = 1f;
    [SerializeField] private AnimatedSpriteRenderer attackDown;
    [SerializeField] private AnimatedSpriteRenderer attackUp;
    [SerializeField] private AnimatedSpriteRenderer attackRight;
    [SerializeField] private LayerMask playerLayerMask;
    [SerializeField, Range(0.1f, 1f)] private float attackTileHitboxPercent = 0.75f;

    private Coroutine attackRoutine;
    private AnimatedSpriteRenderer movementSprite;
    private bool attacking;
    private float attackElapsed;

    protected override void Start()
    {
        base.Start();
        movementSprite = activeSprite;
        DisableAttackVisuals();
    }

    protected override void FixedUpdate()
    {
        if (attacking)
        {
            if (rb != null)
                rb.linearVelocity = Vector2.zero;

            return;
        }

        base.FixedUpdate();
    }

    private void LateUpdate()
    {
        if (isDead || isInDamagedLoop || attacking)
            return;

        attackElapsed += Time.deltaTime;
        if (attackElapsed < attackCooldown)
            return;

        if (TryGetNearestPlayerPosition(out Vector2 playerPosition))
            attackRoutine = StartCoroutine(AttackRoutine(GetCardinalDirection(playerPosition)));
    }

    protected override void Die()
    {
        StopAttack();
        base.Die();
    }

    protected override void OnDestroy()
    {
        StopAttack();
        base.OnDestroy();
    }

    private IEnumerator AttackRoutine(Vector2 attackDirection)
    {
        attacking = true;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            SnapToGrid();
            targetTile = rb.position;
        }

        SetVisualEnabled(movementSprite, false);
        DisableAttackVisuals();

        AnimatedSpriteRenderer attackVisual = GetAttackVisual(attackDirection);
        SetVisualEnabled(attackVisual, true);
        SetAttackHorizontalFlip(attackDirection);
        activeSprite = attackVisual;

        if (attackVisual != null)
        {
            attackVisual.loop = false;
            attackVisual.idle = false;
            attackVisual.RestartAnimation();
        }

        float frameDuration = attackDuration / 9f;
        for (int sequenceFrame = 0; sequenceFrame < 9 && !isDead; sequenceFrame++)
        {
            // AnimatedSpriteRenderer can restore its authored local transform
            // when enabling/disabling. Keep the mirrored horizontal offset for
            // every frame of the left-facing attack.
            SetAttackHorizontalFlip(attackDirection);

            // The authored sequence is 1-2-3-4-3-4-3-2-1. Frames 3 and 4
            // occupy sequence positions 2 through 6 and can hit the front tile.
            if (sequenceFrame >= 2 && sequenceFrame <= 6)
                DamagePlayersInFront(attackDirection);

            yield return new WaitForSeconds(frameDuration);
        }

        if (!isDead)
            FinishAttack();
    }

    private bool TryGetNearestPlayerPosition(out Vector2 playerPosition)
    {
        playerPosition = default;
        if (rb == null || playerLayerMask.value == 0)
            return false;

        float closestDistance = float.MaxValue;
        foreach (MovementController candidate in FindObjectsByType<MovementController>(FindObjectsInactive.Exclude))
        {
            if (candidate == null || !candidate.isActiveAndEnabled || candidate.isDead ||
                !candidate.CompareTag("Player") ||
                (playerLayerMask.value & (1 << candidate.gameObject.layer)) == 0)
                continue;

            float distance = ((Vector2)candidate.transform.position - rb.position).sqrMagnitude;
            if (distance >= closestDistance)
                continue;

            closestDistance = distance;
            playerPosition = candidate.transform.position;
        }

        return closestDistance < float.MaxValue;
    }

    private Vector2 GetCardinalDirection(Vector2 playerPosition)
    {
        Vector2 delta = playerPosition - rb.position;
        if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
            return delta.x >= 0f ? Vector2.right : Vector2.left;

        return delta.y >= 0f ? Vector2.up : Vector2.down;
    }

    private AnimatedSpriteRenderer GetAttackVisual(Vector2 attackDirection)
    {
        if (attackDirection == Vector2.up)
            return attackUp;

        if (attackDirection == Vector2.down)
            return attackDown;

        return attackRight;
    }

    private void SetAttackHorizontalFlip(Vector2 attackDirection)
    {
        if (attackRight == null)
            return;

        bool attackingLeft = attackDirection == Vector2.left;
        if (attackRight.TryGetComponent(out SpriteRenderer rightRenderer))
            rightRenderer.flipX = attackingLeft;

        Vector3 localPosition = attackRight.transform.localPosition;
        float authoredOffset = Mathf.Abs(localPosition.x);
        attackRight.SetRuntimeBaseLocalX(attackingLeft ? -authoredOffset : authoredOffset);
    }

    private void DamagePlayersInFront(Vector2 attackDirection)
    {
        if (rb == null || playerLayerMask.value == 0)
            return;

        float hitboxSize = tileSize * attackTileHitboxPercent;
        Collider2D[] hits = Physics2D.OverlapBoxAll(
            rb.position + attackDirection * tileSize,
            new Vector2(hitboxSize, hitboxSize),
            0f,
            playerLayerMask);

        foreach (Collider2D hit in hits)
        {
            CharacterHealth playerHealth = hit.GetComponentInParent<CharacterHealth>();
            if (playerHealth != null)
                playerHealth.TakeDamage(1);
        }
    }

    private void FinishAttack()
    {
        DisableAttackVisuals();
        ClearAttackHorizontalOffset();
        activeSprite = movementSprite != null ? movementSprite : spriteDown;
        SetVisualEnabled(activeSprite, true);
        if (activeSprite != null)
        {
            activeSprite.RestartAnimation();
            activeSprite.idle = false;
        }

        attacking = false;
        attackRoutine = null;
        attackElapsed = 0f;
        DecideNextTile();
    }

    private void StopAttack()
    {
        if (attackRoutine != null)
            StopCoroutine(attackRoutine);

        attackRoutine = null;
        attacking = false;
        DisableAttackVisuals();
        ClearAttackHorizontalOffset();
    }

    private void DisableAttackVisuals()
    {
        SetVisualEnabled(attackDown, false);
        SetVisualEnabled(attackUp, false);
        SetVisualEnabled(attackRight, false);
    }

    private void ClearAttackHorizontalOffset()
    {
        if (attackRight != null)
            attackRight.ClearRuntimeBaseLocalX();
    }

    private static void SetVisualEnabled(AnimatedSpriteRenderer animation, bool enabled)
    {
        if (animation == null)
            return;

        animation.enabled = enabled;
        if (animation.TryGetComponent(out SpriteRenderer renderer))
            renderer.enabled = enabled;
    }
}

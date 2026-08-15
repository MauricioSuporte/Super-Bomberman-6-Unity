using System.Collections;
using UnityEngine;

public sealed class FuramaMovementController : JunctionTurningEnemyMovementController
{
    [Header("Furama Fire Attack")]
    [SerializeField, Min(0.01f)] float attackMinCooldown = 7f;
    [SerializeField, Min(0.01f)] float attackMaxCooldown = 10f;
    [SerializeField, Min(0.01f)] float prepareDuration = 1f;
    [SerializeField, Min(0.01f)] float attackDuration = 3f;
    [SerializeField, Min(1)] int flameLengthTiles = 3;
    [SerializeField, Min(0.01f)] float flameBurstDuration = 0.5f;
    [SerializeField, Min(0.01f)] float flameBurstInterval = 0.1f;

    [Header("Furama References")]
    [SerializeField] AnimatedSpriteRenderer attackSprite;
    [SerializeField] FuramaFlameVisual flameVisualPrefab;
    [SerializeField] LayerMask playerLayerMask;

    Coroutine attackLoop;
    bool attacking;
    bool started;

    void OnEnable() => TryStartAttackLoop();

    void OnDisable()
    {
        StopAttackLoop();
    }

    protected override void Awake()
    {
        base.Awake();
        if (attackSprite != null)
            attackSprite.enabled = false;
    }

    protected override void Start()
    {
        base.Start();
        if (playerLayerMask.value == 0)
            playerLayerMask = LayerMask.GetMask("Player");

        started = true;
        TryStartAttackLoop();
    }

    protected override void FixedUpdate()
    {
        if (isDead || attacking)
        {
            if (attacking && rb != null)
                rb.linearVelocity = Vector2.zero;
            return;
        }

        base.FixedUpdate();
    }

    protected override void Die()
    {
        StopAttackLoop();
        attacking = false;
        if (attackSprite != null)
            attackSprite.enabled = false;
        base.Die();
    }

    void TryStartAttackLoop()
    {
        if (!started || !isActiveAndEnabled || isDead || attackLoop != null)
            return;

        attackLoop = StartCoroutine(AttackLoop());
    }

    void StopAttackLoop()
    {
        if (attackLoop == null)
            return;

        StopCoroutine(attackLoop);
        attackLoop = null;
    }

    IEnumerator AttackLoop()
    {
        while (isActiveAndEnabled && !isDead)
        {
            yield return new WaitForSeconds(Random.Range(attackMinCooldown, attackMaxCooldown));

            if (isDead || !isActiveAndEnabled || attacking || isInDamagedLoop || IsStunned())
                continue;

            yield return ExecuteAttack();
        }
    }

    IEnumerator ExecuteAttack()
    {
        attacking = true;
        SnapToGrid();
        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        Vector2 fireDirection = GetPreferredDirection();
        ShowDirectionalIdle(fireDirection);

        yield return WaitGameplaySeconds(prepareDuration);

        if (isDead || !isActiveAndEnabled)
            goto END;

        float elapsed = 0f;
        bool launchedFlame = false;
        while (elapsed < attackDuration && isActiveAndEnabled && !isDead)
        {
            if (!IsStunned() && !isInDamagedLoop)
            {
                LaunchFlameBurst(fireDirection);
                launchedFlame = true;
            }

            float wait = Mathf.Min(flameBurstInterval, attackDuration - elapsed);
            yield return WaitGameplaySeconds(wait);
            elapsed += wait;
        }

        if (launchedFlame && isActiveAndEnabled && !isDead)
            yield return WaitGameplaySeconds(flameBurstDuration);

    END:
        attacking = false;
        if (!isDead)
        {
            UpdateSpriteDirection(direction);
            DecideNextTile();
        }
    }

    void LaunchFlameBurst(Vector2 fireDirection)
    {
        Vector2 origin = rb != null ? rb.position : (Vector2)transform.position;
        origin.x = Mathf.Round(origin.x / tileSize) * tileSize;
        origin.y = Mathf.Round(origin.y / tileSize) * tileSize;

        if (flameVisualPrefab == null)
            return;

        FuramaFlameVisual flame = Instantiate(flameVisualPrefab);
        flame.Play(
            origin,
            fireDirection,
            startTileDistance: 1f,
            endTileDistance: Mathf.Max(1, flameLengthTiles),
            tileSize,
            flameBurstDuration);
    }

    void ShowDirectionalIdle(Vector2 facingDirection)
    {
        direction = ToCardinal(facingDirection);
        UpdateSpriteDirection(direction);

        if (attackSprite != null)
            attackSprite.enabled = false;

        if (activeSprite == null)
            return;

        activeSprite.enabled = true;
        activeSprite.idle = true;
        activeSprite.CurrentFrame = 0;
        activeSprite.RefreshFrame();
    }

    Vector2 GetPreferredDirection()
    {
        Vector2 origin = rb != null ? rb.position : (Vector2)transform.position;
        MovementController closestPlayer = null;
        float closestDistance = float.PositiveInfinity;

        foreach (MovementController player in FindObjectsByType<MovementController>(FindObjectsInactive.Exclude))
        {
            if (player == null || !player.isActiveAndEnabled || player.isDead || !player.CompareTag("Player"))
                continue;

            if (playerLayerMask.value != 0 && ((1 << player.gameObject.layer) & playerLayerMask.value) == 0)
                continue;

            float distance = ((Vector2)player.transform.position - origin).sqrMagnitude;
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestPlayer = player;
            }
        }

        if (closestPlayer == null)
            return direction != Vector2.zero ? ToCardinal(direction) : Vector2.down;

        return ToCardinal((Vector2)closestPlayer.transform.position - origin);
    }

    void DisableMovementSprites()
    {
        if (spriteUp != null) spriteUp.enabled = false;
        if (spriteDown != null) spriteDown.enabled = false;
        if (spriteLeft != null) spriteLeft.enabled = false;
        if (spriteRight != null) spriteRight.enabled = false;
        if (spriteDamaged != null) spriteDamaged.enabled = false;
    }

    bool IsStunned() => TryGetComponent(out StunReceiver stun) && stun.IsStunned;

    static Vector2 ToCardinal(Vector2 direction)
    {
        if (Mathf.Abs(direction.x) >= Mathf.Abs(direction.y))
            return direction.x >= 0f ? Vector2.right : Vector2.left;

        return direction.y >= 0f ? Vector2.up : Vector2.down;
    }

    IEnumerator WaitGameplaySeconds(float seconds)
    {
        float elapsed = 0f;
        while (elapsed < seconds && isActiveAndEnabled && !isDead)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
    }
}

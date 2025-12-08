using UnityEngine;

public class MetalHornMovementController : PersecutingEnemyMovementController
{
    [Header("Metal Horn Settings")]
    public float chargePauseDuration = 0.5f;
    public float chargeSpeedMultiplier = 2f;

    private bool isPreparingCharge;
    private bool isCharging;
    private float chargeTimer;
    private Vector2 chargeDirection;
    private float baseSpeed;

    protected override void Start()
    {
        base.Start();
        baseSpeed = speed;
    }

    protected override void FixedUpdate()
    {
        if (isDead)
            return;

        if (isPreparingCharge)
        {
            rb.linearVelocity = Vector2.zero;
            chargeTimer -= Time.fixedDeltaTime;

            if (chargeTimer <= 0f)
            {
                isPreparingCharge = false;
                isCharging = true;

                direction = chargeDirection;
                UpdateSpriteDirection(direction);

                SnapToGrid();
                targetTile = rb.position + direction * tileSize;
            }

            return;
        }

        if (isCharging)
        {
            speed = baseSpeed * chargeSpeedMultiplier;

            if (!HasPlayerInDirection(chargeDirection))
            {
                isCharging = false;
                speed = baseSpeed;
                base.FixedUpdate();
                return;
            }

            if (HasBombAt(targetTile))
                HandleBombAhead();

            rb.MovePosition(
                Vector2.MoveTowards(
                    rb.position,
                    targetTile,
                    speed * Time.fixedDeltaTime
                )
            );

            if (ReachedTile())
            {
                SnapToGrid();
                targetTile = rb.position + direction * tileSize;
            }

            return;
        }

        speed = baseSpeed;
        base.FixedUpdate();
    }

    protected override void DecideNextTile()
    {
        if (isPreparingCharge || isCharging)
            return;

        if (MetalHornTryGetPlayerDirection(out Vector2 playerDir))
        {
            isPreparingCharge = true;
            isCharging = false;
            chargeTimer = chargePauseDuration;
            chargeDirection = playerDir;

            direction = chargeDirection;
            UpdateSpriteDirection(direction);

            rb.linearVelocity = Vector2.zero;
            SnapToGrid();
            return;
        }

        base.DecideNextTile();
    }

    private bool MetalHornTryGetPlayerDirection(out Vector2 dirToPlayer)
    {
        dirToPlayer = Vector2.zero;

        Vector2[] dirs =
        {
            Vector2.up,
            Vector2.down,
            Vector2.left,
            Vector2.right
        };

        int maxSteps = Mathf.Max(1, Mathf.RoundToInt(visionDistance / tileSize));
        Vector2 boxSize = Vector2.one * (tileSize * 0.8f);

        foreach (var dir in dirs)
        {
            for (int step = 1; step <= maxSteps; step++)
            {
                Vector2 tileCenter = rb.position + step * tileSize * dir;

                Collider2D playerHit = Physics2D.OverlapBox(
                    tileCenter,
                    boxSize,
                    0f,
                    playerLayerMask
                );

                if (playerHit != null)
                {
                    dirToPlayer = dir;
                    return true;
                }

                if (IsTileBlocked(tileCenter))
                    break;
            }
        }

        return false;
    }

    private bool HasPlayerInDirection(Vector2 dir)
    {
        int maxSteps = Mathf.Max(1, Mathf.RoundToInt(visionDistance / tileSize));
        Vector2 boxSize = Vector2.one * (tileSize * 0.8f);

        for (int step = 1; step <= maxSteps; step++)
        {
            Vector2 tileCenter = rb.position + step * tileSize * dir;

            Collider2D playerHit = Physics2D.OverlapBox(
                tileCenter,
                boxSize,
                0f,
                playerLayerMask
            );

            if (playerHit != null)
                return true;

            if (IsTileBlocked(tileCenter))
                break;
        }

        return false;
    }

    protected override void Die()
    {
        isPreparingCharge = false;
        isCharging = false;
        speed = baseSpeed;
        base.Die();
    }
}

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

        int mask = obstacleMask | playerLayerMask;

        foreach (var dir in dirs)
        {
            Vector2 origin = rb.position + dir * (tileSize * 0.5f);

            RaycastHit2D hit = Physics2D.Raycast(
                origin,
                dir,
                visionDistance,
                mask
            );

            if (hit.collider == null)
                continue;

            if (((1 << hit.collider.gameObject.layer) & playerLayerMask) != 0)
            {
                dirToPlayer = dir;
                return true;
            }
        }

        return false;
    }

    private bool HasPlayerInDirection(Vector2 dir)
    {
        if (dir == Vector2.zero)
            return false;

        int mask = obstacleMask | playerLayerMask;
        Vector2 origin = rb.position + dir.normalized * (tileSize * 0.5f);

        RaycastHit2D hit = Physics2D.Raycast(
            origin,
            dir.normalized,
            visionDistance,
            mask
        );

        if (hit.collider == null)
            return false;

        return ((1 << hit.collider.gameObject.layer) & playerLayerMask) != 0;
    }

    protected override void Die()
    {
        isPreparingCharge = false;
        isCharging = false;
        speed = baseSpeed;
        base.Die();
    }
}

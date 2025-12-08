using UnityEngine;

public class HornMetalMovementController : EnemyMovementController
{
    [Header("HornMetal (Charge Settings)")]
    public float chargeMultiplier = 2f;
    public float chargePauseDuration = 0.5f;
    public float visionDistance = 10f;
    public LayerMask playerLayerMask;

    private bool isCharging;
    private bool isPreparingCharge;
    private float chargePauseTimer;
    private Vector2 chargeDirection;
    private float baseSpeed;

    protected override void Start()
    {
        base.Start();

        baseSpeed = speed;

        if (playerLayerMask.value == 0)
            playerLayerMask = LayerMask.GetMask("Player");
    }

    protected override void FixedUpdate()
    {
        if (isDead)
            return;

        if (isPreparingCharge)
        {
            rb.linearVelocity = Vector2.zero;
            chargePauseTimer -= Time.fixedDeltaTime;

            if (chargePauseTimer <= 0f)
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
            if (!IsPlayerInSight(chargeDirection))
            {
                StopCharging();
                base.FixedUpdate();
                return;
            }

            if (HasBombAt(targetTile))
                HandleBombAhead();

            float currentSpeed = baseSpeed * chargeMultiplier;

            rb.MovePosition(
                Vector2.MoveTowards(
                    rb.position,
                    targetTile,
                    currentSpeed * Time.fixedDeltaTime
                )
            );

            if (ReachedTile())
            {
                SnapToGrid();
                targetTile = rb.position + direction * tileSize;
            }

            return;
        }

        base.FixedUpdate();

        if (!isCharging && !isPreparingCharge)
            TryStartCharge();
    }

    private void TryStartCharge()
    {
        if (HasLineOfSightToPlayer(out Vector2 dirToPlayer))
        {
            isPreparingCharge = true;
            isCharging = false;
            chargePauseTimer = chargePauseDuration;
            chargeDirection = dirToPlayer;

            rb.linearVelocity = Vector2.zero;
            SnapToGrid();
        }
    }

    private bool HasLineOfSightToPlayer(out Vector2 dirToPlayer)
    {
        dirToPlayer = Vector2.zero;

        Vector2[] dirs =
        {
            Vector2.up,
            Vector2.down,
            Vector2.left,
            Vector2.right
        };

        foreach (var dir in dirs)
        {
            RaycastHit2D hit = Physics2D.Raycast(
                rb.position,
                dir,
                visionDistance,
                obstacleMask | playerLayerMask
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

    private bool IsPlayerInSight(Vector2 dir)
    {
        RaycastHit2D hit = Physics2D.Raycast(
            rb.position,
            dir,
            visionDistance,
            obstacleMask | playerLayerMask
        );

        if (hit.collider == null)
            return false;

        return ((1 << hit.collider.gameObject.layer) & playerLayerMask) != 0;
    }

    private void StopCharging()
    {
        isCharging = false;
        isPreparingCharge = false;
        speed = baseSpeed;
    }
}

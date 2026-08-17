using UnityEngine;

/// <summary>
/// Flying enemy that keeps the usual ghost-flight navigation, but turns toward
/// and accelerates for a player aligned on one of the four cardinal axes.
/// Destructible blocks do not hide an aligned player, but indestructible tiles
/// and bombs stop the pursuit line of sight.
/// </summary>
public class FlyingCardinalPursuitMovementController : FlyMovimentController
{
    [Header("Cardinal Pursuit")]
    [SerializeField, Min(0.1f)] private float visionDistance = 10f;
    [SerializeField] private LayerMask playerLayerMask;
    [SerializeField, Min(0.001f)] private float alignedToleranceTiles = 0.15f;
    [SerializeField, Min(1f)] private float pursuitSpeedMultiplier = 2f;

    private float patrolSpeed;

    protected override void Awake()
    {
        base.Awake();
        patrolSpeed = speed;

        if (playerLayerMask.value == 0)
            playerLayerMask = LayerMask.GetMask("Player");
    }

    protected override void FixedUpdate()
    {
        speed = TryGetPlayerDirection(out _) ? patrolSpeed * pursuitSpeedMultiplier : patrolSpeed;
        base.FixedUpdate();
    }

    protected override void DecideNextTile()
    {
        if (TryGetPlayerDirection(out Vector2 playerDirection))
        {
            Vector2 forwardTile = rb.position + playerDirection * tileSize;

            if (!IsTileBlocked(forwardTile))
            {
                direction = playerDirection;
                UpdateSpriteDirection(direction);
                targetTile = forwardTile;
                return;
            }
        }

        base.DecideNextTile();
    }

    private bool TryGetPlayerDirection(out Vector2 directionToPlayer)
    {
        directionToPlayer = Vector2.zero;

        if (rb == null || playerLayerMask.value == 0)
            return false;

        Vector2 selfPosition = rb.position;
        float tolerance = Mathf.Max(0.001f, alignedToleranceTiles * tileSize);
        float nearestDistance = visionDistance;

        Collider2D[] players = Physics2D.OverlapCircleAll(selfPosition, visionDistance, playerLayerMask);
        for (int i = 0; i < players.Length; i++)
        {
            Collider2D player = players[i];
            if (player == null)
                continue;

            Vector2 playerPosition = player.attachedRigidbody != null
                ? player.attachedRigidbody.position
                : (Vector2)player.transform.position;
            Vector2 offset = playerPosition - selfPosition;

            bool verticallyAligned = Mathf.Abs(offset.x) <= tolerance && Mathf.Abs(offset.y) > tolerance;
            bool horizontallyAligned = Mathf.Abs(offset.y) <= tolerance && Mathf.Abs(offset.x) > tolerance;
            if (!verticallyAligned && !horizontallyAligned)
                continue;

            float distance = verticallyAligned ? Mathf.Abs(offset.y) : Mathf.Abs(offset.x);
            if (distance > nearestDistance)
                continue;

            Vector2 candidateDirection = verticallyAligned
                ? (offset.y > 0f ? Vector2.up : Vector2.down)
                : (offset.x > 0f ? Vector2.right : Vector2.left);

            if (!HasClearPursuitLine(selfPosition, candidateDirection, distance))
                continue;

            nearestDistance = distance;
            directionToPlayer = candidateDirection;
        }

        return directionToPlayer != Vector2.zero;
    }

    private bool HasClearPursuitLine(Vector2 origin, Vector2 scanDirection, float playerDistance)
    {
        int interveningTiles = Mathf.Max(0, Mathf.CeilToInt(playerDistance / tileSize) - 1);

        for (int step = 1; step <= interveningTiles; step++)
        {
            Vector2 tileCenter = origin + scanDirection * (step * tileSize);
            if (IsTileBlocked(tileCenter))
                return false;
        }

        return true;
    }
}

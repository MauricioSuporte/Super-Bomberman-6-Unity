using UnityEngine;

/// <summary>
/// A Hogera-derived Baloon that turns toward the closest nearby player and
/// releases two Mini Baloons after its complete death animation.
/// </summary>
public sealed class BigBaloonMovementController : HogeraMovementController
{
    [Header("Nearby Player Pursuit")]
    [SerializeField, Min(0.1f)] private float pursuitDistance = 3f;
    [SerializeField] private LayerMask playerLayerMask;

    protected override int MiniHogeraCount => 2;

    protected override void Awake()
    {
        base.Awake();
        waitForFullDeathAnimation = true;

        if (playerLayerMask.value == 0)
            playerLayerMask = LayerMask.GetMask("Player");
    }

    protected override void DecideNextTile()
    {
        if (TryGetNearbyPlayerDirection(out Vector2 pursuitDirection))
        {
            Vector2 pursuitTile = rb.position + pursuitDirection * tileSize;
            if (!IsTileBlocked(pursuitTile))
            {
                direction = pursuitDirection;
                UpdateSpriteDirection(direction);
                targetTile = pursuitTile;
                return;
            }
        }

        base.DecideNextTile();
    }

    private bool TryGetNearbyPlayerDirection(out Vector2 pursuitDirection)
    {
        pursuitDirection = Vector2.zero;
        if (playerLayerMask.value == 0 || rb == null)
            return false;

        Collider2D[] players = Physics2D.OverlapCircleAll(rb.position, pursuitDistance, playerLayerMask);
        float closestDistanceSquared = float.PositiveInfinity;
        Vector2 closestPlayerPosition = Vector2.zero;

        for (int i = 0; i < players.Length; i++)
        {
            Collider2D player = players[i];
            if (player == null)
                continue;

            Vector2 playerPosition = player.attachedRigidbody != null
                ? player.attachedRigidbody.position
                : (Vector2)player.transform.position;
            float distanceSquared = (playerPosition - rb.position).sqrMagnitude;

            if (distanceSquared < closestDistanceSquared)
            {
                closestDistanceSquared = distanceSquared;
                closestPlayerPosition = playerPosition;
            }
        }

        if (float.IsPositiveInfinity(closestDistanceSquared))
            return false;

        Vector2 offset = closestPlayerPosition - rb.position;
        if (Mathf.Abs(offset.x) > Mathf.Abs(offset.y))
            pursuitDirection = offset.x >= 0f ? Vector2.right : Vector2.left;
        else
            pursuitDirection = offset.y >= 0f ? Vector2.up : Vector2.down;

        return true;
    }

}

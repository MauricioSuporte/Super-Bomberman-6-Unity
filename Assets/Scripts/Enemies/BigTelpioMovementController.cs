using UnityEngine;

/// <summary>
/// A Hogera-derived Telpio that pursues nearby players, crosses destructible
/// blocks, and releases two Mini Telpios after its complete death animation.
/// </summary>
public sealed class BigTelpioMovementController : HogeraMovementController
{
    [Header("Nearby Player Pursuit")]
    [SerializeField, Min(0.1f)] private float pursuitDistance = 3f;
    [SerializeField] private LayerMask playerLayerMask;

    [Header("Destructibles Pass Through")]
    [SerializeField] private string destructiblesTag = "Destructibles";

    private Collider2D selfCollider;

    protected override int MiniHogeraCount => 2;

    protected override void Awake()
    {
        base.Awake();
        waitForFullDeathAnimation = true;

        if (playerLayerMask.value == 0)
            playerLayerMask = LayerMask.GetMask("Player");

        selfCollider = GetComponent<Collider2D>();
        IgnoreDestructibleCollisions();
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

    protected override bool IsTileBlocked(Vector2 tileCenter)
    {
        Vector2 size = Vector2.one * (tileSize * 0.8f);
        Collider2D[] hits = Physics2D.OverlapBoxAll(tileCenter, size, 0f, obstacleMask);

        foreach (Collider2D hit in hits)
        {
            if (hit == null || hit.gameObject == gameObject || hit.CompareTag(destructiblesTag))
                continue;

            return true;
        }

        return false;
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

    private void IgnoreDestructibleCollisions()
    {
        if (selfCollider == null || string.IsNullOrEmpty(destructiblesTag))
            return;

        foreach (GameObject destructibles in GameObject.FindGameObjectsWithTag(destructiblesTag))
        {
            if (destructibles == null)
                continue;

            foreach (Collider2D destructibleCollider in destructibles.GetComponentsInChildren<Collider2D>(true))
            {
                if (destructibleCollider != null)
                    Physics2D.IgnoreCollision(selfCollider, destructibleCollider, true);
            }
        }
    }
}

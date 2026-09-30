using UnityEngine;

/// <summary>
/// A destructible-block-piercing junction turner that steers toward the
/// nearest player and intermittently gains a short 50% speed increase.
/// </summary>
public sealed class RobofaceMovementController : JunctionTurningEnemyMovementController
{
    [Header("Roboface Behaviour")]
    [SerializeField, Range(0f, 1f)] private float speedBoostChancePerTile = 0.2f;
    [SerializeField, Min(0.01f)] private float speedBoostDuration = 0.5f;
    [SerializeField] private LayerMask playerLayerMask;
    [SerializeField] private string destructibleTag = "Destructibles";

    private float normalSpeed;
    private float speedBoostRemaining;
    private Collider2D selfCollider;

    protected override void Awake()
    {
        base.Awake();
        normalSpeed = speed;
        selfCollider = GetComponent<Collider2D>();
        IgnoreDestructibleCollisions();
    }

    protected override void Start()
    {
        if (playerLayerMask.value == 0)
            playerLayerMask = LayerMask.GetMask("Player");

        base.Start();
    }

    protected override void FixedUpdate()
    {
        if (speedBoostRemaining > 0f)
        {
            speedBoostRemaining = Mathf.Max(0f, speedBoostRemaining - Time.fixedDeltaTime);
            if (speedBoostRemaining == 0f)
                speed = normalSpeed;
        }

        base.FixedUpdate();
    }

    protected override void DecideNextTile()
    {
        if (TryGetClosestPlayerDirection(out Vector2 playerDirection))
        {
            Vector2 playerTile = rb.position + playerDirection * tileSize;
            if (!IsTileBlocked(playerTile))
            {
                direction = playerDirection;
                UpdateSpriteDirection(direction);
                targetTile = playerTile;
                TryStartSpeedBoost();
                return;
            }
        }

        base.DecideNextTile();
        TryStartSpeedBoost();
    }

    protected override bool IsTileBlocked(Vector2 tileCenter)
    {
        Collider2D[] hits = Physics2D.OverlapBoxAll(tileCenter, Vector2.one * (tileSize * 0.8f), 0f, obstacleMask);
        for (int index = 0; index < hits.Length; index++)
        {
            Collider2D hit = hits[index];
            if (hit == null || hit.gameObject == gameObject || IsDestructible(hit))
                continue;

            return true;
        }

        return false;
    }

    private bool TryGetClosestPlayerDirection(out Vector2 directionToPlayer)
    {
        directionToPlayer = Vector2.zero;
        Collider2D[] players = Physics2D.OverlapCircleAll(rb.position, 1000f, playerLayerMask);
        float closestDistanceSquared = float.PositiveInfinity;
        Vector2 closestPosition = Vector2.zero;

        for (int index = 0; index < players.Length; index++)
        {
            Collider2D player = players[index];
            if (player == null)
                continue;

            Vector2 playerPosition = player.attachedRigidbody != null
                ? player.attachedRigidbody.position
                : (Vector2)player.transform.position;
            float distanceSquared = (playerPosition - rb.position).sqrMagnitude;
            if (distanceSquared >= closestDistanceSquared)
                continue;

            closestDistanceSquared = distanceSquared;
            closestPosition = playerPosition;
        }

        if (float.IsPositiveInfinity(closestDistanceSquared))
            return false;

        Vector2 offset = closestPosition - rb.position;
        directionToPlayer = Mathf.Abs(offset.x) >= Mathf.Abs(offset.y)
            ? (offset.x >= 0f ? Vector2.right : Vector2.left)
            : (offset.y >= 0f ? Vector2.up : Vector2.down);
        return true;
    }

    private void TryStartSpeedBoost()
    {
        if (speedBoostRemaining > 0f || Random.value > speedBoostChancePerTile)
            return;

        speedBoostRemaining = speedBoostDuration;
        speed = normalSpeed * 1.5f;
    }

    private bool IsDestructible(Collider2D hit)
    {
        for (Transform current = hit.transform; current != null; current = current.parent)
            if (current.CompareTag(destructibleTag))
                return true;

        return false;
    }

    private void IgnoreDestructibleCollisions()
    {
        if (selfCollider == null)
            return;

        GameObject[] destructibles = GameObject.FindGameObjectsWithTag(destructibleTag);
        for (int objectIndex = 0; objectIndex < destructibles.Length; objectIndex++)
        {
            Collider2D[] colliders = destructibles[objectIndex].GetComponentsInChildren<Collider2D>(true);
            for (int colliderIndex = 0; colliderIndex < colliders.Length; colliderIndex++)
                if (colliders[colliderIndex] != null)
                    Physics2D.IgnoreCollision(selfCollider, colliders[colliderIndex], true);
        }
    }
}

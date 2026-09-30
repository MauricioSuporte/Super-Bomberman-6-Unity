using UnityEngine;

/// <summary>
/// Checker93 continuously selects the closest Bomberman as its next grid
/// destination, but gives a visible bomb priority and retreats from it.
/// </summary>
public sealed class Checker93MovementController : EnemyMovementController
{
    [Header("Checker93 Behaviour")]
    [SerializeField, Min(0.1f)] private float bombFleeDistance = 4f;
    [SerializeField] private LayerMask playerLayerMask;

    protected override void Start()
    {
        if (playerLayerMask.value == 0)
            playerLayerMask = LayerMask.GetMask("Player");

        base.Start();
    }

    protected override void DecideNextTile()
    {
        if (TryGetClosestBomb(out Vector2 bombPosition))
        {
            ChooseDirectionAwayFrom(bombPosition);
            return;
        }

        if (TryGetClosestPlayer(out Vector2 playerPosition))
        {
            ChooseDirectionTowards(playerPosition);
            return;
        }

        base.DecideNextTile();
    }

    private bool TryGetClosestBomb(out Vector2 bombPosition)
    {
        return TryGetClosestPosition(bombLayerMask, bombFleeDistance, out bombPosition);
    }

    private bool TryGetClosestPlayer(out Vector2 playerPosition)
    {
        return TryGetClosestPosition(playerLayerMask, float.PositiveInfinity, out playerPosition);
    }

    private bool TryGetClosestPosition(LayerMask layerMask, float searchRadius, out Vector2 closestPosition)
    {
        closestPosition = Vector2.zero;
        Collider2D[] hits = float.IsPositiveInfinity(searchRadius)
            ? Physics2D.OverlapCircleAll(rb.position, 1000f, layerMask)
            : Physics2D.OverlapCircleAll(rb.position, searchRadius, layerMask);

        float closestDistanceSquared = float.PositiveInfinity;
        for (int index = 0; index < hits.Length; index++)
        {
            Collider2D hit = hits[index];
            if (hit == null)
                continue;

            Vector2 candidatePosition = hit.attachedRigidbody != null
                ? hit.attachedRigidbody.position
                : (Vector2)hit.transform.position;
            float distanceSquared = ((Vector2)rb.position - candidatePosition).sqrMagnitude;
            if (distanceSquared >= closestDistanceSquared)
                continue;

            closestDistanceSquared = distanceSquared;
            closestPosition = candidatePosition;
        }

        return !float.IsPositiveInfinity(closestDistanceSquared);
    }

    private void ChooseDirectionTowards(Vector2 targetPosition)
    {
        ChooseBestDirection(targetPosition, preferGreaterDistance: false);
    }

    private void ChooseDirectionAwayFrom(Vector2 threatPosition)
    {
        ChooseBestDirection(threatPosition, preferGreaterDistance: true);
    }

    private void ChooseBestDirection(Vector2 referencePosition, bool preferGreaterDistance)
    {
        Vector2 bestDirection = Vector2.zero;
        float bestDistanceSquared = preferGreaterDistance ? float.NegativeInfinity : float.PositiveInfinity;

        for (int index = 0; index < Dirs.Length; index++)
        {
            Vector2 candidateDirection = Dirs[index];
            Vector2 candidateTile = rb.position + candidateDirection * tileSize;
            if (IsTileBlocked(candidateTile))
                continue;

            float distanceSquared = (candidateTile - referencePosition).sqrMagnitude;
            bool isBetter = preferGreaterDistance
                ? distanceSquared > bestDistanceSquared
                : distanceSquared < bestDistanceSquared;
            if (!isBetter)
                continue;

            bestDistanceSquared = distanceSquared;
            bestDirection = candidateDirection;
        }

        if (bestDirection == Vector2.zero)
        {
            base.DecideNextTile();
            return;
        }

        direction = bestDirection;
        UpdateSpriteDirection(direction);
        targetTile = rb.position + direction * tileSize;
    }
}

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fast enemy that crosses destructible blocks and turns at every available
/// perpendicular route, alternating left and right turns when possible.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public sealed class SpeederMovementController : EnemyMovementController
{
    [Header("Destructible Pass")]
    [SerializeField] private string destructiblesTag = "Destructibles";

    private Collider2D selfCollider;
    private bool preferLeftTurn;

    protected override void Awake()
    {
        base.Awake();

        selfCollider = GetComponent<Collider2D>();
        preferLeftTurn = Random.value < 0.5f;
        IgnoreDestructibleCollisions();
    }

    protected override void DecideNextTile()
    {
        isStuck = false;

        Vector2 left = new(-direction.y, direction.x);
        Vector2 right = new(direction.y, -direction.x);
        Vector2 forwardTile = rb.position + direction * tileSize;
        Vector2 leftTile = rb.position + left * tileSize;
        Vector2 rightTile = rb.position + right * tileSize;

        bool canTurnLeft = !IsTileBlocked(leftTile);
        bool canTurnRight = !IsTileBlocked(rightTile);

        // A perpendicular route always wins over continuing straight. Alternating
        // the preferred side produces a natural zig-zag where both turns exist.
        if (canTurnLeft || canTurnRight)
        {
            bool turnLeft = canTurnLeft && (!canTurnRight || preferLeftTurn);
            direction = turnLeft ? left : right;
            preferLeftTurn = !turnLeft;
            UpdateSpriteDirection(direction);
            targetTile = rb.position + direction * tileSize;
            return;
        }

        if (!IsTileBlocked(forwardTile))
        {
            targetTile = forwardTile;
            return;
        }

        Vector2 backward = -direction;
        Vector2 backwardTile = rb.position + backward * tileSize;
        if (!IsTileBlocked(backwardTile))
        {
            direction = backward;
            UpdateSpriteDirection(direction);
            targetTile = backwardTile;
            return;
        }

        targetTile = rb.position;
        isStuck = true;
        stuckTimer = recheckStuckEverySeconds;
    }

    protected override bool IsTileBlocked(Vector2 tileCenter)
    {
        Vector2 size = Vector2.one * (tileSize * 0.8f);
        Collider2D[] hits = Physics2D.OverlapBoxAll(tileCenter, size, 0f, obstacleMask);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || hit.gameObject == gameObject)
                continue;

            if (hit.CompareTag(destructiblesTag))
                continue;

            return true;
        }

        return false;
    }

    private void IgnoreDestructibleCollisions()
    {
        if (selfCollider == null)
            return;

        GameObject[] destructibleRoots = GameObject.FindGameObjectsWithTag(destructiblesTag);
        for (int rootIndex = 0; rootIndex < destructibleRoots.Length; rootIndex++)
        {
            Collider2D[] colliders = destructibleRoots[rootIndex].GetComponentsInChildren<Collider2D>(true);
            for (int colliderIndex = 0; colliderIndex < colliders.Length; colliderIndex++)
            {
                Collider2D collider = colliders[colliderIndex];
                if (collider != null)
                    Physics2D.IgnoreCollision(selfCollider, collider, true);
            }
        }
    }
}

using UnityEngine;

/// <summary>
/// Pursues a visible player through the standard junction-turning line of
/// sight, moving 50% faster for as long as that player remains visible.
/// </summary>
public sealed class Yeti93MovementController : JunctionTurningPersecutingEnemyMovementController
{
    [SerializeField, Min(1f)] private float pursuitSpeedMultiplier = 1.5f;
    [SerializeField] private string destructibleTag = "Destructibles";

    private float patrolSpeed;
    private Collider2D selfCollider;

    protected override void Awake()
    {
        base.Awake();
        patrolSpeed = speed;
        selfCollider = GetComponent<Collider2D>();

        IgnoreDestructibleCollisions();
    }

    protected override void ApplyPursuitSpeed(bool playerVisible)
    {
        speed = playerVisible ? patrolSpeed * pursuitSpeedMultiplier : patrolSpeed;
    }

    protected override bool IsTileBlocked(Vector2 tileCenter)
    {
        Vector2 size = Vector2.one * (tileSize * 0.8f);
        Collider2D[] hits = Physics2D.OverlapBoxAll(tileCenter, size, 0f, obstacleMask);

        for (int hitIndex = 0; hitIndex < hits.Length; hitIndex++)
        {
            Collider2D hit = hits[hitIndex];
            if (hit == null || hit.gameObject == gameObject || hit.CompareTag(destructibleTag))
                continue;

            return true;
        }

        return false;
    }

    private void IgnoreDestructibleCollisions()
    {
        if (selfCollider == null)
            return;

        GameObject[] destructibles = GameObject.FindGameObjectsWithTag(destructibleTag);
        for (int destructibleIndex = 0; destructibleIndex < destructibles.Length; destructibleIndex++)
        {
            Collider2D[] colliders = destructibles[destructibleIndex].GetComponentsInChildren<Collider2D>(true);
            for (int colliderIndex = 0; colliderIndex < colliders.Length; colliderIndex++)
            {
                Collider2D destructibleCollider = colliders[colliderIndex];
                if (destructibleCollider != null)
                    Physics2D.IgnoreCollision(selfCollider, destructibleCollider, true);
            }
        }
    }
}

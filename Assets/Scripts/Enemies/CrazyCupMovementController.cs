using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class CrazyCupMovementController : EnemyMovementController
{
    private Collider2D selfCollider;

    protected override void Awake()
    {
        base.Awake();

        selfCollider = GetComponent<Collider2D>();

        GameObject destructibles = GameObject.FindGameObjectWithTag("Destructibles");
        if (destructibles != null)
        {
            var destructibleColliders = destructibles.GetComponents<Collider2D>();
            foreach (var col in destructibleColliders)
            {
                Physics2D.IgnoreCollision(selfCollider, col);
            }
        }
    }

    protected override void DecideNextTile()
    {
        Vector2[] dirs = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
        var freeDirs = new List<Vector2>();

        foreach (var dir in dirs)
        {
            Vector2 checkTile = rb.position + dir * tileSize;
            if (!IsTileBlocked(checkTile))
                freeDirs.Add(dir);
        }

        if (freeDirs.Count == 0)
        {
            targetTile = rb.position;
            return;
        }

        direction = freeDirs[Random.Range(0, freeDirs.Count)];
        UpdateSpriteDirection(direction);
        targetTile = rb.position + direction * tileSize;
    }

    protected override bool IsTileBlocked(Vector2 tileCenter)
    {
        Vector2 size = Vector2.one * (tileSize * 0.8f);
        Collider2D[] hits = Physics2D.OverlapBoxAll(tileCenter, size, 0f, obstacleMask);

        if (hits == null || hits.Length == 0)
            return false;

        foreach (var hit in hits)
        {
            if (hit == null)
                continue;

            if (hit.gameObject == gameObject)
                continue;

            if (hit.CompareTag("Destructibles"))
                continue;

            return true;
        }

        return false;
    }
}

using System.Collections.Generic;
using UnityEngine;

public class JunctionTurningEnemyMovementController : EnemyMovementController
{
    [Header("Junction Turning")]
    public int minAvailablePathsToTurn = 3;

    public bool preferTurnAtJunction = false;

    [Header("Destructible Pass")]
    [SerializeField] private bool passesThroughDestructibles;
    [SerializeField] private string junctionPassDestructiblesTag = "Destructibles";

    private bool hasInitialDirection;

    protected override void Awake()
    {
        base.Awake();

        if (!passesThroughDestructibles)
            return;

        Collider2D junctionCollider = GetComponent<Collider2D>();
        if (junctionCollider == null)
            return;

        GameObject[] destructibles = GameObject.FindGameObjectsWithTag(junctionPassDestructiblesTag);
        for (int objectIndex = 0; objectIndex < destructibles.Length; objectIndex++)
        {
            Collider2D[] colliders = destructibles[objectIndex].GetComponentsInChildren<Collider2D>(true);
            for (int colliderIndex = 0; colliderIndex < colliders.Length; colliderIndex++)
            {
                Collider2D destructibleCollider = colliders[colliderIndex];
                if (destructibleCollider != null)
                    Physics2D.IgnoreCollision(junctionCollider, destructibleCollider, true);
            }
        }
    }

    /// <summary>
    /// Sets the direction that will be used by Start. This lets a group of
    /// otherwise standard junction-turning enemies disperse from one tile.
    /// </summary>
    public void SetInitialDirection(Vector2 desiredDirection)
    {
        if (Mathf.Abs(desiredDirection.x) > Mathf.Abs(desiredDirection.y))
            direction = desiredDirection.x >= 0f ? Vector2.right : Vector2.left;
        else
            direction = desiredDirection.y >= 0f ? Vector2.up : Vector2.down;

        hasInitialDirection = true;
    }

    protected override void Start()
    {
        SnapToGrid();

        if (!hasInitialDirection)
            ChooseInitialDirection();

        UpdateSpriteDirection(direction);
        DecideNextTile();
    }

    protected override void DecideNextTile()
    {
        Vector2[] dirs = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };

        var freeDirs = new List<Vector2>(4);

        for (int i = 0; i < dirs.Length; i++)
        {
            Vector2 dir = dirs[i];
            Vector2 checkTile = rb.position + dir * tileSize;

            if (!IsTileBlocked(checkTile))
                freeDirs.Add(dir);
        }

        if (freeDirs.Count == 0)
        {
            targetTile = rb.position;
            UpdateSpriteDirection(direction);
            return;
        }

        bool isJunction = freeDirs.Count >= minAvailablePathsToTurn;

        if (isJunction)
        {
            Vector2 chosenDir = direction;

            if (preferTurnAtJunction)
            {
                var turningDirs = new List<Vector2>(freeDirs.Count);

                for (int i = 0; i < freeDirs.Count; i++)
                {
                    Vector2 d = freeDirs[i];
                    if (d != direction)
                        turningDirs.Add(d);
                }

                if (turningDirs.Count > 0)
                    chosenDir = turningDirs[Random.Range(0, turningDirs.Count)];
                else
                    chosenDir = freeDirs[Random.Range(0, freeDirs.Count)];
            }
            else
            {
                chosenDir = freeDirs[Random.Range(0, freeDirs.Count)];
            }

            direction = chosenDir;
            UpdateSpriteDirection(direction);
            targetTile = rb.position + direction * tileSize;
            return;
        }

        base.DecideNextTile();
    }

    protected override bool IsTileBlocked(Vector2 tileCenter)
    {
        if (!passesThroughDestructibles)
            return base.IsTileBlocked(tileCenter);

        Vector2 size = Vector2.one * (tileSize * 0.8f);
        Collider2D[] hits = Physics2D.OverlapBoxAll(tileCenter, size, 0f, obstacleMask);

        for (int index = 0; index < hits.Length; index++)
        {
            Collider2D hit = hits[index];
            if (hit == null || hit.gameObject == gameObject || IsDestructible(hit))
                continue;

            return true;
        }

        return false;
    }

    private bool IsDestructible(Collider2D hit)
    {
        for (Transform current = hit.transform; current != null; current = current.parent)
        {
            if (current.CompareTag(junctionPassDestructiblesTag))
                return true;
        }

        return false;
    }
}

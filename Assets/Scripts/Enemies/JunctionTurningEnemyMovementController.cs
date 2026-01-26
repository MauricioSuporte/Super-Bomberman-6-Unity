using System.Collections.Generic;
using UnityEngine;

public class JunctionTurningEnemyMovementController : EnemyMovementController
{
    [Header("Junction Turning")]
    public int minAvailablePathsToTurn = 3;

    public bool preferTurnAtJunction = false;

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
}

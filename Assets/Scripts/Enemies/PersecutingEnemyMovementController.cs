using UnityEngine;

public class PersecutingEnemyMovementController : EnemyMovementController
{
    [Header("Persecuting Settings")]
    public float visionDistance = 10f;
    public LayerMask playerLayerMask;

    protected override void Start()
    {
        base.Start();

        if (playerLayerMask.value == 0)
            playerLayerMask = LayerMask.GetMask("Player");
    }

    protected override void DecideNextTile()
    {
        if (TryGetPlayerDirection(out Vector2 playerDir))
        {
            Vector2 forwardTile = rb.position + playerDir * tileSize;

            if (!IsTileBlocked(forwardTile))
            {
                direction = playerDir;
                UpdateSpriteDirection(direction);
                targetTile = forwardTile;
                return;
            }
        }

        base.DecideNextTile();
    }

    private bool TryGetPlayerDirection(out Vector2 dirToPlayer)
    {
        dirToPlayer = Vector2.zero;

        Vector2[] dirs =
        {
            Vector2.up,
            Vector2.down,
            Vector2.left,
            Vector2.right
        };

        int maxSteps = Mathf.Max(1, Mathf.RoundToInt(visionDistance / tileSize));
        Vector2 boxSize = Vector2.one * (tileSize * 0.8f);

        foreach (var dir in dirs)
        {
            for (int step = 1; step <= maxSteps; step++)
            {
                Vector2 tileCenter = rb.position + step * tileSize * dir;

                Collider2D playerHit = Physics2D.OverlapBox(
                    tileCenter,
                    boxSize,
                    0f,
                    playerLayerMask
                );

                if (playerHit != null)
                {
                    dirToPlayer = dir;
                    return true;
                }

                if (IsTileBlocked(tileCenter))
                    break;
            }
        }

        return false;
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();

        if (!Application.isPlaying || rb == null)
            return;

        Gizmos.color = Color.yellow;

        Vector2[] dirs =
        {
            Vector2.up,
            Vector2.down,
            Vector2.left,
            Vector2.right
        };

        int maxSteps = Mathf.Max(1, Mathf.RoundToInt(visionDistance / tileSize));

        foreach (var dir in dirs)
        {
            for (int step = 1; step <= maxSteps; step++)
            {
                Vector2 tileCenter = rb.position + step * tileSize * dir;
                Gizmos.DrawWireCube(tileCenter, Vector2.one * (tileSize * 0.8f));
            }
        }
    }
}

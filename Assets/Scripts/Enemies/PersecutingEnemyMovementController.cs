using UnityEngine;

public class PersecutingEnemyMovementController : EnemyMovementController
{
    [Header("Vision (PoyoTank-like)")]
    [SerializeField, Min(0.1f)] protected float visionDistance = 10f;
    [SerializeField] protected LayerMask playerLayerMask;

    [Header("Vision Block (Under Player)")]
    [SerializeField] private LayerMask stageLayerMask;
    [SerializeField] private string destructiblesTag = "Destructibles";

    [Header("Vision Alignment")]
    [SerializeField, Min(0.001f)] private float alignedToleranceTiles = 0.15f;
    [SerializeField, Range(0.1f, 1f)] private float scanBoxSizePercent = 0.6f;

    protected override void Start()
    {
        base.Start();

        if (playerLayerMask.value == 0)
            playerLayerMask = LayerMask.GetMask("Player");

        if (stageLayerMask.value == 0)
            stageLayerMask = LayerMask.GetMask("Stage");
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

        int maxSteps = Mathf.Max(1, Mathf.FloorToInt(visionDistance / tileSize));

        float boxPercent = Mathf.Clamp(scanBoxSizePercent, 0.1f, 1f);
        Vector2 boxSize = Vector2.one * (tileSize * boxPercent);

        float alignedToleranceWorld = Mathf.Max(0.001f, alignedToleranceTiles * tileSize);

        Vector2 selfPos = rb.position;

        for (int i = 0; i < dirs.Length; i++)
        {
            var dir = dirs[i];
            bool verticalScan = dir == Vector2.up || dir == Vector2.down;

            for (int step = 1; step <= maxSteps; step++)
            {
                Vector2 tileCenter = selfPos + step * tileSize * dir;

                var hits = Physics2D.OverlapBoxAll(tileCenter, boxSize, 0f, playerLayerMask);

                if (hits != null && hits.Length > 0)
                {
                    for (int h = 0; h < hits.Length; h++)
                    {
                        var col = hits[h];
                        if (col == null)
                            continue;

                        Vector2 p = col.attachedRigidbody != null
                            ? col.attachedRigidbody.position
                            : (Vector2)col.transform.position;

                        bool aligned = verticalScan
                            ? Mathf.Abs(p.x - selfPos.x) <= alignedToleranceWorld
                            : Mathf.Abs(p.y - selfPos.y) <= alignedToleranceWorld;

                        if (!aligned)
                            continue;

                        if (IsPlayerStandingOnDestructibles(p))
                            continue;

                        dirToPlayer = dir;
                        return true;
                    }
                }

                if (IsTileBlocked(tileCenter))
                    break;
            }
        }

        return false;
    }

    private bool IsPlayerStandingOnDestructibles(Vector2 playerWorldPos)
    {
        if (stageLayerMask.value == 0)
            return false;

        Vector2 tileCenter = GetTileCenter(playerWorldPos);
        Vector2 size = Vector2.one * (tileSize * 0.8f);

        var hits = Physics2D.OverlapBoxAll(tileCenter, size, 0f, stageLayerMask);
        if (hits == null || hits.Length == 0)
            return false;

        for (int i = 0; i < hits.Length; i++)
        {
            var h = hits[i];
            if (h == null)
                continue;

            if (!string.IsNullOrEmpty(destructiblesTag) && h.CompareTag(destructiblesTag))
                return true;
        }

        return false;
    }

    private Vector2 GetTileCenter(Vector2 worldPos)
    {
        float x = Mathf.Round(worldPos.x / tileSize) * tileSize;
        float y = Mathf.Round(worldPos.y / tileSize) * tileSize;
        return new Vector2(x, y);
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

        int maxSteps = Mathf.Max(1, Mathf.FloorToInt(visionDistance / tileSize));
        float boxPercent = Mathf.Clamp(scanBoxSizePercent, 0.1f, 1f);
        Vector2 boxSize = Vector2.one * (tileSize * boxPercent);

        Vector2 selfPos = rb.position;

        foreach (var dir in dirs)
        {
            for (int step = 1; step <= maxSteps; step++)
            {
                Vector2 tileCenter = selfPos + step * tileSize * dir;
                Gizmos.DrawWireCube(tileCenter, boxSize);
            }
        }
    }
}
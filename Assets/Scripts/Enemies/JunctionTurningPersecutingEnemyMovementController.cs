using UnityEngine;

public sealed class JunctionTurningPersecutingEnemyMovementController : JunctionTurningEnemyMovementController
{
    [Header("Player Pursuit")]
    [SerializeField, Min(0.1f)] private float visionDistance = 10f;
    [SerializeField] private LayerMask playerLayerMask;

    [Header("Vision Block")]
    [SerializeField] private LayerMask stageLayerMask;
    [SerializeField] private string destructiblesTag = "Destructibles";
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
        if (TryGetPlayerDirection(out Vector2 playerDirection))
        {
            Vector2 forwardTile = rb.position + playerDirection * tileSize;
            if (!IsTileBlocked(forwardTile))
            {
                direction = playerDirection;
                UpdateSpriteDirection(direction);
                targetTile = forwardTile;
                return;
            }
        }

        base.DecideNextTile();
    }

    private bool TryGetPlayerDirection(out Vector2 directionToPlayer)
    {
        directionToPlayer = Vector2.zero;

        Vector2[] directions = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
        int maxSteps = Mathf.Max(1, Mathf.FloorToInt(visionDistance / tileSize));
        float boxSize = Mathf.Clamp(scanBoxSizePercent, 0.1f, 1f) * tileSize;
        float alignmentTolerance = Mathf.Max(0.001f, alignedToleranceTiles * tileSize);

        for (int directionIndex = 0; directionIndex < directions.Length; directionIndex++)
        {
            Vector2 scanDirection = directions[directionIndex];
            bool verticalScan = scanDirection == Vector2.up || scanDirection == Vector2.down;

            for (int step = 1; step <= maxSteps; step++)
            {
                Vector2 tileCenter = rb.position + step * tileSize * scanDirection;
                Collider2D[] hits = Physics2D.OverlapBoxAll(tileCenter, Vector2.one * boxSize, 0f, playerLayerMask);

                for (int hitIndex = 0; hitIndex < hits.Length; hitIndex++)
                {
                    Collider2D playerCollider = hits[hitIndex];
                    if (playerCollider == null)
                        continue;

                    Vector2 playerPosition = playerCollider.attachedRigidbody != null
                        ? playerCollider.attachedRigidbody.position
                        : (Vector2)playerCollider.transform.position;

                    bool aligned = verticalScan
                        ? Mathf.Abs(playerPosition.x - rb.position.x) <= alignmentTolerance
                        : Mathf.Abs(playerPosition.y - rb.position.y) <= alignmentTolerance;

                    if (!aligned || IsPlayerStandingOnDestructibles(playerPosition))
                        continue;

                    directionToPlayer = scanDirection;
                    return true;
                }

                if (IsTileBlocked(tileCenter))
                    break;
            }
        }

        return false;
    }

    private bool IsPlayerStandingOnDestructibles(Vector2 playerPosition)
    {
        if (stageLayerMask.value == 0)
            return false;

        Vector2 tileCenter = new(
            Mathf.Round(playerPosition.x / tileSize) * tileSize,
            Mathf.Round(playerPosition.y / tileSize) * tileSize);
        Collider2D[] hits = Physics2D.OverlapBoxAll(tileCenter, Vector2.one * (tileSize * 0.8f), 0f, stageLayerMask);

        for (int hitIndex = 0; hitIndex < hits.Length; hitIndex++)
        {
            if (hits[hitIndex] != null && hits[hitIndex].CompareTag(destructiblesTag))
                return true;
        }

        return false;
    }
}

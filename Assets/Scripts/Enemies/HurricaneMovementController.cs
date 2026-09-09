using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// A junction-turning enemy that can move through destructible blocks and
/// intermittently pulls aligned players while its cyclone is fully expanded.
/// </summary>
public sealed class HurricaneMovementController : JunctionTurningEnemyMovementController
{
    [Header("Destructibles Pass Through")]
    [SerializeField] private string destructiblesTag = "Destructibles";

    [Header("Hurricane Pull")]
    [SerializeField, Min(1)] private int visionTiles = 3;
    [SerializeField, Min(0.05f)] private float pullSpeedTilesPerSecond = 1f;
    [SerializeField] private LayerMask playerLayerMask;
    [SerializeField] private int[] activeMovementSequenceFrames = { 5, 6, 7, 8, 9 };

    private Collider2D selfCollider;
    private Tilemap destructibleTilemap;
    private Tilemap indestructibleTilemap;

    protected override void Awake()
    {
        if (!Application.isPlaying)
            return;

        base.Awake();

        selfCollider = GetComponent<Collider2D>();

        if (playerLayerMask.value == 0)
            playerLayerMask = LayerMask.GetMask("Player");

        ResolveTilemaps();
        IgnoreDestructibleCollisions();
    }

    protected override void FixedUpdate()
    {
        base.FixedUpdate();

        if (isDead || rb == null || !IsPullFrame())
            return;

        PullAlignedPlayers();
    }

    protected override bool IsTileBlocked(Vector2 tileCenter)
    {
        Vector2 size = Vector2.one * (tileSize * 0.8f);
        Collider2D[] hits = Physics2D.OverlapBoxAll(tileCenter, size, 0f, obstacleMask);

        foreach (Collider2D hit in hits)
        {
            if (hit == null || hit.gameObject == gameObject || hit.CompareTag(destructiblesTag))
                continue;

            return true;
        }

        return false;
    }

    private void IgnoreDestructibleCollisions()
    {
        if (selfCollider == null || string.IsNullOrEmpty(destructiblesTag))
            return;

        foreach (GameObject destructibles in GameObject.FindGameObjectsWithTag(destructiblesTag))
        {
            if (destructibles == null)
                continue;

            foreach (Collider2D destructibleCollider in destructibles.GetComponentsInChildren<Collider2D>(true))
            {
                if (destructibleCollider != null)
                    Physics2D.IgnoreCollision(selfCollider, destructibleCollider, true);
            }
        }
    }

    private bool IsPullFrame()
    {
        if (activeSprite == null || activeMovementSequenceFrames == null)
            return false;

        int currentFrame = activeSprite.CurrentFrame;
        for (int i = 0; i < activeMovementSequenceFrames.Length; i++)
        {
            if (currentFrame == activeMovementSequenceFrames[i])
                return true;
        }

        return false;
    }

    private void PullAlignedPlayers()
    {
        Collider2D[] playerHits = Physics2D.OverlapCircleAll(
            rb.position,
            visionTiles * tileSize,
            playerLayerMask);

        var pulledBodies = new HashSet<Rigidbody2D>();
        float alignmentTolerance = tileSize * 0.15f;
        const float stopDistance = 0.01f;

        for (int i = 0; i < playerHits.Length; i++)
        {
            Collider2D playerCollider = playerHits[i];

            if (playerCollider == null)
                continue;

            Rigidbody2D playerBody = playerCollider.attachedRigidbody;
            if (playerBody == null || !pulledBodies.Add(playerBody))
                continue;

            Vector2 offset = playerBody.position - rb.position;
            bool verticallyAligned = Mathf.Abs(offset.x) <= alignmentTolerance;
            bool horizontallyAligned = Mathf.Abs(offset.y) <= alignmentTolerance;
            float distance = verticallyAligned ? Mathf.Abs(offset.y) : Mathf.Abs(offset.x);

            if ((!verticallyAligned && !horizontallyAligned) || distance < stopDistance)
                continue;

            MovementController playerMovement = playerBody.GetComponent<MovementController>();
            float pullStep = pullSpeedTilesPerSecond * tileSize * Time.fixedDeltaTime;

            Vector2 playerDirection = playerMovement != null ? playerMovement.Direction : playerBody.linearVelocity.normalized;
            bool isFleeing = playerDirection != Vector2.zero &&
                            Vector2.Dot(playerDirection.normalized, offset.normalized) > 0.01f;

            if (isFleeing && playerMovement != null)
            {
                // Let the player move away at half of their own speed instead
                // of overwriting their movement target with a pull MovePosition.
                playerMovement.ApplyExternalMovementSpeedMultiplier(0.5f, Time.fixedDeltaTime * 2f);

                continue;
            }

            if (HasIndestructibleInPullPath(playerBody.position, rb.position))
                continue;

            Vector2 nextPosition = Vector2.MoveTowards(
                playerBody.position,
                rb.position,
                pullStep);

            if (IsPullDestinationBlocked(nextPosition))
                continue;

            playerBody.MovePosition(nextPosition);
        }
    }

    private bool HasIndestructibleInPullPath(Vector2 from, Vector2 to)
    {
        ResolveTilemaps();

        int steps = Mathf.CeilToInt(Vector2.Distance(from, to) / tileSize);
        for (int step = 1; step < steps; step++)
        {
            Vector2 checkedPosition = Vector2.Lerp(from, to, step / (float)steps);
            if (HasTileAt(indestructibleTilemap, checkedPosition))
                return true;
        }

        return false;
    }

    private bool IsPullDestinationBlocked(Vector2 worldPosition)
    {
        ResolveTilemaps();

        return HasTileAt(destructibleTilemap, worldPosition) ||
               HasTileAt(indestructibleTilemap, worldPosition);
    }

    private void ResolveTilemaps()
    {
        GameManager gameManager = GameManager.Instance != null
            ? GameManager.Instance
            : FindAnyObjectByType<GameManager>();

        if (gameManager == null)
            return;

        destructibleTilemap ??= gameManager.destructibleTilemap;
        indestructibleTilemap ??= gameManager.indestructibleTilemap;
    }

    private static bool HasTileAt(Tilemap tilemap, Vector2 worldPosition)
        => tilemap != null && tilemap.HasTile(tilemap.WorldToCell(worldPosition));
}

using UnityEngine;

/// <summary>
/// Walks through junctions and fires an ice projectile at a player visible in
/// a cardinal lane.
/// </summary>
public sealed class IcicleMovementController : JunctionTurningEnemyMovementController
{
    [Header("Ice Shot")]
    [SerializeField, Min(0.1f)] private float shotCooldownSeconds = 2f;
    [SerializeField, Min(0.05f)] private float scanIntervalSeconds = 0.1f;
    [SerializeField, Min(1)] private int visionTiles = 8;
    [SerializeField, Range(0.1f, 1f)] private float scanBoxSizePercent = 0.7f;
    [SerializeField] private Sprite projectileSprite;
    [SerializeField] private Sprite particleSpriteOne;
    [SerializeField] private Sprite particleSpriteTwo;

    private float nextScanTime;
    private float nextShotTime;

    private void Update()
    {
        if (isDead || Time.time < nextScanTime || Time.time < nextShotTime)
            return;

        nextScanTime = Time.time + scanIntervalSeconds;
        if (!TryGetPlayerDirection(out Vector2 shotDirection))
            return;

        Vector2 spawnPosition = (Vector2)transform.position + shotDirection * tileSize;
        IcicleIceProjectile.Create(spawnPosition, shotDirection, gameObject, projectileSprite, particleSpriteOne, particleSpriteTwo);
        nextShotTime = Time.time + shotCooldownSeconds;
    }

    private bool TryGetPlayerDirection(out Vector2 targetDirection)
    {
        Vector2[] directions = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
        Vector2 scanSize = Vector2.one * (tileSize * scanBoxSizePercent);
        Vector2 origin = transform.position;

        for (int directionIndex = 0; directionIndex < directions.Length; directionIndex++)
        {
            Vector2 scanDirection = directions[directionIndex];
            for (int step = 1; step <= visionTiles; step++)
            {
                Vector2 tileCenter = origin + scanDirection * tileSize * step;
                Collider2D[] hits = Physics2D.OverlapBoxAll(tileCenter, scanSize, 0f);
                bool blocked = false;

                for (int hitIndex = 0; hitIndex < hits.Length; hitIndex++)
                {
                    Collider2D hit = hits[hitIndex];
                    if (hit == null || hit.transform.IsChildOf(transform))
                        continue;

                    if (hit.GetComponentInParent<PlayerIdentity>() != null)
                    {
                        targetDirection = scanDirection;
                        return true;
                    }

                    if (hit.GetComponentInParent<Bomb>() != null || hit.gameObject.layer == LayerMask.NameToLayer("Stage"))
                        blocked = true;
                }

                if (blocked)
                    break;
            }
        }

        targetDirection = Vector2.zero;
        return false;
    }
}

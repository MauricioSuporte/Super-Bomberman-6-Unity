using UnityEngine;

/// <summary>Flying junction-turner that crosses destructibles and fires on players in sight.</summary>
public sealed class HeliMovementController : FlyMovimentController
{
    [Header("Heli Shot")]
    [SerializeField, Min(0.1f)] private float shotCooldownSeconds = 2f;
    [SerializeField, Min(1)] private int visionTiles = 8;
    [SerializeField] private Sprite projectileSprite;
    [SerializeField] private Sprite projectileImpactSprite;
    [SerializeField] private string destructibleTag = "Destructibles";

    private float nextShotTime;
    private void Update()
    {
        if (isDead || Time.time < nextShotTime || !TryGetPlayerDirection(out Vector2 shotDirection))
            return;

        HeliProjectile.Create(rb.position + shotDirection * tileSize, shotDirection, gameObject, projectileSprite, projectileImpactSprite);
        nextShotTime = Time.time + shotCooldownSeconds;
    }

    private bool TryGetPlayerDirection(out Vector2 targetDirection)
    {
        Vector2[] directions = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
        for (int directionIndex = 0; directionIndex < directions.Length; directionIndex++)
        {
            Vector2 scanDirection = directions[directionIndex];
            for (int step = 1; step <= visionTiles; step++)
            {
                Vector2 tile = rb.position + scanDirection * tileSize * step;
                Collider2D[] hits = Physics2D.OverlapBoxAll(tile, Vector2.one * (tileSize * 0.7f), 0f);
                bool blocked = false;
                for (int hitIndex = 0; hitIndex < hits.Length; hitIndex++)
                {
                    Collider2D hit = hits[hitIndex];
                    if (hit == null || hit.transform.IsChildOf(transform)) continue;
                    if (hit.GetComponentInParent<PlayerIdentity>() != null) { targetDirection = scanDirection; return true; }
                    if (hit.GetComponentInParent<Bomb>() != null || (hit.gameObject.layer == LayerMask.NameToLayer("Stage") && !IsDestructible(hit))) blocked = true;
                }
                if (blocked) break;
            }
        }
        targetDirection = Vector2.zero;
        return false;
    }

    private bool IsDestructible(Collider2D hit)
    {
        for (Transform current = hit.transform; current != null; current = current.parent)
            if (current.CompareTag(destructibleTag)) return true;
        return false;
    }
}

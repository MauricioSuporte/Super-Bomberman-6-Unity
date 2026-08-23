using UnityEngine;

public sealed class JellyFishAttackController : MonoBehaviour
{
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField, Min(0.1f)] private float cooldownSeconds = 10f;
    [SerializeField, Min(0.1f)] private float scanIntervalSeconds = 0.15f;
    [SerializeField, Min(1)] private int visionTiles = 8;
    [SerializeField, Min(0.1f)] private float tileSize = 1f;
    [SerializeField, Range(0.1f, 1f)] private float scanBoxSizePercent = 0.7f;
    [SerializeField] private LayerMask playerLayerMask;
    [SerializeField] private LayerMask bombLayerMask;
    [SerializeField] private LayerMask obstacleLayerMask;
    [SerializeField] private Vector2 projectileLocalOffset;

    private float nextScanTime;
    private float nextShotTime;
    private CharacterHealth health;

    private void Awake()
    {
        health = GetComponent<CharacterHealth>();
    }

    private void Update()
    {
        if (projectilePrefab == null || (health != null && health.life <= 0))
            return;

        if (Time.time < nextScanTime || Time.time < nextShotTime)
            return;

        nextScanTime = Time.time + scanIntervalSeconds;
        if (!TryGetTargetDirection(out Vector2 direction))
            return;

        Vector2 spawnPosition = (Vector2)transform.position + direction * tileSize + projectileLocalOffset;
        GameObject projectile = Instantiate(projectilePrefab, spawnPosition, Quaternion.identity);
        if (projectile.TryGetComponent(out JellyFishShot shot))
            shot.Init(direction, gameObject);

        nextShotTime = Time.time + cooldownSeconds;
    }

    private bool TryGetTargetDirection(out Vector2 targetDirection)
    {
        Vector2[] directions = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
        Vector2 scanSize = Vector2.one * (tileSize * scanBoxSizePercent);
        Vector2 origin = transform.position;

        for (int directionIndex = 0; directionIndex < directions.Length; directionIndex++)
        {
            Vector2 direction = directions[directionIndex];
            for (int step = 1; step <= visionTiles; step++)
            {
                Vector2 tileCenter = origin + direction * tileSize * step;
                Collider2D[] hits = Physics2D.OverlapBoxAll(tileCenter, scanSize, 0f);

                bool blocked = false;
                for (int hitIndex = 0; hitIndex < hits.Length; hitIndex++)
                {
                    Collider2D hit = hits[hitIndex];
                    if (hit == null || hit.transform.IsChildOf(transform))
                        continue;

                    int layerBit = 1 << hit.gameObject.layer;
                    if ((playerLayerMask.value & layerBit) != 0 && hit.GetComponentInParent<PlayerIdentity>() != null)
                    {
                        targetDirection = direction;
                        return true;
                    }

                    if ((bombLayerMask.value & layerBit) != 0 && hit.GetComponentInParent<Bomb>() != null)
                    {
                        targetDirection = direction;
                        return true;
                    }

                    if ((obstacleLayerMask.value & layerBit) != 0)
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

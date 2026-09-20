using UnityEngine;

/// <summary>
/// A junction-turning Hogera that releases four Mini Hogerás after its death
/// animation has completed.
/// </summary>
public sealed class HogeraMovementController : JunctionTurningEnemyMovementController
{
    private static readonly Vector2[] MiniHogeraSpawnDirections =
    {
        Vector2.up,
        Vector2.right,
        Vector2.down,
        Vector2.left
    };

    [Header("Mini Hogera Spawn")]
    [SerializeField] private GameObject miniHogeraPrefab;
    [SerializeField, Min(1)] private int miniHogeraCount = 4;

    private Vector2 deathTile;
    private bool hasDeathTile;
    private bool spawnedMiniHogeras;

    protected override void Die()
    {
        if (isDead)
            return;

        deathTile = rb != null ? rb.position : (Vector2)transform.position;
        deathTile.x = Mathf.Round(deathTile.x / tileSize) * tileSize;
        deathTile.y = Mathf.Round(deathTile.y / tileSize) * tileSize;
        hasDeathTile = true;

        base.Die();
    }

    protected override void OnDeathAnimationEnded()
    {
        SpawnMiniHogeras();
        base.OnDeathAnimationEnded();
    }

    private void SpawnMiniHogeras()
    {
        if (spawnedMiniHogeras || miniHogeraPrefab == null)
            return;

        spawnedMiniHogeras = true;
        Vector3 spawnPosition = hasDeathTile
            ? new Vector3(deathTile.x, deathTile.y, transform.position.z)
            : transform.position;

        for (int i = 0; i < miniHogeraCount; i++)
        {
            GameObject miniHogera = Instantiate(miniHogeraPrefab, spawnPosition, Quaternion.identity);
            if (miniHogera.TryGetComponent(out MiniHogeraMovementController movement))
                movement.SetSpawnDirection(MiniHogeraSpawnDirections[i % MiniHogeraSpawnDirections.Length]);
        }

        GameManager gameManager = FindAnyObjectByType<GameManager>();
        if (gameManager != null)
            gameManager.NotifyEnemySpawned(miniHogeraCount);
    }
}

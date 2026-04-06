using System.Collections;
using UnityEngine;

public sealed class EnemySpawnDestructibleTileHandler : MonoBehaviour, IDestructibleTileHandler
{
    [Header("Enemy Spawn")]
    [SerializeField] private GameObject enemyPrefab;

    [Header("Timing")]
    [SerializeField, Min(0f)] private float spawnDelaySeconds = 0.5f;

    [Header("Spawn Invincibility (Real)")]
    private readonly float invincibleSeconds = 2f;
    [SerializeField, Min(0.01f)] private float blinkIntervalSeconds = 0.12f;

    private GameManager _gm;

    void Awake()
    {
        _gm = FindFirstObjectByType<GameManager>();
    }

    public bool HandleHit(BombController source, Vector2 worldPos, Vector3Int cell)
    {
        if (source == null)
            return false;

        source.ClearDestructibleForEffect(
            worldPos,
            spawnDestructiblePrefab: true,
            spawnHiddenObject: false
        );

        if (enemyPrefab == null)
        {
            if (_gm != null)
                _gm.NotifyHiddenEnemyCancelledFromBlock(1);

            return true;
        }

        StartCoroutine(SpawnEnemyAfterDelayRoutine(worldPos));
        return true;
    }

    private IEnumerator SpawnEnemyAfterDelayRoutine(Vector2 worldPos)
    {
        if (spawnDelaySeconds > 0f)
            yield return new WaitForSeconds(spawnDelaySeconds);

        Vector2 p = worldPos;
        p.x = Mathf.Round(p.x);
        p.y = Mathf.Round(p.y);

        GameObject enemyGo = Instantiate(enemyPrefab, p, Quaternion.identity);

        if (_gm != null)
            _gm.NotifyHiddenEnemySpawnedFromBlock(1);

        if (enemyGo == null)
            yield break;

        if (invincibleSeconds > 0f && enemyGo.TryGetComponent<CharacterHealth>(out var h))
        {
            h.StartSpawnInvulnerability(invincibleSeconds, blinkIntervalSeconds);
        }

        StartCoroutine(DisableEnemyContactDamage(enemyGo, invincibleSeconds));
    }

    private IEnumerator DisableEnemyContactDamage(GameObject enemy, float duration)
    {
        if (enemy == null)
            yield break;

        
        if (enemy.TryGetComponent<Collider2D>(out var col))
            col.enabled = false;

        yield return new WaitForSeconds(duration);

        if (col != null)
            col.enabled = true;
    }
}
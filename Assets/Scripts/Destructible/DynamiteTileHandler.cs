using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class DynamiteTileHandler : MonoBehaviour, IDestructibleTileHandler
{
    [SerializeField] private int explosionRadius = 2;
    [SerializeField] private float startStepDelaySeconds = 0.1f;

    readonly HashSet<Vector3Int> _triggered = new();
    readonly HashSet<Vector3Int> _scheduled = new();

    AudioSource _audio;

    void Awake()
    {
        _audio = GetComponent<AudioSource>();
        if (_audio == null)
            _audio = gameObject.AddComponent<AudioSource>();

        _audio.playOnAwake = false;
        _audio.loop = false;
    }

    public bool HandleHit(BombController source, Vector2 worldPos, Vector3Int cell)
    {
        if (source == null)
            return false;

        if (!_triggered.Add(cell))
            return true;

        if (!_scheduled.Add(cell))
            return true;

        source.ClearDestructibleForEffect(worldPos, spawnDestructiblePrefab: false, spawnHiddenObject: false);

        Vector2 p = worldPos;
        p.x = Mathf.Round(p.x);
        p.y = Mathf.Round(p.y);

        StartCoroutine(DetonateRoutine(source, p, cell));
        return true;
    }

    IEnumerator DetonateRoutine(BombController source, Vector2 origin, Vector3Int cell)
    {
        if (startStepDelaySeconds > 0f)
            yield return new WaitForSeconds(startStepDelaySeconds);

        SpawnStartAndExplode(source, origin);

        if (startStepDelaySeconds > 0f)
            yield return new WaitForSeconds(startStepDelaySeconds);

        SpawnSecondaryBranches(source, origin);

        _scheduled.Remove(cell);
    }

    void SpawnStartAndExplode(BombController source, Vector2 p)
    {
        source.PlayExplosionSfxExclusive(_audio, explosionRadius);
        source.SpawnExplosionCrossForEffect(p, explosionRadius, pierce: true);
    }

    void SpawnSecondaryBranches(BombController source, Vector2 origin)
    {
        SpawnSecondaryBranch(source, origin, Vector2.up);
        SpawnSecondaryBranch(source, origin, Vector2.down);
        SpawnSecondaryBranch(source, origin, Vector2.left);
        SpawnSecondaryBranch(source, origin, Vector2.right);
    }

    void SpawnSecondaryBranch(BombController source, Vector2 origin, Vector2 dir)
    {
        Vector2 branchOrigin = origin + dir * explosionRadius;
        branchOrigin.x = Mathf.Round(branchOrigin.x);
        branchOrigin.y = Mathf.Round(branchOrigin.y);

        if (!source.CanSpawnTileEffectAt(branchOrigin))
            return;

        source.PlayExplosionSfxExclusive(_audio, explosionRadius);
        source.SpawnExplosionCrossForEffect(branchOrigin, explosionRadius, pierce: true);
    }
}

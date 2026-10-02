using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class ExplosionDestructibleTileHandler : MonoBehaviour, IDestructibleTileHandler
{
    private const int ExplosionRadius = 5;
    private const float ChainExplosionDelaySeconds = 0.1f;

    private readonly HashSet<Vector3Int> scheduledCells = new();
    private AudioSource explosionAudio;

    private void Awake()
    {
        explosionAudio = GetComponent<AudioSource>();
        if (explosionAudio == null)
            explosionAudio = gameObject.AddComponent<AudioSource>();

        explosionAudio.playOnAwake = false;
        explosionAudio.loop = false;
    }

    public bool HandleHit(BombController source, Vector2 worldPos, Vector3Int cell)
    {
        if (source == null || source.destructibleTiles == null)
            return false;

        if (!source.destructibleTiles.HasTile(cell))
            return true;

        if (!scheduledCells.Add(cell))
            return true;

        Vector2 origin = source.destructibleTiles.GetCellCenterWorld(cell);
        StartCoroutine(ExplodeAfterDelay(source, origin, cell));
        return true;
    }

    private IEnumerator ExplodeAfterDelay(BombController source, Vector2 origin, Vector3Int cell)
    {
        yield return new WaitForSeconds(ChainExplosionDelaySeconds);

        scheduledCells.Remove(cell);
        if (source == null || source.destructibleTiles == null || !source.destructibleTiles.HasTile(cell))
            yield break;

        source.ClearDestructibleForEffect(
            origin,
            spawnDestructiblePrefab: false,
            spawnHiddenObject: false);
        source.PlayExplosionSfxExclusive(explosionAudio, ExplosionRadius, pierce: true);
        source.SpawnExplosionCrossForEffect(origin, ExplosionRadius, pierce: true);
    }
}

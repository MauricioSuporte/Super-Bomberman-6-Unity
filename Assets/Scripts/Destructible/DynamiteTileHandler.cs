using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class DynamiteTileHandler : MonoBehaviour, IDestructibleTileHandler
{
    private const string DynamiteExplosionSfxResourcesPath = "Sounds/Explosions/Dynamite";

    [SerializeField] private int explosionRadius = 2;
    [SerializeField] private float startStepDelaySeconds = 0.1f;

    readonly HashSet<Vector3Int> _triggered = new();
    readonly HashSet<Vector3Int> _scheduled = new();

    static AudioClip _dynamiteExplosionSfx;
    AudioSource _audio;

    void Awake()
    {
        if (_dynamiteExplosionSfx == null)
            _dynamiteExplosionSfx = Resources.Load<AudioClip>(DynamiteExplosionSfxResourcesPath);

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

        Vector2 p = GetGroundTileCenter(source, worldPos);

        StartCoroutine(DetonateRoutine(source, p, cell));
        return true;
    }

    IEnumerator DetonateRoutine(BombController source, Vector2 origin, Vector3Int cell)
    {
        if (startStepDelaySeconds > 0f)
            yield return new WaitForSeconds(startStepDelaySeconds);

        SpawnStartAndExplode(source, origin);

        _scheduled.Remove(cell);
    }

    void SpawnStartAndExplode(BombController source, Vector2 p)
    {
        source.PlayExplosionSfxExclusive(_audio, _dynamiteExplosionSfx);
        source.SpawnExplosionAreaForEffect(p, explosionRadius, pierce: true);
    }

    static Vector2 GetGroundTileCenter(BombController source, Vector2 worldPos)
    {
        Tilemap ground = source != null ? source.groundTiles : null;
        if (ground == null)
            return RoundToWholeTile(worldPos);

        Vector3Int cell = ground.WorldToCell(worldPos);
        return ground.GetCellCenterWorld(cell);
    }

    static Vector2 RoundToWholeTile(Vector2 worldPos)
    {
        worldPos.x = Mathf.Round(worldPos.x);
        worldPos.y = Mathf.Round(worldPos.y);
        return worldPos;
    }
}

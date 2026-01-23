using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class BombController
{
    [Header("Dynamite")]
    [SerializeField] private int dynamiteExplosionRadius = 2;
    [SerializeField] private float dynamiteStartStepDelaySeconds = 0.1f;

    private readonly HashSet<Vector3Int> triggeredDynamites = new();
    private readonly HashSet<Vector3Int> scheduledDynamiteStarts = new();

    private AudioSource dynamiteExplosionAudio;

    private void InitDynamite()
    {
        if (dynamiteExplosionAudio != null)
            return;

        dynamiteExplosionAudio = gameObject.AddComponent<AudioSource>();
        dynamiteExplosionAudio.playOnAwake = false;
        dynamiteExplosionAudio.loop = false;
    }

    private bool IsDynamiteAt(Vector2 worldPos)
    {
        if (_gm == null)
            _gm = FindFirstObjectByType<GameManager>();

        if (_gm == null)
            return false;

        if (!TryGetDestructibleTileAt(worldPos, out _, out var tile))
            return false;

        return _gm.IsDynamiteTile(tile);
    }

    private void DetonateDynamite(Vector2 worldPos)
    {
        if (destructibleTiles == null)
            return;

        if (!TryGetDestructibleTileAt(worldPos, out var cell, out _))
            return;

        if (!triggeredDynamites.Add(cell))
            return;

        if (!scheduledDynamiteStarts.Add(cell))
            return;

        ClearDestructible(worldPos, spawnDestructiblePrefab: false, spawnHiddenObject: false);

        Vector2 p = worldPos;
        p.x = Mathf.Round(p.x);
        p.y = Mathf.Round(p.y);

        StartCoroutine(DetonateDynamiteRoutine(p, cell));
    }

    private bool HasGroundAt(Vector2 worldPos)
    {
        if (groundTiles == null)
            return false;

        Vector3Int cell = groundTiles.WorldToCell(worldPos);
        return groundTiles.GetTile(cell) != null;
    }

    private bool CanSpawnDynamiteBranchAt(Vector2 worldPos)
    {
        if (!HasGroundAt(worldPos))
            return false;

        if (HasIndestructibleAt(worldPos))
            return false;

        return true;
    }

    private IEnumerator DetonateDynamiteRoutine(Vector2 origin, Vector3Int cell)
    {
        if (dynamiteStartStepDelaySeconds > 0f)
            yield return new WaitForSeconds(dynamiteStartStepDelaySeconds);

        SpawnDynamiteStartAndExplode(origin);

        if (dynamiteStartStepDelaySeconds > 0f)
            yield return new WaitForSeconds(dynamiteStartStepDelaySeconds);

        SpawnDynamiteSecondaryBranches(origin);

        scheduledDynamiteStarts.Remove(cell);
    }

    private void SpawnDynamiteStartAndExplode(Vector2 p)
    {
        PlayDynamiteExplosionSfx(dynamiteExplosionRadius);

        Explosion center = Instantiate(explosionPrefab, p, Quaternion.identity);
        center.Play(Explosion.ExplosionPart.Start, Vector2.zero, 0f, explosionDuration, p);

        Explode(p, Vector2.up, dynamiteExplosionRadius, pierce: true);
        Explode(p, Vector2.down, dynamiteExplosionRadius, pierce: true);
        Explode(p, Vector2.left, dynamiteExplosionRadius, pierce: true);
        Explode(p, Vector2.right, dynamiteExplosionRadius, pierce: true);
    }

    private void SpawnDynamiteSecondaryBranches(Vector2 origin)
    {
        SpawnDynamiteSecondaryBranch(origin, Vector2.up);
        SpawnDynamiteSecondaryBranch(origin, Vector2.down);
        SpawnDynamiteSecondaryBranch(origin, Vector2.left);
        SpawnDynamiteSecondaryBranch(origin, Vector2.right);
    }

    private void SpawnDynamiteSecondaryBranch(Vector2 origin, Vector2 dir)
    {
        Vector2 branchOrigin = origin + dir * dynamiteExplosionRadius;
        branchOrigin.x = Mathf.Round(branchOrigin.x);
        branchOrigin.y = Mathf.Round(branchOrigin.y);

        if (!CanSpawnDynamiteBranchAt(branchOrigin))
            return;

        PlayDynamiteExplosionSfx(dynamiteExplosionRadius);

        Explosion start = Instantiate(explosionPrefab, branchOrigin, Quaternion.identity);
        start.Play(Explosion.ExplosionPart.Start, Vector2.zero, 0f, explosionDuration, branchOrigin);

        Explode(branchOrigin, Vector2.up, dynamiteExplosionRadius, pierce: true);
        Explode(branchOrigin, Vector2.down, dynamiteExplosionRadius, pierce: true);
        Explode(branchOrigin, Vector2.left, dynamiteExplosionRadius, pierce: true);
        Explode(branchOrigin, Vector2.right, dynamiteExplosionRadius, pierce: true);
    }

    private void PlayDynamiteExplosionSfx(int radius)
    {
        if (dynamiteExplosionAudio == null)
            InitDynamite();

        if (dynamiteExplosionAudio == null || explosionSfxByRadius == null || explosionSfxByRadius.Length == 0)
            return;

        int index = Mathf.Clamp(radius - 1, 0, explosionSfxByRadius.Length - 1);
        AudioClip clip = explosionSfxByRadius[index];
        if (clip == null)
            return;

        if (currentExplosionAudio != null && currentExplosionAudio.isPlaying)
            currentExplosionAudio.Stop();

        currentExplosionAudio = dynamiteExplosionAudio;
        currentExplosionAudio.PlayOneShot(clip, explosionSfxVolume);
    }
}

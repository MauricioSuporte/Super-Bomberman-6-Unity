using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class ResurrectingAnimatedTileHandler : MonoBehaviour, IDestructibleTileHandler
{
    [Header("Overrides (optional)")]
    [SerializeField] private Tilemap destructibleTilemapOverride;
    [SerializeField] private Tilemap groundTilemapOverride;

    private readonly Dictionary<Vector3Int, Coroutine> _pending = new();
    private static readonly Collider2D[] _overlapBuffer = new Collider2D[32];

    public bool HandleHit(BombController source, Vector2 worldPos, Vector3Int cell)
    {
        if (source == null)
            return false;

        Tilemap destructTm = destructibleTilemapOverride != null
            ? destructibleTilemapOverride
            : source.destructibleTiles;

        if (destructTm == null)
            return false;

        TileBase tileBase = destructTm.GetTile(cell);
        if (tileBase is not ResurrectingAnimatedTile tile)
            return false;

        source.ClearDestructibleForEffect(worldPos);

        Tilemap groundTm = groundTilemapOverride != null
            ? groundTilemapOverride
            : source.groundTiles;

        ScheduleRespawn(destructTm, groundTm, cell, tile);
        return true;
    }

    private void ScheduleRespawn(Tilemap destructTm, Tilemap groundTm, Vector3Int cell, ResurrectingAnimatedTile tile)
    {
        if (_pending.TryGetValue(cell, out var c) && c != null)
            StopCoroutine(c);

        _pending[cell] = StartCoroutine(RespawnRoutine(destructTm, groundTm, cell, tile));
    }

    private IEnumerator RespawnRoutine(Tilemap destructTm, Tilemap groundTm, Vector3Int cell, ResurrectingAnimatedTile tile)
    {
        float waitRespawn = Mathf.Max(0.01f, tile.respawnSeconds);

        bool useGround = tile.renderRespawnOnGround && groundTm != null;
        TileBase groundOriginal = null;

        if (useGround)
            groundOriginal = groundTm.GetTile(cell);

        yield return new WaitForSeconds(waitRespawn);

        float retry = Mathf.Max(0.02f, tile.retryCheckSeconds);

        while (true)
        {
            if (destructTm == null)
                yield break;

            if (destructTm.GetTile(cell) != null)
                yield break;

            Vector2 center = destructTm.GetCellCenterWorld(cell);

            if (IsOccupied(center, tile.overlapBoxSize))
            {
                yield return new WaitForSeconds(retry);
                continue;
            }

            float warnSeconds = Mathf.Max(0.01f, tile.preRespawnWarningSeconds);

            if (tile.preRespawnWarningTile != null && warnSeconds > 0f)
            {
                if (useGround)
                {
                    if (groundTm == null)
                        yield break;

                    groundTm.SetTile(cell, tile.preRespawnWarningTile);
                    groundTm.RefreshTile(cell);
                }
                else
                {
                    destructTm.SetTile(cell, tile.preRespawnWarningTile);
                    destructTm.RefreshTile(cell);
                }

                yield return new WaitForSeconds(warnSeconds);

                if (destructTm == null)
                    yield break;

                if (destructTm.GetTile(cell) != null)
                {
                    if (useGround)
                        RestoreGround(groundTm, cell, groundOriginal, tile);
                    yield break;
                }

                if (useGround)
                {
                    if (groundTm == null)
                        yield break;

                    if (groundTm.GetTile(cell) != tile.preRespawnWarningTile)
                        yield break;

                    groundTm.SetTile(cell, groundOriginal);
                    groundTm.RefreshTile(cell);
                }
                else
                {
                    if (destructTm.GetTile(cell) != tile.preRespawnWarningTile)
                        yield break;

                    destructTm.SetTile(cell, null);
                    destructTm.RefreshTile(cell);
                }
            }

            center = destructTm.GetCellCenterWorld(cell);
            if (IsOccupied(center, tile.overlapBoxSize))
            {
                yield return new WaitForSeconds(retry);
                continue;
            }

            if (destructTm.GetTile(cell) != null)
            {
                if (useGround)
                    RestoreGround(groundTm, cell, groundOriginal, tile);
                yield break;
            }

            if (tile.respawnAnimationTile != null)
            {
                if (useGround)
                {
                    if (groundTm == null)
                        yield break;

                    groundTm.SetTile(cell, tile.respawnAnimationTile);
                    groundTm.RefreshTile(cell);
                }
                else
                {
                    destructTm.SetTile(cell, tile.respawnAnimationTile);
                    destructTm.RefreshTile(cell);
                }

                float animDur = Mathf.Max(0.01f, tile.respawnAnimationDuration);
                yield return new WaitForSeconds(animDur);

                if (destructTm == null)
                    yield break;

                if (destructTm.GetTile(cell) != null)
                {
                    if (useGround)
                        RestoreGround(groundTm, cell, groundOriginal, tile);
                    yield break;
                }

                if (useGround)
                {
                    if (groundTm == null)
                        yield break;

                    if (groundTm.GetTile(cell) != tile.respawnAnimationTile)
                        yield break;

                    groundTm.SetTile(cell, groundOriginal);
                    groundTm.RefreshTile(cell);
                }
                else
                {
                    if (destructTm.GetTile(cell) != tile.respawnAnimationTile)
                        yield break;

                    destructTm.SetTile(cell, null);
                    destructTm.RefreshTile(cell);
                }
            }

            center = destructTm.GetCellCenterWorld(cell);
            if (IsOccupied(center, tile.overlapBoxSize))
            {
                yield return new WaitForSeconds(retry);
                continue;
            }

            if (destructTm.GetTile(cell) != null)
            {
                if (useGround)
                    RestoreGround(groundTm, cell, groundOriginal, tile);
                yield break;
            }

            if (useGround)
                RestoreGround(groundTm, cell, groundOriginal, tile);

            destructTm.SetTile(cell, tile);
            destructTm.RefreshTile(cell);
            yield break;
        }
    }

    private static void RestoreGround(Tilemap groundTm, Vector3Int cell, TileBase original, ResurrectingAnimatedTile tile)
    {
        if (groundTm == null)
            return;

        if (!tile.renderRespawnOnGround)
            return;

        groundTm.SetTile(cell, original);
        groundTm.RefreshTile(cell);
    }

    private bool IsOccupied(Vector2 worldCenter, float size)
    {
        int mask = LayerMask.GetMask("Louie", "Player", "Bomb", "Explosion", "Item", "Enemy");

        var filter = new ContactFilter2D
        {
            useLayerMask = true,
            useTriggers = true
        };
        filter.SetLayerMask(mask);

        float s = Mathf.Max(0.05f, size);

        int count = Physics2D.OverlapBox(worldCenter, Vector2.one * s, 0f, filter, _overlapBuffer);

        for (int i = 0; i < count; i++)
        {
            var c = _overlapBuffer[i];
            _overlapBuffer[i] = null;

            if (c != null)
                return true;
        }

        return false;
    }

    private void OnDisable()
    {
        foreach (var kv in _pending)
            if (kv.Value != null)
                StopCoroutine(kv.Value);

        _pending.Clear();
    }
}
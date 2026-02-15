using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class ResurrectingAnimatedTileHandler : MonoBehaviour, IDestructibleTileHandler
{
    [SerializeField] private Tilemap destructibleTilemapOverride;

    private readonly Dictionary<Vector3Int, Coroutine> _pending = new();
    private static readonly Collider2D[] _overlapBuffer = new Collider2D[32];

    public bool HandleHit(BombController source, Vector2 worldPos, Vector3Int cell)
    {
        Tilemap tm = destructibleTilemapOverride != null
            ? destructibleTilemapOverride
            : source.destructibleTiles;

        if (tm == null)
            return false;

        TileBase tileBase = tm.GetTile(cell);
        if (tileBase is not ResurrectingAnimatedTile tile)
            return false;

        source.ClearDestructibleForEffect(worldPos);
        ScheduleRespawn(tm, cell, tile);
        return true;
    }

    private void ScheduleRespawn(Tilemap tm, Vector3Int cell, ResurrectingAnimatedTile tile)
    {
        if (_pending.TryGetValue(cell, out var c) && c != null)
            StopCoroutine(c);

        _pending[cell] = StartCoroutine(RespawnRoutine(tm, cell, tile));
    }

    private IEnumerator RespawnRoutine(Tilemap tm, Vector3Int cell, ResurrectingAnimatedTile tile)
    {
        yield return new WaitForSeconds(Mathf.Max(0.01f, tile.respawnSeconds));

        float retry = Mathf.Max(0.02f, tile.retryCheckSeconds);

        while (true)
        {
            if (tm == null)
                yield break;

            if (tm.GetTile(cell) != null)
                yield break;

            Vector2 center = tm.GetCellCenterWorld(cell);

            if (IsOccupied(center, tile.overlapBoxSize))
            {
                yield return new WaitForSeconds(retry);
                continue;
            }

            float warnSeconds = Mathf.Max(0.01f, tile.preRespawnWarningSeconds);

            if (tile.preRespawnWarningTile != null && warnSeconds > 0f)
            {
                tm.SetTile(cell, tile.preRespawnWarningTile);
                tm.RefreshTile(cell);

                yield return new WaitForSeconds(warnSeconds);

                if (tm == null)
                    yield break;

                if (tm.GetTile(cell) != tile.preRespawnWarningTile)
                    yield break;

                tm.SetTile(cell, null);
                tm.RefreshTile(cell);
            }

            center = tm.GetCellCenterWorld(cell);
            if (IsOccupied(center, tile.overlapBoxSize))
            {
                yield return new WaitForSeconds(retry);
                continue;
            }

            if (tile.respawnAnimationTile != null)
            {
                tm.SetTile(cell, tile.respawnAnimationTile);
                tm.RefreshTile(cell);

                float animDur = Mathf.Max(0.01f, tile.respawnAnimationDuration);
                yield return new WaitForSeconds(animDur);

                if (tm == null)
                    yield break;

                if (tm.GetTile(cell) != tile.respawnAnimationTile)
                    yield break;

                tm.SetTile(cell, null);
                tm.RefreshTile(cell);
            }

            center = tm.GetCellCenterWorld(cell);
            if (IsOccupied(center, tile.overlapBoxSize))
            {
                yield return new WaitForSeconds(retry);
                continue;
            }

            tm.SetTile(cell, tile);
            tm.RefreshTile(cell);
            yield break;
        }
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

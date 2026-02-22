using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class ResurrectingAnimatedTileProgressHandler : MonoBehaviour, IDestructibleTileHandler
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
        if (tileBase is not ResurrectingAnimatedTileProgress tile)
            return false;

        source.ClearDestructibleForEffect(worldPos);

        ScheduleRespawn(tm, cell, tile);
        return true;
    }

    private void ScheduleRespawn(Tilemap tm, Vector3Int cell, ResurrectingAnimatedTileProgress tile)
    {
        if (_pending.TryGetValue(cell, out var c) && c != null)
            StopCoroutine(c);

        _pending[cell] = StartCoroutine(RespawnRoutine(tm, cell, tile));
    }

    private IEnumerator RespawnRoutine(Tilemap tm, Vector3Int cell, ResurrectingAnimatedTileProgress tile)
    {
        float destroyAnim = Mathf.Max(0.01f, tile.destructionAnimationSeconds);
        float total = Mathf.Max(destroyAnim, tile.respawnSeconds);
        float remainingAfterDestroy = Mathf.Max(0.01f, total - destroyAnim);

        yield return new WaitForSeconds(destroyAnim);

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

            if (tile.preRespawnWarningTile != null && tile.preRespawnWarningSeconds > 0f)
            {
                tm.SetTile(cell, tile.preRespawnWarningTile);
                tm.RefreshTile(cell);

                yield return new WaitForSeconds(Mathf.Max(0.01f, tile.preRespawnWarningSeconds));

                if (tm == null)
                    yield break;

                if (tm.GetTile(cell) != tile.preRespawnWarningTile)
                    yield break;

                tm.SetTile(cell, null);
                tm.RefreshTile(cell);

                center = tm.GetCellCenterWorld(cell);
                if (IsOccupied(center, tile.overlapBoxSize))
                {
                    yield return new WaitForSeconds(retry);
                    continue;
                }

                if (tm.GetTile(cell) != null)
                    yield break;
            }

            if (tile.resurrectionAnimatedTile != null)
            {
                tm.SetTile(cell, tile.resurrectionAnimatedTile);
                tm.RefreshTile(cell);

                yield return new WaitForSeconds(remainingAfterDestroy);

                if (tm == null)
                    yield break;

                if (tm.GetTile(cell) != tile.resurrectionAnimatedTile)
                    yield break;

                tm.SetTile(cell, null);
                tm.RefreshTile(cell);
            }
            else if (tile.resurrectionFrames != null && tile.resurrectionFrames.Length > 0)
            {
                int frames = tile.resurrectionFrames.Length;
                float frameDur = remainingAfterDestroy / frames;

                if (tm.GetTile(cell) != null)
                    yield break;

                float elapsed = 0f;

                for (int i = 0; i < frames; i++)
                {
                    if (tm == null)
                        yield break;

                    center = tm.GetCellCenterWorld(cell);
                    if (IsOccupied(center, tile.overlapBoxSize))
                    {
                        yield return new WaitForSeconds(retry);
                        i--;
                        continue;
                    }

                    TileBase frameTile = tile.resurrectionFrames[i];
                    if (frameTile != null)
                    {
                        tm.SetTile(cell, frameTile);
                        tm.RefreshTile(cell);
                    }

                    float wait = (i == frames - 1)
                        ? Mathf.Max(0.01f, remainingAfterDestroy - elapsed)
                        : Mathf.Max(0.01f, frameDur);

                    yield return new WaitForSeconds(wait);
                    elapsed += wait;

                    if (tm == null)
                        yield break;

                    if (frameTile != null && tm.GetTile(cell) != frameTile)
                        yield break;
                }

                tm.SetTile(cell, null);
                tm.RefreshTile(cell);
            }
            else
            {
                yield return new WaitForSeconds(remainingAfterDestroy);
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
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class ResurrectingAnimatedTileProgressHandler : MonoBehaviour, IDestructibleTileHandler
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
        if (tileBase is not ResurrectingAnimatedTileProgress tile)
            return false;

        source.ClearDestructibleForEffect(worldPos);

        Tilemap groundTm = groundTilemapOverride != null
            ? groundTilemapOverride
            : source.groundTiles;

        ScheduleRespawn(destructTm, groundTm, cell, tile);
        return true;
    }

    private void ScheduleRespawn(Tilemap destructTm, Tilemap groundTm, Vector3Int cell, ResurrectingAnimatedTileProgress tile)
    {
        if (_pending.TryGetValue(cell, out var c) && c != null)
            StopCoroutine(c);

        _pending[cell] = StartCoroutine(RespawnRoutine(destructTm, groundTm, cell, tile));
    }

    private IEnumerator RespawnRoutine(Tilemap destructTm, Tilemap groundTm, Vector3Int cell, ResurrectingAnimatedTileProgress tile)
    {
        float destroyAnim = Mathf.Max(0.01f, tile.destructionAnimationSeconds);
        float total = Mathf.Max(destroyAnim, tile.respawnSeconds);
        float remainingAfterDestroy = Mathf.Max(0.01f, total - destroyAnim);

        bool useGround = tile.renderResurrectionOnGround && groundTm != null;

        TileBase groundOriginal = null;
        if (useGround)
            groundOriginal = groundTm.GetTile(cell);

        yield return new WaitForSeconds(destroyAnim);

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

            if (tile.preRespawnWarningTile != null && tile.preRespawnWarningSeconds > 0f)
            {
                if (useGround)
                {
                    groundTm.SetTile(cell, tile.preRespawnWarningTile);
                    groundTm.RefreshTile(cell);
                }
                else
                {
                    destructTm.SetTile(cell, tile.preRespawnWarningTile);
                    destructTm.RefreshTile(cell);
                }

                yield return new WaitForSeconds(Mathf.Max(0.01f, tile.preRespawnWarningSeconds));

                if (destructTm == null)
                    yield break;

                if (destructTm.GetTile(cell) != null)
                {
                    if (useGround)
                    {
                        RestoreGround(groundTm, cell, groundOriginal, tile);
                    }
                    yield break;
                }

                if (useGround)
                {
                    if (groundTm == null)
                        yield break;

                    if (groundTm.GetTile(cell) != tile.preRespawnWarningTile)
                    {
                        yield break;
                    }

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

                center = destructTm.GetCellCenterWorld(cell);
                if (IsOccupied(center, tile.overlapBoxSize))
                {
                    yield return new WaitForSeconds(retry);
                    continue;
                }

                if (destructTm.GetTile(cell) != null)
                    yield break;
            }

            if (tile.resurrectionAnimatedTile != null)
            {
                if (useGround)
                {
                    groundTm.SetTile(cell, tile.resurrectionAnimatedTile);
                    groundTm.RefreshTile(cell);
                }
                else
                {
                    destructTm.SetTile(cell, tile.resurrectionAnimatedTile);
                    destructTm.RefreshTile(cell);
                }

                yield return new WaitForSeconds(remainingAfterDestroy);

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

                    if (groundTm.GetTile(cell) != tile.resurrectionAnimatedTile)
                        yield break;

                    groundTm.SetTile(cell, groundOriginal);
                    groundTm.RefreshTile(cell);
                }
                else
                {
                    if (destructTm.GetTile(cell) != tile.resurrectionAnimatedTile)
                        yield break;

                    destructTm.SetTile(cell, null);
                    destructTm.RefreshTile(cell);
                }
            }
            else if (tile.resurrectionFrames != null && tile.resurrectionFrames.Length > 0)
            {
                int frames = tile.resurrectionFrames.Length;
                float frameDur = remainingAfterDestroy / frames;

                if (destructTm.GetTile(cell) != null)
                    yield break;

                float elapsed = 0f;

                for (int i = 0; i < frames; i++)
                {
                    if (destructTm == null)
                        yield break;

                    if (destructTm.GetTile(cell) != null)
                    {
                        if (useGround)
                            RestoreGround(groundTm, cell, groundOriginal, tile);
                        yield break;
                    }

                    center = destructTm.GetCellCenterWorld(cell);
                    if (IsOccupied(center, tile.overlapBoxSize))
                    {
                        yield return new WaitForSeconds(retry);
                        i--;
                        continue;
                    }

                    TileBase frameTile = tile.resurrectionFrames[i];

                    if (frameTile != null)
                    {
                        if (useGround)
                        {
                            if (groundTm == null)
                                yield break;

                            groundTm.SetTile(cell, frameTile);
                            groundTm.RefreshTile(cell);
                        }
                        else
                        {
                            destructTm.SetTile(cell, frameTile);
                            destructTm.RefreshTile(cell);
                        }
                    }

                    float wait = (i == frames - 1)
                        ? Mathf.Max(0.01f, remainingAfterDestroy - elapsed)
                        : Mathf.Max(0.01f, frameDur);

                    yield return new WaitForSeconds(wait);
                    elapsed += wait;

                    if (destructTm == null)
                        yield break;

                    if (destructTm.GetTile(cell) != null)
                    {
                        if (useGround)
                            RestoreGround(groundTm, cell, groundOriginal, tile);
                        yield break;
                    }

                    if (frameTile != null)
                    {
                        if (useGround)
                        {
                            if (groundTm == null)
                                yield break;

                            if (groundTm.GetTile(cell) != frameTile)
                                yield break;
                        }
                        else
                        {
                            if (destructTm.GetTile(cell) != frameTile)
                                yield break;
                        }
                    }
                }

                if (useGround)
                {
                    RestoreGround(groundTm, cell, groundOriginal, tile);
                }
                else
                {
                    destructTm.SetTile(cell, null);
                    destructTm.RefreshTile(cell);
                }
            }
            else
            {
                yield return new WaitForSeconds(remainingAfterDestroy);
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

    private static void RestoreGround(Tilemap groundTm, Vector3Int cell, TileBase original, ResurrectingAnimatedTileProgress tile)
    {
        if (groundTm == null)
            return;

        if (!tile.renderResurrectionOnGround)
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
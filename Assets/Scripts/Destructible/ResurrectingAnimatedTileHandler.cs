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

            // se alguém já colocou um tile aqui (por qualquer motivo), não mexe
            if (tm.GetTile(cell) != null)
                yield break;

            Vector2 center = tm.GetCellCenterWorld(cell);

            // precisa estar livre para INICIAR o aviso
            if (IsOccupied(center, tile.overlapBoxSize))
            {
                yield return new WaitForSeconds(retry);
                continue;
            }

            // 1) Mostra o "warning tile" (atravessável) por 3s
            float warnSeconds = Mathf.Max(0.01f, tile.preRespawnWarningSeconds);

            if (tile.preRespawnWarningTile != null && warnSeconds > 0f)
            {
                tm.SetTile(cell, tile.preRespawnWarningTile);
                tm.RefreshTile(cell);

                yield return new WaitForSeconds(warnSeconds);

                if (tm == null)
                    yield break;

                // Se alguém trocou o tile no meio tempo, não mexe.
                if (tm.GetTile(cell) != tile.preRespawnWarningTile)
                    yield break;

                // remove o aviso antes de revalidar (fica vazio)
                tm.SetTile(cell, null);
                tm.RefreshTile(cell);
            }
            else
            {
                // mesmo sem warning tile, respeita os 3s se você quiser (aqui não espera)
            }

            // 2) Revalida ocupação APÓS os 3s
            center = tm.GetCellCenterWorld(cell);
            if (IsOccupied(center, tile.overlapBoxSize))
            {
                // se ficou ocupado, volta pro loop e tenta mais tarde
                yield return new WaitForSeconds(retry);
                continue;
            }

            // 3) (Opcional) animação curtinha imediatamente antes de nascer
            if (tile.respawnAnimationTile != null)
            {
                tm.SetTile(cell, tile.respawnAnimationTile);
                tm.RefreshTile(cell);

                float animDur = Mathf.Max(0.01f, tile.respawnAnimationDuration);
                yield return new WaitForSeconds(animDur);

                if (tm == null)
                    yield break;

                // se alguém trocou o tile durante a animação, não mexe.
                if (tm.GetTile(cell) != tile.respawnAnimationTile)
                    yield break;

                // limpa antes de colocar o final
                tm.SetTile(cell, null);
                tm.RefreshTile(cell);
            }

            // 4) Valida mais uma vez (só pra garantir)
            center = tm.GetCellCenterWorld(cell);
            if (IsOccupied(center, tile.overlapBoxSize))
            {
                yield return new WaitForSeconds(retry);
                continue;
            }

            // 5) Respawn final (tile real com collider)
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

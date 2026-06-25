using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class ScorchedGroundTileHandler : MonoBehaviour, IGroundTileHandler, IGroundTileExplosionHitHandler, IGroundTileShadowHandler
{
    [Serializable]
    public sealed class TileSwap
    {
        public TileBase groundTile;
        public TileBase scorchedTile;

        [Header("Optional Shadow Variants")]
        public TileBase shadowTile;
        public TileBase scorchedShadowTile;
    }

    [Header("Tile Swaps")]
    [SerializeField] private List<TileSwap> tileSwaps = new();

    [Header("Tilemaps")]
    [SerializeField] private Tilemap groundTilemapOverride;
    [SerializeField] private GroundTileResolver groundTileResolverOverride;

    private readonly Dictionary<TileBase, TileSwap> tileToSwap = new();
    private readonly Dictionary<Vector3Int, TileSwap> scorchedCells = new();

    private void Awake()
    {
        RebuildLookup();
        RegisterRuntimeTiles();
    }

    private void OnEnable()
    {
        RebuildLookup();
        RegisterRuntimeTiles();
    }

    private void OnDisable()
    {
        UnregisterRuntimeTiles();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
            return;

        RebuildLookup();
        RegisterRuntimeTiles();
    }

    public bool TryModifyExplosion(
        BombController source,
        Vector2 worldPos,
        TileBase groundTile,
        ref int radius,
        ref bool pierce)
    {
        return false;
    }

    public void OnExplosionHit(
        BombController source,
        Vector2 worldPos,
        Vector3Int cell,
        TileBase groundTile)
    {
        if (!TryGetSwapForTile(groundTile, out TileSwap swap))
            return;

        if (swap.scorchedTile == null)
            return;

        scorchedCells[cell] = swap;

        Tilemap tm = ResolveGroundTilemap(source);
        if (tm == null)
            return;

        TileBase nextTile = IsShadowTile(groundTile, swap)
            ? (swap.scorchedShadowTile != null ? swap.scorchedShadowTile : groundTile)
            : swap.scorchedTile;

        if (nextTile == null || tm.GetTile(cell) == nextTile)
            return;

        tm.SetTile(cell, nextTile);
        tm.RefreshTile(cell);
    }

    public bool TryGetShadowTile(
        Vector3Int cell,
        TileBase currentGroundTile,
        TileBase defaultShadowTile,
        out TileBase shadowTile)
    {
        shadowTile = null;

        if (!TryGetScorchedSwap(cell, currentGroundTile, out TileSwap swap))
            return false;

        shadowTile = swap.scorchedShadowTile != null ? swap.scorchedShadowTile : defaultShadowTile;
        return shadowTile != null;
    }

    public bool TryGetRestoredTile(
        Vector3Int cell,
        TileBase currentGroundTile,
        TileBase defaultGroundTile,
        out TileBase restoredTile)
    {
        restoredTile = null;

        if (!TryGetScorchedSwap(cell, currentGroundTile, out TileSwap swap))
            return false;

        restoredTile = swap.scorchedTile;
        return restoredTile != null;
    }

    private bool TryGetScorchedSwap(Vector3Int cell, TileBase currentGroundTile, out TileSwap swap)
    {
        if (scorchedCells.TryGetValue(cell, out swap) && swap != null)
            return true;

        if (!TryGetSwapForTile(currentGroundTile, out swap))
            return false;

        if (currentGroundTile != swap.scorchedTile && currentGroundTile != swap.scorchedShadowTile)
            return false;

        scorchedCells[cell] = swap;
        return true;
    }

    private void RebuildLookup()
    {
        tileToSwap.Clear();

        if (tileSwaps == null)
            return;

        for (int i = 0; i < tileSwaps.Count; i++)
        {
            TileSwap swap = tileSwaps[i];
            if (swap == null || swap.groundTile == null || swap.scorchedTile == null)
                continue;

            RegisterLookupTile(swap.groundTile, swap);
            RegisterLookupTile(swap.scorchedTile, swap);
            RegisterLookupTile(swap.shadowTile, swap);
            RegisterLookupTile(swap.scorchedShadowTile, swap);
        }
    }

    private void RegisterLookupTile(TileBase tile, TileSwap swap)
    {
        if (tile == null || swap == null)
            return;

        tileToSwap[tile] = swap;
    }

    private bool TryGetSwapForTile(TileBase tile, out TileSwap swap)
    {
        if (tile == null)
        {
            swap = null;
            return false;
        }

        return tileToSwap.TryGetValue(tile, out swap) && swap != null;
    }

    private static bool IsShadowTile(TileBase tile, TileSwap swap)
    {
        if (tile == null || swap == null)
            return false;

        return tile == swap.shadowTile || tile == swap.scorchedShadowTile;
    }

    private Tilemap ResolveGroundTilemap(BombController source)
    {
        if (groundTilemapOverride != null)
            return groundTilemapOverride;

        if (source != null && source.groundTiles != null)
            return source.groundTiles;

        if (GameManager.Instance != null && GameManager.Instance.groundTilemap != null)
            return GameManager.Instance.groundTilemap;

        return null;
    }

    private GroundTileResolver ResolveGroundTileResolver()
    {
        if (groundTileResolverOverride != null)
            return groundTileResolverOverride;

        groundTileResolverOverride = FindAnyObjectByType<GroundTileResolver>();
        if (groundTileResolverOverride != null)
            return groundTileResolverOverride;

        if (groundTilemapOverride != null)
            groundTileResolverOverride = groundTilemapOverride.GetComponentInParent<GroundTileResolver>(true);

        return groundTileResolverOverride;
    }

    private void RegisterRuntimeTiles()
    {
        GroundTileResolver resolver = ResolveGroundTileResolver();
        if (resolver == null || tileSwaps == null)
            return;

        for (int i = 0; i < tileSwaps.Count; i++)
        {
            TileSwap swap = tileSwaps[i];
            if (swap == null)
                continue;

            RegisterRuntimeTile(resolver, swap.groundTile);
            RegisterRuntimeTile(resolver, swap.scorchedTile);
            RegisterRuntimeTile(resolver, swap.shadowTile);
            RegisterRuntimeTile(resolver, swap.scorchedShadowTile);
        }
    }

    private void UnregisterRuntimeTiles()
    {
        GroundTileResolver resolver = ResolveGroundTileResolver();
        if (resolver == null || tileSwaps == null)
            return;

        for (int i = 0; i < tileSwaps.Count; i++)
        {
            TileSwap swap = tileSwaps[i];
            if (swap == null)
                continue;

            UnregisterRuntimeTile(resolver, swap.groundTile);
            UnregisterRuntimeTile(resolver, swap.scorchedTile);
            UnregisterRuntimeTile(resolver, swap.shadowTile);
            UnregisterRuntimeTile(resolver, swap.scorchedShadowTile);
        }
    }

    private void RegisterRuntimeTile(GroundTileResolver resolver, TileBase tile)
    {
        if (resolver == null || tile == null)
            return;

        resolver.RegisterRuntimeHandler(tile, this);
    }

    private void UnregisterRuntimeTile(GroundTileResolver resolver, TileBase tile)
    {
        if (resolver == null || tile == null)
            return;

        resolver.UnregisterRuntimeHandler(tile, this);
    }
}

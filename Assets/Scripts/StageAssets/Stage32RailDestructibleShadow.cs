using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace StageAssets
{
    /// <summary>
    /// Applies the rail-specific block shadows used exclusively by Stage 3-2.
    /// The component keeps the original ground tile so the rail is restored
    /// as soon as its destructible block is removed.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Stage32RailDestructibleShadow : MonoBehaviour
    {
        [Header("Stage 3-2 Tilemaps")]
        [SerializeField] private Tilemap groundTilemap;
        [SerializeField] private Tilemap destructibleTilemap;

        [Header("Ground-TrailDarker Variants")]
        [SerializeField] private TileBase trailDarker18;
        [SerializeField] private TileBase trailDarker13;
        [SerializeField] private TileBase trailDarker24;
        [SerializeField] private TileBase trailDarker29;
        [SerializeField] private TileBase trailDarker14;

        private readonly Dictionary<Vector3Int, TileBase> originalTrailTiles = new();

        private void OnEnable()
        {
            CacheOriginalTrailTiles();
            RefreshRailShadows();
        }

        private void LateUpdate()
        {
            // Destructibles may be removed by explosions outside this component.
            // Updating here keeps the visual synchronized without adding another
            // global callback to GameManager.
            RefreshRailShadows();
        }

        private void CacheOriginalTrailTiles()
        {
            if (groundTilemap == null || trailDarker18 == null || trailDarker29 == null)
                return;

            originalTrailTiles.Clear();
            foreach (Vector3Int cell in groundTilemap.cellBounds.allPositionsWithin)
            {
                TileBase tile = groundTilemap.GetTile(cell);
                if (tile == trailDarker18 || tile == trailDarker29)
                    originalTrailTiles[cell] = tile;
            }
        }

        private void RefreshRailShadows()
        {
            if (groundTilemap == null || destructibleTilemap == null || originalTrailTiles.Count == 0)
                return;

            foreach (KeyValuePair<Vector3Int, TileBase> entry in originalTrailTiles)
            {
                Vector3Int cell = entry.Key;
                TileBase originalTile = entry.Value;
                TileBase targetTile = GetTargetTile(cell, originalTile);

                if (targetTile != null && groundTilemap.GetTile(cell) != targetTile)
                    groundTilemap.SetTile(cell, targetTile);
            }
        }

        private TileBase GetTargetTile(Vector3Int cell, TileBase originalTile)
        {
            bool hasDestructibleHere = destructibleTilemap.GetTile(cell) != null;

            if (originalTile == trailDarker18)
            {
                if (hasDestructibleHere)
                    return trailDarker13;

                Vector3Int aboveCell = new(cell.x, cell.y + 1, cell.z);
                return destructibleTilemap.GetTile(aboveCell) == null
                    ? trailDarker18
                    : trailDarker24;
            }

            return originalTile == trailDarker29 && hasDestructibleHere
                ? trailDarker14
                : originalTile;
        }
    }
}

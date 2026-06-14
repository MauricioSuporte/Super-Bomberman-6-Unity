using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class PowerGroundTileHandler : MonoBehaviour, IGroundTileHandler
{
    [SerializeField] private int boostedRadius = 10;

    private Tilemap groundTilemap;
    private GroundTileResolver groundTileResolver;

    public int BoostedRadius => Mathf.Max(1, boostedRadius);

    public bool TryModifyExplosion(
        BombController source,
        Vector2 worldPos,
        TileBase groundTile,
        ref int radius,
        ref bool pierce)
    {
        if (radius < boostedRadius)
            radius = boostedRadius;

        return true;
    }

    public bool TryGetBoostedRadiusAtWorldPosition(
        Vector2 worldPosition,
        out int radius)
    {
        ResolveReferences();
        radius = BoostedRadius;
        if (groundTilemap == null || groundTileResolver == null)
            return false;

        Vector3Int cell = groundTilemap.WorldToCell(worldPosition);
        TileBase tile = groundTilemap.GetTile(cell);
        return tile != null &&
               groundTileResolver.TryGetHandler(
                   tile,
                   out IGroundTileHandler handler) &&
               ReferenceEquals(handler, this);
    }

    private void ResolveReferences()
    {
        if (groundTileResolver == null)
        {
            groundTileResolver =
                GetComponent<GroundTileResolver>();
        }

        if (groundTileResolver == null)
        {
            groundTileResolver =
                FindAnyObjectByType<GroundTileResolver>();
        }

        if (groundTilemap == null)
        {
            GameManager gameManager = GameManager.Instance != null
                ? GameManager.Instance
                : FindAnyObjectByType<GameManager>();
            if (gameManager != null)
                groundTilemap = gameManager.groundTilemap;
        }

        if (groundTilemap == null && groundTileResolver != null)
        {
            groundTilemap =
                groundTileResolver.GetComponentInChildren<Tilemap>(true);
        }
    }
}

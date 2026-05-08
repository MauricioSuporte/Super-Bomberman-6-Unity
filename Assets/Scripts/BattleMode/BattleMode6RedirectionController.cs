using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(GroundTileResolver))]
public sealed class BattleMode6RedirectionController : MonoBehaviour, IGroundTileHandler, IGroundTileBombAtHandler
{
    const string BattleMode6SceneName = "BattleMode_6";

    public enum ArrowDirection
    {
        Up,
        Down,
        Left,
        Right
    }

    [System.Serializable]
    public struct ArrowCell
    {
        public Vector2Int position;
        public ArrowDirection direction;
    }

    [Header("Tilemaps")]
    [SerializeField] private Tilemap groundTilemap;
    [SerializeField] private Tilemap indestructibleTilemap;
    [SerializeField] private GroundTileResolver groundTileResolver;

    [Header("Arrow Tiles")]
    [SerializeField] private TileBase upTile;
    [SerializeField] private TileBase downTile;
    [SerializeField] private TileBase leftTile;
    [SerializeField] private TileBase rightTile;

    [Header("Arrow Shadow Tiles")]
    [SerializeField] private TileBase upShadowTile;
    [SerializeField] private TileBase downShadowTile;
    [SerializeField] private TileBase leftShadowTile;
    [SerializeField] private TileBase rightShadowTile;

    [Header("Arrow Positions")]
    [SerializeField] private ArrowCell[] arrows;

    readonly Dictionary<Vector3Int, ArrowDirection> arrowDirectionsByCell = new();
    readonly HashSet<TileBase> registeredTiles = new();

    void Awake()
    {
        ResolveReferences();
        RebuildArrowMap();
        ApplyArrowTiles();
        RegisterResolverTiles();
    }

    void OnEnable()
    {
        ResolveReferences();
        RebuildArrowMap();
        ApplyArrowTiles();
        RegisterResolverTiles();
    }

    void OnDisable()
    {
        UnregisterResolverTiles();
        arrowDirectionsByCell.Clear();
    }

    public bool TryModifyExplosion(
        BombController source,
        Vector2 worldPos,
        TileBase groundTile,
        ref int radius,
        ref bool pierce)
        => false;

    public void OnBombAt(
        BombController source,
        Vector2 worldPos,
        Vector3Int cell,
        TileBase groundTile,
        GameObject bombGo)
    {
        if (!IsBattleMode6Active() || bombGo == null)
            return;

        RebuildArrowMap();

        if (!arrowDirectionsByCell.TryGetValue(cell, out ArrowDirection direction))
            return;

        if (!bombGo.TryGetComponent(out Bomb bomb) || bomb == null || !bomb.IsBeingKicked)
            return;

        bomb.TryRedirectKick(ToVector(direction));
    }

    public void OnIndestructiblePlaced(Vector3Int cell)
    {
        if (!IsBattleMode6Active())
            return;

        Vector3Int below = cell + Vector3Int.down;
        RefreshArrowTileAt(below);
    }

    [ContextMenu("Apply Arrow Tiles")]
    void ApplyArrowTiles()
    {
        ResolveReferences();
        if (groundTilemap == null || arrows == null)
            return;

        bool changed = false;
        for (int i = 0; i < arrows.Length; i++)
            changed |= RefreshArrowTileAt(ToCell(arrows[i].position), refreshTilemap: false);

        if (changed)
            groundTilemap.RefreshAllTiles();
    }

    bool RefreshArrowTileAt(Vector3Int cell, bool refreshTilemap = true)
    {
        ResolveReferences();
        RebuildArrowMap();

        if (groundTilemap == null)
            return false;

        if (!arrowDirectionsByCell.TryGetValue(cell, out ArrowDirection direction))
            return false;

        TileBase tile = GetArrowTile(direction, HasIndestructibleAbove(cell));
        if (tile == null || groundTilemap.GetTile(cell) == tile)
            return false;

        groundTilemap.SetTile(cell, tile);

        if (refreshTilemap)
            groundTilemap.RefreshTile(cell);

        return true;
    }

    void RebuildArrowMap()
    {
        arrowDirectionsByCell.Clear();

        if (arrows == null)
            return;

        for (int i = 0; i < arrows.Length; i++)
            arrowDirectionsByCell[ToCell(arrows[i].position)] = arrows[i].direction;
    }

    void RegisterResolverTiles()
    {
        ResolveReferences();
        if (groundTileResolver == null)
            return;

        RegisterTile(upTile);
        RegisterTile(downTile);
        RegisterTile(leftTile);
        RegisterTile(rightTile);
        RegisterTile(upShadowTile);
        RegisterTile(downShadowTile);
        RegisterTile(leftShadowTile);
        RegisterTile(rightShadowTile);
    }

    void RegisterTile(TileBase tile)
    {
        if (tile == null || registeredTiles.Contains(tile))
            return;

        groundTileResolver.RegisterRuntimeHandler(tile, this);
        registeredTiles.Add(tile);
    }

    void UnregisterResolverTiles()
    {
        if (groundTileResolver == null)
            return;

        foreach (TileBase tile in registeredTiles)
            groundTileResolver.UnregisterRuntimeHandler(tile, this);

        registeredTiles.Clear();
    }

    void ResolveReferences()
    {
        if (groundTileResolver == null)
            groundTileResolver = GetComponent<GroundTileResolver>();

        if (groundTilemap == null)
        {
            var gm = FindAnyObjectByType<GameManager>();
            if (gm != null)
            {
                groundTilemap = gm.groundTilemap;
                if (indestructibleTilemap == null)
                    indestructibleTilemap = gm.indestructibleTilemap;
            }
        }

        if (indestructibleTilemap == null)
        {
            var gm = FindAnyObjectByType<GameManager>();
            if (gm != null)
                indestructibleTilemap = gm.indestructibleTilemap;
        }

        if (groundTilemap == null)
            groundTilemap = FindTilemapByName("ground");

        if (indestructibleTilemap == null)
            indestructibleTilemap = FindTilemapByName("indestruct");
    }

    Tilemap FindTilemapByName(string namePart)
    {
        var tilemaps = FindObjectsByType<Tilemap>(FindObjectsInactive.Exclude);
        for (int i = 0; i < tilemaps.Length; i++)
        {
            Tilemap tm = tilemaps[i];
            if (tm != null && tm.name.ToLowerInvariant().Contains(namePart))
                return tm;
        }

        return null;
    }

    bool HasIndestructibleAbove(Vector3Int cell)
    {
        if (indestructibleTilemap == null)
            return false;

        Vector3Int above = cell + Vector3Int.up;
        return indestructibleTilemap.GetTile(above) != null;
    }

    TileBase GetArrowTile(ArrowDirection direction, bool shadow)
    {
        TileBase shadowTile = direction switch
        {
            ArrowDirection.Up => upShadowTile,
            ArrowDirection.Down => downShadowTile,
            ArrowDirection.Left => leftShadowTile,
            ArrowDirection.Right => rightShadowTile,
            _ => null,
        };

        if (shadow && shadowTile != null)
            return shadowTile;

        return direction switch
        {
            ArrowDirection.Up => upTile,
            ArrowDirection.Down => downTile,
            ArrowDirection.Left => leftTile,
            ArrowDirection.Right => rightTile,
            _ => null,
        };
    }

    static Vector3Int ToCell(Vector2Int position)
        => new(position.x, position.y, 0);

    static Vector2 ToVector(ArrowDirection direction)
    {
        return direction switch
        {
            ArrowDirection.Up => Vector2.up,
            ArrowDirection.Down => Vector2.down,
            ArrowDirection.Left => Vector2.left,
            ArrowDirection.Right => Vector2.right,
            _ => Vector2.zero,
        };
    }

    static bool IsBattleMode6Active()
        => string.Equals(SceneManager.GetActiveScene().name, BattleMode6SceneName, System.StringComparison.Ordinal);
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class MagnetIndestructibleTileHandler : MonoBehaviour, IIndestructibleTileHandler
{
    [Header("References")]
    [SerializeField] private Tilemap indestructibleTilemap;
    [SerializeField] private Tilemap destructibleTilemap;

    [Header("Magnet Tiles By Direction")]
    [SerializeField] private TileBase magnetLeft;
    [SerializeField] private TileBase magnetUp;
    [SerializeField] private TileBase magnetRight;
    [SerializeField] private TileBase magnetDown;

    [Header("Pull Settings")]
    [SerializeField] private float scanEverySeconds = 0.10f;
    [SerializeField] private int maxPullDistance = 12;
    [SerializeField] private float tileSize = 1f;
    [SerializeField] private LayerMask obstacleMask;

    private readonly Dictionary<Vector3Int, int> _dirIndexByCell = new();
    private readonly List<Vector3Int> _magnetCells = new();
    private Coroutine _scanRoutine;

    void Awake()
    {
        if (indestructibleTilemap == null)
            indestructibleTilemap = GetComponentInParent<Tilemap>();

        BuildMagnetListAndResetDirection();
    }

    void OnEnable()
    {
        BuildMagnetListAndResetDirection();

        if (_scanRoutine != null)
            StopCoroutine(_scanRoutine);

        _scanRoutine = StartCoroutine(ScanRoutine());
    }

    void OnDisable()
    {
        if (_scanRoutine != null)
            StopCoroutine(_scanRoutine);

        _scanRoutine = null;
    }

    public bool HandleExplosionHit(BombController source, Vector2 hitWorldPos, Vector3Int cell, TileBase tile)
    {
        if (!IsAnyMagnetTile(tile))
            return false;

        _dirIndexByCell.TryGetValue(cell, out int idx);

        idx = (idx + 1) % 4;
        _dirIndexByCell[cell] = idx;

        ApplyDirectionTile(cell, idx);
        return true;
    }

    private IEnumerator ScanRoutine()
    {
        var wait = new WaitForSeconds(scanEverySeconds);
        while (true)
        {
            PullBombs();
            yield return wait;
        }
    }

    private void PullBombs()
    {
        for (int i = 0; i < _magnetCells.Count; i++)
        {
            Vector3Int magnetCell = _magnetCells[i];

            var currentTile = indestructibleTilemap.GetTile(magnetCell);
            if (!IsAnyMagnetTile(currentTile))
                continue;

            _dirIndexByCell.TryGetValue(magnetCell, out int idx);

            Vector3Int dir = DirFromIndex(idx);

            for (int dist = 2; dist <= maxPullDistance; dist++)
            {
                Vector3Int bombCell = magnetCell + dir * dist;

                if (HasAnyIndestructibleAt(bombCell) && bombCell != magnetCell)
                    break;

                if (destructibleTilemap != null && destructibleTilemap.GetTile(bombCell) != null)
                    break;

                if (TryGetBombAtCell(bombCell, out _, out var bombComp))
                {
                    if (bombComp == null || bombComp.HasExploded || bombComp.IsBeingKicked || bombComp.IsBeingPunched || bombComp.IsBeingMagnetPulled)
                        break;

                    int steps = dist - 1;
                    Vector2 pullDir = new(-dir.x, -dir.y);

                    bombComp.StartMagnetPull(pullDir, tileSize, steps, obstacleMask, destructibleTilemap);
                    break;
                }
            }
        }
    }

    private void BuildMagnetListAndResetDirection()
    {
        _magnetCells.Clear();
        _dirIndexByCell.Clear();

        if (indestructibleTilemap == null)
            return;

        indestructibleTilemap.CompressBounds();
        var b = indestructibleTilemap.cellBounds;

        for (int y = b.yMin; y < b.yMax; y++)
            for (int x = b.xMin; x < b.xMax; x++)
            {
                var c = new Vector3Int(x, y, 0);
                var t = indestructibleTilemap.GetTile(c);

                if (!IsAnyMagnetTile(t))
                    continue;

                _magnetCells.Add(c);

                _dirIndexByCell[c] = 0;
                ApplyDirectionTile(c, 0);
            }
    }

    private void ApplyDirectionTile(Vector3Int cell, int idx)
    {
        if (indestructibleTilemap == null)
            return;

        indestructibleTilemap.SetTransformMatrix(cell, Matrix4x4.identity);

        indestructibleTilemap.SetTile(cell, GetTileForIndex(idx));
        indestructibleTilemap.RefreshTile(cell);
    }

    private TileBase GetTileForIndex(int idx)
    {
        return idx switch
        {
            0 => magnetLeft,
            1 => magnetUp,
            2 => magnetRight,
            3 => magnetDown,
            _ => magnetLeft
        };
    }

    private bool IsAnyMagnetTile(TileBase tile)
    {
        return tile != null &&
               (tile == magnetLeft || tile == magnetUp || tile == magnetRight || tile == magnetDown);
    }

    private bool HasAnyIndestructibleAt(Vector3Int cell)
    {
        if (indestructibleTilemap == null)
            return false;

        return indestructibleTilemap.GetTile(cell) != null;
    }

    private bool TryGetBombAtCell(Vector3Int cell, out GameObject bombGo, out Bomb bombComp)
    {
        bombGo = null;
        bombComp = null;

        if (indestructibleTilemap == null)
            return false;

        Vector3 world = indestructibleTilemap.GetCellCenterWorld(cell);

        int bombLayer = LayerMask.NameToLayer("Bomb");
        if (bombLayer < 0)
            return false;

        int bombMask = 1 << bombLayer;

        Collider2D hit = Physics2D.OverlapBox((Vector2)world, Vector2.one * 0.6f, 0f, bombMask);
        if (hit == null)
            return false;

        bombGo = hit.attachedRigidbody != null ? hit.attachedRigidbody.gameObject : hit.gameObject;
        if (bombGo == null)
            return false;

        bombGo.TryGetComponent(out bombComp);
        return bombComp != null;
    }

    private static Vector3Int DirFromIndex(int idx)
    {
        return idx switch
        {
            0 => Vector3Int.left,
            1 => Vector3Int.up,
            2 => Vector3Int.right,
            3 => Vector3Int.down,
            _ => Vector3Int.left
        };
    }
}

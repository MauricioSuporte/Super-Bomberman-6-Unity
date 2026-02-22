using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class PushIndestructibleTileHandler : MonoBehaviour, IIndestructibleTileHandler
{
    [Header("Tilemaps")]
    [SerializeField] private Tilemap indestructibleTilemap;
    [SerializeField] private Tilemap groundTilemap;
    [SerializeField] private Tilemap destructiblesTilemap;
    [SerializeField] private Tilemap otherIndestructibleTilemap;

    [Header("Move")]
    [SerializeField, Min(0.01f)] private float moveSeconds = 0.5f;

    [Header("Block If Target Has Any Of These Layers")]
    [SerializeField] private LayerMask blockMoveMask;

    [Header("Overlap Settings")]
    [SerializeField, Range(0.1f, 1.5f)] private float overlapBoxSize = 0.6f;

    [Header("Move Visual")]
    [SerializeField] private GameObject moveVisualPrefab;
    [SerializeField] private float visualZOverride = 0f;

    private readonly HashSet<Vector3Int> _movingCells = new();

    void Awake()
    {
        if (indestructibleTilemap == null)
            indestructibleTilemap = GetComponentInParent<Tilemap>();

        if (otherIndestructibleTilemap == null)
            otherIndestructibleTilemap = indestructibleTilemap;

        if (blockMoveMask.value == 0)
            blockMoveMask = LayerMask.GetMask("Item", "Bomb", "Enemy", "Player", "Louie");
    }

    public bool HandleExplosionHit(BombController source, Vector2 hitWorldPos, Vector3Int cell, TileBase tile)
    {
        if (tile == null) return false;
        if (indestructibleTilemap == null) return false;

        if (_movingCells.Contains(cell))
            return true;

        Vector3 fromCenter3 = indestructibleTilemap.GetCellCenterWorld(cell);
        Vector2 fromCenter = new(fromCenter3.x, fromCenter3.y);

        Vector3Int moveDir = ComputeMoveDirAwayFromHit(fromCenter, hitWorldPos);
        if (moveDir == Vector3Int.zero)
            return false;

        Vector3Int targetCell = cell + moveDir;

        if (!CanMoveToCell(targetCell, out _))
            return true;

        StartCoroutine(MoveTileRoutine(cell, targetCell, tile));
        return true;
    }

    private IEnumerator MoveTileRoutine(Vector3Int fromCell, Vector3Int toCell, TileBase tile)
    {
        _movingCells.Add(fromCell);

        Vector3 fromCenter3 = indestructibleTilemap.GetCellCenterWorld(fromCell);
        Vector3 toCenter3 = indestructibleTilemap.GetCellCenterWorld(toCell);

        Vector3 fromPos = new(fromCenter3.x, fromCenter3.y, fromCenter3.z);
        Vector3 toPos = new(toCenter3.x, toCenter3.y, toCenter3.z);

        if (!Mathf.Approximately(visualZOverride, 0f))
        {
            fromPos.z = visualZOverride;
            toPos.z = visualZOverride;
        }

        Color prevFromColor = indestructibleTilemap.GetColor(fromCell);
        Color prevToColor = indestructibleTilemap.GetColor(toCell);

        indestructibleTilemap.SetColor(fromCell, new Color(prevFromColor.r, prevFromColor.g, prevFromColor.b, 0f));
        indestructibleTilemap.RefreshTile(fromCell);

        GameObject visual = null;
        if (moveVisualPrefab != null)
            visual = Instantiate(moveVisualPrefab, fromPos, Quaternion.identity);

        indestructibleTilemap.SetTile(fromCell, null);
        indestructibleTilemap.RefreshTile(fromCell);

        float elapsed = 0f;
        while (elapsed < moveSeconds)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / moveSeconds);

            if (visual != null)
                visual.transform.position = Vector3.Lerp(fromPos, toPos, t);

            yield return null;
        }

        bool canPlace = CanMoveToCell(toCell, out _);

        if (canPlace)
        {
            indestructibleTilemap.SetTile(toCell, tile);
            indestructibleTilemap.SetColor(toCell, prevToColor);
            indestructibleTilemap.RefreshTile(toCell);
        }
        else
        {
            indestructibleTilemap.SetTile(fromCell, tile);
            indestructibleTilemap.SetColor(fromCell, prevFromColor);
            indestructibleTilemap.RefreshTile(fromCell);
        }

        indestructibleTilemap.SetColor(fromCell, prevFromColor);

        if (visual != null)
            Destroy(visual);

        _movingCells.Remove(fromCell);
    }

    private bool CanMoveToCell(Vector3Int targetCell, out string reason)
    {
        reason = "";

        if (groundTilemap == null)
        {
            reason = "groundTilemap=NULL";
            return false;
        }

        var groundTile = groundTilemap.GetTile(targetCell);
        if (groundTile == null)
        {
            reason = "no_ground";
            return false;
        }

        if (destructiblesTilemap != null && destructiblesTilemap.GetTile(targetCell) != null)
        {
            reason = "blocked_by_destructible_tile";
            return false;
        }

        if (otherIndestructibleTilemap != null && otherIndestructibleTilemap.GetTile(targetCell) != null)
        {
            reason = "blocked_by_indestructible_tile";
            return false;
        }

        Vector3 world3 = indestructibleTilemap.GetCellCenterWorld(targetCell);
        Vector2 world = new(world3.x, world3.y);

        Collider2D hit = Physics2D.OverlapBox(world, Vector2.one * overlapBoxSize, 0f, blockMoveMask);
        if (hit != null)
        {
            reason = $"blocked_by_collider:{hit.name}";
            return false;
        }

        reason = "ok";
        return true;
    }

    private static Vector3Int ComputeMoveDirAwayFromHit(Vector2 tileCenter, Vector2 hitWorldPos)
    {
        Vector2 d = tileCenter - hitWorldPos;

        if (Mathf.Abs(d.x) < 0.001f && Mathf.Abs(d.y) < 0.001f)
            return Vector3Int.zero;

        if (Mathf.Abs(d.x) >= Mathf.Abs(d.y))
            return d.x >= 0f ? Vector3Int.right : Vector3Int.left;

        return d.y >= 0f ? Vector3Int.up : Vector3Int.down;
    }
}
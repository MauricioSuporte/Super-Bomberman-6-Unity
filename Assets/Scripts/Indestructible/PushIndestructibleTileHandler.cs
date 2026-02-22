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

    [Header("Allowed Target Tile (same tilemap)")]
    [SerializeField] private TileBase allowedTargetIndestructibleTile;

    [Header("Merge Result (walkable ground tile)")]
    [SerializeField] private TileBase mergedGroundTile;

    [Header("Merge FX (animated tile)")]
    [SerializeField] private TileBase mergeFxAnimatedTile;
    [SerializeField, Min(0.01f)] private float mergeFxSeconds = 0.25f;

    [Header("Move")]
    [SerializeField, Min(0.01f)] private float moveSeconds = 0.5f;

    [Header("Block If Target Has Any Of These Layers")]
    [SerializeField] private LayerMask blockMoveMask;

    [Header("Overlap Settings")]
    [SerializeField, Range(0.1f, 1.5f)] private float overlapBoxSize = 0.6f;

    [Header("Move Visual")]
    [SerializeField] private GameObject moveVisualPrefab;
    [SerializeField] private float visualZOverride = 0f;

    [Header("Move Visual - Animation Offset (wiggle)")]
    [SerializeField] private bool enableMoveWiggle = true;
    [SerializeField, Min(0.001f)] private float wiggleIntervalSeconds = 0.01f;
    [SerializeField, Min(0f)] private float wiggleAmount = 0.05f;

    [Header("Origin Blocker (while moving)")]
    [SerializeField, Range(0.2f, 1.2f)] private float originBlockerSize = 0.9f;
    [SerializeField] private bool originBlockerUseTrigger = false;

    private readonly HashSet<Vector3Int> _movingCells = new();
    private readonly Dictionary<Vector3Int, GameObject> _originBlockers = new();
    private readonly HashSet<Vector3Int> _mergeFxCells = new();

    void Awake()
    {
        if (indestructibleTilemap == null)
            indestructibleTilemap = GetComponentInParent<Tilemap>();

        if (blockMoveMask.value == 0)
            blockMoveMask = LayerMask.GetMask("Item", "Bomb", "Enemy", "Player", "Louie");
    }

    public bool HandleExplosionHit(BombController source, Vector2 hitWorldPos, Vector3Int cell, TileBase tile)
    {
        if (tile == null) return false;
        if (indestructibleTilemap == null) return false;
        if (_movingCells.Contains(cell)) return true;

        Vector3 fromCenter3 = indestructibleTilemap.GetCellCenterWorld(cell);
        Vector2 fromCenter = new(fromCenter3.x, fromCenter3.y);

        Vector3Int moveDir = ComputeMoveDirAwayFromHit(fromCenter, hitWorldPos);
        if (moveDir == Vector3Int.zero) return false;

        Vector3Int targetCell = cell + moveDir;

        if (!CanMoveToCell(targetCell, out bool isMergeTarget))
            return true;

        StartCoroutine(MoveTileRoutine(cell, targetCell, tile, isMergeTarget));
        return true;
    }

    private IEnumerator MoveTileRoutine(Vector3Int fromCell, Vector3Int toCell, TileBase tile, bool mergeAtDestination)
    {
        _movingCells.Add(fromCell);

        EnsureOriginBlocker(fromCell);

        Vector3 fromCenter3 = indestructibleTilemap.GetCellCenterWorld(fromCell);
        Vector3 toCenter3 = indestructibleTilemap.GetCellCenterWorld(toCell);

        Vector3 fromPos = new(fromCenter3.x, fromCenter3.y, fromCenter3.z);
        Vector3 toPos = new(toCenter3.x, toCenter3.y, toCenter3.z);

        if (!Mathf.Approximately(visualZOverride, 0f))
        {
            fromPos.z = visualZOverride;
            toPos.z = visualZOverride;
        }

        // Determine movement axis for wiggle
        bool moveIsHorizontal = Mathf.Abs(toPos.x - fromPos.x) >= Mathf.Abs(toPos.y - fromPos.y);

        Color prevFromColor = indestructibleTilemap.GetColor(fromCell);

        indestructibleTilemap.SetColor(fromCell, new Color(prevFromColor.r, prevFromColor.g, prevFromColor.b, 0f));
        indestructibleTilemap.RefreshTile(fromCell);

        GameObject visual = null;
        if (moveVisualPrefab != null)
            visual = Instantiate(moveVisualPrefab, fromPos, Quaternion.identity);

        indestructibleTilemap.SetTile(fromCell, null);
        indestructibleTilemap.RefreshTile(fromCell);

        bool cancelAndReturnToOrigin = false;

        float wiggleTimer = 0f;
        float wiggleSign = 1f;

        float elapsed = 0f;
        while (elapsed < moveSeconds)
        {
            if (IsBlockedByMaskAtCell(toCell, blockMoveMask))
            {
                cancelAndReturnToOrigin = true;
                break;
            }

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / moveSeconds);

            if (visual != null)
            {
                Vector3 basePos = Vector3.Lerp(fromPos, toPos, t);

                if (enableMoveWiggle && wiggleAmount > 0f && wiggleIntervalSeconds > 0f)
                {
                    wiggleTimer += Time.deltaTime;
                    while (wiggleTimer >= wiggleIntervalSeconds)
                    {
                        wiggleTimer -= wiggleIntervalSeconds;
                        wiggleSign = -wiggleSign; // alterna: + / -
                    }

                    Vector3 off = moveIsHorizontal
                        ? new Vector3(wiggleSign * wiggleAmount, 0f, 0f)
                        : new Vector3(0f, wiggleSign * wiggleAmount, 0f);

                    visual.transform.position = basePos + off;
                }
                else
                {
                    visual.transform.position = basePos;
                }
            }

            yield return null;
        }

        if (cancelAndReturnToOrigin)
        {
            if (visual != null)
                visual.transform.position = fromPos;

            indestructibleTilemap.SetTile(fromCell, tile);
            indestructibleTilemap.SetColor(fromCell, prevFromColor);
            indestructibleTilemap.RefreshTile(fromCell);

            if (visual != null)
                Destroy(visual);

            RemoveOriginBlocker(fromCell);
            _movingCells.Remove(fromCell);
            yield break;
        }

        if (!CanMoveToCell(toCell, out bool mergeNow))
        {
            indestructibleTilemap.SetTile(fromCell, tile);
            indestructibleTilemap.SetColor(fromCell, prevFromColor);
            indestructibleTilemap.RefreshTile(fromCell);

            if (visual != null)
                Destroy(visual);

            RemoveOriginBlocker(fromCell);
            _movingCells.Remove(fromCell);
            yield break;
        }

        if (mergeAtDestination || mergeNow)
        {
            indestructibleTilemap.SetTile(toCell, null);
            indestructibleTilemap.RefreshTile(toCell);

            indestructibleTilemap.SetColor(fromCell, prevFromColor);

            if (groundTilemap != null && mergedGroundTile != null)
            {
                groundTilemap.SetTile(toCell, mergedGroundTile);
                groundTilemap.RefreshTile(toCell);
            }

            if (groundTilemap != null && mergeFxAnimatedTile != null && mergeFxSeconds > 0f)
            {
                StartMergeFx(toCell);
            }
        }
        else
        {
            Color prevToColor = indestructibleTilemap.GetColor(toCell);

            indestructibleTilemap.SetTile(toCell, tile);
            indestructibleTilemap.SetColor(toCell, prevToColor);
            indestructibleTilemap.RefreshTile(toCell);

            indestructibleTilemap.SetColor(fromCell, prevFromColor);
        }

        if (visual != null)
            Destroy(visual);

        RemoveOriginBlocker(fromCell);
        _movingCells.Remove(fromCell);
    }

    private void StartMergeFx(Vector3Int cell)
    {
        if (_mergeFxCells.Contains(cell))
            return;

        _mergeFxCells.Add(cell);
        StartCoroutine(MergeFxRoutine(cell));
    }

    private IEnumerator MergeFxRoutine(Vector3Int cell)
    {
        if (groundTilemap == null)
        {
            _mergeFxCells.Remove(cell);
            yield break;
        }

        TileBase prevGround = groundTilemap.GetTile(cell);

        groundTilemap.SetTile(cell, mergeFxAnimatedTile);
        groundTilemap.RefreshTile(cell);

        yield return new WaitForSeconds(mergeFxSeconds);

        if (groundTilemap == null)
        {
            _mergeFxCells.Remove(cell);
            yield break;
        }

        TileBase desiredFinal = mergedGroundTile != null ? mergedGroundTile : prevGround;

        groundTilemap.SetTile(cell, desiredFinal);
        groundTilemap.RefreshTile(cell);

        _mergeFxCells.Remove(cell);
    }

    private bool IsBlockedByMaskAtCell(Vector3Int cell, LayerMask mask)
    {
        if (indestructibleTilemap == null)
            return false;

        Vector3 w3 = indestructibleTilemap.GetCellCenterWorld(cell);
        Vector2 w = new(w3.x, w3.y);

        return Physics2D.OverlapBox(w, Vector2.one * overlapBoxSize, 0f, mask) != null;
    }

    private void EnsureOriginBlocker(Vector3Int cell)
    {
        if (_originBlockers.ContainsKey(cell))
            return;

        if (indestructibleTilemap == null)
            return;

        Vector3 c = indestructibleTilemap.GetCellCenterWorld(cell);

        var go = new GameObject($"IndestructibleOriginBlocker_{cell.x}_{cell.y}_{cell.z}");
        go.transform.SetParent(indestructibleTilemap.transform, worldPositionStays: true);
        go.transform.position = new Vector3(c.x, c.y, c.z);

        int stageLayer = LayerMask.NameToLayer("Stage");
        if (stageLayer >= 0)
            go.layer = stageLayer;

        var col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = originBlockerUseTrigger;
        col.size = Vector2.one * Mathf.Max(0.01f, originBlockerSize);

        _originBlockers[cell] = go;
    }

    private void RemoveOriginBlocker(Vector3Int cell)
    {
        if (!_originBlockers.TryGetValue(cell, out var go) || go == null)
        {
            _originBlockers.Remove(cell);
            return;
        }

        _originBlockers.Remove(cell);
        Destroy(go);
    }

    private bool CanMoveToCell(Vector3Int targetCell, out bool isMergeTarget)
    {
        isMergeTarget = false;

        if (groundTilemap == null) return false;

        var groundTile = groundTilemap.GetTile(targetCell);
        if (groundTile == null) return false;

        if (destructiblesTilemap != null && destructiblesTilemap.GetTile(targetCell) != null)
            return false;

        TileBase existing = indestructibleTilemap != null ? indestructibleTilemap.GetTile(targetCell) : null;
        if (existing != null)
        {
            if (allowedTargetIndestructibleTile == null)
                return false;

            if (existing != allowedTargetIndestructibleTile)
                return false;

            isMergeTarget = true;
        }

        Vector3 world3 = indestructibleTilemap != null
            ? indestructibleTilemap.GetCellCenterWorld(targetCell)
            : groundTilemap.GetCellCenterWorld(targetCell);

        Vector2 world = new(world3.x, world3.y);

        Collider2D hit = Physics2D.OverlapBox(world, Vector2.one * overlapBoxSize, 0f, blockMoveMask);
        if (hit != null) return false;

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
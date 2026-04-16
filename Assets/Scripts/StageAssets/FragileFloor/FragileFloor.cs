using Assets.Scripts.StageAssets.FragileFloor;
using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class FragileFloor : MonoBehaviour
{
    [Header("Tiles (assign in Inspector)")]
    [SerializeField] private TileBase crackedGroundTile;
    [SerializeField] private TileBase indestructibleTile;

    [Header("Optional")]
    [SerializeField] private bool disableTriggerAfterBlocked = true;
    [SerializeField, Min(1f)] private float pendingBlockClearanceMultiplier = 1.2f;

    private Tilemap _groundTilemap;
    private Tilemap _indestructiblesTilemap;
    private BoxCollider2D _triggerCollider;

    private Vector3Int _cell;
    private FragileFloorState _state = FragileFloorState.Normal;
    private Coroutine _pendingBlockRoutine;

    private static readonly WaitForFixedUpdate _waitForFixedUpdate = new();
    private static readonly Collider2D[] _overlapBuffer = new Collider2D[16];

    private void Reset()
    {
        if (TryGetComponent<BoxCollider2D>(out var bc))
            bc.isTrigger = true;
    }

    private void Awake()
    {
        if (TryGetComponent(out _triggerCollider))
            _triggerCollider.isTrigger = true;

        ResolveTilemaps();
        ResolveCell();
    }

    private void OnDisable()
    {
        if (_pendingBlockRoutine != null)
        {
            StopCoroutine(_pendingBlockRoutine);
            _pendingBlockRoutine = null;
        }
    }

    private void ResolveTilemaps()
    {
        if (_indestructiblesTilemap == null)
        {
            var indObj = GameObject.FindGameObjectWithTag("Indestructibles");
            if (indObj != null)
                indObj.TryGetComponent(out _indestructiblesTilemap);
        }

        if (_groundTilemap == null)
        {
            var groundGo = GameObject.Find("Ground");
            if (groundGo != null)
                groundGo.TryGetComponent(out _groundTilemap);
        }

        if (_groundTilemap == null || _indestructiblesTilemap == null)
        {
            var maps = Object.FindObjectsByType<Tilemap>();
            for (int i = 0; i < maps.Length; i++)
            {
                var m = maps[i];
                if (m == null) continue;

                var go = m.gameObject;
                if (_indestructiblesTilemap == null && go.CompareTag("Indestructibles"))
                    _indestructiblesTilemap = m;

                if (_groundTilemap == null && string.Equals(go.name, "Ground", System.StringComparison.OrdinalIgnoreCase))
                    _groundTilemap = m;
            }
        }

        if (_groundTilemap == null && _indestructiblesTilemap != null)
        {
            var grid = _indestructiblesTilemap.GetComponentInParent<Grid>();
            if (grid != null)
            {
                var tms = grid.GetComponentsInChildren<Tilemap>(true);
                for (int i = 0; i < tms.Length; i++)
                {
                    if (tms[i] != null && string.Equals(tms[i].gameObject.name, "Ground", System.StringComparison.OrdinalIgnoreCase))
                    {
                        _groundTilemap = tms[i];
                        break;
                    }
                }
            }
        }
    }

    private void ResolveCell()
    {
        var tm = _groundTilemap != null ? _groundTilemap : _indestructiblesTilemap;
        if (tm == null)
        {
            _cell = Vector3Int.zero;
            return;
        }

        _cell = tm.WorldToCell(transform.position);
    }

    private static bool IsPlayer(Collider2D col)
    {
        if (col == null) return false;
        if (col.CompareTag("Player")) return true;
        return col.GetComponentInParent<MovementController>() != null;
    }

    private bool TryResolveTilemapsAndCell()
    {
        if (_groundTilemap != null && _indestructiblesTilemap != null)
            return true;

        ResolveTilemaps();
        ResolveCell();

        return _groundTilemap != null && _indestructiblesTilemap != null;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsPlayer(other))
            return;

        if (_state == FragileFloorState.Blocked)
            return;

        if (!TryResolveTilemapsAndCell())
            return;

        if (_state == FragileFloorState.Normal)
        {
            if (crackedGroundTile != null)
            {
                _groundTilemap.SetTile(_cell, crackedGroundTile);
                _groundTilemap.RefreshTile(_cell);
            }

            _state = FragileFloorState.Cracked;
            return;
        }

        if (_state == FragileFloorState.Cracked)
        {
            _state = FragileFloorState.PendingBlock;
            EnsurePendingBlockRoutine();
            return;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!IsPlayer(other))
            return;

        if (_state != FragileFloorState.PendingBlock)
            return;

        if (!IsPlayerStillOccupyingFloor())
        {
            ApplyBlockNow();
            return;
        }

        EnsurePendingBlockRoutine();
    }

    private void ApplyBlockNow()
    {
        if (!TryResolveTilemapsAndCell())
            return;

        _groundTilemap.SetTile(_cell, null);
        _groundTilemap.RefreshTile(_cell);

        if (indestructibleTile != null)
        {
            _indestructiblesTilemap.SetTile(_cell, indestructibleTile);
            _indestructiblesTilemap.RefreshTile(_cell);
        }

        _state = FragileFloorState.Blocked;

        if (disableTriggerAfterBlocked)
        {
            if (TryGetComponent<BoxCollider2D>(out var bc)) bc.enabled = false;

            enabled = false;
        }
    }

    private void EnsurePendingBlockRoutine()
    {
        if (_state != FragileFloorState.PendingBlock)
            return;

        if (_pendingBlockRoutine != null)
            return;

        _pendingBlockRoutine = StartCoroutine(WaitUntilPlayerLeavesThenBlock());
    }

    private IEnumerator WaitUntilPlayerLeavesThenBlock()
    {
        while (_state == FragileFloorState.PendingBlock)
        {
            if (!IsPlayerStillOccupyingFloor())
            {
                ApplyBlockNow();
                break;
            }

            yield return _waitForFixedUpdate;
        }

        _pendingBlockRoutine = null;
    }

    private bool TryGetFloorBounds(out Vector2 center, out Vector2 size)
    {
        Tilemap tm = _groundTilemap != null ? _groundTilemap : _indestructiblesTilemap;
        if (tm != null)
        {
            Vector3 cellMin = tm.CellToWorld(_cell);
            Vector3 cellMax = tm.CellToWorld(_cell + new Vector3Int(1, 1, 0));
            Vector2 cellSize = new Vector2(Mathf.Abs(cellMax.x - cellMin.x), Mathf.Abs(cellMax.y - cellMin.y));

            if (cellSize.x > 0.0001f && cellSize.y > 0.0001f)
            {
                center = new Vector2((cellMin.x + cellMax.x) * 0.5f, (cellMin.y + cellMax.y) * 0.5f);
                size = cellSize;
                return true;
            }
        }

        if (_triggerCollider == null && !TryGetComponent(out _triggerCollider))
        {
            center = transform.position;
            size = Vector2.one;
            return false;
        }

        Bounds bounds = _triggerCollider.bounds;
        center = (Vector2)bounds.center;
        size = (Vector2)bounds.size;
        return bounds.size.x > 0.0001f && bounds.size.y > 0.0001f;
    }

    private bool TryGetPendingClearanceBounds(out Vector2 center, out Vector2 size)
    {
        if (!TryGetFloorBounds(out center, out size))
            return false;

        size *= Mathf.Max(1f, pendingBlockClearanceMultiplier);
        return true;
    }

    private bool IsPlayerStillOccupyingFloor()
    {
        if (!TryGetPendingClearanceBounds(out Vector2 center, out Vector2 size))
            return false;

        var filter = new ContactFilter2D
        {
            useTriggers = true,
            useLayerMask = false
        };

        int count = Physics2D.OverlapBox(
            center,
            size,
            0f,
            filter,
            _overlapBuffer);

        for (int i = 0; i < count; i++)
        {
            var hit = _overlapBuffer[i];
            _overlapBuffer[i] = null;

            if (hit == null)
                continue;

            if (IsPlayer(hit))
                return true;
        }

        return false;
    }
}

using Assets.Scripts.StageAssets.FragileFloor;
using System.Collections.Generic;
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

    private Tilemap _groundTilemap;
    private Tilemap _indestructiblesTilemap;

    private Vector3Int _cell;
    private FragileFloorState _state = FragileFloorState.Normal;

    private readonly HashSet<Collider2D> _playersInside = new();

    private void Reset()
    {
        if (TryGetComponent<BoxCollider2D>(out var bc)) bc.isTrigger = true;
    }

    private void Awake()
    {
        if (TryGetComponent<BoxCollider2D>(out var bc)) bc.isTrigger = true;

        ResolveTilemaps();
        ResolveCell();
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
            var maps = Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None);
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
        return col.CompareTag("Player") || col.GetComponent<MovementController>() != null;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsPlayer(other))
            return;

        _playersInside.Add(other);

        if (_state == FragileFloorState.Blocked)
            return;

        if (_groundTilemap == null || _indestructiblesTilemap == null)
        {
            ResolveTilemaps();
            ResolveCell();

            if (_groundTilemap == null || _indestructiblesTilemap == null)
                return;
        }

        if (_state == FragileFloorState.Normal)
        {
            if (crackedGroundTile != null)
                _groundTilemap.SetTile(_cell, crackedGroundTile);

            _state = FragileFloorState.Cracked;
            return;
        }

        if (_state == FragileFloorState.Cracked)
        {
            _state = FragileFloorState.PendingBlock;
            return;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!IsPlayer(other))
            return;

        _playersInside.Remove(other);

        if (_state != FragileFloorState.PendingBlock)
            return;

        if (_playersInside.Count > 0)
            return;

        ApplyBlockNow();
    }

    private void ApplyBlockNow()
    {
        if (_groundTilemap == null || _indestructiblesTilemap == null)
            return;

        _groundTilemap.SetTile(_cell, null);

        if (indestructibleTile != null)
            _indestructiblesTilemap.SetTile(_cell, indestructibleTile);

        _state = FragileFloorState.Blocked;

        if (disableTriggerAfterBlocked)
        {
            if (TryGetComponent<BoxCollider2D>(out var bc)) bc.enabled = false;

            enabled = false;
        }
    }
}
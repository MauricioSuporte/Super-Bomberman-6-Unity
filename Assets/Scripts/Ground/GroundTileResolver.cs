using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class GroundTileResolver : MonoBehaviour
{
    [Serializable]
    public sealed class TileGroup
    {
        public string id;
        public TileBase[] tiles;
        public MonoBehaviour handler;
    }

    [SerializeField] private List<TileGroup> groups = new();

    readonly Dictionary<TileBase, IGroundTileHandler> _map = new();
    readonly Dictionary<TileBase, IGroundTileHandler> _runtimeMap = new();

    void Awake() => Rebuild();

    void OnValidate()
    {
        if (!Application.isPlaying)
            return;

        Rebuild();
    }

    void Rebuild()
    {
        _map.Clear();

        if (groups == null)
            return;

        for (int i = 0; i < groups.Count; i++)
        {
            var g = groups[i];
            if (g == null || g.tiles == null || g.tiles.Length == 0 || g.handler == null)
                continue;

            if (g.handler is not IGroundTileHandler h)
                continue;

            for (int t = 0; t < g.tiles.Length; t++)
            {
                var tile = g.tiles[t];
                if (tile == null)
                    continue;

                _map[tile] = h;
            }
        }
    }

    public void RegisterRuntimeHandler(TileBase tile, IGroundTileHandler handler)
    {
        if (tile == null || handler == null)
            return;

        _runtimeMap[tile] = handler;
    }

    public void UnregisterRuntimeHandler(TileBase tile, IGroundTileHandler handler)
    {
        if (tile == null || handler == null)
            return;

        if (_runtimeMap.TryGetValue(tile, out var registered) && ReferenceEquals(registered, handler))
            _runtimeMap.Remove(tile);
    }

    public bool TryGetHandler(TileBase tile, out IGroundTileHandler handler)
    {
        if (tile == null)
        {
            handler = null;
            return false;
        }

        if (_runtimeMap.TryGetValue(tile, out handler) && handler != null)
            return true;

        return _map.TryGetValue(tile, out handler) && handler != null;
    }
}

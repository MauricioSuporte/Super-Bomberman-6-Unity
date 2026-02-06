using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class DestructibleTileResolver : MonoBehaviour
{
    [Serializable]
    public sealed class TileGroup
    {
        public string id;
        public TileBase[] tiles;
        public MonoBehaviour handler;
    }

    [SerializeField] private List<TileGroup> groups = new();

    readonly Dictionary<TileBase, IDestructibleTileHandler> _map = new();
    readonly HashSet<TileBase> _registeredTiles = new();

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
        _registeredTiles.Clear();

        if (groups == null)
            return;

        for (int i = 0; i < groups.Count; i++)
        {
            var g = groups[i];
            if (g == null || g.tiles == null || g.tiles.Length == 0)
                continue;

            for (int t = 0; t < g.tiles.Length; t++)
            {
                var tile = g.tiles[t];
                if (tile == null)
                    continue;

                _registeredTiles.Add(tile);
            }

            if (g.handler is not IDestructibleTileHandler h)
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

    public bool TryGetHandler(TileBase tile, out IDestructibleTileHandler handler)
    {
        if (tile == null)
        {
            handler = null;
            return false;
        }

        return _map.TryGetValue(tile, out handler) && handler != null;
    }

    public bool IsRegisteredDestructibleTile(TileBase tile)
    {
        if (tile == null)
            return false;

        return _registeredTiles.Contains(tile);
    }
}

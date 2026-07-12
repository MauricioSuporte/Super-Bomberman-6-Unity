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
    readonly Dictionary<string, IDestructibleTileHandler> _nameMap = new(StringComparer.OrdinalIgnoreCase);
    readonly HashSet<string> _registeredTileNames = new(StringComparer.OrdinalIgnoreCase);

    void Awake()
    {
        Rebuild();
    }

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
        _nameMap.Clear();
        _registeredTileNames.Clear();

        if (groups == null)
            return;

        for (int i = 0; i < groups.Count; i++)
        {
            var g = groups[i];
            if (g == null)
                continue;

            if (!string.IsNullOrWhiteSpace(g.id))
                _registeredTileNames.Add(g.id);

            if (g.tiles != null)
            {
                for (int t = 0; t < g.tiles.Length; t++)
                {
                    var tile = g.tiles[t];
                    if (tile == null)
                        continue;

                    _registeredTiles.Add(tile);
                    if (!string.IsNullOrWhiteSpace(tile.name))
                        _registeredTileNames.Add(tile.name);
                }
            }

            if (g.handler is not IDestructibleTileHandler h)
                continue;

            if (!string.IsNullOrWhiteSpace(g.id))
                _nameMap[g.id] = h;

            if (g.tiles == null)
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

        if (_map.TryGetValue(tile, out handler) && handler != null)
            return true;

        if (TryGetHandlerByTileName(tile.name, out handler))
            return true;

        handler = null;
        return false;
    }

    public bool IsRegisteredDestructibleTile(TileBase tile)
    {
        if (tile == null)
            return false;

        return _registeredTiles.Contains(tile) || IsRegisteredTileName(tile.name);
    }

    bool TryGetHandlerByTileName(string tileName, out IDestructibleTileHandler handler)
    {
        handler = null;

        if (string.IsNullOrWhiteSpace(tileName))
            return false;

        if (_nameMap.TryGetValue(tileName, out handler) && handler != null)
            return true;

        foreach (KeyValuePair<string, IDestructibleTileHandler> pair in _nameMap)
        {
            if (pair.Value != null && IsTileNameMatch(tileName, pair.Key))
            {
                handler = pair.Value;
                return true;
            }
        }

        handler = null;
        return false;
    }

    bool IsRegisteredTileName(string tileName)
    {
        if (string.IsNullOrWhiteSpace(tileName))
            return false;

        if (_registeredTileNames.Contains(tileName))
            return true;

        foreach (string registeredName in _registeredTileNames)
        {
            if (IsTileNameMatch(tileName, registeredName))
                return true;
        }

        return false;
    }

    static bool IsTileNameMatch(string tileName, string registeredName)
    {
        if (string.IsNullOrWhiteSpace(tileName) || string.IsNullOrWhiteSpace(registeredName))
            return false;

        return string.Equals(tileName, registeredName, StringComparison.OrdinalIgnoreCase) ||
               tileName.StartsWith(registeredName + "_", StringComparison.OrdinalIgnoreCase);
    }
}

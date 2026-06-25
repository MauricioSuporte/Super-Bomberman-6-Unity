using UnityEngine;
using UnityEngine.Tilemaps;

public interface IIndestructibleKickedBombHandler
{
    bool TryHandleKickedBombBlocked(
        Bomb bomb,
        Vector2 currentWorldPos,
        Vector2 blockedWorldPos,
        Vector2 kickDirection,
        Tilemap indestructibleTilemap,
        Vector3Int blockedCell,
        TileBase blockedTile,
        out AudioClip bounceSfx,
        out float bounceSfxVolume);
}

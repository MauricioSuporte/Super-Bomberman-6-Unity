using UnityEngine;

public interface IMovementAbility : IPlayerAbility
{
    bool TryHandleBlockedHit(Collider2D hit, Vector2 direction, float tileSize, LayerMask obstacleMask);
}

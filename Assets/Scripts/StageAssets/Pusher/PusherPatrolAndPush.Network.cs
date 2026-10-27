using Assets.Scripts.Netcode;
using Mirror;
using UnityEngine;

public sealed partial class PusherPatrolAndPush
{
    public const float NetworkContinuationSeconds = 0.15f;

    public struct NetworkPushState
    {
        public int sourceId, playerMask;
        public Vector2 center, size, velocity, safeCell;
        public float remaining, skin, minNormal, maxSide, minOverlap, tileSize;
        public bool horizontal;

        public readonly bool IsValid => sourceId != 0 && Finite(center) && Finite(size) && Finite(velocity) && Finite(safeCell) &&
            size.x > 0 && size.y > 0 && float.IsFinite(remaining) && remaining > 0 && remaining <= NetworkContinuationSeconds &&
            float.IsFinite(skin) && skin >= 0 && float.IsFinite(tileSize) && tileSize > 0 &&
            float.IsFinite(minNormal) && minNormal >= 0 && minNormal <= 1 &&
            float.IsFinite(maxSide) && maxSide >= 0 && maxSide <= 1 &&
            float.IsFinite(minOverlap) && minOverlap >= 0 && minOverlap <= 1 &&
            (horizontal ? Mathf.Abs(velocity.x) > Mathf.Abs(velocity.y) : Mathf.Abs(velocity.y) > Mathf.Abs(velocity.x));

        static bool Finite(Vector2 v) => float.IsFinite(v.x) && float.IsFinite(v.y);

        public readonly bool Contains(Vector2 position) =>
            Mathf.Round(position.x / tileSize) * tileSize == safeCell.x &&
            Mathf.Round(position.y / tileSize) * tileSize == safeCell.y;

        public readonly bool Accepts(RaycastHit2D hit)
        {
            Vector2 n = hit.normal;
            float normal = horizontal ? n.x : n.y, side = horizontal ? n.y : n.x;
            float push = horizontal ? velocity.x : velocity.y;
            if (Mathf.Abs(side) > maxSide || (push > 0 ? normal > -minNormal : normal < minNormal)) return false;
            Bounds player = hit.collider.bounds;
            float sourceMin = horizontal ? center.y - size.y * 0.5f : center.x - size.x * 0.5f;
            float sourceMax = horizontal ? center.y + size.y * 0.5f : center.x + size.x * 0.5f;
            float playerMin = horizontal ? player.min.y : player.min.x, playerMax = horizontal ? player.max.y : player.max.x;
            float overlap = Mathf.Min(sourceMax, playerMax) - Mathf.Max(sourceMin, playerMin);
            float denominator = Mathf.Min(horizontal ? size.y : size.x, horizontal ? player.size.y : player.size.x);
            return overlap > 0 && denominator > 0.00001f && overlap / denominator >= minOverlap;
        }
    }

    public NetworkPushState CaptureNetworkContinuation(MovementController actor)
    {
        if (!NetSync.IsServer || !isActiveAndEnabled || actor == null || _rb == null ||
            _col is not BoxCollider2D box || !box.enabled || box.edgeRadius != 0 ||
            Mathf.Abs(Mathf.DeltaAngle(transform.eulerAngles.z, Mathf.Round(transform.eulerAngles.z / 90) * 90)) > 0.01f ||
            !actor.CanPredictNetworkMovement) return default;
        float distance = Vector2.Distance(_rb.position, _toB ? pointB : pointA);
        if (distance <= arriveDistance || speed <= 0) return default;
        float tile = actor.tileSize;
        if (!float.IsFinite(tile) || tile <= 0) return default;
        Vector2 cell = new(Mathf.Round(actor.Rigidbody.position.x / tile) * tile, Mathf.Round(actor.Rigidbody.position.y / tile) * tile);
        if (actor.TryGetComponent<PlayerPushedOutOfInvalidTile>(out var resolver) && !resolver.CanProjectNetworkPushCell(cell, pushHorizontallyInsteadOfVertically ? Vector2.right : Vector2.up)) return default;
        var state = new NetworkPushState { sourceId = GetEntityId().GetHashCode(), playerMask = playerLayerMask,
            center = box.bounds.center, size = box.bounds.size,
            velocity = ((_toB ? pointB : pointA) - _rb.position).normalized * speed,
            safeCell = cell, tileSize = tile, horizontal = pushHorizontallyInsteadOfVertically,
            remaining = Mathf.Min(NetworkContinuationSeconds, (distance - arriveDistance) / speed), skin = castSkin,
            minNormal = pushHorizontallyInsteadOfVertically ? minHorizontalNormal : minVerticalNormal,
            maxSide = pushHorizontallyInsteadOfVertically ? maxUpDownNormal : maxSideNormal,
            minOverlap = pushHorizontallyInsteadOfVertically ? minYOverlapPercent : minXOverlapPercent };
        return state.IsValid ? state : default;
    }
}

namespace Assets.Scripts.Netcode
{
    public static class NetworkPusherSerialization
    {
        public static void WritePusher(this NetworkWriter writer, PusherPatrolAndPush.NetworkPushState state)
        {
            writer.WriteBool(state.sourceId != 0);
            if (state.sourceId == 0) return;
            writer.WriteInt(state.sourceId); writer.WriteInt(state.playerMask);
            writer.WriteVector2(state.center); writer.WriteVector2(state.size); writer.WriteVector2(state.velocity); writer.WriteVector2(state.safeCell);
            writer.WriteFloat(state.remaining); writer.WriteFloat(state.skin); writer.WriteFloat(state.minNormal);
            writer.WriteFloat(state.maxSide); writer.WriteFloat(state.minOverlap); writer.WriteFloat(state.tileSize); writer.WriteBool(state.horizontal);
        }

        public static PusherPatrolAndPush.NetworkPushState ReadPusher(this NetworkReader reader)
        {
            if (!reader.ReadBool()) return default;
            return new PusherPatrolAndPush.NetworkPushState { sourceId = reader.ReadInt(), playerMask = reader.ReadInt(),
                center = reader.ReadVector2(), size = reader.ReadVector2(), velocity = reader.ReadVector2(), safeCell = reader.ReadVector2(),
                remaining = reader.ReadFloat(), skin = reader.ReadFloat(), minNormal = reader.ReadFloat(), maxSide = reader.ReadFloat(),
                minOverlap = reader.ReadFloat(), tileSize = reader.ReadFloat(), horizontal = reader.ReadBool() };
        }
    }
}

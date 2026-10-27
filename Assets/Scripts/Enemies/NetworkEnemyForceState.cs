using Assets.Scripts.Netcode;
using Mirror;
using UnityEngine;

public enum NetworkEnemyForceKind : byte { None, Hurricane, Magnet, Bubble }

public interface INetworkEnemyForceSource
{
    NetworkEnemyForceState CaptureNetworkForce(MovementController actor, NetworkEnemyForceState receipt);
}

public struct NetworkEnemyForceState
{
    public const float MaximumSeconds = 0.15f;
    public int sourceId;
    public string scene;
    public NetworkEnemyForceKind kind;
    public Vector2 anchor, direction, safeCell;
    public float speed, remaining, remainingDistance, tileSize, alignment, range;
    public bool allowPull;

    static bool Finite(Vector2 value) => float.IsFinite(value.x) && float.IsFinite(value.y);
    public readonly bool IsValid => sourceId != 0 && scene != null && scene.Length <= 128 && kind >= NetworkEnemyForceKind.Hurricane && kind <= NetworkEnemyForceKind.Bubble &&
        Finite(anchor) && Finite(direction) && Finite(safeCell) && float.IsFinite(speed) && speed > 0 &&
        float.IsFinite(remaining) && remaining > 0 && remaining <= MaximumSeconds &&
        float.IsFinite(remainingDistance) && remainingDistance > 0 && float.IsFinite(tileSize) && tileSize > 0 &&
        float.IsFinite(alignment) && alignment >= 0 && float.IsFinite(range) && range > 0 &&
        (kind == NetworkEnemyForceKind.Hurricane || Mathf.Abs(direction.x) + Mathf.Abs(direction.y) == 1 && direction.x * direction.y == 0);

    public readonly bool Contains(Vector2 position) =>
        Mathf.Round(position.x / tileSize) * tileSize == safeCell.x && Mathf.Round(position.y / tileSize) * tileSize == safeCell.y;

    public readonly bool IsAligned(Vector2 position)
    {
        if (kind == NetworkEnemyForceKind.Bubble) return true;
        Vector2 offset = position - anchor;
        if (offset.magnitude > range) return false;
        if (kind == NetworkEnemyForceKind.Hurricane) return Mathf.Abs(offset.x) <= alignment || Mathf.Abs(offset.y) <= alignment;
        return Vector2.Dot(offset, direction) > 0 && (direction.x != 0 ? Mathf.Abs(offset.y) : Mathf.Abs(offset.x)) <= alignment;
    }

    public static bool IsFleeing(Vector2 direction, Vector2 offset) => direction != Vector2.zero && Vector2.Dot(direction.normalized, offset.normalized) > 0.01f;
}

namespace Assets.Scripts.Netcode
{
    public static class NetworkEnemyForceSerialization
    {
        public static void WriteEnemyForce(this NetworkWriter writer, NetworkEnemyForceState state)
        {
            writer.WriteBool(state.sourceId != 0); if (state.sourceId == 0) return;
            writer.WriteInt(state.sourceId); writer.WriteByte((byte)state.kind); writer.WriteString(state.scene);
            writer.WriteVector2(state.anchor); writer.WriteVector2(state.direction); writer.WriteVector2(state.safeCell);
            writer.WriteFloat(state.speed); writer.WriteFloat(state.remaining); writer.WriteFloat(state.remainingDistance);
            writer.WriteFloat(state.tileSize); writer.WriteFloat(state.alignment); writer.WriteFloat(state.range); writer.WriteBool(state.allowPull);
        }
        public static NetworkEnemyForceState ReadEnemyForce(this NetworkReader reader)
        {
            if (!reader.ReadBool()) return default;
            return new NetworkEnemyForceState { sourceId = reader.ReadInt(), kind = (NetworkEnemyForceKind)reader.ReadByte(), scene = reader.ReadString(),
                anchor = reader.ReadVector2(), direction = reader.ReadVector2(), safeCell = reader.ReadVector2(),
                speed = reader.ReadFloat(), remaining = reader.ReadFloat(), remainingDistance = reader.ReadFloat(),
                tileSize = reader.ReadFloat(), alignment = reader.ReadFloat(), range = reader.ReadFloat(), allowPull = reader.ReadBool() };
        }
    }
}

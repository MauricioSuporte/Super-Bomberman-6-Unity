using Assets.Scripts.Netcode;
using Mirror;
using UnityEngine;

public sealed partial class BoatRideZone
{
    // Approved attachment data, not client occupancy/protection or a mount command.
    public struct NetworkRideState
    {
        public string path;
        public int downId, upId, leftId, rightId;
        public Vector2 followOffset, headDown, headUp, headLeft, headRight;
        public Vector3 scale;
        public Quaternion rotation;
        public float z;
        public bool hideBody, hideHead, headOffsets;
    }

    string networkBoatPath;
    public string NetworkBoatPath
    {
        get
        {
            if (networkBoatPath != null) return networkBoatPath;
            networkBoatPath = name;
            for (var parent = transform.parent; parent != null; parent = parent.parent) networkBoatPath = parent.name + "/" + networkBoatPath;
            var authored = transform.localPosition;
            networkBoatPath += System.FormattableString.Invariant($"@{authored.x:R},{authored.y:R},{authored.z:R}");
            return networkBoatPath;
        }
    }

    public bool CanProjectNetworkRide => down != null && down.GetComponent<SpriteRenderer>() != null;
    public static void ReconcileNetworkRider(MovementController actor)
    {
        if (!NetSync.IsServer || !NetworkServer.active || actor == null ||
            !actor.TryGetComponent<NetworkPlayerInput>(out var input) || !input.isServer || input.netId == 0) return;
        NetworkPlayerInput.SyncAuthorityMotionTransform(actor);
        input.ServerReconcileMovementNow();
    }
    static int NetworkRendererId(AnimatedSpriteRenderer animation) => animation != null && animation.TryGetComponent<SpriteRenderer>(out var sprite)
        ? sprite.GetEntityId().GetHashCode() : 0;

    public static NetworkRideState CaptureNetworkRide(MovementController actor)
    {
        if (!NetSync.IsServer || !TryGetBoatForRider(actor, out var boat)) return default;
        return new NetworkRideState { path = boat.NetworkBoatPath,
            downId = NetworkRendererId(boat.down), upId = NetworkRendererId(boat.up), leftId = NetworkRendererId(boat.left), rightId = NetworkRendererId(boat.right),
            followOffset = boat.followOffset, z = boat.boatZ, rotation = boat.transform.rotation, scale = boat.transform.lossyScale,
            hideBody = boat.hidePlayerWhileRiding, hideHead = boat.hideHead, headOffsets = boat.applyHeadOnlyOffsets,
            headDown = boat.headOnlyDownOffset, headUp = boat.headOnlyUpOffset, headLeft = boat.headOnlyLeftOffset, headRight = boat.headOnlyRightOffset };
    }

    public AnimatedSpriteRenderer[] NetworkBoatAnimations() => new[] { down, up, left, right };
    public AnimatedSpriteRenderer[] NetworkHeadAnimations(Transform actor) => new[] {
        FindChildAnimByName(actor, headOnlyDownName), FindChildAnimByName(actor, headOnlyUpName),
        FindChildAnimByName(actor, headOnlyLeftName), FindChildAnimByName(actor, headOnlyRightName) };
}

namespace Assets.Scripts.Netcode
{
    public static class NetworkBoatSerialization
    {
        // Walking snapshots need only the presence bit, not an empty boat's poses.
        public static void WriteBoatRide(this NetworkWriter writer, BoatRideZone.NetworkRideState state)
        {
            bool present = !string.IsNullOrEmpty(state.path);
            writer.WriteBool(present);
            if (!present) return;
            writer.WriteString(state.path);
            writer.WriteInt(state.downId); writer.WriteInt(state.upId); writer.WriteInt(state.leftId); writer.WriteInt(state.rightId);
            writer.WriteVector2(state.followOffset); writer.WriteVector2(state.headDown); writer.WriteVector2(state.headUp);
            writer.WriteVector2(state.headLeft); writer.WriteVector2(state.headRight);
            writer.WriteVector3(state.scale); writer.WriteQuaternion(state.rotation); writer.WriteFloat(state.z);
            writer.WriteBool(state.hideBody); writer.WriteBool(state.hideHead); writer.WriteBool(state.headOffsets);
        }

        public static BoatRideZone.NetworkRideState ReadBoatRide(this NetworkReader reader)
        {
            if (!reader.ReadBool()) return default;
            return new BoatRideZone.NetworkRideState { path = reader.ReadString(),
                downId = reader.ReadInt(), upId = reader.ReadInt(), leftId = reader.ReadInt(), rightId = reader.ReadInt(),
                followOffset = reader.ReadVector2(), headDown = reader.ReadVector2(), headUp = reader.ReadVector2(),
                headLeft = reader.ReadVector2(), headRight = reader.ReadVector2(), scale = reader.ReadVector3(),
                rotation = reader.ReadQuaternion(), z = reader.ReadFloat(), hideBody = reader.ReadBool(),
                hideHead = reader.ReadBool(), headOffsets = reader.ReadBool() };
        }
    }
}

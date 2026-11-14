using Mirror;
using UnityEngine;

namespace Assets.Scripts.Netcode
{
    // Keep reliable delta decoding alive for every peer. The owner's motor
    // reconciles its root; only remote observers apply the normal interpolation.
    [AddComponentMenu("Network/Player Network Transform")]
    public sealed class PlayerNetworkTransform : NetworkTransformReliable
    {
        protected override void UpdateClient()
        {
            if (isLocalPlayer) return;
            base.UpdateClient();
        }

        bool skipHostRoomCut;
        Vector3 hostRoomCutPosition;
        Quaternion hostRoomCutRotation;

        [Server]
        public void ServerRoomCut(Vector3 destination, Quaternion rotation)
        {
            PrepareServerRoomCut(destination, rotation);
            RpcTeleport(destination, rotation);
        }

        void PrepareServerRoomCut(Vector3 destination, Quaternion rotation)
        {
            base.OnTeleport(destination, rotation);
            hostRoomCutPosition = destination;
            hostRoomCutRotation = rotation;
            skipHostRoomCut = isClient;
        }

        protected override void OnTeleport(Vector3 destination, Quaternion rotation)
        {
            // The server already reset its encoder before publishing this cut.
            // A delayed host-local RPC must not reset it a second time after
            // serialization has resumed, or remote delta origins diverge.
            if (isServer && skipHostRoomCut && destination == hostRoomCutPosition && rotation == hostRoomCutRotation)
            {
                skipHostRoomCut = false;
                return;
            }
            base.OnTeleport(destination, rotation);
        }
    }
}

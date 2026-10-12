using System;
using System.Net;
using Mirror;
using Mirror.Discovery;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Assets.Scripts.Netcode
{
    public struct OnlineDiscoveryRequest : NetworkMessage { }
    public struct OnlineDiscoveryResponse : NetworkMessage
    {
        public long serverId;
        public Uri uri;
        public int players;
        public bool available;
    }

    // Dedicated handshake keeps other Mirror games and incompatible discovery packets out.
    public sealed class OnlineRoomDiscovery : NetworkDiscoveryBase<OnlineDiscoveryRequest, OnlineDiscoveryResponse>
    {
        public event Action<OnlineDiscoveryResponse> RoomFound;

        public override void Start()
        {
            secretHandshake = 0x534236524F4F4D01;
            serverBroadcastListenPort = 47776;
            base.Start();
        }

        public static bool AcceptingPlayers => NetworkServer.active &&
            !NetworkServer.isLoadingScene && SceneManager.GetActiveScene().name == NetworkLobby.SceneName &&
            NetworkServer.connections.Count < GameSession.MaxPlayerId;

        protected override OnlineDiscoveryResponse ProcessRequest(OnlineDiscoveryRequest request, IPEndPoint endpoint)
            => new OnlineDiscoveryResponse
            {
                serverId = ServerId,
                uri = transport.ServerUri(),
                players = NetworkServer.connections.Count,
                available = AcceptingPlayers
            };

        protected override void ProcessResponse(OnlineDiscoveryResponse response, IPEndPoint endpoint)
        {
            if (response.uri == null || response.players < 0 || response.players > GameSession.MaxPlayerId) return;
            response.uri = new UriBuilder(response.uri) { Host = endpoint.Address.ToString() }.Uri;
            RoomFound?.Invoke(response);
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace Assets.Scripts.Netcode
{
    /// <summary>Checks compatibility before Mirror admits a connection to the room.</summary>
    public sealed class OnlineVersionAuthenticator : NetworkAuthenticator
    {
        // Bump when wire messages, network prefabs or gameplay content become incompatible.
        public const int ProtocolVersion = 6;
        readonly HashSet<NetworkConnectionToClient> pending = new();
        public static bool VersionRejected { get; private set; }

        public struct Request : NetworkMessage
        {
            public string gameVersion;
            public int protocol;
        }

        public struct Response : NetworkMessage { public bool compatible; }

        public static bool Compatible(string localVersion, string remoteVersion, int remoteProtocol) =>
            !string.IsNullOrEmpty(remoteVersion) && remoteVersion.Length <= 64 &&
            string.Equals(localVersion, remoteVersion, StringComparison.Ordinal) &&
            remoteProtocol == ProtocolVersion;

        public override void OnStartServer() => NetworkServer.RegisterHandler<Request>(OnRequest, false);

        public override void OnStopServer()
        {
            NetworkServer.UnregisterHandler<Request>();
            StopAllCoroutines();
            pending.Clear();
        }

        public override void OnServerAuthenticate(NetworkConnectionToClient connection)
        {
            pending.Add(connection);
            StartCoroutine(Timeout(connection));
        }

        IEnumerator Timeout(NetworkConnectionToClient connection)
        {
            yield return new WaitForSecondsRealtime(10);
            if (pending.Remove(connection) && !connection.isAuthenticated) ServerReject(connection);
        }

        void OnRequest(NetworkConnectionToClient connection, Request request)
        {
            if (!pending.Remove(connection)) return;
            bool compatible = Compatible(Application.version, request.gameVersion, request.protocol);
            if (!compatible) Debug.LogWarning($"[Room] rejected incompatible connection={connection.connectionId} protocol={request.protocol}");
            connection.Send(new Response { compatible = compatible });
            if (compatible) ServerAccept(connection);
            else StartCoroutine(Reject(connection));
        }

        IEnumerator Reject(NetworkConnectionToClient connection)
        {
            yield return new WaitForSecondsRealtime(0.3f);
            ServerReject(connection);
        }

        public override void OnStartClient()
        {
            VersionRejected = false;
            NetworkClient.RegisterHandler<Response>(OnResponse, false);
        }

        public override void OnStopClient() => NetworkClient.UnregisterHandler<Response>();

        public override void OnClientAuthenticate() => NetworkClient.Send(new Request
        {
            gameVersion = Application.version, protocol = ProtocolVersion
        });

        void OnResponse(Response response)
        {
            if (response.compatible) ClientAccept();
            else { VersionRejected = true; ClientReject(); }
        }
    }
}

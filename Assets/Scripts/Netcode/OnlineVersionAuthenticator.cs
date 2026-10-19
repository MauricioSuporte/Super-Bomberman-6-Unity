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
        public const int ProtocolVersion = 11;
        public static string BuildIdentity
        {
            get
            {
                var stamp = Resources.Load<TextAsset>("OnlineBuildIdentity");
                return stamp != null ? stamp.text.Trim() : "local-development";
            }
        }

        public static string RoomPassword { get; set; } = "";
        public static bool PasswordRejected { get; private set; }
        readonly Dictionary<string, (float until, int failures)> passwordFailures = new();

        public static bool PasswordMatches(string salt, string expectedHash, string password)
        {
            if (string.IsNullOrEmpty(expectedHash)) return true;
            if (password == null || password.Length != 6) return false;
            foreach (char digit in password) if (digit < '0' || digit > '9') return false;
            using var sha = System.Security.Cryptography.SHA256.Create();
            byte[] hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(salt + password));
            string actual = System.BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            int difference = actual.Length ^ expectedHash.Length;
            for (int i = 0; i < actual.Length && i < expectedHash.Length; i++) difference |= actual[i] ^ expectedHash[i];
            return difference == 0;
        }

        readonly HashSet<NetworkConnectionToClient> pending = new();
        public static bool VersionRejected { get; private set; }

        public struct Request : NetworkMessage
        {
            public string gameVersion;
            public string buildIdentity, password;
            public int protocol;
        }

        public struct Response : NetworkMessage { public bool compatible, passwordRejected; }

        public static bool Compatible(string localVersion, string remoteVersion, int remoteProtocol) =>
            !string.IsNullOrEmpty(remoteVersion) && remoteVersion.Length <= 64 &&
            string.Equals(localVersion, remoteVersion, StringComparison.Ordinal) &&
            remoteProtocol == ProtocolVersion;

        public static bool Compatible(string localVersion, string remoteVersion, int remoteProtocol,
            string localBuild, string remoteBuild) =>
            Compatible(localVersion, remoteVersion, remoteProtocol) &&
            !string.IsNullOrEmpty(localBuild) && localBuild.Length <= 64 &&
            string.Equals(localBuild, remoteBuild, StringComparison.Ordinal);

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
            bool compatible = Compatible(Application.version, request.gameVersion, request.protocol, BuildIdentity, request.buildIdentity);
            if (!compatible) Debug.LogWarning($"[Room] rejected incompatible connection={connection.connectionId} protocol={request.protocol}");
            bool passwordRejected = false;
            if (compatible)
            {
                string address = connection.address ?? "";
                passwordFailures.TryGetValue(address, out var attempts);
                if (Time.unscaledTime >= attempts.until) attempts = (Time.unscaledTime + 60f, 0);
                passwordRejected = attempts.failures >= 6 || !PasswordMatches(
                    Environment.GetEnvironmentVariable("SB6_ROOM_PASSWORD_SALT") ?? "",
                    Environment.GetEnvironmentVariable("SB6_ROOM_PASSWORD_HASH") ?? "", request.password);
                if (passwordRejected) passwordFailures[address] = (attempts.until, attempts.failures + 1);
                compatible = !passwordRejected;
            }
            connection.Send(new Response { compatible = compatible, passwordRejected = passwordRejected });
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
            PasswordRejected = false;
            NetworkClient.RegisterHandler<Response>(OnResponse, false);
        }

        public override void OnStopClient() => NetworkClient.UnregisterHandler<Response>();

        public override void OnClientAuthenticate() => NetworkClient.Send(new Request
        {
            gameVersion = Application.version, protocol = ProtocolVersion, buildIdentity = BuildIdentity, password = RoomPassword
        });

        void OnResponse(Response response)
        {
            if (response.compatible) ClientAccept();
            else { PasswordRejected = response.passwordRejected; VersionRejected = !PasswordRejected; ClientReject(); }
        }
    }
}

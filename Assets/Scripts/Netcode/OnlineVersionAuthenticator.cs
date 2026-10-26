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
        public const int ProtocolVersion = 43;
        public static string BuildIdentity
        {
            get
            {
                var stamp = Resources.Load<TextAsset>("OnlineBuildIdentity");
                return stamp != null ? stamp.text.Trim() : "local-development";
            }
        }

        // Editor debug can run local changes against the deployed build of the same
        // wire protocol. Keep BuildIdentity local for diagnostic provenance.
#if UNITY_EDITOR
        static string editorServerBuildIdentity;
        public static void SetEditorServerBuildIdentity(string identity)
        {
            editorServerBuildIdentity = identity;
            VersionRejected = false;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetEditorServerBuildIdentity() => editorServerBuildIdentity = null;
#endif
        public static string ConnectionBuildIdentity
        {
            get
            {
#if UNITY_EDITOR
                if (!NetworkServer.active && !string.IsNullOrEmpty(editorServerBuildIdentity)) return editorServerBuildIdentity;
#endif
                return BuildIdentity;
            }
        }

        public static string RoomPassword { get; set; } = "";
        public const int MaxPlayerNameLength = 16;
        public static string PlayerName
        {
            get => PlayerPrefs.GetString("OnlinePlayerName", "");
            set { PlayerPrefs.SetString("OnlinePlayerName", NormalizePlayerName(value)); PlayerPrefs.Save(); }
        }
        public static bool NameRejected { get; private set; }
        public static void ClearNameRejection() => NameRejected = false;
        readonly Dictionary<int, string> admittedNames = new();
        public static string NormalizePlayerName(string name)
        {
            if (name == null || name.Length > 64) return "";
            try { return name.Trim().Normalize(System.Text.NormalizationForm.FormKC); }
            catch (ArgumentException) { return ""; }
        }
        public static bool ValidPlayerName(string name)
        {
            string normalized = NormalizePlayerName(name);
            if (normalized.Length < 1 || normalized.Length > MaxPlayerNameLength) return false;
            foreach (char c in normalized)
                if (!char.IsLetterOrDigit(c) && c != ' ' && c != '_' && c != '-') return false;
            return true;
        }
        public bool NameAvailable(string name)
        {
            if (!ValidPlayerName(name)) return false;
            string normalized = NormalizePlayerName(name);
            foreach (string other in admittedNames.Values)
                if (string.Equals(other, normalized, StringComparison.OrdinalIgnoreCase)) return false;
            return true;
        }
        public string NameForConnection(int id) => admittedNames.TryGetValue(id, out var name) ? name : "";
        public void ReleaseName(int id) => admittedNames.Remove(id);

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
            public string buildIdentity, password, playerName;
            public int protocol;
        }

        public struct Response : NetworkMessage { public bool compatible, passwordRejected, nameRejected; }

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
            pending.Clear(); admittedNames.Clear();
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
            bool nameRejected = compatible && !NameAvailable(request.playerName);
            compatible &= !nameRejected;
            if (compatible) admittedNames[connection.connectionId] = NormalizePlayerName(request.playerName);
            connection.Send(new Response { compatible = compatible, passwordRejected = passwordRejected, nameRejected = nameRejected });
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
            PasswordRejected = false; NameRejected = false;
            NetworkClient.RegisterHandler<Response>(OnResponse, false);
        }

        public override void OnStopClient() => NetworkClient.UnregisterHandler<Response>();

        public override void OnClientAuthenticate() => NetworkClient.Send(new Request
        {
            gameVersion = Application.version, protocol = ProtocolVersion, buildIdentity = ConnectionBuildIdentity, password = RoomPassword, playerName = PlayerName
        });

        void OnResponse(Response response)
        {
            if (response.compatible) ClientAccept();
            else { PasswordRejected = response.passwordRejected; NameRejected = response.nameRejected; VersionRejected = !PasswordRejected && !NameRejected; ClientReject(); }
        }
    }
}

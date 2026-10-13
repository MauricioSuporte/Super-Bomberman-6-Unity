using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using Edgegap;
using Mirror;
using UnityEngine;

namespace Assets.Scripts.Netcode
{
    /// <summary>Opt-in two-network probe. Credentials live outside Assets and distributed builds.</summary>
    public static class OnlineRelayProbe
    {
        [Serializable]
        public sealed class Credentials
        {
            public string role, relayAddress;
            public int relayGameServerPort, relayGameClientPort;
            public uint sessionId, userId;

            public bool IsValid => (role == "host" || role == "client") &&
                !string.IsNullOrWhiteSpace(relayAddress) && relayAddress.Length <= 253 &&
                Uri.CheckHostName(relayAddress) != UriHostNameType.Unknown &&
                relayGameServerPort > 0 && relayGameServerPort <= ushort.MaxValue &&
                relayGameClientPort > 0 && relayGameClientPort <= ushort.MaxValue &&
                sessionId != 0 && userId != 0;
        }

        public static bool Active => Transport.active is EdgegapKcpTransport;
        public static string Role { get; private set; }

        public static void Configure(NetworkManager manager)
        {
            Role = null;
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-relay-config");
            if (index < 0) return;
            try
            {
                if (index + 1 >= args.Length) throw new ArgumentException();
                string path = Path.GetFullPath(args[index + 1]);
                if (new FileInfo(path).Length > 4096) throw new ArgumentException();
                string json = File.ReadAllText(path, new System.Text.UTF8Encoding(false, true));
                var data = JsonUtility.FromJson<Credentials>(json);
                if (data == null || !data.IsValid) throw new ArgumentException();
                bool exactSession = uint.TryParse(Regex.Match(json, "\"sessionId\"\\s*:\\s*(\\d+)").Groups[1].Value, out uint fileSession) && fileSession == data.sessionId;
                bool exactUser = uint.TryParse(Regex.Match(json, "\"userId\"\\s*:\\s*(\\d+)").Groups[1].Value, out uint fileUser) && fileUser == data.userId;
                Debug.Log($"[RelayProbe v3] credential parsing; sessionExact={exactSession}; userExact={exactUser}");
                if (!exactSession || !exactUser) throw new ArgumentException();
                var previous = manager.transport;
                // KcpTransport disallows a second component of its family on the same object.
                // A child follows the persistent manager across scenes without conflicting with LAN KCP.
                var relayObject = new GameObject("OnlineRelayTransport");
                relayObject.transform.SetParent(manager.transform, false);
                var relay = relayObject.AddComponent<OnlineRelayProbeTransport>();
                // This probe authorizes IPv4 peers. Avoid a DNS64/NAT64 route that can
                // expose a different public source address than the authorized IPv4.
                var ipv4 = Array.Find(Dns.GetHostAddresses(data.relayAddress),
                    candidate => candidate.AddressFamily == AddressFamily.InterNetwork);
                if (ipv4 == null) throw new ArgumentException();
                relay.relayAddress = ipv4.ToString();
                relay.relayGameServerPort = (ushort)data.relayGameServerPort;
                relay.relayGameClientPort = (ushort)data.relayGameClientPort;
                relay.sessionId = data.sessionId;
                relay.userId = data.userId;
                relay.relayGUI = false;
                manager.transport = relay;
                Transport.active = relay;
                if (previous != null) previous.enabled = false;
                Role = data.role;
                Debug.Log($"[RelayProbe v2] configured; role={Role}; file={Path.GetFileName(path)}; " +
                    $"relay={relay.relayAddress}; serverPort={data.relayGameServerPort}; " +
                    $"clientPort={data.relayGameClientPort}");
            }
            catch (Exception error) when (error is IOException || error is ArgumentException ||
                error is UnauthorizedAccessException || error is System.Text.DecoderFallbackException ||
                error is SocketException)
            {
                // Fail closed: a bad relay file must never silently start a direct-IP host.
                manager.enabled = false;
                throw new InvalidOperationException("Invalid relay probe configuration. Check the private credential file.");
            }
        }
    }
}

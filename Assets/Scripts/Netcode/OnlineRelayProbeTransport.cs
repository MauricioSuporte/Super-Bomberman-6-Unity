using System;
using Edgegap;
using Mirror;
using UnityEngine;

namespace Assets.Scripts.Netcode
{
    /// <summary>Credential-free diagnostics for the opt-in relay probe.</summary>
    public sealed class OnlineRelayProbeTransport : EdgegapKcpTransport
    {
        int serverIncoming, serverOutgoing, clientIncoming, clientOutgoing;
        float reportUntil;

        void BeginReport(string action)
        {
            serverIncoming = serverOutgoing = clientIncoming = clientOutgoing = 0;
            reportUntil = Time.unscaledTime + 30;
            CancelInvoke(nameof(Report));
            InvokeRepeating(nameof(Report), 0, 2);
            Debug.Log($"[RelayProbe v2] {action}; role={OnlineRelayProbe.Role}; " +
                $"relay={relayAddress}; serverPort={relayGameServerPort}; clientPort={relayGameClientPort}; " +
                $"activeTransport={ReferenceEquals(Transport.active, this)}; enabled={enabled}; " +
                $"gameVersion={Application.version}");
        }

        public override void ServerStart()
        {
            BeginReport("ServerStart");
            base.ServerStart();
            Debug.Log($"[RelayProbe v2] server socket started; relayActive={ServerActive()}; " +
                $"endpoint={((EdgegapKcpServer)server).remoteEndPoint}");
        }

        public override void ClientConnect(string address)
        {
            BeginReport("ClientConnect(string)");
            base.ClientConnect(address);
            Debug.Log($"[RelayProbe v2] client socket started; endpoint={client.remoteEndPoint}");
        }

        public override void ClientConnect(Uri uri)
        {
            BeginReport("ClientConnect(uri)");
            base.ClientConnect(uri);
            Debug.Log($"[RelayProbe v2] client socket started; endpoint={client.remoteEndPoint}");
        }

        public override void ServerEarlyUpdate() { serverIncoming++; base.ServerEarlyUpdate(); }
        public override void ServerLateUpdate() { serverOutgoing++; base.ServerLateUpdate(); }
        public override void ClientEarlyUpdate() { clientIncoming++; base.ClientEarlyUpdate(); }
        public override void ClientLateUpdate() { clientOutgoing++; base.ClientLateUpdate(); }

        void Report()
        {
            if (Time.unscaledTime > reportUntil) { CancelInvoke(nameof(Report)); return; }
            var relayServer = (EdgegapKcpServer)server;
            var relayClient = (EdgegapKcpClient)client;
            Debug.Log($"[RelayProbe v3] tick; role={OnlineRelayProbe.Role}; " +
                $"activeTransport={ReferenceEquals(Transport.active, this)}; enabled={enabled}; " +
                $"serverActive={ServerActive()}; serverState={(byte)relayServer.state}/{relayServer.state}; " +
                $"clientState={(byte)relayClient.connectionState}/{relayClient.connectionState}; " +
                $"kcpConnected={client.connected}; " +
                $"serverTicks={serverIncoming}/{serverOutgoing}; clientTicks={clientIncoming}/{clientOutgoing}; " +
                $"serverPings={relayServer.PingAttempts}/{relayServer.PingsSent}/{relayServer.PingReplies}; " +
                $"clientPings={relayClient.PingAttempts}/{relayClient.PingReplies}; networkTime={NetworkTime.localTime:F2}");
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Mirror;
using UnityEngine;

namespace Assets.Scripts.Netcode
{
    public struct OnlineChatSendMessage : NetworkMessage { public string text; }
    public struct OnlineChatLineMessage : NetworkMessage { public string sender, text; }

    public static class OnlineChatRules
    {
        public const int MaxLength = 128;
        public const int HistoryLimit = 64;
        public const float SendInterval = 0.75f;

        // One plain-text line. Reject malformed Unicode and remove invisible controls.
        public static string Normalize(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > MaxLength * 4) return "";
            var result = new StringBuilder();
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (char.IsHighSurrogate(c))
                {
                    if (i + 1 >= value.Length || !char.IsLowSurrogate(value[i + 1])) continue;
                    if (result.Length + 2 > MaxLength) break;
                    result.Append(c).Append(value[++i]);
                    continue;
                }
                if (char.IsLowSurrogate(c)) continue;
                var category = char.GetUnicodeCategory(c);
                if (char.IsControl(c) || category == UnicodeCategory.Format ||
                    category == UnicodeCategory.LineSeparator || category == UnicodeCategory.ParagraphSeparator)
                { if (char.IsWhiteSpace(c) && result.Length < MaxLength) result.Append(' '); continue; }
                if (result.Length == MaxLength) break;
                result.Append(c);
            }
            return result.ToString().Trim();
        }
    }

    public partial class BombermanNetworkManager
    {
        readonly Dictionary<int, float> nextChatByConnection = new();

        void InitializeChatServer()
        {
            nextChatByConnection.Clear();
            NetworkServer.RegisterHandler<OnlineChatSendMessage>((connection, message) =>
            {
                if (!connection.isAuthenticated || !playerIdByConnection.ContainsKey(connection.connectionId)) return;
                if (nextChatByConnection.TryGetValue(connection.connectionId, out float next) && Time.unscaledTime < next) return;
                nextChatByConnection[connection.connectionId] = Time.unscaledTime + OnlineChatRules.SendInterval;
                string text = OnlineChatRules.Normalize(message.text);
                string sender = authenticator is OnlineVersionAuthenticator names ? names.NameForConnection(connection.connectionId) : "";
                if (text.Length == 0 || !OnlineVersionAuthenticator.ValidPlayerName(sender)) return;
                RegisterRoomActivity();
                var line = new OnlineChatLineMessage { sender = sender, text = text };
                // Chat is independent of gameplay readiness and survives coordinated scene loads.
                foreach (var peer in NetworkServer.connections.Values)
                    if (peer != null && peer.isAuthenticated) peer.Send(line);
            });
        }

        void InitializeChatClient() => NetworkClient.RegisterHandler<OnlineChatLineMessage>(message =>
            GetComponent<OnlineChatOverlay>()?.ReceiveRoomLine(message.sender, message.text));
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Mirror;
using UnityEngine;

namespace Assets.Scripts.Netcode
{
    public struct OnlineChatSendMessage : NetworkMessage { public string text, nonce; }
    public struct OnlineChatLineMessage : NetworkMessage { public string sender, text, nonce; public int playerId; }

    public struct OnlineChatPresenceMessage : NetworkMessage
    {
        public string sender;
        public int playerId;
        public bool joined, you;
    }

    public static class OnlineChatRules
    {
        public const int MaxLength = 128;
        public const int HistoryLimit = 64;
        public const float SendInterval = 0.25f;

        public static string EscapeMarkup(string text) => (text ?? "").Replace("<", "<noparse><</noparse>");
        public static string NameColor(int playerId, string sender = "")
        {
            if (playerId <= 0)
            {
                uint hash = 2166136261;
                foreach (char c in sender) hash = unchecked((hash ^ c) * 16777619);
                playerId = 7 + (int)(hash % 10000);
            }
            return playerId switch
            {
                1 => "FFFFFF", 2 => "000000", 3 => "FF0000",
                4 => "0000FF", 5 => "00FF00", 6 => "FFFF00",
                _ => ColorUtility.ToHtmlStringRGB(Color.HSVToRGB(Mathf.Repeat((playerId - 7) * 0.618034f + 0.15f, 1f), 0.48f, 1f))
            };
        }

        public static string FormatLine(string sender, string text, int playerId = 0, bool contrastOnly = false)
        {
            string nameColor = NameColor(playerId, sender);
            // The second mesh adds a light outline only around dark player names.
            // Transparent glyphs retain identical wrapping/positions for the other text.
            if (contrastOnly) nameColor += playerId is 2 or 4 ? "FF" : "00";
            return "<color=#" + nameColor + ">" + EscapeMarkup(sender) + ":</color> <color=#" +
                (contrastOnly ? "FFFFFF00" : "F5D7DE") + ">" + EscapeMarkup(text) + "</color>";
        }

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

    public sealed class OnlineChatSendBudget
    {
        float credits = 4, last;
        public bool TryConsume(float now)
        {
            credits = Mathf.Min(4, credits + Mathf.Max(0, now - last) / OnlineChatRules.SendInterval);
            last = now;
            if (credits < 1) return false;
            credits -= 1;
            return true;
        }
    }

    public partial class BombermanNetworkManager
    {
        readonly Dictionary<int, OnlineChatSendBudget> nextChatByConnection = new();
        readonly HashSet<int> chatDepartedConnections = new();

        void BroadcastChatPresence(NetworkConnectionToClient connection, bool joined)
        {
            if (connection == null || !playerIdByConnection.TryGetValue(connection.connectionId, out int id)) return;
            if (!joined && !chatDepartedConnections.Add(connection.connectionId)) return;
            string name = authenticator is OnlineVersionAuthenticator names ? names.NameForConnection(connection.connectionId) : "";
            if (!OnlineVersionAuthenticator.ValidPlayerName(name)) return;
            foreach (var peer in NetworkServer.connections.Values)
                if (peer != null && peer.isAuthenticated && (joined || peer != connection))
                    peer.Send(new OnlineChatPresenceMessage { sender = name, playerId = id,
                        joined = joined, you = peer.connectionId == connection.connectionId });
        }

        void InitializeChatServer()
        {
            nextChatByConnection.Clear();
            chatDepartedConnections.Clear();
            NetworkServer.RegisterHandler<OnlineChatSendMessage>((connection, message) =>
            {
                if (!connection.isAuthenticated || !playerIdByConnection.TryGetValue(connection.connectionId, out int playerId)) return;
                if (!Guid.TryParseExact(message.nonce, "N", out _)) return;
                if (!nextChatByConnection.TryGetValue(connection.connectionId, out var budget))
                    nextChatByConnection[connection.connectionId] = budget = new OnlineChatSendBudget();
                if (!budget.TryConsume(Time.unscaledTime)) return;
                string text = OnlineChatRules.Normalize(message.text);
                string sender = authenticator is OnlineVersionAuthenticator names ? names.NameForConnection(connection.connectionId) : "";
                if (text.Length == 0 || !OnlineVersionAuthenticator.ValidPlayerName(sender)) return;
                RegisterRoomActivity();
                var line = new OnlineChatLineMessage { sender = sender, text = text, nonce = message.nonce, playerId = playerId };
                // Chat is independent of gameplay readiness and survives coordinated scene loads.
                foreach (var peer in NetworkServer.connections.Values)
                    if (peer != null && peer.isAuthenticated) peer.Send(line);
            });
        }

        void InitializeChatClient()
        {
            NetworkClient.RegisterHandler<OnlineChatLineMessage>(message =>
                GetComponent<OnlineChatOverlay>()?.ReceiveRoomLine(message.sender, message.text, message.nonce, message.playerId));
            NetworkClient.RegisterHandler<OnlineChatPresenceMessage>(message =>
                GetComponent<OnlineChatOverlay>()?.ReceivePresence(message.sender, message.playerId, message.joined, message.you));
        }
    }
}

using System;
using UnityEngine;

namespace Assets.Scripts.Netcode
{
    public static class OnlineRoomRules
    {
        public const string DirectoryOverrideKey = "Online.DirectoryOverride";
        public const string LocalDirectory = "http://127.0.0.1:18081";

        public static bool IsValid(OnlineRoomMode mode) => mode is OnlineRoomMode.BattleMode or
            OnlineRoomMode.NormalGame or OnlineRoomMode.BossRush;

        public static int Capacity(OnlineRoomMode mode) => mode switch
        {
            OnlineRoomMode.BattleMode => 6,
            OnlineRoomMode.NormalGame or OnlineRoomMode.BossRush => 4,
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };

        public static OnlineRoomMode ReadMode(string[] args)
        {
            int index = Array.IndexOf(args, "-room-mode");
            if (index < 0) return OnlineRoomMode.BattleMode;
            if (index + 1 < args.Length && Enum.TryParse(args[index + 1], out OnlineRoomMode mode) && IsValid(mode))
                return mode;
            throw new ArgumentException("-room-mode must be BattleMode, NormalGame or BossRush.");
        }

        public static string DirectoryUrl(string configured)
        {
            string value = PlayerPrefs.GetString(DirectoryOverrideKey, configured ?? "");
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-online-directory");
            if (index >= 0 && index + 1 < args.Length) value = args[index + 1];
            value = value.Trim().TrimEnd('/');
            return Uri.TryCreate(value, UriKind.Absolute, out var endpoint) &&
                (endpoint.Scheme == "https" || (endpoint.Scheme == "http" &&
                    (endpoint.IsLoopback || endpoint.Host == "100.64.0.46" || endpoint.Host == "192.48.205.46")))
                ? value : "";
        }
    }
}

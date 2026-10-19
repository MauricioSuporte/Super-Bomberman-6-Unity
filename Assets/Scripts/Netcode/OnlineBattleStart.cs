using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Assets.Scripts.Netcode
{
    public struct BattleScreenReady : NetworkMessage { public string scene; }
    public struct BattleStartSchedule : NetworkMessage { public string scene; public double releaseTime; }

    public partial class BombermanNetworkManager
    {
        readonly HashSet<int> battleScreenReadyPeers = new();
        double battleReleaseTime;
        string battleStartScene;

        public static bool IsBattleArena => NetSync.IsOnline && RoomMode == OnlineRoomMode.BattleMode &&
            SceneManager.GetActiveScene().name.StartsWith("BattleMode_", System.StringComparison.Ordinal);
        public static double BattleReleaseTime => singleton is BombermanNetworkManager manager &&
            manager.battleStartScene == SceneManager.GetActiveScene().name ? manager.battleReleaseTime : 0;
        public static bool BattleStartPending => IsBattleArena && (BattleReleaseTime == 0 || NetworkTime.time < BattleReleaseTime);

        void ResetBattleStart(string scene)
        {
            battleScreenReadyPeers.Clear();
            battleReleaseTime = 0;
            battleStartScene = System.IO.Path.GetFileNameWithoutExtension(scene);
        }

        void InitializeBattleStartServer()
        {
            NetworkServer.RegisterHandler<BattleScreenReady>((peer, message) =>
            {
                if (!IsBattleArena || message.scene != SceneManager.GetActiveScene().name ||
                    !peer.isAuthenticated || !peer.isReady || !playerIdByConnection.ContainsKey(peer.connectionId)) return;
                battleScreenReadyPeers.Add(peer.connectionId);
            });
        }

        void InitializeBattleStartClient()
        {
            NetworkClient.RegisterHandler<BattleStartSchedule>(message =>
            {
                if (message.scene != SceneManager.GetActiveScene().name) return;
                battleStartScene = message.scene;
                battleReleaseTime = message.releaseTime;
            });
        }

        public static void AcknowledgeBattleScreen()
        {
            if (IsBattleArena && NetworkClient.ready)
                NetworkClient.Send(new BattleScreenReady { scene = SceneManager.GetActiveScene().name });
        }

        public static bool HasAllBattlePlayerViews()
        {
            if (!IsBattleArena) return true;
            if (!NetworkClient.ready || NetworkClient.localPlayer == null) return false;
            var modes = global::SaveSystem.GetBattleModePlayerControlModes();
            var found = new HashSet<int>();
            foreach (var identity in NetworkClient.spawned.Values)
                if (identity != null && identity.TryGetComponent<NetworkPlayerSetup>(out var player)) found.Add(player.PlayerId);
            for (int i = 0; i < modes.Length; i++)
                if (modes[i] != BattleModePlayerControlMode.Off && !found.Contains(i + 1)) return false;
            return true;
        }

        void TickBattleStart()
        {
            if (!NetworkServer.active || !IsBattleArena || !roundPlayersReady || battleReleaseTime != 0) return;
            if (!AreBattleScreensReady(NetworkServer.connections.Values, battleScreenReadyPeers)) return;
            battleStartScene = SceneManager.GetActiveScene().name;
            battleReleaseTime = NetworkTime.time + 3.0;
            NetworkServer.SendToReady(new BattleStartSchedule { scene = battleStartScene, releaseTime = battleReleaseTime });
        }

        static bool AreBattleScreensReady(IEnumerable<NetworkConnectionToClient> peers, HashSet<int> presented)
        {
            int humans = 0;
            foreach (var peer in peers)
            {
                if (peer == null || !peer.isAuthenticated) continue;
                humans++;
                if (!peer.isReady || !presented.Contains(peer.connectionId)) return false;
            }
            return humans > 0;
        }
    }
}

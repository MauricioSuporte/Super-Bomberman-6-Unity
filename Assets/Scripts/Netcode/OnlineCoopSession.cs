using System;
using System.Collections;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Assets.Scripts.Netcode
{
    public struct CoopSessionState : NetworkMessage { public string bossRush; }

    public partial class BombermanNetworkManager
    {
        public static bool OnlineCoopPlayersReady
        {
            get
            {
                if (!IsCoopRoom || !NetworkServer.active) return true;
                if (singleton is not BombermanNetworkManager manager || manager.playerIdByConnection.Count == 0) return false;
                foreach (int id in manager.playerIdByConnection.Values)
                {
                    if (BossRushSession.IsActive && !BossRushSession.ShouldSpawnPlayer(id)) continue;
                    if (!manager.roundSpawnedPlayerIds.Contains(id)) return false;
                }
                return true;
            }
        }

        IEnumerator StopDedicatedRoomAfterDisconnect()
        {
            // KCP is iterating its connection collection during the disconnect callback.
            yield return null;
            StopServer();
            Application.Quit();
        }

        void InitializeCoopSessionClient()
        {
            InitializeCoopClient();
            NetworkClient.RegisterHandler<CoopSessionState>(message => BossRushSession.ApplyOnlineState(message.bossRush));
        }

        void ServerStartCoop(string initialScene = null)
        {
            if (!NetworkServer.active || !InLobbyScene() || RoomPlayers.Length < 1 || !IsCoopRoom) return;
            RoomCompleted = false; CompletedRunTime = 0;
            global::SaveSystem.BeginOnlineCoopSettings();
            PlayerPersistentStats.ResetGameplayPersistenceToBaseValues();
            PlayerPersistentStats.ApplyOnlineSelections(global::SaveSystem.BattleConfigurationData);
            GameSession.Instance?.EndBattleMatch();
            GameSession.Instance?.SetActivePlayerIds(RoomPlayers);
            PlayerPersistentStats.RollbackStage();
            BossRushSession.CancelRun();
            if (RoomMode == OnlineRoomMode.BossRush)
                BossRushSession.StartRun(BossRushDifficulty.NORMAL, new BossRushLoadoutPreset
                    { difficulty = BossRushDifficulty.NORMAL, life = 4, bombAmount = 3, explosionRadius = 3, speedUps = 2 });
            BroadcastCoopSession();
            BroadcastRoom();
            ServerChangeScene(initialScene ?? (RoomMode == OnlineRoomMode.BossRush ? BossRushSession.GetCurrentStageSceneName() : "Stage_1-1"));
        }

        void BroadcastCoopSession()
        {
            if (IsCoopRoom && NetworkServer.active)
                foreach (var peer in NetworkServer.connections.Values)
                    if (peer != null && peer.isAuthenticated && peer != NetworkServer.localConnection)
                        peer.Send(new CoopSessionState { bossRush = BossRushSession.CaptureOnlineState() });
        }

        // Used by existing campaign controllers; offline retains Unity's scene loading.
        public static bool RouteCoopScene(string target)
        {
            if (!IsCoopRoom) return false;
            if (!NetworkServer.active) return true;
            if (target == "END_SCREEN")
            {
                if (target == "END_SCREEN") RoomCompleted = true;
                BossRushSession.CancelRun();
                RouteOnlineMenuScene(target);
            }
            else if (target is "TitleScreen" or "BossRush" or "WorldMap" or "SkinSelect" or "SaveFileMenu")
                RouteOnlineMenuScene(target);
            else
            {
                if (singleton is BombermanNetworkManager manager) manager.BroadcastCoopSession();
                singleton.ServerChangeScene(target);
            }
            return true;
        }

        public static bool RouteCoopScene(int index) => RouteCoopScene(
            System.IO.Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(index)));

        public static void CoopStageCompleted(GameManager game, string nextStage, string completedStage)
        {
            if (!IsCoopStage || !NetworkServer.active || singleton is not BombermanNetworkManager manager) return;
            manager.StartCoroutine(manager.CoopStageCompletedRoutine(game, nextStage, completedStage));
        }

        IEnumerator CoopStageCompletedRoutine(GameManager game, string nextStage, string completedStage)
        {
            // Keep the existing portal/death choreography on the authoritative scene.
            yield return new WaitForSecondsRealtime(2f);
            if (game == null || !NetworkServer.active) yield break;
            if (RoomMode == OnlineRoomMode.BossRush)
            {
                BossRushSession.CapturePlayerSurvivalStateFromScene();
                BossRushSession.PauseTimer();
                if (BossRushSession.TryAdvanceToNextStage(out string next)) RouteCoopScene(next);
                else
                {
                    BossRushSession.CompleteRunAndStoreTime();
                    RoomCompleted = true; CompletedRunTime = BossRushSession.LastCompletedTime;
                    RouteOnlineMenuScene("BossRush");
                }
            }
            else
            {
                string current = string.IsNullOrEmpty(completedStage) ? SceneManager.GetActiveScene().name : completedStage;
                bool cleared = StageUnlockProgress.IsCleared(current);
                StageUnlockProgress.UnlockCurrentAndNext(current);
                SaveFileMenu.SaveCurrentProgressToActiveSlot();
                if (global::StageAssets.World3HallChipAssemblyController.IsChipStage(current))
                {
                    global::StageAssets.World3HallChipAssemblyController.PrepareReturn(current, !cleared);
                    RouteCoopScene(global::StageAssets.World3HallPortalSelectionController.SceneName);
                }
                else if (cleared || game.IsCompletedWorldBoss(current))
                {
                    if (game.IsCompletedWorldBoss(current)) WorldMapController.FocusStageOnNextLoad(nextStage);
                    RouteCoopScene("WorldMap");
                }
                else RouteCoopScene(string.IsNullOrEmpty(nextStage) ? "END_SCREEN" : nextStage);
            }
        }
    }
}

using System;
using Assets.Scripts.Netcode;
using UnityEngine;

public partial class GamePauseController
{
    [Serializable]
    public struct OnlinePauseState
    {
        public string scene;
        public bool paused;
        public int selection;
        public bool confirming;
        public int confirmation;
        public int target;
    }

    public OnlinePauseState CaptureOnlinePause()
    {
        return new OnlinePauseState { scene = CurrentSceneName, paused = IsPaused, selection = menuIndex,
            confirming = confirmReturn, confirmation = confirmIndex, target = (int)confirmTarget };
    }

    public void ApplyOnlinePause(OnlinePauseState state)
    {
        if (!NetSync.IsOnline || NetSync.IsServer || state.scene != CurrentSceneName) return;
        bool wasPaused = IsPaused;
        IsPaused = state.paused;
        Time.timeScale = state.paused ? 0f : 1f;
        menuIndex = state.selection;
        confirmReturn = state.confirming;
        confirmIndex = state.confirmation;
        confirmTarget = (PauseExitTarget)state.target;
        if (state.paused)
        {
            if (!wasPaused) GameMusicController.Instance?.PauseMusic();
            RefreshPauseUI();
        }
        else if (wasPaused) ResumeGame();
    }

    bool RouteOnlinePauseExit(string sceneName)
    {
        if (!NetSync.IsOnline) return false;
        if (!NetSync.IsServer) return true;
        ForceUnpause();
        if (sceneName == titleSceneName)
        {
            if (Mirror.NetworkManager.singleton is BombermanNetworkManager manager && manager.IsDedicatedServer)
                BombermanNetworkManager.ServerDisconnectRoomAdmin();
            else BombermanNetworkManager.LeaveRoom();
        }
        else if (BombermanNetworkManager.IsCoopRoom && (sceneName == bossRushSceneName || sceneName == worldMapSceneName))
            BombermanNetworkManager.RouteOnlineMenuScene(sceneName);
        else if (sceneName == battleModeMenuSceneName)
        {
            GameSession.Instance?.EndBattleMatch();
            BattleModeMenu.OpenDirectlyAtStageSelect = true;
            Mirror.NetworkManager.singleton.ServerChangeScene(BombermanNetworkManager.ConfigurationScene);
        }
        else BombermanNetworkManager.ServerStartNextRound();
        return true;
    }
}

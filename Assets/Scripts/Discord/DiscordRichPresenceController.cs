using System;
using System.Collections;
using Assets.Scripts.SaveSystem;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class DiscordRichPresenceController : MonoBehaviour
{
    const string ApplicationId = "1514671580762869981";
    const string LargeImageKey = "sb6_cover_v2";
    const string LargeImageText = "Super Bomberman 6";
    const string SmallImageKey = "sb6_logo";
    const string SmallImageText = "Super Bomberman 6";
    const float RetryConnectSeconds = 10f;
    const float RefreshSeconds = 15f;

    static DiscordRichPresenceController instance;

    DiscordIpcClient client;
    WaitForSeconds retryDelay;
    WaitForSeconds refreshDelay;
    long startedAtUnixSeconds;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null)
            return;

        var go = new GameObject(nameof(DiscordRichPresenceController));
        instance = go.AddComponent<DiscordRichPresenceController>();
        DontDestroyOnLoad(go);
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        retryDelay = new WaitForSeconds(RetryConnectSeconds);
        refreshDelay = new WaitForSeconds(RefreshSeconds);
        startedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        client = new DiscordIpcClient(ApplicationId);
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        StartCoroutine(ConnectionLoop());
        StartCoroutine(RefreshLoop());
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    void OnApplicationQuit()
    {
        if (client == null)
            return;

        client.ClearActivity();
        client.Dispose();
        client = null;
    }

    void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        UpdatePresence(scene.name);
    }

    IEnumerator ConnectionLoop()
    {
        while (enabled)
        {
            if (client != null && !client.IsConnected)
                client.TryConnect();

            UpdatePresence(SceneManager.GetActiveScene().name);
            yield return retryDelay;
        }
    }

    IEnumerator RefreshLoop()
    {
        while (enabled)
        {
            UpdatePresence(SceneManager.GetActiveScene().name);
            yield return refreshDelay;
        }
    }

    void UpdatePresence(string sceneName)
    {
        if (client == null)
            return;

        var activity = BuildActivity(sceneName);
        client.SetActivity(activity);
    }

    DiscordRichPresenceActivity BuildActivity(string sceneName)
    {
        int partyMax = GetPartyMax(sceneName);
        int playerCount = GetActivePlayerCount(partyMax);
        string playerText = playerCount == 1 ? "1 player" : playerCount + " players";

        var activity = new DiscordRichPresenceActivity
        {
            Details = "Playing Super Bomberman 6",
            State = "Getting ready to play",
            StartTimestamp = startedAtUnixSeconds,
            LargeImageKey = LargeImageKey,
            LargeImageText = LargeImageText,
            SmallImageKey = SmallImageKey,
            SmallImageText = SmallImageText,
            PartyId = "local-session",
            PartySize = partyMax > 0 ? playerCount : 0,
            PartyMax = partyMax
        };

        if (string.IsNullOrWhiteSpace(sceneName))
            return activity;

        if (sceneName.StartsWith("Stage_", StringComparison.Ordinal))
        {
            activity.Details = BossRushSession.IsActive
                ? "Boss Rush - " + FormatBossRushDifficulty(BossRushSession.CurrentDifficulty)
                : "Normal Game - " + FormatNormalGameDifficulty(SaveSystem.GetActiveNormalGameDifficulty());
            activity.State = FormatStageName(sceneName) + " - " + playerText;
            return activity;
        }

        if (sceneName.StartsWith("BattleMode_", StringComparison.Ordinal))
        {
            activity.Details = FormatBattleModeDetails();
            activity.State = FormatBattleModeName(sceneName) + " - " + playerText;
            return activity;
        }

        switch (sceneName)
        {
            case "TitleScreen":
                activity.Details = "At the title screen";
                activity.State = "Getting ready to play";
                break;

            case "SaveFileMenu":
                activity.Details = "Choosing a save file";
                activity.State = playerText;
                break;

            case "SkinSelect":
                activity.Details = "Choosing bombers";
                activity.State = playerText;
                break;

            case "WorldMap":
                activity.Details = "On the world map";
                activity.State = playerText;
                break;

            case "BossRush":
                activity.Details = "Boss Rush";
                activity.State = "Preparing a run";
                break;

            case "BattleModeMenu":
                activity.Details = "Battle Mode";
                activity.State = "Setting up a match";
                break;

            case "ControlsMenu":
                activity.Details = "Configuring controls";
                activity.State = "Editing input settings";
                break;

            case "Achievements":
                activity.Details = "Checking achievements";
                activity.State = "Viewing progress";
                break;
        }

        return activity;
    }

    static int GetActivePlayerCount(int partyMax)
    {
        int playerCount;

        if (BossRushSession.IsActive)
            playerCount = BossRushSession.RunPlayerCount;
        else
            playerCount = GameSession.Instance != null
                ? GameSession.Instance.ActivePlayerCount
                : 1;

        if (partyMax <= 0)
            return playerCount;

        return Mathf.Clamp(playerCount, 1, partyMax);
    }

    static int GetPartyMax(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
            return 0;

        if (sceneName.StartsWith("BattleMode_", StringComparison.Ordinal) ||
            string.Equals(sceneName, "BattleModeMenu", StringComparison.Ordinal))
            return GameSession.MaxPlayerId;

        if (sceneName.StartsWith("Stage_", StringComparison.Ordinal) ||
            string.Equals(sceneName, "SkinSelect", StringComparison.Ordinal) ||
            string.Equals(sceneName, "SaveFileMenu", StringComparison.Ordinal) ||
            string.Equals(sceneName, "WorldMap", StringComparison.Ordinal) ||
            string.Equals(sceneName, "BossRush", StringComparison.Ordinal))
            return 4;

        return 0;
    }

    static string FormatStageName(string sceneName)
    {
        return "Stage " + sceneName.Substring("Stage_".Length);
    }

    static string FormatBattleModeName(string sceneName)
    {
        return "Battle Stage " + sceneName.Substring("BattleMode_".Length);
    }

    static string FormatNormalGameDifficulty(NormalGameDifficulty difficulty)
    {
        return difficulty switch
        {
            NormalGameDifficulty.Hard => "Hard",
            NormalGameDifficulty.Hardcore => "Hardcore",
            _ => "Normal"
        };
    }

    static string FormatBossRushDifficulty(BossRushDifficulty difficulty)
    {
        return difficulty switch
        {
            BossRushDifficulty.EASY => "Easy",
            BossRushDifficulty.HARD => "Hard",
            BossRushDifficulty.NIGHTMARE => "Nightmare",
            _ => "Normal"
        };
    }

    static string FormatBattleModeDetails()
    {
        if (!HasBattleModeComPlayer())
            return "Battle Mode";

        BattleModeComputerLevel level = BattleModeRules.Instance != null
            ? BattleModeRules.Instance.CurrentComputerLevel
            : SaveSystem.GetBattleModeComputerLevel();

        return "Battle Mode - COM: " + FormatBattleModeComputerLevel(level);
    }

    static bool HasBattleModeComPlayer()
    {
        BattleModePlayerControlMode[] modes = SaveSystem.GetBattleModePlayerControlModes();
        for (int i = 0; i < modes.Length; i++)
        {
            if (modes[i] == BattleModePlayerControlMode.Com)
                return true;
        }

        return false;
    }

    static string FormatBattleModeComputerLevel(BattleModeComputerLevel level)
    {
        return level switch
        {
            BattleModeComputerLevel.Easy => "Easy",
            BattleModeComputerLevel.Hard => "Hard",
            _ => "Normal"
        };
    }
}

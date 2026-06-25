using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class NormalGameComRecordingAssist
{
    public const int DefaultPlayerCount = 4;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    const string EnabledEnvironmentVariable = "SB6_NORMAL_GAME_COM_DEMO";
    const string PlayerCountEnvironmentVariable = "SB6_NORMAL_GAME_COM_PLAYERS";
    const string EnabledArgument = "-normalGameComDemo";
    const string PlayerCountArgument = "-normalGameComPlayers";

    public static bool IsEnabled => IsNormalGameScene() && IsRequested();

    public static int PlayerCount => Mathf.Clamp(
        ReadConfiguredPlayerCount(),
        GameSession.MinPlayerId,
        GameSession.MaxPlayerId);

    public static bool IsComPlayer(int playerId) =>
        IsEnabled && playerId > GameSession.MinPlayerId && playerId <= PlayerCount;

    public static bool IsRecordingPlayer(int playerId) =>
        IsEnabled && playerId >= GameSession.MinPlayerId && playerId <= PlayerCount;

    static bool IsRequested()
    {
#if UNITY_EDITOR
        return true;
#else
        if (IsTruthy(Environment.GetEnvironmentVariable(EnabledEnvironmentVariable)))
            return true;

        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], EnabledArgument, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
#endif
    }

    static int ReadConfiguredPlayerCount()
    {
        if (TryReadPlayerCount(Environment.GetEnvironmentVariable(PlayerCountEnvironmentVariable), out int envCount))
            return envCount;

        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], PlayerCountArgument, StringComparison.OrdinalIgnoreCase) &&
                TryReadPlayerCount(args[i + 1], out int argCount))
            {
                return argCount;
            }
        }

        return DefaultPlayerCount;
    }

    static bool TryReadPlayerCount(string value, out int count)
    {
        if (int.TryParse(value, out count))
            return true;

        count = DefaultPlayerCount;
        return false;
    }

    static bool IsTruthy(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return value == "1" ||
               value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("on", StringComparison.OrdinalIgnoreCase);
    }
#else
    public static bool IsEnabled => false;
    public static int PlayerCount => DefaultPlayerCount;
    public static bool IsComPlayer(int playerId) => false;
    public static bool IsRecordingPlayer(int playerId) => false;
#endif

    public static bool CanRunComInCurrentScene(int playerId) =>
        IsBattleModeScene()
            ? SaveSystem.GetBattleModePlayerControlMode(playerId) == BattleModePlayerControlMode.Com
            : IsComPlayer(playerId);

    static bool IsNormalGameScene()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        return sceneName.StartsWith("Stage_", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsBattleModeScene()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        return sceneName.StartsWith("BattleMode_", StringComparison.OrdinalIgnoreCase);
    }
}

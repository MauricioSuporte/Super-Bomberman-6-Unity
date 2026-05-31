using UnityEngine;

public static class BattleModeComDiagnostics
{
    public enum LogLevel
    {
        Off = 0,
        Summary = 1,
        Verbose = 2
    }

    public static LogLevel DefaultLogLevel
    {
        get
        {
#if UNITY_EDITOR
            return LogLevel.Summary;
#else
            return LogLevel.Off;
#endif
        }
    }

    public static bool ShouldLog(LogLevel current, LogLevel required)
    {
        return current != LogLevel.Off && current >= required;
    }

    public static string FormatSummary(
        string scene,
        int frame,
        int playerId,
        BattleModeComputerLevel difficulty,
        BattleModeComActionType action,
        string target,
        Vector2Int position,
        string danger,
        string route,
        string reason,
        string nextInput)
    {
        return $"scene:{scene} frame:{frame} playerId:{playerId} difficulty:{difficulty} " +
               $"action:{action} target:{target} pos:{position} danger:{danger} route:{route} " +
               $"reason:{reason} input:{nextInput}";
    }
}

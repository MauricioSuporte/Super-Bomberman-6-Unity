using System.Collections.Generic;
using Assets.Scripts.SaveSystem;
using UnityEngine;

public sealed class GameSession : MonoBehaviour
{
    public const int MinPlayerId = 1;
    public const int MaxPlayerId = 6;
    public const int HardNormalGameStartingLives = 3;

    const int AllPlayersMask = (1 << MaxPlayerId) - 1;

    public static GameSession Instance { get; private set; }

    [Header("Players")]
    [Range(MinPlayerId, MaxPlayerId)]
    [SerializeField] private int activePlayerCount = 1;
    [SerializeField, HideInInspector] private int activePlayerMask = 1;

    [Header("Battle Match")]
    [SerializeField, HideInInspector] private bool battleMatchInProgress;
    [SerializeField, HideInInspector] private string battleMatchSceneName = string.Empty;
    [SerializeField, HideInInspector] private bool battleMatchUsesTeams;
    [SerializeField, HideInInspector] private int[] battleMatchWins = new int[MaxPlayerId];

    [Header("Normal Game Run")]
    [SerializeField, HideInInspector] private bool hardNormalGameRunInProgress;
    [SerializeField, HideInInspector] private int hardNormalGameRemainingLives = HardNormalGameStartingLives;

    public int ActivePlayerCount => CountPlayersInMask(GetEffectiveActivePlayerMask());
    public int ActivePlayerMask => GetEffectiveActivePlayerMask();
    public int HardNormalGameRemainingLives => Mathf.Clamp(hardNormalGameRemainingLives, 0, 9);

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        ClampAndSync();
    }

    void OnValidate()
    {
        ClampAndSync();
    }

    void Reset()
    {
        activePlayerCount = 1;
        activePlayerMask = PlayerIdToMask(1);
        ResetBattleMatch();
        ResetNormalGameLivesSession();
    }

    public void SetActivePlayerCount(int count)
    {
        SetActivePlayerMask(CreateMaskFromCount(count));
    }

    public void SetActivePlayerMask(int mask)
    {
        activePlayerMask = SanitizeMask(mask, activePlayerCount);
        activePlayerCount = CountPlayersInMask(activePlayerMask);
    }

    public void SetActivePlayerIds(IReadOnlyList<int> playerIds)
    {
        int mask = 0;

        if (playerIds != null)
        {
            for (int i = 0; i < playerIds.Count; i++)
            {
                if (!IsValidPlayerId(playerIds[i]))
                    continue;

                mask |= PlayerIdToMask(playerIds[i]);
            }
        }

        SetActivePlayerMask(mask);
    }

    public void SetPlayerActive(int playerId, bool active)
    {
        if (!IsValidPlayerId(playerId))
            return;

        int currentMask = GetEffectiveActivePlayerMask();
        int playerMask = PlayerIdToMask(playerId);
        int nextMask = active
            ? currentMask | playerMask
            : currentMask & ~playerMask;

        if (nextMask == 0)
            return;

        SetActivePlayerMask(nextMask);
    }

    public bool IsPlayerActive(int playerId)
    {
        if (!IsValidPlayerId(playerId))
            return false;

        return (GetEffectiveActivePlayerMask() & PlayerIdToMask(playerId)) != 0;
    }

    public bool TryGetFirstActivePlayerId(out int playerId)
    {
        for (int id = MinPlayerId; id <= MaxPlayerId; id++)
        {
            if (!IsPlayerActive(id))
                continue;

            playerId = id;
            return true;
        }

        playerId = MinPlayerId;
        return false;
    }

    public void BeginBattleMatch(string sceneName, bool usesTeams)
    {
        EnsureBattleMatchStorage();

        if (battleMatchInProgress &&
            battleMatchSceneName == (sceneName ?? string.Empty) &&
            battleMatchUsesTeams == usesTeams)
            return;

        battleMatchInProgress = true;
        battleMatchSceneName = sceneName ?? string.Empty;
        battleMatchUsesTeams = usesTeams;
        ClearBattleMatchWins();
    }

    public void EndBattleMatch()
    {
        ResetBattleMatch();
    }

    public void EnsureNormalGameLivesSession(NormalGameDifficulty difficulty)
    {
        if (difficulty != NormalGameDifficulty.Hard)
        {
            ResetNormalGameLivesSession();
            return;
        }

        if (hardNormalGameRunInProgress)
            return;

        hardNormalGameRunInProgress = true;
        hardNormalGameRemainingLives = HardNormalGameStartingLives;
    }

    public void ResetNormalGameLivesSession()
    {
        hardNormalGameRunInProgress = false;
        hardNormalGameRemainingLives = HardNormalGameStartingLives;
    }

    public bool TryConsumeHardNormalGameLife(out int remainingLives)
    {
        remainingLives = HardNormalGameRemainingLives;

        if (!hardNormalGameRunInProgress || remainingLives <= 0)
            return false;

        hardNormalGameRemainingLives = Mathf.Max(0, remainingLives - 1);
        remainingLives = hardNormalGameRemainingLives;
        return true;
    }

    public void AddBattleMatchWin(int playerId)
    {
        if (!IsValidPlayerId(playerId))
            return;

        EnsureBattleMatchStorage();
        battleMatchWins[playerId - 1] = Mathf.Clamp(battleMatchWins[playerId - 1] + 1, 0, 9);
    }

    public int GetBattleMatchWins(int playerId)
    {
        if (!IsValidPlayerId(playerId))
            return 0;

        EnsureBattleMatchStorage();
        return Mathf.Clamp(battleMatchWins[playerId - 1], 0, 9);
    }

    public void GetActivePlayerIds(List<int> results)
    {
        if (results == null)
            return;

        results.Clear();

        for (int id = MinPlayerId; id <= MaxPlayerId; id++)
        {
            if (IsPlayerActive(id))
                results.Add(id);
        }
    }

    public static bool IsValidPlayerId(int playerId)
    {
        return playerId >= MinPlayerId && playerId <= MaxPlayerId;
    }

    public static int CreateMaskFromCount(int count)
    {
        count = Mathf.Clamp(count, MinPlayerId, MaxPlayerId);

        int mask = 0;
        for (int id = MinPlayerId; id <= count; id++)
            mask |= PlayerIdToMask(id);

        return mask;
    }

    public static int CountPlayersInMask(int mask)
    {
        int sanitizedMask = mask & AllPlayersMask;
        int count = 0;

        for (int id = MinPlayerId; id <= MaxPlayerId; id++)
        {
            if ((sanitizedMask & PlayerIdToMask(id)) != 0)
                count++;
        }

        return Mathf.Max(1, count);
    }

    void ClampAndSync()
    {
        activePlayerCount = Mathf.Clamp(activePlayerCount, MinPlayerId, MaxPlayerId);
        activePlayerMask = SanitizeMask(activePlayerMask, activePlayerCount);
        activePlayerCount = CountPlayersInMask(activePlayerMask);
        EnsureBattleMatchStorage();
    }

    int GetEffectiveActivePlayerMask()
    {
        return activePlayerMask != 0
            ? activePlayerMask & AllPlayersMask
            : CreateMaskFromCount(activePlayerCount);
    }

    static int SanitizeMask(int mask, int fallbackCount)
    {
        int sanitizedMask = mask & AllPlayersMask;

        if (sanitizedMask == 0)
            sanitizedMask = CreateMaskFromCount(fallbackCount);

        return sanitizedMask;
    }

    static int PlayerIdToMask(int playerId)
    {
        int clampedPlayerId = Mathf.Clamp(playerId, MinPlayerId, MaxPlayerId);
        return 1 << (clampedPlayerId - 1);
    }

    void EnsureBattleMatchStorage()
    {
        if (battleMatchWins == null || battleMatchWins.Length != MaxPlayerId)
            battleMatchWins = new int[MaxPlayerId];
    }

    void ClearBattleMatchWins()
    {
        EnsureBattleMatchStorage();

        for (int i = 0; i < battleMatchWins.Length; i++)
            battleMatchWins[i] = 0;
    }

    void ResetBattleMatch()
    {
        battleMatchInProgress = false;
        battleMatchSceneName = string.Empty;
        battleMatchUsesTeams = false;
        ClearBattleMatchWins();
    }
}

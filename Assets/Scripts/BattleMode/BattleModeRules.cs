using UnityEngine;

[DisallowMultipleComponent]
public sealed class BattleModeRules : MonoBehaviour
{
    public enum MatchMode
    {
        SingleMatch = 0,
        TagMatch = 1
    }

    public enum RoundTimerMode
    {
        OneMinute = 0,
        TwoMinutes = 1,
        ThreeMinutes = 2,
        FourMinutes = 3,
        FiveMinutes = 4,
        Infinite = 5,
        UmE10 = 6
    }

    public enum BattleMusicSelection
    {
        Random = 0,
        SB1Battle = 1,
        SB2Battle1 = 2,
        SB2Battle2 = 3,
        SB2Battle3 = 4,
        SB3Battle = 5,
        SB4Battle = 6,
        SB5Battle1 = 7,
        SB5Battle2 = 8
    }

    public enum TeamId
    {
        Blue = 1,
        Red = 2,
        Green = 3
    }

    [System.Serializable]
    public struct PlayerTeamEntry
    {
        [Range(GameSession.MinPlayerId, GameSession.MaxPlayerId)]
        public int playerId;
        public TeamId teamId;
    }

    public static BattleModeRules Instance { get; private set; }

    [Header("Match")]
    [SerializeField] private MatchMode matchMode = MatchMode.SingleMatch;
    [SerializeField, Min(1)] private int victoriesToWinMatch = 3;
    [SerializeField] private RoundTimerMode roundTimer = RoundTimerMode.ThreeMinutes;
    [SerializeField] private BattleMusicSelection battleMusic = BattleMusicSelection.Random;
    [SerializeField] private bool enableRevengeBomber;
    [SerializeField] private bool enableSuddenDeath = true;

    [Header("Teams")]
    [SerializeField] private PlayerTeamEntry[] playerTeams = new PlayerTeamEntry[GameSession.MaxPlayerId];

    public MatchMode CurrentMatchMode => matchMode;
    public bool UsesTeams => matchMode == MatchMode.TagMatch;
    public int VictoriesToWinMatch => Mathf.Max(1, victoriesToWinMatch);
    public RoundTimerMode CurrentRoundTimerMode => roundTimer;
    public BattleMusicSelection CurrentBattleMusic => battleMusic;
    public bool UsesRoundTimer => roundTimer != RoundTimerMode.Infinite;
    public float RoundTimerSeconds => GetRoundTimerSeconds(roundTimer);
    public bool EnableRevengeBomber => enableRevengeBomber;
    public bool EnableSuddenDeath => enableSuddenDeath;

    void Awake()
    {
        if (Instance != null && Instance != this)
            Debug.LogWarning("Multiple BattleModeRules instances found in scene. Keeping the most recent one.", this);

        Instance = this;
        EnsureEntries();
        NotifyTeamsConfigurationChanged();
    }

    void OnEnable()
    {
        Instance = this;
        EnsureEntries();
        NotifyTeamsConfigurationChanged();
    }

    void OnDisable()
    {
        if (Instance == this)
            Instance = null;
    }

    void OnValidate()
    {
        EnsureEntries();
        NotifyTeamsConfigurationChanged();
    }

    void Reset()
    {
        EnsureEntries();
        NotifyTeamsConfigurationChanged();
    }

    public TeamId GetTeamForPlayer(int playerId)
    {
        if (!GameSession.IsValidPlayerId(playerId))
            return TeamId.Blue;

        EnsureEntries();

        for (int i = 0; i < playerTeams.Length; i++)
        {
            if (playerTeams[i].playerId == playerId)
                return playerTeams[i].teamId;
        }

        return GetDefaultTeamForPlayer(playerId);
    }

    public static TeamId GetDefaultTeamForPlayer(int playerId)
    {
        int normalizedIndex = Mathf.Abs(playerId - 1) % 3;
        return (TeamId)(normalizedIndex + 1);
    }

    public static float GetRoundTimerSeconds(RoundTimerMode timerMode)
    {
        switch (timerMode)
        {
            case RoundTimerMode.OneMinute:
                return 60f;
            case RoundTimerMode.TwoMinutes:
                return 120f;
            case RoundTimerMode.ThreeMinutes:
                return 180f;
            case RoundTimerMode.FourMinutes:
                return 240f;
            case RoundTimerMode.FiveMinutes:
                return 300f;
            case RoundTimerMode.UmE10:
                return 70f;
            default:
                return Mathf.Infinity;
        }
    }

    void EnsureEntries()
    {
        if (playerTeams == null || playerTeams.Length != GameSession.MaxPlayerId)
            playerTeams = new PlayerTeamEntry[GameSession.MaxPlayerId];

        for (int i = 0; i < playerTeams.Length; i++)
        {
            playerTeams[i].playerId = i + 1;

            if ((int)playerTeams[i].teamId < (int)TeamId.Blue || (int)playerTeams[i].teamId > (int)TeamId.Green)
                playerTeams[i].teamId = GetDefaultTeamForPlayer(i + 1);
        }
    }

    void NotifyTeamsConfigurationChanged()
    {
        BattleModeTeams teams = GetComponent<BattleModeTeams>();
        if (teams != null)
            teams.RefreshFromRules();
    }
}

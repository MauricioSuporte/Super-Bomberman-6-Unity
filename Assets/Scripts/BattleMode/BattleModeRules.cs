using UnityEngine;

public enum BattleModePlayerControlMode
{
    Man = 0,
    Com = 1,
    Off = 2
}

public enum BattleModeComputerLevel
{
    Easy = 0,
    Normal = 1,
    Hard = 2
}

public enum BattleModeSuddenDeathSetting
{
    Off = 0,
    On = 1,
    Random = 2
}

[DisallowMultipleComponent]
public sealed class BattleModeRules : MonoBehaviour
{
    [System.Serializable]
    public sealed class StartingLoadout
    {
        [Min(1)] public int bombAmount = 1;
        [Range(1, PlayerPersistentStats.MaxExplosionRadius)] public int fireLevel = 2;
        [Range(1, 9)] public int speedLevel = 2;
        [Min(1)] public int life = 1;
        public ItemType[] powerups;
        public MountedType mountedLouie = MountedType.None;
        public ItemType[] queuedEggs;
    }

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

    [System.Serializable]
    public struct PlayerStartingLoadoutEntry
    {
        [Range(GameSession.MinPlayerId, GameSession.MaxPlayerId)]
        public int playerId;
        public bool overrideSharedLoadout;
        public StartingLoadout loadout;
    }

    public static BattleModeRules Instance { get; private set; }

    [Header("Match")]
    [SerializeField] private MatchMode matchMode = MatchMode.SingleMatch;
    [SerializeField] private BattleModeComputerLevel computerLevel = BattleModeComputerLevel.Normal;
    [SerializeField, Min(1)] private int victoriesToWinMatch = 3;
    [SerializeField] private RoundTimerMode roundTimer = RoundTimerMode.ThreeMinutes;
    [SerializeField] private BattleMusicSelection battleMusic = BattleMusicSelection.Random;
    [SerializeField] private bool enableRevengeBomber;
    [SerializeField] private bool enableSuddenDeath = true;
    private BattleModeSuddenDeathSetting suddenDeathSetting = BattleModeSuddenDeathSetting.Random;
    [SerializeField] private bool enableItemDropsAfterDeath = true;

    [Header("Teams")]
    [SerializeField] private PlayerTeamEntry[] playerTeams = new PlayerTeamEntry[GameSession.MaxPlayerId];

    [Header("Starting Loadout")]
    [SerializeField] private StartingLoadout sharedStartingLoadout = new();
    [SerializeField] private PlayerStartingLoadoutEntry[] playerStartingLoadouts = new PlayerStartingLoadoutEntry[GameSession.MaxPlayerId];

    public MatchMode CurrentMatchMode => matchMode;
    public bool UsesTeams => matchMode == MatchMode.TagMatch;
    public BattleModeComputerLevel CurrentComputerLevel => computerLevel;
    public int VictoriesToWinMatch => Mathf.Max(1, victoriesToWinMatch);
    public RoundTimerMode CurrentRoundTimerMode => roundTimer;
    public BattleMusicSelection CurrentBattleMusic => battleMusic;
    public bool UsesRoundTimer => roundTimer != RoundTimerMode.Infinite;
    public float RoundTimerSeconds => GetRoundTimerSeconds(roundTimer);
    public bool EnableRevengeBomber => enableRevengeBomber;
    public bool EnableSuddenDeath => enableSuddenDeath;
    public BattleModeSuddenDeathSetting SuddenDeathSetting => suddenDeathSetting;
    public bool UseReducedSuddenDeath =>
        suddenDeathSetting == BattleModeSuddenDeathSetting.Off;
    public bool EnableItemDropsAfterDeath => enableItemDropsAfterDeath;

    void Awake()
    {
        if (Instance != null && Instance != this)
            Debug.LogWarning("Multiple BattleModeRules instances found in scene. Keeping the most recent one.", this);

        Instance = this;
        ApplySavedMatchMode();
        EnsureEntries();
        EnsureStartingLoadoutEntries();
        NotifyTeamsConfigurationChanged();
    }

    void OnEnable()
    {
        Instance = this;
        ApplySavedMatchMode();
        EnsureEntries();
        EnsureStartingLoadoutEntries();
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
        EnsureStartingLoadoutEntries();
        NotifyTeamsConfigurationChanged();
    }

    void Reset()
    {
        EnsureEntries();
        EnsureStartingLoadoutEntries();
        NotifyTeamsConfigurationChanged();
    }

    void ApplySavedMatchMode()
    {
        matchMode = SaveSystem.GetBattleModeMatchMode();
        computerLevel = SaveSystem.GetBattleModeComputerLevel();
        victoriesToWinMatch = SaveSystem.GetBattleModeBattlesToWin();
        roundTimer = SaveSystem.GetBattleModeRoundTimerMode();
        enableRevengeBomber = SaveSystem.GetBattleModeRevengeBomberEnabled();
        suddenDeathSetting = SaveSystem.GetBattleModeSuddenDeathSetting();
        enableSuddenDeath = ResolveSuddenDeath(suddenDeathSetting);
        ApplySavedTeams();
    }

    static bool ResolveSuddenDeath(BattleModeSuddenDeathSetting setting)
    {
        return setting switch
        {
            BattleModeSuddenDeathSetting.Off => false,
            BattleModeSuddenDeathSetting.On => true,
            _ => Random.value >= 0.5f
        };
    }

    void ApplySavedTeams()
    {
        BattleModeRules.TeamId[] savedTeams = SaveSystem.GetBattleModePlayerTeams();
        EnsureEntries();

        for (int i = 0; i < playerTeams.Length; i++)
        {
            playerTeams[i].playerId = i + 1;
            playerTeams[i].teamId = savedTeams != null && i < savedTeams.Length
                ? savedTeams[i]
                : GetDefaultTeamForPlayer(i + 1);
        }
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

    public void ApplyStartingLoadout(int playerId, PlayerPersistentStats.PlayerState state)
    {
        if (state == null)
            return;

        StartingLoadout loadout = GetStartingLoadoutForPlayer(playerId);
        ApplyStartingLoadout(loadout, state);
    }

    StartingLoadout GetStartingLoadoutForPlayer(int playerId)
    {
        EnsureStartingLoadoutEntries();

        for (int i = 0; i < playerStartingLoadouts.Length; i++)
        {
            PlayerStartingLoadoutEntry entry = playerStartingLoadouts[i];
            if (entry.playerId != playerId || !entry.overrideSharedLoadout)
                continue;

            if (entry.loadout == null)
                entry.loadout = CreateDefaultStartingLoadout();

            return entry.loadout;
        }

        return sharedStartingLoadout ?? CreateDefaultStartingLoadout();
    }

    static void ApplyStartingLoadout(StartingLoadout loadout, PlayerPersistentStats.PlayerState state)
    {
        loadout ??= CreateDefaultStartingLoadout();

        state.Life = Mathf.Max(1, loadout.life);
        state.BombAmount = Mathf.Clamp(loadout.bombAmount, 1, PlayerPersistentStats.MaxBombAmount);
        state.ExplosionRadius = Mathf.Clamp(loadout.fireLevel, 1, PlayerPersistentStats.MaxExplosionRadius);
        state.SpeedInternal = PlayerPersistentStats.SpeedLevelToInternal(loadout.speedLevel);
        state.QueuedEggs.Clear();

        if (loadout.mountedLouie != MountedType.None)
            state.MountedLouie = loadout.mountedLouie;

        ApplyPowerups(loadout.powerups, state);

        if (loadout.queuedEggs != null)
        {
            for (int i = 0; i < loadout.queuedEggs.Length; i++)
            {
                ItemType egg = loadout.queuedEggs[i];
                if (IsEgg(egg) && state.QueuedEggs.Count < 8)
                    state.QueuedEggs.Add(egg);
            }
        }
    }

    static void ApplyPowerups(ItemType[] powerups, PlayerPersistentStats.PlayerState state)
    {
        if (powerups == null)
            return;

        for (int i = 0; i < powerups.Length; i++)
            ApplyPowerup(powerups[i], state);
    }

    static void ApplyPowerup(ItemType type, PlayerPersistentStats.PlayerState state)
    {
        switch (type)
        {
            case ItemType.ExtraBomb:
                state.BombAmount = Mathf.Min(state.BombAmount + 1, PlayerPersistentStats.MaxBombAmount);
                break;
            case ItemType.BlastRadius:
                state.ExplosionRadius = Mathf.Min(state.ExplosionRadius + 1, PlayerPersistentStats.MaxExplosionRadius);
                break;
            case ItemType.SpeedIncrese:
                state.SpeedInternal = PlayerPersistentStats.ClampSpeedInternal(state.SpeedInternal + PlayerPersistentStats.SpeedStep);
                break;
            case ItemType.Heart:
                state.Life = Mathf.Max(1, state.Life + 1);
                break;
            case ItemType.BombKick:
                state.CanKickBombs = true;
                state.CanPassBombs = false;
                break;
            case ItemType.BombPass:
                state.CanPassBombs = true;
                state.CanKickBombs = false;
                break;
            case ItemType.BombPunch:
                state.CanPunchBombs = true;
                break;
            case ItemType.PowerGlove:
                state.HasPowerGlove = true;
                break;
            case ItemType.DestructiblePass:
                state.CanPassDestructibles = true;
                break;
            case ItemType.FullFire:
                state.HasFullFire = true;
                break;
            case ItemType.PierceBomb:
                SetBombType(state, pierce: true);
                break;
            case ItemType.ControlBomb:
                SetBombType(state, control: true);
                break;
            case ItemType.PowerBomb:
                SetBombType(state, power: true);
                break;
            case ItemType.RubberBomb:
                SetBombType(state, rubber: true);
                break;
            case ItemType.MagnetBomb:
                SetBombType(state, magnet: true);
                break;
            default:
                if (IsEgg(type))
                    ApplyEgg(type, state);
                break;
        }
    }

    static void ApplyEgg(ItemType egg, PlayerPersistentStats.PlayerState state)
    {
        MountedType louie = EggToLouie(egg);
        if (louie == MountedType.None)
            return;

        if (state.MountedLouie == MountedType.None)
        {
            state.MountedLouie = louie;
            return;
        }

        if (state.QueuedEggs.Count < 8)
            state.QueuedEggs.Add(egg);
    }

    static void SetBombType(
        PlayerPersistentStats.PlayerState state,
        bool pierce = false,
        bool control = false,
        bool power = false,
        bool rubber = false,
        bool magnet = false)
    {
        state.HasPierceBombs = pierce;
        state.HasControlBombs = control;
        state.HasPowerBomb = power;
        state.HasRubberBombs = rubber;
        state.HasMagnetBomb = magnet;
    }

    static MountedType EggToLouie(ItemType type)
    {
        return type switch
        {
            ItemType.BlueLouieEgg => MountedType.Blue,
            ItemType.BlackLouieEgg => MountedType.Black,
            ItemType.PurpleLouieEgg => MountedType.Purple,
            ItemType.GreenLouieEgg => MountedType.Green,
            ItemType.YellowLouieEgg => MountedType.Yellow,
            ItemType.PinkLouieEgg => MountedType.Pink,
            ItemType.RedLouieEgg => MountedType.Red,
            _ => MountedType.None
        };
    }

    static bool IsEgg(ItemType type)
    {
        return EggToLouie(type) != MountedType.None;
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

    void EnsureStartingLoadoutEntries()
    {
        sharedStartingLoadout ??= CreateDefaultStartingLoadout();

        if (playerStartingLoadouts == null || playerStartingLoadouts.Length != GameSession.MaxPlayerId)
            playerStartingLoadouts = new PlayerStartingLoadoutEntry[GameSession.MaxPlayerId];

        for (int i = 0; i < playerStartingLoadouts.Length; i++)
        {
            playerStartingLoadouts[i].playerId = i + 1;
            playerStartingLoadouts[i].loadout ??= CreateDefaultStartingLoadout();
        }
    }

    static StartingLoadout CreateDefaultStartingLoadout()
    {
        return new StartingLoadout();
    }

    void NotifyTeamsConfigurationChanged()
    {
        BattleModeTeams teams = GetComponent<BattleModeTeams>();
        if (teams != null)
            teams.RefreshFromRules();
    }
}

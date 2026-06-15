using System;
using System.Collections;
using System.Collections.Generic;
using Assets.Scripts.SaveSystem;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public class GameManager : MonoBehaviour
{
    public enum BattleModeHiddenDropEntryKind
    {
        Item,
        RandomEggsMin,
        RandomEggsMax
    }

    public readonly struct BattleModeHiddenDropEntry
    {
        public BattleModeHiddenDropEntry(BattleModeHiddenDropEntryKind kind, ItemType itemType = ItemType.ExtraBomb)
        {
            Kind = kind;
            ItemType = itemType;
        }

        public BattleModeHiddenDropEntryKind Kind { get; }
        public ItemType ItemType { get; }
    }

    public static readonly BattleModeHiddenDropEntry[] BattleModeHiddenDropEntries =
    {
        new(BattleModeHiddenDropEntryKind.Item, ItemType.ExtraBomb),
        new(BattleModeHiddenDropEntryKind.Item, ItemType.BlastRadius),
        new(BattleModeHiddenDropEntryKind.Item, ItemType.SpeedIncrese),
        new(BattleModeHiddenDropEntryKind.Item, ItemType.BombKick),
        new(BattleModeHiddenDropEntryKind.Item, ItemType.BombPunch),
        new(BattleModeHiddenDropEntryKind.Item, ItemType.PierceBomb),
        new(BattleModeHiddenDropEntryKind.Item, ItemType.ControlBomb),
        new(BattleModeHiddenDropEntryKind.Item, ItemType.PowerBomb),
        new(BattleModeHiddenDropEntryKind.Item, ItemType.RubberBomb),
        new(BattleModeHiddenDropEntryKind.Item, ItemType.MagnetBomb),
        new(BattleModeHiddenDropEntryKind.Item, ItemType.FullFire),
        new(BattleModeHiddenDropEntryKind.Item, ItemType.BombPass),
        new(BattleModeHiddenDropEntryKind.Item, ItemType.DestructiblePass),
        new(BattleModeHiddenDropEntryKind.Item, ItemType.InvincibleSuit),
        new(BattleModeHiddenDropEntryKind.Item, ItemType.Heart),
        new(BattleModeHiddenDropEntryKind.Item, ItemType.PowerGlove),
        new(BattleModeHiddenDropEntryKind.RandomEggsMin),
        new(BattleModeHiddenDropEntryKind.RandomEggsMax),
        new(BattleModeHiddenDropEntryKind.Item, ItemType.Skull)
    };

    public static readonly MountedType[] BattleModeRandomEggMountTypes =
    {
        MountedType.Blue,
        MountedType.Black,
        MountedType.Purple,
        MountedType.Green,
        MountedType.Yellow,
        MountedType.Pink,
        MountedType.Red,
        MountedType.Mole,
        MountedType.Tank
    };

    static readonly WaitForSecondsRealtime waitNextStageDelay = new(3f);
    static readonly WaitForSecondsRealtime waitBattleVictoryCheckDelay = new(0.5f);
    static readonly WaitForSecondsRealtime waitBattleVictoryDelay = new(1f);
    const float BattleDrawPreFadeDuration = 1f;
    const float BattleTimeUpDelayAfterTimerExpiredSeconds = 1f;
    static readonly WaitForSecondsRealtime waitBattleDrawPreFadeDelay = new(BattleDrawPreFadeDuration);
    const float BattleRoundWinShowDelay = 1f;
    static readonly WaitForSecondsRealtime waitRoundWinScoreboardDelay = new(BattleRoundWinShowDelay);
    const float BattleWinMatchPreFadeDuration = 0.5f;
    static readonly WaitForSecondsRealtime waitWinMatchPreFadeDuration = new(BattleWinMatchPreFadeDuration);
    static readonly WaitForSecondsRealtime waitWinMatchBlackScreenDelay = new(2f);
    const float BattleVictoryFadeDuration = 3f;
    const float BattleRoundWinFinalFadeDuration = 0.5f;
    const float NormalGameDeathFadeDuration = 2f;
    const string BattleVictorySfxResourcesPath = "Sounds/SB5 Sound Effects (48)";
    const string TitleScreenSceneName = "TitleScreen";
    const string BattleModeMenuSceneName = "BattleModeMenu";
    static AudioClip battleVictorySfx;

    public static GameManager Instance { get; private set; }

    public int EnemiesAlive { get; private set; }
    public int PendingHiddenEnemies { get; private set; }
    public bool IsBattleRoundResolutionTriggered =>
        IsBattleModeScene() && (restartingRound || endStageTriggered || battleTimerExpired);

    public event Action OnAllEnemiesDefeated;

    [Header("Auto Prefab Loading (Resources)")]
    [SerializeField] private string portalResourcesPath = "Portal/EndStagePortal";
    private EndStagePortal portalPrefab;

    [Header("Hidden Objects Amounts")]
    [Min(0)] public int portalAmount = 0;
    [Min(0)] public int extraBombAmount = 0;
    [Min(0)] public int blastRadiusAmount = 0;
    [Min(0)] public int speedIncreaseAmount = 0;
    [Min(0)] public int kickBombAmount = 0;
    [Min(0)] public int punchBombAmount = 0;
    [Min(0)] public int pierceBombAmount = 0;
    [Min(0)] public int controlBombAmount = 0;
    [Min(0)] public int powerBombAmount = 0;
    [Min(0)] public int rubberBombAmount = 0;
    [Min(0)] public int magnetBombAmount = 0;
    [Min(0)] public int fullFireAmount = 0;
    [Min(0)] public int bombPassAmount = 0;
    [Min(0)] public int destructiblePassAmount = 0;
    [Min(0)] public int invincibleSuitAmount = 0;
    [Min(0)] public int heartAmount = 0;
    [Min(0)] public int oneUpAmount = 0;
    [Min(0)] public int powerGloveAmount = 0;
    [Min(0)] public int blueLouieEggAmount = 0;
    [Min(0)] public int blackLouieEggAmount = 0;
    [Min(0)] public int purpleLouieEggAmount = 0;
    [Min(0)] public int greenLouieEggAmount = 0;
    [Min(0)] public int yellowLouieEggAmount = 0;
    [Min(0)] public int pinkLouieEggAmount = 0;
    [Min(0)] public int redLouieEggAmount = 0;
    [Min(0)] public int clockAmount = 0;
    [Min(0)] public int skullAmount = 0;

    [Header("Random Eggs")]
    [Min(0)] public int randomEggsMin = 0;
    [Min(0)] public int randomEggsMax = 0;

    [Header("Stage")]
    public Tilemap destructibleTilemap;
    public Tilemap indestructibleTilemap;

    [Header("Stage Flow")]
    public string nextStageSceneName;

    [Header("Stage Prefabs (Optional on Boss Stages)")]
    public Destructible destructiblePrefab;

    [Header("Ground")]
    public Tilemap groundTilemap;
    public TileBase groundTile;
    public TileBase groundShadowTile;
    public TileBase indestructibleGroundShadowTile;
    public TileBase shadowDestructibleTile;
    public TileBase[] groundShadowIgnoredTiles;
    [Tooltip("Tiles indestrutiveis que aplicam sombra no Ground em Y-1. Se vazio, qualquer tile indestrutivel aplica sombra como antes.")]
    public TileBase[] indestructibleGroundShadowCasterTiles;

    [Header("Auto Resolve (Stage Tilemaps)")]
    [SerializeField] private bool autoResolveStageTilemaps = true;

    [Header("Round Restart")]
    [SerializeField] private float restartAfterDeathSeconds = 4f;
    [SerializeField] private string bossRushSceneName = "BossRush";

    private int totalDestructibleBlocks;
    private int destroyedDestructibleBlocks;

    private readonly Dictionary<int, GameObject> orderToSpawn = new();
    private readonly Dictionary<GameObject, MountedType> hiddenMountPrefabTypes = new();
    private readonly Dictionary<Vector3Int, TileBase> pendingIndestructibleShadowCells = new();
    private readonly Dictionary<Vector3Int, TileBase> shadowedDestructibleOriginalTiles = new();
    private readonly List<IGroundTileShadowHandler> groundTileShadowHandlers = new();

    private bool restartingRound;
    private bool endStageTriggered;
    private bool battleTimerExpired;
    private float battleTimeRemainingSeconds = Mathf.Infinity;
    private float battleTimerExpiredElapsedSeconds;
    private bool hasBattleTimeLimit;

    private bool pendingEnemyCheck;
    private Coroutine enemyCheckRoutine;
    private Coroutine battleVictoryCheckRoutine;
    private readonly List<MovementController> winningBattleSurvivorsBuffer = new();
    private readonly List<ItemType> battleDroppedItemsBuffer = new();
    private readonly List<Vector3Int> battleDropCellsBuffer = new();

    private readonly HashSet<Vector3Int> reservedItemSpawnCells = new();
    private readonly HashSet<Vector3Int> pendingHiddenItemCells = new();
    private Coroutine clearReservedItemSpawnCellsRoutine;

    [Header("Destructible Tile Resolver (optional, auto-find)")]
    [SerializeField] private DestructibleTileResolver destructibleTileResolver;

    public float BattleTimeRemainingSeconds => Mathf.Max(0f, battleTimeRemainingSeconds);
    public bool HasBattleTimeLimit => hasBattleTimeLimit;

    void Awake()
    {
        Instance = this;

        if (autoResolveStageTilemaps)
            ResolveStageTilemapsIfNeeded();

        portalPrefab = Resources.Load<EndStagePortal>(portalResourcesPath);

        ResolveGroundTileShadowHandlers();
        ResolveDestructibleTileResolver();
    }

    void Start()
    {
        endStageTriggered = false;
        EndStageVoiceSfx.ResetPlaybackState();

        PlayerPersistentStats.EnsureSessionBooted();

        string currentSceneName = SceneManager.GetActiveScene().name;
        BossRushSession.NotifySceneLoaded(currentSceneName);

        NormalGameDifficulty normalGameDifficulty = SaveSystem.GetActiveNormalGameDifficulty();

        if (!BossRushSession.IsActive && !IsBattleModeScene() && GameSession.Instance != null)
            GameSession.Instance.EnsureNormalGameLivesSession(normalGameDifficulty);

        if (IsBattleModeScene() && GameSession.Instance != null)
            GameSession.Instance.BeginBattleMatch(currentSceneName, IsBattleModeTeamMatch());

        if (IsBattleModeScene())
        {
            PlayerPersistentStats.ResetBattleModeLoadouts(BattleModeRules.Instance);
            ApplySavedBattleModeHiddenItemAmounts();
        }

        if (BossRushSession.IsActive && BossRushSession.IsBossRushScene(currentSceneName))
            BossRushTimerPresenter.EnsureInScene();

        EnemiesAlive = FindObjectsByType<EnemyMovementController>(FindObjectsInactive.Exclude).Length;

        SetupHiddenObjects();
        ApplyDestructibleShadows();
        InitializeBattleRoundTimer();

        CountPendingHiddenEnemiesFromTiles();

        ScheduleEnemyCheckNextFrame();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Update()
    {
        UpdateBattleRoundTimer();
    }

    bool IsConfiguredPlayerActive(int playerId)
    {
        playerId = Mathf.Clamp(playerId, GameSession.MinPlayerId, GameSession.MaxPlayerId);

        if (BossRushSession.IsActive)
            return BossRushSession.IsRunPlayer(playerId);

        if (GameSession.Instance != null)
            return GameSession.Instance.IsPlayerActive(playerId);

        return playerId == GameSession.MinPlayerId;
    }

    List<PlayerIdentity> GetOrderedPlayerIdentities(bool includeInactive)
    {
        List<PlayerIdentity> result = new();
        FindObjectsInactive inactiveMode = includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude;
        PlayerIdentity[] ids = FindObjectsByType<PlayerIdentity>(inactiveMode);

        for (int i = 0; i < ids.Length; i++)
        {
            PlayerIdentity id = ids[i];
            if (id == null)
                continue;

            int playerId = Mathf.Clamp(id.playerId, GameSession.MinPlayerId, GameSession.MaxPlayerId);
            if (!IsConfiguredPlayerActive(playerId))
                continue;

            result.Add(id);
        }

        result.Sort((a, b) => a.playerId.CompareTo(b.playerId));
        return result;
    }

    bool TryGetRuntimePlayerComponents(
        PlayerIdentity identity,
        out MovementController movement,
        out BombController bomb)
    {
        movement = null;
        bomb = null;

        if (identity == null)
            return false;

        if (!identity.TryGetComponent(out movement))
            movement = identity.GetComponentInChildren<MovementController>(true);

        if (!identity.TryGetComponent(out bomb))
            bomb = identity.GetComponentInChildren<BombController>(true);

        if (movement == null)
            return false;

        if (!movement.CompareTag("Player"))
            return false;

        return true;
    }

    void CaptureAllPlayersForStageEnd()
    {
        List<PlayerIdentity> ids = GetOrderedPlayerIdentities(includeInactive: false);

        for (int i = 0; i < ids.Count; i++)
        {
            PlayerIdentity identity = ids[i];
            if (identity == null)
                continue;

            if (!TryGetRuntimePlayerComponents(identity, out var movement, out var bomb))
                continue;

            if (!movement.gameObject.activeInHierarchy)
                continue;

            if (movement.isDead)
                continue;

            if (movement.TryGetComponent<PowerGloveAbility>(out var glove) && glove != null)
                glove.DestroyHeldBombIfHolding();

            PlayerPersistentStats.StageCaptureFromRuntime(movement, bomb);
        }
    }

    public bool AreAllEnemiesCleared()
    {
        return EnemiesAlive <= 0 && PendingHiddenEnemies <= 0;
    }

    public void NotifyEnemySpawned(int amount = 1)
    {
        int add = Mathf.Max(1, amount);
        EnemiesAlive += add;

        ScheduleEnemyCheckNextFrame();
    }

    public void NotifyEnemyDied()
    {
        EnemiesAlive = Mathf.Max(EnemiesAlive - 1, 0);
        ScheduleEnemyCheckNextFrame();
    }

    public void NotifyHiddenEnemySpawnedFromBlock(int amount = 1)
    {
        int a = Mathf.Max(1, amount);

        PendingHiddenEnemies = Mathf.Max(PendingHiddenEnemies - a, 0);
        EnemiesAlive += a;

        ScheduleEnemyCheckNextFrame();
    }

    public void NotifyHiddenEnemyCancelledFromBlock(int amount = 1)
    {
        int a = Mathf.Max(1, amount);

        PendingHiddenEnemies = Mathf.Max(PendingHiddenEnemies - a, 0);

        ScheduleEnemyCheckNextFrame();
    }

    public void ForceEnemyCheckNextFrame()
    {
        ScheduleEnemyCheckNextFrame();
    }

    private void ScheduleEnemyCheckNextFrame()
    {
        if (pendingEnemyCheck)
            return;

        pendingEnemyCheck = true;

        if (enemyCheckRoutine != null)
            StopCoroutine(enemyCheckRoutine);

        enemyCheckRoutine = StartCoroutine(EnemyCheckNextFrameRoutine());
    }

    private IEnumerator EnemyCheckNextFrameRoutine()
    {
        yield return null;

        pendingEnemyCheck = false;
        enemyCheckRoutine = null;

        if (AreAllEnemiesCleared())
        {
            EnemiesAlive = 0;
            PendingHiddenEnemies = 0;
            OnAllEnemiesDefeated?.Invoke();
        }
    }

    private void ResolveDestructibleTileResolver()
    {
        if (destructibleTileResolver != null)
            return;

        destructibleTileResolver = FindAnyObjectByType<DestructibleTileResolver>();
        if (destructibleTileResolver != null)
            return;

        if (destructibleTilemap != null)
            destructibleTileResolver = destructibleTilemap.GetComponentInParent<DestructibleTileResolver>(true);

        if (destructibleTileResolver != null)
            return;

        GameObject stage = GameObject.Find("Stage");
        if (stage != null)
            destructibleTileResolver = stage.GetComponentInChildren<DestructibleTileResolver>(true);
    }

    private void CountPendingHiddenEnemiesFromTiles()
    {
        PendingHiddenEnemies = 0;

        if (destructibleTilemap == null)
            return;

        ResolveDestructibleTileResolver();
        if (destructibleTileResolver == null)
            return;

        BoundsInt bounds = destructibleTilemap.cellBounds;

        foreach (Vector3Int pos in bounds.allPositionsWithin)
        {
            TileBase tile = destructibleTilemap.GetTile(pos);
            if (tile == null)
                continue;

            if (!destructibleTileResolver.TryGetHandler(tile, out var handler) || handler == null)
                continue;

            if (handler is EnemySpawnDestructibleTileHandler)
                PendingHiddenEnemies++;
        }
    }

    public bool HasDestructiblesInStage()
    {
        if (destructibleTilemap == null)
            return false;

        BoundsInt bounds = destructibleTilemap.cellBounds;
        foreach (Vector3Int pos in bounds.allPositionsWithin)
        {
            if (destructibleTilemap.GetTile(pos) != null)
                return true;
        }

        return false;
    }

    void ResolveStageTilemapsIfNeeded()
    {
        if (destructibleTilemap != null && indestructibleTilemap != null && groundTilemap != null)
            return;

        GameObject gameplayRoot = GameObject.Find("GameplayRoot");
        Transform stageRoot = gameplayRoot != null ? gameplayRoot.transform.Find("Stage") : null;

        if (stageRoot != null)
        {
            if (destructibleTilemap == null)
                destructibleTilemap = FindTilemapUnder(stageRoot, "Destructibles");

            if (indestructibleTilemap == null)
                indestructibleTilemap = FindTilemapUnder(stageRoot, "Indestructibles");

            if (groundTilemap == null)
                groundTilemap = FindTilemapUnder(stageRoot, "Ground");
        }

        if (destructibleTilemap == null)
            destructibleTilemap = FindTilemapByNameFallback("Destructibles");

        if (indestructibleTilemap == null)
            indestructibleTilemap = FindTilemapByNameFallback("Indestructibles");

        if (groundTilemap == null)
            groundTilemap = FindTilemapByNameFallback("Ground");
    }

    Tilemap FindTilemapUnder(Transform root, string childName)
    {
        if (root == null)
            return null;

        Transform t = root.Find(childName);
        if (t == null)
            return null;

        Tilemap tm = t.GetComponent<Tilemap>();
        if (tm != null)
            return tm;

        return t.GetComponentInChildren<Tilemap>(true);
    }

    Tilemap FindTilemapByNameFallback(string exactName)
    {
        Tilemap[] all = FindObjectsByType<Tilemap>(FindObjectsInactive.Include);

        for (int i = 0; i < all.Length; i++)
        {
            Tilemap tm = all[i];
            if (tm != null && tm.name == exactName)
                return tm;
        }

        for (int i = 0; i < all.Length; i++)
        {
            Tilemap tm = all[i];
            if (tm != null && tm.name != null &&
                tm.name.IndexOf(exactName, StringComparison.OrdinalIgnoreCase) >= 0)
                return tm;
        }

        return null;
    }

    void SetupHiddenObjects()
    {
        destroyedDestructibleBlocks = 0;
        totalDestructibleBlocks = 0;
        orderToSpawn.Clear();
        hiddenMountPrefabTypes.Clear();

        if (destructibleTilemap == null)
            return;

        AutoItemDatabase.BuildIfNeeded();

        BoundsInt bounds = destructibleTilemap.cellBounds;

        foreach (Vector3Int pos in bounds.allPositionsWithin)
        {
            if (destructibleTilemap.GetTile(pos) != null)
                totalDestructibleBlocks++;
        }

        if (totalDestructibleBlocks <= 0)
            return;

        List<int> indices = new(totalDestructibleBlocks);
        for (int i = 1; i <= totalDestructibleBlocks; i++)
            indices.Add(i);

        for (int i = indices.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (indices[j], indices[i]) = (indices[i], indices[j]);
        }

        int cursor = 0;

        void TryAssignPortal(int amount)
        {
            if (portalPrefab == null || amount <= 0)
                return;

            for (int i = 0; i < amount && cursor < indices.Count; i++)
                orderToSpawn[indices[cursor++]] = portalPrefab.gameObject;
        }

        void TryAssignItem(ItemType type, int amount)
        {
            if (amount <= 0)
                return;

            ItemPickup prefab = AutoItemDatabase.Get(type);
            if (prefab == null)
                return;

            for (int i = 0; i < amount && cursor < indices.Count; i++)
                orderToSpawn[indices[cursor++]] = prefab.gameObject;
        }

        TryAssignPortal(portalAmount);

        TryAssignRandomEggs(indices, ref cursor);

        TryAssignItem(ItemType.ExtraBomb, extraBombAmount);
        TryAssignItem(ItemType.BlastRadius, blastRadiusAmount);
        TryAssignItem(ItemType.SpeedIncrese, speedIncreaseAmount);
        TryAssignItem(ItemType.BombKick, kickBombAmount);
        TryAssignItem(ItemType.BombPunch, punchBombAmount);
        TryAssignItem(ItemType.PierceBomb, pierceBombAmount);
        TryAssignItem(ItemType.ControlBomb, controlBombAmount);
        TryAssignItem(ItemType.PowerBomb, powerBombAmount);
        TryAssignItem(ItemType.RubberBomb, rubberBombAmount);
        TryAssignItem(ItemType.MagnetBomb, magnetBombAmount);
        TryAssignItem(ItemType.FullFire, fullFireAmount);
        TryAssignItem(ItemType.BombPass, bombPassAmount);
        TryAssignItem(ItemType.DestructiblePass, destructiblePassAmount);
        TryAssignItem(ItemType.InvincibleSuit, invincibleSuitAmount);
        TryAssignItem(ItemType.Heart, heartAmount);
        NormalGameDifficulty normalGameDifficulty = SaveSystem.GetActiveNormalGameDifficulty();
        if (normalGameDifficulty == NormalGameDifficulty.Hard)
            TryAssignItem(ItemType.OneUp, oneUpAmount);
        TryAssignItem(ItemType.PowerGlove, powerGloveAmount);

        TryAssignItem(ItemType.BlueLouieEgg, blueLouieEggAmount);
        TryAssignItem(ItemType.BlackLouieEgg, blackLouieEggAmount);
        TryAssignItem(ItemType.PurpleLouieEgg, purpleLouieEggAmount);
        TryAssignItem(ItemType.GreenLouieEgg, greenLouieEggAmount);
        TryAssignItem(ItemType.YellowLouieEgg, yellowLouieEggAmount);
        TryAssignItem(ItemType.PinkLouieEgg, pinkLouieEggAmount);
        TryAssignItem(ItemType.RedLouieEgg, redLouieEggAmount);

        TryAssignItem(ItemType.Clock, clockAmount);
        TryAssignItem(ItemType.Skull, skullAmount);
    }

    public static int[] GetDefaultBattleModeHiddenItemAmounts()
    {
        int[] result = new int[BattleModeHiddenDropEntries.Length];

        for (int i = 0; i < result.Length; i++)
            result[i] = GetDefaultBattleModeHiddenEntryAmount(BattleModeHiddenDropEntries[i]);

        return result;
    }

    public static int[] GetDefaultBattleModeLouieAmounts()
    {
        int[] result = new int[BattleModeRandomEggMountTypes.Length];
        for (int i = 0; i < result.Length; i++)
            result[i] = 1;

        return result;
    }

    static int GetDefaultBattleModeHiddenEntryAmount(BattleModeHiddenDropEntry entry)
    {
        return entry.Kind switch
        {
            BattleModeHiddenDropEntryKind.RandomEggsMin => 4,
            BattleModeHiddenDropEntryKind.RandomEggsMax => 8,
            BattleModeHiddenDropEntryKind.Item => GetDefaultBattleModeHiddenItemAmount(entry.ItemType),
            _ => 0
        };
    }

    static int GetDefaultBattleModeHiddenItemAmount(ItemType type)
    {
        return type switch
        {
            ItemType.ExtraBomb => 12,
            ItemType.BlastRadius => 10,
            ItemType.SpeedIncrese => 8,
            ItemType.BombKick => 4,
            ItemType.BombPunch => 3,
            ItemType.PierceBomb => 1,
            ItemType.ControlBomb => 0,
            ItemType.PowerBomb => 1,
            ItemType.RubberBomb => 1,
            ItemType.MagnetBomb => 1,
            ItemType.FullFire => 1,
            ItemType.BombPass => 1,
            ItemType.DestructiblePass => 1,
            ItemType.InvincibleSuit => 0,
            ItemType.Heart => 0,
            ItemType.PowerGlove => 3,
            ItemType.Skull => 2,
            _ => 0
        };
    }

    void ApplySavedBattleModeHiddenItemAmounts()
    {
        int[] amounts = SaveSystem.GetBattleModeItemAmounts(GetDefaultBattleModeHiddenItemAmounts());
        ApplyBattleModeHiddenItemAmounts(amounts);
    }

    void ApplyBattleModeHiddenItemAmounts(IReadOnlyList<int> amounts)
    {
        if (amounts == null)
            return;

        for (int i = 0; i < BattleModeHiddenDropEntries.Length && i < amounts.Count; i++)
            SetBattleModeHiddenEntryAmount(BattleModeHiddenDropEntries[i], amounts[i]);
    }

    void SetBattleModeHiddenEntryAmount(BattleModeHiddenDropEntry entry, int amount)
    {
        amount = Mathf.Clamp(amount, 0, 99);
        switch (entry.Kind)
        {
            case BattleModeHiddenDropEntryKind.RandomEggsMin:
                randomEggsMin = amount;
                break;
            case BattleModeHiddenDropEntryKind.RandomEggsMax:
                randomEggsMax = Mathf.Max(randomEggsMin, amount);
                break;
            default:
                SetBattleModeHiddenItemAmount(entry.ItemType, amount);
                break;
        }
    }

    void SetBattleModeHiddenItemAmount(ItemType type, int amount)
    {
        amount = Mathf.Clamp(amount, 0, 99);
        switch (type)
        {
            case ItemType.ExtraBomb: extraBombAmount = amount; break;
            case ItemType.BlastRadius: blastRadiusAmount = amount; break;
            case ItemType.SpeedIncrese: speedIncreaseAmount = amount; break;
            case ItemType.BombKick: kickBombAmount = amount; break;
            case ItemType.BombPunch: punchBombAmount = amount; break;
            case ItemType.PierceBomb: pierceBombAmount = amount; break;
            case ItemType.ControlBomb: controlBombAmount = amount; break;
            case ItemType.PowerBomb: powerBombAmount = amount; break;
            case ItemType.RubberBomb: rubberBombAmount = amount; break;
            case ItemType.MagnetBomb: magnetBombAmount = amount; break;
            case ItemType.FullFire: fullFireAmount = amount; break;
            case ItemType.BombPass: bombPassAmount = amount; break;
            case ItemType.DestructiblePass: destructiblePassAmount = amount; break;
            case ItemType.InvincibleSuit: invincibleSuitAmount = amount; break;
            case ItemType.Heart: heartAmount = amount; break;
            case ItemType.OneUp: oneUpAmount = amount; break;
            case ItemType.PowerGlove: powerGloveAmount = amount; break;
            case ItemType.BlueLouieEgg: blueLouieEggAmount = amount; break;
            case ItemType.BlackLouieEgg: blackLouieEggAmount = amount; break;
            case ItemType.PurpleLouieEgg: purpleLouieEggAmount = amount; break;
            case ItemType.GreenLouieEgg: greenLouieEggAmount = amount; break;
            case ItemType.YellowLouieEgg: yellowLouieEggAmount = amount; break;
            case ItemType.PinkLouieEgg: pinkLouieEggAmount = amount; break;
            case ItemType.RedLouieEgg: redLouieEggAmount = amount; break;
            case ItemType.Clock: clockAmount = amount; break;
            case ItemType.Skull: skullAmount = amount; break;
        }
    }

    static int GetBattleModeStageIndexFromScene(string sceneName)
    {
        const string prefix = "BattleMode_";
        if (string.IsNullOrWhiteSpace(sceneName) ||
            !sceneName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        return int.TryParse(sceneName[prefix.Length..], out int index)
            ? Mathf.Clamp(index, 1, 15)
            : 0;
    }

    public GameObject GetSpawnForDestroyedBlock(Vector3Int cell)
    {
        if (totalDestructibleBlocks <= 0)
            return null;

        destroyedDestructibleBlocks++;
        int order = destroyedDestructibleBlocks;

        if (!orderToSpawn.TryGetValue(order, out var prefabGo))
            return null;

        if (!TryReservePendingHiddenItemCell(cell))
            return null;

        return prefabGo;
    }

    public void CheckWinState()
    {
        if (restartingRound || endStageTriggered)
            return;

        EvaluatePlayerWinState();
    }

    IEnumerator RestartRoundRoutine()
    {
        float restartDelay = Mathf.Max(restartAfterDeathSeconds, NormalGameDeathFadeDuration);
        yield return new WaitForSecondsRealtime(restartDelay);

        if (!BossRushSession.IsActive &&
            SaveSystem.GetActiveNormalGameDifficulty() == NormalGameDifficulty.Hard)
        {
            if (GameSession.Instance == null ||
                !GameSession.Instance.TryConsumeHardNormalGameLife(out int remainingLives))
            {
                yield break;
            }
        }

        GamePauseController.ClearPauseFlag();
        Time.timeScale = 1f;
        PlayerPersistentStats.RollbackStage();

        StagePreIntroPlayersWalk.SkipOnNextLoad();

        if (BossRushSession.IsActive)
        {
            BossRushSession.CancelRun();
            SceneManager.LoadScene(bossRushSceneName);
            yield break;
        }

        Scene current = SceneManager.GetActiveScene();
        SceneManager.LoadScene(current.buildIndex);
    }

    IEnumerator RestartBattleStageRoutine()
    {
        yield return waitNextStageDelay;

        GamePauseController.ClearPauseFlag();
        Time.timeScale = 1f;
        PlayerPersistentStats.RollbackStage();

        StagePreIntroPlayersWalk.SkipOnNextLoad();

        Scene current = SceneManager.GetActiveScene();
        SceneManager.LoadScene(current.buildIndex);
    }

    public void EndStage()
    {
        if (endStageTriggered)
            return;

        endStageTriggered = true;

        if (IsBattleModeScene())
        {
            StartCoroutine(RestartBattleStageRoutine());
            return;
        }

        string currentSceneName = SceneManager.GetActiveScene().name;

        CaptureAllPlayersForStageEnd();

        StageUnlockProgress.UnlockCurrentAndNext(currentSceneName);

        PlayerPersistentStats.CommitStage();
        SaveFileMenu.SaveCurrentProgressToActiveSlot();

        if (BossRushSession.IsActive)
        {
            BossRushSession.CapturePlayerSurvivalStateFromScene();
            BossRushSession.PauseTimer();
            StartCoroutine(LoadNextBossRushStageRoutine());
            return;
        }

        if (!string.IsNullOrEmpty(nextStageSceneName) && nextStageSceneName == "END_SCREEN")
        {
            StartCoroutine(ShowEndingAfterDelayRoutine());
            return;
        }

        if (string.IsNullOrEmpty(nextStageSceneName))
        {
            StartCoroutine(ShowEndingAfterDelayRoutine());
            return;
        }

        StartCoroutine(LoadNextStageRoutine());
    }

    IEnumerator LoadNextStageRoutine()
    {
        yield return waitNextStageDelay;

        GamePauseController.ClearPauseFlag();
        Time.timeScale = 1f;

        EnsureStage1_2PlayersHaveMinimumExplosionRadius();
        SceneManager.LoadScene(nextStageSceneName);
    }

    void EnsureStage1_2PlayersHaveMinimumExplosionRadius()
    {
        if (nextStageSceneName != "Stage_1-2")
            return;

        for (int playerId = 1; playerId <= 6; playerId++)
        {
            var state = PlayerPersistentStats.Get(playerId);
            if (state.ExplosionRadius == 1)
                state.ExplosionRadius = 2;
        }
    }

    IEnumerator LoadNextBossRushStageRoutine()
    {
        yield return waitNextStageDelay;

        GamePauseController.ClearPauseFlag();
        Time.timeScale = 1f;

        if (BossRushSession.TryAdvanceToNextStage(out var nextBossRushScene))
        {
            StagePreIntroPlayersWalk.SkipOnNextLoad();
            SceneManager.LoadScene(nextBossRushScene);
            yield break;
        }

        BossRushSession.CompleteRunAndStoreTime();
        SceneManager.LoadScene(bossRushSceneName);
    }

    IEnumerator ShowEndingAfterDelayRoutine()
    {
        yield return waitNextStageDelay;

        GamePauseController.ClearPauseFlag();
        Time.timeScale = 1f;

        if (StageIntroTransition.Instance != null)
            StageIntroTransition.Instance.StartEndingScreenSequence();
        else if (EndingScreenController.Instance != null)
            yield return EndingScreenController.Instance.Play(null);
    }

    void ApplyDestructibleShadows()
    {
        ResolveGroundTileShadowHandlers();

        if (destructibleTilemap != null && shadowDestructibleTile != null)
        {
            BoundsInt destructibleBounds = destructibleTilemap.cellBounds;
            foreach (Vector3Int destructibleCell in destructibleBounds.allPositionsWithin)
                RefreshDestructibleShadowAt(destructibleCell);
        }

        if (groundTilemap != null &&
            groundTile != null &&
            HasAnyGroundShadowTile())
        {
            BoundsInt bounds = groundTilemap.cellBounds;
            foreach (Vector3Int groundCell in bounds.allPositionsWithin)
                RefreshGroundShadowAt(groundCell);
        }
    }

    public void OnDestructibleDestroyed(Vector3Int cell)
    {
        Vector3Int below = new(cell.x, cell.y - 1, cell.z);
        shadowedDestructibleOriginalTiles.Remove(cell);
        RefreshGroundShadowAt(cell);
        RefreshGroundShadowAt(below);
    }

    public void OnIndestructiblePlaced(Vector3Int cell)
    {
        Vector3Int below = new(cell.x, cell.y - 1, cell.z);
        RefreshDestructibleShadowAt(below);
        RefreshDestructibleShadowAt(cell);
        RefreshGroundShadowAt(below);
        RefreshGroundShadowAt(cell);
    }

    public void OnIndestructibleDropStarted(Vector3Int cell)
    {
        OnIndestructibleDropStarted(cell, null);
    }

    public void OnIndestructibleDropStarted(Vector3Int cell, TileBase tile)
    {
        bool added = !pendingIndestructibleShadowCells.ContainsKey(cell);
        pendingIndestructibleShadowCells[cell] = tile;
        if (added)
            OnIndestructiblePlaced(cell);
    }

    public void OnIndestructibleDropFinished(Vector3Int cell)
    {
        bool removed = pendingIndestructibleShadowCells.Remove(cell);
        if (removed)
            OnIndestructiblePlaced(cell);
    }

    public void ClearPendingIndestructibleDrops()
    {
        if (pendingIndestructibleShadowCells.Count == 0)
            return;

        List<Vector3Int> cells = new(pendingIndestructibleShadowCells.Keys);
        pendingIndestructibleShadowCells.Clear();

        for (int i = 0; i < cells.Count; i++)
            OnIndestructiblePlaced(cells[i]);
    }

    void RefreshDestructibleShadowAt(Vector3Int destructibleCell)
    {
        if (destructibleTilemap == null || shadowDestructibleTile == null)
            return;

        TileBase currentDestructible = destructibleTilemap.GetTile(destructibleCell);
        bool hasIndestructibleAbove = HasIndestructibleShadowCasterAt(new Vector3Int(
            destructibleCell.x,
            destructibleCell.y + 1,
            destructibleCell.z));

        if (hasIndestructibleAbove)
        {
            if (currentDestructible == null)
                return;

            if (currentDestructible == shadowDestructibleTile)
                return;

            if (!shadowedDestructibleOriginalTiles.ContainsKey(destructibleCell))
                shadowedDestructibleOriginalTiles[destructibleCell] = currentDestructible;

            destructibleTilemap.SetTile(destructibleCell, shadowDestructibleTile);
            destructibleTilemap.RefreshTile(destructibleCell);
            return;
        }

        if (!shadowedDestructibleOriginalTiles.TryGetValue(destructibleCell, out TileBase originalTile))
            return;

        shadowedDestructibleOriginalTiles.Remove(destructibleCell);
        if (currentDestructible != shadowDestructibleTile)
            return;

        destructibleTilemap.SetTile(destructibleCell, originalTile);
        destructibleTilemap.RefreshTile(destructibleCell);
    }

    void RefreshGroundShadowAt(Vector3Int groundCell)
    {
        if (groundTilemap == null ||
            groundTile == null ||
            !HasAnyGroundShadowTile())
            return;

        TileBase currentGround = groundTilemap.GetTile(groundCell);
        TileBase shadowTile = GetGroundShadowTileForCasterAbove(groundCell);

        if (currentGround == null)
            return;

        if (IsGroundShadowIgnoredTile(currentGround))
            return;

        TileBase customShadowTile = null;
        TileBase customRestoredTile = null;
        bool hasCustomShadowTile = shadowTile != null &&
            TryGetGroundShadowHandlerShadowTile(groundCell, currentGround, shadowTile, out customShadowTile);
        bool hasCustomRestoredTile = shadowTile == null &&
            TryGetGroundShadowHandlerRestoredTile(groundCell, currentGround, groundTile, out customRestoredTile);

        bool refreshable = hasCustomShadowTile ||
            hasCustomRestoredTile ||
            IsShadowRefreshableGroundTile(currentGround);
        if (!refreshable && shadowTile == null)
            return;

        if (shadowTile != null)
        {
            TileBase targetShadowTile = hasCustomShadowTile ? customShadowTile : shadowTile;
            if (targetShadowTile != null && currentGround != targetShadowTile)
                groundTilemap.SetTile(groundCell, targetShadowTile);
        }
        else if (refreshable && currentGround != groundTile)
        {
            TileBase targetGroundTile = hasCustomRestoredTile ? customRestoredTile : groundTile;
            if (targetGroundTile != null && currentGround != targetGroundTile)
                groundTilemap.SetTile(groundCell, targetGroundTile);
        }
    }

    void ResolveGroundTileShadowHandlers()
    {
        groundTileShadowHandlers.Clear();

        MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour is IGroundTileShadowHandler handler)
                groundTileShadowHandlers.Add(handler);
        }
    }

    bool TryGetGroundShadowHandlerShadowTile(
        Vector3Int cell,
        TileBase currentGroundTile,
        TileBase defaultShadowTile,
        out TileBase shadowTile)
    {
        shadowTile = null;

        for (int i = 0; i < groundTileShadowHandlers.Count; i++)
        {
            IGroundTileShadowHandler handler = groundTileShadowHandlers[i];
            if (handler == null)
                continue;

            if (handler.TryGetShadowTile(cell, currentGroundTile, defaultShadowTile, out shadowTile) && shadowTile != null)
                return true;
        }

        return false;
    }

    bool TryGetGroundShadowHandlerRestoredTile(
        Vector3Int cell,
        TileBase currentGroundTile,
        TileBase defaultGroundTile,
        out TileBase restoredTile)
    {
        restoredTile = null;

        for (int i = 0; i < groundTileShadowHandlers.Count; i++)
        {
            IGroundTileShadowHandler handler = groundTileShadowHandlers[i];
            if (handler == null)
                continue;

            if (handler.TryGetRestoredTile(cell, currentGroundTile, defaultGroundTile, out restoredTile) && restoredTile != null)
                return true;
        }

        return false;
    }

    bool IsShadowRefreshableGroundTile(TileBase tile)
    {
        return
            tile == groundTile ||
            tile == groundShadowTile ||
            tile == indestructibleGroundShadowTile;
    }

    bool HasAnyGroundShadowTile()
    {
        return
            groundShadowTile != null ||
            indestructibleGroundShadowTile != null;
    }

    bool IsGroundShadowIgnoredTile(TileBase tile)
    {
        if (tile == null || groundShadowIgnoredTiles == null)
            return false;

        for (int i = 0; i < groundShadowIgnoredTiles.Length; i++)
        {
            TileBase ignoredTile = groundShadowIgnoredTiles[i];
            if (ignoredTile == null)
                continue;

            if (tile == ignoredTile)
                return true;

            if (IsSameAnimatedTileRuntimeVariant(tile, ignoredTile))
                return true;
        }

        return false;
    }

    bool IsSameAnimatedTileRuntimeVariant(TileBase tile, TileBase ignoredTile)
    {
        if (tile is not AnimatedTile || ignoredTile is not AnimatedTile)
            return false;

        string tileName = NormalizeRuntimeAnimatedTileName(tile.name);
        string ignoredName = NormalizeRuntimeAnimatedTileName(ignoredTile.name);
        if (string.IsNullOrEmpty(tileName) || string.IsNullOrEmpty(ignoredName))
            return false;

        return string.Equals(tileName, ignoredName, StringComparison.Ordinal) ||
               tileName.StartsWith(ignoredName + "_", StringComparison.Ordinal);
    }

    static string NormalizeRuntimeAnimatedTileName(string tileName)
    {
        if (string.IsNullOrEmpty(tileName))
            return string.Empty;

        const string cloneSuffix = "(Clone)";
        if (tileName.EndsWith(cloneSuffix, StringComparison.Ordinal))
            tileName = tileName.Substring(0, tileName.Length - cloneSuffix.Length).TrimEnd();

        const string runtimeSuffix = "_Runtime";
        if (tileName.EndsWith(runtimeSuffix, StringComparison.Ordinal))
            tileName = tileName.Substring(0, tileName.Length - runtimeSuffix.Length);

        string[] stateSuffixes =
        {
            "_ClockwiseSlow",
            "_ClockwiseFast",
            "_CounterClockwiseSlow",
            "_CounterClockwiseFast",
        };

        for (int i = 0; i < stateSuffixes.Length; i++)
        {
            string suffix = stateSuffixes[i];
            if (tileName.EndsWith(suffix, StringComparison.Ordinal))
                return tileName.Substring(0, tileName.Length - suffix.Length);
        }

        return tileName;
    }

    TileBase GetGroundShadowTileForCasterAbove(Vector3Int groundCell)
    {
        Vector3Int above = new(groundCell.x, groundCell.y + 1, groundCell.z);

        if (HasIndestructibleGroundShadowCasterAt(above))
        {
            return indestructibleGroundShadowTile != null ? indestructibleGroundShadowTile : groundShadowTile;
        }

        TileBase destructibleTile = destructibleTilemap != null ? destructibleTilemap.GetTile(above) : null;
        if (destructibleTile != null)
        {
            if (IsGroundShadowIgnoredTile(destructibleTile))
                return null;

            return groundShadowTile;
        }

        return null;
    }

    bool HasIndestructibleShadowCasterAt(Vector3Int cell)
    {
        if (indestructibleTilemap != null && indestructibleTilemap.GetTile(cell) != null)
            return true;

        return pendingIndestructibleShadowCells.ContainsKey(cell);
    }

    bool HasIndestructibleGroundShadowCasterAt(Vector3Int cell)
    {
        TileBase tile = indestructibleTilemap != null ? indestructibleTilemap.GetTile(cell) : null;
        if (tile != null)
            return IsIndestructibleGroundShadowCasterTile(tile);

        if (!pendingIndestructibleShadowCells.TryGetValue(cell, out TileBase pendingTile))
            return false;

        return pendingTile == null
            ? !HasIndestructibleGroundShadowCasterFilter()
            : IsIndestructibleGroundShadowCasterTile(pendingTile);
    }

    bool IsIndestructibleGroundShadowCasterTile(TileBase tile)
    {
        if (tile == null)
            return false;

        if (!HasIndestructibleGroundShadowCasterFilter())
            return true;

        for (int i = 0; i < indestructibleGroundShadowCasterTiles.Length; i++)
        {
            TileBase casterTile = indestructibleGroundShadowCasterTiles[i];
            if (casterTile == null)
                continue;

            if (tile == casterTile)
                return true;

            if (IsSameAnimatedTileRuntimeVariant(tile, casterTile))
                return true;
        }

        return false;
    }

    bool HasIndestructibleGroundShadowCasterFilter()
    {
        if (indestructibleGroundShadowCasterTiles == null)
            return false;

        for (int i = 0; i < indestructibleGroundShadowCasterTiles.Length; i++)
        {
            if (indestructibleGroundShadowCasterTiles[i] != null)
                return true;
        }

        return false;
    }

    public void NotifyPlayerDeathStarted(MovementController deadPlayer)
    {
        if (restartingRound || endStageTriggered)
            return;

        if (IsBattleModeScene())
        {
            TryDropBattleItemsForDeadPlayer(deadPlayer);
            ScheduleBattleVictoryCheck();
            return;
        }

        EvaluatePlayerWinState();
    }

    void ScheduleBattleVictoryCheck()
    {
        if (battleVictoryCheckRoutine != null)
            StopCoroutine(battleVictoryCheckRoutine);

        battleVictoryCheckRoutine = StartCoroutine(BattleVictoryCheckRoutine());
    }

    IEnumerator BattleVictoryCheckRoutine()
    {
        yield return waitBattleVictoryCheckDelay;
        battleVictoryCheckRoutine = null;

        if (restartingRound || endStageTriggered)
            yield break;

        EvaluatePlayerWinState();
    }

    void EvaluatePlayerWinState()
    {
        int aliveNotDead = 0;
        MovementController survivingPlayer = null;
        List<PlayerIdentity> ids = GetOrderedPlayerIdentities(includeInactive: false);

        if (IsBattleModeScene() && IsBattleModeTeamMatch())
        {
            if (TryGetWinningTeamSurvivors(ids, winningBattleSurvivorsBuffer, out survivingPlayer))
            {
                TriggerBattleVictorySequence(winningBattleSurvivorsBuffer, survivingPlayer);
                return;
            }
        }

        for (int i = 0; i < ids.Count; i++)
        {
            PlayerIdentity id = ids[i];
            if (id == null)
                continue;

            if (!TryGetRuntimePlayerComponents(id, out var movement, out _))
                continue;

            if (!movement.gameObject.activeInHierarchy)
                continue;

            if (movement.isDead)
                continue;

            aliveNotDead++;
            survivingPlayer = movement;
        }

        if (IsBattleModeScene() && aliveNotDead == 1)
        {
            winningBattleSurvivorsBuffer.Clear();

            if (survivingPlayer != null)
                winningBattleSurvivorsBuffer.Add(survivingPlayer);

            TriggerBattleVictorySequence(winningBattleSurvivorsBuffer, survivingPlayer);
            return;
        }

        if (aliveNotDead <= 0)
        {
            if (IsBattleModeScene())
            {
                TriggerBattleDrawSequence(showTimeUp: false);
                return;
            }

            bool shouldRestartRound = ShouldRestartNormalGameAfterPartyDefeat();
            restartingRound = true;

            if (GameMusicController.Instance != null &&
                GameMusicController.Instance.deathMusic != null)
            {
                GameMusicController.Instance.PlayMusic(
                    GameMusicController.Instance.deathMusic, 1f, false);
            }

            if (StageIntroTransition.Instance != null)
                StageIntroTransition.Instance.StartFadeOut(NormalGameDeathFadeDuration);

            if (shouldRestartRound)
                StartCoroutine(RestartRoundRoutine());
            else
            {
                bool isHardcoreDefeat = SaveSystem.GetActiveNormalGameDifficulty() == NormalGameDifficulty.Hardcore;
                NormalGameOverOverlay.BeginGameOverTransition(isHardcoreDefeat);
                if (isHardcoreDefeat)
                    DeleteActiveHardcoreSaveSlot();

                StartCoroutine(ShowNormalGameOverAfterDeathFadeRoutine());
            }
        }
    }

    IEnumerator ShowNormalGameOverAfterDeathFadeRoutine()
    {
        yield return new WaitForSecondsRealtime(NormalGameDeathFadeDuration);
        yield return NormalGameOverOverlay.PlayAfterDeathFadeRoutine();
    }

    void DeleteActiveHardcoreSaveSlot()
    {
        int activeSlotIndex = SaveSystem.Data.activeSlotIndex;
        if (activeSlotIndex < 0)
            return;

        SaveSystem.DeleteSlot(activeSlotIndex);
        if (SaveSystem.Data.activeSlotIndex == activeSlotIndex)
        {
            SaveSystem.Data.activeSlotIndex = -1;
            SaveSystem.Save();
        }
    }

    bool ShouldRestartNormalGameAfterPartyDefeat()
    {
        if (BossRushSession.IsActive)
            return true;

        NormalGameDifficulty difficulty = SaveSystem.GetActiveNormalGameDifficulty();

        if (difficulty == NormalGameDifficulty.Hardcore)
        {
            return false;
        }

        if (difficulty != NormalGameDifficulty.Hard)
            return true;

        if (GameSession.Instance != null &&
            GameSession.Instance.HardNormalGameRemainingLives > 0)
            return true;

        return false;
    }

    bool TryGetWinningTeamSurvivors(
        List<PlayerIdentity> ids,
        List<MovementController> survivingWinners,
        out MovementController survivingPlayer)
    {
        survivingPlayer = null;
        survivingWinners?.Clear();

        if (ids == null || ids.Count <= 0 || BattleModeRules.Instance == null)
            return false;

        BattleModeRules.TeamId? winningTeam = null;

        for (int i = 0; i < ids.Count; i++)
        {
            PlayerIdentity id = ids[i];
            if (id == null)
                continue;

            if (!TryGetRuntimePlayerComponents(id, out var movement, out _))
                continue;

            if (!movement.gameObject.activeInHierarchy || movement.isDead)
                continue;

            BattleModeRules.TeamId currentTeam = BattleModeRules.Instance.GetTeamForPlayer(movement.PlayerId);

            if (winningTeam == null)
            {
                winningTeam = currentTeam;
                survivingPlayer = movement;
                survivingWinners?.Add(movement);
                continue;
            }

            if (winningTeam.Value != currentTeam)
            {
                survivingWinners?.Clear();
                return false;
            }

            survivingWinners?.Add(movement);
        }

        return survivingPlayer != null;
    }

    void TriggerBattleVictorySequence(List<MovementController> survivingPlayers, MovementController survivingPlayer)
    {
        if (survivingPlayer == null)
            return;

        restartingRound = true;
        endStageTriggered = true;

        BattleSuddenDeathController suddenDeathController = FindAnyObjectByType<BattleSuddenDeathController>();
        if (suddenDeathController != null)
            suddenDeathController.StopSuddenDeathAndClearVisuals();

        BattleModeHud.CaptureDisplayedVictorySnapshot();
        bool matchComplete = RegisterBattleVictory(survivingPlayer);

        if (survivingPlayers != null)
        {
            for (int i = 0; i < survivingPlayers.Count; i++)
            {
                MovementController winner = survivingPlayers[i];
                if (winner == null)
                    continue;

                if (winner.TryGetComponent<PowerGloveAbility>(out var glove) && glove != null)
                    glove.DestroyHeldBombIfHolding();

                if (winner.TryGetComponent<BombController>(out var bombController) && bombController != null)
                    bombController.ClearPlantedBombsOnStageEnd(false);

                Vector2 winnerCelebrationCenter = new(
                    Mathf.Round(winner.transform.position.x),
                    Mathf.Round(winner.transform.position.y)
                );

                winner.PlayEndStageSequence(winnerCelebrationCenter, snapToPortalCenter: true);
            }
        }

        bool hasNightmareBomberWinner = EndStageVoiceSfx.HasAnyActiveNightmareBomber(survivingPlayers, "BattleVictory");

        StartCoroutine(BattleVictorySequenceRoutine(survivingPlayer, matchComplete, hasNightmareBomberWinner));
    }

    void TryDropBattleItemsForDeadPlayer(MovementController deadPlayer)
    {
        if (!Application.isPlaying || deadPlayer == null || !deadPlayer.CompareTag("Player"))
            return;

        if (BattleModeRules.Instance != null && !BattleModeRules.Instance.EnableItemDropsAfterDeath)
            return;

        PlayerPersistentStats.PlayerState state = PlayerPersistentStats.GetRuntime(deadPlayer.PlayerId);
        if (state == null)
            return;

        BuildBattleDropItems(state, battleDroppedItemsBuffer);
        if (battleDroppedItemsBuffer.Count <= 0)
            return;

        CollectBattleDropCells(battleDropCellsBuffer);
        if (battleDropCellsBuffer.Count <= 0)
            return;

        AutoItemDatabase.BuildIfNeeded();

        for (int i = 0; i < battleDroppedItemsBuffer.Count && battleDropCellsBuffer.Count > 0; i++)
        {
            ItemPickup prefab = AutoItemDatabase.Get(battleDroppedItemsBuffer[i]);
            if (prefab == null)
                continue;

            int randomIndex = UnityEngine.Random.Range(0, battleDropCellsBuffer.Count);
            Vector3Int cell = battleDropCellsBuffer[randomIndex];
            battleDropCellsBuffer.RemoveAt(randomIndex);

            if (!TryReserveItemSpawnCell(cell))
                continue;

            Vector3 worldPosition = groundTilemap.GetCellCenterWorld(cell);
            Instantiate(prefab, worldPosition, Quaternion.identity);
        }
    }

    void BuildBattleDropItems(PlayerPersistentStats.PlayerState state, List<ItemType> results)
    {
        if (results == null)
            return;

        results.Clear();

        if (state == null)
            return;

        AddItemCopies(results, ItemType.ExtraBomb, Mathf.Max(0, state.BombAmount - 1));

        if (state.HasFullFire)
            results.Add(ItemType.FullFire);
        else
            AddItemCopies(results, ItemType.BlastRadius, Mathf.Max(0, state.ExplosionRadius - 2));

        int speedUpsAboveLoadout = Mathf.Max(
            0,
            PlayerPersistentStats.SpeedInternalToLevel(state.SpeedInternal)
            - 2);
        AddItemCopies(results, ItemType.SpeedIncrese, speedUpsAboveLoadout);

        if (state.CanKickBombs)
            results.Add(ItemType.BombKick);

        if (state.CanPassBombs)
            results.Add(ItemType.BombPass);

        if (state.CanPunchBombs)
            results.Add(ItemType.BombPunch);

        if (state.HasPowerGlove)
            results.Add(ItemType.PowerGlove);

        if (state.CanPassDestructibles)
            results.Add(ItemType.DestructiblePass);

        ItemType currentBombType = GetBattleDropBombType(state);
        if (currentBombType != ItemType.LandMine)
            results.Add(currentBombType);

    }

    void CollectBattleDropCells(List<Vector3Int> results)
    {
        if (results == null)
            return;

        results.Clear();

        if (groundTilemap == null)
            return;

        BoundsInt bounds = groundTilemap.cellBounds;

        foreach (Vector3Int cell in bounds.allPositionsWithin)
        {
            if (groundTilemap.GetTile(cell) == null)
                continue;

            if (destructibleTilemap != null && destructibleTilemap.HasTile(cell))
                continue;

            if (indestructibleTilemap != null && indestructibleTilemap.HasTile(cell))
                continue;

            if (!CanSpawnBattleDropAtCell(cell))
                continue;

            results.Add(cell);
        }
    }

    bool CanSpawnBattleDropAtCell(Vector3Int cell)
    {
        return CanSpawnItemAtCell(cell);
    }

    static void AddItemCopies(List<ItemType> results, ItemType itemType, int count)
    {
        if (results == null || count <= 0)
            return;

        for (int i = 0; i < count; i++)
            results.Add(itemType);
    }

    static ItemType GetBattleDropBombType(PlayerPersistentStats.PlayerState state)
    {
        if (state == null)
            return ItemType.LandMine;

        if (state.HasPierceBombs)
            return ItemType.PierceBomb;
        if (state.HasControlBombs)
            return ItemType.ControlBomb;
        if (state.HasPowerBomb)
            return ItemType.PowerBomb;
        if (state.HasRubberBombs)
            return ItemType.RubberBomb;
        if (state.HasMagnetBomb)
            return ItemType.MagnetBomb;

        return ItemType.LandMine;
    }

    void InitializeBattleRoundTimer()
    {
        hasBattleTimeLimit = false;
        battleTimeRemainingSeconds = Mathf.Infinity;
        battleTimerExpiredElapsedSeconds = 0f;
        battleTimerExpired = false;

        if (!IsBattleModeScene() || BattleModeRules.Instance == null)
            return;

        hasBattleTimeLimit = BattleModeRules.Instance.UsesRoundTimer;
        battleTimeRemainingSeconds = BattleModeRules.Instance.RoundTimerSeconds;
    }

    void UpdateBattleRoundTimer()
    {
        if (!Application.isPlaying)
            return;

        if (!IsBattleModeScene() || !hasBattleTimeLimit)
            return;

        if (restartingRound || endStageTriggered || battleTimerExpired)
            return;

        if (Time.timeScale <= 0f)
            return;

        if (battleTimeRemainingSeconds > 0f)
        {
            battleTimeRemainingSeconds = Mathf.Max(0f, battleTimeRemainingSeconds - Time.unscaledDeltaTime);

            if (battleTimeRemainingSeconds > 0f)
            {
                battleTimerExpiredElapsedSeconds = 0f;
                return;
            }
        }

        battleTimerExpiredElapsedSeconds += Time.unscaledDeltaTime;

        if (battleTimerExpiredElapsedSeconds < BattleTimeUpDelayAfterTimerExpiredSeconds)
            return;

        TriggerBattleDrawSequence(showTimeUp: true);
    }

    void TriggerBattleDrawSequence(bool showTimeUp)
    {
        if (battleTimerExpired || restartingRound || endStageTriggered)
            return;

        battleTimerExpired = true;
        restartingRound = true;
        endStageTriggered = true;

        if (showTimeUp)
            battleTimeRemainingSeconds = 0f;

        StartCoroutine(BattleDrawSequenceRoutine(showTimeUp));
    }

    IEnumerator BattleDrawSequenceRoutine(bool showTimeUp)
    {
        BattleRevengeSystem.BlockAndRemoveAllActiveCartsForRoundEnd();

        BattleSuddenDeathController suddenDeathController = FindAnyObjectByType<BattleSuddenDeathController>();
        if (suddenDeathController != null)
            suddenDeathController.StopSuddenDeathAndClearVisuals();

        if (showTimeUp)
        {
            PrepareActivePlayersForBattleTimeUp();
            yield return BattleTimeUpOverlay.PlayRoutine();
        }

        if (StageIntroTransition.Instance != null)
            StageIntroTransition.Instance.StartFadeOut(BattleDrawPreFadeDuration);

        yield return waitBattleDrawPreFadeDelay;

        yield return BattleDrawOverlay.PlayRoutine();

        if (StageIntroTransition.Instance != null)
            StageIntroTransition.Instance.StartFadeOut(BattleRoundWinFinalFadeDuration);

        yield return new WaitForSecondsRealtime(BattleRoundWinFinalFadeDuration);

        GamePauseController.ClearPauseFlag();
        Time.timeScale = 1f;
        PlayerPersistentStats.RollbackStage();

        StagePreIntroPlayersWalk.SkipOnNextLoad();

        Scene current = SceneManager.GetActiveScene();
        SceneManager.LoadScene(current.buildIndex);
    }

    static void PrepareActivePlayersForBattleTimeUp()
    {
        MovementController[] players =
            FindObjectsByType<MovementController>(FindObjectsInactive.Exclude);

        for (int i = 0; i < players.Length; i++)
        {
            MovementController player = players[i];
            if (player == null ||
                !player.gameObject.activeInHierarchy ||
                !player.CompareTag("Player") ||
                player.isDead)
            {
                continue;
            }

            if (player.TryGetComponent<PowerGloveAbility>(out var glove) && glove != null)
                glove.DestroyHeldBombIfHolding();

            if (player.TryGetComponent<BombController>(out var bombController) && bombController != null)
                bombController.ClearPlantedBombsOnStageEnd(false);

            player.PlayBattleTimeUpSequence();
        }
    }

    bool RegisterBattleVictory(MovementController survivingPlayer)
    {
        if (GameSession.Instance == null || survivingPlayer == null)
            return false;

        int targetVictories = BattleModeRules.Instance != null
            ? BattleModeRules.Instance.VictoriesToWinMatch
            : 3;
        int highestVictoryCount = 0;

        if (IsBattleModeTeamMatch() && BattleModeRules.Instance != null)
        {
            BattleModeRules.TeamId winningTeam = BattleModeRules.Instance.GetTeamForPlayer(survivingPlayer.PlayerId);

            for (int playerId = GameSession.MinPlayerId; playerId <= GameSession.MaxPlayerId; playerId++)
            {
                if (!IsConfiguredPlayerActive(playerId))
                    continue;

                if (BattleModeRules.Instance.GetTeamForPlayer(playerId) != winningTeam)
                    continue;

                GameSession.Instance.AddBattleMatchWin(playerId);
                highestVictoryCount = Mathf.Max(highestVictoryCount, GameSession.Instance.GetBattleMatchWins(playerId));
            }
        }
        else
        {
            GameSession.Instance.AddBattleMatchWin(survivingPlayer.PlayerId);
            highestVictoryCount = GameSession.Instance.GetBattleMatchWins(survivingPlayer.PlayerId);
        }

        return highestVictoryCount >= targetVictories;
    }

    IEnumerator BattleVictorySequenceRoutine(MovementController survivingPlayer, bool matchComplete, bool hasNightmareBomberWinner)
    {
        BattleRevengeSystem.BlockAndRemoveAllActiveCartsForRoundEnd();

        if (GameMusicController.Instance != null)
            GameMusicController.Instance.StopMusic();

        PlayBattleVictorySfx(survivingPlayer, hasNightmareBomberWinner);

        if (StageIntroTransition.Instance != null)
            StageIntroTransition.Instance.StartFadeOut(BattleVictoryFadeDuration);

        yield return new WaitForSecondsRealtime(BattleVictoryFadeDuration);

        yield return waitRoundWinScoreboardDelay;

        yield return BattleRoundWinScoreboardOverlay.PlayRoutine(survivingPlayer.PlayerId);
        BattleModeHud.ReleaseDisplayedVictorySnapshot();

        if (matchComplete)
        {
            if (GameSession.Instance != null)
                GameSession.Instance.EndBattleMatch();

            if (StageIntroTransition.Instance != null)
                StageIntroTransition.Instance.StartFadeOut(BattleWinMatchPreFadeDuration);

            yield return waitWinMatchPreFadeDuration;
            yield return waitWinMatchBlackScreenDelay;

            BattleRoundWinScoreboardOverlay.DestroyActiveOverlay();
            yield return BattleWinMatchOverlay.PlayRoutine(survivingPlayer.PlayerId);

            GamePauseController.ClearPauseFlag();
            Time.timeScale = 1f;
            PlayerPersistentStats.RollbackStage();

            StagePreIntroPlayersWalk.SkipOnNextLoad();

            BattleModeMenu.OpenDirectlyAtStageSelect = true;
            SceneManager.LoadScene(BattleModeMenuSceneName);

            yield break;
        }

        if (StageIntroTransition.Instance != null)
            StageIntroTransition.Instance.StartFadeOut(BattleRoundWinFinalFadeDuration);

        yield return new WaitForSecondsRealtime(BattleRoundWinFinalFadeDuration);

        GamePauseController.ClearPauseFlag();
        Time.timeScale = 1f;
        PlayerPersistentStats.RollbackStage();

        StagePreIntroPlayersWalk.SkipOnNextLoad();

        Scene current = SceneManager.GetActiveScene();
        SceneManager.LoadScene(current.buildIndex);
    }

    static void PlayBattleVictorySfx(MovementController survivingPlayer, bool hasNightmareBomberWinner)
    {
        if (battleVictorySfx == null)
            battleVictorySfx = Resources.Load<AudioClip>(BattleVictorySfxResourcesPath);

        if (battleVictorySfx == null || survivingPlayer == null)
            return;

        if (!survivingPlayer.TryGetComponent<AudioSource>(out var audioSource))
            audioSource = survivingPlayer.GetComponentInChildren<AudioSource>(true);

        if (audioSource != null)
        {
            audioSource.PlayOneShot(battleVictorySfx);
            survivingPlayer.StartCoroutine(PlayVictoryVoiceAfterDelay(audioSource, hasNightmareBomberWinner, 1f));
        }
    }

    static IEnumerator PlayVictoryVoiceAfterDelay(AudioSource audioSource, bool hasNightmareBomberWinner, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        EndStageVoiceSfx.TryPlayVictoryVoice(audioSource, hasNightmareBomberWinner, context: "BattleVictory");
    }

    static bool IsBattleModeScene()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        return sceneName.StartsWith("BattleMode_", StringComparison.OrdinalIgnoreCase);
    }

    static bool IsBattleModeTeamMatch()
    {
        return BattleModeRules.Instance != null && BattleModeRules.Instance.UsesTeams;
    }

    public ItemPickup GetItemPrefab(ItemType type)
    {
        AutoItemDatabase.BuildIfNeeded();
        return AutoItemDatabase.Get(type);
    }

    private static readonly ItemType[] louieEggTypes =
    {
        ItemType.BlueLouieEgg,
        ItemType.BlackLouieEgg,
        ItemType.PurpleLouieEgg,
        ItemType.GreenLouieEgg,
        ItemType.YellowLouieEgg,
        ItemType.PinkLouieEgg,
        ItemType.RedLouieEgg
    };

    void TryAssignRandomEggs(List<int> indices, ref int cursor)
    {
        if (randomEggsMax <= 0)
            return;

        int amount = UnityEngine.Random.Range(randomEggsMin, randomEggsMax + 1);
        List<GameObject> configuredEggPrefabs = BuildConfiguredEggPrefabPool();
        if (configuredEggPrefabs.Count <= 0)
            return;

        for (int i = configuredEggPrefabs.Count - 1; i > 0; i--)
        {
            int swapIndex = UnityEngine.Random.Range(0, i + 1);
            (configuredEggPrefabs[i], configuredEggPrefabs[swapIndex]) =
                (configuredEggPrefabs[swapIndex], configuredEggPrefabs[i]);
        }

        int spawnAmount = Mathf.Min(amount, configuredEggPrefabs.Count);
        for (int i = 0; i < spawnAmount && cursor < indices.Count; i++)
        {
            GameObject prefab = configuredEggPrefabs[i];
            if (prefab != null)
                orderToSpawn[indices[cursor++]] = prefab;
        }
    }

    List<GameObject> BuildConfiguredEggPrefabPool()
    {
        int[] amounts = SaveSystem.GetBattleModeLouieAmounts(GetDefaultBattleModeLouieAmounts());
        List<GameObject> results = new(BattleModeRandomEggMountTypes.Length);

        for (int i = 0; i < BattleModeRandomEggMountTypes.Length; i++)
        {
            int amount = amounts != null && i < amounts.Length ? Mathf.Clamp(amounts[i], 0, 99) : 0;
            if (amount <= 0)
                continue;

            MountedType type = BattleModeRandomEggMountTypes[i];
            GameObject prefab = ResolveRandomEggPrefab(type);
            if (prefab == null)
                continue;

            for (int copy = 0; copy < amount; copy++)
                results.Add(prefab);
        }

        return results;
    }

    GameObject ResolveRandomEggPrefab(MountedType type)
    {
        if (TryGetEggTypeForMount(type, out ItemType egg))
        {
            ItemPickup itemPrefab = AutoItemDatabase.Get(egg);
            return itemPrefab != null ? itemPrefab.gameObject : null;
        }

        GameObject mountPrefab = ResolveRandomEggMountPrefab(type);
        if (mountPrefab != null)
            hiddenMountPrefabTypes[mountPrefab] = type;

        return mountPrefab;
    }

    static bool TryGetEggTypeForMount(MountedType type, out ItemType egg)
    {
        switch (type)
        {
            case MountedType.Blue: egg = ItemType.BlueLouieEgg; return true;
            case MountedType.Black: egg = ItemType.BlackLouieEgg; return true;
            case MountedType.Purple: egg = ItemType.PurpleLouieEgg; return true;
            case MountedType.Green: egg = ItemType.GreenLouieEgg; return true;
            case MountedType.Yellow: egg = ItemType.YellowLouieEgg; return true;
            case MountedType.Pink: egg = ItemType.PinkLouieEgg; return true;
            case MountedType.Red: egg = ItemType.RedLouieEgg; return true;
            default:
                egg = default;
                return false;
        }
    }

    GameObject ResolveRandomEggMountPrefab(MountedType type)
    {
        var spawner = FindAnyObjectByType<PlayersSpawner>();
        if (spawner == null)
            return null;

        return spawner.GetPlayerMountPrefabForType(type);
    }

    public void PrepareSpawnedHiddenObject(GameObject spawned, GameObject sourcePrefab, Vector3 spawnWorldPosition)
    {
        if (spawned == null)
            return;

        if (!TryResolveHiddenMountType(spawned, sourcePrefab, out MountedType type))
            return;

        spawnWorldPosition.z = 0f;
        spawned.transform.SetParent(null, true);
        spawned.transform.position = spawnWorldPosition;
        if (spawned.TryGetComponent<Rigidbody2D>(out var rb) && rb != null)
            rb.position = spawnWorldPosition;

        Physics2D.SyncTransforms();

        if (!spawned.TryGetComponent<MountWorldPickup>(out var pickup) || pickup == null)
            pickup = spawned.AddComponent<MountWorldPickup>();

        pickup.Init(type);

        if (spawned.TryGetComponent<Collider2D>(out var col) && col != null)
            col.enabled = true;

        if (spawned.TryGetComponent<Rigidbody2D>(out rb) && rb != null)
        {
            rb.position = spawnWorldPosition;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.simulated = true;
        }

        if (spawned.TryGetComponent<MountMovementController>(out var mountMovement) && mountMovement != null)
            mountMovement.enabled = false;

        if (spawned.TryGetComponent<BombController>(out var bombController) && bombController != null)
            bombController.enabled = false;

        if (spawned.TryGetComponent<MovementController>(out var movement) && movement != null)
        {
            movement.SetExplosionInvulnerable(false);
            movement.ForceIdleFacing(Vector2.down, "HiddenMountSpawnWorldPickup");
            movement.EnableExclusiveFromState();
        }

        StartHiddenMountWorldAnimation(spawned, movement);

        spawned.transform.position = spawnWorldPosition;
        if (rb != null)
            rb.position = spawnWorldPosition;

        Physics2D.SyncTransforms();
    }

    static void StartHiddenMountWorldAnimation(GameObject spawned, MovementController movement)
    {
        if (spawned == null)
            return;

        var visual = spawned.GetComponentInChildren<MountVisualController>(true);
        if (visual == null)
            return;

        if (movement == null || movement.isDead)
            return;

        visual.localOffset = (Vector2)visual.transform.localPosition;
        visual.Bind(movement);
        visual.enabled = true;
        visual.SetInactivityEmote(false);

        var loop = spawned.GetComponent<DetachedLouieWorldInactivityLoop>();
        if (loop == null)
            loop = spawned.AddComponent<DetachedLouieWorldInactivityLoop>();

        loop.Init(
            visual,
            movement,
            chanceAlt: 0f,
            refreshFrame: true);
    }

    bool TryResolveHiddenMountType(GameObject spawned, GameObject sourcePrefab, out MountedType type)
    {
        if (sourcePrefab != null && hiddenMountPrefabTypes.TryGetValue(sourcePrefab, out type))
            return type != MountedType.None;

        string nameToResolve = sourcePrefab != null
            ? sourcePrefab.name
            : spawned != null
                ? spawned.name
                : null;

        type = ResolveMountTypeFromName(nameToResolve);
        return type != MountedType.None;
    }

    static MountedType ResolveMountTypeFromName(string n)
    {
        if (string.IsNullOrEmpty(n))
            return MountedType.None;

        n = n.ToLowerInvariant();

        if (n.Contains("mole")) return MountedType.Mole;
        if (n.Contains("tank")) return MountedType.Tank;

        return MountedType.None;
    }

    bool IsItemSpawnCellReserved(Vector3Int cell)
    {
        return reservedItemSpawnCells.Contains(cell) || pendingHiddenItemCells.Contains(cell);
    }

    void ReserveItemSpawnCell(Vector3Int cell)
    {
        if (!reservedItemSpawnCells.Add(cell))
            return;

        clearReservedItemSpawnCellsRoutine ??= StartCoroutine(ClearReservedItemSpawnCellsAtEndOfFrame());
    }

    IEnumerator ClearReservedItemSpawnCellsAtEndOfFrame()
    {
        yield return new WaitForEndOfFrame();
        reservedItemSpawnCells.Clear();
        clearReservedItemSpawnCellsRoutine = null;
    }

    public bool CanSpawnItemAtCell(Vector3Int cell)
    {
        if (groundTilemap == null)
            return false;

        if (IsItemSpawnCellReserved(cell))
            return false;

        Vector3 worldPosition = groundTilemap.GetCellCenterWorld(cell);
        Collider2D[] hits = Physics2D.OverlapCircleAll(worldPosition, 0.2f);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null)
                continue;

            if (hit.GetComponent<ItemPickup>() != null || hit.GetComponentInParent<ItemPickup>() != null)
                return false;

            if (hit.GetComponent<Bomb>() != null || hit.GetComponentInParent<Bomb>() != null)
                return false;

            if (hit.CompareTag("Player"))
                return false;

            if (hit.GetComponent<MovementController>() != null || hit.GetComponentInParent<MovementController>() != null)
                return false;
        }

        return true;
    }

    public bool TryReserveItemSpawnCell(Vector3Int cell)
    {
        if (!CanSpawnItemAtCell(cell))
            return false;

        ReserveItemSpawnCell(cell);
        return true;
    }

    public bool TryReservePendingHiddenItemCell(Vector3Int cell)
    {
        if (groundTilemap == null)
            return false;

        if (IsItemSpawnCellReserved(cell))
            return false;

        pendingHiddenItemCells.Add(cell);
        return true;
    }

    public void ReleasePendingHiddenItemCell(Vector3Int cell)
    {
        pendingHiddenItemCells.Remove(cell);
    }
}

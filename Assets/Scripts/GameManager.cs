using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public class GameManager : MonoBehaviour
{
    static readonly WaitForSecondsRealtime waitNextStageDelay = new(3f);
    static readonly WaitForSecondsRealtime waitBattleVictoryCheckDelay = new(0.5f);
    static readonly WaitForSecondsRealtime waitBattleVictoryDelay = new(1f);
    const float BattleDrawPreFadeDuration = 1f;
    static readonly WaitForSecondsRealtime waitBattleDrawPreFadeDelay = new(BattleDrawPreFadeDuration);
    const float BattleRoundWinShowDelay = 1f;
    static readonly WaitForSecondsRealtime waitRoundWinScoreboardDelay = new(BattleRoundWinShowDelay);
    const float BattleWinMatchPreFadeDuration = 0.5f;
    static readonly WaitForSecondsRealtime waitWinMatchPreFadeDuration = new(BattleWinMatchPreFadeDuration);
    static readonly WaitForSecondsRealtime waitWinMatchBlackScreenDelay = new(2f);
    const float BattleVictoryFadeDuration = 3f;
    const float BattleRoundWinFinalFadeDuration = 0.5f;
    const string BattleVictorySfxResourcesPath = "Sounds/SB5 Sound Effects (48)";
    const string TitleScreenSceneName = "TitleScreen";
    static AudioClip battleVictorySfx;

    public static GameManager Instance { get; private set; }

    public int EnemiesAlive { get; private set; }
    public int PendingHiddenEnemies { get; private set; }

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
    [Min(0)] public int powerGloveAmount = 0;
    [Min(0)] public int blueLouieEggAmount = 0;
    [Min(0)] public int blackLouieEggAmount = 0;
    [Min(0)] public int purpleLouieEggAmount = 0;
    [Min(0)] public int greenLouieEggAmount = 0;
    [Min(0)] public int yellowLouieEggAmount = 0;
    [Min(0)] public int pinkLouieEggAmount = 0;
    [Min(0)] public int redLouieEggAmount = 0;
    [Min(0)] public int clockAmount = 0;

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

    [Header("Auto Resolve (Stage Tilemaps)")]
    [SerializeField] private bool autoResolveStageTilemaps = true;

    [Header("Round Restart")]
    [SerializeField] private float restartAfterDeathSeconds = 4f;
    [SerializeField] private string bossRushSceneName = "BossRush";

    private int totalDestructibleBlocks;
    private int destroyedDestructibleBlocks;

    private readonly Dictionary<int, GameObject> orderToSpawn = new();

    private bool restartingRound;
    private bool endStageTriggered;
    private bool battleTimerExpired;
    private float battleTimeRemainingSeconds = Mathf.Infinity;
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

        ResolveDestructibleTileResolver();
    }

    void Start()
    {
        endStageTriggered = false;

        PlayerPersistentStats.EnsureSessionBooted();

        string currentSceneName = SceneManager.GetActiveScene().name;
        BossRushSession.NotifySceneLoaded(currentSceneName);

        if (IsBattleModeScene() && GameSession.Instance != null)
            GameSession.Instance.BeginBattleMatch(currentSceneName, IsBattleModeTeamMatch());

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
        TryAssignItem(ItemType.PowerGlove, powerGloveAmount);

        TryAssignItem(ItemType.BlueLouieEgg, blueLouieEggAmount);
        TryAssignItem(ItemType.BlackLouieEgg, blackLouieEggAmount);
        TryAssignItem(ItemType.PurpleLouieEgg, purpleLouieEggAmount);
        TryAssignItem(ItemType.GreenLouieEgg, greenLouieEggAmount);
        TryAssignItem(ItemType.YellowLouieEgg, yellowLouieEggAmount);
        TryAssignItem(ItemType.PinkLouieEgg, pinkLouieEggAmount);
        TryAssignItem(ItemType.RedLouieEgg, redLouieEggAmount);

        TryAssignItem(ItemType.Clock, clockAmount);
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
        yield return new WaitForSecondsRealtime(restartAfterDeathSeconds);

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

        bool isPerfectClear = PlayerPersistentStats.IsCurrentStagePerfectClear();

        StageUnlockProgress.UnlockCurrentAndNext(currentSceneName);

        if (isPerfectClear)
            StageUnlockProgress.MarkPerfect(currentSceneName);

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
        if (destructibleTilemap == null ||
            groundTilemap == null ||
            groundTile == null ||
            groundShadowTile == null)
            return;

        BoundsInt bounds = destructibleTilemap.cellBounds;

        foreach (Vector3Int pos in bounds.allPositionsWithin)
        {
            if (destructibleTilemap.GetTile(pos) == null)
                continue;

            Vector3Int below = new(pos.x, pos.y - 1, pos.z);

            TileBase currentGround = groundTilemap.GetTile(below);

            if (currentGround == groundTile)
                groundTilemap.SetTile(below, groundShadowTile);
        }
    }

    public void OnDestructibleDestroyed(Vector3Int cell)
    {
        if (groundTilemap == null ||
            groundTile == null ||
            groundShadowTile == null)
            return;

        Vector3Int below = new(cell.x, cell.y - 1, cell.z);

        TileBase current = groundTilemap.GetTile(below);

        if (current == groundShadowTile)
            groundTilemap.SetTile(below, groundTile);
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
                TriggerBattleDrawSequence();
                return;
            }

            restartingRound = true;

            if (GameMusicController.Instance != null &&
                GameMusicController.Instance.deathMusic != null)
            {
                GameMusicController.Instance.PlayMusic(
                    GameMusicController.Instance.deathMusic, 1f, false);
            }

            if (StageIntroTransition.Instance != null)
                StageIntroTransition.Instance.StartFadeOut(2f);

            StartCoroutine(RestartRoundRoutine());
        }
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

        Vector2 celebrationCenter = new(
            Mathf.Round(survivingPlayer.transform.position.x),
            Mathf.Round(survivingPlayer.transform.position.y)
        );

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

                winner.PlayEndStageSequence(celebrationCenter, snapToPortalCenter: false);
            }
        }

        StartCoroutine(BattleVictorySequenceRoutine(survivingPlayer, matchComplete));
    }

    void TryDropBattleItemsForDeadPlayer(MovementController deadPlayer)
    {
        if (!Application.isPlaying || deadPlayer == null || !deadPlayer.CompareTag("Player"))
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
            AddItemCopies(results, ItemType.BlastRadius, Mathf.Max(0, state.ExplosionRadius - 1));

        int speedUps = Mathf.Max(
            0,
            (PlayerPersistentStats.ClampSpeedInternal(state.SpeedInternal) - PlayerPersistentStats.MinSpeedInternal)
            / PlayerPersistentStats.SpeedStep);
        AddItemCopies(results, ItemType.SpeedIncrese, speedUps);
        AddItemCopies(results, ItemType.Heart, Mathf.Max(0, state.Life - 1));

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

        if (state.HasPierceBombs)
            results.Add(ItemType.PierceBomb);
        else if (state.HasControlBombs)
            results.Add(ItemType.ControlBomb);
        else if (state.HasPowerBomb)
            results.Add(ItemType.PowerBomb);
        else if (state.HasRubberBombs)
            results.Add(ItemType.RubberBomb);
        else if (state.HasMagnetBomb)
            results.Add(ItemType.MagnetBomb);

        ItemType mountedEggType = GetBattleDropEggType(state.MountedLouie);
        if (mountedEggType != ItemType.LandMine)
            results.Add(mountedEggType);

        if (state.QueuedEggs != null)
        {
            for (int i = 0; i < state.QueuedEggs.Count; i++)
            {
                ItemType eggType = state.QueuedEggs[i];
                if (IsBattleDroppableEgg(eggType))
                    results.Add(eggType);
            }
        }
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

    static ItemType GetBattleDropEggType(MountedType mountedType)
    {
        switch (mountedType)
        {
            case MountedType.Blue:
                return ItemType.BlueLouieEgg;
            case MountedType.Black:
                return ItemType.BlackLouieEgg;
            case MountedType.Purple:
                return ItemType.PurpleLouieEgg;
            case MountedType.Green:
                return ItemType.GreenLouieEgg;
            case MountedType.Yellow:
                return ItemType.YellowLouieEgg;
            case MountedType.Pink:
                return ItemType.PinkLouieEgg;
            case MountedType.Red:
                return ItemType.RedLouieEgg;
            default:
                return ItemType.LandMine;
        }
    }

    static bool IsBattleDroppableEgg(ItemType itemType)
    {
        return itemType == ItemType.BlueLouieEgg
            || itemType == ItemType.BlackLouieEgg
            || itemType == ItemType.PurpleLouieEgg
            || itemType == ItemType.GreenLouieEgg
            || itemType == ItemType.YellowLouieEgg
            || itemType == ItemType.PinkLouieEgg
            || itemType == ItemType.RedLouieEgg;
    }

    void InitializeBattleRoundTimer()
    {
        hasBattleTimeLimit = false;
        battleTimeRemainingSeconds = Mathf.Infinity;
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

        battleTimeRemainingSeconds = Mathf.Max(0f, battleTimeRemainingSeconds - Time.unscaledDeltaTime);

        if (battleTimeRemainingSeconds > 0f)
            return;

        TriggerBattleDrawSequence();
    }

    void TriggerBattleDrawSequence()
    {
        if (battleTimerExpired || restartingRound || endStageTriggered)
            return;

        battleTimerExpired = true;
        restartingRound = true;
        endStageTriggered = true;
        battleTimeRemainingSeconds = 0f;

        StartCoroutine(BattleDrawSequenceRoutine());
    }

    IEnumerator BattleDrawSequenceRoutine()
    {
        BattleSuddenDeathController suddenDeathController = FindAnyObjectByType<BattleSuddenDeathController>();
        if (suddenDeathController != null)
            suddenDeathController.StopSuddenDeathAndClearVisuals();

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

    IEnumerator BattleVictorySequenceRoutine(MovementController survivingPlayer, bool matchComplete)
    {
        if (GameMusicController.Instance != null)
            GameMusicController.Instance.StopMusic();

        PlayBattleVictorySfx(survivingPlayer);

        if (StageIntroTransition.Instance != null)
            StageIntroTransition.Instance.StartFadeOut(BattleVictoryFadeDuration);

        yield return new WaitForSecondsRealtime(BattleVictoryFadeDuration);

        yield return waitRoundWinScoreboardDelay;

        yield return BattleRoundWinScoreboardOverlay.PlayRoutine(survivingPlayer.PlayerId);

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
            SceneManager.LoadScene(TitleScreenSceneName);
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

    static void PlayBattleVictorySfx(MovementController survivingPlayer)
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
            survivingPlayer.StartCoroutine(
                PlayGoodAfterDelay(audioSource, 1f)
            );
        }
    }

    static IEnumerator PlayGoodAfterDelay(AudioSource audioSource, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        EndStageVoiceSfx.PlayRandomGood(audioSource);
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

        for (int i = 0; i < amount && cursor < indices.Count; i++)
        {
            int randomIndex = UnityEngine.Random.Range(0, louieEggTypes.Length);
            ItemType randomEgg = louieEggTypes[randomIndex];

            ItemPickup prefab = AutoItemDatabase.Get(randomEgg);

            if (prefab != null)
                orderToSpawn[indices[cursor++]] = prefab.gameObject;
        }
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

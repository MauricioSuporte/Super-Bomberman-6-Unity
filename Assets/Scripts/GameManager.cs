using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public class GameManager : MonoBehaviour
{
    static readonly WaitForSecondsRealtime waitNextStageDelay = new(4f);

    private readonly List<GameObject> runtimePlayers = new();

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

    private int totalDestructibleBlocks;
    private int destroyedDestructibleBlocks;

    private readonly Dictionary<int, GameObject> orderToSpawn = new();

    private bool restartingRound;

    private bool pendingEnemyCheck;
    private Coroutine enemyCheckRoutine;

    [Header("Destructible Tile Resolver (optional, auto-find)")]
    [SerializeField] private DestructibleTileResolver destructibleTileResolver;

    void Awake()
    {
        if (autoResolveStageTilemaps)
            ResolveStageTilemapsIfNeeded();

        portalPrefab = Resources.Load<EndStagePortal>(portalResourcesPath);

        ResolveDestructibleTileResolver();
    }

    void Start()
    {
        PlayerPersistentStats.EnsureSessionBooted();

        CachePlayers();

        EnemiesAlive = FindObjectsByType<EnemyMovementController>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        ).Length;

        SetupHiddenObjects();
        ApplyDestructibleShadows();

        CountPendingHiddenEnemiesFromTiles();

        ScheduleEnemyCheckNextFrame();
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

        destructibleTileResolver = FindFirstObjectByType<DestructibleTileResolver>();
        if (destructibleTileResolver != null)
            return;

        if (destructibleTilemap != null)
            destructibleTileResolver = destructibleTilemap.GetComponentInParent<DestructibleTileResolver>(true);

        if (destructibleTileResolver != null)
            return;

        var stage = GameObject.Find("Stage");
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

        var bounds = destructibleTilemap.cellBounds;

        foreach (var pos in bounds.allPositionsWithin)
        {
            var tile = destructibleTilemap.GetTile(pos);
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

        var bounds = destructibleTilemap.cellBounds;
        foreach (var pos in bounds.allPositionsWithin)
        {
            if (destructibleTilemap.GetTile(pos) != null)
                return true;
        }

        return false;
    }

    void CachePlayers()
    {
        runtimePlayers.Clear();

        var ids = FindObjectsByType<PlayerIdentity>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        for (int i = 0; i < ids.Length; i++)
        {
            if (ids[i] == null)
                continue;

            var go = ids[i].gameObject;
            if (go == null)
                continue;

            runtimePlayers.Add(go);
        }
    }

    GameObject GetPrimaryPlayer()
    {
        PlayerIdentity best = null;

        var ids = FindObjectsByType<PlayerIdentity>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < ids.Length; i++)
        {
            if (ids[i] == null)
                continue;

            if (ids[i].playerId == 1)
            {
                best = ids[i];
                break;
            }
        }

        if (best != null && best.gameObject != null)
            return best.gameObject;

        CachePlayers();

        for (int i = 0; i < runtimePlayers.Count; i++)
        {
            if (runtimePlayers[i] != null)
                return runtimePlayers[i];
        }

        return null;
    }

    int GetPlayerIdFromGO(GameObject go)
    {
        if (go == null) return 1;

        if (go.TryGetComponent<PlayerIdentity>(out var id) && id != null)
            return Mathf.Clamp(id.playerId, 1, 4);

        var parentId = go.GetComponentInParent<PlayerIdentity>(true);
        if (parentId != null)
            return Mathf.Clamp(parentId.playerId, 1, 4);

        return 1;
    }

    void ResolveStageTilemapsIfNeeded()
    {
        if (destructibleTilemap != null && indestructibleTilemap != null && groundTilemap != null)
            return;

        var gameplayRoot = GameObject.Find("GameplayRoot");
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

        var t = root.Find(childName);
        if (t == null)
            return null;

        var tm = t.GetComponent<Tilemap>();
        if (tm != null)
            return tm;

        return t.GetComponentInChildren<Tilemap>(true);
    }

    Tilemap FindTilemapByNameFallback(string exactName)
    {
        var all = FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < all.Length; i++)
        {
            var tm = all[i];
            if (tm != null && tm.name == exactName)
                return tm;
        }

        for (int i = 0; i < all.Length; i++)
        {
            var tm = all[i];
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

        var bounds = destructibleTilemap.cellBounds;

        foreach (var pos in bounds.allPositionsWithin)
            if (destructibleTilemap.GetTile(pos) != null)
                totalDestructibleBlocks++;

        if (totalDestructibleBlocks <= 0)
            return;

        var indices = new List<int>(totalDestructibleBlocks);
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

            var prefab = AutoItemDatabase.Get(type);

            for (int i = 0; i < amount && cursor < indices.Count; i++)
                orderToSpawn[indices[cursor++]] = prefab.gameObject;
        }

        TryAssignPortal(portalAmount);

        TryAssignItem(ItemType.ExtraBomb, extraBombAmount);
        TryAssignItem(ItemType.BlastRadius, blastRadiusAmount);
        TryAssignItem(ItemType.SpeedIncrese, speedIncreaseAmount);
        TryAssignItem(ItemType.BombKick, kickBombAmount);
        TryAssignItem(ItemType.BombPunch, punchBombAmount);
        TryAssignItem(ItemType.PierceBomb, pierceBombAmount);
        TryAssignItem(ItemType.ControlBomb, controlBombAmount);
        TryAssignItem(ItemType.PowerBomb, powerBombAmount);
        TryAssignItem(ItemType.RubberBomb, rubberBombAmount);
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

    public GameObject GetSpawnForDestroyedBlock()
    {
        if (totalDestructibleBlocks <= 0)
            return null;

        destroyedDestructibleBlocks++;
        int order = destroyedDestructibleBlocks;

        if (orderToSpawn.TryGetValue(order, out var prefabGo))
            return prefabGo;

        return null;
    }

    public void CheckWinState()
    {
        if (restartingRound)
            return;

        CachePlayers();

        int aliveCount = 0;

        for (int i = 0; i < runtimePlayers.Count; i++)
        {
            var p = runtimePlayers[i];
            if (p != null && p.activeSelf)
                aliveCount++;
        }

        if (aliveCount <= 0)
        {
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

    IEnumerator RestartRoundRoutine()
    {
        yield return new WaitForSecondsRealtime(restartAfterDeathSeconds);

        StageIntroTransition.SkipTitleScreenOnNextLoad();

        Scene current = SceneManager.GetActiveScene();
        SceneManager.LoadScene(current.buildIndex);
    }

    public void EndStage()
    {
        var primaryPlayer = GetPrimaryPlayer();

        if (primaryPlayer != null)
        {
            if (primaryPlayer.TryGetComponent<AbilitySystem>(out var abilitySystem))
                abilitySystem.Disable(InvincibleSuitAbility.AbilityId);

            if (primaryPlayer.TryGetComponent<BombController>(out var bomb))
            {
                int playerId = GetPlayerIdFromGO(primaryPlayer);

                var state = PlayerPersistentStats.Get(playerId);
                state.BombAmount = Mathf.Min(bomb.bombAmout, PlayerPersistentStats.MaxBombAmount);
                state.ExplosionRadius = Mathf.Min(bomb.explosionRadius, PlayerPersistentStats.MaxExplosionRadius);
            }
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

        StageIntroTransition.SkipTitleScreenOnNextLoad();

        SceneManager.LoadScene(nextStageSceneName);
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

        var bounds = destructibleTilemap.cellBounds;

        foreach (var pos in bounds.allPositionsWithin)
        {
            if (destructibleTilemap.GetTile(pos) == null)
                continue;

            var below = new Vector3Int(pos.x, pos.y - 1, pos.z);

            var currentGround = groundTilemap.GetTile(below);

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

        var below = new Vector3Int(cell.x, cell.y - 1, cell.z);

        var current = groundTilemap.GetTile(below);

        if (current == groundShadowTile)
            groundTilemap.SetTile(below, groundTile);
    }

    public void NotifyPlayerDeathStarted()
    {
        if (restartingRound)
            return;

        var players = FindObjectsByType<MovementController>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );

        int aliveNotDead = 0;

        for (int i = 0; i < players.Length; i++)
        {
            var m = players[i];
            if (m == null)
                continue;

            if (!m.CompareTag("Player"))
                continue;

            if (!m.isActiveAndEnabled && !m.gameObject.activeInHierarchy)
                continue;

            if (m.isDead)
                continue;

            aliveNotDead++;
        }

        if (aliveNotDead <= 0)
        {
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

    public ItemPickup GetItemPrefab(ItemType type)
    {
        AutoItemDatabase.BuildIfNeeded();
        return AutoItemDatabase.Get(type);
    }
}
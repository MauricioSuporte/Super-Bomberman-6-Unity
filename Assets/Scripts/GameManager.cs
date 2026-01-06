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

    public event Action OnAllEnemiesDefeated;

    [Header("Hidden Objects Prefabs")]
    public EndStagePortal endStagePortalPrefab;
    public ItemPickup extraBombItemPrefab;
    public ItemPickup blastRadiusItemPrefab;
    public ItemPickup speedIncreaseItemPrefab;
    public ItemPickup kickBombItemPrefab;
    public ItemPickup punchBombItemPrefab;
    public ItemPickup pierceBombItemPrefab;
    public ItemPickup controlBombItemPrefab;
    public ItemPickup fullFireItemPrefab;
    public ItemPickup bombPassItemPrefab;
    public ItemPickup destructiblePassItemPrefab;
    public ItemPickup invincibleSuitItemPrefab;
    public ItemPickup heartItemPrefab;
    public ItemPickup blueLouieEggItemPrefab;
    public ItemPickup blackLouieEggItemPrefab;
    public ItemPickup purpleLouieEggItemPrefab;
    public ItemPickup greenLouieEggItemPrefab;
    public ItemPickup yellowLouieEggItemPrefab;
    public ItemPickup pinkLouieEggItemPrefab;
    public ItemPickup redLouieEggItemPrefab;

    [Header("Hidden Objects Amounts")]
    [Min(0)] public int portalAmount = 1;
    [Min(0)] public int extraBombAmount = 1;
    [Min(0)] public int blastRadiusAmount = 1;
    [Min(0)] public int speedIncreaseAmount = 1;
    [Min(0)] public int kickBombAmount = 1;
    [Min(0)] public int punchBombAmount = 1;
    [Min(0)] public int pierceBombAmount = 1;
    [Min(0)] public int controlBombAmount = 1;
    [Min(0)] public int fullFireAmount = 1;
    [Min(0)] public int bombPassAmount = 1;
    [Min(0)] public int destructiblePassAmount = 1;
    [Min(0)] public int invincibleSuitAmount = 1;
    [Min(0)] public int heartAmount = 1;
    [Min(0)] public int blueLouieEggAmount = 1;
    [Min(0)] public int blackLouieEggAmount = 1;
    [Min(0)] public int purpleLouieEggAmount = 1;
    [Min(0)] public int greenLouieEggAmount = 1;
    [Min(0)] public int yellowLouieEggAmount = 1;
    [Min(0)] public int pinkLouieEggAmount = 1;
    [Min(0)] public int redLouieEggAmount = 1;

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
    private readonly float restartAfterDeathSeconds = 4f;

    int totalDestructibleBlocks;
    int destroyedDestructibleBlocks;

    readonly Dictionary<int, GameObject> orderToSpawn = new();

    bool restartingRound;

    void Awake()
    {
        if (autoResolveStageTilemaps)
            ResolveStageTilemapsIfNeeded();
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
            if (tm != null && tm.name != null && tm.name.IndexOf(exactName, StringComparison.OrdinalIgnoreCase) >= 0)
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

        var bounds = destructibleTilemap.cellBounds;

        foreach (var pos in bounds.allPositionsWithin)
        {
            if (destructibleTilemap.GetTile(pos) != null)
                totalDestructibleBlocks++;
        }

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

        void TryAssign(GameObject prefabGo, int amount)
        {
            if (prefabGo == null || amount <= 0)
                return;

            for (int k = 0; k < amount && cursor < indices.Count; k++)
                orderToSpawn[indices[cursor++]] = prefabGo;
        }

        TryAssign(endStagePortalPrefab != null ? endStagePortalPrefab.gameObject : null, portalAmount);

        TryAssign(extraBombItemPrefab != null ? extraBombItemPrefab.gameObject : null, extraBombAmount);
        TryAssign(blastRadiusItemPrefab != null ? blastRadiusItemPrefab.gameObject : null, blastRadiusAmount);
        TryAssign(speedIncreaseItemPrefab != null ? speedIncreaseItemPrefab.gameObject : null, speedIncreaseAmount);
        TryAssign(kickBombItemPrefab != null ? kickBombItemPrefab.gameObject : null, kickBombAmount);
        TryAssign(punchBombItemPrefab != null ? punchBombItemPrefab.gameObject : null, punchBombAmount);
        TryAssign(pierceBombItemPrefab != null ? pierceBombItemPrefab.gameObject : null, pierceBombAmount);
        TryAssign(pierceBombItemPrefab != null ? pierceBombItemPrefab.gameObject : null, pierceBombAmount);
        TryAssign(controlBombItemPrefab != null ? controlBombItemPrefab.gameObject : null, controlBombAmount);
        TryAssign(fullFireItemPrefab != null ? fullFireItemPrefab.gameObject : null, fullFireAmount);
        TryAssign(bombPassItemPrefab != null ? bombPassItemPrefab.gameObject : null, bombPassAmount);
        TryAssign(destructiblePassItemPrefab != null ? destructiblePassItemPrefab.gameObject : null, destructiblePassAmount);
        TryAssign(invincibleSuitItemPrefab != null ? invincibleSuitItemPrefab.gameObject : null, invincibleSuitAmount);
        TryAssign(heartItemPrefab != null ? heartItemPrefab.gameObject : null, heartAmount);

        TryAssign(blueLouieEggItemPrefab != null ? blueLouieEggItemPrefab.gameObject : null, blueLouieEggAmount);
        TryAssign(blackLouieEggItemPrefab != null ? blackLouieEggItemPrefab.gameObject : null, blackLouieEggAmount);
        TryAssign(purpleLouieEggItemPrefab != null ? purpleLouieEggItemPrefab.gameObject : null, purpleLouieEggAmount);
        TryAssign(greenLouieEggItemPrefab != null ? greenLouieEggItemPrefab.gameObject : null, greenLouieEggAmount);
        TryAssign(yellowLouieEggItemPrefab != null ? yellowLouieEggItemPrefab.gameObject : null, yellowLouieEggAmount);
        TryAssign(pinkLouieEggItemPrefab != null ? pinkLouieEggItemPrefab.gameObject : null, pinkLouieEggAmount);
        TryAssign(redLouieEggItemPrefab != null ? redLouieEggItemPrefab.gameObject : null, redLouieEggAmount);
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

    public void NotifyEnemyDied()
    {
        EnemiesAlive--;
        if (EnemiesAlive <= 0)
        {
            EnemiesAlive = 0;
            OnAllEnemiesDefeated?.Invoke();
        }
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
}

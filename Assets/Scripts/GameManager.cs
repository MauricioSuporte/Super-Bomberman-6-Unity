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

    [Header("Stage")]
    public Tilemap destructibleTilemap;
    public Tilemap indestructibleTilemap;

    [Header("Stage Flow")]
    public string nextStageSceneName;

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

    int portalSpawnOrder = -1;
    int extraBombSpawnOrder = -1;
    int blastRadiusSpawnOrder = -1;
    int speedIncreaseSpawnOrder = -1;
    int kickBombSpawnOrder = -1;
    int punchBombSpawnOrder = -1;
    int pierceBombSpawnOrder = -1;
    int controlBombSpawnOrder = -1;
    int fullFireSpawnOrder = -1;
    int bombPassSpawnOrder = -1;
    int destructiblePassSpawnOrder = -1;
    int invincibleSuitSpawnOrder = -1;
    int heartSpawnOrder = -1;
    int blueLouieEggSpawnOrder = -1;
    int blackLouieEggSpawnOrder = -1;
    int purpleLouieEggSpawnOrder = -1;
    int greenLouieEggSpawnOrder = -1;
    int yellowLouieEggSpawnOrder = -1;
    int pinkLouieEggSpawnOrder = -1;
    int redLouieEggSpawnOrder = -1;

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
        if (destructibleTilemap == null)
            return;

        var bounds = destructibleTilemap.cellBounds;
        totalDestructibleBlocks = 0;

        foreach (var pos in bounds.allPositionsWithin)
        {
            if (destructibleTilemap.GetTile(pos) != null)
                totalDestructibleBlocks++;
        }

        if (totalDestructibleBlocks <= 0)
            return;

        var indices = new List<int>();
        for (int i = 1; i <= totalDestructibleBlocks; i++)
            indices.Add(i);

        for (int i = indices.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (indices[j], indices[i]) = (indices[i], indices[j]);
        }

        int cursor = 0;

        portalSpawnOrder = -1;
        extraBombSpawnOrder = -1;
        blastRadiusSpawnOrder = -1;
        speedIncreaseSpawnOrder = -1;
        kickBombSpawnOrder = -1;
        punchBombSpawnOrder = -1;
        pierceBombSpawnOrder = -1;
        controlBombSpawnOrder = -1;
        fullFireSpawnOrder = -1;
        bombPassSpawnOrder = -1;
        destructiblePassSpawnOrder = -1;
        invincibleSuitSpawnOrder = -1;
        heartSpawnOrder = -1;
        blueLouieEggSpawnOrder = -1;
        blackLouieEggSpawnOrder = -1;
        purpleLouieEggSpawnOrder = -1;
        greenLouieEggSpawnOrder = -1;
        yellowLouieEggSpawnOrder = -1;
        pinkLouieEggSpawnOrder = -1;
        redLouieEggSpawnOrder = -1;

        if (endStagePortalPrefab != null && cursor < indices.Count)
            portalSpawnOrder = indices[cursor++];

        if (extraBombItemPrefab != null && cursor < indices.Count)
            extraBombSpawnOrder = indices[cursor++];

        if (blastRadiusItemPrefab != null && cursor < indices.Count)
            blastRadiusSpawnOrder = indices[cursor++];

        if (speedIncreaseItemPrefab != null && cursor < indices.Count)
            speedIncreaseSpawnOrder = indices[cursor++];

        if (kickBombItemPrefab != null && cursor < indices.Count)
            kickBombSpawnOrder = indices[cursor++];

        if (punchBombItemPrefab != null && cursor < indices.Count)
            punchBombSpawnOrder = indices[cursor++];

        if (pierceBombItemPrefab != null && cursor < indices.Count)
            pierceBombSpawnOrder = indices[cursor++];

        if (controlBombItemPrefab != null && cursor < indices.Count)
            controlBombSpawnOrder = indices[cursor++];

        if (fullFireItemPrefab != null && cursor < indices.Count)
            fullFireSpawnOrder = indices[cursor++];

        if (bombPassItemPrefab != null && cursor < indices.Count)
            bombPassSpawnOrder = indices[cursor++];

        if (destructiblePassItemPrefab != null && cursor < indices.Count)
            destructiblePassSpawnOrder = indices[cursor++];

        if (invincibleSuitItemPrefab != null && cursor < indices.Count)
            invincibleSuitSpawnOrder = indices[cursor++];

        if (heartItemPrefab != null && cursor < indices.Count)
            heartSpawnOrder = indices[cursor++];

        if (blueLouieEggItemPrefab != null && cursor < indices.Count)
            blueLouieEggSpawnOrder = indices[cursor++];

        if (blackLouieEggItemPrefab != null && cursor < indices.Count)
            blackLouieEggSpawnOrder = indices[cursor++];

        if (purpleLouieEggItemPrefab != null && cursor < indices.Count)
            purpleLouieEggSpawnOrder = indices[cursor++];

        if (greenLouieEggItemPrefab != null && cursor < indices.Count)
            greenLouieEggSpawnOrder = indices[cursor++];

        if (yellowLouieEggItemPrefab != null && cursor < indices.Count)
            yellowLouieEggSpawnOrder = indices[cursor++];

        if (pinkLouieEggItemPrefab != null && cursor < indices.Count)
            pinkLouieEggSpawnOrder = indices[cursor++];

        if (redLouieEggItemPrefab != null && cursor < indices.Count)
            redLouieEggSpawnOrder = indices[cursor++];
    }

    public GameObject GetSpawnForDestroyedBlock()
    {
        if (totalDestructibleBlocks <= 0)
            return null;

        destroyedDestructibleBlocks++;
        int order = destroyedDestructibleBlocks;

        if (order == portalSpawnOrder && endStagePortalPrefab != null)
            return endStagePortalPrefab.gameObject;

        if (order == extraBombSpawnOrder && extraBombItemPrefab != null)
            return extraBombItemPrefab.gameObject;

        if (order == blastRadiusSpawnOrder && blastRadiusItemPrefab != null)
            return blastRadiusItemPrefab.gameObject;

        if (order == speedIncreaseSpawnOrder && speedIncreaseItemPrefab != null)
            return speedIncreaseItemPrefab.gameObject;

        if (order == kickBombSpawnOrder && kickBombItemPrefab != null)
            return kickBombItemPrefab.gameObject;

        if (order == punchBombSpawnOrder && punchBombItemPrefab != null)
            return punchBombItemPrefab.gameObject;

        if (order == pierceBombSpawnOrder && pierceBombItemPrefab != null)
            return pierceBombItemPrefab.gameObject;

        if (order == controlBombSpawnOrder && controlBombItemPrefab != null)
            return controlBombItemPrefab.gameObject;

        if (order == fullFireSpawnOrder && fullFireItemPrefab != null)
            return fullFireItemPrefab.gameObject;

        if (order == bombPassSpawnOrder && bombPassItemPrefab != null)
            return bombPassItemPrefab.gameObject;

        if (order == destructiblePassSpawnOrder && destructiblePassItemPrefab != null)
            return destructiblePassItemPrefab.gameObject;

        if (order == invincibleSuitSpawnOrder && invincibleSuitItemPrefab != null)
            return invincibleSuitItemPrefab.gameObject;

        if (order == heartSpawnOrder && heartItemPrefab != null)
            return heartItemPrefab.gameObject;

        if (order == blueLouieEggSpawnOrder && blueLouieEggItemPrefab != null)
            return blueLouieEggItemPrefab.gameObject;

        if (order == blackLouieEggSpawnOrder && blackLouieEggItemPrefab != null)
            return blackLouieEggItemPrefab.gameObject;

        if (order == purpleLouieEggSpawnOrder && purpleLouieEggItemPrefab != null)
            return purpleLouieEggItemPrefab.gameObject;

        if (order == greenLouieEggSpawnOrder && greenLouieEggItemPrefab != null)
            return greenLouieEggItemPrefab.gameObject;

        if (order == yellowLouieEggSpawnOrder && yellowLouieEggItemPrefab != null)
            return yellowLouieEggItemPrefab.gameObject;

        if (order == pinkLouieEggSpawnOrder && pinkLouieEggItemPrefab != null)
            return pinkLouieEggItemPrefab.gameObject;

        if (order == redLouieEggSpawnOrder && redLouieEggItemPrefab != null)
            return redLouieEggItemPrefab.gameObject;

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

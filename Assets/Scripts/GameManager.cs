using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public class GameManager : MonoBehaviour
{
    private static readonly WaitForSecondsRealtime _waitForSecondsRealtime2 = new(2f);
    public GameObject[] players;

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

    [Header("Stage")]
    public Tilemap destructibleTilemap;

    [Header("Stage Flow")]
    public string nextStageSceneName;

    static readonly WaitForSecondsRealtime waitNextStageDelay = new(4f);

    [Header("Ground")]
    public Tilemap groundTilemap;
    public TileBase groundTile;
    public TileBase groundShadowTile;

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

    void Start()
    {
        EnemiesAlive = FindObjectsByType<EnemyMovementController>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        ).Length;

        SetupHiddenObjects();
        ApplyDestructibleShadows();
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
        int aliveCount = 0;

        foreach (GameObject player in players)
        {
            if (player.activeSelf)
            {
                aliveCount++;
            }
        }

        if (aliveCount <= 1)
        {
            if (StageIntroTransition.Instance != null)
            {
                StageIntroTransition.Instance.StartFadeOut(2f);
            }

            StartCoroutine(RestartRoundRoutine());
        }
    }

    IEnumerator RestartRoundRoutine()
    {
        yield return _waitForSecondsRealtime2;

        StageIntroTransition.SkipTitleScreenOnNextLoad();

        Scene current = SceneManager.GetActiveScene();
        SceneManager.LoadScene(current.buildIndex);
    }

    public void EndStage()
    {
        if (players != null && players.Length > 0 && players[0] != null)
        {
            if (players[0].TryGetComponent<AbilitySystem>(out var abilitySystem))
                abilitySystem.Disable(InvincibleSuitAbility.AbilityId);

            var movement = players[0].GetComponent<MovementController>();
            var bomb = players[0].GetComponent<BombController>();
            PlayerPersistentStats.SaveFrom(movement, bomb);
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
        {
            StageIntroTransition.Instance.StartEndingScreenSequence();
        }
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
}

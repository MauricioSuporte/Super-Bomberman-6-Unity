using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public class GameManager : MonoBehaviour
{
    private static WaitForSecondsRealtime _waitForSecondsRealtime5 = new(5f);
    private static WaitForSecondsRealtime _waitForSecondsRealtime2 = new(2f);
    public GameObject[] players;

    public int EnemiesAlive { get; private set; }

    public event Action OnAllEnemiesDefeated;

    [Header("Hidden Objects Prefabs")]
    public EndStagePortal endStagePortalPrefab;
    public ItemPickup extraBombItemPrefab;
    public ItemPickup blastRadiusItemPrefab;
    public ItemPickup speedIncreaseItemPrefab;
    public ItemPickup kickBombItemPrefab;

    [Header("Stage")]
    public Tilemap destructibleTilemap;

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
            int temp = indices[i];
            indices[i] = indices[j];
            indices[j] = temp;
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
        StartCoroutine(EndStageRoutine());
    }

    IEnumerator EndStageRoutine()
    {
        yield return _waitForSecondsRealtime5;

        if (StageIntroTransition.Instance != null)
        {
            StageIntroTransition.Instance.StartEndingScreenSequence();
        }
        else
        {
            Scene current = SceneManager.GetActiveScene();
            SceneManager.LoadScene(current.buildIndex);
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

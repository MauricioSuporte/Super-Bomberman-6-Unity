using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public class GameManager : MonoBehaviour
{
    public GameObject[] players;

    public int EnemiesAlive { get; private set; }

    public event Action OnAllEnemiesDefeated;

    [Header("Hidden Objects Prefabs")]
    public EndStagePortal endStagePortalPrefab;
    public ItemPickup extraBombItemPrefab;
    public ItemPickup blastRadiusItemPrefab;
    public ItemPickup speedIncreaseItemPrefab;

    [Header("Stage")]
    public Tilemap destructibleTilemap;

    private int totalDestructibleBlocks;
    private int destroyedDestructibleBlocks;

    private int portalSpawnOrder = -1;
    private int extraBombSpawnOrder = -1;
    private int blastRadiusSpawnOrder = -1;
    private int speedIncreaseSpawnOrder = -1;

    private void Start()
    {
        EnemiesAlive = FindObjectsOfType<EnemyMovementController>().Length;
        SetupHiddenObjects();
    }

    private void SetupHiddenObjects()
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
            EndStage();
        }
    }

    public void EndStage()
    {
        Invoke(nameof(NewRound), 4f);
    }

    private void NewRound()
    {
        if (GameMusicController.Instance != null &&
            GameMusicController.Instance.defaultMusic != null)
        {
            GameMusicController.Instance.PlayMusic(
                GameMusicController.Instance.defaultMusic
            );
        }

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}

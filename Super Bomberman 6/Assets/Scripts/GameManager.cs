using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public class GameManager : MonoBehaviour
{
    public GameObject[] players;

    public int EnemiesAlive { get; private set; }

    public event System.Action OnAllEnemiesDefeated;

    [Header("Hidden Objects Prefabs")]
    public EndStagePortal endStagePortalPrefab;
    public ItemPickup extraBombItemPrefab;
    public ItemPickup blastRadiusItemPrefab;
    public ItemPickup speedIncreaseItemPrefab;

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

    void Start()
    {
        EnemiesAlive = FindObjectsOfType<EnemyMovementController>().Length;
        SetupHiddenObjects();
        ApplyDestructibleShadows();
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

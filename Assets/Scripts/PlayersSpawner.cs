using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class PlayersSpawner : MonoBehaviour
{
    [Header("Player Prefab")]
    [SerializeField] private GameObject playerPrefab;

    [Header("Legacy Spawn Points (1..6)")]
    [SerializeField] private Transform[] spawnPoints = new Transform[6];

    [Header("Per Player Spawn Override")]
    [SerializeField] private SpawnOverride[] playerOverrides = new SpawnOverride[6];

    [Header("Optional Parent")]
    [SerializeField] private Transform playersParent;

    [Header("Stage Type")]
    [SerializeField] private bool isBossStage;

    [Header("Spawn Control")]
    [SerializeField] private bool clearExistingPlayersBeforeSpawn = true;

    public bool IsBossStage => isBossStage;

    bool spawned;
    readonly List<int> configuredPlayerIds = new(GameSession.MaxPlayerId);

    static readonly Vector2[] NormalStagePositions =
    {
        new(-7f,  4f),
        new( 5f,  4f),
        new(-7f, -6f),
        new( 5f, -6f),
        new(-5f, -1f),
        new( 3f, -1f) 
    };

    static readonly Vector2[] BossStagePositions =
    {
        new(-3f, -6f),
        new( 1f, -6f),
        new(-5f, -6f),
        new( 3f, -6f),
        new(-7f, -6f),
        new( 5f, -6f) 
    };

    [Serializable]
    private struct SpawnOverride
    {
        [SerializeField] private bool useCustomSpawn;
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private Vector2 positionXY;

        public readonly bool TryGetPosition(out Vector3 pos)
        {
            pos = default;

            if (!useCustomSpawn)
                return false;

            if (spawnPoint != null)
            {
                pos = spawnPoint.position;
                return true;
            }

            if (positionXY == Vector2.zero)
                return false;

            pos = (Vector3)positionXY;
            return true;
        }
    }

    public void SpawnNow()
    {
        if (spawned)
            return;

        if (playerPrefab == null)
            return;

        if (FindAnyPlayerInScene())
        {
            spawned = true;
            return;
        }

        if (clearExistingPlayersBeforeSpawn)
            DestroyExistingPlayers();

        SpawnPlayersInternal();
        spawned = true;
    }

    public GameObject GetPlayerMountPrefabForType(MountedType type)
    {
        if (playerPrefab == null)
            return null;

        var companion = playerPrefab.GetComponent<PlayerMountCompanion>();
        if (companion == null)
            return null;

        return type switch
        {
            MountedType.Mole => companion.molePrefab,
            MountedType.Tank => companion.tankPrefab,
            _ => null
        };
    }

    bool FindAnyPlayerInScene()
    {
        var ids = FindObjectsByType<PlayerIdentity>(FindObjectsInactive.Exclude);
        return ids != null && ids.Length > 0;
    }

    void DestroyExistingPlayers()
    {
        var ids = FindObjectsByType<PlayerIdentity>(FindObjectsInactive.Include);
        if (ids == null)
            return;

        for (int i = 0; i < ids.Length; i++)
        {
            if (ids[i] == null)
                continue;

            var go = ids[i].gameObject;
            if (go == null)
                continue;

            Destroy(go);
        }
    }

    void SpawnPlayersInternal()
    {
        ResolveConfiguredPlayerIds(configuredPlayerIds);

        PlayerPersistentStats.EnsureSessionBooted();

        Vector2[] preset = isBossStage ? BossStagePositions : NormalStagePositions;

        for (int i = 0; i < configuredPlayerIds.Count; i++)
        {
            int playerId = configuredPlayerIds[i];

            if (BossRushSession.IsActive && !BossRushSession.ShouldSpawnPlayer(playerId))
                continue;

            int index = playerId - 1;
            Vector3 spawnPos = ResolveSpawnPosition(index, preset);

            var go = Instantiate(playerPrefab, spawnPos, Quaternion.identity, playersParent);

            if (!go.TryGetComponent<PlayerIdentity>(out var id))
                id = go.AddComponent<PlayerIdentity>();

            id.playerId = playerId;

            var move = go.GetComponent<MovementController>();
            var bomb = go.GetComponent<BombController>();

            if (move != null)
                move.SetPlayerId(playerId);

            if (bomb != null)
                bomb.SetPlayerId(playerId);

            if (go.TryGetComponent<PlayerMountCompanion>(out var louie))
                louie.RestoreMountedFromPersistent();

            var skins = go.GetComponentsInChildren<PlayerBomberSkinController>(true);
            for (int s = 0; s < skins.Length; s++)
            {
                if (skins[s] != null)
                    skins[s].ApplyFromIdentity();
            }
        }
    }

    void ResolveConfiguredPlayerIds(List<int> results)
    {
        if (results == null)
            return;

        results.Clear();

        if (BossRushSession.IsActive)
            BossRushSession.GetRunPlayerIds(results);
        else if (GameSession.Instance != null)
            GameSession.Instance.GetActivePlayerIds(results);

        if (results.Count <= 0)
            results.Add(GameSession.MinPlayerId);
    }

    Vector3 ResolveSpawnPosition(int index, Vector2[] preset)
    {
        if (TryGetOverridePosition(index, out var customPos))
            return customPos;

        if (spawnPoints != null && index < spawnPoints.Length && spawnPoints[index] != null)
            return spawnPoints[index].position;

        if (preset != null && index < preset.Length)
            return new Vector3(preset[index].x, preset[index].y, 0f);

        return Vector3.zero;
    }

    bool TryGetOverridePosition(int playerIndex, out Vector3 pos)
    {
        pos = default;

        if (playerOverrides == null)
            return false;

        if (playerIndex < 0 || playerIndex >= playerOverrides.Length)
            return false;

        return playerOverrides[playerIndex].TryGetPosition(out pos);
    }

    public Vector2 GetResolvedSpawnPosition(int playerId)
    {
        int idx = Mathf.Clamp(playerId - 1, 0, 5);

        Vector2[] preset = isBossStage ? BossStagePositions : NormalStagePositions;

        Vector3 pos = ResolveSpawnPosition(idx, preset);
        return (Vector2)pos;
    }
}

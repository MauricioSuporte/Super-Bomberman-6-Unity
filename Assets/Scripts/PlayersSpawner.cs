using System;
using UnityEngine;

public sealed class PlayersSpawner : MonoBehaviour
{
    [Header("Player Prefab")]
    [SerializeField] private GameObject playerPrefab;

    [Header("Legacy Spawn Points (1..4)")]
    [SerializeField] private Transform[] spawnPoints = new Transform[4];

    [Header("Per Player Spawn Override")]
    [SerializeField] private SpawnOverride[] playerOverrides = new SpawnOverride[4];

    [Header("Optional Parent")]
    [SerializeField] private Transform playersParent;

    [Header("Stage Type")]
    [SerializeField] private bool isBossStage;

    [Header("Spawn Control")]
    [Tooltip("If true, clears existing players before spawning (useful on stage reload).")]
    [SerializeField] private bool clearExistingPlayersBeforeSpawn = true;

    bool spawned;

    static readonly Vector2[] NormalStagePositions =
    {
        new(-7f,  4f),
        new( 5f,  4f),
        new(-7f, -6f),
        new( 5f, -6f)
    };

    static readonly Vector2[] BossStagePositions =
    {
        new(-3f, -6f),
        new( 1f, -6f),
        new(-5f, -6f),
        new( 3f, -6f)
    };

    [Serializable]
    private sealed class SpawnOverride
    {
        [Tooltip("If enabled, uses the custom spawn settings below.")]
        public bool useCustomSpawn;

        [Tooltip("If assigned, this Transform has priority over Position.")]
        public Transform spawnPoint;

        [Tooltip("Used if Spawn Point is not assigned.")]
        public Vector2 positionXY;

        public bool TryGetPosition(out Vector3 pos)
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

            pos = new Vector3(positionXY.x, positionXY.y, 0f);
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

    bool FindAnyPlayerInScene()
    {
        var ids = FindObjectsByType<PlayerIdentity>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        return ids != null && ids.Length > 0;
    }

    void DestroyExistingPlayers()
    {
        var ids = FindObjectsByType<PlayerIdentity>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (ids == null) return;

        for (int i = 0; i < ids.Length; i++)
        {
            if (ids[i] == null) continue;

            var go = ids[i].gameObject;
            if (go == null) continue;

            Destroy(go);
        }
    }

    void SpawnPlayersInternal()
    {
        int count = 1;

        if (GameSession.Instance != null)
            count = Mathf.Clamp(GameSession.Instance.ActivePlayerCount, 1, 4);

        PlayerPersistentStats.EnsureSessionBooted();

        Vector2[] preset = isBossStage ? BossStagePositions : NormalStagePositions;

        for (int i = 0; i < count; i++)
        {
            Vector3 spawnPos = ResolveSpawnPosition(i, preset);

            var go = Instantiate(playerPrefab, spawnPos, Quaternion.identity, playersParent);

            if (!go.TryGetComponent<PlayerIdentity>(out var id))
                id = go.AddComponent<PlayerIdentity>();

            int playerId = i + 1;
            id.playerId = playerId;

            var move = go.GetComponent<MovementController>();
            var bomb = go.GetComponent<BombController>();

            if (move != null)
                move.SetPlayerId(playerId);

            if (bomb != null)
                bomb.SetPlayerId(playerId);

            if (go.TryGetComponent<PlayerLouieCompanion>(out var louie))
                louie.RestoreMountedFromPersistent();

            var skins = go.GetComponentsInChildren<PlayerBomberSkinController>(true);
            for (int s = 0; s < skins.Length; s++)
                if (skins[s] != null)
                    skins[s].ApplyFromIdentity();
        }
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

        var ov = playerOverrides[playerIndex];
        if (ov == null)
            return false;

        return ov.TryGetPosition(out pos);
    }

    public void SpawnNow(GameObject prefab, int playerIndex, Transform parent = null)
    {
        if (prefab == null)
            return;

        Vector2[] preset = isBossStage ? BossStagePositions : NormalStagePositions;
        Vector3 spawnPos = ResolveSpawnPosition(playerIndex, preset);
        Instantiate(prefab, spawnPos, Quaternion.identity, parent);
    }

    public void SpawnNow(GameObject prefab, int playerIndex)
    {
        SpawnNow(prefab, playerIndex, null);
    }

    public void SpawnNow(GameObject prefab)
    {
        if (prefab == null)
            return;

        int count = 1;

        if (GameSession.Instance != null)
            count = Mathf.Clamp(GameSession.Instance.ActivePlayerCount, 1, 4);

        Vector2[] preset = isBossStage ? BossStagePositions : NormalStagePositions;

        for (int i = 0; i < count; i++)
        {
            Vector3 spawnPos = ResolveSpawnPosition(i, preset);
            Instantiate(prefab, spawnPos, Quaternion.identity, playersParent);
        }
    }
}

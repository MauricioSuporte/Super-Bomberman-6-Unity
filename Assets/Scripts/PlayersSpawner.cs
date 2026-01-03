using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayersSpawner : MonoBehaviour
{
    [Header("Player Prefab")]
    [SerializeField] private GameObject playerPrefab;

    [Header("Optional Spawn Overrides (1..4)")]
    [SerializeField] private Transform[] spawnPoints = new Transform[4];

    [Header("Optional Parent")]
    [SerializeField] private Transform playersParent;

    [Header("Stage Type")]
    [SerializeField] private bool isBossStage;

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

    void Start()
    {
        SpawnPlayers();
    }

    void SpawnPlayers()
    {
        int count = 1;

        if (GameSession.Instance != null)
            count = GameSession.Instance.ActivePlayerCount;

        Vector2[] preset = isBossStage ? BossStagePositions : NormalStagePositions;

        for (int i = 0; i < count; i++)
        {
            Vector3 spawnPos = ResolveSpawnPosition(i, preset);

            var go = Instantiate(playerPrefab, spawnPos, Quaternion.identity, playersParent);

            if (!go.TryGetComponent<PlayerIdentity>(out var id))
                id = go.AddComponent<PlayerIdentity>();

            id.playerId = i + 1;
        }
    }

    Vector3 ResolveSpawnPosition(int index, Vector2[] preset)
    {
        if (spawnPoints != null &&
            index < spawnPoints.Length &&
            spawnPoints[index] != null)
        {
            return spawnPoints[index].position;
        }

        if (preset != null && index < preset.Length)
            return preset[index];

        return Vector3.zero;
    }
}

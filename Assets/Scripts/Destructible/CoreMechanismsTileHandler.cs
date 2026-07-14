using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public sealed class CoreMechanismsTileHandler : MonoBehaviour, IDestructibleTileHandler
{
    [SerializeField] private CoreMechanismsDestructible deathPrefab;
    [SerializeField] private Vector3 spawnOffset = Vector3.zero;
    [SerializeField] private AudioClip allDestroyedSfx;
    [SerializeField, Min(0f)] private float allDestroyedSfxVolume = 3f;

    AudioSource audioSource;

    static bool allDestroyedSfxPlayed;
    static ulong sceneHandleRaw = ulong.MaxValue;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStaticStateOnSubsystemRegistration()
    {
        ResetStageCounter();
        sceneHandleRaw = ulong.MaxValue;
    }

    void Awake()
    {
        Scene currentScene = SceneManager.GetActiveScene();
        ulong currentSceneHandle = currentScene.handle.GetRawData();
        if (sceneHandleRaw != currentSceneHandle)
        {
            sceneHandleRaw = currentSceneHandle;
            ResetStageCounter();
        }

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;
    }

    public bool HandleHit(BombController source, Vector2 worldPos, Vector3Int cell)
    {
        if (source == null)
            return false;

        Tilemap destructibleTiles = source.destructibleTiles;
        TileBase coreMechanismsTile = destructibleTiles != null ? destructibleTiles.GetTile(cell) : null;
        Vector3 spawnPosition = destructibleTiles != null
            ? destructibleTiles.GetCellCenterWorld(cell)
            : new Vector3(Mathf.Round(worldPos.x), Mathf.Round(worldPos.y), 0f);

        Transform parent = destructibleTiles != null ? destructibleTiles.transform : null;

        source.ClearDestructibleForEffect(worldPos, spawnDestructiblePrefab: false, spawnHiddenObject: true);
        bool allDestroyed = !HasRemainingCoreMechanismsTiles(destructibleTiles, coreMechanismsTile);

        if (deathPrefab != null)
        {
            CoreMechanismsDestructible instance = parent != null
                ? Instantiate(deathPrefab, spawnPosition + spawnOffset, Quaternion.identity, parent)
                : Instantiate(deathPrefab, spawnPosition + spawnOffset, Quaternion.identity);

            instance.PlayDeath();
        }

        if (allDestroyed)
            PlayAllDestroyedSfx(spawnPosition);

        return true;
    }

    static void ResetStageCounter()
    {
        allDestroyedSfxPlayed = false;
    }

    static bool IsCoreMechanismsTileName(TileBase tile)
    {
        return tile != null &&
               !string.IsNullOrWhiteSpace(tile.name) &&
               tile.name.StartsWith("CoreMechanisms", System.StringComparison.OrdinalIgnoreCase);
    }

    static bool HasRemainingCoreMechanismsTiles(Tilemap destructibleTiles, TileBase coreMechanismsTile)
    {
        if (destructibleTiles == null)
            return false;

        int remaining = 0;
        BoundsInt bounds = destructibleTiles.cellBounds;
        foreach (Vector3Int position in bounds.allPositionsWithin)
        {
            TileBase tile = destructibleTiles.GetTile(position);
            if (tile == null)
                continue;

            if ((coreMechanismsTile != null && tile == coreMechanismsTile) || IsCoreMechanismsTileName(tile))
            {
                remaining++;
            }
        }

        return remaining > 0;
    }

    void PlayAllDestroyedSfx(Vector3 position)
    {
        if (allDestroyedSfxPlayed || allDestroyedSfx == null)
            return;

        allDestroyedSfxPlayed = true;

        if (audioSource != null)
            GameAudioSettings.PlaySfx(audioSource, allDestroyedSfx, allDestroyedSfxVolume);
        else
            GameAudioSettings.PlaySfxAtPoint(allDestroyedSfx, position, allDestroyedSfxVolume);
    }
}

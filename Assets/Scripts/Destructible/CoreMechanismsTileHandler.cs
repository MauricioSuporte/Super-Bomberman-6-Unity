using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class CoreMechanismsTileHandler : MonoBehaviour, IDestructibleTileHandler
{
    [SerializeField] private CoreMechanismsDestructible deathPrefab;
    [SerializeField] private Vector3 spawnOffset = Vector3.zero;

    public bool HandleHit(BombController source, Vector2 worldPos, Vector3Int cell)
    {
        if (source == null)
            return false;

        Tilemap destructibleTiles = source.destructibleTiles;
        Vector3 spawnPosition = destructibleTiles != null
            ? destructibleTiles.GetCellCenterWorld(cell)
            : new Vector3(Mathf.Round(worldPos.x), Mathf.Round(worldPos.y), 0f);

        Transform parent = destructibleTiles != null ? destructibleTiles.transform : null;
        source.ClearDestructibleForEffect(worldPos, spawnDestructiblePrefab: false, spawnHiddenObject: true);

        if (deathPrefab != null)
        {
            CoreMechanismsDestructible instance = parent != null
                ? Instantiate(deathPrefab, spawnPosition + spawnOffset, Quaternion.identity, parent)
                : Instantiate(deathPrefab, spawnPosition + spawnOffset, Quaternion.identity);

            instance.PlayDeath();
        }

        return true;
    }
}

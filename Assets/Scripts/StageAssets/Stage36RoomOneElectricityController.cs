using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace StageAssets
{
    /// <summary>
    /// Drives the Room 1 electrical conductors in Stage 3-6. Pairs are authored
    /// as two Tile_04_04 tiles with exactly one empty tile between them horizontally.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Stage36RoomOneElectricityController : MonoBehaviour
    {
        [Header("Tile Detection")]
        [SerializeField] private Tilemap indestructibleTilemap;
        [SerializeField] private BoxCollider2D roomBounds;
        [SerializeField] private TileBase conductorTile;
        [SerializeField] private TileBase flashingTile;

        [Header("Shock Visual")]
        [SerializeField] private Sprite lightningSprite0;
        [SerializeField] private Sprite lightningSprite1;
        [SerializeField] private string shockSortingLayerName = "Enemy";
        [SerializeField] private int shockSortingOrder = 10;

        [Header("Timing")]
        [SerializeField, Min(0f)] private float minimumDelaySeconds = 10f;
        [SerializeField, Min(0f)] private float maximumDelaySeconds = 20f;
        [SerializeField, Min(0.01f)] private float frameSeconds = 0.1f;
        [SerializeField, Min(0.01f)] private float warningSeconds = 1f;
        [SerializeField, Min(0.01f)] private float shockSeconds = 2f;

        [Header("Audio")]
        [SerializeField] private AudioClip shockSfx;

        private readonly List<ConductorPair> conductorPairs = new();
        private readonly List<ShockVisual> activeShocks = new();
        private AudioSource audioSource;

        private sealed class ConductorPair
        {
            public Vector3Int firstCell;
            public Vector3Int secondCell;
            public Vector3Int shockCell;
        }

        private sealed class ShockVisual
        {
            public GameObject gameObject;
            public SpriteRenderer renderer;
            public Vector3 worldPosition;
        }

        private void Awake()
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 0f;

            FindConductorPairs();
        }

        private void OnEnable()
        {
            for (int i = 0; i < conductorPairs.Count; i++)
                StartCoroutine(ElectricityLoop(conductorPairs[i]));
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            RestoreConductors();
            ClearShockVisuals();
        }

        private void FindConductorPairs()
        {
            conductorPairs.Clear();

            if (indestructibleTilemap == null || roomBounds == null || conductorTile == null || flashingTile == null)
            {
                Debug.LogWarning("[Stage36Electricity] Missing tilemap, Room 1 bounds, or tile references; electricity is disabled.", this);
                return;
            }

            BoundsInt cellBounds = indestructibleTilemap.cellBounds;
            foreach (Vector3Int cell in cellBounds.allPositionsWithin)
            {
                if (indestructibleTilemap.GetTile(cell) != conductorTile || !IsInRoom(cell))
                    continue;

                Vector3Int otherCell = cell + Vector3Int.right * 2;
                if (indestructibleTilemap.GetTile(otherCell) != conductorTile || !IsInRoom(otherCell))
                    continue;

                conductorPairs.Add(new ConductorPair
                {
                    firstCell = cell,
                    secondCell = otherCell,
                    shockCell = cell + Vector3Int.right
                });
            }

        }

        private bool IsInRoom(Vector3Int cell)
        {
            Vector3 worldPosition = indestructibleTilemap.GetCellCenterWorld(cell);
            return roomBounds.OverlapPoint(worldPosition);
        }

        private IEnumerator ElectricityLoop(ConductorPair pair)
        {
            while (true)
            {
                float minDelay = Mathf.Min(minimumDelaySeconds, maximumDelaySeconds);
                float maxDelay = Mathf.Max(minimumDelaySeconds, maximumDelaySeconds);
                float delay = Random.Range(minDelay, maxDelay);
                yield return new WaitForSeconds(delay);

                yield return FlashConductors(pair);
                yield return RunShock(pair);
            }
        }

        private IEnumerator FlashConductors(ConductorPair pair)
        {
            float elapsed = 0f;
            bool useWarningTile = true;
            while (elapsed < warningSeconds)
            {
                SetConductorTiles(pair, useWarningTile ? flashingTile : conductorTile);
                useWarningTile = !useWarningTile;
                yield return new WaitForSeconds(frameSeconds);
                elapsed += frameSeconds;
            }

            RestoreConductors(pair);
        }

        private IEnumerator RunShock(ConductorPair pair)
        {
            ShockVisual shock = CreateShockVisual(pair);
            if (shock == null)
                yield break;

            GameAudioSettings.PlaySfx(audioSource, shockSfx);

            float elapsed = 0f;
            bool useFirstSprite = true;
            while (elapsed < shockSeconds)
            {
                if (shock.renderer != null)
                    shock.renderer.sprite = useFirstSprite ? lightningSprite0 : lightningSprite1;

                DamagePlayersAt(shock.worldPosition);

                useFirstSprite = !useFirstSprite;
                yield return new WaitForSeconds(frameSeconds);
                elapsed += frameSeconds;
            }

            ClearShockVisual(shock);
        }

        private void SetConductorTiles(ConductorPair pair, TileBase tile)
        {
            indestructibleTilemap.SetTile(pair.firstCell, tile);
            indestructibleTilemap.SetTile(pair.secondCell, tile);
        }

        private void RestoreConductors()
        {
            for (int i = 0; i < conductorPairs.Count; i++)
                RestoreConductors(conductorPairs[i]);
        }

        private void RestoreConductors(ConductorPair pair)
        {
            if (indestructibleTilemap != null && conductorTile != null)
                SetConductorTiles(pair, conductorTile);
        }

        private ShockVisual CreateShockVisual(ConductorPair pair)
        {
            if (lightningSprite0 == null || lightningSprite1 == null)
            {
                Debug.LogWarning("[Stage36Electricity] Lightning sprites are missing; shock was not created.", this);
                return null;
            }

            int enemyLayer = LayerMask.NameToLayer("Enemy");
            Vector3 worldPosition = indestructibleTilemap.GetCellCenterWorld(pair.shockCell);
            GameObject shockObject = new($"Stage36Shock_{pair.shockCell.x}_{pair.shockCell.y}");
            shockObject.transform.position = worldPosition;
            shockObject.layer = enemyLayer >= 0 ? enemyLayer : gameObject.layer;

            SpriteRenderer renderer = shockObject.AddComponent<SpriteRenderer>();
            renderer.sprite = lightningSprite0;
            renderer.sortingLayerName = shockSortingLayerName;
            renderer.sortingOrder = shockSortingOrder;

            BoxCollider2D collider = shockObject.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = Vector2.one;

            ShockVisual shock = new()
            {
                gameObject = shockObject,
                renderer = renderer,
                worldPosition = worldPosition
            };
            activeShocks.Add(shock);
            return shock;
        }

        private void DamagePlayersAt(Vector3 worldPosition)
        {
            MovementController[] players = FindObjectsByType<MovementController>(FindObjectsInactive.Exclude);
            for (int i = 0; i < players.Length; i++)
            {
                MovementController player = players[i];
                if (player == null || player.isDead || !player.CompareTag("Player"))
                    continue;

                Vector2 playerPosition = player.Rigidbody != null ? player.Rigidbody.position : player.transform.position;
                if (Vector2.Distance(playerPosition, worldPosition) > 0.45f)
                    continue;

                CharacterHealth health = player.GetComponent<CharacterHealth>();
                health?.TakeDamage(1);
            }
        }

        private void ClearShockVisuals()
        {
            for (int i = 0; i < activeShocks.Count; i++)
                if (activeShocks[i].gameObject != null)
                    Destroy(activeShocks[i].gameObject);

            activeShocks.Clear();
        }

        private void ClearShockVisual(ShockVisual shock)
        {
            if (shock == null)
                return;

            activeShocks.Remove(shock);
            if (shock.gameObject != null)
                Destroy(shock.gameObject);
        }
    }
}

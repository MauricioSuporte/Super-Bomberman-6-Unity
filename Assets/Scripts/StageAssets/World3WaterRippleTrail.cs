using UnityEngine;
using UnityEngine.Tilemaps;

namespace StageAssets
{
    /// <summary>
    /// Leaves a short-lived pixel ripple on each World 3 water cell a player
    /// moves away from. The component is added only to spawned players.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class World3WaterRippleTrail : MonoBehaviour
    {
        private const float RippleLifetime = 0.65f;

        private Tilemap groundTilemap;
        private Tilemap destructibleTilemap;
        private World3GateOpenedSequenceController gateSequence;
        private Vector3Int previousCell;
        private bool hasPreviousCell;
        private int bombLayerMask;

        private void Start()
        {
            bombLayerMask = LayerMask.GetMask("Bomb");
            ResolveStageReferences();
            CaptureCurrentCell();
        }

        private void LateUpdate()
        {
            if (groundTilemap == null || destructibleTilemap == null || gateSequence == null)
                ResolveStageReferences();

            if (groundTilemap == null)
                return;

            Vector3Int currentCell = groundTilemap.WorldToCell(transform.position);
            if (!hasPreviousCell)
            {
                previousCell = currentCell;
                hasPreviousCell = true;
                return;
            }

            if (currentCell == previousCell)
                return;

            if (IsWaterCell(previousCell))
                SpawnRipple(previousCell, currentCell);

            previousCell = currentCell;
        }

        private void ResolveStageReferences()
        {
            groundTilemap = GameManager.Instance != null
                ? GameManager.Instance.groundTilemap
                : null;

            destructibleTilemap = GameManager.Instance != null
                ? GameManager.Instance.destructibleTilemap
                : null;

            if (groundTilemap == null)
            {
                Tilemap[] tilemaps = FindObjectsByType<Tilemap>(FindObjectsInactive.Exclude);
                for (int i = 0; i < tilemaps.Length; i++)
                {
                    if (tilemaps[i] != null && tilemaps[i].name == "Ground")
                    {
                        groundTilemap = tilemaps[i];
                        break;
                    }
                }
            }

            gateSequence ??= FindAnyObjectByType<World3GateOpenedSequenceController>();
        }

        private void CaptureCurrentCell()
        {
            if (groundTilemap == null)
                return;

            previousCell = groundTilemap.WorldToCell(transform.position);
            hasPreviousCell = true;
        }

        private bool IsWaterCell(Vector3Int cell)
        {
            if (groundTilemap.GetTile(cell) == null)
                return false;

            Vector3 worldPosition = groundTilemap.GetCellCenterWorld(cell);
            if (destructibleTilemap != null &&
                destructibleTilemap.GetTile(destructibleTilemap.WorldToCell(worldPosition)) != null)
            {
                return false;
            }

            if (bombLayerMask != 0 &&
                Physics2D.OverlapCircle(worldPosition, 0.2f, bombLayerMask) != null)
            {
                return false;
            }

            if (gateSequence != null && gateSequence.IsTargetTileAtWorldPosition(worldPosition))
                return false;

            return true;
        }

        private void SpawnRipple(Vector3Int cell, Vector3Int nextCell)
        {
            Vector2 movement = new(nextCell.x - cell.x, nextCell.y - cell.y);
            if (movement.sqrMagnitude <= 0.0001f)
                return;

            movement.Normalize();
            Vector3 center = groundTilemap.GetCellCenterWorld(cell);

            // The rounded end stays closest to where the player just was;
            // the open end faces behind the player's movement, like a boat wake.
            SpawnWake(center, -movement);
        }

        private void SpawnWake(Vector3 position, Vector2 openDirection)
        {
            GameObject rippleObject = new("World3WaterRipple");
            rippleObject.transform.position = position;

            SpriteRenderer renderer = rippleObject.AddComponent<SpriteRenderer>();
            renderer.sprite = World3WaterRippleVisual.TrailSprite;
            renderer.sortingLayerID = groundTilemap.GetComponent<TilemapRenderer>().sortingLayerID;
            renderer.sortingOrder = 4;

            World3WaterRippleVisual visual = rippleObject.AddComponent<World3WaterRippleVisual>();
            visual.Initialize(
                renderer,
                RippleLifetime,
                openDirection);
        }
    }

    public sealed class World3WaterRippleVisual : MonoBehaviour
    {
        private static Sprite trailSprite;

        private SpriteRenderer spriteRenderer;
        private float lifetime;
        private float elapsed;

        public static Sprite TrailSprite => trailSprite ??= CreateTrailSprite();

        public void Initialize(
            SpriteRenderer renderer,
            float duration,
            Vector2 openDirection)
        {
            spriteRenderer = renderer;
            lifetime = Mathf.Max(0.01f, duration);
            elapsed = 0f;

            if (openDirection.sqrMagnitude <= 0.0001f)
                openDirection = Vector2.right;

            float angle = Mathf.Atan2(openDirection.y, openDirection.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
            transform.localScale = new Vector3(0.72f, 0.72f, 1f);
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / lifetime);

            transform.localScale = new Vector3(
                Mathf.Lerp(0.72f, 1.12f, progress),
                Mathf.Lerp(0.72f, 1.12f, progress),
                1f);
            if (spriteRenderer != null)
                spriteRenderer.color = new Color(0.76f, 0.96f, 1f, (1f - progress) * 0.95f);

            if (progress >= 1f)
                Destroy(gameObject);
        }

        private static Sprite CreateTrailSprite()
        {
            const int width = 16;
            const int height = 12;
            Texture2D texture = new(width, height, TextureFormat.RGBA32, mipChain: false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "World3WaterTrail_Runtime"
            };

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float progress = x / (float)(width - 1);
                    float halfWidth = Mathf.Lerp(1.2f, 4.5f, progress * progress);
                    float distanceFromCenter = Mathf.Abs(y - (height - 1) * 0.5f);

                    // Local +X is the open end. The two arms curve outward from
                    // a rounded closed end, producing a pixel-art U-shaped wake.
                    bool isArm = x > 1 && Mathf.Abs(distanceFromCenter - halfWidth) <= 0.8f;
                    bool isRoundedEnd = x <= 2 && distanceFromCenter <= halfWidth + 0.5f;
                    texture.SetPixel(x, y, isArm || isRoundedEnd ? Color.white : Color.clear);
                }
            }

            texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            return Sprite.Create(
                texture,
                new Rect(0f, 0f, width, height),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit: 16f,
                extrude: 0,
                meshType: SpriteMeshType.FullRect);
        }
    }
}

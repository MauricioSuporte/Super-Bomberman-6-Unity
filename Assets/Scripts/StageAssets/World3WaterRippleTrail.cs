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
                SpawnRipple(previousCell);

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

            gateSequence ??= FindFirstObjectByType<World3GateOpenedSequenceController>();
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

            return gateSequence == null ||
                   !gateSequence.IsTargetTileAtWorldPosition(worldPosition);
        }

        private void SpawnRipple(Vector3Int cell)
        {
            GameObject rippleObject = new("World3WaterRipple");
            rippleObject.transform.position = groundTilemap.GetCellCenterWorld(cell);

            SpriteRenderer renderer = rippleObject.AddComponent<SpriteRenderer>();
            renderer.sprite = World3WaterRippleVisual.RippleSprite;
            renderer.sortingLayerID = groundTilemap.GetComponent<TilemapRenderer>().sortingLayerID;
            renderer.sortingOrder = 2;

            World3WaterRippleVisual visual = rippleObject.AddComponent<World3WaterRippleVisual>();
            visual.Initialize(renderer, RippleLifetime);
        }
    }

    public sealed class World3WaterRippleVisual : MonoBehaviour
    {
        private static Sprite rippleSprite;

        private SpriteRenderer spriteRenderer;
        private float lifetime;
        private float elapsed;

        public static Sprite RippleSprite => rippleSprite ??= CreateRippleSprite();

        public void Initialize(SpriteRenderer renderer, float duration)
        {
            spriteRenderer = renderer;
            lifetime = Mathf.Max(0.01f, duration);
            elapsed = 0f;
            transform.localScale = new Vector3(0.55f, 0.55f, 1f);
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / lifetime);

            transform.localScale = Vector3.one * Mathf.Lerp(0.55f, 1.15f, progress);
            if (spriteRenderer != null)
                spriteRenderer.color = new Color(0.7f, 0.92f, 1f, (1f - progress) * 0.8f);

            if (progress >= 1f)
                Destroy(gameObject);
        }

        private static Sprite CreateRippleSprite()
        {
            const int width = 16;
            const int height = 8;
            Texture2D texture = new(width, height, TextureFormat.RGBA32, mipChain: false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "World3WaterRipple_Runtime"
            };

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float horizontal = (x - (width - 1) * 0.5f) / ((width - 1) * 0.5f);
                    float vertical = (y - (height - 1) * 0.5f) / ((height - 1) * 0.5f);
                    float distance = Mathf.Sqrt(horizontal * horizontal + vertical * vertical);
                    bool isRing = Mathf.Abs(distance - 0.72f) <= 0.13f;
                    texture.SetPixel(x, y, isRing ? Color.white : Color.clear);
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

using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace StageAssets
{
    public sealed class World3GateOpenedSequenceController : MonoBehaviour
    {
        private const string BubbleCrashLogPrefix = "[World3BubbleCrash]";

        [System.Serializable]
        public struct IndestructibleTileSwap
        {
            public TileBase sourceTile;
            public TileBase targetTile;
        }

        [SerializeField, Min(0f)] private float delayAfterGateOpenedSeconds = 1f;
        [SerializeField] private AudioClip bubbleCrashSfx;
        [SerializeField, Min(0f)] private float bubbleCrashSfxVolume = 3f;
        [SerializeField] private Sprite bubbleCrashSprite;
        [SerializeField] private string bubbleCrashSpriteEditorPath = "Assets/Sprites/StageAssets/World 3/End Stage/BubbleCrash.png";
        [SerializeField, Min(0)] private int bubbleCrashSpriteCount = 6;
        [SerializeField, Min(0f)] private float bubbleCrashRadius = 0.45f;
        [SerializeField] private Vector2 bubbleCrashCenterOffset = Vector2.zero;
        [SerializeField] private Tilemap indestructibleTilemap;
        [SerializeField] private IndestructibleTileSwap[] tileSwaps;
        [SerializeField] private GameObject[] objectsToDisableOnSequence;
        [SerializeField] private string[] fallbackObjectNamesToDisable = { "Bubble" };

        private bool sequenceStarted;

        private void OnEnable()
        {
            ResolveBubbleCrashSpriteIfNeeded();
            CoreMechanismsDestructible.AllCoreMechanismsDestroyed += HandleAllCoreMechanismsDestroyed;
        }

        private void OnDisable()
        {
            CoreMechanismsDestructible.AllCoreMechanismsDestroyed -= HandleAllCoreMechanismsDestroyed;
        }

        private void HandleAllCoreMechanismsDestroyed()
        {
            if (sequenceStarted)
                return;

            sequenceStarted = true;
            StartCoroutine(SequenceRoutine());
        }

        private IEnumerator SequenceRoutine()
        {
            if (delayAfterGateOpenedSeconds > 0f)
                yield return new WaitForSeconds(delayAfterGateOpenedSeconds);

            PlayBubbleCrashSfx();
            SpawnBubbleCrashSprites();
            ApplyTileSwaps();
            DisableSequenceObjects();
        }

        private void PlayBubbleCrashSfx()
        {
            if (bubbleCrashSfx == null)
                return;

            GameObject temp = new("World3_BubbleCrashSfx");
            AudioSource source = temp.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.PlayOneShot(bubbleCrashSfx, GetBoostedSfxVolume(bubbleCrashSfxVolume));
            Destroy(temp, Mathf.Max(0.1f, bubbleCrashSfx.length + 0.1f));
        }

        private void SpawnBubbleCrashSprites()
        {
            ResolveBubbleCrashSpriteIfNeeded();

            if (bubbleCrashSprite == null)
            {
                Debug.LogWarning($"{BubbleCrashLogPrefix} Spawn skipped: bubbleCrashSprite is NULL. " +
                                 $"Editor fallback path='{bubbleCrashSpriteEditorPath}'. " +
                                 "Assign the sprite on the Stage_3-1 controller if this is running outside the Unity Editor.", this);
                return;
            }

            if (bubbleCrashSpriteCount <= 0)
            {
                Debug.LogWarning($"{BubbleCrashLogPrefix} Spawn skipped: bubbleCrashSpriteCount is {bubbleCrashSpriteCount}.", this);
                return;
            }

            Transform chip = FindBubbleChipChild("Chip");
            Vector3 center = chip != null ? chip.position : transform.position;
            center += (Vector3)bubbleCrashCenterOffset;

            Transform parent = transform;
            int sortingLayerId = 0;
            string sortingLayerName = "Default";
            int backSortingOrder = 8;
            int frontSortingOrder = 11;

            if (chip != null && chip.TryGetComponent(out SpriteRenderer chipRenderer))
            {
                sortingLayerId = chipRenderer.sortingLayerID;
                sortingLayerName = chipRenderer.sortingLayerName;
                backSortingOrder = chipRenderer.sortingOrder - 1;
                frontSortingOrder = chipRenderer.sortingOrder + 2;
            }

            int createdCount = 0;
            int flippedCount = 0;
            int behindChipCount = 0;
            for (int i = 0; i < bubbleCrashSpriteCount; i++)
            {
                float angle = Mathf.PI * 2f * i / bubbleCrashSpriteCount;
                Vector3 offset = new(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                bool behindChip = offset.y > 0.001f;
                GameObject piece = new($"BubbleCrash_{i + 1:00}");
                piece.transform.SetParent(parent, true);
                piece.transform.position = center + offset * bubbleCrashRadius;

                SpriteRenderer renderer = piece.AddComponent<SpriteRenderer>();
                renderer.sprite = bubbleCrashSprite;
                renderer.sortingLayerID = sortingLayerId;
                renderer.sortingOrder = behindChip ? backSortingOrder : frontSortingOrder;
                renderer.flipX = offset.x < -0.001f;
                createdCount++;
                if (renderer.flipX)
                    flippedCount++;
                if (behindChip)
                    behindChipCount++;
            }

            Debug.Log($"{BubbleCrashLogPrefix} Spawned {createdCount}/{bubbleCrashSpriteCount}. " +
                      $"chip={(chip != null ? GetHierarchyPath(chip) : "NOT FOUND")}, " +
                      $"parent={GetHierarchyPath(parent)}, center={center}, radius={bubbleCrashRadius}, " +
                      $"flipX={flippedCount}, behindChip={behindChipCount}, " +
                      $"sortingLayer='{sortingLayerName}'({sortingLayerId}), backOrder={backSortingOrder}, frontOrder={frontSortingOrder}.", this);
        }

        private void ResolveBubbleCrashSpriteIfNeeded()
        {
            if (bubbleCrashSprite != null)
                return;

#if UNITY_EDITOR
            if (string.IsNullOrWhiteSpace(bubbleCrashSpriteEditorPath))
            {
                Debug.LogWarning($"{BubbleCrashLogPrefix} Sprite resolve skipped: editor path is empty.", this);
                return;
            }

            bubbleCrashSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(bubbleCrashSpriteEditorPath);
            if (bubbleCrashSprite != null)
                return;

            Debug.LogWarning($"{BubbleCrashLogPrefix} Could not resolve BubbleCrash sprite at editor path '{bubbleCrashSpriteEditorPath}'.", this);
#endif
        }

        private void ApplyTileSwaps()
        {
            if (indestructibleTilemap == null || tileSwaps == null)
                return;

            BoundsInt bounds = indestructibleTilemap.cellBounds;
            foreach (Vector3Int cell in bounds.allPositionsWithin)
            {
                TileBase currentTile = indestructibleTilemap.GetTile(cell);
                if (currentTile == null)
                    continue;

                for (int i = 0; i < tileSwaps.Length; i++)
                {
                    if (tileSwaps[i].sourceTile == null ||
                        tileSwaps[i].targetTile == null ||
                        currentTile != tileSwaps[i].sourceTile)
                    {
                        continue;
                    }

                    indestructibleTilemap.SetTile(cell, tileSwaps[i].targetTile);
                    indestructibleTilemap.SetTransformMatrix(cell, Matrix4x4.identity);
                    indestructibleTilemap.RefreshTile(cell);

                    if (GameManager.Instance != null)
                        GameManager.Instance.OnIndestructiblePlaced(cell);

                    break;
                }
            }
        }

        private static float GetBoostedSfxVolume(float volume)
        {
            return Mathf.Max(0f, volume) * Mathf.Clamp01(GameAudioSettings.SfxVolume);
        }

        private void DisableSequenceObjects()
        {
            if (objectsToDisableOnSequence != null)
            {
                for (int i = 0; i < objectsToDisableOnSequence.Length; i++)
                {
                    if (objectsToDisableOnSequence[i] != null)
                        objectsToDisableOnSequence[i].SetActive(false);
                }
            }

            if (fallbackObjectNamesToDisable == null || fallbackObjectNamesToDisable.Length == 0)
                return;

            Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Exclude);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate == null || !ShouldDisableByFallbackName(candidate.name))
                    continue;

                if (!HasParentNamed(candidate, "BubbleChip"))
                    continue;

                candidate.gameObject.SetActive(false);
            }
        }

        private static Transform FindBubbleChipChild(string childName)
        {
            Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Exclude);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate == null ||
                    !string.Equals(candidate.name, childName, System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (HasParentNamed(candidate, "BubbleChip"))
                    return candidate;
            }

            return null;
        }

        private static string GetHierarchyPath(Transform transform)
        {
            if (transform == null)
                return "NULL";

            string path = transform.name;
            Transform current = transform.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }

        private bool ShouldDisableByFallbackName(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
                return false;

            for (int i = 0; i < fallbackObjectNamesToDisable.Length; i++)
            {
                if (string.Equals(objectName, fallbackObjectNamesToDisable[i], System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static bool HasParentNamed(Transform transform, string parentName)
        {
            Transform current = transform.parent;
            while (current != null)
            {
                if (string.Equals(current.name, parentName, System.StringComparison.OrdinalIgnoreCase))
                    return true;

                current = current.parent;
            }

            return false;
        }
    }
}

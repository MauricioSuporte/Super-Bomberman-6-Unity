using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace StageAssets
{
    public sealed class World3GateOpenedSequenceController : MonoBehaviour
    {
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
        [SerializeField, Min(0f)] private float bubbleCrashPhase2Delay = 1f;
        [SerializeField, Min(1)] private int bubbleCrashThrownSpriteCount = 8;
        [SerializeField, Min(0f)] private float bubbleCrashFirstFlightDuration = 1f;
        [SerializeField, Min(0f)] private float bubbleCrashFirstFlightDistanceTiles = 2f;
        [SerializeField, Min(0f)] private float bubbleCrashFirstFlightHeightTiles = 4f;
        [SerializeField, Min(0f)] private float bubbleCrashBounceDuration = 1f;
        [SerializeField, Min(0f)] private float bubbleCrashBounceDistanceTiles = 2f;
        [SerializeField, Min(0f)] private float bubbleCrashBounceHeightTiles = 1f;
        [SerializeField, Min(1)] private int bubbleCrashBounceCount = 3;
        [SerializeField, Min(0f)] private float bubbleCrashBlinkOutDuration = 0.25f;
        [SerializeField, Min(1f)] private float bubbleCrashPixelsPerUnit = 16f;
        [SerializeField] private Tilemap indestructibleTilemap;
        [SerializeField] private IndestructibleTileSwap[] tileSwaps;
        [SerializeField] private GameObject[] objectsToDisableOnSequence;
        [SerializeField] private string[] fallbackObjectNamesToDisable = { "Bubble" };

        private bool sequenceStarted;
        private readonly List<GameObject> bubbleCrashPhase1Pieces = new();

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
                return;

            if (bubbleCrashSpriteCount <= 0)
                return;

            Transform chip = FindBubbleChipChild("Chip");
            Vector3 center = chip != null ? chip.position : transform.position;
            center += (Vector3)bubbleCrashCenterOffset;

            Transform parent = transform;
            int sortingLayerId = 0;
            int backSortingOrder = 8;
            int frontSortingOrder = 11;

            if (chip != null && chip.TryGetComponent(out SpriteRenderer chipRenderer))
            {
                sortingLayerId = chipRenderer.sortingLayerID;
                backSortingOrder = chipRenderer.sortingOrder - 1;
                frontSortingOrder = chipRenderer.sortingOrder + 2;
            }

            bubbleCrashPhase1Pieces.Clear();
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
                bubbleCrashPhase1Pieces.Add(piece);
            }

            StartCoroutine(BubbleCrashPhase2Routine(center, sortingLayerId, frontSortingOrder));
        }

        private IEnumerator BubbleCrashPhase2Routine(
            Vector3 origin,
            int sortingLayerId,
            int sortingOrder)
        {
            if (bubbleCrashPhase2Delay > 0f)
                yield return new WaitForSeconds(bubbleCrashPhase2Delay);

            DestroyBubbleCrashPhase1Pieces();
            PlayBubbleCrashSfx();

            int count = Mathf.Max(1, bubbleCrashThrownSpriteCount);
            for (int i = 0; i < count; i++)
            {
                float angle = Mathf.PI * 2f * i / count;
                Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
                GameObject piece = CreateBubbleCrashPiece(
                    $"BubbleCrashThrown_{i + 1:00}",
                    origin,
                    sortingLayerId,
                    sortingOrder,
                    direction.x < -0.001f);

                StartCoroutine(AnimateThrownBubbleCrashPiece(piece, direction));
            }
        }

        private void DestroyBubbleCrashPhase1Pieces()
        {
            for (int i = 0; i < bubbleCrashPhase1Pieces.Count; i++)
            {
                if (bubbleCrashPhase1Pieces[i] == null)
                    continue;

                Destroy(bubbleCrashPhase1Pieces[i]);
            }

            bubbleCrashPhase1Pieces.Clear();
        }

        private GameObject CreateBubbleCrashPiece(
            string objectName,
            Vector3 position,
            int sortingLayerId,
            int sortingOrder,
            bool flipX)
        {
            GameObject piece = new(objectName);
            piece.transform.SetParent(transform, true);
            piece.transform.position = SnapPixelPerfect(position);

            SpriteRenderer renderer = piece.AddComponent<SpriteRenderer>();
            renderer.sprite = bubbleCrashSprite;
            renderer.sortingLayerID = sortingLayerId;
            renderer.sortingOrder = sortingOrder;
            renderer.flipX = flipX;
            return piece;
        }

        private IEnumerator AnimateThrownBubbleCrashPiece(GameObject piece, Vector2 direction)
        {
            if (piece == null)
                yield break;

            Vector3 origin = piece.transform.position;
            float firstDuration = Mathf.Max(0.01f, bubbleCrashFirstFlightDuration);
            float elapsed = 0f;
            while (elapsed < firstDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / firstDuration);
                Vector3 horizontal = (Vector3)(direction * (bubbleCrashFirstFlightDistanceTiles * t));
                Vector3 arc = Vector3.up * (Mathf.Sin(t * Mathf.PI) * bubbleCrashFirstFlightHeightTiles);
                piece.transform.position = SnapPixelPerfect(origin + horizontal + arc);
                yield return null;
            }

            Vector3 bounceOrigin = origin + (Vector3)(direction * bubbleCrashFirstFlightDistanceTiles);
            piece.transform.position = SnapPixelPerfect(bounceOrigin);

            float bounceDuration = Mathf.Max(0.01f, bubbleCrashBounceDuration);
            elapsed = 0f;
            while (elapsed < bounceDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / bounceDuration);
                float bouncePhase = Mathf.Sin(t * Mathf.PI * Mathf.Max(1, bubbleCrashBounceCount));
                float height = Mathf.Max(0f, bouncePhase) * Mathf.Lerp(bubbleCrashBounceHeightTiles, 0f, t);
                Vector3 horizontal = (Vector3)(direction * (bubbleCrashBounceDistanceTiles * t));
                piece.transform.position = SnapPixelPerfect(bounceOrigin + horizontal + Vector3.up * height);
                yield return null;
            }

            Vector3 finalPosition = bounceOrigin + (Vector3)(direction * bubbleCrashBounceDistanceTiles);
            piece.transform.position = SnapPixelPerfect(finalPosition);

            yield return BlinkOutAndDestroy(piece);
        }

        private IEnumerator BlinkOutAndDestroy(GameObject piece)
        {
            if (piece == null)
                yield break;

            SpriteRenderer renderer = piece.GetComponent<SpriteRenderer>();
            float duration = Mathf.Max(0.01f, bubbleCrashBlinkOutDuration);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                if (renderer != null)
                    renderer.enabled = Mathf.FloorToInt(elapsed / 0.04f) % 2 == 0;

                yield return null;
            }

            Destroy(piece);
        }

        private Vector3 SnapPixelPerfect(Vector3 position)
        {
            float ppu = Mathf.Max(1f, bubbleCrashPixelsPerUnit);
            position.x = Mathf.Round(position.x * ppu) / ppu;
            position.y = Mathf.Round(position.y * ppu) / ppu;
            return position;
        }

        private void ResolveBubbleCrashSpriteIfNeeded()
        {
            if (bubbleCrashSprite != null)
                return;

#if UNITY_EDITOR
            if (string.IsNullOrWhiteSpace(bubbleCrashSpriteEditorPath))
                return;

            bubbleCrashSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(bubbleCrashSpriteEditorPath);
            if (bubbleCrashSprite != null)
                return;
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

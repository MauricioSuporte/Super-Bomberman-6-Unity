using System.Collections;
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
        [SerializeField] private Tilemap indestructibleTilemap;
        [SerializeField] private IndestructibleTileSwap[] tileSwaps;
        [SerializeField] private GameObject[] objectsToDisableOnSequence;
        [SerializeField] private string[] fallbackObjectNamesToDisable = { "Bubble" };

        private bool sequenceStarted;

        private void OnEnable()
        {
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

using System.Collections;
using UnityEngine;

namespace StageAssets
{
    [RequireComponent(typeof(BoxCollider2D))]
    [DisallowMultipleComponent]
    public sealed class World3BambooExitBlocker : MonoBehaviour
    {
        [Header("Opening Movement")]
        [SerializeField, Min(0f)] private float riseDistanceTiles = 3f;
        [SerializeField, Min(0.01f)] private float riseDurationSeconds = 1f;
        [SerializeField, Min(1)] private int pixelsPerUnit = 16;

        [Header("Blocking")]
        [SerializeField] private BoxCollider2D blockingCollider;
        [SerializeField] private Vector2 blockingColliderSize = Vector2.one;

        private bool openingStarted;

        public bool IsOpeningStarted => openingStarted;
        public bool IsExitOpen => openingStarted && blockingCollider != null && !blockingCollider.enabled;

        private void Awake()
        {
            if (blockingCollider == null)
                blockingCollider = GetComponent<BoxCollider2D>();

            blockingCollider.isTrigger = false;
            blockingCollider.size = blockingColliderSize;
            blockingCollider.enabled = true;
        }

        private void OnEnable()
        {
            CoreMechanismsDestructible.AllCoreMechanismsDestroyed += OpenExit;
        }

        private void OnDisable()
        {
            CoreMechanismsDestructible.AllCoreMechanismsDestroyed -= OpenExit;
        }

        private void OpenExit()
        {
            if (openingStarted)
                return;

            openingStarted = true;
            StartCoroutine(RiseRoutine());
        }

        private IEnumerator RiseRoutine()
        {
            Vector3 startPosition = transform.position;
            Vector3 endPosition = startPosition + Vector3.up * riseDistanceTiles;
            float duration = Mathf.Max(0.01f, riseDurationSeconds);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                transform.position = SnapPixelPerfect(Vector3.Lerp(startPosition, endPosition, elapsed / duration));
                yield return null;
            }

            transform.position = SnapPixelPerfect(endPosition);
            blockingCollider.enabled = false;
        }

        private Vector3 SnapPixelPerfect(Vector3 position)
        {
            float ppu = Mathf.Max(1, pixelsPerUnit);
            position.x = Mathf.Round(position.x * ppu) / ppu;
            position.y = Mathf.Round(position.y * ppu) / ppu;
            return position;
        }
    }
}

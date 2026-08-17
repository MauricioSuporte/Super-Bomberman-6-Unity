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

        [Header("Optional Gate Sprite Sequence")]
        [SerializeField] private SpriteRenderer gateRenderer;
        [SerializeField] private Texture2D gateSpriteSheet;
        [SerializeField, Min(1)] private int gatePixelsPerUnit = 16;
        [SerializeField, Min(0.01f)] private float gateOpeningSeconds = 0.5f;

        [Header("Blocking")]
        [SerializeField] private BoxCollider2D blockingCollider;
        [SerializeField] private Vector2 blockingColliderSize = Vector2.one;
        [SerializeField] private bool openFromLegacyAllCoresEvent;

        private bool openingStarted;
        private Sprite[] runtimeGateSprites;

        public bool IsOpeningStarted => openingStarted;
        public bool IsExitOpen => openingStarted && blockingCollider != null && !blockingCollider.enabled;

        public void BeginOpening() => OpenExit();

        private void Awake()
        {
            if (blockingCollider == null)
                blockingCollider = GetComponent<BoxCollider2D>();

            blockingCollider.isTrigger = false;
            blockingCollider.size = blockingColliderSize;
            blockingCollider.enabled = true;

            if (UsesGateSpriteSequence())
                SetGateSprite(0);
        }

        private void OnEnable()
        {
            if (openFromLegacyAllCoresEvent)
                CoreMechanismsDestructible.AllCoreMechanismsDestroyed += OpenExit;
        }

        private void OnDisable()
        {
            if (openFromLegacyAllCoresEvent)
                CoreMechanismsDestructible.AllCoreMechanismsDestroyed -= OpenExit;
        }

        private void OpenExit()
        {
            if (openingStarted)
                return;

            openingStarted = true;
            StartCoroutine(UsesGateSpriteSequence() ? GateOpeningRoutine() : RiseRoutine());
        }

        private bool UsesGateSpriteSequence()
        {
            return gateRenderer != null &&
                   gateSpriteSheet != null &&
                   gateSpriteSheet.width >= 4 &&
                   gateSpriteSheet.height > 0;
        }

        private IEnumerator GateOpeningRoutine()
        {
            float frameDuration = Mathf.Max(0.01f, gateOpeningSeconds) / 3f;

            SetGateSprite(1);
            yield return new WaitForSeconds(frameDuration);

            SetGateSprite(2);
            yield return new WaitForSeconds(frameDuration);

            SetGateSprite(3);
            yield return new WaitForSeconds(frameDuration);

            blockingCollider.enabled = false;
        }

        private void SetGateSprite(int frameIndex)
        {
            if (gateRenderer == null || frameIndex < 0 || frameIndex >= 4)
                return;

            EnsureGateSprites();
            if (runtimeGateSprites != null && frameIndex < runtimeGateSprites.Length)
                gateRenderer.sprite = runtimeGateSprites[frameIndex];
        }

        private void EnsureGateSprites()
        {
            if (runtimeGateSprites != null || gateSpriteSheet == null)
                return;

            int frameWidth = gateSpriteSheet.width / 4;
            int frameHeight = gateSpriteSheet.height;
            if (frameWidth <= 0 || frameHeight <= 0)
                return;

            runtimeGateSprites = new Sprite[4];
            for (int i = 0; i < runtimeGateSprites.Length; i++)
            {
                runtimeGateSprites[i] = Sprite.Create(
                    gateSpriteSheet,
                    new Rect(i * frameWidth, 0f, frameWidth, frameHeight),
                    new Vector2(0.5f, 0.5f),
                    Mathf.Max(1, gatePixelsPerUnit));
                runtimeGateSprites[i].name = $"{gateSpriteSheet.name}_{i}";
            }
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

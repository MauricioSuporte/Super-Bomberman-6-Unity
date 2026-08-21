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
        [SerializeField] private Vector2 openingDirection = Vector2.down;
        [SerializeField, Min(1)] private int pixelsPerUnit = 16;

        [Header("Sand Sink")]
        [Tooltip("When enabled, the blocker sinks into the sand instead of rising.")]
        [SerializeField] private bool sinkIntoGround = true;
        [SerializeField, Min(0f)] private float sinkDistanceTiles = 0.5f;
        [SerializeField, Min(0f)] private float sinkHorizontalShakeTiles = 0.125f;
        [SerializeField, Min(0f)] private float sinkHorizontalShakeCycles = 4f;

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
        private SpriteRenderer blockerRenderer;
        private SpriteMask sinkMask;

        public bool IsOpeningStarted => openingStarted;
        public bool IsExitOpen => openingStarted && blockingCollider != null && !blockingCollider.enabled;

        public void BeginOpening() => OpenExit();

        private void Awake()
        {
            if (blockingCollider == null)
                blockingCollider = GetComponent<BoxCollider2D>();

            blockerRenderer = GetComponent<SpriteRenderer>();

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
            StartCoroutine(UsesGateSpriteSequence() ? GateOpeningRoutine() : OpenRoutine());
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

        private IEnumerator OpenRoutine()
        {
            Vector3 startPosition = transform.position;
            Vector2 direction = openingDirection.sqrMagnitude > 0.0001f
                ? openingDirection.normalized
                : Vector2.down;
            float distance = sinkIntoGround
                ? GetSinkDistanceToFullyHide(direction)
                : riseDistanceTiles;
            Vector3 endPosition = startPosition + (Vector3)direction * distance;
            float duration = Mathf.Max(0.01f, riseDurationSeconds);
            float elapsed = 0f;

            if (sinkIntoGround)
                CreateSinkMask();

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                Vector3 position = Vector3.Lerp(startPosition, endPosition, progress);
                if (sinkIntoGround)
                    position.x += GetSinkHorizontalShake(progress);

                transform.position = SnapPixelPerfect(position);

                yield return null;
            }

            transform.position = SnapPixelPerfect(endPosition);
            if (sinkIntoGround && blockerRenderer != null)
                blockerRenderer.enabled = false;

            if (sinkMask != null)
                sinkMask.gameObject.SetActive(false);

            blockingCollider.enabled = false;
        }

        private void CreateSinkMask()
        {
            if (blockerRenderer == null || blockerRenderer.sprite == null)
                return;

            blockerRenderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;

            GameObject maskObject = new("Sand Sink Mask")
            {
                hideFlags = HideFlags.DontSave
            };
            // The mask is deliberately not parented to the ship. It represents
            // the stationary sand line: the ship moves down through it.
            maskObject.transform.SetPositionAndRotation(
                blockerRenderer.transform.position,
                blockerRenderer.transform.rotation);
            maskObject.transform.localScale = blockerRenderer.transform.lossyScale;

            sinkMask = maskObject.AddComponent<SpriteMask>();
            sinkMask.sprite = blockerRenderer.sprite;
            sinkMask.alphaCutoff = 0.01f;
            sinkMask.backSortingLayerID = blockerRenderer.sortingLayerID;
            sinkMask.frontSortingLayerID = blockerRenderer.sortingLayerID;
            sinkMask.backSortingOrder = blockerRenderer.sortingOrder;
            sinkMask.frontSortingOrder = blockerRenderer.sortingOrder;
        }

        private float GetSinkDistanceToFullyHide(Vector2 direction)
        {
            if (blockerRenderer == null || blockerRenderer.sprite == null)
                return sinkDistanceTiles;

            // Move at least one sprite height along the vertical axis so every
            // pixel crosses the stationary sand mask before the collider opens.
            float verticalTravel = blockerRenderer.bounds.size.y /
                Mathf.Max(0.001f, Mathf.Abs(direction.y));
            return Mathf.Max(sinkDistanceTiles, verticalTravel);
        }

        private float GetSinkHorizontalShake(float progress)
        {
            // The envelope keeps the ship aligned with its blocker at the start
            // and end, while the middle of the sink has a brief side-to-side wobble.
            float envelope = Mathf.Sin(Mathf.PI * progress);
            float phase = progress * sinkHorizontalShakeCycles * Mathf.PI * 2f;
            return Mathf.Sin(phase) * sinkHorizontalShakeTiles * envelope;
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

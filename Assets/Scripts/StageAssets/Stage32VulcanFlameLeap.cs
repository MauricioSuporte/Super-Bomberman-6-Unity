using UnityEngine;

namespace StageAssets
{
    /// <summary>
    /// Emits a short trail of flame sprites between the two volcano tiles in
    /// Stage 3-2 room. Each flame follows the same parabolic leap shortly
    /// after the previous one, creating a continuous fire-worm trail.
    /// </summary>
    public sealed class Stage32VulcanFlameLeap : MonoBehaviour
    {
        private const int TrailCount = 4;
        private static readonly int[] FrameSequence = { 0, 1, 2, 1 };

        [Header("Volcano tiles (room local coordinates)")]
        [SerializeField] private Vector2 sourceTile = new(-3.5f, -2.5f);
        [SerializeField] private Vector2 destinationTile = new(-1.5f, -0.5f);
        [SerializeField, Min(0f)] private float arcHeightTiles = 3.5f;

        [Header("Timing")]
        [SerializeField, Min(0.01f)] private float minimumRepeatSeconds = 2f;
        [SerializeField, Min(0.01f)] private float maximumRepeatSeconds = 10f;
        [SerializeField, Min(0.01f)] private float burstDurationSeconds = 1.5f;
        private readonly float trailDelaySeconds = 0.1f;
        [SerializeField, Min(1f)] private float animationFramesPerSecond = 10f;
        [SerializeField, Range(0f, 1f)] private float reverseDirectionChance = 0.5f;

        [Header("Visual")]
        [SerializeField] private Sprite[] vulcanFlameSprites;
        [SerializeField] private string sortingLayerName = "Default";
        [SerializeField] private int sortingOrder = 4;
        [SerializeField, Min(1)] private int pixelsPerUnit = 16;

        [Header("Room activation")]
        [SerializeField] private Collider2D roomBounds;

        private SpriteRenderer[] trailRenderers;
        private float nextBurstAt;
        private float burstStartedAt;
        private bool burstActive;
        private bool reverseDirection;

        private void Awake()
        {
            CreateTrailRenderers();
            HideAll();
        }

        private void OnEnable()
        {
            nextBurstAt = Time.unscaledTime;
            burstActive = false;
            HideAll();
        }

        private void Update()
        {
            if (!HasValidSprites())
                return;

            float now = Time.unscaledTime;
            if (roomBounds != null && !World3RoomProgressionController.IsRoomOccupied(roomBounds))
            {
                StopForEmptyRoom(now);
                return;
            }

            if (!burstActive && now >= nextBurstAt)
                StartBurst(now);

            if (burstActive)
                UpdateBurst(now);
        }

        private void StartBurst(float now)
        {
            burstActive = true;
            burstStartedAt = now;
            reverseDirection = Random.value < Mathf.Clamp01(reverseDirectionChance);
            nextBurstAt = now + Random.Range(
                Mathf.Min(minimumRepeatSeconds, maximumRepeatSeconds),
                Mathf.Max(minimumRepeatSeconds, maximumRepeatSeconds));
        }

        private void UpdateBurst(float now)
        {
            float burstAge = now - burstStartedAt;
            float trailLifetime = Mathf.Max(0.01f,
                burstDurationSeconds - trailDelaySeconds * (TrailCount - 1));

            bool hasVisibleFlame = false;
            for (int i = 0; i < trailRenderers.Length; i++)
            {
                float age = burstAge - trailDelaySeconds * i;
                bool visible = age >= 0f && age < trailLifetime;
                trailRenderers[i].enabled = visible;
                if (!visible)
                    continue;

                hasVisibleFlame = true;
                UpdateFlame(trailRenderers[i], age, trailLifetime);
            }

            if (hasVisibleFlame)
                return;

            burstActive = false;
            HideAll();
        }

        private void StopForEmptyRoom(float now)
        {
            burstActive = false;
            nextBurstAt = now;
            HideAll();
        }

        private void UpdateFlame(SpriteRenderer flame, float age, float lifetime)
        {
            float normalizedAge = Mathf.Clamp01(age / lifetime);
            float arcProgress = reverseDirection ? 1f - normalizedAge : normalizedAge;
            Vector2 position = EvaluateCanonicalArc(arcProgress);

            position = SnapToPixelGrid(position);
            flame.transform.localPosition = new Vector3(position.x, position.y, -0.1f);
            int frame = FrameSequence[Mathf.FloorToInt(age * animationFramesPerSecond) % FrameSequence.Length];
            flame.sprite = vulcanFlameSprites[frame];
        }

        private Vector2 EvaluateCanonicalArc(float progress)
        {
            Vector2 position = Vector2.Lerp(sourceTile, destinationTile, progress);
            position.y += 4f * arcHeightTiles * progress * (1f - progress);
            return position;
        }

        private Vector2 SnapToPixelGrid(Vector2 position)
        {
            float unitsPerPixel = 1f / Mathf.Max(1, pixelsPerUnit);
            position.x = Mathf.Round(position.x / unitsPerPixel) * unitsPerPixel;
            position.y = Mathf.Round(position.y / unitsPerPixel) * unitsPerPixel;
            return position;
        }

        private void CreateTrailRenderers()
        {
            if (trailRenderers != null)
                return;

            trailRenderers = new SpriteRenderer[TrailCount];
            for (int i = 0; i < trailRenderers.Length; i++)
            {
                GameObject flame = new($"Vulcan Flame Trail {i + 1}");
                flame.transform.SetParent(transform, false);

                SpriteRenderer renderer = flame.AddComponent<SpriteRenderer>();
                renderer.sortingLayerName = sortingLayerName;
                renderer.sortingOrder = sortingOrder;
                trailRenderers[i] = renderer;
            }
        }

        private bool HasValidSprites() =>
            vulcanFlameSprites != null && vulcanFlameSprites.Length >= 3 &&
            vulcanFlameSprites[0] != null &&
            vulcanFlameSprites[1] != null &&
            vulcanFlameSprites[2] != null;

        private void HideAll()
        {
            if (trailRenderers == null)
                return;

            for (int i = 0; i < trailRenderers.Length; i++)
            {
                if (trailRenderers[i] != null)
                    trailRenderers[i].enabled = false;
            }
        }
    }
}

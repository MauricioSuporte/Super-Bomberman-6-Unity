using System.Collections.Generic;
using UnityEngine;

namespace StageAssets
{
    [DisallowMultipleComponent]
    public sealed class Stage33AmbientBubbleEffect : MonoBehaviour
    {
        private sealed class Bubble
        {
            public GameObject gameObject;
            public SpriteRenderer renderer;
            public Sprite[] animationSprites;
            public Vector2 position;
            public float horizontalSpeed;
            public float phase;
            public float animationTimer;
            public int animationFrame;
        }

        [Header("Timing")]
        [SerializeField, Min(1)] private int maximumActiveBubbles = 15;
        [SerializeField, Min(0.01f)] private float animationFramesPerSecond = 6f;

        [Header("Movement")]
        [SerializeField, Min(0f)] private float upwardSpeed = 1.1f;
        [SerializeField, Min(0f)] private float horizontalDriftSpeed = 0.24f;
        [SerializeField, Min(0f)] private float horizontalWobble = 0.09f;
        [SerializeField, Min(0f)] private float spawnEdgeMargin = 0.35f;
        [SerializeField] private string sortingLayerName = "Default";
        [SerializeField] private int sortingOrder = 50;

        private readonly List<Bubble> activeBubbles = new();
        private Stage33BubbleAnimationCatalog animationCatalog;
        private void Awake()
        {
            animationCatalog = Resources.Load<Stage33BubbleAnimationCatalog>("StageAssets/Stage33BubbleAnimations");
        }

        private void OnDisable()
        {
            ClearBubbles();
        }

        private void Update()
        {
            UpdateBubbles();

            int targetCount = Mathf.Max(1, maximumActiveBubbles);
            while (activeBubbles.Count < targetCount)
            {
                if (!SpawnBubble())
                    break;
            }
        }

        private void UpdateBubbles()
        {
            float deltaTime = Time.unscaledDeltaTime;
            for (int i = activeBubbles.Count - 1; i >= 0; i--)
            {
                Bubble bubble = activeBubbles[i];
                if (bubble.gameObject == null)
                {
                    activeBubbles.RemoveAt(i);
                    continue;
                }

                bubble.phase += deltaTime * 2.5f;
                bubble.position.x += bubble.horizontalSpeed * deltaTime;
                bubble.position.x += Mathf.Sin(bubble.phase) * horizontalWobble * deltaTime;
                bubble.position.y += upwardSpeed * deltaTime;
                bubble.gameObject.transform.position = SnapToPixelGrid(bubble.position);

                if (!AdvanceAnimation(bubble, deltaTime))
                    continue;

                Destroy(bubble.gameObject);
                activeBubbles.RemoveAt(i);
            }
        }

        private bool SpawnBubble()
        {
            Camera camera = Camera.main;
            Sprite[] sprites = GetRandomAnimation();
            if (camera == null || !camera.orthographic || sprites == null || sprites.Length == 0 || sprites[0] == null)
                return false;

            float halfHeight = camera.orthographicSize;
            float halfWidth = halfHeight * camera.aspect;
            float margin = Mathf.Max(0f, spawnEdgeMargin);
            Vector3 cameraWorldPosition = camera.transform.position;
            Vector2 cameraPosition = new(cameraWorldPosition.x, cameraWorldPosition.y);
            Vector2 position = new(
                Random.Range(cameraPosition.x - halfWidth + margin, cameraPosition.x + halfWidth - margin),
                Random.Range(cameraPosition.y - halfHeight + margin, cameraPosition.y + halfHeight - margin));

            GameObject bubbleObject = new("AmbientOceanBubble");
            bubbleObject.transform.position = SnapToPixelGrid(position);
            bubbleObject.transform.localScale = Vector3.one;

            SpriteRenderer renderer = bubbleObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprites[0];
            renderer.sortingLayerName = sortingLayerName;
            renderer.sortingOrder = sortingOrder;

            activeBubbles.Add(new Bubble
            {
                gameObject = bubbleObject,
                renderer = renderer,
                animationSprites = sprites,
                position = position,
                horizontalSpeed = Random.Range(-horizontalDriftSpeed, horizontalDriftSpeed),
                phase = Random.Range(0f, Mathf.PI * 2f)
            });

            return true;
        }

        private Sprite[] GetRandomAnimation()
        {
            Stage33BubbleAnimation[] animations = animationCatalog != null ? animationCatalog.animations : null;
            int count = 0;
            for (int i = 0; animations != null && i < animations.Length; i++)
            {
                if (animations[i]?.sprites?.Length > 0 && animations[i].sprites[0] != null)
                    count++;
            }

            if (count == 0)
                return null;

            int selected = Random.Range(0, count);
            for (int i = 0; i < animations.Length; i++)
            {
                Sprite[] sprites = animations[i]?.sprites;
                if (sprites == null || sprites.Length == 0 || sprites[0] == null)
                    continue;

                if (selected-- == 0)
                    return sprites;
            }

            return null;
        }

        private bool AdvanceAnimation(Bubble bubble, float deltaTime)
        {
            if (bubble.renderer == null || bubble.animationSprites == null || bubble.animationSprites.Length == 0)
                return true;

            bubble.animationTimer += deltaTime;
            float frameDuration = 1f / Mathf.Max(0.01f, animationFramesPerSecond);
            while (bubble.animationTimer >= frameDuration)
            {
                bubble.animationTimer -= frameDuration;
                bubble.animationFrame++;
                if (bubble.animationFrame >= bubble.animationSprites.Length)
                {
                    SetOpacity(bubble, 0f);
                    return true;
                }

                bubble.renderer.sprite = bubble.animationSprites[bubble.animationFrame];
                UpdateOpacity(bubble);
            }

            return false;
        }

        private static void UpdateOpacity(Bubble bubble)
        {
            int fadeStartFrame = bubble.animationSprites.Length / 2;
            float fadeProgress = Mathf.Clamp01(
                (bubble.animationFrame - fadeStartFrame) /
                (float)(bubble.animationSprites.Length - fadeStartFrame));

            SetOpacity(bubble, 1f - fadeProgress);
        }

        private static void SetOpacity(Bubble bubble, float opacity)
        {
            Color color = bubble.renderer.color;
            color.a = opacity;
            bubble.renderer.color = color;
        }

        private void ClearBubbles()
        {
            for (int i = 0; i < activeBubbles.Count; i++)
            {
                if (activeBubbles[i].gameObject != null)
                    Destroy(activeBubbles[i].gameObject);
            }

            activeBubbles.Clear();
        }

        private static Vector3 SnapToPixelGrid(Vector2 position)
        {
            const float pixelsPerUnit = 16f;
            return new Vector3(
                Mathf.Round(position.x * pixelsPerUnit) / pixelsPerUnit,
                Mathf.Round(position.y * pixelsPerUnit) / pixelsPerUnit,
                0f);
        }
    }
}

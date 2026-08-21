using System.Collections.Generic;
using UnityEngine;

namespace StageAssets
{
    [DisallowMultipleComponent]
    public sealed class Stage33DestructibleBubbleBurst : MonoBehaviour
    {
        private sealed class Bubble
        {
            public GameObject gameObject;
            public SpriteRenderer renderer;
            public Sprite[] animationSprites;
            public Vector3 position;
            public Vector2 velocity;
            public float animationTimer;
            public int animationFrame;
        }

        [Header("Sprites")]
        [SerializeField] private Sprite[] bubbleSprites;
        [SerializeField] private Stage33BubbleAnimationCatalog animationCatalog;
        [SerializeField, Min(0.01f)] private float animationFramesPerSecond = 12f;

        [Header("Burst")]
        [SerializeField, Min(1)] private int minimumBubbleCount = 3;
        [SerializeField, Min(1)] private int maximumBubbleCount = 8;
        [SerializeField, Min(0f)] private float throwSpeedMin = 2.2f;
        [SerializeField, Min(0f)] private float throwSpeedMax = 3.2f;
        [SerializeField, Range(1f, 89f)] private float throwAngleFromVertical = 40f;
        [SerializeField, Min(0f)] private float gravity = 1.8f;
        [SerializeField] private string sortingLayerName = "Default";
        [SerializeField] private int sortingOrder = 6;

        private readonly List<Bubble> activeBubbles = new();
        private GameManager gameManager;

        private void Awake()
        {
            gameManager = GetComponent<GameManager>();

            if (animationCatalog == null)
                animationCatalog = Resources.Load<Stage33BubbleAnimationCatalog>("StageAssets/Stage33BubbleAnimations");
        }

        private void OnEnable()
        {
            if (gameManager == null)
                gameManager = GetComponent<GameManager>();

            if (gameManager != null)
                gameManager.DestructibleDestroyed += SpawnBurst;
        }

        private void OnDisable()
        {
            if (gameManager != null)
                gameManager.DestructibleDestroyed -= SpawnBurst;

            ClearBubbles();
        }

        private void Update()
        {
            if (activeBubbles.Count == 0)
                return;

            float deltaTime = Time.deltaTime;

            for (int i = activeBubbles.Count - 1; i >= 0; i--)
            {
                Bubble bubble = activeBubbles[i];
                if (bubble.gameObject == null)
                {
                    activeBubbles.RemoveAt(i);
                    continue;
                }

                bubble.velocity += Vector2.down * gravity * deltaTime;
                bubble.position += (Vector3)(bubble.velocity * deltaTime);
                bubble.gameObject.transform.position = bubble.position;

                if (!AdvanceAnimation(bubble, deltaTime))
                    continue;

                Destroy(bubble.gameObject);
                activeBubbles.RemoveAt(i);
            }
        }

        private void SpawnBurst(Vector3Int cell)
        {
            if (!HasAvailableAnimation() || gameManager == null ||
                gameManager.destructibleTilemap == null)
                return;

            int min = Mathf.Max(1, minimumBubbleCount);
            int max = Mathf.Max(min, maximumBubbleCount);
            int count = Random.Range(min, max + 1);
            Vector3 origin = gameManager.destructibleTilemap.GetCellCenterWorld(cell);

            for (int i = 0; i < count; i++)
            {
                Sprite[] sprites = GetRandomAnimation();
                if (sprites == null || sprites.Length == 0 || sprites[0] == null)
                    continue;

                float angle = Random.Range(-throwAngleFromVertical, throwAngleFromVertical) * Mathf.Deg2Rad;
                float speed = Random.Range(Mathf.Min(throwSpeedMin, throwSpeedMax), Mathf.Max(throwSpeedMin, throwSpeedMax));
                Vector2 launchVelocity = new(Mathf.Sin(angle), Mathf.Cos(angle));
                launchVelocity *= speed;

                GameObject bubbleObject = new($"OceanBubble_{i + 1:00}");
                bubbleObject.transform.position = origin;
                // These source sprites are at most 8x8 pixels, so never apply a
                // serialized multiplier that could enlarge them in an open scene.
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
                    position = bubbleObject.transform.position,
                    velocity = launchVelocity
                });
            }
        }

        private Sprite[] GetRandomAnimation()
        {
            Stage33BubbleAnimation[] animations = animationCatalog != null ? animationCatalog.animations : null;
            int validAnimationCount = 0;
            for (int i = 0; animations != null && i < animations.Length; i++)
            {
                if (animations[i]?.sprites?.Length > 0 && animations[i].sprites[0] != null)
                    validAnimationCount++;
            }

            if (validAnimationCount > 0)
            {
                int selection = Random.Range(0, validAnimationCount);
                for (int i = 0; i < animations.Length; i++)
                {
                    Sprite[] sprites = animations[i]?.sprites;
                    if (sprites == null || sprites.Length == 0 || sprites[0] == null)
                        continue;

                    if (selection-- == 0)
                        return sprites;
                }
            }

            // Keeps the burst visible if the catalog is temporarily unavailable
            // while Unity is importing its asset.
            return bubbleSprites != null && bubbleSprites.Length > 0
                ? new[] { bubbleSprites[Random.Range(0, bubbleSprites.Length)] }
                : null;
        }

        private bool HasAvailableAnimation()
        {
            if (animationCatalog != null && animationCatalog.animations != null)
            {
                for (int i = 0; i < animationCatalog.animations.Length; i++)
                {
                    if (animationCatalog.animations[i]?.sprites?.Length > 0 &&
                        animationCatalog.animations[i].sprites[0] != null)
                        return true;
                }
            }

            return bubbleSprites != null && bubbleSprites.Length > 0;
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
                    return true;

                bubble.renderer.sprite = bubble.animationSprites[bubble.animationFrame];
            }

            return false;
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
    }
}

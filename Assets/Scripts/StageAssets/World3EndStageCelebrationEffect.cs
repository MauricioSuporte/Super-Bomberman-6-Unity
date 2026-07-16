using UnityEngine;

namespace StageAssets
{
    /// <summary>
    /// World 3's end-stage flourish: two translucent, expanding cyan triangles
    /// and a small burst of stars orbiting the World 3 Chip.
    /// The visuals are generated at runtime so the World 3 sequence needs no
    /// additional prefab or scene wiring.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class World3EndStageCelebrationEffect : MonoBehaviour
    {
        private const float Duration = 2.35f;
        private const float PixelsPerUnit = 16f;
        private const int StarCount = 10;
        private const float TriangleFinalScale = 1.9f;
        private const float TriangleRotationDegrees = 3000f;
        private const float TriangleSelfRotationDegrees = 2160f;

        private static Sprite triangleSprite;
        private static Sprite starSprite;

        private SpriteRenderer firstTriangle;
        private SpriteRenderer secondTriangle;
        private float elapsed;
        private int sortingLayerId;
        private int sortingOrder;

        public static void Play(Vector3 center, Transform sortingSource)
        {
            if (sortingSource == null)
                return;

            GameObject effectObject = new("World3EndStageCelebration");
            effectObject.transform.position = center;

            World3EndStageCelebrationEffect effect = effectObject.AddComponent<World3EndStageCelebrationEffect>();
            effect.Initialize(sortingSource);
        }

        private void Initialize(Transform source)
        {
            ResolveSorting(source);

            firstTriangle = CreateRenderer("TriangleA", TriangleSprite, sortingOrder);
            secondTriangle = CreateRenderer("TriangleB", TriangleSprite, sortingOrder + 1);
            secondTriangle.flipX = true;

            for (int i = 0; i < StarCount; i++)
                CreateStar(i);
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / Duration);

            AnimateTriangle(firstTriangle, progress, 1f, 18f, 1f);
            AnimateTriangle(secondTriangle, progress, -1f, -27f, 1f);

            if (progress >= 1f)
                Destroy(gameObject);
        }

        private void AnimateTriangle(
            SpriteRenderer triangle,
            float progress,
            float direction,
            float angleOffset,
            float sizeMultiplier)
        {
            if (triangle == null)
                return;

            float size = Mathf.Lerp(0.18f, TriangleFinalScale * sizeMultiplier, EaseOutCubic(progress));
            triangle.transform.localScale = Vector3.one * size;
            triangle.transform.localPosition = OrbitPosition(
                angleOffset + direction * progress * TriangleRotationDegrees,
                Mathf.Lerp(0.05f, 1.35f * sizeMultiplier, EaseOutCubic(progress)));
            triangle.transform.localRotation = Quaternion.Euler(
                0f,
                0f,
                direction * progress * TriangleSelfRotationDegrees);

            float alpha = Mathf.Lerp(0.62f, 0f, Mathf.Pow(progress, 1.6f));
            triangle.color = new Color(0.42f, 0.9f, 1f, alpha);
        }

        private void CreateStar(int index)
        {
            GameObject starObject = new($"Star_{index + 1:00}");
            starObject.transform.SetParent(transform, false);

            float angle = 360f * index / StarCount + 10f;
            float radius = 0.22f + (index % 5) * 0.12f + (index / 5) * 0.07f;
            starObject.transform.localPosition = OrbitPosition(angle, radius);

            SpriteRenderer star = starObject.AddComponent<SpriteRenderer>();
            star.sprite = StarSprite;
            star.sortingLayerID = sortingLayerId;
            star.sortingOrder = sortingOrder + 2;
            star.color = new Color(0.88f, 0.98f, 1f, 0f);

            World3EndStageCelebrationStar animation = starObject.AddComponent<World3EndStageCelebrationStar>();
            float startDelay = index * 0.075f;
            float radialGrowth = 0.62f + (index % 4) * 0.16f;
            float orbitDegrees = -(640f - radius * 240f);
            animation.Initialize(star, angle, radius, radialGrowth, orbitDegrees, startDelay, Duration - startDelay);
        }

        private static Vector3 OrbitPosition(float angleDegrees, float radius)
        {
            float radians = angleDegrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(radians) * radius, Mathf.Sin(radians) * radius, 0f);
        }

        private SpriteRenderer CreateRenderer(string objectName, Sprite sprite, int order)
        {
            GameObject child = new(objectName);
            child.transform.SetParent(transform, false);

            SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingLayerID = sortingLayerId;
            renderer.sortingOrder = order;
            return renderer;
        }

        private void ResolveSorting(Transform player)
        {
            sortingLayerId = 0;
            sortingOrder = 20;

            SpriteRenderer[] renderers = player.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                sortingLayerId = renderer.sortingLayerID;
                sortingOrder = Mathf.Max(sortingOrder, renderer.sortingOrder + 4);
            }
        }

        private static float EaseOutCubic(float value)
        {
            float inverse = 1f - Mathf.Clamp01(value);
            return 1f - inverse * inverse * inverse;
        }

        private static Sprite TriangleSprite => triangleSprite ??= CreateTriangleSprite();
        private static Sprite StarSprite => starSprite ??= CreateStarSprite();

        private static Sprite CreateTriangleSprite()
        {
            const int size = 32;
            Texture2D texture = new(size, size, TextureFormat.RGBA32, mipChain: false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "World3EndStageTriangle_Runtime"
            };

            for (int y = 0; y < size; y++)
            {
                float normalizedY = y / (float)(size - 1);
                float halfWidth = normalizedY * 0.5f;
                for (int x = 0; x < size; x++)
                {
                    float normalizedX = x / (float)(size - 1);
                    bool inside = Mathf.Abs(normalizedX - 0.5f) <= halfWidth;
                    texture.SetPixel(x, y, inside ? Color.white : Color.clear);
                }
            }

            texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.33f), PixelsPerUnit);
        }

        private static Sprite CreateStarSprite()
        {
            const int size = 16;
            Texture2D texture = new(size, size, TextureFormat.RGBA32, mipChain: false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "World3EndStageStar_Runtime"
            };

            Vector2 center = new((size - 1) * 0.5f, (size - 1) * 0.5f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 point = new Vector2(x, y) - center;
                    bool isStar = Mathf.Abs(point.x) <= 1f || Mathf.Abs(point.y) <= 1f ||
                                  (Mathf.Abs(point.x) <= 3f && Mathf.Abs(point.y) <= 3f);
                    texture.SetPixel(x, y, isStar ? Color.white : Color.clear);
                }
            }

            texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), PixelsPerUnit);
        }
    }

    [DisallowMultipleComponent]
    public sealed class World3EndStageCelebrationStar : MonoBehaviour
    {
        private SpriteRenderer spriteRenderer;
        private float startingAngle;
        private float startingRadius;
        private float radialGrowth;
        private float orbitDegrees;
        private float startDelay;
        private float duration;
        private float elapsed;

        public void Initialize(
            SpriteRenderer renderer,
            float angle,
            float radius,
            float growth,
            float rotationDegrees,
            float delay,
            float lifetime)
        {
            spriteRenderer = renderer;
            startingAngle = angle;
            startingRadius = radius;
            radialGrowth = growth;
            orbitDegrees = rotationDegrees;
            startDelay = delay;
            duration = Mathf.Max(0.01f, lifetime);
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            if (elapsed < startDelay)
                return;

            float progress = Mathf.Clamp01((elapsed - startDelay) / duration);
            float angle = (startingAngle + orbitDegrees * progress) * Mathf.Deg2Rad;
            float radius = Mathf.Lerp(startingRadius, startingRadius + radialGrowth, progress);
            transform.localPosition = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one * Mathf.Lerp(0.62f, 0.9f, progress);

            if (spriteRenderer != null)
                spriteRenderer.color = new Color(0.9f, 0.99f, 1f, Mathf.Clamp01((1f - progress) * 5f) * 0.95f);

            if (progress >= 1f)
                Destroy(gameObject);
        }
    }
}

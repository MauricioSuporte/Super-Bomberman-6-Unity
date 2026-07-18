using UnityEngine;

public sealed class FrogWaterJumpEffect : MonoBehaviour
{
    public enum EffectType
    {
        ExitRipple,
        EntrySplash,
    }

    private const float Lifetime = 0.65f;

    private static Sprite ringSprite;
    private static Sprite dropletSprite;

    private EffectType effectType;
    private float elapsed;
    private SpriteRenderer firstRing;
    private SpriteRenderer secondRing;
    private SpriteRenderer[] droplets;

    public void Initialize(EffectType type, int sortingLayerId, int sortingOrder)
    {
        effectType = type;

        firstRing = CreateRenderer("Ring", GetRingSprite(), sortingLayerId, sortingOrder);
        secondRing = CreateRenderer("RingSecondary", GetRingSprite(), sortingLayerId, sortingOrder);

        if (effectType == EffectType.EntrySplash)
        {
            droplets = new SpriteRenderer[3];
            for (int i = 0; i < droplets.Length; i++)
                droplets[i] = CreateRenderer($"Droplet_{i}", GetDropletSprite(), sortingLayerId, sortingOrder + 1);
        }
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float progress = Mathf.Clamp01(elapsed / Lifetime);

        if (effectType == EffectType.ExitRipple)
            UpdateExitRipple(progress);
        else
            UpdateEntrySplash(progress);

        if (progress >= 1f)
            Destroy(gameObject);
    }

    private void UpdateExitRipple(float progress)
    {
        UpdateRing(firstRing, progress, 0f, 0.24f, 1.05f);
        UpdateRing(secondRing, progress, 0.16f, 0.16f, 0.85f);
    }

    private void UpdateEntrySplash(float progress)
    {
        UpdateRing(firstRing, progress, 0f, 0.28f, 1.25f);
        UpdateRing(secondRing, progress, 0.1f, 0.16f, 0.95f);

        if (droplets == null)
            return;

        float splashProgress = Mathf.Clamp01(progress / 0.6f);
        float height = Mathf.Sin(splashProgress * Mathf.PI) * 0.42f;
        float alpha = 1f - splashProgress;

        for (int i = 0; i < droplets.Length; i++)
        {
            SpriteRenderer droplet = droplets[i];
            if (droplet == null)
                continue;

            float horizontal = (i - 1) * 0.2f * splashProgress;
            droplet.transform.localPosition = new Vector3(horizontal, height * (i == 1 ? 1f : 0.72f), 0f);
            droplet.transform.localScale = Vector3.one * Mathf.Lerp(0.24f, 0.1f, splashProgress);
            droplet.color = new Color(0.76f, 0.96f, 1f, alpha);
        }
    }

    private static void UpdateRing(SpriteRenderer ring, float progress, float delay, float startScale, float endScale)
    {
        if (ring == null)
            return;

        float localProgress = Mathf.Clamp01((progress - delay) / (1f - delay));
        ring.transform.localScale = new Vector3(
            Mathf.Lerp(startScale, endScale, localProgress),
            Mathf.Lerp(startScale * 0.48f, endScale * 0.48f, localProgress),
            1f);
        ring.color = new Color(0.76f, 0.96f, 1f, (1f - localProgress) * 0.9f);
    }

    private SpriteRenderer CreateRenderer(string objectName, Sprite sprite, int sortingLayerId, int sortingOrder)
    {
        GameObject child = new(objectName);
        child.transform.SetParent(transform, false);

        SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingLayerID = sortingLayerId;
        renderer.sortingOrder = sortingOrder;
        return renderer;
    }

    private static Sprite GetRingSprite()
    {
        if (ringSprite != null)
            return ringSprite;

        const int size = 32;
        Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = "FrogWaterRipple"
        };

        Vector2 center = new(15.5f, 15.5f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 point = new((x - center.x) / 15.5f, (y - center.y) / 15.5f);
                float distance = point.magnitude;
                texture.SetPixel(x, y, distance >= 0.72f && distance <= 0.9f ? Color.white : Color.clear);
            }
        }

        texture.Apply();
        ringSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 16f);
        ringSprite.name = "FrogWaterRippleSprite";
        return ringSprite;
    }

    private static Sprite GetDropletSprite()
    {
        if (dropletSprite != null)
            return dropletSprite;

        Texture2D texture = new(4, 4, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            name = "FrogWaterDroplet"
        };

        for (int y = 0; y < 4; y++)
        {
            for (int x = 0; x < 4; x++)
                texture.SetPixel(x, y, Color.white);
        }

        texture.Apply();
        dropletSprite = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 16f);
        dropletSprite.name = "FrogWaterDropletSprite";
        return dropletSprite;
    }
}

using UnityEngine;

public class WorldMapPassingStars : MonoBehaviour
{
    [SerializeField] PixelPerfectScrollingSpriteRenderer backgroundScroller;
    [SerializeField] Sprite[] starSprites;
    [SerializeField, Min(0f)] float speedMultiplier = 1.5f;
    [SerializeField] Vector2 spawnIntervalSeconds = new Vector2(3f, 7f);
    [SerializeField] Vector2 referenceResolution = new Vector2(256f, 224f);
    [SerializeField, Min(1f)] float pixelsPerUnit = 16f;
    [SerializeField] int sortingOrder = -5;

    const int PoolSize = 12;
    readonly SpriteRenderer[] stars = new SpriteRenderer[PoolSize];
    readonly Vector2[] positions = new Vector2[PoolSize];
    float spawnTimer;

    void OnEnable()
    {
        spawnTimer = NextSpawnDelay();
    }

    void OnDisable()
    {
        foreach (var star in stars)
            if (star != null)
                star.gameObject.SetActive(false);
    }

    void Update()
    {
        if (backgroundScroller == null || !backgroundScroller.isActiveAndEnabled ||
            starSprites == null || starSprites.Length == 0)
            return;

        float speed = Mathf.Abs(backgroundScroller.ScrollSpeedUnitsPerSecond) * speedMultiplier;
        if (speed <= 0f)
            return;

        float deltaTime = Time.unscaledDeltaTime;
        float rightEdge = referenceResolution.x / (2f * pixelsPerUnit);
        for (int i = 0; i < stars.Length; i++)
        {
            var star = stars[i];
            if (star == null || !star.gameObject.activeSelf)
                continue;

            positions[i].x += speed * deltaTime;
            if (positions[i].x > rightEdge + star.sprite.bounds.extents.x)
            {
                star.gameObject.SetActive(false);
                continue;
            }

            ApplyPosition(i);
        }

        spawnTimer -= deltaTime;
        if (spawnTimer <= 0f)
        {
            SpawnStar();
            spawnTimer = NextSpawnDelay();
        }
    }

    float NextSpawnDelay()
    {
        float minimum = Mathf.Max(0.1f, spawnIntervalSeconds.x);
        return Random.Range(minimum, Mathf.Max(minimum, spawnIntervalSeconds.y));
    }

    void SpawnStar()
    {
        Sprite sprite = starSprites[Random.Range(0, starSprites.Length)];
        if (sprite == null)
            return;

        for (int i = 0; i < stars.Length; i++)
        {
            if (stars[i] != null && stars[i].gameObject.activeSelf)
                continue;

            if (stars[i] == null)
            {
                var starObject = new GameObject("PassingStar");
                starObject.transform.SetParent(transform, false);
                stars[i] = starObject.AddComponent<SpriteRenderer>();
                var background = backgroundScroller.GetComponent<SpriteRenderer>();
                stars[i].sharedMaterial = background.sharedMaterial;
                stars[i].sortingLayerID = background.sortingLayerID;
            }

            stars[i].sprite = sprite;
            stars[i].sortingOrder = sortingOrder;
            float halfHeight = referenceResolution.y / (2f * pixelsPerUnit) - sprite.bounds.extents.y;
            positions[i] = new Vector2(
                -referenceResolution.x / (2f * pixelsPerUnit) - sprite.bounds.extents.x,
                Random.Range(-halfHeight, halfHeight));
            ApplyPosition(i);
            stars[i].gameObject.SetActive(true);
            return;
        }
    }

    void ApplyPosition(int index)
    {
        Vector2 position = positions[index];
        stars[index].transform.localPosition = new Vector3(
            Mathf.Round(position.x * pixelsPerUnit) / pixelsPerUnit,
            Mathf.Round(position.y * pixelsPerUnit) / pixelsPerUnit, 0f);
    }
}

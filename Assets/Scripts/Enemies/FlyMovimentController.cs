using UnityEngine;

public sealed class FlyMovimentController : JunctionTurningGhostEnemyMovementController
{
    private static Sprite shadowSprite;

    [Header("Flight Visuals")]
    [SerializeField, Min(0f)] private float hoverBaseHeight = 0.45f;
    [SerializeField, Min(0f)] private float hoverAmplitude = 0.08f;
    [SerializeField, Min(0.01f)] private float hoverFrequency = 3f;

    [Header("Flight Shadow")]
    [SerializeField] private Color shadowColor = new(0f, 0f, 0f, 0.45f);
    [SerializeField] private Vector2 shadowScale = new(0.9f, 0.9f);

    private GameObject shadow;

    protected override void Start()
    {
        base.Start();
        CreateShadow();
    }

    private void LateUpdate()
    {
        if (isDead || isInDamagedLoop)
        {
            ClearHoverOffset();
            return;
        }

        UpdateShadowPosition();
        ApplyHoverOffset();
    }

    protected override void Die()
    {
        DestroyShadow();
        ClearHoverOffset();
        base.Die();
    }

    protected override void OnDestroy()
    {
        DestroyShadow();
        base.OnDestroy();
    }

    private void ApplyHoverOffset()
    {
        foreach (AnimatedSpriteRenderer animatedSprite in GetComponentsInChildren<AnimatedSpriteRenderer>(true))
        {
            if (animatedSprite == null || animatedSprite == activeSprite)
                continue;

            animatedSprite.ClearExternalBase();
        }

        if (activeSprite == null)
            return;

        float hoverHeight = hoverBaseHeight + Mathf.Sin(Time.time * hoverFrequency) * hoverAmplitude;
        activeSprite.SetExternalBaseOffsetFromInitial(Vector3.up * hoverHeight);
    }

    private void ClearHoverOffset()
    {
        foreach (AnimatedSpriteRenderer animatedSprite in GetComponentsInChildren<AnimatedSpriteRenderer>(true))
        {
            if (animatedSprite != null)
                animatedSprite.ClearExternalBase();
        }
    }

    private void CreateShadow()
    {
        if (shadow != null)
            return;

        shadow = new GameObject("FlyShadow");
        shadow.transform.localScale = new Vector3(shadowScale.x, shadowScale.y, 1f);

        SpriteRenderer shadowRenderer = shadow.AddComponent<SpriteRenderer>();
        shadowRenderer.sprite = GetShadowSprite();
        shadowRenderer.color = shadowColor;
        shadowRenderer.sortingOrder = 4;

        AnimatedSpriteRenderer visual = spriteDown != null ? spriteDown : activeSprite;
        if (visual != null && visual.TryGetComponent(out SpriteRenderer visualRenderer))
        {
            shadowRenderer.sortingLayerID = visualRenderer.sortingLayerID;
            shadowRenderer.sortingOrder = visualRenderer.sortingOrder - 1;
        }

        UpdateShadowPosition();
    }

    private void UpdateShadowPosition()
    {
        if (shadow == null)
            return;

        Vector3 position = transform.position;
        shadow.transform.position = new Vector3(position.x, position.y, 0f);
    }

    private void DestroyShadow()
    {
        if (shadow != null)
            Destroy(shadow);

        shadow = null;
    }

    private static Sprite GetShadowSprite()
    {
        if (shadowSprite != null)
            return shadowSprite;

        Texture2D texture = new(16, 16, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            name = "FlyShadow"
        };

        Vector2 center = new(7.5f, 7.5f);
        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                Vector2 point = new((x - center.x) / 7.5f, (y - center.y) / 4.5f);
                texture.SetPixel(x, y, point.sqrMagnitude <= 1f ? Color.white : Color.clear);
            }
        }

        texture.Apply();
        shadowSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, 16f, 16f),
            new Vector2(0.5f, 0.5f),
            16f,
            0,
            SpriteMeshType.FullRect);
        shadowSprite.name = "FlyShadowSprite";
        return shadowSprite;
    }
}

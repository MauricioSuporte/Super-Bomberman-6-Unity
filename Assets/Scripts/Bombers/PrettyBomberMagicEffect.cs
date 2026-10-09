using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PrettyBomberMagicEffect : MonoBehaviour
{
    private const int Width = 32;
    private const int Height = 12;
    private readonly Color32[] pixels = new Color32[Width * Height];
    private Texture2D texture;
    private Sprite sprite;
    private SpriteRenderer visual;
    private Texture2D maskTexture;
    private Sprite maskSprite;
    private float pixelsPerUnit;
    private float expansion;
    private float elapsed;
    private int phase = -1;

    public static PrettyBomberMagicEffect Create(Vector3 position, float tileSize, SpriteRenderer reference)
    {
        var root = new GameObject("PrettyBomber Pixel Magic Ellipse");
        root.transform.position = position;
        var effect = root.AddComponent<PrettyBomberMagicEffect>();
        effect.pixelsPerUnit = 16f / Mathf.Max(0.0001f, tileSize);
        effect.texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        effect.sprite = Sprite.Create(effect.texture, new Rect(0, 0, Width, Height),
            new Vector2(0.5f, 0.5f), effect.pixelsPerUnit, 0, SpriteMeshType.FullRect);
        effect.visual = root.AddComponent<SpriteRenderer>();
        effect.visual.sprite = effect.sprite;
        effect.visual.sortingLayerID = reference.sortingLayerID;
        effect.visual.sortingOrder = reference.sortingOrder - 1;
        effect.visual.color = Color.white;
        effect.SetExpansion(0f);
        return effect;
    }

    public void SetExpansion(float value)
    {
        expansion = Mathf.Clamp01(value);
        DrawPixels();
    }

    public IEnumerator Grow(float seconds)
    {
        float time = 0f;
        while (time < seconds)
        {
            SetExpansion(time / seconds);
            yield return null;
            if (!GamePauseController.IsPaused)
                time += Time.deltaTime;
        }
        SetExpansion(1f);
    }

    public IEnumerator Close(float seconds)
    {
        float time = 0f;
        while (time < seconds)
        {
            SetExpansion(1f - time / seconds);
            yield return null;
            if (!GamePauseController.IsPaused)
                time += Time.deltaTime;
        }
        SetExpansion(0f);
        visual.enabled = false;
    }

    private void Update()
    {
        if (GamePauseController.IsPaused)
            return;
        elapsed += Time.deltaTime;
        int nextPhase = Mathf.FloorToInt(elapsed * 16f);
        if (nextPhase == phase)
            return;
        phase = nextPhase;
        DrawPixels();
    }

    private void DrawPixels()
    {
        int width = Mathf.RoundToInt(Mathf.Lerp(4f, Width, expansion));
        int height = Mathf.Max(2, Mathf.RoundToInt(width * Height / (float)Width));
        float radiusX = width * 0.5f;
        float radiusY = height * 0.5f;
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                float nx = (x + 0.5f - Width * 0.5f) / radiusX;
                float ny = (y + 0.5f - Height * 0.5f) / radiusY;
                float distance = nx * nx + ny * ny;
                Color32 color = new(0, 0, 0, 0);
                if (distance <= 1f)
                {
                    int wave = Mathf.RoundToInt(Mathf.Sin((x + phase) * 0.5f));
                    int stripe = ((y - Mathf.Max(0, phase) + wave) % 8 + 8) % 8;
                    color = stripe < 2 ? new Color32(80, 220, 255, 255) :
                        stripe < 4 ? new Color32(95, 135, 255, 255) :
                        stripe < 6 ? new Color32(255, 105, 210, 255) :
                        new Color32(255, 190, 240, 255);
                    if (distance > 0.7f)
                        color = stripe < 4 ? new Color32(145, 240, 255, 255) :
                            new Color32(255, 160, 230, 255);
                }
                pixels[y * Width + x] = color;
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, false);
    }

    public IEnumerator Reveal(GameObject boss, MovementController movement, bool emerging, float seconds)
    {
        Vector3 origin = boss.transform.position;
        var renderers = boss.GetComponentsInChildren<SpriteRenderer>(true);
        var interactions = new SpriteMaskInteraction[renderers.Length];
        float floorOffset = 0f;
        float topOffset = 0f;
        for (int i = 0; i < renderers.Length; i++)
        {
            interactions[i] = renderers[i].maskInteraction;
            if (renderers[i].enabled)
            {
                floorOffset = Mathf.Min(floorOffset, renderers[i].bounds.min.y - origin.y);
                topOffset = Mathf.Max(topOffset, renderers[i].bounds.max.y - origin.y);
            }
            renderers[i].maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
        }

        maskTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        maskTexture.SetPixel(0, 0, Color.white);
        maskTexture.Apply();
        maskSprite = Sprite.Create(maskTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0f), 1f);
        var maskObject = new GameObject("Pixel Portal Floor Mask");
        maskObject.transform.SetParent(transform, false);
        maskObject.transform.position = origin + Vector3.up * floorOffset;
        maskObject.transform.localScale = new Vector3(4f, 6f, 1f);
        maskObject.AddComponent<SpriteMask>().sprite = maskSprite;

        float depth = Mathf.Ceil((topOffset - floorOffset + 0.125f) * pixelsPerUnit) / pixelsPerUnit;
        float time = 0f;
        try
        {
            while (time < seconds)
            {
                float t = Mathf.Clamp01(time / seconds);
                SetExpansion(emerging ? 1f : 1f - t);
                float offset = depth * (emerging ? 1f - t : t);
                offset = Mathf.Round(offset * pixelsPerUnit) / pixelsPerUnit;
                Vector3 position = origin + Vector3.down * offset;
                boss.transform.position = position;
                movement.Rigidbody.position = position;
                movement.Rigidbody.linearVelocity = Vector2.zero;
                yield return null;
                if (!GamePauseController.IsPaused)
                    time += Time.deltaTime;
            }
            SetExpansion(emerging ? 1f : 0f);
            Vector3 finalPosition = emerging ? origin : origin + Vector3.down * depth;
            boss.transform.position = finalPosition;
            movement.Rigidbody.position = finalPosition;
        }
        finally
        {
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null)
                    renderers[i].maskInteraction = interactions[i];
            Destroy(maskObject);
        }
    }

    private void OnDestroy()
    {
        if (sprite != null) Destroy(sprite);
        if (texture != null) Destroy(texture);
        if (maskSprite != null) Destroy(maskSprite);
        if (maskTexture != null) Destroy(maskTexture);
    }
}

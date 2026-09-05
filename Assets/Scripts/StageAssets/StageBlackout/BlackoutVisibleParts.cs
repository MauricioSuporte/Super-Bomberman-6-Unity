using System;
using UnityEngine;

/// <summary>Redraws selected sprite pixels above a world blackout, following animation and flips.</summary>
[DefaultExecutionOrder(1000)]
[DisallowMultipleComponent]
public sealed class BlackoutVisibleParts : MonoBehaviour
{
    [Serializable]
    private sealed class Part
    {
        public SpriteRenderer source;
        [Tooltip("Normalized sprite rectangle: (0,0) bottom-left, (1,1) top-right. Follows flips.")]
        public Rect region = new(0f, 0f, 1f, 1f);
        [Tooltip("Optional prefab palette. When assigned, its colors and tolerance replace the inline settings below.")]
        public BlackoutColorPalette palette;
        [Tooltip("These animation frames bypass the palette and reveal every opaque pixel inside the region.")]
        public Sprite[] fullyVisibleSprites = Array.Empty<Sprite>();
        [Tooltip("Empty selects every color in the region. Otherwise only these colors remain visible (up to 8).")]
        public Color[] colors = Array.Empty<Color>();
        [Range(0f, 0.25f)] public float colorTolerance = 0.01f;
        [NonSerialized] public SpriteRenderer overlay;
        [NonSerialized] public Material material;
        public bool IsFullSprite => source != null && source.sprite != null &&
            fullyVisibleSprites != null && Array.IndexOf(fullyVisibleSprites, source.sprite) >= 0;
        public bool CanReveal => IsFullSprite || palette == null || palette.CanReveal;
        public Color[] EffectiveColors => IsFullSprite ? Array.Empty<Color>() :
            palette != null ? palette.VisibleColors : colors;
        public float EffectiveTolerance => palette != null ? palette.ColorTolerance : colorTolerance;
    }

    [SerializeField] private Material maskMaterial;
    [SerializeField] private Part[] parts = Array.Empty<Part>();
    private readonly Vector4[] colorBuffer = new Vector4[8];

    private void LateUpdate()
    {
        WorldBlackoutRenderer darkness = StageBlackout.Instance != null
            ? StageBlackout.Instance.ActiveWorldOverlay : null;
        foreach (Part part in parts)
        {
            if (part == null) continue;
            SpriteRenderer source = part.source;
            bool show = darkness != null && darkness.IsVisible && source != null &&
                source.enabled && source.gameObject.activeInHierarchy && source.sprite != null &&
                part.region.width > 0f && part.region.height > 0f &&
                part.CanReveal &&
                source.bounds.Intersects(darkness.RoomWorldBounds);
            if (!show || maskMaterial == null)
            {
                if (part.overlay != null) part.overlay.enabled = false;
                continue;
            }
            if (part.overlay == null)
            {
                // Keep outside the source hierarchy so enemy animation/tint scans don't include the copy.
                var copy = new GameObject(source.name + " BlackoutVisibleParts");
                copy.layer = source.gameObject.layer;
                part.overlay = copy.AddComponent<SpriteRenderer>();
                part.material = new Material(maskMaterial);
                part.overlay.sharedMaterial = part.material;
            }
            SpriteRenderer overlay = part.overlay;
            overlay.enabled = true;
            overlay.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            overlay.transform.localScale = source.transform.lossyScale;
            overlay.sprite = source.sprite;
            overlay.flipX = source.flipX;
            overlay.flipY = source.flipY;
            overlay.color = source.color;
            overlay.sortingLayerName = darkness.SortingLayerName;
            overlay.sortingOrder = Mathf.Clamp(darkness.SortingOrder + 1 + Mathf.Max(0, source.sortingOrder), -32768, 32767);
            overlay.maskInteraction = source.maskInteraction;

            Bounds bounds = source.sprite.bounds;
            part.material.SetVector("_SpriteBounds", new Vector4(bounds.min.x, bounds.min.y, bounds.size.x, bounds.size.y));
            Rect region = part.region;
            part.material.SetVector("_VisibleRegion", new Vector4(region.xMin, region.yMin, region.xMax, region.yMax));
            Bounds room = darkness.RoomWorldBounds;
            part.material.SetVector("_RoomBounds", new Vector4(room.min.x, room.min.y, room.max.x, room.max.y));
            Color[] selectedColors = part.EffectiveColors;
            int colorCount = Mathf.Min(selectedColors != null ? selectedColors.Length : 0, colorBuffer.Length);
            for (int i = 0; i < colorCount; i++)
            {
                Color color = QualitySettings.activeColorSpace == ColorSpace.Linear ? selectedColors[i].linear : selectedColors[i];
                colorBuffer[i] = color;
            }
            part.material.SetInt("_VisibleColorCount", colorCount);
            part.material.SetVectorArray("_VisibleColors", colorBuffer);
            part.material.SetFloat("_ColorTolerance", part.EffectiveTolerance);
        }
    }

    private void OnDisable()
    {
        foreach (Part part in parts)
            if (part?.overlay != null) part.overlay.enabled = false;
    }

    private void OnDestroy()
    {
        foreach (Part part in parts)
        {
            if (part == null) continue;
            if (part.overlay != null) Destroy(part.overlay.gameObject);
            if (part.material != null) Destroy(part.material);
        }
    }
}

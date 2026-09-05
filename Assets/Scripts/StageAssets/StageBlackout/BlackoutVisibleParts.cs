using System;
using System.Collections.Generic;
using System.Text;
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
        [NonSerialized] public string lastDiagnosticState;
        [NonSerialized] public HashSet<Sprite> loggedSprites;
        public bool IsFullSprite => source != null && source.sprite != null &&
            fullyVisibleSprites != null && Array.IndexOf(fullyVisibleSprites, source.sprite) >= 0;
        public bool CanReveal => IsFullSprite || palette == null || palette.CanReveal;
        public Color[] EffectiveColors => IsFullSprite ? Array.Empty<Color>() :
            palette != null ? palette.VisibleColors : colors;
        public float EffectiveTolerance => palette != null ? palette.ColorTolerance : colorTolerance;
    }

    [SerializeField] private Material maskMaterial;
    [SerializeField] private Part[] parts = Array.Empty<Part>();
    [Header("Diagnostics")]
    [SerializeField] private bool logDiagnostics;
    private readonly Vector4[] colorBuffer = new Vector4[8];

    private void OnEnable()
    {
        if (!logDiagnostics) return;
        foreach (Part part in parts)
        {
            if (part == null) continue;
            part.lastDiagnosticState = null;
            part.loggedSprites?.Clear();
        }
        Debug.Log($"[BlackoutVisibleParts] ENABLE {transform.root.name}/{name}: parts={parts.Length}, " +
            $"maskMaterial={maskMaterial}, colorSpace={QualitySettings.activeColorSpace}. " +
            "Logs report CPU setup, not confirmation that the GPU rendered matching pixels.", this);
    }

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
            if (logDiagnostics) LogState(part, darkness);
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
            if (logDiagnostics) LogSprite(part, darkness, colorCount);
        }
    }

    private void LogState(Part part, WorldBlackoutRenderer darkness)
    {
        SpriteRenderer source = part.source;
        string state = StageBlackout.Instance == null ? "BLOCKED: StageBlackout.Instance missing" :
            darkness == null ? "BLOCKED: world blackout inactive/missing" :
            !darkness.IsVisible ? "BLOCKED: world overlay not visible" :
            source == null ? "BLOCKED: source renderer missing" :
            !source.gameObject.activeInHierarchy ? "BLOCKED: source GameObject inactive" :
            !source.enabled ? "BLOCKED: source renderer disabled" :
            source.sprite == null ? "BLOCKED: source sprite missing" :
            !source.bounds.Intersects(darkness.RoomWorldBounds) ? "BLOCKED: source outside room bounds" :
            part.region.width <= 0f || part.region.height <= 0f ? "BLOCKED: empty region; shader would discard all pixels. Check the serialized Rect width/height" :
            !part.CanReveal ? "BLOCKED: prefab palette disabled or empty" :
            maskMaterial == null ? "BLOCKED: mask material missing" : "READY: submitting selected pixels above blackout";
        if (state == part.lastDiagnosticState) return;
        part.lastDiagnosticState = state;
        Debug.Log($"[BlackoutVisibleParts] {transform.root.name}/{name}: {state}; " +
            $"source={source}, region={part.region}, sourceBounds={(source != null ? source.bounds.ToString() : "missing")}, " +
            $"roomBounds={(darkness != null ? darkness.RoomWorldBounds.ToString() : "missing")}", this);
    }

    private void LogSprite(Part part, WorldBlackoutRenderer darkness, int colorCount)
    {
        Sprite sprite = part.source.sprite;
        part.loggedSprites ??= new HashSet<Sprite>();
        if (!part.loggedSprites.Add(sprite)) return;
        Texture2D texture = sprite.texture;
        var message = new StringBuilder();
        message.AppendLine($"[BlackoutVisibleParts] SPRITE {transform.root.name}/{name}: {sprite.name}");
        message.AppendLine($"texture={texture.name}, format={texture.graphicsFormat}, filter={texture.filterMode}, " +
            $"readable={texture.isReadable}, packed={sprite.packed}, spriteRect={sprite.rect}, region={part.region}");
        message.AppendLine($"shaderVisibleRegion={part.material.GetVector("_VisibleRegion").ToString("F6")}, " +
            $"shaderSpriteBounds={part.material.GetVector("_SpriteBounds").ToString("F6")}");
        message.AppendLine($"shader={part.material.shader.name}, supported={part.material.shader.isSupported}, " +
            $"passes={part.material.passCount}, queue={part.material.renderQueue}, " +
            $"overlayOrder={part.overlay.sortingLayerName}/{part.overlay.sortingOrder}, blackoutOrder={darkness.SortingLayerName}/{darkness.SortingOrder}, " +
            $"tint={part.overlay.color}, layer={part.overlay.gameObject.layer}, maskInteraction={part.overlay.maskInteraction}");
        Camera camera = Camera.main;
        message.AppendLine($"camera={camera}, cameraIncludesLayer={(camera != null && (camera.cullingMask & (1 << part.overlay.gameObject.layer)) != 0)}, " +
            $"colorSpace={QualitySettings.activeColorSpace}, palette={part.palette}, fullSpriteOverride={part.IsFullSprite}, colorsUploaded={colorCount}, tolerance={part.EffectiveTolerance:F6}");
        for (int i = 0; i < colorCount; i++)
            message.AppendLine($"color[{i}]=#{ColorUtility.ToHtmlStringRGB(part.EffectiveColors[i])}, shaderRGB={colorBuffer[i].ToString("F6")}");
#if UNITY_EDITOR
        string texturePath = UnityEditor.AssetDatabase.GetAssetPath(texture);
        var importer = UnityEditor.AssetImporter.GetAtPath(texturePath) as UnityEditor.TextureImporter;
        if (importer != null)
            message.AppendLine($"importer={texturePath}, sRGB={importer.sRGBTexture}, compression={importer.textureCompression}. " +
                "Compression or an unexpected color space can prevent palette matches at this tolerance.");
#endif
        Debug.Log(message.ToString(), this);
    }

    private void OnDisable()
    {
        if (logDiagnostics)
            Debug.Log($"[BlackoutVisibleParts] DISABLE {transform.root.name}/{name}", this);
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

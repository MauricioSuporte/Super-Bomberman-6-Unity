using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Applies the World 3 water treatment to the player's animated body sprites.
/// The surface is kept in player-local space, so it follows the player while
/// the lower body remains visibly underwater.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerWaterSubmersionEffect : MonoBehaviour
{
    private const string ShaderName = "SuperBomberman/PlayerWaterSubmersion";

    private const float DefaultSurfaceLineHeight = 0.125f;

    private static readonly int WaterSurfaceY = Shader.PropertyToID("_WaterSurfaceY");
    private static readonly int SurfaceLineHeight = Shader.PropertyToID("_SurfaceLineHeight");

    private readonly List<SpriteRenderer> bodyRenderers = new();
    private readonly Dictionary<SpriteRenderer, Material> originalMaterials = new();
    private MaterialPropertyBlock propertyBlock;

    private Material waterMaterial;
    private bool targetTileSuppressed;
    private bool mountedPlayerSuppressed;

    /// <summary>
    /// Temporarily restores the original player materials, for example while
    /// walking over a World 3 gate tile that is no longer underwater.
    /// </summary>
    public void SetEffectSuppressed(bool suppressed)
    {
        if (targetTileSuppressed == suppressed)
            return;

        targetTileSuppressed = suppressed;
        ApplyMaterial();
    }

    private void Awake()
    {
        Shader shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            Debug.LogWarning($"Player water effect shader '{ShaderName}' was not found.", this);
            enabled = false;
            return;
        }

        waterMaterial = new Material(shader)
        {
            name = "Player Water Submersion (Runtime)"
        };

        CacheBodyRenderers();
        ApplyMaterial();
    }

    private void LateUpdate()
    {
        propertyBlock ??= new MaterialPropertyBlock();

        bool shouldSuppressPlayerVisual =
            TryGetComponent(out PlayerMountCompanion companion) &&
            companion.HasMountedLouie();

        if (mountedPlayerSuppressed != shouldSuppressPlayerVisual)
        {
            mountedPlayerSuppressed = shouldSuppressPlayerVisual;
            ApplyMaterial();
        }

        float waterSurfaceY = transform.position.y;

        for (int i = bodyRenderers.Count - 1; i >= 0; i--)
        {
            SpriteRenderer renderer = bodyRenderers[i];
            if (renderer == null)
            {
                bodyRenderers.RemoveAt(i);
                continue;
            }

            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(WaterSurfaceY, waterSurfaceY);
            propertyBlock.SetFloat(SurfaceLineHeight, DefaultSurfaceLineHeight);
            renderer.SetPropertyBlock(propertyBlock);
        }
    }

    private void OnTransformChildrenChanged()
    {
        if (waterMaterial == null)
            return;

        CacheBodyRenderers();
        ApplyMaterial();
    }

    private void OnDestroy()
    {
        foreach (KeyValuePair<SpriteRenderer, Material> entry in originalMaterials)
        {
            if (entry.Key != null)
                entry.Key.sharedMaterial = entry.Value;
        }

        if (waterMaterial != null)
            Destroy(waterMaterial);
    }

    private void CacheBodyRenderers()
    {
        bodyRenderers.Clear();

        AnimatedSpriteRenderer[] animatedRenderers = GetComponentsInChildren<AnimatedSpriteRenderer>(true);
        for (int i = 0; i < animatedRenderers.Length; i++)
        {
            AnimatedSpriteRenderer animated = animatedRenderers[i];
            if (animated == null || animated.transform.parent != transform)
                continue;

            if (!animated.TryGetComponent(out SpriteRenderer spriteRenderer) || spriteRenderer == null)
                continue;

            if (!originalMaterials.ContainsKey(spriteRenderer))
                originalMaterials.Add(spriteRenderer, spriteRenderer.sharedMaterial);

            bodyRenderers.Add(spriteRenderer);
        }
    }

    private void ApplyMaterial()
    {
        for (int i = 0; i < bodyRenderers.Count; i++)
        {
            SpriteRenderer renderer = bodyRenderers[i];
            if (renderer == null)
                continue;

            if ((targetTileSuppressed || mountedPlayerSuppressed) &&
                originalMaterials.TryGetValue(renderer, out Material originalMaterial))
                renderer.sharedMaterial = originalMaterial;
            else
                renderer.sharedMaterial = waterMaterial;
        }
    }
}

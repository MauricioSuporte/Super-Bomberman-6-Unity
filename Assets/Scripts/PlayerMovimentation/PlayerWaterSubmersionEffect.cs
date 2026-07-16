using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Applies the World 3 water treatment to the player's animated body sprites.
/// The surface is kept in player-local space, so it follows the player while
/// the lower body remains visibly underwater.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerWaterSubmersionEffect : MonoBehaviour
{
    private const string WaterMaterialResourcePath = "Materials/PlayerWaterSubmersion";

    private const float DefaultSurfaceLineHeight = 0.15f;

    [Header("CoreMechanisms Water Surface")]
    [Tooltip("World-space offset applied only when this effect is attached to a CoreMechanisms destructible. More negative values lower the waterline.")]
    private readonly float coreMechanismsWaterSurfaceYOffset = -0.125f;

    [Header("Diagnostics")]
    [Tooltip("Writes concise initialization and state-change diagnostics to the player log. Disable after investigating a build.")]
    [SerializeField] private bool diagnosticLogs = true;

    private static readonly int WaterSurfaceY = Shader.PropertyToID("_WaterSurfaceY");
    private static readonly int SurfaceLineHeight = Shader.PropertyToID("_SurfaceLineHeight");

    private readonly List<SpriteRenderer> bodyRenderers = new();
    private readonly Dictionary<SpriteRenderer, Material> originalMaterials = new();
    private MaterialPropertyBlock propertyBlock;

    private Material waterMaterial;
    private bool targetTileSuppressed;
    private bool mountedPlayerSuppressed;
    private bool terrainOrBombSuppressed;
    private Tilemap destructibleTilemap;
    private int bombLayerMask;
    private CoreMechanismsDestructible coreMechanisms;
    private bool initialStateLogged;

    /// <summary>
    /// Temporarily restores the original player materials, for example while
    /// walking over a World 3 gate tile that is no longer underwater.
    /// </summary>
    public void SetEffectSuppressed(bool suppressed)
    {
        if (targetTileSuppressed == suppressed)
            return;

        targetTileSuppressed = suppressed;
        LogDiagnostic($"Target-tile suppression changed to {suppressed}.");
        ApplyMaterial();
    }

    private void Awake()
    {
        bombLayerMask = LayerMask.GetMask("Bomb");
        TryGetComponent(out coreMechanisms);
        ResolveDestructibleTilemap();

        Material waterMaterialTemplate = Resources.Load<Material>(WaterMaterialResourcePath);
        if (waterMaterialTemplate == null)
        {
            Debug.LogWarning($"[WaterSubmerge] Material Resources/{WaterMaterialResourcePath}.mat was not found; effect disabled on '{name}'.", this);
            enabled = false;
            return;
        }

        waterMaterial = new Material(waterMaterialTemplate)
        {
            name = "Player Water Submersion (Runtime)"
        };

        CacheBodyRenderers();
        ApplyMaterial();
        LogDiagnostic($"Initialized. coreMechanisms={coreMechanisms != null}, renderers={bodyRenderers.Count}, material='{waterMaterialTemplate.name}', shader='{waterMaterial.shader.name}'.");
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

        bool shouldSuppressForTerrainOrBomb = IsOnDestructibleOrBomb();
        if (terrainOrBombSuppressed != shouldSuppressForTerrainOrBomb)
        {
            terrainOrBombSuppressed = shouldSuppressForTerrainOrBomb;
            ApplyMaterial();
        }

        float waterSurfaceY = transform.position.y +
                              (coreMechanisms != null ? coreMechanismsWaterSurfaceYOffset : 0f);

        if (!initialStateLogged)
        {
            initialStateLogged = true;
            LogDiagnostic($"First update. waterSurfaceY={waterSurfaceY:F3}, lineHeight={DefaultSurfaceLineHeight:F3}, active={!IsSuppressed()}.");
        }

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
        LogDiagnostic($"Child hierarchy changed; renderers recached={bodyRenderers.Count}.");
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

        if (bodyRenderers.Count == 0)
            Debug.LogWarning($"[WaterSubmerge] No direct-child AnimatedSpriteRenderer/SpriteRenderer was found on '{name}'.", this);
    }

    private bool IsOnDestructibleOrBomb()
    {
        if (GetComponentInParent<PlayerMountCompanion>() == null)
            return false;

        Vector3 worldPosition = GetWaterInteractionPosition();

        if (destructibleTilemap == null)
            ResolveDestructibleTilemap();

        if (destructibleTilemap != null &&
            destructibleTilemap.GetTile(destructibleTilemap.WorldToCell(worldPosition)) != null)
        {
            return true;
        }

        return bombLayerMask != 0 &&
               Physics2D.OverlapCircle(worldPosition, 0.2f, bombLayerMask) != null;
    }

    private Vector3 GetWaterInteractionPosition()
    {
        PlayerMountCompanion parentCompanion = GetComponentInParent<PlayerMountCompanion>();
        return parentCompanion != null ? parentCompanion.transform.position : transform.position;
    }

    private void ResolveDestructibleTilemap()
    {
        if (GameManager.Instance != null)
            destructibleTilemap = GameManager.Instance.destructibleTilemap;
    }

    private void ApplyMaterial()
    {
        bool suppressed = IsSuppressed();

        for (int i = 0; i < bodyRenderers.Count; i++)
        {
            SpriteRenderer renderer = bodyRenderers[i];
            if (renderer == null)
                continue;

            if (suppressed &&
                originalMaterials.TryGetValue(renderer, out Material originalMaterial))
                renderer.sharedMaterial = originalMaterial;
            else
                renderer.sharedMaterial = waterMaterial;
        }

        LogDiagnostic($"Material applied. renderers={bodyRenderers.Count}, suppressed={suppressed} (target={targetTileSuppressed}, mounted={mountedPlayerSuppressed}, terrainOrBomb={terrainOrBombSuppressed}).");
    }

    private bool IsSuppressed()
    {
        return targetTileSuppressed || mountedPlayerSuppressed || terrainOrBombSuppressed;
    }

    private void LogDiagnostic(string message)
    {
        if (diagnosticLogs)
            Debug.Log($"[WaterSubmerge] {message}", this);
    }
}

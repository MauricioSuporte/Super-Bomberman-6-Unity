using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace Assets.Scripts.Netcode
{
    // Approved waterline parameters, never a client terrain/gameplay query.
    public sealed class CoopWaterSurface : IEquatable<CoopWaterSurface>
    {
        public Vector4 parameters;
        public bool Equals(CoopWaterSurface other) => other != null && parameters == other.parameters;
        public bool IsValid() => float.IsFinite(parameters.x) && float.IsFinite(parameters.y) &&
            parameters.y >= 0 && parameters.y <= 1 && (parameters.z == 0 || parameters.z == 1) &&
            (parameters.w == 0 || parameters.w == 1);
    }

    // Bounded recipe for the authored Hall portal shader, never arbitrary client material state.
    public sealed class CoopPortalPalette : IEquatable<CoopPortalPalette>
    {
        public int mode, maskSprite;
        public bool beamMask;
        public float revealY, baseHeight;
        public Vector4 portalWorld, portalUv, renderWorld, renderUv;
        public bool Equals(CoopPortalPalette other) => other != null && mode == other.mode && maskSprite == other.maskSprite &&
            beamMask == other.beamMask && revealY == other.revealY && baseHeight == other.baseHeight &&
            portalWorld == other.portalWorld && portalUv == other.portalUv && renderWorld == other.renderWorld && renderUv == other.renderUv;
        static bool Finite(Vector4 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z) && float.IsFinite(value.w);
        public bool IsValid() => (mode == 1 || mode == 2) && maskSprite >= 0 && float.IsFinite(revealY) &&
            float.IsFinite(baseHeight) && baseHeight >= 0 && Finite(portalWorld) && Finite(portalUv) && Finite(renderWorld) && Finite(renderUv) &&
            portalWorld.z > 0 && portalWorld.w > 0 && renderWorld.z > 0 && renderWorld.w > 0;
    }

    // Optional approved material recipe; ordinary sprite frames carry only null.
    public sealed class CoopBlackoutMask : IEquatable<CoopBlackoutMask>
    {
        public int template, barrelSprite = -1;
        public Vector4 spriteBounds, region, roomBounds, barrelBounds, barrelUv, barrelFlip;
        public Matrix4x4 barrelWorldToLocal;
        public Vector4[] colors;
        public float tolerance, barrelAlpha;
        public bool barrel;

        public bool Equals(CoopBlackoutMask other)
        {
            if (other == null || template != other.template || barrelSprite != other.barrelSprite || barrel != other.barrel ||
                spriteBounds != other.spriteBounds || region != other.region || roomBounds != other.roomBounds ||
                barrelBounds != other.barrelBounds || barrelUv != other.barrelUv || barrelFlip != other.barrelFlip ||
                barrelWorldToLocal != other.barrelWorldToLocal || tolerance != other.tolerance || barrelAlpha != other.barrelAlpha ||
                (colors?.Length ?? 0) != (other.colors?.Length ?? 0)) return false;
            for (int i = 0; i < (colors?.Length ?? 0); i++) if (colors[i] != other.colors[i]) return false;
            return true;
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool Finite(Vector4 value) => Finite(value.x) && Finite(value.y) && Finite(value.z) && Finite(value.w);
        public bool IsValid()
        {
            if (template < 0 || colors == null || colors.Length > 8 || !Finite(tolerance) || tolerance < 0 || tolerance > .25f ||
                !Finite(spriteBounds) || spriteBounds.z <= 0 || spriteBounds.w <= 0 || !Finite(region) ||
                region.z <= region.x || region.w <= region.y || !Finite(roomBounds) || roomBounds.z < roomBounds.x || roomBounds.w < roomBounds.y) return false;
            foreach (var color in colors) if (!Finite(color)) return false;
            if (!barrel) return true;
            if (barrelSprite < 0 || !Finite(barrelBounds) || barrelBounds.z <= 0 || barrelBounds.w <= 0 ||
                !Finite(barrelUv) || !Finite(barrelFlip) || !Finite(barrelAlpha) || barrelAlpha < 0 || barrelAlpha > 1) return false;
            for (int i = 0; i < 16; i++) if (!Finite(barrelWorldToLocal[i])) return false;
            return true;
        }
    }

    public partial class BombermanNetworkManager
    {
        MaterialPropertyBlock coopPortalProperties;
        readonly HashSet<SpriteRenderer> coopPortalViews = new();
        CoopPortalPalette CaptureCoopPortal(SpriteRenderer renderer)
        {
            if (renderer.sharedMaterial == null || renderer.sharedMaterial.shader == null ||
                renderer.sharedMaterial.shader.name != "SuperBomberman/World3 Hall Portal Palette") return null;
            coopPortalProperties ??= new MaterialPropertyBlock();
            renderer.GetPropertyBlock(coopPortalProperties);
            int mode = Mathf.RoundToInt(coopPortalProperties.GetFloat("_RevealMode"));
            if (mode == 0) return null;
            var portal = renderer.GetComponentInParent<global::StageAssets.World3HallPortal>(true);
            if (portal == null) return null;
            return new CoopPortalPalette { mode = mode, maskSprite = CoopSpriteId(portal.PaletteMaskSprite),
                beamMask = coopPortalProperties.GetFloat("_BeamMaskEnabled") > .5f,
                revealY = coopPortalProperties.GetFloat("_RevealY"), baseHeight = coopPortalProperties.GetFloat("_PortalBaseMaskHeight"),
                portalWorld = coopPortalProperties.GetVector("_PortalWorldRect"), portalUv = coopPortalProperties.GetVector("_PortalUVRect"),
                renderWorld = coopPortalProperties.GetVector("_RenderWorldRect"), renderUv = coopPortalProperties.GetVector("_RenderUVRect") };
        }
        bool ApplyCoopPortal(SpriteRenderer view, CoopPortalPalette portal)
        {
            if (!portal.IsValid() || view.sharedMaterial == null || view.sharedMaterial.shader == null ||
                view.sharedMaterial.shader.name != "SuperBomberman/World3 Hall Portal Palette") return false;
            var mask = CoopSpriteAt(portal.maskSprite);
            if (mask == null) return false;
            coopPortalProperties ??= new MaterialPropertyBlock(); coopPortalProperties.Clear();
            coopPortalProperties.SetFloat("_RevealMode", portal.mode);
            coopPortalProperties.SetFloat("_RevealY", portal.revealY);
            coopPortalProperties.SetFloat("_BeamMaskEnabled", portal.beamMask ? 1 : 0);
            coopPortalProperties.SetFloat("_PortalBaseMaskHeight", portal.baseHeight);
            coopPortalProperties.SetTexture("_PortalMaskTex", mask.texture);
            coopPortalProperties.SetVector("_PortalWorldRect", portal.portalWorld);
            coopPortalProperties.SetVector("_PortalUVRect", portal.portalUv);
            coopPortalProperties.SetVector("_RenderWorldRect", portal.renderWorld);
            coopPortalProperties.SetVector("_RenderUVRect", portal.renderUv);
            view.SetPropertyBlock(coopPortalProperties); coopPortalViews.Add(view);
            return true;
        }
        void ReleaseCoopPortal(SpriteRenderer view)
        {
            if (coopPortalViews.Remove(view) && view != null) view.SetPropertyBlock(null);
        }

        MaterialPropertyBlock coopWaterProperties;
        Material coopWaterTemplate;
        readonly Dictionary<SpriteRenderer, Vector4> coopWaterViewStates = new();
        readonly Dictionary<int, int> coopViewMaterialIds = new();
        static Shader coopWaterShader;
        static bool UsesCoopWater(SpriteRenderer renderer)
        {
            var material = renderer.sharedMaterial;
            if (material == null) return false;
            coopWaterShader = coopWaterShader != null ? coopWaterShader : Shader.Find("SuperBomberman/PlayerWaterSubmersion");
            return coopWaterShader != null && material.shader == coopWaterShader;
        }
        internal CoopWaterSurface CaptureCoopWater(SpriteRenderer renderer)
        {
            if (!UsesCoopWater(renderer)) return null;
            coopWaterProperties ??= new MaterialPropertyBlock();
            renderer.GetPropertyBlock(coopWaterProperties);
            return new CoopWaterSurface { parameters = new Vector4(coopWaterProperties.GetFloat("_WaterSurfaceY"),
                coopWaterProperties.GetFloat("_SurfaceLineHeight"), coopWaterProperties.GetFloat("_UseSurfaceLine"),
                coopWaterProperties.GetFloat("_PremultiplyUnderwaterOpacity")) };
        }

        internal bool ApplyCoopWater(SpriteRenderer view, CoopWaterSurface water)
        {
            if (!water.IsValid()) return false;
            coopWaterTemplate = coopWaterTemplate != null ? coopWaterTemplate : Resources.Load<Material>("Materials/PlayerWaterSubmersion");
            if (coopWaterTemplate == null) return false;
            if (coopWaterViewStates.TryGetValue(view, out var previous) && previous == water.parameters) return true;
            view.sharedMaterial = coopWaterTemplate;
            coopWaterProperties ??= new MaterialPropertyBlock(); coopWaterProperties.Clear();
            coopWaterProperties.SetFloat("_WaterSurfaceY", water.parameters.x);
            coopWaterProperties.SetFloat("_SurfaceLineHeight", water.parameters.y);
            coopWaterProperties.SetFloat("_UseSurfaceLine", water.parameters.z);
            coopWaterProperties.SetFloat("_PremultiplyUnderwaterOpacity", water.parameters.w);
            view.SetPropertyBlock(coopWaterProperties);
            coopWaterViewStates[view] = water.parameters;
            return true;
        }

        void ReleaseCoopWater(SpriteRenderer view)
        {
            if (view != null && coopWaterViewStates.Remove(view)) view.SetPropertyBlock(null);
        }

        void ApplyCoopRegularMaterial(int id, SpriteRenderer view, int materialId)
        {
            ReleaseCoopWater(view);
            if (coopViewMaterialIds.TryGetValue(id, out int previous) && previous == materialId) return;
            var material = At(coopCatalog.materials, materialId);
            view.sharedMaterial = material != null ? material : coopDefaultSpriteMaterial;
            coopViewMaterialIds[id] = materialId;
        }

        readonly Dictionary<int, (Material template, Material instance, Sprite barrel)> coopBlackoutMasks = new();
        readonly Dictionary<int, (int template, Material instance)> coopViewMaskMaterials = new();
        readonly Vector4[] coopMaskColorBuffer = new Vector4[8];
        Material coopDefaultSpriteMaterial;

        public static void CoopRegisterBlackoutMask(SpriteRenderer overlay, Material template, Material instance, SpriteRenderer barrel,
            EnemyMovementController motionOwner = null)
        {
            if (overlay == null || !NetworkServer.active || !IsCoopStage || singleton is not BombermanNetworkManager manager ||
                manager.coopCatalog == null || overlay.gameObject.scene.name != manager.coopScene || HasNetworkParent(overlay.transform)) return;
            int id = overlay.GetEntityId().GetHashCode();
            if (!manager.coopBlackoutMasks.ContainsKey(id)) manager.TrackCoopRenderer(overlay);
            float limit = EnemyVisualLimit(motionOwner);
            if (limit > 0) manager.coopEnemyVisualLimits[id] = limit;
            manager.coopBlackoutMasks[id] = (template, instance, barrel != null ? barrel.sprite : null);
        }

        CoopBlackoutMask CaptureCoopBlackoutMask(int id)
        {
            if (!coopBlackoutMasks.TryGetValue(id, out var source)) return null;
            if (source.instance == null) return new CoopBlackoutMask { template = -1 };
            var material = source.instance;
            int count = Mathf.Clamp(material.GetInt("_VisibleColorCount"), 0, 8);
            var values = count > 0 ? material.GetVectorArray("_VisibleColors") : null;
            var colors = new Vector4[count];
            if (values != null) Array.Copy(values, colors, Mathf.Min(values.Length, count));
            bool barrel = material.GetFloat("_BarrelOcclusion") > .5f;
            return new CoopBlackoutMask { template = CatalogId(source.template, coopMaterialIds),
                spriteBounds = material.GetVector("_SpriteBounds"), region = material.GetVector("_VisibleRegion"),
                roomBounds = material.GetVector("_RoomBounds"), colors = colors, tolerance = material.GetFloat("_ColorTolerance"),
                barrel = barrel, barrelSprite = barrel ? CoopSpriteId(source.barrel) : -1,
                barrelBounds = barrel ? material.GetVector("_BarrelBounds") : default,
                barrelUv = barrel ? material.GetVector("_BarrelUV") : default,
                barrelFlip = barrel ? material.GetVector("_BarrelFlip") : default,
                barrelWorldToLocal = barrel ? material.GetMatrix("_BarrelWorldToLocal") : default,
                barrelAlpha = barrel ? material.GetFloat("_BarrelAlpha") : 0 };
        }

        bool ApplyCoopBlackoutMask(int id, SpriteRenderer view, CoopBlackoutMask recipe)
        {
            if (!recipe.IsValid()) return false;
            var template = At(coopCatalog.materials, recipe.template);
            if (template == null || template.shader == null || template.shader.name != "SuperBomberman/Blackout Visible Parts") return false;
            var barrelSprite = recipe.barrel ? CoopSpriteAt(recipe.barrelSprite) : null;
            if (recipe.barrel && barrelSprite == null) return false; // Wait for its approved sprite definition.
            if (!coopViewMaskMaterials.TryGetValue(id, out var material) || material.template != recipe.template || material.instance == null)
            {
                ReleaseCoopMaskMaterial(id, view);
                material = (recipe.template, new Material(template)); coopViewMaskMaterials[id] = material;
            }
            var instance = material.instance; view.sharedMaterial = instance;
            instance.SetVector("_SpriteBounds", recipe.spriteBounds); instance.SetVector("_VisibleRegion", recipe.region);
            instance.SetVector("_RoomBounds", recipe.roomBounds); instance.SetFloat("_ColorTolerance", recipe.tolerance);
            instance.SetInt("_VisibleColorCount", recipe.colors.Length);
            // Unity cannot grow an array after its first SetVectorArray call.
            // Always submit the shader capacity, even when this palette is empty.
            Array.Clear(coopMaskColorBuffer, 0, coopMaskColorBuffer.Length);
            Array.Copy(recipe.colors, coopMaskColorBuffer, recipe.colors.Length);
            instance.SetVectorArray("_VisibleColors", coopMaskColorBuffer);
            instance.SetFloat("_BarrelOcclusion", recipe.barrel ? 1 : 0);
            if (recipe.barrel)
            {
                instance.SetTexture("_BarrelTex", barrelSprite.texture);
                instance.SetMatrix("_BarrelWorldToLocal", recipe.barrelWorldToLocal);
                instance.SetVector("_BarrelBounds", recipe.barrelBounds); instance.SetVector("_BarrelUV", recipe.barrelUv);
                instance.SetVector("_BarrelFlip", recipe.barrelFlip); instance.SetFloat("_BarrelAlpha", recipe.barrelAlpha);
            }
            return true;
        }

        void ReleaseCoopMaskMaterial(int id, SpriteRenderer view)
        {
            if (!coopViewMaskMaterials.Remove(id, out var material)) return;
            if (view != null) view.sharedMaterial = coopDefaultSpriteMaterial;
            DestroyCoopMaskMaterial(material.instance);
        }

        static void DestroyCoopMaskMaterial(Material material)
        {
            if (material == null) return;
            if (Application.isPlaying) Destroy(material);
            else DestroyImmediate(material);
        }

        void ClearCoopBlackoutMasks()
        {
            foreach (var value in coopViewMaskMaterials.Values) DestroyCoopMaskMaterial(value.instance);
            coopViewMaskMaterials.Clear(); coopBlackoutMasks.Clear(); coopDefaultSpriteMaterial = null;
            coopWaterTemplate = null; coopWaterProperties = null;
            coopWaterViewStates.Clear(); coopViewMaterialIds.Clear();
            coopPortalViews.Clear(); coopPortalProperties = null;
        }
    }
}

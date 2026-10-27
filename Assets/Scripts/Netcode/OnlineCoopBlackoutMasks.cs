using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace Assets.Scripts.Netcode
{
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
        readonly Dictionary<int, (Material template, Material instance, Sprite barrel)> coopBlackoutMasks = new();
        readonly Dictionary<int, (int template, Material instance)> coopViewMaskMaterials = new();
        readonly Vector4[] coopMaskColorBuffer = new Vector4[8];
        Material coopDefaultSpriteMaterial;

        public static void CoopRegisterBlackoutMask(SpriteRenderer overlay, Material template, Material instance, SpriteRenderer barrel)
        {
            if (overlay == null || !NetworkServer.active || !IsCoopStage || singleton is not BombermanNetworkManager manager ||
                manager.coopCatalog == null || overlay.gameObject.scene.name != manager.coopScene || HasNetworkParent(overlay.transform)) return;
            int id = overlay.GetEntityId().GetHashCode();
            if (!manager.coopBlackoutMasks.ContainsKey(id)) manager.TrackCoopRenderer(overlay);
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
        }
    }
}

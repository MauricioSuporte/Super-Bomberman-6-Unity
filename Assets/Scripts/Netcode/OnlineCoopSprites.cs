using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace Assets.Scripts.Netcode
{
    public struct CoopSpriteDefinition : NetworkMessage
    {
        public string scene;
        public int id, backingSprite, width, height;
        public Rect rect;
        public Vector2 pivot;
        public float pixelsPerUnit;
        public Color32[] pixels;
    }

    public partial class BombermanNetworkManager
    {
        readonly Dictionary<Sprite, int> coopDynamicIds = new();
        readonly Dictionary<Sprite, uint> coopDynamicVersions = new();
        readonly Dictionary<int, CoopSpriteDefinition> coopDefinitions = new();
        readonly Dictionary<int, Sprite> coopDynamicSprites = new();
        readonly Dictionary<int, Texture2D> coopDynamicTextures = new();
        readonly Dictionary<int, int> coopViewSpriteIds = new();

        int CoopSpriteId(Sprite sprite)
        {
            if (sprite == null) return -1;
            if (coopSpriteIds.TryGetValue(sprite, out int id)) return id;
            bool fresh = !coopDynamicIds.TryGetValue(sprite, out id);
            if (fresh)
            {
                id = coopCatalog.sprites.Length + coopDynamicIds.Count;
                coopDynamicIds[sprite] = id;
            }
            var texture = sprite.texture;
            if (!fresh && coopDynamicVersions.TryGetValue(sprite, out uint version) && version == texture.updateCount) return id;
            int backing = -1;
            for (int i = 0; i < coopCatalog.sprites.Length; i++)
                if (coopCatalog.sprites[i] != null && coopCatalog.sprites[i].texture == texture) { backing = i; break; }
            var definition = new CoopSpriteDefinition { scene = coopScene, id = id, backingSprite = backing,
                width = texture.width, height = texture.height, rect = sprite.rect,
                pivot = sprite.pivot / sprite.rect.size, pixelsPerUnit = sprite.pixelsPerUnit };
            if (backing < 0)
            {
                if (!texture.isReadable || (long)texture.width * texture.height > 65536)
                {
                    CatalogId(sprite, coopSpriteIds);
                    return -1;
                }
                definition.pixels = texture.GetPixels32();
            }
            coopDynamicVersions[sprite] = texture.updateCount;
            coopDefinitions[id] = definition;
            NetworkServer.SendToReady(definition);
            return id;
        }

        void ApplyCoopSpriteDefinition(CoopSpriteDefinition message)
        {
            if (!CanApplyCoop(message.scene) || message.id < coopCatalog.sprites.Length) return;
            Texture2D texture;
            if (message.backingSprite >= 0)
            {
                var backing = At(coopCatalog.sprites, message.backingSprite);
                if (backing == null) return;
                texture = backing.texture;
            }
            else
            {
                if (message.width <= 0 || message.height <= 0 || (long)message.width * message.height > 65536 ||
                    message.pixels == null || message.pixels.Length != message.width * message.height) return;
                if (!coopDynamicTextures.TryGetValue(message.id, out texture))
                {
                    texture = new Texture2D(message.width, message.height, TextureFormat.RGBA32, false)
                        { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                    coopDynamicTextures[message.id] = texture;
                }
                texture.SetPixels32(message.pixels); texture.Apply();
            }
            if (!coopDynamicSprites.TryGetValue(message.id, out var sprite))
            {
                sprite = Sprite.Create(texture, message.rect, message.pivot, message.pixelsPerUnit, 0, SpriteMeshType.FullRect);
                coopDynamicSprites[message.id] = sprite;
            }
            foreach (var view in coopViews)
                if (view.Value != null && coopViewSpriteIds.TryGetValue(view.Key, out int id) && id == message.id) view.Value.sprite = sprite;
        }

        Sprite CoopSpriteAt(int id) => id < coopCatalog.sprites.Length ? At(coopCatalog.sprites, id) :
            coopDynamicSprites.TryGetValue(id, out var sprite) ? sprite : null;

        void ClearCoopSprites()
        {
            foreach (var sprite in coopDynamicSprites.Values) if (sprite != null) Destroy(sprite);
            foreach (var texture in coopDynamicTextures.Values) if (texture != null) Destroy(texture);
            coopDynamicIds.Clear(); coopDynamicVersions.Clear(); coopDefinitions.Clear();
            coopDynamicSprites.Clear(); coopDynamicTextures.Clear(); coopViewSpriteIds.Clear();
        }
    }
}

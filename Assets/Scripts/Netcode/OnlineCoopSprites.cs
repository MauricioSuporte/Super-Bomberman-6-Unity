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
            ClearCoopBlackoutMasks();
            foreach (var sprite in coopDynamicSprites.Values) if (sprite != null) Destroy(sprite);
            foreach (var texture in coopDynamicTextures.Values) if (texture != null) Destroy(texture);
            coopAnimationDefinitions.Clear(); coopAnimationClips.Clear(); coopVisualSamples.Clear();
            coopVisualRemovals.Clear(); coopViewClips.Clear(); bombVisualSamples.Clear();
            coopExpiredViews.Clear(); expiredBombViews.Clear(); coopPendingRenderers.Clear(); nextBombAnimation = 0;
            coopDynamicIds.Clear(); coopDynamicVersions.Clear(); coopDefinitions.Clear();
            coopDynamicSprites.Clear(); coopDynamicTextures.Clear(); coopViewSpriteIds.Clear();
        }
    }

    public struct CoopAnimationDefinition : NetworkMessage
    {
        public string scene;
        public int id, idleSprite, loopStart;
        public int[] sprites;
        public float animationTime, sequenceDuration, restartDelay;
        public float[] durations;
        public Vector2[] offsets;
        public bool sequence, loop, pingPong;
    }

    public struct BombAnimationFrame
    {
        public uint id;
        public double time;
        public float animationTime;
        public AnimatedSpriteRenderer.PlaybackState playback;
    }

    public struct BombAnimations : NetworkMessage
    {
        public string scene;
        public BombAnimationFrame[] frames;
    }

    public partial class BombermanNetworkManager
    {
        readonly Dictionary<int, CoopAnimationDefinition> coopAnimationDefinitions = new();
        readonly Dictionary<int, (int hash, int clip)> coopAnimationClips = new();
        readonly Dictionary<int, List<CoopSpriteFrame>> coopVisualSamples = new();
        readonly Dictionary<int, double> coopVisualRemovals = new();
        readonly Dictionary<uint, List<BombAnimationFrame>> bombVisualSamples = new();
        readonly List<int> coopExpiredViews = new();
        readonly List<uint> expiredBombViews = new();
        float nextBombAnimation;

        int CaptureCoopAnimation(AnimatedSpriteRenderer source)
        {
            var hash = new System.HashCode();
            hash.Add(source.idleSprite); hash.Add(source.animationTime);
            hash.Add(source.useSequenceDuration); hash.Add(source.sequenceDuration);
            hash.Add(source.loop); hash.Add(source.loopStartFrame);
            hash.Add(source.loopRestartDelay); hash.Add(source.pingPong);
            if (source.animationSprite != null) foreach (var sprite in source.animationSprite) hash.Add(sprite);
            if (source.frameDurations != null) foreach (float duration in source.frameDurations) hash.Add(duration);
            if (source.frameOffsets != null) foreach (var offset in source.frameOffsets) hash.Add(offset);
            int key = source.GetEntityId().GetHashCode(), signature = hash.ToHashCode();
            if (coopAnimationClips.TryGetValue(key, out var cached) && cached.hash == signature) return cached.clip;
            int id = coopAnimationDefinitions.Count + 1;
            var sprites = source.animationSprite ?? System.Array.Empty<Sprite>();
            var definition = new CoopAnimationDefinition
            {
                scene = coopScene, id = id, idleSprite = CoopSpriteId(source.idleSprite),
                sprites = new int[sprites.Length], animationTime = source.animationTime,
                sequence = source.useSequenceDuration, sequenceDuration = source.sequenceDuration,
                durations = source.frameDurations == null ? null : (float[])source.frameDurations.Clone(),
                offsets = source.frameOffsets == null ? null : (Vector2[])source.frameOffsets.Clone(),
                loop = source.loop, loopStart = source.loopStartFrame,
                restartDelay = source.loopRestartDelay, pingPong = source.pingPong
            };
            for (int i = 0; i < sprites.Length; i++) definition.sprites[i] = CoopSpriteId(sprites[i]);
            coopAnimationClips[key] = (signature, id);
            coopAnimationDefinitions[id] = definition;
            NetworkServer.SendToReady(definition);
            return id;
        }

        void ApplyCoopAnimationDefinition(CoopAnimationDefinition message)
        {
            if (CanApplyCoop(message.scene)) coopAnimationDefinitions[message.id] = message;
        }

        void PresentCoopAnimation(SpriteRenderer view, CoopSpriteFrame frame)
        {
            if (frame.animation == 0 || !coopAnimationDefinitions.TryGetValue(frame.animation, out var clip)) return;
            if (!view.TryGetComponent<AnimatedSpriteRenderer>(out var animation))
            {
                animation = view.gameObject.AddComponent<AnimatedSpriteRenderer>();
                animation.SetManualAnimationUpdate(true);
            }
            // Definitions are immutable and sent once. Resolve their sprites only when the clip changes.
            if (!coopViewClips.TryGetValue(frame.id, out int previous) || previous != frame.animation)
            {
                animation.idleSprite = CoopSpriteAt(clip.idleSprite);
                animation.animationSprite = new Sprite[clip.sprites.Length];
                for (int i = 0; i < clip.sprites.Length; i++) animation.animationSprite[i] = CoopSpriteAt(clip.sprites[i]);
                animation.animationTime = clip.animationTime;
                animation.useSequenceDuration = clip.sequence; animation.sequenceDuration = clip.sequenceDuration;
                animation.frameDurations = clip.durations;
                animation.loop = clip.loop; animation.loopStartFrame = clip.loopStart;
                animation.loopRestartDelay = clip.restartDelay; animation.pingPong = clip.pingPong;
                coopViewClips[frame.id] = frame.animation;
            }
            animation.PresentPlayback(frame.playback, (float)System.Math.Max(0, CoopVisualClock - frame.time), false);
            if (clip.offsets != null && clip.offsets.Length > 0)
            {
                Vector2 offset = clip.offsets[Mathf.Clamp(animation.CurrentFrame, 0, clip.offsets.Length - 1)];
                view.transform.position += frame.offsetRight * offset.x + frame.offsetUp * offset.y;
            }
        }

        readonly Dictionary<int, int> coopViewClips = new();

        // Align world props/supports with the approved player transform timeline.
        // Sampling on packet time while the rider uses NT's offset makes the
        // platform appear ahead of its passenger even when both are smooth.
        static double CoopVisualClock => NetworkTime.time - TransformVisualOffset(NetworkClient.localPlayer);

        void PresentOnlineSprites()
        {
            if (!NetworkClient.active || NetworkServer.active) return;
            double clock = CoopVisualClock;
            if (coopCatalog != null && coopViewRoot != null)
            {
                coopExpiredViews.Clear();
                foreach (var pair in coopVisualSamples)
                {
                    int id = pair.Key;
                    if (coopVisualRemovals.TryGetValue(id, out double removed) && clock >= removed)
                    {
                        ReleaseCoopMaskMaterial(id, null);
                        if (coopViews.Remove(id, out var view) && view != null) Destroy(view.gameObject);
                        coopViewSpriteIds.Remove(id); coopViewClips.Remove(id);
                        coopExpiredViews.Add(id); continue;
                    }
                    var samples = pair.Value;
                    while (samples.Count > 1 && samples[1].time <= clock) samples.RemoveAt(0);
                    if (samples.Count == 0 || samples[0].time > clock) continue;
                    var frame = samples[0];
                    if (samples.Count > 1 && frame.visible && samples[1].visible && samples[1].time > frame.time)
                    {
                        var next = samples[1];
                        float t = Mathf.Clamp01((float)((clock - frame.time) / (next.time - frame.time)));
                        frame.position = Vector3.Lerp(frame.position, next.position, t);
                        frame.rotation = Quaternion.Slerp(frame.rotation, next.rotation, t);
                        frame.scale = Vector3.Lerp(frame.scale, next.scale, t);
                    }
                    PresentCoopSprite(frame);
                }
                foreach (int id in coopExpiredViews) { coopVisualSamples.Remove(id); coopVisualRemovals.Remove(id); }
            }
            clock = NetworkTime.time;
            expiredBombViews.Clear();
            foreach (var pair in bombVisualSamples)
            {
                if (!NetworkClient.spawned.TryGetValue(pair.Key, out var identity) || identity == null)
                {
                    if (pair.Value.Count > 0 && clock - pair.Value[^1].time > 2) expiredBombViews.Add(pair.Key);
                    continue;
                }
                var samples = pair.Value;
                double bombClock = clock - TransformVisualOffset(identity);
                while (samples.Count > 1 && samples[1].time <= bombClock) samples.RemoveAt(0);
                if (samples.Count == 0 || samples[0].time > bombClock || !identity.TryGetComponent<AnimatedSpriteRenderer>(out var animation)) continue;
                var frame = samples[0];
                animation.SetManualAnimationUpdate(true);
                animation.animationTime = frame.animationTime;
                animation.PresentPlayback(frame.playback, (float)System.Math.Max(0, bombClock - frame.time));
            }
            foreach (uint id in expiredBombViews) bombVisualSamples.Remove(id);
        }

        static double TransformVisualOffset(NetworkIdentity identity)
        {
            if (identity == null || !identity.TryGetComponent<NetworkTransformReliable>(out var sync)) return 0;
            return NetworkClient.sendInterval * (sync.sendIntervalMultiplier - 1 +
                (sync.timelineOffset ? sync.sendIntervalMultiplier : 0));
        }

        readonly HashSet<SpriteRenderer> coopPendingRenderers = new();

        public static void CoopTrackAnimation(AnimatedSpriteRenderer animation)
        {
            if (NetworkServer.active && animation != null && animation.TryGetComponent<SpriteRenderer>(out var renderer)) CoopTrackRenderer(renderer);
        }

        // Plain projectile/particle renderers have no AnimatedSpriteRenderer.OnEnable
        // hook. Register once after configuring them, before the discovery interval.
        public static void CoopTrackRenderer(SpriteRenderer renderer)
        {
            if (!NetworkServer.active || !IsCoopStage || singleton is not BombermanNetworkManager manager) return;
            manager.TrackCoopRenderer(renderer);
        }

        void TrackCoopRenderer(SpriteRenderer renderer)
        {
            if (renderer == null || coopCatalog == null || renderer.gameObject.scene.name != coopScene || HasNetworkParent(renderer.transform)) return;
            coopPendingRenderers.Add(renderer);
        }

        void TickBombAnimations()
        {
            if (!NetworkServer.active || Time.unscaledTime < nextBombAnimation || NetworkServer.isLoadingScene) return;
            nextBombAnimation = Time.unscaledTime + .05f;
            var frames = new List<BombAnimationFrame>();
            foreach (var bomb in Bomb.ActiveBombs)
            {
                if (bomb == null || !bomb.TryGetComponent<NetworkIdentity>(out var identity) || identity.netId == 0 ||
                    !bomb.TryGetComponent<AnimatedSpriteRenderer>(out var animation)) continue;
                frames.Add(new BombAnimationFrame { id = identity.netId, time = NetworkTime.localTime,
                    animationTime = animation.animationTime, playback = animation.CapturePlayback() });
            }
            if (frames.Count > 0) NetworkServer.SendToReady(new BombAnimations
                { scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name, frames = frames.ToArray() });
        }

        void ApplyBombAnimations(BombAnimations message)
        {
            if (NetworkServer.active || message.scene != UnityEngine.SceneManagement.SceneManager.GetActiveScene().name) return;
            foreach (var frame in message.frames ?? System.Array.Empty<BombAnimationFrame>())
            {
                if (!bombVisualSamples.TryGetValue(frame.id, out var samples))
                    bombVisualSamples[frame.id] = samples = new List<BombAnimationFrame>();
                samples.Add(frame);
                if (samples.Count > 64) samples.RemoveAt(0);
            }
        }
    }
}

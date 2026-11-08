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
        readonly Dictionary<int, BattleMode9MinecartController> coopCartCarriers = new();
        public static void RegisterCoopCartCarrier(int id, BattleMode9MinecartController cart)
        {
            if (singleton is BombermanNetworkManager manager && manager != null && cart != null) manager.coopCartCarriers[id] = cart;
        }
        double CoopCarrierClock(int id, double fallback) => coopCartCarriers.TryGetValue(id, out var cart) && cart != null ? cart.GetCartPresentationTime(fallback) : fallback;
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
            ClearCoopBlackoutMasks(); ClearCoopPresentationMetrics();
            foreach (var sprite in coopDynamicSprites.Values) if (sprite != null) Destroy(sprite);
            foreach (var texture in coopDynamicTextures.Values) if (texture != null) Destroy(texture);
            coopAnimationDefinitions.Clear(); coopAnimationClips.Clear(); coopVisualSamples.Clear();
            coopVisualRemovals.Clear(); coopViewClips.Clear(); bombVisualSamples.Clear();
            coopExpiredViews.Clear(); expiredBombViews.Clear(); coopPendingRenderers.Clear(); nextBombAnimation = 0;
            coopEnemyVisualLimits.Clear(); coopPhysicsPause.Clear(); coopCartCarriers.Clear();
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

    public struct CoopMotionEntry
    {
        public int id;
        public Vector3 position;
    }

    /// <summary>
    /// R02: position-only samples for world renderers that translated during a server frame. Full CoopSprites
    /// frames stay at 20 Hz; without these, a client whose presentation clock trails the server by less than one
    /// 50-67 ms frame interval runs past the newest sample, holds, and then jumps. Anchors carry the approved pose
    /// of the previous server frame when motion starts after a rest, so the segment does not begin early.
    /// </summary>
    public struct CoopMotion : NetworkMessage
    {
        public string scene;
        public double time, anchorTime;
        public CoopMotionEntry[] anchors, entries;
        // Explicit repositions of screen-space overlays (a wrap to the other edge or a camera cut) at `time`.
        // repositionAnchors[i] is the approved pose of the same renderer at anchorTime, the last frame before the jump.
        public CoopMotionEntry[] repositionAnchors, repositions;
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
        readonly Dictionary<int, List<CoopVisualSample>> coopVisualSamples = new();
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

        void PresentCoopAnimation(SpriteRenderer view, CoopSpriteFrame frame, double playbackOrigin)
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
            animation.PresentPlayback(frame.playback, (float)System.Math.Max(0, CoopCarrierClock(frame.id, CoopVisualClock) - playbackOrigin), false);
            if (clip.offsets != null && clip.offsets.Length > 0)
            {
                Vector2 offset = clip.offsets[Mathf.Clamp(animation.CurrentFrame, 0, clip.offsets.Length - 1)];
                view.transform.position += frame.offsetRight * offset.x + frame.offsetUp * offset.y;
            }
        }

        readonly Dictionary<int, int> coopViewClips = new();

        static readonly Unity.Profiling.ProfilerMarker CoopMotionMarker = new("SB6.Coop.Motion");
        readonly Dictionary<int, double> coopLastSentTime = new();
        readonly Dictionary<int, Vector3> coopPreviousPose = new();
        readonly Dictionary<int, bool> coopMotionScreenSpace = new();
        readonly HashSet<int> coopMovedLastFrame = new();
        readonly Dictionary<int, CoopSegmentState> coopSegments = new();
        readonly List<CoopMotionEntry> coopMotionEntries = new();
        readonly List<CoopMotionEntry> coopMotionAnchors = new();
        readonly List<CoopMotionEntry> coopRepositionAnchors = new();
        readonly List<CoopMotionEntry> coopRepositions = new();
        readonly HashSet<int> coopRepositionIds = new();
        readonly List<SpriteRenderer> coopOverlayRenderers = new();
        SpriteRenderer[] coopOverlaySource;
        double coopPreviousFrameTime = double.NegativeInfinity;
        static int coopLegacyState, coopMotionModeState;

        // Frequency limit: at most one position sample per renderer per Mirror send tick (sendRate 60). The client
        // receives batches at that rate, so a faster server frame rate would only add bytes, never earlier poses.
        const double CoopMotionMinInterval = .9 / 60;
        // Reposition: a screen-space overlay moving farther than this between two server frames jumped (32 px).
        const float CoopRepositionJump = 2f;
        // Development comparison modes ("segments" keeps every turn vertex within this tolerance).
        const int CoopSegmentHeartbeatFrames = 3;
        const float CoopSegmentTolerance = .5f / 16f;

        internal enum CoopMotionMode { Frame = 1, Half = 2, Segments = 3 }

        sealed class CoopSegmentState
        {
            public double startTime;
            public Vector3 start;
            public int count;
            public readonly double[] times = new double[CoopSegmentHeartbeatFrames + 1];
            public readonly Vector3[] poses = new Vector3[CoopSegmentHeartbeatFrames + 1];
            public void Reset(double time, Vector3 position) { startTime = time; start = position; count = 0; }
            public void Add(double time, Vector3 position)
            {
                if (count == times.Length) { System.Array.Copy(times, 1, times, 0, count - 1); System.Array.Copy(poses, 1, poses, 0, count - 1); count--; }
                times[count] = time; poses[count] = position; count++;
            }
            // Would the straight segment from the start to (time, position) miss a buffered native pose?
            public bool Deviates(double time, Vector3 position)
            {
                for (int i = 0; i < count; i++)
                {
                    float t = (float)((times[i] - startTime) / (time - startTime));
                    if (Vector3.Distance(Vector3.Lerp(start, position, t), poses[i]) > CoopSegmentTolerance) return true;
                }
                return false;
            }
        }

        // Development A/B only: "-coop-r02-legacy" restores the pre-R02 20 Hz-only stream for measurements.
        internal static bool CoopR02Legacy
        {
            get
            {
                if (coopLegacyState == 0)
                    coopLegacyState = Debug.isDebugBuild && System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-coop-r02-legacy") >= 0 ? 1 : 2;
                return coopLegacyState == 1;
            }
        }

        // Development comparison only: "-coop-r02-motion frame|half|segments". Release builds always use Frame.
        internal static CoopMotionMode CoopMotionModeSetting
        {
            get
            {
                if (coopMotionModeState != 0) return (CoopMotionMode)coopMotionModeState;
                coopMotionModeState = (int)CoopMotionMode.Frame;
                var args = System.Environment.GetCommandLineArgs();
                int index = System.Array.IndexOf(args, "-coop-r02-motion");
                if (Debug.isDebugBuild && index >= 0 && index + 1 < args.Length)
                    coopMotionModeState = args[index + 1] switch { "half" => (int)CoopMotionMode.Half, "segments" => (int)CoopMotionMode.Segments, _ => (int)CoopMotionMode.Frame };
                return (CoopMotionMode)coopMotionModeState;
            }
        }

        bool TryCoopRestAnchor(int id, in CoopSpriteFrame previous, in CoopSpriteFrame frame, out CoopSpriteFrame anchor)
        {
            anchor = previous;
            if (CoopR02Legacy || !previous.visible || !frame.visible) return false;
            if (previous.position == frame.position && previous.rotation == frame.rotation && previous.scale == frame.scale) return false;
            double at = coopPreviousFrameTime;
            if (at <= previous.time || at >= frame.time) return false;
            if (coopLastSentTime.TryGetValue(id, out double sent) && sent >= at) return false;
            anchor.time = at;
            if (coopPreviousPose.TryGetValue(id, out var pose)) anchor.position = pose;
            coopLastSentTime[id] = at;
            return true;
        }

        // Called at the start of every server tick, before full frames are captured, so the full frame of a jump
        // frame (and the Development shape reconstruction) already knows about the reposition.
        void TrackCoopOverlays(bool shapeDiagnostics)
        {
            coopRepositionAnchors.Clear(); coopRepositions.Clear(); coopRepositionIds.Clear();
            if (CoopR02Legacy) return;
            if (!ReferenceEquals(coopOverlaySource, coopRenderers))
            {
                coopOverlaySource = coopRenderers; coopOverlayRenderers.Clear();
                if (coopRenderers != null)
                    foreach (var renderer in coopRenderers)
                        if (renderer != null && CoopScreenSpace(renderer, renderer.GetEntityId().GetHashCode())) coopOverlayRenderers.Add(renderer);
            }
            double now = NetworkTime.localTime, previous = coopPreviousFrameTime;
            foreach (var renderer in coopOverlayRenderers)
            {
                if (renderer == null) continue;
                int id = renderer.GetEntityId().GetHashCode();
                if (!renderer.gameObject.activeInHierarchy) { coopPreviousPose.Remove(id); continue; }
                Vector3 position = CoopBasePosition(renderer, out _, out _, out _);
                bool hadPose = coopPreviousPose.TryGetValue(id, out var before);
                coopPreviousPose[id] = position;
                if (!hadPose || (position - before).sqrMagnitude <= CoopRepositionJump * CoopRepositionJump) continue;
                if (!renderer.enabled || renderer.forceRenderingOff || !coopLastSprites.TryGetValue(id, out var approved) || !approved.visible) continue;
                // The approved trajectory reaches `before` at the previous frame and continues from `position`;
                // the client holds the end of the old segment and starts the new one without crossing the screen.
                coopRepositionAnchors.Add(new CoopMotionEntry { id = id, position = before });
                coopRepositions.Add(new CoopMotionEntry { id = id, position = position });
                coopRepositionIds.Add(id);
                if (shapeDiagnostics) RecordCoopRepositionMetric(renderer, id, approved, before, previous, position, now);
            }
        }

        void PublishCoopMotion(bool shapeDiagnostics)
        {
            double now = NetworkTime.localTime, previous = coopPreviousFrameTime;
            coopPreviousFrameTime = now;
            if (CoopR02Legacy) return;
            using var scope = CoopMotionMarker.Auto();
            coopMotionEntries.Clear(); coopMotionAnchors.Clear();
            var mode = CoopMotionModeSetting;
            double heartbeat = mode switch { CoopMotionMode.Half => 1.8 / 60, CoopMotionMode.Segments => (CoopSegmentHeartbeatFrames - .1) / 60, _ => CoopMotionMinInterval };
            foreach (var renderer in coopRenderers)
            {
                if (renderer == null) continue;
                int id = renderer.GetEntityId().GetHashCode();
                if (!renderer.gameObject.activeInHierarchy) { coopPreviousPose.Remove(id); coopMovedLastFrame.Remove(id); continue; }
                if (CoopScreenSpace(renderer, id)) continue;
                long metricStarted = shapeDiagnostics ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
                Vector3 position = CoopBasePosition(renderer, out _, out _, out _);
                bool hadPose = coopPreviousPose.TryGetValue(id, out var before);
                coopPreviousPose[id] = position;
                bool moved = hadPose && before != position;
                // Rest-based anchor: the previous frame did not move (or the stream never sent), so the approved
                // segment starts at the rest pose of the previous frame instead of at an older sample.
                bool restStart = !coopMovedLastFrame.Contains(id);
                if (moved) coopMovedLastFrame.Add(id); else coopMovedLastFrame.Remove(id);
                if (!moved || !coopLastSprites.TryGetValue(id, out var approved) || !approved.visible) continue;
                bool hasSent = coopLastSentTime.TryGetValue(id, out double sent);
                if (!coopSegments.TryGetValue(id, out var segment)) coopSegments[id] = segment = new CoopSegmentState();
                // A full frame published this server frame already carries the pose. Rotation, scale or
                // visibility changes wait for the next full frame so a position-only sample never mixes them.
                if (hasSent && sent >= now) { segment.Reset(now, position); continue; }
                if (!renderer.enabled || renderer.forceRenderingOff ||
                    renderer.transform.rotation != approved.rotation || renderer.transform.lossyScale != approved.scale) continue;
                bool anchor = !hasSent || (restStart && sent < previous);
                bool entry = anchor || now - sent >= heartbeat;
                if (!anchor && mode == CoopMotionMode.Segments && segment.Deviates(now, position) && previous > segment.startTime)
                {
                    // Turn vertex: the straight segment ended at the previous frame's approved pose.
                    anchor = true; segment.Reset(previous, before);
                }
                if (!anchor && !entry)
                {
                    segment.Add(now, position);
                    continue;
                }
                if (anchor) coopMotionAnchors.Add(new CoopMotionEntry { id = id, position = before });
                if (entry)
                {
                    coopMotionEntries.Add(new CoopMotionEntry { id = id, position = position });
                    coopLastSentTime[id] = now; segment.Reset(now, position);
                }
                else
                {
                    coopLastSentTime[id] = previous; segment.Add(now, position);
                }
                if (shapeDiagnostics) RecordCoopMotionMetric(renderer, id, approved, anchor, before, previous, entry, position, now, metricStarted);
            }
            if (coopMotionEntries.Count == 0 && coopMotionAnchors.Count == 0 && coopRepositions.Count == 0) return;
            const int batch = 128;
            for (int start = 0; start == 0 || start < coopMotionEntries.Count; start += batch)
            {
                int count = Mathf.Min(batch, coopMotionEntries.Count - start);
                var entries = count <= 0 ? System.Array.Empty<CoopMotionEntry>() : new CoopMotionEntry[count];
                if (count > 0) coopMotionEntries.CopyTo(start, entries, 0, count);
                // Anchors and repositions precede every entry of the frame; send them once, with the first chunk.
                bool first = start == 0;
                var message = new CoopMotion
                {
                    scene = coopScene, time = now, anchorTime = previous, entries = entries,
                    anchors = first && coopMotionAnchors.Count > 0 ? coopMotionAnchors.ToArray() : System.Array.Empty<CoopMotionEntry>(),
                    repositionAnchors = first && coopRepositions.Count > 0 ? coopRepositionAnchors.ToArray() : System.Array.Empty<CoopMotionEntry>(),
                    repositions = first && coopRepositions.Count > 0 ? coopRepositions.ToArray() : System.Array.Empty<CoopMotionEntry>()
                };
                foreach (var peer in coopPeerBuffer) if (peer != null && peer.isReady) peer.Send(message);
            }
        }

        // Camera-attached overlays (sandstorm grains, snowfall, bat swarms, fish schools) are screen-space ambience that
        // follows the server camera. They keep the 20 Hz full-frame stream (no per-frame samples, which would cost about
        // 145 KiB/s per peer in Stage 3-4) plus an explicit reposition when they jump, so a wrap never sweeps the screen.
        bool CoopScreenSpace(SpriteRenderer renderer, int id)
        {
            if (!coopMotionScreenSpace.TryGetValue(id, out bool screenSpace))
                coopMotionScreenSpace[id] = screenSpace = renderer.GetComponentInParent<Camera>(true) != null;
            return screenSpace;
        }

        void ForgetCoopMotion(int id)
        {
            coopLastSentTime.Remove(id); coopPreviousPose.Remove(id); coopMotionScreenSpace.Remove(id);
            coopMovedLastFrame.Remove(id); coopSegments.Remove(id);
        }

        void ClearCoopMotion()
        {
            coopLastSentTime.Clear(); coopPreviousPose.Clear(); coopMotionScreenSpace.Clear(); coopMotionEntries.Clear(); coopMotionAnchors.Clear();
            coopMovedLastFrame.Clear(); coopSegments.Clear(); coopRepositionAnchors.Clear(); coopRepositions.Clear(); coopRepositionIds.Clear();
            coopOverlayRenderers.Clear(); coopOverlaySource = null;
            coopPreviousFrameTime = double.NegativeInfinity;
        }

        void ApplyCoopMotion(CoopMotion message)
        {
            if (!CanApplyCoop(message.scene)) return;
            AppendCoopMotion(message.anchors, message.anchorTime);
            AppendCoopMotion(message.entries, message.time);
            ApplyCoopRepositions(message.repositionAnchors, message.repositions, message.anchorTime, message.time);
        }

        void AppendCoopMotion(CoopMotionEntry[] entries, double time)
        {
            if (entries == null) return;
            bool diagnostics = CoopShapeDiagnostics;
            foreach (var entry in entries)
            {
                // Only continue an approved visible stream: never create a view, revive a removed one or reorder samples.
                if (coopVisualRemovals.ContainsKey(entry.id) || !coopVisualSamples.TryGetValue(entry.id, out var samples) || samples.Count == 0) continue;
                var latest = samples[^1];
                if (!latest.frame.visible || time <= latest.frame.time) continue;
                var sample = latest.At(time, entry.position);
                if (diagnostics) RecordCoopArrival(entry.id, samples, sample);
                samples.Add(sample);
                if (samples.Count > 64) samples.RemoveAt(0);
            }
        }

        void ApplyCoopRepositions(CoopMotionEntry[] anchors, CoopMotionEntry[] repositions, double anchorTime, double time)
        {
            if (repositions == null) return;
            for (int i = 0; i < repositions.Length; i++)
            {
                var entry = repositions[i];
                if (coopVisualRemovals.ContainsKey(entry.id) || !coopVisualSamples.TryGetValue(entry.id, out var samples) || samples.Count == 0) continue;
                var latest = samples[^1];
                if (!latest.frame.visible) continue;
                bool hasAnchor = anchors != null && i < anchors.Length && anchors[i].id == entry.id;
                if (latest.frame.time == time)
                {
                    // The full frame of this server frame arrived first (same reliable channel, earlier message):
                    // mark it as the explicit reposition and give the old segment its approved end.
                    latest.cut = true; samples[^1] = latest;
                    // The anchor keeps the appearance of the old segment (the sample before the full frame).
                    var end = samples.Count > 1 ? samples[^2] : latest;
                    double endTime = samples.Count > 1 ? end.frame.time : double.NegativeInfinity;
                    if (hasAnchor && anchorTime > endTime && anchorTime < time && end.frame.visible)
                        samples.Insert(samples.Count - 1, end.At(anchorTime, anchors[i].position));
                }
                else if (time > latest.frame.time)
                {
                    if (hasAnchor && anchorTime > latest.frame.time) samples.Add(latest.At(anchorTime, anchors[i].position));
                    samples.Add(latest.At(time, entry.position, true));
                }
                while (samples.Count > 64) samples.RemoveAt(0);
            }
        }

        // Align world props/supports with the approved player transform timeline.
        // Sampling on packet time while the rider uses NT's offset makes the
        // platform appear ahead of its passenger even when both are smooth.
        public static double CoopPresentationTime => CoopVisualClock;
        public static bool TryPresentCoopCarrier(int id, Vector3 approvedPosition, out Vector3 position)
        {
            position = default;
            if (NetworkServer.active || !IsCoopStage || singleton is not BombermanNetworkManager manager ||
                !manager.coopViews.TryGetValue(id, out var renderer) || renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) return false;
            renderer.transform.position = approvedPosition;
            position = renderer.transform.position; return true;
        }

        static double CoopVisualClock => NetworkTime.time - TransformVisualOffset(NetworkClient.localPlayer);

        void PresentOnlineSprites()
        {
            if (!NetworkClient.active || NetworkServer.active) return;
            double clock = CoopVisualClock;
            bool presentDiagnostics = CoopShapeDiagnostics;
            if (coopCatalog != null && coopViewRoot != null)
            {
                coopExpiredViews.Clear();
                foreach (var pair in coopVisualSamples)
                {
                    int id = pair.Key;
                    clock = CoopCarrierClock(id, CoopVisualClock);
                    if (coopVisualRemovals.TryGetValue(id, out double removed) && clock >= removed)
                    {
                        ReleaseCoopMaskMaterial(id, null);
                        if (coopViews.Remove(id, out var view) && view != null) Destroy(view.gameObject);
                        coopViewSpriteIds.Remove(id); coopViewClips.Remove(id);
                        coopExpiredViews.Add(id); continue;
                    }
                    var samples = pair.Value;
                    long presentStarted = presentDiagnostics ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
                    // Keep the already displayed enemy pose/frame in world space.
                    // Buffered pre-pause samples must not advance or rewind it.
                    if (GamePauseController.IsPaused && samples.Count > 0 && (samples[^1].frame.enemyStepLimit > 0 || samples[^1].frame.pauseWithGameplay) && coopViews.ContainsKey(id))
                    {
                        if (presentDiagnostics) CoopPresentStat(id).pausedHeld++;
                        continue;
                    }
                    while (samples.Count > 1 && samples[1].frame.time <= clock) samples.RemoveAt(0);
                    if (samples.Count == 0 || samples[0].frame.time > clock) continue;
                    var current = samples[0];
                    var frame = current.frame;
                    bool interpolated = false;
                    double stateTime = frame.time;
                    if (samples.Count > 1 && CoopVisualSample.Interpolates(current, samples[1]))
                    {
                        var next = samples[1].frame;
                        float t = Mathf.Clamp01((float)((clock - frame.time) / (next.time - frame.time)));
                        frame.position = Vector3.Lerp(frame.position, next.position, t);
                        frame.rotation = Quaternion.Slerp(frame.rotation, next.rotation, t);
                        frame.scale = Vector3.Lerp(frame.scale, next.scale, t);
                        interpolated = true; stateTime = clock;
                    }
                    PresentCoopSprite(frame, current.playbackOrigin);
                    if (presentStarted != 0) RecordCoopPresented(id, frame, interpolated, stateTime, clock, presentStarted);
                }
                foreach (int id in coopExpiredViews) { coopVisualSamples.Remove(id); coopVisualRemovals.Remove(id); }
                if (presentDiagnostics) FlushCoopPresentStats();
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
        readonly Dictionary<int, float> coopEnemyVisualLimits = new();
        readonly Dictionary<int, bool> coopPhysicsPause = new();
        bool CoopPauseWithGameplay(SpriteRenderer renderer)
        {
            int id = renderer.GetEntityId().GetHashCode();
            if (!coopPhysicsPause.TryGetValue(id, out bool paused))
                coopPhysicsPause[id] = paused = renderer.GetComponentInParent<Rigidbody2D>(true) != null || renderer.GetComponentInParent<BattleMode9MinecartController>(true) != null ||
                    renderer.GetComponentInParent<global::StageAssets.DestructibleRocketController>(true) != null;
            return paused;
        }

        static float EnemyVisualLimit(EnemyMovementController actor) =>
            actor != null ? Mathf.Max(.01f, actor.tileSize) * 1.5f : 0;

        float CoopEnemyVisualLimit(SpriteRenderer renderer) => coopEnemyVisualLimits.TryGetValue(renderer.GetEntityId().GetHashCode(), out float limit)
            ? limit : EnemyVisualLimit(renderer.GetComponentInParent<EnemyMovementController>(true));

        public static void CoopTrackAnimation(AnimatedSpriteRenderer animation)
        {
            if (NetworkServer.active && animation != null && animation.TryGetComponent<SpriteRenderer>(out var renderer)) CoopTrackRenderer(renderer);
        }

        // Plain projectile/particle renderers have no AnimatedSpriteRenderer.OnEnable
        // hook. Register once after configuring them, before the discovery interval.
        public static void CoopTrackRenderer(SpriteRenderer renderer, EnemyMovementController motionOwner = null)
        {
            if (!NetworkServer.active || !IsCoopStage || singleton is not BombermanNetworkManager manager) return;
            if (!manager.TrackCoopRenderer(renderer)) return;
            float limit = EnemyVisualLimit(motionOwner);
            if (limit > 0) manager.coopEnemyVisualLimits[renderer.GetEntityId().GetHashCode()] = limit;
        }

        bool TrackCoopRenderer(SpriteRenderer renderer)
        {
            if (renderer == null || coopCatalog == null || renderer.gameObject.scene.name != coopScene || HasNetworkParent(renderer.transform)) return false;
            coopPendingRenderers.Add(renderer);
            return true;
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

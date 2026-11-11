using Mirror;
using UnityEngine;

namespace Assets.Scripts.Netcode
{
    // Observes native renderers only. Never moves a gameplay body or runs a contact.
    [DisallowMultipleComponent]
    public sealed class CoopProjectileVisual : MonoBehaviour
    {
        internal SpriteRenderer Renderer { get; private set; }
        internal string Family { get; private set; }

        internal void Bind(SpriteRenderer renderer, string family)
        {
            Renderer = renderer;
            Family = family;
            if (isActiveAndEnabled) BombermanNetworkManager.CoopRegisterProjectile(this);
        }

        void OnEnable()
        {
            if (Renderer != null) BombermanNetworkManager.CoopRegisterProjectile(this);
        }

        void OnDisable() => BombermanNetworkManager.CoopEndProjectile(this);
    }

    public partial class BombermanNetworkManager
    {
        readonly System.Collections.Generic.Dictionary<int, CoopProjectileVisual> coopProjectiles = new();
        readonly System.Collections.Generic.List<CoopSpriteFrame> coopProjectileEdges = new();
        readonly System.Collections.Generic.List<int> coopDeadProjectiles = new();
        readonly System.Collections.Generic.HashSet<SpriteRenderer> coopEarlyEnemyRenderers = new();

        static int coopR05Mode;
        internal static bool CoopR05Legacy
        {
            get
            {
                if (coopR05Mode == 0) coopR05Mode = Debug.isDebugBuild &&
                    System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-coop-r05-legacy") >= 0 ? 1 : 2;
                return coopR05Mode == 1;
            }
        }

        public static void CoopTrackProjectile(SpriteRenderer renderer, string family)
        {
#if UNITY_EDITOR || DEBUG
            LocalProjectileSmoke.Track(renderer, family);
#endif
            CoopTrackRenderer(renderer);
            if (!NetworkServer.active || renderer == null || CoopR05Legacy) return;
            if (singleton is not BombermanNetworkManager manager || manager == null || !IsCoopStage ||
                !manager.TrackCoopRenderer(renderer)) return;
            if (!renderer.TryGetComponent<CoopProjectileVisual>(out var visual))
                visual = renderer.gameObject.AddComponent<CoopProjectileVisual>();
            visual.Bind(renderer, family);
        }

        internal static void CoopRegisterProjectile(CoopProjectileVisual visual)
        {
            if (singleton is not BombermanNetworkManager manager || manager == null ||
                !NetworkServer.active || !IsCoopStage || NetworkServer.isLoadingScene || visual == null ||
                !manager.TrackCoopRenderer(visual.Renderer)) return;
            int id = visual.Renderer.GetEntityId().GetHashCode();
            manager.coopProjectiles[id] = visual;
            manager.coopPhysicsPause[id] = true;
            manager.coopMetricFamilies.Remove(id);
            // Registration can precede the next world discovery/capture. Preserve even a
            // projectile that is born and removed between two 20 Hz snapshots.
            manager.PublishCoopProjectileFrame(visual.Renderer);
        }

        internal static void CoopEndProjectile(CoopProjectileVisual visual)
        {
            if (singleton is not BombermanNetworkManager manager || manager == null ||
                !NetworkServer.active || !IsCoopStage || NetworkServer.isLoadingScene ||
                visual == null || visual.Renderer == null || manager.coopCatalog == null ||
                visual.gameObject.scene.name != manager.coopScene) return;
            int id = visual.Renderer.GetEntityId().GetHashCode();
            if (!manager.coopProjectiles.ContainsKey(id)) return;
            manager.PublishCoopProjectileEnd(visual.Renderer);
            manager.coopProjectiles.Remove(id);
        }

        internal static AnimatedSpriteRenderer.PlaybackState CaptureCoopProjectilePlayback(AnimatedSpriteRenderer source)
        {
            var state = CaptureCoopPlayback(source);
            // Every approved native sprite edge is streamed. Do not invent an
            // intermediate automatic frame when the authority skipped it in a hitch.
            state.speed = 0;
            return state;
        }

        void PublishCoopProjectileEnd(SpriteRenderer renderer)
        {
            int id = renderer.GetEntityId().GetHashCode();
            var final = CaptureCoopFrame(renderer);
            if (coopLastSprites.TryGetValue(id, out var last))
                final.time = System.Math.Max(final.time, last.time + .000001);
            // Reach the final native position, then hide. Keep a pooled renderer's identity:
            // re-enabling it adds a later visible edge, without resurrecting gameplay.
            if (coopLastSprites.TryGetValue(id, out var previous) && previous.visible &&
                final.time - previous.time > .000002)
            {
                var endpoint = final;
                endpoint.time -= .000001;
                endpoint.visible = true;
                SendCoopProjectileFrame(endpoint);
            }
            final.visible = false;
            SendCoopProjectileFrame(final);
        }

        void PublishCoopProjectileFrame(SpriteRenderer renderer)
        {
            long started = GameplayLatencyDiagnostics.Enabled ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
            var frame = CaptureCoopFrame(renderer);
            if (coopLastSprites.TryGetValue(frame.id, out var previous) && frame.time < previous.time) return;
            RecordCoopPresentationMetric(renderer, frame, true, started);
            SendCoopProjectileFrame(frame);
        }

        void SendCoopProjectileFrame(CoopSpriteFrame frame)
        {
            coopProjectileEdges.Clear(); coopProjectileEdges.Add(frame);
            coopLastSprites[frame.id] = frame;
            coopLastSentTime[frame.id] = frame.time;
            if (CoopShapeDiagnostics) CoopShapePendingList(frame.id).Add(CoopVisualSample.Full(frame));
            foreach (var peer in NetworkServer.connections.Values)
                if (peer != null && peer != NetworkServer.localConnection && peer.isReady &&
                    peer.isAuthenticated && coopInitializedPeers.Contains(peer.connectionId))
                    SendCoopSprites(peer, coopProjectileEdges, System.Array.Empty<int>());
        }

        internal static bool NeedsCoopProjectileEdge(SpriteRenderer renderer, in CoopSpriteFrame previous)
        {
            bool visible = renderer.enabled && renderer.gameObject.activeInHierarchy && !renderer.forceRenderingOff;
            return visible != previous.visible || (visible &&
                (renderer.transform.rotation != previous.rotation || renderer.transform.lossyScale != previous.scale ||
                 renderer.color != previous.color || renderer.flipX != previous.flipX || renderer.flipY != previous.flipY));
        }

        void MergeCoopPendingRenderers()
        {
            if (coopPendingRenderers.Count == 0) return;
            var tracked = new System.Collections.Generic.HashSet<SpriteRenderer>(coopRenderers);
            foreach (var renderer in coopPendingRenderers)
                if (renderer != null && renderer.gameObject.scene.name == coopScene)
                {
                    tracked.Add(renderer);
                    if (CoopEnemyVisualLimit(renderer) > 0) coopEarlyEnemyRenderers.Add(renderer);
                }
            coopRenderers = new SpriteRenderer[tracked.Count]; tracked.CopyTo(coopRenderers);
            coopPendingRenderers.Clear();
        }

        void PublishCoopProjectileEdges()
        {
            coopDeadProjectiles.Clear();
            foreach (var pair in coopProjectiles)
            {
                var visual = pair.Value;
                if (visual == null || visual.Renderer == null) { coopDeadProjectiles.Add(pair.Key); continue; }
                var renderer = visual.Renderer;
                if (!coopLastSprites.TryGetValue(pair.Key, out var previous)) { PublishCoopProjectileFrame(renderer); continue; }
                var sprite = renderer.sprite;
                int spriteId = sprite == null ? -1 :
                    (coopSpriteIds.TryGetValue(sprite, out int known) || coopDynamicIds.TryGetValue(sprite, out known) ? known : -2);
                if (spriteId != previous.sprite || NeedsCoopProjectileEdge(renderer, previous))
                    PublishCoopProjectileFrame(renderer);
            }
            foreach (int id in coopDeadProjectiles) coopProjectiles.Remove(id);
        }
    }
}

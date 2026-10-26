using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Assets.Scripts.Netcode
{
    public struct CoopCollisions : NetworkMessage
    {
        public string scene;
        public bool reset, complete;
        public CoopColliderFrame[] frames;
        public int[] removed;
    }

    public partial class BombermanNetworkManager
    {
        readonly Dictionary<string, CoopTileCollisionView> coopCollisionMaps = new();
        readonly HashSet<string> coopCollisionMapsAwaiting = new();
        readonly Dictionary<int, CoopColliderView> coopCollisionViews = new();
        readonly HashSet<int> coopUnsupportedCollisions = new();
        readonly Dictionary<int, CoopColliderFrame> coopLastCollisions = new();
        readonly HashSet<int> coopCurrentCollisions = new();
        readonly List<CoopColliderFrame> coopChangedCollisions = new();
        readonly List<int> coopRemovedCollisions = new();
        readonly HashSet<Tilemap> coopDirtyMaps = new();
        readonly Dictionary<string, CoopMapCollisionPose> coopMapCollisionPoses = new();
        Collider2D[] coopCollisionSources = Array.Empty<Collider2D>();
        float coopNextCollisionDiscover;
        bool coopCollisionMapsSupported, coopCollisionObjectsReceived, coopCollisionReceivingFull;
        bool coopLastGeometryReady;

        struct CoopMapCollisionPose
        {
            public Vector3 position, scale;
            public Quaternion rotation;
            public Vector2 offset;
            public bool enabled;
            public bool Matches(CoopMapCollisionPose other) => position == other.position && scale == other.scale &&
                rotation == other.rotation && offset == other.offset && enabled == other.enabled;
        }

        bool CoopGeometryStateReady
        {
            get
            {
                if (!coopCollisionMapsSupported || !coopCollisionObjectsReceived || coopCollisionMapsAwaiting.Count > 0 || coopUnsupportedCollisions.Count > 0) return false;
                foreach (var view in coopCollisionMaps.Values) if (!view.Ready) return false;
                return true;
            }
        }
        public static bool ClientPredictionGeometryReady => NetSync.Mode != NetSync.NetMode.Client || !IsCoopStage ||
            (singleton is BombermanNetworkManager manager && manager.CoopGeometryStateReady);

        static CoopMapCollisionPose CaptureMapCollisionPose(Tilemap map)
        {
            var collider = map.GetComponent<TilemapCollider2D>();
            return new CoopMapCollisionPose { position = map.transform.position, rotation = map.transform.rotation, scale = map.transform.lossyScale,
                offset = collider != null ? collider.offset : Vector2.zero,
                enabled = collider != null && collider.enabled && !collider.isTrigger && map.gameObject.activeInHierarchy &&
                    map.gameObject.layer == LayerMask.NameToLayer("Stage") && (collider.attachedRigidbody == null || collider.attachedRigidbody.simulated) };
        }

        bool CoopMapCollisionPoseChanged(string path, Tilemap map)
            => !coopMapCollisionPoses.TryGetValue(path, out var old) || !CaptureMapCollisionPose(map).Matches(old);

        void CreateCoopCollisionMaps()
        {
            coopCollisionMapsSupported = true;
            foreach (var pair in coopMaps)
            {
                var map = pair.Value;
                if (map == null || map.gameObject.layer != LayerMask.NameToLayer("Stage") || !map.TryGetComponent<TilemapCollider2D>(out _)) continue;
                if (!CoopTileCollisionView.TryCreate(map, coopViewRoot.transform, out var view)) { coopCollisionMapsSupported = false; continue; }
                coopCollisionMaps[pair.Key] = view; coopCollisionMapsAwaiting.Add(pair.Key);
            }
        }

        void ClearCoopCollisionWorld()
        {
            Tilemap.tilemapTileChanged -= OnCoopTileChanged;
            foreach (var view in coopCollisionMaps.Values) view.Dispose();
            foreach (var view in coopCollisionViews.Values) view.Dispose();
            coopCollisionMaps.Clear(); coopCollisionMapsAwaiting.Clear(); coopCollisionViews.Clear(); coopUnsupportedCollisions.Clear();
            coopLastCollisions.Clear(); coopCurrentCollisions.Clear(); coopChangedCollisions.Clear(); coopRemovedCollisions.Clear();
            coopDirtyMaps.Clear(); coopMapCollisionPoses.Clear(); coopCollisionSources = Array.Empty<Collider2D>();
            coopNextCollisionDiscover = 0; coopCollisionMapsSupported = coopCollisionObjectsReceived = coopCollisionReceivingFull = coopLastGeometryReady = false;
        }

        void OnCoopTileChanged(Tilemap map, Tilemap.SyncTile[] changes)
        {
            if (NetworkServer.active && IsCoopStage && map != null && map.gameObject.scene.name == coopScene && changes != null && changes.Length > 0)
                coopDirtyMaps.Add(map);
        }

        void CaptureCoopCollisionState()
        {
            if (Time.unscaledTime >= coopNextCollisionDiscover)
            {
                coopNextCollisionDiscover = Time.unscaledTime + 0.25f;
                var sources = new List<Collider2D>();
                foreach (var collider in FindObjectsByType<Collider2D>(FindObjectsInactive.Include))
                    if (collider.gameObject.scene.name == coopScene && collider.gameObject.layer == LayerMask.NameToLayer("Stage") &&
                        collider is not TilemapCollider2D && !HasNetworkParent(collider.transform)) sources.Add(collider);
                coopCollisionSources = sources.ToArray();
            }
            coopCurrentCollisions.Clear(); coopChangedCollisions.Clear(); coopRemovedCollisions.Clear();
            foreach (var collider in coopCollisionSources)
            {
                if (collider == null || collider.gameObject.layer != LayerMask.NameToLayer("Stage")) continue;
                var frame = CoopColliderFrame.Capture(collider); coopCurrentCollisions.Add(frame.id);
                bool known = coopLastCollisions.TryGetValue(frame.id, out var previous);
                if (!frame.enabled && !known) continue;
                if (!known || !frame.Matches(previous)) coopChangedCollisions.Add(frame);
                coopLastCollisions[frame.id] = frame;
            }
            foreach (int id in coopLastCollisions.Keys) if (!coopCurrentCollisions.Contains(id)) coopRemovedCollisions.Add(id);
            foreach (int id in coopRemovedCollisions) coopLastCollisions.Remove(id);
        }

        void SendCoopCollisionFrames(NetworkConnectionToClient peer, List<CoopColliderFrame> frames, int[] removed, bool reset)
        {
            const int batch = 48;
            for (int start = 0; start < Mathf.Max(1, frames.Count); start += batch)
            {
                int count = Mathf.Min(batch, frames.Count - start);
                peer.Send(new CoopCollisions { scene = coopScene, reset = reset && start == 0, complete = start + count >= frames.Count,
                    frames = count > 0 ? frames.GetRange(start, count).ToArray() : Array.Empty<CoopColliderFrame>(),
                    removed = start == 0 ? removed : Array.Empty<int>() });
            }
        }

        void ApplyCoopCollisions(CoopCollisions message)
        {
            if (!CanApplyCoop(message.scene)) return;
            ApplyCoopCollisionData(message);
        }

        void ApplyCoopCollisionData(CoopCollisions message)
        {
            if (message.reset)
            {
                foreach (var view in coopCollisionViews.Values) view.Dispose();
                coopCollisionViews.Clear(); coopUnsupportedCollisions.Clear(); coopCollisionObjectsReceived = false; coopCollisionReceivingFull = true;
            }
            foreach (var frame in message.frames ?? Array.Empty<CoopColliderFrame>())
            {
                if (frame.enabled && !frame.Supported) coopUnsupportedCollisions.Add(frame.id); else coopUnsupportedCollisions.Remove(frame.id);
                if (!frame.Supported)
                {
                    if (coopCollisionViews.Remove(frame.id, out var unsupported)) unsupported.Dispose();
                    continue;
                }
                if (coopCollisionViews.TryGetValue(frame.id, out var view) && view.Kind != frame.kind)
                { view.Dispose(); coopCollisionViews.Remove(frame.id); view = null; }
                if (view == null) coopCollisionViews[frame.id] = new CoopColliderView(frame, coopViewRoot.transform);
                else view.Apply(frame);
            }
            foreach (int id in message.removed ?? Array.Empty<int>())
            {
                if (coopCollisionViews.Remove(id, out var view)) view.Dispose();
                coopUnsupportedCollisions.Remove(id);
            }
            if (message.complete && coopCollisionReceivingFull) { coopCollisionObjectsReceived = true; coopCollisionReceivingFull = false; }
            Physics2D.SyncTransforms();
            TraceCoopGeometryReadiness();
        }

        void ApplyCoopCollisionTiles(CoopTiles message)
        {
            if (!coopCollisionMaps.TryGetValue(message.path, out var view)) return;
            if (message.reset) { view.Clear(); coopCollisionMapsAwaiting.Add(message.path); }
            view.SyncPose(message.collisionEnabled, message.collisionOffset);
            foreach (var cell in message.cells ?? Array.Empty<CoopTileFrame>())
            {
                var type = cell.tile < 0 ? Tile.ColliderType.None : (Tile.ColliderType)cell.collisionType;
                view.SetCell(cell.cell, type, type == Tile.ColliderType.Sprite && cell.collisionSprite >= 0 ? CoopSpriteAt(cell.collisionSprite) : null, cell.matrix);
            }
            view.Refresh();
            if (message.complete) coopCollisionMapsAwaiting.Remove(message.path);
            Physics2D.SyncTransforms();
            TraceCoopGeometryReadiness();
        }

        public static void RefreshApprovedCoopCollisionCell(Tilemap map, Vector3Int cell)
        {
            if (NetSync.Mode != NetSync.NetMode.Client || !IsCoopStage || map == null || singleton is not BombermanNetworkManager manager ||
                !manager.coopCollisionMaps.TryGetValue(CoopPath(map.transform), out var view)) return;
            var type = map.GetColliderType(cell);
            if (type == Tile.ColliderType.Sprite && map.GetSprite(cell) == null) type = Tile.ColliderType.None;
            view.SetCell(cell, type, type == Tile.ColliderType.Sprite ? map.GetSprite(cell) : null, map.GetTransformMatrix(cell));
            view.Refresh(); Physics2D.SyncTransforms();
        }

        void TraceCoopGeometryReadiness()
        {
            bool ready = ClientPredictionGeometryReady;
            if (ready == coopLastGeometryReady) return;
            coopLastGeometryReady = ready;
            if (GameplayLatencyDiagnostics.Enabled)
                GameplayLatencyDiagnostics.Write(GameplayLatencyDiagnostics.For("coop_geometry_ready", detail: "ready=" + ready +
                    ";maps_pending=" + coopCollisionMapsAwaiting.Count + ";unsupported=" + coopUnsupportedCollisions.Count));
        }
    }
}

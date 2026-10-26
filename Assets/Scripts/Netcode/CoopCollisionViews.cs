using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Assets.Scripts.Netcode
{
    // Geometry views contain native query shapes only. Never clone scene scripts
    // or original TileBase instances: tile Startup can instantiate gameplay.
    public sealed class CoopTileCollisionView : IDisposable
    {
        readonly Tilemap source;
        readonly Grid sourceGrid;
        readonly GameObject root;
        readonly Dictionary<(Sprite, Tile.ColliderType), Tile> tiles = new();
        readonly HashSet<Vector3Int> unresolvedCells = new();
        public bool Ready => unresolvedCells.Count == 0;
        public Tilemap Map { get; }
        public TilemapCollider2D Collider { get; }

        CoopTileCollisionView(Tilemap source, Grid sourceGrid, Transform parent)
        {
            this.source = source; this.sourceGrid = sourceGrid;
            root = new GameObject("ClientCollisionGrid:" + source.name, typeof(Grid)) { hideFlags = HideFlags.DontSave };
            root.transform.SetParent(parent, false);
            var shape = new GameObject("CollisionShape", typeof(Tilemap), typeof(TilemapCollider2D));
            shape.transform.SetParent(root.transform, false); shape.layer = source.gameObject.layer; shape.tag = source.gameObject.tag;
            Map = shape.GetComponent<Tilemap>(); Collider = shape.GetComponent<TilemapCollider2D>(); Collider.enabled = false;
            Map.tileAnchor = source.tileAnchor; Map.orientation = source.orientation; Map.orientationMatrix = source.orientationMatrix;
            SyncPose(false, Vector2.zero);
        }

        public static bool TryCreate(Tilemap source, Transform parent, out CoopTileCollisionView view)
        {
            view = null;
            if (source == null || !source.TryGetComponent<TilemapCollider2D>(out var collider) ||
                collider.compositeOperation != Collider2D.CompositeOperation.None || source.layoutGrid == null ||
                !source.layoutGrid.TryGetComponent<Grid>(out var grid)) return false;
            view = new CoopTileCollisionView(source, grid, parent); return true;
        }

        public void Clear() { Map.ClearAllTiles(); unresolvedCells.Clear(); }

        public void SetCell(Vector3Int cell, Tile.ColliderType type, Sprite sprite, Matrix4x4 matrix)
        {
            if (type == Tile.ColliderType.Sprite && sprite == null)
            {
                unresolvedCells.Add(cell);
                if (Map.HasTile(cell)) Map.SetTile(cell, null);
                return;
            }
            unresolvedCells.Remove(cell);
            if (type == Tile.ColliderType.None)
            {
                if (Map.HasTile(cell)) Map.SetTile(cell, null);
                return;
            }
            if (type != Tile.ColliderType.Sprite && type != Tile.ColliderType.Grid) return;
            if (type == Tile.ColliderType.Grid) sprite = null;
            var key = (sprite, type);
            if (!tiles.TryGetValue(key, out var tile))
            {
                tile = ScriptableObject.CreateInstance<Tile>(); tile.hideFlags = HideFlags.HideAndDontSave;
                tile.sprite = sprite; tile.colliderType = type; tile.flags = TileFlags.None; tiles.Add(key, tile);
            }
            if (Map.GetTile(cell) != tile) Map.SetTile(cell, tile);
            if (Map.GetTransformMatrix(cell) != matrix) Map.SetTransformMatrix(cell, matrix);
        }

        public void SyncPose(bool enabled, Vector2 offset)
        {
            if (source == null || sourceGrid == null) { Collider.enabled = false; return; }
            var grid = root.GetComponent<Grid>();
            grid.cellSize = sourceGrid.cellSize; grid.cellGap = sourceGrid.cellGap;
            grid.cellLayout = sourceGrid.cellLayout; grid.cellSwizzle = sourceGrid.cellSwizzle;
            root.transform.SetPositionAndRotation(sourceGrid.transform.position, sourceGrid.transform.rotation);
            SetWorldScale(root.transform, sourceGrid.transform.lossyScale);
            Map.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            SetWorldScale(Map.transform, source.transform.lossyScale);
            Collider.offset = offset; Collider.enabled = enabled;
        }

        public void Refresh()
        {
            if (Collider.hasTilemapChanges) Collider.ProcessTilemapChanges();
        }

        public void Dispose()
        {
            Release(root);
            foreach (var tile in tiles.Values) Release(tile);
            tiles.Clear();
        }

        internal static void SetWorldScale(Transform target, Vector3 scale)
        {
            Vector3 parent = target.parent != null ? target.parent.lossyScale : Vector3.one;
            target.localScale = new Vector3(Mathf.Abs(parent.x) > 0.0001f ? scale.x / parent.x : 1,
                Mathf.Abs(parent.y) > 0.0001f ? scale.y / parent.y : 1, Mathf.Abs(parent.z) > 0.0001f ? scale.z / parent.z : 1);
        }

        internal static void Release(UnityEngine.Object value)
        {
            if (value == null) return;
            if (value is GameObject go) go.SetActive(false);
            if (Application.isPlaying) UnityEngine.Object.Destroy(value); else UnityEngine.Object.DestroyImmediate(value);
        }
    }

    public struct CoopColliderFrame
    {
        public int id, layer;
        public string tag;
        public byte kind, direction;
        public bool enabled;
        public Vector3 position, scale;
        public Quaternion rotation;
        public Vector2 size, offset;
        public float radius, edgeRadius;
        public bool Supported => kind >= 1 && kind <= 3;

        public static CoopColliderFrame Capture(Collider2D collider)
        {
            var frame = new CoopColliderFrame { id = collider.GetEntityId().GetHashCode(), layer = collider.gameObject.layer, tag = collider.tag,
                position = collider.transform.position, rotation = collider.transform.rotation, scale = collider.transform.lossyScale,
                offset = collider.offset, enabled = collider.enabled && !collider.isTrigger && collider.gameObject.activeInHierarchy &&
                    (collider.attachedRigidbody == null || collider.attachedRigidbody.simulated) };
            if (Mathf.Abs(frame.scale.x) < 0.0001f || Mathf.Abs(frame.scale.y) < 0.0001f) frame.enabled = false;
            if (collider.compositeOperation != Collider2D.CompositeOperation.None) return frame;
            if (collider is BoxCollider2D box) { frame.kind = 1; frame.size = box.size; frame.edgeRadius = box.edgeRadius; }
            else if (collider is CircleCollider2D circle) { frame.kind = 2; frame.radius = circle.radius; }
            else if (collider is CapsuleCollider2D capsule) { frame.kind = 3; frame.size = capsule.size; frame.direction = (byte)capsule.direction; }
            return frame;
        }

        public bool Matches(CoopColliderFrame other) => enabled == other.enabled && kind == other.kind && layer == other.layer && tag == other.tag &&
            (!enabled || (position == other.position && rotation == other.rotation && scale == other.scale && offset == other.offset &&
                size == other.size && radius == other.radius && edgeRadius == other.edgeRadius && direction == other.direction));
    }

    public sealed class CoopColliderView : IDisposable
    {
        readonly GameObject root;
        public Collider2D Collider { get; }
        public byte Kind { get; }

        public CoopColliderView(CoopColliderFrame frame, Transform parent)
        {
            if (!frame.Supported) throw new ArgumentException("Unsupported client query collider.", nameof(frame));
            Kind = frame.kind;
            root = new GameObject("CollisionShape") { hideFlags = HideFlags.DontSave }; root.transform.SetParent(parent, false);
            Collider = Kind == 1 ? root.AddComponent<BoxCollider2D>() : Kind == 2 ? root.AddComponent<CircleCollider2D>() : root.AddComponent<CapsuleCollider2D>();
            Collider.enabled = false; Apply(frame);
        }

        public void Apply(CoopColliderFrame frame)
        {
            if (!frame.enabled) Collider.enabled = false;
            root.layer = frame.layer; root.tag = string.IsNullOrEmpty(frame.tag) ? "Untagged" : frame.tag;
            root.transform.SetPositionAndRotation(frame.position, frame.rotation); CoopTileCollisionView.SetWorldScale(root.transform, frame.scale);
            Collider.offset = frame.offset;
            if (Collider is BoxCollider2D box) { box.size = frame.size; box.edgeRadius = frame.edgeRadius; }
            else if (Collider is CircleCollider2D circle) circle.radius = frame.radius;
            else if (Collider is CapsuleCollider2D capsule) { capsule.size = frame.size; capsule.direction = (CapsuleDirection2D)frame.direction; }
            Collider.enabled = frame.enabled;
        }

        public void Dispose() => CoopTileCollisionView.Release(root);
    }
}

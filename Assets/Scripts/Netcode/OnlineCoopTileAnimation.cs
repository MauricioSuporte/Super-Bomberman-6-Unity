using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Assets.Scripts.Netcode
{
    public struct CoopTileAnimationFrame { public Vector3Int cell; public int tile, frame; }
    public struct CoopTileAnimation : NetworkMessage
    {
        public string scene, path;
        public uint revision;
        public CoopTileAnimationFrame[] cells;
    }

    public partial class BombermanNetworkManager
    {
        struct CoopTileClock { public AnimatedTile tile; public float started; }
        readonly Dictionary<Tilemap, Dictionary<Vector3Int, CoopTileClock>> coopTileClocks = new();

        static int ApprovedHeadlessTileFrame(AnimatedTile tile, float elapsed)
        {
            int count = tile.m_AnimatedSprites.Length;
            float speed = tile.UseSpecifiedTime && tile.SpecifiedTime > 0f ? count / tile.SpecifiedTime : tile.m_MinSpeed;
            return Mathf.FloorToInt(Mathf.Max(0f, elapsed + tile.m_AnimationStartTime) * Mathf.Max(0f, speed)) % count;
        }

        int ReadApprovedTileFrame(Tilemap map, Vector3Int cell, AnimatedTile tile)
        {
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                return map.GetAnimationFrame(cell);
            // Dedicated players have no renderer to advance Tilemap animation.
            // This presentation clock uses scaled gameplay time and never draws RNG.
            if (!coopTileClocks.TryGetValue(map, out var clocks)) coopTileClocks[map] = clocks = new();
            if (!clocks.TryGetValue(cell, out var clock) || clock.tile != tile)
                clocks[cell] = clock = new CoopTileClock { tile = tile, started = Time.time };
            return ApprovedHeadlessTileFrame(tile, Time.time - clock.started);
        }

        void InvalidateCoopTileClocks(Tilemap map, Tilemap.SyncTile[] changes)
        {
            if (!coopTileClocks.TryGetValue(map, out var clocks)) return;
            foreach (var change in changes)
                if (clocks.TryGetValue(change.position, out var clock) && map.GetTile(change.position) != clock.tile)
                    clocks.Remove(change.position);
        }

        readonly Dictionary<string, List<Vector3Int>> coopAnimatedCells = new();
        readonly Dictionary<string, uint> coopTileAnimationRevisions = new();
        readonly Dictionary<string, uint> coopReceivedTileAnimationRevisions = new();
        readonly Dictionary<Tile, Tile> coopSafeVisualTiles = new();
        readonly List<CoopTileAnimationFrame> coopChangedAnimationFrames = new();

        TileBase CoopSafeVisualTile(TileBase source)
        {
            if (source is not Tile tile || tile.gameObject == null) return source;
            if (!coopSafeVisualTiles.TryGetValue(tile, out var visual))
            {
                // Never install a prefab-bearing Tile on a pure client: native tile
                // startup would instantiate gameplay after scene behaviours were gated.
                visual = ScriptableObject.CreateInstance<Tile>();
                visual.hideFlags = HideFlags.HideAndDontSave;
                visual.sprite = tile.sprite; visual.color = tile.color;
                visual.transform = tile.transform; visual.flags = tile.flags;
                visual.colliderType = Tile.ColliderType.None;
                coopSafeVisualTiles[tile] = visual;
            }
            return visual;
        }

        static void SetCoopTileAnimationFrame(Tilemap map, Vector3Int cell, int frame)
        {
            int count = map.GetAnimationFrameCount(cell);
            if (count <= 0 || frame < 0 || frame >= count) return;
            map.AddTileAnimationFlags(cell, TileAnimationFlags.PauseAnimation);
            map.SetAnimationFrame(cell, frame);
        }

        void CaptureCoopTileAnimations(List<NetworkConnectionToClient> peers)
        {
            foreach (var pair in coopAnimatedCells)
            {
                if (!coopMaps.TryGetValue(pair.Key, out var map) || map == null ||
                    !coopLastTiles.TryGetValue(pair.Key, out var previous)) continue;
                coopChangedAnimationFrames.Clear();
                foreach (var cell in pair.Value)
                {
                    if (!previous.TryGetValue(cell, out var old) || !old.animated) continue;
                    var current = ReadCoopTileFrame(map, cell);
                    if (!current.animated || current.tile != old.tile || current.animationFrame == old.animationFrame) continue;
                    old.animationFrame = current.animationFrame;
                    previous[cell] = old; // Color/matrix/collision changes still need the structural snapshot.
                    coopChangedAnimationFrames.Add(new CoopTileAnimationFrame { cell = cell, tile = current.tile, frame = current.animationFrame });
                }
                const int batch = 48;
                coopTileAnimationRevisions.TryGetValue(pair.Key, out uint revision);
                for (int start = 0; start < coopChangedAnimationFrames.Count; start += batch)
                {
                    int count = Mathf.Min(batch, coopChangedAnimationFrames.Count - start);
                    var cells = new CoopTileAnimationFrame[count]; coopChangedAnimationFrames.CopyTo(start, cells, 0, count);
                    var message = new CoopTileAnimation { scene = coopScene, path = pair.Key, revision = ++revision, cells = cells };
                    foreach (var peer in peers) peer.Send(message);
                }
                coopTileAnimationRevisions[pair.Key] = revision;
            }
        }

        void ApplyCoopTileAnimation(CoopTileAnimation message)
        {
            if (!CanApplyCoop(message.scene) || !coopMaps.TryGetValue(message.path, out var map) || map == null ||
                message.cells == null || message.cells.Length > 48) return;
            coopReceivedTileAnimationRevisions.TryGetValue(message.path, out uint previous);
            if (!NetworkInputHistory.IsNewer(message.revision, previous)) return;
            foreach (var cell in message.cells)
            {
                if (cell.frame < 0 || cell.frame > 65535 || cell.tile < 0 || cell.tile >= coopCatalog.tiles.Length) return;
                if (map.GetTile(cell.cell) == At(coopCatalog.tiles, cell.tile) && cell.frame >= map.GetAnimationFrameCount(cell.cell)) return;
            }
            coopReceivedTileAnimationRevisions[message.path] = message.revision;
            foreach (var cell in message.cells)
                if (map.GetTile(cell.cell) == At(coopCatalog.tiles, cell.tile))
                    SetCoopTileAnimationFrame(map, cell.cell, cell.frame);
            if (GameplayLatencyDiagnostics.Enabled)
            {
                var sample=GameplayLatencyDiagnostics.For("tile_animation_apply",detail:message.path+";cells="+message.cells.Length);
                sample.authoritativeTick=message.revision;GameplayLatencyDiagnostics.Write(sample);
            }
        }

        void ClearCoopTileAnimation()
        {
            coopTileClocks.Clear(); coopAnimatedCells.Clear(); coopTileAnimationRevisions.Clear();
            coopReceivedTileAnimationRevisions.Clear(); coopChangedAnimationFrames.Clear();
            foreach (var tile in coopSafeVisualTiles.Values)
                if (tile != null) { if (Application.isPlaying) Destroy(tile); else DestroyImmediate(tile); }
            coopSafeVisualTiles.Clear();
        }
    }
}

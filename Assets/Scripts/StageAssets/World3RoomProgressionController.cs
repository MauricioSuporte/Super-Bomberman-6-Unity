using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace StageAssets
{
    /// <summary>Inspector-authored room progression for World 3.</summary>
    public sealed class World3RoomProgressionController : MonoBehaviour
    {
        [Serializable]
        private sealed class Room
        {
            public string name = "Room";
            [Tooltip("This single Room Bounds collider defines both player presence and which CoreMechanisms belong to the room.")]
            public Collider2D roomBounds;
            [Tooltip("Optional roots. Leave empty to automatically use every enemy inside Player Area.")]
            public GameObject[] enemyRoots;
            [Header("Release after every core in this room is destroyed")]
            public World3BambooExitBlocker bambooToOpen;
            public World3GateOpenedSequenceController bubbleChipToRelease;
            public GameObject[] objectsToEnable;

            [NonSerialized] public readonly HashSet<Vector3Int> remainingCores = new();
            [NonSerialized] public readonly HashSet<CoreMechanismsDestructible> remainingSceneCores = new();
            [NonSerialized] public bool released;
        }

        [SerializeField] private Tilemap coreTilemap;
        [SerializeField] private Room[] rooms;

        private readonly Dictionary<EnemyMovementController, bool> enemyOriginalStates = new();

        private void Awake()
        {
            ScanRoomCores();
            CacheEnemies();
            RefreshEnemyRooms();
        }

        private void OnEnable()
        {
            CoreMechanismsDestructible.SuppressLegacyAllDestroyedSfx = true;
            CoreMechanismsTileHandler.CoreMechanismDestroyed += HandleCoreDestroyed;
            CoreMechanismsDestructible.CoreMechanismDestroyed += HandleSceneCoreDestroyed;
        }

        private void OnDisable()
        {
            CoreMechanismsDestructible.SuppressLegacyAllDestroyedSfx = false;
            CoreMechanismsTileHandler.CoreMechanismDestroyed -= HandleCoreDestroyed;
            CoreMechanismsDestructible.CoreMechanismDestroyed -= HandleSceneCoreDestroyed;
        }

        private void Update() => RefreshEnemyRooms();

        private void ScanRoomCores()
        {
            if (coreTilemap == null || rooms == null)
                return;

            BoundsInt bounds = coreTilemap.cellBounds;
            foreach (Room room in rooms)
            {
                if (room == null || room.roomBounds == null)
                    continue;

                room.remainingCores.Clear();
                room.remainingSceneCores.Clear();
                foreach (Vector3Int cell in bounds.allPositionsWithin)
                {
                    TileBase tile = coreTilemap.GetTile(cell);
                    if (!IsCore(tile) || !room.roomBounds.OverlapPoint(coreTilemap.GetCellCenterWorld(cell)))
                        continue;

                    room.remainingCores.Add(cell);
                }

            }

            CoreMechanismsDestructible[] sceneCores = FindObjectsByType<CoreMechanismsDestructible>(FindObjectsInactive.Include);
            for (int i = 0; i < sceneCores.Length; i++)
            {
                CoreMechanismsDestructible core = sceneCores[i];
                if (core == null)
                    continue;

                foreach (Room room in rooms)
                {
                    if (room != null && room.roomBounds != null && room.roomBounds.OverlapPoint(core.transform.position))
                        room.remainingSceneCores.Add(core);
                }
            }

        }

        private void HandleCoreDestroyed(Tilemap tilemap, Vector3Int cell)
        {
            if (tilemap != coreTilemap || rooms == null)
                return;

            foreach (Room room in rooms)
            {
                if (room == null || room.released || !room.remainingCores.Remove(cell))
                    continue;

                if (room.remainingCores.Count == 0 && room.remainingSceneCores.Count == 0)
                    Release(room);
            }
        }

        private void HandleSceneCoreDestroyed(CoreMechanismsDestructible core)
        {
            if (core == null || rooms == null)
                return;

            foreach (Room room in rooms)
            {
                if (room == null || room.released || !room.remainingSceneCores.Remove(core))
                    continue;

                if (room.remainingSceneCores.Count == 0 && room.remainingCores.Count == 0)
                    Release(room, core);
            }
        }

        private void Release(Room room, CoreMechanismsDestructible completionCore = null)
        {
            room.released = true;
            completionCore?.PlayRoomCompletionSfx();
            room.bambooToOpen?.BeginOpening();
            room.bubbleChipToRelease?.BeginSequence();

            if (room.objectsToEnable == null)
                return;

            for (int i = 0; i < room.objectsToEnable.Length; i++)
                if (room.objectsToEnable[i] != null)
                    room.objectsToEnable[i].SetActive(true);
        }

        private void CacheEnemies()
        {
            enemyOriginalStates.Clear();
            EnemyMovementController[] enemies = FindObjectsByType<EnemyMovementController>(FindObjectsInactive.Include);
            for (int i = 0; i < enemies.Length; i++)
                if (enemies[i] != null)
                    enemyOriginalStates[enemies[i]] = enemies[i].enabled;
        }

        private void RefreshEnemyRooms()
        {
            if (rooms == null)
                return;

            foreach (Room room in rooms)
            {
                Collider2D bounds = room != null ? room.roomBounds : null;
                if (bounds == null)
                    continue;

                bool roomHasPlayer = HasLivingPlayer(bounds);
                foreach (EnemyMovementController enemy in GetRoomEnemies(room, bounds))
                {
                    if (enemy == null || !enemyOriginalStates.TryGetValue(enemy, out bool originallyEnabled))
                        continue;

                    enemy.enabled = originallyEnabled && roomHasPlayer;
                    if (!roomHasPlayer && enemy.TryGetComponent(out Rigidbody2D body))
                        body.linearVelocity = Vector2.zero;
                }
            }
        }

        private IEnumerable<EnemyMovementController> GetRoomEnemies(Room room, Collider2D bounds)
        {
            if (room.enemyRoots != null && room.enemyRoots.Length > 0)
            {
                for (int i = 0; i < room.enemyRoots.Length; i++)
                {
                    if (room.enemyRoots[i] == null)
                        continue;

                    foreach (EnemyMovementController enemy in room.enemyRoots[i].GetComponentsInChildren<EnemyMovementController>(true))
                        yield return enemy;
                }
                yield break;
            }

            foreach (EnemyMovementController enemy in enemyOriginalStates.Keys)
                if (enemy != null && bounds != null && bounds.OverlapPoint(enemy.transform.position))
                    yield return enemy;
        }

        private static bool HasLivingPlayer(Collider2D area)
        {
            MovementController[] players = FindObjectsByType<MovementController>(FindObjectsInactive.Exclude);
            for (int i = 0; i < players.Length; i++)
            {
                MovementController player = players[i];
                if (player != null && !player.isDead && player.CompareTag("Player") && area.OverlapPoint(player.transform.position))
                    return true;
            }
            return false;
        }

        private static bool IsCore(TileBase tile)
        {
            return tile != null && !string.IsNullOrWhiteSpace(tile.name) &&
                   tile.name.IndexOf("CoreMechanism", StringComparison.OrdinalIgnoreCase) >= 0;
        }

    }
}

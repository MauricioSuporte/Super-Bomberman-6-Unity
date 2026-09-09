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
            [Tooltip("Defines player presence and is the fallback for CoreMechanisms when no core roots are assigned.")]
            public Collider2D roomBounds;
            [Tooltip("Optional roots. Leave empty to automatically use every enemy inside Player Area.")]
            public GameObject[] enemyRoots;
            [Tooltip("Optional roots that own this room's CoreMechanisms. Use these when the room bounds are only for player presence.")]
            public GameObject[] coreRoots;
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
        private bool enemyRoomLifecycleInitialized;

        /// <summary>
        /// Suspends enemy GameObjects from the room being left and wakes those
        /// in the destination room while the transition fade is covering it.
        /// This applies to every scene that uses this room progression setup.
        /// </summary>
        public static void PrepareEnemyRoomsForTransition(Vector2 sourcePosition, Vector2 destinationPosition)
        {
            World3RoomProgressionController controller =
                FindAnyObjectByType<World3RoomProgressionController>();
            controller?.PrepareEnemyRoomsForTransitionInternal(sourcePosition, destinationPosition);
        }

        /// <summary>
        /// Returns the authored room bounds that contain the supplied position.
        /// This is used by airborne bombs so their bounce wrap cannot escape to
        /// a neighboring World 3 room.
        /// </summary>
        public static Collider2D FindRoomBoundsContaining(Vector2 worldPosition)
        {
            World3RoomProgressionController controller =
                FindAnyObjectByType<World3RoomProgressionController>();

            if (controller == null || controller.rooms == null)
                return null;

            for (int i = 0; i < controller.rooms.Length; i++)
            {
                Collider2D bounds = controller.rooms[i] != null
                    ? controller.rooms[i].roomBounds
                    : null;

                if (bounds != null && bounds.OverlapPoint(worldPosition))
                    return bounds;
            }

            return null;
        }

        /// <summary>Returns an authored room bound by its inspector name.</summary>
        public static Collider2D FindRoomBounds(string roomName)
        {
            if (string.IsNullOrWhiteSpace(roomName))
                return null;

            World3RoomProgressionController controller =
                FindAnyObjectByType<World3RoomProgressionController>();

            if (controller == null || controller.rooms == null)
                return null;

            for (int i = 0; i < controller.rooms.Length; i++)
            {
                Room room = controller.rooms[i];
                if (room != null && string.Equals(room.name, roomName, StringComparison.OrdinalIgnoreCase))
                    return room.roomBounds;
            }

            return null;
        }

        private void Awake()
        {
            CacheEnemies();
        }

        private void Start()
        {
            Physics2D.SyncTransforms();
            ScanRoomCores();
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

        private void Update()
        {
            if (!enemyRoomLifecycleInitialized)
                TryInitializeEnemyRoomLifecycle();
        }

        private void ScanRoomCores()
        {
            if (coreTilemap == null || rooms == null)
            {
                return;
            }

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
                    if (IsCoreInRoom(core, room))
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
                if (room == null || room.released)
                    continue;

                if (!room.remainingCores.Remove(cell))
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
                if (room == null || room.released)
                    continue;

                if (!room.remainingSceneCores.Remove(core))
                {
                    continue;
                }

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

        private void TryInitializeEnemyRoomLifecycle()
        {
            if (rooms == null)
                return;

            bool anyRoomHasPlayer = false;
            foreach (Room room in rooms)
            {
                Collider2D bounds = room != null ? room.roomBounds : null;
                if (bounds == null)
                    continue;

                bool roomHasPlayer = IsRoomOccupied(bounds);
                anyRoomHasPlayer |= roomHasPlayer;
                SetRoomEnemiesActive(room, bounds, roomHasPlayer);
            }

            // Player spawning can occur after this controller's Awake/Start,
            // so keep checking until a player reaches an authored room.
            enemyRoomLifecycleInitialized = anyRoomHasPlayer;
        }

        private void PrepareEnemyRoomsForTransitionInternal(Vector2 sourcePosition, Vector2 destinationPosition)
        {
            Room sourceRoom = FindRoomContaining(sourcePosition);
            Room destinationRoom = FindRoomContaining(destinationPosition);
            if (sourceRoom == null && destinationRoom == null)
                return;

            if (sourceRoom != null)
                RemoveRoomEnemies(sourceRoom, sourceRoom.roomBounds);

            if (destinationRoom != null)
                SetRoomEnemiesActive(destinationRoom, destinationRoom.roomBounds, true);

            enemyRoomLifecycleInitialized = true;
        }

        private Room FindRoomContaining(Vector2 position)
        {
            if (rooms == null)
                return null;

            for (int i = 0; i < rooms.Length; i++)
            {
                Room room = rooms[i];
                if (room?.roomBounds != null && room.roomBounds.OverlapPoint(position))
                    return room;
            }

            return null;
        }

        private void SetRoomEnemiesActive(Room room, Collider2D bounds, bool active)
        {
            foreach (EnemyMovementController enemy in GetRoomEnemies(room, bounds))
            {
                if (enemy == null || !enemyOriginalStates.ContainsKey(enemy))
                    continue;

                if (!active && enemy.TryGetComponent(out Rigidbody2D body))
                    body.linearVelocity = Vector2.zero;

                if (enemy.gameObject.activeSelf != active)
                    enemy.gameObject.SetActive(active);
            }
        }

        private void RemoveRoomEnemies(Room room, Collider2D bounds)
        {
            GameManager gameManager = GameManager.Instance != null
                ? GameManager.Instance
                : FindAnyObjectByType<GameManager>();

            foreach (EnemyMovementController enemy in GetRoomEnemies(room, bounds))
            {
                if (enemy == null || !enemyOriginalStates.ContainsKey(enemy))
                    continue;

                if (enemy.TryGetComponent(out CharacterHealth health) && health.life > 0)
                    gameManager?.NotifyEnemyDied();

                Destroy(enemy.gameObject);
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

        /// <summary>Returns whether a living player is inside an authored room bound.</summary>
        public static bool IsRoomOccupied(Collider2D area)
        {
            MovementController[] players = FindObjectsByType<MovementController>(FindObjectsInactive.Exclude);
            for (int i = 0; i < players.Length; i++)
            {
                MovementController player = players[i];
                if (player != null && !player.isDead && player.CompareTag("Player") &&
                    area.OverlapPoint(BattleMode7PortalController.GetRoomPresencePosition(player)))
                    return true;
            }
            return false;
        }

        private static bool IsCore(TileBase tile)
        {
            return tile != null && !string.IsNullOrWhiteSpace(tile.name) &&
                   tile.name.IndexOf("CoreMechanism", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsCoreInRoom(CoreMechanismsDestructible core, Room room)
        {
            if (core == null || room == null)
                return false;

            if (room.coreRoots != null && room.coreRoots.Length > 0)
            {
                for (int i = 0; i < room.coreRoots.Length; i++)
                {
                    GameObject root = room.coreRoots[i];
                    if (root != null && core.transform.IsChildOf(root.transform))
                        return true;
                }

                return false;
            }

            return room.roomBounds != null && room.roomBounds.OverlapPoint(core.transform.position);
        }

    }
}

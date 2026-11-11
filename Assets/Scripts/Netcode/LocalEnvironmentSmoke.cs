#if UNITY_EDITOR || DEBUG
using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using StageAssets;

namespace Assets.Scripts.Netcode
{
    // Native environment factories/triggers; opt-in Development only, same path offline/server.
    public static class LocalEnvironmentSmoke
    {
        const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        static T Read<T>(object source, string name) => (T)source.GetType().GetField(name, Fields).GetValue(source);
        static void PlacePlayers(Collider2D room, Vector3? near = null)
        {
            if (room == null) throw new InvalidOperationException("R08 missing authored room");
            Vector3 target = near ?? room.bounds.center;
            var previous = UnityEngine.Object.FindAnyObjectByType<PlayerIdentity>();
            World3RoomProgressionController.PrepareEnemyRoomsForTransition(previous != null ? (Vector2)previous.transform.position : (Vector2)target, target);
            int index = 0;
            foreach (var identity in UnityEngine.Object.FindObjectsByType<PlayerIdentity>())
            {
                if (!identity.TryGetComponent<MovementController>(out var mover)) continue;
                Vector2 position = (Vector2)target + Vector2.right * index++;
                mover.Rigidbody.position = position; mover.transform.position = position;
                NetworkPlayerInput.SyncAuthorityMotionTransform(mover);
                if (Mirror.NetworkServer.active && mover.TryGetComponent<NetworkPlayerInput>(out var input) && input.isServer) input.ServerReconcileMovementNow();
                if (mover.TryGetComponent<CharacterHealth>(out var health)) { health.life = 200; health.SetExternalInvulnerability(true); }
            }
            Camera selected = null; float nearest = float.PositiveInfinity;
            foreach (var camera in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
            {
                if (!camera.CompareTag("MainCamera")) continue;
                Vector3 center = camera.transform.position;
                // Some rooms use fixed authored cameras with no follow component.
                if (camera.TryGetComponent<CameraFollowClamp2D>(out var follow))
                {
                    var bounds = Read<Collider2D>(follow, "boundsCollider");
                    if (bounds != null) center = bounds is BoxCollider2D box ? box.transform.TransformPoint(box.offset) : bounds.bounds.center;
                }
                // Player Area and camera bounds are distinct authored colliders.
                float distance = ((Vector2)center - (Vector2)room.bounds.center).sqrMagnitude;
                if (distance < nearest) { nearest = distance; selected = camera; }
            }
            if (selected == null) throw new InvalidOperationException("R08 room has no authored camera");
            foreach (var camera in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
                if (camera.CompareTag("MainCamera"))
                {
                    camera.enabled = camera == selected;
                    if (camera.TryGetComponent<AudioListener>(out var listener)) listener.enabled = camera == selected;
                }
            if (selected.TryGetComponent<CameraFollowClamp2D>(out var selectedFollow)) selectedFollow.ForceSnapNow();
            Physics2D.SyncTransforms();
            Debug.Log("[LoadTest] r08 room setup=" + room.name);
        }
        static IEnumerator PauseDuringMotion()
        {
            yield return new WaitForSecondsRealtime(.3f);
            var pause = GamePauseController.Instance;
            var toggle = typeof(GamePauseController).GetMethod("TogglePause", Fields);
            if (pause == null || toggle == null || GamePauseController.IsPaused) throw new InvalidOperationException("R08 pause setup invalid");
            toggle.Invoke(pause, null); if (!GamePauseController.IsPaused) throw new InvalidOperationException("R08 pause rejected");
            Debug.Log("[LoadTest] r08 paused"); yield return new WaitForSecondsRealtime(1.2f);
            toggle.Invoke(pause, null); if (GamePauseController.IsPaused) throw new InvalidOperationException("R08 resume rejected");
            Debug.Log("[LoadTest] r08 resumed");
        }
        public static IEnumerator Run()
        {
            if (!NetSync.ShouldSimulateLocally) yield break;
            string scene = SceneManager.GetActiveScene().name;
            if (scene == "Stage_3-4")
            {
                var barrel = UnityEngine.Object.FindAnyObjectByType<BarrelPillarTrap>();
                if (barrel == null) throw new InvalidOperationException("R08 missing native barrel");
                var room = World3RoomProgressionController.FindRoomBoundsContaining(barrel.transform.position) ?? World3RoomProgressionController.FindRoomBounds("Room 2");
                PlacePlayers(room, barrel.transform.position + Vector3.down * 3);
                foreach (var pillar in barrel.GetComponentsInChildren<BarrelPillarTrapPillar>())
                    if (!pillar.TryDestroyByExplosion(BombExplosion.ExplosionPart.Start)) throw new InvalidOperationException("R08 native pillar rejected");
                if (!barrel.AreBothPillarsDestroyed) throw new InvalidOperationException("R08 did not destroy both native pillars");
                Debug.Log("[LoadTest] r08 barrel triggered"); yield return PauseDuringMotion();
                yield return new WaitForSecondsRealtime(6);
            }
            else if (scene == "Stage_3-5")
            {
                var penguins = UnityEngine.Object.FindObjectsByType<RocketPenguinController>();
                if (penguins.Length == 0) throw new InvalidOperationException("R08 missing Penguin");
                PlacePlayers(Read<Collider2D>(penguins[0], "roomBounds"));
                yield return new WaitForSecondsRealtime(20);
                Debug.Log("[LoadTest] r08 penguin observed");
                var snow = UnityEngine.Object.FindAnyObjectByType<SnowBallTrap>();
                if (snow == null) throw new InvalidOperationException("R08 missing SnowBall");
                PlacePlayers(Read<Collider2D>(snow, "roomBounds"), snow.transform.position + Vector3.down * 4);
                var map = Read<Tilemap>(snow, "supportTilemap");
                foreach (var cell in Read<Vector3Int[]>(snow, "supportCells")) map.SetTile(cell, null);
                Debug.Log("[LoadTest] r08 snow support removed"); yield return PauseDuringMotion();
                yield return new WaitForSecondsRealtime(10);
            }
            else if (scene == "Stage_3-3")
            {
                var manager = UnityEngine.Object.FindAnyObjectByType<GameManager>();
                if (manager == null || manager.destructibleTilemap == null) throw new InvalidOperationException("R08 missing bubble terrain");
                var player = UnityEngine.Object.FindAnyObjectByType<PlayerIdentity>();
                var source = player != null ? player.GetComponent<BombController>() : null;
                if (source == null) throw new InvalidOperationException("R08 missing bubble damage source");
                int count = 0;
                foreach (var cell in manager.destructibleTilemap.cellBounds.allPositionsWithin)
                    if (manager.destructibleTilemap.HasTile(cell))
                    {
                        Vector3 position = manager.destructibleTilemap.GetCellCenterWorld(cell);
                        Vector3 viewport = Camera.main.WorldToViewportPoint(position);
                        if (viewport.z <= 0 || viewport.x < .1f || viewport.x > .9f || viewport.y < .1f || viewport.y > .9f) continue;
                        source.ClearDestructibleForEffect(position, spawnDestructiblePrefab: false); if (++count == 3) break;
                    }
                if (count == 0) throw new InvalidOperationException("R08 no destructibles for bubble burst");
                Debug.Log("[LoadTest] r08 bubbles triggered"); yield return PauseDuringMotion();
                yield return new WaitForSecondsRealtime(8);
                World3GateOpenedSequenceController gate = null; float nearest = float.PositiveInfinity;
                foreach (var candidate in UnityEngine.Object.FindObjectsByType<World3GateOpenedSequenceController>())
                {
                    float distance = ((Vector2)candidate.GetChipCenterWorld() - (Vector2)Camera.main.transform.position).sqrMagnitude;
                    if (distance < nearest) { nearest = distance; gate = candidate; }
                }
                if (gate == null) throw new InvalidOperationException("R08 missing native bubble gate");
                Vector3 center = gate.GetChipCenterWorld();
                PlacePlayers(World3RoomProgressionController.FindRoomBoundsContaining(center), center + Vector3.left * 2);
                gate.BeginSequence(); Debug.Log("[LoadTest] r08 bubble gate triggered");
                yield return new WaitForSecondsRealtime(6);
            }
            else if (scene == "Stage_3-2")
            {
                var leaps = UnityEngine.Object.FindObjectsByType<Stage32VulcanFlameLeap>();
                if (leaps.Length == 0) throw new InvalidOperationException("R08 missing Vulcan");
                PlacePlayers(Read<Collider2D>(leaps[0], "roomBounds"));
                yield return new WaitForSecondsRealtime(8);
                var volcano = UnityEngine.Object.FindAnyObjectByType<Stage32VolcanoEyesAnimator>();
                if (volcano == null) throw new InvalidOperationException("R08 missing Volcano");
                PlacePlayers(Read<Collider2D>(volcano, "roomBounds"));
                yield return new WaitForSecondsRealtime(17);
                Debug.Log("[LoadTest] r08 volcano observed");
                yield return PauseDuringMotion();
            }
            else if (scene == "Stage_3-6")
            {
                var player = UnityEngine.Object.FindAnyObjectByType<PlayerIdentity>();
                var source = player != null ? player.GetComponent<BombController>() : null;
                var resolver = UnityEngine.Object.FindAnyObjectByType<DestructibleTileResolver>();
                if (source == null || resolver == null || source.destructibleTiles == null) throw new InvalidOperationException("R08 missing rocket setup");
                bool launched = false;
                foreach (var cell in source.destructibleTiles.cellBounds.allPositionsWithin)
                {
                    if (!resolver.TryGetHandler(source.destructibleTiles.GetTile(cell), out var handler) || handler is not RocketTileHandler) continue;
                    Vector3 position = source.destructibleTiles.GetCellCenterWorld(cell);
                    var room = World3RoomProgressionController.FindRoomBoundsContaining(position); if (room == null) continue;
                    PlacePlayers(room, position + Vector3.down);
                    launched = handler.HandleHit(source, position, cell); if (launched) break;
                }
                if (!launched) throw new InvalidOperationException("R08 native destructible rocket not triggered");
                Debug.Log("[LoadTest] r08 rocket triggered"); yield return PauseDuringMotion();
                yield return new WaitForSecondsRealtime(8);
            }
            else throw new InvalidOperationException("R08 unsupported scene " + scene);
            Debug.Log("[LoadTest] r08 environment completed scene=" + scene);
        }
    }
}
#endif

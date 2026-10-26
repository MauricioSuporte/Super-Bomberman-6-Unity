using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace Assets.Scripts.Netcode
{
    public partial class NetworkBombFx
    {
        public struct PlacementState
        {
            public int available;
            public uint prefabAssetId, continuationPrefabAssetId;
            public bool allowed, lineAllowed, purpleAllowed;
            public float purpleDuration;
        }

        sealed class PlacementPrediction
        {
            public GameObject visual;
            public SpriteRenderer renderer;
            public Sprite[] frames;
            public float frameTime, age;
            public Vector2 position;
            public bool confirmed, soundPlayed;
        }
        readonly Dictionary<(uint tick, byte action, Vector2 cell), PlacementPrediction> placements = new();
        readonly List<(uint tick, byte action, Vector2 cell)> expiredPlacements = new();
        PlacementState placementState;
        bool hasPlacementState;
        public int PlacementReservationCount => placements.Count;

        public bool HasPredictedPlacementAt(Vector2 position, float tileSize, uint tick)
        {
            foreach (var pair in placements)
                if (!pair.Value.confirmed && !NetworkInputHistory.IsNewer(pair.Key.tick, tick) &&
                    Vector2.Distance(position, pair.Value.position) < tileSize * 0.35f) return true;
            return false;
        }

        public PlacementState CapturePlacementState()
        {
            var controller = bomb != null ? bomb : GetComponent<BombController>();
            if (controller == null) return default;
            var purple = GetComponent<PurpleLouieBombLineAbility>();
            return new PlacementState { available = controller.BombsRemaining, allowed = controller.NetworkPlacementAllowed,
                prefabAssetId = PlacementPrefabId(controller.GetNetworkPlacementPrefab()),
                continuationPrefabAssetId = PlacementPrefabId(controller.GetNetworkPlacementContinuationPrefab()),
                lineAllowed = TryGetComponent<LineBombAbility>(out var line) && line.IsEnabled,
                purpleAllowed = purple != null && purple.IsEnabled,
                purpleDuration = purple != null ? Mathf.Max(0.01f, purple.lockSeconds) : 0.25f };
        }

        static uint PlacementPrefabId(GameObject prefab)
            => prefab != null && prefab.TryGetComponent<NetworkIdentity>(out var identity) ? identity.assetId : 0;

        bool PlacementSoundWasPredicted(uint tick, byte action)
        {
            foreach (var pair in placements)
                if (pair.Key.tick == tick && pair.Key.action == action && pair.Value.soundPlayed) return true;
            return false;
        }

        public bool BlocksPredictedPlacement(Vector2 current, Vector2 target, float tileSize, uint tick)
        {
            float half = tileSize * 0.35f;
            foreach (var pair in placements)
            {
                var pending = pair.Value;
                if (pending.confirmed || NetworkInputHistory.IsNewer(pair.Key.tick, tick)) continue;
                bool insideCurrent = Mathf.Abs(current.x - pending.position.x) <= half && Mathf.Abs(current.y - pending.position.y) <= half;
                bool insideTarget = Mathf.Abs(target.x - pending.position.x) <= half && Mathf.Abs(target.y - pending.position.y) <= half;
                if (!insideCurrent && insideTarget) return true;
            }
            return false;
        }

        public bool TryPredictPlacement(uint tick)
        {
            if (TryPredictLinePlacement(tick, 0, false)) return true;
            uint prefabId = placements.Count > 0 ? placementState.continuationPrefabAssetId : placementState.prefabAssetId;
            if (!hasPlacementState || !NetworkClient.prefabs.TryGetValue(prefabId, out var prefab)) return false;
            var controller = bomb != null ? bomb : GetComponent<BombController>();
            if (controller == null || !controller.TryGetNetworkPlacementPosition(out var position)) return false;
            return BeginPlacementPrediction(tick, position, prefab);
        }

        public bool BeginPlacementPrediction(uint tick, Vector2 position, GameObject prefab, byte action = 0, bool playSound = true)
        {
            if (NetSync.Mode != NetSync.NetMode.Client || !NetworkClient.active || !isLocalPlayer || isServer ||
                !hasPlacementState || !placementState.allowed || placements.Count >= Mathf.Max(0, placementState.available) ||
                placements.ContainsKey((tick, action, position)) || prefab == null || GamePauseController.IsPaused || BombermanNetworkManager.BattleStartPending ||
                AbilityPredictionLocksMovement || CarryingBombLocally || PlayerBounceActive || PinkJumpActive) return false;
            var movement = GetComponent<MovementController>();
            if (movement == null || movement.isDead || movement.IsEndingStage || movement.InputLocked || movement.IsRidingPlaying()) return false;
            foreach (var pair in placements)
                if ((action == 0 && pair.Key.tick == tick && pair.Key.action == 0) ||
                    Vector2.Distance(pair.Value.position, position) < movement.tileSize * 0.25f) return false;
            var source = prefab.GetComponentInChildren<SpriteRenderer>(true);
            if (source == null) return false;
            var animation = source.GetComponent<AnimatedSpriteRenderer>();
            Sprite sprite = source.sprite != null ? source.sprite : animation != null && animation.animationSprite != null && animation.animationSprite.Length > 0 ?
                animation.animationSprite[0] : animation != null ? animation.idleSprite : null;
            if (sprite == null) return false;
            var proxy = new GameObject("PredictedBombPlacement", typeof(SpriteRenderer));
            proxy.transform.position = (Vector3)position + source.transform.position - prefab.transform.position;
            proxy.transform.rotation = source.transform.rotation; proxy.transform.localScale = source.transform.lossyScale;
            var renderer = proxy.GetComponent<SpriteRenderer>(); renderer.sprite = sprite; renderer.color = source.color;
            renderer.sortingLayerID = source.sortingLayerID; renderer.sortingOrder = source.sortingOrder;
            renderer.sharedMaterial = source.sharedMaterial; renderer.flipX = source.flipX; renderer.flipY = source.flipY;
            var pendingPlacement = new PlacementPrediction { visual = proxy, renderer = renderer, position = position,
                frames = animation != null ? animation.animationSprite : null, frameTime = animation != null ? animation.animationTime : 0.25f };
            placements.Add((tick, action, position), pendingPlacement);
            pendingPlacement.soundPlayed = playSound && PlayPlacementSoundLocally();
            TracePlacement("placement_prediction_begin", tick, position, 0, "visual_only");
            return true;
        }

        [Server]
        public void ServerConfirmPlacement(GameObject placed, byte action = 0)
        {
            if (connectionToClient == null || connectionToClient == NetworkServer.localConnection || placed == null ||
                !placed.TryGetComponent<NetworkIdentity>(out var identity) || identity.netId == 0) return;
            uint tick = TryGetComponent<NetworkPlayerInput>(out var input) ? (action == 2 ? input.CurrentActionCTick : input.CurrentActionATick) : 0;
            TargetPlacementConfirmed(connectionToClient, tick, identity.netId, placed.transform.position, action);
        }

        [TargetRpc]
        void TargetPlacementConfirmed(NetworkConnectionToClient target, uint tick, uint bombId, Vector2 position, byte action) =>
            ReceivePlacementConfirmed(tick, bombId, position, action);

        public void ReceivePlacementConfirmed(uint tick, uint bombId, Vector2 position, byte action = 0)
        {
            if (NetSync.Mode != NetSync.NetMode.Client || !isLocalPlayer || bombId == 0) return;
            float tolerance = GetComponent<MovementController>() is { } movement ? movement.tileSize * 0.35f : 0.35f;
            foreach (var pair in placements)
            {
                if (pair.Key.tick != tick || pair.Key.action != action || pair.Value.confirmed ||
                    (action != 0 && Vector2.Distance(pair.Value.position, position) > tolerance)) continue;
                var pending = pair.Value; pending.confirmed = true; ReleasePlacementVisual(pending);
                TracePlacement("placement_prediction_confirm", tick, position, pending.age * 1000, "bomb=" + bombId + ";action=" + action);
                return;
            }
        }

        public void ReconcilePlacement(uint acknowledgedTick, PlacementState state)
        {
            if (NetSync.Mode != NetSync.NetMode.Client || !isLocalPlayer) return;
            placementState = state; hasPlacementState = true;
            expiredPlacements.Clear();
            foreach (var pair in placements)
                if (!NetworkInputHistory.IsNewer(pair.Key.tick, acknowledgedTick))
                {
                    if (!pair.Value.confirmed) TracePlacement("placement_prediction_reject", pair.Key.tick, pair.Value.position,
                        pair.Value.age * 1000, "input_processed_without_placement");
                    ReleasePlacementVisual(pair.Value); expiredPlacements.Add(pair.Key);
                }
            foreach (var key in expiredPlacements) placements.Remove(key);
        }

        void UpdatePlacementPresentation(float deltaTime)
        {
            if (GamePauseController.IsPaused) return;
            var movement = GetComponent<MovementController>();
            if (movement == null || movement.isDead || movement.IsEndingStage) { ClearPlacementPredictions(); return; }
            expiredPlacements.Clear();
            foreach (var pair in placements)
            {
                var pending = pair.Value; pending.age += deltaTime;
                if (pending.age > Mathf.Clamp((float)NetworkTime.rtt * 3, 0.5f, 1))
                {
                    if (!pending.confirmed) TracePlacement("placement_prediction_reject", pair.Key.tick, pending.position,
                        pending.age * 1000, "confirmation_timeout");
                    ReleasePlacementVisual(pending); expiredPlacements.Add(pair.Key); hasPlacementState = false;
                }
                else if (pending.renderer != null && pending.frames != null && pending.frames.Length > 0)
                    pending.renderer.sprite = pending.frames[Mathf.FloorToInt(pending.age / Mathf.Max(0.01f, pending.frameTime)) % pending.frames.Length];
            }
            foreach (var key in expiredPlacements) placements.Remove(key);
        }

        static void ReleasePlacementVisual(PlacementPrediction prediction)
        {
            if (prediction.visual != null) ReleaseTileVisual(prediction.visual);
            prediction.visual = null; prediction.renderer = null;
        }

        void ClearPlacementPredictions()
        {
            foreach (var pending in placements.Values) ReleasePlacementVisual(pending);
            placements.Clear(); hasPlacementState = false;
        }

        bool PlayPlacementSoundLocally()
        {
            var controller = bomb != null ? bomb : GetComponent<BombController>();
            var source = controller != null && controller.playerAudioSource != null ? controller.playerAudioSource : GetComponent<AudioSource>();
            if (controller == null || controller.placeBombSfx == null || source == null) return false;
            source.PlayOneShot(controller.placeBombSfx, GameAudioSettings.ApplySfxVolume(1));
            return true;
        }

        [Server]
        public void ServerPlacementSound()
        {
            PlayPlacementSoundLocally();
            RpcPlacementSound(TryGetComponent<NetworkPlayerInput>(out var input) ? input.CurrentActionATick : 0);
        }

        [ClientRpc]
        void RpcPlacementSound(uint tick)
        {
            if (isServer) return;
            if (isLocalPlayer)
                foreach (var pair in placements)
                    if (pair.Key.tick == tick && pair.Key.action == 0 && pair.Value.soundPlayed) return;
            PlayPlacementSoundLocally();
        }

        void TracePlacement(string kind, uint tick, Vector2 position, float durationMs, string reason)
        {
            if (!GameplayLatencyDiagnostics.Enabled) return;
            var entry = GameplayLatencyDiagnostics.For(kind, GetComponent<MovementController>(), detail: reason);
            entry.inputTick = tick; entry.bombPosition = position; entry.durationMs = durationMs;
            GameplayLatencyDiagnostics.Write(entry);
        }
    }
}

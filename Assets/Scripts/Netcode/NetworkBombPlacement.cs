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
            public uint prefabAssetId;
            public bool allowed;
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
        readonly Dictionary<uint, PlacementPrediction> placements = new();
        readonly List<uint> expiredPlacements = new();
        PlacementState placementState;
        bool hasPlacementState;
        public int PlacementReservationCount => placements.Count;

        public PlacementState CapturePlacementState()
        {
            var controller = bomb != null ? bomb : GetComponent<BombController>();
            if (controller == null) return default;
            var prefab = controller.GetNetworkPlacementPrefab();
            return new PlacementState { available = controller.BombsRemaining, allowed = controller.NetworkPlacementAllowed,
                prefabAssetId = prefab != null && prefab.TryGetComponent<NetworkIdentity>(out var identity) ? identity.assetId : 0 };
        }

        public bool BlocksPredictedPlacement(Vector2 current, Vector2 target, float tileSize, uint tick)
        {
            float half = tileSize * 0.35f;
            foreach (var pair in placements)
            {
                var pending = pair.Value;
                if (pending.confirmed || NetworkInputHistory.IsNewer(pair.Key, tick)) continue;
                bool insideCurrent = Mathf.Abs(current.x - pending.position.x) <= half && Mathf.Abs(current.y - pending.position.y) <= half;
                bool insideTarget = Mathf.Abs(target.x - pending.position.x) <= half && Mathf.Abs(target.y - pending.position.y) <= half;
                if (!insideCurrent && insideTarget) return true;
            }
            return false;
        }

        public bool TryPredictPlacement(uint tick)
        {
            if (!hasPlacementState || !NetworkClient.prefabs.TryGetValue(placementState.prefabAssetId, out var prefab)) return false;
            var controller = bomb != null ? bomb : GetComponent<BombController>();
            if (controller == null || !controller.TryGetNetworkPlacementPosition(out var position)) return false;
            return BeginPlacementPrediction(tick, position, prefab);
        }

        public bool BeginPlacementPrediction(uint tick, Vector2 position, GameObject prefab)
        {
            if (NetSync.Mode != NetSync.NetMode.Client || !NetworkClient.active || !isLocalPlayer || isServer ||
                !hasPlacementState || !placementState.allowed || placements.Count >= Mathf.Max(0, placementState.available) ||
                placements.ContainsKey(tick) || prefab == null || GamePauseController.IsPaused || BombermanNetworkManager.BattleStartPending ||
                AbilityPredictionLocksMovement || CarryingBombLocally || PlayerBounceActive) return false;
            var movement = GetComponent<MovementController>();
            if (movement == null || movement.isDead || movement.IsEndingStage || movement.InputLocked || movement.IsRidingPlaying()) return false;
            foreach (var pending in placements.Values)
                if (Vector2.Distance(pending.position, position) < movement.tileSize * 0.25f) return false;
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
            placements.Add(tick, pendingPlacement);
            pendingPlacement.soundPlayed = PlayPlacementSoundLocally();
            TracePlacement("placement_prediction_begin", tick, position, 0, "visual_only");
            return true;
        }

        [Server]
        public void ServerConfirmPlacement(GameObject placed)
        {
            if (connectionToClient == null || connectionToClient == NetworkServer.localConnection || placed == null ||
                !placed.TryGetComponent<NetworkIdentity>(out var identity) || identity.netId == 0) return;
            uint tick = TryGetComponent<NetworkPlayerInput>(out var input) ? input.CurrentActionATick : 0;
            TargetPlacementConfirmed(connectionToClient, tick, identity.netId, placed.transform.position);
        }

        [TargetRpc]
        void TargetPlacementConfirmed(NetworkConnectionToClient target, uint tick, uint bombId, Vector2 position) =>
            ReceivePlacementConfirmed(tick, bombId, position);

        public void ReceivePlacementConfirmed(uint tick, uint bombId, Vector2 position)
        {
            if (NetSync.Mode != NetSync.NetMode.Client || !isLocalPlayer || bombId == 0 || !placements.TryGetValue(tick, out var pending) || pending.confirmed) return;
            pending.confirmed = true;
            ReleasePlacementVisual(pending);
            TracePlacement("placement_prediction_confirm", tick, position, pending.age * 1000, "bomb=" + bombId);
        }

        public void ReconcilePlacement(uint acknowledgedTick, PlacementState state)
        {
            if (NetSync.Mode != NetSync.NetMode.Client || !isLocalPlayer) return;
            placementState = state; hasPlacementState = true;
            expiredPlacements.Clear();
            foreach (var pair in placements)
                if (!NetworkInputHistory.IsNewer(pair.Key, acknowledgedTick))
                {
                    if (!pair.Value.confirmed) TracePlacement("placement_prediction_reject", pair.Key, pair.Value.position,
                        pair.Value.age * 1000, "input_processed_without_placement");
                    ReleasePlacementVisual(pair.Value); expiredPlacements.Add(pair.Key);
                }
            foreach (uint tick in expiredPlacements) placements.Remove(tick);
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
                    if (!pending.confirmed) TracePlacement("placement_prediction_reject", pair.Key, pending.position,
                        pending.age * 1000, "confirmation_timeout");
                    ReleasePlacementVisual(pending); expiredPlacements.Add(pair.Key); hasPlacementState = false;
                }
                else if (pending.renderer != null && pending.frames != null && pending.frames.Length > 0)
                    pending.renderer.sprite = pending.frames[Mathf.FloorToInt(pending.age / Mathf.Max(0.01f, pending.frameTime)) % pending.frames.Length];
            }
            foreach (uint tick in expiredPlacements) placements.Remove(tick);
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
            if (isServer || (isLocalPlayer && placements.TryGetValue(tick, out var prediction) && prediction.soundPlayed)) return;
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

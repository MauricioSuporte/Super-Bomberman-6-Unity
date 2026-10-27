using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace Assets.Scripts.Netcode
{
    public partial class NetworkBombFx
    {
        BoatRideZone ownerBoatSource;
        BoatRideZone.NetworkRideState ownerBoatState;
        GameObject ownerBoatView, ownerBoatMount;
        AnimatedSpriteRenderer[] ownerBoatAnimations, ownerBoatHeads;
        bool[] ownerBoatUnscaled;
        readonly List<AnimatedSpriteRenderer> ownerBoatPlayerAnimations = new();
        readonly List<SpriteRenderer> ownerBoatMountSprites = new();
        int[] ownerBoatSpriteIds;
        int ownerBoatDirection;
        bool ownerBoatIdle = true;
        bool boatPreviousInactivity;

        public bool OwnerBoatPredictionReady => ownerBoatSource != null && ownerBoatView != null &&
            ownerBoatSource.gameObject.activeInHierarchy && ownerBoatView.activeInHierarchy;

        static bool FiniteBoatVector(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
        static bool ValidBoatState(BoatRideZone.NetworkRideState state) =>
            !string.IsNullOrEmpty(state.path) && state.path.Length <= 1024 && state.downId != 0 &&
            FiniteBoatVector(state.scale) && FiniteBoatVector(state.followOffset) && float.IsFinite(state.z) &&
            Mathf.Abs(state.scale.x) > 0.000001f && Mathf.Abs(state.scale.y) > 0.000001f && Mathf.Abs(state.scale.z) > 0.000001f &&
            FiniteBoatVector(state.headDown) && FiniteBoatVector(state.headUp) && FiniteBoatVector(state.headLeft) && FiniteBoatVector(state.headRight) &&
            float.IsFinite(state.rotation.x) && float.IsFinite(state.rotation.y) && float.IsFinite(state.rotation.z) && float.IsFinite(state.rotation.w) &&
            Quaternion.Dot(state.rotation, state.rotation) > 0.00001f;

        public void ReconcileOwnerBoat(MovementController.NetworkMovementState state)
        {
            if (NetSync.Mode != NetSync.NetMode.Client || !NetworkClient.active || isServer || !isLocalPlayer ||
                presentationOwnerSuspended || !TryGetComponent<MovementController>(out var movement) || movement.isDead || movement.IsEndingStage ||
                (state.continuousPresentation && !state.canPredict) ||
                !ValidBoatState(state.boat))
            { ClearOwnerBoat(); return; }
            if (ownerBoatSource == null || ownerBoatState.path != state.boat.path)
            {
                ClearOwnerBoat();
                BoatRideZone source = null;
                foreach (var candidate in FindObjectsByType<BoatRideZone>(FindObjectsInactive.Include))
                    if (candidate.gameObject.scene == gameObject.scene && candidate.NetworkBoatPath == state.boat.path)
                    {
                        if (source != null) return; // Ambiguous definitions never grant prediction.
                        source = candidate;
                    }
                if (source == null || !source.CanProjectNetworkRide) return;
                ownerBoatSource = source;
                ownerBoatView = new GameObject("OwnerBoatView");
                ownerBoatView.transform.SetPositionAndRotation(new Vector3(state.position.x, state.position.y, state.boat.z), state.boat.rotation);
                ownerBoatView.transform.localScale = state.boat.scale;
                var definitions = source.NetworkBoatAnimations();
                ownerBoatAnimations = new AnimatedSpriteRenderer[4];
                ownerBoatUnscaled = new bool[4];
                for (int i = 0; i < 4; i++)
                {
                    ownerBoatAnimations[i] = CreateBoatAnimation(definitions[i], source.transform, ownerBoatView.transform);
                    ownerBoatUnscaled[i] = definitions[i] == null || definitions[i].UseUnscaledTime;
                }
                ownerBoatHeads = source.NetworkHeadAnimations(transform);
                var facing = state.direction != Vector2.zero ? state.direction : state.facing;
                ownerBoatDirection = facing == Vector2.up ? 1 : facing == Vector2.left ? 2 : facing == Vector2.right ? 3 : 0;
                ownerBoatIdle = state.direction == Vector2.zero;
                boatPreviousInactivity = movement.SuppressInactivityAnimation;
                movement.SetSuppressInactivityAnimation(true);
                RefreshBoatPlayerAnimations();
            }
            if (ownerBoatSpriteIds == null || ownerBoatSpriteIds[0] != state.boat.downId || ownerBoatSpriteIds[1] != state.boat.upId ||
                ownerBoatSpriteIds[2] != state.boat.leftId || ownerBoatSpriteIds[3] != state.boat.rightId)
            {
                BombermanNetworkManager.SetOwnerBoatSpriteSuppression(this, ownerBoatSpriteIds, false);
                ownerBoatSpriteIds = new[] { state.boat.downId, state.boat.upId, state.boat.leftId, state.boat.rightId };
            }
            ownerBoatState = state.boat;
            BombermanNetworkManager.SetOwnerBoatSpriteSuppression(this, ownerBoatSpriteIds, true);
        }

        static AnimatedSpriteRenderer CreateBoatAnimation(AnimatedSpriteRenderer definition, Transform source, Transform parent)
        {
            if (definition == null || !definition.TryGetComponent<SpriteRenderer>(out var original)) return null;
            var view = new GameObject("BoatDirection", typeof(SpriteRenderer));
            view.layer = definition.gameObject.layer;
            view.transform.SetParent(parent, false);
            var sprite = view.GetComponent<SpriteRenderer>();
            NetworkVisualPose.Capture(source, original).Apply(parent, sprite);
            sprite.sharedMaterial = original.sharedMaterial; sprite.color = original.color;
            sprite.renderingLayerMask = original.renderingLayerMask;
            sprite.drawMode = original.drawMode;
            if (original.drawMode != SpriteDrawMode.Simple) sprite.size = original.size;
            sprite.flipX = original.flipX; sprite.flipY = original.flipY;
            sprite.sortingLayerID = original.sortingLayerID; sprite.sortingOrder = original.sortingOrder;
            var animation = view.AddComponent<AnimatedSpriteRenderer>();
            animation.idleSprite = definition.idleSprite; animation.animationSprite = definition.animationSprite;
            animation.animationTime = definition.animationTime; animation.useSequenceDuration = definition.useSequenceDuration;
            animation.sequenceDuration = definition.sequenceDuration; animation.frameDurations = definition.frameDurations;
            animation.frameOffsets = definition.frameOffsets; animation.loop = definition.loop;
            animation.loopStartFrame = definition.loopStartFrame; animation.loopRestartDelay = definition.loopRestartDelay;
            animation.pingPong = definition.pingPong; animation.allowFlipX = definition.allowFlipX;
            animation.SetManualAnimationUpdate(true); animation.enabled = false; sprite.enabled = false;
            return animation;
        }

        void RefreshBoatPlayerAnimations()
        {
            GetComponentsInChildren(true, ownerBoatPlayerAnimations);
            ownerBoatMount = GetComponent<PlayerMountCompanion>()?.GetMountedLouieObject();
            ownerBoatMountSprites.Clear();
            if (ownerBoatMount != null) ownerBoatMount.GetComponentsInChildren(true, ownerBoatMountSprites);
        }

        static void SetBoatAnimationVisible(AnimatedSpriteRenderer animation, bool visible)
        {
            if (animation == null) return;
            if (animation.enabled != visible) animation.enabled = visible;
            if (visible) animation.SetManualAnimationUpdate(true);
            if (animation.TryGetComponent<SpriteRenderer>(out var sprite)) sprite.enabled = visible;
        }

        static void AdvanceBoatAnimation(AnimatedSpriteRenderer animation, bool idle, float delta, float unscaled)
        {
            if (animation == null) return;
            animation.idle = idle;
            if (!GamePauseController.IsPaused) animation.AdvanceAnimation(unscaled, delta);
            animation.RefreshFrame();
        }

        void UpdateOwnerBoat(float deltaTime, float unscaledDeltaTime)
        {
            if (!OwnerBoatPredictionReady)
            {
                if (ownerBoatAnimations != null || ownerBoatSpriteIds != null) ClearOwnerBoat();
                return;
            }
            if (NetSync.Mode != NetSync.NetMode.Client || !NetworkClient.active || isServer || !isLocalPlayer ||
                presentationOwnerSuspended || !ownerBoatSource.gameObject.activeInHierarchy || ownerBoatSource.gameObject.scene != gameObject.scene ||
                !TryGetComponent<MovementController>(out var movement) || movement.isDead || movement.IsEndingStage)
            { ClearOwnerBoat(); return; }
            if (ownerBoatMount != GetComponent<PlayerMountCompanion>()?.GetMountedLouieObject()) RefreshBoatPlayerAnimations();
            if (!GamePauseController.IsPaused)
            {
                Vector2 p = movement.Rigidbody != null ? movement.Rigidbody.position : (Vector2)transform.position;
                p += ownerBoatState.followOffset;
                ownerBoatView.transform.SetPositionAndRotation(new Vector3(p.x, p.y, ownerBoatState.z), ownerBoatState.rotation);
                ownerBoatView.transform.localScale = ownerBoatState.scale;
                var direction = movement.Direction != Vector2.zero ? movement.Direction : movement.FacingDirection;
                ownerBoatDirection = direction == Vector2.up ? 1 : direction == Vector2.left ? 2 : direction == Vector2.right ? 3 : 0;
                ownerBoatIdle = movement.Direction == Vector2.zero;
            }
            int boatIndex = ownerBoatAnimations[ownerBoatDirection] != null ? ownerBoatDirection : 0;
            for (int i = 0; i < 4; i++) SetBoatAnimationVisible(ownerBoatAnimations[i], i == boatIndex);
            bool idle = ownerBoatIdle;
            AdvanceBoatAnimation(ownerBoatAnimations[boatIndex], idle, deltaTime, ownerBoatUnscaled[boatIndex] ? unscaledDeltaTime : deltaTime);
            if (!ownerBoatState.hideBody) return;
            foreach (var sprite in ownerBoatMountSprites) if (sprite != null) sprite.enabled = false;
            var head = ownerBoatState.hideHead ? null : ownerBoatHeads[ownerBoatDirection] != null ? ownerBoatHeads[ownerBoatDirection] : ownerBoatHeads[0];
            foreach (var renderer in ownerBoatPlayerAnimations) SetBoatAnimationVisible(renderer, renderer == head);
            if (head != null)
            {
                int index = System.Array.IndexOf(ownerBoatHeads, head);
                Vector2 offset = index == 1 ? ownerBoatState.headUp : index == 2 ? ownerBoatState.headLeft : index == 3 ? ownerBoatState.headRight : ownerBoatState.headDown;
                if (ownerBoatState.headOffsets) head.SetExternalBaseOffsetFromInitial(offset);
                AdvanceBoatAnimation(head, idle, deltaTime, unscaledDeltaTime);
            }
        }

        public void RestorePausedBoatPosition(Vector2 before)
        {
            if (!OwnerBoatPredictionReady || !GamePauseController.IsPaused || !TryGetComponent<MovementController>(out var movement) ||
                movement.isDead || movement.Rigidbody == null) return;
            movement.Rigidbody.position = before;
            transform.position = new Vector3(before.x, before.y, transform.position.z);
        }

        void ClearOwnerBoat()
        {
            BombermanNetworkManager.SetOwnerBoatSpriteSuppression(this, ownerBoatSpriteIds, false);
            ownerBoatSpriteIds = null;
            bool hadView = ownerBoatAnimations != null;
            if (ownerBoatView != null)
            {
                ownerBoatView.SetActive(false);
                if (Application.isPlaying) Destroy(ownerBoatView); else DestroyImmediate(ownerBoatView);
            }
            if (hadView && TryGetComponent<MovementController>(out var movement))
            {
                if (ownerBoatHeads != null)
                    foreach (var head in ownerBoatHeads)
                        if (head != null)
                        {
                            head.ClearExternalBase(); head.SetManualAnimationUpdate(!movement.PredictLocally);
                            head.enabled = false;
                            if (head.TryGetComponent<SpriteRenderer>(out var sprite)) sprite.enabled = false;
                        }
                movement.SetSuppressInactivityAnimation(boatPreviousInactivity);
                if (!movement.ExternalVisualSuppressed && !movement.isDead && !movement.IsEndingStage) movement.EnableExclusiveFromState();
                GetComponent<NetworkPlayerAnimation>()?.InvalidateOwnerPose();
            }
            ownerBoatView = null; ownerBoatSource = null; ownerBoatState = default;
            ownerBoatAnimations = null; ownerBoatHeads = null; ownerBoatMount = null;
            ownerBoatUnscaled = null; ownerBoatPlayerAnimations.Clear(); ownerBoatMountSprites.Clear();
        }
    }
}

using System.Collections.Generic;
using UnityEngine.Tilemaps;
using Mirror;
using UnityEngine;

namespace Assets.Scripts.Netcode
{
    // Shares the Player's existing NetworkBombFx behaviour/index; no new manager.
    public partial class NetworkBombFx
    {
        public enum BombMotionKind : byte { Kick, Punch, Throw, Lift, Carry, Yellow, Search, Magnet }
        float abilityLockRemaining, abilityPoseRemaining, abilityPoseDuration;
        int abilityPoseFrame = -1;
        BombMotionKind abilityPoseKind;
        Vector2 abilityPoseDirection;
        bool abilityPoseActive;
        int previousAbilityHeld;
        MountedType preparedMountType = (MountedType)(-1);
        GameObject preparedMountVisual;
        float nextAutoPunch;

        public bool CarryingBombLocally
        {
            get
            {
                if (NetSync.Mode != NetSync.NetMode.Client || !isLocalPlayer || !NetworkClient.active) return false;
                foreach (var pair in kickPlayback)
                    if (pair.Key != null && !pair.Key.HasExploded && pair.Value.motion.moving &&
                        (pair.Value.motion.kind == BombMotionKind.Carry || pair.Value.motion.kind == BombMotionKind.Lift)) return true;
                return false;
            }
        }

        public bool HasUnconfirmedClientMotion(Bomb target) => target != null &&
            kickPlayback.TryGetValue(target, out var state) && state.unconfirmed;

        public bool AbilityPredictionLocksMovement => abilityLockRemaining > 0;
        public bool HasLocalMountAbilityPose => isLocalPlayer && abilityPoseActive && abilityPoseRemaining > 0 &&
            (abilityPoseKind == BombMotionKind.Yellow || abilityPoseKind == BombMotionKind.Punch);
        public static bool NonBlockingMotion(BombMotionKind kind) => kind == BombMotionKind.Punch ||
            kind == BombMotionKind.Throw || kind == BombMotionKind.Lift || kind == BombMotionKind.Carry;

        public static Vector2 SampleBoundedBounce(Vector2 start, Vector2 end, float duration, float elapsed)
        {
            if (duration <= 0 || elapsed <= duration) return SampleKickSegment(start, end, duration, elapsed);
            float reflected = Mathf.Clamp(elapsed - duration, 0, Mathf.Min(duration, 0.15f)) / duration;
            return end - (end - start) * reflected;
        }

        public static Vector2 SampleArc(Vector2 start, Vector2 end, float duration, float elapsed, float height)
        {
            float progress = duration <= 0 ? 1 : Mathf.Clamp01(elapsed / duration);
            return Vector2.Lerp(start, end, progress) + Vector2.up * (Mathf.Sin(progress * Mathf.PI) * Mathf.Max(0, height));
        }

        public static Vector2 SampleCarry(KickMotion motion, Vector2 carrier, float elapsed)
        {
            float progress = motion.kind == BombMotionKind.Carry || motion.duration <= 0 ? 1 : Mathf.Clamp01(elapsed / motion.duration);
            return carrier + Vector2.up * (motion.carryHeight * progress);
        }

        [Server]
        public void ServerEmitBombMotion(Bomb movingBomb, BombMotionKind kind, Vector2 start, Vector2 end,
            float duration, float arcHeight, float carryHeight, bool moving, bool first)
        {
            if (movingBomb == null || !movingBomb.TryGetComponent<NetworkIdentity>(out var identity) || identity.netId == 0) return;
            var motion = new KickMotion { bombId = identity.netId, inputTick = TryGetComponent<NetworkPlayerInput>(out var input) ? input.CurrentInputTick : 0,
                revision = movingBomb.NextNetworkMotionRevision(), kind = kind, rubber = movingBomb.IsRubberBomb, start = start, end = end,
                duration = duration, arcHeight = arcHeight, carryHeight = carryHeight, moving = moving,
                first = first, serverTime = NetworkTime.localTime };
            if (GameplayLatencyDiagnostics.Enabled)
            {
                var entry = GameplayLatencyDiagnostics.For("bomb_motion_send", GetComponent<MovementController>(), movingBomb, kind.ToString());
                entry.authoritativeTick = motion.inputTick; entry.durationMs = duration * 1000; entry.delta = end - start;
                GameplayLatencyDiagnostics.Write(entry);
            }
            RpcBombMotion(motion);
        }

        [ClientRpc]
        void RpcBombMotion(KickMotion motion) => ReceiveKickMotion(motion);

        // Called once per real input update, never from movement replay.
        public void PredictBombAbilityInput(int held, int pressed, uint inputTick)
        {
            if (NetSync.Mode != NetSync.NetMode.Client || !isLocalPlayer || !NetworkClient.active) return;
            PrepareLocalMountAbilities();
            var movement = GetComponent<MovementController>();
            if (movement == null || movement.isDead || GamePauseController.IsPaused || BombermanNetworkManager.BattleStartPending)
            { previousAbilityHeld = held; return; }
            Vector2 direction = MotionDiagnosticProbe.DirectionFromMask(held);
            if (direction == Vector2.zero) direction = movement.FacingDirection;
            direction = Mathf.Abs(direction.x) > Mathf.Abs(direction.y) ? new Vector2(Mathf.Sign(direction.x), 0) : new Vector2(0, Mathf.Sign(direction.y));
            if (direction == Vector2.zero) direction = Vector2.down;
            Bomb carried = null;
            KickPlayback carry = null;
            foreach (var pair in kickPlayback)
                if (pair.Key != null && pair.Value.motion.moving &&
                    (pair.Value.motion.kind == BombMotionKind.Carry || pair.Value.motion.kind == BombMotionKind.Lift))
                { carried = pair.Key; carry = pair.Value; break; }
            bool releasedA = (held & 16) == 0 && ((previousAbilityHeld & 16) != 0 || carried != null);
            previousAbilityHeld = held;
            if (carried != null)
            {
                if (releasedA && (carry.motion.kind == BombMotionKind.Carry || carry.elapsed >= carry.motion.duration) &&
                    TryGetComponent<PowerGloveAbility>(out var glove) && glove.IsEnabled)
                    PredictFlight(carried, BombMotionKind.Throw, direction, movement, glove.ThrowDistanceTiles, inputTick, movement.tileSize);
                return;
            }
            if (movement.InputLocked || abilityLockRemaining > 0) return;
            if ((pressed & 16) != 0 && TryGetComponent<PowerGloveAbility>(out var powerGlove) && powerGlove.IsEnabled && !movement.IsMounted && !movement.IsRidingPlaying())
            {
                Bomb pick = powerGlove.FindNetworkPickupCandidate();
                if (pick != null && !pick.ClientMotionNonBlocking)
                    BeginAbilityPrediction(pick, new KickMotion { kind = BombMotionKind.Lift, moving = true, first = true,
                        start = pick.transform.position, end = movement.transform.position + Vector3.up * movement.tileSize,
                        duration = powerGlove.PickupDuration, carryHeight = movement.tileSize, inputTick = inputTick }, direction, powerGlove.PickupDuration);
                return;
            }
            if ((pressed & 64) != 0 && TryGetComponent<YellowLouieKickAbility>(out var yellow) && yellow.IsEnabled)
            {
                Bomb target = yellow.FindNetworkBombCandidate(direction);
                if (target != null && !target.ClientMotionNonBlocking)
                {
                    Vector2 origin = SnapCell(target.transform.position, movement.tileSize);
                    Vector2 end = origin + direction * movement.tileSize;
                    if (target.CanPredictKickContinuation(end, movement.tileSize))
                        BeginAbilityPrediction(target, new KickMotion { kind = BombMotionKind.Yellow, moving = true, first = true,
                            start = origin, end = end, duration = 1 / Mathf.Max(0.01f, yellow.cellsPerSecond), inputTick = inputTick },
                            direction, yellow.kickCooldownSeconds);
                }
                return;
            }
            if ((held & 64) != 0 && Time.unscaledTime >= nextAutoPunch &&
                TryGetComponent<BombPunchAbility>(out var punch) && punch.CanPredictNetworkPunch((pressed & 64) != 0))
            {
                Bomb target = punch.FindNetworkPunchCandidate(direction);
                if (target != null && !target.ClientMotionNonBlocking &&
                    PredictFlight(target, BombMotionKind.Punch, direction, movement, punch.punchDistanceTiles, inputTick, 0))
                    nextAutoPunch = Time.unscaledTime + Mathf.Max(0.05f, punch.punchLockTime);
            }
        }

        void PrepareLocalMountAbilities()
        {
            if (!TryGetComponent<PlayerMountCompanion>(out var mount)) return;
            MountedType type = mount.GetMountedLouieType();
            var visual = mount.GetMountedLouieObject();
            if (type == preparedMountType && visual == preparedMountVisual) return;
            preparedMountType = type; preparedMountVisual = visual;
            var yellow = GetComponent<YellowLouieKickAbility>();
            if (type == MountedType.Yellow && yellow == null) yellow = gameObject.AddComponent<YellowLouieKickAbility>();
            yellow?.ConfigureNetworkMountAnimator(visual != null ? visual.GetComponentInChildren<Assets.Scripts.Interface.IYellowLouieDestructibleKickExternalAnimator>(true) : null,
                type == MountedType.Yellow);
            var punch = GetComponent<BombPunchAbility>();
            if (type == MountedType.Blue && punch == null) punch = gameObject.AddComponent<BombPunchAbility>();
            punch?.ConfigureNetworkMountAnimator(type == MountedType.Blue && visual != null ? visual.GetComponentInChildren<IBombPunchExternalAnimator>(true) : null,
                type == MountedType.Blue);
        }

        public void ReapplyMountAbilityPose()
        {
            if (!HasLocalMountAbilityPose) return;
            float progress = 1 - abilityPoseRemaining / Mathf.Max(0.001f, abilityPoseDuration);
            if (abilityPoseKind == BombMotionKind.Yellow && TryGetComponent<YellowLouieKickAbility>(out var yellow))
            { yellow.PresentNetworkKick(abilityPoseDirection, false); yellow.PresentNetworkKick(abilityPoseDirection, true, progress); }
            if (abilityPoseKind == BombMotionKind.Punch && TryGetComponent<BombPunchAbility>(out var punch))
            { punch.PresentNetworkPunch(abilityPoseDirection, false); punch.PresentNetworkPunch(abilityPoseDirection, true, progress); }
        }

        static Vector2 SnapCell(Vector2 position, float tile) => new Vector2(Mathf.Round(position.x / tile), Mathf.Round(position.y / tile)) * tile;

        bool PredictFlight(Bomb target, BombMotionKind kind, Vector2 direction, MovementController movement, int distance, uint tick, float yOffset)
        {
            Vector2 origin = kind == BombMotionKind.Throw ? SnapCell(movement.transform.position, movement.tileSize) : SnapCell(target.transform.position, movement.tileSize);
            if (!target.TryPreviewNetworkPunch(origin, direction, movement.tileSize, distance, out Vector2 end, out float duration)) return false;
            float lockTime = kind == BombMotionKind.Throw ? 0.1f : GetComponent<BombPunchAbility>().punchLockTime;
            return BeginAbilityPrediction(target, new KickMotion { kind = kind, moving = true, first = true,
                start = origin + Vector2.up * yOffset, end = end, duration = duration, arcHeight = target.punchArcHeight, inputTick = tick }, direction, lockTime);
        }

        public bool BeginAbilityPrediction(Bomb target, KickMotion motion, Vector2 direction, float lockTime)
        {
            if (NetSync.Mode != NetSync.NetMode.Client || !isLocalPlayer || !NetworkClient.active || GamePauseController.IsPaused ||
                target == null || target.HasExploded || !target.TryGetComponent<NetworkIdentity>(out var identity) || identity.netId == 0) return false;
            if (kickPlayback.TryGetValue(target, out var previous) && previous.unconfirmed) return false;
            if (rejectedStarts.TryGetValue(target, out var rejected) && rejected.direction == direction &&
                Vector2.Distance(rejected.origin, motion.start) < 0.1f) return false;
            if (!target.ClaimClientPresentation(this, target.ClientMotionRevision)) return false;
            motion.bombId = identity.netId; motion.serverTime = KickServerNow;
            var playback = new KickPlayback { motion = motion, unconfirmed = true, predictedAt = Time.realtimeSinceStartupAsDouble,
                replicaPosition = target.transform.position, history = new KickInputHistory(motion.inputTick, target.transform.position),
                startInputTick = motion.inputTick, elapsed = motion.kind == BombMotionKind.Punch || motion.kind == BombMotionKind.Throw ? Time.fixedDeltaTime : Time.deltaTime,
                colliderCaptured = previous != null && previous.colliderCaptured, colliderWasEnabled = previous != null && previous.colliderWasEnabled,
                sortingCaptured = previous != null && previous.sortingCaptured, originalSorting = previous != null ? previous.originalSorting : 0 };
            playback.presented = SamplePlayback(target, playback);
            playback.history.Remember(motion.inputTick, playback.presented);
            kickPlayback[target] = playback;
            ApplyMotionCollider(target, playback);
            ApplyKickPosition(target, playback.presented);
            abilityLockRemaining = Mathf.Max(0, lockTime); abilityPoseRemaining = abilityPoseDuration = Mathf.Max(0, lockTime);
            abilityPoseFrame = Time.frameCount;
            abilityPoseKind = motion.kind; abilityPoseDirection = direction; abilityPoseActive = true;
            if (motion.kind == BombMotionKind.Punch)
            {
                PlayLocalAbilitySound("Sounds/PunchBomb", 1);
                playback.history.predictedSoundPlayed = true;
            }
            if (GameplayLatencyDiagnostics.Enabled)
            {
                var entry = GameplayLatencyDiagnostics.For("bomb_motion_prediction_begin", GetComponent<MovementController>(), target, motion.kind.ToString());
                entry.inputTick = motion.inputTick; entry.durationMs = 0;
                GameplayLatencyDiagnostics.Write(entry);
            }
            UpdateLocalAbilityPresentation();
            return true;
        }

        void ApplyMotionCollider(Bomb target, KickPlayback playback)
        {
            bool nonBlocking = playback.motion.moving && NonBlockingMotion(playback.motion.kind);
            if (nonBlocking && target.TryGetComponent<Collider2D>(out var collider))
            {
                if (!playback.colliderCaptured) { playback.colliderCaptured = true; playback.colliderWasEnabled = collider.enabled; }
                collider.enabled = false;
            }
            else RestoreMotionCollider(target, playback);
            if (nonBlocking && target.TryGetComponent<SpriteRenderer>(out var sprite))
            {
                if (!playback.sortingCaptured) { playback.sortingCaptured = true; playback.originalSorting = sprite.sortingOrder; }
                sprite.sortingOrder = playback.motion.kind == BombMotionKind.Lift || playback.motion.kind == BombMotionKind.Carry ? 6 : playback.originalSorting;
            }
            target.ClientMotionNonBlocking = nonBlocking;
        }

        void RestoreMotionCollider(Bomb target, KickPlayback playback)
        {
            if (target == null) return;
            if (playback.colliderCaptured && target.TryGetComponent<Collider2D>(out var collider))
                collider.enabled = playback.colliderWasEnabled;
            playback.colliderCaptured = false;
            if (playback.sortingCaptured && target.TryGetComponent<SpriteRenderer>(out var sprite)) sprite.sortingOrder = playback.originalSorting;
            playback.sortingCaptured = false;
            target.ClientMotionNonBlocking = false;
        }

        public void ForgetClientBomb(Bomb target)
        {
            if (target != null && kickPlayback.TryGetValue(target, out var state))
            { RestoreMotionCollider(target, state); RestoreAbilityVisuals(state.motion.kind); kickPlayback.Remove(target); }
            rejectedStarts.Remove(target);
        }

        void UpdateLocalAbilityPresentation()
        {
            if (!isLocalPlayer || NetSync.Mode != NetSync.NetMode.Client) return;
            var movement = GetComponent<MovementController>();
            if (movement == null || movement.isDead || movement.IsEndingStage) { ResetLocalAbilityPresentation(); return; }
            if (!GamePauseController.IsPaused && abilityPoseFrame != Time.frameCount)
            {
                abilityLockRemaining = Mathf.Max(0, abilityLockRemaining - Time.deltaTime);
                abilityPoseRemaining = Mathf.Max(0, abilityPoseRemaining - Time.deltaTime);
                abilityPoseFrame = Time.frameCount;
            }
            bool carrying = CarryingBombLocally;
            if (carrying && TryGetComponent<PowerGloveAbility>(out var glove))
            {
                float progress = 1;
                foreach (var pair in kickPlayback)
                    if (pair.Value.motion.kind == BombMotionKind.Lift && pair.Value.motion.moving)
                        progress = pair.Value.motion.duration <= 0 ? 1 : Mathf.Clamp01(pair.Value.elapsed / pair.Value.motion.duration);
                glove.PresentNetworkGlove(movement.Direction != Vector2.zero ? movement.Direction : movement.FacingDirection, progress, true, false);
                return;
            }
            if (abilityPoseRemaining > 0 && abilityPoseActive)
            {
                if (abilityPoseKind == BombMotionKind.Punch && TryGetComponent<BombPunchAbility>(out var punch))
                    punch.PresentNetworkPunch(abilityPoseDirection, true, 1 - abilityPoseRemaining / Mathf.Max(0.001f, abilityPoseDuration));
                else if (abilityPoseKind == BombMotionKind.Throw && TryGetComponent<PowerGloveAbility>(out var throwing))
                    throwing.PresentNetworkGlove(abilityPoseDirection, 1 - abilityPoseRemaining / Mathf.Max(0.001f, abilityPoseDuration), false, true);
                else if (abilityPoseKind == BombMotionKind.Yellow && TryGetComponent<YellowLouieKickAbility>(out var yellow))
                    yellow.PresentNetworkKick(abilityPoseDirection, true, 1 - abilityPoseRemaining / Mathf.Max(0.001f, abilityPoseDuration));
            }
            else if (abilityPoseActive) RestoreAbilityVisuals(abilityPoseKind);
        }

        void BeginConfirmedAbilityPose(KickMotion motion, float age)
        {
            float duration = motion.kind == BombMotionKind.Yellow && TryGetComponent<YellowLouieKickAbility>(out var yellow) ? yellow.kickCooldownSeconds :
                motion.kind == BombMotionKind.Punch && TryGetComponent<BombPunchAbility>(out var punch) ? punch.punchLockTime : 0.1f;
            abilityPoseKind = motion.kind; abilityPoseDirection = (motion.end - motion.start).normalized;
            abilityPoseDuration = duration; abilityPoseRemaining = Mathf.Max(0, duration - age);
            abilityPoseFrame = Time.frameCount; abilityPoseActive = true;
        }

        void RestoreAbilityVisuals(BombMotionKind kind)
        {
            if (!isLocalPlayer || NetSync.Mode != NetSync.NetMode.Client) return;
            bool gloveKind = kind == BombMotionKind.Lift || kind == BombMotionKind.Carry;
            bool glovePose = abilityPoseKind == BombMotionKind.Lift || abilityPoseKind == BombMotionKind.Carry;
            if (kind != abilityPoseKind && !(gloveKind && glovePose)) return;
            abilityLockRemaining = abilityPoseRemaining = 0;
            if (kind == BombMotionKind.Punch) GetComponent<BombPunchAbility>()?.PresentNetworkPunch(Vector2.down, false);
            if (kind == BombMotionKind.Lift || kind == BombMotionKind.Carry || kind == BombMotionKind.Throw)
                GetComponent<PowerGloveAbility>()?.PresentNetworkGlove(Vector2.down, 0, false, false);
            if (kind == BombMotionKind.Yellow) GetComponent<YellowLouieKickAbility>()?.PresentNetworkKick(Vector2.down, false);
            abilityPoseActive = false;
        }

        void ResetLocalAbilityPresentation()
        {
            if (abilityPoseActive) RestoreAbilityVisuals(abilityPoseKind);
            abilityLockRemaining = abilityPoseRemaining = 0; previousAbilityHeld = 0;
        }

        sealed class TileMotionView
        {
            public Tilemap map;
            public TileBase tile;
            public GameObject visual, originBlocker, destinationBlocker;
            public Vector3 from, to;
            public float elapsed, duration;
        }
        uint nextTileMotion;
        readonly Dictionary<uint, TileMotionView> tileMotions = new();
        readonly List<uint> expiredTileMotions = new();

        public uint ServerBeginTileMotion(Vector3Int cell)
        {
            if (!NetSync.IsServer || !isServer || netId == 0) return 0;
            uint id = ++nextTileMotion;
            RpcBeginTileMotion(id, cell);
            return id;
        }

        [Server]
        public void ServerMoveTileMotion(uint id, Vector3Int from, Vector3Int to, float duration)
        { if (id != 0) RpcMoveTileMotion(id, from, to, duration); }

        [Server]
        public void ServerEndTileMotion(uint id, Vector3Int cell, bool destroyed)
        { if (id != 0) RpcEndTileMotion(id, cell, destroyed); }

        [ClientRpc]
        void RpcBeginTileMotion(uint id, Vector3Int cell) => ReceiveTileStart(id, cell, GameManager.Instance != null ? GameManager.Instance.destructibleTilemap : null);

        public void ReceiveTileStart(uint id, Vector3Int cell, Tilemap map)
        {
            if (isServer || NetSync.Mode != NetSync.NetMode.Client || !NetworkClient.active) return;
            if (map == null || !map.HasTile(cell) || tileMotions.ContainsKey(id)) return;
            var tile = map.GetTile(cell);
            var sprite = map.GetSprite(cell);
            var ghost = new GameObject("NetworkYellowTile", typeof(SpriteRenderer));
            ghost.transform.position = map.GetCellCenterWorld(cell);
            var renderer = ghost.GetComponent<SpriteRenderer>(); renderer.sprite = sprite;
            if (map.TryGetComponent<TilemapRenderer>(out var source))
            { renderer.sortingLayerID = source.sortingLayerID; renderer.sortingOrder = source.sortingOrder + 1; renderer.sharedMaterial = source.sharedMaterial; }
            tileMotions[id] = new TileMotionView { map = map, tile = tile, visual = ghost, from = ghost.transform.position, to = ghost.transform.position,
                originBlocker = CreateConfirmedTileBlocker(ghost.transform.position, map) };
            map.SetTile(cell, null);
        }

        [ClientRpc]
        void RpcMoveTileMotion(uint id, Vector3Int from, Vector3Int to, float duration) => ReceiveTileMove(id, from, to, duration);

        public void ReceiveTileMove(uint id, Vector3Int from, Vector3Int to, float duration)
        {
            if (isServer || !tileMotions.TryGetValue(id, out var state) || state.map == null) return;
            state.from = state.map.GetCellCenterWorld(from); state.to = state.map.GetCellCenterWorld(to);
            state.elapsed = 0; state.duration = Mathf.Max(0.001f, duration);
            state.originBlocker.transform.position = state.from;
            if (state.destinationBlocker == null) state.destinationBlocker = CreateConfirmedTileBlocker(state.to, state.map);
            state.destinationBlocker.transform.position = state.to; state.destinationBlocker.SetActive(true);
            Physics2D.SyncTransforms();
        }

        [ClientRpc]
        void RpcEndTileMotion(uint id, Vector3Int cell, bool destroyed) => ReceiveTileEnd(id, cell, destroyed);

        public void ReceiveTileEnd(uint id, Vector3Int cell, bool destroyed)
        {
            if (isServer || !tileMotions.TryGetValue(id, out var state)) return;
            if (!destroyed && state.map != null) state.map.SetTile(cell, state.tile);
            ReleaseTileView(state);
            tileMotions.Remove(id);
        }

        void UpdateTileMotionPresentation()
        {
            expiredTileMotions.Clear();
            foreach (var pair in tileMotions)
            {
                var state = pair.Value;
                if (state.visual == null || state.map == null) { expiredTileMotions.Add(pair.Key); continue; }
                if (!GamePauseController.IsPaused) state.elapsed += Time.deltaTime;
                state.visual.transform.position = Vector3.Lerp(state.from, state.to, state.duration <= 0 ? 1 : Mathf.Clamp01(state.elapsed / state.duration));
                if (state.elapsed >= state.duration && state.destinationBlocker != null && state.destinationBlocker.activeSelf)
                {
                    state.originBlocker.transform.position = state.to; state.destinationBlocker.SetActive(false);
                    Physics2D.SyncTransforms();
                }
            }
            foreach (uint id in expiredTileMotions)
            {
                ReleaseTileView(tileMotions[id]);
                tileMotions.Remove(id);
            }
        }

        // Match the server's current/next-cell reservations. These exist only
        // after an approved tile segment, for the client's collision queries.
        static GameObject CreateConfirmedTileBlocker(Vector3 position, Tilemap map)
        {
            var blocker = new GameObject("NetworkYellowTileReservation");
            blocker.transform.position = position;
            int stage = LayerMask.NameToLayer("Stage");
            if (stage >= 0) blocker.layer = stage;
            var body = blocker.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic; body.gravityScale = 0; body.freezeRotation = true;
            var collider = blocker.AddComponent<BoxCollider2D>();
            float tile = Mathf.Max(0.1f, map.cellSize.x * Mathf.Abs(map.transform.lossyScale.x));
            collider.size = Vector2.one * tile * 0.9f;
            Physics2D.SyncTransforms();
            return blocker;
        }

        static void ReleaseTileView(TileMotionView state)
        {
            if (state.visual != null) ReleaseTileVisual(state.visual);
            if (state.originBlocker != null) ReleaseTileVisual(state.originBlocker);
            if (state.destinationBlocker != null) ReleaseTileVisual(state.destinationBlocker);
        }

        static void ReleaseTileVisual(GameObject visual)
        {
            if (Application.isPlaying) Destroy(visual); else DestroyImmediate(visual);
        }

        void ClearTileMotionPresentation()
        {
            foreach (var state in tileMotions.Values) ReleaseTileView(state);
            tileMotions.Clear();
        }

        void PlayLocalAbilitySound(string resource, float volume)
        {
            var clip = Resources.Load<AudioClip>(resource);
            if (clip != null && TryGetComponent<AudioSource>(out var audio))
                audio.PlayOneShot(clip, GameAudioSettings.ApplySfxVolume(volume));
        }

        [Server]
        public void ServerAbilitySound(Bomb target, string resource, float volume)
        {
            PlayLocalAbilitySound(resource, volume);
            if (target != null && target.TryGetComponent<NetworkIdentity>(out var identity)) RpcAbilitySound(identity.netId, resource, volume);
        }

        [ClientRpc]
        void RpcAbilitySound(uint bombId, string resource, float volume)
        {
            if (isServer) return;
            if (isLocalPlayer && NetworkClient.spawned.TryGetValue(bombId, out var identity) && identity.TryGetComponent<Bomb>(out var target) &&
                kickPlayback.TryGetValue(target, out var state) && state.history?.predictedSoundPlayed == true) return;
            PlayLocalAbilitySound(resource, volume);
        }
    }
}

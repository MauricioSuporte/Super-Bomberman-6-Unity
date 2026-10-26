using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Assets.Scripts.Netcode
{
    /// <summary>Uma célula do visual de explosão a replicar.</summary>
    public struct ExplosionFx
    {
        public Vector2 pos;
        public byte part;   // BombExplosion.ExplosionPart (Start/Middle/End)
        public Vector2 dir;
    }

    /// <summary>
    /// F2 fatia B — replica o VISUAL da explosão (host-autoritativo).
    ///
    /// O objeto BombExplosion NÃO é replicado (usa pool estático incompatível
    /// com o spawn do Mirror). Em vez disso, o host coleta as células visuais
    /// da explosão (centro + braços) e envia por ClientRpc; o cliente recria o
    /// visual localmente usando o mesmo pool, SEM colisão de dano (o dano é
    /// resolvido só no host).
    ///
    /// Vive no Player (dono da bomba) — o BombController do mesmo GameObject
    /// chama <see cref="ServerEmit"/> ao final de ExplodeBomb.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(NetworkIdentity))]
    [RequireComponent(typeof(BombController))]
    public partial class NetworkBombFx : NetworkBehaviour
    {
        BombController bomb;

        void Awake() => bomb = GetComponent<BombController>();

        public struct KickMotion
        {
            public uint bombId, inputTick, revision, bounceSeed;
            public int bounceIndex;
            public float bounceDuration, bounceHeight;
            public BombMotionKind kind;
            public float arcHeight, carryHeight, curveDuration;
            public Vector2 start, end;
            public float duration;
            public double serverTime;
            public bool moving, first, rubber;
        }

        public sealed class KickInputHistory
        {
            readonly Dictionary<uint, Vector2> positions = new();
            readonly Queue<uint> ticks = new();
            public readonly uint startTick;
            public readonly Vector2 origin;
            public bool predictedSoundPlayed, holdPlayer;
            public float presentationLead;
            public KickInputHistory(uint tick, Vector2 start) { startTick = tick; origin = start; }
            public void Remember(uint tick, Vector2 position)
            {
                if (!positions.ContainsKey(tick)) ticks.Enqueue(tick);
                positions[tick] = position;
                if (ticks.Count > NetworkInputHistory.Capacity) positions.Remove(ticks.Dequeue());
            }
            public Vector2 At(uint tick, Vector2 fallback) => NetworkInputHistory.IsNewer(startTick, tick) ? origin :
                positions.TryGetValue(tick, out var position) ? position : fallback;
            public int Count => positions.Count;
        }

        sealed class KickPlayback
        {
            public KickMotion motion;
            public float elapsed, staleSeconds;
            public double endpointWaitAt = -1;
            public Vector2 presented, correction;
            public bool predictedContinuation, unconfirmed, predictedBounce, predictedLandingBounce;
            public uint startInputTick;
            public bool colliderWasEnabled, colliderCaptured, sortingCaptured;
            public int originalSorting;
            public double nextPresentationSample;
            public Vector2 sampledPresentation;
            public bool hasPresentationSample;
            public Vector2 replicaPosition;
            public double predictedAt;
            public KickInputHistory history;
            public Queue<KickMotion> pendingArcs = new();
        }

        readonly Dictionary<Bomb, KickPlayback> kickPlayback = new();
        readonly List<Bomb> expiredKicks = new();
        readonly List<KickMotion> readyBombArcs = new();
        bool applyingQueuedBombArc;
        readonly Dictionary<Bomb, (Vector2 origin, Vector2 direction)> rejectedStarts = new();

        public static float InitialKickPresentationLead(Vector2 presented, KickMotion motion, float serverAge)
        {
            Vector2 step = motion.end - motion.start;
            if (!motion.moving || motion.duration <= 0 || step.sqrMagnitude < 0.0001f) return 0;
            float progressSeconds = Vector2.Dot(presented - motion.start, step) / step.sqrMagnitude * motion.duration;
            return Mathf.Clamp(progressSeconds - serverAge, 0, 0.15f);
        }

        public static bool PredictionAcknowledged(uint acknowledged, uint started) =>
            acknowledged == started || NetworkInputHistory.IsNewer(acknowledged, started);

        public bool TryPredictKick(Bomb movingBomb, Vector2 direction, float tileSize)
        {
            if (NetSync.Mode != NetSync.NetMode.Client || isServer || !isLocalPlayer || !NetworkClient.active ||
                GamePauseController.IsPaused || movingBomb == null || movingBomb.HasExploded ||
                movingBomb.IsBeingKicked || movingBomb.IsBeingPunched || movingBomb.IsBeingHeldByPowerGlove ||
                tileSize <= 0 || direction == Vector2.zero || kickPlayback.ContainsKey(movingBomb) ||
                !movingBomb.TryGetComponent<NetworkIdentity>(out var identity) || identity.netId == 0 ||
                !TryGetComponent<NetworkPlayerInput>(out var input)) return false;
            if (movingBomb.TryGetComponent<NetworkTransformReliable>(out var sync) && sync.clientSnapshots.Count >= 2)
            {
                int last = sync.clientSnapshots.Count - 1;
                if ((sync.clientSnapshots.Values[last].position - sync.clientSnapshots.Values[last - 1].position).sqrMagnitude > 0.0001f)
                    return false; // already moving on the authoritative timeline
            }
            Vector2 origin = new Vector2(Mathf.Round(movingBomb.transform.position.x / tileSize),
                Mathf.Round(movingBomb.transform.position.y / tileSize)) * tileSize;
            direction = direction.normalized;
            if (rejectedStarts.TryGetValue(movingBomb, out var rejected) && rejected.direction == direction &&
                Vector2.Distance(rejected.origin, origin) < tileSize * 0.1f) return false;
            if (!movingBomb.CanPredictKickContinuation(origin + direction * tileSize, tileSize)) return false;
            float duration = Mathf.Ceil((tileSize / Mathf.Max(0.001f, movingBomb.kickSpeed)) / Time.fixedDeltaTime) * Time.fixedDeltaTime;
            var history = new KickInputHistory(input.CurrentMovementTick, origin);
            history.Remember(input.CurrentMovementTick, origin);
            var playback = new KickPlayback { unconfirmed = true, history = history, startInputTick = input.CurrentMovementTick,
                predictedAt = Time.realtimeSinceStartupAsDouble, replicaPosition = movingBomb.transform.position,
                motion = new KickMotion { bombId = identity.netId, inputTick = input.CurrentMovementTick,
                    start = origin, end = origin + direction * tileSize, duration = duration,
                    moving = true, first = true, serverTime = KickServerNow }, elapsed = Time.fixedDeltaTime };
            playback.presented = SamplePlayback(movingBomb, playback);
            history.Remember(input.CurrentMovementTick, playback.presented);
            movingBomb.ClaimClientPresentation(this, movingBomb.ClientMotionRevision);
            kickPlayback[movingBomb] = playback;
            ApplyKickPosition(movingBomb, playback.presented);
            if (TryGetComponent<BombKickAbility>(out var kickAbility))
            {
                kickAbility.PlayNetworkKickSound();
                history.predictedSoundPlayed = true;
            }
            if (GameplayLatencyDiagnostics.Enabled)
            {
                var entry = GameplayLatencyDiagnostics.For("kick_prediction_begin", GetComponent<MovementController>(), movingBomb,
                    "local contact; gameplay remains authoritative; durationMs=local feedback wait");
                entry.inputTick = history.startTick; entry.durationMs = 0;
                entry.delta = playback.presented - origin;
                GameplayLatencyDiagnostics.Write(entry);
            }
            return true;
        }

        public void PrepareClientMovementTick(uint tick, bool replay, int held)
        {
            if (!NetSync.IsOnline || isServer || !isLocalPlayer || !NetworkClient.active) return;
            if (!replay)
            {
                Vector2 direction = MotionDiagnosticProbe.DirectionFromMask(held);
                expiredKicks.Clear();
                foreach (var pair in rejectedStarts)
                    if (pair.Key == null || pair.Value.direction != direction) expiredKicks.Add(pair.Key);
                foreach (var key in expiredKicks) rejectedStarts.Remove(key);
            }
            foreach (var pair in kickPlayback)
            {
                if (pair.Key == null || pair.Key.HasExploded) continue;
                var playback = pair.Value;
                if (!replay) playback.history?.Remember(tick, playback.presented);
                ApplyKickPosition(pair.Key, replay && playback.history != null ?
                    playback.history.At(tick, playback.presented) : playback.presented);
            }
        }

        // The motor must use the historical presentation state, not Bomb.isKicked,
        // which belongs exclusively to the server's gameplay coroutine.
        public bool IsKickMovingForTick(Bomb movingBomb, uint tick)
        {
            if (NetSync.Mode != NetSync.NetMode.Client || !isLocalPlayer || !NetworkClient.active ||
                movingBomb == null || movingBomb.HasExploded || !movingBomb.isActiveAndEnabled ||
                !kickPlayback.TryGetValue(movingBomb, out var playback)) return false;
            uint start = playback.history != null ? playback.history.startTick : playback.startInputTick;
            return (playback.motion.kind == BombMotionKind.Kick || playback.motion.kind == BombMotionKind.Yellow) &&
                PredictionAcknowledged(tick, start) &&
                (playback.motion.moving || !PredictionAcknowledged(tick, playback.motion.inputTick));
        }

        public Bomb LastKickForTick(uint tick)
        {
            Bomb latest = null;
            uint latestTick = 0;
            foreach (var pair in kickPlayback)
            {
                if (!IsKickMovingForTick(pair.Key, tick)) continue;
                uint start = pair.Value.history != null ? pair.Value.history.startTick : pair.Value.startInputTick;
                if (latest == null || NetworkInputHistory.IsNewer(start, latestTick))
                { latest = pair.Key; latestTick = start; }
            }
            return latest;
        }

        public void MarkInitialMotorHold(Bomb movingBomb)
        {
            if (kickPlayback.TryGetValue(movingBomb, out var playback) && playback.unconfirmed)
            {
                playback.history.holdPlayer = true;
                playback.history.Remember(playback.history.startTick, playback.history.origin);
            }
        }

        public bool ReplayInitialKick(uint tick, Vector2 direction)
        {
            foreach (var pair in kickPlayback)
                if (pair.Key != null && pair.Value.history != null && pair.Value.history.holdPlayer && pair.Value.history.startTick == tick &&
                    Vector2.Dot(pair.Value.motion.end - pair.Value.motion.start, direction) > 0) return true;
            return false;
        }

        public void ReconcilePredictedKicks(uint acknowledgedTick)
        {
            expiredKicks.Clear();
            foreach (var pair in kickPlayback)
                if (pair.Value.unconfirmed && PredictionAcknowledged(acknowledgedTick, pair.Value.history.startTick))
                {
                    RollbackPrediction(pair.Key, pair.Value, "server_processed_contact_without_kick", acknowledgedTick);
                    expiredKicks.Add(pair.Key);
                }
            foreach (var key in expiredKicks) kickPlayback.Remove(key);
        }

        void RollbackPrediction(Bomb movingBomb, KickPlayback playback, string reason, uint acknowledgedTick = 0)
        {
            if (movingBomb == null || movingBomb.HasExploded) return;
            rejectedStarts[movingBomb] = (playback.motion.start, (playback.motion.end - playback.motion.start).normalized);
            playback.replicaPosition = LatestReplicaPosition(movingBomb, playback.replicaPosition);
            RestoreMotionCollider(movingBomb, playback);
            ApplyKickPosition(movingBomb, playback.replicaPosition);
            RestoreAbilityVisuals(playback.motion.kind);
            if (GameplayLatencyDiagnostics.Enabled)
            {
                var entry = GameplayLatencyDiagnostics.For(playback.motion.kind == BombMotionKind.Kick ? "kick_prediction_reject" : "bomb_motion_prediction_reject", GetComponent<MovementController>(), movingBomb, reason + ";kind=" + playback.motion.kind);
                entry.inputTick = playback.history.startTick; entry.authoritativeTick = acknowledgedTick;
                entry.durationMs = (Time.realtimeSinceStartupAsDouble - playback.predictedAt) * 1000;
                entry.delta = playback.replicaPosition - playback.presented;
                GameplayLatencyDiagnostics.Write(entry);
            }
        }

        // predictedTime is ahead by one-way latency. Remove that lead, rather than
        // adding the snapshot interpolation buffer to an already confirmed kick.
        public static double KickServerNow => NetworkTime.predictedTime - NetworkTime.rtt * 0.5;

        public static Vector2 SampleKickSegment(Vector2 start, Vector2 end, float duration, float elapsed) =>
            duration <= 0 ? end : Vector2.Lerp(start, end, Mathf.Clamp01(elapsed / duration));

        // At most one tile beyond the last confirmation and 150 ms without a new
        // segment. This bridges message jitter without predicting an entire corridor.
        public static Vector2 SampleKickContinuation(Vector2 start, Vector2 end, float duration, float elapsed, bool exitClear)
        {
            if (!exitClear || duration <= 0) return SampleKickSegment(start, end, duration, elapsed);
            float extra = Mathf.Min(duration, 0.15f);
            return Vector2.LerpUnclamped(start, end, Mathf.Clamp(elapsed / duration, 0, 1 + extra / duration));
        }

        Vector2 SamplePlayback(Bomb movingBomb, KickPlayback playback)
        {
            var motion = playback.motion;
            if (!motion.moving) return motion.end;
            bool continueLocally = false;
            if (motion.moving && motion.kind != BombMotionKind.Punch && motion.kind != BombMotionKind.Throw &&
                motion.kind != BombMotionKind.Lift && motion.kind != BombMotionKind.Carry && playback.elapsed > motion.duration && motion.duration > 0)
            {
                var step = motion.end - motion.start;
                continueLocally = movingBomb.CanPredictKickContinuation(motion.end + step, step.magnitude);
                if (!continueLocally && motion.rubber && movingBomb.CanPredictRubberWallBounce(motion.end + step, step.magnitude) &&
                    movingBomb.CanPredictKickContinuation(motion.start, step.magnitude))
                {
                    if (!playback.predictedBounce && GameplayLatencyDiagnostics.Enabled)
                        GameplayLatencyDiagnostics.Write(GameplayLatencyDiagnostics.For("rubber_bounce_prediction", GetComponent<MovementController>(), movingBomb));
                    playback.predictedBounce = true;
                    return SampleBoundedBounce(motion.start, motion.end, motion.duration, playback.elapsed);
                }
                if (continueLocally && !playback.predictedContinuation && GameplayLatencyDiagnostics.Enabled)
                    GameplayLatencyDiagnostics.Write(GameplayLatencyDiagnostics.For("kick_continuation_prediction",
                        GetComponent<MovementController>(), movingBomb));
                playback.predictedContinuation |= continueLocally;
            }
            if (motion.kind == BombMotionKind.Lift || motion.kind == BombMotionKind.Carry)
                return SampleCarry(motion, transform.position, playback.elapsed);
            if (IsFlightArc(motion.kind) && motion.rubber && motion.bounceDuration > 0 && playback.elapsed > motion.duration &&
                motion.bounceIndex + 1 < RubberBombBounceSequence.MinimumBounces(motion.bounceSeed))
            {
                Vector2 direction = RubberBombBounceSequence.Direction(motion.bounceSeed, motion.bounceIndex + 1);
                float tile = TryGetComponent<MovementController>(out var actor) ? actor.tileSize : 1;
                if (movingBomb.TryPreviewNetworkPunch(motion.end, direction, tile, 1, out var next, out _) &&
                    movingBomb.CanPredictKickContinuation(next, tile))
                {
                    if (!playback.predictedLandingBounce && GameplayLatencyDiagnostics.Enabled)
                        GameplayLatencyDiagnostics.Write(GameplayLatencyDiagnostics.For("rubber_landing_prediction", actor, movingBomb,
                            "seed=" + motion.bounceSeed + ";index=" + (motion.bounceIndex + 1)));
                    playback.predictedLandingBounce = true;
                    float extra = Mathf.Clamp(playback.elapsed - motion.duration, 0, Mathf.Min(motion.bounceDuration, 0.15f));
                    return movingBomb.SnapNetworkPunchPosition(SampleArc(motion.end, next, motion.bounceDuration, extra, motion.bounceHeight));
                }
            }
            if (motion.kind == BombMotionKind.Punch || motion.kind == BombMotionKind.Throw)
                return movingBomb.SnapNetworkPunchPosition(SampleArc(motion.start, motion.end, motion.curveDuration > 0 ? motion.curveDuration : motion.duration, playback.elapsed, motion.arcHeight));
            return SampleKickContinuation(motion.start, motion.end, motion.duration, playback.elapsed,
                continueLocally && motion.kind != BombMotionKind.Punch && motion.kind != BombMotionKind.Throw);
        }

        [Server]
        public void ServerEmitKickMotion(Bomb movingBomb, Vector2 start, Vector2 end, float duration, bool moving, bool first)
        {
            if (connectionToClient == null || connectionToClient == NetworkServer.localConnection || movingBomb == null ||
                !movingBomb.TryGetComponent<NetworkIdentity>(out var identity) || identity.netId == 0) return;
            var motion = new KickMotion { bombId = identity.netId, start = start, end = end,
                duration = duration, moving = moving, first = first, serverTime = NetworkTime.localTime, revision = movingBomb.NextNetworkMotionRevision(), rubber = movingBomb.IsRubberBomb,
                inputTick = TryGetComponent<NetworkPlayerInput>(out var input) ? input.CurrentInputTick : 0 };
            if (GameplayLatencyDiagnostics.Enabled)
            {
                var entry = GameplayLatencyDiagnostics.For(moving ? "kick_segment_send" : "kick_stop_send",
                    GetComponent<MovementController>(), movingBomb);
                entry.delta = end - start;
                entry.durationMs = duration * 1000;
                entry.authoritativeTick = motion.inputTick;
                entry.detail = first ? "first_segment" : "continuation";
                GameplayLatencyDiagnostics.Write(entry);
            }
            TargetKickMotion(connectionToClient, motion);
        }

        [TargetRpc]
        void TargetKickMotion(NetworkConnectionToClient target, KickMotion motion) => ReceiveKickMotion(motion);

        public void ReceiveKickMotion(KickMotion motion)
        {
            if (!NetSync.IsOnline || isServer || !NetworkClient.active || (motion.kind == BombMotionKind.Kick && !isLocalPlayer)) return;
            if (!NetworkClient.spawned.TryGetValue(motion.bombId, out var identity) ||
                !identity.TryGetComponent<Bomb>(out var movingBomb)) return;
            // Another player's last search/kick stop can arrive while our action
            // is in flight. Our own input acknowledgement resolves this prediction.
            if (movingBomb.HasPendingPresentationOtherThan(this)) return;
            if (motion.revision != movingBomb.ClientMotionRevision &&
                !NetworkInputHistory.IsNewer(motion.revision, movingBomb.ClientMotionRevision)) return;
            kickPlayback.TryGetValue(movingBomb, out var previous);
            if (previous != null && previous.unconfirmed && previous.motion.kind != motion.kind &&
                !NetworkInputHistory.IsNewer(motion.inputTick, previous.history.startTick)) return;
            bool arcContinuation = previous != null && !previous.unconfirmed && !motion.first &&
                IsFlightArc(previous.motion.kind) && previous.motion.kind == motion.kind && previous.motion.moving;
            if (arcContinuation && !applyingQueuedBombArc &&
                (previous.elapsed < previous.motion.duration || previous.pendingArcs.Count > 0))
            {
                if (previous.pendingArcs.Count < 32) { previous.pendingArcs.Enqueue(motion); return; }
                previous.pendingArcs.Clear(); // Bounded recovery after a long delivery stall.
            }
            if (!movingBomb.ClaimClientPresentation(this, motion.revision)) return;
            if (previous != null && !previous.unconfirmed && previous.motion.serverTime > motion.serverTime) return;
            if (previous != null && previous.endpointWaitAt >= 0 && GameplayLatencyDiagnostics.Enabled)
            {
                var wait = GameplayLatencyDiagnostics.For("kick_endpoint_wait", GetComponent<MovementController>(), movingBomb,
                    previous.predictedContinuation ? "confirmation_gap_with_local_continuation" : "confirmation_gap_blocked_exit");
                wait.durationMs = (previous.staleSeconds - previous.endpointWaitAt) * 1000;
                wait.authoritativeTick = motion.inputTick;
                GameplayLatencyDiagnostics.Write(wait);
            }
            float age = GamePauseController.IsPaused ? 0 : Mathf.Max(0, (float)(KickServerNow - motion.serverTime));
            var playback = new KickPlayback { motion = motion, elapsed = arcContinuation ?
                (previous.predictedLandingBounce && motion.bounceSeed == previous.motion.bounceSeed && motion.bounceIndex == previous.motion.bounceIndex + 1 ?
                    Mathf.Clamp(previous.elapsed - previous.motion.duration, 0, Mathf.Min(0.15f, motion.duration)) : ArcRemainder(previous.elapsed, previous.motion.duration)) :
                motion.kind == BombMotionKind.Search || motion.kind == BombMotionKind.Magnet ? 0 : age,
                pendingArcs = arcContinuation ? previous.pendingArcs : new Queue<KickMotion>(),
                nextPresentationSample = previous != null ? previous.nextPresentationSample : 0,
                sampledPresentation = previous != null ? previous.sampledPresentation : Vector2.zero,
                hasPresentationSample = previous != null && previous.hasPresentationSample,
                colliderCaptured = previous != null && previous.colliderCaptured,
                colliderWasEnabled = previous != null && previous.colliderWasEnabled,
                sortingCaptured = previous != null && previous.sortingCaptured,
                originalSorting = previous != null ? previous.originalSorting : 0,
                startInputTick = previous != null && (previous.unconfirmed || !motion.first) ? previous.startInputTick : motion.inputTick,
                history = previous != null && (previous.unconfirmed || !motion.first) ? previous.history : null };
            if (previous != null && previous.unconfirmed)
            {
                playback.history.presentationLead = motion.kind == BombMotionKind.Kick ? InitialKickPresentationLead(previous.presented, motion, age) :
                    Mathf.Clamp(previous.elapsed - age, 0, 0.15f);
                rejectedStarts.Remove(movingBomb);
                if (GameplayLatencyDiagnostics.Enabled)
                {
                    var confirmation = GameplayLatencyDiagnostics.For(motion.kind == BombMotionKind.Kick ? "kick_prediction_confirm" : "bomb_motion_prediction_confirm", GetComponent<MovementController>(), movingBomb);
                    confirmation.inputTick = previous.history.startTick; confirmation.authoritativeTick = motion.inputTick;
                    confirmation.durationMs = (Time.realtimeSinceStartupAsDouble - previous.predictedAt) * 1000;
                    confirmation.detail = "kind=" + motion.kind + ";owner_input_timeline_lead_ms=" + playback.history.presentationLead * 1000;
                    confirmation.delta = SampleKickSegment(motion.start, motion.end, motion.duration, age) - previous.presented;
                    GameplayLatencyDiagnostics.Write(confirmation);
                }
            }
            // Preserve the small lead of the owner's input timeline. Pulling the
            // bomb back to server-now on the first reply would recreate the hitch.
            if (motion.moving && playback.history != null && !arcContinuation) playback.elapsed += playback.history.presentationLead;
            var targetPosition = SamplePlayback(movingBomb, playback);
            // The NetworkTransform may have written its older timeline this frame.
            // Reconcile against our last displayed position, not that stale transform.
            if (previous != null && (!motion.first || previous.unconfirmed) && motion.moving && previous.motion.moving &&
                Vector2.Dot(motion.end - motion.start, previous.motion.end - previous.motion.start) > 0 &&
                Vector2.Distance(previous.presented, targetPosition) < 0.75f * (motion.end - motion.start).magnitude)
                playback.correction = previous.presented - targetPosition;
            playback.presented = targetPosition + playback.correction;
            kickPlayback[movingBomb] = playback;
            if (motion.kind == BombMotionKind.Kick && (motion.first || !motion.moving))
                MotionDiagnosticProbe.KickFeedback(gameObject, movingBomb, motion.moving, motion.inputTick, age * 1000);
            if (GameplayLatencyDiagnostics.Enabled)
            {
                var segment = GameplayLatencyDiagnostics.For(motion.kind == BombMotionKind.Kick ? "kick_segment_apply" : "bomb_motion_apply", GetComponent<MovementController>(), movingBomb,
                    "kind=" + motion.kind + ";" + (motion.first ? "first_segment" : motion.moving ? "continuation" : "stop"));
                segment.authoritativeTick = motion.inputTick;
                segment.messageAgeMs = age * 1000;
                segment.durationMs = motion.duration * 1000;
                segment.delta = playback.presented - (previous != null ? previous.presented : (Vector2)movingBomb.transform.position);
                segment.predictionError = targetPosition - (previous != null ? previous.presented : (Vector2)movingBomb.transform.position);
                GameplayLatencyDiagnostics.Write(segment);
            }
            // No global physics replay, no StartKick, no audio or gameplay effects.
            // The normal NetworkTransform remains enabled to receive/decode deltas.
            ApplyMotionCollider(movingBomb, playback);
            ApplyKickPosition(movingBomb, playback.presented);
            if (isLocalPlayer && previous?.unconfirmed != true && motion.first && motion.moving && motion.kind != BombMotionKind.Kick &&
                motion.kind != BombMotionKind.Search && motion.kind != BombMotionKind.Magnet)
                BeginConfirmedAbilityPose(motion, age);
        }

        void LateUpdate() => PresentFrame(Time.deltaTime, Time.unscaledDeltaTime);

        void PresentFrame(float deltaTime, float unscaledDeltaTime)
        {
            if (!NetSync.IsOnline || isServer || !NetworkClient.active)
            {
                ClearKickPlayback();
                return;
            }
            UpdateTileMotionPresentation();
            UpdatePlacementPresentation(unscaledDeltaTime);
            UpdatePlayerBouncePresentation(deltaTime, unscaledDeltaTime);
            UpdateGreenDash(deltaTime, unscaledDeltaTime);
            readyBombArcs.Clear();
            expiredKicks.Clear();
            foreach (var pair in kickPlayback)
            {
                var movingBomb = pair.Key;
                var playback = pair.Value;
                Vector2 bufferedPosition = movingBomb != null ? (Vector2)movingBomb.transform.position : Vector2.zero;
                if (movingBomb == null || !movingBomb.isActiveAndEnabled || movingBomb.HasExploded)
                { expiredKicks.Add(movingBomb); continue; }
                if (playback.unconfirmed)
                {
                    // NetworkTransform is still decoding/updating its authoritative
                    // timeline; retain that replica as the rollback destination.
                    playback.replicaPosition = LatestReplicaPosition(movingBomb, playback.replicaPosition);
                    if (playback.staleSeconds > Mathf.Clamp((float)NetworkTime.rtt * 3, 0.5f, 1f))
                    {
                        RollbackPrediction(movingBomb, playback, "confirmation_timeout");
                        expiredKicks.Add(movingBomb); continue;
                    }
                }
                if (!GamePauseController.IsPaused)
                {
                    playback.elapsed += deltaTime;
                    playback.staleSeconds += unscaledDeltaTime;
                }
                var motion = playback.motion;
                bool hasReadyArc = playback.pendingArcs.Count > 0 && playback.elapsed >= motion.duration;
                if (hasReadyArc)
                    readyBombArcs.Add(playback.pendingArcs.Dequeue());
                if (motion.moving && playback.elapsed >= motion.duration && playback.endpointWaitAt < 0)
                    playback.endpointWaitAt = playback.staleSeconds;
                // Decay small corrections over a few frames rather than jumping at
                // every segment boundary. Stops/bounces still apply immediately.
                if (!GamePauseController.IsPaused)
                    playback.correction = Vector2.MoveTowards(playback.correction, Vector2.zero,
                        deltaTime * 0.5f * (motion.end - motion.start).magnitude / Mathf.Max(0.001f, motion.duration));
                playback.presented = SamplePlayback(movingBomb, playback) + playback.correction;
                ApplyKickPosition(movingBomb, playback.presented);
                double now = Time.realtimeSinceStartupAsDouble;
                if (GameplayLatencyDiagnostics.Enabled && now >= playback.nextPresentationSample)
                {
                    var sample = GameplayLatencyDiagnostics.For(motion.kind == BombMotionKind.Kick ? "kick_presentation_sample" : "bomb_motion_sample", GetComponent<MovementController>(), movingBomb,
                        "kind=" + motion.kind + ";after presenter; delta=displayed displacement since previous presentation sample");
                    sample.delta = playback.hasPresentationSample ? playback.presented - playback.sampledPresentation : Vector2.zero;
                    sample.durationMs = playback.hasPresentationSample ? (now - (playback.nextPresentationSample - 0.1)) * 1000 : -1;
                    GameplayLatencyDiagnostics.Write(sample);
                    playback.sampledPresentation = playback.presented;
                    playback.hasPresentationSample = true;
                    playback.nextPresentationSample = now + 0.1;
                }
                double transformOffset = 0;
                if (movingBomb.TryGetComponent<NetworkTransformReliable>(out var sync))
                    transformOffset = NetworkClient.sendInterval * (sync.sendIntervalMultiplier - 1 +
                        (sync.timelineOffset ? sync.sendIntervalMultiplier : 0));
                bool caughtUp = !motion.moving && NetworkTime.time >= motion.serverTime + transformOffset;
                if (!hasReadyArc && (caughtUp || (playback.motion.kind != BombMotionKind.Carry && playback.motion.kind != BombMotionKind.Lift &&
                    playback.staleSeconds > Mathf.Max(1f, motion.duration + 0.5f))))
                {
                    expiredKicks.Add(movingBomb);
                    if (GameplayLatencyDiagnostics.Enabled)
                    {
                        var handoff = GameplayLatencyDiagnostics.For("kick_transform_handoff", GetComponent<MovementController>(),
                            movingBomb, caughtUp ? "timeline_caught_up" : "segment_timeout");
                        handoff.delta = bufferedPosition - playback.presented;
                        GameplayLatencyDiagnostics.Write(handoff);
                    }
                }
            }
            foreach (var movingBomb in expiredKicks)
            {
                if (movingBomb != null && kickPlayback.TryGetValue(movingBomb, out var oldPlayback))
                    RestoreMotionCollider(movingBomb, oldPlayback);
                kickPlayback.Remove(movingBomb);
            }
            applyingQueuedBombArc = true;
            try { foreach (var next in readyBombArcs) ReceiveKickMotion(next); }
            finally { applyingQueuedBombArc = false; }
            UpdateLocalAbilityPresentation();
        }

        public static bool IsFlightArc(BombMotionKind kind) => kind == BombMotionKind.Punch || kind == BombMotionKind.Throw;
        public static float ArcRemainder(float elapsed, float duration) => Mathf.Clamp(elapsed - duration, 0, Time.unscaledDeltaTime);

        // Called before local movement/replay queries. Normal NetworkTransform may
        // have restored the bomb's older buffered position earlier in the frame.
        public void RestoreClientKickPositions()
        {
            if (!NetSync.IsOnline || isServer || !isLocalPlayer || !NetworkClient.active) return;
            foreach (var pair in kickPlayback)
                if (pair.Key != null && pair.Key.isActiveAndEnabled && !pair.Key.HasExploded)
                    ApplyKickPosition(pair.Key, pair.Value.presented);
        }

        static Vector2 LatestReplicaPosition(Bomb movingBomb, Vector2 fallback)
        {
            if (movingBomb.TryGetComponent<NetworkTransformReliable>(out var sync) && sync.clientSnapshots.Count > 0)
                return sync.clientSnapshots.Values[sync.clientSnapshots.Count - 1].position;
            return fallback;
        }

        [Server]
        public void ServerEmitKickSound(Bomb movingBomb)
        {
            if (movingBomb != null && movingBomb.TryGetComponent<NetworkIdentity>(out var identity))
                RpcKickSound(identity.netId);
        }

        [ClientRpc]
        void RpcKickSound(uint bombId)
        {
            if (isServer) return; // host/offline audio already played locally
            if (isLocalPlayer && NetworkClient.spawned.TryGetValue(bombId, out var identity) &&
                identity.TryGetComponent<Bomb>(out var movingBomb) &&
                kickPlayback.TryGetValue(movingBomb, out var playback) && playback.history?.predictedSoundPlayed == true) return;
            GetComponent<BombKickAbility>()?.PlayNetworkKickSound();
        }

        static void ApplyKickPosition(Bomb movingBomb, Vector2 position)
        {
            if (movingBomb.TryGetComponent<Rigidbody2D>(out var body)) body.position = position;
            movingBomb.transform.position = new Vector3(position.x, position.y, movingBomb.transform.position.z);
        }

        void ClearKickPlayback()
        {
            foreach (var pair in kickPlayback)
                if (pair.Value.unconfirmed) RollbackPrediction(pair.Key, pair.Value, "presenter_disabled");
            foreach (var pair in kickPlayback)
                if (pair.Key != null) RestoreMotionCollider(pair.Key, pair.Value);
            kickPlayback.Clear(); rejectedStarts.Clear();
            ResetLocalAbilityPresentation();
            ClearTileMotionPresentation();
            ClearPlayerBouncePresentation();
            ClearPlacementPredictions();
            ClearGreenDash("presenter_disabled");
        }

        void OnDisable() => ClearKickPlayback();

        [Server]
        public void ServerEmit(Vector2 origin, ExplosionFx[] parts, bool pierce, float duration)
        {
            if (parts == null || parts.Length == 0)
                return;
            RpcPlay(origin, parts, pierce, duration, NetworkTime.localTime);
        }

        [ClientRpc]
        void RpcPlay(Vector2 origin, ExplosionFx[] parts, bool pierce, float duration, double time)
        {
            // No host o visual já foi tocado localmente pela simulação.
            if (isServer || bomb == null || BombermanNetworkManager.IsCoopStage)
                return;

            StartCoroutine(PlayOnTimeline(origin, parts, pierce, duration, time));
        }

        IEnumerator PlayOnTimeline(Vector2 origin, ExplosionFx[] parts, bool pierce, float duration, double time)
        {
            while (NetworkClient.active && NetworkTime.time < time) yield return null;
            if (!NetworkClient.active || bomb == null) yield break;
            for (int i = 0; i < parts.Length; i++)
                bomb.PlayNetworkExplosionVisual(parts[i].pos, parts[i].part, parts[i].dir, origin, duration, pierce);

            // Som da explosão (o VFX acima é mudo; o áudio era host-only).
            // Explosion audio arrives through the shared server sound event.
        }

        // F3a — destruição de blocos destrutíveis. O host coleta as células
        // destruídas na explosão e replica; o Tilemap não é networkável, então
        // sincronizamos por evento de célula.
        [Server]
        public void ServerEmitDestroyed(Vector3Int[] cells)
        {
            if (cells == null || cells.Length == 0)
                return;
            RpcClearDestructibles(cells);
        }

        [ClientRpc]
        void RpcClearDestructibles(Vector3Int[] cells)
        {
            if (isServer || BombermanNetworkManager.IsCoopStage)
                return;

            var gm = GameManager.Instance;
            if (gm == null || gm.destructibleTilemap == null)
                return;

            Tilemap tm = gm.destructibleTilemap;
            for (int i = 0; i < cells.Length; i++)
            {
                if (bomb != null)
                    bomb.PlayNetworkDestructibleBreak(cells[i]);  // animação de quebra
                tm.SetTile(cells[i], null);
                gm.OnDestructibleDestroyed(cells[i]);
            }
        }
    }
}

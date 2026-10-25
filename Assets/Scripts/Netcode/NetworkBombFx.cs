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
    public class NetworkBombFx : NetworkBehaviour
    {
        BombController bomb;

        void Awake() => bomb = GetComponent<BombController>();

        public struct KickMotion
        {
            public uint bombId, inputTick;
            public Vector2 start, end;
            public float duration;
            public double serverTime;
            public bool moving, first;
        }

        sealed class KickPlayback
        {
            public KickMotion motion;
            public float elapsed, staleSeconds;
            public double endpointWaitAt = -1;
            public Vector2 presented, correction;
            public bool predictedContinuation;
        }

        readonly Dictionary<Bomb, KickPlayback> kickPlayback = new();
        readonly List<Bomb> expiredKicks = new();

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
            bool continueLocally = false;
            if (motion.moving && playback.elapsed > motion.duration && motion.duration > 0)
            {
                var step = motion.end - motion.start;
                continueLocally = movingBomb.CanPredictKickContinuation(motion.end + step, step.magnitude);
                if (continueLocally && !playback.predictedContinuation && GameplayLatencyDiagnostics.Enabled)
                    GameplayLatencyDiagnostics.Write(GameplayLatencyDiagnostics.For("kick_continuation_prediction",
                        GetComponent<MovementController>(), movingBomb));
                playback.predictedContinuation |= continueLocally;
            }
            return SampleKickContinuation(motion.start, motion.end, motion.duration, playback.elapsed, continueLocally);
        }

        [Server]
        public void ServerEmitKickMotion(Bomb movingBomb, Vector2 start, Vector2 end, float duration, bool moving, bool first)
        {
            if (connectionToClient == null || connectionToClient == NetworkServer.localConnection || movingBomb == null ||
                !movingBomb.TryGetComponent<NetworkIdentity>(out var identity) || identity.netId == 0) return;
            var motion = new KickMotion { bombId = identity.netId, start = start, end = end,
                duration = duration, moving = moving, first = first, serverTime = NetworkTime.localTime,
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
        void TargetKickMotion(NetworkConnectionToClient target, KickMotion motion)
        {
            if (!NetSync.IsOnline || isServer || !isLocalPlayer || !NetworkClient.active) return;
            if (!NetworkClient.spawned.TryGetValue(motion.bombId, out var identity) ||
                !identity.TryGetComponent<Bomb>(out var movingBomb)) return;
            if (kickPlayback.TryGetValue(movingBomb, out var previous) && previous.motion.serverTime > motion.serverTime) return;
            if (previous != null && previous.endpointWaitAt >= 0 && GameplayLatencyDiagnostics.Enabled)
            {
                var wait = GameplayLatencyDiagnostics.For("kick_endpoint_wait", GetComponent<MovementController>(), movingBomb,
                    previous.predictedContinuation ? "confirmation_gap_with_local_continuation" : "confirmation_gap_blocked_exit");
                wait.durationMs = (previous.staleSeconds - previous.endpointWaitAt) * 1000;
                wait.authoritativeTick = motion.inputTick;
                GameplayLatencyDiagnostics.Write(wait);
            }
            float age = GamePauseController.IsPaused ? 0 : Mathf.Max(0, (float)(KickServerNow - motion.serverTime));
            var playback = new KickPlayback { motion = motion, elapsed = age };
            var targetPosition = SamplePlayback(movingBomb, playback);
            // The NetworkTransform may have written its older timeline this frame.
            // Reconcile against our last displayed position, not that stale transform.
            if (previous != null && !motion.first && motion.moving && previous.motion.moving &&
                Vector2.Dot(motion.end - motion.start, previous.motion.end - previous.motion.start) > 0 &&
                Vector2.Distance(previous.presented, targetPosition) < 0.75f * (motion.end - motion.start).magnitude)
                playback.correction = previous.presented - targetPosition;
            playback.presented = targetPosition + playback.correction;
            kickPlayback[movingBomb] = playback;
            if (motion.first || !motion.moving)
                MotionDiagnosticProbe.KickFeedback(gameObject, movingBomb, motion.moving, motion.inputTick, age * 1000);
            if (GameplayLatencyDiagnostics.Enabled)
            {
                var segment = GameplayLatencyDiagnostics.For("kick_segment_apply", GetComponent<MovementController>(), movingBomb,
                    motion.first ? "first_segment" : motion.moving ? "continuation" : "stop");
                segment.authoritativeTick = motion.inputTick;
                segment.messageAgeMs = age * 1000;
                segment.durationMs = motion.duration * 1000;
                segment.delta = SampleKickSegment(motion.start, motion.end, motion.duration, age) - (Vector2)movingBomb.transform.position;
                GameplayLatencyDiagnostics.Write(segment);
            }
            // No global physics replay, no StartKick, no audio or gameplay effects.
            // The normal NetworkTransform remains enabled to receive/decode deltas.
            ApplyKickPosition(movingBomb, playback.presented);
        }

        void LateUpdate()
        {
            if (!NetSync.IsOnline || isServer || !isLocalPlayer || !NetworkClient.active)
            {
                kickPlayback.Clear();
                return;
            }
            expiredKicks.Clear();
            foreach (var pair in kickPlayback)
            {
                var movingBomb = pair.Key;
                var playback = pair.Value;
                if (movingBomb == null || !movingBomb.isActiveAndEnabled || movingBomb.HasExploded)
                { expiredKicks.Add(movingBomb); continue; }
                if (!GamePauseController.IsPaused)
                {
                    playback.elapsed += Time.deltaTime;
                    playback.staleSeconds += Time.unscaledDeltaTime;
                }
                var motion = playback.motion;
                if (motion.moving && playback.elapsed >= motion.duration && playback.endpointWaitAt < 0)
                    playback.endpointWaitAt = playback.staleSeconds;
                // Decay small corrections over a few frames rather than jumping at
                // every segment boundary. Stops/bounces still apply immediately.
                if (!GamePauseController.IsPaused)
                    playback.correction = Vector2.MoveTowards(playback.correction, Vector2.zero,
                        Time.deltaTime * 0.5f * (motion.end - motion.start).magnitude / Mathf.Max(0.001f, motion.duration));
                playback.presented = SamplePlayback(movingBomb, playback) + playback.correction;
                ApplyKickPosition(movingBomb, playback.presented);
                double transformOffset = 0;
                if (movingBomb.TryGetComponent<NetworkTransformReliable>(out var sync))
                    transformOffset = NetworkClient.sendInterval * (sync.sendIntervalMultiplier - 1 +
                        (sync.timelineOffset ? sync.sendIntervalMultiplier : 0));
                bool caughtUp = !motion.moving && NetworkTime.time >= motion.serverTime + transformOffset;
                if (caughtUp || playback.staleSeconds > 1f)
                {
                    expiredKicks.Add(movingBomb);
                    if (GameplayLatencyDiagnostics.Enabled)
                        GameplayLatencyDiagnostics.Write(GameplayLatencyDiagnostics.For("kick_transform_handoff",
                            GetComponent<MovementController>(), movingBomb, caughtUp ? "timeline_caught_up" : "segment_timeout"));
                }
            }
            foreach (var movingBomb in expiredKicks) kickPlayback.Remove(movingBomb);
        }

        // Called before local movement/replay queries. Normal NetworkTransform may
        // have restored the bomb's older buffered position earlier in the frame.
        public void RestoreClientKickPositions()
        {
            if (!NetSync.IsOnline || isServer || !isLocalPlayer || !NetworkClient.active) return;
            foreach (var pair in kickPlayback)
                if (pair.Key != null && pair.Key.isActiveAndEnabled && !pair.Key.HasExploded)
                    ApplyKickPosition(pair.Key, pair.Value.presented);
        }

        static void ApplyKickPosition(Bomb movingBomb, Vector2 position)
        {
            if (movingBomb.TryGetComponent<Rigidbody2D>(out var body)) body.position = position;
            movingBomb.transform.position = new Vector3(position.x, position.y, movingBomb.transform.position.z);
        }

        void OnDisable() => kickPlayback.Clear();

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

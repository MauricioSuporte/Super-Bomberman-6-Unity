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
        }

        readonly Dictionary<Bomb, KickPlayback> kickPlayback = new();
        readonly List<Bomb> expiredKicks = new();

        // predictedTime is ahead by one-way latency. Remove that lead, rather than
        // adding the snapshot interpolation buffer to an already confirmed kick.
        public static double KickServerNow => NetworkTime.predictedTime - NetworkTime.rtt * 0.5;

        public static Vector2 SampleKickSegment(Vector2 start, Vector2 end, float duration, float elapsed) =>
            duration <= 0 ? end : Vector2.Lerp(start, end, Mathf.Clamp01(elapsed / duration));

        [Server]
        public void ServerEmitKickMotion(Bomb movingBomb, Vector2 start, Vector2 end, float duration, bool moving, bool first)
        {
            if (connectionToClient == null || connectionToClient == NetworkServer.localConnection || movingBomb == null ||
                !movingBomb.TryGetComponent<NetworkIdentity>(out var identity) || identity.netId == 0) return;
            var motion = new KickMotion { bombId = identity.netId, start = start, end = end,
                duration = duration, moving = moving, first = first, serverTime = NetworkTime.localTime,
                inputTick = TryGetComponent<NetworkPlayerInput>(out var input) ? input.CurrentInputTick : 0 };
            if (first || !moving)
            {
                var entry = GameplayLatencyDiagnostics.For(moving ? "kick_segment_send" : "kick_stop_send",
                    GetComponent<MovementController>(), movingBomb);
                if (GameplayLatencyDiagnostics.Enabled) GameplayLatencyDiagnostics.Write(entry);
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
            float age = GamePauseController.IsPaused ? 0 : Mathf.Max(0, (float)(KickServerNow - motion.serverTime));
            kickPlayback[movingBomb] = new KickPlayback { motion = motion, elapsed = Mathf.Min(age, motion.duration) };
            if (motion.first || !motion.moving)
                MotionDiagnosticProbe.KickFeedback(gameObject, movingBomb, motion.moving, motion.inputTick, age * 1000);
            // No global physics replay, no StartKick, no audio or gameplay effects.
            // The normal NetworkTransform remains enabled to receive/decode deltas.
            ApplyKickPosition(movingBomb, SampleKickSegment(motion.start, motion.end, motion.duration, age));
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
                // The sender already checked the next tile. Never extrapolate beyond it.
                ApplyKickPosition(movingBomb, SampleKickSegment(motion.start, motion.end, motion.duration, playback.elapsed));
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

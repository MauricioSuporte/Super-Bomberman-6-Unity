using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace Assets.Scripts.Netcode
{
    public partial class NetworkBombFx
    {
        public struct PlayerBounceMotion
        {
            public uint revision;
            public Vector2 start, end;
            public float duration, curveDuration, height;
            public bool moving, first;
        }

        readonly Queue<PlayerBounceMotion> playerBounceQueue = new();
        PlayerBounceMotion playerBounce;
        uint serverBounceRevision, receivedBounceRevision;
        bool serverBounceActive, playerBounceActive, bounceColliderCaptured, bounceColliderEnabled;
        float bounceElapsed, bounceSilence;
        Vector2 bouncePosition, bounceDirection;
        public bool PlayerBounceActive => playerBounceActive;

        public static Vector2 SamplePlayerBounce(Vector2 start, Vector2 end, float duration, float elapsed, float height)
        {
            float t = duration <= 0 ? 1 : Mathf.Clamp01(elapsed / duration);
            return Vector2.Lerp(start, end, t) + Vector2.up * (4 * Mathf.Max(0, height) * t * (1 - t));
        }

        [Server]
        public void ServerPlayerBounce(Vector2 start, Vector2 end, float duration, float height, bool moving)
        {
            if (netId == 0 || (!moving && !serverBounceActive)) return;
            var motion = new PlayerBounceMotion { revision = ++serverBounceRevision, start = start, end = end,
                duration = duration <= 0 ? 0 : Mathf.Ceil(duration / Time.fixedDeltaTime) * Time.fixedDeltaTime,
                curveDuration = duration, height = height, moving = moving, first = moving && !serverBounceActive };
            serverBounceActive = moving;
            RpcPlayerBounce(motion);
            if (GameplayLatencyDiagnostics.Enabled)
                GameplayLatencyDiagnostics.Write(GameplayLatencyDiagnostics.For("player_bounce_send", GetComponent<MovementController>(),
                    detail: "revision=" + motion.revision + ";moving=" + moving + ";duration=" + duration));
        }

        [ClientRpc]
        void RpcPlayerBounce(PlayerBounceMotion motion) => ReceivePlayerBounce(motion);

        public void ReceivePlayerBounce(PlayerBounceMotion motion)
        {
            if (NetSync.Mode != NetSync.NetMode.Client || isServer || !NetworkClient.active) return;
            if (!NetworkInputHistory.IsNewer(motion.revision, receivedBounceRevision)) return;
            receivedBounceRevision = motion.revision; bounceSilence = 0;
            if (!motion.first && playerBounceActive && (bounceElapsed < playerBounce.duration || playerBounceQueue.Count > 0))
            {
                if (playerBounceQueue.Count < 32) { playerBounceQueue.Enqueue(motion); return; }
            }
            if (motion.first) playerBounceQueue.Clear();
            ApplyPlayerBounce(motion, 0);
        }

        void ApplyPlayerBounce(PlayerBounceMotion motion, float remainder)
        {
            if (GameplayLatencyDiagnostics.Enabled)
            {
                var entry = GameplayLatencyDiagnostics.For("player_bounce_apply", GetComponent<MovementController>(),
                    detail: "revision=" + motion.revision + ";moving=" + motion.moving + ";queued=" + playerBounceQueue.Count);
                entry.durationMs = motion.duration * 1000; entry.delta = motion.end - motion.start;
                GameplayLatencyDiagnostics.Write(entry);
            }
            if (motion.moving) { ClearGreenDash("external_bounce"); ClearPinkJump("external_bounce"); ClearBlackDash("external_bounce"); }
            playerBounce = motion; bounceElapsed = remainder;
            bouncePosition = motion.start;
            if (!motion.moving)
            {
                bouncePosition = motion.end; RestorePlayerBouncePosition(); ClearPlayerBouncePresentation(); return;
            }
            playerBounceActive = true;
            if ((motion.end - motion.start).sqrMagnitude > 0.0001f) bounceDirection = (motion.end - motion.start).normalized;
            if (TryGetComponent<Collider2D>(out var collider))
            {
                if (!bounceColliderCaptured) { bounceColliderEnabled = collider.enabled; bounceColliderCaptured = true; }
                collider.enabled = false;
            }
            if (TryGetComponent<MovementController>(out var movement)) movement.SetPredictLocally(false);
            bouncePosition = SamplePlayerBounce(motion.start, motion.end, motion.curveDuration > 0 ? motion.curveDuration : motion.duration, remainder, motion.height);
            RestorePlayerBouncePosition();
            GetComponent<PlayerPushedOutOfInvalidTile>()?.PresentNetworkBounce(bounceDirection, true);
        }

        public void RestorePlayerBouncePosition()
        {
            if (!playerBounceActive) return;
            if (TryGetComponent<Rigidbody2D>(out var body)) body.position = bouncePosition;
            transform.position = new Vector3(bouncePosition.x, bouncePosition.y, transform.position.z);
        }

        void UpdatePlayerBouncePresentation(float deltaTime, float unscaledDeltaTime)
        {
            if (!playerBounceActive) return;
            var movement = GetComponent<MovementController>();
            if (movement == null || movement.isDead || movement.IsEndingStage) { ClearPlayerBouncePresentation(); return; }
            if (!GamePauseController.IsPaused) { bounceElapsed += deltaTime; bounceSilence += unscaledDeltaTime; }
            if (bounceElapsed >= playerBounce.duration && playerBounceQueue.Count > 0)
                ApplyPlayerBounce(playerBounceQueue.Dequeue(), ArcRemainder(bounceElapsed, playerBounce.duration));
            if (!playerBounceActive) return;
            if (playerBounceQueue.Count == 0 && bounceSilence > Mathf.Max(1, playerBounce.duration + 0.5f))
            {
                ClearPlayerBouncePresentation(); return;
            }
            bouncePosition = SamplePlayerBounce(playerBounce.start, playerBounce.end, playerBounce.curveDuration > 0 ? playerBounce.curveDuration : playerBounce.duration, bounceElapsed, playerBounce.height);
            RestorePlayerBouncePosition();
            GetComponent<PlayerPushedOutOfInvalidTile>()?.PresentNetworkBounce(bounceDirection, true);
        }

        void ClearPlayerBouncePresentation()
        {
            if (bounceColliderCaptured && TryGetComponent<Collider2D>(out var collider)) collider.enabled = bounceColliderEnabled;
            bounceColliderCaptured = false; playerBounceActive = false; playerBounceQueue.Clear();
            GetComponent<PlayerPushedOutOfInvalidTile>()?.PresentNetworkBounce(bounceDirection, false);
        }
    }
}

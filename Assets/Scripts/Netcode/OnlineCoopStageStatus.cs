using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace Assets.Scripts.Netcode
{
    public struct CoopHealthFrame { public string path; public int life; public Vector3 position; }
    public struct CoopStageStatus : NetworkMessage
    {
        public string scene;
        public int enemies;
        public bool ending, fadeVisible;
        public Color fade;
        public CoopHealthFrame[] health;
        public CoopCinematicFrame[] cinematics;
    }

    public partial class BombermanNetworkManager
    {
        readonly Dictionary<string, CharacterHealth> coopSceneHealth = new();
        CharacterHealth[] coopServerHealth = System.Array.Empty<CharacterHealth>();
        float coopNextStatus;

        void CaptureCoopStageStatus()
        {
            if (Time.unscaledTime < coopNextStatus) return;
            coopNextStatus = Time.unscaledTime + 0.1f;
            var health = new List<CoopHealthFrame>();
            foreach (var actor in coopServerHealth)
                if (actor != null && !HasNetworkParent(actor.transform))
                    health.Add(new CoopHealthFrame { path = CoopPath(actor.transform), life = actor.life, position = actor.transform.position });
            var fade = StageIntroTransition.Instance != null ? StageIntroTransition.Instance.fadeImage : null;
            var game = GameManager.Instance;
            NetworkServer.SendToReady(new CoopStageStatus { scene = coopScene,
                enemies = game != null ? game.EnemiesAlive : 0,
                ending = game != null && game.OnlineRoundPresentationActive,
                fadeVisible = fade != null && fade.gameObject.activeInHierarchy && fade.color.a > 0.001f,
                fade = fade != null ? fade.color : Color.clear, health = health.ToArray(), cinematics = CaptureCoopCinematics() });
        }

        void ApplyCoopStageStatus(CoopStageStatus message)
        {
            if (!CanApplyCoop(message.scene)) return;
            GameManager.Instance?.ApplyOnlineCoopStage(message.enemies, message.ending);
            ApplyCoopCinematics(message.cinematics);
            var fade = StageIntroTransition.Instance != null ? StageIntroTransition.Instance.fadeImage : null;
            if (fade != null)
            {
                fade.gameObject.SetActive(message.fadeVisible);
                fade.color = message.fade;
            }
            foreach (var state in message.health ?? System.Array.Empty<CoopHealthFrame>())
                if (coopSceneHealth.TryGetValue(state.path, out var actor) && actor != null)
                {
                    actor.life = state.life;
                    actor.transform.position = state.position;
                }
        }
    }
}

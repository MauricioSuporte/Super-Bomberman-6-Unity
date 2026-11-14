using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace Assets.Scripts.Netcode
{
    public struct CoopHealthFrame { public string path; public int life; public Vector3 position; }
    public struct CoopStageStatus : NetworkMessage
    {
        public string scene, camera;
        public Vector3 cameraPosition;
        public float cameraSize;
        public bool cameraScripted, cameraFollows;
        public int enemies;
        public bool ending, fadeVisible, introLabel, credits, creditsSkipped, creditsFinal;
        public int world, stage;
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
            string cameraPath = CaptureCoopCameraSelection();
            coopClientCameras.TryGetValue(cameraPath ?? "", out var camera);
            var follow = camera != null ? camera.GetComponent<CameraFollowClamp2D>() : null;
            bool scripted = (follow == null || !follow.isActiveAndEnabled) ||
                StageIntroTransition.Instance != null && StageIntroTransition.Instance.IntroRunning ||
                StageMechaIntroController.Instance != null && StageMechaIntroController.Instance.IntroRunning;
            SendPresentationToRemoteReady(new CoopStageStatus { scene = coopScene, camera = cameraPath,
                cameraPosition = camera != null ? camera.transform.position : Vector3.zero,
                cameraSize = camera != null ? camera.orthographicSize : 7f,
                cameraScripted = scripted, cameraFollows = follow != null && follow.enabled,
                introLabel = StageIntroTransition.Instance != null && StageIntroTransition.Instance.IntroRunning && StageIntroTransition.Instance.stageLabel != null &&
                    StageIntroTransition.Instance.stageLabel.gameObject.activeInHierarchy,
                world = StageIntroTransition.Instance != null ? StageIntroTransition.Instance.world : 1,
                stage = StageIntroTransition.Instance != null ? StageIntroTransition.Instance.stageNumber : 1,
                enemies = game != null ? game.EnemiesAlive : 0,
                ending = game != null && game.OnlineRoundPresentationActive,
                credits = StageIntroTransition.Instance != null && StageIntroTransition.Instance.EndingRunning,
                creditsSkipped = EndingScreenController.Instance != null && EndingScreenController.Instance.CreditsSkipped,
                creditsFinal = EndingScreenController.Instance != null && EndingScreenController.Instance.ShowingFinalMessage,
                fadeVisible = fade != null && fade.gameObject.activeInHierarchy && fade.color.a > 0.001f,
                fade = fade != null ? fade.color : Color.clear, health = health.ToArray(), cinematics = CaptureCoopCinematics() });
        }

        void ApplyCoopStageStatus(CoopStageStatus message)
        {
            if (!CanApplyCoop(message.scene)) return;
            if (message.credits && StageIntroTransition.Instance != null && !StageIntroTransition.Instance.EndingRunning)
                StageIntroTransition.Instance.StartEndingScreenSequence();
            if (message.credits) EndingScreenController.Instance?.ApplyOnlineCreditsPhase(message.creditsSkipped, message.creditsFinal);
            GameManager.Instance?.ApplyOnlineCoopStage(message.enemies, message.ending);
            ApplyCoopCinematics(message.cinematics);
            ApplyCoopCameraSelection(message.camera);
            ApplyCoopCameraPose(message.camera, message.cameraPosition, message.cameraSize, message.cameraScripted, message.cameraFollows);
            if (!GamePauseController.IsPaused && StageIntroTransition.Instance != null && StageIntroTransition.Instance.stageLabel != null)
            {
                var label = StageIntroTransition.Instance.stageLabel;
                label.gameObject.SetActive(message.introLabel);
                if (message.introLabel) label.SetStage(message.world, message.stage);
            }
            var fade = StageIntroTransition.Instance != null ? StageIntroTransition.Instance.fadeImage : null;
            if (fade != null)
            {
                // Cinematics owns this image's continuous presentation.
                if (message.cinematics == null || !System.Array.Exists(message.cinematics, frame => frame.path == CoopPath(fade.transform)))
                    ApplyCoopFade(fade, message.fade, message.fadeVisible);
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

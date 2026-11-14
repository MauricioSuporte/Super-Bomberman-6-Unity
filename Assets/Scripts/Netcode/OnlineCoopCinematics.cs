using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.Netcode
{
    public struct CoopCinematicFrame
    {
        public string path;
        public bool visible, spotlight;
        public Color color, materialColor;
        public Vector4 center;
        public float radius, softness, ellipseX, ellipseY;
    }

    public partial class BombermanNetworkManager
    {
        readonly List<Image> coopCinematicImages = new();
        readonly Dictionary<string, Image> coopClientImages = new();
        sealed class CoopFadeSample
        {
            public Color from, to;
            public float started;
            public bool visible;
        }
        readonly Dictionary<Image, CoopFadeSample> coopFadeSamples = new();

        void ApplyCoopFade(Image image, Color color, bool visible)
        {
            if (!coopFadeSamples.TryGetValue(image, out var sample))
            {
                sample = new CoopFadeSample();
                coopFadeSamples.Add(image, sample);
            }
            sample.from = image.color;
            sample.to = color;
            if (!visible) sample.to.a = 0f;
            sample.started = Time.unscaledTime;
            sample.visible = visible;
            if (visible) image.gameObject.SetActive(true);
        }

        void PresentCoopFades()
        {
            PresentCoopCamera();
            foreach (var pair in coopFadeSamples)
            {
                var image = pair.Key;
                if (image == null) continue;
                var sample = pair.Value;
                float progress = Mathf.Clamp01((Time.unscaledTime - sample.started) / .1f);
                image.color = Color.Lerp(sample.from, sample.to, progress);
                if (progress >= 1f && !sample.visible) image.gameObject.SetActive(false);
            }
        }

        Image coopSpotlightImage;
        readonly Dictionary<string, Camera> coopClientCameras = new();
        string coopApprovedCamera;
        StagePreIntroPlayersWalk coopPreIntro;
        Camera coopPoseCamera;
        Vector3 coopCameraFrom, coopCameraTo;
        float coopCameraStarted;
        bool coopCameraScripted;

        void InitializeCoopCinematics()
        {
            coopPreIntro = FindAnyObjectByType<StagePreIntroPlayersWalk>(FindObjectsInactive.Include);
            foreach (var camera in FindObjectsByType<Camera>(FindObjectsInactive.Include))
                if (camera.gameObject.scene.name == coopScene && (camera.CompareTag("MainCamera") ||
                    (coopPreIntro != null && camera == coopPreIntro.EntrancePresentationCamera)))
                    coopClientCameras[CoopPath(camera.transform)] = camera;
            foreach (var image in FindObjectsByType<Image>(FindObjectsInactive.Include))
                if (image.gameObject.scene.name == coopScene) coopClientImages[CoopPath(image.transform)] = image;
            AddCoopCinematic(StageIntroTransition.Instance != null ? StageIntroTransition.Instance.fadeImage : null);
            AddCoopCinematic(StageMechaIntroController.Instance != null ? StageMechaIntroController.Instance.fadeImage : null);
            var clown = FindAnyObjectByType<StageIntroClownMaskBoss>(FindObjectsInactive.Include);
            coopSpotlightImage = clown != null ? clown.spotlightImage : null;
            AddCoopCinematic(coopSpotlightImage);
            foreach (var boiler in FindObjectsByType<Assets.Scripts.Explosions.BoilerPowderTrailIgniterPrefab>(FindObjectsInactive.Include))
                if (boiler.gameObject.scene.name == coopScene) AddCoopCinematic(boiler.ScreenRedOverlay);
        }

        string CaptureCoopCameraSelection()
        {
            var camera = coopPreIntro != null ? coopPreIntro.ActivePresentationCamera : null;
            if (camera == null) camera = Camera.main;
            return camera!=null&&camera.gameObject.scene.name==coopScene?CoopPath(camera.transform):null;
        }

        void ApplyCoopCameraSelection(string path)
        {
            if(string.IsNullOrEmpty(path)||path==coopApprovedCamera||!coopClientCameras.TryGetValue(path,out var selected)||selected==null)return;
            foreach(var camera in coopClientCameras.Values)
                if(camera!=null)
                {
                    camera.enabled=camera==selected;
                    if(camera.TryGetComponent<AudioListener>(out var listener))listener.enabled=camera==selected;
                }
            coopApprovedCamera=path;
            if(selected.TryGetComponent<CameraFollowClamp2D>(out var follow))follow.ForceSnapNow(refreshPlayersNow:true);
        }

        void ApplyCoopCameraPose(string path, Vector3 position, float size, bool scripted, bool follows)
        {
            if (!float.IsFinite(position.x) || !float.IsFinite(position.y) || !float.IsFinite(position.z) ||
                !float.IsFinite(size) || size <= 0 || !coopClientCameras.TryGetValue(path ?? "", out var camera) || camera == null) return;
            bool changed = coopPoseCamera != camera;
            bool wasScripted = coopCameraScripted;
            coopPoseCamera = camera; coopCameraScripted = scripted;
            if (camera.TryGetComponent<CameraFollowClamp2D>(out var follow)) follow.enabled = follows && !scripted;
            camera.orthographicSize = size;
            if (changed || wasScripted && !scripted) camera.transform.position = position;
            coopCameraFrom = camera.transform.position; coopCameraTo = position; coopCameraStarted = Time.unscaledTime;
        }

        void PresentCoopCamera()
        {
            if (!coopCameraScripted || coopPoseCamera == null || GamePauseController.IsPaused) return;
            coopPoseCamera.transform.position = Vector3.Lerp(coopCameraFrom, coopCameraTo,
                Mathf.Clamp01((Time.unscaledTime - coopCameraStarted) / .1f));
        }

        void AddCoopCinematic(Image image)
        {
            if (image != null && !coopCinematicImages.Contains(image)) coopCinematicImages.Add(image);
        }

        CoopCinematicFrame[] CaptureCoopCinematics()
        {
            var frames = new List<CoopCinematicFrame>();
            foreach (var image in coopCinematicImages)
            {
                if (image == null) continue;
                var frame = new CoopCinematicFrame { path = CoopPath(image.transform), color = image.color,
                    visible = image.enabled && image.gameObject.activeInHierarchy };
                // Plain fades only need color/visibility. Accessing their default
                // UI material requests a shader stripped from headless builds.
                var material = image == coopSpotlightImage ? image.material : null;
                if (material != null && material.HasProperty("_EllipseX"))
                {
                    frame.spotlight = true;
                    frame.materialColor = material.GetColor("_Color"); frame.center = material.GetVector("_Center");
                    frame.radius = material.GetFloat("_Radius"); frame.softness = material.GetFloat("_Softness");
                    frame.ellipseX = material.GetFloat("_EllipseX"); frame.ellipseY = material.GetFloat("_EllipseY");
                }
                frames.Add(frame);
            }
            return frames.ToArray();
        }

        void ApplyCoopCinematics(CoopCinematicFrame[] frames)
        {
            foreach (var frame in frames ?? System.Array.Empty<CoopCinematicFrame>())
            {
                if (!coopClientImages.TryGetValue(frame.path, out var image) || image == null) continue;
                if (!frame.spotlight)
                {
                    ApplyCoopFade(image, frame.color, frame.visible);
                    continue;
                }
                // Before the native intro material is initialized this image can
                // arrive as a plain hidden fade. Its old fade must not keep hiding
                // the subsequently approved spotlight every presentation frame.
                coopFadeSamples.Remove(image);
                image.enabled = true;
                image.gameObject.SetActive(frame.visible); image.color = frame.color;
                if (!frame.spotlight || image.material == null || !image.material.HasProperty("_EllipseX")) continue;
                var material = image.material;
                material.SetColor("_Color", frame.materialColor); material.SetVector("_Center", frame.center);
                material.SetFloat("_Radius", frame.radius); material.SetFloat("_Softness", frame.softness);
                material.SetFloat("_EllipseX", frame.ellipseX); material.SetFloat("_EllipseY", frame.ellipseY);
                if (frame.visible) image.transform.SetAsLastSibling();
            }
        }
    }
}

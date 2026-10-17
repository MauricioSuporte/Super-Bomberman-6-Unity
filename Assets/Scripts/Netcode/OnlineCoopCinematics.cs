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

        void InitializeCoopCinematics()
        {
            foreach (var image in FindObjectsByType<Image>(FindObjectsInactive.Include))
                if (image.gameObject.scene.name == coopScene) coopClientImages[CoopPath(image.transform)] = image;
            AddCoopCinematic(StageIntroTransition.Instance != null ? StageIntroTransition.Instance.fadeImage : null);
            AddCoopCinematic(StageMechaIntroController.Instance != null ? StageMechaIntroController.Instance.fadeImage : null);
            var clown = FindAnyObjectByType<StageIntroClownMaskBoss>(FindObjectsInactive.Include);
            if (clown != null) AddCoopCinematic(clown.spotlightImage);
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
                var material = image.material;
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

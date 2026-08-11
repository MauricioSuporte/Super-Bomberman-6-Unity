using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using LegacyPixelPerfectCamera = UnityEngine.U2D.PixelPerfectCamera;

/// <summary>
/// Applies the Stage 3-2 heat-refraction pass, with per-scene material settings,
/// to the World 3 gameplay camera.
/// </summary>
public sealed class StageHeatDistortionRendererFeature : ScriptableRendererFeature
{
    private const string Stage33SceneName = "Stage_3-3";
    private static readonly int BandHeightPixelsId = Shader.PropertyToID("_BandHeightPixels");
    private static readonly int BandScrollSpeedId = Shader.PropertyToID("_BandScrollSpeed");
    private static readonly int BandsPerDirectionId = Shader.PropertyToID("_BandsPerDirection");
    private static readonly int LogicalPixelScaleId = Shader.PropertyToID("_LogicalPixelScale");

    [SerializeField] private Material heatDistortionMaterial;
    [SerializeField] private Material stage33DistortionMaterial;
    [SerializeField] private bool logStage33BandProgress = true;
    [SerializeField] private RenderPassEvent injectionPoint = RenderPassEvent.AfterRenderingTransparents;

    private StageHeatDistortionPass pass;
    private string lastLoggedSceneName;
    private int lastLoggedScanLineIndex = int.MinValue;

    public override void Create()
    {
        pass = new StageHeatDistortionPass();
        pass.renderPassEvent = injectionPoint;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (pass == null)
            return;

        string sceneName = SceneManager.GetActiveScene().name;
        if (sceneName != Stage33SceneName)
        {
            lastLoggedSceneName = null;
            lastLoggedScanLineIndex = int.MinValue;
        }

        Material distortionMaterial = GetSceneDistortionMaterial(sceneName);
        if (distortionMaterial == null)
            return;

        LogStage33BandRippleOnce(sceneName, distortionMaterial);
        LogStage33BandProgress(sceneName, distortionMaterial);
        pass.Setup(distortionMaterial);
        renderer.EnqueuePass(pass);
    }

    private Material GetSceneDistortionMaterial(string sceneName)
    {
        return sceneName switch
        {
            "Stage_3-2" => heatDistortionMaterial,
            Stage33SceneName => stage33DistortionMaterial,
            _ => null
        };
    }

    private void LogStage33BandRippleOnce(string sceneName, Material material)
    {
        if (lastLoggedSceneName == sceneName)
            return;

        lastLoggedSceneName = sceneName;
        if (sceneName != Stage33SceneName)
            return;

        Debug.Log(
            $"[StageWaterBandRipple] Active: {material.name}; " +
            $"line spacing {material.GetFloat(BandHeightPixelsId):0}px, " +
            $"speed {material.GetFloat(BandScrollSpeedId):0} px/s, " +
            $"{material.GetFloat(BandsPerDirectionId):0} bands up then down.");
    }

    private void LogStage33BandProgress(string sceneName, Material material)
    {
        if (!logStage33BandProgress || sceneName != Stage33SceneName)
            return;

        int lineSpacing = Mathf.Max(1, Mathf.RoundToInt(material.GetFloat(BandHeightPixelsId)));
        int travelledPixels = Mathf.FloorToInt(Time.unscaledTime * material.GetFloat(BandScrollSpeedId));
        int lineIndex = Mathf.FloorToInt(-(float)travelledPixels / lineSpacing);
        if (lineIndex == lastLoggedScanLineIndex)
            return;

        lastLoggedScanLineIndex = lineIndex;
        int bandsPerDirection = Mathf.Max(1, Mathf.RoundToInt(material.GetFloat(BandsPerDirectionId)));
        int directionCycle = bandsPerDirection * 2;
        int cyclePosition = lineIndex % directionCycle;
        if (cyclePosition < 0)
            cyclePosition += directionCycle;

        string direction = cyclePosition < bandsPerDirection ? "up" : "down";
        Debug.Log($"[StageWaterBandRipple] Scan line {lineIndex} reached the screen bottom: " +
                  $"its full horizontal row moves 1px {direction}.");
    }

    private sealed class StageHeatDistortionPass : ScriptableRenderPass
    {
        private const string PassName = "Stage Scene Distortion";
        private static readonly int UnscaledTimeId = Shader.PropertyToID("_UnscaledTime");

        private Material material;

        public void Setup(Material heatMaterial)
        {
            material = heatMaterial;
            requiresIntermediateTexture = true;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            if (!ShouldRunFor(cameraData.camera) || material == null)
                return;

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            if (resourceData.isActiveTargetBackBuffer)
                return;

            TextureHandle source = resourceData.activeColorTexture;
            TextureDesc destinationDescriptor = renderGraph.GetTextureDesc(source);
            destinationDescriptor.name = "_StageSceneDistortion";
            destinationDescriptor.clearBuffer = false;
            TextureHandle destination = renderGraph.CreateTexture(destinationDescriptor);

            material.SetFloat(UnscaledTimeId, Time.unscaledTime);
            material.SetFloat(LogicalPixelScaleId, ResolveLogicalPixelScale(cameraData.camera));
            RenderGraphUtils.BlitMaterialParameters parameters = new(source, destination, material, 0);
            renderGraph.AddBlitPass(parameters, PassName);

            // Subsequent renderer passes use the refracted image without an extra copy.
            resourceData.cameraColor = destination;
        }

        private static bool ShouldRunFor(Camera camera)
        {
            return camera != null && camera.CompareTag("MainCamera");
        }

        private static int ResolveLogicalPixelScale(Camera camera)
        {
            if (camera == null || !camera.TryGetComponent(out LegacyPixelPerfectCamera pixelPerfectCamera))
                return 1;

            int referenceHeight = Mathf.Max(1, pixelPerfectCamera.refResolutionY);
            return Mathf.Max(1, Mathf.RoundToInt(camera.pixelHeight / (float)referenceHeight));
        }
    }
}

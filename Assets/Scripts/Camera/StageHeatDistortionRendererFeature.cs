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
    private static readonly int BandsPerDirectionId = Shader.PropertyToID("_BandsPerDirection");
    private static readonly int LogicalPixelScaleId = Shader.PropertyToID("_LogicalPixelScale");
    private static readonly int LogicalPixelOriginYId = Shader.PropertyToID("_LogicalPixelOriginY");

    [SerializeField] private Material heatDistortionMaterial;
    [SerializeField] private Material stage33DistortionMaterial;
    [SerializeField] private RenderPassEvent injectionPoint = RenderPassEvent.AfterRenderingTransparents;

    private StageHeatDistortionPass pass;

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
        Material distortionMaterial = GetSceneDistortionMaterial(sceneName);
        if (distortionMaterial == null)
            return;

        pass.Setup(distortionMaterial, sceneName == Stage33SceneName);
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

    private sealed class StageHeatDistortionPass : ScriptableRenderPass
    {
        private const string PassName = "Stage Scene Distortion";
        private static readonly int UnscaledTimeId = Shader.PropertyToID("_UnscaledTime");

        private Material material;
        private bool isStage33Ripple;
        private int lastLoggedCameraId;
        private int lastLoggedMaterialId;

        public void Setup(Material distortionMaterial, bool stage33Ripple)
        {
            material = distortionMaterial;
            isStage33Ripple = stage33Ripple;
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

            LogStage33LeftEdgeCorrection(cameraData.camera, destinationDescriptor);

            // Time.time is scaled by Time.timeScale, so the water freezes together
            // with gameplay while GamePauseController pauses the game.
            material.SetFloat(UnscaledTimeId, Time.time);
            material.SetFloat(LogicalPixelScaleId, StageHeatDistortionRendererFeature.ResolveLogicalPixelScale(cameraData.camera));
            material.SetFloat(LogicalPixelOriginYId, StageHeatDistortionRendererFeature.ResolveLogicalPixelOriginY(cameraData.camera));
            RenderGraphUtils.BlitMaterialParameters parameters = new(source, destination, material, 0);
            renderGraph.AddBlitPass(parameters, PassName);

            // Subsequent renderer passes use the refracted image without an extra copy.
            resourceData.cameraColor = destination;
        }

        private void LogStage33LeftEdgeCorrection(Camera camera, TextureDesc sourceDescriptor)
        {
            if (!isStage33Ripple || camera == null)
                return;

            int cameraId = camera.GetEntityId().GetHashCode();
            int materialId = material.GetEntityId().GetHashCode();
            if (cameraId == lastLoggedCameraId && materialId == lastLoggedMaterialId)
                return;

            lastLoggedCameraId = cameraId;
            lastLoggedMaterialId = materialId;

            int logicalPixelScale = StageHeatDistortionRendererFeature.ResolveLogicalPixelScale(camera);
            int maximumOffsetLogicalPixels = Mathf.Max(1, Mathf.RoundToInt(
                material.GetFloat(BandsPerDirectionId)));
            int protectedPhysicalPixels = maximumOffsetLogicalPixels * logicalPixelScale;

            Debug.Log(
                $"[StageWaterBandRipple] Correcao da borda esquerda ativa na camera " +
                $"'{camera.name}': origem {sourceDescriptor.width}x{sourceDescriptor.height}, " +
                $"escala logica {logicalPixelScale}x. Nos deslocamentos para a direita, " +
                $"as primeiras ate {maximumOffsetLogicalPixels} colunas logicas " +
                $"({protectedPhysicalPixels}px fisicos) preservam o pixel visivel original; " +
                $"a borda nao usa mais clamp/repeticao da coluna x=0.");
        }

        private static bool ShouldRunFor(Camera camera)
        {
            return camera != null && camera.CompareTag("MainCamera");
        }

    }

    private static int ResolveLogicalPixelScale(Camera camera)
    {
        if (camera == null || !camera.TryGetComponent(out LegacyPixelPerfectCamera pixelPerfectCamera))
            return 1;

        int referenceHeight = Mathf.Max(1, pixelPerfectCamera.refResolutionY);
        Rect finalOutputRect = PixelPerfectViewport.ResolveFinalPixelRect(camera);
        return Mathf.Max(1, Mathf.RoundToInt(finalOutputRect.height / referenceHeight));
    }

    private static float ResolveLogicalPixelOriginY(Camera camera)
    {
        return camera == null ? 0f : PixelPerfectViewport.ResolveFinalPixelRect(camera).yMin;
    }
}

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Applies a colourless heat-refraction pass to the Stage 3-2 gameplay camera.
/// </summary>
public sealed class StageHeatDistortionRendererFeature : ScriptableRendererFeature
{
    [SerializeField] private Material heatDistortionMaterial;
    [SerializeField] private RenderPassEvent injectionPoint = RenderPassEvent.AfterRenderingTransparents;

    private StageHeatDistortionPass pass;

    public override void Create()
    {
        pass = new StageHeatDistortionPass();
        pass.renderPassEvent = injectionPoint;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (heatDistortionMaterial == null || pass == null)
            return;

        pass.Setup(heatDistortionMaterial);
        renderer.EnqueuePass(pass);
    }

    private sealed class StageHeatDistortionPass : ScriptableRenderPass
    {
        private const string PassName = "Stage Heat Distortion";
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
            destinationDescriptor.name = "_StageHeatDistortion";
            destinationDescriptor.clearBuffer = false;
            TextureHandle destination = renderGraph.CreateTexture(destinationDescriptor);

            material.SetFloat(UnscaledTimeId, Time.unscaledTime);
            RenderGraphUtils.BlitMaterialParameters parameters = new(source, destination, material, 0);
            renderGraph.AddBlitPass(parameters, PassName);

            // Subsequent renderer passes use the refracted image without an extra copy.
            resourceData.cameraColor = destination;
        }

        private static bool ShouldRunFor(Camera camera)
        {
            return camera != null && camera.CompareTag("MainCamera") &&
                   SceneManager.GetActiveScene().name == "Stage_3-2";
        }
    }
}

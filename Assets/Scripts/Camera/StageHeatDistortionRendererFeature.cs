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
    private static readonly int LogicalPixelOriginYId = Shader.PropertyToID("_LogicalPixelOriginY");

    [SerializeField] private Material heatDistortionMaterial;
    [SerializeField] private Material stage33DistortionMaterial;
    [SerializeField] private bool logStage33BandProgress = true;
    [SerializeField] private RenderPassEvent injectionPoint = RenderPassEvent.AfterRenderingTransparents;

    private StageHeatDistortionPass pass;
    private string lastLoggedSceneName;
    private int lastLoggedScanLineIndex = int.MinValue;
    private int lastLoggedTravelledPixel = int.MinValue;

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
            lastLoggedTravelledPixel = int.MinValue;
        }

        Material distortionMaterial = GetSceneDistortionMaterial(sceneName);
        if (distortionMaterial == null)
            return;

        Camera camera = renderingData.cameraData.camera;
        LogStage33BandRippleOnce(sceneName, distortionMaterial, camera);
        LogStage33BandProgress(sceneName, distortionMaterial, camera);
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

    private void LogStage33BandRippleOnce(string sceneName, Material material, Camera camera)
    {
        if (camera == null || !camera.CompareTag("MainCamera"))
            return;

        if (lastLoggedSceneName == sceneName)
            return;

        lastLoggedSceneName = sceneName;
        if (sceneName != Stage33SceneName)
            return;

        int lineSpacing = Mathf.Max(1, Mathf.RoundToInt(material.GetFloat(BandHeightPixelsId)));
        int logicalHeight = ResolveLogicalScreenHeight(camera);
        int logicalPixelScale = ResolveLogicalPixelScale(camera);
        int logicalPixelOriginY = Mathf.RoundToInt(ResolveLogicalPixelOriginY(camera));
        int visibleLines = CountVisibleScanLines(logicalHeight, lineSpacing, travelledPixels: 0);

        Debug.Log(
            $"[StageWaterBandRipple] Ativo: {material.name}; " +
            $"altura logica {logicalHeight}px, escala atual {logicalPixelScale}x; " +
            $"origem vertical final y={logicalPixelOriginY}px; " +
            $"espacamento {lineSpacing}px = {lineSpacing * logicalPixelScale}px fisicos; " +
            $"linhas simultaneas calculadas: {visibleLines}. " +
            $"Cada linha tem 1px logico e acumula de 0 ate " +
            $"{material.GetFloat(BandsPerDirectionId):0}px antes de inverter.");
    }

    private void LogStage33BandProgress(string sceneName, Material material, Camera camera)
    {
        if (!logStage33BandProgress || sceneName != Stage33SceneName ||
            camera == null || !camera.CompareTag("MainCamera"))
            return;

        int lineSpacing = Mathf.Max(1, Mathf.RoundToInt(material.GetFloat(BandHeightPixelsId)));
        if (GamePauseController.IsPaused)
            return;

        int travelledPixels = Mathf.FloorToInt(Time.time * material.GetFloat(BandScrollSpeedId));
        LogStage33LogicalPixelStep(material, camera, lineSpacing, travelledPixels);
        int enteredLineIndex = Mathf.FloorToInt(-(float)travelledPixels / lineSpacing);
        if (enteredLineIndex == lastLoggedScanLineIndex)
            return;

        lastLoggedScanLineIndex = enteredLineIndex;
        int stepsPerDirection = Mathf.Max(1, Mathf.RoundToInt(material.GetFloat(BandsPerDirectionId)));
        int passedLineCount = -enteredLineIndex;
        int previousOffset = GetAccumulatedOffset(passedLineCount - 1, stepsPerDirection);
        int accumulatedOffset = GetAccumulatedOffset(passedLineCount, stepsPerDirection);
        string direction = accumulatedOffset >= previousOffset ? "direita" : "esquerda";
        int logicalHeight = ResolveLogicalScreenHeight(camera);
        int visibleLines = CountVisibleScanLines(logicalHeight, lineSpacing, travelledPixels);
        Debug.Log($"[StageWaterBandRipple] Linha {enteredLineIndex} entrou pelo rodape; " +
                  $"a linha horizontal inteira atualizou o deslocamento acumulado de " +
                  $"{previousOffset}px para {accumulatedOffset}px logicos ({direction}). " +
                  $"Linhas simultaneas calculadas agora: {visibleLines}.");
    }

    private void LogStage33LogicalPixelStep(Material material, Camera camera, int lineSpacing, int travelledPixels)
    {
        if (travelledPixels == lastLoggedTravelledPixel)
            return;

        lastLoggedTravelledPixel = travelledPixels;
        int logicalHeight = ResolveLogicalScreenHeight(camera);
        int logicalPixelScale = ResolveLogicalPixelScale(camera);
        int firstChangedLogicalRow = travelledPixels % lineSpacing;
        int visibleLines = CountVisibleScanLines(logicalHeight, lineSpacing, travelledPixels);
        int physicalRowStart = Mathf.RoundToInt(ResolveLogicalPixelOriginY(camera)) +
                               firstChangedLogicalRow * logicalPixelScale;
        int physicalRowEnd = physicalRowStart + logicalPixelScale - 1;

        Debug.Log($"[StageWaterBandRipple] Passo SNES {travelledPixels}: " +
                  $"{visibleLines} linhas logicas atualizadas juntas; a primeira e y={firstChangedLogicalRow} " +
                  $"na resolucao {logicalHeight}px. Cada linha usa exatamente as linhas fisicas " +
                  $"y={physicalRowStart}-{physicalRowEnd} ({logicalPixelScale}x), sem interpolacao.");
    }

    private static int GetAccumulatedOffset(int passedLineCount, int stepsPerDirection)
    {
        int directionCycle = stepsPerDirection * 2;
        int cyclePosition = passedLineCount % directionCycle;
        if (cyclePosition < 0)
            cyclePosition += directionCycle;

        return cyclePosition <= stepsPerDirection
            ? cyclePosition
            : directionCycle - cyclePosition;
    }

    private static int CountVisibleScanLines(int logicalHeight, int lineSpacing, int travelledPixels)
    {
        int firstVisibleLine = travelledPixels % lineSpacing;
        if (firstVisibleLine < 0)
            firstVisibleLine += lineSpacing;

        return firstVisibleLine >= logicalHeight
            ? 0
            : 1 + (logicalHeight - 1 - firstVisibleLine) / lineSpacing;
    }

    private static int ResolveLogicalScreenHeight(Camera camera)
    {
        if (camera != null && camera.TryGetComponent(out LegacyPixelPerfectCamera pixelPerfectCamera))
            return Mathf.Max(1, pixelPerfectCamera.refResolutionY);

        return Mathf.Max(1, camera != null ? camera.pixelHeight : 224);
    }

    private static int ResolveLogicalPixelScale(Camera camera)
    {
        if (camera == null || !camera.TryGetComponent(out LegacyPixelPerfectCamera pixelPerfectCamera))
            return 1;

        int referenceHeight = Mathf.Max(1, pixelPerfectCamera.refResolutionY);
        Rect finalOutputRect = PixelPerfectViewport.ResolveFinalPixelRect(camera);
        return Mathf.Max(1, Mathf.RoundToInt(finalOutputRect.height / referenceHeight));
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

        private static bool ShouldRunFor(Camera camera)
        {
            return camera != null && camera.CompareTag("MainCamera");
        }

    }

    private static float ResolveLogicalPixelOriginY(Camera camera)
    {
        return camera == null ? 0f : PixelPerfectViewport.ResolveFinalPixelRect(camera).yMin;
    }
}

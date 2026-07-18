using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D;

/// <summary>
/// Emits one rendering diagnostic per camera for the scene-specific Flower_6
/// investigation. Attach only to the authored object being inspected.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class FlowerRenderDiagnostics : MonoBehaviour
{
    private const string LogPrefix = "[FlowerRenderDiagnostics]";

    private readonly HashSet<int> loggedCameraIds = new();
    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void OnWillRenderObject()
    {
        if (!Application.isPlaying || spriteRenderer == null || spriteRenderer.sprite == null)
            return;

        Camera camera = Camera.current;
        if (camera == null || !loggedCameraIds.Add(camera.GetInstanceID()))
            return;

        LogRenderState(camera);
    }

    private void LogRenderState(Camera camera)
    {
        Sprite sprite = spriteRenderer.sprite;
        Texture2D texture = sprite.texture;
        PixelPerfectCamera pixelPerfectCamera = camera.GetComponent<PixelPerfectCamera>();
        float assetsPpu = pixelPerfectCamera != null ? pixelPerfectCamera.assetsPPU : sprite.pixelsPerUnit;
        Vector2 cameraRelativePixels = ((Vector2)transform.position - (Vector2)camera.transform.position) * assetsPpu;
        Vector2 transformGridResidual = new(
            DistanceToInteger(cameraRelativePixels.x),
            DistanceToInteger(cameraRelativePixels.y));

        Bounds bounds = spriteRenderer.bounds;
        Vector3 screenMin = camera.WorldToScreenPoint(bounds.min);
        Vector3 screenMax = camera.WorldToScreenPoint(bounds.max);
        Vector3 screenCenter = camera.WorldToScreenPoint(bounds.center);
        Vector2 uvMin = new(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 uvMax = new(float.NegativeInfinity, float.NegativeInfinity);

        foreach (Vector2 uv in sprite.uv)
        {
            uvMin = Vector2.Min(uvMin, uv);
            uvMax = Vector2.Max(uvMax, uv);
        }

        Debug.LogWarning(
            $"{LogPrefix} object:{name} camera:{camera.name} frame:{Time.frameCount}\n" +
            $"sprite:{sprite.name} rect:{sprite.rect} textureRect:{sprite.textureRect} offset:{sprite.textureRectOffset} " +
            $"texture:{texture.name} {texture.width}x{texture.height} filter:{texture.filterMode} wrap:{texture.wrapMode} " +
            $"mipmaps:{texture.mipmapCount} packed:{sprite.packed} packing:{sprite.packingMode} uv:[{uvMin}..{uvMax}]\n" +
            $"renderer sorting:{spriteRenderer.sortingLayerName}/{spriteRenderer.sortingOrder} drawMode:{spriteRenderer.drawMode} " +
            $"flip:{spriteRenderer.flipX}/{spriteRenderer.flipY} scale:{transform.lossyScale} bounds:{bounds}\n" +
            $"world:{transform.position} cameraWorld:{camera.transform.position} assetsPPU:{assetsPpu:F3} " +
            $"cameraRelativePx:{cameraRelativePixels} residualToIntegerPx:{transformGridResidual}\n" +
            $"screen center:{screenCenter} min:{screenMin} max:{screenMax} size:{screenMax - screenMin} " +
            $"cameraPixelRect:{camera.pixelRect} orthoSize:{camera.orthographicSize:F4} " +
            $"pixelPerfect:{DescribePixelPerfectCamera(pixelPerfectCamera)}",
            this);
    }

    private static float DistanceToInteger(float value)
    {
        return Mathf.Abs(value - Mathf.Round(value));
    }

    private static string DescribePixelPerfectCamera(PixelPerfectCamera pixelPerfectCamera)
    {
        if (pixelPerfectCamera == null)
            return "none";

        return $"assetsPPU:{pixelPerfectCamera.assetsPPU} ref:{pixelPerfectCamera.refResolutionX}x{pixelPerfectCamera.refResolutionY} " +
               $"upscaleRT:{pixelPerfectCamera.upscaleRT} pixelSnapping:{pixelPerfectCamera.pixelSnapping} " +
               $"cropX:{pixelPerfectCamera.cropFrameX} cropY:{pixelPerfectCamera.cropFrameY} stretch:{pixelPerfectCamera.stretchFill}";
    }
}

using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives the Stage 3-2 heat-haze material with real time so it keeps moving
/// while the stage-intro sequence has gameplay paused.
/// </summary>
public sealed class StageHeatHazeAnimator : MonoBehaviour
{
    private static readonly int UnscaledTimeId = Shader.PropertyToID("_UnscaledTime");

    [SerializeField] private Image heatHazeImage;

    private Material runtimeMaterial;

    private void Awake()
    {
        if (heatHazeImage == null)
        {
            enabled = false;
            return;
        }

        Material sourceMaterial = heatHazeImage.material;
        if (sourceMaterial == null)
        {
            enabled = false;
            return;
        }

        runtimeMaterial = new Material(sourceMaterial);
        heatHazeImage.material = runtimeMaterial;
        ApplyTime();
    }

    private void Update() => ApplyTime();

    private void OnDestroy()
    {
        if (runtimeMaterial != null)
            Destroy(runtimeMaterial);
    }

    private void ApplyTime()
    {
        if (runtimeMaterial != null)
            runtimeMaterial.SetFloat(UnscaledTimeId, Time.unscaledTime);
    }
}

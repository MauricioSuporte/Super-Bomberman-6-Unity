using UnityEngine;

[RequireComponent(typeof(Camera))]
public sealed class CameraAspectViewport : MonoBehaviour
{
    [SerializeField] private float targetAspect = 4f / 3f;

    Camera cam;

    void Awake()
    {
        cam = GetComponent<Camera>();
        Apply();
    }

    void OnEnable() => Apply();
    void OnPreCull() => Apply();

    void Apply()
    {
        if (cam == null) return;

        float windowAspect = (float)Screen.width / Screen.height;
        float scaleHeight = windowAspect / targetAspect;

        if (scaleHeight < 1f)
        {
            cam.rect = new Rect(0f, (1f - scaleHeight) * 0.5f, 1f, scaleHeight);
        }
        else
        {
            float scaleWidth = 1f / scaleHeight;
            cam.rect = new Rect((1f - scaleWidth) * 0.5f, 0f, scaleWidth, 1f);
        }
    }
}

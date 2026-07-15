using UnityEngine;

namespace StageAssets
{
    [DisallowMultipleComponent]
    public class BubbleChipFloatBobber : MonoBehaviour
    {
        [SerializeField, Min(1)] private int pixelsPerUnit = 16;
        [SerializeField, Min(0)] private int amplitudePixels = 2;
        [SerializeField, Min(0.0001f)] private float cycleDuration = 1.6f;
        [SerializeField] private float phaseOffset;
        [SerializeField] private bool useUnscaledTime;
        [SerializeField] private bool respectGamePause = true;

        private Vector3 baseLocalPosition;
        private float elapsed;
        private bool capturedBaseLocalPosition;

        void OnEnable()
        {
            CaptureBaseLocalPosition();
            elapsed = 0f;
            ApplyFloat();
        }

        void OnDisable()
        {
            if (capturedBaseLocalPosition)
                transform.localPosition = baseLocalPosition;
        }

        void Update()
        {
            if (respectGamePause && GamePauseController.IsPaused)
                return;

            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            if (dt <= 0f)
                return;

            elapsed += dt;
            ApplyFloat();
        }

        void OnValidate()
        {
            if (pixelsPerUnit < 1)
                pixelsPerUnit = 1;

            if (cycleDuration < 0.0001f)
                cycleDuration = 0.0001f;

            if (amplitudePixels < 0)
                amplitudePixels = 0;
        }

        private void CaptureBaseLocalPosition()
        {
            if (capturedBaseLocalPosition)
                return;

            baseLocalPosition = transform.localPosition;
            capturedBaseLocalPosition = true;
        }

        private void ApplyFloat()
        {
            CaptureBaseLocalPosition();

            float cycle = (elapsed / cycleDuration) + phaseOffset;
            float rawPixels = Mathf.Sin(cycle * Mathf.PI * 2f) * amplitudePixels;
            float snappedOffset = Mathf.RoundToInt(rawPixels) / (float)Mathf.Max(1, pixelsPerUnit);

            transform.localPosition = new Vector3(
                baseLocalPosition.x,
                baseLocalPosition.y + snappedOffset,
                baseLocalPosition.z);
        }
    }
}

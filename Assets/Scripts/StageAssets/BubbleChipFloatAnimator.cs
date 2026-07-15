using UnityEngine;

namespace StageAssets
{
    public class BubbleChipFloatAnimator : MonoBehaviour
    {
        [SerializeField] private AnimatedSpriteRenderer[] renderers;

        private Vector3[] rendererBaseLocalPositions;
        private float frameTimer;
        private int frame;
        private bool capturedBaseLocalPosition;

        void Awake()
        {
            CacheRenderersIfNeeded();
        }

        void OnEnable()
        {
            CacheRenderersIfNeeded();
            CaptureBaseLocalPosition();
            frameTimer = 0f;
            frame = 0;
            SetManualAnimation(true);
            ClearRendererFrameOffsets();
            ApplyFrame();
        }

        void OnDisable()
        {
            RestoreRendererBaseLocalPositions();
            SetManualAnimation(false);
        }

        void Update()
        {
            Advance(Time.unscaledDeltaTime, Time.deltaTime);
            ApplyFrame();
        }

        void LateUpdate()
        {
            ApplyFrame();
        }

        private void Advance(float unscaledDeltaTime, float scaledDeltaTime)
        {
            CacheRenderersIfNeeded();

            if (!HasRenderers())
                return;

            AnimatedSpriteRenderer reference = renderers[0];
            if (reference == null)
                return;

            if (reference.RespectGamePause && GamePauseController.IsPaused)
                return;

            Sprite[] sprites = reference.animationSprite;
            int frameCount = sprites != null && sprites.Length > 0 ? sprites.Length : 1;
            float dt = reference.UseUnscaledTime ? unscaledDeltaTime : scaledDeltaTime;
            if (dt <= 0f)
                return;

            float frameDuration = Mathf.Max(0.0001f, reference.animationTime);
            frameTimer += Mathf.Min(dt, frameDuration);

            if (frameTimer >= frameDuration)
            {
                frameTimer -= frameDuration;
                frame = (frame + 1) % frameCount;
            }
        }

        private void CacheRenderersIfNeeded()
        {
            if (HasRenderers())
                return;

            renderers = GetComponentsInChildren<AnimatedSpriteRenderer>(true);
            SetManualAnimation(true);
        }

        private void CaptureBaseLocalPosition()
        {
            if (capturedBaseLocalPosition)
                return;

            CaptureRendererBaseLocalPositions();
            capturedBaseLocalPosition = true;
        }

        private void CaptureRendererBaseLocalPositions()
        {
            if (renderers == null)
                return;

            rendererBaseLocalPositions = new Vector3[renderers.Length];

            for (int i = 0; i < renderers.Length; i++)
            {
                AnimatedSpriteRenderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                rendererBaseLocalPositions[i] = renderer.transform.localPosition;
            }
        }

        private bool HasRenderers()
        {
            if (renderers == null || renderers.Length == 0)
                return false;

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                    return true;
            }

            return false;
        }

        private void SetManualAnimation(bool value)
        {
            if (renderers == null)
                return;

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                    renderers[i].SetManualAnimationUpdate(value);
            }
        }

        private void ClearRendererFrameOffsets()
        {
            if (renderers == null)
                return;

            for (int i = 0; i < renderers.Length; i++)
            {
                AnimatedSpriteRenderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                renderer.frameOffsets = null;
            }
        }

        private void ApplyFrame()
        {
            if (renderers == null)
                return;

            CaptureBaseLocalPosition();

            for (int i = 0; i < renderers.Length; i++)
            {
                AnimatedSpriteRenderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                int count = renderer.animationSprite != null && renderer.animationSprite.Length > 0
                    ? renderer.animationSprite.Length
                    : 1;

                renderer.CurrentFrame = frame % count;
                renderer.RefreshFrame();
            }
        }

        private void RestoreRendererBaseLocalPositions()
        {
            if (rendererBaseLocalPositions == null || renderers == null)
                return;

            for (int i = 0; i < renderers.Length && i < rendererBaseLocalPositions.Length; i++)
            {
                if (renderers[i] != null)
                    renderers[i].transform.localPosition = rendererBaseLocalPositions[i];
            }
        }
    }
}

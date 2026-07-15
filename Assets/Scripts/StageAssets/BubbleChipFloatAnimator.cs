using UnityEngine;

namespace StageAssets
{
    public class BubbleChipFloatAnimator : MonoBehaviour
    {
        [SerializeField] private AnimatedSpriteRenderer[] renderers;
        [SerializeField] private Vector2[] frameOffsets =
        {
            Vector2.zero,
            new Vector2(0f, 0.1f),
            new Vector2(0f, 0.2f),
            new Vector2(0f, 0.1f),
        };

        private Vector3 baseLocalPosition;
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
            if (capturedBaseLocalPosition)
                transform.localPosition = baseLocalPosition;

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

            baseLocalPosition = transform.localPosition;
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
            Vector2 sharedOffset = GetSharedOffset(frame);
            transform.localPosition = baseLocalPosition;

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
                ApplySharedRendererOffset(i, sharedOffset);
            }
        }

        private Vector2 GetSharedOffset(int frameIndex)
        {
            if (frameOffsets == null || frameOffsets.Length == 0)
                return Vector2.zero;

            int offsetIndex = Mathf.Clamp(frameIndex, 0, frameOffsets.Length - 1);
            return frameOffsets[offsetIndex];
        }

        private void ApplySharedRendererOffset(int rendererIndex, Vector2 sharedOffset)
        {
            if (rendererBaseLocalPositions == null ||
                rendererIndex < 0 ||
                rendererIndex >= rendererBaseLocalPositions.Length ||
                renderers == null ||
                rendererIndex >= renderers.Length)
            {
                return;
            }

            AnimatedSpriteRenderer renderer = renderers[rendererIndex];
            if (renderer == null)
                return;

            Vector3 basePosition = rendererBaseLocalPositions[rendererIndex];
            renderer.transform.localPosition = new Vector3(
                basePosition.x + sharedOffset.x,
                basePosition.y + sharedOffset.y,
                basePosition.z);
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

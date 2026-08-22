using UnityEngine;

/// <summary>
/// Drives Crab's shared movement animation: two 1-2 loops, then frames 3-10.
/// </summary>
[DefaultExecutionOrder(100)]
public sealed class CrabMovementAnimationController : MonoBehaviour
{
    [SerializeField] private AnimatedSpriteRenderer movementSprite;

    private static readonly int[] FramePattern = { 0, 1, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };

    private int patternIndex;
    private float frameTimer;

    private void Awake()
    {
        if (movementSprite == null)
            movementSprite = GetComponentInChildren<AnimatedSpriteRenderer>();
    }

    private void OnEnable()
    {
        patternIndex = 0;
        frameTimer = 0f;
        ConfigureMovementSprite();
    }

    private void OnDisable()
    {
        if (movementSprite != null)
            movementSprite.SetManualAnimationUpdate(false);
    }

    private void LateUpdate()
    {
        if (movementSprite == null || !movementSprite.isActiveAndEnabled ||
            movementSprite.animationSprite == null || movementSprite.animationSprite.Length < 10)
        {
            return;
        }

        if (movementSprite.RespectGamePause && GamePauseController.IsPaused)
            return;

        float deltaTime = movementSprite.UseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        float frameDuration = Mathf.Max(0.0001f, movementSprite.animationTime);
        frameTimer += deltaTime;

        while (frameTimer >= frameDuration)
        {
            frameTimer -= frameDuration;
            patternIndex = (patternIndex + 1) % FramePattern.Length;
        }

        movementSprite.CurrentFrame = FramePattern[patternIndex];
        movementSprite.RefreshFrame();
    }

    private void ConfigureMovementSprite()
    {
        if (movementSprite == null)
            return;

        movementSprite.idle = false;
        movementSprite.loop = true;
        movementSprite.SetManualAnimationUpdate(true);
        movementSprite.CurrentFrame = FramePattern[patternIndex];
        movementSprite.RefreshFrame();
    }
}

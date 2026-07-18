using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class FlowerMovementController : JunctionTurningEnemyMovementController
{
    private const int StateAnimationCycles = 3;

    [Header("Flower Visuals")]
    [SerializeField] private AnimatedSpriteRenderer openSprite;
    [SerializeField] private AnimatedSpriteRenderer closedSprite;
    [SerializeField] private AnimatedSpriteRenderer transitionSprite;

    private Coroutine stateCycle;
    private bool isOpen = true;

    protected override void Awake()
    {
        base.Awake();

        // The base controller uses its directional sprites for death cleanup.
        spriteUp = openSprite;
        spriteDown = openSprite;
        spriteLeft = openSprite;
        waitForFullDeathAnimation = true;

        ShowStaticSprite(openSprite);
    }

    protected override void Start()
    {
        base.Start();

        if (!isDead)
            stateCycle = StartCoroutine(StateCycle());
    }

    protected override void UpdateSpriteDirection(Vector2 dir)
    {
        // Flower has a single visual state independent of its movement direction.
    }

    protected override void Die()
    {
        if (isDead)
            return;

        if (stateCycle != null)
        {
            StopCoroutine(stateCycle);
            stateCycle = null;
        }

        DisableFlowerVisuals();
        base.Die();
    }

    protected override void OnDestroy()
    {
        if (stateCycle != null)
            StopCoroutine(stateCycle);

        base.OnDestroy();
    }

    private IEnumerator StateCycle()
    {
        while (!isDead)
        {
            yield return PlayStateAnimation(isOpen ? openSprite : closedSprite);

            if (isDead)
                yield break;

            bool closing = isOpen;
            yield return PlayTransition(closing);

            if (isDead)
                yield break;

            isOpen = !isOpen;
        }
    }

    private IEnumerator PlayStateAnimation(AnimatedSpriteRenderer sprite)
    {
        ShowStaticSprite(sprite);

        if (sprite != null && sprite.animationSprite != null && sprite.animationSprite.Length > 0)
            yield return PlayConfiguredAnimation(sprite, StateAnimationCycles, reverse: false);
    }

    private IEnumerator PlayTransition(bool reverse)
    {
        DisableVisual(openSprite);
        DisableVisual(closedSprite);

        if (transitionSprite == null || transitionSprite.animationSprite == null ||
            transitionSprite.animationSprite.Length == 0)
            yield break;

        transitionSprite.enabled = true;
        transitionSprite.idle = false;
        transitionSprite.loop = false;
        transitionSprite.SetManualAnimationUpdate(true);
        activeSprite = transitionSprite;
        SetFlipX(transitionSprite, false);

        int lastFrame = transitionSprite.animationSprite.Length - 1;

        for (int frameOffset = 0; frameOffset <= lastFrame && !isDead; frameOffset++)
        {
            int frame = reverse ? lastFrame - frameOffset : frameOffset;
            transitionSprite.CurrentFrame = frame;
            transitionSprite.RefreshFrame();
            yield return WaitForAnimationSeconds(GetConfiguredFrameDuration(transitionSprite, frame));
        }

        transitionSprite.SetManualAnimationUpdate(false);
        DisableVisual(transitionSprite);
    }

    private void ShowStaticSprite(AnimatedSpriteRenderer sprite)
    {
        DisableFlowerVisuals();

        if (sprite == null)
            return;

        sprite.enabled = true;
        sprite.idle = true;
        sprite.RefreshFrame();
        activeSprite = sprite;
        SetFlipX(sprite, false);
    }

    private void DisableFlowerVisuals()
    {
        DisableVisual(openSprite);
        DisableVisual(closedSprite);

        if (transitionSprite != null)
        {
            transitionSprite.SetManualAnimationUpdate(false);
            DisableVisual(transitionSprite);
        }
    }

    private static void DisableVisual(AnimatedSpriteRenderer sprite)
    {
        if (sprite != null)
            sprite.enabled = false;
    }

    private IEnumerator PlayConfiguredAnimation(AnimatedSpriteRenderer sprite, int cycles, bool reverse)
    {
        sprite.idle = false;
        sprite.loop = false;
        sprite.SetManualAnimationUpdate(true);

        int lastFrame = sprite.animationSprite.Length - 1;

        for (int cycle = 0; cycle < cycles && !isDead; cycle++)
        {
            for (int frameOffset = 0; frameOffset <= lastFrame && !isDead; frameOffset++)
            {
                int frame = reverse ? lastFrame - frameOffset : frameOffset;
                sprite.CurrentFrame = frame;
                sprite.RefreshFrame();
                yield return WaitForAnimationSeconds(GetConfiguredFrameDuration(sprite, frame));
            }
        }

        sprite.SetManualAnimationUpdate(false);
        sprite.idle = true;
        sprite.CurrentFrame = 0;
        sprite.RefreshFrame();
    }

    private static IEnumerator WaitForAnimationSeconds(float seconds)
    {
        float remaining = Mathf.Max(0.0001f, seconds);

        while (remaining > 0f)
        {
            if (!GamePauseController.IsPaused)
                remaining -= Time.unscaledDeltaTime;

            yield return null;
        }
    }

    private static float GetConfiguredFrameDuration(AnimatedSpriteRenderer sprite, int frame)
    {
        if (sprite.frameDurations != null &&
            sprite.frameDurations.Length == sprite.animationSprite.Length)
        {
            return Mathf.Max(0.0001f, sprite.frameDurations[frame]);
        }

        if (sprite.useSequenceDuration)
        {
            int framesPerCycle = sprite.pingPong && sprite.animationSprite.Length > 1
                ? sprite.animationSprite.Length * 2 - 2
                : sprite.animationSprite.Length;

            return Mathf.Max(0.0001f, sprite.sequenceDuration / framesPerCycle);
        }

        return Mathf.Max(0.0001f, sprite.animationTime);
    }

    private static void SetFlipX(AnimatedSpriteRenderer sprite, bool flipX)
    {
        if (sprite != null && sprite.TryGetComponent(out SpriteRenderer renderer))
            renderer.flipX = flipX;
    }
}

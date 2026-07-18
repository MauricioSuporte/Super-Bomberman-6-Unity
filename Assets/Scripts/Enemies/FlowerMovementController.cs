using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class FlowerMovementController : JunctionTurningEnemyMovementController
{
    private const float StateDurationSeconds = 1f;
    private const float TransitionDurationSeconds = 0.5f;
    private const float FlipIntervalSeconds = 1f / 6f;

    [Header("Flower Visuals")]
    [SerializeField] private AnimatedSpriteRenderer openSprite;
    [SerializeField] private AnimatedSpriteRenderer closedSprite;
    [SerializeField] private AnimatedSpriteRenderer transitionSprite;

    private Coroutine stateCycle;
    private bool isOpen = true;
    private bool isFlipped;
    private float flipTimer;

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

    private void Update()
    {
        if (isDead)
            return;

        flipTimer += Time.deltaTime;
        while (flipTimer >= FlipIntervalSeconds)
        {
            flipTimer -= FlipIntervalSeconds;
            isFlipped = !isFlipped;
            ApplyFlip(activeSprite);
        }
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
            // Open and Closed each remain visible for a full second before changing.
            yield return new WaitForSeconds(StateDurationSeconds);

            if (isDead)
                yield break;

            bool closing = isOpen;
            yield return PlayTransition(closing);

            if (isDead)
                yield break;

            isOpen = !isOpen;
            ShowStaticSprite(isOpen ? openSprite : closedSprite);
        }
    }

    private IEnumerator PlayTransition(bool reverse)
    {
        DisableVisual(openSprite);
        DisableVisual(closedSprite);

        if (transitionSprite == null || transitionSprite.animationSprite == null ||
            transitionSprite.animationSprite.Length == 0)
        {
            yield return new WaitForSeconds(TransitionDurationSeconds);
            yield break;
        }

        transitionSprite.enabled = true;
        transitionSprite.idle = false;
        transitionSprite.loop = false;
        transitionSprite.SetManualAnimationUpdate(true);
        activeSprite = transitionSprite;

        int lastFrame = transitionSprite.animationSprite.Length - 1;
        float elapsed = 0f;

        while (elapsed < TransitionDurationSeconds && !isDead)
        {
            float normalized = Mathf.Clamp01(elapsed / TransitionDurationSeconds);
            int frame = Mathf.Min(lastFrame, Mathf.FloorToInt(normalized * (lastFrame + 1)));
            transitionSprite.CurrentFrame = reverse ? lastFrame - frame : frame;
            transitionSprite.RefreshFrame();
            ApplyFlip(transitionSprite);

            elapsed += Time.deltaTime;
            yield return null;
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
        ApplyFlip(sprite);
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

    private void ApplyFlip(AnimatedSpriteRenderer sprite)
    {
        if (sprite != null && sprite.TryGetComponent(out SpriteRenderer renderer))
            renderer.flipX = isFlipped;
    }
}

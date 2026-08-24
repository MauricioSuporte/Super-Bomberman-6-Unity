using System.Collections;
using UnityEngine;

/// <summary>
/// Junction-turning submarine which alternates between surfaced and submerged
/// movement. All directional sequences are authored in the prefab from the
/// explicitly sliced 32x32 source sheet.
/// </summary>
public sealed class SubmarineMovementController : JunctionTurningEnemyMovementController
{
    private const float SurfaceDuration = 10f;
    private const float SubmergedDuration = 10f;
    private const float FrameDuration = 0.25f;

    private enum SubmarineState { Surfaced, Entering, Submerged, WaitingToSurface, Exiting }

    [Header("Submarine Sheet")]
    [SerializeField] private AnimatedSpriteRenderer downVisual;
    [SerializeField] private AnimatedSpriteRenderer upVisual;
    [SerializeField] private AnimatedSpriteRenderer leftVisual;
    [SerializeField] private AnimatedSpriteRenderer enterDownVisual;
    [SerializeField] private AnimatedSpriteRenderer enterUpVisual;
    [SerializeField] private AnimatedSpriteRenderer enterLeftVisual;
    [SerializeField] private AnimatedSpriteRenderer submergedDownVisual;
    [SerializeField] private AnimatedSpriteRenderer submergedUpVisual;
    [SerializeField] private AnimatedSpriteRenderer submergedLeftVisual;
    [SerializeField] private AnimatedSpriteRenderer deathVisual;

    [Header("Submarine Shadow")]
    [SerializeField, Min(0f)] private float hoverBaseHeight = 0.45f;
    [SerializeField, Min(0f)] private float hoverAmplitude = 0.08f;
    [SerializeField, Min(0.01f)] private float hoverFrequency = 3f;
    [SerializeField] private Color shadowColor = new(0f, 0f, 0f, 0.45f);
    [SerializeField] private Vector2 shadowScale = new(0.9f, 0.9f);
    [SerializeField] private Vector2 shadowOffset = Vector2.zero;

    private Sprite[] walkDown;
    private Sprite[] walkUp;
    private Sprite[] walkLeft;
    private Sprite[] enterDown;
    private Sprite[] enterUp;
    private Sprite[] enterLeft;
    private Sprite[] submergedDown;
    private Sprite[] submergedUp;
    private Sprite[] submergedLeft;

    private CharacterHealth health;
    private AnimatedSpriteRenderer visual;
    private GameObject shadow;
    private Sprite shadowSprite;
    private SubmarineState state;
    private float stateTimer;

    protected override void Awake()
    {
        ConfigureRenderers();
        health = GetComponent<CharacterHealth>();
        base.Awake();
    }

    protected override void Start()
    {
        base.Start();
        CreateShadow();
        EnterSurfacedState();
    }

    protected override void FixedUpdate()
    {
        if (isDead)
            return;

        if (TryGetComponent(out StunReceiver stun) && stun != null && stun.IsStunned)
        {
            if (rb != null)
                rb.linearVelocity = Vector2.zero;
            return;
        }

        switch (state)
        {
            case SubmarineState.Surfaced:
            case SubmarineState.Submerged:
                base.FixedUpdate();
                stateTimer -= Time.fixedDeltaTime;
                if (stateTimer <= 0f)
                {
                    if (state == SubmarineState.Surfaced)
                        StartCoroutine(EnterSubmersionRoutine());
                    else
                        StartCoroutine(ExitSubmersionRoutine());
                }
                break;

            default:
                if (rb != null)
                    rb.linearVelocity = Vector2.zero;
                break;
        }
    }

    protected override void UpdateSpriteDirection(Vector2 dir)
    {
        if (state == SubmarineState.Entering || state == SubmarineState.WaitingToSurface || state == SubmarineState.Exiting)
            return;

        if (state == SubmarineState.Submerged)
        {
            // While it travels underwater, every new direction starts already
            // on that direction's final phase-two sprite and remains frozen.
            ShowSubmergedLastFrame(dir);
            return;
        }

        AnimatedSpriteRenderer target = GetWalkVisual(dir);
        SetVisualFrames(target, GetWalkFrames(dir), loop: true);

        if (target != null && target.TryGetComponent(out SpriteRenderer renderer))
            renderer.flipX = dir == Vector2.right;
    }

    protected override void Die()
    {
        SetSubmergedInvulnerability(false);
        DestroyShadow();
        ClearHoverOffset();
        HideAllVisualsExcept(null);
        base.Die();
    }

    protected override void OnDestroy()
    {
        DestroyShadow();

        if (shadowSprite != null)
        {
            Destroy(shadowSprite.texture);
            Destroy(shadowSprite);
        }

        base.OnDestroy();
    }

    private IEnumerator EnterSubmersionRoutine()
    {
        state = SubmarineState.Entering;
        Sprite[] entryFrames = GetEntryFrames(direction);
        AnimatedSpriteRenderer entryVisual = GetEntryVisual(direction);
        SetHorizontalFlip(entryVisual, direction);
        yield return PlayFrames(entryVisual, entryFrames, forward: true, removeShadowOnLastFrame: true);

        // The entire second phase, including its still frames, is invulnerable.
        SetSubmergedInvulnerability(true);
        AnimatedSpriteRenderer submergedVisual = GetSubmergedVisual(direction);
        SetHorizontalFlip(submergedVisual, direction);
        yield return PlayFrames(submergedVisual, GetSubmergedFrames(direction), forward: true, removeShadowOnLastFrame: false);

        state = SubmarineState.Submerged;
        ShowSubmergedLastFrame(direction);
        stateTimer = SubmergedDuration;
    }

    private IEnumerator ExitSubmersionRoutine()
    {
        state = SubmarineState.WaitingToSurface;
        yield return new WaitForSeconds(FrameDuration);

        state = SubmarineState.Exiting;
        CreateShadow();
        AnimatedSpriteRenderer entryVisual = GetEntryVisual(direction);
        SetHorizontalFlip(entryVisual, direction);
        yield return PlayFrames(entryVisual, GetEntryFrames(direction), forward: false, removeShadowOnLastFrame: false);

        SetSubmergedInvulnerability(false);
        EnterSurfacedState();
    }

    private IEnumerator PlayFrames(AnimatedSpriteRenderer target, Sprite[] frames, bool forward, bool removeShadowOnLastFrame)
    {
        if (frames == null || frames.Length == 0 || target == null)
            yield break;

        ShowVisual(target);
        target.SetManualAnimationUpdate(true);
        target.idle = false;
        target.loop = false;
        target.animationSprite = frames;

        int first = forward ? 0 : frames.Length - 1;
        int last = forward ? frames.Length - 1 : 0;
        int increment = forward ? 1 : -1;

        for (int frame = first; ; frame += increment)
        {
            target.CurrentFrame = frame;
            target.RefreshFrame();

            if (removeShadowOnLastFrame && frame == last)
                DestroyShadow();

            yield return new WaitForSeconds(FrameDuration);
            if (frame == last)
                break;
        }
    }

    private void EnterSurfacedState()
    {
        state = SubmarineState.Surfaced;
        stateTimer = SurfaceDuration;
        SetVisualFrames(GetWalkVisual(direction), GetWalkFrames(direction), loop: true);
        CreateShadow();
    }

    private void ShowSubmergedLastFrame(Vector2 movementDirection)
    {
        Sprite[] frames = GetSubmergedFrames(movementDirection);
        AnimatedSpriteRenderer target = GetSubmergedVisual(movementDirection);
        if (target == null || frames == null || frames.Length == 0)
            return;

        ShowVisual(target);
        SetHorizontalFlip(target, movementDirection);
        target.SetManualAnimationUpdate(true);
        target.idle = false;
        target.loop = false;
        target.animationSprite = frames;
        target.CurrentFrame = frames.Length - 1;
        target.RefreshFrame();
    }

    private void SetVisualFrames(AnimatedSpriteRenderer target, Sprite[] frames, bool loop)
    {
        if (target == null || frames == null || frames.Length == 0)
            return;

        ShowVisual(target);
        target.SetManualAnimationUpdate(false);
        target.idleSprite = frames[0];
        target.animationSprite = frames;
        target.animationTime = FrameDuration;
        target.loop = loop;
        target.idle = false;
    }

    private Sprite[] GetWalkFrames(Vector2 dir)
    {
        if (dir == Vector2.up) return walkUp;
        if (dir == Vector2.down) return walkDown;
        return walkLeft;
    }

    private Sprite[] GetEntryFrames(Vector2 dir)
    {
        if (dir == Vector2.up) return enterUp;
        if (dir == Vector2.down) return enterDown;
        return enterLeft;
    }

    private Sprite[] GetSubmergedFrames(Vector2 dir)
    {
        if (dir == Vector2.up) return submergedUp;
        if (dir == Vector2.down) return submergedDown;
        return submergedLeft;
    }

    private void ConfigureRenderers()
    {
        downVisual ??= transform.Find("Down")?.GetComponent<AnimatedSpriteRenderer>();
        upVisual ??= transform.Find("Up")?.GetComponent<AnimatedSpriteRenderer>();
        leftVisual ??= transform.Find("Left")?.GetComponent<AnimatedSpriteRenderer>();
        enterDownVisual ??= transform.Find("Submerge Phase 1 Down")?.GetComponent<AnimatedSpriteRenderer>();
        enterUpVisual ??= transform.Find("Submerge Phase 1 Up")?.GetComponent<AnimatedSpriteRenderer>();
        enterLeftVisual ??= transform.Find("Submerge Phase 1 Left")?.GetComponent<AnimatedSpriteRenderer>();
        submergedDownVisual ??= transform.Find("Submerge Phase 2 Down")?.GetComponent<AnimatedSpriteRenderer>();
        submergedUpVisual ??= transform.Find("Submerge Phase 2 Up")?.GetComponent<AnimatedSpriteRenderer>();
        submergedLeftVisual ??= transform.Find("Submerge Phase 2 Left")?.GetComponent<AnimatedSpriteRenderer>();
        deathVisual ??= transform.Find("Death")?.GetComponent<AnimatedSpriteRenderer>();

        spriteUp = upVisual;
        spriteDown = downVisual;
        spriteLeft = leftVisual;
        spriteRight = leftVisual;
        spriteDeath = deathVisual;

        walkDown = downVisual != null ? downVisual.animationSprite : null;
        walkUp = upVisual != null ? upVisual.animationSprite : null;
        walkLeft = leftVisual != null ? leftVisual.animationSprite : null;
        enterDown = enterDownVisual != null ? enterDownVisual.animationSprite : null;
        enterUp = enterUpVisual != null ? enterUpVisual.animationSprite : null;
        enterLeft = enterLeftVisual != null ? enterLeftVisual.animationSprite : null;
        submergedDown = submergedDownVisual != null ? submergedDownVisual.animationSprite : null;
        submergedUp = submergedUpVisual != null ? submergedUpVisual.animationSprite : null;
        submergedLeft = submergedLeftVisual != null ? submergedLeftVisual.animationSprite : null;

        if (deathVisual == null)
            return;

        deathVisual.animationTime = 0.1f;
        deathVisual.loop = false;
        deathVisual.enabled = false;
    }

    private AnimatedSpriteRenderer GetWalkVisual(Vector2 dir)
        => dir == Vector2.up ? upVisual : dir == Vector2.down ? downVisual : leftVisual;

    private AnimatedSpriteRenderer GetEntryVisual(Vector2 dir)
        => dir == Vector2.up ? enterUpVisual : dir == Vector2.down ? enterDownVisual : enterLeftVisual;

    private AnimatedSpriteRenderer GetSubmergedVisual(Vector2 dir)
        => dir == Vector2.up ? submergedUpVisual : dir == Vector2.down ? submergedDownVisual : submergedLeftVisual;

    private void ShowVisual(AnimatedSpriteRenderer target)
    {
        HideAllVisualsExcept(target);
        visual = target;
        activeSprite = target;
        target.enabled = true;
    }

    private static void SetHorizontalFlip(AnimatedSpriteRenderer target, Vector2 movementDirection)
    {
        if (target != null && target.TryGetComponent(out SpriteRenderer renderer))
            renderer.flipX = movementDirection == Vector2.right;
    }

    private void HideAllVisualsExcept(AnimatedSpriteRenderer target)
    {
        foreach (AnimatedSpriteRenderer renderer in GetComponentsInChildren<AnimatedSpriteRenderer>(true))
        {
            if (renderer == null || renderer == target || renderer == deathVisual)
                continue;
            renderer.SetManualAnimationUpdate(false);
            renderer.enabled = false;
        }
    }


    private void SetSubmergedInvulnerability(bool value)
    {
        if (health != null)
            health.SetExternalInvulnerability(value);
    }

    private void CreateShadow()
    {
        if (shadow != null || isDead)
            return;

        shadow = new GameObject("SubmarineShadow");
        shadow.transform.localScale = new Vector3(shadowScale.x, shadowScale.y, 1f);

        SpriteRenderer renderer = shadow.AddComponent<SpriteRenderer>();
        renderer.sprite = GetShadowSprite();
        renderer.color = shadowColor;
        renderer.sortingOrder = 4;
        SpriteRenderer visualRenderer = visual != null ? visual.GetComponent<SpriteRenderer>() : null;
        if (visualRenderer != null)
        {
            renderer.sortingLayerID = visualRenderer.sortingLayerID;
            renderer.sortingOrder = visualRenderer.sortingOrder - 1;
        }

        UpdateShadowPosition();
    }

    private void LateUpdate()
    {
        if (isDead || isInDamagedLoop || state != SubmarineState.Surfaced)
        {
            ClearHoverOffset();
            UpdateShadowPosition();
            return;
        }

        UpdateShadowPosition();
        ApplyHoverOffset();
    }

    private void ApplyHoverOffset()
    {
        foreach (AnimatedSpriteRenderer animatedSprite in GetComponentsInChildren<AnimatedSpriteRenderer>(true))
        {
            if (animatedSprite != null && animatedSprite != activeSprite)
                animatedSprite.ClearExternalBase();
        }

        if (activeSprite == null)
            return;

        float hoverHeight = hoverBaseHeight + Mathf.Sin(Time.time * hoverFrequency) * hoverAmplitude;
        activeSprite.SetExternalBaseOffsetFromInitial(Vector3.up * hoverHeight);
    }

    private void ClearHoverOffset()
    {
        foreach (AnimatedSpriteRenderer animatedSprite in GetComponentsInChildren<AnimatedSpriteRenderer>(true))
        {
            if (animatedSprite != null)
                animatedSprite.ClearExternalBase();
        }
    }

    private void UpdateShadowPosition()
    {
        if (shadow == null)
            return;

        Vector3 position = transform.position + (Vector3)shadowOffset;
        shadow.transform.position = new Vector3(position.x, position.y, 0f);
    }

    private void DestroyShadow()
    {
        if (shadow != null)
            Destroy(shadow);
        shadow = null;
    }

    private Sprite GetShadowSprite()
    {
        if (shadowSprite != null)
            return shadowSprite;

        Texture2D texture = new(16, 16, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = "SubmarineShadow" };
        Vector2 center = new(7.5f, 7.5f);
        for (int y = 0; y < 16; y++)
        for (int x = 0; x < 16; x++)
        {
            Vector2 point = new((x - center.x) / 7.5f, (y - center.y) / 4.5f);
            texture.SetPixel(x, y, point.sqrMagnitude <= 1f ? Color.white : Color.clear);
        }

        texture.Apply();
        shadowSprite = Sprite.Create(texture, new Rect(0f, 0f, 16f, 16f), new Vector2(0.5f, 0.5f), 16f, 0, SpriteMeshType.FullRect);
        shadowSprite.name = "SubmarineShadowSprite";
        return shadowSprite;
    }
}

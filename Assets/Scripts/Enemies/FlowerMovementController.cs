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

    [Header("Diagnostics")]
    [SerializeField] private bool logVisualDiagnostics;

    private Coroutine stateCycle;
    private bool isOpen = true;
    private VisualSnapshot openSnapshot;
    private VisualSnapshot closedSnapshot;
    private VisualSnapshot transitionSnapshot;

    private struct VisualSnapshot
    {
        public bool captured;
        public bool enabled;
        public Sprite sprite;
        public bool flipX;
        public bool flipY;
        public Vector3 localPosition;
        public Vector3 localScale;
        public Quaternion localRotation;
    }

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

    private void LateUpdate()
    {
        if (!logVisualDiagnostics)
            return;

        DetectExternalVisualChange(openSprite, ref openSnapshot, "Open");
        DetectExternalVisualChange(closedSprite, ref closedSnapshot, "Closed");
        DetectExternalVisualChange(transitionSprite, ref transitionSnapshot, "Transition");
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
        CaptureAllVisualStates("transition setup");

        int lastFrame = transitionSprite.animationSprite.Length - 1;

        for (int frameOffset = 0; frameOffset <= lastFrame && !isDead; frameOffset++)
        {
            int frame = reverse ? lastFrame - frameOffset : frameOffset;
            transitionSprite.CurrentFrame = frame;
            transitionSprite.RefreshFrame();
            CaptureVisualState(transitionSprite, "Transition", $"frame {frame} ({(reverse ? "reverse" : "forward")})");
            yield return WaitForAnimationSeconds(GetConfiguredFrameDuration(transitionSprite, frame));
        }

        transitionSprite.SetManualAnimationUpdate(false);
        DisableVisual(transitionSprite);
        CaptureAllVisualStates("transition finished");
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
        CaptureAllVisualStates("static " + GetVisualName(sprite));
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
                CaptureVisualState(sprite, GetVisualName(sprite), $"cycle {cycle + 1}/{cycles}, frame {frame}");
                yield return WaitForAnimationSeconds(GetConfiguredFrameDuration(sprite, frame));
            }
        }

        sprite.SetManualAnimationUpdate(false);
        sprite.idle = true;
        sprite.CurrentFrame = 0;
        sprite.RefreshFrame();
        CaptureVisualState(sprite, GetVisualName(sprite), "returned to idle");
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

    private void CaptureAllVisualStates(string reason)
    {
        CaptureVisualState(openSprite, "Open", reason);
        CaptureVisualState(closedSprite, "Closed", reason);
        CaptureVisualState(transitionSprite, "Transition", reason);
    }

    private void CaptureVisualState(AnimatedSpriteRenderer visual, string label, string reason)
    {
        if (!logVisualDiagnostics || visual == null)
            return;

        ref VisualSnapshot snapshot = ref GetSnapshot(visual);
        snapshot = ReadSnapshot(visual);
        Debug.Log($"[Flower Visual] {label}: {reason}; {DescribeVisual(visual, snapshot)}", this);
    }

    private void DetectExternalVisualChange(AnimatedSpriteRenderer visual, ref VisualSnapshot snapshot, string label)
    {
        if (visual == null)
            return;

        VisualSnapshot current = ReadSnapshot(visual);
        if (!snapshot.captured)
        {
            snapshot = current;
            return;
        }

        if (VisualStatesMatch(snapshot, current))
            return;

        Debug.LogWarning($"[Flower Visual] {label}: alteração externa detectada; antes [{DescribeSnapshot(snapshot)}], agora [{DescribeVisual(visual, current)}].", this);
        snapshot = current;
    }

    private ref VisualSnapshot GetSnapshot(AnimatedSpriteRenderer visual)
    {
        if (visual == openSprite)
            return ref openSnapshot;

        if (visual == closedSprite)
            return ref closedSnapshot;

        return ref transitionSnapshot;
    }

    private static VisualSnapshot ReadSnapshot(AnimatedSpriteRenderer visual)
    {
        SpriteRenderer renderer = visual.GetComponent<SpriteRenderer>();
        Transform transform = visual.transform;
        return new VisualSnapshot
        {
            captured = true,
            enabled = visual.enabled && (renderer == null || renderer.enabled),
            sprite = renderer != null ? renderer.sprite : null,
            flipX = renderer != null && renderer.flipX,
            flipY = renderer != null && renderer.flipY,
            localPosition = transform.localPosition,
            localScale = transform.localScale,
            localRotation = transform.localRotation
        };
    }

    private static bool VisualStatesMatch(VisualSnapshot a, VisualSnapshot b)
    {
        return a.enabled == b.enabled &&
               a.sprite == b.sprite &&
               a.flipX == b.flipX &&
               a.flipY == b.flipY &&
               a.localPosition == b.localPosition &&
               a.localScale == b.localScale &&
               a.localRotation == b.localRotation;
    }

    private static string GetVisualName(AnimatedSpriteRenderer visual)
    {
        return visual != null ? visual.gameObject.name : "Unknown";
    }

    private static string DescribeVisual(AnimatedSpriteRenderer visual, VisualSnapshot snapshot)
    {
        if (snapshot.sprite == null)
            return DescribeSnapshot(snapshot);

        Rect rect = snapshot.sprite.rect;
        Vector2 size = snapshot.sprite.bounds.size;
        return $"{DescribeSnapshot(snapshot)}, sprite={snapshot.sprite.name}, rect={rect.width}x{rect.height}@({rect.x},{rect.y}), bounds={size.x:F4}x{size.y:F4}, ppu={snapshot.sprite.pixelsPerUnit:F2}";
    }

    private static string DescribeSnapshot(VisualSnapshot snapshot)
    {
        return $"enabled={snapshot.enabled}, sprite={(snapshot.sprite != null ? snapshot.sprite.name : "null")}, flip=({snapshot.flipX},{snapshot.flipY}), localPos={snapshot.localPosition}, localScale={snapshot.localScale}, localRotation={snapshot.localRotation.eulerAngles}";
    }
}

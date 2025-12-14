using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class AnimatedSpriteRenderer : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;

    [Header("Sprites")]
    public Sprite idleSprite;
    public Sprite[] animationSprite;

    [Header("Timing")]
    public float animationTime = 0.25f;
    public bool useSequenceDuration = false;
    public float sequenceDuration = 0.5f;

    [Header("Loop / Idle")]
    public bool loop = true;
    public bool idle = true;

    [Header("Frame Offsets")]
    public Vector2[] frameOffsets;

    [Header("Ping Pong")]
    public bool pingPong = false;

    private int animationFrame;
    private int direction = 1;

    private Transform visualTransform;
    private Vector3 initialVisualLocalPosition;
    private bool canMoveVisualLocal;

    public int CurrentFrame
    {
        get => animationFrame;
        set
        {
            animationFrame = value;
            if (animationSprite != null && animationSprite.Length > 0)
            {
                if (animationFrame < 0) animationFrame = 0;
                if (animationFrame >= animationSprite.Length) animationFrame = animationSprite.Length - 1;
            }
        }
    }

    void EnsureSpriteRenderer()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Awake()
    {
        EnsureSpriteRenderer();

        visualTransform = spriteRenderer != null ? spriteRenderer.transform : transform;
        initialVisualLocalPosition = visualTransform.localPosition;

        canMoveVisualLocal = visualTransform != null && visualTransform != transform && visualTransform.parent == transform;
    }

    private void OnEnable()
    {
        EnsureSpriteRenderer();

        if (spriteRenderer != null)
            spriteRenderer.enabled = true;

        direction = 1;

        SetupTiming();
        CancelInvoke(nameof(NextFrame));
        InvokeRepeating(nameof(NextFrame), animationTime, animationTime);

        ApplyFrame();
    }

    private void OnDisable()
    {
        CancelInvoke(nameof(NextFrame));

        EnsureSpriteRenderer();
        if (spriteRenderer != null)
            spriteRenderer.enabled = false;
    }

    private void SetupTiming()
    {
        if (animationSprite == null || animationSprite.Length == 0)
            return;

        if (!useSequenceDuration)
            return;

        sequenceDuration = Mathf.Max(sequenceDuration, 0.0001f);

        int framesInCycle;

        if (pingPong && animationSprite.Length > 1)
            framesInCycle = animationSprite.Length * 2 - 2;
        else
            framesInCycle = animationSprite.Length;

        animationTime = sequenceDuration / Mathf.Max(1, framesInCycle);
        animationTime = Mathf.Max(animationTime, 0.0001f);
    }

    public void RefreshFrame()
    {
        ApplyFrame();
    }

    private void NextFrame()
    {
        if (idle)
        {
            ApplyFrame();
            return;
        }

        if (animationSprite == null || animationSprite.Length == 0)
            return;

        if (!pingPong)
        {
            animationFrame++;

            if (loop && animationFrame >= animationSprite.Length)
                animationFrame = 0;
            else if (!loop && animationFrame >= animationSprite.Length)
                animationFrame = animationSprite.Length - 1;
        }
        else
        {
            animationFrame += direction;

            if (animationFrame >= animationSprite.Length)
            {
                if (animationSprite.Length == 1)
                {
                    animationFrame = 0;
                }
                else
                {
                    animationFrame = animationSprite.Length - 2;
                    direction = -1;
                }
            }
            else if (animationFrame < 0)
            {
                if (animationSprite.Length == 1)
                {
                    animationFrame = 0;
                }
                else
                {
                    animationFrame = 1;
                    direction = 1;
                }
            }
        }

        ApplyFrame();
    }

    private void ApplyFrame()
    {
        EnsureSpriteRenderer();
        if (spriteRenderer == null)
            return;

        if (idle)
        {
            spriteRenderer.sprite = idleSprite;
        }
        else
        {
            if (animationSprite == null || animationSprite.Length == 0)
                return;

            if (animationFrame < 0 || animationFrame >= animationSprite.Length)
                return;

            spriteRenderer.sprite = animationSprite[animationFrame];
        }

        if (visualTransform == null)
            visualTransform = spriteRenderer.transform;

        if (!canMoveVisualLocal)
            return;

        if (!idle && frameOffsets != null && animationSprite != null &&
            frameOffsets.Length == animationSprite.Length &&
            animationFrame >= 0 && animationFrame < frameOffsets.Length)
        {
            Vector2 offset = frameOffsets[animationFrame];
            visualTransform.localPosition = initialVisualLocalPosition + (Vector3)offset;
        }
        else
        {
            visualTransform.localPosition = initialVisualLocalPosition;
        }
    }
}

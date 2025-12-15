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

    [Header("Safety")]
    public bool disableOffsetsIfThisObjectHasRigidbody2D = true;

    private int animationFrame;
    private int direction = 1;

    private Transform visualTransform;
    private Vector3 initialVisualLocalPosition;
    private bool canMoveVisualLocal;
    private bool frozen;

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
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
                spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
        }
    }

    private void Awake()
    {
        EnsureSpriteRenderer();

        visualTransform = spriteRenderer != null ? spriteRenderer.transform : transform;
        initialVisualLocalPosition = visualTransform != null ? visualTransform.localPosition : Vector3.zero;

        bool hasRbHere = disableOffsetsIfThisObjectHasRigidbody2D && GetComponent<Rigidbody2D>() != null;
        canMoveVisualLocal = visualTransform != null && !hasRbHere;
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

        ResetOffset();

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

    public void SetFrozen(bool value)
    {
        frozen = value;

        if (frozen)
        {
            ApplyFrame();
            ResetOffset();
        }
    }

    private void NextFrame()
    {
        if (frozen)
            return;

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

        if (!canMoveVisualLocal || visualTransform == null)
            return;

        if (!idle && frameOffsets != null && frameOffsets.Length > 0)
        {
            int idx = Mathf.Clamp(animationFrame, 0, frameOffsets.Length - 1);
            Vector2 offset = frameOffsets[idx];
            visualTransform.localPosition = initialVisualLocalPosition + (Vector3)offset;
        }
        else
        {
            ResetOffset();
        }
    }

    private void ResetOffset()
    {
        if (visualTransform != null)
            visualTransform.localPosition = initialVisualLocalPosition;
    }
}

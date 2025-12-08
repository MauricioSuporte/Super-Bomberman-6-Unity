using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class AnimatedSpriteRenderer : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;

    [Header("Sprites")]
    public Sprite idleSprite;
    public Sprite[] animationSprite;

    [Header("Timing")]
    [Tooltip("Time between frames (used when 'useSequenceDuration' is false).")]
    public float animationTime = 0.25f;

    [Tooltip("When true, 'sequenceDuration' defines the total duration of the animation cycle.")]
    public bool useSequenceDuration = false;

    [Tooltip("Total animation cycle duration (only used when 'useSequenceDuration' is true).")]
    public float sequenceDuration = 0.5f;

    [Header("Loop / Idle")]
    public bool loop = true;
    public bool idle = true;

    [Header("Frame Offsets")]
    [Tooltip("Optional local offset applied per frame. If empty or length differs from animationSprite, no offset is applied.")]
    public Vector2[] frameOffsets;

    [Header("Ping Pong")]
    [Tooltip("If true, animation plays 0..N..0 (ping-pong) instead of looping 0..N-1.")]
    public bool pingPong = false;

    private int animationFrame;
    private int direction = 1;
    private Vector3 initialLocalPosition;

    public int CurrentFrame
    {
        get => animationFrame;
        set
        {
            animationFrame = value;
            if (animationSprite != null && animationSprite.Length > 0)
            {
                if (animationFrame < 0)
                    animationFrame = 0;
                if (animationFrame >= animationSprite.Length)
                    animationFrame = animationSprite.Length - 1;
            }
        }
    }

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        initialLocalPosition = transform.localPosition;
    }

    private void OnEnable()
    {
        spriteRenderer.enabled = true;
        transform.localPosition = initialLocalPosition;
        ApplyFrame();
    }

    private void OnDisable()
    {
        spriteRenderer.enabled = false;
        transform.localPosition = initialLocalPosition;
    }

    private void Start()
    {
        if (animationSprite != null && animationSprite.Length > 0)
        {
            if (useSequenceDuration)
            {
                sequenceDuration = Mathf.Max(sequenceDuration, 0.0001f);

                int framesInCycle;

                if (pingPong && animationSprite.Length > 1)
                {
                    framesInCycle = animationSprite.Length * 2 - 2;
                }
                else
                {
                    framesInCycle = animationSprite.Length;
                }

                animationTime = sequenceDuration / framesInCycle;
            }
        }

        InvokeRepeating(nameof(NextFrame), animationTime, animationTime);
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
        }
        else
        {
            animationFrame += direction;

            if (animationFrame >= animationSprite.Length)
            {
                animationFrame = animationSprite.Length - 2;
                direction = -1;
            }
            else if (animationFrame < 0)
            {
                animationFrame = 1;
                direction = 1;
            }
        }

        ApplyFrame();
    }

    private void ApplyFrame()
    {
        if (idle)
        {
            spriteRenderer.sprite = idleSprite;
            return;
        }

        if (animationSprite == null || animationSprite.Length == 0)
            return;

        if (animationFrame < 0 || animationFrame >= animationSprite.Length)
            return;

        spriteRenderer.sprite = animationSprite[animationFrame];

        if (frameOffsets != null && frameOffsets.Length == animationSprite.Length)
        {
            Vector2 offset = frameOffsets[animationFrame];
            transform.localPosition = initialLocalPosition + (Vector3)offset;
        }
        else
        {
            transform.localPosition = initialLocalPosition;
        }
    }
}

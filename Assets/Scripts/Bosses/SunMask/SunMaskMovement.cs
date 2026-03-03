using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class SunMaskMovement : MonoBehaviour
{
    [Header("Movimento (sempre diagonal)")]
    [Min(0.01f)] public float speed = 2.5f;

    public bool randomizeStartDirection = true;
    public Vector2 startDirection = new(1f, 1f);

    [Header("Parar ao sofrer dano (opcional)")]
    public float defaultHitStopDuration = 0.25f;

    [Header("Limites do mapa")]
    public bool useBounds = true;
    public Vector2 minBounds = new(-7f, -7f);
    public Vector2 maxBounds = new(7f, 7f);

    [Header("Pixel Perfect Step")]
    [SerializeField] private int pixelsPerUnit = 16;

    private Rigidbody2D rb;
    private Vector2 currentDirection;
    private float hitStopTimer;
    private float pixelAccumulatorX;
    private float pixelAccumulatorY;

    private const float EPS = 0.0001f;
    private bool initialized;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        PickInitialDirection();
        initialized = true;
    }

    void OnEnable()
    {
        // Importante: NÃO resetar direção aqui, senão qualquer disable/enable muda o caminho.
        hitStopTimer = 0f;
        pixelAccumulatorX = 0f;
        pixelAccumulatorY = 0f;

        if (!initialized)
        {
            PickInitialDirection();
            initialized = true;
        }
    }

    void FixedUpdate()
    {
        if (GamePauseController.IsPaused)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        float dt = Time.fixedDeltaTime;

        if (hitStopTimer > 0f)
        {
            hitStopTimer -= dt;
            rb.linearVelocity = Vector2.zero;
            return;
        }

        currentDirection = ForceDiagonal(currentDirection);

        float worldStep = speed * dt;
        float pixelStep = worldStep * pixelsPerUnit;

        pixelAccumulatorX += currentDirection.x * pixelStep;
        pixelAccumulatorY += currentDirection.y * pixelStep;

        int movePixelsX = (int)pixelAccumulatorX;
        int movePixelsY = (int)pixelAccumulatorY;

        pixelAccumulatorX -= movePixelsX;
        pixelAccumulatorY -= movePixelsY;

        Vector2 moveWorld = new(
            movePixelsX / (float)pixelsPerUnit,
            movePixelsY / (float)pixelsPerUnit
        );

        Vector2 pos = rb.position;
        Vector2 desired = pos + moveWorld;

        if (useBounds)
        {
            bool hitX = false;
            bool hitY = false;

            if (desired.x < minBounds.x)
            {
                desired.x = minBounds.x + (minBounds.x - desired.x);
                hitX = true;
            }
            else if (desired.x > maxBounds.x)
            {
                desired.x = maxBounds.x - (desired.x - maxBounds.x);
                hitX = true;
            }

            if (desired.y < minBounds.y)
            {
                desired.y = minBounds.y + (minBounds.y - desired.y);
                hitY = true;
            }
            else if (desired.y > maxBounds.y)
            {
                desired.y = maxBounds.y - (desired.y - maxBounds.y);
                hitY = true;
            }

            if (hitX) currentDirection.x *= -1f;
            if (hitY) currentDirection.y *= -1f;

            desired.x = Mathf.Clamp(desired.x, minBounds.x, maxBounds.x);
            desired.y = Mathf.Clamp(desired.y, minBounds.y, maxBounds.y);

            currentDirection = ForceDiagonal(currentDirection);
        }

        rb.MovePosition(desired);
    }

    private void PickInitialDirection()
    {
        if (randomizeStartDirection)
        {
            int sx = Random.value < 0.5f ? -1 : 1;
            int sy = Random.value < 0.5f ? -1 : 1;
            currentDirection = new Vector2(sx, sy).normalized;
            return;
        }

        currentDirection = ForceDiagonal(startDirection);
    }

    private Vector2 ForceDiagonal(Vector2 dir)
    {
        if (dir.sqrMagnitude < EPS)
            dir = new(1f, 1f);

        float sx = dir.x >= 0f ? 1f : -1f;
        float sy = dir.y >= 0f ? 1f : -1f;

        Vector2 d = new(sx, sy);
        return d.normalized;
    }

    public void OnHit(float duration = -1f)
    {
        if (duration <= 0f)
            duration = defaultHitStopDuration;

        if (duration <= 0f)
            return;

        hitStopTimer = Mathf.Max(hitStopTimer, duration);
        rb.linearVelocity = Vector2.zero;
    }

    public Vector2 GetCurrentDirection()
        => currentDirection;

    public void SetCurrentDirection(Vector2 direction)
        => currentDirection = direction;
}
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class TrentMovementController : JunctionTurningEnemyMovementController
{
    private const float WakeUpDurationSeconds = 1f;
    private const float WakeUpFlipIntervalSeconds = 0.1f;
    private const float PostWakeDownIdleSeconds = 0.25f;

    [Header("Wake Up")]
    [SerializeField] private AnimatedSpriteRenderer wakeUpSprite;
    [SerializeField, Min(0.1f)] private float detectionDistanceTiles = 3f;
    [SerializeField] private LayerMask playerLayerMask;

    private Coroutine wakeUpRoutine;
    private bool hasWokenUp;
    private bool isWakingUp;
    private bool wakeUpFlipX;
    private float wakeUpFlipTimer;
    private PlayerWaterSubmersionEffect waterSubmersion;

    protected override void Awake()
    {
        base.Awake();

        if (playerLayerMask.value == 0)
            playerLayerMask = LayerMask.GetMask("Player");

        TryGetComponent(out waterSubmersion);
        SetWaterSubmersionSuppressed(true);
        ShowWakeUpIdle();

    }

    protected override void Start()
    {
        base.Start();

        if (!hasWokenUp && rb != null)
            targetTile = rb.position;
    }

    private void Update()
    {
        if (!isWakingUp || isDead)
            return;

        wakeUpFlipTimer += Time.deltaTime;
        while (wakeUpFlipTimer >= WakeUpFlipIntervalSeconds)
        {
            wakeUpFlipTimer -= WakeUpFlipIntervalSeconds;
            wakeUpFlipX = !wakeUpFlipX;
            ApplyWakeUpFlip();
        }
    }

    protected override void FixedUpdate()
    {
        if (isDead)
            return;

        if (!hasWokenUp)
        {
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                targetTile = rb.position;
            }

            if (!isWakingUp && wakeUpRoutine == null)
            {
                bool hasNearbyPlayer = TryFindNearbyPlayer(out Collider2D player);

                if (hasNearbyPlayer)
                {
                    wakeUpRoutine = StartCoroutine(WakeUp());
                }
            }

            return;
        }

        base.FixedUpdate();
    }

    protected override void UpdateSpriteDirection(Vector2 dir)
    {
        if (!hasWokenUp || isWakingUp)
            return;

        base.UpdateSpriteDirection(dir);
    }

    protected override void Die()
    {
        if (isDead)
            return;

        if (wakeUpRoutine != null)
        {
            StopCoroutine(wakeUpRoutine);
            wakeUpRoutine = null;
        }

        DisableWakeUpSprite();
        base.Die();
    }

    protected override void OnDestroy()
    {
        if (wakeUpRoutine != null)
            StopCoroutine(wakeUpRoutine);

        base.OnDestroy();
    }

    private bool TryFindNearbyPlayer(out Collider2D detectedPlayer)
    {
        detectedPlayer = null;

        if (rb == null || playerLayerMask.value == 0)
            return false;

        float radius = detectionDistanceTiles * tileSize;
        Collider2D[] hits = Physics2D.OverlapCircleAll(rb.position, radius, playerLayerMask);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null)
                continue;

            if (hit.GetComponentInParent<PlayerIdentity>() != null)
            {
                detectedPlayer = hit;
                return true;
            }
        }

        return false;
    }

    private IEnumerator WakeUp()
    {
        isWakingUp = true;
        wakeUpFlipX = false;
        wakeUpFlipTimer = 0f;
        SetWaterSubmersionSuppressed(false);

        DisableMovementSprites();

        if (wakeUpSprite != null)
        {
            wakeUpSprite.enabled = true;
            wakeUpSprite.idle = false;
            wakeUpSprite.loop = false;
            wakeUpSprite.useSequenceDuration = true;
            wakeUpSprite.sequenceDuration = WakeUpDurationSeconds;
            wakeUpSprite.RestartAnimation();
            ApplyWakeUpFlip();
            activeSprite = wakeUpSprite;
        }

        yield return new WaitForSeconds(WakeUpDurationSeconds);

        if (isDead)
            yield break;

        DisableWakeUpSprite();
        isWakingUp = false;
        ShowDownIdle();

        yield return new WaitForSeconds(PostWakeDownIdleSeconds);

        if (isDead)
            yield break;

        direction = Vector2.down;
        hasWokenUp = true;
        wakeUpRoutine = null;

        UpdateSpriteDirection(direction);
        DecideNextTile();
    }

    private void ShowWakeUpIdle()
    {
        DisableMovementSprites();

        if (wakeUpSprite == null)
            return;

        wakeUpSprite.enabled = true;
        wakeUpSprite.idle = true;
        wakeUpSprite.RefreshFrame();
        activeSprite = wakeUpSprite;
        wakeUpFlipX = false;
        ApplyWakeUpFlip();
    }

    private void DisableMovementSprites()
    {
        if (spriteUp != null) spriteUp.enabled = false;
        if (spriteDown != null) spriteDown.enabled = false;
        if (spriteLeft != null) spriteLeft.enabled = false;
    }

    private void ShowDownIdle()
    {
        DisableMovementSprites();

        if (spriteDown == null)
            return;

        spriteDown.enabled = true;
        spriteDown.idle = true;
        spriteDown.RefreshFrame();
        activeSprite = spriteDown;

        if (spriteDown.TryGetComponent(out SpriteRenderer renderer))
            renderer.flipX = false;
    }

    private void DisableWakeUpSprite()
    {
        if (wakeUpSprite != null)
        {
            wakeUpSprite.enabled = false;
            wakeUpSprite.SetManualAnimationUpdate(false);
        }
    }

    private void ApplyWakeUpFlip()
    {
        if (wakeUpSprite != null && wakeUpSprite.TryGetComponent(out SpriteRenderer renderer))
            renderer.flipX = wakeUpFlipX;
    }

    private void SetWaterSubmersionSuppressed(bool suppressed)
    {
        if (waterSubmersion != null)
            waterSubmersion.SetEffectSuppressed(suppressed);
    }

}

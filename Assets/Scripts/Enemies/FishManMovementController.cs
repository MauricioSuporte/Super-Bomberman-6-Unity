using System.Collections;
using UnityEngine;

public sealed class FishManMovementController : JunctionTurningEnemyMovementController
{
    [Header("Spear Attack")]
    [SerializeField] private GameObject spearPrefab;
    [SerializeField] private AnimatedSpriteRenderer throwUp;
    [SerializeField] private AnimatedSpriteRenderer throwDown;
    [SerializeField] private AnimatedSpriteRenderer throwLeft;
    [SerializeField, Min(0.01f)] private float throwAnimationSeconds = 0.25f;
    [SerializeField, Min(0.01f)] private float cooldownSeconds = 10f;
    [SerializeField, Min(0.1f)] private float scanIntervalSeconds = 0.15f;
    [SerializeField, Min(1)] private int visionTiles = 8;
    [SerializeField, Range(0.1f, 1f)] private float scanBoxSizePercent = 0.7f;
    [SerializeField] private LayerMask playerLayerMask;
    [SerializeField] private LayerMask obstacleLayerMask;

    private Coroutine throwRoutine;
    private float nextScanTime;
    private float nextShotTime;

    protected override void Awake()
    {
        base.Awake();
    }

    private void Update()
    {
        if (isDead || isInDamagedLoop || throwRoutine != null || spearPrefab == null ||
            Time.time < nextScanTime || Time.time < nextShotTime)
            return;

        nextScanTime = Time.time + scanIntervalSeconds;
        if (TryGetTargetDirection(out Vector2 targetDirection))
            throwRoutine = StartCoroutine(ThrowSpear(targetDirection));
    }

    protected override void FixedUpdate()
    {
        if (throwRoutine != null)
        {
            if (rb != null)
                rb.linearVelocity = Vector2.zero;
            return;
        }

        base.FixedUpdate();
    }

    protected override void Die()
    {
        DisableAllVisuals();
        StopThrow();
        base.Die();
    }

    protected override void OnDestroy()
    {
        StopThrow();
        base.OnDestroy();
    }

    private IEnumerator ThrowSpear(Vector2 targetDirection)
    {
        direction = targetDirection;
        isStuck = false;
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            SnapToGrid();
            targetTile = rb.position;
        }

        AnimatedSpriteRenderer attackSprite = GetThrowSprite(targetDirection);
        SetOnlyVisibleSprite(attackSprite);
        if (attackSprite != null)
        {
            if (attackSprite.TryGetComponent(out SpriteRenderer attackRenderer))
                attackRenderer.flipX = targetDirection == Vector2.right;

            attackSprite.RestartAnimation();
            attackSprite.loop = false;
            attackSprite.idle = false;
        }

        yield return new WaitForSeconds(throwAnimationSeconds);

        if (!isDead && spearPrefab != null)
        {
            Vector2 spawnPosition = rb != null
                ? rb.position + targetDirection * tileSize
                : (Vector2)transform.position + targetDirection * tileSize;
            GameObject spear = Instantiate(spearPrefab, spawnPosition, Quaternion.identity);
            if (spear.TryGetComponent(out FishManSpear projectile))
                projectile.Init(targetDirection, gameObject);
        }

        DisableVisual(attackSprite);
        nextShotTime = Time.time + cooldownSeconds;
        throwRoutine = null;

        if (!isDead)
        {
            UpdateSpriteDirection(direction);
            DecideNextTile();
        }
    }

    private bool TryGetTargetDirection(out Vector2 targetDirection)
    {
        Vector2[] directions = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
        Vector2 scanSize = Vector2.one * (tileSize * scanBoxSizePercent);
        Vector2 origin = rb != null ? rb.position : (Vector2)transform.position;

        foreach (Vector2 candidate in directions)
        {
            for (int step = 1; step <= visionTiles; step++)
            {
                Collider2D[] hits = Physics2D.OverlapBoxAll(origin + candidate * tileSize * step, scanSize, 0f);
                bool blocked = false;
                foreach (Collider2D hit in hits)
                {
                    if (hit == null || hit.transform.IsChildOf(transform))
                        continue;

                    int layerBit = 1 << hit.gameObject.layer;
                    if ((playerLayerMask.value & layerBit) != 0 && hit.GetComponentInParent<PlayerIdentity>() != null)
                    {
                        targetDirection = candidate;
                        return true;
                    }

                    if ((obstacleLayerMask.value & layerBit) != 0)
                        blocked = true;
                }

                if (blocked)
                    break;
            }
        }

        targetDirection = Vector2.zero;
        return false;
    }

    private AnimatedSpriteRenderer GetThrowSprite(Vector2 targetDirection)
    {
        if (targetDirection == Vector2.up)
            return throwUp;
        if (targetDirection == Vector2.down)
            return throwDown;
        return throwLeft;
    }

    private void SetOnlyVisibleSprite(AnimatedSpriteRenderer visibleSprite)
    {
        foreach (AnimatedSpriteRenderer renderer in GetComponentsInChildren<AnimatedSpriteRenderer>(true))
        {
            bool isVisible = renderer == visibleSprite;
            renderer.enabled = isVisible;

            if (renderer.TryGetComponent(out SpriteRenderer spriteRenderer))
                spriteRenderer.enabled = isVisible;
        }
    }

    private void StopThrow()
    {
        if (throwRoutine != null)
            StopCoroutine(throwRoutine);

        throwRoutine = null;
        DisableVisual(throwUp);
        DisableVisual(throwDown);
        DisableVisual(throwLeft);
    }

    private void DisableAllVisuals()
    {
        foreach (AnimatedSpriteRenderer renderer in GetComponentsInChildren<AnimatedSpriteRenderer>(true))
            DisableVisual(renderer);
    }

    private static void DisableVisual(AnimatedSpriteRenderer renderer)
    {
        if (renderer == null)
            return;

        renderer.enabled = false;
        if (renderer.TryGetComponent(out SpriteRenderer spriteRenderer))
            spriteRenderer.enabled = false;
    }
}

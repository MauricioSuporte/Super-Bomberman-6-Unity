using System.Collections;
using UnityEngine;

/// <summary>
/// SB3 jellyfish: periodically shocks the four neighbouring tiles. Its body
/// floats over a separate authored shadow while it moves.
/// </summary>
public sealed class JellyFishSB3MovementController : JunctionTurningEnemyMovementController
{
    [Header("Shock Ability")]
    [SerializeField, Min(0.01f)] private float movementDuration = 10f;
    [SerializeField] private AnimatedSpriteRenderer shockAnimation;
    [SerializeField, Min(0.01f)] private float shockFrameSeconds = 0.1f;
    [SerializeField, Min(0.01f)] private float shockDuration = 5f;
    [SerializeField] private LayerMask playerLayerMask;
    [SerializeField, Range(0.1f, 1f)] private float shockTileHitboxPercent = 0.75f;

    [Header("Movement Hover")]
    [SerializeField] private SpriteRenderer shadowRenderer;
    [SerializeField, Min(0f)] private float hoverBaseHeight = 0.25f;
    [SerializeField, Min(0f)] private float hoverAmplitude = 0.06f;
    [SerializeField, Min(0.01f)] private float hoverFrequency = 3f;

    private CharacterHealth jellyFishHealth;
    private Coroutine shockRoutine;
    private AnimatedSpriteRenderer movementSprite;
    private bool usingShock;
    private float movementElapsed;

    protected override void Start()
    {
        base.Start();
        jellyFishHealth = GetComponent<CharacterHealth>();
        movementSprite = activeSprite;
        SetVisualEnabled(shockAnimation, false);
    }

    protected override void FixedUpdate()
    {
        if (usingShock)
        {
            if (rb != null)
                rb.linearVelocity = Vector2.zero;

            return;
        }

        base.FixedUpdate();
    }

    private void LateUpdate()
    {
        if (isDead || isInDamagedLoop || usingShock)
        {
            ClearMovementHover();
            SetShadowVisible(false);
            return;
        }

        ApplyMovementHover();
        SetShadowVisible(true);
        movementElapsed += Time.deltaTime;
        if (movementElapsed >= movementDuration)
        {
            movementElapsed = 0f;
            shockRoutine = StartCoroutine(ShockRoutine());
        }
    }

    protected override void Die()
    {
        StopShock();
        ClearMovementHover();
        SetShadowVisible(false);
        base.Die();
    }

    protected override void OnDestroy()
    {
        StopShock();
        base.OnDestroy();
    }

    private IEnumerator ShockRoutine()
    {
        usingShock = true;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            SnapToGrid();
            targetTile = rb.position;
        }

        if (jellyFishHealth != null)
            jellyFishHealth.SetExternalInvulnerability(true);

        ClearMovementHover();
        SetShadowVisible(false);
        SetVisualEnabled(movementSprite, false);
        SetVisualEnabled(shockAnimation, true);
        if (shockAnimation != null)
        {
            activeSprite = shockAnimation;
            shockAnimation.loop = true;
            shockAnimation.idle = false;
            shockAnimation.RestartAnimation();
        }

        float elapsed = 0f;
        while (elapsed < shockDuration && !isDead)
        {
            DamagePlayersOneTileAway();
            yield return new WaitForSeconds(shockFrameSeconds);
            elapsed += shockFrameSeconds;
        }

        if (!isDead)
            FinishShock();
    }

    private void DamagePlayersOneTileAway()
    {
        if (rb == null || playerLayerMask.value == 0)
            return;

        float hitboxSize = tileSize * shockTileHitboxPercent;
        Vector2 hitbox = new(hitboxSize, hitboxSize);
        Vector2[] directions = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };

        foreach (Vector2 directionToPlayer in directions)
        {
            Collider2D[] hits = Physics2D.OverlapBoxAll(rb.position + directionToPlayer * tileSize, hitbox, 0f, playerLayerMask);
            foreach (Collider2D hit in hits)
            {
                CharacterHealth playerHealth = hit.GetComponentInParent<CharacterHealth>();
                if (playerHealth != null)
                    playerHealth.TakeDamage(1);
            }
        }
    }

    private void FinishShock()
    {
        if (jellyFishHealth != null)
            jellyFishHealth.SetExternalInvulnerability(false);

        SetVisualEnabled(shockAnimation, false);
        activeSprite = movementSprite != null ? movementSprite : spriteDown;
        SetVisualEnabled(activeSprite, true);
        if (activeSprite != null)
        {
            activeSprite.RestartAnimation();
            activeSprite.idle = false;
        }

        usingShock = false;
        shockRoutine = null;
        DecideNextTile();
    }

    private void StopShock()
    {
        if (shockRoutine != null)
            StopCoroutine(shockRoutine);

        shockRoutine = null;
        usingShock = false;

        if (jellyFishHealth != null)
            jellyFishHealth.SetExternalInvulnerability(false);

        ClearMovementHover();
        SetShadowVisible(false);
        SetVisualEnabled(shockAnimation, false);
    }

    private static void SetVisualEnabled(AnimatedSpriteRenderer animation, bool enabled)
    {
        if (animation == null)
            return;

        animation.enabled = enabled;
        if (animation.TryGetComponent(out SpriteRenderer renderer))
            renderer.enabled = enabled;
    }

    private void ApplyMovementHover()
    {
        if (movementSprite == null)
            return;

        float height = hoverBaseHeight + Mathf.Sin(Time.time * hoverFrequency) * hoverAmplitude;
        movementSprite.SetExternalBaseOffsetFromInitial(Vector3.up * height);
    }

    private void ClearMovementHover()
    {
        if (movementSprite != null)
            movementSprite.ClearExternalBase();
    }

    private void SetShadowVisible(bool visible)
    {
        if (shadowRenderer != null)
            shadowRenderer.enabled = visible;
    }
}

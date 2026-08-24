using System.Collections;
using UnityEngine;

public sealed class JellyFish94MovementController : JunctionTurningEnemyMovementController
{
    private static Sprite shadowSprite;

    [Header("Jellyfish Hover")]
    [SerializeField, Min(0f)] private float hoverBottomHeight = 0.32f;
    [SerializeField, Min(0f)] private float hoverTopHeight = 0.48f;
    [SerializeField, Min(0.01f)] private float fallDuration = 0.5f;
    [SerializeField, Min(0.01f)] private float impulseDuration = 0.25f;
    [SerializeField, Min(0.01f)] private float riseDuration = 0.5f;

    [Header("Shock Ability")]
    [SerializeField, Min(1)] private int movementLoopsBeforeShock = 2;
    [SerializeField] private AnimatedSpriteRenderer chargeAnimation;
    [SerializeField] private AnimatedSpriteRenderer shockAnimation;
    [SerializeField] private AnimatedSpriteRenderer releaseAnimation;
    [SerializeField, Min(0.01f)] private float chargeFrameSeconds = 0.05f;
    [SerializeField, Min(0.01f)] private float shockFrameSeconds = 0.1f;
    [SerializeField, Min(0.01f)] private float shockDuration = 1f;
    [SerializeField] private LayerMask playerLayerMask;
    [SerializeField, Range(0.1f, 1f)] private float shockTileHitboxPercent = 0.75f;

    [Header("Flight Shadow")]
    [SerializeField] private Color shadowColor = new(0f, 0f, 0f, 0.45f);
    [SerializeField] private Vector2 shadowScale = new(0.9f, 0.9f);
    [SerializeField] private Vector2 shadowOffset = Vector2.zero;

    private GameObject shadow;
    private CharacterHealth health;
    private Coroutine shockRoutine;
    private AnimatedSpriteRenderer movementSprite;
    private bool usingShock;
    private int completedMovementLoops;
    private int lastMovementFrame = -1;

    protected override void Start()
    {
        base.Start();
        health = GetComponent<CharacterHealth>();
        movementSprite = activeSprite;
        lastMovementFrame = activeSprite != null ? activeSprite.CurrentFrame : -1;
        DisableAttackAnimations();
        CreateShadow();
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
        if (isDead || isInDamagedLoop)
        {
            ClearHoverOffset();
            return;
        }

        UpdateShadowPosition();

        if (usingShock)
        {
            ApplyHoverOffset(hoverBottomHeight);
            return;
        }

        ApplyHoverOffset(GetMovementHoverHeight());
        CountMovementLoops();
    }

    protected override void Die()
    {
        StopShock();
        DestroyShadow();
        ClearHoverOffset();
        base.Die();
    }

    protected override void OnDestroy()
    {
        StopShock();
        DestroyShadow();
        base.OnDestroy();
    }

    private void CountMovementLoops()
    {
        if (activeSprite == null || activeSprite.animationSprite == null || activeSprite.animationSprite.Length < 3)
            return;

        int currentFrame = activeSprite.CurrentFrame;
        if (lastMovementFrame == activeSprite.animationSprite.Length - 1 && currentFrame == 0)
        {
            completedMovementLoops++;
            if (completedMovementLoops >= movementLoopsBeforeShock)
            {
                completedMovementLoops = 0;
                shockRoutine = StartCoroutine(ShockRoutine());
            }
        }

        lastMovementFrame = currentFrame;
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

        if (health != null)
            health.SetExternalInvulnerability(true);

        movementSprite = activeSprite != null ? activeSprite : movementSprite;
        if (movementSprite != null)
            movementSprite.enabled = false;

        yield return PlayAttack(chargeAnimation, chargeFrameSeconds, 0.5f, dealDamage: false);
        yield return PlayAttack(shockAnimation, shockFrameSeconds, shockDuration, dealDamage: true);
        yield return PlayAttack(releaseAnimation, shockFrameSeconds, 0.3f, dealDamage: false);

        if (isDead)
            yield break;

        FinishShock();
    }

    private IEnumerator PlayAttack(AnimatedSpriteRenderer attackAnimation, float frameSeconds, float totalSeconds, bool dealDamage)
    {
        if (attackAnimation == null)
            yield break;

        DisableAttackAnimations();
        activeSprite = attackAnimation;
        attackAnimation.enabled = true;
        attackAnimation.RestartAnimation();
        attackAnimation.idle = false;

        float elapsed = 0f;
        while (elapsed < totalSeconds && !isDead)
        {
            if (dealDamage)
                DamagePlayersOneTileAway();

            yield return new WaitForSeconds(frameSeconds);
            elapsed += frameSeconds;
        }

        attackAnimation.enabled = false;
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
        if (health != null)
            health.SetExternalInvulnerability(false);

        DisableAttackAnimations();
        activeSprite = movementSprite != null ? movementSprite : spriteDown;
        if (activeSprite != null)
        {
            activeSprite.enabled = true;
            activeSprite.RestartAnimation();
            activeSprite.idle = false;
        }

        usingShock = false;
        shockRoutine = null;
        lastMovementFrame = activeSprite != null ? activeSprite.CurrentFrame : -1;
        DecideNextTile();
    }

    private void StopShock()
    {
        if (shockRoutine != null)
            StopCoroutine(shockRoutine);

        shockRoutine = null;

        if (health != null)
            health.SetExternalInvulnerability(false);

        DisableAttackAnimations();

        usingShock = false;
    }

    private void DisableAttackAnimations()
    {
        if (chargeAnimation != null)
            chargeAnimation.enabled = false;

        if (shockAnimation != null)
            shockAnimation.enabled = false;

        if (releaseAnimation != null)
            releaseAnimation.enabled = false;
    }

    private float GetMovementHoverHeight()
    {
        float top = Mathf.Max(hoverTopHeight, hoverBottomHeight);
        float bottom = Mathf.Min(hoverTopHeight, hoverBottomHeight);
        int frame = activeSprite != null ? activeSprite.CurrentFrame : 0;
        float frameTime = activeSprite != null ? activeSprite.DebugFrameTimer : 0f;
        float impulseHeight = bottom + (top - bottom) * 0.18f;

        return frame switch
        {
            0 => Mathf.Lerp(top, bottom, Mathf.Clamp01(frameTime / fallDuration)),
            1 => Mathf.Lerp(bottom, impulseHeight, Mathf.Clamp01(frameTime / impulseDuration)),
            _ => Mathf.Lerp(impulseHeight, top, Mathf.Clamp01(frameTime / riseDuration))
        };
    }

    private void ApplyHoverOffset(float hoverHeight)
    {
        foreach (AnimatedSpriteRenderer animatedSprite in GetComponentsInChildren<AnimatedSpriteRenderer>(true))
        {
            if (animatedSprite != null && animatedSprite != activeSprite)
                animatedSprite.ClearExternalBase();
        }

        if (activeSprite != null)
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

    private void CreateShadow()
    {
        if (shadow != null)
            return;

        shadow = new GameObject("JellyFish94Shadow");
        shadow.transform.localScale = new Vector3(shadowScale.x, shadowScale.y, 1f);

        SpriteRenderer shadowRenderer = shadow.AddComponent<SpriteRenderer>();
        shadowRenderer.sprite = GetShadowSprite();
        shadowRenderer.color = shadowColor;

        AnimatedSpriteRenderer visual = spriteDown != null ? spriteDown : activeSprite;
        if (visual != null && visual.TryGetComponent(out SpriteRenderer visualRenderer))
        {
            shadowRenderer.sortingLayerID = visualRenderer.sortingLayerID;
            shadowRenderer.sortingOrder = visualRenderer.sortingOrder - 1;
        }

        UpdateShadowPosition();
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

    private static Sprite GetShadowSprite()
    {
        if (shadowSprite != null)
            return shadowSprite;

        Texture2D texture = new(16, 16, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            name = "JellyFish94Shadow"
        };

        Vector2 center = new(7.5f, 7.5f);
        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                Vector2 point = new((x - center.x) / 7.5f, (y - center.y) / 4.5f);
                texture.SetPixel(x, y, point.sqrMagnitude <= 1f ? Color.white : Color.clear);
            }
        }

        texture.Apply();
        shadowSprite = Sprite.Create(texture, new Rect(0f, 0f, 16f, 16f), new Vector2(0.5f, 0.5f), 16f);
        shadowSprite.name = "JellyFish94ShadowSprite";
        return shadowSprite;
    }
}

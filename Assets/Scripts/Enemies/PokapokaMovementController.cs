using UnityEngine;

public sealed class PokapokaMovementController : JunctionTurningEnemyMovementController
{
    private const float JumpDuration = 0.25f;
    private const int JumpsPerAbilityLoop = 3;

    [Header("Ability")]
    [SerializeField] private AnimatedSpriteRenderer abilitySprite;
    [SerializeField] private AnimatedSpriteRenderer jumpSprite;

    [Header("Player Detection")]
    [SerializeField, Min(0.1f)] private float visionDistance = 10f;
    [SerializeField] private LayerMask playerLayerMask;

    private StunReceiver stunReceiver;
    private bool abilityActive;
    private bool jumping;
    private int previousAbilityFrame;
    private int jumpsRemaining;
    private float jumpTimer;
    private GameObject jumpShadow;
    private Sprite jumpShadowSprite;

    protected override void Awake()
    {
        base.Awake();

        if (abilitySprite != null)
            abilitySprite.enabled = false;

        if (jumpSprite != null)
            jumpSprite.enabled = false;
    }

    protected override void Start()
    {
        base.Start();
        stunReceiver = GetComponent<StunReceiver>();

        if (playerLayerMask.value == 0)
            playerLayerMask = LayerMask.GetMask("Player");
    }

    protected override void FixedUpdate()
    {
        if (isDead)
        {
            StopAbility();
            return;
        }

        if (isInDamagedLoop || (stunReceiver != null && stunReceiver.IsStunned))
        {
            StopAbility();
            base.FixedUpdate();
            return;
        }

        if (jumping)
        {
            UpdateJump();
            return;
        }

        if (TrySeePlayer(out Vector2 seenDirection))
        {
            direction = seenDirection;
            StartAbility();

            if (rb != null)
                rb.linearVelocity = Vector2.zero;

            UpdateAbilityLoop();
            return;
        }

        StopAbility();
        base.FixedUpdate();
    }

    protected override void Die()
    {
        StopAbility();
        base.Die();
    }

    protected override void OnDestroy()
    {
        DestroyJumpShadow();

        if (jumpShadowSprite != null)
        {
            Destroy(jumpShadowSprite.texture);
            Destroy(jumpShadowSprite);
        }

        base.OnDestroy();
    }

    private void StartAbility()
    {
        if (abilitySprite == null || abilityActive)
            return;

        if (spriteUp != null) spriteUp.enabled = false;
        if (spriteDown != null) spriteDown.enabled = false;
        if (spriteLeft != null) spriteLeft.enabled = false;
        if (spriteDamaged != null) spriteDamaged.enabled = false;

        abilitySprite.enabled = true;
        abilitySprite.loop = true;
        abilitySprite.idle = false;
        abilitySprite.RestartAnimation();

        activeSprite = abilitySprite;
        abilityActive = true;
        previousAbilityFrame = abilitySprite.CurrentFrame;
    }

    private void UpdateAbilityLoop()
    {
        if (!abilityActive || abilitySprite == null || abilitySprite.animationSprite == null || abilitySprite.animationSprite.Length < 2)
            return;

        int currentFrame = abilitySprite.CurrentFrame;
        if (currentFrame < previousAbilityFrame)
        {
            jumpsRemaining = JumpsPerAbilityLoop;
            StartJump();
        }

        previousAbilityFrame = currentFrame;
    }

    private void StartJump()
    {
        if (jumpSprite == null)
            return;

        jumping = true;
        jumpTimer = 0f;

        abilitySprite.enabled = false;
        jumpSprite.enabled = true;
        jumpSprite.loop = true;
        jumpSprite.idle = false;
        jumpSprite.RestartAnimation();
        activeSprite = jumpSprite;

        if (rb != null)
        {
            CreateJumpShadow(rb.position);
            SpawnWaterEffect(rb.position, FrogWaterJumpEffect.EffectType.ExitRipple);
        }
    }

    private void UpdateJump()
    {
        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        jumpTimer += Time.fixedDeltaTime;
        float progress = Mathf.Clamp01(jumpTimer / JumpDuration);
        float height = Mathf.Sin(progress * Mathf.PI) * (tileSize * 0.5f);

        if (jumpSprite != null)
            jumpSprite.SetExternalBaseOffsetFromInitial(Vector3.up * height);

        if (progress < 1f)
            return;

        jumping = false;
        if (jumpSprite != null)
        {
            jumpSprite.ClearExternalBase();
            jumpSprite.enabled = false;
        }

        DestroyJumpShadow();

        if (rb != null)
            SpawnWaterEffect(rb.position, FrogWaterJumpEffect.EffectType.EntrySplash);

        jumpsRemaining--;
        if (jumpsRemaining > 0)
        {
            StartJump();
            return;
        }

        if (abilitySprite != null)
        {
            abilitySprite.enabled = true;
            abilitySprite.loop = true;
            abilitySprite.idle = false;
            abilitySprite.RestartAnimation();
            activeSprite = abilitySprite;
            previousAbilityFrame = abilitySprite.CurrentFrame;
        }
    }

    private void StopAbility()
    {
        if (!abilityActive && !jumping)
            return;

        abilityActive = false;
        jumping = false;
        jumpsRemaining = 0;

        if (abilitySprite != null)
            abilitySprite.enabled = false;

        if (jumpSprite != null)
        {
            jumpSprite.ClearExternalBase();
            jumpSprite.enabled = false;
        }

        DestroyJumpShadow();

        if (!isDead && !isInDamagedLoop)
        {
            activeSprite = null;
            UpdateSpriteDirection(direction);
        }
    }

    private bool TrySeePlayer(out Vector2 seenDirection)
    {
        seenDirection = Vector2.zero;
        if (rb == null || playerLayerMask.value == 0)
            return false;

        Vector2[] directions = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
        int collisionMask = obstacleMask | playerLayerMask;

        for (int index = 0; index < directions.Length; index++)
        {
            Vector2 scanDirection = directions[index];
            Vector2 origin = rb.position + scanDirection * (tileSize * 0.5f);
            RaycastHit2D hit = Physics2D.Raycast(origin, scanDirection, visionDistance, collisionMask);

            if (hit.collider == null)
                continue;

            if (((1 << hit.collider.gameObject.layer) & playerLayerMask) == 0)
                continue;

            seenDirection = scanDirection;
            return true;
        }

        return false;
    }

    private void CreateJumpShadow(Vector2 position)
    {
        DestroyJumpShadow();

        jumpShadow = new GameObject("PokapokaJumpShadow");
        jumpShadow.transform.position = new Vector3(position.x, position.y, 0f);
        jumpShadow.transform.localScale = new Vector3(0.75f, 0.32f, 1f);

        SpriteRenderer shadowRenderer = jumpShadow.AddComponent<SpriteRenderer>();
        shadowRenderer.sprite = GetJumpShadowSprite();
        shadowRenderer.color = new Color(0f, 0f, 0f, 0.45f);
        shadowRenderer.sortingOrder = 4;
    }

    private void DestroyJumpShadow()
    {
        if (jumpShadow != null)
            Destroy(jumpShadow);

        jumpShadow = null;
    }

    private Sprite GetJumpShadowSprite()
    {
        if (jumpShadowSprite != null)
            return jumpShadowSprite;

        Texture2D texture = new(16, 16, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            name = "PokapokaJumpShadow"
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
        jumpShadowSprite = Sprite.Create(texture, new Rect(0f, 0f, 16f, 16f), new Vector2(0.5f, 0.5f), 16f);
        jumpShadowSprite.name = "PokapokaJumpShadowSprite";
        return jumpShadowSprite;
    }

    private void SpawnWaterEffect(Vector2 position, FrogWaterJumpEffect.EffectType effectType)
    {
        GameObject effectObject = new($"PokapokaWater{effectType}");
        effectObject.transform.position = new Vector3(position.x, position.y, 0f);

        int sortingLayerId = 0;
        int sortingOrder = 4;
        if (jumpSprite != null && jumpSprite.TryGetComponent(out SpriteRenderer renderer))
        {
            sortingLayerId = renderer.sortingLayerID;
            sortingOrder = renderer.sortingOrder - 1;
        }

        FrogWaterJumpEffect effect = effectObject.AddComponent<FrogWaterJumpEffect>();
        effect.Initialize(effectType, sortingLayerId, sortingOrder);
    }
}

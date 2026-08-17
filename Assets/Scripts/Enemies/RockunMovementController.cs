using UnityEngine;

[RequireComponent(typeof(CharacterHealth))]
public sealed class RockunMovementController : JunctionTurningEnemyMovementController
{
    [Header("Rockun Movement Sprites")]
    public AnimatedSpriteRenderer moveDown;
    public AnimatedSpriteRenderer moveLeft;
    public AnimatedSpriteRenderer moveRight;
    public AnimatedSpriteRenderer moveUp;

    [Header("Rockun Ability Sprites")]
    public AnimatedSpriteRenderer abilityDown;
    public AnimatedSpriteRenderer abilityLeft;
    public AnimatedSpriteRenderer abilityUp;

    [Header("Rockun Ability")]
    [Min(0.1f)] public float walkSecondsBeforeAbility = 5f;
    [Min(0.1f)] public float abilityInvulnerabilitySeconds = 2f;

    CharacterHealth rockunHealth;
    bool isUsingAbility;
    bool isExitingAbility;
    float walkTimer;
    float abilityTimer;
    float abilityFrameTimer;
    int abilityFrame;
    AnimatedSpriteRenderer activeAbilitySprite;
    Vector2 abilityTile;

    protected override void Awake()
    {
        base.Awake();

        rockunHealth = GetComponent<CharacterHealth>();
        DisableAbilitySprites();
        ApplyMovementSprite(direction);
    }

    protected override void OnDestroy()
    {
        if (rockunHealth != null)
            rockunHealth.SetExternalInvulnerability(false);

        base.OnDestroy();
    }

    protected override void FixedUpdate()
    {
        if (isDead)
            return;

        if (isUsingAbility)
        {
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.position = abilityTile;
            }

            if (isExitingAbility)
                AdvanceAbilityExit();
            else
                AdvanceAbilityEntry();

            return;
        }

        base.FixedUpdate();

        if (isInDamagedLoop || IsStunned())
            return;

        walkTimer += Time.fixedDeltaTime;
        if (walkTimer >= walkSecondsBeforeAbility)
            StartAbility();
    }

    protected override void UpdateSpriteDirection(Vector2 dir)
    {
        if (isDead || isUsingAbility || isInDamagedLoop)
            return;

        ApplyMovementSprite(dir);
    }

    protected override void Die()
    {
        CancelAbilityForDeath();
        base.Die();
    }

    void StartAbility()
    {
        if (isUsingAbility || rb == null)
            return;

        isUsingAbility = true;
        walkTimer = 0f;

        SnapToGrid();
        abilityTile = rb.position;
        targetTile = abilityTile;
        rb.linearVelocity = Vector2.zero;

        rockunHealth?.SetExternalInvulnerability(true);

        DisableMovementSprites();
        AnimatedSpriteRenderer ability = ChooseAbilitySprite(direction);
        if (ability != null)
        {
            activeAbilitySprite = ability;
            ability.enabled = true;
            ability.idle = false;
            ability.loop = false;
            ability.SetManualAnimationUpdate(true);
            ability.CurrentFrame = 0;
            ability.RefreshFrame();

            if (ability.TryGetComponent(out SpriteRenderer spriteRenderer))
                spriteRenderer.flipX = ability == abilityLeft && direction == Vector2.right;

            activeSprite = ability;
        }

        abilityTimer = abilityInvulnerabilitySeconds;
        abilityFrameTimer = 0f;
        abilityFrame = 0;
        isExitingAbility = false;
    }

    void EndAbility()
    {
        if (!isUsingAbility)
            return;

        isUsingAbility = false;
        isExitingAbility = false;
        abilityTimer = 0f;
        abilityFrameTimer = 0f;
        rockunHealth?.SetExternalInvulnerability(false);

        if (activeAbilitySprite != null)
            activeAbilitySprite.SetManualAnimationUpdate(false);

        activeAbilitySprite = null;
        DisableAbilitySprites();
        ApplyMovementSprite(direction);
        DecideNextTile();
    }

    void CancelAbilityForDeath()
    {
        if (!isUsingAbility)
            return;

        isUsingAbility = false;
        isExitingAbility = false;
        abilityTimer = 0f;
        abilityFrameTimer = 0f;
        rockunHealth?.SetExternalInvulnerability(false);

        if (activeAbilitySprite != null)
            activeAbilitySprite.SetManualAnimationUpdate(false);

        activeAbilitySprite = null;
        DisableAbilitySprites();
    }

    void AdvanceAbilityEntry()
    {
        abilityTimer -= Time.fixedDeltaTime;

        if (activeAbilitySprite != null)
        {
            abilityFrameTimer += Time.fixedDeltaTime;
            float frameDuration = GetAbilityFrameDuration(activeAbilitySprite, abilityFrame);

            if (abilityFrameTimer >= frameDuration &&
                activeAbilitySprite.animationSprite != null &&
                abilityFrame < activeAbilitySprite.animationSprite.Length - 1)
            {
                abilityFrameTimer -= frameDuration;
                abilityFrame++;
                activeAbilitySprite.CurrentFrame = abilityFrame;
                activeAbilitySprite.RefreshFrame();
            }
        }

        if (abilityTimer <= 0f)
            BeginAbilityExit();
    }

    void BeginAbilityExit()
    {
        rockunHealth?.SetExternalInvulnerability(false);
        isExitingAbility = true;
        abilityFrameTimer = 0f;

        if (activeAbilitySprite == null ||
            activeAbilitySprite.animationSprite == null ||
            activeAbilitySprite.animationSprite.Length == 0)
        {
            EndAbility();
            return;
        }

        abilityFrame = activeAbilitySprite.animationSprite.Length - 1;
        activeAbilitySprite.CurrentFrame = abilityFrame;
        activeAbilitySprite.RefreshFrame();
    }

    void AdvanceAbilityExit()
    {
        if (activeAbilitySprite == null)
        {
            EndAbility();
            return;
        }

        abilityFrameTimer += Time.fixedDeltaTime;
        float frameDuration = GetAbilityFrameDuration(activeAbilitySprite, abilityFrame);
        if (abilityFrameTimer < frameDuration)
            return;

        abilityFrameTimer -= frameDuration;
        if (abilityFrame <= 0)
        {
            EndAbility();
            return;
        }

        abilityFrame--;
        activeAbilitySprite.CurrentFrame = abilityFrame;
        activeAbilitySprite.RefreshFrame();
    }

    static float GetAbilityFrameDuration(AnimatedSpriteRenderer sprite, int frame)
    {
        if (sprite.frameDurations != null &&
            sprite.animationSprite != null &&
            sprite.frameDurations.Length == sprite.animationSprite.Length &&
            frame >= 0 &&
            frame < sprite.frameDurations.Length)
        {
            return Mathf.Max(0.0001f, sprite.frameDurations[frame]);
        }

        return Mathf.Max(0.0001f, sprite.animationTime);
    }

    void ApplyMovementSprite(Vector2 dir)
    {
        DisableAbilitySprites();

        AnimatedSpriteRenderer chosen = ChooseMovementSprite(dir);
        if (moveDown != null) moveDown.enabled = chosen == moveDown;
        if (moveLeft != null) moveLeft.enabled = chosen == moveLeft;
        if (moveRight != null) moveRight.enabled = chosen == moveRight;
        if (moveUp != null) moveUp.enabled = chosen == moveUp;

        if (chosen == null)
            return;

        chosen.enabled = true;
        chosen.idle = false;
        chosen.loop = true;

        if (chosen.TryGetComponent(out SpriteRenderer spriteRenderer))
            spriteRenderer.flipX = chosen == moveLeft && dir == Vector2.right;

        activeSprite = chosen;
    }

    AnimatedSpriteRenderer ChooseMovementSprite(Vector2 dir)
    {
        if (dir == Vector2.left)
            return moveLeft != null ? moveLeft : moveDown;

        if (dir == Vector2.right)
            return moveRight != null ? moveRight : moveLeft != null ? moveLeft : moveDown;

        if (dir == Vector2.up)
            return moveUp != null ? moveUp : moveDown;

        return moveDown != null ? moveDown : moveLeft != null ? moveLeft : moveUp;
    }

    AnimatedSpriteRenderer ChooseAbilitySprite(Vector2 dir)
    {
        if (dir == Vector2.left)
            return abilityLeft != null ? abilityLeft : abilityDown;

        if (dir == Vector2.right)
            return abilityLeft != null ? abilityLeft : abilityDown;

        if (dir == Vector2.up)
            return abilityUp != null ? abilityUp : abilityDown;

        return abilityDown != null ? abilityDown : abilityLeft != null ? abilityLeft : abilityUp;
    }

    void DisableMovementSprites()
    {
        if (moveDown != null) moveDown.enabled = false;
        if (moveLeft != null) moveLeft.enabled = false;
        if (moveRight != null) moveRight.enabled = false;
        if (moveUp != null) moveUp.enabled = false;
    }

    void DisableAbilitySprites()
    {
        if (abilityDown != null) abilityDown.enabled = false;
        if (abilityLeft != null) abilityLeft.enabled = false;
        if (abilityUp != null) abilityUp.enabled = false;
    }

    bool IsStunned()
    {
        return TryGetComponent(out StunReceiver stun) && stun != null && stun.IsStunned;
    }

}

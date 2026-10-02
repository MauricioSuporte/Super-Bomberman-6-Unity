using UnityEngine;

public sealed class MechaBombSB3MovementController : JunctionTurningEnemyMovementController
{
    private const float AbilityDuration = 2f;
    private const float ExplosionDelay = 1.5f;

    [Header("Ability")]
    [Min(0.01f)] public float abilityMinCooldown = 8f;
    [Min(0.01f)] public float abilityMaxCooldown = 10f;
    [Min(1)] public int explosionRadius = 5;
    public AnimatedSpriteRenderer spriteAbility;

    private CharacterHealth abilityHealth;
    private AudioSource abilityAudioSource;
    private BombController cachedBombController;
    private float cooldown;
    private float abilityElapsed;
    private bool isAbilityActive;
    private bool hasExploded;

    protected override void Awake()
    {
        base.Awake();
        abilityHealth = GetComponent<CharacterHealth>();
        abilityAudioSource = GetComponent<AudioSource>();
        abilityAudioSource.playOnAwake = false;
        abilityAudioSource.loop = false;
        if (spriteAbility != null)
            spriteAbility.enabled = false;
    }

    protected override void Start()
    {
        base.Start();
        ScheduleAbility();
    }

    private void Update()
    {
        if (isDead || GamePauseController.IsPaused)
            return;

        if (isAbilityActive)
        {
            abilityElapsed += Time.deltaTime;
            if (!hasExploded && abilityElapsed >= ExplosionDelay)
            {
                hasExploded = true;
                ResolveBombController();
                if (cachedBombController != null)
                    cachedBombController.SpawnExplosionCrossForEffectWithTileEffects(
                        rb.position, explosionRadius, true, abilityAudioSource);
            }

            if (abilityElapsed >= AbilityDuration)
            {
                EndAbility();
                UpdateSpriteDirection(direction);
                DecideNextTile();
                ScheduleAbility();
            }
            return;
        }

        cooldown -= Time.deltaTime;
        if (cooldown > 0f || isInDamagedLoop || abilityHealth.IsInvulnerable ||
            (TryGetComponent<StunReceiver>(out var stun) && stun.IsStunned))
            return;

        ResolveBombController();
        if (spriteAbility == null || cachedBombController == null)
        {
            ScheduleAbility();
            return;
        }

        isAbilityActive = true;
        abilityElapsed = 0f;
        hasExploded = false;
        rb.linearVelocity = Vector2.zero;
        SnapToGrid();
        abilityHealth.SetExternalInvulnerability(true);
        if (spriteUp != null) spriteUp.enabled = false;
        if (spriteDown != null) spriteDown.enabled = false;
        if (spriteLeft != null) spriteLeft.enabled = false;
        if (spriteRight != null) spriteRight.enabled = false;
        if (spriteDamaged != null) spriteDamaged.enabled = false;
        if (spriteDeath != null) spriteDeath.enabled = false;
        activeSprite = spriteAbility;
        spriteAbility.loop = true;
        spriteAbility.idle = false;
        spriteAbility.enabled = true;
    }

    protected override void FixedUpdate()
    {
        if (isAbilityActive)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }
        base.FixedUpdate();
    }

    protected override void UpdateSpriteDirection(Vector2 dir)
    {
        if (!isAbilityActive)
            base.UpdateSpriteDirection(dir);
    }

    protected override void OnTriggerEnter2D(Collider2D other)
    {
        if (!isAbilityActive)
            base.OnTriggerEnter2D(other);
    }

    protected override void Die()
    {
        EndAbility();
        base.Die();
    }

    private void OnDisable()
    {
        bool wasActive = isAbilityActive;
        EndAbility();
        if (wasActive && !isDead)
        {
            UpdateSpriteDirection(direction);
            DecideNextTile();
        }
        ScheduleAbility();
    }

    private void EndAbility()
    {
        if (!isAbilityActive)
            return;
        isAbilityActive = false;
        abilityHealth.SetExternalInvulnerability(false);
        if (spriteAbility != null)
            spriteAbility.enabled = false;
    }

    private void ScheduleAbility()
    {
        float min = Mathf.Max(0.01f, abilityMinCooldown);
        cooldown = Random.Range(min, Mathf.Max(min, abilityMaxCooldown));
    }

    private void ResolveBombController()
    {
        if (cachedBombController != null)
            return;
        if (TryGetComponent(out cachedBombController))
            return;
        foreach (var controller in FindObjectsByType<BombController>())
        {
            if (!controller.CompareTag("Player"))
                continue;
            cachedBombController = controller;
            return;
        }
    }
}

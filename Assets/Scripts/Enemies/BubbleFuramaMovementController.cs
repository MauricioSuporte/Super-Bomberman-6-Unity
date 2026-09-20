using UnityEngine;

public sealed class BubbleFuramaMovementController : JunctionTurningEnemyMovementController
{
    enum AttackPhase { Walking, Preparing, Throwing, Recovering, ReversingPreparation }

    [Header("Bubble Furama Attack")]
    [SerializeField, Min(0.01f)] float attackMinCooldown = 7f;
    [SerializeField, Min(0.01f)] float attackMaxCooldown = 10f;
    [SerializeField, Min(0.01f)] float prepareDuration = 1f;
    [SerializeField, Min(0.01f)] float sweepDuration = 3f;
    [SerializeField, Range(2, 180)] int flamePairsPerSweep = 36;
    [SerializeField, Min(1f)] float flameLengthTiles = 3f;

    [Header("Child Animations (automatically resolved by name)")]
    [SerializeField] AnimatedSpriteRenderer movimentation;
    [SerializeField] AnimatedSpriteRenderer preparing;
    [SerializeField] AnimatedSpriteRenderer throwingFlames;
    [SerializeField] AnimatedSpriteRenderer flame;

    AttackPhase phase;
    float phaseTimer;
    int nextPair;
    int sweepPairCount;
    float pairInterval;
    bool ready;
    StunReceiver stunReceiver;

    protected override void Awake()
    {
        movimentation = Resolve(movimentation, "Movimentation");
        preparing = Resolve(preparing, "Preparing");
        throwingFlames = Resolve(throwingFlames, "ThrowingFlames");
        flame = Resolve(flame, "Flame");
        spriteDeath = Resolve(spriteDeath, "Death");
        spriteUp = spriteDown = spriteLeft = spriteRight = movimentation;
        waitForFullDeathAnimation = true;
        base.Awake();
        stunReceiver = GetComponent<StunReceiver>();
        SetVisible(preparing, false);
        SetVisible(throwingFlames, false);
        SetVisible(flame, false);
        SetVisible(spriteDeath, false);
        ready = movimentation != null && preparing != null && throwingFlames != null && flame != null;
        if (GetComponents<EnemyMovementController>().Length != 1)
        {
            ready = false;
            enabled = false;
            return;
        }
        if (!ready)
        {
            enabled = false;
        }
    }

    protected override void Start()
    {
        if (!ready)
            return;

        base.Start();
        ResetCooldown();
    }

    void Update()
    {
        if (!ready || isDead || GamePauseController.IsPaused || Time.deltaTime <= 0f ||
            isInDamagedLoop || (stunReceiver != null && stunReceiver.IsStunned))
            return;

        phaseTimer -= Time.deltaTime;
        if (phase == AttackPhase.ReversingPreparation)
            RefreshReversePreparationFrame();
        if (phaseTimer > 0f)
            return;

        switch (phase)
        {
            case AttackPhase.Walking:
                phase = AttackPhase.Preparing;
                phaseTimer = Mathf.Max(0.01f, prepareDuration);
                SnapToGrid();
                rb.linearVelocity = Vector2.zero;
                ShowAttack(preparing);
                break;
            case AttackPhase.Preparing:
                phase = AttackPhase.Throwing;
                sweepPairCount = Mathf.Clamp(flamePairsPerSweep, 2, 180);
                pairInterval = Mathf.Max(0.01f, sweepDuration) / sweepPairCount;
                nextPair = 0;
                ShowAttack(throwingFlames);
                LaunchNextPair();
                break;
            case AttackPhase.Throwing:
                if (nextPair < sweepPairCount)
                    LaunchNextPair();
                else
                {
                    // At 180 degrees the opposite rays coincide with the initial pair.
                    // Finish the turn without emitting those directions a second time.
                    phase = AttackPhase.Recovering;
                    phaseTimer = BubbleFuramaFlame.Lifetime;
                }
                break;
            case AttackPhase.Recovering:
                phase = AttackPhase.ReversingPreparation;
                ShowAttack(preparing);
                preparing.SetManualAnimationUpdate(true);
                phaseTimer = preparing.sequenceDuration;
                RefreshReversePreparationFrame();
                break;
            case AttackPhase.ReversingPreparation:
                ResumeWalking();
                break;
        }
    }

    protected override void FixedUpdate()
    {
        if (phase != AttackPhase.Walking || GamePauseController.IsPaused)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }
        base.FixedUpdate();
    }

    protected override void UpdateSpriteDirection(Vector2 dir)
    {
        if (phase == AttackPhase.Walking)
            base.UpdateSpriteDirection(dir);
    }

    protected override void OnTriggerEnter2D(Collider2D other)
    {
        // Bomb/enemy contact must not move or turn the enemy during its attack.
        if (phase == AttackPhase.Walking || other.gameObject.layer == LayerMask.NameToLayer("Explosion"))
            base.OnTriggerEnter2D(other);
    }

    protected override void OnHitInvulnerabilityStarted(float seconds)
    {
        // Health still applies damage and invulnerability. During an attack,
        // preserve its timer, flame index and animation instead of entering
        // the base damaged loop. Lethal damage interrupts through Die().
        if (phase == AttackPhase.Walking)
            base.OnHitInvulnerabilityStarted(seconds);
    }

    protected override void Die()
    {
        if (isDead)
            return;
        if (preparing != null)
            preparing.SetManualAnimationUpdate(false);
        SetVisible(preparing, false);
        SetVisible(throwingFlames, false);
        SetVisible(flame, false);
        base.Die();
    }

    void OnDisable()
    {
        if (preparing != null)
            preparing.SetManualAnimationUpdate(false);
        SetVisible(preparing, false);
        SetVisible(throwingFlames, false);
        SetVisible(flame, false);
        if (ready && !isDead)
        {
            ResumeWalking();
        }
    }

    void LaunchNextPair()
    {
        float degrees = nextPair * (180f / sweepPairCount);
        float radians = degrees * Mathf.Deg2Rad;
        Vector2 ray = new Vector2(Mathf.Cos(radians), -Mathf.Sin(radians));
        LaunchFlame(ray);
        LaunchFlame(-ray);
        nextPair++;
        // Emit at most one pair per frame; slow frames cannot duplicate an angle.
        phaseTimer = pairInterval;
    }

    void LaunchFlame(Vector2 ray)
    {
        AnimatedSpriteRenderer copy = Instantiate(flame, flame.transform.position, Quaternion.identity);
        copy.name = "BubbleFuramaFlame";
        copy.gameObject.SetActive(true);
        copy.gameObject.AddComponent<BubbleFuramaFlame>().Launch(
            copy, ray, Mathf.Max(0.01f, tileSize), Mathf.Max(1f, flameLengthTiles),
            throwingFlames.GetComponent<SpriteRenderer>(), GetComponentsInChildren<Collider2D>(true));
    }

    void ShowAttack(AnimatedSpriteRenderer animation)
    {
        preparing.SetManualAnimationUpdate(false);
        SetVisible(movimentation, false);
        SetVisible(preparing, false);
        SetVisible(throwingFlames, false);
        animation.loop = false;
        animation.idle = false;
        animation.useSequenceDuration = true;
        animation.sequenceDuration = animation == preparing ? Mathf.Max(0.01f, prepareDuration) : Mathf.Max(0.01f, sweepDuration);
        activeSprite = animation;
        SetVisible(animation, true);
        animation.RestartAnimation();
    }

    void ResumeWalking()
    {
        preparing.SetManualAnimationUpdate(false);
        SetVisible(preparing, false);
        SetVisible(throwingFlames, false);
        ResetCooldown();
        activeSprite = movimentation;
        movimentation.loop = true;
        UpdateSpriteDirection(direction);
        DecideNextTile();
    }

    void RefreshReversePreparationFrame()
    {
        int frameCount = preparing.animationSprite != null ? preparing.animationSprite.Length : 0;
        if (frameCount == 0)
            return;

        // Select frames backwards without modifying the authored sprite array.
        // The attack timer also freezes this animation during pause or stun.
        float progress = 1f - Mathf.Clamp01(phaseTimer / preparing.sequenceDuration);
        preparing.CurrentFrame = Mathf.Max(0, frameCount - 1 - Mathf.FloorToInt(progress * frameCount));
        preparing.RefreshFrame();
    }

    void ResetCooldown()
    {
        phase = AttackPhase.Walking;
        float minimum = Mathf.Max(0.01f, attackMinCooldown);
        phaseTimer = Random.Range(minimum, Mathf.Max(minimum, attackMaxCooldown));
    }

    AnimatedSpriteRenderer Resolve(AnimatedSpriteRenderer assigned, string childName)
    {
        if (assigned != null)
            return assigned;
        Transform child = transform.Find(childName);
        return child != null ? child.GetComponent<AnimatedSpriteRenderer>() : null;
    }

    static void SetVisible(AnimatedSpriteRenderer animation, bool visible)
    {
        if (animation == null)
            return;
        animation.enabled = visible;
        if (animation.TryGetComponent(out SpriteRenderer renderer))
            renderer.enabled = visible;
    }

}

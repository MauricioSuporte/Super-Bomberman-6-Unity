using UnityEngine;

public sealed class BirdRobotRedMovementController : JunctionTurningPersecutingEnemyMovementController
{
    [Header("Rocket Attack")]
    [SerializeField] private Vector2 attackIntervalSeconds = new(6f, 12f);
    [SerializeField] private AnimatedSpriteRenderer attackUp;
    [SerializeField] private AnimatedSpriteRenderer attackDown;
    [SerializeField] private AnimatedSpriteRenderer attackLeft;
    [SerializeField] private AnimatedSpriteRenderer rocketDown;
    [SerializeField] private AnimatedSpriteRenderer rocketLeft;
    [SerializeField] private AnimatedSpriteRenderer explosion;
    [SerializeField] private AudioClip attackSfx;

    private AnimatedSpriteRenderer attackSprite;
    private Vector2 attackDirection;
    private float nextAttackTime;
    private float attackElapsed;
    private bool attacking;
    private bool fired;

    protected override void Awake()
    {
        base.Awake();
        HideAttackSprites();
        rocketDown.enabled = false;
        rocketLeft.enabled = false;
        explosion.enabled = false;
        ScheduleAttack();
    }

    protected override void FixedUpdate()
    {
        if (isDead || GamePauseController.IsPaused)
            return;
        if (isInDamagedLoop || (TryGetComponent<StunReceiver>(out var stun) && stun.IsStunned))
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }
        if (attacking)
        {
            rb.linearVelocity = Vector2.zero;
            attackElapsed += Time.fixedDeltaTime;
            attackSprite.CurrentFrame = Mathf.Min(9, Mathf.FloorToInt(attackElapsed / 0.1f));
            attackSprite.RefreshFrame();
            if (!fired && attackElapsed >= 0.6f)
            {
                fired = true;
                LaunchRocket();
            }
            if (attackElapsed >= 1f)
            {
                attacking = false;
                HideAttackSprites();
                UpdateSpriteDirection(direction);
                ScheduleAttack();
            }
            return;
        }
        Vector2 gridPosition = new(Mathf.Round(rb.position.x / tileSize) * tileSize, Mathf.Round(rb.position.y / tileSize) * tileSize);
        if (Time.time >= nextAttackTime && (Vector2.Distance(rb.position, gridPosition) < 0.01f || isStuck) && TryAimAtNearestPlayer(out attackDirection))
        {
            BeginAttack();
            return;
        }
        base.FixedUpdate();
    }

    private void ScheduleAttack()
    {
        float minimum = Mathf.Max(0.1f, attackIntervalSeconds.x);
        nextAttackTime = Time.time + Random.Range(minimum, Mathf.Max(minimum, attackIntervalSeconds.y));
    }

    private bool TryAimAtNearestPlayer(out Vector2 aim)
    {
        aim = Vector2.zero;
        float nearest = float.PositiveInfinity;
        foreach (PlayerIdentity player in PlayerIdentity.ActivePlayers)
        {
            if (player == null || !player.gameObject.activeInHierarchy)
                continue;
            CharacterHealth health = player.GetComponent<CharacterHealth>();
            if (health == null || health.life <= 0)
                continue;
            Vector2 delta = (Vector2)player.transform.position - rb.position;
            if (delta.sqrMagnitude >= nearest)
                continue;
            nearest = delta.sqrMagnitude;
            aim = Mathf.Abs(delta.x) > Mathf.Abs(delta.y)
                ? (delta.x >= 0f ? Vector2.right : Vector2.left)
                : (delta.y >= 0f ? Vector2.up : Vector2.down);
        }
        return aim != Vector2.zero;
    }

    private void BeginAttack()
    {
        if (attackSfx != null && TryGetComponent<AudioSource>(out var source))
            GameAudioSettings.PlaySfx(source, attackSfx);
        attacking = true;
        fired = false;
        attackElapsed = 0f;
        rb.linearVelocity = Vector2.zero;
        if (spriteUp != null) spriteUp.enabled = false;
        if (spriteDown != null) spriteDown.enabled = false;
        if (spriteLeft != null) spriteLeft.enabled = false;
        if (spriteRight != null) spriteRight.enabled = false;
        attackSprite = attackDirection.x != 0f ? attackLeft
            : (attackDirection == Vector2.up && attackUp != null ? attackUp : attackDown);
        attackSprite.SetManualAnimationUpdate(true);
        attackSprite.loop = false;
        attackSprite.idle = false;
        attackSprite.enabled = true;
        attackSprite.RestartAnimation();
        SpriteRenderer renderer = attackSprite.GetComponent<SpriteRenderer>();
        renderer.flipX = attackDirection == Vector2.right;
        renderer.flipY = attackDirection == Vector2.up && attackSprite == attackDown;
    }

    private void LaunchRocket()
    {
        AnimatedSpriteRenderer template = attackDirection.x != 0f ? rocketLeft : rocketDown;
        Vector2 spawnPosition = rb.position + attackDirection * (tileSize * 0.6f);
        GameObject rocket = Instantiate(template.gameObject, spawnPosition, Quaternion.identity);
        rocket.name = "Rocket";
        rocket.layer = LayerMask.NameToLayer("Default");
        rocket.SetActive(true);
        AnimatedSpriteRenderer animation = rocket.GetComponent<AnimatedSpriteRenderer>();
        animation.enabled = true;
        animation.idle = false;
        animation.RestartAnimation();
        SpriteRenderer renderer = rocket.GetComponent<SpriteRenderer>();
        renderer.flipX = attackDirection == Vector2.right;
        renderer.flipY = attackDirection == Vector2.up;
        rocket.AddComponent<BirdRobotRocket>().Init(attackDirection, gameObject, explosion, tileSize);
    }

    private void HideAttackSprites()
    {
        if (attackUp != null) attackUp.enabled = false;
        if (attackDown != null) attackDown.enabled = false;
        if (attackLeft != null) attackLeft.enabled = false;
    }

    protected override void Die()
    {
        attacking = false;
        HideAttackSprites();
        base.Die();
    }

    private void OnDisable()
    {
        attacking = false;
        HideAttackSprites();
        if (!isDead && rb != null)
        {
            UpdateSpriteDirection(direction);
            ScheduleAttack();
        }
    }
}
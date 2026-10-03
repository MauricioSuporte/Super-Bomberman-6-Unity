using System.Collections.Generic;
using UnityEngine;

public sealed class GurunMovementController : EnemyMovementController
{
    enum Phase { Ground, Takeoff, Flying, Landing, PreparingAttack, Attacking, Recovering }

    [Header("Gurun animations")]
    public AnimatedSpriteRenderer movementGround;
    public AnimatedSpriteRenderer movementFly;
    public AnimatedSpriteRenderer preparingFly;
    public AnimatedSpriteRenderer preparingAttack;
    public AnimatedSpriteRenderer attacking;
    public AnimatedSpriteRenderer rocketTop;
    public AnimatedSpriteRenderer rocketDown;
    public AnimatedSpriteRenderer rocketLeft;
    public AnimatedSpriteRenderer explosion;
    public AudioClip rocketSfx;

    [Header("Gurun timing")]
    [Min(0.1f)] public float groundDuration = 6f;
    [Min(0.1f)] public float flightDuration = 5f;
    [Min(0.1f)] public float attackCooldown = 2f;

    Phase phase;
    float phaseElapsed;
    float groundElapsed;
    float cooldownRemaining;
    float frameElapsed;
    bool reversing;
    Vector2 attackDirection;
    readonly Queue<Vector2> frontier = new();
    readonly Dictionary<Vector2, Vector2> firstSteps = new();

    protected override void Awake()
    {
        ResolveVisuals();
        spriteUp = spriteDown = spriteLeft = movementGround;
        base.Awake();
        HideVisuals();
    }

    void Reset() => ResolveVisuals();

    void ResolveVisuals()
    {
        movementGround = Resolve(movementGround, "MovimentationGround");
        movementFly = Resolve(movementFly, "MovimentationFly");
        preparingFly = Resolve(preparingFly, "PreparingFly");
        preparingAttack = Resolve(preparingAttack, "PreparingAttack");
        attacking = Resolve(attacking, "Attacking");
        rocketTop = Resolve(rocketTop, "RocketTop");
        rocketDown = Resolve(rocketDown, "RocketDown");
        rocketLeft = Resolve(rocketLeft, "RocketLeft");
        explosion = Resolve(explosion, "Explosion");
        spriteDeath = Resolve(spriteDeath, "Death");
        spriteUp = spriteDown = spriteLeft = movementGround;
        waitForFullDeathAnimation = true;
    }

    AnimatedSpriteRenderer Resolve(AnimatedSpriteRenderer assigned, string childName)
    {
        if (assigned != null) return assigned;
        Transform child = transform.Find(childName);
        return child != null ? child.GetComponent<AnimatedSpriteRenderer>() : null;
    }

    protected override void Start()
    {
        SnapToGrid();
        direction = Vector2.down;
        Enter(Phase.Ground, movementGround);
        DecideNextTile();
    }

    protected override void FixedUpdate()
    {
        if (isDead || GamePauseController.IsPaused) return;
        if (isInDamagedLoop || (TryGetComponent<StunReceiver>(out var stun) && stun.IsStunned))
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }
        cooldownRemaining = Mathf.Max(0f, cooldownRemaining - Time.fixedDeltaTime);
        phaseElapsed += Time.fixedDeltaTime;
        if (phase == Phase.Ground) groundElapsed += Time.fixedDeltaTime;
        if (phase != Phase.Ground && phase != Phase.Flying)
        {
            rb.linearVelocity = Vector2.zero;
            if (!AdvanceSequence()) return;
            switch (phase)
            {
                case Phase.Takeoff:
                    Enter(Phase.Flying, movementFly);
                    DecideNextTile();
                    break;
                case Phase.Landing:
                    BeginAttack();
                    break;
                case Phase.PreparingAttack:
                    Enter(Phase.Attacking, attacking);
                    break;
                case Phase.Attacking:
                    LaunchRocket();
                    Enter(Phase.Recovering, preparingAttack, true);
                    break;
                case Phase.Recovering:
                    cooldownRemaining = attackCooldown;
                    Enter(Phase.Ground, movementGround);
                    DecideNextTile();
                    break;
            }
            return;
        }

        Vector2 center = Grid(rb.position);
        if (Vector2.Distance(rb.position, center) < 0.01f)
        {
            SnapToGrid();
            if (phase == Phase.Ground)
            {
                if (groundElapsed >= groundDuration)
                {
                    groundElapsed = 0f;
                    Enter(Phase.Takeoff, preparingFly);
                    return;
                }
                if (cooldownRemaining <= 0f && TryPlayer(out var playerPosition))
                {
                    Vector2 delta = playerPosition - rb.position;
                    if (Mathf.Abs(delta.x) <= tileSize * 0.15f || Mathf.Abs(delta.y) <= tileSize * 0.15f)
                    {
                        BeginAttack();
                        return;
                    }
                }
            }
            else if (phaseElapsed >= flightDuration && !Blocked(center, false))
            {
                Enter(Phase.Landing, preparingFly, true);
                return;
            }
            DecideNextTile();
        }
        // A bomb or another enemy may have moved into the previously free tile.
        if (IsTileBlocked(targetTile))
        {
            rb.MovePosition(Vector2.MoveTowards(rb.position, center, speed * Time.fixedDeltaTime));
            if (Vector2.Distance(rb.position, center) < 0.01f) DecideNextTile();
            return;
        }
        MoveTowardsTile();
    }

    Vector2 Grid(Vector2 position) => new(Mathf.Round(position.x / tileSize) * tileSize, Mathf.Round(position.y / tileSize) * tileSize);

    bool TryPlayer(out Vector2 position)
    {
        position = rb.position;
        float nearest = float.PositiveInfinity;
        foreach (PlayerIdentity player in PlayerIdentity.ActivePlayers)
        {
            if (player == null || !player.gameObject.activeInHierarchy ||
                !player.TryGetComponent<CharacterHealth>(out var playerHealth) || playerHealth.life <= 0) continue;
            float distance = ((Vector2)player.transform.position - rb.position).sqrMagnitude;
            if (distance >= nearest) continue;
            nearest = distance;
            position = player.transform.position;
        }
        return !float.IsPositiveInfinity(nearest);
    }

    protected override void DecideNextTile()
    {
        Vector2 origin = Grid(rb.position);
        Vector2 goal = TryPlayer(out var position) ? Grid(position) : origin;
        bool seekingLanding = phase == Phase.Flying && phaseElapsed >= flightDuration;
        frontier.Clear();
        firstSteps.Clear();
        frontier.Enqueue(origin);
        firstSteps[origin] = Vector2.zero;
        Vector2 bestStep = Vector2.zero;
        float bestDistance = (origin - goal).sqrMagnitude;
        // Breadth-first search permits routes that initially move away from the player.
        while (frontier.Count > 0 && firstSteps.Count < 1024)
        {
            Vector2 current = frontier.Dequeue();
            if (current != origin)
            {
                float distance = (current - goal).sqrMagnitude;
                if (seekingLanding && !Blocked(current, false))
                {
                    bestStep = firstSteps[current];
                    break;
                }
                if (!seekingLanding && distance < bestDistance)
                {
                    bestDistance = distance;
                    bestStep = firstSteps[current];
                    if (distance < 0.01f) break;
                }
            }
            foreach (Vector2 dir in Dirs)
            {
                Vector2 next = current + dir * tileSize;
                if (firstSteps.ContainsKey(next) || (next - origin).sqrMagnitude > 576f * tileSize * tileSize || IsTileBlocked(next)) continue;
                firstSteps[next] = current == origin ? dir : firstSteps[current];
                frontier.Enqueue(next);
            }
        }
        if (bestStep == Vector2.zero)
        {
            foreach (Vector2 dir in Dirs)
                if (!IsTileBlocked(origin + dir * tileSize)) { bestStep = dir; break; }
        }
        if (bestStep != Vector2.zero) direction = bestStep;
        targetTile = origin + bestStep * tileSize;
        isStuck = false;
    }

    protected override bool IsTileBlocked(Vector2 center) => Blocked(center, phase == Phase.Flying);

    bool Blocked(Vector2 center, bool flying)
    {
        foreach (Collider2D hit in Physics2D.OverlapBoxAll(center, Vector2.one * tileSize * 0.8f, 0f, obstacleMask))
        {
            if (hit == null || hit.transform.IsChildOf(transform) || hit.GetComponentInParent<StageAssets.IgluRoofController>() != null) continue;
            if (flying && hit.CompareTag("Destructibles") && hit.GetComponentInParent<Bomb>() == null) continue;
            return true;
        }
        return false;
    }

    protected override void OnTriggerEnter2D(Collider2D other)
    {
        // Flight and its transitions are protected; grounded attacks accept damage.
        if (!IsFlightPhase(phase) && other.gameObject.layer == LayerMask.NameToLayer("Explosion"))
            base.OnTriggerEnter2D(other);
    }

    static bool IsFlightPhase(Phase value) => value == Phase.Takeoff || value == Phase.Flying || value == Phase.Landing;

    void BeginAttack()
    {
        Vector2 delta = TryPlayer(out var player) ? player - rb.position : direction;
        attackDirection = Mathf.Abs(delta.x) > Mathf.Abs(delta.y)
            ? (delta.x >= 0f ? Vector2.right : Vector2.left)
            : (delta.y >= 0f ? Vector2.up : Vector2.down);
        Enter(Phase.PreparingAttack, preparingAttack);
    }

    void Enter(Phase next, AnimatedSpriteRenderer visual, bool reverse = false)
    {
        bool wasImmune = IsFlightPhase(phase);
        phase = next;
        phaseElapsed = frameElapsed = 0f;
        reversing = reverse;
        if (TryGetComponent<CharacterHealth>(out var characterHealth) && wasImmune != IsFlightPhase(next))
            characterHealth.SetExternalInvulnerability(IsFlightPhase(next));
        HideVisuals();
        activeSprite = visual;
        if (visual == null) return;
        bool moving = next == Phase.Ground || next == Phase.Flying;
        visual.loop = moving;
        visual.idle = false;
        visual.SetManualAnimationUpdate(!moving);
        visual.enabled = true;
        visual.RestartAnimation();
        if (reverse) visual.CurrentFrame = Mathf.Max(0, visual.animationSprite.Length - 1);
        visual.RefreshFrame();
    }

    bool AdvanceSequence()
    {
        if (activeSprite == null || activeSprite.animationSprite == null || activeSprite.animationSprite.Length == 0) return true;
        frameElapsed += Time.fixedDeltaTime;
        int count = activeSprite.animationSprite.Length;
        while (true)
        {
            int frame = activeSprite.CurrentFrame;
            float duration = activeSprite.frameDurations != null && activeSprite.frameDurations.Length == count
                ? activeSprite.frameDurations[frame]
                : (activeSprite.useSequenceDuration ? activeSprite.sequenceDuration / count : activeSprite.animationTime);
            duration = Mathf.Max(0.0001f, duration);
            if (frameElapsed < duration) return false;
            frameElapsed -= duration;
            int next = frame + (reversing ? -1 : 1);
            if (next < 0 || next >= count) return true;
            activeSprite.CurrentFrame = next;
            activeSprite.RefreshFrame();
        }
    }

    void LaunchRocket()
    {
        AnimatedSpriteRenderer template = attackDirection.x != 0f ? rocketLeft : attackDirection == Vector2.up ? rocketTop : rocketDown;
        if (template == null || explosion == null) return;
        // Spawn on the centered tile so adjacent obstacles also receive the impact.
        GameObject rocket = Instantiate(template.gameObject, rb.position, Quaternion.identity);
        rocket.name = "GurunRocket";
        rocket.layer = LayerMask.NameToLayer("Default");
        rocket.SetActive(true);
        AnimatedSpriteRenderer animation = rocket.GetComponent<AnimatedSpriteRenderer>();
        animation.SetManualAnimationUpdate(false);
        animation.enabled = true;
        animation.idle = false;
        animation.RestartAnimation();
        rocket.GetComponent<SpriteRenderer>().flipX = attackDirection == Vector2.right;
        rocket.AddComponent<BirdRobotRocket>().Init(attackDirection, gameObject, explosion, tileSize, true);
        if (rocketSfx != null && TryGetComponent<AudioSource>(out var source)) GameAudioSettings.PlaySfx(source, rocketSfx);
    }

    void HideVisuals()
    {
        foreach (AnimatedSpriteRenderer visual in GetComponentsInChildren<AnimatedSpriteRenderer>(true))
        {
            visual.enabled = false;
            if (visual.TryGetComponent<SpriteRenderer>(out var renderer)) renderer.enabled = false;
        }
    }

    protected override void Die()
    {
        HideVisuals();
        base.Die();
    }

    void OnDisable()
    {
        if (TryGetComponent<CharacterHealth>(out var characterHealth)) characterHealth.SetExternalInvulnerability(false);
    }

    void OnEnable()
    {
        if (IsFlightPhase(phase) && TryGetComponent<CharacterHealth>(out var characterHealth)) characterHealth.SetExternalInvulnerability(true);
    }
}

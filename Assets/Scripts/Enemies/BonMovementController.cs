using UnityEngine;

public sealed class BonMovementController : JunctionTurningEnemyMovementController
{
    private enum AttackState { Walking, FinishingMovement, Starting, Attacking, Ending }

    private AnimatedSpriteRenderer movement;
    private AnimatedSpriteRenderer startingAttack;
    private AnimatedSpriteRenderer attacking;
    private AnimatedSpriteRenderer endingAttack;
    private BoxCollider2D attackArea;
    private AttackState state;
    private float timer;

    protected override void Awake()
    {
        movement = FindVisual("Movimentation");
        startingAttack = FindVisual("StartingAttack");
        attacking = FindVisual("Attacking");
        endingAttack = FindVisual("EndingAttack");
        spriteUp = spriteDown = spriteLeft = spriteRight = movement;
        spriteDeath = FindVisual("Death");
        base.Awake();

        var area = new GameObject("AttackArea");
        area.transform.SetParent(transform, false);
        area.layer = LayerMask.NameToLayer("Enemy");
        attackArea = area.AddComponent<BoxCollider2D>();
        attackArea.isTrigger = true;
        attackArea.excludeLayers = ~LayerMask.GetMask("Player");
        attackArea.size = Vector2.one * (tileSize * 3f);
        attackArea.enabled = false;
        ShowVisual(movement, true);
    }

    protected override void Start()
    {
        base.Start();
        timer = Random.Range(10f, 20f);
    }

    protected override void FixedUpdate()
    {
        if (isDead || isInDamagedLoop ||
            (TryGetComponent<StunReceiver>(out var stun) && stun.IsStunned))
            return;

        float step = Time.fixedDeltaTime;
        activeSprite?.AdvanceAnimation(step, step);
        timer -= step;

        if (state == AttackState.Walking)
        {
            if (timer > 0f)
            {
                base.FixedUpdate();
                return;
            }

            SnapToGrid();
            targetTile = rb.position;
            rb.linearVelocity = Vector2.zero;
            state = AttackState.FinishingMovement;
            movement.loop = false;
            timer = RemainingDuration(movement);
            return;
        }

        rb.linearVelocity = Vector2.zero;
        if (timer > 0f)
            return;

        switch (state)
        {
            case AttackState.FinishingMovement:
                state = AttackState.Starting;
                ShowVisual(startingAttack, false);
                timer = RemainingDuration(startingAttack);
                break;
            case AttackState.Starting:
                state = AttackState.Attacking;
                ShowVisual(attacking, true);
                attackArea.enabled = true;
                timer = 5f;
                break;
            case AttackState.Attacking:
                attackArea.enabled = false;
                state = AttackState.Ending;
                ShowVisual(endingAttack, false);
                timer = RemainingDuration(endingAttack);
                break;
            case AttackState.Ending:
                state = AttackState.Walking;
                ShowVisual(movement, true);
                timer = Random.Range(10f, 20f);
                isStuck = false;
                DecideNextTile();
                break;
        }
    }

    protected override void UpdateSpriteDirection(Vector2 dir)
    {
        // All four directions share the authored movement animation.
        if (state == AttackState.Walking)
            base.UpdateSpriteDirection(dir);
    }

    protected override void OnTriggerEnter2D(Collider2D other)
    {
        // Expanded attack contacts must not redirect navigation or receive explosions.
        if (state == AttackState.Walking || state == AttackState.FinishingMovement ||
            other.gameObject.layer == LayerMask.NameToLayer("Explosion"))
            base.OnTriggerEnter2D(other);
    }

    protected override void Die()
    {
        if (attackArea != null)
            attackArea.enabled = false;
        ShowVisual(null, false);
        base.Die();
    }

    private void OnDisable()
    {
        if (attackArea != null)
            attackArea.enabled = false;
    }

    private AnimatedSpriteRenderer FindVisual(string childName)
        => transform.Find(childName)?.GetComponent<AnimatedSpriteRenderer>();

    private void ShowVisual(AnimatedSpriteRenderer selected, bool loop)
    {
        foreach (var visual in GetComponentsInChildren<AnimatedSpriteRenderer>(true))
        {
            visual.enabled = visual == selected;
            if (visual.TryGetComponent<SpriteRenderer>(out var renderer))
                renderer.enabled = visual == selected;
        }

        activeSprite = selected;
        if (selected == null)
            return;

        selected.loop = loop;
        selected.idle = false;
        selected.SetManualAnimationUpdate(true);
        selected.RestartAnimation();
    }

    private static float RemainingDuration(AnimatedSpriteRenderer visual)
    {
        if (visual == null || visual.animationSprite == null)
            return 0f;

        int count = visual.animationSprite.Length;
        float duration = -visual.DebugFrameTimer;
        for (int frame = visual.CurrentFrame; frame < count; frame++)
        {
            float frameDuration = visual.useSequenceDuration
                ? visual.sequenceDuration / Mathf.Max(1, count)
                : visual.animationTime;
            if (visual.frameDurations != null && visual.frameDurations.Length == count)
                frameDuration = visual.frameDurations[frame];
            duration += Mathf.Max(0.0001f, frameDuration);
        }
        return Mathf.Max(0f, duration);
    }
}

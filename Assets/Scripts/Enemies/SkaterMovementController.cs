using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Moves Skater through junctions while alternating between an accelerating walk
/// and a two-second decelerating slide.
/// </summary>
public sealed class SkaterMovementController : JunctionTurningEnemyMovementController
{
    [Header("Skater Movement")]
    [SerializeField, Min(0.01f)] private float maximumSpeed = 3f;
    [SerializeField, Min(0.01f)] private float acceleration = 1.5f;
    [SerializeField, Min(0.01f)] private float slideDuration = 2f;

    private float currentSpeed;
    private float slideElapsed;
    private bool isSliding;

    protected override void Start()
    {
        currentSpeed = 0f;
        speed = 0f;
        isSliding = false;
        slideElapsed = 0f;

        base.Start();
        SetWalkingAnimation();
    }

    protected override void FixedUpdate()
    {
        if (isDead)
            return;

        if (TryGetComponent<StunReceiver>(out StunReceiver stun) && stun != null && stun.IsStunned)
        {
            base.FixedUpdate();
            return;
        }

        if (isInDamagedLoop)
        {
            base.FixedUpdate();
            return;
        }

        UpdateMovementPhase();
        base.FixedUpdate();
    }

    protected override void UpdateSpriteDirection(Vector2 dir)
    {
        base.UpdateSpriteDirection(dir);

        if (activeSprite == null || isInDamagedLoop || isDead)
            return;

        if (isSliding)
        {
            activeSprite.SetManualAnimationUpdate(true);
            activeSprite.CurrentFrame = 0;
            activeSprite.RefreshFrame();
            return;
        }

        activeSprite.SetManualAnimationUpdate(false);
        activeSprite.loop = true;
        activeSprite.idle = false;
    }

    protected override void Die()
    {
        isSliding = false;
        currentSpeed = 0f;
        speed = 0f;
        SetWalkingAnimation();
        base.Die();
    }

    private void UpdateMovementPhase()
    {
        if (!isSliding)
        {
            currentSpeed = Mathf.MoveTowards(
                currentSpeed,
                maximumSpeed,
                acceleration * Time.fixedDeltaTime);
            speed = currentSpeed;

            if (currentSpeed >= maximumSpeed)
                BeginSliding();

            return;
        }

        slideElapsed += Time.fixedDeltaTime;
        float normalizedTime = Mathf.Clamp01(slideElapsed / slideDuration);
        currentSpeed = maximumSpeed * (1f - normalizedTime);
        speed = currentSpeed;

        if (normalizedTime >= 1f)
            EndSliding();
    }

    private void BeginSliding()
    {
        isSliding = true;
        slideElapsed = 0f;
        SetSlidingAnimation();
    }

    private void EndSliding()
    {
        isSliding = false;
        slideElapsed = 0f;
        currentSpeed = 0f;
        speed = 0f;
        SetWalkingAnimation();
    }

    private void SetSlidingAnimation()
    {
        foreach (AnimatedSpriteRenderer sprite in GetDirectionalSprites())
        {
            sprite.SetManualAnimationUpdate(true);
            sprite.CurrentFrame = 0;
            sprite.RefreshFrame();
        }
    }

    private void SetWalkingAnimation()
    {
        foreach (AnimatedSpriteRenderer sprite in GetDirectionalSprites())
        {
            sprite.SetManualAnimationUpdate(false);
            sprite.loop = true;
            sprite.idle = false;
        }

        if (activeSprite != null)
        {
            activeSprite.CurrentFrame = 0;
            activeSprite.RefreshFrame();
        }
    }

    private IEnumerable<AnimatedSpriteRenderer> GetDirectionalSprites()
    {
        var seen = new HashSet<AnimatedSpriteRenderer>();

        AddDirectionalSprite(spriteUp, seen);
        AddDirectionalSprite(spriteDown, seen);
        AddDirectionalSprite(spriteLeft, seen);
        AddDirectionalSprite(spriteRight, seen);

        return seen;
    }

    private static void AddDirectionalSprite(
        AnimatedSpriteRenderer sprite,
        ISet<AnimatedSpriteRenderer> sprites)
    {
        if (sprite != null)
            sprites.Add(sprite);
    }
}

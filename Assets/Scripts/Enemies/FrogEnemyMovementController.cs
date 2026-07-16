using UnityEngine;

public sealed class FrogEnemyMovementController : EnemyMovementController
{
    [Header("Idle Visual")]
    [SerializeField] private AnimatedSpriteRenderer jokeSprite;

    protected override void Awake()
    {
        if (jokeSprite != null)
        {
            spriteDown = jokeSprite;
            spriteUp = null;
            spriteLeft = null;
        }

        base.Awake();
        activeSprite = jokeSprite != null ? jokeSprite : spriteDown;
    }

    protected override void Start()
    {
        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        if (activeSprite == null)
            return;

        activeSprite.enabled = true;
        activeSprite.idle = false;
        activeSprite.loop = true;
    }

    protected override void FixedUpdate()
    {
        if (rb != null)
            rb.linearVelocity = Vector2.zero;
    }

    protected override void Die()
    {
        AnimatedSpriteRenderer[] animatedSprites = GetComponentsInChildren<AnimatedSpriteRenderer>(true);
        foreach (AnimatedSpriteRenderer animatedSprite in animatedSprites)
        {
            if (animatedSprite != null && animatedSprite != spriteDeath)
                animatedSprite.enabled = false;
        }

        base.Die();
    }
}

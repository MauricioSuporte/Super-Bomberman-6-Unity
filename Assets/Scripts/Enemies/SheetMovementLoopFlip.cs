using UnityEngine;

[RequireComponent(typeof(AnimatedSpriteRenderer), typeof(SpriteRenderer))]
public sealed class SheetMovementLoopFlip : MonoBehaviour
{
    private AnimatedSpriteRenderer animatedSprite;
    private SpriteRenderer spriteRenderer;
    private int previousFrame;
    private bool alternateFlip;
    private bool baseFlipX;
    private bool appliedFlipX;

    private void Awake()
    {
        animatedSprite = GetComponent<AnimatedSpriteRenderer>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void OnEnable()
    {
        previousFrame = animatedSprite != null ? animatedSprite.CurrentFrame : 0;
        alternateFlip = false;
        baseFlipX = spriteRenderer != null && spriteRenderer.flipX;
        appliedFlipX = baseFlipX;
    }

    private void LateUpdate()
    {
        if (animatedSprite == null || spriteRenderer == null ||
            animatedSprite.idle || !animatedSprite.loop ||
            animatedSprite.animationSprite == null || animatedSprite.animationSprite.Length < 3)
            return;

        if (spriteRenderer.flipX != appliedFlipX)
            baseFlipX = spriteRenderer.flipX;

        int currentFrame = animatedSprite.CurrentFrame;
        if (currentFrame < previousFrame)
            alternateFlip = !alternateFlip;

        previousFrame = currentFrame;
        appliedFlipX = baseFlipX ^ alternateFlip;
        spriteRenderer.flipX = appliedFlipX;
    }
}

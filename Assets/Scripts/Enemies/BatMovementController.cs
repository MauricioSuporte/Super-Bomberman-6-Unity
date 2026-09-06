using UnityEngine;

public sealed class BatMovementController : FlyMovimentController
{
    private const float CoreDestructionDuration = 0.5f;

    [SerializeField] private AnimatedSpriteRenderer coreDestruction;

    protected override void OnDeathAnimationEnded()
    {
        if (coreDestruction == null || coreDestruction.animationSprite == null ||
            coreDestruction.animationSprite.Length == 0 || !coreDestruction.TryGetComponent(out SpriteRenderer renderer))
        {
            base.OnDeathAnimationEnded();
            return;
        }

        if (spriteDeath != null)
        {
            spriteDeath.enabled = false;
            if (spriteDeath.TryGetComponent(out SpriteRenderer deathRenderer))
                deathRenderer.enabled = false;
        }

        coreDestruction.gameObject.SetActive(true);
        renderer.enabled = true;
        renderer.flipX = false;
        renderer.flipY = false;
        coreDestruction.enabled = true;
        coreDestruction.idle = false;
        coreDestruction.loop = false;
        coreDestruction.useSequenceDuration = true;
        coreDestruction.sequenceDuration = CoreDestructionDuration;
        coreDestruction.CurrentFrame = 0;
        coreDestruction.RestartAnimation();
        Invoke(nameof(FinishCoreDestruction), CoreDestructionDuration);
    }

    private void FinishCoreDestruction()
    {
        if (coreDestruction == null)
        {
            base.OnDeathAnimationEnded();
            return;
        }

        coreDestruction.enabled = false;
        if (coreDestruction.TryGetComponent(out SpriteRenderer renderer))
            renderer.enabled = false;
        base.OnDeathAnimationEnded();
    }
}

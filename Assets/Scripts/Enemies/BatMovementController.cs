using System.Collections.Generic;
using UnityEngine;

public sealed class BatMovementController : FlyMovimentController
{
    private const float CoreDestructionDuration = 0.5f;

    [SerializeField] private SpriteRenderer squishedPose;
    [SerializeField] private AnimatedSpriteRenderer coreDestruction;
    private readonly List<(GameObject visual, bool wasActive)> barrelHiddenMovementVisuals = new();
    private StunReceiver barrelStun;

    public bool TryBarrelCrushStun(float seconds)
    {
        if (isDead || !TryGetComponent(out StunReceiver stun) || !stun.TryCrushStun(seconds, squishedPose))
            return false;

        barrelStun = stun;
        HideMovementVisualForBarrel(spriteUp);
        HideMovementVisualForBarrel(spriteDown);
        HideMovementVisualForBarrel(spriteLeft);
        return true;
    }

    private void HideMovementVisualForBarrel(AnimatedSpriteRenderer animation)
    {
        if (animation == null || animation.gameObject == gameObject)
            return;

        GameObject visual = animation.gameObject;
        foreach (var entry in barrelHiddenMovementVisuals)
            if (entry.visual == visual)
                return;

        barrelHiddenMovementVisuals.Add((visual, visual.activeSelf));
        visual.SetActive(false);
    }

    protected override void FixedUpdate()
    {
        base.FixedUpdate();

        if (barrelHiddenMovementVisuals.Count > 0 &&
            (isDead || barrelStun == null || !barrelStun.IsStunned))
            RestoreBarrelMovementVisuals();
    }

    protected override void UpdateSpriteDirection(Vector2 dir)
    {
        if (!isDead && barrelStun != null && barrelStun.IsStunned)
            return;

        base.UpdateSpriteDirection(dir);
    }

    private void RestoreBarrelMovementVisuals()
    {
        foreach (var entry in barrelHiddenMovementVisuals)
            if (entry.visual != null)
                entry.visual.SetActive(entry.wasActive);
        barrelHiddenMovementVisuals.Clear();
        barrelStun = null;
    }

    protected override void Die()
    {
        if (isDead)
            return;

        RestoreBarrelMovementVisuals();
        base.Die();
        if (squishedPose != null)
            squishedPose.enabled = false;
    }

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

    private void OnDisable()
    {
        RestoreBarrelMovementVisuals();
    }
}

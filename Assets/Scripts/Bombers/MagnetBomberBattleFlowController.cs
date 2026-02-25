using UnityEngine;

public class MagnetBomberBattleFlowController : BossIntroFlowBase
{
    [SerializeField] private MovementController boss;
    [SerializeField] private BossBomberAI bossAI;
    [SerializeField] private AIMovementController aiMove;
    [SerializeField] private Vector2 forceBossFacing;

    protected override void LockBoss(bool locked)
    {
        if (!boss) return;

        boss.SetInputLocked(locked, true);
        boss.SetExplosionInvulnerable(locked);

        if (boss.Rigidbody != null)
            boss.Rigidbody.linearVelocity = Vector2.zero;

        if (locked && forceBossFacing.sqrMagnitude > 0.0001f)
            boss.ForceFacingDirection(forceBossFacing);

        if (bossAI) bossAI.enabled = !locked;
        if (aiMove) aiMove.enabled = !locked;

        if (aiMove)
            aiMove.SetIntroIdle(locked);
    }

    protected override void UnlockBoss()
    {
        if (!boss) return;

        boss.SetInputLocked(false, true);
        boss.SetExplosionInvulnerable(false);

        if (aiMove)
            aiMove.SetIntroIdle(false);

        if (bossAI) bossAI.enabled = true;
        if (aiMove) aiMove.enabled = true;
    }
}
using UnityEngine;

/// <summary>
/// Pursues a visible player through the standard junction-turning line of
/// sight, moving 50% faster for as long as that player remains visible.
/// </summary>
public sealed class Yeti93MovementController : JunctionTurningPersecutingEnemyMovementController
{
    [SerializeField, Min(1f)] private float pursuitSpeedMultiplier = 1.5f;

    private float patrolSpeed;

    protected override void Awake()
    {
        base.Awake();
        patrolSpeed = speed;
    }

    protected override void ApplyPursuitSpeed(bool playerVisible)
    {
        speed = playerVisible ? patrolSpeed * pursuitSpeedMultiplier : patrolSpeed;
    }
}

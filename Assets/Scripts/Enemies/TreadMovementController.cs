using UnityEngine;

/// <summary>
/// A junction-turning enemy that intermittently surges to four times its
/// configured movement speed before returning to its normal patrol pace.
/// </summary>
public sealed class TreadMovementController : JunctionTurningEnemyMovementController
{
    [Header("Speed Burst")]
    [SerializeField, Range(0f, 1f)] private float burstChancePerTile = 0.2f;
    [SerializeField, Min(0.01f)] private float burstDuration = 0.5f;

    private float normalSpeed;
    private float burstRemaining;

    protected override void Awake()
    {
        base.Awake();
        normalSpeed = speed;
    }

    protected override void FixedUpdate()
    {
        if (burstRemaining > 0f)
        {
            burstRemaining = Mathf.Max(0f, burstRemaining - Time.fixedDeltaTime);
            if (burstRemaining == 0f)
                speed = normalSpeed;
        }

        base.FixedUpdate();
    }

    protected override void DecideNextTile()
    {
        base.DecideNextTile();

        if (burstRemaining > 0f || Random.value > burstChancePerTile)
            return;

        burstRemaining = burstDuration;
        speed = normalSpeed * 4f;
    }
}

using UnityEngine;

/// <summary>
/// A junction-turning enemy that gains a short burst of speed whenever it
/// makes a perpendicular turn.
/// </summary>
[RequireComponent(typeof(CharacterHealth))]
public sealed class BoboMovementController : JunctionTurningEnemyMovementController
{
    [Header("Bobo Turn Burst")]
    [SerializeField, Min(1f)] float turnSpeedMultiplier = 1.5f;
    [SerializeField, Min(0.01f)] float turnSpeedDuration = 0.18f;

    float normalSpeed;
    float turnSpeedTimer;

    protected override void Awake()
    {
        base.Awake();
        normalSpeed = speed;
    }

    protected override void FixedUpdate()
    {
        if (turnSpeedTimer <= 0f && !Mathf.Approximately(speed, normalSpeed))
            speed = normalSpeed;

        base.FixedUpdate();

        if (turnSpeedTimer <= 0f)
            return;

        turnSpeedTimer -= Time.fixedDeltaTime;
        if (turnSpeedTimer <= 0f)
            speed = normalSpeed;
    }

    protected override void DecideNextTile()
    {
        Vector2 previousDirection = direction;
        base.DecideNextTile();

        if (previousDirection == Vector2.zero ||
            direction == previousDirection ||
            Vector2.Dot(previousDirection, direction) != 0f)
        {
            return;
        }

        turnSpeedTimer = turnSpeedDuration;
        speed = normalSpeed * turnSpeedMultiplier;
    }

    protected override void UpdateSpriteDirection(Vector2 dir)
    {
        base.UpdateSpriteDirection(dir);

        if (spriteDown != null && spriteDown.TryGetComponent<SpriteRenderer>(out var downRenderer))
            downRenderer.flipY = dir == Vector2.down;
    }
}

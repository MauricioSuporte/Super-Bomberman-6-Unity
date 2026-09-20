using UnityEngine;

/// <summary>
/// Junction-turning Mini Hogera that can receive a direction before its first
/// movement tick, allowing groups spawned on one tile to disperse immediately.
/// </summary>
public sealed class MiniHogeraMovementController : JunctionTurningEnemyMovementController
{
    private const float InitialEnemyCollisionGraceSeconds = 0.3f;

    private Vector2 spawnDirection;
    private bool hasSpawnDirection;
    private float ignoreEnemyCollisionsUntil;

    public void SetSpawnDirection(Vector2 desiredDirection)
    {
        if (Mathf.Abs(desiredDirection.x) > Mathf.Abs(desiredDirection.y))
            spawnDirection = desiredDirection.x >= 0f ? Vector2.right : Vector2.left;
        else
            spawnDirection = desiredDirection.y >= 0f ? Vector2.up : Vector2.down;

        hasSpawnDirection = true;
        ignoreEnemyCollisionsUntil = Time.time + InitialEnemyCollisionGraceSeconds;
    }

    protected override void Start()
    {
        SnapToGrid();

        if (hasSpawnDirection)
            direction = spawnDirection;
        else
            ChooseInitialDirection();

        UpdateSpriteDirection(direction);
        DecideNextTile();
    }

    protected override void OnTriggerEnter2D(Collider2D other)
    {
        if (Time.time < ignoreEnemyCollisionsUntil &&
            other.gameObject.layer == LayerMask.NameToLayer("Enemy"))
        {
            return;
        }

        base.OnTriggerEnter2D(other);
    }
}

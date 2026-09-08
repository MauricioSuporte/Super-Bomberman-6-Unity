using UnityEngine;

/// <summary>
/// Pyramid enemy that chases the closest player, briefly retreats when the
/// player gets too close, and intermittently breathes a two-tile fire stream
/// while continuing to walk.
/// </summary>
public sealed class FarohMovementController : JunctionTurningEnemyMovementController
{
    [Header("Pursuit")]
    [SerializeField, Min(0.1f)] private float retreatTriggerDistanceTiles = 2f;
    [SerializeField, Min(0.01f)] private float retreatDurationSeconds = 4f;

    [Header("Fire Breath")]
    [SerializeField, Min(0.01f)] private float fireMinCooldownSeconds = 5f;
    [SerializeField, Min(0.01f)] private float fireMaxCooldownSeconds = 8f;
    [SerializeField, Min(0.01f)] private float fireDurationSeconds = 2f;
    [SerializeField, Min(0.01f)] private float fireBurstIntervalSeconds = 0.15f;
    [SerializeField, Min(1)] private int fireRangeTiles = 2;
    [SerializeField, Min(0.01f)] private float fireTravelSpeed = 5f;
    [SerializeField, Range(0.1f, 1f)] private float fireHitboxSizePercent = 0.7f;
    [SerializeField] private AnimatedSpriteRenderer fireVisual;
    [SerializeField] private LayerMask fireTargetMask;

    private float retreatRemaining;
    private float nextFireAt;
    private float fireRemaining;
    private float nextFireBurstAt;

    protected override void Awake()
    {
        fireVisual ??= transform.Find("Fire")?.GetComponent<AnimatedSpriteRenderer>();
        if (fireVisual != null)
            fireVisual.enabled = false;

        // Player is layer 3 in this project (not the Explosion layer 8).
        // Use only the two intended targets, repairing stale prefab data.
        fireTargetMask = LayerMask.GetMask("Player", "Bomb");

        base.Awake();
    }

    protected override void Start()
    {
        base.Start();
        ScheduleNextFire();
    }

    protected override void FixedUpdate()
    {
        if (isDead)
            return;

        if (TryGetComponent(out StunReceiver stun) && stun != null && stun.IsStunned)
        {
            HideFire();
            base.FixedUpdate();
            return;
        }

        if (isInDamagedLoop)
        {
            HideFire();
            base.FixedUpdate();
            return;
        }

        UpdateFireBreath();
        base.FixedUpdate();
    }

    protected override void DecideNextTile()
    {
        if (rb == null)
        {
            base.DecideNextTile();
            return;
        }

        if (!TryGetClosestPlayer(out Vector2 playerPosition, out float distance))
        {
            base.DecideNextTile();
            return;
        }

        float retreatDistance = Mathf.Max(tileSize, retreatTriggerDistanceTiles * tileSize);
        if (retreatRemaining <= 0f && distance <= retreatDistance)
            retreatRemaining = retreatDurationSeconds;

        Vector2 desiredDirection = retreatRemaining > 0f
            ? CardinalFromVector(rb.position - playerPosition, direction)
            : CardinalFromVector(playerPosition - rb.position, direction);

        if (TryChooseOpenDirection(desiredDirection, out Vector2 chosenDirection))
        {
            isStuck = false;
            direction = chosenDirection;
            UpdateSpriteDirection(direction);
            targetTile = rb.position + direction * tileSize;
            return;
        }

        base.DecideNextTile();
    }

    protected override void Die()
    {
        HideFire();
        base.Die();
    }

    protected override void OnDestroy()
    {
        HideFire();
        base.OnDestroy();
    }

    private void UpdateFireBreath()
    {
        if (retreatRemaining > 0f)
            retreatRemaining = Mathf.Max(0f, retreatRemaining - Time.fixedDeltaTime);

        if (fireRemaining <= 0f && Time.time >= nextFireAt)
        {
            fireRemaining = fireDurationSeconds;
            nextFireBurstAt = Time.time;
        }

        if (fireRemaining <= 0f)
            return;

        fireRemaining = Mathf.Max(0f, fireRemaining - Time.fixedDeltaTime);
        if (Time.time >= nextFireBurstAt)
        {
            EmitFireBurst();
            nextFireBurstAt = Time.time + fireBurstIntervalSeconds;
        }

        if (fireRemaining <= 0f)
        {
            HideFire();
            ScheduleNextFire();
        }
    }

    private void EmitFireBurst()
    {
        if (fireVisual == null)
            return;

        Vector2 fireDirection = CardinalFromVector(direction, Vector2.down);
        Vector2 launchPosition = rb.position + fireDirection * (tileSize * 0.45f);
        GameObject fire = Instantiate(fireVisual.gameObject, launchPosition, Quaternion.identity);
        fire.name = "Faroh Fire";

        FarohFireProjectile projectile = fire.AddComponent<FarohFireProjectile>();
        projectile.Launch(
            fireDirection,
            fireRangeTiles,
            tileSize,
            fireTravelSpeed,
            fireHitboxSizePercent,
            fireTargetMask);
    }

    private void HideFire()
    {
        if (fireVisual == null)
            return;

        fireVisual.enabled = false;
    }

    private void ScheduleNextFire()
    {
        nextFireAt = Time.time + Random.Range(fireMinCooldownSeconds, Mathf.Max(fireMinCooldownSeconds, fireMaxCooldownSeconds));
    }

    private bool TryGetClosestPlayer(out Vector2 playerPosition, out float distance)
    {
        playerPosition = Vector2.zero;
        distance = float.PositiveInfinity;

        foreach (MovementController player in FindObjectsByType<MovementController>())
        {
            if (player == null || player.isDead || player.IsEndingStage || !player.isActiveAndEnabled)
                continue;

            float candidateDistance = Vector2.Distance(rb.position, player.Rigidbody != null ? player.Rigidbody.position : (Vector2)player.transform.position);
            if (candidateDistance >= distance)
                continue;

            distance = candidateDistance;
            playerPosition = player.Rigidbody != null ? player.Rigidbody.position : (Vector2)player.transform.position;
        }

        return distance < float.PositiveInfinity;
    }

    private bool TryChooseOpenDirection(Vector2 preferredDirection, out Vector2 chosenDirection)
    {
        chosenDirection = preferredDirection;
        if (!IsTileBlocked(rb.position + preferredDirection * tileSize))
            return true;

        Vector2[] alternatives = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
        float bestScore = float.NegativeInfinity;
        bool found = false;

        for (int i = 0; i < alternatives.Length; i++)
        {
            Vector2 candidate = alternatives[i];
            if (IsTileBlocked(rb.position + candidate * tileSize))
                continue;

            float score = Vector2.Dot(candidate, preferredDirection);
            if (score > bestScore)
            {
                bestScore = score;
                chosenDirection = candidate;
                found = true;
            }
        }

        return found;
    }

    private static Vector2 CardinalFromVector(Vector2 vector, Vector2 fallback)
    {
        if (vector.sqrMagnitude < 0.0001f)
            return fallback == Vector2.zero ? Vector2.down : fallback;

        if (Mathf.Abs(vector.x) >= Mathf.Abs(vector.y))
            return vector.x >= 0f ? Vector2.right : Vector2.left;

        return vector.y >= 0f ? Vector2.up : Vector2.down;
    }
}

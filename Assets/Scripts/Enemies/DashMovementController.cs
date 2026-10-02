using System.Collections.Generic;
using UnityEngine;

/// <summary>Continuously pursues players and boosts on cardinal alignment, regardless of visibility.</summary>
public sealed class DashMovementController : PersecutingEnemyMovementController
{
    [Header("Dash Rocket Booster")]
    [SerializeField, Min(1f)] private float boostSpeedMultiplier = 2f;
    [SerializeField, Min(0.01f)] private float boostReleaseSeconds = 5f;
    [SerializeField, Min(0.001f)] private float boostAlignmentToleranceTiles = 0.15f;
    [SerializeField] private AnimatedSpriteRenderer boostUp;
    [SerializeField] private AnimatedSpriteRenderer boostDown;
    [SerializeField] private AnimatedSpriteRenderer boostLeft;

    private float normalSpeed;
    private float boostRemaining;
    private bool boosting;
    private readonly Queue<Vector2Int> frontier = new();
    private readonly Dictionary<Vector2Int, Vector2Int> firstSteps = new();

    protected override void Awake()
    {
        base.Awake();
        normalSpeed = speed;
        HideBoostSprites();
    }

    protected override void FixedUpdate()
    {
        if (isDead || GamePauseController.IsPaused)
            return;

        bool aligned = false;
        float tolerance = boostAlignmentToleranceTiles * tileSize;
        foreach (PlayerIdentity player in PlayerIdentity.ActivePlayers)
        {
            if (!IsAlive(player))
                continue;

            Vector2 offset = (Vector2)player.transform.position - rb.position;
            if (Mathf.Abs(offset.x) <= tolerance || Mathf.Abs(offset.y) <= tolerance)
                aligned = true;
        }

        boostRemaining = aligned ? boostReleaseSeconds : Mathf.Max(0f, boostRemaining - Time.fixedDeltaTime);
        bool shouldBoost = boostRemaining > 0f;
        speed = normalSpeed * (shouldBoost ? boostSpeedMultiplier : 1f);
        if (boosting != shouldBoost)
        {
            boosting = shouldBoost;
            UpdateSpriteDirection(direction);
        }

        // Replan when a bomb, enemy or moving stage obstacle enters the route.
        if (!isStuck && !isInDamagedLoop &&
            !(TryGetComponent<StunReceiver>(out var stun) && stun.IsStunned) &&
            IsTileBlocked(targetTile))
        {
            SnapToGrid();
            DecideNextTile();
        }

        base.FixedUpdate();
    }

    protected override void DecideNextTile()
    {
        Vector2 playerPosition = Vector2.zero;
        float nearestDistance = float.PositiveInfinity;
        foreach (PlayerIdentity player in PlayerIdentity.ActivePlayers)
        {
            if (!IsAlive(player))
                continue;

            Vector2 position = player.transform.position;
            float distance = (position - rb.position).sqrMagnitude;
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                playerPosition = position;
            }
        }

        if (!float.IsPositiveInfinity(nearestDistance) && TryFindRoute(playerPosition, out Vector2 nextDirection))
        {
            isStuck = false;
            direction = nextDirection;
            targetTile = rb.position + direction * tileSize;
            UpdateSpriteDirection(direction);
            return;
        }

        base.DecideNextTile();
    }

    private bool TryFindRoute(Vector2 destination, out Vector2 nextDirection)
    {
        nextDirection = Vector2.zero;
        Vector2Int origin = ToCell(rb.position);
        Vector2Int goal = ToCell(destination);
        if (origin == goal)
            return false;

        frontier.Clear();
        firstSteps.Clear();
        frontier.Enqueue(origin);
        firstSteps.Add(origin, Vector2Int.zero);

        // Bound searches when a player is enclosed or the stage has open edges.
        const int maximumVisitedTiles = 512;
        while (frontier.Count > 0 && firstSteps.Count < maximumVisitedTiles)
        {
            Vector2Int current = frontier.Dequeue();
            foreach (Vector2 candidate in Dirs)
            {
                Vector2Int step = new(Mathf.RoundToInt(candidate.x), Mathf.RoundToInt(candidate.y));
                Vector2Int next = current + step;
                if (firstSteps.ContainsKey(next) || IsTileBlocked((Vector2)next * tileSize))
                    continue;

                Vector2Int firstStep = current == origin ? step : firstSteps[current];
                firstSteps.Add(next, firstStep);
                if (next == goal)
                {
                    nextDirection = firstStep;
                    return true;
                }
                frontier.Enqueue(next);
            }
        }
        return false;
    }

    private Vector2Int ToCell(Vector2 position)
    {
        return new Vector2Int(Mathf.RoundToInt(position.x / tileSize), Mathf.RoundToInt(position.y / tileSize));
    }

    private static bool IsAlive(PlayerIdentity player)
    {
        return player != null && player.isActiveAndEnabled &&
            player.TryGetComponent<CharacterHealth>(out var playerHealth) &&
            !playerHealth.IsDead && playerHealth.life > 0;
    }

    protected override void UpdateSpriteDirection(Vector2 dir)
    {
        if (isDead || isInDamagedLoop)
            return;

        if (!boosting)
        {
            HideBoostSprites();
            base.UpdateSpriteDirection(dir);
            return;
        }

        AnimatedSpriteRenderer chosen = dir == Vector2.up ? boostUp : dir == Vector2.down ? boostDown : boostLeft;
        if (chosen == null)
        {
            HideBoostSprites();
            base.UpdateSpriteDirection(dir);
            return;
        }

        if (boostUp != chosen) SetBoostVisible(boostUp, false);
        if (boostDown != chosen) SetBoostVisible(boostDown, false);
        if (boostLeft != chosen) SetBoostVisible(boostLeft, false);
        int previousFrame = activeSprite != null ? activeSprite.CurrentFrame : 0;
        bool changedSprite = activeSprite != chosen;
        if (spriteUp != null) spriteUp.enabled = false;
        if (spriteDown != null) spriteDown.enabled = false;
        if (spriteLeft != null) spriteLeft.enabled = false;
        if (spriteRight != null) spriteRight.enabled = false;
        chosen.enabled = true;
        if (changedSprite && chosen.animationSprite != null && chosen.animationSprite.Length > 0)
        {
            chosen.CurrentFrame = previousFrame % chosen.animationSprite.Length;
            chosen.RefreshFrame();
        }
        chosen.idle = false;
        chosen.loop = true;
        if (chosen.TryGetComponent<SpriteRenderer>(out var renderer))
            renderer.flipX = dir == Vector2.right;
        activeSprite = chosen;
    }

    private void HideBoostSprites()
    {
        SetBoostVisible(boostUp, false);
        SetBoostVisible(boostDown, false);
        SetBoostVisible(boostLeft, false);
    }

    private static void SetBoostVisible(AnimatedSpriteRenderer sprite, bool visible)
    {
        if (sprite == null)
            return;
        sprite.enabled = visible;
        if (sprite.TryGetComponent<SpriteRenderer>(out var renderer))
            renderer.enabled = visible;
    }

    protected override void OnHitInvulnerabilityStarted(float seconds)
    {
        base.OnHitInvulnerabilityStarted(seconds);
        if (isInDamagedLoop)
            HideBoostSprites();
    }

    protected override void Die()
    {
        HideBoostSprites();
        base.Die();
    }
}

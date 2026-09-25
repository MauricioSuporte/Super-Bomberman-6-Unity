using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Pengo stops at a tile center when it spots a bomb in a clear cardinal lane,
/// then creates a destructible block in front of itself behind a short Snow
/// animation.
/// </summary>
public sealed class PengoMovementController : JunctionTurningEnemyMovementController
{
    private const float SnowDuration = 0.5f;
    private const float DirectionAlignmentToleranceInTiles = 0.2f;

    private readonly Dictionary<Vector2, AnimatedSpriteRenderer> attackSnowSprites = new();

    private Tilemap groundTilemap;
    private Tilemap destructibleTilemap;
    private Tilemap indestructibleTilemap;
    private TileBase destructibleTileTemplate;
    private bool preparingSnow;
    private Vector2 pendingSnowDirection;
    private float snowStartedAt;
    private AnimatedSpriteRenderer snowEffectSprite;
    private Vector3 snowEffectLocalPosition;
    private AnimatedSpriteRenderer activeAttackSnowSprite;

    protected override void Awake()
    {
        base.Awake();
        CacheAttackSnowSprite("SnowUp", Vector2.up);
        CacheAttackSnowSprite("SnowDown", Vector2.down);
        CacheAttackSnowSprite("SnowLeft", Vector2.left);
        CacheSnowEffectSprite();
    }

    protected override void Start()
    {
        ResolveTilemaps();
        ResolveDestructibleTileTemplate();
        base.Start();
    }

    protected override void FixedUpdate()
    {
        if (preparingSnow)
        {
            UpdateSnowPreparation();
            return;
        }

        if (CanReactToBomb() && TryFindVisibleBombDirection(out Vector2 bombDirection) &&
            CanCreateDestructibleAt(GetNextTilePosition(bombDirection)))
        {
            pendingSnowDirection = bombDirection;

            // Finish the current tile movement first, so the attack always
            // starts exactly at a grid center.
            if (!ReachedTile())
            {
                MoveTowardsTile();
                return;
            }

            SnapToGrid();
            if (!TryFindVisibleBombDirection(out pendingSnowDirection) ||
                !CanCreateDestructibleAt(GetNextTilePosition(pendingSnowDirection)))
            {
                base.DecideNextTile();
                return;
            }

            BeginSnowPreparation();
            return;
        }

        base.FixedUpdate();
    }

    protected override void OnTriggerEnter2D(Collider2D other)
    {
        if (!preparingSnow)
            base.OnTriggerEnter2D(other);
    }

    protected override void Die()
    {
        EndSnowPreparation();
        base.Die();
    }

    private bool CanReactToBomb()
    {
        return !isDead && !isInDamagedLoop && !GamePauseController.IsPaused &&
            !(TryGetComponent<StunReceiver>(out StunReceiver stun) && stun.IsStunned);
    }

    private bool TryFindVisibleBombDirection(out Vector2 foundDirection)
    {
        foundDirection = Vector2.zero;
        float closestDistance = float.MaxValue;

        foreach (Bomb bomb in Bomb.ActiveBombs)
        {
            if (bomb == null || bomb.HasExploded)
                continue;

            Vector2 delta = bomb.GetLogicalPosition() - rb.position;
            Vector2 directionToBomb;
            float distance;
            if (Mathf.Abs(delta.x) <= tileSize * DirectionAlignmentToleranceInTiles &&
                Mathf.Abs(delta.y) >= tileSize * (1f - DirectionAlignmentToleranceInTiles))
            {
                directionToBomb = delta.y > 0f ? Vector2.up : Vector2.down;
                distance = Mathf.Abs(delta.y);
            }
            else if (Mathf.Abs(delta.y) <= tileSize * DirectionAlignmentToleranceInTiles &&
                     Mathf.Abs(delta.x) >= tileSize * (1f - DirectionAlignmentToleranceInTiles))
            {
                directionToBomb = delta.x > 0f ? Vector2.right : Vector2.left;
                distance = Mathf.Abs(delta.x);
            }
            else
            {
                continue;
            }

            if (distance >= closestDistance || IsSightBlockedBeforeBomb(directionToBomb, distance))
                continue;

            closestDistance = distance;
            foundDirection = directionToBomb;
        }

        return foundDirection != Vector2.zero;
    }

    private bool IsSightBlockedBeforeBomb(Vector2 sightDirection, float bombDistance)
    {
        int cellsBeforeBomb = Mathf.FloorToInt((bombDistance - tileSize * 0.5f) / tileSize);
        for (int cellOffset = 1; cellOffset <= cellsBeforeBomb; cellOffset++)
        {
            Vector2 position = rb.position + sightDirection * tileSize * cellOffset;
            if (HasBlockingTileAt(position))
                return true;
        }

        return false;
    }

    private bool CanCreateDestructibleAt(Vector2 worldPosition)
    {
        ResolveTilemaps();
        ResolveDestructibleTileTemplate();
        if (groundTilemap == null || destructibleTilemap == null || destructibleTileTemplate == null)
            return false;

        Vector3Int cell = groundTilemap.WorldToCell(worldPosition);
        if (!groundTilemap.HasTile(cell) || destructibleTilemap.HasTile(cell) ||
            (indestructibleTilemap != null && indestructibleTilemap.HasTile(cell)))
            return false;

        Vector2 size = Vector2.one * (tileSize * 0.8f);
        foreach (Collider2D hit in Physics2D.OverlapBoxAll(worldPosition, size, 0f))
        {
            if (hit == null || hit.transform.IsChildOf(transform))
                continue;

            if (hit.GetComponentInParent<Bomb>() != null ||
                hit.GetComponentInParent<EnemyMovementController>() != null ||
                hit.gameObject.layer == LayerMask.NameToLayer("Stage"))
                return false;
        }

        return true;
    }

    private bool HasBlockingTileAt(Vector2 worldPosition)
    {
        if (destructibleTilemap != null && destructibleTilemap.HasTile(destructibleTilemap.WorldToCell(worldPosition)))
            return true;

        return indestructibleTilemap != null &&
            indestructibleTilemap.HasTile(indestructibleTilemap.WorldToCell(worldPosition));
    }

    private void BeginSnowPreparation()
    {
        preparingSnow = true;
        isStuck = false;
        targetTile = rb.position;
        direction = pendingSnowDirection;
        rb.linearVelocity = Vector2.zero;
        DisableWalkingSpritesForSnow();

        ShowAttackSnowSprite(direction);

        if (snowEffectSprite != null)
        {
            // AnimatedSpriteRenderer reapplies its local base every animation
            // frame. Set that base instead of assigning world position, so the
            // Snow stays on the tile being created for the whole loop.
            Vector3 snowTileLocalPosition = snowEffectLocalPosition +
                (Vector3)(direction * tileSize);
            snowEffectSprite.SetExternalBaseLocalPosition(snowTileLocalPosition);
            SetVisualEnabled(snowEffectSprite, true);
            snowEffectSprite.idle = false;
            snowEffectSprite.loop = true;
            snowEffectSprite.CurrentFrame = 0;
            snowEffectSprite.RefreshFrame();
        }
        snowStartedAt = Time.time;
    }

    private void UpdateSnowPreparation()
    {
        if (isDead)
        {
            EndSnowPreparation();
            return;
        }

        rb.linearVelocity = Vector2.zero;
        if (GamePauseController.IsPaused || Time.time - snowStartedAt < SnowDuration)
            return;

        // rb was snapped before this phase begins, so this is always the tile
        // immediately ahead, never the tile occupied by Pengo itself.
        Vector2 targetPosition = GetNextTilePosition(pendingSnowDirection);
        bool createdDestructible = false;
        if (CanCreateDestructibleAt(targetPosition))
        {
            Vector3Int cell = destructibleTilemap.WorldToCell(targetPosition);
            destructibleTilemap.SetTile(cell, destructibleTileTemplate);
            destructibleTilemap.RefreshTile(cell);
            createdDestructible = true;
        }

        EndSnowPreparation();
        if (createdDestructible)
        {
            ChooseDirectionAfterCreatingTile(pendingSnowDirection);
            return;
        }

        base.DecideNextTile();
    }

    private void EndSnowPreparation()
    {
        preparingSnow = false;
        DisableAttackSnowSprites();
        activeAttackSnowSprite = null;

        if (snowEffectSprite == null)
            return;

        snowEffectSprite.ClearExternalBase();
        SetVisualEnabled(snowEffectSprite, false);
        snowEffectSprite.transform.localPosition = snowEffectLocalPosition;
    }

    private void ResolveTilemaps()
    {
        GameManager gameManager = GameManager.Instance;
        if (gameManager == null)
            return;

        groundTilemap ??= gameManager.groundTilemap;
        destructibleTilemap ??= gameManager.destructibleTilemap;
        indestructibleTilemap ??= gameManager.indestructibleTilemap;
    }

    private void DisableWalkingSpritesForSnow()
    {
        SetVisualEnabled(spriteUp, false);
        SetVisualEnabled(spriteDown, false);
        SetVisualEnabled(spriteLeft, false);
        SetVisualEnabled(spriteRight, false);
        activeSprite = null;
    }

    private Vector2 GetNextTilePosition(Vector2 snowDirection)
    {
        return rb.position + snowDirection * tileSize;
    }

    private void ChooseDirectionAfterCreatingTile(Vector2 blockedDirection)
    {
        var availableDirections = new List<Vector2>(Dirs.Length - 1);
        foreach (Vector2 candidate in Dirs)
        {
            if (candidate == blockedDirection || IsTileBlocked(rb.position + candidate * tileSize))
                continue;

            availableDirections.Add(candidate);
        }

        if (availableDirections.Count == 0)
        {
            targetTile = rb.position;
            isStuck = true;
            stuckTimer = 0f;
            return;
        }

        isStuck = false;
        direction = availableDirections[Random.Range(0, availableDirections.Count)];
        UpdateSpriteDirection(direction);
        targetTile = rb.position + direction * tileSize;
    }

    private void ResolveDestructibleTileTemplate()
    {
        if (destructibleTileTemplate != null || destructibleTilemap == null)
            return;

        BoundsInt bounds = destructibleTilemap.cellBounds;
        foreach (Vector3Int cell in bounds.allPositionsWithin)
        {
            TileBase tile = destructibleTilemap.GetTile(cell);
            if (tile == null)
                continue;

            destructibleTileTemplate = tile;
            return;
        }
    }

    private void CacheAttackSnowSprite(string childName, Vector2 snowDirection)
    {
        Transform child = transform.Find(childName);
        if (child == null || !child.TryGetComponent(out AnimatedSpriteRenderer snowSprite))
            return;

        attackSnowSprites[snowDirection] = snowSprite;
        snowSprite.enabled = false;
    }

    private void CacheSnowEffectSprite()
    {
        Transform child = transform.Find("Snow");
        if (child == null || !child.TryGetComponent(out snowEffectSprite))
            return;

        snowEffectLocalPosition = child.localPosition;
        SetVisualEnabled(snowEffectSprite, false);
    }

    private void ShowAttackSnowSprite(Vector2 attackDirection)
    {
        DisableAttackSnowSprites();
        Vector2 spriteDirection = attackDirection == Vector2.right ? Vector2.left : attackDirection;
        if (!attackSnowSprites.TryGetValue(spriteDirection, out AnimatedSpriteRenderer snowSprite) || snowSprite == null)
            return;

        activeAttackSnowSprite = snowSprite;
        SetVisualEnabled(activeAttackSnowSprite, true);
        activeAttackSnowSprite.idle = false;
        activeAttackSnowSprite.loop = true;
        activeAttackSnowSprite.CurrentFrame = 0;
        activeAttackSnowSprite.RefreshFrame();

        if (activeAttackSnowSprite.TryGetComponent(out SpriteRenderer spriteRenderer))
            spriteRenderer.flipX = attackDirection == Vector2.right;
    }

    private void DisableAttackSnowSprites()
    {
        foreach (AnimatedSpriteRenderer attackSprite in attackSnowSprites.Values)
            SetVisualEnabled(attackSprite, false);
    }

    private static void SetVisualEnabled(AnimatedSpriteRenderer sprite, bool enabled)
    {
        if (sprite == null)
            return;

        sprite.enabled = enabled;
        if (sprite.TryGetComponent(out SpriteRenderer spriteRenderer))
            spriteRenderer.enabled = enabled;
    }

}

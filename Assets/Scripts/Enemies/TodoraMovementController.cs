using UnityEngine;

/// <summary>
/// A junction-turning enemy that triples its speed and switches to its charge
/// animation after it sees a player in an unobstructed cardinal direction.
/// The charge continues through the player until a valid charge blocker stops it.
/// </summary>
public sealed class TodoraMovementController : JunctionTurningEnemyMovementController
{
    private const string DestructiblesTag = "Destructibles";
    private const string IndestructiblesTag = "Indestructibles";
    private const string CoreMechanismsName = "CoreMechanisms";

    [Header("Player Pursuit")]
    [SerializeField, Min(0.1f)] private float visionDistance = 10f;
    [SerializeField] private LayerMask playerLayerMask;
    [SerializeField, Min(0.001f)] private float alignedToleranceTiles = 0.15f;
    [SerializeField, Range(0.1f, 1f)] private float scanBoxSizePercent = 0.6f;
    [SerializeField, Min(1f)] private float pursuitSpeedMultiplier = 3f;

    [Header("Charge Sprites")]
    [SerializeField] private AnimatedSpriteRenderer chargeUp;
    [SerializeField] private AnimatedSpriteRenderer chargeDown;
    [SerializeField] private AnimatedSpriteRenderer chargeLeft;
    [SerializeField] private AnimatedSpriteRenderer chargeRight;

    [Header("SFX")]
    [SerializeField] private AudioClip chargeSfx;
    [SerializeField, Range(0f, 1f)] private float chargeSfxVolume = 1f;

    private float patrolSpeed;
    private bool isPursuing;
    private AudioSource chargeAudioSource;

    protected override void Awake()
    {
        base.Awake();
        patrolSpeed = speed;
        chargeAudioSource = GetComponent<AudioSource>();

        if (playerLayerMask.value == 0)
            playerLayerMask = LayerMask.GetMask("Player");

        DisableTodoraSprites();
    }

    protected override void FixedUpdate()
    {
        if (!isPursuing && TryGetPlayerDirection(out Vector2 playerDirection))
            StartCharge(playerDirection);

        if (isPursuing && HasBombAt(targetTile))
            EndCharge();
        else if (isPursuing && IsTileBlocked(targetTile))
            EndCharge();

        base.FixedUpdate();
    }

    protected override void DecideNextTile()
    {
        if (isPursuing)
        {
            Vector2 forwardTile = rb.position + direction * tileSize;
            if (IsTileBlocked(forwardTile))
            {
                EndCharge();
                base.DecideNextTile();
                return;
            }

            targetTile = forwardTile;
            return;
        }

        if (TryGetPlayerDirection(out Vector2 playerDirection))
        {
            StartCharge(playerDirection);
            return;
        }

        base.DecideNextTile();
    }

    protected override void UpdateSpriteDirection(Vector2 dir)
    {
        if (isDead || isInDamagedLoop)
            return;

        AnimatedSpriteRenderer selected = isPursuing
            ? GetChargeSprite(dir)
            : GetWalkingSprite(dir);

        if (selected == null)
            return;

        if (activeSprite == selected && selected.enabled)
        {
            selected.idle = false;
            return;
        }

        int frame = activeSprite != null ? activeSprite.CurrentFrame : 0;
        DisableTodoraSprites();

        activeSprite = selected;
        activeSprite.enabled = true;
        activeSprite.idle = false;
        activeSprite.loop = true;

        if (selected.animationSprite != null && selected.animationSprite.Length > 0)
        {
            selected.CurrentFrame = Mathf.Clamp(frame, 0, selected.animationSprite.Length - 1);
            selected.RefreshFrame();
        }
    }

    protected override void Die()
    {
        isPursuing = false;
        speed = patrolSpeed;
        DisableTodoraSprites();
        base.Die();
    }

    private void StartCharge(Vector2 chargeDirection)
    {
        if (isPursuing)
            return;

        isPursuing = true;
        speed = patrolSpeed * pursuitSpeedMultiplier;
        direction = chargeDirection;
        targetTile = rb.position + direction * tileSize;
        GameAudioSettings.PlaySfx(chargeAudioSource, chargeSfx, chargeSfxVolume);
        UpdateSpriteDirection(direction);
    }

    private void EndCharge()
    {
        if (!isPursuing)
            return;

        isPursuing = false;
        speed = patrolSpeed;
        UpdateSpriteDirection(direction);
    }

    private bool TryGetPlayerDirection(out Vector2 directionToPlayer)
    {
        directionToPlayer = Vector2.zero;

        int maxSteps = Mathf.Max(1, Mathf.FloorToInt(visionDistance / tileSize));
        float boxSize = Mathf.Clamp(scanBoxSizePercent, 0.1f, 1f) * tileSize;
        float alignmentTolerance = Mathf.Max(0.001f, alignedToleranceTiles * tileSize);

        for (int directionIndex = 0; directionIndex < Dirs.Length; directionIndex++)
        {
            Vector2 scanDirection = Dirs[directionIndex];
            bool scansVertically = scanDirection == Vector2.up || scanDirection == Vector2.down;

            for (int step = 1; step <= maxSteps; step++)
            {
                Vector2 tileCenter = rb.position + scanDirection * tileSize * step;

                // Obstacles end the scan before any player behind them can be detected.
                if (IsTileBlocked(tileCenter))
                    break;

                Collider2D[] playerHits = Physics2D.OverlapBoxAll(
                    tileCenter, Vector2.one * boxSize, 0f, playerLayerMask);

                for (int hitIndex = 0; hitIndex < playerHits.Length; hitIndex++)
                {
                    Collider2D playerCollider = playerHits[hitIndex];
                    if (playerCollider == null)
                        continue;

                    Vector2 playerPosition = playerCollider.attachedRigidbody != null
                        ? playerCollider.attachedRigidbody.position
                        : (Vector2)playerCollider.transform.position;

                    bool isAligned = scansVertically
                        ? Mathf.Abs(playerPosition.x - rb.position.x) <= alignmentTolerance
                        : Mathf.Abs(playerPosition.y - rb.position.y) <= alignmentTolerance;

                    if (!isAligned)
                        continue;

                    directionToPlayer = scanDirection;
                    return true;
                }
            }
        }

        return false;
    }

    protected override bool IsTileBlocked(Vector2 tileCenter)
    {
        if (!isPursuing)
            return base.IsTileBlocked(tileCenter);

        Collider2D[] hits = Physics2D.OverlapBoxAll(
            tileCenter,
            Vector2.one * (tileSize * 0.8f),
            0f,
            obstacleMask);

        int bombLayer = LayerMask.NameToLayer("Bomb");
        for (int hitIndex = 0; hitIndex < hits.Length; hitIndex++)
        {
            Collider2D hit = hits[hitIndex];
            if (hit == null || hit.gameObject == gameObject)
                continue;

            int hitLayer = hit.gameObject.layer;
            if (hitLayer == bombLayer || IsChargeBlocker(hit))
                return true;
        }

        return false;
    }

    private static bool IsChargeBlocker(Collider2D collider)
    {
        // Use the serialized tag text rather than CompareTag: legacy stage
        // scenes contain Indestructibles even though it is not registered in
        // TagManager, and CompareTag would throw for that legacy tag.
        string tag = collider.tag;
        return tag == DestructiblesTag ||
               tag == IndestructiblesTag ||
               collider.name.StartsWith(CoreMechanismsName, System.StringComparison.Ordinal);
    }

    protected override void OnTriggerEnter2D(Collider2D other)
    {
        if (isPursuing && other != null)
        {
            int otherLayer = other.gameObject.layer;
            int playerLayer = LayerMask.NameToLayer("Player");
            int enemyLayer = LayerMask.NameToLayer("Enemy");

            if (otherLayer == playerLayer || otherLayer == enemyLayer)
                return;

            if (otherLayer == LayerMask.NameToLayer("Bomb"))
                EndCharge();
        }

        base.OnTriggerEnter2D(other);
    }

    private AnimatedSpriteRenderer GetWalkingSprite(Vector2 dir)
    {
        if (dir == Vector2.up) return spriteUp;
        if (dir == Vector2.down) return spriteDown;
        if (dir == Vector2.left) return spriteLeft;
        if (dir == Vector2.right) return spriteRight != null ? spriteRight : spriteLeft;
        return spriteDown;
    }

    private AnimatedSpriteRenderer GetChargeSprite(Vector2 dir)
    {
        if (dir == Vector2.up) return chargeUp;
        if (dir == Vector2.down) return chargeDown;
        if (dir == Vector2.left) return chargeLeft;
        if (dir == Vector2.right) return chargeRight != null ? chargeRight : chargeLeft;
        return chargeDown;
    }

    private void DisableTodoraSprites()
    {
        SetSpriteEnabled(spriteUp, false);
        SetSpriteEnabled(spriteDown, false);
        SetSpriteEnabled(spriteLeft, false);
        SetSpriteEnabled(spriteRight, false);
        SetSpriteEnabled(chargeUp, false);
        SetSpriteEnabled(chargeDown, false);
        SetSpriteEnabled(chargeLeft, false);
        SetSpriteEnabled(chargeRight, false);
    }

    private static void SetSpriteEnabled(AnimatedSpriteRenderer sprite, bool enabled)
    {
        if (sprite != null)
            sprite.enabled = enabled;
    }

}

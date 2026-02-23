using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class JunctionPursuitCrazyMovementController : JunctionTurningEnemyMovementController
{
    [Header("Single Move Sprite (All Directions)")]
    [SerializeField] private AnimatedSpriteRenderer moveSprite;

    [SerializeField] private string destructiblesTag = "Destructibles";

    [SerializeField, Min(0.1f)] private float visionDistance = 10f;
    [SerializeField] private LayerMask playerLayerMask;

    [SerializeField] private LayerMask stageLayerMask;

    [SerializeField, Min(0.001f)] private float alignedToleranceTiles = 0.15f;
    [SerializeField, Range(0.1f, 1f)] private float scanBoxSizePercent = 0.6f;

    private Collider2D _selfCollider;
    private SpriteRenderer _moveSr;

    protected override void Awake()
    {
        base.Awake();

        _selfCollider = GetComponent<Collider2D>();

        if (!string.IsNullOrEmpty(destructiblesTag))
        {
            GameObject destructibles = GameObject.FindGameObjectWithTag(destructiblesTag);
            if (destructibles != null)
            {
                var cols = destructibles.GetComponents<Collider2D>();
                for (int i = 0; i < cols.Length; i++)
                {
                    var col = cols[i];
                    if (col == null) continue;
                    Physics2D.IgnoreCollision(_selfCollider, col);
                }
            }
        }

        if (playerLayerMask.value == 0)
            playerLayerMask = LayerMask.GetMask("Player");

        if (stageLayerMask.value == 0)
            stageLayerMask = LayerMask.GetMask("Stage");

        if (moveSprite != null)
            _moveSr = moveSprite.GetComponent<SpriteRenderer>();

        DisableMoveSpriteHard();
    }

    protected override void Start()
    {
        base.Start();

        if (!isDead && !isInDamagedLoop)
            EnsureMoveSpriteVisibleAndSynced(direction);
    }

    protected override void FixedUpdate()
    {
        if (isDead || isInDamagedLoop)
        {
            if (rb != null)
                rb.linearVelocity = Vector2.zero;
            return;
        }

        base.FixedUpdate();
    }

    protected override void DecideNextTile()
    {
        if (isDead || isInDamagedLoop)
            return;

        if (TryGetPlayerDirection(out Vector2 dirToPlayer))
        {
            Vector2 forwardTile = rb.position + dirToPlayer * tileSize;

            if (!IsTileBlocked(forwardTile))
            {
                direction = dirToPlayer;
                UpdateSpriteDirection(direction);
                targetTile = forwardTile;
                return;
            }
        }

        base.DecideNextTile();
    }

    protected override void UpdateSpriteDirection(Vector2 dir)
    {
        if (isDead || isInDamagedLoop)
            return;

        EnsureMoveSpriteVisibleAndSynced(dir);
    }

    protected override void OnHitInvulnerabilityStarted(float seconds)
    {
        base.OnHitInvulnerabilityStarted(seconds);

        if (!isInDamagedLoop || isDead)
            return;

        DisableMoveSpriteHard();

        if (spriteDamaged != null)
        {
            activeSprite = spriteDamaged;
            spriteDamaged.enabled = true;
            spriteDamaged.idle = false;
            spriteDamaged.loop = true;
        }
    }

    protected override void OnHitInvulnerabilityEnded()
    {
        base.OnHitInvulnerabilityEnded();

        if (isDead)
            return;

        if (isInDamagedLoop)
            return;

        if (spriteDamaged != null)
            spriteDamaged.enabled = false;

        EnsureMoveSpriteVisibleAndSynced(direction);
    }

    private void EnsureMoveSpriteVisibleAndSynced(Vector2 dir)
    {
        if (moveSprite == null)
            return;

        int frame = moveSprite.CurrentFrame;

        if (!moveSprite.enabled)
            moveSprite.enabled = true;

        var sr = _moveSr != null ? _moveSr : moveSprite.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.enabled = true;

            if (moveSprite.allowFlipX)
            {
                if (dir == Vector2.right) sr.flipX = true;
                else if (dir == Vector2.left) sr.flipX = false;
                else sr.flipX = false;
            }
        }

        moveSprite.idle = false;

        moveSprite.CurrentFrame = frame;
        moveSprite.RefreshFrame();

        activeSprite = moveSprite;
    }

    protected override void Die()
    {
        if (isDead)
            return;

        isDead = true;
        isStuck = false;
        isInDamagedLoop = false;

        if (TryGetComponent<StunReceiver>(out var stun))
            stun.CancelStunForDeath();

        var audioSource = GetComponent<AudioSource>();
        if (audioSource != null && deathSfx != null)
            audioSource.PlayOneShot(deathSfx);

        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        if (TryGetComponent<Collider2D>(out var col))
            col.enabled = false;

        if (spriteDamaged != null)
            spriteDamaged.enabled = false;

        DisableMoveSpriteHard();

        if (spriteDeath != null)
        {
            activeSprite = spriteDeath;
            spriteDeath.enabled = true;
            spriteDeath.idle = false;
            spriteDeath.animationTime = 0.1f;
            spriteDeath.loop = false;

            var sr = spriteDeath.GetComponent<SpriteRenderer>();
            if (sr != null) sr.enabled = true;
        }

        var gameManager = FindFirstObjectByType<GameManager>();
        if (gameManager != null)
            gameManager.NotifyEnemyDied();

        Invoke(nameof(OnDeathAnimationEnded), 0.7f);
    }

    private void DisableMoveSpriteHard()
    {
        if (moveSprite == null)
            return;

        moveSprite.enabled = false;

        var sr = _moveSr != null ? _moveSr : moveSprite.GetComponent<SpriteRenderer>();
        if (sr != null) sr.enabled = false;
    }

    private bool TryGetPlayerDirection(out Vector2 dirToPlayer)
    {
        dirToPlayer = Vector2.zero;

        Vector2[] dirs = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };

        int maxSteps = Mathf.Max(1, Mathf.FloorToInt(visionDistance / tileSize));

        float boxPercent = Mathf.Clamp(scanBoxSizePercent, 0.1f, 1f);
        Vector2 boxSize = Vector2.one * (tileSize * boxPercent);

        float alignedToleranceWorld = Mathf.Max(0.001f, alignedToleranceTiles * tileSize);

        Vector2 selfPos = rb.position;

        for (int i = 0; i < dirs.Length; i++)
        {
            Vector2 dir = dirs[i];
            bool verticalScan = (dir == Vector2.up || dir == Vector2.down);

            for (int step = 1; step <= maxSteps; step++)
            {
                Vector2 tileCenter = selfPos + step * tileSize * dir;

                var hits = Physics2D.OverlapBoxAll(tileCenter, boxSize, 0f, playerLayerMask);
                if (hits != null && hits.Length > 0)
                {
                    for (int h = 0; h < hits.Length; h++)
                    {
                        var col = hits[h];
                        if (col == null) continue;

                        Vector2 p = col.attachedRigidbody != null
                            ? col.attachedRigidbody.position
                            : (Vector2)col.transform.position;

                        bool aligned = verticalScan
                            ? Mathf.Abs(p.x - selfPos.x) <= alignedToleranceWorld
                            : Mathf.Abs(p.y - selfPos.y) <= alignedToleranceWorld;

                        if (!aligned)
                            continue;

                        if (IsPlayerStandingOnDestructibles(p))
                            continue;

                        dirToPlayer = dir;
                        return true;
                    }
                }

                if (IsTileBlocked(tileCenter))
                    break;
            }
        }

        return false;
    }

    private bool IsPlayerStandingOnDestructibles(Vector2 playerWorldPos)
    {
        if (stageLayerMask.value == 0)
            return false;

        Vector2 tileCenter = GetTileCenter(playerWorldPos);
        Vector2 size = Vector2.one * (tileSize * 0.8f);

        var hits = Physics2D.OverlapBoxAll(tileCenter, size, 0f, stageLayerMask);
        if (hits == null || hits.Length == 0)
            return false;

        for (int i = 0; i < hits.Length; i++)
        {
            var h = hits[i];
            if (h == null) continue;

            if (!string.IsNullOrEmpty(destructiblesTag) && h.CompareTag(destructiblesTag))
                return true;
        }

        return false;
    }

    private Vector2 GetTileCenter(Vector2 worldPos)
    {
        float x = Mathf.Round(worldPos.x / tileSize) * tileSize;
        float y = Mathf.Round(worldPos.y / tileSize) * tileSize;
        return new Vector2(x, y);
    }

    protected override bool IsTileBlocked(Vector2 tileCenter)
    {
        Vector2 size = Vector2.one * (tileSize * 0.8f);
        Collider2D[] hits = Physics2D.OverlapBoxAll(tileCenter, size, 0f, obstacleMask);

        if (hits == null || hits.Length == 0)
            return false;

        for (int i = 0; i < hits.Length; i++)
        {
            var hit = hits[i];
            if (hit == null) continue;
            if (hit.gameObject == gameObject) continue;

            if (!string.IsNullOrEmpty(destructiblesTag) && hit.CompareTag(destructiblesTag))
                continue;

            return true;
        }

        return false;
    }
}
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(CharacterHealth))]
[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(StunReceiver))]
public sealed class LightSensitiveEnemyMovementController : JunctionTurningEnemyMovementController
{
    [Header("Energized Mode")]
    [SerializeField] private bool energizedWhenLightOn = true;

    [Header("Sprites (Normal)")]
    [SerializeField] private AnimatedSpriteRenderer spriteUpNormal;
    [SerializeField] private AnimatedSpriteRenderer spriteDownNormal;
    [SerializeField] private AnimatedSpriteRenderer spriteLeftNormal;
    [SerializeField] private AnimatedSpriteRenderer spriteRightNormal;

    [Header("Sprites (Normal Damaged)")]
    [SerializeField] private AnimatedSpriteRenderer spriteDamagedNormal;

    [Header("Sprites (Energized)")]
    [SerializeField] private AnimatedSpriteRenderer spriteUpEnergized;
    [SerializeField] private AnimatedSpriteRenderer spriteDownEnergized;
    [SerializeField] private AnimatedSpriteRenderer spriteLeftEnergized;
    [SerializeField] private AnimatedSpriteRenderer spriteRightEnergized;

    [Header("Sprites (Energized Damaged)")]
    [SerializeField] private AnimatedSpriteRenderer spriteDamagedEnergized;

    [Header("Persecuting (Vision)")]
    [SerializeField, Min(0.1f)] private float visionDistance = 10f;
    [SerializeField] private LayerMask playerLayerMask;

    [Header("Vision Block (Under Player)")]
    [SerializeField] private LayerMask stageLayerMask;
    [SerializeField] private string destructiblesTag = "Destructibles";

    [Header("Vision Alignment")]
    [SerializeField, Min(0.001f)] private float alignedToleranceTiles = 0.15f;
    [SerializeField, Range(0.1f, 1f)] private float scanBoxSizePercent = 0.6f;

    [Header("StageBlackout Speed Boost")]
    [SerializeField, Min(1f)] private float blackoutSpeedMultiplier = 3f;

    private float _baseSpeed;
    private bool _cachedEnergized;
    private bool _cachedBlackoutActive;

    private CharacterHealth _health;

    private static FieldInfo _fiActive;
    private static FieldInfo _fiBlackoutImage;

    void OnEnable()
    {
        DisableAllVisualsHard();
    }

    protected override void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        _health = GetComponent<CharacterHealth>();

        if (obstacleMask.value == 0)
            obstacleMask = LayerMask.GetMask("Bomb", "Stage", "Enemy");

        if (bombLayerMask.value == 0)
            bombLayerMask = LayerMask.GetMask("Bomb");

        if (enemyLayerMask.value == 0)
            enemyLayerMask = LayerMask.GetMask("Enemy");

        if (playerLayerMask.value == 0)
            playerLayerMask = LayerMask.GetMask("Player");

        if (stageLayerMask.value == 0)
            stageLayerMask = LayerMask.GetMask("Stage");

        var audioSource = GetComponent<AudioSource>();
        if (audioSource != null)
        {
            audioSource.playOnAwake = false;
            audioSource.loop = false;
        }

        if (_health != null)
        {
            _health.HitInvulnerabilityStarted += OnHitInvulnerabilityStartedInternal;
            _health.HitInvulnerabilityEnded += OnHitInvulnerabilityEndedInternal;
            _health.Died += OnHealthDiedInternal;
        }

        _baseSpeed = speed;
        DisableAllVisualsHard();
    }

    protected override void OnDestroy()
    {
        if (_health != null)
        {
            _health.HitInvulnerabilityStarted -= OnHitInvulnerabilityStartedInternal;
            _health.HitInvulnerabilityEnded -= OnHitInvulnerabilityEndedInternal;
            _health.Died -= OnHealthDiedInternal;
        }

        base.OnDestroy();
    }

    protected override void Start()
    {
        _baseSpeed = speed;

        SnapToGrid();
        ChooseInitialDirection();

        RefreshBoostFromBlackout();
        UpdateEnemySpriteDirection(direction);

        DecideNextTile();
    }

    protected override void FixedUpdate()
    {
        if (isDead)
            return;

        if (TryGetComponent<StunReceiver>(out var stun) && stun != null && stun.IsStunned)
        {
            if (rb != null)
                rb.linearVelocity = Vector2.zero;
            return;
        }

        RefreshBoostFromBlackout();

        if (isInDamagedLoop)
        {
            if (rb != null)
                rb.linearVelocity = Vector2.zero;
            return;
        }

        if (isStuck)
        {
            HandleStuck();
            return;
        }

        if (HasBombAt(targetTile))
            HandleBombAhead();

        MoveTowardsTile();

        if (ReachedTile())
        {
            SnapToGrid();
            DecideNextTile();
        }
    }

    protected override void OnTriggerEnter2D(Collider2D other)
    {
        if (isDead)
            return;

        int layer = other.gameObject.layer;

        if (layer == LayerMask.NameToLayer("Explosion"))
        {
            if (_health != null)
                _health.TakeDamage(1);
            return;
        }

        if (layer == LayerMask.NameToLayer("Bomb"))
        {
            HandleBombCollisionOnContact();
            return;
        }

        if (layer == LayerMask.NameToLayer("Enemy"))
        {
            var otherEnemy = other.GetComponent<EnemyMovementController>();
            HandleEnemyCollision(otherEnemy);
            return;
        }
    }

    protected override void DecideNextTile()
    {
        if (TryGetPlayerDirection(out Vector2 playerDir))
        {
            Vector2 forwardTile = rb.position + playerDir * tileSize;

            if (!IsTileBlocked(forwardTile))
            {
                direction = playerDir;
                UpdateEnemySpriteDirection(direction);
                targetTile = forwardTile;
                return;
            }
        }

        base.DecideNextTile();
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
            var dir = dirs[i];
            bool verticalScan = dir == Vector2.up || dir == Vector2.down;

            for (int step = 1; step <= maxSteps; step++)
            {
                Vector2 tileCenter = selfPos + step * tileSize * dir;

                var hits = Physics2D.OverlapBoxAll(tileCenter, boxSize, 0f, playerLayerMask);
                if (hits != null && hits.Length > 0)
                {
                    for (int h = 0; h < hits.Length; h++)
                    {
                        var col = hits[h];
                        if (col == null)
                            continue;

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
            if (h == null)
                continue;

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

    private void RefreshBoostFromBlackout()
    {
        bool blackoutActive = StageBlackout.Instance != null && IsStageBlackoutActive(StageBlackout.Instance);

        if (blackoutActive != _cachedBlackoutActive)
        {
            _cachedBlackoutActive = blackoutActive;

            bool energizedNow = energizedWhenLightOn ? !_cachedBlackoutActive : _cachedBlackoutActive;

            speed = energizedNow ? (_baseSpeed * Mathf.Max(1f, blackoutSpeedMultiplier)) : _baseSpeed;
            SetEnergized(energizedNow);
        }
    }

    private static bool IsStageBlackoutActive(StageBlackout b)
    {
        if (b == null)
            return false;

        if (_fiActive == null)
            _fiActive = typeof(StageBlackout).GetField("_active", BindingFlags.Instance | BindingFlags.NonPublic);

        if (_fiActive != null)
        {
            object v = _fiActive.GetValue(b);
            if (v is bool bb)
                return bb;
        }

        if (_fiBlackoutImage == null)
            _fiBlackoutImage = typeof(StageBlackout).GetField("blackoutImage", BindingFlags.Instance | BindingFlags.NonPublic);

        if (_fiBlackoutImage != null)
        {
            var img = _fiBlackoutImage.GetValue(b) as Image;
            if (img != null)
                return img.gameObject.activeInHierarchy;
        }

        return false;
    }

    private void SetEnergized(bool energized)
    {
        if (_cachedEnergized == energized)
            return;

        _cachedEnergized = energized;

        if (isInDamagedLoop)
        {
            DisableAllVisualsHard();
            var dmg = _cachedEnergized ? spriteDamagedEnergized : spriteDamagedNormal;
            EnableOnly(dmg);
            return;
        }

        UpdateEnemySpriteDirection(direction);
    }

    private void UpdateEnemySpriteDirection(Vector2 dir)
    {
        if (isInDamagedLoop || isDead)
            return;

        DisableAllVisualsHard();

        AnimatedSpriteRenderer next = GetDirectionalSprite(dir, _cachedEnergized);
        if (next == null)
        {
            next = spriteDownNormal != null ? spriteDownNormal :
                   spriteLeftNormal != null ? spriteLeftNormal :
                   spriteRightNormal != null ? spriteRightNormal :
                   spriteUpNormal != null ? spriteUpNormal :
                   null;
        }

        EnableOnly(next);
    }

    private AnimatedSpriteRenderer GetDirectionalSprite(Vector2 dir, bool energized)
    {
        if (energized)
        {
            if (dir == Vector2.up) return spriteUpEnergized;
            if (dir == Vector2.down) return spriteDownEnergized;
            if (dir == Vector2.left) return spriteLeftEnergized;
            if (dir == Vector2.right) return spriteRightEnergized;
        }
        else
        {
            if (dir == Vector2.up) return spriteUpNormal;
            if (dir == Vector2.down) return spriteDownNormal;
            if (dir == Vector2.left) return spriteLeftNormal;
            if (dir == Vector2.right) return spriteRightNormal;
        }

        return null;
    }

    private void EnableOnly(AnimatedSpriteRenderer asr)
    {
        if (asr == null)
            return;

        activeSprite = asr;
        asr.enabled = true;
        asr.idle = false;
    }

    private void DisableAllVisualsHard()
    {
        DisableASR(spriteUpNormal);
        DisableASR(spriteDownNormal);
        DisableASR(spriteLeftNormal);
        DisableASR(spriteRightNormal);

        DisableASR(spriteUpEnergized);
        DisableASR(spriteDownEnergized);
        DisableASR(spriteLeftEnergized);
        DisableASR(spriteRightEnergized);

        DisableASR(spriteDamagedNormal);
        DisableASR(spriteDamagedEnergized);

        if (spriteDeath != null)
            DisableASR(spriteDeath);

        var srs = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < srs.Length; i++)
        {
            var sr = srs[i];
            if (sr == null) continue;
            sr.enabled = false;
        }
    }

    private static void DisableASR(AnimatedSpriteRenderer asr)
    {
        if (asr == null) return;
        asr.enabled = false;

        var sr = asr.GetComponent<SpriteRenderer>();
        if (sr != null) sr.enabled = false;
    }

    private void OnHitInvulnerabilityStartedInternal(float seconds)
    {
        if (_health == null || !_health.playDamagedLoopInsteadOfBlink || isDead)
            return;

        AnimatedSpriteRenderer dmg = _cachedEnergized ? spriteDamagedEnergized : spriteDamagedNormal;
        if (dmg == null)
            return;

        isInDamagedLoop = true;
        isStuck = false;

        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        DisableAllVisualsHard();
        EnableOnly(dmg);
        dmg.loop = true;
    }

    private void OnHitInvulnerabilityEndedInternal()
    {
        if (!isInDamagedLoop || isDead)
            return;

        isInDamagedLoop = false;

        UpdateEnemySpriteDirection(direction);
        DecideNextTile();
    }

    private void OnHealthDiedInternal()
    {
        isInDamagedLoop = false;
        isStuck = false;
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

        DisableAllVisualsHard();

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

    protected override void UpdateSpriteDirection(Vector2 dir)
    {
        UpdateEnemySpriteDirection(dir);
    }
}
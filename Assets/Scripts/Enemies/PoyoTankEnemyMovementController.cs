using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(CharacterHealth))]
[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(StunReceiver))]
public sealed class PoyoTankEnemyMovementController : JunctionTurningEnemyMovementController
{
    [Header("Vision")]
    [SerializeField, Min(0.1f)] private float visionDistance = 10f;
    [SerializeField] private LayerMask playerLayerMask;

    [Header("Vision Alignment")]
    [SerializeField, Min(0.001f)] private float alignedToleranceTiles = 0.15f;
    [SerializeField, Range(0.1f, 1f)] private float scanBoxSizePercent = 0.6f;

    [Header("Shooting")]
    [SerializeField] private bool stopAndShoot = true;
    [SerializeField, Min(0.01f)] private float shotCooldownSeconds = 5f;
    [SerializeField, Min(0.01f)] private float shotWindupSeconds = 0.12f;
    [SerializeField, Min(0.01f)] private float shotAnimSeconds = 0.25f;

    [Header("Shot SFX")]
    [SerializeField] private AudioClip shotSfx;

    [Header("Projectile")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField, Min(0f)] private float projectileSpeed = 8f;
    [SerializeField] private Vector2 projectileLocalOffset = new(0f, 0.05f);
    [SerializeField] private LayerMask projectileHitMask;

    [Header("Shot Sprites (4 directions)")]
    [SerializeField] private AnimatedSpriteRenderer shotUpSprite;
    [SerializeField] private AnimatedSpriteRenderer shotDownSprite;
    [SerializeField] private AnimatedSpriteRenderer shotLeftSprite;
    [SerializeField] private AnimatedSpriteRenderer shotRightSprite;
    [SerializeField] private bool forceFlipXFalse = true;

    private Coroutine _shootRoutine;
    private float _cooldownTimer;
    private bool _isShooting;
    private Vector2 _shootDir;
    private AnimatedSpriteRenderer _activeShotSprite;
    private AudioSource _audioSource;

    protected override void Awake()
    {
        base.Awake();

        _audioSource = GetComponent<AudioSource>();

        if (playerLayerMask.value == 0)
            playerLayerMask = LayerMask.GetMask("Player");

        if (projectileHitMask.value == 0)
            projectileHitMask = LayerMask.GetMask("Bomb", "Stage", "Enemy", "Player", "Water", "Explosion");

        DisableAllShotSprites();
    }

    protected override void FixedUpdate()
    {
        if (isDead)
            return;

        if (TryGetComponent<StunReceiver>(out var stun) && stun != null && stun.IsStunned)
        {
            StopMotionNow();
            return;
        }

        if (isInDamagedLoop)
        {
            StopMotionNow();
            return;
        }

        if (_cooldownTimer > 0f)
            _cooldownTimer -= Time.fixedDeltaTime;

        if (_isShooting)
        {
            StopMotionNow();
            targetTile = rb.position;
            return;
        }

        if (_cooldownTimer <= 0f && TryGetPlayerDirection(out var dirToPlayer))
        {
            direction = dirToPlayer;
            UpdateSpriteDirection(direction);

            if (stopAndShoot)
                StopMotionNow();

            StartShoot(dirToPlayer);
            return;
        }

        base.FixedUpdate();
    }

    protected override void UpdateSpriteDirection(Vector2 dir)
    {
        if (_isShooting || isDead || isInDamagedLoop)
            return;

        base.UpdateSpriteDirection(dir);
    }

    private void StartShoot(Vector2 dir)
    {
        if (_isShooting || isDead || isInDamagedLoop)
            return;

        if (_cooldownTimer > 0f)
            return;

        _shootDir = dir == Vector2.zero ? Vector2.down : dir.normalized;

        if (_shootRoutine != null)
            StopCoroutine(_shootRoutine);

        _shootRoutine = StartCoroutine(ShootRoutine(_shootDir));
    }

    private IEnumerator ShootRoutine(Vector2 dir)
    {
        _isShooting = true;
        StopMotionNow();

        _activeShotSprite = ResolveShotSprite(dir);

        DisableAllMovementSprites();
        DisableAllShotSprites();

        EnableShotSprite(_activeShotSprite);

        float total = shotAnimSeconds;
        float windup = Mathf.Clamp(shotWindupSeconds, 0.01f, total);

        yield return new WaitForSecondsRealtime(windup);

        if (!isDead && !isInDamagedLoop)
        {
            SpawnProjectile(dir);

            if (shotSfx != null && _audioSource != null)
                _audioSource.PlayOneShot(shotSfx);
        }

        float remain = total - windup;
        if (remain > 0f)
            yield return new WaitForSecondsRealtime(remain);

        DisableAllShotSprites();
        _activeShotSprite = null;

        _cooldownTimer = shotCooldownSeconds;
        _isShooting = false;
        _shootRoutine = null;

        base.UpdateSpriteDirection(direction == Vector2.zero ? Vector2.down : direction);
        DecideNextTile();
    }

    private void StopMotionNow()
    {
        rb.linearVelocity = Vector2.zero;
        targetTile = rb.position;
    }

    private void EnableShotSprite(AnimatedSpriteRenderer chosen)
    {
        if (chosen == null)
            return;

        chosen.loop = true;
        chosen.idle = false;
        chosen.CurrentFrame = 0;
        chosen.enabled = true;
        chosen.RefreshFrame();

        activeSprite = chosen;

        if (forceFlipXFalse)
            ForceNoFlipOn(chosen);
    }

    private void DisableAllMovementSprites()
    {
        if (spriteUp != null) spriteUp.enabled = false;
        if (spriteDown != null) spriteDown.enabled = false;
        if (spriteLeft != null) spriteLeft.enabled = false;

        if (spriteDamaged != null) spriteDamaged.enabled = false;

        if (spriteDeath != null && spriteDeath != activeSprite)
            spriteDeath.enabled = false;
    }

    private AnimatedSpriteRenderer ResolveShotSprite(Vector2 dir)
    {
        if (dir == Vector2.up) return shotUpSprite;
        if (dir == Vector2.down) return shotDownSprite;
        if (dir == Vector2.left) return shotLeftSprite;
        if (dir == Vector2.right) return shotRightSprite;

        return shotDownSprite != null ? shotDownSprite :
               shotLeftSprite != null ? shotLeftSprite :
               shotUpSprite;
    }

    private void DisableAllShotSprites()
    {
        if (shotUpSprite != null) shotUpSprite.enabled = false;
        if (shotDownSprite != null) shotDownSprite.enabled = false;
        if (shotLeftSprite != null) shotLeftSprite.enabled = false;
        if (shotRightSprite != null) shotRightSprite.enabled = false;
    }

    private void ForceNoFlipOn(AnimatedSpriteRenderer r)
    {
        if (r == null)
            return;

        if (r.TryGetComponent<SpriteRenderer>(out var sr))
            sr.flipX = false;
    }

    private void SpawnProjectile(Vector2 dir)
    {
        if (projectilePrefab == null)
            return;

        Vector2 spawn = rb.position + dir * tileSize + projectileLocalOffset;

        var go = Instantiate(projectilePrefab, spawn, Quaternion.identity);
        var proj = go.GetComponent<TankShot>();
        if (proj == null)
            proj = go.AddComponent<TankShot>();

        proj.Init(dir, projectileSpeed, projectileHitMask, gameObject);
    }

    private bool TryGetPlayerDirection(out Vector2 dirToPlayer)
    {
        dirToPlayer = Vector2.zero;

        Vector2[] dirs =
        {
            Vector2.up,
            Vector2.down,
            Vector2.left,
            Vector2.right
        };

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

                        Vector2 p = col.attachedRigidbody != null ? col.attachedRigidbody.position : (Vector2)col.transform.position;

                        bool aligned = verticalScan
                            ? Mathf.Abs(p.x - selfPos.x) <= alignedToleranceWorld
                            : Mathf.Abs(p.y - selfPos.y) <= alignedToleranceWorld;

                        if (aligned)
                        {
                            dirToPlayer = dir;
                            return true;
                        }
                    }
                }

                if (IsTileBlocked(tileCenter))
                    break;
            }
        }

        return false;
    }
}

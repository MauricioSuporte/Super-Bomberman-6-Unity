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

    [Header("Shooting")]
    [SerializeField] private bool stopAndShoot = true;
    [SerializeField, Min(0.01f)] private float shotCooldownSeconds = 1.25f;
    [SerializeField, Min(0.01f)] private float shotWindupSeconds = 0.12f;
    [SerializeField, Min(0.01f)] private float shotAnimSeconds = 0.25f;

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

    protected override void Awake()
    {
        base.Awake();

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
            if (rb != null)
                rb.linearVelocity = Vector2.zero;

            return;
        }

        if (isInDamagedLoop)
        {
            if (rb != null)
                rb.linearVelocity = Vector2.zero;

            return;
        }

        if (_cooldownTimer > 0f)
            _cooldownTimer -= Time.fixedDeltaTime;

        if (_isShooting)
        {
            if (rb != null)
                rb.linearVelocity = Vector2.zero;

            targetTile = rb.position;
            return;
        }

        if (_cooldownTimer <= 0f && TryGetPlayerDirection(out var dirToPlayer))
        {
            direction = dirToPlayer;
            UpdateSpriteDirection(direction);

            if (stopAndShoot && rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                targetTile = rb.position;
            }

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

        if (_shootRoutine != null)
            StopCoroutine(_shootRoutine);

        _shootRoutine = StartCoroutine(ShootRoutine(dir));
    }

    private IEnumerator ShootRoutine(Vector2 dir)
    {
        _isShooting = true;

        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        PlayShotSprite(dir);

        yield return new WaitForSecondsRealtime(Mathf.Max(0.01f, shotWindupSeconds));

        if (!isDead && !isInDamagedLoop)
            SpawnProjectile(dir);

        yield return new WaitForSecondsRealtime(Mathf.Max(0.01f, shotAnimSeconds));

        DisableAllShotSprites();

        _cooldownTimer = Mathf.Max(0.01f, shotCooldownSeconds);
        _isShooting = false;
        _shootRoutine = null;

        UpdateSpriteDirection(direction == Vector2.zero ? Vector2.down : direction);
        DecideNextTile();
    }

    private void PlayShotSprite(Vector2 dir)
    {
        DisableAllShotSprites();

        var chosen = ResolveShotSprite(dir);
        if (chosen == null)
            return;

        chosen.enabled = true;
        chosen.idle = false;
        chosen.loop = false;
        chosen.CurrentFrame = 0;
        chosen.RefreshFrame();

        activeSprite = chosen;
        ForceNoFlipOn(chosen);
    }

    private AnimatedSpriteRenderer ResolveShotSprite(Vector2 dir)
    {
        if (dir == Vector2.up) return shotUpSprite;
        if (dir == Vector2.down) return shotDownSprite;
        if (dir == Vector2.left) return shotLeftSprite;
        if (dir == Vector2.right) return shotRightSprite;

        return shotDownSprite != null ? shotDownSprite : shotLeftSprite != null ? shotLeftSprite : shotUpSprite;
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
        if (!forceFlipXFalse || r == null)
            return;

        if (r.TryGetComponent<SpriteRenderer>(out var sr) && sr != null)
            sr.flipX = false;
    }

    private void SpawnProjectile(Vector2 dir)
    {
        if (projectilePrefab == null || rb == null)
            return;

        Vector2 d = dir == Vector2.zero ? Vector2.down : dir.normalized;
        Vector2 spawn = rb.position + d * tileSize + projectileLocalOffset;

        int expected = LayerMask.GetMask("Player", "Stage", "Bomb", "Enemy", "Water", "Explosion", "Item");
        int current = projectileHitMask.value;

        bool looksWrong = current == 0 || current == LayerMask.GetMask("Explosion");

        if (looksWrong)
            projectileHitMask = expected;

        Debug.Log(
            $"[PoyoTank.Shoot] shooter={name} dir={d} spawn={spawn} maskBefore={current}({MaskToString(current)}) " +
            $"maskAfter={projectileHitMask.value}({MaskToString(projectileHitMask.value)}) looksWrong={looksWrong}"
        );

        var go = Instantiate(projectilePrefab, spawn, Quaternion.identity);
        if (go == null)
            return;

        var proj = go.GetComponent<TankShot>();
        if (proj == null)
            proj = go.AddComponent<TankShot>();

        proj.Init(d, projectileSpeed, projectileHitMask, owner: gameObject);
    }

    private static string MaskToString(int maskValue)
    {
        if (maskValue == 0) return "Nothing";

        string s = "";
        for (int i = 0; i < 32; i++)
        {
            if ((maskValue & (1 << i)) == 0)
                continue;

            string n = LayerMask.LayerToName(i);
            if (string.IsNullOrEmpty(n))
                n = $"Layer{i}";

            if (s.Length > 0) s += ",";
            s += n;
        }
        return s;
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

        int maxSteps = Mathf.Max(1, Mathf.RoundToInt(visionDistance / tileSize));
        Vector2 boxSize = Vector2.one * (tileSize * 0.8f);

        for (int i = 0; i < dirs.Length; i++)
        {
            var dir = dirs[i];

            for (int step = 1; step <= maxSteps; step++)
            {
                Vector2 tileCenter = rb.position + step * tileSize * dir;

                Collider2D playerHit = Physics2D.OverlapBox(tileCenter, boxSize, 0f, playerLayerMask);

                if (playerHit != null)
                {
                    dirToPlayer = dir;
                    return true;
                }

                if (IsTileBlocked(tileCenter))
                    break;
            }
        }

        return false;
    }
}

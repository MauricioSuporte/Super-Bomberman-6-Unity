using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

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

    [Header("Vision Block (Under Player)")]
    [SerializeField] private LayerMask stageLayerMask;
    [SerializeField] private string destructiblesTag = "Destructibles";

    [Header("Vision Alignment")]
    [SerializeField, Min(0.001f)] private float alignedToleranceTiles = 0.15f;
    [SerializeField, Range(0.1f, 1f)] private float scanBoxSizePercent = 0.6f;

    [Header("Shooting")]
    [SerializeField] private bool stopAndShoot = true;
    [SerializeField, Min(0.01f)] private float shotCooldownSeconds = 5f;
    [SerializeField, Min(0.01f)] private float shotWindupSeconds = 0.12f;
    [SerializeField, Min(0.01f)] private float shotAnimSeconds = 0.25f;

    [Header("Cooldown Tint (PoyoTank Sprites)")]
    [SerializeField] private bool tintOnCooldown = true;
    [SerializeField] private Color cooldownTintColor = new(8f, 0.2f, 0.2f, 1f);

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

    [Header("Defeat -> Spawn Poyo")]
    [SerializeField] private GameObject poyoPrefab;
    [SerializeField, Min(0.01f)] private float damagedSecondsBeforeSpawn = 0.5f;
    [SerializeField, Min(0.01f)] private float poyoLaunchSeconds = 0.5f;
    [SerializeField, Min(0f)] private float poyoArcHeightTiles = 3f;
    [SerializeField, Min(0)] private int poyoMinLandingDistanceTiles = 10;
    [SerializeField, Min(0)] private int poyoMaxLandingDistanceTiles = 15;
    [SerializeField] private bool notifyGameManagerOnDefeat = true;

    [Header("Defeat -> Drop World Mount (Tank)")]
    [SerializeField] private GameObject tankMountPrefab;
    [SerializeField] private bool dropTankMountOnDefeat = true;

    [Header("Respawn / Ground")]
    [SerializeField] private Tilemap groundTilemapOverride;
    [SerializeField, Min(1)] private int maxRespawnAttempts = 200;
    [SerializeField, Min(0.05f)] private float overlapBoxSize = 0.8f;
    [SerializeField] private LayerMask occupiedLayers;

    private Coroutine _shootRoutine;
    private Coroutine _defeatRoutine;

    private float _cooldownTimer;
    private bool _isShooting;
    private Vector2 _shootDir;
    private AnimatedSpriteRenderer _activeShotSprite;

    private AudioSource _audioSource;
    private Rigidbody2D _rb;
    private Collider2D _collider;
    private CharacterHealth _health;

    private bool _isDefeated;
    private bool _damageAllowed;

    private static readonly Collider2D[] _overlapBuffer = new Collider2D[32];

    private SpriteRenderer[] _tintSpriteRenderers;
    private Color[] _tintOriginalColors;

    protected override void Awake()
    {
        base.Awake();

        _audioSource = GetComponent<AudioSource>();
        _rb = GetComponent<Rigidbody2D>();
        _collider = GetComponent<Collider2D>();

        TryAutoResolveGroundTilemap();

        if (playerLayerMask.value == 0)
            playerLayerMask = LayerMask.GetMask("Player");

        if (stageLayerMask.value == 0)
            stageLayerMask = LayerMask.GetMask("Stage");

        if (projectileHitMask.value == 0)
            projectileHitMask = LayerMask.GetMask("Bomb", "Stage", "Enemy", "Player", "Water", "Explosion");

        if (occupiedLayers.value == 0)
            occupiedLayers = LayerMask.GetMask("Louie", "Player", "Bomb", "Explosion", "Item", "Enemy", "Stage");

        _health = GetComponent<CharacterHealth>();
        if (_health != null)
            _health.Died += OnAnyDied;

        CacheTintRenderers();

        DisableAllShotSprites();
        RefreshDamageAllowed();
        ClearCooldownTint();
    }

    protected override void OnDestroy()
    {
        ClearCooldownTint();

        if (_health != null)
            _health.Died -= OnAnyDied;

        base.OnDestroy();
    }

    private void CacheTintRenderers()
    {
        _tintSpriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);

        if (_tintSpriteRenderers == null)
        {
            _tintOriginalColors = null;
            return;
        }

        _tintOriginalColors = new Color[_tintSpriteRenderers.Length];
        for (int i = 0; i < _tintSpriteRenderers.Length; i++)
        {
            var sr = _tintSpriteRenderers[i];
            _tintOriginalColors[i] = sr != null ? sr.color : Color.white;
        }
    }

    private void ApplyCooldownTint()
    {
        if (!tintOnCooldown)
            return;

        if (_tintSpriteRenderers == null || _tintOriginalColors == null || _tintSpriteRenderers.Length == 0)
            CacheTintRenderers();

        if (_tintSpriteRenderers == null || _tintOriginalColors == null)
            return;

        if (isDead || _isDefeated)
        {
            ClearCooldownTint();
            return;
        }

        if (_cooldownTimer <= 0f || shotCooldownSeconds <= 0f)
        {
            ClearCooldownTint();
            return;
        }

        float normalized = 1f - Mathf.Clamp01(_cooldownTimer / shotCooldownSeconds);

        for (int i = 0; i < _tintSpriteRenderers.Length; i++)
        {
            var sr = _tintSpriteRenderers[i];
            if (sr == null)
                continue;

            Color baseColor = _tintOriginalColors[i];
            Color tint = cooldownTintColor;
            tint.a = baseColor.a;

            sr.color = Color.Lerp(tint, baseColor, normalized);
        }
    }

    private void ClearCooldownTint()
    {
        if (_tintSpriteRenderers == null || _tintOriginalColors == null)
            return;

        for (int i = 0; i < _tintSpriteRenderers.Length; i++)
        {
            var sr = _tintSpriteRenderers[i];
            if (sr == null)
                continue;

            sr.color = _tintOriginalColors[i];
        }
    }

    private void TryAutoResolveGroundTilemap()
    {
        if (groundTilemapOverride != null)
            return;

        var tilemaps = FindObjectsByType<Tilemap>(FindObjectsSortMode.None);
        if (tilemaps == null)
            return;

        foreach (var tm in tilemaps)
        {
            if (tm == null)
                continue;

            var cell = tm.WorldToCell(transform.position);
            if (tm.GetTile(cell) != null)
            {
                groundTilemapOverride = tm;
                return;
            }
        }
    }

    protected override void FixedUpdate()
    {
        if (isDead || _isDefeated)
        {
            ApplyCooldownTint();
            return;
        }

        if (TryGetComponent<StunReceiver>(out var stun) && stun != null && stun.IsStunned)
        {
            StopMotionNow();
            RefreshDamageAllowed();
            ApplyCooldownTint();
            return;
        }

        if (isInDamagedLoop)
        {
            StopMotionNow();
            RefreshDamageAllowed();
            ApplyCooldownTint();
            return;
        }

        if (_cooldownTimer > 0f)
            _cooldownTimer -= Time.fixedDeltaTime;

        if (_isShooting)
        {
            StopMotionNow();
            targetTile = rb.position;
            RefreshDamageAllowed();
            ApplyCooldownTint();
            return;
        }

        if (_cooldownTimer <= 0f && TryGetPlayerDirection(out var dirToPlayer))
        {
            direction = dirToPlayer;
            UpdateSpriteDirection(direction);

            if (stopAndShoot)
                StopMotionNow();

            StartShoot(dirToPlayer);
            RefreshDamageAllowed();
            ApplyCooldownTint();
            return;
        }

        base.FixedUpdate();
        RefreshDamageAllowed();
        ApplyCooldownTint();
    }

    protected override void UpdateSpriteDirection(Vector2 dir)
    {
        if (_isShooting || isDead || isInDamagedLoop || _isDefeated)
            return;

        base.UpdateSpriteDirection(dir);
        RefreshDamageAllowed();
    }

    protected override void OnTriggerEnter2D(Collider2D other)
    {
        if (isDead || _isDefeated)
            return;

        int layer = other.gameObject.layer;

        if (layer == LayerMask.NameToLayer("Explosion"))
        {
            if (_damageAllowed)
                TriggerDefeatAndSpawnPoyo();

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

    private void TriggerDefeatAndSpawnPoyo()
    {
        if (_isDefeated)
            return;

        _isDefeated = true;

        ClearCooldownTint();

        if (_defeatRoutine != null)
            StopCoroutine(_defeatRoutine);

        _defeatRoutine = StartCoroutine(DefeatRoutine());
    }

    private IEnumerator DefeatRoutine()
    {
        if (_shootRoutine != null)
        {
            StopCoroutine(_shootRoutine);
            _shootRoutine = null;
        }

        _isShooting = false;
        _activeShotSprite = null;

        RefreshDamageAllowed();

        if (_rb != null)
        {
            _rb.linearVelocity = Vector2.zero;
            _rb.simulated = false;
        }

        if (_collider != null)
            _collider.enabled = false;

        DisableAllShotSprites();
        DisableAllMovementSprites();

        if (spriteDamaged != null)
        {
            spriteDamaged.enabled = true;
            spriteDamaged.idle = false;
            spriteDamaged.loop = true;
            spriteDamaged.CurrentFrame = 0;
            spriteDamaged.RefreshFrame();

            if (forceFlipXFalse && spriteDamaged.TryGetComponent<SpriteRenderer>(out var srD) && srD != null)
                srD.flipX = false;

            activeSprite = spriteDamaged;
        }

        yield return new WaitForSecondsRealtime(Mathf.Max(0.01f, damagedSecondsBeforeSpawn));

        Vector2 origin = _rb != null ? _rb.position : (Vector2)transform.position;

        var gameManager = FindFirstObjectByType<GameManager>();

        if (notifyGameManagerOnDefeat)
        {
            if (gameManager != null)
                gameManager.NotifyEnemyDied();
        }

        if (dropTankMountOnDefeat)
            SpawnWorldTankMount(origin);

        if (poyoPrefab != null)
        {
            Vector2 landing = PickLandingInRange(origin, poyoMinLandingDistanceTiles, poyoMaxLandingDistanceTiles);
            GameObject poyoGo = Instantiate(poyoPrefab, origin, Quaternion.identity);

            if (poyoGo != null && gameManager != null)
                gameManager.NotifyEnemySpawned();

            if (poyoGo != null && poyoGo.TryGetComponent<PoyoEnemyMovementController>(out var poyo) && poyo != null)
                poyo.LaunchTo(landing, poyoLaunchSeconds, poyoArcHeightTiles);
            else if (poyoGo != null)
                poyoGo.transform.position = landing;
        }

        Destroy(gameObject);
    }

    private void SpawnWorldTankMount(Vector2 origin)
    {
        if (tankMountPrefab == null)
            return;

        Vector3 pos = (Vector3)origin;
        GameObject tankGo = Instantiate(tankMountPrefab, pos, Quaternion.identity);
        if (tankGo == null)
            return;

        if (!tankGo.TryGetComponent<MountWorldPickup>(out var pickup) || pickup == null)
            pickup = tankGo.AddComponent<MountWorldPickup>();

        pickup.Init(MountedType.Tank);

        var visual = tankGo.GetComponentInChildren<MountVisualController>(true);

        if (tankGo.TryGetComponent<MountMovementController>(out var mmc) && mmc != null)
            mmc.enabled = false;

        if (tankGo.TryGetComponent<Rigidbody2D>(out var rb2) && rb2 != null)
        {
            rb2.simulated = true;
            rb2.linearVelocity = Vector2.zero;
            rb2.angularVelocity = 0f;
        }

        if (tankGo.TryGetComponent<Collider2D>(out var col))
            col.enabled = true;

        if (tankGo.TryGetComponent<BombController>(out var bc) && bc != null)
            bc.enabled = false;

        if (tankGo.TryGetComponent<CharacterHealth>(out var h) && h != null)
            h.StopInvulnerability();

        ClearSpawnedTankInvulnerability(tankGo);
        StartSpawnedTankInactivityLoop(tankGo, visual);
    }

    private void StartSpawnedTankInactivityLoop(GameObject tankGo, MountVisualController visual)
    {
        if (tankGo == null || visual == null)
            return;

        if (tankGo.TryGetComponent<MovementController>(out var rootMc) && rootMc != null && rootMc.isDead)
            return;

        var selfMovement = tankGo.GetComponentInChildren<MovementController>(true);
        if (selfMovement == null || selfMovement.isDead)
            return;

        visual.localOffset = (Vector2)visual.transform.localPosition;

        visual.Bind(selfMovement);
        visual.enabled = true;

        visual.SetInactivityEmote(true);
    }

    private void ClearSpawnedTankInvulnerability(GameObject tankGo)
    {
        if (tankGo == null)
            return;

        var health = tankGo.GetComponentInChildren<CharacterHealth>(true);
        if (health != null)
            health.StopInvulnerability();

        if (tankGo.TryGetComponent<MovementController>(out var mcRoot))
            mcRoot.SetExplosionInvulnerable(false);

        var mcChild = tankGo.GetComponentInChildren<MovementController>(true);
        if (mcChild != null)
            mcChild.SetExplosionInvulnerable(false);
    }

    private Vector2 PickLandingInRange(Vector2 origin, int minRadiusTiles, int maxRadiusTiles)
    {
        if (groundTilemapOverride == null)
            return origin;

        int minR = Mathf.Max(0, minRadiusTiles);
        int maxR = Mathf.Max(minR, maxRadiusTiles);

        int min2 = minR * minR;
        int max2 = maxR * maxR;

        Vector3Int originCell = groundTilemapOverride.WorldToCell(origin);

        for (int i = 0; i < maxRespawnAttempts; i++)
        {
            int dx = Random.Range(-maxR, maxR + 1);
            int dy = Random.Range(-maxR, maxR + 1);

            int d2 = dx * dx + dy * dy;
            if (d2 < min2 || d2 > max2)
                continue;

            Vector3Int cell = new(originCell.x + dx, originCell.y + dy, 0);

            if (groundTilemapOverride.GetTile(cell) == null)
                continue;

            Vector2 center = groundTilemapOverride.GetCellCenterWorld(cell);

            if (IsOccupied(center))
                continue;

            if (IsTileBlocked(center))
                continue;

            return center;
        }

        return origin;
    }

    private bool IsOccupied(Vector2 worldCenter)
    {
        var filter = new ContactFilter2D { useLayerMask = true, useTriggers = true };
        filter.SetLayerMask(occupiedLayers);

        int count = Physics2D.OverlapBox(worldCenter, Vector2.one * overlapBoxSize, 0f, filter, _overlapBuffer);

        for (int i = 0; i < count; i++)
        {
            var c = _overlapBuffer[i];
            _overlapBuffer[i] = null;

            if (c != null && c.gameObject != gameObject)
                return true;
        }

        return false;
    }

    private void RefreshDamageAllowed()
    {
        _damageAllowed =
            !_isShooting &&
            !_isDefeated &&
            !isDead &&
            !isInDamagedLoop;
    }

    private void StartShoot(Vector2 dir)
    {
        if (_isShooting || isDead || isInDamagedLoop || _isDefeated)
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

        if (!isDead && !isInDamagedLoop && !_isDefeated)
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

        ApplyCooldownTint();
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

    private void OnAnyDied()
    {
        ClearCooldownTint();

        if (_defeatRoutine != null)
        {
            StopCoroutine(_defeatRoutine);
            _defeatRoutine = null;
        }

        if (_shootRoutine != null)
        {
            StopCoroutine(_shootRoutine);
            _shootRoutine = null;
        }

        StopAllCoroutines();
    }
}

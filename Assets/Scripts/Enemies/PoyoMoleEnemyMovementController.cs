using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(CharacterHealth))]
[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(StunReceiver))]
public sealed class PoyoMoleEnemyMovementController : JunctionTurningEnemyMovementController
{
    [Header("Walk Sprites (4 directions)")]
    [SerializeField] private AnimatedSpriteRenderer walkUpSprite;
    [SerializeField] private AnimatedSpriteRenderer walkDownSprite;
    [SerializeField] private AnimatedSpriteRenderer walkLeftSprite;
    [SerializeField] private AnimatedSpriteRenderer walkRightSprite;
    [SerializeField] private bool forceFlipXFalse = true;

    [Header("Ability START (3 phases)")]
    [SerializeField] private AnimatedSpriteRenderer abilityStartPhase1Sprite;
    [SerializeField] private AnimatedSpriteRenderer abilityStartPhase2Sprite;
    [SerializeField] private AnimatedSpriteRenderer abilityStartPhase3Sprite;

    [Header("Ability START timings")]
    [SerializeField, Min(0.01f)] private float abilityStartPhase1Seconds = 0.7f;
    [SerializeField, Min(0.01f)] private float abilityStartPhase2Seconds = 0.7f;
    [SerializeField, Min(0.01f)] private float abilityStartPhase3Seconds = 0.6f;

    [Header("Hidden (no visuals)")]
    [SerializeField, Min(0.01f)] private float hiddenSeconds = 5f;

    [Header("Ability END (3 phases)")]
    [SerializeField] private AnimatedSpriteRenderer abilityEndPhase1Sprite;
    [SerializeField] private AnimatedSpriteRenderer abilityEndPhase2Sprite;
    [SerializeField] private AnimatedSpriteRenderer abilityEndPhase3Sprite;

    [Header("Ability END timings")]
    [SerializeField, Min(0.01f)] private float abilityEndPhase2Seconds = 0.7f;
    [SerializeField, Min(0.01f)] private float abilityEndPhase3Seconds = 0.7f;

    [Header("Ability Trigger")]
    [SerializeField, Min(0f)] private float walkSecondsBeforeAbility = 10f;

    [Header("SFX")]
    [SerializeField] private AudioClip abilityStartPhase1Sfx;
    [SerializeField, Range(0f, 1f)] private float abilityStartPhase1SfxVolume = 1f;

    [Header("Defeat -> Spawn Poyo")]
    [SerializeField] private GameObject poyoPrefab;
    [SerializeField, Min(0.01f)] private float damagedSecondsBeforeSpawn = 0.5f;
    [SerializeField, Min(0.01f)] private float poyoLaunchSeconds = 0.5f;
    [SerializeField, Min(0f)] private float poyoArcHeightTiles = 3f;
    [SerializeField, Min(0)] private int poyoMinLandingDistanceTiles = 10;
    [SerializeField, Min(0)] private int poyoMaxLandingDistanceTiles = 15;
    [SerializeField] private bool notifyGameManagerOnDefeat = true;

    [Header("Defeat -> Drop World Mount (Mole)")]
    [SerializeField] private GameObject moleMountPrefab;
    [SerializeField] private bool dropMoleMountOnDefeat = true;

    [Header("Respawn / Ground")]
    [SerializeField] private Tilemap groundTilemapOverride;
    [SerializeField, Min(1)] private int maxRespawnAttempts = 200;
    [SerializeField, Min(0.05f)] private float overlapBoxSize = 0.8f;
    [SerializeField] private LayerMask occupiedLayers;

    [Header("Respawn Exclusion (tiles)")]
    [SerializeField, Min(0)] private int excludeRadiusTiles = 3;

    private CharacterHealth _health;
    private Rigidbody2D _rb;
    private Collider2D _collider;
    private AudioSource _audio;
    private Coroutine _abilityRoutine;
    private Coroutine _defeatRoutine;

    private bool _isUsingAbility;
    private bool _isHidden;
    private float _walkTimer;
    private bool _walkTimerPrimed;

    private bool _damageAllowed;
    private bool _isDefeated;

    private static readonly Collider2D[] _overlapBuffer = new Collider2D[32];

    private Vector3Int _abilityStartCell;
    private bool _hasAbilityStartCell;
    private Vector2 _abilityStartPos;
    private bool _hasAbilityStartPos;

    protected override void Awake()
    {
        base.Awake();

        _rb = GetComponent<Rigidbody2D>();
        _collider = GetComponent<Collider2D>();
        _audio = GetComponent<AudioSource>();

        TryAutoResolveGroundTilemap();

        spriteUp = walkUpSprite;
        spriteDown = walkDownSprite;
        spriteLeft = walkLeftSprite;

        if (activeSprite == null)
            activeSprite = walkDownSprite != null ? walkDownSprite : walkLeftSprite != null ? walkLeftSprite : walkUpSprite;

        if (occupiedLayers.value == 0)
            occupiedLayers = LayerMask.GetMask("Louie", "Player", "Bomb", "Explosion", "Item", "Enemy", "Stage");

        _health = GetComponent<CharacterHealth>();
        if (_health != null)
            _health.Died += OnAnyDied;

        DisableAllAbilitySprites();
        DisableAllWalkSprites();
        ApplyWalkVisualForDirection(Vector2.down);

        ResetWalkTimer();
        RefreshDamageAllowed();
    }

    protected override void OnDestroy()
    {
        if (_health != null)
            _health.Died -= OnAnyDied;

        base.OnDestroy();
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
            return;

        if (_isUsingAbility || _isHidden)
        {
            if (_rb != null)
                _rb.linearVelocity = Vector2.zero;

            targetTile = _rb.position;
            RefreshDamageAllowed();
            return;
        }

        base.FixedUpdate();

        UpdateWalkTimer();

        if (_walkTimerPrimed && _walkTimer >= walkSecondsBeforeAbility)
        {
            ResetWalkTimer();
            StartAbility();
        }

        RefreshDamageAllowed();
    }

    protected override void UpdateSpriteDirection(Vector2 dir)
    {
        if (_isUsingAbility || _isHidden || isInDamagedLoop || isDead || _isDefeated)
            return;

        ApplyWalkVisualForDirection(dir);
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

        if (_defeatRoutine != null)
            StopCoroutine(_defeatRoutine);

        _defeatRoutine = StartCoroutine(DefeatRoutine());
    }

    private IEnumerator DefeatRoutine()
    {
        if (_abilityRoutine != null)
        {
            StopCoroutine(_abilityRoutine);
            _abilityRoutine = null;
        }

        _isUsingAbility = false;
        _isHidden = false;

        ResetWalkTimer();
        RefreshDamageAllowed();

        if (_rb != null)
        {
            _rb.linearVelocity = Vector2.zero;
            _rb.simulated = false;
        }

        if (_collider != null)
            _collider.enabled = false;

        DisableAllWalkSprites();
        DisableAllAbilitySprites();
        ForceAllAbilitySpriteRenderersOff();

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

        if (notifyGameManagerOnDefeat)
        {
            var gameManager = FindFirstObjectByType<GameManager>();
            if (gameManager != null)
                gameManager.NotifyEnemyDied();
        }

        if (dropMoleMountOnDefeat)
            SpawnWorldMoleMount(origin);

        if (poyoPrefab != null)
        {
            Vector2 landing = PickLandingInRange(origin, poyoMinLandingDistanceTiles, poyoMaxLandingDistanceTiles);
            GameObject poyoGo = Instantiate(poyoPrefab, origin, Quaternion.identity);

            if (poyoGo != null && poyoGo.TryGetComponent<PoyoEnemyMovementController>(out var poyo) && poyo != null)
                poyo.LaunchTo(landing, poyoLaunchSeconds, poyoArcHeightTiles);
            else if (poyoGo != null)
                poyoGo.transform.position = landing;
        }

        Destroy(gameObject);
    }

    private void SpawnWorldMoleMount(Vector2 origin)
    {
        if (moleMountPrefab == null)
            return;

        Vector3 pos = (Vector3)origin;
        GameObject moleGo = Instantiate(moleMountPrefab, pos, Quaternion.identity);
        if (moleGo == null)
            return;

        if (!moleGo.TryGetComponent<MountWorldPickup>(out var pickup) || pickup == null)
            pickup = moleGo.AddComponent<MountWorldPickup>();

        pickup.Init(MountedType.Mole);

        var visual = moleGo.GetComponentInChildren<MountVisualController>(true);

        if (moleGo.TryGetComponent<MountMovementController>(out var mmc) && mmc != null)
            mmc.enabled = false;

        if (moleGo.TryGetComponent<Rigidbody2D>(out var rb) && rb != null)
        {
            rb.simulated = true;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        if (moleGo.TryGetComponent<Collider2D>(out var col))
            col.enabled = true;

        if (moleGo.TryGetComponent<BombController>(out var bc) && bc != null)
            bc.enabled = false;

        if (moleGo.TryGetComponent<CharacterHealth>(out var h) && h != null)
            h.StopInvulnerability();

        ClearSpawnedMoleInvulnerability(moleGo);

        StartSpawnedMoleInactivityLoop(moleGo, visual);
    }

    private void StartSpawnedMoleInactivityLoop(GameObject moleGo, MountVisualController visual)
    {
        if (moleGo == null || visual == null)
            return;

        if (moleGo.TryGetComponent<MovementController>(out var rootMc) && rootMc != null && rootMc.isDead)
            return;

        var selfMovement = moleGo.GetComponentInChildren<MovementController>(true);
        if (selfMovement == null || selfMovement.isDead)
            return;

        visual.localOffset = (Vector2)visual.transform.localPosition;

        visual.Bind(selfMovement);
        visual.enabled = true;

        visual.SetInactivityEmote(true);
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

            Vector3Int cell = new Vector3Int(originCell.x + dx, originCell.y + dy, 0);

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

    private void ApplyWalkVisualForDirection(Vector2 dir)
    {
        AnimatedSpriteRenderer chosen = ResolveWalkSprite(dir);
        if (chosen == null)
            return;

        DisableAllAbilitySprites();
        DisableAllWalkSprites();

        chosen.enabled = true;
        chosen.idle = false;
        chosen.loop = true;

        activeSprite = chosen;
        ForceNoFlipOn(chosen);
    }

    private AnimatedSpriteRenderer ResolveWalkSprite(Vector2 dir)
    {
        if (dir == Vector2.up) return walkUpSprite;
        if (dir == Vector2.down) return walkDownSprite;
        if (dir == Vector2.left) return walkLeftSprite;
        if (dir == Vector2.right) return walkRightSprite;

        return walkDownSprite != null ? walkDownSprite : walkLeftSprite != null ? walkLeftSprite : walkUpSprite;
    }

    private void DisableAllWalkSprites()
    {
        if (walkUpSprite != null) walkUpSprite.enabled = false;
        if (walkDownSprite != null) walkDownSprite.enabled = false;
        if (walkLeftSprite != null) walkLeftSprite.enabled = false;
        if (walkRightSprite != null) walkRightSprite.enabled = false;
    }

    private void StartAbility()
    {
        if (_isUsingAbility || isDead || _isDefeated)
            return;

        if (_abilityRoutine != null)
            StopCoroutine(_abilityRoutine);

        _abilityRoutine = StartCoroutine(AbilityRoutine());
    }

    private IEnumerator AbilityRoutine()
    {
        _isUsingAbility = true;
        ResetWalkTimer();
        RefreshDamageAllowed();

        if (_rb != null)
            _rb.linearVelocity = Vector2.zero;

        CaptureAbilityStartPosition();

        DisableAllWalkSprites();

        yield return PlayAbilityStartPhases();

        if (isDead || _isDefeated)
            yield break;

        _isHidden = true;
        RefreshDamageAllowed();

        if (_collider != null)
            _collider.enabled = false;

        if (_rb != null)
        {
            _rb.linearVelocity = Vector2.zero;
            _rb.simulated = false;
        }

        DisableAllAbilitySprites();
        ForceAllAbilitySpriteRenderersOff();

        yield return new WaitForSecondsRealtime(Mathf.Max(0.01f, hiddenSeconds));

        if (isDead || _isDefeated)
            yield break;

        Vector2 respawn = PickRespawnWorldPositionAvoidingOrigin();

        if (_rb != null)
        {
            _rb.simulated = true;
            _rb.position = respawn;
            _rb.linearVelocity = Vector2.zero;
        }

        SnapToGrid();

        if (_collider != null)
            _collider.enabled = true;

        _isHidden = false;
        RefreshDamageAllowed();

        yield return PlayAbilityEndPhasesReverseOrderNoPhase1();

        _isUsingAbility = false;
        _abilityRoutine = null;
        RefreshDamageAllowed();

        DisableAllAbilitySprites();
        ApplyWalkVisualForDirection(direction == Vector2.zero ? Vector2.down : direction);
        DecideNextTile();
    }

    private IEnumerator PlayAbilityStartPhases()
    {
        yield return PlayOnePhase(abilityStartPhase1Sprite, abilityStartPhase1Seconds, loopPhase: true, playSfx: true);
        if (isDead || _isDefeated) yield break;

        yield return PlayOnePhase(abilityStartPhase2Sprite, abilityStartPhase2Seconds, loopPhase: false, playSfx: false);
        if (isDead || _isDefeated) yield break;

        yield return PlayOnePhase(abilityStartPhase3Sprite, abilityStartPhase3Seconds, loopPhase: false, playSfx: false);
    }

    private IEnumerator PlayAbilityEndPhasesReverseOrderNoPhase1()
    {
        yield return PlayOnePhase(abilityEndPhase3Sprite, abilityEndPhase3Seconds, loopPhase: false, playSfx: false);
        if (isDead || _isDefeated) yield break;

        yield return PlayOnePhase(abilityEndPhase2Sprite, abilityEndPhase2Seconds, loopPhase: false, playSfx: false);
    }

    private IEnumerator PlayOnePhase(AnimatedSpriteRenderer phaseSprite, float seconds, bool loopPhase, bool playSfx)
    {
        DisableAllWalkSprites();
        DisableAllAbilitySprites();
        ForceAllAbilitySpriteRenderersOff();

        float dur = Mathf.Max(0.01f, seconds);

        if (phaseSprite != null)
        {
            phaseSprite.enabled = true;
            ForceSpriteRendererEnabled(phaseSprite, true);

            phaseSprite.idle = false;
            phaseSprite.loop = loopPhase;
            phaseSprite.CurrentFrame = 0;
            phaseSprite.RefreshFrame();

            activeSprite = phaseSprite;
            ForceNoFlipOn(phaseSprite);

            if (playSfx && abilityStartPhase1Sfx != null && _audio != null)
                _audio.PlayOneShot(abilityStartPhase1Sfx, Mathf.Clamp01(abilityStartPhase1SfxVolume));

            yield return new WaitForSecondsRealtime(dur);

            phaseSprite.enabled = false;
            ForceSpriteRendererEnabled(phaseSprite, false);
        }
        else
        {
            if (playSfx && abilityStartPhase1Sfx != null && _audio != null)
                _audio.PlayOneShot(abilityStartPhase1Sfx, Mathf.Clamp01(abilityStartPhase1SfxVolume));

            yield return new WaitForSecondsRealtime(dur);
        }
    }

    private void DisableAllAbilitySprites()
    {
        if (abilityStartPhase1Sprite != null) abilityStartPhase1Sprite.enabled = false;
        if (abilityStartPhase2Sprite != null) abilityStartPhase2Sprite.enabled = false;
        if (abilityStartPhase3Sprite != null) abilityStartPhase3Sprite.enabled = false;

        if (abilityEndPhase1Sprite != null) abilityEndPhase1Sprite.enabled = false;
        if (abilityEndPhase2Sprite != null) abilityEndPhase2Sprite.enabled = false;
        if (abilityEndPhase3Sprite != null) abilityEndPhase3Sprite.enabled = false;
    }

    private void ForceAllAbilitySpriteRenderersOff()
    {
        ForceSpriteRendererOff(abilityStartPhase1Sprite);
        ForceSpriteRendererOff(abilityStartPhase2Sprite);
        ForceSpriteRendererOff(abilityStartPhase3Sprite);

        ForceSpriteRendererOff(abilityEndPhase1Sprite);
        ForceSpriteRendererOff(abilityEndPhase2Sprite);
        ForceSpriteRendererOff(abilityEndPhase3Sprite);
    }

    private void ForceNoFlipOn(AnimatedSpriteRenderer r)
    {
        if (!forceFlipXFalse || r == null)
            return;

        if (r.TryGetComponent<SpriteRenderer>(out var sr) && sr != null)
            sr.flipX = false;
    }

    private void ForceSpriteRendererOff(AnimatedSpriteRenderer r)
    {
        if (r == null)
            return;

        if (r.TryGetComponent<SpriteRenderer>(out var sr) && sr != null)
        {
            sr.enabled = false;
            return;
        }

        var childSr = r.GetComponentInChildren<SpriteRenderer>(true);
        if (childSr != null)
            childSr.enabled = false;
    }

    private void ForceSpriteRendererEnabled(AnimatedSpriteRenderer r, bool enabled)
    {
        if (r == null)
            return;

        if (r.TryGetComponent<SpriteRenderer>(out var sr) && sr != null)
        {
            sr.enabled = enabled;
            return;
        }

        var childSr = r.GetComponentInChildren<SpriteRenderer>(true);
        if (childSr != null)
            childSr.enabled = enabled;
    }

    private void CaptureAbilityStartPosition()
    {
        _hasAbilityStartCell = false;
        _hasAbilityStartPos = false;

        if (groundTilemapOverride != null)
        {
            _abilityStartCell = groundTilemapOverride.WorldToCell(_rb.position);
            _hasAbilityStartCell = true;
            return;
        }

        _abilityStartPos = _rb.position;
        _hasAbilityStartPos = true;
    }

    private Vector2 PickRespawnWorldPositionAvoidingOrigin()
    {
        if (groundTilemapOverride == null)
            return _rb.position;

        BoundsInt bounds = groundTilemapOverride.cellBounds;
        int r = Mathf.Max(0, excludeRadiusTiles);
        int r2 = r * r;

        for (int i = 0; i < maxRespawnAttempts; i++)
        {
            int x = Random.Range(bounds.xMin, bounds.xMax);
            int y = Random.Range(bounds.yMin, bounds.yMax);
            Vector3Int cell = new Vector3Int(x, y, 0);

            if (groundTilemapOverride.GetTile(cell) == null)
                continue;

            if (IsForbiddenCell(cell, r, r2))
                continue;

            Vector2 center = groundTilemapOverride.GetCellCenterWorld(cell);

            if (IsOccupied(center))
                continue;

            if (IsTileBlocked(center))
                continue;

            return center;
        }

        return _rb.position;
    }

    private bool IsForbiddenCell(Vector3Int candidate, int radius, int radiusSquared)
    {
        if (radius <= 0)
        {
            if (_hasAbilityStartCell)
                return candidate == _abilityStartCell;

            if (_hasAbilityStartPos)
                return Vector2.Distance(groundTilemapOverride.GetCellCenterWorld(candidate), _abilityStartPos) < 0.01f;

            return false;
        }

        if (_hasAbilityStartCell)
        {
            int dx = candidate.x - _abilityStartCell.x;
            int dy = candidate.y - _abilityStartCell.y;
            return (dx * dx + dy * dy) <= radiusSquared;
        }

        if (_hasAbilityStartPos)
        {
            Vector2 c = groundTilemapOverride.GetCellCenterWorld(candidate);
            float tile = Mathf.Max(0.0001f, tileSize);
            Vector2 deltaTiles = (c - _abilityStartPos) / tile;

            int dx = Mathf.RoundToInt(deltaTiles.x);
            int dy = Mathf.RoundToInt(deltaTiles.y);

            return (dx * dx + dy * dy) <= radiusSquared;
        }

        return false;
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

    private void UpdateWalkTimer()
    {
        if (walkSecondsBeforeAbility <= 0f)
            return;

        bool anyWalkEnabled =
            (walkUpSprite != null && walkUpSprite.enabled) ||
            (walkDownSprite != null && walkDownSprite.enabled) ||
            (walkLeftSprite != null && walkLeftSprite.enabled) ||
            (walkRightSprite != null && walkRightSprite.enabled);

        bool canCount =
            anyWalkEnabled &&
            !_isUsingAbility &&
            !_isHidden &&
            !_isDefeated &&
            !isDead &&
            !isInDamagedLoop &&
            !isStuck;

        if (canCount)
        {
            _walkTimerPrimed = true;
            _walkTimer += Time.fixedDeltaTime;
        }
        else
        {
            ResetWalkTimer();
        }
    }

    private void ResetWalkTimer()
    {
        _walkTimer = 0f;
        _walkTimerPrimed = false;
    }

    private void RefreshDamageAllowed()
    {
        bool anyWalkEnabled =
            (walkUpSprite != null && walkUpSprite.enabled) ||
            (walkDownSprite != null && walkDownSprite.enabled) ||
            (walkLeftSprite != null && walkLeftSprite.enabled) ||
            (walkRightSprite != null && walkRightSprite.enabled);

        _damageAllowed =
            anyWalkEnabled &&
            !_isUsingAbility &&
            !_isHidden &&
            !_isDefeated &&
            !isDead &&
            !isInDamagedLoop;
    }

    private void OnAnyDied()
    {
        if (_defeatRoutine != null)
        {
            StopCoroutine(_defeatRoutine);
            _defeatRoutine = null;
        }

        if (_abilityRoutine != null)
        {
            StopCoroutine(_abilityRoutine);
            _abilityRoutine = null;
        }

        StopAllCoroutines();
    }

    private void ClearSpawnedMoleInvulnerability(GameObject moleGo)
    {
        if (moleGo == null)
            return;

        var health = moleGo.GetComponentInChildren<CharacterHealth>(true);
        if (health != null)
            health.StopInvulnerability();

        if (moleGo.TryGetComponent<MovementController>(out var mcRoot))
            mcRoot.SetExplosionInvulnerable(false);

        var mcChild = moleGo.GetComponentInChildren<MovementController>(true);
        if (mcChild != null)
            mcChild.SetExplosionInvulnerable(false);
    }
}

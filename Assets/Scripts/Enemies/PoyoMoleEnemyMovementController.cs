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
    [SerializeField, Min(0.01f)] private float abilityEndPhase1Seconds = 0.6f;
    [SerializeField, Min(0.01f)] private float abilityEndPhase2Seconds = 0.7f;
    [SerializeField, Min(0.01f)] private float abilityEndPhase3Seconds = 0.7f;

    [Header("Ability Trigger")]
    [SerializeField, Min(0f)] private float walkSecondsBeforeAbility = 10f;

    [Header("Respawn")]
    [SerializeField] private Tilemap groundTilemapOverride;
    [SerializeField, Min(1)] private int maxRespawnAttempts = 200;
    [SerializeField, Min(0.05f)] private float overlapBoxSize = 0.8f;
    [SerializeField] private LayerMask occupiedLayers;

    [Header("Respawn Exclusion (tiles)")]
    [SerializeField, Min(0)] private int excludeRadiusTiles = 3;

    private CharacterHealth _health;
    private Rigidbody2D _rb;
    private Collider2D _collider;
    private Coroutine _abilityRoutine;

    private bool _isUsingAbility;
    private bool _isHidden;
    private float _walkTimer;
    private bool _walkTimerPrimed;

    private static readonly Collider2D[] _overlapBuffer = new Collider2D[32];

    private Vector3Int _abilityStartCell;
    private bool _hasAbilityStartCell;
    private Vector2 _abilityStartPos;
    private bool _hasAbilityStartPos;

    private bool _damageAllowed;

    protected override void Awake()
    {
        base.Awake();

        _rb = GetComponent<Rigidbody2D>();
        _collider = GetComponent<Collider2D>();

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

        DLog($"Awake rb={_rb != null} groundTm={groundTilemapOverride != null} excludeRadiusTiles={excludeRadiusTiles}");
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
                DLog($"AutoResolved GroundTilemap -> {tm.name}");
                return;
            }
        }

        DLog("AutoResolveGroundTilemap FAILED");
    }

    protected override void FixedUpdate()
    {
        if (isDead)
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
            DLog($"TriggerAbility walkTimer={_walkTimer:F3}/{walkSecondsBeforeAbility:F3}");
            ResetWalkTimer();
            StartAbility();
        }

        RefreshDamageAllowed();
    }

    protected override void UpdateSpriteDirection(Vector2 dir)
    {
        if (_isUsingAbility || _isHidden || isInDamagedLoop || isDead)
            return;

        ApplyWalkVisualForDirection(dir);
        RefreshDamageAllowed();
    }

    protected override void OnTriggerEnter2D(Collider2D other)
    {
        if (isDead)
            return;

        int layer = other.gameObject.layer;

        if (layer == LayerMask.NameToLayer("Explosion"))
        {
            if (_health != null && _damageAllowed)
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
        if (dir == Vector2.up)
            return walkUpSprite;

        if (dir == Vector2.down)
            return walkDownSprite;

        if (dir == Vector2.left)
            return walkLeftSprite;

        if (dir == Vector2.right)
            return walkRightSprite;

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
        if (_isUsingAbility || isDead)
            return;

        DLog("StartAbility()");

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

        DLog($"AbilityRoutine BEGIN rbPos={_rb.position} startCell={(_hasAbilityStartCell ? _abilityStartCell.ToString() : "none")} startPos={(_hasAbilityStartPos ? _abilityStartPos.ToString() : "none")}");

        DisableAllWalkSprites();

        yield return PlayAbilityStartPhases();

        if (isDead)
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

        DLog($"HIDE ON rbPos={_rb.position} hiddenSeconds={hiddenSeconds:F3}");

        yield return new WaitForSecondsRealtime(Mathf.Max(0.01f, hiddenSeconds));

        if (isDead)
            yield break;

        Vector2 respawn = PickRespawnWorldPositionAvoidingOrigin();
        DLog($"RESPAWN PICKED {respawn}");

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

        DLog($"AbilityRoutine END rbPos={_rb.position} nextTarget={targetTile}");
    }

    private IEnumerator PlayAbilityStartPhases()
    {
        yield return PlayOnePhase(abilityStartPhase1Sprite, abilityStartPhase1Seconds, "StartPhase1", loopPhase: true);
        if (isDead) yield break;

        yield return PlayOnePhase(abilityStartPhase2Sprite, abilityStartPhase2Seconds, "StartPhase2", loopPhase: false);
        if (isDead) yield break;

        yield return PlayOnePhase(abilityStartPhase3Sprite, abilityStartPhase3Seconds, "StartPhase3", loopPhase: false);
    }

    private IEnumerator PlayAbilityEndPhasesReverseOrderNoPhase1()
    {
        yield return PlayOnePhase(abilityEndPhase3Sprite, abilityEndPhase3Seconds, "EndPhase3", loopPhase: false);
        if (isDead) yield break;

        yield return PlayOnePhase(abilityEndPhase2Sprite, abilityEndPhase2Seconds, "EndPhase2", loopPhase: false);
    }

    private IEnumerator PlayOnePhase(AnimatedSpriteRenderer phaseSprite, float seconds, string label, bool loopPhase)
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

            DLog($"{label} ON loop={loopPhase} dur={dur:F3}");
            yield return new WaitForSecondsRealtime(dur);

            phaseSprite.enabled = false;
            ForceSpriteRendererEnabled(phaseSprite, false);
        }
        else
        {
            DLog($"{label} NULL (waiting) dur={dur:F3}");
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
        {
            DLog("PickRespawn tm=NULL -> returning current position");
            return _rb.position;
        }

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

        DLog("Respawn fallback -> returning original position");
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
            !isDead &&
            !isInDamagedLoop;
    }

    private void OnAnyDied()
    {
        if (_abilityRoutine != null)
        {
            StopCoroutine(_abilityRoutine);
            _abilityRoutine = null;
        }

        StopAllCoroutines();
    }

    private void DLog(string msg)
    {
        Debug.Log($"[PoyoDbg] name='{name}' t={Time.time:F3} f={Time.frameCount} {msg}", this);
    }
}

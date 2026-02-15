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
    [Header("Walk Sprite")]
    [SerializeField] private AnimatedSpriteRenderer walkSprite;
    [SerializeField] private bool forceFlipXFalse = true;

    [Header("Ability Sprites")]
    [SerializeField] private AnimatedSpriteRenderer abilityStartSprite;
    [SerializeField] private AnimatedSpriteRenderer abilityEndSprite;

    [Header("Ability Timing")]
    [SerializeField] private float walkSecondsBeforeAbility = 10f;
    [SerializeField] private float abilityStartDurationSeconds = 2f;
    [SerializeField] private float hiddenSeconds = 5f;
    [SerializeField] private float abilityEndDurationSeconds = 2f;

    [Header("Respawn")]
    [SerializeField] private Tilemap groundTilemapOverride;
    [SerializeField] private int maxRespawnAttempts = 200;
    [SerializeField] private float overlapBoxSize = 0.8f;
    [SerializeField] private LayerMask occupiedLayers;

    [Header("Respawn Exclusion (tiles)")]
    [SerializeField] private int excludeRadiusTiles = 3;

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

    private enum VisualState
    {
        Walk,
        AbilityStart,
        Hidden,
        AbilityEnd
    }

    private VisualState _visualState = VisualState.Walk;

    protected override void Awake()
    {
        base.Awake();

        _rb = GetComponent<Rigidbody2D>();
        _collider = GetComponent<Collider2D>();

        TryAutoResolveGroundTilemap();

        spriteDown = walkSprite;
        activeSprite = walkSprite;

        if (occupiedLayers.value == 0)
            occupiedLayers = LayerMask.GetMask("Louie", "Player", "Bomb", "Explosion", "Item", "Enemy", "Stage");

        _health = GetComponent<CharacterHealth>();
        if (_health != null)
            _health.Died += OnAnyDied;

        ApplyVisualState(VisualState.Walk);
        ResetWalkTimer();

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
            _rb.linearVelocity = Vector2.zero;
            targetTile = _rb.position;
            return;
        }

        base.FixedUpdate();

        UpdateWalkTimer();

        if (_walkTimerPrimed && _walkTimer >= walkSecondsBeforeAbility)
        {
            DLog($"TriggerAbility walkTimer={_walkTimer}");
            ResetWalkTimer();
            StartAbility();
        }
    }

    protected override void UpdateSpriteDirection(Vector2 dir)
    {
        if (_isUsingAbility || _isHidden || isInDamagedLoop || isDead)
            return;

        if (walkSprite == null)
            return;

        if (_visualState != VisualState.Walk)
            ApplyVisualState(VisualState.Walk);

        ForceNoFlipOn(walkSprite);
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

        if (_rb != null)
            _rb.linearVelocity = Vector2.zero;

        CaptureAbilityStartPosition();

        DLog($"AbilityRoutine BEGIN rbPos={_rb.position} startCell={(_hasAbilityStartCell ? _abilityStartCell.ToString() : "none")} startPos={(_hasAbilityStartPos ? _abilityStartPos.ToString() : "none")}");

        ApplyVisualState(VisualState.AbilityStart);

        if (abilityStartSprite != null)
            ConfigureAsOneShot(abilityStartSprite);

        yield return new WaitForSecondsRealtime(Mathf.Max(0.01f, abilityStartDurationSeconds));

        if (isDead)
            yield break;

        _isHidden = true;

        if (_collider != null)
            _collider.enabled = false;

        if (_rb != null)
        {
            _rb.linearVelocity = Vector2.zero;
            _rb.simulated = false;
        }

        ApplyVisualState(VisualState.Hidden);

        DLog($"HIDE ON rbPos={_rb.position} hiddenSeconds={hiddenSeconds}");

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

        ApplyVisualState(VisualState.AbilityEnd);

        if (abilityEndSprite != null)
            ConfigureAsOneShot(abilityEndSprite);

        yield return new WaitForSecondsRealtime(Mathf.Max(0.01f, abilityEndDurationSeconds));

        _isUsingAbility = false;
        _abilityRoutine = null;

        ApplyVisualState(VisualState.Walk);
        DecideNextTile();

        DLog($"AbilityRoutine END rbPos={_rb.position} nextTarget={targetTile}");
    }

    private void ApplyVisualState(VisualState state)
    {
        _visualState = state;

        if (walkSprite != null)
            walkSprite.enabled = false;

        if (abilityStartSprite != null)
            abilityStartSprite.enabled = false;

        if (abilityEndSprite != null)
            abilityEndSprite.enabled = false;

        if (state == VisualState.Walk)
        {
            if (walkSprite != null)
            {
                walkSprite.enabled = true;
                walkSprite.idle = false;
                walkSprite.loop = true;
                activeSprite = walkSprite;
                ForceNoFlipOn(walkSprite);
            }

            return;
        }

        if (state == VisualState.AbilityStart)
        {
            if (abilityStartSprite != null)
            {
                abilityStartSprite.enabled = true;
                abilityStartSprite.idle = false;
                abilityStartSprite.loop = false;
                activeSprite = abilityStartSprite;
                ForceNoFlipOn(abilityStartSprite);
            }

            return;
        }

        if (state == VisualState.Hidden)
        {
            activeSprite = null;
            ForceSpriteRendererOff(walkSprite);
            ForceSpriteRendererOff(abilityStartSprite);
            ForceSpriteRendererOff(abilityEndSprite);
            return;
        }

        if (state == VisualState.AbilityEnd)
        {
            if (abilityEndSprite != null)
            {
                abilityEndSprite.enabled = true;
                abilityEndSprite.idle = false;
                abilityEndSprite.loop = false;
                activeSprite = abilityEndSprite;
                ForceNoFlipOn(abilityEndSprite);
            }

            return;
        }
    }

    private void ConfigureAsOneShot(AnimatedSpriteRenderer r)
    {
        if (r == null)
            return;

        r.idle = false;
        r.loop = false;
        r.CurrentFrame = 0;
        r.RefreshFrame();

        ForceNoFlipOn(r);
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

        bool canCount =
            walkSprite != null &&
            walkSprite.enabled &&
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

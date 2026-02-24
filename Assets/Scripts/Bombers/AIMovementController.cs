using UnityEngine;

public class AIMovementController : MovementController
{
    [Header("AI Control")]
    public Vector2 aiDirection = Vector2.zero;
    public bool isBoss = false;

    [Header("Anti-Stuck")]
    [SerializeField] private float stuckSeconds = 0.30f;
    [SerializeField] private float stuckMoveEpsilon = 0.0015f;
    [SerializeField] private bool avoidWhenForwardBlocked = true;

    [Header("Decision Smoothing")]
    [SerializeField] private bool onlyTurnNearTileCenter = true;
    [SerializeField] private float turnCenterEpsilon = 0.08f;
    [SerializeField] private float commitSeconds = 0.12f;
    [SerializeField] private float minTurnInterval = 0.08f;

    [Header("Hazard Awareness")]
    [SerializeField] private bool avoidActiveExplosions = true;
    [SerializeField] private bool avoidBombTiles = false;
    [SerializeField] private string explosionLayerName = "Explosion";
    [SerializeField] private string bombLayerName = "Bomb";
    [SerializeField] private float hazardCheckBoxSize = 0.42f;

    [Header("Damaged Visual (optional)")]
    [SerializeField] private AnimatedSpriteRenderer spriteRendererDamaged;
    [SerializeField] private bool useDamagedRendererWhenHealthUsesDamagedLoop = true;

    [Header("Boss Intro Idle Visual (optional)")]
    [SerializeField] private AnimatedSpriteRenderer spriteRendererIntroIdle;
    [SerializeField] private bool introIdleLoop = true;

    private Vector2 lastPos;
    private float stuckTimer;

    private Vector2 committedDir = Vector2.zero;
    private Vector2 lastDetour = Vector2.zero;
    private float commitUntilTime;
    private float lastTurnTime;

    private int iaExplosionLayer;
    private int iaBombLayer;
    private int iaExplosionMask;
    private int iaBombMask;

    private CharacterHealth healthForDamaged;
    private bool damagedVisualActive;

    private bool introIdleVisualActive;

    protected override void Awake()
    {
        base.Awake();

        lastPos = (Rigidbody != null) ? Rigidbody.position : (Vector2)transform.position;
        stuckTimer = 0f;

        committedDir = Vector2.zero;
        lastDetour = Vector2.zero;
        commitUntilTime = 0f;
        lastTurnTime = -999f;

        iaExplosionLayer = LayerMask.NameToLayer(explosionLayerName);
        iaBombLayer = LayerMask.NameToLayer(bombLayerName);

        iaExplosionMask = (iaExplosionLayer >= 0) ? (1 << iaExplosionLayer) : 0;
        iaBombMask = (iaBombLayer >= 0) ? (1 << iaBombLayer) : 0;

        if (isBoss)
        {
            if (obstacleMask.value == 0)
                obstacleMask = LayerMask.GetMask("Bomb", "Stage", "Enemy");
        }

        CacheHealthEvents();

        EndDamagedVisual();
        EndIntroIdleVisual();
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        lastPos = (Rigidbody != null) ? Rigidbody.position : (Vector2)transform.position;
        stuckTimer = 0f;

        committedDir = Vector2.zero;
        lastDetour = Vector2.zero;
        commitUntilTime = 0f;
        lastTurnTime = -999f;

        CacheHealthEvents();

        EndDamagedVisual();
    }

    protected override void OnDisable()
    {
        UnhookHealthEvents();
        EndDamagedVisual();
        EndIntroIdleVisual();
        base.OnDisable();
    }

    void LateUpdate()
    {
        if (damagedVisualActive || introIdleVisualActive)
            return;

        Vector2 pos = (Rigidbody != null) ? Rigidbody.position : (Vector2)transform.position;

        float moved = (pos - lastPos).sqrMagnitude;
        if (moved > (stuckMoveEpsilon * stuckMoveEpsilon))
        {
            stuckTimer = 0f;
            lastPos = pos;
            return;
        }

        if (aiDirection.sqrMagnitude > 0.01f && !inputLocked && !GamePauseController.IsPaused && !isDead)
            stuckTimer += Time.deltaTime;
        else
            stuckTimer = 0f;
    }

    protected override void HandleInput()
    {
        if (damagedVisualActive)
        {
            committedDir = Vector2.zero;
            lastDetour = Vector2.zero;

            aiDirection = Vector2.zero;

            ApplyDirectionFromVector(Vector2.zero);

            if (Rigidbody != null)
                Rigidbody.linearVelocity = Vector2.zero;

            return;
        }

        if (introIdleVisualActive)
        {
            committedDir = Vector2.zero;
            lastDetour = Vector2.zero;

            aiDirection = Vector2.zero;

            ApplyDirectionFromVector(Vector2.zero);

            if (Rigidbody != null)
                Rigidbody.linearVelocity = Vector2.zero;

            return;
        }

        Vector2 desired = NormalizeCardinal(aiDirection);

        if (desired == Vector2.zero)
        {
            committedDir = Vector2.zero;
            lastDetour = Vector2.zero;
            ApplyDirectionFromVector(Vector2.zero);
            return;
        }

        bool isStuck = stuckTimer >= stuckSeconds;
        bool forwardBlocked = avoidWhenForwardBlocked && IsMoveBlocked(desired);

        if (Time.time < commitUntilTime && committedDir != Vector2.zero)
        {
            Vector2 safeCommitted = FilterUnsafeMove(committedDir);
            committedDir = safeCommitted;
            ApplyDirectionFromVector(safeCommitted);
            return;
        }

        bool nearCenter = !onlyTurnNearTileCenter || IsNearTileCenter();

        if (!nearCenter)
        {
            if (committedDir == Vector2.zero)
                committedDir = desired;

            Vector2 safeHold = FilterUnsafeMove(committedDir);
            committedDir = safeHold;
            ApplyDirectionFromVector(safeHold);
            return;
        }

        bool canTurnNow = (Time.time - lastTurnTime) >= minTurnInterval;

        if (canTurnNow && (forwardBlocked || isStuck))
        {
            Vector2 detour = PickDetourStable(desired);

            if (detour != Vector2.zero)
            {
                detour = FilterUnsafeMove(detour);
                Commit(detour);
                ApplyDirectionFromVector(detour);
                return;
            }

            Vector2 safeDesired = FilterUnsafeMove(desired);
            Commit(safeDesired);
            ApplyDirectionFromVector(safeDesired);
            return;
        }

        if (canTurnNow)
        {
            Vector2 safeDesired = FilterUnsafeMove(desired);

            if (committedDir != safeDesired)
                Commit(safeDesired);

            ApplyDirectionFromVector(safeDesired);
            return;
        }

        if (committedDir == Vector2.zero)
            committedDir = desired;

        Vector2 safeContinue = FilterUnsafeMove(committedDir);
        committedDir = safeContinue;
        ApplyDirectionFromVector(safeContinue);
    }

    private Vector2 FilterUnsafeMove(Vector2 dir)
    {
        if (dir == Vector2.zero)
            return Vector2.zero;

        Vector2 pos = (Rigidbody != null) ? Rigidbody.position : (Vector2)transform.position;

        float t = Mathf.Max(0.0001f, tileSize);
        Vector2 nextTile = new Vector2(
            Mathf.Round((pos.x + dir.x * t) / t) * t,
            Mathf.Round((pos.y + dir.y * t) / t) * t
        );

        if (avoidActiveExplosions && IsTileWithExplosion(nextTile))
            return Vector2.zero;

        if (avoidBombTiles && IsTileWithBomb(nextTile))
            return Vector2.zero;

        return dir;
    }

    private bool IsTileWithExplosion(Vector2 tileCenter)
    {
        if (iaExplosionMask == 0)
            return false;

        float s = Mathf.Max(0.1f, hazardCheckBoxSize);
        return Physics2D.OverlapBox(tileCenter, Vector2.one * s, 0f, iaExplosionMask) != null;
    }

    private bool IsTileWithBomb(Vector2 tileCenter)
    {
        if (iaBombMask == 0)
            return false;

        float s = Mathf.Max(0.1f, hazardCheckBoxSize);
        return Physics2D.OverlapBox(tileCenter, Vector2.one * s, 0f, iaBombMask) != null;
    }

    private void Commit(Vector2 dir)
    {
        committedDir = dir;
        commitUntilTime = Time.time + Mathf.Max(0.01f, commitSeconds);
        lastTurnTime = Time.time;
        stuckTimer = 0f;
    }

    private bool IsNearTileCenter()
    {
        Vector2 pos = (Rigidbody != null) ? Rigidbody.position : (Vector2)transform.position;

        float t = Mathf.Max(0.0001f, tileSize);
        float cx = Mathf.Round(pos.x / t) * t;
        float cy = Mathf.Round(pos.y / t) * t;

        float eps = Mathf.Max(0.001f, turnCenterEpsilon);
        return Mathf.Abs(pos.x - cx) <= eps && Mathf.Abs(pos.y - cy) <= eps;
    }

    private Vector2 PickDetourStable(Vector2 desired)
    {
        Vector2 perpA, perpB;
        if (Mathf.Abs(desired.x) > 0.01f)
        {
            perpA = Vector2.up;
            perpB = Vector2.down;
        }
        else
        {
            perpA = Vector2.left;
            perpB = Vector2.right;
        }

        if (lastDetour != Vector2.zero && IsMoveOpen(lastDetour) && IsDetourSafe(lastDetour))
            return lastDetour;

        bool aOpen = IsMoveOpen(perpA) && IsDetourSafe(perpA);
        bool bOpen = IsMoveOpen(perpB) && IsDetourSafe(perpB);

        if (aOpen && !bOpen)
        {
            lastDetour = perpA;
            return perpA;
        }

        if (bOpen && !aOpen)
        {
            lastDetour = perpB;
            return perpB;
        }

        if (aOpen && bOpen)
        {
            Vector2 pos = (Rigidbody != null) ? Rigidbody.position : (Vector2)transform.position;
            int tx = Mathf.RoundToInt(pos.x / Mathf.Max(0.0001f, tileSize));
            int ty = Mathf.RoundToInt(pos.y / Mathf.Max(0.0001f, tileSize));
            Vector2 pick = (((tx + ty) & 1) == 0) ? perpA : perpB;

            lastDetour = pick;
            return pick;
        }

        Vector2 back = -desired;
        if (IsMoveOpen(back) && IsDetourSafe(back))
        {
            lastDetour = Vector2.zero;
            return back;
        }

        lastDetour = Vector2.zero;
        return Vector2.zero;
    }

    private bool IsDetourSafe(Vector2 dir)
    {
        if (dir == Vector2.zero)
            return false;

        Vector2 safe = FilterUnsafeMove(dir);
        return safe != Vector2.zero;
    }

    public void SetAIDirection(Vector2 dir)
    {
        aiDirection = dir;
    }

    public void SetIntroIdle(bool on)
    {
        if (on) BeginIntroIdleVisual();
        else EndIntroIdleVisual();
    }

    private void BeginIntroIdleVisual()
    {
        if (introIdleVisualActive)
            return;

        introIdleVisualActive = true;

        committedDir = Vector2.zero;
        lastDetour = Vector2.zero;
        commitUntilTime = 0f;
        stuckTimer = 0f;
        aiDirection = Vector2.zero;

        ApplyDirectionFromVector(Vector2.zero);

        if (Rigidbody != null)
            Rigidbody.linearVelocity = Vector2.zero;

        if (spriteRendererIntroIdle == null)
            return;

        SetVisualOverrideActive(true);
        SetAllSpritesVisible(false);

        SetAnimEnabledLocal(spriteRendererIntroIdle, true);
        spriteRendererIntroIdle.idle = false;
        spriteRendererIntroIdle.loop = introIdleLoop;
        spriteRendererIntroIdle.pingPong = false;
        spriteRendererIntroIdle.RefreshFrame();

        activeSpriteRenderer = spriteRendererIntroIdle;

        ApplyFlipForHorizontal(FacingDirection);
    }

    private void EndIntroIdleVisual()
    {
        if (!introIdleVisualActive)
            return;

        introIdleVisualActive = false;

        if (spriteRendererIntroIdle != null)
            SetAnimEnabledLocal(spriteRendererIntroIdle, false);

        SetVisualOverrideActive(false);
    }

    private void CacheHealthEvents()
    {
        if (healthForDamaged != null)
            return;

        healthForDamaged = GetComponent<CharacterHealth>();
        if (healthForDamaged == null)
            return;

        healthForDamaged.HitInvulnerabilityStarted += OnHitInvulnerabilityStarted;
        healthForDamaged.HitInvulnerabilityEnded += OnHitInvulnerabilityEnded;
        healthForDamaged.Died += OnHealthDied;
    }

    private void UnhookHealthEvents()
    {
        if (healthForDamaged == null)
            return;

        healthForDamaged.HitInvulnerabilityStarted -= OnHitInvulnerabilityStarted;
        healthForDamaged.HitInvulnerabilityEnded -= OnHitInvulnerabilityEnded;
        healthForDamaged.Died -= OnHealthDied;

        healthForDamaged = null;
    }

    private void OnHitInvulnerabilityStarted(float seconds)
    {
        if (!useDamagedRendererWhenHealthUsesDamagedLoop)
            return;

        if (isDead || IsEndingStage)
            return;

        if (healthForDamaged == null || !healthForDamaged.playDamagedLoopInsteadOfBlink)
            return;

        if (spriteRendererDamaged == null)
            return;

        BeginDamagedVisual();
    }

    private void OnHitInvulnerabilityEnded()
    {
        EndDamagedVisual();
    }

    private void OnHealthDied()
    {
        EndDamagedVisual();
    }

    private void BeginDamagedVisual()
    {
        if (damagedVisualActive)
            return;

        damagedVisualActive = true;

        if (introIdleVisualActive)
            EndIntroIdleVisual();

        committedDir = Vector2.zero;
        lastDetour = Vector2.zero;
        commitUntilTime = 0f;
        stuckTimer = 0f;
        aiDirection = Vector2.zero;

        ApplyDirectionFromVector(Vector2.zero);

        if (Rigidbody != null)
            Rigidbody.linearVelocity = Vector2.zero;

        SetVisualOverrideActive(true);
        SetAllSpritesVisible(false);

        SetAnimEnabledLocal(spriteRendererDamaged, true);
        spriteRendererDamaged.idle = false;
        spriteRendererDamaged.loop = true;
        spriteRendererDamaged.pingPong = false;
        spriteRendererDamaged.RefreshFrame();

        activeSpriteRenderer = spriteRendererDamaged;

        ApplyFlipForHorizontal(FacingDirection);
    }

    private void EndDamagedVisual()
    {
        if (!damagedVisualActive)
            return;

        damagedVisualActive = false;

        if (spriteRendererDamaged != null)
            SetAnimEnabledLocal(spriteRendererDamaged, false);

        SetVisualOverrideActive(false);
    }

    private static void SetAnimEnabledLocal(AnimatedSpriteRenderer r, bool on)
    {
        if (r == null) return;

        r.enabled = on;

        if (r.TryGetComponent(out SpriteRenderer sr) && sr != null)
            sr.enabled = on;
    }
}
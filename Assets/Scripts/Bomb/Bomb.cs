using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(AnimatedSpriteRenderer))]
public class Bomb : MonoBehaviour, IMagnetPullable
{
    [Header("SFX")]
    public AudioClip punchSfx;
    [Range(0f, 1f)] public float punchSfxVolume = 1f;
    public AudioClip bounceSfx;
    [Range(0f, 1f)] public float bounceSfxVolume = 1f;
    [Range(0f, 1f)] public float loseItemSfxVolume = 1f;
    private static AudioClip magnetPullSfx;
    [Range(0f, 1f)] public float magnetPullSfxVolume = 1f;
    private static float bombSfxBlockedUntil = 0f;
    private static readonly object bombSfxGate = new();
    private static readonly object kickSfxGate = new();
    private static AudioSource kickSfxCurrentSource;
    private const string RubberBounceClipResourcesPath = "Sounds/RubberBombBounce";
    private const string LoseItemClipResourcesPath = "Sounds/Lose Item";
    private static AudioClip cachedRubberBounceClip;
    private static AudioClip cachedLoseItemClip;

    [Header("Kick")]
    public float kickSpeed = 10f;

    [Header("Punch")]
    public float punchDuration = 0.22f;
    public float punchArcHeight = 0.9f;
    [SerializeField, Min(0.01f)] private float punchMinSegmentDuration = 0.28f;
    [SerializeField, Min(0f)] private float punchSecondsPerTile = 0.055f;
    [SerializeField, Min(0.01f)] private float punchLandingBounceSpeedMultiplier = 2f;
    [SerializeField, Min(1)] private int punchPixelsPerUnit = 16;

    [Header("Chain Explosion")]
    public float chainStepDelay = 0.1f;

    public float FuseSeconds = 2f;
    public bool IsFusePaused => fusePaused;

    [Header("Stage Wrap / Tilemaps")]
    [SerializeField] private Tilemap stageBoundsTilemap;
    [SerializeField] private Tilemap indestructibleTilemap;

    [SerializeField] private float magnetPullSpeed = 10f;

    private BombController owner;
    public BombController Owner => owner;

    public bool HasExploded { get; private set; }
    public float PlacedTime { get; private set; }
    public bool IsControlBomb { get; set; }

    public bool IsBeingKicked => isKicked;
    public bool IsBeingPunched => isPunched;
    public bool WasMovedByKickOrPunch { get; private set; }
    public bool IsBeingHeldByPowerGlove { get; private set; }

    public bool IsSolid => bombCollider != null && !bombCollider.isTrigger;
    public bool CanBeKickedEarly => !HasExploded && !isKicked && !isPunched && !IsBeingHeldByPowerGlove;
    public bool CanBeKicked => !HasExploded && !isKicked && !IsBeingHeldByPowerGlove && IsSolid && charactersInside.Count == 0;
    public bool CanBePunched => !HasExploded && !isKicked && !isPunched && !IsBeingHeldByPowerGlove && IsSolid && charactersInside.Count == 0;
    public bool CanBeMagnetPulled => !HasExploded && !IsBeingKicked && !IsBeingPunched && !IsBeingHeldByPowerGlove && !IsBeingMagnetPulled;
    public bool IsPierceBomb { get; set; }
    public bool IsPowerBomb { get; set; }
    public bool IsRubberBomb { get; set; }
    public bool IsRevengeBomb { get; set; }
    public int ExplosionRadiusOverride { get; set; }

    public bool IsBeingMagnetPulled => magnetRoutine != null;

    public static readonly HashSet<Bomb> ActiveBombs = new();

    public float ApproxRadius { get; private set; } = 0.5f;

    private Collider2D bombCollider;
    private Rigidbody2D rb;
    private AnimatedSpriteRenderer anim;
    private AudioSource audioSource;

    private bool stageBoundsReady;
    private BoundsInt stageCellBounds;

    private bool isKicked;
    private bool isPunched;

    private Vector2 kickDirection;
    private float kickTileSize = 1f;
    private LayerMask kickObstacleMask;
    private Tilemap kickDestructibleTilemap;
    private IIndestructibleKickedBombHandler[] kickIndestructibleHandlers;

    private Coroutine kickRoutine;
    private Coroutine punchRoutine;

    private Vector2 currentTileCenter;
    private Vector2 lastPos;

    private readonly HashSet<Collider2D> charactersInside = new();

    private bool fusePaused;
    private float fusePauseStartedAt;
    private Coroutine fuseRoutine;

    private bool lockWorldPosActive;
    private Vector2 lockWorldPos;

    private Coroutine magnetRoutine;
    private float magnetSpeedMultiplier = 1f;

    private static readonly WaitForFixedUpdate waitFixed = new();
    private static readonly Collider2D[] magnetBlockCheckBuffer = new Collider2D[16];

    private GameObject magnetOriginBlocker;

    private GameObject kickOriginBlocker;
    private LayerMask kickBlockMoveMask;
    private float kickOverlapBoxSize = 0.60f;
    private float kickOriginBlockerSize = 0.90f;
    private bool kickOriginBlockerUseTrigger = false;
    private readonly List<ItemPickup> kickPushedSkulls = new();

    private int stageLayer;
    private int stageMask;
    private const string TagDestructibles = "Destructibles";

    private void Awake()
    {
        bombCollider = GetComponent<Collider2D>();
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<AnimatedSpriteRenderer>();
        audioSource = GetComponent<AudioSource>();

        bombCollider.isTrigger = true;

        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        if (audioSource != null)
        {
            audioSource.playOnAwake = false;
            audioSource.loop = false;
        }

        if (magnetPullSfx == null)
            magnetPullSfx = Resources.Load<AudioClip>("Sounds/magnetbomb");

        if (cachedRubberBounceClip == null)
            cachedRubberBounceClip = Resources.Load<AudioClip>(RubberBounceClipResourcesPath);

        if (cachedLoseItemClip == null)
            cachedLoseItemClip = Resources.Load<AudioClip>(LoseItemClipResourcesPath);

        lastPos = rb.position;

        stageLayer = LayerMask.NameToLayer("Stage");
        stageMask = stageLayer >= 0 ? (1 << stageLayer) : 0;

        if (TryGetComponent<CircleCollider2D>(out var cc))
        {
            float sx = Mathf.Abs(transform.lossyScale.x);
            float sy = Mathf.Abs(transform.lossyScale.y);
            float s = Mathf.Max(sx, sy);
            ApproxRadius = Mathf.Max(0.01f, cc.radius * s);
        }
        else
        {
            ApproxRadius = 0.5f;
        }

        ResolveIndestructibleTilemapIfNeeded();
        ResolveStageBoundsTilemapIfNeeded();
    }

    private void OnEnable()
    {
        ActiveBombs.Add(this);
    }

    private void OnDisable()
    {
        ActiveBombs.Remove(this);
    }

    private void OnDestroy()
    {
        ActiveBombs.Remove(this);
    }

    private void FixedUpdate()
    {
        if (!lockWorldPosActive)
            return;

        if (rb != null)
        {
            rb.position = lockWorldPos;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        transform.position = lockWorldPos;
    }

    public void BeginFuse()
    {
        if (HasExploded) return;
        if (IsControlBomb) return;

        if (fuseRoutine != null)
            StopCoroutine(fuseRoutine);

        fuseRoutine = StartCoroutine(FuseRoutine());
    }

    private IEnumerator FuseRoutine()
    {
        while (!HasExploded)
        {
            if (owner == null)
            {
                fuseRoutine = null;
                yield break;
            }

            float rem = RemainingFuseSeconds;

            if (fusePaused)
            {
                yield return null;
                continue;
            }

            if (rem <= 0f)
            {
                owner.ExplodeBomb(gameObject);
                fuseRoutine = null;
                yield break;
            }

            yield return null;
        }

        fuseRoutine = null;
    }

    public void SetFusePaused(bool pause)
    {
        if (HasExploded)
            return;

        if (pause)
            PauseFuse();
        else
            ResumeFuse();
    }

    private void PauseFuse()
    {
        if (fusePaused)
            return;

        fusePaused = true;
        fusePauseStartedAt = Time.time;
    }

    private void ResumeFuse()
    {
        if (!fusePaused)
            return;

        float paused = Time.time - fusePauseStartedAt;
        PlacedTime += paused;

        fusePaused = false;
        fusePauseStartedAt = 0f;
    }

    public void ForceStopExternalMovementAndSnap(Vector2 snapWorldPos)
    {
        StopKickPunchMagnetRoutines();

        isKicked = false;
        isPunched = false;

        snapWorldPos.x = Mathf.Round(snapWorldPos.x);
        snapWorldPos.y = Mathf.Round(snapWorldPos.y);

        currentTileCenter = snapWorldPos;
        lastPos = snapWorldPos;

        if (rb != null)
            rb.position = snapWorldPos;

        transform.position = snapWorldPos;
    }

    public void StopKickPunchMagnetRoutines()
    {
        if (kickRoutine != null)
        {
            StopCoroutine(kickRoutine);
            kickRoutine = null;
        }

        if (punchRoutine != null)
        {
            StopCoroutine(punchRoutine);
            punchRoutine = null;
        }

        if (magnetRoutine != null)
        {
            StopCoroutine(magnetRoutine);
            magnetRoutine = null;
        }

        RemoveMagnetOriginBlocker();
        RemoveKickOriginBlocker();
    }

    public void SetPowerGloveHeld(bool held)
    {
        IsBeingHeldByPowerGlove = held;

        if (!held)
            return;

        charactersInside.Clear();

        if (bombCollider != null)
            bombCollider.isTrigger = true;
    }

    private void RecalculateCharactersInsideAt(Vector2 worldPos)
    {
        charactersInside.Clear();

        int charMask = LayerMask.GetMask("Player", "Enemy");

        float searchRadius = Mathf.Max(ApproxRadius + 0.35f, 0.75f);
        Collider2D[] cols = Physics2D.OverlapCircleAll(worldPos, searchRadius, charMask);

        if (cols == null || cols.Length == 0)
            return;

        for (int i = 0; i < cols.Length; i++)
        {
            var c = cols[i];
            if (c == null)
                continue;

            if (!IsCharacterLayer(c.gameObject.layer))
                continue;

            if (IsCharacterStillOccupyingBomb(c, worldPos))
                charactersInside.Add(c);
        }

        LogBombEscape(
            $"RecalculateCharactersInsideAt worldPos:{worldPos} " +
            $"searchRadius:{searchRadius:F3} count:{charactersInside.Count}",
            verbose: true);
    }

    private bool TileHasBomb(Vector2 pos)
    {
        int bombLayer = LayerMask.NameToLayer("Bomb");
        int bombMask = 1 << bombLayer;

        Collider2D hit = Physics2D.OverlapBox(pos, Vector2.one * 0.6f, 0f, bombMask);
        return hit != null && hit.gameObject != gameObject;
    }

    private bool TileHasCharacter(Vector2 pos, int charMask)
    {
        Collider2D hit = Physics2D.OverlapBox(pos, Vector2.one * (kickTileSize * 0.6f), 0f, charMask);
        return hit != null;
    }

    private bool TileHasCharacter(Vector2 pos)
    {
        int charMask = LayerMask.GetMask("Player", "Enemy");
        return TileHasCharacter(pos, charMask);
    }

    private void StunMovementControllersAtTile(Vector2 tileCenter, float seconds)
    {
        int charMask = LayerMask.GetMask("Player", "Enemy");

        Collider2D[] hits = Physics2D.OverlapBoxAll(tileCenter, Vector2.one * (kickTileSize * 0.6f), 0f, charMask);
        if (hits == null || hits.Length == 0)
            return;

        HashSet<StunReceiver> stunned = null;

        for (int i = 0; i < hits.Length; i++)
        {
            var h = hits[i];
            if (h == null)
                continue;

            var mc = h.GetComponentInParent<MovementController>();
            if (mc == null)
                continue;

            var sr = h.GetComponentInParent<StunReceiver>();
            if (sr == null)
                continue;

            stunned ??= new HashSet<StunReceiver>();
            if (!stunned.Add(sr))
                continue;

            sr.Stun(seconds);

            if (mc.CompareTag("Player") &&
                PlayerPersistentStats.StageTryExpelRandomPersistentItem(
                    mc,
                    mc.GetComponent<BombController>(),
                    out var expelledType))
            {
                PlayLoseItemSfx(mc);
                SpawnExpelledPersistentItem(mc, expelledType);
            }
        }
    }

    private void PlayLoseItemSfx(MovementController movementController)
    {
        if (cachedLoseItemClip == null)
            cachedLoseItemClip = Resources.Load<AudioClip>(LoseItemClipResourcesPath);

        if (cachedLoseItemClip == null)
            return;

        AudioSource source = null;
        if (movementController != null)
        {
            source = movementController.GetComponent<AudioSource>();
            if (source == null)
                source = movementController.GetComponentInChildren<AudioSource>(true);
        }

        source ??= audioSource;

        if (source != null)
            source.PlayOneShot(cachedLoseItemClip, loseItemSfxVolume);
    }

    private void SpawnExpelledPersistentItem(MovementController movementController, ItemType itemType)
    {
        if (movementController == null)
            return;

        ItemPickup prefab = AutoItemDatabase.Get(itemType);
        if (prefab == null)
            return;

        Vector2 origin = movementController.transform.position;
        float tileSize = Mathf.Max(0.0001f, movementController.tileSize);
        Vector2 direction = RandomCardinalDir();

        var item = Instantiate(prefab, origin, Quaternion.identity);
        if (item == null)
            return;

        Collider2D ignoredCollider = movementController.GetComponent<Collider2D>();
        if (ignoredCollider == null)
            ignoredCollider = movementController.GetComponentInChildren<Collider2D>();

        item.TryExpelItem(direction, tileSize, ignoredCollider, 3);
    }

    private bool IsKickBlocked(Vector2 target)
    {
        Vector2 size = Vector2.one * (kickTileSize * 0.6f);
        Collider2D[] hits = Physics2D.OverlapBoxAll(target, size, 0f, kickObstacleMask);

        int enemyLayer = LayerMask.NameToLayer("Enemy");
        int bombLayer = LayerMask.NameToLayer("Bomb");

        foreach (var hit in hits)
        {
            if (hit == null || hit.gameObject == gameObject)
                continue;

            if (IsHoleCollider(hit))
                continue;

            if (hit.gameObject.layer == enemyLayer)
                return true;

            if (hit.gameObject.layer == bombLayer)
                return true;

            if (hit.isTrigger)
                continue;

            return true;
        }

        if (TileHasBomb(target))
            return true;

        if (kickDestructibleTilemap != null)
        {
            Vector3Int cell = kickDestructibleTilemap.WorldToCell(target);
            if (kickDestructibleTilemap.GetTile(cell) != null)
                return true;
        }

        if (HasIndestructibleAt(target))
            return true;

        return false;
    }

    private bool IsPunchLandingBlocked(Vector2 target)
    {
        Vector2 size = Vector2.one * (kickTileSize * 0.6f);
        Collider2D[] hits = Physics2D.OverlapBoxAll(target, size, 0f, kickObstacleMask);

        int enemyLayer = LayerMask.NameToLayer("Enemy");
        int bombLayer = LayerMask.NameToLayer("Bomb");

        foreach (var hit in hits)
        {
            if (hit == null || hit.gameObject == gameObject)
                continue;

            if (IsHoleCollider(hit))
                continue;

            if (hit.gameObject.layer == enemyLayer)
                return true;

            if (hit.gameObject.layer == bombLayer)
                return true;

            if (hit.isTrigger)
                continue;

            return true;
        }

        if (TileHasBomb(target))
            return true;

        if (TileHasCharacter(target))
            return true;

        if (kickDestructibleTilemap != null)
        {
            Vector3Int cell = kickDestructibleTilemap.WorldToCell(target);
            if (kickDestructibleTilemap.GetTile(cell) != null)
                return true;
        }

        if (HasIndestructibleAt(target))
            return true;

        return false;
    }

    public bool StartPunch(
        Vector2 direction,
        float tileSize,
        int distanceTiles,
        LayerMask obstacleMask,
        Tilemap destructibleTilemap,
        float visualStartYOffset = 0f,
        Vector2? logicalOriginOverride = null,
        float? arcHeightOverride = null,
        float? bounceArcHeightOverride = null)
    {
        bool canEarlyPunch = !HasExploded && !isKicked && !isPunched;

        bool willPunch = CanBePunched || canEarlyPunch;

        if (!willPunch || direction == Vector2.zero)
        {
            return false;
        }

        kickDirection = direction.normalized;
        kickTileSize = tileSize;

        kickObstacleMask = obstacleMask | LayerMask.GetMask("Enemy", "Player");
        kickDestructibleTilemap = destructibleTilemap;

        Vector2 logicalOrigin = logicalOriginOverride ?? (Vector2)transform.position;
        logicalOrigin = SnapToGrid(logicalOrigin, tileSize);

        currentTileCenter = logicalOrigin;
        lastPos = logicalOrigin;

        float yOff = Mathf.Max(0f, visualStartYOffset);
        Vector2 visualStart = yOff > 0f ? (logicalOrigin + Vector2.up * yOff) : logicalOrigin;

        rb.position = visualStart;
        transform.position = visualStart;

        if (kickRoutine != null)
            StopCoroutine(kickRoutine);

        if (punchRoutine != null)
            StopCoroutine(punchRoutine);

        RemoveKickOriginBlocker();

        WasMovedByKickOrPunch = true;
        isPunched = true;

        charactersInside.Clear();
        bombCollider.isTrigger = true;

        SetAirborneColliderSuppressed(true);
        PauseFuse();

        TryPlayBombSfx_NoOverlap(punchSfx, punchSfxVolume);

        float dur = Mathf.Max(punchDuration, Time.fixedDeltaTime);
        float arcHeight = arcHeightOverride.HasValue
            ? Mathf.Max(0f, arcHeightOverride.Value)
            : punchArcHeight;
        float bounceArcHeight = bounceArcHeightOverride.HasValue
            ? Mathf.Max(0f, bounceArcHeightOverride.Value)
            : arcHeight;

        punchRoutine = StartCoroutine(PunchRoutineFixed_Hybrid(
            logicalOrigin,
            yOff,
            distanceTiles,
            80,
            dur,
            arcHeight,
            bounceArcHeight));

        return true;
    }

    private IEnumerator PunchRoutineFixed_Hybrid(
        Vector2 logicalOrigin,
        float visualStartYOffset,
        int forwardSteps,
        int maxExtraBounces,
        float duration,
        float arcHeight,
        float bounceArcHeight)
    {
        Vector2 cur = logicalOrigin;
        Vector2 lastPickupImpactDirection = kickDirection;
        int steps = Mathf.Max(1, forwardSteps);
        float forwardDuration = GetPunchSegmentDuration(duration, steps);
        float bounceDuration = GetPunchSegmentDuration(duration, 1);
        float landingBounceDuration = bounceDuration / Mathf.Max(0.01f, punchLandingBounceSpeedMultiplier);

        bool wrapsDuringForward = false;
        {
            Vector2 sim = cur;
            for (int i = 0; i < steps; i++)
            {
                if (!TryStepWithWrap(sim, out var n, out var didWrap))
                    break;

                if (didWrap)
                {
                    wrapsDuringForward = true;
                    break;
                }

                sim = n;
            }
        }

        bool applyYOffsetOnThisSegment = visualStartYOffset > 0f;

        Vector2 SegmentStart(Vector2 baseStart)
        {
            if (!applyYOffsetOnThisSegment)
                return baseStart;

            applyYOffsetOnThisSegment = false;
            return baseStart + Vector2.up * visualStartYOffset;
        }

        if (!wrapsDuringForward)
        {
            Vector2 target = cur;

            for (int i = 0; i < steps; i++)
            {
                if (!TryStepWithWrap(target, out var n, out var didWrap))
                    goto FINISH;

                if (didWrap)
                {
                    TeleportTo(n);
                    target = n;

                    if (NotifyOwnerAt(target))
                    {
                        cur = target;
                        goto FINISH;
                    }

                    continue;
                }

                target = n;
            }

            if (HasExploded)
                goto FINISH;

            yield return PunchArcSegmentFixed(SegmentStart(cur), target, forwardDuration, arcHeight);
            cur = target;

            if (NotifyOwnerAt(cur))
                goto FINISH;
        }
        else
        {
            for (int i = 0; i < steps; i++)
            {
                if (HasExploded)
                    goto FINISH;

                if (!TryStepWithWrap(cur, out var next, out var didWrap))
                    goto FINISH;

                if (didWrap)
                {
                    TeleportTo(next);
                    cur = next;

                    if (NotifyOwnerAt(cur))
                        goto FINISH;

                    continue;
                }

                yield return PunchArcSegmentFixed(SegmentStart(cur), next, bounceDuration, bounceArcHeight);
                cur = next;

                if (NotifyOwnerAt(cur))
                    goto FINISH;
            }
        }

        int extraLimit = Mathf.Max(0, maxExtraBounces);

        if (IsRubberBomb)
        {
            int minBounces = Random.Range(3, 6);
            int bouncesDone = 0;

            for (int b = 0; b < extraLimit; b++)
            {
                if (HasExploded)
                    goto FINISH;

                bool safeNow = !IsPunchLandingBlocked(cur);

                if (bouncesDone >= minBounces && safeNow)
                    break;

                Vector2 dir = RandomCardinalDir();

                if (!TryStepWithWrapDir(cur, dir, out var next, out var didWrap))
                    break;

                lastPickupImpactDirection = dir;

                if (didWrap)
                {
                    TeleportTo(next);
                    cur = next;

                    if (NotifyOwnerAt(cur))
                        goto FINISH;

                    b--;
                    continue;
                }

                StunMovementControllersAtTile(cur, 0.5f);
                TryPlayKickSfx_StopOthers(GetKickBounceClip(), bounceSfxVolume);

                yield return PunchArcSegmentFixed(cur, next, landingBounceDuration, bounceArcHeight);
                cur = next;
                bouncesDone++;

                if (NotifyOwnerAt(cur))
                    goto FINISH;
            }
        }
        else
        {
            for (int b = 0; b < extraLimit; b++)
            {
                if (HasExploded)
                    goto FINISH;

                if (!IsPunchLandingBlocked(cur))
                    break;

                if (!TryStepWithWrap(cur, out var next, out var didWrap))
                    break;

                if (didWrap)
                {
                    TeleportTo(next);
                    cur = next;

                    if (NotifyOwnerAt(cur))
                        goto FINISH;

                    b--;
                    continue;
                }

                StunMovementControllersAtTile(cur, 0.5f);
                TryPlayKickSfx_StopOthers(GetKickBounceClip(), bounceSfxVolume);

                yield return PunchArcSegmentFixed(cur, next, landingBounceDuration, bounceArcHeight);
                cur = next;

                if (NotifyOwnerAt(cur))
                    goto FINISH;
            }
        }

    FINISH:
        TeleportTo(cur);
        DestroyPickupsAtWorld(cur, lastPickupImpactDirection);
        FinishKickPushedSkull(cur);

        isPunched = false;
        punchRoutine = null;

        if (fusePaused)
            ResumeFuse();

        if (!HasExploded)
        {
            SetAirborneColliderSuppressed(false);

            RecalculateCharactersInsideAt(cur);
            bombCollider.isTrigger = charactersInside.Count > 0;
        }

        if (anim != null)
        {
            anim.SetFrozen(false);
            anim.RefreshFrame();
        }
    }

    private void EnsureStageBounds()
    {
        if (stageBoundsReady)
            return;

        if (stageBoundsTilemap != null)
        {
            stageBoundsTilemap.CompressBounds();
            stageCellBounds = stageBoundsTilemap.cellBounds;
            stageBoundsReady = true;
            return;
        }

        var tilemaps = FindObjectsByType<Tilemap>();
        Tilemap ground = null;
        Tilemap ind = null;

        for (int i = 0; i < tilemaps.Length; i++)
        {
            var tm = tilemaps[i];
            if (tm == null) continue;

            string n = tm.name.ToLowerInvariant();
            if (ground == null && n.Contains("ground")) ground = tm;
            if (ind == null && n.Contains("indestruct")) ind = tm;
        }

        if (ground != null) ground.CompressBounds();
        if (ind != null) ind.CompressBounds();

        if (ground != null && ind != null)
        {
            var a = ground.cellBounds;
            var b = ind.cellBounds;

            int xMin = Mathf.Min(a.xMin, b.xMin);
            int xMax = Mathf.Max(a.xMax, b.xMax);
            int yMin = Mathf.Min(a.yMin, b.yMin);
            int yMax = Mathf.Max(a.yMax, b.yMax);

            stageCellBounds = new BoundsInt(xMin, yMin, 0, xMax - xMin, yMax - yMin, 1);
            stageBoundsReady = true;

            stageBoundsTilemap = ground;
            if (indestructibleTilemap == null)
                indestructibleTilemap = ind;

            return;
        }

        var fallback = ground != null ? ground : (ind != null ? ind : null);
        if (fallback != null)
        {
            stageCellBounds = fallback.cellBounds;
            stageBoundsReady = true;
            stageBoundsTilemap = fallback;

            if (indestructibleTilemap == null && ind != null)
                indestructibleTilemap = ind;
        }
    }

    private bool TryStepWithWrap(Vector2 from, out Vector2 next, out bool didWrap)
    {
        EnsureStageBounds();

        Vector2 raw = from + kickDirection * kickTileSize;
        didWrap = false;

        if (!stageBoundsReady)
        {
            next = raw;
            return true;
        }

        Vector3Int cell = stageBoundsTilemap != null
            ? stageBoundsTilemap.WorldToCell(raw)
            : new Vector3Int(Mathf.RoundToInt(raw.x / kickTileSize), Mathf.RoundToInt(raw.y / kickTileSize), 0);

        int minX = stageCellBounds.xMin;
        int maxX = stageCellBounds.xMax - 1;
        int minY = stageCellBounds.yMin;
        int maxY = stageCellBounds.yMax - 1;

        if (cell.x < minX) { cell.x = maxX; didWrap = true; }
        else if (cell.x > maxX) { cell.x = minX; didWrap = true; }

        if (cell.y < minY) { cell.y = maxY; didWrap = true; }
        else if (cell.y > maxY) { cell.y = minY; didWrap = true; }

        if (!didWrap)
        {
            next = raw;
            return true;
        }

        if (stageBoundsTilemap != null)
        {
            Vector3 c = stageBoundsTilemap.GetCellCenterWorld(cell);
            c.z = transform.position.z;
            next = (Vector2)c;
        }
        else
        {
            next = new Vector2(cell.x * kickTileSize, cell.y * kickTileSize);
        }

        return true;
    }

    private bool TryStepWithWrapDir(Vector2 from, Vector2 dir, out Vector2 next, out bool didWrap)
    {
        Vector2 prev = kickDirection;
        kickDirection = dir;

        bool ok = TryStepWithWrap(from, out next, out didWrap);

        kickDirection = prev;
        return ok;
    }

    private static Vector2 RandomCardinalDir()
    {
        int r = Random.Range(0, 4);
        return r switch
        {
            0 => Vector2.up,
            1 => Vector2.down,
            2 => Vector2.left,
            _ => Vector2.right,
        };
    }

    private void TeleportTo(Vector2 pos)
    {
        rb.position = pos;
        transform.position = pos;
        SetLogicalTileCenter(pos);
    }

    private void SetLogicalTileCenter(Vector2 pos)
    {
        Vector2 snapped = SnapToGrid(pos, kickTileSize > 0f ? kickTileSize : 1f);
        currentTileCenter = snapped;
        lastPos = snapped;
    }

    private float GetPunchSegmentDuration(float baseDuration, int distanceTiles)
    {
        float distanceDuration = Mathf.Max(1, distanceTiles) * Mathf.Max(0f, punchSecondsPerTile);
        return Mathf.Max(baseDuration, punchMinSegmentDuration, distanceDuration, Time.fixedDeltaTime);
    }

    private Vector2 SnapPunchArcPositionToPixelGrid(Vector2 pos)
    {
        int ppu = Mathf.Max(1, punchPixelsPerUnit);
        pos.x = Mathf.Round(pos.x * ppu) / ppu;
        pos.y = Mathf.Round(pos.y * ppu) / ppu;
        return pos;
    }

    private IEnumerator PunchArcSegmentFixed(Vector2 start, Vector2 end, float duration, float arcHeight)
    {
        duration = Mathf.Max(duration, Time.fixedDeltaTime);

        SetLogicalTileCenter(end);

        float t = 0f;
        float inv = 1f / Mathf.Max(0.0001f, duration);

        while (t < duration)
        {
            if (HasExploded)
                yield break;

            t += Time.fixedDeltaTime;
            float a = Mathf.Clamp01(t * inv);

            Vector2 pos = Vector2.Lerp(start, end, a);
            float arc = Mathf.Sin(a * Mathf.PI) * arcHeight;
            pos.y += arc;
            pos = SnapPunchArcPositionToPixelGrid(pos);

            rb.MovePosition(pos);

            yield return waitFixed;
        }

        rb.position = end;
        transform.position = end;
        SetLogicalTileCenter(end);
    }

    public void SetStageBoundsTilemap(Tilemap tilemap)
    {
        stageBoundsTilemap = tilemap;
        stageBoundsReady = false;
    }

    public void SetIndestructibleTilemap(Tilemap tilemap)
    {
        indestructibleTilemap = tilemap;
    }

    private void ResolveStageBoundsTilemapIfNeeded()
    {
        if (stageBoundsTilemap != null)
            return;

        var tilemaps = FindObjectsByType<Tilemap>();
        if (tilemaps == null || tilemaps.Length == 0)
            return;

        for (int i = 0; i < tilemaps.Length; i++)
        {
            var tm = tilemaps[i];
            if (tm == null)
                continue;

            string n = tm.name.ToLowerInvariant();
            if (n.Contains("ground"))
            {
                stageBoundsTilemap = tm;
                return;
            }
        }

        stageBoundsTilemap = tilemaps[0];
    }

    private void ResolveIndestructibleTilemapIfNeeded()
    {
        if (indestructibleTilemap != null)
            return;

        var tilemaps = FindObjectsByType<Tilemap>();
        if (tilemaps == null || tilemaps.Length == 0)
            return;

        for (int i = 0; i < tilemaps.Length; i++)
        {
            var tm = tilemaps[i];
            if (tm == null)
                continue;

            string n = tm.name.ToLowerInvariant();
            if (n.Contains("indestruct"))
            {
                indestructibleTilemap = tm;
                return;
            }
        }
    }

    public void Initialize(BombController owner)
    {
        ResolveStageBoundsTilemapIfNeeded();
        ResolveIndestructibleTilemapIfNeeded();

        this.owner = owner;
        PlacedTime = Time.time;
        WasMovedByKickOrPunch = false;

        lastPos = rb.position;

        RecalculateCharactersInsideAt(rb.position);
        bombCollider.isTrigger = charactersInside.Count > 0;

        LogBombEscape(
            $"Initialize pos:{rb.position} logical:{lastPos} charactersInside:{charactersInside.Count} " +
            $"isTrigger:{bombCollider.isTrigger} approxRadius:{ApproxRadius:F3}");
    }

    public Vector2 GetLogicalPosition() => lastPos;

    public void ForceSetLogicalPosition(Vector2 worldPos)
    {
        worldPos.x = Mathf.Round(worldPos.x);
        worldPos.y = Mathf.Round(worldPos.y);

        lastPos = worldPos;

        if (rb != null)
            rb.position = worldPos;

        transform.position = worldPos;
    }

    public bool TryRedirectKick(Vector2 direction)
    {
        if (HasExploded || !isKicked || direction == Vector2.zero)
            return false;

        Vector2 cardinalDirection = Mathf.Abs(direction.x) > Mathf.Abs(direction.y)
            ? new Vector2(Mathf.Sign(direction.x), 0f)
            : new Vector2(0f, Mathf.Sign(direction.y));

        if (cardinalDirection == Vector2.zero)
            return false;

        kickDirection = cardinalDirection;
        return true;
    }

    public bool StartKick(Vector2 direction, float tileSize, LayerMask obstacleMask, Tilemap destructibleTilemap)
    {
        LayerMask defaultBlockMoveMask = LayerMask.GetMask("Player", "Bomb", "Enemy", "Louie");
        return StartKick(direction, tileSize, obstacleMask, destructibleTilemap,
            defaultBlockMoveMask, 0.60f, 0.90f, false);
    }

    public bool StartKick(
        Vector2 direction,
        float tileSize,
        LayerMask obstacleMask,
        Tilemap destructibleTilemap,
        LayerMask blockMoveMask,
        float overlapBoxSize,
        float originBlockerSize,
        bool originBlockerUseTrigger)
    {

        if ((!CanBeKicked && !CanBeKickedEarly) || direction == Vector2.zero)
        {
            return false;
        }

        kickDirection = direction.normalized;
        kickTileSize = tileSize;

        kickObstacleMask = obstacleMask | LayerMask.GetMask("Enemy");
        kickDestructibleTilemap = destructibleTilemap;

        kickBlockMoveMask = blockMoveMask;
        kickOverlapBoxSize = Mathf.Clamp(overlapBoxSize, 0.1f, 1.5f);
        kickOriginBlockerSize = Mathf.Clamp(originBlockerSize, 0.2f, 1.2f);
        kickOriginBlockerUseTrigger = originBlockerUseTrigger;

        Vector2 origin = SnapToGrid(rb.position, tileSize);
        Vector2 next = origin + kickDirection * kickTileSize;


        if (IsKickBlocked(next))
        {
            return false;
        }

        if (IsKickBlockedByCharacterOnImmediateExit(origin, next))
        {
            return false;
        }

        if (IsBlockedByMaskAtWorld(next, kickBlockMoveMask, kickOverlapBoxSize))
        {
            return false;
        }

        currentTileCenter = origin;
        lastPos = origin;

        rb.position = origin;
        transform.position = origin;

        if (kickRoutine != null)
            StopCoroutine(kickRoutine);

        isKicked = true;
        WasMovedByKickOrPunch = true;

        kickRoutine = StartCoroutine(KickRoutineFixed());

        return true;
    }

    private IEnumerator KickRoutineFixed()
    {
        while (true)
        {
            if (HasExploded || !isKicked)
                break;

            Vector2 next = currentTileCenter + kickDirection * kickTileSize;

            if (TileHasCharacter(next, LayerMask.GetMask("Player", "Enemy")))
            {
                if (IsRubberBomb)
                {
                    kickDirection = -kickDirection;

                    Vector2 back = currentTileCenter + kickDirection * kickTileSize;

                    if (TileHasCharacter(back, LayerMask.GetMask("Player", "Enemy")))
                        break;

                    if (IsKickBlocked(back))
                        break;

                    TryPlayKickSfx_StopOthers(GetKickBounceClip(), bounceSfxVolume);
                    continue;
                }
                break;
            }

            if (IsKickBlocked(next))
            {
                bool handledByIndestructibleKickHandler = TryHandleIndestructibleKickBounce(
                    next,
                    out AudioClip handledBounceSfx,
                    out float handledBounceSfxVolume);

                if (handledByIndestructibleKickHandler || IsRubberBomb)
                {
                    kickDirection = -kickDirection;

                    Vector2 back = currentTileCenter + kickDirection * kickTileSize;

                    if (IsKickBlocked(back))
                        break;

                    if (TileHasCharacter(back, LayerMask.GetMask("Player", "Enemy")))
                        break;

                    if (handledByIndestructibleKickHandler && IsBlockedByMaskAtWorld(back, kickBlockMoveMask, kickOverlapBoxSize))
                        break;

                    AudioClip bounceClip = handledBounceSfx != null ? handledBounceSfx : GetKickBounceClip();
                    float bounceVolume = handledBounceSfx != null ? handledBounceSfxVolume : bounceSfxVolume;
                    TryPlayKickSfx_StopOthers(bounceClip, bounceVolume);
                    continue;
                }

                break;
            }

            if (IsBlockedByMaskAtWorld(next, kickBlockMoveMask, kickOverlapBoxSize))
            {
                if (IsRubberBomb)
                {
                    kickDirection = -kickDirection;

                    Vector2 back = currentTileCenter + kickDirection * kickTileSize;

                    if (IsKickBlocked(back))
                        break;

                    if (TileHasCharacter(back, LayerMask.GetMask("Player", "Enemy")))
                        break;

                    if (IsBlockedByMaskAtWorld(back, kickBlockMoveMask, kickOverlapBoxSize))
                        break;

                    TryPlayKickSfx_StopOthers(GetKickBounceClip(), bounceSfxVolume);
                    continue;
                }

                break;
            }

            TryAttachKickPushedSkullAtWorld(next, kickDirection, currentTileCenter);

            EnsureKickOriginBlocker(currentTileCenter, kickOriginBlockerSize, kickOriginBlockerUseTrigger);

            float travelTime = kickTileSize / Mathf.Max(0.0001f, kickSpeed);
            float elapsed = 0f;
            Vector2 start = currentTileCenter;
            StartKickPushedSkullSegment(start);

            bool cancelAndReturn = false;

            while (elapsed < travelTime)
            {
                if (HasExploded || !isKicked)
                {
                    RemoveKickOriginBlocker();
                    break;
                }

                if (IsBlockedByMaskAtWorld(next, kickBlockMoveMask, kickOverlapBoxSize))
                {
                    cancelAndReturn = true;
                    break;
                }

                elapsed += Time.fixedDeltaTime;

                float a = Mathf.Clamp01(elapsed / travelTime);
                Vector2 pos = Vector2.Lerp(start, next, a);

                lastPos = pos;
                rb.MovePosition(pos);
                UpdateKickPushedSkullSegment(start, a);
                DestroyPickupsAtWorld(pos);

                yield return waitFixed;
            }

            if (HasExploded || !isKicked)
                break;

            if (cancelAndReturn)
            {
                rb.position = start;
                transform.position = start;
                lastPos = start;
                currentTileCenter = start;
                FinishKickPushedSkull(start);

                RemoveKickOriginBlocker();

                if (IsRubberBomb)
                {
                    kickDirection = -kickDirection;
                    TryPlayKickSfx_StopOthers(GetKickBounceClip(), bounceSfxVolume);
                    continue;
                }

                break;
            }

            if (IsBlockedByMaskAtWorld(next, kickBlockMoveMask, kickOverlapBoxSize))
            {
                rb.position = start;
                transform.position = start;
                lastPos = start;
                currentTileCenter = start;
                FinishKickPushedSkull(start);

                RemoveKickOriginBlocker();

                if (IsRubberBomb)
                {
                    kickDirection = -kickDirection;
                    TryPlayKickSfx_StopOthers(GetKickBounceClip(), bounceSfxVolume);
                    continue;
                }

                break;
            }

            RemoveKickOriginBlocker();

            currentTileCenter = next;
            lastPos = next;

            rb.position = next;
            transform.position = next;
            UpdateKickPushedSkullSegment(start, 1f);

            DestroyPickupsAtWorld(next);

            if (owner != null)
                owner.NotifyBombAt(next, gameObject);
        }

        rb.position = currentTileCenter;
        transform.position = currentTileCenter;
        lastPos = currentTileCenter;
        FinishKickPushedSkull(currentTileCenter);

        isKicked = false;
        kickRoutine = null;

        if (anim != null)
        {
            anim.SetFrozen(false);
            anim.RefreshFrame();
        }
    }

    public void StopKickAndSnapToGrid(float tileSize, Vector2? snapWorldPosOverride = null)
    {
        if (HasExploded)
            return;

        if (!isKicked)
            return;

        tileSize = Mathf.Max(0.0001f, tileSize);

        if (kickRoutine != null)
        {
            StopCoroutine(kickRoutine);
            kickRoutine = null;
        }

        RemoveKickOriginBlocker();

        isKicked = false;

        Vector2 cur = snapWorldPosOverride ?? (rb != null ? rb.position : (Vector2)transform.position);
        Vector2 snapped = SnapToGrid(cur, tileSize);

        currentTileCenter = snapped;
        lastPos = snapped;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.position = snapped;
        }

        transform.position = snapped;
        FinishKickPushedSkull(snapped);

        if (anim != null)
        {
            anim.SetFrozen(false);
            anim.RefreshFrame();
        }

        RecalculateCharactersInsideAt(snapped);
        if (bombCollider != null)
            bombCollider.isTrigger = charactersInside.Count > 0;
    }

    private void StopKickAndSnapToExplosionTile(Collider2D explosionCollider, float tileSize)
    {
        Vector2 hitWorldPos = rb != null ? rb.position : (Vector2)transform.position;

        if (explosionCollider != null)
        {
            BombExplosion explosion =
                explosionCollider.GetComponent<BombExplosion>() ??
                explosionCollider.GetComponentInParent<BombExplosion>() ??
                explosionCollider.GetComponentInChildren<BombExplosion>();

            hitWorldPos = explosion != null
                ? (Vector2)explosion.transform.position
                : (Vector2)explosionCollider.bounds.center;
        }

        StopKickAndSnapToGrid(tileSize, hitWorldPos);
    }

    public void MarkAsExploded()
    {
        HasExploded = true;
        SetAirborneColliderSuppressed(false);
        StopKickPunchMagnetRoutines();

        isKicked = false;
        isPunched = false;

        if (fuseRoutine != null)
        {
            StopCoroutine(fuseRoutine);
            fuseRoutine = null;
        }

        RemoveMagnetOriginBlocker();
        RemoveKickOriginBlocker();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (HasExploded)
            return;

        int layer = other.gameObject.layer;

        if (layer == LayerMask.NameToLayer("Explosion"))
        {
            LogBombEscape(
                $"OnTriggerEnter EXPLOSION other:{other.name} " +
                $"isControlBomb:{IsControlBomb} isPunched:{isPunched} isKicked:{isKicked}");

            if (IsControlBomb && isPunched)
                return;

            if (isKicked)
                StopKickAndSnapToExplosionTile(other, kickTileSize);

            if (owner != null)
                owner.ExplodeBomb(gameObject);

            return;
        }

        if (!IsCharacterLayer(layer))
            return;

        int before = charactersInside.Count;
        bool wasTrigger = bombCollider != null && bombCollider.isTrigger;

        if (IsCharacterStillOccupyingBomb(other, rb != null ? rb.position : (Vector2)transform.position))
            charactersInside.Add(other);

        if (bombCollider != null && charactersInside.Count > 0)
            bombCollider.isTrigger = true;

        LogBombEscape(
            $"OnTriggerEnter CHAR other:{other.name} layer:{LayerMask.LayerToName(layer)} " +
            $"beforeCount:{before} afterCount:{charactersInside.Count} " +
            $"wasTrigger:{wasTrigger} nowTrigger:{(bombCollider != null && bombCollider.isTrigger)} " +
            $"bombPos:{transform.position} otherPos:{other.transform.position}");
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (HasExploded || isKicked || isPunched)
            return;

        if (other == null)
            return;

        int layer = other.gameObject.layer;
        if (!IsCharacterLayer(layer))
            return;

        int before = charactersInside.Count;
        bool wasTrigger = bombCollider != null && bombCollider.isTrigger;

        charactersInside.Remove(other);

        Vector2 bombPos = rb != null ? rb.position : (Vector2)transform.position;

        RecalculateCharactersInsideAt(bombPos);

        bool stillOccupying = charactersInside.Contains(other);

        if (bombCollider != null)
            bombCollider.isTrigger = charactersInside.Count > 0;

        LogBombEscape(
            $"OnTriggerExit CHAR other:{other.name} layer:{LayerMask.LayerToName(layer)} " +
            $"beforeCount:{before} afterRecalcCount:{charactersInside.Count} " +
            $"wasTrigger:{wasTrigger} nowTrigger:{(bombCollider != null && bombCollider.isTrigger)} " +
            $"bombPos:{bombPos} otherPos:{other.transform.position} stillOccupying:{stillOccupying}");

        if (charactersInside.Count == 0)
        {
            LogBombEscape(
                $"Bomb became SOLID at bombPos:{bombPos}. " +
                $"This is only valid if no player/enemy is still physically overlapping the bomb.",
                verbose: false);
        }
    }

    public float RemainingFuseSeconds
    {
        get
        {
            if (HasExploded) return 0f;

            float now = fusePaused ? fusePauseStartedAt : Time.time;
            float elapsed = now - PlacedTime;
            return Mathf.Max(0f, FuseSeconds - elapsed);
        }
    }

    public void SetFuseSeconds(float fuseSeconds)
    {
        FuseSeconds = Mathf.Max(0.01f, fuseSeconds);
    }

    public bool StartMagnetPull(
        Vector2 directionToMagnet,
        float tileSize,
        int steps,
        LayerMask obstacleMask,
        Tilemap destructibleTilemap,
        float speedMultiplier)
    {
        LayerMask defaultBlockMoveMask = LayerMask.GetMask("Player", "Stage", "Bomb", "Enemy", "Louie");
        return StartMagnetPull(
            directionToMagnet,
            tileSize,
            steps,
            obstacleMask,
            destructibleTilemap,
            speedMultiplier,
            defaultBlockMoveMask,
            0.60f,
            0.90f,
            false);
    }

    public bool StartMagnetPull(
        Vector2 directionToMagnet,
        float tileSize,
        int steps,
        LayerMask obstacleMask,
        Tilemap destructibleTilemap,
        float speedMultiplier,
        LayerMask blockMoveMask,
        float overlapBoxSize,
        float originBlockerSize,
        bool originBlockerUseTrigger)
    {
        if (HasExploded || isKicked || isPunched)
            return false;

        if (magnetRoutine != null)
            StopCoroutine(magnetRoutine);

        RemoveMagnetOriginBlocker();

        magnetSpeedMultiplier = Mathf.Max(0.05f, speedMultiplier);

        kickDirection = directionToMagnet.normalized;
        kickTileSize = tileSize;

        kickObstacleMask = obstacleMask | LayerMask.GetMask("Enemy", "Player", "Louie");
        kickDestructibleTilemap = destructibleTilemap;

        ResolveIndestructibleTilemapIfNeeded();

        Vector2 origin = SnapToGrid(rb.position, tileSize);
        Vector2 next = origin + kickDirection * kickTileSize;

        if (IsMagnetTileBlocked(next, blockMoveMask, overlapBoxSize))
            return false;

        currentTileCenter = origin;
        lastPos = origin;

        rb.position = origin;
        transform.position = origin;

        TryPlayBombSfx_NoOverlap(magnetPullSfx, magnetPullSfxVolume);

        magnetRoutine = StartCoroutine(MagnetPullRoutineFixed(
            steps,
            magnetSpeedMultiplier,
            blockMoveMask,
            Mathf.Clamp(overlapBoxSize, 0.1f, 1.5f),
            Mathf.Clamp(originBlockerSize, 0.2f, 1.2f),
            originBlockerUseTrigger));

        return true;
    }

    private IEnumerator MagnetPullRoutineFixed(
        int steps,
        float speedMultiplier,
        LayerMask blockMoveMask,
        float overlapBoxSize,
        float originBlockerSize,
        bool originBlockerUseTrigger)
    {
        float mult = Mathf.Max(0.05f, speedMultiplier);
        int remainingSteps = Mathf.Max(0, steps);

        while (true)
        {
            if (HasExploded)
                break;

            if (remainingSteps > 0 && remainingSteps-- == 0)
                break;

            Vector2 start = currentTileCenter;
            Vector2 next = currentTileCenter + kickDirection * kickTileSize;

            if (IsMagnetTileBlocked(next, blockMoveMask, overlapBoxSize))
                break;

            EnsureMagnetOriginBlocker(start, originBlockerSize, originBlockerUseTrigger);

            float baseSpeed = Mathf.Max(0.0001f, magnetPullSpeed);
            float speed = Mathf.Max(0.0001f, baseSpeed * mult);

            float travelTime = kickTileSize / speed;
            float elapsed = 0f;
            bool cancelAndReturn = false;

            while (elapsed < travelTime)
            {
                if (HasExploded)
                {
                    RemoveMagnetOriginBlocker();
                    yield break;
                }

                if (IsMagnetTileBlocked(next, blockMoveMask, overlapBoxSize))
                {
                    cancelAndReturn = true;
                    break;
                }

                elapsed += Time.fixedDeltaTime;

                float a = Mathf.Clamp01(elapsed / travelTime);
                Vector2 pos = Vector2.Lerp(start, next, a);

                lastPos = pos;
                rb.MovePosition(pos);

                yield return waitFixed;
            }

            if (cancelAndReturn)
            {
                rb.position = start;
                transform.position = start;
                lastPos = start;
                currentTileCenter = start;

                RemoveMagnetOriginBlocker();
                break;
            }

            if (IsMagnetTileBlocked(next, blockMoveMask, overlapBoxSize))
            {
                rb.position = start;
                transform.position = start;
                lastPos = start;
                currentTileCenter = start;

                RemoveMagnetOriginBlocker();
                break;
            }

            RemoveMagnetOriginBlocker();

            currentTileCenter = next;
            lastPos = next;

            rb.position = next;
            transform.position = next;

            if (owner != null)
                owner.NotifyBombAt(next, gameObject);
        }

        RemoveMagnetOriginBlocker();

        rb.position = currentTileCenter;
        transform.position = currentTileCenter;
        lastPos = currentTileCenter;

        if (anim != null)
        {
            anim.SetFrozen(false);
            anim.RefreshFrame();
        }

        magnetRoutine = null;
    }

    private bool IsMagnetTileBlocked(Vector2 worldCenter, LayerMask mask, float boxSize)
    {
        if (TileHasCharacter(worldCenter, LayerMask.GetMask("Player", "Enemy")))
            return true;

        if (IsLouieAt(worldCenter, boxSize))
            return true;

        if (HasItemPickupAt(worldCenter, boxSize))
            return true;

        if (HasBlockingBombAtWorld(worldCenter, boxSize))
            return true;

        if (kickDestructibleTilemap != null)
        {
            Vector3Int cell = kickDestructibleTilemap.WorldToCell(worldCenter);
            if (kickDestructibleTilemap.GetTile(cell) != null)
                return true;
        }

        if (HasIndestructibleAt(worldCenter))
            return true;

        if (IsBlockedByMaskAtWorld(worldCenter, mask, boxSize))
            return true;

        return false;
    }

    private bool IsLouieAt(Vector2 worldCenter, float boxSize)
    {
        int louieMask = LayerMask.GetMask("Louie");
        if (louieMask == 0)
            return false;

        Vector2 size = Vector2.one * (kickTileSize * Mathf.Max(0.1f, boxSize));
        Collider2D[] hits = Physics2D.OverlapBoxAll(worldCenter, size, 0f, louieMask);

        if (hits == null || hits.Length == 0)
            return false;

        for (int i = 0; i < hits.Length; i++)
        {
            var hit = hits[i];
            if (hit == null)
                continue;

            if (hit.gameObject == gameObject)
                continue;

            if (hit.transform == transform || hit.transform.IsChildOf(transform))
                continue;

            return true;
        }

        return false;
    }

    private bool HasItemPickupAt(Vector2 worldCenter, float boxSize)
    {
        Vector2 size = Vector2.one * (kickTileSize * Mathf.Max(0.1f, boxSize));
        Collider2D[] hits = Physics2D.OverlapBoxAll(worldCenter, size, 0f);

        if (hits == null || hits.Length == 0)
            return false;

        for (int i = 0; i < hits.Length; i++)
        {
            var hit = hits[i];
            if (hit == null)
                continue;

            if (hit.gameObject == gameObject)
                continue;

            if (hit.transform == transform || hit.transform.IsChildOf(transform))
                continue;

            var pickup = hit.GetComponent<ItemPickup>() ?? hit.GetComponentInParent<ItemPickup>();
            if (pickup != null)
                return true;
        }

        return false;
    }

    private bool IsBlockedByMaskAtWorld(Vector2 worldCenter, LayerMask mask, float boxSize)
    {
        ResolveIndestructibleTilemapIfNeeded();

        Vector2 size = Vector2.one * (kickTileSize * Mathf.Max(0.1f, boxSize));

        Collider2D[] hits = Physics2D.OverlapBoxAll(worldCenter, size, 0f, mask);
        if (hits != null && hits.Length > 0)
        {
            for (int i = 0; i < hits.Length; i++)
            {
                var h = hits[i];
                if (h == null) continue;
                if (h.gameObject == gameObject) continue;
                if (h.isTrigger) continue;

                if (h.transform.IsChildOf(transform))
                    continue;

                if (h.gameObject.name == "KickBombOriginBlocker" || h.gameObject.name == "MagnetBombOriginBlocker")
                    continue;

                return true;
            }
        }

        if (stageMask != 0)
        {
            Collider2D[] stageHits = Physics2D.OverlapBoxAll(worldCenter, size, 0f, stageMask);
            if (stageHits != null && stageHits.Length > 0)
            {
                for (int i = 0; i < stageHits.Length; i++)
                {
                    var h = stageHits[i];
                    if (h == null) continue;
                    if (h.gameObject == gameObject) continue;
                    if (h.isTrigger) continue;

                    bool isBlockTag = h.CompareTag(TagDestructibles);

                    if (isBlockTag)
                        return true;
                }
            }
        }

        if (kickDestructibleTilemap != null)
        {
            Vector3Int dCell = kickDestructibleTilemap.WorldToCell(worldCenter);
            if (kickDestructibleTilemap.GetTile(dCell) != null)
                return true;
        }

        if (HasIndestructibleAt(worldCenter))
            return true;

        return false;
    }

    private bool HasBlockingBombAtWorld(Vector2 worldCenter, float boxSize)
    {
        int bombLayer = LayerMask.NameToLayer("Bomb");
        if (bombLayer < 0)
            return false;

        Vector2 size = Vector2.one * (kickTileSize * Mathf.Max(0.1f, boxSize));

        var filter = new ContactFilter2D
        {
            useLayerMask = true,
            useTriggers = true
        };
        filter.SetLayerMask(1 << bombLayer);

        int count = Physics2D.OverlapBox(worldCenter, size, 0f, filter, magnetBlockCheckBuffer);
        for (int i = 0; i < count; i++)
        {
            Collider2D hit = magnetBlockCheckBuffer[i];
            magnetBlockCheckBuffer[i] = null;

            if (hit == null)
                continue;

            GameObject hitObject = hit.attachedRigidbody != null
                ? hit.attachedRigidbody.gameObject
                : hit.gameObject;

            if (hitObject == null || hitObject == gameObject)
                continue;

            if (hitObject.transform.IsChildOf(transform))
                continue;

            return true;
        }

        return false;
    }

    private void EnsureMagnetOriginBlocker(Vector2 worldCenter, float size, bool useTrigger)
    {
        if (magnetOriginBlocker == null)
        {
            magnetOriginBlocker = new GameObject("MagnetBombOriginBlocker");
            magnetOriginBlocker.transform.SetParent(transform, worldPositionStays: true);

            int sLayer = LayerMask.NameToLayer("Stage");
            if (sLayer >= 0)
                magnetOriginBlocker.layer = sLayer;

            var col = magnetOriginBlocker.AddComponent<BoxCollider2D>();
            col.isTrigger = useTrigger;
            col.size = Vector2.one * Mathf.Max(0.01f, size);
        }
        else
        {
            if (magnetOriginBlocker.TryGetComponent<BoxCollider2D>(out var col))
            {
                col.isTrigger = useTrigger;
                col.size = Vector2.one * Mathf.Max(0.01f, size);
            }
        }

        magnetOriginBlocker.transform.position = new Vector3(worldCenter.x, worldCenter.y, transform.position.z);
    }

    private void RemoveMagnetOriginBlocker()
    {
        if (magnetOriginBlocker == null)
            return;

        Destroy(magnetOriginBlocker);
        magnetOriginBlocker = null;
    }

    private void EnsureKickOriginBlocker(Vector2 worldCenter, float size, bool useTrigger)
    {
        if (kickOriginBlocker == null)
        {
            kickOriginBlocker = new GameObject("KickBombOriginBlocker");
            kickOriginBlocker.transform.SetParent(transform, worldPositionStays: true);

            int sLayer = LayerMask.NameToLayer("Stage");
            if (sLayer >= 0)
                kickOriginBlocker.layer = sLayer;

            var col = kickOriginBlocker.AddComponent<BoxCollider2D>();
            col.isTrigger = useTrigger;
            col.size = Vector2.one * Mathf.Max(0.01f, size);
        }
        else
        {
            if (kickOriginBlocker.TryGetComponent<BoxCollider2D>(out var col))
            {
                col.isTrigger = useTrigger;
                col.size = Vector2.one * Mathf.Max(0.01f, size);
            }
        }

        kickOriginBlocker.transform.position = new Vector3(worldCenter.x, worldCenter.y, transform.position.z);
    }

    private void RemoveKickOriginBlocker()
    {
        if (kickOriginBlocker == null)
            return;

        Destroy(kickOriginBlocker);
        kickOriginBlocker = null;
    }

    public void EnsureMinRemainingFuse(float minSeconds)
    {
        if (HasExploded)
            return;

        float min = Mathf.Max(0f, minSeconds);

        float remaining = RemainingFuseSeconds;
        if (remaining >= min)
            return;

        FuseSeconds += (min - remaining);
    }

    public void LockWorldPosition(Vector2 worldPos)
    {
        worldPos.x = Mathf.Round(worldPos.x);
        worldPos.y = Mathf.Round(worldPos.y);

        lockWorldPosActive = true;
        lockWorldPos = worldPos;

        currentTileCenter = worldPos;
        lastPos = worldPos;

        if (rb != null)
        {
            rb.position = worldPos;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        transform.position = worldPos;
    }

    private static Vector2 SnapToGrid(Vector2 worldPos, float tileSize)
    {
        worldPos.x = Mathf.Round(worldPos.x / tileSize) * tileSize;
        worldPos.y = Mathf.Round(worldPos.y / tileSize) * tileSize;
        return worldPos;
    }

    private bool HasIndestructibleAt(Vector2 worldPos)
    {
        ResolveIndestructibleTilemapIfNeeded();

        if (indestructibleTilemap == null)
            return false;

        Vector3Int cell = indestructibleTilemap.WorldToCell(worldPos);
        return indestructibleTilemap.GetTile(cell) != null;
    }

    private bool IsHoleCollider(Collider2D c)
    {
        return c != null && c.CompareTag("Hole");
    }

    private bool NotifyOwnerAt(Vector2 pos)
    {
        if (owner == null)
            return false;

        owner.NotifyBombAt(pos, gameObject);

        return HasExploded || this == null || gameObject == null;
    }

    private void TryPlayBombSfx_NoOverlap(AudioClip clip, float volume)
    {
        if (audioSource == null || clip == null)
            return;

        float clipLen = clip.length > 0.001f ? clip.length : 0.10f;

        bool canPlay;

        lock (bombSfxGate)
        {
            float now = Time.time;
            canPlay = now >= bombSfxBlockedUntil;

            if (canPlay)
                bombSfxBlockedUntil = now + clipLen;
        }

        if (!canPlay)
            return;

        audioSource.PlayOneShot(clip, volume);
    }

    private void TryPlayKickSfx_StopOthers(AudioClip clip, float volume)
    {
        if (audioSource == null || clip == null)
            return;

        lock (kickSfxGate)
        {
            if (kickSfxCurrentSource != null && kickSfxCurrentSource != audioSource)
                kickSfxCurrentSource.Stop();

            kickSfxCurrentSource = audioSource;
        }

        audioSource.Stop();
        audioSource.PlayOneShot(clip, volume);
    }

    private AudioClip GetKickBounceClip()
    {
        if (IsRubberBomb && cachedRubberBounceClip != null)
            return cachedRubberBounceClip;

        return bounceSfx;
    }

    private bool airborneColliderSuppressed;
    private bool airbornePrevColliderEnabled;

    private void SetAirborneColliderSuppressed(bool suppressed)
    {
        if (bombCollider == null)
            return;

        if (suppressed)
        {
            if (airborneColliderSuppressed)
                return;

            airbornePrevColliderEnabled = bombCollider.enabled;
            bombCollider.enabled = false;
            airborneColliderSuppressed = true;
            return;
        }

        if (!airborneColliderSuppressed)
            return;

        bombCollider.enabled = airbornePrevColliderEnabled;
        airborneColliderSuppressed = false;
    }

    private void DestroyPickupsAtWorld(Vector2 worldCenter)
    {
        DestroyPickupsAtWorld(worldCenter, kickDirection);
    }

    private void DestroyPickupsAtWorld(Vector2 worldCenter, Vector2 impactDirection)
    {
        Vector2 size = Vector2.one * (kickTileSize * 0.45f);
        Collider2D[] hits = Physics2D.OverlapBoxAll(worldCenter, size, 0f);

        if (hits == null || hits.Length == 0)
            return;

        for (int i = 0; i < hits.Length; i++)
        {
            var hit = hits[i];
            if (hit == null)
                continue;

            var pickup = hit.GetComponent<ItemPickup>();
            if (pickup == null)
                pickup = hit.GetComponentInParent<ItemPickup>();

            if (pickup == null)
                continue;

            if (TryAttachKickPushedSkull(pickup, impactDirection, worldCenter))
                continue;

            if (pickup.TryBounceSkull(impactDirection, kickTileSize, bombCollider))
                continue;

            pickup.DestroySilently();
        }
    }

    private bool TryHandleIndestructibleKickBounce(
        Vector2 blockedWorldPos,
        out AudioClip bounceSfx,
        out float bounceSfxVolume)
    {
        bounceSfx = null;
        bounceSfxVolume = 1f;

        ResolveIndestructibleTilemapIfNeeded();

        if (indestructibleTilemap == null)
            return false;

        Vector3Int blockedCell = indestructibleTilemap.WorldToCell(blockedWorldPos);
        TileBase blockedTile = indestructibleTilemap.GetTile(blockedCell);
        if (blockedTile == null)
            return false;

        EnsureKickedBombHandlers();
        if (kickIndestructibleHandlers == null || kickIndestructibleHandlers.Length == 0)
            return false;

        for (int i = 0; i < kickIndestructibleHandlers.Length; i++)
        {
            var handler = kickIndestructibleHandlers[i];
            if (handler == null)
                continue;

            if (handler.TryHandleKickedBombBlocked(
                    this,
                    currentTileCenter,
                    blockedWorldPos,
                    kickDirection,
                    indestructibleTilemap,
                    blockedCell,
                    blockedTile,
                    out AudioClip handledBounceSfx,
                    out float handledBounceSfxVolume))
            {
                bounceSfx = handledBounceSfx;
                bounceSfxVolume = Mathf.Clamp01(handledBounceSfxVolume);
                return true;
            }
        }

        return false;
    }

    private void EnsureKickedBombHandlers()
    {
        if (kickIndestructibleHandlers != null && kickIndestructibleHandlers.Length > 0)
            return;

        MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude);
        if (behaviours == null || behaviours.Length == 0)
        {
            kickIndestructibleHandlers = System.Array.Empty<IIndestructibleKickedBombHandler>();
            return;
        }

        var handlers = new System.Collections.Generic.List<IIndestructibleKickedBombHandler>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IIndestructibleKickedBombHandler handler)
                handlers.Add(handler);
        }

        kickIndestructibleHandlers = handlers.Count > 0
            ? handlers.ToArray()
            : System.Array.Empty<IIndestructibleKickedBombHandler>();
    }

    private bool TryAttachKickPushedSkull(ItemPickup pickup, Vector2 impactDirection, Vector2 bombWorldCenter)
    {
        if (pickup == null || pickup.type != ItemType.Skull)
            return false;

        if (!kickPushedSkulls.Contains(pickup))
            kickPushedSkulls.Add(pickup);

        int pushDistanceTiles = GetKickPushedSkullDistanceTiles(pickup);
        return pickup.TryMoveSkullInFrontOfKickedBomb(
            impactDirection,
            kickTileSize,
            bombCollider,
            bombWorldCenter,
            pushDistanceTiles,
            finishPush: false);
    }

    private bool TryAttachKickPushedSkullAtWorld(Vector2 worldCenter, Vector2 impactDirection, Vector2 bombWorldCenter)
    {
        Vector2 size = Vector2.one * (kickTileSize * 0.45f);
        Collider2D[] hits = Physics2D.OverlapBoxAll(worldCenter, size, 0f);

        if (hits == null || hits.Length == 0)
            return false;

        bool attachedAny = false;

        for (int i = 0; i < hits.Length; i++)
        {
            var hit = hits[i];
            if (hit == null)
                continue;

            var pickup = hit.GetComponent<ItemPickup>();
            if (pickup == null)
                pickup = hit.GetComponentInParent<ItemPickup>();

            if (TryAttachKickPushedSkull(pickup, impactDirection, bombWorldCenter))
                attachedAny = true;
        }

        return attachedAny;
    }

    private void StartKickPushedSkullSegment(Vector2 bombSegmentStart)
    {
        if (kickPushedSkulls.Count == 0)
            return;

        for (int i = kickPushedSkulls.Count - 1; i >= 0; i--)
        {
            var skull = kickPushedSkulls[i];
            if (skull == null)
            {
                kickPushedSkulls.RemoveAt(i);
                continue;
            }

            if (!skull.StartKickedBombPushSegment(
                    kickDirection,
                    kickTileSize,
                    bombCollider,
                    bombSegmentStart,
                    i + 1))
            {
                kickPushedSkulls.RemoveAt(i);
            }
        }
    }

    private void UpdateKickPushedSkullSegment(Vector2 bombSegmentStart, float progress)
    {
        if (kickPushedSkulls.Count == 0)
            return;

        for (int i = kickPushedSkulls.Count - 1; i >= 0; i--)
        {
            var skull = kickPushedSkulls[i];
            if (skull == null)
            {
                kickPushedSkulls.RemoveAt(i);
                continue;
            }

            if (!skull.UpdateKickedBombPushSegment(
                    kickDirection,
                    kickTileSize,
                    bombCollider,
                    bombSegmentStart,
                    progress,
                    i + 1))
            {
                kickPushedSkulls.RemoveAt(i);
            }
        }
    }

    private void FinishKickPushedSkull(Vector2 bombWorldCenter)
    {
        if (kickPushedSkulls.Count == 0)
            return;

        for (int i = kickPushedSkulls.Count - 1; i >= 0; i--)
        {
            var skull = kickPushedSkulls[i];
            if (skull == null)
                continue;

            skull.TryMoveSkullInFrontOfKickedBomb(
                kickDirection,
                kickTileSize,
                bombCollider,
                bombWorldCenter,
                i + 1,
                finishPush: true);
        }

        kickPushedSkulls.Clear();
    }

    private int GetKickPushedSkullDistanceTiles(ItemPickup pickup)
    {
        int index = kickPushedSkulls.IndexOf(pickup);
        return Mathf.Max(1, index + 1);
    }

    private bool IsKickBlockedByCharacterOnImmediateExit(Vector2 origin, Vector2 next)
    {
        int charMask = LayerMask.GetMask("Player", "Enemy", "Louie");
        Collider2D[] hits = Physics2D.OverlapBoxAll(next, Vector2.one * (kickTileSize * 0.6f), 0f, charMask);

        if (hits == null || hits.Length == 0)
            return false;

        for (int i = 0; i < hits.Length; i++)
        {
            var hit = hits[i];
            if (hit == null)
                continue;

            if (hit.transform == transform || hit.transform.IsChildOf(transform))
                continue;

            Vector2 hitPos = hit.attachedRigidbody != null
                ? hit.attachedRigidbody.position
                : (Vector2)hit.transform.position;

            if (Vector2.Distance(hitPos, origin) <= kickTileSize * 0.2f)
                continue;

            return true;
        }

        return false;
    }

    [Header("Debug Bomb Escape")]
    [SerializeField] private bool debugBombEscape;
    [SerializeField] private bool debugBombEscapeVerbose;

    private void LogBombEscape(string message, bool verbose = false)
    {
        if (!debugBombEscape)
            return;

        if (verbose && !debugBombEscapeVerbose)
            return;

        Debug.Log($"[BombEscape][Bomb:{name}] {message}", this);
    }

    private static bool IsCharacterLayer(int layer)
    {
        int playerLayer = LayerMask.NameToLayer("Player");
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        return layer == playerLayer || layer == enemyLayer;
    }

    private static float GetColliderApproxRadius(Collider2D col)
    {
        if (col == null)
            return 0f;

        Bounds b = col.bounds;
        return Mathf.Max(b.extents.x, b.extents.y);
    }

    private bool IsCharacterStillOccupyingBomb(Collider2D col, Vector2 bombWorldPos)
    {
        if (col == null)
            return false;

        float charRadius = GetColliderApproxRadius(col);
        float allowedDistance = ApproxRadius + charRadius + 0.02f;

        Vector2 closest = col.ClosestPoint(bombWorldPos);
        float dist = Vector2.Distance(closest, bombWorldPos);

        return dist <= allowedDistance;
    }
}

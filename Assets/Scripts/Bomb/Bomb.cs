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

    [Header("Kick")]
    public float kickSpeed = 9f;

    [Header("Punch")]
    public float punchDuration = 0.22f;
    public float punchArcHeight = 0.9f;

    [Header("Chain Explosion")]
    public float chainStepDelay = 0.1f;

    public float FuseSeconds = 2f;

    [Header("Stage Wrap")]
    [SerializeField] private Tilemap stageBoundsTilemap;

    [SerializeField] private float magnetPullSpeed = 10f;

    private BombController owner;
    public BombController Owner => owner;

    public bool HasExploded { get; private set; }
    public float PlacedTime { get; private set; }
    public bool IsControlBomb { get; set; }

    public bool IsBeingKicked => isKicked;
    public bool IsBeingPunched => isPunched;

    public bool IsSolid => bombCollider != null && !bombCollider.isTrigger;
    public bool CanBeKicked => !HasExploded && !isKicked && IsSolid && charactersInside.Count == 0;
    public bool CanBePunched => !HasExploded && !isKicked && !isPunched && IsSolid && charactersInside.Count == 0;
    public bool CanBeMagnetPulled => !HasExploded && !IsBeingKicked && !IsBeingPunched && !IsBeingMagnetPulled;
    public bool IsPierceBomb { get; set; }

    public bool IsBeingMagnetPulled => magnetRoutine != null;

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

        lastPos = rb.position;
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
        if (HasExploded)
            return;

        if (IsControlBomb)
            return;

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

            if (fusePaused)
            {
                yield return null;
                continue;
            }

            if (RemainingFuseSeconds <= 0f)
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
    }

    private void RecalculateCharactersInsideAt(Vector2 worldPos)
    {
        charactersInside.Clear();

        int charMask = LayerMask.GetMask("Player", "Enemy");
        Collider2D[] cols = Physics2D.OverlapBoxAll(worldPos, Vector2.one * 0.4f, 0f, charMask);

        if (cols == null)
            return;

        int playerLayer = LayerMask.NameToLayer("Player");
        int enemyLayer = LayerMask.NameToLayer("Enemy");

        for (int i = 0; i < cols.Length; i++)
        {
            var c = cols[i];
            if (c == null)
                continue;

            int layer = c.gameObject.layer;
            if (layer == playerLayer || layer == enemyLayer)
                charactersInside.Add(c);
        }
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
        Vector2? logicalOriginOverride = null)
    {
        if (!CanBePunched || direction == Vector2.zero)
            return false;

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

        isPunched = true;
        charactersInside.Clear();
        bombCollider.isTrigger = true;

        PauseFuse();

        if (audioSource != null && punchSfx != null)
            audioSource.PlayOneShot(punchSfx, punchSfxVolume);

        if (anim != null)
            anim.SetFrozen(true);

        punchRoutine = StartCoroutine(PunchRoutineFixed_Hybrid(
            logicalOrigin,
            yOff,
            distanceTiles,
            80,
            punchDuration,
            punchArcHeight));

        return true;
    }

    private IEnumerator PunchRoutineFixed_Hybrid(
        Vector2 logicalOrigin,
        float visualStartYOffset,
        int forwardSteps,
        int maxExtraBounces,
        float duration,
        float arcHeight)
    {
        Vector2 cur = logicalOrigin;
        int steps = Mathf.Max(1, forwardSteps);

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

            yield return PunchArcSegmentFixed(SegmentStart(cur), target, duration, arcHeight);
            cur = target;

            if (NotifyOwnerAt(cur))
                goto FINISH;
        }
        else
        {
            float segDuration = duration / Mathf.Max(1, steps);

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

                yield return PunchArcSegmentFixed(SegmentStart(cur), next, segDuration, arcHeight);
                cur = next;

                if (NotifyOwnerAt(cur))
                    goto FINISH;
            }
        }

        for (int b = 0; b < Mathf.Max(0, maxExtraBounces); b++)
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

            if (audioSource != null && bounceSfx != null)
                audioSource.PlayOneShot(bounceSfx, bounceSfxVolume);

            yield return PunchArcSegmentFixed(cur, next, duration, arcHeight);
            cur = next;

            if (NotifyOwnerAt(cur))
                goto FINISH;
        }

    FINISH:
        TeleportTo(cur);

        isPunched = false;
        punchRoutine = null;

        ResumeFuse();

        if (!HasExploded)
        {
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

        var tilemaps = FindObjectsByType<Tilemap>(FindObjectsSortMode.None);
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

            stageBoundsTilemap = ind;
            return;
        }

        var fallback = ground != null ? ground : (ind != null ? ind : null);
        if (fallback != null)
        {
            stageCellBounds = fallback.cellBounds;
            stageBoundsReady = true;

            if (stageBoundsTilemap == null && ind != null)
                stageBoundsTilemap = ind;
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

    private void TeleportTo(Vector2 pos)
    {
        rb.position = pos;
        transform.position = pos;
        lastPos = pos;
    }

    private IEnumerator PunchArcSegmentFixed(Vector2 start, Vector2 end, float duration, float arcHeight)
    {
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

            lastPos = pos;
            rb.MovePosition(pos);

            yield return waitFixed;
        }

        rb.position = end;
        transform.position = end;
        lastPos = end;
    }

    public void SetStageBoundsTilemap(Tilemap tilemap)
    {
        stageBoundsTilemap = tilemap;
    }

    private void ResolveStageBoundsTilemapIfNeeded()
    {
        if (stageBoundsTilemap != null)
            return;

        var tilemaps = FindObjectsByType<Tilemap>(FindObjectsSortMode.None);
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

    public void Initialize(BombController owner)
    {
        ResolveStageBoundsTilemapIfNeeded();

        this.owner = owner;
        PlacedTime = Time.time;

        lastPos = rb.position;

        RecalculateCharactersInsideAt(rb.position);
        bombCollider.isTrigger = charactersInside.Count > 0;
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

    public bool StartKick(Vector2 direction, float tileSize, LayerMask obstacleMask, Tilemap destructibleTilemap)
    {
        if (!CanBeKicked || direction == Vector2.zero)
            return false;

        kickDirection = direction.normalized;
        kickTileSize = tileSize;

        kickObstacleMask = obstacleMask | LayerMask.GetMask("Enemy");
        kickDestructibleTilemap = destructibleTilemap;

        Vector2 origin = SnapToGrid(rb.position, tileSize);

        if (IsKickBlocked(origin + kickDirection * kickTileSize))
            return false;

        currentTileCenter = origin;
        lastPos = origin;

        rb.position = origin;
        transform.position = origin;

        if (kickRoutine != null)
            StopCoroutine(kickRoutine);

        isKicked = true;

        if (anim != null)
            anim.SetFrozen(true);

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
                break;

            if (IsKickBlocked(next))
                break;

            float travelTime = kickTileSize / Mathf.Max(0.0001f, kickSpeed);
            float elapsed = 0f;
            Vector2 start = currentTileCenter;

            while (elapsed < travelTime)
            {
                if (HasExploded || !isKicked)
                    break;

                elapsed += Time.fixedDeltaTime;

                float a = Mathf.Clamp01(elapsed / travelTime);
                Vector2 pos = Vector2.Lerp(start, next, a);

                lastPos = pos;
                rb.MovePosition(pos);

                yield return waitFixed;
            }

            if (HasExploded || !isKicked)
                break;

            currentTileCenter = next;
            lastPos = next;

            rb.position = next;
            transform.position = next;

            if (owner != null)
            {
                owner.NotifyBombAt(next, gameObject);

                if (HasExploded || !isKicked)
                    break;
            }
        }

        rb.position = currentTileCenter;
        transform.position = currentTileCenter;
        lastPos = currentTileCenter;

        isKicked = false;
        kickRoutine = null;

        if (anim != null)
        {
            anim.SetFrozen(false);
            anim.RefreshFrame();
        }
    }

    public void StopKickAndSnapToGrid(float tileSize)
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

        isKicked = false;

        Vector2 cur = rb != null ? rb.position : (Vector2)transform.position;
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

        if (anim != null)
        {
            anim.SetFrozen(false);
            anim.RefreshFrame();
        }

        RecalculateCharactersInsideAt(snapped);
        if (bombCollider != null)
            bombCollider.isTrigger = charactersInside.Count > 0;
    }

    public void MarkAsExploded()
    {
        HasExploded = true;

        StopKickPunchMagnetRoutines();

        isKicked = false;
        isPunched = false;

        if (fuseRoutine != null)
        {
            StopCoroutine(fuseRoutine);
            fuseRoutine = null;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (HasExploded)
            return;

        int layer = other.gameObject.layer;

        if (layer == LayerMask.NameToLayer("Explosion"))
        {
            if (IsControlBomb && isPunched)
                return;

            if (owner != null)
                owner.ExplodeBombChained(gameObject);

            return;
        }

        int playerLayer = LayerMask.NameToLayer("Player");
        int enemyLayer = LayerMask.NameToLayer("Enemy");

        if (layer == playerLayer || layer == enemyLayer)
            charactersInside.Add(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (HasExploded || isKicked || isPunched)
            return;

        int layer = other.gameObject.layer;
        int playerLayer = LayerMask.NameToLayer("Player");
        int enemyLayer = LayerMask.NameToLayer("Enemy");

        if (layer != playerLayer && layer != enemyLayer)
            return;

        charactersInside.Remove(other);

        if (charactersInside.Count == 0)
            bombCollider.isTrigger = false;
    }

    public float RemainingFuseSeconds
    {
        get
        {
            if (HasExploded) return 0f;
            float t = (Time.time - PlacedTime);
            return Mathf.Max(0f, FuseSeconds - t);
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
        if (HasExploded || isKicked || isPunched)
            return false;

        if (magnetRoutine != null)
            StopCoroutine(magnetRoutine);

        magnetSpeedMultiplier = Mathf.Max(0.05f, speedMultiplier);

        kickDirection = directionToMagnet.normalized;
        kickTileSize = tileSize;

        kickObstacleMask = obstacleMask | LayerMask.GetMask("Enemy");
        kickDestructibleTilemap = destructibleTilemap;

        Vector2 origin = SnapToGrid(rb.position, tileSize);

        currentTileCenter = origin;
        lastPos = origin;

        rb.position = origin;
        transform.position = origin;

        if (anim != null)
            anim.SetFrozen(true);

        magnetRoutine = StartCoroutine(MagnetPullRoutineFixed(steps, magnetSpeedMultiplier));
        return true;
    }

    private IEnumerator MagnetPullRoutineFixed(int steps, float speedMultiplier)
    {
        float mult = Mathf.Max(0.05f, speedMultiplier);

        int remainingSteps = steps;


        while (true)
        {
            if (HasExploded)
                break;

            if (remainingSteps > 0 && remainingSteps-- == 0)
                break;

            Vector2 next = currentTileCenter + kickDirection * kickTileSize;

            if (TileHasCharacter(next, LayerMask.GetMask("Player")))
                break;

            if (IsKickBlocked(next))
                break;

            float baseSpeed = Mathf.Max(0.0001f, magnetPullSpeed);
            float speed = Mathf.Max(0.0001f, baseSpeed * mult);

            float travelTime = kickTileSize / speed;
            float elapsed = 0f;
            Vector2 start = currentTileCenter;

            while (elapsed < travelTime)
            {
                if (HasExploded)
                    yield break;

                elapsed += Time.fixedDeltaTime;

                float a = Mathf.Clamp01(elapsed / travelTime);
                Vector2 pos = Vector2.Lerp(start, next, a);

                lastPos = pos;
                rb.MovePosition(pos);

                yield return waitFixed;
            }

            currentTileCenter = next;
            lastPos = next;

            rb.position = next;
            transform.position = next;

            if (owner != null)
                owner.NotifyBombAt(next, gameObject);
        }

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
        if (stageBoundsTilemap == null)
            return false;

        Vector3Int cell = stageBoundsTilemap.WorldToCell(worldPos);
        return stageBoundsTilemap.GetTile(cell) != null;
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
}

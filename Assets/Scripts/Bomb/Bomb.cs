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

    [Header("References")]
    private Collider2D bombCollider;
    private Rigidbody2D rb;
    private AnimatedSpriteRenderer anim;
    private AudioSource audioSource;

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
    private bool wrapBoundsReady;
    private int wrapMinX, wrapMaxX, wrapMinY, wrapMaxY;

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

    private static readonly WaitForFixedUpdate waitFixed = new();

    public bool IsBeingMagnetPulled => magnetRoutine != null;

    [SerializeField] private float magnetPullSpeed = 10f;
    private Coroutine magnetRoutine;

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

    private Vector2 WrapToStage(Vector2 worldPos)
    {
        EnsureStageBounds();

        if (!stageBoundsReady)
            return worldPos;

        Vector3Int cell = stageBoundsTilemap != null
            ? stageBoundsTilemap.WorldToCell(worldPos)
            : new Vector3Int(Mathf.RoundToInt(worldPos.x / kickTileSize), Mathf.RoundToInt(worldPos.y / kickTileSize), 0);

        int minX = wrapBoundsReady ? wrapMinX : stageCellBounds.xMin;
        int maxX = wrapBoundsReady ? wrapMaxX : (stageCellBounds.xMax - 1);
        int minY = wrapBoundsReady ? wrapMinY : stageCellBounds.yMin;
        int maxY = wrapBoundsReady ? wrapMaxY : (stageCellBounds.yMax - 1);

        if (cell.x < minX) cell.x = maxX;
        else if (cell.x > maxX) cell.x = minX;

        if (cell.y < minY) cell.y = maxY;
        else if (cell.y > maxY) cell.y = minY;

        Vector3 center;

        if (stageBoundsTilemap != null)
            center = stageBoundsTilemap.GetCellCenterWorld(cell);
        else
            center = new Vector3(cell.x * kickTileSize, cell.y * kickTileSize, transform.position.z);

        center.z = transform.position.z;
        return (Vector2)center;
    }

    private void RecalculateCharactersInsideAt(Vector2 worldPos)
    {
        charactersInside.Clear();

        int charMask = LayerMask.GetMask("Player", "Enemy");
        Collider2D[] cols = Physics2D.OverlapBoxAll(worldPos, Vector2.one * 0.4f, 0f, charMask);

        if (cols == null)
            return;

        for (int i = 0; i < cols.Length; i++)
        {
            var c = cols[i];
            if (c == null)
                continue;

            int layer = c.gameObject.layer;
            if (layer == LayerMask.NameToLayer("Player") || layer == LayerMask.NameToLayer("Enemy"))
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

    private bool TileHasCharacter(Vector2 pos)
    {
        int charMask = LayerMask.GetMask("Player", "Enemy");
        Collider2D hit = Physics2D.OverlapBox(pos, Vector2.one * (kickTileSize * 0.6f), 0f, charMask);
        return hit != null;
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

        return false;
    }

    public bool StartPunch(Vector2 direction, float tileSize, int distanceTiles, LayerMask obstacleMask, Tilemap destructibleTilemap)
    {
        if (!CanBePunched || direction == Vector2.zero)
            return false;

        kickDirection = direction.normalized;
        kickTileSize = tileSize;

        kickObstacleMask = obstacleMask | LayerMask.GetMask("Enemy", "Player");
        kickDestructibleTilemap = destructibleTilemap;

        Vector2 origin = rb.position;
        origin.x = Mathf.Round(origin.x / tileSize) * tileSize;
        origin.y = Mathf.Round(origin.y / tileSize) * tileSize;

        currentTileCenter = origin;
        lastPos = origin;

        rb.position = origin;
        transform.position = origin;

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

        punchRoutine = StartCoroutine(PunchRoutineFixed_Hybrid(origin, distanceTiles, 80, punchDuration, punchArcHeight));
        return true;
    }

    private IEnumerator PunchRoutineFixed_Hybrid(Vector2 start, int forwardSteps, int maxExtraBounces, float duration, float arcHeight)
    {
        Vector2 cur = start;
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
                    continue;
                }

                target = n;
            }

            if (HasExploded)
                goto FINISH;

            yield return PunchArcSegmentFixed(cur, target, duration, arcHeight);
            cur = target;
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
                    continue;
                }

                yield return PunchArcSegmentFixed(cur, next, segDuration, arcHeight);
                cur = next;
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
                b--;
                continue;
            }

            if (audioSource != null && bounceSfx != null)
                audioSource.PlayOneShot(bounceSfx, bounceSfxVolume);

            yield return PunchArcSegmentFixed(cur, next, duration, arcHeight);
            cur = next;
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
            ComputeWrapBoundsFromIndestructible();
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
            ComputeWrapBoundsFromIndestructible();
            return;
        }

        var fallback = ground != null ? ground : (ind != null ? ind : null);
        if (fallback != null)
        {
            stageCellBounds = fallback.cellBounds;
            stageBoundsReady = true;

            if (stageBoundsTilemap == null && ind != null)
                stageBoundsTilemap = ind;

            ComputeWrapBoundsFromIndestructible();
        }
    }

    private void ComputeWrapBoundsFromIndestructible()
    {
        wrapBoundsReady = false;

        if (stageBoundsTilemap == null)
            return;

        var outer = stageCellBounds;

        int outerMinX = outer.xMin;
        int outerMaxX = outer.xMax - 1;
        int outerMinY = outer.yMin;
        int outerMaxY = outer.yMax - 1;

        bool RowFull(int y)
        {
            for (int x = outerMinX; x <= outerMaxX; x++)
            {
                if (stageBoundsTilemap.GetTile(new Vector3Int(x, y, 0)) == null)
                    return false;
            }
            return true;
        }

        bool ColFull(int x)
        {
            for (int y = outerMinY; y <= outerMaxY; y++)
            {
                if (stageBoundsTilemap.GetTile(new Vector3Int(x, y, 0)) == null)
                    return false;
            }
            return true;
        }

        int top = 0;
        for (int y = outerMaxY; y >= outerMinY; y--)
        {
            if (!RowFull(y)) break;
            top++;
        }

        int bottom = 0;
        for (int y = outerMinY; y <= outerMaxY; y++)
        {
            if (!RowFull(y)) break;
            bottom++;
        }

        int left = 0;
        for (int x = outerMinX; x <= outerMaxX; x++)
        {
            if (!ColFull(x)) break;
            left++;
        }

        int right = 0;
        for (int x = outerMaxX; x >= outerMinX; x--)
        {
            if (!ColFull(x)) break;
            right++;
        }

        int minX = outerMinX + left;
        int maxX = outerMaxX - right;
        int minY = outerMinY + bottom;
        int maxY = outerMaxY - top;

        if (minX > maxX || minY > maxY)
            return;

        wrapMinX = minX;
        wrapMaxX = maxX;
        wrapMinY = minY;
        wrapMaxY = maxY;

        wrapBoundsReady = true;
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

        Vector2 origin = rb.position;
        origin.x = Mathf.Round(origin.x / tileSize) * tileSize;
        origin.y = Mathf.Round(origin.y / tileSize) * tileSize;

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
            if (HasExploded)
                break;

            Vector2 next = currentTileCenter + kickDirection * kickTileSize;

            if (IsKickBlocked(next))
                break;

            float travelTime = kickTileSize / Mathf.Max(0.0001f, kickSpeed);
            float elapsed = 0f;
            Vector2 start = currentTileCenter;

            while (elapsed < travelTime)
            {
                if (HasExploded)
                    break;

                elapsed += Time.fixedDeltaTime;

                float a = Mathf.Clamp01(elapsed / travelTime);
                Vector2 pos = Vector2.Lerp(start, next, a);

                lastPos = pos;
                rb.MovePosition(pos);

                yield return waitFixed;
            }

            if (HasExploded)
                break;

            currentTileCenter = next;
            lastPos = next;

            rb.position = next;
            transform.position = next;
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

    public void MarkAsExploded()
    {
        HasExploded = true;

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

        if (layer == LayerMask.NameToLayer("Player") || layer == LayerMask.NameToLayer("Enemy"))
        {
            charactersInside.Add(other);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (HasExploded || isKicked || isPunched)
            return;

        int layer = other.gameObject.layer;
        if (layer != LayerMask.NameToLayer("Player") && layer != LayerMask.NameToLayer("Enemy"))
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

    public bool StartMagnetPull(Vector2 directionToMagnet, float tileSize, int steps, LayerMask obstacleMask, Tilemap destructibleTilemap)
    {
        if (HasExploded || isKicked || isPunched || steps <= 0)
            return false;

        if (magnetRoutine != null)
            StopCoroutine(magnetRoutine);

        kickDirection = directionToMagnet.normalized;
        kickTileSize = tileSize;

        kickObstacleMask = obstacleMask | LayerMask.GetMask("Enemy");
        kickDestructibleTilemap = destructibleTilemap;

        Vector2 origin = rb.position;
        origin.x = Mathf.Round(origin.x / tileSize) * tileSize;
        origin.y = Mathf.Round(origin.y / tileSize) * tileSize;

        currentTileCenter = origin;
        lastPos = origin;

        rb.position = origin;
        transform.position = origin;

        if (anim != null)
            anim.SetFrozen(true);

        magnetRoutine = StartCoroutine(MagnetPullRoutineFixed(steps));
        return true;
    }

    private IEnumerator MagnetPullRoutineFixed(int steps)
    {
        for (int s = 0; s < steps; s++)
        {
            if (HasExploded)
                break;

            Vector2 next = currentTileCenter + kickDirection * kickTileSize;

            if (IsKickBlocked(next))
                break;

            float speed = Mathf.Max(0.0001f, magnetPullSpeed);
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
}

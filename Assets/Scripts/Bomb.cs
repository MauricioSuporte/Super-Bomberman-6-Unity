using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(AudioSource))]
public class Bomb : MonoBehaviour
{
    private BombController owner;
    public BombController Owner => owner;

    public bool HasExploded { get; private set; }
    public bool IsBeingKicked => isKicked;

    public float PlacedTime { get; private set; }

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

    [Header("Chain Explosion")]
    public float chainStepDelay = 0.1f;

    [Header("Punch")]
    public float punchDuration = 0.22f;
    public float punchArcHeight = 0.9f;

    private bool isPunched;
    public bool IsBeingPunched => isPunched;

    public bool CanBePunched => !HasExploded && !isKicked && !isPunched && IsSolid && charactersInside.Count == 0;

    private Coroutine punchRoutine;

    private bool isKicked;
    private Vector2 kickDirection;
    private float kickTileSize = 1f;
    private LayerMask kickObstacleMask;
    private Tilemap kickDestructibleTilemap;
    private Coroutine kickRoutine;
    private Vector2 currentTileCenter;
    private Vector2 lastPos;

    private readonly HashSet<Collider2D> charactersInside = new();
    private bool chainExplosionScheduled;

    private bool fusePaused;
    private float fusePauseStartedAt;

    private static readonly WaitForFixedUpdate waitFixed = new WaitForFixedUpdate();

    public bool IsSolid => bombCollider != null && !bombCollider.isTrigger;
    public bool CanBeKicked => !HasExploded && !isKicked && IsSolid && charactersInside.Count == 0;

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

    private bool TryFindBounceLanding(Vector2 startLanding, Vector2 dir, int maxBounces, out Vector2 finalLanding)
    {
        Vector2 candidate = startLanding;

        for (int i = 0; i <= Mathf.Max(0, maxBounces); i++)
        {
            if (!IsPunchLandingBlocked(candidate))
            {
                finalLanding = candidate;
                return true;
            }

            candidate += dir * kickTileSize;
        }

        finalLanding = startLanding;
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

        Vector2 initialLanding = origin + kickDirection * kickTileSize * Mathf.Max(1, distanceTiles);

        if (!TryFindBounceLanding(initialLanding, kickDirection, 40, out var finalLanding))
            return false;

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

        punchRoutine = StartCoroutine(PunchBounceRoutineFixed(origin, initialLanding, finalLanding, punchDuration, punchArcHeight));
        return true;
    }

    private IEnumerator PunchBounceRoutineFixed(
        Vector2 start,
        Vector2 initialLanding,
        Vector2 finalLanding,
        float duration,
        float arcHeight)
    {
        Vector2 segStart = start;
        Vector2 segTarget = initialLanding;

        while (true)
        {
            yield return PunchArcSegmentFixed(segStart, segTarget, duration, arcHeight);

            if (HasExploded)
                break;

            if (segTarget == finalLanding)
                break;

            segStart = segTarget;
            segTarget = segTarget + kickDirection * kickTileSize;

            if (audioSource != null && bounceSfx != null)
                audioSource.PlayOneShot(bounceSfx, bounceSfxVolume);
        }

        Vector2 end = finalLanding;

        rb.position = end;
        transform.position = end;
        lastPos = end;

        isPunched = false;
        punchRoutine = null;

        ResumeFuse();

        if (!HasExploded)
        {
            RecalculateCharactersInsideAt(end);
            bombCollider.isTrigger = charactersInside.Count > 0;
        }

        if (anim != null)
        {
            anim.SetFrozen(false);
            anim.RefreshFrame();
        }
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

    public void Initialize(BombController owner)
    {
        this.owner = owner;
        PlacedTime = Time.time;

        lastPos = rb.position;

        RecalculateCharactersInsideAt(rb.position);
        bombCollider.isTrigger = charactersInside.Count > 0;
    }

    public Vector2 GetLogicalPosition() => lastPos;

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
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (HasExploded)
            return;

        int layer = other.gameObject.layer;

        if (layer == LayerMask.NameToLayer("Explosion"))
        {
            if (isPunched)
            {
                if (owner != null)
                    owner.ExplodeBomb(gameObject);
                return;
            }

            if (!chainExplosionScheduled && owner != null)
            {
                chainExplosionScheduled = true;
                StartCoroutine(DelayedChainExplosion(chainStepDelay));
            }
            return;
        }

        if (layer == LayerMask.NameToLayer("Player") || layer == LayerMask.NameToLayer("Enemy"))
        {
            charactersInside.Add(other);
        }
    }

    private IEnumerator DelayedChainExplosion(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (!HasExploded && owner != null)
            owner.ExplodeBomb(gameObject);
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
}

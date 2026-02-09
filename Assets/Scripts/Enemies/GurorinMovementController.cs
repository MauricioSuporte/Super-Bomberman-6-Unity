using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(CharacterHealth))]
[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(StunReceiver))]
public sealed class GurorinMovementController : EnemyMovementController
{
    [Header("Gurorin Sprites")]
    [SerializeField] private AnimatedSpriteRenderer moveSprite;
    [SerializeField] private AnimatedSpriteRenderer jokeSprite;

    [Header("Gurorin Timing")]
    [SerializeField, Min(0.01f)] private float jokeSeconds = 0.5f;
    [SerializeField, Min(0f)] private float waitAfterJokeSeconds = 0.5f;

    [Header("Junction Stop")]
    [SerializeField, Min(2)] private int minAvailablePathsToStop = 3;

    [Header("Timing Mode")]
    [SerializeField] private bool useUnscaledTime = true;

    Coroutine mainRoutine;
    bool isInJokeOrWait;
    bool isMovingToDecisionPoint;

    static readonly Vector2[] FourDirs = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };

    protected override void Awake()
    {
        base.Awake();

        if (spriteUp != null) spriteUp.enabled = false;
        if (spriteDown != null) spriteDown.enabled = false;
        if (spriteLeft != null) spriteLeft.enabled = false;
        if (spriteDamaged != null) spriteDamaged.enabled = false;
        if (spriteDeath != null) spriteDeath.enabled = false;

        activeSprite = null;

        if (moveSprite != null) moveSprite.enabled = false;
        if (jokeSprite != null) jokeSprite.enabled = false;
    }

    protected override void Start()
    {
        SnapToGrid();

        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        EnsureInitialDirection();

        mainRoutine = StartCoroutine(MainLoop());
    }

    protected override void FixedUpdate()
    {
        if (isDead || isInDamagedLoop)
            return;

        if (TryGetComponent<StunReceiver>(out var stun) && stun != null && stun.IsStunned)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (isInJokeOrWait)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (!isMovingToDecisionPoint)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (HasBombAt(targetTile))
            HandleBombAhead();

        MoveTowardsTile();

        if (ReachedTile())
        {
            SnapToGrid();

            bool stopHere = ShouldStopHere(rb.position, out var _);
            if (stopHere)
            {
                isMovingToDecisionPoint = false;
                rb.linearVelocity = Vector2.zero;
                return;
            }

            Vector2 next = rb.position + direction * tileSize;

            bool blockedNext = IsTileBlocked(next);
            if (blockedNext)
            {
                isMovingToDecisionPoint = false;
                rb.linearVelocity = Vector2.zero;
                return;
            }

            targetTile = next;
        }
    }

    void OnEnable()
    {
        if (isDead) return;

        if (rb == null) rb = GetComponent<Rigidbody2D>();

        isInJokeOrWait = false;
        isMovingToDecisionPoint = false;

        if (mainRoutine == null)
            mainRoutine = StartCoroutine(MainLoop());
    }

    void OnDisable()
    {
        if (mainRoutine != null)
        {
            StopCoroutine(mainRoutine);
            mainRoutine = null;
        }

        isInJokeOrWait = false;
        isMovingToDecisionPoint = false;

        if (rb != null)
            rb.linearVelocity = Vector2.zero;
    }

    IEnumerator MainLoop()
    {
        while (!isDead)
        {
            yield return Joke();
            yield return Wait();
            yield return Joke();
            yield return MoveToDecisionPoint();
        }
    }

    IEnumerator Joke()
    {
        EnsureInitialDirection();

        isMovingToDecisionPoint = false;
        isInJokeOrWait = true;

        rb.linearVelocity = Vector2.zero;

        StopMoveSprite();
        StartJokeSprite(direction);

        try
        {
            yield return WaitWhileAlive(jokeSeconds);
        }
        finally
        {
            isInJokeOrWait = false;
        }
    }

    IEnumerator Wait()
    {
        isInJokeOrWait = true;

        if (jokeSprite != null)
            jokeSprite.SetFrozen(true);

        try
        {
            yield return WaitWhileAlive(waitAfterJokeSeconds);
        }
        finally
        {
            if (jokeSprite != null)
                jokeSprite.SetFrozen(false);

            isInJokeOrWait = false;
        }
    }

    IEnumerator MoveToDecisionPoint()
    {
        SnapToGrid();

        EnsureInitialDirection();

        if (!TryPickInitialDir(out var dir))
            yield break;

        direction = dir;
        targetTile = rb.position + direction * tileSize;

        StopJokeSprite();
        StartMoveSprite(direction);

        isMovingToDecisionPoint = true;

        if (IsTileBlocked(targetTile))
            isMovingToDecisionPoint = false;

        yield return new WaitUntil(() => isDead || !isMovingToDecisionPoint);

        StopMoveSprite();
    }

    void EnsureInitialDirection()
    {
        if (direction != Vector2.zero)
            return;

        var free = GetFreeDirs(rb.position);
        if (free.Count > 0)
        {
            direction = free[Random.Range(0, free.Count)];
            return;
        }

        direction = Vector2.down;
    }

    bool TryPickInitialDir(out Vector2 chosenDir)
    {
        var freeDirs = GetFreeDirs(rb.position);

        if (freeDirs.Count == 0)
        {
            chosenDir = Vector2.zero;
            return false;
        }

        chosenDir = freeDirs[Random.Range(0, freeDirs.Count)];
        return true;
    }

    bool ShouldStopHere(Vector2 pos, out List<Vector2> freeDirs)
    {
        freeDirs = GetFreeDirs(pos);

        if (freeDirs.Count >= minAvailablePathsToStop)
            return true;

        if (freeDirs.Count <= 1)
            return true;

        Vector2 forward = direction;
        bool canGoForward = false;

        for (int i = 0; i < freeDirs.Count; i++)
        {
            if (freeDirs[i] == forward)
            {
                canGoForward = true;
                break;
            }
        }

        if (!canGoForward)
            return true;

        return false;
    }

    List<Vector2> GetFreeDirs(Vector2 pos)
    {
        var freeDirs = new List<Vector2>(4);

        for (int i = 0; i < FourDirs.Length; i++)
        {
            Vector2 d = FourDirs[i];
            Vector2 checkTile = pos + d * tileSize;

            if (!IsTileBlocked(checkTile))
                freeDirs.Add(d);
        }

        return freeDirs;
    }

    IEnumerator WaitWhileAlive(float seconds)
    {
        float start = useUnscaledTime ? Time.unscaledTime : Time.time;
        float end = start + seconds;

        while ((useUnscaledTime ? Time.unscaledTime : Time.time) < end)
        {
            if (isDead)
                yield break;

            bool stunned = false;
            if (TryGetComponent<StunReceiver>(out var stun) && stun != null)
                stunned = stun.IsStunned;

            if (stunned || isInDamagedLoop)
                rb.linearVelocity = Vector2.zero;

            yield return null;
        }
    }

    void StartMoveSprite(Vector2 dir)
    {
        if (moveSprite == null)
            return;

        if (jokeSprite != null)
            jokeSprite.enabled = false;

        activeSprite = moveSprite;
        moveSprite.enabled = true;
        moveSprite.idle = false;
        moveSprite.loop = true;

        if (moveSprite.TryGetComponent<SpriteRenderer>(out var sr))
            sr.flipX = (dir == Vector2.right);
    }

    void StopMoveSprite()
    {
        if (moveSprite != null)
            moveSprite.enabled = false;

        if (activeSprite == moveSprite)
            activeSprite = null;
    }

    void StartJokeSprite(Vector2 dir)
    {
        if (jokeSprite == null)
            return;

        if (moveSprite != null)
            moveSprite.enabled = false;

        activeSprite = jokeSprite;
        jokeSprite.enabled = true;
        jokeSprite.idle = false;
        jokeSprite.loop = false;
        jokeSprite.CurrentFrame = 0;
        jokeSprite.RefreshFrame();

        if (jokeSprite.TryGetComponent<SpriteRenderer>(out var sr))
            sr.flipX = (dir == Vector2.right);
    }

    void StopJokeSprite()
    {
        if (jokeSprite != null)
            jokeSprite.enabled = false;

        if (activeSprite == jokeSprite)
            activeSprite = null;
    }

    protected override void UpdateSpriteDirection(Vector2 dir)
    {
        if (isInDamagedLoop)
            return;

        if (activeSprite != null && activeSprite.TryGetComponent<SpriteRenderer>(out var sr))
            sr.flipX = (dir == Vector2.right);
    }

    protected override void Die()
    {
        if (mainRoutine != null)
        {
            StopCoroutine(mainRoutine);
            mainRoutine = null;
        }

        isMovingToDecisionPoint = false;
        isInJokeOrWait = false;

        if (moveSprite != null) moveSprite.enabled = false;
        if (jokeSprite != null) jokeSprite.enabled = false;

        base.Die();
    }
}

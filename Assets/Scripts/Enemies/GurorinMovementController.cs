using System.Collections;
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

    Coroutine mainRoutine;
    bool isInJokeOrWait;
    bool isInCorridorMove;

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

        if (!isInCorridorMove)
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

            Vector2 next = rb.position + direction * tileSize;
            if (IsTileBlocked(next))
            {
                isInCorridorMove = false;
                rb.linearVelocity = Vector2.zero;
                return;
            }

            targetTile = next;
        }
    }

    IEnumerator MainLoop()
    {
        while (!isDead)
        {
            yield return Joke();
            yield return Wait();
            yield return Joke();
            yield return MoveUntilBlocked();
        }
    }

    IEnumerator Joke()
    {
        isInCorridorMove = false;
        isInJokeOrWait = true;

        rb.linearVelocity = Vector2.zero;

        StopMoveSprite();
        StartJokeSprite(direction);

        yield return WaitWhileAlive(jokeSeconds);

        isInJokeOrWait = false;
    }

    IEnumerator Wait()
    {
        isInJokeOrWait = true;

        if (jokeSprite != null)
            jokeSprite.SetFrozen(true);

        yield return WaitWhileAlive(waitAfterJokeSeconds);

        if (jokeSprite != null)
            jokeSprite.SetFrozen(false);

        isInJokeOrWait = false;
    }

    IEnumerator MoveUntilBlocked()
    {
        SnapToGrid();

        if (!TryPickAnyFreeDirection(out var dir))
            yield break;

        direction = dir;
        targetTile = rb.position + direction * tileSize;

        StopJokeSprite();
        StartMoveSprite(direction);

        isInCorridorMove = true;

        yield return new WaitUntil(() => isDead || !isInCorridorMove);

        StopMoveSprite();
    }

    IEnumerator WaitWhileAlive(float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            if (isDead)
                yield break;

            if (TryGetComponent<StunReceiver>(out var stun) && stun != null && stun.IsStunned)
            {
                rb.linearVelocity = Vector2.zero;
                yield return null;
                continue;
            }

            if (isInDamagedLoop)
            {
                rb.linearVelocity = Vector2.zero;
                yield return null;
                continue;
            }

            t += Time.deltaTime;
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

        if (activeSprite != null &&
            activeSprite.TryGetComponent<SpriteRenderer>(out var sr))
            sr.flipX = (dir == Vector2.right);
    }

    protected override void Die()
    {
        if (mainRoutine != null)
            StopCoroutine(mainRoutine);

        isInCorridorMove = false;
        isInJokeOrWait = false;

        if (moveSprite != null) moveSprite.enabled = false;
        if (jokeSprite != null) jokeSprite.enabled = false;

        base.Die();
    }
}

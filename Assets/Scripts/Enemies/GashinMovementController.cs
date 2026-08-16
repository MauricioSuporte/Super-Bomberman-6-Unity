using System.Collections;
using UnityEngine;

public sealed class GashinMovementController : JunctionTurningEnemyMovementController
{
    private const int EatAnimationCycles = 3;

    [Header("Bomb Eating")]
    [SerializeField] private SpriteRenderer eatRenderer;
    [SerializeField] private Sprite[] eatFrames;
    [SerializeField] private Sprite eatenBombSprite;
    [SerializeField, Min(0.01f)] private float eatAnimationSeconds = 0.4f;
    [SerializeField, Min(0.01f)] private float eatenBombHoldSeconds = 0.25f;

    private Coroutine consumeRoutine;
    private bool consumingBomb;

    protected override void Awake()
    {
        base.Awake();
        if (eatRenderer != null)
            eatRenderer.enabled = false;
    }

    protected override void FixedUpdate()
    {
        if (isDead)
            return;

        if (consumingBomb)
        {
            if (rb != null)
                rb.linearVelocity = Vector2.zero;

            return;
        }

        if (TryGetComponent(out StunReceiver stun) && stun != null && stun.IsStunned)
        {
            if (rb != null)
                rb.linearVelocity = Vector2.zero;

            return;
        }

        if (isInDamagedLoop)
        {
            if (rb != null)
                rb.linearVelocity = Vector2.zero;

            return;
        }

        if (isStuck)
        {
            HandleStuck();
            return;
        }

        // Intentionally do not call HandleBombAhead: Gashin enters a bomb's tile
        // and consumes it instead of turning away.
        Bomb targetBomb = FindBombAt(targetTile);
        if (targetBomb != null)
        {
            BeginConsumeBomb(targetBomb);
            return;
        }

        MoveTowardsTile();

        if (ReachedTile())
        {
            SnapToGrid();

            Bomb bomb = FindBombAt(rb.position);
            if (bomb != null)
            {
                BeginConsumeBomb(bomb);
                return;
            }

            DecideNextTile();
        }
    }

    protected override void OnTriggerEnter2D(Collider2D other)
    {
        if (isDead || consumingBomb)
            return;

        Bomb bomb = other.GetComponentInParent<Bomb>();
        if (bomb != null)
        {
            BeginConsumeBomb(bomb);
            return;
        }

        base.OnTriggerEnter2D(other);
    }

    protected override bool IsTileBlocked(Vector2 tileCenter)
    {
        Vector2 size = Vector2.one * (tileSize * 0.8f);
        Collider2D[] hits = Physics2D.OverlapBoxAll(tileCenter, size, 0f, obstacleMask);

        foreach (Collider2D hit in hits)
        {
            if (hit == null || hit.gameObject == gameObject)
                continue;

            // Bombs are obstacles to normal enemies, but Gashin must select
            // their tile so it can enter it and consume the bomb.
            if (hit.GetComponentInParent<Bomb>() != null)
                continue;

            return true;
        }

        return false;
    }

    protected override void Die()
    {
        if (consumeRoutine != null)
        {
            StopCoroutine(consumeRoutine);
            consumeRoutine = null;
        }

        consumingBomb = false;
        if (eatRenderer != null)
            eatRenderer.enabled = false;

        base.Die();
    }

    private void BeginConsumeBomb(Bomb bomb)
    {
        if (bomb == null || consumingBomb)
        {
            return;
        }

        consumeRoutine = StartCoroutine(ConsumeBombRoutine(bomb));
    }

    private IEnumerator ConsumeBombRoutine(Bomb bomb)
    {
        consumingBomb = true;

        Vector2 bombTile = bomb.transform.position;
        bombTile.x = Mathf.Round(bombTile.x / tileSize) * tileSize;
        bombTile.y = Mathf.Round(bombTile.y / tileSize) * tileSize;

        Vector2 toBomb = bombTile - rb.position;
        if (toBomb.sqrMagnitude > 0.0001f)
        {
            direction = ToCardinal(toBomb);
            UpdateSpriteDirection(direction);
        }

        if (activeSprite != null)
        {
            activeSprite.enabled = true;
            activeSprite.idle = true;
            activeSprite.CurrentFrame = 0;
            activeSprite.RefreshFrame();
        }

        while (!isDead && bomb != null && Vector2.Distance(rb.position, bombTile) > 0.01f)
        {
            rb.MovePosition(Vector2.MoveTowards(rb.position, bombTile, speed * Time.fixedDeltaTime));
            yield return new WaitForFixedUpdate();
        }

        if (isDead)
            yield break;

        rb.position = bombTile;
        targetTile = bombTile;
        DestroyBomb(bomb);

        DisableMovementSprites();
        if (eatRenderer != null)
            eatRenderer.enabled = true;

        int frameCount = eatFrames != null ? eatFrames.Length : 0;
        if (frameCount > 0)
        {
            float secondsPerFrame = eatAnimationSeconds / (EatAnimationCycles * frameCount);
            for (int cycle = 0; cycle < EatAnimationCycles; cycle++)
            {
                for (int frame = 0; frame < frameCount; frame++)
                {
                    if (eatRenderer != null)
                        eatRenderer.sprite = eatFrames[frame];

                    yield return new WaitForSeconds(Mathf.Max(0.01f, secondsPerFrame));

                    if (isDead)
                        yield break;
                }
            }
        }

        if (eatRenderer != null)
            eatRenderer.sprite = eatenBombSprite;

        yield return new WaitForSeconds(Mathf.Max(0.01f, eatenBombHoldSeconds));

        if (eatRenderer != null)
            eatRenderer.enabled = false;

        consumingBomb = false;
        consumeRoutine = null;

        if (!isDead)
        {
            UpdateSpriteDirection(direction);
            DecideNextTile();
        }
    }

    private Bomb FindBombAt(Vector2 position)
    {
        foreach (Bomb activeBomb in Bomb.ActiveBombs)
        {
            if (activeBomb == null || activeBomb.HasExploded)
                continue;

            if (Vector2.Distance(activeBomb.transform.position, position) < tileSize * 0.4f)
                return activeBomb;
        }

        Collider2D hit = Physics2D.OverlapBox(position, Vector2.one * tileSize * 0.8f, 0f, bombLayerMask);
        return hit != null ? hit.GetComponentInParent<Bomb>() : null;
    }

    private static Vector2 ToCardinal(Vector2 vector)
    {
        if (Mathf.Abs(vector.x) >= Mathf.Abs(vector.y))
            return vector.x >= 0f ? Vector2.right : Vector2.left;

        return vector.y >= 0f ? Vector2.up : Vector2.down;
    }

    private void DestroyBomb(Bomb bomb)
    {
        if (bomb == null)
            return;

        BombController owner = bomb.Owner;
        if (owner != null)
            owner.DestroyBombExternally(bomb.gameObject, refund: true);
        else
            Destroy(bomb.gameObject);
    }

    private void DisableMovementSprites()
    {
        if (spriteUp != null) spriteUp.enabled = false;
        if (spriteDown != null) spriteDown.enabled = false;
        if (spriteLeft != null) spriteLeft.enabled = false;
        if (spriteRight != null) spriteRight.enabled = false;
    }

}

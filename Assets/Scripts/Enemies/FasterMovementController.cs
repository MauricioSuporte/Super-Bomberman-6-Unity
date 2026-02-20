using System.Collections;
using UnityEngine;

[RequireComponent(typeof(CharacterHealth))]
public sealed class FasterMovementController : JunctionTurningEnemyMovementController
{
    [Header("Zippo Sprites")]
    [SerializeField] private AnimatedSpriteRenderer moveSpriteNormal;
    [SerializeField] private AnimatedSpriteRenderer moveSpriteEnraged;
    [SerializeField] private AnimatedSpriteRenderer damagedSprite;
    [SerializeField] private AnimatedSpriteRenderer deathSprite;

    [Header("Zippo Behavior")]
    [SerializeField, Min(0.01f)] private float damagedPauseSeconds = 1f;
    [SerializeField, Min(1f)] private float enragedSpeedMultiplier = 3f;

    [Header("Death")]
    [SerializeField, Min(0.01f)] private float deathFreezeSeconds = 0.7f;

    CharacterHealth zippoHealth;

    float baseSpeed;
    bool enraged;
    bool pausedByDamage;
    bool frozenByDeath;
    Coroutine damageRoutine;

    Vector2 frozenTile;

    protected override void Awake()
    {
        base.Awake();

        zippoHealth = GetComponent<CharacterHealth>();
        if (zippoHealth != null)
        {
            zippoHealth.hitInvulnerableDuration = 0f;
            zippoHealth.playDamagedLoopInsteadOfBlink = false;

            zippoHealth.Damaged += OnZippoDamaged;
            zippoHealth.Died += OnZippoDied;
        }

        baseSpeed = speed;

        DisableAllZippoVisuals();
        ApplyMovementVisual();
    }

    protected override void OnDestroy()
    {
        if (zippoHealth != null)
        {
            zippoHealth.Damaged -= OnZippoDamaged;
            zippoHealth.Died -= OnZippoDied;
        }

        base.OnDestroy();
    }

    protected override void FixedUpdate()
    {
        if (isDead)
            return;

        if (pausedByDamage || frozenByDeath)
        {
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.position = frozenTile;
            }
            return;
        }

        base.FixedUpdate();
    }

    protected override void UpdateSpriteDirection(Vector2 dir)
    {
        if (isDead || pausedByDamage || frozenByDeath)
            return;

        ApplyMovementVisual();

        var activeMove = enraged && moveSpriteEnraged != null ? moveSpriteEnraged : moveSpriteNormal;
        if (activeMove == null)
            return;

        if (activeMove.TryGetComponent<SpriteRenderer>(out var sr))
        {
            if (dir == Vector2.right) sr.flipX = true;
            else if (dir == Vector2.left) sr.flipX = false;
        }
    }

    void OnZippoDamaged(int amount)
    {
        if (isDead || frozenByDeath)
            return;

        if (zippoHealth != null && zippoHealth.life <= 0)
            return;

        if (damageRoutine != null)
            StopCoroutine(damageRoutine);

        damageRoutine = StartCoroutine(DamagedRoutine());
    }

    void OnZippoDied()
    {
        if (damageRoutine != null)
        {
            StopCoroutine(damageRoutine);
            damageRoutine = null;
        }

        SnapToGrid();
        frozenTile = rb.position;
        targetTile = frozenTile;

        pausedByDamage = false;
        frozenByDeath = true;

        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        if (moveSpriteNormal != null) moveSpriteNormal.enabled = false;
        if (moveSpriteEnraged != null) moveSpriteEnraged.enabled = false;
        if (damagedSprite != null) damagedSprite.enabled = false;

        if (deathSprite != null)
        {
            deathSprite.enabled = true;
            deathSprite.idle = false;
            deathSprite.loop = false;
            deathSprite.RefreshFrame();
        }

        if (deathFreezeSeconds > 0f)
            Invoke(nameof(ClearDeathFreeze), deathFreezeSeconds);
        else
            ClearDeathFreeze();
    }

    void ClearDeathFreeze()
    {
        frozenByDeath = false;
    }

    IEnumerator DamagedRoutine()
    {
        pausedByDamage = true;

        SnapToGrid();
        frozenTile = rb.position;
        targetTile = frozenTile;

        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        if (zippoHealth != null)
            zippoHealth.StartTemporaryInvulnerability(damagedPauseSeconds, withBlink: false);

        if (moveSpriteNormal != null) moveSpriteNormal.enabled = false;
        if (moveSpriteEnraged != null) moveSpriteEnraged.enabled = false;
        if (deathSprite != null) deathSprite.enabled = false;

        if (damagedSprite != null)
        {
            damagedSprite.enabled = true;
            damagedSprite.idle = false;
            damagedSprite.loop = true;
            damagedSprite.RefreshFrame();
        }

        yield return new WaitForSeconds(damagedPauseSeconds);

        if (isDead)
        {
            pausedByDamage = false;
            yield break;
        }

        enraged = true;
        speed = baseSpeed * enragedSpeedMultiplier;

        if (damagedSprite != null)
            damagedSprite.enabled = false;

        ApplyMovementVisual();

        pausedByDamage = false;

        DecideNextTile();
        UpdateSpriteDirection(direction);

        damageRoutine = null;
    }

    void ApplyMovementVisual()
    {
        if (frozenByDeath)
            return;

        var activeMove = enraged && moveSpriteEnraged != null ? moveSpriteEnraged : moveSpriteNormal;

        if (moveSpriteNormal != null)
        {
            moveSpriteNormal.enabled = (activeMove == moveSpriteNormal);
            if (moveSpriteNormal.enabled)
            {
                moveSpriteNormal.idle = false;
                moveSpriteNormal.loop = true;
            }
        }

        if (moveSpriteEnraged != null)
        {
            moveSpriteEnraged.enabled = (activeMove == moveSpriteEnraged);
            if (moveSpriteEnraged.enabled)
            {
                moveSpriteEnraged.idle = false;
                moveSpriteEnraged.loop = true;
            }
        }

        if (activeMove != null)
            activeSprite = activeMove;
    }

    void DisableAllZippoVisuals()
    {
        if (moveSpriteNormal != null) moveSpriteNormal.enabled = false;
        if (moveSpriteEnraged != null) moveSpriteEnraged.enabled = false;
        if (damagedSprite != null) damagedSprite.enabled = false;
        if (deathSprite != null) deathSprite.enabled = false;
    }
}

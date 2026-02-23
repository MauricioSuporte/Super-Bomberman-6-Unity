using System.Collections;
using UnityEngine;

[RequireComponent(typeof(CharacterHealth))]
public sealed class RoboDashMovementController : JunctionTurningEnemyMovementController
{
    [Header("Zippo Sprites (Directional)")]
    [SerializeField] private AnimatedSpriteRenderer moveUp;
    [SerializeField] private AnimatedSpriteRenderer moveDown;
    [SerializeField] private AnimatedSpriteRenderer moveLeft;
    [SerializeField] private AnimatedSpriteRenderer moveRight;

    [Header("Zippo Sprites (States)")]
    [SerializeField] private AnimatedSpriteRenderer damagedSprite;
    [SerializeField] private AnimatedSpriteRenderer deathSprite;

    [Header("Zippo Behavior")]
    [SerializeField, Min(0.01f)] private float damagedPauseSeconds = 1f;
    [SerializeField, Min(1f)] private float enragedSpeedMultiplier = 3f;

    [Header("Death")]
    [SerializeField, Min(0.01f)] private float deathFreezeSeconds = 0.7f;

    CharacterHealth zippoHealth;

    float baseSpeed;
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
        ApplyMovementVisual(direction);
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

        ApplyMovementVisual(dir);
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

        DisableAllZippoVisuals();

        if (deathSprite != null)
        {
            deathSprite.enabled = true;
            deathSprite.idle = false;
            deathSprite.loop = false;
            deathSprite.RefreshFrame();
            activeSprite = deathSprite;
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

        DisableAllZippoVisuals();

        if (damagedSprite != null)
        {
            damagedSprite.enabled = true;
            damagedSprite.idle = false;
            damagedSprite.loop = true;
            damagedSprite.RefreshFrame();
            activeSprite = damagedSprite;
        }

        yield return new WaitForSeconds(damagedPauseSeconds);

        if (isDead)
        {
            pausedByDamage = false;
            yield break;
        }

        speed = baseSpeed * enragedSpeedMultiplier;

        if (damagedSprite != null)
            damagedSprite.enabled = false;

        ApplyMovementVisual(direction);

        pausedByDamage = false;

        DecideNextTile();
        UpdateSpriteDirection(direction);

        damageRoutine = null;
    }

    void ApplyMovementVisual(Vector2 dir)
    {
        if (frozenByDeath)
            return;

        if (damagedSprite != null) damagedSprite.enabled = false;
        if (deathSprite != null && deathSprite != activeSprite) deathSprite.enabled = false;

        AnimatedSpriteRenderer chosen = ChooseDirectionalSprite(dir);

        if (moveUp != null) moveUp.enabled = (chosen == moveUp);
        if (moveDown != null) moveDown.enabled = (chosen == moveDown);
        if (moveLeft != null) moveLeft.enabled = (chosen == moveLeft);
        if (moveRight != null) moveRight.enabled = (chosen == moveRight);

        if (chosen != null)
        {
            chosen.idle = false;
            chosen.loop = true;
            chosen.enabled = true;

            if (activeSprite != null && activeSprite != chosen)
            {
                int frame = activeSprite.CurrentFrame;
                if (chosen.animationSprite != null && chosen.animationSprite.Length > 0)
                    frame = Mathf.Clamp(frame, 0, chosen.animationSprite.Length - 1);

                chosen.CurrentFrame = frame;
                chosen.RefreshFrame();
            }

            activeSprite = chosen;
        }
        else
        {
            if (activeSprite != null)
            {
                activeSprite.enabled = true;
                activeSprite.idle = false;
            }
        }
    }

    AnimatedSpriteRenderer ChooseDirectionalSprite(Vector2 dir)
    {
        if (dir == Vector2.up) return moveUp != null ? moveUp : moveDown;
        if (dir == Vector2.down) return moveDown != null ? moveDown : moveUp;
        if (dir == Vector2.left) return moveLeft != null ? moveLeft : moveRight;
        if (dir == Vector2.right) return moveRight != null ? moveRight : moveLeft;

        return moveDown != null ? moveDown : moveLeft != null ? moveLeft : moveRight != null ? moveRight : moveUp;
    }

    void DisableAllZippoVisuals()
    {
        if (moveUp != null) moveUp.enabled = false;
        if (moveDown != null) moveDown.enabled = false;
        if (moveLeft != null) moveLeft.enabled = false;
        if (moveRight != null) moveRight.enabled = false;

        if (damagedSprite != null) damagedSprite.enabled = false;
        if (deathSprite != null) deathSprite.enabled = false;
    }
}

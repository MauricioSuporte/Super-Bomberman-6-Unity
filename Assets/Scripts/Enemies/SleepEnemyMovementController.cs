using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(CharacterHealth))]
[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(StunReceiver))]
public class SleepEnemyMovementController : JunctionTurningEnemyMovementController
{
    [Header("Single Direction Sprite")]
    [SerializeField] private AnimatedSpriteRenderer mainSprite;
    [SerializeField] private bool flipXOnRight = true;

    [Header("Stop Ability Sprites")]
    [SerializeField] private AnimatedSpriteRenderer abilityStartSprite;
    [SerializeField] private AnimatedSpriteRenderer abilityLoopSprite;
    [SerializeField] private AnimatedSpriteRenderer abilityEndSprite;

    [Header("Stop Ability Settings")]
    [SerializeField, Min(0.1f)] private float stopSeconds = 10f;
    [SerializeField, Range(0f, 1f)] private float chanceToTriggerAtJunction = 0.35f;
    [SerializeField, Min(0f)] private float cooldownSeconds = 2f;

    [Header("Ability Timing (Forced)")]
    [SerializeField, Min(0.05f)] private float abilityStartDurationSeconds = 1f;
    [SerializeField, Min(0.05f)] private float abilityEndDurationSeconds = 1f;
    [SerializeField, Min(0.05f)] private float abilityLoopCycleSeconds = 2.4f;
    [SerializeField, Min(1)] private int abilityLoopCycles = 5;

    [Header("Walk -> Pause Cycle (by main sprite active time)")]
    [SerializeField, Min(0.1f)] private float walkSecondsBeforePause = 5f;

    private CharacterHealth _health;
    private Coroutine _abilityRoutine;

    private bool _isUsingAbility;
    private float _cooldownRemaining;

    private float _walkTimer;
    private bool _walkTimerPrimed;

    protected override void Awake()
    {
        base.Awake();

        if (mainSprite != null)
        {
            spriteDown = mainSprite;
            if (activeSprite == null)
                activeSprite = mainSprite;
        }

        _health = GetComponent<CharacterHealth>();
        if (_health != null)
        {
            _health.HitInvulnerabilityStarted += OnAnyHitInvulnerabilityStarted;
            _health.Died += OnAnyDied;
        }

        DisableAbilitySprites();
        EnsureMainSpriteOn();
    }

    protected override void Start()
    {
        base.Start();
        ResetWalkTimer();
    }

    protected override void OnDestroy()
    {
        if (_health != null)
        {
            _health.HitInvulnerabilityStarted -= OnAnyHitInvulnerabilityStarted;
            _health.Died -= OnAnyDied;
        }

        base.OnDestroy();
    }

    protected override void FixedUpdate()
    {
        if (isDead)
            return;

        if (_cooldownRemaining > 0f)
            _cooldownRemaining -= Time.fixedDeltaTime;

        if (_isUsingAbility)
        {
            if (rb != null)
                rb.linearVelocity = Vector2.zero;

            return;
        }

        base.FixedUpdate();

        UpdateWalkTimerByMainSpriteActive();

        bool shouldAutoPause =
            _walkTimerPrimed &&
            _walkTimer >= walkSecondsBeforePause &&
            _cooldownRemaining <= 0f;

        if (shouldAutoPause)
        {
            ResetWalkTimer();
            StartAbilityStop();
            targetTile = rb.position;
        }
    }

    protected override void UpdateSpriteDirection(Vector2 dir)
    {
        if (isInDamagedLoop)
            return;

        if (mainSprite == null)
        {
            base.UpdateSpriteDirection(dir);
            return;
        }

        DisableAbilitySprites();

        if (spriteUp != null && spriteUp != mainSprite) spriteUp.enabled = false;
        if (spriteLeft != null && spriteLeft != mainSprite) spriteLeft.enabled = false;
        if (spriteDown != null && spriteDown != mainSprite) spriteDown.enabled = false;

        if (spriteDamaged != null) spriteDamaged.enabled = false;
        if (spriteDeath != null && spriteDeath != activeSprite) spriteDeath.enabled = false;

        activeSprite = mainSprite;
        mainSprite.enabled = true;
        mainSprite.idle = false;

        if (flipXOnRight && mainSprite.TryGetComponent<SpriteRenderer>(out var sr))
            sr.flipX = (dir == Vector2.right);
    }

    protected override void DecideNextTile()
    {
        if (_isUsingAbility || isDead || isInDamagedLoop)
        {
            targetTile = rb.position;
            return;
        }

        Vector2[] dirs = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
        var freeDirs = new List<Vector2>(4);

        for (int i = 0; i < dirs.Length; i++)
        {
            Vector2 dir = dirs[i];
            Vector2 checkTile = rb.position + dir * tileSize;

            if (!IsTileBlocked(checkTile))
                freeDirs.Add(dir);
        }

        if (freeDirs.Count == 0)
        {
            targetTile = rb.position;
            UpdateSpriteDirection(direction);
            return;
        }

        bool isJunction = freeDirs.Count >= minAvailablePathsToTurn;

        if (isJunction && CanTriggerAbilityHere())
        {
            StartAbilityStop();
            targetTile = rb.position;
            return;
        }

        if (isJunction)
        {
            Vector2 chosenDir = direction;

            if (preferTurnAtJunction)
            {
                var turningDirs = new List<Vector2>(freeDirs.Count);

                for (int i = 0; i < freeDirs.Count; i++)
                {
                    Vector2 d = freeDirs[i];
                    if (d != direction)
                        turningDirs.Add(d);
                }

                if (turningDirs.Count > 0)
                    chosenDir = turningDirs[Random.Range(0, turningDirs.Count)];
                else
                    chosenDir = freeDirs[Random.Range(0, freeDirs.Count)];
            }
            else
            {
                chosenDir = freeDirs[Random.Range(0, freeDirs.Count)];
            }

            direction = chosenDir;
            UpdateSpriteDirection(direction);
            targetTile = rb.position + direction * tileSize;
            return;
        }

        base.DecideNextTile();
    }

    protected override void Die()
    {
        CancelAbilityStop();
        base.Die();
    }

    private void ResetWalkTimer()
    {
        _walkTimer = 0f;
        _walkTimerPrimed = false;
    }

    private void UpdateWalkTimerByMainSpriteActive()
    {
        if (walkSecondsBeforePause <= 0f)
        {
            _walkTimer = 0f;
            _walkTimerPrimed = false;
            return;
        }

        bool mainActive = mainSprite != null && mainSprite.enabled;
        bool canCount =
            mainActive &&
            !_isUsingAbility &&
            !isDead &&
            !isInDamagedLoop &&
            !IsStunned() &&
            !isStuck;

        if (canCount)
        {
            _walkTimerPrimed = true;
            _walkTimer += Time.fixedDeltaTime;
            return;
        }

        _walkTimer = 0f;
        _walkTimerPrimed = false;
    }

    private bool CanTriggerAbilityHere()
    {
        if (_cooldownRemaining > 0f)
            return false;

        if (chanceToTriggerAtJunction <= 0f)
            return false;

        if (chanceToTriggerAtJunction >= 1f)
            return true;

        return Random.value <= chanceToTriggerAtJunction;
    }

    private void StartAbilityStop()
    {
        if (_isUsingAbility || _cooldownRemaining > 0f)
            return;

        if (_abilityRoutine != null)
            StopCoroutine(_abilityRoutine);

        _abilityRoutine = StartCoroutine(AbilityStopRoutine());
    }

    private void CancelAbilityStop()
    {
        _isUsingAbility = false;

        if (_abilityRoutine != null)
        {
            StopCoroutine(_abilityRoutine);
            _abilityRoutine = null;
        }

        DisableAbilitySprites();
        EnsureMainSpriteOn();
        ResetWalkTimer();

        if (cooldownSeconds > 0f)
            _cooldownRemaining = cooldownSeconds;
    }

    private IEnumerator AbilityStopRoutine()
    {
        _isUsingAbility = true;
        ResetWalkTimer();

        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        EnsureMainSpriteOn();

        float startDur = Mathf.Max(0.01f, abilityStartDurationSeconds);
        float endDur = Mathf.Max(0.01f, abilityEndDurationSeconds);
        float loopCycle = Mathf.Max(0.01f, abilityLoopCycleSeconds);
        int cycles = Mathf.Max(1, abilityLoopCycles);

        if (abilityStartSprite != null)
        {
            EnableOnly(abilityStartSprite);
            ConfigureAsOneShot(abilityStartSprite);
            yield return new WaitForSecondsRealtime(startDur);
        }

        if (abilityLoopSprite != null)
        {
            EnableOnly(abilityLoopSprite);
            abilityLoopSprite.idle = false;
            abilityLoopSprite.loop = true;

            for (int i = 0; i < cycles; i++)
            {
                if (isDead || !_isUsingAbility)
                    break;

                float remainingThisCycle = loopCycle;

                while (remainingThisCycle > 0f && !isDead && _isUsingAbility)
                {
                    remainingThisCycle -= Time.unscaledDeltaTime;
                    if (rb != null) rb.linearVelocity = Vector2.zero;
                    yield return null;
                }
            }
        }
        else
        {
            float remaining = Mathf.Max(0f, stopSeconds);

            while (remaining > 0f && !isDead && _isUsingAbility)
            {
                remaining -= Time.unscaledDeltaTime;
                if (rb != null) rb.linearVelocity = Vector2.zero;
                yield return null;
            }
        }

        if (isDead)
            yield break;

        if (abilityEndSprite != null)
        {
            EnableOnly(abilityEndSprite);
            ConfigureAsOneShot(abilityEndSprite);
            yield return new WaitForSecondsRealtime(endDur);
        }

        _isUsingAbility = false;
        _abilityRoutine = null;

        DisableAbilitySprites();
        EnsureMainSpriteOn();

        if (cooldownSeconds > 0f)
            _cooldownRemaining = cooldownSeconds;

        DecideNextTile();
    }

    private void EnableOnly(AnimatedSpriteRenderer target)
    {
        if (mainSprite != null) mainSprite.enabled = false;

        if (abilityStartSprite != null) abilityStartSprite.enabled = (abilityStartSprite == target);
        if (abilityLoopSprite != null) abilityLoopSprite.enabled = (abilityLoopSprite == target);
        if (abilityEndSprite != null) abilityEndSprite.enabled = (abilityEndSprite == target);

        if (target != null)
        {
            target.enabled = true;
            target.idle = false;
        }
    }

    private void DisableAbilitySprites()
    {
        if (abilityStartSprite != null) abilityStartSprite.enabled = false;
        if (abilityLoopSprite != null) abilityLoopSprite.enabled = false;
        if (abilityEndSprite != null) abilityEndSprite.enabled = false;
    }

    private void EnsureMainSpriteOn()
    {
        if (mainSprite == null)
            return;

        if (isInDamagedLoop || isDead)
            return;

        mainSprite.enabled = true;
        mainSprite.idle = false;

        if (activeSprite != mainSprite)
            activeSprite = mainSprite;
    }

    private void ConfigureAsOneShot(AnimatedSpriteRenderer r)
    {
        if (r == null)
            return;

        r.idle = false;
        r.loop = false;
        r.CurrentFrame = 0;
        r.RefreshFrame();
    }

    private void OnAnyHitInvulnerabilityStarted(float seconds)
    {
        CancelAbilityStop();
    }

    private void OnAnyDied()
    {
        CancelAbilityStop();
    }

    private bool IsStunned()
    {
        if (!TryGetComponent<StunReceiver>(out var stun) || stun == null)
            return false;

        return stun.IsStunned;
    }
}

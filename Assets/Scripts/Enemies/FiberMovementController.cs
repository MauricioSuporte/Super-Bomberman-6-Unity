using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(CharacterHealth))]
[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(StunReceiver))]
public sealed class FiberMovementController : JunctionTurningEnemyMovementController
{
    [Header("Ability - Cooldown")]
    [SerializeField] private float abilityMinCooldown = 6f;
    [SerializeField] private float abilityMaxCooldown = 8f;

    [Header("Ability - Timing")]
    [SerializeField, Min(0.01f)] private float abilityAnimationDurationSeconds = 1.2f;
    [SerializeField, Min(0f)] private float fireSpawnDelaySeconds = 0.35f;
    [SerializeField, Min(0.01f)] private float fireLifetimeSeconds = 0.5f;

    [Header("Ability - Fire")]
    [SerializeField] private FiberFlame firePrefab;
    [SerializeField] private bool useCurrentDirectionForFire = false;
    [SerializeField] private bool spawnFireOnAdjacentTile = true;

    [Header("Ability - Sprite")]
    [SerializeField] private AnimatedSpriteRenderer spriteAbility;

    [Header("Debug")]
    [SerializeField] private bool debugAbility = true;

    private Coroutine abilityLoopRoutine;
    private bool isAbilityActive;
    private bool started;

    private void OnEnable()
    {
        if (debugAbility)
            Debug.Log($"[Fiber] {name} OnEnable timeScale={Time.timeScale:0.###} t={Time.time:0.00} rt={Time.realtimeSinceStartup:0.00}", this);

        TryStartAbilityLoop();
    }

    private void OnDisable()
    {
        if (debugAbility)
            Debug.Log($"[Fiber] {name} OnDisable timeScale={Time.timeScale:0.###} t={Time.time:0.00} rt={Time.realtimeSinceStartup:0.00}", this);

        StopAbilityLoop();
    }

    protected override void Start()
    {
        base.Start();

        started = true;

        if (debugAbility)
            Debug.Log($"[Fiber] {name} Start() firePrefab={(firePrefab != null)} spriteAbility={(spriteAbility != null)} timeScale={Time.timeScale:0.###}", this);

        TryStartAbilityLoop();
    }

    protected override void FixedUpdate()
    {
        if (isDead)
            return;

        if (isAbilityActive)
        {
            if (rb != null)
                rb.linearVelocity = Vector2.zero;

            return;
        }

        base.FixedUpdate();
    }

    protected override void Die()
    {
        if (debugAbility)
            Debug.Log($"[Fiber] {name} Die() timeScale={Time.timeScale:0.###} t={Time.time:0.00} rt={Time.realtimeSinceStartup:0.00}", this);

        StopAbilityLoop();
        isAbilityActive = false;

        base.Die();
    }

    private void TryStartAbilityLoop()
    {
        if (!started)
        {
            if (debugAbility)
                Debug.Log($"[Fiber] {name} TryStartAbilityLoop skipped (not started yet)", this);
            return;
        }

        if (!isActiveAndEnabled)
        {
            if (debugAbility)
                Debug.Log($"[Fiber] {name} TryStartAbilityLoop skipped (not active/enabled)", this);
            return;
        }

        if (isDead)
        {
            if (debugAbility)
                Debug.Log($"[Fiber] {name} TryStartAbilityLoop skipped (dead)", this);
            return;
        }

        if (abilityLoopRoutine != null)
        {
            if (debugAbility)
                Debug.Log($"[Fiber] {name} AbilityLoop already running", this);
            return;
        }

        abilityLoopRoutine = StartCoroutine(AbilityLoop());

        if (debugAbility)
            Debug.Log($"[Fiber] {name} AbilityLoop coroutine started", this);
    }

    private void StopAbilityLoop()
    {
        if (abilityLoopRoutine != null)
        {
            StopCoroutine(abilityLoopRoutine);
            abilityLoopRoutine = null;

            if (debugAbility)
                Debug.Log($"[Fiber] {name} AbilityLoop coroutine stopped", this);
        }
    }

    private IEnumerator AbilityLoop()
    {
        if (debugAbility)
            Debug.Log($"[Fiber] {name} AbilityLoop STARTED", this);

        while (!isDead && isActiveAndEnabled)
        {
            float waitTime = Random.Range(abilityMinCooldown, abilityMaxCooldown);

            if (debugAbility)
                Debug.Log($"[Fiber] {name} cooldown {waitTime:0.00}s", this);

            yield return new WaitForSecondsRealtime(waitTime);

            if (!isActiveAndEnabled || isDead)
            {
                if (debugAbility)
                    Debug.Log($"[Fiber] {name} AbilityLoop END (disabled or dead after wait)", this);
                yield break;
            }

            if (TryGetComponent(out StunReceiver stun) && stun != null && stun.IsStunned)
            {
                if (debugAbility)
                    Debug.Log($"[Fiber] {name} skipped ability (stunned)", this);
                continue;
            }

            if (isInDamagedLoop)
            {
                if (debugAbility)
                    Debug.Log($"[Fiber] {name} skipped ability (damaged loop)", this);
                continue;
            }

            if (isAbilityActive)
            {
                if (debugAbility)
                    Debug.Log($"[Fiber] {name} skipped ability (already active)", this);
                continue;
            }

            if (spriteAbility == null)
            {
                Debug.LogWarning($"[Fiber] {name} spriteAbility == NULL", this);
                continue;
            }

            if (firePrefab == null)
            {
                Debug.LogWarning($"[Fiber] {name} firePrefab == NULL", this);
                continue;
            }

            if (debugAbility)
                Debug.Log($"[Fiber] {name} EXECUTING ability", this);

            yield return ExecuteAbility();
        }

        if (debugAbility)
            Debug.Log($"[Fiber] {name} AbilityLoop EXIT (dead or disabled)", this);
    }

    private IEnumerator ExecuteAbility()
    {
        if (debugAbility)
            Debug.Log($"[Fiber] {name} ExecuteAbility START", this);

        isAbilityActive = true;

        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        SnapToGrid();

        float duration = Mathf.Max(0.01f, abilityAnimationDurationSeconds);
        float spawnDelay = Mathf.Clamp(fireSpawnDelaySeconds, 0f, duration);

        var h = GetComponent<CharacterHealth>();
        if (h != null)
        {
            h.StartTemporaryInvulnerability(duration, withBlink: false);
            if (debugAbility)
                Debug.Log($"[Fiber] {name} invulnerable for {duration:0.00}s", this);
        }

        DisableAllForAbility();
        EnableAbilitySprite();

        float elapsed = 0f;
        bool fired = false;

        while (elapsed < duration)
        {
            if (!isActiveAndEnabled || isDead)
            {
                if (debugAbility)
                    Debug.Log($"[Fiber] {name} ExecuteAbility interrupted (disabled or dead)", this);
                break;
            }

            if (!fired && elapsed >= spawnDelay)
            {
                fired = true;
                if (debugAbility)
                    Debug.Log($"[Fiber] {name} SpawnFire at t={elapsed:0.00}s", this);

                SpawnFire();
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        DisableAbilitySprite();
        isAbilityActive = false;

        UpdateSpriteDirection(direction);
        DecideNextTile();

        if (debugAbility)
            Debug.Log($"[Fiber] {name} ExecuteAbility END", this);
    }

    private void SpawnFire()
    {
        Vector2 dir = useCurrentDirectionForFire ? direction : Dirs[Random.Range(0, Dirs.Length)];
        if (dir == Vector2.zero)
            dir = Vector2.down;

        Vector2 origin = rb != null ? rb.position : (Vector2)transform.position;
        origin.x = Mathf.Round(origin.x / tileSize) * tileSize;
        origin.y = Mathf.Round(origin.y / tileSize) * tileSize;

        Vector2 pos = spawnFireOnAdjacentTile ? origin + dir * tileSize : origin;

        if (debugAbility)
            Debug.Log($"[Fiber] {name} SpawnFire dir={dir} pos={pos} lifetime={fireLifetimeSeconds:0.00}s", this);

        var flame = Instantiate(firePrefab, pos, Quaternion.identity);
        if (flame == null)
        {
            Debug.LogWarning($"[Fiber] {name} Instantiate(firePrefab) returned NULL", this);
            return;
        }

        flame.Play(dir, fireLifetimeSeconds);
    }

    private void EnableAbilitySprite()
    {
        if (spriteAbility == null)
            return;

        activeSprite = spriteAbility;
        spriteAbility.enabled = true;
        spriteAbility.idle = false;
        spriteAbility.loop = true;
    }

    private void DisableAbilitySprite()
    {
        if (spriteAbility != null)
            spriteAbility.enabled = false;
    }

    private void DisableAllForAbility()
    {
        if (spriteUp != null) spriteUp.enabled = false;
        if (spriteDown != null) spriteDown.enabled = false;
        if (spriteLeft != null) spriteLeft.enabled = false;
        if (spriteDamaged != null) spriteDamaged.enabled = false;
        if (spriteDeath != null && spriteDeath != activeSprite) spriteDeath.enabled = false;
        if (spriteAbility != null) spriteAbility.enabled = false;
    }
}

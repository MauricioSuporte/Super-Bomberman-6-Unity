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

    [Header("Ability - Phases (fixed)")]
    [SerializeField, Min(0.01f)] private float prepareSeconds = 0.5f;
    [SerializeField, Min(0.01f)] private float flameSeconds = 0.5f;
    [SerializeField, Min(0.01f)] private float recoverSeconds = 0.5f;

    [Header("Ability - Fire")]
    [SerializeField] private FiberFlame firePrefab;
    [SerializeField] private bool useCurrentDirectionForFire = false;
    [SerializeField] private bool spawnFireOnAdjacentTile = true;

    [Header("Ability - Targeting")]
    [SerializeField, Min(0.1f)] private float targetSearchRadiusTiles = 12f;

    [Header("Ability - Layers (optional override)")]
    [SerializeField] private LayerMask playerLayerMask;

    [Header("Ability - Sprite")]
    [SerializeField] private AnimatedSpriteRenderer spriteAbility;

    private Coroutine abilityLoopRoutine;
    private bool isAbilityActive;
    private bool started;

    private Vector2 lastAbilityDir = Vector2.down;

    private void OnEnable()
    {
        TryStartAbilityLoop();
    }

    private void OnDisable()
    {
        StopAbilityLoop();
    }

    protected override void Start()
    {
        base.Start();

        if (playerLayerMask.value == 0)
            playerLayerMask = LayerMask.GetMask("Player");

        if (bombLayerMask.value == 0)
            bombLayerMask = LayerMask.GetMask("Bomb");

        started = true;

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
        StopAbilityLoop();
        isAbilityActive = false;

        base.Die();
    }

    private void TryStartAbilityLoop()
    {
        if (!started)
            return;

        if (!isActiveAndEnabled)
            return;

        if (isDead)
            return;

        if (abilityLoopRoutine != null)
            return;

        abilityLoopRoutine = StartCoroutine(AbilityLoop());
    }

    private void StopAbilityLoop()
    {
        if (abilityLoopRoutine != null)
        {
            StopCoroutine(abilityLoopRoutine);
            abilityLoopRoutine = null;
        }
    }

    private IEnumerator AbilityLoop()
    {
        while (!isDead && isActiveAndEnabled)
        {
            float waitTime = Random.Range(abilityMinCooldown, abilityMaxCooldown);
            yield return new WaitForSecondsRealtime(waitTime);

            if (!isActiveAndEnabled || isDead)
                yield break;

            if (TryGetComponent(out StunReceiver stun) && stun != null && stun.IsStunned)
                continue;

            if (isInDamagedLoop)
                continue;

            if (isAbilityActive)
                continue;

            if (spriteAbility == null)
                continue;

            if (firePrefab == null)
                continue;

            yield return ExecuteAbility();
        }
    }

    private IEnumerator ExecuteAbility()
    {
        isAbilityActive = true;

        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        SnapToGrid();

        float p = Mathf.Max(0.01f, prepareSeconds);
        float f = Mathf.Max(0.01f, flameSeconds);
        float r = Mathf.Max(0.01f, recoverSeconds);

        float total = p + f + r;

        var h = GetComponent<CharacterHealth>();
        if (h != null)
            h.StartTemporaryInvulnerability(total, withBlink: false);

        DisableAllForAbility();
        EnableAbilitySprite();

        yield return WaitSecondsGameplay(p);

        if (!isActiveAndEnabled || isDead)
            goto END;

        lastAbilityDir = PickAbilityDirection();
        SpawnFireWithExactLifetime(f, lastAbilityDir);

        yield return WaitSecondsGameplay(f);

        if (!isActiveAndEnabled || isDead)
            goto END;

        yield return WaitSecondsGameplay(r);

    END:
        DisableAbilitySprite();
        isAbilityActive = false;

        UpdateSpriteDirection(direction);
        DecideNextTile();
    }

    private IEnumerator WaitSecondsGameplay(float seconds)
    {
        if (seconds <= 0f)
            yield break;

        float t = 0f;
        while (t < seconds)
        {
            if (!isActiveAndEnabled || isDead)
                yield break;

            t += Time.deltaTime;
            yield return null;
        }
    }

    private Vector2 PickAbilityDirection()
    {
        Vector2 origin = rb != null ? rb.position : (Vector2)transform.position;
        origin.x = Mathf.Round(origin.x / tileSize) * tileSize;
        origin.y = Mathf.Round(origin.y / tileSize) * tileSize;

        float radius = Mathf.Max(tileSize, targetSearchRadiusTiles * tileSize);

        if (TryGetClosestAlivePlayer(origin, out Vector2 playerPos, radius))
        {
            Vector2 toPlayer = playerPos - origin;
            return BestCardinalFromVector(toPlayer);
        }

        if (TryGetClosestBomb(origin, out Vector2 bombPos, radius))
        {
            Vector2 toBomb = bombPos - origin;
            return BestCardinalFromVector(toBomb);
        }

        Vector2 fallback = direction != Vector2.zero ? direction : Vector2.down;
        return PickBiasedRandomCardinal(fallback, fallback);
    }

    private bool TryGetClosestAlivePlayer(Vector2 origin, out Vector2 bestPos, float radius)
    {
        bestPos = Vector2.zero;
        float bestSqr = float.PositiveInfinity;

        var players = FindObjectsByType<MovementController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (players == null || players.Length == 0)
            return false;

        float rSqr = radius * radius;

        for (int i = 0; i < players.Length; i++)
        {
            var m = players[i];
            if (m == null) continue;
            if (!m.isActiveAndEnabled || !m.gameObject.activeInHierarchy) continue;
            if (!m.CompareTag("Player")) continue;
            if (m.isDead) continue;

            int layer = m.gameObject.layer;
            if (playerLayerMask.value != 0 && ((1 << layer) & playerLayerMask.value) == 0)
                continue;

            Vector2 p = m.transform.position;
            float d = (p - origin).sqrMagnitude;
            if (d > rSqr) continue;

            if (d < bestSqr)
            {
                bestSqr = d;
                bestPos = p;
            }
        }

        return bestSqr < float.PositiveInfinity;
    }

    private bool TryGetClosestBomb(Vector2 origin, out Vector2 bestPos, float radius)
    {
        bestPos = Vector2.zero;
        float bestSqr = float.PositiveInfinity;

        if (bombLayerMask.value == 0)
            return false;

        var hits = Physics2D.OverlapCircleAll(origin, radius, bombLayerMask);
        if (hits == null || hits.Length == 0)
            return false;

        for (int i = 0; i < hits.Length; i++)
        {
            var c = hits[i];
            if (c == null) continue;

            Vector2 p = c.transform.position;
            float d = (p - origin).sqrMagnitude;

            if (d < bestSqr)
            {
                bestSqr = d;
                bestPos = p;
            }
        }

        return bestSqr < float.PositiveInfinity;
    }

    private Vector2 BestCardinalFromVector(Vector2 v)
    {
        if (v.sqrMagnitude < 0.0001f)
            return Vector2.down;

        if (Mathf.Abs(v.x) >= Mathf.Abs(v.y))
            return v.x >= 0f ? Vector2.right : Vector2.left;

        return v.y >= 0f ? Vector2.up : Vector2.down;
    }

    private Vector2 PickBiasedRandomCardinal(Vector2 preferred1, Vector2 preferred2)
    {
        preferred1 = BestCardinalFromVector(preferred1);
        preferred2 = BestCardinalFromVector(preferred2);

        float wUp = 1f;
        float wDown = 1f;
        float wLeft = 1f;
        float wRight = 1f;

        if (preferred1 == Vector2.up) wUp += 2.5f;
        else if (preferred1 == Vector2.down) wDown += 2.5f;
        else if (preferred1 == Vector2.left) wLeft += 2.5f;
        else if (preferred1 == Vector2.right) wRight += 2.5f;

        if (preferred2 == Vector2.up) wUp += 2.5f;
        else if (preferred2 == Vector2.down) wDown += 2.5f;
        else if (preferred2 == Vector2.left) wLeft += 2.5f;
        else if (preferred2 == Vector2.right) wRight += 2.5f;

        float sum = wUp + wDown + wLeft + wRight;
        float r = Random.Range(0f, sum);

        if ((r -= wUp) < 0f) return Vector2.up;
        if ((r -= wDown) < 0f) return Vector2.down;
        if ((r -= wLeft) < 0f) return Vector2.left;
        return Vector2.right;
    }

    private void SpawnFireWithExactLifetime(float exactLifetimeSeconds, Vector2 chosenDir)
    {
        Vector2 dir = chosenDir;

        if (useCurrentDirectionForFire)
            dir = direction != Vector2.zero ? direction : Vector2.down;

        dir = BestCardinalFromVector(dir);

        Vector2 origin = rb != null ? rb.position : (Vector2)transform.position;
        origin.x = Mathf.Round(origin.x / tileSize) * tileSize;
        origin.y = Mathf.Round(origin.y / tileSize) * tileSize;

        Vector2 pos = spawnFireOnAdjacentTile ? origin + dir * tileSize : origin;

        var flame = Instantiate(firePrefab, pos, Quaternion.identity);
        if (flame == null)
            return;

        flame.Play(dir, exactLifetimeSeconds);
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

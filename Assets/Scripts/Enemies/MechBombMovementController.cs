using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(AudioSource))]
public class MechBombMovementController : JunctionTurningEnemyMovementController, IMagnetPullable
{
    [Header("Ability - Cooldown")]
    public float abilityMinCooldown = 8f;
    public float abilityMaxCooldown = 10f;

    [Header("Ability - Explosion")]
    public int explosionRadius = 4;
    public bool pierceExplosion = false;

    [Header("Ability - Sprite (Single)")]
    public AnimatedSpriteRenderer spriteAbility;

    [Header("Ability - Frame Ranges (14 frames @ 0.25s)")]
    public int landStartFrame = 0;
    public int landEndFrame = 1;

    public int armedStartFrame = 2;
    public int armedEndFrame = 9;

    public int detonateStartFrame = 10;
    public int detonateEndFrame = 11;

    public int riseStartFrame = 12;
    public int riseEndFrame = 13;

    BombController cachedBombController;
    AudioSource mechAudioSource;

    Coroutine abilityLoopRoutine;
    bool isAbilityActive;

    [Header("Magnet Pull")]
    [SerializeField] private float magnetPullSpeed = 10f;

    private Coroutine magnetRoutine;

    public bool IsBeingMagnetPulled => magnetRoutine != null;

    public bool CanBeMagnetPulled => isAbilityActive && !isDead && magnetRoutine == null;

    protected override void Awake()
    {
        base.Awake();
        mechAudioSource = GetComponent<AudioSource>();
        if (mechAudioSource != null)
        {
            mechAudioSource.playOnAwake = false;
            mechAudioSource.loop = false;
        }
    }

    protected override void Start()
    {
        base.Start();

        CacheBombController();

        if (abilityLoopRoutine != null)
            StopCoroutine(abilityLoopRoutine);

        abilityLoopRoutine = StartCoroutine(AbilityLoop());
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
        if (abilityLoopRoutine != null)
        {
            StopCoroutine(abilityLoopRoutine);
            abilityLoopRoutine = null;
        }

        isAbilityActive = false;

        base.Die();
    }

    IEnumerator AbilityLoop()
    {
        while (!isDead)
        {
            float waitTime = Random.Range(abilityMinCooldown, abilityMaxCooldown);
            yield return new WaitForSeconds(waitTime);

            if (isDead)
                yield break;

            if (TryGetComponent<StunReceiver>(out var stun) && stun != null && stun.IsStunned)
                continue;

            if (isAbilityActive)
                continue;

            CacheBombController();

            if (spriteAbility == null)
                continue;

            yield return ExecuteAbility();
        }
    }

    IEnumerator ExecuteAbility()
    {
        isAbilityActive = true;

        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        SnapToGrid();

        float frameTime = Mathf.Max(0.01f, spriteAbility.animationTime > 0f ? spriteAbility.animationTime : 0.25f);

        float invulSeconds =
            FramesDurationSeconds(landStartFrame, landEndFrame, frameTime) +
            FramesDurationSeconds(armedStartFrame, armedEndFrame, frameTime) +
            FramesDurationSeconds(detonateStartFrame, detonateEndFrame, frameTime) +
            FramesDurationSeconds(riseStartFrame, riseEndFrame, frameTime);

        var h = GetComponent<CharacterHealth>();
        if (h != null && invulSeconds > 0f)
            h.StartTemporaryInvulnerability(invulSeconds, withBlink: false);

        DisableAllForAbility();
        EnableAbilitySprite();

        yield return PlayFramesOnce(landStartFrame, landEndFrame, frameTime);

        yield return PlayFramesLoopForSeconds(
            armedStartFrame,
            armedEndFrame,
            frameTime,
            FramesDurationSeconds(armedStartFrame, armedEndFrame, frameTime)
        );

        yield return PlayFramesWithCallback(
            detonateStartFrame,
            detonateEndFrame,
            frameTime,
            onStart: ExplodeLikeBombController
        );

        yield return PlayFramesOnce(riseStartFrame, riseEndFrame, frameTime);

        DisableAbilitySprite();

        isAbilityActive = false;

        UpdateSpriteDirection(direction);
        DecideNextTile();
    }

    float FramesDurationSeconds(int startFrame, int endFrame, float frameTime)
    {
        int count = Mathf.Max(0, (endFrame - startFrame) + 1);
        return count * frameTime;
    }

    IEnumerator PlayFramesOnce(int startFrame, int endFrame, float frameTime)
    {
        if (spriteAbility == null)
            yield break;

        int min = Mathf.Min(startFrame, endFrame);
        int max = Mathf.Max(startFrame, endFrame);

        spriteAbility.loop = false;
        spriteAbility.idle = false;

        for (int f = min; f <= max; f++)
        {
            spriteAbility.CurrentFrame = f;
            spriteAbility.RefreshFrame();
            yield return new WaitForSeconds(frameTime);
        }
    }

    IEnumerator PlayFramesLoopForSeconds(int startFrame, int endFrame, float frameTime, float seconds)
    {
        if (spriteAbility == null)
            yield break;

        if (seconds <= 0f)
            yield break;

        int min = Mathf.Min(startFrame, endFrame);
        int max = Mathf.Max(startFrame, endFrame);

        spriteAbility.loop = true;
        spriteAbility.idle = false;

        float elapsed = 0f;

        while (elapsed < seconds)
        {
            for (int f = min; f <= max && elapsed < seconds; f++)
            {
                spriteAbility.CurrentFrame = f;
                spriteAbility.RefreshFrame();
                yield return new WaitForSeconds(frameTime);
                elapsed += frameTime;
            }
        }
    }

    IEnumerator PlayFramesWithCallback(int startFrame, int endFrame, float frameTime, System.Action onStart)
    {
        onStart?.Invoke();
        yield return PlayFramesOnce(startFrame, endFrame, frameTime);
    }

    void CacheBombController()
    {
        if (cachedBombController != null)
            return;

        if (TryGetComponent(out cachedBombController) && cachedBombController != null)
            return;

        var all = FindObjectsByType<BombController>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            var bc = all[i];
            if (bc == null) continue;
            if (!bc.CompareTag("Player")) continue;
            cachedBombController = bc;
            return;
        }

        cachedBombController = null;
    }

    void ExplodeLikeBombController()
    {
        Vector2 origin = rb != null ? rb.position : (Vector2)transform.position;
        origin.x = Mathf.Round(origin.x);
        origin.y = Mathf.Round(origin.y);

        cachedBombController.SpawnExplosionCrossForEffectWithTileEffects(
            origin,
            explosionRadius,
            pierceExplosion,
            mechAudioSource
        );
    }

    void EnableAbilitySprite()
    {
        if (spriteAbility == null)
            return;

        activeSprite = spriteAbility;
        spriteAbility.enabled = true;
        spriteAbility.idle = false;
    }

    void DisableAbilitySprite()
    {
        if (spriteAbility != null)
            spriteAbility.enabled = false;
    }

    void DisableAllForAbility()
    {
        if (spriteUp != null) spriteUp.enabled = false;
        if (spriteDown != null) spriteDown.enabled = false;
        if (spriteLeft != null) spriteLeft.enabled = false;

        if (spriteDamaged != null) spriteDamaged.enabled = false;

        if (spriteDeath != null && spriteDeath != activeSprite)
            spriteDeath.enabled = false;

        if (spriteAbility != null)
            spriteAbility.enabled = false;
    }

    public bool StartMagnetPull(
    Vector2 directionToMagnet,
    float tileSize,
    int steps,
    LayerMask obstacleMask,
    Tilemap destructibleTilemap)
    {
        if (!CanBeMagnetPulled || steps <= 0 || directionToMagnet == Vector2.zero)
            return false;

        if (magnetRoutine != null)
            StopCoroutine(magnetRoutine);

        Vector2 dir = directionToMagnet.normalized;

        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        SnapToGrid();

        magnetRoutine = StartCoroutine(MagnetPullRoutine(dir, tileSize, steps, obstacleMask, destructibleTilemap));
        return true;
    }

    private IEnumerator MagnetPullRoutine(
        Vector2 dir,
        float tileSize,
        int steps,
        LayerMask obstacleMask,
        Tilemap destructibleTilemap)
    {
        var wait = new WaitForFixedUpdate();

        Vector2 current = rb != null ? rb.position : (Vector2)transform.position;
        current.x = Mathf.Round(current.x / tileSize) * tileSize;
        current.y = Mathf.Round(current.y / tileSize) * tileSize;

        if (rb != null)
            rb.position = current;

        transform.position = current;

        for (int s = 0; s < steps; s++)
        {
            if (isDead)
                break;

            Vector2 next = current + dir * tileSize;

            if (IsMagnetMoveBlocked(next, tileSize, obstacleMask, destructibleTilemap))
                break;

            float speed = Mathf.Max(0.0001f, magnetPullSpeed);
            float travelTime = tileSize / speed;

            float elapsed = 0f;
            Vector2 start = current;

            while (elapsed < travelTime)
            {
                if (isDead)
                    goto END;

                elapsed += Time.fixedDeltaTime;

                float a = Mathf.Clamp01(elapsed / travelTime);
                Vector2 pos = Vector2.Lerp(start, next, a);

                if (rb != null)
                    rb.MovePosition(pos);

                transform.position = pos;

                yield return wait;
            }

            current = next;

            if (rb != null)
                rb.position = current;

            transform.position = current;
        }

    END:
        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        magnetRoutine = null;
    }

    private bool IsMagnetMoveBlocked(
        Vector2 target,
        float tileSize,
        LayerMask obstacleMask,
        Tilemap destructibleTilemap)
    {
        Vector2 size = Vector2.one * (tileSize * 0.6f);

        Collider2D[] hits = Physics2D.OverlapBoxAll(target, size, 0f, obstacleMask);

        for (int i = 0; i < hits.Length; i++)
        {
            var h = hits[i];
            if (h == null)
                continue;

            if (h.attachedRigidbody != null && h.attachedRigidbody.gameObject == gameObject)
                continue;

            if (h.gameObject == gameObject)
                continue;

            if (h.isTrigger)
                continue;

            return true;
        }

        if (destructibleTilemap != null)
        {
            Vector3Int cell = destructibleTilemap.WorldToCell(target);
            if (destructibleTilemap.GetTile(cell) != null)
                return true;
        }

        return false;
    }
}

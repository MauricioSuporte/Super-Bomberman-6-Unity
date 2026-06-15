using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class SkullDebuffController : MonoBehaviour
{
    const float DefaultDurationSeconds = 10f;
    const float TransferCooldownSeconds = 1f;
    const float ContactCheckRadius = 0.65f;
    const float LogThrottleSeconds = 0.35f;
    const int ExpelDistanceTiles = 3;
    const string TransferSfxResourcesPath = "Sounds/infected";
    static readonly bool DebugSkullTransfer = false;

    static AudioClip transferSfx;

    enum SkullDebuffType
    {
        SlowSpeed,
        FastSpeed,
        FastBombFuse,
        SlowBombFuse,
        ShortExplosion,
        NoBombs
    }

    MovementController movement;
    BombController bombController;
    Coroutine activeRoutine;
    SkullDebuffType activeEffect;
    bool hasActiveEffect;
    float activeEffectEndsAt;
    float nextTransferAt;
    float nextSkippedLogAt;

    public void ApplyRandom(float durationSeconds = DefaultDurationSeconds)
    {
        CacheReferences();

        float duration = Mathf.Max(0.01f, durationSeconds);
        var effect = (SkullDebuffType)UnityEngine.Random.Range(0, 6);

        ApplyEffect(effect, duration, startTransferCooldown: false);
    }

    void ApplyEffect(SkullDebuffType effect, float durationSeconds, bool startTransferCooldown)
    {
        CacheReferences();
        ClearActiveEffect();

        float duration = Mathf.Max(0.01f, durationSeconds);
        activeEffect = effect;
        hasActiveEffect = true;
        activeEffectEndsAt = Time.time + duration;
        nextTransferAt = startTransferCooldown
            ? Time.time + TransferCooldownSeconds
            : 0f;

        LogTransfer($"apply owner:{GetOwnerName()} effect:{effect} duration:{duration:F2}s");

        switch (effect)
        {
            case SkullDebuffType.SlowSpeed:
                movement?.ApplyTemporarySpeedOverride(PlayerPersistentStats.MinSpeedInternal / 2, duration);
                break;

            case SkullDebuffType.FastSpeed:
                movement?.ApplyTemporarySpeedOverride(PlayerPersistentStats.MaxSpeedInternal * 2, duration);
                break;

            case SkullDebuffType.FastBombFuse:
                movement?.ApplyTemporarySkullVisual(duration);
                bombController?.ApplyTemporarySkullBombFuseMultiplier(0.5f, duration);
                break;

            case SkullDebuffType.SlowBombFuse:
                movement?.ApplyTemporarySkullVisual(duration);
                bombController?.ApplyTemporarySkullBombFuseMultiplier(2f, duration);
                break;

            case SkullDebuffType.ShortExplosion:
                movement?.ApplyTemporarySkullVisual(duration);
                bombController?.ApplyTemporarySkullExplosionRadiusOverride(1, duration);
                break;

            case SkullDebuffType.NoBombs:
                movement?.ApplyTemporarySkullVisual(duration);
                bombController?.ApplyTemporarySkullBombPlacementBlock(duration);
                break;
        }

        activeRoutine = StartCoroutine(ClearAfter(duration));
    }

    void FixedUpdate()
    {
        TryTransferByOverlap();
    }

    void TryTransferByOverlap()
    {
        if (!HasTransferableEffect())
            return;

        CacheReferences();

        Vector2 center = movement != null
            ? (Vector2)movement.transform.position
            : (Vector2)transform.position;

        int playerMask = LayerMask.GetMask("Player");
        if (playerMask == 0)
        {
            LogTransferSkipped("overlap", "player-layer-mask-zero");
            return;
        }

        Collider2D[] hits = Physics2D.OverlapCircleAll(center, ContactCheckRadius, playerMask);
        if (hits == null || hits.Length == 0)
        {
            LogTransferSkipped("overlap", $"no-player-collider center:{FormatVec(center)} radius:{ContactCheckRadius:F2}");
            return;
        }

        for (int i = 0; i < hits.Length; i++)
        {
            var hit = hits[i];

            if (hit == null)
                continue;

            if (TryTransferTo(hit, "overlap"))
                return;
        }
    }

    bool HasTransferableEffect()
    {
        return hasActiveEffect && activeRoutine != null && GetRemainingDuration() > 0.01f;
    }

    float GetRemainingDuration()
    {
        if (!hasActiveEffect)
            return 0f;

        return Mathf.Max(0f, activeEffectEndsAt - Time.time);
    }

    public bool TryExpelActiveSkull(Vector2 origin)
    {
        if (!HasTransferableEffect())
            return false;

        CacheReferences();

        ItemPickup skullPrefab = AutoItemDatabase.Get(ItemType.Skull);
        if (skullPrefab == null)
        {
            LogTransfer($"expel failed owner:{GetOwnerName()} reason:skull-prefab-missing");
            return false;
        }

        float tileSize = movement != null
            ? Mathf.Max(0.0001f, movement.tileSize)
            : 1f;

        Vector2 direction = RandomCardinalDirection();
        var spawnedSkull = Instantiate(skullPrefab, origin, Quaternion.identity);
        if (spawnedSkull == null)
        {
            LogTransfer($"expel failed owner:{GetOwnerName()} reason:instantiate-null");
            return false;
        }

        Collider2D ignoredCollider = GetComponent<Collider2D>();
        if (ignoredCollider == null)
            ignoredCollider = GetComponentInChildren<Collider2D>();

        ClearActiveEffect();
        spawnedSkull.TryExpelSkull(direction, tileSize, ignoredCollider, ExpelDistanceTiles);

        LogTransfer(
            $"expel owner:{GetOwnerName()} spawned:{spawnedSkull.name} " +
            $"origin:{FormatVec(origin)} dir:{FormatVec(direction)} steps:{ExpelDistanceTiles}");

        return true;
    }

    void CacheReferences()
    {
        if (movement == null)
            TryGetComponent(out movement);

        if (bombController == null)
            TryGetComponent(out bombController);
    }

    void ClearActiveEffect()
    {
        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
            activeRoutine = null;
        }

        movement?.ClearTemporarySpeedOverride();
        movement?.ClearTemporarySkullVisual();
        bombController?.ClearTemporarySkullBombModifiers();

        hasActiveEffect = false;
        activeEffectEndsAt = 0f;
        nextTransferAt = 0f;
    }

    public void ClearForArenaRemoval()
    {
        CacheReferences();
        ClearActiveEffect();
    }

    IEnumerator ClearAfter(float durationSeconds)
    {
        yield return new WaitForSeconds(durationSeconds);

        activeRoutine = null;
        movement?.ClearTemporarySpeedOverride();
        movement?.ClearTemporarySkullVisual();
        bombController?.ClearTemporarySkullBombModifiers();
        hasActiveEffect = false;
        activeEffectEndsAt = 0f;
        nextTransferAt = 0f;

        LogTransfer($"expired owner:{GetOwnerName()}");
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        TryTransferTo(other, "trigger-enter");
    }

    void OnTriggerStay2D(Collider2D other)
    {
        TryTransferTo(other, "trigger-stay");
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision == null)
            return;

        TryTransferTo(collision.collider, "collision-enter");
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        if (collision == null)
            return;

        TryTransferTo(collision.collider, "collision-stay");
    }

    bool TryTransferTo(Collider2D other, string source)
    {
        if (!HasTransferableEffect())
            return false;

        if (Time.time < nextTransferAt)
        {
            LogTransferSkipped(source, $"cooldown remaining:{nextTransferAt - Time.time:F2}s");
            return false;
        }

        if (other == null)
        {
            LogTransferSkipped(source, "other-null");
            return false;
        }

        var targetMovement = other.GetComponentInParent<MovementController>();
        if (targetMovement == null)
        {
            LogTransferSkipped(source, $"no-movement other:{other.name}");
            return false;
        }

        CacheReferences();
        if (targetMovement == movement)
        {
            LogTransferSkipped(source, $"self-contact collider:{other.name}");
            return false;
        }

        if (!targetMovement.CompareTag("Player"))
        {
            LogTransferSkipped(source, $"target-not-player target:{targetMovement.name} tag:{targetMovement.tag}");
            return false;
        }

        var target = targetMovement.gameObject;
        if (target == null || target == gameObject)
            return false;

        if (target.TryGetComponent<SkullDebuffController>(out var targetSkull) &&
            targetSkull != null &&
            targetSkull.HasTransferableEffect())
        {
            LogTransferSkipped(source, $"target-already-infected target:{target.name}");
            return false;
        }

        if (targetSkull == null)
            targetSkull = target.AddComponent<SkullDebuffController>();

        float remaining = GetRemainingDuration();
        SkullDebuffType effect = activeEffect;

        targetSkull.ApplyEffect(effect, remaining, startTransferCooldown: true);
        ClearActiveEffect();
        PlayTransferSfx(target);

        LogTransfer(
            $"transfer source:{source} from:{GetOwnerName()} to:{target.name} " +
            $"effect:{effect} remaining:{remaining:F2}s sfx:{(transferSfx != null)}");

        return true;
    }

    static void PlayTransferSfx(GameObject target)
    {
        if (target == null)
            return;

        if (transferSfx == null)
            transferSfx = Resources.Load<AudioClip>(TransferSfxResourcesPath);

        if (transferSfx == null)
            return;

        var audio = target.GetComponent<AudioSource>();
        if (audio != null)
            audio.PlayOneShot(transferSfx, 1f);
        else
            AudioSource.PlayClipAtPoint(transferSfx, target.transform.position, 1f);
    }

    static Vector2 RandomCardinalDirection()
    {
        int r = Random.Range(0, 4);
        return r switch
        {
            0 => Vector2.up,
            1 => Vector2.down,
            2 => Vector2.left,
            _ => Vector2.right
        };
    }

    void OnDisable()
    {
        ClearActiveEffect();
    }

    void LogTransfer(string message)
    {
        if (!DebugSkullTransfer)
            return;

        Debug.Log($"[SkullTransfer] {message}", this);
    }

    void LogTransferSkipped(string source, string reason)
    {
        if (!DebugSkullTransfer || Time.time < nextSkippedLogAt)
            return;

        nextSkippedLogAt = Time.time + LogThrottleSeconds;
        Debug.Log($"[SkullTransfer] skip owner:{GetOwnerName()} source:{source} reason:{reason}", this);
    }

    string GetOwnerName()
    {
        return gameObject != null ? gameObject.name : "<null>";
    }

    static string FormatVec(Vector2 value)
    {
        return $"({value.x:F2},{value.y:F2})";
    }
}

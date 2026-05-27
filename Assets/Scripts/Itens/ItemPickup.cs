using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(AnimatedSpriteRenderer))]
public class ItemPickup : MonoBehaviour
{
    [Header("SFX")]
    public AudioClip collectSfx;

    [Range(0f, 1f)]
    public float collectVolume = 1f;

    [Header("Player Extra SFX (optional)")]
    [SerializeField] private AudioClip playerExtraSfx;

    [Range(0f, 1f)]
    [SerializeField] private float playerExtraVolume = 1f;

    [SerializeField] private ItemType[] playPlayerExtraSfxForTypes;

    public AnimatedSpriteRenderer idleRenderer;
    public AnimatedSpriteRenderer destroyRenderer;

    [Header("Explosion Destroy Animation")]
    [SerializeField] private AnimatedSpriteRenderer explosionDestroyRenderer;

    [Header("Destroy Animation")]
    [SerializeField, Range(0.05f, 3f)]
    private float destroyDelaySeconds = 0.5f;

    [Header("Spawn Immunity")]
    public float spawnImmunitySeconds = 0.5f;

    [Header("Damage Pickup")]
    [SerializeField] private bool damageIgnoresInvulnerability = false;

    [Header("Optional Behavior (overrides default switch for non-eggs)")]
    [SerializeField] private MonoBehaviour behavior;

    public ItemType type;

    bool isBeingDestroyed;
    float spawnTime;
    Collider2D _col;

    IItemPickupBehavior _behavior;

    const float SkullBounceSecondsPerTile = 0.12f;
    const float SkullBounceFlipIntervalSeconds = 0.04f;
    const float SkullBounceDefaultProtectionSeconds = 0.20f;
    const float SkullBounceExplosionProtectionSeconds = 1.05f;
    const float SkullBouncePositionLockSeconds = 0.75f;
    const float SkullPickupCenterDistance = 0.75f;
    const float SkullPickupClosestDistance = 0.15f;
    const float SkullBouncePositionDriftEpsilon = 0.01f;
    const int SkullBounceFallbackMaxSteps = 80;
    static AudioSource playerExtraSfxSource;
    int[] skullBounceOriginalSortingOrders;
    static readonly Collider2D[] SkullBounceOverlapBuffer = new Collider2D[24];

    [Header("Debug Skull Bounce")]
    [SerializeField] private bool debugSkullBounce;

    Coroutine skullBounceRoutine;
    bool skullBounceMoving;
    bool skullKickedBombPushActive;
    float skullBounceSuppressUntil;
    bool skullBouncePositionLocked;
    Vector2 skullBounceLockedWorldPosition;
    float skullBouncePositionLockUntil;
    SpriteRenderer[] skullBounceRenderers;
    bool[] skullBounceOriginalFlipX;
    bool[] skullBounceOriginalFlipY;
    Tilemap skullGroundTilemap;
    Tilemap skullStageBoundsTilemap;
    Tilemap skullIndestructibleTilemap;
    Tilemap skullDestructibleTilemap;
    Tilemap skullWaterTilemap;
    Tilemap skullHoleTilemap;
    bool skullTilemapsResolved;
    bool skullStageBoundsReady;
    BoundsInt skullStageCellBounds;

    void Reset()
    {
        ApplyDefaultsEditor();
    }

    void OnValidate()
    {
        ApplyDefaultsEditor();
    }

    void Awake()
    {
        ApplyDefaultsRuntime();

        spawnTime = Time.time;
        _col = GetComponent<Collider2D>();
        _behavior = behavior as IItemPickupBehavior;
    }

    void LateUpdate()
    {
        EnforceSkullBouncePositionLock();
    }

    void ApplyDefaultsEditor()
    {
        TrySetTypeFromGameObjectName();

        if (collectSfx == null)
            collectSfx = LoadDefaultCollectSfx(type);

        if (idleRenderer == null)
            idleRenderer = GetComponent<AnimatedSpriteRenderer>();

        if (destroyRenderer == null)
        {
            var t = transform.Find("DestroyAnimation");
            if (t != null)
                destroyRenderer = t.GetComponent<AnimatedSpriteRenderer>();
        }

        if (explosionDestroyRenderer == null)
        {
            var t = transform.Find("ExplosionDestroyAnimation");
            if (t != null)
                explosionDestroyRenderer = t.GetComponent<AnimatedSpriteRenderer>();
        }
    }

    void ApplyDefaultsRuntime()
    {
        TrySetTypeFromGameObjectName();

        if (collectSfx == null)
            collectSfx = LoadDefaultCollectSfx(type);

        if (idleRenderer == null)
            idleRenderer = GetComponent<AnimatedSpriteRenderer>();

        if (destroyRenderer == null)
        {
            var t = transform.Find("DestroyAnimation");
            if (t != null)
                destroyRenderer = t.GetComponent<AnimatedSpriteRenderer>();
        }

        if (explosionDestroyRenderer == null)
        {
            var t = transform.Find("ExplosionDestroyAnimation");
            if (t != null)
                explosionDestroyRenderer = t.GetComponent<AnimatedSpriteRenderer>();
        }
    }

    static AudioClip LoadDefaultCollectSfx(ItemType t)
    {
        if (t == ItemType.Skull)
            return Resources.Load<AudioClip>("Sounds/skull collect");

        if (IsLouieEggStatic(t))
        {
            string eggPath = GetEggSfxResourcesPath(t);
            if (!string.IsNullOrEmpty(eggPath))
            {
                var eggClip = Resources.Load<AudioClip>(eggPath);
                if (eggClip != null)
                    return eggClip;
            }
        }

        return Resources.Load<AudioClip>("Sounds/ItemCollect");
    }

    static bool IsLouieEggStatic(ItemType t)
    {
        return t == ItemType.BlueLouieEgg
            || t == ItemType.BlackLouieEgg
            || t == ItemType.PurpleLouieEgg
            || t == ItemType.GreenLouieEgg
            || t == ItemType.YellowLouieEgg
            || t == ItemType.PinkLouieEgg
            || t == ItemType.RedLouieEgg;
    }

    static string GetEggSfxResourcesPath(ItemType t)
    {
        switch (t)
        {
            case ItemType.BlueLouieEgg: return "Sounds/MountBlueLouie";
            case ItemType.BlackLouieEgg: return "Sounds/MountBlackLouie";
            case ItemType.PurpleLouieEgg: return "Sounds/MountPurpleLouie";
            case ItemType.GreenLouieEgg: return "Sounds/MountGreenLouie";
            case ItemType.YellowLouieEgg: return "Sounds/MountYellowLouie";
            case ItemType.PinkLouieEgg: return "Sounds/MountPinkLouie";
            case ItemType.RedLouieEgg: return "Sounds/MountRedLouie";
            default: return null;
        }
    }

    void TrySetTypeFromGameObjectName()
    {
        string n = gameObject != null ? gameObject.name : null;
        if (string.IsNullOrWhiteSpace(n))
            return;

        n = CleanItemName(n);

        if (Enum.TryParse(n, ignoreCase: true, out ItemType parsed))
            type = parsed;
    }

    static string CleanItemName(string n)
    {
        n = n.Trim();

        const string cloneSuffix = "(Clone)";
        if (n.EndsWith(cloneSuffix, StringComparison.OrdinalIgnoreCase))
            n = n.Substring(0, n.Length - cloneSuffix.Length).Trim();

        return n;
    }

    bool IsSpawnImmune() => Time.time - spawnTime < spawnImmunitySeconds;

    bool IsLouieEgg(ItemType t) => IsLouieEggStatic(t);

    bool PlayerAlreadyMounted(GameObject player)
    {
        if (player.TryGetComponent<MovementController>(out var movement) && movement.IsMounted)
            return true;

        if (player.TryGetComponent<PlayerMountCompanion>(out var louieCompanion) && louieCompanion != null)
            return louieCompanion.GetMountedLouieType() != MountedType.None;

        return false;
    }

    bool PlayerHoldingBombWithPowerGlove(GameObject player)
    {
        if (player == null)
            return false;

        if (!player.TryGetComponent<AbilitySystem>(out var ab) || ab == null)
            return false;

        ab.RebuildCache();

        var glove = ab.Get<PowerGloveAbility>(PowerGloveAbility.AbilityId);
        return glove != null && glove.IsEnabled && glove.IsHoldingBomb;
    }

    AbilitySystem GetOrCreateAbilitySystem(GameObject player)
    {
        if (!player.TryGetComponent<AbilitySystem>(out var abilitySystem))
            abilitySystem = player.AddComponent<AbilitySystem>();

        abilitySystem.RebuildCache();
        return abilitySystem;
    }

    Sprite GetEggIdleSpriteFallback()
    {
        if (idleRenderer != null && idleRenderer.idleSprite != null)
            return idleRenderer.idleSprite;

        var sr = GetComponent<SpriteRenderer>();
        return sr != null ? sr.sprite : null;
    }

    MountEggQueue GetOrCreateEggQueue(GameObject player)
    {
        if (!player.TryGetComponent<MountEggQueue>(out var q) || q == null)
            q = player.AddComponent<MountEggQueue>();

        q.BindOwner(player.GetComponent<MovementController>());
        return q;
    }

    void PlayCollectSfxOnPlayer(GameObject player)
    {
        if (collectSfx == null)
            return;

        var audio = player.GetComponent<AudioSource>();
        if (audio != null)
            audio.PlayOneShot(collectSfx, Mathf.Clamp01(collectVolume));
    }

    bool ShouldPlayPlayerExtraSfx()
    {
        if (playerExtraSfx == null)
            return false;

        if (playPlayerExtraSfxForTypes == null || playPlayerExtraSfxForTypes.Length == 0)
            return false;

        for (int i = 0; i < playPlayerExtraSfxForTypes.Length; i++)
            if (playPlayerExtraSfxForTypes[i] == type)
                return true;

        return false;
    }

    void PlayPlayerExtraSfx(GameObject player)
    {
        if (!ShouldPlayPlayerExtraSfx())
            return;

        var playerAudio = player.GetComponent<AudioSource>();
        if (playerAudio == null)
            return;

        var audio = GetOrCreatePlayerExtraSfxSource();
        audio.transform.position = playerAudio.transform.position;
        audio.outputAudioMixerGroup = playerAudio.outputAudioMixerGroup;
        audio.spatialBlend = playerAudio.spatialBlend;
        audio.minDistance = playerAudio.minDistance;
        audio.maxDistance = playerAudio.maxDistance;
        audio.rolloffMode = playerAudio.rolloffMode;
        audio.priority = playerAudio.priority;
        audio.pitch = playerAudio.pitch;
        audio.Stop();
        audio.clip = playerExtraSfx;
        audio.volume = Mathf.Clamp01(playerAudio.volume * playerExtraVolume);
        audio.Play();
    }

    static AudioSource GetOrCreatePlayerExtraSfxSource()
    {
        if (playerExtraSfxSource != null)
            return playerExtraSfxSource;

        var go = new GameObject("ItemPickupPlayerExtraSfx");
        DontDestroyOnLoad(go);
        playerExtraSfxSource = go.AddComponent<AudioSource>();
        playerExtraSfxSource.playOnAwake = false;
        playerExtraSfxSource.loop = false;
        return playerExtraSfxSource;
    }

    bool TrySetMountSfxForImmediateMount(GameObject player)
    {
        if (collectSfx == null)
            return false;

        if (!player.TryGetComponent<PlayerMountCompanion>(out var companion) || companion == null)
            return false;

        companion.SetNextMountSfx(collectSfx, collectVolume);
        return true;
    }

    Vector2 ResolveMountFacing(GameObject player)
    {
        Vector2 face = Vector2.down;

        if (player != null && player.TryGetComponent<MovementController>(out var mv) && mv != null)
        {
            if (mv.Direction != Vector2.zero)
                face = mv.Direction;
            else if (mv.FacingDirection != Vector2.zero)
                face = mv.FacingDirection;
        }

        if (Mathf.Abs(face.x) >= Mathf.Abs(face.y))
            return face.x >= 0f ? Vector2.right : Vector2.left;

        return face.y >= 0f ? Vector2.up : Vector2.down;
    }

    void ConsumeNow(bool playDestroyAnim)
    {
        ClearSkullBouncePositionLock();

        if (playDestroyAnim)
            DestroyWithAnimation();
        else
            Destroy(gameObject);
    }

    public void Consume(bool playDestroyAnim) => ConsumeNow(playDestroyAnim);

    public bool TryApplyDamageLikeEnemyContact(GameObject player, int damage)
        => TryApplyPickupDamageLikeEnemyContact(player, damage, fromExplosion: false);

    public bool TryApplyDamageLikeEnemyContact(GameObject player, int damage, bool fromExplosion)
        => TryApplyPickupDamageLikeEnemyContact(player, damage, fromExplosion);

    bool TryApplyPickupDamageLikeEnemyContact(GameObject player, int damage, bool fromExplosion)
    {
        if (player == null || damage <= 0)
            return false;

        player.TryGetComponent<MovementController>(out var mv);
        player.TryGetComponent<CharacterHealth>(out var health);

        if (!damageIgnoresInvulnerability && health != null && health.IsInvulnerable)
            return false;

        if (mv != null && mv.CompareTag("Player"))
        {
            if (mv.IsRidingPlaying())
            {
                if (player.TryGetComponent<PlayerMountCompanion>(out var companion) && companion != null)
                {
                    companion.HandleDamageWhileMounting(damage);
                    return true;
                }
            }

            if (mv.IsMounted)
            {
                if (player.TryGetComponent<PlayerMountCompanion>(out var companion) && companion != null)
                {
                    companion.OnMountedLouieHit(damage, fromExplosion);
                    return true;
                }
            }
        }

        if (health != null)
        {
            health.TakeDamage(damage, fromExplosion);
            return true;
        }

        if (mv != null)
        {
            if (fromExplosion)
                mv.KillByExplosion();
            else
                mv.Kill();

            return true;
        }

        return false;
    }

    void OnItemPickup(GameObject player)
    {
        PlayerPersistentStats.NotifyStageItemPickupCollected();

        bool isEgg = IsLouieEgg(type);
        Vector2 mountFacing = ResolveMountFacing(player);

        if (isEgg && PlayerHoldingBombWithPowerGlove(player))
            return;

        if (isEgg && PlayerAlreadyMounted(player))
        {
            var q = GetOrCreateEggQueue(player);

            if (!q.TryEnqueue(type, GetEggIdleSpriteFallback(), collectSfx, collectVolume))
                return;

            ConsumeNow(false);
            return;
        }

        if (isEgg)
        {
            if (TryMountEggWithArc(player, mountFacing))
            {
                DestroyWithAnimation();
                return;
            }

            return;
        }

        if (type != ItemType.Skull &&
            player.TryGetComponent<SkullDebuffController>(out var activeSkullDebuff) &&
            activeSkullDebuff != null)
        {
            activeSkullDebuff.TryExpelActiveSkull(transform.position);
        }

        PlayCollectSfxOnPlayer(player);
        PlayPlayerExtraSfx(player);

        if (_behavior != null)
        {
            if (_behavior.OnPickedUp(this, player))
                return;
        }

        int pid = 1;
        if (player.TryGetComponent<PlayerIdentity>(out var id) && id != null)
            pid = Mathf.Clamp(id.playerId, 1, 6);

        if (type != ItemType.LandMine && type != ItemType.Clock && type != ItemType.Skull && type != ItemType.OneUp)
            PlayerPersistentStats.StageApplyPickup(pid, type);

        switch (type)
        {
            case ItemType.Skull:
                if (!player.TryGetComponent<SkullDebuffController>(out var skullDebuff) || skullDebuff == null)
                    skullDebuff = player.AddComponent<SkullDebuffController>();

                skullDebuff.ApplyRandom();
                break;

            case ItemType.Clock:
                ClockStageStunEffect.Trigger(5f);
                break;

            case ItemType.LandMine:
                TryApplyPickupDamageLikeEnemyContact(player, 1, true);
                break;

            case ItemType.ExtraBomb:
                if (player.TryGetComponent<BombController>(out var bombController))
                    bombController.AddBomb();
                break;

            case ItemType.BlastRadius:
                {
                    var bc = player.GetComponent<BombController>();
                    if (bc != null && bc.explosionRadius < PlayerPersistentStats.MaxExplosionRadius)
                        bc.explosionRadius = Mathf.Min(bc.explosionRadius + 1, PlayerPersistentStats.MaxExplosionRadius);
                    break;
                }

            case ItemType.SpeedIncrese:
                {
                    if (player.TryGetComponent<MovementController>(out var mv))
                        mv.TryAddSpeedUp(PlayerPersistentStats.SpeedStep);
                    break;
                }

            case ItemType.BombKick:
                GetOrCreateAbilitySystem(player).Enable(BombKickAbility.AbilityId);
                break;

            case ItemType.BombPunch:
                GetOrCreateAbilitySystem(player).Enable(BombPunchAbility.AbilityId);
                break;

            case ItemType.PowerGlove:
                GetOrCreateAbilitySystem(player).Enable(PowerGloveAbility.AbilityId);
                break;

            case ItemType.PierceBomb:
                {
                    var ab = GetOrCreateAbilitySystem(player);
                    ab.Enable(PierceBombAbility.AbilityId);
                    ab.Disable(ControlBombAbility.AbilityId);
                    ab.Disable(PowerBombAbility.AbilityId);
                    ab.Disable(RubberBombAbility.AbilityId);
                    ab.Disable(MagnetBombAbility.AbilityId);
                    break;
                }

            case ItemType.ControlBomb:
                {
                    var ab = GetOrCreateAbilitySystem(player);
                    ab.Enable(ControlBombAbility.AbilityId);
                    ab.Disable(PierceBombAbility.AbilityId);
                    ab.Disable(PowerBombAbility.AbilityId);
                    ab.Disable(RubberBombAbility.AbilityId);
                    ab.Disable(MagnetBombAbility.AbilityId);
                    break;
                }

            case ItemType.PowerBomb:
                {
                    var ab = GetOrCreateAbilitySystem(player);
                    ab.Enable(PowerBombAbility.AbilityId);
                    ab.Disable(PierceBombAbility.AbilityId);
                    ab.Disable(ControlBombAbility.AbilityId);
                    ab.Disable(RubberBombAbility.AbilityId);
                    ab.Disable(MagnetBombAbility.AbilityId);
                    break;
                }

            case ItemType.RubberBomb:
                {
                    var ab = GetOrCreateAbilitySystem(player);
                    ab.Enable(RubberBombAbility.AbilityId);
                    ab.Disable(PierceBombAbility.AbilityId);
                    ab.Disable(ControlBombAbility.AbilityId);
                    ab.Disable(PowerBombAbility.AbilityId);
                    ab.Disable(MagnetBombAbility.AbilityId);
                    break;
                }

            case ItemType.MagnetBomb:
                {
                    var ab = GetOrCreateAbilitySystem(player);
                    ab.Enable(MagnetBombAbility.AbilityId);
                    ab.Disable(PierceBombAbility.AbilityId);
                    ab.Disable(ControlBombAbility.AbilityId);
                    ab.Disable(PowerBombAbility.AbilityId);
                    ab.Disable(RubberBombAbility.AbilityId);
                    break;
                }

            case ItemType.FullFire:
                GetOrCreateAbilitySystem(player).Enable(FullFireAbility.AbilityId);
                break;

            case ItemType.BombPass:
                GetOrCreateAbilitySystem(player).Enable(BombPassAbility.AbilityId);
                break;

            case ItemType.DestructiblePass:
                GetOrCreateAbilitySystem(player).Enable(DestructiblePassAbility.AbilityId);
                break;

            case ItemType.InvincibleSuit:
                GetOrCreateAbilitySystem(player).Enable(InvincibleSuitAbility.AbilityId);
                break;

            case ItemType.Heart:
                if (player.TryGetComponent<CharacterHealth>(out var health))
                    health.AddLife(1);
                break;

            case ItemType.OneUp:
                GameSession.Instance?.TryAddHardNormalGameLife(out _);
                break;
        }

        bool playDestroyAnim = type == ItemType.LandMine;
        ConsumeNow(playDestroyAnim);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (isBeingDestroyed || other == null)
            return;

        if (other.CompareTag("Player"))
        {
            if (type == ItemType.Skull)
            {
                bool actuallyTouching = IsColliderActuallyTouchingCurrentItem(
                    other,
                    out var itemCenter,
                    out var playerCenter,
                    out float centerDistance,
                    out float closestDistance);

                if (!actuallyTouching)
                {
                    LogSkullBounce(
                        $"ignored stale pickup item:{GetDebugIdentity()} " +
                        $"logical:{FormatVec(itemCenter)} itemVisual:{FormatVec(transform.position)} " +
                        $"itemPhysics:{FormatVec(GetSkullPhysicsPosition())} " +
                        $"itemColliderBounds:{FormatBounds(_col != null ? _col.bounds : default)} " +
                        $"player:{other.name} playerPos:{FormatVec(other.transform.position)} playerCenter:{FormatVec(playerCenter)} " +
                        $"centerDistance:{centerDistance:F2} closestDistance:{closestDistance:F2} " +
                        $"lockActive:{IsSkullBouncePositionLockActive()} lockRemaining:{GetSkullBouncePositionLockRemaining():F2}s " +
                        $"playerBounds:{FormatBounds(other.bounds)}");
                    return;
                }

                LogSkullBounce(
                    $"accepted pickup item:{GetDebugIdentity()} " +
                    $"logical:{FormatVec(itemCenter)} itemVisual:{FormatVec(transform.position)} " +
                    $"itemPhysics:{FormatVec(GetSkullPhysicsPosition())} " +
                    $"player:{other.name} playerPos:{FormatVec(other.transform.position)} playerCenter:{FormatVec(playerCenter)} " +
                    $"centerDistance:{centerDistance:F2} closestDistance:{closestDistance:F2} " +
                    $"lockActive:{IsSkullBouncePositionLockActive()} lockRemaining:{GetSkullBouncePositionLockRemaining():F2}s");
            }

            var player = other.gameObject;

            if (IsLouieEgg(type))
            {
                if (PlayerHoldingBombWithPowerGlove(player))
                    return;
            }

            if (type == ItemType.Skull)
                ClearSkullBouncePositionLock();

            OnItemPickup(player);
            return;
        }

        if (other.CompareTag("Explosion"))
        {
            if (IsSpawnImmune())
                return;

            if (TryBounceSkullFromExplosion(other))
                return;

            DestroyWithExplosionAnimation();
        }
    }

    public bool TryBounceSkull(Vector2 direction, float tileSize = 1f)
    {
        return TryBounceSkull(direction, tileSize, null, SkullBounceDefaultProtectionSeconds);
    }

    public bool TryBounceSkull(Vector2 direction, float tileSize, Collider2D ignoredCollider)
    {
        return TryBounceSkull(direction, tileSize, ignoredCollider, SkullBounceDefaultProtectionSeconds);
    }

    public bool TryBounceSkull(Vector2 direction, float tileSize, Collider2D ignoredCollider, float protectionSeconds)
    {
        if (type != ItemType.Skull)
            return false;

        Vector2 requestedDirection = direction;
        Vector2 origin = GetSkullLogicalPosition();
        string source = ignoredCollider != null
            ? $"{ignoredCollider.name}#{ignoredCollider.GetEntityId()}/layer:{LayerMask.LayerToName(ignoredCollider.gameObject.layer)}"
            : "direct-call";

        if (Time.time < skullBounceSuppressUntil)
        {
            LogSkullBounce(
                $"suppressed item:{GetDebugIdentity()} origin:{FormatVec(origin)} requestedDir:{FormatVec(requestedDirection)} " +
                $"normalizedDir:{FormatVec(NormalizeCardinalOrDown(direction))} source:{source} " +
                $"until:{skullBounceSuppressUntil:F2}");
            return true;
        }

        if (isBeingDestroyed || skullBounceMoving)
        {
            LogSkullBounce(
                $"ignored item:{GetDebugIdentity()} origin:{FormatVec(origin)} requestedDir:{FormatVec(requestedDirection)} " +
                $"source:{source} isBeingDestroyed:{isBeingDestroyed} moving:{skullBounceMoving}");
            return true;
        }

        direction = NormalizeCardinalOrDown(direction);
        tileSize = Mathf.Max(0.0001f, tileSize);
        skullBounceSuppressUntil = Mathf.Max(
            skullBounceSuppressUntil,
            Time.time + Mathf.Max(0f, protectionSeconds));

        LogSkullBounce(
            $"start item:{GetDebugIdentity()} origin:{FormatVec(origin)} requestedDir:{FormatVec(requestedDirection)} " +
            $"normalizedDir:{FormatVec(direction)} tileSize:{tileSize:F2} source:{source} " +
            $"protection:{Mathf.Max(0f, protectionSeconds):F2}s");

        if (skullBounceRoutine != null)
            StopCoroutine(skullBounceRoutine);

        skullBounceRoutine = StartCoroutine(SkullBounceRoutine(direction, tileSize, ignoredCollider, origin, requestedDirection, source));
        return true;
    }

    public bool TryExpelSkull(Vector2 direction, float tileSize, Collider2D ignoredCollider, int distanceTiles)
    {
        if (type != ItemType.Skull)
            return false;

        return TryExpelItem(direction, tileSize, ignoredCollider, distanceTiles);
    }

    public bool TryExpelItem(Vector2 direction, float tileSize, Collider2D ignoredCollider, int distanceTiles)
    {
        if (isBeingDestroyed || skullBounceMoving)
            return true;

        direction = NormalizeCardinalOrDown(direction);
        tileSize = Mathf.Max(0.0001f, tileSize);
        int steps = Mathf.Max(1, distanceTiles);
        Vector2 origin = GetSkullLogicalPosition();

        skullBounceSuppressUntil = Mathf.Max(
            skullBounceSuppressUntil,
            Time.time + SkullBounceDefaultProtectionSeconds);

        LogSkullBounce(
            $"expel start item:{GetDebugIdentity()} origin:{FormatVec(origin)} " +
            $"dir:{FormatVec(direction)} tileSize:{tileSize:F2} steps:{steps}");

        if (skullBounceRoutine != null)
            StopCoroutine(skullBounceRoutine);

        skullBounceRoutine = StartCoroutine(SkullExpelRoutine(direction, tileSize, ignoredCollider, origin, steps));
        return true;
    }

    bool TryBounceSkullFromExplosion(Collider2D explosionCollider)
    {
        if (type != ItemType.Skull)
            return false;

        Vector2 direction = Vector2.zero;

        var explosion = explosionCollider != null
            ? explosionCollider.GetComponentInParent<BombExplosion>()
            : null;

        if (explosion != null)
            direction = (Vector2)transform.position - explosion.Origin;

        if (direction.sqrMagnitude <= 0.0001f && explosionCollider != null)
            direction = (Vector2)transform.position - (Vector2)explosionCollider.transform.position;

        return TryBounceSkull(direction, 1f, explosionCollider, SkullBounceExplosionProtectionSeconds);
    }

    public bool StartKickedBombPushSegment(
        Vector2 direction,
        float tileSize,
        Collider2D ignoredCollider,
        Vector2 bombSegmentStart,
        int distanceTilesFromBomb = 1)
    {
        return UpdateKickedBombPushSegment(
            direction,
            tileSize,
            ignoredCollider,
            bombSegmentStart,
            0f,
            distanceTilesFromBomb);
    }

    public bool UpdateKickedBombPushSegment(
        Vector2 direction,
        float tileSize,
        Collider2D ignoredCollider,
        Vector2 bombSegmentStart,
        float progress,
        int distanceTilesFromBomb = 1)
    {
        if (type != ItemType.Skull)
            return false;

        if (isBeingDestroyed)
            return true;

        direction = NormalizeCardinalOrDown(direction);
        tileSize = Mathf.Max(0.0001f, tileSize);

        EnsureKickedBombPushActive(direction, tileSize, bombSegmentStart);

        skullBounceSuppressUntil = Mathf.Max(
            skullBounceSuppressUntil,
            Time.time + SkullBounceDefaultProtectionSeconds);

        int distanceTiles = Mathf.Max(1, distanceTilesFromBomb);
        Vector2 segmentStart = bombSegmentStart + direction * (tileSize * distanceTiles);
        if (!TryStepSkullWithWrap(segmentStart, direction, tileSize, out var segmentEnd))
            segmentEnd = segmentStart + direction * tileSize;

        SetKickedBombPushPose(segmentStart, segmentEnd, direction, progress);
        return true;
    }

    public bool TryMoveSkullInFrontOfKickedBomb(
        Vector2 direction,
        float tileSize,
        Collider2D ignoredCollider,
        Vector2 bombWorldCenter,
        int distanceTilesFromBomb,
        bool finishPush)
    {
        if (type != ItemType.Skull)
            return false;

        if (isBeingDestroyed)
            return true;

        direction = NormalizeCardinalOrDown(direction);
        tileSize = Mathf.Max(0.0001f, tileSize);

        EnsureKickedBombPushActive(direction, tileSize, bombWorldCenter);

        skullBounceSuppressUntil = Mathf.Max(
            skullBounceSuppressUntil,
            Time.time + SkullBounceDefaultProtectionSeconds);

        int distanceTiles = Mathf.Max(1, distanceTilesFromBomb);
        Vector2 front = bombWorldCenter + direction * (tileSize * distanceTiles);
        SetSkullWorldPosition(front, syncPhysics: true);

        if (!finishPush)
            return true;

        if (skullBounceRoutine != null)
            StopCoroutine(skullBounceRoutine);

        skullBounceRoutine = StartCoroutine(KickedBombPushFinishRoutine(
            front,
            direction,
            tileSize,
            ignoredCollider,
            bombWorldCenter));
        return true;
    }

    void EnsureKickedBombPushActive(Vector2 direction, float tileSize, Vector2 sourcePosition)
    {
        if (skullBounceRoutine != null)
        {
            StopCoroutine(skullBounceRoutine);
            skullBounceRoutine = null;
            transform.localRotation = Quaternion.identity;
            RestoreSkullBounceFlipState();
        }

        if (skullKickedBombPushActive)
            return;

        ClearSkullBouncePositionLock();
        skullKickedBombPushActive = true;
        skullBounceMoving = true;

        if (_col != null)
            _col.enabled = false;

        CacheSkullBounceFlipState();
        ResolveSkullBounceTilemapsIfNeeded();

        LogSkullBounce(
            $"kick-push start item:{GetDebugIdentity()} source:{FormatVec(sourcePosition)} " +
            $"dir:{FormatVec(direction)} tileSize:{tileSize:F2}");
    }

    void SetKickedBombPushPose(Vector2 start, Vector2 end, Vector2 direction, float progress)
    {
        progress = Mathf.Clamp01(progress);

        bool horizontal = Mathf.Abs(direction.x) >= Mathf.Abs(direction.y);
        bool wrapped = IsSkullWrappedSegment(start, end, horizontal);

        Vector2 pos = wrapped
            ? (progress < 0.5f ? start : end)
            : Vector2.Lerp(start, end, Mathf.SmoothStep(0f, 1f, progress));

        SetSkullWorldPosition(pos, syncPhysics: true);

        float angle = progress * 360f;
        if (horizontal)
        {
            float signedAngle = direction.x >= 0f ? -angle : angle;
            transform.localRotation = Quaternion.Euler(0f, signedAngle, 0f);
        }
        else
        {
            float signedAngle = direction.y >= 0f ? angle : -angle;
            transform.localRotation = Quaternion.Euler(signedAngle, 0f, 0f);
        }

        if (progress >= 1f)
            transform.localRotation = Quaternion.identity;
    }

    static bool IsSkullWrappedSegment(Vector2 start, Vector2 end, bool horizontal)
    {
        return horizontal
            ? Mathf.Abs(end.x - start.x) > 1.5f
            : Mathf.Abs(end.y - start.y) > 1.5f;
    }

    Vector2 ResolveKickedBombPushLanding(
        Vector2 front,
        Vector2 direction,
        float tileSize,
        Collider2D ignoredCollider)
    {
        Vector2 current = SnapSkullToTileCenter(front, tileSize);

        if (IsSkullLandingSafe(current, tileSize, ignoredCollider, out _))
            return current;

        int maxSteps = GetSkullBounceMaxSteps(direction);
        for (int step = 0; step < maxSteps; step++)
        {
            if (!TryStepSkullWithWrap(current, direction, tileSize, out var next))
                break;

            current = next;

            if (IsSkullLandingSafe(current, tileSize, ignoredCollider, out _))
                return current;
        }

        return SnapSkullToTileCenter(front, tileSize);
    }

    IEnumerator KickedBombPushFinishRoutine(
        Vector2 front,
        Vector2 direction,
        float tileSize,
        Collider2D ignoredCollider,
        Vector2 bombWorldCenter)
    {
        Vector2 current = SnapSkullToTileCenter(front, tileSize);
        SetSkullWorldPosition(current, syncPhysics: true);

        bool landed = IsSkullLandingSafe(current, tileSize, ignoredCollider, out _);

        if (!landed)
        {
            int maxSteps = GetSkullBounceMaxSteps(direction);
            for (int step = 0; step < maxSteps; step++)
            {
                if (!TryStepSkullWithWrap(current, direction, tileSize, out var next))
                    break;

                yield return SkullBounceSegment(current, next, direction);

                current = next;
                SetSkullWorldPosition(current, syncPhysics: true);

                if (IsSkullLandingSafe(current, tileSize, ignoredCollider, out _))
                {
                    landed = true;
                    break;
                }
            }
        }

        FinishKickedBombPush(current);

        LogSkullBounce(
            $"kick-push finish item:{GetDebugIdentity()} bomb:{FormatVec(bombWorldCenter)} " +
            $"front:{FormatVec(front)} final:{FormatVec(current)} landed:{landed}");

        skullBounceRoutine = null;
    }

    void FinishKickedBombPush(Vector2 finalPosition)
    {
        transform.localRotation = Quaternion.identity;
        RestoreSkullBounceFlipState();
        SetSkullWorldPosition(finalPosition, syncPhysics: true);
        SyncSkullAnimatedRendererBasePosition();
        StartSkullBouncePositionLock(finalPosition);

        if (_col != null)
            _col.enabled = true;

        Physics2D.SyncTransforms();

        skullBounceMoving = false;
        skullKickedBombPushActive = false;
    }

    IEnumerator SkullBounceRoutine(
        Vector2 direction,
        float tileSize,
        Collider2D ignoredCollider,
        Vector2 origin,
        Vector2 requestedDirection,
        string source)
    {
        skullBounceMoving = true;

        if (_col != null)
            _col.enabled = false;

        CacheSkullBounceFlipState();
        ResolveSkullBounceTilemapsIfNeeded();

        Vector2 current = SnapSkullToTileCenter(transform.position, tileSize);
        SetSkullWorldPosition(current, syncPhysics: true);

        int maxSteps = GetSkullBounceMaxSteps(direction);
        bool landed = false;
        Vector2 finalPosition = current;

        LogSkullBounce(
            $"plan item:{GetDebugIdentity()} origin:{FormatVec(origin)} snappedOrigin:{FormatVec(current)} " +
            $"requestedDir:{FormatVec(requestedDirection)} chosenDir:{FormatVec(direction)} " +
            $"maxSteps:{maxSteps} source:{source}");

        for (int step = 0; step < maxSteps; step++)
        {
            if (!TryStepSkullWithWrap(current, direction, tileSize, out var next))
            {
                LogSkullBounce(
                    $"step:{step + 1} failed-to-step item:{GetDebugIdentity()} from:{FormatVec(current)} " +
                    $"dir:{FormatVec(direction)}");
                break;
            }

            LogSkullBounce(
                $"try step:{step + 1} item:{GetDebugIdentity()} from:{FormatVec(current)} to:{FormatVec(next)} " +
                $"dir:{FormatVec(direction)}");

            yield return SkullBounceSegment(current, next, direction);

            current = next;
            finalPosition = current;
            SetSkullWorldPosition(current, syncPhysics: true);

            if (IsSkullLandingSafe(current, tileSize, ignoredCollider, out var blockedReason))
            {
                landed = true;
                LogSkullBounce($"landed step:{step + 1} item:{GetDebugIdentity()} pos:{FormatVec(current)}");
                break;
            }

            LogSkullBounce($"skip step:{step + 1} item:{GetDebugIdentity()} pos:{FormatVec(current)} reason:{blockedReason}");
        }

        if (!landed)
        {
            LogSkullBounce($"no safe tile found item:{GetDebugIdentity()} after {maxSteps} steps; staying at:{FormatVec(current)}");
            SetSkullWorldPosition(current, syncPhysics: true);
        }

        transform.localRotation = Quaternion.identity;
        RestoreSkullBounceFlipState();

        SetSkullWorldPosition(current, syncPhysics: true);
        SyncSkullAnimatedRendererBasePosition();
        StartSkullBouncePositionLock(current);

        yield return new WaitForFixedUpdate();

        transform.localRotation = Quaternion.identity;
        SetSkullWorldPosition(current, syncPhysics: true);
        SyncSkullAnimatedRendererBasePosition();
        StartSkullBouncePositionLock(current);

        if (_col != null)
            _col.enabled = true;

        Physics2D.SyncTransforms();

        Vector2 visualPos = transform.position;
        Vector2 physicsPos = GetSkullPhysicsPosition();
        LogSkullBounce(
            $"finish item:{GetDebugIdentity()} origin:{FormatVec(origin)} final:{FormatVec(finalPosition)} " +
            $"visual:{FormatVec(visualPos)} physics:{FormatVec(physicsPos)} landed:{landed}");

        skullBounceMoving = false;
        skullBounceRoutine = null;
    }

    IEnumerator SkullExpelRoutine(
        Vector2 direction,
        float tileSize,
        Collider2D ignoredCollider,
        Vector2 origin,
        int forcedSteps)
    {
        skullBounceMoving = true;

        if (_col != null)
            _col.enabled = false;

        CacheSkullBounceFlipState();
        ResolveSkullBounceTilemapsIfNeeded();

        Vector2 current = SnapSkullToTileCenter(transform.position, tileSize);
        SetSkullWorldPosition(current, syncPhysics: true);

        int maxSteps = GetSkullBounceMaxSteps(direction);
        int steps = Mathf.Min(Mathf.Max(1, forcedSteps), maxSteps);
        bool landed = false;

        for (int step = 0; step < steps; step++)
        {
            if (!TryStepSkullWithWrap(current, direction, tileSize, out var next))
                break;

            yield return SkullBounceSegment(current, next, direction);
            current = next;
            SetSkullWorldPosition(current, syncPhysics: true);
        }

        if (IsSkullLandingSafe(current, tileSize, ignoredCollider, out _))
        {
            landed = true;
        }
        else
        {
            for (int step = steps; step < maxSteps; step++)
            {
                if (!TryStepSkullWithWrap(current, direction, tileSize, out var next))
                    break;

                yield return SkullBounceSegment(current, next, direction);
                current = next;
                SetSkullWorldPosition(current, syncPhysics: true);

                if (IsSkullLandingSafe(current, tileSize, ignoredCollider, out _))
                {
                    landed = true;
                    break;
                }
            }
        }

        transform.localRotation = Quaternion.identity;
        RestoreSkullBounceFlipState();

        SetSkullWorldPosition(current, syncPhysics: true);
        SyncSkullAnimatedRendererBasePosition();
        StartSkullBouncePositionLock(current);

        yield return new WaitForFixedUpdate();

        transform.localRotation = Quaternion.identity;
        SetSkullWorldPosition(current, syncPhysics: true);
        SyncSkullAnimatedRendererBasePosition();
        StartSkullBouncePositionLock(current);

        if (_col != null)
            _col.enabled = true;

        Physics2D.SyncTransforms();

        LogSkullBounce(
            $"expel finish item:{GetDebugIdentity()} origin:{FormatVec(origin)} " +
            $"final:{FormatVec(current)} landed:{landed}");

        skullBounceMoving = false;
        skullBounceRoutine = null;
    }

    IEnumerator SkullBounceSegment(Vector2 start, Vector2 end, Vector2 direction)
    {
        bool horizontal = Mathf.Abs(direction.x) >= Mathf.Abs(direction.y);

        bool wrapped = horizontal
            ? Mathf.Abs(end.x - start.x) > 1.5f
            : Mathf.Abs(end.y - start.y) > 1.5f;

        if (wrapped)
        {
            float wrapElapsed = 0f;

            while (wrapElapsed < SkullBounceSecondsPerTile)
            {
                wrapElapsed += Time.deltaTime;
                float a = Mathf.Clamp01(wrapElapsed / SkullBounceSecondsPerTile);
                SetKickedBombPushPose(start, end, direction, a);
                yield return null;
            }

            SetSkullWorldPosition(end, syncPhysics: true);
            transform.localRotation = Quaternion.identity;
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < SkullBounceSecondsPerTile)
        {
            elapsed += Time.deltaTime;

            float a = Mathf.Clamp01(elapsed / SkullBounceSecondsPerTile);
            float eased = Mathf.SmoothStep(0f, 1f, a);

            Vector2 pos = Vector2.Lerp(start, end, eased);
            SetSkullWorldPosition(pos);

            float angle = a * 360f;

            if (horizontal)
            {
                float signedAngle = direction.x >= 0f ? -angle : angle;
                transform.localRotation = Quaternion.Euler(0f, signedAngle, 0f);
            }
            else
            {
                float signedAngle = direction.y >= 0f ? angle : -angle;
                transform.localRotation = Quaternion.Euler(signedAngle, 0f, 0f);
            }

            yield return null;
        }

        SetSkullWorldPosition(end);
        transform.localRotation = Quaternion.identity;
    }

    public void DestroyWithAnimation()
    {
        PlayDestroyAnimation(destroyRenderer);
    }

    public void DestroyWithExplosionAnimation()
    {
        if (type == ItemType.Skull)
            return;

        if (IsLouieEgg(type) && explosionDestroyRenderer != null && destroyRenderer != null)
        {
            PlayDestroyAnimationSequence(explosionDestroyRenderer, destroyRenderer);
            return;
        }

        PlayDestroyAnimation(explosionDestroyRenderer != null ? explosionDestroyRenderer : destroyRenderer);
    }

    void PlayDestroyAnimation(AnimatedSpriteRenderer rendererToPlay)
    {
        if (isBeingDestroyed)
            return;

        isBeingDestroyed = true;

        if (_col != null)
            _col.enabled = false;

        if (idleRenderer != null)
        {
            idleRenderer.enabled = false;
            EnableSpriteBranch(idleRenderer, false);
        }

        if (destroyRenderer != null)
        {
            destroyRenderer.enabled = false;
            EnableSpriteBranch(destroyRenderer, false);
        }

        if (explosionDestroyRenderer != null)
        {
            explosionDestroyRenderer.enabled = false;
            EnableSpriteBranch(explosionDestroyRenderer, false);
        }

        if (rendererToPlay != null)
        {
            rendererToPlay.enabled = true;
            EnableSpriteBranch(rendererToPlay, true);

            rendererToPlay.idle = false;
            rendererToPlay.loop = false;
            rendererToPlay.pingPong = false;
            rendererToPlay.CurrentFrame = 0;
            rendererToPlay.RefreshFrame();
        }

        Destroy(gameObject, Mathf.Max(0.05f, destroyDelaySeconds));
    }

    void PlayDestroyAnimationSequence(AnimatedSpriteRenderer firstRenderer, AnimatedSpriteRenderer secondRenderer)
    {
        if (isBeingDestroyed)
            return;

        isBeingDestroyed = true;

        if (_col != null)
            _col.enabled = false;

        if (idleRenderer != null)
        {
            idleRenderer.enabled = false;
            EnableSpriteBranch(idleRenderer, false);
        }

        if (destroyRenderer != null)
        {
            destroyRenderer.enabled = false;
            EnableSpriteBranch(destroyRenderer, false);
        }

        if (explosionDestroyRenderer != null)
        {
            explosionDestroyRenderer.enabled = false;
            EnableSpriteBranch(explosionDestroyRenderer, false);
        }

        StartCoroutine(PlayDestroyAnimationSequenceRoutine(firstRenderer, secondRenderer));
    }

    IEnumerator PlayDestroyAnimationSequenceRoutine(AnimatedSpriteRenderer firstRenderer, AnimatedSpriteRenderer secondRenderer)
    {
        PlayDestroyRenderer(firstRenderer);

        float firstDuration = GetDestroyAnimationDuration(firstRenderer);
        if (firstDuration > 0f)
            yield return new WaitForSeconds(firstDuration);

        HideDestroyRenderer(firstRenderer);
        PlayDestroyRenderer(secondRenderer);

        float secondDuration = GetDestroyAnimationDuration(secondRenderer);
        Destroy(gameObject, Mathf.Max(0.05f, Mathf.Max(secondDuration, destroyDelaySeconds)));
    }

    void PlayDestroyRenderer(AnimatedSpriteRenderer rendererToPlay)
    {
        if (rendererToPlay == null)
            return;

        rendererToPlay.enabled = true;
        EnableSpriteBranch(rendererToPlay, true);

        rendererToPlay.idle = false;
        rendererToPlay.loop = false;
        rendererToPlay.pingPong = false;
        rendererToPlay.CurrentFrame = 0;
        rendererToPlay.RefreshFrame();
    }

    static void HideDestroyRenderer(AnimatedSpriteRenderer rendererToHide)
    {
        if (rendererToHide == null)
            return;

        rendererToHide.enabled = false;
        EnableSpriteBranch(rendererToHide, false);
    }

    static float GetDestroyAnimationDuration(AnimatedSpriteRenderer renderer)
    {
        if (renderer == null)
            return 0f;

        if (renderer.useSequenceDuration)
            return Mathf.Max(0.0001f, renderer.sequenceDuration);

        int frameCount = renderer.animationSprite != null ? renderer.animationSprite.Length : 0;
        return Mathf.Max(0.0001f, renderer.animationTime) * Mathf.Max(1, frameCount);
    }

    static void EnableSpriteBranch(Component root, bool enabled)
    {
        if (root == null) return;

        if (root.TryGetComponent<SpriteRenderer>(out var sr) && sr != null)
            sr.enabled = enabled;

        var childSrs = root.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < childSrs.Length; i++)
            if (childSrs[i] != null)
                childSrs[i].enabled = enabled;
    }

    bool TryMountEggWithArc(GameObject player, Vector2 mountFacing)
    {
        if (player == null)
            return false;

        if (!player.TryGetComponent<PlayerMountCompanion>(out var companion) || companion == null)
            return false;

        MountedType mountedType = EggToMountedType(type);
        if (mountedType == MountedType.None)
            return false;

        GameObject prefab = companion.GetMountPrefabForType(mountedType);
        if (prefab == null)
            return false;

        Vector3 spawnWorldPos = ResolveEggMountSpawnWorldPosition();
        Vector3 startWorldPos = player.transform.position;

        GameObject louieWorld = Instantiate(prefab, spawnWorldPos, Quaternion.identity);
        PrepareSpawnedLouieWorldForPickup(louieWorld, mountedType, mountFacing);

        TrySetMountSfxForImmediateMount(player);

        return companion.TryMountExistingLouieFromWorldWithArc(
            louieWorldInstance: louieWorld,
            louieType: mountedType,
            worldQueueToAdopt: null,
            startWorldPos: startWorldPos,
            targetWorldPos: spawnWorldPos
        );
    }

    Vector3 ResolveEggMountSpawnWorldPosition()
    {
        Vector3 p = _col != null ? _col.bounds.center : transform.position;
        p.z = 0f;
        return p;
    }

    void PrepareSpawnedLouieWorldForPickup(GameObject louieWorld, MountedType mountedType, Vector2 facingDirection)
    {
        if (louieWorld == null)
            return;

        var pickup = louieWorld.GetComponent<MountWorldPickup>();
        if (pickup == null)
            pickup = louieWorld.AddComponent<MountWorldPickup>();

        pickup.Init(mountedType);

        if (louieWorld.TryGetComponent<Collider2D>(out var col) && col != null)
            col.enabled = true;

        if (louieWorld.TryGetComponent<Rigidbody2D>(out var rb) && rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.simulated = true;
        }

        ApplyLouieIdleFacing(louieWorld, facingDirection);

        if (louieWorld.TryGetComponent<MountMovementController>(out var lm) && lm != null)
            lm.enabled = false;

        if (louieWorld.TryGetComponent<BombController>(out var bc) && bc != null)
            bc.enabled = false;

        if (louieWorld.TryGetComponent<MovementController>(out var mc) && mc != null)
            mc.SetExplosionInvulnerable(false);
    }

    MountedType EggToMountedType(ItemType eggType)
    {
        switch (eggType)
        {
            case ItemType.BlueLouieEgg: return MountedType.Blue;
            case ItemType.BlackLouieEgg: return MountedType.Black;
            case ItemType.PurpleLouieEgg: return MountedType.Purple;
            case ItemType.GreenLouieEgg: return MountedType.Green;
            case ItemType.YellowLouieEgg: return MountedType.Yellow;
            case ItemType.PinkLouieEgg: return MountedType.Pink;
            case ItemType.RedLouieEgg: return MountedType.Red;
            default: return MountedType.None;
        }
    }

    void ApplyLouieIdleFacing(GameObject louieWorld, Vector2 facingDirection)
    {
        if (louieWorld == null)
            return;

        if (!louieWorld.TryGetComponent<MovementController>(out var mv) || mv == null)
            return;

        Vector2 face = facingDirection;

        if (Mathf.Abs(face.x) >= Mathf.Abs(face.y))
            face = face.x >= 0f ? Vector2.right : Vector2.left;
        else
            face = face.y >= 0f ? Vector2.up : Vector2.down;

        mv.ForceIdleFacing(face, "EggSpawnIdleFacing");
        mv.EnableExclusiveFromState();
    }

    public void DestroySilently()
    {
        if (type == ItemType.Skull)
            return;

        if (isBeingDestroyed)
            return;

        isBeingDestroyed = true;

        if (_col != null)
            _col.enabled = false;

        Destroy(gameObject);
    }

    static Vector2 NormalizeCardinalOrDown(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.0001f)
            return Vector2.down;

        if (Mathf.Abs(direction.x) >= Mathf.Abs(direction.y))
            return direction.x >= 0f ? Vector2.right : Vector2.left;

        return direction.y >= 0f ? Vector2.up : Vector2.down;
    }

    void SetSkullWorldPosition(Vector2 position, bool syncPhysics = false)
    {
        Vector3 p = transform.position;
        p.x = position.x;
        p.y = position.y;

        transform.position = p;

        // IMPORTANTE:
        // O AnimatedSpriteRenderer está no mesmo GameObject do item.
        // Se ele usar frameOffsets, ele pode restaurar o localPosition antigo.
        // Então atualizamos a base visual para a posição local atual.
        SyncSkullAnimatedRendererBasePosition();

        if (syncPhysics)
            Physics2D.SyncTransforms();
    }

    void SyncSkullAnimatedRendererBasePosition()
    {
        if (type != ItemType.Skull)
            return;

        Vector3 localPos = transform.localPosition;

        if (idleRenderer != null)
            idleRenderer.SetExternalBaseLocalPosition(localPos);

        if (destroyRenderer != null)
            destroyRenderer.SetExternalBaseLocalPosition(localPos);

        if (explosionDestroyRenderer != null)
            explosionDestroyRenderer.SetExternalBaseLocalPosition(localPos);
    }

    bool IsColliderActuallyTouchingCurrentItem(
        Collider2D other,
        out Vector2 itemCenter,
        out Vector2 playerCenter,
        out float centerDistance,
        out float closestDistance)
    {
        itemCenter = GetSkullLogicalPosition();
        playerCenter = Vector2.zero;
        centerDistance = 0f;
        closestDistance = 0f;

        if (other == null)
            return true;

        Physics2D.SyncTransforms();

        playerCenter = other.attachedRigidbody != null
            ? other.attachedRigidbody.position
            : (Vector2)other.bounds.center;

        centerDistance = Vector2.Distance(itemCenter, playerCenter);
        if (centerDistance <= SkullPickupCenterDistance)
            return true;

        Vector2 playerClosest = other.ClosestPoint(itemCenter);
        closestDistance = Vector2.Distance(itemCenter, playerClosest);
        return closestDistance <= SkullPickupClosestDistance;
    }

    Vector2 GetSkullLogicalPosition()
    {
        if (IsSkullBouncePositionLockActive())
            return skullBounceLockedWorldPosition;

        return transform.position;
    }

    Vector2 GetSkullPhysicsPosition()
    {
        return TryGetComponent<Rigidbody2D>(out var rb) && rb != null
            ? rb.position
            : (Vector2)transform.position;
    }

    void StartSkullBouncePositionLock(Vector2 position)
    {
        if (type != ItemType.Skull)
            return;

        skullBouncePositionLocked = true;
        skullBounceLockedWorldPosition = position;
        skullBouncePositionLockUntil = Mathf.Max(
            Mathf.Max(Time.time + SkullBouncePositionLockSeconds, skullBounceSuppressUntil),
            skullBouncePositionLockUntil);
    }

    void ClearSkullBouncePositionLock()
    {
        skullBouncePositionLocked = false;
        skullBouncePositionLockUntil = 0f;
    }

    bool IsSkullBouncePositionLockActive()
    {
        return skullBouncePositionLocked && Time.time <= skullBouncePositionLockUntil;
    }

    float GetSkullBouncePositionLockRemaining()
    {
        return IsSkullBouncePositionLockActive()
            ? Mathf.Max(0f, skullBouncePositionLockUntil - Time.time)
            : 0f;
    }

    void EnforceSkullBouncePositionLock()
    {
        if (type != ItemType.Skull || !skullBouncePositionLocked)
            return;

        if (isBeingDestroyed)
        {
            ClearSkullBouncePositionLock();
            return;
        }

        if (!IsSkullBouncePositionLockActive())
        {
            ClearSkullBouncePositionLock();
            return;
        }

        if (skullBounceMoving)
            return;

        Vector2 visualPosition = transform.position;
        if ((visualPosition - skullBounceLockedWorldPosition).sqrMagnitude <= SkullBouncePositionDriftEpsilon * SkullBouncePositionDriftEpsilon)
            return;

        Vector2 physicsPosition = GetSkullPhysicsPosition();
        Vector2 localPosition = transform.localPosition;
        string parentName = transform.parent != null ? transform.parent.name : "<none>";

        LogSkullBounce(
            $"position drift corrected item:{GetDebugIdentity()} expected:{FormatVec(skullBounceLockedWorldPosition)} " +
            $"visual:{FormatVec(visualPosition)} physics:{FormatVec(physicsPosition)} " +
            $"local:{FormatVec(localPosition)} parent:{parentName} " +
            $"remaining:{GetSkullBouncePositionLockRemaining():F2}s");

        SetSkullWorldPosition(skullBounceLockedWorldPosition, syncPhysics: true);
    }

    void CacheSkullBounceFlipState()
    {
        skullBounceRenderers = GetComponentsInChildren<SpriteRenderer>(true);

        int count = skullBounceRenderers != null ? skullBounceRenderers.Length : 0;

        if (skullBounceOriginalFlipX == null || skullBounceOriginalFlipX.Length != count)
        {
            skullBounceOriginalFlipX = new bool[count];
            skullBounceOriginalFlipY = new bool[count];
            skullBounceOriginalSortingOrders = new int[count];
        }

        for (int i = 0; i < count; i++)
        {
            var sr = skullBounceRenderers[i];
            if (sr == null)
                continue;

            skullBounceOriginalFlipX[i] = sr.flipX;
            skullBounceOriginalFlipY[i] = sr.flipY;
            skullBounceOriginalSortingOrders[i] = sr.sortingOrder;

            sr.sortingOrder = Mathf.Max(sr.sortingOrder, 3);
        }
    }

    void ApplySkullBounceFlip(Vector2 direction, bool flipped)
    {
        if (skullBounceRenderers == null)
            return;

        bool horizontal = Mathf.Abs(direction.x) >= Mathf.Abs(direction.y);

        for (int i = 0; i < skullBounceRenderers.Length; i++)
        {
            var sr = skullBounceRenderers[i];
            if (sr == null)
                continue;

            if (horizontal)
            {
                sr.flipX = skullBounceOriginalFlipX[i] ^ flipped;
                sr.flipY = skullBounceOriginalFlipY[i];
            }
            else
            {
                sr.flipX = skullBounceOriginalFlipX[i];
                sr.flipY = skullBounceOriginalFlipY[i] ^ flipped;
            }
        }
    }

    void RestoreSkullBounceFlipState()
    {
        if (skullBounceRenderers == null)
            return;

        for (int i = 0; i < skullBounceRenderers.Length; i++)
        {
            var sr = skullBounceRenderers[i];
            if (sr == null)
                continue;

            if (skullBounceOriginalFlipX != null && i < skullBounceOriginalFlipX.Length)
                sr.flipX = skullBounceOriginalFlipX[i];

            if (skullBounceOriginalFlipY != null && i < skullBounceOriginalFlipY.Length)
                sr.flipY = skullBounceOriginalFlipY[i];

            if (skullBounceOriginalSortingOrders != null && i < skullBounceOriginalSortingOrders.Length)
                sr.sortingOrder = skullBounceOriginalSortingOrders[i];
        }
    }

    void ResolveSkullBounceTilemapsIfNeeded()
    {
        if (skullTilemapsResolved)
            return;

        GameManager gm = GameManager.Instance != null
            ? GameManager.Instance
            : FindAnyObjectByType<GameManager>();

        if (gm != null)
        {
            skullGroundTilemap = gm.groundTilemap;
            skullDestructibleTilemap = gm.destructibleTilemap;
            skullIndestructibleTilemap = gm.indestructibleTilemap;
        }

        skullGroundTilemap ??= FindTilemapByNameContains("ground");
        skullDestructibleTilemap ??= FindTilemapByNameContains("destruct", "indestruct");
        skullIndestructibleTilemap ??= FindTilemapByNameContains("indestruct");
        skullWaterTilemap ??= FindTilemapByNameContains("water");
        skullHoleTilemap ??= FindTilemapByNameContains("hole");

        skullStageBoundsTilemap = skullGroundTilemap != null
            ? skullGroundTilemap
            : (skullIndestructibleTilemap != null ? skullIndestructibleTilemap : skullDestructibleTilemap);

        skullTilemapsResolved = true;
        skullStageBoundsReady = false;
    }

    static Tilemap FindTilemapByNameContains(string namePart, string excludedPart = null)
    {
        var tilemaps = FindObjectsByType<Tilemap>(FindObjectsInactive.Include);
        if (tilemaps == null || tilemaps.Length == 0)
            return null;

        for (int i = 0; i < tilemaps.Length; i++)
        {
            var tm = tilemaps[i];
            if (tm == null)
                continue;

            string n = tm.name.ToLowerInvariant();
            if (!string.IsNullOrEmpty(excludedPart) && n.Contains(excludedPart))
                continue;

            if (n.Contains(namePart))
                return tm;
        }

        return null;
    }

    void EnsureSkullStageBounds()
    {
        if (skullStageBoundsReady)
            return;

        ResolveSkullBounceTilemapsIfNeeded();

        bool hasAnyBounds = false;
        BoundsInt bounds = default;

        AddSkullBounds(skullGroundTilemap, ref bounds, ref hasAnyBounds);
        AddSkullBounds(skullIndestructibleTilemap, ref bounds, ref hasAnyBounds);
        AddSkullBounds(skullDestructibleTilemap, ref bounds, ref hasAnyBounds);

        if (!hasAnyBounds)
            return;

        skullStageCellBounds = bounds;
        skullStageBoundsReady = true;
    }

    static void AddSkullBounds(Tilemap tilemap, ref BoundsInt bounds, ref bool hasAnyBounds)
    {
        if (tilemap == null)
            return;

        tilemap.CompressBounds();
        BoundsInt b = tilemap.cellBounds;

        if (!hasAnyBounds)
        {
            bounds = b;
            hasAnyBounds = true;
            return;
        }

        int xMin = Mathf.Min(bounds.xMin, b.xMin);
        int xMax = Mathf.Max(bounds.xMax, b.xMax);
        int yMin = Mathf.Min(bounds.yMin, b.yMin);
        int yMax = Mathf.Max(bounds.yMax, b.yMax);

        bounds = new BoundsInt(xMin, yMin, 0, xMax - xMin, yMax - yMin, 1);
    }

    int GetSkullBounceMaxSteps(Vector2 direction)
    {
        EnsureSkullStageBounds();

        if (!skullStageBoundsReady)
            return SkullBounceFallbackMaxSteps;

        bool horizontal = Mathf.Abs(direction.x) >= Mathf.Abs(direction.y);
        return Mathf.Max(1, horizontal ? skullStageCellBounds.size.x : skullStageCellBounds.size.y);
    }

    bool TryStepSkullWithWrap(Vector2 from, Vector2 direction, float tileSize, out Vector2 next)
    {
        EnsureSkullStageBounds();

        Vector2 raw = from + direction * tileSize;

        if (!skullStageBoundsReady)
        {
            next = raw;
            return true;
        }

        Tilemap tm = skullStageBoundsTilemap;
        Vector3Int cell = tm != null
            ? tm.WorldToCell(raw)
            : new Vector3Int(Mathf.RoundToInt(raw.x / tileSize), Mathf.RoundToInt(raw.y / tileSize), 0);

        int minX = skullStageCellBounds.xMin;
        int maxX = skullStageCellBounds.xMax - 1;
        int minY = skullStageCellBounds.yMin;
        int maxY = skullStageCellBounds.yMax - 1;

        if (cell.x < minX) cell.x = maxX;
        else if (cell.x > maxX) cell.x = minX;

        if (cell.y < minY) cell.y = maxY;
        else if (cell.y > maxY) cell.y = minY;

        if (tm != null)
        {
            Vector3 center = tm.GetCellCenterWorld(cell);
            next = new Vector2(center.x, center.y);
        }
        else
        {
            next = new Vector2(cell.x * tileSize, cell.y * tileSize);
        }

        return true;
    }

    Vector2 SnapSkullToTileCenter(Vector2 worldPos, float tileSize)
    {
        ResolveSkullBounceTilemapsIfNeeded();

        Tilemap tm = skullGroundTilemap != null
            ? skullGroundTilemap
            : skullStageBoundsTilemap;

        if (tm != null)
        {
            Vector3Int cell = tm.WorldToCell(worldPos);
            Vector3 center = tm.GetCellCenterWorld(cell);
            return new Vector2(center.x, center.y);
        }

        return new Vector2(
            Mathf.Round(worldPos.x / tileSize) * tileSize,
            Mathf.Round(worldPos.y / tileSize) * tileSize);
    }

    bool IsSkullLandingSafe(Vector2 worldCenter, float tileSize, Collider2D ignoredCollider, out string blockedReason)
    {
        if (skullGroundTilemap != null && !HasSkullTileAt(skullGroundTilemap, worldCenter))
        {
            blockedReason = "no-ground";
            return false;
        }

        if (HasSkullTileAt(skullIndestructibleTilemap, worldCenter))
        {
            blockedReason = "indestructible";
            return false;
        }

        if (HasSkullTileAt(skullDestructibleTilemap, worldCenter))
        {
            blockedReason = "destructible";
            return false;
        }

        if (HasSkullTileAt(skullWaterTilemap, worldCenter))
        {
            blockedReason = "water";
            return false;
        }

        if (HasSkullTileAt(skullHoleTilemap, worldCenter))
        {
            blockedReason = "hole";
            return false;
        }

        if (HasSkullBlockingColliderAt(worldCenter, tileSize, ignoredCollider, out blockedReason))
            return false;

        blockedReason = null;
        return true;
    }

    static bool HasSkullTileAt(Tilemap tilemap, Vector2 worldCenter)
    {
        if (tilemap == null)
            return false;

        Vector3Int cell = tilemap.WorldToCell(worldCenter);
        return tilemap.GetTile(cell) != null;
    }

    bool HasSkullBlockingColliderAt(Vector2 worldCenter, float tileSize, Collider2D ignoredCollider, out string blockedReason)
    {
        Vector2 size = Vector2.one * (tileSize * 0.55f);
        ContactFilter2D filter = new()
        {
            useTriggers = true,
            useLayerMask = false
        };
        int count = Physics2D.OverlapBox(worldCenter, size, 0f, filter, SkullBounceOverlapBuffer);

        int bombLayer = LayerMask.NameToLayer("Bomb");
        int playerLayer = LayerMask.NameToLayer("Player");
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        int louieLayer = LayerMask.NameToLayer("Louie");
        int explosionLayer = LayerMask.NameToLayer("Explosion");

        for (int i = 0; i < count; i++)
        {
            Collider2D hit = SkullBounceOverlapBuffer[i];
            SkullBounceOverlapBuffer[i] = null;

            if (hit == null)
                continue;

            if (ignoredCollider != null && hit == ignoredCollider)
                continue;

            if (hit.gameObject == gameObject)
                continue;

            if (hit.transform == transform || hit.transform.IsChildOf(transform))
                continue;

            var pickup = hit.GetComponent<ItemPickup>() ?? hit.GetComponentInParent<ItemPickup>();
            if (pickup == this)
                continue;

            int layer = hit.gameObject.layer;
            if (layer == bombLayer ||
                layer == playerLayer ||
                layer == enemyLayer ||
                layer == louieLayer ||
                layer == explosionLayer)
            {
                blockedReason = LayerMask.LayerToName(layer);
                return true;
            }

            if (pickup != null)
            {
                blockedReason = $"item:{pickup.type}#{pickup.GetEntityId()}";
                return true;
            }
        }

        blockedReason = null;
        return false;
    }

    void LogSkullBounce(string message)
    {
        if (!debugSkullBounce)
            return;

        Debug.Log($"[SkullBounce] {name} {message}", this);
    }

    static string FormatVec(Vector2 value)
    {
        return $"({value.x:F2},{value.y:F2})";
    }

    static string FormatBounds(Bounds bounds)
    {
        return $"center:{FormatVec(bounds.center)} size:{FormatVec(bounds.size)}";
    }

    string GetDebugIdentity()
    {
        return $"{type}#{GetEntityId()}";
    }
}

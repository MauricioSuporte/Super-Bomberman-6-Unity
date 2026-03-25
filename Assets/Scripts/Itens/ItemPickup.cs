using System;
using UnityEngine;

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
        if (player.TryGetComponent<MovementController>(out var movement) && movement.IsMountedOnLouie)
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

        var audio = player.GetComponent<AudioSource>();
        if (audio != null)
            audio.PlayOneShot(playerExtraSfx, Mathf.Clamp01(playerExtraVolume));
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
        if (playDestroyAnim)
            DestroyWithAnimation();
        else
            Destroy(gameObject);
    }

    public void Consume(bool playDestroyAnim) => ConsumeNow(playDestroyAnim);
    public bool TryApplyDamageLikeEnemyContact(GameObject player, int damage) => TryApplyPickupDamageLikeEnemyContact(player, damage);

    bool TryApplyPickupDamageLikeEnemyContact(GameObject player, int damage)
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

            if (mv.IsMountedOnLouie)
            {
                if (player.TryGetComponent<PlayerMountCompanion>(out var companion) && companion != null)
                {
                    companion.OnMountedLouieHit(damage, fromExplosion: false);
                    return true;
                }
            }
        }

        if (health != null)
        {
            health.TakeDamage(damage);
            return true;
        }

        if (mv != null)
        {
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
                ConsumeNow(false);
                return;
            }

            return;
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
            pid = Mathf.Clamp(id.playerId, 1, 4);

        if (type != ItemType.LandMine && type != ItemType.Clock)
            PlayerPersistentStats.StageApplyPickup(pid, type);

        switch (type)
        {
            case ItemType.Clock:
                ClockStageStunEffect.Trigger(5f);
                break;

            case ItemType.LandMine:
                TryApplyPickupDamageLikeEnemyContact(player, 1);
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
                    break;
                }

            case ItemType.ControlBomb:
                {
                    var ab = GetOrCreateAbilitySystem(player);
                    ab.Enable(ControlBombAbility.AbilityId);
                    ab.Disable(PierceBombAbility.AbilityId);
                    ab.Disable(PowerBombAbility.AbilityId);
                    ab.Disable(RubberBombAbility.AbilityId);
                    break;
                }

            case ItemType.PowerBomb:
                {
                    var ab = GetOrCreateAbilitySystem(player);
                    ab.Enable(PowerBombAbility.AbilityId);
                    ab.Disable(PierceBombAbility.AbilityId);
                    ab.Disable(ControlBombAbility.AbilityId);
                    ab.Disable(RubberBombAbility.AbilityId);
                    break;
                }

            case ItemType.RubberBomb:
                {
                    var ab = GetOrCreateAbilitySystem(player);
                    ab.Enable(RubberBombAbility.AbilityId);
                    ab.Disable(PierceBombAbility.AbilityId);
                    ab.Disable(ControlBombAbility.AbilityId);
                    ab.Disable(PowerBombAbility.AbilityId);
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
            var player = other.gameObject;

            if (IsLouieEgg(type))
            {
                if (PlayerHoldingBombWithPowerGlove(player))
                    return;
            }

            OnItemPickup(player);
            return;
        }

        if (other.CompareTag("Explosion"))
        {
            if (IsSpawnImmune())
                return;

            DestroyWithExplosionAnimation();
        }
    }

    public void DestroyWithAnimation()
    {
        PlayDestroyAnimation(destroyRenderer);
    }

    public void DestroyWithExplosionAnimation()
    {
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
}
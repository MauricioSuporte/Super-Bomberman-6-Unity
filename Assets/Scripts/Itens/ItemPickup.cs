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
        bool isEgg = IsLouieEgg(type);

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
            TrySetMountSfxForImmediateMount(player);
        }
        else
        {
            PlayCollectSfxOnPlayer(player);
            PlayPlayerExtraSfx(player);

            if (_behavior != null)
            {
                if (_behavior.OnPickedUp(this, player))
                    return;
            }
        }

        int pid = 1;
        if (player.TryGetComponent<PlayerIdentity>(out var id) && id != null)
            pid = Mathf.Clamp(id.playerId, 1, 4);

        if (type != ItemType.LandMine)
            PlayerPersistentStats.StageApplyPickup(pid, type);

        switch (type)
        {
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
                    break;
                }

            case ItemType.ControlBomb:
                {
                    var ab = GetOrCreateAbilitySystem(player);
                    ab.Enable(ControlBombAbility.AbilityId);
                    ab.Disable(PierceBombAbility.AbilityId);
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

            case ItemType.BlueLouieEgg:
                if (player.TryGetComponent<PlayerMountCompanion>(out var louieBlue))
                    louieBlue.MountBlueLouie();
                break;

            case ItemType.BlackLouieEgg:
                if (player.TryGetComponent<PlayerMountCompanion>(out var louieBlack))
                    louieBlack.MountBlackLouie();
                break;

            case ItemType.PurpleLouieEgg:
                if (player.TryGetComponent<PlayerMountCompanion>(out var louiePurple))
                    louiePurple.MountPurpleLouie();
                break;

            case ItemType.GreenLouieEgg:
                if (player.TryGetComponent<PlayerMountCompanion>(out var louieGreen))
                    louieGreen.MountGreenLouie();
                break;

            case ItemType.YellowLouieEgg:
                if (player.TryGetComponent<PlayerMountCompanion>(out var louieYellow))
                    louieYellow.MountYellowLouie();
                break;

            case ItemType.PinkLouieEgg:
                if (player.TryGetComponent<PlayerMountCompanion>(out var louiePink))
                    louiePink.MountPinkLouie();
                break;

            case ItemType.RedLouieEgg:
                if (player.TryGetComponent<PlayerMountCompanion>(out var louieRed))
                    louieRed.MountRedLouie();
                break;
        }

        bool playDestroyAnim = isEgg || type == ItemType.LandMine;
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

                if (!PlayerAlreadyMounted(player))
                {
                    if (other.TryGetComponent<MovementController>(out var mv) && mv != null && _col != null)
                        mv.SnapToColliderCenter(_col, false);
                }
            }

            OnItemPickup(player);
            return;
        }

        if (other.CompareTag("Explosion"))
        {
            if (IsSpawnImmune())
                return;

            DestroyWithAnimation();
        }
    }

    public void DestroyWithAnimation()
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
            destroyRenderer.enabled = true;
            EnableSpriteBranch(destroyRenderer, true);

            destroyRenderer.idle = false;
            destroyRenderer.loop = false;
            destroyRenderer.pingPong = false;
            destroyRenderer.CurrentFrame = 0;
            destroyRenderer.RefreshFrame();
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
}
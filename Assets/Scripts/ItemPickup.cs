using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public class ItemPickup : MonoBehaviour
{
    [Header("SFX")]
    public AudioClip collectSfx;

    [Range(0f, 1f)]
    public float collectVolume = 1f;

    public AnimatedSpriteRenderer idleRenderer;
    public AnimatedSpriteRenderer destroyRenderer;

    [Header("Spawn Immunity")]
    public float spawnImmunitySeconds = 0.5f;

    public enum ItemType
    {
        ExtraBomb,
        BlastRadius,
        SpeedIncrese,
        BombKick,
        BombPunch,
        PierceBomb,
        ControlBomb,
        FullFire,
        BombPass,
        DestructiblePass,
        InvincibleSuit,
        Heart,
        BlueLouieEgg,
        BlackLouieEgg,
        PurpleLouieEgg,
        GreenLouieEgg,
        YellowLouieEgg,
        PinkLouieEgg,
        RedLouieEgg
    }

    public ItemType type;

    bool isBeingDestroyed;
    float spawnTime;
    Collider2D _col;

    void Awake()
    {
        spawnTime = Time.time;
        _col = GetComponent<Collider2D>();
    }

    bool IsSpawnImmune() => Time.time - spawnTime < spawnImmunitySeconds;

    bool IsLouieEgg(ItemType t)
    {
        return t == ItemType.BlueLouieEgg
            || t == ItemType.BlackLouieEgg
            || t == ItemType.PurpleLouieEgg
            || t == ItemType.GreenLouieEgg
            || t == ItemType.YellowLouieEgg
            || t == ItemType.PinkLouieEgg
            || t == ItemType.RedLouieEgg;
    }

    bool PlayerAlreadyMounted(GameObject player)
    {
        if (player.TryGetComponent<MovementController>(out var movement) && movement.IsMountedOnLouie)
            return true;

        if (player.TryGetComponent<PlayerLouieCompanion>(out var louieCompanion) && louieCompanion != null)
            return louieCompanion.GetMountedLouieType() != MountedLouieType.None;

        return false;
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

    LouieEggQueue GetOrCreateEggQueue(GameObject player)
    {
        if (!player.TryGetComponent<LouieEggQueue>(out var q) || q == null)
            q = player.AddComponent<LouieEggQueue>();

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

    bool TrySetMountSfxForImmediateMount(GameObject player)
    {
        if (collectSfx == null)
            return false;

        if (!player.TryGetComponent<PlayerLouieCompanion>(out var companion) || companion == null)
            return false;

        companion.SetNextMountSfx(collectSfx, collectVolume);
        return true;
    }

    void OnItemPickup(GameObject player)
    {
        bool isEgg = IsLouieEgg(type);

        if (isEgg)
        {
            if (PlayerAlreadyMounted(player))
            {
                var q = GetOrCreateEggQueue(player);

                if (!q.TryEnqueue(type, GetEggIdleSpriteFallback(), collectSfx, collectVolume))
                    return;

                Destroy(gameObject);
                return;
            }

            TrySetMountSfxForImmediateMount(player);
        }
        else
        {
            PlayCollectSfxOnPlayer(player);
        }

        switch (type)
        {
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
                if (player.TryGetComponent<PlayerLouieCompanion>(out var louieBlue))
                    louieBlue.MountBlueLouie();
                break;

            case ItemType.BlackLouieEgg:
                if (player.TryGetComponent<PlayerLouieCompanion>(out var louieBlack))
                    louieBlack.MountBlackLouie();
                break;

            case ItemType.PurpleLouieEgg:
                if (player.TryGetComponent<PlayerLouieCompanion>(out var louiePurple))
                    louiePurple.MountPurpleLouie();
                break;

            case ItemType.GreenLouieEgg:
                if (player.TryGetComponent<PlayerLouieCompanion>(out var louieGreen))
                    louieGreen.MountGreenLouie();
                break;

            case ItemType.YellowLouieEgg:
                if (player.TryGetComponent<PlayerLouieCompanion>(out var louieYellow))
                    louieYellow.MountYellowLouie();
                break;

            case ItemType.PinkLouieEgg:
                if (player.TryGetComponent<PlayerLouieCompanion>(out var louiePink))
                    louiePink.MountPinkLouie();
                break;

            case ItemType.RedLouieEgg:
                if (player.TryGetComponent<PlayerLouieCompanion>(out var louieRed))
                    louieRed.MountRedLouie();
                break;
        }

        Destroy(gameObject);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null)
            return;

        if (other.CompareTag("Player"))
        {
            if (other.TryGetComponent<MovementController>(out var mv) && mv != null && _col != null)
                mv.SnapToColliderCenter(_col, roundToGrid: false);

            OnItemPickup(other.gameObject);
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

        if (idleRenderer != null)
            idleRenderer.enabled = false;

        if (destroyRenderer != null)
        {
            destroyRenderer.enabled = true;
            destroyRenderer.idle = false;
        }

        Destroy(gameObject, 0.5f);
    }
}

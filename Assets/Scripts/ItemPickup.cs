using UnityEngine;

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
        PurpleLouieEgg
    }

    public ItemType type;

    private bool isBeingDestroyed = false;
    private float spawnTime;

    private void Awake()
    {
        spawnTime = Time.time;
    }

    private bool IsSpawnImmune()
    {
        return Time.time - spawnTime < spawnImmunitySeconds;
    }

    private bool IsLouieEgg(ItemType t)
    {
        return t == ItemType.BlueLouieEgg || t == ItemType.BlackLouieEgg || t == ItemType.PurpleLouieEgg;
    }

    private bool PlayerAlreadyMounted(GameObject player)
    {
        if (player.TryGetComponent<MovementController>(out var movement) && movement.IsMountedOnLouie)
            return true;

        if (player.TryGetComponent<PlayerLouieCompanion>(out var louieCompanion))
            return louieCompanion.GetMountedLouieType() != PlayerPersistentStats.MountedLouieType.None;

        return false;
    }

    private void OnItemPickup(GameObject player)
    {
        if (IsLouieEgg(type) && PlayerAlreadyMounted(player))
            return;

        var audio = player.GetComponent<AudioSource>();
        if (audio != null && collectSfx != null)
            audio.PlayOneShot(collectSfx, collectVolume);

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
                    var mv = player.GetComponent<MovementController>();
                    if (mv != null && mv.speed < PlayerPersistentStats.MaxSpeed)
                        mv.speed = Mathf.Min(mv.speed + 1f, PlayerPersistentStats.MaxSpeed);
                    break;
                }

            case ItemType.BombKick:
                {
                    if (!player.TryGetComponent<AbilitySystem>(out var abilitySystem))
                        abilitySystem = player.AddComponent<AbilitySystem>();

                    abilitySystem.Enable(BombKickAbility.AbilityId);
                    break;
                }

            case ItemType.BombPunch:
                {
                    if (!player.TryGetComponent<AbilitySystem>(out var abilitySystem))
                        abilitySystem = player.AddComponent<AbilitySystem>();

                    abilitySystem.Enable(BombPunchAbility.AbilityId);
                    break;
                }

            case ItemType.PierceBomb:
                {
                    if (!player.TryGetComponent<AbilitySystem>(out var abilitySystem))
                        abilitySystem = player.AddComponent<AbilitySystem>();

                    abilitySystem.RebuildCache();
                    abilitySystem.Enable(PierceBombAbility.AbilityId);
                    abilitySystem.Disable(ControlBombAbility.AbilityId);
                    break;
                }

            case ItemType.ControlBomb:
                {
                    if (!player.TryGetComponent<AbilitySystem>(out var abilitySystem))
                        abilitySystem = player.AddComponent<AbilitySystem>();

                    abilitySystem.RebuildCache();
                    abilitySystem.Enable(ControlBombAbility.AbilityId);
                    abilitySystem.Disable(PierceBombAbility.AbilityId);
                    break;
                }

            case ItemType.FullFire:
                {
                    if (!player.TryGetComponent<AbilitySystem>(out var abilitySystem))
                        abilitySystem = player.AddComponent<AbilitySystem>();

                    abilitySystem.RebuildCache();
                    abilitySystem.Enable(FullFireAbility.AbilityId);
                    break;
                }

            case ItemType.BombPass:
                {
                    if (!player.TryGetComponent<AbilitySystem>(out var abilitySystem))
                        abilitySystem = player.AddComponent<AbilitySystem>();

                    abilitySystem.RebuildCache();
                    abilitySystem.Enable(BombPassAbility.AbilityId);
                    break;
                }

            case ItemType.DestructiblePass:
                {
                    if (!player.TryGetComponent<AbilitySystem>(out var abilitySystem))
                        abilitySystem = player.AddComponent<AbilitySystem>();

                    abilitySystem.RebuildCache();
                    abilitySystem.Enable(DestructiblePassAbility.AbilityId);
                    break;
                }

            case ItemType.InvincibleSuit:
                {
                    if (!player.TryGetComponent<AbilitySystem>(out var abilitySystem))
                        abilitySystem = player.AddComponent<AbilitySystem>();

                    abilitySystem.RebuildCache();
                    abilitySystem.Enable(InvincibleSuitAbility.AbilityId);
                    break;
                }

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
        }

        Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
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

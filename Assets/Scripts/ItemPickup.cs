using UnityEngine;

public class ItemPickup : MonoBehaviour
{
    [Header("SFX")]
    public AudioClip collectSfx;

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

    private void OnItemPickup(GameObject player)
    {
        var audio = player.GetComponent<AudioSource>();
        if (audio != null && collectSfx != null)
            audio.PlayOneShot(collectSfx);

        switch (type)
        {
            case ItemType.ExtraBomb:
                if (player.TryGetComponent<BombController>(out var bombController))
                    bombController.AddBomb();
                break;

            case ItemType.BlastRadius:
                {
                    var bombController2 = player.GetComponent<BombController>();
                    if (bombController2 != null && bombController2.explosionRadius < PlayerPersistentStats.MaxExplosionRadius)
                    {
                        bombController2.explosionRadius = Mathf.Min(
                            bombController2.explosionRadius + 1,
                            PlayerPersistentStats.MaxExplosionRadius
                        );
                    }
                    break;
                }

            case ItemType.SpeedIncrese:
                {
                    var movement = player.GetComponent<MovementController>();
                    if (movement != null && movement.speed < PlayerPersistentStats.MaxSpeed)
                    {
                        movement.speed = Mathf.Min(
                            movement.speed + 1f,
                            PlayerPersistentStats.MaxSpeed
                        );
                    }
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

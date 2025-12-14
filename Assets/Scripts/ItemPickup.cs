using UnityEngine;

public class ItemPickup : MonoBehaviour
{
    [Header("SFX")]
    public AudioClip collectSfx;

    public AnimatedSpriteRenderer idleRenderer;
    public AnimatedSpriteRenderer destroyRenderer;

    public enum ItemType
    {
        ExtraBomb,
        BlastRadius,
        SpeedIncrese,
        BombKick
    }

    public ItemType type;

    private bool isBeingDestroyed = false;

    private void OnItemPickup(GameObject player)
    {
        var audio = player.GetComponent<AudioSource>();
        if (audio != null && collectSfx != null)
            audio.PlayOneShot(collectSfx);

        switch (type)
        {
            case ItemType.ExtraBomb:
                {
                    if (player.TryGetComponent<BombController>(out var bomb))
                        bomb.AddBomb();
                    break;
                }

            case ItemType.BlastRadius:
                {
                    var bomb = player.GetComponent<BombController>();
                    if (bomb != null && bomb.explosionRadius < PlayerPersistentStats.MaxExplosionRadius)
                    {
                        bomb.explosionRadius = Mathf.Min(
                            bomb.explosionRadius + 1,
                            PlayerPersistentStats.MaxExplosionRadius);
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
                            PlayerPersistentStats.MaxSpeed);
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
        }

        Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            OnItemPickup(other.gameObject);
        }
        else if (other.CompareTag("Explosion"))
        {
            DestroyWithAnimation();
        }
    }

    public void DestroyWithAnimation()
    {
        if (isBeingDestroyed)
            return;

        isBeingDestroyed = true;

        idleRenderer.enabled = false;

        destroyRenderer.enabled = true;
        destroyRenderer.idle = false;

        Destroy(gameObject, 0.5f);
    }
}

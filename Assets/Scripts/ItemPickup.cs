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
        {
            audio.PlayOneShot(collectSfx);
        }

        switch (type)
        {
            case ItemType.ExtraBomb:
                player.GetComponent<BombController>().AddBomb();
                break;

            case ItemType.BlastRadius:
                player.GetComponent<BombController>().explosionRadius++;
                break;

            case ItemType.SpeedIncrese:
                player.GetComponent<MovementController>().speed++;
                break;

            case ItemType.BombKick:
                player.GetComponent<MovementController>().EnableBombKick();
                break;
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

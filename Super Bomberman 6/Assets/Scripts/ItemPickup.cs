using UnityEngine;

public class ItemPickup : MonoBehaviour
{
    public enum ItemType
    {
        ExtraBomb,
        BlastRadius,
        SpeedIncrese
    }

    public ItemType type;

    public AnimatedSpriteRenderer idleRenderer;
    public AnimatedSpriteRenderer destroyRenderer;

    private bool isBeingDestroyed = false;

    private void OnItemPickup(GameObject player)
    {
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

    private void DestroyWithAnimation()
    {
        if (isBeingDestroyed) return;
        isBeingDestroyed = true;

        // desativa sprite normal do item
        idleRenderer.enabled = false;

        // ativa animação de destruição
        destroyRenderer.enabled = true;
        destroyRenderer.idle = false;

        // destrói depois de 0.5s (ou tempo que quiser)
        Destroy(gameObject, 0.5f);
    }
}

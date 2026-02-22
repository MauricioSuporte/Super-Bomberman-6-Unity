using UnityEngine;

public sealed class LandMineBehavior : MonoBehaviour, IItemPickupBehavior
{
    [Header("Damage")]
    [SerializeField, Min(1)] private int damage = 1;

    [Header("Consume")]
    [SerializeField] private bool playDestroyAnimation = true;

    public bool OnPickedUp(ItemPickup pickup, GameObject player)
    {
        if (pickup == null || player == null)
            return true;

        pickup.TryApplyDamageLikeEnemyContact(player, damage);
        pickup.Consume(playDestroyAnimation);

        return true;
    }
}
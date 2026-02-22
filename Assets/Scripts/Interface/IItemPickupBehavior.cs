using UnityEngine;

public interface IItemPickupBehavior
{
    bool OnPickedUp(ItemPickup pickup, GameObject player);
}
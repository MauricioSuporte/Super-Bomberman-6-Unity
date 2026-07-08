using UnityEngine;

public static class BattleModeComPickupObstacleUtility
{
    public static bool IsComMounted(GameObject owner)
    {
        if (owner == null)
            return false;

        if (owner.TryGetComponent<MovementController>(out var movement) &&
            movement != null &&
            movement.IsMounted)
        {
            return true;
        }

        return owner.TryGetComponent<PlayerMountCompanion>(out var companion) &&
               companion != null &&
               companion.HasMountedLouie();
    }

    public static bool IsDeniedMountPickupColliderForMountedCom(Collider2D hit, GameObject owner)
    {
        if (hit == null || !IsComMounted(owner))
            return false;

        ItemPickup item = hit.GetComponentInParent<ItemPickup>();
        if (item != null && IsLouieEggItem(item.type))
            return true;

        MountWorldPickup worldMount = hit.GetComponentInParent<MountWorldPickup>();
        return worldMount != null && worldMount.IsAvailable;
    }

    public static bool IsIgnorablePickupCollider(Collider2D hit, GameObject owner)
    {
        if (hit == null)
            return false;

        if (IsDeniedMountPickupColliderForMountedCom(hit, owner))
            return false;

        return hit.GetComponentInParent<ItemPickup>() != null ||
               hit.GetComponentInParent<MountWorldPickup>() != null;
    }

    static bool IsLouieEggItem(ItemType type)
    {
        return type == ItemType.BlueLouieEgg ||
               type == ItemType.BlackLouieEgg ||
               type == ItemType.PurpleLouieEgg ||
               type == ItemType.GreenLouieEgg ||
               type == ItemType.YellowLouieEgg ||
               type == ItemType.PinkLouieEgg ||
               type == ItemType.RedLouieEgg;
    }
}

using System.Collections.Generic;
using UnityEngine;

public sealed partial class BattleRevengeSystem
{
    public Vector2 NetworkMuzzleOffset(Vector2 direction) => GetRevengeLaunchMuzzleVfxOffset(direction);
    public float NetworkMuzzleDuration => revengeLaunchMuzzleVfxDuration;
    public int NetworkMuzzleOrder => revengeLaunchMuzzleVfxOrderInLayer;

    public void CopyNetworkCarts(List<BattleRevengeController> target)
    {
        target.Clear();
        foreach (var cart in activeCartsByOwner.Values)
            if (cart != null && cart.gameObject.activeInHierarchy) target.Add(cart);
    }

    public BattleRevengeController CreateNetworkCartView(MovementController source)
    {
        if (cartPrefab == null || source == null) return null;
        var cart = Instantiate(cartPrefab);
        cart.InitializeNetworkView(this, source);
        return cart;
    }
}

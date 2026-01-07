using System;
using System.Collections.Generic;
using UnityEngine;

public static class AutoItemDatabase
{
    static Dictionary<ItemPickup.ItemType, ItemPickup> itemPrefabs;

    public static void BuildIfNeeded()
    {
        if (itemPrefabs != null)
            return;

        itemPrefabs = new Dictionary<ItemPickup.ItemType, ItemPickup>();

        var allItems = Resources.LoadAll<ItemPickup>("Items");

        foreach (var item in allItems)
        {
            if (!itemPrefabs.ContainsKey(item.type))
                itemPrefabs.Add(item.type, item);
        }
    }

    public static ItemPickup Get(ItemPickup.ItemType type)
    {
        BuildIfNeeded();
        return itemPrefabs.TryGetValue(type, out var prefab)
            ? prefab
            : null;
    }
}

using System.Collections.Generic;
using UnityEngine;

public static class AutoItemDatabase
{
    static Dictionary<ItemType, ItemPickup> itemPrefabs;

    public static void BuildIfNeeded()
    {
        if (itemPrefabs != null)
            return;

        itemPrefabs = new Dictionary<ItemType, ItemPickup>();

        var allItems = Resources.LoadAll<ItemPickup>("Items");

        foreach (var item in allItems)
        {
            if (item == null)
                continue;

            if (!itemPrefabs.ContainsKey(item.type))
                itemPrefabs.Add(item.type, item);
        }
    }

    public static ItemPickup Get(ItemType type)
    {
        BuildIfNeeded();

        return itemPrefabs.TryGetValue(type, out var prefab)
            ? prefab
            : null;
    }
}
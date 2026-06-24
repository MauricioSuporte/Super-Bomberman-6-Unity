using UnityEngine;

public static class LouieEggVisualColor
{
    public static Color GetForItemType(ItemType type)
    {
        switch (type)
        {
            case ItemType.BlueLouieEgg: return new Color(0.35f, 0.75f, 1f, 1f);
            case ItemType.BlackLouieEgg: return new Color(0.22f, 0.22f, 0.26f, 1f);
            case ItemType.PurpleLouieEgg: return new Color(0.72f, 0.45f, 1f, 1f);
            case ItemType.GreenLouieEgg: return Color.white;
            case ItemType.YellowLouieEgg: return new Color(1f, 0.92f, 0.28f, 1f);
            case ItemType.PinkLouieEgg: return new Color(1f, 0.58f, 0.82f, 1f);
            case ItemType.RedLouieEgg: return new Color(1f, 0.34f, 0.28f, 1f);
            default: return Color.white;
        }
    }

    public static bool IsLouieEgg(ItemType type)
    {
        return type == ItemType.BlueLouieEgg
            || type == ItemType.BlackLouieEgg
            || type == ItemType.PurpleLouieEgg
            || type == ItemType.GreenLouieEgg
            || type == ItemType.YellowLouieEgg
            || type == ItemType.PinkLouieEgg
            || type == ItemType.RedLouieEgg;
    }

    public static void ApplyTo(Transform root, ItemType type)
    {
        if (root == null || !IsLouieEgg(type))
            return;

        Color color = GetForItemType(type);
        var renderers = root.GetComponentsInChildren<SpriteRenderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            var sr = renderers[i];
            if (sr != null)
                sr.color = color;
        }
    }
}

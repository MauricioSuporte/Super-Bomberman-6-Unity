using UnityEngine;

// The native launchers and approved client presentation use the same geometry.
// No collision, input, RNG, protection or landing callbacks live in this value.
public struct PlayerLaunchArc
{
    public enum LaunchKind { Spring, Cannon, BattleSpring, Rope }
    public LaunchKind kind;
    public bool groundShadow;
    public Color shadowColor;
    public Vector2 shadowScale;
    public int shadowOrder;
    public Vector2 start, end;
    public float duration, height, ascend, descend, tileSize;
    public Vector4 room; // minX, minY, maxX, maxY
    public bool bounded, rootCarriesHeight, plateau, quantizeVisual;

    public Vector2 Ground(float elapsed)
    {
        float t = Progress(elapsed);
        if (kind == LaunchKind.Rope) t = 1f - Mathf.Pow(1f - t, 2f);
        Vector2 point = Wrap(Vector2.Lerp(start, end, t), bounded, room, tileSize);
        return kind == LaunchKind.Rope ? new Vector2(Mathf.Round(point.x * 16f) / 16f, Mathf.Round(point.y * 16f) / 16f) : point;
    }
    public float Progress(float elapsed) => Mathf.Clamp01(elapsed / Mathf.Max(.0001f, duration));
    public float Height(float elapsed)
    {
        float t = Progress(elapsed), value;
        if (plateau)
        {
            float up = Mathf.Clamp(ascend, 0, duration), down = Mathf.Clamp(descend, 0, duration - up);
            value = elapsed >= duration ? 0 : up > 0 && elapsed < up ? Mathf.Lerp(0, height, Mathf.Clamp01(elapsed / up)) :
                down > 0 && elapsed >= duration - down ? Mathf.Lerp(height, 0, Mathf.Clamp01((elapsed - duration + down) / down)) : height;
        }
        else value = height * (4f * t * (1f - t));
        return ClampHeight(Ground(elapsed).y, value, bounded, room, tileSize);
    }
    public float VisualHeight(float elapsed)
    {
        if (rootCarriesHeight) return 0;
        float value = Height(elapsed);
        return quantizeVisual ? Mathf.Round(value * 16f) / 16f : value;
    }
    public Vector2 Position(float elapsed) => Ground(elapsed) + (rootCarriesHeight ? Vector2.up * Height(elapsed) : Vector2.zero);

    public static Vector4 Room(Collider2D bounds)
    {
        if (bounds == null) return default;
        var b = bounds.bounds; return new Vector4(b.min.x, b.min.y, b.max.x, b.max.y);
    }
    public static Vector2 Wrap(Vector2 position, bool bounded, Vector4 room, float tileSize)
    {
        if (!bounded) return position;
        float width = room.z - room.x, safeTile = Mathf.Max(.0001f, tileSize);
        if (width > 0)
        {
            while (position.x < room.x) position.x += width;
            while (position.x > room.z) position.x -= width;
        }
        if (room.w - room.y > safeTile)
        {
            if (position.y < room.y) position.y = Mathf.Floor((room.w - safeTile) / safeTile) * safeTile;
            else if (position.y > room.w) position.y = Mathf.Ceil((room.y + safeTile) / safeTile) * safeTile;
        }
        return position;
    }
    public static float ClampHeight(float groundY, float height, bool bounded, Vector4 room, float tileSize)
    {
        if (!bounded) return height;
        float safeTile = Mathf.Max(.0001f, tileSize);
        return Mathf.Clamp(groundY + height, room.y + safeTile, room.w - safeTile) - groundY;
    }
}

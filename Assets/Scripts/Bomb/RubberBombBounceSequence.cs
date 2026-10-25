using UnityEngine;

// Stable across offline, server and client: no Unity RNG, instance ids or clock.
public static class RubberBombBounceSequence
{
    static uint Mix(uint value)
    {
        unchecked
        {
            value ^= value >> 16; value *= 0x7feb352dU;
            value ^= value >> 15; value *= 0x846ca68bU;
            return value ^ (value >> 16);
        }
    }

    public static uint Seed(Vector2 origin, float tileSize, Vector2 direction, int distance)
    {
        float tile = Mathf.Max(0.0001f, tileSize);
        unchecked
        {
            uint x = (uint)Mathf.RoundToInt(origin.x / tile), y = (uint)Mathf.RoundToInt(origin.y / tile);
            uint facing = direction.x > 0 ? 0U : direction.y > 0 ? 1U : direction.x < 0 ? 2U : 3U;
            return Mix(x * 73856093U ^ y * 19349663U ^ facing * 83492791U ^ (uint)Mathf.Max(1, distance));
        }
    }

    public static int MinimumBounces(uint seed) => 3 + (int)(seed % 3);

    public static Vector2 Direction(uint seed, int index)
    {
        uint step;
        unchecked { step = Mix(seed + (uint)Mathf.Max(0, index) * 0x9e3779b9U); }
        return (step & 3) switch { 0 => Vector2.up, 1 => Vector2.down, 2 => Vector2.left, _ => Vector2.right };
    }
}

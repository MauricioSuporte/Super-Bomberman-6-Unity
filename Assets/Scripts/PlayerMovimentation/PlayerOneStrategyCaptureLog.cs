using System.Collections.Generic;
using UnityEngine;

public static class PlayerOneStrategyCaptureLog
{
    private const bool Enabled = true;
    private const int TargetPlayerId = 1;
    private const string Prefix = "[P1StrategyCapture]";

    private struct MovementSnapshot
    {
        public Vector2Int Tile;
        public Vector2 Direction;
        public Vector2 Facing;
        public bool HasValue;
    }

    private static readonly Dictionary<int, MovementSnapshot> MovementSnapshots = new();

    public static bool ShouldLog(int playerId)
    {
        return Enabled && playerId == TargetPlayerId;
    }

    public static void MovementSample(MovementController movement)
    {
        if (movement == null || !ShouldLog(movement.PlayerId))
            return;

        Vector2 pos = movement.Rigidbody != null
            ? movement.Rigidbody.position
            : (Vector2)movement.transform.position;

        Vector2Int tile = WorldToTile(pos, movement.tileSize);
        Vector2 dir = NormalizeCardinal(movement.Direction);
        Vector2 facing = NormalizeCardinal(movement.FacingDirection);

        MovementSnapshots.TryGetValue(movement.PlayerId, out MovementSnapshot last);
        if (last.HasValue &&
            last.Tile == tile &&
            last.Direction == dir &&
            last.Facing == facing)
        {
            return;
        }

        MovementSnapshots[movement.PlayerId] = new MovementSnapshot
        {
            Tile = tile,
            Direction = dir,
            Facing = facing,
            HasValue = true
        };

        Log(
            "MOVE",
            movement.PlayerId,
            movement,
            $"tile:{tile} dir:{DirectionLabel(dir)} facing:{DirectionLabel(facing)} ownBombs:{CountOwnBombs(movement.PlayerId)}");
    }

    public static void BombPlanted(BombController owner, Bomb bomb, Vector2 position, Vector2 plantDirection)
    {
        if (owner == null || !ShouldLog(owner.PlayerId))
            return;

        Log(
            "PLANT",
            owner.PlayerId,
            owner,
            $"tile:{WorldToTile(position, GetTileSize(owner))} dir:{DirectionLabel(plantDirection)} " +
            $"bomb:{BombLabel(bomb)} remaining:{owner.BombsRemaining} radius:{owner.explosionRadius}");
    }

    public static void YellowKickInput(MovementController movement, Vector2 direction, bool hasBombInFront, bool hasTileInFront)
    {
        if (movement == null || !ShouldLog(movement.PlayerId))
            return;

        Log(
            "YELLOW_KICK_INPUT",
            movement.PlayerId,
            movement,
            $"tile:{WorldToTile(movement.transform.position, movement.tileSize)} dir:{DirectionLabel(direction)} " +
            $"bombFront:{hasBombInFront} destructibleFront:{hasTileInFront} ownBombs:{CountOwnBombs(movement.PlayerId)}");
    }

    public static void YellowQueueStart(MovementController movement, IReadOnlyList<Bomb> queue, Vector2 kickDirection, float tileSize)
    {
        if (movement == null || !ShouldLog(movement.PlayerId))
            return;

        Log(
            "YELLOW_QUEUE",
            movement.PlayerId,
            movement,
            $"dir:{DirectionLabel(kickDirection)} count:{(queue != null ? queue.Count : 0)} bombs:{FormatBombQueue(queue, tileSize)}");
    }

    public static void YellowKickEnd(MovementController movement, IReadOnlyList<Bomb> queue, Vector2 kickDirection, float tileSize)
    {
        if (movement == null || !ShouldLog(movement.PlayerId))
            return;

        Log(
            "YELLOW_KICK_END",
            movement.PlayerId,
            movement,
            $"dir:{DirectionLabel(kickDirection)} bombs:{FormatBombQueue(queue, tileSize)} ownBombs:{CountOwnBombs(movement.PlayerId)}");
    }

    private static void Log(string key, int playerId, Object context, string message)
    {
        Debug.Log($"{Prefix}[P{playerId}] t:{Time.time:F2} f:{Time.frameCount} {key} {message}", context);
    }

    private static float GetTileSize(BombController owner)
    {
        MovementController movement = owner.GetComponent<MovementController>();
        return movement != null ? movement.tileSize : 1f;
    }

    private static int CountOwnBombs(int playerId)
    {
        int count = 0;
        foreach (Bomb bomb in Bomb.ActiveBombs)
        {
            if (bomb == null || bomb.HasExploded || bomb.Owner == null)
                continue;

            if (bomb.Owner.PlayerId == playerId)
                count++;
        }

        return count;
    }

    private static string FormatBombQueue(IReadOnlyList<Bomb> queue, float tileSize)
    {
        if (queue == null || queue.Count == 0)
            return "none";

        string result = string.Empty;
        for (int i = 0; i < queue.Count; i++)
        {
            Bomb bomb = queue[i];
            string part = BombLabel(bomb, tileSize);
            result = string.IsNullOrEmpty(result) ? part : $"{result}->{part}";
        }

        return result;
    }

    private static string BombLabel(Bomb bomb, float tileSize = 1f)
    {
        if (bomb == null)
            return "null";

        return $"{bomb.name}@{WorldToTile(bomb.GetLogicalPosition(), tileSize)}";
    }

    private static Vector2Int WorldToTile(Vector2 world, float tileSize)
    {
        tileSize = Mathf.Max(0.0001f, tileSize);
        return new Vector2Int(
            Mathf.RoundToInt(world.x / tileSize),
            Mathf.RoundToInt(world.y / tileSize));
    }

    private static Vector2 NormalizeCardinal(Vector2 direction)
    {
        if (direction == Vector2.zero)
            return Vector2.zero;

        if (Mathf.Abs(direction.x) >= Mathf.Abs(direction.y))
            return direction.x > 0f ? Vector2.right : Vector2.left;

        return direction.y > 0f ? Vector2.up : Vector2.down;
    }

    private static string DirectionLabel(Vector2 direction)
    {
        direction = NormalizeCardinal(direction);
        if (direction == Vector2.right) return "Right";
        if (direction == Vector2.left) return "Left";
        if (direction == Vector2.up) return "Up";
        if (direction == Vector2.down) return "Down";
        return "None";
    }
}

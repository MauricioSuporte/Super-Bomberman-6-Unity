using UnityEngine;

public interface IBattleModeComKickTrajectoryProvider
{
    float OffensiveKickChanceMultiplier { get; }
    int OffensiveKickWeightBonus { get; }
    float OffensiveKickCooldownSeconds { get; }
    int MaxSequentialKickBombs { get; }
    float RepeatKickChance { get; }

    bool TryGetRedirectedKickDirection(
        Vector2Int tile,
        Vector2Int incomingDirection,
        out Vector2Int redirectedDirection);
}

using System;
using UnityEngine;

[Serializable]
public sealed class BattleModeComDifficultySettings
{
    public BattleModeComputerLevel difficulty;
    public float decisionInterval = 0.2f;
    public float dangerDecisionInterval = 0.06f;
    public int searchDepth = 8;
    public float dangerReactionSeconds = 0.12f;
    public float safeTileMinimumSeconds = 0.35f;
    [Range(0f, 1f)] public float hesitationChance;

    public int stoppedWeight;
    public int patrolWeight;
    public int collectItemWeight;
    public int farmDestructibleWeight;
    public int combatPlantWeight;

    public static BattleModeComDifficultySettings For(BattleModeComputerLevel level)
    {
        switch (level)
        {
            case BattleModeComputerLevel.Easy:
                return new BattleModeComDifficultySettings
                {
                    difficulty = level,
                    decisionInterval = 0.36f,
                    dangerDecisionInterval = 0.1f,
                    searchDepth = 6,
                    dangerReactionSeconds = 0.22f,
                    safeTileMinimumSeconds = 0.2f,
                    hesitationChance = 0.12f,
                    stoppedWeight = 8,
                    patrolWeight = 30,
                    collectItemWeight = 35,
                    farmDestructibleWeight = 20,
                    combatPlantWeight = 10
                };

            case BattleModeComputerLevel.Hard:
                return new BattleModeComDifficultySettings
                {
                    difficulty = level,
                    decisionInterval = 0.14f,
                    dangerDecisionInterval = 0.035f,
                    searchDepth = 12,
                    dangerReactionSeconds = 0.04f,
                    safeTileMinimumSeconds = 0.45f,
                    hesitationChance = 0.01f,
                    stoppedWeight = 1,
                    patrolWeight = 12,
                    collectItemWeight = 30,
                    farmDestructibleWeight = 25,
                    combatPlantWeight = 45
                };

            default:
                return new BattleModeComDifficultySettings
                {
                    difficulty = BattleModeComputerLevel.Normal,
                    decisionInterval = 0.22f,
                    dangerDecisionInterval = 0.06f,
                    searchDepth = 9,
                    dangerReactionSeconds = 0.1f,
                    safeTileMinimumSeconds = 0.35f,
                    hesitationChance = 0.04f,
                    stoppedWeight = 3,
                    patrolWeight = 20,
                    collectItemWeight = 35,
                    farmDestructibleWeight = 25,
                    combatPlantWeight = 30
                };
        }
    }
}

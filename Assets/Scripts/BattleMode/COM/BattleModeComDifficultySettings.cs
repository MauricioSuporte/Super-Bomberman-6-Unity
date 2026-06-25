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
    [Range(0f, 1f)] public float escapeAbilityChance = 0.5f;
    [Range(0f, 1f)] public float stageProgressPriorityChance = 1f;
    [Range(0f, 1f)] public float postPlantActionChance = 1f;
    [Range(0f, 1f)] public float advancedBombPlanChance = 1f;

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
                    decisionInterval = 0.28f,
                    dangerDecisionInterval = 0.1f,
                    searchDepth = 6,
                    dangerReactionSeconds = 0.22f,
                    safeTileMinimumSeconds = 0.28f,
                    hesitationChance = 0.06f,
                    escapeAbilityChance = 0.25f,
                    stageProgressPriorityChance = 1f,
                    postPlantActionChance = 0.75f,
                    advancedBombPlanChance = 0f,
                    stoppedWeight = 3,
                    patrolWeight = 12,
                    collectItemWeight = 42,
                    farmDestructibleWeight = 38,
                    combatPlantWeight = 24
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
                    escapeAbilityChance = 1f,
                    stageProgressPriorityChance = 1f,
                    postPlantActionChance = 1f,
                    advancedBombPlanChance = 1f,
                    stoppedWeight = 1,
                    patrolWeight = 8,
                    collectItemWeight = 55,
                    farmDestructibleWeight = 45,
                    combatPlantWeight = 40
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
                    escapeAbilityChance = 0.5f,
                    stageProgressPriorityChance = 1f,
                    postPlantActionChance = 1f,
                    advancedBombPlanChance = 1f,
                    stoppedWeight = 2,
                    patrolWeight = 12,
                    collectItemWeight = 65,
                    farmDestructibleWeight = 50,
                    combatPlantWeight = 25
                };
        }
    }
}

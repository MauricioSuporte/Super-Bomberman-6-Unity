using UnityEngine;

public struct BattleModeComAbilityDecision
{
    public BattleModeComActionType Action;
    public int Weight;
    public Vector2Int TargetTile;
    public bool HasTarget;
    public Vector2 FirstMove;
    public string Reason;
    public string InputDescription;
    public bool TapBomb;
    public bool TapActionA;
    public bool HoldActionA;
    public bool TapActionB;
    public bool TapActionR;
    public bool TapActionC;
    public bool UsesEscapeAbilityChance;
}

using UnityEngine;

public struct BattleModeComAbilityDecision
{
    public BattleModeComActionType Action;
    public int Weight;
    public Vector2Int TargetTile;
    public bool HasTarget;
    public Vector2 FirstMove;
    // Optional in-place facing applied only after this decision wins.
    public Vector2 FaceDirection;
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
